using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
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
        public void TwoPaneListStructuralMutationsDeclareUndoAndPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESTwoPaneListAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RegisterCompleteObjectUndo", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
            StringAssert.Contains("BeginStructuralMutation", source);
            StringAssert.Contains("CommitStructuralMutation", source);
        }

        [Test]
        public void EnumStringTableMutationsDeclareUndoAndPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESEnumStringTableAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RegisterCompleteObjectUndo", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
            StringAssert.Contains("BeginMutation", source);
            StringAssert.Contains("CommitMutation", source);
            StringAssert.Contains("TryGetSafeMultiEdit", source);
            StringAssert.Contains("serializedObject.targetObjects", source);
            StringAssert.Contains("property.propertyPath", source);
            StringAssert.Contains("managedReferenceFullTypename", source);
            StringAssert.Contains("same stable Enum/String identity", source);
        }

        [Test]
        public void AudioCuePreviewOnlyUsesFullClipFallbackForUntrimmedRanges()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "Preview",
                "ESAudioCueTrimPreviewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("out int actualStartSample", source);
            StringAssert.Contains("out int actualEndSample", source);
            StringAssert.Contains("if (startSample > 0 || endSample < clip.samples)", source);
            StringAssert.Contains("playFullClipMethod", source);
            StringAssert.Contains("never pretend that a trimmed range was honored", source);
        }

        [Test]
        public void StableTagReferencePickerDeclaresUndoAndPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESTagStableReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RegisterCompleteObjectUndo", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
            StringAssert.Contains("ES 选择稳定 GameTag", source);
        }

        [Test]
        public void AssetConfigKeyCompositeClearsDeclareUndoAndPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESAssetConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("RecordMutationUndo", source);
            StringAssert.Contains("Undo.RecordObjects", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
            StringAssert.Contains("ClearAssetIdentity(property, recordUndo: false)", source);
        }

        [Test]
        public void GameCoreConfigKeyCompositeMutationsDeclareUndoAndPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("RecordMutationUndo", source);
            StringAssert.Contains("Undo.RecordObjects", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
            StringAssert.Contains("同步 GameCore 配置键", source);
            StringAssert.Contains("清空 GameCore 配置键", source);
        }

        [Test]
        public void GameCoreConfigKeyDeclarationRejectsMultiObjectWrites()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("isEditingMultipleObjects", source);
            StringAssert.Contains("定义型 ConfigKey 不支持多对象同时写入", source);
            StringAssert.Contains("return false;", source);
        }

        [Test]
        public void InputImportWindowsRejectMultiObjectTargetsBeforeCreatingTemporaryState()
        {
            string actionPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESInputActionDefineDrawer.cs");
            string bindingPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESInputBindingDefineDrawer.cs");
            string action = File.ReadAllText(actionPath, new UTF8Encoding(false, true));
            string binding = File.ReadAllText(bindingPath, new UTF8Encoding(false, true));
            StringAssert.Contains("targets.Length != 1", action);
            StringAssert.Contains("导入窗口仅支持单对象编辑", action);
            StringAssert.Contains("targets.Length != 1", binding);
            StringAssert.Contains("导入窗口仅支持单对象编辑", binding);
            Assert.Less(action.IndexOf("targets.Length != 1", StringComparison.Ordinal),
                action.IndexOf("GetWindow<ESInputActionImportWindow>", StringComparison.Ordinal));
            Assert.Less(binding.IndexOf("targets.Length != 1", StringComparison.Ordinal),
                binding.IndexOf("GetWindow<ESInputActionBindingImportWindow>", StringComparison.Ordinal));
        }

        [Test]
        public void TwoPaneListDisablesMultiObjectStructuralMutations()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "AttributeDrawers", "ESTwoPaneListAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("isEditingMultipleObjects", source);
            StringAssert.Contains("多对象时禁用结构操作", source);
            StringAssert.Contains("if (serializedObject.isEditingMultipleObjects)", source);
            StringAssert.Contains("bool canResize = !isMultiObject", source);
        }

        [Test]
        public void GameCoreItemRepairSavesOnlyTheMutatedAsset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int repair = source.IndexOf("整理当前 Item 类型配置", StringComparison.Ordinal);
            int scopedSave = source.IndexOf("AssetDatabase.SaveAssetIfDirty(item)", repair, StringComparison.Ordinal);
            Assert.GreaterOrEqual(repair, 0);
            Assert.GreaterOrEqual(scopedSave, 0);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source.Substring(repair, Math.Min(900, source.Length - repair)));
        }

        [Test]
        public void PolymorphicReferenceFallbackWriteDeclaresPrefabPersistenceBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESPolymorphicReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("RecordFallbackUndo", source);
            StringAssert.Contains("MarkSerializedTargetsDirty", source);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", source);
        }

        [Test]
        public void InputImportersDeclarePrefabPersistenceBoundary()
        {
            string editorRoot = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal");
            string actionSource = File.ReadAllText(
                Path.Combine(editorRoot, "ESInputActionDefineDrawer.cs"),
                new UTF8Encoding(false, true));
            string bindingSource = File.ReadAllText(
                Path.Combine(editorRoot, "ESInputBindingDefineDrawer.cs"),
                new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject", actionSource);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", actionSource);
            StringAssert.Contains("window.maxSize", actionSource);
            StringAssert.Contains("Undo.RecordObject", bindingSource);
            StringAssert.Contains("PrefabUtility.RecordPrefabInstancePropertyModifications", bindingSource);
            StringAssert.Contains("window.maxSize", bindingSource);
        }

        [Test]
        public void CollectionDrawerReleasesSerializedTargetsOnValidationEarlyExit()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESCollectionDrawStyleAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool TryDeleteElement", source);
            StringAssert.Contains("private bool TryMoveElement", source);
            StringAssert.Contains("private bool TryDuplicateElement", source);
            StringAssert.Contains("private bool TrySetElementEnabled", source);
            StringAssert.Contains("private bool TryRestoreElementDefaultOrder", source);
            StringAssert.Contains("private bool TrySortAllByDefaultOrder", source);
            Assert.GreaterOrEqual(
                Regex.Matches(source, "DisposeCollectionTargets\\(targets\\);").Count,
                6,
                "所有批量编辑操作都必须拥有统一 SerializedObject 释放路径。");
        }

        [Test]
        public void GetOrAddDrawerReclaimsComponentWhenReferenceWriteFails()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESGetAddAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.AddComponent(go.gameObject, componentType)", source);
            StringAssert.Contains("Undo.DestroyObjectImmediate(cNow)", source);
            StringAssert.Contains("RecordPrefabInstanceModification(go)", source);
            StringAssert.Contains("entry == null || entry.BaseValueType == null", source);
        }

        [Test]
        public void ParticleSceneExampleReclaimsTransientGroundMaterialOnFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SimpleToolsWindow",
                "HierchyTools",
                "Simple_HierchyTool_Page_ParticleSystemAdjustment.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("DestroyTransientGroundMaterial(groundObject)", source);
            StringAssert.Contains("!EditorUtility.IsPersistent(material)", source);
            StringAssert.Contains("DestroyImmediate(material)", source);
        }

        [Test]
        public void ProceduralUiAssetCreationRollsBackTextureWhenSpriteCommitFails()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("bool assetCommitted = false", source);
            StringAssert.Contains("AssetDatabase.AddObjectToAsset(sprite, texture)", source);
            StringAssert.Contains("AssetDatabase.LoadMainAssetAtPath(assetPath) == texture", source);
            StringAssert.Contains("AssetDatabase.DeleteAsset(assetPath)", source);
        }

        [Test]
        public void MaterialPreviewPlayerBuildsPreviewObjectsTransactionally()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Material nextMaterial = null", source);
            StringAssert.Contains("GameObject nextObject = null", source);
            StringAssert.Contains("DestroyImmediate(nextObject)", source);
            StringAssert.Contains("DestroyImmediate(nextMaterial)", source);
            StringAssert.Contains("previewMaterial = nextMaterial", source);
        }

        [Test]
        public void PreviewLightCreationReclaimsLocalObjectBeforeContextOwnership()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private GameObject CreateDirectionalLight", source);
            StringAssert.Contains("if (go != null)\n                    DestroyObject(go);", source.Replace("\r\n", "\n"));
            StringAssert.Contains("预览灯光组件创建失败", source);
        }

        [Test]
        public void PreviewCameraCreationReclaimsLocalObjectOnConfigurationFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview", "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void EnsureCamera()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(2200, source.Length - method));
            StringAssert.Contains("catch", body);
            StringAssert.Contains("DestroyObject(created)", body);
            StringAssert.Contains("ownership registration failed", body);
            StringAssert.Contains("cameraObject = created", body);
        }

        [Test]
        public void PreviewObjectSceneMovesFailClosedAndVerifyDestination()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview", "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int contextMove = source.IndexOf("private bool MoveToContextScene(GameObject obj)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(contextMove, 0);
            string moveBody = source.Substring(contextMove, Math.Min(1300, source.Length - contextMove));
            StringAssert.Contains("SceneManager.MoveGameObjectToScene(obj, previewScene)", moveBody);
            StringAssert.Contains("return obj.scene == previewScene", moveBody);
            StringAssert.Contains("return sceneMode != ESEditorPreviewSceneMode.PreviewScene", moveBody);
            StringAssert.Contains("if (!moved)", source);
        }

        [Test]
        public void WorldBuilderCancelsCommercialValidationDelayCallOnDisable()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldBuilderWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("commercialValidationDelayCallback", source);
            StringAssert.Contains("EditorApplication.delayCall -= commercialValidationDelayCallback", source);
            StringAssert.Contains("CancelCommercialValidationDelayCallback();", source);
            StringAssert.Contains("protected override void ESWindow_OnHostDisable()", source);
        }

        [Test]
        public void WorldAcceptanceMemoryProfilerCallbackRemovesItsDelayCallHandle()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldWorkbenchAcceptance.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.CallbackFunction delayedCallback = null", source);
            StringAssert.Contains("EditorApplication.delayCall -= delayedCallback", source);
            StringAssert.Contains("EditorApplication.delayCall += delayedCallback", source);
        }

        [Test]
        public void InputActionDrawerRejectsIncompleteSerializedSchema()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputActionDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasRequiredProperties(property)", source);
            StringAssert.Contains("序列化结构不完整", source);
            StringAssert.Contains("EditorGUI.EndProperty();", source);
        }

        [Test]
        public void InputBindingDrawerRejectsIncompleteSerializedSchema()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputBindingDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasRequiredProperties(property)", source);
            StringAssert.Contains("ESInputBindingDefine 的序列化结构不完整", source);
            StringAssert.Contains("EditorGUI.EndProperty();", source);
        }

        [Test]
        public void AssetConfigKeyDrawerRejectsIncompleteSerializedSchema()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESAssetConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasRequiredProperties(property)", source);
            StringAssert.Contains("资源配置键的序列化结构不完整", source);
            StringAssert.Contains("EditorGUI.EndProperty();", source);
        }

        [Test]
        public void GameCoreConfigKeyDrawerRejectsIncompleteSerializedSchema()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasRequiredProperties(property)", source);
            StringAssert.Contains("GameCore 配置键的序列化结构不完整", source);
            StringAssert.Contains("EditorGUI.EndProperty();", source);
        }

        [Test]
        public void WorldInspectorsRejectIncompleteSerializedSchemas()
        {
            string graphPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueGraphAssetEditor.cs");
            string mapPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldMapAssetEditor.cs");
            string anchorPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueAnchorEditor.cs");
            string graph = File.ReadAllText(graphPath, new UTF8Encoding(false, true));
            string map = File.ReadAllText(mapPath, new UTF8Encoding(false, true));
            string anchor = File.ReadAllText(anchorPath, new UTF8Encoding(false, true));
            StringAssert.Contains("对话图序列化结构不完整", graph);
            StringAssert.Contains("!nodes.isArray || !edges.isArray", graph);
            StringAssert.Contains("地图序列化字段 definition 缺失", map);
            StringAssert.Contains("地图空间模板结构不完整", map);
            StringAssert.Contains("对话锚点序列化结构不完整", anchor);
        }

        [Test]
        public void WorldDialogueWorkbenchStopsOnIncompleteDefinitions()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("对话图数据结构不完整，已停止 Graph 编辑", source);
            StringAssert.Contains("地图数据结构不完整，已停止 2D 编辑", source);
            StringAssert.Contains("地图数据结构不完整，已停止 Scene 编辑", source);
            StringAssert.Contains("节点列表已被外部修改，已取消本次节点编辑", source);
            StringAssert.Contains("放置列表已被外部修改，已取消本次入口编辑", source);
            StringAssert.Contains("selectedPlacementIndex < placements.Count", source);
            StringAssert.Contains("对话图结构不完整，无法新增节点", source);
            StringAssert.Contains("definition.nodes[selectedNodeIndex] == null", source);
            StringAssert.Contains("graphAsset.Definition == null || mapAsset.Definition == null", source);
            StringAssert.Contains("地图结构无效，无法同步 Scene 对话锚点", source);
            StringAssert.Contains("需要同时绑定有效的地图和对话图", source);
            StringAssert.Contains("地图或对话图不是有效的项目资产，无法创建 Scene 锚点", source);
            StringAssert.Contains("地图或对话图不是有效的项目资产，无法创建地图入口", source);
            StringAssert.Contains("string placementId = string.IsNullOrWhiteSpace(anchor.placementId)", source);
            StringAssert.Contains("? Guid.NewGuid().ToString(\"N\")", source);
            StringAssert.Contains("if (!string.Equals(anchor.placementId, placementId", source);
            StringAssert.Contains("FindPlacement(placementId)", source);
        }

        [Test]
        public void WorldDialogueWorkbenchReleasesSerializedObjectsOnDisableAndRebind()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ReleaseSerializedObjects();", source);
            StringAssert.Contains("graphSerialized?.Dispose()", source);
            StringAssert.Contains("mapSerialized?.Dispose()", source);
            StringAssert.Contains("graphSerialized.targetObject != asset", source);
            StringAssert.Contains("mapSerialized.targetObject != asset", source);
        }

        [Test]
        public void WorldMapSpaceWindowKeepsSessionCleanupIndependent()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldMapSpaceEditorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESWorldAuthoringViewport currentViewport = viewport", source);
            StringAssert.Contains("ESWorldEditSession currentSession = editSession", source);
            StringAssert.Contains("currentViewport?.Dispose()", source);
            StringAssert.Contains("currentSession?.Dispose()", source);
            StringAssert.Contains("viewport = null", source);
            StringAssert.Contains("editSession = null", source);
        }

        [Test]
        public void EditorThemeWindowDisposesSerializedThemeBeforeInvalidation()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESPresentation", "ESEditorThemeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ReleaseSerializedTheme();", source);
            StringAssert.Contains("serializedTheme?.Dispose()", source);
        }

        [Test]
        public void CmdAgentWindowReleasesSerializedAgentBeforePanelRebuildAndClose()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESCmdAgent", "ESCmdAgentWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ReleaseSerializedAgent();", source);
            StringAssert.Contains("serializedAgent?.Dispose()", source);
            StringAssert.Contains("ReleaseSerializedAgent();\n            panel.Clear();", source);
        }

        [Test]
        public void ResourceSettingsPageDisposesSerializedSettingsOnRebindAndDisable()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "ResWindow", "ESResWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ReleaseResSettingSerializedObject();", source);
            StringAssert.Contains("resSettingSerializedObject?.Dispose()", source);
        }

        [Test]
        public void LocalizationDetailsOwnsAndReleasesSerializedCatalog()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "FontToolsWindow", "ESLocalizationToolsWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("detailSerializedCatalog?.Dispose()", source);
            StringAssert.Contains("ReleaseDetailSerializedCatalog();", source);
            StringAssert.Contains("propertyField.Bind(detailSerializedCatalog)", source);
        }

        [Test]
        public void ResourcePlanScanReleasesEachSerializedPlan()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESResourceCollectionWorkflowWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("finally", source);
            StringAssert.Contains("serialized.Dispose();", source);
            StringAssert.Contains("serialized?.Dispose();", source);
            StringAssert.Contains("serialized = null;", source);
        }

        [Test]
        public void ReleaseUploadWindowScopesPerFrameSerializedSettings()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetReleaseUploadWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (serializedSettings)", source);
        }

        [Test]
        public void CompositeShaderFaderScopesDiagnosticSerializedObjects()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESShader", "ESCompositeShaderFaderEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int usingCount = Regex.Matches(source, "using \\(var serializedFader = new SerializedObject\\(fader\\)\\)").Count;
            Assert.GreaterOrEqual(usingCount, 3);
        }

        [Test]
        public void ResourcePlanSynchronizerScopesValidationAndPendingObjects()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ResourcePlan", "Baking", "ESResourcePlanConfigKeySynchronizer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (var serialized = new SerializedObject(plan))", source);
            StringAssert.Contains("using (var scan = new SerializedObject(owner))", source);
            StringAssert.Contains("using (var serialized = new SerializedObject(owner))", source);
        }

        [Test]
        public void DependencyExpansionAndReferenceBakeScopeSerializedObjects()
        {
            string expansionPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "SerializedDependency", "ESSerializedDependencyExpander.cs");
            string bakerPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetReferenceBaker.cs");
            StringAssert.Contains("using (var serialized = new SerializedObject(current.Root))", File.ReadAllText(expansionPath, new UTF8Encoding(false, true)));
            StringAssert.Contains("using (var serializedObject = new SerializedObject(root))", File.ReadAllText(bakerPath, new UTF8Encoding(false, true)));
        }

        [Test]
        public void ResourceRuntimeAcceptanceBinderScopesSerializedSetup()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "Windows", "ESResourceRuntimeMonitorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (var serialized = new SerializedObject(binder))", source);
        }

        [Test]
        public void EnumStringTableMultiEditReleasesPerTargetProjection()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "AttributeDrawers", "ESEnumStringTableAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("finally", source);
            StringAssert.Contains("targetSerializedObject.Dispose();", source);
        }

        [Test]
        public void MaterialReplacementAccessorScopesSerializedReadsAndWrites()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_MaterialReplacement.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int usingCount = Regex.Matches(source, "using \\(var serializedObject = new SerializedObject\\(mono\\)\\)").Count;
            Assert.GreaterOrEqual(usingCount, 3);
            StringAssert.DoesNotContain("private SerializedProperty FindProperty()", source);
        }

        [Test]
        public void TrackViewUnbindsInteractiveCallbacksBeforeRebuild()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESTrackView", "-TrackView-Define", "ESTrackViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("UnbindNormalHandles();", source);
            StringAssert.Contains("horSlider?.UnregisterValueChangedCallback(HorSliderChange)", source);
            StringAssert.Contains("OnTrackPanelSplitterPointerEnter", source);
            StringAssert.DoesNotContain("m_TrackPanelSplitter.RegisterCallback<PointerEnterEvent>(_ =>", source);
        }

        [Test]
        public void WorkbenchGeneratedThumbnailReclaimsFailedTextureCreation()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Workbench", "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SafeDestroyThumbnail(texture);", source);
            StringAssert.Contains("texture.SetPixels32(pixels);", source);
            StringAssert.Contains("if (disposed || root == null || thumbnailRefreshSchedule != null)", source);
            StringAssert.Contains("if (disposed)\n            {\n                thumbnailRefreshSchedule?.Pause();", source);
        }

        [Test]
        public void CommandPaletteSolidTextureReclaimsFailedCreation()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (texture != null)", source);
            StringAssert.Contains("DestroyImmediate(texture);", source);
        }

        [Test]
        public void TrackTemporaryInspectorSkinReclaimsFailedTextureCreation()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESTrackView", "-TrackView-Define", "ESTrackTemporaryInspectorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("cachedTextures.Add(texture);", source);
            StringAssert.Contains("DestroyImmediate(texture);", source);
        }

        [Test]
        public void UiProceduralSpriteReclaimsTextureBeforeAssetCommitFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(texture, assetPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(texture)", source);
            StringAssert.Contains("DestroyImmediate(texture)", source);
        }

        [Test]
        public void UiMaterializerReclaimsFontMaterialBeforeAssetCommitFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(material, ShowcaseFontMaterialPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(material)", source);
            StringAssert.Contains("DestroyImmediate(material)", source);
        }

        [Test]
        public void UiGeneratedWhiteTextureReclaimsBeforeAssetCommitFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(texture, texturePath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(texture)", source);
            StringAssert.Contains("DestroyImmediate(texture)", source);
        }

        [Test]
        public void AssetPackagePersistentFramesReleasePartialLoadOnFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Texture2D[] loaded = null;", source);
            StringAssert.Contains("DisposeLoadedFrames(loaded);", source);
            StringAssert.Contains("catch (Exception ex)", source);
        }

        [Test]
        public void PolymorphicDrawerDisposesBatchSerializedViewsOnAllExits()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESPolymorphicReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("多态引用批量写入序列化视图释放失败", source);
            StringAssert.Contains("assignments[index].SerializedObject?.Dispose();", source);
        }

        [Test]
        public void SceneOptimizationDisposesMissingScriptSerializedView()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_SceneOptimization.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (var serializedObject = new SerializedObject(obj))", source);
            StringAssert.Contains("serializedObject.ApplyModifiedProperties();", source);
        }

        [Test]
        public void GameCorePhysicsLayerSyncDisposesTagManagerSerializedView()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "GameCorePhysicsLayerSettings.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]))", source);
            StringAssert.Contains("tagManager.ApplyModifiedProperties();", source);
        }

        [Test]
        public void WeaponProfilerBinderSetupDisposesSerializedView()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "WeaponTemplates", "ESWeaponShotProfilerSceneBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (var serializedBinder = new SerializedObject(binder))", source);
            StringAssert.Contains("serializedBinder.ApplyModifiedPropertiesWithoutUndo();", source);
        }

        [Test]
        public void CameraDefinitionMigrationDisposesSerializedViewOnEarlyReturn()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraDefinitionMigrationTool.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (SerializedObject serialized = new SerializedObject(target))", source);
            StringAssert.Contains("serialized.ApplyModifiedProperties();", source);
        }

        [Test]
        public void KeyGovernanceAuditDisposesSerializedScanView()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "ESKeyGovernanceAuditBuildGate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (SerializedObject serializedObject = new SerializedObject(owner))", source);
            StringAssert.Contains("serializedObject.GetIterator();", source);
        }

        [Test]
        public void ProfileMigrationDisposesExecutionPlanningAndHashViews()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESProfileWorkbench", "Migration", "ESGenericProfileMigrationService.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using (var serializedProfile = new SerializedObject(plan.Profile))", source);
            StringAssert.Contains("using (var serializedProfile = new SerializedObject(profile))", source);
            StringAssert.Contains("sourceVersion = FindHeaderSchemaVersion(serializedProfile).intValue;", source);
        }

        [Test]
        public void InputBindingHelperReleasesSerializedHolderBeforeDestroy()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESInputBindingDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SerializedObject serializedHolder = holderObject;", source);
            StringAssert.Contains("serializedHolder?.Dispose();", source);
            StringAssert.Contains("DestroyImmediate(holderToRelease);", source);
        }

        [Test]
        public void InputActionHelperReleasesSerializedHolderBeforeDestroy()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESInputActionDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SerializedObject serializedHolder = holderObject;", source);
            StringAssert.Contains("serializedHolder?.Dispose();", source);
            StringAssert.Contains("DestroyImmediate(holderToRelease);", source);
        }

        [Test]
        public void AdvancedDialogCloseCancelsValidationDelayCallback()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int releaseIndex = source.IndexOf("private void ReleaseWindowResources()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(releaseIndex, 0);
            string releaseBody = source.Substring(releaseIndex, Math.Min(1800, source.Length - releaseIndex));
            StringAssert.Contains("CancelAsyncValidation();", releaseBody);
            StringAssert.Contains("pendingValidationDelayCallback", source);
        }

        [Test]
        public void WorldBuilderDisableInvalidatesCommercialValidationCallbacks()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldBuilderWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int disableIndex = source.IndexOf("protected override void ESWindow_OnHostDisable()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(disableIndex, 0);
            string disableBody = source.Substring(disableIndex, Math.Min(900, source.Length - disableIndex));
            StringAssert.Contains("commercialValidationGeneration++;", disableBody);
            StringAssert.Contains("commercialValidationAcceptanceInProgress = false;", disableBody);
            StringAssert.Contains("ReleaseCommercialValidationPeerSession();", disableBody);
        }

        [Test]
        public void ActionAssetBuilderReclaimsUncommittedScriptableObject()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Action", "ESActionSliceABuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(asset)", source);
            StringAssert.Contains("DestroyImmediate(asset)", source);
        }

        [Test]
        public void CameraDefaultContentBuilderRollsBackPartialMainView()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraDefaultContentBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Transform rigRoot = null;", source);
            StringAssert.Contains("DestroyImmediate(rigRoot.gameObject);", source);
            StringAssert.Contains("DestroyImmediate(cameraObject);", source);
        }

        [Test]
        public void CameraTrackPreviewConstructorRollsBackBeforeSessionAssignment()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraTrackPreviewFactory.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("The factory cannot receive a session reference when this", source);
            StringAssert.Contains("ESEditorPreviewLifecycleHub.UnregisterScope(this)", source);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(rigRootObject)", source);
            StringAssert.Contains("renderContext?.Dispose()", source);
        }

        [Test]
        public void CameraCatalogCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraDefaultContentBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(asset)", source);
            StringAssert.Contains("DestroyImmediate(asset)", source);
        }

        [Test]
        public void CameraDefaultContentUsesScopedSavesForKnownAssets()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraDefaultContentBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int refresh = source.LastIndexOf("AssetDatabase.Refresh();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            string beforeRefresh = source.Substring(Math.Max(0, refresh - 520), Math.Min(520, refresh));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(playerDefinition);", beforeRefresh);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(vehicleDefinition);", beforeRefresh);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(definitionCatalog);", beforeRefresh);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(rigCatalog);", beforeRefresh);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(blenderSettings);", beforeRefresh);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", beforeRefresh);
        }

        [Test]
        public void SoDataEditorSaveServiceFlushesOnlyTrackedAssets()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int service = source.IndexOf("public static class EditorSaveService", StringComparison.Ordinal);
            Assert.GreaterOrEqual(service, 0);
            int flush = source.IndexOf("public static void Flush()", service, StringComparison.Ordinal);
            Assert.GreaterOrEqual(flush, service);
            int end = source.IndexOf("public static void RefreshNow()", flush, StringComparison.Ordinal);
            Assert.Greater(end, flush);
            string body = source.Substring(flush, end - flush);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(o);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void SoDataDeleteRejectsMainOrCrossAssetObjects()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void DeleteInfoFromGroup", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int destroy = source.IndexOf("Undo.DestroyObjectImmediate(infoAsset);", method, StringComparison.Ordinal);
            Assert.Greater(destroy, method);
            string guard = source.Substring(method, destroy - method);
            StringAssert.Contains("AssetDatabase.GetAssetPath(groupAsset)", guard);
            StringAssert.Contains("AssetDatabase.GetAssetPath(infoAsset)", guard);
            StringAssert.Contains("AssetDatabase.IsSubAsset(infoAsset)", guard);
            StringAssert.Contains("只允许删除当前 Group 资产中的子资产", guard);
        }

        [Test]
        public void SoDataInfoPersistenceRollsBackDictionaryAndSubAssetOnFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void PersistStandardInfoCandidate", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1800, source.Length - method));
            StringAssert.Contains("targetGroup._TryAddInfoToDic", body);
            StringAssert.Contains("targetGroup.NotContainsInfoKey(key)", body);
            StringAssert.Contains("targetGroup.GetSOInfoType()", body);
            StringAssert.Contains("ReferenceEquals(targetGroup.GetInfoByKey(key), candidate)", body);
            StringAssert.Contains("AssetDatabase.AddObjectToAsset", body);
            StringAssert.Contains("targetGroup._RemoveInfoFromDic", body);
            StringAssert.Contains("AssetDatabase.RemoveObjectFromAsset", body);
            StringAssert.Contains("DestroyImmediate(candidate, true)", body);
        }

        [Test]
        public void SoDataEditDeleteValidatesParentOwnershipAndRejectsPersistentMainAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void DeleteThis()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int refresh = source.IndexOf("AssetDatabase.Refresh();", method, StringComparison.Ordinal);
            Assert.Greater(refresh, method);
            string body = source.Substring(method, refresh - method);
            StringAssert.Contains("AssetDatabase.GetAssetPath(parentGroupAsset)", body);
            StringAssert.Contains("AssetDatabase.GetAssetPath(dataSO)", body);
            StringAssert.Contains("AssetDatabase.IsSubAsset(dataSO)", body);
            StringAssert.Contains("AssetDatabase.IsPersistent(dataSO)", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(parentGroup as ScriptableObject)", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void SoDataCheckRejectsCrossAssetReferencesBeforeMutatingInfo()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Check()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int keyMutation = source.IndexOf("so.SetKey(i);", method, StringComparison.Ordinal);
            Assert.Greater(keyMutation, method);
            string beforeMutation = source.Substring(method, keyMutation - method);
            StringAssert.Contains("AssetDatabase.GetAssetPath(so_)", beforeMutation);
            StringAssert.Contains("AssetDatabase.IsSubAsset(so_)", beforeMutation);
            StringAssert.Contains("ToRemove.Add(i)", beforeMutation);
            StringAssert.Contains("跨资产或主资产引用", beforeMutation);
        }

        [Test]
        public void SoDataCollectRequiresExactSubAssetOwnershipAndType()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Collect()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int add = source.IndexOf("group._TryAddInfoToDic(i.GetKey(), obd);", method, StringComparison.Ordinal);
            Assert.Greater(add, method);
            string guard = source.Substring(method, add - method);
            StringAssert.Contains("string.Equals(soPath, groupPath, StringComparison.Ordinal)", guard);
            StringAssert.Contains("AssetDatabase.IsSubAsset(obd)", guard);
            StringAssert.Contains("infoType.IsAssignableFrom(obd.GetType())", guard);
            StringAssert.Contains("string.IsNullOrWhiteSpace(i.GetKey())", guard);
            StringAssert.DoesNotContain("soPath.StartsWith(groupPath", guard);
        }

        [Test]
        public void SoDataCheckRejectsInternalKeyCollisionBeforeRewritingKey()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Check()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int setKey = source.IndexOf("so.SetKey(i);", method, StringComparison.Ordinal);
            Assert.Greater(setKey, method);
            string beforeSetKey = source.Substring(method, setKey - method);
            StringAssert.Contains("string internalKey = so.GetKey();", beforeSetKey);
            StringAssert.Contains("group.GetInfoByKey(internalKey)", beforeSetKey);
            StringAssert.Contains("!ReferenceEquals(conflicting, so)", beforeSetKey);
            StringAssert.Contains("Info Key 冲突", beforeSetKey);
        }

        [Test]
        public void SoDataGroupWriteApiRejectsInvalidInputsAndHandlesNullDictionary()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "0_Stand", "BaseDefine_ValueType",
                "SO", "PackGroupInfo", "1-SoDataGroup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void _TryAddInfoToDic", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int end = source.IndexOf("public bool NotContainsInfoKey", method, StringComparison.Ordinal);
            Assert.Greater(end, method);
            string body = source.Substring(method, end - method);
            StringAssert.Contains("if (Infos == null)", body);
            StringAssert.Contains("string.IsNullOrWhiteSpace(s)", body);
            StringAssert.Contains("o == null", body);
            StringAssert.Contains("Info 类型不匹配", body);
        }

        [Test]
        public void SoDataGroupBatchQueriesGuardNullInputsAndDictionary()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "0_Stand", "BaseDefine_ValueType",
                "SO", "PackGroupInfo", "1-SoDataGroup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (Infos == null) return;", source);
            StringAssert.Contains("if (Infos == null || predicate == null)", source);
            StringAssert.Contains("if (Infos == null || keys == null)", source);
            StringAssert.Contains("if (string.IsNullOrWhiteSpace(key)) continue;", source);
        }

        [Test]
        public void SkillAssetCreationRollsBackWhenGroupRejectsCandidate()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESTrackView",
                "-TrackView-Define", "ESCreateSkillWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int create = source.IndexOf("AssetDatabase.CreateAsset(skill, assetPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            int close = source.IndexOf("catch (Exception exception)", create, StringComparison.Ordinal);
            Assert.Greater(close, create);
            string body = source.Substring(create, Math.Min(1900, source.Length - create));
            StringAssert.Contains("targetGroup.GetInfoByKey(safeKey)", body);
            StringAssert.Contains("targetGroup._RemoveInfoFromDic(safeKey)", body);
            StringAssert.Contains("bool groupLinked = false", body);
            StringAssert.Contains("AssetDatabase.DeleteAsset(assetPath)", body);
            StringAssert.Contains("已回滚本次创建", body);
        }

        [Test]
        public void GameCoreRegistrationPreflightsPersistentGroupKeyAndType()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESContentRegistration",
                "ESGameCoreRegistrationAuthoring.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static bool TryValidateGroupMembership", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int existing = source.IndexOf("ISoDataInfo existing", method, StringComparison.Ordinal);
            Assert.Greater(existing, method);
            string guard = source.Substring(method, existing - method);
            StringAssert.Contains("string.IsNullOrWhiteSpace(groupKey)", guard);
            StringAssert.Contains("AssetDatabase.GetAssetPath(targetGroupAsset)", guard);
            StringAssert.Contains("AssetDatabase.GetAssetPath(source)", guard);
            StringAssert.Contains("infoType.IsAssignableFrom(source.GetType())", guard);
        }

        [Test]
        public void UiWhiteSpriteSubAssetAttachmentRollsBackOnFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int firstAttach = source.IndexOf("AssetDatabase.AddObjectToAsset(sprite, existingTexture);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstAttach, 0);
            string firstBody = source.Substring(Math.Max(0, firstAttach - 220), Math.Min(620, source.Length - Math.Max(0, firstAttach - 220)));
            StringAssert.Contains("catch", firstBody);
            StringAssert.Contains("DestroyImmediate(sprite)", firstBody);
            int secondAttach = source.IndexOf("AssetDatabase.AddObjectToAsset(sprite, texture);", StringComparison.Ordinal);
            Assert.Greater(secondAttach, firstAttach);
            string secondBody = source.Substring(Math.Max(0, secondAttach - 180), Math.Min(820, source.Length - Math.Max(0, secondAttach - 180)));
            StringAssert.Contains("AssetDatabase.DeleteAsset(texturePath)", secondBody);
            StringAssert.Contains("DestroyImmediate(sprite)", secondBody);
        }

        [Test]
        public void UiShowcaseFontRebuildRollsBackFontAtlasAndMaterialTogether()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void RebuildCompositeShaderShowcaseFont", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int end = source.IndexOf("public static void OpenCompositeShaderUICasesScene", method, StringComparison.Ordinal);
            Assert.Greater(end, method);
            string body = source.Substring(method, end - method);
            StringAssert.Contains("bool fontAssetCreated = false", body);
            StringAssert.Contains("bool materialAssetCreated = false", body);
            StringAssert.Contains("temporaryFontPath", body);
            StringAssert.Contains("backupFontPath", body);
            StringAssert.Contains("BuildSwapAssetPath", body);
            StringAssert.Contains(".asset", body);
            StringAssert.Contains("AssetDatabase.MoveAsset", body);
            StringAssert.Contains("AssetDatabase.DeleteAsset(ShowcaseFontMaterialPath)", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(font)", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(material)", body);
            StringAssert.Contains("旧字体备份未能清理", body);
            StringAssert.Contains("旧资产恢复失败", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void SoTableInfoImportAndDeleteEnforceOwnershipAndRollback()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "0_Stand", "BaseDefine_ValueType",
                "SO", "SoTable", "EditorOnly", "InfoType", "ESSoTableDataRule.GroupInfo.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int add = source.IndexOf("AssetDatabase.AddObjectToAsset(infoAsset, groupAssetPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(add, 0);
            string createBody = source.Substring(add, Math.Min(1100, source.Length - add));
            StringAssert.Contains("targetGroup.GetInfoByKey(infoKey)", createBody);
            StringAssert.Contains("AssetDatabase.RemoveObjectFromAsset(infoAsset)", createBody);
            StringAssert.Contains("DestroyImmediate(infoAsset, true)", createBody);
            int delete = source.IndexOf("private bool TryDeleteInfoFromGroup", StringComparison.Ordinal);
            Assert.Greater(delete, add);
            string deleteBody = source.Substring(delete, Math.Min(1000, source.Length - delete));
            StringAssert.Contains("AssetDatabase.GetAssetPath(infoAsset)", deleteBody);
            StringAssert.Contains("AssetDatabase.IsSubAsset(infoAsset)", deleteBody);
        }

        [Test]
        public void SoTableDeleteGroupRejectsGameCoreAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "0_Stand", "BaseDefine_ValueType",
                "SO", "SoTable", "EditorOnly", "InfoType", "ESSoTableDataRule.GroupInfo.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private bool TryDeleteGroupAsset", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int pathRead = source.IndexOf("string groupAssetPath", method, StringComparison.Ordinal);
            Assert.Greater(pathRead, method);
            string guard = source.Substring(method, pathRead - method);
            StringAssert.Contains("EScriptableObjectClassification.GetClass(groupAsset)", guard);
            StringAssert.Contains("ESSoTable 规则直接删除", guard);
        }

        [Test]
        public void LongBarPrefabUpgradeBacksUpAndRestoresFormalPrefab()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "WeaponTemplates", "ESLongBarMeleeWeaponBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int replace = source.IndexOf("FileUtil.ReplaceFile(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(replace, 0);
            string body = source.Substring(Math.Max(0, replace - 900), Math.Min(2400, source.Length - Math.Max(0, replace - 900)));
            StringAssert.Contains("backupPath", body);
            StringAssert.Contains("Path.GetTempPath()", body);
            StringAssert.Contains("File.Copy", body);
            StringAssert.Contains("replacementCommitted", body);
            StringAssert.Contains("backupRecoveryFailed", body);
            StringAssert.Contains("旧文件恢复", body);
            StringAssert.Contains("LoadMainAssetAtPath(rebuildPath)", body);
            StringAssert.Contains("catch (Exception cleanupException)", body);
            StringAssert.Contains("临时备份未能清理", body);
        }

        [Test]
        public void GameCoreGlobalDataCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "GameCoreEditorGlobalDataMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(created, path);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(created)", source);
            StringAssert.Contains("DestroyImmediate(created)", source);
        }

        [Test]
        public void StateMachineConfigCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "StateMachineConfigEditorMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(config, assetPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(config)", source);
            StringAssert.Contains("DestroyImmediate(config)", source);
        }

        [Test]
        public void InputConfigCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Input", "ESInputConfigAssetMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(target, DefaultAssetPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(target)", source);
            StringAssert.Contains("DestroyImmediate(target)", source);
        }

        [Test]
        public void GlobalThemeCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESPresentation", "ESGlobalEditorThemeMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(theme, ThemeAssetPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(theme)", source);
            StringAssert.Contains("DestroyImmediate(theme)", source);
        }

        [Test]
        public void FontRuntimeCatalogCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(catalog, catalogPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(catalog)", source);
            StringAssert.Contains("DestroyImmediate(catalog)", source);
        }

        [Test]
        public void LocalizationCatalogCreatorReclaimsUncommittedAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(catalog, CatalogPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(catalog)", source);
            StringAssert.Contains("DestroyImmediate(catalog)", source);
        }

        [Test]
        public void FontGeneratedAssetReclaimsUncommittedFontOnCreateFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(fontAsset, assetPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(fontAsset)", source);
            StringAssert.Contains("DestroyImmediate(fontAsset, true)", source);
        }

        [Test]
        public void FontReplacementAttachesNewSubAssetsBeforeRemovingOldOnes()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorJsonUtility.ToJson(existing)", source);
            StringAssert.Contains("AttachGeneratedSubAssets(storedAsset)", source);
            StringAssert.Contains("EditorJsonUtility.FromJsonOverwrite(existingSnapshot, existing)", source);
            StringAssert.Contains("IsGeneratedSubAsset(storedAsset, subAsset)", source);
        }

        [Test]
        public void FontSubAssetAttachmentRollsBackPartialAdds()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void AttachGeneratedSubAssets", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1800, source.Length - method));
            StringAssert.Contains("var attached = new List<UnityEngine.Object>();", body);
            StringAssert.Contains("AssetDatabase.RemoveObjectFromAsset(subAsset)", body);
            StringAssert.Contains("DestroyImmediate(subAsset, true)", body);
            StringAssert.Contains("throw;", body);
        }

        [Test]
        public void FontSnapshotRestoreEnforcesAbsoluteAssetsBoundary()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static string GetProjectAssetAbsolutePath", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(950, source.Length - method));
            StringAssert.Contains("Path.GetFullPath", body);
            StringAssert.Contains("Application.dataPath", body);
            StringAssert.Contains("absolutePath.StartsWith(assetsPrefix", body);
            StringAssert.Contains("越出项目 Assets/ 根目录", body);
        }

        [Test]
        public void FontSnapshotVerificationUsesStreamingFileHash()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools", "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int helper = source.IndexOf("private static string ComputeSha256File", StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            string body = source.Substring(helper, Math.Min(650, source.Length - helper));
            StringAssert.Contains("File.OpenRead(path)", body);
            StringAssert.Contains("hash.ComputeHash(stream)", body);
            int restore = source.IndexOf("private static Exception RestoreSnapshot", StringComparison.Ordinal);
            Assert.GreaterOrEqual(restore, 0);
            string restoreBody = source.Substring(restore, Math.Min(2500, source.Length - restore));
            StringAssert.Contains("ComputeSha256File(absolutePath)", restoreBody);
            StringAssert.Contains("ComputeSha256File(metaPath)", restoreBody);
        }

        [Test]
        public void WeaponBuilderReclaimsDefinitionAndGroupOnAssetCommitFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "WeaponTemplates", "ESLongBarMeleeWeaponBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(info, WeaponInfoPath);", source);
            StringAssert.Contains("AssetDatabase.CreateAsset(group, WeaponGroupPath);", source);
            StringAssert.Contains("DestroyImmediate(info)", source);
            StringAssert.Contains("DestroyImmediate(group)", source);
        }

        [Test]
        public void ItemPrefabAuthoringReclaimsDefinitionBeforeAssetCommit()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "WeaponTemplates", "ESItemPrefabAuthoring.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(definition, request.definitionPath);", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(definition)", source);
            StringAssert.Contains("DestroyImmediate(definition)", source);
        }

        [Test]
        public void LevelValidationGeneratorReclaimsTemporaryPrefabOnSaveFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESLevelAssetValidationGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);", source);
            StringAssert.Contains("DestroyImmediate(temporary);", source);
            StringAssert.Contains("验收 Prefab 保存失败", source);
        }

        [Test]
        public void LevelValidationGeneratorReclaimsMaterialAndGameCoreOnAssetFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESLevelAssetValidationGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.GetAssetPath(material)", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(gameCore)", source);
            StringAssert.Contains("DestroyImmediate(material)", source);
            StringAssert.Contains("DestroyImmediate(gameCore)", source);
        }

        [Test]
        public void LevelValidationGeneratorReclaimsNewPlanLibraryAndConsumerOnAssetFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESLevelAssetValidationGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(plan, path);", source);
            StringAssert.Contains("AssetDatabase.CreateAsset(library, path);", source);
            StringAssert.Contains("AssetDatabase.CreateAsset(consumer, path);", source);
            StringAssert.Contains("DestroyImmediate(plan)", source);
            StringAssert.Contains("DestroyImmediate(library)", source);
            StringAssert.Contains("DestroyImmediate(consumer)", source);
        }

        [Test]
        public void EditorAssetCreationEntrypointsReclaimUncommittedObjectsOnCreateFailure()
        {
            string soTablePath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESSoTable", "ESSoTableDataRuleAssetMenu.cs");
            string uploadSettingsPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetReleaseUploadSettings.cs");
            string dataInfoPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string soTable = File.ReadAllText(soTablePath, new UTF8Encoding(false, true));
            string uploadSettings = File.ReadAllText(uploadSettingsPath, new UTF8Encoding(false, true));
            string dataInfo = File.ReadAllText(dataInfoPath, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(target, DefaultAssetPath);", soTable);
            StringAssert.Contains("DestroyImmediate(target)", soTable);
            StringAssert.Contains("AssetDatabase.CreateAsset(settings, AssetPath);", uploadSettings);
            StringAssert.Contains("DestroyImmediate(settings)", uploadSettings);
            StringAssert.Contains("AssetDatabase.CreateAsset(candidate, candidatePath);", dataInfo);
            StringAssert.Contains("DestroyImmediate(candidate)", dataInfo);
        }

        [Test]
        public void AssetFlowTestSceneGeneratorPreservesCurrentSceneSetup()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetFlowTestSceneGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("NewSceneMode.Additive", source);
            StringAssert.Contains("SceneManager.MoveGameObjectToScene(root, scene)", source);
            StringAssert.Contains("if (!EditorSceneManager.SaveScene(scene, ScenePath))", source);
            StringAssert.Contains("EditorSceneManager.CloseScene(scene, true)", source);
            StringAssert.DoesNotContain("NewSceneMode.Single", source);
        }

        [Test]
        public void WorldMapTerrainBakeUsesScopedAdditiveSceneAndChecksSaveResult()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldMapTerrainEditorFacade.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("OpenSceneMode.Additive", source);
            StringAssert.Contains("NewSceneMode.Additive", source);
            StringAssert.Contains("SceneManager.GetSceneByPath(scenePath)", source);
            StringAssert.Contains("throw new InvalidOperationException(\"地形场景保存失败：\" + scenePath)", source);
            StringAssert.Contains("EditorSceneManager.CloseScene(scene, true)", source);
            StringAssert.DoesNotContain("OpenSceneMode.Single", source);
        }

        [Test]
        public void UiFixtureMaterializerChecksBothSceneSaveBoundaries()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("UI Fixture Scene 初次保存失败", source);
            StringAssert.Contains("UI Fixture Scene 最终保存失败", source);
            StringAssert.Contains("EditorSceneManager.CloseScene(fixture, true)", source);
        }

        [Test]
        public void UiFixtureCaptureUsesSharedPreviewFoundation()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("new ESEditorPreviewRenderContext(", source);
            StringAssert.Contains("ESEditorPreviewUtility.CreateRenderTexture(", source);
            StringAssert.Contains("ESEditorPreviewUtility.SetLayerRecursive", source);
            StringAssert.DoesNotContain("new RenderTexture(", source);
            StringAssert.DoesNotContain("new GameObject(\"UI_Fixture_Camera\"", source);
        }

        [Test]
        public void WorldContentHashSnapshotsAlwaysReleaseTemporaryAssets()
        {
            string graphPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueAuthoringUtility.cs");
            string mapPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldMapAuthoringUtility.cs");
            string graph = File.ReadAllText(graphPath, new UTF8Encoding(false, true));
            string map = File.ReadAllText(mapPath, new UTF8Encoding(false, true));
            StringAssert.Contains("finally", graph);
            StringAssert.Contains("DestroyImmediate(snapshot)", graph);
            StringAssert.Contains("finally", map);
            StringAssert.Contains("DestroyImmediate(snapshot)", map);
        }

        [Test]
        public void WorldWorkbenchDoesNotSaveSceneAfterAssetFailure()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int assetFailure = source.IndexOf("if (!graphResult.success || !mapResult.success)", StringComparison.Ordinal);
            int sceneSave = source.IndexOf("EditorSceneManager.SaveScene(active)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(assetFailure, 0);
            Assert.Greater(sceneSave, assetFailure);
            StringAssert.Contains("当前场景未能保存", source);
            StringAssert.Contains("当前场景保存失败：", source);
        }

        [Test]
        public void WorldSceneAnchorSyncFiltersForeignAndDuplicateIdentities()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("currentMapGuid", source);
            StringAssert.Contains("skippedForeign", source);
            StringAssert.Contains("seenPlacementIds", source);
            StringAssert.Contains("skippedDuplicate", source);
            StringAssert.Contains("跳过其他地图", source);
        }

        [Test]
        public void StableTagDrawerClosesGuiScopeAndRejectsStaleCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESTagStableReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("finally", source);
            StringAssert.Contains("EditorGUI.EndProperty();", source);
            StringAssert.Contains("serializedObject.targetObject == null", source);
            StringAssert.Contains("string.IsNullOrEmpty(propertyPath)", source);
        }

        [Test]
        public void AssetConfigKeyPopupRejectsStaleSerializedObjectCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESAssetConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("property.serializedObject.targetObject == null", source);
            StringAssert.Contains("public static void ShowMenu", source);
            StringAssert.Contains("private static void ClearCandidate", source);
            StringAssert.Contains("if (property == null || property.serializedObject == null", source);
            StringAssert.Contains("TryPrepareMutation", source);
            StringAssert.Contains("取消资源配置写回", source);
        }

        [Test]
        public void AssetConfigKeyDrawerDoesNotSynchronizeDuringInspectorRepaint()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESAssetConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasStaleBoundKey", source);
            StringAssert.Contains("同步绑定 Key", source);
            StringAssert.Contains("不会在 Inspector 重绘时自动写入", source);
            StringAssert.DoesNotContain("SynchronizeBoundKey(property, current)", source);
        }

        [Test]
        public void AssetConfigKeySubAssetResolutionDoesNotFallBackToMainAsset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESAssetConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int resolve = source.IndexOf("public static UnityEngine.Object ResolveAsset", StringComparison.Ordinal);
            int invalidate = source.IndexOf("public static void Invalidate", resolve, StringComparison.Ordinal);
            Assert.GreaterOrEqual(resolve, 0);
            Assert.GreaterOrEqual(invalidate, resolve);
            string method = source.Substring(resolve, invalidate - resolve);
            StringAssert.Contains("if (localFileId == 0)", method);
            StringAssert.Contains("return null;", method);
            StringAssert.DoesNotContain("return AssetDatabase.LoadMainAssetAtPath(path);", method);
        }

        [Test]
        public void ConfigKeyDrawerSchemaChecksReuseStaticFieldNames()
        {
            string assetPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESAssetConfigKeyDrawer.cs");
            string gameCorePath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESGameCoreConfigKeyDrawer.cs");
            string assetSource = File.ReadAllText(assetPath, new UTF8Encoding(false, true));
            string gameCoreSource = File.ReadAllText(gameCorePath, new UTF8Encoding(false, true));
            StringAssert.Contains("private static readonly string[] RequiredPropertyNames", assetSource);
            StringAssert.Contains("private static readonly string[] RequiredPropertyNames", gameCoreSource);
            StringAssert.DoesNotContain("string[] requiredNames =", assetSource);
            StringAssert.DoesNotContain("string[] requiredNames =", gameCoreSource);
        }

        [Test]
        public void GameCoreConfigKeyPopupRejectsStaleSerializedObjectCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ShowStringKeyMenu", source);
            StringAssert.Contains("property.serializedObject.targetObject == null", source);
            StringAssert.Contains("candidate == null", source);
            StringAssert.Contains("TryPrepareMutation", source);
            StringAssert.Contains("目标或序列化字段已失效，取消写回", source);
        }

        [Test]
        public void InputActionImporterRejectsDestroyedTargetBeforeMutation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputActionDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool HasLiveTarget()", source);
            StringAssert.Contains("targetObject.targetObjects", source);
            StringAssert.Contains("Undo.RecordObjects", source);
            StringAssert.Contains("if (!HasLiveTarget())", source);
            StringAssert.Contains("if (!HasLiveTarget() || holder?.action == null)", source);
            StringAssert.Contains("初始化失败，已回收临时对象", source);
            StringAssert.Contains("catch (ExitGUIException)", source);
            StringAssert.Contains("临时 InputAction 已失效，窗口将安全关闭", source);
            StringAssert.Contains("InputAction rebuiltAction = null", source);
            StringAssert.Contains("已保留旧状态", source);
        }

        [Test]
        public void InputBindingImporterRejectsDestroyedTargetBeforeMutation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputBindingDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool HasLiveTarget()", source);
            StringAssert.Contains("HasLiveSerializedTarget(targetObject, bindingPropertyPath)", source);
            StringAssert.Contains("Undo.RecordObjects", source);
            StringAssert.Contains("if (!HasLiveTarget() || holder == null || holderObject == null)", source);
            StringAssert.Contains("if (!HasLiveTarget())", source);
            StringAssert.Contains("初始化失败，已回收临时对象", source);
            StringAssert.Contains("catch (ExitGUIException)", source);
            StringAssert.Contains("绑定导入窗口绘制失败", source);
            StringAssert.Contains("绑定导入失败，未继续关闭窗口", source);
        }

        [Test]
        public void InputControlPopupRejectsDestroyedSerializedTarget()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputBindingDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasLiveSerializedTarget(serializedObject, propertyPath)", source);
            StringAssert.Contains("private static bool HasLiveSerializedTarget", source);
            StringAssert.Contains("serializedObject.targetObjects", source);
        }

        [Test]
        public void CollectionDrawerRejectsDestroyedTargetsBeforeSerialization()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESCollectionDrawStyleAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("target == null", source);
            StringAssert.Contains("new SerializedObject(target)", source);
            StringAssert.Contains("无法建立序列化视图", source);
            StringAssert.Contains("DisposeCollectionTargets(targets)", source);
            StringAssert.Contains("查找集合属性失败", source);
        }

        [Test]
        public void MultiTargetSerializedMutationDeclaresFailureCleanupOwnership()
        {
            string mutationPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESEditorSerializedMutation.cs");
            string collectionPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESCollectionDrawStyleAttributeDrawer.cs");
            string polymorphicPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESPolymorphicReferenceDrawer.cs");
            string mutation = File.ReadAllText(mutationPath, new UTF8Encoding(false, true));
            string collection = File.ReadAllText(collectionPath, new UTF8Encoding(false, true));
            string polymorphic = File.ReadAllText(polymorphicPath, new UTF8Encoding(false, true));
            StringAssert.Contains("DisposeSerializedObjects(serializedObjects)", mutation);
            StringAssert.Contains("DisposeCollectionTargets(targets)", collection);
            StringAssert.Contains("finally", collection);
            StringAssert.Contains("assignments[index].SerializedObject?.Dispose()", polymorphic);
        }

        [Test]
        public void TwoPaneListMutationRejectsInvalidSerializedProperty()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESTwoPaneListAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private static bool HasLiveProperty", source);
            StringAssert.Contains("property.serializedObject.targetObject == null", source);
            StringAssert.Contains("if (!HasLiveProperty(property))", source);
        }

        [Test]
        public void TwoPaneListStateCachesHaveBoundedCapacity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESTwoPaneListAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("MaxStateEntries = 256", source);
            StringAssert.Contains("SetBounded", source);
            StringAssert.Contains("table.Count <= MaxStateEntries", source);
        }

        [Test]
        public void EnumStringTableMultiEditGuardsSerializedObjectCreation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "AttributeDrawers",
                "ESEnumStringTableAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("new SerializedObject(target)", source);
            StringAssert.Contains("无法建立目标的序列化视图", source);
            StringAssert.Contains("UpdateIfRequiredOrScript()", source);
        }

        [Test]
        public void CommandEventDrawerMutationsDeclareUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESCommand",
                "ESCommandEventDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObjects(targets, \"清空命令事件\")", source);
            StringAssert.Contains("Undo.RecordObjects(targets, \"删除命令事件\")", source);
            StringAssert.Contains("Undo.RecordObjects(targets, \"添加命令事件\")", source);
            StringAssert.Contains("serializedObject.FindProperty(data.propertyPath)", source);
            StringAssert.Contains("targets[i] == null", source);
            StringAssert.Contains("添加命令失败，目标可能已失效", source);
            StringAssert.Contains("多对象目标已失效，取消结构修改", source);
        }

        [Test]
        public void LevelValidationGeneratorDeclaresAssetUndoBoundaries()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESLevelAssetValidationGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(material", source);
            StringAssert.Contains("Undo.RecordObject(material", source);
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(gameCore", source);
            StringAssert.Contains("Undo.RecordObject(gameCore", source);
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(plan", source);
            StringAssert.Contains("Undo.RecordObject(plan", source);
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(library", source);
            StringAssert.Contains("Undo.RecordObject(library", source);
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(consumer", source);
            StringAssert.Contains("Undo.RecordObject(consumer", source);
        }

        [Test]
        public void ResourceRuntimeAcceptanceMarksTemporaryNoUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "Windows",
                "ESResourceRuntimeMonitorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ES-EDITOR-VALIDATOR: intentional-no-undo", source);
            StringAssert.Contains("ApplyModifiedPropertiesWithoutUndo", source);
            StringAssert.Contains("ESResourcePlanBinder_Acceptance_", source);
        }

        [Test]
        public void ResourceRuntimeAsyncOperationsRespectWindowGeneration()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "Windows",
                "ESResourceRuntimeMonitorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private int operationGeneration;", source);
            StringAssert.Contains("operationGeneration++;", source);
            StringAssert.Contains("EnsureOperationActive(generation);", source);
            StringAssert.Contains("generation == operationGeneration", source);
            StringAssert.Contains("catch (OperationCanceledException)", source);
        }

        [Test]
        public void ResourceRuntimeErrorsDoNotWriteAfterWindowGenerationChanges()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "Windows",
                "ESResourceRuntimeMonitorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("evidence.AppendLine(\"[FAIL] \" + exception)", source);
            StringAssert.Contains("lastOperationResult = \"[FAIL] 增量安全点失败", source);
            StringAssert.Contains("lastOperationResult = \"[FAIL] 全量安全点失败", source);
            Assert.GreaterOrEqual(
                source.Split(new[] { "if (this != null && generation == operationGeneration)" }, StringSplitOptions.None).Length - 1,
                6);
        }

        [Test]
        public void EditorSupportSerializationBoundariesDeclareUndoOrFixtureScope()
        {
            string cameraPath = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "Camera",
                "ESCameraDefinitionReferenceDrawer.cs");
            string physicsPath = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "GameCorePhysicsLayerSettings.cs");
            string profilerPath = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "WeaponTemplates",
                "ESWeaponShotProfilerSceneBuilder.cs");
            string cameraSource = File.ReadAllText(cameraPath, new UTF8Encoding(false, true));
            string physicsSource = File.ReadAllText(physicsPath, new UTF8Encoding(false, true));
            string profilerSource = File.ReadAllText(profilerPath, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObjects(serializedObject.targetObjects", cameraSource);
            StringAssert.Contains("targets[i] == null", cameraSource);
            StringAssert.Contains("UpdateIfRequiredOrScript()", cameraSource);
            StringAssert.Contains("写回引用失败，目标可能已失效", cameraSource);
            StringAssert.Contains("Undo.RecordObject(tagManager.targetObject", physicsSource);
            StringAssert.Contains("ES-EDITOR-VALIDATOR: intentional-no-undo", profilerSource);
        }

        [Test]
        public void InstallerAsyncStatusWritesRequireActiveWindow()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (!IsActiveInstaller()) return;", source);
            StringAssert.Contains("if (!IsActiveInstaller()) return;", source);
            StringAssert.Contains("private bool IsActiveInstaller()", source);
            StringAssert.Contains("if (!IsActiveInstaller()) return;\n            statusMessage", source);
        }

        [Test]
        public void ResourcePipelineWindowsDeclareMaximumSizeBoundary()
        {
            string[] paths =
            {
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "Windows", "ESResourceRuntimeMonitorWindow.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetReleaseUploadWindow.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESResourceCollectionWorkflowWindow.cs")
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i], new UTF8Encoding(false, true));
                StringAssert.Contains("maxSize = new Vector2(1400f, 1000f)", source);
            }
        }

        [Test]
        public void PreviewAndMonitorWindowsDeclareMaximumSizeBoundary()
        {
            string[] paths =
            {
                Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Preview", "ESAudioCueTrimPreviewWindow.cs"),
                Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "DynamicAtlas", "ESDynamicAtlasMonitorWindow.cs")
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i], new UTF8Encoding(false, true));
                StringAssert.Contains("maxSize = new Vector2(1400f, 1000f)", source);
            }
        }

        [Test]
        public void CommonEditorWindowsDeclareMaximumSizeBoundary()
        {
            var expectations = new Dictionary<string, string>
            {
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESDeveloperCockpit", "ESDeveloperCockpitWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESCmdAgent", "ESCmdAgentWindow.cs")] = "maxSize = new Vector2(1600f, 1100f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESShader", "ESCompositeShaderBakeWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2", "ESStableGraphViewWindow.cs")] = "maxSize = new Vector2(1800f, 1200f)"
            };
            foreach (KeyValuePair<string, string> item in expectations)
            {
                string source = File.ReadAllText(item.Key, new UTF8Encoding(false, true));
                StringAssert.Contains(item.Value, source);
            }
        }

        [Test]
        public void CameraUiAndEntityWindowsDeclareMaximumSizeBoundary()
        {
            var expectations = new Dictionary<string, string>
            {
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Camera", "ESCameraTrackPreviewWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI", "ESUIRiskAuditWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "EntityStatDebugWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "EntityBasicInteractionDebugWindow.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs")] = "maxSize = new Vector2(1600f, 1100f)",
                [Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World", "ESWorldMapSpaceEditorWindow.cs")] = "maxSize = new Vector2(1600f, 1100f)"
            };
            foreach (KeyValuePair<string, string> item in expectations)
            {
                string source = File.ReadAllText(item.Key, new UTF8Encoding(false, true));
                StringAssert.Contains(item.Value, source);
            }
        }

        [Test]
        public void WorkbenchInstanceSessionStateIsClearedOnlyOnDestroy()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchWindowBase.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("protected override void OnDestroy()", source);
            StringAssert.Contains("SessionState.EraseString(AssetGuidPrefix + instanceSuffix);", source);
            StringAssert.Contains("SessionState.EraseString(DocumentPrefix + instanceSuffix);", source);
            StringAssert.Contains("OnDisable\n        /// 不执行此清理", source);
        }

        [Test]
        public void PopupAndSearchWindowSizesNormalizeNonFiniteAndExtremeInputs()
        {
            string popupPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string searchPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string popup = File.ReadAllText(popupPath, new UTF8Encoding(false, true));
            string search = File.ReadAllText(searchPath, new UTF8Encoding(false, true));
            StringAssert.Contains("NormalizePopupSize(size, choices.Count)", popup);
            StringAssert.Contains("!float.IsNaN(value) && !float.IsInfinity(value)", popup);
            StringAssert.Contains("Mathf.Clamp(width, MinimumPopupWidth, MaximumPopupWidth)", popup);
            StringAssert.Contains("NormalizeMinimumWindowSize(minimumWindowSize)", search);
            StringAssert.Contains("MaximumMinimumWidth", search);
            StringAssert.Contains("MaximumMinimumHeight", search);
        }

        [Test]
        public void AutomationAndInstallerWindowsDeclareMaximumSizeBoundary()
        {
            var expectations = new Dictionary<string, string>
            {
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESAutomation", "ESAutomationCenter.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESAutomation", "ESAIBrainCoordinator.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "Installer", "ESInstaller.cs")] = "maxSize = new Vector2(1400, 1000)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESWindowLauncher.cs")] = "maxSize = new Vector2(1400f, 1000f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESTrackView", "-TrackView-Define", "ESCreateSkillWindow.cs")] = "window.maxSize = new Vector2(900f, 600f)",
                [Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2", "ESAgentArtifactGenerationWorkflow.cs")] = "window.maxSize = new Vector2(1800f, 1200f)"
            };
            foreach (KeyValuePair<string, string> item in expectations)
            {
                string source = File.ReadAllText(item.Key, new UTF8Encoding(false, true));
                StringAssert.Contains(item.Value, source);
            }
        }

        [Test]
        public void AssetPackageBakeSavePathsDeclareUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(bake, \"保存资产包索引\")", source);
            StringAssert.Contains("Undo.RecordObject(bake, \"设置资产包分类使用状态\")", source);
            StringAssert.Contains("Undo.RecordObject(state.bake, \"修正资产包导出链路\")", source);
            StringAssert.Contains("Undo.RecordObject(bake, \"标记资产包记录使用状态\")", source);
            StringAssert.Contains("Undo.RecordObject(stateMachineConfig, \"保存状态机预览模型\")", source);
        }

        [Test]
        public void AssetPackageBakeHostDisableReleasesPreviewAndDelayedRepaint()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= RepaintAssetPackageWindow", source);
            StringAssert.Contains("ReleaseInstancePreviewResources();", source);
        }

        [Test]
        public void AssetPackageBakePublicOperationsDeclareUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(data, \"烘焙资产包记录\")", source);
            StringAssert.Contains("Undo.RecordObject(data, \"导出资产包分类内容\")", source);
        }

        [Test]
        public void AssetPackageExportRegistersTransactionItemBeforeCopies()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int registration = source.IndexOf("items.Add(item);", StringComparison.Ordinal);
            int backupCopy = source.IndexOf("AssetDatabase.CopyAsset(exportPlan.targetPath", StringComparison.Ordinal);
            int stagedCopy = source.IndexOf("AssetDatabase.CopyAsset(exportPlan.sourcePath", StringComparison.Ordinal);
            Assert.GreaterOrEqual(registration, 0);
            Assert.GreaterOrEqual(backupCopy, 0);
            Assert.GreaterOrEqual(stagedCopy, 0);
            Assert.Less(registration, backupCopy);
            Assert.Less(registration, stagedCopy);
        }

        [Test]
        public void AssetPackageExportBlocksWhenPreviousTransactionIsUnresolved()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int guard = source.IndexOf("HasUnresolvedExportTransactions(exportRoot", StringComparison.Ordinal);
            int configMutation = source.IndexOf("data.exportRootPath = exportRoot", StringComparison.Ordinal);
            Assert.GreaterOrEqual(guard, 0);
            Assert.GreaterOrEqual(configMutation, 0);
            Assert.Less(guard, configMutation);
            StringAssert.Contains("AssetDatabase.GetSubFolders(transactionRoot)", source);
            StringAssert.Contains("拒绝继续导出", source);
        }

        [Test]
        public void FontBuildOutputFolderUsesProjectAssetPathSafety()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESFontTools",
                "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESAssetPackagePathSafety.TryNormalizeProjectAssetPath", source);
            StringAssert.Contains("不能包含绝对路径或 ..", source);
        }

        [Test]
        public void AssetPackageRollbackRemovesActualSessionAndDeclaresUndo()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("int sessionIndex = data.exportSessions.LastIndexOf(session);", source);
            StringAssert.Contains("Undo.RecordObject(data, \"回退资产包导出\")", source);
            StringAssert.Contains("data.exportSessions.RemoveAt(sessionIndex)", source);
        }

        [Test]
        public void AssetPackageRollbackDerivesLastExportFromCommittedSession()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESAssetPackageExportSession lastCommittedSession = data.exportSessions", source);
            StringAssert.Contains("item.transactionState == ESAssetPackageExportAttemptState.Committed", source);
            StringAssert.Contains("data.lastExportTime = lastCommittedSession != null", source);
        }

        [Test]
        public void SoTableMigrationDeclaresUndoForCreateAndOverwrite()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESSoTable",
                "ESSoTableDataRuleAssetMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(target, \"创建独立 SO 表格规则\")", source);
            StringAssert.Contains("Undo.RecordObject(target, \"迁移旧 SO 表格规则\")", source);
        }

        [Test]
        public void SceneToolbarPreferencesDeclareUndoForAssetBackedToggles()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESEditorToolBar",
                "ESEditorToolBar.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(ESSceneGlobalData.Instance, \"切换场景自动保存\")", source);
            StringAssert.Contains("Undo.RecordObject(ESSceneGlobalData.Instance, \"切换场景叠加模式\")", source);
        }

        [Test]
        public void ReleaseUploadSettingsDeclareUndoForCreateAndEdit()
        {
            string windowPath = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESAssetReleaseUploadWindow.cs");
            string settingsPath = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESAssetReleaseUploadSettings.cs");
            string windowSource = File.ReadAllText(windowPath, new UTF8Encoding(false, true));
            string settingsSource = File.ReadAllText(settingsPath, new UTF8Encoding(false, true));
            StringAssert.Contains("serializedSettings.ApplyModifiedProperties()", windowSource);
            StringAssert.Contains("serializedSettings.UpdateIfRequiredOrScript()", windowSource);
            StringAssert.Contains("远端发布配置字段不完整，已取消本次写回", windowSource);
            StringAssert.Contains("远端发布配置写回失败，已取消本次保存", windowSource);
            StringAssert.Contains("DrawTargetProperty(targetProperty", windowSource);
            StringAssert.Contains("预检未执行：", windowSource);
            StringAssert.Contains("发布未开始：预检失败：", windowSource);
            StringAssert.Contains("settings.target == null", windowSource);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(settings)", windowSource);
            int apply = windowSource.IndexOf("serializedSettings.ApplyModifiedProperties()", StringComparison.Ordinal);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", windowSource.Substring(apply, Math.Min(320, windowSource.Length - apply)));
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(settings, \"创建远端发布配置\")", settingsSource);
        }

        [Test]
        public void ResourceCollectionWorkflowSavesOnlyTheTargetPlan()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESResourceCollectionWorkflowWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int targetPlan = source.IndexOf("EditorUtility.SetDirty(targetPlan)", StringComparison.Ordinal);
            int scopedSave = source.IndexOf("AssetDatabase.SaveAssetIfDirty(targetPlan)", targetPlan, StringComparison.Ordinal);
            Assert.GreaterOrEqual(targetPlan, 0);
            Assert.GreaterOrEqual(scopedSave, 0);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source.Substring(targetPlan, Math.Min(260, source.Length - targetPlan)));
        }

        [Test]
        public void ResourceCollectionWorkflowSkipsInvalidSerializedPlansSafely()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESResourceCollectionWorkflowWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("serialized.UpdateIfRequiredOrScript()", source);
            StringAssert.Contains("ResourcePlan 序列化对象已失效，已跳过本次扫描", source);
            StringAssert.Contains("continue;", source);
            StringAssert.Contains("MaxIntegrityCacheEntries = 2048", source);
            StringAssert.Contains("IntegrityCache.Clear()", source);
        }

        [Test]
        public void ResourceCollectionFiltersUnsupportedPlanKindsBeforeLibraryRegistration()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESResourceCollectionWorkflowWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int filter = source.IndexOf("var supportedAssets = new List<UnityEngine.Object>", StringComparison.Ordinal);
            int collect = source.IndexOf("CollectAssets(unregisteredAssets)", filter, StringComparison.Ordinal);
            int mapping = source.IndexOf("FindPlanField(kind)", filter, StringComparison.Ordinal);
            Assert.GreaterOrEqual(filter, 0);
            Assert.GreaterOrEqual(collect, 0);
            Assert.GreaterOrEqual(mapping, 0);
            Assert.Less(mapping, collect);
            StringAssert.Contains("不支持的资源不能因为一次“收集并加入计划”操作而产生注册副作用。", source);
        }

        [Test]
        public void ResourceCollectionWritesOnlyWhenPlanEntriesWereAdded()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESResourceCollectionWorkflowWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int addedGuard = source.IndexOf("if (added > 0)", StringComparison.Ordinal);
            int setDirty = source.IndexOf("EditorUtility.SetDirty(targetPlan)", addedGuard, StringComparison.Ordinal);
            int save = source.IndexOf("AssetDatabase.SaveAssetIfDirty(targetPlan)", addedGuard, StringComparison.Ordinal);
            Assert.GreaterOrEqual(addedGuard, 0);
            Assert.GreaterOrEqual(setDirty, 0);
            Assert.GreaterOrEqual(save, 0);
            Assert.Less(addedGuard, setDirty);
            Assert.Less(addedGuard, save);
            StringAssert.Contains("bool undoRecorded = false", source);
            StringAssert.Contains("if (!undoRecorded)", source);
        }

        [Test]
        public void CreateSkillWindowSavesOnlyCreatedSkillAndTargetGroup()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESCreateSkillWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(skill);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(targetGroup);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void StableGraphTemplateCreationSavesOnlyCreatedAsset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESGraphViewV2",
                "ESStableGraphViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void StableGraphDelayedRebuildAndFocusAreLifecycleBound()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESGraphViewV2",
                "ESStableGraphViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private IVisualElementScheduledItem focusAfterOpenSchedule;", source);
            StringAssert.Contains("private IVisualElementScheduledItem rebuildSchedule;", source);
            StringAssert.Contains("focusAfterOpenSchedule?.Pause();", source);
            StringAssert.Contains("rebuildSchedule?.Pause();", source);
            StringAssert.Contains("private void ScheduleRebuild()", source);
            StringAssert.DoesNotContain("schedule.Execute(Rebuild).StartingIn(1);", source);
        }

        [Test]
        public void EditorCreationAndRevisionFlowsAvoidGlobalAssetSave()
        {
            string[] paths =
            {
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetReleaseUploadSettings.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline", "ESAssetConsumerBuildRevision.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESPresentation", "ESGlobalEditorThemeMenu.cs")
            };
            string[] required =
            {
                "AssetDatabase.SaveAssetIfDirty(settings);",
                "AssetDatabase.SaveAssetIfDirty(consumer);",
                "AssetDatabase.SaveAssetIfDirty(theme);"
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i], new UTF8Encoding(false, true));
                StringAssert.Contains(required[i], source);
                StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            }
        }

        [Test]
        public void AssetPackageBakeWindowUsesScopedAssetSaves()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(state.bake);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(bake);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(stateMachineConfig);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void SingleTargetEditorMenusAvoidGlobalAssetSave()
        {
            string[] paths =
            {
                Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "GameCorePhysicsLayerSettings.cs"),
                Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "Input", "ESInputConfigAssetMenu.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESSoTable", "ESSoTableDataRuleAssetMenu.cs")
            };
            string[] required =
            {
                "AssetDatabase.SaveAssetIfDirty(data);",
                "AssetDatabase.SaveAssetIfDirty(target);",
                "AssetDatabase.SaveAssetIfDirty(target);"
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i], new UTF8Encoding(false, true));
                StringAssert.Contains(required[i], source);
                StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            }
        }

        [Test]
        public void AssetPackageBakeDataUsesScopedCheckpointSaves()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(data);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(analysis);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void ThemeRestoreSavesOnlyTheThemeAsset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESPresentation",
                "ESGlobalEditorThemeMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int restore = source.IndexOf("Undo.RecordObject(theme, \"恢复 ES 默认编辑器主题\")", StringComparison.Ordinal);
            int scopedSave = source.IndexOf("AssetDatabase.SaveAssetIfDirty(theme)", restore, StringComparison.Ordinal);
            Assert.GreaterOrEqual(restore, 0);
            Assert.GreaterOrEqual(scopedSave, 0);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source.Substring(restore, Math.Min(260, source.Length - restore)));
        }

        [Test]
        public void SceneShortcutOperationsSaveOnlyGlobalSceneData()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SimpleToolsWindow",
                "ESTools",
                "Simple_ESTool_Page_SceneManager.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            Assert.AreEqual(0, System.Text.RegularExpressions.Regex.Matches(source, "AssetDatabase\\.SaveAssets\\(\\)").Count);
            Assert.GreaterOrEqual(
                System.Text.RegularExpressions.Regex.Matches(source, "AssetDatabase\\.SaveAssetIfDirty\\(ESSceneGlobalData\\.Instance\\)").Count,
                8);
        }

        [Test]
        public void SceneShortcutOpenUsesCurrentAssetDatabasePath()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SimpleToolsWindow",
                "ESTools",
                "Simple_ESTool_Page_SceneManager.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("string sceneAssetPath = AssetDatabase.GetAssetPath(scene.SceneAsset);", source);
            StringAssert.Contains("sceneAssetPath.StartsWith(\"Assets/\", StringComparison.Ordinal)", source);
            StringAssert.Contains("EditorSceneManager.OpenScene(sceneAssetPath, mode)", source);
        }

        [Test]
        public void SceneToolbarOpenRejectsUntrustedPersistedPaths()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESEditorToolBar",
                "ESEditorToolBar.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ResolveCanonicalScenePath(scenePath)", source);
            StringAssert.Contains("normalized.StartsWith(\"Assets/\", StringComparison.OrdinalIgnoreCase)", source);
            StringAssert.Contains("AssetDatabase.LoadAssetAtPath<SceneAsset>(normalized)", source);
            StringAssert.Contains("AssetDatabase.GetAssetPath(sceneAsset)", source);
            StringAssert.DoesNotContain("File.Exists(scenePath)", source);
        }

        [Test]
        public void SceneToolbarAssetQuickAccessUsesAssetDatabaseIdentity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESEditorToolBar",
                "ESEditorToolBar.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath)", source);
            StringAssert.Contains("StartsWith(\"Assets/\", StringComparison.OrdinalIgnoreCase)", source);
            int fixedAssets = source.IndexOf("private static void AddFixedAssetEntries", StringComparison.Ordinal);
            int typeAssets = source.IndexOf("private static void AddTypeAssetEntries", StringComparison.Ordinal);
            Assert.GreaterOrEqual(fixedAssets, 0);
            Assert.GreaterOrEqual(typeAssets, 0);
            StringAssert.DoesNotContain("File.Exists(path)", source.Substring(fixedAssets, typeAssets - fixedAssets));
        }

        [Test]
        public void UiEvidenceCaptureDeclaresExceptionSafePreviewCleanup()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Texture2D image = null;", source);
            StringAssert.Contains("finally", source);
            StringAssert.Contains("camera.targetTexture = null", source);
            StringAssert.Contains("RenderTexture.active = previous", source);
            StringAssert.Contains("target.Release()", source);
        }

        [Test]
        public void PreviewSnapshotCleanupDoesNotMaskDestroyedCameraErrors()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int snapshotStart = source.IndexOf("RenderCameraSnapshot", StringComparison.Ordinal);
            Assert.GreaterOrEqual(snapshotStart, 0);
            string snapshot = source.Substring(snapshotStart);
            StringAssert.Contains("if (camera != null)", snapshot);
            StringAssert.Contains("camera.targetTexture = oldTarget", snapshot);
            StringAssert.Contains("RenderTexture.active = oldActive", snapshot);
        }

        [Test]
        public void PreviewTextureCopyReleasesTemporaryOnlyWhenAllocated()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int copyStart = source.IndexOf("CopyTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(copyStart, 0);
            string copy = source.Substring(copyStart, source.IndexOf("RenderCameraSnapshot", copyStart, StringComparison.Ordinal) - copyStart);
            StringAssert.Contains("if (temporary != null)", copy);
            StringAssert.Contains("RenderTexture.ReleaseTemporary(temporary)", copy);
        }

        [Test]
        public void PreviewUtilityRollsBackCreatedObjectsWhenInitializationFails()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int createStart = source.IndexOf("CreatePreviewGameObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(createStart, 0);
            string createBody = source.Substring(createStart, Math.Min(1500, source.Length - createStart));
            StringAssert.Contains("GameObject go = null", createBody);
            StringAssert.Contains("DestroyObject(go)", createBody);
            StringAssert.Contains("bool createdMarker = false", source);
            StringAssert.Contains("if (createdMarker && marker != null)", source);
            StringAssert.Contains("DestroyObject(marker)", source);
        }

        [Test]
        public void PreviewUtilityValidatesLayerBeforeRecursiveMutation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void SetLayerRecursive", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(900, source.Length - method));
            StringAssert.Contains("layer < 0 || layer > 31", body);
            StringAssert.Contains("ArgumentOutOfRangeException", body);
            Assert.Less(body.IndexOf("ArgumentOutOfRangeException", StringComparison.Ordinal), body.IndexOf("hierarchy[i].gameObject.layer", StringComparison.Ordinal));
            StringAssert.Contains("List<Transform> hierarchy = CollectHierarchy(root)", body);
            Assert.Less(body.IndexOf("CollectHierarchy(root)", StringComparison.Ordinal), body.IndexOf("hierarchy[i].gameObject.layer", StringComparison.Ordinal));
        }

        [Test]
        public void CompositeShaderBakeDeclaresPerFramePreviewCleanup()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESShader",
                "ESCompositeShaderBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Texture2D frame = null;", source);
            StringAssert.Contains("if (frame != null)", source);
            StringAssert.Contains("DestroyImmediate(frame)", source);
            StringAssert.Contains("Texture2D result = null;", source);
            StringAssert.Contains("if (result != null)", source);
            StringAssert.Contains("DestroyImmediate(result)", source);
            StringAssert.Contains("if (temporary != null)", source);
        }

        [Test]
        public void PresentationSkinTextureGenerationRollsBackFailedApply()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESPresentation",
                "Core",
                "ESEditorPresentationCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int start = source.IndexOf("private static Texture2D CreateRoundedRectTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private static void ApplyButtonState", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string method = source.Substring(start, end - start);
            StringAssert.Contains("texture.Apply(false, true)", method);
            StringAssert.Contains("catch", method);
            StringAssert.Contains("DestroyImmediate(texture)", method);
        }

        [Test]
        public void WorkbenchPreviewDeclaresFailedInstantiationAndCloseRecovery()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchPreviewScene.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("catch (System.Exception exception)", source);
            StringAssert.Contains("Object.DestroyImmediate(instance)", source);
            StringAssert.Contains("renderContext.Dispose()", source);
            StringAssert.Contains("renderContext.PreviewScene", source);
        }

        [Test]
        public void WorkbenchSerializedInspectorHasObjectInvalidationFallback()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchWindowBase.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int start = source.IndexOf("CreateSerializedInspector", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string method = source.Substring(start);
            StringAssert.Contains("SerializedObject serialized = null", method);
            StringAssert.Contains("catch (Exception exception)", method);
            StringAssert.Contains("serialized.Dispose()", method);
            StringAssert.Contains("Inspector 暂时不可用", method);
        }

        [Test]
        public void SharedPreviewContextDeclaresModelAdoptionRollback()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("GameObject instance = null;", source);
            StringAssert.Contains("return AdoptModelGroup(", source);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(instance);", source);
            StringAssert.Contains("ParticleSystem[] particleSystems", source);
            StringAssert.Contains("catch (Exception exception)", source);
        }

        [Test]
        public void SharedPreviewContextDisposeIsolatesOwnedResourceFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SafeReleaseRenderTexture();", source);
            StringAssert.Contains("SafeDestroyPreviewObject(ref cameraObject);", source);
            StringAssert.Contains("finally { previewScene = default; }", source);
            StringAssert.Contains("private static void SafeDestroyPreviewObject", source);
        }

        [Test]
        public void PreviewResourceScopeIsolatesRegisteredObjectFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewResourceScope.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("for (int i = unityObjects.Count - 1; i >= 0; i--)", source);
            StringAssert.Contains("DestroyRegisteredObject(unityObjects[i]);", source);
            StringAssert.Contains("catch (Exception e)", source);
            StringAssert.Contains("unityObjects.Clear();", source);
        }

        [Test]
        public void PreviewLifecycleHubRetainsFailedScopesForRetry()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private static readonly HashSet<IDisposable> FailedScopes", source);
            StringAssert.Contains("foreach (IDisposable failedScope in FailedScopes)", source);
            StringAssert.Contains("FailedScopes.Add(DisposeBuffer[i])", source);
            StringAssert.Contains("FailedScopes.Remove(scope)", source);
        }

        [Test]
        public void PreviewCameraCreationRollsBackPartialObjects()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("GameObject created = null", source);
            StringAssert.Contains("if (camera == null)", source);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(created)", source);
            StringAssert.Contains("cameraObject = created", source);
        }

        [Test]
        public void PreviewAuxiliaryObjectsRollbackPartialCreation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private void EnsureGroundPlaneCore()", source);
            StringAssert.Contains("private void EnsureScaleReferenceCore()", source);
            StringAssert.Contains("SafeDestroyPreviewObject(ref groundPlaneMaterial)", source);
            StringAssert.Contains("SafeDestroyPreviewObject(ref scaleReferenceMaterial)", source);
        }

        [Test]
        public void PreviewLightCreationRollsBackPartialObjects()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private GameObject CreateLight", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1800, source.Length - method));
            StringAssert.Contains("GameObject created = null", body);
            StringAssert.Contains("if (light == null)", body);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(created)", body);
        }

        [Test]
        public void PreviewFallbackMaterialCommitsOnlyAfterConfiguration()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private Material EnsureFallbackParticleMaterial", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1200, source.Length - method));
            StringAssert.Contains("Material created = null", body);
            StringAssert.Contains("fallbackParticleMaterial = created", body);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(created)", body);
        }

        [Test]
        public void PreviewMarkedObjectCleanupIsolatesDestroyFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("try\n                {\n                    DestroyObject(obj);", source);
            StringAssert.Contains("catch (Exception exception)", source);
            StringAssert.Contains("removed++", source);
        }

        [Test]
        public void CameraTrackPreviewDisposeIsolatesLeaseAndContextFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Camera",
                "ESCameraTrackPreviewFactory.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("try { previewView?.Release(lease); }", source);
            StringAssert.Contains("try { previewView?.Dispose(); }", source);
            StringAssert.Contains("try { renderContext?.Dispose(); }", source);
            StringAssert.Contains("renderContext = null;", source);
        }

        [Test]
        public void EntityPreviewDisposeIsolatesPlayerModelAndScopeFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "0_Stand",
                "BaseDefine_ValueType",
                "Entity",
                "EntityStateDomain.EditorPreview.cs");
            if (!File.Exists(path))
            {
                path = Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "ESLogic",
                    "Runtime",
                    "Entity",
                    "Entity",
                    "Domains",
                    "State",
                    "EntityStateDomain.EditorPreview.cs");
            }
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("try { _previewRenderPlayer.Pause(); }", source);
            StringAssert.Contains("try { _previewModelHandle?.Dispose(); }", source);
            StringAssert.Contains("try { _previewRenderContext?.Dispose(); }", source);
            StringAssert.Contains("try { _previewResourceScope?.Dispose(); }", source);
        }

        [Test]
        public void ParticlePreviewReleaseKeepsStateCleanupAfterContextFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorParticlePreviewSession.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("try { renderContext.Dispose(); }", source);
            StringAssert.Contains("finally { renderContext = null; }", source);
            StringAssert.Contains("modelHandles.Clear();", source);
            StringAssert.Contains("previewSystems.Clear();", source);
        }

        [Test]
        public void UiMaterializerRestoresActiveSceneAfterFixtureCleanupFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorSceneManager.CloseScene(fixture, true)", source);
            StringAssert.Contains("if (previous.IsValid() && previous.isLoaded)", source);
            StringAssert.Contains("SceneManager.SetActiveScene(previous)", source);
            StringAssert.Contains("if (root != null)", source);
            StringAssert.Contains("catch (Exception exception)", source);
        }

        [Test]
        public void WorkbenchHostThumbnailCleanupIsolatesTextureFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SafeDestroyThumbnail(texture);", source);
            StringAssert.Contains("private static void SafeDestroyThumbnail(Texture2D texture)", source);
            StringAssert.Contains("catch (Exception exception)", source);
        }

        [Test]
        public void WorkbenchHostScheduledCallbacksRespectDisposedBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (disposed) return;\n                ApplyPaneVisibility", source);
            StringAssert.Contains("if (disposed || outerSplit == null || contentSplit == null)", source);
            StringAssert.Contains("if (disposed || bottomContent == null) return;", source);
            StringAssert.Contains("if (disposed) return;\n                ObjectRowState currentRow", source);
        }

        [Test]
        public void WorkbenchHostDragCleanupReleasesScheduleAndIsolatesDropPreview()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("dragEdgePanSchedule?.Pause();", source);
            StringAssert.Contains("dragEdgePanSchedule = null;", source);
            StringAssert.Contains("try { previewViewport.ClearDropPreview(); }", source);
        }

        [Test]
        public void WorkbenchHostDisposeResetsContentAndCoordinatedPointerOwnership()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("pointerCoordinator.Reset();", source);
            StringAssert.Contains("contentPointerGate.Reset();", source);
            StringAssert.Contains("if (!disposed) RebuildBottomDrawer();", source);
        }

        [Test]
        public void CompactChoicePopupDelayedFocusRespectsConfigurationAndPanelBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (!configured || rootVisualElement == null || rootVisualElement.panel == null)", source);
            StringAssert.Contains("Option[] choiceSnapshot = new Option[choices.Count]", source);
            StringAssert.Contains("popup.options = choiceSnapshot", source);
        }

        [Test]
        public void CommandPaletteGuardsInactiveTicksAndExecutorFailures()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESCommandPalette",
                "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool lifecycleActive;", source);
            StringAssert.Contains("命令执行器未返回受管结果", source);
            StringAssert.Contains("搜索结果更新失败", source);
            StringAssert.Contains("if (this != null && lifecycleActive)", source);
            StringAssert.Contains("searchEngine.Clear();", source);
            StringAssert.Contains("UnregisterSearchTick();", source);
            StringAssert.Contains("UnregisterShortcutCheckTick();", source);
            StringAssert.Contains("ESDialogService.ShowModal", source,
                "命令面板的快捷键状态反馈必须走 ESDialogService，不能旁路原生对话框生命周期。");
            StringAssert.DoesNotContain("EditorUtility.DisplayDialog", source,
                "命令面板不能直接创建未治理的 EditorUtility 对话框。");
        }

        [Test]
        public void CommandPaletteContextCallbacksIgnoreClosedWindow()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESCommandPalette",
                "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ToggleFavoriteFromContextMenu(item.StableId)", source);
            StringAssert.Contains("if (this == null || !lifecycleActive || item == null)", source);
            StringAssert.Contains("if (this == null || !lifecycleActive || string.IsNullOrEmpty(stableId))", source);
            StringAssert.Contains("if (this == null || !lifecycleActive\n                || selected < 0", source);
        }

        [Test]
        public void SearchDropdownBuilderBoundsEagerEntries()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool entryLimitMarkerAdded;", source);
            StringAssert.Contains("private void AddBounded(Entry entry)", source);
            StringAssert.Contains("entries.Count < MaximumResolvedEntries", source);
            StringAssert.Contains("\"候选项过多，已限制为 \" + MaximumResolvedEntries", source);
        }

        [Test]
        public void CameraDefinitionDrawerInvalidatesCatalogCacheOnProjectChange()
        {
            string path = Path.Combine(
                Application.dataPath, "Scripts", "ESLogic", "Editor", "Camera",
                "ESCameraDefinitionReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.projectChanged += ClearCache", source);
            StringAssert.Contains("private static void ClearCache()", source);
            StringAssert.Contains("cachedByReference.Clear();", source);
            StringAssert.Contains("cachedByString.Clear();", source);
        }

        [Test]
        public void CommandEventDrawerConstructsBeforeArrayMutation()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESCommand",
                "ESCommandEventDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int construct = source.IndexOf("Activator.CreateInstance(data.type)", StringComparison.Ordinal);
            int insert = source.IndexOf("commands.InsertArrayElementAtIndex(index)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(construct, 0);
            Assert.GreaterOrEqual(insert, 0);
            Assert.Less(construct, insert);
            StringAssert.Contains("insertedCommands.DeleteArrayElementAtIndex", source);
        }

        [Test]
        public void TwoPaneListGuardsMultiObjectStructuralMutations()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer",
                "AttributeDrawers", "ESTwoPaneListAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private static bool BeginStructuralMutation", source);
            StringAssert.Contains("if (!BeginStructuralMutation(property", source);
            StringAssert.Contains("if (targets[i] == null)", source);
            StringAssert.Contains("if (!property.MoveArrayElement(from, to))", source);
        }

        [Test]
        public void EnumStringTableGuardsStructuralMutationTargets()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer",
                "AttributeDrawers", "ESEnumStringTableAttributeDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private static bool BeginMutation", source);
            StringAssert.Contains("if (!BeginMutation(table", source);
            StringAssert.Contains("if (!entries.MoveArrayElement(from, to))", source);
            StringAssert.Contains("if (targets[i] == null)", source);
        }

        [Test]
        public void TrackTimerDelayedMenuIgnoresDetachedToolbar()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESTrackView",
                "-TrackView-Define", "ESTrackTimerToolbar.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= ShowTimelineMenuDelayed", source);
            StringAssert.Contains("MoreButton == null || MoreButton.panel == null", source);
        }

        [Test]
        public void AssetPackageBakeCreationCleansFailedAsset()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("bool assetCommitted = false", source);
            StringAssert.Contains("assetCommitted = true", source);
            StringAssert.Contains("AssetDatabase.DeleteAsset(path)", source);
            StringAssert.Contains("DestroyImmediate(bake)", source);
            StringAssert.Contains("创建资产包烘焙数据失败", source);
        }

        [Test]
        public void AssetPackageRollbackRetainsSessionWhenDeletionFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "Data", "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("else\n                        changed++;", source);
            StringAssert.Contains("bool partialRollback = changed > 0", source);
            StringAssert.Contains("RollbackPartial", source);
        }

        [Test]
        public void AssetPackageRollbackHandlesMalformedSessionTargets()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "Data", "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("session.targetAssetPaths?.Count ?? 0", source);
            StringAssert.Contains("session.targetAssetPaths != null", source);
        }

        [Test]
        public void AssetPackageWindowHandlesNullLatestSession()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (last == null)", source);
            StringAssert.Contains("最近导出会话记录为空或已损坏", source);
        }

        [Test]
        public void AssetPackageRollbackPrunesOnlyTransactionNamespace()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "Data", "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("exportRoot + \"/.ESBakeTransactions\"", source);
            StringAssert.Contains("无法清理事务目录", source);
        }

        [Test]
        public void AssetPackageRollbackVerifiesBackupHashBeforeRestore()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "Data", "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ComputeAssetFileHash(item.backupPath)", source);
            StringAssert.Contains("已跳过被修改的原目标备份", source);
        }

        [Test]
        public void AssetPackageCommitVerifiesStagedHashBeforeReplacingTarget()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "Data", "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("expectedStagedFileHash", source);
            StringAssert.Contains("暂存资产已被外部修改，拒绝覆盖目标", source);
            StringAssert.Contains("ComputeAssetFileHash(item.stagedPath)", source);
        }

        [Test]
        public void CameraTrackPreviewBoundsSamplerRefreshWork()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Camera",
                "ESCameraTrackPreviewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SamplerRefreshIntervalSeconds", source);
            StringAssert.Contains("if (now < nextSamplerRefreshTime)", source);
            StringAssert.Contains("nextSamplerRefreshTime = now + SamplerRefreshIntervalSeconds", source);
        }

        [Test]
        public void RuntimeWatchCancelsDeferredCallbacksWhenPageDisables()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SimpleToolsWindow",
                "ESTools",
                "Simple_ESTool_Page_RuntimeWatch.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("public override void OnPageDisable()", source);
            StringAssert.Contains("EditorApplication.delayCall -= ShowDeferredActionFeedback;", source);
            StringAssert.Contains("EditorApplication.delayCall -= ShowDeferredDiagnostics;", source);
            StringAssert.Contains("EditorApplication.delayCall -= DeferredRefresh;", source);
            StringAssert.Contains("private void DeferredRefresh()", source);
        }

        [Test]
        public void ResourceWindowDeferredSelectionKeepsWindowIdentity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "ResWindow",
                "ESResWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESResWindow expectedWindow = UsingWindow;", source);
            StringAssert.Contains("!ReferenceEquals(UsingWindow, expectedWindow)", source);
            StringAssert.Contains("EditorApplication.delayCall -= selectPageCallback", source);
        }

        [Test]
        public void DeferredEditorCallbacksUnregisterAfterOneShotDelivery()
        {
            string automationPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESAutomation", "ESAutomationSceneScanPrototype.cs");
            string polymorphicPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal", "ESPolymorphicReferenceDrawer.cs");
            string menuTreePath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "-Templates", "-ESMenuTreeWindow.cs");
            string automation = File.ReadAllText(automationPath, new UTF8Encoding(false, true));
            string polymorphic = File.ReadAllText(polymorphicPath, new UTF8Encoding(false, true));
            string menuTree = File.ReadAllText(menuTreePath, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= openOptionsCallback", automation);
            StringAssert.Contains("EditorApplication.delayCall -= delayedRebuild", polymorphic);
            StringAssert.Contains("EditorApplication.delayCall -= selectMigrationCallback", menuTree);
        }

        [Test]
        public void InputBindingDelayedStopIsBoundToTheCompletingOperation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESDrawer",
                "Normal",
                "ESInputBindingDefineDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private static InputActionRebindingExtensions.RebindingOperation delayedStopOperation;", source);
            StringAssert.Contains("ScheduleStopListen(operation);", source);
            StringAssert.Contains("ReferenceEquals(listenOperation, operation)", source);
            StringAssert.Contains("EditorApplication.delayCall -= StopScheduledListen;", source);
            StringAssert.Contains("输入绑定监听启动失败", source);
            StringAssert.Contains("输入绑定监听写回失败", source);
        }

        [Test]
        public void InstallerDoesNotTouchUnityPackageStateFromTaskRun()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Unity 序列化对象、AssetDatabase 和类型检查不能从 Task.Run 后台线程访问", source);
            StringAssert.Contains("private void RepaintAfterPackageCheck()", source);
            StringAssert.Contains("EditorApplication.delayCall -= RepaintAfterPackageCheck;", source);
            StringAssert.DoesNotContain("await Task.Run(() =>", source);
        }

        [Test]
        public void InstallerUpmWaitersHaveBoundedTimeoutAndAlwaysUnsubscribe()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("PackageRequestTimeoutSeconds", source);
            StringAssert.Contains("等待 UPM ListRequest 超时", source);
            StringAssert.Contains("等待 UPM AddRequest 超时", source);
            StringAssert.Contains("tcs.TrySetException", source);
            StringAssert.Contains("EditorApplication.update -= CheckCompletion;", source);
        }

        [Test]
        public void InstallerUpmWaitersStopAcceptingResultsAfterWindowInvalidation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Func<bool> isValid = null", source);
            StringAssert.Contains("if (isValid != null && !isValid())", source);
            StringAssert.Contains("tcs.TrySetCanceled();", source);
            StringAssert.Contains("WaitForAddRequestCompletion(request, IsActiveInstaller)", source);
            StringAssert.Contains("CaptureInstalledPackageSnapshotAsync(IsActiveInstaller)", source);
            StringAssert.Contains("catch (OperationCanceledException)", source);
        }

        [Test]
        public void AdvancedDialogAsyncValidationDelayCallbackIsCancellable()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESAdvancedDialog",
                "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("pendingValidationDelayCallback", source);
            StringAssert.Contains("QueueValidationDelayCallback(() =>", source);
            StringAssert.Contains("EditorApplication.delayCall -= pendingValidationDelayCallback;", source);
            StringAssert.DoesNotContain("EditorApplication.delayCall += () =>", source);
        }

        [Test]
        public void CmdAgentMcpConnectIgnoresCompletionAfterWindowInvalidation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESCmdAgent",
                "ESCmdAgentWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private int mcpConnectionGeneration;", source);
            StringAssert.Contains("private Task mcpConnectionTask;", source);
            StringAssert.Contains("private async Task ConnectMcpCoreAsync()", source);
            StringAssert.Contains("catch (Exception exception)", source);
            StringAssert.Contains("mcpConnectionTask != null && !mcpConnectionTask.IsCompleted", source);
            StringAssert.Contains("int generation = ++mcpConnectionGeneration;", source);
            StringAssert.Contains("mcpConnectionGeneration++;", source);
            StringAssert.Contains("if (!IsMcpConnectionActive(generation))", source);
            StringAssert.Contains("private bool IsMcpConnectionActive(int generation)", source);
        }

        [Test]
        public void CmdAgentComposerDragStateHasPhaseAndInvalidationCleanup()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESCmdAgent",
                "ESCmdAgentWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("RegisterCallback<DragUpdatedEvent>(OnComposerDragUpdated, TrickleDown.TrickleDown)", source);
            StringAssert.Contains("RegisterCallback<DragExitedEvent>(OnComposerDragExited, TrickleDown.TrickleDown)", source);
            StringAssert.Contains("RegisterCallback<PointerCaptureOutEvent>(OnComposerPointerCaptureOut, TrickleDown.TrickleDown)", source);
            StringAssert.Contains("RegisterCallback<FocusOutEvent>(OnComposerFocusOut, TrickleDown.TrickleDown)", source);
            StringAssert.Contains("UnregisterComposerDragCallbacks();", source);
            StringAssert.Contains("ClearComposerDragState();", source);
        }

        [Test]
        public void SoDataEditorSaveServiceCancelsPendingFlushBeforeSaving()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SODataInfoWindow",
                "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= Flush;", source);
            StringAssert.Contains("if (dirty.Count == 0)", source);
            StringAssert.Contains("scheduled = false;", source);
        }

        [Test]
        public void ReleaseUploadCallbacksIgnoreResultsAfterWindowLifecycleChanges()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESAssetReleaseUploadWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private int lifecycleGeneration;", source);
            StringAssert.Contains("lifecycleGeneration++;", source);
            StringAssert.Contains("int generation = lifecycleGeneration;", source);
            StringAssert.Contains("if (generation != lifecycleGeneration)", source);
            StringAssert.Contains("activeUploadTask = ESAssetReleaseUploadCoordinator.EnqueueValidation", source);
            StringAssert.Contains("activeUploadTask = ESAssetReleaseUploadCoordinator.Enqueue(CreateRequest()", source);
        }

        [Test]
        public void GraphInspectorLaunchCallbackRejectsDetachedPanelResults()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESGraphViewV2",
                "ESStableGraphInspector.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (launchButton == null || launchButton.panel == null)", source);
            StringAssert.Contains("ESAgentImplementationSessionLauncher.TryLaunchApprovedImplementation", source);
        }

        [Test]
        public void GraphInspectorScheduledValidationRejectsDetachedPanel()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESGraphViewV2",
                "ESStableGraphInspector.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (panel == null)", source);
            StringAssert.Contains("CancelScheduledValidation();", source);
            StringAssert.Contains("validationSchedule = schedule.Execute(RefreshValidationIfNeeded)", source);
        }

        [Test]
        public void TrackViewProjectionTaskRejectsWindowGenerationChanges()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESTrackViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private int m_ProjectionGeneration;", source);
            StringAssert.Contains("int projectionGeneration = m_ProjectionGeneration;", source);
            StringAssert.Contains("projectionGeneration != m_ProjectionGeneration", source);
            StringAssert.Contains("window != this", source);
            StringAssert.Contains("OdinEditor editor = m_EmbeddedInspectorEditor;", source);
            StringAssert.Contains("m_EmbeddedInspectorEditor = null;", source);
            StringAssert.Contains("SetActivePreviewPlayerSafely(seqPlayer)", source);
            StringAssert.Contains("SetActivePreviewPlayerSafely(rebuiltPlayer)", source);
            StringAssert.Contains("candidate.DisposeEditorPreviewTarget();", source);
        }

        [Test]
        public void TrackViewDelayedValidationAndInitialScaleAreLifecycleBound()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESTrackViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private IVisualElementScheduledItem m_AutoValidationTask;", source);
            StringAssert.Contains("private IVisualElementScheduledItem m_InitialScaleTask;", source);
            StringAssert.Contains("m_AutoValidationTask?.Pause();", source);
            StringAssert.Contains("m_InitialScaleTask?.Pause();", source);
            StringAssert.Contains("int generation = m_ProjectionGeneration;", source);
            StringAssert.Contains("generation != m_ProjectionGeneration", source);
            StringAssert.Contains("m_InitialScaleTask = root.schedule.Execute", source);
            int viewRefresh = source.IndexOf("private void FlushScheduledViewRefresh()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(viewRefresh, 0);
            string viewRefreshBody = source.Substring(viewRefresh, Math.Min(520, source.Length - viewRefresh));
            StringAssert.Contains("rootVisualElement == null || window != this", viewRefreshBody);
            int playbackSave = source.IndexOf("private void FlushPlaybackContextSave()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(playbackSave, 0);
            string playbackSaveBody = source.Substring(playbackSave, Math.Min(900, source.Length - playbackSave));
            StringAssert.Contains("rootVisualElement == null || window != this", playbackSaveBody);
            StringAssert.Contains("m_PlaybackContextDirty = false;", playbackSaveBody);
            int autoSave = source.IndexOf("private void FlushAutoSave()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(autoSave, 0);
            string autoSaveBody = source.Substring(autoSave, Math.Min(850, source.Length - autoSave));
            StringAssert.Contains("rootVisualElement == null || window != this", autoSaveBody);
            StringAssert.Contains("CancelTrackAutoSaveWithoutWriting();", autoSaveBody);
        }

        [Test]
        public void WorldSpaceViewportRefreshRejectsStaleScheduledCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Editor",
                "World",
                "ESWorldMapSpaceEditorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private int viewportRefreshGeneration;", source);
            StringAssert.Contains("viewportRefreshGeneration++;", source);
            StringAssert.Contains("int generation = viewportRefreshGeneration;", source);
            StringAssert.Contains("generation != viewportRefreshGeneration", source);
            StringAssert.Contains("viewport.Rebuild();", source);
            StringAssert.Contains("viewport == null", source);
        }

        [Test]
        public void TrackPreviewPlayerReplacementIsTransactionalAndIdempotent()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "EditorPlay.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SetActivePreviewPlayerSafely(candidate)",
                File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "Plugins",
                    "ES",
                    "Editor",
                    "ESTrackView",
                    "-TrackView-Define",
                    "ESTrackViewWindow.cs"), new UTF8Encoding(false, true)));
            StringAssert.Contains("candidate.DisposeEditorPreviewTarget();", source);
            StringAssert.Contains("if (ReferenceEquals(ActiveSequence, value))", source);
            StringAssert.Contains("samplerIterationBufferPool", source);
            StringAssert.Contains("AcquireSamplerIterationBuffer();", source);
            StringAssert.Contains("ReleaseSamplerIterationBuffer(buffer);", source);
            StringAssert.Contains("private bool stoppingSamplers;", source);
            StringAssert.Contains("if (stoppingSamplers)", source);
            StringAssert.Contains("if (PreviewTarget == null || PreviewTarget.IsRecycled)", source);
            StringAssert.Contains("private void ClearDisposedState()", source);
            StringAssert.Contains("OnTimeUpdated = null;", source);
            StringAssert.Contains("samplers.Clear();", source);
            StringAssert.Contains("private void NotifyTimeUpdated()", source);
            StringAssert.Contains("时间更新通知失败", source);
        }

        [Test]
        public void CameraPreviewViewReleasesDirectorAfterAdapterFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "Camera",
                "Preview",
                "ESCameraPreviewView.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESCameraCinemachine2ViewAdapter currentAdapter = adapter;", source);
            StringAssert.Contains("adapter = null;", source);
            StringAssert.Contains("继续执行资源释放", source);
            StringAssert.Contains("director.Dispose();", source);
            StringAssert.Contains("ESCameraCinemachine2ViewAdapter candidate = null;", source);
            StringAssert.Contains("bool registered = false;", source);
            StringAssert.Contains("candidate?.Dispose();", source);
        }

        [Test]
        public void CameraTrackPreviewFactoryRollsBackOwnedTargetOnBuildFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "Camera",
                "ESCameraTrackPreviewFactory.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("request.ownsEditorTarget", source);
            StringAssert.Contains("request.editorTarget is IPoolableAuto poolable", source);
            StringAssert.Contains("poolable.TryAutoPushedToPool();", source);
            StringAssert.Contains("构建失败，owned EditorTarget 回收失败", source);
        }

        [Test]
        public void GameObjectTrackSamplerRollsBackOverrideTargetOnClipFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "SkillTrack",
                "SkillTrackItems",
                "SkillTrackItem_GameObject.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("try", source);
            StringAssert.Contains("lastCreatedEditorSampler = null;", source);
            StringAssert.Contains("if (ownsEditorTarget && target != null && !target.IsRecycled)", source);
            StringAssert.Contains("target.ForcePushToPool();", source);
        }

        [Test]
        public void GameObjectPreviewSamplersResetSessionBaselinesAfterStop()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "Sampler.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int trackStop = source.IndexOf(
                "public override void OnEditorPreviewStop()",
                source.IndexOf("class GameObjectTrackEditorSampler", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(trackStop, 0);
            string trackBody = source.Substring(trackStop, Math.Min(1800, source.Length - trackStop));
            StringAssert.Contains("_targets.Clear();", trackBody);
            StringAssert.Contains("_sampleFrame = 0;", trackBody);
            StringAssert.Contains("_lastSubmitTime = float.NaN;", trackBody);
            StringAssert.Contains("停止预览时恢复对象状态失败", trackBody);
            StringAssert.Contains("HasConflictWarning", source);
            StringAssert.Contains("同一采样帧内多个 Clip", source);
            StringAssert.Contains("当前保持既有顺序语义", source);

            int samplerStop = source.IndexOf(
                "public override void OnEditorPreviewStop()",
                source.IndexOf("class GameObjectEditorSampler", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(samplerStop, 0);
            string samplerBody = source.Substring(samplerStop, Math.Min(1400, source.Length - samplerStop));
            StringAssert.Contains("_hasCachedOriginal = false;", samplerBody);
            StringAssert.Contains("_hasAppliedActiveState = false;", samplerBody);
            StringAssert.Contains("_wasInside = false;", samplerBody);
        }

        [Test]
        public void ParticlePreviewRestorationAttemptsEachStateIndependently()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "Sampler.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int start = source.IndexOf(
                "public override void OnEditorPreviewStop()",
                source.IndexOf("class ParticleEditorSampler", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string body = source.Substring(start, Math.Min(2600, source.Length - start));
            StringAssert.Contains("恢复粒子时间失败", body);
            StringAssert.Contains("恢复粒子发射状态失败", body);
            StringAssert.Contains("恢复粒子播放状态失败", body);
            StringAssert.Contains("originalStateCaptured = false;", body);
        }

        [Test]
        public void AudioPreviewSamplingFailsClosedAndResetsCursor()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "Sampler.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int start = source.IndexOf(
                "public override void SampleTime(float time)",
                source.IndexOf("class AudioEditorSampler", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string body = source.Substring(start, Math.Min(1900, source.Length - start));
            StringAssert.Contains("_lastSampledTime = -1f;", body);
            StringAssert.Contains("_audioSource.Stop();", body);
            StringAssert.Contains("_loggedPlaybackFailure", body);
            StringAssert.Contains("音频预览采样失败，本次采样已停止", body);
        }

        [Test]
        public void EditorSamplerRegistryRebuildCleansPartialResults()
        {
            string path = Path.Combine(
                Application.dataPath,
                "..",
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "Sampler.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int rebuild = source.IndexOf(
                "public void Rebuild(ITrackSequence sequence)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebuild, 0);
            string body = source.Substring(rebuild, Math.Min(2900, source.Length - rebuild));
            StringAssert.Contains("Dictionary<ITrackClip, IEditorTimeSampler> rebuilt", body);
            StringAssert.Contains("StopSamplers(rebuilt.Values);", body);
            StringAssert.Contains("已清理本次已创建的采样器", body);
            StringAssert.Contains("foreach (var pair in rebuilt)", body);
            StringAssert.Contains("rebuilt.TryGetValue(clip", body);
            StringAssert.Contains("bool duplicateClip", body);
            StringAssert.Contains("Register the candidate before diagnostics", body);
            StringAssert.Contains("StopSamplers(new[] { previousSampler })", body);
            StringAssert.Contains("已释放旧采样器并保持后一个轨道的覆盖语义", body);

            int stop = source.IndexOf(
                "private static void StopSamplers",
                rebuild,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(stop, 0);
            string stopBody = source.Substring(stop, Math.Min(1250, source.Length - stop));
            StringAssert.Contains("继续清理其他采样器", stopBody);

            StringAssert.Contains("private readonly List<IEditorTimeSampler> _tickBuffer", source);
            StringAssert.Contains("private bool _isTicking", source);
            StringAssert.Contains("private int _rebuildGeneration", source);
            int tick = source.IndexOf("public void Tick(float time)", rebuild, StringComparison.Ordinal);
            Assert.GreaterOrEqual(tick, 0);
            string tickBody = source.Substring(tick, Math.Min(2100, source.Length - tick));
            StringAssert.Contains("if (_isTicking)", tickBody);
            StringAssert.Contains("generation != _rebuildGeneration", tickBody);
            StringAssert.Contains("_tickBuffer.Clear();", tickBody);
        }

        [Test]
        public void AdvancedAnimationMixerBuildRollsBackCandidateGraph()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "TrackItemAndClip",
                "Tools",
                "Sampler.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private TargetMixer FindOrCreateMixer", source);
            StringAssert.Contains("mixer.Dispose();", source);
            StringAssert.Contains("mixer.RestoreOriginalPose();", source);
            StringAssert.Contains("已回滚候选 Graph", source);
            StringAssert.Contains("TargetMixer mixer = _mixers[i];", source);
            StringAssert.Contains("高级动画预览原始姿态恢复失败", source);
            StringAssert.Contains("GameObject go = null;", source);
            StringAssert.Contains("AudioSource source = _audioSource;", source);
            StringAssert.Contains("_audioSource = null;", source);
            StringAssert.Contains("AnimationMode.BeginSampling();", source);
            StringAssert.Contains("finally", source);
            StringAssert.Contains("AnimationMode.EndSampling();", source);
            StringAssert.Contains("activeAnimationModeUsers", source);
            StringAssert.Contains("animationModeOwned", source);
            StringAssert.Contains("if (activeAnimationModeUsers == 0 && animationModeOwned)", source);
            StringAssert.Contains("if (AnimationMode.InAnimationMode())", source);
            StringAssert.Contains("CaptureOriginalState();", source);
            StringAssert.Contains("originalEmissionEnabled", source);
            StringAssert.Contains("particleSystem.Simulate(Mathf.Max(0f, originalTime), true, true);", source);
        }

        [Test]
        public void GenericSkillTrackRollsBackOverrideTargetOnSamplerFailure()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "Skill",
                "SkillSequence",
                "Base",
                "SkillTrackSequence.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESRuntimeTargetPack target = null;", source);
            StringAssert.Contains("target.ForcePushToPool();", source);
            StringAssert.Contains("return CreateClipEditorSamplers(sequence, target, true);", source);
        }

        [Test]
        public void SemiSleepStressTestCancelsDeferredSleepRequest()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "-Templates",
                "ESMenuTreeToolkitTestWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= RequestAllWindowsSleep", source);
            StringAssert.Contains("EditorApplication.delayCall += RequestAllWindowsSleep", source);
        }

        [Test]
        public void TrackViewKeepsStableSelectionWhenIdentityIsTemporarilyMissing()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESTrackViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("稳定身份暂时不可见时不要清空持久选择", source);
            StringAssert.Contains("m_SelectedTrackIndex = -1;", source);
            StringAssert.DoesNotContain("m_SelectedTrackId = string.Empty;", source.Substring(
                source.IndexOf("稳定身份暂时不可见时不要清空持久选择", StringComparison.Ordinal), 420));
        }

        [Test]
        public void TrackViewAutoSaveRechecksExternalRevisionBeforeWriting()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESTrackViewWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int saveMethod = source.IndexOf("private bool TrySaveAutoSaveTarget", StringComparison.Ordinal);
            int revisionCheck = source.IndexOf("SynchronizeTrackContainerRevision(includeDependencyHash: true)", saveMethod, StringComparison.Ordinal);
            int saveCall = source.IndexOf("AssetDatabase.SaveAssetIfDirty(target)", saveMethod, StringComparison.Ordinal);
            Assert.GreaterOrEqual(saveMethod, 0);
            Assert.GreaterOrEqual(revisionCheck, 0);
            Assert.GreaterOrEqual(saveCall, 0);
            Assert.Less(revisionCheck, saveCall);
            StringAssert.Contains("保存前校验已暂停", source);
            StringAssert.Contains("Directory.GetParent(Application.dataPath)", source);
            StringAssert.Contains("Path.GetFullPath(Path.Combine(projectRoot, normalizedPath))", source);
        }

        [Test]
        public void AssetPackagePreviewContextCannotRehydrateAfterDispose()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (disposed)", source);
            StringAssert.Contains("if (disposed || obj == null)", source);
            StringAssert.Contains("Preview context already disposed.", source);
        }

        [Test]
        public void TrackTemporaryInspectorIdentityRepairHasUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "ESTrackTemporaryInspectorWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(sourceAsset, \"迁移 Track 稳定身份\")", source);
            StringAssert.Contains("Undo.RecordObject(sourceAsset, \"迁移 Clip 稳定身份\")", source);
            StringAssert.Contains("ESTrackIdentity.IsValidStableId(stableTrack.TrackId)", source);
            StringAssert.Contains("ESTrackIdentity.IsValidStableId(stableClip.ClipId)", source);
        }

        [Test]
        public void TrackProjectionIdentityRepairHasUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESTrackView",
                "-TrackView-Define",
                "TrackElements",
                "ESEditorTrackItem.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(undoTarget, \"迁移 Track 稳定身份\")", source);
            StringAssert.Contains("Undo.RecordObject(undoTarget, \"迁移 Clip 稳定身份\")", source);
            StringAssert.Contains("ESTrackIdentity.IsValidStableId(stableTrack.TrackId)", source);
            StringAssert.Contains("ESTrackIdentity.IsValidStableId(stableClip.ClipId)", source);
        }

        [Test]
        public void AssetDeliveryModeReadMigrationHasUndoBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESAssetDeliveryModeEditorUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("bool migrated = !library.HasExplicitDeliveryMode", source);
            StringAssert.Contains("Undo.RecordObject(library, \"迁移资产库分发方式\")", source);
            StringAssert.Contains("library.EnsureDeliveryModeMigrated();", source);
        }

        [Test]
        public void HybridClrConsumerEditorsRecordUndoBeforeMutation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESHybridCLREditorIntegration.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(consumer, \"修改 Consumer 代码热更开关\")", source);
            StringAssert.Contains("Undo.RecordObject(consumer, \"配置 Consumer 代码模块\")", source);
            StringAssert.Contains("consumer.EnsureStableIdentity();", source);
        }

        [Test]
        public void HybridClrPrepareSettingsRecordsLinkedUndoBoundaries()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESResPipeline",
                "ESHybridCLREditorIntegration.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(consumer, \"同步 Consumer 代码模块信息\")", source);
            StringAssert.Contains("Undo.RecordObject(settings, \"更新 HybridCLR 设置\")", source);
            StringAssert.Contains("HybridCLRSettings.Save();", source);
        }

        [Test]
        public void AssetPackageAnalysisRecordsRootAndCreatedAssetUndo()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(data, \"分析 ES 资产包\")", source);
            StringAssert.Contains("Undo.RegisterCreatedObjectUndo(analysis, \"创建 ES 资产包分析数据\")", source);
            StringAssert.Contains("data.analysisData = analysis;", source);
        }

        [Test]
        public void AssetPackageBakeInvalidatesAnalysisWithUndo()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "Data",
                "ESAssetPackageBakeData.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(analysisData, \"标记资产包分析过期\")", source);
            StringAssert.Contains("analysisState = ESAssetPackageAnalysisState.Stale", source);
        }

        [Test]
        public void AutomationAiBridgeStopCancelsDeferredStart()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESAutomation",
                "ESAutomationAiBridge.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall += EnsureStartedIfEnabled", source);
            StringAssert.Contains("EditorApplication.delayCall -= EnsureStartedIfEnabled", source);
        }

        [Test]
        public void AutomationAiBridgeBoundsQueuedRequestPathsWithoutDroppingInboxFiles()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESAutomation",
                "ESAutomationAiBridge.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private const int MaxQueuedRequests = 256;", source);
            StringAssert.Contains("if (queuedPaths.Count >= MaxQueuedRequests)", source);
            StringAssert.Contains("rescanRequested = true;", source);
            StringAssert.Contains("不丢弃 Inbox 文件", source);
        }

        [Test]
        public void SceneViewBaselineIsCancelledBeforePlayMode()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "SceneHierarchyExpansionState.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int playModeStart = source.IndexOf("private static void OnPlayModeStateChanged", StringComparison.Ordinal);
            Assert.GreaterOrEqual(playModeStart, 0);
            int exitingEditMode = source.IndexOf("PlayModeStateChange.ExitingEditMode", playModeStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(exitingEditMode, 0);
            int rememberCamera = source.IndexOf("RememberActiveSceneViewCameraState();", exitingEditMode, StringComparison.Ordinal);
            Assert.GreaterOrEqual(rememberCamera, 0);
            string boundary = source.Substring(exitingEditMode, rememberCamera - exitingEditMode);
            StringAssert.Contains("CancelSceneViewCameraBaseline();", boundary);
        }

        [Test]
        public void SceneHierarchyGuideAutosaveIsScopedToGuideAsset()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "SceneHierarchyExpansionState.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int saveMethod = source.IndexOf("private static void SaveProjectGuideDataIfNeeded", StringComparison.Ordinal);
            int scopedSave = source.IndexOf("AssetDatabase.SaveAssetIfDirty(globalData)", saveMethod, StringComparison.Ordinal);
            Assert.GreaterOrEqual(saveMethod, 0);
            Assert.GreaterOrEqual(scopedSave, 0);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source.Substring(saveMethod, Math.Min(360, source.Length - saveMethod)));
        }

        [Test]
        public void SceneHierarchyReflectionFailuresAreContained()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "SceneHierarchyExpansionState.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SceneHierarchy SetExpanded 反射调用失败", source);
            StringAssert.Contains("SceneHierarchy 内部对象解析失败", source);
            StringAssert.Contains("return false;", source);
        }

        [Test]
        public void InstallerCancelsDeferredPackageStateCheckOnDisable()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.delayCall -= CheckPendingPackageInstallState", source);
            StringAssert.Contains("pendingPackageInstallStateChecks.Clear()", source);
            StringAssert.Contains("pendingPackageInstallStateChecks.Enqueue(package)", source);
            StringAssert.Contains("EditorApplication.delayCall += CheckPendingPackageInstallState", source);
        }

        [Test]
        public void PreviewResourceScopeDoesNotOwnPersistentAssets()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewResourceScope.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (EditorUtility.IsPersistent(obj))", source);
            StringAssert.Contains("if (EditorUtility.IsPersistent(gameObject))", source);
            StringAssert.Contains("禁止改 HideFlags 或在 Dispose 时销毁", source);
        }

        [Test]
        public void PreviewMarkerDoesNotMutatePersistentAssets()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (EditorUtility.IsPersistent(obj))", source);
            StringAssert.Contains("persistent asset is not owned by preview cleanup", source);
        }

        [Test]
        public void PreviewCaptureFailureReleasesTemporaryResources()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (!renderTexture.IsCreated())", source);
            StringAssert.Contains("Texture2D copy = null", source);
            StringAssert.Contains("Texture2D texture = null", source);
            StringAssert.Contains("DestroyObject(copy)", source);
            StringAssert.Contains("DestroyObject(texture)", source);
        }

        [Test]
        public void PreviewRenderTextureCreationCleansFailedAllocation()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("renderTexture.Create();", source);
            StringAssert.Contains("catch", source);
            StringAssert.Contains("DestroyObject(renderTexture);", source);
            StringAssert.Contains("throw;", source);
        }

        [Test]
        public void PreviewCameraSnapshotRejectsInvalidReadbackDimensions()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("width <= 0 || height <= 0", source);
            StringAssert.Contains("width > renderTexture.width || height > renderTexture.height", source);
        }

        [Test]
        public void PreviewDestroyUtilityRejectsPersistentObjects()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (EditorUtility.IsPersistent(obj))", source);
            StringAssert.Contains("不可逆 DestroyImmediate", source);
        }

        [Test]
        public void PreviewMarkerRequiresOwnershipFlags()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("HasPreviewOwnershipFlags(obj)", source);
            StringAssert.Contains("object has no preview ownership flags", source);
            StringAssert.Contains("HideFlags.DontSaveInEditor", source);
        }

        [Test]
        public void PreviewPrepareRejectsUnownedSceneObjectsBeforeMutation()
        {
            string corePath = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string packagePath = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "AssetPackageBakeWindow",
                "ESAssetPackageBakeWindow.cs");
            string core = File.ReadAllText(corePath, new UTF8Encoding(false, true));
            string package = File.ReadAllText(packagePath, new UTF8Encoding(false, true));
            StringAssert.Contains("if (EditorUtility.IsPersistent(obj) || !ESEditorPreviewUtility.HasPreviewOwnershipFlags(obj))", core);
            StringAssert.Contains("if (EditorUtility.IsPersistent(obj) || !ESEditorPreviewUtility.HasPreviewOwnershipFlags(obj))", package);
            StringAssert.Contains("object is not an owned temporary preview object", core);
            StringAssert.Contains("object is not an owned temporary preview object", package);
        }

        [Test]
        public void PreviewScopeRequiresOwnershipBeforeGameObjectRegistration()
        {
            string scopePath = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewResourceScope.cs");
            string worldPath = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "World",
                "ESWorldAuthoringViewport.cs");
            string scope = File.ReadAllText(scopePath, new UTF8Encoding(false, true));
            string world = File.ReadAllText(worldPath, new UTF8Encoding(false, true));
            StringAssert.Contains("if (obj is GameObject gameObject && !ESEditorPreviewUtility.HasPreviewOwnershipFlags(gameObject))", scope);
            StringAssert.Contains("if (!ESEditorPreviewUtility.HasPreviewOwnershipFlags(gameObject))", scope);
            StringAssert.Contains("obj.hideFlags & (HideFlags.HideAndDontSave", scope);
            StringAssert.Contains("createdTerrain.hideFlags = ESEditorPreviewUtility.PreviewHideFlags", world);
            StringAssert.Contains("root.hideFlags = ESEditorPreviewUtility.PreviewHideFlags", world);
            StringAssert.Contains("hideFlags = ESEditorPreviewUtility.PreviewHideFlags", world);
        }

        [Test]
        public void PreviewScopeRegistersObjectsBeforeMutationSoFailedInitializationRemainsOwned()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewResourceScope.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int objectMethod = source.IndexOf("public T RegisterObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(objectMethod, 0);
            string objectBody = source.Substring(objectMethod, Math.Min(1300, source.Length - objectMethod));
            Assert.Less(objectBody.IndexOf("unityObjects.Add(obj)", StringComparison.Ordinal), objectBody.IndexOf("obj.hideFlags =", StringComparison.Ordinal));
            int gameObjectMethod = source.IndexOf("public GameObject RegisterGameObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(gameObjectMethod, 0);
            string gameObjectBody = source.Substring(gameObjectMethod, Math.Min(1700, source.Length - gameObjectMethod));
            Assert.Less(gameObjectBody.IndexOf("unityObjects.Add(gameObject)", StringComparison.Ordinal), gameObjectBody.IndexOf("gameObject.hideFlags =", StringComparison.Ordinal));
            StringAssert.Contains("保留登记", gameObjectBody);
            StringAssert.Contains("HideFlags ownershipFlags = HideFlags.HideAndDontSave", objectBody);
            StringAssert.Contains("(obj.hideFlags & ownershipFlags) != ownershipFlags", objectBody);
        }

        [Test]
        public void PreviewResourceScopeRetainsFailedDisposalsForRetry()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewResourceScope.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Exception firstFailure = null", source);
            StringAssert.Contains("customDisposers.RemoveAt(i)", source);
            StringAssert.Contains("unityObjects.RemoveAt(i)", source);
            StringAssert.Contains("ESEditorPreviewLifecycleHub.RegisterScope(this)", source);
            StringAssert.Contains("失败项已保留等待重试", source);
        }

        [Test]
        public void PreviewLifecycleCountsOnlySuccessfulScopeReleases()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("int releasedScopes = 0", source);
            StringAssert.Contains("releasedScopes++", source);
            StringAssert.Contains("totalScopeReleases += releasedScopes", source);
            StringAssert.DoesNotContain("totalScopeReleases += DisposeBuffer.Count", source);
        }

        [Test]
        public void PreviewLifecycleExposesFailedScopeResidueCount()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Runtime",
                "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("public static int FailedScopeCount => FailedScopes.Count", source);
            StringAssert.Contains("FailedScopes.Add(DisposeBuffer[i])", source);
        }

        [Test]
        public void ShaderPngExportUsesBoundedFailureCleanup()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESShader",
                "ESCompositeShaderBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (bakedTexture == null)", source);
            StringAssert.Contains("if (!absolutePath.StartsWith(projectRoot", source);
            StringAssert.Contains("if (File.Exists(absolutePath))", source);
            StringAssert.Contains("byte[] pngBytes = bakedTexture.EncodeToPNG()", source);
            StringAssert.Contains("if (createdFile && !completed)", source);
            StringAssert.Contains("AssetDatabase.DeleteAsset(path)", source);
            StringAssert.Contains("lastError = \"导出 PNG 失败：\"", source);
        }

        [Test]
        public void EditorAssetCreationRejectsOverwriteAndRollsBack()
        {
            string[] paths =
            {
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESShader", "ESCompositeShaderGUI.Productivity.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2", "ESAgentArtifactGenerationWorkflow.cs"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2", "ESStableGraphViewWindow.cs"),
                Path.Combine(Application.dataPath, "Scripts", "ESLogic", "Editor", "World", "ESWorldDialogueWorkbenchWindow.cs"),
                Path.Combine(Application.dataPath, "Scripts", "ESLogic", "Editor", "World", "ESWorldMapSpaceEditorWindow.cs"),
                Path.Combine(Application.dataPath, "Scripts", "ESLogic", "Editor", "World", "ESWorldBuilderWorkbenchWindow.cs")
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string source = File.ReadAllText(paths[i], new UTF8Encoding(false, true));
                StringAssert.Contains("AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)", source);
                StringAssert.Contains("File.Exists(", source);
                StringAssert.Contains("GetFullPath(path)", source);
            }

            string[] rollbackPaths = { paths[0], paths[3], paths[4], paths[5] };
            for (int i = 0; i < rollbackPaths.Length; i++)
            {
                string source = File.ReadAllText(rollbackPaths[i], new UTF8Encoding(false, true));
                StringAssert.Contains("bool createdAsset = false", source);
                StringAssert.Contains("AssetDatabase.DeleteAsset(path)", source);
                StringAssert.Contains("DestroyImmediate", source);
            }
        }

        [Test]
        public void InstallerPostImportStateCheckKeepsWindowIdentity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "Installer",
                "ESInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ESInstaller expectedInstaller = this;", source);
            StringAssert.Contains("!ReferenceEquals(installer, expectedInstaller)", source);
        }

        [Test]
        public void MenuTreeMigrationSelectionKeepsWindowIdentity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "-Templates",
                "-ESMenuTreeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("This expectedWindow = UsingWindow;", source);
            StringAssert.Contains("!ReferenceEquals(UsingWindow, expectedWindow)", source);
        }

        [Test]
        public void SearchDropdownBuilderAddRangePreservesOrderAndCallbacks()
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
        public void SearchDropdownProviderCollectionIsBounded()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumResolvedEntries = 10000", source);
            StringAssert.Contains("resolved.Count >= MaximumResolvedEntries", source);
            StringAssert.Contains("候选项过多，已限制为 ", source);
            StringAssert.Contains("if (entry.Label == null)", source);
            StringAssert.Contains("候选项无效，已跳过", source);
        }

        [Test]
        public void CompactChoicePopupRejectsUnboundedOptionSets()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumSupportedOptions = 256", source);
            StringAssert.Contains("choices.Count > MaximumSupportedOptions", source);
            StringAssert.Contains("请改用 ESSearchDropdown", source);
        }

        [Test]
        public void WindowLauncherUsesReusableSinglePageIMGUIFoundation()
        {
            Assert.IsTrue(typeof(ESSinglePageIMGUIWindow<ESWindowLauncher>)
                .IsAssignableFrom(typeof(ESWindowLauncher)));
            Assert.AreSame(ESWindowCommandRegistry.All, ESWindowCommandRegistry.All);
        }

        [Test]
        public void WindowLauncherPrunesStalePreferencesAndBoundsLists()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESWindowLauncher.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("PrunePersistedLists()", source);
            StringAssert.Contains("RemoveAll(id => !Commands.ContainsKey(id))", source);
            StringAssert.Contains("FavoriteOrder.RemoveAll(id => !Favorites.Contains(id))", source);
            StringAssert.Contains("MaxFavoriteCount = 32", source);
            StringAssert.Contains("MaxRecentCount = 12", source);
            StringAssert.Contains("EditorPrefs.GetString", source);
            StringAssert.Contains("EditorPrefs.SetString", source);
            StringAssert.Contains("读取 ES 工具启动器偏好失败", source);
            StringAssert.Contains("保存 ES 工具启动器偏好失败", source);
        }

        [Test]
        public void WorldCommercialValidationRejectsStaleDelayedCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "World",
                "ESWorldBuilderWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("commercialValidationGeneration", source);
            StringAssert.Contains("int validationGeneration = ++commercialValidationGeneration", source);
            StringAssert.Contains("validationGeneration != commercialValidationGeneration", source);
            StringAssert.Contains("!commercialValidationAcceptanceInProgress", source);
            StringAssert.Contains("frame + 1, validationGeneration", source);
        }

        [Test]
        public void WorldMemoryProfilerCallbackRejectsStaleWindowGeneration()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "World",
                "ESWorldBuilderWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("memoryProfilerCaptureGeneration", source);
            StringAssert.Contains("memoryProfilerCaptureGeneration++", source);
            StringAssert.Contains("int captureGeneration = ++memoryProfilerCaptureGeneration", source);
            StringAssert.Contains("captureGeneration != memoryProfilerCaptureGeneration", source);
        }

        [Test]
        public void WorldMemoryProfilerCallbackCoalescesDuplicateProviderCallbacks()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts",
                "ESLogic",
                "Editor",
                "World",
                "ESWorldWorkbenchAcceptance.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("bool callbackScheduled = false", source);
            StringAssert.Contains("if (callbackScheduled) return", source);
            StringAssert.Contains("callbackScheduled = true", source);
            StringAssert.Contains("bool callbackDelivered = false", source);
        }

        [Test]
        public void AdvancedDialogCancelsAsyncValidationAtCloseBoundary()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESAdvancedDialog",
                "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private async void ScheduleAsyncValidation()", source);
            StringAssert.Contains("int generation = ++validationGeneration", source);
            StringAssert.Contains("generation != validationGeneration", source);
            StringAssert.Contains("CancelAsyncValidation();", source);
            StringAssert.Contains("CompleteCloseLifecycle();", source);
            StringAssert.Contains("EditorApplication.delayCall -= pendingValidationDelayCallback", source);
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
        public void EditorThemeWindowGuardsStaleSerializedThemeWrites()
        {
            string path = Path.Combine(Application.dataPath, "..", "Plugins", "ES", "Editor", "ESPresentation", "ESEditorThemeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("TryPrepareSerializedTheme()", source);
            StringAssert.Contains("UpdateIfRequiredOrScript()", source);
            StringAssert.Contains("主题序列化对象已失效，取消本次写回", source);
            StringAssert.Contains("深度皮肤开关写回失败，已取消本次修改", source);
            StringAssert.Contains("主题资产在重载或外部修改后已失效，已取消本次编辑", source);
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
        public void DisposedPreviewContextRejectsLateEnsureInsteadOfRecreatingResources()
        {
            ESEditorPreviewRenderContext context = new ESEditorPreviewRenderContext(
                "ES Preview Dispose Contract Test",
                ESEditorPreviewSceneMode.PreviewScene);
            try
            {
                context.Ensure();
                context.Dispose();

                Assert.IsTrue(context.IsDisposed);
                Assert.Throws<ObjectDisposedException>(() => context.Ensure());
                Assert.IsFalse(context.IsReady);
            }
            finally
            {
                context.Dispose();
            }
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

            Type context = typeof(ESAssetPackagePreviewSession);
            Assert.IsNotNull(context.GetProperty("AudioListenerOrigin", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetProperty("AudioListenerRotation", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetMethod("SetPreviewAudioListenerPlaying", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(context.GetMethod("GetAudioListenerDescription", BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNotNull(audioPlayer.GetMethod("RegisterTick", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetMethod("UnregisterTick", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(audioPlayer.GetMethod("DrawDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void PreviewEnhancerQualityMappingKeepsLowEndFreeOfOptionalResources()
        {
            Assert.AreEqual(
                ESEditorPreviewEnhancerSet.LowEnd,
                ESEditorPreviewEnhancerBudgets.ForQuality(ESEditorPreviewQuality.Fast));
            Assert.AreEqual(
                ESEditorPreviewEnhancerSet.GroundPlane | ESEditorPreviewEnhancerSet.ScaleReference,
                ESEditorPreviewEnhancerBudgets.ForQuality(ESEditorPreviewQuality.Balanced));
            Assert.AreEqual(
                ESEditorPreviewEnhancerSet.Full,
                ESEditorPreviewEnhancerBudgets.ForQuality(ESEditorPreviewQuality.High));
            Assert.AreEqual(0, (int)ESEditorPreviewEnhancerSet.LowEnd);
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
        [Test]
        public void AssetReferenceCheckerNormalizesAndVerifiesCachedPaths()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "ESMenuTreeWindow",
                "SimpleToolsWindow",
                "AssetsTools",
                "Simple_AssetTool_Page_AssetReferenceChecker.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("NormalizeAssetPath(info.AssetPath)", source);
            StringAssert.Contains("AssetDatabase.AssetPathToGUID(normalizedPath)", source);
            StringAssert.Contains("LoadAssetAtPath<UnityEngine.Object>(normalizedPath)", source);
        }

        [Test]
        public void ToolbarRecentScenesResolveThroughAssetDatabase()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESEditorToolBar",
                "ESEditorToolBar.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("string canonicalRecentPath = ResolveCanonicalScenePath(recentPath)", source);
            StringAssert.DoesNotContain("!File.Exists(recentPath)", source);
        }

        [Test]
        public void InspectorAssetDeleteUsesAssetDatabaseIdentity()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESEditorInspector",
                "InspectorUser_AssetQuickInfo.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.AssetPathToGUID(normalized)", source);
            StringAssert.DoesNotContain("File.Exists(path)", source);
            StringAssert.DoesNotContain("Directory.Exists(path)", source);
        }

        [Test]
        public void CompactChoicePopupCreationFailureDoesNotMaskOriginalException()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Plugins",
                "ES",
                "Editor",
                "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("if (popup != null)", source);
            StringAssert.Contains("popup.ReleaseHostInteractionHold();", source);
            StringAssert.Contains("throw;", source);
        }

        [Test]
        public void EditorPreferenceMutationsUseScopedAssetSaves()
        {
            string toolbarPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorToolBar", "ESEditorToolBar.cs");
            string inspectorPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorInspector", "InspectorUser_AssetQuickInfo.cs");
            string toolbar = File.ReadAllText(toolbarPath, new UTF8Encoding(false, true));
            string inspector = File.ReadAllText(inspectorPath, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(ESSceneGlobalData.Instance)", toolbar);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(data)", inspector);
            StringAssert.DoesNotContain("EditorUtility.SetDirty(ESSceneGlobalData.Instance);\n                    AssetDatabase.SaveAssets();", toolbar);
            StringAssert.DoesNotContain("EditorUtility.SetDirty(data);\n                    AssetDatabase.SaveAssets();", inspector);
        }

        [Test]
        public void FontProfileCreationUsesScopedAssetSave()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "FontToolsWindow", "Page_FontBuild.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void CreateProfile()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(2200, source.Length - method));
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void SoDataGroupRenameAndMarkSaveOnlyTargetAsset()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int mark = source.IndexOf("private static void MarkGroupAssetDirtyAndSave", StringComparison.Ordinal);
            Assert.GreaterOrEqual(mark, 0);
            string markBody = source.Substring(mark, Math.Min(1000, source.Length - mark));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(groupAsset);", markBody);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", markBody);

            int rename = source.IndexOf("public void RenameFileThis()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rename, 0);
            string renameBody = source.Substring(rename, Math.Min(900, source.Length - rename));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(file);", renameBody);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", renameBody);
        }

        [Test]
        public void SingleAssetCreationFlowsUseScopedAssetSaves()
        {
            string stateMachinePath = Path.Combine(Application.dataPath, "Scripts", "ESLogic", "Editor",
                "StateMachineConfigEditorMenu.cs");
            string dialoguePath = Path.Combine(Application.dataPath, "Scripts", "ESLogic", "Editor", "World",
                "ESWorldDialogueWorkbenchWindow.cs");
            string graphPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2",
                "ESAgentArtifactGenerationWorkflow.cs");
            string stateMachine = File.ReadAllText(stateMachinePath, new UTF8Encoding(false, true));
            string dialogue = File.ReadAllText(dialoguePath, new UTF8Encoding(false, true));
            string graph = File.ReadAllText(graphPath, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(config, assetPath);", stateMachine);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(config);", stateMachine);
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", dialogue);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", dialogue);
            StringAssert.Contains("AssetDatabase.CreateAsset(asset, path);", graph);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", graph);
        }

        [Test]
        public void LibraryTemplateSingleTargetSavesAreScoped()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int immediate = source.IndexOf("private void SaveAssetsImmediate()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(immediate, 0);
            string immediateBody = source.Substring(immediate, Math.Min(700, source.Length - immediate));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(library);", immediateBody);
            int create = source.IndexOf("AssetDatabase.CreateAsset(consumer, path);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string createBody = source.Substring(create, Math.Min(260, source.Length - create));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(consumer);", createBody);
            int disable = source.IndexOf("public override void OnPageDisable()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(disable, 0);
            string disableBody = source.Substring(disable, Math.Min(350, source.Length - disable));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(package);", disableBody);
        }

        [Test]
        public void WorldBuilderSingleAssetFlowsUseScopedSaves()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World",
                "ESWorldBuilderWorkbenchWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int sample = source.IndexOf("PopulateCommercialValidationSample(asset)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(sample, 0);
            string sampleBody = source.Substring(sample, Math.Min(700, source.Length - sample));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", sampleBody);
            int create = source.IndexOf("AssetDatabase.CreateAsset(asset, path);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string createBody = source.Substring(create, Math.Min(500, source.Length - create));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", createBody);
        }

        [Test]
        public void CharacterTemplatePrefabCreationUsesScopedSaves()
        {
            string basicPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "CharacterTemplates", "ESBasicCharacterTemplateBuilder.cs");
            string hertaPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "CharacterTemplates", "ESFormalHertaPlayerVariantBuilder.cs");
            string basic = File.ReadAllText(basicPath, new UTF8Encoding(false, true));
            string herta = File.ReadAllText(hertaPath, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(prefab);", basic);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(definition);", herta);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(saved);", herta);
        }

        [Test]
        public void CharacterTemplateDefinitionCreationReclaimsUncommittedObject()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "CharacterTemplates", "ESFormalHertaPlayerVariantBuilder.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int create = source.IndexOf("AssetDatabase.CreateAsset(definition, DefinitionPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string body = source.Substring(Math.Max(0, create - 180), Math.Min(650, source.Length - Math.Max(0, create - 180)));
            StringAssert.Contains("catch", body);
            StringAssert.Contains("AssetDatabase.GetAssetPath(definition)", body);
            StringAssert.Contains("DestroyImmediate(definition)", body);
            int configure = source.IndexOf("definition.motionVariable = EntityMotionVariableData.Default;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(configure, 0);
            Assert.Less(configure, create,
                "新建定义必须先完成配置，再提交 AssetDatabase，避免留下部分配置资产。");
        }

        [Test]
        public void GameCoreSingleAssetMenusUseScopedSaves()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "GameCoreEditorGlobalDataMenu.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.CreateAsset(data, AssetPath);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(data);", source);
            int bake = source.IndexOf("EditorUtility.SetDirty(attributeRoot);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(bake, 0);
            string bakeBody = source.Substring(bake, Math.Min(500, source.Length - bake));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(data);", bakeBody);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(table);", bakeBody);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(root);", bakeBody);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(attributeTable);", bakeBody);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(attributeRoot);", bakeBody);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", bakeBody);
            int snapshot = source.IndexOf("string tableSnapshot = EditorJsonUtility.ToJson(table);", bake, StringComparison.Ordinal);
            Assert.GreaterOrEqual(snapshot, 0);
            string transactionBody = source.Substring(snapshot, Math.Min(1500, source.Length - snapshot));
            StringAssert.Contains("RestoreBakeOutput(table, tableSnapshot)", transactionBody);
            StringAssert.Contains("已恢复本次操作前的输出状态", transactionBody);
            StringAssert.Contains("ImportAssetOptions.ForceSynchronousImport", transactionBody);
        }

        [Test]
        public void CmdAgentCreationUsesScopedAssetSave()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESCmdAgent", "ESCmdAgentWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int create = source.IndexOf("AssetDatabase.CreateAsset(created, DefaultAgentAssetPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string body = source.Substring(create, Math.Min(500, source.Length - create));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(created);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void UISingleTextureSpriteFlowsUseScopedSaves()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(texture);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(existingTexture);", source);
            int font = source.IndexOf("AssetDatabase.CreateAsset(font, ShowcaseFontPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(font, 0);
            string fontBody = source.Substring(font, Math.Min(1800, source.Length - font));
            StringAssert.Contains("AssetDatabase.SaveAssets();", fontBody);
        }

        [Test]
        public void UIShowcaseFontPreflightsShaderBeforeReplacingAssets()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void RebuildCompositeShaderShowcaseFont", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int delete = source.IndexOf("AssetDatabase.DeleteAsset(ShowcaseFontPath)", method, StringComparison.Ordinal);
            int shader = source.IndexOf("Shader.Find(\"TextMeshPro/Distance Field\")", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(delete, 0);
            Assert.GreaterOrEqual(shader, 0);
            Assert.Less(shader, delete, "Shader 预检必须在替换既有展示资产前完成。");
        }

        [Test]
        public void UIShowcaseFontPreflightsFontGenerationBeforeReplacingAssets()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void RebuildCompositeShaderShowcaseFont", StringComparison.Ordinal);
            int delete = source.IndexOf("AssetDatabase.DeleteAsset(ShowcaseFontPath)", method, StringComparison.Ordinal);
            int create = source.IndexOf("TMP_FontAsset.CreateFontAsset", method, StringComparison.Ordinal);
            int readDefinition = source.IndexOf("font.ReadFontAssetDefinition();", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.GreaterOrEqual(delete, 0);
            Assert.GreaterOrEqual(create, 0);
            Assert.GreaterOrEqual(readDefinition, 0);
            Assert.Less(create, delete);
            Assert.Less(readDefinition, delete);
        }

        [Test]
        public void UIShowcaseFontGlyphExpansionUsesScopedSave()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void EnsureShowcaseFontCharacters", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(800, source.Length - method));
            StringAssert.Contains("EditorUtility.SetDirty(font);", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(font);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void LubanLocalizationCatalogUsesScopedSave()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools",
                "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void BuildLubanTextCatalog", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int end = source.IndexOf("private static string GetLocaleValue", method, StringComparison.Ordinal);
            Assert.Greater(end, method);
            string body = source.Substring(method, end - method);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(catalog);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void FontBuildUsesExplicitAssetSaveSet()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESFontTools",
                "ESFontBuildProfileEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int build = source.IndexOf("public static void Build(ESFontBuildProfile profile)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(build, 0);
            int buildEnd = source.IndexOf("public static void BuildFallbacks(ESFontBuildProfile profile)", build, StringComparison.Ordinal);
            Assert.Greater(buildEnd, build);
            string buildBody = source.Substring(build, buildEnd - build);
            StringAssert.Contains("SaveBuildAssets(profile, entries);", buildBody);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", buildBody);
            int saveSet = source.IndexOf("private static void SaveBuildAssets", StringComparison.Ordinal);
            Assert.GreaterOrEqual(saveSet, 0);
            string saveBody = source.Substring(saveSet, Math.Min(900, source.Length - saveSet));
            StringAssert.Contains("SaveAssetIfDirty(profile)", saveBody);
            StringAssert.Contains("SaveAssetIfDirty(entry.outputFont)", saveBody);
            StringAssert.Contains("SaveAssetIfDirty(profile.runtimeCatalog)", saveBody);
        }

        [Test]
        public void UnityPackageToolUsesScopedGlobalConfigSaves()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "AssetsTools", "Simple_AssetTool_Page_UnityPackageTool.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(ESGlobalEditorDefaultConfi.Instance);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void RuntimeLibraryRegistrationUsesScopedLibrarySaves()
        {
            string injectorPath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor",
                "ESRuntimeDataAssetEditorInjector.cs");
            string injector = File.ReadAllText(injectorPath, new UTF8Encoding(false, true));
            StringAssert.Contains("SaveEditorLibraries();", injector);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(library);", injector);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", injector);

            string modulePath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Runtime",
                "GameManager", "Modules", "Runtime", "MODULE_ESRuntimeDataModule.cs");
            string module = File.ReadAllText(modulePath, new UTF8Encoding(false, true));
            int menu = module.IndexOf("MenuRebuildEditorConfigQueryTableFromLibraries", StringComparison.Ordinal);
            Assert.GreaterOrEqual(menu, 0);
            string menuBody = module.Substring(menu, Math.Min(650, module.Length - menu));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(library);", menuBody);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", menuBody);
            StringAssert.Contains("EditorJsonUtility.ToJson(library)", module);
            StringAssert.Contains("EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key)", module);
        }

        [Test]
        public void HybridClrConsumerSettingsUseExplicitSaveSet()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline",
                "ESHybridCLREditorIntegration.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("SavePreparedSettings(consumers);", source);
            StringAssert.Contains("SaveAssetIfDirty(consumer);", source);
            StringAssert.Contains("SaveAssetIfDirty(HybridCLRSettings.Instance);", source);
            StringAssert.Contains("PrepareSettingsSafely", source);
            StringAssert.Contains("EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key)", source);
            StringAssert.Contains("SyncGeneratedPackages(consumer", source);
            StringAssert.Contains("settingsSnapshot", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void MaterialReplacementSavesOnlyChangedPrefabs()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_MaterialReplacement.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int save = source.IndexOf("changedAssetPaths.Distinct", StringComparison.Ordinal);
            Assert.GreaterOrEqual(save, 0);
            string body = source.Substring(Math.Max(0, save - 300), Math.Min(900, source.Length - Math.Max(0, save - 300)));
            StringAssert.Contains("changedAssetPaths.Distinct", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(changedAsset);", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
        }

        [Test]
        public void PrefabApplySavesOnlyCorrespondingSources()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_PrefabManagement.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void SaveAppliedPrefabSources", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1000, source.Length - method));
            StringAssert.Contains("GetCorrespondingObjectFromSource(instance)", body);
            StringAssert.Contains("SaveAssetIfDirty(asset)", body);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void SceneOptimizationDoesNotGloballySaveUnrelatedAssets()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_SceneOptimization.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int refresh = source.LastIndexOf("AssetDatabase.Refresh();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            string body = source.Substring(Math.Max(0, refresh - 300), Math.Min(700, source.Length - Math.Max(0, refresh - 300)));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", body);
            StringAssert.Contains("SaveAndReimport", source);
            StringAssert.Contains("MarkActiveSceneDirtyIfChanged", source);
        }

        [Test]
        public void ResourcePlanExpansionSavesOnlyPlanAssets()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline",
                "ResourcePlan", "Baking", "ESResourcePlanGameCoreExpansion.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(plan);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void ConfigKeySynchronizerSavesOnlyChangedOwners()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline",
                "ResourcePlan", "Baking", "ESResourcePlanConfigKeySynchronizer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("ICollection<UnityEngine.Object> changedOwners", source);
            StringAssert.Contains("changedOwners.Add(owner)", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(owner);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void CommonExampleReadMeInstallerUsesExplicitPrefabAndSceneSaves()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESPresentation",
                "ESCommonExampleReadMeInstaller.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("PrefabUtility.SaveAsPrefabAsset", source);
            StringAssert.Contains("EditorSceneManager.SaveScene", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void UIShowcasePrefabRefreshUsesScopedSaveAndChecksResult()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "UI",
                "ESUIGameScreenMaterializer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void RefreshCompositeShaderShowcasePrefabOnly", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("GameObject prefab = PrefabUtility.SaveAsPrefabAsset", body);
            StringAssert.Contains("if (prefab == null)", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(prefab);", body);
        }

        [Test]
        public void AnimationBatchSingleAssetCreationUsesScopedSaves()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_AnimationBatchSetting.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int controller = source.IndexOf("AssetDatabase.CreateAsset(controller, path);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(controller, 0);
            string controllerBody = source.Substring(controller, Math.Min(450, source.Length - controller));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(controller);", controllerBody);
            int clip = source.IndexOf("AssetDatabase.CreateAsset(clip, path);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(clip, 0);
            string clipBody = source.Substring(clip, Math.Min(350, source.Length - clip));
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(clip);", clipBody);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(sharedController);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(controllerToUse);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(animatorController);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(controller);", source);
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
        }

        [Test]
        public void AnimationBatchCreationReclaimsUncommittedObjects()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_AnimationBatchSetting.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int controller = source.IndexOf("var controller = new AnimatorController();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(controller, 0);
            string controllerBody = source.Substring(controller, Math.Min(1100, source.Length - controller));
            StringAssert.Contains("catch", controllerBody);
            StringAssert.Contains("AssetDatabase.GetAssetPath(controller)", controllerBody);
            StringAssert.Contains("DestroyImmediate(controller)", controllerBody);
            int clip = source.IndexOf("var clip = new AnimationClip();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(clip, 0);
            string clipBody = source.Substring(clip, Math.Min(900, source.Length - clip));
            StringAssert.Contains("catch", clipBody);
            StringAssert.Contains("AssetDatabase.GetAssetPath(clip)", clipBody);
            StringAssert.Contains("DestroyImmediate(clip)", clipBody);
        }

        [Test]
        public void SoDataInfoWindowUsesScopedPackSavesAndNullGuards()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("if (pack == null)", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(packAsset);", source);
            StringAssert.Contains("if (pack is ScriptableObject packAsset && AssetDatabase.Contains(packAsset)", source);
        }

        [Test]
        public void SoDataInfoSelectionRefreshDoesNotTriggerGlobalAssetRefresh()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            string[] selectionRefreshMethods =
            {
                "private void Refresh()\n            {\n                if (ESSODataInfoWindow.UsingWindow != null)\n                    ESSODataInfoWindow.UsingWindow.selectPackTypeName_",
                "private void Refresh()\n            {\n                if (ESSODataInfoWindow.UsingWindow != null)\n                    ESSODataInfoWindow.UsingWindow.selectGroupTypeName_",
                "private void Refresh()\n            {\n                if (ESSODataInfoWindow.UsingWindow != null)\n                {\n                    ESSODataInfoWindow.UsingWindow.selectNormalCategoryName_"
            };
            foreach (string methodContract in selectionRefreshMethods)
                StringAssert.Contains(methodContract, source);
            StringAssert.DoesNotContain(
                "private void Refresh()\n            {\n                AssetDatabase.Refresh();",
                source);
        }

        [Test]
        public void SoDataInfoRenameRecordsUndoBeforeSaving()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SODataInfoWindow", "ESSODataInfoWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int rename = source.IndexOf("public void RenameFileThis()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rename, 0);
            int end = source.IndexOf("private string Title()", rename, StringComparison.Ordinal);
            Assert.Greater(end, rename);
            string body = source.Substring(rename, end - rename);
            StringAssert.Contains("Undo.RecordObject(file, \"重命名数据文件\")", body);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(file);", body);
            StringAssert.DoesNotContain("file.name = renameFile;\n                    AssetDatabase.Refresh();\n                    AssetDatabase.SaveAssetIfDirty(file);", body);
        }

        [Test]
        public void LibraryCreationPageRefreshDoesNotReimportProject()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int refresh = source.IndexOf("public override ESWindowPageBase ES_Refresh()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            int next = source.IndexOf("public void CreateNewLibrary()", refresh, StringComparison.Ordinal);
            Assert.Greater(next, refresh);
            string body = source.Substring(refresh, next - refresh);
            StringAssert.DoesNotContain("AssetDatabase.Refresh();", body);
            StringAssert.Contains("LibName = GetLibTypeName_NewCreate();", body);
        }

        [Test]
        public void ConsumerPageRefreshDoesNotSilentlyCreateStableIdentity()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int refresh = source.LastIndexOf("public override ESWindowPageBase ES_Refresh()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            int next = source.IndexOf("public override void OnPageDisable()", refresh, StringComparison.Ordinal);
            Assert.Greater(next, refresh);
            string body = source.Substring(refresh, next - refresh);
            StringAssert.DoesNotContain("EnsureStableIdentity()", body);
            StringAssert.DoesNotContain("SetDirty", body);
            StringAssert.Contains("生成稳定 ID", source);
            StringAssert.Contains("Undo.RecordObject(resourceConsumer", source);
        }

        [Test]
        public void LibraryMenuBuildDoesNotRenameOrSaveAssetsForDuplicateDisplayNames()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("public void ApplyTemplateToMenuTree", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string body = source.Substring(start);
            StringAssert.DoesNotContain("i.Name +=", body);
            StringAssert.DoesNotContain("List<UnityEngine.Object> modifiedLibraries", body);
            StringAssert.DoesNotContain("List<UnityEngine.Object> modifiedConsumers", body);
            StringAssert.Contains("string displayName = i.Name;", body);
            StringAssert.Contains("displayName = i.Name + \"_re\" + suffix++", body);
        }

        [Test]
        public void AssetQuickInfoEditsRecordUndoBeforeCommit()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorInspector", "InspectorUser_AssetQuickInfo.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("private static void DrawAssetGuide", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private static void DrawAssetRegistryKeys", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            StringAssert.Contains("string nextOwnerSystem", body);
            StringAssert.Contains("string nextRoleTitle", body);
            StringAssert.Contains("string nextResponsibilityHint", body);
            StringAssert.Contains("Undo.RecordObject(data, \"编辑资产职责提示\")", body);
            StringAssert.Contains("record.ownerSystem = nextOwnerSystem", body);
            StringAssert.Contains("record.MarkManuallyEdited();", body);
        }

        [Test]
        public void PreviewDisableFailureCannotSkipDispose()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorPreview", "BasePreviewEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("private void DeactivatePreviewElement", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private void ReleaseAllActivePreviewElements", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            StringAssert.Contains("lifecycle.OnPreviewDisable();", body);
            StringAssert.Contains("lifecycle.DisposePreview();", body);
            StringAssert.Contains("catch (Exception e)", body);
            StringAssert.Contains("activeProviders.Remove(provider);", body);
            int disableCatch = body.IndexOf("lifecycle.OnPreviewDisable();", StringComparison.Ordinal);
            int dispose = body.IndexOf("lifecycle.DisposePreview();", StringComparison.Ordinal);
            Assert.Greater(dispose, disableCatch);
        }

        [Test]
        public void PreviewEnableFailureCleansPartialProviderAndAllowsRetry()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorPreview", "BasePreviewEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("private void ActivatePreviewElement", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private void DeactivatePreviewElement", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            StringAssert.Contains("lifecycle.OnPreviewEnable();", body);
            StringAssert.Contains("lifecycle.OnPreviewDisable();", body);
            StringAssert.Contains("lifecycle.DisposePreview();", body);
            StringAssert.Contains("activeProviders.Remove(provider);", body);
        }

        [Test]
        public void AssetPackagePreviewDisposeDoesNotShortCircuitOnChildFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackagePreviewSession.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int materialStart = source.IndexOf("private void DisposeInstance()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(materialStart, 0);
            int materialEnd = source.IndexOf("public void Dispose()", materialStart, StringComparison.Ordinal);
            Assert.Greater(materialEnd, materialStart);
            string materialBody = source.Substring(materialStart, materialEnd - materialStart);
            StringAssert.Contains("DestroyImmediate(previewObject)", materialBody);
            StringAssert.Contains("DestroyImmediate(previewMaterial)", materialBody);
            StringAssert.Contains("catch (Exception exception)", materialBody);

            int audioStart = source.IndexOf("internal sealed class ESAssetPackageAudioPreviewPlayer", StringComparison.Ordinal);
            Assert.GreaterOrEqual(audioStart, 0);
            int audioDispose = source.IndexOf("public void Dispose()", audioStart, StringComparison.Ordinal);
            int audioEnd = source.IndexOf("internal static class ESAssetPackagePreviewUtility", audioDispose, StringComparison.Ordinal);
            Assert.Greater(audioEnd, audioDispose);
            string audioBody = source.Substring(audioDispose, audioEnd - audioDispose);
            StringAssert.Contains("try { Stop(); }", audioBody);
            StringAssert.Contains("DestroyImmediate(previewAudioObject)", audioBody);
            StringAssert.Contains("try { previewContext.Dispose(); }", audioBody);
        }

        [Test]
        public void AudioPreviewSourceCreationCommitsOnlyAfterPreparation()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("private void EnsurePreviewAudioSource()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private void ApplyAudioSettings()", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            StringAssert.Contains("GameObject nextObject = null;", body);
            StringAssert.Contains("AudioSource nextSource = null;", body);
            StringAssert.Contains("if (!previewContext.PreparePreviewAudioObject(nextObject))", body);
            StringAssert.Contains("AssetPackage 音频预览对象未能进入公共 PreviewScene。", body);
            StringAssert.Contains("previewAudioObject = nextObject;", body);
            StringAssert.Contains("previewSource = nextSource;", body);
            StringAssert.Contains("DestroyImmediate(nextObject)", body);
        }

        [Test]
        public void PreviewRenderFailureRestoresRenderStateAndReturnsFalse()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("public bool Render(Rect rect", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("public Texture2D RenderSnapshot", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("LastStatus = \"Preview render failed: \"", body);
            StringAssert.Contains("return false;", body);
            StringAssert.Contains("Camera.targetTexture = oldTarget;", body);
            StringAssert.Contains("RenderTexture.active = oldActive;", body);
        }

        [Test]
        public void InvalidatedPreviewSceneRebuildsSceneBoundObjects()
        {
            string path = Path.Combine(Application.dataPath, "../Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int ensureStart = source.IndexOf("public void Ensure()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(ensureStart, 0);
            int ensureEnd = source.IndexOf("public bool PreparePreviewObject", ensureStart, StringComparison.Ordinal);
            Assert.Greater(ensureEnd, ensureStart);
            string ensureBody = source.Substring(ensureStart, ensureEnd - ensureStart);
            StringAssert.Contains("Camera != null && !previewScene.IsValid()", ensureBody);
            StringAssert.Contains("ResetSceneBoundPreviewObjects();", ensureBody);

            int resetStart = source.IndexOf("private void ResetSceneBoundPreviewObjects()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(resetStart, 0);
            int resetEnd = source.IndexOf("private void EnsurePreviewScene()", resetStart, StringComparison.Ordinal);
            Assert.Greater(resetEnd, resetStart);
            string resetBody = source.Substring(resetStart, resetEnd - resetStart);
            StringAssert.Contains("DestroyObject(cameraObject)", resetBody);
            StringAssert.Contains("DestroyObject(keyLightObject)", resetBody);
            StringAssert.Contains("DestroyObject(fillLightObject)", resetBody);
            StringAssert.Contains("CameraSceneBound = false;", resetBody);
        }

        [Test]
        public void RenderTextureCreationFailurePreservesPreviousTexture()
        {
            string path = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Runtime",
                "EditorPreview", "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int start = source.IndexOf("private void EnsureRenderTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = source.IndexOf("private static int GetAntiAliasing", start, StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = source.Substring(start, end - start);
            int create = body.IndexOf("CreateRenderTexture(", StringComparison.Ordinal);
            int nullCheck = body.IndexOf("if (replacement == null)", create, StringComparison.Ordinal);
            int previous = body.IndexOf("RenderTexture previous = renderTexture;", nullCheck, StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            Assert.Greater(nullCheck, create);
            Assert.Greater(previous, nullCheck);
            StringAssert.Contains("ReleaseRenderTexture(ref previous)", body);
        }

        [Test]
        public void PreviewObjectPreparationRejectsUnmanagedObjects()
        {
            string sessionPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackagePreviewSession.cs");
            string session = File.ReadAllText(sessionPath, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int prepare = session.IndexOf("public bool PreparePreviewObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(prepare, 0);
            int audioPrepare = session.IndexOf("public bool PreparePreviewAudioObject", prepare, StringComparison.Ordinal);
            Assert.Greater(audioPrepare, prepare);
            StringAssert.Contains("if (EditorUtility.IsPersistent(instance))", session.Substring(prepare, audioPrepare - prepare));
            StringAssert.Contains("if (!PreparePreviewObject(instance))", session.Substring(audioPrepare, Math.Min(500, session.Length - audioPrepare)));

            string corePath = Path.Combine(Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string core = File.ReadAllText(corePath, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            int corePrepare = core.IndexOf("public bool PreparePreviewObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(corePrepare, 0);
            StringAssert.Contains("EditorUtility.IsPersistent(obj)", core.Substring(corePrepare, Math.Min(1400, core.Length - corePrepare)));
            StringAssert.Contains("return markerRegistered", core.Substring(corePrepare, Math.Min(1400, core.Length - corePrepare)));
        }

        [Test]
        public void LevelValidationGeneratorScopesGeneratedAssetSaves()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline",
                "ESLevelAssetValidationGenerator.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("private static void SaveGeneratedAssets()", source);
            StringAssert.Contains("AssetDatabase.FindAssets(string.Empty, new[] { Root })", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", source);
        }

        [Test]
        public void ResourceWindowRefreshDoesNotGloballySaveProjectAssets()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "ResWindow", "ESResWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("public override void ES_SaveData()", source);
            StringAssert.Contains("AssetDatabase.Refresh();", source);
        }

        [Test]
        public void GameCoreItemRepairSavesOnlyOwningAssets()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESGameCoreConfigKeyDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("private static void SaveItemAssets", source);
            StringAssert.Contains("AssetDatabase.LoadMainAssetAtPath(path)", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(asset);", source);
        }

        [Test]
        public void AssetBundlePlannerPersistsOnlyChangedImporterSettings()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESResPipeline",
                "ESAssetBundleBuildPlanner.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("AssetDatabase.WriteImportSettingsIfDirty(assignment.assetPath);", source);
            StringAssert.Contains("AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);", source);
            StringAssert.Contains("AssetDatabase.RemoveUnusedAssetBundleNames();", source);
        }

        [Test]
        public void LibraryTemplateUsesScopedDirtyAssetSaves()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("AssetDatabase.SaveAssets();", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(lib);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(libraryAsset);", source);
            StringAssert.Contains("AssetDatabase.SaveAssetIfDirty(consumerAsset);", source);
            StringAssert.Contains("bool assetCommitted = false;", source);
            StringAssert.Contains("AssetDatabase.DeleteAsset(path);", source);
        }

        [Test]
        public void FontBuildPageReclaimsUncommittedProfileOnCreateFailure()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "FontToolsWindow", "Page_FontBuild.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int create = source.IndexOf("AssetDatabase.CreateAsset(asset, path);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string body = source.Substring(create, Math.Min(420, source.Length - create));
            StringAssert.Contains("catch", body);
            StringAssert.Contains("EditorUtility.IsPersistent(asset)", body);
            StringAssert.Contains("DestroyImmediate(asset)", body);
        }

        [Test]
        public void FontPreviewCleanupClearsHandlesBeforeBestEffortRelease()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "FontToolsWindow", "Page_FontBuild.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int dispose = source.IndexOf("private void DisposePreview()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            string body = source.Substring(dispose, Math.Min(1300, source.Length - dispose));
            StringAssert.Contains("ESEditorPreviewModelHandle model = previewModel", body);
            StringAssert.Contains("previewModel = null", body);
            StringAssert.Contains("GameObject objectToDestroy = previewObject", body);
            StringAssert.Contains("previewObject = null", body);
            StringAssert.Contains("ESEditorPreviewRenderContext context = previewContext", body);
            StringAssert.Contains("previewContext = null", body);
            StringAssert.Contains("catch (Exception exception)", body);
        }

        [Test]
        public void LibraryIndexReleasesStaticPreviewStyleTextureOnReloadAndQuit()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void ReleaseStaticStyles", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1100, source.Length - method));
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(buttonBackground)", body);
            StringAssert.Contains("buttonBackground = null", body);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload", source);
            StringAssert.Contains("EditorApplication.quitting", source);
        }

        [Test]
        public void LibraryIndexClearsInstanceThumbnailReferencesOnPageDisable()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "ESLibraryTemplate.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int disable = source.IndexOf("public override void OnPageDisable()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(disable, 0);
            string body = source.Substring(disable, Math.Min(1300, source.Length - disable));
            StringAssert.Contains("thumbnailCache.Clear();", body);
            StringAssert.Contains("thumbnailCacheOrder.Clear();", body);
        }

        [Test]
        public void SceneOptimizationAbortsWhenRequestedBackupFails()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "SimpleToolsWindow", "HierchyTools", "Simple_HierchyTool_Page_SceneOptimization.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private bool BackupScene()", source);
            StringAssert.Contains("if (!BackupScene())", source);
            StringAssert.Contains("return false;", source.Substring(source.IndexOf("private bool BackupScene()", StringComparison.Ordinal)));
            StringAssert.Contains("无法创建回滚备份；已中止优化", source);
        }

        [Test]
        public void AgentArtifactLauncherStopsOnEditorQuit()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2",
                "ESAgentArtifactGenerationWorkflow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int start = source.IndexOf("AssemblyReloadEvents.beforeAssemblyReload += DetachForReload", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            string body = source.Substring(start, Math.Min(650, source.Length - start));
            StringAssert.Contains("EditorApplication.quitting += DetachForReload", body);
            StringAssert.Contains("EditorApplication.quitting -= DetachForReload", source);
        }

        [Test]
        public void AutomationBridgeCancelsActiveRunsOnLifecycleStop()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESAutomation",
                "ESAutomationAiBridge.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("CancelActiveRunsForLifecycle", source);
            StringAssert.Contains("ESAutomationRunStatus.Cancelled", source);
            StringAssert.Contains("active.Execution.Terminate()", source);
            StringAssert.Contains("active.Execution.Dispose()", source);
        }

        [Test]
        public void FeishuAutomationRegistersLifecycleStopForActiveWorkers()
        {
            string read = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/Plugins/ES/Editor/ESAutomation/ESFeishuReadAutomation.cs"), Encoding.UTF8);
            string task = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/Plugins/ES/Editor/ESAutomation/ESFeishuTaskAutomation.cs"), Encoding.UTF8);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += StopActiveRunsForLifecycle", read);
            StringAssert.Contains("EditorApplication.quitting += StopActiveRunsForLifecycle", read);
            StringAssert.Contains("active.execution.Terminate()", read);
            StringAssert.Contains("active.execution.Dispose()", read);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += StopActiveRunsForLifecycle", task);
            StringAssert.Contains("EditorApplication.quitting += StopActiveRunsForLifecycle", task);
            StringAssert.Contains("active.Execution.Terminate()", task);
            StringAssert.Contains("active.Execution.Dispose()", task);
        }

        [Test]
        public void ForegroundCmdObserverReleasesNativeHookOnEditorLifecycle()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentForegroundCmdObserver.cs"), Encoding.UTF8);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += ReleaseAllForLifecycle", source);
            StringAssert.Contains("EditorApplication.quitting += ReleaseAllForLifecycle", source);
            StringAssert.Contains("observerReferenceCount = 0", source);
            StringAssert.Contains("UnhookWinEvent(foregroundChangedHook)", source);
            StringAssert.Contains("foregroundChangedHook = IntPtr.Zero", source);
        }

        [Test]
        public void SceneViewBaselineTimeoutIsRetryableAndNotReportedAsCorruption()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/Plugins/ES/Editor/EditorTools/SceneHierarchyExpansionState.cs"), Encoding.UTF8);
            StringAssert.Contains("bool hasSceneView = SceneView.sceneViews != null && SceneView.sceneViews.Count > 0", source);
            StringAssert.Contains("if (hasSceneView)", source);
            StringAssert.Contains("CancelSceneViewCameraBaseline();", source);
            StringAssert.Contains("下次场景视图绘制时将重试", source);
            StringAssert.DoesNotContain("Debug.LogWarning(\"[SceneHierarchyExpansionState] 当前未收到 SceneView 绘制帧", source);
        }

        [Test]
        public void SerializedMutationDisposesViewsAfterCommitOrRollback()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESEditorSerializedMutation.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int tryBlock = source.IndexOf("try\n            {\n                Undo.IncrementCurrentGroup", StringComparison.Ordinal);
            Assert.GreaterOrEqual(tryBlock, 0);
            string body = source.Substring(tryBlock, Math.Min(3600, source.Length - tryBlock));
            StringAssert.Contains("finally", body);
            StringAssert.Contains("DisposeSerializedObjects(serializedObjects);", body);
        }

        [Test]
        public void PolymorphicDrawerDisposesViewWhenPropertyValidationFails()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESPolymorphicReferenceDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int create = source.IndexOf("serializedObject = new SerializedObject(target);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string body = source.Substring(create, Math.Min(1250, source.Length - create));
            StringAssert.Contains("finally", body);
            StringAssert.Contains("serializedObject?.Dispose();", body);
        }

        [Test]
        public void FeedbackSoundSchemeCreationContainsFilesystemFailureBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorFeedbackSound", "ESEditorFeedbackSound.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void CreateScheme", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(900, source.Length - method));
            StringAssert.Contains("Directory.CreateDirectory", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void FeedbackSoundPreviewHasReloadAndQuitCleanup()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorFeedbackSound", "ESEditorFeedbackSound.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("[InitializeOnLoadMethod]", source);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += CleanupPreviewLifecycle", source);
            StringAssert.Contains("EditorApplication.quitting += CleanupPreviewLifecycle", source);
            StringAssert.Contains("StopSchemePreview();", source);
            StringAssert.Contains("DestroyPreviewHost();", source);
            StringAssert.Contains("StopNativePlayback();", source);
            StringAssert.Contains("previewHost = null;", source);
            StringAssert.Contains("previewSource = null;", source);
        }

        [Test]
        public void EditorHandleClearsQueuedTasksAtLifecycleBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESEditorHandle", "ESEditorHandle.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("[InitializeOnLoadMethod]", source);
            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += CleanupForLifecycle", source);
            StringAssert.Contains("EditorApplication.quitting += CleanupForLifecycle", source);
            StringAssert.Contains("ForceClearAllTasks();", source);
            StringAssert.Contains("EditorApplication.update -= Update;", source);
        }

        [Test]
        public void EditorIconCachesDropInvalidatedUnityObjects()
        {
            string resolverPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "-ESMenuTreeWindow.cs");
            string presentationPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESPresentation",
                "Core", "ESEditorPresentationCore.cs");
            string resolver = File.ReadAllText(resolverPath, new UTF8Encoding(false, true));
            string presentation = File.ReadAllText(presentationPath, new UTF8Encoding(false, true));
            StringAssert.Contains("Cache.Remove(normalized);", resolver);
            StringAssert.Contains("BrandCache.Remove(iconName);", resolver);
            StringAssert.Contains("esBrandIconCache.Remove(key);", presentation);
        }

        [Test]
        public void EditorIconCachesRemainBounded()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "-Templates", "-ESMenuTreeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("MaxCachedIcons = 128", source);
            StringAssert.Contains("MaxCachedBrandIcons = 64", source);
            StringAssert.Contains("TrimCache(Cache, MaxCachedIcons);", source);
            StringAssert.Contains("TrimCache(BrandCache, MaxCachedBrandIcons);", source);
        }

        [Test]
        public void CompactChoicePopupCreateGUIIsIdempotent()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int createGui = source.IndexOf("public void CreateGUI()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(createGui, 0);
            string body = source.Substring(createGui, Math.Min(900, source.Length - createGui));
            StringAssert.Contains("UnregisterCallback<KeyDownEvent>", body);
            StringAssert.Contains("rootVisualElement.Clear();", body);
        }

        [Test]
        public void PreviewContextRetainsRetryPathWhenPreviewSceneCloseFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int context = source.IndexOf(
                "public sealed class ESEditorPreviewRenderContext",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(context, 0);
            int dispose = source.IndexOf("public void Dispose()", context, StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            string body = source.Substring(dispose, Math.Min(3200, source.Length - dispose));
            StringAssert.Contains("previewSceneCloseFailed", body);
            StringAssert.Contains("RegisterScope(this);", body);
            StringAssert.Contains("return;", body);
            StringAssert.Contains("disposed = true;", body);
        }

        [Test]
        public void CommandPaletteSearchKeepsOnlyTopKScoredCandidates()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("AddTopScoredItem(new ScoredItem(item, score));", source);
            StringAssert.Contains("scoredItems.BinarySearch(candidate, comparer)", source);
            StringAssert.Contains("scoredItems.RemoveAt(scoredItems.Count - 1);", source);
            StringAssert.DoesNotContain("scoredItems.Sort(ScoredItemComparer.Instance);", source);
        }

        [Test]
        public void CommandPaletteTokenizesQueryOncePerSearch()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("term.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)", source);
            StringAssert.Contains("Score(item, term, tokens)", source);
            StringAssert.DoesNotContain("string[] tokens = term.Split(' ');", source);
        }

        [Test]
        public void AssetPreviewCacheInvalidatesOnProjectChange()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.projectChanged += ClearPreviewCacheAfterProjectChange", source);
            StringAssert.Contains("EditorApplication.projectChanged -= ClearPreviewCacheAfterProjectChange", source);
            StringAssert.Contains("ESAssetPackagePreviewUtility.ClearPreviewCache();", source);
        }

        [Test]
        public void CompositeShaderDefaultsInvalidateOnProjectChange()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESShader",
                "ESCompositeShaderGUI.MaterialState.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("EditorApplication.projectChanged += ReleaseDefaults", source);
            StringAssert.Contains("Defaults.Clear();", source);
        }

        [Test]
        public void ShaderBakeWindowDropsSnapshotOnProjectChange()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESShader",
                "ESCompositeShaderBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private void OnProjectChange()", source);
            StringAssert.Contains("ReleaseBakedTexture();", source);
            StringAssert.Contains("项目资源已变化，请重新生成烘焙预览。", source);
        }

        [Test]
        public void SearchDropdownAddRangeUsesTheSameBoundedFailureContract()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int addRange = source.IndexOf("public Builder AddRange<T>", StringComparison.Ordinal);
            Assert.GreaterOrEqual(addRange, 0);
            string body = source.Substring(addRange, Math.Min(1800, source.Length - addRange));
            StringAssert.Contains("count >= MaximumResolvedEntries", body);
            StringAssert.Contains("候选数据加载失败，请查看 Console", body);
            StringAssert.Contains("AddRange 数据构建失败", body);
            StringAssert.Contains("values is ICollection<T> collection", body);
            StringAssert.Contains("entries.Capacity", body);
        }

        [Test]
        public void SearchDropdownOpenItemsIsBoundedBeforeMaterializingEntries()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void OpenItems<T>", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1900, source.Length - method));
            StringAssert.Contains("values is ICollection<T> collection", body);
            StringAssert.Contains("new List<Entry>(capacity)", body);
            StringAssert.Contains("count >= MaximumResolvedEntries", body);
            StringAssert.Contains("候选项过多，已限制为 " + "MaximumResolvedEntries", body);
        }

        [Test]
        public void SearchDropdownProviderPreallocatesBoundedCollectionResults()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private IReadOnlyList<Entry> ResolveEntries()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1100, source.Length - method));
            StringAssert.Contains("result is ICollection<Entry> collection", body);
            StringAssert.Contains("new List<Entry>(capacity)", body);
            StringAssert.Contains("resolved.Count >= MaximumResolvedEntries", body);
        }

        [Test]
        public void SearchDropdownToolbarActionsHaveAnExplicitBound()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("internal const int MaximumToolbarActions = 32", source);
            StringAssert.Contains("toolbarActions.Count >= MaximumToolbarActions", source);
            StringAssert.Contains("ESSearchDropdown.MaximumToolbarActions", source);
        }

        [Test]
        public void SearchDropdownDuplicateIdRepairIsBounded()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("protected override AdvancedDropdownItem BuildRoot()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1900, source.Length - method));
            StringAssert.Contains("for (int attempts = 0; attempts <= MaximumResolvedEntries; attempts++)", body);
            StringAssert.Contains("disambiguatedId == int.MaxValue", body);
            StringAssert.Contains("候选项 ID 冲突，已跳过", body);
        }

        [Test]
        public void SearchDropdownGroupPathMaterializationIsBounded()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumGroupPathCharacters = 4096", source);
            StringAssert.Contains("MaximumGroupPathSegments = 64", source);
            StringAssert.Contains("MaximumGroupSegmentCharacters = 256", source);
            int method = source.IndexOf("private static AdvancedDropdownItem ResolveGroup", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("groupPath.Length > MaximumGroupPathCharacters", body);
            StringAssert.Contains("segmentCount++ >= MaximumGroupPathSegments", body);
            StringAssert.Contains("segment.Substring(0, MaximumGroupSegmentCharacters)", body);
        }

        [Test]
        public void SearchDropdownTooltipReflectionFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int find = source.IndexOf("private static PropertyInfo FindNativeTooltipProperty", StringComparison.Ordinal);
            Assert.GreaterOrEqual(find, 0);
            string findBody = source.Substring(find, Math.Min(1100, source.Length - find));
            StringAssert.Contains("catch (Exception)", findBody);
            int create = source.IndexOf("private static Action<AdvancedDropdownItem, string> CreateNativeTooltipSetter", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            string createBody = source.Substring(create, Math.Min(1200, source.Length - create));
            StringAssert.Contains("catch (Exception)", createBody);
            StringAssert.Contains("return null;", createBody);
        }

        [Test]
        public void SearchDropdownNativeBridgeFieldReflectionFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int find = source.IndexOf("private static FieldInfo FindField", StringComparison.Ordinal);
            Assert.GreaterOrEqual(find, 0);
            string body = source.Substring(find, Math.Min(900, source.Length - find));
            StringAssert.Contains("catch (Exception)", body);
            StringAssert.Contains("return null;", body);
            StringAssert.Contains("NativeWindowField?.GetValue(dropdown)", source);
        }

        [Test]
        public void SearchDropdownNativeBridgeCleanupDoesNotShortCircuit()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int dispose = source.IndexOf("public void Dispose()", source.IndexOf("private sealed class WindowState", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            int end = source.IndexOf("private void OnDetachedFromPanel", dispose, StringComparison.Ordinal);
            Assert.Greater(end, dispose);
            string body = source.Substring(dispose, end - dispose);
            StringAssert.Contains("root?.UnregisterCallback", body);
            StringAssert.Contains("toolbar?.Element.RemoveFromHierarchy();", body);
            StringAssert.Contains("interactionHold?.Dispose();", body);
            StringAssert.Contains("WindowStates.Remove(window);", body);
            StringAssert.Contains("finally", body);
        }

        [Test]
        public void SearchDropdownBridgeHoldCleanupFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int helper = source.IndexOf("private static void DisposeInteractionHold", StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            string body = source.Substring(helper, Math.Min(900, source.Length - helper));
            StringAssert.Contains("interactionHold.Dispose();", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("宿主交互保持释放失败", body);
            int bridge = source.IndexOf("internal static bool TryAttach", StringComparison.Ordinal);
            Assert.GreaterOrEqual(bridge, 0);
            StringAssert.Contains("DisposeInteractionHold(interactionHold);", source.Substring(bridge, 2600));
        }

        [Test]
        public void SearchDropdownToolbarIgnoresCallbacksAfterBridgeDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int toolbar = source.IndexOf("private sealed class ToolbarOverlay", StringComparison.Ordinal);
            Assert.GreaterOrEqual(toolbar, 0);
            int draw = source.IndexOf("private void Draw()", toolbar, StringComparison.Ordinal);
            Assert.Greater(draw, toolbar);
            string drawBody = source.Substring(draw, Math.Min(700, source.Length - draw));
            StringAssert.Contains("if (disposed)", drawBody);
            StringAssert.Contains("return;", drawBody);
            int dispose = source.IndexOf("toolbar?.Dispose();", source.IndexOf("public void Dispose()", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Greater(dispose, 0);
        }

        [Test]
        public void CommandPaletteIgnoresFinalGuiAfterDisable()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int onGui = source.IndexOf("private void OnGUI()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(onGui, 0);
            string body = source.Substring(onGui, Math.Min(520, source.Length - onGui));
            StringAssert.Contains("if (!lifecycleActive)", body);
            StringAssert.Contains("return;", body);
            StringAssert.Contains("EnsureStyles();", body);
        }

        [Test]
        public void CommandPaletteFailsClosedWhenExecutorReturnsNull()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int execute = source.IndexOf("private void ExecuteItem", StringComparison.Ordinal);
            Assert.GreaterOrEqual(execute, 0);
            int end = source.IndexOf("private static void PlaySuccessKind", execute, StringComparison.Ordinal);
            Assert.Greater(end, execute);
            string body = source.Substring(execute, end - execute);
            StringAssert.Contains("if (result == null)", body);
            StringAssert.Contains("命令执行器未返回结果", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void CommandPaletteRefreshRestoresPreviousIndexOnProviderFailure()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int refresh = source.IndexOf("public static void Refresh()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            int end = source.IndexOf("public static bool TryGet", refresh, StringComparison.Ordinal);
            Assert.Greater(end, refresh);
            string body = source.Substring(refresh, end - refresh);
            StringAssert.Contains("previousItems", body);
            StringAssert.Contains("previousOrderedItems", body);
            StringAssert.Contains("refreshSucceeded", body);
            StringAssert.Contains("已保留上一次有效索引", body);
            StringAssert.Contains("Items.Add(pair.Key, pair.Value);", body);
        }

        [Test]
        public void CommandPaletteRegistryExposesReadOnlyCollectionViews()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("using System.Collections.ObjectModel;", source);
            StringAssert.Contains("OrderedItemsView = OrderedItems.AsReadOnly()", source);
            StringAssert.Contains("DiagnosticsView = Diagnostics.AsReadOnly()", source);
            StringAssert.Contains("FavoriteIdsView = FavoriteIds.AsReadOnly()", source);
            StringAssert.Contains("RecentIdsView = RecentIds.AsReadOnly()", source);
            StringAssert.Contains("return OrderedItemsView;", source);
            StringAssert.Contains("return FavoriteIdsView;", source);
        }

        [Test]
        public void CommandPaletteSearchEngineDoesNotExposeMutableResults()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int engine = source.IndexOf("public sealed class ESCommandPaletteSearchEngine", StringComparison.Ordinal);
            Assert.GreaterOrEqual(engine, 0);
            int nextType = source.IndexOf("public static class", engine + 20, StringComparison.Ordinal);
            string body = source.Substring(engine, nextType > engine ? nextType - engine : source.Length - engine);
            StringAssert.Contains("resultsView = results.AsReadOnly();", body);
            StringAssert.Contains("public IReadOnlyList<ESCommandPaletteItem> Results => resultsView;", body);
            StringAssert.Contains("return resultsView;", body);
        }

        [Test]
        public void CommandPaletteSearchBoundsQueryAndMalformedItems()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int engine = source.IndexOf("public sealed class ESCommandPaletteSearchEngine", StringComparison.Ordinal);
            Assert.GreaterOrEqual(engine, 0);
            int nextType = source.IndexOf("public static class", engine + 20, StringComparison.Ordinal);
            string body = source.Substring(engine, nextType > engine ? nextType - engine : source.Length - engine);
            StringAssert.Contains("MaximumQueryCharacters = 1024", body);
            StringAssert.Contains("text.Length > MaximumQueryCharacters", body);
            StringAssert.Contains("string.IsNullOrEmpty(item.Title)", body);
            StringAssert.Contains("string.IsNullOrEmpty(item.SearchText)", body);
        }

        [Test]
        public void CommandPaletteSearchSortHasStableIdTieBreak()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int comparer = source.IndexOf("private sealed class ScoredItemComparer", StringComparison.Ordinal);
            Assert.GreaterOrEqual(comparer, 0);
            int end = source.IndexOf("private readonly struct", comparer, StringComparison.Ordinal);
            Assert.Greater(end, comparer);
            string body = source.Substring(comparer, end - comparer);
            StringAssert.Contains("left.Item.Title", body);
            StringAssert.Contains("left.Item.StableId", body);
            StringAssert.Contains("right.Item.StableId", body);
        }

        [Test]
        public void CommandPaletteRestoreShortcutFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int settings = source.IndexOf("public static class ESCommandPaletteShortcutSettings", StringComparison.Ordinal);
            Assert.GreaterOrEqual(settings, 0);
            int restore = source.IndexOf("public static void RestoreDefaultBinding()", settings, StringComparison.Ordinal);
            Assert.GreaterOrEqual(restore, 0);
            int end = source.IndexOf("public static string FindConflictingShortcutId", restore, StringComparison.Ordinal);
            Assert.Greater(end, restore);
            string body = source.Substring(restore, end - restore);
            StringAssert.Contains("EditorPrefs.SetBool(EnabledKey, true);", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("默认快捷键恢复失败", body);
        }

        [Test]
        public void CommandPaletteTextureCleanupDoesNotShortCircuit()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int cleanup = source.IndexOf("private static void DestroyCreatedTextures()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(cleanup, 0);
            int end = source.IndexOf("        }", cleanup + 20, StringComparison.Ordinal);
            Assert.Greater(end, cleanup);
            string body = source.Substring(cleanup, Math.Min(950, source.Length - cleanup));
            StringAssert.Contains("try", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("CreatedTextures.Clear();", body);
        }

        [Test]
        public void CompactChoicePopupGuardsReentrantSelection()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void Select(int index)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(750, source.Length - method));
            StringAssert.Contains("selectionInProgress || index < 0", body);
            StringAssert.Contains("selectionInProgress = true;", body);
            StringAssert.Contains("finally { Close(); }", body);
            StringAssert.Contains("selectionInProgress = false;", source);
        }

        [Test]
        public void CompactChoicePopupRebuildKeepsOneFocusSchedule()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private IVisualElementScheduledItem focusSchedule;", source);
            StringAssert.Contains("focusSchedule?.Pause();", source);
            StringAssert.Contains("focusSchedule = rootVisualElement.schedule.Execute(FocusCurrent)", source);
            StringAssert.Contains("focusSchedule = null;", source);
        }

        [Test]
        public void CompactChoicePopupTracksHostLossAfterOpening()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private EditorWindow hostWindow;", source);
            StringAssert.Contains("popup.hostWindow = hostWindow;", source);
            StringAssert.Contains("popup.CloseIfContextWasLost", source);
            StringAssert.Contains("!configured || hostWindow == null", source);
            StringAssert.Contains("hostWindow = null;", source);
        }

        [Test]
        public void CompactChoicePopupClosesOnHostPanelDetachWithoutPolling()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private VisualElement hostRoot;", source);
            StringAssert.Contains("hostRoot?.RegisterCallback<DetachFromPanelEvent>(popup.OnHostDetached);", source);
            StringAssert.Contains("hostRoot?.UnregisterCallback<DetachFromPanelEvent>(OnHostDetached);", source);
            int method = source.IndexOf("private void OnHostDetached", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            StringAssert.Contains("Close();", source.Substring(method, Math.Min(260, source.Length - method)));
        }

        [Test]
        public void CompactChoicePopupHoldReleaseClearsReferenceBeforeDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void ReleaseHostInteractionHold", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int end = source.IndexOf("private void OnKeyDown", method, StringComparison.Ordinal);
            Assert.Greater(end, method);
            string body = source.Substring(method, end - method);
            StringAssert.Contains("IDisposable hold = hostInteractionHold;", body);
            StringAssert.Contains("hostInteractionHold = null;", body);
            StringAssert.Contains("hold.Dispose();", body);
            StringAssert.Contains("catch (Exception exception)", body);
        }

        [Test]
        public void AdvancedDialogReinitializeIsolatesOldHoldRelease()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int initialize = source.IndexOf("private void Initialize(ESAdvancedDialogRequest value)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(initialize, 0);
            int end = source.IndexOf("public void CreateGUI()", initialize, StringComparison.Ordinal);
            Assert.Greater(end, initialize);
            string body = source.Substring(initialize, end - initialize);
            StringAssert.Contains("IDisposable previousHold = ownerInteractionHold;", body);
            StringAssert.Contains("ownerInteractionHold = null;", body);
            StringAssert.Contains("previousHold.Dispose();", body);
            StringAssert.Contains("catch (Exception exception)", body);
        }

        [Test]
        public void AdvancedDialogBeginBusyRollsBackPartialProgressSetup()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int begin = source.IndexOf("private void BeginBusy", StringComparison.Ordinal);
            Assert.GreaterOrEqual(begin, 0);
            int end = source.IndexOf("private void RefreshBusyOverlay", begin, StringComparison.Ordinal);
            Assert.Greater(end, begin);
            string body = source.Substring(begin, end - begin);
            StringAssert.Contains("activeProgress = null;", body);
            StringAssert.Contains("ESProgressCenter.Begin", body);
            StringAssert.Contains("catch", body);
            StringAssert.Contains("failedProgress?.Cancel", body);
            StringAssert.Contains("busy = false;", body);
            StringAssert.Contains("busyMessage = string.Empty;", body);
        }

        [Test]
        public void AdvancedDialogAsyncValidationFailureGuardsRebuiltVisualTree()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int catchCallback = source.IndexOf("validationMessage = \"异步校验发生异常", StringComparison.Ordinal);
            Assert.GreaterOrEqual(catchCallback, 0);
            int end = source.IndexOf("Debug.LogException(exception);", catchCallback, StringComparison.Ordinal);
            Assert.Greater(end, catchCallback);
            string body = source.Substring(catchCallback, end - catchCallback);
            StringAssert.Contains("if (validationPanel != null)", body);
            StringAssert.Contains("if (validationLabel != null)", body);
        }

        [Test]
        public void AdvancedDialogValidationDelayCallbackRestoresPendingStateOnFailure()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void QueueValidationDelayCallback", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            int end = source.IndexOf("private void RevealInvalidField", method, StringComparison.Ordinal);
            Assert.Greater(end, method);
            string body = source.Substring(method, end - method);
            StringAssert.Contains("try", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("asyncValidationPending = false;", body);
            StringAssert.Contains("异步校验结果更新失败", body);
            StringAssert.Contains("SetButtonEnabled(confirmButton, false)", body);
        }

        [Test]
        public void ProgressCenterCleanupIsolatesCancellationFailures()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int helper = source.IndexOf("private static void DisposeCancellationSource", StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            int end = source.IndexOf("private static void CancelAndDisposeCancellationSource", helper, StringComparison.Ordinal);
            Assert.Greater(end, helper);
            string body = source.Substring(helper, end - helper);
            StringAssert.Contains("try", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("cancellation.Dispose();", body);
            int shutdown = source.IndexOf("private static void Shutdown()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(shutdown, 0);
            string shutdownBody = source.Substring(shutdown, Math.Min(1450, source.Length - shutdown));
            StringAssert.Contains("try", shutdownBody);
            StringAssert.Contains("关闭回调执行失败", shutdownBody);
            StringAssert.Contains("CancelAndDisposeCancellationSource", shutdownBody);
        }

        [Test]
        public void ProgressCenterWindowIgnoresRefreshAfterDisable()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int window = source.IndexOf("public sealed class ESProgressCenterWindow", StringComparison.Ordinal);
            Assert.GreaterOrEqual(window, 0);
            int refresh = source.IndexOf("internal void RefreshNow()", window, StringComparison.Ordinal);
            Assert.Greater(refresh, window);
            int refreshEnd = source.IndexOf("private VisualElement CreateTaskRow", refresh, StringComparison.Ordinal);
            Assert.Greater(refreshEnd, refresh);
            string refreshBody = source.Substring(refresh, refreshEnd - refresh);
            StringAssert.Contains("if (!lifecycleActive || content == null)", refreshBody);
            StringAssert.Contains("private bool lifecycleActive;", source.Substring(window, refresh));
            StringAssert.Contains("lifecycleActive = false;", source.Substring(refreshEnd, Math.Min(1200, source.Length - refreshEnd)));
        }

        [Test]
        public void ProgressCenterWindowPrunesStaleExpandedTaskIds()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int refresh = source.IndexOf("internal void RefreshNow()", source.IndexOf("public sealed class ESProgressCenterWindow", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            int end = source.IndexOf("content.Clear();", refresh, StringComparison.Ordinal);
            Assert.Greater(end, refresh);
            string body = source.Substring(refresh, end - refresh);
            StringAssert.Contains("visibleIds", body);
            StringAssert.Contains("expandedIds.RemoveWhere", body);
            StringAssert.Contains("snapshots[i].id", body);
        }

        [Test]
        public void ProgressCenterCancelEntryPointsFailClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int handle = source.IndexOf("public void RequestCancel()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(handle, 0);
            int handleEnd = source.IndexOf("public void Complete", handle, StringComparison.Ordinal);
            Assert.Greater(handleEnd, handle);
            string handleBody = source.Substring(handle, handleEnd - handle);
            StringAssert.Contains("catch (Exception exception)", handleBody);
            StringAssert.Contains("取消回调执行失败", handleBody);
            int center = source.IndexOf("public static bool RequestCancel", StringComparison.Ordinal);
            Assert.GreaterOrEqual(center, 0);
            int centerEnd = source.IndexOf("public static void DismissCompleted", center, StringComparison.Ordinal);
            Assert.Greater(centerEnd, center);
            string centerBody = source.Substring(center, centerEnd - center);
            StringAssert.Contains("catch (Exception exception)", centerBody);
            StringAssert.Contains("return false;", centerBody);
        }

        [Test]
        public void ProgressCenterBeginRollsBackWhenUpdateSubscriptionFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int begin = source.IndexOf("public static ESProgressHandle Begin", StringComparison.Ordinal);
            Assert.GreaterOrEqual(begin, 0);
            int end = source.IndexOf("public static void Run", begin, StringComparison.Ordinal);
            Assert.Greater(end, begin);
            string body = source.Substring(begin, end - begin);
            StringAssert.Contains("EnsureUpdateSubscription();", body);
            StringAssert.Contains("records.Remove(record);", body);
            StringAssert.Contains("DisposeCancellationSource(record.cancellation);", body);
            StringAssert.Contains("catch", body);
        }

        [Test]
        public void ProgressCenterRowsRejectCallbacksFromOlderRefreshGeneration()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int window = source.IndexOf("public sealed class ESProgressCenterWindow", StringComparison.Ordinal);
            int refresh = source.IndexOf("internal void RefreshNow()", window, StringComparison.Ordinal);
            int row = source.IndexOf("private VisualElement CreateTaskRow", refresh, StringComparison.Ordinal);
            Assert.Greater(row, refresh);
            string body = source.Substring(refresh, Math.Min(2600, source.Length - refresh));
            StringAssert.Contains("refreshGeneration", body);
            StringAssert.Contains("generation == refreshGeneration", body);
            StringAssert.Contains("CreateTaskRow(snapshots[i], generation)", body);
        }

        [Test]
        public void CompactChoicePopupCreationFailureClearsActiveInstance()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int open = source.IndexOf("public static bool Open", StringComparison.Ordinal);
            Assert.GreaterOrEqual(open, 0);
            int catchStart = source.IndexOf("catch", open, StringComparison.Ordinal);
            Assert.GreaterOrEqual(catchStart, 0);
            string body = source.Substring(catchStart, Math.Min(700, source.Length - catchStart));
            StringAssert.Contains("popup.ReleaseHostInteractionHold();", body);
            StringAssert.Contains("popup.Close();", body);
            StringAssert.Contains("ReferenceEquals(activePopup, popup)", body);
            StringAssert.Contains("activePopup = null;", body);
        }

        [Test]
        public void AgentArtifactPhysicalReadsUseProjectBoundary()
        {
            string workspacePath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2",
                "ESAgentArtifactGenerationWorkflow.cs");
            string ioPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2",
                "ESAgentImportRecords.cs");
            string workspace = File.ReadAllText(workspacePath, new UTF8Encoding(false, true));
            string io = File.ReadAllText(ioPath, new UTF8Encoding(false, true));
            StringAssert.Contains("internal static void EnsureProjectReadPath", workspace);
            StringAssert.Contains("EnsureProjectReadPath(path);", io);
            StringAssert.Contains("public bool FileExists", io);
            StringAssert.Contains("public string ComputeSha256", io);
        }

        [Test]
        public void RuntimeFontCatalogInspectorStopsOnMissingSerializedFields()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESDrawer", "Normal",
                "ESLocalizedTextRefDrawer.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int inspector = source.IndexOf("public sealed class ESRuntimeFontCatalogInspector", StringComparison.Ordinal);
            Assert.GreaterOrEqual(inspector, 0);
            string body = source.Substring(inspector, Math.Min(1500, source.Length - inspector));
            StringAssert.Contains("catalogId == null || formatVersion == null || bindings == null", body);
            StringAssert.Contains("HelpBox", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void CommandPlayerInspectorStopsOnInvalidTargetOrSerializedShape()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESCommand",
                "ESCommandPlayerEditor.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public override void OnInspectorGUI", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(900, source.Length - method));
            StringAssert.Contains("target as ESCommandPlayer", body);
            StringAssert.Contains("playOnStart == null || eventToPlay == null", body);
            StringAssert.Contains("HelpBox", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void GraphAndShaderInspectorsGuardSchemaDrift()
        {
            string graphPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESGraphViewV2",
                "ESStableGraphAssetEditor.cs");
            string shaderPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESShader",
                "ESCompositeShaderFaderEditor.cs");
            string graph = File.ReadAllText(graphPath, new UTF8Encoding(false, true));
            string shader = File.ReadAllText(shaderPath, new UTF8Encoding(false, true));
            StringAssert.Contains("graph == null || graph.Nodes == null || graph.Edges == null", graph);
            StringAssert.Contains("SerializedProperty tracks = serializedFader.FindProperty(\"tracks\")", shader);
            StringAssert.Contains("if (tracks == null)", shader);
            StringAssert.Contains("SerializedProperty collectChildren = serializedFader.FindProperty", shader);
        }

        [Test]
        public void PreviewContextInitializationRollsBackPartialResources()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Ensure()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1300, source.Length - method));
            StringAssert.Contains("try", body);
            StringAssert.Contains("EnsurePreviewScene();", body);
            StringAssert.Contains("Dispose();", body);
            StringAssert.Contains("throw;", body);
        }

        [Test]
        public void WorldTerrainPreviewRollsBackPartialUnityObjects()
        {
            string path = Path.Combine(
                Application.dataPath, "..", "Scripts", "ESLogic", "Editor", "World",
                "ESWorldMapTerrainEditorFacade.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf(
                "public bool TryCreatePreview(ESWorldMapDefinition definition",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(3100, source.Length - method));
            StringAssert.Contains("TerrainData data = null", body);
            StringAssert.Contains("GameObject terrainObject = null", body);
            StringAssert.Contains("DestroyImmediate(terrainObject)", body);
            StringAssert.Contains("DestroyImmediate(data)", body);
            StringAssert.Contains("已清理临时对象", body);

            int destroy = source.IndexOf(
                "public static void DestroyPreview",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(destroy, 0);
            string destroyBody = source.Substring(destroy, Math.Min(1500, source.Length - destroy));
            StringAssert.Contains("handle.terrainObject = null", destroyBody);
            StringAssert.Contains("handle.terrainData = null", destroyBody);
            StringAssert.Contains("临时 Terrain 对象清理失败", destroyBody);
            StringAssert.Contains("临时 TerrainData 清理失败", destroyBody);
        }

        [Test]
        public void PreviewSharedAudioListenerCreationCleansPartialObject()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public AudioListener EnsurePreviewAudioListener()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1900, source.Length - method));
            StringAssert.Contains("GameObject listenerObject = ESEditorPreviewUtility.CreatePreviewGameObject", body);
            StringAssert.Contains("ESEditorPreviewUtility.DestroyObject(listenerObject)", body);
            StringAssert.Contains("throw;", body);
        }

        [Test]
        public void PreviewSharedAudioListenerReleaseGuardsDestroyedObject()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackagePreviewSession.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("if (audioListener != null)", body);
            StringAssert.Contains("audioListener = null", body);
        }

        [Test]
        public void PreviewContextClearsPreviewSceneHandleWhenCloseThrows()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "Scripts", "ESLogic", "Runtime", "EditorPreview", "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int dispose = source.IndexOf("public void Dispose()", source.IndexOf("public sealed class ESEditorPreviewRenderContext", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            string body = source.Substring(dispose, Math.Min(1900, source.Length - dispose));
            StringAssert.Contains("PreviewScene", body);
            StringAssert.Contains("IsDisposed", source);
        }

        [Test]
        public void PreviewRenderTextureCommitsMetadataOnlyAfterCreation()
        {
            string path = Path.Combine(
                Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void EnsureRenderTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("RenderTexture replacement =", body);
            StringAssert.Contains("if (replacement == null)", body);
            StringAssert.Contains("renderTexture = replacement", body);
        }

        [Test]
        public void PreviewRenderContextReplacesTextureTransactionally()
        {
            string path = Path.Combine(
                Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private void EnsureRenderTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("RenderTexture previous = renderTexture", body);
            StringAssert.Contains("RenderTexture replacement = ESEditorPreviewUtility.CreateRenderTexture", body);
            StringAssert.Contains("renderTexture = replacement", body);
            StringAssert.Contains("ReleaseRenderTexture(ref previous)", body);
        }

        [Test]
        public void PreviewUtilityClearsRenderTextureReferenceEvenWhenReleaseFails()
        {
            string path = Path.Combine(
                Application.dataPath, "..", "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewUtility.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public static void ReleaseRenderTexture", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1300, source.Length - method));
            StringAssert.Contains("DestroyObject(owned)", body);
            StringAssert.Contains("renderTexture = null", body);
            int destroy = source.IndexOf("public static void DestroyObject", StringComparison.Ordinal);
            Assert.GreaterOrEqual(destroy, 0);
            string destroyBody = source.Substring(destroy, Math.Min(900, source.Length - destroy));
            StringAssert.Contains("renderTexture.Release();", destroyBody);
            StringAssert.Contains("DestroyImmediate(obj)", destroyBody);
            StringAssert.Contains("catch (Exception exception)", destroyBody);
            int ownership = source.IndexOf("public static bool HasPreviewOwnershipFlags", StringComparison.Ordinal);
            Assert.GreaterOrEqual(ownership, 0);
            string ownershipBody = source.Substring(ownership, Math.Min(700, source.Length - ownership));
            StringAssert.Contains("== ownershipFlags", ownershipBody);
            StringAssert.Contains("string.IsNullOrWhiteSpace(owner)", source);
            StringAssert.Contains("owner is required", source);
        }

        [Test]
        public void PreviewRenderClampsInteractiveTextureDimensions()
        {
            string path = Path.Combine(
                Application.dataPath, "Scripts", "ESLogic", "Runtime", "EditorPreview",
                "ESEditorPreviewCore.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("private const int MaxRenderTextureDimension = 2048", source);
            StringAssert.Contains("private static int QuantizeRenderDimension(float pixels)", source);
            StringAssert.Contains("Mathf.CeilToInt(pixels)", source);
            StringAssert.Contains("Mathf.Min(MaxRenderTextureDimension", source);
            StringAssert.Contains("(quantized + 7) / 8 * 8", source);
            StringAssert.Contains("GlobalPreviewRenderTextureBudgetBytes = 512L * 1024L * 1024L", source);
            StringAssert.Contains("ApplyGlobalRenderTextureBudget(ref width, ref height, quality)", source);
            StringAssert.Contains("CaptureDiagnosticsSnapshot()", source);
            StringAssert.Contains("public bool HasEnhancer(ESEditorPreviewEnhancerSet enhancer)", source);
            StringAssert.Contains("不会隐式分配资源", source);
        }

        [Test]
        public void PreviewRenderRejectsNonFiniteOrEmptyRect()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public bool Render(Rect rect", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(900, source.Length - method));
            StringAssert.Contains("!IsFinite(rect.width)", body);
            StringAssert.Contains("!IsFinite(rect.height)", body);
            StringAssert.Contains("rect.width <= 0f || rect.height <= 0f", body);
            StringAssert.Contains("private static bool IsFinite(float value)", source);
        }

        [Test]
        public void SpecializedPreviewWritersUseTheSharedRenderContext()
        {
            string shaderPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESShader", "ESCompositeShaderBakeWindow.cs");
            string fontPath = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "FontToolsWindow", "Page_FontBuild.cs");
            string shader = File.ReadAllText(shaderPath, new UTF8Encoding(false, true));
            string font = File.ReadAllText(fontPath, new UTF8Encoding(false, true));
            StringAssert.Contains("ESEditorPreviewRenderContext", shader);
            StringAssert.Contains("context.Snapshot", shader);
            StringAssert.DoesNotContain("PreviewRenderUtility", shader);
            StringAssert.Contains("ESEditorPreviewRenderContext", font);
            StringAssert.Contains("RenderCurrentCameraGUI", font);
            StringAssert.DoesNotContain("PreviewRenderUtility", font);
        }

        [Test]
        public void ProductionPreviewPathsDoNotInstantiateLegacyUtility()
        {
            string[] roots =
            {
                Path.Combine(Application.dataPath, "Scripts"),
                Path.Combine(Application.dataPath, "Plugins", "ES", "Editor")
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.IndexOf(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    string source = File.ReadAllText(file, new UTF8Encoding(false, true));
                    Assert.Less(source.IndexOf("PreviewRenderUtility", StringComparison.Ordinal), 0,
                        "生产预览路径仍包含旧 PreviewRenderUtility：" + file);
                }
            }
        }

        [Test]
        public void AssetPackagePreviewDoesNotReintroducePrivateContext()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.DoesNotContain("ESAssetPackagePreviewSceneContext", source);
            StringAssert.Contains("ESAssetPackagePreviewSession", source);
            StringAssert.Contains("ESEditorPreviewRenderContext", source);
            StringAssert.Contains("if (!previewContext.PreparePreviewObject(previewInstance))", source);
        }

        [Test]
        public void AssetPackageHostDisableUnsubscribesDelayedRepaint()
        {
            string path = Path.Combine(Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("protected override void ESWindow_OnHostDisable()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(650, source.Length - method));
            StringAssert.Contains("EditorApplication.delayCall -= RepaintAssetPackageWindow;", body);
            StringAssert.Contains("ReleaseInstancePreviewResources();", body);
        }

        [Test]
        public void PreviewRenderRejectsNonFiniteCameraInputsAndClampsScale()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public bool Render(Rect rect", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1300, source.Length - method));
            StringAssert.Contains("!IsFinite(center.x)", body);
            StringAssert.Contains("!IsFinite(radius)", body);
            StringAssert.Contains("!IsFinite(renderScale)", body);
            StringAssert.Contains("radius = Mathf.Clamp(radius, 0.01f, 10000f)", body);
            StringAssert.Contains("zoom = Mathf.Clamp(zoom, 0.01f, 100f)", body);
        }

        [Test]
        public void PreviewRenderRejectsUnreasonableWorldCenter()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("Mathf.Abs(center.x) > 1000000f", source);
            StringAssert.Contains("Mathf.Abs(center.y) > 1000000f", source);
            StringAssert.Contains("Mathf.Abs(center.z) > 1000000f", source);
        }

        [Test]
        public void PreviewSnapshotSharesCameraInputGuards()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public Texture2D RenderSnapshot", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1000, source.Length - method));
            StringAssert.Contains("!IsFinite(center.x)", body);
            StringAssert.Contains("!IsFinite(radius)", body);
            StringAssert.Contains("radius = Mathf.Clamp(radius, 0.01f, 10000f)", body);
            StringAssert.Contains("zoom = Mathf.Clamp(zoom, 0.01f, 100f)", body);
        }

        [Test]
        public void StaticGameObjectPreviewGuardsBoundsAndFarClip()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static Texture2D RenderGameObjectPreview", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1800, source.Length - method));
            StringAssert.Contains("float.IsNaN(center.x)", body);
            StringAssert.Contains("float.IsNaN(radius)", body);
            StringAssert.Contains("Mathf.Clamp(radius * 10f, 1f, 10000f)", body);
        }

        [Test]
        public void StaticGameObjectPreviewClampsRequestedSize()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static Texture2D RenderGameObjectPreview", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(500, source.Length - method));
            StringAssert.Contains("size = Mathf.Clamp(size, 16, 2048)", body);
        }

        [Test]
        public void StaticGameObjectPreviewSeparatesBestEffortCleanup()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow",
                "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("private static Texture2D RenderGameObjectPreview", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(2600, source.Length - method));
            StringAssert.Contains("DestroyImmediate(instance)", body);
            StringAssert.Contains("utility?.Cleanup();", body);
            Assert.GreaterOrEqual(Regex.Matches(body, @"catch \(Exception exception\)").Count, 2);
        }
        [Test]
        public void CompactEditDrawersRecordUndoBeforeOpeningEditorWindow()
        {
            string compactPath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Drawers/ESCompactEditAttributeDrawer.cs");
            string expressionPath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Drawers/ESExpressionSourceCompactDrawers.cs");
            string compact = File.ReadAllText(Path.GetFullPath(compactPath), new UTF8Encoding(false, true));
            string expression = File.ReadAllText(Path.GetFullPath(expressionPath), new UTF8Encoding(false, true));
            StringAssert.Contains("Undo.RecordObject(undoTarget, \"编辑 \" + displayName);", compact);
            StringAssert.Contains("Undo.RecordObject(undoTarget, \"编辑 \" + displayName);", expression);
            StringAssert.Contains("OdinEditorWindow.InspectObject(wrapper)", compact);
            StringAssert.Contains("OdinEditorWindow.InspectObject(value)", expression);
        }

        [Test]
        public void CompactEditDrawersIsolateWindowCreationAndCloseFailures()
        {
            string compactPath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Drawers/ESCompactEditAttributeDrawer.cs");
            string expressionPath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Drawers/ESExpressionSourceCompactDrawers.cs");
            string compact = File.ReadAllText(Path.GetFullPath(compactPath), new UTF8Encoding(false, true));
            string expression = File.ReadAllText(Path.GetFullPath(expressionPath), new UTF8Encoding(false, true));
            StringAssert.Contains("Odin 编辑窗口创建失败", compact);
            StringAssert.Contains("Odin 编辑窗口创建失败", expression);
            StringAssert.Contains("紧凑编辑窗口关闭时目标已失效", compact);
            StringAssert.Contains("紧凑编辑窗口关闭时目标已失效", expression);
            StringAssert.Contains("finally", compact);
            StringAssert.Contains("finally", expression);
        }

        [Test]
        public void AudioCuePreviewStopIsolatedFromUnityReflectionFailure()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Preview/ESAudioCueTrimPreviewWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private static void StopNativePreview", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(1500, source.Length - method));
            StringAssert.Contains("try", body);
            StringAssert.Contains("stopPreviewMethod?.Invoke(null, null);", body);
            StringAssert.Contains("已清理 ES 本地播放状态", body);
        }

        [Test]
        public void WorkbenchDataRefreshRejectsCallbacksFromPreviousHostGeneration()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("workbenchRefreshGeneration", source);
            StringAssert.Contains("int refreshGeneration = workbenchRefreshGeneration", source);
            StringAssert.Contains("refreshGeneration != workbenchRefreshGeneration", source);
            StringAssert.Contains("workbenchRefreshGeneration++", source);
        }

        [Test]
        public void WorkbenchPreviewSceneInstancingIsTransactional()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchPreviewScene.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("TryInstantiateRegisteredPrefab", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(2600, source.Length - method));
            StringAssert.Contains("bool openedHere = !IsOpen;", body);
            StringAssert.Contains("PrefabUtility.InstantiatePrefab(prefab)", body);
            StringAssert.Contains("SceneManager.MoveGameObjectToScene(instance, renderContext.PreviewScene)", body);
            StringAssert.Contains("Object.DestroyImmediate(instance)", body);
            StringAssert.Contains("if (openedHere) Close();", body);
        }

        [Test]
        public void WorkbenchPreviewCloseRetainsSceneHandleWhenCloseFails()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchPreviewScene.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int close = source.IndexOf("public void Close()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(close, 0);
            int dispose = source.IndexOf("public void Dispose()", close, StringComparison.Ordinal);
            Assert.Greater(dispose, close);
            string body = source.Substring(close, dispose - close);
            StringAssert.Contains("bool sceneClosed = true;", body);
            StringAssert.Contains("sceneClosed = false;", body);
            StringAssert.Contains("if (sceneClosed)", body);
            StringAssert.Contains("renderContext.Dispose();", body);
            StringAssert.Contains("instances.Clear();", body);
        }

        [Test]
        public void WorkbenchPreviewCloseRetainsFailedInstanceReferencesForRetry()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchPreviewScene.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int close = source.IndexOf("public void Close()", StringComparison.Ordinal);
            int end = source.IndexOf("public void Dispose()", close, StringComparison.Ordinal);
            Assert.GreaterOrEqual(close, 0);
            Assert.Greater(end, close);
            string body = source.Substring(close, end - close);
            StringAssert.Contains("failedInstances", body);
            StringAssert.Contains("failedInstances.Add(instance)", body);
            StringAssert.Contains("instances.AddRange(failedInstances)", body);
            StringAssert.Contains("if (sceneClosed)", body);
        }

        [Test]
        public void WorkbenchPreviewRejectsUnsafeTransformValues()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchPreviewScene.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("NaN 或 Infinity", source);
            StringAssert.Contains("IsFiniteQuaternion", source);
            StringAssert.Contains("IsBoundedVector(position, 1000000f)", source);
            StringAssert.Contains("IsBoundedVector(scale, 10000f)", source);
            StringAssert.Contains("ApplyTransform", source);
        }

        [Test]
        public void WorkbenchHostCleanupRetainsPreviewOwnerWhenSceneRemainsOpen()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int close = source.IndexOf("protected void ESWorkbench_ClosePreviewScene()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(close, 0);
            int next = source.IndexOf("protected void ESWorkbench_SetStatus", close, StringComparison.Ordinal);
            Assert.Greater(next, close);
            string body = source.Substring(close, next - close);
            StringAssert.Contains("previewScene.Dispose();", body);
            StringAssert.Contains("if (!previewScene.IsOpen)", body);
            StringAssert.Contains("previewScene = null;", body);
        }

        [Test]
        public void WorkbenchDestroyRetriesRetainedPreviewSceneCleanup()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int destroy = source.IndexOf("protected override void OnDestroy()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(destroy, 0);
            int identity = source.IndexOf("if (!string.IsNullOrEmpty(workbenchInstanceKey))", destroy, StringComparison.Ordinal);
            Assert.Greater(identity, destroy);
            string body = source.Substring(destroy, identity - destroy);
            StringAssert.Contains("if (previewScene != null)", body);
            StringAssert.Contains("ESWorkbench_ClosePreviewScene();", body);
            StringAssert.Contains("catch (Exception exception)", body);
        }

        [Test]
        public void WorkbenchPopupRejectsSecondInstanceAndTracksOwnerLifetime()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int popup = source.IndexOf("internal sealed class ESWorkbenchPopupWindow", StringComparison.Ordinal);
            Assert.GreaterOrEqual(popup, 0);
            int onEnable = source.IndexOf("private void OnEnable()", popup, StringComparison.Ordinal);
            Assert.Greater(onEnable, popup);
            string body = source.Substring(popup, Math.Min(5200, source.Length - popup));
            StringAssert.Contains("现有实例关闭失败，已拒绝创建第二个实例", body);
            StringAssert.Contains("if (activeWindow != null)", body);
            StringAssert.Contains("private EditorWindow ownerWindow;", body);
            StringAssert.Contains("ownerContextLost", body);
            StringAssert.Contains("ESWindowFoundation.IsBound(ownerWindow)", body);
        }

        [Test]
        public void WorkbenchPopupContentFailureSchedulesSafeClose()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int create = source.IndexOf("content = request?.CreateContent(context);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            int after = source.IndexOf("if (content != null)", create, StringComparison.Ordinal);
            Assert.Greater(after, create);
            string body = source.Substring(create, after - create);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("configured = false;", body);
            StringAssert.Contains("内容创建失败，已安排安全关闭", body);
            StringAssert.Contains("CloseIfContextWasLost", body);
        }

        [Test]
        public void WorkbenchObjectProviderFailurePreservesPreviousList()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int rebuild = source.IndexOf("private void RebuildObjectList", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebuild, 0);
            int reset = source.IndexOf("contentPointerGate.Reset();", rebuild, StringComparison.Ordinal);
            Assert.Greater(reset, rebuild);
            string prefix = source.Substring(rebuild, reset - rebuild);
            StringAssert.Contains("resolvedSource = getObjects?.Invoke();", prefix);
            StringAssert.Contains("catch (Exception exception)", prefix);
            StringAssert.Contains("已保留上一次有效列表", prefix);
            StringAssert.Contains("return;", prefix);
        }

        [Test]
        public void WorkbenchDragCallbacksRejectEventsAfterDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            foreach (string methodName in new[]
            {
                "OnDragUpdated", "OnDragPerform", "OnDragLeave", "OnDragExited",
                "OnRootPointerCaptureOut", "OnRootFocusOut", "OnRootPointerCancel",
                "OnRootDetachedFromPanel"
            })
            {
                int method = source.IndexOf("private void " + methodName, StringComparison.Ordinal);
                Assert.GreaterOrEqual(method, 0, methodName);
                int brace = source.IndexOf('{', method);
                int firstStatement = source.IndexOf("if (disposed)", brace, StringComparison.Ordinal);
                Assert.GreaterOrEqual(firstStatement, brace, methodName);
            }
        }

        [Test]
        public void WorkbenchObjectFilteringIsNullSafeForProviderFields()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int rebuild = source.IndexOf("private void RebuildObjectList", StringComparison.Ordinal);
            int buildTabs = source.IndexOf("BuildContentKindTabs(source)", rebuild, StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebuild, 0);
            Assert.Greater(buildTabs, rebuild);
            string body = source.Substring(rebuild, buildTabs - rebuild);
            foreach (string field in new[]
            {
                "DisplayName", "BaseObjectId", "Category", "ContentKindDisplayName", "Subtitle"
            })
            {
                StringAssert.Contains("item." + field + " ?? string.Empty", body, field);
            }
            StringAssert.Contains("string category = item.Category ?? string.Empty;", source);
        }

        [Test]
        public void WorkbenchExternalDragBatchIsBoundedAndSanitized()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumExternalDragBatchItems = 256", source);
            int resolve = source.IndexOf("private IReadOnlyList<ESWorkbenchObjectDescriptor> ResolveDragBatch()", StringComparison.Ordinal);
            int next = source.IndexOf("private void NoteExternalDragSignal", resolve, StringComparison.Ordinal);
            Assert.GreaterOrEqual(resolve, 0);
            Assert.Greater(next, resolve);
            string body = source.Substring(resolve, next - resolve);
            StringAssert.Contains("Mathf.Min(internalBatch.Count, MaximumExternalDragBatchItems)", body);
            StringAssert.Contains("item != null && item.CanDrag", body);
            StringAssert.Contains("Mathf.Min(references.Length, MaximumExternalDragBatchItems)", body);
        }

        [Test]
        public void WorkbenchInternalDragPayloadRequiresCurrentContentOwnership()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int resolve = source.IndexOf("private ESWorkbenchObjectDescriptor ResolveDragItem()", StringComparison.Ordinal);
            int next = source.IndexOf("private IReadOnlyList<ESWorkbenchObjectDescriptor> ResolveDragBatch()", resolve, StringComparison.Ordinal);
            Assert.GreaterOrEqual(resolve, 0);
            Assert.Greater(next, resolve);
            StringAssert.Contains("IsCurrentContentDescriptor(internalItem)", source.Substring(resolve, next - resolve));
            int helper = source.IndexOf("private bool IsCurrentContentDescriptor", next, StringComparison.Ordinal);
            Assert.Greater(helper, next);
            string body = source.Substring(helper, Math.Min(700, source.Length - helper));
            StringAssert.Contains("contentSourceById.ContainsKey(item.BaseObjectId)", body);
            StringAssert.Contains("string.IsNullOrWhiteSpace(item.BaseObjectId)", body);
        }

        [Test]
        public void WorkbenchPopupOwnerCheckFailsClosedOnFoundationErrors()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int helper = source.IndexOf("private bool IsOwnerContextValid()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            string body = source.Substring(helper, Math.Min(900, source.Length - helper));
            StringAssert.Contains("ESWindowFoundation.IsBound(ownerWindow)", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("按宿主失效处理", body);
            StringAssert.Contains("return false;", body);
        }

        [Test]
        public void WorkbenchUsagePersistenceCannotBreakAuthoringActions()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int store = source.IndexOf("internal sealed class ESWorkbenchContentUsageStore", StringComparison.Ordinal);
            int descriptor = source.IndexOf("public sealed class ESWorkbenchObjectDescriptor", store, StringComparison.Ordinal);
            Assert.GreaterOrEqual(store, 0);
            Assert.Greater(descriptor, store);
            string body = source.Substring(store, descriptor - store);
            StringAssert.Contains("if (string.IsNullOrWhiteSpace(objectId))", body);
            StringAssert.Contains("TrySave();", body);
            StringAssert.Contains("private void TrySave()", body);
            StringAssert.Contains("内容使用记录保存失败", body);
        }

        [Test]
        public void WorkbenchContentScopeFilterFailsClosedForMissingUsageIdentity()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private static bool MatchesContentScope", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(700, source.Length - method));
            StringAssert.Contains("usage == null", body);
            StringAssert.Contains("string.IsNullOrWhiteSpace(item.BaseObjectId)", body);
            StringAssert.Contains("return false;", body);
        }

        [Test]
        public void WorkbenchObjectSortingCommitsVisibleListTransactionally()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int rebuild = source.IndexOf("private void RebuildObjectList", StringComparison.Ordinal);
            int tabs = source.IndexOf("BuildContentKindTabs(source)", rebuild, StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebuild, 0);
            Assert.Greater(tabs, rebuild);
            string body = source.Substring(rebuild, tabs - rebuild);
            StringAssert.Contains("var nextVisibleObjects = new List<ESWorkbenchObjectDescriptor>();", body);
            StringAssert.Contains("nextVisibleObjects.AddRange(filtered);", body);
            StringAssert.Contains("对象排序/过滤失败，已保留当前可见列表", body);
            StringAssert.Contains("visibleObjects.Clear();", body);
            StringAssert.Contains("visibleObjects.AddRange(nextVisibleObjects);", body);
        }

        [Test]
        public void WorkbenchThumbnailProviderFailsClosedToFallback()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private Texture ResolveContentThumbnail", StringComparison.Ordinal);
            int next = source.IndexOf("private Texture2D ResolveGeneratedContentThumbnail", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.Greater(next, method);
            string body = source.Substring(method, next - method);
            StringAssert.Contains("AssetPreview.GetAssetPreview", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("已使用降级图标", body);
            StringAssert.Contains("return entry.fallback ?? ResolveGeneratedContentThumbnail(item);", body);
        }

        [Test]
        public void WorkbenchThumbnailRefreshIsolatesAssetPreviewFailures()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int poll = source.IndexOf("private void PollContentThumbnails", StringComparison.Ordinal);
            int next = source.IndexOf("private static string ResolveContentKindShortName", poll, StringComparison.Ordinal);
            Assert.GreaterOrEqual(poll, 0);
            Assert.Greater(next, poll);
            string body = source.Substring(poll, next - poll);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("已停止该条目重试", body);
            StringAssert.Contains("objectList?.RefreshItems()", body);
            StringAssert.Contains("objectGridList?.RefreshItems()", body);
        }

        [Test]
        public void WorkbenchHierarchyProviderFailurePreservesPreviousList()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private void RebuildHierarchyList()", StringComparison.Ordinal);
            int reset = source.IndexOf("visibleHierarchy.Clear();", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.Greater(reset, method);
            string prefix = source.Substring(method, reset - method);
            StringAssert.Contains("source = getHierarchy?.Invoke();", prefix);
            StringAssert.Contains("catch (Exception exception)", prefix);
            StringAssert.Contains("已保留上一次有效列表", prefix);
            StringAssert.Contains("return;", prefix);
        }

        [Test]
        public void WorkbenchFilterCallbacksRejectEventsAfterDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int kind = source.IndexOf("private void SetContentKind", StringComparison.Ordinal);
            int category = source.IndexOf("private void SetCategory", kind, StringComparison.Ordinal);
            Assert.GreaterOrEqual(kind, 0);
            Assert.Greater(category, kind);
            StringAssert.Contains("if (disposed)", source.Substring(kind, category - kind));
            int rebuild = source.IndexOf("private void RebuildHierarchyList", category, StringComparison.Ordinal);
            StringAssert.Contains("if (disposed)", source.Substring(kind, category - kind));
            Assert.Greater(rebuild, category);
        }

        [Test]
        public void WorkbenchHierarchyIndexRejectsMissingIdsAndNullSearchFields()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int rebuild = source.IndexOf("private void RebuildHierarchyList", StringComparison.Ordinal);
            int filter = source.IndexOf("private HashSet<string> BuildHierarchyFilter", rebuild, StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebuild, 0);
            Assert.Greater(filter, rebuild);
            StringAssert.Contains("!string.IsNullOrWhiteSpace(item.ItemId)", source.Substring(rebuild, filter - rebuild));
            string body = source.Substring(filter, Math.Min(950, source.Length - filter));
            StringAssert.Contains("item.DisplayName ?? string.Empty", body);
            StringAssert.Contains("item.Kind ?? string.Empty", body);
        }

        [Test]
        public void WorkbenchHierarchyMutationCallbacksRejectEventsAfterDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            foreach (string methodName in new[]
            {
                "ToggleHierarchyVisibility", "ToggleHierarchyLock", "ToggleHierarchy",
                "ExpandAllHierarchy", "CollapseHierarchy"
            })
            {
                int method = source.IndexOf("private " + (methodName == "ToggleHierarchyVisibility" || methodName == "ToggleHierarchyLock" || methodName == "ToggleHierarchy" ? "void " : "void ") + methodName, StringComparison.Ordinal);
                Assert.GreaterOrEqual(method, 0, methodName);
                int brace = source.IndexOf('{', method);
                Assert.GreaterOrEqual(source.IndexOf("disposed", brace, StringComparison.Ordinal), brace, methodName);
            }
        }

        [Test]
        public void WorkbenchViewportHierarchyProviderFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private IReadOnlyList<ESWorkbenchHierarchyDescriptor> GetVisibleViewportHierarchy", StringComparison.Ordinal);
            int next = source.IndexOf("private int ResolveHierarchyDepth", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.Greater(next, method);
            string body = source.Substring(method, next - method);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("已回退到最近有效快照", body);
            StringAssert.Contains("hierarchyById.Values", body);
            StringAssert.Contains("IsHierarchyVisible(item.ItemId)", body);
        }

        [Test]
        public void WorkbenchHierarchySortAndRecursionFailClosedForInvalidEntries()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int compare = source.IndexOf("private static int CompareHierarchy", StringComparison.Ordinal);
            int append = source.IndexOf("private void AppendVisibleHierarchy", compare, StringComparison.Ordinal);
            Assert.GreaterOrEqual(compare, 0);
            Assert.Greater(append, compare);
            string compareBody = source.Substring(compare, append - compare);
            StringAssert.Contains("if (left == null) return 1;", compareBody);
            StringAssert.Contains("left.ItemId ?? string.Empty", compareBody);
            int next = source.IndexOf("private VisualElement CreateHierarchyRow", append, StringComparison.Ordinal);
            string appendBody = source.Substring(append, next - append);
            StringAssert.Contains("string.IsNullOrWhiteSpace(item.ItemId)", appendBody);
            StringAssert.Contains("path == null", appendBody);
        }

        [Test]
        public void WorkbenchContentKindTabsHaveStableLabelFallback()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int tabs = source.IndexOf("private void BuildContentKindTabs", StringComparison.Ordinal);
            int next = source.IndexOf("private static int ResolveContentKindOrder", tabs, StringComparison.Ordinal);
            Assert.GreaterOrEqual(tabs, 0);
            Assert.Greater(next, tabs);
            string body = source.Substring(tabs, next - tabs);
            StringAssert.Contains("string.IsNullOrWhiteSpace(sample.ContentKindDisplayName)", body);
            StringAssert.Contains("group.Key.ToString()", body);
        }

        [Test]
        public void WorkbenchContentKindTabsCommitAfterStaging()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int method = source.IndexOf("private void BuildContentKindTabs", StringComparison.Ordinal);
            int next = source.IndexOf("private static int ResolveContentKindOrder", method, StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.Greater(next, method);
            string body = source.Substring(method, next - method);
            StringAssert.Contains("var nextTabs = new List<ContentKindTabItem>();", body);
            StringAssert.Contains("nextTabs.Add", body);
            StringAssert.Contains("contentKindTabs.AddRange(nextTabs);", body);
        }

        [Test]
        public void WorkbenchCategoryTreeRestoresPreviousNodesOnFailure()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int wrapper = source.IndexOf("private void BuildContentCategoryTree(IReadOnlyList", StringComparison.Ordinal);
            int core = source.IndexOf("private void BuildContentCategoryTreeCore", wrapper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(wrapper, 0);
            Assert.Greater(core, wrapper);
            string body = source.Substring(wrapper, core - wrapper);
            StringAssert.Contains("ContentCategoryNode[] previousNodes", body);
            StringAssert.Contains("BuildContentCategoryTreeCore(source);", body);
            StringAssert.Contains("已恢复上一版节点", body);
            StringAssert.Contains("contentCategoryNodes.AddRange(previousNodes);", body);
        }

        [Test]
        public void WorkbenchCategoryTreeRecoveryResynchronizesListControl()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int wrapper = source.IndexOf("private void BuildContentCategoryTree(IReadOnlyList", StringComparison.Ordinal);
            int core = source.IndexOf("private void BuildContentCategoryTreeCore", wrapper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(wrapper, 0);
            Assert.Greater(core, wrapper);
            string body = source.Substring(wrapper, core - wrapper);
            StringAssert.Contains("contentCategoryList?.Rebuild();", body);
            StringAssert.Contains("contentCategoryEmptyLabel.style.display", body);
            StringAssert.Contains("catch (Exception rebuildException)", body);
        }

        [Test]
        public void WorkbenchCategoryTreeRecoveryResynchronizesSelection()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int wrapper = source.IndexOf("private void BuildContentCategoryTree(IReadOnlyList", StringComparison.Ordinal);
            int core = source.IndexOf("private void BuildContentCategoryTreeCore", wrapper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(wrapper, 0);
            Assert.Greater(core, wrapper);
            string body = source.Substring(wrapper, core - wrapper);
            StringAssert.Contains("FindIndex(value =>", body);
            StringAssert.Contains("SetSelectionWithoutNotify", body);
            StringAssert.Contains("catch (Exception selectionException)", body);
        }

        [Test]
        public void WorkbenchCategoryTreeRecoveryResynchronizesDerivedUi()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int wrapper = source.IndexOf("private void BuildContentCategoryTree(IReadOnlyList", StringComparison.Ordinal);
            int core = source.IndexOf("private void BuildContentCategoryTreeCore", wrapper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(wrapper, 0);
            Assert.Greater(core, wrapper);
            string body = source.Substring(wrapper, core - wrapper);
            StringAssert.Contains("UpdateContentBreadcrumb();", body);
            StringAssert.Contains("BuildCompactContentFilterMenu();", body);
            StringAssert.Contains("catch (Exception breadcrumbException)", body);
            StringAssert.Contains("catch (Exception filterException)", body);
        }

        [Test]
        public void WorkbenchSearchInputsHaveBoundedMatchingCost()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumWorkbenchSearchCharacters = 512", source);
            int objectQuery = source.IndexOf("string query = objectSearch?.value", StringComparison.Ordinal);
            int hierarchyQuery = source.IndexOf("string query = hierarchySearch?.value", StringComparison.Ordinal);
            Assert.GreaterOrEqual(objectQuery, 0);
            Assert.Greater(hierarchyQuery, objectQuery);
            StringAssert.Contains("query.Substring(0, MaximumWorkbenchSearchCharacters)", source.Substring(objectQuery, hierarchyQuery - objectQuery));
            int next = source.IndexOf("var visible = new HashSet<string>", hierarchyQuery, StringComparison.Ordinal);
            StringAssert.Contains("query.Substring(0, MaximumWorkbenchSearchCharacters)", source.Substring(hierarchyQuery, next - hierarchyQuery));
        }

        [Test]
        public void WorkbenchDeferredCallbacksFailClosedAfterDispose()
        {
            string path = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("contentCategoryList.selectionChanged += selection =>\n            {\n                if (disposed) return;", source);
            StringAssert.Contains("objectList.selectionChanged += selection =>\n            {\n                if (disposed) return;", source);
            StringAssert.Contains("hierarchyList.selectionChanged += selection =>\n            {\n                if (disposed) return;", source);
            foreach (string methodName in new[] { "SetContentScope", "SetContentSortMode", "SetContentViewMode" })
            {
                int method = source.IndexOf("private void " + methodName, StringComparison.Ordinal);
                Assert.GreaterOrEqual(method, 0, methodName);
                int brace = source.IndexOf('{', method);
                StringAssert.Contains("if (disposed) return;", source.Substring(brace, Math.Min(180, source.Length - brace)), methodName);
            }
            int selectionChanged = source.IndexOf("private void OnSelectionSetChanged", StringComparison.Ordinal);
            Assert.GreaterOrEqual(selectionChanged, 0);
            int selectionBrace = source.IndexOf('{', selectionChanged);
            StringAssert.Contains("if (disposed) return;", source.Substring(selectionBrace, Math.Min(160, source.Length - selectionBrace)));
            foreach (string methodName in new[]
            {
                "ApplyContentBrowserResponsive",
                "ApplyContentVerticalResponsive",
                "ApplyContentResultsResponsive",
                "RebuildObjectList"
            })
            {
                int method = source.IndexOf("private void " + methodName, StringComparison.Ordinal);
                Assert.GreaterOrEqual(method, 0, methodName);
                int brace = source.IndexOf('{', method);
                StringAssert.Contains("if (disposed) return;", source.Substring(brace, Math.Min(180, source.Length - brace)), methodName);
            }
        }

        [Test]
        public void WorkbenchUndoRedoCallbackFailsClosedAfterHostCleanup()
        {
            string basePath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs");
            string baseSource = File.ReadAllText(Path.GetFullPath(basePath), new UTF8Encoding(false, true));
            int callback = baseSource.IndexOf("private void OnWorkbenchUndoRedo", StringComparison.Ordinal);
            Assert.GreaterOrEqual(callback, 0);
            int brace = baseSource.IndexOf('{', callback);
            StringAssert.Contains("if (!workbenchHostSessionActive)", baseSource.Substring(brace, Math.Min(280, baseSource.Length - brace)));
            StringAssert.Contains("RefreshWorkbench(ESWorkbenchRefreshReason.UndoRedo)", baseSource.Substring(brace, Math.Min(560, baseSource.Length - brace)));

            string worldPath = Path.Combine(
                Application.dataPath, "../Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs");
            string worldSource = File.ReadAllText(Path.GetFullPath(worldPath), new UTF8Encoding(false, true));
            int worldCallback = worldSource.IndexOf("protected override void ESWorkbench_OnUndoRedo", StringComparison.Ordinal);
            Assert.GreaterOrEqual(worldCallback, 0);
            StringAssert.Contains("editSession.SynchronizeDraftAfterUndoRedo();", worldSource.Substring(worldCallback));
            StringAssert.Contains("ESWorkbench_SetDirtyStateWithoutNotification(", worldSource.Substring(worldCallback));
        }

        [Test]
        public void AssetPackagePreviewDisposeRetainsSceneHandleWhenCloseFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "ESMenuTreeWindow", "AssetPackageBakeWindow", "ESAssetPackageBakeWindow.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int method = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            string body = source.Substring(method, Math.Min(2600, source.Length - method));
            StringAssert.Contains("renderContext.Dispose();", body);
            StringAssert.Contains("disposed = renderContext.IsDisposed;", body);
        }

        [Test]
        public void CompactChoicePopupClosesWhenHostPanelIsRebuilt()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            StringAssert.Contains("hostWindow.rootVisualElement.panel", source);
            StringAssert.Contains("rootVisualElement.panel", source);
            StringAssert.Contains("ReferenceEquals(rootVisualElement.panel, hostWindow.rootVisualElement.panel)", source);
            StringAssert.Contains("hostContextLost", source);
        }

        [Test]
        public void CompactChoicePopupRejectsSecondInstanceWhenPreviousCloseFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int active = source.IndexOf("if (activePopup != null)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(active, 0);
            int snapshot = source.IndexOf("Option[] choiceSnapshot", active, StringComparison.Ordinal);
            Assert.Greater(snapshot, active);
            string body = source.Substring(active, snapshot - active);
            StringAssert.Contains("catch (Exception closeException)", body);
            StringAssert.Contains("return false;", body);
            StringAssert.Contains("if (activePopup != null)", body);
        }

        [Test]
        public void CompactChoicePopupSelectionFailsClosedAfterHostContextLoss()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int key = source.IndexOf("private void OnKeyDown", StringComparison.Ordinal);
            int select = source.IndexOf("private void Select", StringComparison.Ordinal);
            Assert.GreaterOrEqual(key, 0);
            Assert.Greater(select, key);
            StringAssert.Contains("if (!IsHostContextValid())", source.Substring(key, select - key));
            StringAssert.Contains("if (!IsHostContextValid())", source.Substring(select, Math.Min(700, source.Length - select)));
            StringAssert.Contains("private bool IsHostContextValid()", source);
            StringAssert.Contains("ReferenceEquals(rootVisualElement.panel, hostWindow.rootVisualElement.panel)", source);
        }

        [Test]
        public void CompactChoicePopupRejectsOpenReentrancyBeforeClosingActivePopup()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int open = source.IndexOf("public static bool Open", StringComparison.Ordinal);
            int anchor = source.IndexOf("if (!TryGetScreenAnchor", open, StringComparison.Ordinal);
            int active = source.IndexOf("if (activePopup != null)", anchor, StringComparison.Ordinal);
            Assert.GreaterOrEqual(open, 0);
            Assert.Greater(anchor, open);
            Assert.Greater(active, anchor);
            string prefix = source.Substring(open, anchor - open);
            StringAssert.Contains("if (openingPopup)", prefix);
            StringAssert.Contains("已拒绝重入 Open", prefix);
            StringAssert.Contains("openingPopup = true;", source.Substring(anchor, active - anchor));
            StringAssert.Contains("openingPopup = false;", source.Substring(active));
        }

        [Test]
        public void CompactChoicePopupCreateGuiFailsClosedWhenConfigurationWasCleared()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int create = source.IndexOf("public void CreateGUI()", StringComparison.Ordinal);
            int bind = source.IndexOf("ESWindowFoundation.BindTransient(this)", create, StringComparison.Ordinal);
            Assert.GreaterOrEqual(create, 0);
            Assert.Greater(bind, create);
            string body = source.Substring(create, bind - create);
            StringAssert.Contains("if (!configured)", body);
            StringAssert.Contains("Close();", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void CompactChoicePopupDisableAlwaysClearsActiveState()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int disable = source.IndexOf("private void OnDisable()", StringComparison.Ordinal);
            int destroy = source.IndexOf("private void OnDestroy()", disable, StringComparison.Ordinal);
            Assert.GreaterOrEqual(disable, 0);
            Assert.Greater(destroy, disable);
            string body = source.Substring(disable, destroy - disable);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("ESWindowFoundation.Suspend(this)", body);
            StringAssert.Contains("finally", body);
            StringAssert.Contains("configured = false;", body);
            StringAssert.Contains("activePopup = null;", body);
        }

        [Test]
        public void CompactChoicePopupDestroyFailsClosedAroundFoundationClose()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int destroy = source.IndexOf("private void OnDestroy()", StringComparison.Ordinal);
            int context = source.IndexOf("private void CloseIfContextWasLost", destroy, StringComparison.Ordinal);
            Assert.GreaterOrEqual(destroy, 0);
            Assert.Greater(context, destroy);
            string body = source.Substring(destroy, context - destroy);
            StringAssert.Contains("try", body);
            StringAssert.Contains("ESWindowFoundation.Close(this)", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("阻止异常穿出编辑器回调", body);
        }

        [Test]
        public void SearchDropdownDisablesEntryCallbacksAfterNativeWindowDetach()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int itemSelected = source.IndexOf("protected override void ItemSelected", StringComparison.Ordinal);
            int windowState = source.IndexOf("private sealed class WindowState", StringComparison.Ordinal);
            Assert.GreaterOrEqual(itemSelected, 0);
            Assert.Greater(windowState, itemSelected);
            StringAssert.Contains("if (!selectionEnabled)", source.Substring(itemSelected, windowState - itemSelected));
            StringAssert.Contains("private bool selectionEnabled = true;", source);
            StringAssert.Contains("private void DisableSelection()", source);
            StringAssert.Contains("dropdown?.DisableSelection();", source.Substring(windowState));
            StringAssert.Contains("new WindowState(dropdown, window, root", source);
        }

        [Test]
        public void SearchDropdownSelectionFailsClosedAfterHostContextLoss()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int itemSelected = source.IndexOf("protected override void ItemSelected", StringComparison.Ordinal);
            int disable = source.IndexOf("private void DisableSelection", itemSelected, StringComparison.Ordinal);
            int resolve = source.IndexOf("private IReadOnlyList<Entry> ResolveEntries", disable, StringComparison.Ordinal);
            Assert.GreaterOrEqual(itemSelected, 0);
            Assert.Greater(disable, itemSelected);
            Assert.Greater(resolve, disable);
            StringAssert.Contains("bool hostContextValid = IsHostContextValid();", source.Substring(itemSelected, disable - itemSelected));
            StringAssert.Contains("DisableSelection();", source.Substring(itemSelected, disable - itemSelected));
            StringAssert.Contains("private bool IsHostContextValid()", source.Substring(disable, resolve - disable));
            StringAssert.Contains("禁用选择回调", source.Substring(disable, resolve - disable));
        }

        [Test]
        public void SearchDropdownToolbarFailsClosedAfterHostContextLoss()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int toolbar = source.IndexOf("private sealed class ToolbarOverlay", StringComparison.Ordinal);
            int draw = source.IndexOf("private void Draw()", toolbar, StringComparison.Ordinal);
            int window = source.IndexOf("private sealed class WindowState", draw, StringComparison.Ordinal);
            Assert.GreaterOrEqual(toolbar, 0);
            Assert.Greater(draw, toolbar);
            Assert.Greater(window, draw);
            string body = source.Substring(toolbar, window - toolbar);
            StringAssert.Contains("contextValidator", body);
            StringAssert.Contains("!contextValidator()", body);
            StringAssert.Contains("disposed = true;", body);
            StringAssert.Contains("IsHostContextValidForBridge", source);
        }

        [Test]
        public void SearchDropdownToolbarCountFailureReleasesInteractionHold()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int count = source.IndexOf("bool hasToolbarActions = false", StringComparison.Ordinal);
            int bridge = source.IndexOf("AdvancedDropdownNativeBridge.TryAttach", count, StringComparison.Ordinal);
            Assert.GreaterOrEqual(count, 0);
            Assert.Greater(bridge, count);
            string body = source.Substring(count, bridge - count);
            StringAssert.Contains("toolbarActions.Count", body);
            StringAssert.Contains("DisposeInteractionHold(interactionHold)", body);
            StringAssert.Contains("ToolbarAction 集合计数失败", body);
        }

        [Test]
        public void SearchDropdownBridgeRemovesDestroyedManagedWindowReference()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int dispose = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
            int detached = source.IndexOf("private void OnDetachedFromPanel", dispose, StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            Assert.Greater(detached, dispose);
            string body = source.Substring(dispose, detached - dispose);
            StringAssert.Contains("ReferenceEquals(window, null)", body);
            StringAssert.Contains("WindowStates.Remove(window)", body);
            StringAssert.DoesNotContain("if (window != null)", body);
        }

        [Test]
        public void SearchDropdownToolbarTextHasBoundedPresentationCost()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int toolbar = source.IndexOf("public sealed class ToolbarAction", StringComparison.Ordinal);
            int builder = source.IndexOf("public readonly struct Entry", toolbar, StringComparison.Ordinal);
            Assert.GreaterOrEqual(toolbar, 0);
            Assert.Greater(builder, toolbar);
            string body = source.Substring(toolbar, builder - toolbar);
            StringAssert.Contains("MaximumToolbarTextCharacters = 512", source);
            StringAssert.Contains("NormalizeToolbarText(label, \"·\")", body);
            StringAssert.Contains("NormalizeToolbarText(tooltip, null)", body);
            StringAssert.Contains("normalized.Substring(0, MaximumToolbarTextCharacters)", source);
        }

        [Test]
        public void SearchDropdownEntryPresentationHasBoundedCostWithoutChangingEntryIdentity()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int action = source.IndexOf("private sealed class ActionItem", StringComparison.Ordinal);
            int fields = source.IndexOf("private readonly string title", action, StringComparison.Ordinal);
            Assert.GreaterOrEqual(action, 0);
            Assert.Greater(fields, action);
            string body = source.Substring(action, fields - action);
            StringAssert.Contains("BoundPresentationText(entry.Tooltip)", body);
            StringAssert.Contains("return BoundPresentationText(result);", body);
            StringAssert.Contains("MaximumEntryPresentationCharacters = 2048", source);
            StringAssert.Contains("private static string BoundPresentationText", source);
            StringAssert.Contains("StableId", source);
        }

        [Test]
        public void SearchAndChoicePopupAnchorsAreClampedToHostBounds()
        {
            string searchPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESSearchDropdown.cs");
            string searchSource = File.ReadAllText(Path.GetFullPath(searchPath), new UTF8Encoding(false, true));
            int searchAnchor = searchSource.IndexOf("private static bool TryGetGuiAnchorRect", StringComparison.Ordinal);
            Assert.GreaterOrEqual(searchAnchor, 0);
            string searchBody = searchSource.Substring(searchAnchor, Math.Min(1800, searchSource.Length - searchAnchor));
            StringAssert.Contains("Rect hostBounds = host.position", searchBody);
            StringAssert.Contains("hostBounds.xMax - anchorWidth", searchBody);
            StringAssert.Contains("hostBounds.yMax - anchorHeight", searchBody);

            string popupPath = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools", "ESCompactChoicePopup.cs");
            string popupSource = File.ReadAllText(Path.GetFullPath(popupPath), new UTF8Encoding(false, true));
            int popupAnchor = popupSource.IndexOf("private static bool TryGetScreenAnchor", StringComparison.Ordinal);
            Assert.GreaterOrEqual(popupAnchor, 0);
            string popupBody = popupSource.Substring(popupAnchor, Math.Min(1500, popupSource.Length - popupAnchor));
            StringAssert.Contains("Rect hostBounds = hostWindow.position", popupBody);
            StringAssert.Contains("hostBounds.xMax - screenRect.width", popupBody);
            StringAssert.Contains("hostBounds.yMax - screenRect.height", popupBody);
        }

        [Test]
        public void CommandPaletteContextCopyFailsClosedAfterWindowDisable()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int copyMethod = source.IndexOf("private void CopyTargetFromContextMenu", StringComparison.Ordinal);
            Assert.GreaterOrEqual(copyMethod, 0);
            int copyBrace = source.IndexOf('{', copyMethod);
            StringAssert.Contains("if (this == null || !lifecycleActive", source.Substring(copyBrace, Math.Min(240, source.Length - copyBrace)));
            StringAssert.Contains("CopyTargetFromContextMenu(item.TargetId)", source);
            int execute = source.IndexOf("private void ExecuteSelected", StringComparison.Ordinal);
            int locate = source.IndexOf("private void LocateSelected", StringComparison.Ordinal);
            int shortcut = source.IndexOf("private void CopySelectedShortcut", StringComparison.Ordinal);
            Assert.GreaterOrEqual(execute, 0);
            Assert.Greater(locate, execute);
            Assert.Greater(shortcut, locate);
            StringAssert.Contains("if (this == null || !lifecycleActive", source.Substring(execute, locate - execute));
            StringAssert.Contains("if (this == null || !lifecycleActive", source.Substring(locate, shortcut - locate));
            StringAssert.Contains("if (this == null || !lifecycleActive", source.Substring(shortcut, Math.Min(300, source.Length - shortcut)));
        }

        [Test]
        public void CommandPaletteQueryInputIsBoundedBeforeStateRetention()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("private static string NormalizeQuery(string value)", source);
            StringAssert.Contains("ESCommandPaletteSearchEngine.MaximumQueryCharacters", source);
            StringAssert.Contains("window.query = NormalizeQuery(initialQuery)", source);
            StringAssert.Contains("window.query = NormalizeQuery(lastTab)", source);
            StringAssert.Contains("string next = NormalizeQuery(EditorGUILayout.TextField(", source);
            StringAssert.Contains("query = NormalizeQuery(prefix + CurrentSearchTerm())", source);
        }

        [Test]
        public void CommandPaletteRefreshRestoresSelectionByStableId()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int update = source.IndexOf("private void UpdateResults()", StringComparison.Ordinal);
            int order = source.IndexOf("private static IReadOnlyList<ESCommandPaletteItem> OrderResultsForDisplay", update, StringComparison.Ordinal);
            Assert.GreaterOrEqual(update, 0);
            Assert.Greater(order, update);
            string body = source.Substring(update, order - update);
            StringAssert.Contains("string previousSelectedId", body);
            StringAssert.Contains("results[selected]?.StableId", body);
            StringAssert.Contains("restoredIndex", body);
            StringAssert.Contains("results[index]?.StableId", body);
            StringAssert.Contains("Mathf.Clamp(selected, 0, Math.Max(0, results.Count - 1))", body);
        }

        [Test]
        public void CommandPaletteExecutionRebindsRegisteredItemAfterRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int execute = source.IndexOf("private void ExecuteItem", StringComparison.Ordinal);
            int record = source.IndexOf("RecordQuery(query)", execute, StringComparison.Ordinal);
            Assert.GreaterOrEqual(execute, 0);
            Assert.Greater(record, execute);
            string body = source.Substring(execute, record - execute);
            StringAssert.Contains("ESCommandPaletteRegistry.TryGet(item.StableId", body);
            StringAssert.Contains("ESCommandPaletteItem currentItem", body);
            StringAssert.Contains("item = currentItem;", body);
        }

        [Test]
        public void CommandPaletteRegistryRejectsNestedRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("private static bool refreshing;", source);
            int refresh = source.IndexOf("public static void Refresh()", StringComparison.Ordinal);
            int tryBlock = source.IndexOf("try", refresh, StringComparison.Ordinal);
            int finallyBlock = source.IndexOf("finally", tryBlock, StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            Assert.Greater(tryBlock, refresh);
            Assert.Greater(finallyBlock, tryBlock);
            string body = source.Substring(refresh, finallyBlock - refresh);
            StringAssert.Contains("if (refreshing)", body);
            StringAssert.Contains("refreshing = true;", body);
            StringAssert.Contains("refreshing = false;", source.Substring(finallyBlock, Math.Min(180, source.Length - finallyBlock)));
        }

        [Test]
        public void CommandPaletteProviderItemsHaveBoundedRefreshCost()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("public const int MaximumProviderItems = 4096;", source);
            StringAssert.Contains("public const int MaximumTotalItems = 100000;", source);
            StringAssert.Contains("int totalCandidateCount;", source);
            StringAssert.Contains("totalCandidateCount = candidates.Count;", source);
            StringAssert.Contains("if (totalCandidateCount < 0)", source);
            StringAssert.Contains("Provider 候选项数量无效", source);
            StringAssert.Contains("Mathf.Min(totalCandidateCount, MaximumProviderItems)", source);
            StringAssert.Contains("已截断后续项", source);
            StringAssert.Contains("Items.Count + stagedItems.Count >= MaximumTotalItems", source);
        }

        [Test]
        public void CommandPaletteProviderFieldsHaveBoundedIdentityCost()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumProviderIdCharacters = 128", source);
            StringAssert.Contains("MaximumItemIdCharacters = 512", source);
            StringAssert.Contains("MaximumTitleCharacters = 1024", source);
            StringAssert.Contains("MaximumCategoryCharacters = 256", source);
            StringAssert.Contains("MaximumTargetIdCharacters = 4096", source);
            StringAssert.Contains("命令项字段超过长度上限", source);
            StringAssert.Contains("ProviderId 或 Prefix 超过长度上限", source);
        }

        [Test]
        public void CommandPaletteProviderCountHasGlobalBound()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("public const int MaximumProviders = 64;", source);
            int register = source.IndexOf("private static ESCommandPaletteRegistrationResult RegisterProviderCore", StringComparison.Ordinal);
            int rebuild = source.IndexOf("private static bool RebuildProviderItems", register, StringComparison.Ordinal);
            Assert.GreaterOrEqual(register, 0);
            Assert.Greater(rebuild, register);
            string body = source.Substring(register, rebuild - register);
            StringAssert.Contains("ProviderOrder.Count >= MaximumProviders", body);
            StringAssert.Contains("Provider 数量超过上限", body);
            StringAssert.Contains("return result;", body);
        }

        [Test]
        public void CommandPaletteProviderRegistrationHasGetterFailureBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int register = source.IndexOf("public static ESCommandPaletteRegistrationResult RegisterProvider", StringComparison.Ordinal);
            int refresh = source.IndexOf("public static void Refresh", register, StringComparison.Ordinal);
            Assert.GreaterOrEqual(register, 0);
            Assert.Greater(refresh, register);
            string body = source.Substring(register, refresh - register);
            StringAssert.Contains("try", body);
            StringAssert.Contains("return RegisterProviderCore(provider);", body);
            StringAssert.Contains("Provider 注册边界异常，已安全拒绝", body);
            StringAssert.Contains("return failed;", body);
        }

        [Test]
        public void CommandPaletteInitializationRollsBackPartialBuiltIns()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int ensure = source.IndexOf("public static void EnsureInitialized()", StringComparison.Ordinal);
            int register = source.IndexOf("public static ESCommandPaletteRegistrationResult RegisterProvider", ensure, StringComparison.Ordinal);
            Assert.GreaterOrEqual(ensure, 0);
            Assert.Greater(register, ensure);
            string body = source.Substring(ensure, register - ensure);
            StringAssert.Contains("previousProviders", body);
            StringAssert.Contains("previousProviderOrder", body);
            StringAssert.Contains("previousItems", body);
            StringAssert.Contains("previousOrderedItems", body);
            StringAssert.Contains("previousProviders = new Dictionary", body);
            StringAssert.Contains("命令索引初始化失败，已回滚部分注册状态", body);
            StringAssert.Contains("initialized = false;", body);
        }

        [Test]
        public void CommandPaletteReentrantProviderDiagnosticReadFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int register = source.IndexOf("public static ESCommandPaletteRegistrationResult RegisterProvider", StringComparison.Ordinal);
            int core = source.IndexOf("private static ESCommandPaletteRegistrationResult RegisterProviderCore", register, StringComparison.Ordinal);
            Assert.GreaterOrEqual(register, 0);
            Assert.Greater(core, register);
            string body = source.Substring(register, core - register);
            StringAssert.Contains("string providerId = string.Empty", body);
            StringAssert.Contains("provider?.ProviderId", body);
            StringAssert.Contains("重入拒绝诊断读取 ProviderId 失败", body);
            StringAssert.Contains("providerId, string.Empty", body);
        }

        [Test]
        public void CommandPaletteLifecycleCleanupSurvivesFoundationFailures()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int disable = source.IndexOf("private void OnDisable()", StringComparison.Ordinal);
            int gui = source.IndexOf("private void OnGUI()", disable, StringComparison.Ordinal);
            Assert.GreaterOrEqual(disable, 0);
            Assert.Greater(gui, disable);
            string body = source.Substring(disable, gui - disable);
            StringAssert.Contains("ESWindowFoundation.Suspend(this)", body);
            StringAssert.Contains("finally", body);
            StringAssert.Contains("UnregisterSearchTick();", body);
            StringAssert.Contains("UnregisterShortcutCheckTick();", body);
            StringAssert.Contains("searchEngine.Clear();", body);
            StringAssert.Contains("results = Array.Empty<ESCommandPaletteItem>();", body);
        }

        [Test]
        public void CommandPaletteEnableFailureRollsBackPartialStartup()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int enable = source.IndexOf("private void OnEnable()", StringComparison.Ordinal);
            int disable = source.IndexOf("private void OnDisable()", enable, StringComparison.Ordinal);
            Assert.GreaterOrEqual(enable, 0);
            Assert.Greater(disable, enable);
            string body = source.Substring(enable, disable - enable);
            StringAssert.Contains("try", body);
            StringAssert.Contains("lifecycleActive = false;", body);
            StringAssert.Contains("UnregisterSearchTick();", body);
            StringAssert.Contains("UnregisterShortcutCheckTick();", body);
            StringAssert.Contains("ESWindowFoundation.Suspend(this)", body);
            StringAssert.Contains("searchEngine.Clear();", body);
            StringAssert.Contains("results = Array.Empty<ESCommandPaletteItem>();", body);
            StringAssert.Contains("启动初始化失败，已回滚窗口状态", body);
        }

        [Test]
        public void CommandPaletteReopenSessionStateReadFailsClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int open = source.IndexOf("public static void OpenWindow()", StringComparison.Ordinal);
            int enable = source.IndexOf("private void OnEnable()", open, StringComparison.Ordinal);
            Assert.GreaterOrEqual(open, 0);
            Assert.Greater(enable, open);
            string body = source.Substring(open, enable - open);
            StringAssert.Contains("SessionState.GetString(LastTabKey", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("回退默认标签", body);
            StringAssert.Contains("window.query = NormalizeQuery(lastTab);", body);
        }

        [Test]
        public void CommandPaletteWindowShowFailureStopsPartialInitialization()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int show = source.IndexOf("window.ShowUtility();", StringComparison.Ordinal);
            int center = source.IndexOf("if (!alreadyOpen)", show, StringComparison.Ordinal);
            Assert.GreaterOrEqual(show, 0);
            Assert.Greater(center, show);
            string body = source.Substring(show, center - show);
            StringAssert.Contains("window.Focus();", body);
            StringAssert.Contains("window.minSize = MinimumSize;", body);
            StringAssert.Contains("window.maxSize = MaximumSize;", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("if (!alreadyOpen)", body);
            StringAssert.Contains("window.Close();", body);
            StringAssert.Contains("显示命令面板失败", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void CommandPaletteCenteringRejectsInvalidMainWindowRect()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int center = source.IndexOf("private static void CenterWindowInMainEditor", StringComparison.Ordinal);
            int openQuery = source.IndexOf("public static void OpenWithQuery", center, StringComparison.Ordinal);
            Assert.GreaterOrEqual(center, 0);
            Assert.Greater(openQuery, center);
            string body = source.Substring(center, openQuery - center);
            StringAssert.Contains("!IsFinite(mainWindow.x)", body);
            StringAssert.Contains("!IsFinite(mainWindow.width)", body);
            StringAssert.Contains("mainWindow.width <= 0f", body);
            StringAssert.Contains("return;", body);
        }

        [Test]
        public void CommandPaletteStylesPublishReadyOnlyAfterCompleteBuild()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int ensure = source.IndexOf("private static void EnsureStyles()", StringComparison.Ordinal);
            int solid = source.IndexOf("private static Texture2D SolidTexture", ensure, StringComparison.Ordinal);
            Assert.GreaterOrEqual(ensure, 0);
            Assert.Greater(solid, ensure);
            string body = source.Substring(ensure, solid - ensure);
            int firstReady = body.IndexOf("stylesReady = true;", StringComparison.Ordinal);
            int lastFooter = body.LastIndexOf("footerStyle =", StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstReady, 0);
            Assert.Greater(firstReady, lastFooter);
            StringAssert.Contains("mid-build exception", body);
        }

        [Test]
        public void CommandPaletteStyleRetryClearsPartialTextures()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int ensure = source.IndexOf("private static void EnsureStyles()", StringComparison.Ordinal);
            int solid = source.IndexOf("private static Texture2D SolidTexture", ensure, StringComparison.Ordinal);
            Assert.GreaterOrEqual(ensure, 0);
            Assert.Greater(solid, ensure);
            string body = source.Substring(ensure, solid - ensure);
            int ready = body.IndexOf("if (stylesReady)", StringComparison.Ordinal);
            int skin = body.IndexOf("stylesProSkin =", ready, StringComparison.Ordinal);
            Assert.Greater(ready, 0);
            Assert.Greater(skin, ready);
            StringAssert.Contains("else", body.Substring(ready, skin - ready));
            StringAssert.Contains("DestroyCreatedTextures();", body.Substring(ready, skin - ready));
            StringAssert.Contains("partial allocation", body);
        }

        [Test]
        public void CommandPaletteSolidTexturePreservesOriginalFailure()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int solid = source.IndexOf("private static Texture2D SolidTexture", StringComparison.Ordinal);
            int acquire = source.IndexOf("private static void AcquireStyles", solid, StringComparison.Ordinal);
            Assert.GreaterOrEqual(solid, 0);
            Assert.Greater(acquire, solid);
            string body = source.Substring(solid, acquire - solid);
            StringAssert.Contains("catch (Exception createException)", body);
            StringAssert.Contains("catch (Exception destroyException)", body);
            StringAssert.Contains("throw createException;", body);
        }

        [Test]
        public void CommandPaletteGuiSkipsFrameWhenStyleBuildFails()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int gui = source.IndexOf("private void OnGUI()", StringComparison.Ordinal);
            int ensure = source.IndexOf("private static void EnsureStyles()", gui, StringComparison.Ordinal);
            Assert.GreaterOrEqual(gui, 0);
            Assert.Greater(ensure, gui);
            string body = source.Substring(gui, ensure - gui);
            StringAssert.Contains("try", body);
            StringAssert.Contains("EnsureStyles();", body);
            StringAssert.Contains("stylesReady = false;", body);
            StringAssert.Contains("跳过本帧绘制", body);
            StringAssert.Contains("if (!stylesReady)", body);
        }

        [Test]
        public void CommandPaletteStyleFailureLogIsBoundedUntilRecovery()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("private static bool styleFailureLogged;", source);
            int gui = source.IndexOf("private void OnGUI()", StringComparison.Ordinal);
            int ensure = source.IndexOf("private static void EnsureStyles()", gui, StringComparison.Ordinal);
            Assert.GreaterOrEqual(gui, 0);
            Assert.Greater(ensure, gui);
            string guiBody = source.Substring(gui, ensure - gui);
            StringAssert.Contains("if (!styleFailureLogged)", guiBody);
            StringAssert.Contains("styleFailureLogged = true;", guiBody);
            StringAssert.Contains("styleFailureLogged = false;", source.Substring(ensure));
        }

        [Test]
        public void CommandPaletteStyleFailureRetriesWithBoundedBackoff()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("StyleRetryBackoffSeconds = 0.25d", source);
            StringAssert.Contains("nextStyleRetryAt", source);
            int gui = source.IndexOf("private void OnGUI()", StringComparison.Ordinal);
            int ensure = source.IndexOf("private static void EnsureStyles()", gui, StringComparison.Ordinal);
            Assert.GreaterOrEqual(gui, 0);
            Assert.Greater(ensure, gui);
            string body = source.Substring(gui, ensure - gui);
            StringAssert.Contains("EditorApplication.timeSinceStartup < nextStyleRetryAt", body);
            StringAssert.Contains("nextStyleRetryAt = EditorApplication.timeSinceStartup + StyleRetryBackoffSeconds", body);
        }

        [Test]
        public void CommandPaletteStyleRetryBackoffResetsAcrossLifecycle()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int enable = source.IndexOf("private void OnEnable()", StringComparison.Ordinal);
            int destroy = source.IndexOf("private void OnDestroy()", enable, StringComparison.Ordinal);
            Assert.GreaterOrEqual(enable, 0);
            Assert.Greater(destroy, enable);
            string lifecycle = source.Substring(enable, destroy - enable);
            StringAssert.Contains("nextStyleRetryAt = 0d;", lifecycle);
            StringAssert.Contains("finally", lifecycle);
            StringAssert.Contains("nextStyleRetryAt = 0d;", source.Substring(destroy));
        }

        [Test]
        public void WorkbenchDisposeStopsExternalDragWatchdogImmediately()
        {
            string path = Path.Combine(
                Application.dataPath, "Scripts", "ESLogic", "Editor", "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int dispose = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
            int stop = source.IndexOf("StopDragEdgePan();", dispose, StringComparison.Ordinal);
            Assert.GreaterOrEqual(dispose, 0);
            Assert.Greater(stop, dispose);
            string body = source.Substring(dispose, stop - dispose);
            StringAssert.Contains("StopExternalDragWatchdog();", body);
            StringAssert.Contains("disposed = true;", body);
        }

        [Test]
        public void WorkbenchClipboardActionsHaveFailureBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Scripts", "ESLogic", "Editor", "Workbench",
                "ESWorkbenchUIToolkitHost.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int helper = source.IndexOf("private void CopyToClipboard", StringComparison.Ordinal);
            int left = source.IndexOf("private VisualElement BuildLeftPanel", helper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            Assert.Greater(left, helper);
            string body = source.Substring(helper, left - helper);
            StringAssert.Contains("EditorGUIUtility.systemCopyBuffer = value", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("复制失败：", body);
            StringAssert.Contains("disposed", body);
        }

        [Test]
        public void CommandPaletteRegistryRejectsRefreshDuringInitialization()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int refresh = source.IndexOf("public static void Refresh()", StringComparison.Ordinal);
            int nested = source.IndexOf("if (initializing)", refresh, StringComparison.Ordinal);
            int refreshing = source.IndexOf("if (refreshing)", nested, StringComparison.Ordinal);
            Assert.GreaterOrEqual(refresh, 0);
            Assert.Greater(nested, refresh);
            Assert.Greater(refreshing, nested);
            StringAssert.Contains("已拒绝 Refresh", source.Substring(nested, refreshing - nested));
        }

        [Test]
        public void CommandPalettePersistedStateHasBoundedReadAndSafeWrite()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("MaximumPersistedIdsCharacters = 65536", source);
            StringAssert.Contains("raw.Length > MaximumPersistedIdsCharacters", source);
            StringAssert.Contains("raw.Substring(0, MaximumPersistedIdsCharacters)", source);
            int save = source.IndexOf("private static void SaveIds", StringComparison.Ordinal);
            Assert.GreaterOrEqual(save, 0);
            string saveBody = source.Substring(save, Math.Min(1000, source.Length - save));
            StringAssert.Contains("try", saveBody);
            StringAssert.Contains("catch (Exception exception)", saveBody);
            StringAssert.Contains("已保留当前内存状态", saveBody);
        }

        [Test]
        public void CommandPaletteProviderItemFailuresAreIsolated()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int loop = source.IndexOf("for (int i = 0; i < candidateCount; i++)", StringComparison.Ordinal);
            int limit = source.IndexOf("if (totalCandidateCount > MaximumProviderItems)", loop, StringComparison.Ordinal);
            Assert.GreaterOrEqual(loop, 0);
            Assert.Greater(limit, loop);
            string body = source.Substring(loop, limit - loop);
            StringAssert.Contains("try", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("Provider 单项校验失败，已跳过该项", body);
            StringAssert.Contains("stagedItems.Add(item);", body);
            int assign = body.IndexOf("item.ProviderId = registration.ProviderId", StringComparison.Ordinal);
            int accepted = body.IndexOf("acceptedIds.Add(item.ItemId)", assign, StringComparison.Ordinal);
            Assert.GreaterOrEqual(assign, 0);
            Assert.Greater(accepted, assign);
        }

        [Test]
        public void CommandPaletteFavoriteRecentLoadRollsBackAtomically()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int load = source.IndexOf("private static void LoadAndCleanState", StringComparison.Ordinal);
            int loadIds = source.IndexOf("private static void LoadIds", load, StringComparison.Ordinal);
            Assert.GreaterOrEqual(load, 0);
            Assert.Greater(loadIds, load);
            string body = source.Substring(load, loadIds - load);
            StringAssert.Contains("previousFavorites", body);
            StringAssert.Contains("previousRecent", body);
            StringAssert.Contains("FavoriteIds.AddRange(previousFavorites)", body);
            StringAssert.Contains("RecentIds.AddRange(previousRecent)", body);
            StringAssert.Contains("已保留上一版内存状态", body);
        }

        [Test]
        public void CommandPaletteRegistryExposesStableObservationDuringRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("refreshObservationItems", source);
            StringAssert.Contains("refreshObservationOrdered", source);
            StringAssert.Contains("refreshing && refreshObservationOrdered != null", source);
            StringAssert.Contains("refreshObservationOrdered = previousOrderedItems.AsReadOnly();", source);
            StringAssert.Contains("refreshObservationItems = previousItems;", source);
            StringAssert.Contains("refreshObservationItems = null;", source);
            StringAssert.Contains("refreshObservationOrdered = null;", source);
            int tryGet = source.IndexOf("public static bool TryGet", StringComparison.Ordinal);
            int next = source.IndexOf("public static bool IsFavorite", tryGet, StringComparison.Ordinal);
            Assert.GreaterOrEqual(tryGet, 0);
            Assert.Greater(next, tryGet);
            StringAssert.Contains("refreshObservationItems.TryGetValue", source.Substring(tryGet, next - tryGet));
        }

        [Test]
        public void CommandPaletteRegistryDiagnosticsStayStableDuringRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("refreshObservationDiagnostics", source);
            StringAssert.Contains("refreshing && refreshObservationDiagnostics != null", source);
            StringAssert.Contains("refreshObservationDiagnostics = previousDiagnostics.AsReadOnly();", source);
            StringAssert.Contains("refreshObservationDiagnostics = null;", source);
            int diagnostics = source.IndexOf("public static IReadOnlyList<ESCommandPaletteRegistrationDiagnostic> RegistrationDiagnostics", StringComparison.Ordinal);
            int providerCount = source.IndexOf("public static int ProviderCount", diagnostics, StringComparison.Ordinal);
            Assert.GreaterOrEqual(diagnostics, 0);
            Assert.Greater(providerCount, diagnostics);
            StringAssert.Contains("refreshObservationDiagnostics", source.Substring(diagnostics, providerCount - diagnostics));
        }

        [Test]
        public void CommandPaletteRefreshSnapshotCoversStateActionsAndReset()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("ContainsObservableItem(stableId)", source);
            int helper = source.IndexOf("private static bool ContainsObservableItem", StringComparison.Ordinal);
            int register = source.IndexOf("private static ESCommandPaletteRegistrationResult RegisterProviderCore", helper, StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            Assert.Greater(register, helper);
            StringAssert.Contains("refreshObservationItems.ContainsKey", source.Substring(helper, register - helper));
            int reset = source.IndexOf("internal static void ResetForTests", StringComparison.Ordinal);
            int stored = source.IndexOf("internal static void SetStoredIdsForTests", reset, StringComparison.Ordinal);
            Assert.GreaterOrEqual(reset, 0);
            Assert.Greater(stored, reset);
            string resetBody = source.Substring(reset, stored - reset);
            StringAssert.Contains("refreshing = false;", resetBody);
            StringAssert.Contains("refreshObservationItems = null;", resetBody);
            StringAssert.Contains("refreshObservationDiagnostics = null;", resetBody);
        }

        [Test]
        public void CommandPaletteStateWritesAreRejectedDuringRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int favorite = source.IndexOf("public static void ToggleFavorite", StringComparison.Ordinal);
            int recent = source.IndexOf("public static void RecordRecent", favorite, StringComparison.Ordinal);
            int helper = source.IndexOf("private static bool ContainsObservableItem", recent, StringComparison.Ordinal);
            Assert.GreaterOrEqual(favorite, 0);
            Assert.Greater(recent, favorite);
            Assert.Greater(helper, recent);
            StringAssert.Contains("if (refreshing)", source.Substring(favorite, recent - favorite));
            StringAssert.Contains("拒绝修改收藏状态", source.Substring(favorite, recent - favorite));
            StringAssert.Contains("if (refreshing)", source.Substring(recent, helper - recent));
            StringAssert.Contains("拒绝写入最近使用状态", source.Substring(recent, helper - recent));
        }

        [Test]
        public void CommandPaletteProviderRegistrationIsRejectedDuringBuild()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int register = source.IndexOf("public static ESCommandPaletteRegistrationResult RegisterProvider", StringComparison.Ordinal);
            int registerCore = source.IndexOf("private static ESCommandPaletteRegistrationResult RegisterProviderCore", register, StringComparison.Ordinal);
            Assert.GreaterOrEqual(register, 0);
            Assert.Greater(registerCore, register);
            string body = source.Substring(register, registerCore - register);
            StringAssert.Contains("if (initializing || refreshing)", body);
            StringAssert.Contains("已拒绝重入注册 Provider", body);
            StringAssert.Contains("return rejected;", body);
        }

        [Test]
        public void CommandPaletteFavoritesAndRecentStayStableDuringRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("refreshObservationFavorites", source);
            StringAssert.Contains("refreshObservationRecent", source);
            StringAssert.Contains("refreshing && refreshObservationFavorites != null", source);
            StringAssert.Contains("refreshing && refreshObservationRecent != null", source);
            StringAssert.Contains("new List<string>(FavoriteIds).AsReadOnly()", source);
            StringAssert.Contains("new List<string>(RecentIds).AsReadOnly()", source);
            StringAssert.Contains("refreshObservationFavorites = null;", source);
            StringAssert.Contains("refreshObservationRecent = null;", source);
        }

        [Test]
        public void CommandPaletteProviderCountStaysStableDuringRefresh()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteRegistry.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            StringAssert.Contains("refreshObservationProviderCount", source);
            StringAssert.Contains("refreshing && refreshObservationProviderCount >= 0", source);
            StringAssert.Contains("refreshObservationProviderCount = ProviderOrder.Count;", source);
            StringAssert.Contains("refreshObservationProviderCount = -1;", source);
            int provider = source.IndexOf("public static int ProviderCount", StringComparison.Ordinal);
            int item = source.IndexOf("public static int ItemCount", provider, StringComparison.Ordinal);
            Assert.GreaterOrEqual(provider, 0);
            Assert.Greater(item, provider);
            StringAssert.Contains("refreshObservationProviderCount", source.Substring(provider, item - provider));
        }

        [Test]
        public void CommandPaletteExecutorHasFailClosedBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteExecutors.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int execute = source.IndexOf("public static ESCommandPaletteResult Execute", StringComparison.Ordinal);
            int openMenu = source.IndexOf("internal static class OpenMenuExecutor", execute, StringComparison.Ordinal);
            Assert.GreaterOrEqual(execute, 0);
            Assert.Greater(openMenu, execute);
            string body = source.Substring(execute, openMenu - execute);
            StringAssert.Contains("try", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("命令执行边界异常，已安全拒绝", body);
            StringAssert.Contains("ESCommandPaletteResult.Fail(", body);
        }

        [Test]
        public void CommandPaletteCopyTextHasBoundedFileRead()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteExecutors.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int copy = source.IndexOf("internal static class CopyTextExecutor", StringComparison.Ordinal);
            int select = source.IndexOf("internal static class SelectExecutor", copy, StringComparison.Ordinal);
            Assert.GreaterOrEqual(copy, 0);
            Assert.Greater(select, copy);
            string body = source.Substring(copy, select - copy);
            StringAssert.Contains("MaximumCopyTextBytes = 4L * 1024L * 1024L", body);
            StringAssert.Contains("new FileInfo(fullPath).Length > MaximumCopyTextBytes", body);
            StringAssert.Contains("ReadTextWithinLimit(fullPath)", body);
            StringAssert.Contains("byte[] buffer = new byte[capacity]", body);
            StringAssert.Contains("count > MaximumCopyTextBytes", body);
            StringAssert.Contains("文件超过复制大小上限", body);
        }

        [Test]
        public void CommandPaletteCopyPathHasClipboardFailureBoundary()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteExecutors.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int copyPath = source.IndexOf("public static ESCommandPaletteResult CopyPath", StringComparison.Ordinal);
            int select = source.IndexOf("internal static class SelectExecutor", copyPath, StringComparison.Ordinal);
            Assert.GreaterOrEqual(copyPath, 0);
            Assert.Greater(select, copyPath);
            string body = source.Substring(copyPath, select - copyPath);
            StringAssert.Contains("GUIUtility.systemCopyBuffer = normalizedPath", body);
            StringAssert.Contains("catch (Exception exception)", body);
            StringAssert.Contains("复制路径失败", body);
            StringAssert.Contains("ESCommandPaletteResult.Fail(", body);
        }

        [Test]
        public void CommandPaletteWindowClipboardShortcutsFailClosed()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESCommandPalette", "ESCommandPaletteWindow.cs");
            string source = File.ReadAllText(Path.GetFullPath(path), new UTF8Encoding(false, true));
            int helper = source.IndexOf("private static bool TryCopyTargetToClipboard", StringComparison.Ordinal);
            int shortcut = source.IndexOf("private void CopySelectedShortcut", StringComparison.Ordinal);
            Assert.GreaterOrEqual(helper, 0);
            Assert.Greater(shortcut, helper);
            StringAssert.Contains("catch (Exception exception)", source.Substring(helper, shortcut - helper));
            StringAssert.Contains("TryCopyTargetToClipboard(item.TargetId, out string copyError)", source.Substring(shortcut));
            StringAssert.Contains("复制路径失败：", source.Substring(shortcut));
        }

        [Test]
        public void ProgressCenterTickIsolatesProgressWindowFailures()
        {
            string path = Path.Combine(
                Application.dataPath, "Plugins", "ES", "Editor", "EditorTools",
                "ESAdvancedDialog", "ESAdvancedDialog.cs");
            string source = File.ReadAllText(path, new UTF8Encoding(false, true));
            int tick = source.IndexOf("private static void Tick()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(tick, 0);
            int end = source.IndexOf("private static void AddDetailLocked", tick, StringComparison.Ordinal);
            Assert.Greater(end, tick);
            string body = source.Substring(tick, end - tick);
            StringAssert.Contains("Debug.LogException(windowException);", body);
            StringAssert.Contains("Debug.LogException(closeException);", body);
            StringAssert.Contains("finally", body);
            StringAssert.Contains("window = null;", body);
        }
    }
}
