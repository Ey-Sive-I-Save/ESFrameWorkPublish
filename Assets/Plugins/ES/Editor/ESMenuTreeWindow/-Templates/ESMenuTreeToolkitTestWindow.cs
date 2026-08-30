using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>
    /// 只验证新版 UI Toolkit 菜单树宿主的导航、生命周期和状态呈现，不承载业务数据。
    /// </summary>
    public sealed class ESMenuTreeToolkitTestWindow : ESMenuTreeWindow<ESMenuTreeToolkitTestWindow>
    {
        public override string ESWindow_PresentationShortTitle => "测试";

        private const string RuntimePanelId = "runtime.injected.panel";
        private const string RuntimePanelOwnerId = "test.menu.runtime-panel";

        [NonSerialized] private ToolkitOdinTestPage odinTestPage;
        [NonSerialized] private Page_FontBuild legacyFontPage;
        [NonSerialized] private AdvancedPanelState advancedState;
        [NonSerialized] private bool failNextWindowBuild;
        [NonSerialized] private int runtimePanelRevision;
        [NonSerialized] private bool draftPending;
        [NonSerialized] private string savedDraftValue = "已保存的商业配置";
        [NonSerialized] private string draftValue = "已保存的商业配置";
        [NonSerialized] private ESMenuTreePageContext draftContext;
        [NonSerialized] private TextField draftField;
        [NonSerialized] private Label draftStatusLabel;
        [NonSerialized] private Label taskStatusLabel;

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/新版 UI Toolkit 菜单树测试", false, 9160)]
        private static void OpenMenu()
        {
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("新版菜单树测试", "验证 ES UI Toolkit MenuTreeWindow");
        }

        protected override string ESWindow_Subtitle => "仅验证导航与生命周期，不连接业务数据";

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            maxSize = new Vector2(1400f, 1000f);
        }

        protected override void ESWindow_BuildGlobalActions(
            ICollection<ESMenuTreeGlobalAction> actions)
        {
            if (failNextWindowBuild)
            {
                failNextWindowBuild = false;
                throw new InvalidOperationException("测试窗口主动注入的一次性外壳扩展点异常。");
            }
            actions.Add(new ESMenuTreeGlobalAction(
                    "runtime.add",
                    "添加临时页",
                    "通过窗口契约注册一个由 ownerId 管理的运行时菜单页面。",
                    AddRuntimePanel)
                .WithIcon(EditorIcons.Plus)
                .WhenVisible(() => !RuntimePanelExists())
                .WithPriority(120));
            actions.Add(new ESMenuTreeGlobalAction(
                    "runtime.update",
                    "更新临时页",
                    "替换运行时页面定义并保持 StableId 与 ownerId。",
                    UpdateRuntimePanel)
                .WithIcon(EditorIcons.Refresh)
                .WhenVisible(RuntimePanelExists)
                .WithPriority(110));
            actions.Add(new ESMenuTreeGlobalAction(
                    "runtime.query",
                    "查询临时页",
                    "读取当前页面定义、ownerId 和菜单注册数量。",
                    QueryRuntimePanel)
                .WithIcon(EditorIcons.MagnifyingGlass)
                .WhenVisible(RuntimePanelExists)
                .WithPriority(100));
            actions.Add(new ESMenuTreeGlobalAction(
                    "runtime.remove",
                    "移除临时页",
                    "仅允许持有同一 ownerId 的代码移除运行时页面。",
                    RemoveRuntimePanel)
                .WithIcon(EditorIcons.X)
                .WhenVisible(RuntimePanelExists)
                .WithPriority(90));
        }

        protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            builder.Add("demo.root", "演示", new ToolkitTestPage(
                "根菜单页面", "这个节点既是可选页面，也拥有布局、状态和恢复三个子层级。"));
            builder.Add("layout.root", "演示 / 布局", new ToolkitTestPage(
                "布局菜单页面", "中间层菜单同样可以承载页面，并继续拥有下一级节点。"));
            builder.Add("layout.overview", "演示 / 布局 / 概览", new ToolkitTestPage(
                "布局概览", "这是一个无业务意义的叶页面，用于验证首屏、滚动和状态栏。"));
            builder.Add("layout.deep", "演示 / 布局 / 高级 / 第三级", new ToolkitTestPage(
                "第三级页面", "多层路径应保持稳定展开状态，并且搜索命中时自动展开祖先节点。"));
            builder.Add("state.ready", "演示 / 状态 / 就绪", new ToolkitTestPage(
                "就绪状态", "页面可以报告成功、修改、只读、警告和错误状态。"));
            builder.Add("state.warning", "演示 / 状态 / 警告", new ToolkitTestPage(
                "警告状态", "警告只影响状态反馈，不阻断其他页面导航。"));
            builder.Add("recovery.reload", "演示 / 恢复 / 域重载", new ToolkitTestPage(
                "恢复状态", "窗口重开或域重载后，页面实例应释放并重新创建。"));
            builder.Add(new ESMenuTreePageDefinition(
                    "recovery.failure",
                    "演示 / 恢复 / 可恢复异常",
                    new ToolkitRecoverablePage())
                .WithIcon(EditorIcons.AlertTriangle)
                .WithKeywords("异常 CreateView 重试 原因 影响 恢复 外壳事务")
                .AddPageAction(new ESMenuTreePageAction(
                        "arm-window-failure",
                        "验证外壳恢复",
                        "令下一次整窗构建抛出一次异常，验证事务清理和恢复入口。",
                        _ => ArmWindowBuildFailure())
                    .WithIcon(EditorIcons.AlertTriangle)
                    .WithPriority(20)));
            builder.AddPanel(
                "panel.quick",
                "演示 / 面板 / 快速功能页",
                BuildQuickPanel,
                EditorIcons.Play.Active,
                "声明式 UI Toolkit 功能面板 生命周期 局部重建");
            advancedState ??= new AdvancedPanelState();
            var advancedPage = new ESMenuTreePanelPage(BuildAdvancedPanel)
                .WithOnShow(OnAdvancedPanelShow)
                .WithOnRefresh(OnAdvancedPanelRefresh)
                .WithOnHide(OnAdvancedPanelHide)
                .WithOnReleaseView(OnAdvancedPanelReleaseView)
                .WithOnDispose(OnAdvancedPanelDispose);
            builder.Add(new ESMenuTreePageDefinition(
                    "panel.advanced",
                    "演示 / 面板 / 高级状态面板",
                    advancedPage)
                .WithLayout(ESMenuTreePageLayout.Compact, 880f, 18f)
                .WithIcon(EditorIcons.SettingsCog)
                .WithKeywords("高级 状态 徽标 动态动作 生命周期 合并重建 异常刷新")
                .AddPageAction(new ESMenuTreePageAction(
                        "toggle-mode",
                        "高级模式",
                        "切换页面模式并验证工具动作选中态。",
                        ToggleAdvancedMode)
                    .WithIcon(EditorIcons.SettingsCog)
                    .WithCheckedState(_ => advancedState.AdvancedMode)
                    .WithPriority(120))
                .AddPageAction(new ESMenuTreePageAction(
                        "advanced-notice",
                        "高级提示",
                        "仅在高级模式下出现，验证动态可见动作。",
                        context => context.Notify(
                            "高级页面动作执行成功",
                            ESMenuTreePageStatus.Ready,
                            ESEditorFeedbackSoundKind.Success))
                    .WithIcon(EditorIcons.Bell)
                    .WhenVisible(_ => advancedState.AdvancedMode)
                    .WithPriority(100))
                .AddPageAction(new ESMenuTreePageAction(
                        "clear-badge",
                        "清除徽标",
                        "清除当前页面菜单徽标。",
                        ClearAdvancedBadge)
                    .WithIcon(EditorIcons.Bell)
                    .When(_ => advancedState.HasMenuBadge)
                    .WithPriority(80))
                .AddPageAction(new ESMenuTreePageAction(
                        "fail-refresh",
                        "刷新异常",
                        "令下一次刷新抛出一次可恢复异常。",
                        ArmAdvancedRefreshFailure)
                    .WithIcon(EditorIcons.AlertTriangle)
                    .WithPriority(20)));
            builder.AddPanel(
                "panel.responsive",
                "演示 / 面板 / 窄窗与长文本",
                BuildResponsivePanel,
                EditorIcons.MagnifyingGlass.Active,
                "窄窗口 长中文 表单 换行 单滚动容器");
            var draftPage = new ESMenuTreePanelPage(BuildDraftPanel)
                .WithOnReleaseView(ReleaseDraftPanelView)
                .WithPendingChanges(
                    () => draftPending,
                    SaveDraftChanges,
                    DiscardDraftChanges,
                    () => "商业配置草稿尚未保存；切页或重建前必须明确处理。 ");
            builder.Add(new ESMenuTreePageDefinition(
                    "panel.draft",
                    "演示 / 面板 / 未保存修改保护",
                    draftPage)
                .WithIcon(EditorIcons.AlertTriangle)
                .WithKeywords("Dirty Draft Save Discard Cancel 页面离开保护")
                .WithLayout(ESMenuTreePageLayout.Compact, 760f, 18f));
            builder.Add(new ESMenuTreePageDefinition(
                    "panel.serialized",
                    "演示 / 面板 / SerializedProperty 多目标",
                    new ToolkitSerializedPanelPage())
                .WithIcon(EditorIcons.SettingsCog)
                .WithKeywords("SerializedObject PropertyField Undo Dirty Prefab Mixed 多目标")
                .WithLayout(ESMenuTreePageLayout.Compact, 820f, 18f));
            var taskPage = new ESMenuTreePanelPage(BuildTaskPanel)
                .WithOnReleaseView(() => taskStatusLabel = null);
            builder.Add(new ESMenuTreePageDefinition(
                    "panel.task",
                    "演示 / 面板 / 页面局部任务",
                    taskPage)
                .WithIcon(EditorIcons.Play)
                .WithKeywords("Task CancellationToken Cancel Release DomainReload 局部任务")
                .WithLayout(ESMenuTreePageLayout.Compact, 760f, 18f));
            odinTestPage ??= new ToolkitOdinTestPage();
            builder.Add(ESMenuTreePageDefinition
                .ForOdin("odin.serialized", "演示 / Odin / 序列化内容", odinTestPage)
                .WithLayout(ESMenuTreePageLayout.Compact, 820f, 16f)
                .WithIcon(EditorIcons.SettingsCog)
                .WithKeywords("Odin PropertyTree 序列化 Undo 历史页面迁移")
                .WithSelectionFeedback(
                    "已打开 Odin 序列化兼容页",
                    ESEditorFeedbackSoundKind.Navigate,
                    true)
                .AddPageAction(new ESMenuTreePageAction(
                        "push-success",
                        "推送提示",
                        "聚合状态栏、窗口通知、动效和成功音效。",
                        context => context.Notify(
                            "页面上下文已生效：" + context.StableId + " / " + context.Path,
                            ESMenuTreePageStatus.Ready,
                            ESEditorFeedbackSoundKind.Success))
                    .WithIcon(EditorIcons.Bell)
                    .WithPriority(100))
                .AddPageAction(new ESMenuTreePageAction(
                        "rebuild-view",
                        "局部重建",
                        "仅重建当前页面视图并保留菜单、搜索与历史。",
                        context => context.RebuildView())
                    .WithIcon(EditorIcons.Refresh)
                    .WithPriority(80))
                .AddPageAction(new ESMenuTreePageAction(
                        "open-deep-page",
                        "前往第三级",
                        "使用页面上下文驱动到第三级测试页。",
                        context => context.SelectPage("layout.deep"))
                    .WithIcon(EditorIcons.ArrowRight)
                    .WithPriority(40))
                .AddPageAction(new ESMenuTreePageAction(
                        "push-warning",
                        "警告提示",
                        "验证折叠菜单中的页面动作、状态和音效。",
                        context => context.Notify(
                            "这是来自折叠菜单的页面级警告",
                            ESMenuTreePageStatus.Warning,
                            ESEditorFeedbackSoundKind.Warning))
                    .WithIcon(EditorIcons.AlertTriangle)));

            legacyFontPage ??= new Page_FontBuild();
            builder.Add(ESMenuTreePageDefinition
                .ForOdin("migration.font", "迁移验证 / 历史字体页", legacyFontPage)
                .WithLayout(ESMenuTreePageLayout.Inspector, 1120f, 18f)
                .WithIcon(EditorIcons.Sound)
                .WithKeywords("真实历史页面 Page_FontBuild InlineEditor OnInspectorGUI")
                .WithSelectionFeedback(
                    "历史 Page_FontBuild 已按新版 Inspector 布局承载",
                    ESEditorFeedbackSoundKind.Open)
                .AddPageAction(new ESMenuTreePageAction(
                        "locate-profile",
                        "定位配置",
                        "在 Project 窗口定位当前 Font Build Profile。",
                        LocateFontProfile)
                    .WithIcon(EditorIcons.MagnifyingGlass)
                    .WithPriority(100)
                    .When(context => context.GetOdinTarget<Page_FontBuild>()?.profile != null)));
        }

        private bool RuntimePanelExists()
        {
            return TryGetRuntimePageOwner(RuntimePanelId, out string ownerId)
                && string.Equals(ownerId, RuntimePanelOwnerId, StringComparison.Ordinal);
        }

        private void AddRuntimePanel()
        {
            int revision = 1;
            ESMenuTreePageDefinition definition = CreateRuntimePanelDefinition(revision);
            ESMenuTreeMutationResult result = AddRuntimePage(
                RuntimePanelOwnerId,
                definition,
                true);
            if (result.Succeeded)
            {
                runtimePanelRevision = revision;
                PublishWindowFeedback(
                    "运行时菜单面板已注册并选中",
                    ESMenuTreePageStatus.Ready,
                    ESEditorFeedbackSoundKind.Success);
                return;
            }
            PublishWindowFeedback(
                result.Error,
                ESMenuTreePageStatus.Error,
                ESEditorFeedbackSoundKind.Error);
        }

        private void UpdateRuntimePanel()
        {
            int revision = Mathf.Max(1, runtimePanelRevision + 1);
            ESMenuTreeMutationResult result = UpdateRuntimePage(
                RuntimePanelOwnerId,
                CreateRuntimePanelDefinition(revision));
            if (result.Succeeded)
            {
                runtimePanelRevision = revision;
                PublishWindowFeedback(
                    "临时菜单面板已局部更新到版本 " + revision,
                    ESMenuTreePageStatus.Modified,
                    ESEditorFeedbackSoundKind.Refresh);
                return;
            }
            PublishWindowFeedback(
                result.Error,
                ESMenuTreePageStatus.Error,
                ESEditorFeedbackSoundKind.Error);
        }

        private void QueryRuntimePanel()
        {
            if (TryGetPageDefinition(RuntimePanelId, out ESMenuTreePageDefinition definition)
                && TryGetRuntimePageOwner(RuntimePanelId, out string ownerId))
            {
                PublishWindowFeedback(
                    "临时页：" + definition.Path
                    + " | 所有者：" + ownerId
                    + " | 当前注册：" + GetPageDefinitions().Count,
                    ESMenuTreePageStatus.Info,
                    ESEditorFeedbackSoundKind.Confirm);
                return;
            }
            PublishWindowFeedback(
                "临时菜单面板不存在",
                ESMenuTreePageStatus.Warning,
                ESEditorFeedbackSoundKind.Warning);
        }

        private void RemoveRuntimePanel()
        {
            ESMenuTreeMutationResult result = RemoveRuntimePage(RuntimePanelOwnerId, RuntimePanelId);
            if (result.Succeeded)
            {
                runtimePanelRevision = 0;
                PublishWindowFeedback(
                    "临时菜单面板已移除",
                    ESMenuTreePageStatus.Ready,
                    ESEditorFeedbackSoundKind.Confirm);
                return;
            }
            PublishWindowFeedback(
                result.Error,
                ESMenuTreePageStatus.Error,
                ESEditorFeedbackSoundKind.Error);
        }

        private ESMenuTreePageDefinition CreateRuntimePanelDefinition(int revision)
        {
            return ESMenuTreePageDefinition.ForPanel(
                    RuntimePanelId,
                    "运行时菜单 / 动态功能面板",
                    (context, content) => BuildRuntimeInjectedPanel(context, content, revision))
                .WithIcon(EditorIcons.Play)
                .WithKeywords("运行时 Runtime CRUD Owner Ownership Revision")
                .WithLayout(ESMenuTreePageLayout.Compact, 760f, 18f)
                .AddPageAction(new ESMenuTreePageAction(
                        "runtime.notify",
                        "推送版本",
                        "使用当前页面上下文发布临时面板版本。",
                        context => context.Notify(
                            "临时面板版本 " + revision + "，所有者 " + RuntimePanelOwnerId,
                            ESMenuTreePageStatus.Info,
                            ESEditorFeedbackSoundKind.Confirm))
                    .WithIcon(EditorIcons.Bell)
                    .WithPriority(100));
        }

        private static void BuildRuntimeInjectedPanel(
            ESMenuTreePageContext context,
            VisualElement content,
            int revision)
        {
            content.Add(ESEditorPanelUI.CreateHeading(
                "运行时菜单面板 · 版本 " + revision,
                "该页通过 ownerId 隔离的 CRUD 契约创建；更新和移除不会重建整个窗口外壳。"));
            content.Add(ESEditorPanelUI.CreateNotice(
                "页面所有者",
                RuntimePanelOwnerId,
                ESMenuTreePageStatus.Info));
            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "返回高级状态面板",
                    "使用稳定页面 ID 驱动选中。",
                    () => context.SelectPage("panel.advanced"),
                    true)));
        }

        private void ArmWindowBuildFailure()
        {
            failNextWindowBuild = true;
            ES_RefreshWindow();
        }

        private void BuildDraftPanel(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            draftContext = context;
            content.Add(ESEditorPanelUI.CreateHeading(
                "未保存修改保护",
                "该页使用页面离开合同验证保存、放弃和取消，不把草稿缓存当成业务真源。"));
            draftField = new TextField { value = draftValue };
            draftField.RegisterValueChangedCallback(evt =>
            {
                draftValue = evt.newValue ?? string.Empty;
                draftPending = !string.Equals(draftValue, savedDraftValue, StringComparison.Ordinal);
                if (draftPending)
                    context.SetMenuBadge("未保存", ESMenuTreePageStatus.Modified);
                else
                    context.ClearMenuBadge();
                context.RefreshPendingChanges();
                UpdateDraftStatus();
            });
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "商业配置名称",
                draftField,
                "修改后切换页面或重建窗口，应先出现保存、放弃、取消选择。"));
            draftStatusLabel = new Label();
            draftStatusLabel.style.marginTop = 12f;
            draftStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            content.Add(draftStatusLabel);
            UpdateDraftStatus();
        }

        private bool SaveDraftChanges()
        {
            savedDraftValue = draftValue;
            draftPending = false;
            draftContext?.ClearMenuBadge();
            draftContext?.RefreshPendingChanges();
            UpdateDraftStatus();
            return true;
        }

        private void DiscardDraftChanges()
        {
            draftValue = savedDraftValue;
            draftPending = false;
            draftField?.SetValueWithoutNotify(draftValue);
            draftContext?.ClearMenuBadge();
            draftContext?.RefreshPendingChanges();
            UpdateDraftStatus();
        }

        private void ReleaseDraftPanelView()
        {
            draftContext = null;
            draftField = null;
            draftStatusLabel = null;
        }

        private void UpdateDraftStatus()
        {
            if (draftStatusLabel == null)
                return;
            draftStatusLabel.text = draftPending
                ? "状态：存在未保存草稿 | 已保存值：" + savedDraftValue
                : "状态：已保存 | 当前值：" + savedDraftValue;
        }

        private void BuildTaskPanel(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            content.Add(ESEditorPanelUI.CreateHeading(
                "页面作用域任务",
                "任务以稳定 ID 去重，页面上下文失效时自动取消，完成回调只返回当前页面。"));
            taskStatusLabel = new Label("状态：空闲");
            taskStatusLabel.style.marginTop = 12f;
            content.Add(taskStatusLabel);
            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "启动短任务",
                    "启动一个可取消的页面作用域异步任务。",
                    () => StartTaskTest(context),
                    true),
                ESEditorPanelUI.CreateButton(
                    "取消任务",
                    "取消当前页面拥有的短任务。",
                    () =>
                    {
                        if (context.CancelTask("commercial-delay") && taskStatusLabel != null)
                            taskStatusLabel.text = "状态：正在取消";
                    })));
        }

        private void StartTaskTest(ESMenuTreePageContext context)
        {
            bool started = context.RunTask(
                "commercial-delay",
                async cancellation => await Task.Delay(1200, cancellation),
                result =>
                {
                    if (taskStatusLabel != null)
                        taskStatusLabel.text = "状态：" + result.State;
                    if (result.State == ESMenuTreePageTaskState.Failed)
                    {
                        context.Notify(
                            "页面任务失败：" + result.Exception?.Message,
                            ESMenuTreePageStatus.Error,
                            ESEditorFeedbackSoundKind.Error);
                    }
                });
            if (taskStatusLabel != null)
                taskStatusLabel.text = started ? "状态：运行中" : "状态：已有同名任务正在运行";
        }

        private static void BuildQuickPanel(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            content.Add(ESEditorPanelUI.CreateHeading(
                "快速功能面板",
                "使用 AddPanel 注册，宿主统一处理滚动、缓存、上下文和释放。"));
            ESEditorFunctionalSection commonActions = ESEditorPanelUI.CreateFunctionalSection(
                "常用动作",
                "按钮回调只持有当前页面上下文，上下文失效后会自动拒绝操作。",
                ESMenuTreePageStatus.Ready);
            commonActions.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "标记就绪",
                    "发布当前页面状态。",
                    () => context.Notify(
                        "快速功能面板已就绪",
                        ESMenuTreePageStatus.Ready,
                        ESEditorFeedbackSoundKind.Success),
                    true),
                ESEditorPanelUI.CreateButton(
                    "局部重建",
                    "仅重新创建当前功能面板视图。",
                    context.RebuildView),
                ESEditorPanelUI.CreateButton(
                    "打开 Odin 页",
                    "通过稳定 ID 驱动页面选择。",
                    () => context.SelectPage("odin.serialized"))));
            commonActions.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "显示菜单徽标",
                    "为当前菜单节点写入轻量提示。",
                    () => context.SetMenuBadge("NEW", ESMenuTreePageStatus.Info)),
                ESEditorPanelUI.CreateButton(
                    "清除菜单徽标",
                    "移除当前菜单节点提示。",
                    context.ClearMenuBadge)));
            content.Add(commonActions.Root);
        }

        private void BuildAdvancedPanel(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            advancedState.BuildCount++;
            advancedState.ActiveContext = context;
            content.Add(ESEditorPanelUI.CreateHeading(
                "高级状态面板",
                "集中验证页面生命周期、菜单徽标、动态工具动作、合并重建和刷新异常隔离。"));
            content.Add(ESEditorPanelUI.CreateNotice(
                "局部状态，不是第二份业务数据",
                "测试计数只存于当前 EditorWindow 实例；页面视图释放后不持有 VisualElement，域重载后重新开始。",
                ESMenuTreePageStatus.Info));

            advancedState.LifecycleLabel = new Label();
            advancedState.LifecycleLabel.style.marginTop = 14f;
            advancedState.LifecycleLabel.style.whiteSpace = WhiteSpace.Normal;
            advancedState.LifecycleLabel.style.color =
                ES.EditorInternal.ESEditorPresentation.SectionTextColor;
            content.Add(advancedState.LifecycleLabel);

            advancedState.ModeToggle = new Toggle { value = advancedState.AdvancedMode };
            advancedState.ModeToggle.RegisterValueChangedCallback(evt =>
                SetAdvancedMode(context, evt.newValue));
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "高级模式",
                advancedState.ModeToggle,
                "同步驱动右上工具动作的显示和选中态。"));

            var loadSlider = new SliderInt(0, 12) { value = advancedState.BadgeCount };
            loadSlider.RegisterValueChangedCallback(evt =>
                SetAdvancedBadge(context, evt.newValue));
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "菜单提示数量",
                loadSlider,
                "只更新当前菜单按钮，不重建整棵菜单树。"));

            content.Add(ESEditorPanelUI.CreateSection(
                "恢复与压力动作",
                "以下动作均由用户显式触发，不注册 Update，也不扫描资产。"));
            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "连续请求局部重建 x3",
                    "同一帧的重复请求应合并为一次页面重建。",
                    () =>
                    {
                        context.RebuildView();
                        context.RebuildView();
                        context.RebuildView();
                    },
                    true),
                ESEditorPanelUI.CreateButton(
                    "下一次刷新失败",
                    "刷新异常应被宿主隔离，页面仍可继续使用。",
                    () => ArmAdvancedRefreshFailure(context)),
                ESEditorPanelUI.CreateButton(
                    "整窗菜单重建",
                    "重建菜单并使旧页面上下文失效。",
                    context.RequestMenuRebuild)));

            content.Add(ESEditorPanelUI.CreateSection(
                "旧上下文隔离",
                "整窗重建后调用上一个页面上下文应静默拒绝，不得污染新页面。"));
            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "调用上一个上下文",
                    "仅当发生过整窗重建时有测试意义。",
                    () => advancedState.PreviousContext?.Notify(
                        "旧上下文不应发布这条消息",
                        ESMenuTreePageStatus.Error,
                        ESEditorFeedbackSoundKind.Error))));
            UpdateAdvancedPanelLabels();
        }

        private static void BuildResponsivePanel(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            content.Add(ESEditorPanelUI.CreateHeading(
                "窄窗与长中文验证",
                "这是一段有意拉长的中文说明，用于检查菜单分栏收窄、较高 DPI 和长文本情况下是否自然换行，同时保证主要操作仍然无需横向滚动即可触达。"));
            content.Add(ESEditorPanelUI.CreateNotice(
                "验收重点",
                "单一竖向 ScrollView、隐藏横向滚动、字段行自动换行、按钮动作行自动折行。",
                ESMenuTreePageStatus.Warning));

            var nameField = new TextField
            {
                value = "用于验证长值完整显示、输入焦点和页面内键盘事件不会穿透到菜单导航"
            };
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "较长的功能配置名称",
                nameField,
                "文本框应保持可编辑，不被根节点快捷键误处理。"));

            var enabledToggle = new Toggle { value = true };
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "启用局部高成本功能",
                enabledToggle,
                "测试标签和字段在窄宽度下的排列。"));

            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "执行一个文本很长但仍应完整可发现的主要动作",
                    "动作行应自动折行。",
                    () => context.Notify(
                        "窄窗主要动作已执行",
                        ESMenuTreePageStatus.Ready,
                        ESEditorFeedbackSoundKind.Success),
                    true),
                ESEditorPanelUI.CreateButton(
                    "返回高级状态面板",
                    "通过稳定 ID 跳转。",
                    () => context.SelectPage("panel.advanced"))));
        }

        private void OnAdvancedPanelShow(ESMenuTreePageContext context)
        {
            advancedState.ShowCount++;
            advancedState.ActiveContext = context;
            UpdateAdvancedPanelLabels();
        }

        private void OnAdvancedPanelRefresh(ESMenuTreePageContext context)
        {
            advancedState.RefreshCount++;
            UpdateAdvancedPanelLabels();
            if (!advancedState.FailNextRefresh)
                return;
            advancedState.FailNextRefresh = false;
            throw new InvalidOperationException("高级状态面板主动注入的一次性刷新异常。");
        }

        private void OnAdvancedPanelHide()
        {
            advancedState.HideCount++;
            UpdateAdvancedPanelLabels();
        }

        private void OnAdvancedPanelReleaseView()
        {
            advancedState.ReleaseCount++;
            advancedState.PreviousContext = advancedState.ActiveContext;
            advancedState.ActiveContext = null;
            advancedState.LifecycleLabel = null;
            advancedState.ModeToggle = null;
        }

        private void OnAdvancedPanelDispose()
        {
            advancedState.DisposeCount++;
        }

        private void ToggleAdvancedMode(ESMenuTreePageContext context)
        {
            SetAdvancedMode(context, !advancedState.AdvancedMode);
        }

        private void SetAdvancedMode(ESMenuTreePageContext context, bool enabled)
        {
            advancedState.AdvancedMode = enabled;
            advancedState.ModeToggle?.SetValueWithoutNotify(enabled);
            if (enabled)
            {
                context.SetMenuBadge("高级", ESMenuTreePageStatus.Modified);
                advancedState.HasMenuBadge = true;
            }
            else if (advancedState.BadgeCount > 0)
            {
                context.SetMenuBadge(
                    advancedState.BadgeCount.ToString(),
                    advancedState.BadgeCount >= 8
                        ? ESMenuTreePageStatus.Warning
                        : ESMenuTreePageStatus.Info);
                advancedState.HasMenuBadge = true;
            }
            else
            {
                context.ClearMenuBadge();
                advancedState.HasMenuBadge = false;
            }
            context.RefreshPageActions();
            UpdateAdvancedPanelLabels();
        }

        private void SetAdvancedBadge(ESMenuTreePageContext context, int count)
        {
            advancedState.BadgeCount = Mathf.Max(0, count);
            if (advancedState.BadgeCount > 0)
            {
                context.SetMenuBadge(
                    advancedState.BadgeCount.ToString(),
                    advancedState.BadgeCount >= 8
                        ? ESMenuTreePageStatus.Warning
                        : ESMenuTreePageStatus.Info);
                advancedState.HasMenuBadge = true;
            }
            else if (advancedState.AdvancedMode)
            {
                context.SetMenuBadge("高级", ESMenuTreePageStatus.Modified);
                advancedState.HasMenuBadge = true;
            }
            else
            {
                context.ClearMenuBadge();
                advancedState.HasMenuBadge = false;
            }
            context.RefreshPageActions();
            UpdateAdvancedPanelLabels();
        }

        private void ClearAdvancedBadge(ESMenuTreePageContext context)
        {
            advancedState.BadgeCount = 0;
            advancedState.HasMenuBadge = false;
            context.ClearMenuBadge();
            context.RefreshPageActions();
            UpdateAdvancedPanelLabels();
        }

        private void ArmAdvancedRefreshFailure(ESMenuTreePageContext context)
        {
            advancedState.FailNextRefresh = true;
            context.Notify(
                "下一次刷新将注入一次异常；之后可继续刷新。",
                ESMenuTreePageStatus.Warning,
                ESEditorFeedbackSoundKind.Warning);
            context.RefreshPageActions();
            UpdateAdvancedPanelLabels();
        }

        private void UpdateAdvancedPanelLabels()
        {
            if (advancedState?.LifecycleLabel == null)
                return;
            advancedState.LifecycleLabel.text =
                "Build " + advancedState.BuildCount
                + "  |  Show " + advancedState.ShowCount
                + "  |  Refresh " + advancedState.RefreshCount
                + "  |  Hide " + advancedState.HideCount
                + "  |  Release " + advancedState.ReleaseCount
                + "  |  Dispose " + advancedState.DisposeCount
                + "\n模式：" + (advancedState.AdvancedMode ? "高级" : "基础")
                + "  |  徽标：" + advancedState.BadgeCount
                + (advancedState.HasMenuBadge ? "（显示中）" : "（已清除）")
                + "  |  下次刷新异常：" + (advancedState.FailNextRefresh ? "已就绪" : "否");
        }

        private void LocateFontProfile(ESMenuTreePageContext context)
        {
            Page_FontBuild page = context.GetOdinTarget<Page_FontBuild>();
            if (page?.profile == null)
            {
                context.Notify(
                    "尚未选择 Font Build Profile",
                    ESMenuTreePageStatus.Warning,
                    ESEditorFeedbackSoundKind.Warning);
                return;
            }

            Selection.activeObject = page.profile;
            EditorGUIUtility.PingObject(page.profile);
            context.Notify(
                "已定位 Font Build Profile：" + page.profile.name,
                ESMenuTreePageStatus.Ready,
                ESEditorFeedbackSoundKind.Locate);
        }

        private sealed class ToolkitTestPage : ESMenuTreePage
        {
            private readonly string title;
            private readonly string description;
            private ESMenuTreePageContext context;
            private int viewBuildCount;

            internal ToolkitTestPage(string title, string description)
            {
                this.title = title;
                this.description = description;
            }

            public override VisualElement CreateView(ESMenuTreePageContext pageContext)
            {
                context = pageContext;
                viewBuildCount++;
                VisualElement root = new VisualElement { name = "ESMenuTreeToolkitTestPage" };
                root.style.flexGrow = 1f;
                root.style.minWidth = 0f;

                ScrollView scroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    name = "ESMenuTreeToolkitTestPageScroll",
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.Auto
                };
                scroll.style.flexGrow = 1f;
                scroll.style.paddingLeft = 24f;
                scroll.style.paddingRight = 24f;
                scroll.style.paddingTop = 22f;
                scroll.style.paddingBottom = 28f;
                root.Add(scroll);

                Label heading = new Label(title);
                heading.AddToClassList("es-brand-title");
                heading.style.fontSize = 20f;
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
                scroll.Add(heading);

                Label detail = new Label(description);
                detail.style.marginTop = 8f;
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                scroll.Add(detail);

                VisualElement actions = new VisualElement { name = "ESMenuTreeToolkitTestActions" };
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.flexWrap = Wrap.Wrap;
                actions.style.marginTop = 18f;
                Button ready = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    "标记就绪", "写入就绪状态。", () => context?.Notify(
                        "页面就绪", ESMenuTreePageStatus.Ready, ESEditorFeedbackSoundKind.Success), true);
                Button warning = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    "标记警告", "写入警告状态。", () => context?.Notify(
                        "页面仅用于测试", ESMenuTreePageStatus.Warning, ESEditorFeedbackSoundKind.Warning));
                Button rebuild = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    "请求重建", "请求宿主合并重建菜单。", () => context?.RequestMenuRebuild());
                Button rebuildView = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    "局部重建", "只重建当前页面视图并保留菜单、搜索和历史。", () => context?.RebuildView());
                actions.Add(ready);
                actions.Add(warning);
                actions.Add(rebuildView);
                actions.Add(rebuild);
                scroll.Add(actions);

                Label proof = new Label(
                    "视图代数：" + viewBuildCount
                    + " | 验收点：搜索、折叠、稳定选中、状态栏、局部重建、关闭释放。");
                proof.style.marginTop = 22f;
                proof.style.color = ES.EditorInternal.ESEditorPresentation.EmptyTextColor;
                scroll.Add(proof);
                return root;
            }

            public override void Dispose()
            {
                context = null;
            }

            public override void ReleaseView()
            {
                context = null;
            }
        }

        private sealed class ToolkitRecoverablePage : ESMenuTreePage
        {
            private ESMenuTreePageContext context;
            private bool failNextBuild;
            private int buildCount;

            public override VisualElement CreateView(ESMenuTreePageContext pageContext)
            {
                context = pageContext;
                buildCount++;
                if (failNextBuild)
                {
                    failNextBuild = false;
                    throw new InvalidOperationException(
                        "可恢复异常页主动注入的一次性 CreateView 失败。");
                }

                var root = new VisualElement { name = "ESMenuTreeRecoverablePage" };
                root.style.flexGrow = 1f;
                root.style.minWidth = 0f;
                var scroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.Auto
                };
                scroll.style.flexGrow = 1f;
                root.Add(scroll);
                scroll.Add(ESEditorPanelUI.CreateHeading(
                    "可恢复异常页",
                    "验证 CreateView 失败后的原因、影响、恢复动作和下一次正常创建。"));
                scroll.Add(ESEditorPanelUI.CreateNotice(
                    "当前状态",
                    "视图已成功创建 " + buildCount + " 次。点击下方按钮后，本页会在下一次局部重建时失败一次。",
                    ESMenuTreePageStatus.Ready));
                scroll.Add(ESEditorPanelUI.CreateActionRow(
                    ESEditorPanelUI.CreateButton(
                        "注入一次构建失败并局部重建",
                        "失败后使用错误视图中的重试按钮恢复。",
                        () =>
                        {
                            failNextBuild = true;
                            context.RebuildView();
                        },
                        true)));
                return root;
            }

            public override void ReleaseView()
            {
                context = null;
            }

            public override void Dispose()
            {
                context = null;
            }
        }

        private sealed class ToolkitSerializedPanelPage : ESMenuTreePage
        {
            private sealed class SerializedPanelTarget : ScriptableObject
            {
                public string displayName;
                public int capacity;
                public bool featureEnabled;
            }

            private SerializedPanelTarget firstTarget;
            private SerializedPanelTarget secondTarget;
            private ESEditorSerializedPanelBinding binding;
            private VisualElement viewRoot;
            private bool disposed;

            public ToolkitSerializedPanelPage()
            {
                firstTarget = CreateInstance<SerializedPanelTarget>();
                secondTarget = CreateInstance<SerializedPanelTarget>();
                firstTarget.hideFlags = HideFlags.HideAndDontSave;
                secondTarget.hideFlags = HideFlags.HideAndDontSave;
                firstTarget.displayName = "目标 A";
                secondTarget.displayName = "目标 B";
                firstTarget.capacity = 16;
                secondTarget.capacity = 32;
                firstTarget.featureEnabled = true;
                secondTarget.featureEnabled = true;
            }

            public override VisualElement CreateView(ESMenuTreePageContext context)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(ToolkitSerializedPanelPage));
                viewRoot = new VisualElement { name = "ToolkitSerializedPanelPage" };
                viewRoot.style.flexGrow = 1f;
                viewRoot.Add(ESEditorPanelUI.CreateHeading(
                    "SerializedProperty 多目标绑定",
                    "两个临时目标使用同一个 SerializedObject；不同初始值应显示 Mixed，编辑应进入 Unity Undo。"));
                binding = new ESEditorSerializedPanelBinding(
                    new UnityEngine.Object[] { firstTarget, secondTarget });
                viewRoot.Add(binding.CreatePropertyField(
                    "displayName",
                    "显示名称",
                    "两个目标初始值不同，用于验证 Mixed Value。"));
                viewRoot.Add(binding.CreatePropertyField(
                    "capacity",
                    "容量",
                    "修改后应同时写入两个目标并支持 Undo/Redo。"));
                viewRoot.Add(binding.CreatePropertyField(
                    "featureEnabled",
                    "功能启用",
                    "验证布尔字段的多对象绑定。"));
                return viewRoot;
            }

            public override void Refresh()
            {
                binding?.Update();
            }

            public override void ReleaseView()
            {
                binding?.Dispose();
                binding = null;
                viewRoot?.Clear();
                viewRoot = null;
            }

            public override void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                ReleaseView();
                if (firstTarget != null)
                    UnityEngine.Object.DestroyImmediate(firstTarget);
                if (secondTarget != null)
                    UnityEngine.Object.DestroyImmediate(secondTarget);
                firstTarget = null;
                secondTarget = null;
            }
        }

        private sealed class AdvancedPanelState
        {
            internal bool AdvancedMode;
            internal bool FailNextRefresh;
            internal bool HasMenuBadge;
            internal int BadgeCount;
            internal int BuildCount;
            internal int ShowCount;
            internal int RefreshCount;
            internal int HideCount;
            internal int ReleaseCount;
            internal int DisposeCount;
            internal ESMenuTreePageContext ActiveContext;
            internal ESMenuTreePageContext PreviousContext;
            internal Label LifecycleLabel;
            internal Toggle ModeToggle;
        }

        [Serializable]
        private sealed class ToolkitOdinTestPage : ESWindowPageBase
        {
            internal enum TestMode
            {
                Standard,
                Compact,
                Detailed
            }

            [Title("Odin 序列化兼容页", "该页面由新版 UI Toolkit 窗口托管独立 PropertyTree。")]
            [LabelText("测试名称")]
            public string testName = "无意义的 Odin 页面";

            [LabelText("显示模式"), EnumToggleButtons]
            public TestMode mode = TestMode.Standard;

            [LabelText("强度"), Range(0, 100)]
            public int strength = 48;

            [LabelText("启用高级内容")]
            public bool advanced = true;

            [ShowIf(nameof(advanced)), LabelText("高级说明"), TextArea(2, 5)]
            public string advancedDescription = "用于确认 ShowIf、TextArea 和动态高度都由 Odin 正常处理。";

            [LabelText("无意义子项"), ListDrawerSettings(ShowPaging = false)]
            public List<string> items = new List<string> { "Alpha", "Beta", "Gamma" };

            [ShowInInspector, ReadOnly, LabelText("刷新次数")]
            private int refreshCount;

            [Button("添加一个子项", ButtonHeight = 26)]
            private void AddItem()
            {
                items.Add("Item " + (items.Count + 1));
            }

            public override ESWindowPageBase ES_Refresh()
            {
                refreshCount++;
                return this;
            }
        }
    }

    /// <summary>验证不创建菜单树时仍可使用统一 ES 页面外壳和完整生命周期。</summary>
    public sealed class ESSinglePageToolkitTestWindow : ESSinglePageWindow<ESSinglePageToolkitTestWindow>
    {
        public override string ESWindow_PresentationShortTitle => "单页";

        [NonSerialized] private ESMenuTreePageContext activeContext;
        [NonSerialized] private Label lifecycleLabel;
        [NonSerialized] private Toggle advancedToggle;
        [NonSerialized] private bool advancedMode;
        [NonSerialized] private int buildCount;
        [NonSerialized] private int showCount;
        [NonSerialized] private int refreshCount;
        [NonSerialized] private int hideCount;
        [NonSerialized] private int releaseCount;
        [NonSerialized] private int disposeCount;

        [MenuItem("【ES】/自动化与开发/编辑器扩展/编辑器/新版 UI Toolkit 单页面板测试", false, 9161)]
        private static void OpenMenu()
        {
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("新版单页面板测试", "验证 ES 单页 UI Toolkit EditorWindow");
        }

        protected override string ESWindow_Subtitle => "无菜单树的统一 ES 功能面板宿主";
        protected override Vector2 ESWindow_MinSize => new Vector2(560f, 420f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(820f, 620f);
        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            maxSize = new Vector2(1400f, 1000f);
        }
        protected override ESMenuTreePageLayout ESWindow_PageLayout => ESMenuTreePageLayout.Compact;
        protected override float ESWindow_PageMaxContentWidth => 780f;
        protected override float ESWindow_PageContentPadding => 18f;
        protected override string ESWindow_PageKeywords =>
            "单页 EditorWindow UI Toolkit 生命周期 局部重建 右上动作";

        protected override void ESWindow_BuildGlobalActions(
            ICollection<ESMenuTreeGlobalAction> actions)
        {
            actions.Add(new ESMenuTreeGlobalAction(
                    "reset-counters",
                    "重置计数",
                    "重置单页生命周期计数并局部重建。",
                    ResetCounters)
                .WithIcon(EditorIcons.Refresh)
                .WithPriority(100));
        }

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "toggle-advanced",
                    "高级模式",
                    "验证单页右上动作的动态选中态。",
                    ToggleAdvancedMode)
                .WithIcon(EditorIcons.SettingsCog)
                .WithCheckedState(_ => advancedMode)
                .WithPriority(120));
            actions.Add(new ESMenuTreePageAction(
                    "advanced-notice",
                    "高级提示",
                    "仅在高级模式下显示。",
                    context => context.Notify(
                        "单页高级动作执行成功",
                        ESMenuTreePageStatus.Ready,
                        ESEditorFeedbackSoundKind.Success))
                .WithIcon(EditorIcons.Bell)
                .WhenVisible(_ => advancedMode)
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "rebuild-single-page",
                    "局部重建",
                    "仅释放并重建当前单页视图。",
                    context => context.RebuildView())
                .WithIcon(EditorIcons.Refresh)
                .WithPriority(80));
        }

        protected override void ESWindow_BuildPageContent(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            activeContext = context;
            buildCount++;
            content.Add(ESEditorPanelUI.CreateHeading(
                "单页 ES 功能面板",
                "继承 ESSinglePageWindow 后只需构建页面内容；外壳、状态栏、右上动作、刷新、局部重建和释放均由宿主管理。"));
            content.Add(ESEditorPanelUI.CreateNotice(
                "单页宿主已生效",
                "此窗口不会创建左侧菜单树、搜索框或历史导航按钮，同时仍复用 MenuTree 页面的稳定生命周期。",
                ESMenuTreePageStatus.Ready));

            lifecycleLabel = new Label();
            lifecycleLabel.style.marginTop = 14f;
            lifecycleLabel.style.whiteSpace = WhiteSpace.Normal;
            lifecycleLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionTextColor;
            content.Add(lifecycleLabel);

            advancedToggle = new Toggle { value = advancedMode };
            advancedToggle.RegisterValueChangedCallback(evt =>
                SetAdvancedMode(context, evt.newValue));
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "高级模式",
                advancedToggle,
                "同步驱动当前单页的右上工具动作。"));

            var nameField = new TextField
            {
                value = "单页面板同样支持长中文字段、窄窗换行和统一 ES Presentation"
            };
            content.Add(ESEditorPanelUI.CreateFieldRow(
                "功能配置名称",
                nameField,
                "字段输入不应触发菜单快捷键或页面重建。"));

            content.Add(ESEditorPanelUI.CreateSection(
                "局部操作",
                "连续请求会由宿主合并，不建立全局 Update。"));
            content.Add(ESEditorPanelUI.CreateActionRow(
                ESEditorPanelUI.CreateButton(
                    "连续请求局部重建 x3",
                    "应合并为一次单页视图重建。",
                    () =>
                    {
                        context.RebuildView();
                        context.RebuildView();
                        context.RebuildView();
                    },
                    true),
                ESEditorPanelUI.CreateButton(
                    "发布单页状态",
                    "验证状态栏、通知和音效。",
                    () => context.Notify(
                        "单页面板上下文可用",
                        ESMenuTreePageStatus.Modified,
                        ESEditorFeedbackSoundKind.Confirm))));
            UpdateLifecycleLabel();
        }

        protected override void ESWindow_OnPageShow(ESMenuTreePageContext context)
        {
            activeContext = context;
            showCount++;
            UpdateLifecycleLabel();
        }

        protected override void ESWindow_OnPageRefresh(ESMenuTreePageContext context)
        {
            refreshCount++;
            UpdateLifecycleLabel();
        }

        protected override void ESWindow_OnPageHide()
        {
            hideCount++;
            UpdateLifecycleLabel();
        }

        protected override void ESWindow_OnPageReleaseView()
        {
            releaseCount++;
            activeContext = null;
            lifecycleLabel = null;
            advancedToggle = null;
        }

        protected override void ESWindow_OnPageDispose()
        {
            disposeCount++;
        }

        private void ToggleAdvancedMode(ESMenuTreePageContext context)
        {
            SetAdvancedMode(context, !advancedMode);
        }

        private void SetAdvancedMode(ESMenuTreePageContext context, bool enabled)
        {
            advancedMode = enabled;
            advancedToggle?.SetValueWithoutNotify(enabled);
            context.RefreshPageActions();
            context.Notify(
                enabled ? "单页高级模式已开启" : "单页高级模式已关闭",
                enabled ? ESMenuTreePageStatus.Modified : ESMenuTreePageStatus.Ready,
                ESEditorFeedbackSoundKind.Confirm,
                false);
            UpdateLifecycleLabel();
        }

        private void ResetCounters()
        {
            buildCount = 0;
            showCount = 0;
            refreshCount = 0;
            hideCount = 0;
            releaseCount = 0;
            disposeCount = 0;
            activeContext?.RebuildView();
        }

        private void UpdateLifecycleLabel()
        {
            if (lifecycleLabel == null)
                return;
            lifecycleLabel.text =
                "Build " + buildCount
                + "  |  Show " + showCount
                + "  |  Refresh " + refreshCount
                + "  |  Hide " + hideCount
                + "  |  Release " + releaseCount
                + "  |  Dispose " + disposeCount
                + "\n模式：" + (advancedMode ? "高级" : "基础");
        }
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Workspace)]
    internal sealed class ESWindowSleepBenchmarkProbeWindow : EditorWindow,
        IESWindowMultiInstanceContract
    {
        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => nameof(ESWindowSemiSleepStressTest);

        [SerializeField] private int probeIndex;

        internal void Configure(int index)
        {
            probeIndex = index;
            titleContent = new GUIContent($"休眠探针 {index + 1:000}");
        }

        private void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 6f;
            rootVisualElement.style.paddingBottom = 6f;

            var header = new VisualElement
            {
                name = "ESWindowSleepBenchmarkHeader"
            };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.Add(new Label($"窗口休眠规模探针 {probeIndex + 1:000}")
            {
                pickingMode = PickingMode.Ignore
            });

            var systemActions = new VisualElement
            {
                name = "ESWindowSleepBenchmarkSystemActions"
            };
            systemActions.style.flexDirection = FlexDirection.Row;
            systemActions.style.alignItems = Align.Center;
            systemActions.style.marginLeft = 6f;
            header.Add(systemActions);
            rootVisualElement.Add(header);
            rootVisualElement.Add(new Label("仅承载 Editor update、原生位置提交与 Repaint 规模采样。")
            {
                pickingMode = PickingMode.Ignore
            });

            ESWindowFoundation.BindFullSleep(
                this,
                new ESWindowActionHosts(system: systemActions));
        }

        private void OnDisable()
        {
            ESWindowFoundation.Suspend(this);
        }

        private void OnDestroy()
        {
            ESWindowFoundation.Close(this);
        }
    }

    /// <summary>显式压力测试入口：分帧打开窗口并验证半休眠网格与 update 规模。</summary>
    public static class ESWindowSemiSleepStressTest
    {
        private const int WindowCount = 21;
        private const double BenchmarkDurationSeconds = 8d;
        private const int Columns = 5;
        private const int Rows = (WindowCount + Columns - 1) / Columns;
        private const float SleepSize = 100f;
        private const float Margin = 18f;
        private static readonly int[] PerformanceWindowCounts = { 20, 50, 100 };

        private sealed class WindowSpec
        {
            public readonly string TypeName;
            public readonly string Title;

            public WindowSpec(string typeName, string title)
            {
                TypeName = typeName;
                Title = title;
            }
        }

        private static readonly WindowSpec[] WindowSpecs =
        {
            new WindowSpec("ES.ESWindowLauncher, ES_Editor", "工具启动器"),
            new WindowSpec("ES.ESAutomationCenterWindow, ES_Editor", "自动化中心"),
            new WindowSpec("ES.ESResourceCollectionWorkflowWindow, ES_Editor", "资源收集工作流"),
            new WindowSpec("ES.ESResourceRuntimeMonitorWindow, ES_Editor", "资源运行时监视器"),
            new WindowSpec("ES.ESDeveloperCockpitWindow, ES_Editor", "开发者驾驶舱"),
            new WindowSpec("ES.EditorInternal.ESEditorHealthWindow, ES_Editor", "编辑器健康检查"),
            new WindowSpec("ES.EditorInternal.ESEditorThemeWindow, ES_Editor", "编辑器主题"),
            new WindowSpec("ES.ESAssetPackageBakeWindow, ES_Editor", "资产包分离"),
            new WindowSpec("ES.ESMenuTreeToolkitTestWindow, ES_Editor", "Toolkit MenuTree"),
            new WindowSpec("ES.ESSinglePageToolkitTestWindow, ES_Editor", "Toolkit 单页"),
            new WindowSpec("ES.ESFontToolsWindow, ES_Editor", "字体资产工具"),
            new WindowSpec("ES.ESLocalizationToolsWindow, ES_Editor", "本地化工具"),
            new WindowSpec("ES.SimpleToolsWindow, ES_Editor", "简单工具集"),
            new WindowSpec("ES.ESAssetReleaseUploadWindow, ES_Editor", "发布计划查看"),
            new WindowSpec("ES.ESEditorFeedbackSoundSchemeWindow, ES_Editor", "编辑器音效方案"),
            new WindowSpec("ES.EntityStatDebugWindow, ES_Logic.Editor", "Entity 属性监视器"),
            new WindowSpec("ES.EntityBasicInteractionDebugWindow, ES_Logic.Editor", "交互运行时面板"),
            new WindowSpec("ES.Editor.ESDynamicAtlasMonitorWindow, ES_Logic.Editor", "动态图集监视器"),
            new WindowSpec("ES.Editor.ESUIRiskAuditWindow, ES_Logic.Editor", "UI 风险体检"),
            new WindowSpec("ES.ESCameraTrackPreviewWindow, ES_Logic.Editor", "轨道相机预览"),
            new WindowSpec("ES.ESAudioCueTrimPreviewWindow, ES_Logic.Editor", "音频 Cue 预览")
        };

        private static readonly List<EditorWindow> OpenedWindows = new List<EditorWindow>(WindowCount);
        private static readonly List<string> ProtectedExistingWindowTitles = new List<string>(WindowCount);
        private static MethodInfo hasOpenInstancesMethod;
        private static int nextWindowIndex;
        private static double nextOpenAt;
        private static int benchmarkWindowCount;
        private static double benchmarkEndsAt;
        private static bool benchmarkSampling;
        private static ES.EditorInternal.ESWindowSemiSleepPerformanceSample lastPerformanceSample;

        internal static int ConfiguredWindowCount => WindowSpecs.Length;
        internal static IReadOnlyList<int> ConfiguredPerformanceWindowCounts => PerformanceWindowCounts;
        internal static ES.EditorInternal.ESWindowSemiSleepPerformanceSample LastPerformanceSample =>
            lastPerformanceSample;

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/打开 21 个半休眠窗口", false, 9170)]
        private static void OpenTwentyOneSemiSleepWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("无法开始窗口测试", "请先退出 PlayMode。", "关闭");
                return;
            }

            StopOpeningQueue();
            CloseOpenedWindows();
            ProtectedExistingWindowTitles.Clear();
            nextWindowIndex = 0;
            nextOpenAt = 0d;
            EditorApplication.update -= OpenNextWindow;
            EditorApplication.update += OpenNextWindow;
            AssemblyReloadEvents.beforeAssemblyReload -= StopOpeningQueue;
            AssemblyReloadEvents.beforeAssemblyReload += StopOpeningQueue;
            EditorApplication.quitting -= StopOpeningQueue;
            EditorApplication.quitting += StopOpeningQueue;
        }

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/性能采样 20 窗口", false, 9171)]
        private static void BenchmarkTwentyWindows()
        {
            BeginPerformanceBenchmark(20);
        }

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/性能采样 50 窗口", false, 9172)]
        private static void BenchmarkFiftyWindows()
        {
            BeginPerformanceBenchmark(50);
        }

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/性能采样 100 窗口", false, 9173)]
        private static void BenchmarkOneHundredWindows()
        {
            BeginPerformanceBenchmark(100);
        }

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/关闭窗口休眠压力测试", false, 9174)]
        private static void CloseStressWindows()
        {
            StopOpeningQueue();
            CloseOpenedWindows();
        }

        [MenuItem("【ES】/验证与诊断/测试与验收/编辑器窗口/关闭全部压力测试目标窗口", false, 9175)]
        private static void CloseAllStressTargetWindowsEntry()
        {
            _ = ConfirmAndCloseAllStressTargetWindowsAsync();
        }

        private static async Task ConfirmAndCloseAllStressTargetWindowsAsync()
        {
            bool accepted;
            try
            {
                accepted = await ESDialog.DangerAsync(
                    "es.window.semisleep.close-all-targets",
                    "关闭全部压力测试目标窗口",
                    "将关闭半休眠压力测试目标列表中的所有窗口和性能探针，不会关闭其他 Unity 窗口。",
                    "关闭全部目标窗口",
                    "取消",
                    detail: "影响：目标窗口的未保存编辑状态由各窗口自身负责处理。\n恢复：可从【ES】菜单重新打开需要的窗口。",
                    host: ESDialogHost.Editor,
                    owner: null,
                    allowMainWorkspaceFallback: true);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESWindowSemiSleepStressTest] 全部关闭确认失败：\n" + exception);
                return;
            }

            if (!accepted)
                return;

            CloseAllStressTargetWindows();
        }

        private static void CloseAllStressTargetWindows()
        {
            var windowsToClose = new List<EditorWindow>(WindowSpecs.Length + 1);
            AddOpenWindowsOfType(typeof(ESWindowSleepBenchmarkProbeWindow), windowsToClose);
            foreach (WindowSpec spec in WindowSpecs)
            {
                Type windowType = Type.GetType(spec.TypeName, false);
                if (windowType == null || !typeof(EditorWindow).IsAssignableFrom(windowType))
                    continue;
                AddOpenWindowsOfType(windowType, windowsToClose);
            }

            StopOpeningQueue();
            int closedCount = 0;
            foreach (EditorWindow window in windowsToClose)
            {
                if (window == null)
                {
                    closedCount++;
                    continue;
                }
                try
                {
                    window.Close();
                    closedCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[ESWindowSemiSleepStressTest] 关闭目标窗口失败："
                                   + window.GetType().FullName + "\n" + exception);
                }
            }

            OpenedWindows.Clear();
            ProtectedExistingWindowTitles.Clear();
            Debug.Log("[ESWindowSemiSleepStressTest] 已关闭全部压力测试目标窗口"
                      + $" | closed={closedCount}"
                      + " | scope=WindowSpecs+ESWindowSleepBenchmarkProbeWindow");
        }

        private static void AddOpenWindowsOfType(Type windowType, List<EditorWindow> destination)
        {
            if (windowType == null || destination == null)
                return;

            UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(windowType);
            foreach (UnityEngine.Object instance in instances)
            {
                if (!(instance is EditorWindow window) || window == null || destination.Contains(window))
                    continue;
                destination.Add(window);
            }
        }

        private static void BeginPerformanceBenchmark(int windowCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("无法开始性能采样", "请先退出 PlayMode。", "关闭");
                return;
            }
            if (!PerformanceWindowCounts.Contains(windowCount))
                throw new ArgumentOutOfRangeException(nameof(windowCount));

            StopOpeningQueue();
            CloseOpenedWindows();
            benchmarkWindowCount = windowCount;
            nextWindowIndex = 0;
            nextOpenAt = 0d;
            benchmarkEndsAt = 0d;
            benchmarkSampling = false;
            EditorApplication.update -= UpdatePerformanceBenchmark;
            EditorApplication.update += UpdatePerformanceBenchmark;
            AssemblyReloadEvents.beforeAssemblyReload -= StopOpeningQueue;
            AssemblyReloadEvents.beforeAssemblyReload += StopOpeningQueue;
            EditorApplication.quitting -= StopOpeningQueue;
            EditorApplication.quitting += StopOpeningQueue;
            Debug.Log($"[ESWindowSemiSleepStressTest] 开始 {windowCount} 窗口性能采样；"
                      + "探针会分帧打开、完成休眠与页签晋级后自动关闭。");
        }

        private static void UpdatePerformanceBenchmark()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!benchmarkSampling)
            {
                if (now < nextOpenAt)
                    return;
                if (nextWindowIndex < benchmarkWindowCount)
                {
                    OpenNextBenchmarkWindow(nextWindowIndex++);
                    nextOpenAt = now + 0.025d;
                    return;
                }

                BeginBenchmarkSample(now);
                return;
            }

            if (now < benchmarkEndsAt)
                return;
            CompletePerformanceBenchmark();
        }

        private static void OpenNextBenchmarkWindow(int index)
        {
            try
            {
                ESWindowSleepBenchmarkProbeWindow window =
                    ScriptableObject.CreateInstance<ESWindowSleepBenchmarkProbeWindow>();
                window.Configure(index);
                window.position = BuildBenchmarkAwakeBounds(
                    EditorGUIUtility.GetMainWindowPosition(),
                    index,
                    benchmarkWindowCount);
                window.Show();
                OpenedWindows.Add(window);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESWindowSemiSleepStressTest] 探针窗口打开失败：\n" + exception);
            }
        }

        private static void BeginBenchmarkSample(double now)
        {
            Rect mainBounds = EditorGUIUtility.GetMainWindowPosition();
            OpenedWindows.RemoveAll(window => window == null);
            for (int i = 0; i < OpenedWindows.Count; i++)
            {
                EditorWindow window = OpenedWindows[i];
                TryPrepareWindowForSleep(
                    window,
                    BuildSleepBounds(mainBounds, i, benchmarkWindowCount),
                    allowTestProbeOverride: true);
            }

            SceneView sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            sceneView.Focus();
            ES.EditorInternal.ESEditorPresentation.BeginSemiSleepPerformanceSample();
            int sleeping = OpenedWindows.Count(window =>
                window != null
                && ES.EditorInternal.ESEditorPresentation.RequestWindowSemiSleep(window));
            benchmarkSampling = true;
            benchmarkEndsAt = now + BenchmarkDurationSeconds;
            Debug.Log($"[ESWindowSemiSleepStressTest] {OpenedWindows.Count}/{benchmarkWindowCount} 个探针已打开，"
                      + $"{sleeping} 个开始休眠；采样 {BenchmarkDurationSeconds:0.#} 秒。");
        }

        private static void CompletePerformanceBenchmark()
        {
            lastPerformanceSample =
                ES.EditorInternal.ESEditorPresentation.EndSemiSleepPerformanceSample();
            int requested = benchmarkWindowCount;
            int opened = OpenedWindows.Count(window => window != null);
            benchmarkSampling = false;
            benchmarkWindowCount = 0;
            benchmarkEndsAt = 0d;
            StopOpeningQueue();
            CloseOpenedWindows();

            Debug.Log(
                "[ESWindowSemiSleepStressTest] 性能采样完成"
                + $" | requested={requested}"
                + $" | opened={opened}"
                + $" | globalBoundAtStop={lastPerformanceSample.BoundWindowCount}"
                + $" | duration={lastPerformanceSample.SampleDurationSeconds:0.000}s"
                + $" | updates={lastPerformanceSample.UpdateCount}"
                + $" | bindingVisits={lastPerformanceSample.BindingVisitCount}"
                + $" | avgUpdate={lastPerformanceSample.AverageUpdateMicroseconds:0.000}us"
                + $" | maxUpdate={lastPerformanceSample.MaximumUpdateMicroseconds:0.000}us"
                + $" | allocated={lastPerformanceSample.UpdateAllocatedBytes}B"
                + $" | avgAllocated={lastPerformanceSample.AverageAllocatedBytesPerUpdate:0.000}B/update"
                + $" | maxAllocated={lastPerformanceSample.MaximumAllocatedBytesPerUpdate}B/update"
                + $" | nativeCommits={lastPerformanceSample.NativePositionCommitCount}"
                + $" | repaints={lastPerformanceSample.RepaintRequestCount}"
                + " | profilerMarker=ES.Editor.WindowSleep.Update");
        }

        private static void OpenNextWindow()
        {
            if (EditorApplication.timeSinceStartup < nextOpenAt)
                return;
            if (nextWindowIndex >= WindowSpecs.Length)
            {
                FinishOpening();
                return;
            }

            int index = nextWindowIndex++;
            WindowSpec spec = WindowSpecs[index];
            Type windowType = Type.GetType(spec.TypeName, false);
            if (windowType == null || !typeof(EditorWindow).IsAssignableFrom(windowType))
            {
                Debug.LogWarning("[ESWindowSemiSleepStressTest] 找不到窗口类型：" + spec.TypeName);
                nextOpenAt = EditorApplication.timeSinceStartup + 0.05d;
                return;
            }

            try
            {
                if (HasOpenWindowInstance(windowType))
                {
                    if (!ProtectedExistingWindowTitles.Contains(spec.Title))
                        ProtectedExistingWindowTitles.Add(spec.Title);
                    nextOpenAt = EditorApplication.timeSinceStartup + 0.08d;
                    return;
                }

                EditorWindow window = EditorWindow.GetWindow(windowType);
                if (window == null)
                    throw new InvalidOperationException("无法创建 EditorWindow 实例。");
                window.titleContent = new GUIContent($"{index + 1:00} · {spec.Title}");
                window.position = BuildAwakeBounds(EditorGUIUtility.GetMainWindowPosition(), index);
                window.Show();
                OpenedWindows.Add(window);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESWindowSemiSleepStressTest] 打开失败：" + spec.Title + "\n" + exception);
            }
            nextOpenAt = EditorApplication.timeSinceStartup + 0.08d;
        }

        private static void FinishOpening()
        {
            StopOpeningQueue();
            Rect mainBounds = EditorGUIUtility.GetMainWindowPosition();
            OpenedWindows.RemoveAll(window => window == null);
            ReportProtectedExistingWindows();
            if (OpenedWindows.Count == 0)
            {
                Debug.Log(
                    "[ESWindowSemiSleepStressTest] 本轮没有创建可测试窗口；"
                    + "已保留所有现有窗口不变，半休眠测试结束。"
                    + $" | protectedExisting={ProtectedExistingWindowTitles.Count}");
                return;
            }

            int preparedWindowCount = 0;
            for (int i = 0; i < OpenedWindows.Count; i++)
            {
                EditorWindow window = OpenedWindows[i];
                if (TryPrepareWindowForSleep(
                        window,
                        BuildCommercialEdgeBounds(mainBounds, i, OpenedWindows.Count),
                        allowTestProbeOverride: false))
                {
                    preparedWindowCount++;
                }
            }

            SceneView sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            sceneView.Focus();
            Debug.Log(
                "[ESWindowSemiSleepStressTest] 商业实机环境"
                + $" | productionHosts={preparedWindowCount}/{OpenedWindows.Count}"
                + $" | protectedExisting={ProtectedExistingWindowTitles.Count}"
                + $" | mainBounds={mainBounds}"
                + $" | negativeCoordinates={mainBounds.x < 0f || mainBounds.y < 0f}"
                + $" | pixelsPerPoint={EditorGUIUtility.pixelsPerPoint:0.###}"
                + " | edgeLayout=Left,Right,Top,Bottom"
                + " | narrowHosts="
                + ((OpenedWindows.Count + 3) / 4));
            EditorApplication.delayCall -= RequestAllWindowsSleep;
            EditorApplication.delayCall += RequestAllWindowsSleep;
        }

        private static void ReportProtectedExistingWindows()
        {
            if (ProtectedExistingWindowTitles.Count == 0)
                return;

            Debug.Log(
                "[ESWindowSemiSleepStressTest] 已保护现有窗口，未创建同类型副本："
                + string.Join("、", ProtectedExistingWindowTitles)
                + $" | count={ProtectedExistingWindowTitles.Count}");
        }

        private static bool TryPrepareWindowForSleep(
            EditorWindow window,
            Rect dockBounds,
            bool allowTestProbeOverride)
        {
            if (window == null || !ESWindowFoundation.IsBound(window))
            {
                Debug.LogWarning(
                    "[ESWindowSemiSleepStressTest] 跳过未接入 ES Presentation 的窗口："
                    + (window == null ? "<null>" : window.GetType().FullName));
                return false;
            }
            if (!ESWindowFoundation.IsWindowSleepSupported(window))
            {
                Debug.LogWarning(
                    "[ESWindowSemiSleepStressTest] 跳过未声明半休眠能力的窗口："
                    + window.GetType().FullName);
                return false;
            }
            if (allowTestProbeOverride)
            {
                ESWindowFoundation.TrySetWindowSleepAllowed(window, true);
                ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, true);
            }
            else if (!ESWindowFoundation.IsWindowSemiSleepAllowed(window))
            {
                Debug.LogWarning(
                    "[ESWindowSemiSleepStressTest] 跳过用户已关闭半休眠的窗口："
                    + window.GetType().FullName);
                return false;
            }

            return ES.EditorInternal.ESEditorPresentation.SetWindowSemiSleepDockBounds(
                window,
                dockBounds);
        }

        private static bool HasOpenWindowInstance(Type windowType)
        {
            if (windowType == null || !typeof(EditorWindow).IsAssignableFrom(windowType))
                return false;
            if (hasOpenInstancesMethod == null)
            {
                hasOpenInstancesMethod = typeof(EditorWindow)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == nameof(EditorWindow.HasOpenInstances)
                        && method.IsGenericMethodDefinition
                        && method.GetParameters().Length == 0);
            }
            if (hasOpenInstancesMethod == null)
                throw new MissingMethodException(
                    typeof(EditorWindow).FullName,
                    nameof(EditorWindow.HasOpenInstances));

            return (bool)hasOpenInstancesMethod
                .MakeGenericMethod(windowType)
                .Invoke(null, null);
        }

        private static void RequestAllWindowsSleep()
        {
            int sleeping = OpenedWindows.Count(window =>
                window != null
                && ES.EditorInternal.ESEditorPresentation.RequestWindowSemiSleep(window));
            Debug.Log($"[ESWindowSemiSleepStressTest] 已打开 {OpenedWindows.Count}/{WindowCount} 个窗口，"
                      + $"已保护现有窗口 {ProtectedExistingWindowTitles.Count} 个，"
                      + $"已请求半休眠 {sleeping} 个。点击任一 100×100 窗口可验证唤醒与回位。");
        }

        internal static Rect BuildSleepBounds(Rect mainBounds, int index)
        {
            return BuildSleepBounds(mainBounds, index, WindowCount);
        }

        internal static Rect BuildSleepBounds(Rect mainBounds, int index, int windowCount)
        {
            int safeCount = Mathf.Max(1, windowCount);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(safeCount)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(safeCount / (float)columns));
            int column = safeIndex % columns;
            int row = safeIndex / columns;
            float left = mainBounds.x + Margin;
            float top = mainBounds.y + Margin;
            float width = Mathf.Max(SleepSize, mainBounds.width - Margin * 2f);
            float height = Mathf.Max(SleepSize, mainBounds.height - Margin * 2f);
            float x = columns <= 1
                ? left
                : Mathf.Lerp(left, left + width - SleepSize, column / (float)(columns - 1));
            float y = rows <= 1
                ? top
                : Mathf.Lerp(top, top + height - SleepSize, row / (float)(rows - 1));
            return new Rect(x, y, SleepSize, SleepSize);
        }

        internal static Rect BuildCommercialEdgeBounds(Rect mainBounds, int index, int windowCount)
        {
            int safeCount = Mathf.Max(1, windowCount);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            int edge = safeIndex % 4;
            int slot = safeIndex / 4;
            int countOnEdge = Mathf.Max(1, Mathf.CeilToInt((safeCount - edge) / 4f));
            float progress = countOnEdge <= 1 ? 0.5f : slot / (float)(countOnEdge - 1);
            float left = mainBounds.x + Margin;
            float right = mainBounds.xMax - Margin - SleepSize;
            float top = mainBounds.y + Margin;
            float bottom = mainBounds.yMax - Margin - SleepSize;

            switch (edge)
            {
                case 0:
                    return new Rect(left, Mathf.Lerp(top, bottom, progress), SleepSize, SleepSize);
                case 1:
                    return new Rect(right, Mathf.Lerp(top, bottom, progress), SleepSize, SleepSize);
                case 2:
                    return new Rect(Mathf.Lerp(left, right, progress), top, SleepSize, SleepSize);
                default:
                    return new Rect(Mathf.Lerp(left, right, progress), bottom, SleepSize, SleepSize);
            }
        }

        private static Rect BuildAwakeBounds(Rect mainBounds, int index)
        {
            float width = index % 4 == 0
                ? Mathf.Min(360f, Mathf.Max(240f, mainBounds.width - Margin * 2f))
                : Mathf.Clamp(mainBounds.width * 0.44f, 620f, 920f);
            float height = Mathf.Clamp(mainBounds.height * 0.58f, 480f, 720f);
            int column = index % Columns;
            int row = index / Columns;
            float xRange = Mathf.Max(0f, mainBounds.width - width - Margin * 2f);
            float yRange = Mathf.Max(0f, mainBounds.height - height - Margin * 2f);
            return new Rect(
                mainBounds.x + Margin + xRange * column / Mathf.Max(1, Columns - 1),
                mainBounds.y + Margin + yRange * row / Mathf.Max(1, Rows - 1),
                width,
                height);
        }

        private static Rect BuildBenchmarkAwakeBounds(Rect mainBounds, int index, int windowCount)
        {
            const float width = 360f;
            const float height = 180f;
            int safeCount = Mathf.Max(1, windowCount);
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(safeCount)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(safeCount / (float)columns));
            int column = Mathf.Clamp(index, 0, safeCount - 1) % columns;
            int row = Mathf.Clamp(index, 0, safeCount - 1) / columns;
            float xRange = Mathf.Max(0f, mainBounds.width - width - Margin * 2f);
            float yRange = Mathf.Max(0f, mainBounds.height - height - Margin * 2f);
            return new Rect(
                mainBounds.x + Margin + xRange * column / Mathf.Max(1, columns - 1),
                mainBounds.y + Margin + yRange * row / Mathf.Max(1, rows - 1),
                width,
                height);
        }

        private static void StopOpeningQueue()
        {
            EditorApplication.delayCall -= RequestAllWindowsSleep;
            bool closeBenchmarkProbes = benchmarkWindowCount > 0;
            EditorApplication.update -= OpenNextWindow;
            EditorApplication.update -= UpdatePerformanceBenchmark;
            AssemblyReloadEvents.beforeAssemblyReload -= StopOpeningQueue;
            EditorApplication.quitting -= StopOpeningQueue;
            if (benchmarkSampling)
            {
                lastPerformanceSample =
                    ES.EditorInternal.ESEditorPresentation.EndSemiSleepPerformanceSample();
                benchmarkSampling = false;
            }
            benchmarkWindowCount = 0;
            benchmarkEndsAt = 0d;
            if (closeBenchmarkProbes)
                CloseBenchmarkProbeWindows();
        }

        private static void CloseOpenedWindows()
        {
            for (int i = OpenedWindows.Count - 1; i >= 0; i--)
            {
                EditorWindow window = OpenedWindows[i];
                if (window != null)
                    window.Close();
            }
            OpenedWindows.Clear();
        }

        private static void CloseBenchmarkProbeWindows()
        {
            ESWindowSleepBenchmarkProbeWindow[] probes =
                Resources.FindObjectsOfTypeAll<ESWindowSleepBenchmarkProbeWindow>();
            foreach (ESWindowSleepBenchmarkProbeWindow probe in probes)
            {
                if (probe != null)
                    probe.Close();
            }
            OpenedWindows.RemoveAll(window =>
                window == null || window is ESWindowSleepBenchmarkProbeWindow);
        }
    }
}
