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

        public static GUIStyle SurfaceStyle
        {
            get
            {
                EnsureSkin();
                if (surfaceStyle == null)
                {
                    surfaceStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, 2, 2),
                        padding = new RectOffset(9, 9, 7, 8),
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
                        padding = new RectOffset(0, 0, 0, 2)
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
                        padding = new RectOffset(0, 0, 1, 3)
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
                        padding = new RectOffset(0, 0, 0, 1)
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
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.48f, 0.78f, 1f, 0.92f),
                    new Color(0.13f, 0.42f, 0.72f, 0.96f),
                    progress)
                : Color.Lerp(
                    new Color(0.12f, 0.46f, 0.82f, 0.92f),
                    new Color(0.04f, 0.24f, 0.56f, 0.96f),
                    progress);
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

        public static Color GetFrameColor(int depth, bool hasValue, bool hasUnresolvedType)
        {
            EnsureSkin();
            if (hasUnresolvedType)
                return new Color(0.86f, 0.47f, 0.20f, 0.78f);

            if (!hasValue)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            Color accent = GetDepthAccent(depth);
            accent.a = cachedProSkin ? 0.72f : 0.64f;
            return accent;
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
