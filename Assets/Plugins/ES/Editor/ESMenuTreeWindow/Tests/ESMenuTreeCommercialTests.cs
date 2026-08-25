using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ES.EditorInternal;

namespace ES.Tests
{
    public sealed class ESParticlePreviewUnsafeProbe : MonoBehaviour
    {
        public static int AwakeCount;

        private void Awake()
        {
            AwakeCount++;
        }
    }

    internal static class ESWindowSleepCommercialBaselineRunner
    {
        private const string FixtureName = "ES.Tests.ESMenuTreeCommercialTests";
        private static readonly string[] TargetSourcePaths =
        {
            "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
            "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
            "Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/ESMenuTreeCommercialTests.cs"
        };

        private static TestRunnerApi activeApi;
        private static BaselineCallbacks activeCallbacks;

        [MenuItem(
            "【ES】/验证与诊断/测试与验收/编辑器窗口/运行窗口休眠商业基线 %#F12",
            false,
            9175)]
        private static void Run()
        {
            if (activeApi != null)
            {
                Debug.LogWarning("[ESWindowSleepBaseline] 已有本基线正在运行。");
                return;
            }

            string head = ReadGitHead();
            string sourceFingerprint = BuildSourceFingerprint();
            activeApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            activeCallbacks = new BaselineCallbacks(head, sourceFingerprint, ReleaseActiveRun);
            activeApi.RegisterCallbacks(activeCallbacks);

            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "ES.MenuTree.Editor.Tests" },
                groupNames = new[] { "^ES\\.Tests\\.ESMenuTreeCommercialTests" }
            };

            try
            {
                string jobId = activeApi.Execute(new ExecutionSettings(filter));
                Debug.Log(
                    "[ESWindowSleepBaseline] 已提交窗口休眠商业基线"
                    + $" | job={jobId}"
                    + $" | head={head}"
                    + $" | unity={Application.unityVersion}"
                    + $" | source={sourceFingerprint}");
            }
            catch
            {
                ReleaseActiveRun();
                throw;
            }
        }

        [MenuItem(
            "【ES】/验证与诊断/测试与验收/编辑器窗口/运行窗口休眠商业基线 %#F12",
            true)]
        private static bool ValidateRun()
        {
            return activeApi == null
                && !EditorApplication.isCompiling
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void ReleaseActiveRun()
        {
            if (activeApi == null)
                return;
            if (activeCallbacks != null)
                activeApi.UnregisterCallbacks(activeCallbacks);
            UnityEngine.Object.DestroyImmediate(activeApi);
            activeApi = null;
            activeCallbacks = null;
        }

        private static string ReadGitHead()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (System.Diagnostics.Process process =
                       System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null || !process.WaitForExit(3000) || process.ExitCode != 0)
                        return "unavailable";
                    return process.StandardOutput.ReadToEnd().Trim();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESWindowSleepBaseline] 读取 Git HEAD 失败：" + exception.Message);
                return "unavailable";
            }
        }

        private static string BuildSourceFingerprint()
        {
            return string.Join(
                ",",
                TargetSourcePaths.Select(path =>
                    Path.GetFileName(path) + ":" + ComputeSha256(path)));
        }

        private static string ComputeSha256(string path)
        {
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant()
                        .Substring(0, 12);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ESWindowSleepBaseline] 计算源码指纹失败："
                    + path
                    + " | "
                    + exception.Message);
                return "unavailable";
            }
        }

        private sealed class BaselineCallbacks : ICallbacks
        {
            private readonly string headAtStart;
            private readonly string sourceFingerprint;
            private readonly Action completed;
            private readonly List<string> failedTests = new List<string>();
            private bool observingTargetRun;

            internal BaselineCallbacks(
                string headAtStart,
                string sourceFingerprint,
                Action completed)
            {
                this.headAtStart = headAtStart;
                this.sourceFingerprint = sourceFingerprint;
                this.completed = completed;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                observingTargetRun = IsTargetFixtureRun(testsToRun);
                if (!observingTargetRun)
                    return;
                failedTests.Clear();
                Debug.Log(
                    "[ESWindowSleepBaseline] START"
                    + $" | head={headAtStart}"
                    + $" | unity={Application.unityVersion}"
                    + $" | fixture={FixtureName}"
                    + $" | cases={testsToRun.TestCaseCount}"
                    + $" | source={sourceFingerprint}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!observingTargetRun)
                    return;
                observingTargetRun = false;
                string headAtFinish = ReadGitHead();
                bool headStable = string.Equals(
                    headAtStart,
                    headAtFinish,
                    StringComparison.Ordinal);
                string sourceAtFinish = BuildSourceFingerprint();
                bool sourceStable = string.Equals(
                    sourceFingerprint,
                    sourceAtFinish,
                    StringComparison.Ordinal);
                string summary =
                    "[ESWindowSleepBaseline] FINISH"
                    + $" | head={headAtStart}"
                    + $" | headAtFinish={headAtFinish}"
                    + $" | headStable={headStable}"
                    + $" | sourceStable={sourceStable}"
                    + $" | unity={Application.unityVersion}"
                    + $" | total={result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount}"
                    + $" | passed={result.PassCount}"
                    + $" | failed={result.FailCount}"
                    + $" | skipped={result.SkipCount}"
                    + $" | inconclusive={result.InconclusiveCount}"
                    + $" | duration={result.Duration:0.000}s"
                    + $" | source={sourceFingerprint}"
                    + $" | sourceAtFinish={sourceAtFinish}"
                    + $" | failedTests={(failedTests.Count == 0 ? "none" : string.Join(",", failedTests))}";

                if (result.FailCount == 0 && headStable && sourceStable)
                    Debug.Log(summary);
                else
                    Debug.LogError(summary);
                completed();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (observingTargetRun && !result.HasChildren && result.FailCount > 0)
                    failedTests.Add(result.FullName);
            }

            private static bool IsTargetFixtureRun(ITestAdaptor root)
            {
                bool foundTarget = false;
                bool foundOther = false;
                ClassifyLeaves(root, ref foundTarget, ref foundOther);
                return foundTarget && !foundOther;
            }

            private static void ClassifyLeaves(
                ITestAdaptor test,
                ref bool foundTarget,
                ref bool foundOther)
            {
                if (test == null)
                    return;
                if (!test.HasChildren)
                {
                    if (test.FullName != null
                        && test.FullName.StartsWith(FixtureName + ".", StringComparison.Ordinal))
                        foundTarget = true;
                    else
                        foundOther = true;
                    return;
                }

                foreach (ITestAdaptor child in test.Children)
                    ClassifyLeaves(child, ref foundTarget, ref foundOther);
            }
        }
    }

    public sealed class ESMenuTreeCommercialTests
    {
        [Test]
        public void DefaultWindowIconsUseStableSemanticMappings()
        {
            Assert.AreEqual(
                "d_Font Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "ES 字体工作台", "字体/构建"));
            Assert.AreEqual(
                "d_BuildSettings.Editor.Small",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "资源发布", "构建/Bake"));
            Assert.AreEqual(
                "d_Settings Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "编辑器主题设置", "设置"));
            Assert.AreEqual(
                "d_UnityEditor.ConsoleWindow",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "ES 工具窗口", ""));
            Assert.AreEqual(
                "d_Camera Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "相机作者工具", "世界/相机"));
            Assert.AreEqual(
                "d_Shader Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "Shader Graph 参数", "材质/着色器"));
            Assert.AreEqual(
                "d_ParticleSystem Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "粒子系统调整", "层级工具/粒子"));
            Assert.AreEqual(
                "d_Material Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "材质替换", "层级工具/材质"));
            Assert.IsNull(
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "相机作者工具", "世界/相机"),
                "相机应使用 Unity 原生相机图标，不能被场景品牌图标冒充。");

            Assert.AreEqual(
                "agent",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "Agent 控制台", "自动化与开发/Agent"));
            Assert.AreEqual(
                "diagnostics",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "性能诊断", "验证与诊断/性能"));
            Assert.AreEqual(
                "diagnostics",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(ESProgressCenterWindow), string.Empty, string.Empty),
                "Progress 类型名不得因为包含 res 子串而误用资源图标。");
            Assert.AreEqual(
                "content",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "角色内容制作", "技能/物品"));
            Assert.AreEqual(
                "workbench",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "ES 工具窗口", ""),
                "未知 ES 窗口使用中性工作台图标，不再伪装成资源文件夹。");
            Assert.AreEqual(
                "graph",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "Agent Graph 工作台", "节点/流程"),
                "图节点是当前用户目标时，不能被 Agent/Command 技术名词抢走图标语义。");
            Assert.AreEqual(
                "d_console.infoicon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(ESProgressCenterWindow), string.Empty, string.Empty));
            string presentationSource = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                    "ESPresentation", "Core", "ESEditorPresentationCore.cs"),
                Encoding.UTF8);
            StringAssert.DoesNotContain("DefaultAsset Icon.png", presentationSource,
                "未知窗口不能回退到资源文件夹图标；统一使用中性的 Console/info 语义。");
        }

        private sealed class EmptyPage : ESMenuTreePage
        {
            public override VisualElement CreateView(ESMenuTreePageContext context)
            {
                return new VisualElement();
            }
        }

        private sealed class SerializedTarget : ScriptableObject
        {
            public int capacity;
        }

        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private SendOrPostCallback callback;
            private object callbackState;

            public override void Post(SendOrPostCallback value, object state)
            {
                Assert.IsNull(callback, "测试同步上下文一次只允许一个待处理回调。");
                callback = value;
                callbackState = state;
            }

            public void RunPostedCallback()
            {
                SendOrPostCallback pending = callback;
                object state = callbackState;
                callback = null;
                callbackState = null;
                Assert.IsNotNull(pending, "预期 cancellation 已投递回 Editor 上下文。");
                pending.Invoke(state);
            }
        }

        private static string ExtractBalancedSourceBlock(
            string source,
            string declaration)
        {
            int declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.GreaterOrEqual(
                declarationIndex,
                0,
                "未找到源码声明：" + declaration);
            int openingBrace = source.IndexOf('{', declarationIndex);
            Assert.GreaterOrEqual(openingBrace, 0, "声明缺少方法体：" + declaration);

            int depth = 0;
            bool inString = false;
            bool inCharacter = false;
            bool escaped = false;
            for (int i = openingBrace; i < source.Length; i++)
            {
                char current = source[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if ((inString || inCharacter) && current == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (!inCharacter && current == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (!inString && current == '\'')
                {
                    inCharacter = !inCharacter;
                    continue;
                }
                if (inString || inCharacter)
                    continue;
                if (current == '{')
                    depth++;
                else if (current == '}' && --depth == 0)
                    return source.Substring(openingBrace, i - openingBrace + 1);
            }

            Assert.Fail("源码声明缺少闭合花括号：" + declaration);
            return string.Empty;
        }

        private static MethodInfo GetDialogServiceMethod(string name)
        {
            MethodInfo method = typeof(ESDialogService).GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ESDialogService 缺少内部治理入口：" + name);
            return method;
        }

        private static List<ESDialogService.DialogOperation> GetDialogOperationList(
            string fieldName)
        {
            FieldInfo field = typeof(ESDialogService).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "ESDialogService 缺少治理集合：" + fieldName);
            return (List<ESDialogService.DialogOperation>)field.GetValue(null);
        }

        private static List<ESAdvancedDialogWindow> GetDialogWindowList(string fieldName)
        {
            FieldInfo field = typeof(ESDialogService).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "ESDialogService 缺少窗口治理集合：" + fieldName);
            return (List<ESAdvancedDialogWindow>)field.GetValue(null);
        }

        public sealed class RuntimeContractWindow : ESMenuTreeWindow<RuntimeContractWindow>,
            IESWindowMultiInstanceContract
        {
            string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
                => "ES.Tests.RuntimeContract";
            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add(new ESMenuTreePageDefinition(
                        "declared.page",
                        "声明 / 页面",
                        new EmptyPage())
                    .WithNavigationLabel("声明"));
            }

            protected override void ESWindow_BuildActionHosts(ESWindowActionHosts hosts)
            {
                hosts.AddButton(ESWindowActionScope.System, "系统扩展", "测试系统域", () => { });
                hosts.AddButton(ESWindowActionScope.Global, "全局扩展", "测试全局域", () => { });
                hosts.AddButton(ESWindowActionScope.Window, "窗口扩展", "测试窗口域", () => { });
            }
        }

        public sealed class CompactRuntimeContractWindow
            : ESMenuTreeWindow<CompactRuntimeContractWindow>
        {
            protected override bool ESWindow_UseCompactHostChrome => true;

            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("compact.page", "紧凑 / 页面", new EmptyPage());
            }

            protected override void ESWindow_BuildActionHosts(ESWindowActionHosts hosts)
            {
                hosts.AddButton(ESWindowActionScope.System, "系统扩展", "测试系统域", () => { });
                hosts.AddButton(ESWindowActionScope.Global, "全局扩展", "测试全局域", () => { });
                hosts.AddButton(ESWindowActionScope.Window, "窗口扩展", "测试窗口域", () => { });
            }
        }

        [ESWindowSleepContract(
            ESWindowSleepMode.Transient,
            ESWindowSurfaceKind.Utility,
            "test compact sparse window")]
        public sealed class CompactSparseContractWindow
            : ESMenuTreeWindow<CompactSparseContractWindow>
        {
            protected override bool ESWindow_UseCompactHostChrome => true;
            protected override bool ESWindow_SupportsSemiSleep => false;

            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("compact-sparse.page", "紧凑空宿主 / 页面", new EmptyPage());
            }
        }

        [ESWindowSleepContract(
            ESWindowSleepMode.Transient,
            ESWindowSurfaceKind.Utility,
            "test no-sleep window")]
        public sealed class NoSemiSleepContractWindow
            : ESMenuTreeWindow<NoSemiSleepContractWindow>
        {
            protected override bool ESWindow_SupportsSemiSleep => false;

            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("no-sleep.page", "无休眠 / 页面", new EmptyPage());
            }
        }

        public sealed class DefaultSemiSleepContractWindow
            : ESMenuTreeWindow<DefaultSemiSleepContractWindow>,
                IESWindowMultiInstanceContract
        {
            string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
                => "ES.Tests.DefaultSemiSleep";
            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("default-sleep.page", "默认休眠 / 页面", new EmptyPage());
            }
        }

        public sealed class FollowOwnerContractWindow
            : ESMenuTreeWindow<FollowOwnerContractWindow>
        {
            protected override ESWindowSleepLinkMode ESWindow_SleepLinkMode
                => ESWindowSleepLinkMode.FollowOwner;

            protected override string ESWindow_SleepOwnerKey => "ES.Tests.FollowOwner";

            public void ReactivateOwner(UnityEditor.EditorWindow owner)
            {
                ESWindow_SetSleepOwnerOverride(owner);
            }

            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("follow.page", "跟随 / 页面", new EmptyPage());
            }
        }

        [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Workspace)]
        public sealed class RelationshipCallbackContractWindow : EditorWindow,
            IESWindowMultiInstanceContract,
            IESWindowSleepRelationshipState
        {
            public bool DetachedByOwnerClose;
            public bool ThrowOnDetach;
            public EditorWindow CloseOnDetach;

            string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
                => "ES.Tests.RelationshipCallback";

            bool IESWindowSleepRelationshipState.SleepOwnerDetachedByClose
                => DetachedByOwnerClose;

            void IESWindowSleepRelationshipState.DetachSleepOwnerAfterOwnerClose()
            {
                DetachedByOwnerClose = true;
                EditorWindow target = CloseOnDetach;
                CloseOnDetach = null;
                if (target != null)
                    ESWindowFoundation.Close(target);
                if (ThrowOnDetach)
                    throw new InvalidOperationException("ES relationship callback failure");
            }
        }

        [Test]
        public void SharedPanelUIProvidesReusableStatesAndSingleAxisScrolling()
        {
            ScrollView scroll = ESEditorPanelUI.CreateVerticalScrollView();
            Assert.AreEqual(ScrollViewMode.Vertical, scroll.mode);
            Assert.AreEqual(ScrollerVisibility.Hidden, scroll.horizontalScrollerVisibility);
            Assert.AreEqual(ScrollerVisibility.Auto, scroll.verticalScrollerVisibility);

            VisualElement empty = ESEditorPanelUI.CreateEmptyState(
                "暂无内容", "完成配置后重试。", "重试", () => { });
            VisualElement error = ESEditorPanelUI.CreateErrorState(
                "加载失败", "依赖不可用", "当前页无法显示", "恢复依赖后重试", "重试", () => { });
            Assert.AreEqual("ESEmptyState", empty.name);
            Assert.AreEqual("ESErrorState", error.name);
            Assert.IsNotNull(empty.Q<Button>());
            Assert.IsNotNull(error.Q<Button>());
        }

        [Test]
        public void PresentationProvidesSharedRoundedGeometryAndFunctionalSections()
        {
            float control = ESEditorPresentation.GetCornerRadius(
                ESEditorPresentation.ESCornerRadiusToken.Control);
            float card = ESEditorPresentation.GetCornerRadius(
                ESEditorPresentation.ESCornerRadiusToken.Card);
            float sectionRadius = ESEditorPresentation.GetCornerRadius(
                ESEditorPresentation.ESCornerRadiusToken.Section);
            float overlay = ESEditorPresentation.GetCornerRadius(
                ESEditorPresentation.ESCornerRadiusToken.Overlay);
            Assert.Greater(control, 0f);
            Assert.Greater(card, control);
            Assert.Greater(sectionRadius, card);
            Assert.Greater(overlay, sectionRadius);

            var surface = new VisualElement();
            ESEditorPresentation.ApplyRoundedSurface(
                surface,
                Color.black,
                ESEditorPresentation.ESCornerRadiusToken.Section,
                Color.gray);
            Assert.AreEqual(sectionRadius, surface.style.borderTopLeftRadius.value.value, 0.001f);
            Assert.AreEqual(sectionRadius, surface.style.borderTopRightRadius.value.value, 0.001f);
            Assert.AreEqual(sectionRadius, surface.style.borderBottomLeftRadius.value.value, 0.001f);
            Assert.AreEqual(sectionRadius, surface.style.borderBottomRightRadius.value.value, 0.001f);

            ESEditorPresentation.ApplyCornerRadius(
                surface,
                ESEditorPresentation.ESCornerRadiusToken.Control,
                ESEditorPresentation.ESCornerMask.Left);
            Assert.AreEqual(control, surface.style.borderTopLeftRadius.value.value, 0.001f);
            Assert.AreEqual(control, surface.style.borderBottomLeftRadius.value.value, 0.001f);
            Assert.AreEqual(0f, surface.style.borderTopRightRadius.value.value, 0.001f);
            Assert.AreEqual(0f, surface.style.borderBottomRightRadius.value.value, 0.001f);

            ESEditorFunctionalSection section = ESEditorPanelUI.CreateFunctionalSection(
                "导出计划", "先预检，再提交。", ESMenuTreePageStatus.Warning);
            section.Add(new Label("计划正文"));
            section.AddHeaderAction(ESEditorPanelUI.CreateButton("预检", "执行只读预检", () => { }));
            Assert.AreEqual("ESEditorFunctionalSection", section.Root.name);
            Assert.AreEqual("ESEditorFunctionalSectionHeader", section.Header.name);
            Assert.AreEqual("ESEditorFunctionalSectionContent", section.Content.name);
            Assert.AreEqual("警告", section.StatusLabel.text);
            Assert.AreEqual(1, section.Content.childCount);
            Assert.AreEqual(1, section.HeaderActions.childCount);
            Assert.AreEqual(Wrap.Wrap, section.Header.style.flexWrap.value);
            Assert.AreEqual(1f, section.HeaderActions.style.flexShrink.value, 0.001f);
            Assert.AreEqual(0f, section.HeaderActions.style.minWidth.value.value, 0.001f);

            TextField field = new TextField();
            VisualElement fieldRow = ESEditorPanelUI.CreateFieldRow(
                "很长的业务字段名称",
                field);
            Assert.AreEqual(Wrap.Wrap, fieldRow.style.flexWrap.value);
            Assert.AreEqual(0f, fieldRow.style.minWidth.value.value, 0.001f);
            Assert.AreEqual(0f, field.style.minWidth.value.value, 0.001f,
                "共享字段不得用固有最小宽度挤爆窄页。");
        }

        [Test]
        public void PresentationStyleSheetRoundsNativeControlsAcrossBoundEsWindows()
        {
            const string stylePath =
                "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESBrandTypography.uss";
            string source = File.ReadAllText(stylePath, Encoding.UTF8);
            StringAssert.Contains(
                ".es-presentation-controls .unity-base-field__input", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-base-field", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-slider", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-scroll-view", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-search-field", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-toolbar", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-toolbar-button", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-list-view__item", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-progress-bar__progress", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-two-pane-split-view", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-property-field", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-property-field__input", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-foldout__content", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-base-slider__tracker", source);

            string globalPath = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESPresentation",
                "Styles",
                "ESGlobalEditorDeepSkin.uss");
            string globalSource = File.ReadAllText(globalPath, Encoding.UTF8);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-toolbar-button", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-base-popup-field", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-tree-view__item", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-progress-bar__progress", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-two-pane-split-view", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-property-field", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-base-slider__tracker", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-popup-window", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin #ESWindowShell", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .unity-min-max-slider__tracker", globalSource);
            StringAssert.Contains(
                "border-top-left-radius: 999px", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .es-dialog-summary", globalSource);
            StringAssert.Contains(".es-global-editor-skin .es-window-opening-gate", globalSource);
            StringAssert.Contains(".es-global-editor-skin .es-functional-section", globalSource);
            StringAssert.Contains(".es-global-editor-skin .es-error-state", globalSource);
            StringAssert.Contains(".es-global-editor-skin .es-progress-task", globalSource);
            StringAssert.Contains(
                ".es-global-editor-skin .es-window-surface", globalSource);
            StringAssert.Contains(
                ".es-presentation-controls .es-dialog-field", source);
            StringAssert.Contains(
                ".es-presentation-controls .unity-inspector-element__header", source);
            StringAssert.Contains(".es-presentation-controls .es-functional-section", source);
            StringAssert.Contains(".es-presentation-controls .es-empty-state", source);
            StringAssert.Contains(".es-presentation-controls .es-progress-task", source);

            string presentationCoreSource = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                    "ESPresentation", "Core", "ESEditorPresentationCore.cs"),
                Encoding.UTF8);

            string menuTreeSource = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                    "ESMenuTreeWindow", "-Templates", "-ESMenuTreeWindow.cs"),
                Encoding.UTF8);
            int graphSemantic = menuTreeSource.IndexOf(
                "ContainsAny(key, \"graph\", \"node\", \"flow\"",
                StringComparison.Ordinal);
            int agentSemantic = menuTreeSource.IndexOf(
                "ContainsAny(key, \"agent\", \"协作\")",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(graphSemantic, 0);
            Assert.GreaterOrEqual(agentSemantic, 0);
            Assert.Less(graphSemantic, agentSemantic,
                "菜单页面图标必须先表达 Graph/节点业务语义，再处理 Agent 宿主名词。");
            StringAssert.DoesNotContain("DefaultAsset Icon.png", menuTreeSource,
                "未知图标不能回退到资源文件夹语义；应使用中性的 Unity Console 图标。");
            StringAssert.Contains("d_Camera Icon", menuTreeSource,
                "相机页面必须使用 Unity 原生相机语义图标。");
            StringAssert.Contains("ResolveUnitySemanticIcon", menuTreeSource,
                "页面必须先尝试具体资产语义图标，不能把粒子、材质和 Shader 页面统一降级为 Console 图标。");
            StringAssert.Contains("d_ParticleSystem Icon", menuTreeSource,
                "粒子页面必须使用 Unity 原生粒子语义图标。");
            StringAssert.Contains("d_Shader Icon", menuTreeSource,
                "Shader 页面必须使用 Unity 原生 Shader 语义图标。");
            StringAssert.Contains("d_AnimatorController Icon", presentationCoreSource,
                "Graph/节点窗口缺少品牌资源时也必须保留图语义的 Unity 原生回退。");
            int graphFallback = presentationCoreSource.IndexOf(
                "ContainsAny(key, \"graph\", \"node\", \"flow\"",
                StringComparison.Ordinal);
            int trackFallback = presentationCoreSource.IndexOf(
                "ContainsAny(key, \"track\", \"timeline\", \"animation\"",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(graphFallback, 0);
            Assert.GreaterOrEqual(trackFallback, 0);
            Assert.Less(graphFallback, trackFallback,
                "Graph/节点语义必须优先于 Track/Animation 技术名词。");
            StringAssert.DoesNotContain("Texture titleIcon = window?.titleContent?.image",
                presentationCoreSource,
                "未声明 Presentation metadata 的遗留 titleContent 图标不能覆盖业务语义。");
            StringAssert.Contains("string.Equals(iconName, \"workbench\"", menuTreeSource,
                "菜单树页面不能直接展示空白 workbench 品牌占位图。");
            StringAssert.Contains("string.Equals(iconName, \"inspector\"", menuTreeSource,
                "菜单树页面不能直接展示空白 inspector 品牌占位图。");
        }

        [Test]
        public void LegacySafeEditorDialogUsesTheEsDialogContract()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "1_Design",
                "Design_Tools",
                "ESDesignUtility",
                "SafeEditor.cs");
            string source = File.ReadAllText(path, Encoding.UTF8);
            StringAssert.Contains("ESDialog.ShowModal", source);
            StringAssert.Contains("ESDialogHost.Editor", source);
            StringAssert.Contains("BuildLegacyDialogId", source);
            StringAssert.Contains("ResolveLegacyDialogScope", source);
            StringAssert.DoesNotContain(
                "EditorUtility.DisplayDialog(title, message, ok, cancel)",
                source,
                "旧安全封装不能继续旁路 ESDialog。");
        }

        [Test]
        public void ImguiRoundedSurfaceCachesNineSliceTexturesPerSkinGeneration()
        {
            Texture2D first = ESEditorPresentation.SurfaceTexture;
            Texture2D second = ESEditorPresentation.SurfaceTexture;
            Assert.AreSame(first, second);
            Assert.GreaterOrEqual(first.width, 16);
            Assert.GreaterOrEqual(first.height, 16);
            Assert.AreEqual(HideFlags.HideAndDontSave, first.hideFlags);
            Assert.Greater(ESEditorPresentation.SurfaceStyle.border.left, 1);
            Assert.IsNotNull(ESEditorPresentation.ToolbarButtonStyle.normal.background);
            Assert.IsNotNull(ESEditorPresentation.ToolbarButtonStyle.hover.background);
            Assert.IsNotNull(ESEditorPresentation.ToolbarButtonStyle.active.background);
            Assert.IsNotNull(ESEditorPresentation.PrimaryButtonStyle.normal.background);
        }

        [Test]
        public void EditorCSharpCornerAssignmentsStayCentralizedInPresentationCore()
        {
            string editorRoot = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor");
            string presentationCore = Path.GetFullPath(Path.Combine(
                editorRoot, "ESPresentation", "Core", "ESEditorPresentationCore.cs"));
            string[] files = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();
            var utf8 = new UTF8Encoding(false, true);
            string[] forbiddenAssignments =
            {
                "borderTopLeftRadius " + "=",
                "borderTopRightRadius " + "=",
                "borderBottomLeftRadius " + "=",
                "borderBottomRightRadius " + "="
            };
            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFullPath(files[i]);
                if (string.Equals(file, presentationCore, StringComparison.OrdinalIgnoreCase))
                    continue;
                string source = File.ReadAllText(file, utf8);
                if (forbiddenAssignments.Any(token => source.Contains(token)))
                    violations.Add(file.Substring(editorRoot.Length).TrimStart(Path.DirectorySeparatorChar));
            }

            Assert.IsEmpty(
                violations,
                "ES Editor C# 圆角必须通过 ESEditorPresentation Token 设置：\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void SearchDropdownCachesVisibleSearchMetadataAndForwardsNativeTooltip()
        {
            ESSearchDropdown.Entry entry = ESSearchDropdown.Entry.Item(
                "FireBall",
                () => { },
                groupPath: "技能/火系",
                subtitle: "SkillDefinitionDataInfo",
                tooltip: "Assets/GameCore/Skills/FireBall.asset",
                keywords: "火球 projectile",
                badge: "子资产");
            Type itemType = typeof(ESSearchDropdown).GetNestedType(
                "ActionItem",
                BindingFlags.NonPublic);
            Assert.IsNotNull(itemType);
            ConstructorInfo constructor = itemType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ESSearchDropdown.Entry) },
                null);
            Assert.IsNotNull(constructor);
            object item = constructor.Invoke(new object[] { entry });
            PropertyInfo tooltip = null;
            for (Type type = itemType; type != null && tooltip == null; type = type.BaseType)
            {
                tooltip = type.GetProperty(
                    "tooltip",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            Assert.IsNotNull(tooltip);
            object tooltipSetter = typeof(ESSearchDropdown).GetField(
                "NativeTooltipSetter",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            object tooltipProperty = typeof(ESSearchDropdown).GetField(
                "NativeTooltipProperty",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Assert.IsTrue(tooltipSetter != null || tooltipProperty != null,
                "Unity 原生 tooltip 入口应只在类型初始化时解析一次。");

            string visibleName = itemType.GetProperty("name")?.GetValue(item) as string;
            StringAssert.Contains("FireBall", visibleName);
            StringAssert.Contains("SkillDefinitionDataInfo", visibleName);
            StringAssert.Contains("火球 projectile", visibleName);
            StringAssert.Contains("子资产", visibleName);
            StringAssert.DoesNotContain("Assets/GameCore/Skills/FireBall.asset", visibleName,
                "完整路径不得挤进可见标题。");
            Assert.AreEqual(
                "Assets/GameCore/Skills/FireBall.asset",
                tooltip.GetValue(item));
        }

        [TestCase("FireBall", "技能/火系", false)]
        [TestCase("中文 🔥", "路径/子", false)]
        [TestCase("", "", true)]
        public void SearchDropdownStableEntryIdsMatchLegacyFNVSequence(
            string label,
            string groupPath,
            bool separator)
        {
            ESSearchDropdown.Entry entry = separator
                ? ESSearchDropdown.Entry.Separator(groupPath)
                : ESSearchDropdown.Entry.Item(label, () => { }, groupPath);

            Assert.AreEqual(
                ComputeLegacySearchEntryId(label, groupPath, separator),
                entry.Id,
                "稳定 Entry Id 必须保持旧字符串输入序列，不得因无分配优化发生漂移。");
        }

        [Test]
        public void SearchDropdownBuilderAddRangePreservesOrderCallbacksAndKnownCapacity()
        {
            var values = new List<int> { 3, 1, 2 };
            var selected = new List<int>();
            ESSearchDropdown.Builder builder = ESSearchDropdown.Create("测试")
                .AddRange(values, value => "Entry-" + value, value => selected.Add(value));

            Assert.AreEqual(values.Count, builder.Entries.Count);
            for (int i = 0; i < values.Count; i++)
            {
                Assert.AreEqual("Entry-" + values[i], builder.Entries[i].Label);
                builder.Entries[i].OnSelected?.Invoke();
            }

            CollectionAssert.AreEqual(values, selected,
                "AddRange 必须保留输入顺序和回调捕获语义。");

            FieldInfo entriesField = typeof(ESSearchDropdown.Builder).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(entriesField);
            var storedEntries = entriesField.GetValue(builder) as List<ESSearchDropdown.Entry>;
            Assert.IsNotNull(storedEntries);
            Assert.GreaterOrEqual(
                storedEntries.Capacity,
                values.Count,
                "已知 ICollection<T> 输入应至少预留当前候选集容量。");
        }

        private static int ComputeLegacySearchEntryId(string label, string groupPath, bool separator)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = (groupPath ?? string.Empty) + "\n"
                    + (label ?? string.Empty) + "\n" + separator;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                int id = (int)(hash & 0x7fffffff);
                return id == 0 ? 1 : id;
            }
        }

        [Test]
        public void SearchDropdownLifetimeBridgeUsesDetachEventWithoutDynamicIlOrUpdatePolling()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("DetachFromPanelEvent", source);
            StringAssert.Contains("HoldInteraction(hostWindow", source);
            StringAssert.DoesNotContain("EditorApplication.update +=", source);
            StringAssert.DoesNotContain("System.Reflection.Emit", source);
            StringAssert.DoesNotContain("AssemblyBuilder", source);
            int drawStart = source.IndexOf("private void Draw()", StringComparison.Ordinal);
            int drawEnd = source.IndexOf(
                "private static float EstimateButtonWidth",
                drawStart,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(drawStart, 0);
            Assert.Greater(drawEnd, drawStart);
            string drawSource = source.Substring(drawStart, drawEnd - drawStart);
            StringAssert.DoesNotContain("new GUIContent", drawSource);
            StringAssert.DoesNotContain("GUILayout", drawSource);
            StringAssert.DoesNotContain("PropertyInfo", drawSource);
        }

        [Test]
        public void WindowLauncherUsesReusableSinglePageIMGUIFoundation()
        {
            Assert.IsTrue(typeof(ESSinglePageIMGUIWindow<ESWindowLauncher>)
                .IsAssignableFrom(typeof(ESWindowLauncher)));
            Assert.AreSame(ESWindowCommandRegistry.All, ESWindowCommandRegistry.All);
        }

        [Test]
        public void AutomationAndResourceWorkflowUseCurrentSinglePageFoundation()
        {
            Assert.IsTrue(typeof(ESSinglePageIMGUIWindow<ESAutomationCenterWindow>)
                .IsAssignableFrom(typeof(ESAutomationCenterWindow)));
            Assert.IsTrue(typeof(ESSinglePageIMGUIWindow<ESResourceCollectionWorkflowWindow>)
                .IsAssignableFrom(typeof(ESResourceCollectionWorkflowWindow)));
        }

        [Test]
        public void OdinMigrationHostExposesStablePageInventoryAndSelectionBridge()
        {
            Type migrationPage = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetNestedType("MigrationPage", BindingFlags.Public);
            MethodInfo snapshot = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetMethod("GetMigrationPageSnapshot", BindingFlags.Public | BindingFlags.Static);
            MethodInfo select = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetMethod("TrySelectMigrationPage", BindingFlags.Public | BindingFlags.Static);
            MethodInfo selectedId = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetMethod("GetSelectedMigrationPageId", BindingFlags.Public | BindingFlags.Static);
            MethodInfo openAtPage = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetMethod("OpenWindow", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string) }, null);

            Assert.IsNotNull(migrationPage);
            Assert.IsNotNull(snapshot);
            Assert.IsNotNull(select);
            Assert.IsNotNull(selectedId);
            Assert.IsNotNull(openAtPage);
        }

        [Test]
        public void OdinMigrationStableIdsAreDeterministicAndPathScoped()
        {
            MethodInfo createStableId = typeof(ESOdinMenuTreeWindow<ESResWindow>)
                .GetMethod("CreateMigrationStableId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(createStableId);

            string first = (string)createStableId.Invoke(null, new object[]
            {
                "resource.window", "资源库/角色"
            });
            string repeated = (string)createStableId.Invoke(null, new object[]
            {
                "resource.window", "资源库/角色"
            });
            string otherPath = (string)createStableId.Invoke(null, new object[]
            {
                "resource.window", "资源库/场景"
            });

            Assert.AreEqual(first, repeated);
            Assert.AreNotEqual(first, otherPath);
            StringAssert.StartsWith("resource.window.legacy.", first);
        }

        [Test]
        public void WindowLauncherSeparatesCoreAndPeripheralCommandsWithStableUniqueIds()
        {
            ESWindowCommand[] all = ESWindowCommandRegistry.All.ToArray();
            ESWindowCommand[] core = ESWindowCommandRegistry
                .GetCommands(ESWindowCommandScope.Core)
                .ToArray();
            ESWindowCommand[] peripheral = ESWindowCommandRegistry
                .GetCommands(ESWindowCommandScope.Peripheral)
                .ToArray();

            Assert.IsNotEmpty(core);
            Assert.IsNotEmpty(peripheral);
            Assert.AreEqual(all.Length, core.Length + peripheral.Length);
            Assert.AreEqual(
                all.Length,
                all.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count(),
                "窗口 StableId 必须唯一。");
        }

        [Test]
        public void PeripheralWindowCatalogExcludesTransientAndDangerousActions()
        {
            ESWindowCommand[] peripheral = ESWindowCommandRegistry
                .GetCommands(ESWindowCommandScope.Peripheral)
                .ToArray();
            string[] forbiddenTokens =
            {
                "dialog", "popup", "picker", "测试", "演示", "生成", "清理", "删除", "上传", "发布"
            };

            foreach (ESWindowCommand command in peripheral)
            {
                string menuLeaf = (command.MenuPath ?? string.Empty)
                    .Split('/')
                    .LastOrDefault() ?? string.Empty;
                string searchable = string.Join(" ", command.Id, command.DisplayName, menuLeaf)
                    .ToLowerInvariant();
                foreach (string token in forbiddenTokens)
                    StringAssert.DoesNotContain(token, searchable,
                        $"零散窗口目录包含瞬态或危险动作：{command.Id}");
            }
        }

        [Test]
        public void SinglePageIMGUIFoundationExposesGuardedHostLifecycleAndPageContext()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type windowType = typeof(ESSinglePageIMGUIWindow<ESWindowLauncher>);
            Type hostType = typeof(ESMenuTreeWindow<ESWindowLauncher>);

            PropertyInfo context = windowType.GetProperty("ESWindow_CurrentPageContext", flags);
            MethodInfo enable = hostType.GetMethod("ESWindow_OnHostEnable", flags);
            MethodInfo disable = hostType.GetMethod("ESWindow_OnHostDisable", flags);
            MethodInfo openingActivation = hostType.GetMethod(
                "ScheduleOpeningActivation",
                flags);

            Assert.IsNotNull(context);
            Assert.AreEqual(typeof(ESMenuTreePageContext), context.PropertyType);
            Assert.IsNotNull(enable);
            Assert.IsTrue(enable.IsVirtual);
            Assert.IsNotNull(disable);
            Assert.IsTrue(disable.IsVirtual);
            Assert.IsNotNull(openingActivation);
        }

        [Test]
        public void CommonESWorkbenchWindowsUseSinglePageFoundation()
        {
            Type[] commonWindows =
            {
                typeof(global::ES.EditorInternal.ESEditorHealthWindow),
                typeof(global::ES.EditorInternal.ESEditorThemeWindow),
                typeof(ESDeveloperCockpitWindow),
                typeof(ESAssetReleaseUploadWindow),
                typeof(ESResourceRuntimeMonitorWindow),
                typeof(ESEditorFeedbackSoundSchemeWindow),
                typeof(ESAssetPackageRecordPreviewWindow),
                typeof(ESWindowLauncher)
            };

            for (int i = 0; i < commonWindows.Length; i++)
            {
                Assert.IsTrue(
                    InheritsGenericFoundation(
                        commonWindows[i],
                        typeof(ESSinglePageIMGUIWindow<>)),
                    commonWindows[i].FullName + " 尚未接入统一单页窗口底座。");
            }
        }

        [Test]
        public void SimpleToolsUsesMenuTreeFoundationAndDeclaresCompleteStablePages()
        {
            Assert.IsTrue(
                InheritsGenericFoundation(typeof(SimpleToolsWindow), typeof(ESMenuTreeWindow<>)),
                "SimpleToolsWindow 尚未接入新版 ESMenuTreeWindow 底座。");

            SimpleToolsWindow window = ScriptableObject.CreateInstance<SimpleToolsWindow>();
            var builder = new ESMenuTreeBuilder();
            try
            {
                MethodInfo build = typeof(SimpleToolsWindow).GetMethod(
                    "ESWindow_BuildMenuTree",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(build);
                build.Invoke(window, new object[] { builder });

                string[] expectedIds =
                {
                    SimpleToolsWindow.PageId_Overview,
                    SimpleToolsWindow.PageId_RuntimeWatch,
                    SimpleToolsWindow.PageId_MaterialReplacement,
                    SimpleToolsWindow.PageId_PrefabManagement,
                    SimpleToolsWindow.PageId_PhysicsAlign,
                    SimpleToolsWindow.PageId_AnimationBatchSetting,
                    SimpleToolsWindow.PageId_BatchStaticSetting,
                    SimpleToolsWindow.PageId_BatchRename,
                    SimpleToolsWindow.PageId_LightingSettings,
                    SimpleToolsWindow.PageId_ParticleSystemAdjustment,
                    SimpleToolsWindow.PageId_TextureSpriteTool,
                    SimpleToolsWindow.PageId_UnityPackageTool,
                    SimpleToolsWindow.PageId_ObjectPool,
                    SimpleToolsWindow.PageId_TopToolbar,
                    SimpleToolsWindow.PageId_AssetReferenceChecker,
                    SimpleToolsWindow.PageId_SceneOptimization,
                    SimpleToolsWindow.PageId_SceneTextRepair
                };

                Assert.AreEqual(expectedIds.Length, builder.PageCount);
                CollectionAssert.AreEquivalent(expectedIds, builder.PagesById.Keys);
                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                {
                    Assert.IsInstanceOf<ESOdinPropertyTreePage>(node.Page, node.StableId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(node.Definition.Keywords), node.StableId);
                    Assert.GreaterOrEqual(node.Definition.ContentPadding, 0f, node.StableId);
                }

                ESMenuTreeBuilder.Node runtimeWatch =
                    builder.PagesById[SimpleToolsWindow.PageId_RuntimeWatch];
                Assert.AreEqual(1, runtimeWatch.Definition.PageActions.Count);
                Assert.AreEqual(
                    ESMenuTreeToolbarScope.Page,
                    runtimeWatch.Definition.PageActions[0].Scope);

                string[] actionPageIds =
                {
                    SimpleToolsWindow.PageId_RuntimeWatch,
                    SimpleToolsWindow.PageId_MaterialReplacement,
                    SimpleToolsWindow.PageId_PrefabManagement,
                    SimpleToolsWindow.PageId_BatchStaticSetting,
                    SimpleToolsWindow.PageId_BatchRename,
                    SimpleToolsWindow.PageId_ParticleSystemAdjustment,
                    SimpleToolsWindow.PageId_UnityPackageTool,
                    SimpleToolsWindow.PageId_AssetReferenceChecker,
                    SimpleToolsWindow.PageId_SceneOptimization,
                    SimpleToolsWindow.PageId_SceneTextRepair
                };
                for (int i = 0; i < actionPageIds.Length; i++)
                {
                    Assert.Greater(
                        builder.PagesById[actionPageIds[i]].Definition.PageActions.Count,
                        0,
                        actionPageIds[i] + " 缺少页面专属右上动作。");
                }

                ESMenuTreePageAction[] particleActions = builder.PagesById[
                        SimpleToolsWindow.PageId_ParticleSystemAdjustment]
                    .Definition.PageActions
                    .ToArray();
                CollectionAssert.AreEqual(
                    new[] { "particles.play", "particles.stop" },
                    particleActions.Select(action => action.ActionId).ToArray(),
                    "粒子页必须保留稳定动作 ID，并只控制窗口独立预览，不能重新接回原场景粒子播放。");
                FieldInfo executeField = typeof(ESMenuTreePageAction).GetField(
                    "Execute",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(executeField);
                CollectionAssert.AreEqual(
                    new[] { "StartParticleWindowPreview", "StopParticleWindowPreview" },
                    particleActions
                        .Select(action => ((Delegate)executeField.GetValue(action)).Method.Name)
                        .ToArray(),
                    "粒子页顶部动作路由错误，不能调用原场景粒子播放方法。");
            }
            finally
            {
                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                    node.Page?.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ParticleWindowPreviewRendersVisiblePixelsWithoutPlayingSceneSource()
        {
            UnityEngine.Object[] previousSelection = Selection.objects;
            GameObject source = null;
            Material material = null;
            Texture2D frame = null;
            var page = new Page_ParticleSystemAdjustment();
            try
            {
                source = new GameObject("ES Particle Preview Test", typeof(ParticleSystem));
                source.hideFlags = HideFlags.HideAndDontSave;
                ParticleSystem sourceParticleSystem = source.GetComponent<ParticleSystem>();
                sourceParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Unlit/Color");
                Assert.IsNotNull(shader, "当前项目没有可用于粒子预览验收的 Unlit Shader。");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                source.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                Selection.activeGameObject = source;
                page.duration = 2f;
                page.looping = true;
                page.startLifetime = 2f;
                page.startSpeed = 0f;
                page.startSize = 2f;
                page.startColor = Color.magenta;
                page.emissionRate = 60f;
                page.simulationSpace = ParticleSystemSimulationSpace.Local;

                Assert.IsTrue(page.StartIndependentPreview(), "独立粒子预览未能启动。");
                Assert.IsFalse(sourceParticleSystem.isPlaying, "窗口预览错误地播放了原场景粒子。");

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo previewTimeField = typeof(Page_ParticleSystemAdjustment).GetField(
                    "previewTime", flags);
                MethodInfo simulate = typeof(Page_ParticleSystemAdjustment).GetMethod(
                    "SimulatePreviewAtCurrentTime", flags);
                FieldInfo sessionField = typeof(Page_ParticleSystemAdjustment).GetField(
                    "particlePreviewSession", flags);
                FieldInfo viewField = typeof(Page_ParticleSystemAdjustment).GetField(
                    "previewView", flags);
                Assert.IsNotNull(previewTimeField);
                Assert.IsNotNull(simulate);
                Assert.IsNotNull(sessionField);
                Assert.IsNotNull(viewField);

                previewTimeField.SetValue(page, 0.75f);
                simulate.Invoke(page, null);
                var session = sessionField.GetValue(page) as ESEditorParticlePreviewSession;
                Assert.IsNotNull(session);
                ESEditorPreviewRenderContext context = session.RenderContext;
                Assert.IsNotNull(context);
                Assert.IsTrue(context.IsReady);
                var view = viewField.GetValue(page) as ESEditorPreviewOrbitView;
                Assert.IsNotNull(view);
                frame = context.Snapshot(
                    256,
                    256,
                    view.CreateCameraPose(context),
                    ESEditorPreviewQuality.Balanced,
                    "ES Particle Preview Pixel Test");
                Assert.IsNotNull(frame, "粒子预览没有生成 RenderTexture 快照。");

                Color32[] pixels = frame.GetPixels32();
                Color32 background = pixels[0];
                int visiblePixelCount = pixels.Count(pixel =>
                    Math.Abs(pixel.r - background.r)
                    + Math.Abs(pixel.g - background.g)
                    + Math.Abs(pixel.b - background.b) > 18);
                Assert.Greater(
                    visiblePixelCount,
                    16,
                    "粒子预览快照只有背景色，没有实际可见粒子像素。");
                Assert.IsFalse(sourceParticleSystem.isPlaying, "像素验收后原场景粒子被意外播放。");
            }
            finally
            {
                page.StopIndependentPreview();
                Selection.objects = previousSelection;
                if (frame != null) UnityEngine.Object.DestroyImmediate(frame);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ParticlePreviewSessionClonesCompleteRootAndRemapsInternalReferences()
        {
            ESEditorPreviewDiagnosticsSnapshot before = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            GameObject sourceRoot = null;
            ESEditorParticlePreviewSession session = null;
            try
            {
                sourceRoot = new GameObject("ES Particle Session Root", typeof(ParticleSystem));
                sourceRoot.AddComponent<ESParticlePreviewUnsafeProbe>();
                ESParticlePreviewUnsafeProbe.AwakeCount = 0;
                GameObject simulationSpace = new GameObject("Custom Space");
                simulationSpace.transform.SetParent(sourceRoot.transform, false);
                GameObject lightObject = new GameObject("Particle Light", typeof(Light));
                lightObject.transform.SetParent(sourceRoot.transform, false);

                ParticleSystem sourceSystem = sourceRoot.GetComponent<ParticleSystem>();
                sourceRoot.GetComponent<ParticleSystemRenderer>().enabled = false;
                ParticleSystem.MainModule sourceMain = sourceSystem.main;
                sourceMain.simulationSpace = ParticleSystemSimulationSpace.Custom;
                sourceMain.customSimulationSpace = simulationSpace.transform;
                ParticleSystem.LightsModule sourceLights = sourceSystem.lights;
                sourceLights.enabled = true;
                sourceLights.light = lightObject.GetComponent<Light>();

                session = new ESEditorParticlePreviewSession(
                    "ES Particle Session Reference Test",
                    maximumParticleSystems: 8);
                Assert.IsTrue(
                    session.Rebuild(
                        new[] { sourceRoot },
                        new[] { sourceSystem },
                        null,
                        12345,
                        2f,
                        true,
                        out string error),
                    error);
                Assert.IsTrue(session.TryGetPreviewSystem(sourceSystem, out ParticleSystem previewSystem));
                Assert.AreNotSame(sourceSystem, previewSystem);
                Assert.IsTrue(previewSystem.gameObject.activeInHierarchy, "粒子预览副本必须在完成映射后激活。" );
                Assert.AreNotSame(
                    sourceMain.customSimulationSpace,
                    previewSystem.main.customSimulationSpace,
                    "自定义模拟空间必须重映射到预览根副本，不能继续引用原场景。" );
                Assert.IsTrue(previewSystem.main.customSimulationSpace.IsChildOf(previewSystem.transform));
                Assert.AreNotSame(
                    sourceLights.light,
                    previewSystem.lights.light,
                    "粒子灯光必须重映射到预览根副本。" );
                Assert.IsTrue(previewSystem.lights.light.transform.IsChildOf(previewSystem.transform));
                Assert.IsFalse(
                    previewSystem.GetComponent<ParticleSystemRenderer>().enabled,
                    "粒子预览必须保留源 Renderer 的启停状态，不能为了可见性篡改作者配置。" );
                Assert.AreEqual(0, session.UnresolvedReferenceCount);
                Assert.GreaterOrEqual(session.SkippedComponentCount, 1);
                Assert.AreEqual(
                    0,
                    ESParticlePreviewUnsafeProbe.AwakeCount,
                    "预览复制不得执行来源根上的业务 MonoBehaviour。" );
                Assert.IsFalse(sourceSystem.isPlaying);

                ESEditorPreviewDiagnosticsSnapshot during = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
                Assert.GreaterOrEqual(during.ActiveScopeCount, before.ActiveScopeCount + 2);
            }
            finally
            {
                session?.Dispose();
                if (sourceRoot != null) UnityEngine.Object.DestroyImmediate(sourceRoot);
            }

            ESEditorPreviewDiagnosticsSnapshot after = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            Assert.AreEqual(before.ActiveScopeCount, after.ActiveScopeCount);
            Assert.AreEqual(before.ActiveRenderContextCount, after.ActiveRenderContextCount);
        }

        [Test]
        public void ParticlePreviewSessionPreservesAncestorMatricesAndMultiRootOffsets()
        {
            GameObject parentA = null;
            GameObject parentB = null;
            ESEditorParticlePreviewSession session = null;
            try
            {
                parentA = new GameObject("ES Particle Matrix Parent A");
                parentA.transform.SetPositionAndRotation(new Vector3(12f, 3f, -7f), Quaternion.Euler(11f, 37f, 5f));
                parentA.transform.localScale = new Vector3(2f, 0.75f, 1.35f);
                var sourceA = new GameObject("ES Particle Matrix A", typeof(ParticleSystem));
                sourceA.transform.SetParent(parentA.transform, false);
                sourceA.transform.localPosition = new Vector3(1.2f, -0.4f, 2.1f);
                sourceA.transform.localRotation = Quaternion.Euler(23f, -19f, 41f);
                sourceA.transform.localScale = new Vector3(0.6f, 1.4f, 0.9f);

                parentB = new GameObject("ES Particle Matrix Parent B");
                parentB.transform.SetPositionAndRotation(new Vector3(-8f, 6f, 14f), Quaternion.Euler(-7f, 81f, 13f));
                parentB.transform.localScale = new Vector3(0.8f, 1.7f, 1.1f);
                var sourceB = new GameObject("ES Particle Matrix B", typeof(ParticleSystem));
                sourceB.transform.SetParent(parentB.transform, false);
                sourceB.transform.localPosition = new Vector3(-2.4f, 1.1f, 0.3f);
                sourceB.transform.localRotation = Quaternion.Euler(9f, 28f, -16f);

                ParticleSystem systemA = sourceA.GetComponent<ParticleSystem>();
                ParticleSystem systemB = sourceB.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule mainA = systemA.main;
                mainA.simulationSpace = ParticleSystemSimulationSpace.Custom;
                mainA.customSimulationSpace = parentA.transform;
                Matrix4x4 sourceMatrixA = sourceA.transform.localToWorldMatrix;
                Matrix4x4 sourceMatrixB = sourceB.transform.localToWorldMatrix;
                Vector3 sourceAnchor = sourceA.transform.position;

                session = new ESEditorParticlePreviewSession("ES Particle Matrix Test");
                Assert.IsTrue(session.Rebuild(
                    new[] { sourceA, sourceB },
                    new[] { systemA, systemB },
                    null,
                    12345,
                    2f,
                    true,
                    out string error), error);
                Assert.IsTrue(session.TryGetPreviewSystem(systemA, out ParticleSystem previewA));
                Assert.IsTrue(session.TryGetPreviewSystem(systemB, out ParticleSystem previewB));
                Assert.AreNotSame(parentA.transform, previewA.main.customSimulationSpace);
                Matrix4x4 expectedCustomSpaceMatrix = Matrix4x4.Translate(
                    session.RenderContext.GroupOrigin - sourceAnchor) * parentA.transform.localToWorldMatrix;
                Assert.Less(
                    Vector3.Distance(
                        previewA.main.customSimulationSpace.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero),
                        expectedCustomSpaceMatrix.MultiplyPoint3x4(Vector3.zero)),
                    0.001f,
                    "父级自定义模拟空间必须映射到保持同一坐标矩阵的预览载体。" );

                Vector3 translation = session.RenderContext.GroupOrigin - sourceAnchor;
                Matrix4x4 expectedA = Matrix4x4.Translate(translation) * sourceMatrixA;
                Matrix4x4 expectedB = Matrix4x4.Translate(translation) * sourceMatrixB;
                for (int i = 0; i < 16; i++)
                {
                    Assert.That(previewA.transform.localToWorldMatrix[i], Is.EqualTo(expectedA[i]).Within(0.001f), "Root A matrix index " + i);
                    Assert.That(previewB.transform.localToWorldMatrix[i], Is.EqualTo(expectedB[i]).Within(0.001f), "Root B matrix index " + i);
                }

                Vector3 previewLocalB = session.SourceWorldToPreviewLocalPoint(sourceB.transform.position);
                Assert.Less(
                    Vector3.Distance(session.PreviewLocalToSourceWorldPoint(previewLocalB), sourceB.transform.position),
                    0.001f);
                Assert.Less(
                    Vector3.Distance(session.RenderContext.PreviewLocalToWorldPoint(previewLocalB), previewB.transform.position),
                    0.001f);
            }
            finally
            {
                session?.Dispose();
                if (parentA != null) UnityEngine.Object.DestroyImmediate(parentA);
                if (parentB != null) UnityEngine.Object.DestroyImmediate(parentB);
            }
        }

        [Test]
        public void PreviewContextOwnsCoordinateContractOrbitMathAndOneMeterReference()
        {
            ESEditorPreviewDiagnosticsSnapshot before = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            ESEditorPreviewRenderContext context = null;
            try
            {
                context = new ESEditorPreviewRenderContext(
                    "ES Preview Coordinate Test",
                    ESEditorPreviewSceneMode.PreviewScene);
                context.Ensure();
                Vector3 local = new Vector3(1.25f, -2f, 3.5f);
                Vector3 world = context.PreviewLocalToWorldPoint(local);
                Assert.Less(Vector3.Distance(context.WorldToPreviewLocalPoint(world), local), 0.0001f);

                var view = new ESEditorPreviewOrbitView();
                view.Reset(local, 2f, 20f, 10f);
                view.Orbit(new Vector2(10f, 8f));
                Assert.That(view.Yaw, Is.EqualTo(23.5f).Within(0.0001f));
                Assert.That(view.Pitch, Is.EqualTo(8f).Within(0.0001f));
                Vector3 focusBeforePan = view.FocusLocal;
                view.Pan(new Vector2(12f, -7f));
                Assert.AreNotEqual(focusBeforePan, view.FocusLocal);
                view.ZoomByWheel(3f, context.Camera.farClipPlane);
                Assert.Greater(view.Zoom, 1f);
                Bounds flatBounds = new Bounds(
                    context.PreviewLocalToWorldPoint(new Vector3(0f, 0.1f, 0f)),
                    new Vector3(6f, 1f, 4f));
                view.FrameRecommendedWorldBounds(context, flatBounds);
                Assert.That(view.Yaw, Is.EqualTo(40f).Within(0.001f));
                Assert.That(view.Pitch, Is.EqualTo(38f).Within(0.001f));
                Bounds tallBounds = new Bounds(
                    context.PreviewLocalToWorldPoint(new Vector3(0f, 2f, 0f)),
                    new Vector3(1f, 8f, 1f));
                view.FrameRecommendedWorldBounds(context, tallBounds);
                Assert.That(view.Pitch, Is.EqualTo(14f).Within(0.001f));
                ESEditorPreviewCameraPose pose = view.CreateCameraPose(context);
                Assert.Less(Vector3.Distance(pose.Center, context.PreviewLocalToWorldPoint(view.FocusLocal)), 0.0001f);

                int modelCount = context.ActiveModelGroupCount;
                context.SetScaleReferenceVisible(true);
                Assert.IsTrue(context.IsScaleReferenceVisible);
                Assert.AreEqual(modelCount, context.ActiveModelGroupCount, "尺寸参照不得注册成用户模型或进入模型 Bounds。" );
                Assert.IsTrue(context.TryGetScaleReferenceBounds(out Bounds referenceBounds));
                Assert.That(referenceBounds.size.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(referenceBounds.size.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(referenceBounds.size.z, Is.EqualTo(1f).Within(0.001f));
                Assert.That(referenceBounds.min.y, Is.EqualTo(context.GroupOrigin.y).Within(0.001f));
                context.SetScaleReferenceVisible(false);
                Assert.IsFalse(context.IsScaleReferenceVisible);
            }
            finally
            {
                context?.Dispose();
            }

            ESEditorPreviewDiagnosticsSnapshot after = ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            Assert.AreEqual(before.ActiveScopeCount, after.ActiveScopeCount);
            Assert.AreEqual(before.ActiveRenderContextCount, after.ActiveRenderContextCount);
        }

        [Test]
        public void ParticlePreviewSessionRejectsRootsAboveHardSystemBudget()
        {
            GameObject sourceRoot = null;
            ESEditorParticlePreviewSession session = null;
            try
            {
                sourceRoot = new GameObject("ES Particle Budget Root", typeof(ParticleSystem));
                GameObject child = new GameObject("Second Particle", typeof(ParticleSystem));
                child.transform.SetParent(sourceRoot.transform, false);
                ParticleSystem sourceSystem = sourceRoot.GetComponent<ParticleSystem>();

                session = new ESEditorParticlePreviewSession(
                    "ES Particle Session Budget Test",
                    maximumParticleSystems: 1);
                Assert.IsFalse(session.Rebuild(
                    new[] { sourceRoot },
                    new[] { sourceSystem },
                    null,
                    12345,
                    2f,
                    true,
                    out string error));
                StringAssert.Contains("硬上限 1", error);
                Assert.IsFalse(session.IsReady);
                Assert.AreEqual(0, session.ParticleSystemCount);
            }
            finally
            {
                session?.Dispose();
                if (sourceRoot != null) UnityEngine.Object.DestroyImmediate(sourceRoot);
            }
        }

        [Test]
        public void AssetPackageParticlePreviewStartsSharedSessionWithoutPlayingSource()
        {
            GameObject sourceRoot = null;
            ESAssetPackageDynamicPreviewPlayer player = null;
            try
            {
                sourceRoot = new GameObject("ES AssetPackage Particle Preview Test", typeof(ParticleSystem));
                sourceRoot.hideFlags = HideFlags.HideAndDontSave;
                ParticleSystem sourceSystem = sourceRoot.GetComponent<ParticleSystem>();
                sourceSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                player = new ESAssetPackageDynamicPreviewPlayer();
                MethodInfo ensureInstance = typeof(ESAssetPackageDynamicPreviewPlayer).GetMethod(
                    "EnsureInstance",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo sessionField = typeof(ESAssetPackageDynamicPreviewPlayer).GetField(
                    "particlePreviewSession",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(ensureInstance);
                Assert.IsNotNull(sessionField);

                ensureInstance.Invoke(player, new object[] { sourceRoot, null });
                var session = sessionField.GetValue(player) as ESEditorParticlePreviewSession;
                Assert.IsNotNull(session, "AssetPackage 没有建立公共粒子预览会话。");
                Assert.IsTrue(session.IsReady);
                Assert.IsTrue(session.IsPlaying, "AssetPackage 首次打开仍停在 0 秒，用户只能看到空预览背景。");
                Assert.IsFalse(sourceSystem.isPlaying, "AssetPackage 预览错误地播放了源 ParticleSystem。");
            }
            finally
            {
                player?.Dispose();
                if (sourceRoot != null) UnityEngine.Object.DestroyImmediate(sourceRoot);
            }
        }

        [Test]
        public void IMGUIPageDefinitionKeepsStateAndExplicitScrollOwnership()
        {
            var state = new object();
            ESMenuTreePageDefinition definition = ESMenuTreePageDefinition.ForIMGUI(
                "imgui.page",
                "IMGUI / 页面",
                state,
                (_, __) => { },
                false);

            var page = definition.Page as ESMenuTreeIMGUIPage<object>;
            Assert.IsNotNull(page);
            Assert.AreSame(state, page.State);
            Assert.IsFalse(page.UseVerticalScroll);
            Assert.AreSame(
                state,
                ((IESMenuTreePageStateProvider)page).PageState);

            page.Dispose();
        }

        [Test]
        public void AssetPackageBakeWindowBuildsStableIMGUIContractWithIntegratedPageActions()
        {
            Assert.IsTrue(
                InheritsGenericFoundation(
                    typeof(ESAssetPackageBakeWindow),
                    typeof(ESMenuTreeWindow<>)),
                "ESAssetPackageBakeWindow 尚未接入新版 ESMenuTreeWindow 底座。");

            const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo selectedBakeField = typeof(ESAssetPackageBakeWindow).GetField(
                "selectedBake",
                staticFlags);
            MethodInfo build = typeof(ESAssetPackageBakeWindow).GetMethod(
                "ESWindow_BuildMenuTree",
                instanceFlags);
            Assert.IsNotNull(selectedBakeField);
            Assert.IsNotNull(build);

            ScriptableObject bakeObject = ScriptableObject.CreateInstance(
                typeof(ESAssetPackageBakeData));
            ESAssetPackageBakeData bake = (ESAssetPackageBakeData)(object)bakeObject;
            bake.records.Add(new ESAssetPackageBakeRecord
            {
                category = ESAssetPackageCategory.Prefab,
                assetName = "ContractPrefab"
            });
            ESAssetPackageBakeWindow window =
                ScriptableObject.CreateInstance<ESAssetPackageBakeWindow>();
            var builder = new ESMenuTreeBuilder();
            try
            {
                selectedBakeField.SetValue(null, bake);
                build.Invoke(window, new object[] { builder });

                string categoryId = ESAssetPackageBakeWindow.GetCategoryPageId(
                    ESAssetPackageCategory.Prefab);
                Assert.AreEqual(3, builder.PageCount);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        ESAssetPackageBakeWindow.PageIdHome,
                        ESAssetPackageBakeWindow.PageIdCurrentOverview,
                        categoryId
                    },
                    builder.PagesById.Keys);

                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                {
                    Assert.IsInstanceOf<IESMenuTreePageStateProvider>(
                        node.Page,
                        node.StableId + " 未暴露页面上下文状态。");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(node.Definition.Keywords));
                }

                Assert.AreEqual(
                    1,
                    builder.PagesById[ESAssetPackageBakeWindow.PageIdHome]
                        .Definition.PageActions.Count);
                Assert.AreEqual(
                    7,
                    builder.PagesById[ESAssetPackageBakeWindow.PageIdCurrentOverview]
                        .Definition.PageActions.Count);
                Assert.AreEqual(
                    7,
                    builder.PagesById[categoryId].Definition.PageActions.Count);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "asset-package.index.save",
                        "asset-package.index.bake",
                        "asset-package.index.export",
                        "asset-package.index.preflight",
                        "asset-package.index.analyze",
                        "asset-package.index.repair-links",
                        "asset-package.index.rollback"
                    },
                    GetActionIds(builder.PagesById[
                        ESAssetPackageBakeWindow.PageIdCurrentOverview]));
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "asset-package.category.save",
                        "asset-package.category.refresh-preview",
                        "asset-package.category.select-used",
                        "asset-package.category.mark-all",
                        "asset-package.category.unmark-all",
                        "asset-package.category.export",
                        "asset-package.category.rollback"
                    },
                    GetActionIds(builder.PagesById[categoryId]));
            }
            finally
            {
                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                    node.Page?.Dispose();
                selectedBakeField.SetValue(null, null);
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(bakeObject);
            }
        }

        private static string[] GetActionIds(ESMenuTreeBuilder.Node node)
        {
            var ids = new string[node.Definition.PageActions.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = node.Definition.PageActions[i].Id;
            return ids;
        }

        [Test]
        public void InternalMigratedUtilityWindowsUseSinglePageFoundation()
        {
            Assembly editorAssembly = typeof(ESWindowLauncher).Assembly;
            string[] typeNames =
            {
                "ES.EditorInternal.ESGameCoreDefinitionEditorWindow",
                "ES.EditorInternal.ESInputActionDefineDrawer+ESInputActionImportWindow",
                "ES.EditorInternal.ESInputBindingDefineDrawer+ESInputActionBindingImportWindow"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                Type windowType = editorAssembly.GetType(typeNames[i], false);
                Assert.IsNotNull(windowType, typeNames[i] + " 未被目标 Editor 程序集收录。");
                Assert.IsTrue(
                    InheritsGenericFoundation(
                        windowType,
                        typeof(ESSinglePageIMGUIWindow<>)),
                    typeNames[i] + " 尚未接入统一单页窗口底座。");
            }
        }

        private static bool InheritsGenericFoundation(Type type, Type genericFoundation)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType
                    && current.GetGenericTypeDefinition() == genericFoundation)
                    return true;
            }
            return false;
        }

        [Test]
        public void WindowActivationMotionUsesStagedCommercialCurve()
        {
            Assert.AreEqual(0.735f, ESWindowActivationMotion.EvaluateScale(0f, 1f), 0.0001f);
            Assert.AreEqual(0.70f, ESWindowActivationMotion.EvaluateScale(0.08f, 1f), 0.0001f);
            Assert.AreEqual(1.095f, ESWindowActivationMotion.EvaluateScale(0.50f, 1f), 0.0001f);
            Assert.AreEqual(0.968f, ESWindowActivationMotion.EvaluateScale(0.70f, 1f), 0.0001f);
            Assert.AreEqual(1.018f, ESWindowActivationMotion.EvaluateScale(0.84f, 1f), 0.0001f);
            Assert.AreEqual(1f, ESWindowActivationMotion.EvaluateScale(1f, 1f), 0.0001f);

            Assert.AreEqual(1f, ESWindowActivationMotion.EvaluateOpacity(0.40f, 1f), 0.0001f);
            Assert.AreEqual(0f, ESWindowActivationMotion.EvaluateTranslateY(0.68f, 1f), 0.0001f);
        }

        [Test]
        public void WindowActivationMotionRespectsDisabledIntensity()
        {
            for (int i = 0; i <= 100; i++)
            {
                float progress = i / 100f;
                Assert.AreEqual(1f, ESWindowActivationMotion.EvaluateScale(progress, 0f), 0.0001f);
                Assert.AreEqual(1f, ESWindowActivationMotion.EvaluateOpacity(progress, 0f), 0.0001f);
                Assert.AreEqual(0f, ESWindowActivationMotion.EvaluateTranslateY(progress, 0f), 0.0001f);
            }
        }

        [Test]
        public void WindowActivationMotionStaysInsideSafeVisualBounds()
        {
            for (int i = 0; i <= 1000; i++)
            {
                float progress = i / 1000f;
                float scale = ESWindowActivationMotion.EvaluateScale(progress, 1f);
                float opacity = ESWindowActivationMotion.EvaluateOpacity(progress, 1f);
                float translateY = ESWindowActivationMotion.EvaluateTranslateY(progress, 1f);

                Assert.That(scale, Is.InRange(0.70f, 1.095f));
                Assert.That(opacity, Is.InRange(0.015f, 1f));
                Assert.That(translateY, Is.InRange(-2f, 30f));
            }
        }

        [Test]
        public void FloatingWindowActivationChangesActualBoundsWithoutMovingCenter()
        {
            Type frameActivation = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowFrameActivation",
                false);
            Assert.IsNotNull(frameActivation);
            MethodInfo evaluateFrame = frameActivation.GetMethod(
                "EvaluateFrame",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(evaluateFrame);

            var target = new Rect(120f, 80f, 1200f, 800f);
            Rect start = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { target, 0f, 0.78f });
            Rect overshoot = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { target, 0.50f, 0.78f });
            Rect end = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { target, 1f, 0.78f });

            Assert.Less(start.width, target.width * 0.50f);
            Assert.Less(start.height, target.height * 0.50f);
            Assert.Greater(overshoot.width, target.width);
            Assert.Greater(overshoot.height, target.height);
            Assert.AreEqual(target.center.x, start.center.x, 0.001f);
            Assert.AreEqual(target.center.y, start.center.y, 0.001f);
            Assert.AreEqual(target, end);
        }

        [Test]
        public void OpeningSweepHasVisibleMidpointAndCleanEndpoints()
        {
            Type sweepType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowOpeningSweep",
                false);
            Assert.IsNotNull(sweepType);
            MethodInfo evaluateOpacity = sweepType.GetMethod(
                "EvaluateOpacity",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo evaluatePosition = sweepType.GetMethod(
                "EvaluatePosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(evaluateOpacity);
            Assert.IsNotNull(evaluatePosition);

            float startOpacity = (float)evaluateOpacity.Invoke(
                null,
                new object[] { 0f, 0.78f });
            float middleOpacity = (float)evaluateOpacity.Invoke(
                null,
                new object[] { 0.5f, 0.78f });
            float endOpacity = (float)evaluateOpacity.Invoke(
                null,
                new object[] { 1f, 0.78f });
            float startPosition = (float)evaluatePosition.Invoke(
                null,
                new object[] { 0f, 1200f });
            float endPosition = (float)evaluatePosition.Invoke(
                null,
                new object[] { 1f, 1200f });

            Assert.AreEqual(0f, startOpacity, 0.0001f);
            Assert.Greater(middleOpacity, 0.30f);
            Assert.AreEqual(0f, endOpacity, 0.0001f);
            Assert.Less(startPosition, 0f);
            Assert.Greater(endPosition, 1200f);
        }

        [Test]
        public void FloatingWindowActivationCompletionReleasesAllStaticReferences()
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
            const int windowId = int.MinValue + 73;

            FieldInfo windowIdField = runningType.GetField(
                "WindowId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo rootField = runningType.GetField(
                "Root",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo gateField = runningType.GetField(
                "Gate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(windowIdField);
            Assert.IsNotNull(rootField);
            Assert.IsNotNull(gateField);
            windowIdField.SetValue(running, windowId);
            rootField.SetValue(running, root);

            var runningByWindow = (System.Collections.IDictionary)activationType
                .GetField("Running", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            var runningByRoot = (System.Collections.IDictionary)activationType
                .GetField("RunningByRoot", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            MethodInfo complete = activationType.GetMethod(
                "Complete",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(runningByWindow);
            Assert.IsNotNull(runningByRoot);
            Assert.IsNotNull(complete);

            runningByWindow.Add(windowId, running);
            runningByRoot.Add(root, running);
            complete.Invoke(null, new[] { running, (object)false });

            Assert.IsFalse(runningByWindow.Contains(windowId));
            Assert.IsFalse(runningByRoot.Contains(root));
            Assert.IsNull(rootField.GetValue(running));
            Assert.IsNull(gateField.GetValue(running));
        }

        [Test]
        public void FloatingWindowOpeningGateSuppressesAndRestoresRealContent()
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
            var visible = new VisualElement { name = "VisibleContent" };
            var hidden = new VisualElement { name = "HiddenContent" };
            hidden.style.display = DisplayStyle.None;
            root.Add(visible);
            root.Add(hidden);

            FieldInfo rootField = runningType.GetField(
                "Root",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo gateField = runningType.GetField(
                "Gate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rootField);
            Assert.IsNotNull(gateField);
            rootField.SetValue(running, root);

            MethodInfo createGate = activationType.GetMethod(
                "CreateOpeningGate",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo restoreGate = activationType.GetMethod(
                "RestoreOpeningGate",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(createGate);
            Assert.IsNotNull(restoreGate);

            var gate = (VisualElement)createGate.Invoke(null, new[] { running });
            gateField.SetValue(running, gate);
            Assert.AreEqual(DisplayStyle.None, visible.style.display.value);
            Assert.AreEqual(DisplayStyle.None, hidden.style.display.value);
            Assert.AreSame(gate, root.Q<VisualElement>("ESWindowOpeningGate"));
            Assert.AreEqual("ES", gate.Q<Label>("ESWindowOpeningGateBrand").text);

            restoreGate.Invoke(null, new[] { running });
            Assert.AreNotEqual(DisplayStyle.None, visible.style.display.value);
            Assert.AreEqual(DisplayStyle.None, hidden.style.display.value);
            Assert.IsNull(root.Q<VisualElement>("ESWindowOpeningGate"));
        }

        [Test]
        public void MenuTreeUnityIconResolverFallsBackWithoutThrowing()
        {
            Texture first = null;
            Texture second = null;
            Assert.DoesNotThrow(() =>
            {
                first = ESMenuTreeUnityIconResolver.Resolve(
                    "Definitely.Missing.ES.Icon.Name");
                second = ESMenuTreeUnityIconResolver.Resolve(
                    "Definitely.Missing.ES.Icon.Name");
            });
            Assert.AreSame(first, second);
        }

        [Test]
        public void SimpleToolsPageIconsUseStableConcreteSemantics()
        {
            Assert.AreEqual(
                "d_ParticleSystem Icon",
                ESMenuTreeUnityIconResolver.ResolveExplicitSemanticIcon(
                    "simple-tools.particle-system-adjustment",
                    "02 场景批处理/08 粒子系统批量调整",
                    "d_PreMatCube"));
            Assert.AreEqual(
                "d_Material Icon",
                ESMenuTreeUnityIconResolver.ResolveExplicitSemanticIcon(
                    "simple-tools.material-replacement",
                    "02 场景批处理/01 材质批量替换",
                    "d_Search Icon"));
            Assert.AreEqual(
                "d_Prefab Icon",
                ESMenuTreeUnityIconResolver.ResolveExplicitSemanticIcon(
                    "simple-tools.object-pool",
                    "04 ES 配置与集成/01 对象池与预热配置",
                    "d_PreMatCube"));
            Assert.AreEqual(
                "d_AnimationClip Icon",
                ESMenuTreeUnityIconResolver.ResolveExplicitSemanticIcon(
                    "simple-tools.animation-batch-setting",
                    "02 场景批处理/04 动画器批量设置",
                    "Animation.Record"));
        }

        [Test]
        public void MenuTreeSemanticFallbackUsesStableIdBeforeHostWords()
        {
            string iconName = ESMenuTreeUnityIconResolver.ResolveExplicitSemanticIcon(
                "simple-tools.particle-system-adjustment",
                "自动化与开发/Agent/粒子",
                "d_PreMatCube");
            Assert.AreEqual(
                "d_ParticleSystem Icon",
                iconName,
                "稳定页身份必须先表达粒子业务对象，不能被 Agent 或旧占位图标抢走。");
        }

        [Test]
        public void OpeningSweepCompletionRemovesHostAndReleasesRoot()
        {
            Type sweepType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowOpeningSweep",
                true);
            Type runningType = sweepType.GetNestedType("RunningSweep", BindingFlags.NonPublic);
            Assert.IsNotNull(runningType);
            object running = Activator.CreateInstance(runningType, true);
            var root = new VisualElement();
            var host = new VisualElement();
            var beam = new VisualElement();
            root.Add(host);
            host.Add(beam);

            FieldInfo rootField = runningType.GetField(
                "Root",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hostField = runningType.GetField(
                "Host",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo beamField = runningType.GetField(
                "Beam",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rootField);
            Assert.IsNotNull(hostField);
            Assert.IsNotNull(beamField);
            rootField.SetValue(running, root);
            hostField.SetValue(running, host);
            beamField.SetValue(running, beam);

            var runningSweeps = (System.Collections.IDictionary)sweepType
                .GetField("Running", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            MethodInfo complete = sweepType.GetMethod(
                "Complete",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(runningSweeps);
            Assert.IsNotNull(complete);

            runningSweeps.Add(root, running);
            complete.Invoke(null, new[] { running });

            Assert.IsFalse(runningSweeps.Contains(root));
            Assert.IsNull(host.parent);
            Assert.IsNull(rootField.GetValue(running));
            Assert.IsNull(hostField.GetValue(running));
            Assert.IsNull(beamField.GetValue(running));
        }

        [Test]
        public void OpeningSweepExposesExplicitReplayForSemiSleepWake()
        {
            Type sweepType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowOpeningSweep",
                true);
            MethodInfo replay = sweepType.GetMethod(
                "Replay",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(replay);
            Assert.AreEqual(typeof(void), replay.ReturnType);
            CollectionAssert.AreEqual(
                new[] { typeof(VisualElement) },
                replay.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        }

        [Test]
        public void SemiSleepUsesBottomRightTrayAndRestoresExactBounds()
        {
            Type presentation = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESEditorPresentation",
                true);
            MethodInfo evaluateTarget = presentation.GetMethod(
                "EvaluateSemiSleepTarget",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(Rect) },
                null);
            MethodInfo evaluateFrame = presentation.GetMethod(
                "EvaluateSemiSleepFrame",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(evaluateTarget);
            Assert.IsNotNull(evaluateFrame);

            var awake = new Rect(180f, 90f, 1174f, 714f);
            Rect asleep = (Rect)evaluateTarget.Invoke(null, new object[] { awake });
            Assert.AreEqual(100f, asleep.width, 0.001f);
            Assert.AreEqual(100f, asleep.height, 0.001f);
            Assert.AreEqual(awake.xMax - 12f, asleep.xMax, 0.001f);
            Assert.AreEqual(awake.yMax - 12f, asleep.yMax, 0.001f);

            Rect sleepEnd = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { awake, asleep, 1f, false, 0.78f });
            Rect wakeEnd = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { asleep, awake, 1f, true, 0.78f });
            Assert.AreEqual(asleep, sleepEnd);
            Assert.AreEqual(awake, wakeEnd);
        }

        [Test]
        public void SemiSleepMotionHasControlledCompressionAndWakeOvershoot()
        {
            Type presentation = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESEditorPresentation",
                true);
            MethodInfo evaluateFrame = presentation.GetMethod(
                "EvaluateSemiSleepFrame",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(evaluateFrame);

            var awake = new Rect(180f, 90f, 1174f, 714f);
            var asleep = new Rect(1254f, 90f, 100f, 100f);
            Rect sleepingMiddle = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { awake, asleep, 0.5f, false, 1f });
            Rect wakingMiddle = (Rect)evaluateFrame.Invoke(
                null,
                new object[] { asleep, awake, 0.5f, true, 1f });

            Assert.Less(sleepingMiddle.width, Mathf.Lerp(awake.width, asleep.width, 0.5f));
            Assert.Greater(wakingMiddle.width, Mathf.Lerp(asleep.width, awake.width, 0.5f));
            Assert.AreEqual(awake.xMax, sleepingMiddle.xMax, 0.001f);
            Assert.AreEqual(awake.xMax, wakingMiddle.xMax, 0.001f);
            Assert.AreEqual(
                Mathf.Lerp(awake.yMax, asleep.yMax, 0.5f),
                sleepingMiddle.yMax,
                0.001f);
            Assert.AreEqual(
                Mathf.Lerp(asleep.yMax, awake.yMax, 0.5f),
                wakingMiddle.yMax,
                0.001f);
        }

        [Test]
        public void SemiSleepTrayUsesRightToLeftSlotsThenWrapsUpward()
        {
            Type presentation = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESEditorPresentation",
                true);
            MethodInfo evaluateTarget = presentation.GetMethod(
                "EvaluateSemiSleepTarget",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(Rect), typeof(int) },
                null);
            Assert.IsNotNull(evaluateTarget);

            var awake = new Rect(180f, 90f, 1174f, 714f);
            Rect first = (Rect)evaluateTarget.Invoke(null, new object[] { awake, 0 });
            Rect second = (Rect)evaluateTarget.Invoke(null, new object[] { awake, 1 });
            Rect wrapped = (Rect)evaluateTarget.Invoke(null, new object[] { awake, 10 });
            Rect invalid = (Rect)evaluateTarget.Invoke(null, new object[] { awake, -1 });

            Assert.AreEqual(awake.xMax - 12f, first.xMax, 0.001f);
            Assert.AreEqual(first.x - 108f, second.x, 0.001f);
            Assert.AreEqual(first.y, second.y, 0.001f);
            Assert.AreEqual(first.x, wrapped.x, 0.001f);
            Assert.AreEqual(first.y - 108f, wrapped.y, 0.001f);
            Assert.AreEqual(first, invalid);
        }

        [Test]
        public void SemiSleepDraggedDockBoundsStayVisibleInsideTray()
        {
            Type presentation = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESEditorPresentation",
                true);
            MethodInfo clamp = presentation.GetMethod(
                "ClampSemiSleepDockBounds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(clamp);

            var tray = new Rect(100f, 80f, 1200f, 800f);
            Rect topLeft = (Rect)clamp.Invoke(
                null,
                new object[] { new Rect(-500f, -300f, 100f, 100f), tray });
            Rect bottomRight = (Rect)clamp.Invoke(
                null,
                new object[] { new Rect(1800f, 1400f, 100f, 100f), tray });

            Assert.AreEqual(tray.xMin, topLeft.xMin, 0.001f);
            Assert.AreEqual(tray.yMin, topLeft.yMin, 0.001f);
            Assert.AreEqual(tray.xMax, bottomRight.xMax, 0.001f);
            Assert.AreEqual(tray.yMax, bottomRight.yMax, 0.001f);
        }

        [Test]
        public void SemiSleepDragUsesWorkAreaBoundsWithoutArtificialDistanceLimit()
        {
            var tray = new Rect(100f, 80f, 1200f, 800f);
            var current = new Rect(600f, 420f, 100f, 100f);

            Rect normal = ESEditorPresentation.EvaluateSemiSleepDragFrame(
                current,
                new Vector2(18f, -12f),
                tray);
            Assert.AreEqual(current.x + 18f, normal.x, 0.001f);
            Assert.AreEqual(current.y - 12f, normal.y, 0.001f);

            Rect invalid = ESEditorPresentation.EvaluateSemiSleepDragFrame(
                current,
                new Vector2(float.NaN, 20f),
                tray);
            Assert.AreEqual(current, invalid);

            Rect huge = ESEditorPresentation.EvaluateSemiSleepDragFrame(
                current,
                new Vector2(10000f, 0f),
                tray);
            Assert.AreEqual(tray.xMax, huge.xMax, 0.001f);
            Assert.GreaterOrEqual(huge.xMin, tray.xMin);
            Assert.LessOrEqual(huge.xMax, tray.xMax);

            Rect farTopLeft = ESEditorPresentation.EvaluateSemiSleepDragFrame(
                current,
                new Vector2(-10000f, -10000f),
                tray);
            Assert.AreEqual(tray.xMin, farTopLeft.xMin, 0.001f);
            Assert.AreEqual(tray.yMin, farTopLeft.yMin, 0.001f);
        }

        [Test]
        public void SemiSleepTileOnlyCollapsesWhenItTouchesAWorkAreaEdge()
        {
            var workArea = new Rect(-1600f, 40f, 1600f, 900f);
            Assert.IsFalse(ESEditorPresentation.TryEvaluateEdgeTab(
                new Rect(-900f, 400f, 100f, 100f),
                workArea,
                out _,
                out _,
                out _));

            Assert.IsTrue(ESEditorPresentation.TryEvaluateEdgeTab(
                new Rect(-100f, 360f, 100f, 100f),
                workArea,
                out ESEditorPresentation.ESWindowEdge edge,
                out float offset,
                out Rect collapsed));
            Assert.AreEqual(ESEditorPresentation.ESWindowEdge.Right, edge);
            Assert.GreaterOrEqual(offset, 0f);
            Assert.AreEqual(workArea.xMax, collapsed.xMax, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabCollapsedLength, collapsed.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, collapsed.height, 0.001f);
        }

        [Test]
        public void EdgeTabHoverExtendsInwardWithoutChangingItsScreenEdge()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            const float offset = 240f;
            Rect collapsed = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea,
                ESEditorPresentation.ESWindowEdge.Left,
                offset,
                0f);
            Rect expanded = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea,
                ESEditorPresentation.ESWindowEdge.Left,
                offset,
                1f);

            Assert.AreEqual(workArea.xMin, collapsed.xMin, 0.001f);
            Assert.AreEqual(workArea.xMin, expanded.xMin, 0.001f);
            Assert.AreEqual(collapsed.y, expanded.y, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabCollapsedLength, collapsed.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, collapsed.height, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabExpandedLength, expanded.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, expanded.height, 0.001f);

            Rect bottom = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea,
                ESEditorPresentation.ESWindowEdge.Bottom,
                offset,
                1f);
            Assert.AreEqual(workArea.yMax, bottom.yMax, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, bottom.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabExpandedLength, bottom.height, 0.001f);
        }

        [Test]
        public void EdgeTabOrientationMatchesTheNearestScreenEdge()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            var cases = new[]
            {
                new { edge = ESEditorPresentation.ESWindowEdge.Left, tile = new Rect(100f, 400f, 100f, 100f), vertical = false },
                new { edge = ESEditorPresentation.ESWindowEdge.Right, tile = new Rect(1200f, 400f, 100f, 100f), vertical = false },
                new { edge = ESEditorPresentation.ESWindowEdge.Top, tile = new Rect(600f, 80f, 100f, 100f), vertical = true },
                new { edge = ESEditorPresentation.ESWindowEdge.Bottom, tile = new Rect(600f, 780f, 100f, 100f), vertical = true }
            };
            foreach (var item in cases)
            {
                Assert.IsTrue(ESEditorPresentation.TryEvaluateEdgeTab(
                    item.tile,
                    workArea,
                    out ESEditorPresentation.ESWindowEdge edge,
                    out _,
                    out Rect tab));
                Assert.AreEqual(item.edge, edge);
                Assert.AreEqual(item.vertical, tab.height > tab.width);
            }
        }

        [Test]
        public void SemiSleepTimingUsesFiveSecondsForTileToEdgeTabPromotion()
        {
            Assert.AreEqual(5f, ESEditorPresentation.SleepTileToEdgeTabDelay, 0.001f);
            Assert.IsTrue(ESEditorPresentation.ShouldPauseSleepTilePromotion(true, true));
            Assert.IsTrue(ESEditorPresentation.ShouldPauseSleepTilePromotion(true, false));
            Assert.IsFalse(ESEditorPresentation.ShouldPauseSleepTilePromotion(false, true));
        }

        [Test]
        public void TilePromotionDoesNotTreatFocusHistoryAsAnInteractionLease()
        {
            Assert.IsFalse(ESEditorPresentation.ShouldPauseSleepTilePromotion(false, false));
        }

        [Test]
        public void SemiSleepDragUsesScreenDeltaWithoutDoubleAddingWindowPosition()
        {
            var tray = new Rect(0f, 0f, 1000f, 700f);
            var current = new Rect(400f, 250f, 100f, 100f);
            Rect moved = ESEditorPresentation.EvaluateSemiSleepDragFrame(
                current,
                new Vector2(25f, -15f),
                tray);
            Assert.AreEqual(425f, moved.x, 0.001f);
            Assert.AreEqual(235f, moved.y, 0.001f);
            Assert.Less(moved.x, current.x + 100f);
            Assert.Greater(moved.y, current.y - 100f);
        }

        [Test]
        public void SettledSleepStatesRequireGeometryAgreement()
        {
            var expected = new Rect(100f, 80f, 100f, 100f);
            Assert.IsTrue(ESEditorPresentation.IsVisualStateGeometryConsistent(
                ESWindowVisualState.SleepTile, expected, expected));
            Assert.IsFalse(ESEditorPresentation.IsVisualStateGeometryConsistent(
                ESWindowVisualState.SleepTile,
                new Rect(100f, 80f, 900f, 600f),
                expected));
        }

        [Test]
        public void EdgeTabWakeKeepsTheSelectedScreenEdgeAnchored()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            const float offset = 240f;
            foreach (ESEditorPresentation.ESWindowEdge edge in
                     Enum.GetValues(typeof(ESEditorPresentation.ESWindowEdge)))
            {
                Rect collapsed = ESEditorPresentation.EvaluateEdgeTabBounds(workArea, edge, offset, 0f);
                Rect awake = new Rect(180f, 140f, 820f, 540f);
                Rect middle = ESEditorPresentation.EvaluateEdgeTabTransitionFrame(
                    collapsed, awake, 0.5f, edge);
                switch (edge)
                {
                    case ESEditorPresentation.ESWindowEdge.Left:
                        Assert.AreEqual(workArea.xMin, middle.xMin, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Right:
                        Assert.AreEqual(workArea.xMax, middle.xMax, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Top:
                        Assert.AreEqual(workArea.yMin, middle.yMin, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Bottom:
                        Assert.AreEqual(workArea.yMax, middle.yMax, 0.001f);
                        break;
                }
            }
        }

        [Test]
        public void EdgeTabAnimationKeepsEveryScreenEdgeStableAtMidFrame()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            const float offset = 240f;
            foreach (ESEditorPresentation.ESWindowEdge edge in
                     Enum.GetValues(typeof(ESEditorPresentation.ESWindowEdge)))
            {
                Rect collapsed = ESEditorPresentation.EvaluateEdgeTabBounds(
                    workArea, edge, offset, 0f);
                Rect expanded = ESEditorPresentation.EvaluateEdgeTabBounds(
                    workArea, edge, offset, 1f);
                Rect middle = ESEditorPresentation.EvaluateEdgeTabTransitionFrame(
                    collapsed, expanded, 0.5f, edge);

                switch (edge)
                {
                    case ESEditorPresentation.ESWindowEdge.Left:
                        Assert.AreEqual(workArea.xMin, middle.xMin, 0.001f);
                        Assert.Greater(middle.width, middle.height);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Right:
                        Assert.AreEqual(workArea.xMax, middle.xMax, 0.001f);
                        Assert.Greater(middle.width, middle.height);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Top:
                        Assert.AreEqual(workArea.yMin, middle.yMin, 0.001f);
                        Assert.Greater(middle.height, middle.width);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Bottom:
                        Assert.AreEqual(workArea.yMax, middle.yMax, 0.001f);
                        Assert.Greater(middle.height, middle.width);
                        break;
                }
            }
        }

        [Test]
        public void EdgeTabToSleepTileAnimationKeepsEveryScreenEdgeStableAtMidFrame()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            const float offset = 240f;
            foreach (ESEditorPresentation.ESWindowEdge edge in
                     Enum.GetValues(typeof(ESEditorPresentation.ESWindowEdge)))
            {
                Rect expanded = ESEditorPresentation.EvaluateEdgeTabBounds(
                    workArea, edge, offset, 1f);
                Rect tile = new Rect(
                    edge == ESEditorPresentation.ESWindowEdge.Right
                        ? workArea.xMax - ESEditorPresentation.SemiSleepSize
                        : expanded.x,
                    edge == ESEditorPresentation.ESWindowEdge.Bottom
                        ? workArea.yMax - ESEditorPresentation.SemiSleepSize
                        : expanded.y,
                    ESEditorPresentation.SemiSleepSize,
                    ESEditorPresentation.SemiSleepSize);
                Rect middle = ESEditorPresentation.EvaluateEdgeTabTransitionFrame(
                    expanded, tile, 0.5f, edge);

                switch (edge)
                {
                    case ESEditorPresentation.ESWindowEdge.Left:
                        Assert.AreEqual(workArea.xMin, middle.xMin, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Right:
                        Assert.AreEqual(workArea.xMax, middle.xMax, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Top:
                        Assert.AreEqual(workArea.yMin, middle.yMin, 0.001f);
                        break;
                    case ESEditorPresentation.ESWindowEdge.Bottom:
                        Assert.AreEqual(workArea.yMax, middle.yMax, 0.001f);
                        break;
                }
            }
        }

        [Test]
        public void EdgeTabExpansionUsesLengthAlongItsEdgeAndKeepsTheOppositeAxisFixed()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);
            const float offset = 240f;
            Rect rightCollapsed = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Right, offset, 0f);
            Rect rightExpanded = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Right, offset, 1f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabCollapsedLength, rightCollapsed.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, rightExpanded.height, 0.001f);
            Assert.AreEqual(workArea.xMax, rightCollapsed.xMax, 0.001f);
            Assert.AreEqual(workArea.xMax, rightExpanded.xMax, 0.001f);

            Rect topCollapsed = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Top, offset, 0f);
            Rect topExpanded = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Top, offset, 1f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabThickness, topCollapsed.width, 0.001f);
            Assert.AreEqual(ESEditorPresentation.EdgeTabCollapsedLength, topCollapsed.height, 0.001f);
            Assert.AreEqual(workArea.yMin, topExpanded.yMin, 0.001f);
        }

        [Test]
        public void EdgeTabToTileUsesTheCurrentEdgeAnchorInsteadOfAWindowCorner()
        {
            var from = new Rect(1104f, 320f, 196f, 44f);
            var tile = new Rect(1200f, 292f, 100f, 100f);
            Rect middle = ESEditorPresentation.EvaluateEdgeTabToTileFrame(
                from,
                tile,
                0.5f,
                ESEditorPresentation.ESWindowEdge.Right);
            Assert.AreEqual(from.xMax, middle.xMax, 0.001f);
            Assert.Greater(middle.height, from.height);
            Assert.Less(middle.y, from.y);
            Rect end = ESEditorPresentation.EvaluateEdgeTabToTileFrame(
                from,
                tile,
                1f,
                ESEditorPresentation.ESWindowEdge.Right);
            Assert.AreEqual(tile, end);
        }

        [Test]
        public void EdgeTabReverseAnimationScalesToRemainingDistance()
        {
            var collapsed = new Rect(100f, 80f, 56f, 44f);
            var expanded = new Rect(100f, 80f, 196f, 44f);
            var almostCollapsed = new Rect(100f, 80f, 70f, 44f);

            float full = ESEditorPresentation.EvaluateEdgeTabTransitionDuration(
                collapsed, expanded, 140f, 0.30f);
            float reverse = ESEditorPresentation.EvaluateEdgeTabTransitionDuration(
                almostCollapsed, collapsed, 140f, 0.30f);

            Assert.AreEqual(0.30f, full, 0.001f);
            Assert.Greater(reverse, 0f);
            Assert.Less(reverse, full * 0.35f);
        }

        [Test]
        public void EdgeTabHoverCommitOnlyResetsForIntentionalPointerMovement()
        {
            var origin = new Vector2(40f, 20f);
            Assert.IsTrue(ESEditorPresentation.ShouldResetEdgeTabHoverCommit(
                origin, origin, false));
            Assert.IsFalse(ESEditorPresentation.ShouldResetEdgeTabHoverCommit(
                origin, origin + new Vector2(2f, 1f), true));
            Assert.IsTrue(ESEditorPresentation.ShouldResetEdgeTabHoverCommit(
                origin, origin + new Vector2(3f, 0f), true));
        }

        [Test]
        public void EdgeTabDragMovesAlongItsOwnedEdgeWithoutBreakingTheAnchor()
        {
            var workArea = new Rect(100f, 80f, 1200f, 800f);

            Rect right = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Right, 220f, 1f);
            Rect movedRight = ESEditorPresentation.EvaluateEdgeTabDragFrame(
                right,
                new Vector2(-300f, 90f),
                workArea,
                ESEditorPresentation.ESWindowEdge.Right,
                out float rightOffset);
            Assert.AreEqual(workArea.xMax, movedRight.xMax, 0.001f);
            Assert.AreEqual(right.y + 90f, movedRight.y, 0.001f);
            Assert.AreEqual(right.size, movedRight.size);
            Assert.AreEqual(movedRight.y - workArea.yMin, rightOffset, 0.001f);

            Rect left = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Left, 420f, 0f);
            Rect movedLeft = ESEditorPresentation.EvaluateEdgeTabDragFrame(
                left,
                new Vector2(500f, -120f),
                workArea,
                ESEditorPresentation.ESWindowEdge.Left,
                out _);
            Assert.AreEqual(workArea.xMin, movedLeft.xMin, 0.001f);
            Assert.AreEqual(left.y - 120f, movedLeft.y, 0.001f);

            Rect top = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Top, 360f, 1f);
            Rect movedTop = ESEditorPresentation.EvaluateEdgeTabDragFrame(
                top,
                new Vector2(140f, 500f),
                workArea,
                ESEditorPresentation.ESWindowEdge.Top,
                out float topOffset);
            Assert.AreEqual(workArea.yMin, movedTop.yMin, 0.001f);
            Assert.AreEqual(top.x + 140f, movedTop.x, 0.001f);
            Assert.AreEqual(top.size, movedTop.size);
            Assert.AreEqual(movedTop.x - workArea.xMin, topOffset, 0.001f);

            Rect bottom = ESEditorPresentation.EvaluateEdgeTabBounds(
                workArea, ESEditorPresentation.ESWindowEdge.Bottom, 700f, 0f);
            Rect movedBottom = ESEditorPresentation.EvaluateEdgeTabDragFrame(
                bottom,
                new Vector2(-180f, -500f),
                workArea,
                ESEditorPresentation.ESWindowEdge.Bottom,
                out _);
            Assert.AreEqual(workArea.yMax, movedBottom.yMax, 0.001f);
            Assert.AreEqual(bottom.x - 180f, movedBottom.x, 0.001f);
        }

        [Test]
        public void SemiSleepStateMachineExposesInteractionHoldAndExplicitVisualStates()
        {
            CollectionAssert.AreEquivalent(
                new[] { "ActivePanel", "SleepTile", "EdgeTab", "EdgeTabHover" },
                Enum.GetNames(typeof(ESWindowVisualState)));
            Assert.AreEqual(0.12f, ESEditorPresentation.EdgeTabHoverIntentDelay, 0.001f);
            Assert.IsFalse(ESEditorPresentation.ShouldBeginEdgeTabHover(10d, 10.11d, true));
            Assert.IsTrue(ESEditorPresentation.ShouldBeginEdgeTabHover(10d, 10.12d, true));
            Assert.IsFalse(ESEditorPresentation.ShouldBeginEdgeTabHover(10d, 11d, false));
            Assert.AreEqual(1.65f, ESEditorPresentation.EdgeTabHoverCommitDelay, 0.001f);
            Assert.IsFalse(ESEditorPresentation.ShouldRestoreEdgeTabToTile(10d, 11.64d, true));
            Assert.IsTrue(ESEditorPresentation.ShouldRestoreEdgeTabToTile(10d, 11.65d, true));
            Assert.IsFalse(ESEditorPresentation.ShouldRestoreEdgeTabToTile(10d, 12d, false));
            Assert.IsTrue(ESEditorPresentation.ShouldPauseAutomaticCollection(
                false, false, 0, false, false, true, false));
            // A pointer inside a non-focused window is still an active interaction
            // surface. This prevents ContextMenu/child-popup focus changes from
            // starting an unexpected semi-sleep countdown.
            Assert.IsTrue(ESEditorPresentation.ShouldPauseAutomaticCollection(
                true, false, 0, false, false, false, false));
            Assert.IsFalse(ESEditorPresentation.ShouldPauseAutomaticCollection(
                false, false, 0, false, false, false, false));
            Assert.IsNotNull(typeof(ESEditorPresentation).GetMethod(
                "BeginWindowInteractionHold",
                BindingFlags.Static | BindingFlags.Public));
            Assert.AreEqual(
                typeof(ESWindowVisualState),
                typeof(ESEditorPresentation).GetMethod(
                    "GetWindowVisualState",
                    BindingFlags.Static | BindingFlags.Public)?.ReturnType);
            Assert.AreEqual(
                typeof(IDisposable),
                typeof(ESWindowFoundation).GetMethod(
                    "HoldInteraction",
                    BindingFlags.Static | BindingFlags.Public)?.ReturnType);
            Assert.AreEqual(
                typeof(ESWindowVisualState),
                typeof(ESWindowFoundation).GetMethod(
                    "GetVisualState",
                    BindingFlags.Static | BindingFlags.Public)?.ReturnType);
        }

        [Test]
        public void SemiSleepPublicCommandsExposeIndependentControlSurface()
        {
            Type presentation = typeof(ESEditorPresentation);
            Assert.IsNotNull(presentation.GetMethod(
                "CanWindowEnterSemiSleep",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(presentation.GetMethod(
                "RequestWindowSemiSleep",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(presentation.GetMethod(
                "RequestWindowWake",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(presentation.GetMethod(
                "IsWindowSemiSleeping",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(presentation.GetMethod(
                "SetWindowSemiSleepAllowed",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(presentation.GetMethod(
                "SetWindowPinned",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(typeof(ESWindowFoundation).GetProperty(
                "IsGlobalSemiSleepEnabled",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(typeof(ESWindowFoundation).GetMethod(
                "SetGlobalSemiSleepEnabled",
                BindingFlags.Static | BindingFlags.Public));
        }

        [Test]
        public void SemiSleepControlsRequireDeclaredHostAndUseResponsiveOverflow()
        {
            Assert.IsFalse(ESEditorPresentation.HasDeclaredSystemActionHost(
                new ESWindowActionHosts()));
            Assert.IsTrue(ESEditorPresentation.HasDeclaredSystemActionHost(
                new ESWindowActionHosts(system: new VisualElement())));

            DefaultSemiSleepContractWindow window =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            try
            {
                VisualElement systemHost = new VisualElement
                {
                    name = "DeclaredSystemActionHost"
                };
                window.rootVisualElement.Add(systemHost);

                ESWindowFoundation.Bind(window);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("ESWindowSystemActions"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("ESWindowSystemActionsFallback"));

                ESWindowFoundation.Bind(
                    window,
                    new ESWindowActionHosts(system: systemHost));
                Assert.IsNotNull(systemHost.Q<VisualElement>("ESWindowSystemActions"));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }

            Assert.IsFalse(ESEditorPresentation.ShouldCompactSystemActions(1174f));
            Assert.IsTrue(ESEditorPresentation.ShouldCompactSystemActions(640f));
            Assert.IsFalse(ESCmdAgentWindow.ShouldCollapseHeaderActions(1500f));
            Assert.IsTrue(ESCmdAgentWindow.ShouldCollapseHeaderActions(1174f));
        }

        [Test]
        public void AdvancedDialogContractsStableAuxiliaryActionsAndFieldValidation()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.contracts",
                allowMainWorkspaceFallback = true,
            };
            request.AddText("name", "名称", required: true);
            request.AddAuxiliaryAction("preview", "预览", _ => { });
            request.validateDetailed = _ => new ESAdvancedDialogValidation("名称无效。", "name");

            Assert.AreEqual("preview", request.auxiliaryActions[0].id);
            Assert.AreEqual(ESAdvancedDialogActionRole.Secondary, request.auxiliaryActions[0].role);
            ESAdvancedDialogValidation validation = request.validateDetailed(null);
            Assert.AreEqual("name", validation.fieldId);
            Assert.AreEqual("名称无效。", validation.message);
        }

        [Test]
        public void AdvancedDialogCentersWithinMainWindowAndChoosesReadableActionText()
        {
            Rect main = new Rect(100f, 50f, 1200f, 800f);
            Rect dialog = ESAdvancedDialogWindow.CalculateCenteredPosition(
                main,
                new Vector2(460f, 260f),
                new Vector2(600f, 620f),
                720f);

            Assert.AreEqual(main.center.x, dialog.center.x, 0.001f);
            Assert.AreEqual(main.center.y, dialog.center.y, 0.001f);
            Assert.LessOrEqual(dialog.width, main.width - 48f);
            Assert.LessOrEqual(dialog.height, main.height - 64f);

            Color onDark = ESAdvancedDialogWindow.GetReadableActionTextColor(
                new Color(0.08f, 0.24f, 0.12f));
            Color onLight = ESAdvancedDialogWindow.GetReadableActionTextColor(
                new Color(0.65f, 0.90f, 0.70f));
            Assert.Greater(onDark.r, 0.9f);
            Assert.Less(onLight.r, 0.1f);

            Color primary = ESEditorPresentation.PrimaryActionColor;
            Assert.Less(Mathf.Abs(primary.b - primary.r), 0.08f,
                "主操作底色必须保持中性石墨色，不能重新进入蓝色体系。");
            Assert.Greater(ESEditorPresentation.PrimaryActionTextColor.r, 0.9f);

            Color selection = ESEditorPresentation.SelectionColor;
            Assert.Less(selection.b, 0.70f, "通用选中色不能退回天蓝色；它必须适合边框和导引线。");
            Assert.Greater(selection.g, selection.r);

            Color selectedSurface = ESEditorPresentation.SelectedSurfaceColor;
            Assert.Less(Mathf.Abs(selectedSurface.b - selectedSurface.r), 0.16f,
                "选中表面必须是低对比中性表面，不能把标记色铺成大面积蓝色背景。");
            Assert.Greater(Vector4.Distance(selectedSurface, selection), 0.05f,
                "选中表面与选中标记必须是独立语义色，不能复用同一颜色。");

            Color inactiveAction = ESEditorPresentation.InactiveActionColor;
            Assert.Less(Mathf.Abs(inactiveAction.b - inactiveAction.r), 0.16f,
                "禁用/关闭操作必须使用中性底色，不能把 Warning 色铺到工具栏按钮。");
            Assert.Greater(Vector4.Distance(inactiveAction, ESEditorPresentation.WarningColor), 0.20f,
                "禁用/关闭操作必须与高饱和警告色保持足够区分。");

            Color darkActiveAccent = new Color(0.48f, 0.78f, 1f, 0.92f);
            Color lightActiveAccent = new Color(0.12f, 0.46f, 0.82f, 0.92f);
            foreach (Color activeAction in new[]
                     {
                         ESEditorPresentation.GetActiveActionColor(true, darkActiveAccent),
                         ESEditorPresentation.GetActiveActionColor(false, lightActiveAccent),
                         ESEditorPresentation.GetActiveActionColor(true, Color.white),
                         ESEditorPresentation.GetActiveActionColor(false, Color.white)
                     })
            {
                Assert.Greater(
                    CalculateContrastRatio(
                        ESEditorPresentation.PrimaryActionTextColor,
                        activeAction),
                    4.5f,
                    "暗色、浅色及极亮自定义主题的操作底色都必须保持白字可读性。");
                Assert.AreEqual(1f, activeAction.a, 0.001f,
                    "文字按钮的状态底色必须不透明，避免与宿主浅色背景混合后失去对比度。");
            }
            Assert.Greater(
                Vector4.Distance(
                    ESEditorPresentation.GetActiveActionColor(true, darkActiveAccent),
                    darkActiveAccent),
                0.10f,
                "激活操作底色不能直接复用高亮状态强调色，否则会与浅色文字混在一起。");

        }

        [Test]
        public void PresentationThemeSnapshotMatchesCurrentThemeAndSkinGenerations()
        {
            ESEditorPresentation.ESPresentationThemeSnapshot first =
                ESEditorPresentation.CurrentPresentationTheme;
            ESEditorPresentation.ESPresentationThemeSnapshot second =
                ESEditorPresentation.CurrentPresentationTheme;

            Assert.AreEqual(ESEditorPresentation.ThemeGeneration, first.ThemeGeneration);
            Assert.AreEqual(ESEditorPresentation.SkinGeneration, first.SkinGeneration);
            Assert.AreEqual(first.ThemeGeneration, second.ThemeGeneration);
            Assert.AreEqual(first.SkinGeneration, second.SkinGeneration);
            Assert.AreEqual(first.ProSkin, second.ProSkin);
            Assert.AreEqual(first.Density, second.Density, 0.001f);
            Assert.AreEqual(first.MotionEnabled, second.MotionEnabled);
            AssertColorEqual(ESEditorPresentation.WindowSurfaceColor, first.WindowSurface);
            AssertColorEqual(ESEditorPresentation.ActiveActionColor, first.ActiveActionSurface);
        }

        [Test]
        public void PresentationResolverKeepsSurfaceRolesNeutralAndActionStatesReadable()
        {
            ESEditorPresentation.ESPresentationThemeSnapshot theme =
                ESEditorPresentation.CurrentPresentationTheme;
            ESEditorPresentation.ESPresentationStyle window =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.WindowSurface);
            ESEditorPresentation.ESPresentationStyle selectedControl =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Selected);
            ESEditorPresentation.ESPresentationStyle warningStatus =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Status,
                    ESEditorPresentation.ESPresentationState.Warning);
            ESEditorPresentation.ESPresentationStyle modifiedStatus =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Status,
                    ESEditorPresentation.ESPresentationState.Selected);
            ESEditorPresentation.ESPresentationStyle errorAction =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Error);

            AssertColorEqual(theme.WindowSurface, window.BackgroundColor);
            AssertColorEqual(theme.ActiveActionSurface, selectedControl.BackgroundColor);
            AssertColorEqual(theme.ActionText, selectedControl.TextColor);
            AssertColorEqual(theme.InsetSurface, warningStatus.BackgroundColor);
            AssertColorEqual(theme.Warning, warningStatus.TextColor);
            AssertColorEqual(theme.Warning, warningStatus.BorderColor);
            AssertColorEqual(theme.InsetSurface, modifiedStatus.BackgroundColor);
            AssertColorEqual(theme.Selection, modifiedStatus.TextColor);
            Assert.Greater(
                CalculateContrastRatio(errorAction.TextColor, errorAction.BackgroundColor),
                4.5f,
                "错误操作也必须使用可承载白字的暗色背景，不能直接铺高亮错误色。");
        }

        [Test]
        public void PresentationInteractionOverlaysDoNotEraseSemanticState()
        {
            ESEditorPresentation.ESPresentationStyle warningRest =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Warning);
            ESEditorPresentation.ESPresentationStyle warningHover =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Warning,
                    ESEditorPresentation.ESPresentationInteraction.Hover);
            ESEditorPresentation.ESPresentationStyle warningFocused =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Warning,
                    ESEditorPresentation.ESPresentationInteraction.Focused);
            ESEditorPresentation.ESPresentationStyle disabled =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Disabled);

            Assert.Greater(
                Vector4.Distance(warningRest.BackgroundColor, warningHover.BackgroundColor),
                0.001f);
            AssertColorEqual(warningRest.BorderColor, warningHover.BorderColor);
            AssertColorEqual(warningRest.BorderColor, warningFocused.BorderColor);
            Assert.Less(disabled.Opacity, 0.7f);
        }

        [Test]
        public void PresentationApplicationCanPreserveExistingBorderWidths()
        {
            var element = new VisualElement();
            element.style.borderLeftWidth = 3f;
            element.style.borderRightWidth = 0f;

            ESEditorPresentation.ApplyPresentationStyle(
                element,
                ESEditorPresentation.ESPresentationRole.RaisedSurface,
                ESEditorPresentation.ESPresentationState.Warning,
                borderWidth: null);

            Assert.AreEqual(3f, element.style.borderLeftWidth.value, 0.001f);
            Assert.AreEqual(0f, element.style.borderRightWidth.value, 0.001f);
            AssertColorEqual(
                ESEditorPresentation.WarningColor,
                element.style.borderLeftColor.value);
        }

        [Test]
        public void SharedWindowControlsUsePresentationFoundation()
        {
            Button button = ESWindowPresentation.CreateHeaderActionButton(
                null,
                "全局开关",
                "测试",
                () => { });
            Assert.IsInstanceOf<ESPresentationButton>(button);

            ESWindowPresentation.SetButtonPresentationState(
                button,
                ESEditorPresentation.ESPresentationState.Selected);
            ESEditorPresentation.ESPresentationStyle selected =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Selected);
            AssertColorEqual(selected.BackgroundColor, button.style.backgroundColor.value);
            AssertColorEqual(selected.TextColor, button.Q<Label>().style.color.value);

            var shell = new ESWindowShell("测试", "语义壳层", false);
            Assert.AreEqual(
                Wrap.Wrap,
                shell.Header.Q<VisualElement>("ESWindowTitleRow").style.flexWrap.value,
                "标题与动作必须允许在窄窗口确定性换行。");
            Assert.AreEqual(0f, shell.HeaderToolbar.style.minWidth.value.value, 0.001f);
            Assert.AreEqual(1f, shell.HeaderToolbar.style.flexShrink.value, 0.001f);
            Assert.AreEqual(Wrap.Wrap, shell.HeaderToolbar.style.flexWrap.value);
            Assert.AreEqual(Overflow.Visible, shell.HeaderToolbar.style.overflow.value,
                "标题栏不得裁掉唯一系统菜单；窄宽度应通过换行和动作 overflow 收口。");
            Assert.Greater(
                shell.Toolbar.style.borderTopLeftRadius.value.value,
                0f,
                "工具栏应使用卡片级圆角，而不是只让窗口外壳有圆角。");
            Assert.Greater(
                shell.Content.style.borderTopLeftRadius.value.value,
                0f,
                "内容承载区应使用统一圆角，避免正文区域退回直角矩形。");
            shell.ApplyCompactHostChrome();
            Assert.AreEqual(StyleKeyword.None, shell.Header.style.maxHeight.keyword,
                "紧凑壳不得用固定 Header 高度裁掉换行动作。");
            Assert.AreEqual(Wrap.Wrap, shell.HeaderToolbar.style.flexWrap.value);
            shell.SetStatus("需要处理", ESStatusKind.Warning);
            AssertColorEqual(
                ESEditorPresentation.WindowInsetSurfaceColor,
                shell.StatusBar.style.backgroundColor.value);
            AssertColorEqual(
                ESEditorPresentation.WarningColor,
                shell.StatusLabel.style.color.value);
        }

        [Test]
        public void PresentationButtonKeepsSemanticStateAcrossHoverAndFocus()
        {
            foreach (ESEditorPresentation.ESPresentationState semanticState in new[]
                     {
                         ESEditorPresentation.ESPresentationState.Error,
                         ESEditorPresentation.ESPresentationState.Selected,
                         ESEditorPresentation.ESPresentationState.Inactive
                     })
            {
                Button button = ESWindowPresentation.CreateHeaderActionButton(
                    null,
                    "状态按钮",
                    "测试",
                    () => { });
                ESWindowPresentation.SetButtonPresentationState(button, semanticState);

                using (MouseEnterEvent hover = MouseEnterEvent.GetPooled())
                    button.SendEvent(hover);
                ESEditorPresentation.ESPresentationStyle hoverStyle =
                    ESEditorPresentation.ResolvePresentationStyle(
                        ESEditorPresentation.ESPresentationRole.Control,
                        semanticState,
                        ESEditorPresentation.ESPresentationInteraction.Hover);
                AssertColorEqual(hoverStyle.BackgroundColor, button.style.backgroundColor.value);
                AssertColorEqual(hoverStyle.BorderColor, button.style.borderBottomColor.value);

                using (MouseLeaveEvent leave = MouseLeaveEvent.GetPooled())
                    button.SendEvent(leave);
                using (FocusInEvent focus = FocusInEvent.GetPooled())
                    button.SendEvent(focus);
                ESEditorPresentation.ESPresentationStyle focusStyle =
                    ESEditorPresentation.ResolvePresentationStyle(
                        ESEditorPresentation.ESPresentationRole.Control,
                        semanticState,
                        ESEditorPresentation.ESPresentationInteraction.Focused);
                AssertColorEqual(focusStyle.BackgroundColor, button.style.backgroundColor.value);
                AssertColorEqual(focusStyle.BorderColor, button.style.borderBottomColor.value);
            }
        }

        [Test]
        public void SetButtonEnabledRefreshesDisabledAndRestoredVisualsImmediately()
        {
            Button button = ESWindowPresentation.CreateToolbarButton(
                "执行",
                "测试",
                () => { });
            ESWindowPresentation.SetButtonPresentationState(
                button,
                ESEditorPresentation.ESPresentationState.Selected);
            using (MouseEnterEvent hover = MouseEnterEvent.GetPooled())
                button.SendEvent(hover);

            ESWindowPresentation.SetButtonEnabled(button, false);
            ESEditorPresentation.ESPresentationStyle disabled =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Disabled);
            Assert.AreEqual(disabled.Opacity, button.style.opacity.value, 0.001f);
            AssertColorEqual(disabled.BackgroundColor, button.style.backgroundColor.value);
            AssertColorEqual(disabled.TextColor, button.style.color.value);

            ESWindowPresentation.SetButtonEnabled(button, true);
            ESEditorPresentation.ESPresentationStyle selected =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Control,
                    ESEditorPresentation.ESPresentationState.Selected);
            Assert.AreEqual(selected.Opacity, button.style.opacity.value, 0.001f);
            AssertColorEqual(selected.BackgroundColor, button.style.backgroundColor.value);
            AssertColorEqual(selected.TextColor, button.style.color.value);
        }

        [Test]
        public void ThemeInspectorNotificationInvalidatesSerializedThemeSnapshot()
        {
            ESGlobalEditorTheme previousTheme = ESGlobalEditorTheme.Instance;
            ESGlobalEditorTheme testTheme = ScriptableObject.CreateInstance<ESGlobalEditorTheme>();
            try
            {
                testTheme.RestoreDefault();
                testTheme.density = 0.90f;
                ESGlobalEditorTheme.Instance = testTheme;
                ESEditorPresentation.InvalidateTheme();
                ESEditorPresentation.ESPresentationThemeSnapshot before =
                    ESEditorPresentation.CurrentPresentationTheme;

                var serializedTheme = new SerializedObject(testTheme);
                serializedTheme.Update();
                serializedTheme.FindProperty("density").floatValue = 1.14f;
                Assert.IsTrue(serializedTheme.ApplyModifiedProperties());
                ESGlobalEditorThemeChangeBridge.NotifyThemeChanged(testTheme);

                ESEditorPresentation.ESPresentationThemeSnapshot after =
                    ESEditorPresentation.CurrentPresentationTheme;
                Assert.AreNotSame(before, after);
                Assert.Greater(after.ThemeGeneration, before.ThemeGeneration);
                Assert.AreEqual(1.14f, after.Density, 0.001f);
            }
            finally
            {
                ESGlobalEditorTheme.Instance = previousTheme;
                ESEditorPresentation.InvalidateTheme();
                UnityEngine.Object.DestroyImmediate(testTheme);
            }
        }

        private static void AssertColorEqual(Color expected, Color actual)
        {
            Assert.Less(Vector4.Distance(expected, actual), 0.001f);
        }

        private static float CalculateContrastRatio(Color first, Color second)
        {
            float firstLuminance = CalculateRelativeLuminance(first);
            float secondLuminance = CalculateRelativeLuminance(second);
            float lighter = Mathf.Max(firstLuminance, secondLuminance);
            float darker = Mathf.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float CalculateRelativeLuminance(Color color)
        {
            return ToLinearColorChannel(color.r) * 0.2126f
                + ToLinearColorChannel(color.g) * 0.7152f
                + ToLinearColorChannel(color.b) * 0.0722f;
        }

        private static float ToLinearColorChannel(float value)
        {
            return value <= 0.03928f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        [Test]
        public void AdvancedDialogExposesAsyncModalQueueOwnerAndCustomContentContracts()
        {
            Assert.IsNotNull(typeof(ESAdvancedDialogWindow).GetMethod(
                "ShowAsync",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(typeof(ESAdvancedDialogWindow).GetMethod(
                "ShowModal",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNotNull(typeof(ESProgressCenter).GetMethod(
                "RunSteps",
                BindingFlags.Static | BindingFlags.Public));

            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.contract",
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.Queue,
                initialFocusFieldId = "name",
                asyncValidationDelayMs = 90,
                createCustomContent = _ => new Label("扩展内容"),
                releaseCustomContent = _ => { },
                validateAsync = (_, token) => Task.FromResult<ESAdvancedDialogValidation>(null),
                confirmAsync = (_, progress, token) => Task.CompletedTask,
            };
            request.AddText("name", "名称");
            request.AddAuxiliaryActionAsync(
                "async.preview",
                "异步预览",
                (_, progress, token) => Task.CompletedTask);

            Assert.AreEqual("tests.dialog.contract", request.dialogId);
            Assert.AreEqual(ESDialogDuplicatePolicy.Queue, request.duplicatePolicy);
            Assert.IsNotNull(request.createCustomContent(null));
            Assert.IsNotNull(request.validateAsync);
            Assert.IsNotNull(request.confirmAsync);
            Assert.IsNotNull(request.auxiliaryActions[0].executeAsync);
        }

        [Test]
        public void ProgressAndDialogVisualTreeRebuildUnbindBeforeClear()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "Plugins",
                    "ES",
                    "Editor",
                    "EditorTools",
                    "ESAdvancedDialog",
                    "ESAdvancedDialog.cs"),
                Encoding.UTF8);
            foreach (string declaration in new[]
                     {
                         "public sealed class ESProgressCenterWindow",
                         "public sealed class ESAdvancedDialogWindow"
                     })
            {
                string typeBody = ExtractBalancedSourceBlock(source, declaration);
                string createGui = ExtractBalancedSourceBlock(typeBody, "public void CreateGUI()");
                int unbind = createGui.IndexOf(
                    "ESWindowFoundation.Unbind(this);",
                    StringComparison.Ordinal);
                int clear = createGui.IndexOf(
                    "rootVisualElement.Clear();",
                    StringComparison.Ordinal);
                Assert.GreaterOrEqual(unbind, 0, declaration + " 重建前必须调用 Unbind。");
                Assert.Greater(clear, unbind, declaration + " 必须先 Unbind 再 Clear VisualTree。");
            }
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void AdvancedDialogEscapeRespectsBusyCancellationPolicy(
            bool allowOperationCancellation,
            bool expectedCancellation)
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.escape-policy." + Guid.NewGuid().ToString("N"),
                title = "取消策略测试",
                allowMainWorkspaceFallback = true,
                allowOperationCancellation = allowOperationCancellation,
                animateOpening = false,
            };
            ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, null);
            try
            {
                window.CreateGUI();
                Type windowType = typeof(ESAdvancedDialogWindow);
                MethodInfo beginBusy = windowType.GetMethod(
                    "BeginBusy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onKeyDown = windowType.GetMethod(
                    "OnKeyDown",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo cancellationField = windowType.GetField(
                    "operationCancellation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo busyField = windowType.GetField(
                    "busy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo busyLabelField = windowType.GetField(
                    "busyLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(beginBusy);
                Assert.IsNotNull(onKeyDown);
                Assert.IsNotNull(cancellationField);
                Assert.IsNotNull(busyField);
                Assert.IsNotNull(busyLabelField);

                beginBusy.Invoke(window, new object[] { "正在执行测试操作", "escape-policy" });
                var cancellation = cancellationField.GetValue(window) as CancellationTokenSource;
                Assert.IsNotNull(cancellation);

                KeyDownEvent escape = KeyDownEvent.GetPooled(
                    '\0',
                    KeyCode.Escape,
                    EventModifiers.None);
                try
                {
                    onKeyDown.Invoke(window, new object[] { escape });
                }
                finally
                {
                    escape.Dispose();
                }

                Assert.AreEqual(expectedCancellation, cancellation.IsCancellationRequested);
                Assert.IsTrue((bool)busyField.GetValue(window),
                    "Escape 只请求取消，异步操作结束前 Busy 状态必须保持。");
                if (!allowOperationCancellation)
                {
                    Label busyLabel = busyLabelField.GetValue(window) as Label;
                    Assert.IsNotNull(busyLabel);
                    StringAssert.Contains("不可取消", busyLabel.text);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                ESProgressCenter.DismissCompleted();
            }
        }

        [Test]
        public void AdvancedDialogBusyVisualStateSurvivesCreateGUIRebuild()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.busy-rebuild." + Guid.NewGuid().ToString("N"),
                title = "Busy 重建测试",
                allowMainWorkspaceFallback = true,
                allowOperationCancellation = true,
                animateOpening = false,
            };
            ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, null);
            Type windowType = typeof(ESAdvancedDialogWindow);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo beginBusy = windowType.GetMethod("BeginBusy", flags);
            MethodInfo cancelBusy = windowType.GetMethod("CancelBusyOperation", flags);
            FieldInfo busyField = windowType.GetField("busy", flags);
            FieldInfo busyOverlayField = windowType.GetField("busyOverlay", flags);
            FieldInfo busyLabelField = windowType.GetField("busyLabel", flags);
            FieldInfo cancelButtonField = windowType.GetField("cancelBusyButton", flags);
            FieldInfo decisionActionsField = windowType.GetField("decisionActions", flags);
            FieldInfo auxiliaryActionsField = windowType.GetField("auxiliaryActions", flags);
            FieldInfo refreshScheduleField = windowType.GetField("busyRefreshSchedule", flags);
            FieldInfo cancellationField = windowType.GetField("operationCancellation", flags);
            FieldInfo activeProgressField = windowType.GetField("activeProgress", flags);
            Assert.IsNotNull(beginBusy);
            Assert.IsNotNull(cancelBusy);
            Assert.IsNotNull(busyField);
            Assert.IsNotNull(busyOverlayField);
            Assert.IsNotNull(busyLabelField);
            Assert.IsNotNull(cancelButtonField);
            Assert.IsNotNull(decisionActionsField);
            Assert.IsNotNull(auxiliaryActionsField);
            Assert.IsNotNull(refreshScheduleField);
            Assert.IsNotNull(cancellationField);
            Assert.IsNotNull(activeProgressField);

            try
            {
                window.CreateGUI();
                beginBusy.Invoke(window, new object[] { "正在执行重建测试", "busy-rebuild" });
                object cancellationBeforeRebuild = cancellationField.GetValue(window);
                object progressBeforeRebuild = activeProgressField.GetValue(window);
                object scheduleBeforeRebuild = refreshScheduleField.GetValue(window);
                Assert.IsNotNull(cancellationBeforeRebuild);
                Assert.IsNotNull(progressBeforeRebuild);
                Assert.IsNotNull(scheduleBeforeRebuild);
                window.CreateGUI();

                Assert.IsTrue((bool)busyField.GetValue(window));
                Assert.AreEqual(
                    DisplayStyle.Flex,
                    ((VisualElement)busyOverlayField.GetValue(window)).style.display.value);
                Assert.AreEqual(
                    "正在执行重建测试",
                    ((Label)busyLabelField.GetValue(window)).text);
                Assert.IsFalse(
                    ((VisualElement)decisionActionsField.GetValue(window)).enabledSelf);
                Assert.IsFalse(
                    ((VisualElement)auxiliaryActionsField.GetValue(window)).enabledSelf);
                Assert.IsTrue(((Button)cancelButtonField.GetValue(window)).enabledSelf);
                Assert.IsNotNull(refreshScheduleField.GetValue(window),
                    "VisualTree 重建后必须重新安装 Busy 刷新调度。");
                Assert.AreSame(
                    cancellationBeforeRebuild,
                    cancellationField.GetValue(window),
                    "VisualTree 重建不得替换当前异步操作的 CancellationTokenSource。");
                Assert.AreSame(
                    progressBeforeRebuild,
                    activeProgressField.GetValue(window),
                    "VisualTree 重建不得替换当前操作对应的 Progress 状态。");
                Assert.AreNotSame(
                    scheduleBeforeRebuild,
                    refreshScheduleField.GetValue(window),
                    "VisualTree 重建必须废弃旧 panel 的 schedule 并创建新调度。");

                cancelBusy.Invoke(window, null);
                Assert.IsTrue(
                    ((CancellationTokenSource)cancellationField.GetValue(window))
                    .IsCancellationRequested);
                object cancelledScheduleBeforeRebuild = refreshScheduleField.GetValue(window);
                window.CreateGUI();

                Assert.AreEqual(
                    "正在取消",
                    ((Label)busyLabelField.GetValue(window)).text);
                Assert.IsFalse(((Button)cancelButtonField.GetValue(window)).enabledSelf,
                    "取消请求跨 VisualTree 重建后不得重新启用取消按钮。");
                Assert.IsNotNull(refreshScheduleField.GetValue(window));
                Assert.AreSame(cancellationBeforeRebuild, cancellationField.GetValue(window));
                Assert.AreSame(progressBeforeRebuild, activeProgressField.GetValue(window));
                Assert.AreNotSame(
                    cancelledScheduleBeforeRebuild,
                    refreshScheduleField.GetValue(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                ESProgressCenter.DismissCompleted();
            }
        }

        [Test]
        public void AdvancedDialogOwnerOffsetPreservesNegativeDisplayCoordinates()
        {
            Rect owner = new Rect(-1500f, 80f, 1200f, 800f);
            Rect centered = ESAdvancedDialogWindow.CalculateCenteredPosition(
                owner,
                new Vector2(460f, 260f),
                new Vector2(560f, 440f),
                440f);
            Rect child = ESDialogService.OffsetChildDialog(centered, 2);

            Assert.Less(centered.x, 0f);
            Assert.AreEqual(centered.x + 36f, child.x, 0.001f);
            Assert.AreEqual(centered.y + 36f, child.y, 0.001f);
        }

        [Test]
        public void AdvancedDialogPositionRepairHasNativeAttachTailAndRejectsTransientOwners()
        {
            string source = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                    "EditorTools", "ESAdvancedDialog", "ESAdvancedDialog.cs"),
                Encoding.UTF8);
            StringAssert.Contains(
                "ReapplyInitialPositionOnDelayCall",
                source,
                "ShowUtility 后必须保留一个有界 delayCall 尾部，覆盖 native host 的最后一次几何写回。");
            StringAssert.Contains(
                "return IsLive(request?.owner) ? request.owner : null;",
                source,
                "对话框定位只能使用显式 owner；缺失 owner 时必须回退到主编辑器工作区而不是猜测窗口。");
            StringAssert.DoesNotContain(
                "ResolveImplicitOwner",
                File.ReadAllText(
                    Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                        "EditorTools", "ESAdvancedDialog", "ESEditorDialogPresenter.cs"),
                    Encoding.UTF8),
                "Editor presenter 不得根据焦点或鼠标窗口猜测 owner。");
            StringAssert.Contains(
                "EditorApplication.delayCall -= ReapplyInitialPositionOnDelayCall",
                source,
                "关闭和异常路径必须解绑定位尾部，避免静态回调残留。");
        }

        [Test]
        public void AdvancedDialogUsesOwnerAsAnchorAndWorkAreaForAvailableSize()
        {
            Rect workArea = new Rect(100f, 50f, 1200f, 800f);
            Rect narrowOwner = new Rect(560f, 100f, 280f, 700f);
            Rect dialog = ESAdvancedDialogWindow.CalculatePosition(
                narrowOwner,
                workArea,
                new Vector2(460f, 260f),
                new Vector2(600f, 520f),
                520f,
                ESAdvancedDialogPositionMode.CenterOwner,
                Vector2.zero,
                Vector2.zero);

            Assert.AreEqual(600f, dialog.width, 0.001f,
                "窄 Inspector 只能决定居中锚点，不能把对话框压缩成侧栏宽度。");
            Assert.AreEqual(narrowOwner.center.x, dialog.center.x, 0.001f);
            Assert.AreEqual(narrowOwner.center.y, dialog.center.y, 0.001f);
            Assert.GreaterOrEqual(dialog.xMin, workArea.xMin + 12f);
            Assert.LessOrEqual(dialog.xMax, workArea.xMax - 12f);
        }

        [Test]
        public void AdvancedDialogCornerModesMatchScreenSpaceNames()
        {
            Rect workArea = new Rect(0f, 0f, 1600f, 1000f);
            Rect owner = new Rect(420f, 280f, 520f, 420f);
            Vector2 minimum = new Vector2(360f, 260f);
            Vector2 preferred = new Vector2(400f, 300f);

            Rect topLeft = ESAdvancedDialogWindow.CalculatePosition(
                owner, workArea, minimum, preferred, 300f,
                ESAdvancedDialogPositionMode.OwnerTopLeft, Vector2.zero, Vector2.zero);
            Rect topRight = ESAdvancedDialogWindow.CalculatePosition(
                owner, workArea, minimum, preferred, 300f,
                ESAdvancedDialogPositionMode.OwnerTopRight, Vector2.zero, Vector2.zero);
            Rect bottomLeft = ESAdvancedDialogWindow.CalculatePosition(
                owner, workArea, minimum, preferred, 300f,
                ESAdvancedDialogPositionMode.OwnerBottomLeft, Vector2.zero, Vector2.zero);
            Rect bottomRight = ESAdvancedDialogWindow.CalculatePosition(
                owner, workArea, minimum, preferred, 300f,
                ESAdvancedDialogPositionMode.OwnerBottomRight, Vector2.zero, Vector2.zero);
            Rect custom = ESAdvancedDialogWindow.CalculatePosition(
                owner, workArea, minimum, preferred, 300f,
                ESAdvancedDialogPositionMode.CustomScreenPosition,
                new Vector2(700f, 460f), Vector2.zero);

            Assert.AreEqual(owner.xMin, topLeft.xMin, 0.001f);
            Assert.AreEqual(owner.yMin, topLeft.yMin, 0.001f);
            Assert.AreEqual(owner.xMax, topRight.xMax, 0.001f);
            Assert.AreEqual(owner.yMin, topRight.yMin, 0.001f);
            Assert.AreEqual(owner.xMin, bottomLeft.xMin, 0.001f);
            Assert.AreEqual(owner.yMax, bottomLeft.yMax, 0.001f);
            Assert.AreEqual(owner.xMax, bottomRight.xMax, 0.001f);
            Assert.AreEqual(owner.yMax, bottomRight.yMax, 0.001f);
            Assert.AreEqual(700f, custom.xMin, 0.001f);
            Assert.AreEqual(460f, custom.yMin, 0.001f);
        }

        [Test]
        public void AdvancedDialogBuildsToneIdentityAndRoundedFieldSurfaces()
        {
            string dialogSource = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                    "ESAdvancedDialog", "ESAdvancedDialog.cs"),
                Encoding.UTF8);
            StringAssert.DoesNotContain("d_P4_DeletedLocal", dialogSource,
                "关闭动作不能使用与删除状态无关的 Perforce 图标。");
            StringAssert.Contains("GeometryChangedEvent", dialogSource,
                "对话框必须在原生 Utility 挂载后的首个布局事件重新应用屏幕位置。");
            StringAssert.Contains("ShowModalUtility();", dialogSource);
            StringAssert.Contains("ReapplyInitialPosition(false);", dialogSource,
                "Modal 显示后不能只依赖默认左上角位置。");
            StringAssert.Contains("EditorApplication.update += ReapplyInitialPositionOnEditorUpdate", dialogSource,
                "Modal 首开定位必须在原生窗口挂载后的短时间内重复重放，而不能只依赖一次 delayCall。");
            StringAssert.Contains("rootVisualElement.schedule", dialogSource,
                "Modal 原生嵌套循环可能暂缓 Editor.update，必须保留窗口本地的 UI Toolkit 定位重放路径。");
            StringAssert.Contains("ReapplyInitialPositionOnScheduledLayout", dialogSource,
                "UI Toolkit 定位重放必须使用独立回调，避免把全局 Editor.update 当成唯一时序保证。");
            StringAssert.Contains("StopInitialPositionReapplyLoop();", dialogSource,
                "定位重放循环必须在关闭路径确定性解绑。");
            StringAssert.Contains("InitialPositionReapplyMaxPasses", dialogSource,
                "定位重放必须有明确上限，不能留下常驻 Editor update 回调。");
            StringAssert.Contains("ESDialogIdentityStrip", dialogSource,
                "对话框必须显示 ES 专用身份条，避免与普通 Unity 窗口混淆。");
            StringAssert.Contains("ESDialogIdentityBadge", dialogSource,
                "对话框身份条必须包含稳定的 ES 标识。");
            StringAssert.Contains("ESDialogModeBadge", dialogSource,
                "对话框身份条必须明确模态/非模态模式。");
            StringAssert.Contains("ESDialogPolicyBadge", dialogSource,
                "对话框首屏必须明确仅收集输入/确认，不直接授予业务写入权限。");
            StringAssert.Contains("ESDialogStableId", dialogSource,
                "对话框首屏必须暴露稳定身份，避免多个普通窗口难以区分。");
            StringAssert.Contains("BuildNativeTitle", dialogSource,
                "原生标题必须通过统一的 ES 标题构造路径生成。");
            StringAssert.Contains("模态", dialogSource,
                "原生标题必须包含模态语义，不能只依赖窗口内容区。");
            StringAssert.Contains("ShowModal 不允许并行实例", dialogSource,
                "同步模态入口不得绕过并发与生命周期合同。");
            string menuTreeSource = File.ReadAllText(
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor",
                    "ESMenuTreeWindow", "-Templates", "-ESMenuTreeWindow.cs"),
                Encoding.UTF8);
            StringAssert.Contains("ResolveDefaultWindowIcon(", menuTreeSource,
                "ESMenuTree 主窗口标题栏必须复用统一语义图标解析，而不是只在页面列表显示图标。");
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.visual-contract",
                title = "视觉合同测试",
                message = "确认对话框视觉层级。",
                tone = ESDialogTone.Warning,
                animateOpening = false,
                allowMainWorkspaceFallback = true,
            };
            request.AddText("name", "名称", "测试");
            ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, null);
            try
                {
                window.CreateGUI();
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogIdentityStrip"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogBrandMark"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogLayerLabel"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogToneIconSurface"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogPolicyBadge"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESDialogStableId"));
                Assert.IsNotNull(window.rootVisualElement.Q("ESWindowTitleIcon"),
                    "对话框标题栏必须有稳定的语义图标，不应只有无设计的文字壳。");
                VisualElement shell = window.rootVisualElement.Q("ESWindowShell");
                Assert.IsNotNull(shell);
                Assert.Greater(shell.style.borderTopLeftRadius.value.value, 0f,
                    "对话框外壳必须有统一圆角，而不是只给内部字段加圆角。");
                VisualElement field = window.rootVisualElement.Q("ESDialogField-name");
                Assert.IsNotNull(field);
                Assert.Greater(field.style.borderTopLeftRadius.value.value, 0f);
                VisualElement footer = window.rootVisualElement.Q("ESDialogFooter");
                Assert.IsNotNull(footer);
                Assert.Greater(footer.style.borderTopLeftRadius.value.value, 0f);
                Assert.AreEqual(0f, footer.style.borderBottomLeftRadius.value.value, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ProgressCenterSupportsBoundedDetailsCancellationAndIdempotentFinish()
        {
            string id = "tests.progress." + Guid.NewGuid().ToString("N");
            ESProgressHandle handle = ESProgressCenter.Begin(id, "测试进度", cancellable: true);
            try
            {
                for (int i = 0; i < 100; i++)
                    handle.AddDetail("detail-" + i);
                handle.Report(0.5f, "执行到一半");
                Assert.IsTrue(ESProgressCenter.RequestCancel(id));
                Assert.IsTrue(handle.IsCancellationRequested);
                handle.Cancel();
                handle.Complete("不能覆盖取消状态");

                ESProgressSnapshot snapshot = ESProgressCenter.GetSnapshot()
                    .Single(item => item.id == id);
                Assert.AreEqual(ESProgressState.Cancelled, snapshot.state);
                Assert.AreEqual(80, snapshot.details.Count);
                Assert.AreEqual("detail-20", snapshot.details[0]);
            }
            finally
            {
                handle.Dispose();
                ESProgressCenter.DismissCompleted();
            }
        }

        [Test]
        public void ModalDialogRejectsAsyncContractsBeforeOpeningWindow()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.async-modal-rejection",
                title = "异步模态拒绝测试",
                allowMainWorkspaceFallback = true,
                validateAsync = (_, token) => Task.FromResult<ESAdvancedDialogValidation>(null),
            };
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESDialogService.ShowModal(request));
            StringAssert.Contains("ShowAsync", exception.Message);
        }

        [Test]
        public void DialogMigrationFacadeRequiresStableIdsAndUsesTypedChoiceResult()
        {
            Assert.AreEqual(typeof(Task<bool>), typeof(ESDialog).GetMethod(
                "ConfirmAsync",
                BindingFlags.Static | BindingFlags.Public)?.ReturnType);
            Assert.AreEqual(typeof(bool), typeof(ESDialog).GetMethod(
                "ConfirmModal",
                BindingFlags.Static | BindingFlags.Public)?.ReturnType);
            Assert.AreEqual(typeof(Task<ESDialogChoice>), typeof(ESDialog).GetMethod(
                "ChooseAsync",
                BindingFlags.Static | BindingFlags.Public)?.ReturnType);
            Assert.AreEqual(typeof(ESDialogChoice), typeof(ESDialog).GetMethod(
                "ChooseModal",
                BindingFlags.Static | BindingFlags.Public)?.ReturnType);

            TargetInvocationException wrapped = Assert.Throws<TargetInvocationException>(() =>
                typeof(ESDialog).GetMethod("ConfirmModal", BindingFlags.Static | BindingFlags.Public)
                    ?.Invoke(null, new object[]
                    {
                        string.Empty,
                        "标题",
                        "消息",
                        "确定",
                        "取消",
                        string.Empty,
                        ESDialogTone.Info,
                        ESDialogHost.Auto,
                    }));
            Assert.IsInstanceOf<ArgumentException>(wrapped.InnerException);
            StringAssert.Contains("dialogId", wrapped.InnerException.Message);
        }

        [Test]
        public void DialogRequestSnapshotOwnsMutableFieldAndActionCollections()
        {
            var source = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.snapshot",
                title = "快照测试",
            };
            ESAdvancedDialogField field = source.AddMultiChoice(
                "options",
                "选项",
                new[] { "A", "B" },
                new[] { "A" });
            ESAdvancedDialogAction action = source.AddAuxiliaryAction(
                "preview",
                "预览",
                _ => { });

            ESAdvancedDialogRequest snapshot = ESDialogService.SnapshotRequest(source);
            field.choices[0] = "已修改";
            field.selectedChoiceValues.Clear();
            action.text = "已修改";
            source.fields.Clear();
            source.auxiliaryActions.Clear();

            Assert.AreEqual(1, snapshot.fields.Count);
            Assert.AreEqual("A", snapshot.fields[0].choices[0]);
            CollectionAssert.AreEqual(
                new[] { "A" },
                snapshot.fields[0].selectedChoiceValues);
            Assert.AreEqual("预览", snapshot.auxiliaryActions[0].text);
            Assert.AreNotSame(field, snapshot.fields[0]);
            Assert.AreNotSame(action, snapshot.auxiliaryActions[0]);
        }

        [Test]
        public void DialogOperationCompletesCallbackAndEveryTaskExactlyOnce()
        {
            var operation = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.operation.complete-once",
                title = "Operation complete once",
                allowMainWorkspaceFallback = true,
            });
            var primary = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var observer = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int callbackCount = 0;
            bool callbackSawDetachedSubscribers = false;
            operation.AddSubscriber(
                result =>
                {
                    callbackCount++;
                    callbackSawDetachedSubscribers = operation.subscribers.Count == 0;
                },
                primary);
            operation.AddSubscriber(null, observer);
            var firstResult = new ESAdvancedDialogResult { accepted = true };

            Assert.IsTrue(operation.CompleteOnce(firstResult));
            Assert.IsFalse(operation.CompleteOnce(
                new ESAdvancedDialogResult { cancelled = true }));

            Assert.AreEqual(1, callbackCount);
            Assert.IsTrue(callbackSawDetachedSubscribers,
                "发布用户回调前必须先移出订阅集合，避免回调重入漏发后续 Task。");
            Assert.AreSame(firstResult, primary.Task.Result);
            Assert.AreSame(firstResult, observer.Task.Result);
            Assert.AreEqual(ESDialogService.DialogOperationState.Completed, operation.state);
        }

        [Test]
        public void DialogOperationFirstTerminalResultWinsAcrossClosingAndCompletion()
        {
            var operation = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.operation.first-terminal",
                title = "First terminal result",
                allowMainWorkspaceFallback = true,
            });
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.AddSubscriber(null, completion);
            var accepted = new ESAdvancedDialogResult { accepted = true };
            var cancelled = new ESAdvancedDialogResult { cancelled = true };
            var failed = new ESAdvancedDialogResult
            {
                exception = new InvalidOperationException("late failure")
            };

            Assert.AreSame(accepted, operation.CaptureTerminalResult(accepted));
            Assert.IsTrue(operation.BeginClosing(cancelled));
            Assert.AreSame(accepted, operation.terminalResult);
            Assert.IsTrue(operation.CompleteOnce(failed));

            Assert.AreSame(accepted, completion.Task.Result,
                "确认、取消、关闭异常竞争时，只有首个终态可以发布。");
            Assert.IsFalse(operation.CompleteOnce(cancelled));
            Assert.AreSame(accepted, operation.terminalResult);
        }

        [Test]
        public void DialogDeadWindowCompletesOperationWhenPendingQueueIsEmpty()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            List<ESDialogService.DialogOperation> activeOperations =
                GetDialogOperationList("activeOperations");
            List<ESDialogService.DialogOperation> pendingDialogs =
                GetDialogOperationList("pendingDialogs");
            Assert.IsEmpty(pendingDialogs,
                "该测试要求空队列，以锁定无 pending 时 dead operation 仍被 monitor 收敛。");
            var operation = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.dead-window." + Guid.NewGuid().ToString("N"),
                title = "Dead window",
                allowMainWorkspaceFallback = true,
            });
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.AddSubscriber(null, completion);
            ESAdvancedDialogWindow deadWindow = ESAdvancedDialogWindow.Create(
                operation.request,
                null);
            UnityEngine.Object.DestroyImmediate(deadWindow);
            operation.window = deadWindow;
            operation.state = ESDialogService.DialogOperationState.Active;
            activeOperations.Add(operation);
            try
            {
                GetDialogServiceMethod("MonitorOwnerLifetime").Invoke(null, null);
                Assert.IsTrue(completion.Task.IsCompleted);
                Assert.IsTrue(completion.Task.Result.cancelled);
                Assert.AreEqual(
                    ESDialogService.DialogOperationState.Completed,
                    operation.state);
                CollectionAssert.DoesNotContain(activeOperations, operation);
            }
            finally
            {
                activeOperations.Remove(operation);
                GetDialogServiceMethod("UpdateOwnerLifetimeMonitor").Invoke(null, null);
            }
        }

        [Test]
        public void DialogOpenNowStopsWhenCreateReentrantlyCompletesOperation()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            ESDialogService.DialogOperation operation = null;
            var accepted = new ESAdvancedDialogResult { accepted = true };
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.open-reentrant." + Guid.NewGuid().ToString("N"),
                title = "Open reentrant completion",
                allowMainWorkspaceFallback = true,
                validate = _ =>
                {
                    operation.CompleteOnce(accepted);
                    return string.Empty;
                },
            };
            operation = new ESDialogService.DialogOperation(request);
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.AddSubscriber(null, completion);
            int activeBefore = ESDialogService.ActiveCount;

            object opened = GetDialogServiceMethod("OpenNow").Invoke(
                null,
                new object[] { operation, false });

            Assert.IsNull(opened);
            Assert.AreSame(accepted, completion.Task.Result);
            Assert.AreEqual(activeBefore, ESDialogService.ActiveCount,
                "Create 回调重入完成后不得继续注册或打开窗口。");
        }

        [Test]
        public void DialogBuildFailureRemainsTerminalWhenShutdownCancelsActiveOperations()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            List<ESDialogService.DialogOperation> activeOperations =
                GetDialogOperationList("activeOperations");
            List<ESAdvancedDialogWindow> activeWindows = GetDialogWindowList("activeWindows");
            var buildFailure = new InvalidOperationException("dialog build failure probe");
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.build-failure-shutdown."
                    + Guid.NewGuid().ToString("N"),
                title = "Build failure shutdown",
                allowMainWorkspaceFallback = true,
                createCustomContent = _ => throw buildFailure,
            };
            var operation = new ESDialogService.DialogOperation(request);
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.AddSubscriber(null, completion);
            ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, null);
            operation.window = window;
            operation.state = ESDialogService.DialogOperationState.Active;
            activeOperations.Add(operation);
            activeWindows.Add(window);
            try
            {
                LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex(
                        "dialog build failure probe"));
                window.CreateGUI();

                ESDialogService.Shutdown();

                Assert.IsTrue(completion.Task.IsCompleted);
                Assert.AreSame(buildFailure, completion.Task.Result.exception,
                    "构建 failure 已经成为首个终态，Shutdown 取消不得覆盖它。");
                Assert.IsFalse(completion.Task.Result.cancelled);
                Assert.AreEqual(
                    ESDialogService.DialogOperationState.Completed,
                    operation.state);
                CollectionAssert.DoesNotContain(activeOperations, operation);
                CollectionAssert.DoesNotContain(activeWindows, window);
            }
            finally
            {
                activeOperations.Remove(operation);
                activeWindows.Remove(window);
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                ESDialogService.RestartAfterPresenterRegistration();
            }
        }

        [Test]
        public void DialogParentCloseCancelsActiveAndQueuedChildrenBeforeCallbacksCanReopen()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            List<ESDialogService.DialogOperation> activeOperations =
                GetDialogOperationList("activeOperations");
            List<ESDialogService.DialogOperation> pendingDialogs =
                GetDialogOperationList("pendingDialogs");
            List<ESAdvancedDialogWindow> activeWindows = GetDialogWindowList("activeWindows");
            Assert.IsEmpty(activeOperations);
            Assert.IsEmpty(pendingDialogs);
            Assert.IsEmpty(activeWindows);

            var parentRequest = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.parent-close." + Guid.NewGuid().ToString("N"),
                title = "Parent close",
                allowMainWorkspaceFallback = true,
            };
            ESAdvancedDialogWindow parentWindow = ESAdvancedDialogWindow.Create(
                parentRequest,
                null);
            var parent = new ESDialogService.DialogOperation(parentRequest)
            {
                window = parentWindow,
                state = ESDialogService.DialogOperationState.Active,
            };
            parent.AddSubscriber(null, null);

            var activeChildRequest = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.active-child." + Guid.NewGuid().ToString("N"),
                title = "Active child",
                owner = parentWindow,
                allowMainWorkspaceFallback = false,
            };
            ESAdvancedDialogWindow activeChildWindow = ESAdvancedDialogWindow.Create(
                activeChildRequest,
                null);
            var activeChild = new ESDialogService.DialogOperation(activeChildRequest)
            {
                window = activeChildWindow,
                state = ESDialogService.DialogOperationState.Active,
            };
            ESAdvancedDialogResult callbackReopenResult = null;
            ESAdvancedDialogWindow callbackReopenedWindow = null;
            activeChild.AddSubscriber(
                _ =>
                {
                    callbackReopenedWindow = ESDialogService.Show(new ESAdvancedDialogRequest
                    {
                        dialogId = "tests.dialog.callback-child."
                            + Guid.NewGuid().ToString("N"),
                        title = "Callback child",
                        owner = parentWindow,
                        allowMainWorkspaceFallback = false,
                        completed = result => callbackReopenResult = result,
                    });
                },
                null);

            var queuedChild = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.queued-child." + Guid.NewGuid().ToString("N"),
                title = "Queued child",
                owner = parentWindow,
                allowMainWorkspaceFallback = false,
            });
            var queuedCompletion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            queuedChild.AddSubscriber(null, queuedCompletion);
            queuedChild.state = ESDialogService.DialogOperationState.Queued;

            activeOperations.Add(parent);
            activeOperations.Add(activeChild);
            activeWindows.Add(parentWindow);
            activeWindows.Add(activeChildWindow);
            pendingDialogs.Add(queuedChild);
            try
            {
                var parentResult = new ESAdvancedDialogResult { cancelled = true };
                ESDialogService.NotifyClosed(parentWindow, parentResult);

                Assert.AreEqual(ESDialogService.DialogOperationState.Completed, activeChild.state);
                Assert.AreEqual(ESDialogService.DialogOperationState.Completed, queuedChild.state);
                Assert.IsTrue(queuedCompletion.Task.Result.cancelled);
                Assert.IsNull(callbackReopenedWindow,
                    "父关闭期间的子回调不得重新打开受该父窗口拥有的对话框。");
                Assert.IsNotNull(callbackReopenResult);
                Assert.IsTrue(callbackReopenResult.cancelled);
                Assert.AreEqual(
                    ESDialogService.DialogOperationState.Completed,
                    parent.state);
            }
            finally
            {
                GetDialogServiceMethod("DetachOperation").Invoke(null, new object[] { parent });
                GetDialogServiceMethod("DetachOperation").Invoke(null, new object[] { activeChild });
                GetDialogServiceMethod("DetachOperation").Invoke(null, new object[] { queuedChild });
                activeWindows.Remove(parentWindow);
                activeWindows.Remove(activeChildWindow);
                if (activeChildWindow != null)
                    UnityEngine.Object.DestroyImmediate(activeChildWindow);
                if (parentWindow != null)
                    UnityEngine.Object.DestroyImmediate(parentWindow);
                ESDialogService.RestartAfterPresenterRegistration();
            }
        }

        [Test]
        public void DialogFocusExistingQueuesBehindClosingDuplicate()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            List<ESDialogService.DialogOperation> activeOperations =
                GetDialogOperationList("activeOperations");
            List<ESDialogService.DialogOperation> pendingDialogs =
                GetDialogOperationList("pendingDialogs");
            List<ESAdvancedDialogWindow> activeWindows = GetDialogWindowList("activeWindows");
            string id = "tests.dialog.closing-focus." + Guid.NewGuid().ToString("N");
            var closing = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = id,
                title = "Closing duplicate",
                allowMainWorkspaceFallback = true,
            });
            ESAdvancedDialogWindow closingWindow = ESAdvancedDialogWindow.Create(
                closing.request,
                null);
            closing.window = closingWindow;
            closing.state = ESDialogService.DialogOperationState.Closing;
            activeOperations.Add(closing);
            activeWindows.Add(closingWindow);
            var incoming = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = id,
                title = "Incoming focus",
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting,
            });
            incoming.AddSubscriber(null, null);
            try
            {
                object[] arguments = { incoming, null };
                object opened = GetDialogServiceMethod("SubmitOperation").Invoke(null, arguments);

                Assert.IsNull(opened);
                Assert.AreSame(incoming, arguments[1]);
                CollectionAssert.Contains(pendingDialogs, incoming);
                Assert.AreEqual(ESDialogService.DialogOperationState.Queued, incoming.state);
                Assert.AreEqual(ESDialogService.DialogOperationState.Closing, closing.state,
                    "FocusExisting 不能把 subscriber 转交给即将结束的 operation。");
            }
            finally
            {
                GetDialogServiceMethod("DetachOperation").Invoke(null, new object[] { incoming });
                activeOperations.Remove(closing);
                activeWindows.Remove(closingWindow);
                UnityEngine.Object.DestroyImmediate(closingWindow);
                GetDialogServiceMethod("UpdateOwnerLifetimeMonitor").Invoke(null, null);
            }
        }

        [Test]
        public void DialogReplaceExistingRemovesEveryQueuedDuplicate()
        {
            ESDialogService.Shutdown();
            ESDialogService.RestartAfterPresenterRegistration();
            List<ESDialogService.DialogOperation> pendingDialogs =
                GetDialogOperationList("pendingDialogs");
            string id = "tests.dialog.replace-queued." + Guid.NewGuid().ToString("N");
            ESDialogService.DialogOperation first = CreateQueuedDialogOperation(id);
            ESDialogService.DialogOperation second = CreateQueuedDialogOperation(id);
            var replacement = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = id,
                title = "Replacement",
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.ReplaceExisting,
            });
            pendingDialogs.Add(first);
            pendingDialogs.Add(second);
            try
            {
                object[] arguments = { replacement, null };
                GetDialogServiceMethod("SubmitOperation").Invoke(null, arguments);

                Assert.AreEqual(ESDialogService.DialogOperationState.Completed, first.state);
                Assert.AreEqual(ESDialogService.DialogOperationState.Completed, second.state);
                Assert.IsTrue(first.terminalResult.cancelled);
                Assert.IsTrue(second.terminalResult.cancelled);
                Assert.AreEqual(
                    1,
                    pendingDialogs.Count(item => string.Equals(
                        item?.request?.dialogId,
                        id,
                        StringComparison.Ordinal)),
                    "ReplaceExisting 必须一次清理所有已排队重复项。");
                CollectionAssert.Contains(pendingDialogs, replacement);
            }
            finally
            {
                GetDialogServiceMethod("DetachOperation").Invoke(
                    null,
                    new object[] { replacement });
                pendingDialogs.Remove(first);
                pendingDialogs.Remove(second);
                GetDialogServiceMethod("UpdateOwnerLifetimeMonitor").Invoke(null, null);
            }
        }

        private static ESDialogService.DialogOperation CreateQueuedDialogOperation(string id)
        {
            var operation = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = id,
                title = "Queued duplicate",
                allowMainWorkspaceFallback = true,
            });
            operation.AddSubscriber(null, null);
            operation.state = ESDialogService.DialogOperationState.Queued;
            return operation;
        }

        [Test]
        public void DialogOperationTransfersFocusedSubscribersToOneCompletionChannel()
        {
            var source = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.operation.focus-source",
                title = "Focus source",
                allowMainWorkspaceFallback = true,
            });
            var target = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.operation.focus-target",
                title = "Focus target",
                allowMainWorkspaceFallback = true,
            });
            var sourceTask = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var targetTask = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            source.AddSubscriber(null, sourceTask);
            target.AddSubscriber(null, targetTask);

            source.TransferSubscribersTo(target);
            var sharedResult = new ESAdvancedDialogResult { accepted = true };

            Assert.AreEqual(ESDialogService.DialogOperationState.Completed, source.state);
            Assert.AreEqual(0, source.subscribers.Count);
            Assert.AreEqual(2, target.subscribers.Count);
            Assert.IsFalse(source.CompleteOnce(
                new ESAdvancedDialogResult { cancelled = true }));
            Assert.IsTrue(target.CompleteOnce(sharedResult));
            Assert.AreSame(sharedResult, sourceTask.Task.Result);
            Assert.AreSame(sharedResult, targetTask.Task.Result);
        }

        [Test]
        public void AdvancedDialogCompatibilityOpenersAreObsoleteAndHidden()
        {
            string[] methodNames = { "Show", "ShowAsync", "ShowModal" };
            for (int i = 0; i < methodNames.Length; i++)
            {
                MethodInfo method = typeof(ESAdvancedDialogWindow).GetMethod(
                    methodNames[i],
                    BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(method, methodNames[i]);
                Assert.IsNotNull(
                    method.GetCustomAttribute<ObsoleteAttribute>(),
                    methodNames[i] + " 只允许作为旧源码兼容转发，生产入口必须使用 ESDialogService。");
                var editorBrowsable = method.GetCustomAttribute<
                    System.ComponentModel.EditorBrowsableAttribute>();
                Assert.IsNotNull(editorBrowsable, methodNames[i]);
                Assert.AreEqual(
                    System.ComponentModel.EditorBrowsableState.Never,
                    editorBrowsable.State,
                    methodNames[i]);
            }
        }

        [Test]
        public void DialogSubscriberCancellationPublishesOnceAndCancelsOnlyItsTask()
        {
            var operation = new ESDialogService.DialogOperation(new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.operation.cancellation",
                title = "Operation cancellation",
                allowMainWorkspaceFallback = true,
            });
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int callbackCount = 0;
            ESAdvancedDialogResult callbackResult = null;
            ESDialogService.DialogSubscriber subscriber = operation.AddSubscriber(
                result =>
                {
                    callbackCount++;
                    callbackResult = result;
                },
                completion);
            var context = new QueuedSynchronizationContext();
            using (var cancellation = new CancellationTokenSource())
            {
                subscriber.RegisterCancellation(operation, cancellation.Token, context);
                cancellation.Cancel();
                Assert.IsFalse(completion.Task.IsCompleted,
                    "token 回调只能投递到 Editor 上下文，不能跨线程直接修改窗口治理状态。");
                context.RunPostedCallback();
            }

            Assert.AreEqual(1, callbackCount);
            Assert.IsNotNull(callbackResult);
            Assert.IsTrue(callbackResult.cancelled);
            Assert.IsTrue(completion.Task.IsCanceled);
            Assert.IsTrue(subscriber.IsCompleted);
            Assert.AreEqual(ESDialogService.DialogOperationState.Completed, operation.state);
            Assert.IsFalse(operation.CompleteOnce(new ESAdvancedDialogResult { accepted = true }));
            Assert.AreEqual(1, callbackCount);
        }

        [Test]
        public void AdvancedDialogRebuildReleasesEachCustomContentGenerationOnce()
        {
            int created = 0;
            int released = 0;
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.custom-content.rebuild",
                title = "Custom content rebuild",
                allowMainWorkspaceFallback = true,
                createCustomContent = _ => new Label("generation-" + ++created),
                releaseCustomContent = _ => released++,
            };
            ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, null);
            try
            {
                window.CreateGUI();
                VisualElement first = window.rootVisualElement.Q("ESDialogCustomContent");
                window.CreateGUI();
                VisualElement second = window.rootVisualElement.Q("ESDialogCustomContent");

                Assert.AreEqual(2, created);
                Assert.AreEqual(1, released,
                    "重建前必须释放上一代 custom content，且不能提前释放当前代。");
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                Assert.AreNotSame(first, second);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
            Assert.AreEqual(2, released,
                "窗口销毁必须且只能释放最后一代 custom content。");
        }

        [Test]
        public void ModalDialogRejectsQueuePolicyBeforeOpeningWindow()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.queue-modal-rejection",
                title = "队列模态拒绝测试",
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.Queue,
            };
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESDialogService.ShowModal(request));
            StringAssert.Contains("ShowAsync", exception.Message);
        }

        [Test]
        public void ModalDialogRejectsParallelPolicyBeforeOpeningWindow()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.parallel-modal-rejection",
                title = "并行模态拒绝测试",
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.AllowParallel,
            };
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESDialogService.ShowModal(request));
            StringAssert.Contains("不允许并行实例", exception.Message);
        }

        [Test]
        public void MenuTreeToolbarContractSeparatesSystemGlobalWindowAndPageResponsibilities()
        {
            RuntimeContractWindow window = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                MethodInfo createGui = typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetMethod(
                    "CreateGUI",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);
                VisualElement root = window.rootVisualElement;
                Assert.AreEqual(
                    FlexDirection.Column,
                    root.Q<VisualElement>("ESMenuTreeToolbarContract").style.flexDirection.value,
                    "普通窗口必须继续保留四行职责布局。");
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeSystemActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeSystemActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeGlobalActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeGlobalActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeWindowActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeWindowActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreePageActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreePageActions"));

                TextField search = root.Q<TextField>();
                VisualElement navigation = root.Q<VisualElement>("ESMenuTreeNavigation");
                Assert.IsNotNull(search);
                Assert.IsNotNull(navigation);
                Assert.IsTrue(
                    navigation.Query<Button>().ToList().Any(button => button.text == "声明"),
                    "页面导航应优先显示稳定短标签，而不是强制把完整路径塞入按钮。");
                Assert.AreEqual(96f, search.style.minWidth.value.value, 0.001f,
                    "搜索框必须能随窄窗口收缩，不能保留 160px 硬下限。");
                Assert.AreEqual(148f, navigation.style.minWidth.value.value, 0.001f,
                    "导航仍保留可点击下限，但不能以旧 190px 硬宽度挤压页面。");
                foreach (string hostName in new[]
                         {
                             "ESMenuTreeSystemActions",
                             "ESMenuTreeGlobalActions",
                             "ESMenuTreeWindowActions",
                             "ESMenuTreePageActions"
                         })
                {
                    VisualElement host = root.Q<VisualElement>(hostName);
                    Assert.AreEqual(Wrap.Wrap, host.style.flexWrap.value, hostName);
                    Assert.AreEqual(Overflow.Visible, host.style.overflow.value, hostName);
                }
                foreach (string rowName in new[]
                         {
                             "ESMenuTreeSystemActionRow",
                             "ESMenuTreeGlobalActionRow",
                             "ESMenuTreeWindowActionRow",
                             "ESMenuTreePageActionRow"
                         })
                {
                    VisualElement row = root.Q<VisualElement>(rowName);
                    Assert.AreEqual(Wrap.Wrap, row.style.flexWrap.value, rowName);
                    Assert.AreEqual(1f, row.style.flexShrink.value, 0.001f, rowName);
                    Assert.AreEqual(100f, row.style.width.value.value, 0.001f,
                        rowName + " 必须占满当前动作行宽度并允许按钮换行。");
                }

                Assert.IsTrue(HasHeaderActionText(
                    root.Q<VisualElement>("ESMenuTreeSystemActions"), "系统扩展"));
                Assert.IsTrue(HasHeaderActionText(
                    root.Q<VisualElement>("ESMenuTreeGlobalActions"), "全局扩展"));
                Assert.IsTrue(HasHeaderActionText(
                    root.Q<VisualElement>("ESMenuTreeWindowActions"), "窗口扩展"));

                MethodInfo refreshGlobalActions =
                    typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetMethod(
                        "UpdateGlobalActionToolbar",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(refreshGlobalActions);
                refreshGlobalActions.Invoke(window, null);
                Assert.IsTrue(HasHeaderActionText(
                        root.Q<VisualElement>("ESMenuTreeGlobalActions"), "全局扩展"),
                    "刷新框架内建全局动作时，不得清除窗口注入的全局动作。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CompactHostKeepsFourResponsibilityHostsInAdaptiveRows()
        {
            CompactRuntimeContractWindow window =
                ScriptableObject.CreateInstance<CompactRuntimeContractWindow>();
            try
            {
                MethodInfo createGui = typeof(ESMenuTreeWindow<CompactRuntimeContractWindow>)
                    .GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);

                VisualElement root = window.rootVisualElement;
                VisualElement contract = root.Q<VisualElement>("ESMenuTreeToolbarContract");
                Assert.IsNotNull(contract);
                Assert.AreEqual(FlexDirection.Row, contract.style.flexDirection.value);
                Assert.AreEqual(Wrap.Wrap, contract.style.flexWrap.value,
                    "紧凑壳在窄窗口必须允许职责宿主换行，不能用固定单行高度裁切动作。");
                Assert.AreEqual(StyleKeyword.None, contract.style.maxHeight.keyword);
                foreach (string rowName in new[]
                         {
                             "ESMenuTreeSystemActionRow",
                             "ESMenuTreeGlobalActionRow",
                             "ESMenuTreeWindowActionRow",
                             "ESMenuTreePageActionRow"
                         })
                {
                    VisualElement row = root.Q<VisualElement>(rowName);
                    Assert.IsNotNull(row, rowName + " 仍须作为独立职责宿主存在。");
                    Assert.AreEqual(DisplayStyle.Flex, row.style.display.value);
                    Assert.AreEqual(Overflow.Visible, row.style.overflow.value);
                    Assert.AreEqual(
                        DisplayStyle.None,
                        root.Q<Label>(rowName + "Label").style.display.value,
                        "紧凑宿主应隐藏职责文字，但不能删除职责宿主。");
                }
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CompactHostDoesNotReserveHeightForEmptyResponsibilityRows()
        {
            CompactSparseContractWindow window =
                ScriptableObject.CreateInstance<CompactSparseContractWindow>();
            try
            {
                MethodInfo createGui = typeof(ESMenuTreeWindow<CompactSparseContractWindow>)
                    .GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);

                VisualElement root = window.rootVisualElement;
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeSystemActionRow").style.display.value);
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeGlobalActionRow").style.display.value);
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeWindowActionRow").style.display.value);
                Assert.AreEqual(
                    DisplayStyle.Flex,
                    root.Q<VisualElement>("ESMenuTreePageActionRow").style.display.value,
                    "页面导航仍必须在紧凑宿主中可访问。");
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static bool HasHeaderActionText(VisualElement host, string expected)
        {
            return host != null && host.Children().OfType<Button>().Any(button =>
                string.Equals(button.text, expected, StringComparison.Ordinal)
                || string.Equals(button.Q<Label>()?.text, expected, StringComparison.Ordinal));
        }

        [Test]
        public void MenuTreeBaseOwnsDefaultSemiSleepControlsWithoutWindowHostSetup()
        {
            DefaultSemiSleepContractWindow window =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            try
            {
                MethodInfo createGui = typeof(ESMenuTreeWindow<DefaultSemiSleepContractWindow>)
                    .GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);

                VisualElement systemHost = window.rootVisualElement
                    .Q<VisualElement>("ESMenuTreeSystemActions");
                Assert.IsNotNull(systemHost, "System 宿主必须由窗口基类自动创建。");
                Assert.IsNotNull(
                    systemHost.Q<VisualElement>("ESWindowSystemActions"),
                    "休眠控件必须由基础层自动注入，派生窗口不应手工创建。");
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MenuTreeBuildFailureRebindsAccordingToDeclaredSleepContractAfterClear()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "Plugins",
                    "ES",
                    "Editor",
                    "ESMenuTreeWindow",
                    "-Templates",
                    "-ESMenuTreeWindow.cs"),
                Encoding.UTF8);
            string recovery = ExtractBalancedSourceBlock(
                source,
                "private void RecoverFromWindowBuildFailure(");
            int unbind = recovery.IndexOf(
                "ESWindowFoundation.Unbind(this);",
                StringComparison.Ordinal);
            int clear = recovery.IndexOf(
                "rootVisualElement.Clear();",
                StringComparison.Ordinal);
            int contractBranch = recovery.IndexOf(
                "if (ESWindow_SupportsSemiSleep)",
                StringComparison.Ordinal);
            int bindFull = recovery.IndexOf(
                "ESWindowFoundation.BindFullSleep(this);",
                StringComparison.Ordinal);
            int bindTransient = recovery.IndexOf(
                "ESWindowFoundation.BindTransient(this);",
                StringComparison.Ordinal);

            Assert.GreaterOrEqual(unbind, 0);
            Assert.Greater(clear, unbind,
                "构建失败视图替换 VisualTree 前必须先解除旧绑定。");
            Assert.Greater(contractBranch, clear,
                "失败视图挂载完成后必须按声明合同重新进入 Foundation。");
            Assert.Greater(bindFull, contractBranch,
                "Full 窗口构建失败后仍必须恢复完整休眠绑定。");
            Assert.Greater(bindTransient, bindFull,
                "Transient 窗口构建失败后必须恢复瞬态生命周期绑定。");
        }

        [Test]
        public void MenuTreeWindowCanDisableBuiltInSemiSleepThroughOneOverride()
        {
            NoSemiSleepContractWindow window =
                ScriptableObject.CreateInstance<NoSemiSleepContractWindow>();
            try
            {
                MethodInfo createGui = typeof(ESMenuTreeWindow<NoSemiSleepContractWindow>)
                    .GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);

                VisualElement root = window.rootVisualElement;
                Assert.IsNull(root.Q<VisualElement>("ESWindowSystemActions"));
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeSystemActionRow").style.display.value);
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeGlobalActionRow").style.display.value,
                    "普通宽窗口的空全局动作区不得只显示“全局”标签。");
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q<VisualElement>("ESMenuTreeWindowActionRow").style.display.value,
                    "普通宽窗口的空窗口动作区不得只显示“窗口”标签。");
                Assert.AreEqual(
                    DisplayStyle.Flex,
                    root.Q<VisualElement>("ESMenuTreePageActionRow").style.display.value,
                    "页面导航仍必须保留。");
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SleepOwnerContractUsesExplicitModesAndRejectsSelfOwnership()
        {
            DefaultSemiSleepContractWindow owner =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            DefaultSemiSleepContractWindow child =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(child);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsFalse(ESWindowFoundation.SetSleepOwner(
                    owner,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));

                ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.OwnedSurface);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.OwnedSurface),
                    "OwnedSurface 重复登记必须保持幂等，不能覆盖进入关系前的 Full 能力。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.OwnedSurface,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(child));
                ESWindowFoundation.ClearSleepOwner(child);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsTrue(
                    ESWindowFoundation.IsWindowSleepSupported(child),
                    "OwnedSurface 解除后必须恢复窗口类型原本的默认 Full 休眠能力。");

                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(child, false));
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.OwnedSurface));
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsTrue(
                    ESWindowFoundation.IsWindowSleepSupported(child),
                    "OwnedSurface 切换到 FollowOwner 后必须恢复 Full 类型能力。");
                Assert.IsFalse(
                    ESWindowFoundation.IsWindowSemiSleepAllowed(child),
                    "关系切换不得覆盖用户在系统菜单中关闭休眠的选择。");
                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(child, true));

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                ESWindowFoundation.Close(owner);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "父窗口关闭或解绑后，子窗口必须解除跟随并继续作为独立窗口存在。");
                Assert.IsNotNull(child);
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SleepOwnerContractRejectsAmbiguousModeOwnerCombinationsWithoutMutation()
        {
            DefaultSemiSleepContractWindow owner =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            DefaultSemiSleepContractWindow child =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(child);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));

                Assert.IsFalse(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.Independent));
                Assert.IsFalse(ESWindowFoundation.SetSleepOwner(
                    child,
                    null,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsFalse(ESWindowFoundation.SetSleepOwner(
                    child,
                    null,
                    ESWindowSleepLinkMode.OwnedSurface));
                Assert.IsFalse(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    (ESWindowSleepLinkMode)byte.MaxValue));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "被拒绝的模式/owner 组合不得改写已有关系。");

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    null,
                    ESWindowSleepLinkMode.Independent));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OwnerCloseRestoresOwnedSurfaceBeforeIsolatedRelationshipCallback()
        {
            RuntimeContractWindow owner =
                ScriptableObject.CreateInstance<RuntimeContractWindow>();
            RelationshipCallbackContractWindow child =
                ScriptableObject.CreateInstance<RelationshipCallbackContractWindow>();
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(child);
                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(child, false));
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.OwnedSurface));
                Assert.IsFalse(ESWindowFoundation.IsWindowSleepSupported(child));

                child.ThrowOnDetach = true;
                LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex(
                        "ES relationship callback failure"));
                Assert.DoesNotThrow(() => ESWindowFoundation.Close(owner));

                Assert.IsFalse(ESWindowFoundation.IsBound(owner));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsTrue(ESWindowFoundation.IsWindowSleepSupported(child),
                    "OwnedSurface 的 owner 关闭后必须恢复子窗口原本的 Full 能力。");
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(child),
                    "恢复类型能力时不得覆盖用户关闭休眠的选择。");
                Assert.IsTrue(child.DetachedByOwnerClose,
                    "Core 状态恢复后仍必须发送持久脱离通知。");
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OwnerCloseUsesSnapshotWhenRelationshipCallbackClosesSibling()
        {
            RuntimeContractWindow owner =
                ScriptableObject.CreateInstance<RuntimeContractWindow>();
            RelationshipCallbackContractWindow mutatingChild =
                ScriptableObject.CreateInstance<RelationshipCallbackContractWindow>();
            RelationshipCallbackContractWindow sibling =
                ScriptableObject.CreateInstance<RelationshipCallbackContractWindow>();
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(mutatingChild);
                ESWindowFoundation.Bind(sibling);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    mutatingChild,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    sibling,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                mutatingChild.CloseOnDetach = sibling;

                Assert.DoesNotThrow(() => ESWindowFoundation.Close(owner));

                Assert.IsFalse(ESWindowFoundation.IsBound(owner));
                Assert.IsFalse(ESWindowFoundation.IsBound(sibling));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(mutatingChild));
                Assert.IsTrue(mutatingChild.DetachedByOwnerClose);
            }
            finally
            {
                ESWindowFoundation.Close(sibling);
                ESWindowFoundation.Close(mutatingChild);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(sibling);
                UnityEngine.Object.DestroyImmediate(mutatingChild);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PendingSleepOwnerResolvesOnlyByStableOwnerKey()
        {
            DefaultSemiSleepContractWindow child =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            DefaultSemiSleepContractWindow owner =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            try
            {
                ESWindowFoundation.Bind(child);
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(
                    child,
                    "ES.Tests.Owner",
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner),
                    "显式 owner 绑定必须覆盖并清理此前登记的 PendingFollowOwner。");
                Assert.AreEqual(
                    0,
                    ESWindowFoundation.ResolvePendingSleepOwners("ES.Tests.Owner", owner),
                    "显式绑定后不得保留旧 Pending 记录，避免宿主恢复时反向覆盖当前关系。");
                Assert.AreEqual(
                    0,
                    ESWindowFoundation.ResolvePendingSleepOwners("ES.Tests.Other", owner));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child));
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SuspendAndContentRebuildPreserveFollowOwnerUntilRealClose()
        {
            DefaultSemiSleepContractWindow owner =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            FollowOwnerContractWindow child =
                ScriptableObject.CreateInstance<FollowOwnerContractWindow>();
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(child);
                child.ReactivateOwner(owner);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));

                ESWindowFoundation.Suspend(owner);
                Assert.IsTrue(
                    ESWindowFoundation.IsBound(owner),
                    "OnDisable 暂停必须保留可恢复 binding slot。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "暂时停用不得永久切断 FollowOwner。");

                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Unbind(owner);
                Assert.IsTrue(
                    ESWindowFoundation.IsBound(owner),
                    "VisualTree 内容重建必须保留可恢复 binding slot。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "VisualTree 内容重建解绑不得把存活子窗口降级为 Independent。");
                ESWindowFoundation.Unbind(child);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "子窗口内容重建也不得丢失 FollowOwner 关系。");

                ESWindowFoundation.Close(owner);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsTrue(
                    ((IESWindowSleepRelationshipState)child).SleepOwnerDetachedByClose,
                    "只有 OnDestroy 的真实 Close 才能持久化 owner 已关闭语义。");
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RejectedPendingOwnerCycleKeepsRecoveryIntent()
        {
            DefaultSemiSleepContractWindow first =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            DefaultSemiSleepContractWindow second =
                ScriptableObject.CreateInstance<DefaultSemiSleepContractWindow>();
            const string ownerKey = "ES.Tests.PendingCycle";
            try
            {
                ESWindowFoundation.Bind(first);
                ESWindowFoundation.Bind(second);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    first,
                    second,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(
                    second,
                    ownerKey,
                    ESWindowSleepLinkMode.FollowOwner));

                Assert.AreEqual(
                    0,
                    ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, first),
                    "会形成 owner 环的恢复必须被拒绝。");
                ESWindowFoundation.ClearSleepOwner(first);
                Assert.AreEqual(
                    1,
                    ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, first),
                    "被拒绝的 Pending 恢复意图必须保留到 owner 图合法为止。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(second));
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                ESWindowFoundation.Close(second);
                ESWindowFoundation.Close(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void InteractionHoldNeverImplicitlyBindsUnknownWindows()
        {
            RuntimeContractWindow window =
                ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                Assert.IsFalse(ESWindowFoundation.IsBound(window));
                using (ESWindowFoundation.HoldInteraction(window, "unbound-test"))
                    Assert.IsFalse(
                        ESWindowFoundation.IsBound(window),
                        "InteractionHold 不得把原生或第三方 owner 隐式接入 ES Presentation。");

                ESWindowFoundation.Bind(window);
                Assert.IsTrue(ESWindowFoundation.IsBound(window));
                using (ESWindowFoundation.HoldInteraction(window, "bound-test"))
                    Assert.IsTrue(ESWindowFoundation.IsBound(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void OwnerCloseDetachesPersistentlyButDomainReloadKeepsRelationshipIntent()
        {
            RuntimeContractWindow owner =
                ScriptableObject.CreateInstance<RuntimeContractWindow>();
            FollowOwnerContractWindow child =
                ScriptableObject.CreateInstance<FollowOwnerContractWindow>();
            FieldInfo reloadFlag = typeof(ESEditorPresentation).GetField(
                "domainReloadInProgress",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reloadFlag);
            try
            {
                ESWindowFoundation.Bind(owner);
                ESWindowFoundation.Bind(child);
                child.ReactivateOwner(owner);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));

                ESWindowFoundation.Close(owner);
                var relationship = (IESWindowSleepRelationshipState)child;
                Assert.IsTrue(
                    relationship.SleepOwnerDetachedByClose,
                    "父窗口真实关闭后必须持久化脱离意图，防止重建时偷偷恢复 PendingFollowOwner。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));

                UnityEngine.Object.DestroyImmediate(owner);
                owner = ScriptableObject.CreateInstance<RuntimeContractWindow>();
                ESWindowFoundation.Bind(owner);
                child.ReactivateOwner(owner);
                Assert.IsFalse(relationship.SleepOwnerDetachedByClose);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));

                reloadFlag.SetValue(null, true);
                ESWindowFoundation.Close(owner);
                Assert.IsFalse(
                    relationship.SleepOwnerDetachedByClose,
                    "Domain Reload 期间只释放活动引用，不得把声明关系误记为用户关闭后的永久脱离。");
            }
            finally
            {
                reloadFlag.SetValue(null, false);
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProductionFollowOwnerWindowsExposeExplicitStableContracts()
        {
            AssertFollowOwnerContract(
                typeof(ESTrackItemTemporaryInspectorWindow),
                "ES.TrackView.Window");
            AssertFollowOwnerContract(
                typeof(ESTrackClipTemporaryInspectorWindow),
                "ES.TrackView.Window");
            AssertFollowOwnerContract(
                typeof(ESTrackSkillDataTemporaryInspectorWindow),
                "ES.TrackView.Window");
            AssertFollowOwnerContract(
                typeof(ESAssetPackageRecordPreviewWindow),
                "ES.AssetPackageBake.Window");
            MethodInfo assetPreviewOpen = typeof(ESAssetPackageRecordPreviewWindow).GetMethod(
                "Open",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(ESAssetPackageBakeData),
                    typeof(ESAssetPackageBakeRecord),
                    typeof(ESAssetPackageBakeWindow)
                },
                null);
            Assert.IsNotNull(
                assetPreviewOpen,
                "资产记录预览必须由打开方显式传入具体 ESAssetPackageBakeWindow owner。");
            Assert.IsNotNull(
                typeof(ESAssetPackageBakeWindow).GetMethod(
                    "ESWindow_OnFoundationBound",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                "资产包主窗口必须在 Foundation 绑定完成后按稳定 ownerKey 解析 PendingFollowOwner。");
        }

        private static void AssertFollowOwnerContract(Type windowType, string expectedOwnerKey)
        {
            UnityEditor.EditorWindow window =
                ScriptableObject.CreateInstance(windowType) as UnityEditor.EditorWindow;
            Assert.IsNotNull(window, windowType.FullName + " 必须是 EditorWindow。");
            try
            {
                PropertyInfo modeProperty = FindInstanceProperty(
                    windowType,
                    "ESWindow_SleepLinkMode");
                PropertyInfo ownerKeyProperty = FindInstanceProperty(
                    windowType,
                    "ESWindow_SleepOwnerKey");
                Assert.IsNotNull(modeProperty, windowType.FullName + " 缺少休眠依赖声明。");
                Assert.IsNotNull(ownerKeyProperty, windowType.FullName + " 缺少稳定 ownerKey 声明。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    modeProperty.GetValue(window),
                    windowType.FullName + " 必须显式声明 FollowOwner。");
                Assert.AreEqual(
                    expectedOwnerKey,
                    ownerKeyProperty.GetValue(window),
                    windowType.FullName + " 的 ownerKey 必须与父窗口权威常量一致。");
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static PropertyInfo FindInstanceProperty(Type type, string propertyName)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property;
                type = type.BaseType;
            }
            return null;
        }

        [Test]
        public void WindowActionHostsMapScopesAndRejectMissingHosts()
        {
            var system = new VisualElement();
            var global = new VisualElement();
            var window = new VisualElement();
            var hosts = new ESWindowActionHosts(system, global, window);

            Assert.AreSame(system, hosts.Get(ESWindowActionScope.System));
            Assert.AreSame(global, hosts.Get(ESWindowActionScope.Global));
            Assert.AreSame(window, hosts.Get(ESWindowActionScope.Window));

            var marker = new Label("窗口动作");
            Assert.AreSame(marker, hosts.Add(ESWindowActionScope.Window, marker));
            Assert.AreSame(window, marker.parent);
            Assert.Throws<InvalidOperationException>(() =>
                new ESWindowActionHosts().Add(ESWindowActionScope.Global, new Button()));
        }

        [Test]
        public void WindowActionHostsRejectAHostFromAnotherWindowRoot()
        {
            RuntimeContractWindow owner = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            RuntimeContractWindow other = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                var foreignHost = new VisualElement();
                other.rootVisualElement.Add(foreignHost);
                var hosts = new ESWindowActionHosts(system: foreignHost);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(owner, hosts));
                StringAssert.Contains("必须属于当前 EditorWindow.rootVisualElement", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void WindowActionHostsRejectSharedScopeHost()
        {
            RuntimeContractWindow window = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                var shared = new VisualElement();
                window.rootVisualElement.Add(shared);
                var hosts = new ESWindowActionHosts(system: shared, global: shared);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(window, hosts));
                StringAssert.Contains("不能复用同一个动作宿主", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SemiSleepStressGridDistributesTwentyOneWindowsInsideMainBounds()
        {
            Assert.AreEqual(21, ESWindowSemiSleepStressTest.ConfiguredWindowCount);
            Rect main = new Rect(100f, 80f, 1800f, 1000f);
            Rect[] bounds = Enumerable.Range(0, 21)
                .Select(index => ESWindowSemiSleepStressTest.BuildSleepBounds(main, index))
                .ToArray();

            Assert.AreEqual(21, bounds.Distinct().Count());
            foreach (Rect item in bounds)
            {
                Assert.AreEqual(100f, item.width);
                Assert.AreEqual(100f, item.height);
                Assert.GreaterOrEqual(item.xMin, main.xMin);
                Assert.GreaterOrEqual(item.yMin, main.yMin);
                Assert.LessOrEqual(item.xMax, main.xMax);
                Assert.LessOrEqual(item.yMax, main.yMax);
            }
        }

        [Test]
        public void SemiSleepPerformanceGridSupportsTwentyFiftyAndOneHundredWindows()
        {
            CollectionAssert.AreEqual(
                new[] { 20, 50, 100 },
                ESWindowSemiSleepStressTest.ConfiguredPerformanceWindowCounts);
            Rect main = new Rect(-640f, 120f, 1920f, 1200f);

            foreach (int count in ESWindowSemiSleepStressTest.ConfiguredPerformanceWindowCounts)
            {
                Rect[] bounds = Enumerable.Range(0, count)
                    .Select(index => ESWindowSemiSleepStressTest.BuildSleepBounds(main, index, count))
                    .ToArray();
                Assert.AreEqual(count, bounds.Distinct().Count(), $"{count} 窗口网格不得重复落点。");
                foreach (Rect item in bounds)
                {
                    Assert.GreaterOrEqual(item.xMin, main.xMin);
                    Assert.GreaterOrEqual(item.yMin, main.yMin);
                    Assert.LessOrEqual(item.xMax, main.xMax);
                    Assert.LessOrEqual(item.yMax, main.yMax);
                }
            }
        }

        [Test]
        public void SemiSleepCommercialMatrixDistributesProductionWindowsAcrossFourEdges()
        {
            Rect main = new Rect(-1920f, -120f, 1920f, 1200f);
            int count = ESWindowSemiSleepStressTest.ConfiguredWindowCount;
            Assert.GreaterOrEqual(count, 4);

            for (int index = 0; index < count; index++)
            {
                Rect bounds = ESWindowSemiSleepStressTest.BuildCommercialEdgeBounds(
                    main,
                    index,
                    count);
                Assert.AreEqual(100f, bounds.width);
                Assert.AreEqual(100f, bounds.height);
                switch (index % 4)
                {
                    case 0:
                        Assert.AreEqual(main.x + 18f, bounds.x, 0.001f);
                        break;
                    case 1:
                        Assert.AreEqual(main.xMax - 118f, bounds.x, 0.001f);
                        break;
                    case 2:
                        Assert.AreEqual(main.y + 18f, bounds.y, 0.001f);
                        break;
                    default:
                        Assert.AreEqual(main.yMax - 118f, bounds.y, 0.001f);
                        break;
                }
            }
        }

        [Test]
        public void SemiSleepPerformanceSampleReportsPerUpdateMetrics()
        {
            var sample = new ESWindowSemiSleepPerformanceSample(
                100,
                4,
                400,
                24,
                24,
                System.Diagnostics.Stopwatch.Frequency * 2L,
                400L,
                System.Diagnostics.Stopwatch.Frequency,
                250L,
                8d);

            Assert.AreEqual(100, sample.BoundWindowCount);
            Assert.AreEqual(100, sample.BindingVisitCount / sample.UpdateCount);
            Assert.AreEqual(500000d, sample.AverageUpdateMicroseconds, 0.001d);
            Assert.AreEqual(100d, sample.AverageAllocatedBytesPerUpdate, 0.001d);
            Assert.AreEqual(1000000d, sample.MaximumUpdateMicroseconds, 0.001d);
            Assert.AreEqual(250L, sample.MaximumAllocatedBytesPerUpdate);
        }

        [Test]
        public void SemiSleepBenchmarkProbeDeclaresSystemActionHost()
        {
            ESWindowSleepBenchmarkProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepBenchmarkProbeWindow>();
            try
            {
                window.Configure(0);
                MethodInfo createGui = typeof(ESWindowSleepBenchmarkProbeWindow).GetMethod(
                    "CreateGUI",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(createGui);
                createGui.Invoke(window, null);

                VisualElement systemHost = window.rootVisualElement
                    .Q<VisualElement>("ESWindowSleepBenchmarkSystemActions");
                Assert.IsNotNull(systemHost);
                Assert.IsNotNull(systemHost.Q<VisualElement>("ESWindowSystemActions"));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MenuTreeWindowExposesStablePageContextContract()
        {
            Assert.IsTrue(typeof(IESWindowPageContextHost).IsAssignableFrom(
                typeof(RuntimeContractWindow)));
            MethodInfo select = typeof(IESWindowPageContextHost).GetMethod(
                "ESWindow_TrySelectPage");
            Assert.IsNotNull(select);
            Assert.AreEqual(typeof(bool), select.ReturnType);
        }

        [Test]
        public void RememberedPagePreferenceKeyIsProjectAndWindowScoped()
        {
            MethodInfo buildKey = typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetMethod(
                "BuildRememberedPagePreferenceKey",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(buildKey);

            string normalizedA = (string)buildKey.Invoke(
                null,
                new object[] { "F:\\Project\\Assets", typeof(RuntimeContractWindow) });
            string normalizedB = (string)buildKey.Invoke(
                null,
                new object[] { "f:/project/assets/", typeof(RuntimeContractWindow) });
            string otherProject = (string)buildKey.Invoke(
                null,
                new object[] { "F:/OtherProject/Assets", typeof(RuntimeContractWindow) });
            string otherWindow = (string)buildKey.Invoke(
                null,
                new object[] { "F:/Project/Assets", typeof(ESWindowLauncher) });

            Assert.AreEqual(normalizedA, normalizedB);
            Assert.AreNotEqual(normalizedA, otherProject);
            Assert.AreNotEqual(normalizedA, otherWindow);
            StringAssert.StartsWith("ES.MenuTree.LastPage.", normalizedA);
        }

        [Test]
        public void MenuTreeLastPageMemoryCanBeDisabledPerWindowType()
        {
            PropertyInfo remember = typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetProperty(
                "ESWindow_RememberLastPage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(remember);
            Assert.IsTrue(remember.GetMethod.IsVirtual);
            Assert.AreEqual(typeof(bool), remember.PropertyType);
        }

        [Test]
        public void RememberedPageResolutionRejectsMissingStableId()
        {
            var builder = new ESMenuTreeBuilder();
            builder.Add("page.valid", "分组 / 有效页面", new EmptyPage());
            MethodInfo resolve = typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetMethod(
                "ResolveRememberedPageId",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(resolve);

            string valid = (string)resolve.Invoke(
                null,
                new object[] { "page.valid", builder.PagesById });
            string missing = (string)resolve.Invoke(
                null,
                new object[] { "page.deleted", builder.PagesById });

            Assert.AreEqual("page.valid", valid);
            Assert.AreEqual(string.Empty, missing);
        }

        [Test]
        public void BuilderRejectsDuplicateStableId()
        {
            var builder = new ESMenuTreeBuilder();
            builder.Add("page.same", "分组 / 页面 A", new EmptyPage());

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                builder.Add("page.same", "分组 / 页面 B", new EmptyPage()));

            StringAssert.Contains("StableId", failure.Message);
        }

        [Test]
        public void BuilderRejectsOnePageInstanceAtMultiplePaths()
        {
            var builder = new ESMenuTreeBuilder();
            var page = new EmptyPage();
            builder.Add("page.first", "分组 / 页面 A", page);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                builder.Add("page.second", "分组 / 页面 B", page));

            StringAssert.Contains("同一个页面实例", failure.Message);
        }

        [Test]
        public void BuilderAcceptsOneThousandStablePages()
        {
            var builder = new ESMenuTreeBuilder();
            for (int i = 0; i < 1000; i++)
                builder.Add(
                    "scale.page." + i,
                    "规模 / 分组 " + i % 20 + " / 页面 " + i,
                    new EmptyPage());

            Assert.AreEqual(1000, builder.PageCount);
        }

        [Test]
        public void PanelPendingChangesContractSavesAndDiscards()
        {
            bool pending = true;
            int saveCount = 0;
            int discardCount = 0;
            var page = new ESMenuTreePanelPage((_, __) => { })
                .WithPendingChanges(
                    () => pending,
                    () =>
                    {
                        saveCount++;
                        pending = false;
                        return true;
                    },
                    () =>
                    {
                        discardCount++;
                        pending = false;
                    });

            Assert.IsTrue(page.HasPendingChanges);
            Assert.IsTrue(page.TrySavePendingChanges(out string failure));
            Assert.IsNull(failure);
            Assert.AreEqual(1, saveCount);
            Assert.IsFalse(page.HasPendingChanges);

            pending = true;
            page.DiscardPendingChanges();
            Assert.AreEqual(1, discardCount);
            Assert.IsFalse(page.HasPendingChanges);
        }

        [Test]
        public void SerializedBindingAppliesToAllTargetsAndDisposes()
        {
            SerializedTarget first = ScriptableObject.CreateInstance<SerializedTarget>();
            SerializedTarget second = ScriptableObject.CreateInstance<SerializedTarget>();
            try
            {
                var binding = new ESEditorSerializedPanelBinding(
                    new UnityEngine.Object[] { first, second });
                binding.FindProperty("capacity").intValue = 48;

                Assert.IsTrue(binding.ApplyModifiedProperties());
                Assert.AreEqual(48, first.capacity);
                Assert.AreEqual(48, second.capacity);

                binding.Dispose();
                Assert.Throws<ObjectDisposedException>(() => binding.FindProperty("capacity"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void AssetPackagePathSafetyRejectsTraversalAndAbsolutePaths()
        {
            Assert.IsFalse(ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(
                "Assets/../ProjectSettings", out _));
            Assert.IsFalse(ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(
                "C:/Outside", out _));
            Assert.IsFalse(ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(
                "../Assets/Outside", out _));

            Assert.IsTrue(ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(
                "assets\\Exports\\VFX", out string normalized));
            Assert.AreEqual("Assets/Exports/VFX", normalized);
        }

        [Test]
        public void AssetPackagePathSafetyRejectsReservedAndTransactionFolders()
        {
            Assert.IsTrue(ESAssetPackagePathSafety.IsForbiddenExportFolder("Assets/Resources/Effects"));
            Assert.IsTrue(ESAssetPackagePathSafety.IsForbiddenExportFolder("Assets/Tools/Editor"));
            Assert.IsTrue(ESAssetPackagePathSafety.IsForbiddenExportFolder("Assets/.Recovery/Package"));
            Assert.IsTrue(ESAssetPackagePathSafety.IsForbiddenExportFolder("Assets/.ESBakeTransactions/Run"));
            Assert.IsFalse(ESAssetPackagePathSafety.IsForbiddenExportFolder("Assets/_ESAssetPackageExport/VFX"));
        }

        [Test]
        public void AssetPackagePathSafetyOnlyAllowsAssetsRootOverlapInsideConfiguredExportRoot()
        {
            Assert.IsTrue(ESAssetPackagePathSafety.IsAllowedExportOverlap(
                "Assets", "Assets/_ESAssetPackageExport", "Assets/_ESAssetPackageExport/VFX"));
            Assert.IsFalse(ESAssetPackagePathSafety.IsAllowedExportOverlap(
                "Assets", "Assets/_ESAssetPackageExport", "Assets/OtherSource"));
            Assert.IsFalse(ESAssetPackagePathSafety.IsAllowedExportOverlap(
                "Assets/Source", "Assets/_ESAssetPackageExport", "Assets/Source/Output"));
        }

        [Test]
        public void AssetPackageResolutionSnapshotSealDetectsMutation()
        {
            var snapshot = new ESAssetPackageResolutionSnapshot
            {
                packageId = "package.test",
                definitionHash = "definition.hash",
                createdUtc = "2026-08-13T00:00:00.0000000Z",
                items = new List<ESAssetPackageResolutionItem>
                {
                    new ESAssetPackageResolutionItem
                    {
                        sourceGuid = "source-guid",
                        sourcePath = "Assets/Source.prefab",
                        sourceDependencyHash = "dependency-hash",
                        sourceFileHash = "source-file-hash",
                        targetPath = "Assets/Export/Target.prefab",
                        expectedTargetGuid = string.Empty,
                        expectedTargetFileHash = string.Empty,
                        category = ESAssetPackageCategory.Prefab,
                        operation = ESAssetPackageExportOperation.Create,
                        reasonCode = ESAssetPackageExportReasonCode.NewSource
                    }
                }
            };

            snapshot.Seal();
            Assert.IsTrue(snapshot.HasValidIntegrity());
            snapshot.items[0].targetPath = "Assets/Other/Target.prefab";
            Assert.IsFalse(snapshot.HasValidIntegrity());
        }

        [Test]
        public void AssetPackageFixedPathInvalidDoesNotFallbackToDefaultFolder()
        {
            var setting = new ESAssetPackageCategoryFolderSetting
            {
                category = ESAssetPackageCategory.Prefab,
                folderName = "Prefabs",
                useFixedAssetPath = true,
                fixedAssetFolderPath = "C:/outside"
            };
            Assert.IsTrue(setting.useFixedAssetPath);
            Assert.IsFalse(ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(setting.fixedAssetFolderPath, out _));
        }

        [Test]
        public void AssetPackageExportContractHasSingleEntryAndStructuredRollbackState()
        {
            MethodInfo[] exports = typeof(ESAssetPackageBakeUtility)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "ExportSelectedAssetsByCategory")
                .ToArray();
            Assert.AreEqual(1, exports.Length);
            Assert.IsNotNull(typeof(ESAssetPackageExportSession).GetField("rollbackState"));
            Assert.IsNotNull(typeof(ESAssetPackageExportSession).GetField("sourceAssetGuids"));
        }

        [Test]
        public void AssetPackageCategoryCatalogProvidesStableIdentityAndPreviewCapabilities()
        {
            ESAssetPackageCategoryDescriptor prefab = ESAssetPackageCategoryCatalog.Get(ESAssetPackageCategory.Prefab);
            ESAssetPackageCategoryDescriptor animation = ESAssetPackageCategoryCatalog.Get(ESAssetPackageCategory.Animation);
            Assert.AreEqual("prefab", prefab.stableKey);
            Assert.AreEqual("Prefabs", prefab.defaultExportFolder);
            Assert.AreEqual("d_Prefab Icon", prefab.iconName);
            Assert.IsTrue((prefab.previewCapabilities & ESAssetPackagePreviewCapability.DynamicEffect) != 0);
            Assert.AreEqual("animation", animation.stableKey);
            Assert.IsTrue((animation.previewCapabilities & ESAssetPackagePreviewCapability.Animation) != 0);
            Assert.AreEqual("Prefabs", ESAssetPackageCategoryCatalog.Get(ESAssetPackageCategory.Prefab).defaultExportFolder);
        }

        [Test]
        public void AssetPackagePreviewCapabilityContractSeparatesDynamicAndAnimationRoutes()
        {
            ESAssetPackagePreviewCapability prefab = ESAssetPackageCategoryCatalog.GetPreviewCapabilities(ESAssetPackageCategory.Prefab);
            ESAssetPackagePreviewCapability texture = ESAssetPackageCategoryCatalog.GetPreviewCapabilities(ESAssetPackageCategory.Texture);
            Assert.IsTrue((prefab & ESAssetPackagePreviewCapability.DynamicEffect) != 0);
            Assert.IsTrue((prefab & ESAssetPackagePreviewCapability.Animation) != 0);
            Assert.IsFalse((texture & ESAssetPackagePreviewCapability.DynamicEffect) != 0);
        }

        [Test]
        public void AssetPackageMaterialAndAudioUseDedicatedPreviewCapabilities()
        {
            ESAssetPackagePreviewCapability material = ESAssetPackageCategoryCatalog.GetPreviewCapabilities(ESAssetPackageCategory.Material);
            ESAssetPackagePreviewCapability audio = ESAssetPackageCategoryCatalog.GetPreviewCapabilities(ESAssetPackageCategory.Audio);

            Assert.IsTrue((material & ESAssetPackagePreviewCapability.Material) != 0);
            Assert.IsTrue((material & ESAssetPackagePreviewCapability.Detail) != 0);
            Assert.IsFalse((material & ESAssetPackagePreviewCapability.Animation) != 0);
            Assert.IsTrue((audio & ESAssetPackagePreviewCapability.Audio) != 0);
            Assert.IsTrue((audio & ESAssetPackagePreviewCapability.Detail) != 0);
            Assert.IsFalse((audio & ESAssetPackagePreviewCapability.Animation) != 0);
        }

        [Test]
        public void AssetPackageAudioPreviewKeepsSpatialControlsEditorOnly()
        {
            Type audioPlayer = typeof(ESAssetPackageAudioPreviewPlayer);
            Assert.IsNotNull(audioPlayer.GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("spatialBlend", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("dopplerLevel", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("sourceAzimuth", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("orbitSource", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("customRolloffCurve", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("previewSource", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetField("previewContext", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(audioPlayer.GetField("runtimeAudioModule", BindingFlags.Instance | BindingFlags.NonPublic));

            Type context = typeof(ESAssetPackagePreviewSceneContext);
            Assert.IsNotNull(context.GetProperty("OwnsPreviewAudioListener", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetProperty("AudioListenerOrigin", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetProperty("AudioListenerRotation", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetField("sharedPreviewAudioListener", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.IsNotNull(context.GetField("sharedPreviewAudioUsers", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.IsNotNull(context.GetField("sharedPreviewAudioPlaying", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.IsNotNull(context.GetMethod("SetPreviewAudioListenerPlaying", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetMethod("GetAudioListenerDescription", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(audioPlayer.GetMethod("RegisterTick", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetMethod("UnregisterTick", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetMethod("DrawDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void AssetPackageRecordPreviewWindowUsesSingleEsPreviewHostAndDedicatedPlayers()
        {
            Type windowType = typeof(ESAssetPackageRecordPreviewWindow);
            PropertyInfo stableId = windowType.GetProperty("ESWindow_PageStableId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(stableId);
            Assert.AreEqual(typeof(string), stableId.PropertyType);
            Assert.IsTrue(stableId.GetMethod.IsFamily || stableId.GetMethod.IsAssembly);
            Assert.IsNotNull(windowType.GetField("animationPreview", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(windowType.GetField("dynamicPreview", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(windowType.GetField("materialPreview", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(windowType.GetField("audioPreview", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ESAssetPackagePreviewUtility).GetMethod("DrawMaterialDetail", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ESAssetPackagePreviewUtility).GetMethod("DrawAudioDetail", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNotNull(typeof(ESAssetPackagePreviewUtility).GetMethod("GetCacheDiagnostics", BindingFlags.Static | BindingFlags.Public));
            Type dynamicPlayer = typeof(ESAssetPackageDynamicPreviewPlayer);
            Assert.IsNotNull(dynamicPlayer.GetMethod("DisposeInstance", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(dynamicPlayer.GetField("particlePreviewSession", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(dynamicPlayer.GetMethod("RegisterUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(dynamicPlayer.GetMethod("Simulate", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(Page_ParticleSystemAdjustment).GetMethod(
                "RegisterPreviewLifecycleCallbacks",
                BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.LessOrEqual(ESAssetPackageGridAnimationFrameCache.MaxEntries, 48);
        }

        [Test]
        public void PageDefinitionRejectsDuplicateActionId()
        {
            var definition = new ESMenuTreePageDefinition(
                "action.page",
                "动作 / 页面",
                new EmptyPage());
            definition.AddPageAction(new ESMenuTreePageAction(
                "same-action", "动作 A", "", _ => { }));

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                definition.AddPageAction(new ESMenuTreePageAction(
                    "same-action", "动作 B", "", _ => { })));

            StringAssert.Contains("动作 ID 重复", failure.Message);
        }

        [Test]
        public void PageNavigationLabelIsStableAndSearchableMetadata()
        {
            var definition = ESMenuTreePageDefinition.ForPanel(
                "world.editor",
                "内容制作 / 场景与对象 / 世界编辑器工作台",
                (_, __) => { })
                .WithNavigationLabel("世界");

            Assert.AreEqual("world.editor", definition.StableId);
            Assert.AreEqual("内容制作 / 场景与对象 / 世界编辑器工作台", definition.Path);
            Assert.AreEqual("世界", definition.NavigationLabel);
        }

        [Test]
        public void ToolbarActionsExposeExplicitScopeContract()
        {
            var global = new ESMenuTreeGlobalAction("global.refresh", "刷新", "", () => { });
            var page = new ESMenuTreePageAction("page.apply", "应用", "", _ => { });

            Assert.AreEqual(ESMenuTreeToolbarScope.Global, global.Scope);
            Assert.AreEqual("global.refresh", global.ActionId);
            Assert.AreEqual(ESMenuTreeToolbarScope.Page, page.Scope);
            Assert.AreEqual("page.apply", page.ActionId);
        }

        [Test]
        public void RuntimePageCrudRequiresMatchingOwner()
        {
            RuntimeContractWindow window = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                var first = ESMenuTreePageDefinition.ForPanel(
                    "runtime.panel",
                    "临时 / 面板",
                    (_, __) => { });
                ESMenuTreeMutationResult add = window.AddRuntimePage("tests.owner", first);
                Assert.IsTrue(add.Succeeded, add.Error);
                Assert.IsTrue(window.TryGetRuntimePageOwner(
                    "runtime.panel", out string ownerId));
                Assert.AreEqual("tests.owner", ownerId);

                ESMenuTreeMutationResult wrongOwner = window.RemoveRuntimePage(
                    "other.owner", "runtime.panel");
                Assert.IsFalse(wrongOwner.Succeeded);
                StringAssert.Contains("ownerId 不匹配", wrongOwner.Error);

                var replacement = ESMenuTreePageDefinition.ForPanel(
                    "runtime.panel",
                    "临时 / 更新后的面板",
                    (_, __) => { });
                ESMenuTreeMutationResult update = window.UpdateRuntimePage(
                    "tests.owner", replacement);
                Assert.IsTrue(update.Succeeded, update.Error);
                Assert.IsTrue(window.TryGetPageDefinition(
                    "runtime.panel", out ESMenuTreePageDefinition current));
                Assert.AreEqual("临时 / 更新后的面板", current.Path);

                ESMenuTreeMutationResult remove = window.RemoveRuntimePage(
                    "tests.owner", "runtime.panel");
                Assert.IsTrue(remove.Succeeded, remove.Error);
                Assert.IsFalse(window.TryGetPageDefinition("runtime.panel", out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RuntimePageRegistrationRejectsDuplicateIdPathAndInstance()
        {
            RuntimeContractWindow window = ScriptableObject.CreateInstance<RuntimeContractWindow>();
            try
            {
                var sharedPage = new EmptyPage();
                var first = new ESMenuTreePageDefinition(
                    "runtime.first", "临时 / 唯一路径", sharedPage);
                ESMenuTreeMutationResult firstResult = window.AddRuntimePage("tests.owner", first);
                Assert.IsTrue(firstResult.Succeeded, firstResult.Error);

                var duplicateId = new ESMenuTreePageDefinition(
                    "runtime.first", "临时 / 其他路径", new EmptyPage());
                ESMenuTreeMutationResult duplicateIdResult = window.AddRuntimePage(
                    "tests.owner", duplicateId);
                Assert.IsFalse(duplicateIdResult.Succeeded);
                StringAssert.Contains("StableId", duplicateIdResult.Error);

                var duplicatePath = new ESMenuTreePageDefinition(
                    "runtime.path", "临时/唯一路径", new EmptyPage());
                ESMenuTreeMutationResult duplicatePathResult = window.AddRuntimePage(
                    "tests.owner", duplicatePath);
                Assert.IsFalse(duplicatePathResult.Succeeded);
                StringAssert.Contains("路径", duplicatePathResult.Error);

                var duplicateInstance = new ESMenuTreePageDefinition(
                    "runtime.instance", "临时 / 实例冲突", sharedPage);
                ESMenuTreeMutationResult duplicateInstanceResult = window.AddRuntimePage(
                    "tests.owner", duplicateInstance);
                Assert.IsFalse(duplicateInstanceResult.Succeeded);
                StringAssert.Contains("页面实例", duplicateInstanceResult.Error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
