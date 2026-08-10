using System;

namespace ES
{
    [Serializable]
    public readonly struct ESDeveloperRunId : IEquatable<ESDeveloperRunId>
    {
        public readonly string Value;

        public ESDeveloperRunId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public static ESDeveloperRunId CreateNew()
        {
            return new ESDeveloperRunId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(ESDeveloperRunId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESDeveloperRunId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ESDeveloperRunId left, ESDeveloperRunId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ESDeveloperRunId left, ESDeveloperRunId right)
        {
            return !left.Equals(right);
        }
    }
}
