using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESItemInstanceTableTests
    {
        [SetUp]
        public void SetUp()
        {
            ClearDefinitionTables();
        }

        [TearDown]
        public void TearDown()
        {
            ClearDefinitionTables();
        }

        [Test]
        public void CreateMoveAndDestroy_PreservesHandleAndOwnerIndex()
        {
            var table = new ESItemInstanceTable(4);
            var request = new ESItemInstanceCreateRequest(
                itemDefinitionRuntimeKey: 10,
                ownerId: 100,
                quantity: 3,
                location: ESItemInstanceLocation.Inventory,
                relationSlot: 2);

            Assert.That(table.TryCreate(request, out ESInstanceHandle handle), Is.True);
            Assert.That(table.TryGet(handle, out ESItemInstanceRecord record), Is.True);
            Assert.That(record.quantity, Is.EqualTo(3));
            Assert.That(record.location, Is.EqualTo(ESItemInstanceLocation.Inventory));

            Assert.That(table.TryMove(
                handle,
                ownerId: 200,
                location: ESItemInstanceLocation.Equipped,
                relationSlot: 0), Is.True);
            Assert.That(table.TryGetIdentity(handle, out _, out int definitionKey, out int ownerId), Is.True);
            Assert.That(definitionKey, Is.EqualTo(10));
            Assert.That(ownerId, Is.EqualTo(200));
            Assert.That(table.TryGetOwnerBucket(100, out _, out _), Is.False);
            Assert.That(table.TryGetOwnerBucket(200, out ESInstanceHandle ownerFirst, out int ownerCount), Is.True);
            Assert.That(ownerFirst, Is.EqualTo(handle));
            Assert.That(ownerCount, Is.EqualTo(1));

            Assert.That(table.TryRemove(handle, out _), Is.True);
            Assert.That(table.TryGet(handle, out _), Is.False);
            Assert.That(table.Count, Is.Zero);
        }

        [Test]
        public void ExplicitPersistentId_IsRejectedWhenAlreadyInUse()
        {
            var table = new ESItemInstanceTable(2);
            var firstRequest = new ESItemInstanceCreateRequest(1, 1, persistentId: 9001);
            var secondRequest = new ESItemInstanceCreateRequest(1, 1, persistentId: 9001);

            Assert.That(table.TryCreate(firstRequest, out _), Is.True);
            Assert.That(table.TryCreate(secondRequest, out _), Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void AutomaticPersistentId_SkipsAllLoadedSequentialIds()
        {
            const int loadedCount = 1024;
            var table = new ESItemInstanceTable(loadedCount + 1);
            for (ulong persistentId = 1; persistentId <= loadedCount; persistentId++)
            {
                var loadedRequest = new ESItemInstanceCreateRequest(
                    itemDefinitionRuntimeKey: 1,
                    ownerId: 1,
                    persistentId: persistentId);
                Assert.That(table.TryCreate(loadedRequest, out _), Is.True);
            }

            var runtimeRequest = new ESItemInstanceCreateRequest(1, 1);
            Assert.That(table.TryCreate(runtimeRequest, out ESInstanceHandle handle), Is.True);
            Assert.That(table.TryGetPersistentId(handle, out ulong allocatedId), Is.True);
            Assert.That(allocatedId, Is.EqualTo((ulong)loadedCount + 1));
        }

        [Test]
        public void WorldViewTransfer_MovesInstanceAndViewAsOneOperation()
        {
            var table = new ESItemInstanceTable(1);
            var request = new ESItemInstanceCreateRequest(
                itemDefinitionRuntimeKey: 1,
                ownerId: 10,
                location: ESItemInstanceLocation.Inventory);
            Assert.That(table.TryCreate(request, out ESInstanceHandle handle), Is.True);

            GameObject root = new GameObject("ItemView");
            GameObject target = new GameObject("WorldAnchor");
            EntityWeaponBinding binding = root.AddComponent<EntityWeaponBinding>();
            Transform grip = new GameObject("GripPivot").transform;
            grip.SetParent(root.transform, false);
            binding.ConfigureReferences(grip, null, null, null, root);

            try
            {
                var transfer = new ESItemInstanceViewTransferRequest(
                    root.transform,
                    binding,
                    target.transform,
                    ownerId: 20,
                    location: ESItemInstanceLocation.World,
                    relationSlot: -1,
                    visible: true);
                Assert.That(ESItemInstanceViewTransfer.TryCommit(table, handle, transfer, out string error), Is.True, error);
                Assert.That(root.transform.parent, Is.EqualTo(target.transform));
                Assert.That(table.TryGetIdentity(handle, out _, out _, out int ownerId), Is.True);
                Assert.That(ownerId, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(grip.gameObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableItemKey_ResolvesIndependentItemAndWeaponRuntimeKeys()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.long_bar" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.long_bar" };
            var dummyWeaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.padding" };

            ESRuntimeDataGameCore.Items.BeginBuild();
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            int itemRuntimeKey;
            int weaponRuntimeKey;
            try
            {
                ESRuntimeDataGameCore.Weapons.InjectWith(
                    dummyWeaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default);
                weaponRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default);
                itemRuntimeKey = ESRuntimeDataGameCore.Items.InjectWith(
                    itemKey,
                    ItemKind.Weapon,
                    new ItemBaseConfig { kind = ItemKind.Weapon },
                    weaponKey: weaponKey);
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
                ESRuntimeDataGameCore.Items.EndBuild();
            }

            Assert.That(itemRuntimeKey, Is.Not.EqualTo(weaponRuntimeKey));
            var table = new ESItemInstanceTable(1);
            Assert.That(table.TryCreate(itemKey, ownerId: 10, out ESInstanceHandle handle), Is.True);
            Assert.That(
                table.TryGetDefinitionRuntimeKeys(
                    handle,
                    out int resolvedItemRuntimeKey,
                    out int resolvedWeaponRuntimeKey),
                Is.True);
            Assert.That(resolvedItemRuntimeKey, Is.EqualTo(itemRuntimeKey));
            Assert.That(resolvedWeaponRuntimeKey, Is.EqualTo(weaponRuntimeKey));
        }

        [Test]
        public void ItemGameCoreProjectionConflict_RollsBackNewBaseProjection()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.rollback" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.rollback" };
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            ESWeaponRuntimeData existingWeapon;
            try
            {
                int existingRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default);
                Assert.That(
                    ESRuntimeDataGameCore.Weapons.TryGet(existingRuntimeKey, out existingWeapon),
                    Is.True);
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }

            ItemDataInfo info = CreateWeaponInfo(itemKey, weaponKey);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => ESItemGameCoreTable.Inject(info));
                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out _), Is.False);
                Assert.That(ESRuntimeDataGameCore.Weapons.TryGet(weaponKey, out ESWeaponRuntimeData current), Is.True);
                Assert.That(current, Is.SameAs(existingWeapon));
                Assert.That(current.Ready, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void ItemAndWeaponProjection_ClearReleasesBothRetainedPayloads()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.release" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.release" };
            ItemDataInfo info = CreateWeaponInfo(itemKey, weaponKey);
            try
            {
                ESRuntimeDataGameCore.Weapons.BeginBuild();
                try
                {
                    ESRuntimeDataGameCore.Weapons.InjectWith(
                        new ESWeaponConfigKey { stringKey = "tests.weapon.release.padding" },
                        ItemWeaponSharedData.Default,
                        ItemWeaponVariableData.Default);
                }
                finally
                {
                    ESRuntimeDataGameCore.Weapons.EndBuild();
                }

                ESItemGameCoreTable.Inject(info);
                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out ESItemRuntimeData itemData), Is.True);
                Assert.That(ESRuntimeDataGameCore.Weapons.TryGet(weaponKey, out ESWeaponRuntimeData weaponData), Is.True);
                Assert.That(itemData.Ready, Is.True);
                Assert.That(weaponData.Ready, Is.True);
                Assert.That(itemData.runtimeKey, Is.Not.EqualTo(weaponData.runtimeKey));

                ClearDefinitionTables();

                Assert.That(itemData.Ready, Is.False);
                Assert.That(itemData.soSource, Is.Null);
                Assert.That(itemData.baseConfig, Is.Null);
                Assert.That(itemData.tags, Is.Null);
                Assert.That(itemData.weaponKey, Is.Null);
                Assert.That(weaponData.Ready, Is.False);
                Assert.That(weaponData.soSource, Is.Null);
                Assert.That(weaponData.sharedData, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void NormalItem_InjectsOnlyBaseItemProjection()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.prop.base_only" };
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            info.itemKey = itemKey;
            info.baseConfig.kind = ItemKind.Prop;
            info.EnsureActiveKindData();

            try
            {
                ESItemGameCoreTable.Inject(info);

                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out ESItemRuntimeData itemData), Is.True);
                Assert.That(itemData.Ready, Is.True);
                Assert.That(itemData.kind, Is.EqualTo(ItemKind.Prop));
                Assert.That(ESRuntimeDataGameCore.Shots.Count, Is.Zero);
                Assert.That(ESRuntimeDataGameCore.Weapons.Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        private static ItemDataInfo CreateWeaponInfo(
            ESItemConfigKey itemKey,
            ESWeaponConfigKey weaponKey)
        {
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            info.itemKey = itemKey;
            info.baseConfig.kind = ItemKind.Weapon;
            info.EnsureActiveKindData();
            ItemWeaponDataBlock weapon = (ItemWeaponDataBlock)info.kindData;
            weapon.key = weaponKey;
            weapon.sharedData = ItemWeaponSharedData.Default;
            weapon.initialState = ItemWeaponVariableData.Default;
            return info;
        }

        private static void ClearDefinitionTables()
        {
            if (ESRuntimeDataGameCore.Items.IsBuilding
                || ESRuntimeDataGameCore.Shots.IsBuilding
                || ESRuntimeDataGameCore.Weapons.IsBuilding)
            {
                Assert.Fail("Item/Shot/Weapon definition table leaked an active build transaction.");
            }
            ESRuntimeDataGameCore.Items.BeginBuild(true);
            ESRuntimeDataGameCore.Shots.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.EndBuild();
            ESRuntimeDataGameCore.Shots.EndBuild();
            ESRuntimeDataGameCore.Items.EndBuild();
        }
    }
}
