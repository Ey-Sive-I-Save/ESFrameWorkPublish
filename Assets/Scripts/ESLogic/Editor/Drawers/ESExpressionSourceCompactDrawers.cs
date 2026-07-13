using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public abstract class ESExpressionSourceCompactDrawer<T> : OdinValueDrawer<T> where T : class
    {
        private static readonly HashSet<int> ForceInlineObjects = new HashSet<int>();
        private static readonly Dictionary<Type, SourceFieldCache> FieldCacheByType = new Dictionary<Type, SourceFieldCache>();
        private static readonly Dictionary<Type, string> TypeNameCache = new Dictionary<Type, string>();
        private bool drawInline = true;
        protected override void DrawPropertyLayout(GUIContent label)
        {
            T value = ValueEntry.SmartValue;
            if (value == null)
            {
                CallNextDrawer(label);
                return;
            }

            int objectId = RuntimeHelpersSafe.GetObjectId(value);
            bool forceInline = ForceInlineObjects.Contains(objectId);
            int depth = ESCompactEditVisualState.EnterDepth();
            Color oldBackgroundColor = GUI.backgroundColor;

            try
            {
                GUI.backgroundColor = ESCompactEditVisualState.GetBackgroundColor(depth);
                SirenixEditorGUI.BeginBox();
                GUI.backgroundColor = oldBackgroundColor;
                DrawHeader(label, value, forceInline);

                if (drawInline || forceInline)
                    DrawInlineContent();

                SirenixEditorGUI.EndBox();
                Rect rect = GUILayoutUtility.GetLastRect();
                ESCompactEditVisualState.DrawFrame(rect, depth, objectId);
            }
            finally
            {
                GUI.backgroundColor = oldBackgroundColor;
                ESCompactEditVisualState.ExitDepth();
            }
        }

        private void DrawInlineContent()
        {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            int oldIndent = EditorGUI.indentLevel;

            EditorGUIUtility.labelWidth = ESCompactEditVisualState.GetInlineLabelWidth();
            EditorGUI.indentLevel = 0;
            CallNextDrawer(GUIContent.none);

            EditorGUI.indentLevel = oldIndent;
            EditorGUIUtility.labelWidth = oldLabelWidth;
        }

        private void DrawHeader(GUIContent label, T value, bool forceInline)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);

            string displayName = label != null && !string.IsNullOrEmpty(label.text)
                ? label.text
                : Property.NiceName;

            Rect nameRect = new Rect(rect.x, rect.y, Mathf.Min(260f, rect.width * 0.42f), rect.height);
            Rect summaryRect = new Rect(nameRect.xMax + 6f, rect.y, Mathf.Max(40f, rect.width - nameRect.width - 6f - 116f), rect.height);
            Rect foldRect = new Rect(rect.xMax - 112f, rect.y, 46f, rect.height);
            Rect popupRect = new Rect(rect.xMax - 64f, rect.y, 64f, rect.height);

            EditorGUI.LabelField(nameRect, displayName, EditorStyles.boldLabel);
            EditorGUI.LabelField(summaryRect, GetSummary(value), SirenixGUIStyles.LeftAlignedGreyMiniLabel);

            using (new EditorGUI.DisabledScope(forceInline))
            {
                if (GUI.Button(foldRect, drawInline ? "收起" : "展开", EditorStyles.miniButtonLeft))
                    drawInline = !drawInline;
            }

            if (GUI.Button(popupRect, "弹窗", EditorStyles.miniButtonRight))
                OpenEditWindow(displayName, value);
        }

        private void OpenEditWindow(string displayName, T value)
        {
            int objectId = RuntimeHelpersSafe.GetObjectId(value);
            ForceInlineObjects.Add(objectId);
            ESCompactEditVisualState.Highlight(objectId);

            OdinEditorWindow window = OdinEditorWindow.InspectObject(value);
            window.titleContent = new GUIContent($"编辑 {displayName}");
            window.minSize = new Vector2(680f, 520f);
            window.position = new Rect(window.position.x, window.position.y, 760f, 620f);
            window.OnClose += () =>
            {
                ForceInlineObjects.Remove(objectId);
                ESCompactEditVisualState.ClearHighlight(objectId);
                UnityEngine.Object unityObject = Property.SerializationRoot?.ValueEntry?.WeakSmartValue as UnityEngine.Object;
                if (unityObject != null)
                    EditorUtility.SetDirty(unityObject);
            };
        }

        private static string GetSummary(T value)
        {
            Type type = value.GetType();
            SourceFieldCache cache = GetFieldCache(type);
            if (cache.directSwitch != null && cache.directSwitch.FieldType == typeof(bool) && (bool)cache.directSwitch.GetValue(value))
            {
                object direct = cache.directValue != null ? cache.directValue.GetValue(value) : null;
                return "Direct: " + FormatValue(direct);
            }

            object expr = cache.expression != null ? cache.expression.GetValue(value) : null;
            return expr != null ? "Expression: " + FormatType(expr.GetType()) : "Expression: None";
        }

        private static SourceFieldCache GetFieldCache(Type type)
        {
            if (FieldCacheByType.TryGetValue(type, out SourceFieldCache cache))
                return cache;

            cache = new SourceFieldCache
            {
                directSwitch = FindFieldUncached(type, "useDirect"),
                directValue = FindFieldUncached(type, "direct"),
                expression = FindFieldUncached(type, "expression")
            };
            FieldCacheByType[type] = cache;
            return cache;
        }

        private static FieldInfo FindFieldUncached(Type type, string prefix)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name.StartsWith(prefix, StringComparison.Ordinal))
                    return fields[i];
            }

            return null;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is UnityEngine.Object unityObject)
                return unityObject != null ? unityObject.name : "null";

            return value.ToString();
        }

        private static string FormatType(Type type)
        {
            if (TypeNameCache.TryGetValue(type, out string cachedName))
                return cachedName;

            string name = type.Name;
            if (name.EndsWith("ExpressionSource", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "ExpressionSource".Length) + " Source";
            else
            {
                if (name.StartsWith("ES", StringComparison.Ordinal))
                    name = name.Substring(2);

                if (name.EndsWith("Expression", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - "Expression".Length) + " Expression";
            }

            TypeNameCache[type] = name;
            return name;
        }

        private sealed class SourceFieldCache
        {
            public FieldInfo directSwitch;
            public FieldInfo directValue;
            public FieldInfo expression;
        }
    }

    internal static class RuntimeHelpersSafe
    {
        public static int GetObjectId(object value)
        {
            return value != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value) : 0;
        }
    }

    public sealed class FloatExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<FloatExpressionSource> { }
    public sealed class BoolExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<BoolExpressionSource> { }
    public sealed class IntExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<IntExpressionSource> { }
    public sealed class StringExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<StringExpressionSource> { }
    public sealed class Vector3ExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<Vector3ExpressionSource> { }
    public sealed class EntityExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<EntityExpressionSource> { }
    public sealed class GameObjectExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<GameObjectExpressionSource> { }
    public sealed class AudioClipExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<AudioClipExpressionSource> { }
    public sealed class AnimationClipExpressionSourceCompactDrawer : ESExpressionSourceCompactDrawer<AnimationClipExpressionSource> { }
}
