namespace ES
{
    /// <summary>
    /// ES editor menu paths. Keep editor entry names centralized so the top menu stays readable.
    /// </summary>
    public static class MenuItemPathDefine
    {
        public const string ROOT_MENU = "【ES】";
        public const string ROOT_PATH = ROOT_MENU + "/";

        public const string QUICK_WINDOWS = "★ 常用";

        public const string PLUGIN_TOOLS = "框架";
        public const string EDITOR_OPTIMIZATION = "编辑器";
        public const string EDITOR_MAINTENANCE = "编辑器";
        public const string GAMEPLAY_BUILDING = "玩法";
        public const string RUNTIME_TOOLS = "运行时";

        public const string CONFIG = "配置";
        public const string SCENE_TOOLS = "场景工具";
        public const string PREVIEW_CLEANUP = "预览清理";
        public const string RESOURCE_PIPELINE = "资源管线";
        public const string RESOURCE_WINDOW = "资源管理窗口";
        public const string PROJECT_ASSETS = "项目资产";
        public const string ASSET_CREATION = "资产创建";
        public const string INSTALL_DEPENDENCY = "安装与依赖";
        public const string TEST_TOOLS = "测试案例";
        public const string DEBUG = "调试";
        public const string INTERACTION_RUNTIME_PANEL = "交互运行时面板";

        public const string QUICK_WINDOWS_PATH = ROOT_PATH + QUICK_WINDOWS + "/";

        public const string PLUGIN_TOOLS_PATH = ROOT_PATH + PLUGIN_TOOLS + "/";
        public const string EDITOR_OPTIMIZATION_PATH = ROOT_PATH + EDITOR_OPTIMIZATION + "/";
        public const string EDITOR_MAINTENANCE_PATH = ROOT_PATH + EDITOR_MAINTENANCE + "/";
        public const string GAMEPLAY_BUILDING_PATH = ROOT_PATH + GAMEPLAY_BUILDING + "/";
        public const string RUNTIME_TOOLS_PATH = ROOT_PATH + RUNTIME_TOOLS + "/";

        public const string CONFIG_PATH = GAMEPLAY_BUILDING_PATH + CONFIG + "/";
        public const string SCENE_TOOLS_PATH = EDITOR_OPTIMIZATION_PATH + SCENE_TOOLS + "/";
        public const string PREVIEW_CLEANUP_PATH = EDITOR_MAINTENANCE_PATH + PREVIEW_CLEANUP + "/";
        public const string PROJECT_ASSETS_PATH = PLUGIN_TOOLS_PATH + PROJECT_ASSETS + "/";
        public const string RESOURCE_PIPELINE_PATH = PLUGIN_TOOLS_PATH + RESOURCE_PIPELINE + "/";
        public const string RESOURCE_WINDOW_PATH = RESOURCE_PIPELINE_PATH + RESOURCE_WINDOW;
        public const string ASSET_CREATION_PATH = PLUGIN_TOOLS_PATH + ASSET_CREATION + "/";
        public const string INSTALL_DEPENDENCY_PATH = PLUGIN_TOOLS_PATH + INSTALL_DEPENDENCY + "/";
        public const string TEST_TOOLS_PATH = GAMEPLAY_BUILDING_PATH + TEST_TOOLS + "/";
        public const string DEBUG_PATH = PLUGIN_TOOLS_PATH + DEBUG + "/";
        public const string INTERACTION_RUNTIME_PANEL_PATH = RUNTIME_TOOLS_PATH + INTERACTION_RUNTIME_PANEL;

        public const string ASSET_GLOBAL_SO = "全局 SO";
        public const string ASSET_DEV_MANAGEMENT = "开发管理";
        public const string ASSET_DOCUMENTATION = "文档";

        public const string ASSET_GLOBAL_SO_PATH = ASSET_CREATION_PATH + ASSET_GLOBAL_SO + "/";
        public const string ASSET_DEV_MANAGEMENT_PATH = ASSET_CREATION_PATH + ASSET_DEV_MANAGEMENT + "/";
        public const string ASSET_DOCUMENTATION_PATH = ASSET_CREATION_PATH + ASSET_DOCUMENTATION + "/";

        // Legacy alias: old code can still compile while new entries use QUICK_WINDOWS_PATH.
        public const string EDITOR_TOOLS = QUICK_WINDOWS;
        public const string EDITOR_TOOLS_PATH = QUICK_WINDOWS_PATH;
        public const string EDITOR_DOCS = "文档";
        public const string EDITOR_DOCS_PATH = QUICK_WINDOWS_PATH + EDITOR_DOCS + "/";
    }
}
