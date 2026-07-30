using NUnit.Framework;

using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESConfigKeyTableTests
    {
        private enum TestEnumKey : ushort
        {
            None = 0,
            EnumEntry = 7
        }

        private sealed class TestConfigKey : ESGameCoreConfigKey<TestEnumKey> { }
        private sealed class TestAssetConfigKey : ESAssetConfigKey<TestEnumKey> { }

        private sealed class TestAssetConfigData : ESAssetReferConfigDataBase<GameObject> { }
        private sealed class TestAssetIdentityScriptableObject : ScriptableObject { }

        private static ESRuntimeCatalogEntry CreatePrefabCatalogEntry(
            string stringKey,
            string guid,
            string pageName,
            string libraryFolder,
            long localFileId = 0)
        {
            return new ESRuntimeCatalogEntry
            {
                identity = new ESRuntimeCatalogIdentity
                {
                    guid = guid,
                    localFileId = localFileId
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

        private sealed class TestAssetLoader : IESAssetConfigTableLoader<TestAssetConfigData, GameObject>, System.IDisposable
        {
            private System.Action<GameObject, string> pending;

            public GameObject NextAsset { get; set; }
            public bool CompleteImmediately { get; set; } = true;
            public System.Exception SynchronousException { get; set; }
            public int LoadCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void LoadAsync(
                int runtimeKey,
                TestAssetConfigData configData,
                System.Action<GameObject, string> completed)
            {
                LoadCount++;
                if (SynchronousException != null)
                    throw SynchronousException;
                if (CompleteImmediately)
                    completed?.Invoke(NextAsset, null);
                else
                    pending = completed;
            }

            public void CompletePending()
            {
                System.Action<GameObject, string> completed = pending;
                pending = null;
                completed?.Invoke(NextAsset, null);
            }

            public void Release(
                int runtimeKey,
                TestAssetConfigData configData,
                GameObject asset)
            {
                ReleaseCount++;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class TestRuntimeData
        {
            public int runtimeKey;
        }

        private sealed class TestGameCoreRuntimeData : ESGameCoreRuntimeData
        {
            public object authority;

            public TestGameCoreRuntimeData() { }

            protected override void ReleaseRuntimePayload()
            {
                authority = null;
            }

        }

        [Test]
        public void StringOnlyRegistration_ReturnsCurrentTableRuntimeKey()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "string-only" };
            var data = new TestRuntimeData();

            table.BeginBuild(clear: true);
            try
            {
                int firstBake = table.Bake(key);
                int repeatedBake = table.Bake(key);
                data.runtimeKey = table.RegisterAndGetRuntimeKey(key, data, debugName: "string-only");

                Assert.That(repeatedBake, Is.EqualTo(firstBake));
                Assert.That(data.runtimeKey, Is.EqualTo(firstBake));
                Assert.That(data.runtimeKey, Is.GreaterThanOrEqualTo(ESConfigKeyProtocol.DefaultStringRuntimeKeyStart));
                Assert.That(table.TryGetRuntimeKey(key.StringKey, out int mappedRuntimeKey), Is.True);
                Assert.That(mappedRuntimeKey, Is.EqualTo(data.runtimeKey));
                Assert.That(table.TryGetRuntimeKey(key, out int configRuntimeKey), Is.True);
                Assert.That(configRuntimeKey, Is.EqualTo(data.runtimeKey));
                Assert.That(table.GetRuntimeKey(key), Is.EqualTo(data.runtimeKey));
                Assert.That(table.TryGet(data.runtimeKey, out TestRuntimeData byRuntimeKey), Is.True);
                Assert.That(byRuntimeKey, Is.SameAs(data));
                Assert.That(table.TryGetByStringKey(key.StringKey, out TestRuntimeData byStringKey), Is.True);
                Assert.That(byStringKey, Is.SameAs(data));
            }
            finally
            {
                table.EndBuild();
            }
        }

        [Test]
        public void ConfigKeyRuntimeKey_IsAvailableOnlyAfterInjection()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var key = new TestAssetConfigKey { enumKey = TestEnumKey.EnumEntry };

            Assert.That(table.TryGetRuntimeKey(key, out int missingRuntimeKey), Is.False);
            Assert.That(missingRuntimeKey, Is.Zero);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => table.GetRuntimeKey(key));

            var data = new TestRuntimeData();
            data.runtimeKey = table.Inject(key, data);

            Assert.That(table.IsBuilding, Is.False);
            Assert.That(data.runtimeKey, Is.EqualTo((int)TestEnumKey.EnumEntry));
            Assert.That(table.TryGetRuntimeKey(key, out int runtimeKey), Is.True);
            Assert.That(runtimeKey, Is.EqualTo(data.runtimeKey));
            Assert.That(table.GetRuntimeKey(key), Is.EqualTo(data.runtimeKey));
        }

        [Test]
        public void AssetPage_MainAssetIdentity_NormalizesUnityFileIdToZero()
        {
            string path = "Assets/Plugins/ES/1_Design/Tests/__Temp_ESAssetIdentity_"
                + System.Guid.NewGuid().ToString("N") + ".asset";
            var asset = ScriptableObject.CreateInstance<TestAssetIdentityScriptableObject>();
            bool created = false;

            try
            {
                AssetDatabase.CreateAsset(asset, path);
                created = true;
                AssetDatabase.SaveAssets();

                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _), Is.True);
                Assert.That(ESAssetPage.TryGetAssetIdentityEditor(asset, out string normalizedGuid, out long normalizedLocalFileId), Is.True);
                Assert.That(normalizedGuid, Is.EqualTo(guid));
                Assert.That(normalizedLocalFileId, Is.Zero);

                ESAssetPage page = ESAssetPage.Create(asset);
                Assert.That(page.AssetGuid, Is.EqualTo(guid));
                Assert.That(page.LocalFileId, Is.Zero);
            }
            finally
            {
                if (created)
                    AssetDatabase.DeleteAsset(path);
                else
                    Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetTable_ReleaseAndCatalogRebuild_ReuseStableConfigShell()
        {
            var table = new ESAssetConfigKeyTable<TestAssetConfigData, GameObject>(4, "Tests.Asset.Retained");
            var key = new TestAssetConfigKey { stringKey = "characters.hero" };
            TestAssetConfigData shell = table.AcquireRetained(key, () => new TestAssetConfigData());
            shell.SetAssetIdentity("guid-v1", 0);
            int runtimeKey = table.Inject(key, shell);
            var loader = new TestAssetLoader();
            var firstAsset = new GameObject("AssetTable_First");
            var secondAsset = new GameObject("AssetTable_Second");

            try
            {
                table.SetLoader(loader);
                loader.NextAsset = firstAsset;
                table.GetOrLoadAsync(key, null);

                Assert.That(table.TryGetReady(key, out GameObject firstReady), Is.True);
                Assert.That(firstReady, Is.SameAs(firstAsset));
                Assert.That(table.Release(key), Is.True);
                Assert.That(shell.LoadedAssetReady, Is.False);
                Assert.That(shell.LoadedAsset, Is.Null);
                Assert.That(loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(table.TryGet(runtimeKey, out TestAssetConfigData afterRelease), Is.True);
                Assert.That(afterRelease, Is.SameAs(shell));

                loader.NextAsset = secondAsset;
                table.GetOrLoadAsync(runtimeKey, null);
                Assert.That(table.TryGetReady(runtimeKey, out GameObject secondReady), Is.True);
                Assert.That(secondReady, Is.SameAs(secondAsset));
                Assert.That(loader.LoadCount, Is.EqualTo(2));

                table.BeginBuild(clear: true);
                table.EndBuild();
                Assert.That(loader.ReleaseCount, Is.EqualTo(2));
                Assert.That(shell.LoadedAssetReady, Is.False);
                Assert.That(shell.LoadedAsset, Is.Null);

                var rebuiltKey = new TestAssetConfigKey { stringKey = key.StringKey };
                TestAssetConfigData rebuilt = table.AcquireRetained(rebuiltKey, () => new TestAssetConfigData());
                rebuilt.SetAssetIdentity("guid-v2", 0);
                int rebuiltRuntimeKey = table.Inject(rebuiltKey, rebuilt);

                Assert.That(rebuilt, Is.SameAs(shell));
                Assert.That(rebuilt.AssetGuid, Is.EqualTo("guid-v2"));
                Assert.That(rebuiltRuntimeKey, Is.EqualTo(runtimeKey));
            }
            finally
            {
                Object.DestroyImmediate(firstAsset);
                Object.DestroyImmediate(secondAsset);
            }
        }

        [Test]
        public void AssetCatalog_RebuildRefreshesCompleteDataAndRejectsDuplicateWithoutMutation()
        {
            const string businessKey = "characters.catalog-duplicate";
            var seedCatalog = new ESRuntimeCatalog();
            seedCatalog.assets.Add(CreatePrefabCatalogEntry(
                businessKey,
                "guid-seed",
                "Seed",
                "seed-library"));

            try
            {
                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { seedCatalog }), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    businessKey,
                    out ESAssetReferPrefabConfigData seedData), Is.True);
                seedData.version = "stale-version-must-not-survive";

                var rebuiltCatalog = new ESRuntimeCatalog();
                rebuiltCatalog.assets.Add(CreatePrefabCatalogEntry(
                    businessKey,
                    "guid-first",
                    "First Authoritative Entry",
                    "first-library"));
                rebuiltCatalog.assets.Add(CreatePrefabCatalogEntry(
                    businessKey,
                    "guid-duplicate-must-not-win",
                    "Duplicate Entry",
                    "duplicate-library"));

                int injected = ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { rebuiltCatalog });

                Assert.That(injected, Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.GetAssetConflictCount(), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.GetAssetConflictReport(), Does.Contain("Duplicate Asset Catalog business key"));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    businessKey,
                    out ESAssetReferPrefabConfigData rebuiltData), Is.True);
                Assert.That(rebuiltData, Is.SameAs(seedData));
                Assert.That(rebuiltData.keyName, Is.EqualTo(businessKey));
                Assert.That(rebuiltData.displayName, Is.EqualTo("First Authoritative Entry"));
                Assert.That(rebuiltData.sourcePackage, Is.EqualTo("first-library"));
                Assert.That(rebuiltData.version, Is.Empty);
                Assert.That(rebuiltData.AssetGuid, Is.EqualTo("guid-first"));
                Assert.That(rebuiltData.AssetLocalFileId, Is.Zero);
                Assert.That(rebuiltData.key, Is.Not.Null);
                Assert.That(rebuiltData.key.StringKey, Is.EqualTo(businessKey));
                Assert.That(rebuiltData.key.guid, Is.EqualTo("guid-first"));
                Assert.That(rebuiltData.key.localFileId, Is.Zero);
                Assert.That(rebuiltData.key.assetTypeName, Is.EqualTo(typeof(GameObject).FullName));
                Assert.That(rebuiltData.runtimeKey, Is.EqualTo(
                    ESRuntimeDataAsset.Prefabs.GetRuntimeKey(rebuiltData.key)));
                Assert.That(rebuiltData.LoadedAssetReady, Is.False);
            }
            finally
            {
                ESRuntimeDataAsset.BeginBuild(true);
                ESRuntimeDataAsset.EndBuild();
            }
        }

        [Test]
        public void AssetCatalog_PreflightFailure_PreservesPreviousMappingsAndReadyPayload()
        {
            const string retainedKey = "characters.atomic-catalog-retained";
            const string stagedKey = "characters.atomic-catalog-staged";
            var seedCatalog = new ESRuntimeCatalog();
            seedCatalog.assets.Add(CreatePrefabCatalogEntry(
                retainedKey,
                "guid-atomic-catalog-old",
                "Atomic Catalog Old",
                "atomic-old-library"));
            var readyAsset = new GameObject("AtomicCatalogReadyAsset");

            try
            {
                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { seedCatalog }), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    retainedKey,
                    out ESAssetReferPrefabConfigData oldData), Is.True);
                int oldRuntimeKey = oldData.runtimeKey;
                oldData.SetLoadedAsset(readyAsset);

                var invalidCatalog = new ESRuntimeCatalog();
                invalidCatalog.assets.Add(CreatePrefabCatalogEntry(
                    stagedKey,
                    "guid-atomic-catalog-staged",
                    "Would Be Staged",
                    "atomic-staged-library"));
                invalidCatalog.assets.Add(CreatePrefabCatalogEntry(
                    null,
                    "guid-atomic-catalog-invalid",
                    "Invalid Missing Key",
                    "atomic-invalid-library"));

                Assert.Throws<System.InvalidOperationException>(() =>
                    ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { invalidCatalog }));

                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    retainedKey,
                    out ESAssetReferPrefabConfigData retainedData), Is.True);
                Assert.That(retainedData, Is.SameAs(oldData));
                Assert.That(retainedData.runtimeKey, Is.EqualTo(oldRuntimeKey));
                Assert.That(retainedData.LoadedAssetReady, Is.True);
                Assert.That(retainedData.LoadedAsset, Is.SameAs(readyAsset));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGet(
                    oldRuntimeKey,
                    out ESAssetReferPrefabConfigData retainedByRuntimeKey), Is.True);
                Assert.That(retainedByRuntimeKey, Is.SameAs(oldData));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    stagedKey,
                    out ESAssetReferPrefabConfigData _), Is.False);
            }
            finally
            {
                ESRuntimeDataAsset.BeginBuild(true);
                ESRuntimeDataAsset.EndBuild();
                Object.DestroyImmediate(readyAsset);
            }
        }

        [Test]
        public void AssetPage_PreflightFailure_PreservesPreviousMappingsAndReadyPayload()
        {
            const string retainedKey = "characters.atomic-page-retained";
            const string stagedKey = "characters.atomic-page-staged";
            var seedCatalog = new ESRuntimeCatalog();
            seedCatalog.assets.Add(CreatePrefabCatalogEntry(
                retainedKey,
                "guid-atomic-page-old",
                "Atomic Page Old",
                "atomic-page-old-library"));
            var readyAsset = new GameObject("AtomicPageReadyAsset");

            try
            {
                Assert.That(ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(
                    new[] { seedCatalog }), Is.EqualTo(1));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    retainedKey,
                    out ESAssetReferPrefabConfigData oldData), Is.True);
                int oldRuntimeKey = oldData.runtimeKey;
                oldData.SetLoadedAsset(readyAsset);

                var validPage = new ESAssetPage
                {
                    Kind = ESAssetReferKind.Prefab,
                    StringKey = stagedKey,
                    AssetGuid = "guid-atomic-page-staged",
                    AssetTypeName = typeof(GameObject).FullName,
                    Name = "Would Be Staged"
                };
                var invalidPage = new ESAssetPage
                {
                    Kind = ESAssetReferKind.Prefab,
                    EnumKey = ushort.MaxValue + 1,
                    AssetGuid = "guid-atomic-page-invalid",
                    AssetTypeName = typeof(GameObject).FullName,
                    Name = "Invalid Enum Key"
                };

                Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                    ESRuntimeDataAsset.RebuildAssetConfigTablesFromPages(new[] { validPage, invalidPage }));

                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    retainedKey,
                    out ESAssetReferPrefabConfigData retainedData), Is.True);
                Assert.That(retainedData, Is.SameAs(oldData));
                Assert.That(retainedData.runtimeKey, Is.EqualTo(oldRuntimeKey));
                Assert.That(retainedData.LoadedAssetReady, Is.True);
                Assert.That(retainedData.LoadedAsset, Is.SameAs(readyAsset));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGet(
                    oldRuntimeKey,
                    out ESAssetReferPrefabConfigData retainedByRuntimeKey), Is.True);
                Assert.That(retainedByRuntimeKey, Is.SameAs(oldData));
                Assert.That(ESRuntimeDataAsset.Prefabs.TryGetByStringKey(
                    stagedKey,
                    out ESAssetReferPrefabConfigData _), Is.False);
            }
            finally
            {
                ESRuntimeDataAsset.BeginBuild(true);
                ESRuntimeDataAsset.EndBuild();
                Object.DestroyImmediate(readyAsset);
            }
        }

        [Test]
        public void AssetTable_ReleaseWhileLoading_CompletesAsReleasedWithoutRestoringReady()
        {
            var table = new ESAssetConfigKeyTable<TestAssetConfigData, GameObject>(4, "Tests.Asset.PendingRelease");
            var key = new TestAssetConfigKey { stringKey = "characters.pending" };
            TestAssetConfigData shell = table.AcquireRetained(key, () => new TestAssetConfigData());
            shell.SetAssetIdentity("guid-pending", 0);
            int runtimeKey = table.Inject(key, shell);
            var asset = new GameObject("AssetTable_Pending");
            var loader = new TestAssetLoader
            {
                CompleteImmediately = false,
                NextAsset = asset
            };
            GameObject callbackAsset = asset;
            string callbackError = null;

            try
            {
                table.SetLoader(loader);
                table.GetOrLoadAsync(runtimeKey, (loaded, error) =>
                {
                    callbackAsset = loaded;
                    callbackError = error;
                });

                Assert.That(table.Release(runtimeKey), Is.True);
                loader.CompletePending();

                Assert.That(callbackAsset, Is.Null);
                Assert.That(callbackError, Does.Contain("加载完成前已释放"));
                Assert.That(shell.LoadedAssetReady, Is.False);
                Assert.That(shell.LoadedAsset, Is.Null);
                Assert.That(loader.ReleaseCount, Is.EqualTo(1));
                Assert.That(table.TryGet(runtimeKey, out TestAssetConfigData retained), Is.True);
                Assert.That(retained, Is.SameAs(shell));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetTable_ResetLoader_CancelsMergedPendingCallbacksAndIgnoresLateCompletion()
        {
            var table = new ESAssetConfigKeyTable<TestAssetConfigData, GameObject>(4, "Tests.Asset.ProviderSwitch");
            var key = new TestAssetConfigKey { stringKey = "characters.provider-switch" };
            TestAssetConfigData shell = table.AcquireRetained(key, () => new TestAssetConfigData());
            shell.SetAssetIdentity("guid-provider-switch", 0);
            int runtimeKey = table.Inject(key, shell);
            var oldAsset = new GameObject("AssetTable_ProviderSwitch_Old");
            var newAsset = new GameObject("AssetTable_ProviderSwitch_New");
            var oldLoader = new TestAssetLoader
            {
                CompleteImmediately = false,
                NextAsset = oldAsset
            };
            var newLoader = new TestAssetLoader
            {
                CompleteImmediately = false,
                NextAsset = newAsset
            };
            int callbackCount = 0;
            int newRequestCallbackCount = 0;
            GameObject firstAsset = oldAsset;
            GameObject secondAsset = oldAsset;
            GameObject loadedByNewRequest = null;
            string firstError = null;
            string secondError = null;
            string newRequestError = null;

            try
            {
                table.SetLoader(oldLoader);
                table.GetOrLoadAsync(runtimeKey, (loaded, error) =>
                {
                    callbackCount++;
                    firstAsset = loaded;
                    firstError = error;
                });
                table.GetOrLoadAsync(runtimeKey, (loaded, error) =>
                {
                    callbackCount++;
                    secondAsset = loaded;
                    secondError = error;
                });

                Assert.That(table.HasPendingLoads, Is.True);
                Assert.That(oldLoader.LoadCount, Is.EqualTo(1));

                table.SetLoader(newLoader);

                Assert.That(callbackCount, Is.EqualTo(2));
                Assert.That(firstAsset, Is.Null);
                Assert.That(secondAsset, Is.Null);
                Assert.That(firstError, Does.Contain("Provider 已切换"));
                Assert.That(secondError, Does.Contain("Provider 已切换"));
                Assert.That(table.HasPendingLoads, Is.False);
                Assert.That(table.HasLoader, Is.True);
                Assert.That(oldLoader.DisposeCount, Is.EqualTo(1));
                Assert.That(shell.LoadedAssetReady, Is.False);

                table.GetOrLoadAsync(runtimeKey, (loaded, error) =>
                {
                    newRequestCallbackCount++;
                    loadedByNewRequest = loaded;
                    newRequestError = error;
                });
                Assert.That(newLoader.LoadCount, Is.EqualTo(1));
                Assert.That(table.HasPendingLoads, Is.True);

                oldLoader.CompletePending();

                Assert.That(callbackCount, Is.EqualTo(2));
                Assert.That(newRequestCallbackCount, Is.Zero);
                Assert.That(table.HasPendingLoads, Is.True);
                Assert.That(shell.LoadedAssetReady, Is.False);
                Assert.That(shell.LoadedAsset, Is.Null);
                Assert.That(oldLoader.ReleaseCount, Is.EqualTo(1));

                newLoader.CompletePending();

                Assert.That(newRequestCallbackCount, Is.EqualTo(1));
                Assert.That(newRequestError, Is.Null);
                Assert.That(loadedByNewRequest, Is.SameAs(newAsset));
                Assert.That(table.HasPendingLoads, Is.False);
                Assert.That(table.TryGetReady(runtimeKey, out GameObject ready), Is.True);
                Assert.That(ready, Is.SameAs(newAsset));
            }
            finally
            {
                table.ResetLoader();
                Object.DestroyImmediate(oldAsset);
                Object.DestroyImmediate(newAsset);
            }
        }

        [Test]
        public void AssetTable_SynchronousLoaderFailure_CompletesTransactionAndAllowsRetry()
        {
            var table = new ESAssetConfigKeyTable<TestAssetConfigData, GameObject>(4, "Tests.Asset.SyncFailure");
            var key = new TestAssetConfigKey { stringKey = "characters.sync-failure" };
            TestAssetConfigData shell = table.AcquireRetained(key, () => new TestAssetConfigData());
            shell.SetAssetIdentity("guid-sync-failure", 0);
            table.Inject(key, shell);
            var loader = new TestAssetLoader
            {
                SynchronousException = new System.InvalidOperationException("同步加载失败")
            };
            var asset = new GameObject("AssetTable_SyncFailure_Retry");
            int firstCallbackCount = 0;
            string firstError = null;
            GameObject retriedAsset = null;

            try
            {
                table.SetLoader(loader);
                table.GetOrLoadAsync(key, (loaded, error) =>
                {
                    firstCallbackCount++;
                    firstError = error;
                });

                Assert.That(firstCallbackCount, Is.EqualTo(1));
                Assert.That(firstError, Does.Contain("同步加载失败"));
                Assert.That(table.HasPendingLoads, Is.False);
                Assert.That(table.TryGetReady(key, out _), Is.False);

                loader.SynchronousException = null;
                loader.NextAsset = asset;
                table.GetOrLoadAsync(key, (loaded, error) =>
                {
                    Assert.That(error, Is.Null);
                    retriedAsset = loaded;
                });

                Assert.That(loader.LoadCount, Is.EqualTo(2));
                Assert.That(retriedAsset, Is.SameAs(asset));
                Assert.That(table.TryGetReady(key, out GameObject ready), Is.True);
                Assert.That(ready, Is.SameAs(asset));
            }
            finally
            {
                table.ResetLoader();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetTable_ResetLoader_ContinuesAfterOneCancellationCallbackThrows()
        {
            var table = new ESAssetConfigKeyTable<TestAssetConfigData, GameObject>(4, "Tests.Asset.CallbackIsolation");
            var key = new TestAssetConfigKey { stringKey = "characters.callback-isolation" };
            TestAssetConfigData shell = table.AcquireRetained(key, () => new TestAssetConfigData());
            shell.SetAssetIdentity("guid-callback-isolation", 0);
            table.Inject(key, shell);
            var loader = new TestAssetLoader { CompleteImmediately = false };
            int secondCallbackCount = 0;

            table.SetLoader(loader);
            table.GetOrLoadAsync(key, (_, __) =>
                throw new System.InvalidOperationException("callback isolation sentinel"));
            table.GetOrLoadAsync(key, (loaded, error) =>
            {
                secondCallbackCount++;
                Assert.That(loaded, Is.Null);
                Assert.That(error, Does.Contain("Provider 已切换"));
            });

            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("callback isolation sentinel"));
            table.ResetLoader();

            Assert.That(secondCallbackCount, Is.EqualTo(1));
            Assert.That(table.HasPendingLoads, Is.False);
        }

        [Test]
        public void GameCoreInjection_IsSingleLineAndLeavesTableLocked()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "dynamic-game-core" };
            var data = new TestRuntimeData();

            data.runtimeKey = table.Inject(key, data, debugName: "dynamic-game-core");

            Assert.That(table.IsBuilding, Is.False);
            Assert.That(data.runtimeKey, Is.GreaterThanOrEqualTo(ESConfigKeyProtocol.DefaultStringRuntimeKeyStart));
            Assert.That(table.GetRuntimeKey(key), Is.EqualTo(data.runtimeKey));
            Assert.That(table.TryGet(data.runtimeKey, out TestRuntimeData injected), Is.True);
            Assert.That(injected, Is.SameAs(data));
        }

        [Test]
        public void RawStringInjection_BakesTemporaryRuntimeKeyAndPreservesConflictRules()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var original = new TestRuntimeData();

            int firstRuntimeKey = table.Inject("raw-string", original);
            int repeatedRuntimeKey = table.Inject("raw-string", original);

            Assert.That(repeatedRuntimeKey, Is.EqualTo(firstRuntimeKey));
            Assert.That(firstRuntimeKey, Is.GreaterThanOrEqualTo(ESConfigKeyProtocol.DefaultStringRuntimeKeyStart));
            Assert.That(table.TryGetByStringKey("raw-string", out TestRuntimeData injected), Is.True);
            Assert.That(injected, Is.SameAs(original));

            Assert.That(table.TryInject("raw-string", new TestRuntimeData(), out int rejectedRuntimeKey), Is.False);
            Assert.That(rejectedRuntimeKey, Is.Zero);
            Assert.That(table.IsBuilding, Is.False);
        }

        [Test]
        public void Injection_IsIdempotentForSameInstance_AndRejectsDifferentInstance()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "idempotent" };
            var original = new TestRuntimeData();

            int firstRuntimeKey = table.Inject(key, original);
            int repeatedRuntimeKey = table.Inject(key, original);

            Assert.That(repeatedRuntimeKey, Is.EqualTo(firstRuntimeKey));

            var conflicting = new TestRuntimeData();
            Assert.That(table.TryInject(key, conflicting, out int rejectedRuntimeKey), Is.False);
            Assert.That(rejectedRuntimeKey, Is.Zero);
            Assert.That(table.IsBuilding, Is.False);

            var exception = Assert.Throws<System.InvalidOperationException>(() => table.Inject(key, conflicting));
            Assert.That(exception.Message, Does.Contain("同 Key 已被不同数据实例占用"));
            Assert.That(exception.Message, Does.Not.Contain("Upsert"));
            Assert.That(exception.Message, Does.Contain("由具体表类型的生命周期规则决定"));
            Assert.That(table.IsBuilding, Is.False);
            Assert.That(table.TryGet(firstRuntimeKey, out TestRuntimeData retained), Is.True);
            Assert.That(retained, Is.SameAs(original));
        }

        [Test]
        public void TryInject_InvalidInput_ReturnsFalseWithoutOpeningBuild()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var invalidKey = new TestConfigKey();

            Assert.That(table.TryInject(invalidKey, new TestRuntimeData(), out int runtimeKey), Is.False);
            Assert.That(runtimeKey, Is.Zero);
            Assert.That(table.IsBuilding, Is.False);
        }

        [Test]
        public void StandardRetainedTable_ClearReusesShellAndRejectsReplacement()
        {
            var table = new ESRetainedConfigKeyTable<TestRuntimeData>(4, "Tests.StandardRetained");
            var key = new TestConfigKey { stringKey = "standard-retained" };
            int factoryCalls = 0;

            TestRuntimeData shell = table.AcquireRetained(key, () =>
            {
                factoryCalls++;
                return new TestRuntimeData();
            });
            int runtimeKey = table.Inject(key, shell);

            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(table.TryGet(runtimeKey, out TestRuntimeData active), Is.True);
            Assert.That(active, Is.SameAs(shell));

            table.BeginBuild(clear: true);
            table.EndBuild();

            TestRuntimeData reused = table.AcquireRetained(key, () =>
            {
                factoryCalls++;
                return new TestRuntimeData();
            });

            Assert.That(reused, Is.SameAs(shell));
            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(table.TryInject(key, new TestRuntimeData(), out int rejectedRuntimeKey), Is.False);
            Assert.That(rejectedRuntimeKey, Is.Zero);
            Assert.That(table.Count, Is.Zero);

            int rebuiltRuntimeKey = table.Inject(key, reused);
            Assert.That(rebuiltRuntimeKey, Is.EqualTo(runtimeKey));
            Assert.That(table.TryGet(rebuiltRuntimeKey, out TestRuntimeData rebuilt), Is.True);
            Assert.That(rebuilt, Is.SameAs(shell));
        }

        [Test]
        public void StringOnlyRuntimeKey_IsDeterministicForItsTableScope()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4);
            var targetKey = new TestConfigKey { stringKey = "target" };
            int firstRuntimeKey;

            table.BeginBuild(clear: true);
            try
            {
                var firstData = new TestRuntimeData();
                firstData.runtimeKey = table.RegisterAndGetRuntimeKey(targetKey, firstData, debugName: "target-first-build");
                firstRuntimeKey = firstData.runtimeKey;
            }
            finally
            {
                table.EndBuild();
            }

            table.BeginBuild(clear: true);
            try
            {
                var precedingData = new TestRuntimeData();
                precedingData.runtimeKey = table.RegisterAndGetRuntimeKey(
                    new TestConfigKey { stringKey = "preceding" },
                    precedingData,
                    debugName: "preceding-second-build");

                var secondData = new TestRuntimeData();
                secondData.runtimeKey = table.RegisterAndGetRuntimeKey(targetKey, secondData, debugName: "target-second-build");

                Assert.That(secondData.runtimeKey, Is.EqualTo(firstRuntimeKey),
                    "StringKey 的进程内 RuntimeKey 必须不受同表注册顺序影响。");
                Assert.That(table.TryGetRuntimeKey(targetKey.StringKey, out int mappedRuntimeKey), Is.True);
                Assert.That(mappedRuntimeKey, Is.EqualTo(secondData.runtimeKey));
                Assert.That(table.TryGet(secondData.runtimeKey, out TestRuntimeData currentData), Is.True);
                Assert.That(currentData, Is.SameAs(secondData));
            }
            finally
            {
                table.EndBuild();
            }
        }

        [Test]
        public void RetainedTable_Remove_KeepsReferenceAndMarksItNotReady()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "pooled" };
            TestGameCoreRuntimeData data = table.AcquireRetained(key);
            data.authority = new object();
            data.runtimeKey = table.Inject(key, data);
            int firstRuntimeKey = data.runtimeKey;

            Assert.That(data.Ready, Is.True);

            table.BeginBuild();
            try
            {
                Assert.That(table.Remove(firstRuntimeKey), Is.True);
            }
            finally
            {
                table.EndBuild();
            }

            Assert.That(data.Ready, Is.False);
            Assert.That(data.runtimeKey, Is.EqualTo(firstRuntimeKey));
            Assert.That(data.authority, Is.Null);

            TestGameCoreRuntimeData retained = table.AcquireRetained(key);
            Assert.That(retained, Is.SameAs(data));
            retained.runtimeKey = table.Inject(key, retained);
            Assert.That(retained.Ready, Is.True);
        }

        [Test]
        public void GameCoreTable_Clear_KeepsExternalRuntimeDataShell()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "external" };
            var data = new TestGameCoreRuntimeData { authority = new object() };
            data.runtimeKey = table.Inject(key, data);

            Assert.That(data.Ready, Is.True);
            Assert.That(data, Is.Not.InstanceOf<IPoolableAuto>());

            table.BeginBuild(clear: true);
            table.EndBuild();

            Assert.That(data.Ready, Is.False);
            Assert.That(data.runtimeKey, Is.Not.Zero);
            Assert.That(data.authority, Is.Null);
            Assert.That(table.AcquireRetained(key), Is.SameAs(data));
        }

        [Test]
        public void RetainedTable_Commit_SynchronizesRuntimeKeyBeforeReady()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "commit-order" };
            TestGameCoreRuntimeData data = table.AcquireRetained(key);
            data.authority = new object();

            int committedRuntimeKey = table.CommitRetained(key, data, "commit-order");

            Assert.That(committedRuntimeKey, Is.Not.Zero);
            Assert.That(data.runtimeKey, Is.EqualTo(committedRuntimeKey));
            Assert.That(data.Ready, Is.True);
            Assert.That(data.authority, Is.Not.Null);
        }

        [Test]
        public void RetainedTable_AddingEnumAlias_ReturnsExistingSlotRuntimeKey()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var stringOnly = new TestConfigKey { stringKey = "alias-target" };
            TestGameCoreRuntimeData data = table.AcquireRetained(stringOnly);
            int originalRuntimeKey = table.CommitRetained(stringOnly, data);
            var withEnumAlias = new TestConfigKey
            {
                enumKey = TestEnumKey.EnumEntry,
                stringKey = stringOnly.StringKey
            };

            int aliasedRuntimeKey = table.CommitRetained(withEnumAlias, data);

            Assert.That(aliasedRuntimeKey, Is.EqualTo(originalRuntimeKey));
            Assert.That(data.runtimeKey, Is.EqualTo(originalRuntimeKey));
            Assert.That(data.Ready, Is.True);
            Assert.That(table.TryGet(withEnumAlias, out TestGameCoreRuntimeData aliased), Is.True);
            Assert.That(aliased, Is.SameAs(data));
        }

        [Test]
        public void RetainedTable_FailedCommit_ReleasesPayloadAndKeepsStableShell()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var activeKey = new TestConfigKey { enumKey = TestEnumKey.EnumEntry, stringKey = "active" };
            TestGameCoreRuntimeData active = table.AcquireRetained(activeKey);
            Assert.That(table.TryCommitRetained(activeKey, active, out _), Is.True);

            var retainedKey = new TestConfigKey { stringKey = "retained" };
            TestGameCoreRuntimeData retained = table.AcquireRetained(retainedKey);
            retained.authority = new object();

            var conflictingAliases = new TestConfigKey
            {
                enumKey = TestEnumKey.EnumEntry,
                stringKey = retainedKey.StringKey
            };
            Assert.That(table.TryCommitRetained(conflictingAliases, retained, out int rejectedRuntimeKey), Is.False);
            Assert.That(rejectedRuntimeKey, Is.Zero);
            Assert.That(retained.Ready, Is.False);
            Assert.That(retained.authority, Is.Null);
            Assert.That(table.AcquireRetained(retainedKey), Is.SameAs(retained));
        }

        [Test]
        public void RetainedTable_ExplicitAbandon_IsIdempotent()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "abandon" };
            TestGameCoreRuntimeData data = table.AcquireRetained(key);
            data.authority = new object();

            table.AbandonRetained(data);
            table.AbandonRetained(data);

            Assert.That(data.Ready, Is.False);
            Assert.That(data.authority, Is.Null);
            Assert.That(table.AcquireRetained(key), Is.SameAs(data));
        }

        [Test]
        public void RetainedTable_PrepareException_ReleasesPayloadAndReusesStableShell()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "prepare-exception" };
            TestGameCoreRuntimeData retained = null;

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                retained = table.AcquireRetained(key);
                try
                {
                    retained.authority = new object();
                    throw new System.InvalidOperationException("模拟 filler 失败");
                }
                catch
                {
                    table.AbandonRetained(retained);
                    throw;
                }
            });

            Assert.That(retained, Is.Not.Null);
            Assert.That(retained.Ready, Is.False);
            Assert.That(retained.authority, Is.Null);

            TestGameCoreRuntimeData reused = table.AcquireRetained(key);
            Assert.That(reused, Is.SameAs(retained));
            reused.authority = new object();
            int runtimeKey = table.CommitRetained(key, reused);

            Assert.That(runtimeKey, Is.Not.Zero);
            Assert.That(reused.runtimeKey, Is.EqualTo(runtimeKey));
            Assert.That(reused.Ready, Is.True);
            Assert.That(reused.authority, Is.Not.Null);
        }

        [Test]
        public void BuffTryInjectWithDefaults_FillerException_ReleasesPayloadAndReusesStableShell()
        {
            var table = new ESBuffConfigKeyTable(4);
            var key = new ESBuffConfigKey { stringKey = "buff-filler-exception" };
            var expected = new System.InvalidOperationException("真实 Buff filler 失败");

            System.InvalidOperationException actual = Assert.Throws<System.InvalidOperationException>(() =>
                table.TryInjectWithDefaults(
                    key,
                    out _,
                    fillShared: (ref BuffSharedData value) => throw expected));

            Assert.That(actual, Is.SameAs(expected));

            ESBuffRuntimeData retained = table.AcquireRetained(key);
            Assert.That(retained.Ready, Is.False);
            Assert.That(retained.sharedData, Is.Null);
            Assert.That(retained.defaultVariableData, Is.Null);

            Assert.That(table.TryInjectWithDefaults(key, out int runtimeKey), Is.True);
            Assert.That(runtimeKey, Is.Not.Zero);
            Assert.That(retained.Ready, Is.True);
            Assert.That(retained.runtimeKey, Is.EqualTo(runtimeKey));
            Assert.That(table.TryGet(runtimeKey, out ESBuffRuntimeData current), Is.True);
            Assert.That(current, Is.SameAs(retained));
        }

        [Test]
        public void RetainedTable_ExplicitAbandon_DoesNotInvalidateCommittedData()
        {
            var table = new ESGameCoreConfigKeyTable<TestGameCoreRuntimeData>(4);
            var key = new TestConfigKey { stringKey = "committed" };
            TestGameCoreRuntimeData data = table.AcquireRetained(key);
            object authority = new object();
            data.authority = authority;
            int runtimeKey = table.CommitRetained(key, data);

            table.AbandonRetained(data);

            Assert.That(data.Ready, Is.True);
            Assert.That(data.runtimeKey, Is.EqualTo(runtimeKey));
            Assert.That(data.authority, Is.SameAs(authority));
            Assert.That(table.TryGet(runtimeKey, out TestGameCoreRuntimeData current), Is.True);
            Assert.That(current, Is.SameAs(data));
        }

        [Test]
        public void ConfigKey_WithBothAliases_RejectsAnInconsistentReference()
        {
            var table = new ESConfigKeyTable<TestRuntimeData>(4, "Tests.ConfigKeyAliases");
            var definition = new TestConfigKey { enumKey = TestEnumKey.EnumEntry, stringKey = "combat.attack" };
            var data = new TestRuntimeData();
            data.runtimeKey = table.Inject(definition, data);

            var wrongAlias = new TestConfigKey { enumKey = TestEnumKey.EnumEntry, stringKey = "combat.defend" };
            Assert.That(table.TryGetRuntimeKey(wrongAlias, out _), Is.False);
            Assert.That(table.TryGet(wrongAlias, out _), Is.False);
            Assert.That(table.TryGetRuntimeKey(definition, out int resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(data.runtimeKey));
        }

        [Test]
        public void StableKeyCatalog_BuildIsOrderIndependentAndBindsAliases()
        {
            ESKeyCatalog first = CreateTestCatalog(reverse: false);
            ESKeyCatalog second = CreateTestCatalog(reverse: true);

            Assert.That(first.SchemaHash, Is.EqualTo(second.SchemaHash));
            Assert.That(first.TryGetRuntimeKey(new ESStableKey("Tests.Attribute", 7, "combat.attack"), out int firstRuntime), Is.True);
            Assert.That(second.TryGetRuntimeKey(new ESStableKey("Tests.Attribute", 7, "combat.attack"), out int secondRuntime), Is.True);
            Assert.That(firstRuntime, Is.EqualTo(secondRuntime));
            Assert.That(first.TryGetRuntimeKey(new ESStableKey("Tests.Attribute", 0, "combat.attack"), out int stringAliasRuntime), Is.True);
            Assert.That(stringAliasRuntime, Is.EqualTo(firstRuntime));
            Assert.That(first.IsCompatibleWith(second.CreateHandshake(), out _), Is.True);
        }

        private static ESKeyCatalog CreateTestCatalog(bool reverse)
        {
            ESKeyDeclaration attack = new ESKeyDeclaration
            {
                key = new ESStableKey("Tests.Attribute", 7, "combat.attack"),
                kind = ESKeyCatalogKind.Attribute,
                valueKind = ESKeyValueKind.Float,
                storagePolicy = ESKeyStoragePolicy.HotSlot,
                schemaSignature = "default=10|min=0|max=100|formula=base",
                declaredBy = "Tests"
            };
            ESKeyDeclaration health = new ESKeyDeclaration
            {
                key = new ESStableKey("Tests.Attribute", 0, "health.max"),
                kind = ESKeyCatalogKind.Attribute,
                valueKind = ESKeyValueKind.Float,
                storagePolicy = ESKeyStoragePolicy.Sparse,
                schemaSignature = "default=100|min=1|max=10000|formula=base",
                declaredBy = "Tests"
            };

            ESKeyCatalog catalog = new ESKeyCatalog("Tests.Attributes", "Tests.Attribute");
            if (reverse)
            {
                catalog.Declare(health);
                catalog.Declare(attack);
            }
            else
            {
                catalog.Declare(attack);
                catalog.Declare(health);
            }
            catalog.BuildOrThrow();
            return catalog;
        }
    }
}
