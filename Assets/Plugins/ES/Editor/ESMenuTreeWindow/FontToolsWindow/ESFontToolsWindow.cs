using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESFontToolsWindow : ESMenuTreeWindowAB<ESFontToolsWindow>
    {
        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "UI 与字体/字体资产工作台", false, 20)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "字体资产工作台", false, -945)]
        public static void TryOpenWindow()
        {
            ESWindowCommandRegistry.RecordOpened("font_workbench");
            OpenWindow();
        }

        [System.NonSerialized] private Page_FontBuild fontBuildPage;

        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 字体资产工作台", "管理 TXT 字符集、TMP 字体构建和 Fallback 链。");

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            tree.Config.DrawSearchToolbar = true;
            QuickBuildRootMenu(tree, "01 字体方案与构建", ref fontBuildPage, SdfIconType.Fonts);
        }
    }
}
