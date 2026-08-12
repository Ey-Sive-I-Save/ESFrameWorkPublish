using System;
using UnityEngine;

namespace ES
{
    public static class ESMotion
    {
        public static bool AddVelocity(GameObject target, Vector3 velocity)
        {
            return AddVelocity(target, velocity, ESMotionInfluencePermissions.None);
        }

        public static bool AddVelocity(
            GameObject target,
            Vector3 velocity,
            ESMotionInfluencePermissions permissions)
        {
            return ESMotionInfluenceReceiverResolver.TryResolve(target, out var receiver)
                && receiver.AddVelocity(velocity, permissions);
        }

        public static bool AddVelocity(Component target, Vector3 velocity)
        {
            return target != null && AddVelocity(target.gameObject, velocity);
        }
    }

    public static class ESMotionInfluenceReceiverResolver
    {
        public static bool TryResolve(GameObject target, out IESMotionInfluenceReceiver receiver)
        {
            receiver = null;
            if (target == null)
                return false;

            Core core = target.GetComponentInParent<Core>();
            if (core is IESMotionInfluenceReceiver coreReceiver)
            {
                receiver = coreReceiver;
                return true;
            }

            VehicleController vehicle = target.GetComponentInParent<VehicleController>();
            if (vehicle != null)
            {
                receiver = vehicle;
                return true;
            }

            return false;
        }
    }

    public static class ESMotionInfluenceSolver
    {
        private const float MinDeltaTime = 0.000001f;

        public static Vector3 EvaluateAttractionAcceleration(
            Vector3 position,
            Vector3 velocity,
            Vector3 anchor,
            in ESMotionAttractionSettings settings,
            float deltaTime)
        {
            Vector3 offset = anchor - position;
            float distance = offset.magnitude;
            float stopRadius = Mathf.Max(0f, settings.stopRadius);
            Vector3 direction = distance > stopRadius && distance > Mathf.Epsilon
                ? offset / distance
                : Vector3.zero;
            float maxAcceleration = Mathf.Max(0f, settings.maxAcceleration);
            if (maxAcceleration <= 0f)
                return Vector3.zero;
            if (settings.model == ESMotionAttractionModel.SpringDamper)
            {
                float distanceError = Mathf.Max(0f, distance - stopRadius);
                float radialVelocity = Vector3.Dot(velocity, direction);
                Vector3 acceleration = direction * (
                    distanceError * Mathf.Max(0f, settings.stiffness)
                    - radialVelocity * Mathf.Max(0f, settings.damping));
                return Vector3.ClampMagnitude(acceleration, maxAcceleration);
            }

            float targetSpeed = Mathf.Min(
                Mathf.Max(0f, settings.maxSpeed),
                (distance - stopRadius) * Mathf.Max(0f, settings.response));
            Vector3 desiredVelocity = direction * targetSpeed;
            float safeDeltaTime = Mathf.Max(MinDeltaTime, deltaTime);
            Vector3 nextVelocity = Vector3.MoveTowards(
                velocity,
                desiredVelocity,
                maxAcceleration * safeDeltaTime);
            return (nextVelocity - velocity) / safeDeltaTime;
        }

        public static bool IsAllowed(
            ESMotionInfluencePermissions permissions,
            ESMotionReceiverLockState lockState)
        {
            if ((lockState & ESMotionReceiverLockState.MatchTarget) != 0
                && (permissions & ESMotionInfluencePermissions.AllowDuringMatchTarget) == 0)
                return false;
            if ((lockState & ESMotionReceiverLockState.Mounted) != 0
                && (permissions & ESMotionInfluencePermissions.AllowWhileMounted) == 0)
                return false;
            if ((lockState & ESMotionReceiverLockState.Climbing) != 0
                && (permissions & ESMotionInfluencePermissions.AllowWhileClimbing) == 0)
                return false;
            return true;
        }
    }

    public sealed class ESMotionInfluenceAccumulator : IESMotionFieldLeaseOwner
    {
        private struct FieldSlot
        {
            public ESMotionFieldRequest request;
            public int generation;
            public bool active;
        }

        private const int InlineCapacity = 4;
        private FieldSlot slot0;
        private FieldSlot slot1;
        private FieldSlot slot2;
        private FieldSlot slot3;
        private FieldSlot[] overflowSlots;
        private Vector3 pendingVelocityDelta0;
        private Vector3 pendingVelocityDelta1;
        private Vector3 pendingVelocityDelta2;
        private Vector3 pendingVelocityDelta3;
        private Vector3 pendingVelocityDelta4;
        private Vector3 pendingVelocityDelta5;
        private Vector3 pendingVelocityDelta6;
        private Vector3 pendingVelocityDelta7;
        private bool hasPendingVelocityDelta;
        private int ownerGeneration = 1;
        private int activeFieldCount;

        public ESMotionInfluenceAccumulator(int capacity = InlineCapacity)
        {
            int overflowCapacity = Mathf.Max(0, capacity - InlineCapacity);
            overflowSlots = overflowCapacity > 0
                ? new FieldSlot[overflowCapacity]
                : Array.Empty<FieldSlot>();
        }

        public int ActiveFieldCount => activeFieldCount;
        public int OwnerGeneration => ownerGeneration;
        public bool HasPendingVelocityDelta => hasPendingVelocityDelta;
        public bool HasInfluences => activeFieldCount > 0 || HasPendingVelocityDelta;

        public void AddVelocity(
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None)
        {
            int permissionIndex = (int)permissions & 0x7;
            switch (permissionIndex)
            {
                case 0: pendingVelocityDelta0 += velocity; break;
                case 1: pendingVelocityDelta1 += velocity; break;
                case 2: pendingVelocityDelta2 += velocity; break;
                case 3: pendingVelocityDelta3 += velocity; break;
                case 4: pendingVelocityDelta4 += velocity; break;
                case 5: pendingVelocityDelta5 += velocity; break;
                case 6: pendingVelocityDelta6 += velocity; break;
                default: pendingVelocityDelta7 += velocity; break;
            }
            hasPendingVelocityDelta = true;
        }

        public bool TryAcquireField(
            in ESMotionFieldRequest request,
            out ESMotionFieldLease lease)
        {
            if (!IsValid(request))
            {
                lease = default;
                return false;
            }

            int slotIndex = FindFreeSlot();
            if (slotIndex < 0)
            {
                int previousLength = overflowSlots.Length;
                int nextLength = Mathf.Max(InlineCapacity, previousLength * 2);
                Array.Resize(ref overflowSlots, nextLength);
                slotIndex = InlineCapacity + previousLength;
            }

            ref FieldSlot slot = ref GetSlot(slotIndex);
            slot.generation = NextGeneration(slot.generation);
            slot.request = request;
            slot.active = true;
            activeFieldCount++;
            lease = new ESMotionFieldLease(this, slotIndex, slot.generation, ownerGeneration);
            return true;
        }

        public void Apply(
            ref Vector3 velocity,
            Vector3 position,
            float deltaTime,
            ESMotionReceiverLockState lockState,
            float maxCombinedAcceleration,
            float maxCombinedVelocityDelta)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            Vector3 combinedAcceleration = Vector3.zero;
            int remainingFields = activeFieldCount;
            int slotCount = InlineCapacity + overflowSlots.Length;
            for (int i = 0; i < slotCount && remainingFields > 0; i++)
            {
                ref FieldSlot slot = ref GetSlot(i);
                if (!slot.active)
                    continue;
                remainingFields--;
                if (!ESMotionInfluenceSolver.IsAllowed(slot.request.permissions, lockState))
                    continue;

                if (slot.request.kind == ESMotionFieldKind.Acceleration)
                {
                    combinedAcceleration += slot.request.acceleration;
                }
                else
                {
                    combinedAcceleration += ESMotionInfluenceSolver.EvaluateAttractionAcceleration(
                        position,
                        velocity,
                        slot.request.ResolveAnchorPosition(),
                        slot.request.attraction,
                        safeDeltaTime);
                }
            }

            if (maxCombinedAcceleration > 0f)
                combinedAcceleration = Vector3.ClampMagnitude(combinedAcceleration, maxCombinedAcceleration);
            velocity += combinedAcceleration * safeDeltaTime;

            Vector3 velocityDelta = Vector3.zero;
            if (hasPendingVelocityDelta)
            {
                ConsumePendingVelocityDeltas(ref velocityDelta, lockState);
            }
            if (maxCombinedVelocityDelta > 0f)
                velocityDelta = Vector3.ClampMagnitude(velocityDelta, maxCombinedVelocityDelta);
            velocity += velocityDelta;
        }

        public void Reset()
        {
            ClearPendingVelocityDelta();
            activeFieldCount = 0;
            ownerGeneration = NextGeneration(ownerGeneration);
            slot0.active = false;
            slot0.request = default;
            slot1.active = false;
            slot1.request = default;
            slot2.active = false;
            slot2.request = default;
            slot3.active = false;
            slot3.request = default;
            for (int i = 0; i < overflowSlots.Length; i++)
            {
                overflowSlots[i].active = false;
                overflowSlots[i].request = default;
            }
        }

        public void ClearPendingVelocityDelta()
        {
            pendingVelocityDelta0 = Vector3.zero;
            pendingVelocityDelta1 = Vector3.zero;
            pendingVelocityDelta2 = Vector3.zero;
            pendingVelocityDelta3 = Vector3.zero;
            pendingVelocityDelta4 = Vector3.zero;
            pendingVelocityDelta5 = Vector3.zero;
            pendingVelocityDelta6 = Vector3.zero;
            pendingVelocityDelta7 = Vector3.zero;
            hasPendingVelocityDelta = false;
        }

        private void ConsumePendingVelocityDeltas(
            ref Vector3 combined,
            ESMotionReceiverLockState lockState)
        {
            AddIfAllowed(ref combined, pendingVelocityDelta0, ESMotionInfluencePermissions.None, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta1, (ESMotionInfluencePermissions)1, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta2, (ESMotionInfluencePermissions)2, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta3, (ESMotionInfluencePermissions)3, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta4, (ESMotionInfluencePermissions)4, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta5, (ESMotionInfluencePermissions)5, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta6, (ESMotionInfluencePermissions)6, lockState);
            AddIfAllowed(ref combined, pendingVelocityDelta7, (ESMotionInfluencePermissions)7, lockState);
            ClearPendingVelocityDelta();
        }

        private static void AddIfAllowed(
            ref Vector3 combined,
            Vector3 pending,
            ESMotionInfluencePermissions permissions,
            ESMotionReceiverLockState lockState)
        {
            if (pending != Vector3.zero
                && ESMotionInfluenceSolver.IsAllowed(permissions, lockState))
                combined += pending;
        }

        public bool ReleaseMotionField(int slotIndex, int slotGeneration, int expectedOwnerGeneration)
        {
            if (expectedOwnerGeneration != ownerGeneration
                || slotIndex < 0
                || slotIndex >= InlineCapacity + overflowSlots.Length)
                return false;

            ref FieldSlot slot = ref GetSlot(slotIndex);
            if (!slot.active || slot.generation != slotGeneration)
                return false;

            slot.active = false;
            slot.request = default;
            activeFieldCount--;
            return true;
        }

        private int FindFreeSlot()
        {
            int slotCount = InlineCapacity + overflowSlots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                if (!GetSlot(i).active)
                    return i;
            }
            return -1;
        }

        private ref FieldSlot GetSlot(int index)
        {
            switch (index)
            {
                case 0: return ref slot0;
                case 1: return ref slot1;
                case 2: return ref slot2;
                case 3: return ref slot3;
                default: return ref overflowSlots[index - InlineCapacity];
            }
        }

        private static int NextGeneration(int generation)
        {
            unchecked
            {
                generation++;
                return generation > 0 ? generation : 1;
            }
        }

        private static bool IsValid(in ESMotionFieldRequest request)
        {
            if (request.kind == ESMotionFieldKind.Acceleration)
                return IsFinite(request.acceleration);

            ESMotionAttractionSettings attraction = request.attraction;
            return IsFinite(request.ResolveAnchorPosition())
                && IsFinite(attraction.stopRadius)
                && IsFinite(attraction.maxSpeed)
                && IsFinite(attraction.maxAcceleration)
                && IsFinite(attraction.response)
                && IsFinite(attraction.stiffness)
                && IsFinite(attraction.damping);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
