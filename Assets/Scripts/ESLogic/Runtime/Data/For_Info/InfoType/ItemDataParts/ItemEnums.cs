using System;
using UnityEngine;

namespace ES
{
    public enum ItemKind
    {
        [InspectorName("未指定")]
        None = 0,

        [InspectorName("普通物件")]
        Prop = 1,

        [InspectorName("门")]
        Door = 2,

        [Obsolete("Use Door. Mechanism is an old ItemKind alias.")]
        Mechanism = Door,

        [InspectorName("拾取物")]
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

    /// <summary>武器内容分类之外，主攻击实际采用的交付方式。</summary>
    public enum WeaponAttackDeliveryMode
    {
        [InspectorName("动作攻击")]
        Action = 0,

        [InspectorName("瞬时射线")]
        HitScan = 1,

        [InspectorName("飞行物")]
        Shot = 2,

        [InspectorName("持续射束")]
        Beam = 3
    }

    /// <summary>一次输入如何驱动一次或多次攻击交付。</summary>
    public enum WeaponFirePolicy
    {
        [InspectorName("单发")]
        Single = 0,

        [InspectorName("自动连发")]
        Automatic = 1,

        [InspectorName("点射")]
        Burst = 2,

        [InspectorName("蓄力")]
        Charge = 3,

        [InspectorName("持续")]
        Continuous = 4
    }
}
