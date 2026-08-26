#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    internal enum ESWorldGamePreviewCameraMode : byte
    {
        Player,
        ThirdPerson,
        Overview
    }

    /// <summary>UI Toolkit-owned surface with a bounded Unity preview renderer for the 3D authoring canvas.</summary>
    internal sealed class ESWorldAuthoringViewport : VisualElement, IDisposable, IESWorkbenchNudgeableViewport,
        IESWorkbenchViewportProjection
    {
        private sealed class RegionGuideVisual
        {
            public string StableId;
            public ESWorldMapRegionDefinition Region;
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Vector3[] Vertices;
            public int XSegments;
            public int ZSegments;
            public Vector2 LastMin = new Vector2(float.NaN, float.NaN);
            public Vector2 LastMax = new Vector2(float.NaN, float.NaN);
            public int TerrainRevision = -1;
            public bool Selected;
            public bool Hovered;
        }

        private readonly IMGUIContainer renderHost;
        private readonly Action<Vector3> worldClick;
        private readonly Action terrainStrokeBegin;
        private readonly Action terrainStrokeEnd;
        private readonly Action terrainStrokeCancel;
        private readonly Func<KeyCode, EventModifiers, bool> terrainBrushShortcut;
        private readonly Func<float> terrainBrushRadius;
        private readonly Func<float> terrainBrushStrength;
        private readonly Func<string> terrainBrushSummary;
        private readonly Action statusChanged;
        private readonly ESWorkbenchViewportContext context;
        private readonly ESWorkbenchPointerInteractionCoordinator pointerCoordinator;
        private readonly object pointerOwnerToken = new object();
        private readonly ESWorkbenchViewportLayoutState layoutState;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly bool readOnlyGameView;
        private readonly ESWorkbenchOrbitCameraState cameraNavigation;
        private readonly ESWorkbenchIMGUIOrbitInput orbitInput;
        private readonly ESWorkbenchEdgePanController edgePan;
        private readonly ESWorkbenchEdgePanSession edgePanSession =
            new ESWorkbenchEdgePanSession();
        private ESEditorPreviewRenderContext preview;
        private ESEditorPreviewResourceScope contentScope;
        private readonly List<GameObject> previewObjects = new List<GameObject>();
        private readonly List<string> previewStableIds = new List<string>();
        private readonly ESWorkbenchRendererBoundsCache previewBounds =
            new ESWorkbenchRendererBoundsCache();
        private readonly ESWorkbenchSelectionCache hitSelectionCache =
            new ESWorkbenchSelectionCache();
        private readonly List<ESEditorPreviewModelHandle> dropPreviewHandles = new List<ESEditorPreviewModelHandle>();
        private readonly List<Vector3> dropPreviewPositions = new List<Vector3>();
        private readonly List<string> dropPreviewObjectIds = new List<string>();
        private readonly List<GameObject> dropPreviewSources = new List<GameObject>();
        private ESWorkbenchDropPreviewState dropPreviewState =
            ESWorkbenchDropPreviewState.Allowed;
        private readonly List<RegionGuideVisual> regionGuideVisuals = new List<RegionGuideVisual>();
        private readonly Vector2[] projectedRegionCorners = new Vector2[4];
        private readonly ESWorkbenchHoverState hover = new ESWorkbenchHoverState();
        private ESWorkbenchObjectDescriptor dropPreviewItem;
        private Vector3 dropPreviewSize = Vector3.one;
        private Vector3 lastDropPreviewAnchor;
        private int lastDropPreviewCount = -1;
        private float lastDropPreviewSpacing;
        private bool lastDropPreviewSnapEnabled;
        private float lastDropPreviewSnapStep;
        private bool lastDropPreviewAnchorValid;
        private Material regionFillMaterial;
        private Material regionOutlineMaterial;
        private Material regionSelectedFillMaterial;
        private Material regionSelectedOutlineMaterial;
        private Material regionHoverFillMaterial;
        private Material regionHoverOutlineMaterial;
        private Material[] regionMaterials;
        private Material[] regionSelectedMaterials;
        private Material[] regionHoverMaterials;
        private int regionGuideTerrainRevision;
        private TerrainData terrainData;
        private GameObject terrainObject;
        private float[,] terrainPreviewHeightBuffer;
        private readonly Vector3[] terrainBrushGuidePoints = new Vector3[49];
        private readonly ESWorkbenchStrokeSampler terrainStrokeSampler = new ESWorkbenchStrokeSampler();
        private ESWorkbenchLatestValueCoalescer<Vector3> terrainPreviewCoalescer;
        private double lastTerrainPreviewHeightUpdate;
        private Vector3 lastTerrainPaintPoint;
        private bool lastTerrainPaintPointValid;
        private ESWorldMapAsset draft;
        private bool moving;
        private bool rotating;
        private bool scaling;
        private bool paintingTerrain;
        private bool pendingTransformValid;
        private IVisualElementScheduledItem edgePanSchedule;
        private int activeControlId;
        private string transformingStableId;
        private ESWorkbenchSelection transformingSelection;
        private readonly List<ESWorkbenchSelection> transformingSelections = new List<ESWorkbenchSelection>();
        private readonly List<Vector3> transformingStartPositions = new List<Vector3>();
        private readonly List<Vector3> transformingStartValues = new List<Vector3>();
        private readonly ESWorkbenchPointerGestureSession gestureSession;
        private readonly ESWorkbenchMoveGestureAnchor moveAnchor;
        private readonly ESWorkbenchTransformGestureSession transformGesture;
        private Vector3 transformStartValue;
        private Vector3 pendingTransformValue;
        private Vector3 lastPointerWorldPosition;
        private bool lastPointerWorldPositionValid;
        private IVisualElementScheduledItem pendingRebuild;
        private IVisualElementScheduledItem terrainPreviewUpdateSchedule;
        private bool pendingResetCamera;
        private ESWorldGamePreviewCameraMode gameCameraMode = ESWorldGamePreviewCameraMode.ThirdPerson;
        private Vector3 gameCameraPosition;
        private Quaternion gameCameraRotation = Quaternion.identity;
        private bool playerCameraNavigationSynchronized;
        private int visiblePlacementCount;
        private int culledPlacementCount;
        private int lodGroupCount;
        private bool disposed;

        internal ESEditorPreviewRenderContext PreviewContextForTest => preview;
        internal int PreviewObjectCountForTest => previewObjects.Count;
        internal int CulledPlacementCountForTest => culledPlacementCount;
        internal int LodGroupCountForTest => lodGroupCount;
        internal int RegionGuideCountForTest => regionGuideVisuals.Count;
        internal bool ContentScopeDisposedForTest => contentScope == null || contentScope.IsDisposed;
        internal string PreviewFidelitySummary => BuildPreviewFidelitySummary();

        internal bool TryGetRegionGuideMeshForTest(string stableId, out Mesh mesh)
        {
            for (int i = 0; i < regionGuideVisuals.Count; i++)
                if (string.Equals(regionGuideVisuals[i].StableId, stableId, StringComparison.Ordinal))
                {
                    mesh = regionGuideVisuals[i].Mesh;
                    return mesh != null;
                }
            mesh = null;
            return false;
        }

        public ESWorldAuthoringViewport(Action<Vector3> worldClick)
            : this(worldClick, null)
        {
        }

        public ESWorldAuthoringViewport(
            Action<Vector3> worldClick,
            ESWorkbenchViewportContext context,
            bool readOnlyGameView = false,
            Action terrainStrokeBegin = null,
            Action terrainStrokeEnd = null,
            Action terrainStrokeCancel = null,
            Func<KeyCode, EventModifiers, bool> terrainBrushShortcut = null,
            Func<float> terrainBrushRadius = null,
            Func<float> terrainBrushStrength = null,
            Func<string> terrainBrushSummary = null,
            Action statusChanged = null,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            name = readOnlyGameView ? "ESWorldGameViewport" : "ESWorldAuthoringViewport";
            this.worldClick = worldClick;
            this.terrainStrokeBegin = terrainStrokeBegin;
            this.terrainStrokeEnd = terrainStrokeEnd;
            this.terrainStrokeCancel = terrainStrokeCancel;
            this.terrainBrushShortcut = terrainBrushShortcut;
            this.terrainBrushRadius = terrainBrushRadius;
            this.terrainBrushStrength = terrainBrushStrength;
            this.terrainBrushSummary = terrainBrushSummary;
            this.statusChanged = statusChanged;
            this.context = context;
            pointerCoordinator = context?.PointerCoordinator
                ?? new ESWorkbenchPointerInteractionCoordinator();
            layoutState = context?.Layout;
            this.feel = feel ?? context?.Feel ?? ESWorkbenchViewportFeelSettings.Standard;
            terrainPreviewCoalescer = new ESWorkbenchLatestValueCoalescer<Vector3>(
                this.feel.PreviewCoalescingDelayMilliseconds);
            this.readOnlyGameView = readOnlyGameView;
            orbitInput = new ESWorkbenchIMGUIOrbitInput(
                this.feel,
                this.feel.VerticalFieldOfViewDegrees,
                pointerCoordinator);
            edgePan = new ESWorkbenchEdgePanController(this.feel.EdgePanSettings);
            gestureSession = new ESWorkbenchPointerGestureSession(
                this.feel.DragStartPixels, this.feel);
            moveAnchor = new ESWorkbenchMoveGestureAnchor();
            transformGesture = new ESWorkbenchTransformGestureSession(this.feel);
            cameraNavigation = new ESWorkbenchOrbitCameraState(
                layoutState,
                Vector3.zero,
                300f,
                35f,
                38f,
                8f,
                82f,
                1f,
                8000f,
                this.feel,
                presentationRadiusScale: this.feel.PresentationRadiusScale);
            if (readOnlyGameView && Enum.TryParse(
                layoutState?.previewCameraMode, true, out ESWorldGamePreviewCameraMode restoredMode))
                gameCameraMode = restoredMode;
            style.flexGrow = 1f;
            style.minWidth = 0f;
            style.minHeight = 240f;
            renderHost = new IMGUIContainer(DrawPreview) { name = "ESWorldAuthoringRenderHost" };
            renderHost.style.flexGrow = 1f;
            renderHost.style.minWidth = 0f;
            renderHost.style.minHeight = 240f;
            renderHost.tooltip = readOnlyGameView
                ? "只读游戏构图预览；使用右键、中键和滚轮检查运行时相机构图。"
                : "左键选择和变换，右键旋转视角，中键平移，滚轮缩放，Esc 取消变换。";
            edgePanSchedule = schedule.Execute(ApplyEdgePan).Every(16);
            edgePanSchedule.Pause();
            renderHost.RegisterCallback<PointerLeaveEvent>(_ => ClearPointerWorldPosition());
            renderHost.RegisterCallback<FocusOutEvent>(_ => CompleteInterruptedInteraction());
            Add(renderHost);
        }

        public void Bind(ESWorldMapAsset nextDraft, bool resetCamera)
        {
            bool assetChanged = draft != nextDraft;
            hitSelectionCache.Clear();
            draft = nextDraft;
            draft?.EnsureAuthoringContainers();
            if (paintingTerrain && !assetChanged && !resetCamera)
            {
                renderHost.MarkDirtyRepaint();
                return;
            }
            if (assetChanged || resetCamera || preview == null) Rebuild(resetCamera || assetChanged);
            else RequestRebuild(false);
        }

        /// <summary>
        /// Refreshes selection-dependent projection visuals without rebuilding the
        /// PreviewScene. SelectionChanged is a projection update, not an asset rebuild.
        /// </summary>
        public void RefreshSelection()
        {
            if (disposed) return;
            renderHost?.MarkDirtyRepaint();
        }

        private void RequestRebuild(bool resetCamera)
        {
            if (disposed) return;
            pendingResetCamera |= resetCamera;
            pendingRebuild?.Pause();
            pendingRebuild = schedule.Execute(() =>
            {
                bool shouldReset = pendingResetCamera;
                pendingResetCamera = false;
                pendingRebuild = null;
                Rebuild(shouldReset);
            }).StartingIn(80);
        }

        public void Rebuild(bool resetCamera = false)
        {
            if (disposed) return;
            pendingRebuild?.Pause();
            pendingRebuild = null;
            pendingResetCamera = false;
            ClearPreviewContent();
            if (draft == null || draft.Definition == null)
            {
                renderHost.MarkDirtyRepaint();
                return;
            }

            EnsurePreview();
            contentScope = new ESEditorPreviewResourceScope(
                "ES World Authoring Viewport",
                readOnlyGameView ? "游戏构图预览内容" : "世界作者内容");

            ESWorldMapDefinition definition = draft.Definition;
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = new Vector2(256f, 256f); }
            if (resetCamera)
            {
                cameraNavigation.SetView(
                    new Vector3((min.x + max.x) * 0.5f, definition.terrainHeightScale * 0.2f,
                        (min.y + max.y) * 0.5f),
                    Mathf.Max(80f, Mathf.Max(max.x - min.x, max.y - min.y) * 1.15f),
                    35f,
                    25f);
                if (readOnlyGameView) ApplyGameCameraDefaults(definition, min, max);
            }
            preview.ConfigureGroundPlane(
                new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f),
                Mathf.Max(max.x - min.x, max.y - min.y) * 1.04f);
            ConfigureGamePreviewCamera(definition);

            visiblePlacementCount = 0;
            culledPlacementCount = 0;
            lodGroupCount = 0;
            CreateTerrain(definition);
            CreateRegionGuides(definition);
            CreatePlacements(definition);
            renderHost.MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null)
                return;
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            if (max.x <= min.x || max.y <= min.y)
            {
                min = Vector2.zero;
                max = new Vector2(256f, 256f);
            }
            if (readOnlyGameView)
                ApplyGameCameraDefaults(definition, min, max);
            else
                cameraNavigation.ResetRecommended(
                    new Vector3((min.x + max.x) * 0.5f, definition.terrainHeightScale * 0.2f,
                        (min.y + max.y) * 0.5f),
                    Mathf.Max(80f, Mathf.Max(max.x - min.x, max.y - min.y) * 1.15f),
                    35f,
                    25f);
            renderHost.MarkDirtyRepaint();
        }

        private void CreateTerrain(ESWorldMapDefinition definition)
        {
            ESWorldMapHeightfield field = definition.heightfield;
            if (field == null || field.width < 2 || field.height < 2) return;
            int resolution = ResolveTerrainResolution(Mathf.Max(field.width, field.height));
            terrainData = contentScope.RegisterObject(new TerrainData
            {
                hideFlags = HideFlags.HideAndDontSave,
                heightmapResolution = resolution,
                size = new Vector3(
                    Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x),
                    Mathf.Max(1f, definition.terrainHeightScale),
                    Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y))
            });
            terrainPreviewHeightBuffer = new float[resolution, resolution];
            FillTerrainPreviewHeightBuffer(field, terrainPreviewHeightBuffer, 0, 0, resolution);
            terrainData.SetHeights(0, 0, terrainPreviewHeightBuffer);
            GameObject createdTerrain = Terrain.CreateTerrainGameObject(terrainData);
            createdTerrain.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
            terrainObject = contentScope.RegisterGameObject(createdTerrain);
            terrainObject.name = "ES World Draft Terrain";
            preview.PreparePreviewObject(terrainObject, "World draft terrain.", samplingTarget: false);
            terrainObject.transform.position = preview.GroupOrigin
                + new Vector3(definition.worldMin.x, 0f, definition.worldMin.y);
            TrackPreviewObject(terrainObject, null);
        }

        private void CreatePlacements(ESWorldMapDefinition definition)
        {
            if (definition.prefabPlacements == null) return;
            for (int i = 0; i < definition.prefabPlacements.Count; i++)
            {
                ESWorldMapPrefabPlacement record = definition.prefabPlacements[i];
                if (record == null || !record.enabled || string.IsNullOrWhiteSpace(record.editorPrefabGuid)
                    || (context != null && !context.IsHierarchyVisible("world.prefab." + record.placementId))) continue;
                if (!ShouldIncludeGamePlacement(definition, record))
                {
                    culledPlacementCount++;
                    continue;
                }
                string path = AssetDatabase.GUIDToAssetPath(record.editorPrefabGuid);
                GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                ESEditorPreviewModelHandle handle = preview.CreateModelGroup(
                    prefab, record.placementId, samplingTarget: false);
                GameObject instance = handle?.Instance;
                if (instance == null) continue;
                instance.transform.SetPositionAndRotation(
                    preview.GroupOrigin + record.position,
                    Quaternion.Euler(record.rotationEuler));
                instance.transform.localScale = record.scale;
                TrackPreviewObject(instance, "world.prefab." + record.placementId);
                visiblePlacementCount++;
                lodGroupCount += instance.GetComponentsInChildren<LODGroup>(true).Length;
            }
        }

        private void TrackPreviewObject(GameObject instance, string stableId)
        {
            previewObjects.Add(instance);
            previewStableIds.Add(stableId ?? string.Empty);
        }

        private void CreateRegionGuides(ESWorldMapDefinition definition)
        {
            if (readOnlyGameView || definition?.regions == null || contentScope == null) return;
            if (!CreateRegionGuideMaterials()) return;

            for (int i = 0; i < definition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                string stableId = region == null ? string.Empty : "world.region." + region.regionId;
                if (region == null || (context != null && !context.IsHierarchyVisible(stableId))) continue;

                Vector2 size = Vector2.Max(Vector2.one * 0.1f, region.max - region.min);
                int xSegments = ResolveRegionGuideSegments(size.x, definition.chunkSize);
                int zSegments = ResolveRegionGuideSegments(size.y, definition.chunkSize);
                Mesh mesh = new Mesh
                {
                    name = "ES World 区域贴地网格 · " + region.regionId,
                    hideFlags = ESEditorPreviewUtility.PreviewHideFlags
                };
                mesh = contentScope.RegisterObject(mesh);
                mesh.MarkDynamic();
                var root = new GameObject(
                    "ES World 区域预览 · " + region.regionId,
                    typeof(MeshFilter),
                    typeof(MeshRenderer));
                root.hideFlags = ESEditorPreviewUtility.PreviewHideFlags;
                root = contentScope.RegisterGameObject(root);
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = regionMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                preview.PreparePreviewObject(root, "World region terrain-conforming guide.", false);
                root.transform.position = preview.GroupOrigin;

                var visual = new RegionGuideVisual
                {
                    StableId = stableId,
                    Region = region,
                    Root = root,
                    Mesh = mesh,
                    Renderer = renderer,
                    Vertices = new Vector3[(xSegments + 1) * (zSegments + 1)],
                    XSegments = xSegments,
                    ZSegments = zSegments
                };
                BuildRegionGuideTopology(visual);
                regionGuideVisuals.Add(visual);
            }
            UpdateRegionGuideVisuals();
        }

        private bool CreateRegionGuideMaterials()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) return false;

            Color authoring = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Normal);
            Color hover = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Hover);
            Color selected = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Selected);
            regionFillMaterial = CreateRegionGuideMaterial(
                shader, "ES World 区域填充", ESWorkbenchViewportRenderStyle.WithAlpha(authoring, 0.10f), 3000);
            regionOutlineMaterial = CreateRegionGuideMaterial(
                shader, "ES World 区域边界", ESWorkbenchViewportRenderStyle.WithAlpha(authoring, 0.90f), 3010);
            regionSelectedFillMaterial = CreateRegionGuideMaterial(
                shader, "ES World 选中区域填充", ESWorkbenchViewportRenderStyle.WithAlpha(selected, 0.24f), 3020);
            regionSelectedOutlineMaterial = CreateRegionGuideMaterial(
                shader, "ES World 选中区域边界", selected, 3030);
            regionHoverFillMaterial = CreateRegionGuideMaterial(
                shader, "ES World 悬停区域填充", ESWorkbenchViewportRenderStyle.WithAlpha(hover, 0.17f), 3020);
            regionHoverOutlineMaterial = CreateRegionGuideMaterial(
                shader, "ES World 悬停区域边界", ESWorkbenchViewportRenderStyle.WithAlpha(hover, 0.82f), 3030);
            regionMaterials = new[] { regionFillMaterial, regionOutlineMaterial };
            regionSelectedMaterials = new[] { regionSelectedFillMaterial, regionSelectedOutlineMaterial };
            regionHoverMaterials = new[] { regionHoverFillMaterial, regionHoverOutlineMaterial };
            return true;
        }

        private Material CreateRegionGuideMaterial(Shader shader, string materialName, Color color, int renderQueue)
        {
            Material material = contentScope.RegisterObject(new Material(shader)
            {
                name = materialName,
                hideFlags = ESEditorPreviewUtility.PreviewHideFlags,
                renderQueue = renderQueue
            });
            ESEditorPreviewUtility.ConfigureDoubleSidedTransparent(material, color);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            return material;
        }

        private static int ResolveRegionGuideSegments(float size, float chunkSize)
        {
            float targetSpacing = Mathf.Max(2f, Mathf.Max(1f, chunkSize) * 0.125f);
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0.1f, size) / targetSpacing), 1, 24);
        }

        private static void BuildRegionGuideTopology(RegionGuideVisual visual)
        {
            int triangleIndex = 0;
            int[] triangles = new int[visual.XSegments * visual.ZSegments * 6];
            int row = visual.XSegments + 1;
            for (int z = 0; z < visual.ZSegments; z++)
                for (int x = 0; x < visual.XSegments; x++)
                {
                    int bottomLeft = z * row + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + row;
                    int topRight = topLeft + 1;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                }

            int[] outline = new int[(visual.XSegments + visual.ZSegments) * 4];
            int outlineIndex = 0;
            for (int x = 0; x < visual.XSegments; x++)
            {
                outline[outlineIndex++] = x;
                outline[outlineIndex++] = x + 1;
                int top = visual.ZSegments * row + x;
                outline[outlineIndex++] = top;
                outline[outlineIndex++] = top + 1;
            }
            for (int z = 0; z < visual.ZSegments; z++)
            {
                int left = z * row;
                outline[outlineIndex++] = left;
                outline[outlineIndex++] = left + row;
                int right = left + visual.XSegments;
                outline[outlineIndex++] = right;
                outline[outlineIndex++] = right + row;
            }

            visual.Mesh.subMeshCount = 2;
            // 先提供与索引一致的顶点槽位，再提交两个子网格，避免 Unity 在空网格上拒绝索引。
            visual.Mesh.vertices = visual.Vertices;
            visual.Mesh.SetIndices(triangles, MeshTopology.Triangles, 0, false);
            visual.Mesh.SetIndices(outline, MeshTopology.Lines, 1, false);
        }

        private void UpdateRegionGuideVisuals()
        {
            string selectedStableId = context?.Selection.Current?.StableId ?? string.Empty;
            for (int i = 0; i < regionGuideVisuals.Count; i++)
            {
                RegionGuideVisual visual = regionGuideVisuals[i];
                ESWorldMapRegionDefinition region = visual.Region;
                if (region == null || visual.Mesh == null || visual.Renderer == null) continue;

                Vector2 originalCenter = (region.min + region.max) * 0.5f;
                Vector2 visualCenter = originalCenter;
                if (moving && pendingTransformValid
                    && string.Equals(transformingStableId, visual.StableId, StringComparison.Ordinal))
                    visualCenter = new Vector2(pendingTransformValue.x, pendingTransformValue.z);
                Vector2 offset = visualCenter - originalCenter;
                Vector2 min = region.min + offset;
                Vector2 max = region.max + offset;
                if (visual.TerrainRevision != regionGuideTerrainRevision
                    || !Approximately(visual.LastMin, min)
                    || !Approximately(visual.LastMax, max))
                    UpdateRegionGuideGeometry(visual, min, max);

                bool selected = string.Equals(selectedStableId, visual.StableId, StringComparison.Ordinal);
                bool hovered = !selected && hover.IsHovered(visual.StableId);
                if (visual.Selected != selected || visual.Hovered != hovered)
                {
                    visual.Selected = selected;
                    visual.Hovered = hovered;
                    visual.Renderer.sharedMaterials = selected ? regionSelectedMaterials
                        : hovered ? regionHoverMaterials : regionMaterials;
                }
            }
        }

        private void UpdateRegionGuideGeometry(RegionGuideVisual visual, Vector2 min, Vector2 max)
        {
            const float lift = 0.08f;
            int index = 0;
            for (int z = 0; z <= visual.ZSegments; z++)
            {
                float v = z / (float)visual.ZSegments;
                float worldZ = Mathf.Lerp(min.y, max.y, v);
                for (int x = 0; x <= visual.XSegments; x++)
                {
                    float u = x / (float)visual.XSegments;
                    float worldX = Mathf.Lerp(min.x, max.x, u);
                    Vector2 world2D = new Vector2(worldX, worldZ);
                    visual.Vertices[index++] = new Vector3(
                        worldX,
                        SampleWorldHeight(world2D) + lift,
                        worldZ);
                }
            }
            visual.Mesh.vertices = visual.Vertices;
            visual.Mesh.RecalculateBounds();
            visual.LastMin = min;
            visual.LastMax = max;
            visual.TerrainRevision = regionGuideTerrainRevision;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) <= 0.0001f
                && Mathf.Abs(left.y - right.y) <= 0.0001f;
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (draft?.Definition != null && preview?.IsReady != true && pendingRebuild == null)
                RequestRebuild(false);
            if (preview?.IsReady != true || draft?.Definition == null)
            {
                ESWorkbenchViewportRenderStyle.DrawGuiBackdrop(rect);
                GUI.Label(rect, "选择地图资产以启动 3D 作者视口", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            ESWorkbenchViewportRenderStyle.DrawGuiBackdrop(rect);
            UpdateRegionGuideVisuals();
            if (readOnlyGameView && gameCameraMode == ESWorldGamePreviewCameraMode.Player)
            {
                SynchronizePlayerCameraNavigation(rect);
                ApplyPreviewCamera(rect);
                preview.RenderCurrentCameraGUI(rect, ESEditorPreviewRenderOptions.Balanced);
            }
            else
            {
                preview.RenderGUI(
                    rect,
                    new ESEditorPreviewCameraPose(
                        preview.GroupOrigin + cameraNavigation.Focus,
                        1f,
                        cameraNavigation.Yaw,
                        cameraNavigation.Pitch,
                        cameraNavigation.ResolvePresentationRadius()),
                    ESEditorPreviewRenderOptions.Balanced);
            }
            DrawOverlay(rect);
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleInput(rect, controlId);
            DrawAuthoringGuides();
            DrawTerrainBrushGuide(rect);
            DrawSelectionOutline();
            DrawHoverOutline();
            DrawTransformTargetOutline();
            DrawDropPreviewTargets();
        }

        private void HandleInput(Rect rect, int controlId)
        {
            Event evt = Event.current;
            bool externalContentDragActive =
                ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || pointerCoordinator.IsExternalContentActive;
            if (externalContentDragActive)
            {
                // 外部资源拖放优先级高于所有本地作者手势；在 PointerDown 这一
                // 个接管帧就清理旧状态，避免等不到 MouseMove 而留下半笔预览。
                if (orbitInput.IsCapturing || moving || rotating || scaling || paintingTerrain)
                    CancelInteraction();
                ClearHover();
                return;
            }
            bool transforming = moving || rotating || scaling;
            if ((transforming || paintingTerrain)
                && !pointerCoordinator.Owns(
                    pointerOwnerToken, 0, ESWorkbenchPointerOwnerKind.Viewport))
            {
                // coordinator 已不再认可本视口时，不能继续消费 MouseDrag/MouseUp。
                // 这通常意味着页面/窗口生命周期已经改变；显式取消可回滚
                // 旧会话的领域事务，避免旧页面把临时数据写入新宿主。
                CancelInteraction();
                return;
            }
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (evt.type == EventType.MouseMove)
            {
                UpdatePointerWorldStatus(rect, interactionRect, evt.mousePosition);
                UpdateHover(rect, interactionRect, evt.mousePosition, transforming);
            }
            if ((evt.type == EventType.MouseLeaveWindow || !interactionRect.Contains(evt.mousePosition))
                && !orbitInput.IsCapturing && !transforming && !paintingTerrain)
                ClearHover();
            if (!interactionRect.Contains(evt.mousePosition) && !orbitInput.IsCapturing
                && !transforming && !paintingTerrain) return;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape
                && (transforming || paintingTerrain))
            {
                CancelInteraction();
                evt.Use();
                return;
            }
            if (evt.type == EventType.KeyDown && IsTerrainPaintingInteraction()
                && terrainBrushShortcut?.Invoke(evt.keyCode, evt.modifiers) == true)
            {
                renderHost.MarkDirtyRepaint();
                evt.Use();
                return;
            }
            if (evt.type == EventType.Ignore && (orbitInput.IsCapturing || transforming || paintingTerrain))
            {
                CompleteInterruptedInteraction();
                return;
            }
            // IMGUI 没有 PointerCaptureOut；焦点切换导致 hotControl 被抢占时，
            // 仍按捕获丢失处理，回滚临时变换并结束笔刷/导航状态。
            if (evt.type != EventType.MouseDown
                && evt.type != EventType.MouseUp
                && evt.type != EventType.Ignore
                && activeControlId != 0
                && GUIUtility.hotControl != activeControlId
                && (orbitInput.IsCapturing || transforming || paintingTerrain))
            {
                CompleteInterruptedInteraction();
                return;
            }
            ESWorkbenchOrbitInputResult cameraResult = ESWorkbenchInteractionPolicy.ShouldHandleNavigation(
                    ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                        || pointerCoordinator.IsExternalContentActive,
                    gestureSession.IsActive)
                ? orbitInput.Handle(interactionRect, rect, cameraNavigation, controlId)
                : ESWorkbenchOrbitInputResult.None;
            if (cameraResult != ESWorkbenchOrbitInputResult.None)
            {
                UpdatePointerWorldStatus(rect, interactionRect, evt.mousePosition);
                ClearHover();
                if (cameraResult == ESWorkbenchOrbitInputResult.Orbit
                    || cameraResult == ESWorkbenchOrbitInputResult.Pan
                    || cameraResult == ESWorkbenchOrbitInputResult.Zoom)
                    renderHost.MarkDirtyRepaint();
                return;
            }
            if (evt.type == EventType.MouseDown)
            {
                ClearHover();
                if (gestureSession.IsActive)
                {
                    evt.Use();
                    return;
                }
                if (evt.button == 0)
                {
                    if (readOnlyGameView)
                    {
                        evt.Use();
                        return;
                    }
                    ESWorkbenchToolCapabilities toolCapabilities = context.Actions.Tools.ActiveCapabilities;
                    string stableId;
                    GameObject instance;
                    ESWorkbenchSelection guideSelection = null;
                    Vector3 guidePosition = default;
                    bool hitPlacement = TryHitPlacement(
                        rect, evt.mousePosition, out stableId, out instance);
                    bool hitGuide = !hitPlacement && TryHitAuthoringGuide(
                        rect, evt.mousePosition, out guideSelection, out guidePosition);
                    bool hasHitTarget = hitPlacement || hitGuide;
                    ESWorkbenchSelection hitSelection = hitPlacement
                        ? CreatePlacementSelection(stableId)
                        : guideSelection;
                    ESWorkbenchToolCapabilities targetCapabilities = hasHitTarget
                        ? ESWorkbenchToolCapabilityResolver.ResolveTarget(
                            context.Actions.Authoring.CanMove(hitSelection),
                            context.Actions.Authoring.CanRotate(hitSelection),
                            context.Actions.Authoring.CanScale(hitSelection))
                        : ESWorkbenchToolCapabilities.Select;
                    ESWorkbenchPointerIntentDecision intentDecision = ESWorkbenchPointerIntentResolver.ResolveDecision(
                        new ESWorkbenchPointerIntentContext(
                        externalContentDragActive: externalContentDragActive,
                        navigationGestureActive: gestureSession.IsActive,
                        toolCapabilities: toolCapabilities,
                        viewportCapabilities: ESWorkbenchToolCapabilities.Select
                            | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Rotate
                            | ESWorkbenchToolCapabilities.Scale
                            | ESWorkbenchToolCapabilities.Paint
                            | ESWorkbenchToolCapabilities.GroundAction,
                        targetCapabilities: targetCapabilities,
                        hasHitTarget: hasHitTarget,
                        hierarchyLocked: hasHitTarget && context.IsHierarchyLocked(
                            hitPlacement ? stableId : guideSelection?.StableId),
                        hitKind: ResolvePointerHitKind(hitPlacement, guideSelection)));
                    if (!intentDecision.CanStart)
                    {
                        evt.Use();
                        return;
                    }
                    ESWorkbenchPointerIntentKind intent = intentDecision.Intent;
                    bool additiveSelection = evt.shift;
                    bool toggleSelection = evt.control || evt.command;
                    if ((additiveSelection || toggleSelection)
                        && intent == ESWorkbenchPointerIntentKind.Manipulate)
                        intent = ESWorkbenchPointerIntentKind.Select;
                    if (hitPlacement && intent == ESWorkbenchPointerIntentKind.Manipulate)
                    {
                        string placementId = stableId.Substring("world.prefab.".Length);
                        ESWorkbenchSelection hit = new ESWorkbenchSelection(stableId, "world.prefab", null, placementId);
                        bool preserveExistingSet = !additiveSelection
                            && context.Selection.CurrentSet.Count > 1
                            && context.Selection.CurrentSet.Any(value => value != null
                                && value.StableId == hit.StableId && value.Kind == hit.Kind);
                        context.Selection.Select(hit, additiveSelection || preserveExistingSet, toggleSelection);
                        BeginTransform(stableId, instance, rect, evt.mousePosition);
                    }
                    else if (hitPlacement && intent == ESWorkbenchPointerIntentKind.Select)
                    {
                        string placementId = stableId.Substring("world.prefab.".Length);
                        ESWorkbenchSelection hit = new ESWorkbenchSelection(stableId, "world.prefab", null, placementId);
                        bool preserveExistingSet = !additiveSelection
                            && context.Selection.CurrentSet.Count > 1
                            && context.Selection.CurrentSet.Any(value => value != null
                                && value.StableId == hit.StableId && value.Kind == hit.Kind);
                        context.Selection.Select(hit, additiveSelection || preserveExistingSet, toggleSelection);
                    }
                    else if (hitGuide && (intent == ESWorkbenchPointerIntentKind.Manipulate
                        || intent == ESWorkbenchPointerIntentKind.Select))
                    {
                        bool preserveExistingSet = !additiveSelection
                            && context.Selection.CurrentSet.Count > 1
                            && context.Selection.CurrentSet.Any(value => value != null
                                && value.StableId == guideSelection.StableId && value.Kind == guideSelection.Kind);
                        context.Selection.Select(
                            guideSelection,
                            additiveSelection || preserveExistingSet,
                            toggleSelection);
                        if (intent == ESWorkbenchPointerIntentKind.Manipulate)
                            BeginGuideTransform(guideSelection, guidePosition, rect, evt.mousePosition);
                    }
                    else if (intent == ESWorkbenchPointerIntentKind.Select)
                    {
                        // 空白点击是明确的取消选择操作；移动工具也不能把旧对象留在 Inspector 中。
                        context.Selection.Clear();
                    }
                    else if (intent == ESWorkbenchPointerIntentKind.Paint
                        && TryResolveTerrainPaintPoint(rect, evt.mousePosition, out Vector3 terrainPoint))
                    {
                        if (!pointerCoordinator.TryAcquire(
                                pointerOwnerToken,
                                0,
                                ESWorkbenchPointerOwnerKind.Viewport))
                        {
                            evt.Use();
                            return;
                        }
                        if (!gestureSession.TryArm(
                                ESWorkbenchPointerGestureSession.Kind.Paint, 0, evt.mousePosition))
                        {
                            pointerCoordinator.Release(
                                pointerOwnerToken,
                                0,
                                ESWorkbenchPointerOwnerKind.Viewport);
                            evt.Use();
                            return;
                        }
                        terrainStrokeBegin?.Invoke();
                        paintingTerrain = true;
                        terrainStrokeSampler.Reset();
                        SampleTerrainStroke(terrainPoint);
                        QueueTerrainPreview(terrainPoint);
                    }
                    else if (intent == ESWorkbenchPointerIntentKind.GroundAction
                        && ShouldInvokeWorldClickOnGround()
                        && TryResolveWorldPoint(rect, evt.mousePosition, out Vector3 point))
                    {
                        worldClick?.Invoke(point);
                    }
                    evt.Use();
                }
                if (moving || rotating || scaling || paintingTerrain)
                {
                    activeControlId = controlId;
                    GUIUtility.hotControl = controlId;
                }
            }
            if (evt.type == EventType.MouseDrag && transforming)
            {
                UpdateTransformPreview(rect, evt.mousePosition, evt.shift);
                BeginEdgePan(evt.mousePosition, evt.shift);
                evt.Use();
                renderHost.MarkDirtyRepaint();
            }
            else if (evt.type == EventType.MouseDrag && paintingTerrain)
            {
                if (TryResolveTerrainPaintPoint(rect, evt.mousePosition, out Vector3 terrainPoint))
                {
                    SampleTerrainStroke(terrainPoint);
                }
                evt.Use();
                renderHost.MarkDirtyRepaint();
            }
            if (evt.type == EventType.MouseUp && transforming)
            {
                UpdateTransformPreview(rect, evt.mousePosition, evt.shift, true);
                CommitTransform();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && paintingTerrain)
            {
                if (TryResolveTerrainPaintPoint(rect, evt.mousePosition, out Vector3 terrainPoint))
                    SampleTerrainStroke(terrainPoint);
                FlushTerrainStroke();
                if (lastTerrainPaintPointValid) FlushTerrainPreview(lastTerrainPaintPoint);
                paintingTerrain = false;
                lastTerrainPaintPointValid = false;
                terrainStrokeEnd?.Invoke();
                gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit);
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                ReleaseMouseControl();
                evt.Use();
            }
            if (evt.type == EventType.MouseMove && rect.Contains(evt.mousePosition)
                && IsTerrainPaintingInteraction())
                renderHost.MarkDirtyRepaint();
        }

        private static ESWorkbenchSelection CreatePlacementSelection(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)
                || !stableId.StartsWith("world.prefab.", StringComparison.Ordinal))
                return ESWorkbenchSelection.Empty;
            return new ESWorkbenchSelection(
                stableId,
                "world.prefab",
                null,
                stableId.Substring("world.prefab.".Length));
        }

        private static ESWorkbenchPointerHitKind ResolvePointerHitKind(
            bool hitPlacement,
            ESWorkbenchSelection guideSelection)
        {
            if (hitPlacement) return ESWorkbenchPointerHitKind.PreciseTarget;
            return ESWorkbenchSpatialHitResolver.ResolveHitKind(
                guideSelection,
                IsWorldContainerSelection);
        }

        private static bool IsWorldContainerSelection(ESWorkbenchSelection selection)
        {
            return selection != null
                && string.Equals(selection.Kind, "world.region", StringComparison.Ordinal);
        }

        private bool IsTerrainPaintingInteraction()
        {
            return !readOnlyGameView
                && ESWorkbenchToolCapabilityResolver.Has(
                    context?.Actions.Tools.ActiveCapabilities ?? ESWorkbenchToolCapabilities.None,
                    ESWorkbenchToolCapabilities.Paint);
        }

        private void UpdateHover(Rect rect, Rect interactionRect, Vector2 mousePosition, bool transforming)
        {
            // 笔刷工具未按下时仍显示精确目标悬停；只有实际绘制、变换或相机捕获
            // 才清除悬停，保证点击前的反馈与 PointerIntent 仲裁一致。
            ESWorkbenchToolCapabilities activeCapabilities =
                context?.Actions.Tools.ActiveCapabilities ?? ESWorkbenchToolCapabilities.None;
            if (!ESWorkbenchInteractionPolicy.ShouldShowPreciseHover(
                    readOnlyGameView,
                    transforming,
                    paintingTerrain,
                    orbitInput.IsCapturing,
                    activeCapabilities,
                    interactionRect.Contains(mousePosition)))
            {
                ClearHover();
                return;
            }
            string stableId = string.Empty;
            if (TryHitPlacement(rect, mousePosition, out string placementId, out _))
                stableId = placementId;
            else if (TryHitAuthoringGuide(rect, mousePosition, out ESWorkbenchSelection guide, out _))
                stableId = guide?.StableId ?? string.Empty;
            if (hover.Update(stableId))
            {
                UpdateRegionGuideVisuals();
                renderHost.MarkDirtyRepaint();
            }
        }

        private void ClearHover()
        {
            if (!hover.Clear()) return;
            UpdateRegionGuideVisuals();
            if (!disposed) renderHost.MarkDirtyRepaint();
        }

        private void SampleTerrainStroke(Vector3 point)
        {
            float spacing = feel.ResolveStrokeSpacing(
                terrainBrushRadius?.Invoke() ?? 8f);
            terrainStrokeSampler.Sample(
                point, spacing, EmitTerrainSample, feel.MaximumStrokeSamplesPerEvent);
        }

        private void FlushTerrainStroke()
        {
            terrainStrokeSampler.Flush(EmitTerrainSample);
        }

        private void EmitTerrainSample(Vector3 point)
        {
            lastTerrainPaintPoint = point;
            lastTerrainPaintPointValid = true;
            worldClick?.Invoke(point);
            QueueTerrainPreview(point);
        }

        private bool ShouldInvokeWorldClickOnGround()
        {
            return context == null || ESWorkbenchToolCapabilityResolver.Has(
                context.Actions.Tools.ActiveCapabilities,
                ESWorkbenchToolCapabilities.GroundAction);
        }

        private void BeginTransform(
            string stableId,
            GameObject instance,
            Rect rect,
            Vector2 mousePosition)
        {
            ESWorkbenchSelection selection = context.Selection.Current;
            ESWorkbenchToolCapabilities toolCapabilities = context.Actions.Tools.ActiveCapabilities;
            bool moveTool = ESWorkbenchToolCapabilityResolver.Has(
                toolCapabilities, ESWorkbenchToolCapabilities.Move);
            bool precisePaintMove = ESWorkbenchToolCapabilityResolver.Has(
                    toolCapabilities, ESWorkbenchToolCapabilities.Paint)
                && context.Actions.Authoring.CanMove(selection);
            bool locked = context != null && context.IsHierarchyLocked(selection?.StableId);
            moving = ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: moveTool || precisePaintMove,
                moveInteractionEnabled: moveTool || precisePaintMove,
                canMove: context.Actions.Authoring.CanMove(selection),
                hierarchyLocked: locked);
            rotating = !locked
                && ESWorkbenchToolCapabilityResolver.Has(
                    toolCapabilities, ESWorkbenchToolCapabilities.Rotate)
                && context.Actions.Authoring.CanRotate(selection);
            scaling = !locked
                && ESWorkbenchToolCapabilityResolver.Has(
                    toolCapabilities, ESWorkbenchToolCapabilities.Scale)
                && context.Actions.Authoring.CanScale(selection);
            if (!moving && !rotating && !scaling) return;
            if (!pointerCoordinator.TryAcquire(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport))
                return;
            if (!gestureSession.TryArm(
                    ESWorkbenchPointerGestureSession.Kind.Transform, 0, mousePosition))
            {
                moving = false;
                rotating = false;
                scaling = false;
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                return;
            }
            transformingStableId = stableId;
            transformingSelection = selection;
            transformingSelections.Clear();
            transformingStartPositions.Clear();
            transformingStartValues.Clear();
            if ((moving || rotating || scaling) && context.Selection.CurrentSet.Count > 1)
            {
                foreach (ESWorkbenchSelection candidate in context.Selection.CurrentSet)
                {
                    if (candidate == null || candidate.IsEmpty) continue;
                    bool canTransform = moving
                        ? context.Actions.Authoring.CanMove(candidate)
                        : rotating
                            ? context.Actions.Authoring.CanRotate(candidate)
                            : context.Actions.Authoring.CanScale(candidate);
                    if (!canTransform)
                        continue;
                    int candidateIndex = previewStableIds.IndexOf(candidate.StableId);
                    if (candidateIndex < 0 || candidateIndex >= previewObjects.Count
                        || previewObjects[candidateIndex] == null)
                        continue;
                    transformingSelections.Add(candidate);
                    Transform candidateTransform = previewObjects[candidateIndex].transform;
                    transformingStartPositions.Add(candidateTransform.position - preview.GroupOrigin);
                    transformingStartValues.Add(
                        rotating ? candidateTransform.eulerAngles : candidateTransform.localScale);
                }
            }
            if (transformingSelections.Count == 0)
            {
                transformingSelections.Add(selection);
                transformingStartPositions.Add(instance.transform.position - preview.GroupOrigin);
                transformingStartValues.Add(rotating ? instance.transform.eulerAngles : instance.transform.localScale);
            }
            transformStartValue = moving ? instance.transform.position - preview.GroupOrigin
                : rotating ? instance.transform.eulerAngles : instance.transform.localScale;
            if (!moving && !transformGesture.Begin(
                    rotating ? ESWorkbenchMutationKind.Rotate : ESWorkbenchMutationKind.Scale,
                    mousePosition,
                    transformStartValue))
            {
                StopTransform();
                gestureSession.Cancel();
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                return;
            }
            if (moving)
            {
                if (!TryResolveWorldPoint(rect, mousePosition, out Vector3 pointerWorld))
                {
                    StopTransform();
                    gestureSession.Cancel();
                    pointerCoordinator.Release(
                        pointerOwnerToken,
                        0,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    return;
                }
                pointerWorld.y = transformStartValue.y;
                if (!moveAnchor.Capture(transformStartValue, pointerWorld))
                {
                    StopTransform();
                    gestureSession.Cancel();
                    pointerCoordinator.Release(
                        pointerOwnerToken,
                        0,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    return;
                }
            }
            pendingTransformValue = transformStartValue;
            pendingTransformValid = false;
        }

        private void BeginGuideTransform(
            ESWorkbenchSelection selection,
            Vector3 position,
            Rect rect,
            Vector2 mousePosition)
        {
            if (context == null || selection == null) return;
            bool moveTool = ESWorkbenchToolCapabilityResolver.Has(
                context.Actions.Tools.ActiveCapabilities,
                ESWorkbenchToolCapabilities.Move);
            bool precisePaintMove = ESWorkbenchToolCapabilityResolver.Has(
                    context.Actions.Tools.ActiveCapabilities,
                    ESWorkbenchToolCapabilities.Paint)
                && context.Actions.Authoring.CanMove(selection);
            moving = ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: moveTool || precisePaintMove,
                moveInteractionEnabled: moveTool || precisePaintMove,
                canMove: context.Actions.Authoring.CanMove(selection),
                hierarchyLocked: context.IsHierarchyLocked(selection.StableId));
            rotating = false;
            scaling = false;
            if (!moving) return;
            if (!pointerCoordinator.TryAcquire(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport))
                return;
            if (!gestureSession.TryArm(
                    ESWorkbenchPointerGestureSession.Kind.Transform, 0, mousePosition))
            {
                moving = false;
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                return;
            }
            transformingStableId = selection.StableId;
            transformingSelection = selection;
            transformStartValue = position;
            if (!TryResolveWorldPoint(rect, mousePosition, out Vector3 pointerWorld))
            {
                StopTransform();
                gestureSession.Cancel();
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                return;
            }
            pointerWorld.y = position.y;
            if (!moveAnchor.Capture(position, pointerWorld))
            {
                StopTransform();
                gestureSession.Cancel();
                pointerCoordinator.Release(
                    pointerOwnerToken,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                return;
            }
            pendingTransformValue = position;
            pendingTransformValid = false;
        }

        private void UpdateTransformPreview(
            Rect rect,
            Vector2 mousePosition,
            bool lockDominantAxis,
            bool finalize = false)
        {
            if (!gestureSession.TryEnsureStarted(0, mousePosition)) return;
            if (moving)
            {
                pendingTransformValid = false;
                if (!TryResolveWorldPoint(
                        rect,
                        mousePosition,
                        false,
                        true,
                        out Vector3 point,
                        true))
                {
                    ApplyPreviewTransform(transformingStableId, transformStartValue);
                    return;
                }
                point.y = moveAnchor.PointerStart.y;
                if (!moveAnchor.TryResolve(
                        point,
                        context.SnapPosition,
                        ESWorkbenchMoveAxes.Horizontal,
                        lockDominantAxis,
                        out pendingTransformValue))
                {
                    ApplyPreviewTransform(transformingStableId, transformStartValue);
                    return;
                }
            }
            else
            {
                // 阈值只负责防止误触；越过阈值的首帧位移必须立即进入增量解析。
                pendingTransformValid = finalize
                    ? transformGesture.TryFinalize(
                        mousePosition,
                        rotating ? context.SnapRotation : context.SnapScale,
                        out pendingTransformValue)
                    : transformGesture.TryUpdate(
                        mousePosition,
                        rotating ? context.SnapRotation : context.SnapScale,
                        out pendingTransformValue);
                if (!pendingTransformValid)
                {
                    ApplyPreviewTransform(transformingStableId, transformStartValue);
                    return;
                }
            }
            pendingTransformValid = true;
            ApplyPreviewTransform(transformingStableId, pendingTransformValue);
        }

        private void CommitTransform()
        {
            ESWorkbenchSelection selection = transformingSelection;
            Vector3 value = pendingTransformValue;
            bool shouldCommit = pendingTransformValid;
            bool commitMove = moving;
            bool commitRotate = rotating;
            ESWorkbenchSelection[] batchSelections = transformingSelections.ToArray();
            Vector3[] batchStarts = transformingStartPositions.ToArray();
            Vector3[] batchTransformStarts = transformingStartValues.ToArray();
            Vector3 batchStartValue = transformStartValue;
            StopTransform();
            gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit);
            pointerCoordinator.Release(
                pointerOwnerToken,
                0,
                ESWorkbenchPointerOwnerKind.Viewport);
            ReleaseMouseControl();
            if (!shouldCommit) return;
            bool succeeded = commitMove
                ? CommitMoveSelections(selection, value, batchSelections, batchStarts, batchStartValue)
                : commitRotate
                    ? CommitRotateOrScaleSelections(
                        selection, value, batchSelections, batchTransformStarts, batchStartValue, true)
                    : CommitRotateOrScaleSelections(
                        selection, value, batchSelections, batchTransformStarts, batchStartValue, false);
            if (!succeeded) RequestRebuild(false);
        }

        private bool CommitRotateOrScaleSelections(
            ESWorkbenchSelection primarySelection,
            Vector3 primaryTarget,
            IReadOnlyList<ESWorkbenchSelection> selections,
            IReadOnlyList<Vector3> startValues,
            Vector3 startValue,
            bool rotation)
        {
            if (selections == null || selections.Count <= 1)
            {
                return rotation
                    ? context.Actions.Authoring.TryRotate(primarySelection, primaryTarget, out _)
                    : context.Actions.Authoring.TryScale(primarySelection, primaryTarget, out _);
            }
            var targets = new List<Vector3>(startValues.Count);
            if (rotation)
            {
                Vector3 delta = primaryTarget - startValue;
                for (int i = 0; i < startValues.Count; i++)
                    targets.Add(startValues[i] + delta);
                return context.Actions.Authoring.TryRotateMany(selections, targets, out _);
            }
            Vector3 ratio = new Vector3(
                SafeScaleRatio(primaryTarget.x, startValue.x),
                SafeScaleRatio(primaryTarget.y, startValue.y),
                SafeScaleRatio(primaryTarget.z, startValue.z));
            for (int i = 0; i < startValues.Count; i++)
                targets.Add(Vector3.Scale(startValues[i], ratio));
            return context.Actions.Authoring.TryScaleMany(selections, targets, out _);
        }

        private static float SafeScaleRatio(float target, float start)
        {
            return Mathf.Abs(start) > 0.0001f ? target / start : 1f;
        }

        private bool CommitMoveSelections(
            ESWorkbenchSelection primarySelection,
            Vector3 primaryTarget,
            IReadOnlyList<ESWorkbenchSelection> selections,
            IReadOnlyList<Vector3> startPositions,
            Vector3 startValue)
        {
            if (selections == null || selections.Count <= 1)
                return context.Actions.Authoring.TryMove(primarySelection, primaryTarget, out _);
            Vector3 delta = primaryTarget - startValue;
            var targets = new List<Vector3>(startPositions.Count);
            for (int i = 0; i < startPositions.Count; i++)
                targets.Add(preview.GroupOrigin + startPositions[i] + delta);
            return context.Actions.Authoring.TryMoveMany(selections, targets, out _);
        }

        private bool TryHitPlacement(Rect rect, Vector2 guiPoint, out string stableId, out GameObject instance)
        {
            stableId = string.Empty;
            instance = null;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!ESWorkbenchCameraViewportProjection.TryNormalize(
                    rect, interactionRect, guiPoint, out Vector3 viewportPoint))
                return false;
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            float nearest = float.MaxValue;
            for (int i = 0; i < previewObjects.Count && i < previewStableIds.Count; i++)
            {
                GameObject candidate = previewObjects[i];
                string candidateId = previewStableIds[i];
                if (candidate == null || string.IsNullOrEmpty(candidateId)) continue;
                Bounds bounds = previewBounds.Calculate(candidate);
                if (bounds.IntersectRay(ray, out float hitDistance) && hitDistance < nearest)
                {
                    nearest = hitDistance;
                    stableId = candidateId;
                    instance = candidate;
                }
            }
            if (instance != null) return true;

            float nearestScreenDistance = float.MaxValue;
            float nearestScreenDepth = float.MaxValue;
            for (int i = 0; i < previewObjects.Count && i < previewStableIds.Count; i++)
            {
                GameObject candidate = previewObjects[i];
                string candidateId = previewStableIds[i];
                if (candidate == null || string.IsNullOrEmpty(candidateId)) continue;
                Bounds bounds = previewBounds.Calculate(candidate);

                // Small/point-like authoring objects often have a sub-pixel collider
                // even though their visual marker is clearly under the pointer. Keep
                // the real ray hit authoritative, then use the shared screen radius
                // as a deterministic fallback only when no ray target exists.
                if (!TryProjectWorldToGui(
                        rect,
                        interactionRect,
                        bounds.center - preview.GroupOrigin,
                        out Vector2 screenPoint,
                        out float depth))
                    continue;
                float screenDistance = Vector2.Distance(screenPoint, guiPoint);
                if (screenDistance > feel.SelectionHitRadiusPixels
                    || screenDistance >= nearestScreenDistance
                    || depth >= nearestScreenDepth)
                    continue;
                nearestScreenDistance = screenDistance;
                nearestScreenDepth = depth;
                stableId = candidateId;
                instance = candidate;
            }
            return instance != null;
        }

        private bool TryProjectWorldToGui(
            Rect rect,
            Rect interactionRect,
            Vector3 localWorldPoint,
            out Vector2 guiPoint,
            out float depth)
        {
            guiPoint = default;
            depth = float.MaxValue;
            return ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                preview?.Camera,
                preview == null ? localWorldPoint : preview.GroupOrigin + localWorldPoint,
                rect,
                interactionRect,
                out guiPoint,
                out depth);
        }

        private bool TryHitAuthoringGuide(
            Rect rect,
            Vector2 guiPoint,
            out ESWorkbenchSelection selection,
            out Vector3 position)
        {
            selection = null;
            position = default;
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null) return false;
            // 区域是可见的贴地作者层，不应因为相机射线暂时无法求交（斜视、
            // 无高度场或近似平行）就把同一指针交给笔刷。精确 POI 仍使用屏幕
            // 半径命中，区域稍后使用投影矩形作为世界命中的确定性兜底。
            bool hasWorldPoint = TryResolveWorldPoint(
                rect, guiPoint, false, false, out Vector3 point);
            if (definition.pois != null)
            {
                for (int i = definition.pois.Count - 1; i >= 0; i--)
                {
                    ESWorldMapPoiDefinition poi = definition.pois[i];
                    ESWorkbenchSelection cachedSelection = poi == null
                        ? ESWorkbenchSelection.Empty
                        : hitSelectionCache.GetOrCreateLocal(
                            "world.poi", poi.poiId, "world.poi.", payload: poi.poiId);
                    string stableId = cachedSelection.IsEmpty ? string.Empty : cachedSelection.StableId;
                    if (poi == null || (context != null && !context.IsHierarchyVisible(stableId)))
                        continue;
                    bool hitByScreenRadius = false;
                    if (TryProjectWorldToGui(
                            rect,
                            ESWorkbenchViewportOverlay.GetInteractionRect(
                                rect,
                                readOnlyGameView
                                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                                    : ESWorkbenchViewportOverlay.HeaderHeight),
                            new Vector3(poi.position.x, SampleWorldHeight(poi.position), poi.position.y),
                            out Vector2 poiScreen,
                            out _))
                        hitByScreenRadius = Vector2.Distance(poiScreen, guiPoint)
                            <= feel.SelectionHitRadiusPixels;
                    if (!hitByScreenRadius) continue;
                    position = new Vector3(poi.position.x, SampleWorldHeight(poi.position), poi.position.y);
                    selection = cachedSelection;
                    return true;
                }
            }
            if (definition.regions == null) return false;
            for (int i = definition.regions.Count - 1; i >= 0; i--)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                ESWorkbenchSelection cachedSelection = region == null
                    ? ESWorkbenchSelection.Empty
                    : hitSelectionCache.GetOrCreateLocal(
                        "world.region", region.regionId, "world.region.", payload: region.regionId);
                string stableId = cachedSelection.IsEmpty ? string.Empty : cachedSelection.StableId;
                if (region == null || (context != null && !context.IsHierarchyVisible(stableId))
                    || (!hasWorldPoint && !TryHitProjectedRegion(rect, guiPoint, region))
                    || (hasWorldPoint && !region.Contains(new Vector2(point.x, point.z))
                        && !TryHitProjectedRegion(rect, guiPoint, region))) continue;
                Vector2 center = (region.min + region.max) * 0.5f;
                position = new Vector3(center.x, SampleWorldHeight(center), center.y);
                selection = cachedSelection;
                return true;
            }
            return false;
        }

        private bool TryHitProjectedRegion(
            Rect rect,
            Vector2 guiPoint,
            ESWorldMapRegionDefinition region)
        {
            if (preview?.Camera == null || region == null) return false;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!interactionRect.Contains(guiPoint)) return false;
            Vector2 a = region.min;
            Vector2 b = new Vector2(region.max.x, region.min.y);
            Vector2 c = region.max;
            Vector2 d = new Vector2(region.min.x, region.max.y);
            int projectedCount = 0;
            TryProjectRegionCorner(rect, interactionRect, a, ref projectedCount);
            TryProjectRegionCorner(rect, interactionRect, b, ref projectedCount);
            TryProjectRegionCorner(rect, interactionRect, c, ref projectedCount);
            TryProjectRegionCorner(rect, interactionRect, d, ref projectedCount);
            if (projectedCount < 3) return false;
            // 四角全部可见时按真实投影多边形命中；只有视锥裁切导致部分角点
            // 不可投影时才退回有限包围盒，避免边缘区域完全失去可操作性。
            if (projectedCount == 4)
                return ESWorkbenchScreenGeometry.ContainsPolygon(
                    projectedRegionCorners,
                    projectedCount,
                    guiPoint,
                    Mathf.Max(2f, feel.SelectionHitRadiusPixels));
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < projectedCount; i++)
            {
                Vector2 corner = projectedRegionCorners[i];
                minX = Mathf.Min(minX, corner.x);
                minY = Mathf.Min(minY, corner.y);
                maxX = Mathf.Max(maxX, corner.x);
                maxY = Mathf.Max(maxY, corner.y);
            }
            float padding = Mathf.Max(2f, feel.SelectionHitRadiusPixels);
            return guiPoint.x >= minX - padding && guiPoint.x <= maxX + padding
                && guiPoint.y >= minY - padding && guiPoint.y <= maxY + padding;
        }

        private void TryProjectRegionCorner(
            Rect rect,
            Rect interactionRect,
            Vector2 corner,
            ref int projectedCount)
        {
            if (projectedCount >= projectedRegionCorners.Length) return;
            Vector3 world = new Vector3(corner.x, SampleWorldHeight(corner), corner.y);
            if (!ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    preview.Camera,
                    preview.GroupOrigin + world,
                    rect,
                    interactionRect,
                    out Vector2 projected,
                    out _,
                    allowOutside: true)) return;
            projectedRegionCorners[projectedCount] = projected;
            projectedCount++;
        }

        private void ApplyPreviewTransform(string stableId, Vector3 value)
        {
            if (transformingSelections.Count > 1)
            {
                Vector3 delta = value - transformStartValue;
                Vector3 ratio = new Vector3(
                    SafeScaleRatio(value.x, transformStartValue.x),
                    SafeScaleRatio(value.y, transformStartValue.y),
                    SafeScaleRatio(value.z, transformStartValue.z));
                for (int i = 0; i < transformingSelections.Count; i++)
                {
                    int selectedIndex = previewStableIds.IndexOf(transformingSelections[i].StableId);
                    if (selectedIndex < 0 || selectedIndex >= previewObjects.Count
                        || previewObjects[selectedIndex] == null)
                        continue;
                    Transform selectedTransform = previewObjects[selectedIndex].transform;
                    if (moving)
                        selectedTransform.position =
                            preview.GroupOrigin + transformingStartPositions[i] + delta;
                    else if (rotating)
                        selectedTransform.eulerAngles = transformingStartValues[i] + delta;
                    else if (scaling)
                        selectedTransform.localScale = Vector3.Scale(transformingStartValues[i], ratio);
                }
                return;
            }
            int index = previewStableIds.IndexOf(stableId);
            if (index < 0 || index >= previewObjects.Count || previewObjects[index] == null) return;
            Transform target = previewObjects[index].transform;
            if (moving) target.position = preview.GroupOrigin + value;
            else if (rotating) target.rotation = Quaternion.Euler(value);
            else if (scaling) target.localScale = value;
        }

        public void UpdateDropPreview(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition,
            float spacing,
            Vector3 previewSize,
            bool accepted = true,
            string reason = null)
        {
            UpdateDropPreview(
                item,
                items,
                localPosition,
                spacing,
                previewSize,
                new ESWorkbenchDropPreviewState(accepted, reason));
        }

        public void UpdateDropPreview(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition,
            float spacing,
            Vector3 previewSize,
            ESWorkbenchDropPreviewState state)
        {
            UpdateDropPreview(
                item,
                items,
                localPosition,
                spacing,
                previewSize,
                default,
                false,
                state);
        }

        public void UpdateDropPreview(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition,
            float spacing,
            Vector3 previewSize,
            Vector3 resolvedWorldPosition,
            bool hasResolvedWorldPosition,
            ESWorkbenchDropPreviewState state)
        {
            Vector3 anchor = default;
            // 预览必须复用正式落点合同；否则工具栏/非交互区会出现
            // “预览可见、释放拒绝”的坐标语义分裂。
            bool resolved = item != null && (hasResolvedWorldPosition
                ? ESWorkbenchDropPointPolicy.IsFinite(resolvedWorldPosition)
                : TryResolveDropPoint(
                    localPosition,
                    item.DragMode == ESWorkbenchContentDragMode.ActivateTool,
                    out resolvedWorldPosition));
            if (resolved) anchor = resolvedWorldPosition;
            if (!resolved)
            {
                ClearDropPreview();
                return;
            }
            IReadOnlyList<ESWorkbenchObjectDescriptor> effectiveItems = items != null && items.Count > 0
                ? items
                : new[] { item };
            int count = effectiveItems.Count;
            bool currentSnapEnabled = context?.Layout?.snapEnabled == true;
            float currentSnapStep = context?.Layout?.moveSnap ?? 0f;
            bool contentMatches = state.Rejected
                || (MatchesDropPreviewContent(effectiveItems)
                    && dropPreviewHandles.Count == count);
            if (lastDropPreviewAnchorValid
                && contentMatches
                && ReferenceEquals(dropPreviewItem, item)
                && ESWorkbenchDropPreviewRefreshPolicy.IsEquivalent(
                    lastDropPreviewAnchor,
                    anchor,
                    lastDropPreviewCount,
                    count,
                    lastDropPreviewSpacing,
                    spacing,
                    dropPreviewSize,
                    previewSize,
                    dropPreviewState,
                    state,
                    previousSnapEnabled: lastDropPreviewSnapEnabled,
                    nextSnapEnabled: currentSnapEnabled,
                    previousSnapStep: lastDropPreviewSnapStep,
                    nextSnapStep: currentSnapStep))
                return;
            if (state.Rejected)
            {
                // 拒绝态只保留确定性的红色目标框，避免临时模型仍以正常材质
                // 显示而造成“看起来可以放置”的误导。
                DisposeDropPreviewHandles();
            }
            else if (!contentMatches)
            {
                DisposeDropPreviewHandles();
                dropPreviewObjectIds.Clear();
                dropPreviewSources.Clear();
                for (int i = 0; i < effectiveItems.Count; i++)
                {
                    GameObject source = effectiveItems[i]?.Source as GameObject;
                    dropPreviewObjectIds.Add(effectiveItems[i]?.ObjectId ?? string.Empty);
                    dropPreviewSources.Add(source);
                    ESEditorPreviewModelHandle handle = source == null
                        ? null
                        : preview?.CreateModelGroup(
                            source,
                            source.name + " · 拖放预览",
                            samplingTarget: false);
                    dropPreviewHandles.Add(handle);
                }
            }
            dropPreviewItem = item;
            dropPreviewSize = previewSize;
            dropPreviewState = state;
            ESWorkbenchDropLayout.FillGridPositions(
                anchor,
                count,
                spacing,
                context == null ? null : context.SnapPosition,
                dropPreviewPositions,
                feel.MinimumDropSpacing);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = dropPreviewPositions[i];
                if (i < dropPreviewHandles.Count && dropPreviewHandles[i]?.Instance != null)
                    dropPreviewHandles[i].Instance.transform.position = preview.GroupOrigin + position;
            }
            lastDropPreviewAnchor = anchor;
            lastDropPreviewCount = count;
            lastDropPreviewSpacing = spacing;
            lastDropPreviewSnapEnabled = currentSnapEnabled;
            lastDropPreviewSnapStep = currentSnapStep;
            lastDropPreviewAnchorValid = true;
            renderHost.MarkDirtyRepaint();
        }

        private bool MatchesDropPreviewContent(IReadOnlyList<ESWorkbenchObjectDescriptor> items)
        {
            if (items == null || dropPreviewObjectIds.Count != items.Count
                || dropPreviewSources.Count != items.Count) return false;
            for (int i = 0; i < items.Count; i++)
                if (!string.Equals(
                        dropPreviewObjectIds[i],
                        items[i]?.ObjectId ?? string.Empty,
                        StringComparison.Ordinal)
                    || dropPreviewSources[i] != items[i]?.Source as GameObject)
                    return false;
            return true;
        }

        public void ClearDropPreview()
        {
            bool hadPreview = dropPreviewItem != null || dropPreviewHandles.Count > 0;
            dropPreviewItem = null;
            dropPreviewPositions.Clear();
            dropPreviewObjectIds.Clear();
            dropPreviewSources.Clear();
            dropPreviewState = ESWorkbenchDropPreviewState.Allowed;
            lastDropPreviewAnchor = default;
            lastDropPreviewCount = -1;
            lastDropPreviewSpacing = 0f;
            lastDropPreviewSnapEnabled = false;
            lastDropPreviewSnapStep = 0f;
            lastDropPreviewAnchorValid = false;
            DisposeDropPreviewHandles();
            if (hadPreview && !disposed) renderHost.MarkDirtyRepaint();
        }

        private void DisposeDropPreviewHandles()
        {
            for (int i = dropPreviewHandles.Count - 1; i >= 0; i--)
                dropPreviewHandles[i]?.Dispose();
            dropPreviewHandles.Clear();
        }

        private void DrawAuthoringGuides()
        {
            if (readOnlyGameView || preview?.Camera == null || draft?.Definition == null) return;
            ESWorldMapDefinition definition = draft.Definition;
            Handles.SetCamera(preview.Camera);
            Color previous = Handles.color;
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition poi = definition.pois[i];
                    ESWorkbenchSelection cachedSelection = poi == null
                        ? ESWorkbenchSelection.Empty
                        : hitSelectionCache.GetOrCreateLocal(
                            "world.poi", poi.poiId, "world.poi.", payload: poi.poiId);
                    string stableId = cachedSelection.IsEmpty ? string.Empty : cachedSelection.StableId;
                    if (poi == null || (context != null && !context.IsHierarchyVisible(stableId))) continue;
                    Vector3 position = ResolveGuidePosition(stableId,
                        new Vector3(poi.position.x, SampleWorldHeight(poi.position), poi.position.y));
                    bool selected = context?.Selection.Current?.StableId == stableId;
                    bool hovered = !selected && hover.IsHovered(stableId);
                    Handles.color = ESWorkbenchViewportRenderStyle.WithAlpha(
                        ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                            selected
                                ? ESWorkbenchViewportRenderStyle.InteractionState.Selected
                                : hovered
                                    ? ESWorkbenchViewportRenderStyle.InteractionState.Hover
                                    : ESWorkbenchViewportRenderStyle.InteractionState.Normal),
                        selected || hovered ? 0.92f : 0.85f);
                    Vector3 world = preview.GroupOrigin + position;
                    float markerPixels = feel.ResolveMarkerRadiusPixels(selected, hovered);
                    float markerRadius = Mathf.Max(0.05f, definition.chunkSize * 0.01f);
                    if (ESWorkbenchCameraViewportProjection.TryResolveWorldRadiusForPixels(
                            preview.Camera, world, renderHost.contentRect, markerPixels,
                            out float projectedRadius))
                        markerRadius = projectedRadius;
                    Handles.DrawWireDisc(world, Vector3.up, markerRadius);
                    Handles.DrawLine(world, world + Vector3.up * Mathf.Max(
                        markerRadius * 2f, definition.terrainHeightScale * 0.05f));
                }
            Handles.color = previous;
        }

        private void DrawTerrainBrushGuide(Rect rect)
        {
            if (!IsTerrainPaintingInteraction() || moving || rotating || scaling
                || preview?.Camera == null || draft?.Definition == null) return;
            Vector3 point;
            if (paintingTerrain && lastTerrainPaintPointValid)
                point = lastTerrainPaintPoint;
            else
            {
                Vector2 mouse = Event.current.mousePosition;
                if (!rect.Contains(mouse) || !TryResolveTerrainPaintPoint(rect, mouse, out point)) return;
            }

            float radius = Mathf.Max(0.5f, terrainBrushRadius?.Invoke() ?? 8f);
            Handles.SetCamera(preview.Camera);
            Color previous = Handles.color;
            Handles.color = ESWorkbenchViewportRenderStyle.WithAlpha(
                ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                    ESWorkbenchViewportRenderStyle.InteractionState.Brush), 0.96f);
            for (int i = 0; i < terrainBrushGuidePoints.Length; i++)
            {
                float angle = i / (float)(terrainBrushGuidePoints.Length - 1) * Mathf.PI * 2f;
                Vector2 world2D = new Vector2(
                    point.x + Mathf.Cos(angle) * radius,
                    point.z + Mathf.Sin(angle) * radius);
                terrainBrushGuidePoints[i] = preview.GroupOrigin
                    + new Vector3(world2D.x, SampleWorldHeight(world2D) + 0.05f, world2D.y);
            }
            Handles.DrawAAPolyLine(2f, terrainBrushGuidePoints);
            float strength = Mathf.Clamp01(terrainBrushStrength?.Invoke() ?? 0.65f);
            var innerPoints = new Vector3[terrainBrushGuidePoints.Length];
            float innerRadius = radius * (0.18f + strength * 0.72f);
            for (int i = 0; i < innerPoints.Length; i++)
            {
                float angle = i / (float)(innerPoints.Length - 1) * Mathf.PI * 2f;
                Vector2 world2D = new Vector2(
                    point.x + Mathf.Cos(angle) * innerRadius,
                    point.z + Mathf.Sin(angle) * innerRadius);
                innerPoints[i] = preview.GroupOrigin
                    + new Vector3(world2D.x, SampleWorldHeight(world2D) + 0.07f, world2D.y);
            }
            Handles.color = ESWorkbenchViewportRenderStyle.WithAlpha(
                ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                    ESWorkbenchViewportRenderStyle.InteractionState.Brush),
                0.28f + strength * 0.58f);
            Handles.DrawAAPolyLine(1f + strength * 1.5f, innerPoints);
            Vector3 center = preview.GroupOrigin + point + Vector3.up * 0.08f;
            float cross = Mathf.Clamp(radius * 0.14f, 0.35f, 2.5f);
            Handles.DrawLine(center - Vector3.right * cross, center + Vector3.right * cross);
            Handles.DrawLine(center - Vector3.forward * cross, center + Vector3.forward * cross);
            string summary = terrainBrushSummary?.Invoke() ?? ("半径 " + radius.ToString("0.#") + "m");
            Handles.Label(center + Vector3.up * Mathf.Max(0.5f, radius * 0.08f), summary);
            Handles.color = previous;
        }

        private Vector3 ResolveGuidePosition(string stableId, Vector3 fallback)
        {
            return moving && pendingTransformValid && string.Equals(transformingStableId, stableId, StringComparison.Ordinal)
                ? pendingTransformValue
                : fallback;
        }

        private void DrawTransformTargetOutline()
        {
            if (!pendingTransformValid || preview?.Camera == null || string.IsNullOrEmpty(transformingStableId)) return;
            if (transformingStableId.StartsWith("world.region.", StringComparison.Ordinal))
            {
                DrawRegionTransformOutline();
                return;
            }
            Handles.SetCamera(preview.Camera);
            Color previous = Handles.color;
            Handles.color = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Selected);
            Bounds targetBounds = ResolveTransformTargetBounds();
            Handles.DrawWireCube(targetBounds.center, targetBounds.size);
            if (moving)
            {
                Vector3 start = preview.GroupOrigin + transformStartValue;
                Vector3 target = preview.GroupOrigin + pendingTransformValue;
                Handles.DrawDottedLine(start, target, 4f);
                Handles.Label(target + Vector3.up * Mathf.Max(0.5f, targetBounds.extents.y),
                    "目标  " + pendingTransformValue.x.ToString("0.##") + ", "
                    + pendingTransformValue.y.ToString("0.##") + ", "
                    + pendingTransformValue.z.ToString("0.##"));
            }
            Handles.color = previous;
        }

        private void DrawRegionTransformOutline()
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition?.regions == null) return;
            string regionId = transformingStableId.Substring("world.region.".Length);
            ESWorldMapRegionDefinition region = definition.regions.Find(
                value => value != null && string.Equals(value.regionId, regionId, StringComparison.Ordinal));
            if (region == null) return;

            Vector2 originalCenter = (region.min + region.max) * 0.5f;
            Vector2 targetCenter = new Vector2(pendingTransformValue.x, pendingTransformValue.z);
            Vector2 halfSize = Vector2.Max(Vector2.one * 0.05f, (region.max - region.min) * 0.5f);
            Vector2 targetMin = targetCenter - halfSize;
            Vector2 targetMax = targetCenter + halfSize;
            Vector2[] corners2D =
            {
                new Vector2(targetMin.x, targetMin.y),
                new Vector2(targetMin.x, targetMax.y),
                new Vector2(targetMax.x, targetMax.y),
                new Vector2(targetMax.x, targetMin.y),
                new Vector2(targetMin.x, targetMin.y)
            };
            Vector3[] corners = new Vector3[corners2D.Length];
            for (int i = 0; i < corners2D.Length; i++)
            {
                Vector2 point = corners2D[i];
                corners[i] = preview.GroupOrigin
                    + new Vector3(point.x, SampleWorldHeight(point) + 0.14f, point.y);
            }

            Handles.SetCamera(preview.Camera);
            Color previous = Handles.color;
            Handles.color = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Selected);
            Handles.DrawAAPolyLine(3f, corners);

            Vector3 start = preview.GroupOrigin
                + new Vector3(originalCenter.x, SampleWorldHeight(originalCenter) + 0.16f, originalCenter.y);
            Vector3 target = preview.GroupOrigin
                + new Vector3(targetCenter.x, SampleWorldHeight(targetCenter) + 0.16f, targetCenter.y);
            Handles.DrawDottedLine(start, target, 4f);
            Handles.DrawLine(target - Vector3.right * 0.8f, target + Vector3.right * 0.8f);
            Handles.DrawLine(target - Vector3.forward * 0.8f, target + Vector3.forward * 0.8f);
            Handles.Label(target + Vector3.up * Mathf.Max(0.6f, definition.terrainHeightScale * 0.04f),
                "区域目标  " + pendingTransformValue.x.ToString("0.##") + ", "
                + pendingTransformValue.z.ToString("0.##"));
            Handles.color = previous;
        }

        private Bounds ResolveTransformTargetBounds()
        {
            int index = previewStableIds.IndexOf(transformingStableId);
            if (index >= 0 && index < previewObjects.Count && previewObjects[index] != null)
                return previewBounds.Calculate(previewObjects[index]);
            Vector3 size = ResolveGuideSize(transformingStableId);
            return new Bounds(preview.GroupOrigin + pendingTransformValue + Vector3.up * size.y * 0.5f, size);
        }

        private Vector3 ResolveGuideSize(string stableId)
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null) return Vector3.one * 2f;
            if (stableId.StartsWith("world.region.", StringComparison.Ordinal))
            {
                string id = stableId.Substring("world.region.".Length);
                ESWorldMapRegionDefinition region = definition.regions?.Find(value => value != null && value.regionId == id);
                if (region != null)
                {
                    Vector2 size = Vector2.Max(Vector2.one * 0.1f, region.max - region.min);
                    return new Vector3(size.x, Mathf.Max(1f, definition.terrainHeightScale * 0.03f), size.y);
                }
            }
            return Vector3.one * Mathf.Max(1f, definition.chunkSize * 0.08f);
        }

        private void DrawDropPreviewTargets()
        {
            if (dropPreviewItem == null || dropPreviewPositions.Count == 0 || preview?.Camera == null) return;
            Handles.SetCamera(preview.Camera);
            Color previous = Handles.color;
            Handles.color = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                dropPreviewState.Accepted
                    ? ESWorkbenchViewportRenderStyle.InteractionState.PreviewAllowed
                    : ESWorkbenchViewportRenderStyle.InteractionState.PreviewRejected);
            for (int i = 0; i < dropPreviewPositions.Count; i++)
            {
                Vector3 position = dropPreviewPositions[i];
                Bounds bounds;
                if (i < dropPreviewHandles.Count && dropPreviewHandles[i]?.Instance != null)
                    bounds = previewBounds.Calculate(dropPreviewHandles[i].Instance);
                else
                    bounds = new Bounds(
                        preview.GroupOrigin + position + Vector3.up * dropPreviewSize.y * 0.5f,
                        new Vector3(
                            Mathf.Max(0.2f, dropPreviewSize.x),
                            Mathf.Max(0.2f, dropPreviewSize.y),
                            Mathf.Max(0.2f, dropPreviewSize.z)));
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.DrawWireDisc(preview.GroupOrigin + position, Vector3.up,
                    Mathf.Max(0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z)));
            }
            Vector3 labelPosition = preview.GroupOrigin + dropPreviewPositions[0] + Vector3.up * 1.5f;
            Handles.Label(labelPosition, dropPreviewPositions.Count > 1
                ? (dropPreviewState.Accepted
                    ? "释放以批量放置 " + dropPreviewPositions.Count + " 项"
                    : "不可放置 · " + dropPreviewPositions.Count + " 项")
                : (dropPreviewState.Accepted
                    ? "释放以放置 · " + dropPreviewItem.DisplayName
                    : "不可放置 · " + dropPreviewItem.DisplayName));
            if (dropPreviewState.Rejected && !string.IsNullOrWhiteSpace(dropPreviewState.Reason))
            {
                Handles.Label(labelPosition + Vector3.up * 0.7f,
                    dropPreviewState.ShortReason());
            }
            Handles.color = previous;
        }

        private float SampleWorldHeight(Vector2 position)
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition?.heightfield == null) return 0f;
            float u = Mathf.InverseLerp(definition.worldMin.x, definition.worldMax.x, position.x);
            float v = Mathf.InverseLerp(definition.worldMin.y, definition.worldMax.y, position.y);
            return ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                * definition.terrainHeightScale;
        }

        private void DrawSelectionOutline()
        {
            if (context == null) return;
            string stableId = context.Selection.Current?.StableId;
            int index = string.IsNullOrEmpty(stableId) ? -1 : previewStableIds.IndexOf(stableId);
            if (index < 0 || index >= previewObjects.Count || previewObjects[index] == null) return;
            Color previous = Handles.color;
            Handles.SetCamera(preview.Camera);
            Handles.color = ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                ESWorkbenchViewportRenderStyle.InteractionState.Selected);
            Bounds bounds = previewBounds.Calculate(previewObjects[index]);
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.color = previous;
        }

        private void DrawHoverOutline()
        {
            if (!hover.HasValue || moving || rotating || scaling || preview?.Camera == null) return;
            if (string.Equals(
                    context?.Selection.Current?.StableId,
                    hover.StableId,
                    StringComparison.Ordinal)) return;
            int index = previewStableIds.IndexOf(hover.StableId);
            if (index < 0 || index >= previewObjects.Count || previewObjects[index] == null) return;
            Color previous = Handles.color;
            Handles.SetCamera(preview.Camera);
            Handles.color = ESWorkbenchViewportRenderStyle.WithAlpha(
                ESWorkbenchViewportRenderStyle.ResolveInteractionColor(
                    ESWorkbenchViewportRenderStyle.InteractionState.Hover), 0.78f);
            Bounds bounds = previewBounds.Calculate(previewObjects[index]);
            Handles.DrawWireCube(bounds.center, bounds.size * 1.03f);
            Handles.color = previous;
        }

        public void CancelInteraction()
        {
            ClearHover();
            ESWorkbenchGestureTerminationDecision terrainDecision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.Cancel,
                    ESWorkbenchCaptureLossPolicy.CancelPreview,
                    hasPreview: lastTerrainPaintPointValid);
            ESWorkbenchGestureTerminationDecision transformDecision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.Cancel,
                    ESWorkbenchCaptureLossPolicy.CancelPreview,
                    hasPreview: pendingTransformValid);
            bool wasPaintingTerrain = paintingTerrain;
            orbitInput.Release();
            if (paintingTerrain)
            {
                if (terrainDecision.RestorePreview && lastTerrainPaintPointValid)
                    FlushTerrainPreview(lastTerrainPaintPoint);
                paintingTerrain = false;
                lastTerrainPaintPointValid = false;
                terrainStrokeSampler.Reset();
                if (terrainDecision.CommitAuthoring)
                    terrainStrokeEnd?.Invoke();
                else
                    terrainStrokeCancel?.Invoke();
            }
            StopTransform();
            gestureSession.Cancel(transformDecision.Reason);
            pointerCoordinator.Release(
                pointerOwnerToken,
                0,
                ESWorkbenchPointerOwnerKind.Viewport);
            ReleaseMouseControl();
            if (!wasPaintingTerrain && transformDecision.RestorePreview)
                RequestRebuild(false);
            renderHost.MarkDirtyRepaint();
        }

        private void CompleteInterruptedInteraction()
        {
            // IMGUI Ignore 表示捕获丢失：连续笔刷保留已写入草稿的样本，
            // 变换预览则回滚。显式 CancelInteraction 仍然整笔回滚。
            ESWorkbenchGestureTerminationDecision terrainDecision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                    ESWorkbenchCaptureLossPolicy.CommitPendingSamples,
                    hasPreview: false);
            ESWorkbenchGestureTerminationDecision transformDecision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                    ESWorkbenchCaptureLossPolicy.CancelPreview,
                    hasPreview: pendingTransformValid);
            bool wasPaintingTerrain = paintingTerrain;
            if (paintingTerrain)
            {
                if (terrainDecision.FlushPendingSamples)
                    FlushTerrainStroke();
                if (terrainDecision.FlushPendingSamples && lastTerrainPaintPointValid)
                    FlushTerrainPreview(lastTerrainPaintPoint);
                paintingTerrain = false;
                lastTerrainPaintPointValid = false;
                terrainStrokeSampler.Reset();
                if (terrainDecision.CommitAuthoring)
                    terrainStrokeEnd?.Invoke();
                else
                    terrainStrokeCancel?.Invoke();
                if (terrainDecision.CommitAuthoring)
                    gestureSession.Finish(terrainDecision.Reason);
                else
                    gestureSession.Cancel(terrainDecision.Reason);
            }
            orbitInput.Release();
            StopTransform();
            if (!wasPaintingTerrain)
            {
                if (transformDecision.RestorePreview)
                    RequestRebuild(false);
                gestureSession.Cancel(transformDecision.Reason);
            }
            pointerCoordinator.Release(
                pointerOwnerToken,
                0,
                ESWorkbenchPointerOwnerKind.Viewport);
            ReleaseMouseControl();
            renderHost.MarkDirtyRepaint();
        }

        private void StopTransform()
        {
            moving = false;
            rotating = false;
            scaling = false;
            pendingTransformValid = false;
            transformingStableId = string.Empty;
            transformingSelection = null;
            transformingSelections.Clear();
            transformingStartPositions.Clear();
            transformingStartValues.Clear();
            moveAnchor.Reset();
            transformGesture.Reset();
            edgePanSession.Stop();
            edgePanSchedule?.Pause();
        }

        private void BeginEdgePan(Vector2 localPosition, bool lockDominantAxis)
        {
            if (!moving || readOnlyGameView) return;
            if (!edgePanSession.UpdatePointer(localPosition, lockDominantAxis))
                edgePanSession.Begin(
                    localPosition,
                    lockDominantAxis,
                    EditorApplication.timeSinceStartup);
            if (gestureSession.IsStarted && edgePanSession.IsActive
                && edgePanSchedule?.isActive == false)
            {
                edgePanSchedule.Resume();
            }
        }

        private void ApplyEdgePan()
        {
            if (!moving || !edgePanSession.IsActive || !gestureSession.IsStarted
                || !pointerCoordinator.Owns(
                    pointerOwnerToken, 0, ESWorkbenchPointerOwnerKind.Viewport)
                || !edgePanSession.TryAdvance(
                    EditorApplication.timeSinceStartup, out float deltaTime)) return;
            if (!TryEdgePanRenderPosition(edgePanSession.Pointer, deltaTime)) return;
            UpdateTransformPreview(
                renderHost.contentRect,
                edgePanSession.Pointer,
                edgePanSession.LockDominantAxis);
            renderHost.MarkDirtyRepaint();
        }

        public bool TryEdgePan(Vector2 localPosition, float deltaTime)
        {
            if (readOnlyGameView || renderHost == null) return false;
            Vector2 renderPosition = renderHost.WorldToLocal(this.LocalToWorld(localPosition));
            return TryEdgePanRenderPosition(renderPosition, deltaTime);
        }

        public bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message)
        {
            message = string.Empty;
            if (readOnlyGameView || context == null) return false;
            ESWorkbenchSelection selection = context.Selection.Current;
            if (!ESWorkbenchNudgeResolver.TryResolveDelta(
                    keyCode, shift, controlOrCommand, feel, out Vector3 delta)
                || !ESWorkbenchNudgeResolver.TryResolvePosition(
                    context.Hierarchy, selection, out Vector3 position)
                || context.IsHierarchyLocked(selection.StableId)
                || !context.Actions.Authoring.CanMove(selection)) return false;
            Vector3 target = context.SnapPosition(position + delta);
            bool committed = context.Actions.Authoring.TryMove(selection, target, out message);
            if (committed) Bind(draft, false);
            return committed;
        }

        private bool TryEdgePanRenderPosition(Vector2 renderPosition, float deltaTime)
        {
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                renderHost.contentRect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!ESWorkbenchViewportOverlay.AllowsEdgePanPointer(
                    renderHost.contentRect, interactionRect, renderPosition)) return false;
            if (!edgePan.Evaluate(interactionRect, renderPosition, deltaTime, out Vector2 delta)) return false;
            cameraNavigation.Pan(
                delta, renderHost.contentRect, feel.VerticalFieldOfViewDegrees);
            statusChanged?.Invoke();
            renderHost.MarkDirtyRepaint();
            return true;
        }

        private void ReleaseMouseControl()
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            activeControlId = 0;
        }

        private bool TryResolveWorldPoint(Rect rect, Vector2 guiPoint, out Vector3 point)
        {
            return TryResolveWorldPoint(rect, guiPoint, false, true, out point, false);
        }

        private bool TryResolveTerrainPaintPoint(Rect rect, Vector2 guiPoint, out Vector3 point)
        {
            return TryResolveWorldPoint(rect, guiPoint, true, false, out point);
        }

        private bool TryResolveWorldPoint(
            Rect rect,
            Vector2 guiPoint,
            bool requireTerrainSurface,
            bool clampToWorld,
            out Vector3 point,
            bool allowOutside = false)
        {
            point = default;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            // 拖放、指针和作者点击必须共享同一边界；视口外坐标不能被 Clamp 成边缘落点。
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!ESWorkbenchCameraViewportProjection.TryNormalize(
                    rect, interactionRect, guiPoint, out Vector3 viewportPoint, allowOutside))
                return false;
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            ESWorldMapDefinition activeDefinition = draft?.Definition;
            if (requireTerrainSurface
                && !allowOutside
                && activeDefinition?.heightfield != null)
            {
                // 严格绘制/点击必须命中真实高度场；允许边界夹取的拖放则
                // 继续走 Collider/平面与世界边界约束，保持预览和提交一致。
                Ray localRay = new Ray(ray.origin - preview.GroupOrigin, ray.direction);
                return ESWorldHeightfieldReadOnly.TryRaycast(activeDefinition, localRay, out point);
            }
            TerrainCollider terrainCollider = terrainObject?.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                if (terrainCollider.Raycast(ray, out RaycastHit terrainHit, 100000f))
                    point = terrainHit.point - preview.GroupOrigin;
                else if (requireTerrainSurface)
                    return false;
                else
                {
                    Plane fallbackPlane = new Plane(Vector3.up, preview.GroupOrigin);
                    if (!fallbackPlane.Raycast(ray, out float fallbackDistance)) return false;
                    point = ray.GetPoint(fallbackDistance) - preview.GroupOrigin;
                }
            }
            else
            {
                Plane plane = new Plane(Vector3.up, preview.GroupOrigin);
                if (!plane.Raycast(ray, out float distanceToPlane)) return false;
                point = ray.GetPoint(distanceToPlane) - preview.GroupOrigin;
            }
            if (activeDefinition != null && clampToWorld)
            {
                // 3D 主动移动允许射线暂时越过视口，以便边缘平移继续更新预览；
                // 作者坐标仍必须留在世界边界内。该约束独立于 heightfield，
                // 纯平面世界也不能因为没有高度场而越界。
                point.x = Mathf.Clamp(point.x, activeDefinition.worldMin.x, activeDefinition.worldMax.x);
                point.z = Mathf.Clamp(point.z, activeDefinition.worldMin.y, activeDefinition.worldMax.y);
            }
            if (activeDefinition?.heightfield != null)
            {
                ESWorldMapDefinition definition = activeDefinition;
                bool outsideWorld = point.x < definition.worldMin.x || point.x > definition.worldMax.x
                    || point.z < definition.worldMin.y || point.z > definition.worldMax.y;
                if (outsideWorld && (requireTerrainSurface || !clampToWorld)) return false;
                point.x = Mathf.Clamp(point.x, definition.worldMin.x, definition.worldMax.x);
                point.z = Mathf.Clamp(point.z, definition.worldMin.y, definition.worldMax.y);
                float u = Mathf.InverseLerp(definition.worldMin.x, definition.worldMax.x, point.x);
                float v = Mathf.InverseLerp(definition.worldMin.y, definition.worldMax.y, point.z);
                point.y = ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                    * definition.terrainHeightScale;
            }
            return true;
        }

        public bool TryResolveWorldPoint(
            Vector2 localPoint,
            out Vector3 point,
            bool allowOutside = false)
        {
            return TryResolveProjection(
                localPoint,
                allowOutside
                    ? ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.EdgePanPreview)
                    : ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.AuthorHit),
                out point);
        }

        public bool TryResolveTerrainPoint(
            Vector2 localPoint,
            out Vector3 point,
            bool allowOutside = false)
        {
            return TryResolveProjection(
                localPoint,
                allowOutside
                    ? ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.DropPreview,
                        requireTerrainSurface: true)
                    : ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.TerrainPaint),
                out point);
        }

        public bool TryResolveProjection(
            Vector2 localPoint,
            ESWorkbenchViewportProjectionRequest request,
            out Vector3 point)
        {
            switch (request.Intent)
            {
                case ESWorkbenchViewportProjectionIntent.DropPreview:
                    return TryResolveDropPoint(
                        localPoint, request.RequireTerrainSurface, out point);
                case ESWorkbenchViewportProjectionIntent.TerrainPaint:
                    return TryResolveExternalPoint(
                        localPoint,
                        request.RequireTerrainSurface,
                        request.AllowOutside,
                        out point);
                case ESWorkbenchViewportProjectionIntent.EdgePanPreview:
                    return TryResolveExternalPoint(
                        localPoint,
                        request.RequireTerrainSurface,
                        request.AllowOutside,
                        out point);
                default:
                    return TryResolveExternalPoint(
                        localPoint,
                        request.RequireTerrainSurface,
                        request.AllowOutside,
                        out point);
            }
        }

        /// <summary>
        /// 解析正式拖放点：允许落在地图内容留白或边界外并夹到世界边界，
        /// 但工具栏等非交互区域仍然拒绝。预览和提交必须共用这一入口。
        /// </summary>
        public bool TryResolveDropPoint(
            Vector2 localPoint,
            bool terrainOnly,
            out Vector3 point)
        {
            point = default;
            if (renderHost == null) return false;
            Vector2 renderPoint = renderHost.panel == null
                ? localPoint
                : this.ChangeCoordinatesTo(renderHost, localPoint);
            Rect rect = renderHost.contentRect;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!ESWorkbenchDropPointPolicy.CanCommit(interactionRect, renderPoint)) return false;
            return TryResolveWorldPoint(rect, renderPoint, terrainOnly, true, out point, true);
        }

        private bool TryResolveExternalPoint(
            Vector2 rootPoint,
            bool terrainOnly,
            bool allowOutside,
            out Vector3 point)
        {
            point = default;
            if (renderHost == null) return false;
            Vector2 renderPoint = renderHost.panel == null
                ? rootPoint
                : this.ChangeCoordinatesTo(renderHost, rootPoint);
            Rect rect = renderHost.contentRect;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(
                rect,
                readOnlyGameView
                    ? ESWorkbenchViewportOverlay.HeaderHeight + 29f
                    : ESWorkbenchViewportOverlay.HeaderHeight);
            if (!allowOutside && !interactionRect.Contains(renderPoint)) return false;
            return terrainOnly && !allowOutside
                ? TryResolveWorldPoint(rect, renderPoint, true, false, out point, false)
                : TryResolveWorldPoint(rect, renderPoint, false, true, out point, allowOutside);
        }

        internal bool TryGetPointerWorldPosition(out Vector3 point)
        {
            point = lastPointerWorldPosition;
            return lastPointerWorldPositionValid;
        }

        private void UpdatePointerWorldStatus(
            Rect rect,
            Rect interactionRect,
            Vector2 mousePosition)
        {
            if (interactionRect.Contains(mousePosition)
                && TryResolveWorldPoint(rect, mousePosition, out Vector3 next))
            {
                if (!lastPointerWorldPositionValid
                    || (lastPointerWorldPosition - next).sqrMagnitude > 0.0001f)
                {
                    lastPointerWorldPosition = next;
                    lastPointerWorldPositionValid = true;
                    statusChanged?.Invoke();
                }
                return;
            }
            ClearPointerWorldPosition();
        }

        private void ClearPointerWorldPosition()
        {
            if (!lastPointerWorldPositionValid) return;
            lastPointerWorldPositionValid = false;
            statusChanged?.Invoke();
        }

        private void UpdateTerrainPreview(Vector3 worldPoint, bool force)
        {
            ESWorldMapDefinition definition = draft?.Definition;
            ESWorldMapHeightfield field = definition?.heightfield;
            if (terrainData == null || field == null || field.width < 2 || field.height < 2) return;
            double now = EditorApplication.timeSinceStartup;
            if (!force && now - lastTerrainPreviewHeightUpdate < 0.05d) return;

            int resolution = terrainData.heightmapResolution;
            float worldWidth = Mathf.Max(0.001f, definition.worldMax.x - definition.worldMin.x);
            float worldDepth = Mathf.Max(0.001f, definition.worldMax.y - definition.worldMin.y);
            float radius = Mathf.Max(0.5f, terrainBrushRadius?.Invoke() ?? 8f);
            int radiusX = Mathf.Clamp(Mathf.CeilToInt(radius / worldWidth * (resolution - 1)) + 1, 1, resolution);
            int radiusY = Mathf.Clamp(Mathf.CeilToInt(radius / worldDepth * (resolution - 1)) + 1, 1, resolution);
            int width = Mathf.Min(resolution, radiusX * 2 + 1);
            int height = Mathf.Min(resolution, radiusY * 2 + 1);
            int centerX = Mathf.RoundToInt(Mathf.InverseLerp(
                definition.worldMin.x, definition.worldMax.x, worldPoint.x) * (resolution - 1));
            int centerY = Mathf.RoundToInt(Mathf.InverseLerp(
                definition.worldMin.y, definition.worldMax.y, worldPoint.z) * (resolution - 1));
            int startX = Mathf.Clamp(centerX - width / 2, 0, resolution - width);
            int startY = Mathf.Clamp(centerY - height / 2, 0, resolution - height);
            if (terrainPreviewHeightBuffer == null
                || terrainPreviewHeightBuffer.GetLength(0) != height
                || terrainPreviewHeightBuffer.GetLength(1) != width)
                terrainPreviewHeightBuffer = new float[height, width];
            FillTerrainPreviewHeightBuffer(
                field, terrainPreviewHeightBuffer, startX, startY, resolution);
            terrainData.SetHeightsDelayLOD(startX, startY, terrainPreviewHeightBuffer);
            if (force) terrainData.SyncHeightmap();
            regionGuideTerrainRevision++;
            lastTerrainPreviewHeightUpdate = now;
        }

        private void QueueTerrainPreview(Vector3 worldPoint)
        {
            if (terrainPreviewCoalescer == null) return;
            terrainPreviewCoalescer.Queue(worldPoint, EditorApplication.timeSinceStartup);
            if (terrainPreviewUpdateSchedule != null || renderHost == null) return;
            ScheduleTerrainPreviewFlush();
        }

        private void FlushTerrainPreview(Vector3 worldPoint)
        {
            terrainPreviewUpdateSchedule?.Pause();
            terrainPreviewUpdateSchedule = null;
            terrainPreviewCoalescer?.Queue(worldPoint, EditorApplication.timeSinceStartup);
            if (terrainPreviewCoalescer != null
                && terrainPreviewCoalescer.Flush(out Vector3 endpoint))
                UpdateTerrainPreview(endpoint, true);
        }

        private void ScheduleTerrainPreviewFlush()
        {
            if (terrainPreviewUpdateSchedule != null || renderHost == null
                || terrainPreviewCoalescer == null) return;
            int delay = Mathf.Max(1, Mathf.CeilToInt(
                (float)terrainPreviewCoalescer.RemainingMilliseconds(
                    EditorApplication.timeSinceStartup)));
            terrainPreviewUpdateSchedule = renderHost.schedule.Execute(() =>
            {
                terrainPreviewUpdateSchedule = null;
                double now = EditorApplication.timeSinceStartup;
                if (terrainPreviewCoalescer == null
                    || !terrainPreviewCoalescer.TryConsume(now, out Vector3 point))
                {
                    if (terrainPreviewCoalescer?.HasPending == true) ScheduleTerrainPreviewFlush();
                    return;
                }
                UpdateTerrainPreview(point, false);
                renderHost.MarkDirtyRepaint();
            }).StartingIn(delay);
        }

        private static void FillTerrainPreviewHeightBuffer(
            ESWorldMapHeightfield field,
            float[,] target,
            int startX,
            int startY,
            int terrainResolution)
        {
            int height = target.GetLength(0);
            int width = target.GetLength(1);
            float denominator = Mathf.Max(1, terrainResolution - 1);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.RoundToInt((startX + x) / denominator * (field.width - 1));
                    int sy = Mathf.RoundToInt((startY + y) / denominator * (field.height - 1));
                    target[y, x] = ESWorldHeightfieldReadOnly.Get(field, sx, sy);
                }
        }

        private void EnsurePreview()
        {
            if (preview?.IsReady == true) return;
            preview?.Dispose();
            preview = new ESEditorPreviewRenderContext(
                "ES World Authoring Viewport",
                ESEditorPreviewSceneMode.PreviewScene);
            preview.Ensure();
            if (preview.Camera != null)
            {
                preview.Camera.fieldOfView = feel.VerticalFieldOfViewDegrees;
                preview.Camera.nearClipPlane = 0.1f;
                preview.Camera.backgroundColor = new Color(0.055f, 0.07f, 0.08f, 1f);
            }
        }

        private void ApplyPreviewCamera(Rect rect)
        {
            if (preview?.Camera == null) return;
            preview.Camera.transform.position = preview.GroupOrigin + gameCameraPosition;
            preview.Camera.transform.rotation = gameCameraRotation;
            preview.Camera.aspect = Mathf.Max(0.25f, rect.width / Mathf.Max(1f, rect.height));
            preview.Camera.farClipPlane = 10000f;
        }

        /// <summary>
        /// 玩家视角保留直接 Camera 渲染（便于表达玩家构图），但导航输入仍统一
        /// 写入轨道状态。首次进入时把直接 Camera 反投影到轨道状态，之后每帧再
        /// 将轨道状态投影回直接 Camera，避免“输入已消费、画面不动”的断链。
        /// </summary>
        private void SynchronizePlayerCameraNavigation(Rect rect)
        {
            if (!readOnlyGameView || gameCameraMode != ESWorldGamePreviewCameraMode.Player)
                return;

            if (!playerCameraNavigationSynchronized)
            {
                Quaternion normalizedRotation = Quaternion.Euler(
                    Mathf.Clamp(Mathf.DeltaAngle(0f, gameCameraRotation.eulerAngles.x), -89.9f, 89.9f),
                    Mathf.DeltaAngle(0f, gameCameraRotation.eulerAngles.y),
                    0f);
                float actualCameraDistance = Vector3.Dot(
                    cameraNavigation.Focus - gameCameraPosition,
                    normalizedRotation * Vector3.forward);
                if (float.IsNaN(actualCameraDistance)
                    || float.IsInfinity(actualCameraDistance)
                    || actualCameraDistance <= 0f)
                    actualCameraDistance = cameraNavigation.ResolvePresentationCameraDistance(
                        rect, feel.VerticalFieldOfViewDegrees);
                if (!ESWorkbenchOrbitCameraBinding.TryCaptureExternalCameraAtDistance(
                        cameraNavigation,
                        gameCameraPosition,
                        gameCameraRotation,
                        actualCameraDistance,
                        rect,
                        feel.VerticalFieldOfViewDegrees))
                    return;
                playerCameraNavigationSynchronized = true;
            }

            ESWorkbenchOrbitCameraBinding.TryApplyToExternalCamera(
                cameraNavigation,
                rect,
                out gameCameraPosition,
                out gameCameraRotation,
                feel.VerticalFieldOfViewDegrees);
        }

        private void DrawOverlay(Rect rect)
        {
            string pointerStatus = lastPointerWorldPositionValid
                ? string.Format("坐标 {0:0.##}, {1:0.##}, {2:0.##}",
                    lastPointerWorldPosition.x,
                    lastPointerWorldPosition.y,
                    lastPointerWorldPosition.z)
                : readOnlyGameView ? "构图预览" : "未选择对象";
            ESWorkbenchViewportOverlay.DrawNavigationToolbar(
                rect,
                cameraNavigation,
                readOnlyGameView ? "游戏构图预览" : "世界三维作者视图",
                pointerStatus + " · " + ResolveViewportGizmoStatus()
                    + " · 碰撞 " + (readOnlyGameView ? "投影" : "作者配置")
                    + " · 导航 " + (readOnlyGameView ? "相机" : "轨道"),
                FrameAll,
                readOnlyGameView);

            if (!readOnlyGameView) return;

            float buttonTop = rect.y + ESWorkbenchViewportOverlay.HeaderHeight + 4f;
            float buttonWidth = Mathf.Clamp((rect.width - 32f) / 3f, 66f, 112f);
            DrawGameModeButton(
                new Rect(rect.x + 10f, buttonTop, buttonWidth, 21f),
                ESWorldGamePreviewCameraMode.Player, "玩家视角");
            DrawGameModeButton(
                new Rect(rect.x + 14f + buttonWidth, buttonTop, buttonWidth, 21f),
                ESWorldGamePreviewCameraMode.ThirdPerson, "第三人称");
            DrawGameModeButton(
                new Rect(rect.x + 18f + buttonWidth * 2f, buttonTop, buttonWidth, 21f),
                ESWorldGamePreviewCameraMode.Overview, "总览");

            string fidelitySummary = BuildPreviewFidelitySummary();
            Rect details = new Rect(rect.x + 10f, rect.yMax - 68f, Mathf.Min(rect.width - 20f, 520f), 38f);
            EditorGUI.DrawRect(details, new Color(0.02f, 0.025f, 0.03f, 0.72f));
            GUI.Label(
                new Rect(details.x + 8f, details.y + 3f, details.width - 16f, details.height - 6f),
                new GUIContent(fidelitySummary, fidelitySummary),
                EditorStyles.wordWrappedMiniLabel);
        }

        private string ResolveViewportGizmoStatus()
        {
            if (paintingTerrain) return "操控 笔刷";
            if (moving) return "操控 移动";
            if (rotating) return "操控 旋转";
            if (scaling) return "操控 缩放";
            return readOnlyGameView ? "操控 只读" : "操控 选择";
        }

        private void DrawGameModeButton(Rect rect, ESWorldGamePreviewCameraMode mode, string title)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = gameCameraMode == mode
                ? new Color(0.18f, 0.48f, 0.78f, 1f)
                : new Color(0.12f, 0.14f, 0.17f, 0.95f);
            if (GUI.Button(rect, title, EditorStyles.miniButton))
            {
                if (gameCameraMode != mode)
                {
                    gameCameraMode = mode;
                    if (layoutState != null) layoutState.previewCameraMode = mode.ToString();
                    ESWorldMapDefinition definition = draft?.Definition;
                    if (definition != null)
                    {
                        Vector2 min = definition.worldMin;
                        Vector2 max = definition.worldMax;
                        if (max.x <= min.x || max.y <= min.y)
                        {
                            min = Vector2.zero;
                            max = new Vector2(256f, 256f);
                        }
                        ApplyGameCameraDefaults(definition, min, max);
                        playerCameraNavigationSynchronized = false;
                        ConfigureGamePreviewCamera(definition);
                        // 相机模式同时改变可见内容合同（总览必须恢复被流式裁剪的远端放置物）。
                        // 仅更新相机不会重建 PreviewScene，导致之前已裁剪的对象无法回来。
                        Rebuild(false);
                    }
                    renderHost.MarkDirtyRepaint();
                }
            }
            GUI.backgroundColor = previous;
        }

        private void ApplyGameCameraDefaults(ESWorldMapDefinition definition, Vector2 min, Vector2 max)
        {
            Vector2 anchor = ResolveGameAnchor(definition, min, max);
            float u = Mathf.InverseLerp(min.x, max.x, anchor.x);
            float v = Mathf.InverseLerp(min.y, max.y, anchor.y);
            float ground = definition.heightfield == null
                ? 0f
                : ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                    * definition.terrainHeightScale;
            switch (gameCameraMode)
            {
                case ESWorldGamePreviewCameraMode.Player:
                    gameCameraPosition = new Vector3(anchor.x, ground + 1.7f, anchor.y);
                    gameCameraRotation = Quaternion.Euler(8f, 35f, 0f);
                    playerCameraNavigationSynchronized = false;
                    cameraNavigation.SetView(
                        gameCameraPosition + gameCameraRotation * Vector3.forward * 6f,
                        1f,
                        35f,
                        8f);
                    break;
                case ESWorldGamePreviewCameraMode.Overview:
                    cameraNavigation.SetView(
                        new Vector3((min.x + max.x) * 0.5f, definition.terrainHeightScale * 0.2f,
                            (min.y + max.y) * 0.5f),
                        Mathf.Max(80f, Mathf.Max(max.x - min.x, max.y - min.y) * 1.15f),
                        35f,
                        24f);
                    break;
                default:
                    cameraNavigation.SetView(
                        new Vector3(anchor.x, ground + 1.3f, anchor.y),
                        Mathf.Clamp(Mathf.Max(12f, definition.chunkSize * 0.35f), 12f, 80f),
                        35f,
                        18f);
                    break;
            }
        }

        private void ConfigureGamePreviewCamera(ESWorldMapDefinition definition)
        {
            if (!readOnlyGameView || preview?.Camera == null) return;
            float wetness = Mathf.Clamp01(definition?.waterWeather?.ambientWetness ?? 0f);
            Color clear = Color.Lerp(
                new Color(0.19f, 0.31f, 0.42f, 1f),
                new Color(0.08f, 0.12f, 0.16f, 1f),
                wetness);
            if (definition?.waterWeather?.weatherEnabled == false)
                clear = new Color(0.22f, 0.34f, 0.46f, 1f);
            preview.Camera.backgroundColor = clear;
            preview.Camera.allowHDR = true;
            preview.Camera.allowMSAA = true;
        }

        private static Vector2 ResolveGameAnchor(ESWorldMapDefinition definition, Vector2 min, Vector2 max)
        {
            if (definition?.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition poi = definition.pois[i];
                    if (poi != null) return poi.position;
                }
            return new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        }

        private bool ShouldIncludeGamePlacement(ESWorldMapDefinition definition, ESWorldMapPrefabPlacement placement)
        {
            if (!readOnlyGameView || definition?.streaming == null || !definition.streaming.enabled) return true;
            // 总览是作者验收地图完整性的视图，不能沿用玩家/第三人称的
            // 流式半径裁剪；否则即使点击“总览”，远端正式放置物仍会消失。
            if (gameCameraMode == ESWorldGamePreviewCameraMode.Overview) return true;
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            Vector2 anchor = ResolveGameAnchor(definition, min, max);
            float radius = Mathf.Max(1f, definition.streaming.chunkRadius + 0.5f)
                * Mathf.Max(1f, definition.chunkSize);
            Vector2 position = new Vector2(placement.position.x, placement.position.z);
            return (position - anchor).sqrMagnitude <= radius * radius;
        }

        private string BuildPreviewFidelitySummary()
        {
            if (!readOnlyGameView) return "作者态 · 预览资源按窗口会话释放";
            ESWorldMapDefinition definition = draft?.Definition;
            string weather = definition?.waterWeather == null
                ? "天气未配置"
                : definition.waterWeather.weatherEnabled
                    ? "天气投影 " + (string.IsNullOrWhiteSpace(definition.waterWeather.weatherProfileKey)
                        ? "default" : definition.waterWeather.weatherProfileKey)
                    : "天气关闭";
            string streaming = definition?.streaming != null && definition.streaming.enabled
                ? "流式裁剪 " + visiblePlacementCount + "/" + (visiblePlacementCount + culledPlacementCount)
                : "流式未启用";
            return "作者 PreviewScene 构图近似 · " + GameCameraModeDisplayName(gameCameraMode)
                + " · " + streaming
                + " · LOD 组 " + lodGroupCount
                + " · " + weather
                + " · 后处理未绑定正式 Volume"
                + " · 正式 Scene/运行时 Camera 未接管"
                + " · 碰撞/导航仅投影配置";
        }

        internal static string GameCameraModeDisplayName(ESWorldGamePreviewCameraMode mode)
        {
            switch (mode)
            {
                case ESWorldGamePreviewCameraMode.Player: return "玩家视角";
                case ESWorldGamePreviewCameraMode.Overview: return "总览";
                default: return "第三人称";
            }
        }

        private void ClearPreviewContent()
        {
            hover.Clear();
            ClearDropPreview();
            preview?.DestroyAllModelGroups();
            contentScope?.Dispose();
            contentScope = null;
            terrainObject = null;
            terrainPreviewHeightBuffer = null;
            terrainPreviewUpdateSchedule?.Pause();
            terrainPreviewUpdateSchedule = null;
            terrainPreviewCoalescer?.Cancel();
            lastTerrainPaintPointValid = false;
            lastTerrainPreviewHeightUpdate = 0d;
            previewObjects.Clear();
            previewStableIds.Clear();
            previewBounds.Clear();
            regionGuideVisuals.Clear();
            regionFillMaterial = null;
            regionOutlineMaterial = null;
            regionSelectedFillMaterial = null;
            regionSelectedOutlineMaterial = null;
            regionHoverFillMaterial = null;
            regionHoverOutlineMaterial = null;
            regionMaterials = null;
            regionSelectedMaterials = null;
            regionHoverMaterials = null;
            regionGuideTerrainRevision = 0;
            terrainData = null;
            playerCameraNavigationSynchronized = false;
            // 资产重建会使旧屏幕坐标立即失效；在非关闭路径通知底部状态投影，
            // 关闭路径避免在宿主释放贡献时重入刷新。
            bool hadPointerWorldPosition = lastPointerWorldPositionValid;
            lastPointerWorldPositionValid = false;
            if (hadPointerWorldPosition && !disposed)
                statusChanged?.Invoke();
        }

        private void CleanupPreview()
        {
            ClearPreviewContent();
            if (preview != null)
            {
                preview.Dispose();
                preview = null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            hover.Clear();
            edgePanSchedule?.Pause();
            edgePanSchedule = null;
            pendingRebuild?.Pause();
            pendingRebuild = null;
            CancelInteraction();
            CleanupPreview();
        }

        private static int ResolveTerrainResolution(int requested)
        {
            requested = Mathf.Clamp(requested, 33, 513);
            int exponent = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(requested - 1, 2f)), 5, 9);
            return (1 << exponent) + 1;
        }

    }
}
#endif
