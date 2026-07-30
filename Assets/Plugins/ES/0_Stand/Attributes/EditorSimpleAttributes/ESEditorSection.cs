using System;
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

        public readonly string SectionId;
        public readonly string DisplayName;
        public string Subtitle { get; private set; }

        public ESEditorSectionAttribute(string sectionId, string displayName, float order = 0f, string subtitle = null)
            : base(BuildGroupId(sectionId), order)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                throw new ArgumentException("编辑器内容分区 ID 不能为空。", nameof(sectionId));

            SectionId = sectionId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SectionId : displayName.Trim();
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
            GroupName = DisplayName;
        }

        private static string BuildGroupId(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return GroupRoot + "__invalid";

            // Odin treats '/' as a nested group path and then requires every parent group
            // to be declared. Sections are siblings, so their internal group IDs stay flat.
            return GroupRoot + "__" + sectionId.Trim();
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
}
