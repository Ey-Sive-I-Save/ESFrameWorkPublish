using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.Tests
{
    public sealed class ESAssetConfigTableFailureMatrixTests
    {
        [Test]
        [Parallelizable(ParallelScope.None)]
        public void AssetCatalog_MissingOrEmptyCandidate_PreservesCurrentGeneration()
        {
            const string businessKey = "tests.matrix.empty-candidate";
            var seedCatalog = new ESRuntimeCatalog();
            seedCatalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-matrix-empty-seed",
                "Empty Matrix Seed",
                "matrix-empty-library"));

            try
            {
                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { seedCatalog }), Is.EqualTo(1));
                long committedGeneration = ESRuntimeDataAsset.AssetConfigTableGeneration;

                Assert.Throws<InvalidOperationException>(() =>
                    ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(null));
                Assert.That(ESRuntimeDataAsset.AssetConfigTableGeneration, Is.EqualTo(committedGeneration));

                Assert.Throws<InvalidOperationException>(() =>
                    ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                        Array.Empty<ESRuntimeCatalog>()));
                Assert.That(ESRuntimeDataAsset.AssetConfigTableGeneration, Is.EqualTo(committedGeneration));

                Assert.Throws<InvalidOperationException>(() =>
                    ESRuntimeDataAsset.RebuildAssetConfigTablesFromPages(
                        Array.Empty<ESAssetPage>()));
                Assert.That(ESRuntimeDataAsset.AssetConfigTableGeneration, Is.EqualTo(committedGeneration));

                Assert.That(ESRuntimeDataAsset.Prefabs.TryResolveAssetIdentity(
                    0,
                    businessKey,
                    out ESAssetIdentity identity), Is.True);
                Assert.That(identity.Guid, Is.EqualTo("guid-matrix-empty-seed"));
            }
            finally
            {
                ESRuntimeDataAsset.ClearAssetConfigTables();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderSwitchCancellationBeforeMutation_FailsClosed()
        {
            var service = new ESRuntimeDataAssetLoadingService();
            var provider = new CountingProvider();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Cancellation test requires an isolated resource runtime.");
                Assert.Throws<OperationCanceledException>(() =>
                    service.InitializeAsync(provider, cancellation.Token)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult());

                Assert.That(ESAssets.IsReady, Is.False);
                Assert.That(ESRuntimeDataAsset.AssetConfigTablesAvailable, Is.False);
                Assert.That(ESRuntimeDataAsset.AssetConfigProviderBindingCurrent, Is.False);
                Assert.That(provider.DisposeCount, Is.Zero,
                    "Cancellation before mutation must not create or dispose the new provider.");
            }
            finally
            {
                service.Dispose();
                ESRuntimeDataAsset.ClearAssetConfigTables();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderInitFailure_LeavesNoHalfReadyState_AndRetryCanBootstrap()
        {
            const string businessKey = "tests.matrix.bootstrap-retry";
            var service = new ESRuntimeDataAssetLoadingService();
            var failingProvider = new CountingProvider();
            var retryProvider = new CountingProvider();
            var catalog = new ESRuntimeCatalog();
            catalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-matrix-bootstrap-retry",
                "Bootstrap Retry",
                "matrix-retry-library"));

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Retry test requires an isolated resource runtime.");
                Assert.Throws<InvalidOperationException>(() =>
                    service.InitializeAsync(
                            failingProvider,
                            () => throw new InvalidOperationException("matrix-init-failure"),
                            CancellationToken.None)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult());

                Assert.That(ESAssets.IsReady, Is.False);
                Assert.That(ESRuntimeDataAsset.AssetConfigTablesAvailable, Is.False);
                Assert.That(ESRuntimeDataAsset.AssetConfigProviderBindingCurrent, Is.False);
                Assert.That(service.IsInitialized, Is.False);
                Assert.That(failingProvider.DisposeCount, Is.EqualTo(1));

                service.InitializeAsync(
                        retryProvider,
                        () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { catalog }),
                        CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();

                Assert.That(ESAssets.IsReady, Is.True);
                Assert.That(ESRuntimeDataAsset.AssetConfigTablesAvailable, Is.True);
                Assert.That(ESRuntimeDataAsset.AssetConfigProviderBindingCurrent, Is.True);
                Assert.That(service.IsInitialized, Is.True);
                Assert.That(ESRuntimeDataAsset.Prefabs.TryResolveAssetIdentity(
                    0,
                    businessKey,
                    out ESAssetIdentity identity), Is.True);
                Assert.That(identity.Guid, Is.EqualTo("guid-matrix-bootstrap-retry"));
            }
            finally
            {
                service.Dispose();
                ESRuntimeDataAsset.ClearAssetConfigTables();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ConcurrentOldAndNewReaders_KeepTheirGenerationPinned()
        {
            const string businessKey = "tests.matrix.concurrent-readers";
            var firstCatalog = new ESRuntimeCatalog();
            firstCatalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-matrix-concurrent-v1",
                "Concurrent V1",
                "matrix-concurrent-v1-library"));
            var secondCatalog = new ESRuntimeCatalog();
            secondCatalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-matrix-concurrent-v2",
                "Concurrent V2",
                "matrix-concurrent-v2-library"));
            ESAssetConfigDataReadLease<ESAssetReferPrefabConfigData, GameObject> oldLease = null;
            ESAssetConfigDataReadLease<ESAssetReferPrefabConfigData, GameObject> newLease = null;

            try
            {
                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { firstCatalog }), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryAcquireConfigData(
                    businessKey,
                    out oldLease), Is.True);
                long oldGeneration = oldLease.Generation;
                ESAssetReferPrefabConfigData oldData = oldLease.Data;

                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { secondCatalog }), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryAcquireConfigData(
                    businessKey,
                    out newLease), Is.True);

                Assert.That(newLease.Generation, Is.GreaterThan(oldGeneration));
                Assert.That(newLease.Data, Is.Not.SameAs(oldData));
                Assert.That(oldData.AssetGuid, Is.EqualTo("guid-matrix-concurrent-v1"));
                Assert.That(newLease.Data.AssetGuid, Is.EqualTo("guid-matrix-concurrent-v2"));

                newLease.Dispose();
                newLease = null;
                Assert.That(oldData.AssetGuid, Is.EqualTo("guid-matrix-concurrent-v1"));

                oldLease.Dispose();
                oldLease = null;
            }
            finally
            {
                newLease?.Dispose();
                oldLease?.Dispose();
                ESRuntimeDataAsset.ClearAssetConfigTables();
            }
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ProviderDispose_WaitsForOldConfigReaderBeforeReclaimingGeneration()
        {
            const string businessKey = "tests.matrix.provider-dispose-reader";
            var provider = new CountingProvider();
            var service = new ESRuntimeDataAssetLoadingService();
            var catalog = new ESRuntimeCatalog();
            catalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-matrix-provider-dispose-reader",
                "Provider Dispose Reader",
                "matrix-provider-dispose-library"));
            ESAssetConfigDataReadLease<ESAssetReferPrefabConfigData, GameObject> reader = null;

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Provider dispose test requires an isolated resource runtime.");
                service.InitializeAsync(
                        provider,
                        () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { catalog }),
                        CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Assert.That(ESRuntimeDataAsset.Prefabs.TryAcquireConfigData(
                    businessKey,
                    out reader), Is.True);

                Assert.Throws<InvalidOperationException>(() => service.Dispose());
                Assert.That(provider.DisposeCount, Is.Zero);

                reader.Dispose();
                reader = null;
                service.Dispose();

                Assert.That(provider.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                reader?.Dispose();
                if (service.IsInitialized)
                    service.Dispose();
                ESRuntimeDataAsset.ClearAssetConfigTables();
            }
        }

        private static ESRuntimeCatalogEntry CreatePrefabCatalogEntry(
            string stringKey,
            string guid,
            string pageName,
            string libraryFolder)
        {
            return new ESRuntimeCatalogEntry
            {
                identity = new ESRuntimeCatalogIdentity
                {
                    guid = guid,
                    localFileId = 0
                },
                assetTypeName = typeof(GameObject).FullName,
                kind = ESAssetReferKind.Prefab.ToString(),
                stringKey = stringKey,
                libraryName = "Tests",
                libraryFolder = libraryFolder,
                pageName = pageName,
                isBusinessAsset = true
            };
        }

        private sealed class CountingProvider : IESAssetRuntimeProvider
        {
            public int DisposeCount { get; private set; }

            public UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(
                ESAssetIdentity id,
                CancellationToken cancellationToken = default) where T : UnityEngine.Object
                => UniTask.FromResult(default(ESRuntimeAssetHandle<T>));

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

            public void Release(ESAssetIdentity id)
            {
            }

            public UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(
                CancellationToken cancellationToken = default)
                => UniTask.FromResult(default(ESRuntimeUnusedAssetUnloadResult));

            public UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadZeroReferenceAssetBundlesAtSafePointAsync(
                CancellationToken cancellationToken = default)
                => UniTask.FromResult(default(ESRuntimeUnusedAssetBundleUnloadResult));

            public void UnloadAllAtSafePoint()
            {
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
