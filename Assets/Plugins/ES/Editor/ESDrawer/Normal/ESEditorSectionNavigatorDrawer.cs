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
    /// Replaces heavy inspector tabs with an adaptive content directory.
    /// Section names remain visible by default, wrap into multiple rows when needed,
    /// and can be hidden per inspector session without affecting field rendering.
    /// </summary>
    public sealed class ESEditorSectionNavigatorDrawer : OdinGroupDrawer<ESEditorSectionAttribute>
    {
        private static readonly ConditionalWeakTable<PropertyTree, NavigationContexts> Contexts
            = new ConditionalWeakTable<PropertyTree, NavigationContexts>();
        protected override void DrawPropertyLayout(GUIContent label)
        {
            NavigationContexts contexts = Contexts.GetValue(Property.Tree, NavigationContexts.Create);
            NavigationContext context = contexts.Get(Attribute.NavigatorId);
            context.EnsureInitialized(Property.Tree);
            if (context.Register(Attribute))
                GUI.changed = true;

            if (context.IsFirst(Attribute.SectionId))
            {
                context.ResetDrawPass();
                if (context.TryDrawUnifiedPanel(Property.Tree))
                    context.MarkUnifiedPanelDrawn();
                else
                    context.DrawDirectory();
            }

            if (context.IsSelected(Attribute.SectionId) && !context.UnifiedPanelDrawn)
                DrawSectionChildren(Property, Attribute, context.GetSubtitle(Attribute.SectionId));
        }

        private void DrawSectionChildren(
            InspectorProperty sectionProperty,
            ESEditorSectionAttribute attribute,
            string subtitle)
        {
            using (new EditorGUILayout.VerticalScope(SectionContainerStyle))
                DrawSectionBody(sectionProperty, attribute, subtitle);
        }

        private static void DrawSectionBody(
            InspectorProperty sectionProperty,
            ESEditorSectionAttribute attribute,
            string subtitle)
        {
            GUILayout.Label(attribute.DisplayName, SectionHeaderStyle);
            if (!string.IsNullOrEmpty(subtitle))
                GUILayout.Label(subtitle, SectionSubtitleStyle);

            Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                ESEditorPresentation.DrawDivider(dividerRect);
            }

            EditorGUILayout.Space(3f);
            // Draw the group's children directly. Calling the next group drawer here
            // re-enters Odin's default group layout and creates a second foldout for every
            // selected section. InspectorProperty.Draw keeps each child on Odin's normal
            // drawer chain without introducing another page-level foldout.
            for (int i = 0; i < sectionProperty.Children.Count; i++)
                sectionProperty.Children[i].Draw(GUIContent.none);
        }

        private static GUIStyle SectionContainerStyle
        {
            get { return ESEditorPresentation.SurfaceStyle; }
        }

        private static GUIStyle SectionSurfaceStyle
        {
            get { return ESEditorPresentation.SurfaceStyle; }
        }

        private static GUIStyle SectionHeaderStyle
        {
            get { return ESEditorPresentation.HeaderStyle; }
        }

        private static GUIStyle SectionSubtitleStyle
        {
            get { return ESEditorPresentation.SubtitleStyle; }
        }

        private sealed class NavigationContexts
        {
            private readonly Dictionary<string, NavigationContext> contexts
                = new Dictionary<string, NavigationContext>(StringComparer.Ordinal);

            public static NavigationContexts Create(PropertyTree _) => new NavigationContexts();

            public NavigationContext Get(string navigatorId)
            {
                string key = string.IsNullOrWhiteSpace(navigatorId)
                    ? ESEditorSectionAttribute.DefaultNavigatorId
                    : navigatorId.Trim();
                if (!contexts.TryGetValue(key, out NavigationContext context))
                {
                    context = new NavigationContext(key);
                    contexts.Add(key, context);
                }

                return context;
            }
        }

        private sealed class NavigationContext
        {
            private const string SessionKeyPrefix = "ES.EditorSectionNavigator.";
            private const float DirectoryToolbarHeight = 22f;
            private const float DirectoryRowHeight = 24f;
            private const float DirectoryHeight = DirectoryRowHeight;
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
            private string visibilityKey;
            private bool directoryVisible = true;
            private bool unifiedPanelDrawn;
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
            private static GUIStyle directoryCaptionStyle;
            private static GUIStyle directoryToggleStyle;
            private readonly string navigatorId;
            private readonly GUIContent directoryToggleContent = new GUIContent();

            public NavigationContext(string navigatorId)
            {
                this.navigatorId = navigatorId;
            }

            public void EnsureInitialized(PropertyTree tree)
            {
                if (initialized)
                    return;

                initialized = true;
                selectionKey = BuildSelectionKey(tree);
                visibilityKey = selectionKey + ".directoryVisible";
                directoryVisible = SessionState.GetBool(visibilityKey, true);

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
                        RegisterSectionSyntax(field);

                    foreach (PropertyInfo property in type.GetProperties(memberFlags))
                        RegisterSectionSyntax(property);

                    foreach (MethodInfo method in type.GetMethods(memberFlags))
                        RegisterSectionSyntax(method);
                }
            }

            private void RegisterSectionSyntax(MemberInfo member)
            {
                object[] attributes = member.GetCustomAttributes(true);
                for (int i = 0; i < attributes.Length; i++)
                {
                    if (attributes[i] is ESEditorBeginSectionAttribute begin)
                    {
                        Register(new ESEditorSectionAttribute(
                            begin.NavigatorId,
                            begin.SectionId,
                            begin.DisplayName,
                            begin.Order,
                            begin.Subtitle));
                    }
                    else if (attributes[i] is ESEditorSectionAttribute section && !section.IsContinuation)
                    {
                        Register(section);
                    }
                }
            }

            public bool Register(ESEditorSectionAttribute section)
            {
                if (section == null
                    || string.IsNullOrEmpty(section.SectionId)
                    || !string.Equals(section.NavigatorId, navigatorId, StringComparison.Ordinal))
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

            public bool UnifiedPanelDrawn => unifiedPanelDrawn;

            public void ResetDrawPass()
            {
                unifiedPanelDrawn = false;
            }

            public void MarkUnifiedPanelDrawn()
            {
                unifiedPanelDrawn = true;
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
                int selectedIndex = FindIndex(selectedId);
                if (selectedIndex < 0)
                    selectedIndex = 0;

                DrawDirectoryContents(selectedIndex);
                EditorGUILayout.Space(3f);
            }

            public bool TryDrawUnifiedPanel(PropertyTree tree)
            {
                if (tree == null || sections.Count == 0)
                    return false;

                int selectedIndex = FindIndex(selectedId);
                if (selectedIndex < 0)
                    selectedIndex = 0;

                ESEditorSectionAttribute selectedAttribute = null;
                InspectorProperty selectedProperty = FindSectionProperty(
                    tree.RootProperty,
                    sections[selectedIndex].Id,
                    navigatorId,
                    ref selectedAttribute);
                if (selectedProperty == null || selectedAttribute == null)
                    return false;

                using (new EditorGUILayout.VerticalScope(SectionSurfaceStyle))
                {
                    DrawDirectoryContents(selectedIndex);
                    DrawDirectoryContentDivider();
                    DrawSectionBody(
                        selectedProperty,
                        selectedAttribute,
                        GetSubtitle(selectedAttribute.SectionId));
                }

                return true;
            }

            private static void DrawDirectoryContentDivider()
            {
                // Keep navigation and content in one surface, but give the eye a quiet
                // transition so the selected page does not visually run into the directory.
                EditorGUILayout.Space(3f);
                Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    Color dividerColor = ESEditorPresentation.DividerColor;
                    dividerColor.a = EditorGUIUtility.isProSkin ? 0.72f : 0.90f;
                    EditorGUI.DrawRect(dividerRect, dividerColor);
                }

                EditorGUILayout.Space(3f);
            }

            private void DrawDirectoryContents(int selectedIndex)
            {
                if (sections.Count <= 1)
                    return;

                DrawDirectoryToolbar(selectedIndex);
                if (directoryVisible)
                    DrawWrappedDirectory();
                else
                    DrawCompactDirectory(selectedIndex);
            }

            private static InspectorProperty FindSectionProperty(
                InspectorProperty property,
                string sectionId,
                string navigatorId,
                ref ESEditorSectionAttribute sectionAttribute)
            {
                if (property == null)
                    return null;

                ESEditorSectionAttribute candidate = property.GetAttribute<ESEditorSectionAttribute>();
                if (candidate != null
                    && property.Info != null
                    && property.Info.PropertyType == PropertyType.Group
                    && !candidate.IsContinuation
                    && string.Equals(candidate.SectionId, sectionId, StringComparison.Ordinal)
                    && string.Equals(candidate.NavigatorId, navigatorId, StringComparison.Ordinal))
                {
                    sectionAttribute = candidate;
                    return property;
                }

                for (int i = 0; i < property.Children.Count; i++)
                {
                    InspectorProperty match = FindSectionProperty(
                        property.Children[i], sectionId, navigatorId, ref sectionAttribute);
                    if (match != null)
                        return match;
                }

                return null;
            }

            private void DrawDirectoryToolbar(int selectedIndex)
            {
                Rect toolbarRect = GUILayoutUtility.GetRect(
                    0f,
                    DirectoryToolbarHeight,
                    GUILayout.ExpandWidth(true));
                Rect titleRect = new Rect(
                    toolbarRect.x + DirectoryHorizontalInset,
                    toolbarRect.y,
                    Mathf.Max(80f, toolbarRect.width - 72f),
                    toolbarRect.height);
                Rect toggleRect = new Rect(
                    toolbarRect.xMax - 68f,
                    toolbarRect.y + 1f,
                    64f,
                    toolbarRect.height - 2f);

                GUI.Label(titleRect, "配置目录", DirectoryCaptionStyle);

                directoryToggleContent.text = directoryVisible ? "隐藏" : "显示";
                directoryToggleContent.tooltip = directoryVisible
                    ? "隐藏分区名称，保留当前分区内容"
                    : "显示全部分区名称";
                if (GUI.Button(toggleRect, directoryToggleContent, DirectoryToggleStyle))
                {
                    directoryVisible = !directoryVisible;
                    if (!string.IsNullOrEmpty(visibilityKey))
                        SessionState.SetBool(visibilityKey, directoryVisible);
                    GUI.changed = true;
                }

                if (Event.current.type == EventType.Repaint)
                {
                    Color dividerColor = ESEditorPresentation.DividerColor;
                    EditorGUI.DrawRect(
                        new Rect(toolbarRect.x, toolbarRect.yMax - 1f, toolbarRect.width, 1f),
                        dividerColor);
                }
            }

            private void DrawWrappedDirectory()
            {
                float availableWidth = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - 32f);
                int rowCount = CalculateDirectoryRowCount(availableWidth);
                float totalHeight = Mathf.Max(DirectoryRowHeight, rowCount * DirectoryRowHeight);
                Rect directoryRect = GUILayoutUtility.GetRect(
                    0f,
                    totalHeight,
                    GUILayout.ExpandWidth(true));

                float rowStartX = directoryRect.x + DirectoryHorizontalInset;
                float nextX = rowStartX;
                float rowY = directoryRect.y;
                int row = 0;
                int selectedIndex = FindIndex(selectedId);

                for (int i = 0; i < sections.Count; i++)
                {
                    SectionDescriptor section = sections[i];
                    GUIStyle style = i == selectedIndex ? SelectedSectionStyle : SectionStyle;
                    float itemWidth = style.CalcSize(section.Content).x + SectionHorizontalPadding * 2f;
                    if (nextX > rowStartX && nextX + itemWidth > directoryRect.xMax - DirectoryHorizontalInset)
                    {
                        row++;
                        nextX = rowStartX;
                        rowY = directoryRect.y + row * DirectoryRowHeight;
                    }

                    Rect itemRect = new Rect(
                        nextX,
                        rowY,
                        Mathf.Max(40f, Mathf.Min(itemWidth, directoryRect.xMax - nextX - DirectoryHorizontalInset)),
                        DirectoryRowHeight - 1f);
                    if (GUI.Button(itemRect, GUIContent.none, GUIStyle.none))
                        Select(section.Id);

                    if (Event.current.type == EventType.Repaint && i == selectedIndex)
                    {
                        // Keep the directory lightweight, but give the current section a
                        // real surface area. The underline alone disappears in dark themes
                        // and makes the content page feel disconnected from the selection.
                        Color selectedFill = EditorGUIUtility.isProSkin
                            ? new Color(0.18f, 0.32f, 0.46f, 0.34f)
                            : new Color(0.72f, 0.84f, 0.96f, 0.55f);
                        EditorGUI.DrawRect(
                            new Rect(itemRect.x + 1f, itemRect.y + 2f,
                                Mathf.Max(4f, itemRect.width - 2f), itemRect.height - 3f),
                            selectedFill);
                    }

                    GUI.Label(itemRect, section.Content, style);

                    if (Event.current.type == EventType.Repaint && i == selectedIndex)
                    {
                        Color accentColor = EditorGUIUtility.isProSkin
                            ? new Color(0.34f, 0.68f, 0.96f, 1f)
                            : new Color(0.08f, 0.38f, 0.72f, 1f);
                        EditorGUI.DrawRect(
                            new Rect(itemRect.x + SectionHorizontalPadding, itemRect.yMax - 2f,
                                Mathf.Max(4f, itemRect.width - SectionHorizontalPadding * 2f), 3f),
                            accentColor);
                    }

                    nextX = itemRect.xMax + SeparatorWidth;
                    if (i < sections.Count - 1 && nextX < directoryRect.xMax - DirectoryHorizontalInset)
                    {
                        Rect separatorRect = new Rect(
                            itemRect.xMax,
                            itemRect.y,
                            SeparatorWidth,
                            DirectoryRowHeight - 1f);
                        GUI.Label(separatorRect, "·", SeparatorStyle);
                    }
                }
            }

            private int CalculateDirectoryRowCount(float availableWidth)
            {
                float rowWidth = DirectoryHorizontalInset;
                int rows = 1;
                for (int i = 0; i < sections.Count; i++)
                {
                    float itemWidth = SectionStyle.CalcSize(sections[i].Content).x + SectionHorizontalPadding * 2f;
                    if (rowWidth > DirectoryHorizontalInset
                        && rowWidth + itemWidth > availableWidth - DirectoryHorizontalInset)
                    {
                        rows++;
                        rowWidth = DirectoryHorizontalInset;
                    }

                    rowWidth += itemWidth + SeparatorWidth;
                }

                return rows;
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

                    GUI.Label(hitRect, section.HitContent, GUIStyle.none);
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
                    EditorGUI.DrawRect(
                        new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f),
                        ESEditorPresentation.DividerColor);
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
                    Color accentColor = ESEditorPresentation.GetDepthAccent(0);

                    EditorGUI.DrawRect(
                        new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f),
                        ESEditorPresentation.DividerColor);
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

            private string BuildSelectionKey(PropertyTree tree)
            {
                string typeName = tree.TargetType == null ? "Unknown" : tree.TargetType.FullName;
                int targetId = 0;
                if (tree.WeakTargets.Count == 1 && tree.WeakTargets[0] is UnityEngine.Object target)
                    targetId = target.GetInstanceID();

                return SessionKeyPrefix + typeName + "." + targetId + "." + navigatorId;
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

            private static GUIStyle DirectoryCaptionStyle
            {
                get
                {
                    if (directoryCaptionStyle == null)
                    {
                        directoryCaptionStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleLeft,
                            clipping = TextClipping.Clip,
                            fontStyle = FontStyle.Bold,
                            padding = new RectOffset(0, 0, 1, 1)
                        };
                        directoryCaptionStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.58f, 0.62f, 0.68f, 1f)
                            : new Color(0.34f, 0.37f, 0.42f, 1f);
                    }

                    return directoryCaptionStyle;
                }
            }

            private static GUIStyle DirectoryToggleStyle
            {
                get
                {
                    if (directoryToggleStyle == null)
                    {
                        directoryToggleStyle = new GUIStyle(EditorStyles.miniButton)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(5, 5, 1, 1)
                        };
                    }

                    return directoryToggleStyle;
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
                public readonly GUIContent HitContent;

            public SectionDescriptor(string id, string displayName, string subtitle, float order)
            {
                Id = id;
                DisplayName = displayName;
                Subtitle = subtitle;
                Order = order;
                Content = new GUIContent(displayName, subtitle);
                HitContent = new GUIContent(string.Empty, displayName);
            }

            public static int Compare(SectionDescriptor left, SectionDescriptor right)
            {
                int orderCompare = left.Order.CompareTo(right.Order);
                return orderCompare != 0 ? orderCompare : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            }
        }
    }
}
