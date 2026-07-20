using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "物品数据信息")]
    public class ItemDataInfo : SoDataInfo
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

        [Title("飞行物")]
        [ShowIf(nameof(ShowShotConfig))]
        [HideLabel]
        public ItemShotConfig shotConfig = new ItemShotConfig();

        [Title("武器")]
        [ShowIf(nameof(ShowWeaponConfig))]
        [HideLabel]
        public ItemWeaponConfig weaponConfig = new ItemWeaponConfig();

        private bool ShowShotConfig()
        {
            return baseConfig != null && baseConfig.kind == ItemKind.Shot;
        }

        private bool ShowMoveConfig()
        {
            if (baseConfig == null)
                return false;

            return baseConfig.kind == ItemKind.Mechanism
                || baseConfig.kind == ItemKind.Platform
                || baseConfig.kind == ItemKind.Rotator
                || baseConfig.kind == ItemKind.Pickup
                || baseConfig.kind == ItemKind.Trap
                || baseConfig.kind == ItemKind.Zone;
        }

        private bool ShowWeaponConfig()
        {
            return baseConfig != null && baseConfig.kind == ItemKind.Weapon;
        }

        private string BuildEditorSummary()
        {
            ItemKind kind = baseConfig != null ? baseConfig.kind : ItemKind.None;
            string name = baseConfig != null && !string.IsNullOrWhiteSpace(baseConfig.displayName)
                ? baseConfig.displayName
                : KeyName;

            switch (kind)
            {
                case ItemKind.Shot:
                    return $"{name}：飞行物配置。重点填写【飞行物】和必要的【逻辑】事件。";
                case ItemKind.Weapon:
                    return $"{name}：武器配置。重点填写【武器】，发射物请引用一个类型为【飞行物】的 ItemDataInfo。";
                case ItemKind.Mechanism:
                    return $"{name}：门/机关配置。通常使用【交互】+【逻辑】，需要开合时再填【移动】。";
                case ItemKind.Platform:
                    return $"{name}：移动平台配置。通常填写【移动】，用路径或往返移动。";
                case ItemKind.Rotator:
                    return $"{name}：旋转机关配置。通常填写【移动】，用持续旋转或摆动。";
                case ItemKind.Pickup:
                    return $"{name}：掉落物配置。通常使用【交互】+【逻辑】处理拾取，需要落地表现时再填【移动】。";
                case ItemKind.Tower:
                    return $"{name}：防御塔/炮台配置。第一版优先用【逻辑】驱动索敌/发射，复杂后再拆模块。";
                case ItemKind.Trap:
                    return $"{name}：陷阱配置。通常用【逻辑】处理触发、冷却和失效，需要移动陷阱时再填【移动】。";
                case ItemKind.Zone:
                    return $"{name}：持续区域配置。通常用【逻辑】处理进入、周期和结束事件，需要区域移动时再填【移动】。";
                case ItemKind.Prop:
                    return $"{name}：普通物体配置。按需要填写【交互】或【逻辑】。";
                default:
                    return "请选择物品类型。门/平台/机关显示【移动】，飞行物显示【飞行物】，武器显示【武器】。";
            }
        }
    }

    public enum ItemKind
    {
        [InspectorName("未指定")]
        None = 0,

        [InspectorName("普通物体")]
        Prop = 1,

        [InspectorName("门/机关")]
        Mechanism = 2,

        [InspectorName("掉落物")]
        Pickup = 3,

        [InspectorName("飞行物")]
        Shot = 4,

        [InspectorName("武器")]
        Weapon = 5,

        [InspectorName("防御塔/炮台")]
        Tower = 6,

        [InspectorName("陷阱")]
        Trap = 7,

        [InspectorName("持续区域")]
        Zone = 8,

        [InspectorName("移动平台")]
        Platform = 9,

        [InspectorName("旋转机关")]
        Rotator = 10
    }

    public enum ItemMoveMode
    {
        [InspectorName("不移动")]
        None = 0,

        [InspectorName("直线移动")]
        Line = 1,

        [InspectorName("路径移动")]
        Path = 2,

        [InspectorName("原地旋转")]
        Rotate = 3,

        [InspectorName("门式摆动")]
        Swing = 4,

        [InspectorName("往返平台")]
        Platform = 5,

        [InspectorName("掉落")]
        Drop = 6,

        [InspectorName("跟随目标")]
        Follow = 7,

        [InspectorName("区域移动")]
        Zone = 8,

        [InspectorName("逻辑控制")]
        Logic = 9
    }

    public enum ItemUseMode
    {
        [InspectorName("不可使用")]
        None = 0,

        [InspectorName("交互使用")]
        Interact = 1,

        [InspectorName("拾取")]
        Pickup = 2,

        [InspectorName("装备")]
        Equip = 3,

        [InspectorName("触发")]
        Trigger = 4
    }

    public enum ShotAimMode
    {
        [InspectorName("自由飞行")]
        Free = 0,

        [InspectorName("锁定目标")]
        Target = 1,

        [InspectorName("必中表现")]
        MustHit = 2,

        [InspectorName("瞬时扫描")]
        Scan = 3
    }

    public enum ShotBlockMode
    {
        [InspectorName("不阻挡")]
        None = 0,

        [InspectorName("只被场景阻挡")]
        WorldOnly = 1,

        [InspectorName("任意阻挡体")]
        AnyBlocker = 2
    }

    public enum ItemWeaponKind
    {
        [InspectorName("未指定")]
        None = 0,

        [InspectorName("近战")]
        Melee = 1,

        [InspectorName("远程")]
        Ranged = 2,

        [InspectorName("投掷")]
        Throwable = 3,

        [InspectorName("法器")]
        Magic = 4
    }

    [Serializable]
    public sealed class ItemBaseConfig
    {
        [LabelText("物品类型")]
        public ItemKind kind = ItemKind.Prop;

        [LabelText("运行预制体")]
        public GameObject prefab;

        [LabelText("显示名称")]
        public string displayName;

        [LabelText("图标")]
        public Sprite icon;

        [LabelText("标签")]
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public sealed class ItemInteractConfig
    {
        [LabelText("可交互")]
        public bool canInteract;

        [ShowIf(nameof(canInteract))]
        [LabelText("使用方式")]
        public ItemUseMode useMode = ItemUseMode.Interact;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互提示")]
        public string prompt;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互距离")]
        public float distance = 1.5f;

        [ShowIf(nameof(canInteract))]
        [LabelText("需要面向")]
        public bool requireFacing = true;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互条件")]
        [SerializeReference]
        public ESGetBoolExpression condition;
    }

    [Serializable]
    public sealed class ItemLogicConfig
    {
        [LabelText("生成时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("生成时")]
        public ESOutputOp onSpawn;

        [LabelText("使用时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("使用时")]
        public ESOutputOp onUse;

        [LabelText("命中时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("命中时")]
        public ESOutputOp onHit;

        [LabelText("过期时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("过期时")]
        public ESOutputOp onExpire;

        [LabelText("销毁前")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("销毁前")]
        public ESOutputOp onDestroy;
    }

    [Serializable]
    public sealed class ItemMoveConfig
    {
        [LabelText("移动方式")]
        public ItemMoveMode mode = ItemMoveMode.None;

        [ShowIf(nameof(UsesMoveSpeed))]
        [LabelText("移动速度")]
        public float speed = 1f;

        [ShowIf(nameof(UsesRotateSpeed))]
        [LabelText("旋转速度")]
        public float rotateSpeed = 90f;

        [ShowIf(nameof(UsesDistance))]
        [LabelText("移动距离/角度")]
        public float amount = 1f;

        [ShowIf(nameof(UsesPath))]
        [LabelText("路径点")]
        public List<Vector3> points = new List<Vector3>();

        [ShowIf(nameof(UsesLoop))]
        [LabelText("循环")]
        public bool loop = true;

        [ShowIf(nameof(UsesBackAndForth))]
        [LabelText("往返")]
        public bool backAndForth = true;

        [ShowIf(nameof(UsesGravity))]
        [LabelText("使用重力")]
        public bool useGravity = true;

        private bool UsesMoveSpeed()
        {
            return mode == ItemMoveMode.Line
                || mode == ItemMoveMode.Path
                || mode == ItemMoveMode.Platform
                || mode == ItemMoveMode.Drop
                || mode == ItemMoveMode.Follow
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesRotateSpeed()
        {
            return mode == ItemMoveMode.Rotate || mode == ItemMoveMode.Swing;
        }

        private bool UsesDistance()
        {
            return mode == ItemMoveMode.Line
                || mode == ItemMoveMode.Swing
                || mode == ItemMoveMode.Platform
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesPath()
        {
            return mode == ItemMoveMode.Path;
        }

        private bool UsesLoop()
        {
            return mode == ItemMoveMode.Path
                || mode == ItemMoveMode.Rotate
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesBackAndForth()
        {
            return mode == ItemMoveMode.Platform || mode == ItemMoveMode.Swing;
        }

        private bool UsesGravity()
        {
            return mode == ItemMoveMode.Drop;
        }
    }

    [Serializable]
    public sealed class ItemShotConfig
    {
        [LabelText("启用飞行物")]
        public bool enabled;

        [ShowIf(nameof(enabled))]
        [LabelText("瞄准模式")]
        public ShotAimMode aimMode = ShotAimMode.Free;

        [ShowIf(nameof(enabled))]
        [LabelText("阻挡模式")]
        public ShotBlockMode blockMode = ShotBlockMode.AnyBlocker;

        [ShowIf(nameof(enabled))]
        [LabelText("发射延迟")]
        [MinValue(0)]
        public float launchDelay;

        [ShowIf(nameof(enabled))]
        [LabelText("预热时间")]
        [MinValue(0)]
        public float warmupTime;

        [ShowIf(nameof(enabled))]
        [LabelText("速度")]
        public float speed = 30f;

        [ShowIf(nameof(enabled))]
        [LabelText("加速度")]
        public float acceleration = 120f;

        [ShowIf(nameof(enabled))]
        [LabelText("最大速度")]
        public float maxSpeed = 30f;

        [ShowIf(nameof(enabled))]
        [LabelText("锁头开始")]
        [MinValue(0)]
        public float trackingStartTime;

        [ShowIf(nameof(enabled))]
        [LabelText("锁头持续")]
        [Tooltip("小于 0 表示一直锁头。0 表示只按初始方向飞。")]
        public float trackingDuration = -1f;

        [ShowIf(nameof(enabled))]
        [LabelText("转向速度")]
        [MinValue(0)]
        public float turnSpeed = 720f;

        [ShowIf(nameof(enabled))]
        [LabelText("寿命")]
        public float lifeTime = 5f;

        [ShowIf(nameof(enabled))]
        [LabelText("命中半径")]
        public float radius = 0.05f;

        [ShowIf(nameof(enabled))]
        [LabelText("命中层")]
        public LayerMask hitLayers = ~0;

        [ShowIf(nameof(enabled))]
        [LabelText("使用重力")]
        public bool useGravity;

        [ShowIf(nameof(enabled))]
        [LabelText("朝向速度方向")]
        public bool orientToVelocity = true;

        [ShowIf(nameof(enabled))]
        [LabelText("逻辑随机种子")]
        public int logicSeed;

        public ProjectileMotionConfig ToProjectileMotionConfig()
        {
            ProjectileMotionFlags flags = ProjectileMotionFlags.ClampSpeed;
            if (useGravity)
                flags |= ProjectileMotionFlags.UseGravity;
            if (orientToVelocity)
                flags |= ProjectileMotionFlags.OrientToVelocity;

            return new ProjectileMotionConfig
            {
                speed = speed,
                acceleration = acceleration,
                maxSpeed = maxSpeed,
                maxLifetime = lifeTime,
                launchDelay = launchDelay,
                warmupTime = warmupTime,
                arriveDistance = radius,
                drag = 0f,
                turnSpeedDegrees = turnSpeed,
                trackingStartTime = trackingStartTime,
                trackingDuration = trackingDuration,
                gravity = Physics.gravity,
                flags = flags
            };
        }
    }

    [Serializable]
    public sealed class ItemWeaponConfig
    {
        [LabelText("启用武器")]
        public bool enabled;

        [ShowIf(nameof(enabled))]
        [LabelText("武器类型")]
        public ItemWeaponKind weaponKind = ItemWeaponKind.Ranged;

        [ShowIf(nameof(enabled))]
        [LabelText("装备槽")]
        public string equipSlot;

        [ShowIf(nameof(enabled))]
        [LabelText("基础伤害")]
        public float baseDamage = 10f;

        [ShowIf(nameof(enabled))]
        [LabelText("攻击间隔")]
        public float attackInterval = 0.3f;

        [ShowIf(nameof(enabled))]
        [LabelText("发射飞行物")]
        public ItemDataInfo shotItem;

        [ShowIf(nameof(enabled))]
        [LabelText("使用时逻辑")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("使用时逻辑")]
        public ESOutputOp onUse;
    }
}

// ES已修正
