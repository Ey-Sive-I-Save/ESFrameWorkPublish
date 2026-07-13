using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public class ESExample_TreeViewSolverWindow : EditorWindow
    {
        private readonly ESTreeViewSolver tree = new ESTreeViewSolver();
        private readonly List<ESTreeViewNode> roots = new List<ESTreeViewNode>();
        private Vector2 scroll;
        private ESTreeViewNode selectedNode;

        [MenuItem("【ES】/测试案例/编辑器Solver/03 TreeViewSolver案例")]
        private static void Open()
        {
            GetWindow<ESExample_TreeViewSolverWindow>("TreeViewSolver案例");
        }

        private void OnEnable()
        {
            BuildDemoTree();
            tree.InitSolver(
                allowFolderSelection: false,
                onSelected: node => selectedNode = node,
                onDoubleClick: node => Debug.Log($"[TreeViewSolver案例] 双击节点：{node.Name}"),
                onContextMenu: (node, menu) =>
                {
                    menu.Separator();
                    menu.Add("打印节点信息", () => Debug.Log($"节点：{node.Name} / Id：{node.Id}"));
                    menu.Add("移除节点", () => RemoveNode(node.Id));
                },
                getNodeHeight: node => selectedNode == node ? 24f : 20f,
                onDrawNodeContent: DrawNodeContent,
                onDrawNodeRight: DrawNodeRight,
                rightAreaWidth: 132f);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawTree();
            DrawSelection();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "单独演示 ESTreeViewSolver：搜索、折叠、选择、双击、右键菜单。\n" +
                    "预期效果：文件夹节点是分组文本，不画按钮；资源节点可以选中并显示详情。",
                    MessageType.Info);

                if (GUILayout.Button("?", GUILayout.Width(24), GUILayout.Height(38)))
                    LogTutorial();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tree.SearchText = GUILayout.TextField(tree.SearchText, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(160));
                if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(72))) tree.ExpandAll(roots);
                if (GUILayout.Button("全部折叠", EditorStyles.toolbarButton, GUILayout.Width(72))) tree.CollapseAll(roots);
                if (GUILayout.Button("重建示例", EditorStyles.toolbarButton, GUILayout.Width(72))) BuildDemoTree();
            }
        }

        private void DrawTree()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(220));
            tree.Draw(roots);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelection()
        {
            EditorGUILayout.Space(6);
            if (selectedNode == null)
            {
                EditorGUILayout.HelpBox("点击资源节点后，这里会显示选中节点。文件夹节点默认不可选。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("选中节点", selectedNode.Name);
            EditorGUILayout.LabelField("节点Id", selectedNode.Id);
        }

        private void BuildDemoTree()
        {
            roots.Clear();

            var assets = new ESTreeViewNode("folder:Assets", "Assets");
            var plugins = new ESTreeViewNode("folder:Assets/Plugins", "Plugins");
            var es = new ESTreeViewNode("folder:Assets/Plugins/ES", "ES");
            var examples = new ESTreeViewNode("folder:Assets/Plugins/ES/3_Examples", "3_Examples");
            var foundation = new ESTreeViewNode("folder:Assets/Plugins/ES/3_Examples/0_Foundation", "0_Foundation");
            var runtime = new ESTreeViewNode("folder:Assets/Plugins/ES/3_Examples/1_Runtime", "1_Runtime");
            var editor = new ESTreeViewNode("folder:Assets/Plugins/ES/3_Examples/2_Editor", "2_Editor");
            var data = new ESTreeViewNode("folder:Assets/Plugins/ES/3_Examples/3_Data", "3_Data");

            foundation.Children.Add(new ESTreeViewNode("asset:Example_Ext", "Example_Ext", this));
            runtime.Children.Add(new ESTreeViewNode("asset:RuntimeWatch", "Example_RuntimeWatch", this));
            editor.Children.Add(new ESTreeViewNode("asset:EditorTools", "Example_EditorTools", this));
            data.Children.Add(new ESTreeViewNode("asset:ESDesignUtility", "Example_ESDesignUtility", this));
            examples.Children.Add(foundation);
            examples.Children.Add(runtime);
            examples.Children.Add(editor);
            examples.Children.Add(data);
            es.Children.Add(examples);
            plugins.Children.Add(es);
            assets.Children.Add(plugins);
            roots.Add(assets);

            tree.ExpandAll(roots);
            selectedNode = null;
        }

        private void DrawNodeContent(ESTreeViewNode node, Rect rect, bool selected)
        {
            var prefix = node.Target == null ? "[目录] " : "[资源] ";
            var style = node.Target == null ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUI.LabelField(rect, prefix + node.Name, style);
        }

        private void DrawNodeRight(ESTreeViewNode node, Rect rect)
        {
            if (node.Target == null)
            {
                EditorGUI.LabelField(rect, "分组节点", EditorStyles.miniLabel);
                return;
            }

            var detailRect = new Rect(rect.x, rect.y + 1, 58, rect.height - 2);
            var logRect = new Rect(rect.x + 64, rect.y + 1, 58, rect.height - 2);

            if (GUI.Button(detailRect, "详情"))
                selectedNode = node;

            if (GUI.Button(logRect, "打印"))
                Debug.Log($"[TreeViewSolver案例] 节点：{node.Name} / Id：{node.Id}");
        }

        private void RemoveNode(string id)
        {
            if (roots.RemoveAll(i => i.Id == id) > 0) return;
            foreach (var root in roots)
            {
                if (RemoveNodeRecursive(root, id))
                    return;
            }
        }

        private static bool RemoveNodeRecursive(ESTreeViewNode parent, string id)
        {
            if (parent.Children.RemoveAll(i => i.Id == id) > 0)
                return true;

            foreach (var child in parent.Children)
            {
                if (RemoveNodeRecursive(child, id))
                    return true;
            }
            return false;
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ESTreeViewSolver 功能介绍 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：绘制轻量树视图，支持折叠、搜索、选中、双击和右键扩展。\n\n" +
                "典型用途：资源目录树、RuntimeWatch 分组树、模块/状态机调试树、小型配置树。\n\n" +
                "伪代码：\n" +
                "private readonly ESTreeViewSolver tree = new ESTreeViewSolver();\n" +
                "private readonly List<ESTreeViewNode> roots = new List<ESTreeViewNode>();\n\n" +
                "roots.Add(new ESTreeViewNode(\"folder:Assets\", \"Assets\"));\n" +
                "roots[0].Children.Add(new ESTreeViewNode(\"asset:Config\", \"Config\", targetObject));\n" +
                "tree.SearchText = searchText;\n" +
                "tree.InitSolver(\n" +
                "    allowFolderSelection: false,\n" +
                "    onSelected: node => Select(node.Target),\n" +
                "    onDoubleClick: node => Ping(node.Target),\n" +
                "    onContextMenu: (node, menu) => menu.Add(\"复制Id\", () => Copy(node.Id)),\n" +
                "    getNodeHeight: node => node == selectedNode ? 24f : 20f,\n" +
                "    onDrawNodeContent: (node, rect, selected) => DrawMainLabel(node, rect),\n" +
                "    onDrawNodeRight: (node, rect) => DrawNodeButtons(node, rect),\n" +
                "    rightAreaWidth: 120f);\n" +
                "tree.Draw(roots);\n\n" +
                "说明：Target 为空的节点通常作为文件夹/分组；AllowFolderSelection=false 时文件夹只显示和折叠，不参与资源选择。");
        }
    }
#endif
}
