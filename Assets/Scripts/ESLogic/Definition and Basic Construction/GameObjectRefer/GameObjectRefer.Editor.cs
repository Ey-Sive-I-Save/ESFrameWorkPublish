#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using static ES.GameObjectRefer;
namespace ES
{
    public class GameObjectTreeItemDrawer : OdinValueDrawer<GameObjectTreeItem>
    {
        private ESDragAtSolver solver = new ESDragAtSolver();
        bool isGameObject = false;
        public static Color colorGameobject = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.墨绿);
        public static Color colorGameobjectGroup = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.夜棕);
        protected override void DrawPropertyLayout(GUIContent label)
        {

            var item = this.ValueEntry.SmartValue;
            if (item == null)
            {
                CallNextDrawer(label);
                return;
            }
            isGameObject = item is GameObjectLeaf;
            solver.normalColor = isGameObject ? colorGameobject : colorGameobjectGroup;
            // 开始外框
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            solver.Update(out var users);
            if (!isGameObject)
            {

                if (users != null && users.Length > 0)
                {
                    if (item is GameObjectGroup group1)
                    {
                        foreach (var g in users)
                            group1.TryAddGameObject(g as GameObject, null, true, false);
                    }
                }
            }

            // 绘制默认内容（名称、children等）
            CallNextDrawer(label);

            // 获取自定义路径
            string customPath = GetCustomPath(this.Property);

            // 绘制按钮区域（包含状态块）
            if (item is GameObjectGroup group)
                DrawGroupButtons(group, customPath);
            else if (item is GameObjectLeaf leaf)
                DrawLeafButtons(leaf, customPath);

            EditorGUILayout.EndVertical();
        }

        private void DrawGroupButtons(GameObjectGroup group, string customPath)
        {
            bool isValid = IsGroupValid(group);
            DrawStatusButton(customPath, isValid, "📁 分组路径");
        }

        private void DrawLeafButtons(GameObjectLeaf leaf, string customPath)
        {
            bool isValid = leaf.gameObject != null;
            DrawStatusButton(customPath, isValid, "📄 叶子路径");
        }

        private void DrawStatusButton(string customPath, bool isValid, string buttonPrefix)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();



            // 复制路径按钮
            if (GUILayout.Button($"{buttonPrefix}: {customPath}", EditorStyles.miniButton))
            {
                EditorGUIUtility.systemCopyBuffer = customPath;
                Debug.Log($"已复制路径：{customPath}");
            }

            // 为分组添加“列出子对象”按钮
            if (buttonPrefix.Contains("分组") && GUILayout.Button("列出子对象", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                var paths = new List<string>();
                CollectAllLeafPaths((GameObjectGroup)ValueEntry.SmartValue, customPath, paths);
                Debug.Log($"分组下所有子对象路径：\n" + string.Join("\n", paths));
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool IsGroupValid(GameObjectGroup group)
        {
            if (group.children == null || group.children.Count == 0)
                return false;
            // 递归检查是否有至少一个叶子节点包含有效的 GameObject
            foreach (var child in group.children)
            {
                if (child is GameObjectLeaf leaf && leaf.gameObject != null)
                    return true;
                if (child is GameObjectGroup subGroup && IsGroupValid(subGroup))
                    return true;
            }
            return false;
        }

        private string GetCustomPath(InspectorProperty property)
        {
            if (property == null) return "";
            var names = new List<string>();
            InspectorProperty current = property;
            while (current != null)
            {
                var node = current.ValueEntry?.WeakSmartValue as GameObjectTreeItem;
                if (node != null && !string.IsNullOrEmpty(node.itemName))
                    names.Insert(0, node.itemName);
                current = current.Parent;
            }
            return string.Join("/", names);
        }

        private void CollectAllLeafPaths(GameObjectGroup group, string currentPath, List<string> outPaths)
        {
            foreach (var child in group.children)
            {
                if (child is GameObjectLeaf leaf)
                {
                    string leafPath = string.IsNullOrEmpty(currentPath) ? leaf.itemName : $"{currentPath}/{leaf.itemName}";
                    outPaths.Add(leafPath);
                }
                else if (child is GameObjectGroup subGroup)
                {
                    string subPath = string.IsNullOrEmpty(currentPath) ? subGroup.itemName : $"{currentPath}/{subGroup.itemName}";
                    CollectAllLeafPaths(subGroup, subPath, outPaths);
                }
            }
        }
    }
}
#endif
