using System;

namespace ES
{
    [Serializable]
    public readonly struct ESDeveloperEvidenceRef : IEquatable<ESDeveloperEvidenceRef>
    {
        public readonly ESDeveloperSourceId Source;
        public readonly long Sequence;
        public readonly string EvidenceKey;
        public readonly ESDeveloperEvidenceLevel Level;

        public ESDeveloperEvidenceRef(
            ESDeveloperSourceId source,
            long sequence,
            string evidenceKey,
            ESDeveloperEvidenceLevel level = ESDeveloperEvidenceLevel.LocalEphemeral)
        {
            Source = source;
            Sequence = sequence;
            EvidenceKey = evidenceKey ?? string.Empty;
            Level = level;
        }

        public bool IsValid =>
            Source.IsValid
            && Sequence >= 1
            && !string.IsNullOrWhiteSpace(EvidenceKey);

        public bool Equals(ESDeveloperEvidenceRef other)
        {
            return Source.Equals(other.Source)
                && Sequence == other.Sequence
                && string.Equals(EvidenceKey, other.EvidenceKey, StringComparison.Ordinal)
                && Level == other.Level;
        }

        public override bool Equals(object obj)
        {
            return obj is ESDeveloperEvidenceRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Source.GetHashCode();
                hash = (hash * 397) ^ Sequence.GetHashCode();
                hash = (hash * 397) ^ (EvidenceKey == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(EvidenceKey));
                hash = (hash * 397) ^ Level.GetHashCode();
                return hash;
            }
        }
    }
}
