using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ES.EditorInternal;

namespace ES.Tests
{
    public sealed class ESMenuTreeCommercialTests
    {
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

        public sealed class RuntimeContractWindow : ESMenuTreeWindow<RuntimeContractWindow>
        {
            protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
            {
                builder.Add("declared.page", "声明 / 页面", new EmptyPage());
            }

            protected override void ESWindow_BuildActionHosts(ESWindowActionHosts hosts)
            {
                hosts.AddButton(ESWindowActionScope.System, "系统扩展", "测试系统域", () => { });
                hosts.AddButton(ESWindowActionScope.Global, "全局扩展", "测试全局域", () => { });
                hosts.AddButton(ESWindowActionScope.Window, "窗口扩展", "测试窗口域", () => { });
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
            }
            finally
            {
                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                    node.Page?.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
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
                    4,
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
                BindingFlags.Static | BindingFlags.NonPublic);
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
        public void SemiSleepDragUsesBoundedIncrementalMovement()
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
            Assert.LessOrEqual(huge.x - current.x, 160f + 0.001f);
            Assert.GreaterOrEqual(huge.xMin, tray.xMin);
            Assert.LessOrEqual(huge.xMax, tray.xMax);
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
        }

        [Test]
        public void SemiSleepControlsRequireDeclaredHostAndUseResponsiveOverflow()
        {
            Assert.IsFalse(ESEditorPresentation.HasDeclaredSystemActionHost(
                new ESWindowActionHosts()));
            Assert.IsTrue(ESEditorPresentation.HasDeclaredSystemActionHost(
                new ESWindowActionHosts(system: new VisualElement())));
            Assert.IsFalse(ESEditorPresentation.ShouldCompactSystemActions(1174f));
            Assert.IsTrue(ESEditorPresentation.ShouldCompactSystemActions(640f));
            Assert.IsFalse(ESCmdAgentWindow.ShouldCollapseHeaderActions(1500f));
            Assert.IsTrue(ESCmdAgentWindow.ShouldCollapseHeaderActions(1174f));
        }

        [Test]
        public void AdvancedDialogContractsStableAuxiliaryActionsAndFieldValidation()
        {
            var request = new ESAdvancedDialogRequest();
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
                title = "异步模态拒绝测试",
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
                        null,
                        ESDialogTone.Info,
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
        public void ModalDialogRejectsQueuePolicyBeforeOpeningWindow()
        {
            var request = new ESAdvancedDialogRequest
            {
                title = "队列模态拒绝测试",
                duplicatePolicy = ESDialogDuplicatePolicy.Queue,
            };
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ESDialogService.ShowModal(request));
            StringAssert.Contains("ShowAsync", exception.Message);
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
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeSystemActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeSystemActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeGlobalActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeGlobalActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeWindowActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreeWindowActions"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreePageActionRow"));
                Assert.IsNotNull(root.Q<VisualElement>("ESMenuTreePageActions"));

                Assert.IsTrue(root.Q<VisualElement>("ESMenuTreeSystemActions")
                    .Children().OfType<Button>().Any(button => button.text == "系统扩展"));
                Assert.IsTrue(root.Q<VisualElement>("ESMenuTreeGlobalActions")
                    .Children().OfType<Button>().Any(button => button.text == "全局扩展"));
                Assert.IsTrue(root.Q<VisualElement>("ESMenuTreeWindowActions")
                    .Children().OfType<Button>().Any(button => button.text == "窗口扩展"));

                MethodInfo refreshGlobalActions =
                    typeof(ESMenuTreeWindow<RuntimeContractWindow>).GetMethod(
                        "UpdateGlobalActionToolbar",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(refreshGlobalActions);
                refreshGlobalActions.Invoke(window, null);
                Assert.IsTrue(root.Q<VisualElement>("ESMenuTreeGlobalActions")
                    .Children().OfType<Button>().Any(button => button.text == "全局扩展"),
                    "刷新框架内建全局动作时，不得清除窗口注入的全局动作。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
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
        public void SemiSleepStressGridDistributesTwentyWindowsInsideMainBounds()
        {
            Assert.AreEqual(20, ESWindowSemiSleepStressTest.ConfiguredWindowCount);
            Rect main = new Rect(100f, 80f, 1800f, 1000f);
            Rect[] bounds = Enumerable.Range(0, 20)
                .Select(index => ESWindowSemiSleepStressTest.BuildSleepBounds(main, index))
                .ToArray();

            Assert.AreEqual(20, bounds.Distinct().Count());
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
