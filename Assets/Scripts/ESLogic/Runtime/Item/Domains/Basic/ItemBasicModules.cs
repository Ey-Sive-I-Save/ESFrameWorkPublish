using System;
using System.Collections.Generic;
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
        public int hitBufferCapacity = 8;

        [Title("运行监控")]
        [ShowInInspector, ReadOnly] public ShotMotionState state;
        [ShowInInspector, ReadOnly] public ShotMotionResult latestResult;
        [LabelText("命中缓存溢出次数")]
        [ShowInInspector, ReadOnly] public int hitOverflowCount;

        [NonSerialized] private ShotHitCandidate[] _hitResults;
        [NonSerialized] private IItemShotHitSolver _hitSolver;
        [NonSerialized] private IItemShotTickPolicy _tickPolicy;
        [NonSerialized] private ItemMotionModule _motionModule;
        [NonSerialized] private Transform _targetTransform;
        [NonSerialized] private Transform _targetEntityTransform;
        [NonSerialized] private Vector3 _externalMotionVelocity;
        [NonSerialized] private bool _hasSubmittedMotionResult;
        [NonSerialized] private int _runtimeDefinitionKey;
        [NonSerialized] private ESInstanceHandle _runtimeInstanceHandle;
        [NonSerialized] private ESShotLaunchContext _launchContext;
        [NonSerialized] private ESAssetConfigPayloadLease<GameObject> _prefabLease;
        [NonSerialized] private HashSet<int> _resolvedColliderIds;
        [NonSerialized] private bool _targetHitResolved;
        [NonSerialized] private bool _lifecycleActive;

        public ESInstanceHandle RuntimeInstanceHandle => _runtimeInstanceHandle;
        public ESShotLaunchContext LaunchContext => _launchContext;
        public event System.Action<ESShotLifecycleEvent> LifecycleEvent;

        public override void Start()
        {
            base.Start();
            EnsureRuntimeHelpers();
            ResolveMotionModule();
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
            _targetHitResolved = false;
            _externalMotionVelocity = Vector3.zero;
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
            _targetHitResolved = false;
            _externalMotionVelocity = Vector3.zero;
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
            return launched;
        }

        public void Internal_InitializeSpawn(
            ESShotRuntimeData runtimeData,
            int runtimeDefinitionKey,
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            in ESShotLaunchContext context)
        {
            if (runtimeData == null || runtimeData.sharedData == null)
                throw new System.ArgumentNullException(nameof(runtimeData));
            if (runtimeDefinitionKey <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(runtimeDefinitionKey));
            if (prefabLease == null || prefabLease.IsDisposed || prefabLease.Asset == null)
                throw new System.ArgumentException("Shot spawn requires a live prefab lease.", nameof(prefabLease));

            ReleasePrefabLease();
            MyCore?.BindDefinition(runtimeData.soSource);
            ApplyShotData(runtimeData.sharedData, runtimeData.defaultVariableData);
            _runtimeDefinitionKey = runtimeDefinitionKey;
            _launchContext = context;
            _prefabLease = prefabLease;
            (_resolvedColliderIds ??= new HashSet<int>()).Clear();
            _targetEntityTransform = null;
            _targetHitResolved = false;
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
            if (shared == null)
                throw new System.ArgumentNullException(nameof(shared), "Shot SharedData 不能为空。");
            if (!shared.enabled)
                return;

            sharedData = shared;
            variableData = NormalizeVariable(variable);

            aimMode = shared.aimMode;
            if (variableData.forceMustHit && shared.allowMustHit)
                aimMode = ShotAimMode.MustHit;

            blockMode = shared.blockMode;
            hitLayers = ESPhysicsLayers.GetShotHitMask(shared.hitLayers);
            castRadius = Mathf.Max(0f, shared.radius * variableData.radiusMultiplier);
            config = shared.ToShotMotionConfig(variableData);
        }

        public void ApplyShotData(ItemDataInfo itemData)
        {
            if (itemData == null)
                return;

            MyCore?.BindDefinition(itemData);
            itemData.EnsureActiveKindData();
            ItemShotDataBlock block = itemData.kindData as ItemShotDataBlock;
            if (block != null)
            {
                ApplyShotData(block.sharedData, block.initialState);
                ResolveRuntimeDefinitionKey(itemData);
            }
        }

        protected override void Update()
        {
            Tick(Time.deltaTime);
        }

        public void SetHitSolver(IItemShotHitSolver solver)
        {
            _hitSolver = solver;
        }

        public void SetTickPolicy(IItemShotTickPolicy policy)
        {
            _tickPolicy = policy;
        }

        private void Tick(float deltaTime)
        {
            EnsureRuntimeHelpers();
            if (!state.launched)
                return;

            if (!_tickPolicy.ShouldTick(state, Time.frameCount))
                return;

            if (aimMode == ShotAimMode.Scan)
            {
                TickScan(deltaTime);
                return;
            }

            RefreshTargetPosition();
            ItemMotionModule motionModule = ResolveMotionModule();
            if (_hasSubmittedMotionResult
                && motionModule != null
                && motionModule.TryReadDynamicVelocity(out Vector3 bodyVelocity))
                _externalMotionVelocity = bodyVelocity - state.velocity;
            latestResult = ShotMotionSolver.Step(ref state, config, deltaTime);
            ApplyExternalMotion(ref state, ref latestResult, motionModule, deltaTime);
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
            UnregisterRuntimeInstance();
            ReleasePrefabLease();
            _launchContext = default;
            _resolvedColliderIds?.Clear();
            _targetEntityTransform = null;
            _targetHitResolved = false;
            _lifecycleActive = false;
            ResolveRuntimeDefinitionKey(MyCore != null ? MyCore.prefabDefinition : null);
        }

        public void OnPoolDespawned()
        {
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
            _externalMotionVelocity = Vector3.zero;
            _hasSubmittedMotionResult = false;
            if (hadSpawn)
                PublishLifecycle(ESShotLifecycleKind.Despawned);
            _lifecycleActive = false;
            ReleasePrefabLease();
            _launchContext = default;
            _resolvedColliderIds?.Clear();
            _targetHitResolved = false;
            LifecycleEvent = null;
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
                return true;

            Debug.LogError("[ItemShotModule] Shot 实例表容量不足或身份无效，已拒绝发射。", MyCore);
            return false;
        }

        private void UnregisterRuntimeInstance()
        {
            if (_runtimeInstanceHandle.IsValid)
                ESRuntimeDataModule.ShotInstanceTable.TryRemove(_runtimeInstanceHandle, out _);
            _runtimeInstanceHandle = default;
        }

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

        private void TryBuildHitCandidate(ref ShotMotionResult result)
        {
            if (result.kind == ShotMotionKind.Delayed || result.kind == ShotMotionKind.Warmup)
                return;

            EnsureRuntimeHelpers();
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

            if (_hitSolver.IsOverflow)
                hitOverflowCount++;

            for (int index = 0; index < count && state.launched; index++)
            {
                ShotHitCandidate hit = _hitResults[index];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;
                int colliderId = hit.collider.GetInstanceID();
                if (_resolvedColliderIds != null && _resolvedColliderIds.Contains(colliderId))
                    continue;

                hit.incomingVelocity = result.velocity;
                result.hasHitCandidate = true;
                result.hitCandidate = hit;
                if (!ResolveHit(hit, ref result))
                    return;
                result.hasHitCandidate = false;
            }
        }

        private void TryBuildMustHitCandidate(ref ShotMotionResult result)
        {
            if (aimMode != ShotAimMode.MustHit
                || _targetHitResolved
                || result.hasHitCandidate
                || result.kind != ShotMotionKind.Arrived)
                return;

            Collider targetCollider = _targetTransform != null ? _targetTransform.GetComponentInChildren<Collider>() : null;
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

        private bool ResolveHit(in ShotHitCandidate hit, ref ShotMotionResult result)
        {
            IESShotHitResolver resolver = _launchContext.hitResolver ?? ESDefaultShotHitResolver.Instance;
            ESShotHitDecision decision = resolver.Resolve(_launchContext, sharedData, hit);
            if (IsTargetCollider(hit.collider))
                _targetHitResolved = true;
            PublishLifecycle(ESShotLifecycleKind.Hit, hit, decision);

            if (decision != ESShotHitDecision.Stop && hit.collider != null)
                (_resolvedColliderIds ??= new HashSet<int>()).Add(hit.collider.GetInstanceID());
            if (decision != ESShotHitDecision.Stop)
                return true;

            result.kind = ShotMotionKind.Blocked;
            result.currentPosition = hit.point;
            state.currentPosition = hit.point;
            state.launched = false;
            UnregisterRuntimeInstance();
            PublishLifecycle(ESShotLifecycleKind.Stopped, hit, decision);
            RequestPoolReturn();
            return false;
        }

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

        private void PublishLifecycle(
            ESShotLifecycleKind kind,
            in ShotHitCandidate hit = default,
            ESShotHitDecision decision = ESShotHitDecision.Ignore)
        {
            var evt = new ESShotLifecycleEvent(kind, this, _launchContext, latestResult, hit, decision);
            try { LifecycleEvent?.Invoke(evt); }
            catch (System.Exception exception) { Debug.LogException(exception, MyCore); }
            try { _launchContext.lifecycleObserver?.Invoke(evt); }
            catch (System.Exception exception) { Debug.LogException(exception, MyCore); }
        }

        private void RequestPoolReturn()
        {
            ESPooledGameObject pooled = MyCore != null ? MyCore.GetComponent<ESPooledGameObject>() : null;
            if (pooled != null && pooled.IsSpawned)
                pooled.RequestPushToPool();
        }

        private void ReleasePrefabLease()
        {
            ESAssetConfigPayloadLease<GameObject> ownedLease = _prefabLease;
            _prefabLease = null;
            ownedLease?.Dispose();
        }

        private void RefreshTargetPosition()
        {
            if (_targetTransform == null || !state.hasTarget)
                return;

            state.targetPosition = _targetTransform.position + variableData.targetOffset;
        }

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
            if (_motionModule != null)
                return _motionModule;

            _motionModule = MyCore != null ? MyCore.GetMoudle<ItemMotionModule>() : null;
            return _motionModule;
        }

        private void EnsureRuntimeHelpers()
        {
            int capacity = Mathf.Max(1, hitBufferCapacity);
            if (_hitResults == null || _hitResults.Length != capacity)
                _hitResults = new ShotHitCandidate[capacity];

            if (_hitSolver == null)
                _hitSolver = new ItemShotPhysicsHitSolver(capacity);

            if (_tickPolicy == null)
                _tickPolicy = new ItemShotAlwaysTickPolicy();
        }
    }
}
