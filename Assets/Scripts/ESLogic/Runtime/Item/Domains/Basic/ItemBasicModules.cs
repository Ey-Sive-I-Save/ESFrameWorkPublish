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
        private static readonly RaycastHit[] TransformSweepHits = new RaycastHit[8];

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
            EnsureMotionInfluences().AddVelocity(velocity, permissions);
            return ESMotionSubmitResult.Accepted;
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
            int hitCount = Physics.SphereCastNonAlloc(
                position,
                Mathf.Max(0.001f, transformSweepRadius),
                direction,
                TransformSweepHits,
                distance,
                transformSweepMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestNormal = Vector3.zero;
            Transform ownerTransform = MyCore.transform;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = TransformSweepHits[i];
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
        [NonSerialized] private Vector3 _externalMotionVelocity;
        [NonSerialized] private bool _hasSubmittedMotionResult;

        public override void Start()
        {
            base.Start();
            EnsureRuntimeHelpers();
            ResolveMotionModule();
            sharedData ??= ItemShotSharedData.Default;
            ApplyShotData(sharedData, variableData);
        }

        public void Launch(Vector3 direction)
        {
            if (MyCore == null)
                return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : MyCore.transform.forward;
            dir = ApplySpread(dir);
            _targetTransform = null;
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
        }

        public void LaunchTo(Vector3 targetPosition)
        {
            if (MyCore == null)
                return;

            _targetTransform = null;
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
        }

        public void LaunchTo(Transform target)
        {
            LaunchTo(target, aimMode == ShotAimMode.MustHit);
        }

        public void LaunchTo(Transform target, bool mustHit)
        {
            if (target == null)
                return;

            _targetTransform = target;
            if (mustHit && sharedData.allowMustHit)
                aimMode = ShotAimMode.MustHit;
            else if (aimMode == ShotAimMode.Free)
                aimMode = ShotAimMode.Target;

            LaunchTo(target.position);
            _targetTransform = target;
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
                ApplyShotData(block.sharedData, block.initialState);
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

            RefreshTargetPosition();
            ItemMotionModule motionModule = ResolveMotionModule();
            if (_hasSubmittedMotionResult
                && motionModule != null
                && motionModule.TryReadDynamicVelocity(out Vector3 bodyVelocity))
                _externalMotionVelocity = bodyVelocity - state.velocity;
            latestResult = ShotMotionSolver.Step(ref state, config, deltaTime);
            ApplyExternalMotion(ref state, ref latestResult, motionModule, deltaTime);
            TryBuildHitCandidate(ref latestResult);
            TryBuildMustHitCandidate(ref latestResult);

            motionModule?.SetPendingShotResult(latestResult);
            _hasSubmittedMotionResult = motionModule != null;

            if (latestResult.kind == ShotMotionKind.Arrived || latestResult.kind == ShotMotionKind.Expired)
                state.launched = false;
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
            if (blockMode == ShotBlockMode.None)
                return;

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

            ShotHitCandidate hit = _hitResults[0];
            hit.incomingVelocity = result.velocity;
            result.hasHitCandidate = true;
            result.hitCandidate = hit;
        }

        private void TryBuildMustHitCandidate(ref ShotMotionResult result)
        {
            if (aimMode != ShotAimMode.MustHit || result.hasHitCandidate || result.kind != ShotMotionKind.Arrived)
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
