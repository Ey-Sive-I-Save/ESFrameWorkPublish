using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCharacterCameraMappingTests
    {
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

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }
    }
}
