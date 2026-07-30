using UnityEngine;

namespace ES
{
    public struct ItemShotHitQuery
    {
        public Vector3 from;
        public Vector3 to;
        public float radius;
        public LayerMask hitLayers;
        public QueryTriggerInteraction triggerInteraction;
    }

    public interface IItemShotHitSolver
    {
        int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults);
        bool IsOverflow { get; }
    }

    public interface IItemShotTickScheduler
    {
        bool ShouldTick(in ShotMotionState state, int frameCount);
    }

    public sealed class ItemShotAlwaysTickScheduler : IItemShotTickScheduler
    {
        public bool ShouldTick(in ShotMotionState state, int frameCount)
        {
            return state.launched;
        }
    }

    public sealed class ItemShotPhysicsHitSolver : IItemShotHitSolver
    {
        private RaycastHit[] _hitBuffer;

        public bool IsOverflow { get; private set; }

        public ItemShotPhysicsHitSolver(int capacity)
        {
            EnsureCapacity(capacity);
        }

        public int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults)
        {
            IsOverflow = false;
            if (results == null || maxResults <= 0)
                return 0;

            EnsureCapacity(maxResults);

            Vector3 delta = query.to - query.from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            LayerMask hitLayers = ESPhysicsLayers.ResolveShotHitMask(query.hitLayers);
            ESPhysicsQueryModule physicsQuery = ESGameManager.PhysicsQueryModule;
            int count = physicsQuery != null
                ? physicsQuery.ShotCast(query.from, query.to, query.radius, hitLayers, _hitBuffer, query.triggerInteraction)
                : Physics.SphereCastNonAlloc(
                    query.from,
                    Mathf.Max(0f, query.radius),
                    delta / distance,
                    _hitBuffer,
                    distance,
                    hitLayers,
                    query.triggerInteraction);

            if (count <= 0)
                return 0;

            IsOverflow = count >= _hitBuffer.Length;
            int written = 0;
            for (int i = 0; i < count && written < maxResults; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                results[written++] = new ShotHitCandidate
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

    public enum ESHitTagEligibilityResult : byte
    {
        Allowed = 0,
        MissingAttacker = 1,
        MissingTarget = 2,
        AttackerTagDenied = 3,
        TargetTagDenied = 4
    }

    /// <summary>
    /// Tag-only eligibility contract for a HitResolver. Physics produces candidates and combat
    /// owns damage, faction, and final resolution; this policy answers only whether the current
    /// attacker/target facts permit the hit to continue.
    /// </summary>
    [System.Serializable]
    public sealed class ESHitTagEligibility
    {
        [Tooltip("Optional facts the attacker must satisfy before a hit can resolve.")]
        public ESTagConditionConfig attackerCondition = new ESTagConditionConfig();

        [Tooltip("Optional facts the target must satisfy before a hit can resolve, such as 可受击 and not 无敌.")]
        public ESTagConditionConfig targetCondition = new ESTagConditionConfig();

        public bool TryAllows(Entity attacker, Entity target, out ESHitTagEligibilityResult result, out string error)
        {
            error = null;
            if (attackerCondition != null && !attackerCondition.IsEmpty)
            {
                if (attacker == null)
                {
                    result = ESHitTagEligibilityResult.MissingAttacker;
                    return true;
                }

                if (!attacker.Tags.TryMatches(attackerCondition, out bool attackerMatches, out error))
                {
                    result = ESHitTagEligibilityResult.AttackerTagDenied;
                    return false;
                }

                if (!attackerMatches)
                {
                    result = ESHitTagEligibilityResult.AttackerTagDenied;
                    return true;
                }
            }

            if (targetCondition != null && !targetCondition.IsEmpty)
            {
                if (target == null)
                {
                    result = ESHitTagEligibilityResult.MissingTarget;
                    return true;
                }

                if (!target.Tags.TryMatches(targetCondition, out bool targetMatches, out error))
                {
                    result = ESHitTagEligibilityResult.TargetTagDenied;
                    return false;
                }

                if (!targetMatches)
                {
                    result = ESHitTagEligibilityResult.TargetTagDenied;
                    return true;
                }
            }

            result = ESHitTagEligibilityResult.Allowed;
            return true;
        }
    }
}
