using System;
using Cysharp.Threading.Tasks;

namespace ES
{
    /// <summary>
    /// One caller's ownership of a window request. Multiple leases may reference one singleton
    /// window; the instance closes only after the final lease is released.
    /// </summary>
    public sealed class ESUIWindowLease : IDisposable
    {
        private ESUIRootCoordinator root;
        private ESUIWindowInstance instance;
        private readonly int rootGeneration;
        private readonly int instanceGeneration;
        private readonly int token;
        private bool released;

        internal ESUIWindowLease(ESUIRootCoordinator root, ESUIWindowInstance instance, int token)
        {
            this.root = root;
            this.instance = instance;
            rootGeneration = instance.rootGeneration;
            instanceGeneration = instance.instanceGeneration;
            this.token = token;
        }

        public ESUIWindowState State
        {
            get
            {
                ESUIRootCoordinator currentRoot = root;
                return currentRoot != null
                       && currentRoot.TryGetLeaseState(this, out ESUIWindowState state)
                    ? state
                    : ESUIWindowState.Closed;
            }
        }

        public bool IsValid
        {
            get
            {
                ESUIRootCoordinator currentRoot = root;
                return !released && currentRoot != null && currentRoot.IsLeaseCurrent(this);
            }
        }

        public ESUIWindowContext Context => IsValid ? instance?.context : null;

        public UniTask CloseAsync()
        {
            return RequestCloseAsync(ESUIWindowCloseEffect.Default);
        }

        public UniTask CloseAndDestroyAsync()
        {
            return RequestCloseAsync(ESUIWindowCloseEffect.Destroy);
        }

        public UniTask CloseAndReturnToPoolAsync()
        {
            return RequestCloseAsync(ESUIWindowCloseEffect.ReturnToPool);
        }

        public UniTask CloseAndKeepInactiveAsync()
        {
            return RequestCloseAsync(ESUIWindowCloseEffect.KeepInactive);
        }

        public void Dispose()
        {
            ESUIRootCoordinator currentRoot = root;
            if (currentRoot != null)
                currentRoot.CloseImmediately(this);
        }

        private UniTask RequestCloseAsync(ESUIWindowCloseEffect effect)
        {
            ESUIRootCoordinator currentRoot = root;
            return currentRoot == null ? UniTask.CompletedTask : currentRoot.CloseAsync(this, effect);
        }

        internal ESUIRootCoordinator Root => root;
        internal ESUIWindowInstance Instance => instance;
        internal int RootGeneration => rootGeneration;
        internal int InstanceGeneration => instanceGeneration;
        internal int Token => token;
        internal bool IsReleased => released;

        internal void MarkReleased()
        {
            released = true;
            root = null;
            instance = null;
        }
    }
}
