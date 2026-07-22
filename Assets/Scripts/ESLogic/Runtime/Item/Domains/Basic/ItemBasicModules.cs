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

        [Title("运行监控")]
        [ShowInInspector, ReadOnly] public Vector3 currentPosition;
        [ShowInInspector, ReadOnly] public Quaternion currentRotation;
        [ShowInInspector, ReadOnly] public Vector3 currentVelocity;

        [NonSerialized] private Rigidbody _rigidbody;
        [NonSerialized] private bool _hasPendingResult;
        [NonSerialized] private ShotMotionResult _pendingResult;

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

        public void SubmitShotResult(in ShotMotionResult result)
        {
            _pendingResult = result;
            _hasPendingResult = true;
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
            if (!_hasPendingResult || MyCore == null)
                return;

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
        public LayerMask hitLayers = ~0;
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
        [NonSerialized] private IItemShotTickScheduler _tickScheduler;
        [NonSerialized] private ItemMotionModule _motionModule;
        [NonSerialized] private Transform _targetTransform;

        public override void Start()
        {
            base.Start();
            EnsureRuntimeHelpers();
            ResolveMotionModule();
            ApplyShotData(sharedData, variableData);
        }

        public void Launch(Vector3 direction)
        {
            if (MyCore == null)
                return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : MyCore.transform.forward;
            dir = ApplySpread(dir);
            _targetTransform = null;
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

        public void ApplyShotData(in ItemShotSharedData shared, in ItemShotVariableData variable)
        {
            if (!shared.enabled)
                return;

            sharedData = shared;
            variableData = NormalizeVariable(variable);

            aimMode = shared.aimMode;
            if (variableData.forceMustHit && shared.allowMustHit)
                aimMode = ShotAimMode.MustHit;

            blockMode = shared.blockMode;
            hitLayers = shared.hitLayers;
            castRadius = Mathf.Max(0f, shared.radius * variableData.radiusMultiplier);
            config = shared.ToShotMotionConfig(variableData);
        }

        public void ApplyShotData(ItemDataInfo itemData)
        {
            if (itemData == null)
                return;

            ApplyShotData(itemData.shotShared, itemData.shotVariable);
        }

        protected override void Update()
        {
            Tick(Time.deltaTime);
        }

        public void SetHitSolver(IItemShotHitSolver solver)
        {
            _hitSolver = solver;
        }

        public void SetTickScheduler(IItemShotTickScheduler scheduler)
        {
            _tickScheduler = scheduler;
        }

        private void Tick(float deltaTime)
        {
            EnsureRuntimeHelpers();
            if (!state.launched)
                return;

            if (!_tickScheduler.ShouldTick(state, Time.frameCount))
                return;

            RefreshTargetPosition();
            latestResult = ShotMotionSolver.Step(ref state, config, deltaTime);
            TryBuildHitCandidate(ref latestResult);
            TryBuildMustHitCandidate(ref latestResult);

            ResolveMotionModule()?.SubmitShotResult(latestResult);

            if (latestResult.kind == ShotMotionKind.Arrived || latestResult.kind == ShotMotionKind.Expired)
                state.launched = false;
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

            if (_tickScheduler == null)
                _tickScheduler = new ItemShotAlwaysTickScheduler();
        }
    }
}
