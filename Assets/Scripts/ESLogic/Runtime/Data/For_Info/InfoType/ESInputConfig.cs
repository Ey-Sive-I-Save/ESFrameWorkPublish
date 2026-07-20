using System.Collections.Generic;
using ES.Internal;
using Sirenix.OdinInspector;

namespace ES
{
    [ESCreatePath("数据信息", "输入配置数据信息")]
    public sealed class ESInputConfig : SoDataInfo, IESInputRuntimeConfigSource
    {
        [Title("基础信息")]
        [LabelText("配置 ID")]
        public string configId = "Default";

        [LabelText("默认方案")]
        public string defaultSchemeId = ESInputSchemeIds.KeyboardMouse;

        [Title("输入方案")]
        [LabelText("方案列表")]
        [ESTwoPaneList("displayName", 220f, 240f, false, "输入方案", "方案详情")]
        public List<ESInputSchemeDefine> schemes = new List<ESInputSchemeDefine>();

        [Title("输入动作")]
        [LabelText("动作列表")]
        [ESTwoPaneList("displayName", 240f, 380f, false, "输入动作", "动作详情")]
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
                displayName = "触摸/UI",
                deviceKind = ESInputDeviceKind.Touch,
                bindingGroup = ESInputSchemeIds.Touch
            });

            actions.Clear();
            AddDefaultActions(actions);
        }

        public ESInputRuntimeBuildResult BuildRuntime(params ESInputBindingProfile[] profileLayers)
        {
            EnsureBindingIds();
            return ESInputUtility.BuildRuntime(this, defaultSchemeId, profileLayers);
        }

        public ESInputRuntimeBuildResult BuildRuntime(IList<ESInputBindingProfile> profileLayers)
        {
            EnsureBindingIds();
            return ESInputUtility.BuildRuntime(this, defaultSchemeId, profileLayers);
        }

        [Button("补齐绑定 ID")]
        public void EnsureBindingIds()
        {
            if (actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                ESInputActionDefine action = actions[i];
                if (action == null || action.bindings == null)
                    continue;

                action.NormalizeTriggerSettings();

                Dictionary<string, int> duplicateCounters = new Dictionary<string, int>();
                for (int b = 0; b < action.bindings.Count; b++)
                {
                    ESInputBindingDefine binding = action.bindings[b];
                    if (binding == null)
                        continue;

                    ESInputBindingKeyUtility.EnsureBindingName(binding);

                    if (!string.IsNullOrEmpty(binding.bindingId))
                        continue;

                    string baseKey = ESInputBindingKeyUtility.MakeBindingBaseKey(action, binding);
                    duplicateCounters.TryGetValue(baseKey, out int duplicateIndex);
                    duplicateCounters[baseKey] = duplicateIndex + 1;
                    binding.bindingId = ESInputBindingKeyUtility.MakeBindingId(action, binding, duplicateIndex);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureBindingIds();
        }
#endif

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
            target.Add(Button(ESInputActionId.HeavyAttack, "HeavyAttack", "重击", "<Mouse>/middleButton", "<Gamepad>/rightShoulder", "HeavyAttackButton"));
            target.Add(Button(ESInputActionId.Block, "Block", "格挡", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder", "BlockButton"));
            target.Add(Button(ESInputActionId.Slide, "Slide", "滑行", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast", "SlideButton"));
            target.Add(Button(ESInputActionId.SwitchWeapon, "SwitchWeapon", "切换武器", "<Keyboard>/tab", "<Gamepad>/dpad/right", "SwitchWeaponButton"));
            target.Add(Button(ESInputActionId.EquipWeapon, "EquipWeapon", "装备武器", "<Keyboard>/v", "", "EquipWeaponButton"));
            target.Add(Button(ESInputActionId.HolsterWeapon, "HolsterWeapon", "收起武器", "<Keyboard>/h", "", "HolsterWeaponButton"));
            target.Add(Button(ESInputActionId.WeaponSlot1, "WeaponSlot1", "武器槽 1", "<Keyboard>/1", "", "WeaponSlot1Button"));
            target.Add(Button(ESInputActionId.WeaponSlot2, "WeaponSlot2", "武器槽 2", "<Keyboard>/2", "", "WeaponSlot2Button"));
            target.Add(Button(ESInputActionId.WeaponSlot3, "WeaponSlot3", "武器槽 3", "<Keyboard>/3", "", "WeaponSlot3Button"));
            target.Add(Button(ESInputActionId.WeaponSlot4, "WeaponSlot4", "武器槽 4", "<Keyboard>/4", "", "WeaponSlot4Button"));
            target.Add(Button(ESInputActionId.WeaponSlot5, "WeaponSlot5", "武器槽 5", "<Keyboard>/5", "", "WeaponSlot5Button"));
            target.Add(Button(ESInputActionId.Aim, "Aim", "瞄准", "<Mouse>/rightButton", "<Gamepad>/leftTrigger", "AimButton"));
            target.Add(Button(ESInputActionId.PeekLeft, "PeekLeft", "左探头", "<Keyboard>/z", "", "PeekLeftButton"));
            target.Add(Button(ESInputActionId.PeekRight, "PeekRight", "右探头", "<Keyboard>/x", "", "PeekRightButton"));
            target.Add(Button(ESInputActionId.Skill1, "Skill1", "技能 1", "<Keyboard>/q", "<Gamepad>/dpad/up", "Skill1Button"));
            target.Add(Button(ESInputActionId.Skill2, "Skill2", "技能 2", "<Keyboard>/r", "<Gamepad>/dpad/left", "Skill2Button"));
            target.Add(Button(ESInputActionId.Skill3, "Skill3", "技能 3", "<Keyboard>/t", "<Gamepad>/dpad/down", "Skill3Button"));
            target.Add(Button(ESInputActionId.Jump, "Jump", "跳跃", "<Keyboard>/space", "<Gamepad>/buttonSouth", "JumpButton"));
            target.Add(Button(ESInputActionId.Crouch, "Crouch", "蹲伏", "<Keyboard>/c", "<Gamepad>/rightStickPress", "CrouchButton"));
            target.Add(Button(ESInputActionId.Fly, "Fly", "飞行", "<Keyboard>/b", "<Gamepad>/buttonNorth", "FlyButton"));
            target.Add(Button(ESInputActionId.Mount, "Mount", "骑乘", "<Keyboard>/y", "<Gamepad>/start", "MountButton"));
            target.Add(Button(ESInputActionId.Climb, "Climb", "攀爬", "<Keyboard>/g", "<Gamepad>/leftStickPress", "ClimbButton"));
            target.Add(Button(ESInputActionId.Interact, "Interact", "交互", "<Keyboard>/e", "<Gamepad>/buttonWest", "InteractButton"));

            target.Add(Value(ESInputActionId.FlyVertical, "FlyVertical", ESInputValueType.Axis, "飞行垂直轴")
                .WithAxisComposite(ESInputSchemeIds.KeyboardMouse, "PageUpDown", "<Keyboard>/pageDown", "<Keyboard>/pageUp")
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
        public static ESInputActionDefine WithTrigger(
            this ESInputActionDefine config,
            ESInputTriggerFeature features,
            ESInputPressPolicy pressPolicy = ESInputPressPolicy.PressedImmediate,
            float longPressDuration = 0.5f,
            float doublePressWindow = 0.28f)
        {
            if (config == null)
                return null;

            config.triggerFeatures = features;
            config.pressPolicy = pressPolicy;
            config.longPressDuration = longPressDuration;
            config.doublePressWindow = doublePressWindow;
            return config;
        }

        public static ESInputActionDefine WithClick(this ESInputActionDefine config)
        {
            return config.WithTrigger(ESInputTriggerFeature.Pressed, ESInputPressPolicy.PressedImmediate);
        }

        public static ESInputActionDefine WithClickAndRelease(this ESInputActionDefine config)
        {
            return config.WithTrigger(ESInputTriggerFeature.Pressed | ESInputTriggerFeature.Released, ESInputPressPolicy.PressedImmediate);
        }

        public static ESInputActionDefine WithClickAndLongPress(
            this ESInputActionDefine config,
            float longPressDuration = 0.5f,
            ESInputPressPolicy pressPolicy = ESInputPressPolicy.PressedIfNotLongPress)
        {
            return config.WithTrigger(
                ESInputTriggerFeature.Pressed | ESInputTriggerFeature.LongPress,
                pressPolicy,
                longPressDuration);
        }

        public static ESInputActionDefine WithClickAndDoublePress(
            this ESInputActionDefine config,
            float doublePressWindow = 0.28f,
            ESInputPressPolicy pressPolicy = ESInputPressPolicy.PressedImmediate)
        {
            return config.WithTrigger(
                ESInputTriggerFeature.Pressed | ESInputTriggerFeature.DoublePress,
                pressPolicy,
                0.5f,
                doublePressWindow);
        }

        public static ESInputActionDefine WithClickLongAndDoublePress(
            this ESInputActionDefine config,
            float longPressDuration = 0.5f,
            float doublePressWindow = 0.28f,
            ESInputPressPolicy pressPolicy = ESInputPressPolicy.PressedIfNotLongPress)
        {
            return config.WithTrigger(
                ESInputTriggerFeature.Pressed | ESInputTriggerFeature.LongPress | ESInputTriggerFeature.DoublePress,
                pressPolicy,
                longPressDuration,
                doublePressWindow);
        }

        public static ESInputActionDefine WithBinding(this ESInputActionDefine config, string schemeId, string path, string interactions = "", string processors = "")
        {
            ESInputBindingDefine binding = ESInputBindingDefine.InputSystem(schemeId, path, interactions, processors);
            AddBindingWithId(config, binding);
            return config;
        }

        public static ESInputActionDefine WithVirtualBinding(this ESInputActionDefine config, string schemeId, string controlId)
        {
            ESInputBindingDefine binding = ESInputBindingDefine.VirtualControl(schemeId, controlId);
            AddBindingWithId(config, binding);
            return config;
        }

        public static ESInputActionDefine WithUIOnlyVirtualBinding(
            this ESInputActionDefine config,
            string controlId,
            string schemeId = ESInputSchemeIds.Touch)
        {
            if (config == null)
                return null;

            if (config.bindings == null)
                config.bindings = new List<ESInputBindingDefine>();
            else
                config.bindings.Clear();

            return config.WithVirtualBinding(schemeId, controlId);
        }

        public static ESInputActionDefine WithComposite2D(this ESInputActionDefine config, string schemeId, string compositeName, string up, string down, string left, string right)
        {
            AddBindingWithId(config, Composite(schemeId, compositeName, "2DVector"));
            AddBindingWithId(config, CompositePart(schemeId, "Up", up));
            AddBindingWithId(config, CompositePart(schemeId, "Down", down));
            AddBindingWithId(config, CompositePart(schemeId, "Left", left));
            AddBindingWithId(config, CompositePart(schemeId, "Right", right));
            return config;
        }

        public static ESInputActionDefine WithAxisComposite(this ESInputActionDefine config, string schemeId, string compositeName, string negative, string positive)
        {
            AddBindingWithId(config, Composite(schemeId, compositeName, "1DAxis"));
            AddBindingWithId(config, CompositePart(schemeId, "Negative", negative));
            AddBindingWithId(config, CompositePart(schemeId, "Positive", positive));
            return config;
        }

        private static void AddBindingWithId(ESInputActionDefine config, ESInputBindingDefine binding)
        {
            ESInputBindingKeyUtility.EnsureBindingName(binding);
            binding.bindingId = ESInputBindingKeyUtility.MakeBindingId(config, binding, CountExistingBaseBindings(config, binding));
            config.bindings.Add(binding);
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

        private static int CountExistingBaseBindings(ESInputActionDefine config, ESInputBindingDefine binding)
        {
            if (config == null || config.bindings == null)
                return 0;

            string baseKey = ESInputBindingKeyUtility.MakeBindingBaseKey(config, binding);
            int count = 0;
            for (int i = 0; i < config.bindings.Count; i++)
            {
                ESInputBindingDefine item = config.bindings[i];
                if (item == null)
                    continue;

                ESInputBindingKeyUtility.EnsureBindingName(item);
                if (ESInputBindingKeyUtility.MakeBindingBaseKey(config, item) == baseKey)
                    count++;
            }

            return count;
        }
    }
}
