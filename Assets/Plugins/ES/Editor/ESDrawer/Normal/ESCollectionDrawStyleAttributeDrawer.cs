using System;
using System.Collections.Generic;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Draws an ES-owned SerializeReference collection using either the compact Feel surface or
    /// the ESEditorSection surface. SerializedProperty remains the only data authority; the
    /// drawer keeps only transient foldout and selector state.
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class ESCollectionDrawStyleAttributeDrawer
        : OdinAttributeDrawer<ESCollectionDrawStyleAttribute>
    {
        private const int MaxMultiEditTargets = 10;
        private const float ToolbarHeight = 24f;
        private const float ItemToolbarHeight = 23f;
        private const float SmallButtonWidth = 26f;
        private const float RemoveButtonWidth = 44f;
        private const float AddButtonWidth = 82f;
        private const float DefaultSortButtonWidth = 68f;
        private const int FeelDragControlHint = 0x45F311;

        private bool expandedInitialized;
        private AdvancedDropdownState selectorState;
        private readonly List<Rect> feelHeaderRects = new List<Rect>();
        private int feelDragFrom = -1;
        private int feelDragTo = -1;
        private int feelDragControlId;

        private static GUIStyle itemTitleStyle;
        private static GUIStyle actionButtonStyle;
        private static GUIStyle removeButtonStyle;
        private static GUIStyle emptyStyle;
        private static bool stylesInitialized;
        private static bool stylesProSkin;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            bool useSectionStyle = Attribute.Mode == ESCollectionDrawMode.SectionList;
            if ((Attribute.Mode != ESCollectionDrawMode.FeelList && !useSectionStyle)
                || !TryGetSerializedCollection(out SerializedProperty collectionProperty)
                || !IsManagedReferenceCollection(collectionProperty))
            {
                CallNextDrawer(label);
                return;
            }

            EnsureStyles();
            if (!expandedInitialized)
            {
                Property.State.Expanded = true;
                expandedInitialized = true;
            }

            CollectionState state = ReadCollectionState(collectionProperty);
            EditorGUILayout.BeginVertical(
                useSectionStyle
                    ? ESEditorPresentation.SurfaceStyle
                    : ESEditorPresentation.CompactCollectionBodyStyle);
            try
            {
                if (useSectionStyle)
                    DrawSectionHeader(label, state);
                else
                    DrawFeelHeader(label, state);
                if (!Property.State.Expanded)
                    return;

                if (useSectionStyle)
                    DrawSectionDivider();
                else
                    DrawFeelDivider();
                if (!string.IsNullOrEmpty(state.Notice))
                {
                    EditorGUILayout.HelpBox(
                        state.Notice,
                        state.HasOrderWarning
                            ? MessageType.Warning
                            : state.CanEdit
                                ? MessageType.Info
                                : MessageType.Warning);
                }

                if (state.Count == 0)
                {
                    GUILayout.Label("暂无集合元素", EmptyStyle);
                    return;
                }

                int drawableCount = Mathf.Min(state.Count, Property.Children.Count);
                for (int index = 0; index < drawableCount; index++)
                {
                    ESFeelListEnabledState enabledState = useSectionStyle
                        ? ESFeelListEnabledState.Unavailable
                        : ReadEnabledState(collectionProperty, index);
                    var context = new ESCollectionElementDrawContext(
                        this,
                        Property.Children[index].UnityPropertyPath,
                        index,
                        state.Count,
                        state.CanEdit,
                        enabledState);
                    using (ESCollectionElementDrawScope.Push(context))
                    {
                        if (useSectionStyle)
                            DrawElementToolbar(collectionProperty, index, state);

                        Property.Children[index].Draw(GUIContent.none);
                    }
                    if (index < drawableCount - 1)
                        EditorGUILayout.Space(4f);
                }

                if (drawableCount != state.Count)
                {
                    EditorGUILayout.HelpBox(
                        "集合结构刚刚发生变化，等待 Inspector 在下一帧重建显示。",
                        MessageType.Info);
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawSectionHeader(GUIContent label, CollectionState state)
        {
            string title = label == null || string.IsNullOrWhiteSpace(label.text)
                ? Property.NiceName
                : label.text;
            string subtitle = label == null ? null : label.tooltip;

            Rect headerRect = GUILayoutUtility.GetRect(
                0f,
                ToolbarHeight,
                GUILayout.ExpandWidth(true));
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 20f, headerRect.height);
            Rect addRect = new Rect(
                headerRect.xMax - AddButtonWidth,
                headerRect.y,
                AddButtonWidth,
                headerRect.height);
            float sortWidth = state.HasDefaultOrder ? DefaultSortButtonWidth : 0f;
            Rect sortRect = new Rect(
                addRect.x - sortWidth - (sortWidth > 0f ? 4f : 0f),
                headerRect.y,
                sortWidth,
                headerRect.height);
            Rect countRect = new Rect(
                sortRect.x - 66f,
                headerRect.y,
                60f,
                headerRect.height);
            Rect titleRect = new Rect(
                foldoutRect.xMax,
                headerRect.y,
                Mathf.Max(0f, countRect.x - foldoutRect.xMax - 6f),
                headerRect.height);

            if (GUI.Button(
                    new Rect(headerRect.x, headerRect.y, Mathf.Max(0f, countRect.x - headerRect.x), headerRect.height),
                    GUIContent.none,
                    GUIStyle.none))
            {
                Property.State.Expanded = !Property.State.Expanded;
            }

            GUI.Label(
                foldoutRect,
                Property.State.Expanded ? "▾" : "▸",
                EditorStyles.boldLabel);
            GUI.Label(titleRect, title, ESEditorPresentation.HeaderStyle);
            GUI.Label(
                countRect,
                state.HasMixedSizes ? "数量不一" : state.Count + " 项",
                ESEditorPresentation.MetaStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanEdit;
            if (sortWidth > 0f)
            {
                GUI.enabled = previousEnabled
                              && state.CanEdit
                              && !state.IsDefaultOrderSorted;
                if (GUI.Button(
                        sortRect,
                        new GUIContent("↕ 默认", "整体按 DefaultOrder 稳定重排；相同顺序值保持原相对顺序"),
                        ActionButtonStyle))
                {
                    ExecuteSortAllByDefaultOrder(exitGui: true);
                }
            }

            GUI.enabled = previousEnabled && state.CanEdit;
            if (GUI.Button(
                    addRect,
                    new GUIContent("＋ 添加", "选择具体类型，并按 DefaultOrder 插入合法位置"),
                    ActionButtonStyle))
            {
                OpenAddTypePicker(addRect);
            }
            GUI.enabled = previousEnabled;

            if (!string.IsNullOrWhiteSpace(subtitle))
                GUILayout.Label(subtitle, ESEditorPresentation.SubtitleStyle);
        }

        private void DrawFeelHeader(GUIContent label, CollectionState state)
        {
            string title = label == null || string.IsNullOrWhiteSpace(label.text)
                ? Property.NiceName
                : label.text;
            string subtitle = label == null ? null : label.tooltip;
            float headerHeight = ESEditorPresentation.CompactCollectionHeaderHeight;
            Rect headerRect = GUILayoutUtility.GetRect(
                0f,
                headerHeight,
                GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Color background = ESEditorPresentation.NeutralSelectorBackground;
                background.a *= ESEditorPresentation.IsProSkin ? 0.88f : 0.96f;
                EditorGUI.DrawRect(headerRect, background);
            }

            Rect foldoutRect = new Rect(headerRect.x + 6f, headerRect.y, 18f, headerRect.height);
            Rect addRect = new Rect(
                headerRect.xMax - AddButtonWidth - 4f,
                headerRect.y + 4f,
                AddButtonWidth,
                headerRect.height - 8f);
            float sortWidth = state.HasDefaultOrder ? DefaultSortButtonWidth : 0f;
            Rect sortRect = new Rect(
                addRect.x - sortWidth - (sortWidth > 0f ? 4f : 0f),
                addRect.y,
                sortWidth,
                addRect.height);
            Rect countRect = new Rect(
                sortRect.x - 64f,
                headerRect.y,
                58f,
                headerRect.height);
            Rect titleRect = new Rect(
                foldoutRect.xMax + 2f,
                headerRect.y,
                Mathf.Max(0f, countRect.x - foldoutRect.xMax - 8f),
                headerRect.height);

            if (GUI.Button(
                    new Rect(headerRect.x, headerRect.y, Mathf.Max(0f, countRect.x - headerRect.x), headerRect.height),
                    GUIContent.none,
                    GUIStyle.none))
            {
                Property.State.Expanded = !Property.State.Expanded;
            }

            GUI.Label(
                foldoutRect,
                Property.State.Expanded ? "▾" : "▸",
                EditorStyles.boldLabel);
            GUI.Label(
                titleRect,
                new GUIContent(title, subtitle),
                ESEditorPresentation.CompactCollectionTitleStyle);
            GUI.Label(
                countRect,
                state.HasMixedSizes ? "数量不一" : state.Count + " 项",
                ESEditorPresentation.CompactCollectionMetaStyle);

            bool previousEnabled = GUI.enabled;
            if (sortWidth > 0f)
            {
                GUI.enabled = previousEnabled
                              && state.CanEdit
                              && !state.IsDefaultOrderSorted;
                if (GUI.Button(
                        sortRect,
                        new GUIContent("↕ 默认", "整体按 DefaultOrder 稳定重排；相同顺序值保持原相对顺序"),
                        ActionButtonStyle))
                {
                    ExecuteSortAllByDefaultOrder(exitGui: true);
                }
            }

            GUI.enabled = previousEnabled && state.CanEdit;
            if (GUI.Button(
                    addRect,
                    new GUIContent("＋ 添加", "选择具体类型，并按 DefaultOrder 插入合法位置"),
                    ActionButtonStyle))
            {
                OpenAddTypePicker(addRect);
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawElementToolbar(
            SerializedProperty collectionProperty,
            int index,
            CollectionState state)
        {
            Rect rowRect = GUILayoutUtility.GetRect(
                0f,
                ItemToolbarHeight,
                GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Color background = ESEditorPresentation.NeutralSelectorBackground;
                background.a *= ESEditorPresentation.IsProSkin ? 0.72f : 0.86f;
                EditorGUI.DrawRect(rowRect, background);
                EditorGUI.DrawRect(
                    new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f),
                    ESEditorPresentation.DividerColor);
            }

            float right = rowRect.xMax;
            Rect removeRect = new Rect(
                right - RemoveButtonWidth,
                rowRect.y,
                RemoveButtonWidth,
                rowRect.height);
            right = removeRect.x - 3f;
            Rect downRect = new Rect(
                right - SmallButtonWidth,
                rowRect.y,
                SmallButtonWidth,
                rowRect.height);
            right = downRect.x - 2f;
            Rect upRect = new Rect(
                right - SmallButtonWidth,
                rowRect.y,
                SmallButtonWidth,
                rowRect.height);
            Rect titleRect = new Rect(
                rowRect.x + 6f,
                rowRect.y,
                Mathf.Max(0f, upRect.x - rowRect.x - 10f),
                rowRect.height);

            GUI.Label(titleRect, BuildElementTitle(collectionProperty, index), ItemTitleStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanEdit && index > 0;
            bool moveUp = GUI.Button(
                upRect,
                new GUIContent("↑", "上移当前集合元素"),
                ActionButtonStyle);

            GUI.enabled = previousEnabled && state.CanEdit && index < state.Count - 1;
            bool moveDown = GUI.Button(
                downRect,
                new GUIContent("↓", "下移当前集合元素"),
                ActionButtonStyle);

            GUI.enabled = previousEnabled && state.CanEdit;
            bool remove = GUI.Button(
                removeRect,
                new GUIContent("删除", "真正删除当前 List 元素，可使用 Ctrl+Z 撤销"),
                RemoveButtonStyle);
            GUI.enabled = previousEnabled;

            if (moveUp)
            {
                if (TryMoveElement(index, index - 1, out string moveUpError))
                    ExitAfterStructuralChange();
                ShowOperationError("无法上移集合元素", moveUpError);
            }

            if (moveDown)
            {
                if (TryMoveElement(index, index + 1, out string moveDownError))
                    ExitAfterStructuralChange();
                ShowOperationError("无法下移集合元素", moveDownError);
            }

            if (remove)
            {
                if (TryDeleteElement(index, out string removeError))
                    ExitAfterStructuralChange();
                ShowOperationError("无法删除集合元素", removeError);
            }
        }

        private void OpenAddTypePicker(Rect anchorRect)
        {
            Type elementType = GetCollectionElementType();
            if (elementType == null)
            {
                EditorUtility.DisplayDialog(
                    "无法新增集合元素",
                    "无法解析集合声明的元素基类。",
                    "知道了");
                return;
            }

            ESSearchDropdown.Open(
                anchorRect,
                "添加 " + ESTypeCatalog.GetDisplayName(elementType),
                () => BuildAddTypeEntries(elementType),
                state: selectorState ?? (selectorState = new AdvancedDropdownState()),
                minimumWindowSize: new Vector2(420f, 320f));
        }

        private List<ESSearchDropdown.Entry> BuildAddTypeEntries(Type elementType)
        {
            ESTypeCatalog.Catalog catalog = ESTypeCatalog.Get(elementType);
            var entries = new List<ESSearchDropdown.Entry>(Mathf.Max(1, catalog.Count));
            for (int index = 0; index < catalog.Count; index++)
            {
                ESTypeCatalog.Entry descriptor = catalog.Entries[index];
                Type concreteType = descriptor.Type;
                if (!CanAddConcreteType(concreteType, out string duplicateReason))
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled(
                        descriptor.DisplayName,
                        descriptor.GroupPath,
                        tooltip: duplicateReason));
                    continue;
                }

                entries.Add(ESSearchDropdown.Entry.Item(
                    descriptor.DisplayName,
                    () => AddElement(concreteType),
                    descriptor.GroupPath,
                    subtitle: descriptor.Subtitle,
                    tooltip: descriptor.Tooltip,
                    keywords: descriptor.Keywords,
                    badge: descriptor.Badge));
            }

            if (catalog.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "没有可创建的具体类型",
                    tooltip: "候选类型必须可序列化、非抽象、非泛型，并具有无参构造函数。"));
            }

            return entries;
        }

        private void AddElement(Type concreteType)
        {
            if (TryAddElement(concreteType, out string error))
            {
                Property.State.Expanded = true;
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法新增集合元素", error);
        }

        private bool TryAddElement(Type concreteType, out string error)
        {
            error = null;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                if (!Attribute.AllowDuplicateItems)
                {
                    for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                    {
                        if (ContainsConcreteType(targets[targetIndex].Property, concreteType, -1))
                        {
                            error = "目标 " + (targetIndex + 1) + " 已包含类型 "
                                    + ESTypeCatalog.GetDisplayName(concreteType) + "。";
                            return false;
                        }
                    }
                }

                var values = new List<object>(targets.Count);
                try
                {
                    for (int index = 0; index < targets.Count; index++)
                        values.Add(Activator.CreateInstance(concreteType, nonPublic: true));
                }
                catch (Exception exception)
                {
                    error = exception.GetType().Name + "：" + exception.Message;
                    return false;
                }

                var insertIndexes = new List<int>(targets.Count);
                for (int index = 0; index < targets.Count; index++)
                {
                    int insertIndex = targets[index].Property.arraySize;
                    if (Attribute.EnforceDefaultOrder
                        && !TryFindDefaultInsertIndex(
                            targets[index].Property,
                            values[index],
                            out insertIndex,
                            out error))
                    {
                        return false;
                    }

                    insertIndexes.Add(insertIndex);
                }

                string undoName = "添加 " + ESTypeCatalog.GetDisplayName(concreteType);
                return TryMutateTargets(
                    targets,
                    undoName,
                    (target, targetIndex) =>
                    {
                        int insertIndex = target.Property.arraySize;
                        target.Property.arraySize = insertIndex + 1;
                        SerializedProperty element = target.Property.GetArrayElementAtIndex(insertIndex);
                        if (element == null
                            || element.propertyType != SerializedPropertyType.ManagedReference)
                        {
                            throw new InvalidOperationException("新增位置不是可写的 SerializeReference 元素。");
                        }

                        element.managedReferenceValue = values[targetIndex];
                        int destination = insertIndexes[targetIndex];
                        if (destination != insertIndex
                            && !target.Property.MoveArrayElement(insertIndex, destination))
                        {
                            throw new InvalidOperationException("Unity 拒绝把新增元素放入默认顺序位置。");
                        }
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TryDeleteElement(int index, out string error)
        {
            error = null;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    if (index < 0 || index >= targets[targetIndex].Property.arraySize)
                    {
                        error = "目标 " + (targetIndex + 1) + " 不包含索引 " + index + "。";
                        return false;
                    }
                }

                return TryMutateTargets(
                    targets,
                    "删除集合元素",
                    (target, _) =>
                    {
                        int previousSize = target.Property.arraySize;
                        target.Property.DeleteArrayElementAtIndex(index);
                        if (target.Property.arraySize == previousSize)
                            target.Property.DeleteArrayElementAtIndex(index);
                        if (target.Property.arraySize != previousSize - 1)
                            throw new InvalidOperationException("Unity 没有真正移除目标 List 元素。");
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TryMoveElement(int from, int to, out string error)
        {
            error = null;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    int size = targets[targetIndex].Property.arraySize;
                    if (from < 0 || from >= size || to < 0 || to >= size)
                    {
                        error = "目标 " + (targetIndex + 1) + " 无法执行该重排。";
                        return false;
                    }

                    if (Attribute.EnforceDefaultOrder
                        && !CanMoveWithoutBreakingDefaultOrder(
                            targets[targetIndex].Property,
                            from,
                            to,
                            out error))
                    {
                        return false;
                    }
                }

                return TryMutateTargets(
                    targets,
                    "重排集合元素",
                    (target, _) =>
                    {
                        if (!target.Property.MoveArrayElement(from, to))
                            throw new InvalidOperationException("Unity 拒绝移动目标 List 元素。");
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TryDuplicateElement(int index, out string error)
        {
            error = null;
            if (!Attribute.AllowDuplicateItems)
            {
                error = "当前集合禁止复制元素，以免产生重复稳定身份。";
                return false;
            }

            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                var copies = new List<object>(targets.Count);
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    SerializedProperty property = targets[targetIndex].Property;
                    if (index < 0 || index >= property.arraySize)
                    {
                        error = "目标 " + (targetIndex + 1) + " 不包含索引 " + index + "。";
                        return false;
                    }

                    SerializedProperty source = property.GetArrayElementAtIndex(index);
                    object sourceValue = source?.managedReferenceValue;
                    if (sourceValue == null)
                    {
                        error = "缺失类型或空元素不能复制；请先恢复其具体类型。";
                        return false;
                    }

                    try
                    {
                        copies.Add(Sirenix.Serialization.SerializationUtility.CreateCopy(sourceValue));
                    }
                    catch (Exception exception)
                    {
                        error = exception.GetType().Name + "：" + exception.Message;
                        return false;
                    }
                }

                return TryMutateTargets(
                    targets,
                    "复制集合元素",
                    (target, targetIndex) =>
                    {
                        int appendIndex = target.Property.arraySize;
                        target.Property.arraySize = appendIndex + 1;
                        SerializedProperty duplicate = target.Property.GetArrayElementAtIndex(appendIndex);
                        if (duplicate == null
                            || duplicate.propertyType != SerializedPropertyType.ManagedReference)
                        {
                            throw new InvalidOperationException("复制位置不是可写的 SerializeReference 元素。");
                        }

                        duplicate.managedReferenceValue = copies[targetIndex];
                        int destination = Mathf.Min(index + 1, appendIndex);
                        if (destination != appendIndex
                            && !target.Property.MoveArrayElement(appendIndex, destination))
                        {
                            throw new InvalidOperationException("Unity 拒绝移动复制后的集合元素。");
                        }
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TrySetElementEnabled(int index, bool enabled, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(Attribute.EnabledMemberName))
            {
                error = "当前集合没有声明启用字段。";
                return false;
            }

            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    SerializedProperty collection = targets[targetIndex].Property;
                    if (index < 0 || index >= collection.arraySize)
                    {
                        error = "目标 " + (targetIndex + 1) + " 不包含索引 " + index + "。";
                        return false;
                    }

                    SerializedProperty enabledProperty = collection
                        .GetArrayElementAtIndex(index)
                        ?.FindPropertyRelative(Attribute.EnabledMemberName);
                    if (enabledProperty == null
                        || enabledProperty.propertyType != SerializedPropertyType.Boolean)
                    {
                        error = "元素没有可写的 bool 字段“" + Attribute.EnabledMemberName + "”。";
                        return false;
                    }
                }

                return TryMutateTargets(
                    targets,
                    enabled ? "启用集合元素" : "停用集合元素",
                    (target, _) =>
                    {
                        SerializedProperty enabledProperty = target.Property
                            .GetArrayElementAtIndex(index)
                            .FindPropertyRelative(Attribute.EnabledMemberName);
                        enabledProperty.boolValue = enabled;
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TryRestoreElementDefaultOrder(int index, out string error)
        {
            error = null;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                var destinations = new List<int>(targets.Count);
                bool hasChange = false;
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    SerializedProperty property = targets[targetIndex].Property;
                    if (index < 0 || index >= property.arraySize)
                    {
                        error = "目标 " + (targetIndex + 1) + " 不包含索引 " + index + "。";
                        return false;
                    }

                    if (!TryBuildStableDefaultOrderPlan(property, out List<int> plan, out error))
                        return false;
                    int destination = plan.IndexOf(index);
                    if (destination < 0)
                    {
                        error = "无法定位当前元素的默认顺序位置。";
                        return false;
                    }

                    destinations.Add(destination);
                    hasChange |= destination != index;
                }

                if (!hasChange)
                    return true;

                return TryMutateTargets(
                    targets,
                    "按默认顺序归位集合元素",
                    (target, targetIndex) =>
                    {
                        int destination = destinations[targetIndex];
                        if (destination != index
                            && !target.Property.MoveArrayElement(index, destination))
                        {
                            throw new InvalidOperationException("Unity 拒绝归位当前集合元素。");
                        }
                    },
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TrySortAllByDefaultOrder(out string error)
        {
            error = null;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out error))
                return false;

            try
            {
                var plans = new List<List<int>>(targets.Count);
                bool hasChange = false;
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    if (!TryBuildStableDefaultOrderPlan(
                            targets[targetIndex].Property,
                            out List<int> plan,
                            out error))
                    {
                        return false;
                    }

                    plans.Add(plan);
                    for (int index = 0; index < plan.Count; index++)
                        hasChange |= plan[index] != index;
                }

                if (!hasChange)
                    return true;

                return TryMutateTargets(
                    targets,
                    "按 DefaultOrder 整理集合",
                    (target, targetIndex) => ApplyStableOrderPlan(target.Property, plans[targetIndex]),
                    out error);
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private bool TryFindDefaultInsertIndex(
            SerializedProperty collection,
            object newValue,
            out int insertIndex,
            out string error)
        {
            insertIndex = collection?.arraySize ?? 0;
            error = null;
            if (!(newValue is IESCollectionDefaultOrder orderedValue))
            {
                error = "新增类型没有实现 IESCollectionDefaultOrder，无法进入受控排序集合。";
                return false;
            }

            int previousOrder = int.MinValue;
            for (int index = 0; index < collection.arraySize; index++)
            {
                if (!TryReadDefaultOrder(collection, index, out int currentOrder, out error))
                    return false;
                if (currentOrder < previousOrder)
                {
                    error = "现有集合已经偏离 DefaultOrder；请先执行整体整理，再新增元素。";
                    return false;
                }
                if (currentOrder > orderedValue.DefaultOrder)
                {
                    insertIndex = index;
                    break;
                }

                previousOrder = currentOrder;
            }

            return true;
        }

        private static bool CanMoveWithoutBreakingDefaultOrder(
            SerializedProperty collection,
            int from,
            int to,
            out string error)
        {
            error = null;
            if (!TryReadDefaultOrders(collection, out List<int> orders, out error))
                return false;
            if (from == to)
                return true;

            int moved = orders[from];
            orders.RemoveAt(from);
            orders.Insert(to, moved);
            for (int index = 1; index < orders.Count; index++)
            {
                if (orders[index] >= orders[index - 1])
                    continue;

                error = "该移动会破坏 DefaultOrder 升序。请在相同 DefaultOrder 内重排，"
                        + "或使用“按默认顺序归位/整体整理”。";
                return false;
            }

            return true;
        }

        private static bool TryBuildStableDefaultOrderPlan(
            SerializedProperty collection,
            out List<int> plan,
            out string error)
        {
            plan = null;
            if (!TryReadDefaultOrders(collection, out List<int> orders, out error))
                return false;

            plan = new List<int>(orders.Count);
            for (int index = 0; index < orders.Count; index++)
                plan.Add(index);
            plan.Sort((left, right) =>
            {
                int comparison = orders[left].CompareTo(orders[right]);
                return comparison != 0 ? comparison : left.CompareTo(right);
            });
            return true;
        }

        private static void ApplyStableOrderPlan(
            SerializedProperty collection,
            List<int> desiredOriginalIndexes)
        {
            var currentOriginalIndexes = new List<int>(desiredOriginalIndexes.Count);
            for (int index = 0; index < desiredOriginalIndexes.Count; index++)
                currentOriginalIndexes.Add(index);

            for (int destination = 0; destination < desiredOriginalIndexes.Count; destination++)
            {
                int wantedOriginalIndex = desiredOriginalIndexes[destination];
                int currentIndex = currentOriginalIndexes.IndexOf(wantedOriginalIndex);
                if (currentIndex == destination)
                    continue;
                if (!collection.MoveArrayElement(currentIndex, destination))
                    throw new InvalidOperationException("Unity 拒绝执行 DefaultOrder 稳定重排。");

                currentOriginalIndexes.RemoveAt(currentIndex);
                currentOriginalIndexes.Insert(destination, wantedOriginalIndex);
            }
        }

        private static bool TryReadDefaultOrders(
            SerializedProperty collection,
            out List<int> orders,
            out string error)
        {
            orders = new List<int>(collection?.arraySize ?? 0);
            error = null;
            if (collection == null)
            {
                error = "集合序列化属性不存在。";
                return false;
            }

            for (int index = 0; index < collection.arraySize; index++)
            {
                if (!TryReadDefaultOrder(collection, index, out int order, out error))
                    return false;
                orders.Add(order);
            }

            return true;
        }

        private static bool TryReadDefaultOrder(
            SerializedProperty collection,
            int index,
            out int order,
            out string error)
        {
            order = 0;
            error = null;
            SerializedProperty element = collection.GetArrayElementAtIndex(index);
            object value = element?.managedReferenceValue;
            if (value is IESCollectionDefaultOrder ordered)
            {
                order = ordered.DefaultOrder;
                return true;
            }

            error = "索引 " + index + " 的元素为空、类型缺失，或没有实现 IESCollectionDefaultOrder。";
            return false;
        }

        private bool TryMutateTargets(
            List<CollectionTarget> targets,
            string undoName,
            Action<CollectionTarget, int> mutation,
            out string error)
        {
            try
            {
                bool changed = ESEditorSerializedMutation.TryApply(
                    targets,
                    undoName,
                    target => target.Target,
                    target => target.SerializedObject,
                    mutation,
                    RefreshSerializedTree,
                    out error);
                if (changed)
                    GUI.changed = true;
                return changed;
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        internal bool AllowDuplicateItems => Attribute.AllowDuplicateItems;

        internal bool CanUseConcreteTypeAtIndex(int index, Type concreteType, out string reason)
        {
            reason = null;
            if (Attribute.AllowDuplicateItems)
                return true;
            if (!TryCollectTargets(out List<CollectionTarget> targets, out reason))
                return false;

            try
            {
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    if (!ContainsConcreteType(targets[targetIndex].Property, concreteType, index))
                        continue;
                    reason = "当前集合禁止重复类型；另一个元素已经使用 "
                             + ESTypeCatalog.GetDisplayName(concreteType) + "。";
                    return false;
                }

                return true;
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        internal void ExecuteSetElementEnabled(int index, bool enabled)
        {
            if (!TrySetElementEnabled(index, enabled, out string error))
                ShowOperationError("无法修改元素启用状态", error);
        }

        internal void ExecuteDuplicateElement(int index, bool exitGui)
        {
            if (TryDuplicateElement(index, out string error))
            {
                if (exitGui)
                    ExitAfterStructuralChange();
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法复制集合元素", error);
        }

        internal void ExecuteDeleteElement(int index, bool exitGui)
        {
            if (TryDeleteElement(index, out string error))
            {
                if (exitGui)
                    ExitAfterStructuralChange();
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法删除集合元素", error);
        }

        internal void ShowFeelElementMenu(int index, Rect anchorRect)
        {
            var menu = new GenericMenu();
            if (Attribute.AllowDuplicateItems)
            {
                menu.AddItem(
                    new GUIContent("复制元素"),
                    false,
                    () => ExecuteDuplicateElement(index, exitGui: false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("复制元素（集合禁止重复）"));
            }

            menu.AddSeparator(string.Empty);
            if (TryGetRepresentativeDefaultOrder(index, out int defaultOrder))
            {
                menu.AddDisabledItem(new GUIContent("DefaultOrder / " + defaultOrder));
                menu.AddItem(
                    new GUIContent("按 DefaultOrder 归位当前项"),
                    false,
                    () => ExecuteRestoreElementDefaultOrder(index, exitGui: false));
                menu.AddItem(
                    new GUIContent("整体按 DefaultOrder 整理"),
                    false,
                    () => ExecuteSortAllByDefaultOrder(exitGui: false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("DefaultOrder / 不可用"));
                menu.AddDisabledItem(new GUIContent("按 DefaultOrder 归位当前项"));
                menu.AddDisabledItem(new GUIContent("整体按 DefaultOrder 整理"));
            }

            menu.AddSeparator(string.Empty);
            AddMoveMenuItem(
                menu,
                "移到顶部",
                index,
                0,
                index > 0 && CanMoveRepresentative(index, 0));
            AddMoveMenuItem(
                menu,
                "上移",
                index,
                index - 1,
                index > 0 && CanMoveRepresentative(index, index - 1));
            AddMoveMenuItem(
                menu,
                "下移",
                index,
                index + 1,
                index + 1 < GetCurrentCollectionCount()
                && CanMoveRepresentative(index, index + 1));
            AddMoveMenuItem(
                menu,
                "移到底部",
                index,
                Mathf.Max(0, GetCurrentCollectionCount() - 1),
                index + 1 < GetCurrentCollectionCount()
                && CanMoveRepresentative(index, Mathf.Max(0, GetCurrentCollectionCount() - 1)));

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("删除元素"),
                false,
                () => ExecuteDeleteElement(index, exitGui: false));
            menu.DropDown(anchorRect);
        }

        internal void ProcessFeelDragHandle(
            int index,
            Rect handleRect,
            Rect headerRect,
            bool canEdit)
        {
            EnsureFeelHeaderRectCapacity(index + 1);
            if (Event.current.type == EventType.Repaint)
            {
                feelHeaderRects[index] = headerRect;
                if (feelDragFrom >= 0 && feelDragTo == index)
                {
                    Color line = GetStableTypeColor(GetRepresentativeTypeIdentity(index));
                    float y = feelDragTo < feelDragFrom ? headerRect.y : headerRect.yMax - 2f;
                    EditorGUI.DrawRect(new Rect(headerRect.x + 2f, y, headerRect.width - 4f, 2f), line);
                }
            }

            Event current = Event.current;
            if (feelDragFrom >= 0
                && ((current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
                    || current.type == EventType.Ignore))
            {
                ReleaseFeelDrag();
                current.Use();
                return;
            }

            int controlId = GUIUtility.GetControlID(
                FeelDragControlHint + index,
                FocusType.Passive,
                handleRect);
            if (current.type == EventType.MouseDown
                && current.button == 0
                && canEdit
                && handleRect.Contains(current.mousePosition))
            {
                feelDragFrom = index;
                feelDragTo = index;
                feelDragControlId = controlId;
                GUIUtility.hotControl = controlId;
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != feelDragControlId
                || feelDragFrom < 0
                || controlId != feelDragControlId)
            {
                return;
            }

            if (current.type == EventType.MouseDrag)
            {
                int candidate = FindFeelDropIndex(current.mousePosition.y);
                if (CanMoveRepresentative(feelDragFrom, candidate))
                    feelDragTo = candidate;
                GUI.changed = true;
                current.Use();
                return;
            }

            if (current.type != EventType.MouseUp || current.button != 0)
                return;

            int from = feelDragFrom;
            int to = feelDragTo;
            ReleaseFeelDrag();
            current.Use();
            if (from == to)
                return;

            if (TryMoveElement(from, to, out string error))
                ExitAfterStructuralChange();
            ShowOperationError("无法拖拽重排集合元素", error);
        }

        internal static Color GetStableTypeColor(string identity)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = string.IsNullOrEmpty(identity) ? "<missing>" : identity;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                float hue = (hash % 360u) / 360f;
                float saturation = ESEditorPresentation.IsProSkin ? 0.62f : 0.68f;
                float brightness = ESEditorPresentation.IsProSkin ? 0.92f : 0.70f;
                Color color = Color.HSVToRGB(hue, saturation, brightness);
                color.a = 1f;
                return color;
            }
        }

        private void AddMoveMenuItem(
            GenericMenu menu,
            string label,
            int from,
            int to,
            bool enabled)
        {
            if (enabled)
            {
                menu.AddItem(
                    new GUIContent(label),
                    false,
                    () => ExecuteMoveElement(from, to, exitGui: false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(label));
            }
        }

        private bool CanAddConcreteType(Type concreteType, out string reason)
        {
            reason = null;
            if (Attribute.AllowDuplicateItems)
                return true;
            if (!TryGetSerializedCollection(out SerializedProperty collection))
                return true;
            if (!ContainsConcreteType(collection, concreteType, -1))
                return true;

            reason = "当前集合禁止重复类型；该类型已经存在。";
            return false;
        }

        private bool CanMoveRepresentative(int from, int to)
        {
            if (from == to || !Attribute.EnforceDefaultOrder)
                return true;
            return TryGetSerializedCollection(out SerializedProperty collection)
                   && from >= 0
                   && from < collection.arraySize
                   && to >= 0
                   && to < collection.arraySize
                   && CanMoveWithoutBreakingDefaultOrder(collection, from, to, out _);
        }

        private static bool ContainsConcreteType(
            SerializedProperty collection,
            Type concreteType,
            int ignoredIndex)
        {
            if (collection == null || concreteType == null)
                return false;
            for (int index = 0; index < collection.arraySize; index++)
            {
                if (index == ignoredIndex)
                    continue;
                object value = collection.GetArrayElementAtIndex(index)?.managedReferenceValue;
                if (value != null && value.GetType() == concreteType)
                    return true;
            }

            return false;
        }

        private void ExecuteMoveElement(int from, int to, bool exitGui)
        {
            if (TryMoveElement(from, to, out string error))
            {
                if (exitGui)
                    ExitAfterStructuralChange();
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法重排集合元素", error);
        }

        internal void ExecuteRestoreElementDefaultOrder(int index, bool exitGui)
        {
            if (TryRestoreElementDefaultOrder(index, out string error))
            {
                if (exitGui)
                    ExitAfterStructuralChange();
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法按 DefaultOrder 归位", error);
        }

        private void ExecuteSortAllByDefaultOrder(bool exitGui)
        {
            if (TrySortAllByDefaultOrder(out string error))
            {
                if (exitGui)
                    ExitAfterStructuralChange();
                GUI.changed = true;
                return;
            }

            ShowOperationError("无法按 DefaultOrder 整理集合", error);
        }

        private bool TryGetRepresentativeDefaultOrder(int index, out int order)
        {
            order = 0;
            return TryGetSerializedCollection(out SerializedProperty collection)
                   && index >= 0
                   && index < collection.arraySize
                   && TryReadDefaultOrder(collection, index, out order, out _);
        }

        private string GetRepresentativeTypeIdentity(int index)
        {
            if (!TryGetSerializedCollection(out SerializedProperty collection)
                || index < 0
                || index >= collection.arraySize)
            {
                return null;
            }

            SerializedProperty element = collection.GetArrayElementAtIndex(index);
            object value = element?.managedReferenceValue;
            return value?.GetType().FullName ?? element?.managedReferenceFullTypename;
        }

        private int GetCurrentCollectionCount()
        {
            return TryGetSerializedCollection(out SerializedProperty collection)
                ? collection.arraySize
                : 0;
        }

        private int FindFeelDropIndex(float mouseY)
        {
            int count = Mathf.Min(GetCurrentCollectionCount(), feelHeaderRects.Count);
            if (count <= 0)
                return feelDragFrom;
            for (int index = 0; index < count; index++)
            {
                Rect rect = feelHeaderRects[index];
                if (rect.height > 0f && mouseY < rect.center.y)
                    return index;
            }

            return count - 1;
        }

        private void EnsureFeelHeaderRectCapacity(int capacity)
        {
            while (feelHeaderRects.Count < capacity)
                feelHeaderRects.Add(Rect.zero);
        }

        private void ReleaseFeelDrag()
        {
            if (GUIUtility.hotControl == feelDragControlId)
                GUIUtility.hotControl = 0;
            feelDragFrom = -1;
            feelDragTo = -1;
            feelDragControlId = 0;
            GUI.changed = true;
        }

        private bool TryCollectTargets(
            out List<CollectionTarget> targets,
            out string error)
        {
            targets = new List<CollectionTarget>();
            error = null;
            if (Property.Tree == null
                || Property.Tree.WeakTargets == null
                || Property.Tree.WeakTargets.Count == 0
                || string.IsNullOrEmpty(Property.UnityPropertyPath))
            {
                error = "当前集合没有可写入的 Unity 序列化目标。";
                return false;
            }

            if (Property.Tree.WeakTargets.Count > MaxMultiEditTargets)
            {
                error = "批量编辑最多支持 " + MaxMultiEditTargets + " 个对象。";
                return false;
            }

            for (int index = 0; index < Property.Tree.WeakTargets.Count; index++)
            {
                if (!(Property.Tree.WeakTargets[index] is UnityEngine.Object target) || target == null)
                {
                    error = "目标 " + (index + 1) + " 不是仍然有效的 Unity 对象。";
                    DisposeCollectionTargets(targets);
                    return false;
                }

                SerializedObject serializedObject;
                try
                {
                    serializedObject = new SerializedObject(target);
                    serializedObject.UpdateIfRequiredOrScript();
                }
                catch (Exception exception)
                {
                    error = "目标 " + (index + 1) + " 无法建立序列化视图：" + exception.Message;
                    DisposeCollectionTargets(targets);
                    return false;
                }

                SerializedProperty property;
                try
                {
                    property = serializedObject.FindProperty(Property.UnityPropertyPath);
                }
                catch (Exception exception)
                {
                    error = "目标 " + (index + 1) + " 查找集合属性失败：" + exception.Message;
                    serializedObject.Dispose();
                    DisposeCollectionTargets(targets);
                    return false;
                }
                if (property == null || !property.isArray || !IsManagedReferenceCollection(property))
                {
                    error = "目标 " + (index + 1) + " 没有兼容的 SerializeReference List。";
                    serializedObject.Dispose();
                    DisposeCollectionTargets(targets);
                    return false;
                }

                targets.Add(new CollectionTarget(target, serializedObject, property));
            }

            return true;
        }

        private static void DisposeCollectionTargets(IReadOnlyList<CollectionTarget> targets)
        {
            if (targets == null)
                return;
            for (int index = 0; index < targets.Count; index++)
            {
                try
                {
                    targets[index].SerializedObject?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "集合批量编辑序列化视图释放失败。", exception));
                }
            }
            if (targets is List<CollectionTarget> list)
                list.Clear();
        }

        private CollectionState ReadCollectionState(SerializedProperty collectionProperty)
        {
            int targetCount = Property.Tree?.WeakTargets?.Count ?? 0;
            if (targetCount <= 1)
            {
                ReadDefaultOrderState(
                    collectionProperty,
                    out bool hasDefaultOrder,
                    out bool isDefaultOrderSorted,
                    out string orderNotice);
                return new CollectionState(
                    collectionProperty.arraySize,
                    true,
                    false,
                    orderNotice,
                    hasDefaultOrder,
                    isDefaultOrderSorted,
                    !string.IsNullOrEmpty(orderNotice));
            }

            if (targetCount > MaxMultiEditTargets)
            {
                return new CollectionState(
                    collectionProperty.arraySize,
                    false,
                    false,
                    "已选择 " + targetCount + " 个对象；超过安全批量上限，集合保持只读。",
                    false,
                    true,
                    false);
            }

            if (!TryCollectTargets(out List<CollectionTarget> targets, out string error))
                return new CollectionState(
                    collectionProperty.arraySize,
                    false,
                    false,
                    error,
                    false,
                    true,
                    false);

            try
            {
                int commonSize = targets[0].Property.arraySize;
                for (int index = 1; index < targets.Count; index++)
                {
                    if (targets[index].Property.arraySize != commonSize)
                    {
                        return new CollectionState(
                            Mathf.Min(commonSize, collectionProperty.arraySize),
                            false,
                            true,
                            "多个目标的集合长度不一致。请单独选择对象后再新增、删除或重排，避免覆盖不同配置。",
                            false,
                            true,
                            false);
                    }
                }

                bool allSupportDefaultOrder = Attribute.EnforceDefaultOrder && commonSize > 0;
                bool allDefaultOrderSorted = true;
                string multiOrderNotice = null;
                for (int index = 0; index < targets.Count && Attribute.EnforceDefaultOrder; index++)
                {
                    ReadDefaultOrderState(
                        targets[index].Property,
                        out bool supportsDefaultOrder,
                        out bool isSorted,
                        out string targetNotice);
                    allSupportDefaultOrder &= supportsDefaultOrder;
                    allDefaultOrderSorted &= isSorted;
                    if (multiOrderNotice == null && !string.IsNullOrEmpty(targetNotice))
                        multiOrderNotice = targetNotice;
                }

                return new CollectionState(
                    commonSize,
                    true,
                    false,
                    multiOrderNotice,
                    allSupportDefaultOrder,
                    allDefaultOrderSorted,
                    !string.IsNullOrEmpty(multiOrderNotice));
            }
            finally
            {
                DisposeCollectionTargets(targets);
            }
        }

        private void ReadDefaultOrderState(
            SerializedProperty collection,
            out bool supported,
            out bool sorted,
            out string notice)
        {
            supported = false;
            sorted = true;
            notice = null;
            if (!Attribute.EnforceDefaultOrder || collection == null || collection.arraySize == 0)
                return;

            int previousOrder = int.MinValue;
            for (int index = 0; index < collection.arraySize; index++)
            {
                if (!TryReadDefaultOrder(collection, index, out int currentOrder, out string error))
                {
                    notice = "DefaultOrder 受控排序暂不可用：" + error;
                    return;
                }

                supported = true;
                if (currentOrder >= previousOrder)
                {
                    previousOrder = currentOrder;
                    continue;
                }
                sorted = false;
                notice = "当前集合顺序偏离 DefaultOrder；Bake 会拒绝该配置。可单项归位或整体整理。";
                return;
            }
        }

        private ESFeelListEnabledState ReadEnabledState(
            SerializedProperty collection,
            int index)
        {
            if (collection == null
                || index < 0
                || index >= collection.arraySize
                || string.IsNullOrEmpty(Attribute.EnabledMemberName))
            {
                return ESFeelListEnabledState.Unavailable;
            }

            SerializedProperty enabledProperty = collection
                .GetArrayElementAtIndex(index)
                ?.FindPropertyRelative(Attribute.EnabledMemberName);
            if (enabledProperty == null
                || enabledProperty.propertyType != SerializedPropertyType.Boolean)
            {
                return ESFeelListEnabledState.Unavailable;
            }

            return new ESFeelListEnabledState(
                available: true,
                value: enabledProperty.boolValue,
                mixed: enabledProperty.hasMultipleDifferentValues);
        }

        private bool TryGetSerializedCollection(out SerializedProperty property)
        {
            property = null;
            if (Property.Tree?.UnitySerializedObject == null
                || string.IsNullOrEmpty(Property.UnityPropertyPath))
                return false;

            property = Property.Tree.UnitySerializedObject.FindProperty(Property.UnityPropertyPath);
            return property != null && property.isArray;
        }

        private bool IsManagedReferenceCollection(SerializedProperty property)
        {
            if (Property.GetAttribute<SerializeReference>() != null)
                return true;

            if (property == null || !property.isArray)
                return false;

            if (!string.IsNullOrEmpty(property.arrayElementType)
                && property.arrayElementType.IndexOf(
                    "managedReference",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (property.arraySize == 0)
                return false;

            SerializedProperty element = property.GetArrayElementAtIndex(0);
            return element != null
                   && element.propertyType == SerializedPropertyType.ManagedReference;
        }

        private Type GetCollectionElementType()
        {
            Type collectionType = Property.Info?.TypeOfValue
                                  ?? Property.BaseValueEntry?.TypeOfValue;
            if (collectionType == null)
                return null;
            if (collectionType.IsArray)
                return collectionType.GetElementType();
            if (collectionType.IsGenericType
                && collectionType.GetGenericArguments().Length == 1)
                return collectionType.GetGenericArguments()[0];

            Type[] interfaces = collectionType.GetInterfaces();
            for (int index = 0; index < interfaces.Length; index++)
            {
                Type candidate = interfaces[index];
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IList<>))
                    return candidate.GetGenericArguments()[0];
            }

            return null;
        }

        private string BuildElementTitle(SerializedProperty collectionProperty, int index)
        {
            string prefix = (index + 1).ToString("00") + "  ·  ";
            if (index < Property.Children.Count)
            {
                object value = Property.Children[index].ValueEntry?.WeakSmartValue;
                if (value != null)
                {
                    if (value is IESNameTitle named)
                    {
                        string nameTitle = named.NameTitle;
                        if (!string.IsNullOrWhiteSpace(nameTitle))
                            return prefix + nameTitle;
                    }

                    return prefix + ESTypeCatalog.GetDisplayName(value.GetType());
                }
            }

            if (collectionProperty != null && index < collectionProperty.arraySize)
            {
                SerializedProperty element = collectionProperty.GetArrayElementAtIndex(index);
                if (element != null
                    && element.propertyType == SerializedPropertyType.ManagedReference
                    && !string.IsNullOrEmpty(element.managedReferenceFullTypename))
                    return prefix + "类型缺失";
            }

            return prefix + "空元素";
        }

        private void RefreshSerializedTree()
        {
            if (Property.Tree?.UnitySerializedObject != null)
                Property.Tree.UnitySerializedObject.UpdateIfRequiredOrScript();
        }

        private static void ExitAfterStructuralChange()
        {
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private static void ShowOperationError(string title, string error)
        {
            EditorUtility.DisplayDialog(
                title,
                string.IsNullOrEmpty(error) ? "发生未知序列化错误。" : error,
                "知道了");
        }

        private static void DrawSectionDivider()
        {
            Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                ESEditorPresentation.DrawDivider(dividerRect);
            EditorGUILayout.Space(3f);
        }

        private static void DrawFeelDivider()
        {
            Rect dividerRect = GUILayoutUtility.GetRect(0f, 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(dividerRect, ESEditorPresentation.GetDepthAccent(0));
            EditorGUILayout.Space(2f);
        }

        private static GUIStyle ItemTitleStyle
        {
            get
            {
                EnsureStyles();
                return itemTitleStyle;
            }
        }

        private static GUIStyle ActionButtonStyle
        {
            get
            {
                EnsureStyles();
                return actionButtonStyle;
            }
        }

        private static GUIStyle RemoveButtonStyle
        {
            get
            {
                EnsureStyles();
                return removeButtonStyle;
            }
        }

        private static GUIStyle EmptyStyle
        {
            get
            {
                EnsureStyles();
                return emptyStyle;
            }
        }

        private static void EnsureStyles()
        {
            bool proSkin = ESEditorPresentation.IsProSkin;
            if (stylesInitialized && stylesProSkin == proSkin)
                return;

            stylesInitialized = true;
            stylesProSkin = proSkin;
            itemTitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 4, 1, 1)
            };
            itemTitleStyle.normal.textColor = ESEditorPresentation.SectionSelectedTextColor;

            actionButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 1, 1)
            };
            removeButtonStyle = new GUIStyle(actionButtonStyle);
            removeButtonStyle.normal.textColor = ESEditorPresentation.ClearActionColor;
            removeButtonStyle.hover.textColor = ESEditorPresentation.ClearActionColor;
            removeButtonStyle.active.textColor = ESEditorPresentation.ClearActionColor;
            removeButtonStyle.focused.textColor = ESEditorPresentation.ClearActionColor;

            emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 12, 12)
            };
        }

        private readonly struct CollectionState
        {
            public readonly int Count;
            public readonly bool CanEdit;
            public readonly bool HasMixedSizes;
            public readonly string Notice;
            public readonly bool HasDefaultOrder;
            public readonly bool IsDefaultOrderSorted;
            public readonly bool HasOrderWarning;

            public CollectionState(
                int count,
                bool canEdit,
                bool hasMixedSizes,
                string notice,
                bool hasDefaultOrder,
                bool isDefaultOrderSorted,
                bool hasOrderWarning)
            {
                Count = Mathf.Max(0, count);
                CanEdit = canEdit;
                HasMixedSizes = hasMixedSizes;
                Notice = notice;
                HasDefaultOrder = hasDefaultOrder;
                IsDefaultOrderSorted = isDefaultOrderSorted;
                HasOrderWarning = hasOrderWarning;
            }
        }

        private readonly struct CollectionTarget
        {
            public readonly UnityEngine.Object Target;
            public readonly SerializedObject SerializedObject;
            public readonly SerializedProperty Property;

            public CollectionTarget(
                UnityEngine.Object target,
                SerializedObject serializedObject,
                SerializedProperty property)
            {
                Target = target;
                SerializedObject = serializedObject;
                Property = property;
            }
        }
    }

    internal readonly struct ESFeelListEnabledState
    {
        public static readonly ESFeelListEnabledState Unavailable =
            new ESFeelListEnabledState(false, false, false);

        public readonly bool Available;
        public readonly bool Value;
        public readonly bool Mixed;

        public ESFeelListEnabledState(bool available, bool value, bool mixed)
        {
            Available = available;
            Value = value;
            Mixed = mixed;
        }
    }

    internal readonly struct ESCollectionElementDrawContext
    {
        public readonly ESCollectionDrawStyleAttributeDrawer Owner;
        public readonly string ElementPropertyPath;
        public readonly int Index;
        public readonly int Count;
        public readonly bool CanEdit;
        public readonly ESFeelListEnabledState Enabled;

        public ESCollectionElementDrawContext(
            ESCollectionDrawStyleAttributeDrawer owner,
            string elementPropertyPath,
            int index,
            int count,
            bool canEdit,
            ESFeelListEnabledState enabled)
        {
            Owner = owner;
            ElementPropertyPath = elementPropertyPath;
            Index = index;
            Count = count;
            CanEdit = canEdit;
            Enabled = enabled;
        }
    }

    internal readonly struct ESCollectionElementDrawScope : IDisposable
    {
        private static bool hasCurrent;
        private static ESCollectionElementDrawContext current;

        private readonly bool previousHasCurrent;
        private readonly ESCollectionElementDrawContext previousCurrent;

        private ESCollectionElementDrawScope(
            bool previousHasCurrent,
            ESCollectionElementDrawContext previousCurrent)
        {
            this.previousHasCurrent = previousHasCurrent;
            this.previousCurrent = previousCurrent;
        }

        public static ESCollectionElementDrawScope Push(ESCollectionElementDrawContext context)
        {
            var scope = new ESCollectionElementDrawScope(hasCurrent, current);
            current = context;
            hasCurrent = true;
            return scope;
        }

        public static bool TryGet(
            string propertyPath,
            out ESCollectionElementDrawContext context)
        {
            if (hasCurrent
                && string.Equals(
                    current.ElementPropertyPath,
                    propertyPath,
                    StringComparison.Ordinal))
            {
                context = current;
                return true;
            }

            context = default;
            return false;
        }

        public void Dispose()
        {
            current = previousCurrent;
            hasCurrent = previousHasCurrent;
        }
    }
}
