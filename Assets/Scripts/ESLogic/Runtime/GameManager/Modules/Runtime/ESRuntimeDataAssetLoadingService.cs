using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    /// <summary>
    /// RuntimeData 分类 AssetTable 的唯一装配点。
    /// Boot 在 Manifest 和当前 RunMode Provider 准备完成后调用 Initialize；业务层永远只访问 ESRuntimeDataAsset 的分类 Table。
    /// </summary>
    /// <summary>GameManager 的框架装配服务；业务不直接初始化或切换资源后端。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class ESRuntimeDataAssetLoadingService : IDisposable
    {
        private IESAssetRuntimeProvider runtimeProvider;
        private ESRuntimeAssetCatalog assetCatalog;
        private bool initialized;

        public bool IsInitialized => initialized;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public IESAssetRuntimeProvider RuntimeBackend => runtimeProvider;

        public void Initialize(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            Initialize(new ESRuntimeAssetLoader(manifest, provider, retryPolicy));
        }

        public void Initialize(IESAssetRuntimeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (ReferenceEquals(provider, runtimeProvider))
                return;
            if (runtimeProvider != null && ESAssets.HasPendingOperations)
                throw new InvalidOperationException("[ESRes][Provider] 当前仍有资源请求，运行时重初始化必须使用 InitializeAsync。");

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(runtimeProvider != null);
                ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
                if (ESRuntimeDataAsset.ActiveAssetConfigReaderCount != 0)
                    throw new InvalidOperationException("[ESRes][Provider] 仍有 Asset ConfigTable Payload Lease，不能同步切换 Provider。");
                DisposeCurrentProviderCore();
                AttachProvider(provider);
            }
            catch (Exception exception)
            {
                try
                {
                    if (ReferenceEquals(provider, runtimeProvider))
                        DisposeCurrentProviderCore();
                    else
                        provider.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("[ESRes][Provider] 初始化失败后的 Provider 清理也失败。", exception, cleanupException);
                }
                throw;
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>
        /// 运行中切换 Catalog/Provider 的唯一安全入口：停止新请求，释放旧计划与 Scope，
        /// 等待旧 Provider 收尾后重置所有 AssetTable Loader，再装配新 Provider。
        /// </summary>
        public UniTask InitializeAsync(IESAssetRuntimeProvider provider, CancellationToken cancellationToken = default)
            => InitializeAsync(provider, null, cancellationToken);

        internal async UniTask InitializeAsync(IESAssetRuntimeProvider provider, Action rebuildTablesBeforeAttach, CancellationToken cancellationToken)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (ReferenceEquals(provider, runtimeProvider))
                return;

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(runtimeProvider != null);
                ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
                if (runtimeProvider != null)
                {
                    await ESAssets.WaitForPendingOperationsAsync(cancellationToken);
                    await ESRuntimeDataAsset.WaitForAssetConfigReadersAsync(cancellationToken);
                }
                DisposeCurrentProviderCore();
                if (rebuildTablesBeforeAttach != null)
                {
                    ESRuntimeDataAsset.BeginProviderCandidateBuild();
                    try { rebuildTablesBeforeAttach(); }
                    catch
                    {
                        ESRuntimeDataAsset.CancelProviderCandidateBuild();
                        throw;
                    }
                }
                AttachProvider(provider);
            }
            catch (Exception exception)
            {
                try
                {
                    if (ReferenceEquals(provider, runtimeProvider))
                        DisposeCurrentProviderCore();
                    else
                        provider.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("[ESRes][Provider] 异步初始化失败后的 Provider 清理也失败。", exception, cleanupException);
                }
                throw;
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        public void Dispose()
        {
            if (!initialized && runtimeProvider == null)
                return;

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
                if (ESRuntimeDataAsset.ActiveAssetConfigReaderCount != 0)
                    throw new InvalidOperationException("[ESRes][Provider] 仍有 Asset ConfigTable Payload Lease，不能同步销毁 Provider。");
                DisposeCurrentProviderCore();
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>同步兼容入口，仅用于已确认完全静默的 Bootstrap/退出阶段；场景切换请使用异步版本。</summary>
        public void UnloadAllAssetsAtSafePoint()
        {
            if (!initialized || runtimeProvider == null)
                return;
            if (ESAssets.HasPendingOperations)
                throw new InvalidOperationException("[ESRes][SafePoint] 当前仍有资源请求，请使用 UnloadAllAssetsAtSafePointAsync。");

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                if (ESRuntimeDataAsset.ActiveAssetConfigReaderCount != 0)
                    throw new InvalidOperationException("[ESRes][SafePoint] 仍有 Asset ConfigTable Payload Lease，请使用异步安全点并等待调用方释放。");
                ESRuntimeDataAsset.RotateGenerationAtSafePoint(runtimeProvider, ESAssets.RuntimeBackendGeneration);
                if (ESRuntimeDataAsset.ActiveAssetConfigReaderCount != 0)
                    throw new InvalidOperationException("[ESRes][SafePoint] 代际交换期间出现新的 Asset ConfigTable 读者，拒绝提前卸载 Provider。");
                ESAssets.UnloadAllAtSafePoint();
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>推荐的全量资源安全点：先等待 Provider/Scope 收尾，再清理表缓存并卸载。</summary>
        public async UniTask UnloadAllAssetsAtSafePointAsync(CancellationToken cancellationToken = default)
        {
            if (!initialized || runtimeProvider == null)
                return;

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                await ESAssets.WaitForPendingOperationsAsync(cancellationToken);
                await ESRuntimeDataAsset.WaitForAssetConfigReadersAsync(cancellationToken);
                ESRuntimeDataAsset.RotateGenerationAtSafePoint(runtimeProvider, ESAssets.RuntimeBackendGeneration);
                await ESRuntimeDataAsset.WaitForAssetConfigReadersAsync(cancellationToken);
                await ESAssets.UnloadAllAtSafePointAsync(cancellationToken);
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>由 Consumer GameCore 重新注入完成后调用，恢复 Provider 切换前仍在进入状态的目标。</summary>
        internal UniTask RestoreResourcePlansAfterGameCoreAsync(CancellationToken cancellationToken = default)
        {
            ESResourcePlanRuntimeService plans = ESGameManager.ResourcePlans;
            return plans == null
                ? UniTask.CompletedTask
                : plans.RestoreAfterProviderTransitionAsync(cancellationToken);
        }

        private static void QuiesceScopesAndPlans(bool preserveTargetsForProviderTransition)
        {
            // GameCore 表保存的是 Consumer GameCore SO 的强引用。全量安全点或
            // Provider 切换必须先清表，再释放 Scope/Provider，不能留下旧对象。
            ESRuntimeDataGameCore.ResetForResourceTransition();
            ESResourcePlanRuntimeService plans = ESGameManager.ResourcePlans;
            if (preserveTargetsForProviderTransition)
                plans?.SuspendForProviderTransition();
            else
                plans?.Dispose();
            ESAssets.ResetScopesForProviderTransition();
        }

        private void DisposeCurrentProviderCore()
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            ESRuntimeAssetCatalog catalog = assetCatalog;

            // Invalidate the service before invoking cleanup callbacks. If any cleanup
            // callback throws, callers must observe a non-ready service instead of a stale
            // RuntimeBackend that looks usable after a failed transition.
            runtimeProvider = null;
            assetCatalog = null;
            initialized = false;
            var failures = new List<Exception>(2);
            try { ESRuntimeDataAsset.DetachRuntimeProvider(provider); }
            catch (Exception exception) { failures.Add(exception); }
            try { ESRuntimeAssetCatalog.Deactivate(catalog); }
            catch (Exception exception) { failures.Add(exception); }
            try { ESAssets.DetachRuntimeBackend(provider); }
            catch (Exception exception) { failures.Add(exception); }
            try { provider?.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }

            if (failures.Count == 1)
                throw failures[0];
            if (failures.Count > 1)
                throw new AggregateException("[ESRes][Provider] Provider 清理过程中发生多个异常。", failures);
        }

        private void AttachProvider(IESAssetRuntimeProvider provider)
        {
            runtimeProvider = provider;
            ESAssets.AttachRuntimeBackend(runtimeProvider);
            ESRuntimeDataAsset.AttachRuntimeProvider(
                runtimeProvider,
                ESAssets.RuntimeBackendGeneration == int.MaxValue ? 1 : ESAssets.RuntimeBackendGeneration + 1);
            assetCatalog = new ESRuntimeAssetCatalog();
            ESRuntimeAssetCatalog.Activate(assetCatalog);
            initialized = true;
        }
    }
}
