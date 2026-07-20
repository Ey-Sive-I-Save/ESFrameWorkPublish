using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public class ESExample_DropZoneSolverWindow : EditorWindow
    {
        private readonly ESDropZoneSolver dropZone = new ESDropZoneSolver();
        private readonly List<UnityEngine.Object> acceptedObjects = new List<UnityEngine.Object>();
        private Vector2 scroll;
        private string lastMessage = "等待拖入资源";

        [MenuItem(MenuItemPathDefine.TEST_TOOLS_PATH + "编辑器 Solver/01 DropZoneSolver 案例", false, 10)]
        private static void Open()
        {
            GetWindow<ESExample_DropZoneSolverWindow>("DropZoneSolver案例");
        }

        private void OnEnable()
        {
            dropZone.InitSolver<UnityEngine.Object>(
                allowFolderExpand: true,
                rejectScripts: true,
                maxCount: 32);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawDropArea();
            DrawResults();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "单独演示 ESDropZoneSolver：把 Project 资源或文件夹拖到下方区域。\n" +
                    "预期效果：文件夹会展开，C# 脚本会被过滤，成功接收的对象显示在列表中。",
                    MessageType.Info);

                if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(38)))
                    LogTutorial();
            }
        }

        private void DrawDropArea()
        {
            var area = GUILayoutUtility.GetRect(0, 82, GUILayout.ExpandWidth(true));
            if (dropZone.Draw(area, out var dropped))
            {
                acceptedObjects.Clear();
                acceptedObjects.AddRange(dropped);
                lastMessage = $"已接收 {dropped.Length} 个对象";
            }

            var detail = string.IsNullOrEmpty(dropZone.LastRejectReason)
                ? dropZone.LastAcceptedCount > 0 ? $"松开鼠标后将接收 {dropZone.LastAcceptedCount} 个对象" : lastMessage
                : $"拒绝原因：{dropZone.LastRejectReason}";

            GUI.Label(new Rect(area.x, area.y + 16, area.width, 22), "DropZone 投放区", CenterBoldLabel());
            GUI.Label(new Rect(area.x, area.y + 44, area.width, 20), detail, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField("接收结果", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("清空列表", GUILayout.Width(80)))
                {
                    acceptedObjects.Clear();
                    lastMessage = "等待拖入资源";
                }
                GUILayout.Label("双击 Project 资源拖入即可测试。");
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (acceptedObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有接收到对象。", MessageType.None);
            }
            else
            {
                for (int i = 0; i < acceptedObjects.Count; i++)
                {
                    var obj = acceptedObjects[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(obj, typeof(UnityEngine.Object), false);
                        if (GUILayout.Button("定位", GUILayout.Width(52)))
                            Ping(obj);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ESDropZoneSolver 功能介绍 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：绘制一个可投放区域，接收 Project 面板拖入的资源或文件夹，并输出过滤后的对象数组。\n\n" +
                "典型用途：资源收集窗口、批量导入工具、Inspector 自定义拖拽区。\n\n" +
                "伪代码：\n" +
                "private readonly ESDropZoneSolver dropZone = new ESDropZoneSolver();\n\n" +
                "void OnEnable()\n" +
                "{\n" +
                "    dropZone.InitSolver<UnityEngine.Object>(\n" +
                "        allowFolderExpand: true,\n" +
                "        rejectScripts: true,\n" +
                "        maxCount: 32);\n" +
                "}\n\n" +
                "void OnGUI()\n" +
                "{\n" +
                "    Rect area = GUILayoutUtility.GetRect(...);\n" +
                "    if (dropZone.Draw(area, out var objects))\n" +
                "    {\n" +
                "        foreach (var obj in objects)\n" +
                "            AddToList(obj);\n" +
                "    }\n" +
                "}\n\n" +
                "类型限制示例：如果只想接收贴图，可以改成 dropZone.Accept<Texture2D>();\n" +
                "错误示例：不要在 OnGUI 每帧 new ESDropZoneSolver，否则拖拽缓存和状态字段会丢失。\n\n" +
                "状态字段：LastAcceptedCount 用于显示即将接收数量；LastRejectReason 用于显示拒绝原因。");
        }

        private static GUIStyle CenterBoldLabel()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
        }

        private static void Ping(UnityEngine.Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

    }
#endif
}
