using System;

namespace ES
{
    [Serializable]
    public struct ESStatFloatValueChange
    {
        public int entityId;
        public int statId;
        public int nextInCell;

        public ESFloatValueChange change;
    }
}
