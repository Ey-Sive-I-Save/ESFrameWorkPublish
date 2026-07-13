using UnityEngine;

namespace ES
{
    /// <summary>
    /// Framework-level permit law.
    /// Ignore means this layer does not decide and the resolver should continue to lower-priority data.
    /// Allow* means a soft decision that can be overridden by higher-priority or hard decisions.
    /// Hard* means a forced decision that should only be overridden by a resolver rule with explicit higher authority.
    /// </summary>
    public enum ESPermitLaw : byte
    {
        [InspectorName("忽略")]
        Ignore = 0,

        [InspectorName("软启用")]
        AllowEnable = 1,

        [InspectorName("软禁用")]
        AllowDisable = 2,

        [InspectorName("强制启用")]
        HardEnable = 3,

        [InspectorName("强制禁用")]
        HardDisable = 4
    }

    public static class ESPermitLawUtility
    {
        public static bool TryResolve(ESPermitLaw decision, out bool value)
        {
            switch (decision)
            {
                case ESPermitLaw.AllowEnable:
                case ESPermitLaw.HardEnable:
                    value = true;
                    return true;
                case ESPermitLaw.AllowDisable:
                case ESPermitLaw.HardDisable:
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        public static bool Apply(ESPermitLaw decision, bool fallback)
        {
            switch (decision)
            {
                case ESPermitLaw.AllowEnable:
                case ESPermitLaw.HardEnable:
                    return true;
                case ESPermitLaw.AllowDisable:
                case ESPermitLaw.HardDisable:
                    return false;
                default:
                    return fallback;
            }
        }

        public static bool IsExplicit(ESPermitLaw decision)
        {
            return decision != ESPermitLaw.Ignore;
        }

        public static bool IsEnable(ESPermitLaw decision)
        {
            return decision == ESPermitLaw.AllowEnable || decision == ESPermitLaw.HardEnable;
        }

        public static bool IsDisable(ESPermitLaw decision)
        {
            return decision == ESPermitLaw.AllowDisable || decision == ESPermitLaw.HardDisable;
        }

        public static bool IsHard(ESPermitLaw decision)
        {
            return decision == ESPermitLaw.HardEnable || decision == ESPermitLaw.HardDisable;
        }

        public static bool IsAllow(ESPermitLaw decision)
        {
            return decision == ESPermitLaw.AllowEnable || decision == ESPermitLaw.AllowDisable;
        }
    }
}

