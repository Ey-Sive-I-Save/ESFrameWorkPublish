using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// The single authoring surface for a stable GameTag reference. It writes the canonical
    /// Enum/String pair from the baked catalog and delegates search, grouping and selection to
    /// ES's standard AdvancedDropdown wrapper.
    /// </summary>
    [CustomPropertyDrawer(typeof(ESTagStableReference))]
    public sealed class ESTagStableReferenceDrawer : PropertyDrawer
    {
        private const string EmptyLabel = "选择 GameTag";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);
            ESTagStableReference reference = ReadReference(property);
            bool isKnown = ESTagEditorCatalogCache.TryGetPickerEntry(reference, out ESTagEditorCatalogCache.PickerEntry current);
            string display = reference.IsEmpty ? EmptyLabel : isKnown ? current.FullDisplayName : "缺失 Tag · " + reference;
            GUIContent buttonContent = new GUIContent(display, isKnown || reference.IsEmpty
                ? "从正式 GameTag Catalog 选择稳定 Tag。"
                : "此引用未在当前正式 GameTag Catalog 中找到；请重新选择或执行 Bake。\n" + reference);

            Color previousColor = GUI.color;
            if (!reference.IsEmpty && !isKnown)
                GUI.color = new Color(1f, 0.55f, 0.55f);
            if (EditorGUI.DropdownButton(content, buttonContent, FocusType.Keyboard))
                OpenPicker(content, property, isKnown ? current.Reference : default);
            GUI.color = previousColor;
            EditorGUI.EndProperty();
        }

        private static void OpenPicker(Rect anchorRect, SerializedProperty property, ESTagStableReference selected)
        {
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;
            IReadOnlyList<ESTagEditorCatalogCache.PickerEntry> source = ESTagEditorCatalogCache.GetPickerEntries();
            var entries = new List<ESSearchDropdown.Entry>(source.Count + 1)
            {
                ESSearchDropdown.Entry.Item("清空", () => WriteReference(serializedObject, propertyPath, default), "操作")
            };

            for (int i = 0; i < source.Count; i++)
            {
                ESTagEditorCatalogCache.PickerEntry item = source[i];
                ESTagStableReference captured = item.Reference;
                entries.Add(ESSearchDropdown.Entry.Item(
                    item.DisplayName,
                    () => WriteReference(serializedObject, propertyPath, captured),
                    item.GroupPath,
                    subtitle: item.StringKey,
                    tooltip: item.FullDisplayName,
                    keywords: item.StringKey,
                    badge: item.StorageBadge,
                    selected: captured.Equals(selected)));
            }

            if (source.Count == 0)
                entries.Add(ESSearchDropdown.Entry.Disabled("未找到唯一有效的 ESTagBakeTable", "状态",
                    "请执行【ES】/项目设置/GameCore/Bake并应用GameTag Catalog。"));

            ESSearchDropdown.Open(anchorRect, "选择 GameTag", entries, minimumWindowSize: new Vector2(500f, 360f));
        }

        private static ESTagStableReference ReadReference(SerializedProperty property)
        {
            SerializedProperty enumGroup = property.FindPropertyRelative(nameof(ESTagStableReference.enumGroup));
            SerializedProperty enumValue = property.FindPropertyRelative(nameof(ESTagStableReference.enumValue));
            SerializedProperty stringKey = property.FindPropertyRelative(nameof(ESTagStableReference.stringKey));
            return new ESTagStableReference
            {
                enumGroup = enumGroup != null ? (ESTagEnumGroup)enumGroup.enumValueIndex : default,
                enumValue = enumValue != null ? (ushort)enumValue.intValue : ESTagId.InvalidValue,
                stringKey = stringKey?.stringValue
            };
        }

        private static void WriteReference(SerializedObject serializedObject, string propertyPath, ESTagStableReference reference)
        {
            if (serializedObject == null)
                return;

            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                return;

            SerializedProperty enumGroup = property.FindPropertyRelative(nameof(ESTagStableReference.enumGroup));
            SerializedProperty enumValue = property.FindPropertyRelative(nameof(ESTagStableReference.enumValue));
            SerializedProperty stringKey = property.FindPropertyRelative(nameof(ESTagStableReference.stringKey));
            if (enumGroup == null || enumValue == null || stringKey == null)
                return;

            enumGroup.enumValueIndex = (int)reference.enumGroup;
            enumValue.intValue = reference.enumValue;
            stringKey.stringValue = reference.stringKey ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
