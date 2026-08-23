using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("Item 运动模块")]
    public sealed class ItemMotionModule : ItemBasicModuleBase
    {
        [Title("驱动")]
        public ItemMotionDriverKind driverKind = ItemMotionDriverKind.Transform;
        [LabelText("刚体写回走 FixedUpdate")]
        public bool fixedUpdateForRigidbody = true;
        [MinValue(0f), LabelText("合并加速度上限")]
        public float maxCombinedInfluenceAcceleration = 80f;
        [MinValue(0f), LabelText("单步速度增量上限")]
        public float maxCombinedInfluenceVelocityDelta = 30f;
        [LabelText("Transform 运动启用碰撞 Sweep")]
        public bool sweepTransformInfluences = true;
        [MinValue(0.001f), LabelText("Transform Sweep 半径")]
        public float transformSweepRadius = 0.1f;
        [MinValue(0f), LabelText("Transform Sweep 皮肤宽度")]
        public float transformSweepSkin = 0.01f;
        [LabelText("Transform Sweep 碰撞层")]
        public LayerMask transformSweepMask = Physics.DefaultRaycastLayers;

        [Title("运行监控")]
        [ShowInInspector, ReadOnly] public Vector3 currentPosition;
        [ShowInInspector, ReadOnly] public Quaternion currentRotation;
        [ShowInInspector, ReadOnly] public Vector3 currentVelocity;

        [NonSerialized] private Rigidbody _rigidbody;
        [NonSerialized] private bool _hasPendingResult;
        [NonSerialized] private ShotMotionResult _pendingResult;
        [NonSerialized] private ESMotionInfluenceAccumulator _motionInfluences;
        [NonSerialized] private RaycastHit[] _transformSweepHits;

        private const int InitialTransformSweepCapacity = 8;
        private const int MaxTransformSweepCapacity = 64;

        public override void Start()
        {
            base.Start();
            CacheComponents();
            if (MyCore != null)
            {
                currentPosition = MyCore.transform.position;
                currentRotation = MyCore.transform.rotation;
            }
        }

        [ESHotPath]
        public void SetPendingShotResult(in ShotMotionResult result)
        {
            _pendingResult = result;
            _hasPendingResult = true;
        }

        public bool AddVelocity(
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None)
        {
            return TryAddVelocity(velocity, permissions) == ESMotionSubmitResult.Accepted;
        }

        public ESMotionSubmitResult TryAddVelocity(
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None)
        {
            if (!IsFinite(velocity))
                return ESMotionSubmitResult.InvalidValue;
            return EnsureMotionInfluences().TryAddVelocity(velocity)
                ? ESMotionSubmitResult.Accepted
                : ESMotionSubmitResult.InvalidValue;
        }

        public bool TryAcquireField(
            in ESMotionFieldRequest request,
            out ESMotionFieldLease lease)
        {
            return EnsureMotionInfluences().TryAcquireField(request, out lease);
        }

        [ESHotPath]
        public void ApplyMotionInfluences(
            ref Vector3 velocity,
            Vector3 position,
            float deltaTime)
        {
            if (_motionInfluences == null || !_motionInfluences.HasInfluences)
                return;

            _motionInfluences.Apply(
                ref velocity,
                position,
                deltaTime,
                ESMotionReceiverLockState.None,
                maxCombinedInfluenceAcceleration,
                maxCombinedInfluenceVelocityDelta);
        }

        [ESHotPath]
        public bool TryReadDynamicVelocity(out Vector3 velocity)
        {
            Rigidbody body = driverKind == ItemMotionDriverKind.Rigidbody
                ? ResolveRigidbody()
                : null;
            if (body != null && !body.isKinematic && !_hasPendingResult)
            {
                velocity = body.velocity;
                currentVelocity = velocity;
                return true;
            }

            velocity = default;
            return false;
        }

        public void ResetMotionInfluences()
        {
            _motionInfluences?.Reset();
            _hasPendingResult = false;
        }

        protected override void Update()
        {
            if (ShouldApplyInFixedUpdate())
                return;

            ApplyPendingResult();
        }

        public override void FixedUpdateExpand()
        {
            if (!ShouldApplyInFixedUpdate())
                return;

            ApplyPendingResult();
        }

        private void ApplyPendingResult()
        {
            if (MyCore == null)
                return;

            if (!_hasPendingResult)
            {
                ApplyStandaloneInfluences();
                return;
            }

            _hasPendingResult = false;
            currentPosition = _pendingResult.currentPosition;
            currentRotation = _pendingResult.currentRotation;
            currentVelocity = _pendingResult.velocity;

            if (driverKind == ItemMotionDriverKind.Rigidbody && ResolveRigidbody() != null)
            {
                _rigidbody.MoveRotation(currentRotation);
                if (_rigidbody.isKinematic)
                    _rigidbody.MovePosition(currentPosition);
                else
                    _rigidbody.velocity = currentVelocity;
            }
            else
            {
                MyCore.transform.SetPositionAndRotation(currentPosition, currentRotation);
            }
        }

        private void ApplyStandaloneInfluences()
        {
            if (_motionInfluences == null || !_motionInfluences.HasInfluences)
                return;

            float deltaTime = ShouldApplyInFixedUpdate() ? Time.fixedDeltaTime : Time.deltaTime;
            Rigidbody body = driverKind == ItemMotionDriverKind.Rigidbody ? ResolveRigidbody() : null;
            Vector3 velocity = body != null && !body.isKinematic ? body.velocity : currentVelocity;
            Vector3 position = body != null ? body.position : MyCore.transform.position;
            _motionInfluences.Apply(
                ref velocity,
                position,
                deltaTime,
                ESMotionReceiverLockState.None,
                maxCombinedInfluenceAcceleration,
                maxCombinedInfluenceVelocityDelta);
            currentVelocity = velocity;
            if (body != null && !body.isKinematic)
                body.velocity = velocity;
            else
            {
                currentPosition = ResolveTransformInfluencePosition(
                    position,
                    ref velocity,
                    deltaTime);
                MyCore.transform.position = currentPosition;
            }
        }

        private Vector3 ResolveTransformInfluencePosition(
            Vector3 position,
            ref Vector3 velocity,
            float deltaTime)
        {
            Vector3 displacement = velocity * deltaTime;
            float distance = displacement.magnitude;
            if (!sweepTransformInfluences || distance <= Mathf.Epsilon)
                return position + displacement;

            Vector3 direction = displacement / distance;
            RaycastHit[] sweepHits = EnsureTransformSweepBuffer();
            int hitCount;
            while (true)
            {
                hitCount = Physics.SphereCastNonAlloc(
                    position,
                    Mathf.Max(0.001f, transformSweepRadius),
                    direction,
                    sweepHits,
                    distance,
                    transformSweepMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < sweepHits.Length
                    || sweepHits.Length >= MaxTransformSweepCapacity)
                    break;

                int nextCapacity = Mathf.Min(MaxTransformSweepCapacity, sweepHits.Length * 2);
                Array.Resize(ref _transformSweepHits, nextCapacity);
                sweepHits = _transformSweepHits;
            }

            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestNormal = Vector3.zero;
            Transform ownerTransform = MyCore.transform;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sweepHits[i];
                Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
                if (hitTransform == null || hitTransform == ownerTransform
                    || hitTransform.IsChildOf(ownerTransform))
                    continue;
                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestNormal = hit.normal;
            }

            if (float.IsPositiveInfinity(nearestDistance))
                return position + displacement;

            float inwardSpeed = Vector3.Dot(velocity, nearestNormal);
            if (inwardSpeed < 0f)
                velocity -= nearestNormal * inwardSpeed;
            float travel = Mathf.Max(0f, nearestDistance - Mathf.Max(0f, transformSweepSkin));
            return position + direction * travel;
        }

        private RaycastHit[] EnsureTransformSweepBuffer()
        {
            return _transformSweepHits ??= new RaycastHit[InitialTransformSweepCapacity];
        }

        private ESMotionInfluenceAccumulator EnsureMotionInfluences()
        {
            return _motionInfluences ??= new ESMotionInfluenceAccumulator();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private bool ShouldApplyInFixedUpdate()
        {
            return driverKind == ItemMotionDriverKind.Rigidbody && fixedUpdateForRigidbody;
        }

        private Rigidbody ResolveRigidbody()
        {
            if (_rigidbody == null && MyCore != null)
                _rigidbody = MyCore.GetComponent<Rigidbody>();
            return _rigidbody;
        }

        private void CacheComponents()
        {
            ResolveRigidbody();
        }
    }

    [Serializable, TypeRegistryItem("Item 飞行物模块")]
    public sealed class ItemShotModule : ItemBasicModuleBase
    {
        private const int MaximumHitBufferCapacity = 256;
        private const int DefaultResolvedColliderCapacity = 32;
        private const int MaximumResolvedColliderCapacity = 1024;
        private const int MaximumImpactColliderCapacity = 128;

        [Title("飞行物Shared")]
        [HideLabel]
        public ItemShotSharedData sharedData = ItemShotSharedData.Default;

        [Title("飞行物Variable")]
        [HideLabel]
        public ItemShotVariableData variableData = ItemShotVariableData.Default;

        [Title("飞行物配置")]
        [LabelText("瞄准模式")]
        public ShotAimMode aimMode = ShotAimMode.Free;
        [LabelText("阻挡模式")]
        public ShotBlockMode blockMode = ShotBlockMode.AnyBlocker;
        public ShotMotionConfig config = ShotMotionConfig.Straight(30f, 5f);
        [LabelText("命中层")]
        public LayerMask hitLayers = ESPhysicsLayers.ShotHitMask;
        [LabelText("命中半径")]
        public float castRadius = 0.05f;
        [LabelText("命中缓存容量")]
        [MinValue(1), MaxValue(MaximumHitBufferCapacity)]
        public int hitBufferCapacity = 8;
        [LabelText("单次生命周期去重容量")]
        [MinValue(1), MaxValue(MaximumResolvedColliderCapacity)]
        public int resolvedColliderCapacity = DefaultResolvedColliderCapacity;

        [Title("运行监控")]
        [ShowInInspector, ReadOnly] public ShotMotionState state;
        [ShowInInspector, ReadOnly] public ShotMotionResult latestResult;
        [LabelText("命中缓存溢出次数")]
        [ShowInInspector, ReadOnly] public int hitOverflowCount;
        [LabelText("命中查询饱和停止次数")]
        [ShowInInspector, ReadOnly] public int hitOverflowStopCount;
        [LabelText("命中去重容量停止次数")]
        [ShowInInspector, ReadOnly] public int resolvedColliderOverflowCount;
        [LabelText("范围冲击容量饱和次数")]
        [ShowInInspector, ReadOnly] public int impactOverflowCount;

        [NonSerialized] private ShotHitCandidate[] _hitResults;
        [NonSerialized] private IItemShotHitSolver _hitSolver;
        [NonSerialized] private IItemShotTickPolicy _tickPolicy;
        [NonSerialized] private ItemMotionModule _motionModule;
        [NonSerialized] private Transform _targetTransform;
        [NonSerialized] private Transform _targetEntityTransform;
        [NonSerialized] private Collider _targetCollider;
        [NonSerialized] private Vector3 _externalMotionVelocity;
        [NonSerialized] private float _pendingTickDeltaTime;
        [NonSerialized] private bool _hasSubmittedMotionResult;
        [NonSerialized] private int _runtimeDefinitionKey;
        [NonSerialized] private ESInstanceHandle _runtimeInstanceHandle;
        [NonSerialized] private ESShotLaunchContext _launchContext;
        [NonSerialized] private int[] _resolvedColliderIds;
        [NonSerialized] private int _resolvedColliderCount;
        [NonSerialized] private int _resolvedColliderMask;
        [NonSerialized] private int _runtimeHitCapacity;
        [NonSerialized] private int _runtimeResolvedColliderCapacity;
        [NonSerialized] private bool _motionModuleResolved;
        [NonSerialized] private ESPooledGameObject _pooledObject;
        [NonSerialized] private bool _targetHitResolved;
        [NonSerialized] private bool _lifecycleActive;
        [NonSerialized] private ESAssetIdentity _prefabIdentity;
        [NonSerialized] private System.Action<ESAssetIdentity> _resourceTransitionObserver;
        [NonSerialized] private bool _resourceTransitionSubscribed;
        [NonSerialized] private int _simulationIndex = -1;
        [NonSerialized] private Collider[] _impactColliders;
        [NonSerialized] private int _bounceCount;
        [NonSerialized] private System.Action<ESShotLifecycleEvent> _lifecycleEvent;
        [NonSerialized] private System.Delegate[] _lifecycleEventSnapshot =
            System.Array.Empty<System.Delegate>();

        public ESInstanceHandle RuntimeInstanceHandle => _runtimeInstanceHandle;
        internal int Internal_SimulationIndex
        {
            get => _simulationIndex;
            set => _simulationIndex = value;
        }
        public ESShotLaunchContext LaunchContext => _launchContext;
        public event System.Action<ESShotLifecycleEvent> LifecycleEvent
        {
            add
            {
                if (value == null)
                    return;
                _lifecycleEvent += value;
                _lifecycleEventSnapshot = _lifecycleEvent.GetInvocationList();
            }
            remove
            {
                if (value == null || _lifecycleEvent == null)
                    return;

                System.Action<ESShotLifecycleEvent> before = _lifecycleEvent;
                System.Action<ESShotLifecycleEvent> after =
                    (System.Action<ESShotLifecycleEvent>)System.Delegate.Remove(before, value);
                if (ReferenceEquals(before, after))
                    return;

                _lifecycleEvent = after;
                _lifecycleEventSnapshot = after != null
                    ? after.GetInvocationList()
                    : System.Array.Empty<System.Delegate>();
            }
        }

        public override void Start()
        {
            base.Start();
            Internal_PrepareHotPath();
            ResolveMotionModule();
            // A first-time pooled instance reaches Start after ESShotSpawner has already
            // applied RuntimeData and launched it. The prefab defaults must not overwrite
            // that authoritative per-spawn state.
            if (_lifecycleActive || state.launched)
                return;
            sharedData ??= ItemShotSharedData.Default;
            ApplyShotData(sharedData, variableData);
            ResolveRuntimeDefinitionKey(MyCore != null ? MyCore.prefabDefinition : null);
        }

        public bool Launch(Vector3 direction)
        {
            if (MyCore == null || state.launched || _lifecycleActive)
                return false;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : MyCore.transform.forward;
            dir = ApplySpread(dir);
            _targetTransform = null;
            _targetEntityTransform = null;
            _targetCollider = null;
            _targetHitResolved = false;
            _bounceCount = 0;
            _externalMotionVelocity = Vector3.zero;
            _pendingTickDeltaTime = 0f;
            _hasSubmittedMotionResult = false;
            state = new ShotMotionState
            {
                previousPosition = MyCore.transform.position,
                currentPosition = MyCore.transform.position,
                currentRotation = MyCore.transform.rotation,
                velocity = dir * Mathf.Max(0f, config.speed),
                direction = dir,
                targetPosition = Vector3.zero,
                elapsedTime = 0f,
                hasTarget = false,
                launched = true
            };
            if (!TryRegisterRuntimeInstance())
            {
                state.launched = false;
                return false;
            }

            _lifecycleActive = true;
            PublishLifecycle(ESShotLifecycleKind.Launched);
            return true;
        }

        public bool LaunchTo(Vector3 targetPosition)
        {
            if (MyCore == null || state.launched || _lifecycleActive)
                return false;

            _targetTransform = null;
            _targetEntityTransform = null;
            _targetCollider = null;
            _targetHitResolved = false;
            _bounceCount = 0;
            _externalMotionVelocity = Vector3.zero;
            _pendingTickDeltaTime = 0f;
            _hasSubmittedMotionResult = false;
            Vector3 resolvedTargetPosition = targetPosition + variableData.targetOffset;
            Vector3 toTarget = resolvedTargetPosition - MyCore.transform.position;
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : MyCore.transform.forward;
            dir = ApplySpread(dir);
            state = new ShotMotionState
            {
                previousPosition = MyCore.transform.position,
                currentPosition = MyCore.transform.position,
                currentRotation = MyCore.transform.rotation,
                velocity = dir * Mathf.Max(0f, config.speed),
                direction = dir,
                targetPosition = resolvedTargetPosition,
                elapsedTime = 0f,
                hasTarget = true,
                launched = true
            };
            if (!TryRegisterRuntimeInstance())
            {
                state.launched = false;
                return false;
            }

            _lifecycleActive = true;
            PublishLifecycle(ESShotLifecycleKind.Launched);
            return true;
        }

        public bool LaunchTo(Transform target)
        {
            return LaunchTo(target, aimMode == ShotAimMode.MustHit);
        }

        public bool LaunchTo(Transform target, bool mustHit)
        {
            if (target == null || state.launched || _lifecycleActive)
                return false;

            _targetTransform = target;
            if (mustHit && sharedData.allowMustHit)
                aimMode = ShotAimMode.MustHit;
            else if (aimMode == ShotAimMode.Free)
                aimMode = ShotAimMode.Target;

            bool launched = LaunchTo(target.position);
            _targetTransform = target;
            Entity targetEntity = target.GetComponentInParent<Entity>();
            _targetEntityTransform = targetEntity != null ? targetEntity.transform : null;
            _targetCollider = target.GetComponentInChildren<Collider>();
            return launched;
        }

        public void Internal_InitializeSpawn(
            ESShotRuntimeData runtimeData,
            int runtimeDefinitionKey,
            in ItemShotVariableData spawnVariableData,
            in ESShotLaunchContext context)
        {
            if (runtimeData == null || !runtimeData.Ready || runtimeData.PreparedSharedData == null)
                throw new System.ArgumentNullException(nameof(runtimeData));
            if (runtimeDefinitionKey <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(runtimeDefinitionKey));
            ItemShotSharedData preparedDefinition = runtimeData.PreparedSharedData;
            RequireHitTagEligibilityPrepared(preparedDefinition);
            ApplyPreparedShotData(preparedDefinition, spawnVariableData);
            _runtimeDefinitionKey = runtimeDefinitionKey;
            _launchContext = context;
            _prefabIdentity = runtimeData.TryGetPreparedPrefabIdentity(out ESAssetIdentity prefabIdentity)
                ? prefabIdentity
                : default;
            Internal_SubscribeToResourceTransitions();
            Internal_PrepareHotPath();
            ResetResolvedColliders();
            _targetEntityTransform = null;
            _targetCollider = null;
            _targetHitResolved = false;
            _bounceCount = 0;
            _pendingTickDeltaTime = 0f;
        }

        public void Internal_Stop(bool publishStopped = true)
        {
            if (state.launched)
            {
                state.launched = false;
                UnregisterRuntimeInstance();
                if (publishStopped)
                    PublishLifecycle(ESShotLifecycleKind.Stopped);
            }
            RequestPoolReturn();
        }

        public void ApplyShotData(ItemShotSharedData shared, in ItemShotVariableData variable)
        {
            ValidateShotDataForApply(shared, variable);
            ApplyPreparedShotData(shared, variable);
        }

        private void ApplyPreparedShotData(
            ItemShotSharedData shared,
            in ItemShotVariableData variable)
        {
            if (shared.hitTagEligibility != null && !shared.hitTagEligibility.IsPrepared)
            {
                throw new System.InvalidOperationException(
                    "ShotDefinition 的命中 Tag 条件尚未冻结，拒绝提交运行态。");
            }

            ItemShotVariableData normalizedVariable = NormalizeVariable(variable);
            ShotAimMode resolvedAimMode = shared.aimMode;
            if (normalizedVariable.forceMustHit && shared.allowMustHit)
                resolvedAimMode = ShotAimMode.MustHit;
            ShotBlockMode resolvedBlockMode = shared.blockMode;
            LayerMask resolvedHitLayers = ESPhysicsLayers.GetShotHitMask(shared.hitLayers);
            float resolvedCastRadius = Mathf.Max(0f, shared.radius * normalizedVariable.radiusMultiplier);
            ShotMotionConfig resolvedConfig = shared.ToShotMotionConfig(normalizedVariable);

            sharedData = shared;
            variableData = normalizedVariable;
            aimMode = resolvedAimMode;
            blockMode = resolvedBlockMode;
            hitLayers = resolvedHitLayers;
            castRadius = resolvedCastRadius;
            config = resolvedConfig;
        }

        public void ApplyShotData(ItemDataInfo itemData)
        {
            if (itemData == null)
                return;

            itemData.EnsureActiveKindData();
            ItemShotDataBlock block = itemData.kindData as ItemShotDataBlock;
            if (block != null)
            {
                ValidateShotDataForApply(block.sharedData, block.initialState);
                MyCore?.BindDefinition(itemData);
                ApplyPreparedShotData(block.sharedData, block.initialState);
                ResolveRuntimeDefinitionKey(itemData);
            }
        }

        [ESHotPath]
        internal void Internal_TickCentralized(float deltaTime)
        {
            Tick(deltaTime);
        }

        public void SetHitSolver(IItemShotHitSolver solver)
        {
            _hitSolver = solver;
            if (_hitSolver == null)
                Internal_PrepareHotPath();
        }

        public void SetTickPolicy(IItemShotTickPolicy policy)
        {
            _tickPolicy = policy;
            if (_tickPolicy == null)
                _tickPolicy = ItemShotAlwaysTickPolicy.Instance;
        }

        [ESHotPath]
        private void Tick(float deltaTime)
        {
            if (!state.launched)
                return;

            _pendingTickDeltaTime += Mathf.Max(0f, deltaTime);
            if (!_tickPolicy.ShouldTick(state, Time.frameCount))
                return;

            float stepDeltaTime = _pendingTickDeltaTime;
            _pendingTickDeltaTime = 0f;

            if (aimMode == ShotAimMode.Scan)
            {
                TickScan(stepDeltaTime);
                return;
            }

            RefreshTargetPosition();
            ItemMotionModule motionModule = _motionModule;
            if (_hasSubmittedMotionResult
                && motionModule != null
                && motionModule.TryReadDynamicVelocity(out Vector3 bodyVelocity))
                _externalMotionVelocity = bodyVelocity - state.velocity;
            latestResult = ShotMotionSolver.Step(ref state, config, stepDeltaTime);
            ApplyExternalMotion(ref state, ref latestResult, motionModule, stepDeltaTime);
            TryBuildHitCandidate(ref latestResult);
            if (!state.launched)
                return;
            TryBuildMustHitCandidate(ref latestResult);
            if (latestResult.hasHitCandidate && !ResolveHit(latestResult.hitCandidate, ref latestResult))
                return;

            motionModule?.SetPendingShotResult(latestResult);
            _hasSubmittedMotionResult = motionModule != null;

            if (latestResult.kind == ShotMotionKind.Arrived || latestResult.kind == ShotMotionKind.Expired)
            {
                state.launched = false;
                UnregisterRuntimeInstance();
                PublishLifecycle(latestResult.kind == ShotMotionKind.Arrived
                    ? ESShotLifecycleKind.Arrived
                    : ESShotLifecycleKind.Expired);
                RequestPoolReturn();
            }
        }

        public void OnPoolSpawned()
        {
            Internal_PrepareHotPath();
            UnregisterRuntimeInstance();
            Internal_ResetOverflowDiagnostics();
            _launchContext = default;
            ResetResolvedColliders();
            _pendingTickDeltaTime = 0f;
            _targetEntityTransform = null;
            _targetCollider = null;
            _targetHitResolved = false;
            _lifecycleActive = false;
            ResolveRuntimeDefinitionKey(MyCore != null ? MyCore.prefabDefinition : null);
        }

        internal void Internal_ResetOverflowDiagnostics()
        {
            hitOverflowCount = 0;
            hitOverflowStopCount = 0;
            resolvedColliderOverflowCount = 0;
            impactOverflowCount = 0;
        }

        public void OnPoolDespawned()
        {
            // The pool calls the initial Despawn baseline while prewarming, so default query
            // buffers are allocated before a projectile enters steady gameplay.
            Internal_PrepareHotPath();
            bool hadSpawn = _lifecycleActive;
            bool wasLaunched = hadSpawn && state.launched;
            UnregisterRuntimeInstance();
            if (wasLaunched)
            {
                state.launched = false;
                PublishLifecycle(ESShotLifecycleKind.Stopped);
            }
            state = default;
            latestResult = default;
            _targetTransform = null;
            _targetEntityTransform = null;
            _targetCollider = null;
            _externalMotionVelocity = Vector3.zero;
            _pendingTickDeltaTime = 0f;
            _hasSubmittedMotionResult = false;
            if (hadSpawn)
                PublishLifecycle(ESShotLifecycleKind.Despawned);
            _lifecycleActive = false;
            _launchContext = default;
            ResetResolvedColliders();
            _targetHitResolved = false;
            Internal_UnsubscribeFromResourceTransitions();
            _prefabIdentity = default;
            _lifecycleEvent = null;
            _lifecycleEventSnapshot = System.Array.Empty<System.Delegate>();
        }

        private void ResolveRuntimeDefinitionKey(ItemDataInfo itemData)
        {
            _runtimeDefinitionKey = 0;
            if (itemData == null || !(itemData.kindData is ItemShotDataBlock block) || block.key == null)
                return;
            ESRuntimeDataGameCore.Shots.TryGetRuntimeKey(block.key, out _runtimeDefinitionKey);
        }

        private bool TryRegisterRuntimeInstance()
        {
            UnregisterRuntimeInstance();
            if (MyCore == null)
                return false;
            if (_runtimeDefinitionKey <= 0)
                ResolveRuntimeDefinitionKey(MyCore.prefabDefinition);
            if (_runtimeDefinitionKey <= 0)
            {
                Debug.LogError("[ItemShotModule] Shot 定义尚未注入运行时表，已拒绝发射。", MyCore);
                return false;
            }
            if (ESRuntimeDataModule.ShotInstanceTable.TryAddInstance(
                MyCore,
                _runtimeDefinitionKey,
                MyCore.GetInstanceID(),
                out _runtimeInstanceHandle))
            {
                if (ESShotSimulationBatch.Internal_Register(this))
                    return true;

                ESRuntimeDataModule.ShotInstanceTable.TryRemove(_runtimeInstanceHandle, out _);
                _runtimeInstanceHandle = default;
                Debug.LogError("[ItemShotModule] 集中模拟容量不足，已拒绝发射。", MyCore);
                return false;
            }

            Debug.LogError("[ItemShotModule] Shot 实例表容量不足或身份无效，已拒绝发射。", MyCore);
            return false;
        }

        private void UnregisterRuntimeInstance()
        {
            ESShotSimulationBatch.Internal_Unregister(this);
            if (_runtimeInstanceHandle.IsValid)
                ESRuntimeDataModule.ShotInstanceTable.TryRemove(_runtimeInstanceHandle, out _);
            _runtimeInstanceHandle = default;
        }

        [ESHotPath]
        private void ApplyExternalMotion(
            ref ShotMotionState motionState,
            ref ShotMotionResult result,
            ItemMotionModule motionModule,
            float deltaTime)
        {
            Vector3 baseVelocity = result.velocity;
            Vector3 influencedVelocity = baseVelocity + _externalMotionVelocity;
            motionModule?.ApplyMotionInfluences(
                ref influencedVelocity,
                result.currentPosition,
                deltaTime);

            _externalMotionVelocity = influencedVelocity - baseVelocity;
            result.velocity = influencedVelocity;
            result.currentPosition += _externalMotionVelocity * Mathf.Max(0f, deltaTime);
            motionState.currentPosition = result.currentPosition;
            motionState.velocity = baseVelocity;
        }

        [ESHotPath]
        private void TryBuildHitCandidate(ref ShotMotionResult result)
        {
            if (result.kind == ShotMotionKind.Delayed || result.kind == ShotMotionKind.Warmup)
                return;

            ItemShotHitQuery query = new ItemShotHitQuery
            {
                from = result.previousPosition,
                to = result.currentPosition,
                radius = castRadius,
                hitLayers = hitLayers,
                triggerInteraction = QueryTriggerInteraction.Collide
            };

            int count = _hitSolver.Query(query, _hitResults, _hitResults.Length);

            if (count <= 0)
                return;

            bool queryOverflow = _hitSolver.IsOverflow;
            if (queryOverflow)
                hitOverflowCount++;

            ShotHitCandidate lastResolvedHit = default;
            bool resolvedAnyHit = false;
            for (int index = 0; index < count && state.launched; index++)
            {
                ShotHitCandidate hit = _hitResults[index];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;
                int colliderId = hit.collider.GetInstanceID();
                if (ContainsResolvedCollider(colliderId))
                    continue;
                if (ESShotColliderOwnerRegistry.TryResolveEntity(hit.collider, out Entity hitOwner)
                    && hitOwner != null
                    && ContainsResolvedCollider(hitOwner.GetInstanceID()))
                    continue;

                hit.incomingVelocity = result.velocity;
                lastResolvedHit = hit;
                resolvedAnyHit = true;
                result.hasHitCandidate = true;
                result.hitCandidate = hit;
                int bounceCountBeforeResolve = _bounceCount;
                if (!ResolveHit(hit, ref result))
                    return;
                result.hasHitCandidate = false;
                if (_bounceCount != bounceCountBeforeResolve)
                    return;
            }

            if (queryOverflow && state.launched)
            {
                hitOverflowStopCount++;
                StopAtHitBoundary(
                    resolvedAnyHit ? lastResolvedHit : default,
                    ref result);
            }
        }

        [ESHotPath]
        private void TryBuildMustHitCandidate(ref ShotMotionResult result)
        {
            if (aimMode != ShotAimMode.MustHit
                || _targetHitResolved
                || result.hasHitCandidate
                || result.kind != ShotMotionKind.Arrived)
                return;

            Collider targetCollider = _targetCollider;
            result.hasHitCandidate = true;
            result.hitCandidate = new ShotHitCandidate
            {
                collider = targetCollider,
                point = result.currentPosition,
                normal = result.velocity.sqrMagnitude > 0.0001f ? -result.velocity.normalized : Vector3.up,
                incomingVelocity = result.velocity,
                distance = 0f,
                layer = targetCollider != null ? targetCollider.gameObject.layer : 0,
                isTrigger = targetCollider != null && targetCollider.isTrigger
            };
        }

        [ESHotPath]
        private bool ResolveHit(in ShotHitCandidate hit, ref ShotMotionResult result)
        {
            IESShotHitResolver resolver = _launchContext.hitResolver ?? ESDefaultShotHitResolver.Instance;
            ESShotHitDecision decision = resolver.Resolve(_launchContext, sharedData, hit);
            if (IsTargetCollider(hit.collider))
                _targetHitResolved = true;

            if (decision != ESShotHitDecision.Stop
                && hit.collider != null
                && !TryAddResolvedCollider(hit.collider.GetInstanceID()))
            {
                resolvedColliderOverflowCount++;
                decision = ESShotHitDecision.Stop;
            }
            if (decision == ESShotHitDecision.Stop
                && TryApplyPreparedBounce(hit, ref result))
                decision = ESShotHitDecision.Bounce;
            PublishLifecycle(ESShotLifecycleKind.Hit, hit, decision);

            if (decision != ESShotHitDecision.Ignore)
                PublishPreparedImpactHits(hit);

            if (decision != ESShotHitDecision.Stop)
                return true;

            return StopAtHitBoundary(hit, ref result);
        }

        [ESHotPath]
        private bool TryApplyPreparedBounce(
            in ShotHitCandidate hit,
            ref ShotMotionResult result)
        {
            ShotImpactDefinitionData impact = sharedData != null ? sharedData.impact : null;
            if (impact == null
                || _bounceCount >= impact.bounceCount
                || hit.normal.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 incoming = state.velocity.sqrMagnitude > 0.0001f
                ? state.velocity
                : hit.incomingVelocity;
            if (incoming.sqrMagnitude <= 0.0001f)
                return false;

            if (hit.collider != null
                && !TryAddResolvedCollider(hit.collider.GetInstanceID()))
                return false;

            _bounceCount++;
            Vector3 reflected = Vector3.Reflect(incoming, hit.normal.normalized)
                * impact.bounceVelocityScale;
            Vector3 position = hit.point + hit.normal.normalized * 0.002f;
            state.previousPosition = position;
            state.currentPosition = position;
            state.velocity = reflected;
            state.direction = reflected.normalized;
            result.kind = ShotMotionKind.Moving;
            result.previousPosition = position;
            result.currentPosition = position;
            result.velocity = reflected;
            result.hasHitCandidate = false;
            return true;
        }

        [ESHotPath]
        private void PublishPreparedImpactHits(in ShotHitCandidate sourceHit)
        {
            ShotImpactDefinitionData impact = sharedData != null ? sharedData.impact : null;
            if (impact == null)
                return;

            if (sourceHit.collider != null
                && ESShotColliderOwnerRegistry.TryResolveEntity(sourceHit.collider, out Entity sourceTarget)
                && sourceTarget != null)
                TryAddResolvedCollider(sourceTarget.GetInstanceID());

            if (impact.explosionRadius > 0f)
            {
                PublishAreaHits(
                    sourceHit,
                    impact.explosionRadius,
                    impact.explosionTargetCapacity);
            }
            if (impact.chainTargetCount > 0 && impact.chainRadius > 0f)
                PublishChainHits(sourceHit, impact.chainRadius, impact.chainTargetCount);
        }

        [ESHotPath]
        private void PublishAreaHits(
            in ShotHitCandidate sourceHit,
            float radius,
            int targetLimit)
        {
            Collider[] colliders = _impactColliders;
            if (colliders == null || colliders.Length == 0 || targetLimit <= 0)
                return;

            int count = Physics.OverlapSphereNonAlloc(
                sourceHit.point,
                radius,
                colliders,
                hitLayers,
                QueryTriggerInteraction.Collide);
            if (count >= colliders.Length)
                impactOverflowCount++;
            int published = 0;
            while (published < targetLimit
                   && TrySelectNearestImpactTarget(
                       sourceHit.point,
                       sourceHit.collider,
                       colliders,
                       count,
                       out int selectedIndex,
                       out Collider collider,
                       out Entity target,
                       out Vector3 point))
            {
                colliders[selectedIndex] = null;
                int targetId = target.GetInstanceID();
                if (!TryAddResolvedCollider(targetId))
                {
                    resolvedColliderOverflowCount++;
                    break;
                }

                Vector3 normal = point - sourceHit.point;
                ShotHitCandidate areaHit = BuildImpactHit(
                    sourceHit,
                    collider,
                    point,
                    normal);
                PublishLifecycle(ESShotLifecycleKind.Hit, areaHit, ESShotHitDecision.Pierce);
                published++;
            }
        }

        [ESHotPath]
        private void PublishChainHits(
            in ShotHitCandidate sourceHit,
            float radius,
            int targetLimit)
        {
            Collider[] colliders = _impactColliders;
            if (colliders == null || colliders.Length == 0 || targetLimit <= 0)
                return;

            Vector3 chainOrigin = sourceHit.point;
            for (int hop = 0; hop < targetLimit; hop++)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    chainOrigin,
                    radius,
                    colliders,
                    hitLayers,
                    QueryTriggerInteraction.Collide);
                if (count >= colliders.Length)
                    impactOverflowCount++;
                if (!TrySelectNearestImpactTarget(
                        chainOrigin,
                        sourceHit.collider,
                        colliders,
                        count,
                        out _,
                        out Collider collider,
                        out Entity target,
                        out Vector3 point))
                    return;

                if (!TryAddResolvedCollider(target.GetInstanceID()))
                {
                    resolvedColliderOverflowCount++;
                    return;
                }

                Vector3 normal = point - chainOrigin;
                ShotHitCandidate chainHit = BuildImpactHit(
                    sourceHit,
                    collider,
                    point,
                    normal);
                PublishLifecycle(ESShotLifecycleKind.Hit, chainHit, ESShotHitDecision.Pierce);
                chainOrigin = point;
            }
        }

        [ESHotPath]
        private bool TrySelectNearestImpactTarget(
            Vector3 origin,
            Collider sourceCollider,
            Collider[] colliders,
            int count,
            out int selectedIndex,
            out Collider selectedCollider,
            out Entity selectedTarget,
            out Vector3 selectedPoint)
        {
            selectedIndex = -1;
            selectedCollider = null;
            selectedTarget = null;
            selectedPoint = default;
            float bestDistanceSquared = float.PositiveInfinity;
            int bestTieBreaker = int.MaxValue;

            for (int index = 0; index < count; index++)
            {
                Collider collider = colliders[index];
                if (collider == null
                    || collider == sourceCollider
                    || !ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity target)
                    || target == null
                    || target == _launchContext.owner
                    || ContainsResolvedCollider(target.GetInstanceID()))
                    continue;

                Vector3 point = collider.ClosestPoint(origin);
                float distanceSquared = (point - origin).sqrMagnitude;
                int tieBreaker = collider.GetInstanceID();
                if (distanceSquared > bestDistanceSquared
                    || (Mathf.Approximately(distanceSquared, bestDistanceSquared)
                        && tieBreaker >= bestTieBreaker))
                    continue;

                bestDistanceSquared = distanceSquared;
                bestTieBreaker = tieBreaker;
                selectedIndex = index;
                selectedCollider = collider;
                selectedTarget = target;
                selectedPoint = point;
            }

            return selectedIndex >= 0;
        }

        [ESHotPath]
        private static ShotHitCandidate BuildImpactHit(
            in ShotHitCandidate sourceHit,
            Collider collider,
            Vector3 point,
            Vector3 normal)
        {
            return new ShotHitCandidate
            {
                collider = collider,
                point = point,
                normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up,
                incomingVelocity = sourceHit.incomingVelocity,
                distance = normal.magnitude,
                layer = collider.gameObject.layer,
                isTrigger = collider.isTrigger
            };
        }

        [ESHotPath]
        private bool StopAtHitBoundary(
            in ShotHitCandidate hit,
            ref ShotMotionResult result)
        {
            result.kind = ShotMotionKind.Blocked;
            if (hit.collider != null)
                result.currentPosition = hit.point;
            state.currentPosition = result.currentPosition;
            state.launched = false;
            UnregisterRuntimeInstance();
            PublishLifecycle(ESShotLifecycleKind.Stopped, hit, ESShotHitDecision.Stop);
            RequestPoolReturn();
            return false;
        }

        [ESHotPath]
        private void TickScan(float deltaTime)
        {
            state.elapsedTime += Mathf.Max(0f, deltaTime);
            float launchDelay = Mathf.Max(0f, config.launchDelay);
            float warmupEnd = launchDelay + Mathf.Max(0f, config.warmupTime);
            if (state.elapsedTime < launchDelay)
            {
                SetScanWaitingResult(ShotMotionKind.Delayed);
                return;
            }
            if (state.elapsedTime < warmupEnd)
            {
                SetScanWaitingResult(ShotMotionKind.Warmup);
                return;
            }

            ExecuteScan(state.direction);
        }

        [ESHotPath]
        private void SetScanWaitingResult(ShotMotionKind kind)
        {
            state.previousPosition = state.currentPosition;
            latestResult = new ShotMotionResult
            {
                kind = kind,
                previousPosition = state.previousPosition,
                currentPosition = state.currentPosition,
                currentRotation = state.currentRotation,
                velocity = state.velocity,
                elapsedTime = state.elapsedTime,
                remainingDistance = 0f
            };
        }

        [ESHotPath]
        private void ExecuteScan(Vector3 direction)
        {
            float distance = Mathf.Max(0.01f, config.speed * Mathf.Max(0.01f, config.maxLifetime));
            latestResult = new ShotMotionResult
            {
                kind = ShotMotionKind.Moving,
                previousPosition = MyCore.transform.position,
                currentPosition = MyCore.transform.position + direction * distance,
                currentRotation = MyCore.transform.rotation,
                velocity = direction * Mathf.Max(0f, config.speed),
                elapsedTime = state.elapsedTime,
                remainingDistance = distance
            };
            state.currentPosition = latestResult.currentPosition;
            TryBuildHitCandidate(ref latestResult);
            if (!state.launched)
                return;

            state.launched = false;
            latestResult.kind = ShotMotionKind.Arrived;
            latestResult.remainingDistance = 0f;
            UnregisterRuntimeInstance();
            PublishLifecycle(ESShotLifecycleKind.Arrived);
            RequestPoolReturn();
        }

        private bool IsOwnCollider(Collider collider)
        {
            if (collider == null || MyCore == null)
                return false;
            Transform hitTransform = collider.transform;
            return hitTransform == MyCore.transform || hitTransform.IsChildOf(MyCore.transform);
        }

        private bool IsTargetCollider(Collider collider)
        {
            if (collider == null || _targetTransform == null)
                return false;

            Transform hitTransform = collider.transform;
            Transform targetRoot = _targetEntityTransform != null
                ? _targetEntityTransform
                : _targetTransform;
            return hitTransform == targetRoot
                || hitTransform.IsChildOf(targetRoot)
                || (_targetEntityTransform == null && _targetTransform.IsChildOf(hitTransform));
        }

        [ESHotPath]
        private void PublishLifecycle(
            ESShotLifecycleKind kind,
            in ShotHitCandidate hit = default,
            ESShotHitDecision decision = ESShotHitDecision.Ignore)
        {
            var evt = new ESShotLifecycleEvent(kind, this, _launchContext, latestResult, hit, decision);
            System.Delegate[] invocationList = _lifecycleEventSnapshot;
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((System.Action<ESShotLifecycleEvent>)invocationList[index]).Invoke(evt);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, MyCore);
                }
            }
            try { _launchContext.lifecycleObserver?.Invoke(evt); }
            catch (System.Exception exception) { Debug.LogException(exception, MyCore); }
        }

        [ESHotPath]
        private void RequestPoolReturn()
        {
            if (_pooledObject != null && _pooledObject.IsSpawned)
                _pooledObject.RequestPushToPool();
        }

        [ESHotPath]
        private void RefreshTargetPosition()
        {
            if (_targetTransform == null || !state.hasTarget)
                return;

            state.targetPosition = _targetTransform.position + variableData.targetOffset;
        }

        [ESHotPath]
        private Vector3 ApplySpread(Vector3 direction)
        {
            float spreadAngle = Mathf.Max(0f, variableData.spreadAngle);
            if (spreadAngle <= 0f || direction.sqrMagnitude <= 0.0001f)
                return direction;

            float yaw = RangeFromSeed(variableData.logicSeed, 0, -spreadAngle, spreadAngle);
            float pitch = RangeFromSeed(variableData.logicSeed, 1, -spreadAngle, spreadAngle);
            Quaternion basis = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return (basis * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward).normalized;
        }

        private static ItemShotVariableData NormalizeVariable(ItemShotVariableData variable)
        {
            if (variable.speedMultiplier <= 0f)
                variable.speedMultiplier = 1f;
            if (variable.lifeTimeMultiplier <= 0f)
                variable.lifeTimeMultiplier = 1f;
            if (variable.radiusMultiplier <= 0f)
                variable.radiusMultiplier = 1f;

            variable.launchDelay = Mathf.Max(0f, variable.launchDelay);
            variable.trackingStartTime = Mathf.Max(0f, variable.trackingStartTime);
            variable.spreadAngle = Mathf.Max(0f, variable.spreadAngle);
            return variable;
        }

        [ESHotPath]
        private static float RangeFromSeed(int seed, uint channel, float min, float max)
        {
            uint value = (uint)seed;
            value ^= 0x9E3779B9u + channel * 0x85EBCA6Bu;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            float t = (value & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(min, max, t);
        }

        private ItemMotionModule ResolveMotionModule()
        {
            if (_motionModuleResolved)
                return _motionModule;

            _motionModule = MyCore != null ? MyCore.GetMoudle<ItemMotionModule>() : null;
            _motionModuleResolved = true;
            return _motionModule;
        }

        [ESHotPath]
        private bool ContainsResolvedCollider(int colliderId)
        {
            int index = GetResolvedColliderSlot(colliderId);
            for (int probe = 0; probe < _resolvedColliderIds.Length; probe++)
            {
                int storedId = _resolvedColliderIds[index];
                if (storedId == 0)
                    return false;
                if (storedId == colliderId)
                    return true;
                index = (index + 1) & _resolvedColliderMask;
            }

            return false;
        }

        [ESHotPath]
        private bool TryAddResolvedCollider(int colliderId)
        {
            int index = GetResolvedColliderSlot(colliderId);
            for (int probe = 0; probe < _resolvedColliderIds.Length; probe++)
            {
                int storedId = _resolvedColliderIds[index];
                if (storedId == colliderId)
                    return true;
                if (storedId == 0)
                {
                    if (_resolvedColliderCount >= _runtimeResolvedColliderCapacity)
                        return false;

                    _resolvedColliderIds[index] = colliderId;
                    _resolvedColliderCount++;
                    return true;
                }
                index = (index + 1) & _resolvedColliderMask;
            }

            return false;
        }

        private int GetResolvedColliderSlot(int colliderId)
        {
            uint hash = unchecked((uint)colliderId * 2654435761u);
            return (int)(hash & (uint)_resolvedColliderMask);
        }

        private void ResetResolvedColliders()
        {
            if (_resolvedColliderIds != null && _resolvedColliderCount > 0)
                Array.Clear(_resolvedColliderIds, 0, _resolvedColliderIds.Length);
            _resolvedColliderCount = 0;
        }

        private void Internal_SubscribeToResourceTransitions()
        {
            if (_resourceTransitionSubscribed || !_prefabIdentity.IsValid)
                return;

            _resourceTransitionObserver ??= OnActivePlanAssetOwnershipEnding;
            ESAssets.ActivePlanAssetOwnershipEnding += _resourceTransitionObserver;
            _resourceTransitionSubscribed = true;
        }

        private void Internal_UnsubscribeFromResourceTransitions()
        {
            if (!_resourceTransitionSubscribed)
                return;

            ESAssets.ActivePlanAssetOwnershipEnding -= _resourceTransitionObserver;
            _resourceTransitionSubscribed = false;
        }

        private void OnActivePlanAssetOwnershipEnding(ESAssetIdentity identity)
        {
            if (state.launched && _prefabIdentity.Equals(identity))
                Internal_Stop();
        }

        private void Internal_PrepareHotPath()
        {
            int hitCapacity = Mathf.Clamp(hitBufferCapacity, 1, MaximumHitBufferCapacity);
            bool hitCapacityChanged = _runtimeHitCapacity != hitCapacity;
            if (_hitResults == null || _hitResults.Length != hitCapacity)
                _hitResults = new ShotHitCandidate[hitCapacity];

            int resolvedCapacity = Mathf.Clamp(
                resolvedColliderCapacity,
                1,
                MaximumResolvedColliderCapacity);
            if (_resolvedColliderIds == null
                || _runtimeResolvedColliderCapacity != resolvedCapacity)
            {
                int tableCapacity = Mathf.NextPowerOfTwo(resolvedCapacity * 2);
                _resolvedColliderIds = new int[tableCapacity];
                _resolvedColliderCount = 0;
                _resolvedColliderMask = tableCapacity - 1;
            }

            if (_hitSolver == null
                || (hitCapacityChanged && _hitSolver is ItemShotPhysicsHitSolver))
                _hitSolver = new ItemShotPhysicsHitSolver(hitCapacity);

            if (_tickPolicy == null)
                _tickPolicy = ItemShotAlwaysTickPolicy.Instance;

            ShotImpactDefinitionData impact = sharedData != null ? sharedData.impact : null;
            int impactCapacity = impact != null
                ? Mathf.Clamp(
                    Mathf.Max(impact.explosionTargetCapacity, impact.chainTargetCount),
                    1,
                    MaximumImpactColliderCapacity)
                : 1;
            if (_impactColliders == null || _impactColliders.Length != impactCapacity)
                _impactColliders = new Collider[impactCapacity];

            if (!_motionModuleResolved)
                ResolveMotionModule();
            if (_pooledObject == null && MyCore != null)
                _pooledObject = MyCore.GetComponent<ESPooledGameObject>();
            if (sharedData != null)
                PrepareHitTagEligibility(sharedData);

            _runtimeHitCapacity = hitCapacity;
            _runtimeResolvedColliderCapacity = resolvedCapacity;
        }

        private static void PrepareHitTagEligibility(ItemShotSharedData definition)
        {
            ESHitTagEligibility eligibility = definition.hitTagEligibility;
            if (eligibility == null || eligibility.IsPrepared)
                return;

            if (!eligibility.TryPrepare(out string error))
            {
                throw new System.InvalidOperationException(
                    "ShotDefinition 的命中 Tag 条件无法进入运行态：" + error);
            }
        }

        private static void RequireHitTagEligibilityPrepared(ItemShotSharedData definition)
        {
            ESHitTagEligibility eligibility = definition.hitTagEligibility;
            if (eligibility != null && !eligibility.IsPrepared)
            {
                throw new System.InvalidOperationException(
                    "Shot RuntimeData 的命中 Tag 条件尚未在注入边界冻结。");
            }
        }

        private static void ValidateShotDataForApply(
            ItemShotSharedData shared,
            in ItemShotVariableData variable)
        {
            if (shared == null)
                throw new System.ArgumentNullException(nameof(shared), "Shot SharedData 不能为空。");
            if (!shared.ValidateDefinition(out string sharedError))
            {
                throw new System.InvalidOperationException(
                    "Shot SharedData 无法提交运行态：" + sharedError);
            }
            if (!variable.ValidateDefinition(out string variableError))
            {
                throw new System.InvalidOperationException(
                    "Shot VariableData 无法提交运行态：" + variableError);
            }
            if (variable.forceMustHit && !shared.allowMustHit)
            {
                throw new System.InvalidOperationException(
                    "Shot VariableData 强制必中，但 SharedData 未允许必中。");
            }
        }
    }
}
