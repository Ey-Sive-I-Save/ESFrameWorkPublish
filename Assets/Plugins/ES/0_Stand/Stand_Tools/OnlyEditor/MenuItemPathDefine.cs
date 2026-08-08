namespace ES
{
    public static class MenuItemPathDefine
    {
        public const string ROOT_MENU = "\u3010ES\u3011";
        public const string ROOT_PATH = ROOT_MENU + "/";

        // 一级域只表达用户意图，禁止再使用“插件级”“Tools”“Runtime Data”等实现名充当分类。
        public const string QUICK_ACCESS = "常用窗口";
        public const string CONTENT_CREATION = "内容制作";
        public const string RESOURCE_DELIVERY = "资源与发布";
        public const string SCENE_OBJECTS = "场景与对象";
        public const string RUNTIME_DIAGNOSTICS = "运行时诊断";
        public const string PROJECT_SETTINGS = "项目设置";
        public const string AUTOMATION = "自动化";
        public const string DEVELOPMENT_MAINTENANCE = "开发与维护";
        public const string INSTALL_INTEGRATION = "安装与集成";
        public const string SAMPLES_TESTS = "示例与测试";
        public const string OBSOLETE = "已废弃";

        public const string QUICK_ACCESS_PATH = ROOT_PATH + QUICK_ACCESS + "/";
        public const string CONTENT_CREATION_PATH = ROOT_PATH + CONTENT_CREATION + "/";
        public const string RESOURCE_DELIVERY_PATH = ROOT_PATH + RESOURCE_DELIVERY + "/";
        public const string SCENE_OBJECTS_PATH = ROOT_PATH + SCENE_OBJECTS + "/";
        public const string RUNTIME_DIAGNOSTICS_PATH = ROOT_PATH + RUNTIME_DIAGNOSTICS + "/";
        public const string PROJECT_SETTINGS_PATH = ROOT_PATH + PROJECT_SETTINGS + "/";
        public const string DEVELOPMENT_MAINTENANCE_PATH = ROOT_PATH + DEVELOPMENT_MAINTENANCE + "/";
        public const string INSTALL_INTEGRATION_PATH = ROOT_PATH + INSTALL_INTEGRATION + "/";
        public const string SAMPLES_TESTS_PATH = ROOT_PATH + SAMPLES_TESTS + "/";
        public const string OBSOLETE_PATH = ROOT_PATH + OBSOLETE + "/";

        // “常用窗口”是独立的高频窗口入口；工具的正式归属仍由下面的业务路径表达。
        public const string QUICK_WINDOWS_PATH = QUICK_ACCESS_PATH;
        public const string GAMEPLAY_BUILDING_PATH = CONTENT_CREATION_PATH;
        public const string RESOURCE_PIPELINE_PATH = RESOURCE_DELIVERY_PATH;
        public const string SCENE_TOOLS_PATH = SCENE_OBJECTS_PATH;
        public const string RUNTIME_TOOLS_PATH = RUNTIME_DIAGNOSTICS_PATH;
        public const string CONFIG_PATH = PROJECT_SETTINGS_PATH + "数据管线/";
        public const string PREVIEW_CLEANUP_PATH = SCENE_OBJECTS_PATH + "预览与清理/";
        public const string PROJECT_ASSETS_PATH = DEVELOPMENT_MAINTENANCE_PATH + "项目资产职责/";
        public const string RESOURCE_WINDOW_PATH = RESOURCE_DELIVERY_PATH + "资源管理/资源管理窗口";
        public const string ASSET_CREATION_PATH = PROJECT_SETTINGS_PATH + "创建数据资产/";
        public const string ASSET_CREATE_CONTEXT_PATH = "Assets/Create/" + ROOT_PATH;
        public const string INSTALL_DEPENDENCY_PATH = INSTALL_INTEGRATION_PATH + "依赖管理/";
        public const string TEST_TOOLS_PATH = SAMPLES_TESTS_PATH + "编辑器案例/";
        public const string DEBUG_PATH = DEVELOPMENT_MAINTENANCE_PATH + "自检与调试/";
        public const string AUTOMATION_PATH = ROOT_PATH + AUTOMATION + "/";
        public const string INTERACTION_RUNTIME_PANEL_PATH = RUNTIME_DIAGNOSTICS_PATH + "交互系统/运行时面板";
        public const string STAT_RUNTIME_PANEL_PATH = RUNTIME_DIAGNOSTICS_PATH + "属性系统/运行时面板";

        // 兼容旧调用名，值已指向新的语义分类；新代码不得继续扩散这些旧名。
        public const string QUICK_WINDOWS = QUICK_ACCESS;
        public const string PLUGIN_TOOLS = DEVELOPMENT_MAINTENANCE;
        public const string EDITOR_OPTIMIZATION = DEVELOPMENT_MAINTENANCE;
        public const string EDITOR_MAINTENANCE = DEVELOPMENT_MAINTENANCE;
        public const string GAMEPLAY_BUILDING = CONTENT_CREATION;
        public const string RUNTIME_TOOLS = RUNTIME_DIAGNOSTICS;
        public const string PLUGIN_TOOLS_PATH = DEVELOPMENT_MAINTENANCE_PATH;
        public const string EDITOR_OPTIMIZATION_PATH = DEVELOPMENT_MAINTENANCE_PATH + "综合工具/";
        public const string EDITOR_MAINTENANCE_PATH = DEVELOPMENT_MAINTENANCE_PATH;

        public const string ASSET_GLOBAL_SO = "\u5168\u5c40 SO";
        public const string ASSET_DEV_MANAGEMENT = "\u5f00\u53d1\u7ba1\u7406";
        public const string ASSET_DOCUMENTATION = "\u6587\u6863";

        public const string ASSET_GLOBAL_SO_PATH = PROJECT_SETTINGS_PATH + "全局配置/";
        public const string ASSET_DEV_MANAGEMENT_PATH = DEVELOPMENT_MAINTENANCE_PATH + "维护数据/";
        public const string ASSET_DOCUMENTATION_PATH = DEVELOPMENT_MAINTENANCE_PATH + "文档数据/";

        public const string EDITOR_TOOLS = DEVELOPMENT_MAINTENANCE;
        public const string EDITOR_TOOLS_PATH = DEVELOPMENT_MAINTENANCE_PATH + "综合工具/";
        public const string EDITOR_DOCS = "\u6587\u6863";
        public const string EDITOR_DOCS_PATH = DEVELOPMENT_MAINTENANCE_PATH + "文档工具/";
    }
}
