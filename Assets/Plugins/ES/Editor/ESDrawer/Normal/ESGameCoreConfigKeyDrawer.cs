using System;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public abstract class ESGameCoreConfigKeyDrawerBase : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Line * 4f + Gap * 5f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            Type enumType = ResolveEnumType();

            EditorGUI.LabelField(NextLine(ref position), label, EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.46f, out Rect left, out Rect right);
            EditorGUI.PropertyField(left, enumKey, new GUIContent("Enum Key"));
            EditorGUI.PropertyField(right, stringKey, new GUIContent("String Key"));

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            if (GUI.Button(left, "Fill String From Asset", EditorStyles.miniButtonLeft))
            {
                stringKey.stringValue = ResolveSuggestedStringKey(property);
                property.serializedObject.ApplyModifiedProperties();
                MarkDirty(property);
            }

            if (GUI.Button(right, "Copy Key", EditorStyles.miniButtonRight))
                CopyKey(enumKey, stringKey, enumType);

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            if (GUI.Button(left, "Open Enum Script", EditorStyles.miniButtonLeft))
                OpenEnumScript(enumType);

            if (GUI.Button(right, "Copy AI Enum Request", EditorStyles.miniButtonRight))
                CopyAiEnumRequest(enumType, enumKey, stringKey, ResolveSuggestedStringKey(property));

            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();

        private static Rect NextLine(ref Rect position)
        {
            Rect rect = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return rect;
        }

        private static void DrawSplit(Rect rect, float leftRatio, out Rect left, out Rect right)
        {
            float width = Mathf.Floor((rect.width - Gap) * leftRatio);
            left = new Rect(rect.x, rect.y, width, rect.height);
            right = new Rect(left.xMax + Gap, rect.y, rect.width - width - Gap, rect.height);
        }

        private static string ResolveSuggestedStringKey(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target is SoDataInfo info && !string.IsNullOrEmpty(info.KeyName))
                return info.KeyName;

            return target != null ? target.name : string.Empty;
        }

        private static void CopyKey(SerializedProperty enumKey, SerializedProperty stringKey, Type enumType)
        {
            string enumName = enumType != null && enumKey != null
                ? enumType.Name + "." + enumKey.enumDisplayNames[enumKey.enumValueIndex]
                : "UnknownEnum";

            EditorGUIUtility.systemCopyBuffer =
                "enumKey: " + enumName + Environment.NewLine +
                "stringKey: " + (stringKey != null ? stringKey.stringValue : string.Empty);
        }

        private static void OpenEnumScript(Type enumType)
        {
            ESEnumScriptJump.OpenEnum(enumType, true);
        }

        private static void CopyAiEnumRequest(Type enumType, SerializedProperty enumKey, SerializedProperty stringKey, string fallbackStringKey)
        {
            string desiredStringKey = stringKey != null && !string.IsNullOrEmpty(stringKey.stringValue)
                ? stringKey.stringValue
                : fallbackStringKey;
            string current = enumKey != null ? enumKey.enumDisplayNames[enumKey.enumValueIndex] : "Unknown";
            ESEnumScriptJump.CopyAppendRequest(enumType, desiredStringKey, current);
        }

        private static void MarkDirty(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target != null)
                EditorUtility.SetDirty(target);
        }
    }

    [CustomPropertyDrawer(typeof(ESBuffConfigKey))]
    public sealed class ESBuffConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESBuffEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESShotConfigKey))]
    public sealed class ESShotConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESShotEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESWeaponConfigKey))]
    public sealed class ESWeaponConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESWeaponEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESMonsterConfigKey))]
    public sealed class ESMonsterConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESMonsterEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESNpcConfigKey))]
    public sealed class ESNpcConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESNpcEnumKey);
    }

    [CustomPropertyDrawer(typeof(ESSkillConfigKey))]
    public sealed class ESSkillConfigKeyDrawer : ESGameCoreConfigKeyDrawerBase
    {
        protected override Type ResolveEnumType() => typeof(ESSkillEnumKey);
    }
}
