using System;

namespace ES
{
    [Serializable]
    public struct ESPermitValueChange
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

        public ESPermitLaw law;

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
