namespace ES
{
    public static class MenuItemPathDefine
    {
        public const string ROOT_MENU = "\u3010ES\u3011";
        public const string ROOT_PATH = ROOT_MENU + "/";

        public const string QUICK_WINDOWS = "\u5e38\u7528\u7a97\u53e3";

        public const string PLUGIN_TOOLS = "\u63d2\u4ef6\u7ea7";
        public const string EDITOR_OPTIMIZATION = "\u7f16\u8f91\u5668\u4f18\u5316";
        public const string EDITOR_MAINTENANCE = "\u7f16\u8f91\u5668\u7ef4\u62a4";
        public const string GAMEPLAY_BUILDING = "\u73a9\u6cd5\u642d\u5efa";
        public const string RUNTIME_TOOLS = "\u8fd0\u884c\u65f6";

        public const string CONFIG = "\u914d\u7f6e";
        public const string SCENE_TOOLS = "\u573a\u666f\u5de5\u5177";
        public const string PREVIEW_CLEANUP = "\u9884\u89c8\u6e05\u7406";
        public const string RESOURCE_PIPELINE = "\u8d44\u6e90\u7ba1\u7ebf";
        public const string RESOURCE_WINDOW = "\u8d44\u6e90\u7ba1\u7406\u7a97\u53e3";
        public const string PROJECT_ASSETS = "\u9879\u76ee\u8d44\u4ea7";
        public const string ASSET_CREATION = "\u8d44\u4ea7\u521b\u5efa";
        public const string INSTALL_DEPENDENCY = "\u5b89\u88c5\u4e0e\u4f9d\u8d56";
        public const string TEST_TOOLS = "\u6d4b\u8bd5\u6848\u4f8b";
        public const string DEBUG = "\u8c03\u8bd5";
        public const string INTERACTION_RUNTIME_PANEL = "\u4ea4\u4e92\u8fd0\u884c\u65f6\u9762\u677f";

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

        public const string ASSET_GLOBAL_SO = "\u5168\u5c40 SO";
        public const string ASSET_DEV_MANAGEMENT = "\u5f00\u53d1\u7ba1\u7406";
        public const string ASSET_DOCUMENTATION = "\u6587\u6863";

        public const string ASSET_GLOBAL_SO_PATH = ASSET_CREATION_PATH + ASSET_GLOBAL_SO + "/";
        public const string ASSET_DEV_MANAGEMENT_PATH = ASSET_CREATION_PATH + ASSET_DEV_MANAGEMENT + "/";
        public const string ASSET_DOCUMENTATION_PATH = ASSET_CREATION_PATH + ASSET_DOCUMENTATION + "/";

        public const string EDITOR_TOOLS = QUICK_WINDOWS;
        public const string EDITOR_TOOLS_PATH = QUICK_WINDOWS_PATH;
        public const string EDITOR_DOCS = "\u6587\u6863";
        public const string EDITOR_DOCS_PATH = QUICK_WINDOWS_PATH + EDITOR_DOCS + "/";
    }
}
