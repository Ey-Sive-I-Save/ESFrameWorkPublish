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
        public Animator animator;

        #region Domains

        [TabGroup("域", "基础域"), InlineProperty, HideLabel, SerializeReference]
        public EntityBasicDomain basicDomain;

        [TabGroup("域", "AI域"), InlineProperty, HideLabel, SerializeReference]
        public EntityAIDomain aiDomain;

        [TabGroup("域", "状态域"), InlineProperty, HideLabel, SerializeReference]
        public EntityStateDomain stateDomain;

        #endregion

        #region KCC

        [Title("KCC（不走模块，超高频）")]
        [InlineProperty, HideLabel]
        public EntityKCCData kcc = new EntityKCCData();

        #endregion

        #region Lifecycle

        protected override void OnBeforeAwakeRegister()
        {
            InitializeKCC();
        }

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            // 统一注册：只注册需要参与当前实体运行的域
            RegisterDomain(basicDomain);
            RegisterDomain(aiDomain);
            RegisterDomain(stateDomain);
        }

        #endregion

        #region 运行逻辑

        protected override void Update()
        {
            base.Update();
        }

        #endregion

        #region KCC API

        public void InitializeKCC()
        {
            kcc.Initialize(this);
        }

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
            stateDomain.stateMachine.SetLocomotionSupportFlags(flags);
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

        [Title("飞行")]
        public float flyMaxSpeed = 10f;
        public float flyAcceleration = 12f;
        public float flyDrag = 0.2f;

        [Title("游泳")]
        public float swimMaxSpeed = 6f;
        public float swimAcceleration = 8f;
        public float swimDrag = 0.5f;
        public float swimBuoyancy = 6f;

        [Title("速度倍率/限速")]
        public float speedMultiplier = 1f;
        [Tooltip("<=0 表示不限制")]
        public float speedLimit = 0f;

        [Title("跳跃")]
        public float jumpSpeed = 8f;

        [Title("下蹲")]
        public float standingCapsuleHeight = 2f;
        public float crouchedCapsuleHeight = 1f;

        [Title("旋转")]
        public float orientationSharpness = 10f;

        [Title("重力")]
        public Vector3 gravity = new Vector3(0f, -30f, 0f);

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
        [InlineProperty, HideLabel]
        public EntityKCCMonitor monitor = new EntityKCCMonitor();

        private Vector3 _lastVelocity;
        private Vector3 _rootMotionVelocity;
        private bool _jumpRequested;
        private bool _crouchRequested;
        private bool _isCrouched;

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
        }

        public void RequestJump()
        {
            _jumpRequested = true;
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
            ApplyCrouch();
        }

        public void UpdateRotation(Entity owner, ref Quaternion currentRotation, float deltaTime)
        {
            if (lookInput.sqrMagnitude <= 0f || orientationSharpness <= 0f) return;
            Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, lookInput, 1f - Mathf.Exp(-orientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
        }

        public void UpdateVelocity(Entity owner, ref Vector3 currentVelocity, float deltaTime)
        {
            float multiplier = Mathf.Max(0f, speedMultiplier);
            float stableMaxSpeed = maxStableMoveSpeed * multiplier;
            float airMaxSpeed = maxAirMoveSpeed * multiplier;
            if (speedLimit > 0f)
            {
                stableMaxSpeed = Mathf.Min(stableMaxSpeed, speedLimit);
                airMaxSpeed = Mathf.Min(airMaxSpeed, speedLimit);
            }

            Vector3 targetMovementVelocity = Vector3.zero;
            var supportFlags = owner.stateDomain.stateMachine.currentSupportFlags;
            if ((supportFlags & StateSupportFlags.Flying) != 0)
            {
                Vector3 up = motor.CharacterUp;
                Vector3 input = moveInput + up * verticalInput;
                input = Vector3.ClampMagnitude(input, 1f);
                targetMovementVelocity = input * flyMaxSpeed;

                Vector3 velocityDiff = targetMovementVelocity - currentVelocity;
                currentVelocity += velocityDiff * flyAcceleration * deltaTime;
                currentVelocity *= (1f / (1f + (flyDrag * deltaTime)));
            }
            else if ((supportFlags & StateSupportFlags.Swimming) != 0)
            {
                Vector3 up = motor.CharacterUp;
                Vector3 input = moveInput + up * verticalInput;
                input = Vector3.ClampMagnitude(input, 1f);
                targetMovementVelocity = input * swimMaxSpeed;

                Vector3 velocityDiff = targetMovementVelocity - currentVelocity;
                currentVelocity += velocityDiff * swimAcceleration * deltaTime;
                currentVelocity += up * swimBuoyancy * deltaTime;
                currentVelocity *= (1f / (1f + (swimDrag * deltaTime)));
            }
            else if (motor.GroundingStatus.IsStableOnGround)
            {
                currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                Vector3 inputRight = Vector3.Cross(moveInput, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * moveInput.magnitude;
                targetMovementVelocity = reorientedInput * stableMaxSpeed;

                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-stableMovementSharpness * deltaTime));

                if (_jumpRequested)
                {
                    _jumpRequested = false;
                    motor.ForceUnground(0.1f);
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp) + (motor.CharacterUp * jumpSpeed);
                }
            }
            else
            {
                if (moveInput.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = moveInput * airMaxSpeed;

                    if (motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), motor.CharacterUp).normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity);
                    currentVelocity += velocityDiff * airAccelerationSpeed * deltaTime;
                }

                currentVelocity += gravity * deltaTime;
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
