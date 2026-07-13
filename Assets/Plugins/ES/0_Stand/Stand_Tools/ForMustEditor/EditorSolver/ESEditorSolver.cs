namespace ES
{
    public abstract class ESEditorSolver
    {
        public bool IsInitialized { get; private set; }

        protected TSelf CompleteInitSolver<TSelf>() where TSelf : ESEditorSolver
        {
            IsInitialized = true;
            return (TSelf)this;
        }

        public virtual void ResetSolver()
        {
            IsInitialized = false;
            OnResetSolver();
        }

        protected virtual void OnResetSolver()
        {
        }
    }
}
