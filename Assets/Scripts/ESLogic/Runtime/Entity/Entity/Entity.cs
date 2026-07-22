using System;
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

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("游戏标签")]
        private ESTagRefCountSet64 gameTags;

        [NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("LOD缓存索引")]
        private int lodCacheIndex = -1;

        public int LODCacheIndex => lodCacheIndex;

        #region Domains

        [TabGroup("生命体结构", "身体基础"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBasicDomain basicDomain = new EntityBasicDomain();

        [TabGroup("生命体结构", "意识AI"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityAIDomain aiDomain = new EntityAIDomain();

        [TabGroup("生命体结构", "Buff域"), HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBuffDomain buffDomain = new EntityBuffDomain();

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
            gameTags.Warmup();
            RegisterLODCache();
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
            base.OnDestroy();

            UnregisterLODCache();
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

        public void RegisterLODCache()
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule == null)
                return;

            lodCacheIndex = lodModule.RegisterEntity(this);
        }

        public void UnregisterLODCache()
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule == null)
            {
                lodCacheIndex = -1;
                return;
            }

            lodModule.UnregisterEntity(this);
            lodCacheIndex = -1;
        }

        public bool TryGetLODCache(out ESLODCacheEntry cache)
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule != null && !lodModule.IsValidCacheIndex(lodCacheIndex))
                lodCacheIndex = lodModule.RegisterEntity(this);

            if (lodModule != null && lodModule.IsValidCacheIndex(lodCacheIndex))
            {
                cache = lodModule.GetCacheReadOnly(lodCacheIndex);
                return true;
            }

            cache = default;
            return false;
        }

        public void SetEntityLODLevel(ESEntityLODLevel level)
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule == null)
                return;

            if (!lodModule.IsValidCacheIndex(lodCacheIndex))
                lodCacheIndex = lodModule.RegisterEntity(this);

            lodModule.SetEntityLevel(lodCacheIndex, level);
        }

        public void AddEntityLODGate(ESEntityLODGate gate)
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule == null)
                return;

            if (!lodModule.IsValidCacheIndex(lodCacheIndex))
                lodCacheIndex = lodModule.RegisterEntity(this);

            lodModule.AddEntityGate(lodCacheIndex, gate);
        }

        public void RemoveEntityLODGate(ESEntityLODGate gate)
        {
            ESLODModule lodModule = ESGameManager.LODModule;
            if (lodModule == null)
                return;

            if (!lodModule.IsValidCacheIndex(lodCacheIndex))
                lodCacheIndex = lodModule.RegisterEntity(this);

            lodModule.RemoveEntityGate(lodCacheIndex, gate);
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

        public bool HasWork => workSelf > 0 || workWorld > 0 || workOther > 0;

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
            lastKccJumpRequestFrame = Time.frameCount;
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
            _lastTransientPosition = motor.TransientPosition;

            if (!_moveInputSetThisFrame)
                moveInput = Vector3.zero;
            if (!_verticalInputSetThisFrame)
                verticalInput = 0f;

            _moveInputSetThisFrame = false;
            _verticalInputSetThisFrame = false;
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

        public void UpdateRotation(Entity owner, ref Quaternion currentRotation, float deltaTime)
        {
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

            if (lookInput.sqrMagnitude <= 0f || orientationSharpness <= 0f)
                return;

            Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, lookInput, 1f - Mathf.Exp(-orientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
        }

        public void UpdateVelocity(Entity owner, ref Vector3 currentVelocity, float deltaTime)
        {
            float multiplier = Mathf.Max(0f, speedMultiplier);
            float stableMaxSpeed = maxStableMoveSpeed * multiplier;
            float airMaxSpeed = maxAirMoveSpeed * multiplier;
            if (_isCrouched)
                stableMaxSpeed *= Mathf.Clamp01(crouchSpeedMultiplier);
            if (speedLimit > 0f)
            {
                stableMaxSpeed = Mathf.Min(stableMaxSpeed, speedLimit);
                airMaxSpeed = Mathf.Min(airMaxSpeed, speedLimit);
            }

            Vector3 targetMovementVelocity = Vector3.zero;
            bool handled = false;
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
                        StopWork();
                        _lastVelocity = currentVelocity;
                        return;
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

                Vector3 inputRight = Vector3.Cross(moveInput, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * moveInput.magnitude;
                targetMovementVelocity = reorientedInput * stableMaxSpeed;

                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-stableMovementSharpness * deltaTime));

                if (_jumpRequested)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    lastKccJumpApplyFrame = Time.frameCount;
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
                    lastKccJumpExpiredFrame = Time.frameCount;
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
                    gravityScale = Mathf.Max(0f, jumpApexGravityMultiplier);
                else if (upVel < -0.01f)
                    gravityScale = Mathf.Max(0f, jumpFallGravityMultiplier);

                currentVelocity += gravity_ * (gravityScale * deltaTime);
                currentVelocity *= (1f / (1f + (drag * deltaTime)));
            }

            if (useRootMotion)
            {
                bool canApply = !rootMotionGroundOnly || motor.GroundingStatus.IsStableOnGround;
                if (canApply)
                    currentVelocity += _rootMotionVelocity * rootMotionScale;
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

        public void RebuildMotionSchedulers()
        {
            _motionSchedulersReady = false;
        }

        private void EnsureMotionSchedulers()
        {
            if (_motionSchedulersReady)
                return;

            if (_beforeScheduler == null)
                _beforeScheduler = new ESWorkScheduler<IEntityKCCBeforeMotion>();
            else
                _beforeScheduler.Clear();
            _beforeScheduler.Warmup(4, 2);
            RegisterBefore(flyModule, 100);
            RegisterBefore(swimModule, 110);
            RegisterBefore(climbModule, 120);
            RegisterBefore(mountModule, 130);
            _beforeScheduler.Reset();

            if (_rotationScheduler == null)
                _rotationScheduler = new ESWorkScheduler<IEntityKCCRotationMotion>();
            else
                _rotationScheduler.Clear();
            _rotationScheduler.Warmup(4, 2);
            RegisterRotation(mountModule, 100);
            RegisterRotation(climbModule, 110);
            RegisterRotation(flyModule, 120);
            RegisterRotation(swimModule, 130);
            _rotationScheduler.Reset();

            if (_velocityScheduler == null)
                _velocityScheduler = new ESWorkScheduler<IEntityKCCVelocityMotion>();
            else
                _velocityScheduler.Clear();
            _velocityScheduler.Warmup(4, 2);
            RegisterVelocity(mountModule, 100);
            RegisterVelocity(climbModule, 110);
            RegisterVelocity(flyModule, 120);
            RegisterVelocity(swimModule, 130);
            _velocityScheduler.Reset();

            _motionSchedulersReady = true;
        }

        private void RegisterBefore(IEntityKCCBeforeMotion task, int order)
        {
            if (task != null)
                _beforeScheduler.Register(task, order);
        }

        private void RegisterRotation(IEntityKCCRotationMotion task, int order)
        {
            if (task != null)
                _rotationScheduler.Register(task, order);
        }

        private void RegisterVelocity(IEntityKCCVelocityMotion task, int order)
        {
            if (task != null)
                _velocityScheduler.Register(task, order);
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
