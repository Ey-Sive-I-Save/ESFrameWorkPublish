using System;

namespace ES
{
    public enum ESFloatValueChangeOp : byte
    {
        Add = 0,
        AddPercent = 1,
        Multiply = 2,
        Override = 3,
        Min = 4,
        Max = 5
    }

    [Serializable]
    public struct ESFloatValueChange
    {
        public int tokenId;
        public int tokenVersion;
        public int ownerId;
        public int sourceId;
        public int ownerListIndex;
        public int sourceListIndex;
        public int priority;
        public int order;
        public byte enabled;

        public ESFloatValueChangeOp op;
        public float value;

        public bool IsEnabled
        {
            get { return enabled != 0; }
        }

        public ESValueChangeToken Token
        {
            get { return new ESValueChangeToken(tokenId, tokenVersion); }
        }
    }
}
