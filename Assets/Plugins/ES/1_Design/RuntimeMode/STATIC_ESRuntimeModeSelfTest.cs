using System;
using System.Collections.Generic;

namespace ES
{
    public static class ESRuntimeModeSelfTest
    {
        public static string RunAll()
        {
            int checks = 0;
            VerifyDefaultPolicy(ref checks);
            VerifyModeStack(ref checks);
            VerifyModeAndTagPriority(ref checks);
            VerifyHardModeBlocksLowPriorityTag(ref checks);
            VerifyTagDuplicateCounts(ref checks);
            VerifyOwnerRelease(ref checks);
            VerifyEveryTagCanBeTracked(ref checks);

            return "ES RuntimeMode 自测通过。检查数量: " + checks;
        }

        private static void VerifyDefaultPolicy(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            ESRuntimeModePolicy policy = service.CurrentPolicy;

            ExpectEqual(service.CurrentMode, ESRuntimeMode.Gameplay, "空栈默认 CurrentMode 应为 Gameplay。", ref checks);
            ExpectEqual(service.ModeCount, 0, "默认 ModeCount 应为 0。", ref checks);
            ExpectEqual(service.TagCount, 0, "默认 TagCount 应为 0。", ref checks);
            Expect(policy.allowPlayerInput, "默认应允许玩家输入。", ref checks);
            Expect(policy.allowMoveInput, "默认应允许移动输入。", ref checks);
            Expect(policy.allowCameraLook, "默认应允许镜头输入。", ref checks);
            Expect(policy.allowCombatInput, "默认应允许战斗输入。", ref checks);
            Expect(!policy.allowUIInput, "默认不应允许 UI 输入。", ref checks);
            Expect(!policy.showCursor, "默认不应显示鼠标。", ref checks);
            Expect(policy.lockCursor, "默认应锁定鼠标。", ref checks);
            Expect(!policy.pauseWorld, "默认不应暂停世界。", ref checks);
            Expect(policy.showGameplayHud, "默认应显示玩法 HUD。", ref checks);
        }

        private static void VerifyModeStack(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            int changeCount = 0;
            service.OnPolicyChanged += _ => changeCount++;

            ESRuntimeModeHandle pause = service.PushMode(ESRuntimeMode.PauseMenu);
            ESRuntimeModePolicy policy = service.CurrentPolicy;
            ExpectEqual(service.CurrentMode, ESRuntimeMode.PauseMenu, "压入 PauseMenu 后 CurrentMode 应为 PauseMenu。", ref checks);
            Expect(!policy.allowPlayerInput, "PauseMenu 应禁用玩家输入。", ref checks);
            Expect(policy.allowUIInput, "PauseMenu 应允许 UI 输入。", ref checks);
            Expect(policy.showCursor, "PauseMenu 应显示鼠标。", ref checks);
            Expect(!policy.lockCursor, "PauseMenu 应解锁鼠标。", ref checks);
            Expect(policy.pauseWorld, "PauseMenu 应暂停世界。", ref checks);

            ESRuntimeModeHandle loading = service.PushMode(ESRuntimeMode.Loading);
            policy = service.CurrentPolicy;
            ExpectEqual(service.CurrentMode, ESRuntimeMode.Loading, "压入 Loading 后 CurrentMode 应为栈顶 Loading。", ref checks);
            Expect(!policy.allowUIInput, "Loading 应关闭 UI 输入。", ref checks);
            Expect(policy.pauseWorld, "Loading 应暂停世界。", ref checks);

            Expect(service.RemoveMode(loading), "移除 Loading handle 应成功。", ref checks);
            ExpectEqual(service.CurrentMode, ESRuntimeMode.PauseMenu, "移除 Loading 后应回到 PauseMenu。", ref checks);
            Expect(service.CurrentPolicy.allowUIInput, "回到 PauseMenu 后 UI 输入应恢复允许。", ref checks);

            Expect(service.RemoveMode(pause), "移除 PauseMenu handle 应成功。", ref checks);
            ExpectEqual(service.CurrentMode, ESRuntimeMode.Gameplay, "移除所有模式后 CurrentMode 应回到 Gameplay。", ref checks);
            Expect(service.CurrentPolicy.allowPlayerInput, "清空模式后玩家输入应恢复默认允许。", ref checks);
            Expect(changeCount >= 4, "模式切换应触发策略变化事件。", ref checks);
        }

        private static void VerifyModeAndTagPriority(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            service.PushMode(ESRuntimeMode.Inventory);
            Expect(!service.CurrentPolicy.allowMoveInput, "Inventory 应禁用移动输入。", ref checks);
            Expect(service.CurrentPolicy.allowUIInput, "Inventory 应允许 UI 输入。", ref checks);

            ESRuntimeModeTagHandle mounted = service.AddTag(ESRuntimeModeTag.Mounted, priorityOverride: 600);
            Expect(service.CurrentPolicy.allowMoveInput, "高优先级 Mounted Tag 应能覆盖 Inventory 的移动禁用。", ref checks);
            Expect(!service.CurrentPolicy.allowCombatInput, "Mounted Tag 应禁用战斗输入。", ref checks);
            ExpectEqual(service.CurrentTrace.moveInput.decision, ESPermitLaw.AllowEnable, "移动输入获胜规则应来自 Mounted。", ref checks);

            ESRuntimeModeTagHandle stunned = service.AddTag(ESRuntimeModeTag.Stunned, priorityOverride: 600);
            Expect(!service.CurrentPolicy.allowMoveInput, "同优先级后加入的 Stunned 应覆盖 Mounted 并禁用移动。", ref checks);
            ExpectEqual(service.CurrentTrace.moveInput.decision, ESPermitLaw.HardDisable, "移动输入获胜规则应来自 Stunned。", ref checks);

            Expect(service.RemoveTag(stunned), "移除 Stunned Tag 应成功。", ref checks);
            Expect(service.CurrentPolicy.allowMoveInput, "移除 Stunned 后 Mounted 的移动允许应恢复生效。", ref checks);

            Expect(service.RemoveTag(mounted), "移除 Mounted Tag 应成功。", ref checks);
            Expect(!service.CurrentPolicy.allowMoveInput, "移除 Mounted 后应回到 Inventory 的移动禁用。", ref checks);
        }

        private static void VerifyHardModeBlocksLowPriorityTag(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            ESRuntimeModeHandle loading = service.PushMode(ESRuntimeMode.Loading);
            ESRuntimeModeTagHandle combat = service.AddTag(ESRuntimeModeTag.Combat);

            Expect(!service.CurrentPolicy.allowCombatInput, "低优先级 Combat Tag 不应覆盖 Loading 的战斗禁用。", ref checks);
            Expect(service.CurrentPolicy.pauseWorld, "Loading 应强制暂停世界。", ref checks);

            Expect(service.RemoveMode(loading), "移除 Loading 应成功。", ref checks);
            Expect(service.CurrentPolicy.allowCombatInput, "Loading 移除后 Combat Tag 应恢复允许战斗输入。", ref checks);

            Expect(service.RemoveTag(combat), "移除 Combat Tag 应成功。", ref checks);
        }

        private static void VerifyTagDuplicateCounts(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            ESRuntimeModeTagHandle first = service.AddTag(ESRuntimeModeTag.Combat);
            ESRuntimeModeTagHandle second = service.AddTag(ESRuntimeModeTag.Combat);

            Expect(service.ContainsTag(ESRuntimeModeTag.Combat), "重复添加 Combat 后应包含 Combat。", ref checks);
            ExpectEqual(service.TagCount, 2, "重复添加 Combat 后 TagCount 应为 2。", ref checks);

            Expect(service.RemoveTag(first), "移除第一个 Combat handle 应成功。", ref checks);
            Expect(service.ContainsTag(ESRuntimeModeTag.Combat), "只移除一个 Combat 后仍应包含 Combat。", ref checks);

            Expect(service.RemoveTag(second), "移除第二个 Combat handle 应成功。", ref checks);
            Expect(!service.ContainsTag(ESRuntimeModeTag.Combat), "所有 Combat 移除后不应再包含 Combat。", ref checks);

            ESRuntimeModeTagHandle unique;
            Expect(service.TryAddTagUnique(ESRuntimeModeTag.Aiming, out unique), "第一次 TryAddTagUnique Aiming 应成功。", ref checks);
            ESRuntimeModeTagHandle duplicate;
            Expect(!service.TryAddTagUnique(ESRuntimeModeTag.Aiming, out duplicate), "第二次 TryAddTagUnique Aiming 应失败。", ref checks);
            Expect(!duplicate.IsValid, "重复 TryAddTagUnique 返回的 handle 应无效。", ref checks);
        }

        private static void VerifyOwnerRelease(ref int checks)
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            object owner = new object();
            object otherOwner = new object();

            service.PushMode(ESRuntimeMode.Dialogue, owner);
            service.AddTag(ESRuntimeModeTag.Aiming, owner);
            service.PushMode(ESRuntimeMode.PauseMenu, otherOwner);

            ExpectEqual(service.ReleaseAllByOwner(owner), 2, "ReleaseAllByOwner 应释放同 owner 的模式和标记。", ref checks);
            Expect(!service.ContainsMode(ESRuntimeMode.Dialogue), "释放 owner 后不应包含 Dialogue。", ref checks);
            Expect(!service.ContainsTag(ESRuntimeModeTag.Aiming), "释放 owner 后不应包含 Aiming。", ref checks);
            Expect(service.ContainsMode(ESRuntimeMode.PauseMenu), "释放 owner 不应影响其他 owner 的 PauseMenu。", ref checks);
        }

        private static void VerifyEveryTagCanBeTracked(ref int checks)
        {
            Array values = Enum.GetValues(typeof(ESRuntimeModeTag));
            for (int i = 0; i < values.Length; i++)
            {
                ESRuntimeModeTag tag = (ESRuntimeModeTag)values.GetValue(i);
                ESRuntimeModeService service = new ESRuntimeModeService();
                ESRuntimeModeTagHandle handle = service.AddTag(tag);

                Expect(service.ContainsTag(tag), "Tag 计数表必须能追踪 " + tag, ref checks);
                Expect(service.RemoveTag(handle), "移除 Tag handle 应成功: " + tag, ref checks);
                Expect(!service.ContainsTag(tag), "移除后不应再包含 Tag: " + tag, ref checks);
            }
        }

        private static void Expect(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[ESRuntimeModeSelfTest] " + message);
        }

        private static void ExpectEqual<T>(T actual, T expected, string message, ref int checks)
        {
            checks++;
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
                throw new InvalidOperationException("[ESRuntimeModeSelfTest] " + message + " Actual=" + actual + " Expected=" + expected);
        }
    }
}
