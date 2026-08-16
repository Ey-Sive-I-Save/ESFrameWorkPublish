using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Item/ESItemConfigKeyData.cs")]
    public enum ESItemEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [Serializable]
    public sealed class ESItemConfigKey : ESGameCoreConfigKey<ESItemEnumKey>
    {
        public static implicit operator ESItemConfigKey(ESItemEnumKey value)
            => new ESItemConfigKey { enumKey = value };

        public static implicit operator ESItemConfigKey(string value)
            => new ESItemConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESItemRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemKind kind;
        public ItemBaseConfig baseConfig;
        public ItemInteractConfig interactConfig;
        public ItemLogicConfig logicConfig;
        public ItemMoveConfig moveConfig;
        public List<ESTagStableReference> tags;
        public List<ESItemFloatValue> floatValues;
        public List<ESItemPermitValue> permitValues;
        public ESWeaponConfigKey weaponKey;
        public ESShotConfigKey shotKey;

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            kind = ItemKind.None;
            baseConfig = null;
            interactConfig = null;
            logicConfig = null;
            moveConfig = null;
            tags = null;
            floatValues = null;
            permitValues = null;
            weaponKey = null;
            shotKey = null;
        }
    }

    /// <summary>
    /// Item 基础定义表。Weapon/Shot 是同一 ItemDataInfo 的独立能力投影，
    /// 其 RuntimeKey 只能由各自强类型表解释。
    /// </summary>
    public sealed class ESItemConfigKeyTable : ESGameCoreConfigKeyTable<ESItemRuntimeData>
    {
        public ESItemConfigKeyTable(int capacity = 256) : base(capacity, "GameCore.Item") { }

        public int InjectWith(
            ESItemConfigKey key,
            ItemKind kind,
            ItemBaseConfig baseConfig,
            ItemInteractConfig interactConfig = null,
            ItemLogicConfig logicConfig = null,
            ItemMoveConfig moveConfig = null,
            List<ESTagStableReference> tags = null,
            List<ESItemFloatValue> floatValues = null,
            List<ESItemPermitValue> permitValues = null,
            ESWeaponConfigKey weaponKey = null,
            ESShotConfigKey shotKey = null,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateDefinition(key, kind, baseConfig);
            ESItemRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                PrepareRuntimeData(
                    runtimeData,
                    key,
                    kind,
                    baseConfig,
                    interactConfig,
                    logicConfig,
                    moveConfig,
                    tags,
                    floatValues,
                    permitValues,
                    weaponKey,
                    shotKey,
                    displayName,
                    sourcePackage,
                    version,
                    null);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESItemConfigKey key,
            ItemKind kind,
            ItemBaseConfig baseConfig,
            out int runtimeKey,
            ItemInteractConfig interactConfig = null,
            ItemLogicConfig logicConfig = null,
            ItemMoveConfig moveConfig = null,
            List<ESTagStableReference> tags = null,
            List<ESItemFloatValue> floatValues = null,
            List<ESItemPermitValue> permitValues = null,
            ESWeaponConfigKey weaponKey = null,
            ESShotConfigKey shotKey = null,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || kind == ItemKind.None || baseConfig == null)
                return false;
            if (!TryAcquireRetained(key, out ESItemRuntimeData runtimeData))
                return false;

            try
            {
                PrepareRuntimeData(
                    runtimeData,
                    key,
                    kind,
                    baseConfig,
                    interactConfig,
                    logicConfig,
                    moveConfig,
                    tags,
                    floatValues,
                    permitValues,
                    weaponKey,
                    shotKey,
                    displayName,
                    sourcePackage,
                    version,
                    null);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        internal static void PrepareFromInfo(ESItemRuntimeData runtimeData, ItemDataInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            ValidateDefinition(info.itemKey, info.baseConfig != null ? info.baseConfig.kind : ItemKind.None, info.baseConfig);

            ESWeaponConfigKey weaponKey = (info.kindData as ItemWeaponDataBlock)?.key;
            ESShotConfigKey shotKey = (info.kindData as ItemShotDataBlock)?.key;
            PrepareRuntimeData(
                runtimeData,
                info.itemKey,
                info.baseConfig.kind,
                info.baseConfig,
                info.interactConfig,
                info.logicConfig,
                info.moveConfig,
                info.tags,
                info.floatValues,
                info.permitValues,
                weaponKey,
                shotKey,
                ESItemGameCoreDisplayName.Get(info),
                info.name,
                null,
                info);
        }

        private static void PrepareRuntimeData(
            ESItemRuntimeData runtimeData,
            ESItemConfigKey key,
            ItemKind kind,
            ItemBaseConfig baseConfig,
            ItemInteractConfig interactConfig,
            ItemLogicConfig logicConfig,
            ItemMoveConfig moveConfig,
            List<ESTagStableReference> tags,
            List<ESItemFloatValue> floatValues,
            List<ESItemPermitValue> permitValues,
            ESWeaponConfigKey weaponKey,
            ESShotConfigKey shotKey,
            string displayName,
            string sourcePackage,
            string version,
            ItemDataInfo soSource)
        {
            string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            runtimeData.keyName = keyName;
            runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
            runtimeData.sourcePackage = sourcePackage ?? string.Empty;
            runtimeData.version = version ?? string.Empty;
            runtimeData.soSource = soSource;
            runtimeData.kind = kind;
            runtimeData.baseConfig = baseConfig;
            runtimeData.interactConfig = interactConfig;
            runtimeData.logicConfig = logicConfig;
            runtimeData.moveConfig = moveConfig;
            runtimeData.tags = tags;
            runtimeData.floatValues = floatValues;
            runtimeData.permitValues = permitValues;
            runtimeData.weaponKey = weaponKey;
            runtimeData.shotKey = shotKey;
        }

        private static void ValidateDefinition(ESItemConfigKey key, ItemKind kind, ItemBaseConfig baseConfig)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Item 必须提供有效 ConfigKey。");
            if (kind == ItemKind.None || baseConfig == null || baseConfig.kind != kind)
                throw new InvalidOperationException("Item 基础定义缺失或 ItemKind 不一致。");
        }
    }
}

