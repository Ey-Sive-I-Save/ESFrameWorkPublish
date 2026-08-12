using System;

namespace ES
{
    public enum ESConfigKeyUsage
    {
        Reference,
        Declaration
    }

    /// <summary>
    /// Declares whether a ConfigKey field defines a GameCore identity or references one.
    /// The metadata is editor-only and does not change the serialized ConfigKey shape.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ESConfigKeyUsageAttribute : Attribute
    {
        public readonly ESConfigKeyUsage Usage;

        public ESConfigKeyUsageAttribute(ESConfigKeyUsage usage)
        {
            Usage = usage;
        }
    }

    /// <summary>
    /// ES 字段的重要程度。仅使用“普通 / 重点 / 核心”三级，避免为不同面板重复学习概念。
    /// </summary>
    public enum ESFieldLevel
    {
        /// <summary>普通字段，不额外强调。</summary>
        Normal,

        /// <summary>重点字段，编辑器会给予适度的 ES 风格强调。</summary>
        Important,

        /// <summary>核心字段，编辑器会给予最高级别的 ES 风格强调。</summary>
        Core
    }

    /// <summary>
    /// 为字段声明统一的 ES 语义与编辑器呈现方式。
    /// 所有参数都可以省略，只填写当前字段真正需要的部分即可；该特性不会改变运行时逻辑或序列化值。
    /// </summary>
    /// <example>
    /// <code>
    /// [ESField]
    /// public string description;
    ///
    /// [ESField(ESFieldLevel.Important)]
    /// public string output;
    ///
    /// [ESField(ESFieldLevel.Core, Required = true, Hint = "这是任务最重要的权限边界。")]
    /// public string allowedWriteScopes;
    ///
    /// [ESField(Hint = "可选说明；不需要强调或必填时只写 Hint 即可。")]
    /// public string note;
    /// </code>
    /// </example>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ESFieldAttribute : Attribute
    {
        /// <summary>
        /// 字段的重要程度。省略时为 <see cref="ESFieldLevel.Normal"/>。
        /// </summary>
        public ESFieldLevel Level { get; set; } = ESFieldLevel.Normal;

        /// <summary>
        /// 字段是否必填。省略时为 <see langword="false"/>；必填字段为空时由 ES 编辑器显示错误状态。
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 面向使用者的简短说明。省略、为 <see langword="null"/> 或为空时不附加提示。
        /// </summary>
        public string Hint { get; set; }

        /// <summary>
        /// 使用全部默认值创建字段标记。支持直接写作 <c>[ESField]</c>。
        /// </summary>
        public ESFieldAttribute()
        {
        }

        /// <summary>
        /// 创建只指定重要程度的字段标记；<see cref="Required"/> 与 <see cref="Hint"/> 仍可按需省略。
        /// </summary>
        /// <param name="level">字段的重要程度。</param>
        public ESFieldAttribute(ESFieldLevel level)
        {
            Level = level;
        }
    }

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
