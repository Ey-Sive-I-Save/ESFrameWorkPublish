#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ES.EditorInternal;

namespace ES
{
    /// <summary>
    /// 世界对话编辑器的对话图与空间放置工具。
    /// 图资产负责节点/边权威数据，地图资产负责放置记录，Scene 只保存 3D 锚点投影。
    /// </summary>
    public sealed class ESWorldDialogueEditorWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "对话";
        private enum ViewMode : byte { Graph, Map2D, Scene3D }

        private const string GraphGuidSessionKey = "ES.WorldDialogueWorkbench.GraphGuid";
        private const string MapGuidSessionKey = "ES.WorldDialogueWorkbench.MapGuid";

        private ESWorldDialogueGraphAsset graphAsset;
        private ESWorldMapAsset mapAsset;
        private SerializedObject graphSerialized;
        private SerializedObject mapSerialized;
        private ViewMode viewMode;
        private int selectedNodeIndex = -1;
        private int selectedPlacementIndex = -1;
        private int connectTargetIndex = -1;
        private string status = "请选择对话图资产和地图资产。";
        private MessageType statusType = MessageType.Info;
        private bool sceneHooked;
        private bool mapDragUndoRecorded;

        [MenuItem("【ES】/内容制作/世界/对话编辑器", false, 122)]
        private static void Open()
        {
            ESWorldDialogueEditorWindow window = GetWindow<ESWorldDialogueEditorWindow>("ES 世界对话编辑器");
            window.minSize = new Vector2(920f, 620f);
            window.Show();
            window.Focus();
        }

        public static void OpenFor(ESWorldMapAsset asset, EditorWindow owner = null)
        {
            ESWorldDialogueEditorWindow window = GetWindow<ESWorldDialogueEditorWindow>("ES 世界对话编辑器");
            window.minSize = new Vector2(920f, 620f);
            window.BindMap(asset);
            window.Show();
            window.Focus();
        }

        public static void OpenFor(ESWorldDialogueGraphAsset asset, ESWorldMapAsset map = null, EditorWindow owner = null)
        {
            ESWorldDialogueEditorWindow window = GetWindow<ESWorldDialogueEditorWindow>("ES 世界对话编辑器");
            window.minSize = new Vector2(920f, 620f);
            window.BindGraph(asset);
            if (map != null) window.BindMap(map);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            ESWindowFoundation.BindWithStandardSystemHost(
                this,
                ESWindowFoundation.EnsureStandardSystemActionBar(this));
            if (!sceneHooked)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                sceneHooked = true;
            }
            RestoreAssets();
        }

        private void OnDisable()
        {
            ESWindowFoundation.Unbind(this, true);
            if (sceneHooked)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                sceneHooked = false;
            }
            graphSerialized = null;
            mapSerialized = null;
        }

        private void OnGUI()
        {
            DrawHero();
            DrawToolbar();
            DrawStatus();
            if (graphAsset == null && mapAsset == null)
            {
                DrawEmptyState();
                return;
            }

            if (viewMode == ViewMode.Graph) DrawGraphMode();
            else if (viewMode == ViewMode.Map2D) DrawMap2DMode();
            else DrawScene3DMode();
        }

        private void DrawHero()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), ESEditorPresentation.GetDepthAccent(0));
                ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, ESStatusKind.None));
            }
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 20f, 24f), "ES 世界对话编辑器", ESEditorPresentation.HeaderStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 34f, rect.width - 20f, 18f), "Graph 数据流 · 2D 地图放置 · 3D Scene 拖放 · 稳定资产保存", ESEditorPresentation.MetaStyle);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(ESEditorPresentation.ToolbarStyle))
            {
                ESWorldDialogueGraphAsset nextGraph = (ESWorldDialogueGraphAsset)EditorGUILayout.ObjectField("对话图", graphAsset, typeof(ESWorldDialogueGraphAsset), false, GUILayout.MinWidth(190f), GUILayout.MaxWidth(330f));
                if (nextGraph != graphAsset) BindGraph(nextGraph);
                ESWorldMapAsset nextMap = (ESWorldMapAsset)EditorGUILayout.ObjectField("地图", mapAsset, typeof(ESWorldMapAsset), false, GUILayout.MinWidth(190f), GUILayout.MaxWidth(330f));
                if (nextMap != mapAsset) BindMap(nextMap);
                if (GUILayout.Button("创建图", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(64f))) CreateGraphAsset();
                if (GUILayout.Button("新增节点", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(72f))) AddNode();
                if (GUILayout.Button("保存", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(58f))) SaveAll();
                if (GUILayout.Button("验证", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(58f))) ValidateAll();
                GUILayout.FlexibleSpace();
                viewMode = (ViewMode)GUILayout.Toolbar((int)viewMode, new[] { "Graph 数据流", "2D 地图", "Scene 2D/3D" }, ESEditorPresentation.ToolbarButtonStyle, GUILayout.Width(245f));
            }
        }

        private void DrawStatus()
        {
            using (new EditorGUILayout.HorizontalScope(ESEditorPresentation.SurfaceStyle))
            {
                ESStatusKind semantic = statusType == MessageType.Error ? ESStatusKind.Error : statusType == MessageType.Warning ? ESStatusKind.Warning : ESStatusKind.Ready;
                GUILayout.Label(status, ESEditorPresentation.MetaStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(graphAsset == null ? "图：未绑定" : "图：" + graphAsset.name, ESEditorPresentation.MetaStyle);
                GUILayout.Label(mapAsset == null ? "地图：未绑定" : "地图：" + mapAsset.name, ESEditorPresentation.MetaStyle);
                if (Event.current.type == EventType.Repaint)
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, semantic));
                }
            }
        }

        private void DrawEmptyState()
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("对话编辑器尚未绑定内容", ESEditorPresentation.HeaderStyle);
                EditorGUILayout.HelpBox("先创建或拖入 ESWorldDialogueGraphAsset；要把入口放进地图，还需要绑定 ESWorldMapAsset。", MessageType.Info);
                if (GUILayout.Button("创建对话图资产", GUILayout.Height(30f))) CreateGraphAsset();
            }
        }

        private void DrawGraphMode()
        {
            if (graphAsset == null)
            {
                EditorGUILayout.HelpBox("Graph 数据流需要先绑定对话图资产。", MessageType.Warning);
                return;
            }
            EnsureGraphDefinition();
            graphSerialized.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect canvas = GUILayoutUtility.GetRect(640f, 500f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                Rect inspector = GUILayoutUtility.GetRect(260f, 500f, GUILayout.Width(280f), GUILayout.ExpandHeight(true));
                DrawGraphCanvas(canvas);
                DrawNodeInspector(inspector);
            }
            if (graphSerialized.hasModifiedProperties)
            {
                graphSerialized.ApplyModifiedProperties();
                ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
                status = "对话图有未保存修改。";
                statusType = MessageType.Info;
            }
        }

        private void DrawGraphCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.075f, 0.085f, 0.105f, 1f));
            Handles.BeginGUI();
            ESWorldDialogueGraphDefinition definition = graphAsset.Definition;
            var rects = new Dictionary<string, Rect>(StringComparer.Ordinal);
            for (int i = 0; i < definition.nodes.Count; i++)
            {
                ESWorldDialogueNodeData node = definition.nodes[i];
                if (node == null) continue;
                rects[node.nodeId] = new Rect(canvas.x + node.graphPosition.x, canvas.y + node.graphPosition.y, 220f, 92f + (node.outputs == null ? 0 : node.outputs.Count * 18f));
            }
            if (definition.edges != null)
                for (int i = 0; i < definition.edges.Count; i++)
                {
                    ESWorldDialogueEdgeData edge = definition.edges[i];
                    if (edge == null || !rects.TryGetValue(edge.fromNodeId, out Rect from) || !rects.TryGetValue(edge.toNodeId, out Rect to)) continue;
                    Handles.color = ESEditorPresentation.GetDepthAccent(1);
                    Handles.DrawBezier(from.center + Vector2.right * 100f, to.center - Vector2.right * 100f, from.center + Vector2.right * 140f, to.center - Vector2.right * 140f, Handles.color, null, 2f);
                }
            Handles.EndGUI();

            for (int i = 0; i < definition.nodes.Count; i++)
            {
                ESWorldDialogueNodeData node = definition.nodes[i];
                if (node == null) continue;
                Rect initial = rects[node.nodeId];
                Color old = GUI.color;
                GUI.color = selectedNodeIndex == i ? new Color(0.75f, 0.95f, 1f) : Color.white;
                Rect moved = GUI.Window(7000 + i, initial, _ => DrawNodeWindow(i, node), node.title ?? "对话节点");
                GUI.color = old;
                if (moved.position != initial.position)
                {
                    if (Event.current.type == EventType.Repaint) Undo.RecordObject(graphAsset, "移动对话节点");
                    Vector2 next = moved.position - new Vector2(canvas.x, canvas.y);
                    node.graphPosition = new Vector2(
                        Mathf.Clamp(next.x, 0f, Mathf.Max(0f, canvas.width - moved.width)),
                        Mathf.Clamp(next.y, 0f, Mathf.Max(0f, canvas.height - moved.height)));
                    ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
                }
            }
        }

        private void DrawNodeWindow(int index, ESWorldDialogueNodeData node)
        {
            if (GUILayout.Button("选择", EditorStyles.miniButton)) selectedNodeIndex = index;
            EditorGUILayout.LabelField(node.speaker ?? "旁白", EditorStyles.miniLabel);
            string preview = string.IsNullOrWhiteSpace(node.text) ? "（空文本）" : node.text.Replace("\n", " ");
            EditorGUILayout.LabelField(preview, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(32f));
            if (node.outputs != null)
                for (int i = 0; i < node.outputs.Count; i++)
                    EditorGUILayout.LabelField("→ " + (node.outputs[i] == null ? "缺失端口" : node.outputs[i].displayName), EditorStyles.miniLabel);
            GUI.DragWindow(new Rect(0f, 0f, 220f, 20f));
        }

        private void DrawNodeInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, ESEditorPresentation.SurfaceStyle);
            GUILayout.Space(8f);
            GUILayout.Label("节点检查器", ESEditorPresentation.HeaderStyle);
            ESWorldDialogueGraphDefinition definition = graphAsset.Definition;
            if (selectedNodeIndex < 0 || selectedNodeIndex >= definition.nodes.Count || definition.nodes[selectedNodeIndex] == null)
            {
                EditorGUILayout.HelpBox("选择一个 Graph 节点后编辑内容。", MessageType.Info);
                GUILayout.EndArea();
                return;
            }
            SerializedProperty nodes = graphSerialized.FindProperty("definition.nodes");
            SerializedProperty node = nodes.GetArrayElementAtIndex(selectedNodeIndex);
            EditorGUILayout.LabelField("稳定 Node ID", node.FindPropertyRelative("nodeId").stringValue, EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("title"), new GUIContent("标题"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("speaker"), new GUIContent("说话者"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("text"), new GUIContent("文本"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("outputs"), new GUIContent("输出端口"), true);
            if (GUILayout.Button("新增输出端口"))
            {
                graphSerialized.ApplyModifiedProperties();
                Undo.RecordObject(graphAsset, "新增对话输出端口");
                definition.nodes[selectedNodeIndex].outputs.Add(new ESWorldDialoguePortData { portId = Guid.NewGuid().ToString("N"), displayName = "选项" });
                ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
                graphSerialized.Update();
            }
            if (GUILayout.Button("设为入口节点"))
            {
                graphSerialized.ApplyModifiedProperties();
                Undo.RecordObject(graphAsset, "设置对话入口节点");
                definition.entryNodeId = definition.nodes[selectedNodeIndex].nodeId;
                ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
                graphSerialized.Update();
            }
            if (definition.nodes.Count > 1 && GUILayout.Button("删除节点")) DeleteSelectedNode();
            GUILayout.Space(8f);
            GUILayout.Label("数据流连接", ESEditorPresentation.SubtitleStyle);
            var targets = new List<string>();
            for (int i = 0; i < definition.nodes.Count; i++) targets.Add(i + " · " + (definition.nodes[i] == null ? "缺失" : definition.nodes[i].title));
            connectTargetIndex = EditorGUILayout.Popup("目标节点", connectTargetIndex, targets.ToArray());
            using (new EditorGUI.DisabledScope(connectTargetIndex < 0 || connectTargetIndex == selectedNodeIndex))
                if (GUILayout.Button("连接到目标节点")) ConnectSelectedNode(connectTargetIndex);
            GUILayout.EndArea();
        }

        private void DrawMap2DMode()
        {
            if (mapAsset == null)
            {
                EditorGUILayout.HelpBox("2D 地图放置需要绑定 ESWorldMapAsset。", MessageType.Warning);
                return;
            }
            if (graphAsset == null) EditorGUILayout.HelpBox("可从 Project 拖入 ESWorldDialogueGraphAsset，或先在工具栏绑定对话图。", MessageType.Info);
            mapSerialized = mapSerialized != null && mapSerialized.targetObject == mapAsset ? mapSerialized : new SerializedObject(mapAsset);
            mapSerialized.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect canvas = GUILayoutUtility.GetRect(620f, 500f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                Rect inspector = GUILayoutUtility.GetRect(260f, 500f, GUILayout.Width(280f), GUILayout.ExpandHeight(true));
                DrawMapCanvas(canvas);
                DrawPlacementInspector(inspector);
            }
            if (mapSerialized.hasModifiedProperties)
            {
                mapSerialized.ApplyModifiedProperties();
                ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            }
        }

        private void DrawMapCanvas(Rect canvas)
        {
            ESWorldMapDefinition definition = mapAsset.Definition;
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = new Vector2(256f, 256f); }
            EditorGUI.DrawRect(canvas, ESWorldMapEditorPresentation.TerrainBase);
            Handles.BeginGUI();
            int gridX = Mathf.Max(1, definition.spaceTemplate == null ? 16 : definition.spaceTemplate.gridWidth);
            int gridY = Mathf.Max(1, definition.spaceTemplate == null ? 16 : definition.spaceTemplate.gridHeight);
            for (int x = 0; x <= gridX; x++)
            {
                float px = Mathf.Lerp(canvas.xMin, canvas.xMax, x / (float)gridX);
                Handles.color = ESWorldMapEditorPresentation.Grid;
                Handles.DrawLine(new Vector3(px, canvas.yMin), new Vector3(px, canvas.yMax));
            }
            for (int y = 0; y <= gridY; y++)
            {
                float py = Mathf.Lerp(canvas.yMax, canvas.yMin, y / (float)gridY);
                Handles.DrawLine(new Vector3(canvas.xMin, py), new Vector3(canvas.xMax, py));
            }
            Handles.EndGUI();
            List<ESWorldDialoguePlacement> placements = definition.dialoguePlacements;
            if (placements != null)
                for (int i = 0; i < placements.Count; i++)
                {
                    ESWorldDialoguePlacement placement = placements[i];
                    if (placement == null || placement.space != ESWorldDialoguePlacementSpace.Map2D) continue;
                    Vector2 point = WorldToCanvas(new Vector2(placement.position.x, placement.position.z), min, max, canvas);
                    Handles.color = selectedPlacementIndex == i ? ESEditorPresentation.GetDepthAccent(0) : ESWorldMapEditorPresentation.Poi;
                    Handles.BeginGUI();
                    Handles.DrawSolidDisc(point, Vector3.forward, selectedPlacementIndex == i ? 8f : 6f);
                    Handles.EndGUI();
                    GUI.Label(new Rect(point.x + 9f, point.y - 9f, 180f, 18f), placement.displayName ?? placement.dialogueGraphKey, ESEditorPresentation.MetaStyle);
                }

            Event evt = Event.current;
            ESWorldDialogueGraphAsset dropped = FindDraggedGraph();
            if (canvas.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropped != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    BindGraph(dropped);
                    AddMapPlacement(CanvasToWorld(evt.mousePosition, min, max, canvas));
                }
                evt.Use();
                return;
            }
            if (canvas.Contains(evt.mousePosition) && evt.type == EventType.MouseDown && evt.button == 0)
            {
                selectedPlacementIndex = FindPlacementAt(evt.mousePosition, min, max, canvas);
                if (selectedPlacementIndex < 0 && graphAsset != null) AddMapPlacement(CanvasToWorld(evt.mousePosition, min, max, canvas));
                GUI.FocusControl(null);
                evt.Use();
            }
            if (canvas.Contains(evt.mousePosition) && evt.type == EventType.MouseDrag && evt.button == 0 && selectedPlacementIndex >= 0)
            {
                if (!mapDragUndoRecorded) { Undo.RecordObject(mapAsset, "移动 2D 对话入口"); mapDragUndoRecorded = true; }
                ESWorldDialoguePlacement placement = placements[selectedPlacementIndex];
                Vector2 world = CanvasToWorld(evt.mousePosition, min, max, canvas);
                placement.position = new Vector3(world.x, placement.position.y, world.y);
                ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
                Repaint();
                evt.Use();
            }
            if (evt.type == EventType.MouseUp) mapDragUndoRecorded = false;
        }

        private void DrawPlacementInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, ESEditorPresentation.SurfaceStyle);
            if (mapAsset.Definition.dialoguePlacements == null || selectedPlacementIndex < 0 || selectedPlacementIndex >= mapAsset.Definition.dialoguePlacements.Count)
            {
                EditorGUILayout.HelpBox("点击 2D 入口查看放置检查器；拖入对话图即可创建入口。", MessageType.Info);
                GUILayout.EndArea();
                return;
            }
            SerializedProperty placements = mapSerialized.FindProperty("definition.dialoguePlacements");
            SerializedProperty placement = placements.GetArrayElementAtIndex(selectedPlacementIndex);
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("空间放置检查器", ESEditorPresentation.HeaderStyle);
                EditorGUILayout.LabelField("Placement ID", placement.FindPropertyRelative("placementId").stringValue, EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(placement.FindPropertyRelative("displayName"), new GUIContent("显示名"));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(placement.FindPropertyRelative("dialogueGraphKey"), new GUIContent("对话图 Key"));
                    EditorGUILayout.PropertyField(placement.FindPropertyRelative("dialogueGraphAssetGuid"), new GUIContent("对话图 GUID"));
                    EditorGUILayout.PropertyField(placement.FindPropertyRelative("entryNodeId"), new GUIContent("入口节点"));
                }
                EditorGUILayout.PropertyField(placement.FindPropertyRelative("position"), new GUIContent("位置"));
                EditorGUILayout.PropertyField(placement.FindPropertyRelative("eulerAngles"), new GUIContent("旋转"));
                EditorGUILayout.PropertyField(placement.FindPropertyRelative("scale"), new GUIContent("缩放"));
                EditorGUILayout.LabelField("空间", placement.FindPropertyRelative("space").enumDisplayNames[placement.FindPropertyRelative("space").enumValueIndex]);
                if (GUILayout.Button("加载此入口的对话图")) LoadGraphFromSelectedPlacement();
                using (new EditorGUI.DisabledScope(graphAsset == null))
                    if (GUILayout.Button("将当前对话图绑定到入口")) BindCurrentGraphToSelectedPlacement();
                if (GUILayout.Button("删除放置"))
                {
                    mapSerialized.ApplyModifiedProperties();
                    Undo.RecordObject(mapAsset, "删除对话入口");
                    mapAsset.Definition.dialoguePlacements.RemoveAt(selectedPlacementIndex);
                    selectedPlacementIndex = -1;
                    ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
                    mapSerialized.Update();
                }
            }
            GUILayout.EndArea();
        }

        private void DrawScene3DMode()
        {
            EditorGUILayout.HelpBox("在 SceneView 中直接拖入 ESWorldDialogueGraphAsset。SceneView 为 2D 模式时写入 XY 平面锚点，否则写入 3D 地表/水平面锚点。Scene 对象和地图放置记录分层保存。", MessageType.Info);
            if (mapAsset == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("同步当前场景锚点", GUILayout.Height(28f))) SyncSceneAnchors();
                if (GUILayout.Button("保存地图与场景", GUILayout.Height(28f))) SaveAll();
            }
            EditorGUILayout.LabelField("当前地图 Scene 入口", GetScenePlacementCount().ToString());
            EditorGUILayout.HelpBox("拖放后请点击“保存地图与场景”完成正式落盘；关闭窗口不会自动保存。", MessageType.None);
        }

        private int GetScenePlacementCount()
        {
            if (mapAsset == null || mapAsset.Definition == null || mapAsset.Definition.dialoguePlacements == null) return 0;
            int count = 0;
            for (int i = 0; i < mapAsset.Definition.dialoguePlacements.Count; i++)
                if (mapAsset.Definition.dialoguePlacements[i] != null && mapAsset.Definition.dialoguePlacements[i].space != ESWorldDialoguePlacementSpace.Map2D) count++;
            return count;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (viewMode != ViewMode.Scene3D || mapAsset == null) return;
            Event evt = Event.current;
            ESWorldDialogueGraphAsset dropped = FindDraggedGraph();
            if (dropped == null || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                BindGraph(dropped);
                ESWorldDialoguePlacementSpace space = sceneView.in2DMode ? ESWorldDialoguePlacementSpace.Scene2D : ESWorldDialoguePlacementSpace.Scene3D;
                AddSceneAnchor(ResolveScenePoint(sceneView, evt.mousePosition, sceneView.in2DMode), space);
            }
            evt.Use();
            sceneView.Repaint();
        }

        private Vector3 ResolveScenePoint(SceneView sceneView, Vector2 guiPoint, bool is2D)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            if (!is2D && Physics.Raycast(ray, out RaycastHit hit, 10000f)) return hit.point;
            Plane plane = is2D ? new Plane(Vector3.forward, Vector3.zero) : new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin + ray.direction * 10f;
        }

        private void AddSceneAnchor(Vector3 position, ESWorldDialoguePlacementSpace space)
        {
            if (mapAsset == null || graphAsset == null) { SetStatus("需要同时绑定地图和对话图。", MessageType.Warning); return; }
            EnsureMapPlacements();
            string placementId = Guid.NewGuid().ToString("N");
            string graphKey = ESWorldDialogueAuthoringUtility.GetAssetKey(graphAsset);
            string graphGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graphAsset));
            string scenePath = SceneManager.GetActiveScene().path;
            string mapGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mapAsset));
            Undo.RecordObject(mapAsset, "拖放对话入口到 Scene");
            GameObject anchorObject = new GameObject("ES 对话入口 · " + graphAsset.name);
            Undo.RegisterCreatedObjectUndo(anchorObject, "拖放对话入口到 Scene");
            anchorObject.transform.position = position;
            ESWorldDialogueAnchor anchor = anchorObject.AddComponent<ESWorldDialogueAnchor>();
            anchor.placementId = placementId;
            anchor.dialogueGraphKey = graphKey;
            anchor.dialogueGraphAssetGuid = graphGuid;
            anchor.entryNodeId = graphAsset.Definition.entryNodeId;
            anchor.mapAssetGuid = mapGuid;
            anchor.sceneObjectKey = "dialogue.anchor." + placementId;
            anchor.placementSpace = space;
            mapAsset.Definition.dialoguePlacements.Add(new ESWorldDialoguePlacement
            {
                placementId = placementId,
                dialogueGraphKey = graphKey,
                dialogueGraphAssetGuid = graphGuid,
                entryNodeId = graphAsset.Definition.entryNodeId,
                displayName = graphAsset.name,
                space = space,
                position = position,
                eulerAngles = anchorObject.transform.eulerAngles,
                scale = anchorObject.transform.localScale,
                scenePath = scenePath,
                sceneObjectKey = anchor.sceneObjectKey
            });
            EditorUtility.SetDirty(anchor);
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = anchorObject;
            SetStatus("已创建 3D 对话入口，点击保存完成地图与场景落盘。", MessageType.Info);
        }

        private void SyncSceneAnchors()
        {
            if (mapAsset == null) return;
            EnsureMapPlacements();
            ESWorldDialogueAnchor[] anchors = Resources.FindObjectsOfTypeAll<ESWorldDialogueAnchor>();
            int updated = 0;
            Undo.RecordObject(mapAsset, "同步 Scene 对话锚点");
            for (int i = 0; i < anchors.Length; i++)
            {
                ESWorldDialogueAnchor anchor = anchors[i];
                if (anchor == null || EditorUtility.IsPersistent(anchor) || anchor.gameObject.scene != SceneManager.GetActiveScene()) continue;
                Undo.RecordObject(anchor, "同步 Scene 对话锚点");
                ESWorldDialoguePlacement placement = FindPlacement(anchor.placementId);
                if (placement == null)
                {
                    placement = new ESWorldDialoguePlacement { placementId = string.IsNullOrWhiteSpace(anchor.placementId) ? Guid.NewGuid().ToString("N") : anchor.placementId };
                    anchor.placementId = placement.placementId;
                    mapAsset.Definition.dialoguePlacements.Add(placement);
                }
                placement.dialogueGraphKey = anchor.dialogueGraphKey;
                placement.dialogueGraphAssetGuid = anchor.dialogueGraphAssetGuid;
                placement.entryNodeId = anchor.entryNodeId;
                placement.space = anchor.placementSpace == ESWorldDialoguePlacementSpace.Map2D ? ESWorldDialoguePlacementSpace.Scene3D : anchor.placementSpace;
                placement.position = anchor.transform.position;
                placement.eulerAngles = anchor.transform.eulerAngles;
                placement.scale = anchor.transform.localScale;
                placement.scenePath = anchor.gameObject.scene.path;
                placement.sceneObjectKey = string.IsNullOrWhiteSpace(anchor.sceneObjectKey) ? "dialogue.anchor." + placement.placementId : anchor.sceneObjectKey;
                anchor.sceneObjectKey = placement.sceneObjectKey;
                anchor.mapAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mapAsset));
                EditorUtility.SetDirty(anchor);
                updated++;
            }
            if (updated > 0)
            {
                ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            SetStatus("已同步当前场景对话锚点：" + updated + " 个。", MessageType.Info);
        }

        private ESWorldDialogueGraphAsset FindDraggedGraph()
        {
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            for (int i = 0; i < references.Length; i++)
                if (references[i] is ESWorldDialogueGraphAsset asset) return asset;
            return null;
        }

        private void AddMapPlacement(Vector2 worldPoint)
        {
            if (mapAsset == null || graphAsset == null) { SetStatus("需要同时绑定地图和对话图。", MessageType.Warning); return; }
            EnsureMapPlacements();
            Undo.RecordObject(mapAsset, "放置 2D 对话入口");
            string placementId = Guid.NewGuid().ToString("N");
            mapAsset.Definition.dialoguePlacements.Add(new ESWorldDialoguePlacement
            {
                placementId = placementId,
                dialogueGraphKey = ESWorldDialogueAuthoringUtility.GetAssetKey(graphAsset),
                dialogueGraphAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graphAsset)),
                entryNodeId = graphAsset.Definition.entryNodeId,
                displayName = graphAsset.name,
                space = ESWorldDialoguePlacementSpace.Map2D,
                position = new Vector3(worldPoint.x, 0f, worldPoint.y),
                scale = Vector3.one,
                sceneObjectKey = "dialogue.map2d." + placementId
            });
            selectedPlacementIndex = mapAsset.Definition.dialoguePlacements.Count - 1;
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            SetStatus("已放置 2D 对话入口。", MessageType.Info);
        }

        private void AddNode()
        {
            if (graphAsset == null) { SetStatus("请先绑定对话图资产。", MessageType.Warning); return; }
            EnsureGraphDefinition();
            Undo.RecordObject(graphAsset, "新增对话节点");
            ESWorldDialogueNodeData node = new ESWorldDialogueNodeData
            {
                nodeId = Guid.NewGuid().ToString("N"),
                title = "对话节点 " + (graphAsset.Definition.nodes.Count + 1),
                graphPosition = new Vector2(80f + graphAsset.Definition.nodes.Count * 24f, 100f + graphAsset.Definition.nodes.Count * 18f),
                outputs = new List<ESWorldDialoguePortData> { new ESWorldDialoguePortData { portId = Guid.NewGuid().ToString("N"), displayName = "继续" } }
            };
            graphAsset.Definition.nodes.Add(node);
            if (string.IsNullOrWhiteSpace(graphAsset.Definition.entryNodeId)) graphAsset.Definition.entryNodeId = node.nodeId;
            selectedNodeIndex = graphAsset.Definition.nodes.Count - 1;
            ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
            SetStatus("已新增对话节点。", MessageType.Info);
        }

        private void ConnectSelectedNode(int targetIndex)
        {
            ESWorldDialogueGraphDefinition definition = graphAsset == null ? null : graphAsset.Definition;
            if (definition == null || selectedNodeIndex < 0 || selectedNodeIndex >= definition.nodes.Count || targetIndex < 0 || targetIndex >= definition.nodes.Count || targetIndex == selectedNodeIndex) return;
            graphSerialized?.ApplyModifiedProperties();
            Undo.RecordObject(graphAsset, "连接对话数据流");
            ESWorldDialogueNodeData from = definition.nodes[selectedNodeIndex];
            ESWorldDialogueNodeData to = definition.nodes[targetIndex];
            if (from.outputs == null) from.outputs = new List<ESWorldDialoguePortData>();
            if (from.outputs.Count == 0) from.outputs.Add(new ESWorldDialoguePortData { portId = Guid.NewGuid().ToString("N"), displayName = "继续" });
            if (definition.edges == null) definition.edges = new List<ESWorldDialogueEdgeData>();
            definition.edges.Add(new ESWorldDialogueEdgeData
            {
                edgeId = Guid.NewGuid().ToString("N"),
                fromNodeId = from.nodeId,
                fromPortId = from.outputs[0].portId,
                toNodeId = to.nodeId,
                toPortId = string.Empty
            });
            ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
            graphSerialized?.Update();
            SetStatus("已连接对话数据流。", MessageType.Info);
        }

        private void DeleteSelectedNode()
        {
            if (graphAsset == null || selectedNodeIndex < 0 || selectedNodeIndex >= graphAsset.Definition.nodes.Count) return;
            graphSerialized?.ApplyModifiedProperties();
            string nodeId = graphAsset.Definition.nodes[selectedNodeIndex].nodeId;
            Undo.RecordObject(graphAsset, "删除对话节点");
            graphAsset.Definition.nodes.RemoveAt(selectedNodeIndex);
            if (graphAsset.Definition.edges != null)
                graphAsset.Definition.edges.RemoveAll(edge => edge == null || edge.fromNodeId == nodeId || edge.toNodeId == nodeId);
            graphAsset.Definition.entryNodeId = graphAsset.Definition.nodes.Count == 0 ? string.Empty : graphAsset.Definition.nodes[0].nodeId;
            selectedNodeIndex = Mathf.Clamp(selectedNodeIndex - 1, -1, graphAsset.Definition.nodes.Count - 1);
            connectTargetIndex = -1;
            ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
            graphSerialized?.Update();
            SetStatus("已删除对话节点及其数据流。", MessageType.Info);
        }

        private void EnsureGraphDefinition()
        {
            if (graphAsset == null || graphAsset.Definition == null) return;
            if (!NeedsStableIdRepair(graphAsset.Definition)) return;
            Undo.RecordObject(graphAsset, "修复对话图稳定身份");
            if (graphAsset.Definition.EnsureStableIds())
            {
                ESWorldDialogueAuthoringUtility.MarkChanged(graphAsset);
            }
        }

        private static bool NeedsStableIdRepair(ESWorldDialogueGraphDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.graphId) || definition.nodes == null || definition.edges == null) return true;
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.nodes.Count; i++)
            {
                ESWorldDialogueNodeData node = definition.nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId) || node.outputs == null) return true;
                nodeIds.Add(node.nodeId);
                var portIds = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < node.outputs.Count; p++)
                {
                    if (node.outputs[p] == null || string.IsNullOrWhiteSpace(node.outputs[p].portId)) return true;
                    portIds.Add(node.outputs[p].portId);
                }
            }
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.edges.Count; i++)
            {
                if (definition.edges[i] == null || string.IsNullOrWhiteSpace(definition.edges[i].edgeId)) return true;
                edgeIds.Add(definition.edges[i].edgeId);
            }
            return definition.nodes.Count > 0 && string.IsNullOrWhiteSpace(definition.entryNodeId);
        }

        private void EnsureMapPlacements()
        {
            if (mapAsset.Definition.dialoguePlacements == null) mapAsset.Definition.dialoguePlacements = new List<ESWorldDialoguePlacement>();
        }

        private ESWorldDialoguePlacement FindPlacement(string placementId)
        {
            if (mapAsset == null || mapAsset.Definition.dialoguePlacements == null) return null;
            for (int i = 0; i < mapAsset.Definition.dialoguePlacements.Count; i++)
                if (mapAsset.Definition.dialoguePlacements[i] != null && mapAsset.Definition.dialoguePlacements[i].placementId == placementId) return mapAsset.Definition.dialoguePlacements[i];
            return null;
        }

        private void LoadGraphFromSelectedPlacement()
        {
            if (mapAsset == null || mapAsset.Definition.dialoguePlacements == null || selectedPlacementIndex < 0 || selectedPlacementIndex >= mapAsset.Definition.dialoguePlacements.Count) return;
            ESWorldDialoguePlacement placement = mapAsset.Definition.dialoguePlacements[selectedPlacementIndex];
            string path = placement == null ? string.Empty : AssetDatabase.GUIDToAssetPath(placement.dialogueGraphAssetGuid);
            ESWorldDialogueGraphAsset asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESWorldDialogueGraphAsset>(path);
            if (asset == null) { SetStatus("无法通过 GUID 加载对话图资产。", MessageType.Error); return; }
            BindGraph(asset);
            SetStatus("已从放置记录加载对话图：" + asset.name, MessageType.Info);
        }

        private void BindCurrentGraphToSelectedPlacement()
        {
            if (graphAsset == null || mapAsset == null || mapAsset.Definition.dialoguePlacements == null || selectedPlacementIndex < 0 || selectedPlacementIndex >= mapAsset.Definition.dialoguePlacements.Count) return;
            mapSerialized?.ApplyModifiedProperties();
            ESWorldDialoguePlacement placement = mapAsset.Definition.dialoguePlacements[selectedPlacementIndex];
            if (placement == null) return;
            Undo.RecordObject(mapAsset, "绑定对话图到空间入口");
            placement.dialogueGraphKey = ESWorldDialogueAuthoringUtility.GetAssetKey(graphAsset);
            placement.dialogueGraphAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graphAsset));
            placement.entryNodeId = graphAsset.Definition.entryNodeId;
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            mapSerialized?.Update();
            SetStatus("已将当前对话图绑定到空间入口。", MessageType.Info);
        }

        private int FindPlacementAt(Vector2 point, Vector2 min, Vector2 max, Rect canvas)
        {
            if (mapAsset == null || mapAsset.Definition.dialoguePlacements == null) return -1;
            for (int i = 0; i < mapAsset.Definition.dialoguePlacements.Count; i++)
            {
                ESWorldDialoguePlacement placement = mapAsset.Definition.dialoguePlacements[i];
                if (placement == null || placement.space != ESWorldDialoguePlacementSpace.Map2D) continue;
                if (Vector2.Distance(point, WorldToCanvas(new Vector2(placement.position.x, placement.position.z), min, max, canvas)) < 14f) return i;
            }
            return -1;
        }

        private static Vector2 WorldToCanvas(Vector2 point, Vector2 min, Vector2 max, Rect canvas)
        {
            return new Vector2(Mathf.Lerp(canvas.xMin, canvas.xMax, Mathf.InverseLerp(min.x, max.x, point.x)), Mathf.Lerp(canvas.yMax, canvas.yMin, Mathf.InverseLerp(min.y, max.y, point.y)));
        }

        private static Vector2 CanvasToWorld(Vector2 point, Vector2 min, Vector2 max, Rect canvas)
        {
            return new Vector2(Mathf.Lerp(min.x, max.x, Mathf.InverseLerp(canvas.xMin, canvas.xMax, point.x)), Mathf.Lerp(min.y, max.y, Mathf.InverseLerp(canvas.yMax, canvas.yMin, point.y)));
        }

        private void CreateGraphAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建 ES 对话图资产", "ESWorldDialogueGraph", "asset", "选择对话图保存位置");
            if (string.IsNullOrWhiteSpace(path)) return;
            ESWorldDialogueGraphAsset asset = CreateInstance<ESWorldDialogueGraphAsset>();
            asset.Definition.EnsureStableIds();
            asset.Definition.nodes.Add(new ESWorldDialogueNodeData
            {
                nodeId = Guid.NewGuid().ToString("N"),
                title = "开场对话",
                speaker = "旁白",
                text = "这是一个可拖入 2D 地图或 3D Scene 的对话入口。",
                graphPosition = new Vector2(80f, 90f),
                outputs = new List<ESWorldDialoguePortData> { new ESWorldDialoguePortData { portId = Guid.NewGuid().ToString("N"), displayName = "继续" } }
            });
            asset.Definition.entryNodeId = asset.Definition.nodes[0].nodeId;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            BindGraph(asset);
            SetStatus("已创建对话图资产。", MessageType.Info);
        }

        private void BindGraph(ESWorldDialogueGraphAsset asset)
        {
            graphAsset = asset;
            graphSerialized = asset == null ? null : new SerializedObject(asset);
            selectedNodeIndex = -1;
            if (asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                SessionState.SetString(GraphGuidSessionKey, string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
                EnsureGraphDefinition();
                SetStatus("已加载对话图：" + asset.name, MessageType.Info);
            }
        }

        private void BindMap(ESWorldMapAsset asset)
        {
            mapAsset = asset;
            mapSerialized = asset == null ? null : new SerializedObject(asset);
            selectedPlacementIndex = -1;
            if (asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                SessionState.SetString(MapGuidSessionKey, string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
                SetStatus("已加载地图：" + asset.name, MessageType.Info);
            }
        }

        private void RestoreAssets()
        {
            string graphGuid = SessionState.GetString(GraphGuidSessionKey, string.Empty);
            if (!string.IsNullOrEmpty(graphGuid)) BindGraph(AssetDatabase.LoadAssetAtPath<ESWorldDialogueGraphAsset>(AssetDatabase.GUIDToAssetPath(graphGuid)));
            string mapGuid = SessionState.GetString(MapGuidSessionKey, string.Empty);
            if (!string.IsNullOrEmpty(mapGuid)) BindMap(AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(AssetDatabase.GUIDToAssetPath(mapGuid)));
        }

        private void SaveAll()
        {
            ESWorldDialogueSaveResult graphResult = graphAsset == null ? new ESWorldDialogueSaveResult(true, false, 0, string.Empty, null) : ESWorldDialogueAuthoringUtility.Save(graphAsset, graphSerialized);
            ESWorldMapSaveResult mapResult = mapAsset == null ? new ESWorldMapSaveResult(true, false, 0, string.Empty, null) : ESWorldMapAuthoringUtility.Save(mapAsset, mapSerialized);
            bool sceneSaved = false;
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && !string.IsNullOrEmpty(active.path)) sceneSaved = EditorSceneManager.SaveScene(active);
            if (!graphResult.success || !mapResult.success)
            {
                SetStatus("保存失败：" + (graphResult.success ? mapResult.error : graphResult.error), MessageType.Error);
                return;
            }
            SetStatus("对话图、地图资产" + (sceneSaved ? "与当前场景" : string.Empty) + "已保存。", MessageType.Info);
        }

        private void ValidateAll()
        {
            if (graphAsset != null && !graphAsset.Validate(out string graphError)) { SetStatus(graphError, MessageType.Error); return; }
            if (mapAsset != null && !mapAsset.Validate(out string mapError)) { SetStatus(mapError, MessageType.Error); return; }
            SetStatus("对话图与地图数据验证通过。", MessageType.Info);
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message ?? string.Empty;
            statusType = type;
            Repaint();
        }
    }
}
#endif
