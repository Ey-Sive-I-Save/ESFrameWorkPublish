using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public static class EntityEquipmentSocketKeys
    {
        public const string WeaponSocket = "WeaponSocket";
        public const string MainHandSocket = "MainHandSocket";
        public const string OffHandSocket = "OffHandSocket";
        public const string PrimaryBackSocket = "PrimaryBackSocket";
        public const string SecondaryBackSocket = "SecondaryBackSocket";
        public const string HipSocket = "HipSocket";
        public const string TemporaryHandSocket = "TemporaryHandSocket";
    }

    public enum EntityEquipmentAttachmentTarget
    {
        MainHand,
        OffHand,
        PrimaryBack,
        SecondaryBack,
        Hip,
        TemporaryHand
    }

    [Serializable]
    public readonly struct EquipmentTransitionStamp : IEquatable<EquipmentTransitionStamp>
    {
        public int TransitionId { get; }
        public int EntityGeneration { get; }
        public int MappingGeneration { get; }
        public int SlotRevision { get; }

        public bool IsValid => TransitionId > 0
            && EntityGeneration > 0
            && MappingGeneration > 0
            && SlotRevision > 0;

        public EquipmentTransitionStamp(
            int transitionId,
            int entityGeneration,
            int mappingGeneration,
            int slotRevision)
        {
            TransitionId = transitionId;
            EntityGeneration = entityGeneration;
            MappingGeneration = mappingGeneration;
            SlotRevision = slotRevision;
        }

        public bool Equals(EquipmentTransitionStamp other)
        {
            return TransitionId == other.TransitionId
                && EntityGeneration == other.EntityGeneration
                && MappingGeneration == other.MappingGeneration
                && SlotRevision == other.SlotRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is EquipmentTransitionStamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TransitionId;
                hash = (hash * 397) ^ EntityGeneration;
                hash = (hash * 397) ^ MappingGeneration;
                hash = (hash * 397) ^ SlotRevision;
                return hash;
            }
        }

        public override string ToString()
        {
            return IsValid
                ? TransitionId + ":" + EntityGeneration + ":" + MappingGeneration + ":" + SlotRevision
                : "Invalid";
        }
    }

    /// <summary>
    /// 角色侧装备挂点唯一入口。角色挂点只来自 Entity.TransformMapping；武器自身偏移与 IK 仍归 EntityWeaponBinding。
    /// </summary>
    [Serializable, TypeRegistryItem("装备挂点模块")]
    public sealed class EntityEquipmentAttachmentModule : EntityBasicModuleBase
    {
        [LabelText("允许旧挂点与根节点回退")]
        [Tooltip("迁移期兼容开关。关闭后，除武器显式挂点外，角色挂点必须来自 EntityTransformMapping。")]
        public bool allowEntityRootFallback = true;

        [NonSerialized] private EntityTransformMapping mapping;
        [NonSerialized] private int cachedMappingGeneration;
        [NonSerialized] private Transform weaponSocket;
        [NonSerialized] private Transform mainHandSocket;
        [NonSerialized] private Transform rightHand;
        [NonSerialized] private Transform offHandSocket;
        [NonSerialized] private Transform primaryBackSocket;
        [NonSerialized] private Transform secondaryBackSocket;
        [NonSerialized] private Transform hipSocket;
        [NonSerialized] private Transform temporaryHandSocket;
        [NonSerialized] private int nextTransitionId;
        [NonSerialized] private int slotRevision = 1;
        [NonSerialized] private EquipmentTransitionStamp activeStamp;
        [NonSerialized] private Transform activeMount;

        public int SlotRevision => slotRevision;
        public EquipmentTransitionStamp ActiveStamp => activeStamp;

        public override void Start()
        {
            base.Start();
            TryRefreshMappingCache(out _);
        }

        public void OnPoolSpawned()
        {
            InvalidateTransitions();
            TryRefreshMappingCache(out _);
        }

        public void OnPoolDespawned()
        {
            InvalidateTransitions();
            EntityTransformMapping current = MyCore != null ? MyCore.EnsureTransformMapping() : null;
            current?.ClearDynamic();
            ClearCache();
        }

        public void NotifySlotsChanged()
        {
            AdvanceSlotRevision();
            ClearActiveTransition();
        }

        public bool TryBeginTransition(
            EntityEquipmentAttachmentTarget target,
            Transform explicitMount,
            Transform legacyFallback,
            out EquipmentTransitionStamp stamp,
            out string error)
        {
            stamp = default;
            if (!TryRefreshMappingCache(out error))
                return false;
            if (!TryResolveTargetFromCache(target, explicitMount, legacyFallback, out Transform mount, out error))
                return false;

            nextTransitionId++;
            if (nextTransitionId <= 0)
                nextTransitionId = 1;

            stamp = new EquipmentTransitionStamp(
                nextTransitionId,
                MyCore.LifecycleGeneration,
                cachedMappingGeneration,
                slotRevision);
            activeStamp = stamp;
            activeMount = mount;
            return true;
        }

        public bool IsCurrent(in EquipmentTransitionStamp stamp)
        {
            if (!stamp.IsValid || MyCore == null || !activeStamp.Equals(stamp))
                return false;

            EntityTransformMapping current = MyCore.EnsureTransformMapping();
            return current != null
                && current.TransformMappings.IsValid
                && stamp.EntityGeneration == MyCore.LifecycleGeneration
                && stamp.MappingGeneration == current.TransformMappings.Generation
                && stamp.SlotRevision == slotRevision;
        }

        public bool TryResolveTarget(
            EntityEquipmentAttachmentTarget target,
            Transform explicitMount,
            Transform legacyFallback,
            out Transform mount,
            out string error)
        {
            mount = null;
            if (!TryRefreshMappingCache(out error))
                return false;

            return TryResolveTargetFromCache(target, explicitMount, legacyFallback, out mount, out error);
        }

        private bool TryResolveTargetFromCache(
            EntityEquipmentAttachmentTarget target,
            Transform explicitMount,
            Transform legacyFallback,
            out Transform mount,
            out string error)
        {
            mount = null;
            if (explicitMount != null)
            {
                mount = explicitMount;
                error = null;
                return true;
            }

            switch (target)
            {
                case EntityEquipmentAttachmentTarget.MainHand:
                    mount = mainHandSocket != null
                        ? mainHandSocket
                        : weaponSocket != null ? weaponSocket : rightHand;
                    break;
                case EntityEquipmentAttachmentTarget.OffHand:
                    mount = offHandSocket;
                    break;
                case EntityEquipmentAttachmentTarget.PrimaryBack:
                    mount = primaryBackSocket;
                    break;
                case EntityEquipmentAttachmentTarget.SecondaryBack:
                    mount = secondaryBackSocket != null ? secondaryBackSocket : primaryBackSocket;
                    break;
                case EntityEquipmentAttachmentTarget.Hip:
                    mount = hipSocket;
                    break;
                case EntityEquipmentAttachmentTarget.TemporaryHand:
                    mount = temporaryHandSocket != null ? temporaryHandSocket : mainHandSocket;
                    break;
            }

            if (mount == null && allowEntityRootFallback)
                mount = legacyFallback;
            if (mount == null && allowEntityRootFallback && MyCore != null)
                mount = MyCore.transform;

            if (mount != null)
            {
                error = null;
                return true;
            }

            error = "Equipment attachment target is missing: " + target + ".";
            return false;
        }

        public bool TryCommit(
            in EquipmentTransitionStamp stamp,
            Transform equipmentRoot,
            bool resetLocalPose,
            out string error)
        {
            if (equipmentRoot == null)
            {
                error = "Equipment root is null.";
                return false;
            }

            if (!IsCurrent(stamp))
            {
                error = "Equipment transition stamp is stale before commit: " + stamp + ".";
                return false;
            }

            Transform mount = activeMount;
            if (mount == null)
            {
                error = "Equipment transition has no cached mount: " + stamp + ".";
                return false;
            }

            Transform previousParent = equipmentRoot.parent;
            Vector3 previousLocalPosition = equipmentRoot.localPosition;
            Quaternion previousLocalRotation = equipmentRoot.localRotation;
            Vector3 previousLocalScale = equipmentRoot.localScale;
            equipmentRoot.SetParent(mount, false);
            if (resetLocalPose)
            {
                equipmentRoot.localPosition = Vector3.zero;
                equipmentRoot.localRotation = Quaternion.identity;
            }

            if (!IsCurrent(stamp))
            {
                equipmentRoot.SetParent(previousParent, false);
                equipmentRoot.localPosition = previousLocalPosition;
                equipmentRoot.localRotation = previousLocalRotation;
                equipmentRoot.localScale = previousLocalScale;
                error = "Equipment transition stamp changed during commit: " + stamp + ".";
                return false;
            }

            ClearActiveTransition();
            error = null;
            return true;
        }

        public bool TryAttach(
            EntityEquipmentAttachmentTarget target,
            Transform equipmentRoot,
            Transform explicitMount,
            Transform legacyFallback,
            bool resetLocalPose,
            out EquipmentTransitionStamp stamp,
            out string error)
        {
            stamp = default;
            if (!TryBeginTransition(target, explicitMount, legacyFallback, out stamp, out error))
                return false;
            return TryCommit(stamp, equipmentRoot, resetLocalPose, out error);
        }

        private bool TryRefreshMappingCache(out string error)
        {
            if (MyCore == null)
            {
                error = "Equipment attachment module has no Entity owner.";
                return false;
            }

            EntityTransformMapping current = MyCore.EnsureTransformMapping();
            if (current == null)
            {
                error = "EntityTransformMapping is missing.";
                return false;
            }

            EntityTransformMap map = current.TransformMappings;
            if (!map.IsValid)
            {
                error = "EntityTransformMap is invalid: " + map.LastConflict.Message;
                return false;
            }

            if (ReferenceEquals(mapping, current) && cachedMappingGeneration == map.Generation)
            {
                error = null;
                return true;
            }

            mapping = current;
            cachedMappingGeneration = map.Generation;
            weaponSocket = Resolve(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket);
            mainHandSocket = map.Resolve(EntityEquipmentSocketKeys.MainHandSocket);
            rightHand = map.Resolve(DefaultTransformKey.RightHand);
            offHandSocket = Resolve(DefaultTransformKey.LeftHand, EntityEquipmentSocketKeys.OffHandSocket);
            primaryBackSocket = map.Resolve(EntityEquipmentSocketKeys.PrimaryBackSocket);
            secondaryBackSocket = map.Resolve(EntityEquipmentSocketKeys.SecondaryBackSocket);
            hipSocket = Resolve(DefaultTransformKey.Hip, EntityEquipmentSocketKeys.HipSocket);
            temporaryHandSocket = map.Resolve(EntityEquipmentSocketKeys.TemporaryHandSocket);
            ClearActiveTransition();
            error = null;
            return true;
        }

        private Transform Resolve(DefaultTransformKey defaultKey, string stringKey)
        {
            Transform value = mapping.Resolve(stringKey);
            return value != null ? value : mapping.Resolve(defaultKey);
        }

        private void InvalidateTransitions()
        {
            AdvanceSlotRevision();
            ClearActiveTransition();
        }

        private void ClearActiveTransition()
        {
            activeStamp = default;
            activeMount = null;
        }

        private void AdvanceSlotRevision()
        {
            unchecked
            {
                slotRevision++;
                if (slotRevision <= 0)
                    slotRevision = 1;
            }
        }

        private void ClearCache()
        {
            mapping = null;
            cachedMappingGeneration = 0;
            weaponSocket = null;
            mainHandSocket = null;
            rightHand = null;
            offHandSocket = null;
            primaryBackSocket = null;
            secondaryBackSocket = null;
            hipSocket = null;
            temporaryHandSocket = null;
            activeMount = null;
        }
    }
}
