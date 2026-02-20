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
            else if (MyCore.kcc.monitor.isStableOnGround&&_jumpState.hasEnterTime>0.1f)
            {
                sm?.TryDeactivateState(JUMP_StateName);
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
        [Title("状态")]
        [ReadOnly] public bool isBlocking;
        [ReadOnly] public bool isSliding;
        [ReadOnly] public bool isAiming;
        [ReadOnly] public int weaponIndex;

        [Title("最近触发")]
        [ReadOnly] public float lastAttackTime;
        [ReadOnly] public float lastHeavyAttackTime;

        public void TriggerAttack()
        {
            lastAttackTime = Time.time;
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

        public void SetAim(bool enable)
        {
            isAiming = enable;
        }

        public void SwitchWeaponNext()
        {
            weaponIndex++;
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

        [InlineProperty, HideLabel]
        public VirtualCameraSlot skillCamera = new VirtualCameraSlot();
    }

    [Serializable, TypeRegistryItem("基础相机模块")]
    public class EntityBasicCameraModule : EntityBasicModuleBase
    {
        [Title("模式")]
        public CameraMode mode = CameraMode.ThirdPerson;

        [Title("第一人称")]
        [InlineProperty, HideLabel]
        public CameraRig firstPerson = new CameraRig();

        [Title("第三人称")]
        [InlineProperty, HideLabel]
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
        [InlineProperty, HideLabel]
        public VirtualCameraSlot main = new VirtualCameraSlot();

        [Title("扩展槽位 A")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot slotA = new VirtualCameraSlot();

        [Title("扩展槽位 B")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot slotB = new VirtualCameraSlot();

        [Title("Dynamic")]
        [InlineProperty, HideLabel]
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
