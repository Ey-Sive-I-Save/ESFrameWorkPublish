using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ES.EditorInternal;

namespace ES.Tests
{
    public sealed class ESContentRegistrationTests
    {
        private const string TestRoot = "Assets/__ESContentRegistrationTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__ESContentRegistrationTests");
        }

        [TearDown]
        public void TearDown()
        {
            ESAssetCatalogKeyPicker.Invalidate();
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void StableKeyRules_AcceptStringOnlyAndRejectImplicitNormalization()
        {
            Assert.That(ESContentStringKeyRules.TryValidateStableKey(
                ESContentStableKeyMode.StringOnly,
                0,
                "武器.近战.long_bar",
                out ESContentStableKeyMode mode,
                out string error), Is.True, error);
            Assert.That(mode, Is.EqualTo(ESContentStableKeyMode.StringOnly));

            Assert.That(ESContentStringKeyRules.TryValidateStringKey(" weapon.long_bar ", out error), Is.False);
            Assert.That(error, Does.Contain("Trim"));
            Assert.That(ESContentStringKeyRules.TryValidateStringKey("weapon\nlong_bar", out error), Is.False);
            Assert.That(ESContentStringKeyRules.TryValidateStringKey("e\u0301", out error), Is.False);
            Assert.That(error, Does.Contain("NFC"));
        }

        [Test]
        public void CommitWithoutCurrentProcessPreview_FailsClosed()
        {
            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                requestId = "missing-preview-" + Guid.NewGuid().ToString("N"),
                commit = true,
                assetPath = TestRoot + "/Missing.asset",
                libraryPath = TestRoot + "/MissingLibrary.asset",
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.missing.preview"
            };

            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(result.success, Is.False);
            Assert.That(result.status, Is.EqualTo("preview_required"));
        }

        [UnityTest]
        public IEnumerator EditorLongTask_MultipleFinishedSubscribers_EachReceiveOneReceipt()
        {
            int firstReceipts = 0;
            int secondReceipts = 0;
            var task = new ImmediateSuccessLongTask();
            task.AddFinishedCallback(_ => firstReceipts++);
            task.AddFinishedCallback(_ => secondReceipts++);

            ESEditorHandle.EnqueueLongTask(task);
            while (!task.IsFinished)
                yield return null;

            Assert.That(task.Status, Is.EqualTo(ESEditorLongTaskStatus.Succeeded));
            Assert.That(firstReceipts, Is.EqualTo(1));
            Assert.That(secondReceipts, Is.EqualTo(1));
        }

        [Test]
        public void GameCoreRuntimeData_UsesTypedPrefabKeysInsteadOfDirectAssets()
        {
            Type[] runtimeTypes =
            {
                typeof(ESMonsterRuntimeData),
                typeof(ESNpcRuntimeData),
                typeof(ESBuffRuntimeData),
                typeof(ESSkillRuntimeData),
                typeof(ESWeaponRuntimeData),
                typeof(ESShotRuntimeData)
            };

            foreach (Type runtimeType in runtimeTypes)
            {
                Assert.That(runtimeType.GetField("prefab"), Is.Null, runtimeType.FullName);
                Assert.That(runtimeType.GetField("extraAsset"), Is.Null, runtimeType.FullName);
                Assert.That(runtimeType.GetField("prefabKey")?.FieldType,
                    Is.EqualTo(typeof(ESAssetReferPrefabConfigKey)),
                    runtimeType.FullName);
            }
        }

        [Test]
        public void CatalogProtocol_EditorAndRuntimeUseTheSameVersion()
        {
            Assert.That(new ESAssetLibraryCatalog().formatVersion,
                Is.EqualTo(ESRuntimeCatalog.CurrentFormatVersion));
        }

        [Test]
        public void AssetRegistration_StringOnlyCommit_IsPersistentAndIdempotent()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string materialPath = TestRoot + "/Content.mat";
            string libraryPath = TestRoot + "/Library.asset";
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(library);

            string suffix = Guid.NewGuid().ToString("N");
            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.asset." + suffix,
                assetKind = "Material"
            };

            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(preview.success, Is.True, preview.message);
            Assert.That(preview.status, Is.EqualTo("validated"));
            Assert.That(preview.requestId, Is.Not.Empty);
            Assert.That(library.DefaultMaterialBook.pages, Is.Empty);

            var parallelRequest = JsonUtility.FromJson<ESContentRegistrationRequest>(JsonUtility.ToJson(request));
            ESContentRegistrationResult parallelPreview = ESContentRegistrationAuthoring.Execute(parallelRequest);
            Assert.That(parallelPreview.success, Is.True, parallelPreview.message);
            Assert.That(parallelPreview.requestId, Is.Not.EqualTo(preview.requestId));

            request.requestId = preview.requestId;
            request.commit = true;
            request.expectedGuid = preview.sourceGuid;
            request.expectedLocalFileId = preview.localFileId;
            request.expectedLibraryRevision = preview.targetRevision;
            ESContentRegistrationResult committed = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(committed.success, Is.True, committed.message);
            Assert.That(committed.status, Is.EqualTo("committed"));
            Assert.That(committed.sourceGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(materialPath)));
            Assert.That(committed.libraryGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(libraryPath)));
            Assert.That(library.DefaultMaterialBook.pages.Count, Is.EqualTo(1));
            Assert.That(library.DefaultMaterialBook.pages[0].StringKey, Is.EqualTo(request.stringKey));

            ESContentRegistrationResult replay = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(replay.success, Is.True, replay.message);
            Assert.That(replay.idempotent, Is.True);
            Assert.That(library.DefaultMaterialBook.pages.Count, Is.EqualTo(1));

            parallelRequest.requestId = parallelPreview.requestId;
            parallelRequest.commit = true;
            parallelRequest.expectedGuid = parallelPreview.sourceGuid;
            parallelRequest.expectedLocalFileId = parallelPreview.localFileId;
            parallelRequest.expectedLibraryRevision = parallelPreview.targetRevision;
            ESContentRegistrationResult parallelCommit = ESContentRegistrationAuthoring.Execute(parallelRequest);
            Assert.That(parallelCommit.success, Is.False);
            Assert.That(parallelCommit.status, Is.EqualTo("concurrency_conflict"),
                "第二个独立预检资格必须通过 Facade 门禁并在 revision CAS 处失败，不能被第一个提交误消费。");
            ESContentRegistrationResult retryWithoutPreview = ESContentRegistrationAuthoring.Execute(parallelRequest);
            Assert.That(retryWithoutPreview.status, Is.EqualTo("preview_required"),
                "一次提交尝试无论成功或 CAS 失败都必须消费预检资格。");

            request.stringKey += ".different";
            ESContentRegistrationResult conflict = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(conflict.success, Is.False);
            Assert.That(conflict.status, Is.EqualTo("idempotency_conflict"));
        }

        [UnityTest]
        public IEnumerator AssetPicker_RegistrationCommit_RefreshesOneLibraryOnceWithoutFullReload()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            ESAssetCatalogKeyPicker.RefreshForValidation();
            int fullReloads = ESAssetCatalogKeyPicker.FullReloadCount;
            int incrementalReloads = ESAssetCatalogKeyPicker.IncrementalLibraryReloadCount;
            string materialPath = TestRoot + "/Incremental.mat";
            string libraryPath = TestRoot + "/IncrementalLibrary.asset";
            string stringKey = "test.asset.incremental." + Guid.NewGuid().ToString("N");
            Material material = new Material(shader);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(library);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = stringKey,
                assetKind = ESAssetReferKind.Material.ToString()
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(preview.success, Is.True, preview.message);
            request.commit = true;
            request.requestId = preview.requestId;
            request.expectedGuid = preview.guid;
            request.expectedLocalFileId = preview.localFileId;
            request.expectedLibraryRevision = preview.targetRevision;
            ESContentRegistrationResult committed = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(committed.success, Is.True, committed.message);

            int reloadsAfterCommit = ESAssetCatalogKeyPicker.IncrementalLibraryReloadCount;
            Assert.That(ESAssetCatalogKeyPicker.FullReloadCount, Is.EqualTo(fullReloads));
            Assert.That(reloadsAfterCommit, Is.EqualTo(incrementalReloads + 1));
            Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                ESAssetReferKind.Material,
                0,
                stringKey,
                out ESAssetCatalogKeyPicker.Candidate candidate), Is.True);
            Assert.That(candidate.guid, Is.EqualTo(preview.guid));
            Assert.That(ESAssetCatalogKeyPicker.FullReloadCount, Is.EqualTo(fullReloads),
                "命中缓存的后续查询不得再次全量扫描 Library/Catalog。");

            yield return null;
            yield return null;
            Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                ESAssetReferKind.Material,
                0,
                stringKey,
                out ESAssetCatalogKeyPicker.Candidate delayedCandidate), Is.True);
            Assert.That(delayedCandidate.guid, Is.EqualTo(preview.guid));
            Assert.That(ESAssetCatalogKeyPicker.IncrementalLibraryReloadCount, Is.EqualTo(reloadsAfterCommit),
                "注册同步刷新已消费同路径通知，后续 delayCall/导入回调不得重复刷新该 Library。");
            Assert.That(ESAssetCatalogKeyPicker.FullReloadCount, Is.EqualTo(fullReloads),
                "延迟 Registry 版本同步可以重建索引，但不得退化为全量冷建。");
        }

        [Test]
        public void AssetPicker_CatalogRemoval_ClearsProjectedBakedState()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string suffix = Guid.NewGuid().ToString("N");
            string materialPath = TestRoot + "/BakedState.mat";
            string libraryPath = TestRoot + "/BakedStateLibrary.asset";
            string key = "test.asset.baked_state." + suffix;
            Material material = new Material(shader);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.CreateAsset(library, libraryPath);
            library.DefaultMaterialBook.pages.Add(ESAssetPage.Create(material));
            library.DefaultMaterialBook.pages[0].StringKey = key;
            AssetDatabase.SaveAssetIfDirty(library);
            Assert.That(ESAssetPage.TryGetAssetIdentityEditor(material, out string guid, out long localFileId), Is.True);

            string catalogFolder = Path.Combine(ESAssetPipelineIO.BakeRoot, "__picker_baked_" + suffix);
            try
            {
                Directory.CreateDirectory(catalogFolder);
                WriteCatalog(catalogFolder, guid, localFileId, materialPath, key, "BakedStateLibrary");
                ESAssetCatalogKeyPicker.RefreshForValidation();
                Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                    ESAssetReferKind.Material, 0, key, out ESAssetCatalogKeyPicker.Candidate baked), Is.True);
                Assert.That(baked.isBaked, Is.True);

                File.Delete(Path.Combine(catalogFolder, ESAssetPipelineIO.CatalogFileName));
                Directory.Delete(catalogFolder);
                ESAssetCatalogKeyPicker.NotifyCatalogsChanged();
                Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                    ESAssetReferKind.Material, 0, key, out ESAssetCatalogKeyPicker.Candidate unbaked), Is.True);
                Assert.That(unbaked.isBaked, Is.False,
                    "Catalog 删除后必须从来源候选重新投影，不能保留上次合并产生的 isBaked。");
            }
            finally
            {
                if (Directory.Exists(catalogFolder))
                    Directory.Delete(catalogFolder, true);
            }
        }

        [Test]
        public void AssetPicker_LibraryConflictResolution_ClearsProjectedConflictState()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string suffix = Guid.NewGuid().ToString("N");
            string materialPath = TestRoot + "/Conflict.mat";
            string firstLibraryPath = TestRoot + "/ConflictA.asset";
            string secondLibraryPath = TestRoot + "/ConflictB.asset";
            string firstKey = "test.asset.conflict.a." + suffix;
            string secondKey = "test.asset.conflict.b." + suffix;
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            ESAssetLibrary firstLibrary = CreateLibraryWithMaterial(firstLibraryPath, material, firstKey);
            ESAssetLibrary secondLibrary = CreateLibraryWithMaterial(secondLibraryPath, material, secondKey);

            ESAssetCatalogKeyPicker.RefreshForValidation();
            Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                ESAssetReferKind.Material, 0, firstKey, out ESAssetCatalogKeyPicker.Candidate conflicted), Is.True);
            Assert.That(conflicted.hasLibraryKeyConflict, Is.True);

            secondLibrary.DefaultMaterialBook.pages[0].StringKey = firstKey;
            AssetDatabase.SaveAssetIfDirty(secondLibrary);
            ESAssetCatalogKeyPicker.NotifyLibraryChanged(secondLibraryPath);
            Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                ESAssetReferKind.Material, 0, firstKey, out ESAssetCatalogKeyPicker.Candidate resolved), Is.True);
            Assert.That(resolved.hasLibraryKeyConflict, Is.False,
                "冲突解除后必须从来源候选重新投影，不能保留上次合并产生的冲突标记。");
            Assert.That(firstLibrary, Is.Not.Null);
        }

        [Test]
        public void AssetPicker_RecoveryCatalog_IsExplicitlyExcluded()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string recoveryRoot = Path.Combine(ESAssetPipelineIO.BakeRoot, ".Recovery");
            string catalogPath = Path.Combine(recoveryRoot, ESAssetPipelineIO.CatalogFileName);
            if (File.Exists(catalogPath))
                Assert.Ignore(".Recovery 根目录已有非标准 Catalog，测试不覆盖现有文件。");

            string key = "test.asset.recovery_excluded." + suffix;
            try
            {
                Directory.CreateDirectory(recoveryRoot);
                WriteCatalog(recoveryRoot, Guid.NewGuid().ToString("N"), 0,
                    TestRoot + "/Missing.mat", key, "RecoveryOnly");
                ESAssetCatalogKeyPicker.RefreshForValidation();
                Assert.That(ESAssetCatalogKeyPicker.TryFindByKey(
                    ESAssetReferKind.Material, 0, key, out _), Is.False,
                    ".Recovery 下即使存在可读且无 errors 的 Catalog，也不得进入当前候选索引。");
            }
            finally
            {
                if (File.Exists(catalogPath))
                    File.Delete(catalogPath);
            }
        }

        [Test]
        public void AssetRegistration_DryRun_DoesNotRefreshExistingPageIdentity()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string materialPath = TestRoot + "/DryRunContent.mat";
            string libraryPath = TestRoot + "/DryRunLibrary.asset";
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            var existing = new ESAssetPage
            {
                Name = material.name,
                OB = material,
                Kind = ESAssetReferKind.Material,
                StringKey = "test.asset.dry_run"
            };
            library.DefaultMaterialBook.pages.Add(existing);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = existing.StringKey,
                assetKind = "Material"
            };

            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(result.success, Is.True, result.message);
            Assert.That(result.status, Is.EqualTo("already_registered"));
            Assert.That(existing.AssetGuid, Is.Empty);
            Assert.That(existing.LocalFileId, Is.Zero);
            Assert.That(existing.AssetPath, Is.Empty);
            Assert.That(existing.AssetTypeName, Is.Empty);
        }

        [Test]
        public void AssetRegistration_Commit_RejectsDirtyTargetLibrary()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string materialPath = TestRoot + "/DirtyTargetContent.mat";
            string libraryPath = TestRoot + "/DirtyTargetLibrary.asset";
            Material material = new Material(shader);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(library);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                expectedLocalFileId = 0,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.asset.dirty_target",
                assetKind = "Material"
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(preview.success, Is.True, preview.message);

            library.LibFolderName = "unsaved_local_edit";
            EditorUtility.SetDirty(library);
            request.requestId = preview.requestId;
            request.commit = true;
            request.expectedGuid = preview.guid;
            request.expectedLocalFileId = preview.localFileId;
            request.expectedLibraryRevision = preview.targetRevision;

            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(result.success, Is.False);
            Assert.That(result.status, Is.EqualTo("target_dirty"));
            Assert.That(library.DefaultMaterialBook.pages, Is.Empty);
            Assert.That(EditorUtility.IsDirty(library), Is.True);
        }

        [Test]
        public void AssetKeyUpdate_PreviewCasCommitAndReplay_AreTransactional()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string suffix = Guid.NewGuid().ToString("N");
            string materialPath = TestRoot + "/KeyMutation.mat";
            string libraryPath = TestRoot + "/KeyMutationLibrary.asset";
            Material material = new Material(shader);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(library);

            var register = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                expectedLocalFileId = 0,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.key.old." + suffix,
                assetKind = "Material"
            };
            ESContentRegistrationResult registerPreview = ESContentRegistrationAuthoring.Execute(register);
            Assert.That(registerPreview.success, Is.True, registerPreview.message);
            register.requestId = registerPreview.requestId;
            register.commit = true;
            register.expectedGuid = registerPreview.guid;
            register.expectedLocalFileId = registerPreview.localFileId;
            register.expectedLibraryRevision = registerPreview.targetRevision;
            Assert.That(ESContentRegistrationAuthoring.Execute(register).success, Is.True);

            var update = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.UpdateAssetKey,
                commit = false,
                assetPath = materialPath,
                libraryPath = libraryPath,
                expectedLocalFileId = 0,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.key.new." + suffix,
                assetKind = "auto"
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(update);
            Assert.That(preview.success, Is.True, preview.message);
            Assert.That(preview.status, Is.EqualTo("validated"));
            Assert.That(library.DefaultMaterialBook.pages[0].StringKey, Is.EqualTo(register.stringKey));

            update.commit = true;
            update.requestId = preview.requestId;
            update.expectedGuid = preview.guid;
            update.expectedLocalFileId = preview.localFileId;
            update.expectedLibraryRevision = preview.targetRevision;
            update.hasExpectedCurrentKey = true;
            update.expectedCurrentEnumKey = preview.currentEnumKey;
            update.expectedCurrentStringKey = preview.currentStringKey;
            ESContentRegistrationResult committed = ESContentRegistrationAuthoring.Execute(update);
            Assert.That(committed.success, Is.True, committed.message);
            Assert.That(committed.status, Is.EqualTo("committed"));
            Assert.That(library.DefaultMaterialBook.pages[0].StringKey, Is.EqualTo(update.stringKey));

            ESContentRegistrationResult replay = ESContentRegistrationAuthoring.Execute(update);
            Assert.That(replay.success, Is.True, replay.message);
            Assert.That(replay.idempotent, Is.True);
        }

        [Test]
        public void GameCoreRoot_PreviewCommitAndReplay_LinkConsumerWithoutAssetTablePage()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string groupPath = TestRoot + "/RootGroup.asset";
            string consumerPath = TestRoot + "/RootConsumer.asset";
            ItemDataGroup group = ScriptableObject.CreateInstance<ItemDataGroup>();
            ESAssetLibraryConsumer consumer = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
            AssetDatabase.CreateAsset(group, groupPath);
            AssetDatabase.CreateAsset(consumer, consumerPath);
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(consumer);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterGameCoreRoot,
                commit = false,
                gameCorePath = groupPath,
                consumerPath = consumerPath,
                expectedLocalFileId = 0
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(preview.success, Is.True, preview.message + "\n" + string.Join("\n", preview.errors));
            Assert.That(preview.status, Is.EqualTo("validated"));
            Assert.That(consumer.ManualGameCoreAssets, Is.Empty);

            request.commit = true;
            request.requestId = preview.requestId;
            request.expectedSourceGuid = preview.sourceGuid;
            request.expectedConsumerGuid = preview.consumerGuid;
            request.expectedLocalFileId = preview.localFileId;
            request.expectedSourceRevision = preview.sourceRevision;
            request.expectedConsumerRevision = preview.consumerRevision;
            ESContentRegistrationResult committed = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(committed.success, Is.True, committed.message + "\n" + string.Join("\n", committed.errors));
            Assert.That(committed.status, Is.EqualTo("committed"));
            Assert.That(consumer.ManualGameCoreAssets.Any(entry =>
                entry != null && entry.GUID == preview.sourceGuid && entry.LocalFileId == preview.localFileId), Is.True);

            ESContentRegistrationResult replay = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(replay.success, Is.True, replay.message);
            Assert.That(replay.idempotent, Is.True);
        }

        [Test]
        public void GameCoreRegistration_ItemWeaponStringOnly_LinksGroupAndConsumer()
        {
            string sourcePath = TestRoot + "/Weapon.asset";
            string groupPath = TestRoot + "/ItemGroup.asset";
            string consumerPath = TestRoot + "/Consumer.asset";
            string prefabPath = TestRoot + "/Weapon.prefab";
            string libraryPath = TestRoot + "/Library.asset";
            string suffix = Guid.NewGuid().ToString("N");

            GameObject prefabRoot = new GameObject("TestWeapon");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(library);
            Assert.That(ESAssetPage.TryGetAssetIdentityEditor(prefab, out string prefabGuid, out long prefabLocalFileId), Is.True);

            var assetRequest = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = prefabPath,
                libraryPath = libraryPath,
                expectedLocalFileId = prefabLocalFileId,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.prefab.weapon." + suffix,
                assetKind = ESAssetReferKind.Prefab.ToString()
            };
            ESContentRegistrationResult assetPreview = ESContentRegistrationAuthoring.Execute(assetRequest);
            Assert.That(assetPreview.success, Is.True, assetPreview.message);
            assetRequest.requestId = assetPreview.requestId;
            assetRequest.commit = true;
            assetRequest.expectedGuid = assetPreview.guid;
            assetRequest.expectedLocalFileId = assetPreview.localFileId;
            assetRequest.expectedLibraryRevision = assetPreview.targetRevision;
            ESContentRegistrationResult assetResult = ESContentRegistrationAuthoring.Execute(assetRequest);
            Assert.That(assetResult.success, Is.True, assetResult.message);

            ItemDataInfo item = ScriptableObject.CreateInstance<ItemDataInfo>();
            item.baseConfig.kind = ItemKind.Weapon;
            item.baseConfig.prefabKey = new ESAssetReferPrefabConfigKey
            {
                stringKey = assetRequest.stringKey
            };
            item.baseConfig.prefabKey.SetAssetAuthority(
                prefabGuid,
                prefabLocalFileId,
                typeof(GameObject).FullName,
                prefabPath);
            item.EnsureActiveKindData();
            ((ItemWeaponDataBlock)item.kindData).sharedData.weaponKind = ItemWeaponKind.Melee;
            AssetDatabase.CreateAsset(item, sourcePath);
            ItemDataGroup group = ScriptableObject.CreateInstance<ItemDataGroup>();
            AssetDatabase.CreateAsset(group, groupPath);
            ESAssetLibraryConsumer consumer = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
            AssetDatabase.CreateAsset(consumer, consumerPath);
            AssetDatabase.SaveAssetIfDirty(item);
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(consumer);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterGameCore,
                commit = false,
                dataInfoPath = sourcePath,
                groupPath = groupPath,
                consumerPath = consumerPath,
                groupKey = "test_group_" + suffix,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = "test.weapon." + suffix,
                itemStringKey = "test.item.weapon." + suffix,
                gameCoreRoute = "item.weapon"
            };
            ESContentRegistrationResult gameCorePreview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(gameCorePreview.success, Is.True, gameCorePreview.message);
            request.requestId = gameCorePreview.requestId;
            request.commit = true;
            request.expectedSourceGuid = gameCorePreview.sourceGuid;
            request.expectedGroupGuid = gameCorePreview.groupGuid;
            request.expectedConsumerGuid = gameCorePreview.consumerGuid;
            request.expectedSourceRevision = gameCorePreview.sourceRevision;
            request.expectedGroupRevision = gameCorePreview.groupRevision;
            request.expectedConsumerRevision = gameCorePreview.consumerRevision;

            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(result.success, Is.True, result.message + "\n" + string.Join("\n", result.errors));
            Assert.That(result.sourceGuid, Is.EqualTo(request.expectedSourceGuid));
            Assert.That(result.groupGuid, Is.EqualTo(request.expectedGroupGuid));
            Assert.That(result.consumerGuid, Is.EqualTo(request.expectedConsumerGuid));
            Assert.That(group.GetInfoByKey(request.groupKey), Is.SameAs(item));
            Assert.That(item.GetKey(), Is.EqualTo(request.groupKey));
            Assert.That(item.TryGetGameCoreKey(out IESConfigKey key), Is.True);
            Assert.That(key.EnumKeyInt, Is.Zero);
            Assert.That(key.StringKey, Is.EqualTo(request.stringKey));
            Assert.That(item.itemKey.EnumKeyInt, Is.Zero);
            Assert.That(item.itemKey.StringKey, Is.EqualTo(request.itemStringKey));
            string groupGuid = AssetDatabase.AssetPathToGUID(groupPath);
            Assert.That(consumer.GameCoreAssets.Concat(consumer.ManualGameCoreAssets).Any(entry =>
                entry != null && entry.GUID == groupGuid && entry.LocalFileId == 0), Is.True);
            Assert.That(typeof(ItemBaseConfig).GetField("prefab"), Is.Null);
        }


        [Test]
        public void SynchronizeConsumer_Commit_DoesNotSaveUnrelatedDirtyAsset()
        {
            string targetPath = TestRoot + "/SyncConsumer.asset";
            string unrelatedPath = TestRoot + "/UnrelatedConsumer.asset";
            ESAssetLibraryConsumer target = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
            ESAssetLibraryConsumer unrelated = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
            AssetDatabase.CreateAsset(target, targetPath);
            AssetDatabase.CreateAsset(unrelated, unrelatedPath);
            AssetDatabase.SaveAssetIfDirty(target);
            AssetDatabase.SaveAssetIfDirty(unrelated);

            unrelated.InternalNotes = "unsaved-local-edit";
            EditorUtility.SetDirty(unrelated);
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True);

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Synchronize,
                commit = false,
                consumerPath = targetPath
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(preview.success, Is.True, preview.message);
            request.requestId = preview.requestId;
            request.commit = true;
            request.expectedConsumerGuid = preview.consumerGuid;
            request.expectedConsumerRevision = preview.consumerRevision;

            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            Assert.That(result.success, Is.True, result.message + "\n" + string.Join("\n", result.errors));
            Assert.That(result.consumerGuid, Is.EqualTo(request.expectedConsumerGuid));
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True,
                "目标 Consumer 同步不得通过 AssetDatabase.SaveAssets() 顺带保存无关资产。");
        }

        [Test]
        public void McpAdapter_SnakeCasePreviewAndCommit_UseUnifiedContract()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("当前项目没有可用于测试 Material 的 Shader。");

            string materialPath = TestRoot + "/McpContent.mat";
            string libraryPath = TestRoot + "/McpLibrary.asset";
            Material material = new Material(shader);
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(library);

            string stringKey = "test.mcp." + Guid.NewGuid().ToString("N");
            string json = "{"
                          + "\"action\":\"register_asset\","
                          + "\"commit\":false,"
                          + "\"asset_path\":\"" + materialPath + "\","
                          + "\"library_path\":\"" + libraryPath + "\","
                          + "\"asset_kind\":\"Material\","
                          + "\"key_mode\":\"StringOnly\","
                          + "\"string_key\":\"" + stringKey + "\""
                          + "}";

            ESContentRegistrationResult result = InvokeMcp(json);
            Assert.That(result.success, Is.True, result.message);
            Assert.That(result.dryRun, Is.True);
            Assert.That(result.status, Is.EqualTo("validated"));
            Assert.That(result.action, Is.EqualTo(ESContentRegistrationAction.RegisterAsset.ToString()));
            Assert.That(result.stringKey, Is.EqualTo(stringKey));
            Assert.That(result.sourceGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(materialPath)));
            Assert.That(result.libraryGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(libraryPath)));
            Assert.That(library.DefaultMaterialBook.pages, Is.Empty,
                "MCP commit=false 必须只走统一预检，不能写入 Library。");

            string requestId = result.requestId;
            string commitJson = "{"
                                + "\"action\":\"register_asset\","
                                + "\"request_id\":\"" + requestId + "\","
                                + "\"commit\":true,"
                                + "\"asset_path\":\"" + materialPath + "\","
                                + "\"library_path\":\"" + libraryPath + "\","
                                + "\"asset_kind\":\"Material\","
                                + "\"key_mode\":\"StringOnly\","
                                + "\"string_key\":\"" + stringKey + "\","
                                + "\"expected_guid\":\"" + result.sourceGuid + "\","
                                + "\"expected_local_file_id\":" + result.localFileId + ","
                                + "\"expected_library_revision\":\"" + result.targetRevision + "\""
                                + "}";

            ESContentRegistrationResult committed = InvokeMcp(commitJson);
            Assert.That(committed.success, Is.True, committed.message);
            Assert.That(committed.status, Is.EqualTo("committed"));
            Assert.That(committed.requestId, Is.EqualTo(requestId));
            Assert.That(library.DefaultMaterialBook.pages.Count, Is.EqualTo(1));
            Assert.That(library.DefaultMaterialBook.pages[0].StringKey, Is.EqualTo(stringKey));
        }

        [Test]
        public void McpAdapter_UnknownAction_FailsClosed()
        {
            ESContentRegistrationResult result = InvokeMcp("{\"action\":\"unknown_action\",\"commit\":false}");
            Assert.That(result.success, Is.False);
            Assert.That(result.status, Is.EqualTo("invalid_request"));
            Assert.That(result.message, Does.Contain("Unknown action"));
        }

        [Test]
        public void McpTool_RegistersInProjectCommandRegistry()
        {
            Assembly mcpAssembly = LoadMcpAssembly();
            Type initializerType = mcpAssembly.GetType(
                "ES.ESContentRegistrationMcpRegistryInitializer",
                throwOnError: true);
            Assert.That(typeof(EditorInvoker_Level2).IsAssignableFrom(initializerType), Is.True,
                "MCP 工具必须复用 ES AssemblyStream 初始化阶段，禁止再建一套 Domain Reload bootstrap。");
            var initializer = (EditorInvoker_Level2)Activator.CreateInstance(initializerType);
            initializer.InitInvoke();

            Type registryType = Type.GetType(
                "MCPForUnity.Editor.Tools.CommandRegistry, MCPForUnity.Editor",
                throwOnError: true);
            MethodInfo getHandler = registryType.GetMethod(
                "GetHandler",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(getHandler, Is.Not.Null);
            Assert.That(getHandler.Invoke(null, new object[] { "es_content_registration" }), Is.Not.Null);
        }

        private static ESAssetLibrary CreateLibraryWithMaterial(
            string libraryPath,
            Material material,
            string stringKey)
        {
            ESAssetLibrary library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            ESAssetPage page = ESAssetPage.Create(material);
            page.StringKey = stringKey;
            library.DefaultMaterialBook.pages.Add(page);
            AssetDatabase.SaveAssetIfDirty(library);
            return library;
        }

        private static void WriteCatalog(
            string folder,
            string guid,
            long localFileId,
            string assetPath,
            string stringKey,
            string libraryName)
        {
            var catalog = new ESAssetLibraryCatalog
            {
                libraryName = libraryName,
                libraryFolder = Path.GetFileName(folder)
            };
            catalog.assets.Add(new ESAssetCatalogEntry
            {
                identity = new ESPipelineAssetIdentity { guid = guid, localFileId = localFileId },
                assetPath = assetPath,
                assetTypeName = typeof(Material).FullName,
                kind = ESAssetReferKind.Material.ToString(),
                stringKey = stringKey,
                libraryName = libraryName,
                libraryFolder = catalog.libraryFolder,
                pageName = "Test",
                isBusinessAsset = true
            });
            ESAssetPipelineIO.WriteJson(
                Path.Combine(folder, ESAssetPipelineIO.CatalogFileName),
                catalog,
                true);
        }

        private static ESContentRegistrationResult InvokeMcp(string json)
        {
            Assembly mcpAssembly = LoadMcpAssembly();
            Type toolType = mcpAssembly.GetType("ES.ESContentRegistrationMcpTool", throwOnError: true);
            Type jObjectType = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json", throwOnError: true);
            MethodInfo parse = jObjectType.GetMethod("Parse", new[] { typeof(string) });
            object parameters = parse.Invoke(null, new object[] { json });
            MethodInfo handle = toolType.GetMethod("HandleCommand", BindingFlags.Public | BindingFlags.Static);
            object response = handle.Invoke(null, new[] { parameters });
            return JsonUtility.FromJson<ESContentRegistrationResult>(response.ToString());
        }

        private static Assembly LoadMcpAssembly()
            => AppDomain.CurrentDomain.GetAssemblies()
                   .FirstOrDefault(item => item.GetName().Name == "ES.ContentRegistration.MCP.Editor")
               ?? Assembly.Load("ES.ContentRegistration.MCP.Editor");

        private sealed class ImmediateSuccessLongTask : ESEditorLongTask
        {
            public ImmediateSuccessLongTask()
                : base("Content registration callback test", "ES.Tests.ContentRegistration.Callbacks", 100)
            {
            }

            public override ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context)
                => ESEditorLongTaskStepResult.Complete;
        }
    }
}
