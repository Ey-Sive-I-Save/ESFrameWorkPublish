#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
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
        public void DuplicateDocumentAndAuthoringModeIdsKeepFirstStableDeclaration()
        {
            const string workbenchId = "tests.workbench.document-mode-duplicates";
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "surface",
                    "Surface",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            "authoring", "首次文档", "", true, ESWorkbenchDirtyFlags.Authoring));
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            "authoring", "重复文档", "", true, ESWorkbenchDirtyFlags.Authoring));
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            "terrain", "首次模式", ""));
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            "terrain", "重复模式", ""));
                        return null;
                    },
                    owner: Owner),
                out string message), message);

            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual("首次文档", session.Documents.Single().title);
                Assert.AreEqual("首次模式", session.AuthoringModes.Single().Title);
                CollectionAssert.Contains(session.Diagnostics, "文档 ID 冲突：authoring，已保留首次声明。");
                CollectionAssert.Contains(session.Diagnostics, "作者模式 ID 冲突：terrain，已保留首次声明。");
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
        public void DisabledModuleCannotInjectDocumentsModesSlotsOrTools()
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
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            "gamma", "Gamma", "Gamma", false, ESWorkbenchDirtyFlags.Authoring, () => { }));
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            "gamma", "Gamma", "Gamma"));
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
                Assert.Zero(session.Documents.Count);
                Assert.Zero(session.AuthoringModes.Count);
            }
        }

        [Test]
        public void ActiveContributionCountTracksSuccessfulInjectionAndInvalidatesOnDispose()
        {
            string workbenchId = "tests.workbench.active-contribution-count";
            int releaseCount = 0;
            int thrownInjectionCount = 0;
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "success",
                    "成功贡献",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    _ => new ESWorkbenchBottomPanelContent(new VisualElement(), () => releaseCount++),
                    owner: Owner),
                out string successMessage), successMessage);
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "disabled",
                    "禁用贡献",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    _ =>
                    {
                        Assert.Fail("禁用贡献不应执行 Inject。");
                        return null;
                    },
                    owner: Owner,
                    isEnabled: _ => false),
                out string disabledMessage), disabledMessage);
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    "throws",
                    "异常贡献",
                    TestModule.Core,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        thrownInjectionCount++;
                        context.RegisterTool(new ESWorkbenchToolDescriptor("throws.tool", "异常工具", _ => { }));
                        throw new InvalidOperationException("test injection failure");
                    },
                    owner: Owner),
                out string throwingMessage), throwingMessage);

            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: test injection failure"));
            using (ESWorkbenchContributionSession<TestModule> session = Open(workbenchId))
            {
                Assert.AreEqual(3, session.Descriptors.Count);
                Assert.AreEqual(1, session.ActiveDescriptors.Count);
                Assert.AreEqual(1, session.Entries.Count);
                Assert.Zero(session.Tools.Count, "注入异常不得留下已经登记的半成品工具。");
                Assert.AreEqual(1, thrownInjectionCount);
                Assert.IsFalse(session.IsDisposed);

                session.Dispose();
                Assert.IsTrue(session.IsDisposed);
                Assert.Zero(session.ActiveDescriptors.Count);
                Assert.Zero(session.Entries.Count);
                Assert.Zero(session.Tools.Count);
                session.Dispose();
                Assert.AreEqual(1, releaseCount, "重复 Dispose 不得重复释放成功贡献。");
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
        public void ContentDescriptorCarriesTypeDragContractAndSelectsDescriptorIdentity()
        {
            var payload = new object();
            var descriptor = new ESWorkbenchObjectDescriptor(
                "content.brush.height",
                "高度笔刷",
                "地形塑形",
                null,
                payload,
                contentKind: ESWorkbenchContentKind.Brush,
                dragMode: ESWorkbenchContentDragMode.ActivateTool,
                selectionKind: "test.content.brush");

            ESWorkbenchSelection selection = descriptor.ToSelection();

            Assert.AreEqual(ESWorkbenchContentKind.Brush, descriptor.ContentKind);
            Assert.AreEqual(ESWorkbenchContentDragMode.ActivateTool, descriptor.DragMode);
            Assert.AreEqual("笔刷", descriptor.ContentKindDisplayName);
            Assert.AreEqual("拖入使用", descriptor.DefaultDragHint);
            Assert.AreEqual("test.content.brush", selection.Kind);
            Assert.AreSame(descriptor, selection.Payload,
                "选择服务必须持有可重新解析的内容描述，而不是泄漏领域临时 Payload。");
            Assert.AreSame(payload, descriptor.Payload);
            Assert.IsTrue(ESWorkbenchUIToolkitHost.MatchesContentKind(descriptor, "Brush"));
            Assert.IsFalse(ESWorkbenchUIToolkitHost.MatchesContentKind(descriptor, "Prefab"));
        }

        [Test]
        public void ContentPresetVariantKeepsBaseIdentityAndOverridesOnlyDeclaredPayload()
        {
            var presetPayload = new object();
            var descriptor = new ESWorkbenchObjectDescriptor(
                "world.region",
                "区域",
                "环境/区域",
                null,
                payload: "base",
                contentKind: ESWorkbenchContentKind.RegionTemplate,
                dragMode: ESWorkbenchContentDragMode.CreateRegion,
                presets: new[]
                {
                    new ESWorkbenchContentPresetDescriptor(
                        "large", "大型", payload: presetPayload, overridePayload: true,
                        subtitle: "96 × 96 米", badge: "大型")
                });

            ESWorkbenchObjectDescriptor variant = descriptor.CreatePresetVariant("large");

            Assert.AreEqual("world.region", variant.BaseObjectId);
            Assert.AreEqual("world.region::preset::large", variant.ObjectId);
            Assert.AreEqual("large", variant.PresetId);
            Assert.AreSame(presetPayload, variant.Payload);
            Assert.AreEqual("96 × 96 米", variant.Subtitle);
            Assert.AreEqual("大型", variant.Badge);
            Assert.AreEqual("world.region", variant.ToSelection().StableId,
                "稳定选择应保持基础内容身份，预设通过新描述器代际触发 Inspector 刷新。 ");
            Assert.IsTrue(ESWorkbenchUIToolkitHost.MatchesContentCategory(variant, "环境"));
            Assert.IsTrue(ESWorkbenchUIToolkitHost.MatchesContentCategory(variant, "环境/区域"));
            Assert.IsFalse(ESWorkbenchUIToolkitHost.MatchesContentCategory(variant, "角色"));
        }

        [Test]
        public void ContentUsageStorePersistsFavoriteAndUsageByBaseIdentityWithoutLeakingEditorPrefs()
        {
            string workbenchId = "foundation-content-usage-" + Guid.NewGuid().ToString("N");
            string preferencesKey = "ES.Workbench.ContentUsage.v1."
                + Hash128.Compute(workbenchId).ToString();
            try
            {
                var first = new ESWorkbenchContentUsageStore(workbenchId);
                Assert.IsTrue(first.ToggleFavorite("world.prefab.house::preset::large"));
                first.RecordUse("world.prefab.house::preset::small");

                ESWorkbenchContentUsageRecord firstRecord = first.Get("world.prefab.house");
                Assert.IsNotNull(firstRecord);
                Assert.IsTrue(firstRecord.favorite);
                Assert.AreEqual(1, firstRecord.useCount);
                Assert.Greater(firstRecord.lastUsedUtcTicks, 0L);

                var reopened = new ESWorkbenchContentUsageStore(workbenchId);
                ESWorkbenchContentUsageRecord restored = reopened.Get(
                    "world.prefab.house::preset::standard");
                Assert.IsNotNull(restored);
                Assert.IsTrue(restored.favorite);
                Assert.AreEqual(1, restored.useCount);
                Assert.AreEqual("world.prefab.house", restored.objectId);
            }
            finally
            {
                EditorPrefs.DeleteKey(preferencesKey);
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
                layoutPreset = ESWorkbenchLayoutPreset.Diagnostics,
                bottomDrawerExpanded = false,
                bottomDrawerUserSized = true,
                activeBottomTab = "performance",
                activeContentKind = "RegionTemplate",
                activeContentCategory = "环境/区域",
                contentViewMode = ESWorkbenchContentViewMode.Grid,
                contentSortMode = ESWorkbenchContentSortMode.MostUsed,
                contentScope = ESWorkbenchContentScope.Favorites,
                contentBatchSpacing = 6f
            };
            state.hiddenHierarchyIds.Add("world.region.hidden");
            state.lockedHierarchyIds.Add("world.prefab.locked");
            state.selectedContentIds.Add("world.asset.tree");
            state.expandedContentCategoryPaths.Add("环境");
            state.contentPresetSelections.Add(new ESWorkbenchContentPresetSelectionState
            {
                objectId = "world.region.playable",
                presetId = "large"
            });

            ESWorkbenchLayoutState restored = JsonUtility.FromJson<ESWorkbenchLayoutState>(JsonUtility.ToJson(state));

            Assert.AreEqual("world.region.market", restored.selectedStableId);
            Assert.AreEqual("world.region", restored.selectedKind);
            Assert.AreEqual("0123456789abcdef", restored.selectedAssetGuid);
            Assert.AreEqual("inspector", restored.compactSidePane);
            Assert.AreEqual(ESWorkbenchLayoutPreset.Diagnostics, restored.layoutPreset);
            Assert.IsFalse(restored.bottomDrawerExpanded);
            Assert.IsTrue(restored.bottomDrawerUserSized);
            Assert.AreEqual("performance", restored.activeBottomTab);
            Assert.AreEqual("RegionTemplate", restored.activeContentKind);
            Assert.AreEqual("环境/区域", restored.activeContentCategory);
            Assert.AreEqual(ESWorkbenchContentViewMode.Grid, restored.contentViewMode);
            Assert.AreEqual(ESWorkbenchContentSortMode.MostUsed, restored.contentSortMode);
            Assert.AreEqual(ESWorkbenchContentScope.Favorites, restored.contentScope);
            Assert.AreEqual(6f, restored.contentBatchSpacing);
            CollectionAssert.AreEqual(new[] { "world.region.hidden" }, restored.hiddenHierarchyIds);
            CollectionAssert.AreEqual(new[] { "world.prefab.locked" }, restored.lockedHierarchyIds);
            CollectionAssert.AreEqual(new[] { "world.asset.tree" }, restored.selectedContentIds);
            CollectionAssert.AreEqual(new[] { "环境" }, restored.expandedContentCategoryPaths);
            Assert.AreEqual("large", restored.contentPresetSelections.Single().presetId);
        }

        [Test]
        public void LegacyLayoutResetsDirectlyToDocumentAndAuthoringModeSchema6()
        {
            var layout = new ESWorkbenchLayoutState
            {
                layoutSchemaVersion = 5,
                activeDocument = "page:terrain",
                activeLeftTab = "tools",
                bottomDrawerExpanded = true,
                selectedStableId = "world.region.market"
            };

            ESWorkbenchUIToolkitHost.ResetLayoutToSchema6ForTest(layout);

            Assert.AreEqual(6, layout.layoutSchemaVersion);
            Assert.AreEqual("authoring", layout.activeDocument);
            Assert.AreEqual("terrain", layout.activeAuthoringModeId);
            Assert.AreEqual("objects", layout.activeLeftTab);
            Assert.IsFalse(layout.bottomDrawerExpanded);
            Assert.AreEqual("world.region.market", layout.selectedStableId,
                "布局模式迁移不得破坏稳定选择身份。");
        }

        [Test]
        public void ResponsiveLayoutPolicyDefinesCommercialBreakpointsAndProtectsCenterRatio()
        {
            var policy = new ESWorkbenchResponsiveLayoutPolicy(
                minimumWindowWidth: 980f,
                minimumWindowHeight: 620f,
                minimumCenterWidth: 420f,
                minimumCenterHeight: 320f,
                maximumLeftPaneRatio: 0.26f,
                maximumInspectorPaneRatio: 0.32f,
                maximumBottomDrawerRatio: 0.36f,
                preferredLeftPaneWidth: 270f,
                maximumLeftPaneWidth: 390f,
                preferredInspectorPaneWidth: 340f,
                maximumInspectorPaneWidth: 480f,
                minimumBottomDrawerHeight: 150f,
                preferredBottomDrawerHeight: 230f,
                maximumBottomDrawerHeight: 340f);

            Assert.AreEqual(ESWorkbenchResponsiveTier.Wide, policy.ResolveTier(1280f));
            Assert.AreEqual(ESWorkbenchResponsiveTier.Compact, policy.ResolveTier(980f));
            Assert.AreEqual(ESWorkbenchResponsiveTier.Narrow, policy.ResolveTier(760f));
            Assert.GreaterOrEqual(policy.ResolveProtectedCenterWidth(980f), 980f * 0.58f);
            Assert.GreaterOrEqual(policy.ResolveProtectedCenterWidth(760f), 420f);
            Assert.AreEqual(8, policy.ResolveVisibleCommandCount(1280f));
            Assert.AreEqual(6, policy.ResolveVisibleCommandCount(1160f));
            Assert.AreEqual(4, policy.ResolveVisibleCommandCount(980f));
            Assert.AreEqual(2, policy.ResolveVisibleCommandCount(760f));
            Assert.LessOrEqual(policy.MaximumBottomDrawerRatio, 0.36f);
            Assert.AreEqual(280f, policy.PreferredLeftPaneWidth,
                "首选宽度不得低于商业侧栏最小宽度。 ");
            Assert.AreEqual(390f, policy.MaximumLeftPaneWidth);
            Assert.AreEqual(340f, policy.PreferredInspectorPaneWidth);
            Assert.AreEqual(480f, policy.MaximumInspectorPaneWidth);
            Assert.AreEqual(150f, policy.MinimumBottomDrawerHeight);
            Assert.AreEqual(32f, policy.CollapsedBottomDrawerHeight);
            Assert.AreEqual(96f, policy.CompactBottomDrawerHeight);
            Assert.AreEqual(230f, policy.PreferredBottomDrawerHeight);
            Assert.AreEqual(340f, policy.MaximumBottomDrawerHeight);
            Assert.GreaterOrEqual(
                policy.MinimumWindowWidth,
                policy.MinimumCenterWidth
                    + Mathf.Min(policy.MinimumLeftPaneWidth, policy.MinimumInspectorPaneWidth)
                    + 12f);
            Assert.GreaterOrEqual(
                policy.WideBreakpoint,
                policy.MinimumCenterWidth
                    + policy.MinimumLeftPaneWidth
                    + policy.MinimumInspectorPaneWidth
                    + 12f);
            Assert.GreaterOrEqual(
                policy.MinimumWindowHeight,
                policy.MinimumCenterHeight + policy.MinimumBottomDrawerHeight + 60f);

            Rect highDpiLogicalWorkspace = new Rect(0f, 0f, 720f, 381f);
            Vector2 adaptiveMinimum = policy.ResolveAdaptiveMinimum(highDpiLogicalWorkspace);
            Assert.LessOrEqual(adaptiveMinimum.x, highDpiLogicalWorkspace.width);
            Assert.LessOrEqual(adaptiveMinimum.y, highDpiLogicalWorkspace.height);
            Assert.GreaterOrEqual(adaptiveMinimum.x, 560f);
            Assert.GreaterOrEqual(adaptiveMinimum.y, 360f);
            Rect clamped = policy.ClampFloatingWindow(
                new Rect(-100f, -100f, 1440f, 900f),
                highDpiLogicalWorkspace);
            Assert.GreaterOrEqual(clamped.xMin, highDpiLogicalWorkspace.xMin);
            Assert.GreaterOrEqual(clamped.yMin, highDpiLogicalWorkspace.yMin);
            Assert.LessOrEqual(clamped.xMax, highDpiLogicalWorkspace.xMax);
            Assert.LessOrEqual(clamped.yMax, highDpiLogicalWorkspace.yMax);
            ESWorkbenchVisualValidationResult highDpiResult = policy.EvaluateVisualEnvironment(
                new ESWorkbenchVisualEnvironment(
                    adaptiveMinimum.x,
                    adaptiveMinimum.y,
                    policy.ResolveProtectedCenterWidth(adaptiveMinimum.x),
                    2f,
                    ESWorkbenchVisualTheme.Dark,
                    true));
            Assert.IsTrue(highDpiResult.WindowMinimumSatisfied,
                "200% DPI 下的可达窄屏尺寸必须按商业降级合同通过，而不是被理想尺寸误判失败。");

            float protectedCenter = policy.ResolveProtectedCenterWidth(1440f);
            ESWorkbenchVisualValidationResult undersized = policy.EvaluateVisualEnvironment(
                new ESWorkbenchVisualEnvironment(
                    1440f, 900f, 420f, 1f, ESWorkbenchVisualTheme.Dark));
            ESWorkbenchVisualValidationResult protectedResult = policy.EvaluateVisualEnvironment(
                new ESWorkbenchVisualEnvironment(
                    1440f, 900f, protectedCenter, 1f, ESWorkbenchVisualTheme.Dark));
            Assert.IsFalse(undersized.CenterProtected,
                "宽屏中央区不能只达到绝对最小宽度就通过比例保护。");
            Assert.IsTrue(protectedResult.CenterProtected);
        }

        [Test]
        public void VisualMatrixRequiresActualEnvironmentBeforeEvidenceCapture()
        {
            var policy = new ESWorkbenchResponsiveLayoutPolicy(
                minimumWindowWidth: 760f,
                minimumWindowHeight: 560f,
                minimumCenterWidth: 420f,
                minimumCenterHeight: 320f,
                maximumLeftPaneRatio: 0.26f,
                maximumInspectorPaneRatio: 0.32f,
                maximumBottomDrawerRatio: 0.36f,
                preferredLeftPaneWidth: 270f,
                maximumLeftPaneWidth: 390f,
                preferredInspectorPaneWidth: 340f,
                maximumInspectorPaneWidth: 480f,
                minimumBottomDrawerHeight: 150f,
                preferredBottomDrawerHeight: 230f,
                maximumBottomDrawerHeight: 340f);
            IReadOnlyList<ESWorkbenchVisualValidationScenario> scenarios =
                policy.CreateCommercialVisualMatrix();

            for (int i = 0; i < scenarios.Count; i++)
            {
                ESWorkbenchVisualValidationScenario scenario = scenarios[i];
                ESWorkbenchVisualScenarioMatch match = policy.EvaluateScenario(
                    new ESWorkbenchVisualEnvironment(
                        scenario.Width,
                        scenario.Height,
                        policy.ResolveProtectedCenterWidth(scenario.Width),
                        scenario.PixelsPerPoint,
                        scenario.Theme,
                        scenario.LongChineseContent),
                    scenario);
                Assert.IsTrue(match.Passed, scenario.ScenarioId + " 应能由精确环境匹配");
            }

            ESWorkbenchVisualValidationScenario first = scenarios[0];
            ESWorkbenchVisualScenarioMatch wrongTheme = policy.EvaluateScenario(
                new ESWorkbenchVisualEnvironment(
                    first.Width,
                    first.Height,
                    policy.ResolveProtectedCenterWidth(first.Width),
                    first.PixelsPerPoint,
                    first.Theme == ESWorkbenchVisualTheme.Dark
                        ? ESWorkbenchVisualTheme.Light : ESWorkbenchVisualTheme.Dark,
                    first.LongChineseContent),
                first);
            Assert.IsFalse(wrongTheme.Passed);
            StringAssert.Contains("场景不匹配", wrongTheme.Summary);
        }

        [Test]
        public void VisualEvidenceCaptureUsesPhysicalPixelsAtHighDpi()
        {
            Vector2Int normal = ESWorkbenchVisualEvidenceCapture.ResolveCapturePixelSize(
                new Rect(0f, 0f, 760f, 560f), 1f);
            Vector2Int highDpi = ESWorkbenchVisualEvidenceCapture.ResolveCapturePixelSize(
                new Rect(0f, 0f, 760f, 560f), 2f);
            Assert.AreEqual(new Vector2Int(760, 560), normal);
            Assert.AreEqual(new Vector2Int(1520, 1120), highDpi);

            var request = new ESWorkbenchVisualEvidenceCaptureRequest(
                "world",
                new ESWorkbenchVisualEnvironment(760f, 560f, 420f, 2f,
                    ESWorkbenchVisualTheme.Dark, true),
                ESWorkbenchResponsiveTier.Narrow,
                true,
                "场景匹配",
                "viewport",
                "world.game",
                "narrow-dark-200-long-cn",
                true,
                "场景匹配",
                CreatePassedInteractionChecks(),
                "Assets/World/Source.asset",
                "source-guid-a");
            Assert.AreEqual("narrow-dark-200-long-cn", request.ExpectedScenarioId);
            Assert.IsTrue(request.ScenarioMatch);
            Assert.IsTrue(request.InteractionMatrixPassed);
            Assert.AreEqual("Assets/World/Source.asset", request.SourceAssetPath);
            Assert.AreEqual("source-guid-a", request.SourceAssetGuid);
        }

        [Test]
        public void NativeCaptureGeometryUsesOwnedClientDpiAndRejectsForeignForegroundWindow()
        {
            Rect logical = new Rect(100f, 80f, 760f, 560f);
            RectInt nativeWindow = new RectInt(140, 100, 1160, 880);
            RectInt nativeClient = new RectInt(150, 120, 1140, 840);
            ESWorkbenchScreenCaptureGeometry trusted =
                ESWorkbenchVisualEvidenceCapture.ResolveCaptureGeometry(
                    logical, 2f, true, nativeWindow, nativeClient, true, 144);
            Assert.IsTrue(trusted.Trusted, trusted.Summary);
            Assert.AreEqual("WindowsClientRect", trusted.Source);
            Assert.AreEqual(nativeClient, trusted.CaptureRect);
            Assert.AreEqual(1.5f, trusted.NativeDpiScale, 0.001f);

            ESWorkbenchScreenCaptureGeometry foreign =
                ESWorkbenchVisualEvidenceCapture.ResolveCaptureGeometry(
                    logical, 2f, true, nativeWindow, nativeClient, false, 144);
            Assert.IsFalse(foreign.Trusted);
            Assert.AreEqual("ForeignForegroundWindow", foreign.Source);
        }

        [Test]
        public void NativeCaptureGeometryAcceptsContainedDockedEditorRectAndRejectsUnknownBoundary()
        {
            Rect logical = new Rect(100f, 80f, 760f, 560f);
            RectInt host = new RectInt(0, 0, 2560, 1440);
            ESWorkbenchScreenCaptureGeometry docked =
                ESWorkbenchVisualEvidenceCapture.ResolveCaptureGeometry(
                    logical, 1f, true, host, host, true, 144);
            Assert.IsTrue(docked.Trusted, docked.Summary);
            Assert.AreEqual("LogicalRectNativeDpi", docked.Source);
            Assert.AreEqual(new RectInt(150, 120, 1140, 840), docked.CaptureRect);

            ESWorkbenchScreenCaptureGeometry outside =
                ESWorkbenchVisualEvidenceCapture.ResolveCaptureGeometry(
                    new Rect(4000f, 4000f, 760f, 560f), 1f,
                    true, host, host, true, 144);
            Assert.IsFalse(outside.Trusted);
            Assert.AreEqual("UnmatchedNativeBoundary", outside.Source);
        }

        [Test]
        public void VisualPixelVarianceRejectsBlankCaptureAndAcceptsStyledSurface()
        {
            Color[] blank = Enumerable.Repeat(new Color(0.1f, 0.1f, 0.1f), 256).ToArray();
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasUsablePixelVariance(blank, out _));

            Color[] styled = new Color[256];
            for (int i = 0; i < styled.Length; i++)
            {
                float value = (i % 32) / 31f;
                styled[i] = new Color(value, (i % 17) / 16f, (i % 11) / 10f);
            }
            Assert.IsTrue(ESWorkbenchVisualEvidenceCapture.HasUsablePixelVariance(
                styled, out string summary), summary);
        }

        [Test]
        public void VisualEvidenceIndexKeepsOneLatestEntryPerMatchedScenario()
        {
            var index = new ESWorkbenchVisualEvidenceIndex();
            var first = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/SourceA.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "run-a",
                capturedUtc = "2026-08-16T01:00:00.0000000Z",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = CreatePassedInteractionChecks(),
                screenshotAbsolutePath = @"C:\Evidence\run-a\window.png",
                manifestAbsolutePath = @"C:\Evidence\run-a\manifest.json"
            };
            ApplyValidVisualCaptureGates(first);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, first);

            var replacement = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/SourceA.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "run-b",
                capturedUtc = "2026-08-16T02:00:00.0000000Z",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = CreatePassedInteractionChecks(),
                screenshotAbsolutePath = @"C:\Evidence\run-b\window.png",
                manifestAbsolutePath = @"C:\Evidence\run-b\manifest.json"
            };
            ApplyValidVisualCaptureGates(replacement);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, replacement);

            Assert.AreEqual(1, index.entries.Count);
            Assert.AreEqual("run-b", index.entries[0].runId);
            Assert.AreEqual("wide-dark-100", index.entries[0].scenarioId);

            var otherSource = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/SourceB.asset",
                sourceAssetGuid = "source-guid-b",
                runId = "run-source-b",
                capturedUtc = "2026-08-16T03:00:00.0000000Z",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = CreatePassedInteractionChecks(),
                screenshotAbsolutePath = @"C:\Evidence\run-source-b\window.png",
                manifestAbsolutePath = @"C:\Evidence\run-source-b\manifest.json"
            };
            ApplyValidVisualCaptureGates(otherSource);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, otherSource);
            Assert.AreEqual(2, index.entries.Count,
                "相同场景的不同 Source 必须保留独立证据。");
            Assert.AreEqual(1, index.entries.Count(value => value.sourceAssetGuid == "source-guid-a"));
            Assert.AreEqual(1, index.entries.Count(value => value.sourceAssetGuid == "source-guid-b"));

            var mismatch = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/SourceA.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "run-invalid",
                scenarioId = "compact-light-150-long-cn",
                expectedScenarioId = "narrow-light-200",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = CreatePassedInteractionChecks()
            };
            ApplyValidVisualCaptureGates(mismatch);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, mismatch);
            Assert.AreEqual(2, index.entries.Count);
        }

        [Test]
        public void VisualEvidenceIdentityRejectsOldSchemaUnityOrAssemblyAndRequiresSource()
        {
            ESWorkbenchVisualEvidenceManifest CreateCurrent()
            {
                return new ESWorkbenchVisualEvidenceManifest
                {
                    workbenchId = "world",
                    unityVersion = Application.unityVersion,
                    assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                    sourceAssetPath = "Assets/World/Source.asset",
                    sourceAssetGuid = "source-guid-a"
                };
            }

            ESWorkbenchVisualEvidenceManifest current = CreateCurrent();
            Assert.IsTrue(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(
                current, "world", "source-guid-a"));

            ESWorkbenchVisualEvidenceManifest oldSchema = CreateCurrent();
            oldSchema.schemaVersion = 3;
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(oldSchema));

            ESWorkbenchVisualEvidenceManifest oldUnity = CreateCurrent();
            oldUnity.unityVersion = "older-unity";
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(oldUnity));

            ESWorkbenchVisualEvidenceManifest oldAssembly = CreateCurrent();
            oldAssembly.assemblyModuleVersionId = Guid.Empty.ToString("D");
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(oldAssembly));

            ESWorkbenchVisualEvidenceManifest missingSource = CreateCurrent();
            missingSource.sourceAssetGuid = string.Empty;
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(missingSource));
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.HasCurrentArtifactIdentity(
                current, "world", "source-guid-b"));
        }

        [Test]
        public void VisualEvidenceIndexRejectsIncompleteInteractionMatrixEvenWhenFlagClaimsSuccess()
        {
            var index = new ESWorkbenchVisualEvidenceIndex();
            var manifest = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/Source.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "static-only",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = new List<ESWorkbenchVisualInteractionCheck>
                {
                    new ESWorkbenchVisualInteractionCheck
                    {
                        checkId = "window-open-focus",
                        title = "窗口打开",
                        expected = "窗口正常显示",
                        passed = true
                    }
                },
                screenshotAbsolutePath = @"C:\Evidence\static-only\window.png",
                manifestAbsolutePath = @"C:\Evidence\static-only\manifest.json"
            };
            ApplyValidVisualCaptureGates(manifest);

            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, manifest);

            Assert.IsEmpty(index.entries, "不完整清单不能通过布尔标记冒充完整交互矩阵");
        }

        [Test]
        public void VisualInteractionMatrixRequiresObservedEventsAndDistinctTargets()
        {
            var observations = new Dictionary<string, ESWorkbenchVisualInteractionObservation>(StringComparer.Ordinal);
            void Record(string id, params string[] targets)
            {
                var observation = new ESWorkbenchVisualInteractionObservation();
                for (int i = 0; i < targets.Length; i++)
                    observation.Record("ui-event/test-observation", targets[i]);
                observations[id] = observation;
            }

            Record("window-open-focus", "pointer-focus");
            Record("pane-collapse-restore", "hidden", "visible");
            Record("pane-resize", "310-to-352");
            Record("viewport-switch", "world.canvas-2d", "world.scene-3d");
            Record("viewport-input", "pointer", "wheel");
            Record("bottom-channel-switch", "problems", "tasks");
            Record("command-overflow", "world.validate");
            IReadOnlyList<ESWorkbenchVisualInteractionCheck> incomplete =
                ESWorkbenchVisualEvidenceCapture.CreateObservedInteractionChecklist(observations);
            Assert.IsFalse(ESWorkbenchVisualEvidenceCapture.IsCommercialInteractionMatrixComplete(incomplete));

            observations["viewport-switch"].Record("ui-event/test-observation", "world.game");
            IReadOnlyList<ESWorkbenchVisualInteractionCheck> complete =
                ESWorkbenchVisualEvidenceCapture.CreateObservedInteractionChecklist(observations);
            Assert.IsTrue(ESWorkbenchVisualEvidenceCapture.IsCommercialInteractionMatrixComplete(complete));
            Assert.AreEqual(3, complete.First(value => value.checkId == "viewport-switch").observationCount);
            Assert.AreEqual("ui-event/test-observation", complete.First(value => value.checkId == "viewport-switch").evidenceSource);
        }

        [Test]
        public void VisualEvidenceIndexRejectsManualCheckboxEvidenceEvenWhenCountsPass()
        {
            List<ESWorkbenchVisualInteractionCheck> checks = CreatePassedInteractionChecks();
            for (int i = 0; i < checks.Count; i++) checks[i].evidenceSource = "manual-checkbox";
            var index = new ESWorkbenchVisualEvidenceIndex();
            var manifest = new ESWorkbenchVisualEvidenceManifest
            {
                schemaVersion = 5,
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/Source.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "manual-checkbox",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = checks,
                screenshotAbsolutePath = @"C:\Evidence\manual-checkbox\window.png",
                manifestAbsolutePath = @"C:\Evidence\manual-checkbox\manifest.json"
            };
            ApplyValidVisualCaptureGates(manifest);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, manifest);

            Assert.IsEmpty(index.entries, "手工勾选不能冒充真实 UI 事件证据");
        }

        private static List<ESWorkbenchVisualInteractionCheck> CreatePassedInteractionChecks()
        {
            return ESWorkbenchVisualEvidenceCapture.CreateCommercialInteractionChecklist(new[]
                {
                    "window-open-focus",
                    "pane-collapse-restore",
                    "pane-resize",
                    "viewport-switch",
                    "viewport-input",
                    "bottom-channel-switch",
                    "command-overflow"
                })
                .Select(value => value.Clone())
                .ToList();
        }

        private static void ApplyValidVisualCaptureGates(ESWorkbenchVisualEvidenceManifest manifest)
        {
            manifest.captureGeometryTrusted = true;
            manifest.layoutProbePassed = true;
            manifest.layoutProbes = Enumerable.Range(0, 9)
                .Select(index => new ESWorkbenchVisualLayoutProbe
                {
                    probeId = "probe-" + index,
                    title = "布局探针 " + index,
                    passed = true,
                    diagnostic = "通过"
                })
                .ToList();
            manifest.pixelVariancePassed = true;
            manifest.pixelVarianceSummary = "测试像素差异通过";
        }

        [Test]
        public void VisualEvidenceIndexRejectsUntrustedGeometryAndIncompleteLayoutProbe()
        {
            var manifest = new ESWorkbenchVisualEvidenceManifest
            {
                workbenchId = "world",
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = ESWorkbenchVisualEvidenceCapture.CurrentAssemblyModuleVersionId,
                sourceAssetPath = "Assets/World/Source.asset",
                sourceAssetGuid = "source-guid-a",
                runId = "capture-gates",
                scenarioId = "wide-dark-100",
                expectedScenarioId = "wide-dark-100",
                scenarioMatch = true,
                layoutContractPassed = true,
                interactionMatrixPassed = true,
                interactionChecks = CreatePassedInteractionChecks(),
                screenshotAbsolutePath = @"C:\Evidence\capture-gates\window.png",
                manifestAbsolutePath = @"C:\Evidence\capture-gates\manifest.json"
            };

            var index = new ESWorkbenchVisualEvidenceIndex();
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, manifest);
            Assert.IsEmpty(index.entries, "未核对原生边界的截图不能进入商业矩阵");

            ApplyValidVisualCaptureGates(manifest);
            manifest.layoutProbes.RemoveAt(manifest.layoutProbes.Count - 1);
            ESWorkbenchVisualEvidenceCapture.MergeIndex(index, manifest);
            Assert.IsEmpty(index.entries, "缺少关键布局探针的截图不能进入商业矩阵");
        }

        [Test]
        public void CompactHostPreservesActionsThroughCommercialOverflowMenus()
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
            ESWorkbenchDocumentDefinition[] documents =
            {
                new ESWorkbenchDocumentDefinition(
                    "authoring", "世界创作", "持久作者视口", true, ESWorkbenchDirtyFlags.Authoring),
                new ESWorkbenchDocumentDefinition(
                    "overview", "世界总览", "响应式文档", false, ESWorkbenchDirtyFlags.Authoring, () => { }),
                new ESWorkbenchDocumentDefinition(
                    "production", "生产与发布", "响应式文档", false, ESWorkbenchDirtyFlags.Build, () => { })
            };
            ESWorkbenchAuthoringModeDefinition[] modes = Enumerable.Range(0, 10)
                .Select(index => new ESWorkbenchAuthoringModeDefinition(
                    "mode-" + index, "作者模式 " + index, "响应式作者模式",
                    priority: index, primary: index < 4))
                .ToArray();
            ESWorkbenchCommandDescriptor[] commands =
            {
                new ESWorkbenchCommandDescriptor(
                    "core.save", "保存", _ => { }, priority: 10,
                    role: ESWorkbenchCommandRole.Primary,
                    visibility: ESWorkbenchCommandVisibility.Pinned),
                new ESWorkbenchCommandDescriptor(
                    "core.undo", "撤销", _ => { }, priority: 20,
                    role: ESWorkbenchCommandRole.History,
                    visibility: ESWorkbenchCommandVisibility.Pinned),
                new ESWorkbenchCommandDescriptor(
                    "core.redo", "重做", _ => { }, priority: 10,
                    role: ESWorkbenchCommandRole.History,
                    visibility: ESWorkbenchCommandVisibility.Pinned),
                new ESWorkbenchCommandDescriptor(
                    "world.validate", "验证", _ => { }, priority: 10,
                    role: ESWorkbenchCommandRole.Validation,
                    visibility: ESWorkbenchCommandVisibility.Pinned),
                new ESWorkbenchCommandDescriptor(
                    "world.brush", "笔刷", _ => { }, priority: 100,
                    role: ESWorkbenchCommandRole.Authoring),
                new ESWorkbenchCommandDescriptor(
                    "world.build", "构建", _ => { }, priority: 100,
                    role: ESWorkbenchCommandRole.Build),
                new ESWorkbenchCommandDescriptor(
                    "world.reload-source", "重载正式资产", _ => { }, priority: 1000,
                    role: ESWorkbenchCommandRole.Dangerous),
                new ESWorkbenchCommandDescriptor(
                    "core.refresh", "刷新", _ => { }, priority: 1000,
                    role: ESWorkbenchCommandRole.Utility)
            };
            ESWorkbenchBottomPanelDescriptor[] panels = Enumerable.Range(0, 6)
                .Select(index => new ESWorkbenchBottomPanelDescriptor(
                    "panel-" + index,
                    "业务通道 " + index,
                    _ => new ESWorkbenchBottomPanelContent(new VisualElement()),
                    priority: 100 - index))
                .ToArray();
            var layout = new ESWorkbenchLayoutState
            {
                activeDocument = "authoring",
                activeAuthoringModeId = "mode-9",
                activeBottomTab = "panel-5",
                compactSidePane = string.Empty,
                responsiveLayoutInitialized = true
            };
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "responsive-tests",
                "ES 响应式测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => documents,
                () => modes,
                () => new[]
                {
                    new ESWorkbenchViewportDescriptor(
                        "status", "状态视口", ESWorkbenchViewportKind.Custom,
                        _ => new StubStatusViewport())
                },
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => commands,
                layout,
                document => new Label(document.title),
                _ => { },
                () => "authoring",
                getBottomPanels: () => panels);
            try
            {
                VisualElement root = host.Build();
                Assert.IsNotNull(root.Q<TwoPaneSplitView>("ESWorkbenchOuterSplit"));
                Assert.IsNotNull(root.Q<TwoPaneSplitView>("ESWorkbenchContentSplit"));
                Assert.IsNotNull(root.Q<TwoPaneSplitView>("ESWorkbenchWorkspaceSplit"));
                Assert.AreSame(root.Q<TwoPaneSplitView>("ESWorkbenchOuterSplit"),
                    root.Q<VisualElement>("ESWorkbenchLeftPanel").parent,
                    "左栏必须独占全高，不能被底部抽屉压缩。");
                Assert.AreSame(root.Q<TwoPaneSplitView>("ESWorkbenchWorkspaceSplit"),
                    root.Q<VisualElement>("ESWorkbenchBottomDrawer").parent,
                    "底部抽屉只能位于中央与 Inspector 的右工作区下方。");
                host.SetBottomDrawerExpandedForTest(false);
                Assert.AreEqual(32f, host.AppliedBottomDrawerHeightForTest);
                Assert.IsFalse(host.BottomContentVisibleForTest,
                    "底部收起后必须保留页签条并隐藏内容，而不是折叠整个通道。");
                IESWorkbenchViewport persistentViewport = host.ActiveViewportForTest;
                actions.Selection.Select(new ESWorkbenchSelection("stable.selection", "world.test", null, null));
                ESWorkbenchViewportLayoutState viewportState = layout.GetOrCreateViewportState("status");
                viewportState.pan = new Vector2(37f, 19f);
                host.SelectAuthoringMode("mode-8");
                Assert.AreSame(persistentViewport, host.ActiveViewportForTest,
                    "作者模式切换不得销毁或替换持久视口实例。");
                Assert.AreEqual("stable.selection", actions.Selection.Current.StableId);
                Assert.AreEqual(new Vector2(37f, 19f), viewportState.pan,
                    "作者模式切换必须保留相机/画布布局状态。");
                Assert.AreEqual("authoring", host.ActiveDocumentIdForTest);
                host.ApplyResponsiveLayoutForTest(980f, 570f, 620f);

                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_core_save"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_core_undo"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_core_redo"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_world_validate"));
                Assert.IsNull(root.Q<Button>("ESWorkbenchCommand_world_reload-source"));
                ObjectField compactAssetField = root.Q<ObjectField>("ESWorkbenchAssetField");
                Label compactDocumentStatus = root.Q<Label>("ESWorkbenchDocumentStatus");
                Assert.AreEqual(DisplayStyle.None,
                    compactAssetField.labelElement.style.display.value);
                Assert.LessOrEqual(compactAssetField.style.maxWidth.value.value, 144f);
                Assert.LessOrEqual(compactDocumentStatus.style.maxWidth.value.value, 28f);

                host.ApplyResponsiveLayoutForTest(760f, 420f, 600f);

                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_core_save"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchCommand_core_undo"));
                Assert.IsNull(root.Q<Button>("ESWorkbenchCommand_core_redo"));
                Assert.IsNull(root.Q<Button>("ESWorkbenchCommand_world_validate"));
                ObjectField narrowAssetField = root.Q<ObjectField>("ESWorkbenchAssetField");
                Label narrowDocumentStatus = root.Q<Label>("ESWorkbenchDocumentStatus");
                Assert.IsNotNull(narrowAssetField);
                Assert.IsNotNull(narrowDocumentStatus);
                Assert.LessOrEqual(narrowAssetField.style.maxWidth.value.value, 144f);
                Assert.LessOrEqual(narrowDocumentStatus.style.maxWidth.value.value, 28f);
                Vector2 expectedMinimum = new ESWorkbenchResponsiveLayoutPolicy()
                    .ResolveAdaptiveMinimum(EditorGUIUtility.GetMainWindowPosition());
                Assert.AreEqual(expectedMinimum, window.minSize);
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchCommandIdentityRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchCommandActionRow"));
                Assert.AreEqual("内容", root.Q<Button>("ESWorkbenchToggleLeftPane").text);
                Assert.AreEqual("检查", root.Q<Button>("ESWorkbenchToggleInspectorPane").text);
                Assert.AreEqual("任务", root.Q<Button>("ESWorkbenchToggleBottomDrawer").text);
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchCommandOverflow"));
                int visibleDocumentCount = 0;
                root.Q<VisualElement>("ESWorkbenchDocumentTabs")
                    .Query<ToolbarToggle>()
                    .ForEach(_ => visibleDocumentCount++);
                Assert.IsTrue(
                    visibleDocumentCount == documents.Length
                    || root.Q<ToolbarMenu>("ESWorkbenchDocumentOverflow") != null,
                    "文档必须全部可见，或通过溢出菜单保留完整访问入口。 ");
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchBottomOverflow"));
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchViewportStatusOverflow"));
                bool activeDocumentVisible = false;
                root.Q<VisualElement>("ESWorkbenchDocumentTabs")
                    .Query<ToolbarToggle>()
                    .ForEach(toggle => activeDocumentVisible |= (string)toggle.userData == "authoring");
                Assert.IsTrue(activeDocumentVisible);
                bool activeModeVisible = false;
                root.Q<VisualElement>("ESWorkbenchViewportModes")
                    .Query<ToolbarToggle>()
                    .ForEach(toggle => activeModeVisible |= (string)toggle.userData == "mode:mode-8");
                Assert.IsTrue(activeModeVisible, "活动作者模式必须在任何响应式宽度下保持可见。");

                host.ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset.Focus);
                Assert.AreEqual(ESWorkbenchLayoutPreset.Focus, layout.layoutPreset);
                Assert.IsFalse(layout.leftPaneVisible);
                Assert.IsFalse(layout.inspectorPaneVisible);
                Assert.IsFalse(layout.bottomDrawerExpanded);

                host.ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset.Content);
                Assert.AreEqual("left", layout.compactSidePane);
                Assert.IsTrue(layout.leftPaneVisible);
                Assert.IsFalse(layout.inspectorPaneVisible);

                host.ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset.Diagnostics);
                Assert.AreEqual("inspector", layout.compactSidePane);
                Assert.IsFalse(layout.leftPaneVisible);
                Assert.IsTrue(layout.inspectorPaneVisible);
                Assert.IsTrue(layout.bottomDrawerExpanded);

                host.ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset.Authoring);
                Assert.AreEqual(320f, layout.leftPaneWidth);
                Assert.AreEqual(320f, layout.inspectorPaneWidth);
                Assert.AreEqual(220f, layout.bottomDrawerHeight);
                Assert.IsTrue(layout.leftPaneVisible);
                Assert.IsTrue(layout.inspectorPaneVisible);
                Assert.IsTrue(layout.bottomDrawerExpanded);

                host.CommitPaneResizeForTest(310f, 352f);
                Assert.AreEqual(ESWorkbenchLayoutPreset.Custom, layout.layoutPreset);
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HostActivatesModeOnceAndSuspendsPersistentViewportOutsideAuthoringDocument()
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
            int firstActivations = 0;
            int firstDeactivations = 0;
            int secondActivations = 0;
            var viewport = new StubViewport();
            var layout = new ESWorkbenchLayoutState
            {
                activeDocument = "authoring",
                activeAuthoringModeId = "first",
                responsiveLayoutInitialized = true
            };
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "mode-lifecycle-tests",
                "ES 模式生命周期测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => new[]
                {
                    new ESWorkbenchDocumentDefinition(
                        "authoring", "世界创作", "持久作者视口", true, ESWorkbenchDirtyFlags.Authoring),
                    new ESWorkbenchDocumentDefinition(
                        "overview", "世界总览", "普通文档", false, ESWorkbenchDirtyFlags.Authoring, () => { })
                },
                () => new[]
                {
                    new ESWorkbenchAuthoringModeDefinition(
                        "first", "第一模式", "第一模式", priority: 20,
                        activate: _ => firstActivations++,
                        deactivate: _ => firstDeactivations++),
                    new ESWorkbenchAuthoringModeDefinition(
                        "second", "第二模式", "第二模式", priority: 10,
                        activate: _ => secondActivations++)
                },
                () => new[]
                {
                    new ESWorkbenchViewportDescriptor(
                        "persistent", "持久视口", ESWorkbenchViewportKind.Custom, _ => viewport)
                },
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => Array.Empty<ESWorkbenchCommandDescriptor>(),
                layout,
                document => new Label(document.title),
                _ => { },
                () => layout.activeDocument);
            try
            {
                host.Build();
                Assert.AreEqual(1, firstActivations, "初始作者模式必须在 Host 建立后激活一次。");
                Assert.AreEqual(1, viewport.ActivateCount);

                host.RefreshRegistrations();
                Assert.AreEqual(1, firstActivations, "普通注册刷新不得重复激活当前模式。");

                host.SelectAuthoringMode("second");
                Assert.AreEqual(1, firstDeactivations);
                Assert.AreEqual(1, secondActivations);
                Assert.AreSame(viewport, host.ActiveViewportForTest);

                host.ShowDocumentForTest("overview");
                Assert.AreEqual(1, viewport.DeactivateCount);
                Assert.IsFalse(viewport.Disposed, "离开创作文档只能停用持久视口，不能销毁实例。");

                host.ShowDocumentForTest("authoring");
                Assert.AreEqual(2, viewport.ActivateCount);
                Assert.AreSame(viewport, host.ActiveViewportForTest);
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HostBuildExposesAuthoringRailAndProductionDrawer()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var content = new[]
            {
                new ESWorkbenchObjectDescriptor(
                    "prefab", "预制件", "建筑", null,
                    contentKind: ESWorkbenchContentKind.Prefab),
                new ESWorkbenchObjectDescriptor(
                    "brush", "笔刷", "塑形", null,
                    contentKind: ESWorkbenchContentKind.Brush,
                    dragMode: ESWorkbenchContentDragMode.ActivateTool),
                new ESWorkbenchObjectDescriptor(
                    "brush", "重复笔刷", "塑形/重复", null,
                    contentKind: ESWorkbenchContentKind.Brush,
                    dragMode: ESWorkbenchContentDragMode.ActivateTool)
            };
            var actions = new ESWorkbenchActionContext(
                window,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            int objectSourceReads = 0;
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "ES 底座测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
                () => Array.Empty<ESWorkbenchViewportDescriptor>(),
                () =>
                {
                    objectSourceReads++;
                    return content;
                },
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

                Assert.AreEqual(1, objectSourceReads,
                    "一次内容列表重建只能读取一次业务内容源，菜单、分类和批选复用同一快照。");
                Assert.AreEqual(2, host.ContentSourceSnapshotCountForTest,
                    "重复稳定 ID 必须在内容源快照入口去重，防止选择、批选和拖放解析分叉。");
                Assert.AreEqual(1, host.DuplicateContentIdCountForTest);

                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchToolRail"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchBottomDrawer"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchBottomContent"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchDropFeedback"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentLibraryHeader"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentKindQuickBar"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchContentKindShortcut_all"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchContentKindShortcut_Brush"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentKindRail"));
                Assert.IsNotNull(root.Q<ListView>("ESWorkbenchContentCategoryTree"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentBreadcrumbBar"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentScopeBar"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentModeBar"));
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter"));
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchContentViewMenu"));
                Assert.IsNotNull(root.Q<ToolbarMenu>("ESWorkbenchContentBatchMenu"));
                Assert.IsNotNull(root.Q<ListView>("ESWorkbenchContentGrid"));
                Assert.IsNotNull(root.Q<Image>("DropPreview"));
                Assert.IsNotNull(root.Q<Label>("DropStatus"));
                Assert.IsNotNull(root.Q<Label>("DropCount"));
                Assert.IsNotNull(root.Q<Label>("DropDetail"));
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentResults"));
                bool hasContentLibraryTab = false;
                bool hasCurrentStructureTab = false;
                int leftTabCount = 0;
                root.Q<VisualElement>("ESWorkbenchLeftTabs").Query<ToolbarToggle>().ForEach(toggle =>
                {
                    leftTabCount++;
                    hasContentLibraryTab |= toggle.text == "内容库";
                    hasCurrentStructureTab |= toggle.text == "当前结构";
                });
                Assert.IsTrue(hasContentLibraryTab);
                Assert.IsTrue(hasCurrentStructureTab);
                Assert.AreEqual(2, leftTabCount, "左侧核心内容中心只允许内容库与当前结构两个职责页签。");
                Assert.AreEqual(ESWorkbenchContentSortMode.Type, host.ActiveContentSortModeForTest,
                    "内容库默认应按类型排序，而不是无结构混排。 ");
                Assert.AreEqual("按类型", root.Q<ToolbarMenu>("ESWorkbenchContentSortMenu").text);
                Assert.IsFalse(host.ContentCategoryRootHasFoldForTest(),
                    "全部内容是聚合入口，不应显示无效折叠箭头。");

                Texture2D semanticThumbnail = host.ResolveSemanticContentThumbnailForTest(
                    ESWorkbenchContentKind.Brush);
                Assert.IsNotNull(semanticThumbnail);
                Assert.AreEqual(192, semanticThumbnail.width);
                Assert.AreEqual(128, semanticThumbnail.height);
                Assert.AreEqual(HideFlags.HideAndDontSave, semanticThumbnail.hideFlags);

                var lowBrush = new ESWorkbenchObjectDescriptor(
                    "world.brush.low", "低地形笔刷", "地形塑形", null,
                    contentKind: ESWorkbenchContentKind.Brush);
                var highBrush = new ESWorkbenchObjectDescriptor(
                    "world.brush.high", "高地形笔刷", "地形塑形", null,
                    contentKind: ESWorkbenchContentKind.Brush);
                Texture2D lowThumbnail = host.ResolveContentThumbnailForTest(lowBrush) as Texture2D;
                Texture2D highThumbnail = host.ResolveContentThumbnailForTest(highBrush) as Texture2D;
                Assert.IsNotNull(lowThumbnail);
                Assert.IsNotNull(highThumbnail);
                Assert.AreNotSame(lowThumbnail, highThumbnail);
                Assert.AreNotEqual(
                    host.ResolveGeneratedThumbnailFingerprintForTest(lowBrush),
                    host.ResolveGeneratedThumbnailFingerprintForTest(highBrush),
                    "同类模板必须保持统一预览背景，同时凭稳定身份生成可辨识的专属缩略图。");
                for (int i = 0; i < 260; i++)
                {
                    host.ResolveContentThumbnailForTest(new ESWorkbenchObjectDescriptor(
                        "world.thumbnail.capacity." + i,
                        "容量验证 " + i,
                        "缩略图容量",
                        null,
                        contentKind: i % 2 == 0
                            ? ESWorkbenchContentKind.Brush
                            : ESWorkbenchContentKind.RegionTemplate));
                }
                Assert.AreEqual(256, host.GeneratedThumbnailCacheCountForTest,
                    "专属生成缩略图必须使用有界 LRU，不能随浏览过的内容数量无界增长。");

                host.ApplyContentBrowserResponsiveForTest(250f, 160f);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentKindQuickBar").style.display.value,
                    "最窄内容栏也必须保留一级类型入口。 ");
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<VisualElement>("ESWorkbenchContentKindRail").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<Button>("ESWorkbenchContentListMode").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<ToolbarMenu>("ESWorkbenchContentViewMenu").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<Button>("ESWorkbenchContentBatchPlace").style.display.value);

                host.ApplyContentBrowserResponsiveForTest(360f, 230f);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentKindQuickBar").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentKindRail").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<Button>("ESWorkbenchContentListMode").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<ToolbarMenu>("ESWorkbenchContentViewMenu").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<Button>("ESWorkbenchContentBatchPlace").style.display.value);

                host.SetContentViewModeForTest(ESWorkbenchContentViewMode.Grid);
                host.ApplyContentBrowserResponsiveForTest(360f, 340f);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentKindQuickBar").style.display.value,
                    "大图模式不得再把类型入口收进筛选菜单。 ");
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<VisualElement>("ESWorkbenchContentKindRail").style.display.value,
                    "大图模式只收起业务分类轨，为双列缩略图释放空间。 ");
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter").style.display.value);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<ListView>("ESWorkbenchContentList").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<ListView>("ESWorkbenchContentGrid").style.display.value);

                host.SetContentViewModeForTest(ESWorkbenchContentViewMode.List);
                host.ApplyContentBrowserResponsiveForTest(360f, 230f);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentKindRail").style.display.value);

                host.SetContentKindForTest("Brush");
                Assert.AreEqual("Brush", host.ActiveContentKindForTest);
                Assert.AreEqual(1, host.VisibleContentCountForTest,
                    "一级类型按钮必须直接完成筛选，不需要打开组合筛选菜单。 ");
                Assert.AreEqual("笔刷 1", root.Q<Button>("ESWorkbenchContentKindShortcut_Brush").text);
                host.SetContentKindForTest("all");

                host.ApplyResponsiveLayoutForTest(1400f, 800f, 700f);
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<Label>("ESWorkbenchContentLibraryDescription").style.display.value,
                    "低高度窗口必须压缩内容中心说明，为真实内容卡片保留垂直空间。");
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<VisualElement>("ESWorkbenchContentScopeBar").style.display.value,
                    "低高度窗口应把范围筛选收进组合筛选菜单，而不是挤压内容区。");
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<VisualElement>("ESWorkbenchLeftPanelTitle").style.display.value,
                    "低高度窗口应移除与内容库页签重复的左栏总标题。");
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter").style.display.value);
                Assert.AreEqual(160f, root.Q<ListView>("ESWorkbenchContentGrid").fixedItemHeight);

                host.ApplyResponsiveLayoutForTest(1400f, 800f, 900f);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<Label>("ESWorkbenchContentLibraryDescription").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchContentScopeBar").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex,
                    root.Q<VisualElement>("ESWorkbenchLeftPanelTitle").style.display.value);
                Assert.AreEqual(184f, root.Q<ListView>("ESWorkbenchContentGrid").fixedItemHeight);

                host.ApplyResponsiveLayoutForTest(1400f, 800f, 700f);
                host.ShowLeftTabForTest("hierarchy");
                host.ShowLeftTabForTest("objects");
                Assert.AreEqual(DisplayStyle.None,
                    root.Q<VisualElement>("ESWorkbenchContentScopeBar").style.display.value);
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchContentKindQuickBar"),
                    "内容页离开再返回后必须重新创建一级类型入口。 ");
                ToolbarMenu rebuiltCompactFilter = root.Q<ToolbarMenu>("ESWorkbenchCompactContentFilter");
                Assert.AreEqual(DisplayStyle.Flex, rebuiltCompactFilter.style.display.value);
                Assert.Greater(rebuiltCompactFilter.menu.MenuItems().Count, 0,
                    "内容页重建后必须对新 UI 元素重新应用响应式状态并恢复组合筛选菜单。");
                Assert.AreEqual(1, objectSourceReads,
                    "视图和响应式切换不得再次调用业务内容源。");
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EmptyBottomPanelCompactsAndManualHeightRemainsAuthoritative()
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
            var panels = new[]
            {
                new ESWorkbenchBottomPanelDescriptor(
                    "density.empty",
                    "空通道",
                    _ => new ESWorkbenchBottomPanelContent(
                        new VisualElement(),
                        ESWorkbenchBottomPanelDensity.Empty),
                    priority: 2000),
                new ESWorkbenchBottomPanelDescriptor(
                    "density.normal",
                    "常规通道",
                    _ => new ESWorkbenchBottomPanelContent(
                        new VisualElement(),
                        ESWorkbenchBottomPanelDensity.Normal),
                    priority: 1990)
            };
            var layout = new ESWorkbenchLayoutState
            {
                activeBottomTab = "density.empty",
                bottomDrawerExpanded = true,
                bottomDrawerHeight = 210f,
                bottomDrawerUserSized = false
            };
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "bottom-density-tests",
                "ES 底部密度测试",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
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
                getBottomPanels: () => panels);
            try
            {
                host.Build();
                host.ApplyResponsiveLayoutForTest(1400f, 800f, 900f);

                Assert.AreEqual(ESWorkbenchBottomPanelDensity.Empty, host.ActiveBottomPanelDensityForTest);
                Assert.AreEqual(96f, host.AppliedBottomDrawerHeightForTest);
                Assert.AreEqual(210f, layout.bottomDrawerHeight,
                    "自动紧凑只能改变当前显示高度，不能覆盖用户可恢复高度。 ");

                host.ShowBottomTabForTest("density.normal");
                Assert.AreEqual(ESWorkbenchBottomPanelDensity.Normal, host.ActiveBottomPanelDensityForTest);
                Assert.AreEqual(210f, host.AppliedBottomDrawerHeightForTest);

                host.CommitBottomPaneResizeForTest(210f, 268f);
                Assert.IsTrue(layout.bottomDrawerUserSized);
                Assert.AreEqual(268f, layout.bottomDrawerHeight);
                host.ShowBottomTabForTest("density.empty");
                Assert.AreEqual(268f, host.AppliedBottomDrawerHeightForTest,
                    "用户明确调整后的高度不得被空状态自动压缩覆盖。 ");

                host.ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset.Authoring);
                Assert.IsFalse(layout.bottomDrawerUserSized);
                Assert.AreEqual(96f, host.AppliedBottomDrawerHeightForTest,
                    "恢复标准布局后，空通道应重新进入自动紧凑状态。 ");
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
                "测试检查器",
                leftPanelTitle: "测试内容与层级",
                workspaceTitle: "测试作者场景");
            var actionStack = new VisualElement { name = "TestFoundationActionStack" };
            var systemRow = new VisualElement { name = "TestSystemRow" };
            var systemHost = new VisualElement { name = "TestSystemHost" };
            var systemButton = new Button { name = "TestSystemAction", text = "休眠" };
            systemHost.Add(systemButton);
            systemRow.Add(systemHost);
            actionStack.Add(systemRow);
            var globalRow = new VisualElement { name = "TestGlobalRow" };
            var globalHost = new VisualElement { name = "TestGlobalHost" };
            globalRow.Add(globalHost);
            actionStack.Add(globalRow);
            var windowRow = new VisualElement { name = "TestWindowRow" };
            var windowHost = new VisualElement { name = "TestWindowHost" };
            windowRow.Add(windowHost);
            actionStack.Add(windowRow);
            var actionHosts = new ESWindowActionHosts(
                systemHost,
                globalHost,
                windowHost);
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "foundation-tests",
                "兼容标题",
                typeof(TestAsset),
                () => null,
                _ => { },
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
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
                presentation,
                actionHosts);
            try
            {
                VisualElement root = host.Build();
                bool foundBrand = false;
                root.Query<Label>().ForEach(label =>
                    foundBrand |= label.text == "ES 测试工作台");
                Assert.IsTrue(foundBrand);
                Label brand = root.Q<Label>("ESWorkbenchBrandTitle");
                Assert.IsNotNull(brand);
                Assert.AreEqual("ES 测试工作台", brand.text);
                Assert.GreaterOrEqual(brand.style.minWidth.value.value, 128f);
                Assert.AreEqual("测试作者场景", root.Q<Label>("ESWorkbenchWorkspaceTitle").text);
                Assert.AreSame(root.Q<VisualElement>("ESWorkbenchCommandBar"), actionStack.parent);
                Assert.AreSame(systemHost, systemButton.parent);
                host.ApplyResponsiveLayoutForTest(760f, 420f, 560f);
                brand = root.Q<Label>("ESWorkbenchBrandTitle");
                Assert.AreEqual("ES 测试工作台", brand.text);
                Assert.AreEqual("ES 测试工作台", brand.tooltip);
                Assert.GreaterOrEqual(brand.style.minWidth.value.value, 128f,
                    "最小商业窗口宽度下仍应优先完整保留中文品牌预算。");
                Assert.AreEqual("测试内容与层级", root.Q<Label>("ESWorkbenchLeftPanelTitle").text);
                Assert.IsNull(root.Q<Label>("ESWorkbenchWorkspaceTitle"),
                    "紧凑中心宽度应收纳辅助工作区标题，把空间让给视口和文档入口。");
                Assert.AreSame(root.Q<VisualElement>("ESWorkbenchCommandActionRow"), actionStack.parent,
                    "窄屏重建命令栏后，ES 系统动作必须迁入第二行且保持同一实例。 ");
                Assert.AreSame(systemHost, systemButton.parent);
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
        public void AuthoringEmptyStateBlocksViewportUntilAssetIsBound()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            TestAsset boundAsset = null;
            int primaryRuns = 0;
            int secondaryRuns = 0;
            var actions = new ESWorkbenchActionContext(
                window,
                new ESWorkbenchSelectionService(),
                new ESWorkbenchToolStateService(),
                new ESWorkbenchAuthoringService(),
                (_, __) => { },
                (_, __) => { },
                _ => { },
                (_, __) => { });
            var presentation = new ESWorkbenchHostPresentationDescriptor(
                "test.empty-state",
                "ES 空状态测试",
                emptyState: new ESWorkbenchEmptyStateDescriptor(
                    "创建或打开测试资产",
                    "未绑定资产时不允许启动视口。",
                    "test.create",
                    "test.sample",
                    "测试边界说明。"));
            ESWorkbenchCommandDescriptor[] commands =
            {
                new ESWorkbenchCommandDescriptor(
                    "test.create", "创建测试资产", _ => primaryRuns++,
                    showInToolbar: false,
                    role: ESWorkbenchCommandRole.Primary),
                new ESWorkbenchCommandDescriptor(
                    "test.sample", "打开测试样本", _ => secondaryRuns++,
                    showInToolbar: false,
                    role: ESWorkbenchCommandRole.Validation)
            };
            var host = new ESWorkbenchUIToolkitHost(
                window,
                actions,
                "empty-state-tests",
                "ES 空状态测试",
                typeof(TestAsset),
                () => boundAsset,
                value => boundAsset = value as TestAsset,
                () => new[]
                {
                    new ESWorkbenchDocumentDefinition(
                        "authoring", "作者视图", "测试视口", true, ESWorkbenchDirtyFlags.Authoring)
                },
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
                () => new[]
                {
                    new ESWorkbenchViewportDescriptor(
                        "test.viewport", "测试视口", ESWorkbenchViewportKind.Custom,
                        _ => new StubStatusViewport())
                },
                () => Array.Empty<ESWorkbenchObjectDescriptor>(),
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
                () => Array.Empty<ESWorkbenchInspectorDescriptor>(),
                () => Array.Empty<ESWorkbenchToolDescriptor>(),
                () => commands,
                new ESWorkbenchLayoutState { activeDocument = "authoring" },
                _ => null,
                _ => { },
                () => "authoring",
                presentation: presentation);
            try
            {
                VisualElement root = host.Build();
                Assert.IsNotNull(root.Q<VisualElement>("ESWorkbenchAuthoringEmptyState"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchAuthoringEmptyStatePrimary"));
                Assert.IsNotNull(root.Q<Button>("ESWorkbenchAuthoringEmptyStateSecondary"));
                Assert.IsNull(host.ActiveViewportForTest,
                    "没有作者资产时不得创建或激活 PreviewScene 视口。 ");
                commands[0].Execute(actions);
                commands[1].Execute(actions);
                Assert.AreEqual(1, primaryRuns);
                Assert.AreEqual(1, secondaryRuns);

                boundAsset = ScriptableObject.CreateInstance<TestAsset>();
                host.RefreshRegistrations();

                Assert.IsNull(root.Q<VisualElement>("ESWorkbenchAuthoringEmptyState"));
                Assert.IsNotNull(host.ActiveViewportForTest,
                    "资产绑定后必须恢复正式作者视口。 ");
            }
            finally
            {
                host.Dispose();
                if (boundAsset != null) UnityEngine.Object.DestroyImmediate(boundAsset);
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
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
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
        public void HostRestoresSelectionFromFreshContentDescriptor()
        {
            var window = ScriptableObject.CreateInstance<TestEditorWindow>();
            var selection = new ESWorkbenchSelectionService();
            var content = new[]
            {
                new ESWorkbenchObjectDescriptor(
                    "content.region.spawn",
                    "出生区域",
                    "区域模板",
                    null,
                    contentKind: ESWorkbenchContentKind.RegionTemplate,
                    dragMode: ESWorkbenchContentDragMode.CreateRegion,
                    selectionKind: "test.content.region")
            };
            var layout = new ESWorkbenchLayoutState
            {
                selectedStableId = "content.region.spawn",
                selectedKind = "test.content.region",
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
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
                () => Array.Empty<ESWorkbenchViewportDescriptor>(),
                () => content,
                () => Array.Empty<ESWorkbenchHierarchyDescriptor>(),
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

                Assert.AreEqual("content.region.spawn", selection.Current.StableId);
                Assert.AreEqual("test.content.region", selection.Current.Kind);
                Assert.AreSame(content[0], selection.Current.Payload);
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
                () => Array.Empty<ESWorkbenchDocumentDefinition>(),
                () => Array.Empty<ESWorkbenchAuthoringModeDefinition>(),
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
        public void HeightfieldRaycastUsesCurrentDraftSurfaceAndRejectsOutsideRay()
        {
            var definition = new ESWorldMapDefinition
            {
                worldMin = new Vector2(-10f, -20f),
                worldMax = new Vector2(30f, 20f),
                terrainHeightScale = 100f,
                heightfield = new ESWorldMapHeightfield
                {
                    width = 5,
                    height = 5,
                    defaultHeight = 0.25f,
                    samples = new List<float>()
                }
            };

            Assert.IsTrue(ESWorldHeightfieldReadOnly.TryRaycast(
                definition,
                new Ray(new Vector3(10f, 120f, 0f), Vector3.down),
                out Vector3 hit));
            Assert.That(hit.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(hit.y, Is.EqualTo(25f).Within(0.01f));
            Assert.That(hit.z, Is.EqualTo(0f).Within(0.001f));

            Assert.IsFalse(ESWorldHeightfieldReadOnly.TryRaycast(
                definition,
                new Ray(new Vector3(40f, 120f, 0f), Vector3.down),
                out _), "地图外射线不能被夹取为边缘落点。");
            Assert.AreEqual(0, definition.heightfield.samples.Count,
                "只读射线命中不得初始化作者高度场样本。");
        }

        [Test]
        public void HeightfieldRaycastSupportsObliqueCameraRaysWithoutEdgeClamping()
        {
            var definition = new ESWorldMapDefinition
            {
                worldMin = new Vector2(-20f, -20f),
                worldMax = new Vector2(20f, 20f),
                terrainHeightScale = 80f,
                heightfield = new ESWorldMapHeightfield
                {
                    width = 9,
                    height = 9,
                    defaultHeight = 0.25f,
                    samples = new List<float>()
                }
            };
            Vector3 direction = new Vector3(0.18f, -0.92f, 0.34f).normalized;

            Assert.IsTrue(ESWorldHeightfieldReadOnly.TryRaycast(
                definition,
                new Ray(new Vector3(-5f, 70f, -28f), direction),
                out Vector3 hit),
                "透视相机斜射线必须命中当前 Draft 高度场表面。");
            Assert.That(hit.x, Is.InRange(definition.worldMin.x, definition.worldMax.x));
            Assert.That(hit.z, Is.InRange(definition.worldMin.y, definition.worldMax.y));
            Assert.That(hit.y, Is.EqualTo(20f).Within(0.05f));
            Assert.AreEqual(0, definition.heightfield.samples.Count,
                "斜射线预览仍不得修改或补齐作者高度场样本。");
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
                ESWorkbenchCommandDescriptor save = window.CommandsForTest
                    .Single(command => command.CommandId == "core.save");
                Assert.IsNull(save.Icon, "贡献注册阶段不得解析 Unity GUI 图标。");
                Assert.AreEqual("d_SaveAs", save.UnityIconName);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DocumentSwitchReleasesOnlyDocumentBeingLeftAndCleanupClearsDefinitions()
        {
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            int firstReleased = 0;
            int secondReleased = 0;
            try
            {
                window.RegisterDocumentForTest("first", () => firstReleased++);
                window.RegisterDocumentForTest("second", () => secondReleased++);

                window.SelectDocumentForTest("first");
                window.SelectDocumentForTest("second");
                window.SelectDocumentForTest("second");

                Assert.AreEqual(1, firstReleased);
                Assert.Zero(secondReleased);
                Assert.AreEqual(2, window.DocumentCountForTest);

                window.ReleaseForTest();

                Assert.AreEqual(1, firstReleased);
                Assert.AreEqual(1, secondReleased);
                Assert.Zero(window.DocumentCountForTest);
                Assert.AreEqual(string.Empty, window.SelectedDocumentIdForTest);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReloadRebindAndCloseReleaseCurrentDocumentWithoutKeepingOldClosure()
        {
            int oldReleased = 0;
            int latestReleased = 0;
            RegisterDocumentDescriptor(TestWorkbenchWindow.WorkbenchIdForTest, "old", () => oldReleased++);
            var window = ScriptableObject.CreateInstance<TestWorkbenchWindow>();
            var asset = ScriptableObject.CreateInstance<TestAsset>();
            try
            {
                window.InitializeForTest();
                Assert.AreEqual(1, window.DocumentCountForTest);

                RegisterDocumentDescriptor(TestWorkbenchWindow.WorkbenchIdForTest, "latest", () => latestReleased++);
                window.ReloadForTest();
                Assert.AreEqual(1, oldReleased, "贡献重载必须释放旧文档闭包。");
                Assert.AreEqual(1, window.DocumentCountForTest, "重复加载不得保留旧文档定义。");

                window.BindAssetForTest(asset);
                Assert.AreEqual(1, latestReleased, "资产重绑必须释放重绑前的当前文档。");
                Assert.AreEqual(1, window.DocumentCountForTest);

                window.ReleaseForTest();
                Assert.AreEqual(2, latestReleased, "窗口清理必须释放重绑后的当前文档。");
                Assert.Zero(window.DocumentCountForTest);
            }
            finally
            {
                window.ReleaseForTest();
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RemovingModuleReleasesOldDocumentAndFallsBackToRemainingDocument()
        {
            int coreReleased = 0;
            int alphaReleased = 0;
            RegisterDocumentDescriptor(
                TestWorkbenchWindow.WorkbenchIdForTest,
                "core-page",
                "core",
                TestModule.Core,
                () => coreReleased++);
            RegisterDocumentDescriptor(
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
                window.ReloadForTest();
                coreReleased = 0;
                alphaReleased = 0;
                window.SelectDocumentForTest("alpha");
                Assert.AreEqual(1, coreReleased);

                window.ModulesForTest.Remove(TestModule.Alpha);
                window.ReloadForTest();

                Assert.AreEqual(1, alphaReleased);
                Assert.AreEqual(1, window.DocumentCountForTest);
                Assert.AreEqual("core", window.SelectedDocumentIdForTest);
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
                Assert.AreEqual(3, window.RegisteredDocumentCountForTest);
                Assert.AreEqual(8, window.RegisteredAuthoringModeCountForTest);
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
                Assert.AreEqual(new Vector2(980f, 640f), window.IdealMinimumSizeForTest);
                Assert.LessOrEqual(window.MinimumSizeForTest.x, window.IdealMinimumSizeForTest.x);
                Assert.LessOrEqual(window.MinimumSizeForTest.y, window.IdealMinimumSizeForTest.y);
                Assert.AreEqual(new Vector2(1440f, 900f), window.DefaultSizeForTest);
                Assert.AreEqual(980f, window.LayoutPolicyForTest.MinimumWindowWidth);
                Assert.AreEqual(640f, window.LayoutPolicyForTest.MinimumWindowHeight);
                Assert.AreEqual(320f, window.LayoutPolicyForTest.PreferredLeftPaneWidth);
                Assert.AreEqual(420f, window.LayoutPolicyForTest.MaximumLeftPaneWidth);
                Assert.AreEqual(3, window.RegisteredDocumentCountForTest);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "terrain", "material", "vegetation", "prefab", "region", "poi",
                        "water-weather", "navigation", "collision", "streaming"
                    },
                    window.AuthoringModeIdsForTest);
                Assert.AreEqual(
                    ESWorkbenchResponsiveTier.Compact,
                    window.LayoutPolicyForTest.ResolveTier(window.IdealMinimumSizeForTest.x));
                Assert.GreaterOrEqual(
                    window.LayoutPolicyForTest.ResolveProtectedCenterWidth(window.MinimumSizeForTest.x),
                    Mathf.Min(
                        window.LayoutPolicyForTest.MinimumCenterWidth,
                        Mathf.Max(
                            280f,
                            window.MinimumSizeForTest.x
                                - window.LayoutPolicyForTest.MinimumLeftPaneWidth)));
                Dictionary<string, ESWorkbenchCommandDescriptor> commands = window.CommandsForTest
                    .ToDictionary(command => command.CommandId, StringComparer.Ordinal);
                Assert.AreEqual(ESWorkbenchCommandRole.Validation, commands["world.validate"].Role);
                Assert.AreEqual(ESWorkbenchCommandVisibility.Pinned, commands["world.validate"].Visibility);
                Assert.AreEqual(ESWorkbenchCommandRole.Authoring, commands["world.brush"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Build, commands["world.build-preflight"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Build, commands["world.build"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Dangerous, commands["world.reload-source"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Dangerous, commands["world.revert"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Dangerous, commands["world.formal-output"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Primary, commands["world.create-map"].Role);
                Assert.AreEqual(ESWorkbenchCommandRole.Validation, commands["world.load-commercial-sample"].Role);
                Assert.IsTrue(commands.Values.All(command => command.Icon == null),
                    "World 首次启用只允许登记稳定图标键，不得访问 GUIState。");
                Assert.AreEqual("TestPassed", commands["world.validate"].UnityIconName);
                Assert.AreEqual("d_SceneAsset Icon", commands["world.formal-output"].UnityIconName);
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldWindowCreationHasNoInheritedSerializedFieldCollision()
        {
            ESWorldBuilderWorkbenchWindow window =
                ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            try
            {
                Assert.IsNotNull(window);
                Assert.That(window.IdealMinimumSizeForTest, Is.EqualTo(new Vector2(980f, 640f)));
                Assert.LessOrEqual(window.MinimumSizeForTest.x, 980f);
                Assert.LessOrEqual(window.MinimumSizeForTest.y, 640f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldContentCatalogAlwaysExposesBrushAndRegionTemplateFamilies()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            try
            {
                window.InitializeForTest();
                IReadOnlyList<ESWorkbenchObjectDescriptor> content = window.QueryWorldPaletteForTest();

                Assert.AreEqual(6, content.Count(value => value.ContentKind == ESWorkbenchContentKind.Brush));
                Assert.AreEqual(3, content.Count(value => value.ContentKind == ESWorkbenchContentKind.RegionTemplate));
                Assert.IsTrue(content.Where(value => value.ContentKind == ESWorkbenchContentKind.Brush)
                    .All(value => value.DragMode == ESWorkbenchContentDragMode.ActivateTool
                        && value.SelectionKind == "world.content.brush"
                        && value.Presets.Count == 3));
                Assert.IsTrue(content.Where(value => value.ContentKind == ESWorkbenchContentKind.RegionTemplate)
                    .All(value => value.DragMode == ESWorkbenchContentDragMode.CreateRegion
                        && value.SelectionKind == "world.content.region"
                        && value.Presets.Count == 3));
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldViewportStatusUsesChineseAuthoringToolName()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            try
            {
                ESWorkbenchViewportStatusDescriptor tool = window
                    .GetViewportStatusSnapshot(ESWorkbenchViewportKind.Canvas2D)
                    .Single(value => value.StatusId == "world.gizmo");

                Assert.AreEqual("选择", tool.Value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldContentCatalogBrushAndRegionDropsMutateOnlyDraftAndMarkSessionDirty()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                string sourceBefore = JsonUtility.ToJson(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                IReadOnlyList<ESWorkbenchObjectDescriptor> content = window.QueryWorldPaletteForTest();
                ESWorkbenchObjectDescriptor brush = content.First(value => value.ContentKind == ESWorkbenchContentKind.Brush);
                ESWorkbenchObjectDescriptor region = content.First(value => value.ContentKind == ESWorkbenchContentKind.RegionTemplate);
                ESWorldMapAsset draft = window.EditSessionForTest.Draft;
                int regionCount = draft.Definition.regions.Count;
                Vector3 point = new Vector3(
                    (draft.Definition.worldMin.x + draft.Definition.worldMax.x) * 0.5f,
                    0f,
                    (draft.Definition.worldMin.y + draft.Definition.worldMax.y) * 0.5f);

                Assert.IsTrue(window.CanUsePaletteItem(brush, out string brushReason), brushReason);
                Assert.IsTrue(window.TryUsePaletteItem(brush, point, out string brushMessage), brushMessage);
                Assert.IsTrue(window.CanUsePaletteItem(region, out string regionReason), regionReason);
                Assert.IsTrue(window.TryUsePaletteItem(region, point, out string regionMessage), regionMessage);

                Assert.AreEqual(regionCount + 1, draft.Definition.regions.Count);
                Assert.AreEqual("Gameplay", draft.Definition.regions.Last().semanticTag);
                Assert.AreEqual(region.ObjectId, window.ActiveWorldContentIdForTest);
                window.HandleAuthoringPoint(point + new Vector3(64f, 0f, 64f));
                Assert.AreEqual(regionCount + 2, draft.Definition.regions.Count,
                    "选择切换到新建区域后，活动区域模板仍应保持，可连续创建同类区域。");
                Assert.AreEqual("Gameplay", draft.Definition.regions.Last().semanticTag);
                Assert.IsTrue(window.EditSessionForTest.IsDirty);
                Assert.AreEqual(sourceBefore, JsonUtility.ToJson(source),
                    "内容拖放只能修改当前窗口 Draft，不能越过提交事务改写正式 Source。");
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldBrushCardDropPaintsOnceWithoutTakingOverSceneSelectionTool()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                window.ActivateToolForTest("world.select");
                ESWorldMapAsset draft = window.EditSessionForTest.Draft;
                string before = JsonUtility.ToJson(draft.Definition.heightfield);
                ESWorkbenchObjectDescriptor brush = window.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.Brush
                        && value.ObjectId.EndsWith("highland", StringComparison.Ordinal));
                Vector3 center = new Vector3(
                    (draft.Definition.worldMin.x + draft.Definition.worldMax.x) * 0.5f,
                    0f,
                    (draft.Definition.worldMin.y + draft.Definition.worldMax.y) * 0.5f);

                Assert.IsTrue(window.TryUsePaletteItem(brush, center, out string message), message);

                Assert.AreNotEqual(before, JsonUtility.ToJson(draft.Definition.heightfield));
                Assert.AreEqual("world.select", window.ActiveToolIdForTest,
                    "笔刷卡片拖入只能执行一次笔刷，不能夺走区域或对象的后续移动入口。");
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldTerrainToolCannotPromoteHitObjectIntoMoveGesture()
        {
            Assert.IsTrue(ESWorkbenchInteractionPolicy.ShouldBeginTerrainPaint(
                terrainToolActive: true,
                selectionOrTransformInteractionActive: false),
                "笔刷工具命中区域主体时必须保留地面绘制主意图。");
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldBeginTerrainPaint(
                terrainToolActive: true,
                selectionOrTransformInteractionActive: true),
                "显式选择/变换工具拥有对象命中时，笔刷不得抢占主手势。");
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldBeginTerrainPaint(
                terrainToolActive: false,
                selectionOrTransformInteractionActive: false));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: false,
                moveInteractionEnabled: false,
                canMove: true,
                hierarchyLocked: false));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: true,
                moveInteractionEnabled: false,
                canMove: true,
                hierarchyLocked: false),
                "地形笔刷命中对象时不得把笔刷状态当作移动工具。");
            Assert.IsTrue(ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: true,
                moveInteractionEnabled: true,
                canMove: true,
                hierarchyLocked: false));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldBeginObjectMove(
                hasHitObject: true,
                selectionInteraction: true,
                moveInteractionEnabled: true,
                canMove: true,
                hierarchyLocked: true));
            Assert.IsTrue(ESWorkbenchInteractionPolicy.ShouldHandleNavigation(false, false));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldHandleNavigation(true, false),
                "外部资源拖放必须独占视口导航输入。");
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldHandleNavigation(false, true),
                "对象移动或笔刷进行中不得同时缩放、平移或旋转相机。");
        }

        [Test]
        public void WorldTerrainStrokeCancelRestoresHeightfieldAndDirtyState()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            ESWorldMapAsset draft = null;
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                draft = window.EditSessionForTest.Draft;
                ESWorldMapHeightfield field = draft.Definition.heightfield;
                string before = JsonUtility.ToJson(field);
                ESWorkbenchObjectDescriptor brush = window.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.Brush
                        && value.ObjectId.EndsWith("highland", StringComparison.Ordinal));
                Vector3 center = new Vector3(
                    (draft.Definition.worldMin.x + draft.Definition.worldMax.x) * 0.5f,
                    0f,
                    (draft.Definition.worldMin.y + draft.Definition.worldMax.y) * 0.5f);

                window.BeginTerrainStroke();
                Assert.IsTrue(window.TryUsePaletteItem(brush, center, out string message), message);
                window.HandleAuthoringPoint(center + new Vector3(12f, 0f, 0f));
                Assert.AreNotEqual(before, JsonUtility.ToJson(field));
                Assert.IsTrue(window.EditSessionForTest.IsDirty);

                window.CancelTerrainStroke();

                Assert.AreEqual(before, JsonUtility.ToJson(draft.Definition.heightfield));
                Assert.IsFalse(window.EditSessionForTest.IsDirty,
                    "取消整笔后，只有该笔产生的 Dirty 必须一并消失。");
            }
            finally
            {
                if (draft != null) Undo.ClearUndo(draft);
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldTerrainStrokeEndKeepsChangesAndOneUndoRestoresStart()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            ESWorldMapAsset draft = null;
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                draft = window.EditSessionForTest.Draft;
                string before = JsonUtility.ToJson(draft.Definition.heightfield);
                ESWorkbenchObjectDescriptor brush = window.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.Brush
                        && value.ObjectId.EndsWith("highland", StringComparison.Ordinal));
                Vector3 center = new Vector3(
                    (draft.Definition.worldMin.x + draft.Definition.worldMax.x) * 0.5f,
                    0f,
                    (draft.Definition.worldMin.y + draft.Definition.worldMax.y) * 0.5f);

                window.BeginTerrainStroke();
                Assert.IsTrue(window.TryUsePaletteItem(brush, center, out string message), message);
                window.HandleAuthoringPoint(center + new Vector3(12f, 0f, 0f));
                window.EndTerrainStroke();

                Assert.AreNotEqual(before, JsonUtility.ToJson(draft.Definition.heightfield));
                Undo.PerformUndo();
                window.EditSessionForTest.SynchronizeDraftAfterUndoRedo();
                Assert.AreEqual(before, JsonUtility.ToJson(draft.Definition.heightfield));
                Assert.IsFalse(window.EditSessionForTest.IsDirty);
            }
            finally
            {
                if (draft != null) Undo.ClearUndo(draft);
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldTerrainBrushShortcutsAdjustRadiusAndStrengthWithoutMutatingDraft()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            try
            {
                float radius = window.TerrainBrushRadiusForTest;
                float strength = window.TerrainBrushStrengthForTest;

                Assert.IsTrue(window.HandleTerrainBrushShortcut(
                    KeyCode.RightBracket, EventModifiers.None));
                Assert.Greater(window.TerrainBrushRadiusForTest, radius);
                Assert.AreEqual(strength, window.TerrainBrushStrengthForTest);

                Assert.IsTrue(window.HandleTerrainBrushShortcut(
                    KeyCode.LeftBracket, EventModifiers.Shift));
                Assert.Less(window.TerrainBrushStrengthForTest, strength);
                Assert.IsFalse(window.HandleTerrainBrushShortcut(KeyCode.B, EventModifiers.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldRegionPresetChangesPlacedSizeWithoutChangingBaseContentIdentity()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                ESWorkbenchObjectDescriptor region = window.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.RegionTemplate);
                ESWorkbenchObjectDescriptor large = region.CreatePresetVariant("large");
                Vector3 point = new Vector3(64f, 0f, 64f);

                Assert.AreEqual(region.BaseObjectId, large.BaseObjectId);
                Assert.AreEqual("large", large.PresetId);
                Assert.IsTrue(window.TryUsePaletteItem(large, point, out string message), message);

                ESWorldMapRegionDefinition placed = window.EditSessionForTest.Draft.Definition.regions.Last();
                Vector2 size = placed.max - placed.min;
                Assert.Greater(size.x, 48f);
                Assert.Greater(size.y, 48f);
                Assert.AreEqual(region.BaseObjectId, window.ActiveWorldContentIdForTest);
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void WorldRegionTemplateKeepsRequestedSizeAtWorldBoundary()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                window.InitializeForTest();
                window.BindAssetForTest(source);
                ESWorkbenchObjectDescriptor region = window.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.RegionTemplate
                        && value.ObjectId.EndsWith("playable", StringComparison.Ordinal));
                ESWorldMapDefinition definition = window.EditSessionForTest.Draft.Definition;
                Vector3 boundary = new Vector3(definition.worldMax.x, 0f, definition.worldMax.y);

                Assert.IsTrue(window.TryUsePaletteItem(region, boundary, out string message), message);

                ESWorldMapRegionDefinition created = definition.regions.Last();
                Assert.That(created.max, Is.EqualTo(definition.worldMax));
                Assert.That(created.max - created.min, Is.EqualTo(new Vector2(48f, 48f)));
            }
            finally
            {
                window.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WorldActiveContentIsIsolatedPerWindow()
        {
            var first = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            var second = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldMapAsset source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(source);
                first.InitializeForTest();
                second.InitializeForTest();
                first.BindAssetForTest(source);
                second.BindAssetForTest(source);
                ESWorkbenchObjectDescriptor firstBrush = first.QueryWorldPaletteForTest()
                    .First(value => value.ContentKind == ESWorkbenchContentKind.Brush);
                ESWorkbenchObjectDescriptor secondBrush = second.QueryWorldPaletteForTest()
                    .Last(value => value.ContentKind == ESWorkbenchContentKind.Brush);
                Vector3 center = Vector3.zero;

                Assert.IsTrue(first.TryUsePaletteItem(firstBrush, center, out string firstMessage), firstMessage);
                Assert.IsTrue(second.TryUsePaletteItem(secondBrush, center, out string secondMessage), secondMessage);

                Assert.AreEqual(firstBrush.ObjectId, first.ActiveWorldContentIdForTest);
                Assert.AreEqual(secondBrush.ObjectId, second.ActiveWorldContentIdForTest);
                Assert.AreNotEqual(first.ActiveWorldContentIdForTest, second.ActiveWorldContentIdForTest);
            }
            finally
            {
                first.DisableForTest();
                second.DisableForTest();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ViewportDropDiagnosticsExposeSpecificRejectionReason()
        {
            var viewport = new DiagnosticStubViewport("世界根节点已锁定，不能拖放内容。");
            var item = new ESWorkbenchObjectDescriptor("content", "内容", "测试", null);

            Assert.IsFalse(ESWorkbenchUIToolkitHost.CanViewportAccept(viewport, item, out string reason));
            Assert.AreEqual("世界根节点已锁定，不能拖放内容。", reason);
        }

        [Test]
        public void CommercialValidationSampleIsValidLongChineseAndIdempotent()
        {
            ESWorldMapAsset asset = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(asset);
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(asset);

                Assert.That(asset.Validate(out string error), Is.True, error);
                Assert.That(asset.Definition.mapId, Is.EqualTo("es.world.commercial-validation"));
                Assert.That(asset.Definition.regions.Count(value =>
                    value != null
                    && value.regionId == "region.commercial-validation-long-cn"), Is.EqualTo(1));
                Assert.That(asset.Definition.pois.Count(value =>
                    value != null
                    && value.poiId == "poi.commercial-validation-long-cn"), Is.EqualTo(1));
                Assert.That(asset.Definition.regions.Exists(value =>
                    value != null && value.displayName.Length >= 24), Is.True);
                Assert.That(asset.Definition.pois.Exists(value =>
                    value != null && value.displayName.Length >= 24), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
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
        public void ViewportAdaptersExposeDirectGestureCancellationContract()
        {
            Assert.IsTrue(
                typeof(IESWorkbenchCancelableViewport).IsAssignableFrom(
                    typeof(ESWorkbenchCanvas2DViewport)));
            Assert.IsTrue(
                typeof(IESWorkbenchCancelableViewport).IsAssignableFrom(
                    typeof(ESWorkbenchPreview3DViewport)));
            Assert.IsTrue(
                typeof(IESWorkbenchCancelableViewport).IsAssignableFrom(
                    typeof(ESWorldWorkbenchViewportAdapter)));
        }

        [Test]
        public void DropPreviewContextPreservesDirectItemBatchSpacingAndRejection()
        {
            ESWorkbenchActionContext actions = CreateActionContextForTest();
            var primary = new ESWorkbenchObjectDescriptor(
                "content.primary", "主内容", "测试", null,
                contentKind: ESWorkbenchContentKind.Prefab,
                dragMode: ESWorkbenchContentDragMode.Place);
            var secondary = new ESWorkbenchObjectDescriptor(
                "content.secondary", "次内容", "测试", null,
                contentKind: ESWorkbenchContentKind.Prefab,
                dragMode: ESWorkbenchContentDragMode.Place);
            var context = new ESWorkbenchDropPreviewContext(
                actions,
                primary,
                new[] { primary, secondary },
                new Vector2(120f, 80f),
                new Rect(0f, 0f, 640f, 360f),
                0.1f,
                false,
                "当前目标拒绝放置",
                new Vector3(12f, 3f, 18f),
                true);

            Assert.AreSame(actions, context.Actions);
            Assert.AreSame(primary, context.PrimaryItem);
            CollectionAssert.AreEqual(new[] { primary, secondary }, context.Items);
            Assert.AreEqual(new Vector2(120f, 80f), context.LocalPosition);
            Assert.AreEqual(new Rect(0f, 0f, 640f, 360f), context.ViewportRect);
            Assert.AreEqual(0.25f, context.Spacing, "预览间距必须与正式批量合同使用同一下限。");
            Assert.IsFalse(context.Accepted);
            Assert.AreEqual("当前目标拒绝放置", context.Reason);
            Assert.IsTrue(context.HasResolvedWorldPosition);
            Assert.AreEqual(new Vector3(12f, 3f, 18f), context.ResolvedWorldPosition);
            Assert.IsTrue(context.State.Rejected);
            Assert.AreEqual("当前目标拒绝放置", context.State.ShortReason());
            Assert.IsTrue(context.IsBatch);
        }

        [Test]
        public void DropPreviewContextSeparatesVisualRejectionFromCommitContract()
        {
            ESWorkbenchActionContext actions = CreateActionContextForTest();
            var item = new ESWorkbenchObjectDescriptor(
                "content.region", "区域", "测试", null,
                contentKind: ESWorkbenchContentKind.RegionTemplate,
                dragMode: ESWorkbenchContentDragMode.CreateRegion);
            var rejected = new ESWorkbenchDropPreviewContext(
                actions,
                item,
                new[] { item },
                new Vector2(30f, 40f),
                new Rect(0f, 0f, 640f, 360f),
                1f,
                false,
                "区域根节点已锁定");

            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual("区域根节点已锁定", rejected.Reason);
            Assert.IsTrue(rejected.State.Rejected);
            Assert.AreSame(item, rejected.PrimaryItem,
                "拒绝预览仍必须保留内容身份，视口才能绘制红色目标框。 ");
            Assert.IsFalse(rejected.IsBatch);
        }

        [Test]
        public void DropPreviewStateKeepsSharedAcceptedAndReasonSemantics()
        {
            ESWorkbenchDropPreviewState allowed = ESWorkbenchDropPreviewState.Allowed;
            Assert.IsTrue(allowed.Accepted);
            Assert.IsFalse(allowed.Rejected);
            Assert.AreEqual(string.Empty, allowed.Reason);

            ESWorkbenchDropPreviewState rejected =
                ESWorkbenchDropPreviewState.RejectedBy("这是一条用于验证截断行为的较长拒绝原因文本");
            Assert.IsFalse(rejected.Accepted);
            Assert.IsTrue(rejected.Rejected);
            Assert.LessOrEqual(rejected.ShortReason(12).Length, 13);
            Assert.AreEqual("原因", new ESWorkbenchDropPreviewState(false, "原因").Reason);
        }

        [Test]
        public void PointerDragStateKeepsClickAndDragMutuallyExclusive()
        {
            var drag = new ESWorkbenchPointerDragState();

            Assert.IsTrue(drag.Arm(7, new Vector2(10f, 10f)));
            Assert.IsTrue(drag.ShouldClick(7, new Vector2(13f, 13f)));
            Assert.IsFalse(drag.ShouldStart(7, new Vector2(13f, 13f)));
            Assert.IsTrue(drag.ShouldStart(7, new Vector2(16f, 10f)));
            Assert.IsTrue(drag.MarkStarted(7));
            Assert.IsTrue(drag.IsStarted);
            Assert.IsFalse(drag.ShouldClick(7, new Vector2(10f, 10f)),
                "拖动一旦开始，PointerUp 不得再退化为点击选择。");

            Assert.AreEqual(7, drag.Reset());
            Assert.AreEqual(ESWorkbenchPointerDragPhase.Idle, drag.Phase);
            Assert.AreEqual(-1, drag.PointerId);
        }

        [Test]
        public void PointerDragStateRejectsWrongPointerAndInvalidCoordinates()
        {
            var drag = new ESWorkbenchPointerDragState();

            Assert.IsFalse(drag.Arm(-1, Vector2.zero));
            Assert.IsFalse(drag.Arm(1, new Vector2(float.NaN, 0f)));
            Assert.IsTrue(drag.Arm(1, Vector2.zero));
            Assert.IsFalse(drag.Arm(2, Vector2.one), "已有主指针时，第二个指针不得覆盖拖动所有权。");
            Assert.IsFalse(drag.ShouldStart(2, Vector2.one * 100f));
            Assert.IsFalse(drag.MarkStarted(2));
            Assert.IsFalse(drag.ShouldClick(1, new Vector2(float.PositiveInfinity, 0f)));
        }

        [Test]
        public void PointerDragStateNormalizesNonFiniteThreshold()
        {
            var drag = new ESWorkbenchPointerDragState(float.NaN);
            Assert.IsTrue(drag.Arm(1, Vector2.zero));
            Assert.IsFalse(drag.ShouldStart(1, new Vector2(5.9f, 0f)));
            Assert.IsTrue(drag.ShouldStart(1, new Vector2(6f, 0f)));
            drag.Reset();
            Assert.IsTrue(drag.Arm(1, Vector2.zero));
            Assert.IsTrue(drag.ShouldStart(1, new Vector2(6f, 0f), float.NaN));
        }

        [Test]
        public void PointerDragStateCannotBecomeClickAfterDragStarts()
        {
            var drag = new ESWorkbenchPointerDragState(6f);
            Assert.IsTrue(drag.Arm(1, Vector2.zero));
            Assert.IsTrue(drag.ShouldStart(1, new Vector2(6f, 0f)));
            Assert.IsTrue(drag.MarkStarted(1));
            Assert.IsFalse(drag.ShouldClick(1, Vector2.zero),
                "拖动一旦开始，即使指针回到按下位置也不能再次触发卡片选择。");
        }

        [Test]
        public void ContentDragSessionSnapshotsPrimaryBatchAndSourceIndependentlyOfSelection()
        {
            var primary = new ESWorkbenchObjectDescriptor(
                "content.card", "卡片内容", "测试", null,
                contentKind: ESWorkbenchContentKind.Prefab);
            var secondary = new ESWorkbenchObjectDescriptor(
                "content.secondary", "批量内容", "测试", null,
                contentKind: ESWorkbenchContentKind.Prefab);
            var duplicate = new ESWorkbenchObjectDescriptor(
                "content.duplicate", "重复内容", "测试", null,
                contentKind: ESWorkbenchContentKind.Prefab,
                baseObjectId: primary.BaseObjectId);
            var batch = new List<ESWorkbenchObjectDescriptor> { secondary, duplicate };
            var session = new ESWorkbenchContentDragSession(
                ESWorkbenchContentDragSource.ContentCard, 6f);

            Assert.IsTrue(session.Arm(11, Vector2.zero, primary));
            Assert.IsFalse(session.TryStart(11, new Vector2(5.9f, 0f), primary, batch));
            Assert.IsTrue(session.TryStart(11, new Vector2(6f, 0f), primary, batch));
            Assert.AreEqual(ESWorkbenchContentDragSource.ContentCard, session.Source);
            Assert.AreSame(primary, session.PrimaryItem);
            CollectionAssert.AreEqual(
                new[] { primary, secondary },
                session.Items,
                "批量快照必须以主项开头并按首次出现顺序去重。");

            batch.Clear();
            Assert.AreEqual(2, session.Items.Count, "拖动开始后不得受来源列表回收或重建影响。");
            Assert.IsTrue(session.End(ESWorkbenchContentDragEndReason.Commit));
            Assert.IsFalse(session.End(ESWorkbenchContentDragEndReason.Cancel),
                "结束必须幂等，重复释放不得覆盖首次结束原因。");
            Assert.AreEqual(ESWorkbenchContentDragEndReason.Commit, session.LastEndReason);
            Assert.IsTrue(session.HasEndReason);
            Assert.IsNull(session.PrimaryItem, "结束拖动后不得保留领域对象引用。");
            Assert.AreEqual(0, session.Items.Count, "结束拖动后必须清空批次快照。");
            session.Reset();
            Assert.IsFalse(session.HasEndReason,
                "显式重置后不得残留上一次拖动的终止原因。");
            Assert.IsTrue(session.Arm(12, Vector2.one, primary),
                "完成一次拖动后，新卡片会话仍可重新开始。");
            Assert.IsFalse(session.HasEndReason,
                "重新武装后的拖动不得继承上一次释放原因。");
        }

        [Test]
        public void ContentDragSessionRejectsWrongPointerAndInvalidatesWithoutClickFallback()
        {
            var item = new ESWorkbenchObjectDescriptor(
                "content.row", "列表内容", "测试", null,
                contentKind: ESWorkbenchContentKind.RegionTemplate);
            var session = new ESWorkbenchContentDragSession(
                ESWorkbenchContentDragSource.ObjectRow, 6f);

            Assert.IsTrue(session.Arm(4, Vector2.zero, item));
            Assert.IsFalse(session.TryStart(5, new Vector2(20f, 0f), item, null));
            Assert.IsFalse(session.ShouldClick(5, Vector2.zero));
            Assert.IsTrue(session.TryStart(4, new Vector2(20f, 0f), item, null));
            Assert.IsFalse(session.ShouldClick(4, Vector2.zero),
                "内容拖动已经开始后，列表行 PointerUp 不得退化为选择点击。");
            Assert.IsTrue(session.End(ESWorkbenchContentDragEndReason.CaptureLost));
            Assert.AreEqual(-1, session.PointerId);
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void PointerOwnershipGateAllowsOnlyOneSourceAndRequiresMatchingRelease()
        {
            var gate = new ESWorkbenchPointerOwnershipGate();
            object first = new object();
            object second = new object();

            Assert.IsTrue(gate.TryAcquire(first, 1));
            Assert.IsTrue(gate.Owns(first, 1));
            Assert.IsFalse(gate.TryAcquire(first, 2));
            Assert.IsFalse(gate.TryAcquire(second, 2));
            Assert.IsFalse(gate.Release(second, 1));
            Assert.IsTrue(gate.Release(first, 1));
            Assert.IsFalse(gate.IsOwned);
            Assert.IsTrue(gate.TryAcquire(second, 2));
            Assert.IsTrue(gate.Reset());
            Assert.IsFalse(gate.Reset());
        }

        [Test]
        public void PointerInteractionCoordinatorTransfersContentToExternalDragDeterministically()
        {
            var coordinator = new ESWorkbenchPointerInteractionCoordinator();
            object content = new object();
            object external = new object();
            object other = new object();

            Assert.IsTrue(coordinator.TryAcquire(
                content,
                7,
                ESWorkbenchPointerOwnerKind.Content));
            Assert.IsFalse(coordinator.TryAcquire(
                other,
                8,
                ESWorkbenchPointerOwnerKind.Viewport));
            Assert.IsTrue(coordinator.TryPromoteToExternalContent(content, 7, external));
            Assert.IsTrue(coordinator.IsExternalContentActive);
            Assert.AreEqual(ESWorkbenchPointerOwnerKind.ExternalContent, coordinator.OwnerKind);
            Assert.AreEqual(-1, coordinator.PointerId);
            Assert.IsFalse(coordinator.TryAcquire(
                other,
                8,
                ESWorkbenchPointerOwnerKind.Viewport));
            Assert.IsTrue(coordinator.EndExternalContent(external));
            Assert.IsFalse(coordinator.IsActive);
            Assert.IsFalse(coordinator.EndExternalContent(external));
            Assert.IsTrue(coordinator.TryAcquire(
                content,
                9,
                ESWorkbenchPointerOwnerKind.Content));
            Assert.IsTrue(coordinator.ResetIfOwnerKind(ESWorkbenchPointerOwnerKind.Content));
            Assert.IsFalse(coordinator.IsActive);
            Assert.IsTrue(coordinator.TryBeginExternalContent(external));
            Assert.IsFalse(coordinator.EndExternalContent(other));
            Assert.IsTrue(coordinator.Reset());
            Assert.IsFalse(coordinator.Reset());
        }

        [Test]
        public void ExternalDragOwnershipBlocksEveryLocalViewportOwnerUntilReleased()
        {
            var coordinator = new ESWorkbenchPointerInteractionCoordinator();
            object local = new object();
            object external = new object();

            Assert.IsTrue(coordinator.TryAcquire(
                local, 1, ESWorkbenchPointerOwnerKind.Viewport));
            Assert.IsFalse(coordinator.TryBeginExternalContent(external),
                "本地手势仍在仲裁器中时，外部拖放不能静默抢占；宿主必须先结束本地手势再开始外部拖放。");
            Assert.IsTrue(coordinator.Release(
                local, 1, ESWorkbenchPointerOwnerKind.Viewport));
            Assert.IsTrue(coordinator.TryBeginExternalContent(external));
            Assert.IsTrue(coordinator.IsExternalContentActive);
            Assert.IsFalse(coordinator.TryAcquire(
                local, 1, ESWorkbenchPointerOwnerKind.Viewport));
            Assert.IsTrue(coordinator.EndExternalContent(external));
            Assert.IsFalse(coordinator.IsActive);
        }

        [Test]
        public void PaneResizeSessionCommitsOnlyForOwningPointer()
        {
            var session = new ESWorkbenchPaneResizeSession();
            Assert.IsTrue(session.Begin(11, 240f));
            Assert.IsFalse(session.TryCommit(
                12,
                280f,
                out _,
                out _));
            Assert.IsTrue(session.IsActive);
            Assert.IsTrue(session.TryCommit(
                11,
                280f,
                out float before,
                out float after));
            Assert.AreEqual(240f, before, 0.0001f);
            Assert.AreEqual(280f, after, 0.0001f);
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void PaneResizeSessionCancelRestoresStartAndRejectsInvalidDimension()
        {
            var session = new ESWorkbenchPaneResizeSession();
            Assert.IsFalse(session.Begin(3, float.NaN));
            Assert.IsTrue(session.Begin(3, 320f));
            Assert.IsFalse(session.TryCommit(
                3,
                float.PositiveInfinity,
                out _,
                out _));
            Assert.IsTrue(session.IsActive);
            Assert.IsTrue(session.TryCancel(3, out float restore));
            Assert.AreEqual(320f, restore, 0.0001f);
            Assert.IsFalse(session.IsActive);
            Assert.IsFalse(session.TryCancel(3, out _));
        }

        [Test]
        public void GestureTerminationCommitFlushesAndCommitsWithoutRestoringPreview()
        {
            ESWorkbenchGestureTerminationDecision decision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.Commit,
                    ESWorkbenchCaptureLossPolicy.CancelPreview,
                    hasPreview: true);

            Assert.IsTrue(decision.FlushPendingSamples);
            Assert.IsTrue(decision.CommitAuthoring);
            Assert.IsFalse(decision.RestorePreview);
        }

        [Test]
        public void GestureTerminationCaptureLossCanCommitPendingSamples()
        {
            ESWorkbenchGestureTerminationDecision decision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                    ESWorkbenchCaptureLossPolicy.CommitPendingSamples,
                    hasPreview: true);

            Assert.IsTrue(decision.FlushPendingSamples);
            Assert.IsTrue(decision.CommitAuthoring);
            Assert.IsFalse(decision.RestorePreview);
        }

        [Test]
        public void GestureTerminationCaptureLossCanCancelAndRestorePreview()
        {
            ESWorkbenchGestureTerminationDecision decision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                    ESWorkbenchCaptureLossPolicy.CancelPreview,
                    hasPreview: true);

            Assert.IsFalse(decision.FlushPendingSamples);
            Assert.IsFalse(decision.CommitAuthoring);
            Assert.IsTrue(decision.RestorePreview);
        }

        [TestCase(ESWorkbenchPointerGestureSession.EndReason.Cancel)]
        [TestCase(ESWorkbenchPointerGestureSession.EndReason.ExternalDrag)]
        [TestCase(ESWorkbenchPointerGestureSession.EndReason.Deactivate)]
        public void GestureTerminationNonCommitReasonsNeverCommitOrFlush(
            ESWorkbenchPointerGestureSession.EndReason reason)
        {
            ESWorkbenchGestureTerminationDecision decision =
                ESWorkbenchGestureTerminationDecision.Resolve(
                    reason,
                    ESWorkbenchCaptureLossPolicy.CommitPendingSamples,
                    hasPreview: true);

            Assert.IsFalse(decision.FlushPendingSamples);
            Assert.IsFalse(decision.CommitAuthoring);
            Assert.IsTrue(decision.RestorePreview);
        }

        [Test]
        public void PointerGestureSessionOwnsOnePrimaryGestureAndReleasesOnCompletion()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Paint, 3, new Vector2(4f, 4f)));
            Assert.IsFalse(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Move, 4, new Vector2(4f, 4f)),
                "笔刷进行中不得被对象移动或平移抢占。");
            Assert.IsFalse(session.TryStart(4, new Vector2(20f, 20f)));
            Assert.IsTrue(session.TryStart(3, new Vector2(12f, 4f)));
            Assert.IsTrue(session.IsStarted);
            Assert.IsFalse(session.TryStart(3, new Vector2(20f, 4f)),
                "同一手势只有首次跨越阈值时可以报告 startedNow。");
            Assert.IsTrue(session.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit));
            Assert.IsFalse(session.IsActive);
            Assert.IsTrue(session.HasEndReason);
            Assert.AreEqual(ESWorkbenchPointerGestureSession.EndReason.Commit, session.LastEndReason);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan, 4, new Vector2(0f, 0f)));
            Assert.IsFalse(session.HasEndReason);
        }

        [Test]
        public void PointerGestureSessionRejectsWrongOwnerAndCancellationIsIdempotent()
        {
            var session = new ESWorkbenchPointerGestureSession();
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Move, 2, Vector2.zero));
            Assert.IsFalse(session.Owns(
                ESWorkbenchPointerGestureSession.Kind.Paint, 2));
            Assert.IsFalse(session.TryStart(9, new Vector2(20f, 0f)));
            Assert.IsTrue(session.Cancel(ESWorkbenchPointerGestureSession.EndReason.ExternalDrag));
            Assert.IsFalse(session.Cancel());
            Assert.AreEqual(ESWorkbenchPointerGestureSession.EndReason.ExternalDrag, session.LastEndReason);
            Assert.AreEqual(-1, session.PointerId);
        }

        [Test]
        public void PointerGestureSessionCannotFinishFromAnotherPointer()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Move, 7, Vector2.zero));
            Assert.IsFalse(session.TryFinishOwned(
                8, ESWorkbenchPointerGestureSession.EndReason.CaptureLost));
            Assert.IsTrue(session.IsActive);
            Assert.IsTrue(session.TryFinishOwned(
                7, ESWorkbenchPointerGestureSession.EndReason.Commit));
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void PointerGesturePointerUpCommitRequiresTheOwningPointer()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan, 12, Vector2.zero));
            Assert.IsTrue(session.TryStart(12, new Vector2(8f, 0f)));
            Assert.IsFalse(session.TryFinishOwned(
                13, ESWorkbenchPointerGestureSession.EndReason.Commit));
            Assert.IsTrue(session.IsActive);
            Assert.IsTrue(session.TryFinishOwned(
                12, ESWorkbenchPointerGestureSession.EndReason.Commit));
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void PointerGestureAdvanceReportsFirstFrameAndFinalEndpoint()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan, 4, Vector2.zero));
            Assert.IsFalse(session.TryAdvance(
                4, new Vector2(3f, 0f), false,
                out ESWorkbenchPointerGestureSession.AdvanceResult armed));
            Assert.IsTrue(armed.OwnsPointer);
            Assert.IsFalse(armed.IsStarted);
            Assert.IsTrue(session.TryAdvance(
                4, new Vector2(8f, 0f), false,
                out ESWorkbenchPointerGestureSession.AdvanceResult started));
            Assert.IsTrue(started.StartedNow);
            Assert.AreEqual(new Vector2(8f, 0f), started.Delta);
            Assert.IsTrue(session.TryAdvance(
                4, new Vector2(20f, 0f), true,
                out ESWorkbenchPointerGestureSession.AdvanceResult final));
            Assert.AreEqual(new Vector2(12f, 0f), final.Delta);
            Assert.AreEqual(new Vector2(20f, 0f), final.ConsumedPointer);
        }

        [Test]
        public void PointerGestureSessionCaptureLossIsTerminalAndReleasesOwner()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Move, 7, Vector2.zero));
            Assert.IsTrue(session.TryStart(7, new Vector2(6f, 0f)));
            Assert.IsTrue(session.Finish(
                ESWorkbenchPointerGestureSession.EndReason.CaptureLost));
            Assert.IsFalse(session.IsActive);
            Assert.IsFalse(session.Owns(
                ESWorkbenchPointerGestureSession.Kind.Move, 7));
            Assert.AreEqual(
                ESWorkbenchPointerGestureSession.EndReason.CaptureLost,
                session.LastEndReason);
        }

        [Test]
        public void PointerGestureCancellationDiscardsFutureEndpointDelta()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Move, 3, Vector2.zero));
            Assert.IsTrue(session.TryStartAndConsumePointerDelta(
                3, new Vector2(10f, 0f), out Vector2 firstDelta, out _));
            Assert.AreEqual(10f, firstDelta.x, 0.0001f);

            Assert.IsTrue(session.Cancel(
                ESWorkbenchPointerGestureSession.EndReason.Cancel));
            Assert.IsFalse(session.TryAdvance(
                3, new Vector2(100f, 0f), true,
                out ESWorkbenchPointerGestureSession.AdvanceResult afterCancel));
            Assert.IsFalse(afterCancel.OwnsPointer);
            Assert.AreEqual(
                ESWorkbenchPointerGestureSession.EndReason.Cancel,
                session.LastEndReason);
        }

        [Test]
        public void NavigationGestureUsesFeelThresholdBeforeChangingView()
        {
            var session = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan,
                5,
                new Vector2(10f, 10f)));
            Assert.IsFalse(session.TryStart(5, new Vector2(15.9f, 10f)));
            Assert.IsFalse(session.IsStarted);
            Assert.IsTrue(session.TryStart(5, new Vector2(16f, 10f)));
            Assert.IsTrue(session.IsStarted);
        }

        [Test]
        public void NavigationGestureKeepsFirstThresholdCrossingDeltaAvailable()
        {
            var feel = new ESWorkbenchViewportFeelSettings(maximumPointerDeltaPerEvent: 100f);
            var session = new ESWorkbenchPointerGestureSession(feel.DragStartPixels);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan,
                5,
                Vector2.zero));
            Assert.IsTrue(session.TryStart(5, new Vector2(8f, 0f)));
            Assert.IsTrue(feel.TryConsumePointerDelta(
                session.StartPosition,
                new Vector2(8f, 0f),
                out Vector2 delta,
                out _));
            Assert.AreEqual(8f, delta.x, 0.0001f,
                "越过阈值的首个事件仍应提供完整平移增量。");
        }

        [Test]
        public void NavigationGestureStartsAndConsumesFirstEventThroughOneContract()
        {
            var feel = new ESWorkbenchViewportFeelSettings(maximumPointerDeltaPerEvent: 100f);
            var session = new ESWorkbenchPointerGestureSession(feel.DragStartPixels, feel);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan, 12, Vector2.zero));

            Assert.IsFalse(session.TryStartAndConsumePointerDelta(
                12, new Vector2(5.9f, 0f), out _, out _));
            Assert.IsFalse(session.IsStarted,
                "未越过阈值时统一入口不得提前进入 Started。");

            Assert.IsTrue(session.TryStartAndConsumePointerDelta(
                12, new Vector2(8f, 0f), out Vector2 first, out Vector2 consumed));
            Assert.IsTrue(session.IsStarted);
            Assert.AreEqual(8f, first.x, 0.0001f,
                "统一入口必须在首个越阈值事件内消费完整位移。");
            Assert.AreEqual(8f, consumed.x, 0.0001f);
        }

        [Test]
        public void PointerDeltaResolutionExposesCappedRemainderAndFinalConvergence()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                maximumPointerDeltaPerEvent: 5f);

            ESWorkbenchPointerDeltaResolution capped = feel.ResolvePointerDelta(
                new Vector2(10f, 10f),
                new Vector2(18f, 14f));
            Assert.IsTrue(capped.IsValid);
            Assert.IsTrue(capped.WasCapped);
            Assert.AreEqual(new Vector2(8f, 4f), capped.RawDelta);
            Assert.AreEqual(5f, capped.ConsumedDelta.magnitude, 0.0001f);
            Assert.AreEqual(capped.RawDelta,
                capped.ConsumedDelta + capped.RemainingDelta,
                "单事件限幅只能延迟消费，不能吞掉原始轨迹。");
            Assert.AreEqual(new Vector2(10f, 10f) + capped.ConsumedDelta,
                capped.ConsumedPointer);

            ESWorkbenchPointerDeltaResolution final = feel.ResolvePointerDelta(
                capped.ConsumedPointer,
                new Vector2(18f, 14f),
                capDelta: false);
            Assert.IsTrue(final.IsValid);
            Assert.IsFalse(final.WasCapped);
            Assert.AreEqual(Vector2.zero, final.RemainingDelta);
            Assert.AreEqual(new Vector2(18f, 14f), final.ConsumedPointer,
                "释放端点关闭限幅后必须精确收敛到当前指针。");
        }

        [Test]
        public void PointerDeltaResolutionRejectsInvalidInputWithoutInventingMovement()
        {
            var feel = ESWorkbenchViewportFeelSettings.Standard;
            Vector2 previous = new Vector2(3f, 4f);
            ESWorkbenchPointerDeltaResolution invalid = feel.ResolvePointerDelta(
                previous,
                new Vector2(float.NaN, 2f));

            Assert.IsFalse(invalid.IsValid);
            Assert.AreEqual(Vector2.zero, invalid.RawDelta);
            Assert.AreEqual(previous, invalid.ConsumedPointer);
            Assert.AreEqual(Vector2.zero, invalid.ConsumedDelta);
            Assert.AreEqual(Vector2.zero, invalid.RemainingDelta);
        }

        [Test]
        public void PointerGestureSessionConsumesCappedDeltasAndFinalEndpointWithoutDrift()
        {
            var feel = new ESWorkbenchViewportFeelSettings(maximumPointerDeltaPerEvent: 4f);
            var session = new ESWorkbenchPointerGestureSession(feel.DragStartPixels, feel);
            Assert.IsTrue(session.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Pan, 8, Vector2.zero));
            Assert.IsFalse(session.TryConsumePointerDelta(
                8, new Vector2(8f, 0f), out _, out _),
                "尚未跨过拖动阈值时不能提前消费轨迹，否则首个有效拖动事件会丢位移。");
            Assert.IsTrue(session.TryStart(8, new Vector2(6f, 0f)));

            Assert.IsTrue(session.TryConsumePointerDelta(
                8, new Vector2(20f, 0f), out Vector2 first, out Vector2 consumed));
            Assert.AreEqual(4f, first.x, 0.0001f);
            Assert.AreEqual(4f, consumed.x, 0.0001f);

            Assert.IsTrue(session.TryConsumePointerDelta(
                8, new Vector2(20f, 0f), out Vector2 second, out consumed));
            Assert.AreEqual(4f, second.x, 0.0001f);
            Assert.AreEqual(8f, consumed.x, 0.0001f);

            Assert.IsTrue(session.TryConsumePointerDeltaFinal(
                8, new Vector2(20f, 0f), out Vector2 finalDelta, out consumed));
            Assert.AreEqual(12f, finalDelta.x, 0.0001f);
            Assert.AreEqual(20f, consumed.x, 0.0001f,
                "释放端点必须无损收敛，不能因事件限幅留下视图偏移。");

            Assert.IsTrue(session.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit));
            Assert.IsFalse(session.TryConsumePointerDeltaFinal(
                8, new Vector2(24f, 0f), out _, out _));
        }

        [Test]
        public void MoveGestureAnchorPreservesGrabOffsetAndAppliesSnapAfterWorldDelta()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(anchor.Capture(
                new Vector3(20f, 3f, 30f),
                new Vector3(24f, 3f, 27f)));

            Assert.IsTrue(anchor.TryResolve(
                new Vector3(29.2f, 3f, 35.4f),
                value => new Vector3(
                    Mathf.Round(value.x),
                    value.y,
                    Mathf.Round(value.z)),
                out Vector3 resolved));
            Assert.AreEqual(new Vector3(25f, 3f, 38f), resolved,
                "对象必须保持按下点到对象原点的偏移，吸附只能作用于最终对象位置。");
            Assert.AreEqual(new Vector3(20f, 3f, 30f), anchor.ObjectStart);
            Assert.AreEqual(new Vector3(24f, 3f, 27f), anchor.PointerStart);
        }

        [Test]
        public void MoveGestureAnchorRebasesFirstDragFrameWithoutJumpingObject()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(anchor.Capture(new Vector3(20f, 3f, 30f), new Vector3(24f, 3f, 27f)));
            Assert.IsTrue(anchor.RebasePointer(new Vector3(27f, 3f, 31f)));
            Assert.AreEqual(new Vector3(20f, 3f, 30f), anchor.ObjectStart);
            Assert.AreEqual(new Vector3(27f, 3f, 31f), anchor.PointerStart);
            Assert.IsTrue(anchor.TryResolve(
                new Vector3(28f, 3f, 32f), null, ESWorkbenchMoveAxes.Horizontal, false,
                out Vector3 resolved));
            Assert.AreEqual(new Vector3(21f, 3f, 31f), resolved);
        }

        [Test]
        public void MoveGestureAnchorKeepsThresholdCrossingDelta()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(anchor.Capture(
                new Vector3(20f, 3f, 30f),
                new Vector3(24f, 3f, 27f)));

            // 首个越过拖动阈值的事件仍应按原始抓取锚点计算，不能丢弃这段位移。
            Assert.IsTrue(anchor.TryResolve(
                new Vector3(25f, 3f, 29f),
                null,
                ESWorkbenchMoveAxes.Horizontal,
                false,
                out Vector3 resolved));
            Assert.AreEqual(new Vector3(21f, 3f, 32f), resolved);
        }

        [Test]
        public void MoveGestureAnchorRejectsInvalidRebaseWithoutChangingAnchor()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(anchor.Capture(new Vector3(20f, 3f, 30f), new Vector3(24f, 3f, 27f)));
            Assert.IsFalse(anchor.RebasePointer(new Vector3(float.NaN, 3f, 31f)));
            Assert.AreEqual(new Vector3(20f, 3f, 30f), anchor.ObjectStart);
            Assert.AreEqual(new Vector3(24f, 3f, 27f), anchor.PointerStart);
        }

        [Test]
        public void MoveGestureAnchorRejectsInvalidProjectionAndResetRemovesOldGrabState()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsFalse(anchor.Capture(
                Vector3.zero,
                new Vector3(float.NaN, 0f, 0f)));
            Assert.IsFalse(anchor.TryResolve(Vector3.one, null, out _));
            Assert.IsTrue(anchor.Capture(Vector3.one, Vector3.zero));
            Assert.IsFalse(anchor.TryResolve(
                Vector3.right,
                _ => new Vector3(float.PositiveInfinity, 0f, 0f),
                out _));
            anchor.Reset();
            Assert.IsFalse(anchor.IsValid);
            Assert.IsFalse(anchor.TryResolve(Vector3.one, null, out _));
        }

        [Test]
        public void MoveGestureAnchorConstrainsPlaneAndKeepsDominantAxisStable()
        {
            var anchor = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(anchor.Capture(
                new Vector3(10f, 4f, 20f),
                new Vector3(12f, 8f, 18f)));

            Assert.IsTrue(anchor.TryResolve(
                new Vector3(18f, 100f, 21f),
                null,
                ESWorkbenchMoveAxes.Horizontal,
                true,
                out Vector3 first));
            Assert.AreEqual(new Vector3(16f, 4f, 20f), first,
                "主轴锁定必须忽略非平面轴，并选择首次有效位移的主导轴。");

            Assert.IsTrue(anchor.TryResolve(
                new Vector3(13f, -50f, 40f),
                null,
                ESWorkbenchMoveAxes.Horizontal,
                true,
                out Vector3 locked));
            Assert.AreEqual(new Vector3(11f, 4f, 20f), locked,
                "按住约束键期间不得随指针方向改变而在 X/Z 轴之间抖动切换。");

            Assert.IsTrue(anchor.TryResolve(
                new Vector3(13f, -50f, 40f),
                null,
                ESWorkbenchMoveAxes.Horizontal,
                false,
                out Vector3 released));
            Assert.AreEqual(new Vector3(11f, 4f, 42f), released,
                "释放约束后必须恢复平面内自由移动。 ");

            Assert.IsTrue(anchor.TryResolve(
                new Vector3(13f, -50f, 40f),
                null,
                ESWorkbenchMoveAxes.Horizontal,
                true,
                out Vector3 relocked));
            Assert.AreEqual(new Vector3(10f, 4f, 42f), relocked,
                "释放后重新按住约束键必须按当前位移重新选择主轴。");

            var stationary = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(stationary.Capture(Vector3.one, Vector3.zero));
            Assert.IsFalse(stationary.TryResolve(
                Vector3.zero,
                null,
                ESWorkbenchMoveAxes.Horizontal,
                true,
                out _),
                "没有有效世界位移时不得臆测 X 轴并产生粘滞锁定。");

            var snapped = new ESWorkbenchMoveGestureAnchor();
            Assert.IsTrue(snapped.Capture(
                new Vector3(10.25f, 4.2f, 20.4f),
                Vector3.zero));
            Assert.IsTrue(snapped.TryResolve(
                new Vector3(3.2f, 0f, 1f),
                value => new Vector3(
                    Mathf.Round(value.x),
                    Mathf.Round(value.y),
                    Mathf.Round(value.z)),
                ESWorkbenchMoveAxes.Horizontal,
                true,
                out Vector3 snappedAxis));
            Assert.AreEqual(new Vector3(13f, 4.2f, 20.4f), snappedAxis,
                "整体吸附后必须恢复未参与移动的轴，避免单轴拖动暗改其他坐标。");
        }

        [Test]
        public void ViewportFeelProfileKeepsCameraCanvasAndStrokeResponsesInjectable()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                dragStartPixels: 10f,
                orbitYawDegreesPerPixel: 0.5f,
                orbitPitchDegreesPerPixel: 0.4f,
                 cameraWheelDistanceScale: 0.5f,
                 cameraWheelZoomSensitivity: 0.02f,
                canvasWheelZoomSensitivity: 0.02f,
                strokeSpacingFactor: 0.2f,
                minimumStrokeSpacing: 0.1f,
                rotationDegreesPerPixel: 0.8f,
                scaleExponentPerPixel: 0.02f,
                maximumPointerDeltaPerEvent: 100f,
                maximumWheelDeltaPerEvent: 4f,
                edgePanSizePixels: 22f,
                edgePanMaximumPixelsPerSecond: 180f,
                edgePanResponseExponent: 1.5f,
                selectionHitRadiusPixels: 14f,
                minimumDropSpacing: 0.5f,
                previewCoalescingDelayMilliseconds: 48f,
                presentationRadiusScale: 3.4f);
            var drag = new ESWorkbenchPointerDragState(feel.DragStartPixels);
            var camera = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 0f, 30f, -80f, 80f, 1f, 100f, feel);
            var layout = new ESWorkbenchViewportLayoutState { viewportId = "feel-profile" };
            var canvas = new ESWorkbenchCanvasNavigationState(layout, 0.5f, 8f, 16f, feel);

            Assert.IsTrue(drag.Arm(1, Vector2.zero));
            Assert.IsFalse(drag.ShouldStart(1, new Vector2(7f, 0f)));
            Assert.IsTrue(drag.ShouldStart(1, new Vector2(10f, 0f)));
            camera.Orbit(new Vector2(2f, 5f));
            Assert.AreEqual(1f, camera.Yaw, 0.0001f);
            Assert.AreEqual(28f, camera.Pitch, 0.0001f);
            Assert.AreEqual(1.7f, feel.ResolveStrokeSpacing(8f), 0.0001f);
            Assert.AreEqual(0.1f, feel.ResolveStrokeSpacing(0f), 0.0001f);
            Assert.AreEqual(0.1f, feel.ResolveStrokeSpacing(float.NaN), 0.0001f);
            Assert.AreEqual(22f, feel.EdgePanSettings.EdgeSizePixels, 0.0001f);
            Assert.AreEqual(180f, feel.EdgePanSettings.MaximumPanPixelsPerSecond, 0.0001f);
            Assert.AreEqual(1.5f, feel.EdgePanSettings.ResponseExponent, 0.0001f);
            Assert.AreEqual(14f, feel.SelectionHitRadiusPixels, 0.0001f);
            Assert.AreEqual(0.5f, feel.MinimumDropSpacing, 0.0001f);
            Assert.AreEqual(48f, feel.PreviewCoalescingDelayMilliseconds, 0.0001f);
            Assert.AreEqual(3.4f, feel.PresentationRadiusScale, 0.0001f);
            Assert.AreEqual(8.4f, feel.ResolveMarkerRadiusPixels(false, false), 0.0001f);
            Assert.AreEqual(9.9f, feel.ResolveMarkerRadiusPixels(false, true), 0.0001f);
            Assert.AreEqual(10.9f, feel.ResolveMarkerRadiusPixels(true, false), 0.0001f);
            Assert.AreEqual(new Vector2(60f, 80f),
                feel.NormalizePointerDelta(new Vector2(120f, 160f)));
            Assert.AreEqual(Vector2.zero,
                feel.NormalizePointerDelta(new Vector2(float.NaN, 1f)));

            canvas.ZoomAt(new Vector2(320f, 180f), 2f,
                new Rect(0f, 0f, 640f, 360f), new Rect(0f, 0f, 100f, 100f));
            Assert.AreEqual(Mathf.Exp(-0.04f), layout.zoom, 0.0001f);

            camera.Zoom(1000f);
            Assert.AreEqual(10f * Mathf.Exp(4f * 0.02f * 0.5f), camera.Distance, 0.0001f,
                "高分辨率滚轮或触控板单事件不得让 3D 相机距离发生数量级跳变。");
            canvas.Reset();
            canvas.ZoomAt(new Vector2(320f, 180f), 1000f,
                new Rect(0f, 0f, 640f, 360f), new Rect(0f, 0f, 100f, 100f));
            Assert.AreEqual(Mathf.Exp(-4f * 0.02f), layout.zoom, 0.0001f,
                "2D 和 3D 必须复用同一滚轮事件上限。");

            var invalidFeel = new ESWorkbenchViewportFeelSettings(
                dragStartPixels: float.NaN,
                orbitYawDegreesPerPixel: float.PositiveInfinity,
                canvasWheelZoomSensitivity: float.NaN,
                minimumStrokeSpacing: float.PositiveInfinity,
                maximumPointerDeltaPerEvent: float.NaN,
                maximumWheelDeltaPerEvent: float.NaN,
                selectionHitRadiusPixels: float.PositiveInfinity,
                previewCoalescingDelayMilliseconds: float.PositiveInfinity,
                presentationRadiusScale: float.NaN);
            Assert.AreEqual(6f, invalidFeel.DragStartPixels, 0.0001f);
            Assert.AreEqual(0.35f, invalidFeel.OrbitYawDegreesPerPixel, 0.0001f);
            Assert.AreEqual(0.035f, invalidFeel.CanvasWheelZoomSensitivity, 0.0001f);
            Assert.AreEqual(0.35f, ESWorkbenchViewportFeelSettings.Standard.CanvasMinimumZoom, 0.0001f);
            Assert.AreEqual(12f, ESWorkbenchViewportFeelSettings.Standard.CanvasMaximumZoom, 0.0001f);
            Assert.AreEqual(16f, ESWorkbenchViewportFeelSettings.Standard.CanvasViewportPaddingPixels, 0.0001f);
            Assert.AreEqual(0.25f, invalidFeel.MinimumStrokeSpacing, 0.0001f);
            Assert.AreEqual(160f, invalidFeel.MaximumPointerDeltaPerEvent, 0.0001f);
            Assert.AreEqual(4f, invalidFeel.MaximumWheelDeltaPerEvent, 0.0001f);
            Assert.AreEqual(32f, invalidFeel.SelectionHitRadiusPixels, 0.0001f);
            Assert.AreEqual(32f, invalidFeel.PreviewCoalescingDelayMilliseconds, 0.0001f);
            Assert.AreEqual(2.8f, invalidFeel.PresentationRadiusScale, 0.0001f);
            Assert.Zero(invalidFeel.NormalizeWheelDelta(float.PositiveInfinity));
        }

        [Test]
        public void ViewportFeelGroupsAndPresetsKeepResponseSemanticsDiscoverable()
        {
            ESWorkbenchViewportFeelSettings standard =
                ESWorkbenchViewportFeelSettings.CreatePreset(
                    ESWorkbenchViewportFeelPreset.Standard);
            ESWorkbenchViewportFeelSettings precision =
                ESWorkbenchViewportFeelSettings.CreatePreset(
                    ESWorkbenchViewportFeelPreset.Precision);
            ESWorkbenchViewportFeelSettings rapid =
                ESWorkbenchViewportFeelSettings.CreatePreset(
                    ESWorkbenchViewportFeelPreset.RapidAuthoring);

            Assert.AreSame(ESWorkbenchViewportFeelSettings.Standard, standard);
            Assert.AreEqual(standard.DragStartPixels, standard.Pointer.DragStartPixels);
            Assert.AreEqual(standard.EdgePanSettings, standard.Navigation.EdgePanSettings);
            Assert.AreEqual(standard.MinimumStrokeSpacing, standard.Authoring.MinimumStrokeSpacing);
            Assert.AreEqual(2.8f, standard.PresentationRadiusScale, 0.0001f);
            Assert.AreEqual(standard.PreviewCoalescingDelayMilliseconds,
                standard.Preview.CoalescingDelayMilliseconds);
            Assert.Less(precision.Navigation.EdgePanSettings.MaximumPanPixelsPerSecond,
                rapid.Navigation.EdgePanSettings.MaximumPanPixelsPerSecond);
            Assert.Less(precision.Authoring.MinimumStrokeSpacing,
                rapid.Authoring.MinimumStrokeSpacing);
            Assert.Less(precision.Pointer.MaximumPointerDeltaPerEvent,
                rapid.Pointer.MaximumPointerDeltaPerEvent);
        }

        [Test]
        public void OrbitCameraZoomAtKeepsPointerAnchorAndCenterStable()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                cameraWheelDistanceScale: 0.5f,
                cameraWheelZoomSensitivity: 0.02f);
            var camera = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 0f, 0f, -80f, 80f, 1f, 100f, feel);
            Rect viewport = new Rect(0f, 0f, 100f, 100f);

            camera.ZoomAt(viewport.center, viewport, -4f);
            Assert.AreEqual(0f, camera.Focus.x, 0.0001f,
                "中心滚轮缩放不应偷偷改变轨道焦点。");
            Assert.AreEqual(0f, camera.Focus.y, 0.0001f);

            camera.SetView(Vector3.zero, 10f, 0f, 0f);
            camera.ZoomAt(new Vector2(75f, 50f), viewport, -4f);
            Assert.Less(camera.Distance, 10f);
            Assert.Greater(camera.Focus.x, 0f,
                "偏右指针缩放时，轨道焦点应向指针方向补偿，避免内容向中心漂移。");
            Assert.AreEqual(0f, camera.Focus.y, 0.0001f);

            float focusBeforeInvalid = camera.Focus.x;
            float distanceBeforeInvalid = camera.Distance;
            camera.ZoomAt(new Vector2(float.NaN, 50f), viewport, -4f);
            Assert.AreEqual(focusBeforeInvalid, camera.Focus.x, 0.0001f);
            Assert.AreEqual(distanceBeforeInvalid, camera.Distance, 0.0001f);
        }

        [Test]
        public void OrbitCameraZoomAtKeepsAnchorAcrossAspectAndPresentationRadius()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                verticalFieldOfViewDegrees: 60f,
                cameraWheelDistanceScale: 0.5f,
                cameraWheelZoomSensitivity: 0.02f);
            var camera = new ESWorkbenchOrbitCameraState(
                new Vector3(4f, 2f, -3f),
                10f,
                28f,
                22f,
                -80f,
                80f,
                1f,
                100f,
                feel,
                presentationRadiusScale: 2f);
            Rect viewport = new Rect(0f, 0f, 480f, 960f);
            Vector2 pointer = new Vector2(96f, 672f);

            float previousDistance = camera.ResolvePresentationCameraDistance(
                viewport, 60f);
            Quaternion rotation = Quaternion.Euler(camera.Pitch, camera.Yaw, 0f);
            float halfHeight = Mathf.Tan(30f * Mathf.Deg2Rad);
            float aspect = viewport.width / viewport.height;
            Vector2 normalized = new Vector2(
                ((pointer.x - viewport.xMin) / viewport.width) * 2f - 1f,
                1f - ((pointer.y - viewport.yMin) / viewport.height) * 2f);
            Vector3 ray = rotation * new Vector3(
                normalized.x * halfHeight * aspect,
                normalized.y * halfHeight,
                1f).normalized;
            Vector3 view = rotation * Vector3.forward;
            Vector3 cameraPosition = camera.Focus + rotation * Vector3.back * previousDistance;
            float cosine = Vector3.Dot(ray, view);
            Vector3 before = cameraPosition + ray * (previousDistance / cosine);

            camera.ZoomAt(pointer, viewport, -4f, 60f);

            float nextDistance = camera.ResolvePresentationCameraDistance(viewport, 60f);
            Vector3 nextCameraPosition = camera.Focus + rotation * Vector3.back * nextDistance;
            Vector3 after = nextCameraPosition + ray * (nextDistance / cosine);
            Assert.That((after - before).magnitude, Is.LessThan(0.0001f),
                "窄纵横比与表示半径换算下，滚轮仍必须保持指针下方的视线锚点。");
        }

        [Test]
        public void CanvasZoomAtKeepsPointerAnchorInViewportPadding()
        {
            var layout = new ESWorkbenchViewportLayoutState { viewportId = "padding-zoom" };
            var canvas = new ESWorkbenchCanvasNavigationState(
                layout,
                minimumZoom: 0.35f,
                maximumZoom: 12f,
                viewportPadding: 16f,
                feel: new ESWorkbenchViewportFeelSettings(canvasWheelZoomSensitivity: 0.02f));
            Rect viewport = new Rect(0f, 0f, 640f, 360f);
            Rect world = new Rect(0f, 0f, 100f, 100f);
            Vector2 pointer = new Vector2(8f, viewport.center.y);
            Rect before = canvas.ResolveCanvasBounds(viewport, world);
            float normalizedX = (pointer.x - before.xMin) / before.width;

            canvas.ZoomAt(pointer, 2f, viewport, world);

            Rect after = canvas.ResolveCanvasBounds(viewport, world);
            float anchoredX = after.xMin + after.width * normalizedX;
            Assert.AreEqual(pointer.x, anchoredX, 0.0001f,
                "视口留白区滚轮必须保持指针锚点，不能把内容吸到画布边缘。");
        }

        [Test]
        public void EdgePanControllerIsBoundedDirectionalAndInvalidInputSafe()
        {
            var controller = new ESWorkbenchEdgePanController(
                new ESWorkbenchEdgePanSettings(20f, 100f, 2f));
            Rect viewport = new Rect(0f, 0f, 100f, 100f);

            Assert.IsFalse(controller.Evaluate(viewport, new Vector2(50f, 50f), 0.1f, out _));
            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(1f, 50f), 0.1f, out Vector2 left));
            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(99f, 50f), 0.1f, out Vector2 right));
            Assert.Greater(left.x, 0f);
            Assert.Less(right.x, 0f);
            Assert.AreEqual(0f, left.y, 0.0001f);

            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(1f, 1f), 0.1f, out Vector2 diagonal));
            Assert.Greater(diagonal.x, 0f);
            Assert.Greater(diagonal.y, 0f);
            Assert.LessOrEqual(diagonal.magnitude, 10f + 0.0001f,
                "角落对角线边缘平移的合速度不得超过最大配置速度。");
            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(15f, 50f), 0.1f, out Vector2 inner));
            Assert.Greater(Mathf.Abs(left.x), Mathf.Abs(inner.x), "越靠近边缘，平移速度必须越快。");
            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(-100f, 50f), 0.1f, out Vector2 outside));
            Assert.AreEqual(10f, Mathf.Abs(outside.x), 0.0001f, "视口外必须封顶为最大速度。");
            Assert.IsTrue(controller.Evaluate(viewport, new Vector2(-100f, 50f), 10f, out Vector2 stalled));
            Assert.AreEqual(10f, Mathf.Abs(stalled.x), 0.0001f,
                "调度暂停恢复时必须按最大时间步推进，不能瞬移数秒的边缘距离。");

            Assert.IsFalse(controller.Evaluate(viewport, new Vector2(float.NaN, 50f), 0.1f, out _));
            Assert.IsFalse(controller.Evaluate(viewport, new Vector2(1f, 50f), 0f, out _));
            Assert.IsFalse(controller.Evaluate(new Rect(0f, 0f, 1f, 100f), new Vector2(0f, 50f), 0.1f, out _));

            var invalid = new ESWorkbenchEdgePanSettings(float.NaN, float.PositiveInfinity, 0f);
            Assert.AreEqual(48f, invalid.EdgeSizePixels, 0.0001f);
            Assert.AreEqual(420f, invalid.MaximumPanPixelsPerSecond, 0.0001f);
            Assert.AreEqual(1f, invalid.ResponseExponent, 0.0001f);
        }

        [Test]
        public void EdgePanControllerKeepsNarrowViewportCenterNeutral()
        {
            var controller = new ESWorkbenchEdgePanController(
                new ESWorkbenchEdgePanSettings(48f, 100f, 2f));
            Rect narrowViewport = new Rect(0f, 0f, 30f, 30f);

            Assert.IsFalse(controller.Evaluate(
                narrowViewport, narrowViewport.center, 0.1f, out Vector2 center),
                "窄视口中心不能因为边缘区重叠而自动平移。");
            Assert.AreEqual(Vector2.zero, center);
            Assert.IsTrue(controller.Evaluate(
                narrowViewport, new Vector2(1f, 15f), 0.1f, out Vector2 left));
            Assert.IsTrue(controller.Evaluate(
                narrowViewport, new Vector2(29f, 15f), 0.1f, out Vector2 right));
            Assert.Greater(left.x, 0f);
            Assert.Less(right.x, 0f);
        }

        [Test]
        public void EdgePanSessionSharesPointerLockAndBoundedClockSemantics()
        {
            var session = new ESWorkbenchEdgePanSession();

            Assert.IsFalse(session.IsActive);
            Assert.IsTrue(session.Begin(new Vector2(3f, 4f), true, 10d));
            Assert.IsTrue(session.IsActive);
            Assert.AreEqual(new Vector2(3f, 4f), session.Pointer);
            Assert.IsTrue(session.LockDominantAxis);
            Assert.IsTrue(session.TryAdvance(10.02d, out float deltaTime));
            Assert.That(deltaTime, Is.EqualTo(0.02f).Within(0.0001f));

            Assert.IsTrue(session.TryAdvance(9.5d, out float reversedTimeDelta));
            Assert.AreEqual(
                ESWorkbenchInputClock.MinimumDeltaTime,
                reversedTimeDelta,
                0.0001f);
            Assert.AreEqual(
                10.02d,
                session.LastTimestamp,
                "倒退时间不能污染边缘平移的单调时间基准。");
            Assert.IsTrue(session.TryAdvance(10.05d, out float recoveredDelta));
            Assert.That(
                recoveredDelta,
                Is.EqualTo(0.03f).Within(0.0001f),
                "时钟恢复后必须从最后一个有效时间继续，不得误触发长帧跳动。");

            Assert.IsTrue(session.UpdatePointer(new Vector2(7f, 8f), false));
            Assert.AreEqual(new Vector2(7f, 8f), session.Pointer);
            Assert.IsFalse(session.LockDominantAxis);
            Assert.IsFalse(session.UpdatePointer(
                new Vector2(float.NaN, 1f), true));
            Assert.IsFalse(session.TryAdvance(double.NaN, out _));
            Assert.IsTrue(session.Stop());
            Assert.IsFalse(session.IsActive);
            Assert.IsFalse(session.Stop(), "重复释放边缘会话必须幂等。");
        }

        [Test]
        public void InputClockClampsLongFramesAndRepairsInvalidTimestampOrder()
        {
            Assert.AreEqual(0.05f,
                ESWorkbenchInputClock.ResolveDeltaTime(10d, 10.05d), 0.0001f);
            Assert.AreEqual(ESWorkbenchInputClock.MaximumDeltaTime,
                ESWorkbenchInputClock.ResolveDeltaTime(10d, 20d), 0.0001f);
            Assert.AreEqual(ESWorkbenchInputClock.MinimumDeltaTime,
                ESWorkbenchInputClock.ResolveDeltaTime(10d, 9d), 0.0001f);
            Assert.AreEqual(ESWorkbenchInputClock.MinimumDeltaTime,
                ESWorkbenchInputClock.ResolveDeltaTime(double.NaN, 10d), 0.0001f);
            Assert.AreEqual(ESWorkbenchInputClock.MinimumDeltaTime,
                ESWorkbenchInputClock.ResolveDeltaTime(10d, double.PositiveInfinity), 0.0001f);
        }

        [Test]
        public void OrbitCameraPanOverloadUsesConfiguredFieldOfView()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                verticalFieldOfViewDegrees: 60f,
                panWorldPerPixelAtDistance: 0.01f);
            var implicitFov = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 20f, 15f, feel: feel);
            var explicitFov = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 20f, 15f, feel: feel);

            implicitFov.Pan(new Vector2(12f, -8f));
            explicitFov.Pan(new Vector2(12f, -8f), default, 60f);
            Assert.AreEqual(explicitFov.Focus, implicitFov.Focus,
                "无视口重载必须沿用同一手感配置的 FOV，不能退化为 1 度投影。");

            var implicitViewportFov = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 20f, 15f, feel: feel);
            var explicitViewportFov = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 20f, 15f, feel: feel);
            Rect viewport = new Rect(0f, 0f, 640f, 360f);
            implicitViewportFov.Pan(new Vector2(12f, -8f), viewport);
            explicitViewportFov.Pan(new Vector2(12f, -8f), viewport, 60f);
            Assert.AreEqual(explicitViewportFov.Focus, implicitViewportFov.Focus,
                "带视口重载省略 FOV 时也必须回到配置值，不能偷偷回退到固定常量。");
            Assert.AreEqual(60f, feel.VerticalFieldOfViewDegrees, 0.0001f);
        }

        [Test]
        public void OrbitCameraPresentationDistanceMatchesPreviewProjectionContract()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                verticalFieldOfViewDegrees: 60f);
            var camera = new ESWorkbenchOrbitCameraState(
                Vector3.zero,
                10f,
                20f,
                15f,
                -80f,
                80f,
                1f,
                100f,
                feel,
                presentationRadiusScale: 2f);

            float square = camera.ResolvePresentationCameraDistance(
                new Rect(0f, 0f, 100f, 100f));
            Assert.AreEqual(10f, square, 0.0001f,
                "状态距离必须先还原为 pose 半径，再按 FOV 得到实际相机距离。");
            Assert.AreEqual(5f, camera.ResolvePresentationRadius(), 0.0001f,
                "渲染宿主必须从公共状态解析 pose 半径，不能复制领域比例常量。");
            Assert.AreEqual(2.5f, camera.ResolvePresentationRadius(5f), 0.0001f,
                "按指定状态距离解析半径必须沿用同一表示比例。");

            float narrow = camera.ResolvePresentationCameraDistance(
                new Rect(0f, 0f, 50f, 100f));
            Assert.Greater(narrow, square,
                "窄视口的水平 FOV 更小，实际相机距离必须增大以保持内容完整可见。");
            Assert.AreEqual(5f, camera.ResolvePresentationCameraDistance(
                default, 60f), 0.0001f,
                "无效视口仍应返回去除表示比例后的稳定状态距离。");

            var directCamera = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 10f, 0f, 0f, feel: feel);
            Assert.AreEqual(10f, directCamera.ResolvePresentationCameraDistance(
                new Rect(0f, 0f, 100f, 100f)), 0.0001f,
                "未注入预览表示比例时，状态距离必须保持直接相机距离语义。");
            Assert.AreEqual(10f, directCamera.ResolvePresentationRadius(), 0.0001f,
                "未启用表示比例时，预览半径必须保持状态距离语义。");

            Rect projection = new Rect(0f, 0f, 1280f, 720f);
            float current = camera.ResolvePresentationCameraDistance(projection, 60f);
            float explicitState = camera.ResolvePresentationCameraDistance(
                camera.Distance, projection, 60f);
            Assert.AreEqual(current, explicitState, 0.0001f,
                "外部 Camera 同步必须复用轨道相同的 FOV/纵横比换算。");
            Assert.Greater(
                camera.ResolvePresentationCameraDistance(1f, projection, 60f),
                0f);

            var bound = new ESWorkbenchOrbitCameraState(
                Vector3.zero,
                1f,
                0f,
                0f,
                feel: feel,
                presentationRadiusScale: 2f);
            Vector3 externalPosition = new Vector3(4f, 2f, -3f);
            Quaternion externalRotation = Quaternion.Euler(18f, 42f, 0f);
            Assert.IsTrue(ESWorkbenchOrbitCameraBinding.TryCaptureExternalCamera(
                bound,
                externalPosition,
                externalRotation,
                1f,
                projection,
                60f));
            Assert.IsTrue(ESWorkbenchOrbitCameraBinding.TryApplyToExternalCamera(
                bound,
                projection,
                out Vector3 reboundPosition,
                out Quaternion reboundRotation,
                60f));
            Assert.That((reboundPosition - externalPosition).magnitude, Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(reboundRotation, externalRotation), Is.LessThan(0.001f));

            var distanceBound = new ESWorkbenchOrbitCameraState(
                Vector3.zero,
                1f,
                0f,
                0f,
                feel: feel,
                presentationRadiusScale: 2f);
            Assert.IsTrue(ESWorkbenchOrbitCameraBinding.TryCaptureExternalCameraAtDistance(
                distanceBound,
                externalPosition,
                externalRotation,
                6f,
                projection,
                60f));
            Assert.IsTrue(ESWorkbenchOrbitCameraBinding.TryApplyToExternalCamera(
                distanceBound,
                projection,
                out Vector3 distanceReboundPosition,
                out Quaternion distanceReboundRotation,
                60f));
            Assert.That((distanceReboundPosition - externalPosition).magnitude, Is.LessThan(0.001f),
                "真实 Camera 距离捕获必须保持首次构图，不得被状态标定压缩。");
            Assert.That(Quaternion.Angle(distanceReboundRotation, externalRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void OrbitCameraPresentationDistanceInversePreservesExternalFraming()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                verticalFieldOfViewDegrees: 60f);
            var camera = new ESWorkbenchOrbitCameraState(
                Vector3.zero,
                10f,
                20f,
                15f,
                -80f,
                80f,
                0.01f,
                100f,
                feel,
                presentationRadiusScale: 2.8f);
            Rect viewport = new Rect(0f, 0f, 1440f, 720f);
            const float actualDistance = 6f;

            float stateDistance = camera.ResolveStateDistanceForPresentationCameraDistance(
                actualDistance,
                viewport,
                60f);
            float reboundDistance = camera.ResolvePresentationCameraDistance(
                stateDistance,
                viewport,
                60f);

            Assert.That(reboundDistance, Is.EqualTo(actualDistance).Within(0.0001f),
                "外部 Camera 捕获前反解状态距离，再投影回去必须保持原始构图距离。");
            Assert.That(stateDistance, Is.GreaterThan(0f),
                "反解结果必须仍是正的公共轨道状态距离。");
        }

        [Test]
        public void ViewportInteractionRectExcludesToolbarFromEdgePanZone()
        {
            Rect render = new Rect(10f, 20f, 200f, 120f);
            Rect interaction = ESWorkbenchViewportOverlay.GetInteractionRect(render);
            var controller = new ESWorkbenchEdgePanController(
                new ESWorkbenchEdgePanSettings(20f, 100f, 2f));

            Assert.IsFalse(interaction.Contains(new Vector2(110f, 25f)),
                "三维工具栏区域不能被视为场景交互区。");
            Assert.IsFalse(ESWorkbenchViewportOverlay.AllowsEdgePanPointer(
                render, interaction, new Vector2(110f, 25f)));
            Assert.IsTrue(ESWorkbenchViewportOverlay.AllowsEdgePanPointer(
                render, interaction, new Vector2(110f, -5f)),
                "拖出视口后仍允许连续边缘平移。");
            Assert.IsTrue(controller.Evaluate(
                interaction, new Vector2(110f, interaction.yMin + 1f), 0.1f, out Vector2 contentTop));
            Assert.Greater(contentTop.y, 0f);
        }

        [Test]
        public void DropPointPolicyMatchesPreviewCommitBoundary()
        {
            Rect interaction = new Rect(10f, 30f, 200f, 120f);

            Assert.IsTrue(ESWorkbenchDropPointPolicy.CanCommit(
                interaction, new Vector2(110f, 90f)));
            Assert.IsTrue(ESWorkbenchDropPointPolicy.CanCommit(
                interaction, new Vector2(10.01f, 30.01f)),
                "地图留白或边界夹取由投影负责，屏幕交互矩形内的落点必须可提交。");
            Assert.IsTrue(ESWorkbenchDropPointPolicy.CanCommit(
                interaction, new Vector2(interaction.xMax, interaction.yMax)),
                "交互矩形右下边界必须是闭区间，避免最后一像素释放失败。");
            Assert.IsFalse(ESWorkbenchDropPointPolicy.CanCommit(
                interaction, new Vector2(110f, 25f)),
                "工具栏区域不能成为正式拖放落点。");
            Assert.IsFalse(ESWorkbenchDropPointPolicy.CanCommit(
                new Rect(0f, 0f, 1f, 100f), new Vector2(0.5f, 50f)));
            Assert.IsFalse(ESWorkbenchDropPointPolicy.CanCommit(
                interaction, new Vector2(float.NaN, 90f)));
            Assert.IsTrue(ESWorkbenchDropPointPolicy.IsFinite(new Vector3(1f, 2f, 3f)));
            Assert.IsFalse(ESWorkbenchDropPointPolicy.IsFinite(
                new Vector3(float.PositiveInfinity, 2f, 3f)));
        }

        [Test]
        public void ProjectionIntentPolicySeparatesStrictHitPaintDropAndEdgePan()
        {
            ESWorkbenchViewportProjectionRequest hit =
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.AuthorHit);
            Assert.IsFalse(hit.AllowOutside);
            Assert.IsTrue(hit.RequireInteractionBoundary);
            Assert.IsTrue(hit.ClampToWorld);
            Assert.IsFalse(hit.RequireTerrainSurface);

            ESWorkbenchViewportProjectionRequest paint =
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.TerrainPaint);
            Assert.IsFalse(paint.AllowOutside);
            Assert.IsTrue(paint.RequireInteractionBoundary);
            Assert.IsTrue(paint.RequireTerrainSurface,
                "地形绘制必须严格命中高度场，不能退化为平面命中。");
            Assert.IsFalse(paint.ClampToWorld);

            ESWorkbenchViewportProjectionRequest drop =
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.DropPreview,
                    requireTerrainSurface: true);
            Assert.IsTrue(drop.AllowOutside);
            Assert.IsTrue(drop.RequireInteractionBoundary);
            Assert.IsTrue(drop.RequireTerrainSurface);
            Assert.IsTrue(drop.ClampToWorld);

            ESWorkbenchViewportProjectionRequest edge =
                ESWorkbenchViewportProjectionRequest.For(
                    ESWorkbenchViewportProjectionIntent.EdgePanPreview);
            Assert.IsTrue(edge.AllowOutside);
            Assert.IsFalse(edge.RequireInteractionBoundary);
            Assert.IsTrue(edge.ClampToWorld);
            Assert.IsFalse(edge.RequireTerrainSurface);
        }

        [Test]
        public void DropPreviewRefreshPolicyUsesStableGeometryAndStateTolerance()
        {
            ESWorkbenchDropPreviewState allowed = ESWorkbenchDropPreviewState.Allowed;
            Assert.IsTrue(ESWorkbenchDropPreviewRefreshPolicy.IsEquivalent(
                Vector3.zero,
                new Vector3(0.00005f, 0f, 0f),
                1,
                1,
                2f,
                2.00005f,
                Vector3.one,
                new Vector3(1.00005f, 1f, 1f),
                allowed,
                allowed),
                "同一视觉落点内的浮点抖动不应触发重复重绘。");
            Assert.IsFalse(ESWorkbenchDropPreviewRefreshPolicy.IsEquivalent(
                Vector3.zero,
                Vector3.zero,
                1,
                1,
                2f,
                2f,
                Vector3.one,
                Vector3.one,
                allowed,
                allowed,
                previousSnapEnabled: true,
                nextSnapEnabled: true,
                previousSnapStep: 1f,
                nextSnapStep: 2f));
            Assert.IsFalse(ESWorkbenchDropPreviewRefreshPolicy.IsEquivalent(
                Vector3.zero,
                Vector3.zero,
                1,
                1,
                2f,
                2f,
                Vector3.one,
                Vector3.one,
                allowed,
                ESWorkbenchDropPreviewState.RejectedBy("锁定")));
            Assert.IsFalse(ESWorkbenchDropPreviewRefreshPolicy.IsEquivalent(
                Vector3.zero,
                Vector3.zero,
                1,
                1,
                2f,
                2f,
                Vector3.one,
                Vector3.one,
                allowed,
                allowed,
                float.NaN));
        }

        [Test]
        public void CanvasNavigationConstrainsPanWithoutHardLockingSmallContent()
        {
            var layout = new ESWorkbenchViewportLayoutState { viewportId = "pan-constraint" };
            var navigation = new ESWorkbenchCanvasNavigationState(layout, 0.25f, 8f, 0f);
            Rect viewport = new Rect(0f, 0f, 100f, 100f);
            Rect worldBounds = new Rect(0f, 0f, 100f, 100f);

            navigation.PanBy(new Vector2(1000f, -1000f));
            navigation.ConstrainPan(viewport, worldBounds, 12f);
            Rect canvas = navigation.ResolveCanvasBounds(viewport, worldBounds);
            Assert.Less(canvas.xMin, viewport.xMax);
            Assert.Greater(canvas.xMax, viewport.xMin);
            Assert.Less(canvas.yMin, viewport.yMax);
            Assert.Greater(canvas.yMax, viewport.yMin);

            navigation.Reset();
            navigation.ZoomAt(viewport.center, -4f, viewport, worldBounds);
            navigation.PanBy(new Vector2(1000f, 1000f));
            navigation.ConstrainPan(viewport, worldBounds, 12f);
            canvas = navigation.ResolveCanvasBounds(viewport, worldBounds);
            Assert.LessOrEqual(canvas.xMin, viewport.xMax - 12f + 0.001f);
            Assert.GreaterOrEqual(canvas.xMax, viewport.xMin + 12f - 0.001f);
            Assert.IsFalse(float.IsNaN(layout.pan.x));
            Assert.IsFalse(float.IsInfinity(layout.pan.y));
        }

        [Test]
        public void TransformGestureResolverKeepsRotationAndScaleFiniteAndConsistent()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                scaleExponentPerPixel: 0.1f,
                minimumTransformScale: 0.25f,
                maximumTransformScale: 4f);
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolve(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(10f, 100f),
                new Vector3(0f, 30f, 0f),
                feel,
                value => value,
                out Vector3 rotation));
            Assert.AreEqual(40f, rotation.y, 0.0001f);

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolve(
                ESWorkbenchMutationKind.Scale,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.one,
                feel,
                value => value,
                out Vector3 scale));
            Assert.AreEqual(4f, scale.x, 0.0001f, "缩放必须遵守统一上限。");
            Assert.IsFalse(ESWorkbenchTransformGestureResolver.TryResolve(
                ESWorkbenchMutationKind.Scale,
                new Vector2(float.NaN, 0f),
                Vector2.zero,
                Vector3.one,
                feel,
                value => value,
                out _));
        }

        [Test]
        public void IncrementalTransformResolverCapsEventsWithoutTruncatingLongGestures()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                scaleExponentPerPixel: 0.1f,
                maximumPointerDeltaPerEvent: 10f,
                minimumTransformScale: 0.01f,
                maximumTransformScale: 10000f);

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.zero,
                feel,
                null,
                out Vector3 rotation));
            Assert.AreEqual(10f, rotation.y, 0.0001f,
                "旋转必须复用统一的单事件位移上限，避免高 DPI 首帧跳转。");

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Scale,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.one,
                feel,
                null,
                out Vector3 scale));
            Assert.AreEqual(Mathf.Exp(1f), scale.x, 0.0001f,
                "缩放指数必须基于归一化位移，而不是原始事件尖峰。");

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                new Vector2(100f, 0f),
                new Vector2(200f, 0f),
                rotation,
                feel,
                null,
                out Vector3 continued));
            Assert.AreEqual(20f, continued.y, 0.0001f,
                "后续事件必须在上一结果上继续累计，而不是被总位移上限截断。");

            Func<Vector3, Vector3> snapRotation = value =>
                new Vector3(0f, Mathf.Round(value.y / 15f) * 15f, 0f);
            Vector3 accumulated = Vector3.zero;
            Vector3 snapped = Vector3.zero;
            Vector2 previousPointer = Vector2.zero;
            for (int i = 1; i <= 20; i++)
            {
                Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                    ESWorkbenchMutationKind.Rotate,
                    previousPointer,
                    new Vector2(i, 0f),
                    accumulated,
                    feel,
                    snapRotation,
                    out snapped,
                    out accumulated));
                previousPointer = new Vector2(i, 0f);
            }
            Assert.AreEqual(15f, snapped.y, 0.0001f,
                "吸附预览必须保留未吸附累计值，不能因每个小事件重新吸附而卡死。");

            var boundedFeel = new ESWorkbenchViewportFeelSettings(
                scaleExponentPerPixel: 0.1f,
                maximumPointerDeltaPerEvent: 10f,
                minimumTransformScale: 0.5f,
                maximumTransformScale: 2f);
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Scale,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.one,
                boundedFeel,
                null,
                out Vector3 bounded,
                out Vector3 boundedAccumulated));
            Assert.AreEqual(Vector3.one * 2f, bounded);
            Assert.AreEqual(Vector3.one * 2f, boundedAccumulated,
                "缩放上限必须同时约束累计值，反向拖动不能先穿过不可见的超限区。");
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Scale,
                new Vector2(100f, 0f),
                new Vector2(90f, 0f),
                boundedAccumulated,
                boundedFeel,
                null,
                out Vector3 reversed,
                out _));
            Assert.Less(reversed.x, 2f, "到达缩放上限后，反向拖动应立即产生可见变化。");
        }

        [Test]
        public void TransformThresholdCrossingConsumesTheFirstVisibleDelta()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                dragStartPixels: 6f,
                rotationDegreesPerPixel: 1f);
            var gesture = new ESWorkbenchPointerGestureSession(feel.DragStartPixels, feel);
            Assert.IsTrue(gesture.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Transform,
                0,
                Vector2.zero));
            Assert.IsFalse(gesture.TryStart(0, new Vector2(5.9f, 0f)));
            Assert.IsTrue(gesture.TryStart(0, new Vector2(8f, 0f)));
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                gesture.StartPosition,
                new Vector2(8f, 0f),
                Vector3.zero,
                feel,
                null,
                out Vector3 rotation,
                out _,
                out Vector2 consumedPointer));
            Assert.AreEqual(8f, rotation.y, 0.0001f,
                "越过阈值的首帧位移不能被适配器丢弃，否则旋转/缩放会产生明显迟滞。");
            Assert.AreEqual(new Vector2(8f, 0f), consumedPointer);
        }

        [Test]
        public void GestureEnsureStartedKeepsArmedUntilThresholdThenRemainsStarted()
        {
            var gesture = new ESWorkbenchPointerGestureSession(6f);
            Assert.IsTrue(gesture.TryArm(
                ESWorkbenchPointerGestureSession.Kind.Transform,
                3,
                Vector2.zero));

            Assert.IsFalse(gesture.TryEnsureStarted(3, new Vector2(5.9f, 0f)));
            Assert.IsTrue(gesture.IsActive);
            Assert.IsFalse(gesture.IsStarted);

            Assert.IsTrue(gesture.TryEnsureStarted(3, new Vector2(6f, 0f)));
            Assert.IsTrue(gesture.IsStarted);
            Assert.IsTrue(gesture.TryEnsureStarted(3, new Vector2(7f, 0f)),
                "已启动手势的后续事件不能再次依赖阈值判断。");
        }

        [Test]
        public void TransformGestureSessionAcceptsOnlyRotateAndScale()
        {
            var session = new ESWorkbenchTransformGestureSession(
                new ESWorkbenchViewportFeelSettings(rotationDegreesPerPixel: 1f));

            Assert.IsFalse(session.Begin(
                ESWorkbenchMutationKind.Move,
                Vector2.zero,
                Vector3.zero));
            Assert.IsFalse(session.IsActive);
            Assert.IsFalse(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                new Vector2(float.NaN, 0f),
                Vector3.zero));
            Assert.IsFalse(session.IsActive);
            Assert.IsFalse(session.Begin(
                ESWorkbenchMutationKind.Scale,
                Vector2.zero,
                new Vector3(0f, float.PositiveInfinity, 1f)));
            Assert.IsFalse(session.IsActive);

            Assert.IsTrue(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                Vector3.zero));
            Assert.IsTrue(session.IsActive);
            Assert.AreEqual(ESWorkbenchMutationKind.Rotate, session.Kind);
            Assert.IsFalse(session.Begin(
                ESWorkbenchMutationKind.Scale,
                Vector2.one,
                Vector3.one),
                "活动会话不能被新的 Begin 覆盖，必须先 Reset/结束当前手势。");
        }

        [Test]
        public void TransformGestureSessionRetainsUnsnappedAccumulation()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 32f);
            var session = new ESWorkbenchTransformGestureSession(feel);
            Func<Vector3, Vector3> snap = value =>
                new Vector3(0f, Mathf.Round(value.y / 15f) * 15f, 0f);

            Assert.IsTrue(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                Vector3.zero));
            Assert.IsTrue(session.TryUpdate(new Vector2(7f, 0f), snap, out Vector3 first));
            Assert.AreEqual(0f, first.y, 0.0001f,
                "首个未达到吸附步长的预览应保持在当前吸附值。");
            Assert.AreEqual(7f, session.AccumulatedValue.y, 0.0001f,
                "会话必须保留未吸附累计值，不能每次从吸附结果重新计算。");

            Assert.IsTrue(session.TryUpdate(new Vector2(8f, 0f), snap, out Vector3 second));
            Assert.AreEqual(15f, second.y, 0.0001f);
            Assert.AreEqual(8f, session.AccumulatedValue.y, 0.0001f);
            Assert.AreEqual(new Vector2(8f, 0f), session.ConsumedPointer);
        }

        [Test]
        public void TransformGestureSessionRetainsPointerRemainderAfterEventCap()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 10f);
            var session = new ESWorkbenchTransformGestureSession(feel);
            Assert.IsTrue(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                Vector3.zero));

            Assert.IsTrue(session.TryUpdate(new Vector2(100f, 0f), null, out Vector3 first));
            Assert.AreEqual(10f, first.y, 0.0001f);
            Assert.AreEqual(new Vector2(10f, 0f), session.ConsumedPointer);

            Assert.IsTrue(session.TryUpdate(new Vector2(101f, 0f), null, out Vector3 second));
            Assert.AreEqual(20f, second.y, 0.0001f,
                "后续事件必须继续消耗限幅后尚未消费的指针距离。");
            Assert.AreEqual(new Vector2(20f, 0f), session.ConsumedPointer);
        }

        [Test]
        public void TransformGestureSessionFinalizesFromOriginalPointerEndpoint()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 8f);
            var session = new ESWorkbenchTransformGestureSession(feel);
            Assert.IsTrue(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                Vector3.zero));
            Assert.IsTrue(session.TryUpdate(new Vector2(100f, 0f), null, out Vector3 capped));
            Assert.AreEqual(8f, capped.y, 0.0001f);
            Assert.AreEqual(new Vector2(8f, 0f), session.ConsumedPointer);

            Func<Vector3, Vector3> snap = value =>
                new Vector3(0f, Mathf.Round(value.y / 15f) * 15f, 0f);
            Assert.IsTrue(session.TryFinalize(new Vector2(100f, 0f), snap, out Vector3 endpoint));
            Assert.AreEqual(105f, endpoint.y, 0.0001f,
                "释放必须从原始起点解析到最终指针，不能丢失限幅事件之后的余量。");
            Assert.AreEqual(new Vector2(100f, 0f), session.ConsumedPointer);
            Assert.AreEqual(100f, session.AccumulatedValue.y, 0.0001f);
        }

        [Test]
        public void TransformGestureSessionFailureAndResetDoNotLeakState()
        {
            var feel = new ESWorkbenchViewportFeelSettings(rotationDegreesPerPixel: 1f);
            var session = new ESWorkbenchTransformGestureSession(feel);
            Assert.IsTrue(session.Begin(
                ESWorkbenchMutationKind.Rotate,
                new Vector2(2f, 3f),
                new Vector3(0f, 10f, 0f)));
            Vector2 consumedBefore = session.ConsumedPointer;
            Vector3 accumulatedBefore = session.AccumulatedValue;

            Assert.IsFalse(session.TryUpdate(
                new Vector2(float.NaN, 5f),
                null,
                out _));
            Assert.AreEqual(consumedBefore, session.ConsumedPointer);
            Assert.AreEqual(accumulatedBefore, session.AccumulatedValue);
            Assert.IsTrue(session.IsActive);

            Func<Vector3, Vector3> invalidSnap = _ =>
                new Vector3(float.NaN, 0f, 0f);
            Assert.IsFalse(session.TryFinalize(
                new Vector2(8f, 3f),
                invalidSnap,
                out _));
            Assert.AreEqual(consumedBefore, session.ConsumedPointer);
            Assert.AreEqual(accumulatedBefore, session.AccumulatedValue);
            Assert.IsTrue(session.IsActive);

            session.Reset();
            Assert.IsFalse(session.IsActive);
            Assert.AreEqual(ESWorkbenchMutationKind.Move, session.Kind);
            Assert.AreEqual(Vector2.zero, session.StartPointer);
            Assert.AreEqual(Vector2.zero, session.ConsumedPointer);
            Assert.AreEqual(Vector3.zero, session.StartValue);
            Assert.AreEqual(Vector3.zero, session.AccumulatedValue);
            Assert.IsFalse(session.TryUpdate(Vector2.one, null, out _));
        }

        [Test]
        public void IncrementalTransformResolverFailureLeavesCommitCandidateInvalid()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 20f);
            Vector3 accumulated = Vector3.zero;
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(5f, 0f),
                accumulated,
                feel,
                null,
                out Vector3 valid,
                out accumulated));
            Assert.AreEqual(5f, valid.y, 0.0001f);

            Func<Vector3, Vector3> invalidSnap = _ =>
                new Vector3(float.NaN, 0f, 0f);
            Assert.IsFalse(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                new Vector2(5f, 0f),
                new Vector2(6f, 0f),
                accumulated,
                feel,
                invalidSnap,
                out Vector3 failed,
                out Vector3 failedAccumulated,
                out Vector2 failedConsumedPointer));
            Assert.AreEqual(Vector3.zero, failed,
                "解析失败不得留下可提交输出。");
            Assert.AreEqual(Vector3.zero, failedAccumulated,
                "解析失败不得污染累计状态。");
            Assert.AreEqual(new Vector2(5f, 0f), failedConsumedPointer,
                "解析失败不得推进下一事件的指针基准。");
        }

        [Test]
        public void IncrementalTransformResolverRetainsPointerRemainderAfterEventCap()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 10f);

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.zero,
                feel,
                null,
                out Vector3 first,
                out _,
                out Vector2 consumedPointer));
            Assert.AreEqual(10f, first.y, 0.0001f);
            Assert.AreEqual(10f, consumedPointer.x, 0.0001f,
                "事件限幅后，下一次基准必须停留在实际消耗位置，而不是跳到原始指针。");

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                consumedPointer,
                new Vector2(101f, 0f),
                first,
                feel,
                null,
                out Vector3 continued,
                out _,
                out Vector2 nextConsumedPointer));
            Assert.AreEqual(20f, continued.y, 0.0001f,
                "后续事件应继续消耗未完成的拖动距离。");
            Assert.AreEqual(20f, nextConsumedPointer.x, 0.0001f);
        }

        [Test]
        public void FeelSettingsConsumePointerDeltaWithoutDiscardingCappedDistance()
        {
            var feel = new ESWorkbenchViewportFeelSettings(maximumPointerDeltaPerEvent: 8f);

            Assert.IsTrue(feel.TryConsumePointerDelta(
                Vector2.zero,
                new Vector2(40f, 0f),
                out Vector2 firstDelta,
                out Vector2 firstConsumed));
            Assert.AreEqual(8f, firstDelta.x, 0.0001f);
            Assert.AreEqual(8f, firstConsumed.x, 0.0001f);

            Assert.IsTrue(feel.TryConsumePointerDelta(
                firstConsumed,
                new Vector2(41f, 0f),
                out Vector2 secondDelta,
                out Vector2 secondConsumed));
            Assert.AreEqual(8f, secondDelta.x, 0.0001f);
            Assert.AreEqual(16f, secondConsumed.x, 0.0001f,
                "相机/画布与对象变换必须共享同一增量消费语义。");

            Assert.IsFalse(feel.TryConsumePointerDelta(
                new Vector2(float.NaN, 0f),
                Vector2.zero,
                out _,
                out _));
        }

        [Test]
        public void FeelSettingsCanConsumeReleaseEndpointWithoutEventCap()
        {
            var feel = new ESWorkbenchViewportFeelSettings(maximumPointerDeltaPerEvent: 8f);

            Assert.IsTrue(feel.TryConsumePointerDelta(
                new Vector2(8f, 0f),
                new Vector2(40f, 6f),
                false,
                out Vector2 delta,
                out Vector2 consumedPointer));
            Assert.AreEqual(new Vector2(32f, 6f), delta);
            Assert.AreEqual(new Vector2(40f, 6f), consumedPointer,
                "释放端点必须收敛到真实指针位置，不能再次被单事件限幅截断。");
        }

        [Test]
        public void AbsoluteTransformResolverReachesReleaseEndpointAfterCappedDrag()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                rotationDegreesPerPixel: 1f,
                maximumPointerDeltaPerEvent: 8f);
            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.zero,
                feel,
                null,
                out Vector3 capped,
                out _,
                out Vector2 consumedPointer));
            Assert.AreEqual(8f, capped.y, 0.0001f);
            Assert.AreEqual(8f, consumedPointer.x, 0.0001f);

            Assert.IsTrue(ESWorkbenchTransformGestureResolver.TryResolve(
                ESWorkbenchMutationKind.Rotate,
                Vector2.zero,
                new Vector2(100f, 0f),
                Vector3.zero,
                feel,
                null,
                out Vector3 endpoint));
            Assert.AreEqual(100f, endpoint.y, 0.0001f,
                "MouseUp 端点解析必须使用起始指针到最终指针的绝对位移。");
        }

        [Test]
        public void NudgeResolverUsesStableAxesAndModifierMultipliers()
        {
            var feel = new ESWorkbenchViewportFeelSettings(
                nudgeWorldUnits: 2f,
                nudgeFineMultiplier: 0.25f,
                nudgeCoarseMultiplier: 5f);
            Assert.IsTrue(ESWorkbenchNudgeResolver.TryResolveDelta(
                KeyCode.RightArrow, false, false, feel, out Vector3 normal));
            Assert.AreEqual(new Vector3(2f, 0f, 0f), normal);
            Assert.IsTrue(ESWorkbenchNudgeResolver.TryResolveDelta(
                KeyCode.UpArrow, true, false, feel, out Vector3 coarse));
            Assert.AreEqual(new Vector3(0f, 0f, 10f), coarse);
            Assert.IsTrue(ESWorkbenchNudgeResolver.TryResolveDelta(
                KeyCode.PageDown, false, true, feel, out Vector3 fine));
            Assert.AreEqual(new Vector3(0f, -0.5f, 0f), fine);
            Assert.IsFalse(ESWorkbenchNudgeResolver.TryResolveDelta(
                KeyCode.Home, false, false, feel, out _));
        }

        [Test]
        public void DropLayoutCentersBatchAndAppliesTheSameSnapToEveryTarget()
        {
            var positions = new List<Vector3> { new Vector3(999f, 999f, 999f) };
            ESWorkbenchDropLayout.FillGridPositions(
                new Vector3(10f, 3f, 20f),
                4,
                0.1f,
                value => new Vector3(Mathf.Round(value.x), value.y, Mathf.Round(value.z)),
                positions,
                2f);

            CollectionAssert.AreEqual(new[]
            {
                new Vector3(9f, 3f, 19f),
                new Vector3(11f, 3f, 19f),
                new Vector3(9f, 3f, 21f),
                new Vector3(11f, 3f, 21f)
            }, positions);

            ESWorkbenchDropLayout.FillGridPositions(
                Vector3.zero,
                2,
                float.NaN,
                null,
                positions,
                1.5f);
            Assert.AreEqual(2, positions.Count);
            Assert.IsFalse(float.IsNaN(positions[0].x) || float.IsNaN(positions[0].z));
            Assert.AreEqual(-0.75f, positions[0].x, 0.0001f);
            Assert.AreEqual(0.75f, positions[1].x, 0.0001f);

            ESWorkbenchDropLayout.FillGridPositions(Vector3.zero, 0, 2f, null, positions);
            Assert.Zero(positions.Count, "取消拖放时复用的目标集合必须清空，不能残留旧落点。");
        }

        [Test]
        public void WorldGameViewportRejectsDropPreviewThroughTheFormalAdapter()
        {
            var window = ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>();
            ESWorldWorkbenchViewportAdapter adapter = null;
            try
            {
                ESWorkbenchActionContext actions = CreateActionContextForTest();
                var viewportContext = new ESWorkbenchViewportContext(
                    window,
                    actions,
                    "world.game",
                    new ESWorkbenchViewportLayoutState { viewportId = "world.game" });
                adapter = new ESWorldWorkbenchViewportAdapter(
                    window,
                    viewportContext,
                    ESWorkbenchViewportKind.Game);
                var item = new ESWorkbenchObjectDescriptor(
                    "world.prefab.test", "测试预制件", "测试", null,
                    contentKind: ESWorkbenchContentKind.Prefab,
                    dragMode: ESWorkbenchContentDragMode.Place);

                Assert.That(adapter, Is.InstanceOf<IESWorkbenchDropPreviewViewport>());
                Assert.That(adapter, Is.InstanceOf<IESWorkbenchViewportDropPositionDiagnostics>());
                Assert.IsFalse(adapter.CanAccept(item, out string reason));
                StringAssert.Contains("只读", reason);
                Assert.IsFalse(adapter.TryResolveDropPosition(
                    item,
                    new Vector2(100f, 100f),
                    out Vector3 resolvedPosition,
                    out string positionReason));
                Assert.AreEqual(Vector3.zero, resolvedPosition);
                StringAssert.Contains("只读", positionReason);
                Assert.DoesNotThrow(() => adapter.UpdateDropPreview(new ESWorkbenchDropPreviewContext(
                    actions,
                    item,
                    new[] { item },
                    new Vector2(100f, 100f),
                    new Rect(0f, 0f, 640f, 360f),
                    2f,
                    true,
                    string.Empty)));
                Assert.DoesNotThrow(adapter.Deactivate);
            }
            finally
            {
                adapter?.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
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
                Assert.IsTrue(viewport is IESWorkbenchViewportStatusProvider,
                    "通用二维视口必须直接提供底部状态投影能力，不能要求每个领域重复包装。");
                var statusProvider = (IESWorkbenchViewportStatusProvider)viewport;
                Assert.IsTrue(statusProvider.GetStatusSnapshot()
                    .Any(value => value != null && value.StatusId == "canvas.zoom"));
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
        public void SpatialHitResolverPrefersPreciseTargetsAndUsesSharedRectangleTolerance()
        {
            var region = new ESWorkbenchHierarchyDescriptor(
                "region", "区域", kind: "region",
                spatial: new ESWorkbenchSpatialDescriptor(
                    new Vector3(50f, 0f, 50f),
                    new Vector3(40f, 1f, 40f),
                    shape: ESWorkbenchSpatialShape.Rectangle));
            var poi = new ESWorkbenchHierarchyDescriptor(
                "poi", "点", kind: "poi",
                spatial: new ESWorkbenchSpatialDescriptor(
                    new Vector3(50f, 0f, 50f),
                    Vector3.one,
                    shape: ESWorkbenchSpatialShape.Point));
            var projected = new[] { region, poi };
            Rect world = new Rect(0f, 0f, 100f, 100f);
            Rect canvas = new Rect(0f, 0f, 100f, 100f);

            Assert.AreSame(poi, ESWorkbenchSpatialHitResolver.HitTest2D(
                projected, new Vector2(50f, 50f), world, canvas, 6f),
                "点状目标必须优先于覆盖它的矩形区域。");
            Assert.AreSame(region, ESWorkbenchSpatialHitResolver.HitTest2D(
                new[] { region }, new Vector2(71f, 50f), world, canvas, 2f),
                "矩形边缘应使用统一屏幕容差，避免小目标难以选中。");
            Assert.IsNull(ESWorkbenchSpatialHitResolver.HitTest2D(
                new[] { region }, new Vector2(75f, 50f), world, canvas, 2f));
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
            var customFeel = new ESWorkbenchViewportFeelSettings(dragStartPixels: 12f);
            var customContext = new ESWorkbenchViewportContext(
                null, CreateActionContextForTest(), "snap.custom", new ESWorkbenchViewportLayoutState(), feel: customFeel);

            Assert.AreEqual(new Vector3(1f, 2f, -0.5f), context.SnapPosition(new Vector3(1.2f, 1.8f, -0.4f)));
            Assert.AreEqual(new Vector3(0f, 45f, 0f), context.SnapRotation(new Vector3(2f, 38f, -3f)));
            Assert.AreEqual(new Vector3(1.25f, 2f, 3.5f), context.SnapScale(new Vector3(1.2f, 2.1f, 3.4f)));
            Assert.AreSame(customFeel, customContext.Feel);
        }

        [Test]
        public void CanvasNavigationRoundTripsCoordinatesAndKeepsZoomAnchorStable()
        {
            var layout = new ESWorkbenchViewportLayoutState
            {
                viewportId = "navigation",
                pan = new Vector2(24f, -13f),
                zoom = 1.75f
            };
            var navigation = new ESWorkbenchCanvasNavigationState(layout, 0.5f, 8f);
            var viewport = new Rect(0f, 0f, 1280f, 720f);
            var worldBounds = new Rect(-200f, -100f, 800f, 450f);
            var world = new Vector3(125.25f, 17f, 88.5f);
            Vector2 canvas = navigation.WorldToCanvas(world, worldBounds, viewport);

            Assert.IsTrue(navigation.TryCanvasToWorld(
                canvas, worldBounds, viewport, world.y, out Vector3 roundTrip, true));
            Assert.That(roundTrip.x, Is.EqualTo(world.x).Within(0.001f));
            Assert.That(roundTrip.y, Is.EqualTo(world.y).Within(0.001f));
            Assert.That(roundTrip.z, Is.EqualTo(world.z).Within(0.001f));

            Vector2 anchor = new Vector2(713f, 388f);
            Assert.IsTrue(navigation.TryCanvasToWorld(
                anchor, worldBounds, viewport, 0f, out Vector3 beforeZoom));
            navigation.ZoomAt(anchor, -3f, viewport, worldBounds);
            Assert.IsTrue(navigation.TryCanvasToWorld(
                anchor, worldBounds, viewport, 0f, out Vector3 afterZoom));
            Assert.That(afterZoom.x, Is.EqualTo(beforeZoom.x).Within(0.001f));
            Assert.That(afterZoom.z, Is.EqualTo(beforeZoom.z).Within(0.001f));
            Assert.AreEqual(navigation.Pan, layout.pan);
            Assert.AreEqual(navigation.Zoom, layout.zoom);

            float radiusBefore = navigation.ResolveWorldRadiusForPixels(viewport, worldBounds, 10f);
            navigation.ZoomAt(anchor, -2f, viewport, worldBounds);
            float radiusAfter = navigation.ResolveWorldRadiusForPixels(viewport, worldBounds, 10f);
            Assert.Less(radiusAfter, radiusBefore);
            Assert.AreEqual(0f,
                navigation.ResolveWorldRadiusForPixels(viewport, worldBounds, float.NaN),
                "非法屏幕像素容差不得污染 2D 命中半径。");
            Assert.AreEqual(0f,
                navigation.ResolveWorldRadiusForPixels(viewport, new Rect(0f, 0f, 0f, 100f), 10f),
                "非法世界边界不得通过领域最小半径伪造命中范围。");
        }

        [Test]
        public void CanvasNavigationRepairsInvalidPersistedState()
        {
            var layout = new ESWorkbenchViewportLayoutState
            {
                viewportId = "invalid-navigation",
                pan = new Vector2(float.NaN, float.PositiveInfinity),
                zoom = float.NaN
            };

            var navigation = new ESWorkbenchCanvasNavigationState(layout);

            Assert.AreEqual(Vector2.zero, navigation.Pan);
            Assert.AreEqual(1f, navigation.Zoom);
            Assert.AreEqual(Vector2.zero, layout.pan);
            Assert.AreEqual(1f, layout.zoom);
        }

        [Test]
        public void CameraViewportProjectionRejectsHeaderOutsideAndInvalidCoordinates()
        {
            var render = new Rect(10f, 20f, 200f, 100f);
            Rect interaction = ESWorkbenchViewportOverlay.GetInteractionRect(render);

            Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryNormalize(
                render, interaction, new Vector2(110f, 70f), out Vector3 center));
            Assert.That(center.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(center.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryNormalize(
                render, interaction, new Vector2(110f, 30f), out _),
                "覆盖层标题栏不得映射为场景落点。");
            Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryNormalize(
                render, interaction, new Vector2(250f, 70f), out _),
                "视口外坐标不得被夹到地图边缘。");
            Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryNormalize(
                render, interaction, new Vector2(250f, 70f), out Vector3 outsideDrag, true));
            Assert.Greater(outsideDrag.x, 1f,
                "主动拖动允许把指针外推到视口外，边缘平移后的对象预览不能跳回起点。");
            Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryNormalize(
                render, interaction, new Vector2(float.NaN, 70f), out _));
        }

        [Test]
        public void CameraViewportProjectionProjectsWorldPointsWithSharedEditorCoordinates()
        {
            var cameraRoot = new GameObject("Workbench projection camera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            try
            {
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = 2f;
                var render = new Rect(10f, 20f, 200f, 100f);
                Rect interaction = ESWorkbenchViewportOverlay.GetInteractionRect(render);

                Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    camera,
                    camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f)),
                    render,
                    interaction,
                    out Vector2 center,
                    out float centerDepth));
                Assert.That(center.x, Is.EqualTo(110f).Within(0.001f));
                Assert.That(center.y, Is.EqualTo(70f).Within(0.001f));
                Assert.That(centerDepth, Is.EqualTo(10f).Within(0.001f));

                Vector3 headerPoint = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.9f, 10f));
                Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    camera, headerPoint, render, interaction, out _, out _),
                    "标题覆盖层里的投影不得参与场景命中。");
                Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    camera, headerPoint, render, interaction, out _, out _, true),
                    "显式允许越界时仍应返回可用于拖动预览的连续坐标。");
                Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    camera, new Vector3(0f, 0f, -10f), render, interaction, out _, out _),
                    "相机后方目标不得命中。");
                Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                    camera, new Vector3(float.NaN, 0f, 10f), render, interaction, out _, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void CameraViewportProjectionKeepsScreenMarkerRadiusStableAcrossCameraModes()
        {
            var cameraRoot = new GameObject("Workbench marker radius camera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            try
            {
                Rect render = new Rect(0f, 0f, 200f, 100f);
                camera.orthographic = false;
                camera.fieldOfView = 60f;
                Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryResolveWorldRadiusForPixels(
                    camera, new Vector3(0f, 0f, 10f), render, 10f, out float perspectiveRadius));
                Assert.AreEqual(
                    2f * 10f * Mathf.Tan(30f * Mathf.Deg2Rad) / 100f * 10f,
                    perspectiveRadius,
                    0.0001f);
                Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryResolveWorldRadiusForPixels(
                    camera, new Vector3(0f, 0f, 20f), render, 10f, out float fartherRadius));
                Assert.AreEqual(2f, fartherRadius / perspectiveRadius, 0.0001f,
                    "透视相机下标记世界半径必须随深度线性变化，保持屏幕尺寸稳定。");

                camera.orthographic = true;
                camera.orthographicSize = 5f;
                Assert.IsTrue(ESWorkbenchCameraViewportProjection.TryResolveWorldRadiusForPixels(
                    camera, new Vector3(0f, 0f, 10f), render, 10f, out float orthographicRadius));
                Assert.AreEqual(1f, orthographicRadius, 0.0001f,
                    "正交相机下同样的屏幕标记半径必须保持同一像素手感。");
                Assert.IsFalse(ESWorkbenchCameraViewportProjection.TryResolveWorldRadiusForPixels(
                    camera, new Vector3(0f, 0f, -1f), render, 10f, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void OrbitCameraStateClampsNavigationAndRejectsInvalidInput()
        {
            var camera = new ESWorkbenchOrbitCameraState(
                new Vector3(1f, 2f, 3f), 20f, 35f, 25f, 10f, 70f, 2f, 100f);

            camera.Orbit(new Vector2(10f, 1000f));
            Assert.AreEqual(10f, camera.Pitch);
            camera.Orbit(new Vector2(0f, -1000f));
            Assert.AreEqual(70f, camera.Pitch);
            Assert.That(camera.Yaw, Is.InRange(-180f, 180f));
            camera.Zoom(1000f);
            Assert.AreEqual(100f, camera.Distance);
            camera.Zoom(-1000f);
            Assert.AreEqual(2f, camera.Distance);
            Vector3 beforeInvalid = camera.Focus;
            camera.Pan(new Vector2(float.NaN, 10f));
            Assert.AreEqual(beforeInvalid, camera.Focus);
        }

        [Test]
        public void OrbitCameraPanScaleFollowsViewportProjection()
        {
            var camera = new ESWorkbenchOrbitCameraState(
                Vector3.zero, 20f, 35f, 25f, -80f, 80f, 0.3f, 5000f);
            Rect smallViewport = new Rect(0f, 0f, 800f, 300f);
            Rect largeViewport = new Rect(0f, 0f, 800f, 1200f);

            float smallScale = camera.ResolvePanWorldPerPixel(smallViewport);
            float largeScale = camera.ResolvePanWorldPerPixel(largeViewport);
            Assert.That(smallScale, Is.GreaterThan(0f));
            Assert.That(largeScale, Is.GreaterThan(0f));
            Assert.AreEqual(4f, smallScale / largeScale, 0.0001f,
                "视口高度变化时，平移世界距离必须按投影尺度反向缩放，保持屏幕手感一致。");

            Vector3 before = camera.Focus;
            camera.Pan(new Vector2(10f, 0f), smallViewport);
            Vector3 smallMove = camera.Focus - before;
            camera.SetView(Vector3.zero, 20f, 35f, 25f);
            camera.Pan(new Vector2(10f, 0f), largeViewport);
            Vector3 largeMove = camera.Focus;
            Assert.AreEqual(4f, smallMove.magnitude / largeMove.magnitude, 0.0001f);
        }

        [Test]
        public void OrbitCameraStatePersistsAndRestoresEveryNavigationMutation()
        {
            var layout = new ESWorkbenchViewportLayoutState { viewportId = "camera-persistence" };
            var camera = new ESWorkbenchOrbitCameraState(
                layout,
                new Vector3(1f, 2f, 3f),
                20f,
                35f,
                25f,
                10f,
                70f,
                2f,
                100f);

            Assert.IsTrue(layout.cameraInitialized);
            camera.Orbit(new Vector2(18f, -12f));
            camera.Pan(new Vector2(9f, -4f));
            camera.Zoom(-3f);

            Assert.AreEqual(camera.Focus, layout.cameraFocus);
            Assert.AreEqual(camera.Distance, layout.cameraDistance);
            Assert.AreEqual(camera.Yaw, layout.cameraYaw);
            Assert.AreEqual(camera.Pitch, layout.cameraPitch);

            var restored = new ESWorkbenchOrbitCameraState(
                layout,
                Vector3.zero,
                8f,
                0f,
                0f,
                10f,
                70f,
                2f,
                100f);
            Assert.AreEqual(camera.Focus, restored.Focus);
            Assert.AreEqual(camera.Distance, restored.Distance);
            Assert.AreEqual(camera.Yaw, restored.Yaw);
            Assert.AreEqual(camera.Pitch, restored.Pitch);
        }

        [Test]
        public void OrbitCameraStateRepairsInvalidPersistedViewWithDomainDefaults()
        {
            var layout = new ESWorkbenchViewportLayoutState
            {
                viewportId = "camera-invalid",
                cameraInitialized = true,
                cameraFocus = new Vector3(float.NaN, 4f, 5f),
                cameraDistance = float.PositiveInfinity,
                cameraYaw = float.NaN,
                cameraPitch = float.NegativeInfinity
            };
            Vector3 defaultFocus = new Vector3(10f, 20f, 30f);
            var camera = new ESWorkbenchOrbitCameraState(
                layout,
                defaultFocus,
                42f,
                45f,
                22f,
                10f,
                70f,
                2f,
                100f);

            Assert.AreEqual(defaultFocus, camera.Focus);
            Assert.AreEqual(42f, camera.Distance);
            Assert.AreEqual(45f, camera.Yaw);
            Assert.AreEqual(22f, camera.Pitch);
            Assert.AreEqual(defaultFocus, layout.cameraFocus);
            Assert.AreEqual(42f, layout.cameraDistance);
        }

        [Test]
        public void PrecisionTransformResolverSupportsAbsoluteDeltaAndScaleValidation()
        {
            var spatial = new ESWorkbenchSpatialDescriptor(
                new Vector3(10f, 2f, 20f),
                new Vector3(4f, 1f, 6f),
                new Vector3(0f, 30f, 0f),
                ESWorkbenchSpatialShape.Rectangle);

            Assert.IsTrue(ESWorkbenchPrecisionTransformResolver.TryResolve(
                ESWorkbenchPrecisionTransformMode.Absolute,
                ESWorkbenchMutationKind.Move,
                spatial,
                new Vector3(1f, 2f, 3f),
                out Vector3 absolute,
                out string absoluteError), absoluteError);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), absolute);

            Assert.IsTrue(ESWorkbenchPrecisionTransformResolver.TryResolve(
                ESWorkbenchPrecisionTransformMode.Delta,
                ESWorkbenchMutationKind.Rotate,
                spatial,
                new Vector3(5f, 15f, -5f),
                out Vector3 delta,
                out string deltaError), deltaError);
            Assert.AreEqual(new Vector3(5f, 45f, -5f), delta);

            Assert.IsFalse(ESWorkbenchPrecisionTransformResolver.TryResolve(
                ESWorkbenchPrecisionTransformMode.Absolute,
                ESWorkbenchMutationKind.Scale,
                spatial,
                new Vector3(1f, 0f, 1f),
                out _,
                out string scaleError));
            StringAssert.Contains("大于 0", scaleError);
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
        public void RegionPrecisionResizeClampsToWorldAndKeepsRequestedSize()
        {
            ESWorldBuilderWorkbenchWindow.ResizeRegionWithinWorld(
                new Vector2(95f, 4f),
                new Vector2(30f, 20f),
                Vector2.zero,
                new Vector2(100f, 80f),
                out Vector2 resultMin,
                out Vector2 resultMax);

            Assert.AreEqual(new Vector2(70f, 0f), resultMin);
            Assert.AreEqual(new Vector2(100f, 20f), resultMax);
            Assert.AreEqual(new Vector2(30f, 20f), resultMax - resultMin);
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
        public void AuthoringServiceBatchCreatePreflightsAndRollsBackAsSingleUndoTransaction()
        {
            var target = ScriptableObject.CreateInstance<TestAsset>();
            var selection = new ESWorkbenchSelectionService();
            var service = new ESWorkbenchAuthoringService();
            int refreshCount = 0;
            int dirtyCount = 0;
            var actions = new ESWorkbenchActionContext(
                null,
                selection,
                new ESWorkbenchToolStateService(),
                service,
                (_, __) => { },
                (_, __) => { },
                _ => refreshCount++,
                (_, __) => dirtyCount++);
            var adapter = new ESWorkbenchAuthoringAdapterDescriptor(
                "tests.batch",
                value => value?.Kind == "test.batch",
                _ => true,
                create: context =>
                {
                    target.counter++;
                    if (context.Item.BaseObjectId == "bad")
                        return ESWorkbenchMutationResult.Failure("模拟第二项失败");
                    return ESWorkbenchMutationResult.Success(
                        "Created",
                        new ESWorkbenchSelection("created." + target.counter, "test.batch", target, null),
                        "test.batch.items");
                },
                resolveUndoTargets: _ => new UnityEngine.Object[] { target });
            service.Bind(actions, () => new[] { adapter });
            try
            {
                var failedRequests = new[]
                {
                    new ESWorkbenchCreateRequest(
                        new ESWorkbenchObjectDescriptor("good", "Good", "Tests", target), Vector3.zero),
                    new ESWorkbenchCreateRequest(
                        new ESWorkbenchObjectDescriptor("bad", "Bad", "Tests", target), Vector3.right)
                };
                Assert.IsTrue(service.CanCreateBatch(failedRequests, out string preflightMessage), preflightMessage);
                Assert.IsFalse(service.TryCreateBatch(failedRequests, out string failureMessage));
                StringAssert.Contains("第 2 项失败", failureMessage);
                Assert.AreEqual(0, target.counter, "任一批量项失败必须回滚全部已执行项。 ");
                Assert.AreEqual(0, refreshCount);
                Assert.AreEqual(0, dirtyCount);

                var successRequests = new[]
                {
                    new ESWorkbenchCreateRequest(
                        new ESWorkbenchObjectDescriptor("good-a", "Good A", "Tests", target), Vector3.zero),
                    new ESWorkbenchCreateRequest(
                        new ESWorkbenchObjectDescriptor("good-b", "Good B", "Tests", target), Vector3.right)
                };
                Assert.IsTrue(service.TryCreateBatch(successRequests, out string successMessage), successMessage);
                Assert.AreEqual(2, target.counter);
                Assert.AreEqual("created.2", selection.Current.StableId);
                Assert.AreEqual(1, refreshCount, "批量成功后只能发布一次刷新。 ");
                Assert.AreEqual(1, dirtyCount, "同一 DirtyKey 在批量事务中应合并。 ");
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

        private static void RegisterDocumentDescriptor(
            string workbenchId,
            string marker,
            Action release)
        {
            RegisterDocumentDescriptor(workbenchId, "document", "document", TestModule.Core, release, marker);
        }

        private static void RegisterDocumentDescriptor(
            string workbenchId,
            string contributionId,
            string documentId,
            TestModule module,
            Action release,
            string displayName = null)
        {
            Assert.IsTrue(ESWorkbenchContributionRegistry<TestModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<TestModule>(
                    workbenchId,
                    contributionId,
                    displayName ?? documentId,
                    module,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            documentId,
                            displayName ?? documentId,
                            documentId,
                            false,
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

        [Test]
        public void PointerIntentResolverKeepsSelectionPaintAndGroundActionsExclusive()
        {
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.None,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        externalContentDragActive: true,
                        navigationGestureActive: false,
                        paintInteractionActive: true,
                        selectionInteractionActive: false,
                        hasHitTarget: true,
                        manipulationEnabled: true,
                        canManipulate: true,
                        hierarchyLocked: false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, false, true, true, true, true, false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, false, true, true, true, false, false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, false, true, true, true, true, true)),
                "锁定目标仍可选择，但不能进入直接操作手势。");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, false, true, false, true, true, false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, true, false, true, true, true, false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        ESWorkbenchToolCapabilities.Paint | ESWorkbenchToolCapabilities.Move,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Paint,
                        ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                        true,
                        false)),
                "笔刷命中可移动精确目标时，目标移动优先于地面绘制。 ");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        ESWorkbenchToolCapabilities.Paint,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Paint,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Rotate,
                        true,
                        false)),
                "笔刷命中只能旋转的对象时不能越权启动旋转。 ");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, true, true, true, true, true, false)),
                "异常工具状态同时声明选择和笔刷时，选择/变换必须保持唯一主意图。");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.GroundAction,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(false, false, false, false, false, false, false, false)));
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.None,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false, false, false, false, false, false, false, false,
                        groundActionEnabled: false)));

            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Paint,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        ESWorkbenchToolCapabilities.Select
                            | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Paint,
                        ESWorkbenchToolCapabilities.Select
                            | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Paint
                            | ESWorkbenchToolCapabilities.GroundAction,
                        ESWorkbenchToolCapabilities.Select,
                        false,
                        false)),
                "混合选择/移动/笔刷工具在空地上必须进入笔刷，而不是停留在选择占位状态。");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Paint,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        paintInteractionActive: true,
                        selectionInteractionActive: true,
                        hasHitTarget: false,
                        manipulationEnabled: true,
                        canManipulate: false,
                        hierarchyLocked: false)),
                "旧布尔构造入口也必须保留混合工具的笔刷地面语义。");
        }

        [Test]
        public void PointerIntentDecisionExplainsOwnershipAndCommitPermissions()
        {
            ESWorkbenchPointerIntentDecision external =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        externalContentDragActive: true,
                        navigationGestureActive: false,
                        paintInteractionActive: true,
                        selectionInteractionActive: false,
                        hasHitTarget: false,
                        manipulationEnabled: false,
                        canManipulate: false,
                        hierarchyLocked: false));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.None, external.Intent);
            Assert.IsFalse(external.CanStart);
            Assert.IsTrue(external.ConsumesNavigation);
            Assert.IsFalse(external.CanCommit);
            Assert.AreEqual(
                ESWorkbenchPointerIntentDecisionReason.ExternalContentDrag,
                external.Reason);

            ESWorkbenchPointerIntentDecision navigationOwned =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        externalContentDragActive: false,
                        navigationGestureActive: true,
                        paintInteractionActive: false,
                        selectionInteractionActive: true,
                        hasHitTarget: true,
                        manipulationEnabled: true,
                        canManipulate: true,
                        hierarchyLocked: false));
            Assert.IsFalse(navigationOwned.CanStart);
            Assert.IsTrue(navigationOwned.ConsumesNavigation);
            Assert.AreEqual(
                ESWorkbenchPointerIntentDecisionReason.NavigationAlreadyOwned,
                navigationOwned.Reason);

            ESWorkbenchPointerIntentDecision brushOnRegion =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        ESWorkbenchToolCapabilities.Paint | ESWorkbenchToolCapabilities.Move,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Paint,
                        ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                        hasHitTarget: true,
                        hierarchyLocked: false));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Manipulate, brushOnRegion.Intent);
            Assert.IsTrue(brushOnRegion.CanStart);
            Assert.IsTrue(brushOnRegion.ConsumesNavigation);
            Assert.IsTrue(brushOnRegion.CanCommit);
            Assert.AreEqual(
                ESWorkbenchPointerIntentDecisionReason.ManipulateTarget,
                brushOnRegion.Reason);

            ESWorkbenchPointerIntentDecision lockedRegion =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                        ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                        hasHitTarget: true,
                        hierarchyLocked: true));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Select, lockedRegion.Intent);
            Assert.IsTrue(lockedRegion.CanStart);
            Assert.IsTrue(lockedRegion.ConsumesNavigation);
            Assert.IsFalse(lockedRegion.CanCommit);
            Assert.AreEqual(
                ESWorkbenchPointerIntentDecisionReason.HierarchyLocked,
                lockedRegion.Reason);
        }

        [Test]
        public void PaintCannotPromoteTargetWhenViewportDoesNotDeclareMove()
        {
            ESWorkbenchPointerIntentContext context = new ESWorkbenchPointerIntentContext(
                externalContentDragActive: false,
                navigationGestureActive: false,
                toolCapabilities: ESWorkbenchToolCapabilities.Paint,
                viewportCapabilities: ESWorkbenchToolCapabilities.Select
                    | ESWorkbenchToolCapabilities.Paint,
                targetCapabilities: ESWorkbenchToolCapabilityResolver.ResolveTarget(
                    canMove: true,
                    canRotate: false,
                    canScale: false),
                hasHitTarget: true,
                hierarchyLocked: false);
            ESWorkbenchPointerIntentDecision decision =
                ESWorkbenchPointerIntentResolver.ResolveDecision(context);

            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                decision.Intent,
                "未声明 Move 的视口不能被笔刷路径升级为对象移动。");
            Assert.IsFalse(decision.CanCommit);
            Assert.AreEqual(
                ESWorkbenchPointerIntentDecisionReason.SelectTarget,
                decision.Reason);
            Assert.IsFalse(context.ManipulationEnabled);
            Assert.IsFalse(context.CanManipulate);

            ESWorkbenchPointerIntentContext handoffContext =
                new ESWorkbenchPointerIntentContext(
                    externalContentDragActive: false,
                    navigationGestureActive: false,
                    toolCapabilities: ESWorkbenchToolCapabilities.Paint,
                    viewportCapabilities: ESWorkbenchToolCapabilities.Select
                        | ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.Paint,
                    targetCapabilities: ESWorkbenchToolCapabilityResolver.ResolveTarget(
                        canMove: true,
                        canRotate: false,
                        canScale: false),
                    hasHitTarget: true,
                    hierarchyLocked: false);
            ESWorkbenchPointerIntentDecision handoff =
                ESWorkbenchPointerIntentResolver.ResolveDecision(handoffContext);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                handoff.Intent,
                "笔刷命中可移动精确目标时必须让出给对象移动。");
            Assert.IsTrue(
                handoff.CanStart && handoff.CanCommit,
                "让出路径必须同时暴露可开始和可提交事实。");
            Assert.IsTrue(
                handoffContext.ManipulationEnabled,
                "笔刷命中可移动对象时，上下文必须声明视口实际支持移动手势。");
            Assert.IsTrue(
                handoffContext.CanManipulate,
                "笔刷命中可移动对象时，目标能力必须与最终 Manipulate 决策一致。");

            ESWorkbenchPointerIntentContext lockedHandoff =
                new ESWorkbenchPointerIntentContext(
                    externalContentDragActive: false,
                    navigationGestureActive: false,
                    toolCapabilities: ESWorkbenchToolCapabilities.Paint,
                    viewportCapabilities: ESWorkbenchToolCapabilities.Select
                        | ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.Paint,
                    targetCapabilities: ESWorkbenchToolCapabilityResolver.ResolveTarget(
                        canMove: true,
                        canRotate: false,
                        canScale: false),
                    hasHitTarget: true,
                    hierarchyLocked: true);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Select,
                ESWorkbenchPointerIntentResolver.Resolve(lockedHandoff));
            Assert.IsFalse(
                lockedHandoff.CanManipulate,
                "锁定目标即使声明可移动，也不能暴露可执行移动能力。");

            ESWorkbenchPointerIntentContext moveOnlyTarget =
                new ESWorkbenchPointerIntentContext(
                    externalContentDragActive: false,
                    navigationGestureActive: false,
                    toolCapabilities: ESWorkbenchToolCapabilities.Paint,
                    viewportCapabilities: ESWorkbenchToolCapabilities.Select
                        | ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.Paint,
                    targetCapabilities: ESWorkbenchToolCapabilities.Move,
                    hasHitTarget: true,
                    hierarchyLocked: false);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Paint,
                ESWorkbenchPointerIntentResolver.Resolve(moveOnlyTarget),
                "缺少 Select 身份的目标不能被笔刷让出路径直接移动。");
            Assert.IsFalse(
                moveOnlyTarget.CanManipulate,
                "笔刷让出能力必须与目标 Select 门槛保持一致。");

            ESWorkbenchPointerIntentContext noHitTarget =
                new ESWorkbenchPointerIntentContext(
                    externalContentDragActive: false,
                    navigationGestureActive: false,
                    toolCapabilities: ESWorkbenchToolCapabilities.Paint,
                    viewportCapabilities: ESWorkbenchToolCapabilities.Select
                        | ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.Paint,
                    targetCapabilities: ESWorkbenchToolCapabilityResolver.ResolveTarget(
                        canMove: true,
                        canRotate: false,
                        canScale: false),
                    hasHitTarget: false,
                    hierarchyLocked: false);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Paint,
                ESWorkbenchPointerIntentResolver.Resolve(noHitTarget));
            Assert.IsFalse(
                noHitTarget.CanManipulate,
                "没有命中目标时不能仅凭目标能力位暴露移动事实。");
        }

        [Test]
        public void BrushOnlyTreatsAnImmovableContainerAsGroundButYieldsToMovableContainer()
        {
            ESWorkbenchToolCapabilities brush = ESWorkbenchToolCapabilities.Paint;
            ESWorkbenchToolCapabilities viewport =
                ESWorkbenchToolCapabilities.Select
                | ESWorkbenchToolCapabilities.Move
                | ESWorkbenchToolCapabilities.Paint;

            ESWorkbenchPointerIntentDecision immovable =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        brush,
                        viewport,
                        ESWorkbenchToolCapabilities.Select,
                        hasHitTarget: true,
                        hierarchyLocked: false,
                        hitKind: ESWorkbenchPointerHitKind.Container));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Paint, immovable.Intent);
            Assert.IsTrue(immovable.CanCommit);

            ESWorkbenchPointerIntentDecision movable =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        brush,
                        viewport,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                        hasHitTarget: true,
                        hierarchyLocked: false,
                        hitKind: ESWorkbenchPointerHitKind.Container));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Manipulate, movable.Intent);
            Assert.IsTrue(movable.CanCommit);

            ESWorkbenchPointerIntentDecision locked =
                ESWorkbenchPointerIntentResolver.ResolveDecision(
                    new ESWorkbenchPointerIntentContext(
                        false,
                        false,
                        brush,
                        viewport,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                        hasHitTarget: true,
                        hierarchyLocked: true,
                        hitKind: ESWorkbenchPointerHitKind.Container));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Select, locked.Intent);
            Assert.IsFalse(locked.CanCommit);
        }

        [Test]
        public void SpatialHitResolverClassifiesWorldAuthoringLayersWithoutHostSpecificRules()
        {
            Assert.AreEqual(
                ESWorkbenchPointerHitKind.Ground,
                ESWorkbenchSpatialHitResolver.ResolveHitKind(null, IsWorldRegionSelection));
            Assert.AreEqual(
                ESWorkbenchPointerHitKind.Container,
                ESWorkbenchSpatialHitResolver.ResolveHitKind(
                    new ESWorkbenchSelection("world.region.a", "world.region", null, "a"),
                    IsWorldRegionSelection));
            Assert.AreEqual(
                ESWorkbenchPointerHitKind.PreciseTarget,
                ESWorkbenchSpatialHitResolver.ResolveHitKind(
                    new ESWorkbenchSelection("world.poi.a", "world.poi", null, "a"),
                    IsWorldRegionSelection));
            Assert.AreEqual(
                ESWorkbenchPointerHitKind.PreciseTarget,
                ESWorkbenchSpatialHitResolver.ResolveHitKind(
                    new ESWorkbenchSelection("world.prefab.a", "world.prefab", null, "a"),
                    IsWorldRegionSelection));
        }

        private static bool IsWorldRegionSelection(ESWorkbenchSelection selection)
        {
            return selection != null && selection.Kind == "world.region";
        }

        [Test]
        public void PreciseHoverPolicyKeepsBrushPreviewVisibleUntilStrokeBegins()
        {
            ESWorkbenchToolCapabilities brush = ESWorkbenchToolCapabilities.Paint;

            Assert.IsTrue(ESWorkbenchInteractionPolicy.ShouldShowPreciseHover(
                readOnly: false,
                transforming: false,
                painting: false,
                navigationCapturing: false,
                capabilities: brush,
                pointerInside: true));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldShowPreciseHover(
                readOnly: false,
                transforming: false,
                painting: true,
                navigationCapturing: false,
                capabilities: brush,
                pointerInside: true));
            Assert.IsFalse(ESWorkbenchInteractionPolicy.ShouldShowPreciseHover(
                readOnly: false,
                transforming: false,
                painting: false,
                navigationCapturing: false,
                capabilities: ESWorkbenchToolCapabilities.None,
                pointerInside: true));
        }

        [Test]
        public void ScreenGeometryUsesProjectedPolygonInsteadOfBoundingBox()
        {
            var polygon = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(100f, 20f),
                new Vector2(80f, 100f),
                new Vector2(10f, 80f)
            };

            Assert.IsTrue(ESWorkbenchScreenGeometry.ContainsPolygon(
                polygon, polygon.Length, new Vector2(45f, 45f), 0f));
            Assert.IsFalse(ESWorkbenchScreenGeometry.ContainsPolygon(
                polygon, polygon.Length, new Vector2(95f, 95f), 0f));
            Assert.IsTrue(ESWorkbenchScreenGeometry.ContainsPolygon(
                polygon, polygon.Length, new Vector2(100f, 20f), 2f));
        }

        [Test]
        public void ToolCapabilitiesResolveMixedPrefabAndRejectUnsupportedTransform()
        {
            ESWorkbenchToolCapabilities prefab = ESWorkbenchToolCapabilityResolver.Resolve(
                "world.prefab",
                ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.GroundAction);
            Assert.IsTrue(ESWorkbenchToolCapabilityResolver.Has(prefab, ESWorkbenchToolCapabilities.Select));
            Assert.IsTrue(ESWorkbenchToolCapabilityResolver.Has(prefab, ESWorkbenchToolCapabilities.Move));
            Assert.IsTrue(ESWorkbenchToolCapabilityResolver.Has(prefab, ESWorkbenchToolCapabilities.GroundAction));
            Assert.AreEqual(
                ESWorkbenchToolCapabilities.None,
                ESWorkbenchToolCapabilityResolver.Resolve("world.prefab"),
                "领域工具没有显式描述时，公共底座不得按 World 命名猜测能力。");

            ESWorkbenchPointerIntentKind blankPrefab = ESWorkbenchPointerIntentResolver.Resolve(
                new ESWorkbenchPointerIntentContext(
                    false, false, prefab,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.GroundAction,
                    ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                    false, false));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.GroundAction, blankPrefab);

            ESWorkbenchPointerIntentKind rotateOnMoveOnlyTarget = ESWorkbenchPointerIntentResolver.Resolve(
                new ESWorkbenchPointerIntentContext(
                    false, false,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Rotate,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                    ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                    true, false));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Select, rotateOnMoveOnlyTarget);

            ESWorkbenchPointerIntentKind lockedScale = ESWorkbenchPointerIntentResolver.Resolve(
                new ESWorkbenchPointerIntentContext(
                    false, false,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Scale,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Scale,
                    ESWorkbenchToolCapabilityResolver.ResolveTarget(false, false, true),
                    true, true));
            Assert.AreEqual(ESWorkbenchPointerIntentKind.Select, lockedScale);

            ESWorkbenchToolCapabilities region = ESWorkbenchToolCapabilityResolver.Resolve(
                "world.region",
                ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.GroundAction);
            Assert.AreEqual(
                ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.GroundAction,
                region);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.Manipulate,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false, false, region,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.GroundAction,
                        ESWorkbenchToolCapabilityResolver.ResolveTarget(true, false, false),
                        true, false)),
                "区域工具命中已有区域时必须进入移动，不能被创建动作或笔刷抢占。");
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.GroundAction,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false, false, region,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.GroundAction,
                        ESWorkbenchToolCapabilities.Select,
                        false, false)),
                "区域工具点击空地时仍应创建新区域。");

            ESWorkbenchToolCapabilities poi = ESWorkbenchToolCapabilityResolver.Resolve(
                "world.poi",
                ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.GroundAction);
            Assert.AreEqual(region, poi, "POI 与区域应共享命中移动、空地创建的复合工具语义。");

            ESWorkbenchToolCapabilities customPaint = ESWorkbenchToolCapabilityResolver.Resolve(
                "custom.biome-brush", ESWorkbenchToolCapabilities.Paint);
            Assert.AreEqual(ESWorkbenchToolCapabilities.Paint, customPaint);
            Assert.AreEqual(
                ESWorkbenchPointerIntentKind.None,
                ESWorkbenchPointerIntentResolver.Resolve(
                    new ESWorkbenchPointerIntentContext(
                        false, false, customPaint,
                        ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                        ESWorkbenchToolCapabilities.Select,
                        false, false)),
                "视口没有声明 Paint 能力时，自定义笔刷不能获得占位输入。" );
        }

        [Test]
        public void WorldPreciseHitIsNotOverriddenByContainingRegion()
        {
            // 2D 世界视图中区域是背景容器，预制件/POI 是精确可操作目标。
            // 精确目标命中后，区域不得抢回选择和移动手势。
            ESWorkbenchSelection precise = new ESWorkbenchSelection(
                "world.poi.spawn", "world.poi", null, "spawn");
            ESWorkbenchSelection region = new ESWorkbenchSelection(
                "world.region.playable", "world.region", null, "playable");

            Assert.AreSame(precise, ESWorkbenchSpatialHitResolver.PreferPrecise(precise, region));
            Assert.AreSame(region, ESWorkbenchSpatialHitResolver.PreferPrecise(null, region));
            Assert.AreEqual(
                ESWorkbenchSelection.Empty,
                ESWorkbenchSpatialHitResolver.PreferPrecise(null, ESWorkbenchSelection.Empty));
        }

        [Test]
        public void HoverStateUsesStableIdentityAndClearIsIdempotent()
        {
            var hover = new ESWorkbenchHoverState();

            Assert.IsFalse(hover.HasValue);
            Assert.IsTrue(hover.Update("  world.region.alpha  "));
            Assert.AreEqual("world.region.alpha", hover.StableId);
            Assert.IsTrue(hover.IsHovered("world.region.alpha"));
            Assert.IsFalse(hover.IsHovered("world.region.beta"));
            Assert.IsFalse(hover.Update("world.region.alpha"));
            Assert.IsTrue(hover.Clear());
            Assert.IsFalse(hover.HasValue);
            Assert.IsFalse(hover.Clear());
        }

        [Test]
        public void SelectionCacheReusesLocalHitIdentityAndClearsGenerations()
        {
            var cache = new ESWorkbenchSelectionCache();
            ESWorkbenchSelection first = cache.GetOrCreateLocal(
                "world.poi", "spawn", "world.poi.", payload: "spawn");
            ESWorkbenchSelection repeated = cache.GetOrCreateLocal(
                "world.poi", "spawn", "world.poi.", payload: "spawn");

            Assert.AreSame(first, repeated,
                "高频悬停命中不得为同一个稳定目标反复创建选择对象。");
            Assert.AreEqual("world.poi.spawn", first.StableId);
            Assert.AreEqual(1, cache.Count);

            ESWorkbenchSelection changedPayload = cache.GetOrCreateLocal(
                "world.poi", "spawn", "world.poi.", payload: "spawn.v2");
            Assert.AreNotSame(first, changedPayload,
                "同一 StableId 进入新对象代际时必须替换旧选择合同。");
            Assert.IsTrue(cache.Invalidate("world.poi.spawn"));
            ESWorkbenchSelection invalidatedReplacement = cache.GetOrCreateLocal(
                "world.poi", "spawn", "world.poi.", payload: "spawn.v2");
            Assert.AreNotSame(changedPayload, invalidatedReplacement,
                "局部失效不能只删主表，不能从本地 ID 表返回旧对象。");
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.AreNotSame(
                changedPayload,
                cache.GetOrCreateLocal("world.poi", "spawn", "world.poi.", payload: "spawn.v2"));
        }

        [Test]
        public void SelectionCacheEvictsOldestGenerationWithinConfiguredBound()
        {
            var cache = new ESWorkbenchSelectionCache(maximumEntries: 2);
            ESWorkbenchSelection first = cache.GetOrCreateLocal(
                "world.poi", "first", "world.poi.", payload: "first");
            ESWorkbenchSelection second = cache.GetOrCreateLocal(
                "world.poi", "second", "world.poi.", payload: "second");
            cache.GetOrCreateLocal("world.poi", "third", "world.poi.", payload: "third");

            Assert.AreEqual(2, cache.Count);
            Assert.AreEqual(2, cache.MaximumEntries);
            ESWorkbenchSelection firstReplacement = cache.GetOrCreateLocal(
                "world.poi", "first", "world.poi.", payload: "first");
            Assert.AreNotSame(first, firstReplacement,
                "超过容量后最旧选择代际必须可重建，不能从本地 ID 表泄漏。 ");
            Assert.AreNotSame(second, cache.GetOrCreateLocal(
                "world.poi", "second", "world.poi.", payload: "second"),
                "重新建立最旧目标时应继续淘汰下一个最旧代际，保持容量上限确定。 ");
        }

        [Test]
        public void RendererBoundsCacheReusesRendererSetAndTracksLiveBounds()
        {
            GameObject root = new GameObject("RendererBoundsCacheRoot");
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = "RendererBoundsCacheChild";
            child.transform.SetParent(root.transform, worldPositionStays: false);
            child.transform.localPosition = Vector3.zero;
            var cache = new ESWorkbenchRendererBoundsCache();
            try
            {
                Bounds initial = cache.Calculate(root);
                Bounds repeated = cache.Calculate(root);

                Assert.AreEqual(1, cache.RendererSetBuildCount,
                    "同一预览对象的命中/绘制查询不能重复遍历 Renderer 子树。");
                Assert.AreEqual(initial.center, repeated.center);
                Assert.AreEqual(initial.size, repeated.size);

                child.transform.localPosition = new Vector3(4f, 0f, 0f);
                Bounds moved = cache.Calculate(root);
                Assert.AreEqual(1, cache.RendererSetBuildCount);
                Assert.That(moved.center.x, Is.EqualTo(initial.center.x + 4f).Within(0.001f));

                cache.Clear();
                Assert.AreEqual(0, cache.RendererSetBuildCount);
                cache.Calculate(root);
                Assert.AreEqual(1, cache.RendererSetBuildCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void StrokeSamplerFillsGapsFlushesEndpointAndResetsBetweenStrokes()
        {
            var sampler = new ESWorkbenchStrokeSampler();
            var samples = new List<Vector3>();

            Assert.AreEqual(1, sampler.Sample(Vector3.zero, 3f, samples.Add));
            Assert.AreEqual(0, sampler.Sample(new Vector3(2f, 0f, 0f), 3f, samples.Add));
            Assert.AreEqual(3, sampler.Sample(new Vector3(10f, 0f, 0f), 3f, samples.Add));
            Assert.AreEqual(1, sampler.Flush(samples.Add));
            CollectionAssert.AreEqual(
                new[] { 0f, 3f, 6f, 9f, 10f },
                samples.Select(value => value.x).ToArray());

            sampler.Reset();
            Assert.AreEqual(1, sampler.Sample(new Vector3(100f, 0f, 0f), 3f, samples.Add));
            Assert.AreEqual(100f, samples[samples.Count - 1].x);
        }

        [Test]
        public void StrokeSamplerCapsLargePointerJumpsWithoutDroppingTheEndpoint()
        {
            var sampler = new ESWorkbenchStrokeSampler();
            var samples = new List<Vector3>();

            Assert.AreEqual(1, sampler.Sample(Vector3.zero, 0.001f, samples.Add, 4));
            Assert.AreEqual(4, sampler.Sample(new Vector3(100000f, 0f, 0f), 0.001f, samples.Add, 4));
            Assert.AreEqual(100000f, samples[samples.Count - 1].x, 0.001f);
            Assert.AreEqual(5, samples.Count,
                "超大跨度事件必须受上限保护，不能在主线程执行百万级补点。");
        }

        [Test]
        public void LatestValueCoalescerKeepsOnlyLatestValueAndFlushesEndpoint()
        {
            var coalescer = new ESWorkbenchLatestValueCoalescer<int>(32f);
            coalescer.Queue(1, 10d);
            coalescer.Queue(2, 10.01d);

            Assert.IsTrue(coalescer.HasPending);
            Assert.IsFalse(coalescer.TryConsume(10.02d, out _),
                "合帧窗口未到时不得提前提交。");
            Assert.IsTrue(coalescer.TryConsume(10.05d, out int latest));
            Assert.AreEqual(2, latest,
                "高频输入只提交最后一个值，避免重复重建预览模型。");
            Assert.IsFalse(coalescer.HasPending);

            coalescer.Queue(3, 20d);
            Assert.IsTrue(coalescer.Flush(out int endpoint));
            Assert.AreEqual(3, endpoint);
            Assert.IsFalse(coalescer.HasPending);
            Assert.IsFalse(coalescer.Flush(out _));
        }

        [Test]
        public void LatestValueCoalescerClampsTimeRegressionAndCancelClearsPending()
        {
            var coalescer = new ESWorkbenchLatestValueCoalescer<string>(24f);
            coalescer.Queue("first", 5d);
            coalescer.Queue("latest", 4d);

            Assert.GreaterOrEqual(coalescer.RemainingMilliseconds(4d), 24d - 0.001d);
            Assert.IsFalse(coalescer.TryConsume(4.01d, out _),
                "时间倒退不得让预览提前提交。");
            Assert.IsTrue(coalescer.TryConsume(5.024d, out string value));
            Assert.AreEqual("latest", value);

            coalescer.Queue("cancelled", 8d);
            coalescer.Cancel();
            Assert.IsFalse(coalescer.HasPending);
            Assert.IsFalse(coalescer.TryConsume(9d, out _));
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

            public IReadOnlyList<ESWorkbenchCommandDescriptor> CommandsForTest => ESWorkbench_Commands;

            public void InitializeForTest() => base.ESWindow_OnHostEnable();
            public void ReleaseForTest() => ESWorkbench_ReleaseContributions();
            public void ReloadForTest() => ESWorkbench_LoadContributions();
            public void BindAssetForTest(TestAsset asset) => ESWorkbench_BindAsset(asset);
            public void RegisterDocumentForTest(string documentId, Action release) => ESWorkbench_RegisterDocument(
                new ESWorkbenchDocumentDefinition(
                    documentId,
                    documentId,
                    documentId,
                    false,
                    ESWorkbenchDirtyFlags.Authoring,
                    () => { },
                    release: release));
            public void SelectDocumentForTest(string documentId) => ESWorkbench_SelectDocument(documentId);
            public int DocumentCountForTest => ESWorkbench_Documents.Count;
            public string SelectedDocumentIdForTest => ESWorkbench_SelectedDocumentId;
            public void MarkDirtyForTest(string key, ESWorkbenchDirtyFlags flags) => ESWorkbench_MarkDirty(key, flags);
            protected override void ESWorkbench_OnDirtyStateChanged(string dirtyKey, ESWorkbenchDirtyFlags flags)
            {
                LastDirtyKeyForTest = dirtyKey;
                LastDirtyFlagsForTest = flags;
            }
            public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("Test Workbench");
        }

        private class StubViewport : IESWorkbenchViewport
        {
            public VisualElement Root { get; } = new VisualElement();
            public bool Disposed { get; private set; }
            public int ActivateCount { get; private set; }
            public int DeactivateCount { get; private set; }
            public void Activate() => ActivateCount++;
            public void Deactivate() => DeactivateCount++;
            public void Refresh(ESWorkbenchRefreshReason reason) { }
            public bool CanAccept(ESWorkbenchObjectDescriptor item) => true;
            public bool TryAccept(ESWorkbenchDropContext context, out string message)
            {
                message = string.Empty;
                return true;
            }
            public void Dispose() => Disposed = true;
        }

        private sealed class StubStatusViewport : StubViewport, IESWorkbenchViewportStatusProvider
        {
            public IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetStatusSnapshot()
            {
                return Enumerable.Range(0, 6)
                    .Select(index => new ESWorkbenchViewportStatusDescriptor(
                        "status-" + index,
                        "状态" + index,
                        "值" + index,
                        priority: 100 - index))
                    .ToArray();
            }
        }

        private sealed class DiagnosticStubViewport : StubViewport, IESWorkbenchViewportDropDiagnostics
        {
            private readonly string reason;

            public DiagnosticStubViewport(string reason)
            {
                this.reason = reason;
            }

            public bool CanAccept(ESWorkbenchObjectDescriptor item, out string rejectionReason)
            {
                rejectionReason = reason;
                return false;
            }
        }
    }
}
#endif
