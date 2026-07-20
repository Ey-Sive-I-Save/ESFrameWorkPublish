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

            ESCommandServices.SetRuntimeMode(RuntimeMode);
            ESCommandServices.SetInputRuntime(InputModule != null ? InputModule.RuntimeInstance : null);
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
            ESCommandServices.Clear();
        }
    }
}
