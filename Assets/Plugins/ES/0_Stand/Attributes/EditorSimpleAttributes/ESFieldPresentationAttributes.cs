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
}
