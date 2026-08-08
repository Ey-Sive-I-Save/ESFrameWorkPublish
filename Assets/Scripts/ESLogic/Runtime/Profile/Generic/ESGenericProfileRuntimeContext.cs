namespace ES
{
    [System.Flags]
    internal enum ESGenericProfileExtensionLifecycleState : byte
    {
        None = 0,
        Awake = 1 << 0,
        Enable = 1 << 1,
        Pool = 1 << 2,
        EverEntered = 1 << 3,
        PreserveEverDuringRollback = 1 << 4
    }

    public sealed class ESGenericProfileRuntimeContext : ESProfileRuntimeContextBase
    {
        private byte[] extensionLifecycleStates;

        public bool AwakeLifecycleCompleted { get; private set; }
        public bool EnableLifecycleActive { get; private set; }
        public bool PoolLifecycleActive { get; private set; }
        public bool DestroyLifecycleCompleted { get; private set; }

        internal void MarkAwakeLifecycleCompleted()
        {
            AwakeLifecycleCompleted = true;
        }

        internal void MarkEnableLifecycleActive(bool active)
        {
            EnableLifecycleActive = active;
        }

        internal void MarkPoolLifecycleActive(bool active)
        {
            PoolLifecycleActive = active;
        }

        internal void MarkDestroyLifecycleCompleted()
        {
            DestroyLifecycleCompleted = true;
        }

        internal void BeginPoolSpawn(int generation)
        {
            BeginPoolGeneration(generation);
        }

        internal void ClearPoolGeneration()
        {
            EndPoolGeneration();
        }

        internal void PrepareStartingExtensionPhase(int extensionCount)
        {
            EnsureExtensionStateCapacity(extensionCount);
            ClearPreserveEverMarkers();
        }

        internal void MarkExtensionEntering(
            int extensionIndex,
            ESGenericProfileExtensionLifecycleState phase)
        {
            EnsureExtensionStateCapacity(extensionIndex + 1);
            ESGenericProfileExtensionLifecycleState state = GetExtensionState(extensionIndex);
            if ((state & ESGenericProfileExtensionLifecycleState.EverEntered) != 0)
                state |= ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback;

            state |= phase | ESGenericProfileExtensionLifecycleState.EverEntered;
            extensionLifecycleStates[extensionIndex] = (byte)state;
        }

        internal void CommitStartingExtensionPhase()
        {
            ClearPreserveEverMarkers();
        }

        internal bool HasExtensionState(
            int extensionIndex,
            ESGenericProfileExtensionLifecycleState state)
        {
            return extensionLifecycleStates != null
                && extensionIndex >= 0
                && extensionIndex < extensionLifecycleStates.Length
                && (extensionLifecycleStates[extensionIndex] & (byte)state) != 0;
        }

        internal bool HasAnyExtensionState(ESGenericProfileExtensionLifecycleState state)
        {
            if (extensionLifecycleStates == null)
                return false;

            byte mask = (byte)state;
            for (int index = 0; index < extensionLifecycleStates.Length; index++)
            {
                if ((extensionLifecycleStates[index] & mask) != 0)
                    return true;
            }

            return false;
        }

        internal void CompleteExtensionPhase(
            int extensionIndex,
            ESGenericProfileExtensionLifecycleState phase)
        {
            if (extensionLifecycleStates == null
                || extensionIndex < 0
                || extensionIndex >= extensionLifecycleStates.Length)
            {
                return;
            }

            ESGenericProfileExtensionLifecycleState state = GetExtensionState(extensionIndex);
            state &= ~phase;
            state &= ~ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback;
            extensionLifecycleStates[extensionIndex] = (byte)state;
        }

        internal void CompleteRolledBackExtensionPhase(
            int extensionIndex,
            ESGenericProfileExtensionLifecycleState phase)
        {
            if (extensionLifecycleStates == null
                || extensionIndex < 0
                || extensionIndex >= extensionLifecycleStates.Length)
            {
                return;
            }

            ESGenericProfileExtensionLifecycleState state = GetExtensionState(extensionIndex);
            bool preserveEver =
                (state & ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback) != 0;
            state &= ~phase;
            state &= ~ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback;
            if (!preserveEver)
                state &= ~ESGenericProfileExtensionLifecycleState.EverEntered;

            extensionLifecycleStates[extensionIndex] = (byte)state;
        }

        internal void CompleteExtensionDestroy(int extensionIndex)
        {
            if (extensionLifecycleStates == null
                || extensionIndex < 0
                || extensionIndex >= extensionLifecycleStates.Length)
            {
                return;
            }

            extensionLifecycleStates[extensionIndex] = 0;
        }

        internal void MarkExtensionEndingFailed(int extensionIndex)
        {
            if (extensionLifecycleStates == null
                || extensionIndex < 0
                || extensionIndex >= extensionLifecycleStates.Length)
            {
                return;
            }

            extensionLifecycleStates[extensionIndex] &=
                unchecked((byte)~(byte)ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback);
        }

        private void EnsureExtensionStateCapacity(int extensionCount)
        {
            if (extensionCount <= 0)
                return;

            if (extensionLifecycleStates == null)
            {
                extensionLifecycleStates = new byte[extensionCount];
                return;
            }

            if (extensionLifecycleStates.Length < extensionCount)
                System.Array.Resize(ref extensionLifecycleStates, extensionCount);
        }

        private ESGenericProfileExtensionLifecycleState GetExtensionState(int extensionIndex)
        {
            return (ESGenericProfileExtensionLifecycleState)extensionLifecycleStates[extensionIndex];
        }

        private void ClearPreserveEverMarkers()
        {
            if (extensionLifecycleStates == null)
                return;

            byte preserveMask = (byte)ESGenericProfileExtensionLifecycleState.PreserveEverDuringRollback;
            for (int index = 0; index < extensionLifecycleStates.Length; index++)
                extensionLifecycleStates[index] &= unchecked((byte)~preserveMask);
        }

        protected override void ClearTransientState()
        {
        }
    }
}
