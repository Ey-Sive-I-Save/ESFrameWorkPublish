using System;

namespace ES
{
    [Serializable]
    public readonly struct ESDeveloperCorrelationId : IEquatable<ESDeveloperCorrelationId>
    {
        public readonly string Value;

        public ESDeveloperCorrelationId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public static ESDeveloperCorrelationId CreateNew()
        {
            return new ESDeveloperCorrelationId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(ESDeveloperCorrelationId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESDeveloperCorrelationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ESDeveloperCorrelationId left, ESDeveloperCorrelationId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ESDeveloperCorrelationId left, ESDeveloperCorrelationId right)
        {
            return !left.Equals(right);
        }
    }
}
