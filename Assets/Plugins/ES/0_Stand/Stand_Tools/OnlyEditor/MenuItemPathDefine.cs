namespace ES
{
    /// <summary>ES Unity 菜单信息架构。顶部菜单、资产创建菜单和组件菜单必须分别建模。</summary>
    public static class MenuItemPathDefine
    {
        public const string ROOT_MENU = "\u3010ES\u3011";
        public const string ROOT_PATH = ROOT_MENU + "/";

        // Unity 顶部菜单：五个正式业务域 + 一个只打开窗口的快捷投影。
        public const string QUICK_ACCESS = "常用窗口";
        public const string CONTENT_CREATION = "内容制作";
        public const string PROJECT_CONFIGURATION = "项目配置";
        public const string RESOURCE_DELIVERY = "资源与发布";
        public const string VALIDATION_DIAGNOSTICS = "验证与诊断";
        public const string AUTOMATION_DEVELOPMENT = "自动化与开发";

        public const string QUICK_ACCESS_PATH = ROOT_PATH + QUICK_ACCESS + "/";
        public const string CONTENT_CREATION_PATH = ROOT_PATH + CONTENT_CREATION + "/";
        public const string PROJECT_CONFIGURATION_PATH = ROOT_PATH + PROJECT_CONFIGURATION + "/";
        public const string RESOURCE_DELIVERY_PATH = ROOT_PATH + RESOURCE_DELIVERY + "/";
        public const string VALIDATION_DIAGNOSTICS_PATH = ROOT_PATH + VALIDATION_DIAGNOSTICS + "/";
        public const string AUTOMATION_DEVELOPMENT_PATH = ROOT_PATH + AUTOMATION_DEVELOPMENT + "/";

        public const string CONTENT_SCENE_OBJECTS_PATH = CONTENT_CREATION_PATH + "场景与对象/";
        public const string VALIDATION_RUNTIME_MONITORING_PATH = VALIDATION_DIAGNOSTICS_PATH + "运行时监视/";
        public const string VALIDATION_STATIC_AUDIT_PATH = VALIDATION_DIAGNOSTICS_PATH + "静态审计/";
        public const string VALIDATION_TEST_ACCEPTANCE_PATH = VALIDATION_DIAGNOSTICS_PATH + "测试与验收/";
        public const string VALIDATION_ENVIRONMENT_PATH = VALIDATION_DIAGNOSTICS_PATH + "验证环境/";
        public const string VALIDATION_PERFORMANCE_PATH = VALIDATION_DIAGNOSTICS_PATH + "性能诊断/";
        public const string VALIDATION_EDITOR_HEALTH_PATH = VALIDATION_DIAGNOSTICS_PATH + "编辑器健康/";
        public const string VALIDATION_EDITOR_EXTENSION_TESTS_PATH = VALIDATION_TEST_ACCEPTANCE_PATH + "编辑器扩展/";
        public const string VALIDATION_CLEANUP_RECOVERY_PATH = VALIDATION_DIAGNOSTICS_PATH + "清理与恢复/";
        public const string AUTOMATION_AGENT_COLLABORATION_PATH = AUTOMATION_DEVELOPMENT_PATH + "Agent 与协作/";
        public const string AUTOMATION_CENTER_PATH = AUTOMATION_DEVELOPMENT_PATH + "自动化中心/";
        public const string AUTOMATION_AI_CONTROL_PATH = AUTOMATION_DEVELOPMENT_PATH + "AI 控制/";
        public const string AUTOMATION_EDITOR_EXTENSIONS_PATH = AUTOMATION_DEVELOPMENT_PATH + "编辑器扩展/";
        public const string AUTOMATION_DEPENDENCIES_PATH = AUTOMATION_DEVELOPMENT_PATH + "依赖与集成/";
        public const string AUTOMATION_PROJECT_ASSETS_PATH = AUTOMATION_DEVELOPMENT_PATH + "项目资产职责/";
        public const string AUTOMATION_DOCS_SAMPLES_PATH = AUTOMATION_DEVELOPMENT_PATH + "文档与示例/";
        public const string AUTOMATION_LEGACY_PATH = AUTOMATION_DEVELOPMENT_PATH + "遗留兼容/";

        // Assets/Create/【ES】：按资产类型分类。Attribute 的 menuName 不包含 Assets/Create 前缀。
        public const string ASSET_CONTENT_PATH = ROOT_PATH + "内容/";
        public const string ASSET_CONFIGURATION_PATH = ROOT_PATH + "配置/";
        public const string ASSET_RESOURCE_PIPELINE_PATH = ROOT_PATH + "资源管线/";
        public const string ASSET_SAMPLES_PATH = ROOT_PATH + "示例/";
        public const string ASSET_CREATE_CONTEXT_PATH = "Assets/Create/" + ROOT_PATH;
        public const string ASSET_CREATE_CONTENT_CONTEXT_PATH = "Assets/Create/" + ASSET_CONTENT_PATH;
        public const string ASSET_CREATE_CONFIGURATION_CONTEXT_PATH = "Assets/Create/" + ASSET_CONFIGURATION_PATH;
        public const string ASSET_CREATE_RESOURCE_PIPELINE_CONTEXT_PATH = "Assets/Create/" + ASSET_RESOURCE_PIPELINE_PATH;
        public const string ASSET_CREATE_SAMPLES_CONTEXT_PATH = "Assets/Create/" + ASSET_SAMPLES_PATH;

        // Add Component/【ES】：按 GameObject 获得的组件能力分类。
        public const string COMPONENT_INFRASTRUCTURE_PATH = ROOT_PATH + "基础设施/";
        public const string COMPONENT_CHARACTER_INTERACTION_PATH = ROOT_PATH + "角色与交互/";
        public const string COMPONENT_CAMERA_PRESENTATION_PATH = ROOT_PATH + "相机与表现/";
        public const string COMPONENT_UI_PATH = ROOT_PATH + "UI/";
        public const string COMPONENT_RESOURCE_PATH = ROOT_PATH + "资源/";
        public const string COMPONENT_DEVELOPMENT_VALIDATION_PATH = ROOT_PATH + "开发与验证/";

        public const string QUICK_WINDOWS_PATH = QUICK_ACCESS_PATH;
        public const string GAMEPLAY_BUILDING_PATH = CONTENT_CREATION_PATH;
        public const string RESOURCE_PIPELINE_PATH = RESOURCE_DELIVERY_PATH;
        public const string SCENE_TOOLS_PATH = CONTENT_SCENE_OBJECTS_PATH;
        public const string RUNTIME_TOOLS_PATH = VALIDATION_RUNTIME_MONITORING_PATH;
        public const string CONFIG_PATH = PROJECT_CONFIGURATION_PATH + "数据管线/";
        public const string PREVIEW_CLEANUP_PATH = VALIDATION_CLEANUP_RECOVERY_PATH + "预览/";
        public const string PROJECT_ASSETS_PATH = AUTOMATION_PROJECT_ASSETS_PATH;
        public const string RESOURCE_WINDOW_PATH = RESOURCE_DELIVERY_PATH + "资源管理/打开资源管理窗口";
        public const string SO_DATA_WINDOW_PATH = CONTENT_CREATION_PATH + "数据与配置/SO 数据窗口";
        public const string SIMPLE_TOOLS_WINDOW_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH + "打开简单工具集";
        public const string RUNTIME_WATCH_WINDOW_PATH = VALIDATION_RUNTIME_MONITORING_PATH + "RuntimeWatch/打开运行时观察";
        public const string TRACK_EDITOR_WINDOW_PATH = CONTENT_CREATION_PATH + "技能与轨道/轨道编辑器";
        public const string STABLE_GRAPH_WINDOW_PATH = CONTENT_CREATION_PATH + "图与流程/稳定图编辑器 V2";
        public const string FONT_WORKBENCH_WINDOW_PATH = CONTENT_CREATION_PATH + "UI 与字体/字体资产工具";
        public const string LOCALIZATION_WORKBENCH_WINDOW_PATH = CONTENT_CREATION_PATH + "UI 与字体/本地化工具";
        public const string AGENT_WORKBENCH_WINDOW_PATH = AUTOMATION_AGENT_COLLABORATION_PATH + "打开 Agent 控制台";
        public const string COMMAND_PALETTE_WINDOW_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH + "打开 ES 命令面板";
        public const string WINDOW_LAUNCHER_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH + "打开工具启动器";
        public const string ASSET_CREATION_PATH = ASSET_CONTENT_PATH;
        public const string INSTALL_DEPENDENCY_PATH = AUTOMATION_DEPENDENCIES_PATH;
        public const string TEST_TOOLS_PATH = VALIDATION_EDITOR_EXTENSION_TESTS_PATH;
        public const string SAMPLE_TOOLS_PATH = AUTOMATION_DOCS_SAMPLES_PATH + "编辑器示例/";
        public const string DEBUG_PATH = VALIDATION_EDITOR_HEALTH_PATH;
        public const string AUTOMATION_PATH = AUTOMATION_DEVELOPMENT_PATH;
        public const string INTERACTION_RUNTIME_PANEL_PATH = VALIDATION_RUNTIME_MONITORING_PATH + "交互系统/打开运行时面板";
        public const string STAT_RUNTIME_PANEL_PATH = VALIDATION_RUNTIME_MONITORING_PATH + "属性系统/打开运行时面板";

        public const string ASSET_GLOBAL_SO = "\u5168\u5c40 SO";
        public const string ASSET_DEV_MANAGEMENT = "\u5f00\u53d1\u7ba1\u7406";
        public const string ASSET_DOCUMENTATION = "\u6587\u6863";
        public const string ASSET_GLOBAL_SO_PATH = ASSET_CONFIGURATION_PATH + "全局配置/";
        public const string ASSET_DEV_MANAGEMENT_PATH = ASSET_CONFIGURATION_PATH + "编辑器维护/";
        public const string ASSET_DOCUMENTATION_PATH = ASSET_SAMPLES_PATH + "文档/";

        public const string EDITOR_TOOLS = AUTOMATION_DEVELOPMENT;
        public const string EDITOR_TOOLS_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH;
        public const string EDITOR_DOCS = "\u6587\u6863";
        public const string EDITOR_DOCS_PATH = AUTOMATION_DOCS_SAMPLES_PATH + "文档工具/";

        // 旧标识仅保留源码兼容，值全部投影到六域新树；新代码不得继续引用。
        public const string QUICK_WINDOWS = QUICK_ACCESS;
        public const string SCENE_OBJECTS = CONTENT_CREATION;
        public const string RUNTIME_DIAGNOSTICS = VALIDATION_DIAGNOSTICS;
        public const string PROJECT_SETTINGS = PROJECT_CONFIGURATION;
        public const string AUTOMATION = AUTOMATION_DEVELOPMENT;
        public const string DEVELOPMENT_MAINTENANCE = AUTOMATION_DEVELOPMENT;
        public const string INSTALL_INTEGRATION = AUTOMATION_DEVELOPMENT;
        public const string SAMPLES_TESTS = AUTOMATION_DEVELOPMENT;
        public const string OBSOLETE = AUTOMATION_DEVELOPMENT;
        public const string SCENE_OBJECTS_PATH = CONTENT_SCENE_OBJECTS_PATH;
        public const string RUNTIME_DIAGNOSTICS_PATH = VALIDATION_RUNTIME_MONITORING_PATH;
        public const string PROJECT_SETTINGS_PATH = PROJECT_CONFIGURATION_PATH;
        public const string DEVELOPMENT_MAINTENANCE_PATH = AUTOMATION_DEVELOPMENT_PATH;
        public const string INSTALL_INTEGRATION_PATH = AUTOMATION_DEPENDENCIES_PATH;
        public const string SAMPLES_TESTS_PATH = AUTOMATION_DOCS_SAMPLES_PATH;
        public const string OBSOLETE_PATH = AUTOMATION_LEGACY_PATH;
        public const string PLUGIN_TOOLS = AUTOMATION_DEVELOPMENT;
        public const string EDITOR_OPTIMIZATION = AUTOMATION_DEVELOPMENT;
        public const string EDITOR_MAINTENANCE = AUTOMATION_DEVELOPMENT;
        public const string GAMEPLAY_BUILDING = CONTENT_CREATION;
        public const string RUNTIME_TOOLS = VALIDATION_DIAGNOSTICS;
        public const string PLUGIN_TOOLS_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH;
        public const string EDITOR_OPTIMIZATION_PATH = AUTOMATION_EDITOR_EXTENSIONS_PATH;
        public const string EDITOR_MAINTENANCE_PATH = AUTOMATION_DEVELOPMENT_PATH;
    }
}
