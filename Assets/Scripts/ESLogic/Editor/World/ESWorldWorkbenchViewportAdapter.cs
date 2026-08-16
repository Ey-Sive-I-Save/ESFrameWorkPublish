#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    internal sealed class ESWorldWorkbenchViewportAdapter : IESWorkbenchViewport, IESWorkbenchFrameableViewport
    {
        private readonly ESWorldBuilderWorkbenchWindow window;
        private readonly ESWorkbenchViewportContext viewportContext;
        private readonly ESWorkbenchViewportKind kind;
        private readonly ESWorldAuthoringViewport viewport3D;
        private readonly ESWorldMap2DViewportElement viewport2D;

        public ESWorldWorkbenchViewportAdapter(
            ESWorldBuilderWorkbenchWindow window,
            ESWorkbenchViewportContext context,
            ESWorkbenchViewportKind kind)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            viewportContext = context ?? throw new ArgumentNullException(nameof(context));
            this.kind = kind;
            if (kind == ESWorkbenchViewportKind.Scene3D)
            {
                viewport3D = new ESWorldAuthoringViewport(
                    point => window.HandleAuthoringPoint(point, context.IsHierarchyVisible, context.IsHierarchyLocked),
                    context);
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
                    context.IsHierarchyLocked);
                Root = viewport2D;
            }
            Root.style.flexGrow = 1f;
            Root.style.minWidth = 0f;
            Root.style.minHeight = 0f;
            Refresh(ESWorkbenchRefreshReason.Initial);
        }

        public VisualElement Root { get; }
        public void Activate() => Refresh(ESWorkbenchRefreshReason.Explicit);
        public void Deactivate() => viewport3D?.CancelInteraction();

        public void FrameAll()
        {
            if (kind == ESWorkbenchViewportKind.Scene3D) viewport3D?.FrameAll();
            else viewport2D?.FrameAll();
        }

        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            ESWorldMapAsset draft = window.ESWorld_Draft;
            if (kind == ESWorkbenchViewportKind.Scene3D)
            {
                if (reason == ESWorkbenchRefreshReason.SelectionChanged) return;
                viewport3D?.Bind(draft, reason == ESWorkbenchRefreshReason.Initial || reason == ESWorkbenchRefreshReason.AssetChanged);
            }
            else viewport2D?.Bind(draft, reason);
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item)
        {
            return item?.Source is GameObject gameObject
                && PrefabUtility.IsPartOfPrefabAsset(gameObject)
                && viewportContext.Actions.Authoring.CanCreate(item);
        }

        public bool TryAccept(ESWorkbenchDropContext context, out string message)
        {
            message = string.Empty;
            if (context?.Item == null || !CanAccept(context.Item))
            {
                message = "世界视口只接受 Project 中已注册的 Prefab。";
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
            bool resolved = kind == ESWorkbenchViewportKind.Scene3D
                ? viewport3D != null && viewport3D.TryResolveWorldPoint(context.LocalPosition, out position3D)
                : viewport2D != null && viewport2D.TryResolveWorldPoint(context.LocalPosition, out position3D);
            if (!resolved)
            {
                message = "拖放位置不在当前世界作者画布内。";
                return false;
            }
            Vector3 position = viewportContext.SnapPosition(position3D);
            if (!context.Actions.Authoring.TryCreate(context.Item, position, out message)) return false;
            Refresh(ESWorkbenchRefreshReason.DataChanged);
            return true;
        }

        public void Dispose()
        {
            viewport3D?.Dispose();
            viewport2D?.Dispose();
        }
    }

    internal sealed class ESWorldMap2DViewportElement : VisualElement, IDisposable
    {
        private readonly Action<Vector3> worldClick;
        private readonly ESWorkbenchActionContext actions;
        private readonly ESWorkbenchSelectionService selection;
        private readonly ESWorkbenchViewportLayoutState viewportLayout;
        private readonly Func<Vector3, Vector3> snapPosition;
        private readonly Func<string, bool> isHierarchyVisible;
        private readonly Func<string, bool> isHierarchyLocked;
        private readonly VisualElement labelOverlay;
        private readonly Dictionary<string, Label> regionLabels = new Dictionary<string, Label>(StringComparer.Ordinal);
        private ESWorldMapAsset draft;
        private Vector2 pan;
        private float zoom = 1f;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 lastPointerPosition;
        private bool moving;
        private int movePointerId = -1;
        private Vector2 movePointerStart;
        private Vector3 pendingMoveWorld;
        private bool pendingMoveValid;
        private ESWorkbenchSelection movingSelection;

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
            Func<string, bool> isHierarchyLocked = null)
        {
            name = "ESWorldMap2DViewport";
            this.worldClick = worldClick;
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.selection = selection;
            viewportLayout = layout ?? new ESWorkbenchViewportLayoutState { viewportId = "world.canvas-2d" };
            this.snapPosition = snapPosition ?? (value => value);
            this.isHierarchyVisible = isHierarchyVisible ?? (_ => true);
            this.isHierarchyLocked = isHierarchyLocked ?? (_ => false);
            pan = viewportLayout.pan;
            zoom = Mathf.Clamp(viewportLayout.zoom <= 0f ? 1f : viewportLayout.zoom, 0.5f, 8f);
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
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<WheelEvent>(OnWheel);
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
            pan = Vector2.zero;
            zoom = 1f;
            SaveViewTransform();
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
                DrawCrosshair(painter, WorldToCanvas(
                    new Vector2(pendingMoveWorld.x, pendingMoveWorld.z), min, max, mapRect),
                    10f,
                    ESWorldMapEditorPresentation.Selection);
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

        private void DrawRegions(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.regions == null) return;
            for (int i = 0; i < definition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition region = definition.regions[i];
                if (region == null || !isHierarchyVisible("world.region." + region.regionId)) continue;
                Vector2 a = WorldToCanvas(region.min, min, max, rect);
                Vector2 b = WorldToCanvas(region.max, min, max, rect);
                Rect regionRect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                Color color = ESWorldMapEditorPresentation.Region;
                bool selected = IsSelected("world.region." + region.regionId);
                if (selected) color.a = Mathf.Min(0.78f, color.a + 0.28f);
                DrawFilledRect(painter, regionRect, color);
                if (selected) DrawOutline(painter, regionRect, ESWorldMapEditorPresentation.Selection, 2.5f);
            }
        }

        private void DrawPois(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.pois == null) return;
            for (int i = 0; i < definition.pois.Count; i++)
            {
                ESWorldMapPoiDefinition poi = definition.pois[i];
                if (poi == null || !isHierarchyVisible("world.poi." + poi.poiId)) continue;
                Vector2 point = WorldToCanvas(poi.position, min, max, rect);
                painter.fillColor = IsSelected("world.poi." + poi.poiId)
                    ? ESWorldMapEditorPresentation.Selection : ESWorldMapEditorPresentation.Poi;
                painter.BeginPath();
                painter.Arc(point, IsSelected("world.poi." + poi.poiId) ? 7f : 5f, 0f, 360f);
                painter.Fill();
            }
        }

        private void DrawPlacements(Painter2D painter, ESWorldMapDefinition definition, Vector2 min, Vector2 max, Rect rect)
        {
            if (definition.prefabPlacements == null) return;
            for (int i = 0; i < definition.prefabPlacements.Count; i++)
            {
                ESWorldMapPrefabPlacement placement = definition.prefabPlacements[i];
                if (placement == null || !placement.enabled
                    || !isHierarchyVisible("world.prefab." + placement.placementId)) continue;
                Vector2 point = WorldToCanvas(new Vector2(placement.position.x, placement.position.z), min, max, rect);
                float radius = IsSelected("world.prefab." + placement.placementId) ? 8f : 6f;
                painter.strokeColor = IsSelected("world.prefab." + placement.placementId)
                    ? ESWorldMapEditorPresentation.Selection : ESWorldMapEditorPresentation.Poi;
                painter.lineWidth = IsSelected("world.prefab." + placement.placementId) ? 3f : 2f;
                painter.BeginPath();
                painter.Arc(point, radius, 0f, 360f);
                painter.Stroke();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            Vector2 local = this.WorldToLocal(evt.position);
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                panning = true;
                panPointerId = evt.pointerId;
                lastPointerPosition = local;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0 || !TryResolveWorldPoint(local, out Vector3 point)) return;
            string activeToolId = actions.Tools.ActiveToolId ?? string.Empty;
            bool coreTransform = activeToolId == "core.move"
                || activeToolId == "core.rotate"
                || activeToolId == "core.scale";
            if (!coreTransform) worldClick?.Invoke(point);
            if (IsMoveInteractionEnabled() && actions.Authoring.CanMove(actions.Selection.Current)
                && !isHierarchyLocked(actions.Selection.Current?.StableId))
            {
                moving = true;
                movePointerId = evt.pointerId;
                movePointerStart = local;
                pendingMoveWorld = snapPosition(point);
                pendingMoveValid = false;
                movingSelection = actions.Selection.Current;
                this.CapturePointer(evt.pointerId);
            }
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (moving && evt.pointerId == movePointerId && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 dragLocal = this.WorldToLocal(evt.position);
                if (Vector2.Distance(movePointerStart, dragLocal) >= 3f
                    && TryResolveWorldPoint(dragLocal, out Vector3 point))
                {
                    pendingMoveWorld = snapPosition(point);
                    pendingMoveValid = true;
                    MarkDirtyRepaint();
                }
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId || !this.HasPointerCapture(evt.pointerId)) return;
            Vector2 panLocal = this.WorldToLocal(evt.position);
            pan += panLocal - lastPointerPosition;
            SaveViewTransform();
            UpdateRegionLabelPositions();
            lastPointerPosition = panLocal;
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (moving && evt.pointerId == movePointerId)
            {
                ESWorkbenchSelection target = movingSelection;
                Vector3 worldPosition = pendingMoveWorld;
                bool shouldCommit = pendingMoveValid;
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                StopMoving();
                if (shouldCommit) actions.Authoring.TryMove(target, worldPosition, out _);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId) return;
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            StopPanning();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == panPointerId) StopPanning();
            if (evt.pointerId == movePointerId) StopMoving();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (draft?.Definition == null) return;
            Vector2 local = this.WorldToLocal(evt.mousePosition);
            Rect before = ResolveMapRect(contentRect, ResolveWorldMin(), ResolveWorldMax());
            Vector2 normalized = new Vector2(
                Mathf.InverseLerp(before.xMin, before.xMax, local.x),
                Mathf.InverseLerp(before.yMin, before.yMax, local.y));
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-evt.delta.y * 0.035f), 0.5f, 8f);
            Rect after = ResolveMapRect(contentRect, ResolveWorldMin(), ResolveWorldMax());
            Vector2 anchored = new Vector2(
                Mathf.Lerp(after.xMin, after.xMax, normalized.x),
                Mathf.Lerp(after.yMin, after.yMax, normalized.y));
            pan += local - anchored;
            SaveViewTransform();
            UpdateRegionLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        public bool TryResolveWorldPoint(Vector2 canvasPoint, out Vector3 point)
        {
            point = default;
            ESWorldMapDefinition definition = draft?.Definition;
            if (definition == null) return false;
            Vector2 min = ResolveWorldMin();
            Vector2 max = ResolveWorldMax();
            Rect rect = ResolveMapRect(contentRect, min, max);
            if (!rect.Contains(canvasPoint)) return false;
            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, canvasPoint.x);
            float v = 1f - Mathf.InverseLerp(rect.yMin, rect.yMax, canvasPoint.y);
            point = new Vector3(Mathf.Lerp(min.x, max.x, u), 0f, Mathf.Lerp(min.y, max.y, v));
            if (definition.heightfield != null)
                point.y = ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                    * definition.terrainHeightScale;
            return true;
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
            float availableWidth = Mathf.Max(1f, viewport.width - 32f);
            float availableHeight = Mathf.Max(1f, viewport.height - 32f);
            float aspect = Mathf.Max(0.0001f, (max.x - min.x) / Mathf.Max(0.0001f, max.y - min.y));
            float width = availableWidth;
            float height = width / aspect;
            if (height > availableHeight) { height = availableHeight; width = height * aspect; }
            Vector2 size = new Vector2(width, height) * zoom;
            return new Rect(viewport.center + pan - size * 0.5f, size);
        }

        private static Vector2 WorldToCanvas(Vector2 value, Vector2 min, Vector2 max, Rect rect)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(min.x, max.x, value.x)),
                Mathf.Lerp(rect.yMax, rect.yMin, Mathf.InverseLerp(min.y, max.y, value.y)));
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

        private void StopPanning()
        {
            panning = false;
            panPointerId = -1;
        }

        private void StopMoving()
        {
            moving = false;
            movePointerId = -1;
            pendingMoveValid = false;
            movingSelection = null;
            MarkDirtyRepaint();
        }

        private bool IsMoveInteractionEnabled()
        {
            string toolId = actions.Tools.ActiveToolId ?? string.Empty;
            return string.Equals(toolId, "core.move", StringComparison.Ordinal)
                || string.Equals(toolId, "core.select", StringComparison.Ordinal)
                || toolId.EndsWith(".select", StringComparison.Ordinal);
        }

        private void SaveViewTransform()
        {
            viewportLayout.pan = pan;
            viewportLayout.zoom = zoom;
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
            draft = null;
            generateVisualContent -= Draw;
            regionLabels.Clear();
            labelOverlay.Clear();
            StopPanning();
            StopMoving();
        }
    }

    internal static class ESWorldHeightfieldReadOnly
    {
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
    }
}
#endif
