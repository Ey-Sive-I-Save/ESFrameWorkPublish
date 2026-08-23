using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("装备域模块基类")]
    public abstract class EntityEquipmentModuleBase : Module<Entity, EntityEquipmentDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("装备域")]
    public sealed class EntityEquipmentDomain : Domain<Entity, EntityEquipmentModuleBase>
    {
        [NonSerialized] private StateMachine subscribedStateMachine;
        [NonSerialized] private bool configuredWeaponItemsReady;
        [NonSerialized] private string configuredWeaponPreparationError;
        [NonSerialized] private float nextConfiguredWeaponPreparationAt;
        private const float ConfiguredWeaponPreparationRetryInterval = 0.5f;

        public bool ConfiguredWeaponItemsReady => configuredWeaponItemsReady;
        public string ConfiguredWeaponPreparationError => configuredWeaponPreparationError ?? string.Empty;

        [field: NonSerialized]
        public event Action<
            EntityEquipmentTransitionToken,
            EntityEquipmentTransitionPhase,
            EntityEquipmentTransitionSignal> AttachmentTransitionChanged;

        public EntityEquipmentAttachmentModule Attachment =>
            FindMyModule<EntityEquipmentAttachmentModule>();

        public EntityEquipmentInventoryModule Inventory =>
            FindMyModule<EntityEquipmentInventoryModule>();

        public EntityEquipmentSlotModule Slots =>
            FindMyModule<EntityEquipmentSlotModule>();

        public EntityEquipmentEffectModule Effects =>
            FindMyModule<EntityEquipmentEffectModule>();

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            BindAnimationEvents();
        }

        protected override void Update()
        {
            base.Update();
            if (!configuredWeaponItemsReady
                && Time.unscaledTime >= nextConfiguredWeaponPreparationAt)
            {
                configuredWeaponItemsReady = TryPrepareConfiguredWeaponItems(
                    out configuredWeaponPreparationError);
                if (!configuredWeaponItemsReady)
                    nextConfiguredWeaponPreparationAt = Time.unscaledTime
                        + ConfiguredWeaponPreparationRetryInterval;
            }
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment != null
                && attachment.TryAbortInvalidOrExpired(
                    out EntityEquipmentTransitionToken token,
                    out EntityEquipmentTransitionPhase phase,
                    out _))
            {
                PublishTransitionChanged(
                    token,
                    phase,
                    EntityEquipmentTransitionSignal.Cancelled);
            }
        }

        public void NotifyPoolSpawned()
        {
            BindAnimationEvents();
            Inventory?.OnPoolSpawned();
            Slots?.OnPoolSpawned();
            Effects?.OnPoolSpawned();
            Attachment?.OnPoolSpawned();
            configuredWeaponItemsReady = TryPrepareConfiguredWeaponItems(
                out configuredWeaponPreparationError);
            nextConfiguredWeaponPreparationAt = configuredWeaponItemsReady
                ? 0f
                : Time.unscaledTime + ConfiguredWeaponPreparationRetryInterval;
        }

        public void NotifyPoolDespawned()
        {
            Attachment?.OnPoolDespawned();
            Effects?.OnPoolDespawned();
            Slots?.OnPoolDespawned();
            Inventory?.OnPoolDespawned();
            configuredWeaponItemsReady = false;
            configuredWeaponPreparationError = null;
            nextConfiguredWeaponPreparationAt = 0f;
            UnbindAnimationEvents();
        }

        public bool TryPrepareConfiguredWeaponItems(out string error)
        {
            EntityEquipmentSlotModule slots = Slots;
            if (slots == null || slots.WeaponSlotCount == 0)
            {
                error = null;
                return true;
            }

            for (int index = 0; index < slots.WeaponSlotCount; index++)
            {
                if (slots.TryGetBoundItem(index, out _))
                    continue;
                if (!slots.TryGetWeaponSlotDefinition(index, out _, out error))
                    return false;
                if (!TryCreateAndEquipWeapon(index, out _, out error))
                    return false;
            }

            error = null;
            return true;
        }

        protected override void OnDestroy()
        {
            NotifyPoolDespawned();
            AttachmentTransitionChanged = null;
            base.OnDestroy();
        }

        public bool TryApplyEquippedWeaponEffects(
            EntityWeaponBinding binding,
            object source,
            out string error)
        {
            EntityEquipmentEffectModule effects = Effects;
            if (effects == null)
            {
                error = "EntityEquipmentEffectModule is missing.";
                return false;
            }
            return effects.TryApplyWeaponEffects(binding, source, out error);
        }

        public void ReleaseEquippedEffects()
        {
            Effects?.ReleaseAllEffects();
        }

        public bool TryEquipInventoryItem(
            int inventorySlot,
            int equipmentSlot,
            out ESInstanceHandle handle,
            out string error)
        {
            handle = default;
            EntityEquipmentInventoryModule inventory = Inventory;
            EntityEquipmentSlotModule slots = Slots;
            if (inventory == null || slots == null)
            {
                error = inventory == null
                    ? "EntityEquipmentInventoryModule is missing."
                    : "EntityEquipmentSlotModule is missing.";
                return false;
            }
            if (Attachment != null && Attachment.HasActiveTransition)
            {
                error = "Equipment relations cannot change during an active attachment transition.";
                return false;
            }
            if (slots.TryGetBoundItem(equipmentSlot, out _))
            {
                error = "Equipment slot " + equipmentSlot + " is already occupied.";
                return false;
            }
            if (!slots.TryEnsureRuntimeWeaponView(
                    equipmentSlot,
                    out bool acquiredView,
                    out error))
                return false;
            if (!inventory.TryEquipItem(inventorySlot, equipmentSlot, out handle))
            {
                if (acquiredView)
                    slots.Internal_ReleaseRuntimeWeaponView(equipmentSlot);
                error = "Inventory item cannot enter equipment slot " + equipmentSlot + ".";
                return false;
            }
            if (slots.TryBindItem(equipmentSlot, handle, out error))
                return true;

            if (!inventory.TryStoreItemAt(handle, inventorySlot))
                error += " Rollback failed; item relation requires immediate repair.";
            if (acquiredView)
                slots.Internal_ReleaseRuntimeWeaponView(equipmentSlot);
            handle = default;
            return false;
        }

        public bool TryCreateAndEquipWeapon(
            int equipmentSlot,
            out ESInstanceHandle handle,
            out string error)
        {
            handle = default;
            EntityEquipmentInventoryModule inventory = Inventory;
            EntityEquipmentSlotModule slots = Slots;
            if (inventory == null || slots == null)
            {
                error = inventory == null
                    ? "EntityEquipmentInventoryModule is missing."
                    : "EntityEquipmentSlotModule is missing.";
                return false;
            }
            if (!slots.TryGetWeaponSlotDefinition(
                    equipmentSlot,
                    out EntityEquipmentWeaponSlot slot,
                    out error))
                return false;
            if (!ESRuntimeDataGameCore.Weapons.TryGet(
                    slot.weaponKey,
                    out ESWeaponRuntimeData weaponData)
                || weaponData == null
                || !weaponData.Ready
                || weaponData.PreparedSharedData == null
                || !weaponData.TryGetPreparedItemRuntimeKey(out int itemRuntimeKey))
            {
                error = "Weapon slot cannot resolve its prepared Item projection.";
                return false;
            }

            var request = new ESItemInstanceCreateRequest(
                itemRuntimeKey,
                inventory.OwnerId,
                weaponData.runtimeKey,
                1,
                ESItemInstanceLocation.Inventory);
            if (!inventory.TryCreateItem(request, out handle, out int inventorySlot))
            {
                error = "Inventory cannot create the Weapon Item instance.";
                return false;
            }
            if (TryEquipInventoryItem(inventorySlot, equipmentSlot, out handle, out error))
                return true;

            inventory.TryRemoveItem(inventorySlot, out _);
            handle = default;
            return false;
        }

        public bool TryUnequipItem(
            int equipmentSlot,
            out ESInstanceHandle handle,
            out int inventorySlot,
            out string error)
        {
            handle = default;
            inventorySlot = -1;
            EntityEquipmentInventoryModule inventory = Inventory;
            EntityEquipmentSlotModule slots = Slots;
            if (inventory == null || slots == null)
            {
                error = inventory == null
                    ? "EntityEquipmentInventoryModule is missing."
                    : "EntityEquipmentSlotModule is missing.";
                return false;
            }
            if (Attachment != null && Attachment.HasActiveTransition)
            {
                error = "Equipment relations cannot change during an active attachment transition.";
                return false;
            }
            if (!slots.TryGetBoundItem(equipmentSlot, out handle))
            {
                error = "Equipment slot " + equipmentSlot + " has no current item instance.";
                return false;
            }
            if (!inventory.TryStoreItem(handle, out inventorySlot))
            {
                error = "Inventory has no capacity for the unequipped item.";
                handle = default;
                return false;
            }
            if (slots.TryUnbindItem(equipmentSlot, handle))
            {
                error = null;
                return true;
            }

            if (!inventory.TryEquipItem(
                    inventorySlot,
                    equipmentSlot,
                    out ESInstanceHandle rollbackHandle)
                || rollbackHandle != handle)
            {
                error = "Equipment relation rollback failed and requires immediate repair.";
                return false;
            }

            error = "Equipment slot changed during unequip; transaction was rolled back.";
            handle = default;
            inventorySlot = -1;
            return false;
        }

        public bool TryPrepareAttachmentTransition(
            in EntityEquipmentTransitionRequest request,
            out EntityEquipmentTransitionToken token,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                token = default;
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            return attachment.TryPrepare(request, out token, out error);
        }

        public bool TryApplyInitialAttachmentPose(
            in EntityEquipmentAttachmentOperation operation,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            return attachment.TryApplyInitialPose(operation, out error);
        }

        public bool TryBindAnimationState(
            in EntityEquipmentTransitionToken token,
            StateBase state,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            return attachment.TryBindAnimationState(token, state, out error);
        }

        public bool TryCommitAttachment(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            EntityEquipmentTransitionPhase phase = attachment.TransitionPhase;
            if (!attachment.TryCommit(token, out error))
                return false;

            PublishTransitionChanged(token, phase, EntityEquipmentTransitionSignal.Committed);
            return true;
        }

        public bool TryCompleteAttachment(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            EntityEquipmentTransitionPhase phase = attachment.TransitionPhase;
            if (!attachment.TryComplete(token, out error))
                return false;

            PublishTransitionChanged(token, phase, EntityEquipmentTransitionSignal.Completed);
            return true;
        }

        public bool TryCancelAttachment(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
            {
                error = "EntityEquipmentAttachmentModule is missing.";
                return false;
            }

            EntityEquipmentTransitionPhase phase = attachment.TransitionPhase;
            if (!attachment.TryCancel(token, out error))
                return false;

            PublishTransitionChanged(token, phase, EntityEquipmentTransitionSignal.Cancelled);
            return true;
        }

        private void BindAnimationEvents()
        {
            StateMachine target = MyCore != null && MyCore.stateDomain != null
                ? MyCore.stateDomain.stateMachine
                : null;
            if (ReferenceEquals(subscribedStateMachine, target))
                return;

            UnbindAnimationEvents();
            subscribedStateMachine = target;
            if (subscribedStateMachine != null)
                subscribedStateMachine.AnimationEvent += OnAnimationEvent;
        }

        private void UnbindAnimationEvents()
        {
            if (subscribedStateMachine != null)
                subscribedStateMachine.AnimationEvent -= OnAnimationEvent;
            subscribedStateMachine = null;
        }

        private void OnAnimationEvent(StateBase state, string eventName, string eventParam)
        {
            EntityEquipmentAttachmentModule attachment = Attachment;
            if (attachment == null)
                return;

            if (attachment.TryHandleAnimationEvent(
                    state,
                    eventName,
                    out EntityEquipmentTransitionSignal signal,
                    out EntityEquipmentTransitionToken token,
                    out EntityEquipmentTransitionPhase phase,
                    out _))
            {
                PublishTransitionChanged(token, phase, signal);
            }
        }

        private void PublishTransitionChanged(
            in EntityEquipmentTransitionToken token,
            EntityEquipmentTransitionPhase phase,
            EntityEquipmentTransitionSignal signal)
        {
            AttachmentTransitionChanged?.Invoke(token, phase, signal);
        }
    }
}
