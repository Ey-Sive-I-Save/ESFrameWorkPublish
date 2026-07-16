using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace ES
{
    [DefaultExecutionOrder(-9)]
    public sealed partial class ESGameManager : Core
    {
        /*
         * GameManager / Domain 职责边界摘要：
         *
         * 系统域：长期存在的基础服务，不关心当前关卡，也不关心当前流程。
         * 例如：存档、网络、热更新、语言、配置索引、账号平台、全局日志、全局事件入口。
         *
         * 流程域：当前游戏正在怎么运行，关心启动、加载、菜单、暂停、战斗、剧情等流程状态。
         * 例如：运行模式、输入许可、命令播放、流程切换、暂停恢复。
         *
         * 世界域：当前场景、地图、世界实例内的数据和服务，切关卡时通常要刷新或重建。
         * 例如：当前关卡、出生点、场景对象索引、世界对象池、交互物注册、空间查询。
         *
         * GameManager 只做入口、调度和跨域协调。具体玩法规则放到模块或业务系统里。
         */
        public static ESGameManager Instance { get; private set; }
        public static ESSystemDomain SystemDomain { get; private set; }
        public static ESFlowDomain FlowDomain { get; private set; }
        public static ESWorldDomain WorldDomain { get; private set; }
        public static ESRuntimeModeService RuntimeMode { get; private set; } = new ESRuntimeModeService();
        public static ESCommandModule CommandModule { get; private set; }
        public static ESInputModule InputModule { get; private set; }

        [TabGroup("域", "系统", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public ESSystemDomain systemDomain = new ESSystemDomain();

        [TabGroup("域", "流程", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
        public ESFlowDomain flowDomain = new ESFlowDomain();

        [TabGroup("域", "世界", TabLayouting = TabLayouting.MultiRow, TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
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

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESCommandModule), out IModule commandModule))
                CommandModule = commandModule as ESCommandModule;
            else
                CommandModule = null;

            if (ModuleTables != null && ModuleTables.TryGetValue(typeof(ESInputModule), out IModule inputModule))
                InputModule = inputModule as ESInputModule;
            else
                InputModule = null;
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

    [Serializable]
    [LabelText("系统域")]
    public class ESSystemDomain : Domain<ESGameManager, ESSystemModule>
    {
    }

    [Serializable]
    [LabelText("流程域")]
    public class ESFlowDomain : Domain<ESGameManager, ESFlowModule>
    {
    }

    [Serializable]
    [LabelText("世界域")]
    public class ESWorldDomain : Domain<ESGameManager, ESWorldModule>
    {
    }

    [Serializable]
    [TypeRegistryItem("模块基类/系统模块")]
    public abstract class ESSystemModule : Module<ESGameManager, ESSystemDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }

    [Serializable]
    [TypeRegistryItem("模块基类/流程模块")]
    public abstract class ESFlowModule : Module<ESGameManager, ESFlowDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }

    [Serializable]
    [TypeRegistryItem("模块基类/世界模块")]
    public abstract class ESWorldModule : Module<ESGameManager, ESWorldDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }
}
