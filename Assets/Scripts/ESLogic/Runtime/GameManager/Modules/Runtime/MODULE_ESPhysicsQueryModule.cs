using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct ESPhysicsQueryStats
    {
        public int raycastCount;
        public int sphereCastCount;
        public int overlapSphereCount;
        public int overlapBoxCount;
        public int overflowCount;

        public void Clear()
        {
            raycastCount = 0;
            sphereCastCount = 0;
            overlapSphereCount = 0;
            overlapBoxCount = 0;
            overflowCount = 0;
        }
    }

    [Serializable]
    public sealed class ESPhysicsLayerConfig
    {
        [Title("通用")]
        [LabelText("场景阻挡")]
        public LayerMask worldBlockLayers = ~0;

        [LabelText("角色身体")]
        public LayerMask entityBodyLayers = ~0;

        [LabelText("角色受击")]
        public LayerMask entityHurtboxLayers = ~0;

        [LabelText("Item物体")]
        public LayerMask itemBodyLayers = ~0;

        [LabelText("交互")]
        public LayerMask interactionLayers = ~0;

        [LabelText("陷阱/区域")]
        public LayerMask triggerZoneLayers = ~0;

        [Title("组合")]
        [LabelText("飞行物命中")]
        public LayerMask shotHitLayers = ~0;

        [LabelText("近战命中")]
        public LayerMask meleeHitLayers = ~0;

        [LabelText("交互探测")]
        public LayerMask interactionProbeLayers = ~0;
    }

    [Serializable]
    [TypeRegistryItem("物理查询模块")]
    public sealed class ESPhysicsQueryModule : ESRuntimeModule
    {
        [Title("配置")]
        [HideLabel]
        public ESPhysicsLayerConfig layers = new ESPhysicsLayerConfig();

        [LabelText("默认Trigger策略")]
        public QueryTriggerInteraction defaultTriggerInteraction = QueryTriggerInteraction.Collide;

        [LabelText("共享Ray缓存容量")]
        [MinValue(1)]
        public int sharedRaycastCapacity = 32;

        [LabelText("共享Collider缓存容量")]
        [MinValue(1)]
        public int sharedColliderCapacity = 64;

        [Title("运行统计")]
        [ShowInInspector, ReadOnly]
        public ESPhysicsQueryStats stats;

        private RaycastHit[] sharedRaycastHits;
        private Collider[] sharedColliders;

        public RaycastHit[] SharedRaycastHits
        {
            get
            {
                EnsureRaycastBuffer();
                return sharedRaycastHits;
            }
        }

        public Collider[] SharedColliders
        {
            get
            {
                EnsureColliderBuffer();
                return sharedColliders;
            }
        }

        public override void Start()
        {
            EnsureBuffers();
        }

        public int Raycast(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.raycastCount++;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, results, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int Raycast(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results)
        {
            return Raycast(origin, direction, distance, layerMask, results, defaultTriggerInteraction);
        }

        public int RaycastShared(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask)
        {
            EnsureRaycastBuffer();
            return Raycast(origin, direction, distance, layerMask, sharedRaycastHits, defaultTriggerInteraction);
        }

        public int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.sphereCastCount++;
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0f, radius), direction.normalized, results, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results)
        {
            return SphereCast(origin, radius, direction, distance, layerMask, results, defaultTriggerInteraction);
        }

        public int SphereCastShared(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask)
        {
            EnsureRaycastBuffer();
            return SphereCast(origin, radius, direction, distance, layerMask, sharedRaycastHits, defaultTriggerInteraction);
        }

        public int OverlapSphere(Vector3 center, float radius, LayerMask layerMask, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0)
                return 0;

            stats.overlapSphereCount++;
            int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, radius), results, layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int OverlapSphere(Vector3 center, float radius, LayerMask layerMask, Collider[] results)
        {
            return OverlapSphere(center, radius, layerMask, results, defaultTriggerInteraction);
        }

        public int OverlapSphereShared(Vector3 center, float radius, LayerMask layerMask)
        {
            EnsureColliderBuffer();
            return OverlapSphere(center, radius, layerMask, sharedColliders, defaultTriggerInteraction);
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0)
                return 0;

            stats.overlapBoxCount++;
            Vector3 safeHalfExtents = new Vector3(
                Mathf.Max(0f, halfExtents.x),
                Mathf.Max(0f, halfExtents.y),
                Mathf.Max(0f, halfExtents.z));
            int count = Physics.OverlapBoxNonAlloc(center, safeHalfExtents, results, orientation, layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask, Collider[] results)
        {
            return OverlapBox(center, halfExtents, orientation, layerMask, results, defaultTriggerInteraction);
        }

        public int OverlapBoxShared(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask)
        {
            EnsureColliderBuffer();
            return OverlapBox(center, halfExtents, orientation, layerMask, sharedColliders, defaultTriggerInteraction);
        }

        public int ShotCast(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            return radius > 0.0001f
                ? SphereCast(from, radius, delta, distance, layerMask, results, triggerInteraction)
                : Raycast(from, delta, distance, layerMask, results, triggerInteraction);
        }

        public int ShotCast(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] results)
        {
            return ShotCast(from, to, radius, layerMask, results, defaultTriggerInteraction);
        }

        public bool TryGetNearestShotHit(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] buffer, QueryTriggerInteraction triggerInteraction, out RaycastHit nearestHit)
        {
            nearestHit = default;
            int count = ShotCast(from, to, radius, layerMask, buffer, triggerInteraction);
            return TrySelectNearestHit(buffer, count, out nearestHit);
        }

        public bool TryFindBestInteraction(Vector3 origin, Vector3 forward, float radius, float maxAngle, LayerMask layerMask, Collider[] buffer, QueryTriggerInteraction triggerInteraction, out Collider bestCollider)
        {
            bestCollider = null;
            int count = OverlapSphere(origin, radius, layerMask, buffer, triggerInteraction);
            if (count <= 0)
                return false;

            bool useAngle = forward.sqrMagnitude > 0.0001f && maxAngle > 0f && maxAngle < 180f;
            Vector3 forwardNormal = useAngle ? forward.normalized : Vector3.forward;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < count && i < buffer.Length; i++)
            {
                Collider candidate = buffer[i];
                if (candidate == null)
                    continue;

                Vector3 candidatePoint = candidate.ClosestPoint(origin);
                Vector3 toCandidate = candidatePoint - origin;
                float distance = toCandidate.magnitude;
                if (distance > radius)
                    continue;

                float angle = 0f;
                if (useAngle && toCandidate.sqrMagnitude > 0.0001f)
                {
                    angle = Vector3.Angle(forwardNormal, toCandidate);
                    if (angle > maxAngle)
                        continue;
                }

                float score = distance + angle * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCollider = candidate;
                }
            }

            return bestCollider != null;
        }

        public int TrapOverlapSphere(Vector3 center, float radius, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            return OverlapSphere(center, radius, layers.triggerZoneLayers, results, triggerInteraction);
        }

        public int TrapOverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            return OverlapBox(center, halfExtents, orientation, layers.triggerZoneLayers, results, triggerInteraction);
        }

        public void ClearStats()
        {
            stats.Clear();
        }

        public void EnsureBuffers()
        {
            EnsureRaycastBuffer();
            EnsureColliderBuffer();
        }

        private void EnsureRaycastBuffer()
        {
            int capacity = Mathf.Max(1, sharedRaycastCapacity);
            if (sharedRaycastHits == null || sharedRaycastHits.Length != capacity)
                sharedRaycastHits = new RaycastHit[capacity];
        }

        private void EnsureColliderBuffer()
        {
            int capacity = Mathf.Max(1, sharedColliderCapacity);
            if (sharedColliders == null || sharedColliders.Length != capacity)
                sharedColliders = new Collider[capacity];
        }

        private void TrackOverflow(int count, int capacity)
        {
            if (count >= capacity)
                stats.overflowCount++;
        }

        private static bool TrySelectNearestHit(RaycastHit[] hits, int count, out RaycastHit nearestHit)
        {
            nearestHit = default;
            if (hits == null || count <= 0)
                return false;

            float nearestDistance = float.PositiveInfinity;
            int safeCount = Mathf.Min(count, hits.Length);
            for (int i = 0; i < safeCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return nearestHit.collider != null;
        }
    }
}
