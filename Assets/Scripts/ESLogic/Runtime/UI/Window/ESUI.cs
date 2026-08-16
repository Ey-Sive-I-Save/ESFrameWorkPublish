using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    /// <summary>Business-facing entry points for the registered default UI root.</summary>
    public static class ESUI
    {
        public const string MainRootKey = "main";

        public static UniTask<ESUIWindowLease> OpenAsync(
            ESUIWindowId windowId,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenInRootAsync(MainRootKey, windowId, userData, cancellationToken);
        }

        public static UniTask<ESUIWindowLease> OpenAsync(
            string windowKey,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenInRootAsync(MainRootKey, windowKey, userData, cancellationToken);
        }

        public static UniTask<ESUIWindowLease> OpenInRootAsync(
            string rootKey,
            ESUIWindowId windowId,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return GetModuleOrThrow().OpenAsync(rootKey, windowId, userData, cancellationToken);
        }

        public static UniTask<ESUIWindowLease> OpenInRootAsync(
            string rootKey,
            string windowKey,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return GetModuleOrThrow().OpenAsync(rootKey, windowKey, userData, cancellationToken);
        }

        private static ESUIWindowModule GetModuleOrThrow()
        {
            if (ESGameManager.TryGetModule(out ESUIWindowModule module) && module != null)
                return module;

            throw new InvalidOperationException("ESUI 尚未就绪：请先在场景中启用并配置 ESUIRootCoordinator。");
        }
    }
}
