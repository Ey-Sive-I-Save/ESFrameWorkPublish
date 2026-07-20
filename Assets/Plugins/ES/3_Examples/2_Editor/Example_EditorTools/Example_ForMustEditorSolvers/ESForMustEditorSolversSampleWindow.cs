using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
#if UNITY_EDITOR
    public class ESForMustEditorSolversSampleWindow : EditorWindow
    {
        private readonly ESDropZoneSolver dropZone = new ESDropZoneSolver();
        private readonly ESTreeViewSolver tree = new ESTreeViewSolver();
        private readonly List<ESTreeViewNode> roots = new List<ESTreeViewNode>();

        private Vector2 scroll;
        private UnityEngine.Object selected;
        private string renameTo = string.Empty;
        private string lastDropMessage = "等待拖入资源";

        [MenuItem(MenuItemPathDefine.TEST_TOOLS_PATH + "编辑器 Solver/04 ForMustEditor Solver 综合案例", false, 40)]
        private static void Open()
        {
            GetWindow<ESForMustEditorSolversSampleWindow>("ES编辑器Solver案例");
        }

        private void OnEnable()
        {
            dropZone.InitSolver<UnityEngine.Object>(
                allowFolderExpand: true,
                rejectScripts: true,
                maxCount: 64);

            tree.InitSolver(
                allowFolderSelection: false,
                onSelected: node =>
                {
                    selected = node.Target;
                    renameTo = selected == null ? string.Empty : selected.name;
                },
                onDoubleClick: node => Ping(node.Target),
                onContextMenu: (node, menu) =>
                {
                    menu.Separator();
                    menu.Add("复制资源路径", () => GUIUtility.systemCopyBuffer = GetAssetPath(node.Target), node.Target != null);
                    menu.Add("复制资源GUID", () => GUIUtility.systemCopyBuffer = GetAssetGuid(node.Target), node.Target != null);
                    menu.Separator();
                    menu.Add("从案例树移除", () => RemoveNode(node.Id));
                });
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawDropZone();
            DrawTree();
            DrawSelectionPanel();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(
                    "这个案例用于验证 ESDropZoneSolver、ESTreeViewSolver、ESContextMenuSolver 的配合效果。\n" +
                    "把 Project 里的资源或文件夹拖到投放区，资源会按真实路径进入多层树；文件夹节点只作为分组显示，不会画成按钮。",
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
                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48)))
                {
                    roots.Clear();
                    selected = null;
                    lastDropMessage = "等待拖入资源";
                }
            }
        }

        private void DrawDropZone()
        {
            var area = GUILayoutUtility.GetRect(0, 76, GUILayout.ExpandWidth(true));
            if (dropZone.Draw(area, out var dropped))
            {
                foreach (var obj in dropped)
                    AddAssetNode(obj);

                lastDropMessage = $"已接收 {dropped.Length} 个对象";
            }

            var detail = string.IsNullOrEmpty(dropZone.LastRejectReason)
                ? dropZone.LastAcceptedCount > 0 ? $"松开鼠标后将接收 {dropZone.LastAcceptedCount} 个对象" : $"当前状态：{lastDropMessage}"
                : $"拒绝原因：{dropZone.LastRejectReason}";

            GUI.Label(new Rect(area.x, area.y + 14, area.width, 22), "拖拽资源或文件夹到这里", CenterBoldLabel());
            GUI.Label(new Rect(area.x, area.y + 40, area.width, 20), detail, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawTree()
        {
            EditorGUILayout.LabelField("投放结果树", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(200));
            if (roots.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有节点。请先从 Project 面板拖入资源或文件夹。", MessageType.None);
            }
            else
            {
                tree.Draw(roots);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectionPanel()
        {
            EditorGUILayout.Space(6);
            selected = EditorGUILayout.ObjectField("当前选中", selected, typeof(UnityEngine.Object), true);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("点击资源节点后，这里会显示路径、GUID，并允许测试 Undo/Dirty 改名。文件夹节点只用于分组，不会被选中。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("资源路径", GetAssetPath(selected));
            EditorGUILayout.LabelField("资源GUID", GetAssetGuid(selected));

            using (new EditorGUILayout.HorizontalScope())
            {
                renameTo = EditorGUILayout.TextField("对象名", renameTo);
                if (GUILayout.Button("应用改名", GUILayout.Width(88)))
                {
                    RecordAndDirty(selected, "通过 ES Solver 案例改名");
                    selected.name = renameTo;
                    MarkDirty(selected);
                }
                if (GUILayout.Button("定位", GUILayout.Width(56)))
                    Ping(selected);
            }
        }

        private void AddAssetNode(UnityEngine.Object asset)
        {
            if (asset == null) return;

            var path = GetAssetPath(asset);
            var id = string.IsNullOrEmpty(path) ? asset.GetInstanceID().ToString() : path;
            if (FindNodeById(roots, id) != null) return;

            var parent = GetOrCreateParentFolder(path);
            parent.Children.Add(new ESTreeViewNode(id, asset.name, asset));
        }

        private ESTreeViewNode GetOrCreateParentFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return GetOrCreateRoot("Scene Objects", "folder:Scene Objects");

            var directory = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                return GetOrCreateRoot("Assets", "folder:Assets");

            var parts = directory.Split('/');
            ESTreeViewNode current = null;
            var idPath = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                idPath = string.IsNullOrEmpty(idPath) ? parts[i] : idPath + "/" + parts[i];

                if (current == null)
                {
                    current = GetOrCreateRoot(parts[i], "folder:" + idPath);
                }
                else
                {
                    current = GetOrCreateChildFolder(current, parts[i], "folder:" + idPath);
                }

                tree.SetExpanded(current.Id, true);
            }

            return current ?? GetOrCreateRoot("Assets", "folder:Assets");
        }

        private ESTreeViewNode GetOrCreateRoot(string name, string id)
        {
            var existing = roots.Find(i => i.Id == id);
            if (existing != null) return existing;

            var node = new ESTreeViewNode(id, name);
            roots.Add(node);
            tree.SetExpanded(id, true);
            return node;
        }

        private static ESTreeViewNode GetOrCreateChildFolder(ESTreeViewNode parent, string name, string id)
        {
            var existing = parent.Children.Find(i => i.Id == id);
            if (existing != null) return existing;

            var node = new ESTreeViewNode(id, name);
            parent.Children.Add(node);
            return node;
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

        private static ESTreeViewNode FindNodeById(List<ESTreeViewNode> nodes, string id)
        {
            foreach (var node in nodes)
            {
                if (node.Id == id) return node;
                var found = FindNodeById(node.Children, id);
                if (found != null) return found;
            }
            return null;
        }

        private static void LogTutorial()
        {
            Debug.Log(
                "[ForMustEditor Solver 综合案例 - 临时 Debug 教程，后续会替换为更专业的帮助面板]\n" +
                "功能：演示 DropZone、TreeView、ContextMenu 三个 Solver 如何协作完成一个小型资源收集窗口。\n\n" +
                "流程：拖入资源或文件夹 -> DropZone 过滤对象 -> 按路径写入 TreeView -> 节点右键由 ContextMenu 提供命令。\n\n" +
                "伪代码：\n" +
                "private readonly ESDropZoneSolver dropZone = new ESDropZoneSolver();\n" +
                "private readonly ESTreeViewSolver tree = new ESTreeViewSolver();\n\n" +
                "void OnEnable()\n" +
                "{\n" +
                "    dropZone.InitSolver<UnityEngine.Object>(allowFolderExpand: true, rejectScripts: true, maxCount: 64);\n" +
                "    tree.InitSolver(\n" +
                "        allowFolderSelection: false,\n" +
                "        onContextMenu: (node, menu) =>\n" +
                "        {\n" +
                "            menu.Add(\"复制路径\", () => Copy(node.Id), node.Target != null);\n" +
                "            menu.Add(\"移除节点\", () => RemoveNode(node.Id));\n" +
                "        });\n" +
                "}\n\n" +
                "void OnGUI()\n" +
                "{\n" +
                "    if (dropZone.Draw(dropArea, out var dropped))\n" +
                "    {\n" +
                "        foreach (var obj in dropped)\n" +
                "        {\n" +
                "            var path = AssetDatabase.GetAssetPath(obj);\n" +
                "            var folderNode = GetOrCreateFolderNodes(path);\n" +
                "            folderNode.Children.Add(new ESTreeViewNode(path, obj.name, obj));\n" +
                "        }\n" +
                "    }\n" +
                "    tree.Draw(roots);\n" +
                "}\n\n" +
                "注意：DropZone 和 TreeView 都应作为成员字段持有，不要在 OnGUI 每帧 new。\n\n" +
                "预期验证：拖入文件夹后，应看到 Assets/子目录/资源 的多层结构；文件夹节点只作为分组，资源节点可选中、双击定位、右键操作。");
        }

        private static GUIStyle CenterBoldLabel()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            var path = GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static void Ping(UnityEngine.Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void RecordAndDirty(UnityEngine.Object asset, string undoName)
        {
            if (asset == null) return;
            Undo.RecordObject(asset, undoName);
            MarkDirty(asset);
        }

        private static void MarkDirty(UnityEngine.Object asset)
        {
            if (asset == null) return;
            EditorUtility.SetDirty(asset);
            if (PrefabUtility.IsPartOfPrefabInstance(asset))
                PrefabUtility.RecordPrefabInstancePropertyModifications(asset);
        }

    }
#endif
}
