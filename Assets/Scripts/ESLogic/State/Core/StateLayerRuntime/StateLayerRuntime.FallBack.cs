using Sirenix.OdinInspector;

namespace ES
{
    public partial class StateLayerRuntime
    {
        [LabelText("Grounded FallBack")]      public int FallBackForGrounded = -1;
        [LabelText("Crouched FallBack")]      public int FallBackForCrouched = -1;
        [LabelText("Prone FallBack")]         public int FallBackForProne = -1;
        [LabelText("Swimming FallBack")]      public int FallBackForSwimming = -1;
        [LabelText("Flying FallBack")]        public int FallBackForFlying = -1;
        [LabelText("Mounted FallBack")]       public int FallBackForMounted = -1;
        [LabelText("Climbing FallBack")]      public int FallBackForClimbing = -1;
        [LabelText("SpecialInteraction FallBack")] public int FallBackForSpecialInteraction = -1;
        [LabelText("Observer FallBack")]      public int FallBackForObserver = -1;
        [LabelText("Dead FallBack")]          public int FallBackForDead = -1;
        [LabelText("Transition FallBack")]    public int FallBackForTransition = -1;

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