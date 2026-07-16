using System;
using System.IO;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    [CustomPropertyDrawer(typeof(ESInputActionDefine))]
    public sealed class ESInputActionDefineDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;
        private const string ActionIdFilePath = "Assets/Plugins/ES/1_Design/Input/ENUM_ESInputActionId.cs";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty triggerFeatures = property.FindPropertyRelative("triggerFeatures");
            SerializedProperty bindings = property.FindPropertyRelative("bindings");

            float height = Line * 4f + Gap * 5f;
            if (IsButton(valueType))
            {
                height += Line + Gap;
                height += Line + Gap;
                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.LongPress))
                {
                    height += Line + Gap;
                }

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.DoublePress))
                {
                    height += Line + Gap;
                }
            }

            if (bindings != null)
            {
                height += EditorGUI.GetPropertyHeight(bindings, true) + Gap;
            }

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
            DrawActionIdField(left, id);
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
                DrawTriggerFeatures(row, triggerFeatures);

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

        private static void DrawActionIdField(Rect rect, SerializedProperty id)
        {
            Rect fieldRect = new Rect(rect.x, rect.y, Mathf.Max(40f, rect.width - 44f), rect.height);
            Rect jumpRect = new Rect(fieldRect.xMax + Gap, rect.y, 40f, rect.height);

            EditorGUI.PropertyField(fieldRect, id, new GUIContent("动作"));
            using (new EditorGUI.DisabledScope(id == null || id.enumValueIndex < 0 || id.enumValueIndex >= id.enumNames.Length))
            {
                if (GUI.Button(jumpRect, "定义", EditorStyles.miniButton))
                {
                    JumpToActionIdDefine(id);
                }
            }
        }

        private static void JumpToActionIdDefine(SerializedProperty id)
        {
            if (id == null || id.enumValueIndex < 0 || id.enumValueIndex >= id.enumNames.Length)
            {
                return;
            }

            string enumName = id.enumNames[id.enumValueIndex];
            int line = FindEnumMemberLine(ActionIdFilePath, enumName);
            ESDesignUtility.SafeEditor.OpenCodeAtLine(ActionIdFilePath, line);
        }

        private static int FindEnumMemberLine(string assetPath, string enumName)
        {
            if (string.IsNullOrEmpty(enumName))
            {
                return 1;
            }

            string fullPath = ToFullProjectPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return 1;
            }

            string[] lines = File.ReadAllLines(fullPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith(enumName, StringComparison.Ordinal) &&
                    (line.Length == enumName.Length || !IsIdentifierPart(line[enumName.Length])))
                {
                    return i + 1;
                }
            }

            return 1;
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string ToFullProjectPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static bool IsButton(SerializedProperty valueType)
        {
            return valueType != null && valueType.enumValueIndex == (int)ESInputValueType.Button;
        }

        private static bool HasTriggerFeature(SerializedProperty triggerFeatures, ESInputTriggerFeature feature)
        {
            return triggerFeatures != null && (triggerFeatures.intValue & (int)feature) != 0;
        }

        private static void DrawTriggerFeatures(Rect row, SerializedProperty triggerFeatures)
        {
            Rect labelRect = new Rect(row.x, row.y, 56f, row.height);
            Rect content = new Rect(row.x + 58f, row.y, row.width - 58f, row.height);
            EditorGUI.LabelField(labelRect, "触发");

            ESInputTriggerFeature current = triggerFeatures == null
                ? ESInputTriggerFeature.None
                : (ESInputTriggerFeature)triggerFeatures.intValue;

            const int count = 5;
            float itemWidth = Mathf.Max(52f, content.width / count);
            DrawTriggerToggle(content, 0, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Pressed, "点击");
            DrawTriggerToggle(content, 1, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Released, "松开");
            DrawTriggerToggle(content, 2, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Held, "按住");
            DrawTriggerToggle(content, 3, itemWidth, current, triggerFeatures, ESInputTriggerFeature.LongPress, "长按");
            DrawTriggerToggle(content, 4, itemWidth, current, triggerFeatures, ESInputTriggerFeature.DoublePress, "双击");
        }

        private static void DrawTriggerToggle(
            Rect content,
            int index,
            float itemWidth,
            ESInputTriggerFeature current,
            SerializedProperty triggerFeatures,
            ESInputTriggerFeature feature,
            string text)
        {
            Rect rect = new Rect(content.x + itemWidth * index, content.y, itemWidth - 2f, content.height);
            bool next = GUI.Toggle(rect, (current & feature) != 0, text, EditorStyles.miniButton);
            if (triggerFeatures == null)
            {
                return;
            }

            if (next != ((current & feature) != 0))
            {
                int value = triggerFeatures.intValue;
                if (next)
                {
                    value |= (int)feature;
                }
                else
                {
                    value &= ~(int)feature;
                }

                triggerFeatures.intValue = value;
            }
        }
    }
}
