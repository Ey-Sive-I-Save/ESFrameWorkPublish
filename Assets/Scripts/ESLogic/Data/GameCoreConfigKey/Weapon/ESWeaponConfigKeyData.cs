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

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemWeaponSharedData sharedData;
        public ItemWeaponVariableData defaultVariableData;
        public ESAssetReferPrefabConfigKey prefabKey;

        internal ItemWeaponSharedData PrepareDefaultSharedData()
        {
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal bool OwnsDefaultSharedData(ItemWeaponSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        protected override void ReleaseRuntimePayload()
        {
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
            string socketName = null,
            float? durability = null,
            float? cooldownLeft = null,
            int? ammo = null,
            int? logicSeed = null,
            ESDataFiller<ItemWeaponSharedData> fillShared = null,
            ESDataFiller<ItemWeaponVariableData> fillVariable = null,
            ESActionConfigKey primaryAttackAction = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESWeaponRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                ItemWeaponSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ResolveDefaults(
                    ownedShared, null,
                    weaponKind, primaryAttackAction, defaultShot, hitRadius, cooldown, socketName,
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
            string socketName = null,
            float? durability = null,
            float? cooldownLeft = null,
            int? ammo = null,
            int? logicSeed = null,
            ESDataFiller<ItemWeaponSharedData> fillShared = null,
            ESDataFiller<ItemWeaponVariableData> fillVariable = null,
            ESActionConfigKey primaryAttackAction = null)
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
                    weaponKind, primaryAttackAction, defaultShot, hitRadius, cooldown, socketName,
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

        private static void ResolveDefaults(
            ItemWeaponSharedData sharedData,
            ItemWeaponVariableData? variableData,
            ItemWeaponKind? weaponKind,
            ESActionConfigKey primaryAttackAction,
            ESShotConfigKey defaultShot,
            float? hitRadius,
            float? cooldown,
            string socketName,
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
            if (primaryAttackAction != null) resolvedShared.primaryAttackAction = primaryAttackAction;
            if (defaultShot != null) resolvedShared.defaultShot = defaultShot;
            if (hitRadius.HasValue) resolvedShared.hitRadius = hitRadius.Value;
            if (cooldown.HasValue) resolvedShared.cooldown = cooldown.Value;
            if (socketName != null) resolvedShared.socketName = socketName;

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
