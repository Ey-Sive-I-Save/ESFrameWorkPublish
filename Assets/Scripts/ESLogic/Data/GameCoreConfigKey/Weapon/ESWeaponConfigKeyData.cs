using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Weapon/ESWeaponConfigKeyData.cs")]
    public enum ESWeaponEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [Serializable]
    public sealed class ESWeaponConfigKey : ESGameCoreConfigKey<ESWeaponEnumKey>
    {
        public static implicit operator ESWeaponConfigKey(ESWeaponEnumKey value)
            => new ESWeaponConfigKey { enumKey = value };

        public static implicit operator ESWeaponConfigKey(string value)
            => new ESWeaponConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESWeaponRuntimeData : ESGameCoreRuntimeData
    {
        private readonly ItemWeaponSharedData ownedDefaultSharedData = new ItemWeaponSharedData();
        [NonSerialized] private ItemWeaponSharedData preparedSharedData;
        [NonSerialized] private ItemWeaponVariableData preparedDefaultVariableData;
        [NonSerialized] private ESAssetIdentity preparedPrefabIdentity;
        [NonSerialized] private int preparedItemRuntimeKey;
        [NonSerialized] private bool hasPreparedData;

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemWeaponSharedData sharedData;
        public ItemWeaponVariableData defaultVariableData;
        public ESAssetReferPrefabConfigKey prefabKey;

        internal ItemWeaponSharedData PreparedSharedData => hasPreparedData
            ? preparedSharedData
            : null;

        internal ItemWeaponVariableData PreparedDefaultVariableData => hasPreparedData
            ? preparedDefaultVariableData
            : default;

        internal bool TryGetPreparedPrefabIdentity(out ESAssetIdentity identity)
        {
            identity = preparedPrefabIdentity;
            return hasPreparedData && identity.IsValid;
        }

        internal bool TryGetPreparedItemRuntimeKey(out int runtimeKey)
        {
            runtimeKey = preparedItemRuntimeKey;
            return hasPreparedData && runtimeKey > 0;
        }

        internal bool Internal_TryPrepare(int enumKey, string stringKey, out string error)
        {
            Internal_ClearPrepared();
            if (sharedData == null)
            {
                error = "WeaponDefinition 不能为空。";
                return false;
            }
            if (!sharedData.ValidateDefinition(out error)
                || !sharedData.ValidateInitialState(defaultVariableData, out error))
                return false;
            if (!ESWeaponConfigKeyTable.TryValidatePrefabIdentity(prefabKey, out error))
                return false;

            preparedSharedData = sharedData.Internal_CreatePreparedCopy();
            preparedDefaultVariableData = defaultVariableData;
            preparedPrefabIdentity = new ESAssetIdentity(prefabKey.guid, prefabKey.localFileId);
            if (!TryResolveItemProjection(enumKey, stringKey, out preparedItemRuntimeKey, out error))
            {
                Internal_ClearPrepared();
                return false;
            }
            hasPreparedData = true;
            error = null;
            return true;
        }

        private bool TryResolveItemProjection(
            int enumKey,
            string stringKey,
            out int itemRuntimeKey,
            out string error)
        {
            itemRuntimeKey = 0;
            if (soSource == null)
            {
                error = null;
                return true;
            }

            ESItemConfigKey itemKey = soSource.itemKey;
            if (itemKey == null
                || !itemKey.IsConfigured
                || !ESRuntimeDataGameCore.Items.TryGetRuntimeKey(itemKey, out itemRuntimeKey)
                || !ESRuntimeDataGameCore.Items.TryGet(itemRuntimeKey, out ESItemRuntimeData itemData)
                || itemData == null
                || !itemData.Ready
                || itemData.kind != ItemKind.Weapon
                || itemData.weaponKey == null
                || !ESConfigKeyMatch.Matches(
                    itemData.weaponKey.EnumKeyInt,
                    itemData.weaponKey.StringKey,
                    enumKey,
                    stringKey))
            {
                itemRuntimeKey = 0;
                error = "WeaponDefinition 无法冻结匹配的 Item 投影身份。";
                return false;
            }

            error = null;
            return true;
        }

        internal void Internal_ClearPrepared()
        {
            hasPreparedData = false;
            preparedSharedData = null;
            preparedDefaultVariableData = default;
            preparedPrefabIdentity = default;
            preparedItemRuntimeKey = 0;
        }

        internal ItemWeaponSharedData PrepareDefaultSharedData()
        {
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal bool OwnsDefaultSharedData(ItemWeaponSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        protected override void ReleaseRuntimePayload()
        {
            Internal_ClearPrepared();
            soSource = null;
            sharedData = null;
            defaultVariableData = default;
            prefabKey = null;
            ownedDefaultSharedData.ResetToDefaults();
        }

    }

    /// <summary>Weapon 领域表。调用方只提供武器定义，不需要手工创建 ESWeaponRuntimeData。</summary>
    public sealed class ESWeaponConfigKeyTable : ESGameCoreConfigKeyTable<ESWeaponRuntimeData>
    {
        public ESWeaponConfigKeyTable(int capacity = 64) : base(capacity, "GameCore.Weapon") { }

        public int Inject(ESWeaponEnumKey key, ESWeaponRuntimeData data, string debugName = null)
            => CommitRetained((ESWeaponConfigKey)key, data, debugName);

        public bool TryInject(
            ESWeaponEnumKey key,
            ESWeaponRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESWeaponConfigKey)key, data, out runtimeKey, debugName);

        protected override bool CanRegisterRetainedData(
            int enumKey,
            string stringKey,
            ESWeaponRuntimeData data)
        {
            if (data != null && data.Internal_TryPrepare(enumKey, stringKey, out _))
                return true;

            AbandonRetained(data);
            return false;
        }

        /// <summary>注入现成权威 Weapon 定义；Table 不修改也不回收 SharedData。</summary>
        public int InjectWith(
            ESWeaponConfigKey key,
            ItemWeaponSharedData sharedData,
            ItemWeaponVariableData defaultVariableData,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key, nameof(InjectWith));
            if (sharedData == null)
                throw new ArgumentNullException(nameof(sharedData));

            ESWeaponRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefabKey, sourcePackage, version);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESWeaponConfigKey key,
            ItemWeaponSharedData sharedData,
            ItemWeaponVariableData defaultVariableData,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || sharedData == null)
                return false;

            if (!TryAcquireRetained(key, out ESWeaponRuntimeData runtimeData))
                return false;
            try
            {
                CreateRuntimeData(
                    runtimeData, key, sharedData, defaultVariableData,
                    displayName, prefabKey, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        /// <summary>从 Weapon 领域默认值构造并覆盖 Table 独占的次级运行时定义。</summary>
        public int InjectWithDefaults(
            ESWeaponConfigKey key,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            ItemWeaponKind? weaponKind = null,
            ESShotConfigKey defaultShot = null,
            float? hitRadius = null,
            float? cooldown = null,
            float? durability = null,
            float? cooldownLeft = null,
            int? ammo = null,
            int? logicSeed = null,
            ESDataFiller<ItemWeaponSharedData> fillShared = null,
            ESDataFiller<ItemWeaponVariableData> fillVariable = null,
            ESActionConfigKey primaryAttackAction = null,
            WeaponAttackDeliveryMode? deliveryMode = null,
            WeaponFirePolicy? firePolicy = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESWeaponRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                ItemWeaponSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ResolveDefaults(
                    ownedShared, null,
                    weaponKind, deliveryMode, firePolicy, primaryAttackAction, defaultShot, hitRadius, cooldown,
                    durability, cooldownLeft, ammo, logicSeed,
                    fillShared, fillVariable,
                    out ItemWeaponSharedData sharedData, out ItemWeaponVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                    throw new InvalidOperationException("Weapon InjectWithDefaults 的 fillShared 不得替换 Table 自有 SharedData 实例。");
                CreateRuntimeData(
                    runtimeData, key, sharedData, resolvedVariableData,
                    displayName, prefabKey, sourcePackage, version);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWithDefaults(
            ESWeaponConfigKey key,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            ItemWeaponKind? weaponKind = null,
            ESShotConfigKey defaultShot = null,
            float? hitRadius = null,
            float? cooldown = null,
            float? durability = null,
            float? cooldownLeft = null,
            int? ammo = null,
            int? logicSeed = null,
            ESDataFiller<ItemWeaponSharedData> fillShared = null,
            ESDataFiller<ItemWeaponVariableData> fillVariable = null,
            ESActionConfigKey primaryAttackAction = null,
            WeaponAttackDeliveryMode? deliveryMode = null,
            WeaponFirePolicy? firePolicy = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESWeaponRuntimeData runtimeData))
                return false;
            try
            {
                ItemWeaponSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ResolveDefaults(
                    ownedShared, null,
                    weaponKind, deliveryMode, firePolicy, primaryAttackAction, defaultShot, hitRadius, cooldown,
                    durability, cooldownLeft, ammo, logicSeed,
                    fillShared, fillVariable,
                    out ItemWeaponSharedData sharedData, out ItemWeaponVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                {
                    AbandonRetained(runtimeData);
                    return false;
                }

                CreateRuntimeData(
                    runtimeData, key, sharedData, resolvedVariableData,
                    displayName, prefabKey, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static ESWeaponRuntimeData CreateRuntimeData(
            ESWeaponRuntimeData runtimeData,
            ESWeaponConfigKey key,
            ItemWeaponSharedData sharedData,
            ItemWeaponVariableData defaultVariableData,
            string displayName,
            ESAssetReferPrefabConfigKey prefabKey,
            string sourcePackage,
            string version)
        {
            if (!sharedData.ValidateDefinition(out string validationError))
                throw new InvalidOperationException("WeaponDefinition 校验失败：" + validationError);
            if (!sharedData.ValidateInitialState(defaultVariableData, out string stateValidationError))
                throw new InvalidOperationException("WeaponVariable 校验失败：" + stateValidationError);

            string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            runtimeData.keyName = keyName;
            runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
            runtimeData.sourcePackage = sourcePackage ?? string.Empty;
            runtimeData.version = version ?? string.Empty;
            runtimeData.sharedData = sharedData;
            runtimeData.defaultVariableData = defaultVariableData;
            runtimeData.prefabKey = prefabKey;
            return runtimeData;
        }

        internal static bool TryValidatePrefabIdentity(
            ESAssetReferPrefabConfigKey prefabKey,
            out string error)
        {
            if (prefabKey == null || !prefabKey.IsConfigured || !prefabKey.HasGuid)
            {
                error = "WeaponDefinition 必须冻结完整 Prefab GUID 资产身份。";
                return false;
            }
            if (!string.Equals(
                    prefabKey.assetTypeName,
                    typeof(GameObject).FullName,
                    StringComparison.Ordinal))
            {
                error = "WeaponDefinition 的 Prefab Key 资产类型必须为 UnityEngine.GameObject。";
                return false;
            }

            error = null;
            return true;
        }

        private static void ResolveDefaults(
            ItemWeaponSharedData sharedData,
            ItemWeaponVariableData? variableData,
            ItemWeaponKind? weaponKind,
            WeaponAttackDeliveryMode? deliveryMode,
            WeaponFirePolicy? firePolicy,
            ESActionConfigKey primaryAttackAction,
            ESShotConfigKey defaultShot,
            float? hitRadius,
            float? cooldown,
            float? durability,
            float? cooldownLeft,
            int? ammo,
            int? logicSeed,
            ESDataFiller<ItemWeaponSharedData> fillShared,
            ESDataFiller<ItemWeaponVariableData> fillVariable,
            out ItemWeaponSharedData resolvedShared,
            out ItemWeaponVariableData resolvedVariable)
        {
            resolvedShared = sharedData ?? ItemWeaponSharedData.Default;
            resolvedVariable = variableData ?? ItemWeaponVariableData.Default;

            if (weaponKind.HasValue) resolvedShared.weaponKind = weaponKind.Value;
            if (deliveryMode.HasValue) resolvedShared.deliveryMode = deliveryMode.Value;
            if (firePolicy.HasValue) resolvedShared.firePolicy = firePolicy.Value;
            if (primaryAttackAction != null) resolvedShared.primaryAttackAction = primaryAttackAction;
            if (defaultShot != null) resolvedShared.defaultShot = defaultShot;
            if (hitRadius.HasValue) resolvedShared.hitRadius = hitRadius.Value;
            if (cooldown.HasValue) resolvedShared.cooldown = cooldown.Value;
            if (durability.HasValue) resolvedVariable.durability = durability.Value;
            if (cooldownLeft.HasValue) resolvedVariable.cooldownLeft = cooldownLeft.Value;
            if (ammo.HasValue) resolvedVariable.ammo = ammo.Value;
            if (logicSeed.HasValue) resolvedVariable.logicSeed = logicSeed.Value;

            fillShared?.Invoke(ref resolvedShared);
            fillVariable?.Invoke(ref resolvedVariable);
            resolvedShared ??= ItemWeaponSharedData.Default;
        }

        private static void ValidateKey(ESWeaponConfigKey key, string api)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Weapon " + api + " 必须提供有效 ConfigKey。");
        }
    }
}
