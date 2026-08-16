using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class EntityEquipmentWeaponSlot
    {
        [LabelText("显示名")]
        public string displayName;

        [LabelText("武器定义 Key")]
        public ESWeaponConfigKey weaponKey = new ESWeaponConfigKey();

        [LabelText("武器根节点")]
        public Transform weaponRoot;

        [LabelText("普攻 Action 覆盖")]
        public ESActionConfigKey primaryAttackActionOverride = new ESActionConfigKey();

        [NonSerialized] internal EntityWeaponBinding runtimeBinding;
        [NonSerialized] internal ESInstanceHandle itemHandle;
    }

    [Serializable, TypeRegistryItem("装备槽位模块")]
    public sealed class EntityEquipmentSlotModule : EntityEquipmentModuleBase
    {
        [LabelText("武器槽位")]
        public List<EntityEquipmentWeaponSlot> weaponSlots = new List<EntityEquipmentWeaponSlot>();

        [NonSerialized] private int activeWeaponSlot = -1;
        [NonSerialized] private bool weaponInHand;
        [NonSerialized] private int revision;

        public IReadOnlyList<EntityEquipmentWeaponSlot> WeaponSlots => weaponSlots;
        public int WeaponSlotCount => weaponSlots != null ? weaponSlots.Count : 0;
        public int ActiveWeaponSlot => activeWeaponSlot;
        public bool WeaponInHand => weaponInHand;
        public int Revision => revision;

        public void OnPoolSpawned()
        {
            ResetRuntimeState();
        }

        public void OnPoolDespawned()
        {
            ResetRuntimeState();
        }

        public bool TryGetWeaponSlot(int index, out EntityEquipmentWeaponSlot slot, out string error)
        {
            if (weaponSlots == null || (uint)index >= (uint)weaponSlots.Count)
            {
                slot = null;
                error = "Weapon slot index is out of range: " + index + ".";
                return false;
            }

            slot = weaponSlots[index];
            if (slot == null || slot.weaponRoot == null)
            {
                error = "Weapon slot " + index + " has no authored weapon root.";
                return false;
            }

            EntityWeaponBinding binding = slot.runtimeBinding;
            if (binding == null || binding.transform != slot.weaponRoot)
            {
                binding = slot.weaponRoot.GetComponent<EntityWeaponBinding>();
                slot.runtimeBinding = binding;
            }
            if (binding == null)
            {
                error = "Weapon slot " + index + " has no authored EntityWeaponBinding.";
                return false;
            }
            if (!binding.ValidateReferences(out error))
                return false;

            error = null;
            return true;
        }

        public int FindNextValidWeaponIndex(int from, int direction)
        {
            int count = WeaponSlotCount;
            if (count == 0 || direction == 0)
                return -1;

            int start = from;
            if (start < 0 || start >= count)
                start = 0;
            for (int step = 1; step <= count; step++)
            {
                int index = (start + step * direction) % count;
                if (index < 0)
                    index += count;
                if (TryGetWeaponSlot(index, out _, out _))
                    return index;
            }
            return -1;
        }

        public bool TrySetActiveWeaponSlot(int index)
        {
            if (index >= 0 && !TryGetWeaponSlot(index, out _, out _))
                return false;
            if (activeWeaponSlot == index)
                return true;

            activeWeaponSlot = index;
            AdvanceRevision();
            return true;
        }

        public void SetWeaponInHand(bool value)
        {
            if (weaponInHand == value)
                return;
            weaponInHand = value;
            AdvanceRevision();
        }

        public bool TryBindItem(int index, ESInstanceHandle handle, out string error)
        {
            if (!TryGetWeaponSlot(index, out EntityEquipmentWeaponSlot slot, out error))
                return false;
            if (!ESRuntimeDataModule.ItemInstanceTable.TryGet(handle, out ESItemInstanceRecord record))
            {
                error = "Item instance handle is stale or belongs to another table.";
                return false;
            }
            if (slot.weaponKey != null && slot.weaponKey.IsConfigured)
            {
                if (!ESRuntimeDataGameCore.Weapons.TryGetRuntimeKey(
                        slot.weaponKey,
                        out int expectedDefinitionRuntimeKey))
                {
                    error = "Weapon slot " + index + " references a Weapon Key that is not injected.";
                    return false;
                }
                if (record.weaponDefinitionRuntimeKey != expectedDefinitionRuntimeKey)
                {
                    error = "Item instance Weapon projection does not match Weapon slot " + index + ".";
                    return false;
                }
            }
            if (record.location != ESItemInstanceLocation.Equipped || record.relationSlot != index)
            {
                error = "Item instance relation does not match equipment slot " + index + ".";
                return false;
            }
            if (ESRuntimeDataModule.ItemInstanceTable.IsCurrent(slot.itemHandle))
            {
                error = "Equipment slot " + index + " is already occupied.";
                return false;
            }

            slot.itemHandle = handle;
            AdvanceRevision();
            error = null;
            return true;
        }

        public bool TryGetBoundItem(int index, out ESInstanceHandle handle)
        {
            handle = default;
            if (weaponSlots == null || (uint)index >= (uint)weaponSlots.Count)
                return false;
            EntityEquipmentWeaponSlot slot = weaponSlots[index];
            if (slot == null || !ESRuntimeDataModule.ItemInstanceTable.IsCurrent(slot.itemHandle))
                return false;
            handle = slot.itemHandle;
            return true;
        }

        public bool TryUnbindItem(int index, ESInstanceHandle expectedHandle)
        {
            if (!TryGetBoundItem(index, out ESInstanceHandle current)
                || current != expectedHandle)
                return false;
            weaponSlots[index].itemHandle = default;
            AdvanceRevision();
            return true;
        }

        public EntityWeaponBinding GetBinding(EntityEquipmentWeaponSlot slot)
        {
            if (slot == null || slot.weaponRoot == null)
                return null;
            EntityWeaponBinding binding = slot.runtimeBinding;
            if (binding == null || binding.transform != slot.weaponRoot)
            {
                binding = slot.weaponRoot.GetComponent<EntityWeaponBinding>();
                slot.runtimeBinding = binding;
            }
            return binding;
        }

        private void ResetRuntimeState()
        {
            activeWeaponSlot = -1;
            weaponInHand = false;
            AdvanceRevision();
            if (weaponSlots == null)
                return;
            for (int index = 0; index < weaponSlots.Count; index++)
            {
                EntityEquipmentWeaponSlot slot = weaponSlots[index];
                if (slot == null)
                    continue;
                slot.runtimeBinding = null;
                slot.itemHandle = default;
            }
        }

        private void AdvanceRevision()
        {
            revision++;
            if (revision <= 0)
                revision = 1;
        }
    }
}
