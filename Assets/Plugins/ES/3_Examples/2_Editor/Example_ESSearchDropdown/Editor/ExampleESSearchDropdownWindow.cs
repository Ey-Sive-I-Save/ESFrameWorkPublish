using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES.Examples.Editor
{
    /// <summary>ESSearchDropdown 标准展示窗口，兼顾快速上手、功能验证和视频录制。</summary>
    public sealed class ExampleESSearchDropdownWindow : ESSinglePageIMGUIWindow<ExampleESSearchDropdownWindow>
    {
        private sealed class DemoCommand
        {
            public string Name;
            public string Category;
            public string Description;
            public string Keywords;
            public string Badge;
            public string IconName;
            public bool Enabled = true;
        }

        private readonly List<string> simpleValues = new List<string>
        {
            "玩家出生点", "主城场景", "战斗测试场景", "角色资源库", "RuntimeWatch"
        };

        private readonly List<DemoCommand> commands = new List<DemoCommand>
        {
            new DemoCommand { Name = "创建角色", Category = "内容制作/角色", Description = "创建标准角色根对象和基础模块", Keywords = "角色 actor player", Badge = "常用", IconName = "d_Prefab Icon" },
            new DemoCommand { Name = "创建武器", Category = "内容制作/物品", Description = "生成武器定义和展示对象", Keywords = "武器 weapon item", Badge = "模板", IconName = "d_ToolHandleGlobal" },
            new DemoCommand { Name = "打开 RuntimeWatch", Category = "运行时诊断", Description = "观察字段、属性、方法和 GameObject", Keywords = "观察 watch debug", Badge = "Ctrl+Shift+W", IconName = "d_Profiler.Record" },
            new DemoCommand { Name = "发布资源", Category = "资源与发布", Description = "构建 Catalog、Bundle 和 Consumer", Keywords = "资源 build publish AB", Badge = "构建", IconName = "BuildSettings.Editor.Small" },
            new DemoCommand { Name = "危险维护命令", Category = "开发与维护", Description = "示例禁用项，不可执行", Keywords = "disabled", Badge = "不可用", IconName = "console.warnicon", Enabled = false }
        };

        private string selectedValue = "尚未选择";
        private string selectedCommand = "尚未选择";
        private string selectedAsset = "尚未选择";
        private int providerBuildCount;
        private Vector2 scroll;
        private GUIStyle titleStyle;

        [MenuItem(MenuItemPathDefine.SAMPLE_TOOLS_PATH + "ESSearchDropdown 标准展示", false, 100)]
        private static void OpenSampleWindow()
        {
            var window = GetWindow<ExampleESSearchDropdownWindow>(false, "ESSearchDropdown Sample", true);
            window.minSize = new Vector2(720f, 620f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ESSearchDropdown Sample", "搜索下拉的标准能力、延迟 Provider 与异常隔离示例");
        }

        protected override string ESWindow_Subtitle => "统一搜索、分组、状态与安全回调";
        protected override Vector2 ESWindow_MinSize => new Vector2(720f, 620f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(920f, 760f);
        protected override string ESWindow_PageStableId => "sample.search-dropdown";
        protected override string ESWindow_PageTitle => "ESSearchDropdown 标准展示";
        protected override string ESWindow_PageKeywords => "示例 Search Dropdown Provider 资源 命令";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "search-sample.reset",
                    "重置示例",
                    "重置当前选择和 Provider 构建计数。",
                    context =>
                    {
                        selectedValue = "尚未选择";
                        selectedCommand = "尚未选择";
                        selectedAsset = "尚未选择";
                        providerBuildCount = 0;
                        context.SetStatus("搜索下拉示例已重置");
                        Repaint();
                    })
                .WithUnityIcon("Refresh")
                .WithPriority(100));
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSimpleSample();
            DrawBuilderSample();
            DrawStructuredSample();
            DrawLazyProviderSample();
            DrawSafetySample();
            EditorGUILayout.EndScrollView();
        }

        protected override void ESWindow_OnHostDisable()
        {
            titleStyle = null;
        }

        private void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 72f);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.18f, 0.25f)
                : new Color(0.72f, 0.84f, 0.96f));
            Rect title = new Rect(rect.x + 18f, rect.y + 10f, rect.width - 36f, 28f);
            Rect subtitle = new Rect(rect.x + 18f, rect.y + 40f, rect.width - 36f, 22f);
            if (titleStyle == null)
                titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
            EditorGUI.LabelField(title, "ESSearchDropdown 标准展示", titleStyle);
            EditorGUI.LabelField(subtitle, "统一搜索、分组、图标、状态与回调，同时保持简单 API。", EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);
        }

        private void DrawSimpleSample()
        {
            BeginSection("01  最简单：任意集合", "只提供显示名和选择回调，适合普通字符串、场景、资源或业务对象。");
            EditorGUILayout.LabelField("当前选择", selectedValue);
            if (GUILayout.Button("打开最简选择器", GUILayout.Height(28f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                ESSearchDropdown.OpenItems(anchor, "选择常用对象", simpleValues, value => value, value =>
                {
                    selectedValue = value;
                    Repaint();
                });
            }
            EndSection();
        }

        private void DrawBuilderSample()
        {
            BeginSection("02  Builder：少量固定命令", "链式添加、分组、禁用项和分隔线，不需要手工维护 List<Entry>。");
            EditorGUILayout.LabelField("最后执行", selectedCommand);
            if (GUILayout.Button("打开 Builder 命令菜单", GUILayout.Height(28f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                ESSearchDropdown.Create("选择演示命令")
                    .Add("创建角色", () => SelectCommand("创建角色"), "内容制作")
                    .Add("创建武器", () => SelectCommand("创建武器"), "内容制作")
                    .AddSeparator()
                    .Add("运行检查", () => SelectCommand("运行检查"), "诊断")
                    .AddDisabled("当前不可执行", "诊断", "这是禁用项演示")
                    .Show(anchor);
            }
            EndSection();
        }

        private void DrawStructuredSample()
        {
            BeginSection("03  结构化条目：商业工具常用形态", "主名称保持简洁，使用分组、描述、关键词、徽章、图标和选中状态表达上下文。");
            EditorGUILayout.LabelField("当前命令", selectedCommand);
            if (GUILayout.Button("打开结构化命令选择器", GUILayout.Height(30f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                var entries = new List<ESSearchDropdown.Entry>();
                foreach (DemoCommand command in commands)
                {
                    DemoCommand captured = command;
                    if (!command.Enabled)
                    {
                        entries.Add(ESSearchDropdown.Entry.Disabled(command.Name, command.Category, command.Description));
                        continue;
                    }

                    entries.Add(ESSearchDropdown.Entry.Item(
                        command.Name,
                        () => SelectCommand(captured.Name),
                        command.Category,
                        EditorGUIUtility.IconContent(command.IconName).image as Texture2D,
                        subtitle: command.Description,
                        keywords: command.Keywords,
                        badge: command.Badge,
                        selected: selectedCommand == command.Name));
                }
                ESSearchDropdown.Open(anchor, "ES 功能选择", entries, minimumWindowSize: new Vector2(680f, 400f));
            }
            EndSection();
        }

        private void DrawLazyProviderSample()
        {
            BeginSection("04  延迟 Provider：大型资源列表", "点击前不会扫描项目；Dropdown 真正构建时才调用 AssetDatabase。");
            EditorGUILayout.LabelField("Provider 构建次数", providerBuildCount.ToString());
            EditorGUILayout.LabelField("当前资产", selectedAsset);
            if (GUILayout.Button("延迟扫描并选择 Prefab / Scene", GUILayout.Height(30f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                ESSearchDropdown.Open(anchor, "选择项目资产", BuildAssetEntries,
                    minimumWindowSize: new Vector2(720f, 440f));
            }
            EndSection();
        }

        private void DrawSafetySample()
        {
            BeginSection("05  安全性：异常隔离", "Provider 或业务回调抛出异常时，Dropdown 会输出带上下文的 Console 错误。");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Provider 异常示例", GUILayout.Height(26f)))
                {
                    Rect anchor = GUILayoutUtility.GetLastRect();
                    ESSearchDropdown.Open(anchor, "异常 Provider", () => throw new InvalidOperationException("Sample Provider 主动异常"));
                }
                if (GUILayout.Button("回调异常示例", GUILayout.Height(26f)))
                {
                    Rect anchor = GUILayoutUtility.GetLastRect();
                    ESSearchDropdown.Create("异常回调")
                        .Add("点击后抛出测试异常", () => throw new InvalidOperationException("Sample 回调主动异常"))
                        .Show(anchor);
                }
            }
            EndSection();
        }

        private IEnumerable<ESSearchDropdown.Entry> BuildAssetEntries()
        {
            providerBuildCount++;
            Repaint();
            IEnumerable<string> assetGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                .Distinct(StringComparer.Ordinal)
                .Take(300);
            foreach (string guid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                UnityEngine.Object captured = asset;
                string capturedPath = path;
                yield return ESSearchDropdown.Entry.Item(
                    asset.name,
                    () =>
                    {
                        selectedAsset = capturedPath;
                        Selection.activeObject = captured;
                        EditorGUIUtility.PingObject(captured);
                        Repaint();
                    },
                    asset is SceneAsset ? "场景" : "Prefab",
                    AssetPreview.GetMiniThumbnail(asset),
                    subtitle: path,
                    badge: asset.GetType().Name,
                    selected: selectedAsset == path);
            }
        }

        private void SelectCommand(string command)
        {
            selectedCommand = command;
            Repaint();
        }

        private static void BeginSection(string title, string description)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5f);
        }

        private static void EndSection()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
        }
    }
}
