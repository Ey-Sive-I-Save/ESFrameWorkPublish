using System;

namespace ES
{
    public sealed partial class ESGameManager
    {
        private const int RuntimeModeWarmupModeCapacity = 32;
        private const int RuntimeModeWarmupTagCapacity = 64;

        public static ESGameManager Instance { get; private set; }
        public static ESSystemDomain SystemDomain { get; private set; }
        public static ESFlowDomain FlowDomain { get; private set; }
        public static ESWorldDomain WorldDomain { get; private set; }
        public static ESRuntimeModeService RuntimeMode { get; private set; } = new ESRuntimeModeService();
        public static ESLocalControlService LocalControl { get; private set; } = new ESLocalControlService();
        public static ESCommandModule CommandModule { get; private set; }
        public static ESInputModule InputModule { get; private set; }
        public static ESRuntimeDataModule RuntimeData { get; private set; }
        public static ESGameObjectPoolModule PoolModule { get; private set; }
        public static ESResourcePlanRuntimeService ResourcePlans { get; private set; }
        public static ESPhysicsQueryModule PhysicsQueryModule { get; private set; }
        public static ESLODModule LODModule { get; private set; }
        public static ESConfigKeyTable<ESBuffRuntimeData> BuffData => ESRuntimeDataModule.BuffTable;
        public static ESConfigKeyTable<ESShotRuntimeData> ShotData => ESRuntimeDataModule.ShotTable;
        public static ESConfigKeyTable<ESMonsterRuntimeData> MonsterData => ESRuntimeDataModule.MonsterTable;
        public static ESConfigKeyTable<ESNpcRuntimeData> NpcData => ESRuntimeDataModule.NpcTable;
        public static ESConfigKeyTable<ESWeaponRuntimeData> WeaponData => ESRuntimeDataModule.WeaponTable;
        public static ESConfigKeyTable<ESSkillRuntimeData> SkillData => ESRuntimeDataModule.SkillTable;
        public static ESBuffConfigKeyTable RuntimeBuffData => ESRuntimeDataGameCore.Buffs;
        public static ESShotConfigKeyTable RuntimeShotData => ESRuntimeDataGameCore.Shots;
        public static ESMonsterConfigKeyTable RuntimeMonsterData => ESRuntimeDataGameCore.Monsters;
        public static ESNpcConfigKeyTable RuntimeNpcData => ESRuntimeDataGameCore.Npcs;
        public static ESWeaponConfigKeyTable RuntimeWeaponData => ESRuntimeDataGameCore.Weapons;
        public static ESSkillConfigKeyTable RuntimeSkillData => ESRuntimeDataGameCore.Skills;
        public static ESConfigKeyTable<ESAssetReferPrefabConfigData> RuntimePrefabAssets => ESRuntimeDataAsset.Prefabs;
        public static ESConfigKeyTable<ESAssetReferSpriteConfigData> RuntimeSpriteAssets => ESRuntimeDataAsset.Sprites;
        public static ESConfigKeyTable<ESAssetReferAudioClipConfigData> RuntimeAudioClipAssets => ESRuntimeDataAsset.AudioClips;
        public static ESConfigKeyTable<ESAssetReferAnimationClipConfigData> RuntimeAnimationClipAssets => ESRuntimeDataAsset.AnimationClips;
        public static ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffRuntimeInstances => ESRuntimeDataModule.BuffInstanceIndex;
        public static ESRuntimeInstanceIndex<Item> ShotRuntimeInstances => ESRuntimeDataModule.ShotInstanceIndex;

        public static bool IsReady
        {
            get { return Instance != null; }
        }

        /// <summary>
        /// Performs a read-only module lookup. It never constructs or registers a module, so it
        /// is safe for polling, input, physics and other hot paths.
        /// </summary>
        public static bool TryGetModule<T>(out T module) where T : class, IModule
        {
            ESGameManager manager = Instance;
            if (manager != null && manager.ModuleTables != null
                && manager.ModuleTables.TryGetValue(typeof(T), out IModule registered))
            {
                module = registered as T;
                return module != null;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// Explicit initialization-only entry. A missing module is constructed and registered by
        /// the Core. Never call this from a hot path.
        /// </summary>
        public static T GetOrCreateModule<T>() where T : class, IModule, new()
        {
            ESGameManager manager = Instance;
            return manager != null ? manager.GetMoudle<T>() : null;
        }

        public static void RefreshStaticCache()
        {
            ESGameManager manager = Instance;
            if (manager == null)
            {
                ClearStaticReferences();
                return;
            }

            manager.CacheStaticReferences();
        }

        private void CacheStaticReferences()
        {
            EnsureDefaultDomains();
            SystemDomain = systemDomain;
            FlowDomain = flowDomain;
            WorldDomain = worldDomain;

            if (RuntimeMode == null)
                RuntimeMode = new ESRuntimeModeService();
            if (LocalControl == null)
                LocalControl = new ESLocalControlService();
            if (ResourcePlans == null)
                ResourcePlans = new ESResourcePlanRuntimeService();

            RuntimeMode.Warmup(RuntimeModeWarmupModeCapacity, RuntimeModeWarmupTagCapacity);
            LocalControl.SetRuntimeModeService(RuntimeMode);

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESCommandModule), out IModule commandModule))
                CommandModule = commandModule as ESCommandModule;
            else
                CommandModule = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESInputModule), out IModule inputModule))
                InputModule = inputModule as ESInputModule;
            else
                InputModule = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESRuntimeDataModule), out IModule runtimeDataModule))
                RuntimeData = runtimeDataModule as ESRuntimeDataModule;
            else
                RuntimeData = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESGameObjectPoolModule), out IModule poolModule))
                PoolModule = poolModule as ESGameObjectPoolModule;
            else
                PoolModule = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESPhysicsQueryModule), out IModule physicsQueryModule))
                PhysicsQueryModule = physicsQueryModule as ESPhysicsQueryModule;
            else
                PhysicsQueryModule = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESLODModule), out IModule lodModule))
                LODModule = lodModule as ESLODModule;
            else
                LODModule = null;

            ESCommandServices.SetRuntimeMode(RuntimeMode);
            ESCommandServices.SetInputModule(InputModule);
        }

        private void EnsureDefaultDomains()
        {
            if (systemDomain == null)
                systemDomain = new ESSystemDomain();

            if (flowDomain == null)
                flowDomain = new ESFlowDomain();

            if (worldDomain == null)
                worldDomain = new ESWorldDomain();
        }

        private static void ClearStaticReferences()
        {
            SystemDomain = null;
            FlowDomain = null;
            WorldDomain = null;
            RuntimeMode = null;
            LocalControl?.Dispose();
            LocalControl = new ESLocalControlService();
            CommandModule = null;
            InputModule = null;
            RuntimeData = null;
            PoolModule = null;
            PhysicsQueryModule = null;
            LODModule = null;
            ResourcePlans?.Dispose();
            ResourcePlans = null;
            ESCommandServices.Clear();
        }
    }

    /// <summary>
    /// Single authority for the locally controlled Entity. It owns the GameTag to RuntimeMode
    /// projector so NPC facts can never alter local input or UI policy.
    /// </summary>
    public sealed class ESLocalControlService : IDisposable
    {
        private readonly ESGameTagRuntimeModeProjector projector = new ESGameTagRuntimeModeProjector();
        private Entity controlledEntity;
        private ESRuntimeModeService runtimeModeService;

        public Entity ControlledEntity
        {
            get { return controlledEntity; }
        }

        public bool HasControlledEntity
        {
            get { return controlledEntity != null; }
        }

        public event Action<Entity, Entity> OnControlledEntityChanged;

        public bool IsLocallyControlled(Entity entity)
        {
            return ReferenceEquals(controlledEntity, entity);
        }

        /// <summary>
        /// Claims control only when it is unowned or already owned by the caller. Spawn and
        /// possession flows that intentionally replace a player should call SetControlledEntity.
        /// </summary>
        public bool TryClaim(Entity entity, ESRuntimeModeService modeService = null)
        {
            if (entity == null)
                return false;

            if (controlledEntity != null && !ReferenceEquals(controlledEntity, entity))
                return false;

            SetControlledEntity(entity, modeService);
            return true;
        }

        public bool Release(Entity entity)
        {
            if (!ReferenceEquals(controlledEntity, entity))
                return false;

            SetControlledEntity(null, runtimeModeService);
            return true;
        }

        public void SetControlledEntity(Entity entity, ESRuntimeModeService modeService = null)
        {
            ESRuntimeModeService resolvedModeService = modeService ?? ESGameManager.RuntimeMode;
            if (ReferenceEquals(controlledEntity, entity)
                && ReferenceEquals(runtimeModeService, resolvedModeService)
                && (entity == null || projector.IsBound))
                return;

            Entity previous = controlledEntity;
            projector.Dispose();
            controlledEntity = entity;
            runtimeModeService = resolvedModeService;
            if (controlledEntity != null && runtimeModeService != null)
                projector.Bind(controlledEntity, runtimeModeService);

            if (!ReferenceEquals(previous, controlledEntity))
                OnControlledEntityChanged?.Invoke(previous, controlledEntity);
        }

        public void SetRuntimeModeService(ESRuntimeModeService modeService)
        {
            if (ReferenceEquals(runtimeModeService, modeService))
                return;

            runtimeModeService = modeService;
            if (controlledEntity != null)
                projector.Bind(controlledEntity, runtimeModeService);
        }

        public void Dispose()
        {
            Entity previous = controlledEntity;
            projector.Dispose();
            controlledEntity = null;
            runtimeModeService = null;
            if (previous != null)
                OnControlledEntityChanged?.Invoke(previous, null);
        }
    }
}
