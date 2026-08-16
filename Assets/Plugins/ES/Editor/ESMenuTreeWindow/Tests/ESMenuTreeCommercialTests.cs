using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
                "d_Folder Icon",
                ESEditorPresentation.ResolveDefaultWindowIconName(
                    typeof(RuntimeContractWindow), "ES 工具窗口", ""));

            Assert.AreEqual(
                "agent",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "Agent 工作台", "自动化与开发/Agent"));
            Assert.AreEqual(
                "diagnostics",
                ESEditorPresentation.ResolveESBrandIconResourceName(
                    typeof(RuntimeContractWindow), "性能诊断", "验证与诊断/性能"));
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
            : ESMenuTreeWindow<DefaultSemiSleepContractWindow>
        {
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
                ESWindowFoundation.Unbind(window, true);
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
                ESWindowFoundation.Unbind(window, true);
                UnityEngine.Object.DestroyImmediate(window);
            }
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
            }
            finally
            {
                ESWindowFoundation.Unbind(window, true);
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
                Assert.AreEqual(
                    ESWindowSleepLinkMode.OwnedSurface,
                    ESWindowFoundation.GetSleepLinkMode(child));
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(child));
                ESWindowFoundation.ClearSleepOwner(child);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child));

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    owner,
                    ESWindowSleepLinkMode.FollowOwner));
                ESWindowFoundation.Unbind(owner, true);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(child),
                    "父窗口关闭或解绑后，子窗口必须解除跟随并继续作为独立窗口存在。");
                Assert.IsNotNull(child);
            }
            finally
            {
                ESWindowFoundation.Unbind(child, true);
                ESWindowFoundation.Unbind(owner, true);
                UnityEngine.Object.DestroyImmediate(child);
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
                ESWindowFoundation.Unbind(child, true);
                ESWindowFoundation.Unbind(owner, true);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
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
                ESWindowFoundation.Unbind(window, true);
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

                ESWindowFoundation.Unbind(owner, true);
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
                ESWindowFoundation.Unbind(owner, true);
                Assert.IsFalse(
                    relationship.SleepOwnerDetachedByClose,
                    "Domain Reload 期间只释放活动引用，不得把声明关系误记为用户关闭后的永久脱离。");
            }
            finally
            {
                reloadFlag.SetValue(null, false);
                ESWindowFoundation.Unbind(child, true);
                ESWindowFoundation.Unbind(owner, true);
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
                    typeof(UnityEditor.EditorWindow)
                },
                null);
            Assert.IsNotNull(
                assetPreviewOpen,
                "资产记录预览必须由打开方显式传入 EditorWindow owner。");
            Assert.IsNotNull(
                typeof(ESAssetPackageBakeWindow).GetMethod(
                    "ESWindow_OnHostEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                "资产包主窗口必须在恢复时按稳定 ownerKey 解析 PendingFollowOwner。");
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
                ESWindowFoundation.Unbind(window, true);
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
            Assert.IsNotNull(typeof(ESAssetPackageDynamicPreviewPlayer).GetMethod("DisposeInstance", BindingFlags.Instance | BindingFlags.NonPublic));
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
