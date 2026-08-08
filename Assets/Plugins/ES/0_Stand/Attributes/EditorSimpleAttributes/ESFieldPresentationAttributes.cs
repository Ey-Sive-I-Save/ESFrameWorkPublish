using System;

namespace ES
{
    /// <summary>
    /// Adds a short, non-blocking explanation for an editor field. It affects editor presentation
    /// only and never changes runtime serialization.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ESFieldHintAttribute : Attribute
    {
        public readonly string Text;

        public ESFieldHintAttribute(string text)
        {
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }

    public enum ESFieldRequirement
    {
        Optional,
        Recommended,
        Required
    }

    /// <summary>
    /// Declares the business completion level of a field when the editor cannot infer it from
    /// the type alone. Required fields may surface an error while empty; optional fields remain
    /// quiet. The policy is editor metadata and is not serialized into the asset.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ESFieldPolicyAttribute : Attribute
    {
        public readonly ESFieldRequirement Requirement;

        public ESFieldPolicyAttribute(ESFieldRequirement requirement)
        {
            Requirement = requirement;
        }
    }

    public enum ESCollectionDrawMode
    {
        ProjectDefault,
        StandardCard,
        FeelCard,
        /// <summary>
        /// Uses the compact Feel presentation and lets ES own the complete collection container,
        /// including add, remove and deterministic reordering actions.
        /// </summary>
        FeelList,
        /// <summary>
        /// Uses the ESEditorSection presentation surface while adding collection ownership,
        /// including add, remove and deterministic reordering actions.
        /// </summary>
        SectionList,
        DefaultDrawer
    }

    /// <summary>
    /// Overrides the project-level editor presentation for one serialized collection. This is
    /// type metadata only: it is never serialized into an object instance and has no Player
    /// runtime state or per-instance memory cost.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ESCollectionDrawStyleAttribute : Attribute
    {
        public readonly ESCollectionDrawMode Mode;

        /// <summary>
        /// Optional serialized bool member shown as the integrated FeelList enable toggle.
        /// </summary>
        public string EnabledMemberName { get; set; } = "enabled";

        /// <summary>
        /// Controls whether FeelList exposes duplication. Disable this for collections whose
        /// stable identity contract forbids duplicate element types or keys.
        /// </summary>
        public bool AllowDuplicateItems { get; set; } = true;

        /// <summary>
        /// Keeps manual moves inside the nondecreasing IESCollectionDefaultOrder contract and
        /// exposes single-item and whole-list restore operations.
        /// </summary>
        public bool EnforceDefaultOrder { get; set; }

        public ESCollectionDrawStyleAttribute(ESCollectionDrawMode mode)
        {
            Mode = mode;
        }
    }
}
