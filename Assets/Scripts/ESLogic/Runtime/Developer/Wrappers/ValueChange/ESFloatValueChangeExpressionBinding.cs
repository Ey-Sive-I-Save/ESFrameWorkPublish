using System;

namespace ES
{
    [Serializable]
    public sealed class ESFloatValueChangeExpressionBinding
    {
        public ESFloatValueChangeOp op = ESFloatValueChangeOp.Add;
        public FloatExpressionSource value = new FloatExpressionSource { directFloat = 0f };
        public int priority;
        public bool enabled = true;

        [NonSerialized] private ESValueChangeToken token;

        public bool HasApplied
        {
            get { return token.IsValid; }
        }

        public ESValueChangeToken Token
        {
            get { return token; }
        }

        public ESValueChangeToken ApplyOrRefresh(
            ESFloatValueChangeTracker tracker,
            ESRuntimeTargetPack target,
            ESOpSupport support)
        {
            float finalValue = value.Evaluate(target, support);
            if (token.IsValid && tracker.Update(token, finalValue))
            {
                tracker.SetEnabled(token, enabled);
                return token;
            }

            token = tracker.Add(op, finalValue, priority, enabled);
            return token;
        }

        public bool SetEnabled(ESFloatValueChangeTracker tracker, bool enabled)
        {
            this.enabled = enabled;
            return token.IsValid && tracker.SetEnabled(token, enabled);
        }

        public bool Release(ESFloatValueChangeTracker tracker)
        {
            if (!token.IsValid)
                return false;

            bool released = tracker.Release(token);
            token = ESValueChangeToken.Invalid;
            return released;
        }

        public void ClearRuntimeToken()
        {
            token = ESValueChangeToken.Invalid;
        }
    }
}
