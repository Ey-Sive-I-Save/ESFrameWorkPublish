using System;
using System.Collections.Generic;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomPropertyDrawer(typeof(ESEnumStringTableAttribute))]
    public sealed class ESEnumStringTableAttributeDrawer : PropertyDrawer
    {
        private const float CellGap = 4f;
        private const float ActionWidth = 22f;
        private const float ToggleWidth = 18f;
        private const float MinWideWidth = 510f;

        private static GUIStyle headerStyle;
        private static GUIStyle statusStyle;
        private static GUIStyle centeredMiniStyle;
        private static bool stylesProSkin;
        private string searchText = string.Empty;
        private string searchPropertyKey;
        private readonly HashSet<long> enumKeys = new HashSet<long>();
        private readonly HashSet<long> duplicateEnumKeys = new HashSet<long>();
        private readonly HashSet<string> stringKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> duplicateStringKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<EntryValidation> validation = new List<EntryValidation>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line + 6f;
            if (property.serializedObject.isEditingMultipleObjects)
                return line * 3f + EditorGUIUtility.standardVerticalSpacing + 6f;

            SerializedProperty entries = property.FindPropertyRelative("entries");
            SerializedProperty denseLimit = property.FindPropertyRelative("denseEnumLimit");
            SerializedProperty denseRatio = property.FindPropertyRelative("denseEnumRatio");
            ESEnumStringTableAttribute settings = GetSettings();
            int visibleCount = CountVisibleEntries(entries, GetSearch(property, settings));
            bool wide = EditorGUIUtility.currentViewWidth >= MinWideWidth;

            float height = line;
            if (settings.Searchable)
                height += line + EditorGUIUtility.standardVerticalSpacing;
            height += line + EditorGUIUtility.standardVerticalSpacing;
            if (entries != null)
            {
                string search = GetSearch(property, settings);
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (!MatchesSearch(entry, search))
                        continue;
                    SerializedProperty value = entry.FindPropertyRelative("value");
                    float valueHeight = value != null
                        ? EditorGUI.GetPropertyHeight(value, GUIContent.none, true)
                        : line;
                    height += (wide
                                  ? Mathf.Max(line, valueHeight)
                                  : line + EditorGUIUtility.standardVerticalSpacing + valueHeight)
                              + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            if (visibleCount == 0)
                height += line + EditorGUIUtility.standardVerticalSpacing;
            height += line + EditorGUIUtility.standardVerticalSpacing;
            if (settings.ShowAdvancedSettings && denseLimit != null && denseRatio != null)
                height += (line + EditorGUIUtility.standardVerticalSpacing) * 2f;

            return height + 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureStyles();
            ESEnumStringTableAttribute settings = GetSettings();
            SerializedProperty entries = property.FindPropertyRelative("entries");
            if (entries == null || !entries.isArray)
            {
                EditorGUI.HelpBox(position, "ESEnumStringTable 需要名为 entries 的 Unity 序列化数组。", MessageType.Error);
                return;
            }

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                Rect content = EditorGUI.IndentedRect(position);
                content.y += 3f;
                content.height -= 6f;
                float line = EditorGUIUtility.singleLineHeight;
                string search = GetSearch(property, settings);

                Rect titleRect = NextLine(ref content, line);
                DrawTitle(titleRect, property, label, entries.arraySize);

                if (!property.isExpanded)
                    return;

                if (property.serializedObject.isEditingMultipleObjects)
                {
                    Rect multiRect = new Rect(content.x, content.y, content.width, line * 2f);
                    EditorGUI.HelpBox(multiRect, "Enum/String 表暂不支持安全批量编辑，请单独选择一个对象。", MessageType.Info);
                    return;
                }

                if (settings.Searchable)
                {
                    Rect searchRect = NextLine(ref content, line);
                    string nextSearch = EditorGUI.TextField(searchRect, search, EditorStyles.toolbarSearchField);
                    if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
                    {
                        search = nextSearch;
                        SetSearch(property, search);
                    }
                }

                Rect headerRect = NextLine(ref content, line);
                bool wide = position.width >= MinWideWidth;
                DrawColumnHeader(headerRect, settings, wide);
                RebuildValidation(entries);

                int visibleCount = 0;
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (!MatchesSearch(entry, search))
                        continue;

                    visibleCount++;
                    SerializedProperty value = entry.FindPropertyRelative("value");
                    float valueHeight = value != null
                        ? EditorGUI.GetPropertyHeight(value, GUIContent.none, true)
                        : line;
                    float rowHeight = wide
                        ? Mathf.Max(line, valueHeight)
                        : line + EditorGUIUtility.standardVerticalSpacing + valueHeight;
                    Rect rowRect = NextLine(ref content, rowHeight);
                    DrawEntryRow(rowRect, entries, entry, index, settings, wide, validation[index]);
                }

                if (visibleCount == 0)
                {
                    Rect emptyRect = NextLine(ref content, line);
                    EditorGUI.LabelField(
                        emptyRect,
                        entries.arraySize == 0 ? "暂无条目" : "没有匹配的条目",
                        centeredMiniStyle);
                }

                Rect footerRect = NextLine(ref content, line);
                DrawFooter(footerRect, property, entries, settings, CountInvalidEntries());

                if (settings.ShowAdvancedSettings)
                    DrawAdvanced(ref content, property, line);
            }
        }

        private static void DrawTitle(Rect rect, SerializedProperty property, GUIContent label, int count)
        {
            Rect foldoutRect = rect;
            foldoutRect.width -= 86f;
            property.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                property.isExpanded,
                label,
                true,
                headerStyle);

            Rect badgeRect = rect;
            badgeRect.xMin = badgeRect.xMax - 82f;
            EditorGUI.LabelField(badgeRect, count + " 条  ·  Enum/String", statusStyle);
        }

        private static void DrawColumnHeader(
            Rect rect,
            ESEnumStringTableAttribute settings,
            bool wide)
        {
            if (!wide)
            {
                EditorGUI.LabelField(rect, settings.EnumColumn + " / " + settings.StringColumn + " / " + settings.ValueColumn, EditorStyles.miniBoldLabel);
                return;
            }

            GetWideColumns(rect, settings.AllowReorder, out Rect enumRect, out Rect stringRect, out Rect valueRect, out _);
            EditorGUI.LabelField(enumRect, settings.EnumColumn, EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(stringRect, settings.StringColumn, EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(valueRect, settings.ValueColumn, EditorStyles.miniBoldLabel);
        }

        private static void DrawEntryRow(
            Rect rect,
            SerializedProperty entries,
            SerializedProperty entry,
            int index,
            ESEnumStringTableAttribute settings,
            bool wide,
            EntryValidation entryValidation)
        {
            SerializedProperty hasEnum = entry.FindPropertyRelative("hasEnumKey");
            SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
            SerializedProperty value = entry.FindPropertyRelative("value");
            if (hasEnum == null || enumKey == null || stringKey == null || value == null)
            {
                EditorGUI.HelpBox(rect, "条目序列化结构不兼容。", MessageType.Error);
                return;
            }

            bool invalid = entryValidation.IsInvalid;

            if (Event.current.type == EventType.Repaint)
            {
                Color background = invalid
                    ? new Color(0.52f, 0.16f, 0.14f, EditorGUIUtility.isProSkin ? 0.34f : 0.18f)
                    : new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.10f : 0.04f);
                EditorGUI.DrawRect(rect, background);
            }

            Rect actionsRect;
            if (wide)
            {
                GetWideColumns(rect, settings.AllowReorder, out Rect enumRect, out Rect stringRect, out Rect valueRect, out actionsRect);
                DrawEnumCell(enumRect, hasEnum, enumKey);
                EditorGUI.PropertyField(stringRect, stringKey, GUIContent.none, false);
                EditorGUI.PropertyField(valueRect, value, GUIContent.none, true);
            }
            else
            {
                float line = EditorGUIUtility.singleLineHeight;
                Rect first = new Rect(rect.x, rect.y, rect.width, line);
                actionsRect = first;
                actionsRect.xMin = actionsRect.xMax - GetActionsWidth(settings.AllowReorder);
                first.xMax = actionsRect.xMin - CellGap;
                float enumWidth = Mathf.Max(110f, first.width * 0.46f);
                Rect enumRect = new Rect(first.x, first.y, enumWidth, line);
                Rect stringRect = new Rect(enumRect.xMax + CellGap, first.y, first.xMax - enumRect.xMax - CellGap, line);
                DrawEnumCell(enumRect, hasEnum, enumKey);
                EditorGUI.PropertyField(stringRect, stringKey, GUIContent.none, false);

                Rect valueRect = new Rect(
                    rect.x,
                    rect.y + line + EditorGUIUtility.standardVerticalSpacing,
                    rect.width,
                    rect.height - line - EditorGUIUtility.standardVerticalSpacing);
                EditorGUI.PropertyField(valueRect, value, GUIContent.none, true);
            }

            DrawActions(actionsRect, entries, index, settings.AllowReorder);

            if (invalid && Event.current.type == EventType.Repaint)
            {
                string tooltip = BuildProblemTooltip(entryValidation);
                EditorGUI.LabelField(rect, new GUIContent(string.Empty, tooltip));
            }
        }

        private static void DrawEnumCell(Rect rect, SerializedProperty hasEnum, SerializedProperty enumKey)
        {
            Rect toggleRect = rect;
            toggleRect.width = ToggleWidth;
            hasEnum.boolValue = EditorGUI.Toggle(toggleRect, hasEnum.boolValue);

            Rect fieldRect = rect;
            fieldRect.xMin = toggleRect.xMax + 1f;
            using (new EditorGUI.DisabledScope(!hasEnum.boolValue))
                EditorGUI.PropertyField(fieldRect, enumKey, GUIContent.none, false);
        }

        private static void DrawActions(
            Rect rect,
            SerializedProperty entries,
            int index,
            bool allowReorder)
        {
            float x = rect.x;
            if (allowReorder)
            {
                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "↑", EditorStyles.miniButtonLeft))
                        MoveEntry(entries, index, index - 1);
                }
                x += ActionWidth;
                using (new EditorGUI.DisabledScope(index >= entries.arraySize - 1))
                {
                    if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "↓", EditorStyles.miniButtonMid))
                        MoveEntry(entries, index, index + 1);
                }
                x += ActionWidth;
            }
            GUIStyle removeStyle = allowReorder ? EditorStyles.miniButtonRight : EditorStyles.miniButton;
            if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "×", removeStyle))
                DeleteEntry(entries, index);
        }

        private static void DrawFooter(
            Rect rect,
            SerializedProperty property,
            SerializedProperty entries,
            ESEnumStringTableAttribute settings,
            int invalidCount)
        {
            Rect statusRect = rect;
            statusRect.width -= 128f;
            EditorGUI.LabelField(
                statusRect,
                invalidCount == 0 ? "映射结构有效" : invalidCount + " 个条目需要修复",
                invalidCount == 0 ? statusStyle : EditorStyles.miniBoldLabel);

            Rect addRect = rect;
            addRect.xMin = addRect.xMax - 124f;
            if (GUI.Button(addRect, "添加条目", EditorStyles.miniButton))
            {
                AddEntry(entries, settings.NewEntryMode);
                property.isExpanded = true;
            }
        }

        private static void DrawAdvanced(ref Rect content, SerializedProperty property, float line)
        {
            SerializedProperty denseLimit = property.FindPropertyRelative("denseEnumLimit");
            SerializedProperty denseRatio = property.FindPropertyRelative("denseEnumRatio");
            if (denseLimit == null || denseRatio == null)
                return;

            EditorGUI.PropertyField(NextLine(ref content, line), denseLimit, new GUIContent("Dense Enum Limit"));
            EditorGUI.PropertyField(NextLine(ref content, line), denseRatio, new GUIContent("Dense Enum Ratio"));
        }

        private static void AddEntry(SerializedProperty entries, ESEnumStringTableNewEntryMode mode)
        {
            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            SerializedProperty hasEnum = entry.FindPropertyRelative("hasEnumKey");
            SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
            SerializedProperty value = entry.FindPropertyRelative("value");

            bool wantsEnum = mode != ESEnumStringTableNewEntryMode.StringOnly;
            bool hasAvailableEnum = wantsEnum && TryAssignFirstUnusedEnum(entries, enumKey, index);
            hasEnum.boolValue = hasAvailableEnum;
            bool needsString = mode != ESEnumStringTableNewEntryMode.EnumOnly || !hasAvailableEnum;
            stringKey.stringValue = needsString ? CreateUniqueStringKey(entries, index) : string.Empty;
            ClearProperty(value);
            entries.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private static void DeleteEntry(SerializedProperty entries, int index)
        {
            entries.DeleteArrayElementAtIndex(index);
            entries.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private static void MoveEntry(SerializedProperty entries, int from, int to)
        {
            entries.MoveArrayElement(from, to);
            entries.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private static void ClearProperty(SerializedProperty property)
        {
            if (property == null)
                return;

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = default;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = default;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = default;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = default;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = default;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = default;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = Quaternion.identity;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = new AnimationCurve();
                    break;
                case SerializedPropertyType.ManagedReference:
                    property.managedReferenceValue = null;
                    break;
                case SerializedPropertyType.Generic:
                    if (property.isArray && property.propertyType != SerializedPropertyType.String)
                    {
                        property.arraySize = 0;
                        break;
                    }
                    SerializedProperty iterator = property.Copy();
                    SerializedProperty end = iterator.GetEndProperty();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        enterChildren = false;
                        ClearProperty(iterator);
                    }
                    break;
            }
        }

        private static string CreateUniqueStringKey(SerializedProperty entries, int ignoredIndex)
        {
            const string baseKey = "new.key";
            string candidate = baseKey;
            int suffix = 2;
            while (HasString(entries, candidate, ignoredIndex))
                candidate = baseKey + "." + suffix++;
            return candidate;
        }

        private static bool TryAssignFirstUnusedEnum(
            SerializedProperty entries,
            SerializedProperty enumKey,
            int ignoredIndex)
        {
            if (enumKey == null || enumKey.propertyType != SerializedPropertyType.Enum)
                return false;

            int optionCount = enumKey.enumNames.Length;
            for (int option = 0; option < optionCount; option++)
            {
                bool used = false;
                for (int index = 0; index < entries.arraySize; index++)
                {
                    if (index == ignoredIndex)
                        continue;
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    SerializedProperty otherHasEnum = entry.FindPropertyRelative("hasEnumKey");
                    SerializedProperty otherEnum = entry.FindPropertyRelative("enumKey");
                    if (otherHasEnum.boolValue && otherEnum.enumValueIndex == option)
                    {
                        used = true;
                        break;
                    }
                }

                if (used)
                    continue;
                enumKey.enumValueIndex = option;
                return true;
            }

            return false;
        }

        private static bool MatchesSearch(SerializedProperty entry, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            string needle = search.Trim();
            SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
            SerializedProperty value = entry.FindPropertyRelative("value");
            return Contains(enumKey?.displayName, needle)
                   || Contains(GetEnumDisplay(enumKey), needle)
                   || Contains(stringKey?.stringValue, needle)
                   || Contains(GetValueDisplay(value), needle);
        }

        private static bool Contains(string value, string needle)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetEnumDisplay(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                return null;
            int index = property.enumValueIndex;
            return index >= 0 && index < property.enumDisplayNames.Length
                ? property.enumDisplayNames[index]
                : property.enumValueIndex.ToString();
        }

        private static string GetValueDisplay(SerializedProperty property)
        {
            if (property == null)
                return null;
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return property.objectReferenceValue != null ? property.objectReferenceValue.name : null;
            return property.propertyType == SerializedPropertyType.String ? property.stringValue : property.displayName;
        }

        private static int CountVisibleEntries(SerializedProperty entries, string search)
        {
            if (entries == null || !entries.isArray)
                return 0;
            int count = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                if (MatchesSearch(entries.GetArrayElementAtIndex(index), search))
                    count++;
            }
            return count;
        }

        private void RebuildValidation(SerializedProperty entries)
        {
            enumKeys.Clear();
            duplicateEnumKeys.Clear();
            stringKeys.Clear();
            duplicateStringKeys.Clear();
            validation.Clear();

            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty hasEnum = entry.FindPropertyRelative("hasEnumKey");
                SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
                SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
                if (hasEnum.boolValue && !enumKeys.Add(enumKey.longValue))
                    duplicateEnumKeys.Add(enumKey.longValue);
                if (!string.IsNullOrEmpty(stringKey.stringValue) && !stringKeys.Add(stringKey.stringValue))
                    duplicateStringKeys.Add(stringKey.stringValue);
            }

            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty hasEnum = entry.FindPropertyRelative("hasEnumKey");
                SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
                SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
                SerializedProperty value = entry.FindPropertyRelative("value");
                bool hasString = !string.IsNullOrEmpty(stringKey.stringValue);
                validation.Add(new EntryValidation(
                    !hasEnum.boolValue && !hasString,
                    hasString && !string.Equals(stringKey.stringValue, stringKey.stringValue.Trim(), StringComparison.Ordinal),
                    IsNullObjectReference(value),
                    hasEnum.boolValue && duplicateEnumKeys.Contains(enumKey.longValue),
                    hasString && duplicateStringKeys.Contains(stringKey.stringValue)));
            }
        }

        private int CountInvalidEntries()
        {
            int count = 0;
            for (int index = 0; index < validation.Count; index++)
            {
                if (validation[index].IsInvalid)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasString(SerializedProperty entries, string key, int ignoredIndex)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            for (int index = 0; index < entries.arraySize; index++)
            {
                if (index == ignoredIndex)
                    continue;
                SerializedProperty other = entries.GetArrayElementAtIndex(index).FindPropertyRelative("stringKey");
                if (string.Equals(other.stringValue, key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsNullObjectReference(SerializedProperty value)
        {
            return value.propertyType == SerializedPropertyType.ObjectReference
                   && value.objectReferenceValue == null;
        }

        private static string BuildProblemTooltip(EntryValidation entryValidation)
        {
            var problems = new List<string>(5);
            if (entryValidation.MissingAlias) problems.Add("至少需要一个 Key");
            if (entryValidation.InvalidString) problems.Add("String Key 前后不能有空白");
            if (entryValidation.NullValue) problems.Add("Value 不能为空");
            if (entryValidation.DuplicateEnum) problems.Add("Enum Key 重复");
            if (entryValidation.DuplicateString) problems.Add("String Key 重复");
            return string.Join("；", problems);
        }

        private static void GetWideColumns(
            Rect rect,
            bool allowReorder,
            out Rect enumRect,
            out Rect stringRect,
            out Rect valueRect,
            out Rect actionsRect)
        {
            float actionsWidth = GetActionsWidth(allowReorder);
            float usable = rect.width - actionsWidth - CellGap * 3f;
            float enumWidth = usable * 0.27f;
            float stringWidth = usable * 0.31f;
            enumRect = new Rect(rect.x, rect.y, enumWidth, EditorGUIUtility.singleLineHeight);
            stringRect = new Rect(enumRect.xMax + CellGap, rect.y, stringWidth, EditorGUIUtility.singleLineHeight);
            valueRect = new Rect(stringRect.xMax + CellGap, rect.y, usable - enumWidth - stringWidth, EditorGUIUtility.singleLineHeight);
            actionsRect = new Rect(valueRect.xMax + CellGap, rect.y, actionsWidth, EditorGUIUtility.singleLineHeight);
        }

        private static float GetActionsWidth(bool allowReorder)
        {
            return allowReorder ? ActionWidth * 3f : ActionWidth;
        }

        private static Rect NextLine(ref Rect content, float height)
        {
            Rect line = new Rect(content.x, content.y, content.width, height);
            content.y += height + EditorGUIUtility.standardVerticalSpacing;
            return line;
        }

        private ESEnumStringTableAttribute GetSettings()
        {
            return attribute as ESEnumStringTableAttribute ?? new ESEnumStringTableAttribute();
        }

        private string GetSearch(SerializedProperty property, ESEnumStringTableAttribute settings)
        {
            if (!settings.Searchable)
                return string.Empty;
            string key = BuildSearchKey(property);
            if (!string.Equals(searchPropertyKey, key, StringComparison.Ordinal))
            {
                searchPropertyKey = key;
                searchText = string.Empty;
            }
            return searchText;
        }

        private void SetSearch(SerializedProperty property, string search)
        {
            searchPropertyKey = BuildSearchKey(property);
            searchText = search ?? string.Empty;
        }

        private static string BuildSearchKey(SerializedProperty property)
        {
            return property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;
        }

        private static void EnsureStyles()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (headerStyle != null && stylesProSkin == proSkin)
                return;
            stylesProSkin = proSkin;
            headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            headerStyle.normal.textColor = ESEditorPresentation.SectionSelectedTextColor;
            statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };
            centeredMiniStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private readonly struct EntryValidation
        {
            public readonly bool MissingAlias;
            public readonly bool InvalidString;
            public readonly bool NullValue;
            public readonly bool DuplicateEnum;
            public readonly bool DuplicateString;

            public bool IsInvalid => MissingAlias
                                     || InvalidString
                                     || NullValue
                                     || DuplicateEnum
                                     || DuplicateString;

            public EntryValidation(
                bool missingAlias,
                bool invalidString,
                bool nullValue,
                bool duplicateEnum,
                bool duplicateString)
            {
                MissingAlias = missingAlias;
                InvalidString = invalidString;
                NullValue = nullValue;
                DuplicateEnum = duplicateEnum;
                DuplicateString = duplicateString;
            }
        }
    }
}
