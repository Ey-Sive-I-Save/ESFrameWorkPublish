using System;
using System.Collections;
using System.Collections.Generic;
using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Automatically replaces Odin's standard selector for Unity [SerializeReference] values.
    /// The drawer is selected from Odin's UnityPolymorphic serialization backend, rather than
    /// from an ES attribute, so it never changes source declarations or serialized data.
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class ESPolymorphicReferenceDrawer : OdinValueDrawer<object>
    {
        private const int MaxMultiEditTargets = 10;
        private const float SelectorArrowWidth = 22f;
        private const float SelectorMinimumWidth = 152f;
        private const float SelectorMaximumWidth = 248f;
        private const float HeaderHeight = 44f;
        private const float HeaderTopHeight = 25f;
        private const float ClearHitWidth = 24f;
        private const float FrameLineWidth = 1f;
        private const int ManagedReferenceTypeCacheLimit = 256;
        private static readonly Dictionary<string, Type> managedReferenceTypesByName
            = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly HashSet<string> unresolvedManagedReferenceTypeNames
            = new HashSet<string>(StringComparer.Ordinal);
        private static GUIStyle titleStyle;
        private static GUIStyle selectedSelectorStyle;
        private static GUIStyle emptySelectorStyle;
        private static GUIStyle warningSelectorStyle;
        private static GUIStyle readOnlySelectorStyle;
        private static GUIStyle selectorArrowStyle;
        private static GUIStyle clearStyle;
        private static bool stylesInitialized;
        private static bool stylesProSkin;

        private bool expandedInitialized;
        private bool expanded;
        private int cachedNestingDepth = -1;
        private bool collectionMembershipInitialized;
        private bool isReferenceCollectionElement;
        private Type cachedDeclaredBaseType;
        private AdvancedDropdownState selectorState;
        // Reused across repaints. Dynamic selector text is updated in place instead of creating
        // GUIContent instances on every IMGUI event.
        private readonly GUIContent foldoutContent = new GUIContent();
        private readonly GUIContent selectorContent = new GUIContent();
        private readonly GUIContent clearContent
            = new GUIContent("×", "快速清除当前多态配置（可 Ctrl+Z 撤销）");
        private Type presentationType;
        private string presentationUnresolvedTypeName;
        private bool presentationHasValue;
        private bool presentationHasUnresolvedType;
        private bool presentationMultiEditLimited;
        private int presentationTargetCount;
        private bool presentationHasMixedTargets;
        private bool presentationInitialized;
        private bool presentationProSkin;
        private string presentationSelectorText;
        private string presentationSelectorTooltip;
        private string presentationMetaText;
        private string presentationMissingNotice;
        private string unresolvedRawTypeNameCache;
        private string unresolvedFormattedTypeNameCache;
        private GUIStyle presentationSelectorStyle;
        private readonly GUIContent titleContent = new GUIContent();
        private bool titleInitialized;
        private Type titleValueType;
        private string titleLabelText;
        private int titleNestingDepth;
        private bool titleSuppressValueType;
        private string presentationTitleText;
        private string presentationMultiTargetNotice;

        protected override bool CanDrawValueProperty(InspectorProperty property)
        {
            if (!IsUnityManagedReference(property))
                return false;

            Type baseType = GetBaseType(property);
            return IsSupportedReferenceBaseType(baseType);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (!collectionMembershipInitialized)
            {
                isReferenceCollectionElement = IsReferenceCollectionElement(Property);
                collectionMembershipInitialized = true;
            }

            if (isReferenceCollectionElement
                && !ESPolymorphicReferencePreferences.UseESCollectionRenderer)
            {
                CallNextDrawer(label);
                return;
            }

            if (!ESPolymorphicReferencePreferences.UseESRenderer)
            {
                CallNextDrawer(label);
                return;
            }

            Type baseType = GetCachedBaseType();
            if (!IsSupportedReferenceBaseType(baseType))
            {
                CallNextDrawer(label);
                return;
            }

            int nestingDepth = GetNestingDepth();
            Rect allocatedFrameRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
            Rect frameRect = ApplyNestingInset(allocatedFrameRect, nestingDepth);
            object value = null;
            string unresolvedTypeName = null;
            ESStatusKind status = ESStatusKind.Empty;
            int targetCount = GetTargetCount();
            bool multiEditLimited = targetCount > MaxMultiEditTargets;
            bool multiTargetMixed = targetCount > 1 && HasMixedMultiTargetValue();

            if (!expandedInitialized)
            {
                expanded = true;
                expandedInitialized = true;
            }

            try
            {
                value = ValueEntry.WeakSmartValue;
                unresolvedTypeName = GetUnresolvedManagedReferenceTypeName(value);
                status = ResolveStatusKind(value, unresolvedTypeName, multiEditLimited, multiTargetMixed);
                DrawHeader(
                    label,
                    baseType,
                    value,
                    unresolvedTypeName,
                    nestingDepth,
                    status,
                    multiEditLimited,
                    targetCount,
                    multiTargetMixed);

                if (!string.IsNullOrEmpty(unresolvedTypeName))
                {
                    DrawMissingTypeNotice();
                    return;
                }

                // Odin exposes one representative value even when multiple selected objects
                // disagree. It must not be drawn as a common configuration: selecting a type is
                // the only explicit operation that can normalize all targets safely.
                if (multiTargetMixed || multiEditLimited)
                {
                    DrawMultiTargetNotice();
                    return;
                }

                if (value == null || !expanded)
                    return;

                EditorGUILayout.Space(2f);
                bool nestedBody = nestingDepth > 0;
                if (nestedBody)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(8f + (nestingDepth - 1) * 6f);
                    EditorGUILayout.BeginVertical();
                }

                EditorGUI.indentLevel += nestedBody ? 1 : 0;
                try
                {
                    for (int i = 0; i < Property.Children.Count; i++)
                        Property.Children[i].Draw();
                }
                finally
                {
                    EditorGUI.indentLevel -= nestedBody ? 1 : 0;
                    if (nestedBody)
                    {
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            finally
            {
                // Reserve a small, real layout strip for the bottom border. Using only the last
                // child rect lets Odin's following control paint over the border.
                Rect frameBottomRect = GUILayoutUtility.GetRect(0f, 3f, GUILayout.ExpandWidth(true));
                DrawOuterFrame(
                    frameRect,
                    frameBottomRect,
                    nestingDepth,
                    status);
            }
        }

        private int GetNestingDepth()
        {
            if (cachedNestingDepth < 0)
                cachedNestingDepth = GetManagedReferenceDepth(Property);

            return cachedNestingDepth;
        }

        private Type GetCachedBaseType()
        {
            // A drawer instance is bound to one Odin property for its lifetime. Re-reading the
            // managed-reference field declaration every repaint performs an avoidable
            // SerializedObject lookup, especially expensive in deep collections.
            if (cachedDeclaredBaseType == null)
                cachedDeclaredBaseType = GetBaseType(Property);

            return cachedDeclaredBaseType;
        }

        private int GetTargetCount()
        {
            return Property?.Tree?.WeakTargets == null
                ? 0
                : Property.Tree.WeakTargets.Count;
        }

        private bool HasMixedMultiTargetValue()
        {
            if (ValueEntry == null || ValueEntry.WeakValues == null || ValueEntry.WeakValues.Count <= 1)
                return false;

            // A large selection is read-only by design. Avoid walking every value during IMGUI
            // repaint when no write is permitted anyway.
            if (ValueEntry.WeakValues.Count > MaxMultiEditTargets)
                return false;

            Type commonType = null;
            bool hasEmptyValue = false;
            for (int i = 0; i < ValueEntry.WeakValues.Count; i++)
            {
                object targetValue = ValueEntry.WeakValues[i];
                if (targetValue == null)
                {
                    hasEmptyValue = true;
                    continue;
                }

                Type targetType = targetValue.GetType();
                if (commonType == null)
                {
                    commonType = targetType;
                    continue;
                }

                if (commonType != targetType)
                    return true;
            }

            return hasEmptyValue && commonType != null;
        }

        private ESStatusKind ResolveStatusKind(
            object value,
            string unresolvedTypeName,
            bool multiEditLimited = false,
            bool multiTargetMixed = false)
        {
            if (!string.IsNullOrEmpty(unresolvedTypeName))
                return ESStatusKind.Error;

            if (multiEditLimited)
                return ESStatusKind.ReadOnly;

            if (multiTargetMixed)
                return ESStatusKind.Warning;

            if (value != null)
                return ESStatusKind.Ready;

            ESFieldPolicyAttribute policy = Property?.GetAttribute<ESFieldPolicyAttribute>();
            if (policy != null)
            {
                if (policy.Requirement == ESFieldRequirement.Required)
                    return ESStatusKind.Error;
                if (policy.Requirement == ESFieldRequirement.Recommended)
                    return ESStatusKind.Warning;
            }

            return ESStatusKind.Empty;
        }

        private static bool IsUnityManagedReference(InspectorProperty property)
        {
            if (property == null
                || property.Info == null
                || property.ValueEntry == null
                || property.Tree == null
                || property.Tree.WeakTargets.Count == 0)
                return false;

            try
            {
                SerializedObject serializedObject = property.Tree.UnitySerializedObject;
                if (serializedObject != null)
                {
                    SerializedProperty serializedProperty = string.IsNullOrEmpty(property.UnityPropertyPath)
                        ? null
                        : serializedObject.FindProperty(property.UnityPropertyPath);
                    if (serializedProperty != null)
                        return serializedProperty.propertyType == SerializedPropertyType.ManagedReference;

                    // Do not use an inherited backend for the PropertyTree root or for ordinary
                    // serialized class fields. The fallback is only for a real child path whose
                    // SerializedProperty is temporarily unavailable during Odin tree rebuild.
                    if (string.IsNullOrEmpty(property.UnityPropertyPath))
                        return false;
                }
            }
            catch (Exception)
            {
                // Fall through to Odin's backend information below.
            }

            // InspectorPropertyInfo normally identifies the node directly. This fallback also
            // keeps non-Unity PropertyTree hosts working when no SerializedObject is available.
            return !string.IsNullOrEmpty(property.UnityPropertyPath)
                   && (property.ValueEntry.SerializationBackend == SerializationBackend.UnityPolymorphic
                       || property.Info.SerializationBackend == SerializationBackend.UnityPolymorphic);
        }

        private static bool IsReferenceCollectionElement(InspectorProperty property)
        {
            InspectorProperty ancestor = property?.Parent;
            while (ancestor != null)
            {
                if (ancestor.Info != null
                    && ancestor.ValueEntry != null
                    && ancestor.ValueEntry.WeakSmartValue is IList
                    && IsSerializeReferenceCollection(ancestor))
                    return true;

                ancestor = ancestor.Parent;
            }

            return false;
        }

        private static bool IsSerializeReferenceCollection(InspectorProperty collectionProperty)
        {
            if (collectionProperty?.Tree?.UnitySerializedObject == null
                || string.IsNullOrEmpty(collectionProperty.UnityPropertyPath))
                return false;

            try
            {
                SerializedProperty serializedProperty = collectionProperty.Tree.UnitySerializedObject
                    .FindProperty(collectionProperty.UnityPropertyPath);
                if (serializedProperty == null
                    || !serializedProperty.isArray
                    || serializedProperty.arraySize <= 0)
                    return false;

                SerializedProperty firstElement = serializedProperty.GetArrayElementAtIndex(0);
                return firstElement != null
                       && firstElement.propertyType == SerializedPropertyType.ManagedReference;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Type GetBaseType(InspectorProperty property)
        {
            // Odin may expose the concrete runtime type for a populated managed reference.
            // The selector must instead use the field's declared reference type so sibling
            // implementations remain available when reselecting.
            Type serializedFieldType = GetSerializedManagedReferenceFieldType(property);
            if (serializedFieldType != null && serializedFieldType != typeof(object))
                return serializedFieldType;

            Type declaredType = property?.Info?.TypeOfValue;
            if (declaredType != null && declaredType != typeof(object))
                return declaredType;

            Type type = property?.BaseValueEntry?.TypeOfValue;
            return type == typeof(object) ? null : type;
        }

        private static Type GetSerializedManagedReferenceFieldType(InspectorProperty property)
        {
            if (property?.Tree?.UnitySerializedObject == null
                || string.IsNullOrEmpty(property.UnityPropertyPath))
                return null;

            try
            {
                SerializedProperty serializedProperty = property.Tree.UnitySerializedObject
                    .FindProperty(property.UnityPropertyPath);
                if (serializedProperty == null
                    || serializedProperty.propertyType != SerializedPropertyType.ManagedReference)
                    return null;

                return ResolveManagedReferenceTypeName(serializedProperty.managedReferenceFieldTypename);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Type ResolveManagedReferenceTypeName(string rawTypeName)
        {
            if (string.IsNullOrWhiteSpace(rawTypeName))
                return null;

            if (managedReferenceTypesByName.TryGetValue(rawTypeName, out Type cachedType))
                return cachedType;

            if (unresolvedManagedReferenceTypeNames.Contains(rawTypeName))
                return null;

            Type resolvedType = ResolveManagedReferenceTypeNameUncached(rawTypeName);
            CacheManagedReferenceTypeName(rawTypeName, resolvedType);
            return resolvedType;
        }

        private static Type ResolveManagedReferenceTypeNameUncached(string rawTypeName)
        {

            int separator = rawTypeName.IndexOf(' ');
            if (separator <= 0 || separator >= rawTypeName.Length - 1)
                return null;

            string assemblyName = rawTypeName.Substring(0, separator);
            string typeName = rawTypeName.Substring(separator + 1);
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                    continue;

                return assembly.GetType(typeName, throwOnError: false);
            }

            return Type.GetType(typeName + ", " + assemblyName, throwOnError: false);
        }

        private static void CacheManagedReferenceTypeName(string rawTypeName, Type resolvedType)
        {
            // Type names come from serialized data. Keep this defensive cache bounded so an
            // imported asset with many malformed names cannot grow editor memory indefinitely.
            if (managedReferenceTypesByName.Count + unresolvedManagedReferenceTypeNames.Count
                >= ManagedReferenceTypeCacheLimit)
            {
                managedReferenceTypesByName.Clear();
                unresolvedManagedReferenceTypeNames.Clear();
            }

            if (resolvedType == null)
                unresolvedManagedReferenceTypeNames.Add(rawTypeName);
            else
                managedReferenceTypesByName.Add(rawTypeName, resolvedType);
        }

        private static bool IsSupportedReferenceBaseType(Type type)
        {
            return type != null
                   && type != typeof(object)
                   && (type.IsClass || type.IsInterface)
                   && !typeof(IList).IsAssignableFrom(type)
                   && !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private static int GetManagedReferenceDepth(InspectorProperty property)
        {
            int depth = 0;
            InspectorProperty parent = property?.Parent;
            while (parent != null)
            {
                // The PropertyTree root can expose UnityPolymorphic as its backend too, but it
                // is not a managed-reference field. Only count actual child nodes as nesting.
                if (parent.Parent != null && IsUnityManagedReference(parent))
                    depth++;
                parent = parent.Parent;
            }

            return depth;
        }

        private void DrawHeader(
            GUIContent label,
            Type baseType,
            object value,
            string unresolvedTypeName,
            int nestingDepth,
            ESStatusKind status,
            bool multiEditLimited,
            int targetCount,
            bool multiTargetMixed)
        {
            Rect allocatedRect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
            Rect headerRect = ApplyNestingInset(allocatedRect, nestingDepth);
            bool hasValue = value != null;
            bool hasUnresolvedType = !string.IsNullOrEmpty(unresolvedTypeName);
            float clearWidth = (hasValue || multiTargetMixed)
                               && !hasUnresolvedType
                               && !multiEditLimited
                ? ClearHitWidth
                : 0f;
            float minimumSelectorWidth = headerRect.width < 300f ? 108f : SelectorMinimumWidth;
            float selectorWidth = Mathf.Min(SelectorMaximumWidth, Mathf.Max(minimumSelectorWidth, headerRect.width * 0.42f));
            Rect topRect = new Rect(headerRect.x, headerRect.y + 2f, headerRect.width, HeaderTopHeight);
            Rect foldoutRect = new Rect(topRect.x + 4f, topRect.y + 2f, 24f, 24f);
            Rect selectorRect = new Rect(
                topRect.xMax - selectorWidth - clearWidth,
                topRect.y,
                selectorWidth - 4f,
                HeaderTopHeight);
            Rect clearRect = new Rect(selectorRect.xMax + 4f, topRect.y, clearWidth - 4f, HeaderTopHeight);
            Rect titleRect = new Rect(
                foldoutRect.xMax + 4f,
                topRect.y,
                Mathf.Max(12f, selectorRect.x - foldoutRect.xMax - 10f),
                HeaderTopHeight);
            Rect toggleRect = new Rect(
                headerRect.x,
                headerRect.y,
                Mathf.Max(0f, selectorRect.x - headerRect.x - 8f),
                HeaderTopHeight);
            Rect metaRect = new Rect(
                titleRect.x,
                headerRect.y + HeaderTopHeight + 3f,
                headerRect.xMax - titleRect.x - 8f,
                HeaderHeight - HeaderTopHeight - 7f);

            DrawHeaderBackground(
                headerRect,
                status,
                nestingDepth);
            if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none))
                expanded = !expanded;

            foldoutContent.text = expanded ? "▾" : "▸";
            foldoutContent.tooltip = expanded ? "折叠当前多态配置" : "展开当前多态配置";
            GUI.Label(foldoutRect, foldoutContent, SelectorArrowStyle);
            UpdateTitlePresentation(label, value, nestingDepth, multiTargetMixed || multiEditLimited);
            titleContent.text = presentationTitleText;
            GUI.Label(titleRect, titleContent, TitleStyle);

            UpdateSelectorPresentation(
                value == null ? null : value.GetType(),
                unresolvedTypeName,
                hasValue,
                hasUnresolvedType,
                multiEditLimited,
                targetCount,
                multiTargetMixed);
            selectorContent.text = presentationSelectorText;
            selectorContent.tooltip = presentationSelectorTooltip;

            if (!multiEditLimited && GUI.Button(selectorRect, GUIContent.none, GUIStyle.none))
                OpenTypePicker(
                    selectorRect,
                    baseType,
                    multiTargetMixed || hasUnresolvedType ? null : value == null ? null : value.GetType(),
                    unresolvedTypeName);
            DrawSelector(
                selectorRect,
                selectorContent,
                presentationSelectorStyle,
                hasValue || hasUnresolvedType || multiTargetMixed,
                nestingDepth);

            if (clearWidth > 0f)
            {
                if (GUI.Button(clearRect, clearContent, GUIStyle.none))
                    ClearValue();
                GUI.Label(clearRect, "×", ClearStyle);
            }

            ESFieldRow.DrawStatus(
                metaRect,
                status,
                presentationMetaText,
                MetaStyle);
        }

        private void UpdateTitlePresentation(
            GUIContent label,
            object value,
            int nestingDepth,
            bool suppressValueType)
        {
            string labelText = label == null ? null : label.text;
            Type valueType = value == null ? null : value.GetType();
            if (titleInitialized
                && titleValueType == valueType
                && titleNestingDepth == nestingDepth
                && titleSuppressValueType == suppressValueType
                && string.Equals(titleLabelText, labelText, StringComparison.Ordinal))
                return;

            titleInitialized = true;
            titleValueType = valueType;
            titleLabelText = labelText;
            titleNestingDepth = nestingDepth;
            titleSuppressValueType = suppressValueType;
            presentationTitleText = ResolveTitle(label, value, suppressValueType);
            if (nestingDepth > 0)
                presentationTitleText += " · 嵌套 " + nestingDepth;
        }

        private void UpdateSelectorPresentation(
            Type valueType,
            string unresolvedTypeName,
            bool hasValue,
            bool hasUnresolvedType,
            bool multiEditLimited,
            int targetCount,
            bool multiTargetMixed)
        {
            bool proSkin = ESEditorPresentation.IsProSkin;
            if (presentationInitialized
                && presentationType == valueType
                && string.Equals(presentationUnresolvedTypeName, unresolvedTypeName, StringComparison.Ordinal)
                && presentationHasValue == hasValue
                && presentationHasUnresolvedType == hasUnresolvedType
                && presentationMultiEditLimited == multiEditLimited
                && presentationTargetCount == targetCount
                && presentationHasMixedTargets == multiTargetMixed
                && presentationProSkin == proSkin)
                return;

            presentationInitialized = true;
            presentationType = valueType;
            presentationUnresolvedTypeName = unresolvedTypeName;
            presentationHasValue = hasValue;
            presentationHasUnresolvedType = hasUnresolvedType;
            presentationMultiEditLimited = multiEditLimited;
            presentationTargetCount = targetCount;
            presentationHasMixedTargets = multiTargetMixed;
            presentationProSkin = proSkin;

            if (multiTargetMixed)
            {
                presentationSelectorText = "多目标不一致";
                presentationSelectorTooltip = "已选 " + targetCount
                                              + " 个对象；当前多态类型或空值状态不一致。"
                                              + "\n选择新类型会明确覆盖全部已选对象。";
                presentationSelectorStyle = WarningSelectorStyle;
                presentationMetaText = "多目标 · " + targetCount + " 个对象 · 当前配置不一致";
                presentationMissingNotice = null;
                presentationMultiTargetNotice =
                    "所选对象的多态类型或空值状态不一致，不能安全显示某一个对象的子字段。"
                    + "请从右侧选择一个类型；确认后会为每个对象创建独立实例并统一替换。";
            }
            else if (hasUnresolvedType)
            {
                presentationSelectorText = "替代类型";
                presentationSelectorTooltip = "已保存但无法解析的类型：" + unresolvedTypeName
                                              + "\n选择类型会明确覆盖旧引用。";
                presentationSelectorStyle = WarningSelectorStyle;
                presentationMetaText = "类型缺失 · " + unresolvedTypeName;
                presentationMissingNotice = "无法解析已保存的多态类型：" + unresolvedTypeName
                                            + "。请恢复对应脚本/程序集，或通过右侧“选择替代类型”明确覆盖旧引用。"
                                            + (multiEditLimited
                                                ? "当前选中对象超过批量编辑上限，请先减少到 "
                                                  + MaxMultiEditTargets + " 个以内。"
                                                : string.Empty);
                presentationMultiTargetNotice = null;
            }
            else if (hasValue)
            {
                ESTypeCatalog.Entry current = ESTypeCatalog.GetEntry(valueType);
                presentationSelectorText = current.DisplayName;
                presentationSelectorTooltip = "当前使用类型：" + current.DisplayName
                                              + "\n点击更换类型\n" + current.Tooltip;
                presentationSelectorStyle = SelectedSelectorStyle;
                presentationMetaText = "当前：" + BuildTypeSummary(current);
                presentationMissingNotice = null;
                presentationMultiTargetNotice = null;
            }

            else
            {
                presentationSelectorText = "选择类型";
                presentationSelectorTooltip = "从配置目录中创建一个具体的多态配置";
                ESFieldPolicyAttribute policy = Property?.GetAttribute<ESFieldPolicyAttribute>();
                presentationSelectorStyle = policy != null
                    && policy.Requirement != ESFieldRequirement.Optional
                    ? WarningSelectorStyle
                    : EmptySelectorStyle;
                presentationMetaText = policy != null
                    && policy.Requirement == ESFieldRequirement.Required
                    ? "必填 · 请选择一个具体类型"
                    : policy != null
                        && policy.Requirement == ESFieldRequirement.Recommended
                        ? "建议配置 · 从目录选择一个具体类型"
                        : "未配置 · 从目录选择一个具体类型";
                presentationMissingNotice = null;
                presentationMultiTargetNotice = null;
            }

            if (targetCount > 1 && !multiTargetMixed)
                presentationMetaText = "多目标 · " + targetCount + " 个对象 · " + presentationMetaText;

            if (multiEditLimited)
            {
                presentationSelectorStyle = ReadOnlySelectorStyle;
                presentationSelectorTooltip = "当前选中了超过 " + MaxMultiEditTargets
                                              + " 个对象。请减少选中对象后再编辑多态类型。";
                presentationMetaText = presentationMetaText
                                       + " · 批量编辑上限 " + MaxMultiEditTargets;
                presentationMultiTargetNotice = "当前选中了 " + targetCount + " 个对象，超过多态批量编辑上限 "
                                               + MaxMultiEditTargets
                                               + "。为避免大量序列化写入和错误覆盖，此字段已只读；请减少选中对象后再操作。";
            }

            string hint = GetFieldHint();
            if (!string.IsNullOrEmpty(hint))
                presentationMetaText += " · " + hint;
        }

        private string GetFieldHint()
        {
            return Property?.GetAttribute<ESFieldHintAttribute>()?.Text;
        }

        private static Rect ApplyNestingInset(Rect rect, int nestingDepth)
        {
            float inset = nestingDepth > 0
                ? Mathf.Min(12f + (nestingDepth - 1) * 6f, rect.width * 0.12f)
                : 0f;
            return new Rect(
                rect.x + inset,
                rect.y,
                Mathf.Max(80f, rect.width - inset),
                rect.height);
        }

        private static void DrawOuterFrame(
            Rect frameRect,
            Rect frameBottomRect,
            int nestingDepth,
            ESStatusKind status)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float bottom = Mathf.Max(frameRect.y + HeaderHeight, frameBottomRect.yMax);
            Rect rect = new Rect(frameRect.x, frameRect.y, frameRect.width, bottom - frameRect.y);
            Color line = ESEditorPresentation.GetStatusFrameColor(nestingDepth, status);
            ESEditorPresentation.DrawFrame(rect, line, FrameLineWidth);
        }

        private static void DrawHeaderBackground(
            Rect rect,
            ESStatusKind status,
            int nestingDepth)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color background = status == ESStatusKind.Error
                ? ESEditorPresentation.WarningBackground
                : ESEditorPresentation.GetDepthBackground(nestingDepth);
            Color line = ESEditorPresentation.DividerColor;
            Color accent = status == ESStatusKind.Error
                ? ESEditorPresentation.GetStatusAccent(nestingDepth, ESStatusKind.Error)
                : status == ESStatusKind.Ready
                    ? GetDepthAccent(nestingDepth)
                    : line;

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
        }

        private static string BuildTypeSummary(ESTypeCatalog.Entry descriptor)
        {
            string group = string.IsNullOrWhiteSpace(descriptor.GroupPath)
                ? string.Empty
                : descriptor.GroupPath + " / ";
            return group + descriptor.DisplayName + "  ·  " + descriptor.Subtitle;
        }

        private string ResolveTitle(GUIContent label, object value, bool suppressValueType)
        {
            LabelTextAttribute labelAttribute = Property?.GetAttribute<LabelTextAttribute>();
            if (labelAttribute != null && !string.IsNullOrWhiteSpace(labelAttribute.Text))
                return labelAttribute.Text;

            // Collection elements usually arrive with a generic label such as "Element" or
            // "多态配置". Use the concrete business name there so sibling entries are readable.
            if (!suppressValueType && value != null)
                return ESTypeCatalog.GetDisplayName(value.GetType());

            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;

            return Property?.NiceName ?? "多态配置";
        }

        private static void DrawSelector(
            Rect rect,
            GUIContent content,
            GUIStyle textStyle,
            bool selected,
            int nestingDepth)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color background = textStyle == WarningSelectorStyle
                    ? ESEditorPresentation.WarningBackground
                    : selected
                        ? GetSelectorBackground(nestingDepth)
                        : ESEditorPresentation.NeutralSelectorBackground;
                EditorGUI.DrawRect(rect, background);
            }

            Rect textRect = new Rect(rect.x + 8f, rect.y, rect.width - SelectorArrowWidth - 8f, rect.height);
            Rect arrowRect = new Rect(rect.xMax - SelectorArrowWidth, rect.y, SelectorArrowWidth, rect.height);
            GUI.Label(textRect, content, textStyle);
            GUI.Label(arrowRect, "▾", SelectorArrowStyle);

            if (Event.current.type != EventType.Repaint)
                return;

            bool hovered = rect.Contains(Event.current.mousePosition);
            if (!selected && !hovered)
                return;

            Color underline = textStyle == WarningSelectorStyle
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                : selected
                    ? GetDepthAccent(nestingDepth)
                    : ESEditorPresentation.NeutralHoverColor;
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), underline);
        }

        private static Color GetDepthAccent(int nestingDepth)
        {
            return ESEditorPresentation.GetDepthAccent(nestingDepth);
        }

        private static Color GetSelectorBackground(int nestingDepth)
        {
            return ESEditorPresentation.GetSelectorBackground(nestingDepth);
        }

        private void OpenTypePicker(Rect anchorRect, Type baseType, Type selectedType, string unresolvedTypeName)
        {
            var toolbarActions = new[]
            {
                new ESSearchDropdown.ToolbarAction(
                    "方案",
                    ESPolymorphicReferencePreferences.ShowMenu,
                    "切换绘制方案（当前：" + ESPolymorphicReferencePreferences.CurrentDisplayName + "）"),
                new ESSearchDropdown.ToolbarAction(
                    "集合",
                    ESPolymorphicReferencePreferences.ShowCollectionMenu,
                    "切换集合元素绘制方案（当前："
                    + ESPolymorphicReferencePreferences.CurrentCollectionDisplayName + "）"),
                new ESSearchDropdown.ToolbarAction(
                    "诊断",
                    () => LogTypeCatalogDiagnostics(baseType, selectedType),
                    "输出声明基类、当前类型和候选数量到 Console"),
                new ESSearchDropdown.ToolbarAction(
                    "刷新",
                    ClearTypeCaches,
                    "清除类型目录缓存并重新构建候选项")
            };

            ESSearchDropdown.Open(
                anchorRect,
                "选择 " + ESTypeCatalog.GetDisplayName(baseType),
                () => BuildTypeEntries(baseType, selectedType, unresolvedTypeName),
                state: selectorState ?? (selectorState = new AdvancedDropdownState()),
                minimumWindowSize: new Vector2(420f, 320f),
                toolbarActions: toolbarActions);
        }

        private List<ESSearchDropdown.Entry> BuildTypeEntries(
            Type baseType,
            Type selectedType,
            string unresolvedTypeName)
        {
            ESTypeCatalog.Catalog catalog = GetTypeCatalog(baseType);
            var entries = new List<ESSearchDropdown.Entry>(catalog.Count + 1);
            if (!string.IsNullOrEmpty(unresolvedTypeName))
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "选择新类型会覆盖无法解析的旧引用",
                    tooltip: "原类型：" + unresolvedTypeName));
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                ESTypeCatalog.Entry descriptor = catalog.Entries[i];
                Type capturedType = descriptor.Type;
                entries.Add(ESSearchDropdown.Entry.Item(
                    descriptor.DisplayName,
                    () => CreateValue(capturedType, unresolvedTypeName),
                    descriptor.GroupPath,
                    subtitle: descriptor.Subtitle,
                    tooltip: descriptor.Tooltip,
                    keywords: descriptor.Keywords,
                    badge: descriptor.Badge,
                    selected: capturedType == selectedType));
            }

            if (catalog.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "没有可创建的具体类型",
                    tooltip: "候选类型必须是可序列化、非抽象、非泛型且具有无参构造函数的 class。"));
            }

            return entries;
        }

        private static void LogTypeCatalogDiagnostics(Type baseType, Type selectedType)
        {
            ESTypeCatalog.Catalog catalog = GetTypeCatalog(baseType);
            Debug.Log(
                "[ESPolymorphicReference] 类型选择器诊断\n"
                + "声明基类：" + (baseType == null ? "<null>" : baseType.FullName) + "\n"
                + "当前类型：" + (selectedType == null ? "<null>" : selectedType.FullName) + "\n"
                + "候选数量：" + catalog.Count);
        }

        private void CreateValue(Type concreteType, string unresolvedTypeName)
        {
            object currentValue = ValueEntry.WeakSmartValue;
            int targetCount = GetTargetCount();
            bool multiTargetMixed = targetCount > 1 && HasMixedMultiTargetValue();
            if (!multiTargetMixed && currentValue != null && currentValue.GetType() == concreteType)
                return;

            bool replacesExistingValue = currentValue != null
                                         || !string.IsNullOrEmpty(unresolvedTypeName)
                                         || multiTargetMixed;
            if (replacesExistingValue)
            {
                string previous = multiTargetMixed
                    ? "已选 " + targetCount + " 个对象，当前多态配置不一致。"
                    : currentValue == null
                    ? "无法解析的旧类型：" + unresolvedTypeName
                    : "当前类型：" + BuildTypeSummary(ESTypeCatalog.GetEntry(currentValue.GetType()));
                string batchNotice = targetCount > 1
                    ? "\n每个已选对象都会创建独立的新实例。"
                    : string.Empty;
                if (!EditorUtility.DisplayDialog(
                        "替换多态配置类型",
                        previous + "\n\n将替换为：" + BuildTypeSummary(ESTypeCatalog.GetEntry(concreteType))
                        + "\n当前对象内该多态配置的数据会被覆盖。" + batchNotice
                        + "\n可在保存前使用 Ctrl+Z 撤销。",
                        "替换",
                        "取消"))
                    return;
            }

            if (TryAssignManagedReferenceType(concreteType, "替换多态配置类型", out string assignError))
                expanded = true;
            else
                EditorUtility.DisplayDialog("无法写入多态配置", assignError, "知道了");
        }

        private void ClearValue()
        {
            // Clearing is intentionally a one-click operation. It is reversible through the
            // same Unity/Odin Undo stack as type replacement, so the common cleanup path stays
            // fast without hiding a destructive write behind an extra modal dialog.
            TryAssignManagedReference(null, "清除多态配置", out _);
        }

        private bool TryAssignManagedReference(object value, string undoName, out string error)
        {
            if (GetTargetCount() > 1)
                return TryAssignManagedReferenceToTargets(value == null ? null : value.GetType(), undoName, out error);

            error = null;
            try
            {
                SerializedProperty serializedProperty = GetUnitySerializedProperty();
                if (serializedProperty != null
                    && serializedProperty.propertyType == SerializedPropertyType.ManagedReference)
                {
                    RecordFallbackUndo(undoName);
                    serializedProperty.managedReferenceValue = value;
                    serializedProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    return true;
                }

                RecordFallbackUndo(undoName);
                ValueEntry.WeakSmartValue = value;
                GUI.changed = true;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + "：" + exception.Message;
                return false;
            }
        }

        private bool TryAssignManagedReferenceType(Type concreteType, string undoName, out string error)
        {
            if (GetTargetCount() > 1)
                return TryAssignManagedReferenceToTargets(concreteType, undoName, out error);

            if (!TryCreateValue(concreteType, out object createdValue, out error))
            {
                error = "无法创建多态类型：" + concreteType.FullName + "\n" + error;
                return false;
            }

            return TryAssignManagedReference(createdValue, undoName, out error);
        }

        private bool TryAssignManagedReferenceToTargets(
            Type concreteType,
            string undoName,
            out string error)
        {
            error = null;
            int targetCount = GetTargetCount();
            if (targetCount <= 1)
            {
                error = "没有足够的多目标对象可写入。";
                return false;
            }

            if (targetCount > MaxMultiEditTargets)
            {
                error = "批量编辑最多支持 " + MaxMultiEditTargets + " 个对象。";
                return false;
            }

            if (string.IsNullOrEmpty(Property.UnityPropertyPath))
            {
                error = "当前多态字段没有可写入的 Unity 属性路径。";
                return false;
            }

            var assignments = new List<ManagedReferenceTargetAssignment>(targetCount);
            var undoTargets = new List<UnityEngine.Object>(targetCount);
            try
            {
                for (int i = 0; i < Property.Tree.WeakTargets.Count; i++)
                {
                    if (!(Property.Tree.WeakTargets[i] is UnityEngine.Object target))
                    {
                        error = "第 " + (i + 1) + " 个目标不是 Unity 对象，无法安全批量写入。";
                        return false;
                    }

                    var serializedObject = new SerializedObject(target);
                    SerializedProperty serializedProperty = serializedObject.FindProperty(Property.UnityPropertyPath);
                    if (serializedProperty == null
                        || serializedProperty.propertyType != SerializedPropertyType.ManagedReference)
                    {
                        error = "第 " + (i + 1) + " 个目标没有兼容的 SerializeReference 字段。";
                        return false;
                    }

                    object targetValue = null;
                    if (concreteType != null
                        && !TryCreateValue(concreteType, out targetValue, out string createError))
                    {
                        error = "第 " + (i + 1) + " 个目标无法创建 "
                                + concreteType.Name + "：" + createError;
                        return false;
                    }

                    assignments.Add(new ManagedReferenceTargetAssignment(
                        target,
                        serializedObject,
                        serializedProperty,
                        targetValue));
                    undoTargets.Add(target);
                }

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                Undo.RecordObjects(undoTargets.ToArray(), undoName);
                for (int i = 0; i < assignments.Count; i++)
                {
                    ManagedReferenceTargetAssignment assignment = assignments[i];
                    assignment.Property.managedReferenceValue = assignment.Value;
                    assignment.SerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(assignment.Target);
                }

                Undo.CollapseUndoOperations(undoGroup);
                GUI.changed = true;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + "：" + exception.Message;
                return false;
            }
        }

        private readonly struct ManagedReferenceTargetAssignment
        {
            public readonly UnityEngine.Object Target;
            public readonly SerializedObject SerializedObject;
            public readonly SerializedProperty Property;
            public readonly object Value;

            public ManagedReferenceTargetAssignment(
                UnityEngine.Object target,
                SerializedObject serializedObject,
                SerializedProperty property,
                object value)
            {
                Target = target;
                SerializedObject = serializedObject;
                Property = property;
                Value = value;
            }
        }

        private SerializedProperty GetUnitySerializedProperty()
        {
            if (Property.Tree == null
                || Property.Tree.UnitySerializedObject == null
                || string.IsNullOrEmpty(Property.UnityPropertyPath))
                return null;

            return Property.Tree.UnitySerializedObject.FindProperty(Property.UnityPropertyPath);
        }

        private void RecordFallbackUndo(string undoName)
        {
            var targets = new List<UnityEngine.Object>();
            for (int i = 0; i < Property.Tree.WeakTargets.Count; i++)
            {
                if (Property.Tree.WeakTargets[i] is UnityEngine.Object target)
                    targets.Add(target);
            }

            if (targets.Count > 0)
                Undo.RecordObjects(targets.ToArray(), undoName);
        }

        private string GetUnresolvedManagedReferenceTypeName(object currentValue)
        {
            if (currentValue != null
                || Property.Tree == null
                || Property.Tree.UnitySerializedObject == null
                || string.IsNullOrEmpty(Property.UnityPropertyPath))
                return null;

            try
            {
                SerializedProperty property = Property.Tree.UnitySerializedObject.FindProperty(Property.UnityPropertyPath);
                if (property == null
                    || property.propertyType != SerializedPropertyType.ManagedReference
                    || string.IsNullOrEmpty(property.managedReferenceFullTypename))
                    return null;

                string rawTypeName = property.managedReferenceFullTypename;
                if (string.Equals(unresolvedRawTypeNameCache, rawTypeName, StringComparison.Ordinal))
                    return unresolvedFormattedTypeNameCache;

                unresolvedRawTypeNameCache = rawTypeName;
                unresolvedFormattedTypeNameCache = FormatManagedReferenceTypeName(rawTypeName);
                return unresolvedFormattedTypeNameCache;
            }
            catch (Exception)
            {
                // A stale UnityPropertyPath should never break the Inspector. The ordinary empty
                // selector remains available in that rare case.
                return null;
            }
        }

        private static string FormatManagedReferenceTypeName(string rawTypeName)
        {
            if (string.IsNullOrWhiteSpace(rawTypeName))
                return null;

            string value = rawTypeName.Trim();
            int assemblySeparator = value.IndexOf(' ');
            if (assemblySeparator <= 0 || assemblySeparator >= value.Length - 1)
                return value;

            string assemblyName = value.Substring(0, assemblySeparator);
            string typeName = value.Substring(assemblySeparator + 1);
            return typeName + "（" + assemblyName + "）";
        }

        private void DrawMissingTypeNotice()
        {
            EditorGUILayout.HelpBox(
                presentationMissingNotice,
                MessageType.Error);
        }

        private void DrawMultiTargetNotice()
        {
            if (!string.IsNullOrEmpty(presentationMultiTargetNotice))
                EditorGUILayout.HelpBox(presentationMultiTargetNotice, MessageType.Warning);
        }

        private static bool TryCreateValue(Type type, out object value, out string error)
        {
            value = null;
            error = null;
            try
            {
                value = Activator.CreateInstance(type, nonPublic: true);
                if (value != null)
                    return true;

                error = "构造函数没有返回实例。";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + "：" + exception.Message;
                return false;
            }
        }

        private static ESTypeCatalog.Catalog GetTypeCatalog(Type baseType)
        {
            return ESTypeCatalog.Get(baseType);
        }

        private static void ClearTypeCaches()
        {
            ESTypeCatalog.Clear();
            managedReferenceTypesByName.Clear();
            unresolvedManagedReferenceTypeNames.Clear();
        }

        internal static void OnAssemblyStream()
        {
            ClearTypeCaches();
        }

        private static void DrawDivider()
        {
            Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            ESEditorPresentation.DrawDivider(dividerRect);
        }

        private static GUIStyle TitleStyle
        {
            get
            {
                EnsureStyles();
                if (titleStyle == null)
                {
                    titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 14,
                        clipping = TextClipping.Clip
                    };
                }

                return titleStyle;
            }
        }

        private static GUIStyle SelectedSelectorStyle
        {
            get
            {
                EnsureStyles();
                if (selectedSelectorStyle == null)
                {
                    selectedSelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    selectedSelectorStyle.normal.textColor = ESEditorPresentation.SelectedTextColor;
                }

                return selectedSelectorStyle;
            }
        }

        private static GUIStyle EmptySelectorStyle
        {
            get
            {
                EnsureStyles();
                if (emptySelectorStyle == null)
                {
                    emptySelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    emptySelectorStyle.normal.textColor = ESEditorPresentation.EmptyTextColor;
                }

                return emptySelectorStyle;
            }
        }

        private static GUIStyle WarningSelectorStyle
        {
            get
            {
                EnsureStyles();
                if (warningSelectorStyle == null)
                {
                    warningSelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    warningSelectorStyle.normal.textColor = ESEditorPresentation.WarningTextColor;
                }

                return warningSelectorStyle;
            }
        }

        private static GUIStyle ReadOnlySelectorStyle
        {
            get
            {
                EnsureStyles();
                if (readOnlySelectorStyle == null)
                {
                    readOnlySelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    readOnlySelectorStyle.normal.textColor = ESEditorPresentation.MetaStyle.normal.textColor;
                }

                return readOnlySelectorStyle;
            }
        }

        private static GUIStyle SelectorArrowStyle
        {
            get
            {
                EnsureStyles();
                if (selectorArrowStyle == null)
                {
                    selectorArrowStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15,
                        fontStyle = FontStyle.Bold
                    };
                    selectorArrowStyle.normal.textColor = ESEditorPresentation.SelectorArrowColor;
                }

                return selectorArrowStyle;
            }
        }

        private static GUIStyle ClearStyle
        {
            get
            {
                EnsureStyles();
                if (clearStyle == null)
                {
                    clearStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 16,
                        fontStyle = FontStyle.Bold
                    };
                    clearStyle.normal.textColor = ESEditorPresentation.ClearActionColor;
                }

                return clearStyle;
            }
        }

        private static GUIStyle MetaStyle
        {
            get { return ESEditorPresentation.MetaStyle; }
        }

        private static void EnsureStyles()
        {
            bool proSkin = ESEditorPresentation.IsProSkin;
            if (stylesInitialized && stylesProSkin == proSkin)
                return;

            stylesInitialized = true;
            stylesProSkin = proSkin;
            titleStyle = null;
            selectedSelectorStyle = null;
            emptySelectorStyle = null;
            warningSelectorStyle = null;
            readOnlySelectorStyle = null;
            selectorArrowStyle = null;
            clearStyle = null;
        }

    }

    /// <summary>
    /// Uses ES's assembly stream instead of a separate InitializeOnLoad callback. The cache is
    /// invalidated after scripts change, but stays lazy until an actual field is drawn.
    /// </summary>
    public sealed class ESPolymorphicReferenceDrawerAssemblyStreamInitializer : ES.EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESPolymorphicReferenceDrawer.OnAssemblyStream();
        }
    }

    internal enum ESPolymorphicReferenceDrawMode
    {
        ES,
        Odin
    }

    internal static class ESPolymorphicReferencePreferences
    {
        private const string PreferencePrefix = "ES.PolymorphicReference.DrawMode.";
        private const string CollectionPreferencePrefix = "ES.PolymorphicReference.CollectionDrawMode.";
        private static bool drawModeInitialized;
        private static ESPolymorphicReferenceDrawMode cachedDrawMode;
        private static bool collectionDrawModeInitialized;
        private static ESPolymorphicReferenceDrawMode cachedCollectionDrawMode;

        public static bool UseESRenderer => DrawMode == ESPolymorphicReferenceDrawMode.ES;

        public static string CurrentDisplayName => UseESRenderer
            ? "【ES】自定义渲染"
            : "Odin 默认动态渲染";

        public static bool UseESCollectionRenderer => CollectionDrawMode == ESPolymorphicReferenceDrawMode.ES;

        public static string CurrentCollectionDisplayName => UseESCollectionRenderer
            ? "【ES】集合元素绘制"
            : "Odin 默认集合元素绘制";

        private static ESPolymorphicReferenceDrawMode DrawMode
        {
            get
            {
                if (drawModeInitialized)
                    return cachedDrawMode;

                string value = EditorPrefs.GetString(GetPreferenceKey(), nameof(ESPolymorphicReferenceDrawMode.ES));
                cachedDrawMode = Enum.TryParse(value, out ESPolymorphicReferenceDrawMode mode)
                    ? mode
                    : ESPolymorphicReferenceDrawMode.ES;
                drawModeInitialized = true;
                return cachedDrawMode;
            }
            set
            {
                cachedDrawMode = value;
                drawModeInitialized = true;
                EditorPrefs.SetString(GetPreferenceKey(), value.ToString());
                RebuildInspectors();
            }
        }

        private static ESPolymorphicReferenceDrawMode CollectionDrawMode
        {
            get
            {
                if (collectionDrawModeInitialized)
                    return cachedCollectionDrawMode;

                string value = EditorPrefs.GetString(
                    GetPreferenceKey(CollectionPreferencePrefix),
                    nameof(ESPolymorphicReferenceDrawMode.ES));
                cachedCollectionDrawMode = Enum.TryParse(value, out ESPolymorphicReferenceDrawMode mode)
                    ? mode
                    : ESPolymorphicReferenceDrawMode.ES;
                collectionDrawModeInitialized = true;
                return cachedCollectionDrawMode;
            }
            set
            {
                cachedCollectionDrawMode = value;
                collectionDrawModeInitialized = true;
                EditorPrefs.SetString(GetPreferenceKey(CollectionPreferencePrefix), value.ToString());
                RebuildInspectors();
            }
        }

        public static void ShowMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("【ES】自定义渲染"),
                UseESRenderer,
                () => DrawMode = ESPolymorphicReferenceDrawMode.ES);
            menu.AddItem(
                new GUIContent("Odin 默认动态渲染"),
                !UseESRenderer,
                () => DrawMode = ESPolymorphicReferenceDrawMode.Odin);
            menu.ShowAsContext();
        }

        public static void ShowCollectionMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("【ES】集合元素绘制"),
                UseESCollectionRenderer,
                () => CollectionDrawMode = ESPolymorphicReferenceDrawMode.ES);
            menu.AddItem(
                new GUIContent("Odin 默认集合元素绘制"),
                !UseESCollectionRenderer,
                () => CollectionDrawMode = ESPolymorphicReferenceDrawMode.Odin);
            menu.ShowAsContext();
        }

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "多态引用/绘制方案/【ES】自定义渲染")]
        private static void SelectESRenderer()
        {
            DrawMode = ESPolymorphicReferenceDrawMode.ES;
        }

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "多态引用/绘制方案/Odin 默认动态渲染")]
        private static void SelectOdinRenderer()
        {
            DrawMode = ESPolymorphicReferenceDrawMode.Odin;
        }

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "多态引用/集合元素绘制/【ES】集合元素绘制")]
        private static void SelectESCollectionRenderer()
        {
            CollectionDrawMode = ESPolymorphicReferenceDrawMode.ES;
        }

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "多态引用/集合元素绘制/Odin 默认集合元素绘制")]
        private static void SelectOdinCollectionRenderer()
        {
            CollectionDrawMode = ESPolymorphicReferenceDrawMode.Odin;
        }

        private static string GetPreferenceKey(string prefix = null)
        {
            string projectPath = Application.dataPath ?? "UnknownProject";
            return (prefix ?? PreferencePrefix) + projectPath.Replace('\\', '/');
        }

        private static void RebuildInspectors()
        {
            try
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
            catch (Exception)
            {
                // Repaint remains a safe fallback for editor hosts without an active tracker.
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                }
                catch (Exception)
                {
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            };
        }
    }
}
