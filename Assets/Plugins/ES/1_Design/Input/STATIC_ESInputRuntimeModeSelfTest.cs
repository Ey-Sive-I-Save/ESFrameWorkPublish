using System;
using System.Collections.Generic;

namespace ES
{
    public static class ESInputRuntimeModeSelfTest
    {
        public static string RunAll()
        {
            int checks = 0;
            VerifyPauseMenuAllowsUIOnly(ref checks);
            VerifyLoadingBlocksUIAndGameplay(ref checks);
            VerifyHeldButtonDoesNotFireAfterBlockedMode(ref checks);
            VerifyNoModeServiceMeansNoModeFilter(ref checks);

            return "ESInputRuntimeModeSelfTest passed. Checks: " + checks;
        }

        private static void VerifyPauseMenuAllowsUIOnly(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            mode.PushMode(ESRuntimeMode.PauseMenu);

            Expect(!input.IsActionAllowed(ESInputActionId.Move), "PauseMenu 应禁用 Move 输入", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Attack), "PauseMenu 应禁用 Combat 输入", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Look), "PauseMenu 应允许 UI 输入", ref checks);
        }

        private static void VerifyLoadingBlocksUIAndGameplay(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            mode.PushMode(ESRuntimeMode.Loading);

            Expect(!input.IsActionAllowed(ESInputActionId.Move), "Loading 应禁用 Move 输入", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Attack), "Loading 应禁用 Combat 输入", ref checks);
            Expect(!input.IsActionAllowed(ESInputActionId.Look), "Loading 应禁用 UI 输入", ref checks);
        }

        private static void VerifyHeldButtonDoesNotFireAfterBlockedMode(ref int checks)
        {
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService input = CreateInputService(mode);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0f);
            input.EndFrame(0f);
            Expect(input.WasPressed(ESInputActionId.Attack), "默认模式下 Attack 按下应触发", ref checks);

            ESRuntimeModeHandle pause = mode.PushMode(ESRuntimeMode.PauseMenu);
            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.1f);
            input.EndFrame(0.1f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "PauseMenu 中 Attack 不应触发", ref checks);
            Expect(!input.IsHeld(ESInputActionId.Attack), "PauseMenu 中 Attack 不应保持 held", ref checks);

            mode.RemoveMode(pause);
            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.2f);
            input.EndFrame(0.2f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "退出 PauseMenu 后持续按住 Attack 不应补触发", ref checks);
            Expect(!input.IsHeld(ESInputActionId.Attack), "退出 PauseMenu 后持续按住 Attack 仍应等待松开", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, false, 0.3f);
            input.EndFrame(0.3f);
            Expect(!input.WasPressed(ESInputActionId.Attack), "松开 Attack 不应触发 pressed", ref checks);

            input.BeginFrame();
            input.WriteButton(ESInputActionId.Attack, true, 0.4f);
            input.EndFrame(0.4f);
            Expect(input.WasPressed(ESInputActionId.Attack), "松开后再次按下 Attack 应正常触发", ref checks);
        }

        private static void VerifyNoModeServiceMeansNoModeFilter(ref int checks)
        {
            ESInputService input = CreateInputService(null);

            Expect(input.IsActionAllowed(ESInputActionId.Move), "没有 ModeService 时 Move 不应被模式过滤", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Attack), "没有 ModeService 时 Combat 不应被模式过滤", ref checks);
            Expect(input.IsActionAllowed(ESInputActionId.Look), "没有 ModeService 时 UI 不应被模式过滤", ref checks);
        }

        private static ESInputService CreateInputService(ESRuntimeModeService mode)
        {
            ESInputRuntimeCache cache = new ESInputRuntimeCache(3);
            cache.metas[(int)ESInputActionId.Move] = new ESInputActionMeta
            {
                id = ESInputActionId.Move,
                valueType = ESInputValueType.Vector2,
                category = ESInputActionCategory.Move
            };
            cache.metas[(int)ESInputActionId.Look] = new ESInputActionMeta
            {
                id = ESInputActionId.Look,
                valueType = ESInputValueType.Button,
                category = ESInputActionCategory.UI
            };
            cache.metas[(int)ESInputActionId.Attack] = new ESInputActionMeta
            {
                id = ESInputActionId.Attack,
                valueType = ESInputValueType.Button,
                category = ESInputActionCategory.Combat,
                triggerFeatures = ESInputTriggerFeature.Pressed,
                pressPolicy = ESInputPressPolicy.PressedImmediate
            };

            return new ESInputService(cache, mode);
        }

        private static void Expect(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[ESInputRuntimeModeSelfTest] " + message);
        }
    }
}
