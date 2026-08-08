using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCameraDefinitionCatalogTests
    {
        private ESCameraViewDefinitionCatalog catalog;
        private ESCameraRigCatalog rigCatalog;
        private GameObject rigPrefab;
        private ESCameraViewDefinition first;
        private ESCameraViewDefinition second;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<ESCameraViewDefinitionCatalog>();
            rigCatalog = ScriptableObject.CreateInstance<ESCameraRigCatalog>();
            rigPrefab = new GameObject("Camera Rig Prefab");
            first = CreateDefinition("First", ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person", "player.rig");
            second = CreateDefinition("Second", ESCameraDefinitionEnumKey.VehicleChase, "vehicle.chase", "vehicle.rig");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(rigPrefab);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(rigCatalog);
        }

        [Test]
        public void DuplicateDefinition_IsHardFailure()
        {
            ESCameraViewDefinition duplicate = CreateDefinition("Duplicate", ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person", "other.rig");
            try
            {
                catalog.SetDefinitionsForAuthoring(new[] { first, duplicate });

                Assert.That(catalog.IsValid, Is.False);
                Assert.That(catalog.BuildError, Does.Contain("构建失败"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void AliasConflict_IsHardFailure()
        {
            ESCameraViewDefinition conflict = CreateDefinition("Alias Conflict", ESCameraDefinitionEnumKey.PlayerThirdPerson, "different.player", "other.rig");
            try
            {
                catalog.SetDefinitionsForAuthoring(new[] { first, conflict });

                Assert.That(catalog.IsValid, Is.False);
                Assert.That(catalog.BuildError, Does.Contain("构建失败"));
            }
            finally
            {
                Object.DestroyImmediate(conflict);
            }
        }

        [Test]
        public void Rebuild_RejectsPreviousRuntimeHandle()
        {
            catalog.SetDefinitionsForAuthoring(new[] { first, second });
            Assert.That(catalog.TryResolve(first.Definition, out ESCameraDefinitionRuntimeHandle oldHandle), Is.True);

            catalog.SetDefinitionsForAuthoring(new[] { second, first });

            Assert.That(catalog.TryGet(oldHandle, out _), Is.False);
            Assert.That(catalog.TryResolve(first.Definition, out ESCameraDefinitionRuntimeHandle currentHandle), Is.True);
            Assert.That(currentHandle.runtimeKey, Is.EqualTo(oldHandle.runtimeKey));
            Assert.That(currentHandle.catalogGeneration, Is.Not.EqualTo(oldHandle.catalogGeneration));
        }

        [Test]
        public void DuplicateRig_IsHardFailure()
        {
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(rigCatalog.IsValid, Is.False);
            Assert.That(rigCatalog.BuildError, Does.Contain("重复 RigKey"));
        }

        [Test]
        public void MissingDefinitionRig_IsHardFailure()
        {
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "other.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, out string error), Is.False);
            Assert.That(error, Does.Contain("不存在的 RigKey"));
        }

        [Test]
        public void DefaultPlayerAndVehiclePrefabs_UseConfiguredDefinitionReferences()
        {
            EntityCharacterIdentity player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESNormalAssets/CharacterVariants/大黑塔.prefab")
                .GetComponent<EntityCharacterIdentity>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.defaultCameraDefinition.IsConfigured, Is.True);

            string[] vehiclePaths =
            {
                "Assets/ESNormalAssets/VehiclePrototypes/BlockCar.prefab",
                "Assets/ESNormalAssets/VehiclePrototypes/BlockBicycle.prefab",
                "Assets/ESNormalAssets/VehiclePrototypes/BlockHelicopter.prefab",
            };
            for (int i = 0; i < vehiclePaths.Length; i++)
            {
                VehicleController vehicle = AssetDatabase.LoadAssetAtPath<GameObject>(vehiclePaths[i]).GetComponent<VehicleController>();
                Assert.That(vehicle, Is.Not.Null, vehiclePaths[i]);
                Assert.That(vehicle.driverCameraDefinition.IsConfigured, Is.True, vehiclePaths[i]);
            }
        }

        private static ESCameraViewDefinition CreateDefinition(
            string name,
            ESCameraDefinitionEnumKey enumKey,
            string stringKey,
            string rigKey)
        {
            ESCameraViewDefinition definition = ScriptableObject.CreateInstance<ESCameraViewDefinition>();
            definition.name = name;
            definition.SetDefinitionForAuthoring(new ESCameraDefinitionReference(enumKey, stringKey));
            definition.rigKey = rigKey;
            return definition;
        }
    }
}
