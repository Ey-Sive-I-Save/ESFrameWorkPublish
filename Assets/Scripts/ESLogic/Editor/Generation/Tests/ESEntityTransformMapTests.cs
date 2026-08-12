using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESEntityTransformMapTests
    {
        private const string TempFolder = "Assets/TempTests";
        private const string TempPrefabPath = TempFolder + "/EntityTransformMapRoundTrip.prefab";
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
            AssetDatabase.DeleteAsset(TempPrefabPath);
            if (AssetDatabase.IsValidFolder(TempFolder)
                && AssetDatabase.FindAssets(string.Empty, new[] { TempFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void DualAlias_ResolvesOneTransformThroughBothKeys()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform target = CreateTransform(EntityEquipmentSocketKeys.WeaponSocket);

            Assert.That(
                map.TrySet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, target, out var conflict),
                Is.True,
                conflict.ToString());

            Assert.That(map.Resolve(DefaultTransformKey.Weapon), Is.SameAs(target));
            Assert.That(map.Resolve(EntityEquipmentSocketKeys.WeaponSocket), Is.SameAs(target));
            Assert.That(map.TryGet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, out Transform paired), Is.True);
            Assert.That(paired, Is.SameAs(target));
        }

        [Test]
        public void AliasConflict_DoesNotMutateEitherExistingEntry()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform weapon = CreateTransform("Weapon");
            Transform camera = CreateTransform("Camera");
            Assert.That(map.TrySet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, weapon, out _), Is.True);
            Assert.That(map.TrySet(DefaultTransformKey.Camera, "CameraTarget", camera, out _), Is.True);
            int generation = map.Generation;

            Assert.That(
                map.TrySet(DefaultTransformKey.Weapon, "CameraTarget", camera, out var conflict),
                Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.AliasMismatch));
            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.Resolve(DefaultTransformKey.Weapon), Is.SameAs(weapon));
            Assert.That(map.Resolve("CameraTarget"), Is.SameAs(camera));
        }

        [Test]
        public void RemovingOneAlias_PreservesTheOtherAliasAndEntry()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform target = CreateTransform(EntityEquipmentSocketKeys.WeaponSocket);
            Assert.That(map.TrySet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, target, out _), Is.True);

            Assert.That(map.Remove(EntityEquipmentSocketKeys.WeaponSocket), Is.True);

            Assert.That(map.Resolve(EntityEquipmentSocketKeys.WeaponSocket), Is.Null);
            Assert.That(map.Resolve(DefaultTransformKey.Weapon), Is.SameAs(target));
            Assert.That(map.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearDynamicOnlyEntries_PreservesStableDualAliases()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform weapon = CreateTransform(EntityEquipmentSocketKeys.WeaponSocket);
            Transform runtimeRoot = CreateTransform("RuntimeAttachmentsRoot");
            Assert.That(map.TrySet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, weapon, out _), Is.True);
            Assert.That(map.TrySetDynamic("RuntimeAttachmentsRoot", runtimeRoot, out _), Is.True);

            map.ClearDynamicOnlyEntries();

            Assert.That(map.Resolve(DefaultTransformKey.Weapon), Is.SameAs(weapon));
            Assert.That(map.Resolve(EntityEquipmentSocketKeys.WeaponSocket), Is.SameAs(weapon));
            Assert.That(map.Resolve("RuntimeAttachmentsRoot"), Is.Null);
            Assert.That(map.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeserializeCallback_RebuildsRuntimeMirrorsFromUnityEntries()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform camera = CreateTransform("CameraTarget");
            Assert.That(map.TrySet(DefaultTransformKey.Camera, "CameraTarget", camera, out _), Is.True);
            int generation = map.Generation;

            ((ISerializationCallbackReceiver)map).OnAfterDeserialize();

            Assert.That(map.Resolve(DefaultTransformKey.Camera), Is.SameAs(camera));
            Assert.That(map.Resolve("CameraTarget"), Is.SameAs(camera));
            Assert.That(map.Generation, Is.GreaterThan(generation));
        }

        [Test]
        public void Component_UsesConcreteMapWithoutOdinSerialization()
        {
            GameObject root = CreateObject("EntityRoot");
            root.AddComponent<Entity>();
            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            Transform target = CreateTransform("CameraTarget");
            target.SetParent(root.transform, false);

            Assert.That(
                mapping.Set(DefaultTransformKey.Camera, "CameraTarget", target, out var conflict),
                Is.True,
                conflict.ToString());

            Assert.That(mapping.TransformMappings, Is.TypeOf<EntityTransformMap>());
            Assert.That(mapping.Resolve(DefaultTransformKey.Camera), Is.SameAs(target));
            Assert.That(mapping.Resolve("CameraTarget"), Is.SameAs(target));
        }

        [Test]
        public void ComponentField_IsConcreteAndUsesUnitySerializeField()
        {
            Assert.That(typeof(EntityTransformMap).IsSealed, Is.True);
            Assert.That(
                typeof(EntityTransformMap).BaseType,
                Is.EqualTo(typeof(ESEnumStringMirrorMap<DefaultTransformKey, Transform>)));

            FieldInfo field = typeof(EntityTransformMapping).GetField(
                "transformMappings",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(EntityTransformMap)));
            Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True);

            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                Assert.That(
                    attributes[i].GetType().FullName,
                    Is.Not.EqualTo("Sirenix.Serialization.OdinSerializeAttribute"));
            }
        }

        [Test]
        public void UnityPrefabRoundTrip_PreservesConcreteMapEntriesAndAliases()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "TempTests");

            GameObject root = CreateObject("EntityTransformMapRoundTrip");
            root.AddComponent<Entity>();
            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            Transform weapon = new GameObject(EntityEquipmentSocketKeys.WeaponSocket).transform;
            weapon.SetParent(root.transform, false);
            Assert.That(
                mapping.Set(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, weapon, out var conflict),
                Is.True,
                conflict.ToString());

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TempPrefabPath);
            Assert.That(saved, Is.Not.Null);
            AssetDatabase.ImportAsset(TempPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject loaded = PrefabUtility.LoadPrefabContents(TempPrefabPath);
            try
            {
                EntityTransformMapping reloaded = loaded.GetComponent<EntityTransformMapping>();
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.TransformMappings, Is.TypeOf<EntityTransformMap>());
                Assert.That(reloaded.TransformMappings.IsValid, Is.True);
                Assert.That(reloaded.TransformMappings.Count, Is.EqualTo(1));
                Assert.That(reloaded.Resolve(DefaultTransformKey.Weapon), Is.Not.Null);
                Assert.That(
                    reloaded.Resolve(DefaultTransformKey.Weapon),
                    Is.SameAs(reloaded.Resolve(EntityEquipmentSocketKeys.WeaponSocket)));
                Assert.That(reloaded.Resolve(DefaultTransformKey.Weapon).name, Is.EqualTo(EntityEquipmentSocketKeys.WeaponSocket));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
            }
        }

        [Test]
        public void LegacyMigration_KnownAliasesAreMergedWithoutDroppingOtherKeys()
        {
            Transform weapon = CreateTransform(EntityEquipmentSocketKeys.WeaponSocket);
            Transform camera = CreateTransform("CameraTarget");
            Transform head = CreateTransform("Head");
            Transform aim = CreateTransform("CameraAimTarget");
            var defaults = new Dictionary<DefaultTransformKey, Transform>
            {
                { DefaultTransformKey.Weapon, weapon },
                { DefaultTransformKey.Camera, camera },
                { DefaultTransformKey.Head, head },
            };
            var dynamics = new Dictionary<string, Transform>(System.StringComparer.Ordinal)
            {
                { EntityEquipmentSocketKeys.WeaponSocket, weapon },
                { "CameraTarget", camera },
                { "CameraAimTarget", aim },
            };

            Assert.That(
                ESEntityTransformMappingMigration.TryBuildMigratedEntries(defaults, dynamics, out var entries, out string error),
                Is.True,
                error);

            EntityTransformMap map = new EntityTransformMap();
            Assert.That(map.TryReplaceEntries(entries, out var conflict), Is.True, conflict.ToString());
            Assert.That(map.Count, Is.EqualTo(4));
            Assert.That(map.TryGet(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, out _), Is.True);
            Assert.That(map.TryGet(DefaultTransformKey.Camera, "CameraTarget", out _), Is.True);
            Assert.That(map.Resolve(DefaultTransformKey.Head), Is.SameAs(head));
            Assert.That(map.Resolve("CameraAimTarget"), Is.SameAs(aim));
        }

        [Test]
        public void LegacyMigration_ConflictingKnownAliasIsBlocked()
        {
            Transform enumWeapon = CreateTransform("EnumWeapon");
            Transform stringWeapon = CreateTransform("StringWeapon");
            var defaults = new Dictionary<DefaultTransformKey, Transform>
            {
                { DefaultTransformKey.Weapon, enumWeapon },
            };
            var dynamics = new Dictionary<string, Transform>(System.StringComparer.Ordinal)
            {
                { EntityEquipmentSocketKeys.WeaponSocket, stringWeapon },
            };

            Assert.That(
                ESEntityTransformMappingMigration.TryBuildMigratedEntries(defaults, dynamics, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("指向不同 Transform"));
        }

        private Transform CreateTransform(string name)
        {
            return CreateObject(name).transform;
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }
    }
}
