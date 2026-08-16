#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ES.Tests.Editor.World
{
    public sealed class ESWorkbenchAuthoringFoundationTests
    {
        private enum TestModule : byte
        {
            Core,
            Alpha,
            Beta,
            Gamma
        }

        private const string Owner = "ES.Tests.WorkbenchFoundation";

        [TearDown]
        public void TearDown()
        {
            ESWorkbenchContributionRegistry<TestModule>.ClearOwner(Owner);
        }

        [Test]
        public void DirtyStateHookReceivesExactRecoveryKeyAndFlags()
        {
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            try
            {
                window.MarkDirtyForTest("definition.regions", ESWorkbenchDirtyFlags.Authoring);

                Assert.AreEqual("definition.regions", window.LastDirtyKeyForTest);
                Assert.AreEqual(ESWorkbenchDirtyFlags.Authoring, window.LastDirtyFlagsForTest);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ContributionContextRegistersCompleteAuthoringSurface()
        {
            string workbenchId = "tests.workbench.complete-surface";
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "authoring",
                    "Authoring",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterViewport(CreateViewportDescriptor("viewport"));
                        context.RegisterObject(new ESWorkbenchObjectDescriptor("object", "Object", "Tests", null));
                        context.RegisterHierarchy(new ESWorkbenchHierarchyDescriptor("hierarchy", "Hierarchy"));
                        context.RegisterAuthoringAdapter(new ESWorkbenchAuthoringAdapterDescriptor(
                            "authoring",
                            _ => true,
                            _ => true,
                            create: _ => ESWorkbenchMutationResult.Success("Created")));
                        context.RegisterInspector(new ESWorkbenchInspectorDescriptor(
                            "inspector", _ => true, (_, __) => new VisualElement()));
                        context.RegisterTool(new ESWorkbenchToolDescriptor("tool", "Tool", _ => { }));
                        context.RegisterCommand(new ESWorkbenchCommandDescriptor("command", "Command", _ => { }));
                        context.RegisterIssueSource(new ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>(
                            "issues",
                            _ => new[]
                            {
                                new ESWorkbenchIssueDescriptor(
                                    "issue", "Issue", ESWorkbenchIssueSeverity.Warning)
                            }));
                        return null;
                    },
                    owner: Owner),
                out string registrationMessage), registrationMessage);

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual(1, session.Viewports.Count);
                Assert.AreEqual(1, session.Objects.Count);
                Assert.AreEqual(1, session.Hierarchy.Count);
                Assert.AreEqual(1, session.AuthoringAdapters.Count);
                Assert.AreEqual(1, session.Inspectors.Count);
                Assert.AreEqual(1, session.Tools.Count);
                Assert.AreEqual(1, session.Commands.Count);
                Assert.AreEqual(1, session.IssueSources.Count);
            }
        }

        [Test]
        public void DuplicateCapabilityIdKeepsFirstDeclarationAndReportsDiagnostic()
        {
            string workbenchId = "tests.workbench.duplicate-id";
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "duplicates",
                    "Duplicates",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.Validation,
                    context =>
                    {
                        context.RegisterTool(new ESWorkbenchToolDescriptor("same", "First", _ => { }));
                        context.RegisterTool(new ESWorkbenchToolDescriptor("same", "Second", _ => { }));
                        return null;
                    },
                    owner: Owner),
                out string registrationMessage), registrationMessage);

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual(1, session.Tools.Count);
                Assert.AreEqual("First", session.Tools[0].DisplayName);
                CollectionAssert.Contains(session.Diagnostics, "工具 ID 冲突：same，已保留首次声明。");
            }
        }

        [Test]
        public void PresentationAndBottomPanelsFollowModuleFiltering()
        {
            string workbenchId = "tests.workbench.presentation-panels";
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "beta.surface",
                    "Beta Surface",
                    TestModule.Beta,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "beta.presentation", "测试工作台"));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "beta.panel", "测试面板",
                            _ => new ESWorkbenchBottomPanelContent(new VisualElement())));
                        return null;
                    },
                    owner: Owner),
                out string betaMessage), betaMessage);
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "alpha.surface",
                    "Alpha Surface",
                    TestModule.Alpha,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "alpha.presentation", "不应加载"));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "alpha.panel", "不应加载",
                            _ => new ESWorkbenchBottomPanelContent(new VisualElement())));
                        return null;
                    },
                    owner: Owner),
                out string alphaMessage), alphaMessage);

            using (ESWorkbenchContributionSession<TestModule> session = Open(
                workbenchId,
                new[] { TestModule.Beta }))
            {
                Assert.AreEqual(1, session.Presentations.Count);
                Assert.AreEqual("测试工作台", session.Presentations[0].BrandTitle);
                Assert.AreEqual(1, session.BottomPanels.Count);
                Assert.AreEqual("beta.panel", session.BottomPanels[0].PanelId);
            }
        }

        [Test]
        public void DuplicatePresentationAndBottomPanelIdsKeepFirstDeclaration()
        {
            string workbenchId = "tests.workbench.presentation-panel-duplicates";
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "surface",
                    "Surface",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "main", "首次展示"));
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "main", "重复展示"));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "status", "首次面板",
                            _ => new ESWorkbenchBottomPanelContent(new VisualElement())));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "status", "重复面板",
                            _ => new ESWorkbenchBottomPanelContent(new VisualElement())));
                        return null;
                    },
                    owner: Owner),
                out string message), message);

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual("首次展示", session.Presentations.Single().BrandTitle);
                Assert.AreEqual("首次面板", session.BottomPanels.Single().Title);
                CollectionAssert.Contains(session.Diagnostics, "展示合同冲突：main，已保留 main。");
                CollectionAssert.Contains(session.Diagnostics, "底部面板 ID 冲突：status，已保留首次声明。");
            }
        }

        [Test]
        public void OpenFiltersDisabledModulesAndUsesModuleThenPriorityStableOrder()
        {
            string workbenchId = "tests.workbench.module-order";
            var injected = new List<string>();
            RegisterContribution(workbenchId, "alpha-high", TestModule.Alpha, injected, priority: 100);
            RegisterContribution(workbenchId, "beta-low", TestModule.Beta, injected, priority: 0);
            RegisterContribution(workbenchId, "beta-high", TestModule.Beta, injected, priority: 10);
            RegisterContribution(workbenchId, "gamma", TestModule.Gamma, injected, priority: 1000);

            using (ESWorkbenchContributionSession<TestModule> session = Open(
                workbenchId,
                new[] { TestModule.Beta, TestModule.Alpha, TestModule.Beta }))
            {
                CollectionAssert.AreEqual(
                    new[] { "beta-high", "beta-low", "alpha-high" },
                    injected,
                    "模块顺序必须优先于跨模块 Priority，重复模块只采用首次位置。");
                CollectionAssert.AreEqual(
                    new[] { "beta-high", "beta-low", "alpha-high" },
                    GetContributionIds(session.Descriptors));
            }
        }

        [Test]
        public void DisabledModuleCannotInjectPagesSlotsOrTools()
        {
            string workbenchId = "tests.workbench.module-filter";
            int injectionCount = 0;
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "gamma.surface",
                    "Gamma Surface",
                    TestModule.Gamma,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        injectionCount++;
                        context.RegisterPage(new ESWorkbenchPageDefinition(
                            "gamma", "Gamma", "Gamma", ESWorkbenchDirtyFlags.Authoring, () => { }));
                        context.RegisterAssetSlot(new ESWorkbenchAssetRegistrationSlot(
                            "gamma.slot", "Gamma", string.Empty, string.Empty, string.Empty,
                            typeof(UnityEngine.Object), default, "Gamma", "gamma"));
                        context.RegisterTool(new ESWorkbenchToolDescriptor("gamma.tool", "Gamma", _ => { }));
                        return null;
                    },
                    owner: Owner),
                out string message), message);

            using (ESWorkbenchContributionSession<TestModule> session = Open(
                workbenchId,
                new[] { TestModule.Core }))
            {
                Assert.Zero(injectionCount);
                Assert.Zero(session.Descriptors.Count);
                Assert.Zero(session.AssetSlots.Count);
                Assert.Zero(session.Tools.Count);
            }
        }

        [Test]
        public void DependenciesOverrideModuleOrderButRemainStable()
        {
            string workbenchId = "tests.workbench.dependency-order";
            var injected = new List<string>();
            RegisterContribution(
                workbenchId,
                "alpha",
                TestModule.Alpha,
                injected,
                priority: 100,
                dependencies: new[] { "beta" });
            RegisterContribution(workbenchId, "beta", TestModule.Beta, injected);

            using (Open(workbenchId, new[] { TestModule.Alpha, TestModule.Beta }))
                CollectionAssert.AreEqual(new[] { "beta", "alpha" }, injected);
        }

        [Test]
        public void MissingAndCyclicDependenciesAreSkippedWithDiagnostics()
        {
            string workbenchId = "tests.workbench.dependency-errors";
            var injected = new List<string>();
            RegisterContribution(workbenchId, "missing", TestModule.Core, injected,
                dependencies: new[] { "absent" });
            RegisterContribution(workbenchId, "cycle-a", TestModule.Alpha, injected,
                dependencies: new[] { "cycle-b" });
            RegisterContribution(workbenchId, "cycle-b", TestModule.Beta, injected,
                dependencies: new[] { "cycle-a" });

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.Zero(injected.Count);
                Assert.IsTrue(session.Diagnostics.Any(value => value.Contains("缺少依赖 absent")));
                Assert.IsTrue(session.Diagnostics.Any(value => value.Contains("存在循环或失败依赖")));
            }
        }

        [Test]
        public void SameRevisionRegistrationUsesLatestDelegate()
        {
            string workbenchId = "tests.workbench.reload-domain";
            var injected = new List<string>();
            RegisterContribution(workbenchId, "same", TestModule.Core, injected, marker: "old");
            RegisterContribution(workbenchId, "same", TestModule.Core, injected, marker: "latest");

            using (Open(workbenchId, new[] { TestModule.Core }))
                CollectionAssert.AreEqual(new[] { "latest" }, injected);
        }

        [Test]
        public void SelectionUsesStableIdentityButStillObservesObjectReplacement()
        {
            var service = new ESWorkbenchSelectionService();
            var firstObject = ScriptableObject.CreateInstance<TestAsset>();
            var replacement = ScriptableObject.CreateInstance<TestAsset>();
            int changes = 0;
            service.Changed += _ => changes++;
            try
            {
                service.Select(new ESWorkbenchSelection("same", "test", firstObject, "payload"));
                service.Select(new ESWorkbenchSelection("same", "test", firstObject, "payload"));
                service.Select(new ESWorkbenchSelection("same", "test", replacement, "payload"));

                Assert.AreEqual(2, changes);
                Assert.AreSame(replacement, service.Current.UnityObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void LayoutRoundTripKeepsStableSelectionWithoutUnityObjectReferences()
        {
            var state = new ESWorkbenchLayoutState
            {
                selectedStableId = "world.region.market",
                selectedKind = "world.region",
                selectedAssetGuid = "0123456789abcdef",
                compactSidePane = "inspector",
                responsiveLayoutInitialized = true,
                bottomDrawerExpanded = false,
                activeBottomTab = "performance"
            };
            state.hiddenHierarchyIds.Add("world.region.hidden");
            state.lockedHierarchyIds.Add("world.prefab.locked");

            ESWorkbenchLayoutState restored = JsonUtility.FromJson<ESWorkbenchLayoutState>(JsonUtility.ToJson(state));

            Assert.AreEqual("world.region.market", restored.selectedStableId);
            Assert.AreEqual("world.region", restored.selectedKind);
            Assert.AreEqual("0123456789abcdef", restored.selectedAssetGuid);
            Assert.AreEqual("inspector", restored.compactSidePane);
            Assert.IsFalse(restored.bottomDrawerExpanded);
            Assert.AreEqual("performance", restored.activeBottomTab);
            CollectionAssert.AreEqual(new[] { "world.region.hidden" }, restored.hiddenHierarchyIds);
            CollectionAssert.AreEqual(new[] { "world.prefab.locked" }, restored.lockedHierarchyIds);
        }

        [Test]
        public void HostBuildExposesAuthoringRailAndProductionDrawer()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var actions = new ESWorkbenchActionContext(
                window,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "ES 底座测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchPageDefinition>(),
                () => Array.Empty<ESWorkbenchViewportDescriptor>(),
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => new[] { new ESWorkbenchToolDescriptor("tool", "Tool", _ => { }) },
                () => Array.Empty<ESWorkbenchCommandDescriptor>(),
                new ESWorkbenchLayoutState(),
                _ => null,
                _ => { },
                () => string.Empty,
                () => new[]
                {
                    new ESWorkbenchIssueDescriptor(
                        "issue", "Issue", ESWorkbenchIssueSeverity.Warning)
                });
            try
            {
                VisualElement root = host.Build();

                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchToolRail"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchBottomDrawer"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchBottomContent"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchDropFeedback"));
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HostConsumesPresentationAndDeterministicallyReleasesBottomPanelContent()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var actions = new ESWorkbenchActionContext(
                window,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            int created = 0;
            int released = 0;
            var panels = new[]
            {
                new ESWorkbenchBottomPanelDescriptor(
                    "test.panel",
                    "测试通道",
                    _ =>
                    {
                        created++;
                        return new ESWorkbenchBottomPanelContent(
                            new Label("测试内容"),
                            () => released++);
                    },
                    priority: 1000)
            };
            var layout = new ESWorkbenchLayoutState { activeBottomTab = "test.panel" };
            var presentation = new ESWorkbenchHostPresentationDescriptor(
                "test.presentation",
                "ES 测试工作台",
                "测试资产",
                "测试视图",
                "测试视图说明",
                "测试检查器");
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "兼容标题",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchPageDefinition>(),
                () => Array.Empty<ESWorkbenchViewportDescriptor>(),
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => Array.Empty<ESWorkbenchCommandDescriptor>(),
                layout,
                _ => null,
                _ => { },
                () => string.Empty,
                null,
                null,
                () => panels,
                presentation);
            try
            {
                VisualElement root = host.Build();
                bool foundBrand = false;
                root.Query<Label>().ForEach(label =>
                    foundBrand |= label.text == "ES 测试工作台");
                Assert.IsTrue(foundBrand);
                Assert.AreEqual(1, created);
                Assert.Zero(released);

                host.UpdatePresentation(new ESWorkbenchHostPresentationDescriptor(
                    "test.presentation.reloaded",
                    "ES 重载后工作台"));
                host.RefreshRegistrations();

                Assert.AreEqual(2, created);
                Assert.AreEqual(1, released);
                bool foundReloadedBrand = false;
                root.Query<Label>().ForEach(label =>
                    foundReloadedBrand |= label.text == "ES 重载后工作台");
                Assert.IsTrue(foundReloadedBrand);
            }
            finally
            {
                host.Dispose();
                Assert.AreEqual(created, released);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HostRestoresSelectionFromFreshHierarchyDescriptor()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var selection = new ESWorkbenchSelectionService();
            var payload = new object();
            var hierarchy = new[]
            {
                new ESWorkbenchHierarchyDescriptor(
                    "stable.item", "Stable Item", kind: "test.item", payload: payload)
            };
            var layout = new ESWorkbenchLayoutState
            {
                selectedStableId = "stable.item",
                selectedKind = "test.item",
                responsiveLayoutInitialized = true
            };
            var actions = new ESWorkbenchActionContext(
                window,
                selection,
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "ES 底座测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchPageDefinition>(),
                () => Array.Empty<ESWorkbenchViewportDescriptor>(),
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => hierarchy,
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => Array.Empty<ESWorkbenchCommandDescriptor>(),
                layout,
                _ => null,
                _ => { },
                () => string.Empty);
            try
            {
                host.Build();

                Assert.AreEqual("stable.item", selection.Current.StableId);
                Assert.AreEqual("test.item", selection.Current.Kind);
                Assert.AreSame(payload, selection.Current.Payload);
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RegistrationRefreshDisposesViewportRemovedByDomainContribution()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var selection = new ESWorkbenchSelectionService();
            var viewports = new List<ESWorkbenchViewportDescriptor>();
            StubViewport created = null;
            viewports.Add(new ESWorkbenchViewportDescriptor(
                "transient", "Transient", ESWorkbenchViewportKind.Custom,
                _ => created = new StubViewport()));
            var actions = new ESWorkbenchActionContext(
                window,
                selection,
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "ES 底座测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchPageDefinition>(),
                () => viewports,
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => Array.Empty<ESWorkbenchCommandDescriptor>(),
                new ESWorkbenchLayoutState(),
                _ => null,
                _ => { },
                () => string.Empty);
            try
            {
                host.Build();
                Assert.IsNotNull(created);
                Assert.IsFalse(created.Disposed);

                viewports.Clear();
                host.RefreshRegistrations();

                Assert.IsTrue(created.Disposed);
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PreviewHeightSamplingNeverInitializesAuthoringSamples()
        {
            var field = new ESWorldMapHeightfield
            {
                width = 5,
                height = 5,
                defaultHeight = 0.35f,
                samples = new List<float>()
            };

            float direct = ESWorldHeightfieldReadOnly.Get(field, 2, 2);
            float sampled = ESWorldHeightfieldReadOnly.SampleNormalized(field, 0.5f, 0.5f);

            Assert.AreEqual(0.35f, direct, 0.0001f);
            Assert.AreEqual(0.35f, sampled, 0.0001f);
            Assert.AreEqual(0, field.samples.Count, "预览读取不得补齐或修改作者态高度场。");
        }

        [Test]
        public void WorkbenchBaseOwnsDefaultAuthoringCapabilityInitialization()
        {
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            try
            {
                window.InitializeForTest();

                Assert.IsFalse(window.AnimateOpeningFrameForTest,
                    "作者工作台不得在 Unity DockArea/HostView 绑定期间修改原生窗口外框。");
                CollectionAssert.AreEquivalent(
                    new[] { "core.canvas-2d", "core.preview-3d" },
                    window.ViewportIds);
                CollectionAssert.Contains(window.ToolIds, "core.select");
                CollectionAssert.Contains(window.CommandIds, "core.save");
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PageSwitchReleasesOnlyPageBeingLeftAndCleanupClearsDefinitions()
        {
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            int firstReleased = 0;
            int secondReleased = 0;
            try
            {
                window.RegisterPageForTest("first", () => firstReleased++);
                window.RegisterPageForTest("second", () => secondReleased++);

                window.SelectPageForTest("first");
                window.SelectPageForTest("second");
                window.SelectPageForTest("second");

                Assert.AreEqual(1, firstReleased);
                Assert.Zero(secondReleased);
                Assert.AreEqual(2, window.PageCountForTest);

                window.ReleaseForTest();

                Assert.AreEqual(1, firstReleased);
                Assert.AreEqual(1, secondReleased);
                Assert.Zero(window.PageCountForTest);
                Assert.AreEqual(string.Empty, window.SelectedPageIdForTest);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReloadRebindAndCloseReleaseCurrentPageWithoutKeepingOldClosure()
        {
            int oldReleased = 0;
            int latestReleased = 0;
            RegisterPageDescriptor(TestWorkbenchWindow.WorkbenchIdForTest, "old", () => oldReleased++);
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            try
            {
                window.InitializeForTest();
                Assert.AreEqual(1, window.PageCountForTest);

                RegisterPageDescriptor(TestWorkbenchWindow.WorkbenchIdForTest, "latest", () => latestReleased++);
                window.ReloadForTest();
                Assert.AreEqual(1, oldReleased, "贡献重载必须释放旧页面闭包。");
                Assert.AreEqual(1, window.PageCountForTest, "重复加载不得保留旧页面定义。");

                window.BindAssetForTest(asset);
                Assert.AreEqual(1, latestReleased, "资产重绑必须释放重绑前的当前页。");
                Assert.AreEqual(1, window.PageCountForTest);

                window.ReleaseForTest();
                Assert.AreEqual(2, latestReleased, "窗口清理必须释放重绑后的当前页。");
                Assert.Zero(window.PageCountForTest);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RemovingModuleReleasesOldPageAndFallsBackToRemainingPage()
        {
            int coreReleased = 0;
            int alphaReleased = 0;
            RegisterPageDescriptor(
                TestWorkbenchWindow.WorkbenchIdForTest,
                "core-page",
                "core",
                TestModule.Core,
                () => coreReleased++);
            RegisterPageDescriptor(
                TestWorkbenchWindow.WorkbenchIdForTest,
                "alpha-page",
                "alpha",
                TestModule.Alpha,
                () => alphaReleased++);
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            window.ModulesForTest.Add(TestModule.Alpha);
            try
            {
                window.InitializeForTest();
                window.SelectPageForTest("alpha");
                Assert.AreEqual(1, coreReleased);

                window.ModulesForTest.Remove(TestModule.Alpha);
                window.ReloadForTest();

                Assert.AreEqual(1, alphaReleased);
                Assert.AreEqual(1, window.PageCountForTest);
                Assert.AreEqual("core", window.SelectedPageIdForTest);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void IntegrationTestWindowUsesFormalContributionRegistry()
        {
            var window = ScriptableObject.CreateInstance<ESWorkbenchIntegrationTestWindow>();
            try
            {
                window.InitializeForTest();
                Assert.AreEqual(10, window.RegisteredContributionCountForTest);
                Assert.AreEqual(10, window.RegisteredPageCountForTest);
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldFirstEnableCreatesSingleContributionSession()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            try
            {
                window.InitializeForTest();
                Assert.AreEqual(1, window.ContributionLoadCountForTest);
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DynamicObjectSourceObservesDataChangesWithoutReopeningContributionSession()
        {
            string workbenchId = "tests.workbench.dynamic-source";
            var sourceItems = new List<ESWorkbenchObjectDescriptor>
            {
                new ESWorkbenchObjectDescriptor("first", "First", "Tests", null)
            };
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "dynamic",
                    "Dynamic",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterObjectSource(new ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>(
                            "tests.dynamic.objects", _ => sourceItems));
                        return null;
                    },
                    owner: Owner),
                out string registrationMessage), registrationMessage);

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual(1, session.ObjectSources.Count);
                CollectionAssert.AreEqual(new[] { "first" },
                    GetObjectIds(session.ObjectSources[0].Query(null)));

                sourceItems.Add(new ESWorkbenchObjectDescriptor("second", "Second", "Tests", null));

                CollectionAssert.AreEqual(new[] { "first", "second" },
                    GetObjectIds(session.ObjectSources[0].Query(null)));
            }
        }

        [Test]
        public void ToolStateActivationIsStableAndIdempotent()
        {
            var service = new ESWorkbenchToolStateService();
            var observed = new List<string>();
            service.Changed += observed.Add;

            service.Activate("world.select");
            service.Activate("world.select");
            service.Activate("world.region");

            Assert.AreEqual("world.region", service.ActiveToolId);
            CollectionAssert.AreEqual(new[] { "world.select", "world.region" }, observed);
        }

        [Test]
        public void World2DViewportUsesNativeUiToolkitRenderingSurface()
        {
            var layout = new ESWorkbenchViewportLayoutState
            {
                viewportId = "test",
                pan = new Vector2(40f, -25f),
                zoom = 2.5f
            };
            var viewport = new ESWorldMap2DViewportElement(
                _ => { },
                CreateActionContextForTest(),
                new ESWorkbenchSelectionService(),
                layout);
            try
            {
                Assert.IsNull(viewport.Q<IMGUIContainer>(), "2D 作者画布不得回退为 IMGUIContainer。");
                Assert.GreaterOrEqual(viewport.style.minHeight.value.value, 240f);
                viewport.FrameAll();
                Assert.AreEqual(Vector2.zero, layout.pan);
                Assert.AreEqual(1f, layout.zoom);
            }
            finally
            {
                viewport.Dispose();
            }
        }

        [Test]
        public void Default2DViewportProjectsStableHierarchyWithoutTemporaryNodes()
        {
            var hierarchy = new[]
            {
                new ESWorkbenchHierarchyDescriptor(
                    "spatial.one",
                    "Spatial One",
                    kind: "test.spatial",
                    spatial: new ESWorkbenchSpatialDescriptor(
                        new Vector3(12f, 0f, 18f),
                        new Vector3(4f, 1f, 6f),
                        shape: ESWorkbenchSpatialShape.Rectangle))
            };
            ESWorkbenchActionContext actions = CreateActionContextForTest();
            var viewportContext = new ESWorkbenchViewportContext(
                null,
                actions,
                "test.canvas",
                new ESWorkbenchViewportLayoutState { viewportId = "test.canvas" },
                () => hierarchy);
            var viewport = new ESWorkbenchCanvas2DViewport(viewportContext);
            try
            {
                Assert.IsNull(viewport.Q<IMGUIContainer>(), "通用 2D 作者画布不得保存 IMGUI 临时节点。 ");
                Assert.IsNotNull(viewport.Q<VisualElement>("ESWorkbenchCanvas2DLabels"));
                Assert.IsNotNull(viewport.Q<Label>("ESWorkbenchSpatialLabel"));
                viewport.FrameAll();
                Assert.AreEqual(Vector2.zero, viewportContext.Layout.pan);
                Assert.AreEqual(1f, viewportContext.Layout.zoom);
            }
            finally
            {
                viewport.Dispose();
            }
        }

        [Test]
        public void ViewportSnapStateQuantizesTransformsDeterministically()
        {
            var layout = new ESWorkbenchViewportLayoutState
            {
                viewportId = "snap",
                snapEnabled = true,
                moveSnap = 0.5f,
                rotationSnap = 15f,
                scaleSnap = 0.25f
            };
            var context = new ESWorkbenchViewportContext(null, CreateActionContextForTest(), "snap", layout);

            Assert.AreEqual(new Vector3(1f, 2f, -0.5f), context.SnapPosition(new Vector3(1.2f, 1.8f, -0.4f)));
            Assert.AreEqual(new Vector3(0f, 45f, 0f), context.SnapRotation(new Vector3(2f, 38f, -3f)));
            Assert.AreEqual(new Vector3(1.25f, 2f, 3.5f), context.SnapScale(new Vector3(1.2f, 2.1f, 3.4f)));
        }

        [Test]
        public void RegionDuplicateKeepsSizeWhenOffsetReachesWorldBoundary()
        {
            ESWorldBuilderWorkbenchWindow.OffsetRegionWithinWorld(
                new Vector2(80f, 82f),
                new Vector2(100f, 100f),
                new Vector2(12f, 12f),
                Vector2.zero,
                new Vector2(100f, 100f),
                out Vector2 resultMin,
                out Vector2 resultMax);

            Assert.AreEqual(new Vector2(80f, 82f), resultMin);
            Assert.AreEqual(new Vector2(100f, 100f), resultMax);
            Assert.AreEqual(new Vector2(20f, 18f), resultMax - resultMin);
        }

        [Test]
        public void AuthoringServiceOwnsDirtySelectionAndFailureRollback()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var selection = new ESWorkbenchSelectionService();
            var tools = new ESWorkbenchToolStateService();
            var service = new ESWorkbenchAuthoringService();
            string dirtyKey = string.Empty;
            int refreshCount = 0;
            var actions = new ESWorkbenchActionContext(
                null,
                selection,
                tools,
                service,
                (_, __) => { },
                (_, __) => { },
                _ => refreshCount++,
                (key, _) => dirtyKey = key);
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.authoring",
                value => value?.Kind == "test.object",
                _ => true,
                create: _ =>
                {
                    target.counter++;
                    return ESWorkbenchMutationResult.Success(
                        "Created",
                        new ESWorkbenchSelection("test.1", "test.object", target, null),
                        "test.items");
                },
                delete: _ =>
                {
                    target.counter = 99;
                    return ESWorkbenchMutationResult.Failure("Rejected");
                },
                resolveUndoTargets: _ => new UnityEngine.Object[] { target });
            service.Bind(actions, () => new[] { adapter });
            try
            {
                Assert.IsTrue(service.TryCreate(
                    new ESWorkbenchObjectDescriptor("palette", "Palette", "Tests", target),
                    Vector3.zero,
                    out string createMessage), createMessage);
                Assert.AreEqual(1, target.counter);
                Assert.AreEqual("test.1", selection.Current.StableId);
                Assert.AreEqual("test.items", dirtyKey);
                Assert.AreEqual(1, refreshCount);

                Assert.IsFalse(service.TryDelete(selection.Current, out string deleteMessage));
                Assert.AreEqual("Rejected", deleteMessage);
                Assert.AreEqual(1, target.counter, "失败事务必须恢复操作前的作者数据。");
                Assert.AreEqual(1, refreshCount, "失败事务不得发布数据刷新。");
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoringServiceRoutesRotateAndScaleOnlyWhenAdapterAllowsThem()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var selection = new ESWorkbenchSelectionService();
            var service = new ESWorkbenchAuthoringService();
            var actions = new ESWorkbenchActionContext(
                null,
                selection,
                new ESWorkbenchToolStateService(),
                service,
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.transform",
                value => value?.Kind == "test.prefab" || value?.Kind == "test.point",
                move: null,
                resolveUndoTargets: _ => new UnityEngine.Object[] { target },
                canRotate: value => value?.Kind == "test.prefab",
                rotate: context =>
                {
                    target.vector = context.RotationEuler;
                    return ESWorkbenchMutationResult.Success("Rotated", context.Selection, "test.transform");
                },
                canScale: value => value?.Kind == "test.prefab",
                scale: context =>
                {
                    target.vector = context.Scale;
                    return ESWorkbenchMutationResult.Success("Scaled", context.Selection, "test.transform");
                });
            service.Bind(actions, () => new[] { adapter });
            try
            {
                var prefabSelection = new ESWorkbenchSelection("prefab", "test.prefab", target, null);
                var pointSelection = new ESWorkbenchSelection("point", "test.point", target, null);

                Assert.IsTrue(service.CanRotate(prefabSelection));
                Assert.IsTrue(service.CanScale(prefabSelection));
                Assert.IsFalse(service.CanRotate(pointSelection));
                Assert.IsFalse(service.CanScale(pointSelection));
                Assert.IsTrue(service.TryRotate(prefabSelection, new Vector3(0f, 45f, 0f), out string rotateMessage), rotateMessage);
                Assert.AreEqual(new Vector3(0f, 45f, 0f), target.vector);
                Assert.IsTrue(service.TryScale(prefabSelection, new Vector3(2f, 3f, 4f), out string scaleMessage), scaleMessage);
                Assert.AreEqual(new Vector3(2f, 3f, 4f), target.vector);
                Assert.IsFalse(service.TryRotate(pointSelection, new Vector3(0f, 90f, 0f), out _));
                Assert.AreEqual(new Vector3(2f, 3f, 4f), target.vector,
                    "直接执行也不得绕过旋转能力谓词。");
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoringServiceIsolatesBrokenAdapterAndContinuesWithHealthyAdapter()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var service = new ESWorkbenchAuthoringService();
            int isolatedErrors = 0;
            bool unrelatedMatchEvaluated = false;
            var actions = new ESWorkbenchActionContext(
                null,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                service,
                (_, type) => { if (type == MessageType.Error) isolatedErrors++; },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var broken = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.broken",
                _ => throw new InvalidOperationException("Broken adapter"),
                move: _ => ESWorkbenchMutationResult.Success("Should not execute"),
                resolveUndoTargets: _ => new UnityEngine.Object[] { target },
                priority: 100);
            var deleteOnly = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.delete-only",
                _ =>
                {
                    unrelatedMatchEvaluated = true;
                    return true;
                },
                delete: _ => ESWorkbenchMutationResult.Success("Deleted"),
                resolveUndoTargets: _ => new UnityEngine.Object[] { target },
                priority: 200);
            var healthy = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.healthy",
                value => value?.Kind == "test.object",
                move: context =>
                {
                    target.vector = context.WorldPosition;
                    return ESWorkbenchMutationResult.Success("Moved");
                },
                resolveUndoTargets: _ => new UnityEngine.Object[] { target });
            service.Bind(actions, () => new[] { deleteOnly, broken, healthy });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Broken adapter"));
            try
            {
                var selection = new ESWorkbenchSelection("test", "test.object", target, null);
                Assert.IsTrue(service.TryMove(selection, new Vector3(3f, 0f, 4f), out string message), message);
                Assert.AreEqual(new Vector3(3f, 0f, 4f), target.vector);
                Assert.AreEqual(1, isolatedErrors);
                Assert.IsFalse(unrelatedMatchEvaluated,
                    "不支持当前操作的适配器不应参与选择匹配。");
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoringServiceRejectsMutationWithoutUndoTargetsBeforeCallback()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var service = new ESWorkbenchAuthoringService();
            bool callbackExecuted = false;
            var actions = new ESWorkbenchActionContext(
                null,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                service,
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.no-undo",
                value => value?.Kind == "test.object",
                move: _ =>
                {
                    callbackExecuted = true;
                    target.counter++;
                    return ESWorkbenchMutationResult.Success("Moved");
                });
            service.Bind(actions, () => new[] { adapter });
            try
            {
                var selection = new ESWorkbenchSelection("test", "test.object", target, null);
                Assert.IsFalse(service.TryMove(selection, Vector3.one, out string message));
                StringAssert.Contains("Undo 目标", message);
                Assert.IsFalse(callbackExecuted);
                Assert.AreEqual(0, target.counter);
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoringServiceRejectsLockedMutationBeforeUndoAndCallback()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var service = new ESWorkbenchAuthoringService();
            bool callbackExecuted = false;
            string status = string.Empty;
            var actions = new ESWorkbenchActionContext(
                null,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                service,
                (message, _) => status = message,
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.locked",
                value => value?.Kind == "test.object",
                move: _ =>
                {
                    callbackExecuted = true;
                    target.counter++;
                    return ESWorkbenchMutationResult.Success("Moved");
                },
                resolveUndoTargets: _ => new UnityEngine.Object[] { target });
            service.Bind(
                actions,
                () => new[] { adapter },
                (kind, _, __) => kind == ESWorkbenchMutationKind.Move ? "对象已锁定。" : string.Empty);
            try
            {
                var selection = new ESWorkbenchSelection("test", "test.object", target, null);
                Assert.IsFalse(service.CanMove(selection));
                Assert.IsFalse(service.TryMove(selection, Vector3.one, out string message));
                Assert.AreEqual("对象已锁定。", message);
                Assert.AreEqual("对象已锁定。", status);
                Assert.IsFalse(callbackExecuted);
                Assert.AreEqual(0, target.counter);
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void WorkbenchShortcutRoutingIgnoresTextEditingTargets()
        {
            var root = new VisualElement();
            var textField = new TextField();
            var passive = new VisualElement();
            root.Add(textField);
            root.Add(passive);

            Assert.IsTrue(ESWorkbenchUIToolkitHost.IsTextEditingTarget(textField));
            Assert.IsFalse(ESWorkbenchUIToolkitHost.IsTextEditingTarget(passive));
        }

        [Test]
        public void AuthoringServiceReportsPostCommitFailureWithoutPretendingRollback()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var service = new ESWorkbenchAuthoringService();
            var actions = new ESWorkbenchActionContext(
                null,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                service,
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.post-commit",
                value => value?.Kind == "test.object",
                move: _ =>
                {
                    target.counter = 7;
                    return ESWorkbenchMutationResult.Success("Moved");
                },
                resolveUndoTargets: _ => new UnityEngine.Object[] { target },
                committed: (_, __) => throw new InvalidOperationException("Post commit failed"));
            service.Bind(actions, () => new[] { adapter });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Post commit failed"));
            try
            {
                var selection = new ESWorkbenchSelection("test", "test.object", target, null);
                Assert.IsTrue(service.TryMove(selection, Vector3.one, out string message));
                Assert.AreEqual(7, target.counter, "提交后失败不得谎报作者数据已经回滚。");
                Assert.IsTrue(service.LastOperationCommittedWithPostCommitFailure);
                StringAssert.Contains("已提交，但提交后同步失败", message);
            }
            finally
            {
                service.Unbind();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void RegisterContribution(
            string workbenchId,
            string contributionId,
            TestModule module,
            List<string> injected,
            int priority = 0,
            IEnumerable<string> dependencies = null,
            string marker = null)
        {
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    contributionId,
                    contributionId,
                    module,
                    ESWorkbenchContributionCategory.General,
                    _ =>
                    {
                        injected.Add(marker ?? contributionId);
                        return null;
                    },
                    owner: Owner,
                    priority: priority,
                    dependencies: dependencies),
                out string message), message);
        }

        private static void RegisterPageDescriptor(
            string workbenchId,
            string marker,
            Action release)
        {
            RegisterPageDescriptor(workbenchId, "page", "page", TestModule.Core, release, marker);
        }

        private static void RegisterPageDescriptor(
            string workbenchId,
            string contributionId,
            string pageId,
            TestModule module,
            Action release,
            string displayName = null)
        {
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    contributionId,
                    displayName ?? pageId,
                    module,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterPage(new ESWorkbenchPageDefinition(
                            pageId,
                            displayName ?? pageId,
                            pageId,
                            ESWorkbenchDirtyFlags.Authoring,
                            () => { },
                            release: release));
                        return null;
                    },
                    owner: Owner,
                    revision: 1),
                out string message), message);
        }

        private static string[] GetContributionIds(
            IEnumerable<ESWorkbenchContributionDescriptor<TestModule>> descriptors)
        {
            return descriptors.Select(value => value.ContributionId).ToArray();
        }

        private static ESWorkbenchContributionSession<TestModule> Open(
            string workbenchId,
            IEnumerable<TestModule> modules = null)
        {
            return ESWorkbenchContributionRegistry<TestModule>.Open(
                workbenchId,
                modules ?? new[] { TestModule.Core, TestModule.Alpha, TestModule.Beta, TestModule.Gamma },
                new object(),
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { });
        }

        private static ESWorkbenchActionContext CreateActionContextForTest()
        {
            return new ESWorkbenchActionContext(
                null,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
        }

        private static ESWorkbenchViewportDescriptor CreateViewportDescriptor(string id)
        {
            return new ESWorkbenchViewportDescriptor(
                id, id, ESWorkbenchViewportKind.Custom, _ => new StubViewport());
        }

        private static string[] GetObjectIds(IEnumerable<ESWorkbenchObjectDescriptor> source)
        {
            var result = new List<string>();
            foreach (ESWorkbenchObjectDescriptor item in source) result.Add(item.ObjectId);
            return result.ToArray();
        }

        private sealed class TestAsset : ScriptableObject
        {
            public int counter;
            public Vector3 vector;
        }

        private sealed class TestEditorWindow : EditorWindow
        {
        }

        private sealed class TestWorkbenchWindow : ESWorkbenchWindowBase<TestWorkbenchWindow, TestAsset, TestModule>
        {
            public const string WorkbenchIdForTest = "tests.workbench.window-lifecycle";
            public List<TestModule> ModulesForTest { get; } = new List<TestModule> { TestModule.Core };
            protected override List<TestModule> ESWorkbench_DefaultModules => new List<TestModule>(ModulesForTest);
            protected override string ESWorkbench_WorkbenchId => WorkbenchIdForTest;
            public bool AnimateOpeningFrameForTest => ESWindow_AnimateOpeningFrame;
            public string LastDirtyKeyForTest { get; private set; }
            public ESWorkbenchDirtyFlags LastDirtyFlagsForTest { get; private set; }

            public IReadOnlyList<string> ViewportIds
            {
                get
                {
                    var values = new List<string>();
                    for (int i = 0; i < ESWorkbench_Viewports.Count; i++) values.Add(ESWorkbench_Viewports[i].ViewportId);
                    return values;
                }
            }

            public IReadOnlyList<string> ToolIds
            {
                get
                {
                    var values = new List<string>();
                    for (int i = 0; i < ESWorkbench_Tools.Count; i++) values.Add(ESWorkbench_Tools[i].ToolId);
                    return values;
                }
            }

            public IReadOnlyList<string> CommandIds
            {
                get
                {
                    var values = new List<string>();
                    for (int i = 0; i < ESWorkbench_Commands.Count; i++) values.Add(ESWorkbench_Commands[i].CommandId);
                    return values;
                }
            }

            public void InitializeForTest() => base.ESWindow_OnHostEnable();
            public void ReleaseForTest() => ESWorkbench_ReleaseContributions();
            public void ReloadForTest() => ESWorkbench_LoadContributions();
            public void BindAssetForTest(TestAsset asset) => ESWorkbench_BindAsset(asset);
            public void RegisterPageForTest(string pageId, Action release) => ESWorkbench_RegisterPage(
                new ESWorkbenchPageDefinition(
                    pageId,
                    pageId,
                    pageId,
                    ESWorkbenchDirtyFlags.Authoring,
                    () => { },
                    release: release));
            public void SelectPageForTest(string pageId) => ESWorkbench_SelectPage(pageId);
            public int PageCountForTest => ESWorkbench_Pages.Count;
            public string SelectedPageIdForTest => ESWorkbench_SelectedPageId;
            public void MarkDirtyForTest(string key, ESWorkbenchDirtyFlags flags) => ESWorkbench_MarkDirty(key, flags);
            protected override void ESWorkbench_OnDirtyStateChanged(string dirtyKey, ESWorkbenchDirtyFlags flags)
            {
                LastDirtyKeyForTest = dirtyKey;
                LastDirtyFlagsForTest = flags;
            }
            public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("Test Workbench");
        }

        private sealed class StubViewport : IESWorkbenchViewport
        {
            public VisualElement Root { get; } = new VisualElement();
            public bool Disposed { get; private set; }
            public void Activate() { }
            public void Deactivate() { }
            public void Refresh(ESWorkbenchRefreshReason reason) { }
            public bool CanAccept(ESWorkbenchObjectDescriptor item) => true;
            public bool TryAccept(ESWorkbenchDropContext context, out string message)
            {
                message = string.Empty;
                return true;
            }
            public void Dispose() => Disposed = true;
        }
    }
}
#endif
