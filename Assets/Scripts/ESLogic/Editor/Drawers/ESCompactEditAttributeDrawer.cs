using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESCompactEditAttributeDrawer : OdinAttributeDrawer<ESCompactEditAttribute>
    {
        private bool drawInline = true;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            object value = Property.ValueEntry.WeakSmartValue;
            if (value == null)
            {
                CallNextDrawer(label);
                return;
            }

            if (IsExpressionSource(value))
            {
                CallNextDrawer(label);
                return;
            }

            int objectId = RuntimeHelpersSafe.GetObjectId(value);
            int depth = ESCompactEditVisualState.EnterDepth();
            Color oldBackgroundColor = GUI.backgroundColor;

            try
            {
                GUI.backgroundColor = ESCompactEditVisualState.GetBackgroundColor(depth);
                EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
                GUI.backgroundColor = oldBackgroundColor;
                DrawHeader(label, value);

                if (drawInline)
                    DrawInlineContent();

                EditorGUILayout.EndVertical();
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

        private void DrawHeader(GUIContent label, object value)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22f);

            string displayName = label != null && !string.IsNullOrEmpty(label.text)
                ? label.text
                : Property.NiceName;

            string typeName = GetTypeSummary(value);
            string title = string.IsNullOrEmpty(Attribute.title) ? displayName : Attribute.title;
            Rect summaryRect = new Rect(rect.x, rect.y, Mathf.Max(40f, rect.width - 116f), rect.height);
            Rect foldRect = new Rect(rect.xMax - 112f, rect.y, 46f, rect.height);
            Rect popupRect = new Rect(rect.xMax - 64f, rect.y, 64f, rect.height);

            EditorGUI.LabelField(summaryRect, typeName, SirenixGUIStyles.LeftAlignedGreyMiniLabel);

            if (GUI.Button(foldRect, drawInline ? "收起" : "展开", EditorStyles.miniButtonLeft))
                drawInline = !drawInline;

            if (GUI.Button(popupRect, "弹窗", EditorStyles.miniButtonRight))
                OpenEditWindow(displayName, value);
        }

        private void OpenEditWindow(string displayName, object value)
        {
            if (value == null)
                return;

            int objectId = RuntimeHelpersSafe.GetObjectId(value);
            ESCompactEditVisualState.Highlight(objectId);

            var wrapper = new ESCompactEditWindowTarget(displayName, value);
            OdinEditorWindow window = OdinEditorWindow.InspectObject(wrapper);
            window.titleContent = new GUIContent($"编辑 {displayName}");
            window.minSize = new Vector2(680f, 520f);
            window.position = new Rect(window.position.x, window.position.y, 760f, 620f);
            window.OnClose += () =>
            {
                ESCompactEditVisualState.ClearHighlight(objectId);
                UnityEngine.Object unityObject = Property.SerializationRoot?.ValueEntry?.WeakSmartValue as UnityEngine.Object;
                if (unityObject != null)
                    EditorUtility.SetDirty(unityObject);
            };
        }

        private static string GetTypeSummary(object value)
        {
            Type type = value.GetType();
            string name = type.Name;
            if (name.EndsWith("ExpressionSource", StringComparison.Ordinal))
                return name.Substring(0, name.Length - "ExpressionSource".Length) + " Source";

            if (name.StartsWith("ES", StringComparison.Ordinal))
                name = name.Substring(2);

            if (name.EndsWith("Expression", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Expression".Length) + " Expression";

            return name;
        }

        private static bool IsExpressionSource(object value)
        {
            return value is FloatExpressionSource
                || value is BoolExpressionSource
                || value is IntExpressionSource
                || value is StringExpressionSource
                || value is Vector3ExpressionSource
                || value is EntityExpressionSource
                || value is GameObjectExpressionSource
                || value is AudioClipExpressionSource
                || value is AnimationClipExpressionSource;
        }

        private sealed class ESCompactEditWindowTarget
        {
            [ShowInInspector, ReadOnly, LabelText("目标")]
            public string title;

            [SerializeReference]
            [ShowInInspector, HideLabel]
            public object target;

            public ESCompactEditWindowTarget(string title, object target)
            {
                this.title = title;
                this.target = target;
            }
        }
    }
}
