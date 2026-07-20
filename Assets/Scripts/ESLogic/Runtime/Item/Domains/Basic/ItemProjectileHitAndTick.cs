using UnityEngine;

namespace ES
{
    public struct ItemProjectileHitQuery
    {
        public Vector3 from;
        public Vector3 to;
        public float radius;
        public LayerMask hitLayers;
        public QueryTriggerInteraction triggerInteraction;
    }

    public interface IItemProjectileHitSolver
    {
        int Query(in ItemProjectileHitQuery query, ProjectileHitCandidate[] results, int maxResults);
        bool IsOverflow { get; }
    }

    public interface IItemProjectileTickScheduler
    {
        bool ShouldTick(in ProjectileMotionState state, int frameCount);
    }

    public sealed class ItemProjectileAlwaysTickScheduler : IItemProjectileTickScheduler
    {
        public bool ShouldTick(in ProjectileMotionState state, int frameCount)
        {
            return state.launched;
        }
    }

    public sealed class ItemProjectilePhysicsHitSolver : IItemProjectileHitSolver
    {
        private RaycastHit[] _hitBuffer;

        public bool IsOverflow { get; private set; }

        public ItemProjectilePhysicsHitSolver(int capacity)
        {
            EnsureCapacity(capacity);
        }

        public int Query(in ItemProjectileHitQuery query, ProjectileHitCandidate[] results, int maxResults)
        {
            IsOverflow = false;
            if (results == null || maxResults <= 0)
                return 0;

            EnsureCapacity(maxResults);

            Vector3 delta = query.to - query.from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            int count = Physics.SphereCastNonAlloc(
                query.from,
                Mathf.Max(0f, query.radius),
                delta / distance,
                _hitBuffer,
                distance,
                query.hitLayers,
                query.triggerInteraction);

            if (count <= 0)
                return 0;

            IsOverflow = count >= _hitBuffer.Length;
            int written = 0;
            for (int i = 0; i < count && written < maxResults; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                results[written++] = new ProjectileHitCandidate
                {
                    collider = hit.collider,
                    point = hit.point,
                    normal = hit.normal,
                    incomingVelocity = Vector3.zero,
                    distance = hit.distance,
                    layer = hit.collider != null ? hit.collider.gameObject.layer : 0,
                    isTrigger = hit.collider != null && hit.collider.isTrigger
                };
            }

            return written;
        }

        private void EnsureCapacity(int capacity)
        {
            int useCapacity = Mathf.Max(1, capacity);
            if (_hitBuffer == null || _hitBuffer.Length != useCapacity)
                _hitBuffer = new RaycastHit[useCapacity];
        }
    }
}
