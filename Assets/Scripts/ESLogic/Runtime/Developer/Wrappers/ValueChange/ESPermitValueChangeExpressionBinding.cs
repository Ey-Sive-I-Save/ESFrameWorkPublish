using System;

namespace ES
{
    [Serializable]
    public sealed class ESPermitValueChangeExpressionBinding
    {
        public BoolExpressionSource condition = new BoolExpressionSource { directBool = true };
        public ESPermitLaw trueLaw = ESPermitLaw.AllowEnable;
        public ESPermitLaw falseLaw = ESPermitLaw.Ignore;
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
            ESPermitValueChangeTracker tracker,
            ESRuntimeTargetPack target,
            ESOpSupport support)
        {
            ESPermitLaw finalLaw = condition.Evaluate(target, support) ? trueLaw : falseLaw;
            if (token.IsValid && tracker.Update(token, finalLaw))
            {
                tracker.SetEnabled(token, enabled);
                return token;
            }

            token = tracker.Add(finalLaw, priority, enabled);
            return token;
        }

        public bool SetEnabled(ESPermitValueChangeTracker tracker, bool enabled)
        {
            this.enabled = enabled;
            return token.IsValid && tracker.SetEnabled(token, enabled);
        }

        public bool Release(ESPermitValueChangeTracker tracker)
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
