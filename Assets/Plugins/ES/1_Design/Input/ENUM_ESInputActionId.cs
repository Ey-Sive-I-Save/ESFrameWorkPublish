using UnityEngine;

namespace ES
{
    public enum ESInputActionId
    {
        [InspectorName("移动")]
        Move = 0,

        [InspectorName("视角")]
        Look = 1,

        [InspectorName("攻击")]
        Attack = 2,

        [InspectorName("重击")]
        HeavyAttack = 3,

        [InspectorName("格挡")]
        Block = 4,

        [InspectorName("滑行")]
        Slide = 5,

        [InspectorName("切换武器")]
        SwitchWeapon = 6,

        [InspectorName("装备武器")]
        EquipWeapon = 7,

        [InspectorName("收起武器")]
        HolsterWeapon = 8,

        [InspectorName("武器槽1")]
        WeaponSlot1 = 9,

        [InspectorName("武器槽2")]
        WeaponSlot2 = 10,

        [InspectorName("武器槽3")]
        WeaponSlot3 = 11,

        [InspectorName("武器槽4")]
        WeaponSlot4 = 12,

        [InspectorName("武器槽5")]
        WeaponSlot5 = 13,

        [InspectorName("瞄准")]
        Aim = 14,

        [InspectorName("左探头")]
        PeekLeft = 15,

        [InspectorName("右探头")]
        PeekRight = 16,

        [InspectorName("技能1")]
        Skill1 = 17,

        [InspectorName("技能2")]
        Skill2 = 18,

        [InspectorName("技能3")]
        Skill3 = 19,

        [InspectorName("跳跃")]
        Jump = 20,

        [InspectorName("蹲伏")]
        Crouch = 21,

        [InspectorName("飞行")]
        Fly = 22,

        [InspectorName("骑乘")]
        Mount = 23,

        [InspectorName("飞行垂直")]
        FlyVertical = 24,

        [InspectorName("攀爬")]
        Climb = 25,

        [InspectorName("交互")]
        Interact = 26,

        [InspectorName("动态扩展")]
        Dynamic = 10000
    }

    public enum ESInputActionCategory
    {
        [InspectorName("通用")]
        Common,

        [InspectorName("移动")]
        Move,

        [InspectorName("视角")]
        CameraLook,

        [InspectorName("战斗")]
        Combat,

        [InspectorName("交互")]
        Interaction,

        [InspectorName("界面")]
        UI,

        [InspectorName("载具/特殊移动")]
        SpecialMove
    }

    public enum ESInputValueType
    {
        [InspectorName("按钮")]
        Button,

        [InspectorName("单轴")]
        Axis,

        [InspectorName("二维向量")]
        Vector2
    }

    public enum ESInputDeviceKind
    {
        [InspectorName("键盘鼠标")]
        KeyboardMouse,

        [InspectorName("手柄")]
        Gamepad,

        [InspectorName("触摸")]
        Touch,

        [InspectorName("自定义")]
        Custom
    }

    public enum ESInputBindingSource
    {
        [InspectorName("InputSystem")]
        InputSystem,

        [InspectorName("虚拟控件")]
        VirtualControl
    }

    public static class ESInputSchemeIds
    {
        public const string KeyboardMouse = "KeyboardMouse";
        public const string Gamepad = "Gamepad";
        public const string Touch = "Touch";
    }
}
