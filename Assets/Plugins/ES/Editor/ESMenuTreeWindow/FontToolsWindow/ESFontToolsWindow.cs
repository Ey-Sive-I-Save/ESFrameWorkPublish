using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESFontToolsWindow : ESMenuTreeWindow<ESFontToolsWindow>
    {
        [MenuItem(MenuItemPathDefine.FONT_WORKBENCH_WINDOW_PATH, false, 20)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "字体资产工作台", false, -945)]
        public static void TryOpenWindow()
        {
            ESWindowCommandRegistry.RecordOpened("font_workbench");
            OpenWindow();
        }

        [System.NonSerialized] private Page_FontBuild fontBuildPage;

        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 字体资产工作台", "管理 TXT 字符集、TMP 字体构建和 Fallback 链。");
        protected override string ESWindow_Subtitle => "字体方案、字符收集、TMP 构建与 Fallback 管理";
        protected override Vector2 ESWindow_MinSize => new Vector2(760f, 540f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1120f, 760f);

        protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            fontBuildPage ??= new Page_FontBuild();
            builder.Add(ESMenuTreePageDefinition
                .ForOdin("font.build", "字体 / 方案与构建", fontBuildPage)
                .WithUnityIcon("Font Icon")
                .WithKeywords("字体 Font TMP SDF 字符集 Fallback 构建 Profile")
                .WithLayout(ESMenuTreePageLayout.Inspector, 1120f, 18f)
                .WithSelectionFeedback("已打开字体方案与构建", ESEditorFeedbackSoundKind.Open)
                .AddPageAction(new ESMenuTreePageAction(
                        "locate-profile",
                        "定位配置",
                        "在 Project 窗口定位当前 Font Build Profile。",
                        LocateCurrentProfile)
                    .WithUnityIcon("d_Search Icon")
                    .WithPriority(100)
                    .When(context => context.GetOdinTarget<Page_FontBuild>()?.profile != null))
                .AddPageAction(new ESMenuTreePageAction(
                        "rebuild-font-page",
                        "重建页面",
                        "仅重建字体页面视图并保留窗口导航状态。",
                        context => context.RebuildView())
                    .WithUnityIcon("Refresh")
                    .WithPriority(40)));
        }

        private static void LocateCurrentProfile(ESMenuTreePageContext context)
        {
            ESFontBuildProfile profile = context.GetOdinTarget<Page_FontBuild>()?.profile;
            if (profile == null)
                return;
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            context.Notify(
                "已定位字体构建配置：" + profile.name,
                ESMenuTreePageStatus.Ready,
                ESEditorFeedbackSoundKind.Navigate,
                false);
        }
    }
}
