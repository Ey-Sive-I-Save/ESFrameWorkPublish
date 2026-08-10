using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.Tests
{
    public sealed class ESPrefabTesterTests
    {
        private const BindingFlags InstanceMethodFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private string temporaryFolder;
        private string prefabPath;
        private string prefabGuid;
        private GameObject host;
        private ESPrefabTester tester;

        [SetUp]
        public void SetUp()
        {
            temporaryFolder = "Assets/Plugins/ES/1_Design/Tests/GeneratedPrefabTester_" + Guid.NewGuid().ToString("N");
            string folderGuid = AssetDatabase.CreateFolder(
                "Assets/Plugins/ES/1_Design/Tests",
                temporaryFolder.Substring(temporaryFolder.LastIndexOf('/') + 1));
            Assert.That(folderGuid, Is.Not.Empty);

            prefabPath = temporaryFolder + "/Source.prefab";
            var source = new GameObject("ESPrefabTester_Source");
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                Assert.That(saved, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            Assert.That(prefabGuid, Is.Not.Empty);

            host = new GameObject("ESPrefabTester_Host");
            tester = host.AddComponent<ESPrefabTester>();
        }

        [TearDown]
        public void TearDown()
        {
            if (tester != null)
                Invoke(tester, "DestroyPreviewInstance", new object[] { null });
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            if (!string.IsNullOrEmpty(temporaryFolder))
                AssetDatabase.DeleteAsset(temporaryFolder);
        }

        [Test]
        public void GuidBinding_RebuildsEditableDontSavePrefabInstance()
        {
            SetGuid(prefabGuid);

            bool rebuilt = Invoke<bool>(tester, "RebuildPreview");

            Assert.That(rebuilt, Is.True, tester.LastStatus);
            Assert.That(tester.PreviewInstance, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(tester.PreviewInstance), Is.True);
            Assert.That(tester.PreviewInstance.transform.parent, Is.Not.Null);
            Assert.That(
                tester.PreviewInstance.hideFlags & HideFlags.DontSaveInEditor,
                Is.EqualTo(HideFlags.DontSaveInEditor));
            Assert.That(
                tester.PreviewInstance.hideFlags & HideFlags.DontSaveInBuild,
                Is.EqualTo(HideFlags.DontSaveInBuild));
            Assert.That(
                tester.PreviewInstance.hideFlags & HideFlags.NotEditable,
                Is.EqualTo((HideFlags)0));
        }

        [Test]
        public void DefaultCreationMode_IsAutomatic()
        {
            Assert.That(GetPrivateField<bool>(tester, "createAutomatically"), Is.True);
        }

        [Test]
        public void ManualMode_DoesNotAutoCreateButButtonPathStillCreates()
        {
            SetGuid(prefabGuid);
            SetPrivateField(tester, "createAutomatically", false);

            Invoke(tester, "RebuildAfterEnable");
            Assert.That(tester.PreviewInstance, Is.Null);

            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            Assert.That(tester.PreviewInstance, Is.Not.Null);
        }

        [Test]
        public void CustomParent_PlacesPreviewContainerUnderSelectedTransform()
        {
            SetGuid(prefabGuid);
            var customParent = new GameObject("ESPrefabTester_CustomParent");
            try
            {
                SetPrivateField(tester, "previewParent", customParent.transform);

                Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
                Assert.That(tester.PreviewInstance.transform.parent.parent, Is.EqualTo(customParent.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customParent);
            }
        }

        [Test]
        public void ParentInDifferentScene_RejectsRebuild()
        {
            SetGuid(prefabGuid);
            Scene otherScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var otherParent = new GameObject("ESPrefabTester_OtherSceneParent");
            SceneManager.MoveGameObjectToScene(otherParent, otherScene);
            try
            {
                SetPrivateField(tester, "previewParent", otherParent.transform);

                Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.False);
                Assert.That(tester.PreviewInstance, Is.Null);
                Assert.That(tester.LastStatus, Does.Contain("同一场景"));
            }
            finally
            {
                EditorSceneManager.CloseScene(otherScene, true);
            }
        }

        [Test]
        public void FreshPreview_DontSaveFlagsAreNotUserOverrides()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);

            object[] arguments = { null };
            bool canApply = Invoke<bool>(tester, "CanApply", arguments);

            Assert.That(canApply, Is.False);
            Assert.That(arguments[0] as string, Does.Contain("没有可应用"));
        }

        [Test]
        public void Apply_UserOverrideDoesNotPersistPreviewHideFlags()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            BoxCollider addedCollider = tester.PreviewInstance.AddComponent<BoxCollider>();
            addedCollider.isTrigger = true;

            bool applied = Invoke<bool>(tester, "ApplyToPrefabInternal", false);

            Assert.That(applied, Is.True, tester.LastStatus);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.GetComponent<BoxCollider>(), Is.Not.Null);
            AssertNoDontSaveFlags(source.transform);
        }

        [Test]
        public void SourceChangedAfterRebuild_RejectsStaleApply()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.AddComponent<SphereCollider>();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            object[] arguments = { null };
            bool canApply = Invoke<bool>(tester, "CanApply", arguments);

            Assert.That(canApply, Is.False);
            Assert.That(arguments[0] as string, Does.Contain("已经变化"));
        }

        [Test]
        public void NestedTesterInsidePreview_RejectsRecursiveRebuild()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ESPrefabTester nested = contents.AddComponent<ESPrefabTester>();
                var serializedNested = new SerializedObject(nested);
                serializedNested.FindProperty("prefabGuid").stringValue = prefabGuid;
                serializedNested.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            ESPrefabTester nestedTester = tester.PreviewInstance.GetComponent<ESPrefabTester>();
            Assert.That(nestedTester, Is.Not.Null);

            bool rebuilt = Invoke<bool>(nestedTester, "RebuildPreview");

            Assert.That(rebuilt, Is.False);
            Assert.That(nestedTester.LastStatus, Does.Contain("递归预览"));
        }

        [Test]
        public void RebuildAndDestroy_PreserveCleanSceneState()
        {
            SetGuid(prefabGuid);
            Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.MoveGameObjectToScene(host, testScene);
                string scenePath = temporaryFolder + "/PrefabTesterScene.unity";
                Assert.That(EditorSceneManager.SaveScene(testScene, scenePath), Is.True);
                Assert.That(testScene.isDirty, Is.False);

                Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
                Assert.That(testScene.isDirty, Is.False);

                Invoke(tester, "DestroyPreviewInstance", new object[] { null });
                Assert.That(testScene.isDirty, Is.False);
            }
            finally
            {
                tester = null;
                host = null;
                EditorSceneManager.CloseScene(testScene, true);
            }
        }

        [Test]
        public void InvalidGuid_RejectsRebuild()
        {
            SetGuid(new string('0', 32));

            bool rebuilt = Invoke<bool>(tester, "RebuildPreview");

            Assert.That(rebuilt, Is.False);
            Assert.That(tester.PreviewInstance, Is.Null);
            Assert.That(tester.LastStatus, Does.Contain("GUID"));
        }

        [Test]
        public void ModelPrefab_RejectsRebuild()
        {
            GameObject modelPrefab = FindModelPrefab();
            if (modelPrefab == null)
                Assert.Ignore("项目中没有可用于 Model Prefab 拒绝测试的模型资产。");

            string modelPath = AssetDatabase.GetAssetPath(modelPrefab);
            SetGuid(AssetDatabase.AssetPathToGUID(modelPath));

            bool rebuilt = Invoke<bool>(tester, "RebuildPreview");

            Assert.That(rebuilt, Is.False);
            Assert.That(tester.PreviewInstance, Is.Null);
            Assert.That(tester.LastStatus, Does.Contain("模型 Prefab"));
        }

        [Test]
        public void SourceGuidChangedAfterRebuild_RejectsApply()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            SetGuid(new string('f', 32));

            object[] arguments = { null };
            bool canApply = Invoke<bool>(tester, "CanApply", arguments);

            Assert.That(canApply, Is.False);
            Assert.That(arguments[0] as string, Does.Contain("GUID"));
        }

        [Test]
        public void OnDisable_CleansPreviewInstance()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            GameObject instance = tester.PreviewInstance;

            tester.enabled = false;

            Assert.That(instance, Is.Null);
            Assert.That(tester.PreviewInstance, Is.Null);
        }

        [Test]
        public void BeforeAssemblyReloadCallback_CleansPreviewInstance()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            GameObject instance = tester.PreviewInstance;

            Invoke(tester, "CleanupBeforeAssemblyReload");

            Assert.That(instance, Is.Null);
            Assert.That(tester.PreviewInstance, Is.Null);
        }

        [Test]
        public void SceneClosingCallback_CleansOnlyMatchingScenePreview()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);
            GameObject instance = tester.PreviewInstance;
            Scene hostScene = host.scene;

            Invoke(tester, "CleanupOnSceneClosing", hostScene, true);

            Assert.That(instance, Is.Null);
            Assert.That(tester.PreviewInstance, Is.Null);
        }

        [Test]
        public void ReparentedPreviewInstance_IsDestroyedOnCleanup()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);

            GameObject instance = tester.PreviewInstance;
            instance.transform.SetParent(null);

            Invoke(tester, "DestroyPreviewInstance", "已清理");

            Assert.That(instance, Is.Null);
            Assert.That(tester.PreviewInstance, Is.Null);
        }

        [Test]
        public void RenamedContainer_IsFoundAndDestroyedByMarker()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);

            GameObject container = tester.PreviewInstance.transform.parent.gameObject;
            container.name = "Renamed Tester Container";
            SetPrivateField(tester, "previewContainer", null);

            Invoke(tester, "DestroyPreviewInstance", "已清理");

            Assert.That(container, Is.Null);
            Assert.That(tester.PreviewInstance, Is.Null);
        }

        [Test]
        public void TransformOverride_IsDetected()
        {
            SetGuid(prefabGuid);
            Assert.That(Invoke<bool>(tester, "RebuildPreview"), Is.True, tester.LastStatus);

            tester.PreviewInstance.transform.localPosition = new Vector3(1f, 2f, 3f);

            bool hasTransformOverride = InvokeStatic<bool>(
                typeof(ESPrefabTester),
                "HasTransformOverride",
                tester.PreviewInstance);

            Assert.That(hasTransformOverride, Is.True);
        }

        private void SetGuid(string guid)
        {
            var serializedTester = new SerializedObject(tester);
            SerializedProperty guidProperty = serializedTester.FindProperty("prefabGuid");
            Assert.That(guidProperty, Is.Not.Null);
            guidProperty.stringValue = guid;
            serializedTester.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindModelPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate != null && PrefabUtility.GetPrefabAssetType(candidate) == PrefabAssetType.Model)
                    return candidate;
            }

            return null;
        }

        private static void AssertNoDontSaveFlags(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            HideFlags forbidden = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Assert.That(root.gameObject.hideFlags & forbidden, Is.EqualTo((HideFlags)0), root.name);
            for (int i = 0; i < root.childCount; i++)
                AssertNoDontSaveFlags(root.GetChild(i));
        }

        private static void Invoke(ESPrefabTester target, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(ESPrefabTester).GetMethod(methodName, InstanceMethodFlags);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static TResult Invoke<TResult>(ESPrefabTester target, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(ESPrefabTester).GetMethod(methodName, InstanceMethodFlags);
            Assert.That(method, Is.Not.Null, methodName);
            return (TResult)method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = typeof(ESPrefabTester).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static TResult GetPrivateField<TResult>(object target, string fieldName)
        {
            FieldInfo field = typeof(ESPrefabTester).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (TResult)field.GetValue(target);
        }

        private static TResult InvokeStatic<TResult>(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (TResult)method.Invoke(null, arguments);
        }
    }
}
