using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.Tests
{
    public sealed class ESAssetScopePoolingTests
    {
        [Test]
        [Parallelizable(ParallelScope.None)]
        public void Dispose_RecyclesHeavyState_ButNeverReusesScopeShell()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            ESAssetScope first = null;
            ESAssetScope second = null;
            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Scope 池测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);

                int before = ESAssets.GetRuntimeDiagnostics().PooledScopeStateCount;
                first = ESAssets.CreateScope();
                int afterFirstRent = ESAssets.GetRuntimeDiagnostics().PooledScopeStateCount;

                first.Dispose();
                int afterFirstReturn = ESAssets.GetRuntimeDiagnostics().PooledScopeStateCount;
                Assert.That(first.IsDisposed, Is.True);
                Assert.That(afterFirstReturn, Is.EqualTo(afterFirstRent + 1));

                second = ESAssets.CreateScope();
                int afterSecondRent = ESAssets.GetRuntimeDiagnostics().PooledScopeStateCount;
                Assert.That(second, Is.Not.SameAs(first), "Scope 外壳不得复用，否则旧引用会产生 ABA 串线。");
                Assert.That(first.IsDisposed, Is.True, "旧 Scope 引用必须永久保持失效。");
                Assert.That(afterSecondRent, Is.EqualTo(afterFirstReturn - 1));

                second.Dispose();
                Assert.That(ESAssets.GetRuntimeDiagnostics().PooledScopeStateCount, Is.EqualTo(afterFirstReturn));
                Assert.That(afterFirstReturn, Is.GreaterThanOrEqualTo(before));
            }
            finally
            {
                second?.Dispose();
                first?.Dispose();
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void OwnerLoad_DoesNotBorrowActivePlanFastPath()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var ownerObject = new GameObject("ESAssetScopeOwnerTest");
            var plannedAsset = new GameObject("ESAssetScopePlannedAssetTest");
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("owner-independent-hold", 0, ESAssetReferKind.Prefab, 0, string.Empty);
            ESAssetIdentity identity = refer.AssetIdentity;
            bool planRegistered = false;

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Owner Scope 测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);
                ESAssets.RegisterActivePlanAsset(identity, plannedAsset);
                planRegistered = true;

                UniTask<GameObject> loading = refer.LoadAsync(ownerObject.transform);

                Assert.That(ownerObject.GetComponent<ESAssetOwnerTracker>(), Is.Not.Null,
                    "显式 Owner 加载必须创建/复用 OwnerTracker，不能直接返回活动 Plan 资产。");
                Assert.That(provider.MainAssetLoadCount, Is.EqualTo(1),
                    "显式 Owner 必须通过自己的 Scope 取得 Provider Lease；Provider 可缓存命中，但不能跳过所有权建立。");
                Assert.Throws<InvalidCastException>(() => loading.AsTask().GetAwaiter().GetResult());
            }
            finally
            {
                if (planRegistered)
                    ESAssets.UnregisterActivePlanAsset(identity);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(plannedAsset);
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void OwnerTryLoad_MissDoesNotCreateTrackerOrScope()
        {
            var ownerObject = new GameObject("ESAssetOwnedLookupMissTest");
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("owner-lookup-miss", 0, ESAssetReferKind.Prefab, 0, string.Empty);

            try
            {
                bool found = refer.TryLoad(ownerObject.transform, out GameObject asset);

                Assert.That(found, Is.False);
                Assert.That(asset, Is.Null);
                Assert.That(ownerObject.GetComponent<ESAssetOwnerTracker>(), Is.Null,
                    "Owner 同步未命中只是观察，不得为查询创建 Tracker 或 Scope。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void DefaultLoad_ImplicitlyCreatesGameSession_AndReleaseClosesIt()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("registry-default-load", 0, ESAssetReferKind.Prefab, 0, string.Empty);
            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Registry 测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);

                Assert.Throws<InvalidCastException>(() => refer.LoadAsync().AsTask().GetAwaiter().GetResult());
                Assert.That(ESAssets.ReleaseScope(ESAssetDomain.GameSession), Is.True,
                    "默认加载必须创建可由 GameFlow 统一关闭的 GameSession，而不是隐式 Resident。");
                Assert.That(ESAssets.ReleaseScope(ESAssetDomain.GameSession), Is.False,
                    "同一代 Registry Scope 只能完成一次逻辑关闭。");
            }
            finally
            {
                ESAssets.ReleaseScope(ESAssetDomain.GameSession);
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void GameInternal_PublicDomainEntry_IsRejected()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("game-internal-public-entry", 0, ESAssetReferKind.Prefab, 0, string.Empty);
            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "GameInternal 权限测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);

                Assert.Throws<InvalidOperationException>(() => ESAssets.CreateScope(ESAssetDomain.GameInternal));
                Assert.Throws<InvalidOperationException>(() => ESAssets.ReleaseScope(ESAssetDomain.GameInternal));
                Assert.Throws<InvalidOperationException>(() => refer.LoadAsync(ESAssetDomain.GameInternal));
                Assert.That(ESAssets.GetRuntimeDiagnostics().RegisteredScopeCount, Is.Zero,
                    "普通公共 Domain 入口拒绝 GameInternal 后，不得创建 Registry Scope。");
            }
            finally
            {
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ReleaseScope_KeepsClosingRegistrationVisibleDuringDisposeCallbacks()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("registry-closing-reentry", 0, ESAssetReferKind.Prefab, 0, string.Empty);
            Exception reentryFailure = null;
            Action<ESAssetScope> onEnding = _ =>
            {
                try { refer.LoadAsync(ESAssetDomain.Presentation); }
                catch (Exception exception) { reentryFailure = exception; }
            };

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Registry 测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);
                ESAssets.CreateScope(ESAssetDomain.Presentation);
                ESAssets.ScopeOwnershipEnding += onEnding;

                Assert.That(ESAssets.ReleaseScope(ESAssetDomain.Presentation), Is.True);
                Assert.That(reentryFailure, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains("正在关闭", reentryFailure.Message);
            }
            finally
            {
                ESAssets.ScopeOwnershipEnding -= onEnding;
                ESAssets.ReleaseScope(ESAssetDomain.Presentation);
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ParentRelease_ClosesChildrenBeforeRemovingParent()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Registry 测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);
                ESAssets.CreateScope(ESAssetDomain.GameSession);
                ESAssets.CreateScope("presentation:intro", ESAssetDomain.GameSession);

                Assert.That(ESAssets.ReleaseScope(ESAssetDomain.GameSession), Is.True);
                Assert.That(ESAssets.ReleaseScope("presentation:intro"), Is.False,
                    "父域关闭完成后，子域不得残留在 Registry。");
            }
            finally
            {
                ESAssets.ReleaseScope("presentation:intro");
                ESAssets.ReleaseScope(ESAssetDomain.GameSession);
                loadingService.Dispose();
            }
        }

        [TestCase("GameSession")]
        [TestCase("@domain:Scene")]
        [TestCase("NoPrefix")]
        [TestCase("UI:inventory")]
        public void StringScopeKey_RejectsReservedOrUnstableNames(string key)
        {
            Assert.Throws<ArgumentException>(() => ESAssets.CreateScope(key));
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderTransition_BlocksNewRequestsFromCapturedOldScope()
        {
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            ESAssetScope scope = null;
            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference("transition-old-scope", 0, ESAssetReferKind.Prefab, 0, string.Empty);
            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Transition 测试要求独立的资源运行时环境。");
                loadingService.Initialize(provider);
                scope = ESAssets.CreateScope();
                ESAssets.BeginProviderTransition();

                Assert.Throws<InvalidOperationException>(() => scope.LoadAsync(refer).AsTask().GetAwaiter().GetResult());
                Assert.That(provider.MainAssetLoadCount, Is.Zero,
                    "TransitionStarting 之后，已捕获旧 Scope 不得再把新请求送入旧 Provider。");
            }
            finally
            {
                scope?.Dispose();
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderBootstrap_CommitsCatalogTablesAndLoaderBindingAsOneGeneration()
        {
            const string businessKey = "tests.provider-generation";
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var catalog = new ESRuntimeCatalog();
            catalog.assets.Add(new ESRuntimeCatalogEntry
            {
                identity = new ESRuntimeCatalogIdentity { guid = "guid-provider-generation" },
                assetTypeName = typeof(GameObject).FullName,
                kind = ESAssetReferKind.Prefab.ToString(),
                stringKey = businessKey,
                isBusinessAsset = true
            });

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Provider generation 测试要求独立资源环境。");
                loadingService.InitializeAsync(
                        provider,
                        () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { catalog }),
                        CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();

                Assert.That(ESRuntimeDataAsset.AssetConfigTablesAvailable, Is.True);
                Assert.That(ESRuntimeDataAsset.AssetConfigProviderBindingCurrent, Is.True);
                Assert.That(ESRuntimeDataAsset.AssetConfigProviderGeneration,
                    Is.EqualTo(ESAssets.RuntimeBackendGeneration));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryResolveAssetIdentity(
                    0,
                    businessKey,
                    out ESAssetIdentity identity), Is.True);
                Assert.That(identity.Guid, Is.EqualTo("guid-provider-generation"));
            }
            finally
            {
                loadingService.Dispose();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderDispose_WaitsForAssetConfigPayloadLeaseBeforeReclaimingOldGeneration()
        {
            const string businessKey = "tests.provider-payload-lease";
            var provider = new NoopProvider();
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var asset = new GameObject("AssetConfigPayloadLease");
            var catalog = new ESRuntimeCatalog();
            catalog.assets.Add(new ESRuntimeCatalogEntry
            {
                identity = new ESRuntimeCatalogIdentity { guid = "guid-provider-payload-lease" },
                assetTypeName = typeof(GameObject).FullName,
                kind = ESAssetReferKind.Prefab.ToString(),
                stringKey = businessKey,
                isBusinessAsset = true
            });
            ESAssetConfigPayloadLease<GameObject> payloadLease = null;

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Payload Lease 测试要求独立资源环境。");
                loadingService.InitializeAsync(
                        provider,
                        () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { catalog }),
                        CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
                Assert.That(ESRuntimeDataAsset.Prefabs.TryAcquireConfigData(
                    businessKey,
                    out ESAssetConfigDataReadLease<ESAssetReferPrefabConfigData, GameObject> dataLease), Is.True);
                using (dataLease)
                    dataLease.Data.SetLoadedAsset(asset);

                var key = new ESAssetReferPrefabConfigKey { stringKey = businessKey };
                Assert.That(ESRuntimeDataAsset.Prefabs.TryAcquireReady(key, out payloadLease), Is.True);
                Assert.That(payloadLease.Asset, Is.SameAs(asset));
                Assert.That(ESRuntimeDataAsset.ActiveAssetConfigReaderCount, Is.EqualTo(1));

                Assert.Throws<InvalidOperationException>(() => loadingService.Dispose());
                Assert.That(provider.DisposeCount, Is.Zero);
                Assert.That(payloadLease.Asset, Is.SameAs(asset));

                payloadLease.Dispose();
                payloadLease.Dispose();
                payloadLease = null;
                Assert.That(ESRuntimeDataAsset.ActiveAssetConfigReaderCount, Is.Zero);

                loadingService.Dispose();
                Assert.That(provider.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                payloadLease?.Dispose();
                if (loadingService.IsInitialized)
                    loadingService.Dispose();
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private sealed class NoopProvider : IESAssetRuntimeProvider
        {
            public int MainAssetLoadCount { get; private set; }
            public int DisposeCount { get; private set; }

            public UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(
                ESAssetIdentity id,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
            {
                MainAssetLoadCount++;
                return UniTask.FromResult(default(ESRuntimeAssetHandle<T>));
            }

            public UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(
                ESAssetIdentity id,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
                => UniTask.FromResult(default(ESRuntimeAssetHandle<T>));

            public UniTask<ESRuntimeSceneHandle> LoadSceneAsync(
                ESAssetIdentity id,
                LoadSceneMode mode = LoadSceneMode.Single,
                CancellationToken cancellationToken = default)
                => UniTask.FromResult(default(ESRuntimeSceneHandle));

            public bool TryGetLoaded<T>(ESAssetIdentity id, out T asset) where T : UnityEngine.Object
            {
                asset = null;
                return false;
            }

            public bool TryGetStatus(ESAssetIdentity id, out ESRuntimeAssetLoadStatus status)
            {
                status = default;
                return false;
            }

            public void Release(ESAssetIdentity id) { }
            public UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default)
                => UniTask.FromResult(default(ESRuntimeUnusedAssetUnloadResult));
            public UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadZeroReferenceAssetBundlesAtSafePointAsync(CancellationToken cancellationToken = default)
                => UniTask.FromResult(default(ESRuntimeUnusedAssetBundleUnloadResult));
            public void UnloadAllAtSafePoint() { }
            public void Dispose() { DisposeCount++; }
        }
    }
}
