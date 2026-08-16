#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>UI Toolkit-owned surface with a bounded Unity preview renderer for the 3D authoring canvas.</summary>
    internal sealed class ESWorldAuthoringViewport : VisualElement, IDisposable
    {
        private readonly IMGUIContainer renderHost;
        private readonly Action<Vector3> worldClick;
        private readonly ESWorkbenchViewportContext context;
        private readonly bool readOnlyGameView;
        private ESEditorPreviewRenderContext preview;
        private readonly List<GameObject> previewObjects = new List<GameObject>();
        private readonly List<string> previewStableIds = new List<string>();
        private TerrainData terrainData;
        private GameObject terrainObject;
        private ESWorldMapAsset draft;
        private Vector3 focus;
        private float distance = 300f;
        private float yaw = 35f;
        private float pitch = 38f;
        private bool orbiting;
        private bool panning;
        private bool moving;
        private bool rotating;
        private bool scaling;
        private bool pendingTransformValid;
        private int activeControlId;
        private string transformingStableId;
        private ESWorkbenchSelection transformingSelection;
        private Vector2 transformStartMouse;
        private Vector3 transformStartValue;
        private Vector3 pendingTransformValue;
        private Vector2 lastMouse;
        private IVisualElementScheduledItem pendingRebuild;
        private bool pendingResetCamera;
        private bool disposed;

        public ESWorldAuthoringViewport(Action<Vector3> worldClick)
            : this(worldClick, null)
        {
        }

        public ESWorldAuthoringViewport(
            Action<Vector3> worldClick,
            ESWorkbenchViewportContext context,
            bool readOnlyGameView = false)
        {
            name = readOnlyGameView ? "ESWorldGameViewport" : "ESWorldAuthoringViewport";
            this.worldClick = worldClick;
            this.context = context;
            this.readOnlyGameView = readOnlyGameView;
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
            Add(renderHost);
        }

        public void Bind(ESWorldMapAsset nextDraft, bool resetCamera)
        {
            bool assetChanged = draft != nextDraft;
            draft = nextDraft;
            if (assetChanged || resetCamera || preview == null) Rebuild(resetCamera || assetChanged);
            else RequestRebuild(false);
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

            ESWorldMapDefinition definition = draft.Definition;
            Vector2 min = definition.worldMin;
            Vector2 max = definition.worldMax;
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = new Vector2(256f, 256f); }
            if (resetCamera)
            {
                focus = new Vector3((min.x + max.x) * 0.5f, definition.terrainHeightScale * 0.2f, (min.y + max.y) * 0.5f);
                distance = Mathf.Max(80f, Mathf.Max(max.x - min.x, max.y - min.y) * 1.15f);
                if (readOnlyGameView)
                {
                    pitch = 24f;
                    yaw = 35f;
                    distance *= 0.72f;
                }
            }

            CreateTerrain(definition);
            CreatePlacements(definition);
            renderHost.MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            Rebuild(true);
        }

        private void CreateTerrain(ESWorldMapDefinition definition)
        {
            ESWorldMapHeightfield field = definition.heightfield;
            if (field == null || field.width < 2 || field.height < 2) return;
            int resolution = ResolveTerrainResolution(Mathf.Max(field.width, field.height));
            terrainData = new TerrainData
            {
                hideFlags = HideFlags.HideAndDontSave,
                heightmapResolution = resolution,
                size = new Vector3(
                    Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x),
                    Mathf.Max(1f, definition.terrainHeightScale),
                    Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y))
            };
            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    int sx = Mathf.RoundToInt(x / (float)(resolution - 1) * (field.width - 1));
                    int sy = Mathf.RoundToInt(y / (float)(resolution - 1) * (field.height - 1));
                    heights[y, x] = ESWorldHeightfieldReadOnly.Get(field, sx, sy);
                }
            terrainData.SetHeights(0, 0, heights);
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
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
            }
        }

        private void TrackPreviewObject(GameObject instance, string stableId)
        {
            previewObjects.Add(instance);
            previewStableIds.Add(stableId ?? string.Empty);
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (draft?.Definition != null && preview?.IsReady != true && pendingRebuild == null)
                RequestRebuild(false);
            if (preview?.IsReady != true || draft?.Definition == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.055f, 0.07f, 0.08f, 1f));
                GUI.Label(rect, "选择地图资产以启动 3D 作者视口", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            ApplyPreviewCamera(rect);
            preview.RenderCurrentCameraGUI(rect, ESEditorPreviewRenderOptions.Balanced);
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleInput(rect, controlId);
            DrawSelectionOutline();
            DrawOverlay(rect);
        }

        private void HandleInput(Rect rect, int controlId)
        {
            Event evt = Event.current;
            bool transforming = moving || rotating || scaling;
            if (!rect.Contains(evt.mousePosition) && !orbiting && !panning && !transforming) return;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && transforming)
            {
                CancelInteraction();
                evt.Use();
                return;
            }
            if (evt.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance * (1f + evt.delta.y * 0.055f), 8f, 8000f);
                evt.Use();
                renderHost.MarkDirtyRepaint();
                return;
            }
            if (evt.type == EventType.MouseDown)
            {
                lastMouse = evt.mousePosition;
                orbiting = evt.button == 1 || (evt.button == 0 && evt.alt);
                panning = evt.button == 2;
                if (orbiting || panning)
                {
                    evt.Use();
                }
                else if (evt.button == 0)
                {
                    if (readOnlyGameView)
                    {
                        evt.Use();
                        return;
                    }
                    if (IsSelectionInteraction()
                        && TryHitPlacement(rect, evt.mousePosition, out string stableId, out GameObject instance))
                    {
                        string placementId = stableId.Substring("world.prefab.".Length);
                        context.Selection.Select(new ESWorkbenchSelection(stableId, "world.prefab", null, placementId));
                        BeginTransform(stableId, instance, evt.mousePosition);
                    }
                    else if (ShouldInvokeWorldClickOnGround()
                        && TryResolveWorldPoint(rect, evt.mousePosition, out Vector3 point))
                    {
                        worldClick?.Invoke(point);
                    }
                    evt.Use();
                }
                if (orbiting || panning || moving || rotating || scaling)
                {
                    activeControlId = controlId;
                    GUIUtility.hotControl = controlId;
                }
            }
            if (evt.type == EventType.MouseDrag && transforming)
            {
                UpdateTransformPreview(rect, evt.mousePosition);
                evt.Use();
                renderHost.MarkDirtyRepaint();
            }
            else if (evt.type == EventType.MouseDrag && (orbiting || panning))
            {
                Vector2 delta = evt.mousePosition - lastMouse;
                lastMouse = evt.mousePosition;
                if (orbiting)
                {
                    yaw += delta.x * 0.35f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.25f, 12f, 82f);
                }
                else
                {
                    Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
                    focus += (-(rotation * Vector3.right) * delta.x + rotation * Vector3.up * delta.y) * (distance * 0.0018f);
                }
                evt.Use();
                renderHost.MarkDirtyRepaint();
            }
            if (evt.type == EventType.MouseUp && transforming)
            {
                CommitTransform();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && (orbiting || panning))
            {
                orbiting = false;
                panning = false;
                ReleaseMouseControl();
                evt.Use();
            }
        }

        private bool IsSelectionInteraction()
        {
            if (context == null) return false;
            string toolId = context.Actions.Tools.ActiveToolId ?? string.Empty;
            return toolId == "core.select" || toolId == "core.move" || toolId == "core.rotate"
                || toolId == "core.scale" || toolId.EndsWith(".select", StringComparison.Ordinal);
        }

        private bool ShouldInvokeWorldClickOnGround()
        {
            if (context == null) return true;
            string toolId = context.Actions.Tools.ActiveToolId ?? string.Empty;
            return toolId != "core.move" && toolId != "core.rotate" && toolId != "core.scale";
        }

        private void BeginTransform(string stableId, GameObject instance, Vector2 mousePosition)
        {
            ESWorkbenchSelection selection = context.Selection.Current;
            string toolId = context.Actions.Tools.ActiveToolId ?? string.Empty;
            bool moveTool = toolId == "core.move" || toolId == "core.select"
                || toolId.EndsWith(".select", StringComparison.Ordinal);
            bool locked = context != null && context.IsHierarchyLocked(selection?.StableId);
            moving = !locked && moveTool && context.Actions.Authoring.CanMove(selection);
            rotating = !locked && toolId == "core.rotate" && context.Actions.Authoring.CanRotate(selection);
            scaling = !locked && toolId == "core.scale" && context.Actions.Authoring.CanScale(selection);
            if (!moving && !rotating && !scaling) return;
            transformingStableId = stableId;
            transformingSelection = selection;
            transformStartMouse = mousePosition;
            transformStartValue = moving ? instance.transform.position - preview.GroupOrigin
                : rotating ? instance.transform.eulerAngles : instance.transform.localScale;
            pendingTransformValue = transformStartValue;
            pendingTransformValid = false;
        }

        private void UpdateTransformPreview(Rect rect, Vector2 mousePosition)
        {
            if (moving)
            {
                if (!TryResolveWorldPoint(rect, mousePosition, out Vector3 point)) return;
                pendingTransformValue = context.SnapPosition(point);
            }
            else
            {
                Vector2 delta = mousePosition - transformStartMouse;
                pendingTransformValue = rotating
                    ? context.SnapRotation(transformStartValue + new Vector3(0f, delta.x * 0.6f, 0f))
                    : context.SnapScale(transformStartValue * Mathf.Exp((delta.x - delta.y) * 0.01f));
                if (scaling)
                    pendingTransformValue = new Vector3(
                        Mathf.Max(0.01f, pendingTransformValue.x),
                        Mathf.Max(0.01f, pendingTransformValue.y),
                        Mathf.Max(0.01f, pendingTransformValue.z));
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
            StopTransform();
            ReleaseMouseControl();
            if (!shouldCommit)
            {
                RequestRebuild(false);
                return;
            }
            bool succeeded = commitMove
                ? context.Actions.Authoring.TryMove(selection, value, out _)
                : commitRotate
                    ? context.Actions.Authoring.TryRotate(selection, value, out _)
                    : context.Actions.Authoring.TryScale(selection, value, out _);
            if (!succeeded) RequestRebuild(false);
        }

        private bool TryHitPlacement(Rect rect, Vector2 guiPoint, out string stableId, out GameObject instance)
        {
            stableId = string.Empty;
            instance = null;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Vector3 viewportPoint = new Vector3(
                Mathf.InverseLerp(rect.xMin, rect.xMax, guiPoint.x),
                1f - Mathf.InverseLerp(rect.yMin, rect.yMax, guiPoint.y),
                0f);
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            float nearest = float.MaxValue;
            for (int i = 0; i < previewObjects.Count && i < previewStableIds.Count; i++)
            {
                GameObject candidate = previewObjects[i];
                string candidateId = previewStableIds[i];
                if (candidate == null || string.IsNullOrEmpty(candidateId)) continue;
                if (!CalculateBounds(candidate).IntersectRay(ray, out float hitDistance) || hitDistance >= nearest) continue;
                nearest = hitDistance;
                stableId = candidateId;
                instance = candidate;
            }
            return instance != null;
        }

        private void ApplyPreviewTransform(string stableId, Vector3 value)
        {
            int index = previewStableIds.IndexOf(stableId);
            if (index < 0 || index >= previewObjects.Count || previewObjects[index] == null) return;
            Transform target = previewObjects[index].transform;
            if (moving) target.position = preview.GroupOrigin + value;
            else if (rotating) target.rotation = Quaternion.Euler(value);
            else if (scaling) target.localScale = value;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void DrawSelectionOutline()
        {
            if (context == null) return;
            string stableId = context.Selection.Current?.StableId;
            int index = string.IsNullOrEmpty(stableId) ? -1 : previewStableIds.IndexOf(stableId);
            if (index < 0 || index >= previewObjects.Count || previewObjects[index] == null) return;
            Color previous = Handles.color;
            Handles.SetCamera(preview.Camera);
            Handles.color = ESWorldMapEditorPresentation.Selection;
            Bounds bounds = CalculateBounds(previewObjects[index]);
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.color = previous;
        }

        public void CancelInteraction()
        {
            bool restorePreview = moving || rotating || scaling;
            orbiting = false;
            panning = false;
            StopTransform();
            ReleaseMouseControl();
            if (restorePreview) RequestRebuild(false);
        }

        private void StopTransform()
        {
            moving = false;
            rotating = false;
            scaling = false;
            pendingTransformValid = false;
            transformingStableId = string.Empty;
            transformingSelection = null;
        }

        private void ReleaseMouseControl()
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            activeControlId = 0;
        }

        private bool TryResolveWorldPoint(Rect rect, Vector2 guiPoint, out Vector3 point)
        {
            point = default;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Vector3 viewportPoint = new Vector3(
                Mathf.InverseLerp(rect.xMin, rect.xMax, guiPoint.x),
                1f - Mathf.InverseLerp(rect.yMin, rect.yMax, guiPoint.y),
                0f);
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            Plane plane = new Plane(Vector3.up, preview.GroupOrigin);
            if (!plane.Raycast(ray, out float distanceToPlane)) return false;
            point = ray.GetPoint(distanceToPlane) - preview.GroupOrigin;
            if (draft?.Definition?.heightfield != null)
            {
                ESWorldMapDefinition definition = draft.Definition;
                float u = Mathf.InverseLerp(definition.worldMin.x, definition.worldMax.x, point.x);
                float v = Mathf.InverseLerp(definition.worldMin.y, definition.worldMax.y, point.z);
                point.y = ESWorldHeightfieldReadOnly.SampleNormalized(definition.heightfield, u, v)
                    * definition.terrainHeightScale;
            }
            return true;
        }

        public bool TryResolveWorldPoint(Vector2 localPoint, out Vector3 point)
        {
            Rect rect = renderHost.contentRect;
            return TryResolveWorldPoint(rect, localPoint, out point);
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
                preview.Camera.fieldOfView = 42f;
                preview.Camera.nearClipPlane = 0.1f;
                preview.Camera.backgroundColor = new Color(0.055f, 0.07f, 0.08f, 1f);
            }
        }

        private void ApplyPreviewCamera(Rect rect)
        {
            if (preview?.Camera == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            preview.Camera.transform.position = preview.GroupOrigin + focus - rotation * Vector3.forward * distance;
            preview.Camera.transform.rotation = rotation;
            preview.Camera.aspect = Mathf.Max(0.25f, rect.width / Mathf.Max(1f, rect.height));
            preview.Camera.farClipPlane = 10000f;
        }

        private void DrawOverlay(Rect rect)
        {
            Rect badge = new Rect(rect.x + 10f, rect.y + 10f, readOnlyGameView ? 116f : 92f, 24f);
            EditorGUI.DrawRect(badge, new Color(0.02f, 0.025f, 0.03f, 0.82f));
            GUI.Label(new Rect(badge.x + 8f, badge.y + 3f, badge.width - 12f, 18f),
                readOnlyGameView ? "游戏预览" : "三维草稿", EditorStyles.miniLabel);
        }

        private void ClearPreviewContent()
        {
            preview?.DestroyAllModelGroups();
            if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
            terrainObject = null;
            previewObjects.Clear();
            previewStableIds.Clear();
            if (terrainData != null) UnityEngine.Object.DestroyImmediate(terrainData);
            terrainData = null;
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
