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
        [InspectorName("武器槽 1")]
        WeaponSlot1 = 9,
        [InspectorName("武器槽 2")]
        WeaponSlot2 = 10,
        [InspectorName("武器槽 3")]
        WeaponSlot3 = 11,
        [InspectorName("武器槽 4")]
        WeaponSlot4 = 12,
        [InspectorName("武器槽 5")]
        WeaponSlot5 = 13,
        [InspectorName("瞄准")]
        Aim = 14,
        [InspectorName("左探头")]
        PeekLeft = 15,
        [InspectorName("右探头")]
        PeekRight = 16,
        [InspectorName("技能 1")]
        Skill1 = 17,
        [InspectorName("技能 2")]
        Skill2 = 18,
        [InspectorName("技能 3")]
        Skill3 = 19,
        [InspectorName("跳跃")]
        Jump = 20,
        [InspectorName("蹲伏")]
        Crouch = 21,
        [InspectorName("飞行")]
        Fly = 22,
        [InspectorName("骑乘")]
        Mount = 23,
        [InspectorName("飞行垂直轴")]
        FlyVertical = 24,
        [InspectorName("攀爬")]
        Climb = 25,
        [InspectorName("交互")]
        Interact = 26,

        [InspectorName("UI 提交")]
        UISubmit = 100,
        [InspectorName("UI 取消")]
        UICancel = 101,
        [InspectorName("UI 导航")]
        UINavigate = 102,
        [InspectorName("UI 指针")]
        UIPoint = 103,
        [InspectorName("UI 点击")]
        UIClick = 104,
        [InspectorName("UI 滚动")]
        UIScroll = 105,

        [InspectorName("动态动作")]
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
        [InspectorName("UI")]
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

    public enum ESInputTriggerType
    {
        [InspectorName("按下触发")]
        Pressed,
        [InspectorName("松开触发")]
        Released,
        [InspectorName("按住触发")]
        Held,
        [InspectorName("长按触发")]
        LongPress,
        [InspectorName("双击触发")]
        DoublePress
    }

    [System.Flags]
    public enum ESInputTriggerFeature
    {
        [InspectorName("无")]
        None = 0,
        [InspectorName("按下")]
        Pressed = 1 << 0,
        [InspectorName("松开")]
        Released = 1 << 1,
        [InspectorName("按住")]
        Held = 1 << 2,
        [InspectorName("长按")]
        LongPress = 1 << 3,
        [InspectorName("双击")]
        DoublePress = 1 << 4
    }

    public enum ESInputPressPolicy
    {
        [InspectorName("按下立即触发")]
        PressedImmediate,
        [InspectorName("松开时判定短按")]
        PressedOnRelease,
        [InspectorName("未触发长按前算短按")]
        PressedIfNotLongPress,
        [InspectorName("长按触发后吞掉短按")]
        LongPressConsumesPressed
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
