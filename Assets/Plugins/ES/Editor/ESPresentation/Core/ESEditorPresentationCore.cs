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
        private static string selectedAssetGuid = string.Empty;
        private static int selectedHierarchyInstanceId = int.MinValue;

        /// <summary>
        /// 安装 ES 对 Unity 原生编辑器区域的轻量表现适配。只订阅官方绘制回调，
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

        private static void InstallGlobalEditorAdapterCallbacks()
        {
            if (globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawGlobalInspectorHeader;
            EditorApplication.hierarchyWindowItemOnGUI -= DrawGlobalHierarchyItem;
            EditorApplication.hierarchyWindowItemOnGUI += DrawGlobalHierarchyItem;
            EditorApplication.projectWindowItemOnGUI -= DrawGlobalProjectItem;
            EditorApplication.projectWindowItemOnGUI += DrawGlobalProjectItem;
            Selection.selectionChanged -= RefreshSelectedAssetGuid;
            Selection.selectionChanged += RefreshSelectedAssetGuid;
            RefreshSelectedAssetGuid();
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
            UnbindAllWindowBindings();
        }

        private static void UninstallGlobalEditorAdapterCallbacks()
        {
            if (!globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            EditorApplication.hierarchyWindowItemOnGUI -= DrawGlobalHierarchyItem;
            EditorApplication.projectWindowItemOnGUI -= DrawGlobalProjectItem;
            Selection.selectionChanged -= RefreshSelectedAssetGuid;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = false;
            selectedAssetGuid = string.Empty;
            selectedHierarchyInstanceId = int.MinValue;
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

        private static void SuspendWindowBindings()
        {
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null)
                    continue;
                binding.animation?.Pause();
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

        private static void DrawGlobalHierarchyItem(int instanceId, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint || selectionRect.width <= 0f || selectionRect.height <= 0f)
                return;

            if (selectedHierarchyInstanceId != instanceId)
                return;

            Color accent = GetDepthAccent(0);
            accent.a = IsProSkin ? 0.88f : 0.72f;
            Color previousGuiColor = GUI.color;
            GUI.color = Color.white;
            EditorGUI.DrawRect(new Rect(selectionRect.x, selectionRect.y, 2f, selectionRect.height), accent);
            GUI.color = previousGuiColor;
        }

        private static void DrawGlobalProjectItem(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint || selectionRect.width <= 0f || selectionRect.height <= 0f)
                return;

            if (string.IsNullOrEmpty(guid) || !string.Equals(guid, selectedAssetGuid, System.StringComparison.OrdinalIgnoreCase))
                return;

            Color accent = GetDepthAccent(0);
            accent.a = IsProSkin ? 0.88f : 0.72f;
            Color previousGuiColor = GUI.color;
            GUI.color = Color.white;
            EditorGUI.DrawRect(new Rect(selectionRect.x, selectionRect.y, 2f, selectionRect.height), accent);
            GUI.color = previousGuiColor;
        }

        private static void RefreshSelectedAssetGuid()
        {
            UnityEngine.Object selected = Selection.activeObject;
            selectedHierarchyInstanceId = Selection.activeInstanceID;
            string path = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            selectedAssetGuid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
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
                        ? new Color(0.61f, 0.64f, 0.68f, 1f)
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
                        ? new Color(0.60f, 0.64f, 0.69f, 1f)
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
            if (binding.host != null)
            {
                if (binding.root != null)
                {
                    binding.root.UnregisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
                    binding.root.UnregisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
                }
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

            binding.animation = binding.host.schedule.Execute(() => UpdateWindowOverlay(binding)).Every(33);
            binding.animation.Pause();
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
            if (binding == null || binding.window == null || binding.host == null || binding.host.parent == null)
                return;

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
                    ? new Color(0.72f, 0.74f, 0.77f, 1f)
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
                    ? new Color(0.42f, 0.44f, 0.48f, 1f)
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
                    ? new Color(0.63f, 0.66f, 0.70f, 1f)
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
                        ? new Color(0.34f, 0.37f, 0.40f, 1f)
                        : new Color(0.58f, 0.61f, 0.64f, 1f);
                    Color fillColor = cachedProSkin
                        ? new Color(0.22f, 0.23f, 0.25f, 1f)
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
                    new Color(0.28f, 0.29f, 0.32f, 0.90f),
                    new Color(0.08f, 0.15f, 0.23f, 0.96f),
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
                    new Color(0.35f, 0.44f, 0.54f, 0.88f),
                    new Color(0.12f, 0.24f, 0.36f, 0.94f),
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
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || binding.host == null || binding.host.parent == null)
                    continue;
                Color accent = GetDepthAccent(0);
                binding.host.style.backgroundColor = accent;
                if (binding.accentLine != null)
                    binding.accentLine.style.backgroundColor = accent;
                binding.window.Repaint();
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
    /// zios/unity-themes inspired experiment. It is intentionally opt-in and only adjusts a
    /// small, known set of EditorStyles GUIStyle fields for Unity 2022.3. It never runs during
    /// AssemblyStream initialization, never scans assets and always keeps a restore snapshot.
    /// </summary>
    internal static class ESGlobalEditorSkinExperiment
    {
        private static readonly Dictionary<FieldInfo, GUIStyle> snapshots =
            new Dictionary<FieldInfo, GUIStyle>(32);
        private static bool applied;

        public static bool IsApplied => applied;

        public static bool TryApply(out string message)
        {
            if (applied)
            {
                message = "ES 深度皮肤实验已经启用。";
                return true;
            }

            if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal))
            {
                message = "当前 Unity 版本不是 2022.3，实验皮肤已拒绝运行。";
                return false;
            }

            snapshots.Clear();
            FieldInfo[] fields = typeof(EditorStyles).GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Color accent = ESEditorPresentation.LogicSteelBlue;
            Color textAccent = Color.Lerp(
                EditorGUIUtility.isProSkin ? new Color(0.82f, 0.86f, 0.91f) : new Color(0.18f, 0.21f, 0.25f),
                accent,
                0.16f);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(GUIStyle) || field.IsInitOnly || field.IsLiteral)
                    continue;

                string name = field.Name;
                if (!IsSupportedStyleName(name))
                    continue;

                try
                {
                    GUIStyle original = field.GetValue(null) as GUIStyle;
                    if (original == null)
                        continue;

                    GUIStyle clone = new GUIStyle(original);
                    clone.normal.textColor = textAccent;
                    clone.hover.textColor = Color.Lerp(clone.hover.textColor, ESEditorPresentation.LogicGold, 0.12f);
                    snapshots[field] = original;
                    field.SetValue(null, clone);
                }
                catch
                {
                    // Unity 内部字段可能受版本或权限限制；单个字段失败不影响其余字段。
                }
            }

            if (snapshots.Count == 0)
            {
                message = "没有找到可安全调整的 EditorStyles 字段，未改变 Unity 原生样式。";
                return false;
            }

            InvokeSkinChanged();
            applied = true;
            InternalEditorUtility.RepaintAllViews();
            message = "ES 深度皮肤实验已启用；可随时恢复 Unity 原生样式。";
            return true;
        }

        public static void Restore()
        {
            if (!applied && snapshots.Count == 0)
                return;

            foreach (KeyValuePair<FieldInfo, GUIStyle> pair in snapshots)
            {
                try
                {
                    pair.Key.SetValue(null, pair.Value);
                }
                catch
                {
                    // 恢复逐字段隔离；剩余字段继续恢复，避免半途退出。
                }
            }

            snapshots.Clear();
            applied = false;
            InvokeSkinChanged();
            InternalEditorUtility.RepaintAllViews();
        }

        private static bool IsSupportedStyleName(string name)
        {
            return name.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("foldout", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("toolbar", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("helpBox", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("objectField", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void InvokeSkinChanged()
        {
            try
            {
                MethodInfo method = typeof(EditorGUIUtility).GetMethod(
                    "SkinChanged",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(null, null);
            }
            catch
            {
                // SkinChanged 是 Unity 内部实现细节；调用失败时仍保留当前快照和恢复路径。
            }
        }
    }
}
