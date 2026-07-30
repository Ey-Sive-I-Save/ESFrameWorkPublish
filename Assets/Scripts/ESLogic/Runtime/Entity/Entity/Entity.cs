using System;
using System.Collections.Generic;
using KinematicCharacterController;
using Sirenix.OdinInspector;
using UnityEngine;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace ES
{
    // Entity：直接接入 KCC 的角色核心（不走模块，超高频）
    [Serializable, TypeRegistryItem("实体核心")]
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class Entity : Core, ICharacterController
    {
        [LabelText("主 Animator")]
        public Animator animator;

        [NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Entity长期OpSupport")]
        public ESOpSupport opSupport;

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureEntityOpSupport();
                return opSupport;
            }
        }

        [NonSerialized] private ESTagCollection tags;

        /// <summary>Entity is one Tag host. The container itself has no Entity-specific behavior.</summary>
        public ESTagCollection Tags => tags ??= CreateTagCollection();

        /// <summary>标签引用计数变化。回调在写入方同步执行，订阅者不得在回调内修改同一 Tag。</summary>
        public event Action<ESGameTag, int, int> OnGameTagCountChanged;

        /// <summary>标签从不存在变为存在，或从存在变为不存在时触发。</summary>
        public event Action<ESGameTag, bool> OnGameTagPresenceChanged;

        public event Action<ESTagId, int, int> OnTagCountChanged;
        public event Action<ESTagId, bool> OnTagPresenceChanged;

        #region Domains

        [TabGroup("生命体结构", "身体基础"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBasicDomain basicDomain = new EntityBasicDomain();

        [TabGroup("生命体结构", "意识AI"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityAIDomain aiDomain = new EntityAIDomain();

        [TabGroup("生命体结构", "Buff域"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBuffDomain buffDomain = new EntityBuffDomain();

        [TitleGroup("属性基础表（角色 Schema）", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public ESSuperAttributeTable superAttributes = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();

        [TabGroup("生命体结构", "状态表现"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityStateDomain stateDomain = new EntityStateDomain();

        #endregion

        #region KCC

        [Title("身体运动核心（KCC，高频）")]
        [HideLabel]
        public EntityKCCData kcc = new EntityKCCData();

        #endregion

        #region Lifecycle

        protected override void OnBeforeAwakeRegister()
        {
            EnsureEntityStructure();
            EnsureEntityOpSupport();
            Tags.Warmup();
            InitializeKCC();
        }

        private void Reset()
        {
            EnsureEntityStructure();
        }

        private void OnValidate()
        {
            EnsureEntityStructure();
        }

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            // 统一注册：只注册需要参与当前实体运行的域
            RegisterDomain(basicDomain);
            RegisterDomain(aiDomain);
            RegisterDomain(buffDomain);
            RegisterDomain(stateDomain);
        }

        #endregion

        #region 运行逻辑

        protected override void Update()
        {
            base.Update();
        }

        protected override void OnDestroy()
        {
            if (tags != null)
            {
                tags.OnTagCountChanged -= HandleTagCountChanged;
                tags.OnTagPresenceChanged -= HandleTagPresenceChanged;
                tags.Dispose();
                tags = null;
            }
            base.OnDestroy();

            opSupport?.Dispose();

            opSupport = null;
        }

        #endregion

        #region KCC API

        public void InitializeKCC()
        {
            kcc.Initialize(this);
        }

        public void EnsureEntityStructure()
        {
            basicDomain ??= new EntityBasicDomain();
            aiDomain ??= new EntityAIDomain();
            buffDomain ??= new EntityBuffDomain();
            superAttributes ??= ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            ESCharacterAttributeCatalog.EnsureCharacterScope(superAttributes);
            buffDomain.BindSuperAttributeTable(superAttributes);
            stateDomain ??= new EntityStateDomain();
            stateDomain.stateMachine ??= new StateMachine();
            kcc ??= new EntityKCCData();
        }

        public void EnsureEntityOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.CreateStandalone();

            if (opSupport.Kind != ESOpSupportKind.Entity || opSupport.OwnerEntity != this)
                opSupport.InitializeEntityOwner(this, GetInstanceID());
        }

        private ESTagCollection CreateTagCollection()
        {
            var collection = new ESTagCollection();
            collection.OnTagCountChanged += HandleTagCountChanged;
            collection.OnTagPresenceChanged += HandleTagPresenceChanged;
            return collection;
        }

        private void HandleTagCountChanged(ESTagId tag, int previous, int current)
        {
            OnTagCountChanged?.Invoke(tag, previous, current);
            if (ESGameTagCatalog.TryFromCoreId(tag, out ESGameTag coreTag))
                OnGameTagCountChanged?.Invoke(coreTag, previous, current);
        }

        private void HandleTagPresenceChanged(ESTagId tag, bool present)
        {
            OnTagPresenceChanged?.Invoke(tag, present);
            if (ESGameTagCatalog.TryFromCoreId(tag, out ESGameTag coreTag))
                OnGameTagPresenceChanged?.Invoke(coreTag, present);
        }

        #endregion

        #region 游戏标签 API

        public bool HasGameTag(ESGameTag tag)
        {
            return Tags.Has(ESTagId.FromInt32((ushort)tag));
        }

        public bool HasGameTag(ESTagId tag)
        {
            return Tags.Has(tag);
        }

        public byte GetGameTagCount(ESGameTag tag)
        {
            return (byte)Math.Min(byte.MaxValue, Tags.GetCount(ESTagId.FromInt32((ushort)tag)));
        }

        public byte GetGameTagCount(ESTagId tag)
        {
            return (byte)Math.Min(byte.MaxValue, Tags.GetCount(tag));
        }

        public ESTagMask64 GetGameTagMask()
        {
            return Tags.HotMask;
        }

        public bool HasAnyGameTag(ESTagMask64 mask)
        {
            return Tags.HasAny(mask);
        }

        public bool HasAllGameTags(ESTagMask64 mask)
        {
            return Tags.HasAll(mask);
        }

        /// <summary>
        /// Evaluates a compiled Core plus Extension Tag condition. The common Core-only path is
        /// two mask tests and does not touch the sparse extension dictionary.
        /// </summary>
        public bool MatchesTagCondition(ESTagConditionRuntime condition)
        {
            return Tags.Matches(condition);
        }

        /// <summary>
        /// Business-facing condition query. The configuration owns stable Core and StringKey
        /// identities; its current-process RuntimeKey representation stays internal.
        /// </summary>
        public bool MatchesTagCondition(ESTagConditionConfig config)
        {
            return Tags.Matches(config);
        }

        /// <summary>
        /// The explicit diagnostic form of <see cref="MatchesTagCondition"/>. A false return
        /// means the condition itself cannot be evaluated under the active Catalog; a true
        /// return with <paramref name="matches"/> false means it was evaluated and did not match.
        /// </summary>
        public bool TryMatchesTagCondition(ESTagConditionRuntime condition, out bool matches, out string error)
        {
            return Tags.TryMatches(condition, out matches, out error);
        }

        /// <summary>
        /// Diagnostic form of the stable configuration query. A false return means the
        /// configuration cannot be compiled or evaluated under the active Tag Catalog.
        /// </summary>
        public bool TryMatchesTagCondition(ESTagConditionConfig config, out bool matches, out string error)
        {
            return Tags.TryMatches(config, out matches, out error);
        }

        public void ClearGameTags()
        {
            Tags.Clear();
        }

        public int GetTagCount(ESTagId tag)
        {
            return Tags.GetCount(tag);
        }

        public ESTagDebugSnapshot GetTagDebugSnapshot()
        {
            return Tags.GetDebugSnapshot();
        }

        /// <summary>
        /// Captures persistent or replicated Tag identities. The payload deliberately contains
        /// only stable EnumKey/StringKey values and the Catalog SchemaHash, never a process-local
        /// RuntimeKey, count, or lease source.
        /// </summary>
        public bool TryCreateStableTagSnapshot(ESTagStableTransferScope scope, out ESTagStableSnapshot snapshot, out string error)
        {
            return Tags.TryCreateStableSnapshot(scope, out snapshot, out error);
        }

        #endregion

        #region KCC API

        public void SetMoveInput(Vector3 moveInput)
        {
            kcc.SetMoveInput(moveInput);
        }

        public void SetLookInput(Vector3 lookInput)
        {
            kcc.SetLookInput(lookInput);
        }

        public void ResetKCCInputs()
        {
            kcc.ResetInputs();
        }

        public void RequestJump()
        {
            kcc.RequestJump();
        }

        public void SetCrouch(bool enable)
        {
            kcc.SetCrouch(enable);
        }

        public void SetRootMotionVelocity(Vector3 velocity)
        {
            kcc.SetRootMotionVelocity(velocity);
        }

        public void ClearRootMotionVelocity()
        {
            kcc.ClearRootMotionVelocity();
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            kcc.SetSpeedMultiplier(multiplier);
        }

        public void SetSpeedLimit(float limit)
        {
            kcc.SetSpeedLimit(limit);
        }

        public void ResetSpeedModifiers()
        {
            kcc.ResetSpeedModifiers();
        }

        public void SetLocomotionSupportFlags(StateSupportFlags flags)
        {
            stateDomain.stateMachine.SetSupportFlags(flags);
        }

        public void SetVerticalInput(float input)
        {
            kcc.SetVerticalInput(input);
        }

        #endregion

        #region ICharacterController

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            kcc.UpdateRotation(this, ref currentRotation, deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            kcc.UpdateVelocity(this, ref currentVelocity, deltaTime);
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            kcc.BeforeCharacterUpdate(this, deltaTime);
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            kcc.PostGroundingUpdate(this, deltaTime);
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            kcc.AfterCharacterUpdate(this, deltaTime);
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return kcc.IsColliderValidForCollisions(this, coll);
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            kcc.OnGroundHit(this, hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            kcc.OnMovementHit(this, hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            kcc.ProcessHitStabilityReport(this, hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, ref hitStabilityReport);
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            kcc.OnDiscreteCollisionDetected(this, hitCollider);
        }

        #endregion
    }

    /// <summary>
    /// 将“一个被控制实体”的部分 GameTag 单向投影为全局 RuntimeModeTag。
    /// 该投影只服务输入/UI 策略，不能反向修改实体事实；多人或多实体场景必须由控制权系统
    /// 显式 Bind 当前本地控制实体，避免把其他 NPC 的死亡、眩晕投影到玩家输入。
    /// </summary>
    public sealed class ESGameTagRuntimeModeProjector : IDisposable
    {
        private Entity entity;
        private ESRuntimeModeService modeService;
        private ESRuntimeModeTagHandle combatHandle;
        private ESRuntimeModeTagHandle aimingHandle;
        private ESRuntimeModeTagHandle mountedHandle;
        private ESRuntimeModeTagHandle climbingHandle;
        private ESRuntimeModeTagHandle deadHandle;
        private ESRuntimeModeTagHandle stunnedHandle;

        public bool IsBound => entity != null && modeService != null;

        public void Bind(Entity controlledEntity, ESRuntimeModeService runtimeModeService)
        {
            Dispose();
            if (controlledEntity == null || runtimeModeService == null)
                return;

            entity = controlledEntity;
            modeService = runtimeModeService;
            entity.OnGameTagPresenceChanged += HandleGameTagPresenceChanged;

            Sync(ESGameTag.战斗类_战斗中);
            Sync(ESGameTag.战斗类_瞄准中);
            Sync(ESGameTag.移动类_骑乘中);
            Sync(ESGameTag.移动类_攀爬中);
            Sync(ESGameTag.生命类_死亡);
            Sync(ESGameTag.控制类_眩晕);
        }

        public void Dispose()
        {
            if (entity != null)
                entity.OnGameTagPresenceChanged -= HandleGameTagPresenceChanged;

            Release(ref combatHandle);
            Release(ref aimingHandle);
            Release(ref mountedHandle);
            Release(ref climbingHandle);
            Release(ref deadHandle);
            Release(ref stunnedHandle);
            entity = null;
            modeService = null;
        }

        private void Sync(ESGameTag tag)
        {
            if (entity != null)
                HandleGameTagPresenceChanged(tag, entity.HasGameTag(tag));
        }

        private void HandleGameTagPresenceChanged(ESGameTag tag, bool present)
        {
            if (modeService == null)
                return;

            switch (tag)
            {
                case ESGameTag.战斗类_战斗中:
                    SynchronizeTag(ESRuntimeModeTag.Combat, ref combatHandle, present);
                    break;
                case ESGameTag.战斗类_瞄准中:
                    SynchronizeTag(ESRuntimeModeTag.Aiming, ref aimingHandle, present);
                    break;
                case ESGameTag.移动类_骑乘中:
                    SynchronizeTag(ESRuntimeModeTag.Mounted, ref mountedHandle, present);
                    break;
                case ESGameTag.移动类_攀爬中:
                    SynchronizeTag(ESRuntimeModeTag.Climbing, ref climbingHandle, present);
                    break;
                case ESGameTag.生命类_死亡:
                    SynchronizeTag(ESRuntimeModeTag.Dead, ref deadHandle, present);
                    break;
                case ESGameTag.控制类_眩晕:
                    SynchronizeTag(ESRuntimeModeTag.Stunned, ref stunnedHandle, present);
                    break;
            }
        }

        private void SynchronizeTag(ESRuntimeModeTag tag, ref ESRuntimeModeTagHandle handle, bool present)
        {
            if (present)
            {
                if (!handle.IsValid)
                    handle = modeService.AddTag(tag, entity);
                return;
            }

            Release(ref handle);
        }

        private void Release(ref ESRuntimeModeTagHandle handle)
        {
            if (modeService != null && handle.IsValid)
                modeService.RemoveTag(handle);
            handle = ESRuntimeModeTagHandle.Invalid;
        }
    }

    #region KCC Data

    [Serializable]
    public class EntityKCCData
    {
        [Title("KCC 组件")]
        [LabelText("角色运动器")]
        public KinematicCharacterMotor motor;

        [Title("稳定地面移动")]
        [LabelText("地面最大速度")]
        public float maxStableMoveSpeed = 8f;
        [LabelText("地面速度响应")]
        public float stableMovementSharpness = 15f;

        [Title("空中移动")]
        [LabelText("空中最大速度")]
        public float maxAirMoveSpeed = 8f;
        [LabelText("空中加速度")]
        public float airAccelerationSpeed = 5f;
        [LabelText("空中阻力")]
        public float drag = 0.1f;

        [Title("速度倍率/限速")]
        [LabelText("速度倍率")]
        public float speedMultiplier = 1f;
        [LabelText("平面速度上限")]
        [Tooltip("<=0 表示不限制")]
        public float speedLimit = 0f;

        [Title("跳跃")]
        [LabelText("基础跳跃速度")]
        public float jumpSpeed = 8f;
        [LabelText("跳跃速度倍率")]
        [Tooltip("跳跃速度倍率（降低跳跃高度）")]
        public float jumpSpeedMultiplier = 0.8f;
        [LabelText("上升重力倍率")]
        [Tooltip("上升阶段重力倍率(>1 更短更硬)")]
        public float jumpApexGravityMultiplier = 2f;
        [LabelText("下落重力倍率")]
        [Tooltip("下落阶段重力倍率(>1 更快落地)")]
        public float jumpFallGravityMultiplier = 1.3f;

        [Title("下蹲")]
        [LabelText("站立胶囊高度")]
        public float standingCapsuleHeight = 2f;
        [LabelText("下蹲胶囊高度")]
        public float crouchedCapsuleHeight = 1f;
        [LabelText("下蹲速度倍率")]
        [Tooltip("下蹲移动速度倍率")]
        public float crouchSpeedMultiplier = 0.5f;

        [Title("旋转")]
        [LabelText("朝向响应")]
        public float orientationSharpness = 10f;

        [Title("重力")]
        [LabelText("重力向量")]
        public Vector3 gravity_ = new Vector3(0f, -9.81f, 0f);

        [Title("跳跃请求")]
        [LabelText("跳跃请求缓冲时长(秒)")]
        [Tooltip("跳跃请求超过该时长仍未在地面被消费，则自动过期，避免落地后二次起跳。")]
        public float jumpRequestBufferTime = 0.12f;

        [Title("根运动")]
        [LabelText("启用根运动速度")]
        public bool useRootMotion = true;
        [LabelText("根运动倍率")]
        public float rootMotionScale = 1f;
        [LabelText("仅稳定地面应用")]
        public bool rootMotionGroundOnly = true;

        [Title("输入（世界空间）")]
        [LabelText("移动输入")]
        public Vector3 moveInput;
        [LabelText("朝向输入")]
        public Vector3 lookInput;

        [LabelText("垂直输入")]
        public float verticalInput;

        [Title("Monitor（运行监视）")]
        [HideLabel]
        public EntityKCCMonitor monitor = new EntityKCCMonitor();

        [LabelText("Monitor调试")]
        public bool debugMonitor = false;

        [LabelText("防止静止上漂")]
        public bool preventUpwardDriftWhenIdle = true;

        [LabelText("上漂阈值(米/帧)")]
        public float upwardDriftThreshold = 0.005f;

        private Vector3 _lastVelocity;
        private Vector3 _rootMotionVelocity;
        private int _rootMotionWriteFrame = -1;
        private bool _jumpRequested;
        private float _jumpRequestTime = -999f;
        private bool _crouchRequested;
        private bool _isCrouched;
        private Vector3 _lastTransientPosition;

        [NonSerialized] private bool _matchTargetPoseActive;
        [NonSerialized] private bool _matchTargetReleaseAfterApply;
        [NonSerialized] private Vector3 _matchTargetPendingPosition;
        [NonSerialized] private Quaternion _matchTargetPendingRotation = Quaternion.identity;
        [NonSerialized] private int _matchTargetPoseSequence;
        [NonSerialized] private int _matchTargetConsumedSequence;
        [NonSerialized] private bool _matchTargetAppliedThisTick;

        [ShowInInspector, ReadOnly, LabelText("跳跃请求中")]
        public bool JumpRequested => _jumpRequested;

        [ShowInInspector, ReadOnly, LabelText("最近KCC跳跃请求帧")]
        public int lastKccJumpRequestFrame;

        [ShowInInspector, ReadOnly, LabelText("最近KCC起跳帧")]
        public int lastKccJumpApplyFrame;

        [ShowInInspector, ReadOnly, LabelText("最近KCC跳跃过期帧")]
        public int lastKccJumpExpiredFrame;

        [NonSerialized]
        public EntityBasicFlyModule flyModule;

        [NonSerialized]
        public EntityBasicSwimModule swimModule;

        [NonSerialized]
        public EntityBasicClimbModule climbModule;

        [NonSerialized]
        public EntityBasicMountModule mountModule;

        [NonSerialized] private ESWorkScheduler<IEntityKCCBeforeMotion> _beforeScheduler;
        [NonSerialized] private ESWorkScheduler<IEntityKCCRotationMotion> _rotationScheduler;
        [NonSerialized] private ESWorkScheduler<IEntityKCCVelocityMotion> _velocityScheduler;
        [NonSerialized] private StateMachine _stateMachine;
        [NonSerialized] private StateSupportFlags _currentSupportFlags;
        [NonSerialized] private bool _motionSchedulersReady;

        [NonSerialized] public int workSelf;
        [NonSerialized] public int workWorld;
        [NonSerialized] public int workOther;

        public StateSupportFlags CurrentSupportFlags => _currentSupportFlags;

        [ShowInInspector, ReadOnly, LabelText("注册的运动前置任务")]
        public int RegisteredBeforeMotionCount => _beforeScheduler != null ? _beforeScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("注册的旋转任务")]
        public int RegisteredRotationMotionCount => _rotationScheduler != null ? _rotationScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("注册的速度任务")]
        public int RegisteredVelocityMotionCount => _velocityScheduler != null ? _velocityScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("扩展运动已接管速度")]
        public bool lastVelocityHandledByFeature;

        [ShowInInspector, ReadOnly, LabelText("MatchTarget 位姿待应用")]
        public bool HasPendingMatchTargetPose => _matchTargetPoseActive && _matchTargetPoseSequence != _matchTargetConsumedSequence;

        /// <summary>
        /// MatchTarget 活跃期间由 KCC 维护根位姿；普通朝向、重力、RootMotion 和其它速度能力不得覆盖它。
        /// 该属性只读，供 KCC 自身阶段判断，不参与业务状态机。
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("MatchTarget 运动锁定")]
        public bool IsMatchTargetMotionLocked => _matchTargetPoseActive || _matchTargetAppliedThisTick;

        public bool HasWork => workSelf > 0 || workWorld > 0 || workOther > 0;

        private static float ResolveSuperFloat(Entity owner, ESCharacterFloatAttributeId id, float fallbackValue)
        {
            EntityBuffDomain buffDomain = owner != null ? owner.buffDomain : null;
            return buffDomain != null ? buffDomain.GetCharacterFloatStatValue(id, fallbackValue) : fallbackValue;
        }

        private static bool ResolveSuperPermit(Entity owner, ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            EntityBuffDomain buffDomain = owner != null ? owner.buffDomain : null;
            return buffDomain != null ? buffDomain.GetCharacterPermitValue(id, fallbackValue) : fallbackValue;
        }

        private void ResetWork()
        {
            workSelf = 100;
            workWorld = 100;
            workOther = 100;
        }

        public void StopWork()
        {
            workSelf = 0;
            workWorld = 0;
            workOther = 0;
        }

        public void Initialize(Entity owner)
        {
            if (owner == null)
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：owner 为空。");
                return;
            }
            if (motor == null)
            {
                motor = owner.GetComponent<KinematicCharacterMotor>();
                if (motor == null)
                {
                    motor = owner.gameObject.AddComponent<KinematicCharacterMotor>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[EntityKCCData] {owner.name} 缺少 KinematicCharacterMotor，已自动补齐。建议在预制体上固定配置 KCC 参数。", owner);
#endif
                }
            }
            _stateMachine = owner.stateDomain != null ? owner.stateDomain.stateMachine : null;
            if (motor != null)
            {
                motor.CharacterController = owner;
                if (motor.Capsule != null && standingCapsuleHeight <= 0f)
                {
                    standingCapsuleHeight = motor.Capsule.height;
                }
                if (crouchedCapsuleHeight <= 0f)
                {
                    crouchedCapsuleHeight = Mathf.Max(0.5f, standingCapsuleHeight * 0.5f);
                }
            }
            if (motor != null)
            {
                _lastTransientPosition = motor.TransientPosition;
            }
            else
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：缺少 KinematicCharacterMotor。");
                return;
            }

            if (_stateMachine == null)
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：缺少 StateMachine。");
                return;
            }

            EnsureMotionSchedulers();
        }

        public void SetMoveInput(Vector3 input)
        {
            moveInput = Vector3.ClampMagnitude(input, 1f);
        }

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SetLookInput(Vector3 input)
        {
            lookInput = input.sqrMagnitude > 0f ? input.normalized : Vector3.zero;
        }

        public void ResetInputs()
        {
            moveInput = Vector3.zero;
            lookInput = Vector3.zero;
            verticalInput = 0f;
            _jumpRequested = false;
            _jumpRequestTime = -999f;
        }

        public void RequestJump()
        {
            _jumpRequested = true;
            _jumpRequestTime = Time.time;
            lastKccJumpRequestFrame = Time.frameCount;
        }

        public void SetCrouch(bool enable)
        {
            _crouchRequested = enable;
        }

        public void SetRootMotionVelocity(Vector3 velocity)
        {
            _rootMotionVelocity = velocity;
            _rootMotionWriteFrame = Time.frameCount;
        }

        public void ClearRootMotionVelocity()
        {
            _rootMotionVelocity = Vector3.zero;
            _rootMotionWriteFrame = -1;
        }

        /// <summary>
        /// 提交由 State/Animator 计算出的 MatchTarget 根位姿。
        /// 位姿在下一个 KCC BeforeCharacterUpdate 边界应用，避免普通 Update 直接争写 Motor。
        /// </summary>
        public void QueueMatchTargetPose(Vector3 position, Quaternion rotation, bool releaseAfterApply)
        {
            _matchTargetPendingPosition = position;
            _matchTargetPendingRotation = rotation;
            _matchTargetReleaseAfterApply = releaseAfterApply;
            _matchTargetPoseActive = true;

            if (_matchTargetPoseSequence == int.MaxValue)
            {
                _matchTargetPoseSequence = 1;
                _matchTargetConsumedSequence = 0;
            }
            else
            {
                _matchTargetPoseSequence++;
            }
        }

        /// <summary>
        /// 取消尚未进入物理边界的 MatchTarget 位姿。
        /// </summary>
        public void ClearMatchTargetPose()
        {
            _matchTargetPoseActive = false;
            _matchTargetReleaseAfterApply = false;
            _matchTargetConsumedSequence = _matchTargetPoseSequence;
        }

        /// <summary>
        /// 当渲染帧快于物理帧时，MatchTarget 继续以上一次尚未应用的计划位姿为计算起点，
        /// 避免多个 Update 都从同一个 Motor 物理位置重复计算而丢失推进量。
        /// </summary>
        public bool TryGetPendingMatchTargetPose(out Vector3 position, out Quaternion rotation)
        {
            if (_matchTargetPoseActive && _matchTargetPoseSequence != _matchTargetConsumedSequence)
            {
                position = _matchTargetPendingPosition;
                rotation = _matchTargetPendingRotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetSpeedLimit(float limit)
        {
            speedLimit = limit;
        }

        public void ResetSpeedModifiers()
        {
            speedMultiplier = 1f;
            speedLimit = 0f;
        }

        public void BeforeCharacterUpdate(Entity owner, float deltaTime)
        {
            _matchTargetAppliedThisTick = false;
            ApplyPendingMatchTargetPose();
            _lastTransientPosition = motor.TransientPosition;
            ApplyCrouch();

            EnsureMotionSchedulers();
            _currentSupportFlags = _stateMachine.currentSupportFlags;
            _beforeScheduler.Reset();
            ResetWork();
            if (!HasWork)
                return;

            Vector3 initialPosition = motor.TransientPosition;
            for (int i = 0; i < _beforeScheduler.Count && HasWork; i++)
            {
                if (_beforeScheduler.Get(i).BeforeCharacterUpdate(owner, this, initialPosition, deltaTime))
                    StopWork();
            }
        }

        private void ApplyPendingMatchTargetPose()
        {
            if (!_matchTargetPoseActive || _matchTargetPoseSequence == _matchTargetConsumedSequence)
                return;

            motor.SetPositionAndRotation(
                _matchTargetPendingPosition,
                _matchTargetPendingRotation,
                true);
            _matchTargetConsumedSequence = _matchTargetPoseSequence;
            _matchTargetAppliedThisTick = true;

            if (_matchTargetReleaseAfterApply)
            {
                _matchTargetPoseActive = false;
                _matchTargetReleaseAfterApply = false;
            }
        }

        public void UpdateRotation(Entity owner, ref Quaternion currentRotation, float deltaTime)
        {
            if (IsMatchTargetMotionLocked)
            {
                currentRotation = motor.TransientRotation;
                return;
            }

            EnsureMotionSchedulers();
            _currentSupportFlags = _stateMachine.currentSupportFlags;
            _rotationScheduler.Reset();
            ResetWork();
            if (HasWork)
            {
                Quaternion initialRotation = currentRotation;
                for (int i = 0; i < _rotationScheduler.Count && HasWork; i++)
                {
                    if (_rotationScheduler.Get(i).UpdateRotation(owner, this, initialRotation, ref currentRotation, deltaTime))
                    {
                        StopWork();
                        return;
                    }
                }
            }

            if (!ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Rotate, true))
                return;

            float finalOrientationSharpness = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.OrientationSharpness, orientationSharpness);
            if (lookInput.sqrMagnitude <= 0f || finalOrientationSharpness <= 0f)
                return;

            Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, lookInput, 1f - Mathf.Exp(-finalOrientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
        }

        public void UpdateVelocity(Entity owner, ref Vector3 currentVelocity, float deltaTime)
        {
            bool canMove = ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Move, true);
            bool canJump = ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Jump, true);
            Vector3 effectiveMoveInput = canMove ? moveInput : Vector3.zero;

            float multiplier = Mathf.Max(0f, speedMultiplier);
            float stableMaxSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.GroundMaxMoveSpeed, maxStableMoveSpeed) * multiplier;
            float airMaxSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.AirMaxMoveSpeed, maxAirMoveSpeed) * multiplier;
            float finalCrouchSpeedMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.CrouchSpeedMultiplier, crouchSpeedMultiplier);
            float finalGroundMovementSharpness = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.GroundMovementSharpness, stableMovementSharpness);
            float finalJumpSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpSpeed, jumpSpeed);
            float finalJumpSpeedMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpSpeedMultiplier, jumpSpeedMultiplier);
            float finalAirAcceleration = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.AirAcceleration, airAccelerationSpeed);
            float finalApexGravityMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpApexGravityMultiplier, jumpApexGravityMultiplier);
            float finalFallGravityMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpFallGravityMultiplier, jumpFallGravityMultiplier);
            float finalDrag = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.Drag, drag);
            float finalRootMotionScale = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.RootMotionScale, rootMotionScale);
            if (_isCrouched)
                stableMaxSpeed *= Mathf.Clamp01(finalCrouchSpeedMultiplier);
            if (speedLimit > 0f)
            {
                stableMaxSpeed = Mathf.Min(stableMaxSpeed, speedLimit);
                airMaxSpeed = Mathf.Min(airMaxSpeed, speedLimit);
            }

            Vector3 targetMovementVelocity = Vector3.zero;
            bool handled = false;
            lastVelocityHandledByFeature = false;
            if (IsMatchTargetMotionLocked)
            {
                currentVelocity = Vector3.zero;
                _lastVelocity = currentVelocity;
                return;
            }

            _currentSupportFlags = _stateMachine.currentSupportFlags;
            EnsureMotionSchedulers();
            _velocityScheduler.Reset();
            ResetWork();
            if (HasWork)
            {
                Vector3 initialVelocity = currentVelocity;
                for (int i = 0; i < _velocityScheduler.Count && HasWork; i++)
                {
                    if (_velocityScheduler.Get(i).UpdateVelocity(owner, this, initialVelocity, ref currentVelocity, deltaTime))
                    {
                        handled = true;
                        lastVelocityHandledByFeature = true;
                        StopWork();
                        break;
                    }
                }
            }

            if (!handled && motor.GroundingStatus.IsStableOnGround)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                    lastKccJumpExpiredFrame = Time.frameCount;
                }

                currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                Vector3 inputRight = Vector3.Cross(effectiveMoveInput, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * effectiveMoveInput.magnitude;
                targetMovementVelocity = reorientedInput * stableMaxSpeed;

                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-finalGroundMovementSharpness * deltaTime));

                if (_jumpRequested && canJump)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    lastKccJumpApplyFrame = Time.frameCount;
                    motor.ForceUnground(0.1f);
                    float appliedJumpSpeed = finalJumpSpeed * Mathf.Max(0f, finalJumpSpeedMultiplier);
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp) + (motor.CharacterUp * appliedJumpSpeed);
                }
            }
            else if (!handled)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    lastKccJumpExpiredFrame = Time.frameCount;
                }

                if (effectiveMoveInput.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = effectiveMoveInput * airMaxSpeed;

                    if (motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), motor.CharacterUp).normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity_);
                    currentVelocity += velocityDiff * finalAirAcceleration * deltaTime;
                }

                float gravityScale = 1f;
                float upVel = Vector3.Dot(currentVelocity, motor.CharacterUp);
                if (upVel > 0.01f)
                    gravityScale = Mathf.Max(0f, finalApexGravityMultiplier);
                else if (upVel < -0.01f)
                    gravityScale = Mathf.Max(0f, finalFallGravityMultiplier);

                currentVelocity += gravity_ * (gravityScale * deltaTime);
                currentVelocity *= (1f / (1f + (finalDrag * deltaTime)));
            }

            if (useRootMotion)
            {
                bool rootMotionFresh = _rootMotionWriteFrame >= 0 && Time.frameCount - _rootMotionWriteFrame <= 1;
                bool canApply = rootMotionFresh && (!rootMotionGroundOnly || motor.GroundingStatus.IsStableOnGround);
                if (canApply)
                    currentVelocity += _rootMotionVelocity * finalRootMotionScale;
                else if (!rootMotionFresh)
                    _rootMotionVelocity = Vector3.zero;
            }

            if (speedLimit > 0f)
            {
                Vector3 up = motor.CharacterUp;
                Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, up);
                float planarMag = planar.magnitude;
                if (planarMag > speedLimit)
                {
                    Vector3 vertical = Vector3.Project(currentVelocity, up);
                    currentVelocity = planar.normalized * speedLimit + vertical;
                }
            }

            _lastVelocity = currentVelocity;
        }

        /// <summary>
        /// 将一个运动能力注册到它实际实现的 KCC 阶段。
        /// 新增运动能力只需要实现对应接口并注册，不再修改 EntityKCCData 的中央字段表。
        /// </summary>
        public EntityKCCMotionRegistration RegisterMotionFeature(
            object feature,
            EntityKCCMotionOrder order)
        {
            EnsureMotionSchedulers();

            EntityKCCMotionRegistration registration = default;
            if (feature is IEntityKCCBeforeMotion beforeMotion)
                registration.beforeHandle = _beforeScheduler.Register(beforeMotion, order.before);
            if (feature is IEntityKCCRotationMotion rotationMotion)
                registration.rotationHandle = _rotationScheduler.Register(rotationMotion, order.rotation);
            if (feature is IEntityKCCVelocityMotion velocityMotion)
                registration.velocityHandle = _velocityScheduler.Register(velocityMotion, order.velocity);

            return registration;
        }

        /// <summary>
        /// 注销一个运动能力的全部阶段注册。重复调用安全。
        /// </summary>
        public void UnregisterMotionFeature(ref EntityKCCMotionRegistration registration)
        {
            if (_beforeScheduler != null && registration.beforeHandle.IsValid)
                _beforeScheduler.Unregister(registration.beforeHandle);
            if (_rotationScheduler != null && registration.rotationHandle.IsValid)
                _rotationScheduler.Unregister(registration.rotationHandle);
            if (_velocityScheduler != null && registration.velocityHandle.IsValid)
                _velocityScheduler.Unregister(registration.velocityHandle);

            registration.Clear();
        }

        private void EnsureMotionSchedulers()
        {
            if (_motionSchedulersReady)
                return;

            if (_beforeScheduler == null)
                _beforeScheduler = new ESWorkScheduler<IEntityKCCBeforeMotion>();
            _beforeScheduler.Warmup(8, 4);

            if (_rotationScheduler == null)
                _rotationScheduler = new ESWorkScheduler<IEntityKCCRotationMotion>();
            _rotationScheduler.Warmup(8, 4);

            if (_velocityScheduler == null)
                _velocityScheduler = new ESWorkScheduler<IEntityKCCVelocityMotion>();
            _velocityScheduler.Warmup(8, 4);

            _motionSchedulersReady = true;
        }

        private void ApplyCrouch()
        {
            if (_crouchRequested == _isCrouched) return;

            _isCrouched = _crouchRequested;
            float radius = motor.Capsule.radius;
            if (_isCrouched)
            {
                motor.SetCapsuleDimensions(radius, crouchedCapsuleHeight, crouchedCapsuleHeight * 0.5f);
            }
            else
            {
                motor.SetCapsuleDimensions(radius, standingCapsuleHeight, standingCapsuleHeight * 0.5f);
            }
        }

        public void PostGroundingUpdate(Entity owner, float deltaTime)
        {
            // 预留扩展
        }

        public void AfterCharacterUpdate(Entity owner, float deltaTime)
        {

            if (preventUpwardDriftWhenIdle)
            {
                Vector3 posDelta = motor.TransientPosition - _lastTransientPosition;
                bool noInput = moveInput.sqrMagnitude <= 0.0001f && Mathf.Abs(verticalInput) <= 0.0001f;
                bool noVelocity = _lastVelocity.sqrMagnitude <= 0.0001f && _rootMotionVelocity.sqrMagnitude <= 0.0001f;
                if (posDelta.y > upwardDriftThreshold && noInput && noVelocity)
                {
                    if (debugMonitor)
                    {
                        Debug.LogWarning($"[KCC-Monitor] Clamp upward drift | deltaY={posDelta.y:F4}");
                    }
                    motor.SetPosition(_lastTransientPosition, true);
                }
            }
            monitor.UpdateFromMotor(motor, _lastVelocity);
        }


        public bool IsColliderValidForCollisions(Entity owner, Collider coll)
        {
            return true;
        }

        public void OnGroundHit(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void OnMovementHit(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void ProcessHitStabilityReport(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void OnDiscreteCollisionDetected(Entity owner, Collider hitCollider)
        {
            // 预留扩展
        }


    }

    [Serializable]
    public class EntityKCCMonitor
    {
        [LabelText("是否存在 Motor")]
        public bool hasMotor;

        [LabelText("是否稳定在地面")]
        public bool isStableOnGround;

        [LabelText("速度")]
        public Vector3 velocity;

        [LabelText("位置")]
        public Vector3 position;

        [LabelText("朝向")]
        public Quaternion rotation;

        public void UpdateFromMotor(KinematicCharacterMotor motor, Vector3 currentVelocity)
        {
            hasMotor = motor != null;
            isStableOnGround = motor.GroundingStatus.IsStableOnGround;
            velocity = currentVelocity;
            position = motor.TransientPosition;
            rotation = motor.TransientRotation;
        }
    }

    #endregion
}
