using System;
using UnityEngine;

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
        public bool hasWeaponState;
        public ItemWeaponVariableData weaponState;
    }

    public enum ESWeaponUseFailure : byte
    {
        None = 0,
        InvalidHandle = 1,
        MissingWeaponDefinition = 2,
        Cooldown = 3,
        Ammo = 4,
        Durability = 5,
        Overheated = 6
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
            if (request.weaponDefinitionRuntimeKey > 0
                && ESRuntimeDataGameCore.Weapons.TryGet(
                    request.weaponDefinitionRuntimeKey,
                    out ESWeaponRuntimeData weaponRuntimeData)
                && weaponRuntimeData != null
                && weaponRuntimeData.Ready)
            {
                record.hasWeaponState = true;
                record.weaponState = weaponRuntimeData.defaultVariableData;
                record.weaponState.lastStateUpdateTime = 0f;
            }
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

        public bool TryGetWeaponState(
            ESInstanceHandle handle,
            float now,
            out ItemWeaponVariableData state)
        {
            state = default;
            if (!TryGet(handle, out ESItemInstanceRecord record)
                || !TryEnsureWeaponState(ref record))
                return false;

            RefreshWeaponState(ref record.weaponState, record.weaponDefinitionRuntimeKey, now);
            state = record.weaponState;
            return TrySet(handle, record);
        }

        public bool Internal_TrySetWeaponState(
            ESInstanceHandle handle,
            in ItemWeaponVariableData state)
        {
            if (!TryGet(handle, out ESItemInstanceRecord record)
                || record.weaponDefinitionRuntimeKey <= 0)
                return false;

            record.hasWeaponState = true;
            record.weaponState = state;
            return TrySet(handle, record);
        }

        /// <summary>
        /// 以一个表写事务刷新并消费当前武器实例。定义是只读规则，所有可变结果只写回 itemHandle。
        /// </summary>
        public bool TryConsumeWeaponUse(
            ESInstanceHandle handle,
            ItemWeaponSharedData definition,
            float now,
            out ItemWeaponVariableData state,
            out ESWeaponUseFailure failure,
            float cooldownOverride = -1f)
        {
            state = default;
            failure = ESWeaponUseFailure.InvalidHandle;
            if (definition == null
                || definition.fire == null
                || !TryGet(handle, out ESItemInstanceRecord record)
                || !TryEnsureWeaponState(ref record))
            {
                failure = definition == null || definition.fire == null
                    ? ESWeaponUseFailure.MissingWeaponDefinition
                    : ESWeaponUseFailure.InvalidHandle;
                return false;
            }
            if (!ESRuntimeDataGameCore.Weapons.TryGet(
                    record.weaponDefinitionRuntimeKey,
                    out ESWeaponRuntimeData boundRuntimeData)
                || boundRuntimeData == null
                || !boundRuntimeData.Ready
                || !ReferenceEquals(boundRuntimeData.sharedData, definition))
            {
                failure = ESWeaponUseFailure.MissingWeaponDefinition;
                return false;
            }

            RefreshWeaponState(ref record.weaponState, record.weaponDefinitionRuntimeKey, now);
            WeaponFireDefinitionData fire = definition.fire;
            if (record.weaponState.cooldownLeft > 0f)
                failure = ESWeaponUseFailure.Cooldown;
            else if (fire.ammoCost > 0 && record.weaponState.ammo < fire.ammoCost)
                failure = ESWeaponUseFailure.Ammo;
            else if (fire.durabilityCost > 0f && record.weaponState.durability < fire.durabilityCost)
                failure = ESWeaponUseFailure.Durability;
            else if (fire.maxHeat > 0f && record.weaponState.heat + fire.heatPerUse > fire.maxHeat)
                failure = ESWeaponUseFailure.Overheated;
            else
            {
                record.weaponState.ammo -= fire.ammoCost;
                record.weaponState.durability = Mathf.Max(0f, record.weaponState.durability - fire.durabilityCost);
                record.weaponState.heat = Mathf.Max(0f, record.weaponState.heat + fire.heatPerUse);
                record.weaponState.cooldownLeft = cooldownOverride >= 0f
                    ? Mathf.Max(0.01f, cooldownOverride)
                    : Mathf.Max(
                        Mathf.Max(0f, definition.cooldown),
                        Mathf.Max(0.01f, fire.interval));
                record.weaponState.logicSeed++;
                if (record.weaponState.logicSeed == int.MinValue)
                    record.weaponState.logicSeed = 1;
                failure = ESWeaponUseFailure.None;
            }

            state = record.weaponState;
            return TrySet(handle, record) && failure == ESWeaponUseFailure.None;
        }

        private static bool TryEnsureWeaponState(ref ESItemInstanceRecord record)
        {
            if (record.weaponDefinitionRuntimeKey <= 0)
                return false;
            if (record.hasWeaponState)
                return true;
            if (!ESRuntimeDataGameCore.Weapons.TryGet(
                    record.weaponDefinitionRuntimeKey,
                    out ESWeaponRuntimeData runtimeData)
                || runtimeData == null
                || !runtimeData.Ready)
                return false;

            record.hasWeaponState = true;
            record.weaponState = runtimeData.defaultVariableData;
            record.weaponState.lastStateUpdateTime = 0f;
            return true;
        }

        private static void RefreshWeaponState(
            ref ItemWeaponVariableData state,
            int weaponDefinitionRuntimeKey,
            float now)
        {
            float safeNow = Mathf.Max(0f, now);
            float elapsed = state.lastStateUpdateTime > 0f
                ? Mathf.Max(0f, safeNow - state.lastStateUpdateTime)
                : 0f;
            state.cooldownLeft = Mathf.Max(0f, state.cooldownLeft - elapsed);

            if (elapsed > 0f
                && ESRuntimeDataGameCore.Weapons.TryGet(
                    weaponDefinitionRuntimeKey,
                    out ESWeaponRuntimeData runtimeData)
                && runtimeData?.sharedData?.fire != null)
            {
                float dissipation = Mathf.Max(0f, runtimeData.sharedData.fire.heatDissipationPerSecond);
                state.heat = Mathf.Max(0f, state.heat - elapsed * dissipation);
            }
            state.lastStateUpdateTime = safeNow;
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
