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
        private const float RowGap = 2f;
        private const float CellGap = 4f;
        private const float ActionWidth = 22f;
        private const float ToggleWidth = 18f;
        private const float MinWideWidth = 510f;

        private static readonly Dictionary<string, string> SearchByProperty =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static GUIStyle headerStyle;
        private static GUIStyle statusStyle;
        private static GUIStyle centeredMiniStyle;
        private static bool stylesProSkin;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line + 6f;

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
                    DrawEntryRow(rowRect, entries, entry, index, settings, wide);
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
                DrawFooter(footerRect, property, entries, settings);

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

            GetWideColumns(rect, out Rect enumRect, out Rect stringRect, out Rect valueRect, out _);
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
            bool wide)
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

            bool hasString = !string.IsNullOrEmpty(stringKey.stringValue);
            bool missingAlias = !hasEnum.boolValue && !hasString;
            bool invalidString = hasString
                                 && !string.Equals(stringKey.stringValue, stringKey.stringValue.Trim(), StringComparison.Ordinal);
            bool nullValue = IsNullObjectReference(value);
            bool duplicateEnum = hasEnum.boolValue && HasDuplicateEnum(entries, enumKey, index);
            bool duplicateString = hasString && HasDuplicateString(entries, stringKey.stringValue, index);
            bool invalid = missingAlias || invalidString || nullValue || duplicateEnum || duplicateString;

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
                GetWideColumns(rect, out Rect enumRect, out Rect stringRect, out Rect valueRect, out actionsRect);
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

                Rect valueRect = new Rect(rect.x, rect.y + line + EditorGUIUtility.standardVerticalSpacing, rect.width, line);
                EditorGUI.PropertyField(valueRect, value, GUIContent.none, true);
            }

            DrawActions(actionsRect, entries, index, settings.AllowReorder);

            if (invalid && Event.current.type == EventType.Repaint)
            {
                string tooltip = BuildProblemTooltip(missingAlias, invalidString, nullValue, duplicateEnum, duplicateString);
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
            using (new EditorGUI.DisabledScope(!allowReorder || index <= 0))
            {
                if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "↑", EditorStyles.miniButtonLeft))
                    MoveEntry(entries, index, index - 1);
            }
            x += ActionWidth;
            using (new EditorGUI.DisabledScope(!allowReorder || index >= entries.arraySize - 1))
            {
                if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "↓", EditorStyles.miniButtonMid))
                    MoveEntry(entries, index, index + 1);
            }
            x += ActionWidth;
            if (GUI.Button(new Rect(x, rect.y, ActionWidth, EditorGUIUtility.singleLineHeight), "×", EditorStyles.miniButtonRight))
                DeleteEntry(entries, index);
        }

        private static void DrawFooter(
            Rect rect,
            SerializedProperty property,
            SerializedProperty entries,
            ESEnumStringTableAttribute settings)
        {
            Rect statusRect = rect;
            statusRect.width -= 128f;
            int invalidCount = CountInvalidEntries(entries);
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

            hasEnum.boolValue = mode != ESEnumStringTableNewEntryMode.StringOnly;
            if (enumKey.propertyType == SerializedPropertyType.Enum)
                enumKey.enumValueIndex = 0;
            stringKey.stringValue = mode == ESEnumStringTableNewEntryMode.EnumOnly ? string.Empty : CreateUniqueStringKey(entries, index);
            ResetValue(value);
            entries.serializedObject.ApplyModifiedProperties();
        }

        private static void DeleteEntry(SerializedProperty entries, int index)
        {
            entries.DeleteArrayElementAtIndex(index);
            entries.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
        }

        private static void MoveEntry(SerializedProperty entries, int from, int to)
        {
            entries.MoveArrayElement(from, to);
            entries.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
        }

        private static void ResetValue(SerializedProperty value)
        {
            switch (value.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    value.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.String:
                    value.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Boolean:
                    value.boolValue = false;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    value.intValue = 0;
                    break;
                case SerializedPropertyType.Float:
                    value.floatValue = 0f;
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

        private static int CountInvalidEntries(SerializedProperty entries)
        {
            int count = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty hasEnum = entry.FindPropertyRelative("hasEnumKey");
                SerializedProperty enumKey = entry.FindPropertyRelative("enumKey");
                SerializedProperty stringKey = entry.FindPropertyRelative("stringKey");
                SerializedProperty value = entry.FindPropertyRelative("value");
                bool hasString = !string.IsNullOrEmpty(stringKey.stringValue);
                if ((!hasEnum.boolValue && !hasString)
                    || (hasString && stringKey.stringValue != stringKey.stringValue.Trim())
                    || IsNullObjectReference(value)
                    || (hasEnum.boolValue && HasDuplicateEnum(entries, enumKey, index))
                    || (hasString && HasDuplicateString(entries, stringKey.stringValue, index)))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasDuplicateEnum(SerializedProperty entries, SerializedProperty enumKey, int ignoredIndex)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                if (index == ignoredIndex)
                    continue;
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty otherHasEnum = entry.FindPropertyRelative("hasEnumKey");
                SerializedProperty otherEnum = entry.FindPropertyRelative("enumKey");
                if (otherHasEnum.boolValue && otherEnum.intValue == enumKey.intValue)
                    return true;
            }
            return false;
        }

        private static bool HasDuplicateString(SerializedProperty entries, string key, int ignoredIndex)
        {
            return HasString(entries, key, ignoredIndex);
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

        private static string BuildProblemTooltip(
            bool missingAlias,
            bool invalidString,
            bool nullValue,
            bool duplicateEnum,
            bool duplicateString)
        {
            var problems = new List<string>(5);
            if (missingAlias) problems.Add("至少需要一个 Key");
            if (invalidString) problems.Add("String Key 前后不能有空白");
            if (nullValue) problems.Add("Value 不能为空");
            if (duplicateEnum) problems.Add("Enum Key 重复");
            if (duplicateString) problems.Add("String Key 重复");
            return string.Join("；", problems);
        }

        private static void GetWideColumns(
            Rect rect,
            out Rect enumRect,
            out Rect stringRect,
            out Rect valueRect,
            out Rect actionsRect)
        {
            float actionsWidth = GetActionsWidth(true);
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

        private static string GetSearch(SerializedProperty property, ESEnumStringTableAttribute settings)
        {
            if (!settings.Searchable)
                return string.Empty;
            string key = BuildSearchKey(property);
            return SearchByProperty.TryGetValue(key, out string search) ? search : string.Empty;
        }

        private static void SetSearch(SerializedProperty property, string search)
        {
            string key = BuildSearchKey(property);
            if (string.IsNullOrEmpty(search))
                SearchByProperty.Remove(key);
            else
                SearchByProperty[key] = search;
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
    }
}
