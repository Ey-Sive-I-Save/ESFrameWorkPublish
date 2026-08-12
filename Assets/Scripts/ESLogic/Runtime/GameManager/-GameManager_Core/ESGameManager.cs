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
        [LabelText("自动创建RuntimeData模块")]
        public bool autoCreateRuntimeDataModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建对象池模块")]
        public bool autoCreateGameObjectPoolModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建音频模块")]
        public bool autoCreateAudioModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建 VFX 模块")]
        public bool autoCreateVfxModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建相机模块")]
        public bool autoCreateCameraModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建物理查询模块")]
        public bool autoCreatePhysicsQueryModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建LOD模块")]
        public bool autoCreateLODModule = true;

        [TabGroup("配置")]
        [LabelText("自动创建运行时动态图集模块")]
        public bool autoCreateDynamicAtlasModule = false;

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

            if (autoCreateRuntimeDataModule)
                GetMoudle<ESRuntimeDataModule>();

            if (autoCreateGameObjectPoolModule)
                GetMoudle<ESGameObjectPoolModule>();

            if (autoCreateAudioModule)
                GetMoudle<ESAudioModule>();

            if (autoCreateVfxModule)
                GetMoudle<ESVfxModule>();

            if (autoCreateCameraModule)
                GetMoudle<ESCameraModule>();

            if (autoCreatePhysicsQueryModule)
                GetMoudle<ESPhysicsQueryModule>();

            if (autoCreateLODModule)
                GetMoudle<ESLODModule>();

            if (autoCreateDynamicAtlasModule)
                GetMoudle<ESDynamicAtlasModule>();

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

        private void OnApplicationFocus(bool hasFocus)
        {
            Audio?.HandleApplicationFocus(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Audio?.HandleApplicationFocus(!pauseStatus);
        }

        // 所有普通相机请求都只标记脏状态；这里是本项目唯一的正常提交点。
        // DefaultExecutionOrder(-9) 使结果在 CinemachineBrain 的 LateUpdate 前写入。
        private void LateUpdate()
        {
            ESZoneMaintenance.Tick();
            Camera?.LateTick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ESDeveloperObservationController.SampleFrame();
#endif
        }
    }
}
