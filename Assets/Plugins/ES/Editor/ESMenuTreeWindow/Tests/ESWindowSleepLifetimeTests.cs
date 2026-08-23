using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ES.EditorInternal;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ES.Tests
{
    public sealed class ESWindowSleepLifetimeProbeWindow : EditorWindow,
        IESWindowMultiInstanceContract
    {
        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => nameof(ESWindowSleepLifetimeTests);
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, "test full contract")]
    public sealed class ESWindowFullContractProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(ESWindowSleepMode.Transient, "test transient contract")]
    public sealed class ESWindowTransientContractProbeWindow : EditorWindow
    {
    }

    public sealed class ESWindowCoordinatorIdentityProbeWindow : EditorWindow,
        IESWindowMultiInstanceContract
    {
        public string CoordinatorId;

        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => CoordinatorId;
    }

    public sealed class ESWindowDuplicateHealthProbeWindow : EditorWindow
    {
    }

    public sealed class ESWindowShortTitleProbeWindow : EditorWindow,
        IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "契约";
    }

    [ESWindowPresentationShortTitle("标记")]
    public sealed class ESWindowAttributeShortTitleProbeWindow : EditorWindow
    {
    }

    public sealed class ESWindowTabLabelProbeWindow : EditorWindow,
        IESWindowPresentationTabLabel,
        IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationTabLabel => "世界";
        public string ESWindow_PresentationShortTitle => "旧标题";
    }

    public sealed class ESWindowSleepLifetimeTests
    {
        [Test]
        public void DeclaredSleepContractRejectsMismatchedBindingMode()
        {
            ESWindowFullContractProbeWindow full =
                ScriptableObject.CreateInstance<ESWindowFullContractProbeWindow>();
            ESWindowTransientContractProbeWindow transient =
                ScriptableObject.CreateInstance<ESWindowTransientContractProbeWindow>();
            try
            {
                Assert.AreEqual(
                    ESWindowSleepMode.Full,
                    ESWindowFoundation.GetDeclaredSleepMode(full));
                Assert.AreEqual(
                    ESWindowSleepMode.Transient,
                    ESWindowFoundation.GetDeclaredSleepMode(transient));
                Assert.DoesNotThrow(() => ESWindowFoundation.BindFullSleep(full));
                Assert.DoesNotThrow(() => ESWindowFoundation.BindTransient(transient));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(transient));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(full, allowSemiSleep: false));

            }
            finally
            {
                ESWindowFoundation.Unbind(transient, true);
                ESWindowFoundation.Unbind(full, true);
                UnityEngine.Object.DestroyImmediate(transient);
                UnityEngine.Object.DestroyImmediate(full);
            }
        }

        [Test]
        public void DirectProductionEditorWindowsDefaultToFullAndTransientOptOutIsExplicit()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string[] sourceFiles = EnumerateESProductionEditorSources(projectRoot);
            int directWindowFileCount = 0;
            foreach (string path in sourceFiles)
            {
                string normalized = NormalizeProjectPath(path);
                if ((!normalized.Contains("/Plugins/ES/Editor/", StringComparison.OrdinalIgnoreCase)
                        && !normalized.Contains("/Scripts/ESLogic/Editor/", StringComparison.OrdinalIgnoreCase))
                    || normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/-Templates/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (!Regex.IsMatch(
                        source,
                        @"class\s+[^\r\n]*:\s*[^\r\n]*\b(?:EditorWindow|OdinEditorWindow)\b",
                        RegexOptions.CultureInvariant))
                    continue;

                directWindowFileCount++;
                bool hasContract = source.Contains(
                    "ESWindowSleepContract(",
                    StringComparison.Ordinal);
                bool hasBinding = source.Contains(
                        "BindWindow(this",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.Bind(",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.BindWithStandardSystemHost(",
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    hasBinding,
                    "直接生产窗口必须显式声明 ES 生命周期绑定：" + normalized);

                bool bindsTransient = source.Contains(
                    "allowSemiSleep: false",
                    StringComparison.Ordinal);
                if (bindsTransient)
                {
                    Assert.IsTrue(
                        hasContract,
                        "关闭休眠的直接窗口必须显式声明 Transient 及原因：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepMode.Transient",
                        source,
                        "Transient 窗口合同模式无法解析：" + normalized);
                }
                else if (hasContract)
                {
                    Assert.IsFalse(
                        source.Contains(
                            "ESWindowSleepMode.Transient",
                            StringComparison.Ordinal),
                        "未关闭休眠的窗口不应声明 Transient：" + normalized);
                }
            }

            Assert.GreaterOrEqual(
                directWindowFileCount,
                12,
                "直接 EditorWindow 扫描结果异常，可能漏掉了生产窗口。");
        }

        [Test]
        public void UnmarkedWindowDefaultsToFullSleep()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                Assert.IsNull(ESWindowFoundation.GetDeclaredSleepMode(window));
                ESWindowFoundation.Bind(window);
                Assert.IsTrue(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EdgeTabShortTitleSupportsSemanticContractAndPersistentOverride()
        {
            Assert.AreEqual(
                "世界构建",
                ESEditorPresentation.BuildDefaultPresentationShortTitle(
                    "环境/世界构建工作台"));

            ESWindowShortTitleProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowShortTitleProbeWindow>();
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);

            try
            {
                ESWindowFoundation.Bind(window);
                Assert.IsTrue(
                    ESWindowFoundation.IsWindowSleepSupported(window),
                    "首次绑定必须立即建立休眠能力，不能依赖窗口进行第二次绑定。");
                Assert.AreEqual("契约", ESWindowFoundation.GetPresentationShortTitle(window));

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, "世界"));
                Assert.AreEqual("世界", ESWindowFoundation.GetPresentationShortTitle(window));
                StringAssert.Contains(
                    "\"presentationShortTitle\":\"世界\"",
                    EditorPrefs.GetString(preferenceKey));

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(
                    window,
                    "这是一个远超页签宽度的短标题"));
                Assert.AreEqual(
                    "这是一个远超页签",
                    ESWindowFoundation.GetPresentationShortTitle(window),
                    "用户覆盖仍须保持极限页签的紧凑边界。");

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, null));
                Assert.AreEqual("契约", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TabLabelContractHasPriorityWithoutChangingWindowIdentity()
        {
            ESWindowTabLabelProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowTabLabelProbeWindow>();
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);
            try
            {
                ESWindowFoundation.Bind(window);
                Assert.AreEqual("世界", ESWindowFoundation.GetPresentationShortTitle(window));
                Assert.AreEqual(typeof(ESWindowTabLabelProbeWindow), window.GetType());
                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, "自定义"));
                Assert.AreEqual("自定义", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DefaultShortTitlePrefersSemanticDomainWords()
        {
            Assert.AreEqual(
                "资源",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES 资源诊断"));
            Assert.AreEqual(
                "Agent",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Codex 会话控制台"));
            Assert.AreEqual(
                "世界构建",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("环境/世界构建工作台"));
            Assert.AreEqual(
                "材质",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("Composite Material Bake"));
            Assert.AreEqual(
                "诊断",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Runtime Diagnostic Monitor"));
            Assert.AreEqual(
                "层级",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("Hierarchy Object Tools"));
            Assert.AreEqual(
                "运行时",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Runtime Watch"));
        }

        [Test]
        public void AttributeShortTitleIsUsedWhenWindowDoesNotImplementInterface()
        {
            ESWindowAttributeShortTitleProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowAttributeShortTitleProbeWindow>();
            try
            {
                ESWindowFoundation.Bind(window);
                Assert.AreEqual("标记", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UngovernedSecondWindowInstanceCannotJoinSleepPersistence()
        {
            ESWindowDuplicateHealthProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            ESWindowDuplicateHealthProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            try
            {
                ESWindowFoundation.Bind(first);
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("同一 EditorWindow 具体类型出现多个实例：.*额外实例"));
                ESWindowFoundation.Bind(second);

                bool firstViolation = ESWindowFoundation.IsWindowSingleInstanceViolation(first);
                bool secondViolation = ESWindowFoundation.IsWindowSingleInstanceViolation(second);
                Assert.AreNotEqual(firstViolation, secondViolation,
                    "同一无协调契约类型只能有一个实例拥有休眠与持久化所有权。");

                ESWindowDuplicateHealthProbeWindow primary = firstViolation ? second : first;
                ESWindowDuplicateHealthProbeWindow duplicate = firstViolation ? first : second;
                Assert.IsFalse(ESWindowFoundation.CanWindowSleep(duplicate));
                StringAssert.Contains(
                    "额外实例",
                    ESWindowFoundation.GetWindowSleepBlockReason(duplicate));

                ESWindowFoundation.Unbind(primary, true);
                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(duplicate),
                    "首实例退出后，剩余实例应确定性接管唯一实例所有权。");
            }
            finally
            {
                ESWindowFoundation.Unbind(second, true);
                ESWindowFoundation.Unbind(first, true);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void GovernedMultiInstanceRequiresOneStableCoordinatorIdentity()
        {
            ESWindowCoordinatorIdentityProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowCoordinatorIdentityProbeWindow>();
            ESWindowCoordinatorIdentityProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowCoordinatorIdentityProbeWindow>();
            try
            {
                first.CoordinatorId = " ES.Tests.Coordinator ";
                second.CoordinatorId = "ES.Tests.Coordinator";
                ESWindowFoundation.Bind(first);
                ESWindowFoundation.Bind(second);

                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(first));
                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(second));

                ESWindowFoundation.Unbind(second, true);
                second.CoordinatorId = "ES.Tests.OtherCoordinator";
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("同一 EditorWindow 具体类型出现多个实例：.*额外实例"));
                ESWindowFoundation.Bind(second);

                Assert.AreNotEqual(
                    ESWindowFoundation.IsWindowSingleInstanceViolation(first),
                    ESWindowFoundation.IsWindowSingleInstanceViolation(second),
                    "同类型实例的协调器 ID 不一致时，只能保留一个单实例所有者。");
            }
            finally
            {
                ESWindowFoundation.Unbind(first, true);
                ESWindowFoundation.Unbind(second, true);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void StandardSystemHostBindingIsExplicitAndIdempotent()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);

            try
            {
                ESWindowActionHosts first = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    actionBar);
                ESWindowActionHosts second = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    actionBar);

                Assert.AreSame(first.System, second.System);
                Assert.AreSame(actionBar, first.System.parent);
                Assert.IsNotNull(first.System.Q<VisualElement>("ESWindowSystemActions"));
                Assert.AreEqual(
                    "系统",
                    first.System.Q<ToolbarMenu>("ESWindowSystemActionsOverflow").text);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>(
                    "ESWindowSystemActionsFallback"));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StandardSystemActionBarFactoryUsesNormalFlowAndExplicitOwnership()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                VisualElement bar = ESWindowFoundation.EnsureStandardSystemActionBar(window);
                Assert.AreSame(window.rootVisualElement, bar.parent);
                Assert.AreEqual(Position.Relative, bar.style.position.value);
                Assert.AreEqual(0f, bar.style.top.value.value, 0.001f);
                ESWindowActionHosts hosts = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    bar);
                Assert.AreSame(bar, hosts.System.parent);
                Assert.IsNotNull(bar.Q<VisualElement>("ESWindowStandardSystemActionHost"));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DirectESBindingGetsNormalFlowSystemSleepControls()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.Bind(window);

                VisualElement bar = window.rootVisualElement.Q<VisualElement>(
                    "ESWindowStandardSystemActionBar");
                Assert.IsNotNull(bar);
                Assert.AreEqual(Position.Relative, bar.style.position.value);
                Assert.IsNotNull(bar.Q<VisualElement>(
                    "ESWindowStandardSystemActionHost"));
                VisualElement systemActions = window.rootVisualElement.Q<VisualElement>(
                    "ESWindowSystemActions");
                Assert.IsNotNull(systemActions);
                ToolbarMenu overflow = systemActions.Q<ToolbarMenu>(
                    "ESWindowSystemActionsOverflow");
                Assert.AreEqual(
                    1,
                    systemActions.Query<Button>().ToList()
                        .Count(button => !ReferenceEquals(button, overflow)),
                    "窗口级高频操作只保留一个休眠/唤醒主按钮。");
                Assert.IsNotNull(
                    overflow,
                    "允许、自动/固定和全局策略必须统一进入系统菜单。");

                ESWindowFoundation.Unbind(window, true);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>(
                    "ESWindowSystemActions"));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReturningFromTemporaryOptOutRestoresDefaultSleepParticipation()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);

            try
            {
                ESWindowFoundation.Bind(window, allowSemiSleep: false);
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsFalse(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.IsFalse(ESWindowFoundation.TrySetWindowSleepAllowed(window, true));
                Assert.IsFalse(ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, true));

                ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    actionBar,
                    allowSemiSleep: true);

                Assert.IsTrue(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PublicSleepControlsAreSymmetricAndExplainUnavailableState()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);

            try
            {
                Assert.IsFalse(ESWindowFoundation.TrySetWindowSleepAllowed(window, false));
                Assert.AreEqual(
                    "窗口尚未接入 ES Presentation。",
                    ESWindowFoundation.GetWindowSleepBlockReason(window));

                ESWindowFoundation.BindWithStandardSystemHost(window, actionBar);
                Assert.IsTrue(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowAutoSleepEnabled(window));

                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(window, false));
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsFalse(ESWindowFoundation.CanWindowSleep(window));
                Assert.AreEqual(
                    "当前窗口已关闭半休眠。",
                    ESWindowFoundation.GetWindowSleepBlockReason(window));

                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(window, true));
                Assert.IsTrue(ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, false));
                Assert.IsFalse(ESWindowFoundation.IsWindowAutoSleepEnabled(window));
                Assert.IsTrue(ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, true));
                Assert.IsTrue(ESWindowFoundation.IsWindowAutoSleepEnabled(window));
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SystemActionsUseExpandedIntermediateAndMinimalLayouts()
        {
            Assert.IsFalse(ESEditorPresentation.ShouldCompactSystemActions(1174f));
            Assert.IsTrue(ESEditorPresentation.ShouldCompactSystemActions(800f));
            Assert.IsTrue(ESEditorPresentation.ShouldShowPrimarySystemAction(800f));
            Assert.IsFalse(ESEditorPresentation.ShouldShowPrimarySystemAction(480f));
        }

        [Test]
        public void AwakeGeometryRecoveryRequiresAFullPanelSizedFrame()
        {
            Rect sleep = new Rect(1800f, 900f, 100f, 100f);
            Rect awake = new Rect(320f, 180f, 920f, 620f);

            Assert.IsTrue(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(340f, 200f, 900f, 600f),
                sleep,
                awake));
            Assert.IsFalse(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(1800f, 900f, 180f, 140f),
                sleep,
                awake));
            Assert.IsFalse(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(340f, 200f, 520f, 300f),
                sleep,
                awake));
        }

        [Test]
        public void SemiSleepSlotSelectionReusesItsMainThreadScratch()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            object requested = Activator.CreateInstance(bindingType, true);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            FieldInfo scratchField = presentationType.GetField(
                "semiSleepUsedSlotScratch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(scratchField);
            object scratch = scratchField.GetValue(null);
            Assert.IsNotNull(scratch);

            int key = int.MinValue + 647;
            while (bindings.Contains(key))
                key++;
            bindings[key] = requested;

            try
            {
                int first = InvokePrivate<int>(
                    presentationType,
                    "AcquireSemiSleepSlot",
                    requested);
                int second = InvokePrivate<int>(
                    presentationType,
                    "AcquireSemiSleepSlot",
                    requested);

                Assert.GreaterOrEqual(first, 0);
                Assert.AreEqual(first, second);
                Assert.AreSame(scratch, scratchField.GetValue(null));
                AssertSlotIsUnusedByOtherSleepingWindows(
                    bindings,
                    bindingType,
                    requested,
                    first);
            }
            finally
            {
                bindings.Remove(key);
            }
        }

        [Test]
        public void FollowOwnerSyncNeverAddsBindingsDuringTheUpdatePass()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow owner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", child);
            SetField(
                bindingType,
                binding,
                "sleepLinkMode",
                ESWindowSleepLinkMode.FollowOwner);
            SetField(bindingType, binding, "sleepOwner", owner);

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int childId = child.GetInstanceID();
            int ownerId = owner.GetInstanceID();
            Assert.IsFalse(bindings.Contains(ownerId));
            bindings[childId] = binding;
            int countBefore = bindings.Count;

            try
            {
                InvokePrivate<object>(presentationType, "SyncSleepOwnerState", binding);

                Assert.AreEqual(countBefore, bindings.Count);
                Assert.IsFalse(bindings.Contains(ownerId));
            }
            finally
            {
                bindings.Remove(childId);
                bindings.Remove(ownerId);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RegularProductionWindowOpenersUseSingleInstancePaths()
        {
            Assert.IsTrue(typeof(IESWindowMultiInstanceContract).IsAssignableFrom(
                typeof(ESAdvancedDialogWindow)));
            Assert.IsTrue(typeof(IESWindowMultiInstanceContract).IsAssignableFrom(
                typeof(global::ES.ESWindowSleepBenchmarkProbeWindow)));
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs",
                "static new void OpenWindow");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESEditorFeedbackSound/ESEditorFeedbackSound.cs",
                "ESWindow_SupportsSemiSleep => false");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs",
                "private static void OpenFromMenu()",
                "OpenWindow();");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs",
                "CreateInstance<ESCmdAgentWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESIndependentInspectorWindow.cs",
                "CreateInstance<TWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs",
                "CreateInstance<ESInputActionImportWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputBindingDefineDrawer.cs",
                "CreateInstance<ESInputActionBindingImportWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "CreateInstance<ESProgressCenterWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/0_Stand/_Res/ResUse/ESAssetRefer.cs",
                "CreateInstance<ESAssetReferKeyPickerWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "IsSemiSleepWindowTypeExcluded");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "!ReferenceEquals(binding.root, window.rootVisualElement)",
                "AttachWindowOverlay(binding);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;",
                "private static void EnsurePlayModeLifecycleHook()");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void EnsurePlayModeLifecycleHook()",
                "if (globalEditorAdapterLifecycleInstalled)");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "bool playModeSuspended = playModeBindingsSuspended",
                "if (playModeSuspended)");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "if (playModeSuspended)",
                "bool resumed = ResumeWindowBindings();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings()",
                "if (binding.window.rootVisualElement.panel == null)",
                "QueueResumeOnPanelAttach(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "BindWindow",
                "catch (InvalidOperationException) when (lifecycleSuspended)",
                "actionHosts = null;");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "EditorApplication.delayCall -= ResumeWindowBindingsRetry;");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "resumeBindingsRetryCount >= 8");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings()",
                "if (binding.window.rootVisualElement == null)",
                "waitingForPanel = true",
                "QueueResumeOnPanelAttach(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings()",
                "bool overlayNeedsRebuild =",
                "if (overlayNeedsRebuild)",
                "LoadSemiSleepPreferences(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings()",
                "binding.actionHostsWereExplicit",
                "awaitingExplicitHosts = true",
                "return !needsPanelRetry && !waitingForPanel && !awaitingExplicitHosts;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "if (callerProvidedActionHosts)",
                "binding.actionHostsWereExplicit = true;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void QueueResumeWindowBindingsRetry()",
                "resumeBindingsRetryRequested = true;",
                "|| domainReloadInProgress",
                "EditorApplication.delayCall += ResumeWindowBindingsRetry;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void ResumeWindowBindingsRetry()",
                "if (domainReloadInProgress || EditorApplication.isCompiling)\n                return;",
                "resumeBindingsRetryRequested = false;",
                "ResumeWindowBindings();");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "EditorApplication.delayCall -= RecoverSemiSleepAfterFailedCompilation;");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.cs",
                "activePopup.Close();",
                "CreateInstance<ESCompactChoicePopup>");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId",
                "=> nameof(ESDialogService);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId",
                "=> nameof(ESWindowSemiSleepStressTest);");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ScriptableObject.CreateInstance(windowType)");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ESEditorPresentation.BindWindow(window, true)");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ESEditorPresentation.SetSemiSleepEnabled(true)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "if (HasOpenWindowInstance(windowType))",
                "EditorWindow window = EditorWindow.GetWindow(windowType);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "!ESWindowFoundation.IsBound(window)",
                "!ESWindowFoundation.IsWindowSleepSupported(window)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "private const int MaximumActiveDialogs = 8;",
                "if (activeWindows.Count >= MaximumActiveDialogs)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "if (activeWindows.Count >= MaximumActiveDialogs)",
                "CreateInstance<ESAdvancedDialogWindow>()");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "ESAdvancedDialogWindow duplicate = FindDuplicate(request.dialogId);",
                "switch (request.duplicatePolicy)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "switch (request.duplicatePolicy)",
                "return OpenNow(request, null, false);");
            AssertSourceContainsInOrder(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "activeWindow.Close();",
                "CreateInstance<ESWorkbenchPopupWindow>");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "if (!TryBeginDependencyCheck())",
                "using DependencyCheckWindowLease lease = AcquireCheckInstance();");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "using DependencyCheckWindowLease lease = AcquireCheckInstance();",
                "ESInstaller checkInstance = lease.Instance;");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "else if (ReferenceEquals(temporaryCheckInstance, window))",
                "window.hideFlags = HideFlags.None;");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "Resources.FindObjectsOfTypeAll<");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "public static ESInstaller installer;");
        }

        [Test]
        public void DirectTransientWindowsDeclareExplicitNoSleepContracts()
        {
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESTreeCollector/ESTree_Center/ESTreeMenuBuilder.cs",
                "BindWindow(this, allowSemiSleep: false)");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESTreeCollector/ESTree_Center/ESTreeMenuBuilder.cs",
                "UnbindWindow(this, true)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "BindWindow(this, allowSemiSleep: false)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "UnbindWindow(this, true)");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/AssetsTools/Simple_AssetTool_Page_UnityPackageTool.cs",
                "BindWindow(this, allowSemiSleep: false)");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/AssetsTools/Simple_AssetTool_Page_UnityPackageTool.cs",
                "UnbindWindow(this, true)");
        }

        [Test]
        public void ProgressCenterUsesExplicitTransientSleepContract()
        {
            string path =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            AssertSourceContains(path, "public sealed class ESProgressCenterWindow");
            AssertSourceContains(path, "BindWindow(this, allowSemiSleep: false)");
            AssertSourceContains(path, "跨任务全局进度聚合面不参与自动半休眠");
        }

        [Test]
        public void ESStandNativeKeyPickerDoesNotAcquirePresentationOwnership()
        {
            const string path = "Assets/Plugins/ES/0_Stand/_Res/ResUse/ESAssetRefer.cs";
            AssertSourceContains(path, "class ESAssetReferKeyPickerWindow : EditorWindow");
            AssertSourceContains(path, "ShowAuxWindow();");
            AssertSourceExcludes(path, "ESEditorPresentation.BindWindow");
            AssertSourceExcludes(path, "ESWindowFoundation.Bind");
        }

        [Test]
        public void ProductionBaseWindowsKeepFullSleepByDefault()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string[] sourceFiles = EnumerateESProductionEditorSources(projectRoot);
            var shortExceptionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeProjectPath(
                    Path.Combine(
                        projectRoot,
                        "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs")),
                NormalizeProjectPath(
                    Path.Combine(
                        projectRoot,
                        "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputBindingDefineDrawer.cs")),
            };
            int productionBaseWindowCount = 0;
            foreach (string path in sourceFiles)
            {
                string normalized = NormalizeProjectPath(path);
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase))
                    continue;
                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (!Regex.IsMatch(
                        source,
                        @"class\s+[^\r\n]*:\s*[^\r\n]*(?:ESSinglePage(?:IMGUI)?Window|ESMenuTreeWindow|ESOdinMenuTreeWindow)",
                        RegexOptions.CultureInvariant))
                    continue;

                productionBaseWindowCount++;
                if (Regex.IsMatch(
                        source,
                        @"ESWindow_SupportsSemiSleep\s*=>\s*false",
                        RegexOptions.CultureInvariant))
                {
                    Assert.IsTrue(
                        shortExceptionPaths.Contains(normalized),
                        "生产基类窗口不能静默关闭完整休眠：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepMode.Transient",
                        source,
                        "基类休眠例外必须声明 Transient 合同：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepContract",
                        source,
                        "基类休眠例外必须登记原因：" + normalized);
                }
            }

            Assert.GreaterOrEqual(
                productionBaseWindowCount,
                20,
                "生产基类窗口扫描结果异常，可能是扫描规则或工程路径失效。");
        }

        [Test]
        public void ProductionWindowInventoryDefaultsToFullWithExplicitTransientExceptions()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));

            Regex windowDeclaration = new Regex(
                @"(?m)\bclass\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<base>[^\r\n\{]+)",
                RegexOptions.CultureInvariant);
            var declarations = new List<(string Name, string BaseType, string Path, string Source)>();
            foreach (string path in EnumerateESProductionEditorSources(projectRoot))
            {
                string normalized = NormalizeProjectPath(path);
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Obsolete/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/-Templates/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = ReadSourceText(path);
                foreach (Match match in windowDeclaration.Matches(source))
                {
                    string baseType = match.Groups["base"].Value.Trim();
                    if (!baseType.Contains("EditorWindow", StringComparison.Ordinal)
                        && !baseType.Contains("OdinEditorWindow", StringComparison.Ordinal)
                        && !baseType.Contains("ESMenuTreeWindow", StringComparison.Ordinal)
                        && !baseType.Contains("ESOdinMenuTreeWindow", StringComparison.Ordinal)
                        && !baseType.Contains("ESSinglePage", StringComparison.Ordinal)
                        && !baseType.Contains("ESWorkbenchWindowBase", StringComparison.Ordinal)
                        && !baseType.Contains("ESIndependentInspectorWindow", StringComparison.Ordinal)
                        && !baseType.Contains("ESTrackTemporaryInspectorWindow", StringComparison.Ordinal))
                        continue;

                    declarations.Add((
                        match.Groups["name"].Value,
                        baseType,
                        normalized,
                        source));
                }
            }

            Assert.AreEqual(
                49,
                declarations.Count,
                "生产 ES 窗口库存发生漂移；新增窗口必须重新判定 Full/Transient 并更新覆盖表。");
            var expectedProductionNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "EntityBasicInteractionDebugWindow",
                "EntityStatDebugWindow",
                "ESAIBrainWindow",
                "ESAgentArtifactCandidateReviewWindow",
                "ESAssetPackageBakeWindow",
                "ESAssetPackageRecordPreviewWindow",
                "ESAssetReleaseUploadWindow",
                "ESAudioCueTrimPreviewWindow",
                "ESAutomationCenterWindow",
                "ESCameraTrackPreviewWindow",
                "ESCmdAgentWindow",
                "ESCommandPaletteWindow",
                "ESCompactChoicePopup",
                "ESCompositeShaderBakeWindow",
                "ESCompositeSSUMigrationWindow",
                "ESCreateSkillWindow",
                "ESDeveloperCockpitWindow",
                "ESDynamicAtlasMonitorWindow",
                "ESEditorFeedbackSoundSchemeWindow",
                "ESEditorHealthWindow",
                "ESEditorThemeWindow",
                "ESFontToolsWindow",
                "ESGameCoreDefinitionEditorWindow",
                "ESInputActionBindingImportWindow",
                "ESInputActionImportWindow",
                "ESInstaller",
                "ESLocalizationToolsWindow",
                "ESProgressCenterWindow",
                "ESResourceCollectionWorkflowWindow",
                "ESResourceRuntimeMonitorWindow",
                "ESResWindow",
                "ESSODataInfoWindow",
                "ESStableGraphViewWindow",
                "ESTrackClipTemporaryInspectorWindow",
                "ESTrackItemTemporaryInspectorWindow",
                "ESTrackSkillDataTemporaryInspectorWindow",
                "ESTrackViewWindow",
                "ESTreeMenuShower",
                "ESUIRiskAuditWindow",
                "ESWindowLauncher",
                "ESWorkbenchCaseStudyWindow",
                "ESWorkbenchIntegrationTestWindow",
                "ESWorkbenchPopupWindow",
                "ESWorldBuilderWorkbenchWindow",
                "ESWorldDialogueEditorWindow",
                "ESWorldMapSpaceEditorWindow",
                "EditorInputDialog",
                "SimpleToolsWindow",
                "ESAdvancedDialogWindow",
            };
            CollectionAssert.AreEquivalent(
                expectedProductionNames,
                declarations.Select(item => item.Name).ToArray(),
                "生产 ES 窗口类型集合发生漂移。");
            Assert.AreEqual(
                16,
                declarations.Count(item => Regex.IsMatch(
                    item.BaseType,
                    @"\b(?:EditorWindow|OdinEditorWindow)\b",
                    RegexOptions.CultureInvariant)),
                "直接 EditorWindow/OdinEditorWindow 窗口数量发生漂移。");
            Assert.AreEqual(
                33,
                declarations.Count(item => !Regex.IsMatch(
                    item.BaseType,
                    @"\b(?:EditorWindow|OdinEditorWindow)\b",
                    RegexOptions.CultureInvariant)),
                "基类派生窗口数量发生漂移。");

            var expectedTransient = new HashSet<string>(StringComparer.Ordinal)
            {
                "ESAdvancedDialogWindow",
                "ESProgressCenterWindow",
                "ESCommandPaletteWindow",
                "ESCompactChoicePopup",
                "ESCreateSkillWindow",
                "ESTreeMenuShower",
                "EditorInputDialog",
                "ESWorkbenchPopupWindow",
                "ESInputActionImportWindow",
                "ESInputActionBindingImportWindow",
            };

            foreach (string name in expectedTransient)
            {
                (string Name, string BaseType, string Path, string Source) declaration =
                    declarations.SingleOrDefault(item => item.Name == name);
                Assert.IsFalse(
                    string.IsNullOrEmpty(declaration.Name),
                    "Transient 窗口不在生产库存中：" + name);
                StringAssert.Contains(
                    "ESWindowSleepMode.Transient",
                    declaration.Source,
                    "Transient 窗口必须登记显式休眠合同：" + name);
                Assert.IsTrue(
                    declaration.Source.Contains("allowSemiSleep: false", StringComparison.Ordinal)
                    || declaration.Source.Contains(
                        "ESWindow_SupportsSemiSleep => false",
                        StringComparison.Ordinal),
                    "Transient 窗口必须显式关闭独立休眠：" + name);
            }

            Assert.AreEqual(
                expectedTransient.Count,
                declarations.Count(item => expectedTransient.Contains(item.Name)),
                "Transient 例外集合与生产窗口库存不一致。");
            Assert.AreEqual(
                39,
                declarations.Count - expectedTransient.Count,
                "未声明 Transient 的生产窗口必须保持默认 Full。");
        }

        [Test]
        public void DirectProductionEditorWindowsDeclareExplicitESLifecycleBinding()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string[] sourceFiles = EnumerateESProductionEditorSources(projectRoot);
            int directWindowFileCount = 0;
            foreach (string path in sourceFiles)
            {
                string normalized = NormalizeProjectPath(path);
                if (!normalized.Contains("/Plugins/ES/Editor/", StringComparison.OrdinalIgnoreCase)
                    && !normalized.Contains("/Scripts/ESLogic/Editor/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase))
                    continue;
                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (!Regex.IsMatch(
                        source,
                        @"class\s+[^\r\n]*:\s*[^\r\n]*\b(?:EditorWindow|OdinEditorWindow)\b",
                        RegexOptions.CultureInvariant))
                    continue;

                directWindowFileCount++;
                bool hasExplicitBinding = source.Contains(
                        "BindWindow(this",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.Bind(",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.BindWithStandardSystemHost(",
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    hasExplicitBinding,
                    "直接 EditorWindow/OdinEditorWindow 生产文件必须声明 ES 生命周期绑定："
                    + normalized);
            }

            Assert.GreaterOrEqual(
                directWindowFileCount,
                12,
                "直接 EditorWindow 扫描结果异常，可能漏掉了生产窗口。");
        }

        [Test]
        public void AdvancedDialogRequiresExplicitOwnerOrDeclaredMainWorkspaceFallback()
        {
            Type dialogWindowType = typeof(ESAdvancedDialogWindow);
            MethodInfo validate = dialogWindowType.GetMethod(
                "ValidateRequest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(validate);

            var ownerless = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.owner.required",
                title = "Owner contract",
            };
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => validate.Invoke(null, new object[] { ownerless }));
            Assert.IsInstanceOf<ArgumentException>(exception.InnerException);
            StringAssert.Contains("显式 owner", exception.InnerException.Message);

            var fallback = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.owner.fallback",
                title = "Fallback contract",
                allowMainWorkspaceFallback = true,
            };
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { fallback }));
        }

        [Test]
        public void AdvancedDialogRejectsDestroyedExplicitOwner()
        {
            Type dialogWindowType = typeof(ESAdvancedDialogWindow);
            MethodInfo validate = dialogWindowType.GetMethod(
                "ValidateRequest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(validate);

            EditorWindow owner = ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                UnityEngine.Object.DestroyImmediate(owner);
                var request = new ESAdvancedDialogRequest
                {
                    dialogId = "tests.dialog.owner.destroyed",
                    title = "Destroyed owner",
                    owner = owner,
                };
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => validate.Invoke(null, new object[] { request }));
                Assert.IsInstanceOf<ArgumentException>(exception.InnerException);
                StringAssert.Contains("已关闭", exception.InnerException.Message);
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AdvancedDialogServiceTreatsDestroyedOwnerAsInvalidUntilFallbackIsExplicit()
        {
            Type serviceType = typeof(ESAdvancedDialogWindow).Assembly
                .GetType("ES.ESDialogService");
            MethodInfo isOwnerInvalid = serviceType?.GetMethod(
                "IsOwnerInvalid",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(isOwnerInvalid);

            EditorWindow owner = ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                UnityEngine.Object.DestroyImmediate(owner);
                var request = new ESAdvancedDialogRequest
                {
                    dialogId = "tests.dialog.owner.monitor",
                    title = "Owner monitor",
                    owner = owner,
                };
                Assert.IsTrue((bool)isOwnerInvalid.Invoke(null, new object[] { request }));
                request.allowMainWorkspaceFallback = true;
                Assert.IsFalse((bool)isOwnerInvalid.Invoke(null, new object[] { request }));
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AdvancedDialogFirstScreenBrandingAndLifecycleGuardsRemainSourceLocked()
        {
            const string dialogPath =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            AssertSourceContains(dialogPath, "es-dialog-branded");
            AssertSourceContains(dialogPath, "ESDialogBrandMark");
            AssertSourceContains(dialogPath, "对话交互层");
            AssertSourceContains(dialogPath, "仅输入 / 确认");
            AssertSourceContainsInOrder(
                dialogPath,
                "if (state == PlayModeStateChange.ExitingEditMode",
                "Shutdown();");
            AssertSourceContainsInOrder(
                dialogPath,
                "beforeAssemblyReload -= Shutdown",
                "CompilationPipeline.compilationFinished -= OnCompilationFinished");
        }

        [Test]
        public void PresentationLifecycleCapturesEachBoundaryOnlyOnceAndResumesAfterFailure()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void CapturePlayModePreferences()",
                "CapturePlayModePreferences",
                "if (playModeBindingsSuspended)",
                "return;");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void CaptureAssemblyReloadPreferences()",
                "CaptureAssemblyReloadPreferences",
                "if (assemblyReloadPreferencesCaptured)",
                "return;");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "RecoverSemiSleepAfterFailedCompilation",
                "bool resumed = ResumeWindowBindings();",
                "LoadSemiSleepPreferences(binding);");
            AssertSourceContains(presentationPath, "SuspendWindowBinding(binding);");
            AssertSourceContains(presentationPath, "ResumeWindowBindings();");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void OnGlobalPlayModeStateChanged(PlayModeStateChange state)",
                "OnGlobalPlayModeStateChanged",
                "InstallGlobalEditorAdapters();",
                "EditorApplication.RepaintHierarchyWindow();");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static bool ResumeWindowBindings()",
                "bool completedPlayModeRestore = playModeBindingsSuspended;",
                "playModeBindingsSuspended = false;",
                "if (completedPlayModeRestore)",
                "assemblyReloadPreferencesCaptured = false;");
        }

        [Test]
        public void DetachedWindowRootsUseTheSameDeterministicSuspendTeardown()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void OnWindowRootDetached(DetachFromPanelEvent evt)",
                "OnWindowRootDetached",
                "SuspendWindowBinding(binding);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void SuspendWindowBinding(WindowBinding binding)",
                "SuspendWindowBinding",
                "UnregisterWindowCallbacks(binding);");
            AssertSourceContains(
                presentationPath,
                "windowBindingsByRoot.Remove(binding.root)");
        }

        [Test]
        public void PresentationStateApisDoNotImplicitlyBindUnknownWindows()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "测试前窗口必须保持未显式接入状态。");

                using (IDisposable lease = ESEditorPresentation.BeginWindowBusy(
                           window,
                           "不应隐式接入"))
                {
                    Assert.IsFalse(
                        ESWindowFoundation.IsBound(window),
                        "BeginWindowBusy 不得为未知窗口创建 Presentation binding。");
                }

                ESEditorPresentation.NotifyWindow(
                    window,
                    "不应隐式接入",
                    ESStatusKind.Info,
                    focus: false);
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "NotifyWindow 不得为未知窗口创建 Presentation binding。");

                Assert.IsFalse(
                    ESWindowFoundation.TrySetPresentationShortTitle(window, "未知"),
                    "未绑定窗口不能写入 Presentation 页签标签。");
                Assert.IsFalse(
                    ESEditorPresentation.SetWindowSemiSleepDockBounds(
                        window,
                        new Rect(20f, 20f, 100f, 100f)),
                    "未绑定窗口不能写入半休眠落点。");
                ESEditorPresentation.PulseWindow(window, ESStatusKind.Modified);
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "Presentation 状态 API 不得为未知窗口创建 binding。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DirectProductionWindowCreationIsRestrictedToGovernedExceptions()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs|ESAdvancedDialogWindow",
                "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.cs|ESCompactChoicePopup",
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs|ESInstaller",
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs|ESWindowSleepBenchmarkProbeWindow",
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs|ESWorkbenchPopupWindow"
            };
            var genericPattern = new Regex(
                @"(?:CreateInstance|CreateWindow)\s*<\s*((?:global::)?[A-Za-z_][A-Za-z0-9_.]*)\s*>",
                RegexOptions.CultureInvariant);
            var typeofPattern = new Regex(
                @"(?:CreateInstance|CreateWindow|Activator\.CreateInstance)\s*\(\s*typeof\s*\(\s*((?:global::)?[A-Za-z_][A-Za-z0-9_.]*)\s*\)",
                RegexOptions.CultureInvariant);
            var dynamicCreateAssignmentPattern = new Regex(
                @"(?m)^\s*(?<declared>(?:global::)?[A-Za-z_][A-Za-z0-9_.]*)\s+\w+\s*=\s*(?:ScriptableObject\.)?CreateInstance\s*\(\s*[A-Za-z_][A-Za-z0-9_.]*\s*\)",
                RegexOptions.CultureInvariant);
            var dynamicCreateCastPattern = new Regex(
                @"(?:ScriptableObject\.)?CreateInstance\s*\(\s*[A-Za-z_][A-Za-z0-9_.]*\s*\)\s+as\s+(?<cast>(?:global::)?[A-Za-z_][A-Za-z0-9_.]*)",
                RegexOptions.CultureInvariant);
            var dynamicCreateExplicitCastPattern = new Regex(
                @"\(\s*(?<cast>(?:global::)?[A-Za-z_][A-Za-z0-9_.]*)\s*\)\s*(?:ScriptableObject\.)?CreateInstance\s*\(\s*[A-Za-z_][A-Za-z0-9_.]*\s*\)",
                RegexOptions.CultureInvariant);
            var dynamicGetWindowPattern = new Regex(
                @"EditorWindow\.GetWindow\s*\(\s*[A-Za-z_][A-Za-z0-9_.]*\s*\)",
                RegexOptions.CultureInvariant);
            var allowedDynamicTypeOpeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs|EditorWindow.GetWindow(dynamicType)"
            };
            var editorWindowTypeNames = new HashSet<string>(
                TypeCache.GetTypesDerivedFrom<EditorWindow>().Select(type => type.Name),
                StringComparer.Ordinal);
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets", "Plugins", "ES"),
                Path.Combine(projectRoot, "Assets", "Scripts", "ESLogic")
            };

            foreach (string root in roots)
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(projectRoot.Length + 1)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    if (relative.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0
                        || relative.IndexOf("/Obsolete/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    string source = ReadSourceText(path);
                    foreach (Match match in genericPattern.Matches(source))
                    {
                        string typeName = GetSimpleTypeName(match.Groups[1].Value);
                        if (!editorWindowTypeNames.Contains(typeName))
                            continue;
                        string key = relative + "|" + typeName;
                        Assert.IsTrue(
                            allowed.Remove(key),
                            "发现未受治理的 EditorWindow 直接创建入口：" + key);
                    }

                    foreach (Match match in typeofPattern.Matches(source))
                    {
                        string typeName = GetSimpleTypeName(match.Groups[1].Value);
                        Assert.IsFalse(
                            editorWindowTypeNames.Contains(typeName),
                            "禁止通过 typeof/Activator 绕过 EditorWindow 单实例入口："
                            + relative + "|" + typeName);
                    }

                    foreach (Match match in dynamicCreateAssignmentPattern.Matches(source))
                    {
                        string declaredType = GetSimpleTypeName(match.Groups["declared"].Value);
                        Assert.IsFalse(
                            editorWindowTypeNames.Contains(declaredType),
                            "禁止把运行时 Type 创建结果直接赋给 EditorWindow 派生类型；请走受治理的单实例入口："
                            + relative + "|" + declaredType);
                    }

                    foreach (Match match in dynamicCreateCastPattern.Matches(source))
                    {
                        string castType = GetSimpleTypeName(match.Groups["cast"].Value);
                        Assert.IsFalse(
                            editorWindowTypeNames.Contains(castType),
                            "禁止通过运行时 Type 创建后再转换为 EditorWindow 派生类型："
                            + relative + "|" + castType);
                    }

                    foreach (Match match in dynamicCreateExplicitCastPattern.Matches(source))
                    {
                        string castType = GetSimpleTypeName(match.Groups["cast"].Value);
                        Assert.IsFalse(
                            editorWindowTypeNames.Contains(castType),
                            "禁止通过运行时 Type 创建后显式转换为 EditorWindow 派生类型："
                            + relative + "|" + castType);
                    }

                    foreach (Match match in dynamicGetWindowPattern.Matches(source))
                    {
                        string key = relative + "|EditorWindow.GetWindow(dynamicType)";
                        Assert.IsTrue(
                            allowedDynamicTypeOpeners.Remove(key),
                            "禁止在生产代码中通过运行时 Type 打开 EditorWindow；请走具体类型的单实例入口："
                            + key);
                    }
                }
            }

            Assert.AreEqual(
                0,
                allowed.Count + allowedDynamicTypeOpeners.Count,
                "受治理例外清单已经漂移："
                + string.Join(", ", allowed.Concat(allowedDynamicTypeOpeners)));
        }

        [Test]
        public void UnbindWindowBindingReleasesWindowAndVisualTreeReferences()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var root = new VisualElement();
            var host = new VisualElement();
            var overlay = new VisualElement();
            root.Add(host);
            root.Add(overlay);

            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", root);
            SetField(bindingType, binding, "host", host);
            SetField(bindingType, binding, "accentLine", new VisualElement());
            SetField(bindingType, binding, "sweep", new VisualElement());
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            overlay.userData = binding;

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            int id = window.GetInstanceID();
            bindings[id] = binding;
            bindingsByRoot[root] = binding;

            try
            {
                MethodInfo unbind = presentationType.GetMethod(
                    "UnbindWindowBinding",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbind);
                unbind.Invoke(null, new[] { (object)id, binding, true, false });

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
                Assert.IsNull(host.parent);
                Assert.IsNull(overlay.parent);
                Assert.IsNull(overlay.userData);
                AssertFieldIsNull(bindingType, binding, "window");
                AssertFieldIsNull(bindingType, binding, "root");
                AssertFieldIsNull(bindingType, binding, "host");
                AssertFieldIsNull(bindingType, binding, "semiSleepOverlay");
                AssertFieldIsNull(bindingType, binding, "animation");
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ClearPendingSleepOwnerRemovesDestroyedWindowReference()
        {
            const string ownerKey = "ES.Tests.WindowSleepLifetime";
            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(child, ownerKey));
                UnityEngine.Object.DestroyImmediate(child);

                ESWindowFoundation.ClearPendingSleepOwner(child);
                AssertPendingOwnerKeyAbsent(ownerKey);
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                if (child != null)
                    UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void UnbindWindowAcceptsDestroyedUnityObjectReference()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var root = new VisualElement();
            var overlay = new VisualElement();
            root.Add(overlay);

            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", root);
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            overlay.userData = binding;

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            int id = window.GetInstanceID();
            bindings[id] = binding;
            bindingsByRoot[root] = binding;

            try
            {
                UnityEngine.Object.DestroyImmediate(window);
                Assert.IsFalse(ReferenceEquals(window, null));
                Assert.IsTrue(window == null);

                ESEditorPresentation.UnbindWindow(window, true);

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
                Assert.IsNull(overlay.parent);
                Assert.IsNull(overlay.userData);
                AssertFieldIsNull(bindingType, binding, "window");
                AssertFieldIsNull(bindingType, binding, "root");
                AssertFieldIsNull(bindingType, binding, "semiSleepOverlay");
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnbindNullBindingClearsOrphanedRootEntries()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            var root = new VisualElement();
            const int id = int.MinValue + 173;
            bindings[id] = null;
            bindingsByRoot[root] = null;

            try
            {
                MethodInfo unbind = presentationType.GetMethod(
                    "UnbindWindowBinding",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbind);
                unbind.Invoke(null, new object[] { id, null, true, false });

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
            }
        }

        [Test]
        public void EdgeTabTitleUsesAvailableLengthForBothOrientations()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            object binding = Activator.CreateInstance(bindingType, true);
            var overlay = new VisualElement();
            var title = new Label("轨道编辑器完整标题");
            var icon = new Image();
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            SetField(bindingType, binding, "semiSleepTitleLabel", title);
            SetField(bindingType, binding, "semiSleepIcon", icon);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.EdgeTab);

            MethodInfo apply = presentationType.GetMethod(
                "ApplySemiSleepOverlayState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(apply);

            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Top);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(FlexDirection.Column, overlay.style.flexDirection.value);
            Assert.AreEqual(WhiteSpace.Normal, title.style.whiteSpace.value);
            Assert.AreEqual(TextOverflow.Clip, title.style.textOverflow.value);
            Assert.AreEqual(36f, title.style.maxWidth.value.value, 0.01f);
            Assert.AreEqual(30f, title.style.maxHeight.value.value, 0.01f);

            SetField(bindingType, binding, "visualState", ESWindowVisualState.EdgeTabHover);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(156f, title.style.maxHeight.value.value, 0.01f);

            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Left);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(FlexDirection.Row, overlay.style.flexDirection.value);
            Assert.AreEqual(WhiteSpace.NoWrap, title.style.whiteSpace.value);
            Assert.AreEqual(TextOverflow.Ellipsis, title.style.textOverflow.value);
            Assert.AreEqual(164f, title.style.maxWidth.value.value, 0.01f);
        }

        [Test]
        public void PersistedSleepRestoreRemainsPendingWhileBusy()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", new VisualElement());
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "restorePersistedSleepScheduled", true);
            SetField(bindingType, binding, "busyCount", 1);

            try
            {
                bool restored = InvokePrivate<bool>(
                    presentationType,
                    "TryRestorePersistedSemiSleepGeometry",
                    binding);

                Assert.IsFalse(restored);
                Assert.IsTrue(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.IsTrue(InvokePrivate<bool>(
                    presentationType,
                    "HasPersistedSleepRuntimeState",
                    binding));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PermanentlyDisallowedPersistedSleepNormalizesToActivePanel()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", false);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                bool restored = InvokePrivate<bool>(
                    presentationType,
                    "TryRestorePersistedSemiSleepGeometry",
                    binding);

                Assert.IsFalse(restored);
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.IsFalse(GetFieldValue<bool>(bindingType, binding, "semiSleepTarget"));
                Assert.AreEqual(
                    ESWindowVisualState.ActivePanel,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "visualState"));
                Assert.IsFalse(InvokePrivate<bool>(
                    presentationType,
                    "HasPersistedSleepRuntimeState",
                    binding));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PersistedSleepTileRestoreSynchronizesNativeGeometryAndRestartsPromotion()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            window.minSize = new Vector2(320f, 220f);
            window.maxSize = new Vector2(1600f, 1200f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "restorePersistedSleepScheduled", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(bindingType, binding, "semiSleepDockBounds", new Rect(250f, 190f, 100f, 100f));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            double startedAt = EditorApplication.timeSinceStartup;

            try
            {
                InvokePrivate<object>(
                    presentationType,
                    "RestorePersistedSemiSleepGeometry",
                    binding);

                Assert.AreEqual(
                    ESWindowVisualState.SleepTile,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "visualState"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "semiSleeping"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepScheduled"));
                Assert.AreEqual(100f, window.position.width, 0.01f);
                Assert.AreEqual(100f, window.position.height, 0.01f);
                Assert.GreaterOrEqual(
                    GetFieldValue<double>(bindingType, binding, "sleepTileIdleStartedAt"),
                    startedAt);
                Assert.Greater(
                    GetFieldValue<double>(bindingType, binding, "persistedSleepGeometryVerifyUntil"),
                    EditorApplication.timeSinceStartup);
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PersistedEdgeTabRestoreRepairsLaterNativeFrameWriteback()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect awake = new Rect(320f, 220f, 960f, 640f);
            window.position = awake;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "awakeBounds", awake);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.EdgeTab);
            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Right);
            SetField(bindingType, binding, "edgeOffset", 120f);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(
                    presentationType,
                    "RestorePersistedSemiSleepGeometry",
                    binding);
                Rect expected = window.position;
                Assert.AreEqual(56f, expected.width, 0.01f);
                Assert.AreEqual(44f, expected.height, 0.01f);

                window.position = awake;
                InvokePrivate<object>(
                    presentationType,
                    "RepairSettledSemiSleepGeometry",
                    binding);

                Assert.AreEqual(expected.x, window.position.x, 0.01f);
                Assert.AreEqual(expected.y, window.position.y, 0.01f);
                Assert.AreEqual(expected.width, window.position.width, 0.01f);
                Assert.AreEqual(expected.height, window.position.height, 0.01f);
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LegacySemiSleepPreferencesRemainCompatible()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(180f, 140f, 840f, 560f);
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(preferenceType, saved, "schemaVersion", 0);
            SetField(preferenceType, saved, "allowSemiSleep", true);
            SetField(preferenceType, saved, "pinned", true);
            SetField(preferenceType, saved, "sleeping", true);
            SetField(preferenceType, saved, "visualState", (int)ESWindowVisualState.EdgeTab);
            SetField(preferenceType, saved, "edge", (int)ESEditorPresentation.ESWindowEdge.Right);
            SetField(preferenceType, saved, "edgeOffset", 96f);
            SetField(preferenceType, saved, "awakeBounds", new Rect(200f, 160f, 900f, 620f));
            SetField(preferenceType, saved, "dockBounds", new Rect(260f, 220f, 100f, 100f));
            SetField(preferenceType, saved, "hasDockBounds", true);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsTrue(applied);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "allowSemiSleep"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "pinned"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "restorePersistedSleepOnBind"));
                Assert.AreEqual(
                    ESWindowVisualState.EdgeTab,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "transitionTargetState"));
                Assert.AreEqual(96f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "hasSemiSleepDockBounds"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowHealthSnapshotUsesExistingBindingsAndReportsContractDrift()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            ESWindowPresentationHealthSnapshot before =
                ESEditorPresentation.CaptureWindowHealthSnapshot();

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(280f, 200f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", window.rootVisualElement);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(
                bindingType,
                binding,
                "semiSleepDockBounds",
                new Rect(320f, 240f, 100f, 100f));
            int windowId = window.GetInstanceID();
            bindings[windowId] = binding;

            try
            {
                ESWindowPresentationHealthSnapshot after =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();

                Assert.AreEqual(before.BindingSlotCount + 1, after.BindingSlotCount);
                Assert.AreEqual(before.LiveWindowCount + 1, after.LiveWindowCount);
                Assert.AreEqual(before.SleepSupportedCount + 1, after.SleepSupportedCount);
                Assert.AreEqual(before.SleepingCount + 1, after.SleepingCount);
                Assert.AreEqual(
                    before.MissingSystemHostCount + 1,
                    after.MissingSystemHostCount);
                Assert.AreEqual(
                    before.GeometryMismatchCount + 1,
                    after.GeometryMismatchCount);
                Assert.AreEqual(
                    before.FirstIssueWindowType ?? window.GetType().FullName,
                    after.FirstIssueWindowType);
            }
            finally
            {
                bindings.Remove(windowId);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowHealthSnapshotReportsDuplicateConcreteTypesFromExistingBindings()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            ESWindowPresentationHealthSnapshot before =
                ESEditorPresentation.CaptureWindowHealthSnapshot();

            ESWindowDuplicateHealthProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            ESWindowDuplicateHealthProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            object firstBinding = Activator.CreateInstance(bindingType, true);
            object secondBinding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, firstBinding, "window", first);
            SetField(bindingType, firstBinding, "root", first.rootVisualElement);
            SetField(bindingType, secondBinding, "window", second);
            SetField(bindingType, secondBinding, "root", second.rootVisualElement);
            int firstId = first.GetInstanceID();
            int secondId = second.GetInstanceID();
            bindings[firstId] = firstBinding;
            bindings[secondId] = secondBinding;

            try
            {
                ESWindowPresentationHealthSnapshot after =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();

                Assert.AreEqual(before.LiveWindowCount + 2, after.LiveWindowCount);
                Assert.AreEqual(
                    before.DuplicateWindowInstanceCount + 1,
                    after.DuplicateWindowInstanceCount);
                Assert.IsTrue(after.HasIssues);
            }
            finally
            {
                bindings.Remove(firstId);
                bindings.Remove(secondId);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void FutureSemiSleepPreferenceSchemaIsRejectedWithoutMutatingBinding()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "pinned", false);
            SetField(bindingType, binding, "edgeOffset", 33f);
            SetField(preferenceType, saved, "schemaVersion", int.MaxValue);
            SetField(preferenceType, saved, "allowSemiSleep", false);
            SetField(preferenceType, saved, "pinned", true);
            SetField(preferenceType, saved, "edgeOffset", 900f);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsFalse(applied);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "allowSemiSleep"));
                Assert.IsFalse(GetFieldValue<bool>(bindingType, binding, "pinned"));
                Assert.AreEqual(33f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NonFiniteSemiSleepPreferenceGeometryFallsBackWithoutPoisoningState()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect currentBounds = new Rect(320f, 240f, 960f, 640f);
            window.position = currentBounds;
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(preferenceType, saved, "schemaVersion", 1);
            SetField(preferenceType, saved, "allowSemiSleep", true);
            SetField(preferenceType, saved, "sleeping", true);
            SetField(preferenceType, saved, "edgeOffset", float.NaN);
            SetField(
                preferenceType,
                saved,
                "awakeBounds",
                new Rect(float.NaN, 20f, 900f, 600f));
            SetField(
                preferenceType,
                saved,
                "dockBounds",
                new Rect(20f, float.PositiveInfinity, 100f, 100f));
            SetField(preferenceType, saved, "hasDockBounds", true);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsTrue(applied);
                Assert.AreEqual(0f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
                Assert.AreEqual(
                    currentBounds,
                    GetFieldValue<Rect>(bindingType, binding, "awakeBounds"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "hasSemiSleepDockBounds"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.AreEqual(
                    ESWindowVisualState.ActivePanel,
                    GetFieldValue<ESWindowVisualState>(
                        bindingType,
                        binding,
                        "transitionTargetState"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(bindingType, binding, "semiSleepDockBounds"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SavingSemiSleepPreferencesWritesCurrentSchemaAndSanitizesDockBounds()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "edgeOffset", float.NegativeInfinity);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(
                bindingType,
                binding,
                "semiSleepDockBounds",
                new Rect(float.NaN, 20f, 100f, 100f));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                string json = EditorPrefs.GetString(preferenceKey, string.Empty);
                Assert.IsNotEmpty(json);
                object saved = JsonUtility.FromJson(json, preferenceType);
                Assert.IsNotNull(saved);
                Assert.AreEqual(1, GetFieldValue<int>(preferenceType, saved, "schemaVersion"));
                Assert.AreEqual(0f, GetFieldValue<float>(preferenceType, saved, "edgeOffset"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "hasDockBounds"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(preferenceType, saved, "dockBounds"));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SavingSemiSleepPolicyDoesNotDependOnValidWindowGeometry()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = default;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", false);
            SetField(bindingType, binding, "pinned", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", default(Rect));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                string json = EditorPrefs.GetString(preferenceKey, string.Empty);
                Assert.IsNotEmpty(json);
                object saved = JsonUtility.FromJson(json, preferenceType);
                Assert.IsNotNull(saved);
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "allowSemiSleep"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, saved, "pinned"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "sleeping"));
                Assert.AreEqual(
                    (int)ESWindowVisualState.ActivePanel,
                    GetFieldValue<int>(preferenceType, saved, "visualState"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(preferenceType, saved, "awakeBounds"));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AssemblyReloadPreferenceCaptureIsOneShotAcrossRootDetach()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo capturedField = presentationType.GetField(
                "assemblyReloadPreferencesCaptured",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(capturedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4101;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousCaptured = (bool)capturedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                capturedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");
                SetField(bindingType, binding, "semiSleeping", false);
                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "Detach 后的临时 Awake 状态不能覆盖 Reload 前保存的休眠状态。");
            }
            finally
            {
                capturedField.SetValue(null, previousCaptured);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PlayModePreferenceCaptureIsOneShotAcrossDuplicateNotifications()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo suspendedField = presentationType.GetField(
                "playModeBindingsSuspended",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(suspendedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(260f, 200f, 880f, 580f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4102;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousSuspended = (bool)suspendedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                suspendedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CapturePlayModePreferences");
                SetField(bindingType, binding, "semiSleeping", false);
                InvokePrivate<object>(presentationType, "CapturePlayModePreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "EnteredPlayMode 的重复通知不能覆盖 ExitingEditMode 保存的休眠状态。");
            }
            finally
            {
                suspendedField.SetValue(null, previousSuspended);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AssemblyReloadDuringPlayModeCannotOverwritePlayModeSnapshot()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo suspendedField = presentationType.GetField(
                "playModeBindingsSuspended",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo capturedField = presentationType.GetField(
                "assemblyReloadPreferencesCaptured",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(suspendedField);
            Assert.IsNotNull(capturedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(280f, 190f, 860f, 560f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4103;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousSuspended = (bool)suspendedField.GetValue(null);
            bool previousCaptured = (bool)capturedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                SetField(bindingType, binding, "semiSleeping", false);
                suspendedField.SetValue(null, true);
                capturedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "PlayMode 中 Reload 不能把已捕获的休眠状态覆盖成 Awake。");
            }
            finally
            {
                suspendedField.SetValue(null, previousSuspended);
                capturedField.SetValue(null, previousCaptured);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FrameActivationCanReleaseStaticEntryByCachedWindowId()
        {
            Type activationType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowFrameActivation",
                true);
            Type runningType = activationType.GetNestedType(
                "RunningAnimation",
                BindingFlags.NonPublic);
            Assert.IsNotNull(runningType);

            object running = Activator.CreateInstance(runningType, true);
            var root = new VisualElement();
            const int windowId = int.MinValue + 149;
            SetField(runningType, running, "WindowId", windowId);
            SetField(runningType, running, "Root", root);

            IDictionary runningByWindow = GetStaticDictionary(activationType, "Running");
            IDictionary runningByRoot = GetStaticDictionary(activationType, "RunningByRoot");
            runningByWindow[windowId] = running;
            runningByRoot[root] = running;

            MethodInfo stop = activationType.GetMethod(
                "Stop",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(bool) },
                null);
            Assert.IsNotNull(stop);
            stop.Invoke(null, new object[] { windowId, false });

            Assert.IsFalse(runningByWindow.Contains(windowId));
            Assert.IsFalse(runningByRoot.Contains(root));
            AssertFieldIsNull(runningType, running, "Root");
            AssertFieldIsNull(runningType, running, "Window");
            AssertFieldIsNull(runningType, running, "Schedule");
        }

        private static IDictionary GetStaticDictionary(Type ownerType, string fieldName)
        {
            var dictionary = ownerType
                .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IDictionary;
            Assert.IsNotNull(dictionary, ownerType.FullName + "." + fieldName);
            return dictionary;
        }

        private static void AssertSourceExcludes(string projectRelativePath, string forbiddenText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            StringAssert.DoesNotContain(forbiddenText, source, projectRelativePath);
        }

        private static void AssertSourceContains(string projectRelativePath, string requiredText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            StringAssert.Contains(requiredText, source, projectRelativePath);
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string[] EnumerateESProductionEditorSources(string projectRoot)
        {
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets/Plugins/ES/Editor"),
                Path.Combine(projectRoot, "Assets/Scripts/ESLogic/Editor"),
            };
            return roots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(
                    root,
                    "*.cs",
                    SearchOption.AllDirectories))
                .ToArray();
        }

        private static string GetSimpleTypeName(string typeName)
        {
            string normalized = (typeName ?? string.Empty).Replace("global::", string.Empty);
            int separator = normalized.LastIndexOf('.');
            return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
        }

        private static void AssertSourceContainsInOrder(
            string projectRelativePath,
            string first,
            string second)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, projectRelativePath + " 缺少：" + first);
            Assert.Greater(secondIndex, firstIndex, projectRelativePath + " 创建顺序不符合单实例合同。");
        }

        private static void AssertSourceContainsInMethodInOrder(
            string projectRelativePath,
            string methodSignature,
            params string[] requiredText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            int methodIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, projectRelativePath + " missing method: " + methodSignature);

            int nextMethodIndex = source.IndexOf(
                "\n        private static ",
                methodIndex + methodSignature.Length,
                StringComparison.Ordinal);
            string methodSource = nextMethodIndex < 0
                ? source.Substring(methodIndex)
                : source.Substring(methodIndex, nextMethodIndex - methodIndex);
            int previousIndex = -1;
            foreach (string text in requiredText)
            {
                int index = methodSource.IndexOf(text, StringComparison.Ordinal);
                Assert.GreaterOrEqual(index, 0, methodSignature + " missing: " + text);
                Assert.Greater(index, previousIndex, methodSignature + " source order mismatch");
                previousIndex = index;
            }
        }

        private static string ReadSourceText(string path)
        {
            try
            {
                return File.ReadAllText(path, new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException)
            {
                // Some legacy sources use the local code page; API tokens remain
                // ASCII, so a fallback is sufficient for source contract checks.
                return File.ReadAllText(path, Encoding.Default);
            }
        }

        private static void SetField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            field.SetValue(target, value);
        }

        private static T GetFieldValue<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            return (T)field.GetValue(target);
        }

        private static T InvokePrivate<T>(Type ownerType, string methodName, params object[] arguments)
        {
            MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, ownerType.FullName + "." + methodName);
            object result = method.Invoke(null, arguments);
            return result == null ? default : (T)result;
        }

        private static void AssertFieldIsNull(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            Assert.IsNull(field.GetValue(target), ownerType.FullName + "." + fieldName);
        }

        private static void AssertPendingOwnerKeyAbsent(string ownerKey)
        {
            Type presentationType = typeof(ESEditorPresentation);
            var pending = presentationType
                .GetField("pendingSleepOwners", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IEnumerable;
            Assert.IsNotNull(pending);
            foreach (object item in pending)
            {
                if (item == null)
                    continue;
                FieldInfo keyField = item.GetType().GetField(
                    "ownerKey",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(keyField);
                Assert.AreNotEqual(ownerKey, keyField.GetValue(item));
            }
        }

        private static void AssertSlotIsUnusedByOtherSleepingWindows(
            IDictionary bindings,
            Type bindingType,
            object requested,
            int slot)
        {
            foreach (DictionaryEntry entry in bindings)
            {
                object binding = entry.Value;
                if (binding == null || ReferenceEquals(binding, requested))
                    continue;
                bool sleeping = GetFieldValue<bool>(bindingType, binding, "semiSleeping");
                bool targetingSleep = GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "semiSleepAnimating")
                    && GetFieldValue<bool>(bindingType, binding, "semiSleepTarget");
                if (!sleeping && !targetingSleep)
                    continue;
                Assert.AreNotEqual(
                    slot,
                    GetFieldValue<int>(bindingType, binding, "semiSleepSlot"));
            }
        }
    }
}
