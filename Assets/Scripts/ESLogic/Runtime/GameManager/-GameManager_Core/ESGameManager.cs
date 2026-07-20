using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DefaultExecutionOrder(-9)]
    public sealed partial class ESGameManager : Core
    {
        [TabGroup("核心", "系统", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public ESSystemDomain systemDomain = new ESSystemDomain();

        [TabGroup("核心", "流程", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public ESFlowDomain flowDomain = new ESFlowDomain();

        [TabGroup("核心", "世界", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public ESWorldDomain worldDomain = new ESWorldDomain();

        [TabGroup("设置")]
        [LabelText("自动创建命令模块")]
        public bool autoCreateCommandModule = true;

        [TabGroup("设置")]
        [LabelText("自动创建输入模块")]
        public bool autoCreateInputModule = true;

        [TabGroup("设置")]
        [LabelText("跨场景不销毁")]
        public bool dontDestroyOnLoad = true;

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
            base.OnAwakeRegisterOnly();
            EnsureDefaultDomains();
            RegisterDomain(systemDomain);
            RegisterDomain(flowDomain);
            RegisterDomain(worldDomain);
        }

        protected override void OnAfterAwakeRegister()
        {
            if (autoCreateCommandModule)
                GetMoudle<ESCommandModule>();

            if (autoCreateInputModule)
                GetMoudle<ESInputModule>();

            CacheStaticReferences();
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
