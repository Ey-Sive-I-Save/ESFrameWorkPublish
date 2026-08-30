using System;
using UnityEngine;

namespace ES
{
    public enum ESSpaceProbeShape : byte
    {
        OverlapSphere = 0,
        OverlapBox = 1,
        OverlapCapsule = 2,
        Cast = 3,
        BoxCast = 4,
        CapsuleCast = 5
    }

    public enum ESSpaceProbeStatus : byte
    {
        Completed = 0,
        InvalidRequest = 1,
        NoBuffer = 2,
        Overflow = 3
    }

    public struct ESSpaceProbeRequest
    {
        public ESSpaceProbeShape shape;
        public Vector3 origin;
        public Vector3 destination;
        public Vector3 halfExtents;
        public Vector3 capsulePointA;
        public Vector3 capsulePointB;
        public Quaternion orientation;
        public float radius;
        public LayerMask layerMask;
        public QueryTriggerInteraction triggerInteraction;
        public Entity owner;
        public int maxResults;

        public static ESSpaceProbeRequest Sphere(Vector3 center, float radius, LayerMask mask,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide, Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.OverlapSphere,
                origin = center,
                radius = radius,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }

        public static ESSpaceProbeRequest Cast(Vector3 from, Vector3 to, float radius, LayerMask mask,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide, Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.Cast,
                origin = from,
                destination = to,
                radius = radius,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }

        public static ESSpaceProbeRequest Box(Vector3 center, Vector3 halfExtents, Quaternion rotation,
            LayerMask mask, QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide, Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.OverlapBox,
                origin = center,
                halfExtents = halfExtents,
                orientation = rotation,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }

        public static ESSpaceProbeRequest Capsule(Vector3 pointA, Vector3 pointB, float radius, LayerMask mask,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide, Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.OverlapCapsule,
                origin = pointA,
                destination = pointB,
                radius = radius,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }

        public static ESSpaceProbeRequest BoxCast(Vector3 from, Vector3 to, Vector3 halfExtents,
            Quaternion rotation, LayerMask mask, QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide,
            Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.BoxCast,
                origin = from,
                destination = to,
                halfExtents = halfExtents,
                orientation = rotation,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }

        public static ESSpaceProbeRequest CapsuleCast(Vector3 pointA, Vector3 pointB, float radius,
            Vector3 from, Vector3 to, LayerMask mask,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide, Entity owner = null)
        {
            return new ESSpaceProbeRequest
            {
                shape = ESSpaceProbeShape.CapsuleCast,
                origin = from,
                destination = to,
                capsulePointA = pointA,
                capsulePointB = pointB,
                radius = radius,
                layerMask = mask,
                triggerInteraction = triggers,
                owner = owner,
                maxResults = 0
            };
        }
    }

    public readonly struct ESSpaceProbeHit
    {
        public readonly Collider collider;
        public readonly Entity entity;
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly float distance;
        public readonly bool isTrigger;

        public ESSpaceProbeHit(Collider collider, Entity entity, Vector3 point, Vector3 normal,
            float distance, bool isTrigger)
        {
            this.collider = collider;
            this.entity = entity;
            this.point = point;
            this.normal = normal;
            this.distance = distance;
            this.isTrigger = isTrigger;
        }
    }

    public readonly struct ESSpaceProbeResult
    {
        public readonly ESSpaceProbeStatus status;
        public readonly int count;
        public readonly int colliderCount;
        public readonly bool overflow;

        public ESSpaceProbeResult(ESSpaceProbeStatus status, int count, int colliderCount, bool overflow)
        {
            this.status = status;
            this.count = count;
            this.colliderCount = colliderCount;
            this.overflow = overflow;
        }
    }

    /// <summary>
    /// ES 3D 游戏空间探查统一入口。它编排物理查询并归一化 Collider→Entity，
    /// 但不执行伤害、状态变更或玩法资格裁决。
    /// </summary>
    public sealed class ESSpaceProbe
    {
        private readonly ESPhysicsQueryModule physics;
        private RaycastHit[] castBuffer;
        private Collider[] overlapBuffer;
        private int[] entityIds;

        public ESSpaceProbe(ESPhysicsQueryModule physicsQuery = null, int capacity = 32)
        {
            physics = physicsQuery;
            int safeCapacity = Mathf.Clamp(capacity, 1, 256);
            castBuffer = new RaycastHit[safeCapacity];
            overlapBuffer = new Collider[safeCapacity];
            entityIds = new int[safeCapacity];
        }

        public int Capacity => overlapBuffer != null ? overlapBuffer.Length : 0;

        /// <summary>统一的原始 Collider Overlap 入口，供领域后端复用调用方工作区。</summary>
        public int OverlapSphere(Vector3 center, float radius, LayerMask mask,
            Collider[] results, QueryTriggerInteraction triggers)
        {
            if (results == null || results.Length == 0)
                return 0;
            return physics != null
                ? physics.OverlapSphere(center, radius, mask, results, triggers)
                : Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, radius), results, mask, triggers);
        }

        /// <summary>统一的原始 Cast 入口；命中语义仍由 Shot/Combat 领域决定。</summary>
        public int Cast(Vector3 from, Vector3 to, float radius, LayerMask mask,
            RaycastHit[] results, QueryTriggerInteraction triggers)
        {
            if (results == null || results.Length == 0)
                return 0;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;
            return physics != null
                ? physics.ShotCast(from, to, radius, mask, results, triggers)
                : radius > 0.0001f
                    ? Physics.SphereCastNonAlloc(from, radius, delta / distance, results, distance, mask, triggers)
                    : Physics.RaycastNonAlloc(from, delta / distance, results, distance, mask, triggers);
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, LayerMask mask,
            out RaycastHit hit, QueryTriggerInteraction triggers)
        {
            hit = default;
            if (direction.sqrMagnitude <= 0.0001f || distance <= 0f)
                return false;
            if (physics != null)
            {
                int count = physics.Raycast(origin, direction, distance, mask, castBuffer, triggers);
                if (count <= 0)
                    return false;
                int nearest = 0;
                for (int i = 1; i < count && i < castBuffer.Length; i++)
                    if (castBuffer[i].distance < castBuffer[nearest].distance)
                        nearest = i;
                hit = castBuffer[nearest];
                return hit.collider != null;
            }
            return Physics.Raycast(origin, direction, out hit, distance, mask, triggers);
        }

        public bool SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask mask,
            out RaycastHit hit, QueryTriggerInteraction triggers)
        {
            hit = default;
            if (radius <= 0.0001f)
                return Raycast(origin, direction, distance, mask, out hit, triggers);
            Vector3 to = origin + direction.normalized * Mathf.Max(0f, distance);
            int count = Cast(origin, to, radius, mask, castBuffer, triggers);
            if (count <= 0)
                return false;
            int nearest = 0;
            for (int i = 1; i < count && i < castBuffer.Length; i++)
                if (castBuffer[i].distance < castBuffer[nearest].distance)
                    nearest = i;
            hit = castBuffer[nearest];
            return hit.collider != null;
        }

        public ESSpaceProbeResult Execute(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            if (results == null || results.Length == 0)
                return new ESSpaceProbeResult(ESSpaceProbeStatus.NoBuffer, 0, 0, false);
            if (request.triggerInteraction == QueryTriggerInteraction.UseGlobal)
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);

            switch (request.shape)
            {
                case ESSpaceProbeShape.OverlapSphere:
                    return ExecuteSphere(request, results, out written);
                case ESSpaceProbeShape.Cast:
                    return ExecuteCast(request, results, out written);
                case ESSpaceProbeShape.OverlapBox:
                    return ExecuteBox(request, results, out written);
                case ESSpaceProbeShape.OverlapCapsule:
                    return ExecuteCapsule(request, results, out written);
                case ESSpaceProbeShape.BoxCast:
                    return ExecuteBoxCast(request, results, out written);
                case ESSpaceProbeShape.CapsuleCast:
                    return ExecuteCapsuleCast(request, results, out written);
                default:
                    return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);
            }
        }

        private ESSpaceProbeResult ExecuteBox(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            Vector3 extents = request.halfExtents;
            if (extents.x < 0f || extents.y < 0f || extents.z < 0f
                || float.IsNaN(extents.x) || float.IsNaN(extents.y) || float.IsNaN(extents.z))
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);

            int count = physics != null
                ? physics.OverlapBox(request.origin, extents, request.orientation, request.layerMask,
                    overlapBuffer, request.triggerInteraction)
                : Physics.OverlapBoxNonAlloc(request.origin, extents, overlapBuffer, request.orientation,
                    request.layerMask, request.triggerInteraction);
            bool overflow = count >= overlapBuffer.Length;
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && written < resultLimit; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                Vector3 point = collider.ClosestPoint(request.origin);
                results[written++] = new ESSpaceProbeHit(collider, entity, point, point - request.origin, 0f,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private ESSpaceProbeResult ExecuteSphere(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            if (request.radius < 0f || float.IsNaN(request.radius) || float.IsInfinity(request.radius))
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);

            int count = physics != null
                ? physics.OverlapSphere(request.origin, request.radius, request.layerMask, overlapBuffer,
                    request.triggerInteraction)
                : Physics.OverlapSphereNonAlloc(request.origin, Mathf.Max(0f, request.radius), overlapBuffer,
                    request.layerMask, request.triggerInteraction);
            bool overflow = count >= overlapBuffer.Length;
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && written < resultLimit; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                Vector3 point = collider.ClosestPoint(request.origin);
                results[written++] = new ESSpaceProbeHit(collider, entity, point, point - request.origin, 0f,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private ESSpaceProbeResult ExecuteCapsule(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            if (request.radius < 0f || float.IsNaN(request.radius) || float.IsInfinity(request.radius))
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);
            int count = physics != null
                ? physics.OverlapCapsule(request.origin, request.destination, request.radius,
                    request.layerMask, overlapBuffer, request.triggerInteraction)
                : Physics.OverlapCapsuleNonAlloc(request.origin, request.destination, request.radius,
                    overlapBuffer, request.layerMask, request.triggerInteraction);
            bool overflow = count >= overlapBuffer.Length;
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && written < resultLimit; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                Vector3 point = collider.ClosestPoint(request.origin);
                results[written++] = new ESSpaceProbeHit(collider, entity, point, point - request.origin, 0f,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private ESSpaceProbeResult ExecuteBoxCast(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            Vector3 delta = request.destination - request.origin;
            float distance = delta.magnitude;
            Vector3 extents = request.halfExtents;
            if (distance <= 0.0001f || extents.x < 0f || extents.y < 0f || extents.z < 0f)
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);
            int count = physics != null
                ? physics.BoxCast(request.origin, extents, delta, distance, request.orientation,
                    request.layerMask, castBuffer, request.triggerInteraction)
                : Physics.BoxCastNonAlloc(request.origin, extents, castBuffer, delta / distance,
                    request.orientation, distance, request.layerMask, request.triggerInteraction);
            bool overflow = count >= castBuffer.Length;
            Array.Sort(castBuffer, 0, Mathf.Min(count, castBuffer.Length), RaycastHitComparer.Instance);
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && i < castBuffer.Length && written < resultLimit; i++)
            {
                RaycastHit hit = castBuffer[i];
                Collider collider = hit.collider;
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                results[written++] = new ESSpaceProbeHit(collider, entity, hit.point, hit.normal, hit.distance,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private ESSpaceProbeResult ExecuteCapsuleCast(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            Vector3 delta = request.destination - request.origin;
            float distance = delta.magnitude;
            if (distance <= 0.0001f || request.radius < 0f)
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);
            int count = physics != null
                ? physics.CapsuleCast(request.capsulePointA, request.capsulePointB, request.radius, delta,
                    distance, request.layerMask, castBuffer, request.triggerInteraction)
                : Physics.CapsuleCastNonAlloc(request.capsulePointA, request.capsulePointB, request.radius,
                    delta / distance, castBuffer, distance, request.layerMask, request.triggerInteraction);
            bool overflow = count >= castBuffer.Length;
            Array.Sort(castBuffer, 0, Mathf.Min(count, castBuffer.Length), RaycastHitComparer.Instance);
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && i < castBuffer.Length && written < resultLimit; i++)
            {
                RaycastHit hit = castBuffer[i];
                Collider collider = hit.collider;
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                results[written++] = new ESSpaceProbeHit(collider, entity, hit.point, hit.normal, hit.distance,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private ESSpaceProbeResult ExecuteCast(in ESSpaceProbeRequest request, ESSpaceProbeHit[] results,
            out int written)
        {
            written = 0;
            Vector3 delta = request.destination - request.origin;
            float distance = delta.magnitude;
            if (distance <= 0.0001f || request.radius < 0f)
                return new ESSpaceProbeResult(ESSpaceProbeStatus.InvalidRequest, 0, 0, false);
            int count = physics != null
                ? physics.ShotCast(request.origin, request.destination, request.radius, request.layerMask,
                    castBuffer, request.triggerInteraction)
                : (request.radius > 0.0001f
                    ? Physics.SphereCastNonAlloc(request.origin, request.radius, delta / distance, castBuffer,
                        distance, request.layerMask, request.triggerInteraction)
                    : Physics.RaycastNonAlloc(request.origin, delta / distance, castBuffer, distance,
                        request.layerMask, request.triggerInteraction));
            bool overflow = count >= castBuffer.Length;
            Array.Sort(castBuffer, 0, Mathf.Min(count, castBuffer.Length), RaycastHitComparer.Instance);
            int unique = 0;
            int resultLimit = request.maxResults > 0
                ? Mathf.Min(request.maxResults, results.Length)
                : results.Length;
            for (int i = 0; i < count && i < castBuffer.Length && written < resultLimit; i++)
            {
                RaycastHit hit = castBuffer[i];
                Collider collider = hit.collider;
                if (collider == null || IsOwner(collider, request.owner))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int id = entity.GetInstanceID();
                if (ContainsEntity(id, unique))
                    continue;
                if (unique < entityIds.Length)
                    entityIds[unique++] = id;
                results[written++] = new ESSpaceProbeHit(collider, entity, hit.point, hit.normal, hit.distance,
                    collider.isTrigger);
            }
            return new ESSpaceProbeResult(overflow ? ESSpaceProbeStatus.Overflow : ESSpaceProbeStatus.Completed,
                written, unique, overflow);
        }

        private bool ContainsEntity(int id, int count)
        {
            for (int i = 0; i < count; i++)
                if (entityIds[i] == id) return true;
            return false;
        }

        private static bool IsOwner(Collider collider, Entity owner)
        {
            return owner != null && (collider.transform == owner.transform
                || collider.transform.IsChildOf(owner.transform));
        }

        private sealed class RaycastHitComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            internal static readonly RaycastHitComparer Instance = new RaycastHitComparer();
            public int Compare(RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance);
        }
    }

    /// <summary>持续区域的 Trigger 候选缓存；只提供候选，不直接裁决或施加伤害。</summary>
    public sealed class ESSpaceProbeTrigger
    {
        private readonly Collider[] colliders;
        private readonly int[] entityIds;
        private int count;

        public ESSpaceProbeTrigger(int capacity = 32)
        {
            int safeCapacity = Mathf.Clamp(capacity, 1, 256);
            colliders = new Collider[safeCapacity];
            entityIds = new int[safeCapacity];
        }

        public int Count => count;
        public bool Overflowed { get; private set; }

        public bool Enter(Collider collider)
        {
            if (collider == null || IndexOf(collider) >= 0)
                return false;
            if (count >= colliders.Length)
            {
                Overflowed = true;
                return false;
            }
            colliders[count++] = collider;
            return true;
        }

        public bool Exit(Collider collider)
        {
            int index = IndexOf(collider);
            if (index < 0)
                return false;
            int last = --count;
            colliders[index] = colliders[last];
            colliders[last] = null;
            return true;
        }

        public int Collect(ESSpaceProbeHit[] results, Entity owner = null, int maxResults = 0)
        {
            if (results == null || results.Length == 0)
                return 0;
            PruneInvalid();
            int written = 0;
            int limit = maxResults > 0 ? Mathf.Min(maxResults, results.Length) : results.Length;
            for (int i = 0; i < count && written < limit; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || (owner != null && (collider.transform == owner.transform
                    || collider.transform.IsChildOf(owner.transform))))
                    continue;
                if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity) || entity == null)
                    continue;
                int entityId = entity.GetInstanceID();
                bool duplicate = false;
                for (int j = 0; j < written; j++)
                {
                    if (entityIds[j] == entityId)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                    continue;
                entityIds[written] = entityId;
                Vector3 point = collider.ClosestPoint(collider.transform.position);
                results[written++] = new ESSpaceProbeHit(collider, entity, point, Vector3.zero, 0f,
                    collider.isTrigger);
            }
            return written;
        }

        /// <summary>移除已销毁或已解除 Entity 注册的 Collider，作为 Trigger Exit 的生命周期兜底。</summary>
        public int PruneInvalid()
        {
            int removed = 0;
            int index = 0;
            while (index < count)
            {
                Collider collider = colliders[index];
                bool invalid = collider == null
                    || !ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity entity)
                    || entity == null;
                if (!invalid)
                {
                    index++;
                    continue;
                }

                int last = --count;
                colliders[index] = colliders[last];
                colliders[last] = null;
                removed++;
            }
            return removed;
        }

        public void Clear()
        {
            Array.Clear(colliders, 0, colliders.Length);
            count = 0;
            Overflowed = false;
        }

        private int IndexOf(Collider collider)
        {
            if (collider == null)
                return -1;
            for (int i = 0; i < count; i++)
                if (colliders[i] == collider)
                    return i;
            return -1;
        }
    }
}
