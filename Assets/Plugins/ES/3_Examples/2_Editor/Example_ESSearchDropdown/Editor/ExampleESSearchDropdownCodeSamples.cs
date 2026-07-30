using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES.Examples.Editor
{
    /// <summary>
    /// ESSearchDropdown 可复制代码案例。
    /// 调用这些方法时传入触发按钮的 Rect，例如 GUILayoutUtility.GetLastRect()。
    /// </summary>
    public static class ExampleESSearchDropdownCodeSamples
    {
        public sealed class ToolInfo
        {
            public string Name;
            public string Category;
            public string Description;
            public string Shortcut;
            public Action Open;
        }

        /// <summary>案例 1：最简单的字符串集合。</summary>
        public static void ShowSimpleStrings(Rect anchorRect, Action<string> onSelected)
        {
            string[] values = { "主城", "战斗场景", "角色资源库", "RuntimeWatch" };

            ESSearchDropdown.OpenItems(
                anchorRect,
                "选择功能",
                values,
                value => value,
                value => onSelected?.Invoke(value));
        }

        /// <summary>案例 2：Builder 适合少量、固定的命令。</summary>
        public static void ShowBuilderCommands(Rect anchorRect)
        {
            ESSearchDropdown.Create("创建内容")
                .Add("创建角色", () => Debug.Log("创建角色"), "角色")
                .Add("创建 NPC", () => Debug.Log("创建 NPC"), "角色")
                .Add("创建武器", () => Debug.Log("创建武器"), "物品")
                .AddSeparator()
                .AddDisabled("服务器配置当前不可用", "网络")
                .Show(anchorRect);
        }

        /// <summary>案例 3：任意业务对象，不需要业务类型依赖 Dropdown。</summary>
        public static void ShowBusinessObjects(Rect anchorRect, IEnumerable<ToolInfo> tools)
        {
            ESSearchDropdown.OpenItems(
                anchorRect,
                "打开 ES 工具",
                tools,
                tool => tool.Name,
                tool => tool.Open?.Invoke(),
                tool => tool.Category,
                tool => EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image as Texture2D,
                minimumWindowSize: new Vector2(620f, 360f));
        }

        /// <summary>案例 4：完整结构化条目。</summary>
        public static void ShowStructuredEntries(Rect anchorRect, string currentId, Action<string> onSelected)
        {
            var entries = new List<ESSearchDropdown.Entry>
            {
                ESSearchDropdown.Entry.Item(
                    label: "FireBall",
                    onSelected: () => onSelected?.Invoke("fire_ball"),
                    groupPath: "技能/火系",
                    icon: EditorGUIUtility.IconContent("d_PreMatSphere").image as Texture2D,
                    subtitle: "SkillDefinitionDataInfo · Skill.FireBall",
                    keywords: "火球 fire projectile",
                    badge: "子资产",
                    selected: currentId == "fire_ball"),

                ESSearchDropdown.Entry.Item(
                    label: "IceBolt",
                    onSelected: () => onSelected?.Invoke("ice_bolt"),
                    groupPath: "技能/冰系",
                    icon: EditorGUIUtility.IconContent("d_PreMatSphere").image as Texture2D,
                    subtitle: "SkillDefinitionDataInfo · Skill.IceBolt",
                    keywords: "冰箭 ice projectile",
                    badge: "主资产",
                    selected: currentId == "ice_bolt"),

                ESSearchDropdown.Entry.Disabled("尚未解锁的技能", "技能/未解锁")
            };

            ESSearchDropdown.Open(
                anchorRect,
                "选择技能定义",
                entries,
                minimumWindowSize: new Vector2(680f, 400f));
        }

        /// <summary>案例 5：打开时才扫描项目资源。</summary>
        public static void ShowLazyAssetProvider(Rect anchorRect, Action<UnityEngine.Object> onSelected)
        {
            ESSearchDropdown.Open(
                anchorRect,
                "选择 Prefab",
                () => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                    .Take(500)
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
                    .Where(asset => asset != null)
                    .Select(asset =>
                    {
                        UnityEngine.Object captured = asset;
                        string path = AssetDatabase.GetAssetPath(asset);
                        return ESSearchDropdown.Entry.Item(
                            asset.name,
                            () => onSelected?.Invoke(captured),
                            System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'),
                            AssetPreview.GetMiniThumbnail(asset),
                            subtitle: path,
                            badge: asset.GetType().Name);
                    }),
                minimumWindowSize: new Vector2(720f, 440f));
        }

        /// <summary>案例 6：典型 IMGUI 按钮调用方式。</summary>
        public static void DrawExampleButton()
        {
            if (!GUILayout.Button("选择项目 Prefab"))
                return;

            Rect anchorRect = GUILayoutUtility.GetLastRect();
            ShowLazyAssetProvider(anchorRect, asset =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });
        }
    }
}
