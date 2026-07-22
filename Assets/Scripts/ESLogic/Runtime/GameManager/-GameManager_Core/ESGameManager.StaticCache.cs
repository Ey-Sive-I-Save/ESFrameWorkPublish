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
        public static ESCommandModule CommandModule { get; private set; }
        public static ESInputModule InputModule { get; private set; }
        public static ESRuntimeDataModule RuntimeData { get; private set; }
        public static ESGameObjectPoolModule PoolModule { get; private set; }
        public static ESPhysicsQueryModule PhysicsQueryModule { get; private set; }
        public static ESLODModule LODModule { get; private set; }
        public static ESAssetTable AssetTable { get; private set; } = ESAssetRegistry.Table;
        public static ESConfigKeyTable<ESBuffRuntimeData> BuffData => ESRuntimeDataModule.BuffTable;
        public static ESConfigKeyTable<ESShotRuntimeData> ShotData => ESRuntimeDataModule.ShotTable;
        public static ESConfigKeyTable<ESMonsterRuntimeData> MonsterData => ESRuntimeDataModule.MonsterTable;
        public static ESConfigKeyTable<ESNpcRuntimeData> NpcData => ESRuntimeDataModule.NpcTable;
        public static ESConfigKeyTable<ESWeaponRuntimeData> WeaponData => ESRuntimeDataModule.WeaponTable;
        public static ESConfigKeyTable<ESSkillRuntimeData> SkillData => ESRuntimeDataModule.SkillTable;
        public static ESConfigKeyTable<ESBuffRuntimeData> RuntimeBuffData => ESRuntimeDataGameCore.Buffs;
        public static ESConfigKeyTable<ESShotRuntimeData> RuntimeShotData => ESRuntimeDataGameCore.Shots;
        public static ESConfigKeyTable<ESMonsterRuntimeData> RuntimeMonsterData => ESRuntimeDataGameCore.Monsters;
        public static ESConfigKeyTable<ESNpcRuntimeData> RuntimeNpcData => ESRuntimeDataGameCore.Npcs;
        public static ESConfigKeyTable<ESWeaponRuntimeData> RuntimeWeaponData => ESRuntimeDataGameCore.Weapons;
        public static ESConfigKeyTable<ESSkillRuntimeData> RuntimeSkillData => ESRuntimeDataGameCore.Skills;
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

        public static T GetModuleFast<T>() where T : class, IModule, new()
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

            RuntimeMode.Warmup(RuntimeModeWarmupModeCapacity, RuntimeModeWarmupTagCapacity);

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
            AssetTable = ESAssetRegistry.Table;
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
            CommandModule = null;
            InputModule = null;
            AssetTable = ESAssetRegistry.Table;
            RuntimeData = null;
            PoolModule = null;
            PhysicsQueryModule = null;
            LODModule = null;
            ESCommandServices.Clear();
        }
    }
}
