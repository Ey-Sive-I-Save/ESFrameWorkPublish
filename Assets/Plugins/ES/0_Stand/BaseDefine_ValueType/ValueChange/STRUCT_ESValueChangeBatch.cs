using System;

namespace ES
{
    /// <summary>
    /// Allocation-free notification scope for a single float resolver. Use with <c>using</c> to
    /// publish one Changed event after several mutations.
    /// </summary>
    public struct ESFloatValueChangeBatch : IDisposable
    {
        private ESFloatValueChangeSet set;

        internal ESFloatValueChangeBatch(ESFloatValueChangeSet set)
        {
            this.set = set;
        }

        public void Dispose()
        {
            ESFloatValueChangeSet target = set;
            set = null;
            target?.EndBatch();
        }
    }

    /// <summary>
    /// Allocation-free notification scope for a single permit resolver. Use with <c>using</c> to
    /// publish one Changed event after several mutations.
    /// </summary>
    public struct ESPermitValueChangeBatch : IDisposable
    {
        private ESPermitSet set;

        internal ESPermitValueChangeBatch(ESPermitSet set)
        {
            this.set = set;
        }

        public void Dispose()
        {
            ESPermitSet target = set;
            set = null;
            target?.EndBatch();
        }
    }
}
