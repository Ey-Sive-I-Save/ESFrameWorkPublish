using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCharacterCameraMappingTests
    {
        private const int LockToTargetOnAssign = 0;
        private const int LockToTargetWithWorldUp = 1;
        private const string PlayerThirdPersonRigPath =
            "Assets/ESNormalAssets/Camera/Rigs/PlayerThirdPersonRig.prefab";
        private const string VehicleChaseRigPath =
            "Assets/ESNormalAssets/Camera/Rigs/VehicleChaseRig.prefab";

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void MissingCameraTarget_FailsFormalCameraMapping()
        {
            GameObject root = CreateObject("Character Without Camera Target");
            root.AddComponent<EntityTransformMapping>();

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterCameraMapping(
                    root.GetComponent<Entity>(),
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("CameraTarget"));
        }

        [Test]
        public void DynamicCameraTarget_PassesFormalCameraMapping()
        {
            GameObject root = CreateObject("Character With Camera Target");
            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            GameObject cameraTarget = CreateObject("CameraTarget");
            cameraTarget.transform.SetParent(root.transform, false);
            mapping.Set("CameraTarget", cameraTarget.transform);

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterCameraMapping(
                    root.GetComponent<Entity>(),
                    out string error),
                Is.True,
                error);
        }

        [Test]
        public void DefaultCameraKey_PassesFormalCameraMapping()
        {
            GameObject root = CreateObject("Character With Default Camera");
            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            GameObject cameraTarget = CreateObject("Default Camera Target");
            cameraTarget.transform.SetParent(root.transform, false);
            mapping.Set(DefaultTransformKey.Camera, cameraTarget.transform);

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterCameraMapping(
                    root.GetComponent<Entity>(),
                    out string error),
                Is.True,
                error);
        }

        [TestCase(PlayerThirdPersonRigPath, LockToTargetOnAssign)]
        [TestCase(VehicleChaseRigPath, LockToTargetWithWorldUp)]
        public void DefaultFreeLookRig_UsesExpectedTargetOrientationBinding(
            string prefabPath,
            int expectedBindingMode)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing camera rig prefab: " + prefabPath);

            int bindingCount = 0;
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    continue;

                SerializedProperty bindingMode =
                    new SerializedObject(components[i]).FindProperty("m_BindingMode");
                if (bindingMode == null)
                    continue;

                Assert.That(bindingMode.intValue, Is.EqualTo(expectedBindingMode));
                bindingCount++;
            }

            Assert.That(bindingCount, Is.EqualTo(4), "Expected FreeLook plus three orbital bindings.");
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }
    }
}
