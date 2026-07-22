using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES.Samples.Editor{
#if UNITY_EDITOR
    public class ESExample_ContextMenuSolverWindow : EditorWindow
    {
        private UnityEngine.Object targetObject;
        private int commandCount;
        private string lastCommand = "尚未执行命令";

        [MenuItem(MenuItemPathDefine.TEST_TOOLS_PATH + "编辑器 Solver/02 ContextMenuSolver 案例", false, 20)]
        private static void Open()
        {
            GetWindow<ESExample_ContextMenuSolverWindow>("ContextMenuSolver案例");
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTarget();
            DrawRightClickAreas();
            DrawStatus();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "单独演示 ESContextMenuSolver：在指定区域右键，弹出菜单。\n" +
                    "预期效果：菜单项可以启用、禁用、分组，并执行复制、定位、清空等命令。",
                    MessageType.Info);

                if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(38)))
                    LogTutorial();
            }
        }

        private void DrawTarget()
        {
            targetObject = EditorGUILayout.ObjectField("测试对象", targetObject, typeof(UnityEngine.Object), true);
        }

        private void DrawRightClickAreas()
        {
            var mainArea = GUILayoutUtility.GetRect(0, 70, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(mainArea, new Color(0.15f, 0.35f, 0.55f, 0.18f));
            GUI.Label(mainArea, "在这个区域右键：对象菜单", CenterBoldLabel());

            ESContextMenuSolver.HandleContextClick(mainArea, menu =>
            {
                menu.Add("复制对象名", CopyObjectName, targetObject != null);
                menu.Add("复制资源路径", CopyAssetPath, targetObject != null && !string.IsNullOrEmpty(GetAssetPath(targetObject)));
                menu.Add("定位对象", () => Ping(targetObject), targetObject != null);
                menu.Separator();
                menu.Add("清空测试对象", () =>
                {
                    targetObject = null;
                    SetStatus("已清空测试对象");
                }, targetObject != null);
            });

            var commandArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(commandArea, new Color(0.35f, 0.25f, 0.10f, 0.18f));
            GUI.Label(commandArea, "在这个区域右键：命令菜单", CenterBoldLabel());

            ESContextMenuSolver.HandleContextClick(commandArea, menu =>
            {
                menu.Add("执行计数 +1", () => SetStatus($"执行计数：{++commandCount}"));
                menu.Add("禁用示例项", null, false);
                menu.Separator();
                menu.Add("重置计数", () =>
                {
                    commandCount = 0;
                    SetStatus("计数已重置");
                });
            });
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("最后命令", lastCommand);
            EditorGUILayout.LabelField("执行次数", commandCount.ToString());
        }

        private void CopyObjectName()
        {
            if (targetObject == null) return;
            GUIUtility.systemCopyBuffer = targetObject.name;
            SetStatus("已复制对象名");
        }

        private void CopyAssetPath()
        {
            var path = GetAssetPath(targetObject);
            if (string.IsNullOrEmpty(path)) return;
            GUIUtility.systemCopyBuffer = path;
            SetStatus("已复制资源路径");
        }

        private void SetStatus(string message)
        {
            lastCommand = message;
            Repaint();
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ESContextMenuSolver 功能介绍 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：为指定 Rect 区域绑定右键菜单，统一创建菜单项、禁用项和分隔线。\n\n" +
                "典型用途：树节点右键、列表项右键、工具窗口命令区、Inspector 小区域操作菜单。\n\n" +
                "伪代码：\n" +
                "Rect area = GUILayoutUtility.GetRect(...);\n" +
                "ESContextMenuSolver.HandleContextClick(area, menu =>\n" +
                "{\n" +
                "    menu.Add(\"复制路径\", CopyPath, obj != null);\n" +
                "    menu.Add(\"定位资源\", () => Ping(obj), obj != null);\n" +
                "    menu.Separator();\n" +
                "    menu.Add(\"删除节点\", DeleteNode, canDelete);\n" +
                "});\n\n" +
                "说明：enabled=false 时菜单项会显示为禁用状态，适合表达当前命令不可用。");
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
        }

        private static void Ping(UnityEngine.Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static GUIStyle CenterBoldLabel()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
        }

    }
#endif
}
