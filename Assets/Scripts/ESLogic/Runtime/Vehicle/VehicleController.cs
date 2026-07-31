using System;
using KinematicCharacterController;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum VehicleMotionBackend
    {
        [InspectorName("Rigidbody")] Rigidbody = 0,
        [InspectorName("Kinematic Character Motor")] KinematicCharacterMotor = 1,
    }

    /// <summary>
    /// 载具驾驶意图。输入源可以是骑手、AI 或网络；这里不保存输入源对象引用。
    /// </summary>
    [Serializable]
    public struct VehicleInputState
    {
        public Vector3 moveWorld;
        public Vector3 lookWorld;
        public int frameIndex;

        public void Set(Vector3 move, Vector3 look)
        {
            moveWorld = Vector3.ClampMagnitude(move, 1f);
            lookWorld = look.sqrMagnitude > 0.0001f ? look.normalized : Vector3.zero;
            frameIndex = Time.frameCount;
        }

        public void Clear()
        {
            moveWorld = Vector3.zero;
            lookWorld = Vector3.zero;
            frameIndex = -1;
        }
    }

    /// <summary>
    /// 载具运动阶段顺序。数值越小越先执行；它不表达具体车辆模式。
    /// </summary>
    [Serializable]
    public readonly struct VehicleMotionOrder
    {
        public readonly int before;
        public readonly int rotation;
        public readonly int velocity;
        public readonly int after;

        public VehicleMotionOrder(int before, int rotation, int velocity, int after)
        {
            this.before = before;
            this.rotation = rotation;
            this.velocity = velocity;
            this.after = after;
        }

        public static readonly VehicleMotionOrder Default = new VehicleMotionOrder(100, 100, 100, 100);
    }

    /// <summary>一个载具能力在四个运动阶段的注册句柄。</summary>
    [Serializable]
    public struct VehicleMotionRegistration
    {
        public ESWorkHandle beforeHandle;
        public ESWorkHandle rotationHandle;
        public ESWorkHandle velocityHandle;
        public ESWorkHandle afterHandle;

        public bool IsValid => beforeHandle.IsValid || rotationHandle.IsValid
            || velocityHandle.IsValid || afterHandle.IsValid;

        public void Clear()
        {
            beforeHandle = ESWorkHandle.Invalid;
            rotationHandle = ESWorkHandle.Invalid;
            velocityHandle = ESWorkHandle.Invalid;
            afterHandle = ESWorkHandle.Invalid;
        }
    }

    /// <summary>状态切换、座位同步等前置工作。不得直接写 Rigidbody、KCC 或 Transform。</summary>
    public interface IVehicleBeforeMotion
    {
        void BeforeVehicleMotion(VehicleController vehicle, float deltaTime);
    }

    /// <summary>
    /// 修改候选旋转。返回 true 表示已声明本阶段旋转权，后续载具旋转模块不再执行；
    /// 最终写回仍只由 VehicleController 完成。
    /// </summary>
    public interface IVehicleRotationMotion
    {
        bool UpdateVehicleRotation(
            VehicleController vehicle,
            Quaternion initialRotation,
            ref Quaternion currentRotation,
            float deltaTime);
    }

    /// <summary>
    /// 修改候选速度。返回 true 表示已声明本阶段速度权，后续载具速度模块不再执行；
    /// 最终写回仍只由 VehicleController 完成。
    /// </summary>
    public interface IVehicleVelocityMotion
    {
        bool UpdateVehicleVelocity(
            VehicleController vehicle,
            Vector3 initialVelocity,
            ref Vector3 currentVelocity,
            float deltaTime);
    }

    /// <summary>只读后置阶段，适合状态同步、音效和表现；不得驱动物理。</summary>
    public interface IVehicleAfterMotion
    {
        void AfterVehicleMotion(VehicleController vehicle, float deltaTime);
    }

    /// <summary>
    /// 载具的唯一运动权威。它可使用 Rigidbody 或 KinematicCharacterMotor，
    /// 并用独立的 ESWorkScheduler 阶段承载车辆能力，绝不复用 Entity 专用运动接口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour, ICharacterController
    {
        [Title("运动后端")]
        [LabelText("后端")]
        public VehicleMotionBackend motionBackend = VehicleMotionBackend.Rigidbody;

        [LabelText("自动定位后端组件")]
        public bool autoResolveBackend = true;

        [ShowIf(nameof(UsesRigidbody)), LabelText("刚体")]
        public Rigidbody physicsBody;

        [ShowIf(nameof(UsesKinematicMotor)), LabelText("Kinematic Motor")]
        public KinematicCharacterMotor kinematicMotor;

        [Title("基础驾驶")]
        [LabelText("最大速度")]
        [MinValue(0f)]
        public float maxMoveSpeed = 12f;

        [LabelText("速度响应")]
        [MinValue(0f)]
        public float acceleration = 20f;

        [LabelText("转向速度")]
        [MinValue(0f)]
        public float turnSpeed = 180f;

        [LabelText("KCC 重力")]
        public bool useKinematicGravity = true;

        [ShowIf(nameof(useKinematicGravity)), LabelText("KCC 重力向量")]
        public Vector3 kinematicGravity = new Vector3(0f, -9.81f, 0f);

        [Title("输入")]
        [ReadOnly, ShowInInspector, LabelText("驾驶意图")]
        public VehicleInputState InputState => inputState;

        [Title("调度诊断")]
        [ReadOnly, ShowInInspector, LabelText("前置任务数")]
        public int BeforeMotionCount => beforeScheduler != null ? beforeScheduler.Count : 0;

        [ReadOnly, ShowInInspector, LabelText("旋转任务数")]
        public int RotationMotionCount => rotationScheduler != null ? rotationScheduler.Count : 0;

        [ReadOnly, ShowInInspector, LabelText("速度任务数")]
        public int VelocityMotionCount => velocityScheduler != null ? velocityScheduler.Count : 0;

        [ReadOnly, ShowInInspector, LabelText("后置任务数")]
        public int AfterMotionCount => afterScheduler != null ? afterScheduler.Count : 0;

        [ReadOnly, ShowInInspector, LabelText("后端已就绪")]
        public bool IsReady => initialized;

        [NonSerialized] private VehicleInputState inputState;
        [NonSerialized] private ESWorkScheduler<IVehicleBeforeMotion> beforeScheduler;
        [NonSerialized] private ESWorkScheduler<IVehicleRotationMotion> rotationScheduler;
        [NonSerialized] private ESWorkScheduler<IVehicleVelocityMotion> velocityScheduler;
        [NonSerialized] private ESWorkScheduler<IVehicleAfterMotion> afterScheduler;
        [NonSerialized] private bool schedulersReady;
        [NonSerialized] private bool initialized;

        private bool UsesRigidbody => motionBackend == VehicleMotionBackend.Rigidbody;
        private bool UsesKinematicMotor => motionBackend == VehicleMotionBackend.KinematicCharacterMotor;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!initialized)
                Initialize();
        }

        private void OnDisable()
        {
            ClearDriverInput();
        }

        private void OnDestroy()
        {
            // KCC 不保存弱引用；销毁载具时清掉自己登记的控制器，避免 Motor 保留已销毁的 owner。
            if (kinematicMotor != null && kinematicMotor.CharacterController == this)
                kinematicMotor.CharacterController = null;
        }

        private void OnValidate()
        {
            maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            turnSpeed = Mathf.Max(0f, turnSpeed);

            if (!Application.isPlaying && autoResolveBackend)
                ResolveBackendReference();
        }

        /// <summary>初始化后端和固定容量调度器。初始化失败时不会静默回退到 Transform 移动。</summary>
        public bool Initialize()
        {
            ResolveBackendReference();
            if (!ValidateBackend(out string error))
            {
                initialized = false;
                Debug.LogError("[VehicleController] 初始化失败：" + error, this);
                return false;
            }

            EnsureSchedulers();
            if (UsesKinematicMotor)
                kinematicMotor.CharacterController = this;

            initialized = true;
            return true;
        }

        /// <summary>验证所选后端；工具和 Prefab 验证器应调用此公开 API。</summary>
        public bool ValidateBackend(out string error)
        {
            if (UsesRigidbody)
            {
                if (physicsBody == null)
                {
                    error = "Rigidbody 后端缺少 Rigidbody。";
                    return false;
                }
                if (physicsBody.isKinematic)
                {
                    error = "Rigidbody 后端要求非 Kinematic Rigidbody；Kinematic 载具请改用 KinematicCharacterMotor 后端。";
                    return false;
                }
            }
            else
            {
                if (kinematicMotor == null)
                {
                    error = "KinematicCharacterMotor 后端缺少 KinematicCharacterMotor。";
                    return false;
                }
                if (physicsBody != null && !physicsBody.isKinematic)
                {
                    error = "KinematicCharacterMotor 后端不能同时启用非 Kinematic Rigidbody。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 由当前驾驶者、AI 或网络控制权仲裁层写入本帧世界空间意图；不直接驱动物理。
        /// 同一载具每个物理步只能有一个已仲裁的输入来源，控制器不采用“最后写入者胜出”以外的来源策略。
        /// </summary>
        public void SetDriverInput(Vector3 moveWorld, Vector3 lookWorld)
        {
            inputState.Set(moveWorld, lookWorld);
        }

        /// <summary>驾驶者离座、失去控制权或输入被阻断时调用。</summary>
        public void ClearDriverInput()
        {
            inputState.Clear();
        }

        /// <summary>注册一项载具运动能力。能力可以参与一个或多个阶段。</summary>
        public VehicleMotionRegistration RegisterMotionFeature(object feature, VehicleMotionOrder order)
        {
            EnsureSchedulers();
            VehicleMotionRegistration registration = default;
            if (feature is IVehicleBeforeMotion before)
                registration.beforeHandle = beforeScheduler.Register(before, order.before);
            if (feature is IVehicleRotationMotion rotation)
                registration.rotationHandle = rotationScheduler.Register(rotation, order.rotation);
            if (feature is IVehicleVelocityMotion velocity)
                registration.velocityHandle = velocityScheduler.Register(velocity, order.velocity);
            if (feature is IVehicleAfterMotion after)
                registration.afterHandle = afterScheduler.Register(after, order.after);
            return registration;
        }

        /// <summary>注销一项载具能力的所有阶段注册；重复调用安全。</summary>
        public void UnregisterMotionFeature(ref VehicleMotionRegistration registration)
        {
            if (beforeScheduler != null && registration.beforeHandle.IsValid)
                beforeScheduler.Unregister(registration.beforeHandle);
            if (rotationScheduler != null && registration.rotationHandle.IsValid)
                rotationScheduler.Unregister(registration.rotationHandle);
            if (velocityScheduler != null && registration.velocityHandle.IsValid)
                velocityScheduler.Unregister(registration.velocityHandle);
            if (afterScheduler != null && registration.afterHandle.IsValid)
                afterScheduler.Unregister(registration.afterHandle);
            registration.Clear();
        }

        private void FixedUpdate()
        {
            if (!initialized || !UsesRigidbody)
                return;

            float deltaTime = Time.fixedDeltaTime;
            DispatchBeforeMotion(deltaTime);

            Quaternion rotation = physicsBody.rotation;
            if (!DispatchRotation(ref rotation, deltaTime))
                ApplyDefaultRotation(ref rotation, physicsBody.transform.up, deltaTime);

            Vector3 velocity = physicsBody.velocity;
            if (!DispatchVelocity(ref velocity, deltaTime))
                ApplyDefaultVelocity(ref velocity, physicsBody.transform.up, stableOnGround: true, deltaTime);

            physicsBody.MoveRotation(rotation);
            physicsBody.velocity = velocity;
            DispatchAfterMotion(deltaTime);
        }

        #region KinematicCharacterMotor callbacks

        public void BeforeCharacterUpdate(float deltaTime)
        {
            if (initialized && UsesKinematicMotor)
                DispatchBeforeMotion(deltaTime);
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (!initialized || !UsesKinematicMotor)
                return;

            if (!DispatchRotation(ref currentRotation, deltaTime))
                ApplyDefaultRotation(ref currentRotation, kinematicMotor.CharacterUp, deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!initialized || !UsesKinematicMotor)
                return;

            if (!DispatchVelocity(ref currentVelocity, deltaTime))
            {
                bool stableOnGround = kinematicMotor.GroundingStatus.IsStableOnGround;
                ApplyDefaultVelocity(ref currentVelocity, kinematicMotor.CharacterUp, stableOnGround, deltaTime);
                if (!stableOnGround && useKinematicGravity)
                    currentVelocity += kinematicGravity * deltaTime;
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            if (initialized && UsesKinematicMotor)
                DispatchAfterMotion(deltaTime);
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void ProcessHitStabilityReport(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        #endregion

        private void ResolveBackendReference()
        {
            if (!autoResolveBackend)
                return;

            if (UsesRigidbody && physicsBody == null)
                physicsBody = GetComponent<Rigidbody>();
            else if (UsesKinematicMotor && kinematicMotor == null)
                kinematicMotor = GetComponent<KinematicCharacterMotor>();
        }

        private void EnsureSchedulers()
        {
            if (schedulersReady)
                return;

            beforeScheduler ??= new ESWorkScheduler<IVehicleBeforeMotion>();
            rotationScheduler ??= new ESWorkScheduler<IVehicleRotationMotion>();
            velocityScheduler ??= new ESWorkScheduler<IVehicleVelocityMotion>();
            afterScheduler ??= new ESWorkScheduler<IVehicleAfterMotion>();
            beforeScheduler.Warmup(4, 2);
            rotationScheduler.Warmup(4, 2);
            velocityScheduler.Warmup(4, 2);
            afterScheduler.Warmup(4, 2);
            schedulersReady = true;
        }

        private void DispatchBeforeMotion(float deltaTime)
        {
            beforeScheduler.Reset();
            for (int i = 0; i < beforeScheduler.Count; i++)
                beforeScheduler.Get(i).BeforeVehicleMotion(this, deltaTime);
        }

        private bool DispatchRotation(ref Quaternion currentRotation, float deltaTime)
        {
            rotationScheduler.Reset();
            Quaternion initialRotation = currentRotation;
            for (int i = 0; i < rotationScheduler.Count; i++)
            {
                if (rotationScheduler.Get(i).UpdateVehicleRotation(this, initialRotation, ref currentRotation, deltaTime))
                    return true;
            }
            return false;
        }

        private bool DispatchVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            velocityScheduler.Reset();
            Vector3 initialVelocity = currentVelocity;
            for (int i = 0; i < velocityScheduler.Count; i++)
            {
                if (velocityScheduler.Get(i).UpdateVehicleVelocity(this, initialVelocity, ref currentVelocity, deltaTime))
                    return true;
            }
            return false;
        }

        private void DispatchAfterMotion(float deltaTime)
        {
            afterScheduler.Reset();
            for (int i = 0; i < afterScheduler.Count; i++)
                afterScheduler.Get(i).AfterVehicleMotion(this, deltaTime);
        }

        private void ApplyDefaultRotation(ref Quaternion currentRotation, Vector3 up, float deltaTime)
        {
            Vector3 desiredLook = Vector3.ProjectOnPlane(inputState.lookWorld, up);
            if (desiredLook.sqrMagnitude <= 0.0001f)
                desiredLook = Vector3.ProjectOnPlane(inputState.moveWorld, up);
            if (desiredLook.sqrMagnitude <= 0.0001f || turnSpeed <= 0f)
                return;

            Quaternion target = Quaternion.LookRotation(desiredLook.normalized, up);
            currentRotation = Quaternion.RotateTowards(currentRotation, target, turnSpeed * deltaTime);
        }

        private void ApplyDefaultVelocity(ref Vector3 currentVelocity, Vector3 up, bool stableOnGround, float deltaTime)
        {
            Vector3 move = Vector3.ProjectOnPlane(inputState.moveWorld, up);
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            Vector3 targetPlanarVelocity = move * maxMoveSpeed;
            Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
            float response = acceleration * deltaTime;
            if (stableOnGround)
                currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, targetPlanarVelocity, response);
            else
                currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, targetPlanarVelocity, response * 0.5f);

            Vector3 verticalVelocity = Vector3.Project(currentVelocity, up);
            currentVelocity = currentPlanarVelocity + verticalVelocity;
        }
    }
}
