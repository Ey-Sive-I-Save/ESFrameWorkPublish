using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ItemDoorSharedData
    {
        [LabelText("默认开启")]
        public bool defaultOpen;

        [LabelText("可锁")]
        public bool canLock;

        [LabelText("关闭时阻挡")]
        public bool blocksWhenClosed;

        [LabelText("交互距离")]
        public float interactDistance;

        [LabelText("开关耗时")]
        public float moveDuration;

        [LabelText("逻辑标签")]
        public string logicTag;

        public static ItemDoorSharedData Default => new ItemDoorSharedData
        {
            defaultOpen = false,
            canLock = false,
            blocksWhenClosed = true,
            interactDistance = 2f,
            moveDuration = 0.3f,
            logicTag = string.Empty
        };
    }

    [Serializable]
    public struct ItemDoorVariableData
    {
        [LabelText("当前开启")]
        public bool isOpen;

        [LabelText("当前上锁")]
        public bool isLocked;

        [LabelText("临时禁用时间")]
        public float disabledTime;

        public static ItemDoorVariableData Default => new ItemDoorVariableData
        {
            isOpen = false,
            isLocked = false,
            disabledTime = 0f
        };
    }

    [Serializable]
    public sealed class ItemTrapSharedData
    {
        [LabelText("启用")]
        public bool enabled;

        [LabelText("一次性")]
        public bool oneShot;

        [LabelText("检测半径")]
        public float radius;

        [LabelText("检测间隔")]
        public float checkInterval;

        [LabelText("冷却")]
        public float cooldown;

        [LabelText("目标层")]
        public LayerMask targetLayers;

        public static ItemTrapSharedData Default => new ItemTrapSharedData
        {
            enabled = true,
            oneShot = false,
            radius = 1.5f,
            checkInterval = 0.1f,
            cooldown = 0.5f,
            targetLayers = ~0
        };
    }

    [Serializable]
    public struct ItemTrapVariableData
    {
        [LabelText("激活")]
        public bool active;

        [LabelText("剩余冷却")]
        public float cooldownLeft;

        [LabelText("已触发次数")]
        public int triggerCount;

        [LabelText("逻辑随机种子")]
        public int logicSeed;

        public static ItemTrapVariableData Default => new ItemTrapVariableData
        {
            active = true,
            cooldownLeft = 0f,
            triggerCount = 0,
            logicSeed = 0
        };
    }

    [Serializable]
    public sealed class ItemWeaponSharedData
    {
        [LabelText("武器类型")]
        public ItemWeaponKind weaponKind;

        [LabelText("默认飞行物Key")]
        public string defaultShotKey;

        [LabelText("攻击检测半径")]
        public float hitRadius;

        [LabelText("默认冷却")]
        public float cooldown;

        [LabelText("挂点名")]
        public string socketName;

        public static ItemWeaponSharedData Default => new ItemWeaponSharedData
        {
            weaponKind = ItemWeaponKind.None,
            defaultShotKey = string.Empty,
            hitRadius = 0.2f,
            cooldown = 0.2f,
            socketName = string.Empty
        };

        /// <summary>把 Table 自有的运行时默认对象原位恢复为领域默认值，不产生新对象。</summary>
        internal void ResetToDefaults()
        {
            weaponKind = ItemWeaponKind.None;
            defaultShotKey = string.Empty;
            hitRadius = 0.2f;
            cooldown = 0.2f;
            socketName = string.Empty;
        }
    }

    [Serializable]
    public struct ItemWeaponVariableData
    {
        [LabelText("当前耐久")]
        public float durability;

        [LabelText("剩余冷却")]
        public float cooldownLeft;

        [LabelText("当前装填")]
        public int ammo;

        [LabelText("逻辑随机种子")]
        public int logicSeed;

        public static ItemWeaponVariableData Default => new ItemWeaponVariableData
        {
            durability = 1f,
            cooldownLeft = 0f,
            ammo = 0,
            logicSeed = 0
        };
    }

    [Serializable]
    public sealed class ItemPickupSharedData
    {
        [LabelText("拾取半径")]
        public float radius;

        [LabelText("自动拾取")]
        public bool autoPickup;

        [LabelText("存在时间")]
        public float lifeTime;

        [LabelText("堆叠上限")]
        public int maxStack;

        public static ItemPickupSharedData Default => new ItemPickupSharedData
        {
            radius = 1.2f,
            autoPickup = false,
            lifeTime = 0f,
            maxStack = 1
        };
    }

    [Serializable]
    public struct ItemPickupVariableData
    {
        [LabelText("数量")]
        public int count;

        [LabelText("剩余时间")]
        public float lifeLeft;

        [LabelText("已被预定拾取")]
        public bool reserved;

        public static ItemPickupVariableData Default => new ItemPickupVariableData
        {
            count = 1,
            lifeLeft = 0f,
            reserved = false
        };
    }

    [Serializable]
    public sealed class ItemZoneSharedData
    {
        [LabelText("启用")]
        public bool enabled;

        [LabelText("区域半径")]
        public float radius;

        [LabelText("检测间隔")]
        public float checkInterval;

        [LabelText("持续时间")]
        public float duration;

        [LabelText("目标层")]
        public LayerMask targetLayers;

        public static ItemZoneSharedData Default => new ItemZoneSharedData
        {
            enabled = true,
            radius = 3f,
            checkInterval = 0.2f,
            duration = 0f,
            targetLayers = ~0
        };
    }

    [Serializable]
    public struct ItemZoneVariableData
    {
        [LabelText("激活")]
        public bool active;

        [LabelText("剩余时间")]
        public float durationLeft;

        [LabelText("逻辑随机种子")]
        public int logicSeed;

        public static ItemZoneVariableData Default => new ItemZoneVariableData
        {
            active = true,
            durationLeft = 0f,
            logicSeed = 0
        };
    }

    [Serializable]
    public sealed class ItemPropSharedData
    {
        [LabelText("可交互")]
        public bool canInteract;

        [LabelText("可破坏")]
        public bool breakable;

        [LabelText("阻挡")]
        public bool blocks;

        [LabelText("最大耐久")]
        public float maxDurability;

        [LabelText("逻辑标签")]
        public string logicTag;

        public static ItemPropSharedData Default => new ItemPropSharedData
        {
            canInteract = false,
            breakable = false,
            blocks = true,
            maxDurability = 1f,
            logicTag = string.Empty
        };
    }

    [Serializable]
    public struct ItemPropVariableData
    {
        [LabelText("当前耐久")]
        public float durability;

        [LabelText("当前状态")]
        public int state;

        [LabelText("临时禁用时间")]
        public float disabledTime;

        public static ItemPropVariableData Default => new ItemPropVariableData
        {
            durability = 1f,
            state = 0,
            disabledTime = 0f
        };
    }

    /// <summary>
    /// ItemDataInfo 只实例化当前 ItemKind 对应的数据块，避免每个条目同时分配全部类型配置。
    /// 这些是 ItemDataInfo 内部配置块，不是独立 DataInfo，也不会建立独立 GameCore 表。
    /// </summary>
    [Serializable]
    public abstract class ItemKindDataBlock
    {
        public abstract ItemKind Kind { get; }
    }

    [Serializable]
    public sealed class ItemShotDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Shot;
        [HideLabel] public ItemShotSharedData sharedData = ItemShotSharedData.Default;
        [HideLabel, InlineProperty] public ESShotConfigKey key = new ESShotConfigKey();
        [HideLabel] public ItemShotVariableData initialState = ItemShotVariableData.Default;

        public static ItemShotDataBlock Default => new ItemShotDataBlock();
    }

    [Serializable]
    public sealed class ItemDoorDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Door;
        [HideLabel] public ItemDoorSharedData sharedData = ItemDoorSharedData.Default;
        [HideLabel] public ItemDoorVariableData initialState = ItemDoorVariableData.Default;

        public static ItemDoorDataBlock Default => new ItemDoorDataBlock();
    }

    [Serializable]
    public sealed class ItemTrapDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Trap;
        [HideLabel] public ItemTrapSharedData sharedData = ItemTrapSharedData.Default;
        [HideLabel] public ItemTrapVariableData initialState = ItemTrapVariableData.Default;

        public static ItemTrapDataBlock Default => new ItemTrapDataBlock();
    }

    [Serializable]
    public sealed class ItemWeaponDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Weapon;
        [HideLabel] public ItemWeaponSharedData sharedData = ItemWeaponSharedData.Default;
        [HideLabel, InlineProperty] public ESWeaponConfigKey key = new ESWeaponConfigKey();
        [HideLabel] public ItemWeaponVariableData initialState = ItemWeaponVariableData.Default;
        [HideLabel] public ItemWeaponConfig config = new ItemWeaponConfig();

        public static ItemWeaponDataBlock Default => new ItemWeaponDataBlock();
    }

    [Serializable]
    public sealed class ItemPickupDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Pickup;
        [HideLabel] public ItemPickupSharedData sharedData = ItemPickupSharedData.Default;
        [HideLabel] public ItemPickupVariableData initialState = ItemPickupVariableData.Default;

        public static ItemPickupDataBlock Default => new ItemPickupDataBlock();
    }

    [Serializable]
    public sealed class ItemZoneDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Zone;
        [HideLabel] public ItemZoneSharedData sharedData = ItemZoneSharedData.Default;
        [HideLabel] public ItemZoneVariableData initialState = ItemZoneVariableData.Default;

        public static ItemZoneDataBlock Default => new ItemZoneDataBlock();
    }

    [Serializable]
    public sealed class ItemPropDataBlock : ItemKindDataBlock
    {
        public override ItemKind Kind => ItemKind.Prop;
        [HideLabel] public ItemPropSharedData sharedData = ItemPropSharedData.Default;
        [HideLabel] public ItemPropVariableData initialState = ItemPropVariableData.Default;

        public static ItemPropDataBlock Default => new ItemPropDataBlock();
    }
}
