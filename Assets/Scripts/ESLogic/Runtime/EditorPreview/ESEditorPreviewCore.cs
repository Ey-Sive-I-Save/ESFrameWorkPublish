#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESEditorPreviewSceneMode
    {
        PreviewScene,
        HiddenObjectsInActiveScene
    }

    public enum ESEditorPreviewQuality
    {
        Fast,
        Balanced,
        High
    }

    /// <summary>
    /// 可选预览增强器集合。底座生命周期始终启用；增强器按集合显式打开，
    /// 低端路径可使用 LowEnd 而不会承担地面、标尺或粒子初始化成本。
    /// </summary>
    [Flags]
    public enum ESEditorPreviewEnhancerSet
    {
        LowEnd = 0,
        GroundPlane = 1 << 0,
        ScaleReference = 1 << 1,
        ParticleSimulation = 1 << 2,
        HighQualityLighting = 1 << 3,
        AudioSpatialization = 1 << 4,
        PostProcessing = 1 << 5,
        Full = GroundPlane | ScaleReference | ParticleSimulation
            | HighQualityLighting | AudioSpatialization | PostProcessing
    }

    public static class ESEditorPreviewEnhancerBudgets
    {
        /// <summary>将渲染质量映射为增强器预算；Fast 不创建可选辅助资源。</summary>
        public static ESEditorPreviewEnhancerSet ForQuality(ESEditorPreviewQuality quality)
        {
            switch (quality)
            {
                case ESEditorPreviewQuality.Fast:
                    return ESEditorPreviewEnhancerSet.LowEnd;
                case ESEditorPreviewQuality.Balanced:
                    return ESEditorPreviewEnhancerSet.GroundPlane
                        | ESEditorPreviewEnhancerSet.ScaleReference;
                default:
                    return ESEditorPreviewEnhancerSet.Full;
            }
        }
    }

    public readonly struct ESEditorPreviewCameraPose
    {
        /// <summary>最终用于渲染的世界坐标中心。</summary>
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly float Zoom;

        public ESEditorPreviewCameraPose(Vector3 center, float radius, float yaw, float pitch, float zoom)
        {
            Center = center;
            Radius = Mathf.Max(0.05f, radius);
            Yaw = yaw;
            Pitch = pitch;
            Zoom = Mathf.Max(0.05f, zoom);
        }
    }

    public enum ESEditorPreviewViewportInputResult
    {
        None,
        Orbit,
        Pan,
        Zoom
    }

    /// <summary>
    /// 与具体窗口无关的预览轨道视角。FocusLocal 始终位于 RenderContext 的 PreviewLocal 空间，
    /// 不允许业务页自行叠加 GroupOrigin。
    /// </summary>
    public sealed class ESEditorPreviewOrbitView
    {
        public const float MinimumZoom = 0.05f;

        public Vector3 FocusLocal { get; private set; }
        public float Radius { get; private set; } = 2.5f;
        public float Zoom { get; private set; } = 1f;
        public float Yaw { get; private set; } = 35f;
        public float Pitch { get; private set; } = 18f;

        public void Reset(Vector3 focusLocal, float radius, float yaw = 35f, float pitch = 18f, float zoom = 1f)
        {
            FocusLocal = IsFinite(focusLocal) ? focusLocal : Vector3.zero;
            Radius = Mathf.Max(0.05f, radius);
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, -80f, 80f);
            Zoom = Mathf.Max(0.05f, zoom);
        }

        public void ResetRecommended(Vector3 focusLocal = default, float radius = 1.6f)
        {
            Reset(focusLocal, radius, 45f, 22f, 1.05f);
        }

        public void FrameWorldBounds(
            ESEditorPreviewRenderContext context,
            Bounds worldBounds,
            float minimumRadius = 0.5f,
            float maximumRadius = 18f)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            FocusLocal = context.WorldToPreviewLocalPoint(worldBounds.center);
            float normalizedMinimum = Mathf.Max(0.05f, minimumRadius);
            float normalizedMaximum = Mathf.Max(normalizedMinimum, maximumRadius);
            Radius = Mathf.Clamp(worldBounds.extents.magnitude * 1.05f, normalizedMinimum, normalizedMaximum);
            ClampZoom(context.Camera != null ? context.Camera.farClipPlane : 80f);
        }

        /// <summary>根据内容的高宽比例选择稳定的三分之四推荐角度，并重置平移和缩放。</summary>
        public void FrameRecommendedWorldBounds(
            ESEditorPreviewRenderContext context,
            Bounds worldBounds,
            float minimumRadius = 0.5f,
            float maximumRadius = 500f)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Vector3 size = worldBounds.size;
            float horizontal = Mathf.Max(0.01f, Mathf.Max(size.x, size.z));
            float heightRatio = Mathf.Max(0f, size.y) / horizontal;
            float yaw = 40f;
            float pitch = heightRatio <= 0.45f ? 38f
                : heightRatio >= 1.8f ? 14f
                : Mathf.Lerp(30f, 18f, Mathf.InverseLerp(0.45f, 1.8f, heightRatio));
            float normalizedMinimum = Mathf.Max(0.05f, minimumRadius);
            float normalizedMaximum = Mathf.Max(normalizedMinimum, maximumRadius);
            float radius = Mathf.Clamp(worldBounds.extents.magnitude * 1.08f,
                normalizedMinimum, normalizedMaximum);
            Reset(context.WorldToPreviewLocalPoint(worldBounds.center), radius, yaw, pitch, 1.04f);
            ClampZoom(context.Camera != null ? context.Camera.farClipPlane : 80f);
        }

        public void Orbit(Vector2 pointerDelta, float sensitivity = 1f)
        {
            float multiplier = Mathf.Clamp(sensitivity, 0.1f, 4f);
            Yaw += pointerDelta.x * 0.35f * multiplier;
            Pitch = Mathf.Clamp(Pitch - pointerDelta.y * 0.25f * multiplier, -80f, 80f);
        }

        public void Pan(Vector2 pointerDelta, float sensitivity = 1f)
        {
            Quaternion orbit = Quaternion.Euler(Pitch, Yaw, 0f);
            float panScale = Radius * Zoom * 0.0018f * Mathf.Clamp(sensitivity, 0.1f, 4f);
            FocusLocal += (-(orbit * Vector3.right) * pointerDelta.x
                + orbit * Vector3.up * pointerDelta.y) * panScale;
        }

        public void ZoomByWheel(float wheelDelta, float farClipPlane = 80f)
        {
            Zoom = Mathf.Clamp(
                Zoom * Mathf.Exp(wheelDelta * 0.08f),
                MinimumZoom,
                GetMaximumZoom(farClipPlane));
        }

        public void ZoomByFactor(float factor, float farClipPlane = 80f)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                return;

            Zoom = Mathf.Clamp(
                Zoom * factor,
                MinimumZoom,
                GetMaximumZoom(farClipPlane));
        }

        public void SetZoom(float zoom, float farClipPlane = 80f)
        {
            Zoom = Mathf.Clamp(zoom, MinimumZoom, GetMaximumZoom(farClipPlane));
        }

        public void ClampZoom(float farClipPlane = 80f)
        {
            Zoom = Mathf.Clamp(Zoom, MinimumZoom, GetMaximumZoom(farClipPlane));
        }

        public float GetMaximumZoom(float farClipPlane = 80f)
        {
            return Mathf.Clamp(
                Mathf.Max(1.2f, farClipPlane) * 0.72f / (Mathf.Max(0.05f, Radius) * 2.8f),
                MinimumZoom,
                12f);
        }

        /// <summary>
        /// 返回当前姿态下的实际相机距离。UI 可以用它显示可观察的缩放结果，
        /// 也避免业务窗口各自重新实现 FOV/aspect 的距离计算。
        /// </summary>
        public float GetCameraDistance(float aspect = 1f, float fieldOfView = 30f)
        {
            float safeAspect = Mathf.Max(0.25f, aspect);
            float verticalHalfFov = Mathf.Max(1f, fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * safeAspect);
            float limitingHalfFov = Mathf.Max(1f * Mathf.Deg2Rad, Mathf.Min(verticalHalfFov, horizontalHalfFov));
            float fittedDistance = Radius / Mathf.Max(0.02f, Mathf.Sin(limitingHalfFov));
            return Mathf.Max(0.03f, fittedDistance * Zoom);
        }

        public float GetNormalizedZoom(float farClipPlane = 80f)
        {
            float maximumZoom = Mathf.Max(MinimumZoom, GetMaximumZoom(farClipPlane));
            return Mathf.InverseLerp(
                Mathf.Log(MinimumZoom),
                Mathf.Log(maximumZoom),
                Mathf.Log(Mathf.Clamp(Zoom, MinimumZoom, maximumZoom)));
        }

        /// <summary>
        /// 返回符合用户直觉的放大倍率：0 表示最远，1 表示最近。
        /// 内部 Zoom 仍表示相机距离倍率，避免破坏既有相机姿态合同。
        /// </summary>
        public float GetNormalizedMagnification(float farClipPlane = 80f)
        {
            return 1f - GetNormalizedZoom(farClipPlane);
        }

        public void SetNormalizedZoom(float normalized, float farClipPlane = 80f)
        {
            float maximumZoom = Mathf.Max(MinimumZoom, GetMaximumZoom(farClipPlane));
            SetZoom(
                Mathf.Exp(Mathf.Lerp(
                    Mathf.Log(MinimumZoom),
                    Mathf.Log(maximumZoom),
                    Mathf.Clamp01(normalized))),
                farClipPlane);
        }

        public void SetNormalizedMagnification(float normalized, float farClipPlane = 80f)
        {
            SetNormalizedZoom(1f - Mathf.Clamp01(normalized), farClipPlane);
        }

        public ESEditorPreviewCameraPose CreateCameraPose(ESEditorPreviewRenderContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return context.CreateCameraPose(FocusLocal, Radius, Yaw, Pitch, Zoom);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>共享 IMGUI 鼠标捕获；UI Toolkit 可直接复用 OrbitView 的纯数学入口。</summary>
    public sealed class ESEditorPreviewIMGUIOrbitInput
    {
        private static readonly int ControlHint = "ES.EditorPreview.OrbitInput".GetHashCode();
        private bool orbiting;
        private bool panning;
        private Vector2 lastPointer;
        private int activeControlId;

        public ESEditorPreviewViewportInputResult Handle(
            Rect viewportRect,
            ESEditorPreviewOrbitView view,
            bool requireModifierForWheelZoom = false,
            float orbitSensitivity = 1f,
            float panSensitivity = 1f,
            float farClipPlane = 80f)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            Event current = Event.current;
            if (current == null)
                return ESEditorPreviewViewportInputResult.None;

            bool pointerInside = viewportRect.Contains(current.mousePosition);
            if (!pointerInside && !orbiting && !panning)
                return ESEditorPreviewViewportInputResult.None;

            int controlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive, viewportRect);
            if (current.type == EventType.ScrollWheel && pointerInside)
            {
                if (requireModifierForWheelZoom && !current.control && !current.alt)
                    return ESEditorPreviewViewportInputResult.None;
                // Event.delta.y 向下为正；向下拉远，向上靠近，和 Unity SceneView 一致。
                view.ZoomByWheel(current.delta.y, farClipPlane);
                current.Use();
                return ESEditorPreviewViewportInputResult.Zoom;
            }

            if (current.type == EventType.MouseDown && pointerInside)
            {
                bool wantsOrbit = current.button == 1 || (current.button == 0 && current.alt);
                bool wantsPan = current.button == 2;
                if (!wantsOrbit && !wantsPan)
                    return ESEditorPreviewViewportInputResult.None;

                lastPointer = current.mousePosition;
                orbiting = wantsOrbit;
                panning = wantsPan;
                activeControlId = controlId;
                GUIUtility.hotControl = controlId;
                current.Use();
                return ESEditorPreviewViewportInputResult.None;
            }

            if (current.type == EventType.MouseDrag && (orbiting || panning))
            {
                Vector2 delta = current.mousePosition - lastPointer;
                lastPointer = current.mousePosition;
                ESEditorPreviewViewportInputResult result;
                if (orbiting)
                {
                    view.Orbit(delta, orbitSensitivity);
                    result = ESEditorPreviewViewportInputResult.Orbit;
                }
                else
                {
                    view.Pan(delta, panSensitivity);
                    result = ESEditorPreviewViewportInputResult.Pan;
                }

                current.Use();
                return result;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore
                    || current.type == EventType.MouseLeaveWindow)
                && (orbiting || panning))
            {
                Release();
                if (current.type != EventType.Ignore)
                    current.Use();
            }
            else if ((orbiting || panning) && GUIUtility.hotControl != activeControlId)
            {
                Release();
            }

            return ESEditorPreviewViewportInputResult.None;
        }

        public void Release()
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            activeControlId = 0;
            orbiting = false;
            panning = false;
        }
    }

    /// <summary>预览视口通用三轴辅助，保持与当前轨道视角一致。</summary>
    public static class ESEditorPreviewGizmos
    {
        private static readonly GUIContent AxisX = new GUIContent("X");
        private static readonly GUIContent AxisY = new GUIContent("Y");
        private static readonly GUIContent AxisZ = new GUIContent("Z");

        public static void DrawAxis(Rect viewportRect, ESEditorPreviewOrbitView view)
        {
            if (view == null || viewportRect.width < 72f || viewportRect.height < 72f)
                return;
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Rect panelRect = new Rect(viewportRect.xMax - 82f, viewportRect.yMin + 40f, 74f, 74f);
            EditorGUI.DrawRect(panelRect, new Color(0.035f, 0.045f, 0.055f, 0.86f));
            EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
            EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.yMax - 1f, panelRect.width, 1f), new Color(0f, 0f, 0f, 0.38f));

            Quaternion orbit = Quaternion.Euler(view.Pitch, view.Yaw, 0f);
            Quaternion cameraRotation = Quaternion.LookRotation(orbit * Vector3.forward, Vector3.up);
            DrawAxisLines(panelRect, Quaternion.Inverse(cameraRotation));
        }

        /// <summary>使用真实预览 Camera 绘制方向轴，避免 HUD 角标与实际投影脱节。</summary>
        public static void DrawAxis(Rect viewportRect, Camera camera)
        {
            if (camera == null || viewportRect.width < 72f || viewportRect.height < 72f
                || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Rect panelRect = new Rect(viewportRect.xMax - 82f, viewportRect.yMin + 40f, 74f, 74f);
            EditorGUI.DrawRect(panelRect, new Color(0.035f, 0.045f, 0.055f, 0.86f));
            EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
            EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.yMax - 1f, panelRect.width, 1f), new Color(0f, 0f, 0f, 0.38f));
            DrawAxisLines(panelRect, Quaternion.Inverse(camera.transform.rotation));
        }

        private static void DrawAxisLines(Rect panelRect, Quaternion worldToView)
        {
            const float size = 27f;
            Vector2 origin = new Vector2(panelRect.center.x, panelRect.center.y + 4f);
            Color previousHandleColor = Handles.color;
            Color previousGuiColor = GUI.color;
            Handles.BeginGUI();
            DrawAxisLine(origin, worldToView * Vector3.right, new Color(0.96f, 0.30f, 0.28f, 1f), AxisX);
            DrawAxisLine(origin, worldToView * Vector3.up, new Color(0.36f, 0.92f, 0.44f, 1f), AxisY);
            DrawAxisLine(origin, worldToView * Vector3.forward, new Color(0.30f, 0.60f, 1f, 1f), AxisZ);
            Handles.EndGUI();
            Handles.color = previousHandleColor;
            GUI.color = previousGuiColor;
            EditorGUI.DrawRect(new Rect(origin.x - 2f, origin.y - 2f, 4f, 4f), new Color(0.92f, 0.94f, 0.96f, 1f));

            void DrawAxisLine(Vector2 start, Vector3 direction, Color color, GUIContent label)
            {
                Vector2 projected = new Vector2(direction.x, -direction.y);
                if (projected.sqrMagnitude < 0.0001f)
                    projected = Vector2.up * 0.1f;
                Vector2 end = start + projected.normalized * Mathf.Lerp(10f, size, Mathf.Clamp01(projected.magnitude));
                Handles.color = color;
                Handles.DrawAAPolyLine(3f, new Vector3(start.x, start.y), new Vector3(end.x, end.y));
                GUI.color = color;
                GUI.Label(new Rect(end.x - 5f, end.y - 9f, 16f, 18f), label, EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>把 PreviewScene 原点三轴投影到当前预览矩形，避免只显示屏幕角标而看不到局部坐标。</summary>
        public static void DrawWorldAxes(Rect viewportRect, Camera camera, Vector3 origin, float size)
        {
            if (camera == null || viewportRect.width < 100f || viewportRect.height < 100f
                || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            float axisSize = Mathf.Clamp(size, 0.25f, 12f);
            // 原点或任一轴端点不可完整投影时，直接切到完整的屏幕三轴，
            // 禁止只剩一两根轴线，让用户误判局部坐标方向。
            if (!TryProject(origin, out Vector2 projectedOrigin)
                || !TryProject(origin + Vector3.right * axisSize, out Vector2 projectedX)
                || !TryProject(origin + Vector3.up * axisSize, out Vector2 projectedY)
                || !TryProject(origin + Vector3.forward * axisSize, out Vector2 projectedZ))
            {
                projectedOrigin = new Vector2(viewportRect.xMin + 56f, viewportRect.yMax - 42f);
                DrawFallbackAxes(projectedOrigin, axisSize);
                return;
            }

            Color previous = Handles.color;
            Handles.BeginGUI();
            DrawProjectedAxis(projectedX, new Color(0.96f, 0.30f, 0.28f, 1f), "X");
            DrawProjectedAxis(projectedY, new Color(0.36f, 0.92f, 0.44f, 1f), "Y");
            DrawProjectedAxis(projectedZ, new Color(0.30f, 0.60f, 1f, 1f), "Z");
            Handles.EndGUI();
            Handles.color = previous;

            bool TryProject(Vector3 world, out Vector2 gui)
            {
                Vector3 viewport = camera.WorldToViewportPoint(world);
                gui = new Vector2(
                    viewportRect.x + viewport.x * viewportRect.width,
                    viewportRect.yMax - viewport.y * viewportRect.height);
                return viewport.z > camera.nearClipPlane
                    && viewport.x >= -0.2f && viewport.x <= 1.2f
                    && viewport.y >= -0.2f && viewport.y <= 1.2f;
            }

            void DrawProjectedAxis(Vector2 end, Color color, string label)
            {
                Handles.color = color;
                Handles.DrawAAPolyLine(3f,
                    new Vector3(projectedOrigin.x, projectedOrigin.y),
                    new Vector3(end.x, end.y));
                Color oldGui = GUI.color;
                GUI.color = color;
                GUI.Label(new Rect(end.x - 5f, end.y - 9f, 16f, 18f), label, EditorStyles.whiteMiniLabel);
                GUI.color = oldGui;
            }

            void DrawFallbackAxes(Vector2 fallbackOrigin, float fallbackSize)
            {
                Color oldGui = GUI.color;
                Color oldHandles = Handles.color;
                Handles.BeginGUI();
                DrawFallbackAxis(fallbackOrigin, Vector2.right, new Color(0.96f, 0.30f, 0.28f, 1f), "X", fallbackSize);
                DrawFallbackAxis(fallbackOrigin, Vector2.down, new Color(0.36f, 0.92f, 0.44f, 1f), "Y", fallbackSize);
                DrawFallbackAxis(fallbackOrigin, new Vector2(0.72f, -0.72f), new Color(0.30f, 0.60f, 1f, 1f), "Z", fallbackSize);
                Handles.EndGUI();
                GUI.color = oldGui;
                Handles.color = oldHandles;
            }

            void DrawFallbackAxis(Vector2 start, Vector2 direction, Color color, string label, float length)
            {
                Handles.color = color;
                Vector2 end = start + direction.normalized * Mathf.Clamp(length * 1.6f, 18f, 42f);
                Handles.DrawAAPolyLine(3f,
                    new Vector3(start.x, start.y),
                    new Vector3(end.x, end.y));
                GUI.color = color;
                GUI.Label(new Rect(end.x - 5f, end.y - 9f, 18f, 18f), label, EditorStyles.whiteMiniLabel);
            }
        }
    }

    public readonly struct ESEditorPreviewRenderOptions
    {
        public readonly ESEditorPreviewQuality Quality;
        public readonly float RenderScale;
        public readonly double MinRenderInterval;

        public ESEditorPreviewRenderOptions(ESEditorPreviewQuality quality, float renderScale, double minRenderInterval = 0d)
        {
            Quality = quality;
            RenderScale = Mathf.Clamp(renderScale, 0.5f, 4f);
            MinRenderInterval = Math.Max(0d, minRenderInterval);
        }

        public static ESEditorPreviewRenderOptions Fast => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Fast, 1f, 1d / 15d);
        public static ESEditorPreviewRenderOptions Balanced => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Balanced, 2f, 1d / 30d);
        public static ESEditorPreviewRenderOptions High => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.High, 3f, 0d);
    }

    public sealed class ESEditorPreviewModelHandle : IDisposable
    {
        private readonly ESEditorPreviewRenderContext ownerContext;
        private bool disposed;

        public GameObject Source { get; }
        public GameObject Instance { get; private set; }
        public Bounds Bounds { get; private set; }
        public Vector3 StableCenter { get; private set; }
        public float StableRadius { get; private set; }

        internal ESEditorPreviewModelHandle(ESEditorPreviewRenderContext ownerContext, GameObject source, GameObject instance)
        {
            this.ownerContext = ownerContext;
            Source = source;
            Instance = instance;
            RefreshBounds(lockStableView: true);
        }

        public T GetComponentInPreview<T>() where T : Component
        {
            return Instance != null ? Instance.GetComponentInChildren<T>(true) : null;
        }

        public Bounds RefreshBounds(bool lockStableView = false)
        {
            Bounds = ESEditorPreviewUtility.CalculateBounds(Instance);
            float radius = Mathf.Max(0.5f, Bounds.extents.magnitude);
            if (lockStableView || StableRadius <= 0f)
            {
                StableCenter = Bounds.center;
                StableRadius = radius;
            }

            return Bounds;
        }

        public ESEditorPreviewCameraPose GetCameraPose(float yaw, float pitch, float zoom, bool followAnimatedBounds)
        {
            Bounds bounds = RefreshBounds(lockStableView: false);
            Vector3 center = followAnimatedBounds ? bounds.center : StableCenter;
            float radius = followAnimatedBounds ? Mathf.Max(0.5f, bounds.extents.magnitude) : StableRadius;
            return new ESEditorPreviewCameraPose(center, radius, yaw, pitch, zoom);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ownerContext?.UnregisterModel(this);
            ESEditorPreviewUtility.DestroyObject(Instance);
            Instance = null;
        }

        internal void DisposeFromOwner()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewUtility.DestroyObject(Instance);
            Instance = null;
        }
    }

    public readonly struct ESEditorPreviewDiagnosticsSnapshot
    {
        public ESEditorPreviewDiagnosticsSnapshot(
            int activeScopeCount,
            int activeRenderContextCount,
            int activeResourceScopeCount,
            int activeModelGroupCount,
            int activeTemporaryObjectCount,
            int activeRenderTextureCount,
            long activeRenderTexturePixels,
            long estimatedRenderTextureBytes,
            int peakScopeCount,
            int peakRenderContextCount,
            int peakModelGroupCount,
            int peakRenderTextureCount,
            long peakRenderTexturePixels,
            long peakEstimatedRenderTextureBytes,
            long totalScopeRegistrations,
            long totalScopeReleases,
            int cleanupRunCount,
            int cleanupFailureCount,
            string lastCleanupReason,
            int lastCleanupReleasedCount)
        {
            ActiveScopeCount = activeScopeCount;
            ActiveRenderContextCount = activeRenderContextCount;
            ActiveResourceScopeCount = activeResourceScopeCount;
            ActiveModelGroupCount = activeModelGroupCount;
            ActiveTemporaryObjectCount = activeTemporaryObjectCount;
            ActiveRenderTextureCount = activeRenderTextureCount;
            ActiveRenderTexturePixels = activeRenderTexturePixels;
            EstimatedRenderTextureBytes = estimatedRenderTextureBytes;
            PeakScopeCount = peakScopeCount;
            PeakRenderContextCount = peakRenderContextCount;
            PeakModelGroupCount = peakModelGroupCount;
            PeakRenderTextureCount = peakRenderTextureCount;
            PeakRenderTexturePixels = peakRenderTexturePixels;
            PeakEstimatedRenderTextureBytes = peakEstimatedRenderTextureBytes;
            TotalScopeRegistrations = totalScopeRegistrations;
            TotalScopeReleases = totalScopeReleases;
            CleanupRunCount = cleanupRunCount;
            CleanupFailureCount = cleanupFailureCount;
            LastCleanupReason = lastCleanupReason ?? string.Empty;
            LastCleanupReleasedCount = lastCleanupReleasedCount;
        }

        public int ActiveScopeCount { get; }
        public int ActiveRenderContextCount { get; }
        public int ActiveResourceScopeCount { get; }
        public int ActiveModelGroupCount { get; }
        public int ActiveTemporaryObjectCount { get; }
        public int ActiveRenderTextureCount { get; }
        public long ActiveRenderTexturePixels { get; }
        public long EstimatedRenderTextureBytes { get; }
        public int PeakScopeCount { get; }
        public int PeakRenderContextCount { get; }
        public int PeakModelGroupCount { get; }
        public int PeakRenderTextureCount { get; }
        public long PeakRenderTexturePixels { get; }
        public long PeakEstimatedRenderTextureBytes { get; }
        public long TotalScopeRegistrations { get; }
        public long TotalScopeReleases { get; }
        public int CleanupRunCount { get; }
        public int CleanupFailureCount { get; }
        public string LastCleanupReason { get; }
        public int LastCleanupReleasedCount { get; }

        public string ToSummary()
        {
            return "Scope " + ActiveScopeCount
                + " · Context " + ActiveRenderContextCount
                + " · 模型组 " + ActiveModelGroupCount
                + " · RT " + ActiveRenderTextureCount
                + " / " + ActiveRenderTexturePixels.ToString("N0") + " px"
                + " · 估算 " + FormatBytes(EstimatedRenderTextureBytes)
                + " · 峰值 " + FormatBytes(PeakEstimatedRenderTextureBytes);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("0.0") + " KiB";
            return (bytes / (1024d * 1024d)).ToString("0.0") + " MiB";
        }
    }

    /// <summary>
    /// 全局预览生命周期入口。窗口和预览模块可以注册 IDisposable，上下文重载、退出、切 PlayMode 时统一清理。
    /// </summary>
    public static class ESEditorPreviewLifecycleHub
    {
        private static readonly HashSet<IDisposable> ActiveScopes = new HashSet<IDisposable>();
        private static readonly HashSet<IDisposable> FailedScopes = new HashSet<IDisposable>();
        private static readonly List<IDisposable> DisposeBuffer = new List<IDisposable>(32);
        private static bool registered;
        private static long totalScopeRegistrations;
        private static long totalScopeReleases;
        private static int cleanupRunCount;
        private static int cleanupFailureCount;
        private static string lastCleanupReason = string.Empty;
        private static int lastCleanupReleasedCount;
        private static int peakScopeCount;
        private static int peakRenderContextCount;
        private static int peakModelGroupCount;
        private static int peakRenderTextureCount;
        private static long peakRenderTexturePixels;
        private static long peakEstimatedRenderTextureBytes;

        public static int ActiveScopeCount => ActiveScopes.Count;
        /// <summary>当前上一次清理失败、仍等待下一次 CleanupAll 重试的 Scope 数量。</summary>
        public static int FailedScopeCount => FailedScopes.Count;

        public static void RegisterGlobalHooks()
        {
            if (registered)
            {
                AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
                EditorApplication.quitting -= CleanupBeforeEditorQuit;
                EditorApplication.playModeStateChanged -= CleanupOnPlayModeChanged;
            }

            registered = true;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.quitting += CleanupBeforeEditorQuit;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeChanged;
        }

        public static void RegisterScope(IDisposable scope)
        {
            if (scope == null)
                return;

            RegisterGlobalHooks();
            FailedScopes.Remove(scope);
            if (ActiveScopes.Add(scope))
            {
                totalScopeRegistrations++;
                RefreshPeaks();
            }
        }

        public static void UnregisterScope(IDisposable scope)
        {
            if (scope == null)
                return;

            if (ActiveScopes.Remove(scope))
            {
                totalScopeReleases++;
                RefreshPeaks();
            }
            FailedScopes.Remove(scope);
        }

        public static ESEditorPreviewDiagnosticsSnapshot CaptureDiagnosticsSnapshot()
        {
            return BuildDiagnosticsSnapshot(true);
        }

        internal static void NotifyResourceChanged()
        {
            RefreshPeaks();
        }

        public static int CleanupAll(string reason, bool includeMarkedObjects = true)
        {
            cleanupRunCount++;
            lastCleanupReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();
            DisposeBuffer.Clear();
            DisposeBuffer.AddRange(ActiveScopes);
            foreach (IDisposable failedScope in FailedScopes)
            {
                if (failedScope != null && !DisposeBuffer.Contains(failedScope))
                    DisposeBuffer.Add(failedScope);
            }
            ActiveScopes.Clear();
            FailedScopes.Clear();

            int disposed = 0;
            int releasedScopes = 0;
            int failures = 0;
            for (int i = DisposeBuffer.Count - 1; i >= 0; i--)
            {
                try
                {
                    DisposeBuffer[i]?.Dispose();
                    disposed++;
                    releasedScopes++;
                }
                catch (Exception e)
                {
                    failures++;
                    if (DisposeBuffer[i] != null)
                        FailedScopes.Add(DisposeBuffer[i]);
                    Debug.LogWarning("[ESEditorPreviewLifecycle] Dispose failed. reason=" + reason + " error=" + e.Message);
                }
            }
            DisposeBuffer.Clear();

            if (includeMarkedObjects)
                disposed += ESEditorPreviewUtility.CleanupAllMarkedPreviewObjects();

            totalScopeReleases += releasedScopes;
            cleanupFailureCount += failures;
            lastCleanupReleasedCount = disposed;
            RefreshPeaks();
            return disposed;
        }

        private static void RefreshPeaks()
        {
            BuildDiagnosticsSnapshot(true);
        }

        private static ESEditorPreviewDiagnosticsSnapshot BuildDiagnosticsSnapshot(bool updatePeaks)
        {
            int contextCount = 0;
            int resourceScopeCount = 0;
            int modelGroupCount = 0;
            int temporaryObjectCount = 0;
            int renderTextureCount = 0;
            long renderTexturePixels = 0L;
            long estimatedBytes = 0L;
            foreach (IDisposable scope in ActiveScopes)
            {
                if (scope is ESEditorPreviewRenderContext context)
                {
                    contextCount++;
                    modelGroupCount += context.ActiveModelGroupCount;
                    temporaryObjectCount += context.ActiveTemporaryObjectCount;
                    if (context.HasRenderTexture)
                    {
                        renderTextureCount++;
                        Vector2Int size = context.RenderTextureSize;
                        renderTexturePixels += (long)size.x * size.y;
                        estimatedBytes += context.EstimatedRenderTextureBytes;
                    }
                }
                else if (scope is ESEditorPreviewResourceScope resourceScope)
                {
                    resourceScopeCount++;
                    temporaryObjectCount += resourceScope.RegisteredObjectCount;
                    renderTextureCount += resourceScope.RegisteredRenderTextureCount;
                    renderTexturePixels += resourceScope.RegisteredRenderTexturePixels;
                    estimatedBytes += resourceScope.EstimatedRegisteredRenderTextureBytes;
                }
            }

            if (updatePeaks)
            {
                peakScopeCount = Math.Max(peakScopeCount, ActiveScopes.Count);
                peakRenderContextCount = Math.Max(peakRenderContextCount, contextCount);
                peakModelGroupCount = Math.Max(peakModelGroupCount, modelGroupCount);
                peakRenderTextureCount = Math.Max(peakRenderTextureCount, renderTextureCount);
                peakRenderTexturePixels = Math.Max(peakRenderTexturePixels, renderTexturePixels);
                peakEstimatedRenderTextureBytes = Math.Max(peakEstimatedRenderTextureBytes, estimatedBytes);
            }

            return new ESEditorPreviewDiagnosticsSnapshot(
                ActiveScopes.Count,
                contextCount,
                resourceScopeCount,
                modelGroupCount,
                temporaryObjectCount,
                renderTextureCount,
                renderTexturePixels,
                estimatedBytes,
                peakScopeCount,
                peakRenderContextCount,
                peakModelGroupCount,
                peakRenderTextureCount,
                peakRenderTexturePixels,
                peakEstimatedRenderTextureBytes,
                totalScopeRegistrations,
                totalScopeReleases,
                cleanupRunCount,
                cleanupFailureCount,
                lastCleanupReason,
                lastCleanupReleasedCount);
        }

        [MenuItem(MenuItemPathDefine.PREVIEW_CLEANUP_PATH + "清理全部ES预览上下文", false, -20)]
        public static void CleanupAllMenu()
        {
            int removed = CleanupAll("Menu");
            Debug.Log("[ESEditorPreviewLifecycle] 已清理预览上下文和残留对象: " + removed);
        }

        private static void CleanupBeforeAssemblyReload()
        {
            CleanupAll("AssemblyReload");
        }

        private static void CleanupBeforeEditorQuit()
        {
            CleanupAll("EditorQuit");
        }

        private static void CleanupOnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                CleanupAll("PlayModeChanged");
        }
    }

    /// <summary>
    /// 可复用预览渲染上下文。负责隔离点、相机、灯光、RT、截图和生命周期。
    /// 业务只需要提供预览对象、中心点、半径和采样后的当前姿态。
    /// </summary>
    public sealed class ESEditorPreviewRenderContext : IDisposable
    {
        private const float GroupSpacing = 100f;
        private const float CameraFarClip = 80f;
        private const float GroundSize = 25f;
        private const float GroundThickness = 0.02f;
        private const int MaxRenderTextureDimension = 2048;
        private const long PreviewRenderTextureBudgetBytes = 192L * 1024L * 1024L;
        private const long GlobalPreviewRenderTextureBudgetBytes = 512L * 1024L * 1024L;
        private const int MaxCellProbeAttempts = 4096;
        private static readonly object AllocationLock = new object();
        private static readonly HashSet<Vector2Int> OccupiedCells = new HashSet<Vector2Int>();
        private static readonly Queue<Vector2Int> ReleasedCells = new Queue<Vector2Int>();
        private static int nextAllocationId;

        private readonly string owner;
        private readonly ESEditorPreviewSceneMode sceneMode;
        private readonly int previewLayer;
        private ESEditorPreviewEnhancerSet enhancerSet;
        private readonly int allocationId;
        private readonly Vector2Int allocatedCell;
        private readonly Vector3 groupOrigin;
        private readonly string allocationReport;
        private Scene previewScene;
        private GameObject cameraObject;
        private GameObject keyLightObject;
        private GameObject fillLightObject;
        private GameObject groundPlaneObject;
        private Material groundPlaneMaterial;
        private GameObject scaleReferenceObject;
        private Material scaleReferenceMaterial;
        private Vector3 scaleReferenceLocalGroundPosition;
        private Material fallbackParticleMaterial;
        private readonly List<ESEditorPreviewModelHandle> modelHandles = new List<ESEditorPreviewModelHandle>(4);
        private RenderTexture renderTexture;
        private int renderTextureWidth;
        private int renderTextureHeight;
        private ESEditorPreviewQuality renderTextureQuality;
        private double lastRenderTime;
        private bool disposed;
        private bool cellReleased;
        private bool CameraSceneBound;

        public Camera Camera { get; private set; }
        public Scene PreviewScene => previewScene;
        public Vector3 GroupOrigin => groupOrigin;
        public bool IsScaleReferenceVisible => scaleReferenceObject != null && scaleReferenceObject.activeSelf;
        public bool IsReady => Camera != null && (sceneMode != ESEditorPreviewSceneMode.PreviewScene || previewScene.IsValid());
        public bool IsDisposed => disposed;
        public ESEditorPreviewSceneMode SceneMode => sceneMode;
        public ESEditorPreviewEnhancerSet EnhancerSet => enhancerSet;
        public bool PreviewSceneIsValid => sceneMode != ESEditorPreviewSceneMode.PreviewScene || previewScene.IsValid();
        public int ActiveModelGroupCount => modelHandles.Count;
        public int ActiveTemporaryObjectCount => (cameraObject != null ? 1 : 0)
            + (keyLightObject != null ? 1 : 0)
            + (fillLightObject != null ? 1 : 0)
            + (groundPlaneObject != null ? 1 : 0)
            + (scaleReferenceObject != null ? 1 : 0)
            + modelHandles.Count;
        public bool HasRenderTexture => renderTexture != null;
        public Vector2Int RenderTextureSize => new Vector2Int(renderTextureWidth, renderTextureHeight);
        public long EstimatedRenderTextureBytes
        {
            get
            {
                if (renderTexture == null) return 0L;
                int samples = Math.Max(1, renderTexture.antiAliasing);
                int depthBytes = renderTexture.depth > 0 ? 4 : 0;
                return (long)renderTextureWidth * renderTextureHeight * (4 + depthBytes) * samples;
            }
        }
        public string Owner => owner;
        public string LastStatus { get; private set; } = "Preview context not created.";
        public string LastObjectFlowStatus { get; private set; } = "Preview object flow not requested.";
        public string IsolationReport => IsReady
            ? sceneMode + ", Layer=" + previewLayer + ", Origin=" + FormatVector(groupOrigin) + ", Cell=" + allocatedCell + ", FarClip=" + (Camera != null ? Camera.farClipPlane : CameraFarClip).ToString("F0") + "m, " + allocationReport
            : "Preview render context not ready.";

        public ESEditorPreviewRenderContext(
            string owner,
            ESEditorPreviewSceneMode sceneMode = ESEditorPreviewSceneMode.HiddenObjectsInActiveScene,
            int previewLayer = ESEditorPreviewUtility.DefaultPreviewLayer,
            ESEditorPreviewEnhancerSet enhancerSet = ESEditorPreviewEnhancerSet.Full)
        {
            this.owner = string.IsNullOrWhiteSpace(owner) ? "EditorPreview" : owner;
            this.sceneMode = sceneMode;
            this.previewLayer = Mathf.Clamp(previewLayer, 0, 31);
            this.enhancerSet = enhancerSet;
            allocationId = System.Threading.Interlocked.Increment(ref nextAllocationId);
            allocatedCell = AllocateCell(allocationId, out allocationReport);
            groupOrigin = new Vector3(allocatedCell.x * GroupSpacing, 0f, allocatedCell.y * GroupSpacing);
            ESEditorPreviewLifecycleHub.RegisterScope(this);
        }

        public void Ensure()
        {
            ThrowIfDisposed();
            if (sceneMode == ESEditorPreviewSceneMode.PreviewScene && Camera != null && !previewScene.IsValid())
                ResetSceneBoundPreviewObjects();
            else if (sceneMode == ESEditorPreviewSceneMode.PreviewScene
                && !previewScene.IsValid()
                && (cameraObject != null || keyLightObject != null || fillLightObject != null
                    || groundPlaneObject != null || scaleReferenceObject != null
                    || fallbackParticleMaterial != null || modelHandles.Count > 0))
                ResetSceneBoundPreviewObjects();
            if (IsReady)
                return;

            EnsurePreviewScene();
            EnsureCamera();
            EnsureLights();
            if (HasEnhancer(ESEditorPreviewEnhancerSet.GroundPlane))
                EnsureGroundPlane();
            LastStatus = "Preview render context ready.";
        }

        /// <summary>
        /// 在首次 Ensure 前选择增强器预算；一旦资源已创建便拒绝热切换，避免半套资源状态。
        /// </summary>
        public bool TryConfigureEnhancers(ESEditorPreviewEnhancerSet set)
        {
            ThrowIfDisposed();
            if (IsReady)
                return false;
            enhancerSet = set;
            return true;
        }

        public bool PreparePreviewObject(GameObject obj, string note, bool samplingTarget)
        {
            if (obj == null)
                return false;
            if (EditorUtility.IsPersistent(obj) || !ESEditorPreviewUtility.HasPreviewOwnershipFlags(obj))
            {
                LastStatus = "Preview object rejected: object is not an owned temporary preview object.";
                return false;
            }

            Ensure();
            bool moved = MoveToContextScene(obj);
            HideFlags flags = samplingTarget ? ESEditorPreviewUtility.SamplingSafeHideFlags : ESEditorPreviewUtility.PreviewHideFlags;
            ESEditorPreviewUtility.SetHideFlagsRecursive(obj.transform, flags);
            ESEditorPreviewUtility.SetLayerRecursive(obj.transform, previewLayer);
            bool markerRegistered = ESEditorPreviewUtility.TryMarkPreviewObject(obj, owner, note, out string markerStatus);
            LastObjectFlowStatus =
                "Object=" + obj.name
                + ", HideFlags=" + flags
                + ", SamplingTarget=" + samplingTarget
                + ", Scene=" + FormatScene(obj.scene)
                + ", Move=" + moved
                + ", Layer=" + previewLayer
                + ", Marker=" + markerStatus;
            return moved && markerRegistered;
        }

        /// <summary>PreviewLocal 是仅平移的米制空间：原点对应 GroupOrigin，轴向与 Unity 世界轴一致。</summary>
        public Vector3 PreviewLocalToWorldPoint(Vector3 previewLocalPoint)
        {
            return groupOrigin + previewLocalPoint;
        }

        public Vector3 WorldToPreviewLocalPoint(Vector3 worldPoint)
        {
            return worldPoint - groupOrigin;
        }

        /// <summary>
        /// 将预览辅助底板对齐到当前内容的局部 XZ 边界。底板属于 PreviewScene 临时对象，
        /// 不会写入作者态或正式场景；使用 Cube 几何保证上下表面均存在。
        /// </summary>
        public void ConfigureGroundPlane(Vector3 localCenter, float size, float localY = 0f)
        {
            Ensure();
            if (groundPlaneObject == null)
                return;

            float extent = Mathf.Clamp(size, 1f, 100000f);
            if (!IsFinite(localCenter))
                localCenter = Vector3.zero;
            groundPlaneObject.transform.position = PreviewLocalToWorldPoint(
                new Vector3(localCenter.x, localY - GroundThickness * 0.5f, localCenter.z));
            groundPlaneObject.transform.localScale = new Vector3(extent, GroundThickness, extent);
        }

        public ESEditorPreviewCameraPose CreateCameraPose(
            Vector3 previewLocalCenter,
            float radius,
            float yaw,
            float pitch,
            float zoom)
        {
            return new ESEditorPreviewCameraPose(
                PreviewLocalToWorldPoint(previewLocalCenter),
                radius,
                yaw,
                pitch,
                zoom);
        }

        public void SetScaleReferenceVisible(bool visible, float sizeMeters = 1f)
        {
            if (!HasEnhancer(ESEditorPreviewEnhancerSet.ScaleReference))
                return;
            if (!visible)
            {
                if (scaleReferenceObject != null)
                    scaleReferenceObject.SetActive(false);
                return;
            }

            Ensure();
            sizeMeters = Mathf.Clamp(sizeMeters, 0.01f, 100f);
            EnsureScaleReference();
            if (scaleReferenceObject == null)
                return;

            // 预览原点代表地面接触点。1m 参照物严格占据 [0, 1]m，避免半个立方体
            // 埋入地面后产生“看起来不是 1m”的误判。
            scaleReferenceObject.transform.position = PreviewLocalToWorldPoint(
                scaleReferenceLocalGroundPosition + Vector3.up * (sizeMeters * 0.5f));
            scaleReferenceObject.transform.rotation = Quaternion.identity;
            scaleReferenceObject.transform.localScale = Vector3.one * sizeMeters;
            scaleReferenceObject.SetActive(true);
        }

        /// <summary>把 1m 参照物放到内容左侧，既保持真实米制比例，也避免错误 Pivot 拉坏推荐构图。</summary>
        public void PositionScaleReferenceBesideWorldBounds(Bounds worldBounds, float sizeMeters = 1f, float gapMeters = 0.25f)
        {
            if (!IsFinite(worldBounds.center) || !IsFinite(worldBounds.extents))
                return;

            sizeMeters = Mathf.Clamp(sizeMeters, 0.01f, 100f);
            gapMeters = Mathf.Clamp(gapMeters, 0f, 100f);
            Vector3 localCenter = WorldToPreviewLocalPoint(worldBounds.center);
            scaleReferenceLocalGroundPosition = new Vector3(
                localCenter.x - worldBounds.extents.x - gapMeters - sizeMeters * 0.5f,
                0f,
                localCenter.z);
            if (IsScaleReferenceVisible)
                SetScaleReferenceVisible(true, sizeMeters);
        }

        public bool TryGetScaleReferenceBounds(out Bounds bounds)
        {
            bounds = default;
            if (!IsScaleReferenceVisible)
                return false;
            Renderer renderer = scaleReferenceObject.GetComponent<Renderer>();
            if (renderer == null)
                return false;
            bounds = renderer.bounds;
            return true;
        }

        public ESEditorPreviewModelHandle CreateModelGroup(
            GameObject source,
            string instanceName = null,
            bool samplingTarget = true,
            bool copyRendererState = true,
            bool disableRuntimeBehaviours = true,
            bool ensureRenderersEnabled = true,
            bool activateInstance = true)
        {
            if (source == null)
                return null;

            Ensure();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(source);
                NormalizeTransform(instance.transform);
                return AdoptModelGroup(
                    source,
                    instance,
                    instanceName,
                    samplingTarget,
                    copyRendererState,
                    disableRuntimeBehaviours,
                    ensureRenderersEnabled,
                    activateInstance);
            }
            catch
            {
                ESEditorPreviewUtility.DestroyObject(instance);
                throw;
            }
        }

        /// <summary>
        /// 接管业务已在非激活状态安全构造的预览实例。需要阻止第三方 Awake/OnEnable 的业务
        /// 应先复制允许的组件，再通过此入口完成隔离、登记和激活。
        /// </summary>
        public ESEditorPreviewModelHandle AdoptModelGroup(
            GameObject source,
            GameObject instance,
            string instanceName = null,
            bool samplingTarget = true,
            bool copyRendererState = true,
            bool disableRuntimeBehaviours = true,
            bool ensureRenderersEnabled = true,
            bool activateInstance = true,
            bool moveToGroupOrigin = true)
        {
            if (source == null || instance == null)
                return null;

            Ensure();
            ESEditorPreviewModelHandle handle = null;
            try
            {
                instance.SetActive(false);
                instance.name = string.IsNullOrWhiteSpace(instanceName) ? source.name + "_ESPreview" : instanceName;
                ESEditorPreviewUtility.SetHideFlagsRecursive(
                    instance.transform,
                    samplingTarget
                        ? ESEditorPreviewUtility.SamplingSafeHideFlags
                        : ESEditorPreviewUtility.PreviewHideFlags);
                if (!PreparePreviewObject(instance, "Preview model group.", samplingTarget))
                    throw new InvalidOperationException("Preview model could not be moved into the Context scene.");
                if (moveToGroupOrigin)
                    MoveToGroupOrigin(instance.transform);

                if (copyRendererState)
                    ESEditorPreviewUtility.CopyRendererState(source, instance);
                if (disableRuntimeBehaviours)
                    ESEditorPreviewUtility.DisableRuntimeBehaviours(instance);
                ESEditorPreviewUtility.EnsureParticleRendererMaterials(instance, EnsureFallbackParticleMaterial());

                ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    ParticleSystem.MainModule main = particleSystems[i].main;
                    main.playOnAwake = false;
                }
                if (ensureRenderersEnabled)
                    ESEditorPreviewUtility.EnsureRenderersEnabled(instance);
                if (activateInstance)
                    instance.SetActive(true);
                handle = new ESEditorPreviewModelHandle(this, source, instance);
                modelHandles.Add(handle);
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                return handle;
            }
            catch
            {
                if (handle != null)
                    modelHandles.Remove(handle);
                ESEditorPreviewUtility.DestroyObject(instance);
                throw;
            }
        }

        public void DestroyAllModelGroups()
        {
            for (int i = modelHandles.Count - 1; i >= 0; i--)
            {
                try
                {
                    modelHandles[i]?.DisposeFromOwner();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            modelHandles.Clear();
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        public bool RenderGUI(Rect rect, ESEditorPreviewCameraPose pose, ESEditorPreviewRenderOptions options)
        {
            Ensure();
            if (Camera == null)
                return false;

            ApplyCameraPose(rect.width / Mathf.Max(1f, rect.height), pose, options.Quality);
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return true;

            float scale = Mathf.Clamp(EditorGUIUtility.pixelsPerPoint * options.RenderScale, 0.5f, 4f);
            int width = QuantizeRenderDimension(rect.width * scale);
            int height = QuantizeRenderDimension(rect.height * scale);
            EnsureRenderTexture(width, height, options.Quality);
            if (renderTexture == null)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (options.MinRenderInterval > 0d && lastRenderTime > 0d && now - lastRenderTime < options.MinRenderInterval)
            {
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
                return true;
            }

            RenderTexture oldTarget = Camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                Camera.targetTexture = renderTexture;
                Camera.Render();
                lastRenderTime = now;
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
            }
            finally
            {
                Camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }

            return true;
        }

        /// <summary>
        /// 渲染当前相机姿态，不写入 Transform。Cinemachine 等拥有相机姿态权威的编辑器
        /// 预览必须使用此入口，不能先让 RenderGUI 的自由轨道相机覆盖它。
        /// </summary>
        public bool RenderCurrentCameraGUI(Rect rect, ESEditorPreviewRenderOptions options)
        {
            Ensure();
            if (Camera == null || rect.width < 1f || rect.height < 1f)
                return false;

            if (Event.current == null || Event.current.type != EventType.Repaint)
                return true;

            float scale = Mathf.Clamp(EditorGUIUtility.pixelsPerPoint * options.RenderScale, 0.5f, 4f);
            int width = QuantizeRenderDimension(rect.width * scale);
            int height = QuantizeRenderDimension(rect.height * scale);
            EnsureRenderTexture(width, height, options.Quality);
            if (renderTexture == null)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (options.MinRenderInterval > 0d && lastRenderTime > 0d && now - lastRenderTime < options.MinRenderInterval)
            {
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
                return true;
            }

            RenderTexture oldTarget = Camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                Camera.targetTexture = renderTexture;
                Camera.Render();
                lastRenderTime = now;
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
            }
            finally
            {
                Camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }

            return true;
        }

        public Texture2D Snapshot(int width, int height, ESEditorPreviewCameraPose pose, ESEditorPreviewQuality quality, string textureName, bool linear = false)
        {
            Ensure();
            if (Camera == null)
                return null;

            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 2048);
            ApplyCameraPose(width / (float)Mathf.Max(1, height), pose, quality);
            EnsureRenderTexture(width, height, quality);
            return ESEditorPreviewUtility.RenderCameraSnapshot(Camera, renderTexture, width, height, textureName, linear);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try { DestroyAllModelGroups(); }
            catch (Exception exception) { Debug.LogException(exception); }
            SafeReleaseRenderTexture();
            SafeDestroyPreviewObject(ref cameraObject);
            SafeDestroyPreviewObject(ref keyLightObject);
            SafeDestroyPreviewObject(ref fillLightObject);
            SafeDestroyPreviewObject(ref groundPlaneObject);
            SafeDestroyPreviewObject(ref groundPlaneMaterial);
            SafeDestroyPreviewObject(ref scaleReferenceObject);
            SafeDestroyPreviewObject(ref scaleReferenceMaterial);
            SafeDestroyPreviewObject(ref fallbackParticleMaterial);
            Camera = null;

            bool previewSceneCloseFailed = false;
            try
            {
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
            catch (Exception exception)
            {
                previewSceneCloseFailed = true;
                LastStatus = "Preview scene cleanup pending; will retry on the next lifecycle cleanup.";
                Debug.LogException(exception);
            }

            if (previewSceneCloseFailed)
            {
                // Do not mark the context disposed or lose the retry path. CleanupAll
                // clears its active set before invoking Dispose, so explicitly
                // re-register this context for the next reload/quit/manual cleanup.
                ESEditorPreviewLifecycleHub.RegisterScope(this);
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
                return;
            }

            previewScene = default;
            if (!cellReleased)
            {
                ReleaseCell(allocatedCell);
                cellReleased = true;
            }
            disposed = true;
            ESEditorPreviewLifecycleHub.UnregisterScope(this);
            LastStatus = "Preview context disposed.";
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        private void SafeReleaseRenderTexture()
        {
            RenderTexture owned = renderTexture;
            renderTexture = null;
            if (owned == null)
                return;

            try { owned.Release(); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { UnityEngine.Object.DestroyImmediate(owned); }
            catch (Exception exception) { Debug.LogException(exception); }
            renderTextureWidth = 0;
            renderTextureHeight = 0;
        }

        private static void SafeDestroyPreviewObject<T>(ref T value) where T : UnityEngine.Object
        {
            T owned = value;
            value = null;
            if (owned == null)
                return;
            try { ESEditorPreviewUtility.DestroyObject(owned); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void ResetSceneBoundPreviewObjects()
        {
            DestroyAllModelGroups();

            if (cameraObject != null)
            {
                try { ESEditorPreviewUtility.DestroyObject(cameraObject); }
                catch (Exception exception) { Debug.LogException(exception); }
                cameraObject = null;
            }
            if (keyLightObject != null)
            {
                try { ESEditorPreviewUtility.DestroyObject(keyLightObject); }
                catch (Exception exception) { Debug.LogException(exception); }
                keyLightObject = null;
            }
            if (fillLightObject != null)
            {
                try { ESEditorPreviewUtility.DestroyObject(fillLightObject); }
                catch (Exception exception) { Debug.LogException(exception); }
                fillLightObject = null;
            }

            SafeDestroyPreviewObject(ref groundPlaneObject);
            SafeDestroyPreviewObject(ref groundPlaneMaterial);
            SafeDestroyPreviewObject(ref scaleReferenceObject);
            SafeDestroyPreviewObject(ref scaleReferenceMaterial);
            SafeDestroyPreviewObject(ref fallbackParticleMaterial);
            Camera = null;
            CameraSceneBound = false;
            previewScene = default;
            LastObjectFlowStatus = "PreviewScene 已失效，已清理旧场景绑定资源，等待重建。";
            LastStatus = "PreviewScene invalidated; scene-bound preview objects reset.";
        }

        private void EnsurePreviewScene()
        {
            if (sceneMode != ESEditorPreviewSceneMode.PreviewScene || previewScene.IsValid())
                return;

            previewScene = EditorSceneManager.NewPreviewScene();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESEditorPreviewRenderContext));
        }

        internal void UnregisterModel(ESEditorPreviewModelHandle handle)
        {
            if (handle != null)
            {
                modelHandles.Remove(handle);
                ESEditorPreviewLifecycleHub.NotifyResourceChanged();
            }
        }

        private void EnsureCamera()
        {
            if (Camera != null)
                return;

            GameObject created = null;
            try
            {
                created = ESEditorPreviewUtility.CreatePreviewGameObject(owner + " Preview Camera", typeof(Camera));
                MoveToContextScene(created);
                if (!ESEditorPreviewUtility.TryMarkPreviewObject(
                        created, owner, "Preview camera.", out string cameraMarkerStatus))
                    throw new InvalidOperationException("Preview camera ownership registration failed: " + cameraMarkerStatus);
                Camera camera = created.GetComponent<Camera>();
                if (camera == null)
                    throw new InvalidOperationException("Preview camera component was not created.");
                camera.enabled = false;
                camera.fieldOfView = 30f;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.16f, 0.18f, 0.21f, 1f);
                camera.cullingMask = 1 << previewLayer;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = CameraFarClip;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.useOcclusionCulling = false;
                camera.depthTextureMode = DepthTextureMode.None;
                TrySetCameraScene(camera, previewScene);
                ESEditorPreviewUtility.TryConfigureUniversalCameraData(camera);
                cameraObject = created;
                Camera = camera;
                CameraSceneBound = true;
            }
            catch
            {
                if (created != null)
                    ESEditorPreviewUtility.DestroyObject(created);
                throw;
            }
        }

        private void EnsureLights()
        {
            if (keyLightObject == null)
                keyLightObject = CreateLight(owner + " Preview Key Light", 1.2f, Quaternion.Euler(35f, 35f, 0f));
            if (fillLightObject == null)
                fillLightObject = CreateLight(owner + " Preview Fill Light", 0.55f, Quaternion.Euler(340f, 210f, 0f));
        }

        private GameObject CreateLight(string name, float intensity, Quaternion rotation)
        {
            GameObject created = null;
            try
            {
                created = ESEditorPreviewUtility.CreatePreviewGameObject(name, typeof(Light));
                MoveToContextScene(created);
                ESEditorPreviewUtility.SetLayerRecursive(created.transform, previewLayer);
                if (!ESEditorPreviewUtility.TryMarkPreviewObject(
                        created, owner, "Preview light.", out string lightMarkerStatus))
                    throw new InvalidOperationException("Preview light ownership registration failed: " + lightMarkerStatus);
                Light light = created.GetComponent<Light>();
                if (light == null)
                    throw new InvalidOperationException("Preview light component was not created.");
                light.type = sceneMode == ESEditorPreviewSceneMode.PreviewScene ? LightType.Directional : LightType.Spot;
                light.intensity = sceneMode == ESEditorPreviewSceneMode.PreviewScene ? intensity : intensity * 5f;
                light.range = 60f;
                light.spotAngle = 75f;
                light.cullingMask = 1 << previewLayer;
                light.transform.rotation = rotation;
                if (sceneMode != ESEditorPreviewSceneMode.PreviewScene)
                    light.transform.position = groupOrigin - light.transform.forward * 18f + Vector3.up * 8f;
                return created;
            }
            catch
            {
                if (created != null)
                    ESEditorPreviewUtility.DestroyObject(created);
                throw;
            }
        }

        private void EnsureGroundPlane()
        {
            try { EnsureGroundPlaneCore(); }
            catch
            {
                SafeDestroyPreviewObject(ref groundPlaneMaterial);
                SafeDestroyPreviewObject(ref groundPlaneObject);
                throw;
            }
        }

        /// <summary>
        /// 查询当前会话是否启用指定增强器。未启用的增强器只保留能力位，不会隐式分配资源；
        /// 业务扩展器可据此决定是否注册粒子模拟、空间音频或后处理管线。
        /// </summary>
        public bool HasEnhancer(ESEditorPreviewEnhancerSet enhancer)
        {
            if (enhancer == ESEditorPreviewEnhancerSet.LowEnd)
                return enhancerSet == ESEditorPreviewEnhancerSet.LowEnd;
            return (enhancerSet & enhancer) == enhancer;
        }

        private void EnsureGroundPlaneCore()
        {
            if (groundPlaneObject != null)
                return;

            // 使用有真实上下表面的薄板，而不是依赖某个 Shader 是否暴露 Cull 属性。
            // 这样 URP、内置管线和兜底 Shader 下，从地面上下观察都能看到标尺板。
            groundPlaneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundPlaneObject.name = owner + " Preview Ground";
            groundPlaneObject.transform.position = groupOrigin + Vector3.down * (GroundThickness * 0.5f);
            groundPlaneObject.transform.rotation = Quaternion.identity;
            groundPlaneObject.transform.localScale = new Vector3(GroundSize, GroundThickness, GroundSize);
            ESEditorPreviewUtility.SetHideFlagsRecursive(
                groundPlaneObject.transform,
                ESEditorPreviewUtility.PreviewHideFlags);
            ESEditorPreviewUtility.SetLayerRecursive(groundPlaneObject.transform, previewLayer);
            MoveToContextScene(groundPlaneObject);

            Collider collider = groundPlaneObject.GetComponent<Collider>();
            if (collider != null)
                ESEditorPreviewUtility.DestroyObject(collider);

            Renderer renderer = groundPlaneObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null)
            {
                groundPlaneMaterial = new Material(shader)
                {
                    name = owner + " Preview Ground Material",
                    hideFlags = ESEditorPreviewUtility.PreviewHideFlags,
                    color = new Color(0.28f, 0.34f, 0.40f, 0.24f),
                    renderQueue = 3000
                };
                // 底板是预览辅助几何，必须从上下两侧都可见；统一走公共材质配置。
                ESEditorPreviewUtility.ConfigureDoubleSidedTransparent(groundPlaneMaterial, groundPlaneMaterial.color);
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterial = groundPlaneMaterial;
            }

            if (!ESEditorPreviewUtility.TryMarkPreviewObject(
                    groundPlaneObject, owner, "Preview ground plane.", out string groundMarkerStatus))
                throw new InvalidOperationException("Preview ground ownership registration failed: " + groundMarkerStatus);
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        private void EnsureScaleReference()
        {
            try { EnsureScaleReferenceCore(); }
            catch
            {
                SafeDestroyPreviewObject(ref scaleReferenceMaterial);
                SafeDestroyPreviewObject(ref scaleReferenceObject);
                throw;
            }
        }

        private void EnsureScaleReferenceCore()
        {
            if (scaleReferenceObject != null)
                return;

            scaleReferenceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scaleReferenceObject.name = owner + " 1m Scale Reference";
            scaleReferenceObject.SetActive(false);
            ESEditorPreviewUtility.SetHideFlagsRecursive(
                scaleReferenceObject.transform,
                ESEditorPreviewUtility.PreviewHideFlags);
            Collider collider = scaleReferenceObject.GetComponent<Collider>();
            if (collider != null)
                ESEditorPreviewUtility.DestroyObject(collider);
            if (!PreparePreviewObject(scaleReferenceObject,
                    "Optional one-meter scale reference.", samplingTarget: false))
                throw new InvalidOperationException("Scale reference could not be adopted by the preview context.");

            Renderer renderer = scaleReferenceObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Hidden/Internal-Colored")
                ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null)
            {
                scaleReferenceMaterial = new Material(shader)
                {
                    name = owner + " Scale Reference Material",
                    hideFlags = ESEditorPreviewUtility.PreviewHideFlags,
                    color = new Color(0.55f, 0.62f, 0.68f, 0.22f),
                    renderQueue = 3000
                };
                ESEditorPreviewUtility.ConfigureDoubleSidedTransparent(scaleReferenceMaterial, scaleReferenceMaterial.color);
                renderer.sharedMaterial = scaleReferenceMaterial;
            }

            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        private Material EnsureFallbackParticleMaterial()
        {
            if (fallbackParticleMaterial != null)
                return fallbackParticleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            Material created = null;
            try
            {
                created = new Material(shader)
                {
                    name = owner + " Particle Preview Fallback Material",
                    hideFlags = ESEditorPreviewUtility.PreviewHideFlags,
                    color = Color.white
                };
                fallbackParticleMaterial = created;
                return created;
            }
            catch
            {
                if (created != null)
                    ESEditorPreviewUtility.DestroyObject(created);
                throw;
            }
        }

        private void ApplyCameraPose(float aspect, ESEditorPreviewCameraPose pose, ESEditorPreviewQuality quality)
        {
            Camera.aspect = Mathf.Max(0.25f, aspect);
            Quaternion orbit = Quaternion.Euler(pose.Pitch, pose.Yaw, 0f);
            float verticalHalfFov = Mathf.Max(1f, Camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * Camera.aspect);
            float limitingHalfFov = Mathf.Max(1f * Mathf.Deg2Rad, Mathf.Min(verticalHalfFov, horizontalHalfFov));
            float fittedDistance = pose.Radius / Mathf.Max(0.02f, Mathf.Sin(limitingHalfFov));
            float distance = Mathf.Max(0.03f, fittedDistance * pose.Zoom);
            Camera.transform.position = pose.Center + orbit * Vector3.back * distance;
            Camera.transform.LookAt(pose.Center);
            Camera.nearClipPlane = Mathf.Clamp(distance * 0.0025f, 0.001f, 0.05f);
            Camera.farClipPlane = sceneMode == ESEditorPreviewSceneMode.PreviewScene
                ? Mathf.Max(CameraFarClip, distance + pose.Radius * 2.5f)
                : CameraFarClip;
            Camera.cullingMask = 1 << previewLayer;
            // 公共预览 RT 当前固定为 ARGB32；开启 HDR 只会增加管线成本，
            // 却无法保留 HDR 精度，因而显式保持 LDR，避免高质量档出现隐性性能损耗。
            Camera.allowHDR = false;
            Camera.allowMSAA = quality != ESEditorPreviewQuality.Fast;
            TrySetCameraScene(Camera, previewScene);
        }

        private void EnsureRenderTexture(int width, int height, ESEditorPreviewQuality quality)
        {
            ApplyGlobalRenderTextureBudget(ref width, ref height, quality);
            if (renderTexture != null && renderTextureWidth == width && renderTextureHeight == height && renderTextureQuality == quality)
                return;

            RenderTexture previous = renderTexture;
            RenderTexture replacement = ESEditorPreviewUtility.CreateRenderTexture(
                width,
                height,
                24,
                GetAntiAliasing(width, height, quality),
                owner + " Preview RT");
            if (replacement == null)
                throw new InvalidOperationException("Preview RenderTexture creation returned null; existing target was retained.");
            renderTexture = replacement;
            renderTextureWidth = width;
            renderTextureHeight = height;
            renderTextureQuality = quality;
            if (previous != null)
            {
                try { ESEditorPreviewUtility.ReleaseRenderTexture(ref previous); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            ESEditorPreviewLifecycleHub.NotifyResourceChanged();
        }

        private void ApplyGlobalRenderTextureBudget(ref int width, ref int height, ESEditorPreviewQuality quality)
        {
            width = Mathf.Clamp(width, 64, MaxRenderTextureDimension);
            height = Mathf.Clamp(height, 64, MaxRenderTextureDimension);

            ESEditorPreviewDiagnosticsSnapshot diagnostics = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            long otherBytes = Math.Max(0L, diagnostics.EstimatedRenderTextureBytes - EstimatedRenderTextureBytes);
            long availableBytes = Math.Max(8L * 1024L * 1024L, GlobalPreviewRenderTextureBudgetBytes - otherBytes);
            int samples = GetAntiAliasing(width, height, quality);
            while ((long)width * height * (4L + 4L) * samples > availableBytes
                && (width > 64 || height > 64))
            {
                width = Math.Max(64, width / 2);
                height = Math.Max(64, height / 2);
                samples = GetAntiAliasing(width, height, quality);
            }

            if ((long)width * height * (4L + 4L) * samples > availableBytes)
                LastStatus = "Preview render context ready with global RT budget floor.";
        }

        private static int QuantizeRenderDimension(float pixels)
        {
            int raw = Mathf.Max(1, Mathf.CeilToInt(pixels));
            // 轻微布局抖动不应触发 RenderTexture 反复释放/重建；8px 对 UI 预览不可见，
            // 但能显著减少窗口拖拽和 DPI 变化时的 native allocation churn。
            int quantized = Mathf.Max(8, ((raw + 7) / 8) * 8);
            return Mathf.Min(MaxRenderTextureDimension, quantized);
        }

        private bool MoveToContextScene(GameObject obj)
        {
            if (obj == null)
                return false;

            try
            {
                if (sceneMode == ESEditorPreviewSceneMode.PreviewScene)
                {
                    EnsurePreviewScene();
                    SceneManager.MoveGameObjectToScene(obj, previewScene);
                    return obj.scene == previewScene;
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && obj.scene != activeScene)
                    SceneManager.MoveGameObjectToScene(obj, activeScene);
                return obj.scene.IsValid();
            }
            catch
            {
                // PreviewScene 模式下，仍在活动场景的对象不能被视为迁移成功；
                // 上层必须看到 false 并停止继续渲染，避免正式场景污染/假成功。
                return sceneMode != ESEditorPreviewSceneMode.PreviewScene
                    && obj.scene.IsValid();
            }
        }

        private void MoveToGroupOrigin(Transform root)
        {
            if (root == null)
                return;

            root.position = groupOrigin;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private static void NormalizeTransform(Transform transform)
        {
            if (transform == null)
                return;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static int GetAntiAliasing(ESEditorPreviewQuality quality)
        {
            switch (quality)
            {
                case ESEditorPreviewQuality.High:
                    return 8;
                case ESEditorPreviewQuality.Balanced:
                    return 4;
                default:
                    return 1;
            }
        }

        private static int GetAntiAliasing(int width, int height, ESEditorPreviewQuality quality)
        {
            int samples = GetAntiAliasing(quality);
            while (samples > 1
                   && (long)width * height * (4L + 4L) * samples > PreviewRenderTextureBudgetBytes)
            {
                samples >>= 1;
            }
            return Mathf.Max(1, samples);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static Vector2Int AllocateCell(int allocationId, out string report)
        {
            unchecked
            {
                int seed = allocationId * 73856093 ^ Environment.TickCount * 19349663 ^ Guid.NewGuid().GetHashCode();
                var random = new System.Random(seed);
                lock (AllocationLock)
                {
                    while (ReleasedCells.Count > 0)
                    {
                        Vector2Int reusable = ReleasedCells.Dequeue();
                        if (OccupiedCells.Contains(reusable))
                            continue;

                        OccupiedCells.Add(reusable);
                        report = "CellAlloc=reused, Free=" + ReleasedCells.Count + ", Occupied=" + OccupiedCells.Count;
                        return reusable;
                    }

                    for (int attempt = 0; attempt < MaxCellProbeAttempts; attempt++)
                    {
                        int hash = seed ^ (attempt * 83492791);
                        int ring = 1 + Mathf.Abs(hash % 128);
                        int x = ((hash >> 8) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        int y = ((hash >> 20) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        var candidate = new Vector2Int(x, y);
                        if (OccupiedCells.Contains(candidate))
                            continue;

                        OccupiedCells.Add(candidate);
                        report = "CellAlloc=hash-random, Attempt=" + attempt + ", Occupied=" + OccupiedCells.Count;
                        return candidate;
                    }

                    // 哈希探测耗尽时仍必须登记一个唯一单元，不能回退到未登记的
                    // (0,0)，否则极端多窗口/碰撞场景会让 Context 互相重叠。
                    for (int offset = 1; ; offset++)
                    {
                        var fallback = new Vector2Int(offset, 0);
                        if (OccupiedCells.Contains(fallback))
                            continue;

                        OccupiedCells.Add(fallback);
                        report = "CellAlloc=deterministic-fallback, Occupied=" + OccupiedCells.Count;
                        return fallback;
                    }
                }
            }
        }

        private static void ReleaseCell(Vector2Int cell)
        {
            lock (AllocationLock)
            {
                if (OccupiedCells.Remove(cell))
                    ReleasedCells.Enqueue(cell);
            }
        }

        private static bool TrySetCameraScene(Camera camera, Scene scene)
        {
            if (camera == null || !scene.IsValid())
                return false;

            PropertyInfo sceneProperty = typeof(Camera).GetProperty("scene", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sceneProperty == null || !sceneProperty.CanWrite || sceneProperty.PropertyType != typeof(Scene))
                return false;

            sceneProperty.SetValue(camera, scene);
            return true;
        }

        private static string FormatScene(Scene scene)
        {
            if (!scene.IsValid())
                return "<invalid>";

            return string.IsNullOrEmpty(scene.name) ? "<untitled-active-scene>" : scene.name;
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F1") + ", " + value.y.ToString("F1") + ", " + value.z.ToString("F1") + ")";
        }
    }

    public static class ESEditorPreviewPersistentFramePaths
    {
        private const string RootFolderName = "ESPreviewFrames";

        public static string RootFolder
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Library", RootFolderName);
            }
        }

        public static string GetFrameFolder(string workflow, string stableKey, string viewName)
        {
            workflow = SanitizePathPart(workflow, "General");
            stableKey = SanitizePathPart(stableKey, "Unknown");
            viewName = SanitizePathPart(viewName, "Default");
            return Path.Combine(RootFolder, workflow, stableKey, viewName);
        }

        public static string GetFramePath(string workflow, string stableKey, string viewName, int frameIndex)
        {
            return Path.Combine(GetFrameFolder(workflow, stableKey, viewName), "preview_" + Mathf.Max(1, frameIndex).ToString("000") + ".png");
        }

        public static void EnsureFrameFolder(string workflow, string stableKey, string viewName)
        {
            Directory.CreateDirectory(GetFrameFolder(workflow, stableKey, viewName));
        }

        private static string SanitizePathPart(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = fallback;

            value = value.Trim().Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(invalid[i], '_');

            value = value.Trim();
            if (value.Length == 0 || string.Equals(value, ".", StringComparison.Ordinal)
                || string.Equals(value, "..", StringComparison.Ordinal))
                return fallback;
            return value;
        }
    }
}
#endif
