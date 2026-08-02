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
        [NonParallelizable]
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
        [NonParallelizable]
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
                Assert.ThrowsAsync<InvalidCastException>(async () => await loading.AsTask());
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
        [NonParallelizable]
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

        private sealed class NoopProvider : IESAssetRuntimeProvider
        {
            public int MainAssetLoadCount { get; private set; }

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
            public void Dispose() { }
        }
    }
}
