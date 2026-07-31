using System;

namespace ES
{
    /// <summary>
    /// Legacy compatibility marker from the first prototype. New fields do not need this
    /// attribute: the editor automatically detects Unity's SerializeReference backend.
    /// Existing source using it can remain temporarily without changing serialized data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ESPolymorphicReferenceAttribute : Attribute
    {
        public readonly string Title;
        public string Subtitle { get; set; }
        public bool AllowNull { get; set; } = true;
        public bool Expanded { get; set; } = true;

        public ESPolymorphicReferenceAttribute(string title = null)
        {
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }
    }

    /// <summary>
    /// Legacy compatibility metadata from the first prototype. The automatic selector now
    /// uses Odin's existing TypeRegistryItemAttribute for the business directory tree. Types
    /// without TypeRegistryItem remain selectable under the "未登记类型" group.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ESPolymorphicTypeAttribute : Attribute
    {
        public readonly string DisplayName;
        public readonly string Category;
        public string Subtitle { get; set; }
        public int Order { get; set; }

        public ESPolymorphicTypeAttribute(string displayName, string category = null, string subtitle = null)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().Trim('/');
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
        }
    }
}
