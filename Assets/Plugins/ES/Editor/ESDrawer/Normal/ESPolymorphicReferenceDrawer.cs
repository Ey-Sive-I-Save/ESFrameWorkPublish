using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Compilation;
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
        private const float SelectorArrowWidth = 22f;
        private const float SelectorMinimumWidth = 152f;
        private const float SelectorMaximumWidth = 248f;
        private const float HeaderHeight = 44f;
        private const float HeaderTopHeight = 25f;
        private const float ClearHitWidth = 24f;
        private const float FrameLineWidth = 1f;
        private const string UnregisteredGroupName = "未登记类型";

        private static readonly Dictionary<Type, TypeCatalog> CatalogsByBaseType
            = new Dictionary<Type, TypeCatalog>();
        private static readonly Dictionary<Type, TypeDescriptor> DescriptorsByType
            = new Dictionary<Type, TypeDescriptor>();

        private static GUIStyle titleStyle;
        private static GUIStyle selectedSelectorStyle;
        private static GUIStyle emptySelectorStyle;
        private static GUIStyle warningSelectorStyle;
        private static GUIStyle selectorArrowStyle;
        private static GUIStyle clearStyle;

        private bool expandedInitialized;
        private bool expanded;
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
        private bool presentationInitialized;
        private string presentationSelectorText;
        private string presentationSelectorTooltip;
        private string presentationMetaText;
        private GUIStyle presentationSelectorStyle;

        static ESPolymorphicReferenceDrawer()
        {
            // Domain Reload normally clears these dictionaries. This explicit invalidation also
            // covers projects that disable Domain Reload while recompiling scripts.
            AssemblyReloadEvents.beforeAssemblyReload += ClearTypeCaches;
            CompilationPipeline.compilationFinished += _ => ClearTypeCaches();
        }

        protected override bool CanDrawValueProperty(InspectorProperty property)
        {
            if (!IsUnityManagedReference(property))
                return false;

            Type baseType = GetBaseType(property);
            return IsSupportedReferenceBaseType(baseType);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (!ESPolymorphicReferencePreferences.UseESRenderer)
            {
                CallNextDrawer(label);
                return;
            }

            Type baseType = GetBaseType(Property);
            if (!IsSupportedReferenceBaseType(baseType))
            {
                CallNextDrawer(label);
                return;
            }

            int nestingDepth = GetManagedReferenceDepth(Property);
            Rect allocatedFrameRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
            Rect frameRect = ApplyNestingInset(allocatedFrameRect, nestingDepth);
            object value = null;
            string unresolvedTypeName = null;

            if (!expandedInitialized)
            {
                expanded = true;
                expandedInitialized = true;
            }

            try
            {
                value = ValueEntry.WeakSmartValue;
                unresolvedTypeName = GetUnresolvedManagedReferenceTypeName(value);
                DrawHeader(label, baseType, value, unresolvedTypeName, nestingDepth);

                if (!string.IsNullOrEmpty(unresolvedTypeName))
                {
                    DrawMissingTypeNotice(unresolvedTypeName);
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
                    value != null,
                    !string.IsNullOrEmpty(unresolvedTypeName));
            }
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
            int nestingDepth)
        {
            Rect allocatedRect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
            Rect headerRect = ApplyNestingInset(allocatedRect, nestingDepth);
            bool hasValue = value != null;
            bool hasUnresolvedType = !string.IsNullOrEmpty(unresolvedTypeName);
            float clearWidth = hasValue && !hasUnresolvedType ? ClearHitWidth : 0f;
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

            DrawHeaderBackground(headerRect, hasUnresolvedType, hasValue, nestingDepth);
            if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none))
                expanded = !expanded;

            foldoutContent.text = expanded ? "▾" : "▸";
            foldoutContent.tooltip = expanded ? "折叠当前多态配置" : "展开当前多态配置";
            GUI.Label(foldoutRect, foldoutContent, SelectorArrowStyle);
            string title = ResolveTitle(label, value);
            if (nestingDepth > 0)
                title += " · 嵌套 " + nestingDepth;
            GUI.Label(titleRect, title, TitleStyle);

            UpdateSelectorPresentation(
                value == null ? null : value.GetType(),
                unresolvedTypeName,
                hasValue,
                hasUnresolvedType);
            selectorContent.text = presentationSelectorText;
            selectorContent.tooltip = presentationSelectorTooltip;

            if (GUI.Button(selectorRect, GUIContent.none, GUIStyle.none))
                OpenTypePicker(selectorRect, baseType, value == null ? null : value.GetType(), unresolvedTypeName);
            DrawSelector(
                selectorRect,
                selectorContent,
                presentationSelectorStyle,
                hasValue || hasUnresolvedType,
                nestingDepth);

            if (clearWidth > 0f)
            {
                if (GUI.Button(clearRect, clearContent, GUIStyle.none))
                    ClearValue();
                GUI.Label(clearRect, "×", ClearStyle);
            }

            GUI.Label(metaRect, presentationMetaText, MetaStyle);
        }

        private void UpdateSelectorPresentation(
            Type valueType,
            string unresolvedTypeName,
            bool hasValue,
            bool hasUnresolvedType)
        {
            if (presentationInitialized
                && presentationType == valueType
                && string.Equals(presentationUnresolvedTypeName, unresolvedTypeName, StringComparison.Ordinal)
                && presentationHasValue == hasValue
                && presentationHasUnresolvedType == hasUnresolvedType)
                return;

            presentationInitialized = true;
            presentationType = valueType;
            presentationUnresolvedTypeName = unresolvedTypeName;
            presentationHasValue = hasValue;
            presentationHasUnresolvedType = hasUnresolvedType;

            if (hasUnresolvedType)
            {
                presentationSelectorText = "替代类型";
                presentationSelectorTooltip = "已保存但无法解析的类型：" + unresolvedTypeName
                                              + "\n选择类型会明确覆盖旧引用。";
                presentationSelectorStyle = WarningSelectorStyle;
                presentationMetaText = "类型缺失 · " + unresolvedTypeName;
            }
            else if (hasValue)
            {
                TypeDescriptor current = DescribeType(valueType);
                presentationSelectorText = current.DisplayName;
                presentationSelectorTooltip = "当前使用类型：" + current.DisplayName
                                              + "\n点击更换类型\n" + current.Tooltip;
                presentationSelectorStyle = SelectedSelectorStyle;
                presentationMetaText = "当前：" + BuildTypeSummary(current);
            }
            else
            {
                presentationSelectorText = "选择类型";
                presentationSelectorTooltip = "从配置目录中创建一个具体的多态配置";
                presentationSelectorStyle = EmptySelectorStyle;
                presentationMetaText = "未配置 · 从目录选择一个具体类型";
            }
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
            bool hasValue,
            bool hasUnresolvedType)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float bottom = Mathf.Max(frameRect.y + HeaderHeight, frameBottomRect.yMax);
            Rect rect = new Rect(frameRect.x, frameRect.y, frameRect.width, bottom - frameRect.y);
            Color line = ESEditorPresentation.GetFrameColor(nestingDepth, hasValue, hasUnresolvedType);
            ESEditorPresentation.DrawFrame(rect, line, FrameLineWidth);
        }

        private static void DrawHeaderBackground(
            Rect rect,
            bool hasUnresolvedType,
            bool hasValue,
            int nestingDepth)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color background = ESEditorPresentation.GetDepthBackground(nestingDepth);
            Color line = EditorGUIUtility.isProSkin
                ? new Color(0.30f, 0.32f, 0.35f, 1f)
                : new Color(0.70f, 0.72f, 0.74f, 1f);
            Color accent = hasUnresolvedType
                ? new Color(0.86f, 0.47f, 0.20f, 1f)
                : hasValue
                    ? GetDepthAccent(nestingDepth)
                    : line;

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
        }

        private static string BuildTypeSummary(TypeDescriptor descriptor)
        {
            string group = string.IsNullOrWhiteSpace(descriptor.GroupPath)
                ? string.Empty
                : descriptor.GroupPath + " / ";
            return group + descriptor.DisplayName + "  ·  " + descriptor.Subtitle;
        }

        private string ResolveTitle(GUIContent label, object value)
        {
            LabelTextAttribute labelAttribute = Property?.GetAttribute<LabelTextAttribute>();
            if (labelAttribute != null && !string.IsNullOrWhiteSpace(labelAttribute.Text))
                return labelAttribute.Text;

            // Collection elements usually arrive with a generic label such as "Element" or
            // "多态配置". Use the concrete business name there so sibling entries are readable.
            if (value != null)
                return DescribeType(value.GetType()).DisplayName;

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
                    ? (EditorGUIUtility.isProSkin
                        ? new Color(0.33f, 0.22f, 0.16f, 0.90f)
                        : new Color(1f, 0.92f, 0.84f, 1f))
                    : selected
                        ? GetSelectorBackground(nestingDepth)
                        : (EditorGUIUtility.isProSkin
                            ? new Color(0.25f, 0.26f, 0.28f, 0.90f)
                            : new Color(0.88f, 0.89f, 0.90f, 1f));
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
                ? new Color(0.86f, 0.47f, 0.20f, 0.92f)
                : selected
                    ? GetDepthAccent(nestingDepth)
                    : (EditorGUIUtility.isProSkin
                        ? new Color(0.48f, 0.51f, 0.55f, 1f)
                        : new Color(0.48f, 0.51f, 0.55f, 1f));
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
                "选择 " + GetDisplayName(baseType),
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
            TypeCatalog catalog = GetTypeCatalog(baseType);
            var entries = new List<ESSearchDropdown.Entry>(catalog.Descriptors.Count + 1);
            if (!string.IsNullOrEmpty(unresolvedTypeName))
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "选择新类型会覆盖无法解析的旧引用",
                    tooltip: "原类型：" + unresolvedTypeName));
            }

            for (int i = 0; i < catalog.Descriptors.Count; i++)
            {
                TypeDescriptor descriptor = catalog.Descriptors[i];
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

            if (catalog.Descriptors.Count == 0)
            {
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "没有可创建的具体类型",
                    tooltip: "候选类型必须是可序列化、非抽象、非泛型且具有无参构造函数的 class。"));
            }

            return entries;
        }

        private static void LogTypeCatalogDiagnostics(Type baseType, Type selectedType)
        {
            TypeCatalog catalog = GetTypeCatalog(baseType);
            Debug.Log(
                "[ESPolymorphicReference] 类型选择器诊断\n"
                + "声明基类：" + (baseType == null ? "<null>" : baseType.FullName) + "\n"
                + "当前类型：" + (selectedType == null ? "<null>" : selectedType.FullName) + "\n"
                + "候选数量：" + catalog.Descriptors.Count);
        }

        private void CreateValue(Type concreteType, string unresolvedTypeName)
        {
            object currentValue = ValueEntry.WeakSmartValue;
            if (currentValue != null && currentValue.GetType() == concreteType)
                return;

            bool replacesExistingValue = currentValue != null || !string.IsNullOrEmpty(unresolvedTypeName);
            if (replacesExistingValue)
            {
                string previous = currentValue == null
                    ? "无法解析的旧类型：" + unresolvedTypeName
                    : "当前类型：" + BuildTypeSummary(DescribeType(currentValue.GetType()));
                if (!EditorUtility.DisplayDialog(
                        "替换多态配置类型",
                        previous + "\n\n将替换为：" + BuildTypeSummary(DescribeType(concreteType))
                        + "\n当前对象内该多态配置的数据会被覆盖。可在保存前使用 Ctrl+Z 撤销。",
                        "替换",
                        "取消"))
                    return;
            }

            if (!TryCreateValue(concreteType, out object createdValue, out string error))
            {
                Debug.LogError("[ESPolymorphicReference] 无法创建多态类型："
                               + concreteType.FullName + "\n" + error);
                EditorUtility.DisplayDialog("无法创建多态类型", error, "知道了");
                return;
            }

            if (TryAssignManagedReference(createdValue, "替换多态配置类型", out string assignError))
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
            error = null;
            try
            {
                SerializedProperty serializedProperty = GetUnitySerializedProperty();
                if (serializedProperty != null
                    && serializedProperty.propertyType == SerializedPropertyType.ManagedReference)
                {
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

                return FormatManagedReferenceTypeName(property.managedReferenceFullTypename);
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

        private static void DrawMissingTypeNotice(string unresolvedTypeName)
        {
            EditorGUILayout.HelpBox(
                "无法解析已保存的多态类型：" + unresolvedTypeName
                + "。请恢复对应脚本/程序集，或通过右侧“选择替代类型”明确覆盖旧引用。",
                MessageType.Error);
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

        private static TypeCatalog GetTypeCatalog(Type baseType)
        {
            if (CatalogsByBaseType.TryGetValue(baseType, out TypeCatalog catalog))
                return catalog;

            var descriptors = new List<TypeDescriptor>();
            var collected = new HashSet<Type>();
            AddCandidate(baseType, collected, descriptors);
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom(baseType))
                AddCandidate(candidate, collected, descriptors);

            descriptors.Sort(TypeDescriptor.Compare);
            catalog = new TypeCatalog(descriptors);
            CatalogsByBaseType.Add(baseType, catalog);
            return catalog;
        }

        private static void AddCandidate(Type candidate, HashSet<Type> collected, List<TypeDescriptor> descriptors)
        {
            if (!CanCreate(candidate) || !collected.Add(candidate))
                return;

            descriptors.Add(DescribeType(candidate));
        }

        private static bool CanCreate(Type type)
        {
            if (type == null
                || !type.IsClass
                || type.IsAbstract
                || type.IsGenericTypeDefinition
                || type.ContainsGenericParameters
                || !type.IsSerializable
                || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || (type.Namespace != null && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal))
                || type.IsDefined(typeof(ObsoleteAttribute), false))
                return false;

            return type.GetConstructor(
                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                       binder: null,
                       types: Type.EmptyTypes,
                       modifiers: null) != null;
        }

        private static TypeDescriptor DescribeType(Type type)
        {
            if (DescriptorsByType.TryGetValue(type, out TypeDescriptor descriptor))
                return descriptor;

            TypeRegistryItemAttribute registryItem = type.GetCustomAttribute<TypeRegistryItemAttribute>(false);
            string registryName = registryItem?.Name;
            bool registered = !string.IsNullOrWhiteSpace(registryName);
            string groupPath;
            string displayName;
            if (registered)
            {
                string normalized = registryName.Trim().Trim('/');
                int separator = normalized.LastIndexOf('/');
                groupPath = separator > 0 ? normalized.Substring(0, separator) : null;
                displayName = separator >= 0 ? normalized.Substring(separator + 1) : normalized;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = GetDisplayName(type);
            }
            else
            {
                groupPath = UnregisteredGroupName;
                displayName = GetDisplayName(type);
            }

            string assemblyName = type.Assembly.GetName().Name;
            string fullName = type.FullName ?? type.Name;
            string subtitle = registered ? type.Name : "未使用 TypeRegistryItem 登记";
            string tooltip = fullName + "\n程序集：" + assemblyName;
            string keywords = (registryName ?? string.Empty) + " " + type.Name + " " + fullName;
            descriptor = new TypeDescriptor(
                type,
                displayName,
                groupPath,
                subtitle,
                tooltip,
                keywords,
                registered ? null : "未登记");
            DescriptorsByType.Add(type, descriptor);
            return descriptor;
        }

        private static string GetDisplayName(Type type)
        {
            return SplitWords(TrimCommonPrefix(type.Name));
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

        private static void ClearTypeCaches()
        {
            CatalogsByBaseType.Clear();
            DescriptorsByType.Clear();
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
                if (selectedSelectorStyle == null)
                {
                    selectedSelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
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
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    emptySelectorStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.63f, 0.66f, 0.70f, 1f)
                        : new Color(0.38f, 0.41f, 0.45f, 1f);
                }

                return emptySelectorStyle;
            }
        }

        private static GUIStyle WarningSelectorStyle
        {
            get
            {
                if (warningSelectorStyle == null)
                {
                    warningSelectorStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                    warningSelectorStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(1f, 0.66f, 0.35f, 1f)
                        : new Color(0.72f, 0.29f, 0.05f, 1f);
                }

                return warningSelectorStyle;
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
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15,
                        fontStyle = FontStyle.Bold
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
                        fontSize = 16,
                        fontStyle = FontStyle.Bold
                    };
                    clearStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.65f, 0.48f, 0.48f, 1f)
                        : new Color(0.62f, 0.28f, 0.28f, 1f);
                }

                return clearStyle;
            }
        }

        private static GUIStyle MetaStyle
        {
            get { return ESEditorPresentation.MetaStyle; }
        }

        private sealed class TypeCatalog
        {
            public readonly List<TypeDescriptor> Descriptors;

            public TypeCatalog(List<TypeDescriptor> descriptors)
            {
                Descriptors = descriptors;
            }
        }

        private readonly struct TypeDescriptor
        {
            public readonly Type Type;
            public readonly string DisplayName;
            public readonly string GroupPath;
            public readonly string Subtitle;
            public readonly string Tooltip;
            public readonly string Keywords;
            public readonly string Badge;

            public TypeDescriptor(
                Type type,
                string displayName,
                string groupPath,
                string subtitle,
                string tooltip,
                string keywords,
                string badge)
            {
                Type = type;
                DisplayName = displayName;
                GroupPath = groupPath;
                Subtitle = subtitle;
                Tooltip = tooltip;
                Keywords = keywords;
                Badge = badge;
            }

            public static int Compare(TypeDescriptor left, TypeDescriptor right)
            {
                int groupCompare = string.Compare(left.GroupPath, right.GroupPath, StringComparison.Ordinal);
                if (groupCompare != 0)
                    return groupCompare;

                int nameCompare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
                return nameCompare != 0
                    ? nameCompare
                    : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            }
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
        private static bool drawModeInitialized;
        private static ESPolymorphicReferenceDrawMode cachedDrawMode;

        public static bool UseESRenderer => DrawMode == ESPolymorphicReferenceDrawMode.ES;

        public static string CurrentDisplayName => UseESRenderer
            ? "【ES】自定义渲染"
            : "Odin 默认动态渲染";

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

        private static string GetPreferenceKey()
        {
            string projectPath = Application.dataPath ?? "UnknownProject";
            return PreferencePrefix + projectPath.Replace('\\', '/');
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
