using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>
    /// ES 编辑器通用可搜索选择器。
    ///
    /// 这是业务无关的基础组件：ConfigKey、场景、命令、RuntimeWatch、资产和任意
    /// 编辑器对象都可以复用同一套 Entry/Builder API。它只存在于 Editor 程序集。
    /// </summary>
    public sealed class ESSearchDropdown : AdvancedDropdown
    {
        private const float DefaultMinimumWidth = 420f;
        private const float AbsoluteMinimumWidth = 280f;

        public sealed class Builder
        {
            private readonly string title;
            private readonly List<Entry> entries = new List<Entry>();
            private readonly List<ToolbarAction> toolbarActions = new List<ToolbarAction>();
            private Func<IEnumerable<Entry>> provider;

            internal Builder(string title)
            {
                this.title = title;
            }

            /// <summary>添加一个可选项。旧调用方式保持兼容。</summary>
            public Builder Add(
                string label,
                Action onSelected,
                string groupPath = null,
                Texture2D icon = null,
                int id = 0,
                string subtitle = null,
                string tooltip = null,
                string keywords = null,
                string badge = null,
                bool selected = false)
            {
                entries.Add(Entry.Item(label, onSelected, groupPath, icon, id, subtitle, tooltip, keywords, badge, selected));
                return this;
            }

            public Builder AddDisabled(string label, string groupPath = null, string tooltip = null)
            {
                entries.Add(Entry.Disabled(label, groupPath, tooltip));
                return this;
            }

            public Builder AddSeparator(string groupPath = null)
            {
                entries.Add(Entry.Separator(groupPath));
                return this;
            }

            public Builder AddToolbarAction(string label, Action onClick, string tooltip = null)
            {
                toolbarActions.Add(new ToolbarAction(label, onClick, tooltip));
                return this;
            }

            /// <summary>从任意业务集合批量创建选项，不要求业务类型依赖 ESSearchDropdown。</summary>
            public Builder AddRange<T>(
                IEnumerable<T> values,
                Func<T, string> getLabel,
                Action<T> onSelected,
                Func<T, string> getGroupPath = null,
                Func<T, Texture2D> getIcon = null)
            {
                if (values == null || getLabel == null || onSelected == null)
                    return this;
                foreach (T value in values)
                {
                    T captured = value;
                    Add(
                        getLabel(value),
                        () => onSelected(captured),
                        getGroupPath?.Invoke(value),
                        getIcon?.Invoke(value));
                }
                return this;
            }

            /// <summary>
            /// 延迟构建数据。适合资源扫描、场景扫描和大型候选集；只有 Dropdown 真正打开时才执行。
            /// </summary>
            public Builder SetProvider(Func<IEnumerable<Entry>> valueProvider)
            {
                provider = valueProvider;
                return this;
            }

            public void Show(
                Rect anchorRect,
                AdvancedDropdownState state = null,
                Vector2? minimumWindowSize = null)
            {
                Open(
                    anchorRect,
                    title,
                    provider ?? (() => entries),
                    state,
                    minimumWindowSize,
                    toolbarActions);
            }

            /// <summary>UI Toolkit 入口：自动把 VisualElement 锚点转换为 AdvancedDropdown 所需的宿主窗口局部坐标。</summary>
            public void Show(
                VisualElement anchor,
                EditorWindow hostWindow = null,
                AdvancedDropdownState state = null,
                Vector2? minimumWindowSize = null)
            {
                Open(
                    anchor,
                    hostWindow,
                    title,
                    provider ?? (() => entries),
                    state,
                    minimumWindowSize,
                    toolbarActions);
            }

            public IReadOnlyList<Entry> Entries => entries;
        }

        /// <summary>
        /// Small actions rendered in the top-right utility area of the native AdvancedDropdown.
        /// They are intentionally separate from selectable entries.
        /// </summary>
        public sealed class ToolbarAction
        {
            public readonly string Label;
            public readonly string Tooltip;
            public readonly Action OnClick;

            public ToolbarAction(string label, Action onClick, string tooltip = null)
            {
                Label = string.IsNullOrWhiteSpace(label) ? "·" : label.Trim();
                Tooltip = tooltip?.Trim();
                OnClick = onClick;
            }
        }

        public readonly struct Entry
        {
            public readonly string Label;
            public readonly string GroupPath;
            public readonly Texture2D Icon;
            public readonly Action OnSelected;
            public readonly bool Enabled;
            public readonly int Id;
            public readonly bool IsSeparator;
            public readonly string Subtitle;
            public readonly string Tooltip;
            public readonly string Keywords;
            public readonly string Badge;
            public readonly bool Selected;

            public Entry(
                string label,
                Texture2D icon,
                Action onSelected,
                string groupPath = null,
                bool enabled = true,
                int id = 0,
                bool isSeparator = false,
                string subtitle = null,
                string tooltip = null,
                string keywords = null,
                string badge = null,
                bool selected = false)
            {
                Label = string.IsNullOrWhiteSpace(label) ? "<未命名>" : label.Trim();
                GroupPath = groupPath?.Trim().Trim('/');
                Icon = icon;
                OnSelected = onSelected;
                Enabled = enabled;
                IsSeparator = isSeparator;
                Subtitle = subtitle?.Trim();
                Tooltip = tooltip?.Trim();
                Keywords = keywords?.Trim();
                Badge = badge?.Trim();
                Selected = selected;
                Id = id != 0 ? id : StableId(Label, GroupPath, isSeparator);
            }

            public static Entry Disabled(string label, string groupPath = null, string tooltip = null)
                => new Entry(label, null, null, groupPath, enabled: false, tooltip: tooltip);

            public static Entry Separator(string groupPath = null)
                => new Entry(string.Empty, null, null, groupPath, enabled: false, isSeparator: true);

            public static Entry Item(
                string label,
                Action onSelected,
                string groupPath = null,
                Texture2D icon = null,
                int id = 0,
                string subtitle = null,
                string tooltip = null,
                string keywords = null,
                string badge = null,
                bool selected = false)
                => new Entry(label, icon, onSelected, groupPath, id: id, subtitle: subtitle,
                    tooltip: tooltip, keywords: keywords, badge: badge, selected: selected);

            internal Entry WithId(int id)
                => new Entry(Label, Icon, OnSelected, GroupPath, Enabled, id, IsSeparator,
                    Subtitle, Tooltip, Keywords, Badge, Selected);

            private static int StableId(string label, string groupPath, bool separator)
            {
                unchecked
                {
                    uint hash = 2166136261u;
                    string value = (groupPath ?? string.Empty) + "\n" + (label ?? string.Empty) + "\n" + separator;
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                    int id = (int)(hash & 0x7fffffff);
                    return id == 0 ? 1 : id;
                }
            }
        }

        private sealed class ActionItem : AdvancedDropdownItem
        {
            public readonly Entry Entry;

            public ActionItem(Entry entry) : base(FormatLabel(entry))
            {
                Entry = entry;
                icon = entry.Icon;
                enabled = entry.Enabled;
                id = entry.Id;
            }

            private static string FormatLabel(Entry entry)
            {
                string result = (entry.Selected ? "✓ " : string.Empty) + entry.Label;
                if (!string.IsNullOrWhiteSpace(entry.Subtitle)) result += "  ·  " + entry.Subtitle;
                if (!string.IsNullOrWhiteSpace(entry.Badge)) result += "  [" + entry.Badge + "]";
                if (!string.IsNullOrWhiteSpace(entry.Keywords)) result += "  ‹" + entry.Keywords + "›";
                return result;
            }
        }

        private readonly string title;
        private readonly Func<IEnumerable<Entry>> provider;

        public static Builder Create(string title) => new Builder(title);

        private ESSearchDropdown(
            AdvancedDropdownState state,
            string title,
            Func<IEnumerable<Entry>> provider,
            Vector2 minimumWindowSize)
            : base(state ?? new AdvancedDropdownState())
        {
            this.title = string.IsNullOrWhiteSpace(title) ? "选择" : title.Trim();
            this.provider = provider;
            minimumSize = new Vector2(
                Mathf.Max(AbsoluteMinimumWidth, minimumWindowSize.x),
                Mathf.Max(220f, minimumWindowSize.y));
        }

        /// <summary>兼容旧 API：直接传入候选项。</summary>
        public static void Open(
            Rect anchorRect,
            string title,
            IReadOnlyList<Entry> entries,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            Open(anchorRect, title, () => entries, state, minimumWindowSize, toolbarActions);
        }

        /// <summary>UI Toolkit 兼容入口：直接传入候选项，并自动定位所属 EditorWindow。</summary>
        public static void Open(
            VisualElement anchor,
            string title,
            IReadOnlyList<Entry> entries,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            Open(anchor, null, title, () => entries, state, minimumWindowSize, toolbarActions);
        }

        /// <summary>UI Toolkit 兼容入口：使用明确的宿主窗口，避免多窗口停靠时锚点漂移。</summary>
        public static void Open(
            VisualElement anchor,
            EditorWindow hostWindow,
            string title,
            IReadOnlyList<Entry> entries,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            Open(anchor, hostWindow, title, () => entries, state, minimumWindowSize, toolbarActions);
        }

        /// <summary>延迟数据源 API：只在 Dropdown 构建时读取候选项。</summary>
        public static void Open(
            Rect anchorRect,
            string title,
            Func<IEnumerable<Entry>> provider,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            if (provider == null)
                provider = () => Array.Empty<Entry>();

            var dropdown = new ESSearchDropdown(
                state,
                title,
                provider,
                minimumWindowSize ?? new Vector2(DefaultMinimumWidth, 320f));
            dropdown.Show(anchorRect);
            // AdvancedDropdown 在 Show 内部才创建自己的 DataSource/Window/GUI。
            // 因此必须在原生窗口完成初始化后替换 GUI；提前写入会被 Show 覆盖，且首帧永远看不到工具栏。
            if (toolbarActions != null && toolbarActions.Count > 0)
                AdvancedDropdownNativeToolbar.TryInstall(dropdown, toolbarActions);
        }

        /// <summary>UI Toolkit 延迟数据源入口：自动定位所属 EditorWindow。</summary>
        public static void Open(
            VisualElement anchor,
            string title,
            Func<IEnumerable<Entry>> provider,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            Open(anchor, null, title, provider, state, minimumWindowSize, toolbarActions);
        }

        /// <summary>
        /// UI Toolkit 延迟数据源入口。AdvancedDropdown 内部会执行 GUIToScreenRect，
        /// 因此这里传入的是宿主窗口面板局部坐标，不是屏幕坐标。
        /// </summary>
        public static void Open(
            VisualElement anchor,
            EditorWindow hostWindow,
            string title,
            Func<IEnumerable<Entry>> provider,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            if (!TryGetGuiAnchorRect(anchor, hostWindow, out Rect anchorRect))
            {
                Debug.LogWarning("[ESSearchDropdown] 无法打开选择器：锚点尚未加入有效的 EditorWindow 面板。");
                return;
            }

            Open(anchorRect, title, provider, state, minimumWindowSize, toolbarActions);
        }

        private static bool TryGetGuiAnchorRect(VisualElement anchor, EditorWindow preferredHost,
            out Rect anchorRect)
        {
            anchorRect = default;
            if (anchor == null || anchor.panel == null)
                return false;

            EditorWindow host = preferredHost ?? FindHostWindow(anchor);
            VisualElement hostRoot = host != null ? host.rootVisualElement : null;
            if (hostRoot == null || hostRoot.panel == null || !ReferenceEquals(hostRoot.panel, anchor.panel))
                return false;

            Rect worldRect = anchor.worldBound;
            Rect rootWorldRect = hostRoot.worldBound;
            Vector2 localPosition = worldRect.position - rootWorldRect.position;
            if (!IsFinite(localPosition.x) || !IsFinite(localPosition.y)
                || !IsFinite(worldRect.width) || !IsFinite(worldRect.height))
                return false;

            // ToolbarMenu 的回调可能来自另一个临时 GUIView。先算出宿主窗口中的真实屏幕位置，
            // 再转换回“当前 GUIView”的局部坐标，保证 AdvancedDropdown 内部二次 GUIToScreenRect 后仍落在锚点下方。
            Vector2 screenPosition = host.position.position + localPosition;
            Vector2 guiPosition = GUIUtility.ScreenToGUIPoint(screenPosition);
            anchorRect = new Rect(
                guiPosition,
                new Vector2(Mathf.Max(1f, worldRect.width), Mathf.Max(1f, worldRect.height)));
            return true;
        }

        private static EditorWindow FindHostWindow(VisualElement anchor)
        {
            if (anchor == null || anchor.panel == null)
                return null;

            EditorWindow focused = EditorWindow.focusedWindow;
            if (IsHostWindow(focused, anchor))
                return focused;

            EditorWindow mouseOver = EditorWindow.mouseOverWindow;
            if (IsHostWindow(mouseOver, anchor))
                return mouseOver;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
                if (IsHostWindow(windows[i], anchor))
                    return windows[i];
            return null;
        }

        private static bool IsHostWindow(EditorWindow window, VisualElement anchor)
        {
            return window != null
                   && window.rootVisualElement != null
                   && ReferenceEquals(window.rootVisualElement.panel, anchor.panel);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 最简单的业务集合入口。通常只需提供“显示名”和“选择后做什么”。
        /// </summary>
        public static void OpenItems<T>(
            Rect anchorRect,
            string title,
            IEnumerable<T> values,
            Func<T, string> getLabel,
            Action<T> onSelected,
            Func<T, string> getGroupPath = null,
            Func<T, Texture2D> getIcon = null,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null)
        {
            Open(anchorRect, title, () =>
            {
                var entries = new List<Entry>();
                if (values == null || getLabel == null || onSelected == null)
                    return entries;
                foreach (T value in values)
                {
                    T captured = value;
                    entries.Add(Entry.Item(
                        getLabel(value),
                        () => onSelected(captured),
                        getGroupPath?.Invoke(value),
                        getIcon?.Invoke(value)));
                }
                return entries;
            }, state, minimumWindowSize, toolbarActions);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(title);
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };

            IReadOnlyList<Entry> entries = ResolveEntries();
            var usedIds = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (!entry.IsSeparator && !usedIds.Add(entry.Id))
                {
                    int disambiguatedId = entry.Id;
                    while (!usedIds.Add(disambiguatedId)) disambiguatedId++;
                    entry = entry.WithId(disambiguatedId);
                    Debug.LogWarning("[ESSearchDropdown] 检测到重复 Entry Id，已自动修正：" + entry.Label);
                }

                AdvancedDropdownItem parent = ResolveGroup(root, groups, entry.GroupPath);
                if (entry.IsSeparator)
                    parent.AddSeparator();
                else
                    parent.AddChild(new ActionItem(entry));
            }

            if (entries.Count == 0)
                root.AddChild(new AdvancedDropdownItem("没有可选项"));
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (!(item is ActionItem actionItem) || !actionItem.Entry.Enabled)
                return;

            try
            {
                actionItem.Entry.OnSelected?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 选项回调执行失败：" + actionItem.Entry.Label, exception));
            }
        }

        private IReadOnlyList<Entry> ResolveEntries()
        {
            if (provider == null)
                return Array.Empty<Entry>();

            try
            {
                var result = provider();
                if (result == null)
                    return Array.Empty<Entry>();
                return new List<Entry>(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 候选数据提供器执行失败：" + title, exception));
                return new[] { Entry.Disabled("候选数据加载失败，请查看 Console") };
            }
        }

        private static AdvancedDropdownItem ResolveGroup(
            AdvancedDropdownItem root,
            Dictionary<string, AdvancedDropdownItem> groups,
            string groupPath)
        {
            if (string.IsNullOrWhiteSpace(groupPath))
                return root;

            AdvancedDropdownItem parent = root;
            string currentPath = string.Empty;
            foreach (string rawSegment in groupPath.Split('/'))
            {
                string segment = rawSegment.Trim();
                if (segment.Length == 0) continue;
                currentPath = currentPath.Length == 0 ? segment : currentPath + "/" + segment;
                if (!groups.TryGetValue(currentPath, out AdvancedDropdownItem group))
                {
                    group = new AdvancedDropdownItem(segment);
                    groups.Add(currentPath, group);
                    parent.AddChild(group);
                }
                parent = group;
            }
            return parent;
        }
    }

    /// <summary>
    /// Adds optional utility buttons to the native AdvancedDropdownWindow without replacing its IMGUI
    /// renderer. The overlay lives in the same window, above the title row, and leaves the search layout intact.
    /// </summary>
    // 公开仅用于 Unity Mono 动态子类回调；具体状态和安装流程仍保持内部封装。
    public static class AdvancedDropdownNativeToolbar
    {
        private sealed class ToolbarState
        {
            public readonly IReadOnlyList<ESSearchDropdown.ToolbarAction> Actions;
            public readonly object NativeGui;

            public ToolbarState(
                IReadOnlyList<ESSearchDropdown.ToolbarAction> actions,
                object nativeGui)
            {
                Actions = actions;
                NativeGui = nativeGui;
            }
        }

        private static readonly ConditionalWeakTable<object, ToolbarState> States
            = new ConditionalWeakTable<object, ToolbarState>();

        private static readonly ConditionalWeakTable<EditorWindow, VisualElement> OverlayStates
            = new ConditionalWeakTable<EditorWindow, VisualElement>();

        private static bool initialized;
        private static bool available;
        private static FieldInfo guiField;
        private static FieldInfo windowField;
        private static FieldInfo dataSourceField;
        private static FieldInfo stateField;
        private static FieldInfo windowGuiField;
        private static FieldInfo guiDataSourceField;
        private static FieldInfo guiStateField;
        private static FieldInfo searchRectField;
        private static PropertyInfo guiStateProperty;
        private static MethodInfo drawSearchFieldControlMethod;
        private static Type generatedGuiType;
        private static bool failureLogged;

        public static void TryInstall(
            ESSearchDropdown dropdown,
            IReadOnlyList<ESSearchDropdown.ToolbarAction> actions)
        {
            if (dropdown == null || actions == null || actions.Count == 0)
                return;

            // 叠加在原生 AdvancedDropdownWindow 的 rootVisualElement 上，独立于搜索框布局，
            // 因而不会抢搜索框的控制 ID，也不会改变原生窗口的导航和数据源。
            if (TryInstallWindowOverlay(dropdown, actions))
                return;
            // 不再回退到动态替换 AdvancedDropdownGUI：Unity Mono 对 internal 成员的访问不稳定，
            // overlay 失败时宁可保留干净的原生下拉框，也不能破坏搜索和选择流程。
        }

        private static bool TryInstallWindowOverlay(
            ESSearchDropdown dropdown,
            IReadOnlyList<ESSearchDropdown.ToolbarAction> actions)
        {
            try
            {
                FieldInfo nativeWindowField = FindField(typeof(AdvancedDropdown), "m_WindowInstance");
                EditorWindow window = nativeWindowField?.GetValue(dropdown) as EditorWindow;
                if (window == null || window.rootVisualElement == null)
                    return false;

                if (OverlayStates.TryGetValue(window, out VisualElement existing))
                {
                    existing.RemoveFromHierarchy();
                    OverlayStates.Remove(window);
                }

                float width = 4f;
                var widths = new float[actions.Count];
                for (int i = 0; i < actions.Count; i++)
                {
                    ESSearchDropdown.ToolbarAction action = actions[i];
                    widths[i] = Mathf.Max(
                        30f,
                        EditorStyles.toolbarButton.CalcSize(new GUIContent(action?.Label ?? "·")).x);
                    width += widths[i] + 2f;
                }

                var container = new IMGUIContainer
                {
                    name = "es-advanced-dropdown-toolbar",
                    pickingMode = PickingMode.Position,
                    onGUIHandler = () =>
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(width), GUILayout.Height(20f));
                        for (int i = 0; i < actions.Count; i++)
                        {
                            ESSearchDropdown.ToolbarAction action = actions[i];
                            if (action == null)
                                continue;

                            Rect buttonRect = GUILayoutUtility.GetRect(
                                widths[i],
                                18f,
                                GUILayout.Width(widths[i]),
                                GUILayout.Height(18f));
                            if (GUI.Button(
                                    buttonRect,
                                    new GUIContent(action.Label, action.Tooltip),
                                    EditorStyles.toolbarButton))
                            {
                                try
                                {
                                    action.OnClick?.Invoke();
                                    GUI.changed = true;
                                }
                                catch (Exception exception)
                                {
                                    Debug.LogException(new InvalidOperationException(
                                        "[ESSearchDropdown] AdvancedDropdown 工具栏动作执行失败：" + action.Label,
                                        exception));
                                }
                            }

                            GUILayout.Space(2f);
                        }
                        GUILayout.EndHorizontal();
                    }
                };
                container.style.position = Position.Absolute;
                container.style.top = 1f;
                container.style.right = 2f;
                container.style.width = width;
                container.style.height = 20f;
                container.style.overflow = Overflow.Visible;
                container.style.backgroundColor = Color.clear;
                window.rootVisualElement.Add(container);
                OverlayStates.Add(window, container);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESSearchDropdown] 顶部工具栏 overlay 安装失败，保留原生下拉框：" + exception.Message);
                return false;
            }
        }

        private static bool EnsureInitialized()
        {
            if (initialized)
                return available;

            initialized = true;
            try
            {
                Type dropdownType = typeof(AdvancedDropdown);
                guiField = FindGuiField(dropdownType);
                if (guiField == null)
                    return Fail("找不到 AdvancedDropdown.m_Gui。", logAsWarning: true);

                windowField = FindField(dropdownType, "m_WindowInstance");
                dataSourceField = FindField(dropdownType, "m_DataSource");
                stateField = FindField(dropdownType, "m_State");
                if (windowField == null || dataSourceField == null || stateField == null)
                    return Fail("找不到 AdvancedDropdown 的 Window/DataSource/State 字段。", logAsWarning: true);

                Type guiType = guiField.FieldType;
                Type windowType = windowField.FieldType;
                windowGuiField = FindField(windowType, "m_Gui");
                if (windowGuiField == null)
                    return Fail("找不到 AdvancedDropdownWindow.m_Gui。", logAsWarning: true);
                searchRectField = guiType.GetField(
                    "m_SearchRect",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                guiDataSourceField = FindField(guiType, "m_DataSource");
                guiStateField = FindField(guiType, "<state>k__BackingField");
                guiStateProperty = guiType.GetProperty(
                    "state",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                drawSearchFieldControlMethod = guiType.GetMethod(
                    "DrawSearchFieldControl",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (drawSearchFieldControlMethod == null || !drawSearchFieldControlMethod.IsVirtual || searchRectField == null)
                    return Fail("AdvancedDropdownGUI 缺少可扩展的 DrawSearchFieldControl 或 m_SearchRect。", logAsWarning: true);

                ConstructorInfo baseConstructor = guiType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { dataSourceField.FieldType },
                    modifiers: null);
                if (baseConstructor == null)
                    return Fail("找不到 AdvancedDropdownGUI(DataSource) 构造函数。", logAsWarning: true);

                var assemblyName = new AssemblyName("ES.AdvancedDropdownToolbar.Dynamic");
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                    assemblyName,
                    AssemblyBuilderAccess.Run);
                ConstructorInfo ignoreAccessCtor = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                    .GetConstructor(new[] { typeof(string) });
                if (ignoreAccessCtor != null)
                {
                    assembly.SetCustomAttribute(new CustomAttributeBuilder(
                        ignoreAccessCtor,
                        new object[] { guiType.Assembly.GetName().Name }));
                    // 动态方法还需要回调本类的内部绘制桥接函数。
                    assembly.SetCustomAttribute(new CustomAttributeBuilder(
                        ignoreAccessCtor,
                        new object[] { typeof(AdvancedDropdownNativeToolbar).Assembly.GetName().Name }));
                }
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name);
                TypeBuilder typeBuilder = module.DefineType(
                    "ESAdvancedDropdownToolbarGUI",
                    TypeAttributes.Public | TypeAttributes.Class,
                    guiType);

                ConstructorBuilder constructor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    new[] { dataSourceField.FieldType });
                ILGenerator constructorIl = constructor.GetILGenerator();
                constructorIl.Emit(OpCodes.Ldarg_0);
                constructorIl.Emit(OpCodes.Ldarg_1);
                constructorIl.Emit(OpCodes.Call, baseConstructor);
                constructorIl.Emit(OpCodes.Ret);

                ParameterInfo[] parameters = drawSearchFieldControlMethod.GetParameters();
                var parameterTypes = new Type[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                    parameterTypes[i] = parameters[i].ParameterType;

                MethodBuilder overrideMethod = typeBuilder.DefineMethod(
                    drawSearchFieldControlMethod.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    drawSearchFieldControlMethod.ReturnType,
                    parameterTypes);
                ILGenerator il = overrideMethod.GetILGenerator();
                il.DeclareLocal(drawSearchFieldControlMethod.ReturnType);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(
                    OpCodes.Call,
                    typeof(AdvancedDropdownNativeToolbar).GetMethod(
                        nameof(InvokeNativeSearchFieldControl),
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
                il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(
                    OpCodes.Call,
                    typeof(AdvancedDropdownNativeToolbar).GetMethod(
                        nameof(DrawHeaderButtonsInSearchRow),
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(overrideMethod, drawSearchFieldControlMethod);

                generatedGuiType = typeBuilder.CreateType();
                available = generatedGuiType != null;
                if (!available)
                    return Fail("动态 GUI 类型创建结果为空。", logAsWarning: true);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] Unity AdvancedDropdownGUI 内部扩展不可用，已保留原生下拉框。",
                    exception));
                available = false;
            }

            return available;
        }

        private static void InitializeGuiInstance(
            object gui,
            object dataSource,
            object state)
        {
            if (guiDataSourceField != null)
                guiDataSourceField.SetValue(gui, dataSource);

            if (guiStateField != null)
            {
                guiStateField.SetValue(gui, state);
                return;
            }

            if (guiStateProperty != null)
            {
                try
                {
                    guiStateProperty.SetValue(gui, state, null);
                }
                catch (Exception stateException)
                {
                    Debug.LogWarning("[ESSearchDropdown] 无法复制 AdvancedDropdown 状态，工具栏仍会保留：" + stateException.Message);
                }
            }
        }

        // 不能让动态程序集直接 call internal DrawSearchFieldControl，Unity Mono 会在运行时拒绝。
        // 使用一个独立的原生 GUI 实例反射调用基类方法，再把搜索矩形复制回动态 GUI。
        public static string InvokeNativeSearchFieldControl(object generatedGui, string value)
        {
            if (!States.TryGetValue(generatedGui, out ToolbarState state)
                || state.NativeGui == null
                || drawSearchFieldControlMethod == null)
                return value;

            try
            {
                string result = drawSearchFieldControlMethod.Invoke(
                    state.NativeGui,
                    new object[] { value }) as string;
                if (searchRectField != null)
                    searchRectField.SetValue(generatedGui, searchRectField.GetValue(state.NativeGui));
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 无法调用 Unity 原生 AdvancedDropdown 搜索绘制。",
                    exception));
                return value;
            }
        }

        private static FieldInfo FindGuiField(Type dropdownType)
        {
            for (Type type = dropdownType; type != null; type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.Name.IndexOf("gui", StringComparison.OrdinalIgnoreCase) >= 0
                        || field.FieldType.Name.IndexOf("AdvancedDropdownGUI", StringComparison.OrdinalIgnoreCase) >= 0)
                        return field;
                }
            }

            return null;
        }

        private static FieldInfo FindField(Type declaringType, string name)
        {
            for (Type type = declaringType; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static bool Fail(string message, bool logAsWarning)
        {
            if (!failureLogged)
            {
                failureLogged = true;
                if (logAsWarning)
                    Debug.LogWarning("[ESSearchDropdown] 原生 AdvancedDropdown 工具栏不可用：" + message);
                else
                    Debug.Log("[ESSearchDropdown] " + message);
            }

            available = false;
            return false;
        }

        public static void DrawHeaderButtonsInSearchRow(object gui)
        {
            if (!States.TryGetValue(gui, out ToolbarState state)
                || state.Actions == null
                || state.Actions.Count == 0
                || searchRectField == null)
                return;

            if (Event.current == null)
                return;

            if (!(searchRectField.GetValue(gui) is Rect searchRect)
                || searchRect.width <= 0f
                || searchRect.height <= 0f)
                return;

            float totalWidth = 4f;
            for (int i = 0; i < state.Actions.Count; i++)
            {
                ESSearchDropdown.ToolbarAction action = state.Actions[i];
                if (action == null)
                    continue;
                totalWidth += Mathf.Max(
                    28f,
                    EditorStyles.toolbarButton.CalcSize(new GUIContent(action.Label)).x);
                totalWidth += 2f;
            }

            float x = searchRect.xMax - totalWidth;
            for (int i = 0; i < state.Actions.Count; i++)
            {
                ESSearchDropdown.ToolbarAction action = state.Actions[i];
                if (action == null)
                    continue;

                float width = Mathf.Max(
                    28f,
                    EditorStyles.toolbarButton.CalcSize(new GUIContent(action.Label)).x);
                Rect buttonRect = new Rect(x, searchRect.y + 1f, width, searchRect.height - 2f);
                if (GUI.Button(
                        buttonRect,
                        new GUIContent(action.Label, action.Tooltip),
                        EditorStyles.toolbarButton))
                {
                    try
                    {
                        action.OnClick?.Invoke();
                        GUI.changed = true;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(new InvalidOperationException(
                            "[ESSearchDropdown] AdvancedDropdown 工具栏动作执行失败：" + action.Label,
                            exception));
                    }
                }

                x += width + 2f;
            }
        }
    }

}

// Unity 2022.3 将 AdvancedDropdownGUI 声明为 internal。动态子类需要这个标准兼容属性
// 才能在 Mono/.NET Framework 下 override 它的 internal virtual DrawItem。
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
