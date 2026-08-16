using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ES
{
    /// <summary>Runtime-only data passed to a bound view. It is cleared before pooling or destroy.</summary>
    public sealed class ESUIWindowContext
    {
        internal ESUIWindowContext(
            ESUIRootCoordinator root,
            ESUIWindowDefinition definition,
            ESUIWindowLease lease,
            object userData,
            long operationId)
        {
            Root = root;
            Definition = definition;
            Lease = lease;
            UserData = userData;
            OperationId = operationId;
        }

        public ESUIRootCoordinator Root { get; private set; }
        public ESUIWindowDefinition Definition { get; private set; }
        public ESUIWindowLease Lease { get; private set; }
        public object UserData { get; private set; }
        public long OperationId { get; private set; }

        internal void Clear()
        {
            Root = null;
            Definition = null;
            Lease = null;
            UserData = null;
            OperationId = 0;
        }
    }

    internal sealed class ESUIWindowInstance
    {
        internal readonly HashSet<int> leaseTokens = new HashSet<int>();

        internal ESUIRootCoordinator root;
        internal ESUIWindowDefinition definition;
        internal ESUIWindowView view;
        internal GameObject gameObject;
        internal ESRuntimeModeLease runtimeModeLease;
        internal CancellationTokenSource lifetimeCancellation;
        internal ESUIWindowContext context;
        internal ESUIWindowState state;
        internal int rootGeneration;
        internal int instanceGeneration;
        internal int providerGeneration;
        internal long operationId;
        internal long inactiveOrder;
        internal bool isPoolManaged;
        internal bool isRetainedInactive;
    }
}
