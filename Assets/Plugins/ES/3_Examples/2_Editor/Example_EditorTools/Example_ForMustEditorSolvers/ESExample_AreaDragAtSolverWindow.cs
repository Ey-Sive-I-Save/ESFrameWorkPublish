using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public class ESExample_AreaDragAtSolverWindow : EditorWindow
    {
        private readonly ESAreaSolver area = new ESAreaSolver();
        private readonly ESDragAtSolver dragAt = new ESDragAtSolver();
        private readonly List<UnityEngine.Object> droppedObjects = new List<UnityEngine.Object>();

        private Vector2 scroll;
        private string lastMessage = "等待拖入对象";

        [MenuItem("【ES】/测试案例/编辑器Solver/00 AreaDragAtSolver案例")]
        private static void Open()
        {
            GetWindow<ESExample_AreaDragAtSolverWindow>("AreaDragAtSolver案例");
        }

        private void OnEnable()
        {
            area.InitSolver(
                drawBackground: true,
                drawBorder: true,
                backgroundColor: new Color(0.1f, 0.45f, 0.85f, 0.05f),
                borderColor: new Color(0.1f, 0.45f, 0.85f, 0.9f));
            dragAt.InitSolver();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawCapturedToolBlock();
            DrawResultPanel();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "ESAreaSolver：捕获一段 IMGUI 绘制内容实际占用的 Rect。\n" +
                    "ESDragAtSolver：在指定 Rect 上检测 Project/Hierarchy 对象拖拽。\n" +
                    "预期效果：把资源拖到下方整块工具区域，不只是拖到某个 ObjectField，也能被接收。",
                    MessageType.Info);

                if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(52)))
                    LogTutorial();
            }
        }

        private void DrawCapturedToolBlock()
        {
            area.UpdateAtFisrt();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("被 ESAreaSolver 捕获的工具区域", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("把 Project 资源或 Hierarchy 对象拖到这一整块区域。");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("清空结果", GUILayout.Width(80)))
                    {
                        droppedObjects.Clear();
                        lastMessage = "已清空，等待拖入对象";
                    }
                }

                EditorGUILayout.Space(28);
                EditorGUILayout.LabelField("这里可以放任意复杂 IMGUI：按钮、输入框、列表、提示文本。");
                EditorGUILayout.LabelField("AreaSolver 会在 UpdateAtLast 后得到这整段 UI 的 Rect。");
            }

            area.UpdateAtLast();

            var rect = area.GetAreaRect();
            if (dragAt.Update(out var users, rect))
            {
                droppedObjects.Clear();
                if (users != null)
                    droppedObjects.AddRange(users);

                lastMessage = $"接收 {droppedObjects.Count} 个对象";
                Repaint();
            }
        }

        private void DrawResultPanel()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("最后状态", lastMessage);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (droppedObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有拖入对象。请从 Project 或 Hierarchy 拖对象到上方整块工具区域。", MessageType.None);
            }
            else
            {
                for (int i = 0; i < droppedObjects.Count; i++)
                {
                    var obj = droppedObjects[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField($"{i}", obj, typeof(UnityEngine.Object), true);
                        using (new EditorGUI.DisabledScope(obj == null))
                        {
                            if (GUILayout.Button("定位", GUILayout.Width(52)))
                            {
                                Selection.activeObject = obj;
                                EditorGUIUtility.PingObject(obj);
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ESAreaSolver + ESDragAtSolver 功能介绍 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：把一段普通 IMGUI 绘制内容转成可拖拽响应区域。\n\n" +
                "典型用途：自定义 AttributeDrawer、Odin Drawer、复杂 Inspector 小工具、需要整块区域响应拖拽的编辑器面板。\n\n" +
                "伪代码：\n" +
                "private readonly ESAreaSolver area = new ESAreaSolver();\n" +
                "private readonly ESDragAtSolver dragAt = new ESDragAtSolver();\n\n" +
                "void OnEnable()\n" +
                "{\n" +
                "    area.InitSolver(\n" +
                "        drawBackground: true,\n" +
                "        drawBorder: true,\n" +
                "        backgroundColor: new Color(0.1f, 0.45f, 0.85f, 0.05f),\n" +
                "        borderColor: new Color(0.1f, 0.45f, 0.85f, 0.9f));\n" +
                "    dragAt.InitSolver();\n" +
                "}\n\n" +
                "void OnGUI()\n" +
                "{\n" +
                "    area.UpdateAtFisrt();\n" +
                "    DrawAnyIMGUIBlock();\n" +
                "    area.UpdateAtLast();\n\n" +
                "    Rect rect = area.GetAreaRect();\n" +
                "    if (dragAt.Update(out var objects, rect))\n" +
                "    {\n" +
                "        UseDroppedObjects(objects);\n" +
                "    }\n" +
                "\n" +
                "    // 如果没有 AreaSolver，才需要每帧给兜底高度：\n" +
                "    // dragAt.Update(out var objects, fallbackHeight: currentHeight);\n" +
                "}\n\n" +
                "说明：AreaSolver 只负责推算区域；DragAtSolver 只负责拖拽事件。需要类型过滤、文件夹展开、拒绝原因时，用更上层的 ESDropZoneSolver。");
        }

    }
#endif
}
