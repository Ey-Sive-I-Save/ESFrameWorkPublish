using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESEquipmentDomainTransactionTests
    {
        private const string DefaultWeaponKey = "tests.equipment.default-weapon";
        private readonly List<GameObject> created = new List<GameObject>();
        private int defaultWeaponRuntimeKey;

        private static ESAssetReferPrefabConfigKey CreateWeaponPrefabKey(string key)
        {
            return new ESAssetReferPrefabConfigKey
            {
                stringKey = key,
                guid = "guid-" + key,
                assetTypeName = typeof(GameObject).FullName
            };
        }

        [SetUp]
        public void SetUp()
        {
            ESRuntimeDataModule.ItemInstanceTable.Clear();
            ResetWeaponDefinitions();
            defaultWeaponRuntimeKey = InjectWeaponDefinition(DefaultWeaponKey);
        }

        [TearDown]
        public void TearDown()
        {
            ESRuntimeDataModule.ItemInstanceTable.Clear();
            ResetWeaponDefinitions();
            for (int index = 0; index < created.Count; index++)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void EquipmentModuleBase_HasManagedReferenceSerializationIdentity()
        {
            Assert.That(
                Attribute.IsDefined(typeof(EntityEquipmentModuleBase), typeof(SerializableAttribute)),
                Is.True);
        }

        [Test]
        public void EquipAndUnequip_MoveOneHandleAcrossInventoryAndSlot()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out EntityEquipmentSlotModule slots);

            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        10,
                        1,
                        weaponDefinitionRuntimeKey: defaultWeaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);

            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out ESInstanceHandle equippedHandle,
                    out string equipError),
                Is.True,
                equipError);
            Assert.That(equippedHandle, Is.EqualTo(handle));
            Assert.That(inventory.TryGetItem(inventorySlot, out _), Is.False);
            Assert.That(slots.TryGetBoundItem(0, out ESInstanceHandle slotHandle), Is.True);
            Assert.That(slotHandle, Is.EqualTo(handle));
            Assert.That(
                ESRuntimeDataModule.ItemInstanceTable.TryGet(handle, out ESItemInstanceRecord equippedRecord),
                Is.True);
            Assert.That(equippedRecord.location, Is.EqualTo(ESItemInstanceLocation.Equipped));
            Assert.That(equippedRecord.relationSlot, Is.EqualTo(0));

            Assert.That(
                entity.equipmentDomain.TryUnequipItem(
                    0,
                    out ESInstanceHandle unequippedHandle,
                    out int returnedInventorySlot,
                    out string unequipError),
                Is.True,
                unequipError);
            Assert.That(unequippedHandle, Is.EqualTo(handle));
            Assert.That(slots.TryGetBoundItem(0, out _), Is.False);
            Assert.That(inventory.TryGetItem(returnedInventorySlot, out ESInstanceHandle storedHandle), Is.True);
            Assert.That(storedHandle, Is.EqualTo(handle));
        }

        [Test]
        public void PoolDespawn_RemovesInventoryAndEquippedInstancesOwnedByEntity()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out _);
            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        10,
                        1,
                        weaponDefinitionRuntimeKey: defaultWeaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);
            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out _,
                    out string error),
                Is.True,
                error);

            entity.equipmentDomain.NotifyPoolDespawned();

            Assert.That(ESRuntimeDataModule.ItemInstanceTable.IsCurrent(handle), Is.False);
        }

        [Test]
        public void UnresolvedWeaponDefinition_RollsItemBackToOriginalInventorySlot()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out EntityEquipmentSlotModule slots);
            slots.weaponSlots[0].weaponKey.stringKey = "tests.equipment.missing-weapon";

            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        10,
                        1,
                        weaponDefinitionRuntimeKey: defaultWeaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);

            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("not injected"));
            Assert.That(inventory.TryGetItem(inventorySlot, out ESInstanceHandle stored), Is.True);
            Assert.That(stored, Is.EqualTo(handle));
            Assert.That(slots.TryGetBoundItem(0, out _), Is.False);
            Assert.That(
                ESRuntimeDataModule.ItemInstanceTable.TryGet(handle, out ESItemInstanceRecord record),
                Is.True);
            Assert.That(record.location, Is.EqualTo(ESItemInstanceLocation.Inventory));
            Assert.That(record.relationSlot, Is.EqualTo(inventorySlot));
        }

        [Test]
        public void NonWeaponItem_CannotEnterWeaponSlotAndRollsBack()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out EntityEquipmentSlotModule slots);
            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(10, 1),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);

            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("Weapon projection"));
            Assert.That(inventory.TryGetItem(inventorySlot, out ESInstanceHandle stored), Is.True);
            Assert.That(stored, Is.EqualTo(handle));
            Assert.That(slots.TryGetBoundItem(0, out _), Is.False);
        }

        [Test]
        public void EntityPoolDespawnCallback_RemovesOwnedItemInstances()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out _);
            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        10,
                        1,
                        weaponDefinitionRuntimeKey: defaultWeaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out _),
                Is.True);

            entity.OnPoolDespawned();

            Assert.That(ESRuntimeDataModule.ItemInstanceTable.IsCurrent(handle), Is.False);
        }

        [Test]
        public void ActiveAttachmentTransition_BlocksInventoryEquipmentMutation()
        {
            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out EntityEquipmentSlotModule slots);
            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        10,
                        1,
                        weaponDefinitionRuntimeKey: defaultWeaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);

            EntityTransformMapping mapping = entity.gameObject.AddComponent<EntityTransformMapping>();
            Transform mainHand = CreateObject(EntityEquipmentSocketKeys.MainHandSocket).transform;
            mainHand.SetParent(entity.transform, false);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            EntityEquipmentWeaponSlot slot = slots.weaponSlots[0];
            var request = new EntityEquipmentTransitionRequest(
                new EntityEquipmentAttachmentOperation(
                    slot.weaponRoot,
                    slots.GetBinding(slot),
                    EntityEquipmentAttachmentPose.MainHand,
                    EntityEquipmentVisibilityState.Visible),
                EntityEquipmentTransitionPhase.Equipping,
                1);
            Assert.That(
                entity.equipmentDomain.TryPrepareAttachmentTransition(
                    request,
                    out EntityEquipmentTransitionToken token,
                    out string prepareError),
                Is.True,
                prepareError);

            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out _,
                    out string equipError),
                Is.False);
            Assert.That(equipError, Does.Contain("active attachment transition"));
            Assert.That(inventory.TryGetItem(inventorySlot, out ESInstanceHandle stored), Is.True);
            Assert.That(stored, Is.EqualTo(handle));
            Assert.That(entity.equipmentDomain.TryCancelAttachment(token, out _), Is.True);
        }

        [Test]
        public void PreparedWeaponPrefab_CreatesBindsUnequipsAndReturnsRuntimeView()
        {
            Assert.That(ESGameManager.Instance, Is.Null, "Weapon runtime view test requires an isolated GameManager.");
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.equipment.runtime-view" };
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.runtime-view" };
            var prefabIdentity = new ESAssetIdentity("guid-tests-equipment-runtime-view");
            ESAssetReferPrefabConfigKey prefabKey = CreateWeaponPrefabKey("tests.prefab.equipment.runtime-view");
            prefabKey.guid = prefabIdentity.Guid;
            GameObject prefab = CreateObject("RuntimeWeaponPrefab");
            Item prefabItem = prefab.AddComponent<Item>();
            EntityWeaponBinding prefabBinding = prefab.AddComponent<EntityWeaponBinding>();
            Transform grip = CreateObject("RuntimeWeaponGrip").transform;
            grip.SetParent(prefab.transform, false);
            prefabBinding.ConfigureReferences(grip, null, null, null, prefab);
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            info.name = "RuntimeWeaponDefinition";
            info.itemKey = itemKey;
            info.baseConfig.kind = ItemKind.Weapon;
            info.baseConfig.prefabKey = prefabKey;
            info.EnsureActiveKindData();
            ItemWeaponDataBlock block = (ItemWeaponDataBlock)info.kindData;
            block.key = weaponKey;
            block.sharedData = ItemWeaponSharedData.Default;
            block.initialState = ItemWeaponVariableData.Default;
            prefabItem.prefabDefinition = info;
            GameObject managerObject = null;
            bool activePlanRegistered = false;

            try
            {
                ESAssets.RegisterActivePlanAsset(prefabIdentity, prefab);
                activePlanRegistered = true;
                ESItemGameCoreTable.Inject(info);
                Assert.That(ESRuntimeDataGameCore.Weapons.TryGet(weaponKey, out ESWeaponRuntimeData weaponData), Is.True);
                Assert.That(weaponData.TryGetPreparedItemRuntimeKey(out int preparedItemRuntimeKey), Is.True);
                Assert.That(preparedItemRuntimeKey, Is.GreaterThan(0));
                weaponData.soSource = null;

                managerObject = CreateObject("WeaponRuntimeViewManager");
                ESGameManager manager = managerObject.AddComponent<ESGameManager>();
                manager.dontDestroyOnLoad = false;
                manager.autoCreateCommandModule = false;
                manager.autoCreateInputModule = false;
                manager.autoCreateAudioModule = false;
                manager.autoCreateVfxModule = false;
                manager.autoCreateCameraModule = false;
                manager.autoCreatePhysicsQueryModule = false;
                manager.autoCreateLODModule = false;
                manager.autoCreateWorldMapModule = false;
                InvokeAwakeForEditModeTest(manager);
                Assert.That(ESGameManager.TryGetModule(out ESGameObjectPoolModule pool), Is.True);

                CreateDomain(
                    out Entity entity,
                    out EntityEquipmentInventoryModule inventory,
                    out EntityEquipmentSlotModule slots,
                    createAuthoredWeaponRoot: false);
                slots.weaponSlots[0].weaponKey = weaponKey;

                Assert.That(
                    entity.equipmentDomain.TryCreateAndEquipWeapon(0, out ESInstanceHandle handle, out string error),
                    Is.True,
                    error);
                Assert.That(handle.IsValid, Is.True);
                Assert.That(slots.TryGetWeaponSlot(0, out EntityEquipmentWeaponSlot slot, out error), Is.True, error);
                Assert.That(slot.weaponRoot, Is.Not.Null);
                Assert.That(slot.weaponRoot.GetComponent<Item>().prefabDefinition, Is.SameAs(info));
                Assert.That(slot.weaponRoot.GetComponent<EntityWeaponBinding>(), Is.Not.Null);
                Assert.That(ESRuntimeDataModule.ItemInstanceTable.TryGet(handle, out ESItemInstanceRecord record), Is.True);
                Assert.That(record.location, Is.EqualTo(ESItemInstanceLocation.Equipped));
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats activeStats), Is.True);
                Assert.That(activeStats.activeCount, Is.EqualTo(1));

                Assert.That(
                    entity.equipmentDomain.TryUnequipItem(0, out ESInstanceHandle unequipped, out int inventorySlot, out error),
                    Is.True,
                    error);
                Assert.That(unequipped, Is.EqualTo(handle));
                Assert.That(inventory.TryGetItem(inventorySlot, out ESInstanceHandle stored), Is.True);
                Assert.That(stored, Is.EqualTo(handle));
                Assert.That(slots.weaponSlots[0].weaponRoot, Is.Null);
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats returnedStats), Is.True);
                Assert.That(returnedStats.activeCount, Is.Zero);

                entity.equipmentDomain.NotifyPoolDespawned();
                Assert.That(ESRuntimeDataModule.ItemInstanceTable.IsCurrent(handle), Is.False);
            }
            finally
            {
                if (activePlanRegistered)
                    ESAssets.UnregisterActivePlanAsset(prefabIdentity);
                if (managerObject != null)
                    UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void ConfiguredWeaponSlot_UsesWeaponProjectionInsteadOfItemRuntimeKey()
        {
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.equipment.weapon-projection" };
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            int weaponRuntimeKey;
            try
            {
                ESRuntimeDataGameCore.Weapons.InjectWith(
                    new ESWeaponConfigKey { stringKey = "tests.equipment.padding" },
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.equipment.padding"));
                weaponRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.equipment.projection"));
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }

            CreateDomain(
                out Entity entity,
                out EntityEquipmentInventoryModule inventory,
                out EntityEquipmentSlotModule slots);
            slots.weaponSlots[0].weaponKey = weaponKey;

            int independentItemRuntimeKey = weaponRuntimeKey == 1 ? 2 : 1;
            Assert.That(independentItemRuntimeKey, Is.Not.EqualTo(weaponRuntimeKey));
            Assert.That(
                inventory.TryCreateItem(
                    new ESItemInstanceCreateRequest(
                        independentItemRuntimeKey,
                        ownerId: 1,
                        weaponDefinitionRuntimeKey: weaponRuntimeKey),
                    out ESInstanceHandle handle,
                    out int inventorySlot),
                Is.True);
            Assert.That(
                entity.equipmentDomain.TryEquipInventoryItem(
                    inventorySlot,
                    0,
                    out ESInstanceHandle equipped,
                    out string error),
                Is.True,
                error);
            Assert.That(equipped, Is.EqualTo(handle));
            Assert.That(slots.TryGetBoundItem(0, out ESInstanceHandle bound), Is.True);
            Assert.That(bound, Is.EqualTo(handle));
        }

        private void CreateDomain(
            out Entity entity,
            out EntityEquipmentInventoryModule inventory,
            out EntityEquipmentSlotModule slots,
            bool createAuthoredWeaponRoot = true)
        {
            GameObject root = CreateObject("EquipmentEntity");
            entity = root.AddComponent<Entity>();
            entity.EnsureEntityStructure();
            entity.equipmentDomain._Editor_RegisterAllButOnlyCreateRelationship(entity);
            entity.RegisterDomain(entity.equipmentDomain);

            inventory = new EntityEquipmentInventoryModule { capacity = 4 };
            slots = new EntityEquipmentSlotModule();
            entity.equipmentDomain.TryAddModuleRuntime(inventory);
            entity.equipmentDomain.TryAddModuleRuntime(slots);
            entity.equipmentDomain.TryAddModuleRuntime(new EntityEquipmentAttachmentModule());
            entity.equipmentDomain.TryAddModuleRuntime(new EntityEquipmentEffectModule());
            entity.equipmentDomain.MyModules.ApplyBuffers(true);
            inventory.OnPoolSpawned();
            slots.OnPoolSpawned();

            GameObject weaponRoot = null;
            if (createAuthoredWeaponRoot)
            {
                weaponRoot = CreateObject("WeaponRoot");
                weaponRoot.transform.SetParent(entity.transform, false);
                EntityWeaponBinding binding = weaponRoot.AddComponent<EntityWeaponBinding>();
                Transform grip = CreateObject("GripPivot").transform;
                grip.SetParent(weaponRoot.transform, false);
                binding.ConfigureReferences(grip, null, null, null, weaponRoot);
            }
            slots.weaponSlots.Add(new EntityEquipmentWeaponSlot
            {
                displayName = "Test Weapon",
                weaponKey = new ESWeaponConfigKey { stringKey = DefaultWeaponKey },
                weaponRoot = weaponRoot != null ? weaponRoot.transform : null,
            });
        }

        private static void InvokeAwakeForEditModeTest(ESGameManager manager)
        {
            System.Reflection.MethodInfo awake = typeof(ESGameManager).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(manager, null);
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }

        private static void ResetWeaponDefinitions()
        {
            if (ESRuntimeDataGameCore.Weapons.IsBuilding)
                Assert.Fail("Weapon definition table leaked an active build transaction.");
            ESRuntimeDataGameCore.Weapons.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.EndBuild();
        }

        private static int InjectWeaponDefinition(string stringKey)
        {
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            try
            {
                return ESRuntimeDataGameCore.Weapons.InjectWith(
                    new ESWeaponConfigKey { stringKey = stringKey },
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab." + stringKey));
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }
        }
    }
}
