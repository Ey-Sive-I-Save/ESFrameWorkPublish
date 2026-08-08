namespace ES
{
    /// <summary>
    /// Supplies a deterministic authoring-order contract for collection elements. Collections
    /// may still preserve a stable manual order between elements that share the same value.
    /// </summary>
    public interface IESCollectionDefaultOrder
    {
        int DefaultOrder { get; }
    }
}
