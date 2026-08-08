namespace ES
{
    /// <summary>
    /// Unified naming contract for serializable polymorphic objects and other authoring data
    /// that need both a stable type default and an optional per-instance title.
    /// </summary>
    public interface IESNameTitle
    {
        /// <summary>
        /// Gets the effective title and accepts an optional per-instance override. Implementers
        /// should return NameTitleDefault when no custom value is stored.
        /// </summary>
        string NameTitle { get; set; }

        /// <summary>The deterministic default title supplied by the concrete type.</summary>
        string NameTitleDefault { get; }
    }
}
