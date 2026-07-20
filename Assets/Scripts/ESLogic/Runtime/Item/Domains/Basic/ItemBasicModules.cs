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
        [NonSerialized] private ProjectileMotionResult _pendingResult;

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

        public void SubmitProjectileResult(in ProjectileMotionResult result)
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
    public sealed class ItemProjectileModule : ItemBasicModuleBase
    {
        [Title("飞行物配置")]
        [LabelText("瞄准模式")]
        public ShotAimMode aimMode = ShotAimMode.Free;
        [LabelText("阻挡模式")]
        public ShotBlockMode blockMode = ShotBlockMode.AnyBlocker;
        public ProjectileMotionConfig config = ProjectileMotionConfig.Straight(30f, 5f);
        [LabelText("命中层")]
        public LayerMask hitLayers = ~0;
        [LabelText("命中半径")]
        public float castRadius = 0.05f;
        [LabelText("命中缓存容量")]
        public int hitBufferCapacity = 8;

        [Title("运行监控")]
        [ShowInInspector, ReadOnly] public ProjectileMotionState state;
        [ShowInInspector, ReadOnly] public ProjectileMotionResult latestResult;
        [LabelText("命中缓存溢出次数")]
        [ShowInInspector, ReadOnly] public int hitOverflowCount;

        [NonSerialized] private ProjectileHitCandidate[] _hitResults;
        [NonSerialized] private IItemProjectileHitSolver _hitSolver;
        [NonSerialized] private IItemProjectileTickScheduler _tickScheduler;
        [NonSerialized] private ItemMotionModule _motionModule;
        [NonSerialized] private Transform _targetTransform;

        public override void Start()
        {
            base.Start();
            EnsureRuntimeHelpers();
            ResolveMotionModule();
        }

        public void Launch(Vector3 direction)
        {
            if (MyCore == null)
                return;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : MyCore.transform.forward;
            _targetTransform = null;
            state = new ProjectileMotionState
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
            Vector3 toTarget = targetPosition - MyCore.transform.position;
            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : MyCore.transform.forward;
            state = new ProjectileMotionState
            {
                previousPosition = MyCore.transform.position,
                currentPosition = MyCore.transform.position,
                currentRotation = MyCore.transform.rotation,
                velocity = dir * Mathf.Max(0f, config.speed),
                direction = dir,
                targetPosition = targetPosition,
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
            if (mustHit)
                aimMode = ShotAimMode.MustHit;
            else if (aimMode == ShotAimMode.Free)
                aimMode = ShotAimMode.Target;

            LaunchTo(target.position);
            _targetTransform = target;
        }

        public void ApplyShotConfig(ItemShotConfig shotConfig)
        {
            if (shotConfig == null || !shotConfig.enabled)
                return;

            aimMode = shotConfig.aimMode;
            blockMode = shotConfig.blockMode;
            hitLayers = shotConfig.hitLayers;
            castRadius = Mathf.Max(0f, shotConfig.radius);
            config = shotConfig.ToProjectileMotionConfig();
        }

        protected override void Update()
        {
            Tick(Time.deltaTime);
        }

        public void SetHitSolver(IItemProjectileHitSolver solver)
        {
            _hitSolver = solver;
        }

        public void SetTickScheduler(IItemProjectileTickScheduler scheduler)
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
            latestResult = ProjectileMotionSolver.Step(ref state, config, deltaTime);
            TryBuildHitCandidate(ref latestResult);
            TryBuildMustHitCandidate(ref latestResult);

            ResolveMotionModule()?.SubmitProjectileResult(latestResult);

            if (latestResult.kind == ProjectileMotionKind.Arrived || latestResult.kind == ProjectileMotionKind.Expired)
                state.launched = false;
        }

        private void TryBuildHitCandidate(ref ProjectileMotionResult result)
        {
            if (blockMode == ShotBlockMode.None)
                return;

            if (result.kind == ProjectileMotionKind.Delayed || result.kind == ProjectileMotionKind.Warmup)
                return;

            EnsureRuntimeHelpers();
            ItemProjectileHitQuery query = new ItemProjectileHitQuery
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

            ProjectileHitCandidate hit = _hitResults[0];
            hit.incomingVelocity = result.velocity;
            result.hasHitCandidate = true;
            result.hitCandidate = hit;
        }

        private void TryBuildMustHitCandidate(ref ProjectileMotionResult result)
        {
            if (aimMode != ShotAimMode.MustHit || result.hasHitCandidate || result.kind != ProjectileMotionKind.Arrived)
                return;

            Collider targetCollider = _targetTransform != null ? _targetTransform.GetComponentInChildren<Collider>() : null;
            result.hasHitCandidate = true;
            result.hitCandidate = new ProjectileHitCandidate
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

            state.targetPosition = _targetTransform.position;
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
                _hitResults = new ProjectileHitCandidate[capacity];

            if (_hitSolver == null)
                _hitSolver = new ItemProjectilePhysicsHitSolver(capacity);

            if (_tickScheduler == null)
                _tickScheduler = new ItemProjectileAlwaysTickScheduler();
        }
    }
}
