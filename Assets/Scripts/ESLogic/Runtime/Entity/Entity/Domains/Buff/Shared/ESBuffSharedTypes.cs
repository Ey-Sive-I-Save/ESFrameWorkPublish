using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    public enum ESBuffEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }


    public enum ESBuffSourceIsolationMode
    {
        IgnoreSource,
        BySourceSupport,
        BySourceOwner,
        ByCasterEntity,
        ByItem,
        ByCustomSourceId
    }

    public enum ESBuffStackMode
    {
        IndependentInstance,
        StackSameBuff,
        RefreshSameBuff,
        ReplaceSameBuff,
        IgnoreSameBuff
    }

    public enum ESBuffTimeRefreshMode
    {
        KeepRemaining,
        ResetDuration,
        ExtendDuration,
        UseMaxRemaining,
        MergeRemaining
    }

    public enum ESBuffGroupConflictMode
    {
        None,
        ReplaceWeakerInGroup,
        ReplaceLowerOrEqualInGroup,
        RejectIfWeakerInGroup
    }

    public enum ESBuffTickMode
    {
        None,
        EveryFrame,
        FixedInterval,
        StateMachineTime
    }

    public static class ESBuffSourceKeyUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSourceKey(BuffSharedData sharedData, ESOpSupport sourceSupport, Entity casterEntity = null, Item sourceItem = null, int customSourceId = 0)
        {
            if (sharedData == null)
                return 0;

            switch (sharedData.sourceIsolationMode)
            {
                case ESBuffSourceIsolationMode.BySourceSupport:
                    return ReferenceKey(sourceSupport);
                case ESBuffSourceIsolationMode.BySourceOwner:
                    return sourceSupport != null
                        ? sourceSupport.OwnerId != 0 ? sourceSupport.OwnerId : ReferenceKey(sourceSupport.OwnerObject)
                        : 0;
                case ESBuffSourceIsolationMode.ByCasterEntity:
                    return ObjectKey(casterEntity != null ? casterEntity : sourceSupport != null ? sourceSupport.CurrentEntity : null);
                case ESBuffSourceIsolationMode.ByItem:
                    return ObjectKey(sourceItem != null ? sourceItem : sourceSupport != null ? sourceSupport.OwnerItem : null);
                case ESBuffSourceIsolationMode.ByCustomSourceId:
                    return customSourceId;
                default:
                    return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveDefinitionKey(BuffDefinitionDataInfo definition)
        {
            if (definition == null)
                return 0;

            return ResolveDefinitionKey(definition, definition.SharedData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveDefinitionKey(BuffDefinitionDataInfo definition, BuffSharedData sharedData)
        {
            if (sharedData != null && sharedData.key != null)
            {
                int enumKey = sharedData.key.EnumKeyInt;
                if (enumKey != 0)
                    return enumKey;

                ESRuntimeDataModule runtimeData = ESGameManager.RuntimeData;
                if (runtimeData != null && runtimeData.Buffs.TryGetRuntimeKey(sharedData.key.StringKey, out int runtimeKey))
                    return runtimeKey;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ObjectKey(UnityEngine.Object obj)
        {
            return obj != null ? obj.GetInstanceID() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReferenceKey(object obj)
        {
            return obj != null ? RuntimeHelpers.GetHashCode(obj) : 0;
        }
    }

}
