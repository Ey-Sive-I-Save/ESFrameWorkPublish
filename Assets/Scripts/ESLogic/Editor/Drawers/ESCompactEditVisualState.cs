using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESCompactEditVisualState
    {
        private static int depth;
        private static int highlightedObjectId;

        public static int CurrentDepth => depth;

        public static int EnterDepth()
        {
            int current = depth;
            depth++;
            return current;
        }

        public static void ExitDepth()
        {
            depth = Mathf.Max(0, depth - 1);
        }

        public static void Highlight(int objectId)
        {
            highlightedObjectId = objectId;
        }

        public static void ClearHighlight(int objectId)
        {
            if (highlightedObjectId == objectId)
                highlightedObjectId = 0;
        }

        public static Color GetBackgroundColor(int currentDepth)
        {
            float shade = Mathf.Clamp01(0.23f - currentDepth * 0.022f);
            return new Color(shade, shade, shade, 1f);
        }

        public static Color GetBorderColor(int currentDepth, int objectId)
        {
            if (objectId != 0 && objectId == highlightedObjectId)
                return new Color(1f, 0.72f, 0.16f, 1f);

            float shade = Mathf.Clamp01(0.12f - currentDepth * 0.012f);
            return new Color(shade, shade, shade, 1f);
        }

        public static int GetBorderWidth(int objectId)
        {
            return objectId != 0 && objectId == highlightedObjectId ? 3 : 1;
        }

        public static void DrawFrame(Rect rect, int currentDepth, int objectId)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            DrawBorder(rect, GetBorderColor(currentDepth, objectId), GetBorderWidth(objectId));
        }

        public static float GetInlineLabelWidth()
        {
            float byWindow = EditorGUIUtility.currentViewWidth * 0.085f;
            float byDepth = 68f - Mathf.Max(0, depth - 1) * 4f;
            return Mathf.Clamp(Mathf.Min(byWindow, byDepth), 42f, 68f);
        }

        private static void DrawBorder(Rect rect, Color color, int width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
