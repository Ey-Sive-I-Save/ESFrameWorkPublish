#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ES.EditorInternal;

namespace ES
{
    /// <summary>Draft-first UI Toolkit world authoring surface. Formal assets are changed only by CommitDraft.</summary>
    public sealed class ESWorldMapSpaceEditorWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "空间";
        private sealed class HierarchyItem
        {
            public ESWorldAuthoringSelectionKind kind;
            public int index;
            public string title;
        }

        [SerializeField] private string mapGuid;
        [SerializeField] private ESWorldAuthoringTool activeTool;
        [SerializeField] private ESWorldAuthoringSelectionKind selectionKind;
        [SerializeField] private int selectionIndex = -1;
        [SerializeField] private float brushHeight = 0.5f;
        [SerializeField] private bool snapEnabled = true;
        [SerializeField] private float snapStep = 1f;

        private ESWorldEditSession editSession;
        private ESWindowShell shell;
        private ObjectField mapField;
        private ObjectField prefabField;
        private Vector3Field placementRotationField;
        private Vector3Field placementScaleField;
        private Slider brushHeightField;
        private ListView hierarchyList;
        private ScrollView inspectorScroll;
        private ESWorldAuthoringViewport viewport;
        private readonly List<HierarchyItem> hierarchyItems = new List<HierarchyItem>();
        private readonly Dictionary<ESWorldAuthoringTool, ToolbarToggle> toolToggles = new Dictionary<ESWorldAuthoringTool, ToolbarToggle>();
        private IVisualElementScheduledItem pendingViewportRefresh;
        private bool rebuildingInspector;

        [MenuItem("【ES】/内容制作/环境/世界空间编辑器", false, 121)]
        private static void Open()
        {
            ESWorldMapSpaceEditorWindow window = GetWindow<ESWorldMapSpaceEditorWindow>();
            window.titleContent = new GUIContent("ES 世界空间编辑器");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        public static void OpenFor(ESWorldMapAsset asset)
        {
            ESWorldMapSpaceEditorWindow window = GetWindow<ESWorldMapSpaceEditorWindow>();
            window.titleContent = new GUIContent("ES 世界空间编辑器");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
            window.SetMapAsset(asset);
            window.Focus();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(mapGuid)) return;
            string path = AssetDatabase.GUIDToAssetPath(mapGuid);
            ESWorldMapAsset restored = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(path);
            if (restored != null) SetMapAsset(restored);
        }

        private void OnDisable()
        {
            ESWindowFoundation.Unbind(this, true);
            pendingViewportRefresh?.Pause();
            pendingViewportRefresh = null;
            viewport?.Dispose();
            viewport = null;
            editSession?.Dispose();
            editSession = null;
        }

        private void OnFocus()
        {
            CheckExternalDrift();
        }

        private void OnProjectChange()
        {
            CheckExternalDrift();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            shell = new ESWindowShell("ES 世界空间编辑器", "草稿、可视编辑、验证与提交共用一个作者态会话");
            rootVisualElement.Add(shell.Root);
            BuildHeader();
            BuildToolBar();
            BuildWorkspace();
            ESEditorPresentation.BindWindow(this, true, new ESWindowActionHosts(shell.HeaderToolbar, shell.Toolbar, shell.Content));
            ESWorldMapAsset current = editSession?.Source;
            if (current == null && !string.IsNullOrEmpty(mapGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(mapGuid);
                current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(path);
            }
            if (current != null) BindSessionToUi(current);
            else ShowEmptyWorkspace();
        }

        private void BuildHeader()
        {
            shell.HeaderToolbar.Add(ESWindowPresentation.CreateHeaderActionButton(
                EditorGUIUtility.IconContent("d_SaveAs").image, "提交", "验证并提交当前草稿", CommitDraft));
            shell.HeaderToolbar.Add(ESWindowPresentation.CreateHeaderActionButton(
                EditorGUIUtility.IconContent("d_Refresh").image, "回退", "回退到本次编辑会话的基线", RevertDraft));
            shell.HeaderToolbar.Add(ESWindowPresentation.CreateHeaderActionButton(
                EditorGUIUtility.IconContent("d_TestPassed").image, "验证", "验证当前草稿", ValidateDraft));
        }

        private void BuildToolBar()
        {
            mapField = new ObjectField("地图") { objectType = typeof(ESWorldMapAsset), allowSceneObjects = false };
            mapField.style.width = 330f;
            mapField.RegisterValueChangedCallback(evt => SetMapAsset(evt.newValue as ESWorldMapAsset));
            shell.Toolbar.Add(mapField);
            shell.Toolbar.Add(ESWindowPresentation.CreateToolbarButton("新建", "创建新的地图资产", CreateMapAsset));
            shell.Toolbar.Add(ESWindowPresentation.CreateToolbarButton("初始化", "初始化当前草稿的必要世界数据", InitializeDraft));
            shell.Toolbar.Add(ESWindowPresentation.CreateToolbarButton("聚焦全部", "重置 3D 视口相机", () => viewport?.FrameAll()));
        }

        private void BuildWorkspace()
        {
            var outer = new TwoPaneSplitView(0, 228f, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorldOuterSplit" };
            outer.style.flexGrow = 1f;
            outer.Add(BuildLeftPanel());
            var contentSplit = new TwoPaneSplitView(1, 310f, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorldContentSplit" };
            contentSplit.style.flexGrow = 1f;
            contentSplit.Add(BuildViewportPanel());
            contentSplit.Add(BuildInspectorPanel());
            outer.Add(contentSplit);
            shell.Content.Add(outer);
        }

        private VisualElement BuildLeftPanel()
        {
            VisualElement panel = CreatePanel("ESWorldToolsPanel");
            panel.style.minWidth = 180f;
            panel.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            panel.Add(CreatePanelTitle("制作工具"));
            VisualElement toolGrid = new VisualElement { name = "ESWorldToolGrid" };
            toolGrid.style.flexDirection = FlexDirection.Row;
            toolGrid.style.flexWrap = Wrap.Wrap;
            AddToolToggle(toolGrid, ESWorldAuthoringTool.Select, "选择");
            AddToolToggle(toolGrid, ESWorldAuthoringTool.Terrain, "地形");
            AddToolToggle(toolGrid, ESWorldAuthoringTool.Region, "区域");
            AddToolToggle(toolGrid, ESWorldAuthoringTool.Poi, "POI");
            AddToolToggle(toolGrid, ESWorldAuthoringTool.Prefab, "Prefab");
            panel.Add(toolGrid);

            VisualElement settings = new VisualElement { name = "ESWorldToolSettings" };
            settings.style.marginTop = 8f;
            var snapToggle = new Toggle("网格吸附") { value = snapEnabled };
            snapToggle.RegisterValueChangedCallback(evt => snapEnabled = evt.newValue);
            settings.Add(snapToggle);
            var snapField = new FloatField("吸附步长") { value = snapStep };
            snapField.RegisterValueChangedCallback(evt => snapStep = Mathf.Max(0.01f, evt.newValue));
            settings.Add(snapField);
            brushHeightField = new Slider("目标高度", 0f, 1f) { value = brushHeight, showInputField = true };
            brushHeightField.RegisterValueChangedCallback(evt => brushHeight = evt.newValue);
            settings.Add(brushHeightField);
            panel.Add(settings);

            panel.Add(CreatePanelTitle("Prefab 来源"));
            prefabField = new ObjectField("资源") { objectType = typeof(GameObject), allowSceneObjects = false };
            panel.Add(prefabField);
            placementRotationField = new Vector3Field("旋转");
            panel.Add(placementRotationField);
            placementScaleField = new Vector3Field("缩放") { value = Vector3.one };
            panel.Add(placementScaleField);

            panel.Add(CreatePanelTitle("作者层级"));
            hierarchyList = new ListView
            {
                name = "ESWorldHierarchy",
                itemsSource = hierarchyItems,
                fixedItemHeight = 22f,
                selectionType = SelectionType.Single,
                makeItem = () => new Label { style = { paddingLeft = 7f, unityTextAlign = TextAnchor.MiddleLeft } },
                bindItem = (element, index) => ((Label)element).text = hierarchyItems[index].title
            };
            hierarchyList.style.flexGrow = 1f;
            hierarchyList.style.minHeight = 120f;
            hierarchyList.selectionChanged += OnHierarchySelectionChanged;
            panel.Add(hierarchyList);
            return panel;
        }

        private VisualElement BuildViewportPanel()
        {
            VisualElement panel = CreatePanel("ESWorldViewportPanel");
            panel.style.backgroundColor = new Color(0.045f, 0.055f, 0.06f, 1f);
            VisualElement bar = new VisualElement { name = "ESWorldViewportBar" };
            bar.style.height = 30f;
            bar.style.flexShrink = 0f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 9f;
            bar.style.paddingRight = 9f;
            bar.style.backgroundColor = ESEditorPresentation.ToolbarSurfaceColor;
            Label title = new Label("3D 作者视口");
            title.style.flexGrow = 1f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            bar.Add(title);
            Label mode = new Label("DRAFT");
            mode.style.color = ESEditorPresentation.WarningColor;
            mode.style.unityFontStyleAndWeight = FontStyle.Bold;
            bar.Add(mode);
            panel.Add(bar);
            viewport = new ESWorldAuthoringViewport(OnViewportWorldClick);
            panel.Add(viewport);
            return panel;
        }

        private VisualElement BuildInspectorPanel()
        {
            VisualElement panel = CreatePanel("ESWorldInspectorPanel");
            panel.style.minWidth = 240f;
            panel.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            panel.Add(CreatePanelTitle("上下文属性"));
            inspectorScroll = new ScrollView(ScrollViewMode.Vertical) { name = "ESWorldInspectorScroll" };
            inspectorScroll.style.flexGrow = 1f;
            inspectorScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspectorScroll.RegisterCallback<SerializedPropertyChangeEvent>(OnSerializedPropertyChanged);
            panel.Add(inspectorScroll);
            return panel;
        }

        private void AddToolToggle(VisualElement parent, ESWorldAuthoringTool tool, string text)
        {
            var toggle = new ToolbarToggle { text = text, value = activeTool == tool };
            toggle.style.width = 65f;
            toggle.style.marginRight = 3f;
            toggle.style.marginBottom = 3f;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) SetActiveTool(tool);
                else if (activeTool == tool) toggle.SetValueWithoutNotify(true);
            });
            toolToggles[tool] = toggle;
            parent.Add(toggle);
        }

        private void SetActiveTool(ESWorldAuthoringTool tool)
        {
            activeTool = tool;
            foreach (KeyValuePair<ESWorldAuthoringTool, ToolbarToggle> pair in toolToggles)
                pair.Value.SetValueWithoutNotify(pair.Key == tool);
            brushHeightField?.SetEnabled(tool == ESWorldAuthoringTool.Terrain);
            shell?.SetStatus("当前工具：" + ResolveToolName(tool), ESStatusKind.Ready);
        }

        private void SetMapAsset(ESWorldMapAsset asset)
        {
            if (editSession?.Source == asset)
            {
                BindSessionToUi(asset);
                return;
            }
            pendingViewportRefresh?.Pause();
            editSession?.Dispose();
            editSession = ESWorldEditSession.Open(asset);
            if (asset == null)
            {
                mapGuid = string.Empty;
                mapField?.SetValueWithoutNotify(null);
                ShowEmptyWorkspace();
                return;
            }
            string path = AssetDatabase.GetAssetPath(asset);
            mapGuid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            mapField?.SetValueWithoutNotify(asset);
            selectionKind = ESWorldAuthoringSelectionKind.Map;
            selectionIndex = -1;
            BindSessionToUi(asset);
        }

        private void BindSessionToUi(ESWorldMapAsset asset)
        {
            if (asset == null || editSession == null) return;
            mapField?.SetValueWithoutNotify(asset);
            viewport?.Bind(editSession.Draft, true);
            RefreshHierarchy();
            RebuildInspector();
            UpdateSessionStatus("已载入隔离草稿。");
            CheckExternalDrift();
        }

        private void ShowEmptyWorkspace()
        {
            hierarchyItems.Clear();
            hierarchyList?.Rebuild();
            inspectorScroll?.Unbind();
            inspectorScroll?.Clear();
            if (inspectorScroll != null)
                inspectorScroll.Add(ESWindowPresentation.CreateEmptyState("未选择地图", "选择或新建 ESWorldMapAsset。", "新建地图", CreateMapAsset));
            viewport?.Bind(null, true);
            shell?.SetStatus("请选择地图资产。", ESStatusKind.None);
        }

        private void OnViewportWorldClick(Vector3 point)
        {
            if (editSession?.Draft?.Definition == null) return;
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            point.x = Mathf.Clamp(point.x, definition.worldMin.x, definition.worldMax.x);
            point.z = Mathf.Clamp(point.z, definition.worldMin.y, definition.worldMax.y);
            if (snapEnabled)
            {
                float step = Mathf.Max(0.01f, snapStep);
                point.x = Mathf.Round(point.x / step) * step;
                point.z = Mathf.Round(point.z / step) * step;
            }

            switch (activeTool)
            {
                case ESWorldAuthoringTool.Terrain: PaintTerrain(point); break;
                case ESWorldAuthoringTool.Region: AddRegion(point); break;
                case ESWorldAuthoringTool.Poi: AddPoi(point); break;
                case ESWorldAuthoringTool.Prefab: AddPrefabPlacement(point); break;
                default: SelectNearest(point); break;
            }
        }

        private void PaintTerrain(Vector3 point)
        {
            Undo.RecordObject(editSession.Draft, "绘制世界草稿高度");
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (!ESWorldMapTerrainEditorFacade.TryPaintHeight(definition, new Vector2(point.x, point.z),
                    definition.worldMin, definition.worldMax, brushHeight, out string error))
            {
                shell.SetStatus(error, ESStatusKind.Error);
                return;
            }
            DraftChanged("高度场已更新。", false, "definition.heightfield");
        }

        private void AddRegion(Vector3 point)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition.regions == null) definition.regions = new List<ESWorldMapRegionDefinition>();
            Undo.RecordObject(editSession.Draft, "添加世界区域");
            float halfSize = Mathf.Max(2f, definition.chunkSize * 0.5f);
            int index = definition.regions.Count;
            definition.regions.Add(new ESWorldMapRegionDefinition
            {
                regionId = NextId("region", definition.regions.Count, id => definition.regions.Exists(item => item != null && item.regionId == id)),
                displayName = "区域 " + (index + 1),
                semanticTag = "Default",
                min = new Vector2(Mathf.Max(definition.worldMin.x, point.x - halfSize), Mathf.Max(definition.worldMin.y, point.z - halfSize)),
                max = new Vector2(Mathf.Min(definition.worldMax.x, point.x + halfSize), Mathf.Min(definition.worldMax.y, point.z + halfSize)),
                priority = index
            });
            selectionKind = ESWorldAuthoringSelectionKind.Region;
            selectionIndex = index;
            DraftChanged("已添加区域。", true, "definition.regions");
        }

        private void AddPoi(Vector3 point)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition.pois == null) definition.pois = new List<ESWorldMapPoiDefinition>();
            Undo.RecordObject(editSession.Draft, "添加世界 POI");
            int index = definition.pois.Count;
            definition.pois.Add(new ESWorldMapPoiDefinition
            {
                poiId = NextId("poi", definition.pois.Count, id => definition.pois.Exists(item => item != null && item.poiId == id)),
                displayName = "POI " + (index + 1),
                category = "PointOfInterest",
                position = new Vector2(point.x, point.z)
            });
            selectionKind = ESWorldAuthoringSelectionKind.Poi;
            selectionIndex = index;
            DraftChanged("已添加 POI。", true, "definition.pois");
        }

        private void AddPrefabPlacement(Vector3 point)
        {
            GameObject prefab = prefabField?.value as GameObject;
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                shell.SetStatus("请先选择 Project 中的 Prefab 资产。", ESStatusKind.Warning);
                return;
            }
            if (!ESWorkbenchContentRegistration.TryResolveRegisteredAsset(prefab, ESAssetReferKind.Prefab,
                    out ESAssetPage page, out string error))
            {
                shell.SetStatus(error, ESStatusKind.Error);
                return;
            }
            Vector3 scale = placementScaleField?.value ?? Vector3.one;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                shell.SetStatus("Prefab 缩放必须全部大于 0。", ESStatusKind.Warning);
                return;
            }
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition.prefabPlacements == null) definition.prefabPlacements = new List<ESWorldMapPrefabPlacement>();
            Undo.RecordObject(editSession.Draft, "放置世界 Prefab");
            int index = definition.prefabPlacements.Count;
            definition.prefabPlacements.Add(new ESWorldMapPrefabPlacement
            {
                placementId = NextId("placement", index, id => definition.prefabPlacements.Exists(item => item != null && item.placementId == id)),
                prefabKey = page.EffectiveStringKey,
                editorPrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),
                position = point,
                rotationEuler = placementRotationField?.value ?? Vector3.zero,
                scale = scale,
                enabled = true
            });
            selectionKind = ESWorldAuthoringSelectionKind.PrefabPlacement;
            selectionIndex = index;
            DraftChanged("Prefab 已写入作者态放置记录。", true, "definition.prefabPlacements");
        }

        private void SelectNearest(Vector3 point)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            float threshold = Mathf.Max(3f, definition.chunkSize * 0.18f);
            float best = float.MaxValue;
            ESWorldAuthoringSelectionKind bestKind = ESWorldAuthoringSelectionKind.Map;
            int bestIndex = -1;
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition item = definition.pois[i];
                    if (item == null) continue;
                    float distance = Vector2.Distance(item.position, new Vector2(point.x, point.z));
                    if (distance < best && distance <= threshold) { best = distance; bestKind = ESWorldAuthoringSelectionKind.Poi; bestIndex = i; }
                }
            if (definition.prefabPlacements != null)
                for (int i = 0; i < definition.prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement item = definition.prefabPlacements[i];
                    if (item == null) continue;
                    float distance = Vector2.Distance(new Vector2(item.position.x, item.position.z), new Vector2(point.x, point.z));
                    if (distance < best && distance <= threshold) { best = distance; bestKind = ESWorldAuthoringSelectionKind.PrefabPlacement; bestIndex = i; }
                }
            if (bestIndex < 0 && definition.regions != null)
                for (int i = definition.regions.Count - 1; i >= 0; i--)
                    if (definition.regions[i] != null && definition.regions[i].Contains(new Vector2(point.x, point.z)))
                    { bestKind = ESWorldAuthoringSelectionKind.Region; bestIndex = i; break; }
            selectionKind = bestKind;
            selectionIndex = bestIndex;
            RebuildInspector();
            SyncHierarchySelection();
        }

        private void RefreshHierarchy()
        {
            hierarchyItems.Clear();
            if (editSession?.Draft?.Definition == null) { hierarchyList?.Rebuild(); return; }
            hierarchyItems.Add(new HierarchyItem { kind = ESWorldAuthoringSelectionKind.Map, index = -1, title = "世界 · " + editSession.Draft.name.Replace(" (Draft)", string.Empty) });
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition.regions != null)
                for (int i = 0; i < definition.regions.Count; i++)
                    hierarchyItems.Add(new HierarchyItem { kind = ESWorldAuthoringSelectionKind.Region, index = i, title = "区域 · " + (definition.regions[i]?.displayName ?? "缺失") });
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                    hierarchyItems.Add(new HierarchyItem { kind = ESWorldAuthoringSelectionKind.Poi, index = i, title = "POI · " + (definition.pois[i]?.displayName ?? "缺失") });
            if (definition.prefabPlacements != null)
                for (int i = 0; i < definition.prefabPlacements.Count; i++)
                    hierarchyItems.Add(new HierarchyItem { kind = ESWorldAuthoringSelectionKind.PrefabPlacement, index = i, title = "Prefab · " + (definition.prefabPlacements[i]?.placementId ?? "缺失") });
            hierarchyList?.Rebuild();
            SyncHierarchySelection();
        }

        private void OnHierarchySelectionChanged(IEnumerable<object> selection)
        {
            foreach (object selected in selection)
            {
                if (!(selected is HierarchyItem item)) continue;
                selectionKind = item.kind;
                selectionIndex = item.index;
                RebuildInspector();
                return;
            }
        }

        private void SyncHierarchySelection()
        {
            if (hierarchyList == null) return;
            for (int i = 0; i < hierarchyItems.Count; i++)
                if (hierarchyItems[i].kind == selectionKind && hierarchyItems[i].index == selectionIndex)
                { hierarchyList.SetSelection(i); return; }
        }

        private void RebuildInspector()
        {
            if (inspectorScroll == null) return;
            rebuildingInspector = true;
            inspectorScroll.Unbind();
            inspectorScroll.Clear();
            if (editSession?.SerializedDraft == null)
            {
                rebuildingInspector = false;
                return;
            }
            editSession.SerializedDraft.Update();
            SerializedProperty definition = editSession.SerializedDraft.FindProperty("definition");
            SerializedProperty target = ResolveSelectionProperty(definition);
            if (target == null)
            {
                selectionKind = ESWorldAuthoringSelectionKind.Map;
                selectionIndex = -1;
                target = definition;
            }
            BuildPropertyInspector(target);
            inspectorScroll.Bind(editSession.SerializedDraft);
            rebuildingInspector = false;
        }

        private SerializedProperty ResolveSelectionProperty(SerializedProperty definition)
        {
            if (definition == null || selectionKind == ESWorldAuthoringSelectionKind.Map) return definition;
            string collectionName = selectionKind == ESWorldAuthoringSelectionKind.Region ? "regions" :
                selectionKind == ESWorldAuthoringSelectionKind.Poi ? "pois" : "prefabPlacements";
            SerializedProperty collection = definition.FindPropertyRelative(collectionName);
            return collection != null && selectionIndex >= 0 && selectionIndex < collection.arraySize
                ? collection.GetArrayElementAtIndex(selectionIndex)
                : null;
        }

        private void BuildPropertyInspector(SerializedProperty target)
        {
            if (selectionKind == ESWorldAuthoringSelectionKind.Map)
            {
                AddSectionLabel(inspectorScroll, "世界身份与范围");
                AddProperty(target, "mapId", "地图 ID");
                AddProperty(target, "sourceMode", "内容来源");
                AddProperty(target, "terrainMode", "地形后端");
                AddProperty(target, "worldMin", "世界最小点");
                AddProperty(target, "worldMax", "世界最大点");
                AddProperty(target, "chunkSize", "区块尺寸");
                AddProperty(target, "terrainHeightScale", "地形高度");
                AddSectionLabel(inspectorScroll, "空间与地形数据");
                SerializedProperty spaceTemplate = target.FindPropertyRelative("spaceTemplate");
                AddProperty(spaceTemplate, "templateId", "空间模板 ID");
                AddProperty(spaceTemplate, "gridWidth", "网格列数");
                AddProperty(spaceTemplate, "gridHeight", "网格行数");
                AddProperty(spaceTemplate, "cellSize", "单元尺寸");
                SerializedProperty heightfield = target.FindPropertyRelative("heightfield");
                AddProperty(heightfield, "width", "高度采样宽度");
                AddProperty(heightfield, "height", "高度采样高度");
                AddProperty(heightfield, "defaultHeight", "默认高度");
                AddSectionLabel(inspectorScroll, "构建与 UGC 约束");
                AddProperty(target, "build", "构建设置");
                AddProperty(target, "ugcLimits", "UGC 配额");
                return;
            }
            AddSectionLabel(inspectorScroll, ResolveSelectionTitle());
            PropertyField field = new PropertyField(target);
            field.style.minWidth = 0f;
            inspectorScroll.Add(field);
            Button remove = ESWindowPresentation.CreateToolbarButton("删除所选", "从草稿中删除当前作者对象", DeleteSelection);
            remove.style.marginTop = 10f;
            inspectorScroll.Add(remove);
        }

        private void AddProperty(SerializedProperty parent, string relativeName, string label)
        {
            SerializedProperty property = parent?.FindPropertyRelative(relativeName);
            if (property == null) return;
            PropertyField field = new PropertyField(property, label);
            field.style.minWidth = 0f;
            inspectorScroll.Add(field);
        }

        private void DeleteSelection()
        {
            if (editSession?.Draft?.Definition == null || selectionIndex < 0) return;
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            string changePath = ResolveSelectionChangePath();
            Undo.RecordObject(editSession.Draft, "删除世界作者对象");
            if (selectionKind == ESWorldAuthoringSelectionKind.Region && definition.regions != null && selectionIndex < definition.regions.Count)
                definition.regions.RemoveAt(selectionIndex);
            else if (selectionKind == ESWorldAuthoringSelectionKind.Poi && definition.pois != null && selectionIndex < definition.pois.Count)
                definition.pois.RemoveAt(selectionIndex);
            else if (selectionKind == ESWorldAuthoringSelectionKind.PrefabPlacement && definition.prefabPlacements != null && selectionIndex < definition.prefabPlacements.Count)
                definition.prefabPlacements.RemoveAt(selectionIndex);
            selectionKind = ESWorldAuthoringSelectionKind.Map;
            selectionIndex = -1;
            DraftChanged("已从草稿删除所选对象。", true, changePath);
        }

        private void OnSerializedPropertyChanged(SerializedPropertyChangeEvent evt)
        {
            if (rebuildingInspector || editSession == null) return;
            editSession.NotifyDraftChanged(evt.changedProperty?.propertyPath ?? "definition");
            RefreshHierarchy();
            ScheduleViewportRefresh();
            UpdateSessionStatus("草稿属性已更新。");
        }

        private void DraftChanged(string message, bool rebuildInspector = false, string changePath = "definition")
        {
            editSession.NotifyDraftChanged(changePath);
            RefreshHierarchy();
            if (rebuildInspector) RebuildInspector();
            ScheduleViewportRefresh();
            UpdateSessionStatus(message);
        }

        private void ScheduleViewportRefresh()
        {
            pendingViewportRefresh?.Pause();
            pendingViewportRefresh = rootVisualElement.schedule.Execute(() => viewport?.Rebuild()).StartingIn(180);
        }

        private void CommitDraft()
        {
            if (editSession == null) return;
            ESWorldEditCommitResult result = editSession.TryCommit();
            shell.SetStatus(result.message, result.success ? ESStatusKind.Ready : result.conflict ? ESStatusKind.Warning : ESStatusKind.Error);
            if (!result.success) return;
            viewport?.Bind(editSession.Draft, false);
            RefreshHierarchy();
            RebuildInspector();
        }

        private void RevertDraft()
        {
            if (editSession == null || !editSession.IsDirty) return;
            if (!EditorUtility.DisplayDialog("回退世界草稿", "放弃本次会话中的未提交变更并恢复到基线？", "回退", "取消")) return;
            editSession.RevertDraft();
            selectionKind = ESWorldAuthoringSelectionKind.Map;
            selectionIndex = -1;
            viewport?.Bind(editSession.Draft, false);
            RefreshHierarchy();
            RebuildInspector();
            UpdateSessionStatus("草稿已回退到会话基线。");
        }

        private void ValidateDraft()
        {
            if (editSession?.Draft == null) return;
            if (editSession.Draft.Validate(out string error)) shell.SetStatus("草稿验证通过。", ESStatusKind.Ready);
            else shell.SetStatus(error, ESStatusKind.Error);
        }

        private void ReloadFromSource()
        {
            if (editSession == null) return;
            if (editSession.IsDirty && !EditorUtility.DisplayDialog("重新载入正式资产", "当前草稿有未提交变更。重新载入会丢弃这些变更。", "重新载入", "取消")) return;
            editSession.ReloadFromSource();
            viewport?.Bind(editSession.Draft, false);
            RefreshHierarchy();
            RebuildInspector();
            UpdateSessionStatus("已从正式地图建立新基线。");
        }

        private void CheckExternalDrift()
        {
            if (editSession == null || !editSession.RefreshExternalConflict())
            {
                shell?.StatusBar.Q<Button>("ESWorldReloadSource")?.RemoveFromHierarchy();
                return;
            }
            shell.SetStatus("检测到正式地图已在外部变化，当前草稿禁止提交。", ESStatusKind.Warning);
            if (shell.StatusBar.Q<Button>("ESWorldReloadSource") != null) return;
            Button reload = ESWindowPresentation.CreateToolbarButton("检查并重载", "放弃草稿并从正式资产建立新基线", ReloadFromSource);
            reload.name = "ESWorldReloadSource";
            shell.StatusBar.Add(reload);
        }

        private void InitializeDraft()
        {
            if (editSession == null) return;
            Undo.RecordObject(editSession.Draft, "初始化世界草稿");
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            definition.mapId = string.IsNullOrWhiteSpace(definition.mapId) ? editSession.Source.name : definition.mapId;
            definition.contentVersion = Mathf.Max(1, definition.contentVersion);
            definition.contentHash = string.IsNullOrWhiteSpace(definition.contentHash) ? "editor-draft" : definition.contentHash;
            definition.sourceMode = ESWorldMapSourceMode.Procedural;
            definition.generatorKey = string.IsNullOrWhiteSpace(definition.generatorKey) ? "es.world.authoring" : definition.generatorKey;
            definition.generatorVersion = Mathf.Max(1, definition.generatorVersion);
            definition.terrainMode = ESWorldMapTerrainMode.UnityTerrain;
            if (definition.worldMax.x <= definition.worldMin.x || definition.worldMax.y <= definition.worldMin.y)
            {
                definition.worldMin = Vector2.zero;
                definition.worldMax = new Vector2(256f, 256f);
            }
            definition.chunkSize = Mathf.Max(16f, definition.chunkSize);
            definition.terrainHeightScale = Mathf.Max(1f, definition.terrainHeightScale);
            if (definition.heightfield == null) definition.heightfield = new ESWorldMapHeightfield();
            definition.heightfield.width = Mathf.Max(33, definition.heightfield.width);
            definition.heightfield.height = Mathf.Max(33, definition.heightfield.height);
            definition.heightfield.EnsureSamples();
            if (definition.spaceTemplate == null) definition.spaceTemplate = new ESWorldMapSpaceTemplate();
            DraftChanged("草稿已初始化。", true);
            viewport?.FrameAll();
        }

        private void CreateMapAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建 ES 世界地图", "ESWorldMap", "asset", "选择地图资产保存位置");
            if (string.IsNullOrWhiteSpace(path)) return;
            ESWorldMapAsset asset = CreateInstance<ESWorldMapAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssetIfDirty(asset);
            SetMapAsset(asset);
            InitializeDraft();
            Selection.activeObject = asset;
        }

        private void UpdateSessionStatus(string message)
        {
            if (editSession == null) { shell.SetStatus(message, ESStatusKind.None); return; }
            if (editSession.HasExternalConflict) shell.SetStatus("正式地图已外部变化，提交被锁定。", ESStatusKind.Warning);
            else if (editSession.IsDirty) shell.SetStatus(message + " · 未提交 " + editSession.ChangeCount + " 项", ESStatusKind.Warning);
            else shell.SetStatus(message, ESStatusKind.Ready);
        }

        private static VisualElement CreatePanel(string name)
        {
            VisualElement panel = new VisualElement { name = name };
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 0f;
            panel.style.minHeight = 0f;
            panel.style.paddingLeft = 8f;
            panel.style.paddingRight = 8f;
            panel.style.paddingTop = 8f;
            panel.style.paddingBottom = 8f;
            return panel;
        }

        private static Label CreatePanelTitle(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("es-brand-title");
            label.style.marginTop = 5f;
            label.style.marginBottom = 5f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = ESEditorPresentation.SectionSelectedTextColor;
            return label;
        }

        private static void AddSectionLabel(VisualElement parent, string text)
        {
            Label label = CreatePanelTitle(text);
            label.style.marginTop = 10f;
            label.style.borderBottomWidth = 1f;
            label.style.borderBottomColor = ESEditorPresentation.DividerColor;
            label.style.paddingBottom = 4f;
            parent.Add(label);
        }

        private static string NextId(string prefix, int start, Func<string, bool> exists)
        {
            int index = Mathf.Max(1, start + 1);
            string candidate;
            do candidate = prefix + "_" + index++; while (exists(candidate));
            return candidate;
        }

        private string ResolveSelectionTitle()
        {
            switch (selectionKind)
            {
                case ESWorldAuthoringSelectionKind.Region: return "区域属性";
                case ESWorldAuthoringSelectionKind.Poi: return "POI 属性";
                case ESWorldAuthoringSelectionKind.PrefabPlacement: return "Prefab 放置属性";
                default: return "世界属性";
            }
        }

        private string ResolveSelectionChangePath()
        {
            switch (selectionKind)
            {
                case ESWorldAuthoringSelectionKind.Region: return "definition.regions";
                case ESWorldAuthoringSelectionKind.Poi: return "definition.pois";
                case ESWorldAuthoringSelectionKind.PrefabPlacement: return "definition.prefabPlacements";
                default: return "definition";
            }
        }

        private static string ResolveToolName(ESWorldAuthoringTool tool)
        {
            switch (tool)
            {
                case ESWorldAuthoringTool.Terrain: return "地形";
                case ESWorldAuthoringTool.Region: return "区域";
                case ESWorldAuthoringTool.Poi: return "POI";
                case ESWorldAuthoringTool.Prefab: return "Prefab";
                default: return "选择";
            }
        }
    }
}
#endif
