using System;

namespace ES
{
    /// <summary>资源加载核心的只读诊断快照；供编辑器和问题报告使用。</summary>
    public readonly struct ESAssetRuntimeDiagnostics
    {
        public readonly bool IsReady;
        public readonly bool IsProviderTransitioning;
        public readonly bool ProviderHasPendingOperations;
        public readonly string ProviderType;
        public readonly int LiveScopeCount;
        public readonly int PendingScopeCount;
        public readonly int LoadedAssetCount;
        public readonly int PendingAssetCount;
        public readonly int PooledScopeStateCount;
        public readonly bool HasResidentScope;
        public readonly int RegisteredScopeCount;
        public readonly int ImplicitRegisteredScopeCount;
        public readonly int ClosingRegisteredScopeCount;
        public readonly bool HasUnloadFailure;
        public readonly int UnloadFailureCount;
        public readonly string LastUnloadError;

        public ESAssetRuntimeDiagnostics(
            bool isReady,
            bool isProviderTransitioning,
            bool providerHasPendingOperations,
            string providerType,
            int liveScopeCount,
            int pendingScopeCount,
            int loadedAssetCount,
            int pendingAssetCount,
            int pooledScopeStateCount,
            bool hasResidentScope,
            int registeredScopeCount,
            int implicitRegisteredScopeCount,
            int closingRegisteredScopeCount,
            bool hasUnloadFailure,
            int unloadFailureCount,
            string lastUnloadError)
        {
            IsReady = isReady;
            IsProviderTransitioning = isProviderTransitioning;
            ProviderHasPendingOperations = providerHasPendingOperations;
            ProviderType = providerType ?? string.Empty;
            LiveScopeCount = liveScopeCount;
            PendingScopeCount = pendingScopeCount;
            LoadedAssetCount = loadedAssetCount;
            PendingAssetCount = pendingAssetCount;
            PooledScopeStateCount = pooledScopeStateCount;
            HasResidentScope = hasResidentScope;
            RegisteredScopeCount = registeredScopeCount;
            ImplicitRegisteredScopeCount = implicitRegisteredScopeCount;
            ClosingRegisteredScopeCount = closingRegisteredScopeCount;
            HasUnloadFailure = hasUnloadFailure;
            UnloadFailureCount = unloadFailureCount;
            LastUnloadError = lastUnloadError ?? string.Empty;
        }
    }

    public sealed partial class ESAssetScope
    {
        internal int DiagnosticLoadedAssetCount => entries.Count;
        internal int DiagnosticPendingAssetCount => pending.Count;
    }

    public static partial class ESAssets
    {
        /// <summary>生成独立快照，不向编辑器暴露可变集合或 Provider 实例。</summary>
        public static ESAssetRuntimeDiagnostics GetRuntimeDiagnostics()
        {
            IESAssetRuntimeProvider provider = ESAssets.RuntimeBackend;
            bool providerPending = provider is IESRuntimeAssetOperationTracker tracker && tracker.HasPendingOperations;
            var unloadDiagnostics = provider as IESRuntimeAssetUnloadDiagnostics;
            int pendingScopes = 0;
            int loadedAssets = 0;
            int pendingAssets = 0;
            int implicitRegisteredScopes = 0;
            int closingRegisteredScopes = 0;

            foreach (ESAssetScope scope in liveScopes)
            {
                if (scope == null || scope.IsDisposed)
                    continue;
                loadedAssets += scope.DiagnosticLoadedAssetCount;
                pendingAssets += scope.DiagnosticPendingAssetCount;
                if (scope.HasPendingOperations)
                    pendingScopes++;
            }

            foreach (ScopeRegistration registration in registeredScopes.Values)
            {
                if (registration.ImplicitlyCreated)
                    implicitRegisteredScopes++;
                if (registration.State == ScopeRegistryState.Closing)
                    closingRegisteredScopes++;
            }

            return new ESAssetRuntimeDiagnostics(
                IsReady,
                providerTransitioning,
                providerPending,
                provider?.GetType().FullName ?? string.Empty,
                liveScopes.Count,
                pendingScopes,
                loadedAssets,
                pendingAssets,
                ESAssetScope.PooledStateCount,
                residentScope != null && !residentScope.IsDisposed,
                registeredScopes.Count,
                implicitRegisteredScopes,
                closingRegisteredScopes,
                unloadDiagnostics != null && unloadDiagnostics.HasUnloadFailure,
                unloadDiagnostics?.UnloadFailureCount ?? 0,
                unloadDiagnostics?.LastUnloadError ?? string.Empty);
        }
    }
}
