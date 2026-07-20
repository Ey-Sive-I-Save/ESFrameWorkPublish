using System;
using Sirenix.OdinInspector;
using UnityEngine;
using KinematicCharacterController;

namespace ES
{
    // Entity：直接接入 KCC 的角色核心（不走模块，超高频）
    [Serializable, TypeRegistryItem("实体核心")]
    public class Entity : Core, ICharacterController
    {
        [LabelText("主 Animator")]
        public Animator animator;

        [LabelText("Entity长期OpSupport"), SerializeReference]
        public ESOpSupport opSupport = new ESOpSupport();

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureEntityOpSupport();
                return opSupport;
            }
        }

        [NonSerialized] private Animator _cachedStateDriverAnimator;
        [NonSerialized] private StateFinalIKDriver _cachedStateFinalIKDriver;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("游戏标签")]
        private ESTagRefCountSet64 gameTags;

        #region Domains

        [TabGroup("生命体结构", "身体基础"), HideLabel, SerializeReference]
        public EntityBasicDomain basicDomain;

        [TabGroup("生命体结构", "意识AI"), HideLabel, SerializeReference]
        public EntityAIDomain aiDomain;

        [TabGroup("生命体结构", "Buff域"), HideLabel, SerializeReference]
        public EntityBuffDomain buffDomain;

        [TabGroup("生命体结构", "状态表现"), HideLabel, SerializeReference]
        public EntityStateDomain stateDomain;

        #endregion

        #region 生命体关系链

        public StateMachine StateMachineOrNull => stateDomain != null ? stateDomain.stateMachine : null;

        public Animator StateAnimatorOrNull
        {
            get
            {
                var stateMachine = StateMachineOrNull;
                return stateMachine != null && stateMachine.BoundAnimator != null ? stateMachine.BoundAnimator : animator;
            }
        }

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, PropertyOrder(-20), FoldoutGroup("生命体关系链", expanded: true), LabelText("动画器")]
        private Animator InspectorStateAnimator => StateAnimatorOrNull;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, PropertyOrder(-19), FoldoutGroup("生命体关系链"), LabelText("状态机")]
        private StateMachine InspectorStateMachine => StateMachineOrNull;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, PropertyOrder(-18), FoldoutGroup("生命体关系链"), LabelText("IK表现驱动")]
        private StateFinalIKDriver InspectorStateFinalIKDriver => ResolveStateFinalIKDriver();

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, PropertyOrder(-17), FoldoutGroup("生命体关系链"), LabelText("链路状态")]
        private string InspectorStateDriverRelation
        {
            get
            {
                var stateMachine = StateMachineOrNull;
                var stateAnimator = StateAnimatorOrNull;
                var ikDriver = ResolveStateFinalIKDriver();

                if (stateMachine == null) return "缺少 StateDomain/StateMachine";
                if (stateAnimator == null) return "缺少 Animator，状态机无法输出动画";
                if (ikDriver == null) return "缺少 StateFinalIKDriver，IK表现不会接收状态机贡献";
                return stateMachine.isRunning ? "运行中：Entity -> StateDomain -> StateMachine -> StateFinalIKDriver" : "已绑定：等待状态机运行";
            }
        }

        public StateFinalIKDriver ResolveStateFinalIKDriver(bool allowSearchChildren = false)
        {
            var stateAnimator = StateAnimatorOrNull;
            if (stateAnimator == null)
            {
                _cachedStateDriverAnimator = null;
                _cachedStateFinalIKDriver = null;
                return null;
            }

            if (_cachedStateFinalIKDriver != null && _cachedStateDriverAnimator == stateAnimator)
                return _cachedStateFinalIKDriver;

            _cachedStateDriverAnimator = stateAnimator;
            _cachedStateFinalIKDriver = stateAnimator.GetComponent<StateFinalIKDriver>();
            if (_cachedStateFinalIKDriver == null && allowSearchChildren)
                _cachedStateFinalIKDriver = stateAnimator.GetComponentInChildren<StateFinalIKDriver>(true);

            return _cachedStateFinalIKDriver;
        }

        public void ClearStateDriverRelationCache()
        {
            _cachedStateDriverAnimator = null;
            _cachedStateFinalIKDriver = null;
        }

        #endregion

        #region KCC

        [Title("身体运动核心（KCC，高频）")]
        [HideLabel]
        public EntityKCCData kcc = new EntityKCCData();

        #endregion

        #region Lifecycle

        protected override void OnBeforeAwakeRegister()
        {
            EnsureEntityOpSupport();
            gameTags.Warmup();
            InitializeKCC();
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
            base.OnDestroy();

            if (opSupport != null && !opSupport.IsRecycled)
                opSupport.TryAutoPushedToPool();

            opSupport = null;
        }

        #endregion

        #region KCC API

        public void InitializeKCC()
        {
            kcc.Initialize(this);
        }

        public void EnsureEntityOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = new ESOpSupport();

            if (opSupport.Kind != ESOpSupportKind.Entity || opSupport.OwnerEntity != this)
                opSupport.InitializeEntityOwner(this, GetInstanceID());
        }

        #endregion

        #region 游戏标签 API

        public bool AddGameTag(ESGameTag tag)
        {
            gameTags.Warmup();
            return gameTags.Add(tag);
        }

        public bool AddGameTag(ESTagId tag)
        {
            gameTags.Warmup();
            return gameTags.Add(tag);
        }

        public bool RemoveGameTag(ESGameTag tag)
        {
            return gameTags.Remove(tag);
        }

        public bool RemoveGameTag(ESTagId tag)
        {
            return gameTags.Remove(tag);
        }

        public bool RemoveAllGameTag(ESGameTag tag)
        {
            return gameTags.RemoveAll(tag);
        }

        public bool RemoveAllGameTag(ESTagId tag)
        {
            return gameTags.RemoveAll(tag);
        }

        public bool SetGameTagCount(ESGameTag tag, byte count)
        {
            return gameTags.SetCount(tag, count);
        }

        public bool SetGameTagCount(ESTagId tag, byte count)
        {
            return gameTags.SetCount(tag, count);
        }

        public bool HasGameTag(ESGameTag tag)
        {
            return gameTags.Has(tag);
        }

        public bool HasGameTag(ESTagId tag)
        {
            return gameTags.Has(tag);
        }

        public byte GetGameTagCount(ESGameTag tag)
        {
            return gameTags.GetCount(tag);
        }

        public byte GetGameTagCount(ESTagId tag)
        {
            return gameTags.GetCount(tag);
        }

        public bool HasAnyGameTag(ESTagMask64 mask)
        {
            return gameTags.Overlaps(mask);
        }

        public bool HasAllGameTags(ESTagMask64 mask)
        {
            return gameTags.HasAll(mask);
        }

        public void ClearGameTags()
        {
            gameTags.Clear();
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
            if (enable)
            {
                stateDomain.stateMachine.SetSupportFlags(StateSupportFlags.Crouched);
            }
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

    #region KCC Data

    [Serializable]
    public class EntityKCCData
    {
        [Title("KCC 组件")]
        public KinematicCharacterMotor motor;

        [Title("稳定地面移动")]
        public float maxStableMoveSpeed = 8f;
        public float stableMovementSharpness = 15f;

        [Title("空中移动")]
        public float maxAirMoveSpeed = 8f;
        public float airAccelerationSpeed = 5f;
        public float drag = 0.1f;

        [Title("速度倍率/限速")]
        public float speedMultiplier = 1f;
        [Tooltip("<=0 表示不限制")]
        public float speedLimit = 0f;

        [Title("跳跃")]
        public float jumpSpeed = 8f;
        [Tooltip("跳跃速度倍率（降低跳跃高度）")]
        public float jumpSpeedMultiplier = 0.8f;
        [Tooltip("上升阶段重力倍率(>1 更短更硬)")]
        public float jumpApexGravityMultiplier = 2f;
        [Tooltip("下落阶段重力倍率(>1 更快落地)")]
        public float jumpFallGravityMultiplier = 1.3f;

        [Title("下蹲")]
        public float standingCapsuleHeight = 2f;
        public float crouchedCapsuleHeight = 1f;
        [Tooltip("下蹲移动速度倍率")]
        public float crouchSpeedMultiplier = 0.5f;

        [Title("旋转")]
        public float orientationSharpness = 10f;

        [Title("重力")]
        public Vector3 gravity_ = new Vector3(0f, -9.81f, 0f);

        [Title("跳跃请求")]
        [LabelText("跳跃请求缓冲时长(秒)")]
        [Tooltip("跳跃请求超过该时长仍未在地面被消费，则自动过期，避免落地后二次起跳。")]
        public float jumpRequestBufferTime = 0.12f;

        [Title("根运动")]
        public bool useRootMotion = true;
        public float rootMotionScale = 1f;
        public bool rootMotionGroundOnly = true;

        [Title("输入（世界空间）")]
        public Vector3 moveInput;
        public Vector3 lookInput = Vector3.forward;

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
        private bool _jumpRequested;
        private float _jumpRequestTime = -999f;
        private bool _crouchRequested;
        private bool _isCrouched;
        private bool _moveInputSetThisFrame;
        private bool _verticalInputSetThisFrame;
        private Vector3 _lastTransientPosition;

        [NonSerialized]
        public EntityBasicFlyModule flyModule;

        [NonSerialized]
        public EntityBasicSwimModule swimModule;

        [NonSerialized]
        public EntityBasicClimbModule climbModule;

        [NonSerialized]
        public EntityBasicMountModule mountModule;

        public void Initialize(Entity owner)
        {
            if (owner == null) return;
            if (motor == null)
            {
                motor = owner.GetComponent<KinematicCharacterMotor>();
            }
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
        }

        public void SetMoveInput(Vector3 input)
        {
            moveInput = Vector3.ClampMagnitude(input, 1f);
            _moveInputSetThisFrame = true;
        }

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
            _verticalInputSetThisFrame = true;
        }

        public void SetLookInput(Vector3 input)
        {
            if (input.sqrMagnitude > 0f)
            {
                lookInput = input.normalized;
            }
        }

        public void ResetInputs()
        {
            moveInput = Vector3.zero;
            lookInput = Vector3.forward;
            verticalInput = 0f;
            _moveInputSetThisFrame = false;
            _verticalInputSetThisFrame = false;
        }

        public void RequestJump()
        {
            _jumpRequested = true;
            _jumpRequestTime = Time.time;
        }

        public void SetCrouch(bool enable)
        {
            _crouchRequested = enable;
        }

        public void SetRootMotionVelocity(Vector3 velocity)
        {
            _rootMotionVelocity = velocity;
        }

        public void ClearRootMotionVelocity()
        {
            _rootMotionVelocity = Vector3.zero;
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
            // ★ 在 Simulate 开始前记录位置快照（而非 AfterCharacterUpdate 末尾）。
            //   这样 posDelta 只反映本次 KCC Simulate 内部产生的真实漂移；
            //   Update 里 SetPositionAndRotation 的外部位移在 PreSimulation 已写入
            //   TransientPosition，此时快照即可将其吸收，AfterCharacterUpdate 算出的
            //   delta 始终接近零，防漂逻辑天然不会误触发。
            if (motor != null)
                _lastTransientPosition = motor.TransientPosition;

            if (!_moveInputSetThisFrame)
            {
                moveInput = Vector3.zero;
            }
            if (!_verticalInputSetThisFrame)
            {
                verticalInput = 0f;
            }
            _moveInputSetThisFrame = false;
            _verticalInputSetThisFrame = false;
            ApplyCrouch();

            if (owner != null && owner.stateDomain != null && owner.stateDomain.stateMachine != null)
            {
                var supportFlags = owner.stateDomain.stateMachine.currentSupportFlags;
                if (supportFlags == StateSupportFlags.Flying && flyModule != null)
                {
                    flyModule.BeforeCharacterUpdate(owner, this, deltaTime);
                }
                if (supportFlags == StateSupportFlags.Swimming && swimModule != null)
                {
                    swimModule.BeforeCharacterUpdate(owner, this, deltaTime);
                }
                if (supportFlags == StateSupportFlags.Climbing && climbModule != null)
                {
                    climbModule.BeforeCharacterUpdate(owner, this, deltaTime);
                }
                if (mountModule != null && (supportFlags == StateSupportFlags.Mounted || mountModule.mountHold))
                {
                    // ★ mountHold 兜底：即便 Inspector 未配置 StateSupportFlags.Mounted，
                    //   骑乘期间仍确保 ForceUnground 被调用，防止 KCC 接地系统将角色压回地面，
                    //   对抗 MatchTarget 向上对齐的位移。
                    mountModule.BeforeCharacterUpdate(owner, this, deltaTime);
                }
            }
        }

        public void UpdateRotation(Entity owner, ref Quaternion currentRotation, float deltaTime)
        {
            if (owner != null && owner.stateDomain != null && owner.stateDomain.stateMachine != null)
            {
                var supportFlags = owner.stateDomain.stateMachine.currentSupportFlags;
                if (supportFlags == StateSupportFlags.Mounted)
                {
                    if (mountModule != null)
                    {
                        mountModule.UpdateRotation(owner, this, ref currentRotation, deltaTime);
                    }
                    return;
                }
                if (supportFlags == StateSupportFlags.Climbing)
                {
                    if (climbModule != null)
                    {
                        climbModule.UpdateRotation(owner, this, ref currentRotation, deltaTime);
                    }
                    return;
                }
                if (supportFlags == StateSupportFlags.Flying && flyModule != null)
                {
                    if (flyModule.UpdateRotation(owner, this, ref currentRotation, deltaTime))
                    {
                        return;
                    }
                }
                if (supportFlags == StateSupportFlags.Swimming && swimModule != null)
                {
                    if (swimModule.UpdateRotation(owner, this, ref currentRotation, deltaTime))
                    {
                        return;
                    }
                }
            }
            if (lookInput.sqrMagnitude <= 0f || orientationSharpness <= 0f) return;
            Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, lookInput, 1f - Mathf.Exp(-orientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
        }

        public void UpdateVelocity(Entity owner, ref Vector3 currentVelocity, float deltaTime)
        {
            float multiplier = Mathf.Max(0f, speedMultiplier);
            float stableMaxSpeed = maxStableMoveSpeed * multiplier;
            float airMaxSpeed = maxAirMoveSpeed * multiplier;
            if (_isCrouched)
            {
                stableMaxSpeed *= Mathf.Clamp01(crouchSpeedMultiplier);
            }
            if (speedLimit > 0f)
            {
                stableMaxSpeed = Mathf.Min(stableMaxSpeed, speedLimit);
                airMaxSpeed = Mathf.Min(airMaxSpeed, speedLimit);
            }

            Vector3 targetMovementVelocity = Vector3.zero;
            if (owner == null || owner.stateDomain == null || owner.stateDomain.stateMachine == null || motor == null)
            {
                _lastVelocity = currentVelocity;
                return;
            }
            var supportFlags = owner.stateDomain.stateMachine.currentSupportFlags;
            if (supportFlags == StateSupportFlags.Mounted || (mountModule != null && mountModule.mountHold))
            {
                // ★ mountHold 兜底：即便 Inspector 未配置 StateSupportFlags.Mounted，
                //   骑乘期间仍保证把速度归零，防止重力在 MatchTarget 窗口内持续积累，
                //   MatchTarget 结束时一次性释放导致角色飞回地面。
                if (mountModule != null)
                {
                    mountModule.UpdateVelocity(owner, this, ref currentVelocity, deltaTime);
                }
                else
                {
                    currentVelocity = Vector3.zero;
                }
                _lastVelocity = currentVelocity;
                return;
            }
            bool handled = false;
            if (supportFlags != StateSupportFlags.Climbing && climbModule != null && climbModule.subState == ClimbSubState.ClimbJump)
            {
                handled = climbModule.UpdateVelocity(owner, this, ref currentVelocity, deltaTime);
            }
            if (supportFlags == StateSupportFlags.Climbing)
            {
                handled = true;
                if (climbModule != null)
                {
                    climbModule.UpdateVelocity(owner, this, ref currentVelocity, deltaTime);
                }
                else
                {
                    currentVelocity = Vector3.zero;
                }
            }
            else if (supportFlags == StateSupportFlags.Flying)
            {
                handled = true;
                if (flyModule != null)
                {
                    flyModule.UpdateVelocity(owner, this, ref currentVelocity, deltaTime);
                }
            }
            else if (supportFlags == StateSupportFlags.Swimming)
            {
                handled = true;
                if (swimModule != null)
                {
                    swimModule.UpdateVelocity(owner, this, ref currentVelocity, deltaTime);
                }
            }
            if (!handled && motor.GroundingStatus.IsStableOnGround)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                }

                currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                Vector3 inputRight = Vector3.Cross(moveInput, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * moveInput.magnitude;
                targetMovementVelocity = reorientedInput * stableMaxSpeed;

                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-stableMovementSharpness * deltaTime));

                if (_jumpRequested)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    motor.ForceUnground(0.1f);
                    float finalJumpSpeed = jumpSpeed * Mathf.Max(0f, jumpSpeedMultiplier);
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp) + (motor.CharacterUp * finalJumpSpeed);
                }
            }
            else if (!handled)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                }

                if (moveInput.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = moveInput * airMaxSpeed;

                    if (motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), motor.CharacterUp).normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity_);
                    currentVelocity += velocityDiff * airAccelerationSpeed * deltaTime;
                }

                float gravityScale = 1f;
                float upVel = Vector3.Dot(currentVelocity, motor.CharacterUp);
                if (upVel > 0.01f)
                {
                    gravityScale = Mathf.Max(0f, jumpApexGravityMultiplier);
                }
                else if (upVel < -0.01f)
                {
                    gravityScale = Mathf.Max(0f, jumpFallGravityMultiplier);
                }
                currentVelocity += gravity_ * (gravityScale * deltaTime);
                currentVelocity *= (1f / (1f + (drag * deltaTime)));
            }

            if (useRootMotion)
            {
                bool canApply = !rootMotionGroundOnly || motor.GroundingStatus.IsStableOnGround;
                if (canApply)
                {
                    currentVelocity += _rootMotionVelocity * rootMotionScale;
                }
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
