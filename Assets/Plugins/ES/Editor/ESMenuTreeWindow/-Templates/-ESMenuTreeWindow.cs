using ES;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace ES {
    internal static class ESMenuTreeUnityIconResolver
    {
        private static readonly Dictionary<string, Texture> Cache =
            new Dictionary<string, Texture>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Texture> BrandCache =
            new Dictionary<string, Texture>(StringComparer.Ordinal);

        internal static Texture Resolve(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
                return null;
            string normalized = iconName.Trim();
            if (Cache.TryGetValue(normalized, out Texture cached))
                return cached;

            Texture icon = EditorGUIUtility.Load("Icons/" + normalized + ".png") as Texture;
            if (icon == null && normalized.StartsWith("d_", StringComparison.Ordinal))
                icon = EditorGUIUtility.Load("Icons/" + normalized.Substring(2) + ".png") as Texture;
            if (icon == null)
                icon = ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(
                    "d_UnityEditor.ConsoleWindow");
            if (icon == null)
                icon = ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(
                    "d_console.infoicon");
            Cache[normalized] = icon;
            return icon;
        }

        internal static Texture ResolveBrand(string path)
        {
            return ResolveBrand(null, path);
        }

        internal static Texture ResolveBrand(string stableId, string path)
        {
            string key = ((stableId ?? string.Empty) + " " + (path ?? string.Empty))
                .ToLowerInvariant();
            Texture semanticUnityIcon = ResolveUnitySemanticIcon(key);
            if (semanticUnityIcon != null)
                return semanticUnityIcon;
            string iconName = ResolveBrandName(key);
            if (string.IsNullOrEmpty(iconName)
                || string.Equals(iconName, "workbench", StringComparison.Ordinal)
                || string.Equals(iconName, "inspector", StringComparison.Ordinal))
            {
                // These legacy ES PNGs are visually empty rounded squares. Unknown
                // pages and Inspector technical shells use a neutral Unity icon
                // instead of presenting an empty or misleading brand mark.
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(
                           "d_UnityEditor.ConsoleWindow")
                    ?? ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(
                        "d_console.infoicon");
            }
            if (BrandCache.TryGetValue(iconName, out Texture cached))
                return cached;

            Texture icon = ES.EditorInternal.ESEditorPresentation.LoadESBrandIcon(iconName);
            BrandCache[iconName] = icon;
            return icon;
        }

        internal static string ResolveExplicitSemanticIcon(
            string stableId,
            string path,
            string requestedIcon)
        {
            string key = ((stableId ?? string.Empty) + " " + (path ?? string.Empty))
                .ToLowerInvariant();
            // These are page contracts, not fuzzy title decoration. Keep the
            // concrete object being edited as the icon authority.
            if (ContainsAny(key, "material-replacement", "材质"))
                return "d_Material Icon";
            if (ContainsAny(key, "prefab-management", "prefab", "预制体"))
                return "d_Prefab Icon";
            if (ContainsAny(key, "physics-align", "physics", "物理"))
                return "d_Grid.BoxTool";
            if (ContainsAny(key, "animation-batch-setting", "animation", "动画"))
                return "d_AnimationClip Icon";
            if (ContainsAny(key, "batch-static-setting", "static", "静态"))
                return "Static On";
            if (ContainsAny(key, "batch-rename", "rename", "重命名"))
                return "d_TreeEditor.Duplicate";
            if (ContainsAny(key, "lighting-settings", "lighting", "灯光"))
                return "d_Lighting";
            if (ContainsAny(key, "particle-system-adjustment", "particle", "粒子"))
                return "d_ParticleSystem Icon";
            if (ContainsAny(key, "texture-sprite", "texture", "sprite", "纹理", "精灵"))
                return "d_Texture Icon";
            if (ContainsAny(key, "unity-package", "package", "打包"))
                return "Package Manager";
            if (ContainsAny(key, "object-pool", "pool", "对象池"))
                return "d_Prefab Icon";
            if (ContainsAny(key, "top-toolbar", "toolbar", "快捷入口"))
                return "d_SceneAsset Icon";
            if (ContainsAny(key, "asset-reference-checker", "reference", "引用"))
                return "d_Search Icon";
            if (ContainsAny(key, "scene-optimization", "optimization", "优化"))
                return "d_UnityEditor.ProfilerWindow";
            if (ContainsAny(key, "scene-text-repair", "text-repair", "文本"))
                return "d_TextAsset Icon";
            return requestedIcon;
        }

        private static Texture ResolveUnitySemanticIcon(string key)
        {
            // Stable page identity is prepended by ResolveBrand. Concrete asset
            // semantics therefore win over broad host words in the path.
            if (ContainsAny(key, "camera", "相机"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Camera Icon");
            if (ContainsAny(key, "shadergraph", "shader graph", "shader", "着色器"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Shader Icon");
            if (ContainsAny(key, "material", "材质"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Material Icon");
            if (ContainsAny(key, "particle", "particlesystem", "vfx", "effect", "粒子", "特效"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_ParticleSystem Icon");
            if (ContainsAny(key, "prefab", "预制体"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Prefab Icon");
            if (ContainsAny(key, "model", "mesh", "模型", "网格"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Mesh Icon");
            if (ContainsAny(key, "hierarchy", "层级"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(
                    "d_UnityEditor.SceneHierarchyWindow");
            if (ContainsAny(key, "lighting", "light", "灯光"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Lighting");
            if (ContainsAny(key, "texture", "sprite", "纹理", "精灵"))
                return ES.EditorInternal.ESEditorPresentation.LoadUnityIcon("d_Texture Icon");
            return null;
        }

        private static string ResolveBrandName(string key)
        {
            // 页面路径描述的是用户正在操作的业务对象；Graph/节点语义必须
            // 先于 Agent/Command 等宿主技术名词，避免同一页面被贴错图标。
            if (ContainsAny(key, "graph", "node", "flow", "图表", "节点", "流程")) return "graph";
            if (ContainsAny(key, "camera", "相机")) return null;
            if (ContainsAny(key, "agent", "协作")) return "agent";
            if (ContainsAny(key, "automation", "自动化", "command")) return "automation";
            if (ContainsAny(key, "diagnostic", "validation", "test", "验证", "诊断", "测试")) return "diagnostics";
            if (ContainsAny(key, "font", "字体")) return "font";
            if (ContainsAny(key, "audio", "sound", "音频", "音效")) return "audio";
            if (ContainsAny(key, "track", "timeline", "animation", "动作", "轨道")) return "graph";
            if (ContainsAny(key, "scene", "world", "environment", "场景", "环境")) return "scene";
            if (ContainsAny(key, "build", "bake", "release", "publish", "构建", "发布")) return "build";
            if (ContainsAny(key, "package", "installer", "dependency", "安装", "依赖")) return "package";
            if (ContainsAny(key, "settings", "config", "theme", "设置", "配置", "主题")) return "settings";
            if (ContainsAny(key, "inspector", "drawer", "property", "检查器", "属性")) return "inspector";
            if (ContainsAny(key, "data", "so", "table", "catalog", "数据", "表", "目录")) return "data";
            if (ContainsAny(key, "asset", "resource", "res", "资源", "资产")) return "assets";
            if (ContainsAny(key, "workbench", "工作台", "window", "窗口")) return "workbench";
            return null;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
                if (!string.IsNullOrEmpty(tokens[i])
                    && value.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }
    }

    public interface IESWindowPageContextHost
    {
        string ESWindow_SelectedPageId { get; }
        bool ESWindow_TrySelectPage(string stableId, bool revealInMenu = true);
    }

    public enum ESMenuTreePageStatus
    {
        Ready,
        Info,
        Warning,
        Error,
        ReadOnly,
        Modified
    }

    public enum ESMenuTreePageLayout
    {
        Standard,
        Inspector,
        Wide,
        Canvas,
        Compact
    }

    public enum ESMenuTreePageLeaveReason
    {
        Navigate,
        RebuildView,
        RebuildWindow,
        RemoveRuntimePage,
        ReplaceRuntimePage
    }

    public enum ESMenuTreePageTaskState
    {
        Succeeded,
        Cancelled,
        Failed
    }

    /// <summary>右上工具栏的固定职责分区；全局动作不依赖页面上下文，页面动作只作用于当前页。</summary>
    public enum ESMenuTreeToolbarScope
    {
        Global,
        Page
    }

    public enum ESMenuTreeGroupClickBehavior
    {
        SelectFirstDescendant,
        ToggleExpansion
    }

    public sealed class ESMenuTreePageTaskResult
    {
        internal ESMenuTreePageTaskResult(
            string taskId,
            ESMenuTreePageTaskState state,
            Exception exception)
        {
            TaskId = taskId ?? string.Empty;
            State = state;
            Exception = exception;
        }

        public string TaskId { get; }
        public ESMenuTreePageTaskState State { get; }
        public Exception Exception { get; }
    }

    /// <summary>运行时菜单变更的统一结果；失败原因可直接展示给用户或测试。</summary>
    public readonly struct ESMenuTreeMutationResult
    {
        private readonly string error;

        private ESMenuTreeMutationResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            this.error = error;
        }

        public bool Succeeded { get; }
        public string Error => error ?? string.Empty;

        internal static ESMenuTreeMutationResult Success()
        {
            return new ESMenuTreeMutationResult(true, string.Empty);
        }

        internal static ESMenuTreeMutationResult Failure(string error)
        {
            return new ESMenuTreeMutationResult(false, error);
        }
    }

    public sealed class ESMenuTreePageContext
    {
        private sealed class RunningPageTask
        {
            internal int Generation;
            internal CancellationTokenSource Cancellation;
        }

        private readonly Action<string, ESMenuTreePageStatus> setStatus;
        private readonly EditorWindow window;
        private readonly ESMenuTreePage page;
        private readonly ESMenuTreePageDefinition definition;
        private readonly Action requestMenuRebuild;
        private readonly Action<string> selectPage;
        private readonly Action refreshPageActions;
        private readonly Action refreshPendingChanges;
        private readonly Action rebuildView;
        private readonly Action<string, ESMenuTreePageStatus> setMenuBadge;
        private readonly Action clearMenuBadge;
        private readonly Action<string, ESMenuTreePageStatus, ESEditorFeedbackSoundKind?, bool> publishFeedback;
        private readonly Func<bool> isAvailable;
        private readonly Func<bool> isSelected;
        private readonly SynchronizationContext synchronizationContext;
        private readonly Dictionary<string, RunningPageTask> runningTasks =
            new Dictionary<string, RunningPageTask>(StringComparer.Ordinal);
        private int taskGeneration;
        private bool invalidated;

        internal ESMenuTreePageContext(
            EditorWindow window,
            string stableId,
            string path,
            ESMenuTreePage page,
            ESMenuTreePageDefinition definition,
            Action<string, ESMenuTreePageStatus> setStatus,
            Action requestMenuRebuild,
            Action<string> selectPage,
            Action refreshPageActions,
            Action refreshPendingChanges,
            Action rebuildView,
            Action<string, ESMenuTreePageStatus> setMenuBadge,
            Action clearMenuBadge,
            Func<bool> isAvailable,
            Func<bool> isSelected,
            Action<string, ESMenuTreePageStatus, ESEditorFeedbackSoundKind?, bool> publishFeedback)
        {
            this.window = window;
            StableId = stableId ?? string.Empty;
            Path = path ?? string.Empty;
            this.page = page;
            this.definition = definition;
            this.setStatus = setStatus;
            this.requestMenuRebuild = requestMenuRebuild;
            this.selectPage = selectPage;
            this.refreshPageActions = refreshPageActions;
            this.refreshPendingChanges = refreshPendingChanges;
            this.rebuildView = rebuildView;
            this.setMenuBadge = setMenuBadge;
            this.clearMenuBadge = clearMenuBadge;
            this.isAvailable = isAvailable;
            this.isSelected = isSelected;
            this.publishFeedback = publishFeedback;
            synchronizationContext = SynchronizationContext.Current;
        }

        public EditorWindow Window => IsAvailable ? window : null;
        public string StableId { get; }
        public string Path { get; }
        public ESMenuTreePage Page => IsAvailable ? page : null;
        public ESMenuTreePageDefinition Definition => IsAvailable ? definition : null;
        public bool IsAvailable => !invalidated && isAvailable?.Invoke() == true;
        public bool IsSelected => IsAvailable && isSelected?.Invoke() == true;

        public TWindow GetWindow<TWindow>() where TWindow : EditorWindow
        {
            return IsAvailable ? Window as TWindow : null;
        }

        public TPage GetPage<TPage>() where TPage : ESMenuTreePage
        {
            return IsAvailable ? Page as TPage : null;
        }

        public TTarget GetOdinTarget<TTarget>() where TTarget : class
        {
            return IsAvailable ? (Page as ESOdinPropertyTreePage)?.PrimaryTarget as TTarget : null;
        }

        public TState GetPageState<TState>() where TState : class
        {
            if (!IsAvailable)
                return null;
            if (Page is ESOdinPropertyTreePage odinPage)
                return odinPage.PrimaryTarget as TState;
            return (Page as IESMenuTreePageStateProvider)?.PageState as TState;
        }

        public void SetStatus(string message, ESMenuTreePageStatus status = ESMenuTreePageStatus.Info)
        {
            if (!IsAvailable)
                return;
            setStatus?.Invoke(message, status);
        }

        public void RequestMenuRebuild()
        {
            if (!IsAvailable)
                return;
            requestMenuRebuild?.Invoke();
        }

        public void SelectPage(string stableId)
        {
            if (!IsAvailable)
                return;
            selectPage?.Invoke(stableId);
        }

        public void SelectSelf()
        {
            if (!IsAvailable)
                return;
            selectPage?.Invoke(StableId);
        }

        public void RefreshPageActions()
        {
            if (!IsAvailable)
                return;
            refreshPageActions?.Invoke();
        }

        public void RefreshPendingChanges()
        {
            if (!IsAvailable)
                return;
            refreshPendingChanges?.Invoke();
        }

        public void RebuildView()
        {
            if (!IsAvailable)
                return;
            rebuildView?.Invoke();
        }

        public void SetMenuBadge(
            string text,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info)
        {
            if (!IsAvailable)
                return;
            setMenuBadge?.Invoke(text, status);
        }

        public void ClearMenuBadge()
        {
            if (!IsAvailable)
                return;
            clearMenuBadge?.Invoke();
        }

        public void Notify(
            string message,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info,
            ESEditorFeedbackSoundKind? sound = null,
            bool showNotification = true)
        {
            if (!IsAvailable)
                return;
            publishFeedback?.Invoke(message, status, sound, showNotification);
        }

        public bool RunTask(
            string taskId,
            Func<CancellationToken, Task> operation,
            Action<ESMenuTreePageTaskResult> completed = null,
            bool replaceExisting = false)
        {
            if (!IsAvailable || operation == null || synchronizationContext == null)
                return false;
            string normalizedId = taskId?.Trim();
            if (string.IsNullOrEmpty(normalizedId))
                throw new ArgumentException("页面任务 ID 不能为空。", nameof(taskId));
            if (runningTasks.TryGetValue(normalizedId, out RunningPageTask existing))
            {
                if (!replaceExisting)
                    return false;
                existing.Cancellation.Cancel();
                runningTasks.Remove(normalizedId);
            }

            var running = new RunningPageTask
            {
                Generation = ++taskGeneration,
                Cancellation = new CancellationTokenSource()
            };
            runningTasks.Add(normalizedId, running);
            Task task;
            try
            {
                task = operation(running.Cancellation.Token) ?? Task.CompletedTask;
            }
            catch (Exception exception)
            {
                FinishTask(normalizedId, running, Task.FromException(exception), completed);
                return true;
            }

            task.ContinueWith(
                finished => synchronizationContext.Post(
                    _ => FinishTask(normalizedId, running, finished, completed),
                    null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return true;
        }

        public bool CancelTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)
                || !runningTasks.TryGetValue(taskId.Trim(), out RunningPageTask running))
                return false;
            running.Cancellation.Cancel();
            return true;
        }

        public bool IsTaskRunning(string taskId)
        {
            return !string.IsNullOrWhiteSpace(taskId)
                && runningTasks.ContainsKey(taskId.Trim());
        }

        public void CancelAllTasks()
        {
            foreach (RunningPageTask running in runningTasks.Values)
                running.Cancellation.Cancel();
        }

        private void FinishTask(
            string taskId,
            RunningPageTask expected,
            Task task,
            Action<ESMenuTreePageTaskResult> completed)
        {
            if (!runningTasks.TryGetValue(taskId, out RunningPageTask running)
                || !ReferenceEquals(running, expected))
            {
                expected.Cancellation.Dispose();
                return;
            }
            runningTasks.Remove(taskId);
            running.Cancellation.Dispose();
            if (invalidated)
                return;

            ESMenuTreePageTaskState state;
            Exception failure = null;
            if (task.IsCanceled)
            {
                state = ESMenuTreePageTaskState.Cancelled;
            }
            else if (task.IsFaulted)
            {
                state = ESMenuTreePageTaskState.Failed;
                failure = task.Exception?.GetBaseException();
            }
            else
            {
                state = ESMenuTreePageTaskState.Succeeded;
            }

            try
            {
                completed?.Invoke(new ESMenuTreePageTaskResult(taskId, state, failure));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal void Invalidate()
        {
            invalidated = true;
            CancelAllTasks();
        }
    }

    public sealed class ESMenuTreePageAction
    {
        internal readonly string Id;
        internal readonly string Text;
        internal readonly string Tooltip;
        internal readonly Action<ESMenuTreePageContext> Execute;
        internal Texture Icon;
        internal Func<ESMenuTreePageContext, bool> Enabled;
        internal Func<ESMenuTreePageContext, bool> Visible;
        internal Func<ESMenuTreePageContext, bool> Checked;
        internal ESEditorFeedbackSoundKind? Sound;
        internal string SuccessMessage;
        internal int Priority;

        public string ActionId => Id;
        public ESMenuTreeToolbarScope Scope => ESMenuTreeToolbarScope.Page;

        public ESMenuTreePageAction(
            string id,
            string text,
            string tooltip,
            Action<ESMenuTreePageContext> execute)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("页面动作 ID 不能为空。", nameof(id));
            Id = id.Trim();
            Text = text?.Trim() ?? string.Empty;
            Tooltip = tooltip?.Trim() ?? string.Empty;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public ESMenuTreePageAction WithIcon(Texture icon)
        {
            Icon = icon;
            return this;
        }

        public ESMenuTreePageAction WithIcon(EditorIcon icon)
        {
            Icon = icon?.Active;
            return this;
        }

        public ESMenuTreePageAction WithUnityIcon(string iconName)
        {
            Icon = ESMenuTreeUnityIconResolver.Resolve(iconName);
            return this;
        }

        public ESMenuTreePageAction When(Func<bool> enabled)
        {
            Enabled = enabled == null ? null : _ => enabled();
            return this;
        }

        public ESMenuTreePageAction When(Func<ESMenuTreePageContext, bool> enabled)
        {
            Enabled = enabled;
            return this;
        }

        public ESMenuTreePageAction WhenVisible(Func<bool> visible)
        {
            Visible = visible == null ? null : _ => visible();
            return this;
        }

        public ESMenuTreePageAction WhenVisible(Func<ESMenuTreePageContext, bool> visible)
        {
            Visible = visible;
            return this;
        }

        public ESMenuTreePageAction WithCheckedState(Func<bool> isChecked)
        {
            Checked = isChecked == null ? null : _ => isChecked();
            return this;
        }

        public ESMenuTreePageAction WithCheckedState(
            Func<ESMenuTreePageContext, bool> isChecked)
        {
            Checked = isChecked;
            return this;
        }

        public ESMenuTreePageAction WithSuccessFeedback(
            string message,
            ESEditorFeedbackSoundKind sound = ESEditorFeedbackSoundKind.Confirm)
        {
            SuccessMessage = message?.Trim() ?? string.Empty;
            Sound = sound;
            return this;
        }

        /// <summary>Higher-priority actions stay visible longer as the header becomes narrower.</summary>
        public ESMenuTreePageAction WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }
    }

    public sealed class ESMenuTreeGlobalAction
    {
        internal readonly string Id;
        internal readonly string Text;
        internal readonly string Tooltip;
        internal readonly Action Execute;
        internal Texture Icon;
        internal Func<bool> Enabled;
        internal Func<bool> Visible;
        internal Func<bool> Checked;
        internal int Priority;

        public string ActionId => Id;
        public ESMenuTreeToolbarScope Scope => ESMenuTreeToolbarScope.Global;

        public ESMenuTreeGlobalAction(string id, string text, string tooltip, Action execute)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("全局动作 ID 不能为空。", nameof(id));
            Id = id.Trim();
            Text = text?.Trim() ?? string.Empty;
            Tooltip = tooltip?.Trim() ?? string.Empty;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public ESMenuTreeGlobalAction WithIcon(Texture icon)
        {
            Icon = icon;
            return this;
        }

        public ESMenuTreeGlobalAction WithIcon(EditorIcon icon)
        {
            Icon = icon?.Active;
            return this;
        }

        public ESMenuTreeGlobalAction WithUnityIcon(string iconName)
        {
            Icon = ESMenuTreeUnityIconResolver.Resolve(iconName);
            return this;
        }

        public ESMenuTreeGlobalAction When(Func<bool> enabled)
        {
            Enabled = enabled;
            return this;
        }

        public ESMenuTreeGlobalAction WhenVisible(Func<bool> visible)
        {
            Visible = visible;
            return this;
        }

        public ESMenuTreeGlobalAction WithCheckedState(Func<bool> isChecked)
        {
            Checked = isChecked;
            return this;
        }

        public ESMenuTreeGlobalAction WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }
    }

    public sealed class ESMenuTreePageDefinition
    {
        internal readonly List<ESMenuTreePageAction> PageActions = new List<ESMenuTreePageAction>();

        public string StableId { get; }
        public string Path { get; }
        /// <summary>导航树上的短显示标签；为空时使用路径的最后一段，不影响 Path/StableId。</summary>
        public string NavigationLabel { get; private set; } = string.Empty;
        public ESMenuTreePage Page { get; }
        public Texture Icon { get; private set; }
        public string Keywords { get; private set; } = string.Empty;
        public ESMenuTreePageLayout Layout { get; private set; } = ESMenuTreePageLayout.Standard;
        public float MaxContentWidth { get; private set; }
        public float ContentPadding { get; private set; } = -1f;
        public string SelectionMessage { get; private set; } = string.Empty;
        public bool ShowSelectionNotification { get; private set; }
        public ESEditorFeedbackSoundKind SelectionSound { get; private set; } =
            ESEditorFeedbackSoundKind.Navigate;
        public string RuntimeOwnerId { get; private set; } = string.Empty;
        public bool IsRuntimePage => !string.IsNullOrEmpty(RuntimeOwnerId);

        public ESMenuTreePageDefinition(string stableId, string path, ESMenuTreePage page)
        {
            StableId = string.IsNullOrWhiteSpace(stableId)
                ? throw new ArgumentException("页面 StableId 不能为空。", nameof(stableId))
                : stableId.Trim();
            Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("页面路径不能为空。", nameof(path))
                : path.Trim();
            Page = page ?? throw new ArgumentNullException(nameof(page));
        }

        public static ESMenuTreePageDefinition ForOdin(string stableId, string path, object target)
        {
            return new ESMenuTreePageDefinition(
                stableId, path, new ESOdinPropertyTreePage(target));
        }

        public static ESMenuTreePageDefinition ForOdinTargets(string stableId, string path, IList targets)
        {
            return new ESMenuTreePageDefinition(
                stableId, path, new ESOdinPropertyTreePage(targets));
        }

        public static ESMenuTreePageDefinition ForPanel(
            string stableId,
            string path,
            Action<ESMenuTreePageContext, VisualElement> buildContent,
            bool useVerticalScroll = true)
        {
            return new ESMenuTreePageDefinition(
                stableId,
                path,
                new ESMenuTreePanelPage(buildContent, useVerticalScroll));
        }

        public static ESMenuTreePageDefinition ForIMGUI<TState>(
            string stableId,
            string path,
            TState state,
            Action<ESMenuTreePageContext, TState> draw,
            bool useVerticalScroll = true)
            where TState : class
        {
            return new ESMenuTreePageDefinition(
                stableId,
                path,
                new ESMenuTreeIMGUIPage<TState>(state, draw, useVerticalScroll));
        }

        public ESMenuTreePageDefinition WithIcon(Texture icon)
        {
            Icon = icon;
            return this;
        }

        public ESMenuTreePageDefinition WithIcon(EditorIcon icon)
        {
            Icon = icon?.Active;
            return this;
        }

        public ESMenuTreePageDefinition WithUnityIcon(string iconName)
        {
            Icon = ESMenuTreeUnityIconResolver.Resolve(iconName);
            return this;
        }

        public ESMenuTreePageDefinition WithKeywords(string keywords)
        {
            Keywords = keywords?.Trim() ?? string.Empty;
            return this;
        }

        /// <summary>
        /// 为长页面路径提供稳定的导航短标签。完整路径仍保留在 tooltip 与页面上下文中，
        /// 该值只负责首屏可扫描性，不参与身份、持久化或页面匹配。
        /// </summary>
        public ESMenuTreePageDefinition WithNavigationLabel(string label)
        {
            NavigationLabel = label?.Trim() ?? string.Empty;
            return this;
        }

        public ESMenuTreePageDefinition WithLayout(
            ESMenuTreePageLayout layout,
            float maxContentWidth = 0f,
            float contentPadding = -1f)
        {
            Layout = layout;
            MaxContentWidth = Mathf.Max(0f, maxContentWidth);
            ContentPadding = contentPadding < 0f ? -1f : Mathf.Max(0f, contentPadding);
            return this;
        }

        public ESMenuTreePageDefinition WithSelectionFeedback(
            string message,
            ESEditorFeedbackSoundKind sound = ESEditorFeedbackSoundKind.Navigate,
            bool showNotification = false)
        {
            SelectionMessage = message?.Trim() ?? string.Empty;
            SelectionSound = sound;
            ShowSelectionNotification = showNotification;
            return this;
        }

        public ESMenuTreePageDefinition AddPageAction(ESMenuTreePageAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (PageActions.Any(existing => string.Equals(existing.Id, action.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("页面右上动作 ID 重复：" + action.Id);
            PageActions.Add(action);
            return this;
        }

        internal void SetRuntimeOwnerId(string ownerId)
        {
            RuntimeOwnerId = ownerId?.Trim() ?? string.Empty;
        }
    }

    public abstract class ESMenuTreePage : IDisposable
    {
        public abstract VisualElement CreateView(ESMenuTreePageContext context);

        public virtual bool HasPendingChanges => false;

        public virtual string PendingChangesSummary => "当前页面包含尚未提交的修改。";

        public virtual bool TrySavePendingChanges(out string failure)
        {
            failure = null;
            return true;
        }

        public virtual void DiscardPendingChanges()
        {
        }

        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        public virtual void Refresh()
        {
        }

        /// <summary>Releases view-only state before this page is locally or globally rebuilt.</summary>
        public virtual void ReleaseView()
        {
        }

        public virtual void Dispose()
        {
        }
    }

    public interface IESMenuTreePageStateProvider
    {
        object PageState { get; }
    }

    /// <summary>
    /// Hosts an existing IMGUI feature page without changing its data or interaction model.
    /// The page owns one optional vertical scroll view and deterministically releases view callbacks.
    /// </summary>
    public sealed class ESMenuTreeIMGUIPage<TState> : ESMenuTreePage, IESMenuTreePageStateProvider
        where TState : class
    {
        private readonly Action<ESMenuTreePageContext, TState> draw;
        private readonly bool useVerticalScroll;
        private Action<ESMenuTreePageContext, TState> onShow;
        private Action<ESMenuTreePageContext, TState> onRefresh;
        private Action<TState> onHide;
        private Action<TState> onReleaseView;
        private Action<TState> onDispose;
        private ESMenuTreePageContext context;
        private VisualElement viewRoot;
        private IMGUIContainer container;
        private Vector2 scrollPosition;
        private Exception drawFailure;
        private bool viewCreated;
        private bool disposed;

        public ESMenuTreeIMGUIPage(
            TState state,
            Action<ESMenuTreePageContext, TState> draw,
            bool useVerticalScroll = true)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.draw = draw ?? throw new ArgumentNullException(nameof(draw));
            this.useVerticalScroll = useVerticalScroll;
        }

        public TState State { get; }
        public bool UseVerticalScroll => useVerticalScroll;
        object IESMenuTreePageStateProvider.PageState => State;

        public ESMenuTreeIMGUIPage<TState> WithOnShow(
            Action<ESMenuTreePageContext, TState> callback)
        {
            onShow = callback;
            return this;
        }

        public ESMenuTreeIMGUIPage<TState> WithOnRefresh(
            Action<ESMenuTreePageContext, TState> callback)
        {
            onRefresh = callback;
            return this;
        }

        public ESMenuTreeIMGUIPage<TState> WithOnHide(Action<TState> callback)
        {
            onHide = callback;
            return this;
        }

        public ESMenuTreeIMGUIPage<TState> WithOnReleaseView(Action<TState> callback)
        {
            onReleaseView = callback;
            return this;
        }

        public ESMenuTreeIMGUIPage<TState> WithOnDispose(Action<TState> callback)
        {
            onDispose = callback;
            return this;
        }

        public override VisualElement CreateView(ESMenuTreePageContext pageContext)
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
            if (viewCreated)
                throw new InvalidOperationException("IMGUI 页面视图已经创建，请先调用 ReleaseView。");

            context = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
            viewRoot = new VisualElement { name = "ESMenuTreeIMGUIPage" };
            viewRoot.style.flexGrow = 1f;
            viewRoot.style.flexShrink = 1f;
            viewRoot.style.flexBasis = 0f;
            viewRoot.style.minWidth = 0f;
            viewRoot.style.minHeight = 0f;
            container = new IMGUIContainer(DrawPage) { name = "ESMenuTreeIMGUIContainer" };
            container.style.flexGrow = 1f;
            container.style.flexShrink = 1f;
            container.style.flexBasis = 0f;
            container.style.minWidth = 0f;
            container.style.minHeight = 0f;
            viewRoot.Add(container);
            viewCreated = true;
            drawFailure = null;
            return viewRoot;
        }

        public override void OnShow()
        {
            if (!disposed && context?.IsAvailable == true)
                onShow?.Invoke(context, State);
        }

        public override void OnHide()
        {
            if (!disposed && viewCreated)
                onHide?.Invoke(State);
        }

        public override void Refresh()
        {
            if (disposed || context?.IsAvailable != true)
                return;
            onRefresh?.Invoke(context, State);
            container?.MarkDirtyRepaint();
        }

        public override void ReleaseView()
        {
            if (!viewCreated)
                return;
            viewCreated = false;
            try
            {
                onReleaseView?.Invoke(State);
            }
            finally
            {
                if (container != null)
                    container.onGUIHandler = null;
                viewRoot?.Clear();
                container = null;
                viewRoot = null;
                context = null;
                drawFailure = null;
            }
        }

        public override void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Exception failure = null;
            try
            {
                ReleaseView();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            try
            {
                onDispose?.Invoke(State);
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                else
                    Debug.LogException(exception);
            }

            if (failure != null)
                throw failure;
        }

        private void DrawPage()
        {
            if (disposed || context?.IsAvailable != true)
                return;

            if (drawFailure != null)
            {
                EditorGUILayout.HelpBox(
                    "IMGUI 页面绘制失败。\n原因：" + drawFailure.Message
                    + "\n影响：当前页面已暂停绘制，其他页面不受影响。"
                    + "\n恢复：修复依赖后重建当前页面。",
                    MessageType.Error);
                if (GUILayout.Button("重建当前页面", GUILayout.Height(26f)))
                {
                    drawFailure = null;
                    context.RebuildView();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            bool beganScroll = false;
            try
            {
                if (useVerticalScroll)
                {
                    scrollPosition.x = 0f;
                    scrollPosition = GUILayout.BeginScrollView(
                        scrollPosition,
                        false,
                        true,
                        GUIStyle.none,
                        GUI.skin.verticalScrollbar);
                    scrollPosition.x = 0f;
                    beganScroll = true;
                }
                draw(context, State);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                drawFailure = exception;
                Debug.LogException(exception);
                context.SetStatus("页面绘制失败：" + exception.Message, ESMenuTreePageStatus.Error);
            }
            finally
            {
                if (beganScroll)
                    GUILayout.EndScrollView();
            }
        }
    }

    /// <summary>
    /// Lightweight page for ordinary ES feature panels. The host caches its view and calls the
    /// configured lifecycle callbacks only on explicit navigation, refresh, rebuild, or disposal.
    /// </summary>
    public sealed class ESMenuTreePanelPage : ESMenuTreePage
    {
        private readonly Action<ESMenuTreePageContext, VisualElement> buildContent;
        private readonly bool useVerticalScroll;
        private Action<ESMenuTreePageContext> onShow;
        private Action<ESMenuTreePageContext> onRefresh;
        private Action onHide;
        private Action onReleaseView;
        private Action onDispose;
        private Func<bool> hasPendingChanges;
        private Func<string> pendingChangesSummary;
        private Func<bool> savePendingChanges;
        private Action discardPendingChanges;
        private ESMenuTreePageContext context;
        private VisualElement viewRoot;
        private bool viewCreated;
        private bool disposed;

        public ESMenuTreePanelPage(
            Action<ESMenuTreePageContext, VisualElement> buildContent,
            bool useVerticalScroll = true)
        {
            this.buildContent = buildContent
                ?? throw new ArgumentNullException(nameof(buildContent));
            this.useVerticalScroll = useVerticalScroll;
        }

        public ESMenuTreePanelPage WithOnShow(Action<ESMenuTreePageContext> callback)
        {
            onShow = callback;
            return this;
        }

        public ESMenuTreePanelPage WithOnRefresh(Action<ESMenuTreePageContext> callback)
        {
            onRefresh = callback;
            return this;
        }

        public ESMenuTreePanelPage WithOnHide(Action callback)
        {
            onHide = callback;
            return this;
        }

        public ESMenuTreePanelPage WithOnReleaseView(Action callback)
        {
            onReleaseView = callback;
            return this;
        }

        public ESMenuTreePanelPage WithOnDispose(Action callback)
        {
            onDispose = callback;
            return this;
        }

        public ESMenuTreePanelPage WithPendingChanges(
            Func<bool> hasChanges,
            Func<bool> saveChanges,
            Action discardChanges,
            Func<string> summary = null)
        {
            hasPendingChanges = hasChanges;
            savePendingChanges = saveChanges;
            discardPendingChanges = discardChanges;
            pendingChangesSummary = summary;
            return this;
        }

        public override bool HasPendingChanges =>
            !disposed && hasPendingChanges?.Invoke() == true;

        public override string PendingChangesSummary =>
            pendingChangesSummary?.Invoke() ?? base.PendingChangesSummary;

        public override bool TrySavePendingChanges(out string failure)
        {
            failure = null;
            if (savePendingChanges == null || savePendingChanges())
                return true;
            failure = "页面保存回调返回失败。";
            return false;
        }

        public override void DiscardPendingChanges()
        {
            discardPendingChanges?.Invoke();
        }

        public override VisualElement CreateView(ESMenuTreePageContext pageContext)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESMenuTreePanelPage));
            if (viewCreated)
                throw new InvalidOperationException("功能面板视图已经创建，请先调用 ReleaseView。");

            context = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
            viewRoot = new VisualElement { name = "ESMenuTreePanelPage" };
            viewRoot.style.flexGrow = 1f;
            viewRoot.style.flexShrink = 1f;
            viewRoot.style.flexBasis = 0f;
            viewRoot.style.minWidth = 0f;
            viewRoot.style.minHeight = 0f;
            viewCreated = true;

            VisualElement content = viewRoot;
            if (useVerticalScroll)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    name = "ESMenuTreePanelScroll",
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.Auto
                };
                scroll.style.flexGrow = 1f;
                scroll.style.flexShrink = 1f;
                scroll.style.flexBasis = 0f;
                scroll.style.minWidth = 0f;
                scroll.style.minHeight = 0f;
                viewRoot.Add(scroll);
                content = scroll.contentContainer;
                content.style.flexGrow = 1f;
                content.style.flexShrink = 1f;
                content.style.minWidth = 0f;
                content.style.width = Length.Percent(100f);
            }

            buildContent(context, content);
            return viewRoot;
        }

        public override void OnShow()
        {
            if (!disposed && context?.IsAvailable == true)
                onShow?.Invoke(context);
        }

        public override void Refresh()
        {
            if (!disposed && context?.IsAvailable == true)
                onRefresh?.Invoke(context);
        }

        public override void OnHide()
        {
            if (!disposed && viewCreated)
                onHide?.Invoke();
        }

        public override void ReleaseView()
        {
            if (!viewCreated)
                return;
            viewCreated = false;
            try
            {
                onReleaseView?.Invoke();
            }
            finally
            {
                viewRoot?.Clear();
                viewRoot = null;
                context = null;
            }
        }

        public override void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Exception failure = null;
            try
            {
                ReleaseView();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                onDispose?.Invoke();
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                else
                    Debug.LogException(exception);
            }
            if (failure != null)
                throw failure;
        }
    }

    /// <summary>
    /// Shared, unframed UI primitives for menu-tree pages, single-page windows, dialogs, and
    /// embedded editor panels. These helpers own presentation only; callers retain data and
    /// lifecycle ownership.
    /// </summary>
    public sealed class ESEditorFunctionalSection
    {
        public VisualElement Root { get; }
        public VisualElement Header { get; }
        public VisualElement HeaderActions { get; }
        public VisualElement Content { get; }
        public Label TitleLabel { get; }
        public Label DetailLabel { get; }
        public Label StatusLabel { get; }

        internal ESEditorFunctionalSection(
            VisualElement root,
            VisualElement header,
            VisualElement headerActions,
            VisualElement content,
            Label titleLabel,
            Label detailLabel,
            Label statusLabel)
        {
            Root = root;
            Header = header;
            HeaderActions = headerActions;
            Content = content;
            TitleLabel = titleLabel;
            DetailLabel = detailLabel;
            StatusLabel = statusLabel;
        }

        public void Add(VisualElement element)
        {
            if (element != null)
                Content.Add(element);
        }

        public void AddHeaderAction(VisualElement action)
        {
            if (action != null)
            {
                HeaderActions.style.display = DisplayStyle.Flex;
                HeaderActions.Add(action);
            }
        }
    }

    public static class ESEditorPanelUI
    {
        public static VisualElement CreateHeading(string title, string detail = null)
        {
            var heading = new VisualElement { name = "ESEditorPanelHeading" };
            heading.style.flexShrink = 0f;
            Label titleLabel = new Label(title ?? string.Empty);
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 18f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            heading.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Label detailLabel = new Label(detail.Trim());
                detailLabel.style.marginTop = 6f;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                heading.Add(detailLabel);
            }
            return heading;
        }

        public static VisualElement CreateSection(string title, string detail = null)
        {
            VisualElement section = CreateHeading(title, detail);
            section.name = "ESEditorPanelSection";
            section.AddToClassList("es-panel-section");
            section.style.marginTop = 18f;
            section.style.paddingLeft = 10f;
            section.style.paddingRight = 10f;
            section.style.paddingTop = 9f;
            section.style.paddingBottom = 9f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                section,
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                ES.EditorInternal.ESEditorPresentation.DividerColor);
            section.style.borderLeftWidth = 3f;
            section.style.borderLeftColor = ES.EditorInternal.ESEditorPresentation.SelectionColor;
            Label titleLabel = section.Q<Label>();
            if (titleLabel != null)
                titleLabel.style.fontSize = 13f;
            return section;
        }

        public static ESEditorFunctionalSection CreateFunctionalSection(
            string title,
            string detail = null,
            ESMenuTreePageStatus? status = null)
        {
            var root = new VisualElement { name = "ESEditorFunctionalSection" };
            root.AddToClassList("es-functional-section");
            root.style.minWidth = 0f;
            root.style.width = Length.Percent(100f);
            root.style.flexShrink = 1f;
            root.style.marginTop = 12f;
            root.style.marginBottom = 2f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                root,
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Section,
                ES.EditorInternal.ESEditorPresentation.DividerColor);

            var header = new VisualElement { name = "ESEditorFunctionalSectionHeader" };
            header.AddToClassList("es-functional-section-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;
            header.style.alignItems = Align.Center;
            header.style.minWidth = 0f;
            header.style.width = Length.Percent(100f);
            header.style.paddingLeft = 12f;
            header.style.paddingRight = 10f;
            header.style.paddingTop = 9f;
            header.style.paddingBottom = 9f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = ES.EditorInternal.ESEditorPresentation.DividerColor;
            root.Add(header);

            var titleBlock = new VisualElement { name = "ESEditorFunctionalSectionTitleBlock" };
            titleBlock.style.flexGrow = 1f;
            titleBlock.style.minWidth = 0f;
            var titleLabel = new Label(title ?? string.Empty)
            {
                name = "ESEditorFunctionalSectionTitle"
            };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 13f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleBlock.Add(titleLabel);

            Label detailLabel = null;
            if (!string.IsNullOrWhiteSpace(detail))
            {
                detailLabel = new Label(detail.Trim())
                {
                    name = "ESEditorFunctionalSectionDetail"
                };
                detailLabel.style.marginTop = 3f;
                detailLabel.style.fontSize = 10f;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                titleBlock.Add(detailLabel);
            }
            header.Add(titleBlock);

            Label statusLabel = null;
            if (status.HasValue)
            {
                statusLabel = new Label(GetStatusLabel(status.Value))
                {
                    name = "ESEditorFunctionalSectionStatus"
                };
                statusLabel.style.flexShrink = 0f;
                statusLabel.style.marginLeft = 8f;
                statusLabel.style.paddingLeft = 8f;
                statusLabel.style.paddingRight = 8f;
                statusLabel.style.paddingTop = 2f;
                statusLabel.style.paddingBottom = 2f;
                statusLabel.style.fontSize = 9f;
                statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                ES.EditorInternal.ESWindowPresentation.StyleStatusPill(
                    statusLabel, ToPresentationStatus(status.Value));
                header.Add(statusLabel);
            }

            var headerActions = new VisualElement { name = "ESEditorFunctionalSectionActions" };
            headerActions.AddToClassList("es-functional-section-actions");
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.flexWrap = Wrap.Wrap;
            headerActions.style.alignItems = Align.Center;
            headerActions.style.justifyContent = Justify.FlexEnd;
            headerActions.style.flexGrow = 0f;
            headerActions.style.flexShrink = 1f;
            headerActions.style.minWidth = 0f;
            headerActions.style.maxWidth = Length.Percent(100f);
            headerActions.style.marginLeft = 8f;
            headerActions.style.display = DisplayStyle.None;
            header.Add(headerActions);

            var content = new VisualElement { name = "ESEditorFunctionalSectionContent" };
            content.AddToClassList("es-functional-section-content");
            content.style.minWidth = 0f;
            content.style.paddingLeft = 12f;
            content.style.paddingRight = 12f;
            content.style.paddingTop = 10f;
            content.style.paddingBottom = 12f;
            root.Add(content);

            return new ESEditorFunctionalSection(
                root, header, headerActions, content, titleLabel, detailLabel, statusLabel);
        }

        public static VisualElement CreateActionRow(params VisualElement[] controls)
        {
            var row = new VisualElement { name = "ESEditorPanelActions" };
            row.AddToClassList("es-panel-actions");
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0f;
            row.style.width = Length.Percent(100f);
            row.style.flexShrink = 1f;
            row.style.marginTop = 14f;
            if (controls == null)
                return row;
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null)
                    row.Add(controls[i]);
            return row;
        }

        public static Button CreateButton(
            string text,
            string tooltip,
            Action action,
            bool primary = false)
        {
            return ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                text, tooltip, action, primary);
        }

        public static VisualElement CreateNotice(
            string title,
            string detail,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info)
        {
            var notice = new VisualElement { name = "ESEditorPanelNotice" };
            notice.AddToClassList("es-panel-notice");
            ES.EditorInternal.ESStatusKind presentationStatus = ToPresentationStatus(status);
            Color accent = ES.EditorInternal.ESEditorPresentation.GetStatusAccent(
                0, presentationStatus);
            Color surface = accent;
            surface.a = EditorGUIUtility.isProSkin ? 0.10f : 0.07f;
            notice.style.marginTop = 14f;
            notice.style.paddingLeft = 10f;
            notice.style.paddingRight = 10f;
            notice.style.paddingTop = 8f;
            notice.style.paddingBottom = 8f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                notice,
                surface,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                accent);
            notice.style.borderLeftWidth = 3f;

            Label titleLabel = new Label(title ?? string.Empty);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.color = accent;
            notice.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Label detailLabel = new Label(detail.Trim());
                detailLabel.style.marginTop = 4f;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionTextColor;
                notice.Add(detailLabel);
            }
            return notice;
        }

        private static string GetStatusLabel(ESMenuTreePageStatus status)
        {
            switch (status)
            {
                case ESMenuTreePageStatus.Ready: return "就绪";
                case ESMenuTreePageStatus.Warning: return "警告";
                case ESMenuTreePageStatus.Error: return "错误";
                case ESMenuTreePageStatus.ReadOnly: return "只读";
                case ESMenuTreePageStatus.Modified: return "已修改";
                default: return "信息";
            }
        }

        public static VisualElement CreateFieldRow(
            string label,
            VisualElement field,
            string tooltip = null)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));
            var row = new VisualElement { name = "ESEditorPanelFieldRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0f;
            row.style.width = Length.Percent(100f);
            row.style.marginTop = 8f;
            row.tooltip = tooltip ?? string.Empty;

            Label labelElement = new Label(label ?? string.Empty);
            labelElement.style.width = 150f;
            labelElement.style.minWidth = 96f;
            labelElement.style.maxWidth = 150f;
            labelElement.style.flexBasis = 112f;
            labelElement.style.flexShrink = 1f;
            labelElement.style.marginRight = 8f;
            labelElement.style.whiteSpace = WhiteSpace.Normal;
            labelElement.style.color = ES.EditorInternal.ESEditorPresentation.SectionTextColor;
            row.Add(labelElement);

            field.style.flexGrow = 1f;
            field.style.flexShrink = 1f;
            field.style.flexBasis = 160f;
            field.style.minWidth = 0f;
            field.style.maxWidth = Length.Percent(100f);
            row.Add(field);
            return row;
        }

        public static ScrollView CreateVerticalScrollView(string name = "ESEditorPanelScroll")
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = string.IsNullOrWhiteSpace(name) ? "ESEditorPanelScroll" : name.Trim(),
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            scroll.style.flexGrow = 1f;
            scroll.style.flexShrink = 1f;
            scroll.style.flexBasis = 0f;
            scroll.style.minWidth = 0f;
            scroll.style.minHeight = 0f;
            scroll.contentContainer.style.flexGrow = 1f;
            scroll.contentContainer.style.flexShrink = 1f;
            scroll.contentContainer.style.minWidth = 0f;
            return scroll;
        }

        public static VisualElement CreateEmptyState(
            string title,
            string detail,
            string actionText = null,
            Action action = null)
        {
            return ES.EditorInternal.ESWindowPresentation.CreateEmptyState(
                title, detail, actionText, action);
        }

        public static VisualElement CreateErrorState(
            string title,
            string cause,
            string impact,
            string recovery,
            string actionText = null,
            Action action = null)
        {
            return ES.EditorInternal.ESWindowPresentation.CreateErrorState(
                title, cause, impact, recovery, actionText, action);
        }

        private static ES.EditorInternal.ESStatusKind ToPresentationStatus(
            ESMenuTreePageStatus status)
        {
            switch (status)
            {
                case ESMenuTreePageStatus.Info: return ES.EditorInternal.ESStatusKind.Info;
                case ESMenuTreePageStatus.Warning: return ES.EditorInternal.ESStatusKind.Warning;
                case ESMenuTreePageStatus.Error: return ES.EditorInternal.ESStatusKind.Error;
                case ESMenuTreePageStatus.ReadOnly: return ES.EditorInternal.ESStatusKind.ReadOnly;
                case ESMenuTreePageStatus.Modified: return ES.EditorInternal.ESStatusKind.Modified;
                default: return ES.EditorInternal.ESStatusKind.Ready;
            }
        }
    }

    /// <summary>
    /// Owns the SerializedObject projection used by a UI Toolkit panel. Unity objects remain the
    /// source of truth; PropertyField binding supplies Undo, mixed values, and prefab overrides.
    /// </summary>
    public sealed class ESEditorSerializedPanelBinding : IDisposable
    {
        private readonly UnityEngine.Object[] targets;
        private readonly List<VisualElement> boundElements = new List<VisualElement>();
        private SerializedObject serializedObject;
        private bool disposed;

        public ESEditorSerializedPanelBinding(UnityEngine.Object target)
            : this(new[] { target })
        {
        }

        public ESEditorSerializedPanelBinding(IList<UnityEngine.Object> targetList)
        {
            if (targetList == null || targetList.Count == 0)
                throw new ArgumentException("序列化面板至少需要一个 Unity 对象。", nameof(targetList));
            targets = new UnityEngine.Object[targetList.Count];
            for (int i = 0; i < targetList.Count; i++)
            {
                UnityEngine.Object target = targetList[i];
                if (target == null)
                    throw new ArgumentException("序列化面板目标不能为 null。", nameof(targetList));
                targets[i] = target;
            }
            serializedObject = new SerializedObject(targets);
        }

        public IReadOnlyList<UnityEngine.Object> Targets => targets;
        public SerializedObject SerializedObject
        {
            get
            {
                EnsureAvailable();
                return serializedObject;
            }
        }

        public SerializedProperty FindProperty(string propertyPath)
        {
            EnsureAvailable();
            if (string.IsNullOrWhiteSpace(propertyPath))
                throw new ArgumentException("SerializedProperty 路径不能为空。", nameof(propertyPath));
            SerializedProperty property = serializedObject.FindProperty(propertyPath.Trim());
            if (property == null)
                throw new ArgumentException("找不到 SerializedProperty：" + propertyPath, nameof(propertyPath));
            return property;
        }

        public PropertyField CreatePropertyField(
            string propertyPath,
            string label = null,
            string tooltip = null)
        {
            SerializedProperty property = FindProperty(propertyPath);
            var field = string.IsNullOrWhiteSpace(label)
                ? new PropertyField(property)
                : new PropertyField(property, label.Trim());
            field.name = "ESSerializedPanelField-" + property.propertyPath.Replace('.', '-');
            field.tooltip = tooltip ?? string.Empty;
            field.BindProperty(property);
            boundElements.Add(field);
            return field;
        }

        public void Bind(VisualElement root)
        {
            EnsureAvailable();
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            root.Bind(serializedObject);
            if (!boundElements.Contains(root))
                boundElements.Add(root);
        }

        public void Update()
        {
            EnsureAvailable();
            serializedObject.UpdateIfRequiredOrScript();
        }

        public bool ApplyModifiedProperties()
        {
            EnsureAvailable();
            bool changed = serializedObject.ApplyModifiedProperties();
            if (!changed)
                return false;
            for (int i = 0; i < targets.Length; i++)
            {
                UnityEngine.Object target = targets[i];
                if (target == null)
                    continue;
                EditorUtility.SetDirty(target);
                if (target is Component || target is GameObject)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            for (int i = 0; i < boundElements.Count; i++)
                boundElements[i]?.Unbind();
            boundElements.Clear();
            serializedObject?.Dispose();
            serializedObject = null;
        }

        private void EnsureAvailable()
        {
            if (disposed || serializedObject == null)
                throw new ObjectDisposedException(nameof(ESEditorSerializedPanelBinding));
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] == null)
                    throw new InvalidOperationException("序列化面板目标已经失效，请重建页面。 ");
        }
    }

    /// <summary>
    /// Hosts an Odin PropertyTree inside the UI Toolkit menu window. Each page owns its tree and
    /// SerializedObject projection; the supplied targets remain the only source of editable data.
    /// </summary>
    public sealed class ESOdinPropertyTreePage : ESMenuTreePage
    {
        private readonly object[] targets;
        private readonly IReadOnlyList<object> readOnlyTargets;
        private readonly ESWindowPageBase legacyPage;
        private PropertyTree propertyTree;
        private SerializedObject serializedObject;
        private IMGUIContainer container;
        private ESMenuTreePageContext context;
        private Vector2 scrollPosition;
        private string drawFailure;
        private bool legacyInitialized;
        private bool failureReported;
        private bool disposed;

        public ESOdinPropertyTreePage(object target)
            : this(new[] { target })
        {
        }

        public ESOdinPropertyTreePage(IList targetList)
        {
            targets = CopyAndValidateTargets(targetList);
            readOnlyTargets = Array.AsReadOnly(targets);
            legacyPage = targets.Length == 1 ? targets[0] as ESWindowPageBase : null;
        }

        public IReadOnlyList<object> Targets => readOnlyTargets;
        public object PrimaryTarget => targets.Length > 0 ? targets[0] : null;

        public override VisualElement CreateView(ESMenuTreePageContext pageContext)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESOdinPropertyTreePage));

            context = pageContext;
            VisualElement root = new VisualElement { name = "ESOdinPropertyTreePage" };
            root.style.flexGrow = 1f;
            root.style.flexShrink = 1f;
            root.style.minWidth = 0f;
            root.style.minHeight = 0f;

            container = new IMGUIContainer(DrawOdinTree) { name = "ESOdinPropertyTreeIMGUI" };
            container.style.flexGrow = 1f;
            container.style.flexShrink = 1f;
            container.style.minWidth = 0f;
            container.style.minHeight = 0f;
            root.Add(container);
            return root;
        }

        public override void OnShow()
        {
            if (disposed)
                return;
            EnsureLegacyInitialized();
        }

        public override void OnHide()
        {
            ApplyPendingChanges();
        }

        public override void Refresh()
        {
            if (disposed)
                return;
            EnsureLegacyInitialized();
            EnsurePropertyTree();
            propertyTree.UpdateTree();
            container?.MarkDirtyRepaint();
        }

        public override void Dispose()
        {
            DisposeInternal(true);
        }

        internal bool SharesLegacyLifecycleWith(ESOdinPropertyTreePage other)
        {
            return legacyPage != null && other != null && ReferenceEquals(legacyPage, other.legacyPage);
        }

        internal void DisposeForRebuild()
        {
            DisposeInternal(false);
        }

        internal void RefreshFromSource()
        {
            if (disposed)
                return;
            if (legacyPage != null)
            {
                legacyPage.ES_Refresh();
                legacyInitialized = true;
            }
            Refresh();
        }

        private void DrawOdinTree()
        {
            if (disposed)
                return;

            if (!string.IsNullOrEmpty(drawFailure))
            {
                EditorGUILayout.HelpBox(
                    "Odin 页面绘制失败。\n原因：" + drawFailure
                    + "\n影响：当前页面暂时不可编辑，其他页面不受影响。"
                    + "\n恢复：点击下方按钮重新创建 PropertyTree。",
                    MessageType.Error);
                if (GUILayout.Button("重新创建 Odin 视图", GUILayout.MinHeight(26f)))
                    ResetAfterFailure();
                return;
            }

            bool beganScroll = false;
            try
            {
                EnsureLegacyInitialized();
                if (!TargetsAreAlive())
                    throw new InvalidOperationException("Odin 页面目标已经失效，请重新选择对象或刷新菜单。");
                EnsurePropertyTree();
                scrollPosition = GUILayout.BeginScrollView(
                    scrollPosition,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar);
                beganScroll = true;
                GUILayout.Space(6f);
                EditorGUI.BeginChangeCheck();
                try
                {
                    propertyTree.Draw(false);
                }
                finally
                {
                    if (EditorGUI.EndChangeCheck())
                        context?.RefreshPageActions();
                }
                GUILayout.Space(10f);
            }
            catch (Exception exception)
            {
                drawFailure = exception.Message;
                if (!failureReported)
                {
                    failureReported = true;
                    Debug.LogException(exception);
                    context?.Notify(
                        "Odin 页面绘制失败：" + exception.Message,
                        ESMenuTreePageStatus.Error,
                        ESEditorFeedbackSoundKind.Error);
                }
            }
            finally
            {
                if (beganScroll)
                    GUILayout.EndScrollView();
            }
        }

        private void EnsureLegacyInitialized()
        {
            if (legacyInitialized || legacyPage == null)
                return;
            legacyPage.ES_Refresh();
            legacyInitialized = true;
        }

        private void EnsurePropertyTree()
        {
            if (propertyTree != null)
                return;
            if (!TargetsAreAlive())
                throw new InvalidOperationException("Odin 页面目标已经失效，请重新选择对象或刷新菜单。");

            if (AllTargetsAreUnityObjects())
            {
                var unityTargets = new UnityEngine.Object[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                    unityTargets[i] = (UnityEngine.Object)targets[i];
                serializedObject = new SerializedObject(unityTargets);
                propertyTree = PropertyTree.Create(serializedObject);
            }
            else
            {
                propertyTree = targets.Length == 1
                    ? PropertyTree.Create(targets[0], SerializationBackend.Odin)
                    : PropertyTree.Create((IList)targets, SerializationBackend.Odin);
            }
        }

        private void ApplyPendingChanges()
        {
            if (propertyTree == null)
                return;
            try
            {
                propertyTree.ApplyChanges();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ResetAfterFailure()
        {
            ReleasePropertyTree();
            drawFailure = null;
            failureReported = false;
            context?.SetStatus("正在重新创建 Odin 页面", ESMenuTreePageStatus.Info);
            container?.MarkDirtyRepaint();
        }

        private void DisposeInternal(bool invokeLegacyDisable)
        {
            if (disposed)
                return;
            disposed = true;
            ReleaseView();

            if (!invokeLegacyDisable || legacyPage == null)
                return;
            legacyPage.OnPageDisable();
        }

        public override void ReleaseView()
        {
            ReleasePropertyTree();
            if (container != null)
                container.onGUIHandler = null;
            container = null;
            context = null;
            drawFailure = null;
            failureReported = false;
            legacyInitialized = false;
        }

        private void ReleasePropertyTree()
        {
            ApplyPendingChanges();
            if (propertyTree != null)
            {
                try
                {
                    propertyTree.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                propertyTree = null;
            }
            serializedObject = null;
        }

        private bool TargetsAreAlive()
        {
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] == null
                    || targets[i] is UnityEngine.Object unityTarget && unityTarget == null)
                    return false;
            return true;
        }

        private bool AllTargetsAreUnityObjects()
        {
            for (int i = 0; i < targets.Length; i++)
                if (!(targets[i] is UnityEngine.Object))
                    return false;
            return true;
        }

        private static object[] CopyAndValidateTargets(IList source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("Odin 页面至少需要一个有效目标。", nameof(source));

            var result = new object[source.Count];
            Type targetType = null;
            for (int i = 0; i < source.Count; i++)
            {
                object target = source[i];
                if (target == null || target is UnityEngine.Object unityTarget && unityTarget == null)
                    throw new ArgumentException("Odin 页面目标不能为 null。", nameof(source));
                targetType ??= target.GetType();
                if (target.GetType() != targetType)
                    throw new ArgumentException("Odin 多目标页面要求所有目标具有相同类型。", nameof(source));
                result[i] = target;
            }
            return result;
        }
    }

    public sealed class ESMenuTreeBuilder
    {
        internal sealed class Node
        {
            internal readonly string Name;
            internal readonly string Path;
            internal readonly List<Node> Children = new List<Node>();
            internal string StableId;
            internal string Keywords;
            internal string NavigationLabel;
            internal Texture Icon;
            internal ESMenuTreePage Page;
            internal ESMenuTreePageDefinition Definition;
            internal ESMenuTreePageContext Context;

            internal Node(string name, string path)
            {
                Name = name;
                Path = path;
            }
        }

        private readonly List<Node> roots = new List<Node>();
        private readonly Dictionary<string, Node> nodesByPath =
            new Dictionary<string, Node>(StringComparer.Ordinal);
        private readonly Dictionary<string, Node> pagesById =
            new Dictionary<string, Node>(StringComparer.Ordinal);

        public int PageCount => pagesById.Count;

        internal IReadOnlyList<Node> Roots => roots;
        internal IReadOnlyDictionary<string, Node> PagesById => pagesById;

        internal bool TryGetPageNode(string stableId, out Node node)
        {
            node = null;
            return !string.IsNullOrWhiteSpace(stableId)
                && pagesById.TryGetValue(stableId.Trim(), out node);
        }

        internal bool ContainsPagePath(string path, string exceptStableId = null)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath)
                || !nodesByPath.TryGetValue(normalizedPath, out Node node)
                || node.Page == null)
                return false;
            return string.IsNullOrEmpty(exceptStableId)
                || !string.Equals(node.StableId, exceptStableId, StringComparison.Ordinal);
        }

        public void Add(string stableId, string path, ESMenuTreePage page, Texture icon = null, string keywords = null)
        {
            Add(new ESMenuTreePageDefinition(stableId, path, page)
                .WithIcon(icon ?? ESMenuTreeUnityIconResolver.ResolveBrand(stableId, path))
                .WithKeywords(keywords));
        }

        public void Add(ESMenuTreePageDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            string stableId = definition.StableId;
            string path = definition.Path;
            if (pagesById.ContainsKey(stableId))
                throw new InvalidOperationException("页面 StableId 重复：" + stableId);
            Node reusedPage = pagesById.Values.FirstOrDefault(
                node => ReferenceEquals(node.Page, definition.Page));
            if (reusedPage != null)
                throw new InvalidOperationException(
                    "同一个页面实例不能注册到多个菜单节点：" + reusedPage.Path + " / " + path);

            string[] segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                throw new ArgumentException("页面路径不包含有效节点。", nameof(path));

            List<Node> siblings = roots;
            Node current = null;
            string currentPath = string.Empty;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (string.IsNullOrEmpty(segment))
                    continue;
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
                if (!nodesByPath.TryGetValue(currentPath, out current))
                {
                    current = new Node(segment, currentPath);
                    nodesByPath.Add(currentPath, current);
                    siblings.Add(current);
                }
                siblings = current.Children;
            }

            if (current == null)
                throw new ArgumentException("页面路径不包含有效节点。", nameof(path));
            if (current.Page != null)
                throw new InvalidOperationException("页面路径重复：" + current.Path);

            current.StableId = stableId;
            current.Keywords = definition.Keywords;
            current.NavigationLabel = definition.NavigationLabel;
            current.Icon = definition.Icon ?? ESMenuTreeUnityIconResolver.ResolveBrand(
                definition.StableId,
                definition.Path);
            current.Page = definition.Page;
            current.Definition = definition;
            pagesById.Add(stableId, current);
        }

        internal bool Remove(string stableId, out Node removed)
        {
            removed = null;
            if (string.IsNullOrWhiteSpace(stableId)
                || !pagesById.TryGetValue(stableId.Trim(), out Node node))
                return false;

            pagesById.Remove(node.StableId);
            removed = node;
            node.StableId = null;
            node.Keywords = null;
            node.NavigationLabel = null;
            node.Icon = null;
            node.Page = null;
            node.Definition = null;
            node.Context = null;
            PruneEmptyNodes(node);
            return true;
        }

        private void PruneEmptyNodes(Node node)
        {
            Node current = node;
            while (current != null && current.Page == null && current.Children.Count == 0)
            {
                nodesByPath.Remove(current.Path);
                int separator = current.Path.LastIndexOf('/');
                if (separator < 0)
                {
                    roots.Remove(current);
                    current = null;
                    continue;
                }

                string parentPath = current.Path.Substring(0, separator);
                if (!nodesByPath.TryGetValue(parentPath, out Node parent))
                    break;
                parent.Children.Remove(current);
                current = parent;
            }
        }

        internal static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            string[] segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var normalized = new List<string>(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!string.IsNullOrEmpty(segment))
                    normalized.Add(segment);
            }
            return string.Join("/", normalized);
        }

        public void AddOdinTarget(
            string stableId,
            string path,
            object target,
            Texture icon = null,
            string keywords = null)
        {
            Add(stableId, path, new ESOdinPropertyTreePage(target), icon, keywords);
        }

        public void AddPanel(
            string stableId,
            string path,
            Action<ESMenuTreePageContext, VisualElement> buildContent,
            Texture icon = null,
            string keywords = null,
            bool useVerticalScroll = true)
        {
            Add(ESMenuTreePageDefinition
                .ForPanel(stableId, path, buildContent, useVerticalScroll)
                .WithIcon(icon)
                .WithKeywords(keywords));
        }

        public void AddOdinTargets(
            string stableId,
            string path,
            IList targets,
            Texture icon = null,
            string keywords = null)
        {
            Add(stableId, path, new ESOdinPropertyTreePage(targets), icon, keywords);
        }

        [Obsolete("仅作为旧页面临时兼容入口；正式迁移请使用 ESMenuTreePageDefinition.ForOdin 并声明布局、图标与反馈。")]
        public void AddLegacyOdinPage<TPage>(
            string stableId,
            string path,
            ref TPage page,
            Texture icon = null,
            string keywords = null)
            where TPage : ESWindowPageBase, new()
        {
            page ??= new TPage();
            Add(stableId, path, new ESOdinPropertyTreePage(page), icon, keywords);
        }
    }

    /// <summary>
    /// ES 的 UI Toolkit 菜单树窗口宿主。菜单、搜索、内容区和状态栏均由 UI Toolkit
    /// 管理；新页面可直接返回 VisualElement，历史页面可通过独立 PropertyTree 渐进迁移。
    /// </summary>
    public abstract class ESMenuTreeWindow<This> : EditorWindow, IESWindowPageContextHost,
        IESWindowPresentationMetadata, IESWindowPresentationShortTitle,
        IESWindowPresentationTabLabel,
        ES.EditorInternal.IESWindowSleepRelationshipState
        where This : ESMenuTreeWindow<This>
    {
        private const float MenuPaneMinimumWidth = 148f;
        private const float MenuPaneMaximumWidth = 340f;
        private const float SearchMinimumWidth = 96f;

        private sealed class PageBadgeState
        {
            internal string Text;
            internal ESMenuTreePageStatus Status;
        }

        public static This UsingWindow;
        public event Action<string> ESWindow_SelectionChanged;

        public string ESWindow_SelectedPageId => selectedPageId ?? string.Empty;
        public ESMenuTreePage ESWindow_SelectedPage => activePage?.Page;
        public virtual string ESWindow_PresentationTitle =>
            activePage?.Path ?? ESWindow_GetWindowGUIContent()?.text ?? "ES 功能窗口";
        public virtual Texture ESWindow_PresentationIcon => activePage?.Definition?.Icon;
        public virtual string ESWindow_PresentationShortTitle =>
            ES.EditorInternal.ESEditorPresentation.BuildDefaultPresentationShortTitle(
                ESWindow_PresentationTitle);
        /// <summary>
        /// 页面窗口的半休眠标签。派生窗口只需覆写此保护属性即可配置短标签，
        /// 不必重复实现公开 Presentation 接口；为空时沿用现有短标题。
        /// </summary>
        protected virtual string ESWindow_SemiSleepLabel => string.Empty;
        public virtual string ESWindow_PresentationTabLabel =>
            string.IsNullOrWhiteSpace(ESWindow_SemiSleepLabel)
                ? ESWindow_PresentationShortTitle
                : ESWindow_SemiSleepLabel;
        public bool ESWindow_IsPinned
        {
            get => ES.EditorInternal.ESEditorPresentation.IsWindowPinned(this);
            set => ES.EditorInternal.ESEditorPresentation.SetWindowPinned(this, value);
        }
        public bool ESWindow_IsFocusMode
        {
            get => ES.EditorInternal.ESEditorPresentation.IsFocusMode(this);
            set => ES.EditorInternal.ESEditorPresentation.SetFocusMode(this, value);
        }

        public IDisposable ESWindow_BeginBusy(string message = null, string pageId = null)
        {
            return ES.EditorInternal.ESEditorPresentation.BeginWindowBusy(
                this,
                message,
                string.IsNullOrEmpty(pageId) ? ESWindow_SelectedPageId : pageId);
        }

        public void ESWindow_Notify(
            string message,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info,
            string pageId = null,
            string context = null,
            bool focus = true)
        {
            ES.EditorInternal.ESEditorPresentation.NotifyWindow(
                this,
                message,
                ToPresentationStatus(status),
                string.IsNullOrEmpty(pageId) ? ESWindow_SelectedPageId : pageId,
                context,
                focus);
        }

        [SerializeField] private string selectedPageId = string.Empty;
        [SerializeField] private string searchTerm = string.Empty;
        [SerializeField] private List<string> expandedPaths = new List<string>();
        [SerializeField] private bool expansionInitialized;
        [SerializeField] private float menuPaneWidth;

        private readonly List<ESMenuTreeBuilder.Node> registeredPages =
            new List<ESMenuTreeBuilder.Node>();
        private readonly List<ESMenuTreeBuilder.Node> rootNodes =
            new List<ESMenuTreeBuilder.Node>();
        private readonly Dictionary<string, ESMenuTreeBuilder.Node> pagesById =
            new Dictionary<string, ESMenuTreeBuilder.Node>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESMenuTreePageDefinition> runtimePageDefinitions =
            new Dictionary<string, ESMenuTreePageDefinition>(StringComparer.Ordinal);
        private readonly List<string> runtimePageOrder = new List<string>();
        private readonly Dictionary<string, Button> pageButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<ESMenuTreePage, VisualElement> pageViews =
            new Dictionary<ESMenuTreePage, VisualElement>();
        private readonly Dictionary<string, PageBadgeState> pageBadges =
            new Dictionary<string, PageBadgeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, Label> pageBadgeLabels =
            new Dictionary<string, Label>(StringComparer.Ordinal);
        private readonly List<string> navigationHistory = new List<string>();
        private readonly List<string> visiblePageIds = new List<string>();
        private readonly List<ESMenuTreeGlobalAction> globalActions =
            new List<ESMenuTreeGlobalAction>();
        private readonly List<ESMenuTreeGlobalAction> visibleGlobalActions =
            new List<ESMenuTreeGlobalAction>();
        private readonly List<ESMenuTreeGlobalAction> renderedGlobalActions =
            new List<ESMenuTreeGlobalAction>();
        private readonly Dictionary<string, Button> renderedGlobalActionButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly List<ESMenuTreePageAction> visiblePageActions =
            new List<ESMenuTreePageAction>();
        private readonly List<ESMenuTreePageAction> renderedPageActions =
            new List<ESMenuTreePageAction>();
        private readonly Dictionary<string, Button> renderedPageActionButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly HashSet<string> actionEvaluationFailures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ESMenuTreeBuilder.Node, bool> navigationMatchCache =
            new Dictionary<ESMenuTreeBuilder.Node, bool>();
        private readonly HashSet<string> expandedPathLookup =
            new HashSet<string>(StringComparer.Ordinal);

        private ES.EditorInternal.ESWindowShell shell;
        private ESMenuTreeBuilder menuBuilder;
        private ScrollView navigationScroll;
        private TwoPaneSplitView workspaceSplit;
        private TextField searchField;
        private VisualElement contentHost;
        private ESMenuTreeBuilder.Node activePage;
        private bool rebuildScheduled;
        private IVisualElementScheduledItem navigationRebuildSchedule;
        private Button navigateBackButton;
        private Button navigateForwardButton;
        private Button refreshPageButton;
        private Button settingsButton;
        private VisualElement actionToolbarStack;
        private VisualElement systemActionRow;
        private VisualElement systemActionToolbar;
        private VisualElement globalActionRow;
        private VisualElement globalActionToolbar;
        private VisualElement builtInGlobalActionToolbar;
        private VisualElement windowActionRow;
        private VisualElement windowActionToolbar;
        private VisualElement pageActionRow;
        private VisualElement pageActionToolbar;
        private ESWindowActionHosts actionHosts;
        private string searchCandidatePageId;
        private string renderedSearchTerm;
        private int globalActionCapacity;
        private int pageActionCapacity = 1;
        private int navigationHistoryIndex = -1;
        private bool navigatingHistory;
        private bool pageTransitionInProgress;
        private bool suppressSelectionFeedback;
        private string pendingSelectionId;
        private bool pendingSelectionReveal;
        private IVisualElementScheduledItem pendingSelectionSchedule;
        private readonly HashSet<string> pendingPageViewRebuilds = new HashSet<string>(StringComparer.Ordinal);
        private IVisualElementScheduledItem pendingPageViewRebuildSchedule;
        private IVisualElementScheduledItem openingActivationSchedule;
        private bool openingActivationScheduled;
        private string rebuildingPageId;
        private bool pagesDisposed;
        private int renderedGlobalActionCapacity = -1;
        private int renderedPageActionCapacity = -1;
        private ESMenuTreePageContext renderedPageActionContext;

        public virtual GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 菜单树窗口", "UI Toolkit 菜单树窗口");
        }

        protected virtual string ESWindow_Subtitle => "ES UI Toolkit 工作区";
        protected virtual Vector2 ESWindow_MinSize => new Vector2(720f, 520f);
        protected virtual Vector2 ESWindow_DefaultSize => new Vector2(1180f, 760f);
        protected virtual float ESWindow_MenuWidth => 240f;
        protected virtual bool ESWindow_ShowNavigation => true;
        /// <summary>
        /// 完整作者工作台已经拥有自己的品牌栏、命令栏和状态栏时，压缩外层 ES 壳，
        /// 只保留底座级系统动作，避免双重窗口框架挤占作者区域。
        /// </summary>
        protected virtual bool ESWindow_UseCompactHostChrome => false;
        /// <summary>
        /// 是否由 ES 窗口基类提供休眠生命周期与标准系统按钮。默认开启；
        /// 只有对话框、短生命周期弹窗等明确不适用的窗口才应覆写为 false。
        /// </summary>
        protected virtual bool ESWindow_SupportsSemiSleep => true;
        /// <summary>
        /// Whether a newly opened floating window may animate its native frame. Large authoring
        /// workbenches can disable this because Unity may create their UI panel before DockArea and
        /// HostView have finished binding the window.
        /// </summary>
        protected virtual bool ESWindow_AnimateOpeningFrame => true;
        /// <summary>窗口休眠归属。默认独立；依附型辅助窗口可声明跟随宿主。</summary>
        protected virtual ESWindowSleepLinkMode ESWindow_SleepLinkMode
            => ESWindowSleepLinkMode.Independent;
        protected virtual EditorWindow ESWindow_SleepOwner => null;
        protected virtual string ESWindow_SleepOwnerKey => null;
        [SerializeField] private string serializedSleepOwnerKey = string.Empty;
        [SerializeField] private bool serializedSleepOwnerDetachedByClose;
        [NonSerialized] private EditorWindow explicitSleepOwner;
        protected EditorWindow ESWindow_ExplicitSleepOwner => explicitSleepOwner;
        protected void ESWindow_SetSleepOwnerOverride(EditorWindow owner)
        {
            explicitSleepOwner = owner;
            serializedSleepOwnerDetachedByClose = false;
            if (!string.IsNullOrWhiteSpace(ESWindow_SleepOwnerKey))
                serializedSleepOwnerKey = ESWindow_SleepOwnerKey;
        }

        bool ES.EditorInternal.IESWindowSleepRelationshipState.SleepOwnerDetachedByClose
            => serializedSleepOwnerDetachedByClose;

        void ES.EditorInternal.IESWindowSleepRelationshipState.DetachSleepOwnerAfterOwnerClose()
        {
            explicitSleepOwner = null;
            serializedSleepOwnerDetachedByClose = true;
        }

        private string GetSleepOwnerKey()
        {
            return !string.IsNullOrWhiteSpace(ESWindow_SleepOwnerKey)
                ? ESWindow_SleepOwnerKey
                : serializedSleepOwnerKey;
        }
        /// <summary>
        /// 是否按“项目 + 窗口类型”记住最后一次成功打开的页面。只保存 StableId，
        /// 不保存页面实例、Unity 对象或业务数据。
        /// </summary>
        protected virtual bool ESWindow_RememberLastPage => true;
        protected virtual ESMenuTreeGroupClickBehavior ESWindow_GroupClickBehavior =>
            ESMenuTreeGroupClickBehavior.SelectFirstDescendant;

        protected abstract void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder);

        protected virtual void ESWindow_BuildGlobalActions(
            ICollection<ESMenuTreeGlobalAction> actions)
        {
        }

        /// <summary>
        /// 向基类已经创建的标准系统、全局或窗口动作域追加当前窗口自有控件。
        /// 窗口无需创建或挂载宿主，也不得重复创建休眠按钮；基础系统动作由基类负责。
        /// 页面上下文动作仍通过页面定义注册，以保留状态刷新和溢出菜单能力。
        /// </summary>
        protected virtual void ESWindow_BuildActionHosts(ESWindowActionHosts hosts)
        {
        }

        protected virtual void ESWindow_BuildToolbar(VisualElement toolbar)
        {
        }

        protected virtual void ESWindow_OnOpen()
        {
        }

        /// <summary>
        /// Host lifecycle hook for subscriptions and other lightweight, idempotent setup.
        /// It runs before CreateGUI and may run again after a domain reload.
        /// </summary>
        protected virtual void ESWindow_OnHostEnable()
        {
        }

        /// <summary>
        /// Host lifecycle hook for idempotent unsubscription and owned-resource cleanup.
        /// Base page disposal always continues even when this hook fails.
        /// </summary>
        protected virtual void ESWindow_OnHostDisable()
        {
        }

        public static This OpenWindow()
        {
            bool alreadyOpen = HasOpenInstances<This>();
            This window = GetWindow<This>();
            UsingWindow = window;
            window.titleContent = window.ESWindow_GetWindowGUIContent();
            window.ESWindow_OnOpen();
            window.Show();
            window.Focus();
            window.minSize = window.ESWindow_MinSize;
            if (!alreadyOpen && !window.docked)
            {
                window.maximized = false;
                window.PlaceInitialWindow();
            }
            return window;
        }

        public static This OpenWindow(EditorWindow sleepOwner)
        {
            This window = OpenWindow();
            window.ESWindow_SetSleepOwnerOverride(sleepOwner);
            if (sleepOwner != null && window.ESWindow_SleepLinkMode != ESWindowSleepLinkMode.Independent)
                ESWindowFoundation.SetSleepOwner(window, sleepOwner, window.ESWindow_SleepLinkMode);
            window.ForceMenuTreeRebuild();
            return window;
        }

        public static This OpenWindow(string stableId)
        {
            This window = OpenWindow();
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                window.rootVisualElement.schedule.Execute(() =>
                    window.ESWindow_TrySelectPage(stableId));
            }
            return window;
        }

        public static void ES_RefreshWindow()
        {
            if (UsingWindow == null)
                OpenWindow();
            UsingWindow?.ScheduleMenuRebuild();
        }

        private void OnEnable()
        {
            try
            {
                ESWindow_OnHostEnable();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void CreateGUI()
        {
            UsingWindow = this as This;
            titleContent = ESWindow_GetWindowGUIContent();
            minSize = ESWindow_MinSize;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            RebuildWindow();
            ScheduleOpeningActivation();
        }

        private void ScheduleOpeningActivation()
        {
            if (!ESWindow_AnimateOpeningFrame || openingActivationScheduled || rootVisualElement == null)
                return;
            openingActivationScheduled = true;
            openingActivationSchedule = rootVisualElement.schedule.Execute(() =>
            {
                openingActivationSchedule = null;
                if (this == null
                    || !ESWindow_AnimateOpeningFrame
                    || docked
                    || rootVisualElement.panel == null)
                    return;
                ES.EditorInternal.ESWindowFrameActivation.Play(this, position);
            }).StartingIn(16);
        }

        private void RebuildWindow()
        {
            rebuildScheduled = false;
            List<ESMenuTreePage> previousPages = registeredPages
                .Where(node => node?.Page != null)
                .Select(node => node.Page)
                .Distinct()
                .ToList();
            try
            {
                RebuildWindowCore(previousPages);
            }
            catch (Exception exception)
            {
                RecoverFromWindowBuildFailure(previousPages, exception);
            }
        }

        private void RebuildWindowCore(List<ESMenuTreePage> previousPages)
        {
            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = null;
            CancelPendingPageViewRebuilds();
            pagesDisposed = false;
            expandedPaths ??= new List<string>();
            expandedPathLookup.Clear();
            for (int i = 0; i < expandedPaths.Count; i++)
                if (!string.IsNullOrEmpty(expandedPaths[i]))
                    expandedPathLookup.Add(expandedPaths[i]);
            List<ESMenuTreePage> previousViewPages = pageViews.Keys.ToList();
            InvalidatePageContexts(registeredPages);
            HideActivePage();
            ReleasePageViewList(previousViewPages);

            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this);
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor =
                ES.EditorInternal.ESEditorPresentation.WindowInsetSurfaceColor;
            registeredPages.Clear();
            rootNodes.Clear();
            pagesById.Clear();
            menuBuilder = null;
            pageButtons.Clear();
            pageBadgeLabels.Clear();
            pageViews.Clear();

            GUIContent content = ESWindow_GetWindowGUIContent();
            Texture titleIcon = ESWindow_PresentationIcon
                ?? ES.EditorInternal.ESEditorPresentation.ResolveDefaultWindowIcon(
                    this,
                    content?.text,
                    null);
            shell = new ES.EditorInternal.ESWindowShell(
                content?.text,
                ESWindow_Subtitle,
                docked,
                titleIcon);
            if (ESWindow_UseCompactHostChrome)
                shell.ApplyCompactHostChrome();
            rootVisualElement.Add(shell.Root);
            globalActions.Clear();
            actionEvaluationFailures.Clear();
            ESWindow_BuildGlobalActions(globalActions);
            ValidateGlobalActions();
            BuildToolbarContract(shell.HeaderToolbar);
            shell.Header.RegisterCallback<GeometryChangedEvent>(OnHeaderGeometryChanged);

            searchField = null;
            if (ESWindow_ShowNavigation)
            {
                searchField = new TextField("搜索") { value = searchTerm ?? string.Empty };
                searchField.tooltip = "按页面名称、路径或关键词筛选。";
                searchField.style.flexGrow = 1f;
                searchField.style.flexShrink = 1f;
                searchField.style.minWidth = SearchMinimumWidth;
                searchField.style.maxWidth = 320f;
                searchField.RegisterValueChangedCallback(evt =>
                {
                    searchTerm = evt.newValue ?? string.Empty;
                    ScheduleNavigationRebuild();
                });
                searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
                shell.Toolbar.Add(searchField);
            }
            shell.Toolbar.Add(ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                ESWindow_ShowNavigation ? "刷新" : "重建页面",
                ESWindow_ShowNavigation
                    ? "重新生成菜单并刷新当前页面。"
                    : "重新创建单页内容并释放旧页面状态。",
                ScheduleMenuRebuild));
            ESWindow_BuildToolbar(shell.Toolbar);

            contentHost = new VisualElement { name = "ESMenuTreeContentHost" };
            contentHost.style.flexGrow = 1f;
            contentHost.style.flexShrink = 1f;
            contentHost.style.flexBasis = 0f;
            contentHost.style.minWidth = 0f;
            contentHost.style.minHeight = 0f;
            contentHost.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WindowSurfaceColor;
            workspaceSplit = null;
            navigationScroll = null;
            if (ESWindow_ShowNavigation)
            {
                float initialMenuWidth = menuPaneWidth > 0f
                    ? Mathf.Clamp(menuPaneWidth, MenuPaneMinimumWidth, MenuPaneMaximumWidth)
                    : Mathf.Clamp(ESWindow_MenuWidth, MenuPaneMinimumWidth, MenuPaneMaximumWidth);
                workspaceSplit = new TwoPaneSplitView(
                    0,
                    initialMenuWidth,
                    TwoPaneSplitViewOrientation.Horizontal)
                {
                    name = "ESMenuTreeWorkspace"
                };
                workspaceSplit.style.flexGrow = 1f;
                workspaceSplit.style.flexShrink = 1f;
                workspaceSplit.style.flexBasis = 0f;
                workspaceSplit.style.minWidth = 0f;
                workspaceSplit.style.minHeight = 0f;

                VisualElement navigation = new VisualElement { name = "ESMenuTreeNavigation" };
                navigation.style.minWidth = MenuPaneMinimumWidth;
                navigation.style.maxWidth = MenuPaneMaximumWidth;
                navigation.style.flexGrow = 1f;
                navigation.style.backgroundColor =
                    ES.EditorInternal.ESEditorPresentation.WindowInsetSurfaceColor;
                navigation.style.borderRightWidth = 1f;
                navigation.style.borderRightColor =
                    ES.EditorInternal.ESEditorPresentation.DividerColor;
                navigation.RegisterCallback<GeometryChangedEvent>(OnNavigationGeometryChanged);
                navigationScroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    name = "ESMenuTreeNavigationScroll",
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.Auto
                };
                navigationScroll.style.flexGrow = 1f;
                navigationScroll.style.flexShrink = 1f;
                navigationScroll.style.minWidth = 0f;
                navigationScroll.style.minHeight = 0f;
                navigationScroll.style.paddingTop = 6f;
                navigationScroll.style.paddingBottom = 8f;
                navigation.Add(navigationScroll);
                workspaceSplit.Add(navigation);
                workspaceSplit.Add(contentHost);
                shell.Content.Add(workspaceSplit);
            }
            else
            {
                shell.Content.Add(contentHost);
            }

            var builder = new ESMenuTreeBuilder();
            try
            {
                ESWindow_BuildMenuTree(builder);
                AddRuntimeDefinitions(builder);
                menuBuilder = builder;
                rootNodes.AddRange(builder.Roots);
                if (!expansionInitialized)
                {
                    for (int i = 0; i < rootNodes.Count; i++)
                        SetExpanded(rootNodes[i].Path, true);
                    expansionInitialized = true;
                }
                foreach (ESMenuTreeBuilder.Node node in builder.PagesById.Values)
                {
                    registeredPages.Add(node);
                    pagesById.Add(node.StableId, node);
                    node.Context = CreatePageContext(node);
                }
                RestoreRememberedPageSelection();
                RemoveInvalidPageBadges();
                DisposeRemovedPages(previousPages);
                if (ESWindow_ShowNavigation)
                {
                    RebuildNavigation();
                }
                else
                {
                    ESMenuTreeBuilder.Node singlePage = registeredPages.Count > 0
                        ? registeredPages[0]
                        : null;
                    if (singlePage != null)
                    {
                        selectedPageId = singlePage.StableId;
                        SelectPage(singlePage.StableId);
                    }
                }
            }
            catch (Exception exception)
            {
                InvalidatePageContexts(registeredPages);
                DisposeFailedBuildPages(builder, previousPages);
                ClearRuntimePageRegistrations();
                registeredPages.Clear();
                rootNodes.Clear();
                pagesById.Clear();
                menuBuilder = null;
                pageButtons.Clear();
                pageBadgeLabels.Clear();
                pageViews.Clear();
                Debug.LogException(exception);
                contentHost.Clear();
                contentHost.Add(ES.EditorInternal.ESWindowPresentation.CreateErrorState(
                    "菜单构建失败",
                    exception.Message,
                    "当前窗口没有可用菜单页面。",
                    "检查页面注册和数据依赖后重新刷新。",
                    "重试",
                    ScheduleMenuRebuild));
                SetStatus("菜单构建失败：" + exception.Message, ESMenuTreePageStatus.Error);
            }

            ESWindowFoundation.Bind(
                this,
                actionHosts,
                allowSemiSleep: ESWindow_SupportsSemiSleep);
            UpdateCompactToolbarScopeVisibility();
            if (!serializedSleepOwnerDetachedByClose
                && ESWindow_SleepLinkMode != ESWindowSleepLinkMode.Independent)
            {
                EditorWindow owner = ESWindow_ExplicitSleepOwner ?? ESWindow_SleepOwner;
                if (owner != null)
                    ESWindowFoundation.SetSleepOwner(this, owner, ESWindow_SleepLinkMode);
                else if (ESWindow_SleepLinkMode == ESWindowSleepLinkMode.FollowOwner)
                {
                    string ownerKey = GetSleepOwnerKey();
                    if (!ESWindowFoundation.RegisterPendingSleepOwner(
                        this,
                        ownerKey,
                        ESWindow_SleepLinkMode))
                        Debug.LogError("ES FollowOwner 窗口必须声明稳定 ESWindow_SleepOwnerKey。窗口：" + GetType().FullName);
                }
            }
        }

        private void RecoverFromWindowBuildFailure(
            List<ESMenuTreePage> previousPages,
            Exception exception)
        {
            Debug.LogException(exception);
            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = null;
            pendingSelectionSchedule?.Pause();
            pendingSelectionSchedule = null;
            pendingSelectionId = null;
            pendingSelectionReveal = false;
            CancelPendingPageViewRebuilds();
            InvalidatePageContexts(registeredPages);
            ReleasePageViewList(pageViews.Keys.ToList());

            var pagesToDispose = new List<ESMenuTreePage>();
            if (previousPages != null)
                pagesToDispose.AddRange(previousPages);
            pagesToDispose.AddRange(registeredPages
                .Where(node => node?.Page != null)
                .Select(node => node.Page));
            DisposePageList(pagesToDispose.Where(page => page != null).Distinct().ToList());
            ClearRuntimePageRegistrations();

            activePage = null;
            shell = null;
            contentHost = null;
            searchField = null;
            workspaceSplit = null;
            navigationScroll = null;
            registeredPages.Clear();
            rootNodes.Clear();
            pagesById.Clear();
            menuBuilder = null;
            pageButtons.Clear();
            pageBadgeLabels.Clear();
            pageViews.Clear();
            globalActions.Clear();
            ClearRenderedGlobalActions();
            ClearRenderedPageActions();
            actionToolbarStack = null;
            systemActionRow = null;
            systemActionToolbar = null;
            globalActionRow = null;
            globalActionToolbar = null;
            builtInGlobalActionToolbar = null;
            windowActionRow = null;
            windowActionToolbar = null;
            pageActionRow = null;
            pageActionToolbar = null;
            actionHosts = null;

            try
            {
                ES.EditorInternal.ESEditorPresentation.UnbindWindow(this);
            }
            catch (Exception unbindFailure)
            {
                Debug.LogException(unbindFailure);
            }

            rootVisualElement.Clear();
            var failureRoot = new VisualElement { name = "ESMenuTreeWindowBuildFailure" };
            failureRoot.style.flexGrow = 1f;
            failureRoot.style.justifyContent = Justify.Center;
            failureRoot.style.alignItems = Align.Center;
            failureRoot.style.paddingLeft = 24f;
            failureRoot.style.paddingRight = 24f;
            failureRoot.style.paddingTop = 24f;
            failureRoot.style.paddingBottom = 24f;
            Label title = new Label("窗口构建失败");
            title.style.fontSize = 18f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            failureRoot.Add(title);
            Label detail = new Label(
                "原因：" + exception.Message
                + "\n影响：旧页面已经安全释放，本窗口暂时不可用。"
                + "\n恢复：修复窗口扩展点或页面注册后重新构建。");
            detail.style.maxWidth = 720f;
            detail.style.marginTop = 10f;
            detail.style.whiteSpace = WhiteSpace.Normal;
            failureRoot.Add(detail);
            Button retry = new Button(ScheduleMenuRebuild) { text = "重试构建" };
            retry.style.marginTop = 16f;
            retry.style.minWidth = 120f;
            retry.style.height = 30f;
            failureRoot.Add(retry);
            rootVisualElement.Add(failureRoot);
        }

        private void RebuildNavigation()
        {
            if (navigationScroll == null)
                return;
            navigationScroll.Clear();
            pageButtons.Clear();
            pageBadgeLabels.Clear();
            visiblePageIds.Clear();

            string query = (searchTerm ?? string.Empty).Trim();
            bool searchChanged = !string.Equals(renderedSearchTerm, query, StringComparison.Ordinal);
            renderedSearchTerm = query;
            navigationMatchCache.Clear();
            if (!string.IsNullOrEmpty(query))
                for (int i = 0; i < rootNodes.Count; i++)
                    CacheNodeMatches(rootNodes[i], query);
            ESMenuTreeBuilder.Node firstVisiblePage = null;
            for (int i = 0; i < rootNodes.Count; i++)
                RenderNode(rootNodes[i], navigationScroll, 0, query, ref firstVisiblePage);

            if (firstVisiblePage == null)
            {
                searchCandidatePageId = null;
                navigationScroll.Add(ES.EditorInternal.ESWindowPresentation.CreateEmptyState(
                    "没有匹配页面", "请调整搜索关键词。", null, null));
                if (activePage == null
                    && !string.IsNullOrEmpty(selectedPageId)
                    && pagesById.ContainsKey(selectedPageId))
                    SelectPage(selectedPageId);
                if (activePage == null)
                    ShowContentEmptyState("没有匹配页面", "左侧筛选结果为空，请修改搜索词。", null, null);
                SetStatus(
                    activePage == null ? "没有匹配页面" : "没有匹配页面；当前页面保持打开",
                    ESMenuTreePageStatus.Info);
                return;
            }

            ESMenuTreeBuilder.Node selected = null;
            bool selectedExists = !string.IsNullOrEmpty(selectedPageId)
                && pagesById.TryGetValue(selectedPageId, out selected)
                && selected.Page != null;
            if (!selectedExists)
            {
                selectedPageId = firstVisiblePage.StableId;
                selected = firstVisiblePage;
            }

            if (!string.IsNullOrEmpty(query))
            {
                searchCandidatePageId = selectedExists && NodeMatchesCached(selected, query)
                    ? selectedPageId
                    : firstVisiblePage.StableId;
                if (activePage == null)
                    SelectPage(selectedPageId);
                RefreshSelectionStyles();
                ScrollSelectedPageIntoView();
                SetStatus(
                    "筛选到 " + visiblePageIds.Count + " 个页面；Enter 打开："
                    + pagesById[searchCandidatePageId].Path,
                    ESMenuTreePageStatus.Info);
                return;
            }

            searchCandidatePageId = null;
            SelectPage(selectedPageId);
            ScrollSelectedPageIntoView();
            if (searchChanged && activePage != null)
                SetStatus("当前页面：" + activePage.Path, ESMenuTreePageStatus.Ready);
        }

        private bool RenderNode(
            ESMenuTreeBuilder.Node node,
            VisualElement parent,
            int depth,
            string query,
            ref ESMenuTreeBuilder.Node firstVisiblePage)
        {
            if (node == null || !NodeMatchesCached(node, query))
                return false;

            bool ownMatches = string.IsNullOrEmpty(query) || NodeSelfMatches(node, query);
            string childQuery = ownMatches ? string.Empty : query;
            bool hasVisibleChildren = false;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (!NodeMatchesCached(node.Children[i], childQuery))
                    continue;
                hasVisibleChildren = true;
                break;
            }
            bool expanded = !string.IsNullOrEmpty(query) || expandedPathLookup.Contains(node.Path);
            VisualElement nodeRoot = new VisualElement { name = "ESMenuTreeNode" };
            nodeRoot.style.flexShrink = 0f;

            VisualElement row = new VisualElement { name = "ESMenuTreeRow" };
            row.style.height = 30f;
            row.style.minHeight = 30f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginLeft = 6f + depth * 14f;
            row.style.marginRight = 6f;
            row.style.marginBottom = 2f;

            Button toggle = new Button { text = hasVisibleChildren ? (expanded ? "▼" : "▶") : string.Empty };
            toggle.tooltip = hasVisibleChildren ? (expanded ? "折叠" : "展开") : string.Empty;
            toggle.SetEnabled(hasVisibleChildren);
            toggle.style.width = 24f;
            toggle.style.minWidth = 24f;
            toggle.style.height = 26f;
            toggle.style.paddingLeft = 0f;
            toggle.style.paddingRight = 0f;
            toggle.style.borderLeftWidth = 0f;
            toggle.style.borderRightWidth = 0f;
            toggle.style.borderTopWidth = 0f;
            toggle.style.borderBottomWidth = 0f;
            toggle.style.backgroundColor = Color.clear;
            if (hasVisibleChildren)
            {
                toggle.clicked += () =>
                {
                    SetExpanded(node.Path, !expandedPathLookup.Contains(node.Path));
                    RebuildNavigation();
                };
            }
            row.Add(toggle);

            bool runtimeInjected = node.Definition?.IsRuntimePage == true;
            string pageTooltip = runtimeInjected
                ? node.Path + "\n运行时所有者：" + node.Definition.RuntimeOwnerId
                : node.Path;
            string navigationLabel = string.IsNullOrWhiteSpace(node.NavigationLabel)
                ? ES.EditorInternal.ESEditorPresentation.BuildDefaultPresentationShortTitle(node.Name)
                : node.NavigationLabel;
            Button pageButton = new Button
            {
                text = runtimeInjected ? navigationLabel + "  · 临时" : navigationLabel,
                tooltip = pageTooltip
            };
            pageButton.style.flexGrow = 1f;
            pageButton.style.flexShrink = 1f;
            pageButton.style.minWidth = 0f;
            pageButton.style.height = 28f;
            pageButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            pageButton.style.paddingLeft = node.Icon != null ? 30f : 8f;
            pageButton.style.paddingRight = 8f;
            pageButton.style.whiteSpace = WhiteSpace.NoWrap;
            pageButton.style.overflow = Overflow.Hidden;
            pageButton.style.textOverflow = TextOverflow.Ellipsis;
            pageButton.style.borderLeftWidth = 0f;
            pageButton.style.borderRightWidth = 0f;
            pageButton.style.borderTopWidth = 0f;
            pageButton.style.borderBottomWidth = 0f;
            pageButton.style.backgroundColor = Color.clear;
            pageButton.style.color = ES.EditorInternal.ESEditorPresentation.SectionTextColor;
            if (node.Page != null)
            {
                string stableId = node.StableId;
                pageButton.clicked += () => SelectPage(stableId);
                pageButtons[stableId] = pageButton;
                visiblePageIds.Add(stableId);
                if (firstVisiblePage == null)
                    firstVisiblePage = node;
            }
            else if (hasVisibleChildren)
            {
                pageButton.clicked += () =>
                {
                    if (ESWindow_GroupClickBehavior == ESMenuTreeGroupClickBehavior.SelectFirstDescendant)
                    {
                        ESMenuTreeBuilder.Node firstChild = FindFirstSelectableDescendant(node, childQuery);
                        if (firstChild?.Page != null)
                        {
                            SetExpanded(node.Path, true);
                            ESWindow_TrySelectPage(firstChild.StableId, true);
                            return;
                        }
                    }
                    SetExpanded(node.Path, !expandedPathLookup.Contains(node.Path));
                    RebuildNavigation();
                };
            }
            else
            {
                pageButton.SetEnabled(false);
            }

            if (node.Icon != null)
            {
                Image icon = new Image
                {
                    image = node.Icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                    tintColor = ES.EditorInternal.ESEditorPresentation.SectionTextColor
                };
                icon.style.position = Position.Absolute;
                icon.style.left = 6f;
                icon.style.top = 5f;
                icon.style.width = 18f;
                icon.style.height = 18f;
                pageButton.Add(icon);
            }
            if (node.Page != null)
            {
                Label badge = CreatePageBadgeLabel();
                pageButton.Add(badge);
                pageBadgeLabels[node.StableId] = badge;
                ApplyPageBadgeVisual(node.StableId);
            }
            row.Add(pageButton);
            nodeRoot.Add(row);

            if (hasVisibleChildren && expanded)
            {
                VisualElement children = new VisualElement { name = "ESMenuTreeChildren" };
                for (int i = 0; i < node.Children.Count; i++)
                    RenderNode(node.Children[i], children, depth + 1, childQuery, ref firstVisiblePage);
                nodeRoot.Add(children);
            }

            parent.Add(nodeRoot);
            return true;
        }

        private ESMenuTreeBuilder.Node FindFirstSelectableDescendant(
            ESMenuTreeBuilder.Node node,
            string query)
        {
            if (node == null)
                return null;
            for (int i = 0; i < node.Children.Count; i++)
            {
                ESMenuTreeBuilder.Node child = node.Children[i];
                if (!NodeMatchesCached(child, query))
                    continue;
                if (child.Page != null)
                    return child;
                ESMenuTreeBuilder.Node nested = FindFirstSelectableDescendant(child, query);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static Label CreatePageBadgeLabel()
        {
            var badge = new Label
            {
                name = "ESMenuTreePageBadge",
                pickingMode = PickingMode.Ignore
            };
            badge.style.position = Position.Absolute;
            badge.style.right = 6f;
            badge.style.top = 5f;
            badge.style.height = 18f;
            badge.style.minWidth = 18f;
            badge.style.maxWidth = 54f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.fontSize = 10f;
            badge.style.whiteSpace = WhiteSpace.NoWrap;
            badge.style.overflow = Overflow.Hidden;
            badge.style.textOverflow = TextOverflow.Ellipsis;
            badge.style.borderLeftWidth = 1f;
            badge.style.borderRightWidth = 1f;
            badge.style.borderTopWidth = 1f;
            badge.style.borderBottomWidth = 1f;
            ES.EditorInternal.ESEditorPresentation.ApplyCornerRadius(
                badge, ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Pill);
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        private void ApplyPageBadgeVisual(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)
                || !pageBadgeLabels.TryGetValue(stableId, out Label badge))
                return;
            bool hasBadge = pageBadges.TryGetValue(stableId, out PageBadgeState state)
                && !string.IsNullOrWhiteSpace(state.Text);
            badge.style.display = hasBadge ? DisplayStyle.Flex : DisplayStyle.None;
            if (pageButtons.TryGetValue(stableId, out Button button))
                button.style.paddingRight = hasBadge ? 64f : 8f;
            if (!hasBadge)
                return;

            badge.text = state.Text.Trim();
            badge.tooltip = state.Text.Trim();
            Color accent = ES.EditorInternal.ESEditorPresentation.GetStatusAccent(
                0, ToPresentationStatus(state.Status));
            Color surface = accent;
            surface.a = EditorGUIUtility.isProSkin ? 0.18f : 0.12f;
            badge.style.color = accent;
            badge.style.backgroundColor = surface;
            badge.style.borderLeftColor = accent;
            badge.style.borderRightColor = accent;
            badge.style.borderTopColor = accent;
            badge.style.borderBottomColor = accent;
        }

        public bool ESWindow_SetPageBadge(
            string stableId,
            string text,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            string normalizedId = stableId.Trim();
            if (!pagesById.ContainsKey(normalizedId))
                return false;
            if (string.IsNullOrWhiteSpace(text))
                pageBadges.Remove(normalizedId);
            else
                pageBadges[normalizedId] = new PageBadgeState
                {
                    Text = text.Trim(),
                    Status = status
                };
            ApplyPageBadgeVisual(normalizedId);
            return true;
        }

        public bool ESWindow_ClearPageBadge(string stableId)
        {
            return ESWindow_SetPageBadge(stableId, null);
        }

        /// <summary>注册一个由 ownerId 独占管理的运行时页面，并局部刷新菜单。</summary>
        public ESMenuTreeMutationResult AddRuntimePage(
            string ownerId,
            ESMenuTreePageDefinition definition,
            bool selectAfterAdd = false)
        {
            return TryAddRuntimePage(ownerId, definition, selectAfterAdd, out string error)
                ? ESMenuTreeMutationResult.Success()
                : ESMenuTreeMutationResult.Failure(error);
        }

        private bool TryAddRuntimePage(
            string ownerId,
            ESMenuTreePageDefinition definition,
            bool selectAfterAdd,
            out string failure)
        {
            failure = null;
            if (!TryValidateRuntimeDefinition(
                    ownerId,
                    definition,
                    null,
                    out string normalizedOwnerId,
                    out failure))
                return false;
            if (runtimePageDefinitions.ContainsKey(definition.StableId)
                || pagesById.ContainsKey(definition.StableId))
            {
                failure = "页面 StableId 已存在：" + definition.StableId;
                return false;
            }

            string previousOwnerId = definition.RuntimeOwnerId;
            definition.SetRuntimeOwnerId(normalizedOwnerId);
            runtimePageDefinitions.Add(definition.StableId, definition);
            runtimePageOrder.Add(definition.StableId);
            try
            {
                if (menuBuilder != null)
                {
                    menuBuilder.Add(definition);
                    AttachRuntimePageNode(definition.StableId);
                    RefreshRuntimeMenu(selectAfterAdd ? definition.StableId : null);
                }
            }
            catch (Exception exception)
            {
                runtimePageDefinitions.Remove(definition.StableId);
                runtimePageOrder.Remove(definition.StableId);
                definition.SetRuntimeOwnerId(previousOwnerId);
                if (menuBuilder != null)
                    menuBuilder.Remove(definition.StableId, out _);
                failure = "运行时页面注册失败：" + exception.Message;
                return false;
            }

            SetStatus("已注册运行时页面：" + definition.Path, ESMenuTreePageStatus.Ready);
            return true;
        }

        /// <summary>按 replacement.StableId 替换 ownerId 持有的运行时页面。</summary>
        public ESMenuTreeMutationResult UpdateRuntimePage(
            string ownerId,
            ESMenuTreePageDefinition replacement)
        {
            if (replacement == null)
                return ESMenuTreeMutationResult.Failure("更新后的页面定义不能为 null。");
            string stableId = replacement.StableId;
            return TryUpdateRuntimePage(stableId, ownerId, replacement, out string error)
                ? ESMenuTreeMutationResult.Success()
                : ESMenuTreeMutationResult.Failure(error);
        }

        private bool TryUpdateRuntimePage(
            string stableId,
            string ownerId,
            ESMenuTreePageDefinition replacement,
            out string failure)
        {
            failure = null;
            string normalizedId = stableId?.Trim();
            if (string.IsNullOrEmpty(normalizedId)
                || !runtimePageDefinitions.TryGetValue(normalizedId, out ESMenuTreePageDefinition previous))
            {
                failure = "找不到临时页面：" + (normalizedId ?? string.Empty);
                return false;
            }
            if (!OwnerMatches(previous, ownerId, out string normalizedOwnerId, out failure))
                return false;
            if (replacement == null)
            {
                failure = "更新后的页面定义不能为 null。";
                return false;
            }
            if (!string.Equals(replacement.StableId, normalizedId, StringComparison.Ordinal))
            {
                failure = "更新临时页面时不能改变 StableId：" + normalizedId;
                return false;
            }
            if (!TryValidateRuntimeDefinition(
                    normalizedOwnerId,
                    replacement,
                    normalizedId,
                    out _,
                    out failure))
                return false;

            bool wasActive = false;
            ESMenuTreeBuilder.Node previousNode = null;
            if (menuBuilder != null && menuBuilder.TryGetPageNode(normalizedId, out previousNode))
            {
                wasActive = ReferenceEquals(activePage, previousNode);
                if (wasActive
                    && !TryResolvePendingChanges(
                        previousNode.Page,
                        ESMenuTreePageLeaveReason.ReplaceRuntimePage))
                {
                    failure = "当前临时页面拒绝离开，更新已取消。";
                    return false;
                }
            }

            string replacementOwnerId = replacement.RuntimeOwnerId;
            replacement.SetRuntimeOwnerId(normalizedOwnerId);
            if (menuBuilder == null || previousNode == null)
            {
                runtimePageDefinitions[normalizedId] = replacement;
                if (!ReferenceEquals(previous.Page, replacement.Page))
                    DisposePage(previous.Page);
                return true;
            }

            DetachRuntimePageNode(previousNode);
            menuBuilder.Remove(normalizedId, out _);
            try
            {
                menuBuilder.Add(replacement);
                runtimePageDefinitions[normalizedId] = replacement;
                AttachRuntimePageNode(normalizedId);
            }
            catch (Exception exception)
            {
                replacement.SetRuntimeOwnerId(replacementOwnerId);
                runtimePageDefinitions[normalizedId] = previous;
                menuBuilder.Remove(normalizedId, out _);
                menuBuilder.Add(previous);
                AttachRuntimePageNode(normalizedId);
                RefreshRuntimeMenu(wasActive ? normalizedId : null);
                failure = "临时页面更新失败，旧页面已恢复：" + exception.Message;
                return false;
            }

            if (!ReferenceEquals(previous.Page, replacement.Page))
                DisposePage(previous.Page);
            RefreshRuntimeMenu(wasActive ? normalizedId : null);
            SetStatus("已更新临时页面：" + replacement.Path, ESMenuTreePageStatus.Ready);
            return true;
        }

        /// <summary>移除 ownerId 持有的运行时页面，并释放页面视图与生命周期资源。</summary>
        public ESMenuTreeMutationResult RemoveRuntimePage(string ownerId, string stableId)
        {
            return TryRemoveRuntimePage(stableId, ownerId, out string error)
                ? ESMenuTreeMutationResult.Success()
                : ESMenuTreeMutationResult.Failure(error);
        }

        private bool TryRemoveRuntimePage(
            string stableId,
            string ownerId,
            out string failure)
        {
            failure = null;
            string normalizedId = stableId?.Trim();
            if (string.IsNullOrEmpty(normalizedId)
                || !runtimePageDefinitions.TryGetValue(normalizedId, out ESMenuTreePageDefinition definition))
            {
                failure = "找不到临时页面：" + (normalizedId ?? string.Empty);
                return false;
            }
            if (!OwnerMatches(definition, ownerId, out _, out failure))
                return false;

            ESMenuTreeBuilder.Node node = null;
            bool wasActive = menuBuilder != null
                && menuBuilder.TryGetPageNode(normalizedId, out node)
                && ReferenceEquals(activePage, node);
            if (wasActive
                && !TryResolvePendingChanges(
                    node.Page,
                    ESMenuTreePageLeaveReason.RemoveRuntimePage))
            {
                failure = "当前临时页面拒绝离开，移除已取消。";
                return false;
            }

            if (node != null)
            {
                DetachRuntimePageNode(node);
                menuBuilder.Remove(normalizedId, out _);
            }
            runtimePageDefinitions.Remove(normalizedId);
            runtimePageOrder.Remove(normalizedId);
            DisposePage(definition.Page);
            if (menuBuilder != null)
                RefreshRuntimeMenu(null);
            SetStatus("已移除临时页面：" + definition.Path, ESMenuTreePageStatus.Ready);
            return true;
        }

        public bool TryGetPageDefinition(
            string stableId,
            out ESMenuTreePageDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            string normalizedId = stableId.Trim();
            if (pagesById.TryGetValue(normalizedId, out ESMenuTreeBuilder.Node node))
            {
                definition = node.Definition;
                return definition != null;
            }
            return runtimePageDefinitions.TryGetValue(normalizedId, out definition);
        }

        public bool TryGetRuntimePageOwner(string stableId, out string ownerId)
        {
            ownerId = null;
            if (!TryGetPageDefinition(stableId, out ESMenuTreePageDefinition definition)
                || !definition.IsRuntimePage)
                return false;
            ownerId = definition.RuntimeOwnerId;
            return true;
        }

        public IReadOnlyList<ESMenuTreePageDefinition> GetPageDefinitions()
        {
            if (registeredPages.Count > 0)
                return registeredPages
                    .Where(node => node?.Definition != null)
                    .Select(node => node.Definition)
                    .ToArray();
            return runtimePageOrder
                .Where(runtimePageDefinitions.ContainsKey)
                .Select(stableId => runtimePageDefinitions[stableId])
                .ToArray();
        }

        private bool TryValidateRuntimeDefinition(
            string ownerId,
            ESMenuTreePageDefinition definition,
            string exceptStableId,
            out string normalizedOwnerId,
            out string failure)
        {
            normalizedOwnerId = ownerId?.Trim();
            failure = null;
            if (!ESWindow_ShowNavigation)
            {
                failure = "单页面板不接受运行时菜单注册。";
                return false;
            }
            if (string.IsNullOrEmpty(normalizedOwnerId) || normalizedOwnerId.Length > 96)
            {
                failure = "运行时页面 ownerId 不能为空且长度不能超过 96。";
                return false;
            }
            if (definition == null)
            {
                failure = "临时页面定义不能为 null。";
                return false;
            }
            if (!string.IsNullOrEmpty(definition.RuntimeOwnerId)
                && !string.Equals(definition.RuntimeOwnerId, normalizedOwnerId, StringComparison.Ordinal))
            {
                failure = "页面定义已归属其他 ownerId：" + definition.RuntimeOwnerId;
                return false;
            }
            if (menuBuilder != null
                && menuBuilder.ContainsPagePath(definition.Path, exceptStableId))
            {
                failure = "菜单路径已被其他页面占用：" + definition.Path;
                return false;
            }

            string normalizedPath = ESMenuTreeBuilder.NormalizePath(definition.Path);
            foreach (KeyValuePair<string, ESMenuTreePageDefinition> pair in runtimePageDefinitions)
            {
                if (string.Equals(pair.Key, exceptStableId, StringComparison.Ordinal))
                    continue;
                ESMenuTreePageDefinition current = pair.Value;
                if (string.Equals(
                    ESMenuTreeBuilder.NormalizePath(current.Path),
                    normalizedPath,
                    StringComparison.Ordinal))
                {
                    failure = "菜单路径已被临时页面占用：" + definition.Path;
                    return false;
                }
                if (ReferenceEquals(current.Page, definition.Page))
                {
                    failure = "同一个页面实例不能由多个临时菜单项持有。";
                    return false;
                }
            }
            foreach (ESMenuTreeBuilder.Node node in pagesById.Values)
            {
                if (string.Equals(node.StableId, exceptStableId, StringComparison.Ordinal))
                    continue;
                if (ReferenceEquals(node.Page, definition.Page))
                {
                    failure = "同一个页面实例不能注册到多个菜单项。";
                    return false;
                }
            }
            return true;
        }

        private static bool OwnerMatches(
            ESMenuTreePageDefinition definition,
            string ownerId,
            out string normalizedOwnerId,
            out string failure)
        {
            normalizedOwnerId = ownerId?.Trim();
            failure = null;
            if (string.IsNullOrEmpty(normalizedOwnerId)
                || !string.Equals(definition.RuntimeOwnerId, normalizedOwnerId, StringComparison.Ordinal))
            {
                failure = "ownerId 不匹配，拒绝修改运行时页面。";
                return false;
            }
            return true;
        }

        private void AddRuntimeDefinitions(ESMenuTreeBuilder builder)
        {
            for (int i = 0; i < runtimePageOrder.Count; i++)
            {
                string stableId = runtimePageOrder[i];
                if (runtimePageDefinitions.TryGetValue(stableId, out ESMenuTreePageDefinition definition))
                    builder.Add(definition);
            }
        }

        private void AttachRuntimePageNode(string stableId)
        {
            if (menuBuilder == null
                || !menuBuilder.TryGetPageNode(stableId, out ESMenuTreeBuilder.Node node))
                throw new InvalidOperationException("找不到刚注册的运行时菜单节点：" + stableId);
            if (pagesById.ContainsKey(stableId))
                throw new InvalidOperationException("窗口页面注册表已包含 StableId：" + stableId);
            node.Context = CreatePageContext(node);
            registeredPages.Add(node);
            pagesById.Add(stableId, node);
        }

        private void DetachRuntimePageNode(ESMenuTreeBuilder.Node node)
        {
            if (node?.Page == null)
                return;
            ESMenuTreePage page = node.Page;
            string stableId = node.StableId;
            bool wasActive = ReferenceEquals(activePage, node);
            if (wasActive)
            {
                TryHidePage(page);
                activePage = null;
                contentHost?.Clear();
                selectedPageId = string.Empty;
            }
            if (pageViews.TryGetValue(page, out VisualElement view))
                view?.RemoveFromHierarchy();
            pageViews.Remove(page);
            TryReleasePageView(page, out _);
            node.Context?.Invalidate();
            registeredPages.Remove(node);
            pagesById.Remove(stableId);
            pageBadges.Remove(stableId);
            pageBadgeLabels.Remove(stableId);
            navigationHistory.RemoveAll(id => string.Equals(id, stableId, StringComparison.Ordinal));
            navigationHistoryIndex = navigationHistory.Count == 0
                ? -1
                : Mathf.Clamp(navigationHistoryIndex, 0, navigationHistory.Count - 1);
            if (string.Equals(pendingSelectionId, stableId, StringComparison.Ordinal))
            {
                pendingSelectionId = null;
                pendingSelectionReveal = false;
            }
        }

        private void RefreshRuntimeMenu(string selectStableId)
        {
            rootNodes.Clear();
            if (menuBuilder != null)
                rootNodes.AddRange(menuBuilder.Roots);
            RemoveInvalidPageBadges();
            if (ESWindow_ShowNavigation)
            {
                RebuildNavigation();
                if (!string.IsNullOrEmpty(selectStableId))
                    ESWindow_TrySelectPage(selectStableId, true);
            }
            UpdateGlobalActionToolbar();
            UpdatePageActionToolbar();
            UpdateHeaderNavigationState();
        }

        private void RemoveInvalidPageBadges()
        {
            if (pageBadges.Count == 0)
                return;
            var removedIds = new List<string>();
            foreach (string stableId in pageBadges.Keys)
                if (!pagesById.ContainsKey(stableId))
                    removedIds.Add(stableId);
            for (int i = 0; i < removedIds.Count; i++)
                pageBadges.Remove(removedIds[i]);
        }

        private bool CacheNodeMatches(ESMenuTreeBuilder.Node node, string query)
        {
            if (node == null)
                return false;
            bool matches = NodeSelfMatches(node, query);
            for (int i = 0; i < node.Children.Count; i++)
                matches |= CacheNodeMatches(node.Children[i], query);
            navigationMatchCache[node] = matches;
            return matches;
        }

        private bool NodeMatchesCached(ESMenuTreeBuilder.Node node, string query)
        {
            return node != null
                && (string.IsNullOrEmpty(query)
                    || navigationMatchCache.TryGetValue(node, out bool matches) && matches);
        }

        private static bool NodeSelfMatches(ESMenuTreeBuilder.Node node, string query)
        {
            return ContainsIgnoreCase(node.Name, query)
                || ContainsIgnoreCase(node.Path, query)
                || ContainsIgnoreCase(node.StableId, query)
                || ContainsIgnoreCase(node.Keywords, query)
                || ContainsIgnoreCase(node.NavigationLabel, query)
                || ContainsIgnoreCase(node.Definition?.RuntimeOwnerId, query);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnWindowKeyDown(KeyDownEvent evt)
        {
            if (!ESWindow_ShowNavigation
                || searchField == null
                || !(evt.ctrlKey || evt.commandKey)
                || evt.keyCode != KeyCode.K)
                return;
            searchField.Focus();
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.UpArrow
                || evt.keyCode == KeyCode.DownArrow
                || evt.keyCode == KeyCode.Return
                || evt.keyCode == KeyCode.KeypadEnter)
                FlushSearchNavigation();
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveSearchCandidate(-1);
                    break;
                case KeyCode.DownArrow:
                    MoveSearchCandidate(1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenSearchCandidate();
                    break;
                case KeyCode.Escape:
                    ClearSearchFromKeyboard();
                    break;
                default:
                    return;
            }
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void MoveSearchCandidate(int direction)
        {
            if (direction == 0 || visiblePageIds.Count == 0)
                return;
            int currentIndex = visiblePageIds.IndexOf(searchCandidatePageId);
            if (currentIndex < 0)
                currentIndex = visiblePageIds.IndexOf(selectedPageId);
            int nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : visiblePageIds.Count - 1)
                : Mathf.Clamp(currentIndex + Math.Sign(direction), 0, visiblePageIds.Count - 1);
            searchCandidatePageId = visiblePageIds[nextIndex];
            RefreshSelectionStyles();
            ScrollSelectedPageIntoView();
            if (pagesById.TryGetValue(searchCandidatePageId, out ESMenuTreeBuilder.Node candidate))
                SetStatus("Enter 打开页面：" + candidate.Path, ESMenuTreePageStatus.Info);
        }

        private void OpenSearchCandidate()
        {
            if (string.IsNullOrEmpty(searchCandidatePageId))
                return;
            ESWindow_TrySelectPage(searchCandidatePageId, false);
            searchField?.Focus();
        }

        private void ClearSearchFromKeyboard()
        {
            if (string.IsNullOrEmpty(searchTerm))
                return;
            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = null;
            searchTerm = string.Empty;
            searchField?.SetValueWithoutNotify(string.Empty);
            RebuildNavigation();
            searchField?.Focus();
        }

        private void FlushSearchNavigation()
        {
            string query = (searchTerm ?? string.Empty).Trim();
            if (string.Equals(renderedSearchTerm, query, StringComparison.Ordinal))
                return;
            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = null;
            RebuildNavigation();
        }

        private void OnHeaderGeometryChanged(GeometryChangedEvent evt)
        {
            float headerWidth = evt.newRect.width;
            bool compactHeader = headerWidth <= 0f || headerWidth < 960f;
            // 窄窗口不允许动作行按内容宽度向左溢出；所有动作从左侧稳定排列，
            // 超出的页面动作由自己的 overflow 菜单承接。
            ConfigureResponsiveHeaderLayout(compactHeader);
            int nextHeaderCapacity = evt.newRect.width >= 1280f ? 3
                : evt.newRect.width >= 1080f ? 2
                : evt.newRect.width >= 900f ? 1
                : 0;
            int nextPageCapacity = evt.newRect.width >= 1180f ? 3
                : evt.newRect.width >= 920f ? 2
                : 1;
            if (nextHeaderCapacity != globalActionCapacity)
            {
                globalActionCapacity = nextHeaderCapacity;
                UpdateGlobalActionToolbar();
            }
            if (nextPageCapacity != pageActionCapacity)
            {
                pageActionCapacity = nextPageCapacity;
                UpdatePageActionToolbar();
            }
        }

        private void ConfigureResponsiveHeaderLayout(bool compact)
        {
            if (actionToolbarStack == null)
                return;
            actionToolbarStack.style.flexGrow = compact ? 1f : 0f;
            actionToolbarStack.style.width = compact ? Length.Percent(100f) : StyleKeyword.Auto;
            actionToolbarStack.style.alignItems = compact ? Align.Stretch : Align.FlexEnd;
            VisualElement[] rows = { systemActionRow, globalActionRow, windowActionRow, pageActionRow };
            for (int i = 0; i < rows.Length; i++)
            {
                VisualElement row = rows[i];
                if (row == null)
                    continue;
                row.style.flexGrow = compact ? 1f : 0f;
                row.style.flexShrink = compact ? 1f : 0f;
                row.style.width = compact ? Length.Percent(100f) : StyleKeyword.Auto;
                row.style.justifyContent = compact ? Justify.FlexStart : Justify.FlexEnd;
                row.style.overflow = Overflow.Visible;
            }
        }

        private void OnNavigationGeometryChanged(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            if (width < MenuPaneMinimumWidth - 1f
                || width > MenuPaneMaximumWidth + 1f
                || Mathf.Abs(menuPaneWidth - width) < 0.5f)
                return;
            menuPaneWidth = width;
        }

        private void SelectPage(string stableId)
        {
            if (pageTransitionInProgress)
            {
                SchedulePageSelection(stableId, false);
                return;
            }

            pageTransitionInProgress = true;
            try
            {
                SelectPageNow(stableId);
            }
            finally
            {
                pageTransitionInProgress = false;
            }
        }

        private void SelectPageNow(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)
                || !pagesById.TryGetValue(stableId, out ESMenuTreeBuilder.Node pageNode)
                || pageNode.Page == null)
                return;
            if (pageButtons.ContainsKey(pageNode.StableId))
                searchCandidatePageId = pageNode.StableId;
            if (ReferenceEquals(activePage, pageNode) && contentHost?.childCount > 0)
            {
                RememberSuccessfulPageSelection(pageNode.StableId);
                RefreshSelectionStyles();
                UpdatePageActionToolbar();
                UpdateHeaderNavigationState();
                UpdateUnsavedChangesState();
                return;
            }
            if (activePage?.Page != null
                && !TryResolvePendingChanges(activePage.Page, ESMenuTreePageLeaveReason.Navigate))
                return;

            string previousPageId = activePage?.StableId;
            HideActivePage();
            selectedPageId = pageNode.StableId;
            activePage = pageNode;
            contentHost?.Clear();
            bool lifecycleEntered = false;
            VisualElement view = null;
            try
            {
                if (!pageViews.TryGetValue(pageNode.Page, out view) || view == null)
                {
                    view = pageNode.Page.CreateView(pageNode.Context);
                    if (view == null)
                        throw new InvalidOperationException("页面 CreateView 返回了 null：" + pageNode.StableId);
                    view.name = string.IsNullOrEmpty(view.name) ? "ESMenuTreePageView" : view.name;
                    view.style.flexGrow = 1f;
                    view.style.flexShrink = 1f;
                    view.style.minWidth = 0f;
                    view.style.minHeight = 0f;
                    pageViews[pageNode.Page] = view;
                }
                ApplyPageLayout(view, pageNode.Definition);
                contentHost?.Add(view);
                SetStatus("当前页面：" + pageNode.Path, ESMenuTreePageStatus.Ready);
                lifecycleEntered = true;
                pageNode.Page.OnShow();
                pageNode.Page.Refresh();
                RememberSuccessfulPageSelection(pageNode.StableId);
                if (!suppressSelectionFeedback && string.IsNullOrEmpty(pendingSelectionId))
                    RecordNavigation(pageNode.StableId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (lifecycleEntered)
                    TryHidePage(pageNode.Page);
                view?.RemoveFromHierarchy();
                pageViews.Remove(pageNode.Page);
                TryReleasePageView(pageNode.Page, out _);
                activePage = null;
                if (TryRestorePageAfterFailedNavigation(previousPageId, out Exception restoreFailure))
                {
                    PublishFeedback(
                        "页面打开失败，已恢复上一页面：" + pageNode.Path,
                        ESMenuTreePageStatus.Warning,
                        ESEditorFeedbackSoundKind.Error,
                        true);
                }
                else
                {
                    string recovery = restoreFailure == null
                        ? "修复页面依赖后点击“重试”，或切换到其他页面。"
                        : "上一页面恢复也失败：" + restoreFailure.Message + "。修复页面依赖后重试。";
                    ShowContentErrorState(
                        "页面打开失败",
                        exception.Message,
                        "当前页面无法显示，其他菜单页面仍可继续使用。",
                        recovery,
                        () => SelectPage(stableId));
                    SetStatus("页面打开失败：" + pageNode.Path, ESMenuTreePageStatus.Error);
                }
            }
            if (activePage != null
                && !suppressSelectionFeedback
                && string.IsNullOrEmpty(pendingSelectionId)
                && !string.Equals(previousPageId, selectedPageId, StringComparison.Ordinal))
            {
                PublishSelectionFeedback(activePage);
                NotifySelectionChanged(selectedPageId);
            }
            RefreshSelectionStyles();
            UpdatePageActionToolbar();
            UpdateHeaderNavigationState();
            UpdateUnsavedChangesState();
        }

        private bool TryRestorePageAfterFailedNavigation(
            string stableId,
            out Exception failure)
        {
            failure = null;
            if (string.IsNullOrEmpty(stableId)
                || !pagesById.TryGetValue(stableId, out ESMenuTreeBuilder.Node previous)
                || previous.Page == null
                || !pageViews.TryGetValue(previous.Page, out VisualElement previousView)
                || previousView == null)
                return false;
            try
            {
                contentHost?.Clear();
                activePage = previous;
                selectedPageId = previous.StableId;
                ApplyPageLayout(previousView, previous.Definition);
                contentHost?.Add(previousView);
                previous.Page.OnShow();
                previous.Page.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                failure = exception;
                Debug.LogException(exception);
                previousView.RemoveFromHierarchy();
                pageViews.Remove(previous.Page);
                TryReleasePageView(previous.Page, out _);
                activePage = null;
                return false;
            }
        }

        public bool ESWindow_TrySelectPage(string stableId, bool revealInMenu = true)
        {
            if (string.IsNullOrWhiteSpace(stableId)
                || !pagesById.TryGetValue(stableId, out ESMenuTreeBuilder.Node pageNode)
                || pageNode.Page == null)
                return false;
            if (pageTransitionInProgress)
            {
                SchedulePageSelection(pageNode.StableId, revealInMenu);
                return true;
            }

            bool navigationChanged = false;
            if (revealInMenu)
            {
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = string.Empty;
                    searchField?.SetValueWithoutNotify(string.Empty);
                    navigationChanged = true;
                }
                navigationChanged |= ExpandAncestorPaths(pageNode.Path);
            }

            if (navigationChanged)
            {
                selectedPageId = pageNode.StableId;
                RebuildNavigation();
            }
            else
            {
                SelectPage(pageNode.StableId);
                ScrollSelectedPageIntoView();
            }
            return ReferenceEquals(activePage, pageNode);
        }

        private void SchedulePageSelection(string stableId, bool revealInMenu)
        {
            if (string.IsNullOrWhiteSpace(stableId) || rootVisualElement == null)
                return;
            pendingSelectionId = stableId;
            pendingSelectionReveal = revealInMenu;
            if (pendingSelectionSchedule != null)
                return;
            pendingSelectionSchedule = rootVisualElement.schedule.Execute(ExecutePendingPageSelection);
        }

        private void ExecutePendingPageSelection()
        {
            pendingSelectionSchedule = null;
            string stableId = pendingSelectionId;
            bool revealInMenu = pendingSelectionReveal;
            pendingSelectionId = null;
            pendingSelectionReveal = false;
            if (!string.IsNullOrEmpty(stableId))
                ESWindow_TrySelectPage(stableId, revealInMenu);
        }

        private bool ExpandAncestorPaths(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            bool changed = false;
            int separator = path.IndexOf('/');
            while (separator > 0)
            {
                string ancestor = path.Substring(0, separator);
                if (expandedPathLookup.Add(ancestor))
                {
                    expandedPaths.Add(ancestor);
                    changed = true;
                }
                separator = path.IndexOf('/', separator + 1);
            }
            return changed;
        }

        private void ScrollSelectedPageIntoView()
        {
            string targetId = !string.IsNullOrEmpty(searchCandidatePageId)
                && pageButtons.ContainsKey(searchCandidatePageId)
                    ? searchCandidatePageId
                    : selectedPageId;
            if (navigationScroll != null
                && !string.IsNullOrEmpty(targetId)
                && pageButtons.TryGetValue(targetId, out Button button))
                navigationScroll.ScrollTo(button);
        }

        private void NotifySelectionChanged(string stableId)
        {
            try
            {
                ESWindow_SelectionChanged?.Invoke(stableId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void RestoreRememberedPageSelection()
        {
            if (!ESWindow_RememberLastPage || pagesById.Count == 0)
                return;
            string key = GetRememberedPagePreferenceKey();
            string remembered = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(remembered))
                return;
            string resolved = ResolveRememberedPageId(remembered, pagesById);
            if (!string.IsNullOrEmpty(resolved))
            {
                selectedPageId = resolved;
                return;
            }

            EditorPrefs.DeleteKey(key);
            selectedPageId = string.Empty;
        }

        private void RememberSuccessfulPageSelection(string stableId)
        {
            if (!ESWindow_RememberLastPage || string.IsNullOrWhiteSpace(stableId))
                return;
            string key = GetRememberedPagePreferenceKey();
            if (!string.Equals(EditorPrefs.GetString(key, string.Empty), stableId, StringComparison.Ordinal))
                EditorPrefs.SetString(key, stableId);
        }

        private string GetRememberedPagePreferenceKey()
        {
            return BuildRememberedPagePreferenceKey(Application.dataPath, GetType());
        }

        internal static string BuildRememberedPagePreferenceKey(string projectDataPath, Type windowType)
        {
            string project = (projectDataPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
            string typeName = windowType == null
                ? "UnknownWindow"
                : (windowType.FullName ?? windowType.Name)
                    + "|"
                    + windowType.Assembly.GetName().Name;
            string identity = project + "|" + typeName;
            return "ES.MenuTree.LastPage." + Hash128.Compute(identity);
        }

        internal static string ResolveRememberedPageId(
            string remembered,
            IReadOnlyDictionary<string, ESMenuTreeBuilder.Node> availablePages)
        {
            if (string.IsNullOrWhiteSpace(remembered)
                || availablePages == null
                || !availablePages.TryGetValue(remembered, out ESMenuTreeBuilder.Node node)
                || node?.Page == null)
                return string.Empty;
            return node.StableId;
        }

        public bool ESWindow_TryNavigateBack()
        {
            return TryNavigateHistory(-1);
        }

        public bool ESWindow_TryNavigateForward()
        {
            return TryNavigateHistory(1);
        }

        public bool ESWindow_RefreshSelectedPage()
        {
            ESMenuTreeBuilder.Node refreshingPage = activePage;
            if (refreshingPage?.Page == null || pageTransitionInProgress)
                return false;
            pageTransitionInProgress = true;
            try
            {
                if (refreshingPage.Page is ESOdinPropertyTreePage odinPage)
                    odinPage.RefreshFromSource();
                else
                    refreshingPage.Page.Refresh();
                UpdatePageActionToolbar();
                if (ReferenceEquals(activePage, refreshingPage) && string.IsNullOrEmpty(pendingSelectionId))
                {
                    PublishFeedback(
                        "已刷新当前页面：" + refreshingPage.Path,
                        ESMenuTreePageStatus.Ready,
                        ESEditorFeedbackSoundKind.Refresh,
                        false);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                UpdatePageActionToolbar();
                PublishFeedback(
                    "刷新页面失败：" + refreshingPage.Path,
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
                return false;
            }
            finally
            {
                pageTransitionInProgress = false;
            }
        }

        public bool ESWindow_RebuildSelectedPage()
        {
            return activePage != null && ESWindow_TryRebuildPage(activePage.StableId);
        }

        public bool ESWindow_TryRebuildPage(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)
                || !pagesById.TryGetValue(stableId, out ESMenuTreeBuilder.Node pageNode)
                || pageNode.Page == null)
                return false;
            if (string.Equals(rebuildingPageId, pageNode.StableId, StringComparison.Ordinal))
                return false;
            if (pageTransitionInProgress || !string.IsNullOrEmpty(rebuildingPageId))
            {
                SchedulePageViewRebuild(pageNode.StableId);
                return true;
            }

            rebuildingPageId = pageNode.StableId;
            pageTransitionInProgress = true;
            bool wasActive = ReferenceEquals(activePage, pageNode);
            if (wasActive
                && !TryResolvePendingChanges(pageNode.Page, ESMenuTreePageLeaveReason.RebuildView))
                return false;
            try
            {
                pageNode.Context?.CancelAllTasks();
                if (wasActive)
                    HideActivePage();
                if (pageViews.TryGetValue(pageNode.Page, out VisualElement oldView))
                    oldView?.RemoveFromHierarchy();
                pageViews.Remove(pageNode.Page);

                if (!TryReleasePageView(pageNode.Page, out Exception releaseFailure))
                {
                    if (wasActive)
                    {
                        ShowContentErrorState(
                            "页面局部重建失败",
                            releaseFailure?.Message ?? "页面视图释放失败。",
                            "当前页面已停止显示，其他菜单页面不受影响。",
                            "修复页面 ReleaseView 后重试，或切换到其他页面。",
                            () => ESWindow_TryRebuildPage(stableId));
                        SetStatus("页面局部重建失败：" + pageNode.Path, ESMenuTreePageStatus.Error);
                    }
                    else
                    {
                        PublishFeedback(
                            "后台页面局部重建失败：" + pageNode.Path + "；"
                            + (releaseFailure?.Message ?? "页面视图释放失败。"),
                            ESMenuTreePageStatus.Error,
                            ESEditorFeedbackSoundKind.Error,
                            true);
                    }
                    return false;
                }

                if (!wasActive)
                    return true;

                bool previousSuppression = suppressSelectionFeedback;
                suppressSelectionFeedback = true;
                try
                {
                    SelectPageNow(pageNode.StableId);
                }
                finally
                {
                    suppressSelectionFeedback = previousSuppression;
                }
                bool succeeded = ReferenceEquals(activePage, pageNode);
                if (succeeded)
                {
                    PublishFeedback(
                        "已局部重建当前页面：" + pageNode.Path,
                        ESMenuTreePageStatus.Ready,
                        ESEditorFeedbackSoundKind.Refresh,
                        false);
                }
                return succeeded;
            }
            finally
            {
                pageTransitionInProgress = false;
                rebuildingPageId = null;
            }
        }

        private void SchedulePageViewRebuild(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || rootVisualElement == null)
                return;
            pendingPageViewRebuilds.Add(stableId);
            if (pendingPageViewRebuildSchedule != null)
                return;
            pendingPageViewRebuildSchedule = rootVisualElement.schedule.Execute(ExecutePendingPageViewRebuilds);
        }

        private void ExecutePendingPageViewRebuilds()
        {
            pendingPageViewRebuildSchedule = null;
            string[] stableIds = pendingPageViewRebuilds.ToArray();
            pendingPageViewRebuilds.Clear();
            for (int i = 0; i < stableIds.Length; i++)
                ESWindow_TryRebuildPage(stableIds[i]);
        }

        private void CancelPendingPageViewRebuilds()
        {
            pendingPageViewRebuildSchedule?.Pause();
            pendingPageViewRebuildSchedule = null;
            pendingPageViewRebuilds.Clear();
        }

        private void ValidateGlobalActions()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < globalActions.Count; i++)
            {
                ESMenuTreeGlobalAction action = globalActions[i]
                    ?? throw new InvalidOperationException("窗口右上动作不能为 null。");
                if (!ids.Add(action.Id))
                    throw new InvalidOperationException("窗口右上动作 ID 重复：" + action.Id);
            }
        }

        private void BuildToolbarContract(VisualElement toolbar)
        {
            actionToolbarStack = new VisualElement { name = "ESMenuTreeToolbarContract" };
            actionToolbarStack.style.flexDirection = ESWindow_UseCompactHostChrome
                ? FlexDirection.Row
                : FlexDirection.Column;
            actionToolbarStack.style.alignItems = Align.FlexEnd;
            actionToolbarStack.style.justifyContent = Justify.Center;
            actionToolbarStack.style.flexShrink = 0f;
            actionToolbarStack.style.minWidth = 0f;
            actionToolbarStack.style.marginTop = 1f;
            actionToolbarStack.style.marginBottom = 1f;
            if (ESWindow_UseCompactHostChrome)
            {
                actionToolbarStack.style.alignItems = Align.Center;
                actionToolbarStack.style.flexWrap = Wrap.Wrap;
                actionToolbarStack.style.minHeight = 24f;
                actionToolbarStack.style.maxHeight = StyleKeyword.None;
            }
            toolbar.Add(actionToolbarStack);

            systemActionRow = CreateToolbarScopeRow(
                "ESMenuTreeSystemActionRow",
                "系统",
                "窗口生命周期与休眠控制；不执行页面业务命令。");
            systemActionToolbar = new VisualElement { name = "ESMenuTreeSystemActions" };
            systemActionToolbar.style.flexDirection = FlexDirection.Row;
            systemActionToolbar.style.flexWrap = Wrap.Wrap;
            systemActionToolbar.style.alignItems = Align.Center;
            systemActionToolbar.style.flexGrow = 1f;
            systemActionToolbar.style.flexShrink = 1f;
            systemActionToolbar.style.minWidth = 0f;
            systemActionToolbar.style.overflow = Overflow.Visible;
            systemActionRow.Add(systemActionToolbar);
            actionToolbarStack.Add(systemActionRow);

            globalActionRow = CreateToolbarScopeRow(
                "ESMenuTreeGlobalActionRow",
                "全局",
                "窗口通用动作；不依赖当前页面上下文。");
            actionToolbarStack.Add(globalActionRow);
            BuildGlobalActionToolbar(globalActionRow);
            windowActionRow = CreateToolbarScopeRow(
                "ESMenuTreeWindowActionRow",
                "窗口",
                "当前窗口实例的业务动作；不依赖当前页面上下文。");
            windowActionToolbar = new VisualElement { name = "ESMenuTreeWindowActions" };
            windowActionToolbar.style.flexDirection = FlexDirection.Row;
            windowActionToolbar.style.flexWrap = Wrap.Wrap;
            windowActionToolbar.style.alignItems = Align.Center;
            windowActionToolbar.style.flexGrow = 1f;
            windowActionToolbar.style.flexShrink = 1f;
            windowActionToolbar.style.minWidth = 0f;
            windowActionToolbar.style.overflow = Overflow.Visible;
            windowActionRow.Add(windowActionToolbar);
            actionToolbarStack.Add(windowActionRow);
            pageActionRow = CreateToolbarScopeRow(
                "ESMenuTreePageActionRow",
                "页面",
                "仅作用于当前选中页面，并使用当前页面上下文。");
            actionToolbarStack.Add(pageActionRow);
            BuildPageActionToolbar(pageActionRow);
            BuildHeaderNavigation(pageActionRow);

            if (ESWindow_UseCompactHostChrome)
            {
                ConfigureCompactToolbarScope(systemActionRow);
                ConfigureCompactToolbarScope(globalActionRow);
                ConfigureCompactToolbarScope(windowActionRow);
                ConfigureCompactToolbarScope(pageActionRow);
            }

            actionHosts = new ESWindowActionHosts(
                systemActionToolbar,
                globalActionToolbar,
                windowActionToolbar);
            ESWindow_BuildActionHosts(actionHosts);
            systemActionRow.style.display = ESWindow_SupportsSemiSleep
                && ESWindow_SleepLinkMode != ESWindowSleepLinkMode.OwnedSurface
                || systemActionToolbar.childCount > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            UpdateCompactToolbarScopeVisibility();
        }

        private static void ConfigureCompactToolbarScope(VisualElement row)
        {
            if (row == null)
                return;
            row.style.minHeight = 24f;
            row.style.maxHeight = StyleKeyword.None;
            row.style.marginLeft = 2f;
            row.style.marginRight = 0f;
            Label label = row.Q<Label>(row.name + "Label");
            if (label != null)
                label.style.display = DisplayStyle.None;
        }

        private void UpdateCompactToolbarScopeVisibility()
        {
            bool systemVisible = HasToolbarAction(systemActionToolbar);
            bool globalVisible = HasToolbarAction(globalActionToolbar);
            bool windowVisible = HasToolbarAction(windowActionToolbar);
            // 页面行还承载上一页/下一页等导航按钮，不能只检查页面专属动作容器。
            bool pageVisible = HasToolbarAction(pageActionRow);

            SetToolbarScopeVisibility(systemActionRow, systemVisible);
            SetToolbarScopeVisibility(globalActionRow, globalVisible);
            SetToolbarScopeVisibility(windowActionRow, windowVisible);
            SetToolbarScopeVisibility(pageActionRow, pageVisible);

            // 空的动作栈也不能留下顶部边距，否则窗口看起来像多出一条无意义的空行。
            if (actionToolbarStack != null)
                actionToolbarStack.style.display = systemVisible
                    || globalVisible
                    || windowVisible
                    || pageVisible
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
        }

        private void SetToolbarScopeVisibility(VisualElement row, bool visible)
        {
            if (row == null)
                return;
            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            Label label = row.Q<Label>(row.name + "Label");
            if (label == null)
                return;
            label.style.display = visible && !ESWindow_UseCompactHostChrome
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static bool HasToolbarAction(VisualElement root)
        {
            if (root == null)
                return false;
            if (root is Button || root is ToolbarMenu || root is ToolbarToggle)
                return true;
            foreach (VisualElement child in root.Children())
                if (HasToolbarAction(child))
                    return true;
            return false;
        }

        private static VisualElement CreateToolbarScopeRow(
            string name,
            string labelText,
            string tooltip)
        {
            var row = new VisualElement { name = name, tooltip = tooltip };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.flexShrink = 1f;
            row.style.minWidth = 0f;
            row.style.minHeight = 25f;
            row.style.width = Length.Percent(100f);
            row.style.overflow = Overflow.Visible;

            var label = new Label(labelText) { name = name + "Label", tooltip = tooltip };
            label.AddToClassList("es-brand-status");
            label.style.width = 32f;
            label.style.minWidth = 32f;
            label.style.marginRight = 4f;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            label.style.fontSize = 9f;
            label.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            row.Add(label);
            return row;
        }

        private void BuildGlobalActionToolbar(VisualElement toolbar)
        {
            globalActionToolbar = new VisualElement { name = "ESMenuTreeGlobalActions" };
            globalActionToolbar.style.flexDirection = FlexDirection.Row;
            globalActionToolbar.style.flexWrap = Wrap.Wrap;
            globalActionToolbar.style.alignItems = Align.Center;
            globalActionToolbar.style.flexGrow = 1f;
            globalActionToolbar.style.flexShrink = 1f;
            globalActionToolbar.style.minWidth = 0f;
            globalActionToolbar.style.overflow = Overflow.Visible;
            toolbar.Add(globalActionToolbar);

            builtInGlobalActionToolbar = new VisualElement
            {
                name = "ESMenuTreeBuiltInGlobalActions"
            };
            builtInGlobalActionToolbar.style.flexDirection = FlexDirection.Row;
            builtInGlobalActionToolbar.style.flexWrap = Wrap.Wrap;
            builtInGlobalActionToolbar.style.alignItems = Align.Center;
            builtInGlobalActionToolbar.style.flexGrow = 1f;
            builtInGlobalActionToolbar.style.flexShrink = 1f;
            builtInGlobalActionToolbar.style.minWidth = 0f;
            builtInGlobalActionToolbar.style.overflow = Overflow.Visible;
            globalActionToolbar.Add(builtInGlobalActionToolbar);
            ClearRenderedGlobalActions();
            UpdateGlobalActionToolbar();
        }

        private void UpdateGlobalActionToolbar()
        {
            if (builtInGlobalActionToolbar == null)
                return;
            visibleGlobalActions.Clear();
            for (int i = 0; i < globalActions.Count; i++)
                if (IsGlobalActionVisible(globalActions[i]))
                    visibleGlobalActions.Add(globalActions[i]);
            OrderGlobalActions(visibleGlobalActions);
            int visibleCount = Mathf.Min(globalActionCapacity, visibleGlobalActions.Count);
            if (GlobalActionStructureMatches(visibleCount))
            {
                UpdateRenderedGlobalActionStates(visibleCount);
                UpdateCompactToolbarScopeVisibility();
                return;
            }

            ClearRenderedGlobalActions();
            renderedGlobalActionCapacity = globalActionCapacity;
            renderedGlobalActions.AddRange(visibleGlobalActions);
            for (int i = 0; i < visibleCount; i++)
            {
                ESMenuTreeGlobalAction action = visibleGlobalActions[i];
                Button button = ES.EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                    action.Icon,
                    action.Text,
                    action.Tooltip,
                    () => ExecuteGlobalAction(action));
                button.style.maxWidth = 146f;
                Label label = button.Q<Label>();
                if (label != null)
                {
                    label.style.maxWidth = 104f;
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                }
                renderedGlobalActionButtons[action.Id] = button;
                builtInGlobalActionToolbar.Add(button);
            }

            if (visibleCount < visibleGlobalActions.Count)
                builtInGlobalActionToolbar.Add(
                    CreateGlobalActionOverflowMenu(visibleGlobalActions, visibleCount));
            UpdateRenderedGlobalActionStates(visibleCount);
            UpdateCompactToolbarScopeVisibility();
        }

        private bool GlobalActionStructureMatches(int visibleCount)
        {
            if (renderedGlobalActionCapacity != globalActionCapacity
                || renderedGlobalActions.Count != visibleGlobalActions.Count
                || renderedGlobalActionButtons.Count != visibleCount)
                return false;
            for (int i = 0; i < visibleGlobalActions.Count; i++)
                if (!ReferenceEquals(renderedGlobalActions[i], visibleGlobalActions[i]))
                    return false;
            return true;
        }

        private void UpdateRenderedGlobalActionStates(int visibleCount)
        {
            for (int i = 0; i < visibleCount; i++)
            {
                ESMenuTreeGlobalAction action = visibleGlobalActions[i];
                if (!renderedGlobalActionButtons.TryGetValue(action.Id, out Button button))
                    continue;
                ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                    button,
                    IsGlobalActionEnabled(action));
                ApplyCheckedActionStyle(button, IsGlobalActionChecked(action));
            }
        }

        private void ClearRenderedGlobalActions()
        {
            builtInGlobalActionToolbar?.Clear();
            renderedGlobalActions.Clear();
            renderedGlobalActionButtons.Clear();
            renderedGlobalActionCapacity = -1;
        }

        private static void OrderGlobalActions(List<ESMenuTreeGlobalAction> actions)
        {
            for (int i = 1; i < actions.Count; i++)
            {
                ESMenuTreeGlobalAction action = actions[i];
                int insertAt = i;
                while (insertAt > 0 && action.Priority > actions[insertAt - 1].Priority)
                {
                    actions[insertAt] = actions[insertAt - 1];
                    insertAt--;
                }
                actions[insertAt] = action;
            }
        }

        private ToolbarMenu CreateGlobalActionOverflowMenu(
            List<ESMenuTreeGlobalAction> actions,
            int firstOverflowIndex)
        {
            var menu = CreateCompactOverflowMenu(
                "ESMenuTreeGlobalActionOverflow",
                "全局",
                "显示当前工具跨页面通用的全局动作。");
            for (int i = firstOverflowIndex; i < actions.Count; i++)
            {
                ESMenuTreeGlobalAction action = actions[i];
                string label = string.IsNullOrEmpty(action.Text) ? action.Id : action.Text;
                menu.menu.AppendAction(
                    label,
                    _ => ExecuteGlobalAction(action),
                    _ => !IsGlobalActionVisible(action) || !IsGlobalActionEnabled(action)
                        ? DropdownMenuAction.Status.Disabled
                        : IsGlobalActionChecked(action)
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal);
            }
            return menu;
        }

        private bool IsGlobalActionEnabled(ESMenuTreeGlobalAction action)
        {
            if (action == null)
                return false;
            return EvaluateActionState(
                "window:" + action.Id + ":enabled",
                () => action.Enabled?.Invoke() ?? true,
                false);
        }

        private bool IsGlobalActionVisible(ESMenuTreeGlobalAction action)
        {
            if (action == null)
                return false;
            return EvaluateActionState(
                "window:" + action.Id + ":visible",
                () => action.Visible?.Invoke() ?? true,
                false);
        }

        private bool IsGlobalActionChecked(ESMenuTreeGlobalAction action)
        {
            if (action == null)
                return false;
            return EvaluateActionState(
                "window:" + action.Id + ":checked",
                () => action.Checked?.Invoke() ?? false,
                false);
        }

        private void ExecuteGlobalAction(ESMenuTreeGlobalAction action)
        {
            if (!IsGlobalActionVisible(action) || !IsGlobalActionEnabled(action))
                return;
            try
            {
                action.Execute();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                PublishFeedback(
                    "全局动作失败：" + exception.Message,
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
            }
            finally
            {
                UpdateGlobalActionToolbar();
            }
        }

        private void BuildHeaderNavigation(VisualElement toolbar)
        {
            navigateBackButton = null;
            navigateForwardButton = null;
            if (ESWindow_ShowNavigation)
            {
                navigateBackButton = CreateNavigationIconButton(
                    EditorIcons.ArrowLeft.Active,
                    EditorIcons.ArrowLeft.Raw,
                    "‹",
                    "返回上一个页面",
                    () => ESWindow_TryNavigateBack());
                navigateForwardButton = CreateNavigationIconButton(
                    EditorIcons.ArrowRight.Active,
                    EditorIcons.ArrowRight.Raw,
                    "›",
                    "前往下一个页面",
                    () => ESWindow_TryNavigateForward());
                toolbar.Add(navigateBackButton);
                toolbar.Add(navigateForwardButton);
            }
            refreshPageButton = CreateNavigationIconButton(
                EditorIcons.Refresh.Active,
                EditorIcons.Refresh.Raw,
                "↻",
                "刷新当前页面",
                () => ESWindow_RefreshSelectedPage());
            Texture appearanceIcon = ES.EditorInternal.ESEditorPresentation.LoadESBrandIcon(
                "config") ?? EditorIcons.SettingsCog.Active;
            settingsButton = CreateNavigationIconButton(
                appearanceIcon,
                EditorIcons.SettingsCog.Raw,
                "⚙",
                "外观：查看职责并打开 ES 编辑器主题设置",
                OpenEditorExperienceSettings);
            settingsButton.name = "ESMenuTreeAppearanceSettingsButton";
            toolbar.Add(refreshPageButton);
            toolbar.Add(settingsButton);
            UpdateHeaderNavigationState();
        }

        private static Button CreateNavigationIconButton(
            Texture icon,
            Texture fallbackIcon,
            string fallbackText,
            string tooltip,
            Action action)
        {
            Texture resolvedIcon = icon ?? fallbackIcon;
            Button button = ES.EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                resolvedIcon,
                resolvedIcon == null ? fallbackText : string.Empty,
                tooltip,
                action);
            button.style.width = 26f;
            button.style.minWidth = 26f;
            button.style.maxWidth = 26f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            if (resolvedIcon == null)
                button.style.fontSize = 17f;
            return button;
        }

        private void OpenEditorExperienceSettings()
        {
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "es.window.appearance-settings.entry",
                title = "打开 ES 编辑器外观设置",
                subtitle = "外观职责确认",
                message = "此入口只管理 ES 编辑器窗口的颜色、密度、品牌字体边界与动效表现。",
                detail = "它不会修改当前页面的业务数据，也不是窗口休眠或全局生命周期设置。休眠策略请使用右上角“系统”入口。",
                confirmText = "打开外观设置",
                cancelText = "留在当前页面",
                tone = ESDialogTone.Info,
                owner = this,
                preferredSize = new Vector2(560f, 360f),
                minSize = new Vector2(480f, 280f),
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting,
            };
            request.completed = result =>
            {
                if (result == null || !result.accepted || this == null)
                    return;
                ExecuteOpenEditorExperienceSettings();
            };
            ESDialogService.Show(request);
        }

        private void ExecuteOpenEditorExperienceSettings()
        {
            const string settingsSuffix = "编辑器体验/打开主题设置";
            string menuPath = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + settingsSuffix;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
            {
                PublishFeedback(
                    "无法打开 ES 编辑器外观设置",
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
            }
        }

        private void BuildPageActionToolbar(VisualElement toolbar)
        {
            pageActionToolbar = new VisualElement { name = "ESMenuTreePageActions" };
            pageActionToolbar.style.flexDirection = FlexDirection.Row;
            pageActionToolbar.style.flexWrap = Wrap.Wrap;
            pageActionToolbar.style.alignItems = Align.Center;
            pageActionToolbar.style.flexGrow = 1f;
            pageActionToolbar.style.flexShrink = 1f;
            pageActionToolbar.style.minWidth = 0f;
            pageActionToolbar.style.overflow = Overflow.Visible;
            toolbar.Add(pageActionToolbar);
            ClearRenderedPageActions();
        }

        private void UpdatePageActionToolbar()
        {
            if (pageActionToolbar == null)
                return;
            IReadOnlyList<ESMenuTreePageAction> actions = activePage?.Definition?.PageActions;
            ESMenuTreePageContext context = activePage?.Context;
            visiblePageActions.Clear();
            if (actions != null)
                for (int i = 0; i < actions.Count; i++)
                    if (IsPageActionVisible(actions[i], context))
                        visiblePageActions.Add(actions[i]);
            OrderPageActions(visiblePageActions);
            int visibleCount = Mathf.Min(pageActionCapacity, visiblePageActions.Count);
            if (PageActionStructureMatches(context, visibleCount))
            {
                UpdateRenderedPageActionStates(context, visibleCount);
                UpdateCompactToolbarScopeVisibility();
                return;
            }

            ClearRenderedPageActions();
            renderedPageActionContext = context;
            renderedPageActionCapacity = pageActionCapacity;
            renderedPageActions.AddRange(visiblePageActions);
            if (visiblePageActions.Count == 0)
            {
                UpdateCompactToolbarScopeVisibility();
                return;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                ESMenuTreePageAction action = visiblePageActions[i];
                Button button = ES.EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                    action.Icon,
                    action.Text,
                    action.Tooltip,
                    () => ExecutePageAction(action, context));
                button.style.maxWidth = 154f;
                Label label = button.Q<Label>();
                if (label != null)
                {
                    label.style.maxWidth = 112f;
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                }
                renderedPageActionButtons[action.Id] = button;
                pageActionToolbar.Add(button);
            }

            if (visibleCount < visiblePageActions.Count)
                pageActionToolbar.Add(CreatePageActionOverflowMenu(visiblePageActions, visibleCount, context));
            UpdateRenderedPageActionStates(context, visibleCount);
            UpdateCompactToolbarScopeVisibility();
        }

        private bool PageActionStructureMatches(
            ESMenuTreePageContext context,
            int visibleCount)
        {
            if (!ReferenceEquals(renderedPageActionContext, context)
                || renderedPageActionCapacity != pageActionCapacity
                || renderedPageActions.Count != visiblePageActions.Count
                || renderedPageActionButtons.Count != visibleCount)
                return false;
            for (int i = 0; i < visiblePageActions.Count; i++)
                if (!ReferenceEquals(renderedPageActions[i], visiblePageActions[i]))
                    return false;
            return true;
        }

        private void UpdateRenderedPageActionStates(
            ESMenuTreePageContext context,
            int visibleCount)
        {
            for (int i = 0; i < visibleCount; i++)
            {
                ESMenuTreePageAction action = visiblePageActions[i];
                if (!renderedPageActionButtons.TryGetValue(action.Id, out Button button))
                    continue;
                ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                    button,
                    IsPageActionEnabled(action, context));
                ApplyCheckedActionStyle(button, IsPageActionChecked(action, context));
            }
        }

        private void ClearRenderedPageActions()
        {
            pageActionToolbar?.Clear();
            renderedPageActions.Clear();
            renderedPageActionButtons.Clear();
            renderedPageActionContext = null;
            renderedPageActionCapacity = -1;
        }

        private static void OrderPageActions(List<ESMenuTreePageAction> actions)
        {
            for (int i = 1; i < actions.Count; i++)
            {
                ESMenuTreePageAction action = actions[i];
                int insertAt = i;
                while (insertAt > 0 && action.Priority > actions[insertAt - 1].Priority)
                {
                    actions[insertAt] = actions[insertAt - 1];
                    insertAt--;
                }
                actions[insertAt] = action;
            }
        }

        private ToolbarMenu CreatePageActionOverflowMenu(
            List<ESMenuTreePageAction> actions,
            int firstOverflowIndex,
            ESMenuTreePageContext context)
        {
            ToolbarMenu menu = CreateCompactOverflowMenu(
                "ESMenuTreePageActionOverflow",
                "页面",
                "显示当前页面的其他工具动作。");
            for (int i = firstOverflowIndex; i < actions.Count; i++)
            {
                ESMenuTreePageAction action = actions[i];
                string label = string.IsNullOrEmpty(action.Text) ? action.Id : action.Text;
                menu.menu.AppendAction(
                    label,
                    _ => ExecutePageAction(action, context),
                    _ => context == null || !context.IsAvailable || !context.IsSelected
                        || !IsPageActionVisible(action, context)
                        || !IsPageActionEnabled(action, context)
                            ? DropdownMenuAction.Status.Disabled
                            : IsPageActionChecked(action, context)
                                ? DropdownMenuAction.Status.Checked
                                : DropdownMenuAction.Status.Normal);
            }
            return menu;
        }

        private static ToolbarMenu CreateCompactOverflowMenu(
            string name,
            string text,
            string tooltip)
        {
            return ES.EditorInternal.ESWindowPresentation.CreateHeaderOverflowMenu(
                name,
                text,
                tooltip,
                48f,
                58f);
        }

        private bool IsPageActionEnabled(
            ESMenuTreePageAction action,
            ESMenuTreePageContext context)
        {
            if (action == null || context == null || !context.IsAvailable || !context.IsSelected)
                return false;
            return EvaluateActionState(
                "page:" + context.StableId + ":" + action.Id + ":enabled",
                () => action.Enabled?.Invoke(context) ?? true,
                false);
        }

        private bool IsPageActionVisible(
            ESMenuTreePageAction action,
            ESMenuTreePageContext context)
        {
            if (action == null || context == null || !context.IsAvailable || !context.IsSelected)
                return false;
            return EvaluateActionState(
                "page:" + context.StableId + ":" + action.Id + ":visible",
                () => action.Visible?.Invoke(context) ?? true,
                false);
        }

        private bool IsPageActionChecked(
            ESMenuTreePageAction action,
            ESMenuTreePageContext context)
        {
            if (action == null || context == null || !context.IsAvailable || !context.IsSelected)
                return false;
            return EvaluateActionState(
                "page:" + context.StableId + ":" + action.Id + ":checked",
                () => action.Checked?.Invoke(context) ?? false,
                false);
        }

        private static void ApplyCheckedActionStyle(Button control, bool isChecked)
        {
            if (control == null)
                return;
            ES.EditorInternal.ESWindowPresentation.SetButtonPresentationState(
                control,
                isChecked
                    ? ES.EditorInternal.ESEditorPresentation.ESPresentationState.Selected
                    : ES.EditorInternal.ESEditorPresentation.ESPresentationState.Normal);
        }

        private bool EvaluateActionState(
            string failureKey,
            Func<bool> evaluator,
            bool fallback)
        {
            try
            {
                bool result = evaluator();
                actionEvaluationFailures.Remove(failureKey);
                return result;
            }
            catch (Exception exception)
            {
                if (actionEvaluationFailures.Add(failureKey))
                    Debug.LogException(exception);
                return fallback;
            }
        }

        private void ExecutePageAction(
            ESMenuTreePageAction action,
            ESMenuTreePageContext expectedContext)
        {
            if (action == null)
                return;
            ESMenuTreePageContext context = activePage?.Context;
            if (!ReferenceEquals(context, expectedContext)
                || context == null
                || !context.IsAvailable
                || !context.IsSelected)
                return;
            try
            {
                if (!IsPageActionVisible(action, context)
                    || !IsPageActionEnabled(action, context))
                    return;
                action.Execute(context);
                if (!context.IsAvailable || !context.IsSelected)
                    return;
                if (!string.IsNullOrEmpty(action.SuccessMessage))
                    PublishFeedback(
                        action.SuccessMessage,
                        ESMenuTreePageStatus.Ready,
                        action.Sound,
                        true);
                else if (action.Sound.HasValue)
                    ESEditorFeedbackSound.Play(action.Sound.Value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (context.IsAvailable && context.IsSelected)
                {
                    PublishFeedback(
                        "页面动作失败：" + exception.Message,
                        ESMenuTreePageStatus.Error,
                        ESEditorFeedbackSoundKind.Error,
                        true);
                }
            }
            finally
            {
                UpdatePageActionToolbar();
            }
        }

        private ESMenuTreePageContext CreatePageContext(ESMenuTreeBuilder.Node node)
        {
            return new ESMenuTreePageContext(
                this,
                node.StableId,
                node.Path,
                node.Page,
                node.Definition,
                SetStatus,
                ScheduleMenuRebuild,
                stableId => { ESWindow_TrySelectPage(stableId); },
                () =>
                {
                    if (ReferenceEquals(activePage, node))
                        UpdatePageActionToolbar();
                },
                UpdateUnsavedChangesState,
                () =>
                {
                    if (!string.Equals(rebuildingPageId, node.StableId, StringComparison.Ordinal))
                        SchedulePageViewRebuild(node.StableId);
                },
                (text, status) => ESWindow_SetPageBadge(node.StableId, text, status),
                () => ESWindow_ClearPageBadge(node.StableId),
                () => pagesById.TryGetValue(node.StableId, out ESMenuTreeBuilder.Node current)
                    && ReferenceEquals(current, node),
                () => ReferenceEquals(activePage, node),
                PublishFeedback);
        }

        private void ApplyPageLayout(VisualElement view, ESMenuTreePageDefinition definition)
        {
            if (view == null || contentHost == null)
                return;

            ESMenuTreePageLayout layout = definition?.Layout ?? ESMenuTreePageLayout.Standard;
            float padding;
            float maxWidth;
            switch (layout)
            {
                case ESMenuTreePageLayout.Inspector:
                    padding = 18f;
                    maxWidth = 1040f;
                    break;
                case ESMenuTreePageLayout.Wide:
                    padding = 8f;
                    maxWidth = 0f;
                    break;
                case ESMenuTreePageLayout.Canvas:
                    padding = 0f;
                    maxWidth = 0f;
                    break;
                case ESMenuTreePageLayout.Compact:
                    padding = 16f;
                    maxWidth = 760f;
                    break;
                default:
                    padding = 12f;
                    maxWidth = 0f;
                    break;
            }

            if (definition != null)
            {
                if (definition.ContentPadding >= 0f)
                    padding = definition.ContentPadding;
                if (definition.MaxContentWidth > 0f)
                    maxWidth = definition.MaxContentWidth;
            }

            contentHost.style.paddingLeft = padding;
            contentHost.style.paddingRight = padding;
            contentHost.style.paddingTop = padding;
            contentHost.style.paddingBottom = padding;
            contentHost.style.alignItems = maxWidth > 0f ? Align.Center : Align.Stretch;
            contentHost.style.minWidth = 0f;
            contentHost.style.minHeight = 0f;
            contentHost.style.backgroundColor = layout == ESMenuTreePageLayout.Canvas
                ? ES.EditorInternal.ESEditorPresentation.CanvasSurfaceColor
                : ES.EditorInternal.ESEditorPresentation.WindowSurfaceColor;
            view.style.width = Length.Percent(100f);
            view.style.minWidth = 0f;
            view.style.flexBasis = 0f;
            view.style.flexGrow = 1f;
            view.style.flexShrink = 1f;
            view.style.alignSelf = Align.Stretch;
            view.style.maxWidth = maxWidth > 0f
                ? new StyleLength(maxWidth)
                : new StyleLength(StyleKeyword.None);
        }

        private void PublishSelectionFeedback(ESMenuTreeBuilder.Node pageNode)
        {
            ESMenuTreePageDefinition definition = pageNode?.Definition;
            string message = !string.IsNullOrEmpty(definition?.SelectionMessage)
                ? definition.SelectionMessage
                : "当前页面：" + pageNode?.Path;
            PublishFeedback(
                message,
                ESMenuTreePageStatus.Ready,
                definition?.SelectionSound ?? ESEditorFeedbackSoundKind.Navigate,
                definition?.ShowSelectionNotification == true);
        }

        private void PublishFeedback(
            string message,
            ESMenuTreePageStatus status,
            ESEditorFeedbackSoundKind? sound,
            bool showNotification)
        {
            string normalized = string.IsNullOrWhiteSpace(message) ? "就绪" : message.Trim();
            SetStatus(normalized, status);
            if (showNotification)
                ShowNotification(new GUIContent(normalized));
            if (sound.HasValue)
                ESEditorFeedbackSound.Play(sound.Value);
        }

        private void RecordNavigation(string stableId)
        {
            if (navigatingHistory || string.IsNullOrEmpty(stableId))
                return;
            if (navigationHistoryIndex >= 0
                && navigationHistoryIndex < navigationHistory.Count
                && string.Equals(navigationHistory[navigationHistoryIndex], stableId, StringComparison.Ordinal))
                return;

            if (navigationHistoryIndex + 1 < navigationHistory.Count)
                navigationHistory.RemoveRange(
                    navigationHistoryIndex + 1,
                    navigationHistory.Count - navigationHistoryIndex - 1);
            navigationHistory.Add(stableId);
            if (navigationHistory.Count > 64)
                navigationHistory.RemoveAt(0);
            navigationHistoryIndex = navigationHistory.Count - 1;
        }

        private bool TryNavigateHistory(int direction)
        {
            if (pageTransitionInProgress)
                return false;
            int targetIndex = FindNavigableHistoryIndex(direction);
            if (targetIndex < 0)
                return false;

            int previousIndex = navigationHistoryIndex;
            navigatingHistory = true;
            try
            {
                navigationHistoryIndex = targetIndex;
                bool succeeded = ESWindow_TrySelectPage(navigationHistory[targetIndex]);
                if (!succeeded)
                    navigationHistoryIndex = previousIndex;
                return succeeded;
            }
            catch
            {
                navigationHistoryIndex = previousIndex;
                throw;
            }
            finally
            {
                navigatingHistory = false;
                UpdateHeaderNavigationState();
            }
        }

        private int FindNavigableHistoryIndex(int direction)
        {
            if (direction == 0 || navigationHistory.Count == 0)
                return -1;
            int index = navigationHistoryIndex + Math.Sign(direction);
            while (index >= 0 && index < navigationHistory.Count)
            {
                if (pagesById.ContainsKey(navigationHistory[index]))
                    return index;
                index += Math.Sign(direction);
            }
            return -1;
        }

        private void UpdateHeaderNavigationState()
        {
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                navigateBackButton,
                FindNavigableHistoryIndex(-1) >= 0);
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                navigateForwardButton,
                FindNavigableHistoryIndex(1) >= 0);
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                refreshPageButton,
                activePage?.Page != null);
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(settingsButton, true);
            UpdateGlobalActionToolbar();
        }

        private void RefreshSelectionStyles()
        {
            foreach (KeyValuePair<string, Button> pair in pageButtons)
            {
                bool selected = string.Equals(pair.Key, selectedPageId, StringComparison.Ordinal);
                bool candidate = !selected
                    && string.Equals(pair.Key, searchCandidatePageId, StringComparison.Ordinal);
                Color candidateFill = ES.EditorInternal.ESEditorPresentation.SectionSelectedFill;
                candidateFill.a *= 0.42f;
                pair.Value.style.backgroundColor = selected
                    ? ES.EditorInternal.ESEditorPresentation.SectionSelectedFill
                    : candidate ? candidateFill : Color.clear;
                pair.Value.style.color = selected
                    ? ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor
                    : ES.EditorInternal.ESEditorPresentation.SectionTextColor;
                pair.Value.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void HideActivePage()
        {
            if (activePage?.Page != null)
            {
                activePage.Context?.CancelAllTasks();
                TryHidePage(activePage.Page);
            }
            activePage = null;
            UpdatePageActionToolbar();
            UpdateUnsavedChangesState();
        }

        private static void TryHidePage(ESMenuTreePage page)
        {
            if (page == null)
                return;
            try
            {
                page.OnHide();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ShowContentEmptyState(string title, string detail, string actionText, Action action)
        {
            contentHost?.Clear();
            contentHost?.Add(ES.EditorInternal.ESWindowPresentation.CreateEmptyState(
                title, detail, actionText, action));
        }

        private void ShowContentErrorState(string title, string cause, string impact, string recovery, Action action)
        {
            contentHost?.Clear();
            contentHost?.Add(ES.EditorInternal.ESWindowPresentation.CreateErrorState(
                title, cause, impact, recovery, "重试", action));
        }

        private void SetStatus(string message, ESMenuTreePageStatus status)
        {
            shell?.SetStatus(message, ToPresentationStatus(status));
            if (status == ESMenuTreePageStatus.Error || status == ESMenuTreePageStatus.Warning)
                ES.EditorInternal.ESEditorPresentation.PulseWindow(
                    this, ToPresentationStatus(status));
        }

        private void UpdateUnsavedChangesState()
        {
#if UNITY_2021_2_OR_NEWER
            bool pending = false;
            string message = "当前 ES 页面包含尚未提交的修改。";
            try
            {
                if (activePage?.Page != null)
                {
                    pending = activePage.Page.HasPendingChanges;
                    if (pending && !string.IsNullOrWhiteSpace(activePage.Page.PendingChangesSummary))
                        message = activePage.Page.PendingChangesSummary.Trim();
                }
            }
            catch (Exception exception)
            {
                pending = true;
                message = "无法确认页面修改状态：" + exception.Message;
                Debug.LogException(exception);
            }
            hasUnsavedChanges = pending;
            saveChangesMessage = message;
#endif
        }

#if UNITY_2021_2_OR_NEWER
        public override void SaveChanges()
        {
            ESMenuTreePage page = activePage?.Page;
            if (page != null && page.HasPendingChanges)
            {
                try
                {
                    if (!page.TrySavePendingChanges(out string failure))
                    {
                        ShowNotification(new GUIContent(
                            "保存页面修改失败：" + (failure ?? "页面拒绝保存。")));
                        UpdateUnsavedChangesState();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    ShowNotification(new GUIContent("保存页面修改失败：" + exception.Message));
                    UpdateUnsavedChangesState();
                    return;
                }
            }
            hasUnsavedChanges = false;
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            try
            {
                if (activePage?.Page?.HasPendingChanges == true)
                    activePage.Page.DiscardPendingChanges();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowNotification(new GUIContent("放弃页面修改失败：" + exception.Message));
                UpdateUnsavedChangesState();
                return;
            }
            hasUnsavedChanges = false;
            base.DiscardChanges();
        }
#endif

        protected void SetWindowStatus(string message, ESMenuTreePageStatus status)
        {
            SetStatus(message, status);
        }

        protected void PublishWindowFeedback(
            string message,
            ESMenuTreePageStatus status = ESMenuTreePageStatus.Info,
            ESEditorFeedbackSoundKind? sound = null,
            bool showNotification = true)
        {
            PublishFeedback(message, status, sound, showNotification);
        }

        private static ES.EditorInternal.ESStatusKind ToPresentationStatus(ESMenuTreePageStatus status)
        {
            switch (status)
            {
                case ESMenuTreePageStatus.Info: return ES.EditorInternal.ESStatusKind.Info;
                case ESMenuTreePageStatus.Warning: return ES.EditorInternal.ESStatusKind.Warning;
                case ESMenuTreePageStatus.Error: return ES.EditorInternal.ESStatusKind.Error;
                case ESMenuTreePageStatus.ReadOnly: return ES.EditorInternal.ESStatusKind.ReadOnly;
                case ESMenuTreePageStatus.Modified: return ES.EditorInternal.ESStatusKind.Modified;
                default: return ES.EditorInternal.ESStatusKind.Ready;
            }
        }

        private void SetExpanded(string path, bool expanded)
        {
            if (expanded)
            {
                if (expandedPathLookup.Add(path))
                    expandedPaths.Add(path);
            }
            else
            {
                expandedPathLookup.Remove(path);
                expandedPaths.Remove(path);
            }
        }

        private void ScheduleMenuRebuild()
        {
            if (rebuildScheduled || rootVisualElement == null)
                return;
            if (activePage?.Page != null
                && !TryResolvePendingChanges(activePage.Page, ESMenuTreePageLeaveReason.RebuildWindow))
                return;
            rebuildScheduled = true;
            rootVisualElement.schedule.Execute(RebuildWindow);
        }

        /// <summary>供窗口打开/宿主关系变更后请求一次菜单树重建。</summary>
        public void ForceMenuTreeRebuild()
        {
            ScheduleMenuRebuild();
        }

        private bool TryResolvePendingChanges(
            ESMenuTreePage page,
            ESMenuTreePageLeaveReason reason)
        {
            if (page == null)
                return true;

            bool hasChanges;
            string summary;
            try
            {
                hasChanges = page.HasPendingChanges;
                summary = page.PendingChangesSummary;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                PublishFeedback(
                    "无法确认页面修改状态：" + exception.Message,
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
                return false;
            }
            if (!hasChanges)
                return true;

            string action;
            switch (reason)
            {
                case ESMenuTreePageLeaveReason.Navigate:
                    action = "切换页面";
                    break;
                case ESMenuTreePageLeaveReason.RebuildView:
                    action = "局部重建页面";
                    break;
                case ESMenuTreePageLeaveReason.RemoveRuntimePage:
                    action = "移除临时页面";
                    break;
                case ESMenuTreePageLeaveReason.ReplaceRuntimePage:
                    action = "更新临时页面";
                    break;
                default:
                    action = "重建窗口";
                    break;
            }
            int choice = EditorUtility.DisplayDialogComplex(
                "页面包含未提交修改",
                (string.IsNullOrWhiteSpace(summary) ? "当前页面包含尚未提交的修改。" : summary.Trim())
                + "\n\n继续" + action + "前请选择处理方式。",
                "保存并继续",
                "取消",
                "放弃修改");
            if (choice == 1)
                return false;
            if (choice == 2)
            {
                try
                {
                    page.DiscardPendingChanges();
                    UpdateUnsavedChangesState();
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    PublishFeedback(
                        "放弃页面修改失败：" + exception.Message,
                        ESMenuTreePageStatus.Error,
                        ESEditorFeedbackSoundKind.Error,
                        true);
                    return false;
                }
            }

            try
            {
                if (page.TrySavePendingChanges(out string failure))
                {
                    UpdateUnsavedChangesState();
                    return true;
                }
                PublishFeedback(
                    "保存页面修改失败：" + (failure ?? "页面拒绝保存。"),
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                PublishFeedback(
                    "保存页面修改失败：" + exception.Message,
                    ESMenuTreePageStatus.Error,
                    ESEditorFeedbackSoundKind.Error,
                    true);
            }
            return false;
        }

        private void ScheduleNavigationRebuild()
        {
            if (rootVisualElement == null || navigationScroll == null)
                return;

            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = rootVisualElement.schedule
                .Execute(() =>
                {
                    navigationRebuildSchedule = null;
                    RebuildNavigation();
                })
                .StartingIn(75);
        }

        private void DisposeRemovedPages(List<ESMenuTreePage> previousPages)
        {
            HashSet<ESMenuTreePage> current = new HashSet<ESMenuTreePage>(
                registeredPages.Where(node => node?.Page != null).Select(node => node.Page));
            for (int i = 0; i < previousPages.Count; i++)
            {
                ESMenuTreePage previous = previousPages[i];
                if (previous == null || current.Contains(previous))
                    continue;
                if (previous is ESOdinPropertyTreePage previousOdin
                    && current.OfType<ESOdinPropertyTreePage>()
                        .Any(currentOdin => previousOdin.SharesLegacyLifecycleWith(currentOdin)))
                {
                    previousOdin.DisposeForRebuild();
                    continue;
                }
                DisposePage(previous);
            }
        }

        private static void InvalidatePageContexts(IEnumerable<ESMenuTreeBuilder.Node> nodes)
        {
            if (nodes == null)
                return;
            foreach (ESMenuTreeBuilder.Node node in nodes)
                node?.Context?.Invalidate();
        }

        private static bool TryReleasePageView(ESMenuTreePage page, out Exception failure)
        {
            failure = null;
            if (page == null)
                return true;
            try
            {
                page.ReleaseView();
                return true;
            }
            catch (Exception exception)
            {
                failure = exception;
                Debug.LogException(exception);
                return false;
            }
        }

        private static void ReleasePageViewList(IEnumerable<ESMenuTreePage> pages)
        {
            if (pages == null)
                return;
            foreach (ESMenuTreePage page in pages.Where(page => page != null).Distinct())
                TryReleasePageView(page, out _);
        }

        private static void DisposeFailedBuildPages(
            ESMenuTreeBuilder builder,
            List<ESMenuTreePage> previousPages)
        {
            List<ESMenuTreePage> failedPages = builder?.PagesById.Values
                .Where(node => node?.Page != null)
                .Select(node => node.Page)
                .Distinct()
                .ToList() ?? new List<ESMenuTreePage>();
            for (int i = 0; i < failedPages.Count; i++)
            {
                ESMenuTreePage failedPage = failedPages[i];
                if (previousPages.Any(previous => ReferenceEquals(previous, failedPage)))
                    continue;
                if (failedPage is ESOdinPropertyTreePage failedOdin)
                    failedOdin.DisposeForRebuild();
                else
                    DisposePage(failedPage);
            }
            DisposePageList(previousPages);
        }

        private static void DisposePageList(List<ESMenuTreePage> pages)
        {
            for (int i = 0; i < pages.Count; i++)
                DisposePage(pages[i]);
        }

        private static void DisposePage(ESMenuTreePage page)
        {
            if (page == null)
                return;
            try
            {
                page.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void DisposeAllPages()
        {
            if (pagesDisposed)
                return;
            pagesDisposed = true;
            pendingSelectionSchedule?.Pause();
            pendingSelectionSchedule = null;
            pendingSelectionId = null;
            pendingSelectionReveal = false;
            CancelPendingPageViewRebuilds();
            InvalidatePageContexts(registeredPages);
            HideActivePage();
            ReleasePageViewList(pageViews.Keys.ToList());
            DisposePageList(registeredPages
                .Where(node => node?.Page != null)
                .Select(node => node.Page)
                .Distinct()
                .ToList());
            registeredPages.Clear();
            rootNodes.Clear();
            pagesById.Clear();
            pageViews.Clear();
            pageBadgeLabels.Clear();
            pageBadges.Clear();
            ClearRuntimePageRegistrations();
            menuBuilder = null;
        }

        private void ClearRuntimePageRegistrations()
        {
            runtimePageDefinitions.Clear();
            runtimePageOrder.Clear();
        }

        private void OnDisable()
        {
            try
            {
                ESWindow_OnHostDisable();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            openingActivationSchedule?.Pause();
            openingActivationSchedule = null;
            navigationRebuildSchedule?.Pause();
            navigationRebuildSchedule = null;
            ESWindow_SelectionChanged = null;
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
            DisposeAllPages();
            if (ReferenceEquals(UsingWindow, this))
                UsingWindow = null;
        }

        private void PlaceInitialWindow()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width <= 0f || main.height <= 0f)
                return;
            Vector2 minimum = ESWindow_MinSize;
            Vector2 preferred = ESWindow_DefaultSize;
            float margin = Mathf.Min(24f, Mathf.Min(main.width, main.height) * 0.05f);
            float availableWidth = Mathf.Max(1f, main.width - margin * 2f);
            float availableHeight = Mathf.Max(1f, main.height - margin * 2f);
            float width = Mathf.Clamp(preferred.x, Mathf.Min(minimum.x, availableWidth), availableWidth);
            float height = Mathf.Clamp(preferred.y, Mathf.Min(minimum.y, availableHeight), availableHeight);
            position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }
    }

    /// <summary>
    /// Lightweight ES EditorWindow host for one feature page. It reuses the same page lifecycle,
    /// page actions, status feedback, local rebuild, error isolation, and Odin-compatible page
    /// definition as ESMenuTreeWindow without creating navigation or search UI.
    /// </summary>
    public abstract class ESSinglePageWindow<This> : ESMenuTreeWindow<This>
        where This : ESSinglePageWindow<This>
    {
        protected sealed override bool ESWindow_ShowNavigation => false;
        protected virtual string ESWindow_PageStableId => "single.page";
        protected virtual string ESWindow_PageTitle =>
            ESWindow_GetWindowGUIContent()?.text ?? "单页功能面板";
        /// <summary>单页窗口在导航/页签语境中的稳定短标签；为空时回退页面标题。</summary>
        protected virtual string ESWindow_PageNavigationLabel => string.Empty;
        protected virtual string ESWindow_PageKeywords => string.Empty;
        protected virtual ESMenuTreePageLayout ESWindow_PageLayout => ESMenuTreePageLayout.Standard;
        protected virtual float ESWindow_PageMaxContentWidth => 0f;
        protected virtual float ESWindow_PageContentPadding => -1f;
        protected virtual bool ESWindow_UseVerticalScroll => true;

        protected abstract void ESWindow_BuildPageContent(
            ESMenuTreePageContext context,
            VisualElement content);

        protected virtual void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
        }

        protected virtual void ESWindow_OnPageShow(ESMenuTreePageContext context)
        {
        }

        protected virtual void ESWindow_OnPageRefresh(ESMenuTreePageContext context)
        {
        }

        protected virtual void ESWindow_OnPageHide()
        {
        }

        protected virtual void ESWindow_OnPageReleaseView()
        {
        }

        protected virtual void ESWindow_OnPageDispose()
        {
        }

        protected virtual ESMenuTreePageDefinition ESWindow_CreatePageDefinition()
        {
            var page = new ESMenuTreePanelPage(
                    ESWindow_BuildPageContent,
                    ESWindow_UseVerticalScroll)
                .WithOnShow(ESWindow_OnPageShow)
                .WithOnRefresh(ESWindow_OnPageRefresh)
                .WithOnHide(ESWindow_OnPageHide)
                .WithOnReleaseView(ESWindow_OnPageReleaseView)
                .WithOnDispose(ESWindow_OnPageDispose);
            return new ESMenuTreePageDefinition(
                    ESWindow_PageStableId,
                    ESWindow_PageTitle,
                    page)
                .WithLayout(
                    ESWindow_PageLayout,
                    ESWindow_PageMaxContentWidth,
                    ESWindow_PageContentPadding)
                .WithNavigationLabel(ESWindow_PageNavigationLabel)
                .WithKeywords(ESWindow_PageKeywords);
        }

        protected sealed override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            ESMenuTreePageDefinition definition = ESWindow_CreatePageDefinition()
                ?? throw new InvalidOperationException("单页窗口必须返回有效页面定义。");
            var actions = new List<ESMenuTreePageAction>();
            ESWindow_BuildPageActions(actions);
            for (int i = 0; i < actions.Count; i++)
                definition.AddPageAction(actions[i]);
            builder.Add(definition);
        }
    }

    /// <summary>
    /// Migration host for existing IMGUI panels that only need the current ES shell, toolbar,
    /// status, activation motion, local rebuild, and deterministic page disposal. The derived
    /// window keeps ownership of its IMGUI layout and must own its only scroll view.
    /// </summary>
    public abstract class ESSinglePageIMGUIWindow<This> : ESSinglePageWindow<This>
        where This : ESSinglePageIMGUIWindow<This>
    {
        private Exception drawFailure;
        private ESMenuTreePageContext currentPageContext;

        /// <summary>
        /// Current live page context for editor callbacks that occur outside IMGUI drawing,
        /// such as Selection changes or a throttled EditorApplication.update callback.
        /// It becomes null as soon as the page view is released or invalidated.
        /// </summary>
        protected ESMenuTreePageContext ESWindow_CurrentPageContext =>
            currentPageContext != null && currentPageContext.IsAvailable
                ? currentPageContext
                : null;

        protected sealed override bool ESWindow_UseVerticalScroll => false;

        protected sealed override void ESWindow_BuildPageContent(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            drawFailure = null;
            var container = new IMGUIContainer(() => DrawIMGUIPage(context))
            {
                name = "ESSinglePageIMGUI"
            };
            container.style.flexGrow = 1f;
            container.style.flexShrink = 1f;
            container.style.minWidth = 0f;
            container.style.minHeight = 0f;
            content.Add(container);
        }

        protected abstract void ESWindow_DrawIMGUI(ESMenuTreePageContext context);

        private void DrawIMGUIPage(ESMenuTreePageContext context)
        {
            if (context == null || !context.IsAvailable)
            {
                currentPageContext = null;
                return;
            }

            currentPageContext = context;

            if (drawFailure != null)
            {
                EditorGUILayout.HelpBox(
                    "页面绘制失败。\n原因：" + drawFailure.Message
                    + "\n影响：当前页面内容已暂停绘制，窗口外壳与其他窗口不受影响。"
                    + "\n恢复：修复依赖后点击下方按钮重新创建当前页面。",
                    MessageType.Error);
                if (GUILayout.Button("重试页面", GUILayout.Height(26f)))
                {
                    drawFailure = null;
                    context.SetStatus("正在重试页面", ESMenuTreePageStatus.Info);
                    context.RebuildView();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            try
            {
                ESWindow_DrawIMGUI(context);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                drawFailure = exception;
                Debug.LogException(exception);
                context.SetStatus("页面绘制失败：" + exception.Message, ESMenuTreePageStatus.Error);
                GUIUtility.ExitGUI();
            }
        }

        protected sealed override void ESWindow_OnPageReleaseView()
        {
            currentPageContext = null;
            drawFailure = null;
            ESWindow_OnIMGUIPageRelease();
        }

        protected sealed override void ESWindow_OnPageDispose()
        {
            currentPageContext = null;
            drawFailure = null;
            ESWindow_OnIMGUIPageDispose();
        }

        protected virtual void ESWindow_OnIMGUIPageRelease()
        {
        }

        protected virtual void ESWindow_OnIMGUIPageDispose()
        {
        }
    }

    public abstract class ESOdinMenuTreeWindow<This> : OdinMenuEditorWindow,
        IESWindowPresentationMetadata, IESWindowPresentationShortTitle,
        IESWindowPresentationTabLabel,
        ES.EditorInternal.IESWindowSleepRelationshipState where This : ESOdinMenuTreeWindow<This>
    {
        public readonly struct MigrationPage
        {
            public readonly string StableId;
            public readonly string MenuPath;
            public readonly Type ValueType;

            public MigrationPage(string stableId, string menuPath, Type valueType)
            {
                StableId = stableId;
                MenuPath = menuPath;
                ValueType = valueType;
            }
        }

        public static This UsingWindow;
        public static OdinMenuTree menuTree;
        public static Dictionary<string, OdinMenuItem> MenuItems = new Dictionary<string, OdinMenuItem>();
        private static readonly Dictionary<string, OdinMenuItem> MigrationItemsById =
            new Dictionary<string, OdinMenuItem>(StringComparer.Ordinal);
        private static readonly Dictionary<OdinMenuItem, string> MigrationIdsByItem =
            new Dictionary<OdinMenuItem, string>();
        private static readonly HashSet<OdinMenuItem> MigrationItems = new HashSet<OdinMenuItem>();
        private static readonly List<MigrationPage> MigrationPages = new List<MigrationPage>();
        private IVisualElementScheduledItem openingActivationSchedule;
        private bool openingActivationScheduled;
        private string observedMigrationPageId = string.Empty;
        private VisualElement odinSystemActionBar;

        public virtual string ESWindow_PresentationTitle =>
            titleContent?.text ?? "ES窗口";
        public virtual Texture ESWindow_PresentationIcon => titleContent?.image;
        public virtual string ESWindow_PresentationShortTitle =>
            ES.EditorInternal.ESEditorPresentation.BuildDefaultPresentationShortTitle(
                ESWindow_PresentationTitle);
        protected virtual string ESWindow_SemiSleepLabel => string.Empty;
        public virtual string ESWindow_PresentationTabLabel =>
            string.IsNullOrWhiteSpace(ESWindow_SemiSleepLabel)
                ? ESWindow_PresentationShortTitle
                : ESWindow_SemiSleepLabel;
        
        /// <summary>
        /// 收集所有注册的页面，用于统一调用OnPageDisable
        /// </summary>
        private static List<ESWindowPageBase> registeredPages = new List<ESWindowPageBase>();
        
        /// <summary>
        /// 获取当前注册的页面数量（用于调试）
        /// </summary>
        public static int GetRegisteredPageCount()
        {
            return registeredPages?.Count ?? 0;
        }

        public static IReadOnlyList<MigrationPage> GetMigrationPageSnapshot()
        {
            return MigrationPages.ToArray();
        }

        public static bool TrySelectMigrationPage(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)
                || menuTree == null
                || !MigrationItemsById.TryGetValue(stableId, out OdinMenuItem item)
                || item == null)
                return false;
            menuTree.Selection.Clear();
            menuTree.Selection.Add(item);
            item.Select();
            UsingWindow?.Repaint();
            return true;
        }

        public static string GetSelectedMigrationPageId()
        {
            if (menuTree?.Selection == null || menuTree.Selection.Count == 0)
                return string.Empty;
            OdinMenuItem selected = menuTree.Selection[0];
            return selected != null && MigrationIdsByItem.TryGetValue(selected, out string stableId)
                ? stableId
                : string.Empty;
        }

        /// <summary>兼容窗口刷新入口；Odin 菜单由其宿主维护，刷新请求只需重绘当前树。</summary>
        public void ForceMenuTreeRebuild()
        {
            Repaint();
        }

        protected static string CreateMigrationStableId(string windowId, string menuPath)
        {
            if (string.IsNullOrWhiteSpace(windowId))
                throw new ArgumentException("迁移窗口 ID 不能为空。", nameof(windowId));
            string normalized = (menuPath ?? string.Empty).Trim().Replace('\\', '/');
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= 16777619u;
                }
                return windowId + ".legacy." + hash.ToString("x8");
            }
        }

        protected void RegisterMigrationPage(string stableId, string menuPath, OdinMenuItem item)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("迁移页面 StableId 不能为空。", nameof(stableId));
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (MigrationItemsById.ContainsKey(stableId))
                throw new InvalidOperationException("Odin 迁移页面 StableId 重复：" + stableId);
            if (MigrationIdsByItem.ContainsKey(item))
                throw new InvalidOperationException("Odin 迁移菜单项被重复登记：" + (menuPath ?? item.Name));
            MigrationItemsById.Add(stableId, item);
            MigrationIdsByItem.Add(item, stableId);
            MigrationItems.Add(item);
            MigrationPages.Add(new MigrationPage(stableId, menuPath, item.Value?.GetType()));
        }
        
        public virtual GUIContent ESWindow_GetWindowGUIContent()
        {
            var content = new GUIContent("ES窗口", "使用ES工具完成快速开发");
            return content;
        }

        /// <summary>首开窗口的内容下限；页面可按自身内容覆盖，但不再被基类强制最大化。</summary>
        protected virtual Vector2 ESWindow_MinSize => new Vector2(680f, 520f);
        protected virtual Vector2 ESWindow_DefaultSize => new Vector2(1120f, 720f);
        protected virtual float ESWindow_DefaultMenuWidth => 220f;
        protected virtual bool ESWindow_ShowSearchToolbar => true;
        protected virtual bool ESWindow_SupportsSemiSleep => true;
        protected virtual ESWindowSleepLinkMode ESWindow_SleepLinkMode
            => ESWindowSleepLinkMode.Independent;
        protected virtual EditorWindow ESWindow_SleepOwner => null;
        protected virtual string ESWindow_SleepOwnerKey => null;
        [SerializeField] private string serializedSleepOwnerKey = string.Empty;
        [SerializeField] private bool serializedSleepOwnerDetachedByClose;
        [NonSerialized] private EditorWindow explicitSleepOwner;
        protected EditorWindow ESWindow_ExplicitSleepOwner => explicitSleepOwner;
        protected void ESWindow_SetSleepOwnerOverride(EditorWindow owner)
        {
            explicitSleepOwner = owner;
            serializedSleepOwnerDetachedByClose = false;
            if (!string.IsNullOrWhiteSpace(ESWindow_SleepOwnerKey))
                serializedSleepOwnerKey = ESWindow_SleepOwnerKey;
        }

        bool ES.EditorInternal.IESWindowSleepRelationshipState.SleepOwnerDetachedByClose
            => serializedSleepOwnerDetachedByClose;

        void ES.EditorInternal.IESWindowSleepRelationshipState.DetachSleepOwnerAfterOwnerClose()
        {
            explicitSleepOwner = null;
            serializedSleepOwnerDetachedByClose = true;
        }

        private string GetSleepOwnerKey()
        {
            return !string.IsNullOrWhiteSpace(ESWindow_SleepOwnerKey)
                ? ESWindow_SleepOwnerKey
                : serializedSleepOwnerKey;
        }
        protected virtual string ESWindow_MigrationId => typeof(This).FullName ?? typeof(This).Name;
        protected virtual bool ESWindow_RememberMigrationPage => true;

        protected override void Initialize()
        {
            base.Initialize();
        }

        public virtual void ESWindow_OnOpen()
        {

        }
        public static void OpenWindow()
        {
            bool alreadyOpen = HasOpenInstances<This>();
            UsingWindow = GetWindow<This>();
            UsingWindow.ESWindow_OnOpen();
            UsingWindow.titleContent = UsingWindow.ESWindow_GetWindowGUIContent();
            UsingWindow.Show();
            UsingWindow.Focus();
            UsingWindow.minSize = UsingWindow.ESWindow_MinSize;
            if (!alreadyOpen && !UsingWindow.docked)
            {
                UsingWindow.maximized = false;
                UsingWindow.MenuWidth = UsingWindow.ESWindow_DefaultMenuWidth;
                UsingWindow.PlaceInitialWindow();
            }
            UsingWindow.OnClose -= SaveUsingWindowDataOnClose;
            UsingWindow.OnClose += SaveUsingWindowDataOnClose;
        }

        public static void OpenWindow(EditorWindow sleepOwner)
        {
            OpenWindow();
            This window = UsingWindow;
            window.ESWindow_SetSleepOwnerOverride(sleepOwner);
            if (sleepOwner != null && window.ESWindow_SleepLinkMode != ESWindowSleepLinkMode.Independent)
                ESWindowFoundation.SetSleepOwner(window, sleepOwner, window.ESWindow_SleepLinkMode);
            window.ForceMenuTreeRebuild();
        }

        public static void OpenWindow(string stableId)
        {
            OpenWindow();
            if (string.IsNullOrWhiteSpace(stableId))
                return;
            EditorApplication.delayCall += () =>
            {
                if (!TrySelectMigrationPage(stableId))
                    Debug.LogWarning("[ESOdinMenuTreeWindow] 未找到迁移页面：" + stableId);
            };
        }

        private void PlaceInitialWindow()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width <= 0f || main.height <= 0f)
                return;

            Vector2 minimum = ESWindow_MinSize;
            Vector2 preferred = ESWindow_DefaultSize;
            float margin = Mathf.Min(24f, Mathf.Min(main.width, main.height) * 0.05f);
            float availableWidth = Mathf.Max(minimum.x, main.width - margin * 2f);
            float availableHeight = Mathf.Max(minimum.y, main.height - margin * 2f);
            float width = Mathf.Clamp(preferred.x, minimum.x, availableWidth);
            float height = Mathf.Clamp(preferred.y, minimum.y, availableHeight);
            position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }

        private static void SaveUsingWindowDataOnClose()
        {
            UsingWindow?.ES_SaveData();
        }

        protected sealed override OdinMenuTree BuildMenuTree()
        {
            List<ESWindowPageBase> previousPages = new List<ESWindowPageBase>(registeredPages);
            registeredPages.Clear();
            MenuItems.Clear();
            MigrationItemsById.Clear();
            MigrationIdsByItem.Clear();
            MigrationItems.Clear();
            MigrationPages.Clear();
            OdinMenuTree tree = menuTree = new OdinMenuTree();
            tree.Config.DrawSearchToolbar = ESWindow_ShowSearchToolbar;
            tree.DefaultMenuStyle.Height = Mathf.RoundToInt(30f * ES.EditorInternal.ESEditorPresentation.Density);
            tree.DefaultMenuStyle.IconSize = Mathf.RoundToInt(18f * ES.EditorInternal.ESEditorPresentation.Density);
            tree.DefaultMenuStyle.IndentAmount = Mathf.RoundToInt(14f * ES.EditorInternal.ESEditorPresentation.Density);
            tree.DefaultMenuStyle.Borders = true;
            tree.DefaultMenuStyle.BorderAlpha = 0.18f;
            tree.DefaultMenuStyle.SetSelectedColorDarkSkin(
                ES.EditorInternal.ESEditorPresentation.GetSelectionFill(true));
            tree.DefaultMenuStyle.SetSelectedColorLightSkin(
                ES.EditorInternal.ESEditorPresentation.GetSelectionFill(false));
            try
            {
                ES_OnBuildMenuTree(tree);
                ES_LoadData();
                CaptureUnregisteredMigrationPages(tree);
                RestoreRememberedMigrationPage();
            }
            finally
            {
                ReleaseUnregisteredPages(previousPages);
            }
            return tree;
        }

        private void CaptureUnregisteredMigrationPages(OdinMenuTree tree)
        {
            foreach (OdinMenuItem item in tree.EnumerateTree())
            {
                if (item == null || item.Value == null || MigrationItems.Contains(item))
                    continue;
                string path = item.GetFullPath() ?? item.Name ?? string.Empty;
                RegisterMigrationPage(
                    CreateMigrationStableId(ESWindow_MigrationId, path),
                    path,
                    item);
            }
        }

        private void RestoreRememberedMigrationPage()
        {
            observedMigrationPageId = string.Empty;
            if (!ESWindow_RememberMigrationPage || MigrationItemsById.Count == 0)
                return;
            string key = GetMigrationPagePreferenceKey();
            string remembered = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(remembered))
                return;
            if (TrySelectMigrationPage(remembered))
            {
                observedMigrationPageId = remembered;
                return;
            }
            EditorPrefs.DeleteKey(key);
        }

        private void RememberSelectedMigrationPage()
        {
            if (!ESWindow_RememberMigrationPage)
                return;
            string selected = GetSelectedMigrationPageId();
            if (string.IsNullOrWhiteSpace(selected)
                || string.Equals(selected, observedMigrationPageId, StringComparison.Ordinal))
                return;
            observedMigrationPageId = selected;
            string key = GetMigrationPagePreferenceKey();
            if (!string.Equals(EditorPrefs.GetString(key, string.Empty), selected, StringComparison.Ordinal))
                EditorPrefs.SetString(key, selected);
        }

        private string GetMigrationPagePreferenceKey()
        {
            string project = (Application.dataPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
            string typeName = (GetType().FullName ?? GetType().Name)
                              + "|" + GetType().Assembly.GetName().Name;
            return "ES.MenuTree.LastPage." + Hash128.Compute(project + "|" + typeName);
        }

        private static void ReleaseUnregisteredPages(List<ESWindowPageBase> previousPages)
        {
            for (int i = 0; i < previousPages.Count; i++)
            {
                ESWindowPageBase page = previousPages[i];
                if (page == null || registeredPages.Contains(page))
                    continue;
                try
                {
                    page.OnPageDisable();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ESOdinMenuTreeWindow] 页面 {page.GetType().Name} 重建释放失败: {exception.Message}");
                }
            }
        }
        protected virtual void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
 
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, SdfIconType sdfIcon) where P : ESWindowPageBase, new()
        {
            // Odin 的 Add("父/子", obj) 会返回多个菜单项，最后一个才是真正绑定页面的叶子节点
            MenuItems[name] = tree.Add(name, (page ??= new P()), sdfIcon).Last();
            page.ES_Refresh();
            
            // 注册页面到列表，用于窗口关闭时统一调用OnPageDisable
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, Texture texture) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), texture).Last();
            page.ES_Refresh();
            
            // 注册页面到列表
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, EditorIcon icon) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), icon).Last();
            page.ES_Refresh();
            
            // 注册页面到列表
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（用于动态创建的页面）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, SdfIconType icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESOdinMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表，用于窗口关闭时统一调用OnPageDisable
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESOdinMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（Texture重载）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, Texture icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESOdinMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESOdinMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（EditorIcon重载）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, EditorIcon icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESOdinMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESOdinMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            UsingWindow = this as This;
            if (docked)
                rootVisualElement.RemoveFromClassList(
                    ES.EditorInternal.ESWindowFrameActivation.NativeFrameClass);
            else
                rootVisualElement.AddToClassList(
                    ES.EditorInternal.ESWindowFrameActivation.NativeFrameClass);
            bool supportsIndependentSemiSleep = ESWindow_SupportsSemiSleep
                && ESWindow_SleepLinkMode != ESWindowSleepLinkMode.OwnedSurface;
            if (supportsIndependentSemiSleep)
            {
                ESWindowFoundation.BindWithStandardSystemHost(
                    this,
                    EnsureOdinSystemActionBar(),
                    allowSemiSleep: true);
            }
            else
            {
                odinSystemActionBar?.RemoveFromHierarchy();
                odinSystemActionBar = null;
                ES.EditorInternal.ESEditorPresentation.BindWindow(
                    this,
                    allowSemiSleep: ESWindow_SupportsSemiSleep);
            }
            if (!serializedSleepOwnerDetachedByClose
                && ESWindow_SleepLinkMode != ESWindowSleepLinkMode.Independent)
            {
                EditorWindow owner = ESWindow_ExplicitSleepOwner ?? ESWindow_SleepOwner;
                if (owner != null)
                    ESWindowFoundation.SetSleepOwner(this, owner, ESWindow_SleepLinkMode);
                else if (ESWindow_SleepLinkMode == ESWindowSleepLinkMode.FollowOwner)
                {
                    string ownerKey = GetSleepOwnerKey();
                    if (!ESWindowFoundation.RegisterPendingSleepOwner(
                        this,
                        ownerKey,
                        ESWindow_SleepLinkMode))
                        Debug.LogError("ES FollowOwner 窗口必须声明稳定 ESWindow_SleepOwnerKey。窗口：" + GetType().FullName);
                }
            }
            ScheduleOpeningActivation();
        }

        private VisualElement EnsureOdinSystemActionBar()
        {
            if (odinSystemActionBar != null && odinSystemActionBar.parent != null)
                return odinSystemActionBar;

            odinSystemActionBar = rootVisualElement.Q<VisualElement>(
                "ESOdinStandardSystemActionBar");
            if (odinSystemActionBar != null)
                return odinSystemActionBar;

            odinSystemActionBar = new VisualElement
            {
                name = "ESOdinStandardSystemActionBar",
                tooltip = "ES 系统动作：窗口休眠、自动模式与全局策略"
            };
            odinSystemActionBar.style.height = 28f;
            odinSystemActionBar.style.minHeight = 28f;
            odinSystemActionBar.style.flexDirection = FlexDirection.Row;
            odinSystemActionBar.style.alignItems = Align.Center;
            odinSystemActionBar.style.justifyContent = Justify.FlexEnd;
            odinSystemActionBar.style.flexGrow = 1f;
            odinSystemActionBar.style.flexShrink = 1f;
            odinSystemActionBar.style.minWidth = 0f;
            odinSystemActionBar.style.overflow = Overflow.Hidden;
            odinSystemActionBar.style.paddingRight = 4f;
            odinSystemActionBar.style.borderBottomWidth = 1f;
            odinSystemActionBar.style.borderBottomColor =
                ES.EditorInternal.ESEditorPresentation.DividerColor;
            rootVisualElement.Insert(0, odinSystemActionBar);
            return odinSystemActionBar;
        }

        protected void QuickBuildMigrationRootMenu<P>(
            OdinMenuTree tree,
            string windowId,
            string stableId,
            string name,
            ref P page,
            SdfIconType icon) where P : ESWindowPageBase, new()
        {
            QuickBuildRootMenu(tree, name, ref page, icon);
            RegisterMigrationPage(
                string.IsNullOrWhiteSpace(stableId) ? CreateMigrationStableId(windowId, name) : stableId,
                name,
                MenuItems[name]);
        }

        private void ScheduleOpeningActivation()
        {
            if (openingActivationScheduled || rootVisualElement == null)
                return;
            openingActivationScheduled = true;
            openingActivationSchedule = rootVisualElement.schedule.Execute(() =>
            {
                openingActivationSchedule = null;
                if (this == null || docked || rootVisualElement.panel == null)
                    return;
                ES.EditorInternal.ESWindowFrameActivation.Play(this, position);
            }).StartingIn(16);
        }

        protected override void OnImGUI()
        {
            if (UsingWindow == null)
            {
                UsingWindow = this as This;
            }
            if (ESWindow_SupportsSemiSleep
                && ESWindow_SleepLinkMode != ESWindowSleepLinkMode.OwnedSurface
                && (odinSystemActionBar == null || odinSystemActionBar.parent == null))
            {
                ESWindowFoundation.BindWithStandardSystemHost(
                    this,
                    EnsureOdinSystemActionBar(),
                    allowSemiSleep: true);
            }
            RememberSelectedMigrationPage();
            base.OnImGUI();
        }

        protected override void OnDisable()
        {
            openingActivationSchedule?.Pause();
            openingActivationSchedule = null;
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
            base.OnDisable();
        }
        public static void ES_RefreshWindow()
        {
            if (UsingWindow == null) OpenWindow();
            UsingWindow.ESWindow_RefreshWindow();
        }
        public virtual void ESWindow_RefreshWindow()
        {
            ES_SaveData();
            this.ForceMenuTreeRebuild();
            ES_LoadData();
        }
        public virtual void ES_LoadData()
        {

        }
        public virtual void ES_SaveData()
        {

        }
        
        /// <summary>
        /// 窗口销毁时统一调用所有注册页面的OnPageDisable
        /// </summary>
        protected override void OnDestroy()
        {
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
            // Debug.Log($"[ESOdinMenuTreeWindow] 窗口销毁，开始调用 {registeredPages.Count} 个页面的OnPageDisable");
            
            int callCount = 0;
            foreach (var page in registeredPages)
            {
                if (page != null)
                {
                    try
                    {
                        page.OnPageDisable();
                        callCount++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[ESOdinMenuTreeWindow] 页面 {page.GetType().Name} 的OnPageDisable调用失败: {e.Message}");
                    }
                }
            }
            
            // Debug.Log($"[ESOdinMenuTreeWindow] OnPageDisable调用完成，成功调用 {callCount}/{registeredPages.Count} 个页面");
            
            // 清理列表
            registeredPages.Clear();
            MenuItems.Clear();
            MigrationItemsById.Clear();
            MigrationIdsByItem.Clear();
            MigrationItems.Clear();
            MigrationPages.Clear();
            menuTree = null;
            if (ReferenceEquals(UsingWindow, this))
                UsingWindow = null;
            OnClose -= SaveUsingWindowDataOnClose;
        }
    }

    [Serializable]
    public abstract class ESWindowPageBase
    {
        public virtual ESWindowPageBase ES_Refresh()
        {
            return this;
        }
        
        /// <summary>
        /// 窗口关闭或页面销毁时调用，用于清理资源和保存数据
        /// </summary>
        public virtual void OnPageDisable()
        {
            // 子类可重写此方法进行清理工作
        }
    }

}
