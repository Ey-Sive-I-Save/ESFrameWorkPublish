using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [ESCreatePath("数据信息", "输入配置数据信息")]
    public sealed class ESInputConfig : SoDataInfo, IESInputRuntimeConfigSource
    {
        [Title("基础信息")]
        [LabelText("配置ID")]
        public string configId = "Default";

        [LabelText("默认方案")]
        public string defaultSchemeId = ESInputSchemeIds.KeyboardMouse;

        [Title("输入方案")]
        [LabelText("方案列表")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ESInputSchemeDefine> schemes = new List<ESInputSchemeDefine>();

        [Title("输入动作")]
        [LabelText("动作列表")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ESInputActionDefine> actions = new List<ESInputActionDefine>();

        public int ActionCount
        {
            get { return actions == null ? 0 : actions.Count; }
        }

        public bool TryGetActionDefine(int index, out ESInputActionDefine action)
        {
            if (actions != null && index >= 0 && index < actions.Count)
            {
                action = actions[index];
                return action != null;
            }

            action = null;
            return false;
        }

        public bool TryGetAction(ESInputActionId id, out ESInputActionDefine action)
        {
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    ESInputActionDefine item = actions[i];
                    if (item != null && item.id == id)
                    {
                        action = item;
                        return true;
                    }
                }
            }

            action = null;
            return false;
        }

        public int GetMaxCoreActionIndex()
        {
            int max = -1;
            if (actions == null)
                return max;

            for (int i = 0; i < actions.Count; i++)
            {
                ESInputActionDefine item = actions[i];
                if (item == null || item.id == ESInputActionId.Dynamic)
                    continue;

                int index = (int)item.id;
                if (index > max)
                    max = index;
            }

            return max;
        }

        [Button("重置为默认动作配置")]
        public void ResetDefaultGameplayConfig()
        {
            defaultSchemeId = ESInputSchemeIds.KeyboardMouse;

            schemes.Clear();
            schemes.Add(new ESInputSchemeDefine
            {
                schemeId = ESInputSchemeIds.KeyboardMouse,
                displayName = "键盘鼠标",
                deviceKind = ESInputDeviceKind.KeyboardMouse,
                bindingGroup = ESInputSchemeIds.KeyboardMouse
            });
            schemes.Add(new ESInputSchemeDefine
            {
                schemeId = ESInputSchemeIds.Gamepad,
                displayName = "手柄",
                deviceKind = ESInputDeviceKind.Gamepad,
                bindingGroup = ESInputSchemeIds.Gamepad
            });
            schemes.Add(new ESInputSchemeDefine
            {
                schemeId = ESInputSchemeIds.Touch,
                displayName = "触摸",
                deviceKind = ESInputDeviceKind.Touch,
                bindingGroup = ESInputSchemeIds.Touch
            });

            actions.Clear();
            AddDefaultActions(actions);
        }

        public ESInputRuntimeBuildResult BuildRuntime(ESInputBindingProfile profile = null)
        {
            return ESInputRuntimeBuilder.Build(this, profile, defaultSchemeId);
        }

        private static void AddDefaultActions(List<ESInputActionDefine> target)
        {
            target.Add(Value(ESInputActionId.Move, "Move", ESInputValueType.Vector2, "移动")
                .WithComposite2D(ESInputSchemeIds.KeyboardMouse, "WASD", "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d")
                .WithBinding(ESInputSchemeIds.Gamepad, "<Gamepad>/leftStick")
                .WithVirtualBinding(ESInputSchemeIds.Touch, "MoveJoystick"));

            target.Add(Value(ESInputActionId.Look, "Look", ESInputValueType.Vector2, "视角")
                .WithBinding(ESInputSchemeIds.KeyboardMouse, "<Mouse>/delta")
                .WithBinding(ESInputSchemeIds.Gamepad, "<Gamepad>/rightStick")
                .WithVirtualBinding(ESInputSchemeIds.Touch, "LookArea"));

            target.Add(Button(ESInputActionId.Attack, "Attack", "攻击", "<Mouse>/leftButton", "<Gamepad>/rightTrigger", "AttackButton"));
            target.Add(Button(ESInputActionId.HeavyAttack, "HeavyAttack", "重击", "<Mouse>/rightButton", "<Gamepad>/leftTrigger", "HeavyAttackButton"));
            target.Add(Button(ESInputActionId.Block, "Block", "格挡", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder", "BlockButton"));
            target.Add(Button(ESInputActionId.Slide, "Slide", "滑行", "<Keyboard>/leftCtrl", "<Gamepad>/rightShoulder", "SlideButton"));
            target.Add(Button(ESInputActionId.SwitchWeapon, "SwitchWeapon", "切换武器", "<Keyboard>/tab", "<Gamepad>/dpad/right", "SwitchWeaponButton"));
            target.Add(Button(ESInputActionId.EquipWeapon, "EquipWeapon", "装备武器", "<Keyboard>/q", "<Gamepad>/buttonWest", "EquipWeaponButton"));
            target.Add(Button(ESInputActionId.HolsterWeapon, "HolsterWeapon", "收起武器", "<Keyboard>/t", "<Gamepad>/start", "HolsterWeaponButton"));
            target.Add(Button(ESInputActionId.WeaponSlot1, "WeaponSlot1", "武器槽1", "<Keyboard>/1", "<Gamepad>/dpad/up", "WeaponSlot1Button"));
            target.Add(Button(ESInputActionId.WeaponSlot2, "WeaponSlot2", "武器槽2", "<Keyboard>/2", "<Gamepad>/dpad/right", "WeaponSlot2Button"));
            target.Add(Button(ESInputActionId.WeaponSlot3, "WeaponSlot3", "武器槽3", "<Keyboard>/3", "<Gamepad>/dpad/down", "WeaponSlot3Button"));
            target.Add(Button(ESInputActionId.WeaponSlot4, "WeaponSlot4", "武器槽4", "<Keyboard>/4", "<Gamepad>/dpad/left", "WeaponSlot4Button"));
            target.Add(Button(ESInputActionId.WeaponSlot5, "WeaponSlot5", "武器槽5", "<Keyboard>/5", "<Gamepad>/rightStickPress", "WeaponSlot5Button"));
            target.Add(Button(ESInputActionId.Aim, "Aim", "瞄准", "<Mouse>/rightButton", "<Gamepad>/leftTrigger", "AimButton"));
            target.Add(Button(ESInputActionId.PeekLeft, "PeekLeft", "左探头", "<Keyboard>/z", "", "PeekLeftButton"));
            target.Add(Button(ESInputActionId.PeekRight, "PeekRight", "右探头", "<Keyboard>/x", "", "PeekRightButton"));
            target.Add(Button(ESInputActionId.Skill1, "Skill1", "技能1", "<Keyboard>/f1", "<Gamepad>/dpad/up", "Skill1Button"));
            target.Add(Button(ESInputActionId.Skill2, "Skill2", "技能2", "<Keyboard>/f2", "<Gamepad>/dpad/left", "Skill2Button"));
            target.Add(Button(ESInputActionId.Skill3, "Skill3", "技能3", "<Keyboard>/f3", "<Gamepad>/dpad/down", "Skill3Button"));
            target.Add(Button(ESInputActionId.Jump, "Jump", "跳跃", "<Keyboard>/space", "<Gamepad>/buttonSouth", "JumpButton"));
            target.Add(Button(ESInputActionId.Crouch, "Crouch", "蹲伏", "<Keyboard>/c", "<Gamepad>/rightStickPress", "CrouchButton"));
            target.Add(Button(ESInputActionId.Fly, "Fly", "飞行", "<Keyboard>/f", "<Gamepad>/buttonNorth", "FlyButton"));
            target.Add(Button(ESInputActionId.Mount, "Mount", "骑乘", "<Keyboard>/r", "<Gamepad>/buttonWest", "MountButton"));
            target.Add(Button(ESInputActionId.Climb, "Climb", "攀爬", "<Keyboard>/g", "<Gamepad>/leftStickPress", "ClimbButton"));
            target.Add(Button(ESInputActionId.Interact, "Interact", "交互", "<Keyboard>/e", "<Gamepad>/buttonEast", "InteractButton"));

            target.Add(Value(ESInputActionId.FlyVertical, "FlyVertical", ESInputValueType.Axis, "飞行垂直")
                .WithAxisComposite(ESInputSchemeIds.KeyboardMouse, "QE", "<Keyboard>/q", "<Keyboard>/e")
                .WithAxisComposite(ESInputSchemeIds.Gamepad, "Triggers", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger")
                .WithVirtualBinding(ESInputSchemeIds.Touch, "FlyVerticalSlider"));
        }

        private static ESInputActionDefine Value(ESInputActionId id, string name, ESInputValueType valueType, string displayName)
        {
            ESInputActionDefine config = ESInputActionDefine.Value(id, name, valueType);
            config.displayName = displayName;
            return config;
        }

        private static ESInputActionDefine Button(
            ESInputActionId id,
            string name,
            string displayName,
            string keyboardPath,
            string gamepadPath,
            string touchControlId)
        {
            ESInputActionDefine config = Value(id, name, ESInputValueType.Button, displayName);

            if (!string.IsNullOrEmpty(keyboardPath))
                config.WithBinding(ESInputSchemeIds.KeyboardMouse, keyboardPath);

            if (!string.IsNullOrEmpty(gamepadPath))
                config.WithBinding(ESInputSchemeIds.Gamepad, gamepadPath);

            if (!string.IsNullOrEmpty(touchControlId))
                config.WithVirtualBinding(ESInputSchemeIds.Touch, touchControlId);

            return config;
        }
    }

    public static class ESInputActionDefineExtensions
    {
        public static ESInputActionDefine WithBinding(this ESInputActionDefine config, string schemeId, string path, string interactions = "", string processors = "")
        {
            config.bindings.Add(ESInputBindingDefine.InputSystem(schemeId, path, interactions, processors));
            return config;
        }

        public static ESInputActionDefine WithVirtualBinding(this ESInputActionDefine config, string schemeId, string controlId)
        {
            config.bindings.Add(ESInputBindingDefine.VirtualControl(schemeId, controlId));
            return config;
        }

        public static ESInputActionDefine WithComposite2D(this ESInputActionDefine config, string schemeId, string compositeName, string up, string down, string left, string right)
        {
            config.bindings.Add(Composite(schemeId, compositeName, "2DVector"));
            config.bindings.Add(CompositePart(schemeId, "Up", up));
            config.bindings.Add(CompositePart(schemeId, "Down", down));
            config.bindings.Add(CompositePart(schemeId, "Left", left));
            config.bindings.Add(CompositePart(schemeId, "Right", right));
            return config;
        }

        public static ESInputActionDefine WithAxisComposite(this ESInputActionDefine config, string schemeId, string compositeName, string negative, string positive)
        {
            config.bindings.Add(Composite(schemeId, compositeName, "1DAxis"));
            config.bindings.Add(CompositePart(schemeId, "Negative", negative));
            config.bindings.Add(CompositePart(schemeId, "Positive", positive));
            return config;
        }

        private static ESInputBindingDefine Composite(string schemeId, string name, string compositePath)
        {
            return new ESInputBindingDefine
            {
                schemeId = schemeId,
                source = ESInputBindingSource.InputSystem,
                isComposite = true,
                name = name,
                path = compositePath
            };
        }

        private static ESInputBindingDefine CompositePart(string schemeId, string name, string path)
        {
            return new ESInputBindingDefine
            {
                schemeId = schemeId,
                source = ESInputBindingSource.InputSystem,
                isPartOfComposite = true,
                name = name,
                path = path
            };
        }
    }
}
