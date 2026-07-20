using System;

namespace ES
{
    [Serializable]
    public struct ESValueChangeToken : IEquatable<ESValueChangeToken>
    {
        public int tokenId;
        public int tokenVersion;

        public bool IsValid
        {
            get { return tokenId > 0 && tokenVersion > 0; }
        }

        public static ESValueChangeToken Invalid
        {
            get { return default; }
        }

        public ESValueChangeToken(int tokenId, int tokenVersion)
        {
            this.tokenId = tokenId;
            this.tokenVersion = tokenVersion;
        }

        public bool Equals(ESValueChangeToken other)
        {
            return tokenId == other.tokenId && tokenVersion == other.tokenVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is ESValueChangeToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (tokenId * 397) ^ tokenVersion;
            }
        }

        public override string ToString()
        {
            return IsValid ? tokenId + ":" + tokenVersion : "Invalid";
        }
    }
}
