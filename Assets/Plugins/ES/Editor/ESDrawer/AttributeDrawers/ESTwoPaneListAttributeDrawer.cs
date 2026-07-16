using Sirenix.OdinInspector.Editor;
using System.Collections;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed class ESTwoPaneListAttributeDrawer : OdinAttributeDrawer<ESTwoPaneListAttribute>
    {
        private const float ToolbarHeight = 24f;
        private const float SearchHeight = 20f;
        private const float RowHeight = 24f;
        private const float Gap = 6f;

        private static readonly System.Collections.Generic.Dictionary<string, int> SelectedIndexByPath =
            new System.Collections.Generic.Dictionary<string, int>(64);
        private static readonly System.Collections.Generic.Dictionary<string, Vector2> LeftScrollByPath =
            new System.Collections.Generic.Dictionary<string, Vector2>(64);
        private static readonly System.Collections.Generic.Dictionary<string, Vector2> RightScrollByPath =
            new System.Collections.Generic.Dictionary<string, Vector2>(64);
        private static readonly System.Collections.Generic.Dictionary<string, string> SearchByPath =
            new System.Collections.Generic.Dictionary<string, string>(64);

        private static GUIStyle selectedRowStyle;
        private static GUIStyle normalRowStyle;
        private static GUIStyle emptyLabelStyle;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            IList list = Property.ValueEntry.WeakSmartValue as IList;
            SerializedProperty serializedProperty = Property.Tree.UnitySerializedObject != null
                ? Property.Tree.UnitySerializedObject.FindProperty(Property.UnityPropertyPath)
                : null;

            if (list == null || serializedProperty == null || !serializedProperty.isArray)
            {
                CallNextDrawer(label);
                return;
            }

            EnsureStyles();

            bool expanded = EditorGUILayout.Foldout(Property.State.Expanded, label, true);
            Property.State.Expanded = expanded;
            if (!expanded)
                return;

            Rect body = EditorGUILayout.GetControlRect(false, Mathf.Max(Attribute.minHeight, 160f));
            float leftWidth = Mathf.Clamp(Attribute.leftWidth, 160f, Mathf.Max(160f, body.width * 0.55f));
            Rect left = new Rect(body.x, body.y, leftWidth, body.height);
            Rect right = new Rect(left.xMax + Gap, body.y, Mathf.Max(120f, body.xMax - left.xMax - Gap), body.height);

            GUI.Box(left, GUIContent.none);
            GUI.Box(right, GUIContent.none);

            DrawLeftPane(left, list, serializedProperty);
            DrawRightPane(right, list, serializedProperty);
        }

        private void DrawLeftPane(Rect rect, IList list, SerializedProperty serializedProperty)
        {
            Rect toolbar = new Rect(rect.x + Gap, rect.y + Gap, rect.width - Gap * 2f, ToolbarHeight);
            EditorGUI.LabelField(toolbar, Attribute.leftTitle + "  " + list.Count + " 项", EditorStyles.boldLabel);

            Rect addRect = new Rect(toolbar.xMax - 136f, toolbar.y, 24f, ToolbarHeight);
            Rect duplicateRect = new Rect(toolbar.xMax - 108f, toolbar.y, 24f, ToolbarHeight);
            Rect removeRect = new Rect(toolbar.xMax - 80f, toolbar.y, 24f, ToolbarHeight);
            Rect upRect = new Rect(toolbar.xMax - 52f, toolbar.y, 24f, ToolbarHeight);
            Rect downRect = new Rect(toolbar.xMax - 24f, toolbar.y, 24f, ToolbarHeight);

            bool canResize = !list.IsFixedSize && !list.IsReadOnly;
            GUI.enabled = canResize;
            if (GUI.Button(addRect, "+"))
                AddElement(serializedProperty);

            int selectedIndex = GetSelectedIndex(list);
            GUI.enabled = canResize && list.Count > 0;
            if (GUI.Button(duplicateRect, "D"))
                DuplicateElement(serializedProperty, selectedIndex);

            if (GUI.Button(removeRect, "-"))
                RemoveElement(serializedProperty, selectedIndex);
            GUI.enabled = true;

            GUI.enabled = list.Count > 1 && selectedIndex > 0;
            if (GUI.Button(upRect, "\u2191"))
                MoveElement(serializedProperty, selectedIndex, selectedIndex - 1);

            GUI.enabled = list.Count > 1 && selectedIndex < list.Count - 1;
            if (GUI.Button(downRect, "\u2193"))
                MoveElement(serializedProperty, selectedIndex, selectedIndex + 1);
            GUI.enabled = true;

            string key = GetStateKey();
            Rect viewRect;
            string search = string.Empty;
            if (Attribute.searchable)
            {
                Rect searchRect = new Rect(rect.x + Gap, toolbar.yMax + Gap, rect.width - Gap * 2f, SearchHeight);
                search = GetSearch(key);
                EditorGUI.BeginChangeCheck();
                search = EditorGUI.TextField(searchRect, search, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                    SetSearch(key, search);

                viewRect = new Rect(rect.x + Gap, searchRect.yMax + Gap, rect.width - Gap * 2f, rect.yMax - searchRect.yMax - Gap * 2f);
            }
            else
            {
                viewRect = new Rect(rect.x + Gap, toolbar.yMax + Gap, rect.width - Gap * 2f, rect.yMax - toolbar.yMax - Gap * 2f);
            }

            int visibleCount = CountVisible(list, search);
            Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, viewRect.width - 16f), Mathf.Max(viewRect.height, visibleCount * RowHeight));
            Vector2 scroll = GetScroll(LeftScrollByPath, key);
            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);

            float y = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                string itemLabel = GetElementLabel(list[i], i);
                if (!IsSearchMatch(itemLabel, search))
                    continue;

                Rect row = new Rect(0f, y, contentRect.width, RowHeight - 2f);
                bool selected = i == selectedIndex;
                if (selected)
                    EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.90f, 0.28f));

                if (GUI.Button(row, itemLabel, selected ? selectedRowStyle : normalRowStyle))
                {
                    SetSelectedIndex(i);
                    GUI.FocusControl(null);
                }

                y += RowHeight;
            }

            if (visibleCount == 0)
            {
                Rect emptyRect = new Rect(0f, 0f, contentRect.width, RowHeight);
                GUI.Label(emptyRect, list.Count == 0 ? "暂无数据" : "没有匹配项", emptyLabelStyle);
            }

            GUI.EndScrollView();
            SetScroll(LeftScrollByPath, key, scroll);
        }

        private void DrawRightPane(Rect rect, IList list, SerializedProperty serializedProperty)
        {
            int selectedIndex = GetSelectedIndex(list);
            string selectedLabel = selectedIndex >= 0 && selectedIndex < list.Count
                ? GetElementLabel(list[selectedIndex], selectedIndex)
                : string.Empty;

            Rect title = new Rect(rect.x + Gap, rect.y + Gap, rect.width - Gap * 2f, ToolbarHeight);
            EditorGUI.LabelField(title, string.IsNullOrEmpty(selectedLabel) ? Attribute.rightTitle : Attribute.rightTitle + "  /  " + selectedLabel, EditorStyles.boldLabel);

            Rect detail = new Rect(rect.x + Gap, title.yMax + Gap, rect.width - Gap * 2f, rect.height - ToolbarHeight - Gap * 3f);
            if (selectedIndex < 0 || selectedIndex >= serializedProperty.arraySize)
            {
                EditorGUI.LabelField(detail, "暂无数据", emptyLabelStyle);
                return;
            }

            SerializedProperty element = serializedProperty.GetArrayElementAtIndex(selectedIndex);
            element.isExpanded = true;
            float elementHeight = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            string key = GetStateKey() + "|right|" + selectedIndex;
            Vector2 scroll = GetScroll(RightScrollByPath, key);
            Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, detail.width - 16f), Mathf.Max(detail.height, elementHeight));
            scroll = GUI.BeginScrollView(detail, scroll, contentRect);
            Rect elementRect = new Rect(0f, 0f, contentRect.width, elementHeight);
            EditorGUI.PropertyField(elementRect, element, GUIContent.none, true);
            GUI.EndScrollView();
            SetScroll(RightScrollByPath, key, scroll);
        }

        private void AddElement(SerializedProperty property)
        {
            property.serializedObject.Update();
            property.arraySize++;
            SetSelectedIndex(property.arraySize - 1);
            property.serializedObject.ApplyModifiedProperties();
        }

        private void DuplicateElement(SerializedProperty property, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            property.serializedObject.Update();
            property.InsertArrayElementAtIndex(index);
            SetSelectedIndex(index + 1);
            property.serializedObject.ApplyModifiedProperties();
        }

        private void RemoveElement(SerializedProperty property, int index)
        {
            if (index < 0 || index >= property.arraySize)
                return;

            property.serializedObject.Update();
            property.DeleteArrayElementAtIndex(index);
            SetSelectedIndex(Mathf.Clamp(index, 0, property.arraySize - 1));
            property.serializedObject.ApplyModifiedProperties();
        }

        private void MoveElement(SerializedProperty property, int from, int to)
        {
            if (from < 0 || from >= property.arraySize || to < 0 || to >= property.arraySize || from == to)
                return;

            property.serializedObject.Update();
            property.MoveArrayElement(from, to);
            SetSelectedIndex(to);
            property.serializedObject.ApplyModifiedProperties();
        }

        private int GetSelectedIndex(IList list)
        {
            if (!SelectedIndexByPath.TryGetValue(GetStateKey(), out int index))
                index = 0;
            return Mathf.Clamp(index, 0, Mathf.Max(0, list.Count - 1));
        }

        private void SetSelectedIndex(int index)
        {
            SelectedIndexByPath[GetStateKey()] = Mathf.Max(0, index);
        }

        private string GetElementLabel(object element, int index)
        {
            if (element == null)
                return Attribute.showIndex ? index + ". <null>" : "<null>";

            string value = TryGetMemberString(element, Attribute.itemLabelMember);
            if (string.IsNullOrEmpty(value))
                value = TryGetMemberString(element, "displayName");
            if (string.IsNullOrEmpty(value))
                value = TryGetMemberString(element, "name");
            if (string.IsNullOrEmpty(value))
                value = TryGetMemberString(element, "actionName");
            if (string.IsNullOrEmpty(value))
                value = TryGetMemberString(element, "id");
            if (string.IsNullOrEmpty(value))
                value = "元素 " + index;

            return Attribute.showIndex ? index + ". " + value : value;
        }

        private static string TryGetMemberString(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            System.Type type = target.GetType();
            var field = type.GetField(memberName);
            if (field != null)
                return ConvertMemberValue(field.GetValue(target));

            var property = type.GetProperty(memberName);
            return property != null && property.GetIndexParameters().Length == 0
                ? ConvertMemberValue(property.GetValue(target, null))
                : null;
        }

        private static string ConvertMemberValue(object value)
        {
            return value == null ? null : value.ToString();
        }

        private int CountVisible(IList list, string search)
        {
            if (string.IsNullOrEmpty(search))
                return list.Count;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (IsSearchMatch(GetElementLabel(list[i], i), search))
                    count++;
            }

            return count;
        }

        private static bool IsSearchMatch(string value, string search)
        {
            return string.IsNullOrEmpty(search)
                   || (!string.IsNullOrEmpty(value)
                       && value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string GetStateKey()
        {
            return Property.Tree.GetHashCode() + "|" + Property.Path;
        }

        private static Vector2 GetScroll(System.Collections.Generic.Dictionary<string, Vector2> table, string key)
        {
            return table.TryGetValue(key, out Vector2 value) ? value : Vector2.zero;
        }

        private static void SetScroll(System.Collections.Generic.Dictionary<string, Vector2> table, string key, Vector2 value)
        {
            table[key] = value;
        }

        private static string GetSearch(string key)
        {
            return SearchByPath.TryGetValue(key, out string value) ? value : string.Empty;
        }

        private static void SetSearch(string key, string value)
        {
            SearchByPath[key] = value ?? string.Empty;
        }

        private static void EnsureStyles()
        {
            if (normalRowStyle != null)
                return;

            normalRowStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 6, 0, 0),
                clipping = TextClipping.Clip
            };
            selectedRowStyle = new GUIStyle(normalRowStyle)
            {
                fontStyle = FontStyle.Bold
            };
            emptyLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
