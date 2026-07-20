using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public class ESExample_RecordListSolverWindow : EditorWindow
    {
        [Serializable]
        private class RecordItem
        {
            public string Name;
            public int Priority;
            public bool Enable;
            public UnityEngine.Object Target;
        }

        private readonly ESRecordListSolver<RecordItem> recordList = new ESRecordListSolver<RecordItem>();
        private readonly List<RecordItem> records = new List<RecordItem>();
        private int createIndex = 1;
        private string lastMessage = "等待操作";

        [MenuItem(MenuItemPathDefine.TEST_TOOLS_PATH + "编辑器 Solver/05 RecordListSolver 案例", false, 50)]
        private static void Open()
        {
            GetWindow<ESExample_RecordListSolverWindow>("RecordListSolver案例");
        }

        private void OnEnable()
        {
            if (records.Count == 0)
                BuildDemoRecords();

            recordList.DefaultElementHeight = 24f;
            recordList.InitSolver(
                records,
                headerName: "资源处理记录",
                draggable: true,
                displayAdd: true,
                displayRemove: true,
                onCreateElement: CreateRecord,
                onDrawElement: DrawRecordRow,
                onDrawHeaderRight: DrawHeaderButtons,
                getElementHeight: item => item != null && !item.Enable ? 24f : 28f,
                getElementLabel: item => item == null ? "空记录" : item.Name,
                onRemoveElement: item => lastMessage = $"已移除：{item?.Name}",
                onChanged: _ => Repaint());
        }

        private void OnGUI()
        {
            DrawHeader();
            recordList.Draw();
            DrawSelectedInfo();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "单独演示 ESRecordListSolver：基于 ReorderableList 的可排序记录列表。\n" +
                    "预期效果：每行可以自定义绘制 Toggle、优先级、对象引用和按钮；顶部可以执行自定义排序。",
                    MessageType.Info);

                if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(38)))
                    LogTutorial();
            }
        }

        private void DrawHeaderButtons(Rect rect)
        {
            var byName = new Rect(rect.x, rect.y + 1, 48, rect.height - 2);
            var byPriority = new Rect(rect.x + 52, rect.y + 1, 48, rect.height - 2);
            var reset = new Rect(rect.x + 104, rect.y + 1, 48, rect.height - 2);

            if (GUI.Button(byName, "名称"))
            {
                recordList.Sort((a, b) => string.Compare(a?.Name, b?.Name, StringComparison.Ordinal));
                lastMessage = "已按名称排序";
            }

            if (GUI.Button(byPriority, "优先"))
            {
                recordList.Sort((a, b) => (a?.Priority ?? 0).CompareTo(b?.Priority ?? 0));
                lastMessage = "已按优先级排序";
            }

            if (GUI.Button(reset, "重置"))
            {
                BuildDemoRecords();
                recordList.Rebuild();
                lastMessage = "已重建示例数据";
            }
        }

        private void DrawRecordRow(Rect rect, RecordItem item, int index, bool isActive, bool isFocused)
        {
            if (item == null)
            {
                EditorGUI.LabelField(rect, $"{index}. 空记录");
                return;
            }

            var enabledRect = new Rect(rect.x, rect.y + 2, 52, rect.height - 4);
            var priorityRect = new Rect(rect.x + 56, rect.y + 2, 42, rect.height - 4);
            var nameRect = new Rect(rect.x + 102, rect.y + 2, Mathf.Max(80, rect.width * 0.25f), rect.height - 4);
            var objectRect = new Rect(nameRect.xMax + 6, rect.y + 2, Mathf.Max(80, rect.width - nameRect.width - 238), rect.height - 4);
            var pingRect = new Rect(rect.xMax - 108, rect.y + 2, 48, rect.height - 4);
            var printRect = new Rect(rect.xMax - 56, rect.y + 2, 48, rect.height - 4);

            item.Enable = EditorGUI.ToggleLeft(enabledRect, "启用", item.Enable);
            item.Priority = EditorGUI.IntField(priorityRect, item.Priority);
            item.Name = EditorGUI.TextField(nameRect, item.Name);
            item.Target = EditorGUI.ObjectField(objectRect, item.Target, typeof(UnityEngine.Object), false);

            using (new EditorGUI.DisabledScope(item.Target == null))
            {
                if (GUI.Button(pingRect, "定位"))
                    Ping(item.Target);
            }

            if (GUI.Button(printRect, "打印"))
            {
                Debug.Log($"[RecordListSolver案例] #{index} {item.Name} / Priority={item.Priority} / Enable={item.Enable}");
                lastMessage = $"已打印：{item.Name}";
            }
        }

        private void DrawSelectedInfo()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("最后操作", lastMessage);

            if (recordList.TryGetSelected(out var selected) && selected != null)
            {
                EditorGUILayout.LabelField("当前选中", selected.Name);
                EditorGUILayout.LabelField("优先级", selected.Priority.ToString());
                EditorGUILayout.ObjectField("目标对象", selected.Target, typeof(UnityEngine.Object), false);
            }
            else
            {
                EditorGUILayout.HelpBox("点击列表行后，这里会显示当前选中记录。", MessageType.None);
            }
        }

        private RecordItem CreateRecord()
        {
            return new RecordItem
            {
                Name = "新记录 " + createIndex,
                Priority = createIndex++,
                Enable = true
            };
        }

        private void BuildDemoRecords()
        {
            records.Clear();
            records.Add(new RecordItem { Name = "构建AB包", Priority = 30, Enable = true });
            records.Add(new RecordItem { Name = "分析资源去向", Priority = 10, Enable = true });
            records.Add(new RecordItem { Name = "上传服务器", Priority = 50, Enable = false });
            records.Add(new RecordItem { Name = "生成依赖表", Priority = 20, Enable = true });
            createIndex = records.Count + 1;
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ESRecordListSolver 功能介绍 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：封装 UnityEditorInternal.ReorderableList，用于绘制可排序、可拖拽、可自定义行内容的记录列表。\n\n" +
                "典型用途：资源管理窗口的 Library 列表、构建步骤列表、操作记录列表、优先级任务列表。\n\n" +
                "伪代码：\n" +
                "private readonly ESRecordListSolver<MyRecord> recordList = new ESRecordListSolver<MyRecord>();\n" +
                "private readonly List<MyRecord> records = new List<MyRecord>();\n\n" +
                "void OnEnable()\n" +
                "{\n" +
                "    recordList.InitSolver(\n" +
                "        records,\n" +
                "        headerName: \"构建记录\",\n" +
                "        draggable: true,\n" +
                "        onCreateElement: () => new MyRecord(),\n" +
                "        onDrawElement: (rect, item, index, active, focused) => DrawRecord(rect, item),\n" +
                "        onDrawHeaderRight: rect =>\n" +
                "        {\n" +
                "            if (GUI.Button(rect, \"排序\"))\n" +
                "                recordList.Sort((a, b) => a.Priority.CompareTo(b.Priority));\n" +
                "        });\n" +
                "}\n\n" +
                "void OnGUI()\n" +
                "{\n" +
                "    recordList.Draw();\n" +
                "}\n\n" +
                "注意：ESRecordListSolver 应作为成员字段持有，不要在 OnGUI 每帧 new。");
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
