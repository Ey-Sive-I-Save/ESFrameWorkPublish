#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        private const string Owner = "ES.Tests.WorkbenchFoundation";

        [TearDown]
        public void TearDown()
        {
            ESWorkbenchContributionRegistry.ClearOwner(Owner);
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
            Assert.IsTrue(ESWorkbenchContributionRegistry.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor(
                    workbenchId,
                    "authoring",
                    "Authoring",
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

            using (ESWorkbenchContributionSession session = Open(workbenchId))
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
            Assert.IsTrue(ESWorkbenchContributionRegistry.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor(
                    workbenchId,
                    "duplicates",
                    "Duplicates",
                    ESWorkbenchContributionCategory.Validation,
                    context =>
                    {
                        context.RegisterTool(new ESWorkbenchToolDescriptor("same", "First", _ => { }));
                        context.RegisterTool(new ESWorkbenchToolDescriptor("same", "Second", _ => { }));
                        return null;
                    },
                    owner: Owner),
                out string registrationMessage), registrationMessage);

            using (ESWorkbenchContributionSession session = Open(workbenchId))
            {
                Assert.AreEqual(1, session.Tools.Count);
                Assert.AreEqual("First", session.Tools[0].DisplayName);
                CollectionAssert.Contains(session.Diagnostics, "工具 ID 冲突：same，已保留首次声明。");
            }
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
        public void DynamicObjectSourceObservesDataChangesWithoutReopeningContributionSession()
        {
            string workbenchId = "tests.workbench.dynamic-source";
            var sourceItems = new List<ESWorkbenchObjectDescriptor>
            {
                new ESWorkbenchObjectDescriptor("first", "First", "Tests", null)
            };
            Assert.IsTrue(ESWorkbenchContributionRegistry.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor(
                    workbenchId,
                    "dynamic",
                    "Dynamic",
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        context.RegisterObjectSource(new ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>(
                            "tests.dynamic.objects", _ => sourceItems));
                        return null;
                    },
                    owner: Owner),
                out string registrationMessage), registrationMessage);

            using (ESWorkbenchContributionSession session = Open(workbenchId))
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

        private static ESWorkbenchContributionSession Open(string workbenchId)
        {
            return ESWorkbenchContributionRegistry.Open(
                workbenchId,
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

        private sealed class TestWorkbenchWindow : ESWorkbenchWindowBase<TestWorkbenchWindow, TestAsset>
        {
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
