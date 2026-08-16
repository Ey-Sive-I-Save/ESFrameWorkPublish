using System;
using System.Threading;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("装备背包模块")]
    public sealed class EntityEquipmentInventoryModule : EntityEquipmentModuleBase
    {
        private static int nextOwnerId;

        [MinValue(1), LabelText("背包容量")]
        public int capacity = 32;

        [NonSerialized] private ESInstanceHandle[] itemSlots;
        [NonSerialized] private int ownerId;
        [NonSerialized] private int itemCount;

        [ShowInInspector, ReadOnly, LabelText("运行时所有者 ID")]
        public int OwnerId => ownerId;

        [ShowInInspector, ReadOnly, LabelText("物品数量")]
        public int ItemCount => itemCount;

        public int Capacity => itemSlots != null ? itemSlots.Length : Math.Max(1, capacity);

        public override void Start()
        {
            base.Start();
            EnsureRuntimeState();
        }

        public void OnPoolSpawned()
        {
            EnsureRuntimeState();
        }

        public void OnPoolDespawned()
        {
            ReleaseAllItems();
            ownerId = 0;
        }

        public bool TryCreateItem(
            in ESItemInstanceCreateRequest request,
            out ESInstanceHandle handle,
            out int inventorySlot)
        {
            EnsureRuntimeState();
            handle = default;
            inventorySlot = FindFreeSlot();
            if (inventorySlot < 0)
                return false;

            var ownedRequest = new ESItemInstanceCreateRequest(
                request.itemDefinitionRuntimeKey,
                ownerId,
                request.weaponDefinitionRuntimeKey,
                request.quantity,
                ESItemInstanceLocation.Inventory,
                inventorySlot,
                request.stateBits,
                request.persistentId);
            if (!ESRuntimeDataModule.ItemInstanceTable.TryCreate(ownedRequest, out handle))
                return false;

            itemSlots[inventorySlot] = handle;
            itemCount++;
            return true;
        }

        public bool TryGetItem(int inventorySlot, out ESInstanceHandle handle)
        {
            if (itemSlots == null
                || (uint)inventorySlot >= (uint)itemSlots.Length
                || !ESRuntimeDataModule.ItemInstanceTable.IsCurrent(itemSlots[inventorySlot]))
            {
                handle = default;
                return false;
            }

            handle = itemSlots[inventorySlot];
            return true;
        }

        public bool TryEquipItem(int inventorySlot, int equipmentSlot, out ESInstanceHandle handle)
        {
            handle = default;
            if (equipmentSlot < 0 || !TryGetItem(inventorySlot, out handle))
                return false;
            if (!ESRuntimeDataModule.ItemInstanceTable.TryMove(
                    handle,
                    ownerId,
                    ESItemInstanceLocation.Equipped,
                    equipmentSlot))
                return false;

            itemSlots[inventorySlot] = default;
            itemCount--;
            return true;
        }

        public bool TryStoreItem(ESInstanceHandle handle, out int inventorySlot)
        {
            EnsureRuntimeState();
            inventorySlot = FindFreeSlot();
            return inventorySlot >= 0 && TryStoreItemAt(handle, inventorySlot);
        }

        public bool TryStoreItemAt(ESInstanceHandle handle, int inventorySlot)
        {
            EnsureRuntimeState();
            if ((uint)inventorySlot >= (uint)itemSlots.Length
                || ESRuntimeDataModule.ItemInstanceTable.IsCurrent(itemSlots[inventorySlot])
                || !ESRuntimeDataModule.ItemInstanceTable.TryMove(
                    handle,
                    ownerId,
                    ESItemInstanceLocation.Inventory,
                    inventorySlot))
                return false;

            itemSlots[inventorySlot] = handle;
            itemCount++;
            return true;
        }

        public bool TryRemoveItem(int inventorySlot, out ESItemInstanceRecord removed)
        {
            removed = default;
            if (!TryGetItem(inventorySlot, out ESInstanceHandle handle)
                || !ESRuntimeDataModule.ItemInstanceTable.TryRemove(handle, out removed))
                return false;

            itemSlots[inventorySlot] = default;
            itemCount--;
            return true;
        }

        private void EnsureRuntimeState()
        {
            int resolvedCapacity = Math.Max(1, capacity);
            if (itemSlots == null || itemSlots.Length != resolvedCapacity)
                itemSlots = new ESInstanceHandle[resolvedCapacity];
            if (ownerId == 0)
                ownerId = AllocateOwnerId();
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < itemSlots.Length; index++)
            {
                if (!ESRuntimeDataModule.ItemInstanceTable.IsCurrent(itemSlots[index]))
                    return index;
            }

            return -1;
        }

        private void ReleaseAllItems()
        {
            if (itemSlots == null)
                return;

            while (ownerId != 0
                && ESRuntimeDataModule.ItemInstanceTable.TryGetOwnerBucket(
                    ownerId,
                    out ESInstanceHandle ownedHandle,
                    out _))
            {
                if (!ESRuntimeDataModule.ItemInstanceTable.TryRemove(ownedHandle, out _))
                    break;
            }

            for (int index = 0; index < itemSlots.Length; index++)
                itemSlots[index] = default;
            itemCount = 0;
        }

        private static int AllocateOwnerId()
        {
            int id = Interlocked.Increment(ref nextOwnerId);
            if (id <= 0)
                throw new InvalidOperationException("Entity equipment owner ID space is exhausted.");
            return id;
        }
    }
}
