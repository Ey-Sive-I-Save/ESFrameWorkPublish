using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Lightweight field-row decoration. It does not replace Odin controls or own their layout;
    /// it only adds a stable status marker and reserves a small text inset for ES metadata.
    /// Ordinary primitive fields can keep using Odin's direct drawing path.
    /// </summary>
    internal static class ESFieldRow
    {
        private const float StatusMarkerSize = 4f;
        private const float StatusMarkerGap = 6f;

        public static void DrawStatus(
            Rect rect,
            ESStatusKind status,
            string text,
            GUIStyle textStyle)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Rect textRect = rect;
            if (status != ESStatusKind.None)
            {
                Rect markerRect = new Rect(
                    rect.x,
                    rect.y + Mathf.Max(0f, (rect.height - StatusMarkerSize) * 0.5f),
                    StatusMarkerSize,
                    StatusMarkerSize);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(
                        markerRect,
                        ESEditorPresentation.GetStatusAccent(0, status));
                }

                textRect = new Rect(
                    rect.x + StatusMarkerSize + StatusMarkerGap,
                    rect.y,
                    Mathf.Max(0f, rect.width - StatusMarkerSize - StatusMarkerGap),
                    rect.height);
            }

            GUI.Label(textRect, text, textStyle);
        }
    }
}
