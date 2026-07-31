using System;
using System.Text;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// Declares the editor content section that owns a field, property, or button method.
    /// The editor-only drawer uses this metadata to provide section navigation
    /// without changing runtime data or Odin's normal property rendering.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ESEditorSectionAttribute : PropertyGroupAttribute
    {
        internal const string GroupRoot = "__ESEditorSection";
        public const string DefaultNavigatorId = "default";

        public readonly string NavigatorId;
        public readonly string SectionId;
        public readonly string DisplayName;
        public string Subtitle { get; private set; }
        /// <summary>True only for the parameterless shorthand <c>[ESEditorSection]</c>.</summary>
        public bool IsContinuation { get; }

        /// <summary>
        /// Continues the closest preceding active <see cref="ESEditorBeginSectionAttribute"/>
        /// or explicit ESEditorSection declaration. Odin expands this editor shorthand before
        /// it builds PropertyGroup metadata.
        /// </summary>
        public ESEditorSectionAttribute()
            : base(GroupRoot + "__continuation", 0f)
        {
            NavigatorId = DefaultNavigatorId;
            SectionId = null;
            DisplayName = null;
            Subtitle = null;
            IsContinuation = true;
            GroupName = "继续上一个分区";
        }

        /// <summary>
        /// 简化声明：分区 ID 由显示名稳定生成，适合不需要跨版本自定义 ID 的普通字段。
        /// 需要稳定业务 ID、多个导航目录或迁移兼容时，继续使用完整构造函数。
        /// </summary>
        public ESEditorSectionAttribute(string displayName, float order = 0f, string subtitle = null)
            : this(
                DefaultNavigatorId,
                BuildImplicitSectionId(displayName),
                displayName,
                order,
                subtitle)
        {
        }

        public ESEditorSectionAttribute(string sectionId, string displayName, float order = 0f, string subtitle = null)
            : this(DefaultNavigatorId, sectionId, displayName, order, subtitle)
        {
        }

        public ESEditorSectionAttribute(
            string navigatorId,
            string sectionId,
            string displayName,
            float order = 0f,
            string subtitle = null)
            : base(BuildGroupId(navigatorId, sectionId), order)
        {
            NavigatorId = NormalizeNavigatorId(navigatorId);
            if (string.IsNullOrWhiteSpace(sectionId))
                throw new ArgumentException("编辑器内容分区 ID 不能为空。", nameof(sectionId));

            SectionId = sectionId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SectionId : displayName.Trim();
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
            GroupName = DisplayName;
        }

        private static string NormalizeNavigatorId(string navigatorId)
        {
            return string.IsNullOrWhiteSpace(navigatorId) ? DefaultNavigatorId : navigatorId.Trim();
        }

        private static string BuildGroupId(string navigatorId, string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return GroupRoot + "__" + NormalizeNavigatorId(navigatorId) + "__invalid";

            // Odin treats '/' as a nested group path and then requires every parent group
            // to be declared. Sections and navigators are siblings, so their internal group
            // IDs stay flat. Plain legacy IDs (for example "core") remain byte-for-byte
            // compatible while punctuation cannot accidentally create nested Odin groups.
            string normalizedNavigatorId = NormalizeNavigatorId(navigatorId);
            string normalizedSectionId = sectionId.Trim();
            if (string.Equals(normalizedNavigatorId, DefaultNavigatorId, StringComparison.Ordinal))
                return GroupRoot + "__" + ToFlatToken(normalizedSectionId);

            return GroupRoot + "__" + ToFlatToken(normalizedNavigatorId) + "__" + ToFlatToken(normalizedSectionId);
        }

        internal static string BuildImplicitSectionId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("编辑器内容分区名称不能为空。", nameof(displayName));

            return ToFlatToken(displayName.Trim());
        }

        private static string ToFlatToken(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                {
                    builder.Append(character);
                    continue;
                }

                builder.Append('_');
                builder.Append(((int)character).ToString("X4"));
            }

            return builder.ToString();
        }

        protected override void CombineValuesWith(PropertyGroupAttribute other)
        {
            if (string.IsNullOrEmpty(Subtitle)
                && other is ESEditorSectionAttribute section
                && !string.IsNullOrEmpty(section.Subtitle))
            {
                Subtitle = section.Subtitle;
            }
        }
    }

    /// <summary>
    /// Opens an editor content section. The annotated member is the first member in the section;
    /// later members may use parameterless <see cref="ESEditorSectionAttribute"/> to continue it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ESEditorBeginSectionAttribute : Attribute
    {
        public readonly string NavigatorId;
        public readonly string SectionId;
        public readonly string DisplayName;
        public readonly float Order;
        public readonly string Subtitle;

        public ESEditorBeginSectionAttribute(string displayName, float order = 0f, string subtitle = null)
            : this(
                ESEditorSectionAttribute.DefaultNavigatorId,
                ESEditorSectionAttribute.BuildImplicitSectionId(displayName),
                displayName,
                order,
                subtitle)
        {
        }

        public ESEditorBeginSectionAttribute(string sectionId, string displayName, float order = 0f, string subtitle = null)
            : this(ESEditorSectionAttribute.DefaultNavigatorId, sectionId, displayName, order, subtitle)
        {
        }

        public ESEditorBeginSectionAttribute(
            string navigatorId,
            string sectionId,
            string displayName,
            float order = 0f,
            string subtitle = null)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                throw new ArgumentException("编辑器内容分区 ID 不能为空。", nameof(sectionId));

            NavigatorId = string.IsNullOrWhiteSpace(navigatorId)
                ? ESEditorSectionAttribute.DefaultNavigatorId
                : navigatorId.Trim();
            SectionId = sectionId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SectionId : displayName.Trim();
            Order = order;
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
        }

        internal ESEditorSectionAttribute CreateSectionAttribute()
            => new ESEditorSectionAttribute(NavigatorId, SectionId, DisplayName, Order, Subtitle);
    }

    public enum ESEditorSectionEndMode
    {
        /// <summary>The annotated member remains in the active section, then the section closes.</summary>
        AfterMember,
        /// <summary>The active section closes before the annotated member, leaving it ungrouped.</summary>
        BeforeMember
    }

    /// <summary>
    /// Closes a shorthand editor section. With no arguments, the annotated member is the final
    /// member of the active section. Use <see cref="ESEditorSectionEndMode.BeforeMember"/> when
    /// the annotated member should stay outside the preceding section.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ESEditorEndSectionAttribute : Attribute
    {
        public readonly string NavigatorId;
        public readonly ESEditorSectionEndMode Mode;

        public ESEditorEndSectionAttribute()
            : this(null, ESEditorSectionEndMode.AfterMember)
        {
        }

        public ESEditorEndSectionAttribute(ESEditorSectionEndMode mode)
            : this(null, mode)
        {
        }

        public ESEditorEndSectionAttribute(string navigatorId, ESEditorSectionEndMode mode = ESEditorSectionEndMode.AfterMember)
        {
            NavigatorId = string.IsNullOrWhiteSpace(navigatorId) ? null : navigatorId.Trim();
            Mode = mode;
        }
    }
}
