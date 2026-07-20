using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal enum SimpleToolsActionTone
    {
        Neutral,
        Primary,
        Success,
        Warning,
        Danger
    }

    internal static class SimpleToolsPanelUtility
    {
        public static readonly Color PrimaryColor = new Color(0.28f, 0.52f, 0.85f);
        public static readonly Color SuccessColor = new Color(0.25f, 0.62f, 0.45f);
        public static readonly Color WarningColor = new Color(0.78f, 0.56f, 0.22f);
        public static readonly Color DangerColor = new Color(0.82f, 0.38f, 0.30f);
        public static readonly Color NeutralColor = new Color(0.48f, 0.48f, 0.48f);

        public static void DrawSectionTitle(string title, string subtitle = null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(subtitle))
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
        }

        public static void DrawSummary(params string[] items)
        {
            DrawSummary((IEnumerable<string>)items);
        }

        public static void DrawSummary(IEnumerable<string> items)
        {
            string text = string.Join("  |  ", (items ?? Enumerable.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)));
            if (string.IsNullOrEmpty(text))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }

        public static void DrawEmptyState(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        public static void DrawWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        public static void DrawResultSummary(string title, string summary, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                DrawEmptyState("还没有执行结果。完成一次扫描、预览或批处理后，这里会显示最近结果。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(title) ? "最近结果" : title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.TextArea(detail, GUILayout.MinHeight(42), GUILayout.MaxHeight(110));
                }
            }
        }

        public static bool DrawActionButton(string label, SimpleToolsActionTone tone, int height = 28, params GUILayoutOption[] options)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = GetToneColor(tone);
            bool clicked = GUILayout.Button(label, MergeHeight(height, options));
            GUI.backgroundColor = previous;
            return clicked;
        }

        private static Color GetToneColor(SimpleToolsActionTone tone)
        {
            switch (tone)
            {
                case SimpleToolsActionTone.Primary:
                    return PrimaryColor;
                case SimpleToolsActionTone.Success:
                    return SuccessColor;
                case SimpleToolsActionTone.Warning:
                    return WarningColor;
                case SimpleToolsActionTone.Danger:
                    return DangerColor;
                default:
                    return NeutralColor;
            }
        }

        private static GUILayoutOption[] MergeHeight(int height, GUILayoutOption[] options)
        {
            if (options == null || options.Length == 0)
                return new[] { GUILayout.Height(height) };

            var merged = new GUILayoutOption[options.Length + 1];
            merged[0] = GUILayout.Height(height);
            Array.Copy(options, 0, merged, 1, options.Length);
            return merged;
        }
    }
}
