using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESBuffConfigKey : ESGameCoreConfigKey<ESBuffEnumKey>
    {
        public static implicit operator ESBuffConfigKey(ESBuffEnumKey value)
            => new ESBuffConfigKey { enumKey = value };

        public static implicit operator ESBuffConfigKey(string value)
            => new ESBuffConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESBuffRuntimeData : ESGameCoreRuntimeData
    {
        private readonly BuffSharedData ownedDefaultSharedData = new BuffSharedData();
        private readonly BuffVariableData ownedDefaultVariableData = new BuffVariableData();
        private readonly ESBuffConfigKey ownedDefaultKey;
        private readonly ESTagGrantConfig ownedTagGrants;
        private readonly List<ESTagStableReference> ownedGrantedTags;
        private readonly ESTagConditionConfig ownedTagCondition;
        private readonly List<ESTagStableReference> ownedRequiredTags;
        private readonly List<ESTagStableReference> ownedRequiredAnyTags;
        private readonly List<ESTagStableReference> ownedForbiddenTags;
        private readonly List<ESGameTag> ownedRequiredCore;
        private readonly List<ESGameTag> ownedRequiredAnyCore;
        private readonly List<ESGameTag> ownedForbiddenCore;
        private readonly List<string> ownedRequiredExtensions;
        private readonly List<string> ownedRequiredAnyExtensions;
        private readonly List<string> ownedForbiddenExtensions;
        private readonly List<ESBuffFloatValueChangeBinding> ownedFloatChanges;
        private readonly List<ESBuffPermitValueChangeBinding> ownedPermitChanges;

        public ESBuffRuntimeData()
        {
            ownedDefaultKey = ownedDefaultSharedData.key;
            ownedTagGrants = ownedDefaultSharedData.tagGrants;
            ownedGrantedTags = ownedTagGrants.tags;
            ownedTagCondition = ownedDefaultSharedData.applyTargetTagCondition;
            ownedRequiredTags = ownedTagCondition.required;
            ownedRequiredAnyTags = ownedTagCondition.requiredAny;
            ownedForbiddenTags = ownedTagCondition.forbidden;
            ownedRequiredCore = ownedTagCondition.requiredCore;
            ownedRequiredAnyCore = ownedTagCondition.requiredAnyCore;
            ownedForbiddenCore = ownedTagCondition.forbiddenCore;
            ownedRequiredExtensions = ownedTagCondition.requiredExtensions;
            ownedRequiredAnyExtensions = ownedTagCondition.requiredAnyExtensions;
            ownedForbiddenExtensions = ownedTagCondition.forbiddenExtensions;
            ownedFloatChanges = ownedDefaultSharedData.floatChanges;
            ownedPermitChanges = ownedDefaultSharedData.permitChanges;
        }

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public BuffDefinitionDataInfo soSource;
        public BuffSharedData sharedData;
        public BuffVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;

        internal BuffSharedData PrepareDefaultSharedData()
        {
            RestoreDefaultOwnership();
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal BuffVariableData PrepareDefaultVariableData()
        {
            ownedDefaultVariableData.ResetToDefaults();
            return ownedDefaultVariableData;
        }

        internal bool OwnsDefaultSharedData(BuffSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        internal bool OwnsDefaultVariableData(BuffVariableData value)
            => ReferenceEquals(value, ownedDefaultVariableData);

        internal bool OwnsCompleteDefaultGraph()
        {
            return ReferenceEquals(ownedDefaultSharedData.key, ownedDefaultKey)
                && ReferenceEquals(ownedDefaultSharedData.tagGrants, ownedTagGrants)
                && ReferenceEquals(ownedTagGrants.tags, ownedGrantedTags)
                && ReferenceEquals(ownedDefaultSharedData.applyTargetTagCondition, ownedTagCondition)
                && ReferenceEquals(ownedTagCondition.required, ownedRequiredTags)
                && ReferenceEquals(ownedTagCondition.requiredAny, ownedRequiredAnyTags)
                && ReferenceEquals(ownedTagCondition.forbidden, ownedForbiddenTags)
                && ReferenceEquals(ownedTagCondition.requiredCore, ownedRequiredCore)
                && ReferenceEquals(ownedTagCondition.requiredAnyCore, ownedRequiredAnyCore)
                && ReferenceEquals(ownedTagCondition.forbiddenCore, ownedForbiddenCore)
                && ReferenceEquals(ownedTagCondition.requiredExtensions, ownedRequiredExtensions)
                && ReferenceEquals(ownedTagCondition.requiredAnyExtensions, ownedRequiredAnyExtensions)
                && ReferenceEquals(ownedTagCondition.forbiddenExtensions, ownedForbiddenExtensions)
                && ReferenceEquals(ownedDefaultSharedData.floatChanges, ownedFloatChanges)
                && ReferenceEquals(ownedDefaultSharedData.permitChanges, ownedPermitChanges);
        }

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            sharedData = null;
            defaultVariableData = null;
            prefab = null;
            extraAsset = null;
            RestoreDefaultOwnership();
            ownedDefaultSharedData.ResetToDefaults();
            ownedDefaultVariableData.ResetToDefaults();
        }

        private void RestoreDefaultOwnership()
        {
            ownedDefaultSharedData.key = ownedDefaultKey;
            ownedDefaultSharedData.tagGrants = ownedTagGrants;
            ownedTagGrants.tags = ownedGrantedTags;
            ownedDefaultSharedData.applyTargetTagCondition = ownedTagCondition;
            ownedTagCondition.required = ownedRequiredTags;
            ownedTagCondition.requiredAny = ownedRequiredAnyTags;
            ownedTagCondition.forbidden = ownedForbiddenTags;
            ownedTagCondition.requiredCore = ownedRequiredCore;
            ownedTagCondition.requiredAnyCore = ownedRequiredAnyCore;
            ownedTagCondition.forbiddenCore = ownedForbiddenCore;
            ownedTagCondition.requiredExtensions = ownedRequiredExtensions;
            ownedTagCondition.requiredAnyExtensions = ownedRequiredAnyExtensions;
            ownedTagCondition.forbiddenExtensions = ownedForbiddenExtensions;
            ownedDefaultSharedData.floatChanges = ownedFloatChanges;
            ownedDefaultSharedData.permitChanges = ownedPermitChanges;
        }
    }

    /// <summary>Buff 领域表。调用方只提供 Buff 配置，不需要了解 ESBuffRuntimeData 的构造细节。</summary>
    public sealed class ESBuffConfigKeyTable : ESGameCoreConfigKeyTable<ESBuffRuntimeData>
    {
        public ESBuffConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Buff") { }

        public int Inject(ESBuffEnumKey key, ESBuffRuntimeData data, string debugName = null)
            => CommitRetained((ESBuffConfigKey)key, data, debugName);

        public bool TryInject(
            ESBuffEnumKey key,
            ESBuffRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESBuffConfigKey)key, data, out runtimeKey, debugName);

        /// <summary>注入现成权威数据。Table 只持有引用，不修改也不回收 Shared/VariableData。</summary>
        public int InjectWith(
            ESBuffConfigKey key,
            BuffSharedData sharedData,
            BuffVariableData defaultVariableData,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key, nameof(InjectWith));
            ValidateAuthoritativeData(key, sharedData, defaultVariableData, nameof(InjectWith));
            ESBuffRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESBuffConfigKey key,
            BuffSharedData sharedData,
            BuffVariableData defaultVariableData,
            out int runtimeKey,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (!IsAuthoritativeDataValid(key, sharedData, defaultVariableData))
                return false;

            if (!TryAcquireRetained(key, out ESBuffRuntimeData runtimeData))
                return false;
            try
            {
                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        /// <summary>
        /// 从 Buff 领域默认值构造 Table 独占的次级运行时定义，再应用可空覆盖。
        /// 不接收也不会改写外部 SharedData；Remove/Clear/Rebuild 时整条定义自动回池。
        /// </summary>
        public int InjectWithDefaults(
            ESBuffConfigKey key,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null,
            float? duration = null,
            string buffGroup = null,
            int? strength = null,
            ESBuffSourceIsolationMode? sourceIsolationMode = null,
            ESBuffStackMode? stackMode = null,
            ESBuffTimeRefreshMode? timeRefreshMode = null,
            ESBuffGroupConflictMode? groupConflictMode = null,
            int? maxStack = null,
            ESBuffTickMode? tickMode = null,
            float? tickInterval = null,
            int? initialStackCount = null,
            float? initialRemainingTime = null,
            float? initialElapsedTime = null,
            float? initialTickAccumulator = null,
            int? initialSourceKey = null,
            ESDataFiller<BuffSharedData> fillShared = null,
            ESDataFiller<BuffVariableData> fillVariable = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESBuffRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                ResolveDefaults(
                    runtimeData, key,
                    duration, buffGroup, strength, sourceIsolationMode, stackMode, timeRefreshMode,
                    groupConflictMode, maxStack, tickMode, tickInterval,
                    initialStackCount, initialRemainingTime, initialElapsedTime, initialTickAccumulator, initialSourceKey,
                    fillShared, fillVariable,
                    out BuffSharedData sharedData, out BuffVariableData defaultVariableData);
                ValidateAuthoritativeData(key, sharedData, defaultVariableData, nameof(InjectWithDefaults));
                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWithDefaults(
            ESBuffConfigKey key,
            out int runtimeKey,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null,
            float? duration = null,
            string buffGroup = null,
            int? strength = null,
            ESBuffSourceIsolationMode? sourceIsolationMode = null,
            ESBuffStackMode? stackMode = null,
            ESBuffTimeRefreshMode? timeRefreshMode = null,
            ESBuffGroupConflictMode? groupConflictMode = null,
            int? maxStack = null,
            ESBuffTickMode? tickMode = null,
            float? tickInterval = null,
            int? initialStackCount = null,
            float? initialRemainingTime = null,
            float? initialElapsedTime = null,
            float? initialTickAccumulator = null,
            int? initialSourceKey = null,
            ESDataFiller<BuffSharedData> fillShared = null,
            ESDataFiller<BuffVariableData> fillVariable = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESBuffRuntimeData runtimeData))
                return false;
            try
            {
                ResolveDefaults(
                    runtimeData, key,
                    duration, buffGroup, strength, sourceIsolationMode, stackMode, timeRefreshMode,
                    groupConflictMode, maxStack, tickMode, tickInterval,
                    initialStackCount, initialRemainingTime, initialElapsedTime, initialTickAccumulator, initialSourceKey,
                    fillShared, fillVariable,
                    out BuffSharedData sharedData, out BuffVariableData defaultVariableData);
                if (!IsAuthoritativeDataValid(key, sharedData, defaultVariableData))
                {
                    AbandonRetained(runtimeData);
                    return false;
                }

                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static ESBuffRuntimeData CreateRuntimeData(
            ESBuffRuntimeData runtimeData,
            ESBuffConfigKey key,
            BuffSharedData sharedData,
            BuffVariableData defaultVariableData,
            string displayName,
            GameObject prefab,
            UnityEngine.Object extraAsset,
            string sourcePackage,
            string version)
        {
            string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            runtimeData.keyName = keyName;
            runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
            runtimeData.sourcePackage = sourcePackage ?? string.Empty;
            runtimeData.version = version ?? string.Empty;
            runtimeData.sharedData = sharedData;
            runtimeData.defaultVariableData = defaultVariableData;
            runtimeData.prefab = prefab;
            runtimeData.extraAsset = extraAsset;
            return runtimeData;
        }

        private static void ResolveDefaults(
            ESBuffRuntimeData runtimeData,
            ESBuffConfigKey key,
            float? duration,
            string buffGroup,
            int? strength,
            ESBuffSourceIsolationMode? sourceIsolationMode,
            ESBuffStackMode? stackMode,
            ESBuffTimeRefreshMode? timeRefreshMode,
            ESBuffGroupConflictMode? groupConflictMode,
            int? maxStack,
            ESBuffTickMode? tickMode,
            float? tickInterval,
            int? initialStackCount,
            float? initialRemainingTime,
            float? initialElapsedTime,
            float? initialTickAccumulator,
            int? initialSourceKey,
            ESDataFiller<BuffSharedData> fillShared,
            ESDataFiller<BuffVariableData> fillVariable,
            out BuffSharedData resolvedShared,
            out BuffVariableData resolvedVariable)
        {
            resolvedShared = runtimeData.PrepareDefaultSharedData();
            resolvedVariable = runtimeData.PrepareDefaultVariableData();
            ESBuffConfigKey ownedKey = resolvedShared.key;
            CopyKey(key, ownedKey);

            if (duration.HasValue) resolvedShared.duration = duration.Value;
            if (buffGroup != null) resolvedShared.buffGroup = buffGroup;
            if (strength.HasValue) resolvedShared.strength = strength.Value;
            if (sourceIsolationMode.HasValue) resolvedShared.sourceIsolationMode = sourceIsolationMode.Value;
            if (stackMode.HasValue) resolvedShared.stackMode = stackMode.Value;
            if (timeRefreshMode.HasValue) resolvedShared.timeRefreshMode = timeRefreshMode.Value;
            if (groupConflictMode.HasValue) resolvedShared.groupConflictMode = groupConflictMode.Value;
            if (maxStack.HasValue) resolvedShared.maxStack = maxStack.Value;
            if (tickMode.HasValue) resolvedShared.tickMode = tickMode.Value;
            if (tickInterval.HasValue) resolvedShared.tickInterval = tickInterval.Value;

            if (initialStackCount.HasValue) resolvedVariable.stackCount = initialStackCount.Value;
            if (initialRemainingTime.HasValue) resolvedVariable.remainingTime = initialRemainingTime.Value;
            if (initialElapsedTime.HasValue) resolvedVariable.elapsedTime = initialElapsedTime.Value;
            if (initialTickAccumulator.HasValue) resolvedVariable.tickAccumulator = initialTickAccumulator.Value;
            if (initialSourceKey.HasValue) resolvedVariable.sourceKey = initialSourceKey.Value;

            fillShared?.Invoke(ref resolvedShared);
            fillVariable?.Invoke(ref resolvedVariable);

            if (!runtimeData.OwnsDefaultSharedData(resolvedShared))
                throw new InvalidOperationException("Buff InjectWithDefaults 的 fillShared 不得替换 Table 自有 SharedData 实例。");
            if (!runtimeData.OwnsDefaultVariableData(resolvedVariable))
                throw new InvalidOperationException("Buff InjectWithDefaults 的 fillVariable 不得替换 Table 自有 VariableData 实例。");
            if (!ReferenceEquals(resolvedShared.key, ownedKey) || !runtimeData.OwnsCompleteDefaultGraph())
                throw new InvalidOperationException("Buff InjectWithDefaults 的 fillShared 不得替换 Table 自有 Key、集合或 Tag 条件容器。");
        }

        private static bool IsAuthoritativeDataValid(
            ESBuffConfigKey key,
            BuffSharedData sharedData,
            BuffVariableData defaultVariableData)
        {
            return key != null
                && key.IsConfigured
                && sharedData != null
                && sharedData.key != null
                && defaultVariableData != null
                && sharedData.key.IsConfigured
                && ESConfigKeyMatch.Matches(
                    key.EnumKeyInt, key.StringKey,
                    sharedData.key.EnumKeyInt, sharedData.key.StringKey)
                && sharedData.TryValidateGameTagConfiguration(out _);
        }

        private static void ValidateKey(ESBuffConfigKey key, string api)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Buff " + api + " 必须提供有效 ConfigKey。");
        }

        private static void ValidateAuthoritativeData(
            ESBuffConfigKey key,
            BuffSharedData sharedData,
            BuffVariableData defaultVariableData,
            string api)
        {
            if (sharedData == null)
                throw new InvalidOperationException("Buff " + api + " 的 SharedData 不能为 null。");
            if (defaultVariableData == null)
                throw new InvalidOperationException("Buff " + api + " 的 VariableData 不能为 null。");
            if (sharedData.key == null || !sharedData.key.IsConfigured ||
                !ESConfigKeyMatch.Matches(
                    key.EnumKeyInt, key.StringKey,
                    sharedData.key.EnumKeyInt, sharedData.key.StringKey))
                throw new InvalidOperationException("Buff " + api + " 的 SharedData Key 必须与显式 ConfigKey 一致。");
            if (!sharedData.TryValidateGameTagConfiguration(out string error))
                throw new InvalidOperationException("Buff " + api + " 配置无效：" + error);
        }

        private static void CopyKey(ESBuffConfigKey source, ESBuffConfigKey target)
        {
            target.enumKey = source.enumKey;
            target.stringKey = source.stringKey;
            target.definitionGuid = source.definitionGuid;
            target.definitionLocalFileId = source.definitionLocalFileId;
            target.definitionTypeName = source.definitionTypeName;
        }
    }
}
