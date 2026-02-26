using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础骑乘模块")]
    public class EntityBasicMountModule : EntityBasicModuleBase, IEntitySupportMotion
    {
        [Title("开关")]
        public bool enableMount = true;

        [Title("状态")]
        [ReadOnly] public bool mountHold;

        [Title("检测")]
        [LabelText("射线起点(可选)")]
        public Transform rayOrigin;

        [LabelText("检测距离")]
        public float mountDistance = 2f;

        [LabelText("检测层")]
        public LayerMask mountLayerMask = ~0;

        [LabelText("触发器命中")]
        public QueryTriggerInteraction mountQuery = QueryTriggerInteraction.Ignore;

        [Title("骑乘目标")]
        [ReadOnly] public EntityMountable currentMount;

        [LabelText("骑乘状态名")]
        public string Mount_StateName = "骑乘";

        private StateBase _mountState;
        private StateMachine sm;

        // ★ 通用生命周期跟踪器：封装 _isActive 幂等守卫 + Update 轮询打断检测。
        //   任何需要"与状态严格同步进入/退出"的模块复用同一套逻辑，无需重复编写。
        private StateLifecycleTracker _lifecycle;

        // TryEnterMount → OnMountEnter 之间的参数桥接（避免闭包捕获堆分配）
        private EntityMountable _pendingMountable;

        public override void Start()
        {
            base.Start();
            if (MyCore != null && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                sm = MyCore.stateDomain.stateMachine;
                _mountState = sm.GetStateByString(Mount_StateName);
            }
            if (MyCore != null)
            {
                MyCore.kcc.mountModule = this;
            }

            // 构造跟踪器并绑定状态（无委托，零 GC）
            _lifecycle = new StateLifecycleTracker();
            _lifecycle.Bind(sm, _mountState, Mount_StateName);
        }

        // ================================================================
        // 对外 API（键盘输入 / 业务逻辑调用）
        // ================================================================

        public void SetMount(bool enable)
        {
            if (!enableMount || _mountState == null) return;
            if (enable) TryEnterMount();
            else if (_lifecycle.RequestExit()) OnMountExit();
        }

        public void ToggleMount()
        {
            if (!enableMount || _mountState == null) return;
            if (_lifecycle.IsActive) { if (_lifecycle.RequestExit()) OnMountExit(); }
            else                     TryEnterMount();
        }

        // ================================================================
        // 骑乘生命周期核心（严格与状态同步，幂等由 StateLifecycleTracker 保证）
        // ================================================================

        private void TryEnterMount()
        {
            if (_lifecycle.IsActive) return;
            var mountable = FindMountable();
            if (mountable == null) return;

            // 将激活结果直接传入，无闭包分配
            if (_lifecycle.TryEnter(sm.TryActivateState(_mountState)))
            {
                _pendingMountable = mountable;
                OnMountEnter();
                _pendingMountable = null;
            }
        }

        /// <summary>
        /// 骑乘 Enter 点：绑定目标、触发 MatchTarget 对齐。
        /// 由 <see cref="StateLifecycleTracker"/> 保证只执行一次。
        /// </summary>
        private void OnMountEnter()
        {
            currentMount = _pendingMountable;
            mountHold    = true;

            // skipImmediateSync=true：不立即传送，让 MatchTarget 做渐近动画对齐；
            // 若 MatchTarget 未配置/失败，ApplyMountMatchTarget 内部 fallback 会直接 StartMatchTarget，
            // 同样需要先不传送，否则起点==终点，位移为零。
            currentMount.Mount(MyCore, skipImmediateSync: true);
            Debug.Log($"[Mount] OnMountEnter 执行 | currentMount={currentMount.name} | matchPoint={(currentMount.matchPoint != null ? currentMount.matchPoint.name : "null")}");

            // ★ 不在代码里调用 sm.SetSupportFlags(Mounted)！
            //   StateMachine 的 Flag 清理（TruelyDeactivateState）只会清除与 Inspector 配置匹配的 Flag。
            //   若由代码强制设置，退出时不会被自动清除，导致 Mounted 旗标在其他状态（如 Climbing）中持续存在，
            //   使 UpdateVelocity 归零速度、climbModule.BeforeCharacterUpdate 不再触发，造成全局 MatchTarget 损坏。
            //   请在 Mount 状态的 Inspector basicConfig → stateSupportFlag 里配置 Mounted，让框架自动管理。

            ApplyMountMatchTarget(currentMount);
        }

        /// <summary>
        /// 骑乘 Exit 点：清理 MatchTarget、解绑目标。
        /// 由 <see cref="StateLifecycleTracker"/> 保证无论何种来源都只执行一次。
        /// </summary>
        private void OnMountExit()
        {
            mountHold = false;

            if (_mountState != null && _mountState.IsMatchTargetActive)
                _mountState.CancelMatchTarget();

            if (currentMount != null)
            {
                currentMount.Unmount();
                currentMount = null;
            }
        }

        // ================================================================
        // MatchTarget 对齐辅助
        // ================================================================

        private void ApplyMountMatchTarget(EntityMountable mountable)
        {
            if (_mountState == null || mountable.matchPoint == null)
            {
                Debug.LogWarning($"[Mount] ApplyMountMatchTarget 跳过 | _mountState={((_mountState == null) ? "null" : _mountState.ToString())} | matchPoint={(mountable.matchPoint == null ? "null" : mountable.matchPoint.name)}");
                return;
            }

            Debug.Log($"[Mount] 尝试 StartMatchTargetFromConfig | matchPoint.pos={mountable.matchPoint.position:F3} | matchPoint.rot={mountable.matchPoint.rotation.eulerAngles:F1}");
            bool ok = _mountState.StartMatchTargetFromConfig(
                mountable.matchPoint.position,
                mountable.matchPoint.rotation);

            Debug.Log($"[Mount] StartMatchTargetFromConfig 结果={ok} | IsMatchTargetActive={_mountState.IsMatchTargetActive}");

            if (!ok)
            {
                Debug.Log("[Mount] Inspector 未配置，走 Fallback 全参）");
                _mountState.StartMatchTarget(
                    mountable.matchPoint.position,
                    mountable.matchPoint.rotation,
                    AvatarTarget.Root,
                    0.05f, 0.6f,
                    3f, 360f);  // approachSpeed=3单位/秒, approachAngleSpeed=360度/秒
                Debug.Log($"[Mount] Fallback 后 IsMatchTargetActive={_mountState.IsMatchTargetActive}");
            }
        }

        private EntityMountable FindMountable()
        {
            if (MyCore == null) return null;

            Transform origin = rayOrigin != null ? rayOrigin : MyCore.transform;
            Vector3 dir = origin.forward;
            if (Physics.Raycast(origin.position, dir, out RaycastHit hit, mountDistance, mountLayerMask, mountQuery))
            {
#if UNITY_EDITOR
                var mountable = hit.collider.GetComponentInParent<EntityMountable>();
                Debug.Log($"[Mount] 射线命中: {hit.collider.name} | 距离={hit.distance:F2} | EntityMountable={(mountable != null ? mountable.name : "null（无法骑乘）")}");
                return mountable;
#else
                return hit.collider.GetComponentInParent<EntityMountable>();
#endif
            }
   Debug.Log($"[Mount] 射线没命中:  |)");
             
            return null;
        }

        protected override void Update()
        {
            if (MyCore == null || !enableMount) return;

            // ★ 每帧轮询：检测状态是否被外部打断（baseStatus ≠ Running）→ 触发 OnMountExit
            //   键盘主动退出时 IsActive 已为 false，Poll() 直接跳过，不会重复清理
            if (_lifecycle.CheckExit()) { OnMountExit(); return; }

            if (!_lifecycle.IsActive) return;

            mountHold = true;
           // MyCore.SetLocomotionSupportFlags(StateSupportFlags.Mounted);
           // currentMount.TickMounted(MyCore, MyCore.kcc.moveInput, MyCore.kcc.lookInput, Time.deltaTime);

            // MatchTarget 激活期间实时修正目标点（载具在移动，matchPoint 每帧都在变）
            // MatchTarget 完成后 IsMatchTargetActive 变 false，自动停止修正，SyncRider 无缝接管
            if (_mountState.IsMatchTargetActive && currentMount != null && currentMount.matchPoint != null)
            {
                // MatchTarget 进行中：每帧修正目标点（载具可能在移动）
                // ★ 改用 PatchMatchTargetWithConfigOffset：
                //   原 PatchMatchTarget(pos, rot) 每帧直接写入 raw 位置，
                //   会覆盖 StartMatchTargetFromConfig 在启动时叠加的 Inspector positionOffset/rotationOffsetEuler。
                //   PatchMatchTargetWithConfigOffset 会自动重新叠加当前阶段的配置偏移，使偏移每帧持续生效。
                _mountState.PatchMatchTargetWithConfigOffset(
                    currentMount.matchPoint.position,
                    currentMount.matchPoint.rotation);
            }
            else if (!_mountState.IsMatchTargetActive && _lifecycle.IsActive && currentMount != null)
            {
                // MatchTarget 已完成，用 MoveTowards 柔性收尾，避免硬 snap 突变。
                // 足够近后（<0.005m & <0.5°）再精确锁定。
                if (currentMount.rider != null && currentMount.rider.kcc != null && currentMount.rider.kcc.motor != null)
                {
                    var motor = currentMount.rider.kcc.motor;
                    Vector3    curPos = motor.TransientPosition;
                    Quaternion curRot = motor.TransientRotation;
                    Vector3    tgtPos = currentMount.alignRiderPosition ? currentMount.matchPoint.position : curPos;
                    Quaternion tgtRot = currentMount.alignRiderRotation ? currentMount.matchPoint.rotation : curRot;

                    float posDist = Vector3.Distance(curPos, tgtPos);
                    float rotDist = Quaternion.Angle(curRot, tgtRot);

                    const float snapPosThr = 0.005f;
                    const float snapRotThr = 0.5f;

                    if (posDist < snapPosThr && rotDist < snapRotThr)
                    {
                        // 误差极小，精确锁定
                     //   motor.SetPositionAndRotation(tgtPos, tgtRot, true);
                    }
                    else
                    {
                        // 仍有残余误差，继续以高速 MoveTowards 收尾（不突变）
                        float dt = Time.deltaTime;
                        Vector3    newPos = Vector3.MoveTowards(curPos, tgtPos, 8f * dt);
                        Quaternion newRot = Quaternion.RotateTowards(curRot, tgtRot, 720f * dt);
                       // motor.SetPositionAndRotation(newPos, newRot, true);
                    }
                }
            }
        }

        public bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime)
        {
            if (!enableMount || !_lifecycle.IsActive || kcc == null || kcc.motor == null) return false;
            // ★ 防止 KCC Grounding 在骑乘/MatchTarget 期间与 SetPositionAndRotation 产生冲突。
            //   Climb 模块采用同样的做法：ForceUnground 告知 KCC 本帧不做稳定地面检测，
            //   避免 KCC 将角色压回地面，对抗 MatchTarget 向上对齐的位移。
            kcc.motor.ForceUnground(0.1f);
            return true;
        }

        public bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime)
        {
            if (!enableMount || currentMount == null) return false;
            return true;
        }

        public bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime)
        {
            if (!enableMount || currentMount == null) return false;
            currentVelocity = Vector3.zero;
            return true;
        }

        public override void OnDestroy()
        {
            // 实体销毁时保证骑乘生命周期干净退出（幂等，多次调用安全）
            if (_lifecycle != null && _lifecycle.Dispose()) OnMountExit();

            if (MyCore != null && MyCore.kcc.mountModule == this)
            {
                MyCore.kcc.mountModule = null;
            }
            base.OnDestroy();
        }
    }
}
