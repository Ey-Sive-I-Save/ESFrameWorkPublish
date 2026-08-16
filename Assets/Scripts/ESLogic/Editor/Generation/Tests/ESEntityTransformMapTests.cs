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
            Transform target = CreateTransform(EntityEquipmentSocketKeys.MainHandSocket);

            Assert.That(
                map.TrySet(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, target, out var conflict),
                Is.True,
                conflict.ToString());

            Assert.That(map.Resolve(DefaultTransformKey.CustomA), Is.SameAs(target));
            Assert.That(map.Resolve(EntityEquipmentSocketKeys.MainHandSocket), Is.SameAs(target));
            Assert.That(map.TryGet(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, out Transform paired), Is.True);
            Assert.That(paired, Is.SameAs(target));
        }

        [Test]
        public void AliasConflict_DoesNotMutateEitherExistingEntry()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform weapon = CreateTransform("Weapon");
            Transform camera = CreateTransform("Camera");
            Assert.That(map.TrySet(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, weapon, out _), Is.True);
            Assert.That(map.TrySet(DefaultTransformKey.Camera, "CameraTarget", camera, out _), Is.True);
            int generation = map.Generation;

            Assert.That(
                map.TrySet(DefaultTransformKey.CustomA, "CameraTarget", camera, out var conflict),
                Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.AliasMismatch));
            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.Resolve(DefaultTransformKey.CustomA), Is.SameAs(weapon));
            Assert.That(map.Resolve("CameraTarget"), Is.SameAs(camera));
        }

        [Test]
        public void RemovingOneAlias_PreservesTheOtherAliasAndEntry()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform target = CreateTransform(EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(map.TrySet(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, target, out _), Is.True);

            Assert.That(map.Remove(EntityEquipmentSocketKeys.MainHandSocket), Is.True);

            Assert.That(map.Resolve(EntityEquipmentSocketKeys.MainHandSocket), Is.Null);
            Assert.That(map.Resolve(DefaultTransformKey.CustomA), Is.SameAs(target));
            Assert.That(map.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearDynamicOnlyEntries_PreservesStableDualAliases()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform weapon = CreateTransform(EntityEquipmentSocketKeys.MainHandSocket);
            Transform runtimeRoot = CreateTransform("RuntimeAttachmentsRoot");
            Assert.That(map.TrySet(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, weapon, out _), Is.True);
            Assert.That(map.TrySetDynamic("RuntimeAttachmentsRoot", runtimeRoot, out _), Is.True);

            map.ClearDynamicOnlyEntries();

            Assert.That(map.Resolve(DefaultTransformKey.CustomA), Is.SameAs(weapon));
            Assert.That(map.Resolve(EntityEquipmentSocketKeys.MainHandSocket), Is.SameAs(weapon));
            Assert.That(map.Resolve("RuntimeAttachmentsRoot"), Is.Null);
            Assert.That(map.Count, Is.EqualTo(1));
        }

        [Test]
        public void DynamicWrites_RejectMissingOrUnnormalizedKeysWithoutMutation()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform value = CreateTransform("DynamicSocket");
            int generation = map.Generation;

            Assert.That(map.TrySetDynamic(null, value, out var nullConflict), Is.False);
            Assert.That(nullConflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.MissingKey));
            Assert.That(map.TrySetDynamic(string.Empty, value, out var emptyConflict), Is.False);
            Assert.That(emptyConflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.MissingKey));
            Assert.That(map.TrySetDynamic(" invalid ", value, out var whitespaceConflict), Is.False);
            Assert.That(whitespaceConflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.InvalidStringKey));

            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.Count, Is.Zero);
            Assert.That(map.TryGet(string.Empty, out _), Is.False);
        }

        [Test]
        public void InheritedSerializedWrite_CannotCollideWithDynamicAlias()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform dynamicValue = CreateTransform("DynamicSocket");
            Transform serializedValue = CreateTransform("SerializedSocket");
            Assert.That(map.TrySetDynamic("RuntimeSocket", dynamicValue, out _), Is.True);

            var baseMap = (ESEnumStringMirrorMap<DefaultTransformKey, Transform>)map;
            Assert.That(baseMap.TryAdd("RuntimeSocket", serializedValue, out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.DuplicateStringKey));
            Assert.That(map.Resolve("RuntimeSocket"), Is.SameAs(dynamicValue));
            Assert.That(map.Count, Is.Zero);
        }

        [Test]
        public void BatchSerializedWrite_CannotCollideWithDynamicAlias()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform dynamicValue = CreateTransform("DynamicSocket");
            Transform serializedValue = CreateTransform("SerializedSocket");
            Assert.That(map.TrySetDynamic("RuntimeSocket", dynamicValue, out _), Is.True);

            var entries = new List<EntityTransformMap.Entry>
            {
                new EntityTransformMap.Entry("RuntimeSocket", serializedValue)
            };
            Assert.That(map.TryReplaceEntries(entries, out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.DuplicateStringKey));
            Assert.That(map.Resolve("RuntimeSocket"), Is.SameAs(dynamicValue));
            Assert.That(map.Count, Is.Zero);
        }

        [Test]
        public void SerializedSet_AtomicallyPromotesDynamicAlias()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform dynamicValue = CreateTransform("DynamicSocket");
            Transform serializedValue = CreateTransform("SerializedSocket");
            Assert.That(map.TrySetDynamic("RuntimeSocket", dynamicValue, out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySet("RuntimeSocket", serializedValue, out var conflict), Is.True, conflict.ToString());

            Assert.That(map.Resolve("RuntimeSocket"), Is.SameAs(serializedValue));
            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(map.Generation, Is.GreaterThan(generation));
            ((ISerializationCallbackReceiver)map).OnAfterDeserialize();
            Assert.That(map.Resolve("RuntimeSocket"), Is.SameAs(serializedValue));
        }

        [Test]
        public void FailedSerializedPromotion_RestoresDynamicAlias()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform dynamicValue = CreateTransform("DynamicSocket");
            Assert.That(map.TrySetDynamic("RuntimeSocket", dynamicValue, out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySet("RuntimeSocket", null, out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.NullValue));
            Assert.That(map.Resolve("RuntimeSocket"), Is.SameAs(dynamicValue));
            Assert.That(map.Count, Is.Zero);
            Assert.That(map.Generation, Is.EqualTo(generation));
        }

        [Test]
        public void BaseClear_AlsoClearsDynamicState()
        {
            EntityTransformMap map = new EntityTransformMap();
            Transform dynamicValue = CreateTransform("DynamicSocket");
            Assert.That(map.TrySetDynamic("RuntimeSocket", dynamicValue, out _), Is.True);

            ((ESEnumStringMirrorMap<DefaultTransformKey, Transform>)map).Clear();

            Assert.That(map.Resolve("RuntimeSocket"), Is.Null);
            Assert.That(map.Count, Is.Zero);
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

            Assert.That(mapping.TransformMappings.IsCreated, Is.True);
            Assert.That((object)mapping.TransformMappings, Is.TypeOf<EntityTransformMapView>());
            Assert.That(mapping.Resolve(DefaultTransformKey.Camera), Is.SameAs(target));
            Assert.That(mapping.Resolve("CameraTarget"), Is.SameAs(target));
        }

        [Test]
        public void ComponentField_IsConcreteAndUsesUnitySerializeField()
        {
            Assert.That(typeof(EntityTransformMap).IsSealed, Is.True);
            Assert.That(typeof(EntityTransformMap).IsNotPublic, Is.True);
            Assert.That(
                typeof(EntityTransformMap).BaseType,
                Is.EqualTo(typeof(ESEnumStringMirrorMap<DefaultTransformKey, Transform>)));

            FieldInfo field = typeof(EntityTransformMapping).GetField(
                "transformMappings",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(EntityTransformMap)));
            Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(
                typeof(EntityTransformMapping).GetProperty(nameof(EntityTransformMapping.TransformMappings))?.PropertyType,
                Is.EqualTo(typeof(EntityTransformMapView)));
            Assert.That(typeof(EntityTransformMapView).IsValueType, Is.True);
            Assert.That(typeof(EntityTransformMapView).GetMethod("TrySet"), Is.Null);
            Assert.That(typeof(EntityTransformMapView).GetMethod("TryAdd"), Is.Null);
            Assert.That(typeof(EntityTransformMapView).GetMethod("Clear"), Is.Null);
            Assert.That(
                typeof(EntityTransformMapView).GetProperty(
                    "LastConflict",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(EntityTransformMapView).GetMethod(
                    "TryGetEntryAt",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(EntityTransformMapView).GetMethod(
                    "CopyEntries",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(EntityTransformMapping).GetMethod(
                    "Set",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(Transform) },
                    null),
                Is.Null);
            Assert.That(
                typeof(EntityTransformMapping).GetMethod(
                    "SetDynamic",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(Transform),
                        typeof(EntityTransformMap.Conflict).MakeByRefType()
                    },
                    null),
                Is.Null);

            ESEnumStringTableAttribute table = field.GetCustomAttribute<ESEnumStringTableAttribute>();
            Assert.That(table, Is.Not.Null);
            Assert.That(table.EnumColumn, Is.EqualTo("固定挂点"));
            Assert.That(table.StringColumn, Is.EqualTo("稳定 String Key"));
            Assert.That(table.ValueColumn, Is.EqualTo("Transform"));
            Assert.That(table.NewEntryMode, Is.EqualTo(ESEnumStringTableNewEntryMode.EnumAndString));

            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                Assert.That(
                    attributes[i].GetType().FullName,
                    Is.Not.EqualTo("Sirenix.Serialization.OdinSerializeAttribute"));
            }
        }

        [Test]
        public void TableDrawerContract_UsesTheExistingUnityEntryAuthority()
        {
            GameObject root = CreateObject("EntityTransformTableContract");
            root.AddComponent<Entity>();
            root.AddComponent<EntityTransformMapping>();

            var serializedObject = new SerializedObject(root.GetComponent<EntityTransformMapping>());
            SerializedProperty table = serializedObject.FindProperty("transformMappings");
            SerializedProperty entries = table?.FindPropertyRelative("entries");

            Assert.That(table, Is.Not.Null);
            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.isArray, Is.True);

            entries.InsertArrayElementAtIndex(0);
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            Assert.That(entry.FindPropertyRelative("hasEnumKey"), Is.Not.Null);
            Assert.That(entry.FindPropertyRelative("enumKey"), Is.Not.Null);
            Assert.That(entry.FindPropertyRelative("stringKey"), Is.Not.Null);
            Assert.That(entry.FindPropertyRelative("value"), Is.Not.Null);
        }

        [Test]
        public void UnityPrefabRoundTrip_PreservesConcreteMapEntriesAndAliases()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "TempTests");

            GameObject root = CreateObject("EntityTransformMapRoundTrip");
            root.AddComponent<Entity>();
            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            Transform weapon = new GameObject(EntityEquipmentSocketKeys.MainHandSocket).transform;
            weapon.SetParent(root.transform, false);
            Assert.That(
                mapping.Set(DefaultTransformKey.CustomA, EntityEquipmentSocketKeys.MainHandSocket, weapon, out var conflict),
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
                Assert.That((object)reloaded.TransformMappings, Is.TypeOf<EntityTransformMapView>());
                Assert.That(reloaded.TransformMappings.IsValid, Is.True);
                Assert.That(reloaded.TransformMappings.Count, Is.EqualTo(1));
                Assert.That(reloaded.Resolve(DefaultTransformKey.CustomA), Is.Not.Null);
                Assert.That(
                    reloaded.Resolve(DefaultTransformKey.CustomA),
                    Is.SameAs(reloaded.Resolve(EntityEquipmentSocketKeys.MainHandSocket)));
                Assert.That(reloaded.Resolve(DefaultTransformKey.CustomA).name, Is.EqualTo(EntityEquipmentSocketKeys.MainHandSocket));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loaded);
            }
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
