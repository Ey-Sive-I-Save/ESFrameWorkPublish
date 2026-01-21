using UnityEditor;

namespace ES.ESInstaller
{
    /// <summary>
    /// ES框架菜单路径定义常量类
    /// 统一管理所有MenuItem和CreateAssetMenu的路径
    /// </summary>
    public static class MenuItemPathDefine
    {
        // 根菜单
        public const string ROOT_MENU = "【ES】";

        // 安装与依赖
        public const string INSTALL_DEPENDENCY_PATH = ROOT_MENU + "/安装与依赖/";
    }
}