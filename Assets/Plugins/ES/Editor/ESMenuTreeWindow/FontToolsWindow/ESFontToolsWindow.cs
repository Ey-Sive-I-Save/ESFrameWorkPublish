using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

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
        [System.NonSerialized] private Page_FontPreview fontPreviewPage;

        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 字体资产工作台", "管理十语言字体族、字符集、生成资产和自动回退链。");
        protected override string ESWindow_Subtitle => "十语言字体族、字符收集、生成与覆盖验证";
        protected override Vector2 ESWindow_MinSize => new Vector2(760f, 540f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1120f, 760f);

        protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            fontBuildPage ??= new Page_FontBuild();
            fontPreviewPage ??= new Page_FontPreview();
            builder.Add(ESMenuTreePageDefinition
                .ForOdin("font.build", "字体 / 方案与构建", fontBuildPage)
                .WithUnityIcon("Font Icon")
                .WithKeywords("字体 Font 字体族 字符集 自动回退 构建 十语言")
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
            builder.Add(ESMenuTreePageDefinition
                .ForOdin("font.preview", "字体 / 预览与覆盖率", fontPreviewPage)
                .WithUnityIcon("Font Icon")
                .WithKeywords("字体 Font 预览 图集 字符覆盖 缺字 自动回退 运行时目录 十语言")
                .WithLayout(ESMenuTreePageLayout.Inspector, 1120f, 18f)
                .WithSelectionFeedback("已打开字体预览与覆盖率", ESEditorFeedbackSoundKind.Navigate));
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
