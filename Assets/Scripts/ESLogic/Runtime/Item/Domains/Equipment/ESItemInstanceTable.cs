using System;

namespace ES
{
    public enum ESItemInstanceLocation : byte
    {
        Detached = 0,
        Inventory = 1,
        Equipped = 2,
        World = 3,
    }

    [Serializable]
    public struct ESItemInstanceRecord
    {
        public int quantity;
        public int relationSlot;
        public int weaponDefinitionRuntimeKey;
        public ESItemInstanceLocation location;
        public uint stateBits;
    }

    public readonly struct ESItemInstanceCreateRequest
    {
        public readonly ulong persistentId;
        public readonly int itemDefinitionRuntimeKey;
        public readonly int weaponDefinitionRuntimeKey;
        public readonly int ownerId;
        public readonly int quantity;
        public readonly int relationSlot;
        public readonly ESItemInstanceLocation location;
        public readonly uint stateBits;

        public ESItemInstanceCreateRequest(
            int itemDefinitionRuntimeKey,
            int ownerId,
            int weaponDefinitionRuntimeKey = 0,
            int quantity = 1,
            ESItemInstanceLocation location = ESItemInstanceLocation.Detached,
            int relationSlot = -1,
            uint stateBits = 0,
            ulong persistentId = 0)
        {
            this.persistentId = persistentId;
            this.itemDefinitionRuntimeKey = itemDefinitionRuntimeKey;
            this.weaponDefinitionRuntimeKey = weaponDefinitionRuntimeKey;
            this.ownerId = ownerId;
            this.quantity = quantity;
            this.relationSlot = relationSlot;
            this.location = location;
            this.stateBits = stateBits;
        }
    }

    /// <summary>
    /// Item-domain specialization of the fixed-capacity instance table.
    /// Runtime definition keys and owner IDs are process-local indexes; only the
    /// persistent ID is allowed to cross a save/network boundary.
    /// </summary>
    public sealed class ESItemInstanceTable
        : ESInstanceTable<ESItemInstanceRecord, ulong, int, int>
    {
        private ulong nextPersistentId = 1;

        public ESItemInstanceTable(int capacity)
            : base(capacity)
        {
        }

        public bool TryCreate(
            in ESItemInstanceCreateRequest request,
            out ESInstanceHandle handle)
        {
            handle = default;
            if (request.itemDefinitionRuntimeKey <= 0
                || request.weaponDefinitionRuntimeKey < 0
                || request.ownerId <= 0
                || request.quantity <= 0
                || request.relationSlot < -1)
                return false;

            ulong persistentId = request.persistentId;
            if (persistentId == 0)
            {
                if (!TryAllocatePersistentId(out persistentId))
                    return false;
            }

            var record = new ESItemInstanceRecord
            {
                quantity = request.quantity,
                relationSlot = request.relationSlot,
                weaponDefinitionRuntimeKey = request.weaponDefinitionRuntimeKey,
                location = request.location,
                stateBits = request.stateBits,
            };
            return TryAdd(record, persistentId, request.itemDefinitionRuntimeKey, request.ownerId, out handle);
        }

        public bool TryCreate(
            ESItemConfigKey itemKey,
            int ownerId,
            out ESInstanceHandle handle,
            int quantity = 1,
            ESItemInstanceLocation location = ESItemInstanceLocation.Detached,
            int relationSlot = -1,
            uint stateBits = 0,
            ulong persistentId = 0)
        {
            handle = default;
            if (itemKey == null
                || !itemKey.IsConfigured
                || !ESRuntimeDataGameCore.Items.TryGetRuntimeKey(itemKey, out int itemRuntimeKey)
                || !ESRuntimeDataGameCore.Items.TryGet(itemRuntimeKey, out ESItemRuntimeData itemData)
                || itemData == null
                || !itemData.Ready)
            {
                return false;
            }

            int weaponRuntimeKey = 0;
            if (itemData.weaponKey != null && itemData.weaponKey.IsConfigured)
            {
                if (!ESRuntimeDataGameCore.Weapons.TryGetRuntimeKey(
                        itemData.weaponKey,
                        out weaponRuntimeKey))
                {
                    return false;
                }
            }
            else if (itemData.kind == ItemKind.Weapon)
            {
                return false;
            }

            var request = new ESItemInstanceCreateRequest(
                itemRuntimeKey,
                ownerId,
                weaponRuntimeKey,
                quantity,
                location,
                relationSlot,
                stateBits,
                persistentId);
            return TryCreate(request, out handle);
        }

        public bool TryMove(
            ESInstanceHandle handle,
            int ownerId,
            ESItemInstanceLocation location,
            int relationSlot)
        {
            if (ownerId <= 0 || relationSlot < -1 || !TryGet(handle, out ESItemInstanceRecord record))
                return false;

            if (!TrySetOwner(handle, ownerId))
                return false;

            record.location = location;
            record.relationSlot = relationSlot;
            return TrySet(handle, record);
        }

        public bool TrySetQuantity(ESInstanceHandle handle, int quantity)
        {
            if (quantity <= 0 || !TryGet(handle, out ESItemInstanceRecord record))
                return false;

            record.quantity = quantity;
            return TrySet(handle, record);
        }

        public bool TryGetPersistentId(ESInstanceHandle handle, out ulong persistentId)
        {
            return TryGetIdentity(handle, out persistentId, out _, out _);
        }

        public bool TryGetDefinitionRuntimeKeys(
            ESInstanceHandle handle,
            out int itemDefinitionRuntimeKey,
            out int weaponDefinitionRuntimeKey)
        {
            if (TryGetIdentity(handle, out _, out itemDefinitionRuntimeKey, out _)
                && TryGet(handle, out ESItemInstanceRecord record))
            {
                weaponDefinitionRuntimeKey = record.weaponDefinitionRuntimeKey;
                return true;
            }

            itemDefinitionRuntimeKey = 0;
            weaponDefinitionRuntimeKey = 0;
            return false;
        }

        private bool TryAllocatePersistentId(out ulong persistentId)
        {
            // At most Count currently-live IDs can collide. Count + 1 sequential
            // candidates therefore guarantees a free ID unless the ulong space wraps.
            int maximumAttempts = Count + 1;
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                if (nextPersistentId == 0)
                {
                    persistentId = 0;
                    return false;
                }

                persistentId = nextPersistentId++;
                if (!TryGetByPersistentId(persistentId, out _))
                    return true;
            }

            persistentId = 0;
            return false;
        }
    }
}
