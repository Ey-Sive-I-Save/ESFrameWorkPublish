using System;
using System.Collections.Generic;
using System.Reflection;
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
        private const float MaximumMinimumWidth = 2400f;
        private const float MaximumMinimumHeight = 1600f;
        // Provider 数据来自编辑器扫描或外部适配器时，必须有界，避免错误枚举导致 UI 长时间无响应。
        private const int MaximumResolvedEntries = 10000;
        internal const int MaximumToolbarActions = 32;
        private const int MaximumGroupPathCharacters = 4096;
        private const int MaximumGroupPathSegments = 64;
        private const int MaximumGroupSegmentCharacters = 256;
        private const int MaximumToolbarTextCharacters = 512;
        private const int MaximumEntryPresentationCharacters = 2048;
        private static readonly PropertyInfo NativeTooltipProperty = FindNativeTooltipProperty();
        private static readonly Action<AdvancedDropdownItem, string> NativeTooltipSetter =
            CreateNativeTooltipSetter(NativeTooltipProperty);
        private static bool nativeTooltipFailureLogged;

        private static void DisposeInteractionHold(IDisposable interactionHold)
        {
            if (interactionHold == null)
                return;
            try
            {
                interactionHold.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 宿主交互保持释放失败。", exception));
            }
        }

        public sealed class Builder
        {
            private readonly string title;
            private readonly List<Entry> entries = new List<Entry>();
            private readonly List<ToolbarAction> toolbarActions = new List<ToolbarAction>();
            private Func<IEnumerable<Entry>> provider;
            private bool entryLimitMarkerAdded;

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
                AddBounded(Entry.Item(label, onSelected, groupPath, icon, id, subtitle, tooltip, keywords, badge, selected));
                return this;
            }

            public Builder AddDisabled(string label, string groupPath = null, string tooltip = null)
            {
                AddBounded(Entry.Disabled(label, groupPath, tooltip));
                return this;
            }

            public Builder AddSeparator(string groupPath = null)
            {
                AddBounded(Entry.Separator(groupPath));
                return this;
            }

            private void AddBounded(Entry entry)
            {
                if (entries.Count < MaximumResolvedEntries)
                {
                    entries.Add(entry);
                    return;
                }

                if (entryLimitMarkerAdded)
                    return;

                entryLimitMarkerAdded = true;
                entries.Add(Entry.Disabled(
                    "候选项过多，已限制为 " + MaximumResolvedEntries + " 项"));
            }

            public Builder AddToolbarAction(string label, Action onClick, string tooltip = null)
            {
                if (toolbarActions.Count >= MaximumToolbarActions)
                    return this;
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
                if (values is ICollection<T> collection)
                    entries.Capacity = Mathf.Min(
                        MaximumResolvedEntries + 1,
                        Mathf.Max(entries.Count, collection.Count));
                int count = 0;
                try
                {
                    foreach (T value in values)
                    {
                        if (count >= MaximumResolvedEntries)
                        {
                            AddDisabled(
                                "候选项过多，已限制为 " + MaximumResolvedEntries + " 项");
                            break;
                        }

                        T captured = value;
                        Add(
                            getLabel(value),
                            () => onSelected(captured),
                            getGroupPath?.Invoke(value),
                            getIcon?.Invoke(value));
                        count++;
                    }
                }
                catch (Exception exception)
                {
                    AddDisabled("候选数据加载失败，请查看 Console");
                    Debug.LogException(new InvalidOperationException(
                        "[ESSearchDropdown] AddRange 数据构建失败。", exception));
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
                Vector2? minimumWindowSize = null,
                EditorWindow hostWindow = null)
            {
                Open(
                    anchorRect,
                    title,
                    provider ?? (() => entries),
                    state,
                    minimumWindowSize,
                    toolbarActions,
                    hostWindow);
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
                Label = NormalizeToolbarText(label, "·");
                Tooltip = NormalizeToolbarText(tooltip, null);
                OnClick = onClick;
            }
        }

        private static string NormalizeToolbarText(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (string.IsNullOrEmpty(normalized))
                return null;
            return normalized.Length > MaximumToolbarTextCharacters
                ? normalized.Substring(0, MaximumToolbarTextCharacters)
                : normalized;
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
                    // 保持旧字符串拼接算法的字节序列完全一致，但避免每个 Entry
                    // 为稳定 Id 创建一次临时组合字符串。separator 的 ToString() 结果
                    // 是首字母大写的 "True"/"False"，这里显式展开以保留既有身份。
                    hash = AppendHash(hash, groupPath);
                    hash = AppendHashChar(hash, '\n');
                    hash = AppendHash(hash, label);
                    hash = AppendHashChar(hash, '\n');
                    hash = separator
                        ? AppendHashLiteral(hash, "True")
                        : AppendHashLiteral(hash, "False");
                    int id = (int)(hash & 0x7fffffff);
                    return id == 0 ? 1 : id;
                }
            }

            private static uint AppendHash(uint hash, string value)
            {
                return AppendHashLiteral(hash, value ?? string.Empty);
            }

            private static uint AppendHashLiteral(uint hash, string value)
            {
                for (int i = 0; i < value.Length; i++)
                    hash = AppendHashChar(hash, value[i]);
                return hash;
            }

            private static uint AppendHashChar(uint hash, char value)
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private sealed class ActionItem : AdvancedDropdownItem
        {
            public readonly Entry Entry;

            public ActionItem(Entry entry) : base(FormatLabel(entry))
            {
                Entry = entry;
                if (!string.IsNullOrWhiteSpace(entry.Tooltip))
                    ApplyNativeTooltip(this, BoundPresentationText(entry.Tooltip));
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
                return BoundPresentationText(result);
            }

        }

        private static string BoundPresentationText(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaximumEntryPresentationCharacters)
                return value;
            return value.Substring(0, MaximumEntryPresentationCharacters);
        }

        private readonly string title;
        private readonly Func<IEnumerable<Entry>> provider;
        private readonly EditorWindow hostWindow;
        private readonly VisualElement hostRoot;
        private bool selectionEnabled = true;

        public static Builder Create(string title) => new Builder(title);

        private static PropertyInfo FindNativeTooltipProperty()
        {
            try
            {
                for (Type type = typeof(AdvancedDropdownItem); type != null; type = type.BaseType)
                {
                    PropertyInfo property = type.GetProperty(
                        "tooltip",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (property?.GetSetMethod(true) != null)
                        return property;
                }
            }
            catch (Exception) { }
            return null;
        }

        private static Action<AdvancedDropdownItem, string> CreateNativeTooltipSetter(
            PropertyInfo property)
        {
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null)
                return null;

            try
            {
                return (Action<AdvancedDropdownItem, string>)Delegate.CreateDelegate(
                    typeof(Action<AdvancedDropdownItem, string>),
                    null,
                    setter);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (MemberAccessException)
            {
                return null;
            }
            catch (Exception)
            {
                // Unity 版本差异可能抛出其他反射异常；Tooltip 只是增强能力，
                // 不能让整个 SearchDropdown 类型初始化失败。
                return null;
            }
        }

        private static void ApplyNativeTooltip(AdvancedDropdownItem item, string value)
        {
            try
            {
                if (NativeTooltipSetter != null)
                    NativeTooltipSetter(item, value);
                else
                    NativeTooltipProperty?.SetValue(item, value, null);
            }
            catch (Exception exception)
            {
                if (nativeTooltipFailureLogged)
                    return;
                nativeTooltipFailureLogged = true;
                Debug.LogWarning("[ESSearchDropdown] 无法写入原生条目 Tooltip：" + exception.Message);
            }
        }

        private ESSearchDropdown(
            AdvancedDropdownState state,
            string title,
            Func<IEnumerable<Entry>> provider,
            Vector2 minimumWindowSize,
            EditorWindow hostWindow = null)
            : base(state ?? new AdvancedDropdownState())
        {
            this.title = string.IsNullOrWhiteSpace(title) ? "选择" : title.Trim();
            this.provider = provider;
            this.hostWindow = hostWindow;
            this.hostRoot = hostWindow != null ? hostWindow.rootVisualElement : null;
            minimumWindowSize = NormalizeMinimumWindowSize(minimumWindowSize);
            minimumSize = new Vector2(
                Mathf.Max(AbsoluteMinimumWidth, minimumWindowSize.x),
                Mathf.Max(220f, minimumWindowSize.y));
        }

        private static Vector2 NormalizeMinimumWindowSize(Vector2 requested)
        {
            float width = IsFinite(requested.x) ? requested.x : DefaultMinimumWidth;
            float height = IsFinite(requested.y) ? requested.y : 320f;
            return new Vector2(
                Mathf.Clamp(width, AbsoluteMinimumWidth, MaximumMinimumWidth),
                Mathf.Clamp(height, 220f, MaximumMinimumHeight));
        }

        /// <summary>兼容旧 API：直接传入候选项。</summary>
        public static void Open(
            Rect anchorRect,
            string title,
            IReadOnlyList<Entry> entries,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null,
            IReadOnlyList<ToolbarAction> toolbarActions = null,
            EditorWindow hostWindow = null)
        {
            Open(anchorRect, title, () => entries, state, minimumWindowSize, toolbarActions, hostWindow);
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
            IReadOnlyList<ToolbarAction> toolbarActions = null,
            EditorWindow hostWindow = null)
        {
            if (provider == null)
                provider = () => Array.Empty<Entry>();

            var dropdown = new ESSearchDropdown(
                state,
                title,
                provider,
                minimumWindowSize ?? new Vector2(DefaultMinimumWidth, 320f),
                hostWindow);
            IDisposable interactionHold = hostWindow != null
                ? ESWindowFoundation.HoldInteraction(hostWindow, "ESSearchDropdown")
                : null;
            try
            {
                dropdown.Show(anchorRect);
            }
            catch
            {
                DisposeInteractionHold(interactionHold);
                throw;
            }

            // AdvancedDropdown 在 Show 内部才创建原生窗口。生命周期和可选工具栏都挂到
            // 该窗口的 DetachFromPanelEvent，不增加 EditorApplication.update 常驻轮询。
            bool hasToolbarActions = false;
            try
            {
                hasToolbarActions = toolbarActions != null && toolbarActions.Count > 0;
            }
            catch (Exception exception)
            {
                DisposeInteractionHold(interactionHold);
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] ToolbarAction 集合计数失败，已放弃原生桥接。", exception));
                return;
            }

            if (interactionHold != null || hasToolbarActions)
                AdvancedDropdownNativeBridge.TryAttach(dropdown, toolbarActions, interactionHold);
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
            EditorWindow resolvedHost = hostWindow ?? FindHostWindow(anchor);
            if (!TryGetGuiAnchorRect(anchor, resolvedHost, out Rect anchorRect))
            {
                Debug.LogWarning("[ESSearchDropdown] 无法打开选择器：锚点尚未加入有效的 EditorWindow 面板。");
                return;
            }

            Open(anchorRect, title, provider, state, minimumWindowSize, toolbarActions, resolvedHost);
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
            Rect hostBounds = host.position;
            if (hostBounds.width > 0f && hostBounds.height > 0f)
            {
                float anchorWidth = Mathf.Max(1f, worldRect.width);
                float anchorHeight = Mathf.Max(1f, worldRect.height);
                screenPosition.x = Mathf.Clamp(
                    screenPosition.x,
                    hostBounds.x,
                    Mathf.Max(hostBounds.x, hostBounds.xMax - anchorWidth));
                screenPosition.y = Mathf.Clamp(
                    screenPosition.y,
                    hostBounds.y,
                    Mathf.Max(hostBounds.y, hostBounds.yMax - anchorHeight));
            }
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
            IReadOnlyList<ToolbarAction> toolbarActions = null,
            EditorWindow hostWindow = null)
        {
            Open(anchorRect, title, () =>
            {
                int capacity = values is ICollection<T> collection
                    ? Mathf.Min(MaximumResolvedEntries + 1, collection.Count)
                    : 0;
                var entries = capacity > 0 ? new List<Entry>(capacity) : new List<Entry>();
                if (values == null || getLabel == null || onSelected == null)
                    return entries;
                int count = 0;
                foreach (T value in values)
                {
                    if (count >= MaximumResolvedEntries)
                    {
                        entries.Add(Entry.Disabled(
                            "候选项过多，已限制为 " + MaximumResolvedEntries + " 项"));
                        break;
                    }
                    T captured = value;
                    entries.Add(Entry.Item(
                        getLabel(value),
                        () => onSelected(captured),
                        getGroupPath?.Invoke(value),
                        getIcon?.Invoke(value)));
                    count++;
                }
                return entries;
            }, state, minimumWindowSize, toolbarActions, hostWindow);
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
                // Entry is a readonly struct, so a provider cannot yield null.
                // Treat default(Entry) as invalid while preserving valid separators
                // whose label is intentionally an empty string.
                if (entry.Label == null)
                {
                    root.AddChild(new AdvancedDropdownItem("候选项无效，已跳过"));
                    continue;
                }
                if (!entry.IsSeparator && !usedIds.Add(entry.Id))
                {
                    int disambiguatedId = entry.Id;
                    bool assigned = false;
                    for (int attempts = 0; attempts <= MaximumResolvedEntries; attempts++)
                    {
                        if (usedIds.Add(disambiguatedId))
                        {
                            assigned = true;
                            break;
                        }
                        disambiguatedId = disambiguatedId == int.MaxValue
                            ? int.MinValue
                            : disambiguatedId + 1;
                    }
                    if (!assigned)
                    {
                        root.AddChild(new AdvancedDropdownItem("候选项 ID 冲突，已跳过"));
                        continue;
                    }
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
            bool hostContextValid = IsHostContextValid();
            if (!selectionEnabled || !hostContextValid)
            {
                if (!hostContextValid)
                    DisableSelection();
                return;
            }
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

        internal void DisableSelection()
        {
            selectionEnabled = false;
        }

        internal bool IsHostContextValidForBridge()
        {
            return IsHostContextValid();
        }

        private bool IsHostContextValid()
        {
            if (hostWindow == null)
                return true;
            if (hostRoot == null || hostWindow.rootVisualElement == null)
                return false;
            try
            {
                return hostRoot.panel != null
                    && ReferenceEquals(hostRoot, hostWindow.rootVisualElement)
                    && hostWindow.rootVisualElement.panel == hostRoot.panel;
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 宿主上下文校验失败，已禁用选择回调。", exception));
                return false;
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

                int capacity = result is ICollection<Entry> collection
                    ? Mathf.Min(MaximumResolvedEntries + 1, collection.Count)
                    : 0;
                var resolved = capacity > 0 ? new List<Entry>(capacity) : new List<Entry>();
                bool truncated = false;
                foreach (Entry entry in result)
                {
                    if (resolved.Count >= MaximumResolvedEntries)
                    {
                        truncated = true;
                        break;
                    }
                    resolved.Add(entry);
                }

                if (truncated)
                {
                    resolved.Add(Entry.Disabled(
                        "候选项过多，已限制为 " + MaximumResolvedEntries + " 项"));
                }
                return resolved;
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
            if (groupPath.Length > MaximumGroupPathCharacters)
                return root;

            AdvancedDropdownItem parent = root;
            string currentPath = string.Empty;
            int segmentCount = 0;
            foreach (string rawSegment in groupPath.Split('/'))
            {
                if (segmentCount++ >= MaximumGroupPathSegments)
                    break;
                string segment = rawSegment.Trim();
                if (segment.Length == 0) continue;
                if (segment.Length > MaximumGroupSegmentCharacters)
                    segment = segment.Substring(0, MaximumGroupSegmentCharacters);
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
    /// Connects the native AdvancedDropdownWindow to ES host lifetime and optional toolbar UI.
    /// Reflection is resolved once per domain; all live state is released by DetachFromPanelEvent.
    /// </summary>
    internal static class AdvancedDropdownNativeBridge
    {
        private sealed class ToolbarOverlay
        {
            private readonly ESSearchDropdown.ToolbarAction[] actions;
            private readonly GUIContent[] contents;
            private readonly float[] widths;
            private readonly System.Func<bool> contextValidator;
            private bool disposed;

            internal ToolbarOverlay(
                IReadOnlyList<ESSearchDropdown.ToolbarAction> source,
                System.Func<bool> contextValidator)
            {
                this.contextValidator = contextValidator;
                int sourceCount = Mathf.Min(
                    source?.Count ?? 0,
                    ESSearchDropdown.MaximumToolbarActions);
                actions = new ESSearchDropdown.ToolbarAction[sourceCount];
                contents = new GUIContent[sourceCount];
                widths = new float[sourceCount];

                float totalWidth = 4f;
                int count = 0;
                for (int i = 0; i < sourceCount; i++)
                {
                    ESSearchDropdown.ToolbarAction action = source[i];
                    if (action == null)
                        continue;

                    actions[count] = action;
                    contents[count] = new GUIContent(action.Label, action.Tooltip);
                    widths[count] = EstimateButtonWidth(action.Label);
                    totalWidth += widths[count] + 2f;
                    count++;
                }

                Count = count;
                Width = totalWidth;
                Element = new IMGUIContainer { onGUIHandler = Draw };
                Element.name = "es-advanced-dropdown-toolbar";
                Element.pickingMode = PickingMode.Position;
                Element.style.position = Position.Absolute;
                Element.style.top = 1f;
                Element.style.right = 2f;
                Element.style.width = Width;
                Element.style.height = 20f;
                Element.style.overflow = Overflow.Visible;
                Element.style.backgroundColor = Color.clear;
            }

            internal int Count { get; }
            internal float Width { get; }
            internal IMGUIContainer Element { get; }

            internal void Dispose()
            {
                disposed = true;
            }

            private void Draw()
            {
                if (disposed)
                    return;
                if (contextValidator != null && !contextValidator())
                {
                    disposed = true;
                    return;
                }
                float x = 2f;
                for (int i = 0; i < Count; i++)
                {
                    Rect buttonRect = new Rect(x, 1f, widths[i], 18f);
                    if (GUI.Button(buttonRect, contents[i], EditorStyles.toolbarButton))
                    {
                        try
                        {
                            actions[i].OnClick?.Invoke();
                            GUI.changed = true;
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(new InvalidOperationException(
                                "[ESSearchDropdown] AdvancedDropdown 工具栏动作执行失败：" + actions[i].Label,
                                exception));
                        }
                    }
                    x += widths[i] + 2f;
                }
            }

            private static float EstimateButtonWidth(string label)
            {
                float width = 16f;
                string value = string.IsNullOrEmpty(label) ? "·" : label;
                for (int i = 0; i < value.Length; i++)
                    width += value[i] <= 0x7f ? 7f : 13f;
                return Mathf.Clamp(width, 30f, 120f);
            }
        }

        private sealed class WindowState : IDisposable
        {
            private readonly ESSearchDropdown dropdown;
            private readonly EditorWindow window;
            private readonly VisualElement root;
            private readonly IDisposable interactionHold;
            private readonly ToolbarOverlay toolbar;
            private bool disposed;

            internal WindowState(
                ESSearchDropdown dropdown,
                EditorWindow window,
                VisualElement root,
                IDisposable interactionHold,
                ToolbarOverlay toolbar)
            {
                this.dropdown = dropdown;
                this.window = window;
                this.root = root;
                this.interactionHold = interactionHold;
                this.toolbar = toolbar;
            }

            internal void Attach()
            {
                root.RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
                if (toolbar != null && toolbar.Count > 0)
                    root.Add(toolbar.Element);
                if (disposed)
                    throw new InvalidOperationException("AdvancedDropdown 原生窗口在桥接期间已关闭。");
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                dropdown?.DisableSelection();
                try
                {
                    root?.UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "AdvancedDropdown 原生窗口回调注销失败。", exception));
                }
                try
                {
                    toolbar?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "AdvancedDropdown 工具栏回调失效处理失败。", exception));
                }
                try
                {
                    toolbar?.Element.RemoveFromHierarchy();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "AdvancedDropdown 工具栏移除失败。", exception));
                }
                try
                {
                    interactionHold?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "AdvancedDropdown 宿主交互保持释放失败。", exception));
                }
                finally
                {
                    // UnityEngine.Object 被销毁后会让 operator != 返回 false，
                    // 但 ConditionalWeakTable 仍需要使用托管引用完成移除。
                    if (!ReferenceEquals(window, null))
                        WindowStates.Remove(window);
                }
            }

            private void OnDetachedFromPanel(DetachFromPanelEvent evt)
            {
                Dispose();
            }
        }

        private static readonly FieldInfo NativeWindowField =
            FindField(typeof(AdvancedDropdown), "m_WindowInstance");
        private static readonly ConditionalWeakTable<EditorWindow, WindowState> WindowStates =
            new ConditionalWeakTable<EditorWindow, WindowState>();
        private static bool failureLogged;

        internal static bool TryAttach(
            ESSearchDropdown dropdown,
            IReadOnlyList<ESSearchDropdown.ToolbarAction> actions,
            IDisposable interactionHold)
        {
            WindowState attachedState = null;
            try
            {
                EditorWindow window = NativeWindowField?.GetValue(dropdown) as EditorWindow;
                VisualElement root = window != null ? window.rootVisualElement : null;
                if (window == null || root == null)
                {
                    DisposeInteractionHold(interactionHold);
                    LogFailureOnce("找不到原生 AdvancedDropdownWindow，宿主交互保持已安全释放。");
                    return false;
                }

                if (WindowStates.TryGetValue(window, out WindowState existing))
                    existing.Dispose();

                ToolbarOverlay toolbar = actions != null && actions.Count > 0
                    ? new ToolbarOverlay(actions, dropdown != null
                        ? dropdown.IsHostContextValidForBridge
                        : null)
                    : null;
                attachedState = new WindowState(dropdown, window, root, interactionHold, toolbar);
                WindowStates.Add(window, attachedState);
                attachedState.Attach();
                return true;
            }
            catch (Exception exception)
            {
                if (attachedState != null)
                    attachedState.Dispose();
                else
                    DisposeInteractionHold(interactionHold);
                LogFailureOnce("原生窗口桥接失败，已保留下拉选择流程：" + exception.Message);
                return false;
            }
        }

        private static void DisposeInteractionHold(IDisposable interactionHold)
        {
            if (interactionHold == null)
                return;
            try
            {
                interactionHold.Dispose();
            }
            catch (Exception exception)
            {
                // Cleanup is best-effort and must never replace the original
                // dropdown/bridge failure or escape an editor callback.
                Debug.LogException(new InvalidOperationException(
                    "[ESSearchDropdown] 宿主交互保持释放失败。", exception));
            }
        }

        private static FieldInfo FindField(Type declaringType, string name)
        {
            try
            {
                for (Type type = declaringType; type != null; type = type.BaseType)
                {
                    FieldInfo field = type.GetField(
                        name,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return field;
                }
            }
            catch (Exception) { }
            return null;
        }

        private static void LogFailureOnce(string message)
        {
            if (failureLogged)
                return;
            failureLogged = true;
            Debug.LogWarning("[ESSearchDropdown] " + message);
        }
    }
}
