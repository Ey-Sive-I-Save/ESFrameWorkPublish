using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础域模块基类")]
    public abstract class EntityBasicModuleBase : Module<Entity, EntityBasicDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("基础移动模块")]
    public class EntityBasicMoveRotateModule : EntityBasicModuleBase
    {
        [Title("输入（世界空间）")]
        [ShowInInspector, ReadOnly]
        public Vector3 moveWorld => MyCore != null ? MyCore.kcc.moveInput : Vector3.zero;

        [ShowInInspector, ReadOnly]
        public Vector3 lookWorld => MyCore != null ? MyCore.kcc.lookInput : Vector3.forward;

        [Title("应用开关")]
        public bool applyMove = true;
        public bool applyLook = true;

        [Title("跳跃/下蹲")]
        [ReadOnly] public bool crouchHold;
        [ReadOnly] public bool jumpRequested;

        public string JUMP_StateName = "跳跃";
        public string Crouch_StateName = "下蹲";
        private StateBase _jumpState;
        private StateBase _crouchState;
        private StateMachine sm;

        [Title("平均速度窗口")]
        [LabelText("窗口时长(秒)"), Tooltip("计算AvgSpeedX/Z的滑动窗口大小")]
        public float avgSpeedWindowDuration = 0.5f;

        /// <summary>
        /// 滑动窗口速度采样 - 环形缓冲区
        /// 记录每帧的局部空间水平速度，用于计算前N秒平均值
        /// </summary>
        private const int AVG_BUFFER_CAPACITY = 60; // 覆盖60帧（60FPS下=1秒，足够0.5秒窗口）
        [NonSerialized] private float[] _avgBufX = new float[AVG_BUFFER_CAPACITY];
        [NonSerialized] private float[] _avgBufZ = new float[AVG_BUFFER_CAPACITY];
        [NonSerialized] private float[] _avgBufTime = new float[AVG_BUFFER_CAPACITY];
        [NonSerialized] private int _avgBufHead = 0;
        [NonSerialized] private int _avgBufCount = 0;

        public override void Start()
        {
            base.Start();
            if (MyCore != null && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                sm = MyCore.stateDomain.stateMachine;
                _jumpState = sm.GetStateByString(JUMP_StateName);
                _crouchState = sm.GetStateByString(Crouch_StateName);
            }
            _avgBufHead = 0;
            _avgBufCount = 0;
        }
        public void RequestJump()
        {
            // ★ 攀爬中跳跃 → 攀爬跳跃（同一个键，不同行为）
            if (MyCore != null && MyCore.kcc.climbModule != null)
            {
                var climbSub = MyCore.kcc.climbModule.subState;
                if (climbSub == ClimbSubState.Climbing || climbSub == ClimbSubState.Approach)
                {
                    MyCore.kcc.climbModule.RequestClimbJump();
                    return;
                }
            }

            if (sm.TryActivateState(_jumpState))
            {
                jumpRequested = true;
            }
        }

        public void ToggleCrouch()
        {
            if (_crouchState == null) return;

            if (_crouchState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Crouch_StateName);
            }
            else
            {
                sm.TryActivateState(_crouchState);
            }

            crouchHold = _crouchState.baseStatus == StateBaseStatus.Running;
        }

        protected override void Update()
        {
            if (MyCore == null) return;
            var moveWorld = MyCore.kcc.moveInput;
            if (jumpRequested)
            {
                MyCore.RequestJump();
                jumpRequested = false;
            }
            else if (_jumpState != null
                && _jumpState.baseStatus == StateBaseStatus.Running
                && MyCore.kcc.monitor.isStableOnGround
                && _jumpState.hasEnterTime > 0.1f)
            {
                sm?.TryDeactivateState(_jumpState.strKey);
            }
            crouchHold = _crouchState != null && _crouchState.baseStatus == StateBaseStatus.Running;
            MyCore.SetCrouch(crouchHold);

            // ★ 使用实际角色速度（而非键盘输入）驱动动画参数
            // moveInput 是玩家意图，monitor.velocity 是角色实际运动速度
            if (applyMove && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                var stateMachine = MyCore.stateDomain.stateMachine;
                if (stateMachine != null)
                {
                    // ★ 实际速度转换为角色局部空间
                    Vector3 actualVelocity = MyCore.kcc.monitor.velocity;
                    Vector3 localVelocity = MyCore.transform.InverseTransformDirection(actualVelocity);
                    
                    // 设置当前帧速度参数（性能热点：直写快路径）
                    stateMachine.SetMotionSpeedXZ(localVelocity.x, localVelocity.z);

                    // ★ 滑动窗口平均速度计算
                    // 将本帧数据写入环形缓冲区
                    _avgBufX[_avgBufHead] = localVelocity.x;
                    _avgBufZ[_avgBufHead] = localVelocity.z;
                    _avgBufTime[_avgBufHead] = Time.time;
                    _avgBufHead = (_avgBufHead + 1) % AVG_BUFFER_CAPACITY;
                    if (_avgBufCount < AVG_BUFFER_CAPACITY) _avgBufCount++;

                    // 计算窗口内平均值
                    float sumX = 0f, sumZ = 0f;
                    int validCount = 0;
                    float windowStart = Time.time - avgSpeedWindowDuration;
                    for (int i = 0; i < _avgBufCount; i++)
                    {
                        int idx = (_avgBufHead - 1 - i + AVG_BUFFER_CAPACITY) % AVG_BUFFER_CAPACITY;
                        if (_avgBufTime[idx] < windowStart) break; // 超出窗口
                        sumX += _avgBufX[idx];
                        sumZ += _avgBufZ[idx];
                        validCount++;
                    }
                    if (validCount > 0)
                    {
                        stateMachine.SetAvgSpeedXZ(sumX / validCount, sumZ / validCount);
                    }
                    else
                    {
                        stateMachine.SetAvgSpeedXZ(0f, 0f);
                    }
                }
            }
        }
    }

    [Serializable, TypeRegistryItem("基础战斗模块")]
    public class EntityBasicCombatModule : EntityBasicModuleBase
    {
        [Title("瞄准状态管理")]
        [LabelText("瞄准状态键")]
        public string aimStateKey = "瞄准";

        [LabelText("瞄准状态 AniInfo")]
        public StateAniDataInfo aimStateInfo;

        [LabelText("允许注入瞄准状态")]
        [Tooltip("当状态机中找不到瞄准状态时，允许通过 AimStateInfo 自动注册。")]
        public bool allowAimStateInjection = true;

        [Title("探头状态管理")]
        [LabelText("探头状态键")]
        public string peekStateKey = "探头";

        [LabelText("探头状态 AniInfo")]
        public StateAniDataInfo peekStateInfo;

        [LabelText("允许注入探头状态")]
        [Tooltip("当状态机中找不到探头状态时，允许通过 PeekStateInfo 自动注册。")]
        public bool allowPeekStateInjection = true;

        [Title("状态")]
        [ReadOnly] public bool isBlocking;
        [ReadOnly] public bool isSliding;
        [ReadOnly] public bool isAiming;
        [ReadOnly] public bool isPeeking;
        [ReadOnly] public float aimPeek;
        [ReadOnly] public int weaponIndex;
        [ReadOnly] public string lastAimStateFailureReason;
        [ReadOnly] public string lastPeekStateFailureReason;

        [NonSerialized] private StateMachine _sm;
        [NonSerialized] private StateBase _aimState;
        [NonSerialized] private StateBase _peekState;
        [NonSerialized] private StateLifecycleTracker _aimLifecycle = new StateLifecycleTracker();
        [NonSerialized] private StateLifecycleTracker _peekLifecycle = new StateLifecycleTracker();

        public override void Start()
        {
            base.Start();
            CacheStateMachine();
            ResolveAimState();
            ResolvePeekState();
            _aimLifecycle.Bind(_sm, _aimState, GetAimStateKeyForLifecycle());
            _peekLifecycle.Bind(_sm, _peekState, GetPeekStateKeyForLifecycle());
        }

        protected override void Update()
        {
            base.Update();

            if (_aimLifecycle.CheckExit())
            {
                LogCombatState($"Aim lifecycle detected exit | State={GetStateDebugName(_aimState)} | IsAiming={isAiming}");
                OnAimExit();
                return;
            }

            if (_peekLifecycle.CheckExit())
            {
                LogCombatState($"Peek lifecycle detected exit | State={GetStateDebugName(_peekState)} | IsPeeking={isPeeking} | AimPeek={aimPeek:F2}");
                OnPeekExit();
            }

            if (!_aimLifecycle.IsActive)
                return;

            if (_aimState == null)
                RebindAimLifecycle(ResolveAimState());

            if (_peekLifecycle.IsActive && _peekState == null)
                RebindPeekLifecycle(ResolvePeekState());
        }

        [Title("最近触发")]
        [ReadOnly] public float lastAttackTime;
        [ReadOnly] public float lastHeavyAttackTime;

        [Title("枪械开火")]
        [LabelText("启用枪械开火")]
        public bool enableGunFire = true;

        [LabelText("攻击输入触发开火")]
        [Tooltip("开启后，左键攻击输入会直接走枪械开火逻辑。")]
        public bool fireOnAttackInput = true;

        [LabelText("必须在瞄准中")]
        [Tooltip("开启后，只有进入瞄准状态时左键才会开火。")]
        public bool requireAimToFire = true;

        [LabelText("射击间隔(秒)")]
        [MinValue(0.01f)]
        public float fireInterval = 0.12f;

        [LabelText("射击距离")]
        [MinValue(0.5f)]
        public float fireDistance = 120f;

        [LabelText("枪口/开火原点")]
        [Tooltip("优先作为子弹/射线的发射原点；为空时回退到相机或角色自身。")]
        public Transform fireOrigin;

        [LabelText("命中层")]
        public LayerMask fireLayerMask = Physics.DefaultRaycastLayers;

        [LabelText("射线命中触发器")]
        public QueryTriggerInteraction fireQueryTriggerInteraction = QueryTriggerInteraction.Ignore;

        [LabelText("开火触发后坐力")]
        public bool recoilOnFire = true;

        [LabelText("后坐力强度")]
        [Range(0f, 2f)]
        public float fireRecoilMagnitude = 1f;

        [LabelText("绘制调试射线")]
        public bool debugDrawFireRay;

        [LabelText("输出开火日志")]
        public bool debugFireLog;

        [ShowInInspector, ReadOnly, LabelText("最近开火时间")]
        public float lastFireTime;

        [ShowInInspector, ReadOnly, LabelText("累计开火次数")]
        public int fireCount;

        [ShowInInspector, ReadOnly, LabelText("最近命中点")]
        public Vector3 lastFireHitPoint;

        [ShowInInspector, ReadOnly, LabelText("最近命中物")]
        public string lastFireHitName;

        [NonSerialized] private StateFinalIKDriver _cachedIKDriver;
        [NonSerialized] private Animator _cachedIKDriverAnimator;

        public void TriggerAttack()
        {
            lastAttackTime = Time.time;

            if (fireOnAttackInput)
                TryFireWeapon();
        }

        public void TriggerHeavyAttack()
        {
            lastHeavyAttackTime = Time.time;
        }

        public void SetBlock(bool enable)
        {
            isBlocking = enable;
        }

        public void SetSlide(bool enable)
        {
            isSliding = enable;
        }

        public bool SetAim(bool enable)
        {
            LogCombatState($"SetAim request | Enable={enable} | IsActive={_aimLifecycle.IsActive} | IsAiming={isAiming} | AimStateKey={aimStateKey}");

            if (!enable)
            {
                if (_aimLifecycle.RequestExit())
                {
                    LogCombatState($"SetAim disable accepted | State={GetStateDebugName(_aimState)}");
                    OnAimExit();
                }
                return true;
            }

            if (_aimLifecycle.IsActive)
            {
                LogCombatState($"SetAim ignored because lifecycle already active | State={GetStateDebugName(_aimState)}");
                return true;
            }

            CacheStateMachine();
            var aimState = ResolveAimState();
            if (_sm == null || aimState == null)
            {
                lastAimStateFailureReason = "瞄准状态未配置或未注册";
                LogCombatState($"SetAim failed | Reason={lastAimStateFailureReason} | StateMachineNull={_sm == null}");
                ForceStopAimInternal();
                return false;
            }

            if (aimState.baseStatus == StateBaseStatus.Running)
            {
                _aimLifecycle.Bind(_sm, aimState, GetAimStateKeyForLifecycle());
                if (_aimLifecycle.TryEnter(true))
                    OnAimEnter();
                else
                    _aimLifecycle.SyncFromBoundState();

                isAiming = true;
                lastAimStateFailureReason = string.Empty;
                LogCombatState($"SetAim reused running state | State={GetStateDebugName(aimState)}");
                return true;
            }

            var activationResult = _sm.TestStateActivation(aimState);
            if (!activationResult.CanActivate)
            {
                lastAimStateFailureReason = activationResult.failureReason;
                LogCombatState($"SetAim activation test failed | State={GetStateDebugName(aimState)} | Reason={lastAimStateFailureReason}");
                ForceStopAimInternal();
                return false;
            }

            bool activated = aimState.baseStatus == StateBaseStatus.Running || _sm.TryActivateState(aimState);
            _aimLifecycle.Bind(_sm, aimState, GetAimStateKeyForLifecycle());

            if (!_aimLifecycle.TryEnter(activated))
            {
                lastAimStateFailureReason = string.IsNullOrEmpty(activationResult.failureReason) && !activated
                    ? "瞄准状态激活失败"
                    : activationResult.failureReason;
                LogCombatState($"SetAim lifecycle enter failed | State={GetStateDebugName(aimState)} | Activated={activated} | Reason={lastAimStateFailureReason}");
                ForceStopAimInternal();
                return false;
            }

            LogCombatState($"SetAim activated | State={GetStateDebugName(aimState)} | Layer={TryGetStateLayer(aimState)}");
            OnAimEnter();
            return true;
        }

        public void SetAimPeek(float normalizedPeek)
        {
            SetAimPeekInternal(normalizedPeek);
        }

        public void SetAimPeekLeft(float intensity = 1f)
        {
            SetAimPeekInternal(-Mathf.Clamp01(intensity));
        }

        public void SetAimPeekRight(float intensity = 1f)
        {
            SetAimPeekInternal(Mathf.Clamp01(intensity));
        }

        public void ClearAimPeek()
        {
            RequestStopPeek();
        }

        public void SwitchWeaponNext()
        {
            weaponIndex++;
        }

        public bool TryFireWeapon()
        {
            if (!enableGunFire || MyCore == null)
                return false;

            if (requireAimToFire && !isAiming)
            {
                if (debugFireLog)
                    Debug.Log("[EntityBasicCombatModule] 开火被忽略：当前未处于瞄准状态。", MyCore);
                return false;
            }

            if (Time.time - lastFireTime < fireInterval)
                return false;

            Vector3 rayOrigin;
            Vector3 rayDirection;
            Vector3 visualOrigin;
            ResolveFireRay(out rayOrigin, out rayDirection, out visualOrigin);

            float distance = Mathf.Max(0.5f, fireDistance);
            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                rayOrigin,
                rayDirection,
                out hit,
                distance,
                fireLayerMask,
                fireQueryTriggerInteraction);

            Vector3 endPoint = hasHit ? hit.point : rayOrigin + rayDirection * distance;

            lastFireTime = Time.time;
            fireCount++;
            lastFireHitPoint = endPoint;
            lastFireHitName = hasHit && hit.collider != null ? hit.collider.name : string.Empty;

            if (recoilOnFire)
            {
                var ikDriver = ResolveIKDriver();
                if (ikDriver != null)
                    ikDriver.HandleRecoil(fireRecoilMagnitude);
            }

            if (debugDrawFireRay)
                Debug.DrawLine(visualOrigin, endPoint, hasHit ? Color.red : Color.yellow, 0.2f);

            if (debugFireLog)
            {
                string hitName = hasHit && hit.collider != null ? hit.collider.name : "<none>";
                Debug.Log(
                    $"[EntityBasicCombatModule] Fire #{fireCount} | Origin={rayOrigin} | Dir={rayDirection} | Hit={hitName} | Point={endPoint}",
                    MyCore);
            }

            return true;
        }

        private void CacheStateMachine()
        {
            if (_sm == null && MyCore != null && MyCore.stateDomain != null)
            {
                _sm = MyCore.stateDomain.stateMachine;
            }
        }

        private void ResolveFireRay(out Vector3 rayOrigin, out Vector3 rayDirection, out Vector3 visualOrigin)
        {
            Transform cameraTransform = GetActiveCameraTransform();
            Transform actualOrigin = fireOrigin != null ? fireOrigin : (cameraTransform != null ? cameraTransform : MyCore.transform);

            Vector3 aimingOrigin = cameraTransform != null ? cameraTransform.position : actualOrigin.position;
            Vector3 aimingDirection = cameraTransform != null ? cameraTransform.forward : actualOrigin.forward;
            if (aimingDirection.sqrMagnitude <= 0.0001f)
                aimingDirection = MyCore.transform.forward;

            rayOrigin = aimingOrigin;
            rayDirection = aimingDirection.normalized;
            visualOrigin = actualOrigin.position;

            if (cameraTransform != null && actualOrigin != cameraTransform)
            {
                float distance = Mathf.Max(0.5f, fireDistance);
                Vector3 targetPoint = aimingOrigin + rayDirection * distance;
                if (Physics.Raycast(aimingOrigin, rayDirection, out RaycastHit preHit, distance, fireLayerMask, fireQueryTriggerInteraction))
                    targetPoint = preHit.point;

                Vector3 muzzleDirection = targetPoint - actualOrigin.position;
                if (muzzleDirection.sqrMagnitude > 0.0001f)
                {
                    rayOrigin = actualOrigin.position;
                    rayDirection = muzzleDirection.normalized;
                }
                else
                {
                    rayOrigin = actualOrigin.position;
                    rayDirection = actualOrigin.forward.sqrMagnitude > 0.0001f ? actualOrigin.forward.normalized : rayDirection;
                }
            }
            else
            {
                rayOrigin = actualOrigin.position;
                rayDirection = actualOrigin.forward.sqrMagnitude > 0.0001f ? actualOrigin.forward.normalized : rayDirection;
            }
        }

        private Transform GetActiveCameraTransform()
        {
            if (MyCore == null)
                return null;

            if (MyCore.ModuleTables.TryGetValue(typeof(EntityBasicCameraModule), out var module))
            {
                var cameraModule = module as EntityBasicCameraModule;
                if (cameraModule != null)
                    return cameraModule.GetActiveCameraTransform();
            }

            return null;
        }

        private StateFinalIKDriver ResolveIKDriver()
        {
            if (MyCore == null)
                return null;

            var animator = MyCore.animator;
            if (animator == null)
                return null;

            if (_cachedIKDriver != null && _cachedIKDriverAnimator == animator)
                return _cachedIKDriver;

            _cachedIKDriverAnimator = animator;
            _cachedIKDriver = animator.GetComponent<StateFinalIKDriver>();
            return _cachedIKDriver;
        }

        private StateBase ResolveAimState()
        {
            CacheStateMachine();
            if (_sm == null)
                return null;

            string desiredKey = aimStateKey;
            if (string.IsNullOrEmpty(desiredKey) && aimStateInfo != null)
            {
                var shared = aimStateInfo.sharedData;
                if (shared != null && shared.basicConfig != null)
                    desiredKey = shared.basicConfig.stateName;
            }

            StateBase state = FindRegisteredStateByInfo(aimStateInfo);
            if (state == null && !string.IsNullOrEmpty(desiredKey))
                state = _sm.GetStateByString(desiredKey);

            if (aimStateInfo != null && state != null && !IsStateBoundToInfo(state, aimStateInfo) && allowAimStateInjection)
            {
                LogCombatState($"ResolveAimState detected key/info mismatch | DesiredKey={desiredKey} | Existing={GetStateDebugName(state)} | Re-registerFromInfo=True");
                state = _sm.RegisterStateFromInfo(aimStateInfo, desiredKey, false) ?? state;
            }

            if (state == null && allowAimStateInjection && aimStateInfo != null)
            {
                string keyOverride = string.IsNullOrEmpty(desiredKey) ? null : desiredKey;
                state = _sm.RegisterStateFromInfo(aimStateInfo, keyOverride, false);
            }

            _aimState = state;
            LogCombatState($"ResolveAimState result | DesiredKey={desiredKey} | State={GetStateDebugName(state)} | BoundToInfo={IsStateBoundToInfo(state, aimStateInfo)}");
            RebindAimLifecycle(state);
            return state;
        }

        private StateBase ResolvePeekState()
        {
            CacheStateMachine();
            if (_sm == null)
                return null;

            string desiredKey = peekStateKey;
            if (string.IsNullOrEmpty(desiredKey) && peekStateInfo != null)
            {
                var shared = peekStateInfo.sharedData;
                if (shared != null && shared.basicConfig != null)
                    desiredKey = shared.basicConfig.stateName;
            }

            StateBase state = FindRegisteredStateByInfo(peekStateInfo);
            if (state == null && !string.IsNullOrEmpty(desiredKey))
                state = _sm.GetStateByString(desiredKey);

            if (peekStateInfo != null && state != null && !IsStateBoundToInfo(state, peekStateInfo) && allowPeekStateInjection)
            {
                LogCombatState($"ResolvePeekState detected key/info mismatch | DesiredKey={desiredKey} | Existing={GetStateDebugName(state)} | Re-registerFromInfo=True");
                state = _sm.RegisterStateFromInfo(peekStateInfo, desiredKey, false) ?? state;
            }

            if (state == null && allowPeekStateInjection && peekStateInfo != null)
            {
                string keyOverride = string.IsNullOrEmpty(desiredKey) ? null : desiredKey;
                state = _sm.RegisterStateFromInfo(peekStateInfo, keyOverride, false);
            }

            _peekState = state;
            LogCombatState($"ResolvePeekState result | DesiredKey={desiredKey} | State={GetStateDebugName(state)} | BoundToInfo={IsStateBoundToInfo(state, peekStateInfo)}");
            RebindPeekLifecycle(state);
            return state;
        }

        private bool IsAimStateRunning()
        {
            if (!_aimLifecycle.IsActive)
                return false;

            if (_aimState == null)
                _aimState = ResolveAimState();

            return _aimState != null && _aimState.baseStatus == StateBaseStatus.Running;
        }

        private bool IsPeekStateRunning()
        {
            if (!_peekLifecycle.IsActive)
                return false;

            if (_peekState == null)
                _peekState = ResolvePeekState();

            return _peekState != null && _peekState.baseStatus == StateBaseStatus.Running;
        }

        private StateBase FindRegisteredStateByInfo(StateAniDataInfo info)
        {
            if (_sm == null || info == null || info.sharedData == null)
                return null;

            foreach (var kvp in _sm.EnumerateRegisteredStatesByKey())
            {
                var state = kvp.Value;
                if (IsStateBoundToInfo(state, info))
                    return state;
            }

            return null;
        }

        private static bool IsStateBoundToInfo(StateBase state, StateAniDataInfo info)
        {
            if (state == null || info == null)
                return false;

            return ReferenceEquals(state.stateSharedData, info.sharedData);
        }

        public override void OnDestroy()
        {
            if (_aimLifecycle.Dispose())
                OnAimExit();

            if (_peekLifecycle.Dispose())
                OnPeekExit();

            base.OnDestroy();
        }

        private void OnAimEnter()
        {
            isAiming = true;
            lastAimStateFailureReason = string.Empty;
            LogCombatState($"Aim enter | State={GetStateDebugName(_aimState)} | Layer={TryGetStateLayer(_aimState)}");
        }

        private void OnAimExit()
        {
            LogCombatState($"Aim exit | State={GetStateDebugName(_aimState)} | PeekActive={_peekLifecycle.IsActive} | IsAiming={isAiming}");
            if (_peekLifecycle.RequestExit())
                OnPeekExit();
            else
                ForceStopPeekInternal();

            ForceStopAimInternal();
            lastAimStateFailureReason = string.Empty;
        }

        private void OnPeekEnter()
        {
            isPeeking = true;
            lastPeekStateFailureReason = string.Empty;
            LogCombatState($"Peek enter | State={GetStateDebugName(_peekState)} | AimPeek={aimPeek:F2}");
        }

        private void OnPeekExit()
        {
            ForceStopPeekInternal();
            lastPeekStateFailureReason = string.Empty;
            LogCombatState($"Peek exit | State={GetStateDebugName(_peekState)}");
        }

        private void ForceStopAimInternal()
        {
            isAiming = false;
            ForceStopPeekInternal();
        }

        private void ForceStopPeekInternal()
        {
            isPeeking = false;
            aimPeek = 0f;
        }

        private void RebindAimLifecycle(StateBase state)
        {
            _aimLifecycle.Bind(_sm, state, GetAimStateKeyForLifecycle());
        }

        private void RebindPeekLifecycle(StateBase state)
        {
            _peekLifecycle.Bind(_sm, state, GetPeekStateKeyForLifecycle());
        }

        private string GetAimStateKeyForLifecycle()
        {
            if (_aimState != null && !string.IsNullOrEmpty(_aimState.strKey))
                return _aimState.strKey;

            if (!string.IsNullOrEmpty(aimStateKey))
                return aimStateKey;

            if (aimStateInfo != null && aimStateInfo.sharedData != null && aimStateInfo.sharedData.basicConfig != null)
                return aimStateInfo.sharedData.basicConfig.stateName;

            return string.Empty;
        }

        private string GetPeekStateKeyForLifecycle()
        {
            if (_peekState != null && !string.IsNullOrEmpty(_peekState.strKey))
                return _peekState.strKey;

            if (!string.IsNullOrEmpty(peekStateKey))
                return peekStateKey;

            if (peekStateInfo != null && peekStateInfo.sharedData != null && peekStateInfo.sharedData.basicConfig != null)
                return peekStateInfo.sharedData.basicConfig.stateName;

            return string.Empty;
        }

        private bool SetAimPeekInternal(float normalizedPeek)
        {
            if (!IsAimStateRunning())
            {
                RequestStopPeek();
                return false;
            }

            float clampedPeek = Mathf.Clamp(normalizedPeek, -1f, 1f);
            if (Mathf.Abs(clampedPeek) <= 0.001f)
            {
                RequestStopPeek();
                return true;
            }

            if (!TryEnsurePeekStateActive())
            {
                ForceStopPeekInternal();
                return false;
            }

            aimPeek = clampedPeek;
            isPeeking = true;
            lastPeekStateFailureReason = string.Empty;
            return true;
        }

        private bool TryEnsurePeekStateActive()
        {
            if (_peekLifecycle.IsActive && IsPeekStateRunning())
                return true;

            CacheStateMachine();
            var peekState = ResolvePeekState();
            if (_sm == null || peekState == null)
            {
                lastPeekStateFailureReason = "探头状态未配置或未注册";
                LogCombatState($"TryEnsurePeekStateActive failed | Reason={lastPeekStateFailureReason} | StateMachineNull={_sm == null}");
                return false;
            }

            if (peekState.baseStatus == StateBaseStatus.Running)
            {
                _peekLifecycle.Bind(_sm, peekState, GetPeekStateKeyForLifecycle());
                if (_peekLifecycle.TryEnter(true))
                    OnPeekEnter();
                else
                    _peekLifecycle.SyncFromBoundState();

                isPeeking = true;
                lastPeekStateFailureReason = string.Empty;
                LogCombatState($"TryEnsurePeekStateActive reused running state | State={GetStateDebugName(peekState)}");
                return true;
            }

            var activationResult = _sm.TestStateActivation(peekState);
            if (!activationResult.CanActivate)
            {
                lastPeekStateFailureReason = activationResult.failureReason;
                LogCombatState($"TryEnsurePeekStateActive activation test failed | State={GetStateDebugName(peekState)} | Reason={lastPeekStateFailureReason}");
                return false;
            }

            bool activated = peekState.baseStatus == StateBaseStatus.Running || _sm.TryActivateState(peekState);
            _peekLifecycle.Bind(_sm, peekState, GetPeekStateKeyForLifecycle());

            if (!_peekLifecycle.TryEnter(activated))
            {
                lastPeekStateFailureReason = string.IsNullOrEmpty(activationResult.failureReason) && !activated
                    ? "探头状态激活失败"
                    : activationResult.failureReason;
                LogCombatState($"TryEnsurePeekStateActive lifecycle enter failed | State={GetStateDebugName(peekState)} | Activated={activated} | Reason={lastPeekStateFailureReason}");
                return false;
            }

            LogCombatState($"TryEnsurePeekStateActive activated | State={GetStateDebugName(peekState)} | Layer={TryGetStateLayer(peekState)}");
            OnPeekEnter();
            return true;
        }

        private void RequestStopPeek()
        {
            if (_peekLifecycle.RequestExit())
                OnPeekExit();
            else
                ForceStopPeekInternal();
        }

        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        private void LogCombatState(string message)
        {
            StateMachineDebug.Log($"[EntityBasicCombatModule] {message}");
        }

        private string GetStateDebugName(StateBase state)
        {
            if (state == null)
                return "<null>";

            var basic = state.stateSharedData != null ? state.stateSharedData.basicConfig : null;
            string stateName = basic != null ? basic.stateName : string.Empty;
            return $"{state.strKey}|Name={stateName}|Id={state.intKey}|Status={state.baseStatus}";
        }

        private string TryGetStateLayer(StateBase state)
        {
            if (_sm != null && state != null && _sm.TryGetStateLayerType(state, out var layerType))
                return layerType.ToString();

            return "<unknown>";
        }
    }



    [Serializable]
    public struct FlyParams
    {
        [LabelText("最大飞行速度")]
        public float flyMaxSpeed;

        [LabelText("飞行加速")]
        public float flyAcceleration;

        [LabelText("飞行阻力")]
        public float flyDrag;

        [LabelText("飞行重力倍率(0=无重力,1=全重力)")]
        public float flyGravityScale;

        public static FlyParams Default => new FlyParams
        {
            flyMaxSpeed = 10f,
            flyAcceleration = 12f,
            flyDrag = 0.2f,
            flyGravityScale = 0.1f
        };
    }

    [Serializable]
    public struct SwimParams
    {
        [LabelText("最大游泳速度")]
        public float swimMaxSpeed;

        [LabelText("游泳加速")]
        public float swimAcceleration;

        [LabelText("水阻力")]
        public float swimDrag;

        [LabelText("浮力")]
        public float swimBuoyancy;

        public static SwimParams Default => new SwimParams
        {
            swimMaxSpeed = 6f,
            swimAcceleration = 8f,
            swimDrag = 0.5f,
            swimBuoyancy = 6f
        };
    }

    [Serializable, TypeRegistryItem("基础技能模块")]
    public class EntityBasicSkillModule : EntityBasicModuleBase
    {
        [Title("最近触发")]
        [ReadOnly] public int lastSkillIndex = -1;
        [ReadOnly] public float lastSkillTime;

        [Title("技能效果")]
        [LabelText("启用技能效果")]
        public bool enableSkillEffects = true;

        [LabelText("技能效果列表")]
        public System.Collections.Generic.List<SkillEffectConfig> skillEffects = new System.Collections.Generic.List<SkillEffectConfig>();

        [NonSerialized] private bool _hasActiveEffect;
        [NonSerialized] private SkillEffectConfig _activeEffect;
        [NonSerialized] private float _activeEffectEndTime;

        public void TriggerSkill(int index)
        {
            lastSkillIndex = index;
            lastSkillTime = Time.time;

            if (!enableSkillEffects) return;
            var effect = FindEffect(index);
            if (effect != null)
            {
                ActivateEffect(effect);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!_hasActiveEffect) return;
            if (Time.time < _activeEffectEndTime) return;

            DeactivateEffect();
        }

        private SkillEffectConfig FindEffect(int index)
        {
            if (skillEffects == null) return null;
            for (int i = 0; i < skillEffects.Count; i++)
            {
                var effect = skillEffects[i];
                if (effect != null && effect.skillIndex == index)
                {
                    return effect;
                }
            }
            return null;
        }

        private void ActivateEffect(SkillEffectConfig effect)
        {
            if (effect == null) return;

            if (_hasActiveEffect)
            {
                DeactivateEffect();
            }

            _activeEffect = effect;
            _hasActiveEffect = true;
            _activeEffectEndTime = Time.time + Mathf.Max(0f, effect.duration);

            if (MyCore != null)
            {
                MyCore.SetSpeedMultiplier(effect.speedMultiplier);
                MyCore.SetSpeedLimit(effect.speedLimit);
            }

            if (effect.useDynamicCamera && TryGetCameraModule(out var camModule))
            {
                camModule.SetDynamicCameraSlot(effect.skillCamera, effect.autoActivateCamera);
            }
        }

        private void DeactivateEffect()
        {
            if (!_hasActiveEffect) return;

            if (MyCore != null)
            {
                MyCore.ResetSpeedModifiers();
            }

            if (_activeEffect != null && _activeEffect.useDynamicCamera && TryGetCameraModule(out var camModule))
            {
                camModule.ClearDynamicCamera(_activeEffect.autoActivateCamera);
            }

            _activeEffect = null;
            _hasActiveEffect = false;
        }

        private bool TryGetCameraModule(out EntityBasicCameraModule module)
        {
            module = null;
            if (MyCore == null) return false;
            if (MyCore.ModuleTables.TryGetValue(typeof(EntityBasicCameraModule), out var m))
            {
                module = m as EntityBasicCameraModule;
            }
            return module != null;
        }
    }

    [Serializable]
    public class SkillEffectConfig
    {
        [Title("触发条件")]
        [LabelText("技能索引")]
        public int skillIndex = 1;

        [Title("持续时间")]
        [LabelText("持续时长(秒)")]
        public float duration = 0.25f;

        [Title("速度影响")]
        [LabelText("速度倍率")]
        public float speedMultiplier = 1f;

        [LabelText("速度上限(<=0 不限制)")]
        public float speedLimit = 0f;

        [Title("技能专属相机")]
        [LabelText("使用动态相机")]
        public bool useDynamicCamera = false;

        [LabelText("自动启用相机组件")]
        public bool autoActivateCamera = true;

        [HideLabel]
        public VirtualCameraSlot skillCamera = new VirtualCameraSlot();
    }

    [Serializable, TypeRegistryItem("基础相机模块")]
    public class EntityBasicCameraModule : EntityBasicModuleBase
    {
        [Title("模式")]
        public CameraMode mode = CameraMode.ThirdPerson;

        [Title("第一人称")]
        [HideLabel]
        public CameraRig firstPerson = new CameraRig();

        [Title("第三人称")]
        [HideLabel]
        public CameraRig thirdPerson = new CameraRig();

        [Title("当前激活")]
        [ShowInInspector, ReadOnly]
        public Transform activeCameraTransform;

        public void SetMode(CameraMode newMode)
        {
            mode = newMode;
            RefreshActiveCamera();
        }

        public void SetDynamicCamera(Transform camTransform)
        {
            GetRig(mode).dynamicSlot.Set(camTransform);
            RefreshActiveCamera();
        }

        public void SetDynamicCameraSlot(VirtualCameraSlot slot, bool activate = true)
        {
            if (slot == null)
            {
                ClearDynamicCamera(activate);
                return;
            }

            var rig = GetRig(mode);
            rig.dynamicSlot.CopyFrom(slot);
            if (activate)
            {
                rig.dynamicSlot.SetActive(true);
            }
            RefreshActiveCamera();
        }

        public void ClearDynamicCamera(bool deactivate = true)
        {
            var rig = GetRig(mode);
            if (deactivate)
            {
                rig.dynamicSlot.SetActive(false);
            }
            rig.dynamicSlot.Clear();
            RefreshActiveCamera();
        }

        public Transform GetActiveCameraTransform()
        {
            RefreshActiveCamera();
            return activeCameraTransform;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshActiveCamera();
        }

        private void RefreshActiveCamera()
        {
            var rig = GetRig(mode);
            activeCameraTransform = rig.GetBestTransform();
        }

        private CameraRig GetRig(CameraMode m)
        {
            return m == CameraMode.FirstPerson ? firstPerson : thirdPerson;
        }
    }

    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Serializable]
    public class CameraRig
    {
        [Title("Main")]
        [HideLabel]
        public VirtualCameraSlot main = new VirtualCameraSlot();

        [Title("扩展槽位 A")]
        [HideLabel]
        public VirtualCameraSlot slotA = new VirtualCameraSlot();

        [Title("扩展槽位 B")]
        [HideLabel]
        public VirtualCameraSlot slotB = new VirtualCameraSlot();

        [Title("Dynamic")]
        [HideLabel]
        public VirtualCameraSlot dynamicSlot = new VirtualCameraSlot();

        public Transform GetBestTransform()
        {
            return dynamicSlot.Transform ?? main.Transform ?? slotA.Transform ?? slotB.Transform;
        }
    }

    [Serializable]
    public class VirtualCameraSlot
    {
        [LabelText("Virtual Camera")]
        public Component virtualCamera;

        [LabelText("备用 Transform")]
        public Transform fallbackTransform;

        public Transform Transform => virtualCamera != null ? virtualCamera.transform : fallbackTransform;

        public void Set(Transform transform)
        {
            fallbackTransform = transform;
        }

        public void CopyFrom(VirtualCameraSlot other)
        {
            if (other == null) return;
            virtualCamera = other.virtualCamera;
            fallbackTransform = other.fallbackTransform;
        }

        public void Clear()
        {
            virtualCamera = null;
            fallbackTransform = null;
        }

        public void SetActive(bool active)
        {
            if (virtualCamera is Behaviour behaviour)
            {
                behaviour.enabled = active;
            }
        }
    }

    /// <summary>
    /// 急停模块 — 完全独立，可自由添加/移除。
    /// 
    /// 检测原理：
    ///   每帧监测角色实际移动速度（monitor.velocity）的幅度变化率。
    ///   当速度幅度在短时间内骤减（如从跑步松手），且当前处于地面常态移动时，
    ///   尝试激活"急停"状态。同时将触发时的AvgSpeedX/Z写入Context，
    ///   供急停动画BlendTree读取行进方向。
    /// 
    /// 优先级设计：
    ///   急停状态的代价(cost)很低，容易与跑步等常态移动状态合并；
    ///   但其打断能力很弱，几乎无法打断技能、跳跃等高优先级状态。
    ///   只有在常态地面移动且速度骤减时才有机会被激活。
    /// </summary>
    [Serializable, TypeRegistryItem("基础急停模块")]
    public class EntityBasicQuickStopModule : EntityBasicModuleBase
    {
        [Title("开关")]
        [LabelText("启用急停检测")]
        public bool enableQuickStop = true;

        [Title("状态绑定")]
        [LabelText("急停状态名")]
        public string QuickStop_StateName = "急停";

        [Title("检测参数")]
        [LabelText("速度骤减阈值"), Tooltip("角色实际水平速度在窗口内下降超过此值时视为骤减(m/s)")]
        [Range(0.5f, 10f)]
        public float speedDropThreshold = 2f;

        [LabelText("最小触发速度(m/s)"), Tooltip("只有窗口内峰值速度大于此值时才可能触发(避免静止时误触发)")]
        [Range(0.5f, 10f)]
        public float minSpeedToTrigger = 2f;

        [LabelText("激活冷却(秒)"), Tooltip("两次急停之间的最小间隔")]
        public float cooldown = 0.6f;

        [LabelText("要求稳定着地"), Tooltip("仅在稳定着地时才检测急停")]
        public bool requireGrounded = true;

        [Title("自动退出")]
        [LabelText("自动退出时长(秒)"), Tooltip("急停状态激活后最长持续时间，到期自动退出；<=0 表示由动画/状态机自行管理")]
        public float autoExitDuration = 0.35f;

        [Title("滑动窗口")]
        [LabelText("峰值记忆时长(秒)"), Tooltip("在此时间窗口内记录速度峰值，用于检测骤减（解决平滑加速下单帧差值不够的问题）")]
        [Range(0.05f, 0.5f)]
        public float peakMemoryDuration = 0.15f;

        [Title("调试")]
        [LabelText("启用调试日志")]
        public bool debugLog = false;

        [ShowInInspector, ReadOnly, LabelText("急停中")]
        public bool isQuickStopping { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("上帧速度幅度")]
        public float lastSpeedMagnitude { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("当前速度幅度")]
        public float currentSpeedMagnitude { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("窗口内峰值")]
        public float peakSpeedMagnitude { get; private set; }

        // 内部状态
        [NonSerialized] private StateBase _quickStopState;
        [NonSerialized] private StateMachine _sm;
        [NonSerialized] private float _lastActivateTime = -999f;
        [NonSerialized] private float _activateTimestamp;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private float _peakValue;
        [NonSerialized] private float _peakTimestamp;

        public override void Start()
        {
            base.Start();
            _initialized = false;
            if (MyCore != null && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                _sm = MyCore.stateDomain.stateMachine;
                _quickStopState = _sm.GetStateByString(QuickStop_StateName);
                _initialized = _quickStopState != null;

                if (debugLog)
                {
                    if (_quickStopState == null)
                        Debug.LogWarning($"[急停模块] 初始化失败：状态机中找不到名为 \"{QuickStop_StateName}\" 的状态！请检查状态机配置。");
                    else
                        Debug.Log($"[急停模块] 初始化成功：已绑定状态 \"{QuickStop_StateName}\"");
                }
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[急停模块] 初始化失败：MyCore={MyCore != null}, stateDomain={MyCore?.stateDomain != null}, sm={MyCore?.stateDomain?.stateMachine != null}");
            }

            lastSpeedMagnitude = 0f;
            currentSpeedMagnitude = 0f;
            _peakValue = 0f;
            _peakTimestamp = 0f;
        }

        protected override void Update()
        {
            if (MyCore == null || !enableQuickStop || !_initialized) return;

            // ---- 采样角色实际水平速度 ----
            Vector3 velocity = MyCore.kcc.monitor.velocity;
            // 取水平分量（忽略垂直速度，避免跳跃/下落干扰）
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            currentSpeedMagnitude = horizontalVelocity.magnitude;

            // ---- 滑动窗口峰值追踪 ----
            if (currentSpeedMagnitude >= _peakValue)
            {
                _peakValue = currentSpeedMagnitude;
                _peakTimestamp = Time.time;
            }
            else if (Time.time - _peakTimestamp > peakMemoryDuration)
            {
                // 峰值过期，衰减到当前值
                _peakValue = currentSpeedMagnitude;
                _peakTimestamp = Time.time;
            }
            peakSpeedMagnitude = _peakValue;

            // ---- 状态跟踪：当前是否在急停中 ----
            isQuickStopping = _quickStopState != null && _quickStopState.baseStatus == StateBaseStatus.Running;

            // ---- 自动退出逻辑 ----
            if (isQuickStopping && autoExitDuration > 0f)
            {
                if (Time.time - _activateTimestamp >= autoExitDuration)
                {
                    TryDeactivate();
                }
            }

            // ---- 激活检测 ----
            if (!isQuickStopping)
            {
                TryDetectAndActivate();
            }

            // ---- 保存本帧数据供下帧比较 ----
            lastSpeedMagnitude = currentSpeedMagnitude;
        }

        /// <summary>
        /// 检测是否满足急停条件并尝试激活。
        /// 使用滑动窗口峰值代替单帧比较，解决平滑/插值速度下骤减检测不灵敏的问题。
        /// 触发时Context中的AvgSpeedX/Z已经由移动模块维护，急停BlendTree可直接读取方向。
        /// </summary>
        private void TryDetectAndActivate()
        {
            // 冷却检查
            if (Time.time - _lastActivateTime < cooldown)
            {
                return;
            }

            // 着地检查
            if (requireGrounded && !MyCore.kcc.monitor.isStableOnGround)
            {
                if (debugLog) Debug.Log($"[急停模块] 跳过：未着地 (isStableOnGround=false)");
                return;
            }

            // 窗口内峰值必须有足够速度（之前在移动）
            if (_peakValue < minSpeedToTrigger)
            {
                return;
            }

            // 当前实际速度相对于峰值的骤减量
            float drop = _peakValue - currentSpeedMagnitude;
            if (drop < speedDropThreshold)
            {
                return;
            }

            if (debugLog)
            {
                Debug.Log($"[急停模块] 检测到骤减！peak={_peakValue:F3}m/s, current={currentSpeedMagnitude:F3}m/s, drop={drop:F3} >= threshold={speedDropThreshold:F3}，尝试激活...");
            }

            // 满足条件，尝试激活
            bool activated = _sm.TryActivateState(_quickStopState);
            if (activated)
            {
                _lastActivateTime = Time.time;
                _activateTimestamp = Time.time;
                isQuickStopping = true;
                // 激活后重置峰值，避免退出后立刻再次触发
                _peakValue = 0f;
                if (debugLog) Debug.Log($"[急停模块] ★ 急停激活成功！AvgSpeedX={_sm.GetFloat(StateDefaultFloatParameter.AvgSpeedX):F2}, AvgSpeedZ={_sm.GetFloat(StateDefaultFloatParameter.AvgSpeedZ):F2}");
            }
            else
            {
                if (debugLog) Debug.LogWarning($"[急停模块] TryActivateState 返回 false — 被状态机合并/优先级规则拒绝。检查急停状态的 Cost/Priority/MergeRule 配置。");
            }
        }

        /// <summary>
        /// 尝试退出急停状态
        /// </summary>
        private void TryDeactivate()
        {
            if (_quickStopState == null) return;
            if (_quickStopState.baseStatus == StateBaseStatus.Running)
            {
                _sm.TryDeactivateState(QuickStop_StateName);
            }
            isQuickStopping = _quickStopState.baseStatus == StateBaseStatus.Running;
        }

        /// <summary>
        /// 外部强制退出急停（例如其他系统需要中断时调用）
        /// </summary>
        public void ForceExit()
        {
            if (_quickStopState == null || _sm == null) return;
            if (_quickStopState.baseStatus == StateBaseStatus.Running)
            {
                _sm.TryDeactivateState(QuickStop_StateName);
                if (_quickStopState.baseStatus == StateBaseStatus.Running)
                {
                    _sm.ForceExitState(_quickStopState);
                }
            }
            isQuickStopping = false;
        }

        /// <summary>
        /// 外部手动触发急停（跳过检测条件，仅检查冷却）
        /// </summary>
        public bool RequestQuickStop()
        {
            if (!enableQuickStop || !_initialized) return false;
            if (isQuickStopping) return false;
            if (Time.time - _lastActivateTime < cooldown) return false;

            if (_sm.TryActivateState(_quickStopState))
            {
                _lastActivateTime = Time.time;
                _activateTimestamp = Time.time;
                isQuickStopping = true;
                return true;
            }
            return false;
        }

        public override void OnDestroy()
        {
            ForceExit();
            base.OnDestroy();
        }
    }

    [Serializable, TypeRegistryItem("基础根运动模块")]
    public class EntityBasicRootMotionModule : EntityBasicModuleBase
    {
        [Title("开关")]
        public bool enableRootMotion = true;

        [Title("参数")]
        public float minDeltaTime = 0.0001f;

        [NonSerialized] private Animator _cachedAnimator;

        public override void Start()
        {
            if (MyCore != null)
            {
                _cachedAnimator = MyCore.animator;
            }
        }

        protected override void Update()
        {
            if (!enableRootMotion || MyCore == null) return;
            if (_cachedAnimator == null)
            {
                _cachedAnimator = MyCore.animator;
            }
            if (_cachedAnimator == null) return;

            float dt = Time.deltaTime;
            if (dt <= minDeltaTime) return;

            Vector3 velocity = _cachedAnimator.deltaPosition / dt;
            MyCore.SetRootMotionVelocity(velocity);
        }

        public override void OnDestroy()
        {
            if (MyCore != null)
            {
                MyCore.ClearRootMotionVelocity();
            }
            base.OnDestroy();
        }
    }

    // =====================================================================
    // ★ 攀爬模块 — 管理墙壁攀爬、顶部攀上、矮墙翻越
    // =====================================================================

    /// <summary>
    /// 攀爬子状态枚举
    /// </summary>
    public enum ClimbSubState
    {
        /// <summary>不在攀爬中</summary>
        None,
        /// <summary>正在向攀爬面靠近/贴附</summary>
        Approach,
        /// <summary>在墙面上正常攀爬中</summary>
        Climbing,
        /// <summary>攀爬到顶部执行翻上动作</summary>
        ClimbOver,
        /// <summary>直接翻越矮墙</summary>
        Vault,
        /// <summary>攀爬中跳跃（脱离墙面）</summary>
        ClimbJump,
    }

    /// <summary>
    /// 攀爬参数集 — 可在 Inspector 中配置
    /// </summary>
    [Serializable]
    public struct ClimbParams
    {
        [LabelText("攀爬移动速度(m/s)")]
        public float climbSpeed;

        [LabelText("攀爬加速度")]
        public float climbAcceleration;

        [LabelText("贴壁偏移(米)"), Tooltip("角色中心距离墙面的距离")]
        public float wallOffset;

        [LabelText("贴壁吸附速度")]
        public float snapSharpness;

        [LabelText("吸附力上限(m/s)"), Tooltip("贴墙吸附力的最大速度，防止抨动")]
        public float maxSnapSpeed;

        [LabelText("贴墙持续时间(秒)"), Tooltip("进入攀爬后自动贴墙持续的时间")]
        public float snapDuration;

        [LabelText("持续着地退出(秒)"), Tooltip("攀爬中脚下持续为地面超过该时间则退出")]
        public float groundedExitDelay;

        [LabelText("攀爬朝向最大转速(度/秒)"), Tooltip("限制角色面朝墙的旋转速度，避免镜头突兀旋转")]
        public float climbMaxTurnSpeed;

        [LabelText("重力倍率"), Tooltip("攀爬时重力倍率(0=无重力悬挂)")]
        public float gravityScale;

        [LabelText("攀爬跳跃速度"), Tooltip("攀爬中按跳跃时脱离墙面的初速")]
        public float climbJumpSpeed;

        [LabelText("攀爬跳跃法线弹出力"), Tooltip("跳跃时沿墙面法线方向的弹出速度")]
        public float climbJumpNormalForce;

        [LabelText("攀爬跳跃回贴延迟(秒)"), Tooltip("跳跃后延迟多久再开始检测回贴")]
        public float climbJumpReattachDelay;

        [LabelText("攀爬跳跃最大时长(秒)"), Tooltip("超过该时间仍未回贴则退出攀爬")]
        public float climbJumpMaxDuration;

        [LabelText("可翻越最大高度(米)"), Tooltip("低于此高度的墙可直接翻越")]
        public float vaultMaxHeight;

        [LabelText("顶部检测阈值(米)"), Tooltip("距离攀爬面顶部多近时触发攀上")]
        public float topReachThreshold;

        [LabelText("最大墙壁距离(米)"), Tooltip("攀爬中角色距墙面超过此距离则脱离")]
        public float maxWallDistance;

        public static ClimbParams Default => new ClimbParams
        {
            climbSpeed = 1.2f,
            climbAcceleration = 5f,
            wallOffset = 0.32f,
            snapSharpness = 8f,
            maxSnapSpeed = 1f,
            snapDuration = 1.0f,
            groundedExitDelay = 2.0f,
            climbMaxTurnSpeed = 120f,
            gravityScale = 0f,
            climbJumpSpeed = 8f,
            climbJumpNormalForce = 9f,
            climbJumpReattachDelay = 0.25f,
            climbJumpMaxDuration = 1.5f,
            vaultMaxHeight = 1.5f,
            topReachThreshold = 0.5f,
            maxWallDistance = 1.2f,
        };
    }

}
