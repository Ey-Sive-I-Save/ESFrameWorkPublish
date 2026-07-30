using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

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
                Open(anchorRect, title, provider ?? (() => entries), state, minimumWindowSize);
            }

            public IReadOnlyList<Entry> Entries => entries;
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
            Vector2? minimumWindowSize = null)
        {
            Open(anchorRect, title, () => entries, state, minimumWindowSize);
        }

        /// <summary>延迟数据源 API：只在 Dropdown 构建时读取候选项。</summary>
        public static void Open(
            Rect anchorRect,
            string title,
            Func<IEnumerable<Entry>> provider,
            AdvancedDropdownState state = null,
            Vector2? minimumWindowSize = null)
        {
            if (provider == null)
                provider = () => Array.Empty<Entry>();

            var dropdown = new ESSearchDropdown(
                state,
                title,
                provider,
                minimumWindowSize ?? new Vector2(DefaultMinimumWidth, 320f));
            dropdown.Show(anchorRect);
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
            Vector2? minimumWindowSize = null)
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
            }, state, minimumWindowSize);
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
                return result == null ? Array.Empty<Entry>() : new List<Entry>(result);
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
}
