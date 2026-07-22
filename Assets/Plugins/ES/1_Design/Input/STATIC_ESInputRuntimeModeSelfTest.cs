using System;
using System.Collections.Generic;

namespace ES
{
    public static class ESInputRuntimeModeSelfTest
    {
        #region 对外入口

        public static string RunAll()
        {
            int checks = 0;

            VerifyActionCategories(ref checks);
            VerifyPauseMenuAllowsUIOnly(ref checks);
            VerifyLoadingBlocksUIAndGameplay(ref checks);
            VerifyHeldButtonDoesNotFireAfterBlockedMode(ref checks);
            VerifyLongPressTrigger(ref checks);
            VerifyDoublePressTrigger(ref checks);
            VerifyNoModeServiceMeansNoModeFilter(ref checks);

            return "ES 输入运行时自测通过。检查数量: " + checks;
        }

        #endregion

        #region 动作分类

        private static void VerifyActionCategories(ref int checks)
        {
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Move), ESInputActionCategory.Move, "Move 应属于移动分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Jump), ESInputActionCategory.Move, "Jump 应属于移动分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Look), ESInputActionCategory.CameraLook, "Look 应属于视角分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Attack), ESInputActionCategory.Combat, "Attack 应属于战斗分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Skill1), ESInputActionCategory.Combat, "Skill1 应属于战斗分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Interact), ESInputActionCategory.Interaction, "Interact 应属于交互分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.Climb), ESInputActionCategory.SpecialMove, "Climb 应属于特殊移动分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.UISubmit), ESInputActionCategory.UI, "UISubmit 应属于 UI 分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.UINavigate), ESInputActionCategory.UI, "UINavigate 应属于 UI 分类。", ref checks);
            ExpectEqual(ESInputDefineUtility.GuessCategory(ESInputActionId.UIScroll), ESInputActionCategory.UI, "UIScroll 应属于 UI 分类。", ref checks);
        }

        #endregion

        #region RuntimeMode 过滤

        private static void VerifyPauseMenuAllowsUIOnly(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            mode.PushMode(ESRuntimeMode.PauseMenu);

            Expect(!input.IsActionAllowed(ESInputActionId.Move), "暂停菜单应禁止移动输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Look), "暂停菜单应禁止视角输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Attack), "暂停菜单应禁止战斗输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Interact), "暂停菜单应禁止交互输入。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.UISubmit), "暂停菜单应允许 UI 提交。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.UINavigate), "暂停菜单应允许 UI 导航。", ref checks);
        }

        private static void VerifyLoadingBlocksUIAndGameplay(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            mode.PushMode(ESRuntimeMode.Loading);

            Expect(!input.IsActionAllowed(ESInputActionId.Move), "加载中应禁止移动输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Look), "加载中应禁止视角输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Attack), "加载中应禁止战斗输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Interact), "加载中应禁止交互输入。", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.UISubmit), "加载中应禁止普通 UI 输入。", ref checks);
        }

        private static void VerifyHeldButtonDoesNotFireAfterBlockedMode(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0f);
            input.EndFrame(0f);
            Expect(input.WasPressed(ESInputActionId.Attack), "默认模式下按下攻击应触发。", ref checks);

            ESRuntimeModeHandle pause = mode.PushMode(ESRuntimeMode.PauseMenu);
            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.1f);
            input.EndFrame(0.1f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "暂停菜单中攻击不应触发。", ref checks);
            Expect(!input.IsHeld(ESInputActionId.Attack), "暂停菜单中攻击不应保持按住状态。", ref checks);

            mode.RemoveMode(pause);
            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.2f);
            input.EndFrame(0.2f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "退出暂停后，持续按住的攻击不应补触发。", ref checks);
            Expect(!input.IsHeld(ESInputActionId.Attack), "退出暂停后，持续按住的攻击仍应等待松开。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, false, 0.3f);
            input.EndFrame(0.3f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "等待松开的攻击在松开帧不应触发 pressed。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.4f);
            input.EndFrame(0.4f);
            Expect(input.WasPressed(ESInputActionId.Attack), "松开后再次按下攻击应正常触发。", ref checks);
        }

        private static void VerifyNoModeServiceMeansNoModeFilter(ref int checks)
        {
            ESInputService input = CreateInputService(null);

            Expect(input.IsActionAllowed(ESInputActionId.Move), "没有 ModeService 时，移动不应被模式过滤。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Look), "没有 ModeService 时，视角不应被模式过滤。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Attack), "没有 ModeService 时，战斗不应被模式过滤。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Interact), "没有 ModeService 时，交互不应被模式过滤。", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.UISubmit), "没有 ModeService 时，UI 不应被模式过滤。", ref checks);
        }

        #endregion

        #region 触发方式

        private static void VerifyLongPressTrigger(ref int checks)
        {
            ESInputService input = CreateInputService(null);
            input.Cache.metas[(int)ESInputActionId.Attack].triggerFeatures =
                ESInputTriggerFeature.Pressed | ESInputTriggerFeature.LongPress;
            input.Cache.metas[(int)ESInputActionId.Attack].pressPolicy = ESInputPressPolicy.PressedIfNotLongPress;
            input.Cache.metas[(int)ESInputActionId.Attack].longPressDuration = 0.5f;

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1f);
            input.EndFrame(1f);
            Expect(!input.WasLongPressed(ESInputActionId.Attack), "长按不应在按下第一帧触发。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1.49f);
            input.EndFrame(1.49f);
            Expect(!input.WasLongPressed(ESInputActionId.Attack), "长按未达到时间前不应触发。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1.5f);
            input.EndFrame(1.5f);
            Expect(input.ConsumeLongPressed(ESInputActionId.Attack), "长按达到时间后应触发。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1.6f);
            input.EndFrame(1.6f);
            Expect(!input.WasLongPressed(ESInputActionId.Attack), "同一次按住期间长按只应触发一次。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, false, 1.7f);
            input.EndFrame(1.7f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "长按触发后，短按不应在松开时补触发。", ref checks);
        }

        private static void VerifyDoublePressTrigger(ref int checks)
        {
            ESInputService input = CreateInputService(null);
            input.Cache.metas[(int)ESInputActionId.Attack].triggerFeatures =
                ESInputTriggerFeature.Pressed | ESInputTriggerFeature.DoublePress;
            input.Cache.metas[(int)ESInputActionId.Attack].doublePressWindow = 0.28f;

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1f);
            input.EndFrame(1f);
            Expect(!input.WasDoublePressed(ESInputActionId.Attack), "第一次按下不应算双击。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, false, 1.05f);
            input.EndFrame(1.05f);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1.2f);
            input.EndFrame(1.2f);
            Expect(input.ConsumeDoublePressed(ESInputActionId.Attack), "双击窗口内第二次按下应触发双击。", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, false, 1.25f);
            input.EndFrame(1.25f);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 1.7f);
            input.EndFrame(1.7f);
            Expect(!input.WasDoublePressed(ESInputActionId.Attack), "超过双击窗口后不应触发双击。", ref checks);
        }

        #endregion

        #region 测试工具

        private static ESInputService CreateInputService(ESRuntimeModeService mode)
        {
            ESInputRuntimeCache cache = new ESInputRuntimeCache((int)ESInputActionId.UIScroll + 1);
            FillMeta(cache, ESInputActionId.Move, ESInputValueType.Vector2);
            FillMeta(cache, ESInputActionId.Look, ESInputValueType.Vector2);
            FillMeta(cache, ESInputActionId.Attack, ESInputValueType.Button);
            FillMeta(cache, ESInputActionId.Interact, ESInputValueType.Button);
            FillMeta(cache, ESInputActionId.UISubmit, ESInputValueType.Button);
            FillMeta(cache, ESInputActionId.UINavigate, ESInputValueType.Vector2);
            FillMeta(cache, ESInputActionId.UIScroll, ESInputValueType.Vector2);
            return new ESInputService(cache, mode);
        }

        private static void FillMeta(ESInputRuntimeCache cache, ESInputActionId id, ESInputValueType valueType)
        {
            cache.metas[(int)id] = new ESInputActionMeta
            {
                id = id,
                valueType = valueType,
                category = ESInputDefineUtility.GuessCategory(id),
                triggerFeatures = ESInputTriggerFeature.Pressed,
                pressPolicy = ESInputPressPolicy.PressedImmediate
            };
        }

        private static void Expect(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[ESInputRuntimeModeSelfTest] " + message);
        }

        private static void ExpectEqual<T>(T actual, T expected, string message, ref int checks)
        {
            checks++;
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
                throw new InvalidOperationException("[ESInputRuntimeModeSelfTest] " + message + " Actual=" + actual + " Expected=" + expected);
        }

        #endregion
    }
}
