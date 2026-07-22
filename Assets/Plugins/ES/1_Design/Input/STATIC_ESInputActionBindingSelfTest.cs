using System;
using UnityEngine.InputSystem;
using ES.Internal;

namespace ES
{
    public static class ESInputActionBindingSelfTest
    {
        public static string RunAll()
        {
            int checks = 0;

            VerifyCompositeActionBuild(ref checks);
            VerifyNormalBindingExtract(ref checks);
            VerifyCompositeBindingExtract(ref checks);
            VerifyInputSystemSourcePollList(ref checks);
            VerifyRuntimeVirtualSourceLifecycle(ref checks);

            return "ES 输入 InputAction 转换自测通过。检查数量: " + checks;
        }

        private static void VerifyCompositeActionBuild(ref int checks)
        {
            ESInputActionDefine move = ESInputActionDefine.Vector2(ESInputActionId.Move, "Move");
            move.bindings.Add(new ESInputBindingDefine
            {
                schemeId = ESInputSchemeIds.KeyboardMouse,
                source = ESInputBindingSource.InputSystem,
                path = "2DVector",
                name = "WASD",
                isComposite = true
            });
            move.bindings.Add(Part("Up", "<Keyboard>/w"));
            move.bindings.Add(Part("Down", "<Keyboard>/s"));
            move.bindings.Add(Part("Left", "<Keyboard>/a"));
            move.bindings.Add(Part("Right", "<Keyboard>/d"));

            using (InputAction action = ESInputActionBindingUtility.CreateInputAction(move, ESInputSchemeIds.KeyboardMouse))
            {
                ExpectEqual(action.name, "Move", "InputAction 名称应来自 actionName。", ref checks);
                ExpectEqual(action.type, InputActionType.Value, "Vector2 动作应构建为 Value 类型。", ref checks);
                ExpectEqual(action.bindings.Count, 5, "WASD 组合应生成 1 个组合父项和 4 个组合子项。", ref checks);

                InputBinding composite = action.bindings[0];
                Expect(composite.isComposite, "第一条应为 2DVector 组合父项。", ref checks);
                ExpectEqual(composite.path, "2DVector", "组合父项 path 应为 InputSystem 标准 2DVector。", ref checks);
                ExpectEqual(composite.name, "WASD", "组合父项 name 应保留为 WASD。", ref checks);

                ExpectPart(action.bindings[1], "Up", "<Keyboard>/w", ESInputSchemeIds.KeyboardMouse, ref checks);
                ExpectPart(action.bindings[2], "Down", "<Keyboard>/s", ESInputSchemeIds.KeyboardMouse, ref checks);
                ExpectPart(action.bindings[3], "Left", "<Keyboard>/a", ESInputSchemeIds.KeyboardMouse, ref checks);
                ExpectPart(action.bindings[4], "Right", "<Keyboard>/d", ESInputSchemeIds.KeyboardMouse, ref checks);
            }
        }

        private static void VerifyNormalBindingExtract(ref int checks)
        {
            InputAction action = new InputAction("Attack", InputActionType.Button);
            action.AddBinding("<Mouse>/leftButton", interactions: "tap", processors: "");
            InputBinding binding = action.bindings[0];

            ESInputBindingDefine extracted = ESInputActionBindingUtility.ExtractBinding(binding, ESInputSchemeIds.KeyboardMouse);
            ExpectEqual(extracted.schemeId, ESInputSchemeIds.KeyboardMouse, "提取普通绑定时应写入方案。", ref checks);
            ExpectEqual(extracted.source, ESInputBindingSource.InputSystem, "提取普通绑定来源应为 InputSystem。", ref checks);
            ExpectEqual(extracted.path, "<Mouse>/leftButton", "提取普通绑定 path 应保持不变。", ref checks);
            ExpectEqual(extracted.interactions, "tap", "提取普通绑定 interactions 应保持不变。", ref checks);
            Expect(!extracted.isComposite, "普通绑定不应是组合父项。", ref checks);
            Expect(!extracted.isPartOfComposite, "普通绑定不应是组合子项。", ref checks);

            action.Dispose();
        }

        private static void VerifyInputSystemSourcePollList(ref int checks)
        {
            TestConfigSource config = new TestConfigSource();
            ESInputActionDefine move = ESInputActionDefine.Vector2(ESInputActionId.Move, "Move");
            move.bindings.Add(Composite(ESInputSchemeIds.KeyboardMouse, "WASD", "2DVector"));
            move.bindings.Add(Part("Up", "<Keyboard>/w"));
            move.bindings.Add(Part("Down", "<Keyboard>/s"));
            move.bindings.Add(Part("Left", "<Keyboard>/a"));
            move.bindings.Add(Part("Right", "<Keyboard>/d"));
            config.actions.Add(move);

            ESInputActionDefine attack = ESInputActionDefine.Button(ESInputActionId.Attack, "Attack");
            attack.bindings.Add(ESInputBindingDefine.InputSystem(ESInputSchemeIds.KeyboardMouse, "<Mouse>/leftButton"));
            config.actions.Add(attack);

            ESInputActionDefine uiSubmit = ESInputActionDefine.Button(ESInputActionId.UISubmit, "UISubmit");
            uiSubmit.bindings.Add(ESInputBindingDefine.InputSystem(ESInputSchemeIds.KeyboardMouse, "<Keyboard>/enter"));
            config.actions.Add(uiSubmit);

            ESInputRuntimeBuildResult build = ESInputUtility.BuildRuntime(config, ESInputSchemeIds.KeyboardMouse);
            ESRuntimeModeService mode = new ESRuntimeModeService();
            ESInputService service = new ESInputService(build.cache, mode);
            using (ESInputSystemSource source = new ESInputSystemSource())
            {
                source.Initialize(build, service);
                ExpectEqual(source.EnabledActionCount, 3, "默认构建应启用 3 个有绑定的 InputAction。", ref checks);
                ExpectEqual(source.PollActionCount, 2, "Gameplay 默认应轮询移动和战斗动作，普通 UI 动作保持静默。", ref checks);

                ESRuntimeModeHandle pause = mode.PushMode(ESRuntimeMode.PauseMenu);
                ExpectEqual(source.PollActionCount, 1, "PauseMenu 后轮询列表应只保留 UI 动作。", ref checks);

                mode.RemoveMode(pause);
                ExpectEqual(source.PollActionCount, 2, "退出 PauseMenu 后轮询列表应恢复到 Gameplay 可用动作。", ref checks);
            }
        }

        private static void VerifyRuntimeVirtualSourceLifecycle(ref int checks)
        {
            TestConfigSource config = new TestConfigSource();
            ESInputActionDefine interact = ESInputActionDefine.Button(ESInputActionId.Interact, "Interact");
            interact.bindings.Add(ESInputBindingDefine.VirtualControl(ESInputSchemeIds.Touch, "InteractButton"));
            config.actions.Add(interact);

            ESInputBindingProfile profile = ESInputProfileIO.CreateDefaultProfile();
            profile.activeSchemeId = ESInputSchemeIds.Touch;
            ESInputRuntimeBuildResult build = ESInputRuntimeBuilder.Build(config, profile, ESInputSchemeIds.KeyboardMouse);
            ESInputService service = new ESInputService();
            ESInputVirtualSource virtualSource = new ESInputVirtualSource();

            service.SetCache(build.cache);
            virtualSource.Initialize(build, service);
            Expect(virtualSource.TryGetHandle("InteractButton", out ESInputVirtualControlHandle handle), "虚拟控件应在初始化后可取得 handle。", ref checks);
            Expect(handle.IsValid, "虚拟控件 handle 应有效。", ref checks);

            virtualSource.PulseButton(handle);
            service.BeginFrame();
            virtualSource.Update(1f);
            service.EndFrame(1f);
            Expect(service.ConsumePressed(ESInputActionId.Interact), "虚拟按钮 Pulse 后应产生一次 pressed。", ref checks);

            service.BeginFrame();
            virtualSource.Update(1.1f);
            service.EndFrame(1.1f);
            Expect(!service.WasPressed(ESInputActionId.Interact), "虚拟按钮 Pulse 下一帧应自然释放，不应重复 pressed。", ref checks);

            virtualSource.ClearAll();
            service.ResetAll();
            virtualSource.PulseButton(handle);
            service.BeginFrame();
            service.EndFrame(1.2f);
            Expect(!service.WasPressed(ESInputActionId.Interact), "虚拟输入未更新时不应写入本帧输入。", ref checks);

            virtualSource.Dispose();
            Expect(!virtualSource.TryGetHandle("InteractButton", out _), "Dispose 后虚拟控件 handle 表应释放。", ref checks);
            virtualSource.PulseButton(ESInputActionId.Interact);
            Expect(true, "Dispose 后 ActionId 虚拟输入入口应静默失败，不应抛异常。", ref checks);
        }

        private static void VerifyCompositeBindingExtract(ref int checks)
        {
            InputAction action = new InputAction("Move", InputActionType.Value);
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s");

            ESInputBindingDefine composite = ESInputActionBindingUtility.ExtractBinding(action.bindings[0], ESInputSchemeIds.KeyboardMouse);
            Expect(composite.isComposite, "提取组合父项时 isComposite 应为 true。", ref checks);
            ExpectEqual(composite.path, "2DVector", "提取组合父项 path 应为 2DVector。", ref checks);

            ESInputBindingDefine part = ESInputActionBindingUtility.ExtractBinding(action.bindings[1], ESInputSchemeIds.KeyboardMouse);
            Expect(part.isPartOfComposite, "提取组合子项时 isPartOfComposite 应为 true。", ref checks);
            ExpectEqual(part.name, "Up", "提取组合子项 name 应为 Up。", ref checks);
            ExpectEqual(part.path, "<Keyboard>/w", "提取组合子项 path 应保持不变。", ref checks);

            action.Dispose();
        }

        private static void ExpectPart(InputBinding binding, string expectedName, string expectedPath, string expectedGroups, ref int checks)
        {
            Expect(binding.isPartOfComposite, expectedName + " 应为组合子项。", ref checks);
            ExpectEqual(binding.name, expectedName, expectedName + " 名称不正确。", ref checks);
            ExpectEqual(binding.path, expectedPath, expectedName + " 路径不正确。", ref checks);
            ExpectEqual(binding.groups, expectedGroups, expectedName + " 方案分组不正确。", ref checks);
        }

        private static ESInputBindingDefine Part(string name, string path)
        {
            return new ESInputBindingDefine
            {
                schemeId = ESInputSchemeIds.KeyboardMouse,
                source = ESInputBindingSource.InputSystem,
                path = path,
                name = name,
                isPartOfComposite = true
            };
        }

        private static ESInputBindingDefine Composite(string schemeId, string name, string path)
        {
            return new ESInputBindingDefine
            {
                schemeId = schemeId,
                source = ESInputBindingSource.InputSystem,
                path = path,
                name = name,
                isComposite = true
            };
        }

        private sealed class TestConfigSource : IESInputRuntimeConfigSource
        {
            public readonly System.Collections.Generic.List<ESInputActionDefine> actions =
                new System.Collections.Generic.List<ESInputActionDefine>();

            public int ActionCount
            {
                get { return actions.Count; }
            }

            public bool TryGetActionDefine(int index, out ESInputActionDefine action)
            {
                if (index >= 0 && index < actions.Count)
                {
                    action = actions[index];
                    return action != null;
                }

                action = null;
                return false;
            }
        }

        private static void Expect(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[ESInputActionBindingSelfTest] " + message);
        }

        private static void ExpectEqual<T>(T actual, T expected, string message, ref int checks)
        {
            checks++;
            if (!Equals(actual, expected))
                throw new InvalidOperationException("[ESInputActionBindingSelfTest] " + message + " Actual=" + actual + " Expected=" + expected);
        }
    }
}
