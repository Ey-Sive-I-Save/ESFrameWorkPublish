namespace ES
{
    /// <summary>
    /// A diagnostic view of one resolved float stat. This is a value type so callers can inspect a
    /// stat without creating strings, collections, or a second runtime resolver.
    /// </summary>
    public struct ESFloatStatSnapshot
    {
        public int setId;
        public int revision;
        public int changeCount;
        public int enabledChangeCount;

        public float baseValue;
        public float additiveValue;
        public float addedPercent;
        public float multiplyValue;

        public bool hasOverride;
        public ESValueChangeToken overrideToken;
        public float overrideValue;
        public int overridePriority;
        public int overrideOrder;

        public bool hasModifierMinimum;
        public float modifierMinimum;
        public bool hasModifierMaximum;
        public float modifierMaximum;

        public float definitionMinimum;
        public float definitionMaximum;

        public float valueAfterOverride;
        public float valueAfterAdd;
        public float valueAfterPercent;
        public float valueAfterMultiply;
        public float valueAfterModifierBounds;
        public float value;

        /// <summary>Builds the diagnostic form for a stat that has no active modifier resolver.</summary>
        public static ESFloatStatSnapshot FromBaseValue(float baseValue, float minimumValue, float maximumValue)
        {
            float value = baseValue;
            if (value < minimumValue)
                value = minimumValue;
            if (value > maximumValue)
                value = maximumValue;

            return new ESFloatStatSnapshot
            {
                baseValue = baseValue,
                multiplyValue = 1f,
                definitionMinimum = minimumValue,
                definitionMaximum = maximumValue,
                valueAfterOverride = baseValue,
                valueAfterAdd = baseValue,
                valueAfterPercent = baseValue,
                valueAfterMultiply = baseValue,
                valueAfterModifierBounds = baseValue,
                value = value
            };
        }
    }

    /// <summary>
    /// One modifier row for a stat inspector. OwnerId and SourceId are process-local diagnostics;
    /// they deliberately do not retain gameplay object references.
    /// </summary>
    public struct ESFloatStatModifierSnapshot
    {
        public ESValueChangeToken token;
        public int ownerId;
        public int sourceId;
        public int priority;
        public int order;
        public ESFloatValueChangeOp operation;
        public float value;
        public bool enabled;
        public bool isWinningOverride;
    }
}
