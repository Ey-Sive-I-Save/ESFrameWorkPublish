using Sirenix.OdinInspector;

namespace ES
{
    public partial class StateLayerRuntime
    {
        [FoldoutGroup("回退设置", expanded: false), LabelText("Grounded"), ReadOnly]
        public int FallBackForGrounded = -1;

        [FoldoutGroup("回退设置"), LabelText("Crouched"), ReadOnly]
        public int FallBackForCrouched = -1;

        [FoldoutGroup("回退设置"), LabelText("Prone"), ReadOnly]
        public int FallBackForProne = -1;

        [FoldoutGroup("回退设置"), LabelText("Swimming"), ReadOnly]
        public int FallBackForSwimming = -1;

        [FoldoutGroup("回退设置"), LabelText("Flying"), ReadOnly]
        public int FallBackForFlying = -1;

        [FoldoutGroup("回退设置"), LabelText("Mounted"), ReadOnly]
        public int FallBackForMounted = -1;

        [FoldoutGroup("回退设置"), LabelText("Climbing"), ReadOnly]
        public int FallBackForClimbing = -1;

        [FoldoutGroup("回退设置"), LabelText("SpecialInteraction"), ReadOnly]
        public int FallBackForSpecialInteraction = -1;

        [FoldoutGroup("回退设置"), LabelText("Observer"), ReadOnly]
        public int FallBackForObserver = -1;

        [FoldoutGroup("回退设置"), LabelText("Dead"), ReadOnly]
        public int FallBackForDead = -1;

        [FoldoutGroup("回退设置"), LabelText("Transition"), ReadOnly]
        public int FallBackForTransition = -1;

        public int GetFallBack(StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var originalFlag = supportFlag;
            if (supportFlag == StateSupportFlags.None)
            {
                var machine = GetStateMachineOrNull();
                supportFlag = machine != null ? machine.currentSupportFlags : StateSupportFlags.Grounded;
            }
            supportFlag = NormalizeSingleFlag(supportFlag);

            int result = supportFlag switch
            {
                StateSupportFlags.Grounded => FallBackForGrounded,
                StateSupportFlags.Crouched => FallBackForCrouched,
                StateSupportFlags.Prone => FallBackForProne,
                StateSupportFlags.Swimming => FallBackForSwimming,
                StateSupportFlags.Flying => FallBackForFlying,
                StateSupportFlags.Mounted => FallBackForMounted,
                StateSupportFlags.Climbing => FallBackForClimbing,
                StateSupportFlags.SpecialInteraction => FallBackForSpecialInteraction,
                StateSupportFlags.Observer => FallBackForObserver,
                StateSupportFlags.Dead => FallBackForDead,
                StateSupportFlags.Transition => FallBackForTransition,
                _ => ResolveFallBackByFlag(StateSupportFlags.Grounded)
            };

            return result;
        }

        public void SetFallBack(int stateID, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            if (supportFlag == StateSupportFlags.None)
            {
                var machine = GetStateMachineOrNull();
                supportFlag = machine != null ? machine.currentSupportFlags : StateSupportFlags.Grounded;
            }
            supportFlag = NormalizeSingleFlag(supportFlag);

            switch (supportFlag)
            {
                case StateSupportFlags.Grounded: FallBackForGrounded = stateID; break;
                case StateSupportFlags.Crouched: FallBackForCrouched = stateID; break;
                case StateSupportFlags.Prone: FallBackForProne = stateID; break;
                case StateSupportFlags.Swimming: FallBackForSwimming = stateID; break;
                case StateSupportFlags.Flying: FallBackForFlying = stateID; break;
                case StateSupportFlags.Mounted: FallBackForMounted = stateID; break;
                case StateSupportFlags.Climbing: FallBackForClimbing = stateID; break;
                case StateSupportFlags.SpecialInteraction: FallBackForSpecialInteraction = stateID; break;
                case StateSupportFlags.Observer: FallBackForObserver = stateID; break;
                case StateSupportFlags.Dead: FallBackForDead = stateID; break;
                case StateSupportFlags.Transition: FallBackForTransition = stateID; break;
            }
        }

        public bool HasFallBack(StateSupportFlags supportFlag = StateSupportFlags.None) => GetFallBack(supportFlag) >= 0;

        private static StateSupportFlags NormalizeSingleFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;
            ushort value = (ushort)flag;
            ushort lowest = (ushort)(value & (ushort)(-(short)value));
            return (StateSupportFlags)lowest;
        }

        private int ResolveFallBackByFlag(StateSupportFlags flag)
        {
            return flag switch
            {
                StateSupportFlags.Grounded => FallBackForGrounded,
                StateSupportFlags.Crouched => FallBackForCrouched,
                StateSupportFlags.Prone => FallBackForProne,
                StateSupportFlags.Swimming => FallBackForSwimming,
                StateSupportFlags.Flying => FallBackForFlying,
                StateSupportFlags.Mounted => FallBackForMounted,
                StateSupportFlags.Climbing => FallBackForClimbing,
                StateSupportFlags.SpecialInteraction => FallBackForSpecialInteraction,
                StateSupportFlags.Observer => FallBackForObserver,
                StateSupportFlags.Dead => FallBackForDead,
                StateSupportFlags.Transition => FallBackForTransition,
                _ => FallBackForGrounded
            };
        }
    }
}