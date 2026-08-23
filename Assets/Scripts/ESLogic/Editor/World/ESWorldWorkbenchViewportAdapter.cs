#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    internal sealed class ESWorldWorkbenchViewportAdapter : IESWorkbenchViewport, IESWorkbenchCancelableViewport, IESWorkbenchFrameableViewport,
        IESWorkbenchViewportStatusProvider, IESWorkbenchViewportDropDiagnostics, IESWorkbenchBatchViewport,
        IESWorkbenchDropPreviewViewport, IESWorkbenchEdgePannableViewport, IESWorkbenchNudgeableViewport,
        IESWorkbenchViewportDropPositionDiagnostics, IESWorkbenchViewportProjection
    {
        private readonly ESWorldBuilderWorkbenchWindow window;
        private readonly ESWorkbenchViewportContext viewportContext;
        private readonly ESWorkbenchViewportKind kind;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly ESWorldAuthoringViewport viewport3D;
        private readonly ESWorldMap2DViewportElement viewport2D;

        public ESWorldWorkbenchViewportAdapter(
            ESWorldBuilderWorkbenchWindow window,
            ESWorkbenchViewportContext context,
            ESWorkbenchViewportKind kind,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            viewportContext = context ?? throw new ArgumentNullException(nameof(context));
            this.kind = kind;
            this.feel = feel ?? context?.Feel ?? ESWorkbenchViewportFeelSettings.Standard;
            if (kind == ESWorkbenchViewportKind.Scene3D || kind == ESWorkbenchViewportKind.Game)
            {
                viewport3D = new ESWorldAuthoringViewport(
                    point => window.HandleAuthoringPoint(point, context.IsHierarchyVisible, context.IsHierarchyLocked),
                    context,
                    kind == ESWorkbenchViewportKind.Game,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.BeginTerrainStroke : null,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.EndTerrainStroke : null,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.CancelTerrainStroke : null,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.HandleTerrainBrushShortcut : null,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.GetTerrainBrushRadius : null,
                    kind == ESWorkbenchViewportKind.Scene3D ? window.GetTerrainBrushSummary : null,
                     context.StatusChanged,
                     this.feel);
                Root = viewport3D;
            }
            else
            {
                viewport2D = new ESWorldMap2DViewportElement(
                    point => window.HandleAuthoringPoint(point, context.IsHierarchyVisible, context.IsHierarchyLocked),
                    context.Actions,
                    context.Selection,
                    context.Layout,
                    context.SnapPosition,
                    context.IsHierarchyVisible,
                    context.IsHierarchyLocked,
                    window.BeginTerrainStroke,
                    window.EndTerrainStroke,
                    window.CancelTerrainStroke,
                    window.HandleTerrainBrushShortcut,
                    window.GetTerrainBrushRadius,
                    window.GetTerrainBrushSummary,
                    this.feel,
                    context.PointerCoordinator,
                    context.StatusChanged);
                Root = viewport2D;
            }
            Root.style.flexGrow = 1f;
            Root.style.minWidth = 0f;
            Root.style.minHeight = 0f;
            Refresh(ESWorkbenchRefreshReason.Initial);
        }

        public VisualElement Root { get; }
        public void Activate() => Refresh(ESWorkbenchRefreshReason.Explicit);
        public void Deactivate()
        {
            ClearDropPreview();
            viewport3D?.CancelInteraction();
            viewport2D?.CancelInteraction();
        }
        public void CancelInteraction() => Deactivate();

        public void FrameAll()
        {
            if (kind == ESWorkbenchViewportKind.Scene3D || kind == ESWorkbenchViewportKind.Game) viewport3D?.FrameAll();
            else viewport2D?.FrameAll();
        }

        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            ESWorldMapAsset draft = window.ESWorld_Draft;
            if (kind == ESWorkbenchViewportKind.Scene3D || kind == ESWorkbenchViewportKind.Game)
            {
                if (reason == ESWorkbenchRefreshReason.SelectionChanged) return;
                viewport3D?.Bind(draft, reason == ESWorkbenchRefreshReason.Initial || reason == ESWorkbenchRefreshReason.AssetChanged);
            }
            else viewport2D?.Bind(draft, reason);
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item) => CanAccept(item, out _);

        public bool TryResolveDropPosition(
            ESWorkbenchObjectDescriptor item,
            Vector2 localPosition,
            out Vector3 worldPosition,
            out string reason)
        {
            worldPosition = default;
            reason = string.Empty;
            if (kind == ESWorkbenchViewportKind.Game)
            {
                reason = "游戏构图预览是只读视图。";
                return false;
            }
            if (item == null)
            {
                reason = "没有可用的拖放内容。";
                return false;
            }
            ESWorkbenchViewportProjectionRequest request =
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.DropPreview,
                    item.DragMode == ESWorkbenchContentDragMode.ActivateTool);
            bool resolved = kind == ESWorkbenchViewportKind.Scene3D
                ? viewport3D != null && viewport3D.TryResolveProjection(
                    localPosition, request, out worldPosition)
                : viewport2D != null && viewport2D.TryResolveProjection(
                    localPosition, request, out worldPosition);
            if (!resolved) reason = "拖放位置不在当前世界作者交互区域内。";
            return resolved;
        }

        public bool TryResolveProjection(
            Vector2 localPosition,
            ESWorkbenchViewportProjectionRequest request,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (kind == ESWorkbenchViewportKind.Game) return false;
            return kind == ESWorkbenchViewportKind.Scene3D
                ? viewport3D != null && viewport3D.TryResolveProjection(
                    localPosition, request, out worldPosition)
                : viewport2D != null && viewport2D.TryResolveProjection(
                    localPosition, request, out worldPosition);
        }

        private bool TryResolveAuthoringDropPoint(
            Vector2 localPosition,
            bool requireTerrainSurface,
            out Vector3 worldPosition)
        {
            return TryResolveProjection(
                localPosition,
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.DropPreview,
                    requireTerrainSurface),
                out worldPosition);
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item, out string reason)
        {
            if (kind == ESWorkbenchViewportKind.Game)
            {
                reason = "游戏构图预览是只读视图，请切换到 2D 地图或 3D 世界后再拖放。";
                return false;
            }
            return window.CanUsePaletteItem(item, out reason);
        }

        public bool TryAccept(ESWorkbenchDropContext context, out string message)
        {
            message = string.Empty;
            if (kind == ESWorkbenchViewportKind.Game)
            {
                message = "游戏构图预览是只读 PreviewScene 构图，请在 2D 地图或 3D 世界视口中放置对象。";
                return false;
            }
            if (context?.Item == null || !CanAccept(context.Item, out message))
            {
                if (string.IsNullOrWhiteSpace(message)) message = "当前世界视口不能使用该内容。";
                return false;
            }
            if (viewportContext.IsHierarchyLocked("world.map"))
            {
                message = "世界根节点已锁定，不能创建新的放置对象。";
                return false;
            }
            ESWorldMapDefinition definition = window.ESWorld_Draft?.Definition;
            if (definition == null) { message = "当前世界草稿无效。"; return false; }
            Vector3 position3D = default;
            bool resolved = TryResolveAuthoringDropPoint(
                context.LocalPosition,
                context.Item.DragMode == ESWorkbenchContentDragMode.ActivateTool,
                out position3D);
            if (!resolved)
            {
                message = "拖放位置不在当前世界作者画布内。";
                return false;
            }
            Vector3 position = viewportContext.SnapPosition(position3D);
            if (!window.TryUsePaletteItem(context.Item, position, out message)) return false;
            Refresh(ESWorkbenchRefreshReason.DataChanged);
            return true;
        }

        public bool CanAcceptBatch(
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            out string reason)
        {
            reason = string.Empty;
            if (kind == ESWorkbenchViewportKind.Game)
            {
                reason = "游戏构图预览是只读视图，不能批量放置内容。";
                return false;
            }
            if (items == null || items.Count <= 1)
            {
                reason = "批量放置至少需要两项内容。";
                return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                ESWorkbenchObjectDescriptor item = items[i];
                if (item?.DragMode != ESWorkbenchContentDragMode.Place)
                {
                    reason = "批量放置当前只接受已注册 Prefab；笔刷、区域和场景模板仍需单独应用。";
                    return false;
                }
                if (!window.CanUsePaletteItem(item, out reason))
                {
                    reason = "第 " + (i + 1) + " 项不可用：" + reason;
                    return false;
                }
            }
            ESWorldMapDefinition definition = window.ESWorld_Draft?.Definition;
            int limit = definition?.ugcLimits?.maxPrefabInstances ?? 0;
            int existing = definition?.prefabPlacements?.Count ?? 0;
            if (limit > 0 && existing + items.Count > limit)
            {
                reason = "批量放置将超过 Prefab 配额：当前 " + existing
                    + "，计划新增 " + items.Count + "，上限 " + limit + "。";
                return false;
            }
            return true;
        }

        public bool TryAcceptBatch(ESWorkbenchBatchDropContext context, out string message)
        {
            message = string.Empty;
            if (context == null || !CanAcceptBatch(context.Items, out message)) return false;
            if (viewportContext.IsHierarchyLocked("world.map"))
            {
                message = "世界根节点已锁定，不能批量放置内容。";
                return false;
            }
            Vector3 anchor = default;
            bool resolved = TryResolveAuthoringDropPoint(
                context.LocalPosition, false, out anchor);
            if (!resolved)
            {
                message = "批量拖放位置不在当前世界作者画布内。";
                return false;
            }

            var positions = new List<Vector3>(context.Items.Count);
            ESWorkbenchDropLayout.FillGridPositions(
                anchor,
                context.Items.Count,
                context.Spacing,
                viewportContext.SnapPosition,
                positions,
                feel.MinimumDropSpacing);
            var requests = new List<ESWorkbenchCreateRequest>(context.Items.Count);
            for (int i = 0; i < context.Items.Count; i++)
                requests.Add(new ESWorkbenchCreateRequest(context.Items[i], positions[i]));
            if (!context.Actions.Authoring.CanCreateBatch(requests, out message)) return false;
            if (!context.Actions.Authoring.TryCreateBatch(requests, out message)) return false;
            Refresh(ESWorkbenchRefreshReason.DataChanged);
            return true;
        }

        public IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetStatusSnapshot()
        {
            ESWorkbenchViewportStatusDescriptor[] baseStatuses =
                window.GetViewportStatusSnapshot(kind)?.ToArray()
                ?? Array.Empty<ESWorkbenchViewportStatusDescriptor>();
            var additions = new List<ESWorkbenchViewportStatusDescriptor>();
            Vector3 pointer = default;
            bool hasPointer = viewport3D != null
                ? viewport3D.TryGetPointerWorldPosition(out pointer)
                : viewport2D != null && viewport2D.TryGetPointerWorldPosition(out pointer);
            if (hasPointer)
            {
                additions.Add(new ESWorkbenchViewportStatusDescriptor(
                    "world.pointer-coordinate",
                    "指针",
                    pointer.x.ToString("0.##") + ", "
                        + pointer.y.ToString("0.##") + ", "
                        + pointer.z.ToString("0.##"),
                    "鼠标当前落点的世界坐标；移出视口后清除",
                    450));
            }
            if (kind == ESWorkbenchViewportKind.Game && viewport3D != null)
                additions.Add(new ESWorkbenchViewportStatusDescriptor(
                    "world.preview-fidelity",
                    "构图",
                    "构图近似 · 非 Unity Game View",
                    viewport3D.PreviewFidelitySummary,
                    -50));
            else if (kind != ESWorkbenchViewportKind.Game)
            {
                string toolId = viewportContext.Actions?.Tools?.ActiveToolId ?? string.Empty;
                string guide = ResolveInteractionGuide(toolId);
                if (!string.IsNullOrWhiteSpace(guide))
                    additions.Add(new ESWorkbenchViewportStatusDescriptor(
                        "world.interaction-guide",
                        "操作提示",
                        guide,
                        "当前工具的直接操作、提交和恢复语义",
                        440));
            }
            return baseStatuses.Concat(additions).ToArray();
        }

        private string ResolveInteractionGuide(string toolId)
        {
            switch (toolId)
            {
                case "world.terrain":
                    return window.GetTerrainBrushSummary()
                        + " · 左键连续绘制 · [ / ] 半径 · Shift+[ / ] 强度 · Esc 取消整笔 · 一次 Undo";
                case "world.region":
                    return "点击地图创建区域 · 可在 Inspector 精确调整尺寸 · Esc 取消当前操作";
                case "world.poi":
                    return "点击地图创建 POI · 选择后在 Inspector 修改位置和属性";
                case "world.prefab":
                    return "未选中对象也可从内容库直接拖入 · 释放前显示目标框";
                default:
                    return "左键选择 · 右键旋转 · 中键平移 · 滚轮缩放 · Esc 取消预览操作";
            }
        }

        public void UpdateDropPreview(ESWorkbenchDropPreviewContext context)
        {
            if (context == null || context.PrimaryItem == null || kind == ESWorkbenchViewportKind.Game)
            {
                ClearDropPreview();
                return;
            }
            Vector3 previewSize = window.ResolvePalettePreviewSize(context.PrimaryItem);
            if (kind == ESWorkbenchViewportKind.Scene3D)
            {
                if (context.HasResolvedWorldPosition)
                    viewport3D?.UpdateDropPreview(
                        context.PrimaryItem,
                        context.Items,
                        context.LocalPosition,
                        context.Spacing,
                        previewSize,
                        context.ResolvedWorldPosition,
                        true,
                        context.State);
                else
                    viewport3D?.UpdateDropPreview(
                        context.PrimaryItem,
                        context.Items,
                        context.LocalPosition,
                        context.Spacing,
                        previewSize,
                        context.State);
            }
            else
            {
                if (context.HasResolvedWorldPosition)
                    viewport2D?.UpdateDropPreview(
                        context.PrimaryItem,
                        context.Items,
                        context.LocalPosition,
                        context.Spacing,
                        previewSize,
                        context.ResolvedWorldPosition,
                        true,
                        context.State);
                else
                    viewport2D?.UpdateDropPreview(
                        context.PrimaryItem,
                        context.Items,
                        context.LocalPosition,
                        context.Spacing,
                        previewSize,
                        context.State);
            }
        }

        public void ClearDropPreview()
        {
            viewport3D?.ClearDropPreview();
            viewport2D?.ClearDropPreview();
        }

        public bool TryEdgePan(Vector2 localPosition, float deltaTime)
        {
            if (kind == ESWorkbenchViewportKind.Game) return false;
            return viewport2D?.TryEdgePan(localPosition, deltaTime) == true
                || viewport3D?.TryEdgePan(localPosition, deltaTime) == true;
        }

        public bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message)
        {
            message = string.Empty;
            return viewport2D?.TryNudge(keyCode, shift, controlOrCommand, out message) == true
                || viewport3D?.TryNudge(keyCode, shift, controlOrCommand, out message) == true;
        }

        public void Dispose()
        {
            ClearDropPreview();
            viewport3D?.Dispose();
            viewport2D?.Dispose();
        }
    }

    internal sealed class ESWorldMap2DViewportElement : VisualElement, IDisposable, IESWorkbenchNudgeableViewport,
        IESWorkbenchViewportProjection
    {
        private readonly Action<Vector3> worldClick;
        private readonly ESWorkbenchActionContext actions;
        private readonly ESWorkbenchSelectionService selection;
        private readonly ESWorkbenchCanvasNavigationState navigation;
        private readonly ESWorkbenchViewportLayoutState viewportLayout;
        private readonly ESWorkbenchEdgePanController edgePan;
        private readonly ESWorkbenchHoverState hover = new ESWorkbenchHoverState();
        private readonly Func<Vector3, Vector3> snapPosition;
        private readonly Func<string, bool> isHierarchyVisible;
        private readonly Func<string, bool> isHierarchyLocked;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly ESWorkbenchPointerInteractionCoordinator pointerCoordinator;
        private readonly ESWorkbenchEdgePanSession edgePanSession =
            new ESWorkbenchEdgePanSession();
        private readonly ESWorkbenchSelectionCache hitSelectionCache =
            new ESWorkbenchSelectionCache();
        private readonly Action terrainStrokeBegin;
        private readonly Action terrainStrokeEnd;
        private readonly Action terrainStrokeCancel;
        private readonly Func<KeyCode, EventModifiers, bool> terrainBrushShortcut;
        private readonly Func<float> terrainBrushRadius;
        private readonly Func<string> terrainBrushSummary;
        private readonly Action statusChanged;
        private readonly VisualElement labelOverlay;
        private readonly Dictionary<string, Label> regionLabels = new Dictionary<string, Label>(StringComparer.Ordinal);
        private ESWorldMapAsset draft;
        private bool panning;
        private int panPointerId = -1;
        private IVisualElementScheduledItem edgePanSchedule;
        private bool moving;
        private int movePointerId = -1;
        private Vector3 pendingMoveWorld;
        private Vector3 moveOriginWorld;
        private bool pendingMoveValid;
        private ESWorkbenchSelection movingSelection;
        private readonly ESWorkbenchMoveGestureAnchor moveAnchor;
        private bool paintingTerrain;
        private readonly ESWorkbenchStrokeSampler terrainStrokeSampler = new ESWorkbenchStrokeSampler();
        private int terrainPointerId = -1;
        private readonly ESWorkbenchPointerGestureSession gestureSession;
        private Vector2 terrainPointerPosition;
        private bool terrainPointerValid;
        private Vector3 lastPointerWorldPosition;
        private bool lastPointerWorldPositionValid;
        private ESWorkbenchObjectDescriptor dropPreviewItem;
        private readonly List<Vector3> dropPreviewPositions = new List<Vector3>();
        private Vector3 dropPreviewSize = Vector3.one;
        private Vector3 lastDropPreviewAnchor;
        private int lastDropPreviewCount = -1;
        private float lastDropPreviewSpacing;
        private bool lastDropPreviewSnapEnabled;
        private float lastDropPreviewSnapStep;
        private bool lastDropPreviewAnchorValid;
        private ESWorkbenchDropPreviewState dropPreviewState =
            ESWorkbenchDropPreviewState.Allowed;

        public ESWorldMap2DViewportElement(
            Action<Vector3> worldClick,
            ESWorkbenchActionContext actions,
            ESWorkbenchSelectionService selection,
            ESWorkbenchViewportLayoutState layout)
            : this(worldClick, actions, selection, layout, null)
        {
        }

        public ESWorldMap2DViewportElement(
            Action<Vector3> worldClick,
            ESWorkbenchActionContext actions,
            ESWorkbenchSelectionService selection,
            ESWorkbenchViewportLayoutState layout,
            Func<Vector3, Vector3> snapPosition,
            Func<string, bool> isHierarchyVisible = null,
            Func<string, bool> isHierarchyLocked = null,
            Action terrainStrokeBegin = null,
            Action terrainStrokeEnd = null,
            Action terrainStrokeCancel = null,
            Func<KeyCode, EventModifiers, bool> terrainBrushShortcut = null,
            Func<float> terrainBrushRadius = null,
            Func<string> terrainBrushSummary = null,
            ESWorkbenchViewportFeelSettings feel = null,
            ESWorkbenchPointerInteractionCoordinator pointerCoordinator = null,
            Action statusChanged = null)
        {
            name = "ESWorldMap2DViewport";
            this.worldClick = worldClick;
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.selection = selection;
            this.snapPosition = snapPosition ?? (value => value);
            this.isHierarchyVisible = isHierarchyVisible ?? (_ => true);
            this.isHierarchyLocked = isHierarchyLocked ?? (_ => false);
            this.terrainStrokeBegin = terrainStrokeBegin;
            this.terrainStrokeEnd = terrainStrokeEnd;
            this.terrainStrokeCancel = terrainStrokeCancel;
            this.terrainBrushShortcut = terrainBrushShortcut;
            this.terrainBrushRadius = terrainBrushRadius;
            this.terrainBrushSummary = terrainBrushSummary;
            this.statusChanged = statusChanged;
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            this.viewportLayout = layout ?? new ESWorkbenchViewportLayoutState { viewportId = "world.canvas-2d" };
            this.pointerCoordinator = pointerCoordinator ?? new ESWorkbenchPointerInteractionCoordinator();
            gestureSession = new ESWorkbenchPointerGestureSession(
                this.feel.DragStartPixels, this.feel);
            moveAnchor = new ESWorkbenchMoveGestureAnchor();
            navigation = new ESWorkbenchCanvasNavigationState(
                this.viewportLayout,
                this.feel.CanvasMinimumZoom,
                this.feel.CanvasMaximumZoom,
                this.feel.CanvasViewportPaddingPixels,
                this.feel);
            edgePan = new ESWorkbenchEdgePanController(this.feel.EdgePanSettings);
            style.flexGrow = 1f;
            style.minWidth = 0f;
            style.minHeight = 240f;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = ESWorldMapEditorPresentation.TerrainBase;
            focusable = true;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<FocusOutEvent>(OnFocusOut);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            edgePanSchedule = schedule.Execute(ApplyEdgePan).Every(16);
            edgePanSchedule.Pause();
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UpdateRegionLabelPositions();
                MarkDirtyRepaint();
            });
            labelOverlay = new VisualElement { name = "ESWorldMap2DLabels", pickingMode = PickingMode.Ignore };
            labelOverlay.style.position = Position.Absolute;
            labelOverlay.style.left = 0f;
            labelOverlay.style.right = 0f;
            labelOverlay.style.top = 0f;
            labelOverlay.style.bottom = 0f;
            Add(labelOverlay);
        }

        public void Bind(ESWorldMapAsset asset, ESWorkbenchRefreshReason reason)
        {
            bool assetChanged = draft != asset;
            if (assetChanged || reason != ESWorkbenchRefreshReason.SelectionChanged)
                hitSelectionCache.Clear();
            if (assetChanged || reason != ESWorkbenchRefreshReason.SelectionChanged)
            {
                hover.Clear();
                ClearPointerWorldStatus();
            }
            draft = asset;
            if (assetChanged || reason == ESWorkbenchRefreshReason.Initial
                || reason == ESWorkbenchRefreshReason.AssetChanged
                || reason == ESWorkbenchRefreshReason.DataChanged
                || reason == ESWorkbenchRefreshReason.UndoRedo
                || reason == ESWorkbenchRefreshReason.Explicit)
                SynchronizeRegionLabels();
            else UpdateRegionLabelPositions();
            MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            navigation.Reset();
            UpdateRegionLabelPositions();
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect viewport = contentRect;
            if (viewport.width <= 1f || viewport.height <= 1f) return;
            Painter2D painter = context.painter2D;
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null)
            {
                DrawCrosshair(painter, viewport.center, 18f, ESWorldMapEditorPresentation.Grid);
                return;
            }
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = new Vector2(256f, 256f); }
            Rect mapRect = ResolveMapRect(viewport, min, max);
            DrawHeightfield(painter, definition, mapRect);
            DrawGrid(painter, definition, mapRect);
            DrawRegions(painter, definition, min, max, mapRect);
            DrawPois(painter, definition, min, max, mapRect);
            DrawPlacements(painter, definition, min, max, mapRect);
            DrawMapBorder(painter, mapRect);
            if (moving && pendingMoveValid)
                DrawMoveTarget(painter, definition, min, max, mapRect);
            DrawDropPreview(painter, min, max, mapRect);
            // 笔刷光标是当前输入状态的直接反馈，必须位于内容层之上，
            // 否则区域填充会把光标盖住而造成“笔刷失效”的错觉。
            DrawTerrainBrushGuide(painter, definition, min, max, mapRect);
        }

        private static void DrawHeightfield(Painter2D painter, ESWorldMapDefinition definition, Rect rect)
        {
            ESWorldMapHeightfield field = definition.heightfield;
            if (field == null || field.width < 2 || field.height < 2) return;
            int width = Mathf.Min(48, field.width - 1);
            int height = Mathf.Min(48, field.height - 1);
            float cellWidth = rect.width / width;
            float cellHeight = rect.height / height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.RoundToInt(x / (float)width * (field.width - 1));
                    int sy = Mathf.RoundToInt(y / (float)height * (field.height - 1));
                    float value = ESWorldHeightfieldReadOnly.Get(field, sx, sy);
                    Color color = Color.Lerp(ESWorldMapEditorPresentation.HeightLow, ESWorldMapEditorPresentation.HeightHigh, value);
                    color.a = 0.82f;
                    DrawFilledRect(painter, new Rect(rect.x + x * cellWidth, rect.yMax - (y + 1) * cellHeight,
                        cellWidth + 0.5f, cellHeight + 0.5f), color);
                }
        }

        private static void DrawGrid(Painter2D painter, ESWorldMapDefinition definition, Rect rect)
        {
            int columns = Mathf.Clamp(definition.spaceTemplate?.gridWidth ?? 16, 1, 128);
            int rows = Mathf.Clamp(definition.spaceTemplate?.gridHeight ?? 16, 1, 128);
            painter.strokeColor = ESWorldMapEditorPresentation.Grid;
            painter.lineWidth = 1f;
            painter.BeginPath();
            for (int x = 0; x <= columns; x++)
            {
                float px = Mathf.Lerp(rect.xMin, rect.xMax, x / (float)columns);
                painter.MoveTo(new Vector2(px, rect.yMin));
                painter.LineTo(new Vector2(px, rect.yMax));
            }
            for (int y = 0; y <= rows; y++)
            {
                float py = Mathf.Lerp(rect.yMin, rect.yMax, y / (float)rows);
                painter.MoveTo(new Vector2(rect.xMin, py));
                painter.LineTo(new Vector2(rect.xMax, py));
            }
            painter.Stroke();
        }

        private void DrawTerrainBrushGuide(
            Painter2D painter,
            ESWorldMapDefinition definition,
            Vector2 min,
            Vector2 max,
            Rect rect)
        {
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || !IsTerrainPaintingInteraction() || moving || !terrainPointerValid
                || !TryResolveTerrainPoint(terrainPointerPosition, out Vector3 worldPoint)) return;
            float radiusWorld = Mathf.Max(0.5f, terrainBrushRadius?.Invoke() ?? 8f);
            Vector2 center = WorldToCanvas(new Vector2(worldPoint.x, worldPoint.z), min, max, rect);
            float radiusPixels = Mathf.Max(2f, radiusWorld / Mathf.Max(0.001f, max.x - min.x) * rect.width);
            painter.strokeColor = new Color(0.18f, 0.86f, 1f, 0.96f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.Arc(center, radiusPixels, 0f, 360f);
            painter.Stroke();
            float cross = Mathf.Clamp(radiusPixels * 0.18f, 4f, 12f);
            painter.BeginPath();
            painter.MoveTo(center - Vector2.right * cross);
            painter.LineTo(center + Vector2.right * cross);
            painter.MoveTo(center - Vector2.up * cross);
            painter.LineTo(center + Vector2.up * cross);
            painter.Stroke();
        }

        private void DrawRegions(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.regions == null) return;
            for (int i = 0; i < definition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                ESWorkbenchSelection cachedSelection = region == null
                    ? ESWorkbenchSelection.Empty
                    : hitSelectionCache.GetOrCreateLocal(
                        "world.region", region.regionId, "world.region.", payload: region.regionId);
                string stableId = cachedSelection.StableId;
                if (region == null || !isHierarchyVisible(stableId)) continue;
                Vector2 a = WorldToCanvas(region.min, min, max, rect);
                Vector2 b = WorldToCanvas(region.max, min, max, rect);
                Rect regionRect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                Color color = ESWorldMapEditorPresentation.Region;
                bool selected = IsSelected(stableId);
                bool hovered = !selected && hover.IsHovered(stableId);
                if (selected) color.a = Mathf.Min(0.78f, color.a + 0.28f);
                else if (hovered) color.a = Mathf.Min(0.62f, color.a + 0.18f);
                DrawFilledRect(painter, regionRect, color);
                if (selected) DrawOutline(painter, regionRect, ESWorldMapEditorPresentation.Selection, 2.5f);
                else if (hovered) DrawOutline(painter, regionRect, ESWorldMapEditorPresentation.Selection, 1.5f);
            }
        }

        private void DrawPois(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.pois == null) return;
            for (int i = 0; i < definition.pois.Count; i++)
            {
                ESWorldMapPoiDefinition poi = definition.pois[i];
                ESWorkbenchSelection cachedSelection = poi == null
                    ? ESWorkbenchSelection.Empty
                    : hitSelectionCache.GetOrCreateLocal(
                        "world.poi", poi.poiId, "world.poi.", payload: poi.poiId);
                string stableId = cachedSelection.StableId;
                if (poi == null || !isHierarchyVisible(stableId)) continue;
                Vector2 point = WorldToCanvas(poi.position, min, max, rect);
                bool selected = IsSelected(stableId);
                bool hovered = !selected && hover.IsHovered(stableId);
                painter.fillColor = selected || hovered
                    ? ESWorldMapEditorPresentation.Selection : ESWorldMapEditorPresentation.Poi;
                float radius = feel.ResolveMarkerRadiusPixels(selected, hovered);
                painter.BeginPath();
                painter.Arc(point, radius, 0f, 360f);
                painter.Fill();
            }
        }

        private void DrawPlacements(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.prefabPlacements == null) return;
            for (int i = 0; i < definition.prefabPlacements.Count; i++)
            {
                ESWorldMapPrefabPlacement placement = definition.prefabPlacements[i];
                ESWorkbenchSelection cachedSelection = placement == null
                    ? ESWorkbenchSelection.Empty
                    : hitSelectionCache.GetOrCreateLocal(
                        "world.prefab", placement.placementId, "world.prefab.", payload: placement.placementId);
                string stableId = cachedSelection.StableId;
                if (placement == null || !placement.enabled || !isHierarchyVisible(stableId)) continue;
                Vector2 point = WorldToCanvas(new Vector2(placement.position.x, placement.position.z), min, max, rect);
                bool selected = IsSelected(stableId);
                bool hovered = !selected && hover.IsHovered(stableId);
                float radius = feel.ResolveMarkerRadiusPixels(selected, hovered);
                painter.strokeColor = selected || hovered
                    ? ESWorldMapEditorPresentation.Selection : ESWorldMapEditorPresentation.Poi;
                painter.lineWidth = selected ? 3f : hovered ? 2.5f : 2f;
                painter.BeginPath();
                painter.Arc(point, radius, 0f, 360f);
                painter.Stroke();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (hover.Clear()) MarkDirtyRepaint();
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || pointerCoordinator.IsExternalContentActive)
            {
                // 外部资源拖放一旦接管主指针，当前帧即终止本地手势。
                // 不能依赖后续 PointerMove 才清理，否则在拖放起始帧仍可能
                // 让笔刷或对象移动写入一次错误尾点。
                if (gestureSession.IsActive || panning || moving || paintingTerrain)
                    CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            Focus();
            Vector2 local = this.WorldToLocal(evt.position);
            UpdatePointerWorldStatus(local);
            if (gestureSession.IsActive)
            {
                if (!pointerCoordinator.Owns(
                        this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport))
                    CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                if (!pointerCoordinator.TryAcquire(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!gestureSession.TryArm(ESWorkbenchPointerGestureSession.Kind.Pan, evt.pointerId, local))
                {
                    pointerCoordinator.Release(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    evt.StopImmediatePropagation();
                    return;
                }
                panning = true;
                panPointerId = evt.pointerId;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            if (!TryResolveWorldPoint(local, out Vector3 point))
            {
                evt.StopImmediatePropagation();
                return;
            }
            ESWorkbenchToolCapabilities toolCapabilities = actions.Tools.ActiveCapabilities;
            ESWorkbenchSelection hitSelection = TryHitWorldItem(
                point,
                contentRect,
                ResolveWorldBounds());
            ESWorkbenchToolCapabilities targetCapabilities = hitSelection == null || hitSelection.IsEmpty
                ? ESWorkbenchToolCapabilities.Select
                : ESWorkbenchToolCapabilityResolver.ResolveTarget(
                    actions.Authoring.CanMove(hitSelection),
                    actions.Authoring.CanRotate(hitSelection),
                    actions.Authoring.CanScale(hitSelection));
            ESWorkbenchPointerIntentDecision intentDecision = ESWorkbenchPointerIntentResolver.ResolveDecision(
                new ESWorkbenchPointerIntentContext(
                externalContentDragActive: ESWorkbenchUIToolkitHost.IsExternalContentDragActive,
                navigationGestureActive: gestureSession.IsActive,
                toolCapabilities: toolCapabilities,
                viewportCapabilities: ESWorkbenchToolCapabilities.Select
                    | ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.Paint
                    | ESWorkbenchToolCapabilities.GroundAction,
                targetCapabilities: targetCapabilities,
                hasHitTarget: hitSelection != null && !hitSelection.IsEmpty,
                hierarchyLocked: hitSelection != null && !hitSelection.IsEmpty
                    && isHierarchyLocked(hitSelection.StableId),
                hitKind: ResolvePointerHitKind(hitSelection)));
            if (!intentDecision.CanStart)
            {
                evt.StopPropagation();
                return;
            }
            ESWorkbenchPointerIntentKind intent = intentDecision.Intent;
            // 地形笔刷与对象变换是互斥工具。笔刷只在没有精确可操作目标时
            // 拥有地面主意图；命中区域、POI 或 Prefab 时沿用统一目标仲裁。
            if (hitSelection != null && !hitSelection.IsEmpty
                && (intent == ESWorkbenchPointerIntentKind.Select
                    || intent == ESWorkbenchPointerIntentKind.Manipulate))
            {
                selection.Select(hitSelection);
                if (intent == ESWorkbenchPointerIntentKind.Manipulate)
                {
                    if (!pointerCoordinator.TryAcquire(
                            this,
                            evt.pointerId,
                            ESWorkbenchPointerOwnerKind.Viewport))
                    {
                        evt.StopImmediatePropagation();
                        return;
                    }
                    if (!gestureSession.TryArm(ESWorkbenchPointerGestureSession.Kind.Move, evt.pointerId, local))
                    {
                        pointerCoordinator.Release(
                            this,
                            evt.pointerId,
                            ESWorkbenchPointerOwnerKind.Viewport);
                        evt.StopImmediatePropagation();
                        return;
                    }
                    moving = true;
                    movePointerId = evt.pointerId;
                    movingSelection = actions.Selection.Current;
                    moveOriginWorld = ResolveSelectionWorldPosition(movingSelection, point);
                    Vector3 pointerStart = new Vector3(point.x, moveOriginWorld.y, point.z);
                    if (!moveAnchor.Capture(moveOriginWorld, pointerStart))
                    {
                        StopMoving();
                        gestureSession.Cancel();
                        pointerCoordinator.Release(
                            this,
                            evt.pointerId,
                            ESWorkbenchPointerOwnerKind.Viewport);
                        evt.StopImmediatePropagation();
                        return;
                    }
                    pendingMoveWorld = moveOriginWorld;
                    pendingMoveValid = false;
                    this.CapturePointer(evt.pointerId);
                }
            }
            else if (intent == ESWorkbenchPointerIntentKind.Paint)
            {
                if (!TryResolveTerrainPoint(local, out point))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!pointerCoordinator.TryAcquire(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!gestureSession.TryArm(ESWorkbenchPointerGestureSession.Kind.Paint, evt.pointerId, local))
                {
                    pointerCoordinator.Release(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    evt.StopImmediatePropagation();
                    return;
                }
                terrainStrokeBegin?.Invoke();
                paintingTerrain = true;
                terrainStrokeSampler.Reset();
                terrainPointerId = evt.pointerId;
                terrainPointerPosition = local;
                terrainPointerValid = true;
                tooltip = terrainBrushSummary?.Invoke() ?? "按住左键连续绘制地形。";
                this.CapturePointer(evt.pointerId);
                SampleTerrainStroke(point);
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }
            else if (intent == ESWorkbenchPointerIntentKind.Select)
            {
                if (hitSelection == null || hitSelection.IsEmpty)
                {
                    selection.Clear();
                    evt.StopPropagation();
                    return;
                }
                selection.Select(hitSelection);
            }
            else if (intent == ESWorkbenchPointerIntentKind.GroundAction)
            {
                worldClick?.Invoke(point);
            }
            evt.StopPropagation();
        }

        private ESWorkbenchSelection TryHitWorldItem(
            Vector3 point,
            Rect viewport,
            Rect worldBounds)
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null) return ESWorkbenchSelection.Empty;
            Vector2 point2D = new Vector2(point.x, point.z);
            float threshold = navigation.ResolveWorldRadiusForPixels(
                viewport, worldBounds, feel.SelectionHitRadiusPixels);
            float best = float.MaxValue;
            ESWorkbenchSelection result = ESWorkbenchSelection.Empty;
            if (definition.prefabPlacements != null)
                for (int i = 0; i < definition.prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement item = definition.prefabPlacements[i];
                    ESWorkbenchSelection cachedSelection = item == null
                        ? ESWorkbenchSelection.Empty
                        : hitSelectionCache.GetOrCreateLocal(
                            "world.prefab", item.placementId, "world.prefab.", payload: item.placementId);
                    string stableId = cachedSelection.IsEmpty
                        ? string.Empty
                        : cachedSelection.StableId;
                    if (item == null || !item.enabled || !isHierarchyVisible(stableId)) continue;
                    float distance = Vector2.Distance(point2D, new Vector2(item.position.x, item.position.z));
                    if (distance <= threshold && distance < best)
                    {
                        best = distance;
                        result = cachedSelection;
                    }
                }
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition item = definition.pois[i];
                    ESWorkbenchSelection cachedSelection = item == null
                        ? ESWorkbenchSelection.Empty
                        : hitSelectionCache.GetOrCreateLocal(
                            "world.poi", item.poiId, "world.poi.", payload: item.poiId);
                    string stableId = cachedSelection.IsEmpty
                        ? string.Empty
                        : cachedSelection.StableId;
                    if (item == null || !isHierarchyVisible(stableId)) continue;
                    float distance = Vector2.Distance(point2D, item.position);
                    if (distance <= threshold && distance < best)
                    {
                        best = distance;
                        result = cachedSelection;
                    }
                }
            // 区域是空间容器，不应覆盖已经命中的可直接操作对象。
            // 只有没有预制件/POI 精确命中时，区域才作为背景命中兜底。
            if (result != null && !result.IsEmpty)
                return ESWorkbenchSpatialHitResolver.PreferPrecise(result, ESWorkbenchSelection.Empty);
            ESWorkbenchSelection areaHit = ESWorkbenchSelection.Empty;
            if (definition.regions != null)
                for (int i = definition.regions.Count - 1; i >= 0; i--)
                {
                    ESWorldMapRegionDefinition item = definition.regions[i];
                    ESWorkbenchSelection cachedSelection = item == null
                        ? ESWorkbenchSelection.Empty
                        : hitSelectionCache.GetOrCreateLocal(
                            "world.region", item.regionId, "world.region.", payload: item.regionId);
                    string stableId = cachedSelection.IsEmpty
                        ? string.Empty
                        : cachedSelection.StableId;
                    if (item != null && isHierarchyVisible(stableId) && item.Contains(point2D))
                    {
                        areaHit = cachedSelection;
                        break;
                    }
                }
            return ESWorkbenchSpatialHitResolver.PreferPrecise(result, areaHit);
        }

        private static ESWorkbenchPointerHitKind ResolvePointerHitKind(
            ESWorkbenchSelection hitSelection)
        {
            return ESWorkbenchSpatialHitResolver.ResolveHitKind(
                hitSelection,
                IsWorldContainerSelection);
        }

        private static bool IsWorldContainerSelection(ESWorkbenchSelection selection)
        {
            return selection != null
                && string.Equals(selection.Kind, "world.region", StringComparison.Ordinal);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || pointerCoordinator.IsExternalContentActive)
            {
                if (hover.Clear()) MarkDirtyRepaint();
                if (panning || moving || paintingTerrain) CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            Vector2 currentLocal = this.WorldToLocal(evt.position);
            UpdatePointerWorldStatus(currentLocal);
            terrainPointerPosition = currentLocal;
            terrainPointerValid = contentRect.Contains(currentLocal);
            if (((paintingTerrain && evt.pointerId == terrainPointerId)
                    || (moving && evt.pointerId == movePointerId)
                    || (panning && evt.pointerId == panPointerId))
                && !pointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport))
            {
                CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            // 笔刷工具未按下时仍显示精确目标悬停；这样用户能在点击前看到
            // 区域/POI 会让出给对象移动，避免笔刷与移动语义在按下瞬间才跳变。
            bool showPreciseHover = ESWorkbenchInteractionPolicy.ShouldShowPreciseHover(
                readOnly: false,
                transforming: moving,
                painting: paintingTerrain,
                navigationCapturing: panning,
                capabilities: actions.Tools.ActiveCapabilities,
                pointerInside: terrainPointerValid);
            if (showPreciseHover
                && TryResolveWorldPoint(currentLocal, out Vector3 hoverPoint))
            {
                ESWorkbenchSelection hovered = TryHitWorldItem(
                    hoverPoint, contentRect, ResolveWorldBounds());
                if (hover.Update(hovered?.StableId)) MarkDirtyRepaint();
            }
            else if (hover.Clear()) MarkDirtyRepaint();
            if (paintingTerrain && evt.pointerId == terrainPointerId
                && pointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport)
                && this.HasPointerCapture(evt.pointerId))
            {
                if (terrainPointerValid && TryResolveTerrainPoint(currentLocal, out Vector3 terrainPoint))
                    SampleTerrainStroke(terrainPoint);
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }
            if (IsTerrainPaintingInteraction()) MarkDirtyRepaint();
            if (moving && evt.pointerId == movePointerId
                && pointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport)
                && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 moveLocal = this.WorldToLocal(evt.position);
                if (UpdateMovePreview(moveLocal, evt.shiftKey)) MarkDirtyRepaint();
                BeginEdgePan(moveLocal, evt.shiftKey);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId
                || !pointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport)
                || !this.HasPointerCapture(evt.pointerId)) return;
            Vector2 panLocal = this.WorldToLocal(evt.position);
            if (!gestureSession.TryAdvance(
                    evt.pointerId,
                    panLocal,
                    final: false,
                    out ESWorkbenchPointerGestureSession.AdvanceResult advance))
            {
                if (!advance.IsStarted)
                {
                    evt.StopPropagation();
                    return;
                }
                StopPanning();
                gestureSession.Cancel(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
                pointerCoordinator.Release(
                    this,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
                evt.StopPropagation();
                return;
            }
            navigation.PanBy(advance.Delta);
            navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(panLocal);
            UpdateRegionLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            bool ownsViewportPointer = pointerCoordinator.Owns(
                this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport);
            bool isCurrentGesturePointer =
                (paintingTerrain && evt.pointerId == terrainPointerId)
                || (moving && evt.pointerId == movePointerId)
                || (panning && evt.pointerId == panPointerId);
            if ((paintingTerrain || moving || panning)
                && isCurrentGesturePointer
                && !ownsViewportPointer)
            {
                // owner 已被窗口/面板生命周期夺走时，PointerUp 不能再提交领域变更。
                // 统一按取消收敛，避免旧窗口闭包或重挂载把临时预览写入新会话。
                CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            if (paintingTerrain && evt.pointerId == terrainPointerId)
            {
                bool ownsGesture = gestureSession.Owns(
                    ESWorkbenchPointerGestureSession.Kind.Paint, evt.pointerId);
                if (ownsGesture)
                {
                    Vector2 local = this.WorldToLocal(evt.position);
                    if (contentRect.Contains(local) && TryResolveTerrainPoint(local, out Vector3 terrainPoint))
                        SampleTerrainStroke(terrainPoint);
                    terrainStrokeSampler.Flush(EmitTerrainSample);
                    StopTerrainPainting(false);
                }
                else
                {
                    StopTerrainPainting(false, true);
                }
                gestureSession.TryFinishOwned(
                    evt.pointerId,
                    ESWorkbenchPointerGestureSession.EndReason.Commit);
                pointerCoordinator.Release(
                    this,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (moving && evt.pointerId == movePointerId)
            {
                bool ownsGesture = gestureSession.Owns(
                    ESWorkbenchPointerGestureSession.Kind.Move, evt.pointerId);
                if (ownsGesture)
                    UpdateMovePreview(this.WorldToLocal(evt.position), evt.shiftKey);
                ESWorkbenchSelection target = movingSelection;
                Vector3 worldPosition = pendingMoveWorld;
                bool shouldCommit = ownsGesture && pendingMoveValid;
                StopMoving();
                gestureSession.TryFinishOwned(
                    evt.pointerId,
                    ESWorkbenchPointerGestureSession.EndReason.Commit);
                pointerCoordinator.Release(
                    this,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                if (shouldCommit) actions.Authoring.TryMove(target, worldPosition, out _);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId) return;
            Vector2 panLocal = this.WorldToLocal(evt.position);
            if (gestureSession.TryAdvance(
                    evt.pointerId,
                    panLocal,
                    final: true,
                    out ESWorkbenchPointerGestureSession.AdvanceResult advance))
            {
                navigation.PanBy(advance.Delta);
                navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
                UpdatePointerWorldStatus(panLocal);
                UpdateRegionLabelPositions();
            }
            StopPanning();
            gestureSession.TryFinishOwned(
                evt.pointerId,
                ESWorkbenchPointerGestureSession.EndReason.Commit);
            pointerCoordinator.Release(
                this,
                evt.pointerId,
                ESWorkbenchPointerOwnerKind.Viewport);
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            // PointerCancel 表示系统或宿主明确中断本次指针序列：它不是普通
            // PointerUp，不能提交未确认的对象位置或笔刷尾点。统一走显式取消，
            // 让作者事务回滚、预览清空、capture 和 owner 一次性释放。
            if (gestureSession.IsActive || panning || moving || paintingTerrain)
                CancelInteraction();
            evt.StopImmediatePropagation();
        }

        private bool UpdateMovePreview(Vector2 local, bool lockDominantAxis)
        {
            if (!moving || !gestureSession.Owns(
                    ESWorkbenchPointerGestureSession.Kind.Move, movePointerId)) return false;
            if (!gestureSession.TryEnsureStarted(movePointerId, local)) return false;
            bool invalidatesPreviousPreview = pendingMoveValid;
            pendingMoveValid = false;
            if (!TryResolveWorldPoint(local, out Vector3 point, true)) return invalidatesPreviousPreview;
            point.y = moveAnchor.PointerStart.y;
            if (!moveAnchor.TryResolve(
                    point,
                    snapPosition,
                    ESWorkbenchMoveAxes.Horizontal,
                    lockDominantAxis,
                    out pendingMoveWorld)) return invalidatesPreviousPreview;
            pendingMoveValid = true;
            return true;
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == panPointerId) StopPanning();
            if (evt.pointerId == movePointerId) StopMoving();
            // Pointer capture can be lost when the pointer leaves the panel, the window
            // changes focus, or another editor control takes ownership. Preserve the
            // same terminal-sample contract as an ordinary pointer-up instead of
            // discarding the unflushed tail of the stroke.
            if (evt.pointerId == terrainPointerId)
            {
                ESWorkbenchGestureTerminationDecision decision =
                    ESWorkbenchGestureTerminationDecision.Resolve(
                        ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                        ESWorkbenchCaptureLossPolicy.CommitPendingSamples,
                        hasPreview: false);
                StopTerrainPainting(
                    decision.FlushPendingSamples,
                    cancel: !decision.CommitAuthoring);
            }
            if (gestureSession.PointerId == evt.pointerId)
                gestureSession.TryFinishOwned(
                    evt.pointerId,
                    ESWorkbenchGestureTerminationDecision.Resolve(
                        ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                        ESWorkbenchCaptureLossPolicy.CancelPreview,
                        hasPreview: false).Reason);
            pointerCoordinator.Release(
                this,
                evt.pointerId,
                ESWorkbenchPointerOwnerKind.Viewport);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            terrainPointerValid = false;
            ClearPointerWorldStatus();
            if (paintingTerrain)
            {
                MarkDirtyRepaint();
                return;
            }
            hover.Clear();
            if (IsTerrainPaintingInteraction()) MarkDirtyRepaint();
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (!gestureSession.IsActive && !paintingTerrain && !panning && !moving)
            {
                hover.Clear();
                terrainPointerValid = false;
                ClearPointerWorldStatus();
                return;
            }

            int capturedPanPointerId = panPointerId;
            int capturedMovePointerId = movePointerId;
            int capturedTerrainPointerId = terrainPointerId;
            // 失焦与 PointerCaptureOut 具有相同的所有权语义：笔刷保留已发出的
            // 尾样本，移动/平移只释放会话，不把临时预览误写入作者数据。
            ESWorkbenchGestureTerminationDecision terrainDecision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                    ESWorkbenchCaptureLossPolicy.CommitPendingSamples,
                    hasPreview: false);
            StopTerrainPainting(
                terrainDecision.FlushPendingSamples,
                cancel: !terrainDecision.CommitAuthoring);
            StopPanning();
            StopMoving();
            if (gestureSession.IsActive)
                gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
            if (capturedPanPointerId >= 0)
                pointerCoordinator.Release(
                    this,
                    capturedPanPointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedMovePointerId >= 0)
                pointerCoordinator.Release(
                    this,
                    capturedMovePointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedTerrainPointerId >= 0)
                pointerCoordinator.Release(
                    this,
                    capturedTerrainPointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedPanPointerId >= 0 && this.HasPointerCapture(capturedPanPointerId))
                this.ReleasePointer(capturedPanPointerId);
            if (capturedMovePointerId >= 0 && this.HasPointerCapture(capturedMovePointerId))
                this.ReleasePointer(capturedMovePointerId);
            if (capturedTerrainPointerId >= 0 && this.HasPointerCapture(capturedTerrainPointerId))
                this.ReleasePointer(capturedTerrainPointerId);
            hover.Clear();
            terrainPointerValid = false;
            ClearPointerWorldStatus();
            MarkDirtyRepaint();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsTerrainPaintingInteraction()
                && terrainBrushShortcut?.Invoke(evt.keyCode, evt.modifiers) == true)
            {
                MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode != KeyCode.Escape || (!panning && !moving && !paintingTerrain)) return;
            CancelInteraction();
            evt.StopPropagation();
        }

        private bool IsTerrainPaintingInteraction()
        {
            return ESWorkbenchToolCapabilityResolver.Has(
                actions.Tools.ActiveCapabilities, ESWorkbenchToolCapabilities.Paint);
        }

        private void StopTerrainPainting(bool flushPendingSample = false, bool cancel = false)
        {
            if (!paintingTerrain) return;
            if (flushPendingSample)
                terrainStrokeSampler.Flush(EmitTerrainSample);
            paintingTerrain = false;
            terrainPointerId = -1;
            terrainStrokeSampler.Reset();
            if (cancel) terrainStrokeCancel?.Invoke();
            else terrainStrokeEnd?.Invoke();
            MarkDirtyRepaint();
        }

        private void SampleTerrainStroke(Vector3 point)
        {
            float spacing = feel.ResolveStrokeSpacing(
                terrainBrushRadius?.Invoke() ?? 8f);
            terrainStrokeSampler.Sample(
                point, spacing, EmitTerrainSample, feel.MaximumStrokeSamplesPerEvent);
        }

        private void EmitTerrainSample(Vector3 point)
        {
            worldClick?.Invoke(point);
            MarkDirtyRepaint();
        }

        public void CancelInteraction()
        {
            hover.Clear();
            ClearPointerWorldStatus();
            int capturedPanPointerId = panPointerId;
            int capturedMovePointerId = movePointerId;
            int capturedTerrainPointerId = terrainPointerId;
            // Explicit cancellation is intentionally discardive. Stop the stroke before
            // releasing capture so PointerCaptureOut cannot interpret Esc/close as a
            // focus-loss commit and flush the pending tail.
            StopTerrainPainting(false, true);
            gestureSession.Cancel();
            StopPanning();
            StopMoving();
            if (capturedPanPointerId >= 0)
                pointerCoordinator.Release(
                    this,
                    capturedPanPointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedMovePointerId >= 0)
                pointerCoordinator.Release(
                    this,
                    capturedMovePointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedPanPointerId >= 0 && this.HasPointerCapture(capturedPanPointerId))
                this.ReleasePointer(capturedPanPointerId);
            if (capturedMovePointerId >= 0 && this.HasPointerCapture(capturedMovePointerId))
                this.ReleasePointer(capturedMovePointerId);
            if (capturedTerrainPointerId >= 0 && this.HasPointerCapture(capturedTerrainPointerId))
                this.ReleasePointer(capturedTerrainPointerId);
            MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!ESWorkbenchInteractionPolicy.ShouldHandleNavigation(
                    ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                        || pointerCoordinator.IsExternalContentActive,
                    gestureSession.IsActive))
            {
                evt.StopPropagation();
                return;
            }
            if (draft?.Definition == null) return;
            Vector2 local = this.WorldToLocal(evt.mousePosition);
            Rect worldBounds = ResolveWorldBounds();
            navigation.ZoomAt(local, evt.delta.y, contentRect, worldBounds);
            navigation.ConstrainPan(contentRect, worldBounds, feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(local);
            UpdateRegionLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        public bool TryResolveWorldPoint(
            Vector2 canvasPoint,
            out Vector3 point,
            bool allowOutside = false)
        {
            return TryResolveProjection(
                canvasPoint,
                allowOutside
                    ? ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.EdgePanPreview)
                    : ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.AuthorHit),
                out point);
        }

        internal bool TryGetPointerWorldPosition(out Vector3 point)
        {
            point = lastPointerWorldPosition;
            return lastPointerWorldPositionValid;
        }

        private void UpdatePointerWorldStatus(Vector2 local)
        {
            Vector3 next = default;
            bool valid = contentRect.Contains(local)
                && TryResolveWorldPoint(local, out next);
            if (valid)
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
            if (lastPointerWorldPositionValid)
            {
                lastPointerWorldPositionValid = false;
                statusChanged?.Invoke();
            }
        }

        private void ClearPointerWorldStatus()
        {
            if (!lastPointerWorldPositionValid) return;
            lastPointerWorldPositionValid = false;
            statusChanged?.Invoke();
        }

        public bool TryResolveTerrainPoint(
            Vector2 canvasPoint,
            out Vector3 point,
            bool allowOutside = false)
        {
            return TryResolveProjection(
                canvasPoint,
                allowOutside
                    ? ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.DropPreview,
                        requireTerrainSurface: true)
                    : ESWorkbenchViewportProjectionRequest.For(
                        ESWorkbenchViewportProjectionIntent.TerrainPaint),
                out point);
        }

        public bool TryResolveProjection(
            Vector2 canvasPoint,
            ESWorkbenchViewportProjectionRequest request,
            out Vector3 point)
        {
            point = default;
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null) return false;
            Rect worldBounds = ResolveWorldBounds();
            bool allowOutside = request.AllowOutside;
            if (request.RequireInteractionBoundary)
            {
                if (!ESWorkbenchDropPointPolicy.CanCommit(contentRect, canvasPoint)) return false;
                if (request.Intent == ESWorkbenchViewportProjectionIntent.TerrainPaint
                    && !ResolveMapRect(contentRect, ResolveWorldMin(), ResolveWorldMax())
                        .Contains(canvasPoint)) return false;
            }
            if (!navigation.TryCanvasToWorld(
                    canvasPoint,
                    worldBounds,
                    contentRect,
                    0f,
                    out point,
                    !allowOutside)) return false;
            if (request.ClampToWorld)
            {
                // 边缘平移和拖放允许指针暂时越过画布，但最终作者坐标必须夹在世界内。
                point.x = Mathf.Clamp(point.x, worldBounds.xMin, worldBounds.xMax);
                point.z = Mathf.Clamp(point.z, worldBounds.yMin, worldBounds.yMax);
            }
            float u = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, point.x);
            float v = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, point.z);
            if (definition.heightfield != null)
                point.y = ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                    * definition.terrainHeightScale;
            return true;
        }

        /// <summary>
        /// 解析正式拖放点：允许地图矩形外的视口留白夹到世界边界，
        /// 使拖放预览与释放提交保持同一落点语义。
        /// </summary>
        public bool TryResolveDropPoint(
            Vector2 canvasPoint,
            bool terrainOnly,
            out Vector3 point)
        {
            point = default;
            if (!ESWorkbenchDropPointPolicy.CanCommit(contentRect, canvasPoint)) return false;
            return terrainOnly
                ? TryResolveTerrainPoint(canvasPoint, out point, allowOutside: true)
                : TryResolveWorldPoint(canvasPoint, out point, allowOutside: true);
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
            // 与正式提交共享同一个边界和投影入口，避免无效区域遗留拖放幽灵。
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
            int count = Mathf.Max(1, items?.Count ?? 0);
            bool singleItem = items == null || items.Count <= 1;
            bool currentSnapEnabled = viewportLayout?.snapEnabled == true;
            float currentSnapStep = viewportLayout?.moveSnap ?? 0f;
            if (singleItem
                && lastDropPreviewAnchorValid
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
            dropPreviewItem = item;
            dropPreviewSize = previewSize;
            dropPreviewState = state;
            ESWorkbenchDropLayout.FillGridPositions(
                anchor,
                count,
                spacing,
                snapPosition,
                dropPreviewPositions,
                feel.MinimumDropSpacing);
            lastDropPreviewAnchor = anchor;
            lastDropPreviewCount = count;
            lastDropPreviewSpacing = spacing;
            lastDropPreviewSnapEnabled = currentSnapEnabled;
            lastDropPreviewSnapStep = currentSnapStep;
            lastDropPreviewAnchorValid = true;
            MarkDirtyRepaint();
        }

        public void ClearDropPreview()
        {
            if (dropPreviewItem == null && dropPreviewPositions.Count == 0) return;
            dropPreviewItem = null;
            dropPreviewPositions.Clear();
            dropPreviewState = ESWorkbenchDropPreviewState.Allowed;
            lastDropPreviewAnchor = default;
            lastDropPreviewCount = -1;
            lastDropPreviewSpacing = 0f;
            lastDropPreviewSnapEnabled = false;
            lastDropPreviewSnapStep = 0f;
            lastDropPreviewAnchorValid = false;
            MarkDirtyRepaint();
        }

        private Vector2 ResolveWorldMin()
        {
            Vector2 value = draft.Definition.worldMin;
            return draft.Definition.worldMax.x > value.x && draft.Definition.worldMax.y > value.y ? value : Vector2.zero;
        }

        private Vector2 ResolveWorldMax()
        {
            Vector2 min = draft.Definition.worldMin;
            Vector2 value = draft.Definition.worldMax;
            return value.x > min.x && value.y > min.y ? value : new Vector2(256f, 256f);
        }

        private Rect ResolveMapRect(Rect viewport, Vector2 min, Vector2 max)
        {
            return navigation.ResolveCanvasBounds(viewport,
                Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        private static Vector2 WorldToCanvas(Vector2 value, Vector2 min, Vector2 max, Rect rect)
        {
            return ESWorkbenchCanvasNavigationState.WorldToCanvas(
                value, Rect.MinMaxRect(min.x, min.y, max.x, max.y), rect);
        }

        private Rect ResolveWorldBounds()
        {
            Vector2 min = ResolveWorldMin();
            Vector2 max = ResolveWorldMax();
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool IsSelected(string stableId)
        {
            return selection?.Current != null && selection.Current.StableId == stableId;
        }

        private static void DrawFilledRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawOutline(Painter2D painter, Rect rect, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void DrawMapBorder(Painter2D painter, Rect rect)
        {
            Color border = ESWorldMapEditorPresentation.Grid;
            border.a = 0.9f;
            DrawOutline(painter, rect, border, 1.5f);
        }

        private static void DrawCrosshair(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(center - Vector2.right * radius);
            painter.LineTo(center + Vector2.right * radius);
            painter.MoveTo(center - Vector2.up * radius);
            painter.LineTo(center + Vector2.up * radius);
            painter.Stroke();
        }

        private void DrawMoveTarget(
            Painter2D painter,
            ESWorldMapDefinition definition,
            Vector2 min,
            Vector2 max,
            Rect mapRect)
        {
            Vector2 origin = WorldToCanvas(new Vector2(moveOriginWorld.x, moveOriginWorld.z), min, max, mapRect);
            Vector2 target = WorldToCanvas(new Vector2(pendingMoveWorld.x, pendingMoveWorld.z), min, max, mapRect);
            painter.strokeColor = new Color(1f, 0.72f, 0.18f, 0.9f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(origin);
            painter.LineTo(target);
            painter.Stroke();

            Vector3 size = ResolveSelectionSize(definition, movingSelection);
            DrawTargetShape(
                painter,
                target,
                WorldSizeToCanvas(size, min, max, mapRect),
                movingSelection?.Kind == "world.region" ? ESWorkbenchContentKind.RegionTemplate : ESWorkbenchContentKind.Prefab,
                new Color(1f, 0.72f, 0.18f, 0.2f),
                new Color(1f, 0.72f, 0.18f, 1f));
            DrawCrosshair(painter, target, 10f, new Color(1f, 0.78f, 0.22f, 1f));
        }

        private void DrawDropPreview(Painter2D painter, Vector2 min, Vector2 max, Rect mapRect)
        {
            if (dropPreviewItem == null || dropPreviewPositions.Count == 0) return;
            Vector2 canvasSize = WorldSizeToCanvas(dropPreviewSize, min, max, mapRect);
            Color fill = dropPreviewState.Accepted
                ? new Color(0.18f, 0.72f, 0.92f, 0.2f)
                : new Color(0.92f, 0.18f, 0.2f, 0.16f);
            Color outline = dropPreviewState.Accepted
                ? new Color(0.26f, 0.84f, 1f, 1f)
                : new Color(1f, 0.28f, 0.3f, 1f);
            for (int i = 0; i < dropPreviewPositions.Count; i++)
            {
                Vector3 position = dropPreviewPositions[i];
                Vector2 center = WorldToCanvas(new Vector2(position.x, position.z), min, max, mapRect);
                DrawTargetShape(painter, center, canvasSize, dropPreviewItem.ContentKind, fill, outline);
                DrawCrosshair(painter, center, 8f, outline);
            }
        }

        private static void DrawTargetShape(
            Painter2D painter,
            Vector2 center,
            Vector2 size,
            ESWorkbenchContentKind kind,
            Color fill,
            Color outline)
        {
            float width = Mathf.Max(16f, Mathf.Abs(size.x));
            float height = Mathf.Max(16f, Mathf.Abs(size.y));
            if (kind == ESWorkbenchContentKind.RegionTemplate || kind == ESWorkbenchContentKind.SceneTemplate)
            {
                Rect rect = new Rect(center - new Vector2(width, height) * 0.5f, new Vector2(width, height));
                DrawFilledRect(painter, rect, fill);
                DrawOutline(painter, rect, outline, 2f);
                return;
            }
            float radius = Mathf.Clamp(Mathf.Max(width, height) * 0.5f, 8f, 64f);
            painter.fillColor = fill;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
            painter.strokeColor = outline;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();
        }

        private static Vector2 WorldSizeToCanvas(Vector3 size, Vector2 min, Vector2 max, Rect rect)
        {
            return new Vector2(
                Mathf.Abs(size.x) / Mathf.Max(0.001f, max.x - min.x) * rect.width,
                Mathf.Abs(size.z) / Mathf.Max(0.001f, max.y - min.y) * rect.height);
        }

        private Vector3 ResolveSelectionWorldPosition(ESWorkbenchSelection current, Vector3 fallback)
        {
            return TryResolveSelectionWorldPosition(current, fallback.y, out Vector3 position)
                ? position
                : fallback;
        }

        private bool TryResolveSelectionWorldPosition(
            ESWorkbenchSelection current,
            float preservedY,
            out Vector3 position)
        {
            position = default;
            ESWorldMapDefinition definition = draft?.Definition;
            string id = current?.Payload as string;
            if (definition == null || string.IsNullOrWhiteSpace(id)) return false;
            if (current.Kind == "world.region")
            {
                ESWorldMapRegionDefinition region = definition.regions?.Find(value => value != null && value.regionId == id);
                if (region != null)
                {
                    Vector2 center = (region.min + region.max) * 0.5f;
                    position = new Vector3(center.x, preservedY, center.y);
                    return true;
                }
            }
            if (current.Kind == "world.poi")
            {
                ESWorldMapPoiDefinition poi = definition.pois?.Find(value => value != null && value.poiId == id);
                if (poi != null)
                {
                    position = new Vector3(poi.position.x, preservedY, poi.position.y);
                    return true;
                }
            }
            ESWorldMapPrefabPlacement placement = definition.prefabPlacements?
                .Find(value => value != null && value.placementId == id);
            if (placement == null) return false;
            position = placement.position;
            return true;
        }

        private static Vector3 ResolveSelectionSize(
            ESWorldMapDefinition definition,
            ESWorkbenchSelection current)
        {
            string id = current?.Payload as string;
            if (definition == null || string.IsNullOrWhiteSpace(id)) return Vector3.one * 2f;
            if (current.Kind == "world.region")
            {
                ESWorldMapRegionDefinition region = definition.regions?.Find(value => value != null && value.regionId == id);
                if (region != null)
                {
                    Vector2 size = region.max - region.min;
                    return new Vector3(size.x, 1f, size.y);
                }
            }
            if (current.Kind == "world.poi") return Vector3.one * 2f;
            ESWorldMapPrefabPlacement placement = definition.prefabPlacements?
                .Find(value => value != null && value.placementId == id);
            return placement?.scale ?? Vector3.one * 2f;
        }

        private void StopPanning()
        {
            panning = false;
            panPointerId = -1;
        }

        private void BeginEdgePan(Vector2 local, bool lockDominantAxis)
        {
            if (!edgePanSession.UpdatePointer(local, lockDominantAxis))
                edgePanSession.Begin(local, lockDominantAxis, EditorApplication.timeSinceStartup);
            if (moving && gestureSession.IsStarted && edgePanSession.IsActive
                && edgePanSchedule?.isActive == false)
            {
                edgePanSchedule.Resume();
            }
        }

        private void ApplyEdgePan()
        {
            if (!moving || !edgePanSession.IsActive || !gestureSession.IsStarted
                || !pointerCoordinator.Owns(
                    this, movePointerId, ESWorkbenchPointerOwnerKind.Viewport)
                || !edgePanSession.TryAdvance(
                    EditorApplication.timeSinceStartup, out float deltaTime)) return;
            if (!TryEdgePan(edgePanSession.Pointer, deltaTime)) return;
            UpdateMovePreview(edgePanSession.Pointer, edgePanSession.LockDominantAxis);
            MarkDirtyRepaint();
        }

        public bool TryEdgePan(Vector2 localPosition, float deltaTime)
        {
            if (!edgePan.Evaluate(contentRect, localPosition, deltaTime, out Vector2 delta)) return false;
            navigation.PanBy(delta);
            navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(localPosition);
            UpdateRegionLabelPositions();
            MarkDirtyRepaint();
            return true;
        }

        public bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message)
        {
            message = string.Empty;
            ESWorkbenchSelection current = selection?.Current;
            if (!ESWorkbenchNudgeResolver.TryResolveDelta(
                    keyCode, shift, controlOrCommand, feel, out Vector3 delta)
                || current == null
                || isHierarchyLocked(current.StableId)
                || !actions.Authoring.CanMove(current)) return false;
            if (!TryResolveSelectionWorldPosition(current, 0f, out Vector3 position)) return false;
            Vector3 target = snapPosition(position + delta);
            bool committed = actions.Authoring.TryMove(current, target, out message);
            if (committed) Bind(draft, ESWorkbenchRefreshReason.DataChanged);
            return committed;
        }

        private void StopMoving()
        {
            moving = false;
            movePointerId = -1;
            edgePanSession.Stop();
            edgePanSchedule?.Pause();
            pendingMoveValid = false;
            movingSelection = null;
            moveAnchor.Reset();
            MarkDirtyRepaint();
        }

        private void SynchronizeRegionLabels()
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition?.regions == null)
            {
                regionLabels.Clear();
                labelOverlay.Clear();
                return;
            }
            var liveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                if (region == null || string.IsNullOrWhiteSpace(region.regionId)
                    || !isHierarchyVisible("world.region." + region.regionId)) continue;
                liveIds.Add(region.regionId);
                if (!regionLabels.TryGetValue(region.regionId, out Label label))
                {
                    label = new Label { pickingMode = PickingMode.Ignore };
                    label.style.position = Position.Absolute;
                    label.style.height = 18f;
                    label.style.fontSize = 10f;
                    label.style.paddingLeft = 4f;
                    label.style.paddingRight = 4f;
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    regionLabels.Add(region.regionId, label);
                    labelOverlay.Add(label);
                }
                label.text = string.IsNullOrWhiteSpace(region.displayName) ? region.regionId : region.displayName;
                label.tooltip = label.text;
            }
            string[] stale = regionLabels.Keys.Where(id => !liveIds.Contains(id)).ToArray();
            for (int i = 0; i < stale.Length; i++)
            {
                Label label = regionLabels[stale[i]];
                label.RemoveFromHierarchy();
                regionLabels.Remove(stale[i]);
            }
            UpdateRegionLabelPositions();
        }

        private void UpdateRegionLabelPositions()
        {
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition?.regions == null || contentRect.width <= 1f || contentRect.height <= 1f) return;
            Vector2 min = ResolveWorldMin();
            Vector2 max = ResolveWorldMax();
            Rect mapRect = ResolveMapRect(contentRect, min, max);
            for (int i = 0; i < definition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                if (region == null || !isHierarchyVisible("world.region." + region.regionId)
                    || !regionLabels.TryGetValue(region.regionId, out Label label)) continue;
                Vector2 first = WorldToCanvas(region.min, min, max, mapRect);
                Vector2 second = WorldToCanvas(region.max, min, max, mapRect);
                float left = Mathf.Min(first.x, second.x);
                float top = Mathf.Min(first.y, second.y);
                label.style.left = left + 2f;
                label.style.top = top + 1f;
                label.style.width = Mathf.Max(24f, Mathf.Abs(second.x - first.x) - 4f);
            }
        }

        public void Dispose()
        {
            hover.Clear();
            edgePanSchedule?.Pause();
            edgePanSchedule = null;
            CancelInteraction();
            draft = null;
            ClearDropPreview();
            generateVisualContent -= Draw;
            regionLabels.Clear();
            labelOverlay.Clear();
            StopPanning();
            StopMoving();
            gestureSession.Cancel(ESWorkbenchPointerGestureSession.EndReason.Deactivate);
        }
    }

    internal static class ESWorldHeightfieldReadOnly
    {
        public static bool TryRaycast(ESWorldMapDefinition definition, Ray localRay, out Vector3 point)
        {
            point = default;
            ESWorldMapHeightfield field = definition?.heightfield;
            if (field == null || field.width < 2 || field.height < 2
                || definition.worldMax.x <= definition.worldMin.x
                || definition.worldMax.y <= definition.worldMin.y)
                return false;

            float heightScale = Mathf.Max(0.001f, definition.terrainHeightScale);
            Bounds bounds = new Bounds(
                new Vector3(
                    (definition.worldMin.x + definition.worldMax.x) * 0.5f,
                    heightScale * 0.5f,
                    (definition.worldMin.y + definition.worldMax.y) * 0.5f),
                new Vector3(
                    definition.worldMax.x - definition.worldMin.x,
                    heightScale,
                    definition.worldMax.y - definition.worldMin.y));
            if (!TryIntersectBounds(localRay, bounds, out float enter, out float exit)) return false;

            int stepCount = Mathf.Clamp(Mathf.Max(field.width, field.height) * 2, 64, 512);
            float previousDistance = SurfaceDistance(definition, localRay.GetPoint(enter));
            float previousT = enter;
            if (Mathf.Abs(previousDistance) <= 0.0001f)
            {
                point = ResolveSurfacePoint(definition, localRay.GetPoint(enter));
                return true;
            }
            for (int i = 1; i <= stepCount; i++)
            {
                float currentT = Mathf.Lerp(enter, exit, i / (float)stepCount);
                float currentDistance = SurfaceDistance(definition, localRay.GetPoint(currentT));
                if (previousDistance >= 0f && currentDistance <= 0f)
                {
                    float low = previousT;
                    float high = currentT;
                    for (int iteration = 0; iteration < 14; iteration++)
                    {
                        float middle = (low + high) * 0.5f;
                        if (SurfaceDistance(definition, localRay.GetPoint(middle)) > 0f) low = middle;
                        else high = middle;
                    }
                    point = ResolveSurfacePoint(definition, localRay.GetPoint((low + high) * 0.5f));
                    return true;
                }
                previousT = currentT;
                previousDistance = currentDistance;
            }
            return false;
        }

        public static float Get(ESWorldMapHeightfield field, int x, int y)
        {
            if (field == null) return 0f;
            int width = Mathf.Max(2, field.width);
            int height = Mathf.Max(2, field.height);
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            int expected = width * height;
            if (field.samples == null || field.samples.Count != expected)
                return Mathf.Clamp01(field.defaultHeight);
            return Mathf.Clamp01(field.samples[y * width + x]);
        }

        public static float SampleNormalized(ESWorldMapHeightfield field, float u, float v)
        {
            if (field == null) return 0f;
            int width = Mathf.Max(2, field.width);
            int height = Mathf.Max(2, field.height);
            float fx = Mathf.Clamp01(u) * (width - 1);
            float fy = Mathf.Clamp01(v) * (height - 1);
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            return Mathf.Lerp(
                Mathf.Lerp(Get(field, x0, y0), Get(field, x1, y0), tx),
                Mathf.Lerp(Get(field, x0, y1), Get(field, x1, y1), tx),
                ty);
        }

        private static Vector3 ResolveSurfacePoint(ESWorldMapDefinition definition, Vector3 candidate)
        {
            candidate.x = Mathf.Clamp(candidate.x, definition.worldMin.x, definition.worldMax.x);
            candidate.z = Mathf.Clamp(candidate.z, definition.worldMin.y, definition.worldMax.y);
            float u = Mathf.InverseLerp(definition.worldMin.x, definition.worldMax.x, candidate.x);
            float v = Mathf.InverseLerp(definition.worldMin.y, definition.worldMax.y, candidate.z);
            candidate.y = SampleNormalized(definition.heightfield, u, v)
                * Mathf.Max(0.001f, definition.terrainHeightScale);
            return candidate;
        }

        private static float SurfaceDistance(ESWorldMapDefinition definition, Vector3 candidate)
        {
            return candidate.y - ResolveSurfacePoint(definition, candidate).y;
        }

        private static bool TryIntersectBounds(Ray ray, Bounds bounds, out float enter, out float exit)
        {
            enter = 0f;
            exit = float.PositiveInfinity;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin = ray.origin[axis];
                float direction = ray.direction[axis];
                if (Mathf.Abs(direction) <= 0.000001f)
                {
                    if (origin < min[axis] || origin > max[axis]) return false;
                    continue;
                }
                float first = (min[axis] - origin) / direction;
                float second = (max[axis] - origin) / direction;
                if (first > second) (first, second) = (second, first);
                enter = Mathf.Max(enter, first);
                exit = Mathf.Min(exit, second);
                if (exit < enter) return false;
            }
            return exit >= 0f && !float.IsInfinity(exit);
        }
    }
}
#endif
