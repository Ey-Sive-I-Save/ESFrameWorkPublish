using System;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESGameManagerStaticModuleTests
    {
        private GameObject managerObject;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ESGameManager.Instance, Is.Null,
                "This fixture requires an isolated GameManager static cache.");
            CreateManager();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManager();
        }

        [Test]
        public void ModuleLookup_IsReadOnly_AndExplicitCreationIsIdempotent()
        {
            Assert.That(ESGameManager.TryGetModule<ProbeSystemModule>(out ProbeSystemModule missing), Is.False);
            Assert.That(missing, Is.Null);

            ProbeSystemModule first = ESGameManager.GetOrCreateModule<ProbeSystemModule>();
            Assert.That(first, Is.Not.Null);
            Assert.That(ESGameManager.TryGetModule(out ProbeSystemModule found), Is.True);
            Assert.That(found, Is.SameAs(first));

            ProbeSystemModule second = ESGameManager.GetOrCreateModule<ProbeSystemModule>();
            Assert.That(second, Is.SameAs(first), "Explicit creation must register once and return the existing module afterwards.");
        }

        [Test]
        public void SaveFacade_OnlyWritesAndLoadsCreateTheSaveModule()
        {
            Assert.That(ESGameSave.Module, Is.Null);
            Assert.That(ESGameSave.Get<int>("module-api-missing", out _), Is.False);
            Assert.That(ESGameSave.Has(), Is.False);
            Assert.That(ESGameSave.Save(), Is.False);
            Assert.That(ESGameSave.Module, Is.Null, "Read-only Save facade calls must not create a module.");

            ESGameSave.Set("module-api-value", 42);
            Assert.That(ESGameSave.Module, Is.Not.Null);
            Assert.That(ESGameSave.Get<int>("module-api-value", out int value), Is.True);
            Assert.That(value, Is.EqualTo(42));

            DestroyManager();
            CreateManager();
            Assert.That(ESGameSave.Module, Is.Null);
            string emptySlotId = "__es_module_api_" + Guid.NewGuid().ToString("N");
            Assert.That(ESGameSave.Load(emptySlotId), Is.False);
            Assert.That(ESGameSave.Module, Is.Not.Null, "Load is an explicit initialization workflow.");
        }

        private void CreateManager()
        {
            managerObject = new GameObject("ESGameManagerStaticModuleTests");
            managerObject.AddComponent<ESGameManager>();
            Assert.That(ESGameManager.Instance, Is.Not.Null);
        }

        private void DestroyManager()
        {
            if (managerObject == null)
                return;

            UnityEngine.Object.DestroyImmediate(managerObject);
            managerObject = null;
            Assert.That(ESGameManager.Instance, Is.Null);
        }

        [Serializable]
        private sealed class ProbeSystemModule : ESSystemModule
        {
            public ProbeSystemModule()
            {
            }
        }
    }
}
