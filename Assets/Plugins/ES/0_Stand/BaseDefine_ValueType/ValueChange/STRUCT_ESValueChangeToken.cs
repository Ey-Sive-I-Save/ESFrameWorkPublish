using System;
using System.Threading;

namespace ES
{
    /// <summary>Process-local identity allocator for ValueChange sets. It is never serialized or replicated.</summary>
    internal static class ESValueChangeSetIdentity
    {
        private static int nextSetId;

        public static int Allocate()
        {
            int id = Interlocked.Increment(ref nextSetId);
            if (id <= 0)
                throw new InvalidOperationException("ESValueChangeSet identity exhausted.");

            return id;
        }
    }

    [Serializable]
    public struct ESValueChangeToken : IEquatable<ESValueChangeToken>
    {
        /// <summary>Diagnostic-only process-local owner identity. It is not a persistent ID.</summary>
        public readonly int setId;
        public readonly int tokenId;
        public readonly int tokenVersion;

        public bool IsValid
        {
            get { return setId > 0 && tokenId > 0 && tokenVersion > 0; }
        }

        public static ESValueChangeToken Invalid
        {
            get { return default; }
        }

        internal ESValueChangeToken(int setId, int tokenId, int tokenVersion)
        {
            this.setId = setId;
            this.tokenId = tokenId;
            this.tokenVersion = tokenVersion;
        }

        public bool Equals(ESValueChangeToken other)
        {
            return setId == other.setId
                && tokenId == other.tokenId
                && tokenVersion == other.tokenVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is ESValueChangeToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = setId;
                hash = (hash * 397) ^ tokenId;
                return (hash * 397) ^ tokenVersion;
            }
        }

        public override string ToString()
        {
            return IsValid ? setId + ":" + tokenId + ":" + tokenVersion : "Invalid";
        }
    }
}
