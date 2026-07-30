using Sirenix.OdinInspector;

using UnityEngine;

namespace ES
{
    public enum ESItemDataValidationCode : byte
    {
        Valid = 0,
        MissingBusinessKey = 1,
        MissingBaseConfig = 2,
        ItemKindNotSelected = 3,
        MissingInteractConfig = 4,
        MissingLogicConfig = 5,
        MissingMoveConfig = 6,
        MissingKindData = 7,
        KindDataMismatch = 8,
        MissingSharedData = 9,
        MissingGameCoreKey = 10,
        MissingWeaponConfig = 11
    }

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

        [Title("类型专属配置")]
        [SerializeReference, HideReferenceObjectPicker]
        [HideLabel]
        public ItemKindDataBlock kindData;

        public bool EnsureActiveKindData()
        {
            bool changed = false;
            if (baseConfig == null)
            {
                baseConfig = new ItemBaseConfig();
                changed = true;
            }
            if (interactConfig == null)
            {
                interactConfig = new ItemInteractConfig();
                changed = true;
            }
            if (logicConfig == null)
            {
                logicConfig = new ItemLogicConfig();
                changed = true;
            }
            if (moveConfig == null)
            {
                moveConfig = new ItemMoveConfig();
                changed = true;
            }

            ItemKind kind = baseConfig.kind;
            if (!IsKindDataCompatible(kindData, kind))
            {
                kindData = CreateKindData(kind);
                changed = true;
            }

            return EnsureNestedData(kindData) || changed;
        }

        public ESItemDataValidationCode ValidateConfiguration(bool includeEditorMetadata = true)
        {
            if (includeEditorMetadata && string.IsNullOrWhiteSpace(KeyName))
                return ESItemDataValidationCode.MissingBusinessKey;
            if (baseConfig == null)
                return ESItemDataValidationCode.MissingBaseConfig;
            if (baseConfig.kind == ItemKind.None)
                return ESItemDataValidationCode.ItemKindNotSelected;
            if (interactConfig == null)
                return ESItemDataValidationCode.MissingInteractConfig;
            if (logicConfig == null)
                return ESItemDataValidationCode.MissingLogicConfig;
            if (ShowMoveConfig() && moveConfig == null)
                return ESItemDataValidationCode.MissingMoveConfig;
            if (kindData == null)
                return ESItemDataValidationCode.MissingKindData;
            if (!IsKindDataCompatible(kindData, baseConfig.kind))
                return ESItemDataValidationCode.KindDataMismatch;

            switch (kindData)
            {
                case ItemShotDataBlock shot:
                    if (shot.sharedData == null) return ESItemDataValidationCode.MissingSharedData;
                    if (shot.key == null || !shot.key.IsConfigured) return ESItemDataValidationCode.MissingGameCoreKey;
                    break;
                case ItemWeaponDataBlock weapon:
                    if (weapon.sharedData == null) return ESItemDataValidationCode.MissingSharedData;
                    if (weapon.key == null || !weapon.key.IsConfigured) return ESItemDataValidationCode.MissingGameCoreKey;
                    if (weapon.config == null) return ESItemDataValidationCode.MissingWeaponConfig;
                    break;
                case ItemDoorDataBlock door when door.sharedData == null:
                case ItemTrapDataBlock trap when trap.sharedData == null:
                case ItemPickupDataBlock pickup when pickup.sharedData == null:
                case ItemZoneDataBlock zone when zone.sharedData == null:
                case ItemPropDataBlock prop when prop.sharedData == null:
                    return ESItemDataValidationCode.MissingSharedData;
            }

            return ESItemDataValidationCode.Valid;
        }

        public string GetValidationMessage(ESItemDataValidationCode code)
        {
            switch (code)
            {
                case ESItemDataValidationCode.Valid: return "配置有效。";
                case ESItemDataValidationCode.MissingBusinessKey: return "缺少 Item 业务 Key（SoDataInfo.KeyName）。";
                case ESItemDataValidationCode.MissingBaseConfig: return "缺少基础配置 BaseConfig。";
                case ESItemDataValidationCode.ItemKindNotSelected: return "尚未选择 ItemKind。";
                case ESItemDataValidationCode.MissingInteractConfig: return "缺少通用交互配置。";
                case ESItemDataValidationCode.MissingLogicConfig: return "缺少通用逻辑配置。";
                case ESItemDataValidationCode.MissingMoveConfig: return "当前 ItemKind 需要移动配置。";
                case ESItemDataValidationCode.MissingKindData: return "缺少当前 ItemKind 对应的类型专属配置块。";
                case ESItemDataValidationCode.KindDataMismatch: return "ItemKind 与类型专属配置块不匹配。";
                case ESItemDataValidationCode.MissingSharedData: return "当前类型专属配置块缺少 SharedData。";
                case ESItemDataValidationCode.MissingGameCoreKey: return "Shot/Weapon 必须显式配置 EnumKey 或 StringKey；KeyName 仅供编辑器与策划使用。";
                case ESItemDataValidationCode.MissingWeaponConfig: return "Weapon 缺少武器逻辑配置。";
                default: return "未知 Item 配置错误。";
            }
        }

        public string GetGameCoreRouteName()
        {
            if (baseConfig == null) return "未配置";
            switch (baseConfig.kind)
            {
                case ItemKind.Shot: return "Item/Shot -> ESRuntimeDataGameCore.Shots";
                case ItemKind.Weapon: return "Item/Weapon -> ESRuntimeDataGameCore.Weapons";
                default: return "普通 Item（不进入 GameCore 启动表）";
            }
        }

        public bool TryGetGameCoreKey(out IESConfigKey key)
        {
            if (kindData is ItemShotDataBlock shot)
            {
                key = shot.key;
                return key != null;
            }
            if (kindData is ItemWeaponDataBlock weapon)
            {
                key = weapon.key;
                return key != null;
            }

            key = null;
            return false;
        }

        private static ItemKindDataBlock CreateKindData(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Shot: return ItemShotDataBlock.Default;
                case ItemKind.Door: return ItemDoorDataBlock.Default;
                case ItemKind.Trap: return ItemTrapDataBlock.Default;
                case ItemKind.Weapon: return ItemWeaponDataBlock.Default;
                case ItemKind.Pickup: return ItemPickupDataBlock.Default;
                case ItemKind.Zone: return ItemZoneDataBlock.Default;
                case ItemKind.Prop:
                case ItemKind.Tower:
                case ItemKind.Platform:
                case ItemKind.Rotator:
                    return ItemPropDataBlock.Default;
                default:
                    return null;
            }
        }

        private static bool EnsureNestedData(ItemKindDataBlock data)
        {
            switch (data)
            {
                case ItemShotDataBlock shot:
                {
                    bool changed = false;
                    if (shot.sharedData == null) { shot.sharedData = ItemShotSharedData.Default; changed = true; }
                    if (shot.key == null) { shot.key = new ESShotConfigKey(); changed = true; }
                    return changed;
                }
                case ItemDoorDataBlock door when door.sharedData == null:
                    door.sharedData = ItemDoorSharedData.Default;
                    return true;
                case ItemTrapDataBlock trap when trap.sharedData == null:
                    trap.sharedData = ItemTrapSharedData.Default;
                    return true;
                case ItemWeaponDataBlock weapon:
                {
                    bool changed = false;
                    if (weapon.sharedData == null) { weapon.sharedData = ItemWeaponSharedData.Default; changed = true; }
                    if (weapon.key == null) { weapon.key = new ESWeaponConfigKey(); changed = true; }
                    if (weapon.config == null) { weapon.config = new ItemWeaponConfig(); changed = true; }
                    return changed;
                }
                case ItemPickupDataBlock pickup when pickup.sharedData == null:
                    pickup.sharedData = ItemPickupSharedData.Default;
                    return true;
                case ItemZoneDataBlock zone when zone.sharedData == null:
                    zone.sharedData = ItemZoneSharedData.Default;
                    return true;
                case ItemPropDataBlock prop when prop.sharedData == null:
                    prop.sharedData = ItemPropSharedData.Default;
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsKindDataCompatible(ItemKindDataBlock data, ItemKind kind)
        {
            if (data == null)
                return kind == ItemKind.None;

            if (kind == ItemKind.Prop || kind == ItemKind.Tower || kind == ItemKind.Platform || kind == ItemKind.Rotator)
                return data is ItemPropDataBlock;

            return data.Kind == kind;
        }

        private void OnValidate()
        {
            EnsureActiveKindData();
        }

        public override void OnEditorApply()
        {
            base.OnEditorApply();
            EnsureActiveKindData();
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
            // Item Group/Pack 会按 IGameCoreSO 统一转发；条件型普通 Item 在这里安全跳过，
            // 只有 Shot/Weapon 才是实际 GameCore 根并进入强类型表。
            if (!IsGameCoreRoot)
                return;

            ESItemDataValidationCode validation = ValidateConfiguration(includeEditorMetadata: false);
            if (validation != ESItemDataValidationCode.Valid)
                throw new System.InvalidOperationException("Item GameCore 配置无效：" + name + "，" + GetValidationMessage(validation));
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
                    return $"{displayName}：飞行物配置；在类型专属块中配置 SharedData、初始状态与 Shot Key。";
                case ItemKind.Door:
                    return $"{displayName}：门配置；组合通用交互、逻辑、移动与阻挡规则。";
                case ItemKind.Trap:
                    return $"{displayName}：陷阱配置；设置检测、冷却与目标规则。";
                case ItemKind.Weapon:
                    return $"{displayName}：武器配置；设置武器逻辑、运行状态与默认飞行物。";
                case ItemKind.Pickup:
                    return $"{displayName}：拾取物配置；设置拾取半径、数量、归属与生命周期。";
                case ItemKind.Zone:
                    return $"{displayName}：持续区域配置；设置进入、停留、离开与周期检测。";
                case ItemKind.Prop:
                    return $"{displayName}：普通场景物件配置。";
                case ItemKind.Tower:
                case ItemKind.Platform:
                case ItemKind.Rotator:
                    return $"{displayName}：复用普通物件类型块，并保留独立 ItemKind 语义。";
                default:
                    return "请先选择 ItemKind；每条 ItemDataInfo 只保留一个对应的类型专属配置块。";
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
        public static ESShotConfigKeyTable Table => ESRuntimeDataGameCore.Shots;

        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null || info.baseConfig.kind != ItemKind.Shot)
                throw new System.InvalidOperationException("Shot Table 只能接收 ItemKind.Shot：" + info.name);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                ItemShotDataBlock block = info.kindData as ItemShotDataBlock;
                if (block == null)
                    throw new System.InvalidOperationException("Shot 缺少激活配置块：" + info.name);
                if (Table.TryGet(block.key, out ESShotRuntimeData existing))
                {
                    if (object.ReferenceEquals(existing.soSource, info)) return;
                    throw new System.InvalidOperationException("Shot GameCore Key 重复：" + info.name);
                }
                ESShotRuntimeData data = Table.AcquireRetained(block.key);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(block.key.EnumKeyInt, block.key.StringKey);
                    data.displayName = ESItemGameCoreDisplayName.Get(info);
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = block.sharedData;
                    data.defaultVariableData = block.initialState;
                    data.prefab = info.baseConfig.prefab;
                    int runtimeKey = Table.CommitRetained(block.key, data, debugName: info.name);
                    if (runtimeKey == 0)
                        throw new System.InvalidOperationException("Shot GameCore 注入失败：" + info.name);
                }
                catch
                {
                    Table.AbandonRetained(data);
                    throw;
                }
            }
            finally { if (ownsBuild) Table.EndBuild(); }
        }
    }

    /// <summary>Weapon 的强类型 GameCore 表入口；ItemDataInfo 是其按 ItemKind 分流的配置根。</summary>
    public static class ESWeaponGameCoreTable
    {
        public static ESWeaponConfigKeyTable Table => ESRuntimeDataGameCore.Weapons;

        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null || info.baseConfig.kind != ItemKind.Weapon)
                throw new System.InvalidOperationException("Weapon Table 只能接收 ItemKind.Weapon：" + info.name);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                ItemWeaponDataBlock block = info.kindData as ItemWeaponDataBlock;
                if (block == null)
                    throw new System.InvalidOperationException("Weapon 缺少激活配置块：" + info.name);
                if (Table.TryGet(block.key, out ESWeaponRuntimeData existing))
                {
                    if (object.ReferenceEquals(existing.soSource, info)) return;
                    throw new System.InvalidOperationException("Weapon GameCore Key 重复：" + info.name);
                }
                ESWeaponRuntimeData data = Table.AcquireRetained(block.key);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(block.key.EnumKeyInt, block.key.StringKey);
                    data.displayName = ESItemGameCoreDisplayName.Get(info);
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = block.sharedData;
                    data.defaultVariableData = block.initialState;
                    data.prefab = info.baseConfig.prefab;
                    int runtimeKey = Table.CommitRetained(block.key, data, debugName: info.name);
                    if (runtimeKey == 0)
                        throw new System.InvalidOperationException("Weapon GameCore 注入失败：" + info.name);
                }
                catch
                {
                    Table.AbandonRetained(data);
                    throw;
                }
            }
            finally { if (ownsBuild) Table.EndBuild(); }
        }
    }

    internal static class ESItemGameCoreDisplayName
    {
        public static string Get(ItemDataInfo info) => !string.IsNullOrWhiteSpace(info.baseConfig.displayName) ? info.baseConfig.displayName : info.name;
    }
}
