using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Replaces heavy inspector tabs with a compact content directory.
    /// Fields still use Odin's normal drawer chain; this drawer only selects
    /// which declared section is visible.
    /// </summary>
    public sealed class ESEditorSectionNavigatorDrawer : OdinGroupDrawer<ESEditorSectionAttribute>
    {
        private static readonly ConditionalWeakTable<PropertyTree, NavigationContext> Contexts
            = new ConditionalWeakTable<PropertyTree, NavigationContext>();
        private static GUIStyle sectionContainerStyle;
        private static GUIStyle sectionHeaderStyle;
        private static GUIStyle sectionSubtitleStyle;
        private static Texture2D sectionContainerTexture;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            NavigationContext context = Contexts.GetValue(Property.Tree, NavigationContext.Create);
            context.EnsureInitialized(Property.Tree);
            if (context.Register(Attribute))
                GUI.changed = true;

            if (context.IsFirst(Attribute.SectionId))
                context.DrawDirectory();

            if (context.IsSelected(Attribute.SectionId))
                DrawSectionChildren(context.GetSubtitle(Attribute.SectionId));
        }

        private void DrawSectionChildren(string subtitle)
        {
            using (new EditorGUILayout.VerticalScope(SectionContainerStyle))
            {
                GUILayout.Label(Attribute.DisplayName, SectionHeaderStyle);
                if (!string.IsNullOrEmpty(subtitle))
                    GUILayout.Label(subtitle, SectionSubtitleStyle);
                Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    Color dividerColor = EditorGUIUtility.isProSkin
                        ? new Color(0.30f, 0.32f, 0.35f, 1f)
                        : new Color(0.72f, 0.74f, 0.76f, 1f);
                    EditorGUI.DrawRect(dividerRect, dividerColor);
                }

                EditorGUILayout.Space(3f);
                // The next group drawer owns the child layout boundary. Drawing the children
                // directly here can interleave separate component inspectors after a reload.
                CallNextDrawer(GUIContent.none);
            }
        }

        private static GUIStyle SectionContainerStyle
        {
            get
            {
                if (sectionContainerStyle == null)
                {
                    sectionContainerStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, 2, 2),
                        padding = new RectOffset(9, 9, 7, 8),
                        border = new RectOffset(1, 1, 1, 1)
                    };
                    sectionContainerStyle.normal.background = SectionContainerTexture;
                }

                return sectionContainerStyle;
            }
        }

        private static Texture2D SectionContainerTexture
        {
            get
            {
                if (sectionContainerTexture == null)
                {
                    Color borderColor = EditorGUIUtility.isProSkin
                        ? new Color(0.34f, 0.37f, 0.40f, 1f)
                        : new Color(0.58f, 0.61f, 0.64f, 1f);
                    Color fillColor = EditorGUIUtility.isProSkin
                        ? new Color(0.22f, 0.23f, 0.25f, 1f)
                        : new Color(0.91f, 0.92f, 0.93f, 1f);

                    sectionContainerTexture = new Texture2D(3, 3, UnityEngine.TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ESEditorSectionContainer"
                    };

                    for (int y = 0; y < 3; y++)
                    {
                        for (int x = 0; x < 3; x++)
                            sectionContainerTexture.SetPixel(x, y, x == 1 && y == 1 ? fillColor : borderColor);
                    }

                    sectionContainerTexture.Apply(false, true);
                }

                return sectionContainerTexture;
            }
        }

        private static GUIStyle SectionHeaderStyle
        {
            get
            {
                if (sectionHeaderStyle == null)
                {
                    sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(0, 0, 0, 2)
                    };
                    sectionHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.83f, 0.85f, 0.88f, 1f)
                        : new Color(0.16f, 0.18f, 0.21f, 1f);
                }

                return sectionHeaderStyle;
            }
        }

        private static GUIStyle SectionSubtitleStyle
        {
            get
            {
                if (sectionSubtitleStyle == null)
                {
                    sectionSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        wordWrap = true,
                        padding = new RectOffset(0, 0, 1, 3)
                    };
                    sectionSubtitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.61f, 0.64f, 0.68f, 1f)
                        : new Color(0.39f, 0.42f, 0.45f, 1f);
                }

                return sectionSubtitleStyle;
            }
        }

        private sealed class NavigationContext
        {
            private const string SessionKeyPrefix = "ES.EditorSectionNavigator.";
            private const float DirectoryHeight = 30f;
            private const float DirectoryHorizontalInset = 4f;
            private const float SectionHorizontalPadding = 4f;
            private const float SeparatorWidth = 18f;
            private const float CompactMarkerHitWidth = 14f;
            private const float CompactMarkerVerticalHitPadding = 12f;
            private const float CompactMarkerEndpointHitPadding = 8f;
            private const float CompactMarkerSize = 4f;
            private const float CompactSelectedMarkerSize = 7f;
            private const double CompactLongPressSeconds = 0.12d;
            private readonly List<SectionDescriptor> sections = new List<SectionDescriptor>(8);
            private bool initialized;
            private string selectedId;
            private string selectionKey;
            private bool compactPointerActive;
            private bool compactPointerMoved;
            private double compactPointerStartedAt;
            private int compactPointerControlId;
            private int compactPressedSectionIndex;

            private static GUIStyle sectionStyle;
            private static GUIStyle selectedSectionStyle;
            private static GUIStyle separatorStyle;
            private static GUIStyle compactArrowStyle;
            private static GUIStyle compactTitleStyle;

            public static NavigationContext Create(PropertyTree _) => new NavigationContext();

            public void EnsureInitialized(PropertyTree tree)
            {
                if (initialized)
                    return;

                initialized = true;
                selectionKey = BuildSelectionKey(tree);

                RegisterDeclaredSections(tree.TargetType);

                SortSections();
                if (sections.Count == 0)
                    return;

                selectedId = SessionState.GetString(selectionKey, sections[0].Id);
                if (FindIndex(selectedId) < 0)
                    Select(sections[0].Id);
            }

            private void RegisterDeclaredSections(Type targetType)
            {
                const BindingFlags memberFlags = BindingFlags.Instance
                                                 | BindingFlags.Public
                                                 | BindingFlags.NonPublic
                                                 | BindingFlags.DeclaredOnly;

                for (Type type = targetType; type != null && type != typeof(object); type = type.BaseType)
                {
                    foreach (FieldInfo field in type.GetFields(memberFlags))
                        RegisterAttributes(field.GetCustomAttributes(typeof(ESEditorSectionAttribute), true));

                    foreach (PropertyInfo property in type.GetProperties(memberFlags))
                        RegisterAttributes(property.GetCustomAttributes(typeof(ESEditorSectionAttribute), true));

                    foreach (MethodInfo method in type.GetMethods(memberFlags))
                        RegisterAttributes(method.GetCustomAttributes(typeof(ESEditorSectionAttribute), true));
                }
            }

            private void RegisterAttributes(object[] attributes)
            {
                for (int i = 0; i < attributes.Length; i++)
                    Register(attributes[i] as ESEditorSectionAttribute);
            }

            public bool Register(ESEditorSectionAttribute section)
            {
                if (section == null || string.IsNullOrEmpty(section.SectionId))
                    return false;

                int index = FindIndex(section.SectionId);
                if (index >= 0)
                {
                    SectionDescriptor current = sections[index];
                    if (!string.Equals(current.DisplayName, section.DisplayName, StringComparison.Ordinal))
                        Debug.LogWarning("[ESEditorSectionNavigator] 同一内容分区 ID 使用了不同显示名：" + section.SectionId);

                    if (string.IsNullOrEmpty(current.Subtitle) && !string.IsNullOrEmpty(section.Subtitle))
                    {
                        sections[index] = new SectionDescriptor(
                            current.Id,
                            current.DisplayName,
                            section.Subtitle,
                            current.Order);
                        SortSections();
                        return true;
                    }

                    return false;
                }

                sections.Add(new SectionDescriptor(section.SectionId, section.DisplayName, section.Subtitle, section.Order));
                SortSections();

                if (string.IsNullOrEmpty(selectedId) || FindIndex(selectedId) < 0)
                    Select(sections[0].Id);

                return true;
            }

            public bool IsFirst(string sectionId)
            {
                return sections.Count > 0 && string.Equals(sections[0].Id, sectionId, StringComparison.Ordinal);
            }

            public bool IsSelected(string sectionId)
            {
                return string.Equals(selectedId, sectionId, StringComparison.Ordinal);
            }

            public string GetSubtitle(string sectionId)
            {
                int index = FindIndex(sectionId);
                return index >= 0 ? sections[index].Subtitle : null;
            }

            public void DrawDirectory()
            {
                if (sections.Count <= 1)
                    return;

                int selectedIndex = FindIndex(selectedId);
                if (selectedIndex < 0)
                    selectedIndex = 0;

                EditorGUILayout.Space(3f);
                if (RequiresCompactPicker())
                    DrawCompactDirectory(selectedIndex);
                else
                    DrawDirectoryLine(selectedIndex);
                EditorGUILayout.Space(4f);
            }

            private void DrawCompactDirectory(int selectedIndex)
            {
                Rect rowRect = GUILayoutUtility.GetRect(0f, DirectoryHeight, GUILayout.ExpandWidth(true));
                float markerRailWidth = sections.Count * CompactMarkerHitWidth;
                Rect titleRect = new Rect(rowRect.x + DirectoryHorizontalInset, rowRect.y,
                    Mathf.Max(72f, rowRect.width - markerRailWidth - 10f), DirectoryHeight - 1f);
                Rect arrowRect = new Rect(titleRect.xMax - 15f, rowRect.y, 14f, DirectoryHeight - 1f);
                Rect markerRailRect = new Rect(rowRect.xMax - markerRailWidth, rowRect.y,
                    markerRailWidth, DirectoryHeight - 1f);

                HandleCompactPointer(rowRect, titleRect, markerRailRect, selectedIndex);

                GUI.Label(titleRect, sections[selectedIndex].Content, CompactTitleStyle);
                GUI.Label(arrowRect, "▾", CompactArrowStyle);

                for (int i = 0; i < sections.Count; i++)
                {
                    SectionDescriptor section = sections[i];
                    bool selected = i == selectedIndex;
                    Rect hitRect = new Rect(markerRailRect.x + i * CompactMarkerHitWidth, rowRect.y,
                        CompactMarkerHitWidth, DirectoryHeight - 1f);

                    GUI.Label(hitRect, new GUIContent(string.Empty, section.DisplayName), GUIStyle.none);
                    if (Event.current.type != EventType.Repaint)
                        continue;

                    float markerSize = selected ? CompactSelectedMarkerSize : CompactMarkerSize;
                    Color markerColor = selected
                        ? (EditorGUIUtility.isProSkin
                            ? new Color(0.34f, 0.68f, 0.96f, 1f)
                            : new Color(0.08f, 0.38f, 0.72f, 1f))
                        : (EditorGUIUtility.isProSkin
                            ? new Color(0.42f, 0.45f, 0.49f, 1f)
                            : new Color(0.54f, 0.57f, 0.60f, 1f));
                    EditorGUI.DrawRect(new Rect(hitRect.center.x - markerSize * 0.5f,
                        hitRect.center.y - markerSize * 0.5f, markerSize, markerSize), markerColor);
                }

                if (Event.current.type == EventType.Repaint)
                {
                    Color dividerColor = EditorGUIUtility.isProSkin
                        ? new Color(0.28f, 0.30f, 0.33f, 1f)
                        : new Color(0.70f, 0.72f, 0.74f, 1f);
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), dividerColor);
                }
            }

            private void HandleCompactPointer(Rect rowRect, Rect titleRect, Rect markerRailRect, int selectedIndex)
            {
                int controlId = GUIUtility.GetControlID(FocusType.Passive, rowRect);
                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown
                    && currentEvent.button == 0
                    && rowRect.Contains(currentEvent.mousePosition))
                {
                    compactPointerActive = true;
                    compactPointerMoved = false;
                    compactPointerStartedAt = EditorApplication.timeSinceStartup;
                    compactPointerControlId = controlId;
                    compactPressedSectionIndex = GetCompactSectionIndex(markerRailRect, currentEvent.mousePosition);
                    GUIUtility.hotControl = controlId;
                    currentEvent.Use();
                    return;
                }

                if (!compactPointerActive || GUIUtility.hotControl != controlId || compactPointerControlId != controlId)
                    return;

                if (currentEvent.type == EventType.MouseDrag)
                {
                    compactPointerMoved = true;
                    if (EditorApplication.timeSinceStartup - compactPointerStartedAt >= CompactLongPressSeconds)
                    {
                        int targetIndex = GetCompactDragSectionIndex(markerRailRect, currentEvent.mousePosition);
                        if (targetIndex >= 0 && targetIndex != FindIndex(selectedId))
                            Select(sections[targetIndex].Id);
                    }

                    currentEvent.Use();
                    return;
                }

                if (currentEvent.type != EventType.MouseUp || currentEvent.button != 0)
                    return;

                bool wasLongPress = EditorApplication.timeSinceStartup - compactPointerStartedAt >= CompactLongPressSeconds;
                int releasedSectionIndex = GetCompactSectionIndex(markerRailRect, currentEvent.mousePosition);
                if (wasLongPress)
                {
                    int targetIndex = GetCompactDragSectionIndex(markerRailRect, currentEvent.mousePosition);
                    if (targetIndex >= 0 && targetIndex != FindIndex(selectedId))
                        Select(sections[targetIndex].Id);
                }
                else if (compactPressedSectionIndex >= 0)
                {
                    Select(sections[releasedSectionIndex >= 0 ? releasedSectionIndex : compactPressedSectionIndex].Id);
                }
                else if (!compactPointerMoved && titleRect.Contains(currentEvent.mousePosition))
                {
                    ShowCompactMenu(selectedIndex, titleRect);
                }

                compactPointerActive = false;
                compactPointerMoved = false;
                compactPressedSectionIndex = -1;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }

            private int GetCompactSectionIndex(Rect markerRailRect, Vector2 mousePosition)
            {
                if (!markerRailRect.Contains(mousePosition))
                    return -1;

                int index = Mathf.FloorToInt((mousePosition.x - markerRailRect.x) / CompactMarkerHitWidth);
                return index >= 0 && index < sections.Count ? index : -1;
            }

            private int GetCompactDragSectionIndex(Rect markerRailRect, Vector2 mousePosition)
            {
                Rect acceptedRect = new Rect(
                    markerRailRect.x - CompactMarkerEndpointHitPadding,
                    markerRailRect.y - CompactMarkerVerticalHitPadding,
                    markerRailRect.width + CompactMarkerEndpointHitPadding * 2f,
                    markerRailRect.height + CompactMarkerVerticalHitPadding * 2f);
                if (!acceptedRect.Contains(mousePosition))
                    return -1;

                float clampedX = Mathf.Clamp(mousePosition.x, markerRailRect.x, markerRailRect.xMax - 0.01f);
                int index = Mathf.FloorToInt((clampedX - markerRailRect.x) / CompactMarkerHitWidth);
                return Mathf.Clamp(index, 0, sections.Count - 1);
            }

            private void ShowCompactMenu(int selectedIndex, Rect anchorRect)
            {
                var entries = new List<ESSearchDropdown.Entry>(sections.Count);
                for (int i = 0; i < sections.Count; i++)
                {
                    SectionDescriptor section = sections[i];
                    string sectionId = section.Id;
                    entries.Add(ESSearchDropdown.Entry.Item(
                        section.DisplayName,
                        () => Select(sectionId),
                        subtitle: section.Subtitle,
                        tooltip: section.Subtitle,
                        selected: i == selectedIndex));
                }

                ESSearchDropdown.Open(
                    anchorRect,
                    "配置目录",
                    entries,
                    minimumWindowSize: new Vector2(360f, 260f));
            }

            private void DrawDirectoryLine(int selectedIndex)
            {
                Rect rowRect = GUILayoutUtility.GetRect(0f, DirectoryHeight, GUILayout.ExpandWidth(true));
                float nextX = rowRect.x + DirectoryHorizontalInset;
                Rect selectedRect = default;

                for (int i = 0; i < sections.Count; i++)
                {
                    SectionDescriptor section = sections[i];
                    GUIStyle style = i == selectedIndex ? SelectedSectionStyle : SectionStyle;
                    float width = style.CalcSize(section.Content).x + SectionHorizontalPadding * 2f;
                    Rect itemRect = new Rect(nextX, rowRect.y, width, DirectoryHeight - 1f);

                    if (GUI.Button(itemRect, GUIContent.none, GUIStyle.none))
                        Select(section.Id);

                    GUI.Label(itemRect, section.Content, style);
                    if (i == selectedIndex)
                        selectedRect = itemRect;

                    nextX = itemRect.xMax;
                    if (i >= sections.Count - 1)
                        continue;

                    Rect separatorRect = new Rect(nextX, rowRect.y, SeparatorWidth, DirectoryHeight - 1f);
                    GUI.Label(separatorRect, "·", SeparatorStyle);
                    nextX = separatorRect.xMax;
                }

                if (Event.current.type == EventType.Repaint)
                {
                    Color dividerColor = EditorGUIUtility.isProSkin
                        ? new Color(0.28f, 0.30f, 0.33f, 1f)
                        : new Color(0.70f, 0.72f, 0.74f, 1f);
                    Color accentColor = EditorGUIUtility.isProSkin
                        ? new Color(0.34f, 0.68f, 0.96f, 1f)
                        : new Color(0.08f, 0.38f, 0.72f, 1f);

                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), dividerColor);
                    if (selectedRect.width > 0f)
                    {
                        EditorGUI.DrawRect(
                            new Rect(selectedRect.x + SectionHorizontalPadding, rowRect.yMax - 2f,
                                selectedRect.width - SectionHorizontalPadding * 2f, 2f),
                            accentColor);
                    }
                }
            }

            private bool RequiresCompactPicker()
            {
                float requiredWidth = DirectoryHorizontalInset * 2f;
                for (int i = 0; i < sections.Count; i++)
                {
                    requiredWidth += SectionStyle.CalcSize(sections[i].Content).x + SectionHorizontalPadding * 2f;
                    if (i < sections.Count - 1)
                        requiredWidth += SeparatorWidth;
                }

                return requiredWidth > Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 32f);
            }

            private void Select(string sectionId)
            {
                selectedId = sectionId;
                if (!string.IsNullOrEmpty(selectionKey))
                    SessionState.SetString(selectionKey, selectedId);
                GUI.changed = true;
            }

            private void SortSections()
            {
                sections.Sort(SectionDescriptor.Compare);
            }

            private int FindIndex(string sectionId)
            {
                for (int i = 0; i < sections.Count; i++)
                    if (string.Equals(sections[i].Id, sectionId, StringComparison.Ordinal))
                        return i;

                return -1;
            }

            private static string BuildSelectionKey(PropertyTree tree)
            {
                string typeName = tree.TargetType == null ? "Unknown" : tree.TargetType.FullName;
                int targetId = 0;
                if (tree.WeakTargets.Count == 1 && tree.WeakTargets[0] is UnityEngine.Object target)
                    targetId = target.GetInstanceID();

                return SessionKeyPrefix + typeName + "." + targetId;
            }

            private static GUIStyle SectionStyle
            {
                get
                {
                    if (sectionStyle == null)
                    {
                        sectionStyle = new GUIStyle(EditorStyles.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(4, 4, 2, 2),
                            clipping = TextClipping.Clip
                        };

                        sectionStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.72f, 0.74f, 0.77f, 1f)
                            : new Color(0.28f, 0.30f, 0.33f, 1f);
                    }

                    return sectionStyle;
                }
            }

            private static GUIStyle SelectedSectionStyle
            {
                get
                {
                    if (selectedSectionStyle == null)
                    {
                        selectedSectionStyle = new GUIStyle(SectionStyle)
                        {
                            fontStyle = FontStyle.Bold
                        };

                        selectedSectionStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.70f, 0.84f, 1f, 1f)
                            : new Color(0.06f, 0.31f, 0.61f, 1f);
                    }

                    return selectedSectionStyle;
                }
            }

            private static GUIStyle SeparatorStyle
            {
                get
                {
                    if (separatorStyle == null)
                    {
                        separatorStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(0, 0, 2, 2)
                        };
                        separatorStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.42f, 0.44f, 0.48f, 1f)
                            : new Color(0.50f, 0.52f, 0.55f, 1f);
                    }

                    return separatorStyle;
                }
            }

            private static GUIStyle CompactArrowStyle
            {
                get
                {
                    if (compactArrowStyle == null)
                    {
                        compactArrowStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(0, 0, 1, 2)
                        };
                        compactArrowStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.62f, 0.66f, 0.70f, 1f)
                            : new Color(0.36f, 0.39f, 0.43f, 1f);
                    }

                    return compactArrowStyle;
                }
            }

            private static GUIStyle CompactTitleStyle
            {
                get
                {
                    if (compactTitleStyle == null)
                    {
                        compactTitleStyle = new GUIStyle(SelectedSectionStyle)
                        {
                            alignment = TextAnchor.MiddleLeft,
                            fontSize = 14,
                            fontStyle = FontStyle.Bold,
                            padding = new RectOffset(4, 18, 2, 2)
                        };
                    }

                    return compactTitleStyle;
                }
            }
        }

            private readonly struct SectionDescriptor
            {
                public readonly string Id;
                public readonly string DisplayName;
                public readonly string Subtitle;
                public readonly float Order;
                public readonly GUIContent Content;

            public SectionDescriptor(string id, string displayName, string subtitle, float order)
            {
                Id = id;
                DisplayName = displayName;
                Subtitle = subtitle;
                Order = order;
                Content = new GUIContent(displayName, subtitle);
            }

            public static int Compare(SectionDescriptor left, SectionDescriptor right)
            {
                int orderCompare = left.Order.CompareTo(right.Order);
                return orderCompare != 0 ? orderCompare : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            }
        }
    }
}
