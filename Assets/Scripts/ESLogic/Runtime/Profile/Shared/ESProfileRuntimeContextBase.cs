namespace ES
{
    /// <summary>Non-serialized state owned by one Profile instance and its current Pool generation.</summary>
    public abstract class ESProfileRuntimeContextBase
    {
        public int PoolGeneration { get; private set; }
        public bool IsPoolSpawned { get; private set; }

        protected void BeginPoolGeneration(int generation)
        {
            ClearTransientState();
            PoolGeneration = generation;
            IsPoolSpawned = true;
        }

        protected void EndPoolGeneration()
        {
            ClearTransientState();
            PoolGeneration = 0;
            IsPoolSpawned = false;
        }

        protected abstract void ClearTransientState();
    }
}
