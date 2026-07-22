using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ESInputSchemeDefine
    {
        [LabelText("方案 ID")]
        public string schemeId = ESInputSchemeIds.KeyboardMouse;

        [LabelText("显示名称")]
        public string displayName = "键盘鼠标";

        [LabelText("设备类型")]
        public ESInputDeviceKind deviceKind = ESInputDeviceKind.KeyboardMouse;

        [LabelText("绑定分组")]
        public string bindingGroup = ESInputSchemeIds.KeyboardMouse;
    }

    [Serializable]
    public sealed class ESInputActionDefine
    {
        [HorizontalGroup("动作", Width = 120)]
        [LabelText("动作 ID")]
        public ESInputActionId id;

        [HorizontalGroup("动作")]
        [LabelText("内部名称")]
        public string actionName;

        [HorizontalGroup("动作", Width = 100)]
        [LabelText("值类型")]
        public ESInputValueType valueType;

        [LabelText("动作分类")]
        public ESInputActionCategory category = ESInputActionCategory.Common;

        [LabelText("允许改键")]
        public bool allowRebind = true;

        [LabelText("显示名称")]
        public string displayName;

        [LabelText("旧触发方式")]
        public ESInputTriggerType triggerType = ESInputTriggerType.Pressed;

        [LabelText("触发方式")]
        public ESInputTriggerFeature triggerFeatures = ESInputTriggerFeature.None;

        [LabelText("短按策略")]
        public ESInputPressPolicy pressPolicy = ESInputPressPolicy.PressedImmediate;

        [MinValue(0f)]
        [ShowIf(nameof(UsesLongPress))]
        [LabelText("长按秒数")]
        public float longPressDuration = 0.5f;

        [MinValue(0f)]
        [ShowIf(nameof(UsesDoublePress))]
        [LabelText("双击窗口")]
        public float doublePressWindow = 0.28f;

        [LabelText("绑定列表")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        public List<ESInputBindingDefine> bindings = new List<ESInputBindingDefine>();

        public static ESInputActionDefine Value(ESInputActionId id, string actionName, ESInputValueType valueType)
        {
            return new ESInputActionDefine
            {
                id = id,
                actionName = actionName,
                valueType = valueType,
                category = ESInputDefineUtility.GuessCategory(id),
                displayName = ESInputDefineUtility.GetDefaultChineseName(id),
                triggerFeatures = ESInputTriggerFeature.Pressed,
                pressPolicy = ESInputPressPolicy.PressedImmediate,
                allowRebind = true
            };
        }

        public static ESInputActionDefine Button(ESInputActionId id, string actionName)
        {
            return Value(id, actionName, ESInputValueType.Button);
        }

        public static ESInputActionDefine Axis(ESInputActionId id, string actionName)
        {
            return Value(id, actionName, ESInputValueType.Axis);
        }

        public static ESInputActionDefine Vector2(ESInputActionId id, string actionName)
        {
            return Value(id, actionName, ESInputValueType.Vector2);
        }

        public ESInputTriggerFeature GetEffectiveTriggerFeatures()
        {
            return triggerFeatures != ESInputTriggerFeature.None
                ? triggerFeatures
                : ConvertLegacyTriggerType(triggerType);
        }

        public void NormalizeTriggerSettings()
        {
            if (triggerFeatures == ESInputTriggerFeature.None)
                triggerFeatures = ConvertLegacyTriggerType(triggerType);
        }

        public static ESInputTriggerFeature ConvertLegacyTriggerType(ESInputTriggerType type)
        {
            switch (type)
            {
                case ESInputTriggerType.Released:
                    return ESInputTriggerFeature.Released;
                case ESInputTriggerType.Held:
                    return ESInputTriggerFeature.Held;
                case ESInputTriggerType.LongPress:
                    return ESInputTriggerFeature.LongPress;
                case ESInputTriggerType.DoublePress:
                    return ESInputTriggerFeature.DoublePress;
                default:
                    return ESInputTriggerFeature.Pressed;
            }
        }

        private bool UsesLongPress()
        {
            return (GetEffectiveTriggerFeatures() & ESInputTriggerFeature.LongPress) != 0;
        }

        private bool UsesDoublePress()
        {
            return (GetEffectiveTriggerFeatures() & ESInputTriggerFeature.DoublePress) != 0;
        }
    }

    [Serializable]
    public sealed class ESInputBindingDefine
    {
        [UnityEngine.HideInInspector]
        public string bindingId;

        [LabelText("方案 ID")]
        public string schemeId;

        [LabelText("来源")]
        public ESInputBindingSource source = ESInputBindingSource.InputSystem;

        [LabelText("InputSystem 路径")]
        public string path;

        [LabelText("虚拟控件 ID")]
        public string virtualControlId;

        [LabelText("交互参数")]
        public string interactions;

        [LabelText("处理器")]
        public string processors;

        [LabelText("组合绑定")]
        public bool isComposite;

        [LabelText("组合部分")]
        public bool isPartOfComposite;

        [LabelText("名称")]
        public string name;

        public static ESInputBindingDefine InputSystem(string schemeId, string path, string interactions = "", string processors = "", string bindingId = null)
        {
            return new ESInputBindingDefine
            {
                bindingId = bindingId,
                schemeId = schemeId,
                source = ESInputBindingSource.InputSystem,
                path = path,
                interactions = interactions,
                processors = processors
            };
        }

        public static ESInputBindingDefine VirtualControl(string schemeId, string controlId, string bindingId = null)
        {
            return new ESInputBindingDefine
            {
                bindingId = bindingId,
                schemeId = schemeId,
                source = ESInputBindingSource.VirtualControl,
                virtualControlId = controlId
            };
        }
    }

    public static class ESInputDefineUtility
    {
        public static ESInputActionCategory GuessCategory(ESInputActionId id)
        {
            switch (id)
            {
                case ESInputActionId.Move:
                case ESInputActionId.Jump:
                case ESInputActionId.Crouch:
                    return ESInputActionCategory.Move;

                case ESInputActionId.Look:
                    return ESInputActionCategory.CameraLook;

                case ESInputActionId.Interact:
                    return ESInputActionCategory.Interaction;

                case ESInputActionId.Fly:
                case ESInputActionId.FlyVertical:
                case ESInputActionId.Mount:
                case ESInputActionId.Climb:
                    return ESInputActionCategory.SpecialMove;

                case ESInputActionId.UISubmit:
                case ESInputActionId.UICancel:
                case ESInputActionId.UINavigate:
                case ESInputActionId.UIPoint:
                case ESInputActionId.UIClick:
                case ESInputActionId.UIScroll:
                    return ESInputActionCategory.UI;

                case ESInputActionId.Attack:
                case ESInputActionId.HeavyAttack:
                case ESInputActionId.Block:
                case ESInputActionId.Slide:
                case ESInputActionId.SwitchWeapon:
                case ESInputActionId.EquipWeapon:
                case ESInputActionId.HolsterWeapon:
                case ESInputActionId.WeaponSlot1:
                case ESInputActionId.WeaponSlot2:
                case ESInputActionId.WeaponSlot3:
                case ESInputActionId.WeaponSlot4:
                case ESInputActionId.WeaponSlot5:
                case ESInputActionId.Aim:
                case ESInputActionId.PeekLeft:
                case ESInputActionId.PeekRight:
                case ESInputActionId.Skill1:
                case ESInputActionId.Skill2:
                case ESInputActionId.Skill3:
                    return ESInputActionCategory.Combat;

                default:
                    return ESInputActionCategory.Common;
            }
        }

        public static string GetDefaultChineseName(ESInputActionId id)
        {
            switch (id)
            {
                case ESInputActionId.Move: return "移动";
                case ESInputActionId.Look: return "视角";
                case ESInputActionId.Attack: return "攻击";
                case ESInputActionId.HeavyAttack: return "重击";
                case ESInputActionId.Block: return "格挡";
                case ESInputActionId.Slide: return "滑行";
                case ESInputActionId.SwitchWeapon: return "切换武器";
                case ESInputActionId.EquipWeapon: return "装备武器";
                case ESInputActionId.HolsterWeapon: return "收起武器";
                case ESInputActionId.WeaponSlot1: return "武器槽 1";
                case ESInputActionId.WeaponSlot2: return "武器槽 2";
                case ESInputActionId.WeaponSlot3: return "武器槽 3";
                case ESInputActionId.WeaponSlot4: return "武器槽 4";
                case ESInputActionId.WeaponSlot5: return "武器槽 5";
                case ESInputActionId.Aim: return "瞄准";
                case ESInputActionId.PeekLeft: return "左探头";
                case ESInputActionId.PeekRight: return "右探头";
                case ESInputActionId.Skill1: return "技能 1";
                case ESInputActionId.Skill2: return "技能 2";
                case ESInputActionId.Skill3: return "技能 3";
                case ESInputActionId.Jump: return "跳跃";
                case ESInputActionId.Crouch: return "蹲伏";
                case ESInputActionId.Fly: return "飞行";
                case ESInputActionId.Mount: return "骑乘";
                case ESInputActionId.FlyVertical: return "飞行垂直轴";
                case ESInputActionId.Climb: return "攀爬";
                case ESInputActionId.Interact: return "交互";
                case ESInputActionId.UISubmit: return "UI 提交";
                case ESInputActionId.UICancel: return "UI 取消";
                case ESInputActionId.UINavigate: return "UI 导航";
                case ESInputActionId.UIPoint: return "UI 指针";
                case ESInputActionId.UIClick: return "UI 点击";
                case ESInputActionId.UIScroll: return "UI 滚动";
                default: return id.ToString();
            }
        }
    }
}
