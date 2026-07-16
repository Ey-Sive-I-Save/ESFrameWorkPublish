using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public sealed class ESGameManager : Core
    {
        public static ESGameManager Instance { get; private set; }
        public static ESRuntimeDomain RuntimeDomain { get; private set; }
        public static ESWorldDomain WorldDomain { get; private set; }
        public static ESPlayerDomain PlayerDomain { get; private set; }
        public static ESPresentationDomain PresentationDomain { get; private set; }
        public static ESCommandModule CommandModule { get; private set; }

        [TabGroup("\u57df", "\u8fd0\u884c")]
        [LabelText("\u81ea\u52a8\u521b\u5efa\u547d\u4ee4\u6a21\u5757")]
        public bool autoCreateCommandModule = true;

        [TabGroup("\u57df", "\u8fd0\u884c")]
        [LabelText("\u8de8\u573a\u666f\u4e0d\u9500\u6bc1")]
        public bool dontDestroyOnLoad = true;

        [TabGroup("\u57df", "\u8fd0\u884c", TextColor = "@Editor_DomainTabColor(runtimeDomain)")]
        [LabelText("\u8fd0\u884c\u57df")]
        [HideLabel, SerializeReference]
        public ESRuntimeDomain runtimeDomain = new ESRuntimeDomain();

        [TabGroup("\u57df", "\u4e16\u754c", TextColor = "@Editor_DomainTabColor(worldDomain)")]
        [LabelText("\u4e16\u754c\u57df")]
        [HideLabel, SerializeReference]
        public ESWorldDomain worldDomain = new ESWorldDomain();

        [TabGroup("\u57df", "\u73a9\u5bb6", TextColor = "@Editor_DomainTabColor(playerDomain)")]
        [LabelText("\u73a9\u5bb6\u57df")]
        [HideLabel, SerializeReference]
        public ESPlayerDomain playerDomain = new ESPlayerDomain();

        [TabGroup("\u57df", "\u8868\u73b0", TextColor = "@Editor_DomainTabColor(presentationDomain)")]
        [LabelText("\u8868\u73b0\u57df")]
        [HideLabel, SerializeReference]
        public ESPresentationDomain presentationDomain = new ESPresentationDomain();

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            base.Awake();
            CacheStaticReferences();
        }

        protected override void OnAwakeRegisterOnly()
        {
            EnsureDefaultDomains();
            RegisterDomain(runtimeDomain);
            RegisterDomain(worldDomain);
            RegisterDomain(playerDomain);
            RegisterDomain(presentationDomain);
        }

        protected override void OnAfterAwakeRegister()
        {
            if (autoCreateCommandModule)
                GetMoudle<ESCommandModule>();

            CacheStaticReferences();
        }

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
            RuntimeDomain = runtimeDomain;
            WorldDomain = worldDomain;
            PlayerDomain = playerDomain;
            PresentationDomain = presentationDomain;
            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESCommandModule), out IModule module))
                CommandModule = module as ESCommandModule;
            else
                CommandModule = null;
        }

        private void EnsureDefaultDomains()
        {
            if (runtimeDomain == null)
                runtimeDomain = new ESRuntimeDomain();

            if (worldDomain == null)
                worldDomain = new ESWorldDomain();

            if (playerDomain == null)
                playerDomain = new ESPlayerDomain();

            if (presentationDomain == null)
                presentationDomain = new ESPresentationDomain();
        }

        private static void ClearStaticReferences()
        {
            RuntimeDomain = null;
            WorldDomain = null;
            PlayerDomain = null;
            PresentationDomain = null;
            CommandModule = null;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
            {
                Instance = null;
                ClearStaticReferences();
            }
        }
    }
}
