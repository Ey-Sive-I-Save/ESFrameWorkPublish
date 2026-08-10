using System;

namespace ES
{
    [Serializable]
    public readonly struct ESDeveloperSourceId : IEquatable<ESDeveloperSourceId>
    {
        public readonly string Value;
        public readonly string InstanceId;
        public readonly long Epoch;

        public ESDeveloperSourceId(string value, string instanceId, long epoch)
        {
            Value = value ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            Epoch = epoch;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Value)
            && !string.IsNullOrWhiteSpace(InstanceId)
            && Epoch >= 1;

        public bool Equals(ESDeveloperSourceId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal)
                && string.Equals(InstanceId, other.InstanceId, StringComparison.Ordinal)
                && Epoch == other.Epoch;
        }

        public override bool Equals(object obj)
        {
            return obj is ESDeveloperSourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
                hash = (hash * 397) ^ (InstanceId == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(InstanceId));
                hash = (hash * 397) ^ Epoch.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return Value + "@" + InstanceId + ":" + Epoch;
        }
    }
}
