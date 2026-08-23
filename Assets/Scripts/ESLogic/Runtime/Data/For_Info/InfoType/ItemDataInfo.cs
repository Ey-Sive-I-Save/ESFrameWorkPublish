using Sirenix.OdinInspector;

using UnityEngine;
using System.Collections.Generic;

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
        MissingWeaponConfig = 11,
        InvalidTagDefinition = 12,
        InvalidAttributeValues = 13,
        MissingItemConfigKey = 14,
        InvalidShotConfig = 15,
        MissingShotPrefab = 16
    }

    [ESCreatePath("数据信息", "物品数据信息")]
    public class ItemDataInfo : SoDataInfo, IGameCoreSO, IConditionalGameCoreSO
    {
        [Title("摘要")]
        [ShowInInspector, ReadOnly, LabelText("配置说明")]
        private string EditorSummary => BuildEditorSummary();

        [Title("稳定身份")]
        [LabelText("Item 定义 Key")]
        public ESItemConfigKey itemKey = new ESItemConfigKey();

        [Title("出生 Tag")]
        [LabelText("出生时添加")]
        [Tooltip("Item 自身出生后持续持有的事实。Item Prefab 不重复保存此列表。")]
        public List<ESTagStableReference> tags = new List<ESTagStableReference>();

        [Title("物品属性基础值")]
        [InfoBox("属性类型、范围和 Hot/Sparse 只在 GameCore 的物品属性集中定义。这里仅填写本 Item 的基础值，不复制 Schema。")]
        [TableList(AlwaysExpanded = true)]
        public List<ESItemFloatValue> floatValues = new List<ESItemFloatValue>();

        [TableList(AlwaysExpanded = true)]
        public List<ESItemPermitValue> permitValues = new List<ESItemPermitValue>();

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
            if (tags == null)
            {
                tags = new List<ESTagStableReference>();
                changed = true;
            }
            if (itemKey == null)
            {
                itemKey = new ESItemConfigKey();
                changed = true;
            }
            if (floatValues == null)
            {
                floatValues = new List<ESItemFloatValue>();
                changed = true;
            }
            if (permitValues == null)
            {
                permitValues = new List<ESItemPermitValue>();
                changed = true;
            }
            if (baseConfig == null)
            {
                baseConfig = new ItemBaseConfig();
                changed = true;
            }
            if (baseConfig.prefabKey == null)
            {
                baseConfig.prefabKey = new ESAssetReferPrefabConfigKey();
                changed = true;
            }
            if (baseConfig.iconKey == null)
            {
                baseConfig.iconKey = new ESAssetReferSpriteConfigKey();
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
            if (itemKey == null || !itemKey.IsConfigured)
                return ESItemDataValidationCode.MissingItemConfigKey;
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
            if (!ESTagLeaseSet.TryValidateTags(tags, out _))
                return ESItemDataValidationCode.InvalidTagDefinition;
            if (!ESItemAttributeValues.TryValidate(floatValues, permitValues, out _))
                return ESItemDataValidationCode.InvalidAttributeValues;

            switch (kindData)
            {
                case ItemShotDataBlock shot:
                    if (shot.sharedData == null) return ESItemDataValidationCode.MissingSharedData;
                    if (shot.key == null || !shot.key.IsConfigured) return ESItemDataValidationCode.MissingGameCoreKey;
                    if (!shot.sharedData.ValidateDefinition(out _)) return ESItemDataValidationCode.InvalidShotConfig;
                    if (!shot.initialState.ValidateDefinition(out _)) return ESItemDataValidationCode.InvalidShotConfig;
                    if (shot.initialState.forceMustHit && !shot.sharedData.allowMustHit) return ESItemDataValidationCode.InvalidShotConfig;
                    if (!ESShotConfigKeyTable.TryValidatePrefabIdentity(baseConfig.prefabKey, out _)) return ESItemDataValidationCode.MissingShotPrefab;
                    break;
                case ItemWeaponDataBlock weapon:
                    if (weapon.sharedData == null) return ESItemDataValidationCode.MissingSharedData;
                    if (weapon.key == null || !weapon.key.IsConfigured) return ESItemDataValidationCode.MissingGameCoreKey;
                    if (!weapon.sharedData.ValidateDefinition(out _)) return ESItemDataValidationCode.MissingWeaponConfig;
                    if (!weapon.sharedData.ValidateInitialState(weapon.initialState, out _)) return ESItemDataValidationCode.MissingWeaponConfig;
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
                case ESItemDataValidationCode.MissingItemConfigKey: return "缺少正式 Item ConfigKey；KeyName 不能作为 GameCore 身份。";
                case ESItemDataValidationCode.InvalidShotConfig: return "Shot 逻辑配置无效。";
                case ESItemDataValidationCode.MissingShotPrefab: return "Shot 必须配置包含 GUID 与 GameObject 类型的完整 Prefab Key。";
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
                case ESItemDataValidationCode.InvalidTagDefinition: return "出生 Tag 存在空引用、重复引用或冲突别名。";
                case ESItemDataValidationCode.InvalidAttributeValues: return "物品属性基础值存在空 Key、重复 Key 或同一 Key 的 Float/Permit 冲突。";
                default: return "未知 Item 配置错误。";
            }
        }

        public string GetGameCoreRouteName()
        {
            if (baseConfig == null) return "未配置";
            switch (baseConfig.kind)
            {
                case ItemKind.Shot: return "Item -> Items + Shot -> Shots";
                case ItemKind.Weapon: return "Item -> Items + Weapon -> Weapons";
                default: return "Item -> ESRuntimeDataGameCore.Items";
            }
        }

        public bool TryGetItemGameCoreKey(out ESItemConfigKey key)
        {
            key = itemKey;
            return key != null && key.IsConfigured;
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

        public bool IsGameCoreRoot => baseConfig != null && baseConfig.kind != ItemKind.None;

        public void InjectGameCoreTables()
        {
            // Item Group/Pack 会按 IGameCoreSO 统一转发。每条 Item 都先进入基础 Item 表；
            // Shot/Weapon 再由同一根 SO 显式形成第二个强类型能力投影。
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

    /// <summary>
    /// A direct Item definition value: stable identity plus one base number. It deliberately does
    /// not repeat storage policy, bounds, display text or any other GameCore schema field.
    /// </summary>
    [System.Serializable]
    public struct ESItemFloatValue
    {
        [LabelText("EnumKey")]
        public ushort enumKey;

        [LabelText("StringKey")]
        public string key;

        [LabelText("基础值")]
        public float value;
    }

    /// <summary>Direct Item permission default. Schema ownership remains in GameCore.</summary>
    [System.Serializable]
    public struct ESItemPermitValue
    {
        [LabelText("EnumKey")]
        public ushort enumKey;

        [LabelText("StringKey")]
        public string key;

        [LabelText("默认许可")]
        public bool value;
    }

    /// <summary>Validation belongs to the direct Item value lists, not to a second attribute schema.</summary>
    public static class ESItemAttributeValues
    {
        public static bool TryValidate(
            List<ESItemFloatValue> floatValues,
            List<ESItemPermitValue> permitValues,
            out string error)
        {
            var enumKeys = new HashSet<ushort>();
            var stringKeys = new HashSet<string>(System.StringComparer.Ordinal);
            if (!TryTrackFloats(floatValues, enumKeys, stringKeys, out error)
                || !TryTrackPermits(permitValues, enumKeys, stringKeys, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryTrackFloats(
            List<ESItemFloatValue> values,
            HashSet<ushort> enumKeys,
            HashSet<string> stringKeys,
            out string error)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    ESItemFloatValue value = values[i];
                    if (float.IsNaN(value.value) || float.IsInfinity(value.value))
                    {
                        error = "Float[" + i + "] 的基础值必须是有限数值。";
                        return false;
                    }
                    if (!TryTrack(value.enumKey, value.key, "Float[" + i + "]", enumKeys, stringKeys, out error))
                        return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryTrackPermits(
            List<ESItemPermitValue> values,
            HashSet<ushort> enumKeys,
            HashSet<string> stringKeys,
            out string error)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    ESItemPermitValue value = values[i];
                    if (!TryTrack(value.enumKey, value.key, "Permit[" + i + "]", enumKeys, stringKeys, out error))
                        return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryTrack(
            ushort enumKey,
            string key,
            string label,
            HashSet<ushort> enumKeys,
            HashSet<string> stringKeys,
            out string error)
        {
            if (enumKey == 0 && string.IsNullOrEmpty(key))
            {
                error = label + " 缺少 EnumKey/StringKey。";
                return false;
            }
            if (enumKey != 0 && !enumKeys.Add(enumKey))
            {
                error = label + " 的 EnumKey 重复：" + enumKey + "。";
                return false;
            }
            if (!string.IsNullOrEmpty(key) && !stringKeys.Add(key))
            {
                error = label + " 的 StringKey 重复：" + key + "。";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Item 根 SO 的显式双投影入口：所有 Item 写入基础 Item 表，
    /// Shot/Weapon 再写入各自能力表。跨表 RuntimeKey 永不互相解释。
    /// </summary>
    public static class ESItemGameCoreTable
    {
        public static ESItemConfigKeyTable Table => ESRuntimeDataGameCore.Items;

        public static void Inject(ItemDataInfo info)
        {
            if (info == null) throw new System.ArgumentNullException(nameof(info));
            if (info.baseConfig == null) throw new System.InvalidOperationException("Item 缺少 BaseConfig：" + info.name);

            ESItemConfigKey itemKey = info.itemKey;
            if (itemKey == null || !itemKey.IsConfigured)
                throw new System.InvalidOperationException("Item 缺少正式 Item ConfigKey：" + info.name);

            bool hasShotProjection = info.baseConfig.kind == ItemKind.Shot;
            bool hasWeaponProjection = info.baseConfig.kind == ItemKind.Weapon;
            bool ownsItemBuild = !Table.IsBuilding;
            bool ownsProjectionBuild = hasShotProjection
                ? !ESShotGameCoreTable.Table.IsBuilding
                : hasWeaponProjection && !ESWeaponGameCoreTable.Table.IsBuilding;

            if (ownsItemBuild)
                Table.BeginBuild();
            if (hasShotProjection && ownsProjectionBuild)
                ESShotGameCoreTable.Table.BeginBuild();
            else if (hasWeaponProjection && ownsProjectionBuild)
                ESWeaponGameCoreTable.Table.BeginBuild();

            ESItemRuntimeData preparedItem = null;
            int committedItemRuntimeKey = 0;
            try
            {
                bool itemAlreadyReady = Table.TryGet(itemKey, out ESItemRuntimeData existingItem);
                if (itemAlreadyReady && !object.ReferenceEquals(existingItem.soSource, info))
                    throw new System.InvalidOperationException("Item GameCore Key 重复：" + info.name);

                ValidateProjectionOwner(info);

                if (!itemAlreadyReady)
                {
                    preparedItem = Table.AcquireRetained(itemKey);
                    ESItemConfigKeyTable.PrepareFromInfo(preparedItem, info);
                    committedItemRuntimeKey = Table.CommitRetained(itemKey, preparedItem, debugName: info.name);
                }

                if (hasShotProjection)
                    ESShotGameCoreTable.Inject(info);
                else if (hasWeaponProjection)
                    ESWeaponGameCoreTable.Inject(info);
            }
            catch (System.Exception exception)
            {
                if (committedItemRuntimeKey != 0 && !Table.Remove(committedItemRuntimeKey))
                {
                    throw new System.AggregateException(
                        "Item 双投影提交失败，且基础 Item 投影回滚不完整：" + info.name,
                        exception,
                        new System.InvalidOperationException("无法移除本轮 Item RuntimeData。"));
                }

                Table.AbandonRetained(preparedItem);
                throw;
            }
            finally
            {
                if (hasWeaponProjection && ownsProjectionBuild)
                    ESWeaponGameCoreTable.Table.EndBuild();
                else if (hasShotProjection && ownsProjectionBuild)
                    ESShotGameCoreTable.Table.EndBuild();
                if (ownsItemBuild)
                    Table.EndBuild();
            }
        }

        private static void ValidateProjectionOwner(ItemDataInfo info)
        {
            if (info.kindData is ItemShotDataBlock shot)
            {
                if (shot.key == null || !shot.key.IsConfigured || shot.sharedData == null)
                    throw new System.InvalidOperationException("Shot 投影配置不完整：" + info.name);
                if (!shot.sharedData.ValidateDefinition(out string shotValidationError))
                    throw new System.InvalidOperationException("ShotDefinition 校验失败：" + shotValidationError + " | " + info.name);
                if (!shot.initialState.ValidateDefinition(out string variableValidationError))
                    throw new System.InvalidOperationException("ShotVariable 校验失败：" + variableValidationError + " | " + info.name);
                if (shot.initialState.forceMustHit && !shot.sharedData.allowMustHit)
                    throw new System.InvalidOperationException("ShotVariable 要求必中，但 ShotDefinition 禁止必中：" + info.name);
                ESAssetReferPrefabConfigKey prefabKey = info.baseConfig != null ? info.baseConfig.prefabKey : null;
                if (!ESShotConfigKeyTable.TryValidatePrefabIdentity(prefabKey, out string prefabIdentityError))
                    throw new System.InvalidOperationException(prefabIdentityError + " | " + info.name);
                if (ESShotGameCoreTable.Table.TryGet(shot.key, out ESShotRuntimeData existing)
                    && !object.ReferenceEquals(existing.soSource, info))
                {
                    throw new System.InvalidOperationException("Shot GameCore Key 重复：" + info.name);
                }
                return;
            }

            if (info.kindData is ItemWeaponDataBlock weapon)
            {
                if (weapon.key == null || !weapon.key.IsConfigured || weapon.sharedData == null)
                    throw new System.InvalidOperationException("Weapon 投影配置不完整：" + info.name);
                if (!weapon.sharedData.ValidateDefinition(out string validationError))
                    throw new System.InvalidOperationException("WeaponDefinition 校验失败：" + validationError + " | " + info.name);
                if (!weapon.sharedData.ValidateInitialState(weapon.initialState, out string stateValidationError))
                    throw new System.InvalidOperationException("WeaponVariable 校验失败：" + stateValidationError + " | " + info.name);
                ESAssetReferPrefabConfigKey prefabKey = info.baseConfig != null ? info.baseConfig.prefabKey : null;
                if (!ESWeaponConfigKeyTable.TryValidatePrefabIdentity(prefabKey, out string prefabIdentityError))
                    throw new System.InvalidOperationException(prefabIdentityError + " | " + info.name);
                if (ESWeaponGameCoreTable.Table.TryGet(weapon.key, out ESWeaponRuntimeData existing)
                    && !object.ReferenceEquals(existing.soSource, info))
                {
                    throw new System.InvalidOperationException("Weapon GameCore Key 重复：" + info.name);
                }
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
                if (block.key == null || !block.key.IsConfigured)
                    throw new System.InvalidOperationException("Shot 缺少有效 ConfigKey：" + info.name);
                if (block.sharedData == null)
                    throw new System.InvalidOperationException("ShotDefinition 缺少 SharedData：" + info.name);
                if (!block.sharedData.ValidateDefinition(out string validationError))
                    throw new System.InvalidOperationException("ShotDefinition 校验失败：" + validationError + " | " + info.name);
                if (!block.initialState.ValidateDefinition(out string variableValidationError))
                    throw new System.InvalidOperationException("ShotVariable 校验失败：" + variableValidationError + " | " + info.name);
                if (block.initialState.forceMustHit && !block.sharedData.allowMustHit)
                    throw new System.InvalidOperationException("ShotVariable 要求必中，但 ShotDefinition 禁止必中：" + info.name);
                if (!ESShotConfigKeyTable.TryValidatePrefabIdentity(info.baseConfig.prefabKey, out string prefabIdentityError))
                    throw new System.InvalidOperationException(prefabIdentityError + " | " + info.name);
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
                    data.prefabKey = info.baseConfig.prefabKey;
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
                    if (block.sharedData == null)
                        throw new System.InvalidOperationException("WeaponDefinition 缺少 SharedData | " + info.name);
                    if (!block.sharedData.ValidateDefinition(out string validationError))
                        throw new System.InvalidOperationException("WeaponDefinition 校验失败：" + validationError + " | " + info.name);
                    if (!block.sharedData.ValidateInitialState(block.initialState, out string stateValidationError))
                        throw new System.InvalidOperationException("WeaponVariable 校验失败：" + stateValidationError + " | " + info.name);

                    data.keyName = ESConfigKeyMatch.Describe(block.key.EnumKeyInt, block.key.StringKey);
                    data.displayName = ESItemGameCoreDisplayName.Get(info);
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = block.sharedData;
                    data.defaultVariableData = block.initialState;
                    data.prefabKey = info.baseConfig.prefabKey;
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
