using System;

namespace ES
{
    /// <summary>
    /// Legacy storage experiment retained only for binary/source migration compatibility.
    /// It is not used by the ValueChange runtime and must not be used as an attribute-table entry.
    /// </summary>
    [Obsolete("ESStatFloatValueChange is retired. Use ESFloatValueChangeSet plus ESValueChangeToken through an owning domain.", true)]
    [Serializable]
    public struct ESStatFloatValueChange
    {
        public int entityId;
        public int statId;
        public int nextInCell;

        public ESFloatValueChange change;
    }
}
