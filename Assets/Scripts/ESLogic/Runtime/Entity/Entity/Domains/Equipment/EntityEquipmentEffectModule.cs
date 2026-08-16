using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("装备效果模块")]
    public sealed class EntityEquipmentEffectModule : EntityEquipmentModuleBase
    {
        [NonSerialized] private readonly ESTagLeaseSet equippedTagLeases = new ESTagLeaseSet();

        [ShowInInspector, ReadOnly, LabelText("装备 Tag Lease 数")]
        public int ActiveTagLeaseCount => equippedTagLeases.Count;

        public bool TryApplyWeaponEffects(EntityWeaponBinding binding, object source, out string error)
        {
            if (MyCore == null)
            {
                error = "Equipment effect module has no Entity owner.";
                return false;
            }

            // ESTagLeaseSet stages and validates the replacement before releasing the
            // current set. Passing an empty list intentionally unequips all Tag effects.
            return equippedTagLeases.TryApply(
                MyCore.Tags,
                binding != null ? binding.equippedTags : null,
                source ?? this,
                out error);
        }

        public void ReleaseAllEffects()
        {
            equippedTagLeases.ReleaseAll();
        }

        public void OnPoolSpawned()
        {
            ReleaseAllEffects();
        }

        public void OnPoolDespawned()
        {
            ReleaseAllEffects();
        }
    }
}
