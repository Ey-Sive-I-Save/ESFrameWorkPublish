using System;
using UnityEngine;

namespace ES
{
    public static class EntityEquipmentAnimationEvents
    {
        public const string Commit = "Equipment.Commit";
        public const string Complete = "Equipment.Complete";
        public const string Cancel = "Equipment.Cancel";
    }

    public static class EntityEquipmentSocketKeys
    {
        public const string MainHandSocket = "MainHandSocket";
        public const string OffHandSocket = "OffHandSocket";
        public const string PrimaryBackSocket = "PrimaryBackSocket";
        public const string SecondaryBackSocket = "SecondaryBackSocket";
        public const string HipSocket = "HipSocket";
        public const string TemporaryHandSocket = "TemporaryHandSocket";
    }

    public enum EntityEquipmentTransitionSignal : byte
    {
        None = 0,
        Committed = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum EntityEquipmentAttachmentPose : byte
    {
        None = 0,
        MainHand = 1,
        OffHand = 2,
        PrimaryBack = 3,
        SecondaryBack = 4,
        Hip = 5,
        TemporaryHand = 6
    }

    public enum EntityEquipmentVisibilityState : byte
    {
        Hidden = 0,
        Visible = 1,
        FadingIn = 2,
        FadingOut = 3
    }

    public enum EntityEquipmentTransitionPhase : byte
    {
        Idle = 0,
        Equipping = 1,
        Holstering = 2,
        Switching = 3
    }

    [Serializable]
    public readonly struct EntityEquipmentTransitionToken : IEquatable<EntityEquipmentTransitionToken>
    {
        public readonly int transitionId;
        public readonly int entityGeneration;
        public readonly int mappingGeneration;
        public readonly int targetRevision;

        public bool IsValid => transitionId > 0
            && entityGeneration > 0
            && mappingGeneration > 0
            && targetRevision > 0;

        public EntityEquipmentTransitionToken(
            int transitionId,
            int entityGeneration,
            int mappingGeneration,
            int targetRevision)
        {
            this.transitionId = transitionId;
            this.entityGeneration = entityGeneration;
            this.mappingGeneration = mappingGeneration;
            this.targetRevision = targetRevision;
        }

        public bool Equals(EntityEquipmentTransitionToken other)
        {
            return transitionId == other.transitionId
                && entityGeneration == other.entityGeneration
                && mappingGeneration == other.mappingGeneration
                && targetRevision == other.targetRevision;
        }

        public override bool Equals(object obj) =>
            obj is EntityEquipmentTransitionToken other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = transitionId;
                hash = (hash * 397) ^ entityGeneration;
                hash = (hash * 397) ^ mappingGeneration;
                return (hash * 397) ^ targetRevision;
            }
        }

        public static bool operator ==(
            EntityEquipmentTransitionToken left,
            EntityEquipmentTransitionToken right) => left.Equals(right);

        public static bool operator !=(
            EntityEquipmentTransitionToken left,
            EntityEquipmentTransitionToken right) => !left.Equals(right);

        public override string ToString()
        {
            return IsValid
                ? transitionId + ":" + entityGeneration + ":" + mappingGeneration + ":" + targetRevision
                : "Invalid";
        }
    }

    public readonly struct EntityEquipmentAttachmentOperation
    {
        public readonly Transform viewRoot;
        public readonly EntityWeaponBinding binding;
        public readonly EntityEquipmentAttachmentPose targetPose;
        public readonly EntityEquipmentVisibilityState targetVisibility;

        public bool IsConfigured => viewRoot != null && binding != null;
        public bool HasAnyReference => viewRoot != null || binding != null;

        public EntityEquipmentAttachmentOperation(
            Transform viewRoot,
            EntityWeaponBinding binding,
            EntityEquipmentAttachmentPose targetPose,
            EntityEquipmentVisibilityState targetVisibility)
        {
            this.viewRoot = viewRoot;
            this.binding = binding;
            this.targetPose = targetPose;
            this.targetVisibility = targetVisibility;
        }
    }

    public readonly struct EntityEquipmentTransitionRequest
    {
        public readonly EntityEquipmentAttachmentOperation primary;
        public readonly EntityEquipmentAttachmentOperation secondary;
        public readonly EntityEquipmentTransitionPhase phase;
        public readonly int targetRevision;
        public readonly float timeoutSeconds;

        public int OperationCount => secondary.IsConfigured ? 2 : 1;

        public EntityEquipmentTransitionRequest(
            in EntityEquipmentAttachmentOperation primary,
            EntityEquipmentTransitionPhase phase,
            int targetRevision,
            float timeoutSeconds = 3f)
        {
            this.primary = primary;
            secondary = default;
            this.phase = phase;
            this.targetRevision = targetRevision;
            this.timeoutSeconds = timeoutSeconds;
        }

        public EntityEquipmentTransitionRequest(
            in EntityEquipmentAttachmentOperation primary,
            in EntityEquipmentAttachmentOperation secondary,
            EntityEquipmentTransitionPhase phase,
            int targetRevision,
            float timeoutSeconds = 3f)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.phase = phase;
            this.targetRevision = targetRevision;
            this.timeoutSeconds = timeoutSeconds;
        }

        public bool TryGetOperation(int index, out EntityEquipmentAttachmentOperation operation)
        {
            if (index == 0)
            {
                operation = primary;
                return true;
            }
            if (index == 1 && secondary.IsConfigured)
            {
                operation = secondary;
                return true;
            }

            operation = default;
            return false;
        }
    }
}
