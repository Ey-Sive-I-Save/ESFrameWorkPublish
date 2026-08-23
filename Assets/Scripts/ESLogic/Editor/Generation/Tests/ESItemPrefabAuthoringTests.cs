using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESItemPrefabAuthoringTests
    {
        private const string TestRoot = "Assets/__ESItemPrefabAuthoringTests";
        private const string TargetLibraryPath = TestRoot + "/TargetLibrary.asset";
        private ESAssetLibrary targetLibrary;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__ESItemPrefabAuthoringTests");
            targetLibrary = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(targetLibrary, TargetLibraryPath);
            AssetDatabase.SaveAssetIfDirty(targetLibrary);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void CreateOrValidate_ReentryAfterPrefabReload_IsIdempotent()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Idempotent", suffix);

            ESItemPrefabAuthoringResult first = ESItemPrefabAuthoring.CreateOrValidate(
                TargetLibraryPath,
                request)[0];

            Assert.That(first.definitionCreated, Is.True);
            Assert.That(first.prefabCreated, Is.True);
            string prefabGuid = AssetDatabase.AssetPathToGUID(request.prefabPath);
            AssetDatabase.ImportAsset(request.prefabPath, ImportAssetOptions.ForceUpdate);

            ESItemPrefabAuthoringResult second = ESItemPrefabAuthoring.CreateOrValidate(
                TargetLibraryPath,
                request)[0];

            Assert.That(second.definitionCreated, Is.False);
            Assert.That(second.prefabCreated, Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(request.prefabPath), Is.EqualTo(prefabGuid));
            Assert.That(second.definition.baseConfig.prefabKey.StringKey, Is.EqualTo(request.prefabAssetKey));
            Assert.That(
                targetLibrary.TryGetPageByStringKey(
                    ESAssetReferKind.Prefab,
                    request.prefabAssetKey,
                    out ESAssetPage page),
                Is.True);
            Assert.That(page.AssetGuid, Is.EqualTo(prefabGuid).IgnoreCase);
            Assert.That(page.LocalFileId, Is.Zero);
        }

        [Test]
        public void CreateOrValidate_DirtyDefinition_FailsBeforeWritingPrefab()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Dirty", suffix);
            ItemDataInfo definition = CreateDefinitionAsset(request.definitionPath);
            definition.name = "Dirty Definition";
            EditorUtility.SetDirty(definition);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            StringAssert.Contains("未保存修改", exception.Message);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(request.prefabPath), Is.Null);
        }

        [Test]
        public void CreateOrValidate_CrossLibraryStringKeyConflict_FailsBeforeAnyRequestWrite()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Conflict", suffix);
            string otherLibraryPath = TestRoot + "/OtherLibrary.asset";
            string otherPrefabPath = TestRoot + "/Other.prefab";
            ESAssetLibrary otherLibrary = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(otherLibrary, otherLibraryPath);
            GameObject otherRoot = new GameObject("Other");
            GameObject otherPrefab;
            try
            {
                otherPrefab = PrefabUtility.SaveAsPrefabAsset(otherRoot, otherPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherRoot);
            }

            ESAssetPage page = ESAssetPage.Create(otherPrefab);
            page.Kind = ESAssetReferKind.Prefab;
            page.StringKey = request.prefabAssetKey;
            otherLibrary.DefaultPrefabBook.pages.Add(page);
            otherLibrary.MarkFastIndexDirty();
            EditorUtility.SetDirty(otherLibrary);
            AssetDatabase.SaveAssetIfDirty(otherLibrary);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            StringAssert.Contains("项目级注册预检失败", exception.Message);
            Assert.That(AssetDatabase.LoadAssetAtPath<ItemDataInfo>(request.definitionPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(request.prefabPath), Is.Null);
        }

        [Test]
        public void CreateOrValidate_CrossLibraryPrefabIdentityWithDifferentKey_FailsPreflight()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Identity Conflict", suffix);
            CreateDefinitionAsset(request.definitionPath);
            GameObject prefab = CreatePrefabAsset(request.prefabPath, "Shared Identity");

            string otherLibraryPath = TestRoot + "/IdentityOwnerLibrary.asset";
            ESAssetLibrary otherLibrary = ScriptableObject.CreateInstance<ESAssetLibrary>();
            AssetDatabase.CreateAsset(otherLibrary, otherLibraryPath);
            ESAssetPage page = ESAssetPage.Create(prefab);
            page.Kind = ESAssetReferKind.Prefab;
            page.StringKey = "tests.prefab.other." + suffix;
            otherLibrary.DefaultPrefabBook.pages.Add(page);
            otherLibrary.MarkFastIndexDirty();
            EditorUtility.SetDirty(otherLibrary);
            AssetDatabase.SaveAssetIfDirty(otherLibrary);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            StringAssert.Contains("同一 Prefab 身份已使用其他 AssetKey", exception.Message);
        }

        [Test]
        public void CreateOrValidate_TargetLibraryContainsDuplicateExactPages_FailsPreflight()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Duplicate Page", suffix);
            CreateDefinitionAsset(request.definitionPath);
            GameObject prefab = CreatePrefabAsset(request.prefabPath, "Duplicate Page Prefab");

            ESAssetPage first = ESAssetPage.Create(prefab);
            first.Kind = ESAssetReferKind.Prefab;
            first.StringKey = request.prefabAssetKey;
            ESAssetPage second = ESAssetPage.Create(prefab);
            second.Kind = ESAssetReferKind.Prefab;
            second.StringKey = request.prefabAssetKey;
            targetLibrary.DefaultPrefabBook.pages.Add(first);
            targetLibrary.DefaultPrefabBook.pages.Add(second);
            targetLibrary.MarkFastIndexDirty();
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssetIfDirty(targetLibrary);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            StringAssert.Contains("目标 Library 内存在重复", exception.Message);
        }

        [Test]
        public void CreateOrValidate_DuplicatePagesInsertedAfterInitialPreflight_FailBeforeRegistration()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Late Duplicate Page", suffix);
            bool injected = false;
            request.validatePrefab = (prefab, _) =>
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (injected || string.IsNullOrEmpty(prefabPath))
                    return;

                injected = true;
                ESAssetPage first = ESAssetPage.Create(prefab);
                first.Kind = ESAssetReferKind.Prefab;
                first.StringKey = request.prefabAssetKey;
                ESAssetPage second = ESAssetPage.Create(prefab);
                second.Kind = ESAssetReferKind.Prefab;
                second.StringKey = request.prefabAssetKey;
                targetLibrary.DefaultPrefabBook.pages.Add(first);
                targetLibrary.DefaultPrefabBook.pages.Add(second);
                targetLibrary.MarkFastIndexDirty();
                EditorUtility.SetDirty(targetLibrary);
                AssetDatabase.SaveAssetIfDirty(targetLibrary);
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            Assert.That(injected, Is.True);
            StringAssert.Contains("目标 Library 内存在重复", exception.Message);
        }

        [Test]
        public void CreateOrValidate_DirtyPrefab_FailsBeforeRegistration()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest request = CreateRequest("Dirty Prefab", suffix);
            CreateDefinitionAsset(request.definitionPath);
            GameObject prefab = CreatePrefabAsset(request.prefabPath, "Dirty Prefab");
            EditorUtility.SetDirty(prefab);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(TargetLibraryPath, request));

            StringAssert.Contains("未保存修改", exception.Message);
            Assert.That(
                targetLibrary.TryGetPageByStringKey(
                    ESAssetReferKind.Prefab,
                    request.prefabAssetKey,
                    out _),
                Is.False);
        }

        [Test]
        public void CreateOrValidate_BuildStageFailure_CanResumeCompletedStages()
        {
            string suffix = Guid.NewGuid().ToString("N");
            ESItemPrefabAuthoringRequest firstRequest = CreateRequest("First", suffix + ".first");
            ESItemPrefabAuthoringRequest failingSecond = CreateRequest("Second", suffix + ".second");
            failingSecond.buildNewPrefab = _ => throw new InvalidOperationException("simulated build failure");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESItemPrefabAuthoring.CreateOrValidate(
                    TargetLibraryPath,
                    firstRequest,
                    failingSecond));

            StringAssert.Contains("simulated build failure", exception.Message);
            Assert.That(AssetDatabase.LoadAssetAtPath<ItemDataInfo>(firstRequest.definitionPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(firstRequest.prefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ItemDataInfo>(failingSecond.definitionPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(failingSecond.prefabPath), Is.Null);

            ESItemPrefabAuthoringRequest retrySecond = CreateRequest("Second", suffix + ".second");
            ESItemPrefabAuthoringResult[] retry = ESItemPrefabAuthoring.CreateOrValidate(
                TargetLibraryPath,
                firstRequest,
                retrySecond);

            Assert.That(retry[0].definitionCreated, Is.False);
            Assert.That(retry[0].prefabCreated, Is.False);
            Assert.That(retry[1].definitionCreated, Is.False);
            Assert.That(retry[1].prefabCreated, Is.True);
        }

        private static ESItemPrefabAuthoringRequest CreateRequest(string label, string suffix)
        {
            string safeSuffix = suffix.Replace('.', '_');
            return new ESItemPrefabAuthoringRequest
            {
                label = label,
                definitionPath = TestRoot + "/" + safeSuffix + ".asset",
                prefabPath = TestRoot + "/" + safeSuffix + ".prefab",
                prefabAssetKey = "tests.prefab.authoring." + suffix,
                configureNewDefinition = info =>
                {
                    info.name = label + " Definition";
                    info.baseConfig = new ItemBaseConfig
                    {
                        prefabKey = new ESAssetReferPrefabConfigKey()
                    };
                },
                validateDefinitionOwnership = _ => { },
                validateDefinitionBeforePrefab = _ => { },
                validateDefinition = _ => { },
                buildNewPrefab = _ => new GameObject(label + " Prefab"),
                validatePrefab = (prefab, _) =>
                {
                    if (prefab == null)
                        throw new InvalidOperationException(label + " Prefab is null.");
                }
            };
        }

        private static ItemDataInfo CreateDefinitionAsset(string path)
        {
            ItemDataInfo definition = ScriptableObject.CreateInstance<ItemDataInfo>();
            definition.baseConfig = new ItemBaseConfig
            {
                prefabKey = new ESAssetReferPrefabConfigKey()
            };
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static GameObject CreatePrefabAsset(string path, string name)
        {
            GameObject root = new GameObject(name);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
