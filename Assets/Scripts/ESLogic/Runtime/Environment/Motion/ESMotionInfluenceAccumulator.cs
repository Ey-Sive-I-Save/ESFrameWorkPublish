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
            return TryAddVelocity(target, velocity, permissions) == ESMotionSubmitResult.Accepted;
        }

        public static ESMotionSubmitResult TryAddVelocity(
            GameObject target,
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None)
        {
            return ESMotionInfluenceReceiverResolver.TryResolve(target, out var receiver)
                ? receiver.TryAddVelocity(velocity, permissions)
                : ESMotionSubmitResult.UnsupportedTarget;
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
            Vector3 direction = distance > Mathf.Epsilon
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
                Mathf.Max(0f, distance - stopRadius) * Mathf.Max(0f, settings.response));
            float safeDeltaTime = Mathf.Max(MinDeltaTime, deltaTime);
            if (settings.velocityMode == ESMotionAttractionVelocityMode.RadialOnly)
            {
                float currentRadialSpeed = Vector3.Dot(velocity, direction);
                float nextRadialSpeed = Mathf.MoveTowards(
                    currentRadialSpeed,
                    targetSpeed,
                    maxAcceleration * safeDeltaTime);
                return direction * ((nextRadialSpeed - currentRadialSpeed) / safeDeltaTime);
            }

            Vector3 desiredVelocity = direction * targetSpeed;
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
        public const int DefaultFieldCapacity = 4;
        public const int MaxFieldCapacity = 32;

        private sealed class FieldStore
        {
            internal struct Slot
            {
                public ESMotionFieldRequest request;
                public int generation;
                public int activeOrderIndex;
                public bool active;
            }

            public Slot[] slots;
            public int[] activeIndices;
            public int activeCount;

            public FieldStore(int capacity)
            {
                int safeCapacity = Mathf.Clamp(capacity, 1, MaxFieldCapacity);
                slots = new Slot[safeCapacity];
                activeIndices = new int[safeCapacity];
            }

            public int Capacity => slots.Length;

            public bool TryAcquire(in ESMotionFieldRequest request, out int slotIndex, out int generation)
            {
                slotIndex = FindFreeSlot();
                if (slotIndex < 0)
                {
                    generation = 0;
                    return false;
                }

                ref Slot slot = ref slots[slotIndex];
                slot.generation = NextGeneration(slot.generation);
                slot.request = request;
                slot.active = true;

                int insertionIndex = FindInsertionIndex(request);
                for (int i = activeCount; i > insertionIndex; i--)
                {
                    int movedSlot = activeIndices[i - 1];
                    activeIndices[i] = movedSlot;
                    slots[movedSlot].activeOrderIndex = i;
                }
                activeIndices[insertionIndex] = slotIndex;
                slot.activeOrderIndex = insertionIndex;
                activeCount++;
                generation = slot.generation;
                return true;
            }

            public bool Release(int slotIndex, int generation)
            {
                if ((uint)slotIndex >= (uint)slots.Length)
                    return false;

                ref Slot slot = ref slots[slotIndex];
                if (!slot.active || slot.generation != generation)
                    return false;

                int removeIndex = slot.activeOrderIndex;
                for (int i = removeIndex; i < activeCount - 1; i++)
                {
                    int movedSlot = activeIndices[i + 1];
                    activeIndices[i] = movedSlot;
                    slots[movedSlot].activeOrderIndex = i;
                }
                activeCount--;
                activeIndices[activeCount] = 0;
                slot.active = false;
                slot.activeOrderIndex = 0;
                slot.request = default;
                return true;
            }

            private int FindInsertionIndex(in ESMotionFieldRequest request)
            {
                int index = 0;
                while (index < activeCount)
                {
                    ref Slot current = ref slots[activeIndices[index]];
                    if (request.priority > current.request.priority
                        || (request.priority == current.request.priority
                            && request.sourceId < current.request.sourceId))
                        break;
                    index++;
                }
                return index;
            }

            private int FindFreeSlot()
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].active)
                        return i;
                }
                return -1;
            }

        }

        private Vector3 pendingVelocityDelta;
        private FieldStore fieldStore;
        private bool hasPendingVelocityDelta;
        private int ownerGeneration = 1;
        private int rejectedFieldCount;
        private int invalidSolveCount;

        /// <summary>
        /// Creates an accumulator with a bounded store. <paramref name="capacity"/> is kept
        /// for source compatibility; the store always reserves <see cref="MaxFieldCapacity"/>
        /// so field acquisition cannot resize arrays during gameplay.
        /// </summary>
        public ESMotionInfluenceAccumulator(int capacity = DefaultFieldCapacity)
        {
            // Capacity is retained for source compatibility; the runtime store is always
            // bounded at MaxFieldCapacity so later field acquisition cannot resize arrays.
        }

        public int ActiveFieldCount => fieldStore?.activeCount ?? 0;
        public int FieldCapacity => fieldStore?.Capacity ?? 0;
        public int RejectedFieldCount => rejectedFieldCount;
        public int InvalidSolveCount => invalidSolveCount;
        public int OwnerGeneration => ownerGeneration;
        public bool HasPendingVelocityDelta => hasPendingVelocityDelta;
        public bool HasInfluences => ActiveFieldCount > 0 || HasPendingVelocityDelta;

        /// <summary>Allocate the bounded field store during lifecycle setup, never during motion.</summary>
        public void Warmup()
        {
            fieldStore ??= new FieldStore(MaxFieldCapacity);
        }

        public void AddVelocity(
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None)
        {
            TryAddVelocity(velocity);
        }

        public bool TryAddVelocity(Vector3 velocity)
        {
            if (!IsFinite(velocity))
                return false;

            Vector3 combined = pendingVelocityDelta + velocity;
            if (!IsFinite(combined))
                return false;

            pendingVelocityDelta = combined;
            hasPendingVelocityDelta = true;
            return true;
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

            fieldStore ??= new FieldStore(MaxFieldCapacity);
            if (!fieldStore.TryAcquire(request, out int slotIndex, out int slotGeneration))
            {
                rejectedFieldCount++;
                lease = default;
                return false;
            }

            lease = new ESMotionFieldLease(this, slotIndex, slotGeneration, ownerGeneration);
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
            bool canApplyFields = true;
            float safeDeltaTime = 0f;
            if (!IsFinite(deltaTime) || deltaTime < 0f)
            {
                invalidSolveCount++;
                canApplyFields = false;
            }
            else
            {
                safeDeltaTime = deltaTime;
            }

            if (!IsValidLimit(maxCombinedAcceleration))
            {
                invalidSolveCount++;
                canApplyFields = false;
            }

            bool canApplyVelocityDelta = IsValidLimit(maxCombinedVelocityDelta);
            if (!canApplyVelocityDelta)
                invalidSolveCount++;

            double combinedAccelerationX = 0d;
            double combinedAccelerationY = 0d;
            double combinedAccelerationZ = 0d;
            if (canApplyFields && fieldStore != null)
            {
                int priorityCutoff = int.MinValue;
                for (int i = 0; i < fieldStore.activeCount; i++)
                {
                    ref FieldStore.Slot candidate = ref fieldStore.slots[fieldStore.activeIndices[i]];
                    if (candidate.request.blendMode == ESMotionFieldBlendMode.OverrideLowerPriority
                        && ESMotionInfluenceSolver.IsAllowed(candidate.request.permissions, lockState)
                        && IsRuntimeFieldValid(candidate.request))
                    {
                        priorityCutoff = candidate.request.priority;
                        break;
                    }
                }

                for (int i = 0; i < fieldStore.activeCount; i++)
                {
                    ref FieldStore.Slot slot = ref fieldStore.slots[fieldStore.activeIndices[i]];
                    if (slot.request.priority < priorityCutoff)
                        break;
                    if (!ESMotionInfluenceSolver.IsAllowed(slot.request.permissions, lockState))
                        continue;

                    if (slot.request.kind == ESMotionFieldKind.Acceleration)
                    {
                        Accumulate(
                            slot.request.acceleration,
                            ref combinedAccelerationX,
                            ref combinedAccelerationY,
                            ref combinedAccelerationZ);
                    }
                    else
                    {
                        Vector3 anchorPosition = slot.request.ResolveAnchorPosition();
                        if (!IsFinite(anchorPosition))
                        {
                            invalidSolveCount++;
                            continue;
                        }

                        Vector3 attractionAcceleration =
                            ESMotionInfluenceSolver.EvaluateAttractionAcceleration(
                                position,
                                velocity,
                                anchorPosition,
                                slot.request.attraction,
                                safeDeltaTime);
                        if (!IsFinite(attractionAcceleration))
                        {
                            invalidSolveCount++;
                            continue;
                        }

                        Accumulate(
                            attractionAcceleration,
                            ref combinedAccelerationX,
                            ref combinedAccelerationY,
                            ref combinedAccelerationZ);
                    }
                }
            }

            Vector3 combinedAcceleration = ToFiniteVector(
                combinedAccelerationX,
                combinedAccelerationY,
                combinedAccelerationZ,
                maxCombinedAcceleration,
                ref invalidSolveCount);
            Vector3 nextVelocity = velocity + combinedAcceleration * safeDeltaTime;
            if (IsFinite(nextVelocity))
                velocity = nextVelocity;
            else
                invalidSolveCount++;

            Vector3 velocityDelta = Vector3.zero;
            if (hasPendingVelocityDelta)
            {
                velocityDelta = pendingVelocityDelta;
                ClearPendingVelocityDelta();
            }
            velocityDelta = canApplyVelocityDelta
                ? ToFiniteVector(
                    velocityDelta.x,
                    velocityDelta.y,
                    velocityDelta.z,
                    maxCombinedVelocityDelta,
                    ref invalidSolveCount)
                : Vector3.zero;
            nextVelocity = velocity + velocityDelta;
            if (IsFinite(nextVelocity))
                velocity = nextVelocity;
            else
                invalidSolveCount++;
        }

        public void Reset()
        {
            ClearPendingVelocityDelta();
            ownerGeneration = NextGeneration(ownerGeneration);
            fieldStore = null;
        }

        public void ClearPendingVelocityDelta()
        {
            pendingVelocityDelta = Vector3.zero;
            hasPendingVelocityDelta = false;
        }

        public bool ReleaseMotionField(int slotIndex, int slotGeneration, int expectedOwnerGeneration)
        {
            if (expectedOwnerGeneration != ownerGeneration || fieldStore == null)
                return false;

            if (!fieldStore.Release(slotIndex, slotGeneration))
                return false;

            if (fieldStore.activeCount == 0)
            {
                fieldStore = null;
                ownerGeneration = NextGeneration(ownerGeneration);
            }
            return true;
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

        private static bool IsRuntimeFieldValid(in ESMotionFieldRequest request)
        {
            return request.kind != ESMotionFieldKind.Attraction
                || IsFinite(request.ResolveAnchorPosition());
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidLimit(float value)
        {
            // Zero is the established unlimited setting. Negative and non-finite limits are invalid.
            return IsFinite(value) && value >= 0f;
        }

        private static void Accumulate(
            Vector3 value,
            ref double x,
            ref double y,
            ref double z)
        {
            x += value.x;
            y += value.y;
            z += value.z;
        }

        private static Vector3 ToFiniteVector(
            double x,
            double y,
            double z,
            float maxMagnitude,
            ref int invalidSolveCount)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)
                || double.IsNaN(y) || double.IsInfinity(y)
                || double.IsNaN(z) || double.IsInfinity(z))
            {
                invalidSolveCount++;
                return Vector3.zero;
            }

            if (maxMagnitude > 0f)
            {
                double maxComponent = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
                if (maxComponent > 0d)
                {
                    double scaledX = x / maxComponent;
                    double scaledY = y / maxComponent;
                    double scaledZ = z / maxComponent;
                    double magnitude = maxComponent * Math.Sqrt(
                        scaledX * scaledX + scaledY * scaledY + scaledZ * scaledZ);
                    if (magnitude > maxMagnitude)
                    {
                        double scale = maxMagnitude / magnitude;
                        x *= scale;
                        y *= scale;
                        z *= scale;
                    }
                }
            }

            return new Vector3(
                ClampToFloat(x),
                ClampToFloat(y),
                ClampToFloat(z));
        }

        private static float ClampToFloat(double value)
        {
            if (value > float.MaxValue)
                return float.MaxValue;
            if (value < -float.MaxValue)
                return -float.MaxValue;
            return (float)value;
        }
    }
}
