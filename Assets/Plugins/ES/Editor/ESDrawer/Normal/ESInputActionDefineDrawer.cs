using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomPropertyDrawer(typeof(ESInputActionDefine))]
    public sealed class ESInputActionDefineDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty triggerFeatures = property.FindPropertyRelative("triggerFeatures");
            SerializedProperty bindings = property.FindPropertyRelative("bindings");

            float height = Line * 4f + Gap * 5f;
            if (IsButton(valueType))
            {
                height += (Line + Gap) * 2f;
                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.LongPress))
                    height += Line + Gap;
                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.DoublePress))
                    height += Line + Gap;
            }

            if (bindings != null)
                height += EditorGUI.GetPropertyHeight(bindings, true) + Gap;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty id = property.FindPropertyRelative("id");
            SerializedProperty actionName = property.FindPropertyRelative("actionName");
            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty category = property.FindPropertyRelative("category");
            SerializedProperty allowRebind = property.FindPropertyRelative("allowRebind");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            SerializedProperty triggerFeatures = property.FindPropertyRelative("triggerFeatures");
            SerializedProperty pressPolicy = property.FindPropertyRelative("pressPolicy");
            SerializedProperty longPressDuration = property.FindPropertyRelative("longPressDuration");
            SerializedProperty doublePressWindow = property.FindPropertyRelative("doublePressWindow");
            SerializedProperty bindings = property.FindPropertyRelative("bindings");

            EditorGUI.LabelField(NextLine(ref position), label, EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.28f, out Rect left, out Rect right);
            EditorGUI.PropertyField(left, id, new GUIContent("动作"));
            EditorGUI.PropertyField(right, actionName, new GUIContent("内部名"));

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            EditorGUI.PropertyField(left, valueType, new GUIContent("值类型"));
            EditorGUI.PropertyField(right, category, new GUIContent("分类"));

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            EditorGUI.PropertyField(left, allowRebind, new GUIContent("允许改键"));
            EditorGUI.PropertyField(right, displayName, new GUIContent("显示名"));

            if (IsButton(valueType))
            {
                row = NextLine(ref position);
                EditorGUI.PropertyField(row, triggerFeatures, new GUIContent("触发标记"));

                row = NextLine(ref position);
                EditorGUI.PropertyField(row, pressPolicy, new GUIContent("短按策略"));

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.LongPress))
                {
                    row = NextLine(ref position);
                    EditorGUI.PropertyField(row, longPressDuration, new GUIContent("长按秒数"));
                }

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.DoublePress))
                {
                    row = NextLine(ref position);
                    EditorGUI.PropertyField(row, doublePressWindow, new GUIContent("双击窗口"));
                }
            }

            if (bindings != null)
            {
                float bindingsHeight = EditorGUI.GetPropertyHeight(bindings, true);
                Rect bindingsRect = new Rect(position.x, position.y, position.width, bindingsHeight);
                EditorGUI.PropertyField(bindingsRect, bindings, new GUIContent("绑定列表"), true);
            }

            EditorGUI.EndProperty();
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect row = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return row;
        }

        private static void DrawSplit(Rect row, float leftRatio, out Rect left, out Rect right)
        {
            float leftWidth = Mathf.Max(80f, row.width * leftRatio);
            left = new Rect(row.x, row.y, leftWidth - Gap * 0.5f, row.height);
            right = new Rect(row.x + leftWidth + Gap * 0.5f, row.y, row.width - leftWidth - Gap * 0.5f, row.height);
        }

        private static bool IsButton(SerializedProperty valueType)
        {
            return valueType != null && valueType.enumValueIndex == (int)ESInputValueType.Button;
        }

        private static bool HasTriggerFeature(SerializedProperty triggerFeatures, ESInputTriggerFeature feature)
        {
            return triggerFeatures != null && (triggerFeatures.intValue & (int)feature) != 0;
        }
    }
}
