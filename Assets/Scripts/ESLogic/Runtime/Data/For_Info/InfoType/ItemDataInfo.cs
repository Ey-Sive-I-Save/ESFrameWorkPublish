using Sirenix.OdinInspector;

namespace ES
{
    [ESCreatePath("鏁版嵁淇℃伅", "鐗╁搧鏁版嵁淇℃伅")]
    public class ItemDataInfo : SoDataInfo
    {
        [Title("鎽樿")]
        [ShowInInspector, ReadOnly, LabelText("閰嶇疆璇存槑")]
        private string EditorSummary => BuildEditorSummary();

        [Title("鍩虹")]
        [HideLabel]
        public ItemBaseConfig baseConfig = new ItemBaseConfig();

        [Title("浜や簰")]
        [HideLabel]
        public ItemInteractConfig interactConfig = new ItemInteractConfig();

        [Title("閫昏緫")]
        [HideLabel]
        public ItemLogicConfig logicConfig = new ItemLogicConfig();

        [Title("绉诲姩")]
        [ShowIf(nameof(ShowMoveConfig))]
        [HideLabel]
        public ItemMoveConfig moveConfig = new ItemMoveConfig();

        [Title("Shot")]
        [ShowIf(nameof(ShowShotConfig))]
        [HideLabel]
        public ItemShotSharedData shotShared = ItemShotSharedData.Default;

        [ShowIf(nameof(ShowShotConfig))]
        [HideLabel, InlineProperty]
        public ESShotConfigKey shotKey = new ESShotConfigKey();

        [ShowIf(nameof(ShowShotConfig))]
        [HideLabel]
        public ItemShotVariableData shotVariable = ItemShotVariableData.Default;

        [Title("Door")]
        [ShowIf(nameof(ShowDoorConfig))]
        [HideLabel]
        public ItemDoorSharedData doorShared = ItemDoorSharedData.Default;

        [ShowIf(nameof(ShowDoorConfig))]
        [HideLabel]
        public ItemDoorVariableData doorVariable = ItemDoorVariableData.Default;

        [Title("Trap / 闄烽槺")]
        [ShowIf(nameof(ShowTrapConfig))]
        [HideLabel]
        public ItemTrapSharedData trapShared = ItemTrapSharedData.Default;

        [ShowIf(nameof(ShowTrapConfig))]
        [HideLabel]
        public ItemTrapVariableData trapVariable = ItemTrapVariableData.Default;

        [Title("Weapon / 姝﹀櫒")]
        [ShowIf(nameof(ShowWeaponConfig))]
        [HideLabel]
        public ItemWeaponSharedData weaponShared = ItemWeaponSharedData.Default;

        [ShowIf(nameof(ShowWeaponConfig))]
        [HideLabel, InlineProperty]
        public ESWeaponConfigKey weaponKey = new ESWeaponConfigKey();

        [ShowIf(nameof(ShowWeaponConfig))]
        [HideLabel]
        public ItemWeaponVariableData weaponVariable = ItemWeaponVariableData.Default;

        [ShowIf(nameof(ShowWeaponConfig))]
        [HideLabel]
        public ItemWeaponConfig weaponConfig = new ItemWeaponConfig();

        [Title("Pickup")]
        [ShowIf(nameof(ShowPickupConfig))]
        [HideLabel]
        public ItemPickupSharedData pickupShared = ItemPickupSharedData.Default;

        [ShowIf(nameof(ShowPickupConfig))]
        [HideLabel]
        public ItemPickupVariableData pickupVariable = ItemPickupVariableData.Default;

        [Title("Zone / 鍖哄煙")]
        [ShowIf(nameof(ShowZoneConfig))]
        [HideLabel]
        public ItemZoneSharedData zoneShared = ItemZoneSharedData.Default;

        [ShowIf(nameof(ShowZoneConfig))]
        [HideLabel]
        public ItemZoneVariableData zoneVariable = ItemZoneVariableData.Default;

        [Title("Prop")]
        [ShowIf(nameof(ShowPropConfig))]
        [HideLabel]
        public ItemPropSharedData propShared = ItemPropSharedData.Default;

        [ShowIf(nameof(ShowPropConfig))]
        [HideLabel]
        public ItemPropVariableData propVariable = ItemPropVariableData.Default;

        private bool ShowShotConfig() => baseConfig != null && baseConfig.kind == ItemKind.Shot;
        private bool ShowDoorConfig() => baseConfig != null && baseConfig.kind == ItemKind.Door;
        private bool ShowTrapConfig() => baseConfig != null && baseConfig.kind == ItemKind.Trap;
        private bool ShowWeaponConfig() => baseConfig != null && baseConfig.kind == ItemKind.Weapon;
        private bool ShowPickupConfig() => baseConfig != null && baseConfig.kind == ItemKind.Pickup;
        private bool ShowZoneConfig() => baseConfig != null && baseConfig.kind == ItemKind.Zone;

        private bool ShowPropConfig()
        {
            return baseConfig != null
                && (baseConfig.kind == ItemKind.Prop
                    || baseConfig.kind == ItemKind.Tower
                    || baseConfig.kind == ItemKind.Platform
                    || baseConfig.kind == ItemKind.Rotator);
        }

        private bool ShowMoveConfig()
        {
            return baseConfig != null
                && (baseConfig.kind == ItemKind.Door
                    || baseConfig.kind == ItemKind.Platform
                    || baseConfig.kind == ItemKind.Rotator
                    || baseConfig.kind == ItemKind.Pickup
                    || baseConfig.kind == ItemKind.Trap
                    || baseConfig.kind == ItemKind.Zone);
        }

        private string BuildEditorSummary()
        {
            ItemKind kind = baseConfig != null ? baseConfig.kind : ItemKind.None;
            string displayName = baseConfig != null && !string.IsNullOrWhiteSpace(baseConfig.displayName)
                ? baseConfig.displayName
                : KeyName;

            switch (kind)
            {
                case ItemKind.Shot:
                    return $"{displayName}: Shot data. Configure Shot Shared/Variable here.";
                case ItemKind.Door:
                    return $"{displayName}: Door data. Configure interaction, logic and blocking rules.";
                case ItemKind.Trap:
                    return $"{displayName}: Trap data. Configure detection, cooldown and target rules.";
                case ItemKind.Weapon:
                    return $"{displayName}: Weapon data. Describes weapon logic and default shot.";
                case ItemKind.Pickup:
                    return $"{displayName}: Pickup data. Configure pickup radius, amount, owner and lifetime.";
                case ItemKind.Zone:
                    return $"{displayName}: Zone data. Configure enter, stay, exit and period checks.";
                case ItemKind.Prop:
                    return $"{displayName}: Prop data. Basic world object definition.";
                case ItemKind.Tower:
                case ItemKind.Platform:
                case ItemKind.Rotator:
                    return $"{displayName}: Legacy item subtype. Currently indexed as Prop.";
                default:
                    return "Select an Item kind. Main kinds: Shot / Door / Trap / Weapon / Pickup / Zone / Prop.";
            }
        }
    }
}
