using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    [Serializable]
    public class ESTreeViewNode
    {
        public string Id;
        public string Name;
        public UnityEngine.Object Target;
        public readonly List<ESTreeViewNode> Children = new List<ESTreeViewNode>();

        public ESTreeViewNode(string id, string name, UnityEngine.Object target = null)
        {
            Id = id;
            Name = name;
            Target = target;
        }
    }

    public class ESTreeViewSolver : ESEditorSolver
    {
        public string SearchText = string.Empty;
        public string SelectedId { get; private set; }
        public Action<ESTreeViewNode, ESContextMenuSolver> OnContextMenu;
        public Action<ESTreeViewNode> OnSelected;
        public Action<ESTreeViewNode> OnDoubleClick;
        public Func<ESTreeViewNode, float> GetNodeHeight;
        public Action<ESTreeViewNode, Rect, bool> OnDrawNodeContent;
        public Action<ESTreeViewNode, Rect> OnDrawNodeRight;
        public float RightAreaWidth = 0f;
        public bool AllowFolderSelection = false;

        private readonly HashSet<string> foldouts = new HashSet<string>();

        public ESTreeViewSolver InitSolver(
            bool allowFolderSelection = false,
            Action<ESTreeViewNode> onSelected = null,
            Action<ESTreeViewNode> onDoubleClick = null,
            Action<ESTreeViewNode, ESContextMenuSolver> onContextMenu = null,
            Func<ESTreeViewNode, float> getNodeHeight = null,
            Action<ESTreeViewNode, Rect, bool> onDrawNodeContent = null,
            Action<ESTreeViewNode, Rect> onDrawNodeRight = null,
            float rightAreaWidth = 0f)
        {
            AllowFolderSelection = allowFolderSelection;
            OnSelected = onSelected;
            OnDoubleClick = onDoubleClick;
            OnContextMenu = onContextMenu;
            GetNodeHeight = getNodeHeight;
            OnDrawNodeContent = onDrawNodeContent;
            OnDrawNodeRight = onDrawNodeRight;
            RightAreaWidth = rightAreaWidth;
            return CompleteInitSolver<ESTreeViewSolver>();
        }

        public bool IsExpanded(string id) => !string.IsNullOrEmpty(id) && foldouts.Contains(id);

        public void SetExpanded(string id, bool expanded)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (expanded) foldouts.Add(id);
            else foldouts.Remove(id);
        }

        public void ExpandAll(IEnumerable<ESTreeViewNode> nodes)
        {
            Visit(nodes, node => SetExpanded(node.Id, true));
        }

        public void CollapseAll(IEnumerable<ESTreeViewNode> nodes)
        {
            Visit(nodes, node => SetExpanded(node.Id, false));
        }

        public void Select(ESTreeViewNode node)
        {
            if (node == null) return;
            SelectedId = node.Id;
            OnSelected?.Invoke(node);
        }

        public void Draw(IEnumerable<ESTreeViewNode> roots)
        {
#if UNITY_EDITOR
            if (roots == null) return;
            foreach (var node in roots)
                DrawNode(node, 0);
#endif
        }

        private void DrawNode(ESTreeViewNode node, int depth)
        {
#if UNITY_EDITOR
            if (node == null || !PassSearch(node)) return;

            var hasChildren = node.Children.Count > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14f);
                if (hasChildren)
                {
                    var foldoutRect = GUILayoutUtility.GetRect(16, EditorGUIUtility.singleLineHeight, GUILayout.Width(16));
                    var next = EditorGUI.Foldout(foldoutRect, IsExpanded(node.Id), string.Empty, true);
                    SetExpanded(node.Id, next);
                }
                else
                {
                    GUILayout.Space(16);
                }
                
                var nodeHeight = Mathf.Max(EditorGUIUtility.singleLineHeight + 2, GetNodeHeight?.Invoke(node) ?? EditorGUIUtility.singleLineHeight + 2);
                var selected = SelectedId == node.Id;
                var label = node.Target == null ? node.Name : $"{node.Name}  ({node.Target.GetType().Name})";
                var rect = GUILayoutUtility.GetRect(0, nodeHeight, GUILayout.ExpandWidth(true));
                var canSelect = node.Target != null || AllowFolderSelection;

                if (selected && canSelect)
                    EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.90f, 0.22f));

                if (OnDrawNodeContent != null)
                {
                    OnDrawNodeContent.Invoke(node, rect, selected);
                }
                else
                {
                    var labelStyle = node.Target == null ? EditorStyles.boldLabel : EditorStyles.label;
                    EditorGUI.LabelField(rect, label, labelStyle);
                }

                if (OnDrawNodeRight != null && RightAreaWidth > 0)
                {
                    var rightRect = GUILayoutUtility.GetRect(RightAreaWidth, nodeHeight, GUILayout.Width(RightAreaWidth));
                    OnDrawNodeRight.Invoke(node, rightRect);
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition))
                {
                    OnDoubleClick?.Invoke(node);
                    Event.current.Use();
                }
                else if (canSelect && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
                {
                    Select(node);
                    Event.current.Use();
                }

                ESContextMenuSolver.HandleContextClick(rect, menu =>
                {
                    menu.Add("选择", () => Select(node), canSelect);
                    menu.Add("定位", () => ESStandUtility.SafeEditor.Ping(node.Target), node.Target != null);
                    OnContextMenu?.Invoke(node, menu);
                });
            }

            if (hasChildren && IsExpanded(node.Id))
            {
                foreach (var child in node.Children)
                    DrawNode(child, depth + 1);
            }
#endif
        }

        private bool PassSearch(ESTreeViewNode node)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (!string.IsNullOrEmpty(node.Name) && node.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (var child in node.Children)
            {
                if (PassSearch(child))
                    return true;
            }
            return false;
        }

        private static void Visit(IEnumerable<ESTreeViewNode> nodes, Action<ESTreeViewNode> visitor)
        {
            if (nodes == null || visitor == null) return;
            foreach (var node in nodes)
            {
                if (node == null) continue;
                visitor(node);
                Visit(node.Children, visitor);
            }
        }
    }
}
