using System;

namespace ES
{
    /// <summary>
    /// Replaces Odin's default managed-reference presentation while retaining the field's
    /// existing SerializeReference persistence and normal Odin child drawers.
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
    /// Supplies the business wording used when a concrete managed-reference type is selected.
    /// Types without this attribute remain selectable with a name inferred from their CLR type.
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
