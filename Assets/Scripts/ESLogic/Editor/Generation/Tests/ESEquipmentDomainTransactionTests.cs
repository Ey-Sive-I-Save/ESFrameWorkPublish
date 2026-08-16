using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESEquipmentDomainTransactionTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ESRuntimeDataModule.ItemInstanceTable.Clear();
            ResetWeaponDefinitions();
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
                    new ESItemInstanceCreateRequest(10, 1),
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
                    ItemWeaponVariableData.Default);
                weaponRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default);
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
            out EntityEquipmentSlotModule slots)
        {
            GameObject root = CreateObject("EquipmentEntity");
            entity = root.AddComponent<Entity>();
            entity.EnsureEntityStructure();
            entity.equipmentDomain._Editor_RegisterAllButOnlyCreateRelationship(entity);

            inventory = new EntityEquipmentInventoryModule { capacity = 4 };
            slots = new EntityEquipmentSlotModule();
            entity.equipmentDomain.TryAddModuleRuntime(inventory);
            entity.equipmentDomain.TryAddModuleRuntime(slots);
            entity.equipmentDomain.TryAddModuleRuntime(new EntityEquipmentAttachmentModule());
            entity.equipmentDomain.TryAddModuleRuntime(new EntityEquipmentEffectModule());
            entity.equipmentDomain.MyModules.ApplyBuffers(true);
            inventory.OnPoolSpawned();
            slots.OnPoolSpawned();

            GameObject weaponRoot = CreateObject("WeaponRoot");
            weaponRoot.transform.SetParent(entity.transform, false);
            EntityWeaponBinding binding = weaponRoot.AddComponent<EntityWeaponBinding>();
            Transform grip = CreateObject("GripPivot").transform;
            grip.SetParent(weaponRoot.transform, false);
            binding.ConfigureReferences(grip, null, null, null, weaponRoot);
            slots.weaponSlots.Add(new EntityEquipmentWeaponSlot
            {
                displayName = "Test Weapon",
                weaponRoot = weaponRoot.transform,
            });
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
    }
}
