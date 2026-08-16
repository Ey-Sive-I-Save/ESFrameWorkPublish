namespace ES.EditorInternal
{
    internal enum ESWindowActivityState
    {
        None,
        Active,
        Background,
        Busy,
        Attention
    }

    /// <summary>
    /// Shared semantic state for ES editor fields. Empty is intentionally different from Error:
    /// an optional unassigned value should remain quiet, while a missing type blocks continuation.
    /// </summary>
    public enum ESStatusKind
    {
        None,
        Ready,
        Empty,
        Info,
        Warning,
        Error,
        ReadOnly,
        Modified
    }
}
