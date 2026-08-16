using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>One logical member currently overlapping an ESZone.</summary>
    public readonly struct ESZoneMember
    {
        internal ESZoneMember(
            UnityEngine.Object key,
            GameObject rootObject,
            Core core,
            IESMotionInfluenceReceiver motionReceiver)
        {
            Key = key;
            RootObject = rootObject;
            Core = core;
            MotionReceiver = motionReceiver;
        }

        internal UnityEngine.Object Key { get; }
        public GameObject RootObject { get; }
        public Core Core { get; }
        public IESMotionInfluenceReceiver MotionReceiver { get; }

        public bool IsActive
        {
            get
            {
                if (Key == null || RootObject == null)
                    return false;

                if (Core != null)
                    return Core.isActiveAndEnabled;
                if (MotionReceiver is Behaviour receiverBehaviour)
                    return receiverBehaviour.isActiveAndEnabled;
                return RootObject.activeInHierarchy;
            }
        }

        public bool TryGetCore<TCore>(out TCore value) where TCore : Core
        {
            value = Core as TCore;
            return value != null;
        }
    }

    /// <summary>
    /// Generic spatial zone. It owns trigger membership only; ESZoneProfile is the sole
    /// authority that receives the resulting member events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("【ES】/基础设施/环境/通用区域")]
    public sealed class ESZone : MonoBehaviour
    {
        private struct Occupant
        {
            public ESZoneMember member;
            public int overlapCount;
        }

        private struct ColliderOverlap
        {
            public UnityEngine.Object memberKey;
            public int overlapCount;
            public int cleanupIndex;
        }

        private const int CleanupBudgetPerPass = 16;
        private const int InitialMemberCapacity = 4;
        private const int InitialColliderCapacity = 8;

        private Dictionary<UnityEngine.Object, Occupant> occupants;
        private Dictionary<Collider, ColliderOverlap> colliderOverlaps;
        private List<Collider> trackedColliders;
        private ESZoneProfile profile;
        private int cleanupCursor;
        private bool maintenanceRegistered;

        public ESZoneProfile Profile => profile;
        public int Priority => Profile != null ? Profile.Settings.Priority : 0;
        public int ActiveMemberCount => occupants?.Count ?? 0;

        public bool HasSemanticTag(ESTagStableReference tag)
        {
            return Profile != null && Profile.Settings.HasSemanticTag(tag);
        }

        public bool Contains(Core core)
        {
            return core != null && occupants != null && occupants.ContainsKey(core);
        }

        public bool Contains(Collider collider)
        {
            return collider != null
                   && TryResolveMember(collider, out UnityEngine.Object key, out _)
                   && occupants != null
                   && occupants.ContainsKey(key);
        }

        public void CopyMembersTo(List<ESZoneMember> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (occupants == null)
                return;
            foreach (Occupant occupant in occupants.Values)
                destination.Add(occupant.member);
        }

        public bool TryValidateConfiguration(out string error)
        {
            Collider[] zoneColliders = GetComponents<Collider>();
            if (zoneColliders == null || zoneColliders.Length == 0)
            {
                error = "区域至少需要一个 Collider。";
                return false;
            }

            for (int i = 0; i < zoneColliders.Length; i++)
            {
                if (zoneColliders[i] != null && zoneColliders[i].isTrigger)
                    continue;

                error = "区域 Collider 必须全部启用 Is Trigger。";
                return false;
            }

            if (Profile != null && !Profile.ValidateProfile(new List<string>()))
            {
                error = "同根 ESZoneProfile 配置无效。";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!TryValidateConfiguration(out string error))
                Debug.LogWarning("[ESZone] 配置无效: " + error, this);

            if (gameObject.layer != ESPhysicsLayers.TriggerZone)
                Debug.LogWarning("[ESZone] 请按 GameCore Layer 规则使用 TriggerZone 层。", this);
        }
#endif

        private void Awake()
        {
            profile = GetComponent<ESZoneProfile>();
        }

        private void OnEnable()
        {
            // SubsystemRegistration preserves live registrations when scene reload is disabled,
            // while an ordinary domain/scene reload rebuilds them through OnEnable. Register is
            // idempotent, so reconciling here is safe for either lifecycle.
            if (trackedColliders != null && trackedColliders.Count > 0)
            {
                maintenanceRegistered = false;
                RegisterMaintenance();
            }
        }

        internal void RegisterProfile(ESZoneProfile candidate)
        {
            if (candidate == null || candidate.gameObject != gameObject)
                return;

            profile = candidate;
            if (occupants == null)
                return;
            foreach (Occupant occupant in occupants.Values)
            {
                if (occupant.member.IsActive)
                    TryNotifyProfileEntered(occupant.member);
            }
        }

        internal void UnregisterProfile(ESZoneProfile candidate)
        {
            if (!ReferenceEquals(profile, candidate))
                return;

            if (occupants != null)
            {
                foreach (Occupant occupant in occupants.Values)
                    candidate.ExitMember(occupant.member);
            }
            profile = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            if (colliderOverlaps != null
                && colliderOverlaps.TryGetValue(other, out ColliderOverlap colliderOverlap))
            {
                colliderOverlap.overlapCount++;
                colliderOverlaps[other] = colliderOverlap;
                if (occupants.TryGetValue(colliderOverlap.memberKey, out Occupant existingOccupant))
                {
                    existingOccupant.overlapCount++;
                    occupants[colliderOverlap.memberKey] = existingOccupant;
                }
                return;
            }

            if (!TryResolveMember(other, out UnityEngine.Object key, out ESZoneMember member)
                || !member.IsActive)
                return;

            EnsureTrackingCollections();
            colliderOverlaps.Add(other, new ColliderOverlap
            {
                memberKey = key,
                overlapCount = 1,
                cleanupIndex = trackedColliders.Count
            });
            trackedColliders.Add(other);
            RegisterMaintenance();

            if (occupants.TryGetValue(key, out Occupant occupant))
            {
                occupant.overlapCount++;
                occupants[key] = occupant;
                return;
            }

            occupant = new Occupant
            {
                member = member,
                overlapCount = 1
            };
            occupants.Add(key, occupant);
            NotifyMemberEntered(member);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || colliderOverlaps == null
                || !colliderOverlaps.TryGetValue(other, out ColliderOverlap overlap))
                return;

            RemoveColliderOverlap(other, overlap, false);
        }

        internal void RunMaintenance()
        {
            if (trackedColliders == null
                || trackedColliders.Count == 0)
                return;

            int remainingBudget = Mathf.Min(CleanupBudgetPerPass, trackedColliders.Count);
            while (remainingBudget-- > 0 && trackedColliders.Count > 0)
            {
                if (cleanupCursor >= trackedColliders.Count)
                    cleanupCursor = 0;

                Collider collider = trackedColliders[cleanupCursor];
                if (!colliderOverlaps.TryGetValue(collider, out ColliderOverlap overlap))
                {
                    RemoveTrackedColliderAt(cleanupCursor);
                    continue;
                }

                if (collider == null
                    || !occupants.TryGetValue(overlap.memberKey, out Occupant occupant)
                    || !occupant.member.IsActive)
                {
                    RemoveColliderOverlap(collider, overlap, true);
                    continue;
                }

                cleanupCursor++;
            }
        }

        private void OnDisable()
        {
            UnregisterMaintenance();
            ClearMembers();
        }

        private void RemoveColliderOverlap(Collider collider, ColliderOverlap overlap, bool removeAll)
        {
            int removedCount = removeAll ? overlap.overlapCount : 1;
            overlap.overlapCount -= removedCount;
            if (overlap.overlapCount <= 0)
            {
                colliderOverlaps.Remove(collider);
                RemoveTrackedColliderAt(overlap.cleanupIndex);
            }
            else
            {
                colliderOverlaps[collider] = overlap;
            }

            if (!occupants.TryGetValue(overlap.memberKey, out Occupant occupant))
            {
                ReleaseTrackingCollectionsIfEmpty();
                return;
            }

            occupant.overlapCount -= removedCount;
            if (occupant.overlapCount > 0)
            {
                occupants[overlap.memberKey] = occupant;
                return;
            }

            occupants.Remove(overlap.memberKey);
            NotifyMemberExited(occupant.member);
            ReleaseTrackingCollectionsIfEmpty();
        }

        private void ClearMembers()
        {
            if (occupants == null)
                return;

            foreach (Occupant occupant in occupants.Values)
                NotifyMemberExited(occupant.member);

            occupants.Clear();
            colliderOverlaps.Clear();
            trackedColliders.Clear();
            cleanupCursor = 0;
            occupants = null;
            colliderOverlaps = null;
            trackedColliders = null;
        }

        private void RemoveTrackedColliderAt(int index)
        {
            if (trackedColliders == null)
                return;
            int lastIndex = trackedColliders.Count - 1;
            if (index < 0 || index > lastIndex)
                return;

            bool removedBeforeCursor = index < cleanupCursor;
            Collider moved = trackedColliders[lastIndex];
            trackedColliders[index] = moved;
            trackedColliders.RemoveAt(lastIndex);
            if (trackedColliders.Count == 0)
                UnregisterMaintenance();

            if (index < trackedColliders.Count
                && colliderOverlaps.TryGetValue(moved, out ColliderOverlap movedOverlap))
            {
                movedOverlap.cleanupIndex = index;
                colliderOverlaps[moved] = movedOverlap;
            }

            if (removedBeforeCursor)
                cleanupCursor--;
            if (cleanupCursor > trackedColliders.Count)
                cleanupCursor = trackedColliders.Count;
        }

        private void NotifyMemberEntered(ESZoneMember member)
        {
            TryNotifyProfileEntered(member);
        }

        private void NotifyMemberExited(ESZoneMember member)
        {
            profile?.ExitMember(member);
        }

        private void TryNotifyProfileEntered(ESZoneMember member)
        {
            if (profile == null || !profile.isActiveAndEnabled)
                return;

            try
            {
                if (!profile.TryEnterMember(member, out string error)
                    && profile.Settings.LogExtensionFailures)
                {
                    Debug.LogWarning(
                        "[ESZone] Profile 处理成员进入失败: " + error,
                        profile);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, profile);
            }
        }

        private static bool TryResolveMember(
            Collider collider,
            out UnityEngine.Object key,
            out ESZoneMember member)
        {
            Core core = collider.GetComponentInParent<Core>();
            if (core != null)
            {
                key = core;
                member = new ESZoneMember(
                    core,
                    core.gameObject,
                    core,
                    core as IESMotionInfluenceReceiver);
                return true;
            }

            Rigidbody attachedBody = collider.attachedRigidbody;
            if (attachedBody != null)
            {
                VehicleController vehicle = attachedBody.GetComponentInParent<VehicleController>();
                if (vehicle != null)
                {
                    key = vehicle;
                    member = new ESZoneMember(vehicle, vehicle.gameObject, null, vehicle);
                    return true;
                }

                key = attachedBody;
                member = new ESZoneMember(attachedBody, attachedBody.gameObject, null, null);
                return true;
            }

            GameObject colliderObject = collider.gameObject;
            VehicleController fallbackVehicle = collider.GetComponentInParent<VehicleController>();
            if (fallbackVehicle != null)
            {
                key = fallbackVehicle;
                member = new ESZoneMember(
                    fallbackVehicle,
                    fallbackVehicle.gameObject,
                    null,
                    fallbackVehicle);
                return true;
            }

            key = colliderObject;
            member = new ESZoneMember(colliderObject, colliderObject, null, null);
            return true;
        }

        private void EnsureTrackingCollections()
        {
            occupants ??= new Dictionary<UnityEngine.Object, Occupant>(InitialMemberCapacity);
            colliderOverlaps ??= new Dictionary<Collider, ColliderOverlap>(InitialColliderCapacity);
            trackedColliders ??= new List<Collider>(InitialColliderCapacity);
        }

        private void ReleaseTrackingCollectionsIfEmpty()
        {
            if (occupants == null || occupants.Count != 0
                || colliderOverlaps == null || colliderOverlaps.Count != 0
                || trackedColliders == null || trackedColliders.Count != 0)
                return;

            cleanupCursor = 0;
            occupants = null;
            colliderOverlaps = null;
            trackedColliders = null;
        }

        private void RegisterMaintenance()
        {
            if (!maintenanceRegistered)
            {
                ESZoneMaintenance.Register(this);
                maintenanceRegistered = true;
            }
        }

        private void UnregisterMaintenance()
        {
            if (!maintenanceRegistered)
                return;
            ESZoneMaintenance.Unregister(this);
            maintenanceRegistered = false;
        }
    }

    internal static class ESZoneMaintenance
    {
        private const int ZoneBudgetPerFrame = 64;
        private static readonly List<ESZone> Zones = new List<ESZone>(32);
        private static readonly ESZone[] TickBuffer = new ESZone[ZoneBudgetPerFrame];
        private static int cursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                if (Zones[i] == null)
                    Zones.RemoveAt(i);
            }
            Array.Clear(TickBuffer, 0, TickBuffer.Length);
            cursor = 0;
        }

        internal static void Register(ESZone zone)
        {
            if (zone != null && !Zones.Contains(zone))
                Zones.Add(zone);
        }

        internal static void Unregister(ESZone zone)
        {
            int index = Zones.IndexOf(zone);
            if (index < 0)
                return;

            // Registration churn is rare. Ordered removal keeps the round-robin cursor
            // deterministic even when one zone disables another during maintenance.
            Zones.RemoveAt(index);
            if (cursor > index)
                cursor--;
            if (cursor >= Zones.Count)
                cursor = 0;
        }

        internal static void Tick()
        {
            int scheduledCount = Mathf.Min(ZoneBudgetPerFrame, Zones.Count);
            for (int i = 0; i < scheduledCount; i++)
            {
                if (cursor >= Zones.Count)
                    cursor = 0;

                TickBuffer[i] = Zones[cursor++];
            }

            for (int i = 0; i < scheduledCount; i++)
            {
                ESZone zone = TickBuffer[i];
                TickBuffer[i] = null;
                if (zone != null && zone.isActiveAndEnabled)
                    zone.RunMaintenance();
            }
        }
    }
}
