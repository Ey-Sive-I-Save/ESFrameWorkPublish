using Sirenix.OdinInspector;

namespace ES
{
    [ESCreatePath("数据信息", "物品数据信息")]
    public class ItemDataInfo : SoDataInfo, IGameCoreSO, IConditionalGameCoreSO
    {
        [Title("摘要")]
        [ShowInInspector, ReadOnly, LabelText("配置说明")]
        private string EditorSummary => BuildEditorSummary();

        [Title("基础")]
        [HideLabel]
        public ItemBaseConfig baseConfig = new ItemBaseConfig();

        [Title("交互")]
        [HideLabel]
        public ItemInteractConfig interactConfig = new ItemInteractConfig();

        [Title("逻辑")]
        [HideLabel]
        public ItemLogicConfig logicConfig = new ItemLogicConfig();

        [Title("移动")]
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

        [Title("Trap / 陷阱")]
        [ShowIf(nameof(ShowTrapConfig))]
        [HideLabel]
        public ItemTrapSharedData trapShared = ItemTrapSharedData.Default;

        [ShowIf(nameof(ShowTrapConfig))]
        [HideLabel]
        public ItemTrapVariableData trapVariable = ItemTrapVariableData.Default;

        [Title("Weapon / 武器")]
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

        [Title("Zone / 区域")]
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

        public bool IsGameCoreRoot => baseConfig != null && (baseConfig.kind == ItemKind.Shot || baseConfig.kind == ItemKind.Weapon);

        public void InjectGameCoreTables()
        {
            ESItemGameCoreTable.Inject(this);
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

    /// <summary>Item 的枚举分流注册入口；Shot/Weapon 分别写入各自强类型表。</summary>
    public static class ESItemGameCoreTable
    {
        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null) throw new System.InvalidOperationException("Item 缺少 BaseConfig：" + info.name);

            switch (info.baseConfig.kind)
            {
                case ItemKind.Shot: ESShotGameCoreTable.Inject(info); return;
                case ItemKind.Weapon: ESWeaponGameCoreTable.Inject(info); return;
                default: throw new System.InvalidOperationException("非 GameCore Item 不得注入：" + info.name + " (" + info.baseConfig.kind + ")");
            }
        }
    }

    /// <summary>Shot 的强类型 GameCore 表入口；ItemDataInfo 是其按 ItemKind 分流的配置根。</summary>
    public static class ESShotGameCoreTable
    {
        public static ESConfigKeyTable<ESShotRuntimeData> Table => ESRuntimeDataGameCore.Shots;

        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null || info.baseConfig.kind != ItemKind.Shot)
                throw new System.InvalidOperationException("Shot Table 只能接收 ItemKind.Shot：" + info.name);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.shotShared ??= ItemShotSharedData.Default;
                info.shotKey ??= new ESShotConfigKey();
                if (Table.TryGet(info.shotKey, out ESShotRuntimeData existing))
                {
                    if (object.ReferenceEquals(existing.soSource, info)) return;
                    throw new System.InvalidOperationException("Shot GameCore Key 重复：" + info.KeyName);
                }
                var data = new ESShotRuntimeData
                {
                    keyName = info.KeyName, displayName = ESItemGameCoreDisplayName.Get(info), sourcePackage = info.name,
                    soSource = info, sharedData = info.shotShared, defaultVariableData = info.shotVariable,
                    prefab = info.baseConfig.prefab
                };
                data.runtimeKey = Table.Bake(info.shotKey, info.KeyName);
                if (!Table.Upsert(info.shotKey, data, info.KeyName))
                    throw new System.InvalidOperationException("Shot GameCore 注入失败：" + info.KeyName);
            }
            finally { if (ownsBuild) Table.EndBuild(); }
        }
    }

    /// <summary>Weapon 的强类型 GameCore 表入口；ItemDataInfo 是其按 ItemKind 分流的配置根。</summary>
    public static class ESWeaponGameCoreTable
    {
        public static ESConfigKeyTable<ESWeaponRuntimeData> Table => ESRuntimeDataGameCore.Weapons;

        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null || info.baseConfig.kind != ItemKind.Weapon)
                throw new System.InvalidOperationException("Weapon Table 只能接收 ItemKind.Weapon：" + info.name);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.weaponShared ??= ItemWeaponSharedData.Default;
                info.weaponKey ??= new ESWeaponConfigKey();
                if (Table.TryGet(info.weaponKey, out ESWeaponRuntimeData existing))
                {
                    if (object.ReferenceEquals(existing.soSource, info)) return;
                    throw new System.InvalidOperationException("Weapon GameCore Key 重复：" + info.KeyName);
                }
                var data = new ESWeaponRuntimeData
                {
                    keyName = info.KeyName, displayName = ESItemGameCoreDisplayName.Get(info), sourcePackage = info.name,
                    soSource = info, sharedData = info.weaponShared, defaultVariableData = info.weaponVariable,
                    prefab = info.baseConfig.prefab
                };
                data.runtimeKey = Table.Bake(info.weaponKey, info.KeyName);
                if (!Table.Upsert(info.weaponKey, data, info.KeyName))
                    throw new System.InvalidOperationException("Weapon GameCore 注入失败：" + info.KeyName);
            }
            finally { if (ownsBuild) Table.EndBuild(); }
        }
    }

    internal static class ESItemGameCoreDisplayName
    {
        public static string Get(ItemDataInfo info) => !string.IsNullOrWhiteSpace(info.baseConfig.displayName) ? info.baseConfig.displayName : info.KeyName;
    }
}
