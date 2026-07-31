using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Buff/Shared/ESBuffSharedTypes.cs")]
    public enum ESBuffEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
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

    /// <summary>
    /// Controls how a Buff-backed ValueChange expression is refreshed after its initial application.
    /// The default keeps the current low-cost, apply-once behaviour.
    /// </summary>
    public enum ESBuffValueChangeRefreshMode
    {
        OnApplyOnly,
        OnStackChanged,
        OnLevelChanged,
        /// <summary>
        /// Re-evaluate only after an observed expression dependency or the owning gameplay system
        /// marks this active Buff dirty. Stable frames perform no expression evaluation.
        /// </summary>
        OnDirty,
        EveryTick
    }

    /// <summary>How one explicit Buff operation behaves when its key does not currently resolve to an active instance.</summary>
    public enum ESBuffMissingPolicy : byte
    {
        [InspectorName("不存在时新建")]
        Add,
        [InspectorName("不存在时忽略")]
        Ignore
    }

    public enum ESBuffOperationAction : byte
    {
        [InspectorName("应用 / 重施加")]
        Apply,
        [InspectorName("移除")]
        Remove
    }

    public enum ESBuffStackOperation : byte
    {
        [InspectorName("按 Buff 定义")]
        DefinitionRule,
        [InspectorName("增加")]
        Add,
        [InspectorName("设为")]
        Set
    }

    public enum ESBuffDurationOperation : byte
    {
        [InspectorName("按 Buff 定义")]
        DefinitionRule,
        [InspectorName("重置为定义时间")]
        Reset,
        [InspectorName("增加")]
        Add,
        [InspectorName("设为")]
        Set
    }

    public enum ESBuffLevelOperation : byte
    {
        [InspectorName("保持")]
        Keep,
        [InspectorName("增加")]
        Add,
        [InspectorName("设为")]
        Set
    }

    /// <summary>
    /// One allocation-free, composable Buff operation. It is the explicit path for gameplay that
    /// needs more than the definition's normal Add/reapply rule: refresh a timer, add/set stacks,
    /// add/set duration, change level, or remove. <see cref="ESBuffOperation.Default"/> preserves
    /// the existing AddBuff behaviour exactly.
    /// </summary>
    [Serializable]
    public struct ESBuffOperation
    {
        [LabelText("执行")]
        public ESBuffOperationAction action;

        [ShowIf(nameof(IsApplying)), LabelText("不存在时")]
        public ESBuffMissingPolicy missingPolicy;

        [ShowIf(nameof(IsApplying)), LabelText("层数")]
        public ESBuffStackOperation stackOperation;

        [ShowIf(nameof(ShowsStackValue)), LabelText("层数值")]
        public int stackValue;

        [ShowIf(nameof(IsApplying)), LabelText("持续时间")]
        public ESBuffDurationOperation durationOperation;

        [ShowIf(nameof(ShowsDurationValue)), LabelText("持续时间值")]
        public float durationValue;

        [ShowIf(nameof(IsApplying)), LabelText("等级")]
        public ESBuffLevelOperation levelOperation;

        [ShowIf(nameof(ShowsLevelValue)), LabelText("等级值")]
        public int levelValue;

        private bool IsApplying => action == ESBuffOperationAction.Apply;
        private bool ShowsStackValue => IsApplying && (stackOperation == ESBuffStackOperation.Add || stackOperation == ESBuffStackOperation.Set);
        private bool ShowsDurationValue => IsApplying && (durationOperation == ESBuffDurationOperation.Add || durationOperation == ESBuffDurationOperation.Set);
        private bool ShowsLevelValue => IsApplying && (levelOperation == ESBuffLevelOperation.Add || levelOperation == ESBuffLevelOperation.Set);

        public static ESBuffOperation Default => new ESBuffOperation
        {
            action = ESBuffOperationAction.Apply,
            missingPolicy = ESBuffMissingPolicy.Add,
            stackOperation = ESBuffStackOperation.DefinitionRule,
            durationOperation = ESBuffDurationOperation.DefinitionRule,
            levelOperation = ESBuffLevelOperation.Keep
        };

        public static ESBuffOperation Remove => new ESBuffOperation
        {
            action = ESBuffOperationAction.Remove,
            missingPolicy = ESBuffMissingPolicy.Ignore
        };

        public bool UsesDefinitionReapply => action == ESBuffOperationAction.Apply
                                            && stackOperation == ESBuffStackOperation.DefinitionRule
                                            && durationOperation == ESBuffDurationOperation.DefinitionRule
                                            && levelOperation == ESBuffLevelOperation.Keep;

        public ESBuffOperation OnlyIfPresent()
        {
            missingPolicy = ESBuffMissingPolicy.Ignore;
            return this;
        }

        public ESBuffOperation AddStack(int value)
        {
            if (stackOperation == ESBuffStackOperation.Add)
                stackValue += value;
            else
            {
                stackOperation = ESBuffStackOperation.Add;
                stackValue = value;
            }

            return this;
        }

        public ESBuffOperation SetStack(int value)
        {
            stackOperation = ESBuffStackOperation.Set;
            stackValue = value;
            return this;
        }

        public ESBuffOperation ResetDuration()
        {
            durationOperation = ESBuffDurationOperation.Reset;
            durationValue = 0f;
            return this;
        }

        public ESBuffOperation AddDuration(float value)
        {
            if (durationOperation == ESBuffDurationOperation.Add)
                durationValue += value;
            else
            {
                durationOperation = ESBuffDurationOperation.Add;
                durationValue = value;
            }

            return this;
        }

        public ESBuffOperation SetDuration(float value)
        {
            durationOperation = ESBuffDurationOperation.Set;
            durationValue = value;
            return this;
        }

        public ESBuffOperation AddLevel(int value)
        {
            if (levelOperation == ESBuffLevelOperation.Add)
                levelValue += value;
            else
            {
                levelOperation = ESBuffLevelOperation.Add;
                levelValue = value;
            }

            return this;
        }

        public ESBuffOperation SetLevel(int value)
        {
            levelOperation = ESBuffLevelOperation.Set;
            levelValue = value;
            return this;
        }

        internal int ResolveInitialStack(BuffSharedData data)
        {
            int maxStack = Mathf.Max(1, data != null ? data.maxStack : 1);
            switch (stackOperation)
            {
                case ESBuffStackOperation.Add:
                case ESBuffStackOperation.Set:
                    return Mathf.Clamp(stackValue, 1, maxStack);
                default:
                    return 1;
            }
        }

        internal float ResolveInitialDuration(BuffSharedData data)
        {
            switch (durationOperation)
            {
                case ESBuffDurationOperation.Add:
                case ESBuffDurationOperation.Set:
                    return durationValue;
                default:
                    return data != null ? data.duration : 0f;
            }
        }

        internal int ResolveInitialLevel(BuffSharedData data)
        {
            int maxLevel = Mathf.Max(1, data != null ? data.maxLevel : 1);
            switch (levelOperation)
            {
                case ESBuffLevelOperation.Add:
                case ESBuffLevelOperation.Set:
                    return Mathf.Clamp(levelValue, 1, maxLevel);
                default:
                    return 1;
            }
        }
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
