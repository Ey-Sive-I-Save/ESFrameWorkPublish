using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Shot/ESShotConfigKeyData.cs")]
    public enum ESShotEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [Serializable]
    public sealed class ESShotConfigKey : ESGameCoreConfigKey<ESShotEnumKey>
    {
        public static implicit operator ESShotConfigKey(ESShotEnumKey value)
            => new ESShotConfigKey { enumKey = value };

        public static implicit operator ESShotConfigKey(string value)
            => new ESShotConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESShotRuntimeData : ESGameCoreRuntimeData
    {
        private readonly ItemShotSharedData ownedDefaultSharedData = new ItemShotSharedData();

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemShotSharedData sharedData;
        public ItemShotVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;

        internal ItemShotSharedData PrepareDefaultSharedData()
        {
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal bool OwnsDefaultSharedData(ItemShotSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            sharedData = null;
            defaultVariableData = default;
            prefab = null;
            extraAsset = null;
            ownedDefaultSharedData.ResetToDefaults();
        }

    }

    /// <summary>Shot 领域表。调用方只提供飞行物定义，不需要手工创建 ESShotRuntimeData。</summary>
    public sealed class ESShotConfigKeyTable : ESGameCoreConfigKeyTable<ESShotRuntimeData>
    {
        public ESShotConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Shot") { }

        public int Inject(ESShotEnumKey key, ESShotRuntimeData data, string debugName = null)
            => CommitRetained((ESShotConfigKey)key, data, debugName);

        public bool TryInject(
            ESShotEnumKey key,
            ESShotRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESShotConfigKey)key, data, out runtimeKey, debugName);

        /// <summary>注入现成权威 Shot 定义；Table 不修改也不回收 SharedData。</summary>
        public int InjectWith(
            ESShotConfigKey key,
            ItemShotSharedData sharedData,
            ItemShotVariableData defaultVariableData,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key, nameof(InjectWith));
            if (sharedData == null)
                throw new ArgumentNullException(nameof(sharedData));

            ESShotRuntimeData runtimeData = AcquireRetained(key);
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
            ESShotConfigKey key,
            ItemShotSharedData sharedData,
            ItemShotVariableData defaultVariableData,
            out int runtimeKey,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || sharedData == null)
                return false;

            if (!TryAcquireRetained(key, out ESShotRuntimeData runtimeData))
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

        /// <summary>从 Shot 领域默认值构造并覆盖 Table 独占的次级运行时定义。</summary>
        public int InjectWithDefaults(
            ESShotConfigKey key,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null,
            bool? enabled = null,
            ShotAimMode? aimMode = null,
            ShotBlockMode? blockMode = null,
            float? launchDelay = null,
            float? warmupTime = null,
            float? speed = null,
            float? acceleration = null,
            float? maxSpeed = null,
            float? trackingStartTime = null,
            float? trackingDuration = null,
            float? turnSpeed = null,
            float? lifeTime = null,
            float? radius = null,
            LayerMask? hitLayers = null,
            bool? useGravity = null,
            bool? orientToVelocity = null,
            bool? allowMustHit = null,
            int? logicSeed = null,
            float? speedMultiplier = null,
            float? lifeTimeMultiplier = null,
            float? radiusMultiplier = null,
            bool? forceMustHit = null,
            bool? overrideLaunchDelay = null,
            float? variableLaunchDelay = null,
            bool? overrideTrackingStartTime = null,
            float? variableTrackingStartTime = null,
            Vector3? targetOffset = null,
            float? spreadAngle = null,
            ESDataFiller<ItemShotSharedData> fillShared = null,
            ESDataFiller<ItemShotVariableData> fillVariable = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESShotRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                ItemShotSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ResolveDefaults(
                    ownedShared, null,
                    enabled, aimMode, blockMode, launchDelay, warmupTime, speed, acceleration, maxSpeed,
                    trackingStartTime, trackingDuration, turnSpeed, lifeTime, radius, hitLayers,
                    useGravity, orientToVelocity, allowMustHit,
                    logicSeed, speedMultiplier, lifeTimeMultiplier, radiusMultiplier, forceMustHit,
                    overrideLaunchDelay, variableLaunchDelay, overrideTrackingStartTime, variableTrackingStartTime,
                    targetOffset, spreadAngle, fillShared, fillVariable,
                    out ItemShotSharedData sharedData, out ItemShotVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                    throw new InvalidOperationException("Shot InjectWithDefaults 的 fillShared 不得替换 Table 自有 SharedData 实例。");
                CreateRuntimeData(
                    runtimeData, key, sharedData, resolvedVariableData,
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
            ESShotConfigKey key,
            out int runtimeKey,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null,
            bool? enabled = null,
            ShotAimMode? aimMode = null,
            ShotBlockMode? blockMode = null,
            float? launchDelay = null,
            float? warmupTime = null,
            float? speed = null,
            float? acceleration = null,
            float? maxSpeed = null,
            float? trackingStartTime = null,
            float? trackingDuration = null,
            float? turnSpeed = null,
            float? lifeTime = null,
            float? radius = null,
            LayerMask? hitLayers = null,
            bool? useGravity = null,
            bool? orientToVelocity = null,
            bool? allowMustHit = null,
            int? logicSeed = null,
            float? speedMultiplier = null,
            float? lifeTimeMultiplier = null,
            float? radiusMultiplier = null,
            bool? forceMustHit = null,
            bool? overrideLaunchDelay = null,
            float? variableLaunchDelay = null,
            bool? overrideTrackingStartTime = null,
            float? variableTrackingStartTime = null,
            Vector3? targetOffset = null,
            float? spreadAngle = null,
            ESDataFiller<ItemShotSharedData> fillShared = null,
            ESDataFiller<ItemShotVariableData> fillVariable = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESShotRuntimeData runtimeData))
                return false;
            try
            {
                ItemShotSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ResolveDefaults(
                    ownedShared, null,
                    enabled, aimMode, blockMode, launchDelay, warmupTime, speed, acceleration, maxSpeed,
                    trackingStartTime, trackingDuration, turnSpeed, lifeTime, radius, hitLayers,
                    useGravity, orientToVelocity, allowMustHit,
                    logicSeed, speedMultiplier, lifeTimeMultiplier, radiusMultiplier, forceMustHit,
                    overrideLaunchDelay, variableLaunchDelay, overrideTrackingStartTime, variableTrackingStartTime,
                    targetOffset, spreadAngle, fillShared, fillVariable,
                    out ItemShotSharedData sharedData, out ItemShotVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                {
                    AbandonRetained(runtimeData);
                    return false;
                }

                CreateRuntimeData(
                    runtimeData, key, sharedData, resolvedVariableData,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static ESShotRuntimeData CreateRuntimeData(
            ESShotRuntimeData runtimeData,
            ESShotConfigKey key,
            ItemShotSharedData sharedData,
            ItemShotVariableData defaultVariableData,
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
            ItemShotSharedData sharedData,
            ItemShotVariableData? variableData,
            bool? enabled,
            ShotAimMode? aimMode,
            ShotBlockMode? blockMode,
            float? launchDelay,
            float? warmupTime,
            float? speed,
            float? acceleration,
            float? maxSpeed,
            float? trackingStartTime,
            float? trackingDuration,
            float? turnSpeed,
            float? lifeTime,
            float? radius,
            LayerMask? hitLayers,
            bool? useGravity,
            bool? orientToVelocity,
            bool? allowMustHit,
            int? logicSeed,
            float? speedMultiplier,
            float? lifeTimeMultiplier,
            float? radiusMultiplier,
            bool? forceMustHit,
            bool? overrideLaunchDelay,
            float? variableLaunchDelay,
            bool? overrideTrackingStartTime,
            float? variableTrackingStartTime,
            Vector3? targetOffset,
            float? spreadAngle,
            ESDataFiller<ItemShotSharedData> fillShared,
            ESDataFiller<ItemShotVariableData> fillVariable,
            out ItemShotSharedData resolvedShared,
            out ItemShotVariableData resolvedVariable)
        {
            resolvedShared = sharedData ?? ItemShotSharedData.Default;
            resolvedVariable = variableData ?? ItemShotVariableData.Default;

            if (enabled.HasValue) resolvedShared.enabled = enabled.Value;
            if (aimMode.HasValue) resolvedShared.aimMode = aimMode.Value;
            if (blockMode.HasValue) resolvedShared.blockMode = blockMode.Value;
            if (launchDelay.HasValue) resolvedShared.launchDelay = launchDelay.Value;
            if (warmupTime.HasValue) resolvedShared.warmupTime = warmupTime.Value;
            if (speed.HasValue) resolvedShared.speed = speed.Value;
            if (acceleration.HasValue) resolvedShared.acceleration = acceleration.Value;
            if (maxSpeed.HasValue) resolvedShared.maxSpeed = maxSpeed.Value;
            if (trackingStartTime.HasValue) resolvedShared.trackingStartTime = trackingStartTime.Value;
            if (trackingDuration.HasValue) resolvedShared.trackingDuration = trackingDuration.Value;
            if (turnSpeed.HasValue) resolvedShared.turnSpeed = turnSpeed.Value;
            if (lifeTime.HasValue) resolvedShared.lifeTime = lifeTime.Value;
            if (radius.HasValue) resolvedShared.radius = radius.Value;
            if (hitLayers.HasValue) resolvedShared.hitLayers = hitLayers.Value;
            if (useGravity.HasValue) resolvedShared.useGravity = useGravity.Value;
            if (orientToVelocity.HasValue) resolvedShared.orientToVelocity = orientToVelocity.Value;
            if (allowMustHit.HasValue) resolvedShared.allowMustHit = allowMustHit.Value;

            if (logicSeed.HasValue) resolvedVariable.logicSeed = logicSeed.Value;
            if (speedMultiplier.HasValue) resolvedVariable.speedMultiplier = speedMultiplier.Value;
            if (lifeTimeMultiplier.HasValue) resolvedVariable.lifeTimeMultiplier = lifeTimeMultiplier.Value;
            if (radiusMultiplier.HasValue) resolvedVariable.radiusMultiplier = radiusMultiplier.Value;
            if (forceMustHit.HasValue) resolvedVariable.forceMustHit = forceMustHit.Value;
            if (overrideLaunchDelay.HasValue) resolvedVariable.overrideLaunchDelay = overrideLaunchDelay.Value;
            if (variableLaunchDelay.HasValue) resolvedVariable.launchDelay = variableLaunchDelay.Value;
            if (overrideTrackingStartTime.HasValue) resolvedVariable.overrideTrackingStartTime = overrideTrackingStartTime.Value;
            if (variableTrackingStartTime.HasValue) resolvedVariable.trackingStartTime = variableTrackingStartTime.Value;
            if (targetOffset.HasValue) resolvedVariable.targetOffset = targetOffset.Value;
            if (spreadAngle.HasValue) resolvedVariable.spreadAngle = spreadAngle.Value;

            fillShared?.Invoke(ref resolvedShared);
            fillVariable?.Invoke(ref resolvedVariable);
            resolvedShared ??= ItemShotSharedData.Default;
        }

        private static void ValidateKey(ESShotConfigKey key, string api)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Shot " + api + " 必须提供有效 ConfigKey。");
        }
    }
}
