using System;
using UnityEngine;
using System.Collections.Generic;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Actor/ESActorConfigKeyData.cs")]
    public enum ESMonsterEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Actor/ESActorConfigKeyData.cs")]
    public enum ESNpcEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [Serializable]
    public sealed class ESMonsterConfigKey : ESGameCoreConfigKey<ESMonsterEnumKey>
    {
        public static implicit operator ESMonsterConfigKey(ESMonsterEnumKey value)
            => new ESMonsterConfigKey { enumKey = value };

        public static implicit operator ESMonsterConfigKey(string value)
            => new ESMonsterConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESNpcConfigKey : ESGameCoreConfigKey<ESNpcEnumKey>
    {
        public static implicit operator ESNpcConfigKey(ESNpcEnumKey value)
            => new ESNpcConfigKey { enumKey = value };

        public static implicit operator ESNpcConfigKey(string value)
            => new ESNpcConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESMonsterRuntimeData : ESGameCoreRuntimeData
    {
        private readonly EntityMotionSharedData ownedDefaultSharedData = new EntityMotionSharedData();

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ScriptableObject soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public List<ESTagStableReference> tags;
        public ESAssetReferPrefabConfigKey prefabKey;

        internal EntityMotionSharedData PrepareDefaultSharedData()
        {
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal bool OwnsDefaultSharedData(EntityMotionSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            sharedData = null;
            defaultVariableData = default;
            tags = null;
            prefabKey = null;
            ownedDefaultSharedData.ResetToDefaults();
        }

    }

    [Serializable]
    public sealed class ESNpcRuntimeData : ESGameCoreRuntimeData
    {
        private readonly EntityMotionSharedData ownedDefaultSharedData = new EntityMotionSharedData();

        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ScriptableObject soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public List<ESTagStableReference> tags;
        public ESAssetReferPrefabConfigKey prefabKey;

        internal EntityMotionSharedData PrepareDefaultSharedData()
        {
            ownedDefaultSharedData.ResetToDefaults();
            return ownedDefaultSharedData;
        }

        internal bool OwnsDefaultSharedData(EntityMotionSharedData value)
            => ReferenceEquals(value, ownedDefaultSharedData);

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            sharedData = null;
            defaultVariableData = default;
            tags = null;
            prefabKey = null;
            ownedDefaultSharedData.ResetToDefaults();
        }

    }

    /// <summary>Monster 领域表。调用方只提供运动定义，不需要手工创建 ESMonsterRuntimeData。</summary>
    public sealed class ESMonsterConfigKeyTable : ESGameCoreConfigKeyTable<ESMonsterRuntimeData>
    {
        public ESMonsterConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Monster") { }

        public int Inject(ESMonsterEnumKey key, ESMonsterRuntimeData data, string debugName = null)
            => CommitRetained((ESMonsterConfigKey)key, data, debugName);

        public bool TryInject(
            ESMonsterEnumKey key,
            ESMonsterRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESMonsterConfigKey)key, data, out runtimeKey, debugName);

        /// <summary>注入现成权威运动定义；Table 不修改也不回收 SharedData。</summary>
        public int InjectWith(
            ESMonsterConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key, nameof(InjectWith));
            if (sharedData == null)
                throw new ArgumentNullException(nameof(sharedData));

            ESMonsterRuntimeData runtimeData = AcquireRetained(key);
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
            ESMonsterConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || sharedData == null)
                return false;

            if (!TryAcquireRetained(key, out ESMonsterRuntimeData runtimeData))
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

        /// <summary>从领域默认值构造并覆盖 Table 独占的次级运行时运动定义。</summary>
        public int InjectWithDefaults(
            ESMonsterConfigKey key,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            bool? enableGroundMove = null,
            bool? enableJump = null,
            bool? enableCrouch = null,
            bool? enableFly = null,
            bool? enableClimb = null,
            bool? enableMount = null,
            bool? enableGrappleMotion = null,
            float? maxStableMoveSpeed = null,
            float? stableMovementSharpness = null,
            float? maxAirMoveSpeed = null,
            float? airAccelerationSpeed = null,
            float? jumpSpeed = null,
            float? maxStableSlopeAngle = null,
            float? steepSlopeSlideSpeed = null,
            EntityMotionStepPolicy? stepPolicy = null,
            EntityFlyControlMode? flyControlMode = null,
            float? flyMaxSpeed = null,
            float? flySprintMultiplier = null,
            StateSupportFlags? initialSupportFlag = null,
            float? speedMultiplier = null,
            float? speedLimit = null,
            float? gravityMultiplier = null,
            bool? allowMoveInput = null,
            bool? allowLookInput = null,
            bool? allowJump = null,
            bool? allowMotionModeSwitch = null,
            bool? allowRootMotion = null,
            ESDataFiller<EntityMotionSharedData> fillShared = null,
            ESDataFiller<EntityMotionVariableData> fillVariable = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESMonsterRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                EntityMotionSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ESEntityMotionInjectionData.Resolve(
                    ownedShared, null,
                    enableGroundMove, enableJump, enableCrouch, enableFly, enableClimb, enableMount, enableGrappleMotion,
                    maxStableMoveSpeed, stableMovementSharpness, maxAirMoveSpeed, airAccelerationSpeed, jumpSpeed,
                    maxStableSlopeAngle, steepSlopeSlideSpeed, stepPolicy, flyControlMode, flyMaxSpeed, flySprintMultiplier,
                    initialSupportFlag, speedMultiplier, speedLimit, gravityMultiplier,
                    allowMoveInput, allowLookInput, allowJump, allowMotionModeSwitch, allowRootMotion,
                    fillShared, fillVariable,
                    out EntityMotionSharedData sharedData, out EntityMotionVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                    throw new InvalidOperationException("Monster InjectWithDefaults 的 fillShared 不得替换 Table 自有 SharedData 实例。");
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
            ESMonsterConfigKey key,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            bool? enableGroundMove = null,
            bool? enableJump = null,
            bool? enableCrouch = null,
            bool? enableFly = null,
            bool? enableClimb = null,
            bool? enableMount = null,
            bool? enableGrappleMotion = null,
            float? maxStableMoveSpeed = null,
            float? stableMovementSharpness = null,
            float? maxAirMoveSpeed = null,
            float? airAccelerationSpeed = null,
            float? jumpSpeed = null,
            float? maxStableSlopeAngle = null,
            float? steepSlopeSlideSpeed = null,
            EntityMotionStepPolicy? stepPolicy = null,
            EntityFlyControlMode? flyControlMode = null,
            float? flyMaxSpeed = null,
            float? flySprintMultiplier = null,
            StateSupportFlags? initialSupportFlag = null,
            float? speedMultiplier = null,
            float? speedLimit = null,
            float? gravityMultiplier = null,
            bool? allowMoveInput = null,
            bool? allowLookInput = null,
            bool? allowJump = null,
            bool? allowMotionModeSwitch = null,
            bool? allowRootMotion = null,
            ESDataFiller<EntityMotionSharedData> fillShared = null,
            ESDataFiller<EntityMotionVariableData> fillVariable = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESMonsterRuntimeData runtimeData))
                return false;
            try
            {
                EntityMotionSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ESEntityMotionInjectionData.Resolve(
                    ownedShared, null,
                    enableGroundMove, enableJump, enableCrouch, enableFly, enableClimb, enableMount, enableGrappleMotion,
                    maxStableMoveSpeed, stableMovementSharpness, maxAirMoveSpeed, airAccelerationSpeed, jumpSpeed,
                    maxStableSlopeAngle, steepSlopeSlideSpeed, stepPolicy, flyControlMode, flyMaxSpeed, flySprintMultiplier,
                    initialSupportFlag, speedMultiplier, speedLimit, gravityMultiplier,
                    allowMoveInput, allowLookInput, allowJump, allowMotionModeSwitch, allowRootMotion,
                    fillShared, fillVariable,
                    out EntityMotionSharedData sharedData, out EntityMotionVariableData resolvedVariableData);
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

        private static ESMonsterRuntimeData CreateRuntimeData(
            ESMonsterRuntimeData runtimeData,
            ESMonsterConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            string displayName,
            ESAssetReferPrefabConfigKey prefabKey,
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
            runtimeData.prefabKey = prefabKey;
            return runtimeData;
        }

        private static void ValidateKey(IESConfigKey key, string api)
        {
            if (key == null || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey))
                throw new InvalidOperationException("Monster " + api + " 必须提供有效 ConfigKey。");
        }
    }

    /// <summary>NPC 领域表。调用方只提供运动定义，不需要手工创建 ESNpcRuntimeData。</summary>
    public sealed class ESNpcConfigKeyTable : ESGameCoreConfigKeyTable<ESNpcRuntimeData>
    {
        public ESNpcConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Npc") { }

        public int Inject(ESNpcEnumKey key, ESNpcRuntimeData data, string debugName = null)
            => CommitRetained((ESNpcConfigKey)key, data, debugName);

        public bool TryInject(
            ESNpcEnumKey key,
            ESNpcRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESNpcConfigKey)key, data, out runtimeKey, debugName);

        /// <summary>注入现成权威运动定义；Table 不修改也不回收 SharedData。</summary>
        public int InjectWith(
            ESNpcConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key, nameof(InjectWith));
            if (sharedData == null)
                throw new ArgumentNullException(nameof(sharedData));

            ESNpcRuntimeData runtimeData = AcquireRetained(key);
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
            ESNpcConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || sharedData == null)
                return false;

            if (!TryAcquireRetained(key, out ESNpcRuntimeData runtimeData))
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

        /// <summary>从领域默认值构造并覆盖 Table 独占的次级运行时运动定义。</summary>
        public int InjectWithDefaults(
            ESNpcConfigKey key,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            bool? enableGroundMove = null,
            bool? enableJump = null,
            bool? enableCrouch = null,
            bool? enableFly = null,
            bool? enableClimb = null,
            bool? enableMount = null,
            bool? enableGrappleMotion = null,
            float? maxStableMoveSpeed = null,
            float? stableMovementSharpness = null,
            float? maxAirMoveSpeed = null,
            float? airAccelerationSpeed = null,
            float? jumpSpeed = null,
            float? maxStableSlopeAngle = null,
            float? steepSlopeSlideSpeed = null,
            EntityMotionStepPolicy? stepPolicy = null,
            EntityFlyControlMode? flyControlMode = null,
            float? flyMaxSpeed = null,
            float? flySprintMultiplier = null,
            StateSupportFlags? initialSupportFlag = null,
            float? speedMultiplier = null,
            float? speedLimit = null,
            float? gravityMultiplier = null,
            bool? allowMoveInput = null,
            bool? allowLookInput = null,
            bool? allowJump = null,
            bool? allowMotionModeSwitch = null,
            bool? allowRootMotion = null,
            ESDataFiller<EntityMotionSharedData> fillShared = null,
            ESDataFiller<EntityMotionVariableData> fillVariable = null)
        {
            ValidateKey(key, nameof(InjectWithDefaults));
            ESNpcRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                EntityMotionSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ESEntityMotionInjectionData.Resolve(
                    ownedShared, null,
                    enableGroundMove, enableJump, enableCrouch, enableFly, enableClimb, enableMount, enableGrappleMotion,
                    maxStableMoveSpeed, stableMovementSharpness, maxAirMoveSpeed, airAccelerationSpeed, jumpSpeed,
                    maxStableSlopeAngle, steepSlopeSlideSpeed, stepPolicy, flyControlMode, flyMaxSpeed, flySprintMultiplier,
                    initialSupportFlag, speedMultiplier, speedLimit, gravityMultiplier,
                    allowMoveInput, allowLookInput, allowJump, allowMotionModeSwitch, allowRootMotion,
                    fillShared, fillVariable,
                    out EntityMotionSharedData sharedData, out EntityMotionVariableData resolvedVariableData);
                if (!runtimeData.OwnsDefaultSharedData(sharedData))
                    throw new InvalidOperationException("NPC InjectWithDefaults 的 fillShared 不得替换 Table 自有 SharedData 实例。");
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
            ESNpcConfigKey key,
            out int runtimeKey,
            string displayName = null,
            ESAssetReferPrefabConfigKey prefabKey = null,
            string sourcePackage = null,
            string version = null,
            bool? enableGroundMove = null,
            bool? enableJump = null,
            bool? enableCrouch = null,
            bool? enableFly = null,
            bool? enableClimb = null,
            bool? enableMount = null,
            bool? enableGrappleMotion = null,
            float? maxStableMoveSpeed = null,
            float? stableMovementSharpness = null,
            float? maxAirMoveSpeed = null,
            float? airAccelerationSpeed = null,
            float? jumpSpeed = null,
            float? maxStableSlopeAngle = null,
            float? steepSlopeSlideSpeed = null,
            EntityMotionStepPolicy? stepPolicy = null,
            EntityFlyControlMode? flyControlMode = null,
            float? flyMaxSpeed = null,
            float? flySprintMultiplier = null,
            StateSupportFlags? initialSupportFlag = null,
            float? speedMultiplier = null,
            float? speedLimit = null,
            float? gravityMultiplier = null,
            bool? allowMoveInput = null,
            bool? allowLookInput = null,
            bool? allowJump = null,
            bool? allowMotionModeSwitch = null,
            bool? allowRootMotion = null,
            ESDataFiller<EntityMotionSharedData> fillShared = null,
            ESDataFiller<EntityMotionVariableData> fillVariable = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESNpcRuntimeData runtimeData))
                return false;
            try
            {
                EntityMotionSharedData ownedShared = runtimeData.PrepareDefaultSharedData();
                ESEntityMotionInjectionData.Resolve(
                    ownedShared, null,
                    enableGroundMove, enableJump, enableCrouch, enableFly, enableClimb, enableMount, enableGrappleMotion,
                    maxStableMoveSpeed, stableMovementSharpness, maxAirMoveSpeed, airAccelerationSpeed, jumpSpeed,
                    maxStableSlopeAngle, steepSlopeSlideSpeed, stepPolicy, flyControlMode, flyMaxSpeed, flySprintMultiplier,
                    initialSupportFlag, speedMultiplier, speedLimit, gravityMultiplier,
                    allowMoveInput, allowLookInput, allowJump, allowMotionModeSwitch, allowRootMotion,
                    fillShared, fillVariable,
                    out EntityMotionSharedData sharedData, out EntityMotionVariableData resolvedVariableData);
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

        private static ESNpcRuntimeData CreateRuntimeData(
            ESNpcRuntimeData runtimeData,
            ESNpcConfigKey key,
            EntityMotionSharedData sharedData,
            EntityMotionVariableData defaultVariableData,
            string displayName,
            ESAssetReferPrefabConfigKey prefabKey,
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
            runtimeData.prefabKey = prefabKey;
            return runtimeData;
        }

        private static void ValidateKey(ESNpcConfigKey key, string api)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("NPC " + api + " 必须提供有效 ConfigKey。");
        }
    }

    internal static class ESEntityMotionInjectionData
    {
        public static void Resolve(
            EntityMotionSharedData sharedData,
            EntityMotionVariableData? variableData,
            bool? enableGroundMove,
            bool? enableJump,
            bool? enableCrouch,
            bool? enableFly,
            bool? enableClimb,
            bool? enableMount,
            bool? enableGrappleMotion,
            float? maxStableMoveSpeed,
            float? stableMovementSharpness,
            float? maxAirMoveSpeed,
            float? airAccelerationSpeed,
            float? jumpSpeed,
            float? maxStableSlopeAngle,
            float? steepSlopeSlideSpeed,
            EntityMotionStepPolicy? stepPolicy,
            EntityFlyControlMode? flyControlMode,
            float? flyMaxSpeed,
            float? flySprintMultiplier,
            StateSupportFlags? initialSupportFlag,
            float? speedMultiplier,
            float? speedLimit,
            float? gravityMultiplier,
            bool? allowMoveInput,
            bool? allowLookInput,
            bool? allowJump,
            bool? allowMotionModeSwitch,
            bool? allowRootMotion,
            ESDataFiller<EntityMotionSharedData> fillShared,
            ESDataFiller<EntityMotionVariableData> fillVariable,
            out EntityMotionSharedData resolvedShared,
            out EntityMotionVariableData resolvedVariable)
        {
            resolvedShared = sharedData ?? EntityMotionSharedData.Default;
            resolvedVariable = variableData ?? EntityMotionVariableData.Default;

            if (enableGroundMove.HasValue) resolvedShared.enableGroundMove = enableGroundMove.Value;
            if (enableJump.HasValue) resolvedShared.enableJump = enableJump.Value;
            if (enableCrouch.HasValue) resolvedShared.enableCrouch = enableCrouch.Value;
            if (enableFly.HasValue) resolvedShared.enableFly = enableFly.Value;
            if (enableClimb.HasValue) resolvedShared.enableClimb = enableClimb.Value;
            if (enableMount.HasValue) resolvedShared.enableMount = enableMount.Value;
            if (enableGrappleMotion.HasValue) resolvedShared.enableGrappleMotion = enableGrappleMotion.Value;
            if (maxStableMoveSpeed.HasValue) resolvedShared.maxStableMoveSpeed = maxStableMoveSpeed.Value;
            if (stableMovementSharpness.HasValue) resolvedShared.stableMovementSharpness = stableMovementSharpness.Value;
            if (maxAirMoveSpeed.HasValue) resolvedShared.maxAirMoveSpeed = maxAirMoveSpeed.Value;
            if (airAccelerationSpeed.HasValue) resolvedShared.airAccelerationSpeed = airAccelerationSpeed.Value;
            if (jumpSpeed.HasValue) resolvedShared.jumpSpeed = jumpSpeed.Value;
            if (maxStableSlopeAngle.HasValue) resolvedShared.maxStableSlopeAngle = maxStableSlopeAngle.Value;
            if (steepSlopeSlideSpeed.HasValue) resolvedShared.steepSlopeSlideSpeed = steepSlopeSlideSpeed.Value;
            if (stepPolicy.HasValue) resolvedShared.stepPolicy = stepPolicy.Value;
            if (flyControlMode.HasValue) resolvedShared.flyControlMode = flyControlMode.Value;
            if (flyMaxSpeed.HasValue) resolvedShared.flyMaxSpeed = flyMaxSpeed.Value;
            if (flySprintMultiplier.HasValue) resolvedShared.flySprintMultiplier = flySprintMultiplier.Value;

            if (initialSupportFlag.HasValue) resolvedVariable.initialSupportFlag = initialSupportFlag.Value;
            if (speedMultiplier.HasValue) resolvedVariable.speedMultiplier = speedMultiplier.Value;
            if (speedLimit.HasValue) resolvedVariable.speedLimit = speedLimit.Value;
            if (gravityMultiplier.HasValue) resolvedVariable.gravityMultiplier = gravityMultiplier.Value;
            if (allowMoveInput.HasValue) resolvedVariable.allowMoveInput = allowMoveInput.Value;
            if (allowLookInput.HasValue) resolvedVariable.allowLookInput = allowLookInput.Value;
            if (allowJump.HasValue) resolvedVariable.allowJump = allowJump.Value;
            if (allowMotionModeSwitch.HasValue) resolvedVariable.allowMotionModeSwitch = allowMotionModeSwitch.Value;
            if (allowRootMotion.HasValue) resolvedVariable.allowRootMotion = allowRootMotion.Value;

            fillShared?.Invoke(ref resolvedShared);
            fillVariable?.Invoke(ref resolvedVariable);
            resolvedShared ??= EntityMotionSharedData.Default;
        }
    }
}
