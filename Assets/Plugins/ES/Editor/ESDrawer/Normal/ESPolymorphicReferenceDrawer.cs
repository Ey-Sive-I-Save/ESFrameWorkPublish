using System;
using System.Collections.Generic;
using System.Text;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Presentation-only managed-reference editor. It deliberately leaves SerializeReference
    /// ownership, child drawing, undo, and persistence with Unity and Odin.
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class ESPolymorphicReferenceDrawer : OdinAttributeDrawer<ESPolymorphicReferenceAttribute>
    {
        private static readonly Dictionary<Type, List<TypeDescriptor>> TypeDescriptorsByBaseType
            = new Dictionary<Type, List<TypeDescriptor>>();
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle selectedSelectorStyle;
        private static GUIStyle emptySelectorStyle;
        private static GUIStyle selectorArrowStyle;
        private static GUIStyle clearStyle;
        private bool expandedInitialized;
        private bool expanded;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            Type baseType = Property.BaseValueEntry?.TypeOfValue;
            if (!CanDraw(baseType))
            {
                CallNextDrawer(label);
                return;
            }

            if (!expandedInitialized)
            {
                expanded = Attribute.Expanded;
                expandedInitialized = true;
            }

            object value = Property.ValueEntry.WeakSmartValue;
            DrawHeader(label, baseType, value);

            if (!string.IsNullOrEmpty(Attribute.Subtitle))
                GUILayout.Label(Attribute.Subtitle, SubtitleStyle);

            DrawDivider();
            if (value == null)
                return;

            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            try
            {
                for (int i = 0; i < Property.Children.Count; i++)
                    Property.Children[i].Draw();
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawHeader(GUIContent label, Type baseType, object value)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            bool canClear = Attribute.AllowNull && value != null;
            float clearWidth = canClear ? 18f : 0f;
            float selectorWidth = Mathf.Min(230f, Mathf.Max(144f, rect.width * 0.44f));
            Rect foldoutRect = new Rect(rect.x, rect.y + 5f, 16f, rect.height - 6f);
            Rect titleRect = new Rect(foldoutRect.xMax + 2f, rect.y, rect.width - selectorWidth - clearWidth - 22f, rect.height);
            Rect selectorRect = new Rect(rect.xMax - selectorWidth - clearWidth, rect.y + 3f, selectorWidth - 3f, rect.height - 6f);
            Rect clearRect = new Rect(selectorRect.xMax + 2f, rect.y + 3f, 16f, rect.height - 6f);

            expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
            string title = string.IsNullOrEmpty(Attribute.Title)
                ? (label == null || string.IsNullOrEmpty(label.text) ? Property.NiceName : label.text)
                : Attribute.Title;
            GUI.Label(titleRect, title, TitleStyle);

            TypeDescriptor current = value == null ? default : DescribeType(value.GetType());
            bool hasValue = value != null;
            GUIContent selectorContent = hasValue
                ? new GUIContent(current.DisplayName, current.Subtitle)
                : new GUIContent("创建并选择类型", "创建一个具体的多态配置");
            if (GUI.Button(selectorRect, selectorContent, GUIStyle.none))
                OpenTypePicker(selectorRect, baseType, value == null ? null : value.GetType());

            DrawSelector(selectorRect, selectorContent, hasValue);
            if (canClear)
            {
                if (GUI.Button(clearRect, new GUIContent("×", "清空当前多态配置"), GUIStyle.none))
                    Property.ValueEntry.WeakSmartValue = null;
                GUI.Label(clearRect, "×", ClearStyle);
            }
        }

        private static void DrawSelector(Rect rect, GUIContent content, bool selected)
        {
            Rect textRect = new Rect(rect.x, rect.y, rect.width - 16f, rect.height);
            Rect arrowRect = new Rect(textRect.xMax, rect.y, 16f, rect.height);
            GUI.Label(textRect, content, selected ? SelectedSelectorStyle : EmptySelectorStyle);
            GUI.Label(arrowRect, "▾", SelectorArrowStyle);

            if (Event.current.type != EventType.Repaint)
                return;

            bool hovered = rect.Contains(Event.current.mousePosition);
            if (!selected && !hovered)
                return;

            Color underline = selected
                ? (EditorGUIUtility.isProSkin
                    ? new Color(0.34f, 0.68f, 0.96f, 0.90f)
                    : new Color(0.08f, 0.38f, 0.72f, 0.90f))
                : (EditorGUIUtility.isProSkin
                    ? new Color(0.48f, 0.51f, 0.55f, 1f)
                    : new Color(0.48f, 0.51f, 0.55f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), underline);
        }

        private void OpenTypePicker(Rect anchorRect, Type baseType, Type selectedType)
        {
            List<TypeDescriptor> descriptors = GetTypeDescriptors(baseType);
            var entries = new List<ESSearchDropdown.Entry>(descriptors.Count);
            for (int i = 0; i < descriptors.Count; i++)
            {
                TypeDescriptor descriptor = descriptors[i];
                Type selected = descriptor.Type;
                entries.Add(ESSearchDropdown.Entry.Item(
                    descriptor.DisplayName,
                    () => CreateValue(selected),
                    descriptor.Category,
                    subtitle: descriptor.Subtitle,
                    tooltip: descriptor.Tooltip,
                    selected: selected == selectedType));
            }

            if (entries.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "没有可创建的具体类型",
                    tooltip: "需要非抽象、非 UnityEngine.Object 且具有无参构造函数的派生类。"));
            }

            ESSearchDropdown.Open(
                anchorRect,
                "选择 " + GetDisplayName(baseType),
                entries,
                minimumWindowSize: new Vector2(380f, 300f));
        }

        private void CreateValue(Type concreteType)
        {
            try
            {
                Property.ValueEntry.WeakSmartValue = Activator.CreateInstance(concreteType);
                expanded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESPolymorphicReference] 无法创建多态类型：" + concreteType.FullName,
                    exception));
            }
        }

        private static bool CanDraw(Type type)
        {
            return type != null
                   && (type.IsClass || type.IsInterface)
                   && !typeof(System.Collections.IList).IsAssignableFrom(type)
                   && !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private static List<TypeDescriptor> GetTypeDescriptors(Type baseType)
        {
            if (TypeDescriptorsByBaseType.TryGetValue(baseType, out List<TypeDescriptor> cached))
                return cached;

            var descriptors = new List<TypeDescriptor>();
            if (CanCreate(baseType))
                descriptors.Add(DescribeType(baseType));

            foreach (Type candidate in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (CanCreate(candidate))
                    descriptors.Add(DescribeType(candidate));
            }

            descriptors.Sort(TypeDescriptor.Compare);
            TypeDescriptorsByBaseType.Add(baseType, descriptors);
            return descriptors;
        }

        private static bool CanCreate(Type type)
        {
            return type != null
                   && type.IsClass
                   && !type.IsAbstract
                   && !type.IsGenericTypeDefinition
                   && !type.ContainsGenericParameters
                   && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                   && !System.Attribute.IsDefined(type, typeof(ObsoleteAttribute))
                   && type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static TypeDescriptor DescribeType(Type type)
        {
            var attribute = (ESPolymorphicTypeAttribute)System.Attribute.GetCustomAttribute(
                type,
                typeof(ESPolymorphicTypeAttribute));
            string category = attribute?.Category;
            string displayName = attribute?.DisplayName;
            if (string.IsNullOrEmpty(category))
                category = GetDefaultCategory(type);
            if (string.IsNullOrEmpty(displayName))
                displayName = GetDisplayName(type);

            string subtitle = attribute?.Subtitle;
            string tooltip = string.IsNullOrEmpty(subtitle)
                ? type.FullName
                : subtitle + "\n" + type.FullName;
            return new TypeDescriptor(type, displayName, category, subtitle, tooltip, attribute?.Order ?? 0);
        }

        private static string GetDefaultCategory(Type type)
        {
            string name = TrimCommonPrefix(type.Name);
            int separator = name.IndexOf('_');
            return separator > 0 ? SplitWords(name.Substring(0, separator)) : "其他";
        }

        private static string GetDisplayName(Type type)
        {
            string name = TrimCommonPrefix(type.Name);
            int separator = name.IndexOf('_');
            return SplitWords(separator >= 0 ? name.Substring(separator + 1) : name);
        }

        private static string TrimCommonPrefix(string name)
        {
            if (name.StartsWith("ES", StringComparison.Ordinal) && name.Length > 2)
                return name.Substring(2);
            if (name.StartsWith("Op", StringComparison.Ordinal) && name.Length > 2)
                return name.Substring(2);
            return name;
        }

        private static string SplitWords(string value)
        {
            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '_')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                        builder.Append(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                    builder.Append(' ');
                builder.Append(current);
            }

            return builder.ToString().Trim();
        }

        private static void DrawDivider()
        {
            Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
                return;

            Color color = EditorGUIUtility.isProSkin
                ? new Color(0.30f, 0.32f, 0.35f, 1f)
                : new Color(0.72f, 0.74f, 0.76f, 1f);
            EditorGUI.DrawRect(dividerRect, color);
        }

        private static GUIStyle TitleStyle
        {
            get
            {
                if (titleStyle == null)
                {
                    titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 15,
                        clipping = TextClipping.Clip
                    };
                }

                return titleStyle;
            }
        }

        private static GUIStyle SubtitleStyle
        {
            get
            {
                if (subtitleStyle == null)
                {
                    subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        padding = new RectOffset(18, 0, 0, 2)
                    };
                    subtitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.60f, 0.63f, 0.67f, 1f)
                        : new Color(0.40f, 0.43f, 0.46f, 1f);
                }

                return subtitleStyle;
            }
        }

        private static GUIStyle SelectedSelectorStyle
        {
            get
            {
                if (selectedSelectorStyle == null)
                {
                    selectedSelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        clipping = TextClipping.Clip
                    };
                    selectedSelectorStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.62f, 0.80f, 1f, 1f)
                        : new Color(0.06f, 0.31f, 0.61f, 1f);
                }

                return selectedSelectorStyle;
            }
        }

        private static GUIStyle EmptySelectorStyle
        {
            get
            {
                if (emptySelectorStyle == null)
                {
                    emptySelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        clipping = TextClipping.Clip
                    };
                    emptySelectorStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.63f, 0.66f, 0.70f, 1f)
                        : new Color(0.38f, 0.41f, 0.45f, 1f);
                }

                return emptySelectorStyle;
            }
        }

        private static GUIStyle SelectorArrowStyle
        {
            get
            {
                if (selectorArrowStyle == null)
                {
                    selectorArrowStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    selectorArrowStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.59f, 0.62f, 0.66f, 1f)
                        : new Color(0.39f, 0.42f, 0.46f, 1f);
                }

                return selectorArrowStyle;
            }
        }

        private static GUIStyle ClearStyle
        {
            get
            {
                if (clearStyle == null)
                {
                    clearStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 13
                    };
                    clearStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.65f, 0.48f, 0.48f, 1f)
                        : new Color(0.62f, 0.28f, 0.28f, 1f);
                }

                return clearStyle;
            }
        }

        private readonly struct TypeDescriptor
        {
            public readonly Type Type;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly string Subtitle;
            public readonly string Tooltip;
            public readonly int Order;

            public TypeDescriptor(Type type, string displayName, string category, string subtitle, string tooltip, int order)
            {
                Type = type;
                DisplayName = displayName;
                Category = category;
                Subtitle = subtitle;
                Tooltip = tooltip;
                Order = order;
            }

            public static int Compare(TypeDescriptor left, TypeDescriptor right)
            {
                int categoryCompare = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
                if (categoryCompare != 0)
                    return categoryCompare;

                int orderCompare = left.Order.CompareTo(right.Order);
                return orderCompare != 0
                    ? orderCompare
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            }
        }
    }
}
