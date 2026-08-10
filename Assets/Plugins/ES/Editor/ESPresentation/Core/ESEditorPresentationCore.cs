using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.EditorInternal
{
    /// <summary>
    /// Shared visual primitives for ES editor drawing.
    ///
    /// The class deliberately contains no serialized or runtime state. All objects are lazily
    /// created once per editor skin and are reused by Section, polymorphic and future ES drawers.
    /// This keeps the IMGUI repaint path free from style/texture allocations.
    /// </summary>
    internal static class ESEditorPresentation
    {
        private static bool skinInitialized;
        private static bool cachedProSkin;
        private static int skinGeneration;
        private static int globalEditorSkinGeneration;
        private static GUIStyle surfaceStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle metaStyle;
        private static GUIStyle compactCollectionTitleStyle;
        private static GUIStyle compactCollectionMetaStyle;
        private static GUIStyle compactCollectionBodyStyle;
        private static Texture2D surfaceTexture;
        private static Texture2D compactCollectionBodyTexture;
        private static ESGlobalEditorTheme theme;
        private static bool themeInitialized;
        private static int themeGeneration;
        private static readonly Dictionary<int, WindowBinding> windowBindings =
            new Dictionary<int, WindowBinding>(32);

        private sealed class WindowBinding
        {
            public EditorWindow window;
            public VisualElement root;
            public VisualElement host;
            public VisualElement accentLine;
            public VisualElement sweep;
            public IVisualElementScheduledItem animation;
            public double pulseStartedAt;
            public ESStatusKind pulseStatus;
            public float pulseDuration;
        }

        private const float GlobalAccentLineHeight = 2f;
        private const float GlobalSweepDuration = 0.52f;
        private static bool globalEditorAdaptersInstalled;
        private static bool globalEditorAdapterLifecycleInstalled;
        private static bool deepSkinSyncQueued;

        /// <summary>
        /// 安装 ES 对 Unity 原生 Inspector/SceneView 的轻量表现适配。只订阅官方绘制回调，
        /// 不枚举 EditorWindow、不扫描资产、不修改 Unity 控件布局或业务数据。
        /// </summary>
        public static void InstallGlobalEditorAdapters()
        {
            if (!globalEditorAdapterLifecycleInstalled)
            {
                EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnGlobalPlayModeStateChanged;
                globalEditorAdapterLifecycleInstalled = true;
            }

            if (!GlobalEditorShellEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            InstallGlobalEditorAdapterCallbacks();
            QueueDeepSkinSynchronization();
        }

        /// <summary>全局 ES 外观是否由主题启用，且当前处于编辑模式。</summary>
        public static bool GlobalEditorShellEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return (current == null || current.enableGlobalEditorShell)
                    && !EditorApplication.isPlayingOrWillChangePlaymode;
            }
        }

        /// <summary>主题/皮肤缓存世代，供已接入窗口判断是否需要重建样式。</summary>
        internal static int ThemeGeneration => themeGeneration;

        private static void InstallGlobalEditorAdapterCallbacks()
        {
            if (globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            SceneView.duringSceneGui += DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = true;
        }

        /// <summary>卸载全局适配，供测试、域重载和受控关闭路径使用。</summary>
        public static void UninstallGlobalEditorAdapters()
        {
            UninstallGlobalEditorAdapterCallbacks();
            EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
            globalEditorAdapterLifecycleInstalled = false;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;
            ESGlobalEditorSkinExperiment.Restore();
            UnbindAllWindowBindings();
        }

        private static void UninstallGlobalEditorAdapterCallbacks()
        {
            if (!globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = false;
        }

        private static void OnGlobalPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                UninstallGlobalEditorAdapterCallbacks();
                SuspendWindowBindings();
                ESGlobalEditorSkinExperiment.Restore();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                InstallGlobalEditorAdapters();
                ResumeWindowBindings();
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
                SceneView.RepaintAll();
            }
        }

        private static void QueueDeepSkinSynchronization()
        {
            if (deepSkinSyncQueued)
                return;

            deepSkinSyncQueued = true;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            EditorApplication.delayCall += SynchronizeDeepSkinWithTheme;
        }

        private static void SynchronizeDeepSkinWithTheme()
        {
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;

            ESGlobalEditorTheme current = CurrentTheme;
            bool shouldApply = GlobalEditorShellEnabled
                && current != null
                && current.enableDeepEditorSkin;
            ESGlobalEditorSkinExperiment.Synchronize(shouldApply);
        }

        private static void UnbindAllWindowBindings()
        {
            if (windowBindings.Count == 0)
                return;

            List<EditorWindow> windows = new List<EditorWindow>(windowBindings.Count);
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && binding.window != null)
                    windows.Add(binding.window);

            for (int i = 0; i < windows.Count; i++)
                UnbindWindow(windows[i]);
        }

        private static void UnregisterWindowCallbacks(WindowBinding binding)
        {
            if (binding == null || binding.root == null)
                return;

            binding.root.UnregisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
        }

        private static void SuspendWindowBindings()
        {
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null)
                    continue;
                binding.animation?.Pause();
                UnregisterWindowCallbacks(binding);
                binding.host?.RemoveFromHierarchy();
                binding.root = null;
            }
        }

        private static void ResumeWindowBindings()
        {
            if (!GlobalEditorShellEnabled)
                return;

            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || binding.window == null || binding.window.rootVisualElement == null)
                    continue;
                AttachWindowOverlay(binding);
            }
        }

        private static void DrawGlobalInspectorHeader(UnityEditor.Editor editor)
        {
            if (editor == null || (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint))
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, GetDepthAccent(0));
        }

        private static void DrawGlobalSceneViewChrome(SceneView sceneView)
        {
            if (sceneView == null || Event.current.type != EventType.Repaint)
                return;

            Color previousGuiColor = GUI.color;
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            bool previousGuiEnabled = GUI.enabled;
            Handles.BeginGUI();
            Rect rect = new Rect(0f, 0f, sceneView.position.width, 2f);
            EditorGUI.DrawRect(rect, GetDepthAccent(0));
            Handles.EndGUI();
            GUI.color = previousGuiColor;
            GUI.matrix = previousGuiMatrix;
            GUI.enabled = previousGuiEnabled;
        }

        public static GUIStyle SurfaceStyle
        {
            get
            {
                EnsureSkin();
                if (surfaceStyle == null)
                {
                    surfaceStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, Metric(2f), Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(9f), Metric(7f), Metric(8f)),
                        border = new RectOffset(1, 1, 1, 1)
                    };
                    surfaceStyle.normal.background = SurfaceTexture;
                }

                return surfaceStyle;
            }
        }

        internal static int SkinGeneration
        {
            get { return skinGeneration + globalEditorSkinGeneration * 100000; }
        }

        internal static void NotifyGlobalEditorSkinChanged()
        {
            globalEditorSkinGeneration++;
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                EnsureSkin();
                if (headerStyle == null)
                {
                    headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(0, 0, 0, Metric(2f))
                    };
                    headerStyle.normal.textColor = cachedProSkin
                        ? new Color(0.83f, 0.85f, 0.88f, 1f)
                        : new Color(0.16f, 0.18f, 0.21f, 1f);
                }

                return headerStyle;
            }
        }

        public static GUIStyle SubtitleStyle
        {
            get
            {
                EnsureSkin();
                if (subtitleStyle == null)
                {
                    subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        wordWrap = true,
                        padding = new RectOffset(0, 0, Metric(1f), Metric(3f))
                    };
                    subtitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.72f, 0.76f, 0.82f, 1f)
                        : new Color(0.39f, 0.42f, 0.45f, 1f);
                }

                return subtitleStyle;
            }
        }

        public static GUIStyle MetaStyle
        {
            get
            {
                EnsureSkin();
                if (metaStyle == null)
                {
                    metaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, Metric(1f))
                    };
                    metaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.70f, 0.74f, 0.80f, 1f)
                        : new Color(0.37f, 0.40f, 0.44f, 1f);
                }

                return metaStyle;
            }
        }

        /// <summary>
        /// Compact feedback-card primitives used by optional collection drawers. The visual
        /// language is intentionally generic and has no dependency on third-party editor code.
        /// </summary>
        public static float CompactCollectionHeaderHeight
        {
            get { return Mathf.Max(34f, Mathf.Round(36f * Density)); }
        }

        public static GUIStyle CompactCollectionTitleStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionTitleStyle == null)
                {
                    compactCollectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionTitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.88f, 0.90f, 0.93f, 1f)
                        : new Color(0.15f, 0.17f, 0.20f, 1f);
                }

                return compactCollectionTitleStyle;
            }
        }

        public static GUIStyle CompactCollectionMetaStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionMetaStyle == null)
                {
                    compactCollectionMetaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionMetaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.58f, 0.62f, 0.68f, 1f)
                        : new Color(0.38f, 0.41f, 0.45f, 1f);
                }

                return compactCollectionMetaStyle;
            }
        }

        public static GUIStyle CompactCollectionBodyStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyStyle == null)
                {
                    compactCollectionBodyStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, 0, Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(7f), Metric(6f), Metric(7f))
                    };
                    compactCollectionBodyStyle.normal.background = CompactCollectionBodyTexture;
                }

                return compactCollectionBodyStyle;
            }
        }

        public static Color DividerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.30f, 0.32f, 0.35f, 1f)
                    : new Color(0.72f, 0.74f, 0.76f, 1f);
            }
        }

        /// <summary>
        /// Low-priority category accents borrowed from the FolderSystem ES_Logic artwork.
        /// They are for module identity only and must not replace semantic status colors.
        /// </summary>
        public static Color LogicSteelBlue
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.29f, 0.51f, 0.66f, 0.96f)
                    : new Color(0.24f, 0.46f, 0.62f, 0.96f);
            }
        }

        public static Color LogicGold
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.78f, 0.69f, 0.14f, 0.96f)
                    : new Color(0.68f, 0.49f, 0.06f, 0.96f);
            }
        }

        public static bool IsProSkin
        {
            get
            {
                EnsureSkin();
                return cachedProSkin;
            }
        }

        public static bool ShowSectionSubtitle
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.showSectionSubtitle;
            }
        }

        public static float Density
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 1f : Mathf.Clamp(current.density, 0.85f, 1.20f);
            }
        }

        /// <summary>
        /// Whether optional ES feedback motion is enabled. Motion is presentation-only;
        /// disabling it never hides status text, icons or validation information.
        /// </summary>
        public static bool MotionEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.enableMotion;
            }
        }

        /// <summary>
        /// 绑定一个 ES 编辑器窗口到共享 Presentation 层。绑定只持有当前域内的活动窗口，
        /// 不会扫描全部 EditorWindow，也不会把窗口引用写入资产或 SessionState。
        /// </summary>
        public static void BindWindow(EditorWindow window)
        {
            if (!GlobalEditorShellEnabled || window == null || window.rootVisualElement == null)
                return;

            int id = window.GetInstanceID();
            WindowBinding binding;
            if (!windowBindings.TryGetValue(id, out binding) || binding == null || binding.window != window)
            {
                if (binding != null)
                    UnbindWindow(binding.window);

                binding = new WindowBinding
                {
                    window = window,
                    pulseStatus = ESStatusKind.None,
                    pulseDuration = GlobalSweepDuration
                };
                windowBindings[id] = binding;
            }

            if (binding.host == null || binding.host.parent == null)
                AttachWindowOverlay(binding);
        }

        /// <summary>解除窗口绑定并停止所有局部调度。</summary>
        public static void UnbindWindow(EditorWindow window)
        {
            if (window == null)
                return;

            int id = window.GetInstanceID();
            WindowBinding binding;
            if (!windowBindings.TryGetValue(id, out binding))
                return;

            if (binding.animation != null)
                binding.animation.Pause();
            UnregisterWindowCallbacks(binding);
            if (binding.host != null)
            {
                binding.host.RemoveFromHierarchy();
            }
            windowBindings.Remove(id);
        }

        /// <summary>
        /// 播放一次统一 ES 操作反馈。只在反馈持续期间请求当前窗口局部刷新。
        /// </summary>
        public static void PulseWindow(EditorWindow window, ESStatusKind status = ESStatusKind.Modified)
        {
            if (window == null || !GlobalEditorShellEnabled || !MotionEnabled)
                return;

            BindWindow(window);
            WindowBinding binding;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out binding) || binding == null)
                return;

            binding.pulseStatus = status;
            binding.pulseStartedAt = EditorApplication.timeSinceStartup;
            binding.pulseDuration = GlobalSweepDuration;
            binding.animation?.Resume();
            binding.window.Repaint();
        }

        private static void AttachWindowOverlay(WindowBinding binding)
        {
            VisualElement root = binding.window.rootVisualElement;
            if (root == null)
                return;
            UnregisterWindowCallbacks(binding);
            binding.root = root;

            if (binding.host != null)
                binding.host.RemoveFromHierarchy();

            binding.host = new VisualElement
            {
                name = "ESGlobalPresentationOverlay",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            binding.host.style.position = Position.Absolute;
            binding.host.style.left = 0f;
            binding.host.style.right = 0f;
            binding.host.style.top = 0f;
            binding.host.style.height = GlobalAccentLineHeight;
            binding.host.style.backgroundColor = GetDepthAccent(0);

            binding.accentLine = new VisualElement { name = "ESGlobalPresentationAccent" };
            binding.accentLine.pickingMode = PickingMode.Ignore;
            binding.accentLine.style.flexGrow = 1f;
            binding.accentLine.style.backgroundColor = GetDepthAccent(0);
            binding.host.Add(binding.accentLine);

            binding.sweep = new VisualElement { name = "ESGlobalPresentationSweep" };
            binding.sweep.pickingMode = PickingMode.Ignore;
            binding.sweep.style.position = Position.Absolute;
            binding.sweep.style.top = 0f;
            binding.sweep.style.bottom = 0f;
            binding.sweep.style.width = 0f;
            binding.sweep.style.backgroundColor = GetStatusAccent(0, ESStatusKind.Modified);
            binding.host.Add(binding.sweep);

            root.RegisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            root.Add(binding.host);
            binding.host.BringToFront();

            binding.animation = MotionEnabled
                ? binding.host.schedule.Execute(() => UpdateWindowOverlay(binding)).Every(33)
                : null;
            binding.animation?.Pause();
        }

        private static void OnWindowFocusIn(FocusInEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null)
                PulseWindow(binding.window, ESStatusKind.Modified);
        }

        private static void OnWindowPointerDown(PointerDownEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null && evt.button == 0)
                PulseWindow(binding.window, ESStatusKind.Modified);
        }

        private static WindowBinding FindBindingByRoot(VisualElement element)
        {
            if (element == null)
                return null;

            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding != null && binding.root == element)
                    return binding;
            }
            return null;
        }

        private static void UpdateWindowOverlay(WindowBinding binding)
        {
            if (!IsWindowOverlayAttached(binding))
            {
                binding?.animation?.Pause();
                return;
            }

            try
            {
                float pulse = EvaluatePulse(binding.pulseStartedAt, binding.pulseDuration);
                if (pulse <= 0f)
                {
                    binding.animation?.Pause();
                    binding.sweep.style.width = 0f;
                    binding.accentLine.style.backgroundColor = GetDepthAccent(0);
                    return;
                }

                Color accent = GetStatusAccent(0, binding.pulseStatus);
                accent.a = Mathf.Clamp01(0.62f + pulse * 0.34f);
                binding.accentLine.style.backgroundColor = accent;

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - binding.pulseStartedAt) / binding.pulseDuration));
                float hostWidth = binding.host.resolvedStyle.width;
                if (float.IsNaN(hostWidth) || float.IsInfinity(hostWidth) || hostWidth <= 0f)
                    hostWidth = 180f;
                float width = Mathf.Clamp(hostWidth * 0.16f, 24f, 180f);
                binding.sweep.style.width = width;
                binding.sweep.style.left = Mathf.Lerp(-width, hostWidth, progress);
                Color sweepColor = accent;
                sweepColor.a = Mathf.Clamp01(0.10f + pulse * 0.24f);
                binding.sweep.style.backgroundColor = sweepColor;
                binding.window.Repaint();
            }
            catch (NullReferenceException)
            {
                // Unity may invalidate an internal InlineStyleAccess between DetachFromPanel and
                // the scheduled callback. Stop this local animation; the window content remains intact.
                binding.animation?.Pause();
            }
        }

        private static bool IsWindowOverlayAttached(WindowBinding binding)
        {
            return binding != null
                && binding.window != null
                && binding.root != null
                && binding.root.panel != null
                && binding.host != null
                && binding.host.panel != null
                && ReferenceEquals(binding.host.parent, binding.root)
                && binding.accentLine != null
                && binding.sweep != null;
        }

        /// <summary>Normalized global motion strength used by all ES editor surfaces.</summary>
        public static float MotionIntensity
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 0.78f : Mathf.Clamp01(current.motionIntensity);
            }
        }

        /// <summary>
        /// Returns a one-shot, allocation-free pulse value. Callers can request a repaint only
        /// while this value is non-zero; no global per-frame repaint loop is installed here.
        /// </summary>
        public static float EvaluatePulse(double startedAt, float duration = 0.42f)
        {
            if (!MotionEnabled || startedAt <= 0d || duration <= 0.001f)
                return 0f;

            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            if (elapsed <= 0d || elapsed >= duration)
                return 0f;

            float normalized = Mathf.Clamp01((float)(elapsed / duration));
            return Mathf.Sin(normalized * Mathf.PI) * MotionIntensity;
        }

        /// <summary>
        /// Returns a subtle looping breath value for a focused/selected surface. It is intended
        /// for a single local highlight, never for animating an entire large editor tree.
        /// </summary>
        public static float EvaluateBreath(double now = -1d, float period = 1.6f)
        {
            if (!MotionEnabled || period <= 0.05f)
                return 0f;

            if (now < 0d)
                now = EditorApplication.timeSinceStartup;

            float phase = Mathf.Repeat((float)(now / period), 1f) * Mathf.PI * 2f;
            return (0.5f + 0.5f * Mathf.Sin(phase)) * MotionIntensity;
        }

        /// <summary>Blends a base color toward an ES accent without changing semantic status.</summary>
        public static Color GetMotionColor(Color baseColor, Color accent, float amount)
        {
            float strength = Mathf.Clamp01(amount) * MotionIntensity;
            return Color.Lerp(baseColor, accent, strength);
        }

        /// <summary>
        /// Draws a one-shot feedback frame for IMGUI surfaces. Returns true while the effect is
        /// active so the owning window can schedule a local repaint.
        /// </summary>
        public static bool DrawFeedbackFrame(
            Rect rect,
            ESStatusKind status,
            int depth,
            double startedAt,
            float duration = 0.42f,
            float thickness = 1f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            Color accent = GetStatusAccent(depth, status);
            accent.a = Mathf.Clamp01(0.28f + pulse * 0.52f);
            DrawFrame(rect, accent, thickness);
            return true;
        }

        /// <summary>
        /// Draws a restrained horizontal sweep used for save/preview/selection feedback. The
        /// caller owns the animation start time and should repaint only the local view.
        /// </summary>
        public static bool DrawFeedbackSweep(
            Rect rect,
            Color accent,
            double startedAt,
            float duration = 0.60f,
            float widthRatio = 0.18f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            float sweepProgress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startedAt) / duration));
            float sweepWidth = Mathf.Clamp(rect.width * widthRatio, 6f, 96f);
            float x = Mathf.Lerp(rect.x - sweepWidth, rect.xMax, sweepProgress);
            Color sweepColor = accent;
            sweepColor.a = Mathf.Clamp01(0.06f + pulse * 0.18f);
            EditorGUI.DrawRect(new Rect(x, rect.y, sweepWidth, rect.height), sweepColor);
            return true;
        }

        public static Color SectionSelectedFill
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.18f, 0.32f, 0.46f, 0.34f)
                    : new Color(0.72f, 0.84f, 0.96f, 0.55f);
            }
        }

        public static Color SectionTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.88f, 0.91f, 0.96f, 1f)
                    : new Color(0.28f, 0.30f, 0.33f, 1f);
            }
        }

        public static Color SectionSelectedTextColor
        {
            get { return GetDepthAccent(0); }
        }

        public static Color SectionMutedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.64f, 0.69f, 0.77f, 1f)
                    : new Color(0.50f, 0.52f, 0.55f, 1f);
            }
        }

        public static Color SectionMarkerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.42f, 0.45f, 0.49f, 1f)
                    : new Color(0.54f, 0.57f, 0.60f, 1f);
            }
        }

        public static Color WarningBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.33f, 0.22f, 0.16f, 0.90f)
                    : new Color(1f, 0.92f, 0.84f, 1f);
            }
        }

        public static Color NeutralSelectorBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.25f, 0.26f, 0.28f, 0.90f)
                    : new Color(0.88f, 0.89f, 0.90f, 1f);
            }
        }

        public static Color NeutralHoverColor
        {
            get { return new Color(0.48f, 0.51f, 0.55f, 1f); }
        }

        public static Color WarningTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(1f, 0.66f, 0.35f, 1f)
                    : new Color(0.72f, 0.29f, 0.05f, 1f);
            }
        }

        public static Color EmptyTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.74f, 0.78f, 0.85f, 1f)
                    : new Color(0.38f, 0.41f, 0.45f, 1f);
            }
        }

        public static Color SelectedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.62f, 0.80f, 1f, 1f)
                    : new Color(0.06f, 0.31f, 0.61f, 1f);
            }
        }

        public static Color SelectorArrowColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.59f, 0.62f, 0.66f, 1f)
                    : new Color(0.39f, 0.42f, 0.46f, 1f);
            }
        }

        public static Color ClearActionColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.65f, 0.48f, 0.48f, 1f)
                    : new Color(0.62f, 0.28f, 0.28f, 1f);
            }
        }

        public static Texture2D SurfaceTexture
        {
            get
            {
                EnsureSkin();
                if (surfaceTexture == null)
                {
                    Color borderColor = cachedProSkin
                        ? new Color(0.28f, 0.34f, 0.42f, 1f)
                        : new Color(0.58f, 0.61f, 0.64f, 1f);
                    Color fillColor = cachedProSkin
                        ? new Color(0.13f, 0.17f, 0.23f, 1f)
                        : new Color(0.91f, 0.92f, 0.93f, 1f);

                    // Keep the constructor version-neutral. Unity versions differ in the
                    // TextureFormat/creation-flags overloads available to editor assemblies.
                    surfaceTexture = new Texture2D(3, 3)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ESEditorPresentationSurface"
                    };

                    for (int y = 0; y < 3; y++)
                    {
                        for (int x = 0; x < 3; x++)
                            surfaceTexture.SetPixel(x, y, x == 1 && y == 1 ? fillColor : borderColor);
                    }

                    surfaceTexture.Apply(false, true);
                }

                return surfaceTexture;
            }
        }

        private static Texture2D CompactCollectionBodyTexture
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyTexture == null)
                {
                    compactCollectionBodyTexture = new Texture2D(1, 1)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ESEditorPresentationCompactCollectionBody"
                    };
                    compactCollectionBodyTexture.SetPixel(
                        0,
                        0,
                        cachedProSkin
                            ? new Color(0.16f, 0.17f, 0.19f, 0.96f)
                            : new Color(0.94f, 0.945f, 0.95f, 1f));
                    compactCollectionBodyTexture.Apply(false, true);
                }

                return compactCollectionBodyTexture;
            }
        }

        public static float GetDepthProgress(int depth)
        {
            if (depth <= 0)
                return 0f;

            // The first nested level must be visually obvious; later levels converge quickly
            // so deep data remains readable instead of becoming nearly black.
            return Mathf.Clamp01(0.28f + (depth - 1) * 0.38f);
        }

        public static Color GetDepthAccent(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            ESGlobalEditorTheme current = CurrentTheme;
            Color start = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentStart : current.lightAccentStart)
                : cachedProSkin
                    ? new Color(0.48f, 0.78f, 1f, 0.92f)
                    : new Color(0.12f, 0.46f, 0.82f, 0.92f);
            Color end = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentEnd : current.lightAccentEnd)
                : cachedProSkin
                    ? new Color(0.13f, 0.42f, 0.72f, 0.96f)
                    : new Color(0.04f, 0.24f, 0.56f, 0.96f);
            return Color.Lerp(start, end, progress);
        }

        public static Color GetDepthBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.12f, 0.15f, 0.19f, 0.98f),
                    new Color(0.025f, 0.055f, 0.10f, 0.99f),
                    progress)
                : Color.Lerp(
                    new Color(0.96f, 0.96f, 0.97f, 1f),
                    new Color(0.76f, 0.86f, 0.95f, 1f),
                    progress);
        }

        public static Color GetSelectorBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.08f, 0.14f, 0.22f, 0.94f),
                    new Color(0.04f, 0.08f, 0.13f, 0.96f),
                    progress)
                : Color.Lerp(
                    new Color(0.88f, 0.93f, 0.98f, 1f),
                    new Color(0.70f, 0.82f, 0.94f, 1f),
                    progress);
        }

        public static Color GetStatusFrameColor(int depth, ESStatusKind status)
        {
            EnsureSkin();
            if (status == ESStatusKind.Error)
            {
                Color error = GetStatusAccent(depth, status);
                error.a = 0.78f;
                return error;
            }

            if (status == ESStatusKind.Warning)
            {
                Color warning = GetStatusAccent(depth, status);
                warning.a = 0.82f;
                return warning;
            }

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            Color accent = GetStatusAccent(depth, status);
            accent.a = cachedProSkin ? 0.72f : 0.64f;
            return accent;
        }

        public static Color GetStatusAccent(int depth, ESStatusKind status)
        {
            EnsureSkin();
            ESGlobalEditorTheme current = CurrentTheme;
            if (status == ESStatusKind.Error)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkError : current.lightError)
                    : new Color(0.92f, 0.40f, 0.24f, 0.96f);

            if (status == ESStatusKind.Warning)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkWarning : current.lightWarning)
                    : new Color(0.90f, 0.68f, 0.24f, 0.96f);

            if (status == ESStatusKind.Modified)
                return new Color(0.36f, 0.70f, 0.98f, 0.96f);

            if (status == ESStatusKind.ReadOnly)
                return cachedProSkin
                    ? new Color(0.50f, 0.54f, 0.60f, 0.86f)
                    : new Color(0.45f, 0.49f, 0.54f, 0.86f);

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            return GetDepthAccent(depth);
        }

        public static void DrawCompactCollectionHeaderBackground(
            Rect rect,
            int depth,
            ESStatusKind status,
            bool expanded)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EnsureSkin();
            Color background = cachedProSkin
                ? new Color(0.205f, 0.215f, 0.235f, 0.98f)
                : new Color(0.885f, 0.895f, 0.91f, 1f);
            background = Color.Lerp(background, GetDepthBackground(depth), 0.22f);

            Color accent = status == ESStatusKind.Error || status == ESStatusKind.Warning
                ? GetStatusAccent(depth, status)
                : GetDepthAccent(depth);
            if (status == ESStatusKind.Error || status == ESStatusKind.Warning)
                background = Color.Lerp(background, accent, cachedProSkin ? 0.14f : 0.09f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, Metric(4f), rect.height), accent);

            Color edge = GetStatusFrameColor(depth, status);
            edge.a = cachedProSkin ? 0.74f : 0.66f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), edge);

            if (!expanded)
                return;

            Color openEdge = accent;
            openEdge.a = cachedProSkin ? 0.48f : 0.38f;
            EditorGUI.DrawRect(new Rect(rect.x + Metric(4f), rect.yMax - 2f, rect.width - Metric(4f), 1f), openEdge);
        }

        public static void DrawFrame(Rect rect, Color color, float thickness = 1f)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawDivider(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f)
                EditorGUI.DrawRect(rect, DividerColor);
        }

        public static void InvalidateSkinCache()
        {
            skinInitialized = false;
        }

        public static void InvalidateTheme()
        {
            themeGeneration++;
            themeInitialized = false;
            theme = null;
            InvalidateSkinCache();
            if (GlobalEditorShellEnabled)
                InstallGlobalEditorAdapters();
            else
            {
                UninstallGlobalEditorAdapterCallbacks();
                UnbindAllWindowBindings();
            }
            QueueDeepSkinSynchronization();
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (!IsWindowOverlayAttached(binding))
                    continue;

                try
                {
                    Color accent = GetDepthAccent(0);
                    binding.host.style.backgroundColor = accent;
                    binding.accentLine.style.backgroundColor = accent;
                    binding.window.Repaint();
                }
                catch (NullReferenceException)
                {
                    binding.animation?.Pause();
                }
            }
            SceneView.RepaintAll();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        private static ESGlobalEditorTheme CurrentTheme
        {
            get
            {
                if (!themeInitialized)
                {
                    theme = ESGlobalEditorTheme.Instance;
                    themeInitialized = true;
                }

                return theme;
            }
        }

        private static int Metric(float value)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * Density));
        }

        private static void EnsureSkin()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (skinInitialized && cachedProSkin == proSkin)
                return;

            cachedProSkin = proSkin;
            skinInitialized = true;
            skinGeneration++;
            surfaceStyle = null;
            headerStyle = null;
            subtitleStyle = null;
            metaStyle = null;
            compactCollectionTitleStyle = null;
            compactCollectionMetaStyle = null;
            compactCollectionBodyStyle = null;

            if (surfaceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(surfaceTexture);
                surfaceTexture = null;
            }

            if (compactCollectionBodyTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(compactCollectionBodyTexture);
                compactCollectionBodyTexture = null;
            }
        }
    }

    /// <summary>
    /// Reversible Unity 2022.3 deep-skin layer. The project theme explicitly opts in; application
    /// performs one bounded EditorStyles reflection pass and one enumeration of open EditorWindow
    /// instances. No asset scan, global window polling or per-frame skin work is used.
    /// </summary>
    internal static class ESGlobalEditorSkinExperiment
    {
        private enum StyleRole
        {
            None,
            Text,
            InteractiveText,
            Toolbar,
            Button,
            Input,
            Header,
            Help,
            Selection
        }

        private enum SkinTone
        {
            Surface,
            Raised,
            Hover,
            Active,
            Focused,
            Input,
            Toolbar,
            Help
        }

        private sealed class StateSnapshot
        {
            public GUIStyleState state;
            public Color textColor;
            public Texture2D background;
            public Texture2D[] scaledBackgrounds;
        }

        private sealed class StyleSnapshot
        {
            public GUIStyle style;
            public StateSnapshot[] states;
        }

        private sealed class RootSnapshot
        {
            public VisualElement root;
        }

        private const string GlobalStyleSheetPath =
            "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESGlobalEditorDeepSkin.uss";
        private const string RootClass = "es-global-editor-skin";
        private const string DarkRootClass = "es-global-editor-skin--dark";
        private const string LightRootClass = "es-global-editor-skin--light";
        private const int MaxTintTexturePixels = 262144;
        private const int MaxCreatedTextureCount = 64;
        private const long MaxCreatedTextureBytes = 16L * 1024L * 1024L;
        private const int MaxEditorStylesInitializationRetries = 8;

        private static readonly List<StyleSnapshot> snapshots = new List<StyleSnapshot>(96);
        private static readonly List<RootSnapshot> rootSnapshots = new List<RootSnapshot>(32);
        private static readonly Dictionary<long, Texture2D> themedTextureCache =
            new Dictionary<long, Texture2D>(96);
        private static readonly List<Texture2D> createdTextures = new List<Texture2D>(96);
        private static long createdTextureBytes;
        private static bool applied;
        private static bool editorStylesInitializationPending;
        private static bool initializationRetryQueued;
        private static int initializationRetryCount;
        private static StyleSheet globalStyleSheet;

        public static bool IsApplied => applied;
        public static int StyledWindowCount => rootSnapshots.Count;

        public static bool TryApply(out string message)
        {
            if (applied)
            {
                RefreshOpenWindowRoots();
                message = BuildAppliedMessage();
                return true;
            }

            if (Application.isBatchMode)
            {
                message = "BatchMode 不加载 ES 深度皮肤，未改变 Unity 原生样式。";
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "PlayMode 中不会启用 ES 深度皮肤，请返回 EditMode 后重试。";
                return false;
            }

            if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal))
            {
                message = "当前 Unity 版本不是 2022.3，深度皮肤已拒绝运行。";
                return false;
            }

            snapshots.Clear();
            rootSnapshots.Clear();
            themedTextureCache.Clear();
            DestroyCreatedTextures();

            if (!TryGetCurrentEditorStyles(out object currentStyles, out message))
            {
                if (editorStylesInitializationPending)
                    QueueInitializationRetry();
                return false;
            }

            ApplyEditorStyles(currentStyles);
            RefreshOpenWindowRoots();
            if (snapshots.Count == 0 && rootSnapshots.Count == 0)
            {
                Restore();
                message = "没有找到可安全调整的 Unity 编辑器表面，未改变原生样式。";
                return false;
            }

            applied = true;
            ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            CancelInitializationRetry();
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            EditorApplication.delayCall += RefreshOpenWindowRoots;
            InternalEditorUtility.RepaintAllViews();
            message = BuildAppliedMessage();
            return true;
        }

        public static void Restore()
        {
            bool wasApplied = applied;
            CancelInitializationRetry();
            EditorApplication.delayCall -= RefreshOpenWindowRoots;

            for (int i = 0; i < snapshots.Count; i++)
                RestoreStyle(snapshots[i]);

            for (int i = 0; i < rootSnapshots.Count; i++)
                RestoreRoot(rootSnapshots[i]);

            snapshots.Clear();
            rootSnapshots.Clear();
            themedTextureCache.Clear();
            DestroyCreatedTextures();
            globalStyleSheet = null;
            applied = false;
            if (wasApplied)
                ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            InternalEditorUtility.RepaintAllViews();
        }

        public static void Synchronize(bool shouldApply)
        {
            if (!shouldApply || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            {
                Restore();
                return;
            }

            if (applied)
            {
                RefreshOpenWindowRoots();
                return;
            }

            TryApply(out _);
        }

        public static bool Refresh(out string message)
        {
            Restore();
            return TryApply(out message);
        }

        private static bool TryGetCurrentEditorStyles(out object currentStyles, out string message)
        {
            editorStylesInitializationPending = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 正在编译或导入资源，ES 全局皮肤将在 Editor 空闲后重试。";
                return false;
            }

            // Force common lazy properties to initialize before the bounded field pass.
            GUIStyle currentLabel;
            GUIStyle currentToolbar;
            GUIStyle currentTextField;
            GUIStyle currentButton;
            GUIStyle currentHelpBox;
            try
            {
                currentLabel = EditorStyles.label;
                currentToolbar = EditorStyles.toolbar;
                currentTextField = EditorStyles.textField;
                currentButton = EditorStyles.miniButton;
                currentHelpBox = EditorStyles.helpBox;
            }
            catch (NullReferenceException)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity EditorStyles 正在初始化，ES 全局皮肤将在下一次 Editor 回调中重试。";
                return false;
            }
            if (currentLabel == null || currentToolbar == null || currentTextField == null
                || currentButton == null || currentHelpBox == null)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 尚未准备完成，ES 全局皮肤将延迟重试。";
                return false;
            }

            FieldInfo currentField = typeof(EditorStyles).GetField(
                "s_Current",
                BindingFlags.Static | BindingFlags.NonPublic);
            currentStyles = currentField?.GetValue(null);
            if (currentStyles == null)
            {
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 当前容器尚未初始化，ES 全局皮肤将延迟重试。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void QueueInitializationRetry()
        {
            if (initializationRetryQueued
                || initializationRetryCount >= MaxEditorStylesInitializationRetries
                || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.isBatchMode)
                return;

            initializationRetryQueued = true;
            initializationRetryCount++;
            EditorApplication.delayCall -= RetryInitialization;
            EditorApplication.delayCall += RetryInitialization;
        }

        private static void RetryInitialization()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;

            ESGlobalEditorTheme current = ESGlobalEditorTheme.Instance;
            bool shouldApply = current != null
                && current.enableGlobalEditorShell
                && current.enableDeepEditorSkin
                && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (!shouldApply)
            {
                CancelInitializationRetry();
                return;
            }

            TryApply(out _);
        }

        private static void CancelInitializationRetry()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;
            initializationRetryCount = 0;
            editorStylesInitializationPending = false;
        }

        private static void ApplyEditorStyles(object currentStyles)
        {
            FieldInfo[] fields = currentStyles.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Color normalText = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.88f, 0.92f, 1f)
                : new Color(0.15f, 0.18f, 0.22f, 1f);
            Color interactiveText = Color.Lerp(normalText, ESEditorPresentation.LogicSteelBlue,
                EditorGUIUtility.isProSkin ? 0.34f : 0.28f);
            Color selectedText = EditorGUIUtility.isProSkin
                ? new Color(0.94f, 0.97f, 1f, 1f)
                : new Color(0.06f, 0.13f, 0.19f, 1f);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(GUIStyle) || field.IsLiteral)
                    continue;

                try
                {
                    GUIStyle style = field.GetValue(currentStyles) as GUIStyle;
                    if (style == null || ContainsStyle(style))
                        continue;

                    StyleRole role = ClassifyStyle(field.Name, style.name);
                    if (role == StyleRole.None)
                        continue;

                    StyleSnapshot snapshot = CaptureStyle(style);
                    snapshots.Add(snapshot);
                    ApplyStyle(style, role, normalText, interactiveText, selectedText);
                }
                catch
                {
                    // Unity 内部字段逐项隔离；不可访问字段不会中断其余可逆样式。
                }
            }

            // Unity 内置 Inspector/Scene GUISkin 是跨窗口共享对象。修改它会污染所有
            // Editor 页面，因此深度皮肤只处理已识别的 EditorStyles 文本语义。
        }

        private static void ApplyBuiltInSkin(
            EditorSkin editorSkin,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            GUISkin skin;
            try
            {
                skin = EditorGUIUtility.GetBuiltinSkin(editorSkin);
            }
            catch
            {
                return;
            }
            if (skin == null)
                return;

            ApplyKnownStyle(skin.label, StyleRole.Text, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.button, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.box, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.toggle, StyleRole.InteractiveText, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textField, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textArea, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.window, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);

            GUIStyle[] customStyles = skin.customStyles;
            if (customStyles == null)
                return;
            for (int i = 0; i < customStyles.Length; i++)
            {
                GUIStyle style = customStyles[i];
                if (style == null)
                    continue;
                StyleRole role = ClassifyStyle(style.name, style.name);
                ApplyKnownStyle(style, role, normalText, interactiveText, selectedText);
            }
        }

        private static void ApplyKnownStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            if (style == null || role == StyleRole.None || ContainsStyle(style))
                return;
            try
            {
                StyleSnapshot snapshot = CaptureStyle(style);
                snapshots.Add(snapshot);
                ApplyStyle(style, role, normalText, interactiveText, selectedText);
            }
            catch
            {
                // 内置皮肤逐样式隔离，保留已捕获快照供完整恢复。
            }
        }

        private static StyleSnapshot CaptureStyle(GUIStyle style)
        {
            return new StyleSnapshot
            {
                style = style,
                states = new[]
                {
                    CaptureState(style.normal),
                    CaptureState(style.hover),
                    CaptureState(style.active),
                    CaptureState(style.focused),
                    CaptureState(style.onNormal),
                    CaptureState(style.onHover),
                    CaptureState(style.onActive),
                    CaptureState(style.onFocused)
                }
            };
        }

        private static StateSnapshot CaptureState(GUIStyleState state)
        {
            Texture2D[] scaled = state.scaledBackgrounds;
            return new StateSnapshot
            {
                state = state,
                textColor = state.textColor,
                background = state.background,
                scaledBackgrounds = scaled == null ? null : (Texture2D[])scaled.Clone()
            };
        }

        private static void ApplyStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            Color baseText = role == StyleRole.Text ? normalText : interactiveText;
            ApplyState(style.normal, baseText, GetNormalTone(role));
            ApplyState(style.hover, selectedText, SkinTone.Hover);
            ApplyState(style.active, selectedText, SkinTone.Active);
            ApplyState(style.focused, selectedText, SkinTone.Focused);
            ApplyState(style.onNormal, selectedText, SkinTone.Active);
            ApplyState(style.onHover, selectedText, SkinTone.Hover);
            ApplyState(style.onActive, selectedText, SkinTone.Active);
            ApplyState(style.onFocused, selectedText, SkinTone.Focused);
        }

        private static void ApplyState(GUIStyleState state, Color textColor, SkinTone tone)
        {
            // background == null 在 Unity IMGUI 中通常表示透明宿主表面，不能替换为
            // 不透明纯色纹理。仅对已有背景保留透明度与形状后做 ES 色调染色。
            state.textColor = textColor;
            if (state.background == null)
                return;

            state.background = GetThemedTexture(state.background, tone);
            Texture2D[] scaled = state.scaledBackgrounds;
            if (scaled == null || scaled.Length == 0)
                return;

            Texture2D[] themedScaled = new Texture2D[scaled.Length];
            for (int i = 0; i < scaled.Length; i++)
                themedScaled[i] = scaled[i] == null ? null : GetThemedTexture(scaled[i], tone);
            state.scaledBackgrounds = themedScaled;
        }

        private static SkinTone GetNormalTone(StyleRole role)
        {
            switch (role)
            {
                case StyleRole.Toolbar:
                    return SkinTone.Toolbar;
                case StyleRole.Input:
                    return SkinTone.Input;
                case StyleRole.Help:
                    return SkinTone.Help;
                case StyleRole.Button:
                case StyleRole.Header:
                    return SkinTone.Raised;
                case StyleRole.Selection:
                    return SkinTone.Active;
                default:
                    return SkinTone.Surface;
            }
        }

        private static bool ProvidesBackground(StyleRole role)
        {
            return role == StyleRole.Toolbar
                || role == StyleRole.Button
                || role == StyleRole.Input
                || role == StyleRole.Header
                || role == StyleRole.Help
                || role == StyleRole.Selection;
        }

        private static StyleRole ClassifyStyle(string fieldName, string styleName)
        {
            string name = (fieldName ?? string.Empty) + " " + (styleName ?? string.Empty);
            if (ContainsIgnoreCase(name, "toolbar"))
                return StyleRole.Toolbar;
            if (ContainsIgnoreCase(name, "helpbox") || ContainsIgnoreCase(name, "notification"))
                return StyleRole.Help;
            if (ContainsIgnoreCase(name, "textfield") || ContainsIgnoreCase(name, "textarea")
                || ContainsIgnoreCase(name, "numberfield") || ContainsIgnoreCase(name, "objectfield")
                || ContainsIgnoreCase(name, "colorfield") || ContainsIgnoreCase(name, "searchfield")
                || ContainsIgnoreCase(name, "popup") || ContainsIgnoreCase(name, "dropdown")
                || ContainsIgnoreCase(name, "layermask"))
                return StyleRole.Input;
            if (ContainsIgnoreCase(name, "button"))
                return StyleRole.Button;
            if (ContainsIgnoreCase(name, "titlebar") || ContainsIgnoreCase(name, "header"))
                return StyleRole.Header;
            if (ContainsIgnoreCase(name, "selection") || ContainsIgnoreCase(name, "selected"))
                return StyleRole.Selection;
            if (ContainsIgnoreCase(name, "foldout") || ContainsIgnoreCase(name, "toggle")
                || ContainsIgnoreCase(name, "radio"))
                return StyleRole.InteractiveText;
            if (ContainsIgnoreCase(name, "label") || ContainsIgnoreCase(name, "link"))
                return StyleRole.Text;
            return StyleRole.None;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D GetThemedTexture(Texture2D source, SkinTone tone)
        {
            if (source == null)
                return null;

            int sourceId = source.GetInstanceID();
            long key = ((long)sourceId << 8) ^ (int)tone;
            if (themedTextureCache.TryGetValue(key, out Texture2D cached))
                return cached;

            Texture2D themed = CreateTintedTexture(source, tone);
            if (themed == null)
                themed = source;
            themedTextureCache[key] = themed;
            return themed;
        }

        private static Texture2D CreateTintedTexture(Texture2D source, SkinTone tone)
        {
            long pixelCount = source == null ? 0L : (long)source.width * source.height;
            long requiredBytes = pixelCount * 4L;
            if (source == null || source.width <= 0 || source.height <= 0
                || pixelCount > MaxTintTexturePixels || !CanCreateTexture(requiredBytes))
                return source;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D output = null;
            try
            {
                temporary = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                output = new Texture2D(source.width, source.height, UnityEngine.TextureFormat.RGBA32, false)
                {
                    name = "ES Deep Skin " + source.name + " " + tone,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                output.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                Color32[] pixels = output.GetPixels32();
                Color target = GetToneColor(tone);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color original = pixels[i];
                    float luminance = original.r * 0.2126f + original.g * 0.7152f + original.b * 0.0722f;
                    float brightness = Mathf.Lerp(0.72f, 1.18f, luminance);
                    Color tinted = new Color(
                        Mathf.Clamp01(target.r * brightness),
                        Mathf.Clamp01(target.g * brightness),
                        Mathf.Clamp01(target.b * brightness),
                        original.a);
                    Color blended = Color.Lerp(original, tinted, 0.76f);
                    blended.a = original.a;
                    pixels[i] = blended;
                }

                output.SetPixels32(pixels);
                output.Apply(false, true);
                createdTextures.Add(output);
                createdTextureBytes += requiredBytes;
                return output;
            }
            catch
            {
                if (output != null)
                    UnityEngine.Object.DestroyImmediate(output);
                return source;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null)
                    RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Color GetToneColor(SkinTone tone)
        {
            if (!EditorGUIUtility.isProSkin)
            {
                switch (tone)
                {
                    case SkinTone.Toolbar: return new Color(0.78f, 0.82f, 0.86f, 1f);
                    case SkinTone.Input: return new Color(0.86f, 0.89f, 0.92f, 1f);
                    case SkinTone.Raised: return new Color(0.80f, 0.84f, 0.88f, 1f);
                    case SkinTone.Hover: return new Color(0.63f, 0.76f, 0.86f, 1f);
                    case SkinTone.Active: return new Color(0.36f, 0.62f, 0.80f, 1f);
                    case SkinTone.Focused: return new Color(0.48f, 0.70f, 0.84f, 1f);
                    case SkinTone.Help: return new Color(0.76f, 0.83f, 0.88f, 1f);
                    default: return new Color(0.83f, 0.86f, 0.89f, 1f);
                }
            }

            switch (tone)
            {
                case SkinTone.Toolbar: return new Color(0.105f, 0.13f, 0.16f, 1f);
                case SkinTone.Input: return new Color(0.13f, 0.17f, 0.205f, 1f);
                case SkinTone.Raised: return new Color(0.17f, 0.215f, 0.255f, 1f);
                case SkinTone.Hover: return new Color(0.18f, 0.30f, 0.38f, 1f);
                case SkinTone.Active: return new Color(0.12f, 0.38f, 0.55f, 1f);
                case SkinTone.Focused: return new Color(0.14f, 0.32f, 0.44f, 1f);
                case SkinTone.Help: return new Color(0.16f, 0.22f, 0.27f, 1f);
                default: return new Color(0.135f, 0.165f, 0.195f, 1f);
            }
        }

        private static void RefreshOpenWindowRoots()
        {
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;

            if (globalStyleSheet == null)
                globalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GlobalStyleSheetPath);
            if (globalStyleSheet == null)
                return;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                VisualElement root = window == null ? null : window.rootVisualElement;
                if (root == null || ContainsRoot(root))
                    continue;

                if (!root.styleSheets.Contains(globalStyleSheet))
                    root.styleSheets.Add(globalStyleSheet);
                root.AddToClassList(RootClass);
                root.EnableInClassList(DarkRootClass, EditorGUIUtility.isProSkin);
                root.EnableInClassList(LightRootClass, !EditorGUIUtility.isProSkin);
                rootSnapshots.Add(new RootSnapshot { root = root });
                root.MarkDirtyRepaint();
            }
        }

        private static void RestoreStyle(StyleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.style == null || snapshot.states == null)
                return;

            for (int i = 0; i < snapshot.states.Length; i++)
            {
                StateSnapshot state = snapshot.states[i];
                if (state == null || state.state == null)
                    continue;
                try
                {
                    state.state.textColor = state.textColor;
                    state.state.background = state.background;
                    state.state.scaledBackgrounds = state.scaledBackgrounds;
                }
                catch
                {
                    // 恢复逐状态隔离；一个 Unity 内部状态失效不阻断其余样式恢复。
                }
            }
        }

        private static void RestoreRoot(RootSnapshot snapshot)
        {
            VisualElement root = snapshot == null ? null : snapshot.root;
            if (root == null)
                return;
            root.RemoveFromClassList(RootClass);
            root.RemoveFromClassList(DarkRootClass);
            root.RemoveFromClassList(LightRootClass);
            if (globalStyleSheet != null && root.styleSheets.Contains(globalStyleSheet))
                root.styleSheets.Remove(globalStyleSheet);
            root.MarkDirtyRepaint();
        }

        private static void DestroyCreatedTextures()
        {
            for (int i = 0; i < createdTextures.Count; i++)
                if (createdTextures[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdTextures[i]);
            createdTextures.Clear();
            createdTextureBytes = 0L;
        }

        private static bool CanCreateTexture(long requiredBytes)
        {
            return requiredBytes > 0L
                && createdTextures.Count < MaxCreatedTextureCount
                && createdTextureBytes + requiredBytes <= MaxCreatedTextureBytes;
        }

        private static string BuildAppliedMessage()
        {
            return "ES 全局深度皮肤已覆盖 " + snapshots.Count + " 个 IMGUI 文字样式和 "
                + rootSnapshots.Count + " 个 UI Toolkit 窗口；纯色只应用到安全内容容器，"
                + "原生窗口根节点与透明绘制层保持不变。进入 PlayMode 自动停用，可随时恢复原生样式。";
        }

        private static bool ContainsStyle(GUIStyle style)
        {
            for (int i = 0; i < snapshots.Count; i++)
                if (ReferenceEquals(snapshots[i].style, style))
                    return true;
            return false;
        }

        private static bool ContainsRoot(VisualElement root)
        {
            for (int i = 0; i < rootSnapshots.Count; i++)
                if (ReferenceEquals(rootSnapshots[i].root, root))
                    return true;
            return false;
        }

    }
}
