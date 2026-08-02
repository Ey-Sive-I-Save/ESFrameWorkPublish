using UnityEditor;
using UnityEngine;

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
        private static Texture2D surfaceTexture;
        private static ESGlobalEditorTheme theme;
        private static bool themeInitialized;

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

            if (surfaceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(surfaceTexture);
                surfaceTexture = null;
            }
        }
    }
}
