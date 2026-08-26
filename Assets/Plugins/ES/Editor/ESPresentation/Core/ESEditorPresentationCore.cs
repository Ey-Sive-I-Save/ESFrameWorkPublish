using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES.MenuTree.Editor.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES_Logic.Editor")]

namespace ES
{
    public enum ESWindowVisualState : byte
    {
        ActivePanel,
        SleepTile,
        EdgeTab,
        EdgeTabHover
    }

    /// <summary>窗口半休眠的生命周期归属。关系只存在于当前 Editor 域，不持久化窗口引用。</summary>
    public enum ESWindowSleepLinkMode : byte
    {
        Independent,
        FollowOwner,
        OwnedSurface
    }

    public enum ESWindowActionScope : byte
    {
        System,
        Global,
        Window
    }

    /// <summary>
    /// 窗口参与 ES Presentation 的生命周期分类。Full 窗口拥有完整休眠状态机；
    /// Transient 窗口仍可接入解绑与生命周期清理，但不得拥有独立休眠状态。
    /// </summary>
    public enum ESWindowSleepMode : byte
    {
        Full,
        Transient
    }

    /// <summary>
    /// ES EditorWindow 的显式界面语义。休眠能力由 ESWindowSleepMode 表达；
    /// 此分类用于约束创建入口、owner 和关闭合同，不从类型名推断。
    /// </summary>
    public enum ESWindowSurfaceKind : byte
    {
        Unknown,
        Workspace,
        Inspector,
        Popup,
        Dialog,
        Preview,
        Utility
    }

    /// <summary>
    /// ES EditorWindow 的显式生命周期准入合同。未声明合同的 Unity/第三方窗口
    /// 不得接入 ES Presentation；长期窗口声明 Full，短生命周期窗口声明 Transient，
    /// 并显式登记独立于类型命名的 SurfaceKind。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ESWindowSleepContractAttribute : Attribute
    {
        public ESWindowSleepContractAttribute(
            ESWindowSleepMode mode,
            ESWindowSurfaceKind surfaceKind,
            string reason = null)
        {
            Mode = mode;
            SurfaceKind = surfaceKind;
            Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        }

        public ESWindowSleepMode Mode { get; }
        public ESWindowSurfaceKind SurfaceKind { get; }
        public string Reason { get; }
    }

    /// <summary>可选的 ES 窗口展示元数据。只返回当前页面的轻量标题与图标。</summary>
    public interface IESWindowPresentationMetadata
    {
        string ESWindow_PresentationTitle { get; }
        Texture ESWindow_PresentationIcon { get; }
    }

    /// <summary>
    /// 可选的半休眠页签短标题。窗口可以为自己声明一个稳定、可扫描的短文本，
    /// 例如“世界”或“工作台”；未声明时由基础层从完整标题生成保守回退值。
    /// </summary>
    public interface IESWindowPresentationShortTitle
    {
        string ESWindow_PresentationShortTitle { get; }
    }

    /// <summary>
    /// 声明窗口在半休眠页签上的稳定短标签。它是 IESWindowPresentationShortTitle
    /// 的低摩擦替代方案，适合不需要额外逻辑的窗口；显式接口实现仍优先。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ESWindowPresentationShortTitleAttribute : Attribute
    {
        public ESWindowPresentationShortTitleAttribute(string title)
        {
            Title = title ?? string.Empty;
        }

        public string Title { get; }
    }

    /// <summary>
    /// 供窗口基类使用的低摩擦页签标签契约。它只影响半休眠页签/方块上的可见文案，
    /// 不改变窗口标题、菜单路径或稳定身份；为空时继续使用默认语义推导。
    /// </summary>
    public interface IESWindowPresentationTabLabel
    {
        string ESWindow_PresentationTabLabel { get; }
    }

    /// <summary>
    /// 同一具体 EditorWindow 类型确需并行时的显式例外合同。
    /// 协调器负责数量上限、稳定业务身份、关闭与 ReloadDomain 收口。
    /// </summary>
    public interface IESWindowMultiInstanceContract
    {
        string ESWindow_MultiInstanceCoordinatorId { get; }
    }

    /// <summary>
    /// ES 窗口三域动作宿主。标准 ES 窗口由基类创建宿主并传入此合同；
    /// 派生窗口只追加动作。基础层校验归属，并向 System 域接入窗口生命周期动作。
    /// </summary>
    public sealed class ESWindowActionHosts
    {
        public VisualElement System { get; }
        public VisualElement Global { get; }
        public VisualElement Window { get; }

        public ESWindowActionHosts(
            VisualElement system = null,
            VisualElement global = null,
            VisualElement window = null)
        {
            System = system;
            Global = global;
            Window = window;
        }

        public VisualElement Get(ESWindowActionScope scope)
        {
            switch (scope)
            {
                case ESWindowActionScope.System: return System;
                case ESWindowActionScope.Global: return Global;
                case ESWindowActionScope.Window: return Window;
                default: throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
        }

        public T Add<T>(ESWindowActionScope scope, T element) where T : VisualElement
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            VisualElement host = Get(scope)
                ?? throw new InvalidOperationException("当前窗口没有声明 " + scope + " 动作宿主。");
            host.Add(element);
            return element;
        }

        public Button AddButton(
            ESWindowActionScope scope,
            string text,
            string tooltip,
            Action action)
        {
            return AddButton(scope, null, text, tooltip, action);
        }

        public Button AddButton(
            ESWindowActionScope scope,
            Texture icon,
            string text,
            string tooltip,
            Action action)
        {
            Button button = EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                icon,
                text,
                tooltip,
                action);
            return Add(scope, button);
        }

        internal void ValidateOwnership(VisualElement root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            ValidateDistinctScopes();
            ValidateHostOwnership(root, System, nameof(System));
            ValidateHostOwnership(root, Global, nameof(Global));
            ValidateHostOwnership(root, Window, nameof(Window));
        }

        private void ValidateDistinctScopes()
        {
            ValidateDistinctScopes(System, nameof(System), Global, nameof(Global));
            ValidateDistinctScopes(System, nameof(System), Window, nameof(Window));
            ValidateDistinctScopes(Global, nameof(Global), Window, nameof(Window));
        }

        private static void ValidateDistinctScopes(
            VisualElement first,
            string firstName,
            VisualElement second,
            string secondName)
        {
            if (first != null && ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    "ESWindowActionHosts." + firstName + " 与 " + secondName
                    + " 不能复用同一个动作宿主；System、Global、Window 必须各自拥有布局位置。");
            }
        }

        private static void ValidateHostOwnership(
            VisualElement root,
            VisualElement host,
            string hostName)
        {
            if (host == null)
                return;
            for (VisualElement current = host; current != null; current = current.parent)
                if (current == root)
                    return;
            throw new InvalidOperationException(
                "ESWindowActionHosts." + hostName + " 必须属于当前 EditorWindow.rootVisualElement。");
        }
    }

    /// <summary>所有 ES EditorWindow 接入共享 Presentation 与三域动作合同的公开入口。</summary>
    public static class ESWindowFoundation
    {
        internal const string StandardSystemActionHostName = "ESWindowStandardSystemActionHost";

        public static void Bind(
            EditorWindow window,
            ESWindowActionHosts actionHosts = null,
            bool allowSemiSleep = true)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            ValidateDeclaredSleepContract(window, allowSemiSleep);
            EditorInternal.ESEditorPresentation.BindWindow(window, allowSemiSleep, actionHosts);
        }

        /// <summary>显式绑定完整休眠窗口。</summary>
        public static void BindFullSleep(
            EditorWindow window,
            ESWindowActionHosts actionHosts = null)
        {
            Bind(window, actionHosts, true);
        }

        /// <summary>显式绑定短生命周期窗口；仍保留解绑与恢复清理，但不启用独立休眠。</summary>
        public static void BindTransient(
            EditorWindow window,
            ESWindowActionHosts actionHosts = null)
        {
            Bind(window, actionHosts, false);
        }

        public static ESWindowSleepMode? GetDeclaredSleepMode(EditorWindow window)
        {
            if (window == null)
                return null;
            ESWindowSleepContractAttribute contract =
                (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                    window.GetType(),
                    typeof(ESWindowSleepContractAttribute),
                    true);
            return contract?.Mode;
        }

        public static ESWindowSurfaceKind? GetDeclaredSurfaceKind(EditorWindow window)
        {
            if (window == null)
                return null;
            ESWindowSleepContractAttribute contract =
                (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                    window.GetType(),
                    typeof(ESWindowSleepContractAttribute),
                    true);
            return contract?.SurfaceKind;
        }

        internal static void ValidateDeclaredSleepContract(
            EditorWindow window,
            bool allowSemiSleep)
        {
            ESWindowSleepContractAttribute contract =
                GetValidatedSleepContract(window);
            ESWindowSleepMode declared = contract.Mode;
            bool expectedFull = declared == ESWindowSleepMode.Full;
            if (expectedFull == allowSemiSleep)
                return;

            string reason = contract?.Reason;
            throw new InvalidOperationException(
                "ES 窗口休眠合同与绑定模式不一致：" + window.GetType().FullName
                + "，声明=" + declared
                + "，绑定=" + (allowSemiSleep ? "Full" : "Transient")
                + (string.IsNullOrEmpty(reason) ? string.Empty : "，原因=" + reason));
        }

        private static ESWindowSleepContractAttribute GetValidatedSleepContract(
            EditorWindow window)
        {
            ESWindowSleepContractAttribute contract = GetRequiredSleepContract(window);
            if (contract.Mode != ESWindowSleepMode.Full
                && contract.Mode != ESWindowSleepMode.Transient)
            {
                throw new InvalidOperationException(
                    "ES 窗口休眠合同使用了未知模式：" + window.GetType().FullName);
            }
            ValidateDeclaredSurfaceKind(window, contract);
            if (contract.Mode == ESWindowSleepMode.Transient
                && string.IsNullOrWhiteSpace(contract.Reason))
            {
                throw new InvalidOperationException(
                    "Transient ES 窗口必须登记不参与独立休眠的原因："
                    + window.GetType().FullName);
            }
            return contract;
        }

        private static void ValidateDeclaredSurfaceKind(
            EditorWindow window,
            ESWindowSleepContractAttribute contract)
        {
            ESWindowSurfaceKind surfaceKind = contract.SurfaceKind;
            if (surfaceKind == ESWindowSurfaceKind.Unknown
                || !Enum.IsDefined(typeof(ESWindowSurfaceKind), surfaceKind))
            {
                throw new InvalidOperationException(
                    "ES 窗口必须声明已知 SurfaceKind：" + window.GetType().FullName);
            }

            bool requiresTransient = surfaceKind == ESWindowSurfaceKind.Popup
                || surfaceKind == ESWindowSurfaceKind.Dialog
                || surfaceKind == ESWindowSurfaceKind.Utility;
            bool requiresFull = surfaceKind == ESWindowSurfaceKind.Workspace
                || surfaceKind == ESWindowSurfaceKind.Inspector
                || surfaceKind == ESWindowSurfaceKind.Preview;
            if ((requiresTransient && contract.Mode != ESWindowSleepMode.Transient)
                || (requiresFull && contract.Mode != ESWindowSleepMode.Full))
            {
                throw new InvalidOperationException(
                    "ES 窗口 SurfaceKind 与休眠模式不一致：" + window.GetType().FullName
                    + "，SurfaceKind=" + surfaceKind
                    + "，Mode=" + contract.Mode);
            }

            if (surfaceKind == ESWindowSurfaceKind.Dialog
                && window.GetType() != typeof(ESAdvancedDialogWindow))
            {
                throw new InvalidOperationException(
                    "生产 Dialog 只能由 ESDialogService 管理的 ESAdvancedDialogWindow 承载："
                    + window.GetType().FullName);
            }
        }

        internal static void ValidateFullLifecycleSurfaceCapability(
            EditorWindow window,
            string capability)
        {
            ESWindowSleepContractAttribute contract = GetValidatedSleepContract(window);
            bool supportedSurface = contract.SurfaceKind == ESWindowSurfaceKind.Workspace
                || contract.SurfaceKind == ESWindowSurfaceKind.Inspector
                || contract.SurfaceKind == ESWindowSurfaceKind.Preview;
            if (contract.Mode == ESWindowSleepMode.Full && supportedSurface)
                return;

            throw new InvalidOperationException(
                (string.IsNullOrWhiteSpace(capability) ? "该 ES 全局能力" : capability)
                + " 仅允许 Full + Workspace/Inspector/Preview 窗口接入："
                + window.GetType().FullName
                + "，Mode=" + contract.Mode
                + "，SurfaceKind=" + contract.SurfaceKind);
        }

        private static ESWindowSleepContractAttribute GetRequiredSleepContract(
            EditorWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            ESWindowSleepContractAttribute contract =
                (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                    window.GetType(),
                    typeof(ESWindowSleepContractAttribute),
                    true);
            if (contract == null)
            {
                throw new InvalidOperationException(
                    "只有显式声明 ESWindowSleepContract 的 ES 窗口才能接入 ES Presentation："
                    + window.GetType().FullName);
            }
            return contract;
        }

        /// <summary>
        /// 在调用方明确指定的工具栏中创建或复用标准 System 宿主并完成绑定。
        /// 该入口不猜测标题栏、不绝对定位，也不会接管 Global/Window 业务动作布局。
        /// </summary>
        public static ESWindowActionHosts BindWithStandardSystemHost(
            EditorWindow window,
            VisualElement systemActionBar,
            VisualElement globalActionHost = null,
            VisualElement windowActionHost = null,
            bool allowSemiSleep = true)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            if (systemActionBar == null)
                throw new ArgumentNullException(nameof(systemActionBar));

            ValidateFullLifecycleSurfaceCapability(window, "标准 System 动作宿主");
            ValidateDeclaredSleepContract(window, allowSemiSleep);
            ValidateActionBarOwnership(window.rootVisualElement, systemActionBar);
            VisualElement existingSystemHost =
                systemActionBar.Q<VisualElement>(StandardSystemActionHostName);
            new ESWindowActionHosts(
                    existingSystemHost,
                    globalActionHost,
                    windowActionHost)
                .ValidateOwnership(window.rootVisualElement);
            VisualElement systemHost = EnsureStandardSystemActionHost(systemActionBar);

            var hosts = new ESWindowActionHosts(
                systemHost,
                globalActionHost,
                windowActionHost);
            Bind(window, hosts, allowSemiSleep);
            return hosts;
        }

        /// <summary>
        /// Creates or reuses a normal-flow System action bar owned by the window
        /// root. This is the explicit host entry point for long-lived IMGUI
        /// windows; it never uses absolute positioning or a hidden fallback.
        /// </summary>
        public static VisualElement EnsureStandardSystemActionBar(
            EditorWindow window,
            string name = null)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            ValidateFullLifecycleSurfaceCapability(window, "标准 System 动作栏");
            VisualElement root = window.rootVisualElement
                ?? throw new InvalidOperationException("EditorWindow 尚未提供 rootVisualElement。");
            string resolvedName = string.IsNullOrWhiteSpace(name)
                ? "ESWindowStandardSystemActionBar"
                : name.Trim();
            VisualElement bar = root.Q<VisualElement>(resolvedName);
            if (bar == null)
            {
                bar = new Toolbar { name = resolvedName };
                bar.tooltip = "ES 系统动作：窗口生命周期与休眠控制";
                bar.AddToClassList("es-window-explicit-system-actions");
                bar.style.flexShrink = 0f;
                bar.style.minWidth = 0f;
                bar.style.minHeight = 28f;
                bar.style.flexDirection = FlexDirection.Row;
                bar.style.flexWrap = Wrap.Wrap;
                bar.style.alignItems = Align.Center;
                bar.style.justifyContent = Justify.FlexEnd;
                bar.style.paddingLeft = 8f;
                bar.style.paddingRight = 8f;
                bar.style.paddingTop = 3f;
                bar.style.paddingBottom = 3f;
                EditorInternal.ESEditorPresentation.ApplyPresentationStyle(
                    bar,
                    EditorInternal.ESEditorPresentation.ESPresentationRole.Toolbar,
                    radius: EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                borderWidth: 1f);
                root.Insert(0, bar);
            }
            else if (bar.parent == null)
            {
                root.Insert(0, bar);
            }
            EnsureStandardSystemActionHost(bar);
            return bar;
        }

        private static VisualElement EnsureStandardSystemActionHost(VisualElement bar)
        {
            VisualElement host = bar?.Q<VisualElement>(StandardSystemActionHostName);
            if (host != null)
                return host;
            host = new VisualElement
            {
                name = StandardSystemActionHostName,
                tooltip = "ES 系统动作：窗口生命周期与休眠控制"
            };
            host.style.flexDirection = FlexDirection.Row;
            host.style.alignItems = Align.Center;
            host.style.flexGrow = 1f;
            host.style.flexShrink = 1f;
            host.style.minWidth = 0f;
            host.style.overflow = Overflow.Hidden;
            bar.Add(host);
            return host;
        }

        private static void ValidateActionBarOwnership(
            VisualElement root,
            VisualElement actionBar)
        {
            for (VisualElement current = actionBar; current != null; current = current.parent)
                if (current == root)
                    return;
            throw new InvalidOperationException(
                "标准 System 动作栏必须属于当前 EditorWindow.rootVisualElement。");
        }

        /// <summary>
        /// 为同一窗口实例的内容重建解除当前 VisualTree 绑定。该操作保留其他窗口
        /// 指向此窗口的 FollowOwner 关系；真实销毁必须使用 Close。
        /// </summary>
        public static void Unbind(EditorWindow window)
        {
            EditorInternal.ESEditorPresentation.SuspendWindow(window);
        }

        /// <summary>
        /// 暂停窗口的 Presentation 绑定，用于 OnDisable、布局重建、PlayMode 和
        /// ReloadDomain 边界。保留当前域内的绑定槽与 owner 关系，供后续 Bind 恢复。
        /// </summary>
        public static void Suspend(EditorWindow window)
        {
            EditorInternal.ESEditorPresentation.SuspendWindow(window);
        }

        /// <summary>
        /// 结束窗口生命周期，用于 OnDestroy。该操作永久解除子窗口关系并释放全部引用。
        /// </summary>
        public static void Close(EditorWindow window)
        {
            EditorInternal.ESEditorPresentation.UnbindWindow(window, true);
        }

        [Obsolete("Use Unbind(window) for VisualTree rebuilds, Suspend(window) for OnDisable, or Close(window) for OnDestroy.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void Unbind(EditorWindow window, bool windowClosing)
        {
            EditorInternal.ESEditorPresentation.UnbindWindow(window, windowClosing);
        }

        /// <summary>查询窗口是否已经显式接入 ES Presentation；不会隐式绑定原生或第三方窗口。</summary>
        public static bool IsBound(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.IsWindowBound(window);
        }

        public static bool Sleep(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.RequestWindowSemiSleep(window);
        }

        public static bool Wake(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.RequestWindowWake(window);
        }

        public static ESWindowVisualState GetVisualState(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.GetWindowVisualState(window);
        }

        public static bool IsWindowSemiSleepAllowed(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.IsWindowSemiSleepAllowed(window);
        }

        /// <summary>查询窗口契约是否支持独立半休眠。</summary>
        public static bool IsWindowSleepSupported(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.IsWindowSemiSleepSupported(window);
        }

        /// <summary>
        /// 查询窗口类型是否违反默认的单实例边界。未显式实现
        /// IESWindowMultiInstanceContract 的具体类型默认只能有一个活动实例。
        /// </summary>
        public static bool IsWindowSingleInstanceViolation(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.IsWindowSingleInstanceViolation(window);
        }

        /// <summary>查询窗口此刻是否满足立即休眠的硬条件，不受全局自动开关影响。</summary>
        public static bool CanWindowSleep(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.CanWindowEnterSemiSleep(window);
        }

        /// <summary>
        /// 返回阻止窗口立即休眠的硬资格原因；空字符串表示满足硬条件。
        /// automatic=true 时额外检查全局自动开关与窗口级自动模式，
        /// 不包含焦点、指针悬停或剩余等待时间等瞬时状态。
        /// </summary>
        public static string GetWindowSleepBlockReason(
            EditorWindow window,
            bool automatic = false)
        {
            return EditorInternal.ESEditorPresentation.GetWindowSemiSleepBlockReason(
                window,
                automatic);
        }

        /// <summary>设置窗口是否参与半休眠；窗口未绑定或不支持时返回 false。</summary>
        public static bool TrySetWindowSleepAllowed(EditorWindow window, bool allowed)
        {
            if (!IsWindowSleepSupported(window))
                return false;
            EditorInternal.ESEditorPresentation.SetWindowSemiSleepAllowed(window, allowed);
            return true;
        }

        /// <summary>
        /// 设置窗口级自动休眠模式。该设置不会修改全局自动半休眠开关。
        /// 窗口未绑定或不支持时返回 false。
        /// </summary>
        public static bool TrySetWindowAutoSleepEnabled(EditorWindow window, bool enabled)
        {
            if (!IsWindowSleepSupported(window))
                return false;
            EditorInternal.ESEditorPresentation.SetWindowPinned(window, !enabled);
            return true;
        }

        /// <summary>查询窗口级自动休眠模式；全局自动开关需单独查询。</summary>
        public static bool IsWindowAutoSleepEnabled(EditorWindow window)
        {
            return IsWindowSleepSupported(window)
                && !EditorInternal.ESEditorPresentation.IsWindowPinned(window);
        }

        /// <summary>读取半休眠页签短标题；持久化覆盖优先，其次使用窗口契约和默认推导。</summary>
        public static string GetPresentationShortTitle(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.GetWindowPresentationShortTitle(window);
        }

        /// <summary>
        /// 设置当前窗口类型的页签短标题。传入空值会清除覆盖并恢复默认推导，
        /// 只保存字符串，不保存窗口引用；窗口尚未显式绑定时返回 false。
        /// </summary>
        public static bool TrySetPresentationShortTitle(EditorWindow window, string shortTitle)
        {
            return EditorInternal.ESEditorPresentation.TrySetWindowPresentationShortTitle(window, shortTitle);
        }

        /// <summary>
        /// 读取全局自动半休眠开关。窗口级“自动”只表示参与资格，真正的自动策略由此开关统一控制。
        /// </summary>
        public static bool IsGlobalSemiSleepEnabled
        {
            get { return EditorInternal.ESEditorPresentation.SemiSleepEnabled; }
        }

        /// <summary>
        /// 设置全局自动半休眠开关；只更新稳定偏好和已绑定窗口的控件，不重建窗口内容。
        /// </summary>
        public static void SetGlobalSemiSleepEnabled(bool enabled)
        {
            EditorInternal.ESEditorPresentation.SetSemiSleepEnabled(enabled);
        }

        public static IDisposable HoldInteraction(EditorWindow window, string reason = null)
        {
            return EditorInternal.ESEditorPresentation.BeginWindowInteractionHold(window, reason);
        }

        /// <summary>
        /// 为辅助窗口设置明确的休眠归属。FollowOwner 会同步主窗口的进入/退出休眠；
        /// OwnedSurface 表示内容应由宿主窗口承载，不生成独立休眠控件。
        /// </summary>
        public static bool SetSleepOwner(
            EditorWindow child,
            EditorWindow owner,
            ESWindowSleepLinkMode mode = ESWindowSleepLinkMode.FollowOwner)
        {
            if (child == null)
                return false;
            GetValidatedSleepContract(child);
            switch (mode)
            {
                case ESWindowSleepLinkMode.Independent:
                    if (owner != null)
                        return false;
                    break;
                case ESWindowSleepLinkMode.FollowOwner:
                case ESWindowSleepLinkMode.OwnedSurface:
                    if (owner == null || child == owner)
                        return false;
                    ValidateFullLifecycleSurfaceCapability(child, mode + " 子窗口关系");
                    ValidateFullLifecycleSurfaceCapability(owner, mode + " owner 关系");
                    break;
                default:
                    return false;
            }
            return EditorInternal.ESEditorPresentation.SetWindowSleepOwner(child, owner, mode);
        }

        public static void ClearSleepOwner(EditorWindow child)
        {
            EditorInternal.ESEditorPresentation.ClearWindowSleepOwner(child);
        }

        public static ESWindowSleepLinkMode GetSleepLinkMode(EditorWindow window)
        {
            return EditorInternal.ESEditorPresentation.GetWindowSleepLinkMode(window);
        }

        public static bool RegisterPendingSleepOwner(
            EditorWindow child,
            string ownerKey,
            ESWindowSleepLinkMode mode = ESWindowSleepLinkMode.FollowOwner)
        {
            if (child == null)
                return false;
            GetValidatedSleepContract(child);
            if (mode == ESWindowSleepLinkMode.FollowOwner)
                ValidateFullLifecycleSurfaceCapability(child, "Pending FollowOwner 子窗口关系");
            return EditorInternal.ESEditorPresentation.RegisterPendingSleepOwner(child, ownerKey, mode);
        }

        public static int ResolvePendingSleepOwners(string ownerKey, EditorWindow owner)
        {
            if (owner == null)
                return 0;
            ValidateFullLifecycleSurfaceCapability(owner, "Pending FollowOwner owner 关系");
            return EditorInternal.ESEditorPresentation.ResolvePendingSleepOwners(ownerKey, owner);
        }

        public static void ClearPendingSleepOwner(EditorWindow child)
        {
            EditorInternal.ESEditorPresentation.ClearPendingSleepOwner(child);
        }

        public static void ClearPendingSleepOwners(string ownerKey)
        {
            EditorInternal.ESEditorPresentation.ClearPendingSleepOwners(ownerKey);
        }
    }
}

namespace ES.EditorInternal
{
    internal static class ESWindowActivationMotion
    {
        internal const float Duration = 0.64f;

        private const float AnticipationPoint = 0.08f;
        private const float PrimaryOvershootPoint = 0.50f;
        private const float RecoilPoint = 0.70f;
        private const float SecondaryOvershootPoint = 0.84f;
        private const float OpacitySettlePoint = 0.40f;
        private const float TranslationSettlePoint = 0.68f;

        internal static void Apply(VisualElement element, float progress, float intensity)
        {
            if (element == null)
                return;
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float scale = EvaluateScale(normalized, strength);
            element.style.opacity = EvaluateOpacity(normalized, strength);
            element.style.translate = new Translate(
                0f,
                EvaluateTranslateY(normalized, strength),
                0f);
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        internal static void ApplyWithFrameScale(
            VisualElement element,
            float progress,
            float intensity)
        {
            Apply(element, progress, intensity);
            if (element == null)
                return;
            float scale = ESWindowFrameActivation.EvaluateFrameScale(progress, intensity);
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        internal static float EvaluateScale(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = Mathf.Lerp(1f, 0.735f, strength);
            float anticipation = Mathf.Lerp(1f, 0.70f, strength);
            float primaryOvershoot = Mathf.Lerp(1f, 1.095f, strength);
            float recoil = Mathf.Lerp(1f, 0.968f, strength);
            float secondaryOvershoot = Mathf.Lerp(1f, 1.018f, strength);
            if (normalized <= AnticipationPoint)
            {
                float phase = normalized / AnticipationPoint;
                return Mathf.Lerp(start, anticipation, SmoothStep(phase));
            }

            if (normalized <= PrimaryOvershootPoint)
            {
                float phase = (normalized - AnticipationPoint)
                    / (PrimaryOvershootPoint - AnticipationPoint);
                return Mathf.Lerp(anticipation, primaryOvershoot, EaseOutQuart(phase));
            }

            if (normalized <= RecoilPoint)
            {
                float phase = (normalized - PrimaryOvershootPoint)
                    / (RecoilPoint - PrimaryOvershootPoint);
                return Mathf.Lerp(primaryOvershoot, recoil, SmoothStep(phase));
            }

            if (normalized <= SecondaryOvershootPoint)
            {
                float phase = (normalized - RecoilPoint)
                    / (SecondaryOvershootPoint - RecoilPoint);
                return Mathf.Lerp(recoil, secondaryOvershoot, SmoothStep(phase));
            }

            float settle = (normalized - SecondaryOvershootPoint)
                / (1f - SecondaryOvershootPoint);
            return Mathf.Lerp(secondaryOvershoot, 1f, SmootherStep(settle));
        }

        internal static float EvaluateOpacity(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float phase = Mathf.Clamp01(normalized / OpacitySettlePoint);
            float start = Mathf.Lerp(1f, 0.015f, strength);
            return Mathf.Lerp(start, 1f, EaseOutCubic(phase));
        }

        internal static float EvaluateTranslateY(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = 26f * strength;
            float anticipation = 30f * strength;
            float lift = -2f * strength;
            if (normalized <= AnticipationPoint)
            {
                float phase = normalized / AnticipationPoint;
                return Mathf.Lerp(start, anticipation, SmoothStep(phase));
            }

            if (normalized <= PrimaryOvershootPoint)
            {
                float phase = (normalized - AnticipationPoint)
                    / (PrimaryOvershootPoint - AnticipationPoint);
                return Mathf.Lerp(anticipation, lift, EaseOutQuart(phase));
            }

            if (normalized <= TranslationSettlePoint)
            {
                float phase = (normalized - PrimaryOvershootPoint)
                    / (TranslationSettlePoint - PrimaryOvershootPoint);
                return Mathf.Lerp(lift, 0f, SmoothStep(phase));
            }

            return 0f;
        }

        private static float EaseOutCubic(float value)
        {
            float clamped = Mathf.Clamp01(value);
            float inverse = 1f - clamped;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutQuart(float value)
        {
            float clamped = Mathf.Clamp01(value);
            float inverse = 1f - clamped;
            float square = inverse * inverse;
            return 1f - square * square;
        }

        private static float SmoothStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private static float SmootherStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * clamped
                * (clamped * (clamped * 6f - 15f) + 10f);
        }
    }

    internal static class ESWindowFrameActivation
    {
        private sealed class RunningAnimation
        {
            internal int WindowId;
            internal EditorWindow Window;
            internal VisualElement Root;
            internal VisualElement Gate;
            internal readonly List<VisualElement> HiddenContent = new List<VisualElement>();
            internal readonly List<StyleEnum<DisplayStyle>> HiddenContentDisplays =
                new List<StyleEnum<DisplayStyle>>();
            internal Rect Target;
            internal Vector2 OriginalMinSize;
            internal float Intensity;
            internal double StartedAt;
            internal IVisualElementScheduledItem Schedule;
        }

        internal const string NativeFrameClass = "es-window-native-frame-activation";
        private static readonly Dictionary<int, RunningAnimation> Running =
            new Dictionary<int, RunningAnimation>();
        private static readonly Dictionary<VisualElement, RunningAnimation> RunningByRoot =
            new Dictionary<VisualElement, RunningAnimation>();

        internal static Rect EvaluateFrame(Rect target, float progress, float intensity)
        {
            float scale = EvaluateFrameScale(progress, intensity);
            float width = Mathf.Max(1f, target.width * scale);
            float height = Mathf.Max(1f, target.height * scale);
            return new Rect(
                target.center.x - width * 0.5f,
                target.center.y - height * 0.5f,
                width,
                height);
        }

        internal static float EvaluateFrameScale(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = Mathf.Lerp(1f, 0.34f, strength);
            float anticipation = Mathf.Lerp(1f, 0.32f, strength);
            float primaryOvershoot = Mathf.Lerp(1f, 1.04f, strength);
            float recoil = Mathf.Lerp(1f, 0.982f, strength);
            float secondaryOvershoot = Mathf.Lerp(1f, 1.012f, strength);
            if (normalized <= 0.08f)
                return Mathf.Lerp(start, anticipation, SmoothStep(normalized / 0.08f));
            if (normalized <= 0.50f)
            {
                float phase = (normalized - 0.08f) / 0.42f;
                return Mathf.Lerp(anticipation, primaryOvershoot, EaseOutQuart(phase));
            }
            if (normalized <= 0.70f)
            {
                float phase = (normalized - 0.50f) / 0.20f;
                return Mathf.Lerp(primaryOvershoot, recoil, SmoothStep(phase));
            }
            if (normalized <= 0.84f)
            {
                float phase = (normalized - 0.70f) / 0.14f;
                return Mathf.Lerp(recoil, secondaryOvershoot, SmoothStep(phase));
            }

            return Mathf.Lerp(
                secondaryOvershoot,
                1f,
                SmootherStep((normalized - 0.84f) / 0.16f));
        }

        internal static void Play(EditorWindow window, Rect target)
        {
            if (window == null)
                return;
            if (Running.ContainsKey(window.GetInstanceID()))
                return;
            Stop(window);
            VisualElement root = window.rootVisualElement;
            if (root == null
                || root.panel == null
                || !ESEditorPresentation.MotionEnabled
                || ESEditorPresentation.MotionIntensity <= 0.001f
                || target.width <= 1f
                || target.height <= 1f)
                return;

            var running = new RunningAnimation
            {
                WindowId = window.GetInstanceID(),
                Window = window,
                Root = root,
                Target = target,
                OriginalMinSize = window.minSize,
                Intensity = ESEditorPresentation.MotionIntensity,
                StartedAt = EditorApplication.timeSinceStartup
            };
            Running[running.WindowId] = running;
            RunningByRoot[running.Root] = running;
            running.Root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            try
            {
                running.Gate = CreateOpeningGate(running);
                window.minSize = new Vector2(
                    Mathf.Min(running.OriginalMinSize.x, Mathf.Max(240f, target.width * 0.28f)),
                    Mathf.Min(running.OriginalMinSize.y, Mathf.Max(180f, target.height * 0.28f)));
                window.position = EvaluateFrame(target, 0f, running.Intensity);
                window.Repaint();
                if (!Running.TryGetValue(running.WindowId, out RunningAnimation current)
                    || !ReferenceEquals(current, running)
                    || running.Window == null
                    || running.Root == null
                    || running.Root.panel == null)
                {
                    if (Running.TryGetValue(running.WindowId, out current)
                        && ReferenceEquals(current, running))
                        Complete(running, true);
                    return;
                }
                running.Schedule = running.Root.schedule
                    .Execute(() => Update(running))
                    .Every(16);
            }
            catch (Exception exception)
            {
                Complete(running, true);
                Debug.LogException(exception);
            }
        }

        private static void OnRootDetached(DetachFromPanelEvent evt)
        {
            if (evt.currentTarget is VisualElement root
                && RunningByRoot.TryGetValue(root, out RunningAnimation running))
                Complete(running, false);
        }

        internal static void Stop(EditorWindow window, bool restoreWindow = true)
        {
            if (ReferenceEquals(window, null))
                return;
            Stop(window.GetInstanceID(), restoreWindow);
        }

        internal static void Stop(int windowId, bool restoreWindow = true)
        {
            if (Running.TryGetValue(windowId, out RunningAnimation running))
                Complete(running, restoreWindow);
        }

        private static void Update(RunningAnimation running)
        {
            if (running == null
                || !Running.TryGetValue(running.WindowId, out RunningAnimation current)
                || !ReferenceEquals(current, running))
            {
                running?.Schedule?.Pause();
                return;
            }

            try
            {
                if (running.Window == null
                    || running.Root == null
                    || running.Root.panel == null)
                {
                    Complete(running, true);
                    return;
                }

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup
                    - running.StartedAt) / ESWindowActivationMotion.Duration));
                running.Window.position = EvaluateFrame(
                    running.Target,
                    progress,
                    running.Intensity);
                running.Window.Repaint();
                if (progress >= 1f)
                    Complete(running, true);
            }
            catch (Exception exception)
            {
                Complete(running, true);
                Debug.LogException(exception);
            }
        }

        private static void Complete(RunningAnimation running, bool restoreWindow)
        {
            if (running == null)
                return;

            Exception firstFailure = null;
            try
            {
                running.Schedule?.Pause();
            }
            catch (Exception exception)
            {
                firstFailure = exception;
            }

            try
            {
                if (Running.TryGetValue(running.WindowId, out RunningAnimation current)
                    && ReferenceEquals(current, running))
                    Running.Remove(running.WindowId);
                if (running.Root != null
                    && RunningByRoot.TryGetValue(running.Root, out RunningAnimation rootAnimation)
                    && ReferenceEquals(rootAnimation, running))
                    RunningByRoot.Remove(running.Root);
            }
            catch (Exception exception)
            {
                RecordFrameActivationTeardownFailure(ref firstFailure, exception);
            }

            try
            {
                running.Root?.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            }
            catch (Exception exception)
            {
                RecordFrameActivationTeardownFailure(ref firstFailure, exception);
            }

            try
            {
                if (restoreWindow && running.Root != null)
                    ESWindowOpeningSweep.Stop(running.Root);
            }
            catch (Exception exception)
            {
                RecordFrameActivationTeardownFailure(ref firstFailure, exception);
            }

            try
            {
                RestoreOpeningGate(running);
            }
            catch (Exception exception)
            {
                RecordFrameActivationTeardownFailure(ref firstFailure, exception);
            }

            try
            {
                if (restoreWindow)
                    RestoreWindow(
                        running.Window,
                        running.Root,
                        running.Target,
                        running.OriginalMinSize);
            }
            catch (Exception exception)
            {
                RecordFrameActivationTeardownFailure(ref firstFailure, exception);
            }
            finally
            {
                running.Schedule = null;
                running.Gate = null;
                running.HiddenContent.Clear();
                running.HiddenContentDisplays.Clear();
                running.Root = null;
                running.Window = null;
            }

            if (firstFailure != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(firstFailure)
                    .Throw();
        }

        private static void RecordFrameActivationTeardownFailure(
            ref Exception firstFailure,
            Exception exception)
        {
            if (firstFailure == null)
                firstFailure = exception;
            else
                Debug.LogException(exception);
        }

        private static VisualElement CreateOpeningGate(RunningAnimation running)
        {
            VisualElement root = running?.Root;
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                VisualElement child = root[i];
                if (child == null)
                    continue;
                running.HiddenContent.Add(child);
                running.HiddenContentDisplays.Add(child.style.display);
                child.style.display = DisplayStyle.None;
            }

            var gate = new VisualElement
            {
                name = "ESWindowOpeningGate",
                pickingMode = PickingMode.Position,
                focusable = true,
                viewDataKey = null
            };
            gate.AddToClassList("es-window-opening-gate");
            gate.style.position = Position.Absolute;
            gate.style.left = 0f;
            gate.style.right = 0f;
            gate.style.top = 0f;
            gate.style.bottom = 0f;
            gate.style.alignItems = Align.Center;
            gate.style.justifyContent = Justify.Center;
            gate.style.backgroundColor = ESEditorPresentation.WindowSurfaceColor;

            var content = new VisualElement { name = "ESWindowOpeningGateContent" };
            content.AddToClassList("es-window-opening-gate-content");
            content.style.alignItems = Align.Center;
            content.style.justifyContent = Justify.Center;
            content.style.width = Length.Percent(100f);
            content.style.maxWidth = 520f;
            content.style.paddingLeft = 18f;
            content.style.paddingRight = 18f;

            var brand = new Label("ES") { name = "ESWindowOpeningGateBrand" };
            brand.AddToClassList("es-brand-title");
            brand.style.fontSize = 26f;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.unityTextAlign = TextAnchor.MiddleCenter;
            brand.style.color = ESEditorPresentation.SelectedTextColor;
            content.Add(brand);

            var title = new Label(ResolveOpeningTitle(running.Window, root))
            {
                name = "ESWindowOpeningGateTitle"
            };
            title.style.marginTop = 5f;
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.color = ESEditorPresentation.SectionSelectedTextColor;
            content.Add(title);

            var function = new Label(ResolveOpeningFunction(root))
            {
                name = "ESWindowOpeningGateFunction"
            };
            function.style.marginTop = 4f;
            function.style.fontSize = 10f;
            function.style.unityTextAlign = TextAnchor.MiddleCenter;
            function.style.whiteSpace = WhiteSpace.Normal;
            function.style.color = ESEditorPresentation.SectionMutedTextColor;
            content.Add(function);

            gate.Add(content);
            root.Add(gate);
            gate.BringToFront();
            gate.Focus();
            return gate;
        }

        private static void RestoreOpeningGate(RunningAnimation running)
        {
            if (running == null)
                return;
            running.Gate?.RemoveFromHierarchy();
            int count = Mathf.Min(
                running.HiddenContent.Count,
                running.HiddenContentDisplays.Count);
            for (int i = 0; i < count; i++)
            {
                VisualElement child = running.HiddenContent[i];
                if (child != null)
                    child.style.display = running.HiddenContentDisplays[i];
            }
        }

        private static string ResolveOpeningTitle(EditorWindow window, VisualElement root)
        {
            string title = window?.titleContent?.text;
            if (string.IsNullOrWhiteSpace(title))
                title = root?.Q<Label>("ESWindowTitle")?.text;
            return string.IsNullOrWhiteSpace(title) ? "ES 功能窗口" : title.Trim();
        }

        private static string ResolveOpeningFunction(VisualElement root)
        {
            string status = root?.Q<Label>("ESWindowStatus")?.text;
            const string currentPagePrefix = "当前页面：";
            if (!string.IsNullOrWhiteSpace(status)
                && status.StartsWith(currentPagePrefix, StringComparison.Ordinal))
                return status.Substring(currentPagePrefix.Length).Trim();

            string subtitle = root?.Q<Label>("ESWindowSubtitle")?.text;
            return string.IsNullOrWhiteSpace(subtitle)
                ? "正在准备功能界面"
                : subtitle.Trim();
        }

        private static void RestoreWindow(
            EditorWindow window,
            VisualElement root,
            Rect target,
            Vector2 originalMinSize)
        {
            if (window == null)
                return;
            try
            {
                window.minSize = originalMinSize;
                if (!window.docked && root != null && root.panel != null)
                    window.position = target;
            }
            catch (MissingReferenceException)
            {
            }
            catch (NullReferenceException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static float SmoothStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float SmootherStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float EaseOutQuart(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse;
        }
    }

    internal static class ESWindowOpeningSweep
    {
        private sealed class RunningSweep
        {
            internal VisualElement Root;
            internal VisualElement Host;
            internal VisualElement Beam;
            internal float Width;
            internal float Intensity;
            internal double StartedAt;
            internal IVisualElementScheduledItem Schedule;
        }

        internal const float Duration = 0.72f;
        private const string PlayedClass = "es-window-opening-sweep-played";
        private static readonly Dictionary<VisualElement, RunningSweep> Running =
            new Dictionary<VisualElement, RunningSweep>();

        internal static float EvaluateOpacity(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            return Mathf.Sin(normalized * Mathf.PI)
                * Mathf.Clamp01(intensity)
                * 0.42f;
        }

        internal static float EvaluatePosition(float progress, float width)
        {
            float normalized = Mathf.Clamp01(progress);
            float inverse = 1f - normalized;
            float eased = 1f - inverse * inverse * inverse;
            return Mathf.Lerp(-190f, Mathf.Max(1f, width) + 50f, eased);
        }

        internal static void Play(VisualElement root)
        {
            if (root == null
                || root.panel == null
                || root.ClassListContains(PlayedClass)
                || !ESEditorPresentation.MotionEnabled
                || ESEditorPresentation.MotionIntensity <= 0.001f)
                return;

            Stop(root);
            root.AddToClassList(PlayedClass);
            var host = new VisualElement
            {
                name = "ESWindowOpeningSweep",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            host.style.position = Position.Absolute;
            host.style.left = 0f;
            host.style.right = 0f;
            host.style.top = 0f;
            host.style.bottom = 0f;
            host.style.overflow = Overflow.Hidden;

            var beam = new VisualElement
            {
                name = "ESWindowOpeningSweepBeam",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            beam.style.position = Position.Absolute;
            beam.style.top = -120f;
            beam.style.bottom = -120f;
            beam.style.width = 150f;
            beam.style.flexDirection = FlexDirection.Row;
            beam.style.rotate = new Rotate(new Angle(-11f, AngleUnit.Degree));
            AddSweepBand(beam, 0.10f, 34f);
            AddSweepBand(beam, 0.28f, 82f);
            AddSweepBand(beam, 0.08f, 34f);
            host.Add(beam);
            root.Add(host);
            host.BringToFront();

            float width = root.resolvedStyle.width;
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 1f)
                width = 1200f;
            var running = new RunningSweep
            {
                Root = root,
                Host = host,
                Beam = beam,
                Width = width,
                Intensity = ESEditorPresentation.MotionIntensity,
                StartedAt = EditorApplication.timeSinceStartup
            };
            Running[root] = running;
            try
            {
                root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
                running.Schedule = host.schedule.Execute(() => Update(running)).Every(16);
            }
            catch
            {
                Complete(running);
                throw;
            }
        }

        internal static void Stop(VisualElement root)
        {
            if (root != null && Running.TryGetValue(root, out RunningSweep running))
                Complete(running);
        }

        internal static void Replay(VisualElement root)
        {
            if (root == null)
                return;
            Stop(root);
            root.RemoveFromClassList(PlayedClass);
            Play(root);
        }

        private static void OnRootDetached(DetachFromPanelEvent evt)
        {
            Stop(evt.currentTarget as VisualElement);
        }

        private static void Update(RunningSweep running)
        {
            if (running == null
                || running.Root == null
                || !Running.TryGetValue(running.Root, out RunningSweep current)
                || !ReferenceEquals(current, running))
            {
                running?.Schedule?.Pause();
                return;
            }

            try
            {
                if (running.Root.panel == null
                    || running.Host == null
                    || running.Host.panel == null)
                {
                    Complete(running);
                    return;
                }

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup
                    - running.StartedAt) / Duration));
                running.Beam.style.left = EvaluatePosition(progress, running.Width);
                running.Beam.style.opacity = EvaluateOpacity(progress, running.Intensity);
                if (progress >= 1f)
                    Complete(running);
            }
            catch (Exception exception)
            {
                Complete(running);
                Debug.LogException(exception);
            }
        }

        private static void Complete(RunningSweep running)
        {
            if (running == null)
                return;
            running.Schedule?.Pause();
            if (running.Root != null
                && Running.TryGetValue(running.Root, out RunningSweep current)
                && ReferenceEquals(current, running))
                Running.Remove(running.Root);
            running.Root?.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            try
            {
                running.Host?.RemoveFromHierarchy();
            }
            catch (NullReferenceException)
            {
            }
            finally
            {
                running.Schedule = null;
                running.Beam = null;
                running.Host = null;
                running.Root = null;
            }
        }

        private static void AddSweepBand(VisualElement beam, float alpha, float width)
        {
            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            Color color = ESEditorPresentation.ActiveColor;
            color.a = alpha;
            band.style.width = width;
            band.style.backgroundColor = color;
            beam.Add(band);
        }
    }

    /// <summary>
    /// 已解析的 ES 字段呈现信息。字段反射和新旧 Attribute 合并只执行一次，
    /// GraphView 后续创建卡片与详情面板时直接读取缓存结果。
    /// </summary>
    internal readonly struct ESFieldPresentationMetadata
    {
        public readonly FieldInfo Field;
        public readonly bool IsDefined;
        public readonly ESFieldLevel Level;
        public readonly bool Required;
        public readonly string Hint;

        public ESFieldPresentationMetadata(FieldInfo field, bool isDefined,
            ESFieldLevel level, bool required, string hint)
        {
            Field = field;
            IsDefined = isDefined;
            Level = level;
            Required = required;
            Hint = hint;
        }
    }

    /// <summary>
    /// 按 Payload 类型缓存 ES 字段元数据。缓存随 Unity 域重载自然释放，
    /// 不持有资产、窗口或序列化对象，因此不会形成编辑器生命周期泄漏。
    /// </summary>
    internal static class ESFieldPresentationMetadataCache
    {
        private sealed class TypeMetadata
        {
            public readonly Dictionary<string, ESFieldPresentationMetadata> Fields;
            public readonly ESFieldPresentationMetadata[] SummaryFields;

            public TypeMetadata(Dictionary<string, ESFieldPresentationMetadata> fields,
                ESFieldPresentationMetadata[] summaryFields)
            {
                Fields = fields;
                SummaryFields = summaryFields;
            }
        }

        private static readonly object CacheGate = new object();
        private static readonly Dictionary<Type, TypeMetadata> Cache
            = new Dictionary<Type, TypeMetadata>();

        public static bool TryGet(Type payloadType, string fieldName,
            out ESFieldPresentationMetadata metadata)
        {
            if (payloadType == null || string.IsNullOrWhiteSpace(fieldName))
            {
                metadata = default;
                return false;
            }

            return GetOrCreate(payloadType).Fields.TryGetValue(fieldName, out metadata)
                   && metadata.IsDefined;
        }

        public static IReadOnlyList<ESFieldPresentationMetadata> GetSummaryFields(Type payloadType)
        {
            return payloadType == null
                ? Array.Empty<ESFieldPresentationMetadata>()
                : GetOrCreate(payloadType).SummaryFields;
        }

        private static TypeMetadata GetOrCreate(Type payloadType)
        {
            lock (CacheGate)
            {
                if (Cache.TryGetValue(payloadType, out TypeMetadata cached))
                    return cached;

                TypeMetadata created = Build(payloadType);
                Cache.Add(payloadType, created);
                return created;
            }
        }

        private static TypeMetadata Build(Type payloadType)
        {
            FieldInfo[] fields = payloadType.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var byName = new Dictionary<string, ESFieldPresentationMetadata>(
                fields.Length, StringComparer.Ordinal);
            var summary = new List<ESFieldPresentationMetadata>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                ESFieldAttribute current = field.GetCustomAttribute<ESFieldAttribute>(true);
                ESFieldPolicyAttribute oldPolicy = field.GetCustomAttribute<ESFieldPolicyAttribute>(true);
                ESFieldHintAttribute oldHint = field.GetCustomAttribute<ESFieldHintAttribute>(true);
                bool defined = current != null || oldPolicy != null || oldHint != null;
                ESFieldLevel level = current?.Level
                    ?? (oldPolicy?.Requirement == ESFieldRequirement.Recommended
                        ? ESFieldLevel.Important
                        : oldPolicy?.Requirement == ESFieldRequirement.Required
                            ? ESFieldLevel.Core
                            : ESFieldLevel.Normal);
                bool required = current?.Required == true
                                || oldPolicy?.Requirement == ESFieldRequirement.Required;
                string hint = NormalizeHint(current?.Hint ?? oldHint?.Text);
                var metadata = new ESFieldPresentationMetadata(
                    field, defined, level, required, hint);
                byName[field.Name] = metadata;
                if (field.IsPublic && defined && level != ESFieldLevel.Normal)
                    summary.Add(metadata);
            }

            return new TypeMetadata(byName, summary.ToArray());
        }

        private static string NormalizeHint(string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? null : hint.Trim();
        }
    }

    /// <summary>
    /// Shared visual primitives for ES editor drawing.
    ///
    /// The class deliberately contains no serialized or runtime state. All objects are lazily
    /// created once per editor skin and are reused by Section, polymorphic and future ES drawers.
    /// This keeps the IMGUI repaint path free from style/texture allocations.
    /// </summary>
    public interface IESWindowSleepRelationshipState
    {
        bool SleepOwnerDetachedByClose { get; }
        void DetachSleepOwnerAfterOwnerClose();
    }

    internal readonly struct ESWindowSemiSleepPerformanceSample
    {
        internal ESWindowSemiSleepPerformanceSample(
            int boundWindowCount,
            long updateCount,
            long bindingVisitCount,
            long nativePositionCommitCount,
            long repaintRequestCount,
            long updateElapsedTicks,
            long updateAllocatedBytes,
            long maximumUpdateElapsedTicks,
            long maximumAllocatedBytesPerUpdate,
            double sampleDurationSeconds)
        {
            BoundWindowCount = boundWindowCount;
            UpdateCount = updateCount;
            BindingVisitCount = bindingVisitCount;
            NativePositionCommitCount = nativePositionCommitCount;
            RepaintRequestCount = repaintRequestCount;
            UpdateElapsedTicks = updateElapsedTicks;
            UpdateAllocatedBytes = updateAllocatedBytes;
            MaximumUpdateElapsedTicks = maximumUpdateElapsedTicks;
            MaximumAllocatedBytesPerUpdate = maximumAllocatedBytesPerUpdate;
            SampleDurationSeconds = sampleDurationSeconds;
        }

        internal int BoundWindowCount { get; }
        internal long UpdateCount { get; }
        internal long BindingVisitCount { get; }
        internal long NativePositionCommitCount { get; }
        internal long RepaintRequestCount { get; }
        internal long UpdateElapsedTicks { get; }
        internal long UpdateAllocatedBytes { get; }
        internal long MaximumUpdateElapsedTicks { get; }
        internal long MaximumAllocatedBytesPerUpdate { get; }
        internal double SampleDurationSeconds { get; }

        internal double AverageUpdateMicroseconds => UpdateCount <= 0
            ? 0d
            : UpdateElapsedTicks * 1000000d
                / System.Diagnostics.Stopwatch.Frequency
                / UpdateCount;

        internal double MaximumUpdateMicroseconds => MaximumUpdateElapsedTicks <= 0L
            ? 0d
            : MaximumUpdateElapsedTicks * 1000000d
                / System.Diagnostics.Stopwatch.Frequency;

        internal double AverageAllocatedBytesPerUpdate => UpdateCount <= 0
            ? 0d
            : UpdateAllocatedBytes / (double)UpdateCount;
    }

    internal readonly struct ESWindowPresentationHealthSnapshot
    {
        internal ESWindowPresentationHealthSnapshot(
            int bindingSlotCount,
            int liveWindowCount,
            int sleepSupportedCount,
            int sleepingCount,
            int transitioningCount,
            int duplicateWindowInstanceCount,
            int missingSystemHostCount,
            int geometryMismatchCount,
            int pendingOwnerCount,
            int staleEntryCount,
            bool resumeRetryExhausted,
            string firstIssueWindowType)
        {
            BindingSlotCount = bindingSlotCount;
            LiveWindowCount = liveWindowCount;
            SleepSupportedCount = sleepSupportedCount;
            SleepingCount = sleepingCount;
            TransitioningCount = transitioningCount;
            DuplicateWindowInstanceCount = duplicateWindowInstanceCount;
            MissingSystemHostCount = missingSystemHostCount;
            GeometryMismatchCount = geometryMismatchCount;
            PendingOwnerCount = pendingOwnerCount;
            StaleEntryCount = staleEntryCount;
            ResumeRetryExhausted = resumeRetryExhausted;
            FirstIssueWindowType = firstIssueWindowType;
        }

        internal int BindingSlotCount { get; }
        internal int LiveWindowCount { get; }
        internal int SleepSupportedCount { get; }
        internal int SleepingCount { get; }
        internal int TransitioningCount { get; }
        internal int DuplicateWindowInstanceCount { get; }
        internal int MissingSystemHostCount { get; }
        internal int GeometryMismatchCount { get; }
        internal int PendingOwnerCount { get; }
        internal int StaleEntryCount { get; }
        internal bool ResumeRetryExhausted { get; }
        internal string FirstIssueWindowType { get; }
        internal bool HasIssues => DuplicateWindowInstanceCount > 0
            || MissingSystemHostCount > 0
            || GeometryMismatchCount > 0
            || StaleEntryCount > 0
            || ResumeRetryExhausted;
    }

    internal static class ESEditorPresentation
    {
        private const int MaximumPresentationShortTitleLength = 8;

        // These are semantic fallbacks for windows that have not yet declared the
        // short-title contract.  Keep the list small and stable: it is evaluated
        // only while binding a window, never from the semi-sleep update loop.
        private static readonly KeyValuePair<string, string>[] PreferredShortTitles =
        {
            new KeyValuePair<string, string>("世界构建", "世界构建"),
            new KeyValuePair<string, string>("世界编辑器", "世界"),
            new KeyValuePair<string, string>("世界工作台", "世界"),
            new KeyValuePair<string, string>("Workbench", "工作台"),
            new KeyValuePair<string, string>("工作台", "工作台"),
            new KeyValuePair<string, string>("World", "世界"),
            new KeyValuePair<string, string>("世界", "世界"),
            new KeyValuePair<string, string>("Terrain", "地形"),
            new KeyValuePair<string, string>("地形", "地形"),
            new KeyValuePair<string, string>("Scene", "场景"),
            new KeyValuePair<string, string>("场景", "场景"),
            new KeyValuePair<string, string>("Hierarchy", "层级"),
            new KeyValuePair<string, string>("层级", "层级"),
            new KeyValuePair<string, string>("Object", "对象"),
            new KeyValuePair<string, string>("对象", "对象"),
            new KeyValuePair<string, string>("Prefab", "Prefab"),
            new KeyValuePair<string, string>("预制体", "Prefab"),
            new KeyValuePair<string, string>("对话", "对话"),
            new KeyValuePair<string, string>("空间", "空间"),
            new KeyValuePair<string, string>("Codex", "Agent"),
            new KeyValuePair<string, string>("Agent", "Agent"),
            new KeyValuePair<string, string>("Graph", "图"),
            new KeyValuePair<string, string>("图", "图"),
            new KeyValuePair<string, string>("Track", "轨道"),
            new KeyValuePair<string, string>("轨道", "轨道"),
            new KeyValuePair<string, string>("Command", "命令"),
            new KeyValuePair<string, string>("命令", "命令"),
            new KeyValuePair<string, string>("Font", "字体"),
            new KeyValuePair<string, string>("字体", "字体"),
            new KeyValuePair<string, string>("Localization", "本地化"),
            new KeyValuePair<string, string>("本地化", "本地化"),
            new KeyValuePair<string, string>("Resource", "资源"),
            new KeyValuePair<string, string>("资源", "资源"),
            new KeyValuePair<string, string>("Package", "资产包"),
            new KeyValuePair<string, string>("资产包", "资产包"),
            new KeyValuePair<string, string>("Collection", "收集"),
            new KeyValuePair<string, string>("收集", "收集"),
            new KeyValuePair<string, string>("Release", "发布"),
            new KeyValuePair<string, string>("发布", "发布"),
            new KeyValuePair<string, string>("Data", "数据"),
            new KeyValuePair<string, string>("数据", "数据"),
            new KeyValuePair<string, string>("Launcher", "启动器"),
            new KeyValuePair<string, string>("启动器", "启动器"),
            new KeyValuePair<string, string>("Automation", "自动化"),
            new KeyValuePair<string, string>("自动化", "自动化"),
            new KeyValuePair<string, string>("Health", "健康"),
            new KeyValuePair<string, string>("健康", "健康"),
            new KeyValuePair<string, string>("Theme", "主题"),
            new KeyValuePair<string, string>("主题", "主题"),
            new KeyValuePair<string, string>("Cockpit", "驾驶舱"),
            new KeyValuePair<string, string>("驾驶舱", "驾驶舱"),
            new KeyValuePair<string, string>("Camera", "相机"),
            new KeyValuePair<string, string>("相机", "相机"),
            new KeyValuePair<string, string>("Audio", "音频"),
            new KeyValuePair<string, string>("音频", "音频"),
            new KeyValuePair<string, string>("Sound", "音效"),
            new KeyValuePair<string, string>("音效", "音效"),
            new KeyValuePair<string, string>("Interaction", "交互"),
            new KeyValuePair<string, string>("交互", "交互"),
            new KeyValuePair<string, string>("Property", "属性"),
            new KeyValuePair<string, string>("属性", "属性"),
            new KeyValuePair<string, string>("Installer", "安装"),
            new KeyValuePair<string, string>("安装", "安装"),
            new KeyValuePair<string, string>("Material", "材质"),
            new KeyValuePair<string, string>("材质", "材质"),
            new KeyValuePair<string, string>("Migration", "迁移"),
            new KeyValuePair<string, string>("迁移", "迁移"),
            new KeyValuePair<string, string>("Preview", "预览"),
            new KeyValuePair<string, string>("预览", "预览"),
            new KeyValuePair<string, string>("Diagnostic", "诊断"),
            new KeyValuePair<string, string>("诊断", "诊断"),
            new KeyValuePair<string, string>("Runtime", "运行时"),
            new KeyValuePair<string, string>("运行时", "运行时"),
            new KeyValuePair<string, string>("Monitor", "监视"),
            new KeyValuePair<string, string>("监视", "监视"),
            new KeyValuePair<string, string>("Risk", "风险"),
            new KeyValuePair<string, string>("风险", "风险"),
            new KeyValuePair<string, string>("Progress", "进度"),
            new KeyValuePair<string, string>("进度", "进度"),
            new KeyValuePair<string, string>("Dialog", "对话框"),
            new KeyValuePair<string, string>("对话框", "对话框"),
            new KeyValuePair<string, string>("InputAction", "输入"),
            new KeyValuePair<string, string>("Test", "测试"),
            new KeyValuePair<string, string>("测试", "测试"),
            new KeyValuePair<string, string>("Input", "输入"),
            new KeyValuePair<string, string>("输入", "输入"),
            new KeyValuePair<string, string>("GameCore", "核心"),
            new KeyValuePair<string, string>("Core", "核心"),
            new KeyValuePair<string, string>("核心", "核心"),
            new KeyValuePair<string, string>("Skill", "技能"),
            new KeyValuePair<string, string>("技能", "技能"),
            new KeyValuePair<string, string>("Candidate", "候选"),
            new KeyValuePair<string, string>("候选", "候选"),
            new KeyValuePair<string, string>("Review", "审查"),
            new KeyValuePair<string, string>("审查", "审查")
        };

        internal static string BuildDefaultPresentationShortTitle(string fullTitle)
        {
            string normalized = string.IsNullOrWhiteSpace(fullTitle)
                ? "ES"
                : fullTitle.Trim();
            int separator = normalized.LastIndexOf('/');
            if (separator >= 0 && separator + 1 < normalized.Length)
                normalized = normalized.Substring(separator + 1).Trim();

            for (int i = 0; i < PreferredShortTitles.Length; i++)
            {
                KeyValuePair<string, string> preferred = PreferredShortTitles[i];
                if (normalized.IndexOf(preferred.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return preferred.Value;
            }

            char[] compact = new char[Mathf.Min(4, normalized.Length)];
            int count = 0;
            for (int i = 0; i < normalized.Length && count < compact.Length; i++)
            {
                char current = normalized[i];
                if (char.IsWhiteSpace(current)
                    || current == '【'
                    || current == '】'
                    || current == '-'
                    || current == '_'
                    || current == ':')
                    continue;
                compact[count++] = current;
            }
            return count == 0 ? "ES" : new string(compact, 0, count);
        }

        private static string ResolveWindowPresentationTitle(EditorWindow window)
        {
            if (window is ES.IESWindowPresentationMetadata metadata
                && !string.IsNullOrWhiteSpace(metadata.ESWindow_PresentationTitle))
                return metadata.ESWindow_PresentationTitle.Trim();
            return string.IsNullOrWhiteSpace(window?.titleContent?.text)
                ? "ES 功能窗口"
                : window.titleContent.text.Trim();
        }

        private static string ResolveWindowPresentationShortTitle(EditorWindow window)
        {
            if (window is ES.IESWindowPresentationTabLabel tabLabel
                && !string.IsNullOrWhiteSpace(tabLabel.ESWindow_PresentationTabLabel))
                return NormalizePresentationShortTitle(tabLabel.ESWindow_PresentationTabLabel);

            if (window is ES.IESWindowPresentationShortTitle shortTitle
                && !string.IsNullOrWhiteSpace(shortTitle.ESWindow_PresentationShortTitle))
                return NormalizePresentationShortTitle(shortTitle.ESWindow_PresentationShortTitle);

            ES.ESWindowPresentationShortTitleAttribute attribute =
                window?.GetType().GetCustomAttribute<ES.ESWindowPresentationShortTitleAttribute>(true);
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Title))
                return NormalizePresentationShortTitle(attribute.Title);
            return BuildDefaultPresentationShortTitle(ResolveWindowPresentationTitle(window));
        }

        internal static string GetWindowPresentationShortTitle(EditorWindow window)
        {
            if (window == null)
                return "ES";
            if (windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                && !string.IsNullOrWhiteSpace(binding.presentationShortTitle))
                return binding.presentationShortTitle;
            return ResolveWindowPresentationShortTitle(window);
        }

        internal static bool TrySetWindowPresentationShortTitle(EditorWindow window, string shortTitle)
        {
            if (window == null)
                return false;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || binding == null)
                return false;

            string normalized = NormalizePresentationShortTitle(shortTitle);
            binding.presentationShortTitle = normalized;
            SaveSemiSleepPreferences(binding);
            ApplySemiSleepOverlayState(binding);
            RefreshSemiSleepControls(binding);
            window.Repaint();
            return true;
        }

        private static string NormalizePresentationShortTitle(string shortTitle)
        {
            string normalized = string.IsNullOrWhiteSpace(shortTitle)
                ? string.Empty
                : shortTitle.Trim();
            return normalized.Length > MaximumPresentationShortTitleLength
                ? normalized.Substring(0, MaximumPresentationShortTitleLength)
                : normalized;
        }

        internal enum ESCornerRadiusToken : byte
        {
            None,
            Control,
            Card,
            Section,
            Overlay,
            Pill
        }

        [Flags]
        internal enum ESCornerMask : byte
        {
            None = 0,
            TopLeft = 1 << 0,
            TopRight = 1 << 1,
            BottomLeft = 1 << 2,
            BottomRight = 1 << 3,
            Left = TopLeft | BottomLeft,
            Right = TopRight | BottomRight,
            Top = TopLeft | TopRight,
            Bottom = BottomLeft | BottomRight,
            All = TopLeft | TopRight | BottomLeft | BottomRight
        }

        internal enum ESWindowEdge : byte
        {
            Left,
            Right,
            Top,
            Bottom
        }

        private enum SemiSleepBlockReason : byte
        {
            None,
            InvalidBinding,
            Unsupported,
            DuplicateInstance,
            NotAllowed,
            OwnedSurface,
            Busy,
            FocusMode,
            Docked,
            PanelUnavailable,
            GlobalAutoDisabled,
            Pinned
        }

        internal enum ESPresentationRole : byte
        {
            WindowSurface,
            RaisedSurface,
            InsetSurface,
            CanvasSurface,
            Toolbar,
            Control,
            PrimaryAction,
            Status
        }

        internal enum ESPresentationState : byte
        {
            Normal,
            Selected,
            Busy,
            Inactive,
            Disabled,
            ReadOnly,
            Warning,
            Error
        }

        internal enum ESPresentationInteraction : byte
        {
            Rest,
            Hover,
            Pressed,
            Focused
        }

        internal readonly struct ESPresentationStyle
        {
            internal ESPresentationStyle(
                Color backgroundColor,
                Color textColor,
                Color borderColor,
                float opacity = 1f)
            {
                BackgroundColor = backgroundColor;
                TextColor = textColor;
                BorderColor = borderColor;
                Opacity = Mathf.Clamp01(opacity);
            }

            internal Color BackgroundColor { get; }
            internal Color TextColor { get; }
            internal Color BorderColor { get; }
            internal float Opacity { get; }
        }

        /// <summary>
        /// 当前主题的只读语义快照。它只持有标量和颜色，不持有资产、窗口或 UI 引用。
        /// </summary>
        internal sealed class ESPresentationThemeSnapshot
        {
            internal ESPresentationThemeSnapshot(
                int themeGeneration,
                int skinGeneration,
                bool proSkin,
                float density,
                bool motionEnabled,
                Color windowSurface,
                Color raisedSurface,
                Color insetSurface,
                Color canvasSurface,
                Color toolbarSurface,
                Color controlSurface,
                Color selectedSurface,
                Color inactiveActionSurface,
                Color primaryActionSurface,
                Color activeActionSurface,
                Color warningActionSurface,
                Color errorActionSurface,
                Color text,
                Color strongText,
                Color mutedText,
                Color actionText,
                Color divider,
                Color selection,
                Color active,
                Color disabled,
                Color warning,
                Color error)
            {
                ThemeGeneration = themeGeneration;
                SkinGeneration = skinGeneration;
                ProSkin = proSkin;
                Density = density;
                MotionEnabled = motionEnabled;
                WindowSurface = windowSurface;
                RaisedSurface = raisedSurface;
                InsetSurface = insetSurface;
                CanvasSurface = canvasSurface;
                ToolbarSurface = toolbarSurface;
                ControlSurface = controlSurface;
                SelectedSurface = selectedSurface;
                InactiveActionSurface = inactiveActionSurface;
                PrimaryActionSurface = primaryActionSurface;
                ActiveActionSurface = activeActionSurface;
                WarningActionSurface = warningActionSurface;
                ErrorActionSurface = errorActionSurface;
                Text = text;
                StrongText = strongText;
                MutedText = mutedText;
                ActionText = actionText;
                Divider = divider;
                Selection = selection;
                Active = active;
                Disabled = disabled;
                Warning = warning;
                Error = error;
            }

            internal int ThemeGeneration { get; }
            internal int SkinGeneration { get; }
            internal bool ProSkin { get; }
            internal float Density { get; }
            internal bool MotionEnabled { get; }
            internal Color WindowSurface { get; }
            internal Color RaisedSurface { get; }
            internal Color InsetSurface { get; }
            internal Color CanvasSurface { get; }
            internal Color ToolbarSurface { get; }
            internal Color ControlSurface { get; }
            internal Color SelectedSurface { get; }
            internal Color InactiveActionSurface { get; }
            internal Color PrimaryActionSurface { get; }
            internal Color ActiveActionSurface { get; }
            internal Color WarningActionSurface { get; }
            internal Color ErrorActionSurface { get; }
            internal Color Text { get; }
            internal Color StrongText { get; }
            internal Color MutedText { get; }
            internal Color ActionText { get; }
            internal Color Divider { get; }
            internal Color Selection { get; }
            internal Color Active { get; }
            internal Color Disabled { get; }
            internal Color Warning { get; }
            internal Color Error { get; }
        }

        private static bool skinInitialized;
        private static bool skinCleanupRegistered;
        private static bool cachedProSkin;
        private static int skinGeneration;
        private static int globalEditorSkinGeneration;
        private static GUIStyle surfaceStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle metaStyle;
        private static GUIStyle compactCollectionTitleStyle;
        private static GUIStyle compactCollectionMetaStyle;
        private static GUIStyle compactCollectionBodyStyle;
        private static GUIStyle toolbarStyle;
        private static GUIStyle toolbarButtonStyle;
        private static GUIStyle primaryButtonStyle;
        private static Texture2D surfaceTexture;
        private static Texture2D toolbarTexture;
        private static Texture2D toolbarButtonTexture;
        private static Texture2D toolbarButtonHoverTexture;
        private static Texture2D toolbarButtonActiveTexture;
        private static Texture2D primaryButtonTexture;
        private static Texture2D primaryButtonHoverTexture;
        private static Texture2D primaryButtonActiveTexture;
        private static Texture2D compactCollectionBodyTexture;
        private static ESGlobalEditorTheme theme;
        private static bool themeInitialized;
        private static int themeGeneration;
        private static ESPresentationThemeSnapshot presentationThemeSnapshot;
        private static readonly Dictionary<int, WindowBinding> windowBindings =
            new Dictionary<int, WindowBinding>(32);
        private static readonly Dictionary<VisualElement, WindowBinding> windowBindingsByRoot =
            new Dictionary<VisualElement, WindowBinding>(32);
        private static readonly Dictionary<string, WindowBinding> sleepOwnerBindingsByKey =
            new Dictionary<string, WindowBinding>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, string> windowHealthCoordinatorScratch =
            new Dictionary<Type, string>(16);
        private static readonly HashSet<int> resumeBindingsRetryExhaustedWindowIds =
            new HashSet<int>();
        // Editor 主线程专用工作区。清空后复用，避免批量休眠时逐窗口创建临时集合。
        private static readonly HashSet<int> semiSleepUsedSlotScratch =
            new HashSet<int>(32);
        private static readonly Dictionary<string, Texture> esBrandIconCache =
            new Dictionary<string, Texture>(StringComparer.Ordinal);
        private static readonly Unity.Profiling.ProfilerMarker SemiSleepUpdateProfilerMarker =
            new Unity.Profiling.ProfilerMarker("ES.Editor.WindowSleep.Update");
        private static readonly Unity.Profiling.ProfilerMarker SemiSleepNativePositionProfilerMarker =
            new Unity.Profiling.ProfilerMarker("ES.Editor.WindowSleep.NativePositionCommit");
        private static readonly Unity.Profiling.ProfilerMarker SemiSleepRepaintProfilerMarker =
            new Unity.Profiling.ProfilerMarker("ES.Editor.WindowSleep.Repaint");
        private static bool semiSleepPerformanceSampleActive;
        private static double semiSleepPerformanceSampleStartedAt;
        private static long semiSleepPerformanceUpdateCount;
        private static long semiSleepPerformanceBindingVisitCount;
        private static long semiSleepPerformanceNativePositionCommitCount;
        private static long semiSleepPerformanceRepaintRequestCount;
        private static long semiSleepPerformanceUpdateElapsedTicks;
        private static long semiSleepPerformanceUpdateAllocatedBytes;
        private static long semiSleepPerformanceMaximumUpdateElapsedTicks;
        private static long semiSleepPerformanceMaximumAllocatedBytesPerUpdate;
        private const string BrandFontResourcePath = "ESPresentation/Fonts/ESBrandSansSC";
        private const string BrandTypographyStyleSheetPath =
            "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESBrandTypography.uss";
        private const string PresentationControlsClass = "es-presentation-controls";
        private static Font brandFont;
        private static bool brandFontLoadAttempted;
        private static StyleSheet brandTypographyStyleSheet;

        private sealed class WindowBinding
        {
            public EditorWindow window;
            public VisualElement root;
            public VisualElement host;
            public VisualElement accentLine;
            public VisualElement sweep;
            public VisualElement semiSleepOverlay;
            public Label semiSleepMonogram;
            public Image semiSleepIcon;
            public Label semiSleepTitleLabel;
            public string presentationShortTitle;
            public string multiInstanceCoordinatorId;
            public VisualElement semiSleepPromotionProgress;
            public VisualElement semiSleepDockProgress;
            public bool diagnosticBarsHidden = true;
            public float diagnosticPromotionProgress = -1f;
            public bool diagnosticPromotionComplete;
            public VisualElement semiSleepControls;
            public ES.ESWindowActionHosts actionHosts;
            // Explicit hosts belong to the window's own panel. Lifecycle
            // recovery must wait for the caller to provide them again instead
            // of silently creating a second standard System bar.
            public bool actionHostsWereExplicit;
            public Button semiSleepToggleButton;
            public ToolbarMenu semiSleepOverflowMenu;
            public IVisualElementScheduledItem animation;
            public bool activationPending;
            public double pulseStartedAt;
            public ESStatusKind pulseStatus;
            public float pulseDuration;
            public bool allowSemiSleep;
            public bool supportsSemiSleep;
            // 同一具体 EditorWindow 类型的第二实例不会参与休眠状态机；
            // 这样不会让两个窗口竞争同一个按类型保存的 EditorPrefs 几何键。
            public bool singleInstanceViolation;
            public int singleInstanceOwnerId;
            public ESWindowVisualState visualState;
            public ESWindowVisualState transitionTargetState;
            public bool semiSleeping;
            public bool semiSleepTarget;
            public bool semiSleepAnimating;
            public double focusLostAt = -1d;
            public double semiSleepStartedAt;
            public double semiSleepTransitionDuration;
            public double sleepTileIdleStartedAt = -1d;
            public double edgeTabFullyExpandedAt = -1d;
            public double edgeTabHoverIntentStartedAt = -1d;
            public double edgeTabHoverExitGraceUntil = -1d;
            public double lastInteractionAt;
            public double transientInteractionGraceUntil;
            public Vector2 edgeTabLastPointerPosition;
            public bool hasEdgeTabPointerPosition;
            public Rect awakeBounds;
            public Rect semiSleepFromBounds;
            public Rect semiSleepToBounds;
            public Vector2 awakeMinSize;
            public Vector2 awakeMaxSize;
            public int semiSleepSlot = -1;
            public bool hasSemiSleepDockBounds;
            public Rect semiSleepDockBounds;
            public int semiSleepDragPointerId = -1;
            public Vector2 semiSleepDragScreenStart;
            public Rect semiSleepDragWindowStart;
            public ESWindowVisualState semiSleepDragStartState;
            public Rect semiSleepDragPendingBounds;
            public float semiSleepDragPendingEdgeOffset;
            public bool hasSemiSleepDragPendingBounds;
            public bool semiSleepDragging;
            public bool semiSleepRecaptureScheduled;
            public bool semiSleepManualHold;
            public bool pointerInside;
            public int interactionHoldCount;
            public ESWindowEdge edge;
            public float edgeOffset;
            public bool pinned;
            public int busyCount;
            public ESWindowActivityState activityState;
            public string activityMessage;
            public string activityPageId;
            public string activityContext;
            public bool focusModeForcedSleep;
            public ES.ESWindowSleepLinkMode sleepLinkMode;
            public EditorWindow sleepOwner;
            public bool sleepOwnerForcedSleep;
            public bool sleepLinkSyncing;
            public bool ownedSurfacePreviousSupports;
            public bool ownedSurfacePreviousAllow;
            public HashSet<string> registeredSleepOwnerKeys;
            public bool restorePersistedSleepOnBind;
            public bool restorePersistedSleepScheduled;
            public VisualElement pendingPanelRoot;
            public bool lifecycleSuspended;
            public double persistedSleepGeometryVerifyUntil = -1d;
            public bool persistedSleepGeometryRepairScheduled;
        }

        private sealed class PendingSleepOwner
        {
            internal EditorWindow child;
            internal string ownerKey;
            internal ES.ESWindowSleepLinkMode mode;
        }

        private static readonly List<PendingSleepOwner> pendingSleepOwners =
            new List<PendingSleepOwner>(8);
        private static readonly List<WindowBinding> sameTypeBindingScratch =
            new List<WindowBinding>(4);
        private static readonly HashSet<Type> singleInstanceWarnings =
            new HashSet<Type>();

        private const float GlobalAccentLineHeight = 2f;
        private const float GlobalSweepDuration = 0.52f;
        internal const float SemiSleepSize = 100f;
        internal const float SemiSleepDelay = 1.6f;
        internal const float SemiSleepDuration = 0.56f;
        internal const float SleepTileToEdgeTabDelay = 5f;
        internal const float EdgeTabHoverCommitDelay = 1.65f;
        internal const float EdgeTabHoverIntentDelay = 0.12f;
        internal const float EdgeTabCollapsedLength = 56f;
        internal const float EdgeTabExpandedLength = 196f;
        internal const float EdgeTabThickness = 44f;
        internal const float EdgeTabSnapDistance = 72f;
        private const float EdgeTabTransitionDuration = 0.22f;
        private const float EdgeTabHoverDuration = 0.16f;
        private const float EdgeTabToTileDuration = 0.30f;
        private const float EdgeTabMinimumReverseDuration = 0.045f;
        private const float EdgeTabPointerIntentDistance = 3f;
        private const float EdgeTabHoverExitGrace = 0.22f;
        private const float TransientInteractionGrace = 1.5f;
        private const double PersistedSleepGeometryVerificationDuration = 1.25d;
        private const float SemiSleepTrayGap = 8f;
        private const float SemiSleepTrayMargin = 12f;
        private const float SemiSleepDragThreshold = 6f;
        private const int SemiSleepPreferenceSchemaVersion = 1;
        private const int ResumeBindingsRetryBurstLimit = 4;
        private const string SemiSleepPreferenceKey = "ES.EditorPresentation.SemiSleep.Enabled";
        private const string SemiSleepWindowPreferencePrefix = "ES.EditorPresentation.SemiSleep.Window.";
        private static bool globalEditorAdaptersInstalled;
        private static bool globalEditorAdapterLifecycleInstalled;
        private static bool deepSkinSyncQueued;
        private static bool? semiSleepEnabledCache;
        private static bool semiSleepUpdateSubscribed;
        private static bool semiSleepAnyAnimating;
        private static double nextSemiSleepIdleCheckAt;
        private static bool windowLifecycleHooksInstalled;
        private static bool domainReloadInProgress;
        private static bool failedCompilationRecoveryScheduled;
        private static bool resumeBindingsRetryScheduled;
        private static bool resumeBindingsRetryRequested;
        private static int resumeBindingsRetryAttempt;
        private static bool editorQuitting;
        // Play Mode temporarily restores native editor frames so Unity can run
        // without ES overlay controls. Keep the pre-play sleep preference alive
        // across both ExitingEditMode and EnteredPlayMode notifications; the
        // latter must not overwrite it with the already-awake frame.
        private static bool playModeBindingsSuspended;
        // Root detachment can run before the subscribed beforeAssemblyReload
        // callback. Capture once so that a later restore cannot overwrite the
        // user's sleeping preference with the temporary awake frame.
        private static bool assemblyReloadPreferencesCaptured;

        internal static int BoundWindowCount => windowBindings.Count;

        internal static ESWindowPresentationHealthSnapshot CaptureWindowHealthSnapshot()
        {
            int liveWindowCount = 0;
            int sleepSupportedCount = 0;
            int sleepingCount = 0;
            int transitioningCount = 0;
            int duplicateWindowInstanceCount = 0;
            int missingSystemHostCount = 0;
            int geometryMismatchCount = 0;
            int staleEntryCount = 0;
            bool resumeRetryExhausted = HasExhaustedResumeWindowBinding();
            string firstIssueWindowType = resumeRetryExhausted
                ? "Presentation 恢复重试耗尽"
                : null;
            windowHealthCoordinatorScratch.Clear();

            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding?.window == null)
                {
                    staleEntryCount++;
                    firstIssueWindowType ??= "失效窗口绑定槽";
                    continue;
                }

                liveWindowCount++;
                Type concreteType = binding.window.GetType();
                if (!windowHealthCoordinatorScratch.TryGetValue(
                        concreteType,
                        out string expectedCoordinatorId))
                {
                    windowHealthCoordinatorScratch.Add(
                        concreteType,
                        binding.multiInstanceCoordinatorId);
                }
                else if (string.IsNullOrEmpty(expectedCoordinatorId)
                    || string.IsNullOrEmpty(binding.multiInstanceCoordinatorId)
                    || !string.Equals(
                        expectedCoordinatorId,
                        binding.multiInstanceCoordinatorId,
                        StringComparison.Ordinal))
                {
                    duplicateWindowInstanceCount++;
                    firstIssueWindowType ??= concreteType.FullName;
                    // Once a type has mixed or missing coordinator identities, every
                    // additional instance remains unsafe for this snapshot.
                    windowHealthCoordinatorScratch[concreteType] = null;
                }
                if (binding.supportsSemiSleep)
                {
                    sleepSupportedCount++;
                    if (!binding.lifecycleSuspended
                        && FindDeclaredSystemActionHost(binding) == null)
                    {
                        missingSystemHostCount++;
                        firstIssueWindowType ??= binding.window.GetType().FullName;
                    }
                }

                if (IsSleepingOrTargetingSleep(binding))
                    sleepingCount++;
                if (binding.semiSleepAnimating)
                    transitioningCount++;
                if (!binding.lifecycleSuspended
                    && HasSettledSemiSleepGeometryMismatch(binding))
                {
                    geometryMismatchCount++;
                    firstIssueWindowType ??= binding.window.GetType().FullName;
                }
            }

            int pendingOwnerCount = 0;
            for (int i = 0; i < pendingSleepOwners.Count; i++)
            {
                PendingSleepOwner pending = pendingSleepOwners[i];
                if (pending?.child == null)
                {
                    staleEntryCount++;
                    firstIssueWindowType ??= "失效 PendingFollowOwner";
                    continue;
                }
                pendingOwnerCount++;
            }

            return new ESWindowPresentationHealthSnapshot(
                windowBindings.Count,
                liveWindowCount,
                sleepSupportedCount,
                sleepingCount,
                transitioningCount,
                duplicateWindowInstanceCount,
                missingSystemHostCount,
                geometryMismatchCount,
                pendingOwnerCount,
                staleEntryCount,
                resumeRetryExhausted,
                firstIssueWindowType);
        }

        private static bool HasExhaustedResumeWindowBinding()
        {
            foreach (int id in resumeBindingsRetryExhaustedWindowIds)
            {
                if (windowBindings.TryGetValue(id, out WindowBinding binding)
                    && binding != null
                    && binding.window != null
                    && binding.lifecycleSuspended)
                    return true;
            }

            return false;
        }

        internal static void BeginSemiSleepPerformanceSample()
        {
            semiSleepPerformanceSampleActive = true;
            semiSleepPerformanceSampleStartedAt = EditorApplication.timeSinceStartup;
            semiSleepPerformanceUpdateCount = 0L;
            semiSleepPerformanceBindingVisitCount = 0L;
            semiSleepPerformanceNativePositionCommitCount = 0L;
            semiSleepPerformanceRepaintRequestCount = 0L;
            semiSleepPerformanceUpdateElapsedTicks = 0L;
            semiSleepPerformanceUpdateAllocatedBytes = 0L;
            semiSleepPerformanceMaximumUpdateElapsedTicks = 0L;
            semiSleepPerformanceMaximumAllocatedBytesPerUpdate = 0L;
        }

        internal static ESWindowSemiSleepPerformanceSample EndSemiSleepPerformanceSample()
        {
            double duration = semiSleepPerformanceSampleActive
                ? Math.Max(0d, EditorApplication.timeSinceStartup - semiSleepPerformanceSampleStartedAt)
                : 0d;
            semiSleepPerformanceSampleActive = false;
            return new ESWindowSemiSleepPerformanceSample(
                windowBindings.Count,
                semiSleepPerformanceUpdateCount,
                semiSleepPerformanceBindingVisitCount,
                semiSleepPerformanceNativePositionCommitCount,
                semiSleepPerformanceRepaintRequestCount,
                semiSleepPerformanceUpdateElapsedTicks,
                semiSleepPerformanceUpdateAllocatedBytes,
                semiSleepPerformanceMaximumUpdateElapsedTicks,
                semiSleepPerformanceMaximumAllocatedBytesPerUpdate,
                duration);
        }

        private static void CommitSemiSleepWindowPosition(WindowBinding binding, Rect position)
        {
            if (binding?.window == null)
                return;
            using (SemiSleepNativePositionProfilerMarker.Auto())
                binding.window.position = position;
            if (semiSleepPerformanceSampleActive)
                semiSleepPerformanceNativePositionCommitCount++;
        }

        private static void RequestSemiSleepRepaint(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            using (SemiSleepRepaintProfilerMarker.Auto())
                binding.window.Repaint();
            if (semiSleepPerformanceSampleActive)
                semiSleepPerformanceRepaintRequestCount++;
        }

        private const string WorkspaceSessionKeyPrefix = "ES.EditorPresentation.Workspace.";
        private static int focusModeWindowId;
        private static int lastFocusedWindowId;

        [Serializable]
        private sealed class WorkspaceSnapshot
        {
            public List<WorkspaceWindowSnapshot> windows = new List<WorkspaceWindowSnapshot>();
        }

        [Serializable]
        private sealed class WorkspaceWindowSnapshot
        {
            public string typeName;
            public int typeIndex;
            public Rect bounds;
            public bool pinned;
            public bool allowSemiSleep;
            public string pageId;
            public int focusOrder;
        }

        [Serializable]
        private sealed class SemiSleepWindowPreferences
        {
            public int schemaVersion;
            public string presentationShortTitle;
            public bool allowSemiSleep = true;
            public bool pinned;
            public bool sleeping;
            public int visualState;
            public int edge;
            public float edgeOffset;
            public Rect awakeBounds;
            public Rect dockBounds;
            public bool hasDockBounds;
        }

        private sealed class EmptyWindowLease : IDisposable
        {
            internal static readonly EmptyWindowLease Instance = new EmptyWindowLease();
            public void Dispose() { }
        }

        private sealed class WindowBusyLease : IDisposable
        {
            private EditorWindow window;

            internal WindowBusyLease(EditorWindow window)
            {
                this.window = window;
            }

            public void Dispose()
            {
                EditorWindow current = window;
                window = null;
                if (current == null
                    || !windowBindings.TryGetValue(current.GetInstanceID(), out WindowBinding binding)
                    || binding == null)
                    return;
                binding.busyCount = Mathf.Max(0, binding.busyCount - 1);
                if (binding.busyCount == 0)
                {
                    binding.activityState = ESWindowActivityState.Active;
                    binding.activityMessage = null;
                    binding.activityPageId = null;
                    PulseWindow(current, ESStatusKind.Ready);
                }
                RefreshSemiSleepUpdateSubscription();
            }
        }

        private sealed class WindowInteractionLease : IDisposable
        {
            private EditorWindow window;

            internal WindowInteractionLease(EditorWindow window)
            {
                this.window = window;
            }

            public void Dispose()
            {
                EditorWindow current = window;
                window = null;
                if (current == null
                    || !windowBindings.TryGetValue(current.GetInstanceID(), out WindowBinding binding)
                    || binding == null)
                    return;
                binding.interactionHoldCount = Mathf.Max(0, binding.interactionHoldCount - 1);
                RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
                RefreshSemiSleepUpdateSubscription();
            }
        }

        /// <summary>
        /// 安装 ES 对 Unity 原生 Inspector/SceneView 的轻量表现适配。只订阅官方绘制回调，
        /// 不枚举 EditorWindow、不扫描资产、不修改 Unity 控件布局或业务数据。
        /// </summary>
        public static void InstallGlobalEditorAdapters()
        {
            EnsurePlayModeLifecycleHook();

            if (!GlobalEditorShellEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            InstallGlobalEditorAdapterCallbacks();
            // A PlayMode transition can finish while the global ES shell is
            // disabled. In that case EnteredEditMode deliberately leaves the
            // dormant binding slots intact, so re-enabling the shell must
            // rebuild their visual hosts and persisted sleep state here.
            // Avoid competing with the lifecycle callbacks while a compile is
            // still tearing down or reconstructing editor panels.
            if (!domainReloadInProgress
                && !EditorApplication.isCompiling)
                ResumeWindowBindings();
            QueueDeepSkinSynchronization();
        }

        /// <summary>全局 ES 外观是否由主题启用，且当前处于编辑模式。</summary>
        public static bool GlobalEditorShellEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return (current == null || current.enableGlobalEditorShell)
                    && !EditorApplication.isPlayingOrWillChangePlaymode;
            }
        }

        /// <summary>主题/皮肤缓存世代，供已接入窗口判断是否需要重建样式。</summary>
        internal static int ThemeGeneration => themeGeneration;

        private static void InstallGlobalEditorAdapterCallbacks()
        {
            if (globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            SceneView.duringSceneGui += DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = true;
        }

        /// <summary>卸载全局适配，供测试、域重载和受控关闭路径使用。</summary>
        public static void UninstallGlobalEditorAdapters()
        {
            UninstallGlobalEditorAdapterCallbacks();
            EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
            globalEditorAdapterLifecycleInstalled = false;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
            windowLifecycleHooksInstalled = false;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;
            ESGlobalEditorSkinExperiment.Restore();
            playModeBindingsSuspended = false;
            assemblyReloadPreferencesCaptured = false;
            domainReloadInProgress = false;
            failedCompilationRecoveryScheduled = false;
            EditorApplication.delayCall -= ResumeWindowBindingsRetry;
            resumeBindingsRetryScheduled = false;
            resumeBindingsRetryRequested = false;
            resumeBindingsRetryAttempt = 0;
            resumeBindingsRetryExhaustedWindowIds.Clear();
            EditorApplication.delayCall -= RecoverSemiSleepAfterFailedCompilation;
            UnbindAllWindowBindings();
        }

        private static void UninstallGlobalEditorAdapterCallbacks()
        {
            if (!globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = false;
        }

        private static void OnGlobalPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                UninstallGlobalEditorAdapterCallbacks();
                SuspendWindowBindings();
                ESGlobalEditorSkinExperiment.Restore();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                InstallGlobalEditorAdapters();
                // InstallGlobalEditorAdapters owns the single resume attempt.
                // Do not call ResumeWindowBindings a second time in the same
                // lifecycle callback: a panel can be rebuilt between the two
                // calls, which would create an avoidable detach/attach race.
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
                SceneView.RepaintAll();
            }
        }

        private static void QueueDeepSkinSynchronization()
        {
            if (deepSkinSyncQueued)
                return;

            deepSkinSyncQueued = true;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            EditorApplication.delayCall += SynchronizeDeepSkinWithTheme;
        }

        private static void SynchronizeDeepSkinWithTheme()
        {
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;

            ESGlobalEditorTheme current = CurrentTheme;
            bool shouldApply = GlobalEditorShellEnabled
                && current != null
                && current.enableDeepEditorSkin;
            ESGlobalEditorSkinExperiment.Synchronize(shouldApply);
        }

        private static void UnbindAllWindowBindings()
        {
            try
            {
                if (windowBindings.Count == 0)
                    return;

                var bindings = new List<KeyValuePair<int, WindowBinding>>(windowBindings);
                for (int i = 0; i < bindings.Count; i++)
                {
                    try
                    {
                        UnbindWindowBinding(
                            bindings[i].Key,
                            bindings[i].Value,
                            false,
                            false);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                windowBindings.Clear();
                windowBindingsByRoot.Clear();
                sleepOwnerBindingsByKey.Clear();
                pendingSleepOwners.Clear();
                resumeBindingsRetryExhaustedWindowIds.Clear();
                RefreshSemiSleepUpdateSubscription();
            }
        }

        private static void UnregisterWindowCallbacks(WindowBinding binding)
        {
            if (binding == null || binding.root == null)
                return;

            binding.root.UnregisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerEnterEvent>(OnWindowPointerEnter, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerLeaveEvent>(OnWindowPointerLeave, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerMoveEvent>(OnWindowPointerMove, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<WheelEvent>(OnWindowWheel, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            binding.root.UnregisterCallback<DetachFromPanelEvent>(OnWindowRootDetached);
            binding.root.UnregisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            binding.semiSleepOverlay?.UnregisterCallback<PointerDownEvent>(
                OnSemiSleepOverlayPointerDown,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerEnterEvent>(
                OnSemiSleepOverlayPointerEnter,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerLeaveEvent>(
                OnSemiSleepOverlayPointerLeave,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerMoveEvent>(
                OnSemiSleepOverlayPointerMove,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerUpEvent>(
                OnSemiSleepOverlayPointerUp,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerCancelEvent>(
                OnSemiSleepOverlayPointerCancel,
                TrickleDown.TrickleDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerCaptureOutEvent>(
                OnSemiSleepOverlayPointerCaptureOut,
                TrickleDown.TrickleDown);
            windowBindingsByRoot.Remove(binding.root);
        }

        private static void UnregisterPendingPanelAttach(WindowBinding binding)
        {
            if (binding?.pendingPanelRoot == null)
                return;
            binding.pendingPanelRoot.UnregisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            binding.pendingPanelRoot = null;
        }

        private static void QueueResumeOnPanelAttach(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            VisualElement root = binding.window.rootVisualElement;
            if (root == null || root.panel != null)
                return;
            if (binding.pendingPanelRoot != null
                && !ReferenceEquals(binding.pendingPanelRoot, root))
                binding.pendingPanelRoot.UnregisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            root.UnregisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            root.RegisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            binding.pendingPanelRoot = root;
        }

        private static void OnWindowRootAttached(AttachToPanelEvent evt)
        {
            VisualElement root = evt.currentTarget as VisualElement;
            if (root == null)
                return;
            root.UnregisterCallback<AttachToPanelEvent>(OnWindowRootAttached);
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || !ReferenceEquals(binding.pendingPanelRoot, root))
                    continue;
                binding.pendingPanelRoot = null;
                QueueResumeWindowBindingsRetry(true);
                break;
            }
        }

        private static void StopTransientWindowVisuals(
            WindowBinding binding,
            bool restoreOpeningFrame)
        {
            if (binding == null)
                return;
            if (binding.window != null)
                ESWindowFrameActivation.Stop(binding.window, restoreOpeningFrame);
            ESWindowOpeningSweep.Stop(binding.root);
        }

        private static void StopTransientWindowVisuals(bool restoreOpeningFrame)
        {
            foreach (WindowBinding binding in windowBindings.Values)
                StopTransientWindowVisuals(binding, restoreOpeningFrame);
        }

        private static void CapturePlayModePreferences()
        {
            if (playModeBindingsSuspended)
                return;
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && !binding.lifecycleSuspended)
                    SaveSemiSleepPreferences(binding);
            playModeBindingsSuspended = true;
        }

        private static void CaptureAssemblyReloadPreferences()
        {
            if (assemblyReloadPreferencesCaptured)
                return;

            // PlayMode has already captured the user's sleeping state and
            // deliberately restored native editor frames. A compile/reload
            // notification can still arrive while PlayMode is active; never
            // replace that snapshot with the temporary awake geometry.
            if (playModeBindingsSuspended)
            {
                assemblyReloadPreferencesCaptured = true;
                return;
            }

            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && !binding.lifecycleSuspended)
                    SaveSemiSleepPreferences(binding);
            assemblyReloadPreferencesCaptured = true;
        }

        private static void SuspendWindowBindings()
        {
            // Save only on the first play-mode notification. Unity emits both
            // ExitingEditMode and EnteredPlayMode; saving after the first restore
            // would replace the user's sleeping state with the awake frame.
            CapturePlayModePreferences();

            foreach (WindowBinding binding in windowBindings.Values)
                SuspendWindowBinding(binding, true);
            semiSleepAnyAnimating = false;
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// Temporarily detaches ES-owned visuals while retaining the binding
        /// record. Unity may call an EditorWindow's OnDisable during PlayMode
        /// transitions; deleting the record there makes EnteredEditMode unable
        /// to restore the window's persisted sleep state.
        /// </summary>
        private static void SuspendWindowBinding(WindowBinding binding)
        {
            SuspendWindowBinding(binding, false);
        }

        private static void SuspendWindowBinding(
            WindowBinding binding,
            bool preserveSleepGeometry)
        {
            if (binding == null)
                return;
            if (!binding.lifecycleSuspended)
            {
                CaptureWindowPreferencesForSuspend(binding);
                binding.lifecycleSuspended = true;
            }
            StopTransientWindowVisuals(binding, !preserveSleepGeometry);
            binding.animation?.Pause();
            binding.animation = null;

            // PlayMode/reload suspension preserves the user's actual sleep
            // rectangle. If the current panel is still alive, keep the
            // Editor-only overlay visible and keep it hit-testable so pointer
            // events do not fall through into the sleeping page. Lifecycle
            // guards below consume the callbacks without waking or moving it.
            if (preserveSleepGeometry
                && binding.root != null
                && binding.root.panel != null
                && binding.semiSleepOverlay != null)
            {
                binding.semiSleepOverlay.pickingMode = PickingMode.Position;
                binding.lifecycleSuspended = true;
                return;
            }

            if (!preserveSleepGeometry)
                RestoreSemiSleep(binding, true, true);
            UnregisterPendingPanelAttach(binding);
            UnregisterWindowCallbacks(binding);
            binding.host?.RemoveFromHierarchy();
            if (binding.semiSleepOverlay != null)
            {
                binding.semiSleepOverlay.userData = null;
                binding.semiSleepOverlay.RemoveFromHierarchy();
            }
            RemoveSemiSleepControls(binding);
            RemoveBrandTypography(binding.root);
            binding.host = null;
            binding.accentLine = null;
            binding.sweep = null;
            binding.semiSleepOverlay = null;
            binding.semiSleepMonogram = null;
            binding.semiSleepIcon = null;
            binding.semiSleepTitleLabel = null;
            binding.semiSleepPromotionProgress = null;
            binding.semiSleepDockProgress = null;
            binding.root = null;
            // Action hosts belong to the detached panel just like the ES overlay.
            // Retaining them lets a later BindWindow attach controls to a dead
            // VisualElement when Unity rebuilds the root on the same window
            // instance. The next resume either receives fresh caller-owned hosts
            // or creates the normal-flow standard System host.
            binding.actionHosts = null;
        }

        private static void SuspendWindowBindingForPanelRetry(
            WindowBinding binding,
            ES.ESWindowActionHosts resumableActionHosts)
        {
            // Panel replacement is an infrastructure event, not a user wake.
            // Keep the captured sleep geometry while the new root is attached.
            SuspendWindowBinding(binding, true);
            VisualElement root = binding?.window?.rootVisualElement;
            if (resumableActionHosts != null && root != null)
            {
                try
                {
                    resumableActionHosts.ValidateOwnership(root);
                    binding.actionHosts = resumableActionHosts;
                }
                catch (InvalidOperationException)
                {
                    // The root was replaced while the panel was being rebuilt.
                    // A later explicit BindWindow call must supply fresh hosts.
                }
            }
            QueueResumeOnPanelAttach(binding);
        }

        private static void CaptureWindowPreferencesForSuspend(WindowBinding binding)
        {
            if (binding == null || binding.lifecycleSuspended)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                CapturePlayModePreferences();
            else if (domainReloadInProgress || EditorApplication.isCompiling)
                CaptureAssemblyReloadPreferences();
            else
                SaveSemiSleepPreferences(binding);
        }

        private static void PruneDeadWindowBindings()
        {
            List<KeyValuePair<int, WindowBinding>> dead = null;
            foreach (KeyValuePair<int, WindowBinding> pair in windowBindings)
            {
                if (pair.Value?.window != null)
                    continue;
                dead ??= new List<KeyValuePair<int, WindowBinding>>();
                dead.Add(pair);
            }
            if (dead == null)
                return;
            for (int i = 0; i < dead.Count; i++)
                UnbindWindowBinding(dead[i].Key, dead[i].Value, true);
        }

        private static bool ResumeWindowBindings(bool resetRetryBudget = true)
        {
            if (resetRetryBudget)
            {
                resumeBindingsRetryAttempt = 0;
                resumeBindingsRetryExhaustedWindowIds.Clear();
            }
            if (!GlobalEditorShellEnabled)
                return false;

            // EnteredEditMode can be reported before compilation and panel
            // reconstruction have settled. Do not create a second visual layer
            // during that transient turn; the delayed retry will run after the
            // lifecycle owner is stable.
            if (domainReloadInProgress || EditorApplication.isCompiling)
            {
                QueueResumeWindowBindingsRetry();
                return false;
            }

            // A window can be closed while PlayMode is active. Its OnDisable
            // intentionally leaves a dormant binding so lifecycle restoration
            // remains possible; remove that binding once Unity reports the
            // object as destroyed before rebuilding the live set.
            PruneDeadWindowBindings();
            bool needsPanelRetry = false;
            bool waitingForPanel = false;
            bool awaitingExplicitHosts = false;
            foreach (KeyValuePair<int, WindowBinding> pair in windowBindings)
            {
                WindowBinding binding = pair.Value;
                if (binding == null || binding.window == null)
                    continue;
                if (binding.window.rootVisualElement == null)
                {
                    // During a domain-reload-disabled compile Unity can keep the
                    // native EditorWindow alive while temporarily clearing its
                    // UI Toolkit root. Treat that as an incomplete restore, not
                    // as a successful no-op that would consume the snapshot.
                    QueueResumeWindowBindingsRetry();
                    waitingForPanel = true;
                    continue;
                }
                if (binding.window.rootVisualElement.panel == null)
                {
                    // EnteredEditMode and a domain-reload-disabled compile can
                    // report completion before UI Toolkit has attached the new
                    // panel. Do not register callbacks or create controls on a
                    // detached root; the window's CreateGUI/next editor turn
                    // will provide a valid host.
                    QueueResumeOnPanelAttach(binding);
                    waitingForPanel = true;
                    continue;
                }
                try
                {
                    if (binding.actionHosts != null)
                    {
                        try
                        {
                            binding.actionHosts.ValidateOwnership(binding.window.rootVisualElement);
                        }
                        catch (InvalidOperationException)
                        {
                            // The window rebuilt its root while Unity kept the
                            // same native instance. Recreate a normal-flow
                            // standard host; a later explicit BindWindow call may
                            // replace it with the window's business hosts.
                            binding.actionHosts = null;
                        }
                    }
                    if (binding.supportsSemiSleep
                        && (binding.actionHosts == null || binding.actionHosts.System == null)
                        && binding.actionHostsWereExplicit)
                    {
                        // A custom title/action layout is an explicit caller
                        // contract. Wait for its next BindWindow call rather
                        // than injecting a duplicate standard toolbar.
                        awaitingExplicitHosts = true;
                        resumeBindingsRetryRequested = true;
                        continue;
                    }
                    if (binding.supportsSemiSleep
                        && (binding.actionHosts == null || binding.actionHosts.System == null))
                    {
                        VisualElement bar = ES.ESWindowFoundation.EnsureStandardSystemActionBar(
                            binding.window);
                        binding.actionHosts = new ES.ESWindowActionHosts(
                            bar.Q<VisualElement>(ES.ESWindowFoundation.StandardSystemActionHostName));
                    }
                    VisualElement currentRoot = binding.window.rootVisualElement;
                    bool overlayNeedsRebuild = !ReferenceEquals(binding.root, currentRoot)
                        || binding.host == null
                        || binding.host.parent == null
                        || binding.semiSleepOverlay == null
                        || binding.semiSleepOverlay.parent == null;
                    if (overlayNeedsRebuild || binding.lifecycleSuspended)
                        LoadSemiSleepPreferences(binding);
                    if (overlayNeedsRebuild)
                        AttachWindowOverlay(binding);
                    else if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                        AttachSemiSleepControls(binding);
                    if (!IsWindowOverlayAttached(binding))
                    {
                        SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);
                        needsPanelRetry = true;
                        continue;
                    }
                    MarkWindowBindingResumed(pair.Key, binding);
                    EnsureWindowOverlayScheduledVisuals(binding);
                    if (binding.restorePersistedSleepOnBind
                        && binding.allowSemiSleep
                        && !binding.window.docked)
                        SchedulePersistedSemiSleepGeometryRestore(binding);
                }
                catch (Exception exception) when (
                    exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is MissingReferenceException
                    || exception is NullReferenceException)
                {
                    // Panel teardown can race the first EnteredEditMode turn.
                    // Return the binding to a clean dormant state and retry from
                    // the next editor turn instead of losing the whole restore.
                    SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);
                    needsPanelRetry = true;
                }
            }
            if (needsPanelRetry)
                QueueResumeWindowBindingsRetry();
            else
            {
                if (!awaitingExplicitHosts && !waitingForPanel)
                    CompleteResumeWindowBindingsRetry();
                // Keep the one-shot PlayMode snapshot alive until a real resume
                // succeeds. This prevents a compile/panel race from allowing a
                // later callback to capture the temporary awake frame again.
                if (!awaitingExplicitHosts
                    && !waitingForPanel
                    && !EditorApplication.isPlayingOrWillChangePlaymode
                    && !EditorApplication.isCompiling
                    && !domainReloadInProgress)
                {
                    bool completedPlayModeRestore = playModeBindingsSuspended;
                    playModeBindingsSuspended = false;
                    if (completedPlayModeRestore)
                    {
                        // A compile/reload inside PlayMode can mark assembly
                        // capture as handled without writing a second snapshot.
                        // Clear it only after the original PlayMode snapshot has
                        // been mounted successfully, including delayed retries.
                        assemblyReloadPreferencesCaptured = false;
                    }
                    else
                    {
                        // A failed compile may keep this AppDomain alive. Once
                        // the panel is actually rebound, the previous reload
                        // capture is no longer needed and the next reload must
                        // be allowed to capture a fresh stable state.
                        assemblyReloadPreferencesCaptured = false;
                    }
                }
            }
            RefreshSemiSleepUpdateSubscription();
            return !needsPanelRetry && !waitingForPanel && !awaitingExplicitHosts;
        }

        private static void QueueResumeWindowBindingsRetry(bool resetRetryBudget = false)
        {
            resumeBindingsRetryRequested = true;
            if (resetRetryBudget)
            {
                resumeBindingsRetryAttempt = 0;
                resumeBindingsRetryExhaustedWindowIds.Clear();
            }
            if (resumeBindingsRetryScheduled
                || editorQuitting
                || domainReloadInProgress
                || EditorApplication.isCompiling)
                return;
            if (resumeBindingsRetryAttempt >= ResumeBindingsRetryBurstLimit)
            {
                RecordExhaustedResumeWindowBindings();
                return;
            }

            resumeBindingsRetryAttempt++;
            resumeBindingsRetryScheduled = true;
            EditorApplication.delayCall -= ResumeWindowBindingsRetry;
            EditorApplication.delayCall += ResumeWindowBindingsRetry;
        }

        private static void RecordExhaustedResumeWindowBindings()
        {
            resumeBindingsRetryExhaustedWindowIds.Clear();
            foreach (KeyValuePair<int, WindowBinding> pair in windowBindings)
            {
                if (pair.Value != null && pair.Value.lifecycleSuspended)
                    resumeBindingsRetryExhaustedWindowIds.Add(pair.Key);
            }
        }

        private static void MarkWindowBindingResumed(int id, WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.lifecycleSuspended = false;
            if (binding.semiSleepOverlay != null)
                binding.semiSleepOverlay.pickingMode = PickingMode.Position;
            // A duplicate ungoverned instance may have been suspended while it
            // was still asleep. RefreshSingleInstanceSafetyForType cannot wake
            // it during lifecycle suspension; enforce the violation only after
            // the panel is live again, so it cannot remain a hidden competing
            // sleep owner after PlayMode/domain-reload recovery.
            if (binding.singleInstanceViolation && IsSleepingOrTargetingSleep(binding))
                RestoreSemiSleep(binding, true);
            resumeBindingsRetryExhaustedWindowIds.Remove(id);
        }

        private static void ResumeWindowBindingsRetry()
        {
            EditorApplication.delayCall -= ResumeWindowBindingsRetry;
            resumeBindingsRetryScheduled = false;
            if (editorQuitting
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !GlobalEditorShellEnabled)
                return;
            if (domainReloadInProgress || EditorApplication.isCompiling)
                return;
            if (!resumeBindingsRetryRequested)
                return;
            ResumeWindowBindings(false);
        }

        private static void CompleteResumeWindowBindingsRetry()
        {
            resumeBindingsRetryRequested = false;
            resumeBindingsRetryAttempt = 0;
            resumeBindingsRetryExhaustedWindowIds.Clear();
            if (!resumeBindingsRetryScheduled)
                return;
            EditorApplication.delayCall -= ResumeWindowBindingsRetry;
            resumeBindingsRetryScheduled = false;
        }

        private static void DrawGlobalInspectorHeader(UnityEditor.Editor editor)
        {
            if (editor == null || (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint))
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, GetDepthAccent(0));
        }

        private static void DrawGlobalSceneViewChrome(SceneView sceneView)
        {
            if (sceneView == null || Event.current.type != EventType.Repaint)
                return;

            Color previousGuiColor = GUI.color;
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            bool previousGuiEnabled = GUI.enabled;
            Handles.BeginGUI();
            Rect rect = new Rect(0f, 0f, sceneView.position.width, 2f);
            EditorGUI.DrawRect(rect, GetDepthAccent(0));
            Handles.EndGUI();
            GUI.color = previousGuiColor;
            GUI.matrix = previousGuiMatrix;
            GUI.enabled = previousGuiEnabled;
        }

        public static GUIStyle SurfaceStyle
        {
            get
            {
                EnsureSkin();
                if (surfaceStyle == null)
                {
                    surfaceStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, Metric(2f), Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(9f), Metric(7f), Metric(8f)),
                        border = CreateNineSlice(ESCornerRadiusToken.Section)
                    };
                    surfaceStyle.normal.background = SurfaceTexture;
                }

                return surfaceStyle;
            }
        }

        internal static int SkinGeneration
        {
            get { return skinGeneration + globalEditorSkinGeneration * 100000; }
        }

        internal static void NotifyGlobalEditorSkinChanged()
        {
            globalEditorSkinGeneration++;
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                EnsureSkin();
                if (headerStyle == null)
                {
                    headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(0, 0, 0, Metric(2f))
                    };
                    headerStyle.normal.textColor = cachedProSkin
                        ? new Color(0.83f, 0.85f, 0.88f, 1f)
                        : new Color(0.16f, 0.18f, 0.21f, 1f);
                    ApplyBrandFont(headerStyle);
                }

                return headerStyle;
            }
        }

        public static GUIStyle SubtitleStyle
        {
            get
            {
                EnsureSkin();
                if (subtitleStyle == null)
                {
                    subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        wordWrap = true,
                        padding = new RectOffset(0, 0, Metric(1f), Metric(3f))
                    };
                    subtitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.72f, 0.76f, 0.82f, 1f)
                        : new Color(0.39f, 0.42f, 0.45f, 1f);
                }

                return subtitleStyle;
            }
        }

        public static GUIStyle MetaStyle
        {
            get
            {
                EnsureSkin();
                if (metaStyle == null)
                {
                    metaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, Metric(1f))
                    };
                    metaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.70f, 0.74f, 0.80f, 1f)
                        : new Color(0.37f, 0.40f, 0.44f, 1f);
                }

                return metaStyle;
            }
        }

        /// <summary>
        /// Compact feedback-card primitives used by optional collection drawers. The visual
        /// language is intentionally generic and has no dependency on third-party editor code.
        /// </summary>
        public static float CompactCollectionHeaderHeight
        {
            get { return Mathf.Max(34f, Mathf.Round(36f * Density)); }
        }

        public static GUIStyle CompactCollectionTitleStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionTitleStyle == null)
                {
                    compactCollectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionTitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.88f, 0.90f, 0.93f, 1f)
                        : new Color(0.15f, 0.17f, 0.20f, 1f);
                    ApplyBrandFont(compactCollectionTitleStyle);
                }

                return compactCollectionTitleStyle;
            }
        }

        public static GUIStyle CompactCollectionMetaStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionMetaStyle == null)
                {
                    compactCollectionMetaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionMetaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.58f, 0.62f, 0.68f, 1f)
                        : new Color(0.38f, 0.41f, 0.45f, 1f);
                }

                return compactCollectionMetaStyle;
            }
        }

        public static GUIStyle CompactCollectionBodyStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyStyle == null)
                {
                    compactCollectionBodyStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, 0, Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(7f), Metric(6f), Metric(7f)),
                        border = CreateNineSlice(ESCornerRadiusToken.Card)
                    };
                    compactCollectionBodyStyle.normal.background = CompactCollectionBodyTexture;
                }

                return compactCollectionBodyStyle;
            }
        }

        public static GUIStyle ToolbarStyle
        {
            get
            {
                EnsureSkin();
                if (toolbarStyle == null)
                {
                    toolbarStyle = new GUIStyle(EditorStyles.toolbar)
                    {
                        padding = new RectOffset(Metric(4f), Metric(4f), Metric(2f), Metric(2f)),
                        border = CreateNineSlice(ESCornerRadiusToken.Control)
                    };
                    toolbarStyle.normal.background = ToolbarTexture;
                }
                return toolbarStyle;
            }
        }

        public static GUIStyle ToolbarButtonStyle
        {
            get
            {
                EnsureSkin();
                if (toolbarButtonStyle == null)
                {
                    toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        padding = new RectOffset(Metric(7f), Metric(7f), Metric(2f), Metric(2f)),
                        border = CreateNineSlice(ESCornerRadiusToken.Control)
                    };
                    ApplyButtonState(
                        toolbarButtonStyle,
                        ToolbarButtonTexture,
                        ToolbarButtonHoverTexture,
                        ToolbarButtonActiveTexture,
                        SectionSelectedTextColor,
                        PrimaryActionTextColor);
                }
                return toolbarButtonStyle;
            }
        }

        public static GUIStyle PrimaryButtonStyle
        {
            get
            {
                EnsureSkin();
                if (primaryButtonStyle == null)
                {
                    primaryButtonStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(Metric(10f), Metric(10f), Metric(3f), Metric(3f)),
                        border = CreateNineSlice(ESCornerRadiusToken.Control)
                    };
                    ApplyButtonState(
                        primaryButtonStyle,
                        PrimaryButtonTexture,
                        PrimaryButtonHoverTexture,
                        PrimaryButtonActiveTexture,
                        PrimaryActionTextColor,
                        PrimaryActionTextColor);
                }

                return primaryButtonStyle;
            }
        }

        public static Color DividerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.30f, 0.32f, 0.35f, 1f)
                    : new Color(0.72f, 0.74f, 0.76f, 1f);
            }
        }

        /// <summary>
        /// Low-priority category accents borrowed from the FolderSystem ES_Logic artwork.
        /// They are for module identity only and must not replace semantic status colors.
        /// </summary>
        public static Color LogicSteelBlue
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.29f, 0.51f, 0.66f, 0.96f)
                    : new Color(0.24f, 0.46f, 0.62f, 0.96f);
            }
        }

        public static Color LogicGold
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.78f, 0.69f, 0.14f, 0.96f)
                    : new Color(0.68f, 0.49f, 0.06f, 0.96f);
            }
        }

        public static bool IsProSkin
        {
            get
            {
                EnsureSkin();
                return cachedProSkin;
            }
        }

        public static bool ShowSectionSubtitle
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.showSectionSubtitle;
            }
        }

        public static float Density
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 1f : Mathf.Clamp(current.density, 0.85f, 1.20f);
            }
        }

        /// <summary>
        /// Whether optional ES feedback motion is enabled. Motion is presentation-only;
        /// disabling it never hides status text, icons or validation information.
        /// </summary>
        public static bool MotionEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.enableMotion;
            }
        }

        /// <summary>
        /// 用户显式开启的浮动 ES 工具窗口半休眠。全局开关与窗口级偏好均只保存稳定标量。
        /// </summary>
        public static bool SemiSleepEnabled
        {
            get
            {
                if (!semiSleepEnabledCache.HasValue)
                    semiSleepEnabledCache = EditorPrefs.GetBool(SemiSleepPreferenceKey, false);
                return semiSleepEnabledCache.Value;
            }
        }

        public static void SetSemiSleepEnabled(bool enabled)
        {
            if (SemiSleepEnabled != enabled)
            {
                semiSleepEnabledCache = enabled;
                EditorPrefs.SetBool(SemiSleepPreferenceKey, enabled);
                if (!enabled)
                    RestoreAutomaticSemiSleepWindows();
            }
            RefreshAllSemiSleepControls();
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// 绑定一个 ES 编辑器窗口到共享 Presentation 层。绑定只持有当前域内的活动窗口，
        /// 不会扫描全部 EditorWindow，也不会把窗口引用写入资产或 SessionState。
        /// </summary>
        public static void BindWindow(
            EditorWindow window,
            bool allowSemiSleep = true,
            ES.ESWindowActionHosts actionHosts = null)
        {
            if (window == null || window.rootVisualElement == null)
                return;

            ES.ESWindowFoundation.ValidateDeclaredSleepContract(window, allowSemiSleep);

            // During PlayMode or an assembly compile the ES overlay must stay
            // detached, but the binding itself still has to be registered. A
            // domain reload in PlayMode reconstructs EditorWindows while the
            // global shell is disabled; dropping these registrations would make
            // EnteredEditMode unable to resume presentation or persisted sleep.
            bool lifecycleSuspended = EditorApplication.isPlayingOrWillChangePlaymode
                || domainReloadInProgress
                || EditorApplication.isCompiling;
            if (!GlobalEditorShellEnabled && !lifecycleSuspended)
                return;
            bool callerProvidedActionHosts = actionHosts != null;
            if (actionHosts != null)
            {
                if (actionHosts.System != null)
                {
                    ES.ESWindowFoundation.ValidateFullLifecycleSurfaceCapability(
                        window,
                        "System 动作宿主");
                }
                try
                {
                    actionHosts.ValidateOwnership(window.rootVisualElement);
                }
                catch (InvalidOperationException) when (lifecycleSuspended)
                {
                    // The caller may still hold hosts from the previous panel.
                    // They are not valid during PlayMode/compile suspension;
                    // discard them and let the next CreateGUI bind fresh hosts.
                    actionHosts = null;
                }
            }
            EnsureWindowLifecycleHooks();

            int id = window.GetInstanceID();
            bool hadBindingSlot = windowBindings.TryGetValue(id, out WindowBinding binding);
            if (!hadBindingSlot || binding == null || binding.window != window)
            {
                if (binding != null)
                    UnbindWindowBinding(id, binding, false, false);
                else if (hadBindingSlot)
                    RemoveNullWindowBindingRoots();

                binding = new WindowBinding
                {
                    window = window,
                    multiInstanceCoordinatorId = ResolveMultiInstanceCoordinatorId(window),
                    allowSemiSleep = allowSemiSleep,
                    supportsSemiSleep = allowSemiSleep,
                    visualState = ESWindowVisualState.ActivePanel,
                    transitionTargetState = ESWindowVisualState.ActivePanel,
                    lastInteractionAt = EditorApplication.timeSinceStartup,
                    activationPending = true,
                    pulseStatus = ESStatusKind.None,
                    pulseDuration = GlobalSweepDuration,
                    actionHostsWereExplicit = callerProvidedActionHosts
                };
                LoadSemiSleepPreferences(binding);
                if (!binding.allowSemiSleep || window.docked)
                {
                    binding.semiSleepTarget = false;
                    binding.restorePersistedSleepOnBind = false;
                    binding.visualState = ESWindowVisualState.ActivePanel;
                    binding.transitionTargetState = ESWindowVisualState.ActivePanel;
                }
                windowBindings[id] = binding;
            }
            else
            {
                // Once a window has declared custom hosts, keep that contract
                // until it is explicitly rebound with fresh hosts. A later
                // parameterless BindWindow must not silently introduce a
                // second standard System bar beside the custom toolbar.
                if (callerProvidedActionHosts)
                    binding.actionHostsWereExplicit = true;
                bool wasSupported = binding.supportsSemiSleep;
                binding.supportsSemiSleep = allowSemiSleep;
                if (wasSupported && !allowSemiSleep)
                {
                    binding.allowSemiSleep = false;
                    RestoreSemiSleep(binding, true);
                }
                else if (!wasSupported && allowSemiSleep)
                {
                    // A temporary opt-out (OwnedSurface/dialog-like phase) must not leave
                    // the window permanently disabled when its standard host returns.
                    // Restore the user's persisted choice when present; otherwise the
                    // standard base-class default remains enabled.
                    binding.allowSemiSleep = true;
                    LoadSemiSleepPreferences(binding);
                }
            }

            bool rootChanged = !ReferenceEquals(binding.root, window.rootVisualElement);
            RefreshSingleInstanceSafetyForType(window.GetType());

            // Keep the dormant binding and its caller-owned action hosts. All
            // visual elements are attached by ResumeWindowBindings after
            // EnteredEditMode, when Unity has finished rebuilding the panel.
            if (lifecycleSuspended)
            {
                // Ownership validation above distinguishes fresh hosts on a
                // detached current root from stale hosts on a replaced root.
                SuspendWindowBindingForPanelRetry(binding, actionHosts);
                QueueResumeWindowBindingsRetry();
                return;
            }

            if (window.rootVisualElement.panel == null)
            {
                // Create the logical binding before Show/Attach so owner and
                // capability contracts are immediately available, but do not
                // register callbacks, schedules or visual controls on a
                // detached root.
                SuspendWindowBindingForPanelRetry(binding, actionHosts);
                QueueResumeWindowBindingsRetry();
                RefreshSemiSleepUpdateSubscription();
                return;
            }

            if (rootChanged)
            {
                // A normal UI Toolkit panel rebuild does not necessarily pass
                // through AssemblyReloadEvents. Re-read the last stable scalar
                // snapshot before attaching the new root so a sleeping window
                // cannot become visually awake with a stale in-memory target.
                binding.actionHosts = null;
                LoadSemiSleepPreferences(binding);
            }

            // A complete-sleep binding always has a normal-flow System host.
            // Keep compatibility for older callers by creating that host via
            // the explicit standard-bar factory, never through an absolute
            // overlay fallback.
            if (allowSemiSleep
                && (actionHosts == null || actionHosts.System == null)
                && !binding.actionHostsWereExplicit)
            {
                VisualElement bar = ES.ESWindowFoundation.EnsureStandardSystemActionBar(window);
                actionHosts = new ES.ESWindowActionHosts(
                    bar.Q<VisualElement>(ES.ESWindowFoundation.StandardSystemActionHostName),
                    actionHosts?.Global,
                    actionHosts?.Window);
            }

            if (actionHosts != null && binding.actionHosts != actionHosts)
            {
                RemoveSemiSleepControls(binding);
                binding.actionHosts = actionHosts;
            }
            // Unity may replace rootVisualElement while retaining the same
            // EditorWindow instance (notably during panel recreation and some
            // PlayMode/ReloadDomain transitions). A non-null old host is not
            // evidence that it belongs to the current root.
            if (!ReferenceEquals(binding.root, window.rootVisualElement)
                || binding.host == null
                || binding.host.parent == null)
                AttachWindowOverlay(binding);
            else if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                AttachSemiSleepControls(binding);
            else if (!binding.supportsSemiSleep)
                RemoveSemiSleepControls(binding);
            if (!IsWindowOverlayAttached(binding))
            {
                SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);
                QueueResumeWindowBindingsRetry();
                RefreshSemiSleepUpdateSubscription();
                return;
            }
            MarkWindowBindingResumed(id, binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            if (binding.semiSleepTarget && binding.allowSemiSleep && !binding.window.docked)
                SchedulePersistedSemiSleepGeometryRestore(binding);

            if (playModeBindingsSuspended || resumeBindingsRetryRequested)
                ResumeWindowBindings();
        }

        private static string ResolveMultiInstanceCoordinatorId(EditorWindow window)
        {
            if (!(window is ES.IESWindowMultiInstanceContract contract))
                return null;
            string coordinatorId = contract.ESWindow_MultiInstanceCoordinatorId;
            return string.IsNullOrWhiteSpace(coordinatorId)
                ? null
                : coordinatorId.Trim();
        }

        private static void RefreshSingleInstanceSafetyForType(Type concreteType)
        {
            if (concreteType == null)
                return;

            sameTypeBindingScratch.Clear();
            foreach (WindowBinding candidate in windowBindings.Values)
                if (candidate?.window != null && candidate.window.GetType() == concreteType)
                    sameTypeBindingScratch.Add(candidate);

            sameTypeBindingScratch.Sort(CompareWindowBindings);
            string coordinatorId = sameTypeBindingScratch.Count > 0
                ? sameTypeBindingScratch[0].multiInstanceCoordinatorId
                : null;
            bool governed = !string.IsNullOrEmpty(coordinatorId);
            for (int i = 1; governed && i < sameTypeBindingScratch.Count; i++)
            {
                governed = string.Equals(
                    coordinatorId,
                    sameTypeBindingScratch[i].multiInstanceCoordinatorId,
                    StringComparison.Ordinal);
            }
            int ownerId = sameTypeBindingScratch.Count > 0
                ? sameTypeBindingScratch[0].window.GetInstanceID()
                : 0;
            for (int i = 0; i < sameTypeBindingScratch.Count; i++)
            {
                WindowBinding candidate = sameTypeBindingScratch[i];
                bool violation = !governed && i > 0;
                if (candidate.singleInstanceViolation == violation
                    && candidate.singleInstanceOwnerId == (violation ? ownerId : 0))
                    continue;
                candidate.singleInstanceViolation = violation;
                candidate.singleInstanceOwnerId = violation ? ownerId : 0;
                if (violation)
                {
                    RestoreSemiSleep(candidate, true);
                    candidate.restorePersistedSleepOnBind = false;
                    candidate.restorePersistedSleepScheduled = false;
                }
                RefreshSemiSleepControls(candidate);
            }

            if (!governed && sameTypeBindingScratch.Count > 1
                && singleInstanceWarnings.Add(concreteType))
            {
                Debug.LogError(
                    "[ES Window] 同一 EditorWindow 具体类型出现多个实例："
                    + concreteType.FullName
                    + "。仅首个实例参与 ES 休眠与持久化；额外实例必须关闭，"
                    + "或由确有并行需求的窗口显式实现 IESWindowMultiInstanceContract，"
                    + "并让同类型所有实例返回同一个稳定、非空协调器 ID。");
            }
            else if (sameTypeBindingScratch.Count <= 1)
            {
                singleInstanceWarnings.Remove(concreteType);
            }
        }

        private static void SchedulePersistedSemiSleepGeometryRestore(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.root == null
                || !binding.restorePersistedSleepOnBind
                || binding.restorePersistedSleepScheduled)
                return;

            int bindingId = binding.window.GetInstanceID();
            binding.restorePersistedSleepScheduled = true;
            binding.root.schedule.Execute(() =>
            {
                binding.restorePersistedSleepScheduled = false;
                if (!windowBindings.TryGetValue(bindingId, out WindowBinding current)
                    || !ReferenceEquals(current, binding)
                    || binding.lifecycleSuspended
                    || EditorApplication.isPlayingOrWillChangePlaymode
                    || !binding.restorePersistedSleepOnBind)
                    return;
                TryRestorePersistedSemiSleepGeometry(binding);
                RefreshSemiSleepUpdateSubscription();
            }).StartingIn(1);
        }

        /// <summary>运行时调整当前 ES 窗口是否参与半休眠，不需要重建窗口内容。</summary>
        public static void SetWindowSemiSleepAllowed(EditorWindow window, bool allowed)
        {
            if (window == null
                || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return;
            binding.allowSemiSleep = binding.supportsSemiSleep && allowed;
            binding.focusLostAt = -1d;
            if (!binding.allowSemiSleep)
                RestoreSemiSleep(binding, true);
            SaveSemiSleepPreferences(binding);
            if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                AttachSemiSleepControls(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// 为当前域内的已绑定窗口指定半休眠落点。落点会按项目与窗口类型持久化，
        /// 只保存矩形和模式，不保存窗口引用、页面实例或业务资产。
        /// </summary>
        public static bool SetWindowSemiSleepDockBounds(EditorWindow window, Rect bounds)
        {
            if (window == null || bounds.width < 1f || bounds.height < 1f)
                return false;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return false;
            if (binding == null)
                return false;
            binding.hasSemiSleepDockBounds = true;
            binding.semiSleepDockBounds = ClampSemiSleepDockBounds(
                bounds,
                GetSemiSleepTrayBounds(binding.window.position));
            binding.semiSleepManualHold = true;
            SaveSemiSleepPreferences(binding);
            RefreshSemiSleepControls(binding);
            return true;
        }

        /// <summary>立即请求一个符合条件的浮动 ES 窗口进入半休眠。</summary>
        public static bool RequestWindowSemiSleep(EditorWindow window)
        {
            if (window == null || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return false;
            if (binding.lifecycleSuspended || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (!CanEnterSemiSleep(binding, false))
                return false;
            binding.focusLostAt = EditorApplication.timeSinceStartup - SemiSleepDelay;
            binding.semiSleepManualHold = true;
            BeginSemiSleepTransition(binding, true);
            SaveSemiSleepPreferences(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        /// <summary>查询窗口此刻是否满足立即休眠的硬条件；不受全局自动开关和固定策略影响。</summary>
        public static bool CanWindowEnterSemiSleep(EditorWindow window)
        {
            if (window == null
                || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return false;
            return EvaluateSemiSleepBlockReason(binding, false) == SemiSleepBlockReason.None;
        }

        /// <summary>
        /// 返回当前休眠阻塞原因。该入口只读取已绑定状态，不扫描窗口或资产。
        /// </summary>
        public static string GetWindowSemiSleepBlockReason(
            EditorWindow window,
            bool requireAutomaticPolicy = false)
        {
            if (window == null)
                return "窗口为空。";
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || binding == null)
                return "窗口尚未接入 ES Presentation。";
            return GetSemiSleepBlockReasonText(
                EvaluateSemiSleepBlockReason(binding, requireAutomaticPolicy));
        }

        /// <summary>显式唤醒当前休眠窗口；不会改动参与资格、固定状态或全局自动策略。</summary>
        public static bool RequestWindowWake(EditorWindow window)
        {
            if (window == null
                || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !IsSleepingOrTargetingSleep(binding))
                return false;
            if (binding.restorePersistedSleepOnBind)
            {
                CancelPersistedSemiSleepRestore(binding);
                window.Focus();
                RefreshSemiSleepControls(binding);
                RefreshSemiSleepUpdateSubscription();
                return true;
            }
            binding.semiSleepManualHold = false;
            window.Focus();
            BeginSemiSleepTransition(binding, false);
            SaveSemiSleepPreferences(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        public static bool IsWindowSemiSleeping(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget);
        }

        public static ES.ESWindowVisualState GetWindowVisualState(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                ? binding.visualState
                : ES.ESWindowVisualState.ActivePanel;
        }

        /// <summary>固定窗口，固定期间不会自动进入半休眠。</summary>
        public static void SetWindowPinned(EditorWindow window, bool pinned)
        {
            if (window == null || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return;
            binding.pinned = pinned;
            if (pinned && !binding.semiSleepManualHold)
                RestoreSemiSleep(binding, true);
            SaveSemiSleepPreferences(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        public static bool IsWindowPinned(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding.pinned;
        }

        /// <summary>
        /// 设置当前域内的明确父子休眠关系。不会扫描全部 EditorWindow，也不会持久化对象引用。
        /// </summary>
        public static bool SetWindowSleepOwner(
            EditorWindow child,
            EditorWindow owner,
            ES.ESWindowSleepLinkMode mode = ES.ESWindowSleepLinkMode.FollowOwner)
        {
            if (child == null)
                return false;
            switch (mode)
            {
                case ES.ESWindowSleepLinkMode.Independent:
                    if (owner != null)
                        return false;
                    ClearPendingSleepOwner(child);
                    ClearWindowSleepOwner(child);
                    return true;
                case ES.ESWindowSleepLinkMode.FollowOwner:
                case ES.ESWindowSleepLinkMode.OwnedSurface:
                    if (owner == null || child == owner)
                        return false;
                    ES.ESWindowFoundation.ValidateFullLifecycleSurfaceCapability(
                        child,
                        mode + " internal 子窗口关系");
                    ES.ESWindowFoundation.ValidateFullLifecycleSurfaceCapability(
                        owner,
                        mode + " internal owner 关系");
                    break;
                default:
                    return false;
            }
            if (!windowBindings.TryGetValue(child.GetInstanceID(), out WindowBinding childBinding)
                || childBinding == null)
            {
                // OwnedSurface is a relationship-level opt-out. Establish the
                // binding expected by the type contract first, then the
                // relationship below removes the independent sleep surface.
                ESWindowSleepMode? declared = ESWindowFoundation.GetDeclaredSleepMode(child);
                BindWindow(child, declared != ESWindowSleepMode.Transient);
            }
            if (!windowBindings.TryGetValue(child.GetInstanceID(), out childBinding)
                || childBinding == null)
                return false;

            if (!windowBindings.TryGetValue(owner.GetInstanceID(), out WindowBinding ownerBinding)
                || ownerBinding == null)
            {
                BindWindow(owner);
                windowBindings.TryGetValue(owner.GetInstanceID(), out ownerBinding);
            }
            if (ownerBinding == null || !ReferenceEquals(ownerBinding.window, owner))
                return false;

            // 拒绝任意长度的 owner 环，避免同步递归和互相唤醒。
            WindowBinding cursor = ownerBinding;
            int guard = windowBindings.Count + 1;
            while (cursor != null && guard-- > 0)
            {
                if (cursor.window == child)
                    return false;
                cursor = cursor.sleepOwner != null
                    && windowBindings.TryGetValue(
                        cursor.sleepOwner.GetInstanceID(),
                        out WindowBinding next)
                    ? next
                    : null;
            }

            // 显式绑定拥有更高优先级：只有所有输入和 owner 环校验通过后，
            // 才移除此前因宿主尚未恢复而登记的 PendingFollowOwner；被拒绝的
            // 非法绑定不能破坏原有可恢复意图。
            ClearPendingSleepOwner(child);
            ES.ESWindowSleepLinkMode previousMode = childBinding.sleepLinkMode;
            childBinding.sleepOwner = owner;
            if (mode == ES.ESWindowSleepLinkMode.OwnedSurface)
            {
                // OnEnable/CreateGUI can register the same relationship more than
                // once while Unity rebuilds a panel. Preserve the type capability
                // and user preference from before the first OwnedSurface bind.
                if (previousMode != ES.ESWindowSleepLinkMode.OwnedSurface)
                {
                    childBinding.ownedSurfacePreviousSupports = childBinding.supportsSemiSleep;
                    childBinding.ownedSurfacePreviousAllow = childBinding.allowSemiSleep;
                }
                childBinding.sleepLinkMode = mode;
                childBinding.supportsSemiSleep = false;
                childBinding.allowSemiSleep = false;
                childBinding.sleepOwnerForcedSleep = false;
                RestoreSemiSleep(childBinding, true);
                RemoveSemiSleepControls(childBinding);
            }
            else
            {
                // Leaving OwnedSurface restores exactly what it temporarily hid.
                // A normal FollowOwner bind must not promote Transient windows or
                // overwrite a Full window's user-controlled participation flag.
                childBinding.sleepLinkMode = mode;
                if (previousMode == ES.ESWindowSleepLinkMode.OwnedSurface)
                    RestoreOwnedSurfaceSleepCapability(childBinding);
                if (childBinding.supportsSemiSleep && childBinding.semiSleepControls == null)
                    AttachSemiSleepControls(childBinding);
                SyncSleepOwnerState(childBinding);
            }
            RefreshSemiSleepControls(childBinding);
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        public static bool RegisterPendingSleepOwner(
            EditorWindow child,
            string ownerKey,
            ES.ESWindowSleepLinkMode mode = ES.ESWindowSleepLinkMode.FollowOwner)
        {
            if (child == null || string.IsNullOrWhiteSpace(ownerKey)
                || mode != ES.ESWindowSleepLinkMode.FollowOwner)
                return false;
            ES.ESWindowFoundation.ValidateFullLifecycleSurfaceCapability(
                child,
                "Pending FollowOwner internal 子窗口关系");

            string normalizedOwnerKey = ownerKey.Trim();
            ClearWindowSleepOwner(child);
            ClearPendingSleepOwner(child);
            pendingSleepOwners.Add(new PendingSleepOwner
            {
                child = child,
                ownerKey = normalizedOwnerKey,
                mode = mode
            });

            // ReloadDomain does not guarantee parent/child OnEnable ordering. If
            // the Full owner already registered this key, resolve immediately;
            // otherwise retain the Pending intent until the owner appears.
            if (TryGetRegisteredSleepOwner(normalizedOwnerKey, out EditorWindow owner))
                SetWindowSleepOwner(child, owner, mode);
            return true;
        }

        public static int ResolvePendingSleepOwners(string ownerKey, EditorWindow owner)
        {
            if (owner == null || string.IsNullOrWhiteSpace(ownerKey))
                return 0;
            ES.ESWindowFoundation.ValidateFullLifecycleSurfaceCapability(
                owner,
                "Pending FollowOwner internal owner 关系");

            string normalizedOwnerKey = ownerKey.Trim();
            EnsureSleepOwnerKeyAvailable(normalizedOwnerKey, owner);
            WindowBinding ownerBinding = GetOrCreateSleepOwnerBinding(owner);
            if (ownerBinding == null)
                return 0;
            RegisterSleepOwnerKey(normalizedOwnerKey, ownerBinding);

            int resolved = 0;
            for (int i = pendingSleepOwners.Count - 1; i >= 0; i--)
            {
                PendingSleepOwner pending = pendingSleepOwners[i];
                if (pending == null || pending.child == null)
                {
                    pendingSleepOwners.RemoveAt(i);
                    continue;
                }
                if (!string.Equals(pending.ownerKey, normalizedOwnerKey, StringComparison.Ordinal))
                    continue;

                // SetWindowSleepOwner removes the Pending record only after all
                // validation succeeds. A rejected owner cycle must leave the
                // recovery intent available for a later valid owner.
                if (SetWindowSleepOwner(pending.child, owner, pending.mode))
                {
                    resolved++;
                    i = Math.Min(i, pendingSleepOwners.Count);
                }
            }
            return resolved;
        }

        private static WindowBinding GetOrCreateSleepOwnerBinding(EditorWindow owner)
        {
            if (owner == null)
                return null;

            int ownerId = owner.GetInstanceID();
            if (!windowBindings.TryGetValue(ownerId, out WindowBinding ownerBinding)
                || ownerBinding == null
                || !ReferenceEquals(ownerBinding.window, owner))
            {
                BindWindow(owner);
                windowBindings.TryGetValue(ownerId, out ownerBinding);
            }
            return ownerBinding != null && ReferenceEquals(ownerBinding.window, owner)
                ? ownerBinding
                : null;
        }

        private static void EnsureSleepOwnerKeyAvailable(string ownerKey, EditorWindow owner)
        {
            if (!sleepOwnerBindingsByKey.TryGetValue(ownerKey, out WindowBinding existing))
                return;
            if (existing?.window == null)
            {
                sleepOwnerBindingsByKey.Remove(ownerKey);
                existing?.registeredSleepOwnerKeys?.Remove(ownerKey);
                return;
            }
            if (ReferenceEquals(existing.window, owner))
                return;

            throw new InvalidOperationException(
                "ES 窗口 ownerKey 必须唯一；当前 key 已属于另一个活动窗口："
                + ownerKey);
        }

        private static void RegisterSleepOwnerKey(string ownerKey, WindowBinding ownerBinding)
        {
            if (ownerBinding?.window == null || string.IsNullOrWhiteSpace(ownerKey))
                return;

            string normalizedOwnerKey = ownerKey.Trim();
            if (sleepOwnerBindingsByKey.TryGetValue(normalizedOwnerKey, out WindowBinding existing)
                && existing != null
                && existing.window != null
                && !ReferenceEquals(existing.window, ownerBinding.window))
            {
                throw new InvalidOperationException(
                    "ES 窗口 ownerKey 必须唯一；当前 key 已属于另一个活动窗口："
                    + normalizedOwnerKey);
            }

            sleepOwnerBindingsByKey[normalizedOwnerKey] = ownerBinding;
            ownerBinding.registeredSleepOwnerKeys ??= new HashSet<string>(StringComparer.Ordinal);
            ownerBinding.registeredSleepOwnerKeys.Add(normalizedOwnerKey);
        }

        private static bool TryGetRegisteredSleepOwner(string ownerKey, out EditorWindow owner)
        {
            owner = null;
            if (string.IsNullOrWhiteSpace(ownerKey))
                return false;

            string normalizedOwnerKey = ownerKey.Trim();
            if (!sleepOwnerBindingsByKey.TryGetValue(normalizedOwnerKey, out WindowBinding binding))
                return false;
            if (binding?.window != null
                && windowBindings.TryGetValue(binding.window.GetInstanceID(), out WindowBinding current)
                && ReferenceEquals(current, binding))
            {
                owner = binding.window;
                return true;
            }

            sleepOwnerBindingsByKey.Remove(normalizedOwnerKey);
            binding?.registeredSleepOwnerKeys?.Remove(normalizedOwnerKey);
            return false;
        }

        private static void UnregisterSleepOwnerKeys(WindowBinding ownerBinding, bool clearPending)
        {
            if (ownerBinding?.registeredSleepOwnerKeys == null)
                return;

            DeactivateSleepOwnerKeys(ownerBinding);
            foreach (string ownerKey in ownerBinding.registeredSleepOwnerKeys)
            {
                if (clearPending
                    && (!sleepOwnerBindingsByKey.TryGetValue(ownerKey, out WindowBinding registered)
                        || ReferenceEquals(registered, ownerBinding)))
                    ClearPendingSleepOwners(ownerKey);
            }
            ownerBinding.registeredSleepOwnerKeys.Clear();
        }

        private static void DeactivateSleepOwnerKeys(WindowBinding ownerBinding)
        {
            if (ownerBinding?.registeredSleepOwnerKeys == null)
                return;

            foreach (string ownerKey in ownerBinding.registeredSleepOwnerKeys)
            {
                if (sleepOwnerBindingsByKey.TryGetValue(ownerKey, out WindowBinding registered)
                    && ReferenceEquals(registered, ownerBinding))
                    sleepOwnerBindingsByKey.Remove(ownerKey);
            }
        }

        public static void ClearPendingSleepOwner(EditorWindow child)
        {
            if (ReferenceEquals(child, null))
                return;
            for (int i = pendingSleepOwners.Count - 1; i >= 0; i--)
                if (pendingSleepOwners[i] == null
                    || pendingSleepOwners[i].child == null
                    || ReferenceEquals(pendingSleepOwners[i].child, child))
                    pendingSleepOwners.RemoveAt(i);
        }

        public static void ClearPendingSleepOwners(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
                return;
            string normalized = ownerKey.Trim();
            for (int i = pendingSleepOwners.Count - 1; i >= 0; i--)
            {
                PendingSleepOwner pending = pendingSleepOwners[i];
                if (pending == null || string.Equals(pending.ownerKey, normalized, StringComparison.Ordinal))
                    pendingSleepOwners.RemoveAt(i);
            }
        }

        public static void ClearWindowSleepOwner(EditorWindow child)
        {
            if (child == null
                || !windowBindings.TryGetValue(child.GetInstanceID(), out WindowBinding binding)
                || binding == null)
                return;
            DetachWindowSleepOwnerCore(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        private static void DetachWindowSleepOwnerCore(WindowBinding binding)
        {
            if (binding == null)
                return;

            bool ownerForcedSleep = binding.sleepOwnerForcedSleep;
            bool wasOwnedSurface = binding.sleepLinkMode == ES.ESWindowSleepLinkMode.OwnedSurface;
            binding.sleepOwner = null;
            binding.sleepOwnerForcedSleep = false;
            binding.sleepLinkSyncing = false;
            binding.sleepLinkMode = ES.ESWindowSleepLinkMode.Independent;
            if (wasOwnedSurface)
                RestoreOwnedSurfaceSleepCapability(binding);
            if (ownerForcedSleep)
                RestoreSemiSleep(binding, true);
            RefreshSemiSleepControls(binding);
        }

        private static void RestoreOwnedSurfaceSleepCapability(WindowBinding binding)
        {
            binding.supportsSemiSleep = binding.ownedSurfacePreviousSupports;
            binding.allowSemiSleep = binding.ownedSurfacePreviousAllow;
            binding.ownedSurfacePreviousSupports = false;
            binding.ownedSurfacePreviousAllow = false;
            if (binding.supportsSemiSleep)
                AttachSemiSleepControls(binding);
        }

        public static ES.ESWindowSleepLinkMode GetWindowSleepLinkMode(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                ? binding.sleepLinkMode
                : ES.ESWindowSleepLinkMode.Independent;
        }

        public static bool IsWindowSemiSleepAllowed(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding.supportsSemiSleep
                && binding.allowSemiSleep;
        }

        public static bool IsWindowSemiSleepSupported(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                && binding.supportsSemiSleep
                && binding.sleepLinkMode != ES.ESWindowSleepLinkMode.OwnedSurface;
        }

        public static bool IsWindowSingleInstanceViolation(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                && binding.singleInstanceViolation;
        }

        public static bool IsWindowBound(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                && binding.window == window;
        }

        /// <summary>
        /// 进入忙碌状态。支持嵌套 Lease，Dispose 顺序不影响最终状态。
        /// 目标窗口必须已经通过 ESWindowFoundation.Bind 显式接入；未知窗口返回空 Lease，
        /// 不会因为一次业务调用而隐式创建 ES Presentation、系统动作宿主或半休眠能力。
        /// </summary>
        public static IDisposable BeginWindowBusy(EditorWindow window, string message = null, string pageId = null)
        {
            if (window == null)
                return EmptyWindowLease.Instance;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding) || binding == null)
                return EmptyWindowLease.Instance;
            binding.busyCount++;
            binding.activityState = ESWindowActivityState.Busy;
            binding.activityMessage = message;
            binding.activityPageId = pageId;
            if (!string.IsNullOrWhiteSpace(message))
                window.ShowNotification(new GUIContent(message.Trim()), 1.5f);
            RestoreSemiSleep(binding, true);
            PulseWindow(window, ESStatusKind.Info);
            RefreshSemiSleepUpdateSubscription();
            return new WindowBusyLease(window);
        }

        /// <summary>
        /// 暂停窗口自动收纳。菜单、Popup、拖动和子交互应在其真实生命周期内持有此 Lease；
        /// Dispose 后只恢复计时，不改变 Unity 焦点或强制唤醒窗口。
        /// </summary>
        public static IDisposable BeginWindowInteractionHold(EditorWindow window, string reason = null)
        {
            if (window == null)
                return EmptyWindowLease.Instance;
            // InteractionHold 只能暂停一个已经显式接入 Presentation 的 ES 窗口。
            // 禁止因为 Dialog/Popup 的 owner 恰好是原生 Inspector 或第三方窗口，
            // 就在这里隐式 Bind 并向其注入 ES 系统按钮与半休眠生命周期。
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || binding == null)
                return EmptyWindowLease.Instance;
            binding.interactionHoldCount++;
            RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
            RefreshSemiSleepUpdateSubscription();
            return new WindowInteractionLease(window);
        }

        /// <summary>
        /// 向窗口和可选页面上下文发送一次结果提示，并唤醒目标窗口。
        /// 目标窗口必须已经显式接入 ES Presentation；未知窗口不会被隐式绑定。
        /// </summary>
        public static void NotifyWindow(
            EditorWindow window,
            string message,
            ESStatusKind status = ESStatusKind.Info,
            string pageId = null,
            string context = null,
            bool focus = true)
        {
            if (window == null)
                return;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding) || binding == null)
                return;
            binding.activityState = status == ESStatusKind.Error || status == ESStatusKind.Warning
                ? ESWindowActivityState.Attention
                : ESWindowActivityState.Background;
            binding.activityMessage = message;
            binding.activityPageId = pageId;
            binding.activityContext = context;
            if (!string.IsNullOrWhiteSpace(message))
                window.ShowNotification(new GUIContent(message.Trim()), 2.5f);
            RestoreSemiSleep(binding, true);
            if (!string.IsNullOrEmpty(pageId)
                && window is ES.IESWindowPageContextHost pageHost)
                pageHost.ESWindow_TrySelectPage(pageId, true);
            if (focus)
                window.Focus();
            PulseWindow(window, status);
            window.Repaint();
        }

        public static string GetWindowActivityMessage(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                ? binding.activityMessage ?? string.Empty
                : string.Empty;
        }

        public static ESWindowActivityState GetWindowActivityState(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                ? binding.activityState
                : ESWindowActivityState.None;
        }

        /// <summary>保存当前已绑定窗口的轻量工作区快照，仅写入当前 Editor 会话。</summary>
        public static void SaveWorkspaceSnapshot(string workspaceId)
        {
            string normalized = NormalizeWorkspaceId(workspaceId);
            var snapshot = new WorkspaceSnapshot();
            var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int focusOrder = 0;
            var orderedBindings = new List<WindowBinding>(windowBindings.Values);
            orderedBindings.Sort(CompareWindowBindings);
            for (int bindingIndex = 0; bindingIndex < orderedBindings.Count; bindingIndex++)
            {
                WindowBinding binding = orderedBindings[bindingIndex];
                if (binding?.window == null || binding.window.docked)
                    continue;
                string typeName = binding.window.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName))
                    continue;
                typeCounts.TryGetValue(typeName, out int typeIndex);
                typeCounts[typeName] = typeIndex + 1;
                snapshot.windows.Add(new WorkspaceWindowSnapshot
                {
                    typeName = typeName,
                    typeIndex = typeIndex,
                    bounds = binding.semiSleeping || binding.semiSleepAnimating
                        ? binding.awakeBounds
                        : binding.window.position,
                    pinned = binding.pinned,
                    allowSemiSleep = binding.allowSemiSleep,
                    pageId = binding.window is ES.IESWindowPageContextHost pageHost
                        ? pageHost.ESWindow_SelectedPageId
                        : string.Empty,
                    focusOrder = ReferenceEquals(EditorWindow.focusedWindow, binding.window)
                        ? int.MaxValue
                        : focusOrder++
                });
            }
            SessionState.SetString(WorkspaceSessionKeyPrefix + normalized, JsonUtility.ToJson(snapshot));
        }

        /// <summary>恢复当前仍存在的窗口，不会自动创建窗口或恢复 Unity 对象引用。</summary>
        public static int RestoreWorkspaceSnapshot(string workspaceId, bool focusLast = true)
        {
            string normalized = NormalizeWorkspaceId(workspaceId);
            string json = SessionState.GetString(WorkspaceSessionKeyPrefix + normalized, string.Empty);
            if (string.IsNullOrEmpty(json))
                return 0;
            WorkspaceSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<WorkspaceSnapshot>(json);
            }
            catch (ArgumentException)
            {
                return 0;
            }
            if (snapshot?.windows == null)
                return 0;

            var liveByType = new Dictionary<string, List<WindowBinding>>(StringComparer.Ordinal);
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding?.window == null)
                    continue;
                string typeName = binding.window.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName))
                    continue;
                if (!liveByType.TryGetValue(typeName, out List<WindowBinding> bindings))
                {
                    bindings = new List<WindowBinding>();
                    liveByType.Add(typeName, bindings);
                }
                bindings.Add(binding);
            }
            foreach (List<WindowBinding> bindings in liveByType.Values)
                bindings.Sort(CompareWindowBindings);

            int restored = 0;
            EditorWindow focusWindow = null;
            int bestFocusOrder = int.MinValue;
            for (int i = 0; i < snapshot.windows.Count; i++)
            {
                WorkspaceWindowSnapshot saved = snapshot.windows[i];
                if (saved == null
                    || string.IsNullOrEmpty(saved.typeName)
                    || !liveByType.TryGetValue(saved.typeName, out List<WindowBinding> matches)
                    || saved.typeIndex < 0
                    || saved.typeIndex >= matches.Count)
                    continue;
                WindowBinding binding = matches[saved.typeIndex];
                RestoreSemiSleep(binding, true);
                binding.pinned = saved.pinned;
                binding.allowSemiSleep = saved.allowSemiSleep;
                if (!binding.window.docked && saved.bounds.width > 1f && saved.bounds.height > 1f)
                    binding.window.position = saved.bounds;
                if (!string.IsNullOrEmpty(saved.pageId)
                    && binding.window is ES.IESWindowPageContextHost pageHost)
                    pageHost.ESWindow_TrySelectPage(saved.pageId, true);
                binding.window.Repaint();
                restored++;
                if (saved.focusOrder > bestFocusOrder)
                {
                    bestFocusOrder = saved.focusOrder;
                    focusWindow = binding.window;
                }
            }
            if (focusLast)
                focusWindow?.Focus();
            RefreshSemiSleepUpdateSubscription();
            return restored;
        }

        public static bool HasWorkspaceSnapshot(string workspaceId)
        {
            return SessionState.GetString(
                WorkspaceSessionKeyPrefix + NormalizeWorkspaceId(workspaceId),
                string.Empty).Length > 0;
        }

        public static bool SetFocusMode(EditorWindow window, bool enabled)
        {
            if (!enabled)
            {
                ExitFocusMode();
                RefreshSemiSleepUpdateSubscription();
                return true;
            }
            if (window == null || !windowBindings.ContainsKey(window.GetInstanceID()))
                return false;
            ExitFocusMode();
            focusModeWindowId = window.GetInstanceID();
            RestoreSemiSleep(windowBindings[focusModeWindowId], true);
            window.Focus();
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        public static bool IsFocusMode(EditorWindow window)
        {
            return window != null && focusModeWindowId == window.GetInstanceID();
        }

        private static string NormalizeWorkspaceId(string workspaceId)
        {
            return string.IsNullOrWhiteSpace(workspaceId) ? "default" : workspaceId.Trim();
        }

        private static string GetSemiSleepPreferenceKey(EditorWindow window)
        {
            string typeName = window?.GetType().AssemblyQualifiedName ?? "UnknownWindow";
            string project = Application.dataPath ?? string.Empty;
            return SemiSleepWindowPreferencePrefix + Hash128.Compute(
                project + "|" + typeName);
        }

        private static void LoadSemiSleepPreferences(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            if (TryReadSemiSleepPreferences(binding.window, out SemiSleepWindowPreferences saved))
                TryApplySemiSleepPreferences(binding, saved);
        }

        private static bool TryReadSemiSleepPreferences(
            EditorWindow window,
            out SemiSleepWindowPreferences saved)
        {
            saved = null;
            if (window == null)
                return false;
            string json = EditorPrefs.GetString(GetSemiSleepPreferenceKey(window), string.Empty);
            if (string.IsNullOrEmpty(json))
                return false;
            try
            {
                saved = JsonUtility.FromJson<SemiSleepWindowPreferences>(json);
                return saved != null
                    && saved.schemaVersion >= 0
                    && saved.schemaVersion <= SemiSleepPreferenceSchemaVersion;
            }
            catch (ArgumentException)
            {
                saved = null;
                return false;
            }
        }

        private static bool TryApplySemiSleepPreferences(
            WindowBinding binding,
            SemiSleepWindowPreferences saved)
        {
            if (binding?.window == null
                || saved == null
                || saved.schemaVersion < 0
                || saved.schemaVersion > SemiSleepPreferenceSchemaVersion)
                return false;

            binding.allowSemiSleep = binding.supportsSemiSleep && saved.allowSemiSleep;
            binding.pinned = saved.pinned;
            binding.presentationShortTitle = NormalizePresentationShortTitle(
                saved.presentationShortTitle);
            binding.edge = saved.edge >= 0 && saved.edge <= (int)ESWindowEdge.Bottom
                ? (ESWindowEdge)saved.edge
                : ESWindowEdge.Left;
            binding.edgeOffset = IsFinite(saved.edgeOffset) ? saved.edgeOffset : 0f;

            Rect currentBounds = binding.window.position;
            bool hasSavedAwakeBounds = IsUsableWindowBounds(saved.awakeBounds);
            if (hasSavedAwakeBounds)
                binding.awakeBounds = saved.awakeBounds;
            else if (IsUsableWindowBounds(currentBounds))
                binding.awakeBounds = currentBounds;
            bool restoreSavedSleep = saved.sleeping && hasSavedAwakeBounds;
            binding.semiSleepTarget = restoreSavedSleep;
            binding.restorePersistedSleepOnBind = restoreSavedSleep;
            binding.transitionTargetState = restoreSavedSleep
                && (saved.visualState == (int)ESWindowVisualState.EdgeTab
                    || saved.visualState == (int)ESWindowVisualState.EdgeTabHover)
                        ? ESWindowVisualState.EdgeTab
                        : ESWindowVisualState.ActivePanel;

            binding.hasSemiSleepDockBounds = saved.hasDockBounds
                && IsUsableWindowBounds(saved.dockBounds);
            binding.semiSleepDockBounds = binding.hasSemiSleepDockBounds
                ? saved.dockBounds
                : default;
            return true;
        }

        private static void SaveSemiSleepPreferences(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.singleInstanceViolation)
                return;
            if (binding.lifecycleSuspended)
            {
                SaveSuspendedStablePreferences(binding);
                return;
            }
            SemiSleepWindowPreferences saved = CreateSemiSleepPreferences(binding);
            EditorPrefs.SetString(GetSemiSleepPreferenceKey(binding.window), JsonUtility.ToJson(saved));
        }

        private static SemiSleepWindowPreferences CreateSemiSleepPreferences(WindowBinding binding)
        {
            bool sleeping = IsSleepingOrTargetingSleep(binding);
            Rect awakeBounds = IsUsableWindowBounds(binding.awakeBounds)
                ? binding.awakeBounds
                : !sleeping && IsUsableWindowBounds(binding.window.position)
                    ? binding.window.position
                    : default;
            sleeping = sleeping && IsUsableWindowBounds(awakeBounds);
            bool hasDockBounds = binding.hasSemiSleepDockBounds
                && IsUsableWindowBounds(binding.semiSleepDockBounds);
            return new SemiSleepWindowPreferences
            {
                schemaVersion = SemiSleepPreferenceSchemaVersion,
                presentationShortTitle = binding.presentationShortTitle ?? string.Empty,
                allowSemiSleep = binding.allowSemiSleep,
                pinned = binding.pinned,
                sleeping = sleeping,
                visualState = (int)(sleeping
                    ? (binding.transitionTargetState == ESWindowVisualState.ActivePanel
                        ? binding.visualState
                        : binding.transitionTargetState)
                    : ESWindowVisualState.ActivePanel),
                edge = (int)binding.edge >= 0 && (int)binding.edge <= (int)ESWindowEdge.Bottom
                    ? (int)binding.edge
                    : (int)ESWindowEdge.Left,
                edgeOffset = IsFinite(binding.edgeOffset) ? binding.edgeOffset : 0f,
                awakeBounds = awakeBounds,
                dockBounds = hasDockBounds ? binding.semiSleepDockBounds : default,
                hasDockBounds = hasDockBounds
            };
        }

        private static void SaveSuspendedStablePreferences(WindowBinding binding)
        {
            if (!TryReadSemiSleepPreferences(binding.window, out SemiSleepWindowPreferences saved))
                saved = CreateSemiSleepPreferences(binding);

            // Suspension owns the first sleep/geometry snapshot. Stable user
            // settings may still change while the panel is detached, so merge
            // only those fields without replacing the recovery snapshot.
            saved.schemaVersion = SemiSleepPreferenceSchemaVersion;
            saved.presentationShortTitle = binding.presentationShortTitle ?? string.Empty;
            saved.allowSemiSleep = binding.allowSemiSleep;
            saved.pinned = binding.pinned;
            saved.hasDockBounds = binding.hasSemiSleepDockBounds
                && IsUsableWindowBounds(binding.semiSleepDockBounds);
            saved.dockBounds = saved.hasDockBounds ? binding.semiSleepDockBounds : default;
            EditorPrefs.SetString(GetSemiSleepPreferenceKey(binding.window), JsonUtility.ToJson(saved));
        }

        private static int CompareWindowBindings(WindowBinding left, WindowBinding right)
        {
            int leftId = left?.window == null ? int.MaxValue : left.window.GetInstanceID();
            int rightId = right?.window == null ? int.MaxValue : right.window.GetInstanceID();
            return leftId.CompareTo(rightId);
        }

        private static void ExitFocusMode()
        {
            focusModeWindowId = 0;
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || !binding.focusModeForcedSleep)
                    continue;
                binding.focusModeForcedSleep = false;
                RestoreSemiSleep(binding, true);
            }
        }

        /// <summary>
        /// 解除窗口绑定并停止所有局部调度。运行中解绑会恢复开场动画目标尺寸；
        /// 关闭生命周期则保留当前原生窗口几何，避免关闭瞬间反向拉伸。
        /// </summary>
        public static void UnbindWindow(EditorWindow window, bool windowClosing = false)
        {
            if (ReferenceEquals(window, null))
                return;

            int id = window.GetInstanceID();
            bool hasBinding = windowBindings.TryGetValue(id, out WindowBinding binding);
            if (!hasBinding || binding == null)
            {
                try
                {
                    if (hasBinding)
                    {
                        windowBindings.Remove(id);
                        RemoveNullWindowBindingRoots();
                    }
                    RunWindowTeardownStep(
                        () => ESWindowFrameActivation.Stop(id, !windowClosing));
                    if (windowClosing)
                    {
                        RunWindowTeardownStep(() => ClearPendingSleepOwner(window));
                        RunWindowTeardownStep(
                            () => DetachOwnedSleepRelationships(window, null));
                    }
                }
                finally
                {
                    resumeBindingsRetryExhaustedWindowIds.Remove(id);
                    RefreshSemiSleepUpdateSubscription();
                }
                return;
            }

            UnbindWindowBinding(id, binding, windowClosing);
            RefreshSemiSleepUpdateSubscription();
        }

        public static void SuspendWindow(EditorWindow window)
        {
            if (ReferenceEquals(window, null))
                return;

            int id = window.GetInstanceID();
            if (!windowBindings.TryGetValue(id, out WindowBinding binding)
                || binding == null)
                return;

            // OnDisable is not a user wake or a close confirmation. Unity uses
            // it for panel replacement, PlayMode transitions, and other native
            // lifecycle churn, so every OnDisable path preserves sleep geometry;
            // OnDestroy/Close is the explicit teardown authority.
            SuspendWindowBinding(binding, true);
            RefreshSemiSleepUpdateSubscription();
        }

        private static bool ShouldPreserveLifecycleSleepGeometry()
        {
            return !editorQuitting
                && (playModeBindingsSuspended
                    || domainReloadInProgress
                    || EditorApplication.isCompiling
                    || EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private static void UnbindWindowBinding(
            int id,
            WindowBinding binding,
            bool windowClosing,
            bool preserveLifecycle = true)
        {
            if (binding == null)
            {
                windowBindings.Remove(id);
                resumeBindingsRetryExhaustedWindowIds.Remove(id);
                RemoveNullWindowBindingRoots();
                return;
            }

            EditorWindow window = binding.window;
            bool lifecycleReset = EditorApplication.isPlayingOrWillChangePlaymode
                || domainReloadInProgress
                || EditorApplication.isCompiling
                || editorQuitting;
            // OnDisable ordering is not stable across Unity versions. Capture
            // before removing the binding so a reload cannot lose the last
            // sleeping preference when OnDisable precedes beforeAssemblyReload.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                CapturePlayModePreferences();
            else if (domainReloadInProgress || EditorApplication.isCompiling)
                CaptureAssemblyReloadPreferences();

            // OnDisable can arrive before the global PlayMode callback. Keep
            // the dormant binding in that case; removing it loses the only
            // in-memory route back to the persisted sleep state. Explicit
            // shutdown paths (UnbindAllWindowBindings) opt out via the last
            // parameter, and EnteredEditMode prunes windows actually destroyed
            // while the editor was running.
            if (preserveLifecycle
                && lifecycleReset
                && !editorQuitting)
            {
                SuspendWindowBinding(binding, ShouldPreserveLifecycleSleepGeometry());
                RefreshSemiSleepUpdateSubscription();
                return;
            }

            if (windowClosing)
            {
                CloseWindowBinding(id, binding, window, lifecycleReset);
                return;
            }

            ESWindowFrameActivation.Stop(id, true);
            UnregisterPendingPanelAttach(binding);

            RestoreSemiSleep(binding, true, lifecycleReset);
            binding.animation?.Pause();
            binding.animation = null;
            ESWindowOpeningSweep.Stop(binding.root);
            UnregisterWindowCallbacks(binding);
            RemoveBrandTypography(binding.root);
            binding.host?.RemoveFromHierarchy();
            if (binding.semiSleepOverlay != null)
            {
                binding.semiSleepOverlay.userData = null;
                binding.semiSleepOverlay.RemoveFromHierarchy();
            }
            RemoveSemiSleepControls(binding);
            if (focusModeWindowId == id)
                ExitFocusMode();
            if (lastFocusedWindowId == id)
                lastFocusedWindowId = 0;
            windowBindings.Remove(id);
            resumeBindingsRetryExhaustedWindowIds.Remove(id);
            UnregisterSleepOwnerKeys(binding, false);
            RefreshSingleInstanceSafetyForType(window?.GetType());
            ReleaseWindowBindingReferences(binding);
        }

        private static void CloseWindowBinding(
            int id,
            WindowBinding binding,
            EditorWindow window,
            bool lifecycleReset)
        {
            VisualElement boundRoot = binding?.root;
            // Stop late child registrations before relationship callbacks run.
            RunWindowTeardownStep(() => ESWindowFrameActivation.Stop(id, false));
            RunWindowTeardownStep(() => ClearPendingSleepOwner(window));
            RunWindowTeardownStep(() => DeactivateSleepOwnerKeys(binding));

            try
            {
                RunWindowTeardownStep(() => UnregisterPendingPanelAttach(binding));
                // Close is the one lifecycle action that is explicitly allowed
                // to restore the user's awake rectangle, even if OnDisable
                // already marked the binding as suspended.
                RunWindowTeardownStep(() => RestoreSemiSleep(binding, true, true));
                RunWindowTeardownStep(() => PauseAndClearWindowAnimation(binding));
                RunWindowTeardownStep(() => ESWindowOpeningSweep.Stop(binding.root));
                RunWindowTeardownStep(() => UnregisterWindowCallbacks(binding));
                RunWindowTeardownStep(() => RemoveBrandTypography(binding.root));
                RunWindowTeardownStep(() => DetachOwnedSleepRelationships(window, binding));
                RunWindowTeardownStep(() => binding.host?.RemoveFromHierarchy());
                RunWindowTeardownStep(() => RemoveSemiSleepOverlay(binding));
                RunWindowTeardownStep(() => RemoveSemiSleepControls(binding));
                if (focusModeWindowId == id)
                    RunWindowTeardownStep(ExitFocusMode);
                if (lastFocusedWindowId == id)
                    lastFocusedWindowId = 0;
            }
            finally
            {
                RunWindowTeardownStep(() => UnregisterSleepOwnerKeys(binding, true));
                RunWindowTeardownStep(() => RemoveSleepOwnerBindingReferences(binding));
                if (boundRoot != null)
                    windowBindingsByRoot.Remove(boundRoot);
                windowBindings.Remove(id);
                resumeBindingsRetryExhaustedWindowIds.Remove(id);
                RunWindowTeardownStep(() => UnregisterSleepOwnerKeys(binding, false));
                RunWindowTeardownStep(() => RefreshSingleInstanceSafetyForType(window?.GetType()));
                ReleaseWindowBindingReferences(binding);
            }
        }

        private static void RemoveSleepOwnerBindingReferences(WindowBinding binding)
        {
            if (binding == null || sleepOwnerBindingsByKey.Count == 0)
                return;

            List<string> ownedKeys = null;
            foreach (KeyValuePair<string, WindowBinding> pair in sleepOwnerBindingsByKey)
            {
                if (!ReferenceEquals(pair.Value, binding))
                    continue;
                ownedKeys ??= new List<string>();
                ownedKeys.Add(pair.Key);
            }
            if (ownedKeys == null)
                return;

            for (int i = 0; i < ownedKeys.Count; i++)
            {
                sleepOwnerBindingsByKey.Remove(ownedKeys[i]);
                ClearPendingSleepOwners(ownedKeys[i]);
            }
        }

        private static void PauseAndClearWindowAnimation(WindowBinding binding)
        {
            IVisualElementScheduledItem animation = binding?.animation;
            if (binding != null)
                binding.animation = null;
            animation?.Pause();
        }

        private static void RemoveSemiSleepOverlay(WindowBinding binding)
        {
            if (binding?.semiSleepOverlay == null)
                return;
            binding.semiSleepOverlay.userData = null;
            binding.semiSleepOverlay.RemoveFromHierarchy();
        }

        private static void RunWindowTeardownStep(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void DetachOwnedSleepRelationships(
            EditorWindow owner,
            WindowBinding ownerBinding)
        {
            if (ReferenceEquals(owner, null))
                return;

            List<WindowBinding> ownedChildren = null;
            foreach (WindowBinding child in windowBindings.Values)
            {
                if (child == null
                    || child == ownerBinding
                    || !ReferenceEquals(child.sleepOwner, owner))
                    continue;
                ownedChildren ??= new List<WindowBinding>();
                ownedChildren.Add(child);
            }

            if (ownedChildren == null)
                return;

            for (int i = 0; i < ownedChildren.Count; i++)
            {
                WindowBinding child = ownedChildren[i];
                if (child == null || !ReferenceEquals(child.sleepOwner, owner))
                    continue;

                IESWindowSleepRelationshipState relationshipState =
                    !domainReloadInProgress && !editorQuitting
                        ? child.window as IESWindowSleepRelationshipState
                        : null;
                try
                {
                    DetachWindowSleepOwnerCore(child);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                if (relationshipState == null)
                    continue;
                try
                {
                    relationshipState.DetachSleepOwnerAfterOwnerClose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void RemoveNullWindowBindingRoots()
        {
            List<VisualElement> staleRoots = null;
            foreach (KeyValuePair<VisualElement, WindowBinding> pair in windowBindingsByRoot)
            {
                if (pair.Value != null)
                    continue;
                if (staleRoots == null)
                    staleRoots = new List<VisualElement>();
                staleRoots.Add(pair.Key);
            }

            if (staleRoots == null)
                return;
            for (int i = 0; i < staleRoots.Count; i++)
                windowBindingsByRoot.Remove(staleRoots[i]);
        }

        private static void ReleaseWindowBindingReferences(WindowBinding binding)
        {
            if (binding == null)
                return;

            IVisualElementScheduledItem animation = binding.animation;
            VisualElement pendingPanelRoot = binding.pendingPanelRoot;
            VisualElement semiSleepOverlay = binding.semiSleepOverlay;
            HashSet<string> registeredSleepOwnerKeys = binding.registeredSleepOwnerKeys;
            binding.animation = null;
            binding.pendingPanelRoot = null;
            binding.window = null;
            binding.root = null;
            binding.host = null;
            binding.accentLine = null;
            binding.sweep = null;
            binding.semiSleepOverlay = null;
            binding.semiSleepMonogram = null;
            binding.semiSleepIcon = null;
            binding.semiSleepTitleLabel = null;
            binding.semiSleepPromotionProgress = null;
            binding.semiSleepDockProgress = null;
            binding.semiSleepControls = null;
            binding.semiSleepToggleButton = null;
            binding.semiSleepOverflowMenu = null;
            binding.diagnosticBarsHidden = true;
            binding.diagnosticPromotionProgress = -1f;
            binding.diagnosticPromotionComplete = false;
            binding.actionHosts = null;
            binding.multiInstanceCoordinatorId = null;
            binding.sleepOwner = null;
            binding.registeredSleepOwnerKeys = null;
            binding.activityMessage = null;
            binding.activityPageId = null;
            binding.activityContext = null;

            RunWindowTeardownStep(() => animation?.Pause());
            RunWindowTeardownStep(() => pendingPanelRoot?.UnregisterCallback<AttachToPanelEvent>(
                OnWindowRootAttached));
            RunWindowTeardownStep(() =>
            {
                if (semiSleepOverlay != null)
                    semiSleepOverlay.userData = null;
            });
            RunWindowTeardownStep(() => registeredSleepOwnerKeys?.Clear());
        }

        private static void EnsureWindowLifecycleHooks()
        {
            if (windowLifecycleHooksInstalled)
            {
                EnsurePlayModeLifecycleHook();
                return;
            }
            windowLifecycleHooksInstalled = true;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
            EnsurePlayModeLifecycleHook();
        }

        private static void EnsurePlayModeLifecycleHook()
        {
            if (globalEditorAdapterLifecycleInstalled)
                return;
            EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnGlobalPlayModeStateChanged;
            globalEditorAdapterLifecycleInstalled = true;
        }

        private static void HandleBeforeAssemblyReload()
        {
            domainReloadInProgress = true;
            // This handler is registered before the semi-sleep update callback.
            // Capture before Unity detaches roots so DetachFromPanel cannot
            // turn a sleeping window into an "awake" preference on reload.
            StopTransientWindowVisuals(false);
            CaptureAssemblyReloadPreferences();
            // Do not rely on the optional semi-sleep update subscription or on
            // Unity's root-detach ordering. Restore every bound window now so
            // the native editor frame is stable throughout the reload.
            RestoreAllSemiSleepWindows();
        }

        private static void OnCompilationFinished(object context)
        {
            // A failed compile can leave Unity in the previous AppDomain. In that
            // case beforeAssemblyReload already restored the native frame, but no
            // new BindWindow call will run to restore the persisted sleep state.
            // Clear the transient guard and repair the live bindings on the next
            // editor turn; successful compilation normally replaces this domain.
            domainReloadInProgress = true;
            if (failedCompilationRecoveryScheduled
                || editorQuitting)
                return;

            failedCompilationRecoveryScheduled = true;
            EditorApplication.delayCall -= RecoverSemiSleepAfterFailedCompilation;
            EditorApplication.delayCall += RecoverSemiSleepAfterFailedCompilation;
        }

        private static void RecoverSemiSleepAfterFailedCompilation()
        {
            EditorApplication.delayCall -= RecoverSemiSleepAfterFailedCompilation;
            domainReloadInProgress = false;
            if (editorQuitting)
            {
                failedCompilationRecoveryScheduled = false;
                return;
            }
            if (EditorApplication.isCompiling)
            {
                domainReloadInProgress = true;
                EditorApplication.delayCall += RecoverSemiSleepAfterFailedCompilation;
                return;
            }

            failedCompilationRecoveryScheduled = false;
            domainReloadInProgress = false;
            PruneDeadWindowBindings();
            // A successful compile may finish without replacing this domain. In
            // that case beforeAssemblyReload either did not run (domain reload
            // disabled) or already restored the native frame. Re-apply the
            // persisted sleep snapshot when the editor is still in EditMode;
            // this is deliberately skipped while PlayMode owns the native
            // frame.
            bool playModeSuspended = playModeBindingsSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode;
            if (playModeSuspended)
            {
                assemblyReloadPreferencesCaptured = false;
                return;
            }

            // With Domain Reload disabled (and after a failed compile), Unity may
            // keep the same EditorWindow instance and never call CreateGUI again.
            // Reuse the normal panel-aware resume path so a rebuilt root is
            // reattached, while a detached root is deferred until its panel is
            // available instead of leaving a dormant binding permanently inert.
            bool resumed = ResumeWindowBindings();

            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding?.window == null)
                    continue;

                LoadSemiSleepPreferences(binding);
                if (binding.restorePersistedSleepOnBind
                    && binding.allowSemiSleep
                    && !binding.window.docked
                    && binding.root != null
                    && binding.root.panel != null)
                {
                    TryRestorePersistedSemiSleepGeometry(binding);
                }
                RefreshSemiSleepControls(binding);
            }

            if (resumed)
                assemblyReloadPreferencesCaptured = false;
            RefreshSemiSleepUpdateSubscription();
        }

        private static void HandleEditorQuitting()
        {
            editorQuitting = true;
        }

        /// <summary>
        /// 播放一次统一 ES 操作反馈。只在反馈持续期间请求当前窗口局部刷新。
        /// 目标窗口必须已经显式接入 ES Presentation；未知窗口不会被隐式绑定。
        /// </summary>
        public static void PulseWindow(EditorWindow window, ESStatusKind status = ESStatusKind.Modified)
        {
            if (window == null || !GlobalEditorShellEnabled || !MotionEnabled)
                return;

            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || binding == null
                || binding.lifecycleSuspended)
                return;

            BeginWindowPulse(binding, status);
        }

        private static void BeginWindowPulse(WindowBinding binding, ESStatusKind status)
        {
            if (binding?.window == null)
                return;
            binding.pulseStatus = status;
            binding.pulseStartedAt = EditorApplication.timeSinceStartup;
            binding.pulseDuration = GlobalSweepDuration;
            binding.animation?.Resume();
            binding.window.Repaint();
        }

        private static void AttachWindowOverlay(WindowBinding binding)
        {
            VisualElement root = binding.window.rootVisualElement;
            if (root == null)
                return;
            binding.animation?.Pause();
            binding.animation = null;
            UnregisterWindowCallbacks(binding);
            binding.root = root;
            windowBindingsByRoot[root] = binding;
            ApplyBrandTypography(root);
            ESWindowPresentation.ApplySemanticTheme(root);

            if (binding.host != null)
                binding.host.RemoveFromHierarchy();
            if (binding.semiSleepOverlay != null)
                binding.semiSleepOverlay.RemoveFromHierarchy();

            binding.host = new VisualElement
            {
                name = "ESGlobalPresentationOverlay",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            binding.host.style.position = Position.Absolute;
            binding.host.style.left = 0f;
            binding.host.style.right = 0f;
            binding.host.style.top = 0f;
            binding.host.style.height = GlobalAccentLineHeight;
            binding.host.style.backgroundColor = GetDepthAccent(0);

            binding.accentLine = new VisualElement { name = "ESGlobalPresentationAccent" };
            binding.accentLine.pickingMode = PickingMode.Ignore;
            binding.accentLine.style.flexGrow = 1f;
            binding.accentLine.style.backgroundColor = GetDepthAccent(0);
            binding.host.Add(binding.accentLine);

            binding.sweep = new VisualElement { name = "ESGlobalPresentationSweep" };
            binding.sweep.pickingMode = PickingMode.Ignore;
            binding.sweep.style.position = Position.Absolute;
            binding.sweep.style.top = 0f;
            binding.sweep.style.bottom = 0f;
            binding.sweep.style.width = 0f;
            binding.sweep.style.backgroundColor = GetStatusAccent(0, ESStatusKind.Modified);
            binding.host.Add(binding.sweep);

            binding.semiSleepOverlay = CreateSemiSleepOverlay(binding);
            root.Add(binding.semiSleepOverlay);
            AttachSemiSleepControls(binding);

            root.RegisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerEnterEvent>(OnWindowPointerEnter, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(OnWindowPointerLeave, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnWindowPointerMove, TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnWindowWheel, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            root.RegisterCallback<DetachFromPanelEvent>(OnWindowRootDetached);
            root.Add(binding.host);
            binding.host.BringToFront();
            binding.semiSleepOverlay.BringToFront();
            BringSemiSleepControlsToFront(binding);

            EnsureWindowOverlayScheduledVisuals(binding);
        }

        private static void EnsureWindowOverlayScheduledVisuals(WindowBinding binding)
        {
            if (binding == null
                || binding.lifecycleSuspended
                || !IsWindowOverlayAttached(binding)
                || binding.host == null)
                return;

            if (binding.activationPending)
                binding.host.schedule.Execute(() => BeginWindowActivation(binding));

            if (binding.animation == null && MotionEnabled)
            {
                binding.animation = binding.host.schedule
                    .Execute(() => UpdateWindowOverlay(binding))
                    .Every(33);
                binding.animation.Pause();
            }
        }

        private static void BeginWindowActivation(WindowBinding binding)
        {
            if (!IsWindowOverlayAttached(binding)
                || !binding.activationPending
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            binding.activationPending = false;
            ESWindowOpeningSweep.Play(binding.root);
            if (binding.window != null && !binding.window.docked)
                ESWindowFrameActivation.Play(binding.window, binding.window.position);
        }

        private static void OnWindowFocusIn(FocusInEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null
                && !binding.lifecycleSuspended
                && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (focusModeWindowId != 0
                    && focusModeWindowId != binding.window.GetInstanceID())
                    ExitFocusMode();
                lastFocusedWindowId = binding.window.GetInstanceID();
                binding.focusLostAt = -1d;
                if (binding.activityState == ESWindowActivityState.Attention
                    || binding.activityState == ESWindowActivityState.Background)
                {
                    binding.activityState = ESWindowActivityState.Active;
                    binding.activityMessage = null;
                    binding.activityPageId = null;
                    binding.activityContext = null;
                }
                binding.root?.schedule.Execute(() => RestoreFocusedSemiSleepAfterPointerRouting(binding));
                PulseWindow(binding.window, ESStatusKind.Modified);
            }
        }

        private static void RestoreFocusedSemiSleepAfterPointerRouting(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !ReferenceEquals(EditorWindow.focusedWindow, binding.window)
                || binding.semiSleepManualHold
                || binding.semiSleepDragPointerId >= 0
                || !binding.semiSleeping && !binding.semiSleepAnimating)
                return;
            BeginSemiSleepTransition(binding, false);
        }

        private static void OnWindowPointerDown(PointerDownEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding == null || binding.lifecycleSuspended)
                return;
            RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
            if (evt.button == 0)
                PulseWindow(binding.window, ESStatusKind.Modified);
            else if (evt.button == 1)
                binding.transientInteractionGraceUntil = EditorApplication.timeSinceStartup
                    + TransientInteractionGrace;
        }

        private static void OnWindowPointerEnter(PointerEnterEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding == null
                || binding.lifecycleSuspended
                || evt.target != evt.currentTarget)
                return;
            binding.pointerInside = true;
            binding.edgeTabHoverExitGraceUntil = -1d;
            RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
        }

        private static void OnWindowPointerMove(PointerMoveEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding?.root == null || binding.lifecycleSuspended)
                return;

            // PointerMove is observed at the root so child controls, context menus and
            // non-focused windows still keep the owning window active while the pointer
            // remains inside its actual visual bounds.
            Vector2 rootPointerPosition = new Vector2(evt.position.x, evt.position.y);
            bool inside = binding.root.worldBound.Contains(rootPointerPosition);
            binding.pointerInside = inside;
            if (inside)
                RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
        }

        private static void OnWindowPointerLeave(PointerLeaveEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding == null
                || binding.lifecycleSuspended
                || evt.target != evt.currentTarget)
                return;
            binding.pointerInside = false;
            if (!binding.semiSleepAnimating
                    && binding.visualState == ESWindowVisualState.EdgeTabHover
                || binding.semiSleepAnimating
                    && binding.transitionTargetState == ESWindowVisualState.EdgeTabHover)
                BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTab);
        }

        private static void OnWindowWheel(WheelEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null && !binding.lifecycleSuspended)
                RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
        }

        private static void OnWindowKeyDown(KeyDownEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null && !binding.lifecycleSuspended)
                RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
        }

        private static void OnWindowRootDetached(DetachFromPanelEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding == null)
                return;

            // Detach is also emitted during ordinary UI Toolkit panel rebuilds,
            // not only during ReloadDomain. Keeping the old root in
            // windowBindingsByRoot leaves callbacks and scheduled items attached
            // to a dead panel; the next BindWindow call then has to compete with
            // stale visual state. Reuse the same deterministic teardown used by
            // PlayMode suspension while retaining the logical binding slot and
            // its persisted sleep preference. Preserve hosts only when they still
            // belong to the current root, then wait for that root to reattach. A
            // panel move is not required to invoke CreateGUI again, so relying on
            // the window to call BindWindow would leave the logical binding inert.
            // SuspendWindowBinding owns the capture-once guard shared with explicit
            // OnDisable suspension.
            SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);
            QueueResumeWindowBindingsRetry();
            RefreshSemiSleepUpdateSubscription();
        }

        internal static Rect EvaluateSemiSleepTarget(Rect awakeBounds)
        {
            return EvaluateSemiSleepTarget(awakeBounds, 0);
        }

        internal static Rect EvaluateSemiSleepFrame(
            Rect from,
            Rect to,
            float progress,
            bool restoring,
            float intensity)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float strength = Mathf.Clamp01(intensity);
            float accent = Mathf.Sin(t * Mathf.PI) * strength;
            float width = Mathf.Lerp(from.width, to.width, eased);
            float height = Mathf.Lerp(from.height, to.height, eased);
            float overshoot = restoring ? 0.026f : -0.045f;
            width = Mathf.Max(1f, width + to.width * overshoot * accent);
            height = Mathf.Max(1f, height + to.height * overshoot * accent);
            float anchorX = Mathf.Lerp(from.xMax, to.xMax, eased);
            float anchorY = Mathf.Lerp(from.yMax, to.yMax, eased);
            return new Rect(anchorX - width, anchorY - height, width, height);
        }

        internal static Rect EvaluateEdgeTabTransitionFrame(
            Rect from,
            Rect to,
            float progress,
            ESWindowEdge edge)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float width = Mathf.Lerp(from.width, to.width, eased);
            float height = Mathf.Lerp(from.height, to.height, eased);
            float x = Mathf.Lerp(from.x, to.x, eased);
            float y = Mathf.Lerp(from.y, to.y, eased);
            // 页签召回时先沿当前屏幕边缘完成主要展开，再在后半程平滑回到
            // 大窗口位置。这样鼠标触发后的第一视觉锚点稳定，不会刚展开就
            // 像从边缘被横向甩走；progress=1 仍精确到达原窗口几何。
            float anchorProgress = Mathf.Clamp01((eased - 0.5f) * 2f);
            anchorProgress = anchorProgress * anchorProgress * (3f - 2f * anchorProgress);
            switch (edge)
            {
                case ESWindowEdge.Left:
                    x = Mathf.Lerp(from.xMin, to.xMin, anchorProgress);
                    break;
                case ESWindowEdge.Right:
                    x = Mathf.Lerp(from.xMax, to.xMax, anchorProgress) - width;
                    break;
                case ESWindowEdge.Top:
                    y = Mathf.Lerp(from.yMin, to.yMin, anchorProgress);
                    break;
                case ESWindowEdge.Bottom:
                    y = Mathf.Lerp(from.yMax, to.yMax, anchorProgress) - height;
                    break;
            }
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 根据停靠边缘从当前形态向方块态过渡。方块态始终以页签的屏幕锚点为基准，
        /// 不允许从窗口右上角或其他默认角点飞入。
        /// </summary>
        internal static Rect EvaluateEdgeTabToTileFrame(
            Rect from,
            Rect tile,
            float progress,
            ESWindowEdge edge)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float width = Mathf.Lerp(from.width, tile.width, eased);
            float height = Mathf.Lerp(from.height, tile.height, eased);
            float x = Mathf.Lerp(from.x, tile.x, eased);
            float y = Mathf.Lerp(from.y, tile.y, eased);
            switch (edge)
            {
                case ESWindowEdge.Left:
                    x = Mathf.Lerp(from.xMin, tile.xMin, eased);
                    break;
                case ESWindowEdge.Right:
                    x = Mathf.Lerp(from.xMax, tile.xMax, eased) - width;
                    break;
                case ESWindowEdge.Top:
                    y = Mathf.Lerp(from.yMin, tile.yMin, eased);
                    break;
                case ESWindowEdge.Bottom:
                    y = Mathf.Lerp(from.yMax, tile.yMax, eased) - height;
                    break;
            }
            return new Rect(x, y, width, height);
        }

        internal static float EvaluateEdgeTabTransitionDuration(
            Rect current,
            Rect target,
            float fullDistance,
            float fullDuration)
        {
            float distance = Mathf.Max(
                Mathf.Abs(target.width - current.width),
                Mathf.Abs(target.height - current.height));
            float ratio = fullDistance > 0.001f
                ? Mathf.Clamp01(distance / fullDistance)
                : 1f;
            return Mathf.Lerp(
                EdgeTabMinimumReverseDuration,
                Mathf.Max(EdgeTabMinimumReverseDuration, fullDuration),
                ratio);
        }

        internal static bool ShouldResetEdgeTabHoverCommit(
            Vector2 previousPointerPosition,
            Vector2 currentPointerPosition,
            bool hasPreviousPosition)
        {
            return !hasPreviousPosition
                || (currentPointerPosition - previousPointerPosition).sqrMagnitude
                >= EdgeTabPointerIntentDistance * EdgeTabPointerIntentDistance;
        }

        internal static bool TryEvaluateEdgeTab(
            Rect tileBounds,
            Rect workArea,
            out ESWindowEdge edge,
            out float edgeOffset,
            out Rect tabBounds)
        {
            edge = ESWindowEdge.Right;
            edgeOffset = 0f;
            tabBounds = tileBounds;
            if (!IsFinite(tileBounds.position)
                || !IsFinite(tileBounds.size)
                || !IsFinite(workArea.position)
                || !IsFinite(workArea.size)
                || workArea.width < EdgeTabCollapsedLength
                || workArea.height < EdgeTabThickness)
                return false;

            float left = Mathf.Abs(tileBounds.xMin - workArea.xMin);
            float right = Mathf.Abs(workArea.xMax - tileBounds.xMax);
            float top = Mathf.Abs(tileBounds.yMin - workArea.yMin);
            float bottom = Mathf.Abs(workArea.yMax - tileBounds.yMax);
            float nearest = Mathf.Min(left, right, top, bottom);
            if (nearest > EdgeTabSnapDistance)
                return false;

            if (nearest == left)
                edge = ESWindowEdge.Left;
            else if (nearest == right)
                edge = ESWindowEdge.Right;
            else if (nearest == top)
                edge = ESWindowEdge.Top;
            else
                edge = ESWindowEdge.Bottom;

            // ES tabs extend inward perpendicular to the screen edge. The product
            // interaction contract intentionally uses a vertical tab at the top/
            // bottom edge and a horizontal tab at the left/right edge.
            bool horizontalEdge = edge == ESWindowEdge.Top || edge == ESWindowEdge.Bottom;
            if (horizontalEdge)
            {
                edgeOffset = Mathf.Clamp(
                    tileBounds.center.x - workArea.xMin - EdgeTabThickness * 0.5f,
                    0f,
                    Mathf.Max(0f, workArea.width - EdgeTabThickness));
                float y = edge == ESWindowEdge.Top
                    ? workArea.yMin
                    : workArea.yMax - EdgeTabCollapsedLength;
                tabBounds = new Rect(
                    workArea.xMin + edgeOffset,
                    y,
                    EdgeTabThickness,
                    EdgeTabCollapsedLength);
            }
            else
            {
                edgeOffset = Mathf.Clamp(
                    tileBounds.center.y - workArea.yMin - EdgeTabThickness * 0.5f,
                    0f,
                    Mathf.Max(0f, workArea.height - EdgeTabThickness));
                float x = edge == ESWindowEdge.Left
                    ? workArea.xMin
                    : workArea.xMax - EdgeTabCollapsedLength;
                tabBounds = new Rect(
                    x,
                    workArea.yMin + edgeOffset,
                    EdgeTabCollapsedLength,
                    EdgeTabThickness);
            }
            return true;
        }

        internal static Rect EvaluateEdgeTabBounds(
            Rect workArea,
            ESWindowEdge edge,
            float edgeOffset,
            float expansion)
        {
            float length = Mathf.Lerp(
                EdgeTabCollapsedLength,
                EdgeTabExpandedLength,
                Mathf.Clamp01(expansion));
            bool horizontalEdge = edge == ESWindowEdge.Top || edge == ESWindowEdge.Bottom;
            if (horizontalEdge)
            {
                float x = Mathf.Clamp(
                    workArea.xMin + edgeOffset,
                    workArea.xMin,
                    Mathf.Max(workArea.xMin, workArea.xMax - EdgeTabThickness));
                float y = edge == ESWindowEdge.Top ? workArea.yMin : workArea.yMax - length;
                return new Rect(x, y, EdgeTabThickness, length);
            }

            float yPosition = Mathf.Clamp(
                workArea.yMin + edgeOffset,
                workArea.yMin,
                Mathf.Max(workArea.yMin, workArea.yMax - EdgeTabThickness));
            float xPosition = edge == ESWindowEdge.Left
                ? workArea.xMin
                : workArea.xMax - length;
            return new Rect(xPosition, yPosition, length, EdgeTabThickness);
        }

        /// <summary>
        /// 沿页签所属屏幕边缘移动原生窗口。拖动只改变沿边坐标，边缘锚点、
        /// 当前展开长度和厚度保持不变，避免拖动时页签离开屏幕边缘或突然换向。
        /// </summary>
        internal static Rect EvaluateEdgeTabDragFrame(
            Rect current,
            Vector2 pointerDelta,
            Rect workArea,
            ESWindowEdge edge,
            out float edgeOffset)
        {
            edgeOffset = 0f;
            if (!IsFinite(pointerDelta)
                || !IsFinite(current.position)
                || !IsFinite(current.size)
                || !IsFinite(workArea.position)
                || !IsFinite(workArea.size))
                return current;

            Rect target = current;
            bool verticalTab = edge == ESWindowEdge.Top || edge == ESWindowEdge.Bottom;
            if (verticalTab)
            {
                float minX = workArea.xMin;
                float maxX = Mathf.Max(minX, workArea.xMax - current.width);
                target.x = Mathf.Clamp(current.x + pointerDelta.x, minX, maxX);
                target.y = edge == ESWindowEdge.Top
                    ? workArea.yMin
                    : workArea.yMax - current.height;
                edgeOffset = target.x - workArea.xMin;
            }
            else
            {
                float minY = workArea.yMin;
                float maxY = Mathf.Max(minY, workArea.yMax - current.height);
                target.y = Mathf.Clamp(current.y + pointerDelta.y, minY, maxY);
                target.x = edge == ESWindowEdge.Left
                    ? workArea.xMin
                    : workArea.xMax - current.width;
                edgeOffset = target.y - workArea.yMin;
            }

            return target;
        }

        internal static bool ShouldRestoreEdgeTabToTile(
            double fullyExpandedAt,
            double now,
            bool interactionHeld)
        {
            return interactionHeld
                && fullyExpandedAt >= 0d
                && now >= fullyExpandedAt
                && now - fullyExpandedAt >= EdgeTabHoverCommitDelay;
        }

        internal static bool ShouldBeginEdgeTabHover(
            double intentStartedAt,
            double now,
            bool pointerInside)
        {
            return pointerInside
                && intentStartedAt >= 0d
                && now >= intentStartedAt
                && now - intentStartedAt >= EdgeTabHoverIntentDelay;
        }

        private static VisualElement CreateSemiSleepOverlay(WindowBinding binding)
        {
            binding.diagnosticBarsHidden = true;
            binding.diagnosticPromotionProgress = -1f;
            binding.diagnosticPromotionComplete = false;
            var overlay = new VisualElement
            {
                name = "ESSemiSleepOverlay",
                pickingMode = PickingMode.Position,
                userData = binding
            };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.right = 0f;
            overlay.style.top = 0f;
            overlay.style.bottom = 0f;
            overlay.style.display = DisplayStyle.None;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = WindowRaisedSurfaceColor;
            overlay.style.borderLeftWidth = 1f;
            overlay.style.borderRightWidth = 1f;
            overlay.style.borderTopWidth = 1f;
            overlay.style.borderBottomWidth = 1f;
            overlay.style.borderLeftColor = ActiveColor;
            overlay.style.borderRightColor = ActiveColor;
            overlay.style.borderTopColor = ActiveColor;
            overlay.style.borderBottomColor = ActiveColor;
            // 休眠提示由视觉状态和按钮承担；不要把诊断文本挂到整个覆盖层，
            // 否则悬停时会遮挡窗口内容并把次要信息提升为主反馈。
            overlay.tooltip = null;

            Texture icon = ResolveWindowPresentationIcon(binding.window);
            if (icon != null)
            {
                binding.semiSleepIcon = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                    tintColor = SelectedTextColor
                };
                binding.semiSleepIcon.style.width = 22f;
                binding.semiSleepIcon.style.height = 22f;
                overlay.Add(binding.semiSleepIcon);
            }
            else
            {
                binding.semiSleepMonogram = new Label("ES")
                {
                    pickingMode = PickingMode.Ignore
                };
                binding.semiSleepMonogram.style.fontSize = 20f;
                binding.semiSleepMonogram.style.unityFontStyleAndWeight = FontStyle.Bold;
                binding.semiSleepMonogram.style.color = SelectedTextColor;
                overlay.Add(binding.semiSleepMonogram);
            }

            string title = binding.window is ES.IESWindowPresentationMetadata metadata
                ? metadata.ESWindow_PresentationTitle
                : binding.window?.titleContent?.text;
            binding.semiSleepTitleLabel = new Label(
                string.IsNullOrWhiteSpace(title) ? "工具窗口" : title.Trim())
            {
                pickingMode = PickingMode.Ignore
            };
            binding.semiSleepTitleLabel.style.maxWidth = 172f;
            binding.semiSleepTitleLabel.style.marginTop = 3f;
            binding.semiSleepTitleLabel.style.fontSize = 9f;
            binding.semiSleepTitleLabel.style.color = SectionMutedTextColor;
            binding.semiSleepTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            binding.semiSleepTitleLabel.style.overflow = Overflow.Hidden;
            binding.semiSleepTitleLabel.style.textOverflow = TextOverflow.Ellipsis;
            overlay.Add(binding.semiSleepTitleLabel);
            binding.semiSleepPromotionProgress = CreateSemiSleepDiagnosticBar("ESPromotionProgress");
            binding.semiSleepDockProgress = CreateSemiSleepDiagnosticBar("ESDockProgress");
            binding.semiSleepPromotionProgress.style.display = DisplayStyle.None;
            binding.semiSleepDockProgress.style.display = DisplayStyle.None;
            overlay.Add(binding.semiSleepPromotionProgress);
            overlay.Add(binding.semiSleepDockProgress);
            overlay.RegisterCallback<PointerEnterEvent>(OnSemiSleepOverlayPointerEnter, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerLeaveEvent>(OnSemiSleepOverlayPointerLeave, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerDownEvent>(OnSemiSleepOverlayPointerDown, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerMoveEvent>(OnSemiSleepOverlayPointerMove, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerUpEvent>(OnSemiSleepOverlayPointerUp, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCancelEvent>(OnSemiSleepOverlayPointerCancel, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCaptureOutEvent>(OnSemiSleepOverlayPointerCaptureOut, TrickleDown.TrickleDown);
            return overlay;
        }

        private static VisualElement CreateSemiSleepDiagnosticBar(string name)
        {
            // Diagnostic only: never become a pointer target, otherwise moving
            // across the two rails can synthesize overlay leave/enter events.
            var bar = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = 8f;
            bar.style.bottom = 4f;
            bar.style.height = 3f;
            bar.style.width = 0f;
            bar.style.minWidth = 0f;
            bar.style.maxWidth = 82f;
            bar.style.backgroundColor = SectionMutedTextColor;
            bar.style.flexShrink = 0f;
            return bar;
        }

        private static Texture ResolveWindowPresentationIcon(EditorWindow window)
        {
            string metadataTitle = null;
            if (window is ES.IESWindowPresentationMetadata metadata)
            {
                metadataTitle = metadata.ESWindow_PresentationTitle;
                if (metadata.ESWindow_PresentationIcon != null)
                    return metadata.ESWindow_PresentationIcon;
            }
            // 只有 Presentation metadata 才能声明标题图标；普通窗口的
            // titleContent.image 可能是遗留宿主图标，不能覆盖当前业务语义。
            return ResolveDefaultWindowIcon(window, metadataTitle ?? window?.titleContent?.text, null);
        }

        internal static Texture ResolveDefaultWindowIcon(
            EditorWindow window,
            string title,
            string pagePath)
        {
            string brandResourceName = ResolveESBrandIconResourceName(
                window?.GetType(), title, pagePath);
            // These two legacy placeholders are visually empty rounded squares.
            // Keep their semantic names available to callers, but do not present
            // an empty shape as a product icon when Unity has a precise fallback.
            Texture brandIcon = brandResourceName == "workbench"
                || brandResourceName == "inspector"
                ? null
                : LoadESBrandIcon(brandResourceName);
            if (brandIcon != null)
                return brandIcon;
            string iconName = ResolveDefaultWindowIconName(window?.GetType(), title, pagePath);
            return LoadUnityIcon(iconName)
                ?? LoadUnityIcon("d_UnityEditor.ConsoleWindow")
                ?? LoadUnityIcon("d_console.infoicon");
        }

        internal static string ResolveDefaultWindowIconName(
            Type windowType,
            string title,
            string pagePath)
        {
            // 标题与页路径是窗口主动声明的语义，必须优先于类型名回退。
            // 否则测试程序集、Window/Inspector 等技术后缀会污染真正的业务图标。
            string semanticKey = ((title ?? string.Empty) + " " + (pagePath ?? string.Empty))
                .ToLowerInvariant();
            string resolved = ResolveDefaultWindowIconNameFromKey(semanticKey, false);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
            string typeKey = (windowType?.Name ?? string.Empty).ToLowerInvariant();
            return ResolveDefaultWindowIconNameFromKey(typeKey, true)
                ?? "d_UnityEditor.ConsoleWindow";
        }

        private static string ResolveDefaultWindowIconNameFromKey(string key, bool typeFallback)
        {
            // Concrete asset/tool semantics must win over broad host words such
            // as Graph or Window. This keeps Shader Graph, particle and authoring
            // pages from inheriting a generic workflow icon.
            if (ContainsAny(key, "shadergraph", "shader graph", "着色器图"))
                return "d_Shader Icon";
            if (ContainsAny(key, "material", "材质"))
                return "d_Material Icon";
            if (ContainsAny(key, "shader", "着色器"))
                return "d_Shader Icon";
            if (ContainsAny(key, "particle", "particlesystem", "vfx", "effect", "粒子", "特效"))
                return "d_ParticleSystem Icon";
            if (ContainsAny(key, "prefab", "预制体"))
                return "d_Prefab Icon";
            if (ContainsAny(key, "model", "mesh", "模型", "网格"))
                return "d_Mesh Icon";
            if (ContainsAny(key, "hierarchy", "层级"))
                return "d_UnityEditor.SceneHierarchyWindow";
            if (ContainsAny(key, "font", "字体"))
                return "d_Font Icon";
            if (ContainsAny(key, "audio", "sound", "音频", "音效"))
                return "d_AudioClip Icon";
            if (ContainsAny(key, "graph", "node", "flow", "图表", "节点", "流程"))
                return "d_AnimatorController Icon";
            if (ContainsAny(key, "track", "timeline", "animation", "动作", "轨道"))
                return "d_AnimationClip Icon";
            if (ContainsAny(key, "camera", "相机"))
                return "d_Camera Icon";
            if (ContainsAny(key, "scene", "world", "map", "environment", "场景", "地图", "环境"))
                return "d_UnityEditor.SceneView";
            if (ContainsAny(key, "build", "bake", "release", "publish", "构建", "发布"))
                return "d_BuildSettings.Editor.Small";
            if (ContainsAny(key, "package", "installer", "dependency", "安装", "依赖"))
                return "d_Package Manager";
            if (ContainsAny(key, "health", "diagnostic", "validation", "test", "progress", "验证", "诊断", "测试", "进度"))
                return "d_console.infoicon";
            if (ContainsAny(key, "agent", "automation", "command", "协作", "自动化"))
                return "d_UnityEditor.ConsoleWindow";
            if (ContainsAny(key, "settings", "config", "theme", "设置", "配置", "主题"))
                return "d_Settings Icon";
            if (ContainsAny(key, "inspector", "drawer", "property", "检查器", "属性"))
                return "d_UnityEditor.InspectorWindow";
            if (ContainsAny(key, "data", "table", "catalog", "数据", "数据表", "目录")
                || typeFallback && ContainsAny(key, "sodata", "scriptableobject"))
                return "d_ScriptableObject Icon";
            if (ContainsAny(key, "asset", "resource", "资源", "资产")
                || typeFallback && ContainsAny(key, "esreswindow", "resourcewindow"))
                return "d_Project";
            return null;
        }

        /// <summary>
        /// 返回 ES 品牌语义图标的项目内资源名。没有明确语义时使用中性的
        /// workbench 图标，避免把未知编辑器窗口误标成资源文件夹。
        /// </summary>
        internal static string ResolveESBrandIconResourceName(
            Type windowType,
            string title,
            string pagePath)
        {
            string displayKey = ((title ?? string.Empty) + " " + (pagePath ?? string.Empty))
                .ToLowerInvariant();
            // 相机有明确的 Unity 原生语义图标；不要拿场景品牌图标冒充相机。
            if (ContainsAny(displayKey, "camera", "相机"))
                return null;
            // Concrete Unity asset icons are more precise than an ES host brand.
            // Returning null here deliberately lets ResolveDefaultWindowIcon use
            // the matching built-in icon instead of the generic workbench mark.
            if (ContainsAny(displayKey,
                    "shadergraph", "shader graph", "shader", "着色器",
                    "material", "材质", "particle", "particlesystem", "vfx",
                    "effect", "粒子", "特效", "prefab", "预制体",
                    "model", "mesh", "模型", "网格", "hierarchy", "层级"))
                return null;
            string resolved = ResolveESBrandIconResourceNameFromKey(displayKey, false);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
            string typeKey = (windowType?.FullName ?? string.Empty).ToLowerInvariant();
            return ResolveESBrandIconResourceNameFromKey(typeKey, true);
        }

        private static string ResolveESBrandIconResourceNameFromKey(
            string key,
            bool typeFallback)
        {
            if (ContainsAny(key, "camera", "相机"))
                return null;
            // 先命中用户真正操作的业务对象，再处理宿主/技术名词。
            if (ContainsAny(key, "graph", "track", "timeline", "animation", "node", "flow",
                    "图表", "节点", "流程", "动作", "轨道")) return "graph";
            if (ContainsAny(key, "agent", "协作")) return "agent";
            if (ContainsAny(key, "automation", "自动化", "command")) return "automation";
            if (ContainsAny(key, "diagnostic", "validation", "test", "health", "progress",
                    "验证", "诊断", "测试", "健康", "进度")) return "diagnostics";
            if (ContainsAny(key, "font", "字体")) return "font";
            if (ContainsAny(key, "audio", "sound", "音频", "音效")) return "audio";
            if (ContainsAny(key, "scene", "world", "map", "environment", "场景", "地图", "环境")) return "scene";
            if (ContainsAny(key, "build", "bake", "release", "publish", "构建", "发布")) return "build";
            if (ContainsAny(key, "package", "installer", "dependency", "安装", "依赖")) return "package";
            if (ContainsAny(key, "theme", "settings", "设置", "主题")) return "settings";
            if (ContainsAny(key, "config", "配置")) return "config";
            if (ContainsAny(key, "inspector", "drawer", "property", "检查器", "属性")) return "inspector";
            if (ContainsAny(key, "content", "gameplay", "character", "skill", "item",
                    "内容", "角色", "技能", "物品")) return "content";
            if (ContainsAny(key, "data", "table", "catalog", "数据", "数据表", "目录")
                || typeFallback && ContainsAny(key, "sodata", "scriptableobject")) return "data";
            if (ContainsAny(key, "asset", "resource", "资源", "资产")
                || typeFallback && ContainsAny(key, "esreswindow", "resourcewindow")) return "assets";
            if (ContainsAny(key, "workbench", "dialog", "popup", "工作台", "对话框", "弹窗")) return "workbench";
            return "workbench";
        }

        internal static Texture LoadESBrandIcon(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                return null;
            string key = resourceName.Trim();
            if (esBrandIconCache.TryGetValue(key, out Texture cached))
            {
                if (cached != null)
                    return cached;
                // Asset reimport may invalidate the native Texture behind the
                // managed reference. Drop the stale entry and resolve again.
                esBrandIconCache.Remove(key);
            }
            Texture icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Plugins/ES/Editor/Resources/ESBrandIcons/"
                + key + ".png");
            if (icon != null)
                esBrandIconCache[key] = icon;
            return icon;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
                if (!string.IsNullOrEmpty(tokens[i])
                    && value.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        internal static Texture LoadUnityIcon(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
                return null;
            string normalized = iconName.Trim();
            Texture icon = EditorGUIUtility.Load("Icons/" + normalized + ".png") as Texture;
            if (icon == null && normalized.StartsWith("d_", StringComparison.Ordinal))
                icon = EditorGUIUtility.Load("Icons/" + normalized.Substring(2) + ".png") as Texture;
            return icon;
        }

        private static void OnSemiSleepOverlayPointerDown(PointerDownEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            VisualElement overlay = evt.currentTarget as VisualElement;
            if (binding?.window == null
                || binding.lifecycleSuspended
                || overlay == null
                || evt.button != 0)
                return;
            RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);
            binding.edgeTabHoverIntentStartedAt = -1d;
            // A user gesture owns the native frame. In particular, invalidate a
            // domain-reload recovery callback before it can write the old frame back.
            binding.restorePersistedSleepOnBind = false;
            binding.restorePersistedSleepScheduled = false;
            binding.persistedSleepGeometryVerifyUntil = -1d;
            binding.persistedSleepGeometryRepairScheduled = false;
            if (binding.semiSleepAnimating
                && binding.transitionTargetState == ESWindowVisualState.SleepTile)
            {
                // Do not snap to the stale animation target under the pointer. The
                // current intermediate frame becomes the drag origin instead.
                binding.visualState = ESWindowVisualState.SleepTile;
                binding.semiSleeping = true;
                binding.semiSleepTarget = true;
                binding.semiSleepAnimating = false;
                binding.semiSleepFromBounds = binding.window.position;
                binding.semiSleepToBounds = binding.window.position;
                ApplySemiSleepOverlayState(binding, ESWindowVisualState.SleepTile);
                RefreshSemiSleepUpdateSubscription();
            }
            else if (binding.semiSleepAnimating
                && (binding.transitionTargetState == ESWindowVisualState.EdgeTab
                    || binding.transitionTargetState == ESWindowVisualState.EdgeTabHover))
            {
                // Freeze the current native frame as an expanded tab drag origin.
                // On release it will collapse from this exact geometry, instead of
                // snapping to either end of the interrupted hover animation.
                binding.visualState = ESWindowVisualState.EdgeTabHover;
                binding.semiSleeping = true;
                binding.semiSleepTarget = true;
                binding.semiSleepAnimating = false;
                binding.semiSleepFromBounds = binding.window.position;
                binding.semiSleepToBounds = binding.window.position;
                ApplySemiSleepOverlayState(binding, ESWindowVisualState.EdgeTabHover);
                RefreshSemiSleepUpdateSubscription();
            }
            if (binding.semiSleepAnimating
                || binding.visualState != ESWindowVisualState.SleepTile
                    && binding.visualState != ESWindowVisualState.EdgeTab
                    && binding.visualState != ESWindowVisualState.EdgeTabHover)
            {
                binding.semiSleepManualHold = false;
                binding.window.Focus();
                BeginSemiSleepTransition(binding, false);
                evt.StopImmediatePropagation();
                return;
            }
            binding.semiSleepDragPointerId = evt.pointerId;
            binding.semiSleepDragStartState = binding.visualState;
            // Store a fixed screen-space pointer anchor and native frame. Every move
            // is solved from this anchor, so the moving native frame cannot feed its
            // changing panel-local coordinates back into the next target.
            binding.semiSleepDragWindowStart = binding.window.position;
            binding.semiSleepDragScreenStart = binding.window.position.position
                + new Vector2(evt.position.x, evt.position.y);
            binding.semiSleepDragPendingBounds = binding.window.position;
            binding.semiSleepDragPendingEdgeOffset = binding.edgeOffset;
            binding.hasSemiSleepDragPendingBounds = false;
            binding.semiSleepDragging = false;
            overlay.CapturePointer(evt.pointerId);
            nextSemiSleepIdleCheckAt = 0d;
            RefreshSemiSleepUpdateSubscription();
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerMove(PointerMoveEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding?.window == null || binding.lifecycleSuspended)
                return;

            if (binding.visualState == ESWindowVisualState.EdgeTabHover
                || binding.semiSleepAnimating
                && binding.transitionTargetState == ESWindowVisualState.EdgeTabHover)
            {
                Vector2 hoverPointerPosition = binding.window.position.position
                    + new Vector2(evt.position.x, evt.position.y);
                if (ShouldResetEdgeTabHoverCommit(
                    binding.edgeTabLastPointerPosition,
                    hoverPointerPosition,
                    binding.hasEdgeTabPointerPosition))
                {
                    binding.edgeTabLastPointerPosition = hoverPointerPosition;
                    binding.hasEdgeTabPointerPosition = true;
                    if (binding.visualState == ESWindowVisualState.EdgeTabHover
                        && !binding.semiSleepAnimating)
                        binding.edgeTabFullyExpandedAt = EditorApplication.timeSinceStartup;
                }
            }

            if (binding.semiSleepDragPointerId != evt.pointerId)
                return;
            if (binding.semiSleepRecaptureScheduled)
            {
                // The native frame moved after the last accepted event. Ignore
                // panel coordinates until pointer capture is re-established;
                // those coordinates may still be relative to the previous frame
                // and would otherwise create a one-frame teleport and rollback.
                evt.StopImmediatePropagation();
                return;
            }

            bool tileDrag = binding.semiSleepDragStartState == ESWindowVisualState.SleepTile;
            bool edgeTabDrag = binding.semiSleepDragStartState == ESWindowVisualState.EdgeTab
                || binding.semiSleepDragStartState == ESWindowVisualState.EdgeTabHover;
            if (!tileDrag && !edgeTabDrag)
                return;

            RecordWindowInteraction(binding, EditorApplication.timeSinceStartup);

            // PointerEvent.position is panel-local. Convert it back to a stable
            // screen coordinate using the current native frame before solving the
            // target; otherwise moving the window changes the local coordinate and
            // feeds a false reverse delta into the next event.
            Vector2 currentScreenPosition = binding.window.position.position
                + new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = currentScreenPosition - binding.semiSleepDragScreenStart;
            if (!IsFinite(delta))
                return;
            if (!binding.semiSleepDragging
                && delta.sqrMagnitude < SemiSleepDragThreshold * SemiSleepDragThreshold)
                return;

            binding.semiSleepDragging = true;
            Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
            Rect target;
            if (edgeTabDrag)
            {
                target = EvaluateEdgeTabDragFrame(
                    binding.semiSleepDragWindowStart,
                    delta,
                    workArea,
                    binding.edge,
                    out float edgeOffset);
                binding.semiSleepDragPendingEdgeOffset = edgeOffset;
            }
            else
            {
                target = EvaluateSemiSleepDragFrame(
                    binding.semiSleepDragWindowStart,
                    delta,
                    workArea);
            }
            // Native EditorWindow movement can invalidate pointer capture. Do not
            // mutate it re-entrantly from the pointer callback; the editor update
            // loop coalesces multiple events into one stable frame write.
            binding.semiSleepDragPendingBounds = target;
            binding.hasSemiSleepDragPendingBounds = true;
            nextSemiSleepIdleCheckAt = 0d;
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerUp(PointerUpEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            VisualElement overlay = evt.currentTarget as VisualElement;
            if (binding?.window == null
                || binding.lifecycleSuspended
                || overlay == null
                || binding.semiSleepDragPointerId != evt.pointerId)
                return;

            bool dragged = binding.semiSleepDragging;
            ESWindowVisualState dragStartState = binding.semiSleepDragStartState;
            ApplyPendingSemiSleepDragFrame(binding);
            if (overlay.HasPointerCapture(evt.pointerId))
                overlay.ReleasePointer(evt.pointerId);
            ResetSemiSleepDrag(binding);
            if (dragged)
            {
                binding.semiSleepManualHold = true;
                bool edgeTabDrag = dragStartState == ESWindowVisualState.EdgeTab
                    || dragStartState == ESWindowVisualState.EdgeTabHover;
                if (edgeTabDrag)
                {
                    binding.semiSleeping = true;
                    binding.semiSleepTarget = true;
                    // Releasing a moved tab must settle once. Requiring a real
                    // leave/re-enter prevents the pointer that performed the drag
                    // from immediately opening the hover transition again.
                    binding.pointerInside = false;
                    binding.edgeTabHoverIntentStartedAt = -1d;
                    binding.edgeTabFullyExpandedAt = -1d;
                    binding.semiSleepFromBounds = binding.window.position;
                    binding.semiSleepToBounds = binding.window.position;
                    if (dragStartState == ESWindowVisualState.EdgeTabHover)
                    {
                        binding.visualState = ESWindowVisualState.EdgeTabHover;
                        BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTab);
                    }
                    else
                    {
                        binding.visualState = ESWindowVisualState.EdgeTab;
                        binding.transitionTargetState = ESWindowVisualState.EdgeTab;
                        ShowSemiSleepOverlay(binding, true, 1f);
                        ApplySemiSleepOverlayState(binding, ESWindowVisualState.EdgeTab);
                        RefreshSemiSleepControls(binding);
                        SaveSemiSleepPreferences(binding);
                    }
                }
                else
                {
                    Rect dockBounds = ClampSemiSleepDockBounds(
                        binding.window.position,
                        GetSemiSleepTrayBounds(binding.awakeBounds));
                    CommitSemiSleepWindowPosition(binding, dockBounds);
                    binding.semiSleepToBounds = dockBounds;
                    binding.semiSleepDockBounds = dockBounds;
                    binding.hasSemiSleepDockBounds = true;
                    binding.visualState = ESWindowVisualState.SleepTile;
                    binding.transitionTargetState = ESWindowVisualState.SleepTile;
                    binding.sleepTileIdleStartedAt = EditorApplication.timeSinceStartup;
                    ShowSemiSleepOverlay(binding, true, 1f);
                    ApplySemiSleepOverlayState(binding);
                    RefreshSemiSleepControls(binding);
                    SaveSemiSleepPreferences(binding);
                }
            }
            else
            {
                binding.semiSleepManualHold = false;
                binding.window.Focus();
                BeginSemiSleepTransition(binding, false);
                SaveSemiSleepPreferences(binding);
            }
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerEnter(PointerEnterEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding == null
                || binding.lifecycleSuspended
                || evt.target != evt.currentTarget)
                return;
            binding.pointerInside = true;
            binding.edgeTabHoverExitGraceUntil = -1d;
            // PointerEvent.position is panel-local. A right/bottom anchored tab changes
            // its panel origin while extending, so comparing local values would treat
            // the window's own animation as user movement and reset the dwell timer.
            binding.edgeTabLastPointerPosition = binding.window != null
                ? binding.window.position.position + new Vector2(evt.position.x, evt.position.y)
                : new Vector2(evt.position.x, evt.position.y);
            binding.hasEdgeTabPointerPosition = true;
            double now = EditorApplication.timeSinceStartup;
            RecordWindowInteraction(binding, now);
            if ((binding.visualState == ESWindowVisualState.EdgeTab
                    && !binding.semiSleepAnimating)
                || (binding.semiSleepAnimating
                    && binding.transitionTargetState == ESWindowVisualState.EdgeTab))
            {
                binding.edgeTabHoverIntentStartedAt = now;
                nextSemiSleepIdleCheckAt = 0d;
                RefreshSemiSleepUpdateSubscription();
            }
        }

        private static void OnSemiSleepOverlayPointerLeave(PointerLeaveEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding == null
                || binding.lifecycleSuspended
                || evt.target != evt.currentTarget)
                return;
            binding.pointerInside = false;
            binding.hasEdgeTabPointerPosition = false;
            binding.edgeTabHoverIntentStartedAt = -1d;
            binding.edgeTabHoverExitGraceUntil = EditorApplication.timeSinceStartup
                + EdgeTabHoverExitGrace;
            RefreshSemiSleepUpdateSubscription();
        }

        private static void OnSemiSleepOverlayPointerCancel(PointerCancelEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding == null || binding.lifecycleSuspended)
                return;
            CancelSemiSleepDrag(evt.currentTarget as VisualElement, evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            VisualElement overlay = evt.currentTarget as VisualElement;
            WindowBinding binding = overlay?.userData as WindowBinding;
            if (binding == null
                || binding.lifecycleSuspended
                || overlay == null
                || binding.semiSleepDragPointerId != evt.pointerId
                || binding.semiSleepRecaptureScheduled)
                return;

            // Native EditorWindow movement can emit PointerCaptureOut. Re-capturing
            // synchronously here creates a capture-out/capture loop. Defer one
            // guarded recapture so subsequent moves remain available without
            // re-entering the event currently being dispatched.
            binding.semiSleepRecaptureScheduled = true;
            int pointerId = evt.pointerId;
            overlay.schedule.Execute(() =>
            {
                binding.semiSleepRecaptureScheduled = false;
                if (binding.window == null
                    || binding.lifecycleSuspended
                    || EditorApplication.isPlayingOrWillChangePlaymode
                    || binding.semiSleepDragPointerId != pointerId
                    || overlay.panel == null
                    || overlay.HasPointerCapture(pointerId))
                    return;
                overlay.CapturePointer(pointerId);
            });
        }

        private static void CancelSemiSleepDrag(VisualElement overlay, int pointerId)
        {
            WindowBinding binding = overlay?.userData as WindowBinding;
            if (binding == null
                || binding.lifecycleSuspended
                || binding.semiSleepDragPointerId != pointerId)
                return;
            ESWindowVisualState dragStartState = binding.semiSleepDragStartState;
            ApplyPendingSemiSleepDragFrame(binding);
            if (binding.semiSleepDragging && binding.window != null)
            {
                // Pointer cancellation can be emitted when the native frame changes
                // under the pointer. Preserve the latest valid position instead of
                // snapping back to the drag origin.
                binding.semiSleepManualHold = true;
                binding.semiSleeping = true;
                binding.semiSleepTarget = true;
                bool edgeTabDrag = dragStartState == ESWindowVisualState.EdgeTab
                    || dragStartState == ESWindowVisualState.EdgeTabHover;
                if (edgeTabDrag)
                {
                    binding.pointerInside = false;
                    binding.edgeTabHoverIntentStartedAt = -1d;
                    binding.visualState = ESWindowVisualState.EdgeTabHover;
                    binding.transitionTargetState = ESWindowVisualState.EdgeTabHover;
                    binding.semiSleepFromBounds = binding.window.position;
                    binding.semiSleepToBounds = binding.window.position;
                    binding.edgeTabFullyExpandedAt = -1d;
                }
                else
                {
                    Rect dockBounds = ClampSemiSleepDockBounds(
                        binding.window.position,
                        GetSemiSleepTrayBounds(binding.awakeBounds));
                    CommitSemiSleepWindowPosition(binding, dockBounds);
                    binding.semiSleepToBounds = dockBounds;
                    binding.semiSleepDockBounds = dockBounds;
                    binding.hasSemiSleepDockBounds = true;
                    binding.visualState = ESWindowVisualState.SleepTile;
                    binding.transitionTargetState = ESWindowVisualState.SleepTile;
                    binding.sleepTileIdleStartedAt = EditorApplication.timeSinceStartup;
                    SaveSemiSleepPreferences(binding);
                }
            }
            if (overlay.HasPointerCapture(pointerId))
                overlay.ReleasePointer(pointerId);
            ResetSemiSleepDrag(binding);
            if (binding.window != null
                && (dragStartState == ESWindowVisualState.EdgeTab
                    || dragStartState == ESWindowVisualState.EdgeTabHover))
            {
                BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTab);
            }
        }

        private static void ApplyPendingSemiSleepDragFrame(WindowBinding binding)
        {
            if (binding?.window == null || !binding.hasSemiSleepDragPendingBounds)
                return;
            Rect target = binding.semiSleepDragPendingBounds;
            binding.hasSemiSleepDragPendingBounds = false;
            if (!IsFinite(target.position) || !IsFinite(target.size))
                return;
            CommitSemiSleepWindowPosition(binding, target);
            binding.semiSleepToBounds = target;
            if (binding.semiSleepDragStartState == ESWindowVisualState.EdgeTab
                || binding.semiSleepDragStartState == ESWindowVisualState.EdgeTabHover)
                binding.edgeOffset = binding.semiSleepDragPendingEdgeOffset;
        }

        private static void ResetSemiSleepDrag(WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.semiSleepDragPointerId = -1;
            binding.semiSleepDragStartState = ESWindowVisualState.ActivePanel;
            binding.hasSemiSleepDragPendingBounds = false;
            binding.semiSleepDragging = false;
            binding.semiSleepRecaptureScheduled = false;
            RefreshSemiSleepUpdateSubscription();
        }

        private static void AttachSemiSleepControls(WindowBinding binding)
        {
            RemoveSemiSleepControls(binding);
            if (binding?.root == null || binding.window == null || !binding.supportsSemiSleep)
                return;

            VisualElement toolbar = FindDeclaredSystemActionHost(binding);
            if (toolbar == null)
                return;

            var controls = new VisualElement
            {
                name = "ESWindowSystemActions",
                tooltip = "系统：窗口生命周期与休眠控制"
            };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 1f;
            controls.style.minWidth = 0f;
            controls.style.overflow = Overflow.Visible;

            binding.semiSleepToggleButton = ESWindowPresentation.CreateHeaderActionButton(
                null,
                "休眠",
                "立即收起到休眠托盘；休眠后单击恢复，拖动可修改下次收纳位置。",
                () => ToggleSemiSleepFromHeader(binding));
            controls.Add(binding.semiSleepToggleButton);
            binding.semiSleepOverflowMenu = CreateSemiSleepOverflowMenu(binding);
            controls.Add(binding.semiSleepOverflowMenu);

            controls.style.marginRight = 4f;
            VisualElement systemActions = toolbar.Q<VisualElement>("ESMenuTreeSystemActions");
            if (systemActions != null)
            {
                systemActions.style.flexGrow = 1f;
                systemActions.style.flexShrink = 1f;
                systemActions.style.minWidth = 0f;
                systemActions.style.flexWrap = Wrap.Wrap;
                systemActions.style.overflow = Overflow.Visible;
                systemActions.Add(controls);
            }
            else
                toolbar.Insert(0, controls);

            binding.semiSleepControls = controls;
            RefreshSemiSleepControls(binding);
        }

        internal static bool HasDeclaredSystemActionHost(ES.ESWindowActionHosts actionHosts)
        {
            return actionHosts?.System != null;
        }

        private static VisualElement FindDeclaredSystemActionHost(WindowBinding binding)
        {
            if (binding?.root == null)
                return null;
            VisualElement declared = binding.actionHosts?.System;
            if (IsDescendantOf(declared, binding.root))
                return declared;
            return null;
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement root)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (current == root)
                    return true;
            return false;
        }

        internal static bool ShouldCompactSystemActions(float rootWidth)
        {
            // Geometry 在窗口首次绑定时可能暂时为 0；此时先隐藏主按钮，
            // 由稳定的系统菜单承载全部配置动作。
            return rootWidth <= 0f || rootWidth < 960f;
        }

        internal static bool ShouldShowPrimarySystemAction(float rootWidth)
        {
            return rootWidth <= 0f || rootWidth >= 560f;
        }

        private static ToolbarMenu CreateSemiSleepOverflowMenu(WindowBinding binding)
        {
            ToolbarMenu menu = ESWindowPresentation.CreateHeaderOverflowMenu(
                "ESWindowSystemActionsOverflow",
                "系统",
                "系统设置：窗口休眠、自动模式与全局策略",
                52f);
            menu.menu.AppendAction(
                "允许参与休眠",
                _ => SetWindowSemiSleepAllowed(binding.window, !binding.allowSemiSleep),
                _ => binding.supportsSemiSleep && binding.allowSemiSleep
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "立即休眠",
                _ => RequestWindowSemiSleep(binding.window),
                _ => CanUseSleepCommand(binding) && !IsSleepingOrTargetingSleep(binding)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction(
                "立即唤醒",
                _ => RequestWindowWake(binding.window),
                _ => CanUseSleepCommand(binding) && IsSleepingOrTargetingSleep(binding)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "自动模式",
                _ => SetWindowPinned(binding.window, false),
                _ => GetPinModeStatus(binding, false));
            menu.menu.AppendAction(
                "固定展开",
                _ => SetWindowPinned(binding.window, true),
                _ => GetPinModeStatus(binding, true));
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "全局自动半休眠",
                _ => SetSemiSleepEnabled(!SemiSleepEnabled),
                _ => SemiSleepEnabled
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            return menu;
        }

        private static bool CanUseSleepCommand(WindowBinding binding)
        {
            return binding?.window != null
                && EvaluateSemiSleepBlockReason(binding, false) == SemiSleepBlockReason.None;
        }

        private static bool IsSleepingOrTargetingSleep(WindowBinding binding)
        {
            return binding != null
                && (binding.restorePersistedSleepOnBind
                    || binding.semiSleeping
                    || binding.semiSleepAnimating && binding.semiSleepTarget);
        }

        private static DropdownMenuAction.Status GetPinModeStatus(
            WindowBinding binding,
            bool pinned)
        {
            if (!CanUseSleepCommand(binding))
                return DropdownMenuAction.Status.Disabled;
            return binding.pinned == pinned
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal;
        }

        private static void RemoveSemiSleepControls(WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.semiSleepControls?.RemoveFromHierarchy();
            binding.semiSleepControls = null;
            binding.semiSleepToggleButton = null;
            binding.semiSleepOverflowMenu = null;
        }

        private static void BringSemiSleepControlsToFront(WindowBinding binding)
        {
            // Declared toolbar hosts own layout and paint order. System actions never escape them.
        }

        private static void OnWindowGeometryChanged(GeometryChangedEvent evt)
        {
            VisualElement root = evt.currentTarget as VisualElement;
            if (root != null && windowBindingsByRoot.TryGetValue(root, out WindowBinding binding))
            {
                if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                    AttachSemiSleepControls(binding);
                RefreshSemiSleepControls(binding);
                ScheduleSettledSemiSleepGeometryRepair(binding);
            }
        }

        private static void ScheduleSettledSemiSleepGeometryRepair(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.root == null
                || binding.persistedSleepGeometryRepairScheduled
                || !binding.semiSleeping
                || binding.semiSleepAnimating
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0
                || binding.visualState != ESWindowVisualState.SleepTile
                    && binding.visualState != ESWindowVisualState.EdgeTab
                    && binding.visualState != ESWindowVisualState.EdgeTabHover)
                return;

            int bindingId = binding.window.GetInstanceID();
            binding.persistedSleepGeometryRepairScheduled = true;
            binding.root.schedule.Execute(() =>
            {
                if (!windowBindings.TryGetValue(bindingId, out WindowBinding current)
                    || !ReferenceEquals(current, binding))
                    return;
                binding.persistedSleepGeometryRepairScheduled = false;
                if (binding.lifecycleSuspended
                    || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                RepairSettledSemiSleepGeometry(binding);
            }).StartingIn(1);
        }

        private static void ToggleSemiSleepFromHeader(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (IsSleepingOrTargetingSleep(binding))
            {
                RequestWindowWake(binding.window);
            }
            else
            {
                RequestWindowSemiSleep(binding.window);
            }
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        private static void ToggleSemiSleepPinFromHeader(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            SetWindowPinned(binding.window, !binding.pinned);
        }

        private static void RefreshAllSemiSleepControls()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                RefreshSemiSleepControls(binding);
        }

        private static void RefreshSemiSleepControls(WindowBinding binding)
        {
            if (binding?.semiSleepControls == null)
                return;
            TryNormalizeUnexpectedAwakeGeometry(binding);
            bool sleeping = IsSleepingOrTargetingSleep(binding);
            bool visible = binding.supportsSemiSleep && binding.window != null;
            binding.semiSleepControls.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;
            if (sleeping)
                ApplySemiSleepOverlayState(binding);
            bool docked = binding.window.docked;
            bool commandsEnabled = binding.allowSemiSleep && !docked;
            float hostWidth = binding.actionHosts?.System?.resolvedStyle.width ?? 0f;
            float rootWidth = binding.root?.resolvedStyle.width ?? 0f;
            float availableWidth = hostWidth > 0f ? hostWidth : rootWidth;
            bool showPrimary = ShouldShowPrimarySystemAction(availableWidth);
            binding.semiSleepToggleButton.style.display = showPrimary ? DisplayStyle.Flex : DisplayStyle.None;
            if (binding.semiSleepOverflowMenu != null)
            {
                // Keep configuration in one stable menu at every width. This avoids
                // settings moving between buttons and the overflow menu on resize.
                binding.semiSleepOverflowMenu.style.display = DisplayStyle.Flex;
                binding.semiSleepOverflowMenu.tooltip = docked
                    ? "停靠窗口保持展开；拖出后可使用休眠控制。"
                    : sleeping
                        ? "窗口正在休眠；打开菜单可立即唤醒或调整模式。"
                        : "系统设置：窗口休眠、自动模式与全局策略";
            }

            if (binding.semiSleepToggleButton != null)
            {
                SetHeaderActionButtonText(
                    binding.semiSleepToggleButton,
                    docked ? "停靠" : sleeping ? "唤醒" : "收起");
                ESWindowPresentation.SetButtonEnabled(
                    binding.semiSleepToggleButton,
                    commandsEnabled);
                binding.semiSleepToggleButton.tooltip = !binding.allowSemiSleep
                    ? "此窗口已禁用休眠；打开“系统”菜单可重新允许。"
                    : docked
                    ? "停靠窗口保持展开；拖出为浮动窗口后可使用休眠模式。"
                    : sleeping
                    ? "恢复窗口；也可单击休眠块恢复，拖动休眠块修改收纳位置。"
                    : "立即收起到休眠托盘；休眠后单击恢复，拖动可修改下次收纳位置。";
                ESWindowPresentation.SetButtonPresentationState(
                    binding.semiSleepToggleButton,
                    sleeping ? ESPresentationState.Selected : ESPresentationState.Normal);
            }
        }

        private static void SetHeaderActionButtonText(Button button, string text)
        {
            if (button == null)
                return;
            Label label = button.Q<Label>();
            if (label != null)
                label.text = text ?? string.Empty;
            else
                button.text = text ?? string.Empty;
        }

        private static void RefreshSemiSleepUpdateSubscription()
        {
            bool shouldSubscribe = (SemiSleepEnabled || focusModeWindowId != 0 || HasSemiSleepRuntimeState())
                && GlobalEditorShellEnabled
                && HasSemiSleepCandidates();
            if (shouldSubscribe == semiSleepUpdateSubscribed)
                return;
            semiSleepUpdateSubscribed = shouldSubscribe;
            EditorApplication.update -= UpdateSemiSleepWindows;
            EditorApplication.quitting -= RestoreAllSemiSleepWindows;
            if (!shouldSubscribe)
            {
                semiSleepAnyAnimating = false;
                return;
            }
            nextSemiSleepIdleCheckAt = 0d;
            EditorApplication.update += UpdateSemiSleepWindows;
            EditorApplication.quitting += RestoreAllSemiSleepWindows;
        }

        private static bool HasSemiSleepCandidates()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null
                    && binding.window != null
                    && !binding.lifecycleSuspended
                    && (HasPersistedSleepRuntimeState(binding)
                        || HasBlockedSemiSleepStateToNormalize(binding)
                        || binding.allowSemiSleep
                            && (binding.semiSleepAnimating
                                || binding.semiSleepDragPointerId >= 0
                                || binding.hasSemiSleepDragPendingBounds
                                || binding.visualState == ESWindowVisualState.ActivePanel
                                || binding.visualState == ESWindowVisualState.SleepTile
                                || binding.visualState == ESWindowVisualState.EdgeTabHover
                                || (binding.visualState == ESWindowVisualState.EdgeTab
                                    && binding.pointerInside)
                                || binding.sleepLinkMode == ES.ESWindowSleepLinkMode.FollowOwner)))
                    return true;
            return false;
        }

        private static bool HasSemiSleepRuntimeState()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null
                    && (HasPersistedSleepRuntimeState(binding)
                        || HasBlockedSemiSleepStateToNormalize(binding)
                        || binding.semiSleepAnimating
                        || binding.semiSleepDragPointerId >= 0
                        || binding.hasSemiSleepDragPendingBounds
                        || binding.visualState == ESWindowVisualState.SleepTile
                        || binding.visualState == ESWindowVisualState.EdgeTabHover
                        || (binding.visualState == ESWindowVisualState.EdgeTab
                            && binding.pointerInside)
                        || binding.sleepLinkMode == ES.ESWindowSleepLinkMode.FollowOwner))
                    return true;
            return false;
        }

        private static bool HasPersistedSleepRuntimeState(WindowBinding binding)
        {
            return binding != null
                && (binding.restorePersistedSleepOnBind
                    || binding.restorePersistedSleepScheduled
                    || binding.persistedSleepGeometryVerifyUntil >= 0d);
        }

        private static bool HasBlockedSemiSleepStateToNormalize(WindowBinding binding)
        {
            return HasSemiSleepStateToNormalize(binding)
                && !CanEnterSemiSleep(binding, false);
        }

        private static bool ShouldEvaluateSemiSleepBinding(WindowBinding binding)
        {
            if (binding == null)
                return false;
            if (binding.lifecycleSuspended)
                return false;
            if (binding.restorePersistedSleepOnBind
                || binding.restorePersistedSleepScheduled
                || binding.persistedSleepGeometryVerifyUntil >= 0d
                || HasBlockedSemiSleepStateToNormalize(binding)
                || binding.semiSleepAnimating
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0)
                return true;

            switch (binding.visualState)
            {
                case ESWindowVisualState.ActivePanel:
                case ESWindowVisualState.SleepTile:
                case ESWindowVisualState.EdgeTabHover:
                    return true;
                case ESWindowVisualState.EdgeTab:
                    // A settled independent tab is event-driven: PointerEnter/Leave
                    // owns hover transitions. FollowOwner still needs the low-cost
                    // synchronization pass in case its owner changes state.
                    return binding.pointerInside
                        || binding.sleepLinkMode == ES.ESWindowSleepLinkMode.FollowOwner;
                default:
                    return true;
            }
        }

        private static bool IsSemiSleepEligible(WindowBinding binding)
        {
            return CanEnterSemiSleep(binding, true);
        }

        private static bool CanEnterSemiSleep(WindowBinding binding, bool requireAutomaticPolicy)
        {
            return EvaluateSemiSleepBlockReason(binding, requireAutomaticPolicy)
                == SemiSleepBlockReason.None;
        }

        private static SemiSleepBlockReason EvaluateSemiSleepBlockReason(
            WindowBinding binding,
            bool requireAutomaticPolicy)
        {
            if (binding == null || binding.window == null)
                return SemiSleepBlockReason.InvalidBinding;
            if (!binding.supportsSemiSleep)
                return SemiSleepBlockReason.Unsupported;
            if (binding.singleInstanceViolation)
                return SemiSleepBlockReason.DuplicateInstance;
            if (!binding.allowSemiSleep)
                return SemiSleepBlockReason.NotAllowed;
            if (binding.sleepLinkMode == ES.ESWindowSleepLinkMode.OwnedSurface)
                return SemiSleepBlockReason.OwnedSurface;
            if (binding.busyCount > 0)
                return SemiSleepBlockReason.Busy;
            if (focusModeWindowId == binding.window.GetInstanceID())
                return SemiSleepBlockReason.FocusMode;
            if (binding.window.docked)
                return SemiSleepBlockReason.Docked;
            if (binding.root == null || binding.root.panel == null)
                return SemiSleepBlockReason.PanelUnavailable;
            if (requireAutomaticPolicy && !SemiSleepEnabled)
                return SemiSleepBlockReason.GlobalAutoDisabled;
            if (requireAutomaticPolicy && binding.pinned)
                return SemiSleepBlockReason.Pinned;
            return SemiSleepBlockReason.None;
        }

        private static string GetSemiSleepBlockReasonText(SemiSleepBlockReason reason)
        {
            switch (reason)
            {
                case SemiSleepBlockReason.None: return string.Empty;
                case SemiSleepBlockReason.InvalidBinding: return "窗口绑定已失效。";
                case SemiSleepBlockReason.Unsupported: return "窗口契约未启用半休眠。";
                case SemiSleepBlockReason.DuplicateInstance:
                    return "同一窗口类型存在额外实例；仅首个实例允许参与休眠与持久化。";
                case SemiSleepBlockReason.NotAllowed: return "当前窗口已关闭半休眠。";
                case SemiSleepBlockReason.OwnedSurface: return "当前内容由父窗口承载，不独立休眠。";
                case SemiSleepBlockReason.Busy: return "窗口正在执行任务。";
                case SemiSleepBlockReason.FocusMode: return "窗口处于专注模式。";
                case SemiSleepBlockReason.Docked: return "停靠窗口不参与半休眠。";
                case SemiSleepBlockReason.PanelUnavailable: return "窗口界面尚未完成挂载。";
                case SemiSleepBlockReason.GlobalAutoDisabled: return "全局自动半休眠已关闭。";
                case SemiSleepBlockReason.Pinned: return "当前窗口已固定展开。";
                default: return "窗口当前不能进入半休眠。";
            }
        }

        private static bool IsPersistedSleepRestorePermanentlyBlocked(WindowBinding binding)
        {
            return binding == null
                || binding.window == null
                || !binding.supportsSemiSleep
                || !binding.allowSemiSleep
                || binding.sleepLinkMode == ES.ESWindowSleepLinkMode.OwnedSurface
                || binding.window.docked;
        }

        private static bool TryRestorePersistedSemiSleepGeometry(WindowBinding binding)
        {
            if (binding == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !binding.restorePersistedSleepOnBind)
                return false;
            if (IsPersistedSleepRestorePermanentlyBlocked(binding))
            {
                CancelPersistedSemiSleepRestore(binding);
                return false;
            }
            if (!CanEnterSemiSleep(binding, false))
                return false;
            RestorePersistedSemiSleepGeometry(binding);
            return true;
        }

        private static void CancelPersistedSemiSleepRestore(WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.restorePersistedSleepOnBind = false;
            binding.restorePersistedSleepScheduled = false;
            binding.persistedSleepGeometryVerifyUntil = -1d;
            binding.persistedSleepGeometryRepairScheduled = false;
            RestoreSemiSleep(binding, true);
            SaveSemiSleepPreferences(binding);
        }

        private static bool IsTransientFocusWindow(EditorWindow window)
        {
            if (window == null)
                return false;
            string typeName = window.GetType().Name;
            return typeName.IndexOf("ObjectSelector", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Picker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RecordWindowInteraction(WindowBinding binding, double now)
        {
            if (binding == null)
                return;
            binding.lastInteractionAt = now;
            binding.focusLostAt = -1d;
        }

        private static bool IsWindowInteractionHeld(WindowBinding binding, double now)
        {
            if (binding?.window == null)
                return false;
            bool pointerOver = binding.pointerInside
                || ReferenceEquals(EditorWindow.mouseOverWindow, binding.window);
            bool isLastFocused = lastFocusedWindowId == binding.window.GetInstanceID();
            bool transientFocus = isLastFocused
                && IsTransientFocusWindow(EditorWindow.focusedWindow);
            bool transientNullFocus = isLastFocused && EditorWindow.focusedWindow == null;
            return ShouldPauseAutomaticCollection(
                binding.pointerInside,
                ReferenceEquals(EditorWindow.mouseOverWindow, binding.window),
                binding.interactionHoldCount,
                binding.semiSleepDragPointerId >= 0,
                transientFocus,
                transientNullFocus,
                now <= binding.transientInteractionGraceUntil);
        }

        internal static bool ShouldPauseAutomaticCollection(
            bool pointerInside,
            bool mouseOverWindow,
            int interactionHolds,
            bool dragging,
            bool transientFocus,
            bool focusUnknown,
            bool withinGrace)
        {
            return pointerInside
                || mouseOverWindow
                || interactionHolds > 0
                || dragging
                || transientFocus
                || focusUnknown
                || withinGrace;
        }

        internal static bool ShouldPauseSleepTilePromotion(
            bool interactionHeld,
            bool pointerOver)
        {
            // Hovering a tile is discovery, not an interaction lease. An explicit
            // hold, drag, busy operation, or popup is a real lease and pauses the
            // promotion timer even when the pointer remains over the tile.
            // pointerOver stays in the contract so callers can pass the observed
            // state without creating a second hover-specific policy.
            _ = pointerOver;
            return interactionHeld;
        }

        private static void UpdateSemiSleepWindows()
        {
            using (SemiSleepUpdateProfilerMarker.Auto())
            {
                bool sampling = semiSleepPerformanceSampleActive;
                long startedAt = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
                long allocatedAt = sampling ? GC.GetAllocatedBytesForCurrentThread() : 0L;
                try
                {
                    UpdateSemiSleepWindowsCore();
                }
                finally
                {
                    if (sampling)
                    {
                        semiSleepPerformanceUpdateCount++;
                        long elapsedTicks =
                            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
                        semiSleepPerformanceUpdateElapsedTicks += elapsedTicks;
                        if (elapsedTicks > semiSleepPerformanceMaximumUpdateElapsedTicks)
                            semiSleepPerformanceMaximumUpdateElapsedTicks = elapsedTicks;
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedAt;
                        if (allocated > 0L)
                        {
                            semiSleepPerformanceUpdateAllocatedBytes += allocated;
                            if (allocated > semiSleepPerformanceMaximumAllocatedBytesPerUpdate)
                                semiSleepPerformanceMaximumAllocatedBytesPerUpdate = allocated;
                        }
                    }
                }
            }
        }

        private static void UpdateSemiSleepWindowsCore()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!semiSleepAnyAnimating && now < nextSemiSleepIdleCheckAt)
                return;

            bool hasAnimation = false;
            bool subscriptionStateChanged = false;
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (semiSleepPerformanceSampleActive)
                    semiSleepPerformanceBindingVisitCount++;
                if (binding == null || binding.lifecycleSuspended)
                    continue;
                SyncSleepOwnerState(binding);
                if (!ShouldEvaluateSemiSleepBinding(binding))
                    continue;
                // Native frame movement is exclusively owned by the pointer drag.
                // Skip every automatic state/geometry path until the gesture commits.
                if (binding != null
                    && (binding.semiSleepDragging || binding.semiSleepDragPointerId >= 0))
                {
                    ApplyPendingSemiSleepDragFrame(binding);
                    // Pointer movement owns the native frame and needs animation-rate
                    // updates even though it is not a visual-state tween.
                    hasAnimation = true;
                    continue;
                }
                if (binding.restorePersistedSleepOnBind)
                {
                    bool wasPending = binding.restorePersistedSleepOnBind;
                    TryRestorePersistedSemiSleepGeometry(binding);
                    subscriptionStateChanged = subscriptionStateChanged
                        || wasPending != binding.restorePersistedSleepOnBind;
                    if (binding.restorePersistedSleepOnBind)
                        continue;
                }
                if (!CanEnterSemiSleep(binding, false))
                {
                    if (HasSemiSleepStateToNormalize(binding))
                    {
                        RestoreSemiSleep(binding, true);
                        subscriptionStateChanged = true;
                    }
                    continue;
                }

                bool focused = ReferenceEquals(EditorWindow.focusedWindow, binding.window);
                if (focused)
                    lastFocusedWindowId = binding.window.GetInstanceID();
                bool interactionHeld = IsWindowInteractionHeld(binding, now);
                if (focused || interactionHeld)
                {
                    binding.focusLostAt = -1d;
                }
                else if (IsSemiSleepEligible(binding)
                    && binding.visualState == ESWindowVisualState.ActivePanel
                    && !binding.semiSleepAnimating)
                {
                    binding.awakeBounds = binding.window.position;
                    if (binding.focusLostAt < 0d)
                        binding.focusLostAt = now;
                    else if (focusModeWindowId != 0 || now - binding.focusLostAt >= SemiSleepDelay)
                    {
                        if (focusModeWindowId != 0)
                            binding.focusModeForcedSleep = true;
                        binding.semiSleepManualHold = false;
                        BeginSemiSleepTransition(binding, true);
                    }
                }

                if (!binding.semiSleepAnimating)
                {
                    bool pointerOver = binding.pointerInside
                        || ReferenceEquals(EditorWindow.mouseOverWindow, binding.window);
                    // Tile-to-tab promotion is blocked only by an explicit
                    // interaction lease, drag, or busy operation. Focus history,
                    // transient null focus, and ordinary hover must not reset the
                    // idle timer; those values differ between EditorWindow types.
                    bool tileInteractionHeld = binding.interactionHoldCount > 0
                        || binding.semiSleepDragPointerId >= 0
                        || binding.busyCount > 0;
                    UpdateVisualStateIdle(
                        binding,
                        now,
                        // Focus and mere pointer hover are not interaction leases.
                        // A SleepTile must still promote to EdgeTab while the
                        // cursor rests over it; only an actual hold/drag/busy lease
                        // pauses that timer.
                        ShouldPauseSleepTilePromotion(tileInteractionHeld, pointerOver),
                        pointerOver);
                    RefreshSemiSleepDiagnosticBars(binding, now);
                }

                if (binding.semiSleepAnimating)
                {
                    UpdateSemiSleepTransition(binding, now);
                    hasAnimation = hasAnimation || binding.semiSleepAnimating;
                }
                else
                {
                    RepairSettledSemiSleepGeometry(binding);
                }
                if (binding.persistedSleepGeometryVerifyUntil >= 0d
                    && now >= binding.persistedSleepGeometryVerifyUntil)
                {
                    // Keep the final repair in this tick, then return a settled tab to
                    // its event-driven path instead of retaining a permanent update loop.
                    binding.persistedSleepGeometryVerifyUntil = -1d;
                    subscriptionStateChanged = true;
                }
            }

            semiSleepAnyAnimating = hasAnimation;
            nextSemiSleepIdleCheckAt = now + (hasAnimation ? 0.016d : 0.10d);
            if (subscriptionStateChanged)
                RefreshSemiSleepUpdateSubscription();
        }

        private static bool HasSemiSleepStateToNormalize(WindowBinding binding)
        {
            return binding != null
                && (binding.restorePersistedSleepOnBind
                    || binding.restorePersistedSleepScheduled
                    || binding.persistedSleepGeometryVerifyUntil >= 0d
                    || binding.persistedSleepGeometryRepairScheduled
                    || binding.semiSleeping
                    || binding.semiSleepAnimating
                    || binding.semiSleepTarget
                    || binding.visualState != ESWindowVisualState.ActivePanel
                    || binding.transitionTargetState != ESWindowVisualState.ActivePanel
                    || binding.semiSleepManualHold
                    || binding.semiSleepSlot >= 0
                    || binding.focusLostAt >= 0d
                    || binding.sleepTileIdleStartedAt >= 0d
                    || binding.edgeTabFullyExpandedAt >= 0d
                    || binding.edgeTabHoverIntentStartedAt >= 0d
                    || binding.edgeTabHoverExitGraceUntil >= 0d
                    || binding.pointerInside
                    || binding.hasEdgeTabPointerPosition
                    || binding.semiSleepDragging
                    || binding.semiSleepDragPointerId >= 0
                    || binding.hasSemiSleepDragPendingBounds
                    || binding.semiSleepRecaptureScheduled
                    || binding.semiSleepDragStartState != ESWindowVisualState.ActivePanel);
        }

        private static void SyncSleepOwnerState(WindowBinding child)
        {
            if (child == null
                || child.sleepLinkMode != ES.ESWindowSleepLinkMode.FollowOwner)
                return;
            // A child being dragged has temporary ownership of its native frame;
            // owner synchronization must wait for PointerUp/PointerCancel.
            if (child.semiSleepDragging || child.semiSleepDragPointerId >= 0)
                return;
            EditorWindow owner = child.sleepOwner;
            if (owner == null)
            {
                child.sleepOwner = null;
                child.sleepOwnerForcedSleep = false;
                child.sleepLinkMode = ES.ESWindowSleepLinkMode.Independent;
                return;
            }
            if (!windowBindings.TryGetValue(owner.GetInstanceID(), out WindowBinding ownerBinding)
                || ownerBinding == null)
            {
                // Owner binding is established by its own window lifecycle or the
                // explicit SetWindowSleepOwner path. Mutating windowBindings while
                // UpdateSemiSleepWindowsCore enumerates it would invalidate the pass.
                return;
            }

            bool ownerSleeping = ownerBinding.semiSleeping
                || ownerBinding.semiSleepAnimating && ownerBinding.semiSleepTarget;
            bool childSleeping = child.semiSleeping
                || child.semiSleepAnimating && child.semiSleepTarget;
            if (ownerSleeping
                && !childSleeping
                && !CanEnterSemiSleep(child, false))
            {
                // 子窗口可能正处于 Busy、拖动或输入交互；保留未同步状态，
                // 下一次 tick 在交互结束后继续尝试，而不是吞掉跟随请求。
                child.sleepOwnerForcedSleep = false;
                return;
            }
            if (ownerSleeping == child.sleepOwnerForcedSleep
                && (ownerSleeping == childSleeping || !ownerSleeping))
                return;

            child.sleepOwnerForcedSleep = ownerSleeping;
            child.sleepLinkSyncing = true;
            try
            {
                if (ownerSleeping)
                {
                    if (!childSleeping && CanEnterSemiSleep(child, false))
                    {
                        child.semiSleepManualHold = true;
                        BeginSemiSleepTransition(child, true);
                    }
                }
                else if (childSleeping)
                {
                    child.semiSleepManualHold = false;
                    BeginSemiSleepTransition(child, false);
                }
            }
            finally
            {
                child.sleepLinkSyncing = false;
            }
        }

        private static void RepairSettledSemiSleepGeometry(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !binding.semiSleeping
                || binding.semiSleepAnimating
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0)
                return;
            Rect expected;
            if (binding.visualState == ESWindowVisualState.SleepTile)
            {
                Rect tray = GetSemiSleepTrayBounds(binding.awakeBounds);
                expected = binding.semiSleepDockBounds.width > 1f
                    && binding.semiSleepDockBounds.height > 1f
                    ? ClampSemiSleepDockBounds(binding.semiSleepDockBounds, tray)
                    : EvaluateSemiSleepTarget(tray, binding.semiSleepSlot);
            }
            else if (binding.visualState == ESWindowVisualState.EdgeTab
                || binding.visualState == ESWindowVisualState.EdgeTabHover)
            {
                expected = EvaluateEdgeTabBounds(
                    GetSemiSleepTrayBounds(binding.awakeBounds),
                    binding.edge,
                    binding.edgeOffset,
                    binding.visualState == ESWindowVisualState.EdgeTabHover ? 1f : 0f);
            }
            else
                return;
            Rect current = binding.window.position;
            if (TryNormalizeUnexpectedAwakeGeometry(binding, current, expected))
                return;
            if (Mathf.Abs(current.x - expected.x) > 1f
                || Mathf.Abs(current.y - expected.y) > 1f
                || Mathf.Abs(current.width - expected.width) > 1f
                || Mathf.Abs(current.height - expected.height) > 1f)
            {
                CommitSemiSleepWindowPosition(binding, expected);
                binding.semiSleepToBounds = expected;
            }
        }

        private static bool TryNormalizeUnexpectedAwakeGeometry(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !binding.semiSleeping
                || binding.semiSleepAnimating
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0)
                return false;

            Rect expected;
            Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
            if (binding.visualState == ESWindowVisualState.SleepTile)
            {
                expected = binding.hasSemiSleepDockBounds
                    ? ClampSemiSleepDockBounds(binding.semiSleepDockBounds, workArea)
                    : EvaluateSemiSleepTarget(workArea, binding.semiSleepSlot);
            }
            else if (binding.visualState == ESWindowVisualState.EdgeTab
                || binding.visualState == ESWindowVisualState.EdgeTabHover)
            {
                expected = EvaluateEdgeTabBounds(
                    workArea,
                    binding.edge,
                    binding.edgeOffset,
                    binding.visualState == ESWindowVisualState.EdgeTabHover ? 1f : 0f);
            }
            else
            {
                return false;
            }
            return TryNormalizeUnexpectedAwakeGeometry(
                binding,
                binding.window.position,
                expected);
        }

        private static bool TryNormalizeUnexpectedAwakeGeometry(
            WindowBinding binding,
            Rect actual,
            Rect expected)
        {
            if (binding == null
                || binding.persistedSleepGeometryVerifyUntil >= EditorApplication.timeSinceStartup
                || !IsClearlyAwakeGeometry(actual, expected, binding.awakeBounds))
                return false;

            binding.semiSleeping = false;
            binding.semiSleepTarget = false;
            binding.semiSleepAnimating = false;
            binding.restorePersistedSleepOnBind = false;
            binding.restorePersistedSleepScheduled = false;
            binding.persistedSleepGeometryVerifyUntil = -1d;
            binding.persistedSleepGeometryRepairScheduled = false;
            binding.visualState = ESWindowVisualState.ActivePanel;
            binding.transitionTargetState = ESWindowVisualState.ActivePanel;
            binding.semiSleepManualHold = false;
            binding.semiSleepSlot = -1;
            binding.focusLostAt = -1d;
            binding.sleepTileIdleStartedAt = -1d;
            binding.edgeTabFullyExpandedAt = -1d;
            binding.awakeBounds = actual;
            binding.window.minSize = binding.awakeMinSize;
            binding.window.maxSize = binding.awakeMaxSize;
            ShowSemiSleepOverlay(binding, false, 0f);
            SaveSemiSleepPreferences(binding);
            return true;
        }

        internal static bool IsClearlyAwakeGeometry(
            Rect actual,
            Rect expectedSleep,
            Rect awakeBounds)
        {
            bool substantiallyLarger = actual.width >= Mathf.Max(320f, expectedSleep.width + 160f)
                && actual.height >= Mathf.Max(220f, expectedSleep.height + 100f);
            if (!substantiallyLarger)
                return false;
            if (awakeBounds.width <= 1f || awakeBounds.height <= 1f)
                return true;
            float widthTolerance = Mathf.Max(32f, awakeBounds.width * 0.20f);
            float heightTolerance = Mathf.Max(32f, awakeBounds.height * 0.20f);
            return Mathf.Abs(actual.width - awakeBounds.width) <= widthTolerance
                && Mathf.Abs(actual.height - awakeBounds.height) <= heightTolerance;
        }

        private static void RestorePersistedSemiSleepGeometry(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !binding.restorePersistedSleepOnBind)
                return;

            binding.restorePersistedSleepOnBind = false;
            binding.restorePersistedSleepScheduled = false;
            if (binding.awakeBounds.width <= 1f || binding.awakeBounds.height <= 1f)
                binding.awakeBounds = binding.window.position;
            // Unity restores the original frame constraints before CreateGUI. Capture
            // them before replacing minSize for the sleep form, otherwise waking after
            // a domain reload would restore zero/default constraints.
            if (binding.awakeMinSize.x <= 0f || binding.awakeMinSize.y <= 0f)
                binding.awakeMinSize = binding.window.minSize;
            if (binding.awakeMaxSize.x <= 0f || binding.awakeMaxSize.y <= 0f)
                binding.awakeMaxSize = binding.window.maxSize;

            ESWindowVisualState state = binding.transitionTargetState == ESWindowVisualState.EdgeTab
                ? ESWindowVisualState.EdgeTab
                : ESWindowVisualState.SleepTile;
            Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
            Rect target;
            if (state == ESWindowVisualState.EdgeTab)
            {
                target = EvaluateEdgeTabBounds(workArea, binding.edge, binding.edgeOffset, 0f);
                binding.window.minSize = new Vector2(32f, 32f);
            }
            else
            {
                if (binding.semiSleepSlot < 0)
                    binding.semiSleepSlot = AcquireSemiSleepSlot(binding);
                target = binding.hasSemiSleepDockBounds
                    ? ClampSemiSleepDockBounds(binding.semiSleepDockBounds, workArea)
                    : EvaluateSemiSleepTarget(workArea, binding.semiSleepSlot);
                binding.window.minSize = new Vector2(80f, 80f);
            }

            binding.semiSleepFromBounds = target;
            binding.semiSleepToBounds = target;
            CommitSemiSleepWindowPosition(binding, target);
            binding.visualState = state;
            binding.transitionTargetState = state;
            binding.semiSleeping = true;
            binding.semiSleepTarget = true;
            binding.semiSleepAnimating = false;
            binding.semiSleepManualHold = true;
            binding.sleepTileIdleStartedAt = state == ESWindowVisualState.SleepTile
                ? EditorApplication.timeSinceStartup
                : -1d;
            binding.edgeTabFullyExpandedAt = -1d;
            binding.persistedSleepGeometryVerifyUntil =
                EditorApplication.timeSinceStartup + PersistedSleepGeometryVerificationDuration;
            ShowSemiSleepOverlay(binding, true, 1f);
            ApplySemiSleepOverlayState(binding, state);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            // Persist the normalized state (not EdgeTabHover and not an in-flight
            // transition) so a subsequent domain reload has one deterministic target.
            SaveSemiSleepPreferences(binding);
        }

        internal static bool IsVisualStateGeometryConsistent(
            ESWindowVisualState state,
            Rect actual,
            Rect expected)
        {
            if (state == ESWindowVisualState.ActivePanel)
                return true;
            return Mathf.Abs(actual.x - expected.x) <= 1f
                && Mathf.Abs(actual.y - expected.y) <= 1f
                && Mathf.Abs(actual.width - expected.width) <= 1f
                && Mathf.Abs(actual.height - expected.height) <= 1f;
        }

        private static bool HasSettledSemiSleepGeometryMismatch(WindowBinding binding)
        {
            if (binding?.window == null
                || !binding.semiSleeping
                || binding.semiSleepAnimating
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0)
                return false;

            Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
            Rect expected;
            if (binding.visualState == ESWindowVisualState.SleepTile)
            {
                expected = binding.semiSleepDockBounds.width > 1f
                    && binding.semiSleepDockBounds.height > 1f
                        ? ClampSemiSleepDockBounds(binding.semiSleepDockBounds, workArea)
                        : EvaluateSemiSleepTarget(workArea, binding.semiSleepSlot);
            }
            else if (binding.visualState == ESWindowVisualState.EdgeTab
                || binding.visualState == ESWindowVisualState.EdgeTabHover)
            {
                expected = EvaluateEdgeTabBounds(
                    workArea,
                    binding.edge,
                    binding.edgeOffset,
                    binding.visualState == ESWindowVisualState.EdgeTabHover ? 1f : 0f);
            }
            else
            {
                return true;
            }

            return !IsVisualStateGeometryConsistent(
                binding.visualState,
                binding.window.position,
                expected);
        }

        private static void UpdateVisualStateIdle(
            WindowBinding binding,
            double now,
            bool collectionHeld,
            bool pointerOver)
        {
            if (binding == null || binding.window == null)
                return;
            switch (binding.visualState)
            {
                case ESWindowVisualState.SleepTile:
                    if (collectionHeld)
                    {
                        binding.sleepTileIdleStartedAt = now;
                        return;
                    }
                    if (binding.sleepTileIdleStartedAt < 0d)
                        binding.sleepTileIdleStartedAt = now;
                    if (now - binding.sleepTileIdleStartedAt < SleepTileToEdgeTabDelay)
                        return;
                    Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
                    Rect currentTileBounds = binding.window.position;
                    Rect savedTileBounds = binding.hasSemiSleepDockBounds
                        ? ClampSemiSleepDockBounds(binding.semiSleepDockBounds, workArea)
                        : currentTileBounds;
                    Rect tileBounds = SelectMoreEdgeAlignedTileBounds(
                        currentTileBounds,
                        savedTileBounds,
                        workArea);
                    if (!TryEvaluateEdgeTab(
                            tileBounds,
                            workArea,
                            out ESWindowEdge edge,
                            out float edgeOffset,
                            out Rect tabBounds))
                        return;
                    binding.edge = edge;
                    binding.edgeOffset = edgeOffset;
                    BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTab, tabBounds);
                    break;

                case ESWindowVisualState.EdgeTab:
                    if (!binding.pointerInside)
                    {
                        binding.edgeTabHoverIntentStartedAt = -1d;
                    }
                    else
                    {
                        if (binding.edgeTabHoverIntentStartedAt < 0d)
                            binding.edgeTabHoverIntentStartedAt = now;
                    }
                    if (ShouldBeginEdgeTabHover(
                        binding.edgeTabHoverIntentStartedAt,
                        now,
                        binding.pointerInside))
                    {
                        binding.edgeTabHoverIntentStartedAt = -1d;
                        BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTabHover);
                    }
                    break;

                case ESWindowVisualState.EdgeTabHover:
                    if (!pointerOver
                        && now >= binding.edgeTabHoverExitGraceUntil)
                    {
                        binding.edgeTabFullyExpandedAt = -1d;
                        BeginVisualStateTransition(binding, ESWindowVisualState.EdgeTab);
                    }
                    else if (ShouldRestoreEdgeTabToTile(
                        binding.edgeTabFullyExpandedAt,
                        now,
                        pointerOver))
                    {
                        Rect hoverTileBounds = EvaluateSleepTileFromEdgeTab(
                            binding.window.position,
                            GetSemiSleepTrayBounds(binding.awakeBounds),
                            binding.edge);
                        binding.semiSleepDockBounds = hoverTileBounds;
                        binding.hasSemiSleepDockBounds = true;
                        BeginVisualStateTransition(binding, ESWindowVisualState.SleepTile, hoverTileBounds);
                    }
                    break;
            }
        }

        private static void BeginSemiSleepTransition(WindowBinding binding, bool sleep)
        {
            ESWindowVisualState target = sleep
                ? ESWindowVisualState.SleepTile
                : ESWindowVisualState.ActivePanel;
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0
                || binding.transitionTargetState == target && binding.semiSleepAnimating)
                return;
            if (!sleep
                && binding.visualState == ESWindowVisualState.ActivePanel
                && !binding.semiSleepAnimating)
                return;

            if (sleep && binding.visualState == ESWindowVisualState.ActivePanel)
            {
                if (binding.awakeBounds.width <= 1f || binding.awakeBounds.height <= 1f)
                    binding.awakeBounds = binding.window.position;
                binding.awakeMinSize = binding.window.minSize;
                binding.awakeMaxSize = binding.window.maxSize;
                binding.window.minSize = new Vector2(80f, 80f);
                binding.semiSleepSlot = AcquireSemiSleepSlot(binding);
            }

            binding.transitionTargetState = target;
            binding.semiSleepTarget = sleep;
            binding.semiSleepAnimating = MotionEnabled;
            semiSleepAnyAnimating = semiSleepAnyAnimating || binding.semiSleepAnimating;
            binding.semiSleepStartedAt = EditorApplication.timeSinceStartup;
            binding.semiSleepTransitionDuration = SemiSleepDuration;
            binding.semiSleepFromBounds = binding.window.position;
            binding.semiSleepToBounds = sleep
                ? binding.hasSemiSleepDockBounds
                    ? ClampSemiSleepDockBounds(
                        binding.semiSleepDockBounds,
                        GetSemiSleepTrayBounds(binding.awakeBounds))
                    : EvaluateSemiSleepTarget(
                        GetSemiSleepTrayBounds(binding.awakeBounds),
                        binding.semiSleepSlot)
                    : binding.awakeBounds;
            ApplySemiSleepOverlayState(
                binding,
                sleep ? ESWindowVisualState.SleepTile : ESWindowVisualState.ActivePanel);
            // The ES overlay owns the visual during the whole geometry transition.
            // Leaving the original content visible while the native frame is shrinking
            // creates the false state “small tile on a large window”.
            ShowSemiSleepOverlay(binding, true, 1f);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            if (!binding.semiSleepAnimating)
                CompleteSemiSleepTransition(binding);
        }

        private static void BeginVisualStateTransition(
            WindowBinding binding,
            ESWindowVisualState target,
            Rect? explicitBounds = null)
        {
            if (binding?.window == null
                || binding.semiSleepDragging
                || binding.semiSleepDragPointerId >= 0
                || target == ESWindowVisualState.ActivePanel)
                return;
            // A visual transition is single-flight. While EdgeTabHover is
            // converting to SleepTile, repeated hover/update ticks must not
            // rebuild the target from the moving native frame and restart the
            // animation at a new coordinate.
            if (binding.semiSleepAnimating
                && binding.transitionTargetState == target)
                return;
            if (binding.visualState == target && !binding.semiSleepAnimating)
                return;
            Rect workArea = GetSemiSleepTrayBounds(binding.awakeBounds);
            Rect transitionFrom = binding.window.position;
            if ((binding.visualState == ESWindowVisualState.EdgeTab
                    || binding.visualState == ESWindowVisualState.EdgeTabHover)
                && binding.semiSleepToBounds.width > 1f
                && binding.semiSleepToBounds.height > 1f)
            {
                // The last completed tab target is more stable than the native
                // frame during a UI Toolkit/native-window handoff.
                transitionFrom = binding.semiSleepToBounds;
            }
            Rect targetBounds = explicitBounds ?? (target == ESWindowVisualState.EdgeTabHover
                ? EvaluateEdgeTabBounds(workArea, binding.edge, binding.edgeOffset, 1f)
                : target == ESWindowVisualState.EdgeTab
                    ? EvaluateEdgeTabBounds(workArea, binding.edge, binding.edgeOffset, 0f)
                    : EvaluateSleepTileFromEdgeTab(transitionFrom, workArea, binding.edge));
            binding.transitionTargetState = target;
            if (target == ESWindowVisualState.SleepTile)
            {
                binding.edgeTabFullyExpandedAt = -1d;
                binding.hasEdgeTabPointerPosition = false;
            }
            if (target == ESWindowVisualState.EdgeTab
                || target == ESWindowVisualState.EdgeTabHover)
                binding.window.minSize = new Vector2(32f, 32f);
            else if (target == ESWindowVisualState.SleepTile)
                binding.window.minSize = new Vector2(80f, 80f);
            binding.semiSleepTarget = true;
            binding.semiSleepFromBounds = transitionFrom;
            binding.semiSleepToBounds = targetBounds;
            binding.semiSleepStartedAt = EditorApplication.timeSinceStartup;
            float fullDistance = EdgeTabExpandedLength - EdgeTabCollapsedLength;
            float fullDuration = target == ESWindowVisualState.EdgeTabHover
                ? EdgeTabHoverDuration
                : target == ESWindowVisualState.SleepTile
                    ? EdgeTabToTileDuration
                    : EdgeTabTransitionDuration;
            binding.semiSleepTransitionDuration = target == ESWindowVisualState.EdgeTab
                || target == ESWindowVisualState.EdgeTabHover
                    ? EvaluateEdgeTabTransitionDuration(
                        binding.semiSleepFromBounds,
                        targetBounds,
                        fullDistance,
                        fullDuration)
                    : fullDuration;
            binding.semiSleepAnimating = MotionEnabled;
            semiSleepAnyAnimating = semiSleepAnyAnimating || binding.semiSleepAnimating;
            ShowSemiSleepOverlay(binding, true, 1f);
            ApplySemiSleepOverlayState(binding, target);
            RefreshSemiSleepUpdateSubscription();
            if (!binding.semiSleepAnimating)
                CompleteSemiSleepTransition(binding);
        }

        private static void UpdateSemiSleepTransition(WindowBinding binding, double now)
        {
            if (binding == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            double duration = Math.Max(0.01d, binding.semiSleepTransitionDuration);
            float progress = Mathf.Clamp01((float)((now - binding.semiSleepStartedAt) / duration));
            try
            {
                bool edgeTransition = binding.transitionTargetState == ESWindowVisualState.EdgeTab
                    || binding.transitionTargetState == ESWindowVisualState.EdgeTabHover
                    || binding.transitionTargetState == ESWindowVisualState.SleepTile
                    && (binding.visualState == ESWindowVisualState.EdgeTab
                        || binding.visualState == ESWindowVisualState.EdgeTabHover);
                bool edgeToTile = binding.transitionTargetState == ESWindowVisualState.SleepTile
                    && (binding.visualState == ESWindowVisualState.EdgeTab
                        || binding.visualState == ESWindowVisualState.EdgeTabHover);
                bool edgeToActive = binding.transitionTargetState == ESWindowVisualState.ActivePanel
                    && (binding.visualState == ESWindowVisualState.EdgeTab
                        || binding.visualState == ESWindowVisualState.EdgeTabHover);
                CommitSemiSleepWindowPosition(binding, edgeToTile
                    ? EvaluateEdgeTabToTileFrame(
                        binding.semiSleepFromBounds,
                        binding.semiSleepToBounds,
                        progress,
                        binding.edge)
                    : edgeToActive
                    ? EvaluateEdgeTabTransitionFrame(
                        binding.semiSleepFromBounds,
                        binding.semiSleepToBounds,
                        progress,
                        binding.edge)
                    : edgeTransition
                    ? EvaluateEdgeTabTransitionFrame(
                        binding.semiSleepFromBounds,
                        binding.semiSleepToBounds,
                        progress,
                        binding.edge)
                    : EvaluateSemiSleepFrame(
                        binding.semiSleepFromBounds,
                        binding.semiSleepToBounds,
                        progress,
                        binding.transitionTargetState == ESWindowVisualState.ActivePanel,
                        MotionIntensity));
                float overlayOpacity;
                overlayOpacity = binding.transitionTargetState == ESWindowVisualState.ActivePanel
                    ? 1f - Mathf.Clamp01(progress / 0.55f)
                    : 1f;
                ShowSemiSleepOverlay(binding, true, overlayOpacity);
                RequestSemiSleepRepaint(binding);
                if (progress >= 1f)
                    CompleteSemiSleepTransition(binding);
            }
            catch (Exception exception) when (
                exception is MissingReferenceException
                || exception is NullReferenceException
                || exception is InvalidOperationException)
            {
                RestoreSemiSleep(binding, false);
            }
        }

        private static void CompleteSemiSleepTransition(WindowBinding binding)
        {
            if (binding?.window == null
                || binding.lifecycleSuspended
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            CommitSemiSleepWindowPosition(binding, binding.semiSleepToBounds);
            binding.visualState = binding.transitionTargetState;
            binding.semiSleeping = binding.visualState != ESWindowVisualState.ActivePanel;
            binding.semiSleepTarget = binding.semiSleeping;
            binding.semiSleepAnimating = false;
            if (binding.semiSleeping)
            {
                ShowSemiSleepOverlay(binding, true, 1f);
                ApplySemiSleepOverlayState(binding);
                if (binding.visualState == ESWindowVisualState.SleepTile)
                {
                    binding.semiSleepDockBounds = binding.semiSleepToBounds;
                    binding.hasSemiSleepDockBounds = true;
                    binding.sleepTileIdleStartedAt = EditorApplication.timeSinceStartup;
                    binding.edgeTabFullyExpandedAt = -1d;
                }
                else if (binding.visualState == ESWindowVisualState.EdgeTabHover)
                {
                    binding.edgeTabFullyExpandedAt = EditorApplication.timeSinceStartup;
                }
                SaveSemiSleepPreferences(binding);
            }
            else
            {
                binding.semiSleepSlot = -1;
                binding.window.minSize = binding.awakeMinSize;
                binding.window.maxSize = binding.awakeMaxSize;
                ShowSemiSleepOverlay(binding, false, 0f);
                ESWindowOpeningSweep.Replay(binding.root);
                BeginWindowPulse(binding, ESStatusKind.Ready);
                SaveSemiSleepPreferences(binding);
            }
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        private static Rect EvaluateSleepTileFromEdgeTab(
            Rect tabBounds,
            Rect workArea,
            ESWindowEdge edge)
        {
            // During EdgeTabHover the native frame is the authoritative visual
            // anchor. Keep its center, then expand to the 100x100 tile on the same
            // screen edge; never regenerate a tab from the persisted offset here.
            if (tabBounds.width <= 1f || tabBounds.height <= 1f)
                tabBounds = EvaluateEdgeTabBounds(workArea, edge, 0f, 1f);
            float x;
            float y;
            switch (edge)
            {
                case ESWindowEdge.Left:
                    x = workArea.xMin;
                    y = tabBounds.center.y - SemiSleepSize * 0.5f;
                    break;
                case ESWindowEdge.Right:
                    x = workArea.xMax - SemiSleepSize;
                    y = tabBounds.center.y - SemiSleepSize * 0.5f;
                    break;
                case ESWindowEdge.Top:
                    x = tabBounds.center.x - SemiSleepSize * 0.5f;
                    y = workArea.yMin;
                    break;
                default:
                    x = tabBounds.center.x - SemiSleepSize * 0.5f;
                    y = workArea.yMax - SemiSleepSize;
                    break;
            }
            return ClampSemiSleepDockBounds(
                new Rect(x, y, SemiSleepSize, SemiSleepSize),
                workArea);
        }

        private static void ApplySemiSleepOverlayState(
            WindowBinding binding,
            ESWindowVisualState? stateOverride = null)
        {
            if (binding?.semiSleepOverlay == null)
                return;
            RefreshSemiSleepOverlayMetadata(binding);
            ESWindowVisualState state = stateOverride ?? binding.visualState;
            bool tile = state == ESWindowVisualState.SleepTile;
            bool verticalEdgeTab = !tile
                && (binding.edge == ESWindowEdge.Top || binding.edge == ESWindowEdge.Bottom);
            bool expandedTab = state == ESWindowVisualState.EdgeTabHover;
            binding.semiSleepOverlay.style.flexDirection = tile || verticalEdgeTab
                ? FlexDirection.Column
                : FlexDirection.Row;
            if (binding.semiSleepIcon != null)
            {
                float iconSize = tile ? 22f : 18f;
                binding.semiSleepIcon.style.width = iconSize;
                binding.semiSleepIcon.style.height = iconSize;
                binding.semiSleepIcon.style.marginRight = !tile && !verticalEdgeTab ? 5f : 0f;
                binding.semiSleepIcon.style.marginBottom = verticalEdgeTab ? 2f : 0f;
            }
            if (binding.semiSleepMonogram != null)
            {
                binding.semiSleepMonogram.style.fontSize = tile ? 20f : 13f;
                binding.semiSleepMonogram.style.marginRight = !tile && !verticalEdgeTab ? 5f : 0f;
                binding.semiSleepMonogram.style.marginBottom = verticalEdgeTab ? 2f : 0f;
            }
            if (binding.semiSleepTitleLabel != null)
            {
                string currentTitle = ResolveWindowPresentationTitle(binding.window);
                string shortTitle = GetWindowPresentationShortTitle(binding.window);
                binding.semiSleepTitleLabel.text = tile || expandedTab
                    ? currentTitle
                    : shortTitle;
                binding.semiSleepTitleLabel.tooltip = currentTitle;
                binding.semiSleepTitleLabel.style.display = DisplayStyle.Flex;
                binding.semiSleepTitleLabel.style.marginTop = tile || verticalEdgeTab ? 3f : 0f;
                binding.semiSleepTitleLabel.style.whiteSpace = verticalEdgeTab
                    ? WhiteSpace.Normal
                    : WhiteSpace.NoWrap;
                binding.semiSleepTitleLabel.style.textOverflow = verticalEdgeTab
                    ? TextOverflow.Clip
                    : TextOverflow.Ellipsis;
                binding.semiSleepTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                binding.semiSleepTitleLabel.style.maxWidth = tile
                    ? 82f
                    : verticalEdgeTab
                        ? 36f
                        : expandedTab
                        ? 164f
                        : 30f;
                binding.semiSleepTitleLabel.style.maxHeight = tile
                    ? 18f
                    : verticalEdgeTab
                        ? expandedTab ? 156f : 30f
                        : 18f;
            }
            RefreshSemiSleepDiagnosticBars(binding, EditorApplication.timeSinceStartup);
        }

        private static void RefreshSemiSleepDiagnosticBars(WindowBinding binding, double now)
        {
            if (binding?.semiSleepPromotionProgress == null
                || binding.semiSleepDockProgress == null)
                return;
            bool tile = binding.visualState == ESWindowVisualState.SleepTile
                || binding.semiSleepAnimating
                && binding.transitionTargetState == ESWindowVisualState.SleepTile;
            if (!tile)
            {
                if (!binding.diagnosticBarsHidden)
                {
                    binding.semiSleepPromotionProgress.style.width = 0f;
                    binding.semiSleepDockProgress.style.width = 0f;
                    binding.semiSleepPromotionProgress.style.display = DisplayStyle.None;
                    binding.semiSleepDockProgress.style.display = DisplayStyle.None;
                    binding.diagnosticBarsHidden = true;
                    binding.diagnosticPromotionProgress = -1f;
                    binding.diagnosticPromotionComplete = false;
                }
                return;
            }

            // 只保留一条底部晋级进度条。停靠判定继续参与状态机，但不再以第二条
            // 诊断色带或 tooltip 占用休眠块的视觉注意力。
            if (binding.diagnosticBarsHidden)
            {
                binding.semiSleepPromotionProgress.style.display = DisplayStyle.Flex;
                binding.semiSleepDockProgress.style.display = DisplayStyle.None;
                binding.diagnosticBarsHidden = false;
            }

            double started = binding.sleepTileIdleStartedAt;
            float progress = started < 0d
                ? 0f
                : Mathf.Clamp01((float)((now - started) / SleepTileToEdgeTabDelay));
            bool complete = progress >= 1f;
            if (Mathf.Abs(progress - binding.diagnosticPromotionProgress) >= 0.001f
                || complete != binding.diagnosticPromotionComplete)
            {
                binding.semiSleepPromotionProgress.style.width = 82f * progress;
                binding.semiSleepPromotionProgress.style.backgroundColor = complete
                    ? GetStatusAccent(0, ESStatusKind.Ready)
                    : SectionMutedTextColor;
                binding.diagnosticPromotionProgress = progress;
                binding.diagnosticPromotionComplete = complete;
            }
        }

        private static Rect SelectMoreEdgeAlignedTileBounds(
            Rect current,
            Rect saved,
            Rect workArea)
        {
            if (!IsFinite(current.position) || !IsFinite(current.size))
                return saved;
            if (!IsFinite(saved.position) || !IsFinite(saved.size))
                return current;
            float currentNearest = GetNearestEdgeDistance(current, workArea);
            float savedNearest = GetNearestEdgeDistance(saved, workArea);
            return currentNearest <= savedNearest ? current : saved;
        }

        private static float GetNearestEdgeDistance(Rect bounds, Rect workArea)
        {
            return Mathf.Min(
                Mathf.Abs(bounds.xMin - workArea.xMin),
                Mathf.Abs(workArea.xMax - bounds.xMax),
                Mathf.Abs(bounds.yMin - workArea.yMin),
                Mathf.Abs(workArea.yMax - bounds.yMax));
        }

        private static void RefreshSemiSleepOverlayMetadata(WindowBinding binding)
        {
            if (binding?.semiSleepOverlay == null)
                return;
            Texture icon = ResolveWindowPresentationIcon(binding.window);
            if (icon != null)
            {
                if (binding.semiSleepIcon == null)
                {
                    binding.semiSleepIcon = new Image
                    {
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore,
                        tintColor = SelectedTextColor
                    };
                    binding.semiSleepIcon.style.width = 22f;
                    binding.semiSleepIcon.style.height = 22f;
                    binding.semiSleepOverlay.Insert(0, binding.semiSleepIcon);
                }
                binding.semiSleepIcon.image = icon;
                binding.semiSleepIcon.tintColor = SelectedTextColor;
                binding.semiSleepIcon.style.display = DisplayStyle.Flex;
                binding.semiSleepMonogram?.RemoveFromHierarchy();
            }
            else if (binding.semiSleepIcon != null)
            {
                binding.semiSleepIcon.style.display = DisplayStyle.None;
            }
            if (binding.semiSleepTitleLabel != null)
            {
                string title = ResolveWindowPresentationTitle(binding.window);
                bool tile = binding.visualState == ESWindowVisualState.SleepTile;
                bool expandedTab = binding.visualState == ESWindowVisualState.EdgeTabHover;
                binding.semiSleepTitleLabel.text = tile || expandedTab
                    ? title
                    : GetWindowPresentationShortTitle(binding.window);
                binding.semiSleepTitleLabel.tooltip = title;
            }
        }

        private static void ShowSemiSleepOverlay(
            WindowBinding binding,
            bool visible,
            float opacity)
        {
            if (binding?.semiSleepOverlay == null)
                return;
            binding.semiSleepOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            binding.semiSleepOverlay.style.opacity = Mathf.Clamp01(opacity);
            if (visible)
                binding.semiSleepOverlay.BringToFront();
        }

        private static void RestoreSemiSleep(
            WindowBinding binding,
            bool restoreBounds,
            bool forceLifecycleReset = false)
        {
            if (binding == null)
                return;
            // A lifecycle suspension is not a user wake request. Public status,
            // busy, pinning, owner, and focus helpers can still be notified while
            // Unity is entering PlayMode or rebuilding a panel; none of them may
            // expand the native frame behind the user's back. Explicit close and
            // teardown paths pass forceLifecycleReset=true and remain authoritative.
            if (binding.lifecycleSuspended && !forceLifecycleReset)
                return;
            // A cancelled/closing lifecycle must not interrupt an active pointer
            // gesture and restore the pre-drag awake rectangle mid-frame.
            if (!forceLifecycleReset
                && (binding.semiSleepDragging || binding.semiSleepDragPointerId >= 0))
                return;
            bool hadState = binding.semiSleeping || binding.semiSleepAnimating;
            VisualElement dragOverlay = binding.semiSleepOverlay;
            int dragPointerId = binding.semiSleepDragPointerId;
            if (dragOverlay != null
                && dragPointerId >= 0
                && dragOverlay.HasPointerCapture(dragPointerId))
                dragOverlay.ReleasePointer(dragPointerId);
            binding.semiSleeping = false;
            binding.semiSleepAnimating = false;
            binding.semiSleepTarget = false;
            binding.restorePersistedSleepOnBind = false;
            binding.restorePersistedSleepScheduled = false;
            binding.persistedSleepGeometryVerifyUntil = -1d;
            binding.persistedSleepGeometryRepairScheduled = false;
            binding.visualState = ESWindowVisualState.ActivePanel;
            binding.transitionTargetState = ESWindowVisualState.ActivePanel;
            binding.semiSleepManualHold = false;
            binding.semiSleepSlot = -1;
            binding.focusLostAt = -1d;
            binding.sleepTileIdleStartedAt = -1d;
            binding.edgeTabFullyExpandedAt = -1d;
            binding.edgeTabHoverIntentStartedAt = -1d;
            binding.edgeTabHoverExitGraceUntil = -1d;
            binding.pointerInside = false;
            binding.hasEdgeTabPointerPosition = false;
            ResetSemiSleepDrag(binding);
            ShowSemiSleepOverlay(binding, false, 0f);
            RefreshSemiSleepControls(binding);
            if (!hadState || binding.window == null)
                return;
            try
            {
                binding.window.minSize = binding.awakeMinSize;
                binding.window.maxSize = binding.awakeMaxSize;
                if (restoreBounds && !binding.window.docked)
                    CommitSemiSleepWindowPosition(binding, binding.awakeBounds);
            }
            catch (Exception exception) when (
                exception is MissingReferenceException
                || exception is NullReferenceException
                || exception is InvalidOperationException)
            {
            }
        }

        private static void RestoreAllSemiSleepWindows()
        {
            if (!assemblyReloadPreferencesCaptured)
            {
                StopTransientWindowVisuals(false);
                CaptureAssemblyReloadPreferences();
            }
            foreach (WindowBinding binding in windowBindings.Values)
            {
                // Assembly reload and editor shutdown must not turn a sleeping
                // window into its awake rectangle. Preserve the current native
                // geometry; Unity may discard the old VisualTree, and the next
                // BindWindow call will rebuild the overlay from the snapshot.
                SuspendWindowBinding(binding, true);
            }
            semiSleepAnyAnimating = false;
        }

        private static void RestoreAutomaticSemiSleepWindows()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && !binding.semiSleepManualHold)
                    RestoreSemiSleep(binding, true);
            semiSleepAnyAnimating = HasSemiSleepRuntimeState();
        }

        private static int AcquireSemiSleepSlot(WindowBinding requested)
        {
            semiSleepUsedSlotScratch.Clear();
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null
                    && binding != requested
                    && (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget)
                    && binding.semiSleepSlot >= 0)
                    semiSleepUsedSlotScratch.Add(binding.semiSleepSlot);
            int slot = 0;
            while (semiSleepUsedSlotScratch.Contains(slot))
                slot++;
            return slot;
        }

        internal static Rect EvaluateSemiSleepTarget(Rect trayBounds, int slot)
        {
            int safeSlot = Mathf.Max(0, slot);
            float availableWidth = Mathf.Max(SemiSleepSize, trayBounds.width - SemiSleepTrayMargin * 2f);
            int columns = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth + SemiSleepTrayGap)
                    / (SemiSleepSize + SemiSleepTrayGap)));
            int column = safeSlot % columns;
            int row = safeSlot / columns;
            return new Rect(
                trayBounds.xMax - SemiSleepTrayMargin - SemiSleepSize
                    - column * (SemiSleepSize + SemiSleepTrayGap),
                trayBounds.yMax - SemiSleepTrayMargin - SemiSleepSize
                    - row * (SemiSleepSize + SemiSleepTrayGap),
                SemiSleepSize,
                SemiSleepSize);
        }

        internal static Rect ClampSemiSleepDockBounds(Rect bounds, Rect trayBounds)
        {
            float width = Mathf.Min(Mathf.Max(1f, bounds.width), Mathf.Max(1f, trayBounds.width));
            float height = Mathf.Min(Mathf.Max(1f, bounds.height), Mathf.Max(1f, trayBounds.height));
            float minX = trayBounds.xMin;
            float maxX = Mathf.Max(minX, trayBounds.xMax - width);
            float minY = trayBounds.yMin;
            float maxY = Mathf.Max(minY, trayBounds.yMax - height);
            return new Rect(
                Mathf.Clamp(bounds.x, minX, maxX),
                Mathf.Clamp(bounds.y, minY, maxY),
                width,
                height);
        }

        internal static Rect EvaluateSemiSleepDragFrame(
            Rect current,
            Vector2 pointerDelta,
            Rect trayBounds)
        {
            if (!IsFinite(pointerDelta)
                || !IsFinite(current.position)
                || !IsFinite(current.size)
                || !IsFinite(trayBounds.position)
                || !IsFinite(trayBounds.size))
                return current;

            Rect target = current;
            // The tray clamp is the only movement bound. Do not clamp the total
            // pointer displacement: a long, deliberate drag must reach any point
            // in the valid work area rather than stop after an arbitrary distance.
            target.position += pointerDelta;
            return ClampSemiSleepDockBounds(target, trayBounds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsUsableWindowBounds(Rect bounds)
        {
            return IsFinite(bounds.position)
                && IsFinite(bounds.size)
                && bounds.width > 1f
                && bounds.height > 1f;
        }

        private static Rect GetSemiSleepTrayBounds(Rect fallback)
        {
            try
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                if (main.width >= SemiSleepSize && main.height >= SemiSleepSize)
                {
                    Rect overlap = Rect.MinMaxRect(
                        Mathf.Max(main.xMin, fallback.xMin),
                        Mathf.Max(main.yMin, fallback.yMin),
                        Mathf.Min(main.xMax, fallback.xMax),
                        Mathf.Min(main.yMax, fallback.yMax));
                    if (overlap.width > 0f && overlap.height > 0f)
                        return main;
                }
            }
            catch (Exception exception) when (
                exception is MissingMethodException
                || exception is InvalidOperationException)
            {
            }
            return fallback.width >= SemiSleepSize && fallback.height >= SemiSleepSize
                ? fallback
                : new Rect(fallback.position, new Vector2(SemiSleepSize, SemiSleepSize));
        }

        private static WindowBinding FindBindingByRoot(VisualElement element)
        {
            if (element == null)
                return null;
            return windowBindingsByRoot.TryGetValue(element, out WindowBinding binding)
                ? binding
                : null;
        }

        private static void UpdateWindowOverlay(WindowBinding binding)
        {
            if (binding == null || binding.lifecycleSuspended)
            {
                binding?.animation?.Pause();
                return;
            }
            if (!IsWindowOverlayAttached(binding))
            {
                binding?.animation?.Pause();
                return;
            }

            try
            {
                float pulse = EvaluatePulse(binding.pulseStartedAt, binding.pulseDuration);
                if (pulse <= 0f)
                {
                    binding.animation?.Pause();
                    binding.sweep.style.width = 0f;
                    binding.accentLine.style.backgroundColor =
                        binding.activityState == ESWindowActivityState.Attention
                            ? GetStatusAccent(0, binding.pulseStatus)
                            : GetDepthAccent(0);
                    return;
                }

                Color accent = GetStatusAccent(0, binding.pulseStatus);
                accent.a = Mathf.Clamp01(0.62f + pulse * 0.34f);
                binding.accentLine.style.backgroundColor = accent;

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - binding.pulseStartedAt) / binding.pulseDuration));
                float hostWidth = binding.host.resolvedStyle.width;
                if (float.IsNaN(hostWidth) || float.IsInfinity(hostWidth) || hostWidth <= 0f)
                    hostWidth = 180f;
                float width = Mathf.Clamp(hostWidth * 0.16f, 24f, 180f);
                binding.sweep.style.width = width;
                binding.sweep.style.left = Mathf.Lerp(-width, hostWidth, progress);
                Color sweepColor = accent;
                sweepColor.a = Mathf.Clamp01(0.10f + pulse * 0.24f);
                binding.sweep.style.backgroundColor = sweepColor;
                binding.window.Repaint();
            }
            catch (NullReferenceException)
            {
                // Unity may invalidate an internal InlineStyleAccess between DetachFromPanel and
                // the scheduled callback. Stop this local animation; the window content remains intact.
                binding.animation?.Pause();
            }
        }

        private static bool IsWindowOverlayAttached(WindowBinding binding)
        {
            bool overlayAttached = binding != null
                && binding.window != null
                && binding.root != null
                && ReferenceEquals(binding.root, binding.window.rootVisualElement)
                && binding.root.panel != null
                && binding.host != null
                && binding.host.panel != null
                && ReferenceEquals(binding.host.parent, binding.root)
                && binding.accentLine != null
                && binding.sweep != null
                && binding.semiSleepOverlay != null
                && binding.semiSleepOverlay.panel != null
                && ReferenceEquals(binding.semiSleepOverlay.parent, binding.root);
            if (!overlayAttached || !binding.supportsSemiSleep)
                return overlayAttached;

            VisualElement systemHost = FindDeclaredSystemActionHost(binding);
            return systemHost != null
                && systemHost.panel != null
                && binding.semiSleepControls != null
                && binding.semiSleepControls.panel != null
                && IsDescendantOf(binding.semiSleepControls, systemHost);
        }

        /// <summary>Normalized global motion strength used by all ES editor surfaces.</summary>
        public static float MotionIntensity
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 0.78f : Mathf.Clamp01(current.motionIntensity);
            }
        }

        /// <summary>
        /// Returns a one-shot, allocation-free pulse value. Callers can request a repaint only
        /// while this value is non-zero; no global per-frame repaint loop is installed here.
        /// </summary>
        public static float EvaluatePulse(double startedAt, float duration = 0.42f)
        {
            if (!MotionEnabled || startedAt <= 0d || duration <= 0.001f)
                return 0f;

            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            if (elapsed <= 0d || elapsed >= duration)
                return 0f;

            float normalized = Mathf.Clamp01((float)(elapsed / duration));
            return Mathf.Sin(normalized * Mathf.PI) * MotionIntensity;
        }

        /// <summary>
        /// Returns a subtle looping breath value for a focused/selected surface. It is intended
        /// for a single local highlight, never for animating an entire large editor tree.
        /// </summary>
        public static float EvaluateBreath(double now = -1d, float period = 1.6f)
        {
            if (!MotionEnabled || period <= 0.05f)
                return 0f;

            if (now < 0d)
                now = EditorApplication.timeSinceStartup;

            float phase = Mathf.Repeat((float)(now / period), 1f) * Mathf.PI * 2f;
            return (0.5f + 0.5f * Mathf.Sin(phase)) * MotionIntensity;
        }

        /// <summary>Blends a base color toward an ES accent without changing semantic status.</summary>
        public static Color GetMotionColor(Color baseColor, Color accent, float amount)
        {
            float strength = Mathf.Clamp01(amount) * MotionIntensity;
            return Color.Lerp(baseColor, accent, strength);
        }

        /// <summary>
        /// Draws a one-shot feedback frame for IMGUI surfaces. Returns true while the effect is
        /// active so the owning window can schedule a local repaint.
        /// </summary>
        public static bool DrawFeedbackFrame(
            Rect rect,
            ESStatusKind status,
            int depth,
            double startedAt,
            float duration = 0.42f,
            float thickness = 1f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            Color accent = GetStatusAccent(depth, status);
            accent.a = Mathf.Clamp01(0.28f + pulse * 0.52f);
            DrawFrame(rect, accent, thickness);
            return true;
        }

        /// <summary>
        /// Draws a restrained horizontal sweep used for save/preview/selection feedback. The
        /// caller owns the animation start time and should repaint only the local view.
        /// </summary>
        public static bool DrawFeedbackSweep(
            Rect rect,
            Color accent,
            double startedAt,
            float duration = 0.60f,
            float widthRatio = 0.18f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            float sweepProgress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startedAt) / duration));
            float sweepWidth = Mathf.Clamp(rect.width * widthRatio, 6f, 96f);
            float x = Mathf.Lerp(rect.x - sweepWidth, rect.xMax, sweepProgress);
            Color sweepColor = accent;
            sweepColor.a = Mathf.Clamp01(0.06f + pulse * 0.18f);
            EditorGUI.DrawRect(new Rect(x, rect.y, sweepWidth, rect.height), sweepColor);
            return true;
        }

        public static Color SectionSelectedFill
        {
            get
            {
                EnsureSkin();
                return GetSelectionFill(cachedProSkin);
            }
        }

        public static Color GetSelectionFill(bool proSkin)
        {
            return proSkin
                ? new Color(0.18f, 0.32f, 0.46f, 0.34f)
                : new Color(0.72f, 0.78f, 0.84f, 0.42f);
        }

        public static Color SectionTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.88f, 0.91f, 0.96f, 1f)
                    : new Color(0.28f, 0.30f, 0.33f, 1f);
            }
        }

        public static Color SectionSelectedTextColor
        {
            get { return GetDepthAccent(0); }
        }

        public static Color SectionMutedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.64f, 0.69f, 0.77f, 1f)
                    : new Color(0.50f, 0.52f, 0.55f, 1f);
            }
        }

        public static Color SectionMarkerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.42f, 0.45f, 0.49f, 1f)
                    : new Color(0.54f, 0.57f, 0.60f, 1f);
            }
        }

        public static Color WarningBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.33f, 0.22f, 0.16f, 0.90f)
                    : new Color(1f, 0.92f, 0.84f, 1f);
            }
        }

        public static Color NeutralSelectorBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.25f, 0.26f, 0.28f, 0.90f)
                    : new Color(0.88f, 0.89f, 0.90f, 1f);
            }
        }

        public static Color NeutralHoverColor
        {
            get { return new Color(0.48f, 0.51f, 0.55f, 1f); }
        }

        public static Color WarningTextColor
        {
            get
            {
                EnsureSkin();
                return Color.Lerp(
                    WarningColor,
                    SectionTextColor,
                    cachedProSkin ? 0.18f : 0.08f);
            }
        }

        public static Color EmptyTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.74f, 0.78f, 0.85f, 1f)
                    : new Color(0.38f, 0.41f, 0.45f, 1f);
            }
        }

        public static Color SelectedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.54f, 0.70f, 0.80f, 1f)
                    : new Color(0.10f, 0.32f, 0.52f, 1f);
            }
        }

        public static Color SelectorArrowColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.59f, 0.62f, 0.66f, 1f)
                    : new Color(0.39f, 0.42f, 0.46f, 1f);
            }
        }

        public static Color ClearActionColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.65f, 0.48f, 0.48f, 1f)
                    : new Color(0.62f, 0.28f, 0.28f, 1f);
            }
        }

        // Semantic surface and interaction tokens shared by ES windows. Consumers should use
        // these accessors instead of embedding graph-specific RGB values in their view code.
        public static Color WindowSurfaceColor => GetDepthBackground(0);
        public static Color WindowRaisedSurfaceColor => GetDepthBackground(1);
        public static Color WindowInsetSurfaceColor => GetDepthBackground(2);
        public static Color CanvasSurfaceColor => GetDepthBackground(3);
        public static Color ToolbarSurfaceColor => GetSelectorBackground(0);
        public static Color ControlSurfaceColor => GetSelectorBackground(1);
        /// <summary>
        /// 通用选中色。选中态不复用主题的天蓝起始色，避免边框、导引线和选中卡片
        /// 在浅色皮肤下与白色文字或浅色表面混成一片。
        /// </summary>
        public static Color SelectionColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.29f, 0.51f, 0.66f, 0.96f)
                    : new Color(0.24f, 0.46f, 0.62f, 0.96f);
            }
        }
        /// <summary>
        /// 选中对象的低对比表面色。与 SelectionColor 分离，避免把标记色直接铺到
        /// 卡片或按钮背景上；SelectionColor 仅用于边框、导引线和小型选中标记。
        /// </summary>
        public static Color SelectedSurfaceColor
        {
            get
            {
                EnsureSkin();
                return Color.Lerp(
                    WindowRaisedSurfaceColor,
                    ControlSurfaceColor,
                    cachedProSkin ? 0.62f : 0.56f);
            }
        }
        /// <summary>
        /// 高对比主操作底色。SelectionColor 服务选中/标记，PrimaryActionColor 专门承载主操作背景。
        /// </summary>
        public static Color PrimaryActionColor
        {
            get
            {
                EnsureSkin();
                return GetPrimaryActionColor(cachedProSkin);
            }
        }

        private static Color GetPrimaryActionColor(bool proSkin)
        {
            return proSkin
                ? new Color(0.30f, 0.32f, 0.35f, 0.98f)
                : new Color(0.20f, 0.22f, 0.25f, 1f);
        }

        /// <summary>
        /// 非活动或被禁用的操作控件底色。它保持中性低对比，不借用 WarningColor，
        /// 避免把“关闭/不可用”误读成故障或在窄工具栏中形成刺眼色块。
        /// </summary>
        public static Color InactiveActionColor
        {
            get
            {
                EnsureSkin();
                return Color.Lerp(
                    ControlSurfaceColor,
                    WindowInsetSurfaceColor,
                    cachedProSkin ? 0.58f : 0.42f);
            }
        }

        /// <summary>
        /// 激活操作控件的承载面。它保留状态强调色的辨识度，但不会直接把高亮强调色
        /// 铺成按钮背景，确保浅色文字在深色与浅色编辑器皮肤下都保持清晰。
        /// </summary>
        public static Color ActiveActionColor
        {
            get
            {
                EnsureSkin();
                return GetActiveActionColor(cachedProSkin, ActiveColor);
            }
        }

        public static Color GetActiveActionColor(bool proSkin, Color activeAccent)
        {
            return GetActionSurfaceColor(proSkin, activeAccent);
        }

        public static Color WarningActionColor
        {
            get
            {
                EnsureSkin();
                return GetActionSurfaceColor(cachedProSkin, WarningColor);
            }
        }

        private static Color GetActionSurfaceColor(bool proSkin, Color accent)
        {
            const float maximumAccentChannel = 0.70f;
            Color actionSurface = GetPrimaryActionColor(proSkin);
            Color boundedAccent = new Color(
                Mathf.Min(accent.r, maximumAccentChannel),
                Mathf.Min(accent.g, maximumAccentChannel),
                Mathf.Min(accent.b, maximumAccentChannel),
                1f);
            Color result = Color.Lerp(
                actionSurface,
                boundedAccent,
                proSkin ? 0.18f : 0.12f);
            result.a = 1f;
            return result;
        }

        public static Color PrimaryActionTextColor => new Color(0.98f, 0.99f, 1f, 1f);
        public static Color ActiveColor => GetStatusAccent(0, ESStatusKind.Ready);
        public static Color DisabledColor => GetStatusAccent(0, ESStatusKind.ReadOnly);
        public static Color WarningColor => GetStatusAccent(0, ESStatusKind.Warning);
        public static Color ErrorColor => GetStatusAccent(0, ESStatusKind.Error);
        public static Color NodeBorderColor => GetStatusFrameColor(1, ESStatusKind.None);
        public static Color NodeSelectedBorderColor => GetStatusFrameColor(0, ESStatusKind.Modified);

        public static Color MapTerrainBaseColor => GetMapThemeColor(new Color(0.07f, 0.11f, 0.16f, 1f), new Color(0.92f, 0.94f, 0.97f, 1f), theme => theme.darkMapTerrainBase, theme => theme.lightMapTerrainBase);
        public static Color MapGridColor => GetMapThemeColor(new Color(0.42f, 0.52f, 0.64f, 0.22f), new Color(0.22f, 0.34f, 0.48f, 0.24f), theme => theme.darkMapGrid, theme => theme.lightMapGrid);
        public static Color MapRegionColor => GetMapThemeColor(new Color(0.18f, 0.48f, 0.86f, 0.22f), new Color(0.18f, 0.46f, 0.82f, 0.18f), theme => theme.darkMapRegion, theme => theme.lightMapRegion);
        public static Color MapPoiColor => GetMapThemeColor(new Color(0.96f, 0.70f, 0.24f, 0.96f), new Color(0.78f, 0.40f, 0.04f, 1f), theme => theme.darkMapPoi, theme => theme.lightMapPoi);
        public static Color MapSelectionColor => GetMapThemeColor(new Color(0.32f, 0.88f, 0.68f, 0.96f), new Color(0.04f, 0.54f, 0.42f, 1f), theme => theme.darkMapSelection, theme => theme.lightMapSelection);
        public static Color MapHeightLowColor => GetMapThemeColor(new Color(0.06f, 0.22f, 0.18f, 1f), new Color(0.42f, 0.74f, 0.60f, 1f), theme => theme.darkMapHeightLow, theme => theme.lightMapHeightLow);
        public static Color MapHeightHighColor => GetMapThemeColor(new Color(0.76f, 0.62f, 0.28f, 1f), new Color(0.86f, 0.70f, 0.30f, 1f), theme => theme.darkMapHeightHigh, theme => theme.lightMapHeightHigh);

        private static Color GetMapThemeColor(Color darkFallback, Color lightFallback, Func<ESGlobalEditorTheme, Color> darkSelector, Func<ESGlobalEditorTheme, Color> lightSelector)
        {
            EnsureSkin();
            ESGlobalEditorTheme theme = CurrentTheme;
            if (theme == null || !theme.useCustomPalette) return cachedProSkin ? darkFallback : lightFallback;
            Color color = cachedProSkin ? darkSelector(theme) : lightSelector(theme);
            color.a = Mathf.Clamp01(color.a);
            return color;
        }

        public static Color GetSemanticAccent(int paletteIndex)
        {
            Color color;
            switch (paletteIndex)
            {
                case 1: color = new Color(0.25f, 0.55f, 0.96f); break;
                case 2: color = new Color(0.30f, 0.72f, 0.46f); break;
                case 3: color = new Color(0.82f, 0.38f, 0.38f); break;
                case 4: color = new Color(0.32f, 0.74f, 0.45f); break;
                case 5: color = new Color(0.86f, 0.34f, 0.36f); break;
                case 6: color = new Color(0.95f, 0.63f, 0.22f); break;
                case 7: color = new Color(0.28f, 0.72f, 0.72f); break;
                case 8: color = new Color(0.35f, 0.62f, 0.90f); break;
                case 9: color = new Color(0.48f, 0.58f, 0.86f); break;
                case 10: color = new Color(0.28f, 0.75f, 0.72f); break;
                case 11: color = new Color(0.95f, 0.63f, 0.22f); break;
                case 12: color = new Color(0.65f, 0.43f, 0.94f); break;
                case 13: color = new Color(0.83f, 0.39f, 0.72f); break;
                case 14: color = new Color(0.35f, 0.78f, 0.43f); break;
                default: color = new Color(0.42f, 0.48f, 0.58f); break;
            }

            Color themeAccent = GetDepthAccent(Mathf.Abs(paletteIndex) % 3);
            color = Color.Lerp(color, themeAccent, 0.12f);
            color.a = 1f;
            return color;
        }

        public static Color NormalizeSemanticAccent(Color requested, int fallbackPaletteIndex)
        {
            if (requested.a <= 0f)
                return GetSemanticAccent(fallbackPaletteIndex);
            Color normalized = Color.Lerp(requested, GetDepthAccent(0), 0.12f);
            normalized.a = 1f;
            return normalized;
        }

        public static Color GetSemanticChannelColor(int channel)
        {
            switch (channel)
            {
                case 1: return GetSemanticAccent(0);
                case 2: return Color.Lerp(DisabledColor, SectionTextColor, 0.45f);
                case 3: return Color.Lerp(WarningColor, ErrorColor, 0.38f);
                case 4: return GetSemanticAccent(4);
                case 5: return GetSemanticAccent(8);
                case 6: return GetSemanticAccent(12);
                case 7: return GetSemanticAccent(1);
                case 8: return GetSemanticAccent(6);
                case 9: return GetSemanticAccent(13);
                default: return Color.Lerp(DisabledColor, SectionMutedTextColor, 0.48f);
            }
        }

        public static Texture2D SurfaceTexture
        {
            get
            {
                EnsureSkin();
                if (surfaceTexture == null)
                {
                    surfaceTexture = CreateRoundedRectTexture(
                        "ESEditorPresentationSurface",
                        ESCornerRadiusToken.Section,
                        DividerColor,
                        WindowRaisedSurfaceColor);
                }

                return surfaceTexture;
            }
        }

        private static Texture2D ToolbarTexture => toolbarTexture ?? (toolbarTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationToolbar",
                ESCornerRadiusToken.Control,
                DividerColor,
                ToolbarSurfaceColor));

        private static Texture2D ToolbarButtonTexture => toolbarButtonTexture ?? (toolbarButtonTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationToolbarButton",
                ESCornerRadiusToken.Control,
                DividerColor,
                ControlSurfaceColor));

        private static Texture2D ToolbarButtonHoverTexture => toolbarButtonHoverTexture ?? (toolbarButtonHoverTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationToolbarButtonHover",
                ESCornerRadiusToken.Control,
                Color.Lerp(DividerColor, SectionSelectedTextColor, 0.25f),
                Color.Lerp(ControlSurfaceColor, WindowRaisedSurfaceColor, 0.55f)));

        private static Texture2D ToolbarButtonActiveTexture => toolbarButtonActiveTexture ?? (toolbarButtonActiveTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationToolbarButtonActive",
                ESCornerRadiusToken.Control,
                PrimaryActionColor,
                PrimaryActionColor));

        private static Texture2D PrimaryButtonTexture => primaryButtonTexture ?? (primaryButtonTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationPrimaryButton",
                ESCornerRadiusToken.Control,
                PrimaryActionColor,
                PrimaryActionColor));

        private static Texture2D PrimaryButtonHoverTexture => primaryButtonHoverTexture ?? (primaryButtonHoverTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationPrimaryButtonHover",
                ESCornerRadiusToken.Control,
                Color.Lerp(PrimaryActionColor, PrimaryActionTextColor, 0.20f),
                Color.Lerp(PrimaryActionColor, PrimaryActionTextColor, 0.10f)));

        private static Texture2D PrimaryButtonActiveTexture => primaryButtonActiveTexture ?? (primaryButtonActiveTexture =
            CreateRoundedRectTexture(
                "ESEditorPresentationPrimaryButtonActive",
                ESCornerRadiusToken.Control,
                Color.Lerp(PrimaryActionColor, Color.black, 0.30f),
                Color.Lerp(PrimaryActionColor, Color.black, 0.18f)));

        private static Texture2D CompactCollectionBodyTexture
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyTexture == null)
                {
                    Color fill = cachedProSkin
                        ? new Color(0.16f, 0.17f, 0.19f, 0.96f)
                        : new Color(0.94f, 0.945f, 0.95f, 1f);
                    compactCollectionBodyTexture = CreateRoundedRectTexture(
                        "ESEditorPresentationCompactCollectionBody",
                        ESCornerRadiusToken.Card,
                        DividerColor,
                        fill);
                }

                return compactCollectionBodyTexture;
            }
        }

        public static float GetDepthProgress(int depth)
        {
            if (depth <= 0)
                return 0f;

            // The first nested level must be visually obvious; later levels converge quickly
            // so deep data remains readable instead of becoming nearly black.
            return Mathf.Clamp01(0.28f + (depth - 1) * 0.38f);
        }

        public static Color GetDepthAccent(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            ESGlobalEditorTheme current = CurrentTheme;
            Color start = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentStart : current.lightAccentStart)
                : cachedProSkin
                    ? new Color(0.36f, 0.62f, 0.78f, 0.90f)
                    : new Color(0.16f, 0.40f, 0.62f, 0.90f);
            Color end = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentEnd : current.lightAccentEnd)
                : cachedProSkin
                    ? new Color(0.13f, 0.42f, 0.72f, 0.96f)
                    : new Color(0.04f, 0.24f, 0.56f, 0.96f);
            return Color.Lerp(start, end, progress);
        }

        public static Color GetDepthBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.15f, 0.16f, 0.18f, 0.98f),
                    new Color(0.075f, 0.08f, 0.095f, 0.99f),
                    progress)
                : Color.Lerp(
                    new Color(0.96f, 0.96f, 0.96f, 1f),
                    new Color(0.86f, 0.87f, 0.88f, 1f),
                    progress);
        }

        public static Color GetSelectorBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.12f, 0.13f, 0.15f, 0.94f),
                    new Color(0.075f, 0.08f, 0.095f, 0.96f),
                    progress)
                : Color.Lerp(
                    new Color(0.91f, 0.92f, 0.93f, 1f),
                    new Color(0.82f, 0.84f, 0.86f, 1f),
                    progress);
        }

        public static Color GetStatusFrameColor(int depth, ESStatusKind status)
        {
            EnsureSkin();
            if (status == ESStatusKind.Error)
            {
                Color error = GetStatusAccent(depth, status);
                error.a = 0.78f;
                return error;
            }

            if (status == ESStatusKind.Warning)
            {
                Color warning = GetStatusAccent(depth, status);
                warning.a = 0.82f;
                return warning;
            }

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            Color accent = GetStatusAccent(depth, status);
            accent.a = cachedProSkin ? 0.72f : 0.64f;
            return accent;
        }

        public static Color GetFieldLevelAccent(ESFieldLevel level)
        {
            EnsureSkin();
            switch (level)
            {
                case ESFieldLevel.Core:
                    return GetDepthAccent(0);
                case ESFieldLevel.Important:
                    return GetDepthAccent(1);
                default:
                    return GetStatusFrameColor(0, ESStatusKind.None);
            }
        }

        public static void StyleField(VisualElement field, Label label, ESFieldLevel level,
            bool required, bool empty, string hint)
        {
            if (field == null)
                return;
            string levelText = level == ESFieldLevel.Core ? "核心"
                : level == ESFieldLevel.Important ? "重点" : string.Empty;
            if (label != null)
            {
                string clean = label.text ?? string.Empty;
                if (!string.IsNullOrEmpty(levelText)
                    && !clean.StartsWith(levelText + " · ", StringComparison.Ordinal))
                    clean = levelText + " · " + clean;
                if (required && !clean.EndsWith(" *", StringComparison.Ordinal))
                    clean += " *";
                if (!string.Equals(label.text, clean, StringComparison.Ordinal))
                    label.text = clean;
                if (level != ESFieldLevel.Normal)
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            Color accent = required && empty
                ? GetStatusAccent(0, ESStatusKind.Error)
                : GetFieldLevelAccent(level);
            if (level != ESFieldLevel.Normal || required && empty)
            {
                field.style.borderLeftWidth = level == ESFieldLevel.Core ? 3f : 2f;
                field.style.borderLeftColor = accent;
                field.style.paddingLeft = 4f;
                Color background = accent;
                background.a = EditorGUIUtility.isProSkin ? 0.075f : 0.045f;
                field.style.backgroundColor = background;
            }

            string metaText = (string.IsNullOrEmpty(levelText) ? string.Empty : levelText)
                + (required ? (string.IsNullOrEmpty(levelText) ? "必填" : " · 必填") : string.Empty);
            string tooltip = BuildFieldTooltip(metaText, hint, field.tooltip);
            if (!string.Equals(field.tooltip, tooltip, StringComparison.Ordinal))
                field.tooltip = tooltip;
        }

        private static string BuildFieldTooltip(string metaText, string hint, string existing)
        {
            string result = existing ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(hint))
            {
                string normalizedHint = hint.Trim();
                if (!ContainsTooltipLine(result, normalizedHint))
                    result = string.IsNullOrEmpty(result)
                        ? normalizedHint
                        : normalizedHint + "\n" + result;
            }

            if (!string.IsNullOrEmpty(metaText) && !ContainsTooltipLine(result, metaText))
                result = string.IsNullOrEmpty(result) ? metaText : metaText + "\n" + result;
            return result;
        }

        private static bool ContainsTooltipLine(string tooltip, string line)
        {
            if (string.IsNullOrEmpty(tooltip) || string.IsNullOrEmpty(line))
                return false;
            int searchFrom = 0;
            while (searchFrom <= tooltip.Length - line.Length)
            {
                int index = tooltip.IndexOf(line, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                    return false;
                int end = index + line.Length;
                bool startsAtLine = index == 0 || tooltip[index - 1] == '\n';
                bool endsAtLine = end == tooltip.Length || tooltip[end] == '\n' || tooltip[end] == '\r';
                if (startsAtLine && endsAtLine)
                    return true;
                searchFrom = index + 1;
            }
            return false;
        }

        public static Color GetStatusAccent(int depth, ESStatusKind status)
        {
            EnsureSkin();
            ESGlobalEditorTheme current = CurrentTheme;
            if (status == ESStatusKind.Error)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkError : current.lightError)
                    : new Color(0.92f, 0.40f, 0.24f, 0.96f);

            if (status == ESStatusKind.Warning)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkWarning : current.lightWarning)
                    : cachedProSkin
                        ? new Color(0.68f, 0.48f, 0.24f, 0.92f)
                        : new Color(0.58f, 0.33f, 0.10f, 0.92f);

            if (status == ESStatusKind.Modified)
                return cachedProSkin
                    ? new Color(0.32f, 0.55f, 0.68f, 0.90f)
                    : new Color(0.20f, 0.40f, 0.58f, 0.90f);

            if (status == ESStatusKind.ReadOnly)
                return cachedProSkin
                    ? new Color(0.50f, 0.54f, 0.60f, 0.86f)
                    : new Color(0.45f, 0.49f, 0.54f, 0.86f);

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            return GetDepthAccent(depth);
        }

        public static void DrawCompactCollectionHeaderBackground(
            Rect rect,
            int depth,
            ESStatusKind status,
            bool expanded)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EnsureSkin();
            Color background = cachedProSkin
                ? new Color(0.205f, 0.215f, 0.235f, 0.98f)
                : new Color(0.885f, 0.895f, 0.91f, 1f);
            background = Color.Lerp(background, GetDepthBackground(depth), 0.22f);

            Color accent = status == ESStatusKind.Error || status == ESStatusKind.Warning
                ? GetStatusAccent(depth, status)
                : GetDepthAccent(depth);
            if (status == ESStatusKind.Error || status == ESStatusKind.Warning)
                background = Color.Lerp(background, accent, cachedProSkin ? 0.14f : 0.09f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, Metric(4f), rect.height), accent);

            Color edge = GetStatusFrameColor(depth, status);
            edge.a = cachedProSkin ? 0.74f : 0.66f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), edge);

            if (!expanded)
                return;

            Color openEdge = accent;
            openEdge.a = cachedProSkin ? 0.48f : 0.38f;
            EditorGUI.DrawRect(new Rect(rect.x + Metric(4f), rect.yMax - 2f, rect.width - Metric(4f), 1f), openEdge);
        }

        public static void DrawFrame(Rect rect, Color color, float thickness = 1f)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawDivider(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f)
                EditorGUI.DrawRect(rect, DividerColor);
        }

        private static bool BrandTypographyEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.enableBrandTypography;
            }
        }

        private static void ApplyBrandFont(GUIStyle style)
        {
            if (style == null || !BrandTypographyEnabled)
                return;

            if (!brandFontLoadAttempted)
            {
                brandFontLoadAttempted = true;
                brandFont = Resources.Load<Font>(BrandFontResourcePath);
            }
            if (brandFont != null)
                style.font = brandFont;
        }

        private static void ApplyBrandTypography(VisualElement root)
        {
            if (root == null)
                return;

            if (brandTypographyStyleSheet == null)
                brandTypographyStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BrandTypographyStyleSheetPath);
            if (brandTypographyStyleSheet != null && !root.styleSheets.Contains(brandTypographyStyleSheet))
                root.styleSheets.Add(brandTypographyStyleSheet);
            root.AddToClassList(PresentationControlsClass);
            root.EnableInClassList("es-brand-typography", BrandTypographyEnabled);
        }

        private static void RemoveBrandTypography(VisualElement root)
        {
            if (root == null)
                return;
            root.RemoveFromClassList(PresentationControlsClass);
            root.RemoveFromClassList("es-brand-typography");
            if (brandTypographyStyleSheet != null && root.styleSheets.Contains(brandTypographyStyleSheet))
                root.styleSheets.Remove(brandTypographyStyleSheet);
        }

        public static void InvalidateSkinCache()
        {
            skinInitialized = false;
        }

        public static void InvalidateTheme()
        {
            themeGeneration++;
            themeInitialized = false;
            theme = null;
            InvalidateSkinCache();
            if (GlobalEditorShellEnabled)
                InstallGlobalEditorAdapters();
            else
            {
                UninstallGlobalEditorAdapterCallbacks();
                // PlayMode and compilation are temporary presentation
                // suspensions. Do not discard the binding records here: the
                // corresponding lifecycle callback needs them to restore the
                // user's persisted sleep state after Unity rebuilds panels.
                bool lifecycleSuspended = EditorApplication.isPlayingOrWillChangePlaymode
                    || domainReloadInProgress
                    || EditorApplication.isCompiling;
                if (lifecycleSuspended)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        CapturePlayModePreferences();
                    else
                        CaptureAssemblyReloadPreferences();
                    SuspendWindowBindings();
                }
                else
                    UnbindAllWindowBindings();
            }
            QueueDeepSkinSynchronization();
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (!IsWindowOverlayAttached(binding))
                    continue;

                try
                {
                    ApplyBrandTypography(binding.root);
                    ESWindowPresentation.ApplySemanticTheme(binding.root);
                    Color accent = GetDepthAccent(0);
                    binding.host.style.backgroundColor = accent;
                    binding.accentLine.style.backgroundColor = accent;
                    binding.window.Repaint();
                }
                catch (NullReferenceException)
                {
                    binding.animation?.Pause();
                }
            }
            SceneView.RepaintAll();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        private static ESGlobalEditorTheme CurrentTheme
        {
            get
            {
                if (!themeInitialized)
                {
                    theme = ESGlobalEditorTheme.Instance;
                    themeInitialized = true;
                }

                return theme;
            }
        }

        internal static ESPresentationThemeSnapshot CurrentPresentationTheme
        {
            get
            {
                _ = CurrentTheme;
                EnsureSkin();
                int currentSkinGeneration = SkinGeneration;
                if (presentationThemeSnapshot == null
                    || presentationThemeSnapshot.ThemeGeneration != themeGeneration
                    || presentationThemeSnapshot.SkinGeneration != currentSkinGeneration)
                {
                    presentationThemeSnapshot = BuildPresentationThemeSnapshot();
                }

                return presentationThemeSnapshot;
            }
        }

        internal static ESPresentationThemeSnapshot BuildPresentationThemeSnapshot()
        {
            _ = CurrentTheme;
            EnsureSkin();
            return new ESPresentationThemeSnapshot(
                themeGeneration,
                SkinGeneration,
                cachedProSkin,
                Density,
                MotionEnabled,
                WindowSurfaceColor,
                WindowRaisedSurfaceColor,
                WindowInsetSurfaceColor,
                CanvasSurfaceColor,
                ToolbarSurfaceColor,
                ControlSurfaceColor,
                SelectedSurfaceColor,
                InactiveActionColor,
                PrimaryActionColor,
                ActiveActionColor,
                WarningActionColor,
                GetActionSurfaceColor(cachedProSkin, ErrorColor),
                SectionTextColor,
                SectionSelectedTextColor,
                SectionMutedTextColor,
                PrimaryActionTextColor,
                DividerColor,
                SelectionColor,
                ActiveColor,
                DisabledColor,
                WarningColor,
                ErrorColor);
        }

        internal static ESPresentationStyle ResolvePresentationStyle(
            ESPresentationRole role,
            ESPresentationState state = ESPresentationState.Normal,
            ESPresentationInteraction interaction = ESPresentationInteraction.Rest)
        {
            ESPresentationThemeSnapshot snapshot = CurrentPresentationTheme;
            Color background;
            Color text;
            Color border = snapshot.Divider;
            float opacity = 1f;

            switch (role)
            {
                case ESPresentationRole.WindowSurface:
                    background = snapshot.WindowSurface;
                    text = snapshot.Text;
                    break;
                case ESPresentationRole.RaisedSurface:
                    background = snapshot.RaisedSurface;
                    text = snapshot.Text;
                    break;
                case ESPresentationRole.InsetSurface:
                    background = snapshot.InsetSurface;
                    text = snapshot.Text;
                    break;
                case ESPresentationRole.CanvasSurface:
                    background = snapshot.CanvasSurface;
                    text = snapshot.Text;
                    break;
                case ESPresentationRole.Toolbar:
                    background = snapshot.ToolbarSurface;
                    text = snapshot.StrongText;
                    break;
                case ESPresentationRole.Control:
                    background = snapshot.ControlSurface;
                    text = snapshot.StrongText;
                    break;
                case ESPresentationRole.PrimaryAction:
                    background = snapshot.PrimaryActionSurface;
                    text = snapshot.ActionText;
                    border = snapshot.PrimaryActionSurface;
                    break;
                case ESPresentationRole.Status:
                    background = snapshot.InsetSurface;
                    text = snapshot.Active;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }

            bool actionRole = role == ESPresentationRole.Control
                || role == ESPresentationRole.PrimaryAction;
            switch (state)
            {
                case ESPresentationState.Selected:
                    if (role == ESPresentationRole.Status)
                    {
                        text = snapshot.Selection;
                        border = snapshot.Selection;
                    }
                    else if (actionRole)
                    {
                        background = snapshot.ActiveActionSurface;
                        text = snapshot.ActionText;
                        border = snapshot.Active;
                    }
                    else
                    {
                        background = snapshot.SelectedSurface;
                        text = snapshot.StrongText;
                        border = snapshot.Selection;
                    }
                    break;
                case ESPresentationState.Busy:
                    if (actionRole)
                    {
                        background = snapshot.ActiveActionSurface;
                        text = snapshot.ActionText;
                    }
                    else
                    {
                        text = snapshot.Active;
                    }
                    border = snapshot.Active;
                    break;
                case ESPresentationState.Inactive:
                    if (actionRole)
                        background = snapshot.InactiveActionSurface;
                    text = snapshot.MutedText;
                    border = snapshot.Divider;
                    break;
                case ESPresentationState.Disabled:
                    if (actionRole)
                        background = snapshot.InactiveActionSurface;
                    text = snapshot.MutedText;
                    border = snapshot.Disabled;
                    opacity = 0.58f;
                    break;
                case ESPresentationState.ReadOnly:
                    if (actionRole)
                        background = snapshot.InactiveActionSurface;
                    text = snapshot.Disabled;
                    border = snapshot.Disabled;
                    opacity = 0.86f;
                    break;
                case ESPresentationState.Warning:
                    if (actionRole)
                    {
                        background = snapshot.WarningActionSurface;
                        text = snapshot.ActionText;
                    }
                    else
                    {
                        text = snapshot.Warning;
                    }
                    border = snapshot.Warning;
                    break;
                case ESPresentationState.Error:
                    if (actionRole)
                    {
                        background = snapshot.ErrorActionSurface;
                        text = snapshot.ActionText;
                    }
                    else
                    {
                        text = snapshot.Error;
                    }
                    border = snapshot.Error;
                    break;
            }

            switch (interaction)
            {
                case ESPresentationInteraction.Hover:
                    background = Color.Lerp(background, text, 0.07f);
                    break;
                case ESPresentationInteraction.Pressed:
                    background = Color.Lerp(background, text, 0.12f);
                    break;
                case ESPresentationInteraction.Focused:
                    if (state != ESPresentationState.Warning && state != ESPresentationState.Error)
                        border = snapshot.Selection;
                    break;
            }
            background.a = Mathf.Clamp01(background.a);

            return new ESPresentationStyle(background, text, border, opacity);
        }

        internal static void ApplyPresentationStyle(
            VisualElement element,
            ESPresentationRole role,
            ESPresentationState state = ESPresentationState.Normal,
            ESPresentationInteraction interaction = ESPresentationInteraction.Rest,
            ESCornerRadiusToken radius = ESCornerRadiusToken.None,
            float? borderWidth = 1f)
        {
            if (element == null)
                return;

            ESPresentationStyle style = ResolvePresentationStyle(role, state, interaction);
            element.style.backgroundColor = style.BackgroundColor;
            element.style.color = style.TextColor;
            element.style.opacity = style.Opacity;
            ApplyCornerRadius(element, radius);
            if (borderWidth.HasValue)
            {
                ApplyBorder(element, style.BorderColor, borderWidth.Value);
            }
            else
            {
                element.style.borderLeftColor = style.BorderColor;
                element.style.borderRightColor = style.BorderColor;
                element.style.borderTopColor = style.BorderColor;
                element.style.borderBottomColor = style.BorderColor;
            }
            if (element is Button)
            {
                element.Query<Label>().ForEach(label => label.style.color = style.TextColor);
            }
        }

        internal static ESPresentationState GetPresentationState(ESStatusKind status)
        {
            switch (status)
            {
                case ESStatusKind.Warning: return ESPresentationState.Warning;
                case ESStatusKind.Error: return ESPresentationState.Error;
                case ESStatusKind.ReadOnly: return ESPresentationState.ReadOnly;
                case ESStatusKind.Modified: return ESPresentationState.Selected;
                default: return ESPresentationState.Normal;
            }
        }

        private static int Metric(float value)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * Density));
        }

        /// <summary>
        /// 为 Inspector 右侧的小型操作按钮保留可点击的安全留白。
        /// 窄面板尽量节省空间，宽面板逐步增加缓冲，避免按钮紧贴宿主边缘。
        /// </summary>
        internal static float GetInspectorRightGutter(float availableWidth)
        {
            if (availableWidth <= 0f)
                return 0f;
            if (availableWidth < 190f)
                return 4f;
            if (availableWidth < 300f)
                return 8f;
            if (availableWidth < 460f)
                return 12f;
            return 18f;
        }

        internal static float GetCornerRadius(ESCornerRadiusToken token)
        {
            if (token == ESCornerRadiusToken.Pill)
                return 999f;

            float baseRadius;
            switch (token)
            {
                case ESCornerRadiusToken.Control: baseRadius = 4f; break;
                case ESCornerRadiusToken.Card: baseRadius = 6f; break;
                case ESCornerRadiusToken.Section: baseRadius = 8f; break;
                case ESCornerRadiusToken.Overlay: baseRadius = 11f; break;
                default: baseRadius = 0f; break;
            }
            return Mathf.Max(0f, Mathf.Round(baseRadius * Density));
        }

        internal static void ApplyCornerRadius(VisualElement element, ESCornerRadiusToken token)
        {
            ApplyCornerRadius(element, token, ESCornerMask.All);
        }

        internal static void ApplyCornerRadius(
            VisualElement element,
            ESCornerRadiusToken token,
            ESCornerMask mask)
        {
            if (element == null)
                return;

            float radius = GetCornerRadius(token);
            element.style.borderTopLeftRadius = (mask & ESCornerMask.TopLeft) != 0 ? radius : 0f;
            element.style.borderTopRightRadius = (mask & ESCornerMask.TopRight) != 0 ? radius : 0f;
            element.style.borderBottomLeftRadius = (mask & ESCornerMask.BottomLeft) != 0 ? radius : 0f;
            element.style.borderBottomRightRadius = (mask & ESCornerMask.BottomRight) != 0 ? radius : 0f;
        }

        internal static void ApplyBorder(VisualElement element, Color color, float width = 1f)
        {
            if (element == null)
                return;

            float safeWidth = Mathf.Max(0f, width);
            element.style.borderLeftWidth = safeWidth;
            element.style.borderRightWidth = safeWidth;
            element.style.borderTopWidth = safeWidth;
            element.style.borderBottomWidth = safeWidth;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        internal static void ApplyRoundedSurface(
            VisualElement element,
            Color background,
            ESCornerRadiusToken radius,
            Color border,
            float borderWidth = 1f)
        {
            if (element == null)
                return;

            element.style.backgroundColor = background;
            ApplyCornerRadius(element, radius);
            ApplyBorder(element, border, borderWidth);
        }

        private static RectOffset CreateNineSlice(ESCornerRadiusToken token)
        {
            int slice = Mathf.Max(2, Mathf.CeilToInt(GetCornerRadius(token)) + 2);
            return new RectOffset(slice, slice, slice, slice);
        }

        private static Texture2D CreateRoundedRectTexture(
            string textureName,
            ESCornerRadiusToken token,
            Color borderColor,
            Color fillColor)
        {
            EnsureSkinCleanupRegistered();
            float requestedRadius = Mathf.Max(1f, GetCornerRadius(token));
            int size = Mathf.Max(16, Mathf.CeilToInt(requestedRadius * 2f + 8f));
            float radius = Mathf.Min(requestedRadius, size * 0.5f - 1f);
            const float borderWidth = 1.25f;

            // Keep the constructor version-neutral. Unity versions differ in the
            // TextureFormat/creation-flags overloads available to editor assemblies.
            var texture = new Texture2D(size, size)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            try
            {
                var pixels = new Color[size * size];
                float half = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    float py = y + 0.5f - half;
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f - half;
                        float qx = Mathf.Abs(px) - (half - radius);
                        float qy = Mathf.Abs(py) - (half - radius);
                        float outsideX = Mathf.Max(qx, 0f);
                        float outsideY = Mathf.Max(qy, 0f);
                        float signedDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                                               + Mathf.Min(Mathf.Max(qx, qy), 0f)
                                               - radius;
                        float outerCoverage = Mathf.Clamp01(0.5f - signedDistance);
                        float innerCoverage = Mathf.Clamp01(0.5f - (signedDistance + borderWidth));
                        Color pixel = Color.Lerp(borderColor, fillColor, innerCoverage);
                        pixel.a *= outerCoverage;
                        pixels[y * size + x] = pixel;
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static void ApplyButtonState(
            GUIStyle style,
            Texture2D normal,
            Texture2D hover,
            Texture2D active,
            Color normalText,
            Color activeText)
        {
            ApplyStyleState(style.normal, normal, normalText);
            ApplyStyleState(style.hover, hover, normalText);
            ApplyStyleState(style.focused, hover, normalText);
            ApplyStyleState(style.active, active, activeText);
            ApplyStyleState(style.onNormal, active, activeText);
            ApplyStyleState(style.onHover, active, activeText);
            ApplyStyleState(style.onFocused, active, activeText);
            ApplyStyleState(style.onActive, active, activeText);
        }

        private static void ApplyStyleState(GUIStyleState state, Texture2D background, Color textColor)
        {
            state.background = background;
            state.textColor = textColor;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
                return;
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        private static void EnsureSkinCleanupRegistered()
        {
            if (skinCleanupRegistered)
                return;
            skinCleanupRegistered = true;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSkinResources;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseSkinResources;
            EditorApplication.quitting -= ReleaseSkinResources;
            EditorApplication.quitting += ReleaseSkinResources;
        }

        private static void ReleaseSkinResources()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSkinResources;
            EditorApplication.quitting -= ReleaseSkinResources;
            skinCleanupRegistered = false;
            surfaceStyle = null;
            compactCollectionBodyStyle = null;
            toolbarStyle = null;
            toolbarButtonStyle = null;
            primaryButtonStyle = null;
            DestroyTexture(ref surfaceTexture);
            DestroyTexture(ref toolbarTexture);
            DestroyTexture(ref toolbarButtonTexture);
            DestroyTexture(ref toolbarButtonHoverTexture);
            DestroyTexture(ref toolbarButtonActiveTexture);
            DestroyTexture(ref primaryButtonTexture);
            DestroyTexture(ref primaryButtonHoverTexture);
            DestroyTexture(ref primaryButtonActiveTexture);
            DestroyTexture(ref compactCollectionBodyTexture);
        }

        private static void EnsureSkin()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (skinInitialized && cachedProSkin == proSkin)
                return;

            cachedProSkin = proSkin;
            skinInitialized = true;
            skinGeneration++;
            surfaceStyle = null;
            headerStyle = null;
            subtitleStyle = null;
            metaStyle = null;
            compactCollectionTitleStyle = null;
            compactCollectionMetaStyle = null;
            compactCollectionBodyStyle = null;
            toolbarStyle = null;
            toolbarButtonStyle = null;
            primaryButtonStyle = null;

            DestroyTexture(ref surfaceTexture);
            DestroyTexture(ref toolbarTexture);
            DestroyTexture(ref toolbarButtonTexture);
            DestroyTexture(ref toolbarButtonHoverTexture);
            DestroyTexture(ref toolbarButtonActiveTexture);
            DestroyTexture(ref primaryButtonTexture);
            DestroyTexture(ref primaryButtonHoverTexture);
            DestroyTexture(ref primaryButtonActiveTexture);
            DestroyTexture(ref compactCollectionBodyTexture);
        }
    }

    /// <summary>只在控件自身事件上解析交互态，不订阅全局更新。</summary>
    internal sealed class ESPresentationButton : Button
    {
        private readonly ESEditorPresentation.ESPresentationRole role;
        private ESEditorPresentation.ESPresentationState semanticState;
        private bool hovered;
        private bool pressed;
        private bool focused;

        internal ESPresentationButton(
            Action action,
            ESEditorPresentation.ESPresentationRole role,
            ESEditorPresentation.ESPresentationState state =
                ESEditorPresentation.ESPresentationState.Normal)
            : base(action)
        {
            this.role = role;
            semanticState = state;
            RegisterCallback<MouseEnterEvent>(_ =>
            {
                hovered = true;
                RefreshPresentationStyle();
            });
            RegisterCallback<MouseLeaveEvent>(_ =>
            {
                hovered = false;
                pressed = false;
                RefreshPresentationStyle();
            });
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                pressed = true;
                RefreshPresentationStyle();
            });
            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                pressed = false;
                RefreshPresentationStyle();
            });
            RegisterCallback<MouseCaptureOutEvent>(_ =>
            {
                pressed = false;
                RefreshPresentationStyle();
            });
            RegisterCallback<FocusInEvent>(_ =>
            {
                focused = true;
                RefreshPresentationStyle();
            });
            RegisterCallback<FocusOutEvent>(_ =>
            {
                focused = false;
                pressed = false;
                RefreshPresentationStyle();
            });
            RefreshPresentationStyle();
        }

        internal void SetPresentationState(ESEditorPresentation.ESPresentationState state)
        {
            semanticState = state;
            RefreshPresentationStyle();
        }

        internal void RefreshPresentationStyle()
        {
            bool enabled = enabledInHierarchy;
            if (!enabled)
                pressed = false;
            ESEditorPresentation.ESPresentationState state = enabled
                ? semanticState
                : ESEditorPresentation.ESPresentationState.Disabled;
            ESEditorPresentation.ESPresentationInteraction interaction = enabled
                ? pressed
                    ? ESEditorPresentation.ESPresentationInteraction.Pressed
                    : hovered
                        ? ESEditorPresentation.ESPresentationInteraction.Hover
                        : focused
                            ? ESEditorPresentation.ESPresentationInteraction.Focused
                            : ESEditorPresentation.ESPresentationInteraction.Rest
                : ESEditorPresentation.ESPresentationInteraction.Rest;
            ESEditorPresentation.ApplyPresentationStyle(
                this,
                role,
                state,
                interaction,
                ESEditorPresentation.ESCornerRadiusToken.Control);
        }
    }

    /// <summary>
    /// Small UI Toolkit shell shared by ES windows. It intentionally owns only presentation
    /// elements; the content area remains under the caller's scroll and data lifecycle.
    /// </summary>
    internal sealed class ESWindowShell
    {
        internal readonly VisualElement Root;
        internal readonly VisualElement Header;
        internal readonly VisualElement HeaderToolbar;
        internal readonly VisualElement Toolbar;
        internal readonly VisualElement Content;
        internal readonly VisualElement StatusBar;
        internal readonly Label StatusLabel;

        private ESStatusKind status;
        internal ESWindowShell(
            string title,
            string subtitle,
            bool animateOnAttach = true,
            Texture titleIcon = null)
        {
            Root = new VisualElement { name = "ESWindowShell" };
            Root.AddToClassList("es-window-surface");
            Root.style.flexGrow = 1f;
            Root.style.flexShrink = 1f;
            Root.style.minWidth = 0f;
            Root.style.minHeight = 0f;
            Root.style.flexDirection = FlexDirection.Column;
            ESEditorPresentation.ApplyPresentationStyle(
                Root,
                ESEditorPresentation.ESPresentationRole.WindowSurface,
                radius: ESEditorPresentation.ESCornerRadiusToken.Overlay,
                borderWidth: 0f);
            Root.style.overflow = Overflow.Hidden;
            Root.style.transformOrigin = new TransformOrigin(
                Length.Percent(50f),
                Length.Percent(50f),
                0f);

            Header = new VisualElement { name = "ESWindowHeader" };
            Header.AddToClassList("es-window-header");
            Header.style.flexShrink = 0f;
            Header.style.minWidth = 0f;
            Header.style.paddingLeft = 14f;
            Header.style.paddingRight = 14f;
            Header.style.paddingTop = 10f;
            Header.style.paddingBottom = 8f;
            ESEditorPresentation.ApplyPresentationStyle(
                Header,
                ESEditorPresentation.ESPresentationRole.RaisedSurface,
                radius: ESEditorPresentation.ESCornerRadiusToken.Section,
                borderWidth: 0f);
            Header.style.borderBottomWidth = 1f;
            Header.style.borderBottomColor = ESEditorPresentation.DividerColor;

            VisualElement titleRow = new VisualElement { name = "ESWindowTitleRow" };
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.flexWrap = Wrap.Wrap;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.minWidth = 0f;
            titleRow.style.width = Length.Percent(100f);
            Label titleLabel = new Label(title ?? "ES 窗口") { name = "ESWindowTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.flexGrow = 1f;
            titleLabel.style.minWidth = 0f;
            titleLabel.style.fontSize = 15f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.SectionSelectedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            if (titleIcon != null)
            {
                Image image = new Image
                {
                    name = "ESWindowTitleIcon",
                    image = titleIcon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                };
                image.style.width = 20f;
                image.style.height = 20f;
                image.style.marginRight = 7f;
                image.style.flexShrink = 0f;
                titleRow.Add(image);
            }
            titleRow.Add(titleLabel);

            HeaderToolbar = new VisualElement { name = "ESWindowHeaderToolbar" };
            HeaderToolbar.AddToClassList("es-window-header-actions");
            HeaderToolbar.style.flexShrink = 1f;
            HeaderToolbar.style.flexGrow = 0f;
            HeaderToolbar.style.flexDirection = FlexDirection.Row;
            HeaderToolbar.style.flexWrap = Wrap.Wrap;
            HeaderToolbar.style.alignItems = Align.Center;
            HeaderToolbar.style.justifyContent = Justify.FlexEnd;
            HeaderToolbar.style.marginLeft = 10f;
            HeaderToolbar.style.minHeight = 26f;
            HeaderToolbar.style.minWidth = 0f;
            HeaderToolbar.style.maxWidth = Length.Percent(100f);
            HeaderToolbar.style.overflow = Overflow.Visible;
            titleRow.Add(HeaderToolbar);
            Header.Add(titleRow);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                Label subtitleLabel = new Label(subtitle.Trim()) { name = "ESWindowSubtitle" };
                subtitleLabel.style.marginTop = 2f;
                subtitleLabel.style.fontSize = 10f;
                subtitleLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
                subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                subtitleLabel.style.overflow = Overflow.Hidden;
                subtitleLabel.style.textOverflow = TextOverflow.Ellipsis;
                Header.Add(subtitleLabel);
            }

            Root.Add(Header);

            Toolbar = new VisualElement { name = "ESWindowToolbar" };
            Toolbar.AddToClassList("es-window-toolbar");
            Toolbar.style.flexShrink = 0f;
            Toolbar.style.minWidth = 0f;
            Toolbar.style.flexDirection = FlexDirection.Row;
            Toolbar.style.flexWrap = Wrap.Wrap;
            Toolbar.style.alignItems = Align.Center;
            Toolbar.style.paddingLeft = 10f;
            Toolbar.style.paddingRight = 10f;
            Toolbar.style.paddingTop = 5f;
            Toolbar.style.paddingBottom = 5f;
            ESEditorPresentation.ApplyPresentationStyle(
                Toolbar,
                ESEditorPresentation.ESPresentationRole.Toolbar,
                radius: ESEditorPresentation.ESCornerRadiusToken.Card,
                borderWidth: 0f);
            Toolbar.style.borderBottomWidth = 1f;
            Toolbar.style.borderBottomColor = ESEditorPresentation.DividerColor;
            Root.Add(Toolbar);

            Content = new VisualElement { name = "ESWindowContent" };
            Content.AddToClassList("es-window-content");
            Content.style.flexGrow = 1f;
            Content.style.flexShrink = 1f;
            Content.style.minWidth = 0f;
            Content.style.minHeight = 0f;
            ESEditorPresentation.ApplyPresentationStyle(
                Content,
                ESEditorPresentation.ESPresentationRole.CanvasSurface,
                radius: ESEditorPresentation.ESCornerRadiusToken.Card,
                borderWidth: 0f);
            Root.Add(Content);

            StatusBar = new VisualElement { name = "ESWindowStatusBar" };
            StatusBar.AddToClassList("es-window-status");
            StatusBar.style.flexShrink = 0f;
            StatusBar.style.minWidth = 0f;
            StatusBar.style.flexDirection = FlexDirection.Row;
            StatusBar.style.alignItems = Align.Center;
            StatusBar.style.minHeight = 24f;
            StatusBar.style.paddingLeft = 10f;
            StatusBar.style.paddingRight = 10f;
            ESEditorPresentation.ApplyPresentationStyle(
                StatusBar,
                ESEditorPresentation.ESPresentationRole.Status,
                radius: ESEditorPresentation.ESCornerRadiusToken.Section,
                borderWidth: 0f);
            StatusBar.style.borderTopWidth = 1f;
            StatusBar.style.borderTopColor = ESEditorPresentation.DividerColor;
            StatusLabel = new Label { name = "ESWindowStatus" };
            StatusLabel.style.flexGrow = 1f;
            StatusLabel.style.minWidth = 0f;
            StatusLabel.style.fontSize = 10f;
            StatusBar.Add(StatusLabel);
            Root.Add(StatusBar);
            SetStatus("就绪", ESStatusKind.Ready);
        }

        internal void ApplyCompactHostChrome()
        {
            Label title = Header.Q<Label>("ESWindowTitle");
            if (title != null) title.style.display = DisplayStyle.None;
            Label subtitle = Header.Q<Label>("ESWindowSubtitle");
            if (subtitle != null) subtitle.style.display = DisplayStyle.None;
            VisualElement titleRow = Header.Q<VisualElement>("ESWindowTitleRow");
            if (titleRow != null)
            {
                titleRow.style.flexGrow = 1f;
                titleRow.style.width = Length.Percent(100f);
                titleRow.style.minHeight = 24f;
                titleRow.style.justifyContent = Justify.FlexEnd;
            }

            Header.style.paddingLeft = 6f;
            Header.style.paddingRight = 6f;
            Header.style.paddingTop = 3f;
            Header.style.paddingBottom = 3f;
            Header.style.minHeight = 30f;
            Header.style.maxHeight = StyleKeyword.None;
            HeaderToolbar.style.minHeight = 24f;
            HeaderToolbar.style.marginLeft = 0f;
            Toolbar.style.display = DisplayStyle.None;
            StatusBar.style.display = DisplayStyle.None;
        }

        internal void SetStatus(string message, ESStatusKind nextStatus)
        {
            status = nextStatus;
            StatusLabel.text = string.IsNullOrWhiteSpace(message) ? "就绪" : message.Trim();
            ESEditorPresentation.ESPresentationStyle style =
                ESEditorPresentation.ResolvePresentationStyle(
                    ESEditorPresentation.ESPresentationRole.Status,
                    ESEditorPresentation.GetPresentationState(status));
            Color accent = ESEditorPresentation.GetStatusAccent(0, status);
            StatusBar.style.backgroundColor = style.BackgroundColor;
            StatusBar.style.opacity = style.Opacity;
            StatusLabel.style.color = accent;
            StatusBar.style.borderTopColor = ESEditorPresentation.DividerColor;
            StatusBar.style.borderLeftWidth = status == ESStatusKind.Error || status == ESStatusKind.Warning ? 3f : 0f;
            StatusBar.style.borderLeftColor = accent;
        }
    }

    internal static class ESWindowPresentation
    {
        internal static Button CreateToolbarButton(string text, string tooltip, Action action, bool primary = false)
        {
            var button = new ESPresentationButton(
                action,
                primary
                    ? ESEditorPresentation.ESPresentationRole.PrimaryAction
                    : ESEditorPresentation.ESPresentationRole.Control)
            {
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList("es-window-toolbar-button");
            if (primary)
                button.AddToClassList("primary");
            button.style.minHeight = 24f;
            button.style.marginRight = 4f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.RefreshPresentationStyle();
            return button;
        }

        internal static Button CreateHeaderIconButton(string symbol, string tooltip, Action action)
        {
            var button = new ESPresentationButton(
                action,
                ESEditorPresentation.ESPresentationRole.Control)
            {
                text = symbol ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList("es-window-header-icon-button");
            button.style.width = 26f;
            button.style.minWidth = 26f;
            button.style.height = 26f;
            button.style.minHeight = 26f;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.fontSize = 14f;
            button.RefreshPresentationStyle();
            return button;
        }

        internal static Button CreateHeaderActionButton(
            Texture icon,
            string text,
            string tooltip,
            Action action)
        {
            var button = new ESPresentationButton(
                action,
                ESEditorPresentation.ESPresentationRole.Control)
            {
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList("es-window-header-action-button");
            button.style.height = 26f;
            button.style.minHeight = 26f;
            button.style.minWidth = string.IsNullOrEmpty(text) ? 26f : 32f;
            button.style.flexShrink = 1f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 6f;
            button.style.paddingRight = 6f;

            if (icon != null)
            {
                Image image = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = 15f;
                image.style.height = 15f;
                image.style.flexShrink = 0f;
                button.Add(image);
            }

            if (!string.IsNullOrEmpty(text))
            {
                Label label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.style.marginLeft = icon == null ? 0f : 4f;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.minWidth = 0f;
                label.style.flexShrink = 1f;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.style.color = ESEditorPresentation.SectionSelectedTextColor;
                button.Add(label);
            }
            button.RefreshPresentationStyle();
            return button;
        }

        internal static ToolbarMenu CreateHeaderOverflowMenu(
            string name,
            string text,
            string tooltip,
            float minWidth = 48f,
            float maxWidth = 0f)
        {
            var menu = new ToolbarMenu
            {
                name = name ?? string.Empty,
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            menu.AddToClassList("es-window-header-action-button");
            menu.style.height = 26f;
            menu.style.minHeight = 26f;
            menu.style.minWidth = Mathf.Max(26f, minWidth);
            if (maxWidth > 0f)
                menu.style.maxWidth = Mathf.Max(minWidth, maxWidth);
            menu.style.marginLeft = 2f;
            menu.style.paddingLeft = 6f;
            menu.style.paddingRight = 4f;
            menu.style.color = ESEditorPresentation.SectionSelectedTextColor;
            ESEditorPresentation.ApplyRoundedSurface(
                menu,
                ESEditorPresentation.ControlSurfaceColor,
                ESEditorPresentation.ESCornerRadiusToken.Control,
                ESEditorPresentation.DividerColor);
            return menu;
        }

        internal static void SetButtonPresentationState(
            Button button,
            ESEditorPresentation.ESPresentationState state)
        {
            if (button is ESPresentationButton presentationButton)
            {
                presentationButton.SetPresentationState(state);
                return;
            }

            ESEditorPresentation.ApplyPresentationStyle(
                button,
                ESEditorPresentation.ESPresentationRole.Control,
                state,
                radius: ESEditorPresentation.ESCornerRadiusToken.Control);
        }

        internal static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.SetEnabled(enabled);
            if (button is ESPresentationButton presentationButton)
                presentationButton.RefreshPresentationStyle();
        }

        internal static void SetElementEnabled(VisualElement element, bool enabled)
        {
            if (element == null)
                return;

            element.SetEnabled(enabled);
            if (element is ESPresentationButton presentationButton)
                presentationButton.RefreshPresentationStyle();
            element.Query<ESPresentationButton>().ForEach(button =>
                button.RefreshPresentationStyle());
        }

        internal static VisualElement CreateEmptyState(string title, string detail, string actionText, Action action)
        {
            VisualElement empty = new VisualElement { name = "ESEmptyState" };
            empty.AddToClassList("es-empty-state");
            empty.style.flexGrow = 1f;
            empty.style.alignItems = Align.Center;
            empty.style.justifyContent = Justify.Center;
            empty.style.paddingLeft = 24f;
            empty.style.paddingRight = 24f;
            empty.style.paddingTop = 24f;
            empty.style.paddingBottom = 24f;
            empty.style.marginLeft = 12f;
            empty.style.marginRight = 12f;
            empty.style.marginTop = 12f;
            empty.style.marginBottom = 12f;
            ESEditorPresentation.ApplyRoundedSurface(
                empty,
                ESEditorPresentation.CanvasSurfaceColor,
                ESEditorPresentation.ESCornerRadiusToken.Section,
                ESEditorPresentation.DividerColor);
            Label titleLabel = new Label(title ?? "暂无内容") { name = "ESEmptyStateTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.SectionSelectedTextColor;
            empty.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Label detailLabel = new Label(detail.Trim()) { name = "ESEmptyStateDetail" };
                detailLabel.style.marginTop = 5f;
                detailLabel.style.color = ESEditorPresentation.EmptyTextColor;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.Add(detailLabel);
            }
            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                Button actionButton = CreateToolbarButton(actionText, actionText, action, true);
                actionButton.style.marginTop = 12f;
                empty.Add(actionButton);
            }
            return empty;
        }

        internal static VisualElement CreateErrorState(
            string title,
            string cause,
            string impact,
            string recovery,
            string actionText,
            Action action)
        {
            VisualElement error = new VisualElement { name = "ESErrorState" };
            error.AddToClassList("es-error-state");
            error.style.flexGrow = 1f;
            error.style.alignItems = Align.Center;
            error.style.justifyContent = Justify.Center;
            error.style.paddingLeft = 28f;
            error.style.paddingRight = 28f;
            error.style.paddingTop = 24f;
            error.style.paddingBottom = 24f;
            error.style.marginLeft = 12f;
            error.style.marginRight = 12f;
            error.style.marginTop = 12f;
            error.style.marginBottom = 12f;
            ESEditorPresentation.ApplyRoundedSurface(
                error,
                ESEditorPresentation.CanvasSurfaceColor,
                ESEditorPresentation.ESCornerRadiusToken.Section,
                ESEditorPresentation.DividerColor);

            Label titleLabel = new Label(title ?? "操作失败") { name = "ESErrorStateTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.ErrorColor;
            error.Add(titleLabel);

            AddErrorLine(error, "原因", cause);
            AddErrorLine(error, "影响", impact);
            AddErrorLine(error, "恢复", recovery);

            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                Button actionButton = CreateToolbarButton(actionText, actionText, action, true);
                actionButton.style.marginTop = 12f;
                error.Add(actionButton);
            }
            return error;
        }

        private static void AddErrorLine(VisualElement parent, string label, string value)
        {
            if (parent == null || string.IsNullOrWhiteSpace(value))
                return;

            VisualElement line = new VisualElement { name = "ESErrorStateLine" };
            line.style.flexDirection = FlexDirection.Row;
            line.style.maxWidth = 640f;
            line.style.marginTop = 6f;

            Label key = new Label(label + "：") { name = "ESErrorStateKey" };
            key.style.width = 42f;
            key.style.flexShrink = 0f;
            key.style.color = ESEditorPresentation.SectionSelectedTextColor;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            line.Add(key);

            Label detail = new Label(value.Trim()) { name = "ESErrorStateDetail" };
            detail.style.flexGrow = 1f;
            detail.style.minWidth = 0f;
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.color = ESEditorPresentation.EmptyTextColor;
            line.Add(detail);
            parent.Add(line);
        }

        internal static void ApplySemanticTheme(VisualElement root)
        {
            if (root == null)
                return;

            ESEditorPresentation.ApplyPresentationStyle(
                root,
                ESEditorPresentation.ESPresentationRole.WindowSurface,
                borderWidth: 0f);
            StyleClass(root, "es-agent-header",
                ESEditorPresentation.ESPresentationRole.RaisedSurface,
                ESEditorPresentation.ESCornerRadiusToken.None);
            StyleClass(root, "es-agent-sidebar",
                ESEditorPresentation.ESPresentationRole.InsetSurface,
                ESEditorPresentation.ESCornerRadiusToken.None);
            StyleClass(root, "es-agent-conversation",
                ESEditorPresentation.ESPresentationRole.WindowSurface,
                ESEditorPresentation.ESCornerRadiusToken.None);
            StyleClass(root, "es-agent-context-panel",
                ESEditorPresentation.ESPresentationRole.RaisedSurface,
                ESEditorPresentation.ESCornerRadiusToken.Section);
            StyleClass(root, "es-agent-composer-shell",
                ESEditorPresentation.ESPresentationRole.InsetSurface,
                ESEditorPresentation.ESCornerRadiusToken.Card);
            StyleClass(root, "es-agent-empty",
                ESEditorPresentation.ESPresentationRole.CanvasSurface,
                ESEditorPresentation.ESCornerRadiusToken.Section);
            StyleClass(root, "es-agent-header-button",
                ESEditorPresentation.ESPresentationRole.Control,
                ESEditorPresentation.ESCornerRadiusToken.Control);
            StyleClass(root, "es-agent-secondary-button",
                ESEditorPresentation.ESPresentationRole.Control,
                ESEditorPresentation.ESCornerRadiusToken.Control);

            VisualElement brandMark = FindClass(root, "es-agent-brand-mark");
            if (brandMark != null)
            {
                ESEditorPresentation.ApplyPresentationStyle(
                    brandMark,
                    ESEditorPresentation.ESPresentationRole.RaisedSurface,
                    ESEditorPresentation.ESPresentationState.Selected,
                    radius: ESEditorPresentation.ESCornerRadiusToken.Card,
                    borderWidth: null);
            }

            root.Query<VisualElement>(className: "es-agent-primary-button").ForEach(primary =>
            {
                ESEditorPresentation.ApplyPresentationStyle(
                    primary,
                    ESEditorPresentation.ESPresentationRole.PrimaryAction,
                    radius: ESEditorPresentation.ESCornerRadiusToken.Control,
                    borderWidth: null);
            });

            root.Query<VisualElement>(className: "es-agent-link-button").ForEach(link =>
                link.style.color = ESEditorPresentation.SectionSelectedTextColor);

            VisualElement composer = FindClass(root, "es-agent-composer");
            if (composer != null)
            {
                SetBorderColor(composer, ESEditorPresentation.DividerColor);
                ESEditorPresentation.ApplyCornerRadius(
                    composer, ESEditorPresentation.ESCornerRadiusToken.Card);
            }

            root.Query<ESPresentationButton>().ForEach(button =>
                button.RefreshPresentationStyle());
        }

        internal static void StyleStatusPill(VisualElement pill, ESStatusKind status)
        {
            if (pill == null)
                return;
            ESEditorPresentation.ApplyPresentationStyle(
                pill,
                ESEditorPresentation.ESPresentationRole.Status,
                ESEditorPresentation.GetPresentationState(status),
                radius: ESEditorPresentation.ESCornerRadiusToken.Pill);
            Color accent = ESEditorPresentation.GetStatusAccent(0, status);
            pill.style.color = accent;
            ESEditorPresentation.ApplyBorder(pill, accent);
        }

        private static VisualElement FindClass(VisualElement root, string className)
        {
            return root?.Q<VisualElement>(className: className);
        }

        private static void StyleClass(
            VisualElement root,
            string className,
            ESEditorPresentation.ESPresentationRole role,
            ESEditorPresentation.ESCornerRadiusToken radius)
        {
            root.Query<VisualElement>(className: className).ForEach(element =>
            {
                ESEditorPresentation.ApplyPresentationStyle(
                    element,
                    role,
                    radius: radius,
                    borderWidth: null);
            });
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }
    }

    /// <summary>
    /// Reversible Unity 2022.3 deep-skin layer. The project theme explicitly opts in; application
    /// performs one bounded EditorStyles reflection pass and one enumeration of open EditorWindow
    /// instances. No asset scan, global window polling or per-frame skin work is used.
    /// </summary>
    internal static class ESGlobalEditorSkinExperiment
    {
        private enum StyleRole
        {
            None,
            Text,
            InteractiveText,
            Toolbar,
            Button,
            Input,
            Header,
            Help,
            Selection
        }

        private enum SkinTone
        {
            Surface,
            Raised,
            Hover,
            Active,
            Focused,
            Input,
            Toolbar,
            Help
        }

        private sealed class StateSnapshot
        {
            public GUIStyleState state;
            public Color textColor;
            public Texture2D background;
            public Texture2D[] scaledBackgrounds;
        }

        private sealed class StyleSnapshot
        {
            public GUIStyle style;
            public StateSnapshot[] states;
        }

        private sealed class RootSnapshot
        {
            public VisualElement root;
        }

        private sealed class SourcePixels
        {
            public Color32[] pixels;
        }

        private const string GlobalStyleSheetPath =
            "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESGlobalEditorDeepSkin.uss";
        private const string RootClass = "es-global-editor-skin";
        private const string DarkRootClass = "es-global-editor-skin--dark";
        private const string LightRootClass = "es-global-editor-skin--light";
        private const int MaxTintTexturePixels = 262144;
        private const int MaxCreatedTextureCount = 64;
        private const long MaxCreatedTextureBytes = 16L * 1024L * 1024L;
        private const int MaxEditorStylesInitializationRetries = 8;

        private static readonly List<StyleSnapshot> snapshots = new List<StyleSnapshot>(96);
        private static readonly HashSet<GUIStyle> styledStyles = new HashSet<GUIStyle>();
        private static readonly List<RootSnapshot> rootSnapshots = new List<RootSnapshot>(32);
        private static readonly HashSet<VisualElement> styledRoots = new HashSet<VisualElement>();
        private static readonly Dictionary<long, Texture2D> themedTextureCache =
            new Dictionary<long, Texture2D>(96);
        private static readonly List<Texture2D> createdTextures = new List<Texture2D>(96);
        private static readonly Dictionary<int, SourcePixels> sourcePixelsCache =
            new Dictionary<int, SourcePixels>(32);
        private static readonly Dictionary<Type, FieldInfo[]> styleFieldsByType =
            new Dictionary<Type, FieldInfo[]>(2);
        private static readonly FieldInfo currentEditorStylesField = typeof(EditorStyles).GetField(
            "s_Current",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static long createdTextureBytes;
        private static bool applied;
        private static object appliedEditorStyles;
        private static bool appliedProSkin;
        private static bool editorStylesInitializationPending;
        private static bool initializationRetryQueued;
        private static bool rootRefreshQueued;
        private static int initializationRetryCount;
        private static StyleSheet globalStyleSheet;

        public static bool IsApplied => applied;
        public static int StyledWindowCount => rootSnapshots.Count;

        public static bool TryApply(out string message)
        {
            if (applied)
            {
                QueueOpenWindowRootRefresh();
                message = BuildAppliedMessage();
                return true;
            }

            if (Application.isBatchMode)
            {
                message = "BatchMode 不加载 ES 深度皮肤，未改变 Unity 原生样式。";
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "PlayMode 中不会启用 ES 深度皮肤，请返回 EditMode 后重试。";
                return false;
            }

            if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal))
            {
                message = "当前 Unity 版本不是 2022.3，深度皮肤已拒绝运行。";
                return false;
            }

            snapshots.Clear();
            styledStyles.Clear();
            rootSnapshots.Clear();
            styledRoots.Clear();
            themedTextureCache.Clear();
            DestroyCreatedTextures();

            if (!TryGetCurrentEditorStyles(out object currentStyles, out message))
            {
                if (editorStylesInitializationPending)
                    QueueInitializationRetry();
                return false;
            }

            try
            {
                ApplyEditorStyles(currentStyles);
            }
            finally
            {
                sourcePixelsCache.Clear();
            }
            RefreshOpenWindowRoots();
            if (snapshots.Count == 0 && rootSnapshots.Count == 0)
            {
                Restore();
                message = "没有找到可安全调整的 Unity 编辑器表面，未改变原生样式。";
                return false;
            }

            applied = true;
            appliedEditorStyles = currentStyles;
            appliedProSkin = EditorGUIUtility.isProSkin;
            ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            CancelInitializationRetry();
            QueueOpenWindowRootRefresh();
            InternalEditorUtility.RepaintAllViews();
            message = BuildAppliedMessage();
            return true;
        }

        public static void Restore()
        {
            RestoreInternal(true, true);
        }

        private static void RestoreInternal(bool notifyPresentation, bool repaintAllViews)
        {
            bool wasApplied = applied;
            bool hadState = wasApplied || snapshots.Count > 0 || rootSnapshots.Count > 0
                || createdTextures.Count > 0;
            CancelInitializationRetry();
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            rootRefreshQueued = false;

            for (int i = 0; i < snapshots.Count; i++)
                RestoreStyle(snapshots[i]);

            for (int i = 0; i < rootSnapshots.Count; i++)
                RestoreRoot(rootSnapshots[i]);

            snapshots.Clear();
            styledStyles.Clear();
            rootSnapshots.Clear();
            styledRoots.Clear();
            themedTextureCache.Clear();
            sourcePixelsCache.Clear();
            DestroyCreatedTextures();
            globalStyleSheet = null;
            applied = false;
            appliedEditorStyles = null;
            if (wasApplied && notifyPresentation)
                ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            if (repaintAllViews && hadState)
                InternalEditorUtility.RepaintAllViews();
        }

        public static void Synchronize(bool shouldApply)
        {
            if (!shouldApply || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            {
                Restore();
                return;
            }

            if (applied)
            {
                QueueOpenWindowRootRefresh();
                return;
            }

            TryApply(out _);
        }

        public static bool Refresh(out string message)
        {
            if (!applied)
                return TryApply(out message);
            if (!TryGetCurrentEditorStyles(out object currentStyles, out message))
            {
                if (editorStylesInitializationPending)
                    QueueInitializationRetry();
                return false;
            }

            if (ReferenceEquals(appliedEditorStyles, currentStyles)
                && appliedProSkin == EditorGUIUtility.isProSkin)
            {
                RefreshOpenWindowRoots();
                InternalEditorUtility.RepaintAllViews();
                message = BuildAppliedMessage() + " 本次仅增量同步窗口，未重建 IMGUI 纹理。";
                return true;
            }

            RestoreInternal(false, false);
            return TryApply(out message);
        }

        private static bool TryGetCurrentEditorStyles(out object currentStyles, out string message)
        {
            editorStylesInitializationPending = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 正在编译或导入资源，ES 全局皮肤将在 Editor 空闲后重试。";
                return false;
            }

            // Force common lazy properties to initialize before the bounded field pass.
            GUIStyle currentLabel;
            GUIStyle currentToolbar;
            GUIStyle currentTextField;
            GUIStyle currentButton;
            GUIStyle currentHelpBox;
            try
            {
                currentLabel = EditorStyles.label;
                currentToolbar = EditorStyles.toolbar;
                currentTextField = EditorStyles.textField;
                currentButton = EditorStyles.miniButton;
                currentHelpBox = EditorStyles.helpBox;
            }
            catch (NullReferenceException)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity EditorStyles 正在初始化，ES 全局皮肤将在下一次 Editor 回调中重试。";
                return false;
            }
            if (currentLabel == null || currentToolbar == null || currentTextField == null
                || currentButton == null || currentHelpBox == null)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 尚未准备完成，ES 全局皮肤将延迟重试。";
                return false;
            }

            currentStyles = currentEditorStylesField?.GetValue(null);
            if (currentStyles == null)
            {
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 当前容器尚未初始化，ES 全局皮肤将延迟重试。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void QueueInitializationRetry()
        {
            if (initializationRetryQueued
                || initializationRetryCount >= MaxEditorStylesInitializationRetries
                || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.isBatchMode)
                return;

            initializationRetryQueued = true;
            initializationRetryCount++;
            EditorApplication.delayCall -= RetryInitialization;
            EditorApplication.delayCall += RetryInitialization;
        }

        private static void RetryInitialization()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;

            ESGlobalEditorTheme current = ESGlobalEditorTheme.Instance;
            bool shouldApply = current != null
                && current.enableGlobalEditorShell
                && current.enableDeepEditorSkin
                && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (!shouldApply)
            {
                CancelInitializationRetry();
                return;
            }

            TryApply(out _);
        }

        private static void CancelInitializationRetry()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;
            initializationRetryCount = 0;
            editorStylesInitializationPending = false;
        }

        private static void ApplyEditorStyles(object currentStyles)
        {
            Type stylesType = currentStyles.GetType();
            if (!styleFieldsByType.TryGetValue(stylesType, out FieldInfo[] fields))
            {
                FieldInfo[] allFields = stylesType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var supportedFields = new List<FieldInfo>(allFields.Length);
                for (int i = 0; i < allFields.Length; i++)
                {
                    FieldInfo candidate = allFields[i];
                    if (candidate.FieldType == typeof(GUIStyle) && !candidate.IsLiteral)
                        supportedFields.Add(candidate);
                }
                fields = supportedFields.ToArray();
                styleFieldsByType[stylesType] = fields;
            }
            Color normalText = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.88f, 0.92f, 1f)
                : new Color(0.15f, 0.18f, 0.22f, 1f);
            Color interactiveText = Color.Lerp(normalText, ESEditorPresentation.LogicSteelBlue,
                EditorGUIUtility.isProSkin ? 0.34f : 0.28f);
            Color selectedText = EditorGUIUtility.isProSkin
                ? new Color(0.94f, 0.97f, 1f, 1f)
                : new Color(0.06f, 0.13f, 0.19f, 1f);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                try
                {
                    GUIStyle style = field.GetValue(currentStyles) as GUIStyle;
                    if (style == null || ContainsStyle(style))
                        continue;

                    StyleRole role = ClassifyStyle(field.Name, style.name);
                    if (role == StyleRole.None)
                        continue;

                    StyleSnapshot snapshot = CaptureStyle(style);
                    snapshots.Add(snapshot);
                    styledStyles.Add(style);
                    ApplyStyle(style, role, normalText, interactiveText, selectedText);
                }
                catch
                {
                    // Unity 内部字段逐项隔离；不可访问字段不会中断其余可逆样式。
                }
            }

            // Unity 内置 Inspector/Scene GUISkin 是跨窗口共享对象。修改它会污染所有
            // Editor 页面，因此深度皮肤只处理已识别的 EditorStyles 文本语义。
        }

        private static void ApplyBuiltInSkin(
            EditorSkin editorSkin,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            GUISkin skin;
            try
            {
                skin = EditorGUIUtility.GetBuiltinSkin(editorSkin);
            }
            catch
            {
                return;
            }
            if (skin == null)
                return;

            ApplyKnownStyle(skin.label, StyleRole.Text, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.button, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.box, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.toggle, StyleRole.InteractiveText, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textField, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textArea, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.window, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);

            GUIStyle[] customStyles = skin.customStyles;
            if (customStyles == null)
                return;
            for (int i = 0; i < customStyles.Length; i++)
            {
                GUIStyle style = customStyles[i];
                if (style == null)
                    continue;
                StyleRole role = ClassifyStyle(style.name, style.name);
                ApplyKnownStyle(style, role, normalText, interactiveText, selectedText);
            }
        }

        private static void ApplyKnownStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            if (style == null || role == StyleRole.None || ContainsStyle(style))
                return;
            try
            {
                StyleSnapshot snapshot = CaptureStyle(style);
                snapshots.Add(snapshot);
                styledStyles.Add(style);
                ApplyStyle(style, role, normalText, interactiveText, selectedText);
            }
            catch
            {
                // 内置皮肤逐样式隔离，保留已捕获快照供完整恢复。
            }
        }

        private static StyleSnapshot CaptureStyle(GUIStyle style)
        {
            return new StyleSnapshot
            {
                style = style,
                states = new[]
                {
                    CaptureState(style.normal),
                    CaptureState(style.hover),
                    CaptureState(style.active),
                    CaptureState(style.focused),
                    CaptureState(style.onNormal),
                    CaptureState(style.onHover),
                    CaptureState(style.onActive),
                    CaptureState(style.onFocused)
                }
            };
        }

        private static StateSnapshot CaptureState(GUIStyleState state)
        {
            Texture2D[] scaled = state.scaledBackgrounds;
            return new StateSnapshot
            {
                state = state,
                textColor = state.textColor,
                background = state.background,
                scaledBackgrounds = scaled == null ? null : (Texture2D[])scaled.Clone()
            };
        }

        private static void ApplyStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            Color baseText = role == StyleRole.Text ? normalText : interactiveText;
            ApplyState(style.normal, baseText, GetNormalTone(role));
            ApplyState(style.hover, selectedText, SkinTone.Hover);
            ApplyState(style.active, selectedText, SkinTone.Active);
            ApplyState(style.focused, selectedText, SkinTone.Focused);
            ApplyState(style.onNormal, selectedText, SkinTone.Active);
            ApplyState(style.onHover, selectedText, SkinTone.Hover);
            ApplyState(style.onActive, selectedText, SkinTone.Active);
            ApplyState(style.onFocused, selectedText, SkinTone.Focused);
        }

        private static void ApplyState(GUIStyleState state, Color textColor, SkinTone tone)
        {
            // background == null 在 Unity IMGUI 中通常表示透明宿主表面，不能替换为
            // 不透明纯色纹理。仅对已有背景保留透明度与形状后做 ES 色调染色。
            state.textColor = textColor;
            if (state.background == null)
                return;

            state.background = GetThemedTexture(state.background, tone);
            Texture2D[] scaled = state.scaledBackgrounds;
            if (scaled == null || scaled.Length == 0)
                return;

            Texture2D[] themedScaled = new Texture2D[scaled.Length];
            for (int i = 0; i < scaled.Length; i++)
                themedScaled[i] = scaled[i] == null ? null : GetThemedTexture(scaled[i], tone);
            state.scaledBackgrounds = themedScaled;
        }

        private static SkinTone GetNormalTone(StyleRole role)
        {
            switch (role)
            {
                case StyleRole.Toolbar:
                    return SkinTone.Toolbar;
                case StyleRole.Input:
                    return SkinTone.Input;
                case StyleRole.Help:
                    return SkinTone.Help;
                case StyleRole.Button:
                case StyleRole.Header:
                    return SkinTone.Raised;
                case StyleRole.Selection:
                    return SkinTone.Active;
                default:
                    return SkinTone.Surface;
            }
        }

        private static bool ProvidesBackground(StyleRole role)
        {
            return role == StyleRole.Toolbar
                || role == StyleRole.Button
                || role == StyleRole.Input
                || role == StyleRole.Header
                || role == StyleRole.Help
                || role == StyleRole.Selection;
        }

        private static StyleRole ClassifyStyle(string fieldName, string styleName)
        {
            string name = (fieldName ?? string.Empty) + " " + (styleName ?? string.Empty);
            if (ContainsIgnoreCase(name, "toolbar"))
                return StyleRole.Toolbar;
            if (ContainsIgnoreCase(name, "helpbox") || ContainsIgnoreCase(name, "notification"))
                return StyleRole.Help;
            if (ContainsIgnoreCase(name, "textfield") || ContainsIgnoreCase(name, "textarea")
                || ContainsIgnoreCase(name, "numberfield") || ContainsIgnoreCase(name, "objectfield")
                || ContainsIgnoreCase(name, "colorfield") || ContainsIgnoreCase(name, "searchfield")
                || ContainsIgnoreCase(name, "popup") || ContainsIgnoreCase(name, "dropdown")
                || ContainsIgnoreCase(name, "layermask"))
                return StyleRole.Input;
            if (ContainsIgnoreCase(name, "button"))
                return StyleRole.Button;
            if (ContainsIgnoreCase(name, "titlebar") || ContainsIgnoreCase(name, "header"))
                return StyleRole.Header;
            if (ContainsIgnoreCase(name, "selection") || ContainsIgnoreCase(name, "selected"))
                return StyleRole.Selection;
            if (ContainsIgnoreCase(name, "foldout") || ContainsIgnoreCase(name, "toggle")
                || ContainsIgnoreCase(name, "radio"))
                return StyleRole.InteractiveText;
            if (ContainsIgnoreCase(name, "label") || ContainsIgnoreCase(name, "link"))
                return StyleRole.Text;
            return StyleRole.None;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D GetThemedTexture(Texture2D source, SkinTone tone)
        {
            if (source == null)
                return null;

            int sourceId = source.GetInstanceID();
            long key = ((long)sourceId << 8) ^ (int)tone;
            if (themedTextureCache.TryGetValue(key, out Texture2D cached))
                return cached;

            Texture2D themed = CreateTintedTexture(source, tone);
            if (themed == null)
                themed = source;
            themedTextureCache[key] = themed;
            return themed;
        }

        private static Texture2D CreateTintedTexture(Texture2D source, SkinTone tone)
        {
            long pixelCount = source == null ? 0L : (long)source.width * source.height;
            long requiredBytes = pixelCount * 4L;
            if (source == null || source.width <= 0 || source.height <= 0
                || pixelCount > MaxTintTexturePixels || !CanCreateTexture(requiredBytes))
                return source;

            Texture2D output = null;
            try
            {
                SourcePixels sourcePixels = GetSourcePixels(source);
                if (sourcePixels == null || sourcePixels.pixels == null)
                    return source;

                output = new Texture2D(source.width, source.height, UnityEngine.TextureFormat.RGBA32, false)
                {
                    name = "ES Deep Skin " + source.name + " " + tone,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                Color32[] pixels = new Color32[sourcePixels.pixels.Length];
                Color target = GetToneColor(tone);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color original = sourcePixels.pixels[i];
                    float luminance = original.r * 0.2126f + original.g * 0.7152f + original.b * 0.0722f;
                    float brightness = Mathf.Lerp(0.72f, 1.18f, luminance);
                    Color tinted = new Color(
                        Mathf.Clamp01(target.r * brightness),
                        Mathf.Clamp01(target.g * brightness),
                        Mathf.Clamp01(target.b * brightness),
                        original.a);
                    Color blended = Color.Lerp(original, tinted, 0.76f);
                    blended.a = original.a;
                    pixels[i] = blended;
                }

                output.SetPixels32(pixels);
                output.Apply(false, true);
                createdTextures.Add(output);
                createdTextureBytes += requiredBytes;
                return output;
            }
            catch
            {
                if (output != null)
                    UnityEngine.Object.DestroyImmediate(output);
                return source;
            }
        }

        private static SourcePixels GetSourcePixels(Texture2D source)
        {
            int sourceId = source.GetInstanceID();
            if (sourcePixelsCache.TryGetValue(sourceId, out SourcePixels cached))
                return cached;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readableCopy = null;
            try
            {
                Color32[] pixels;
                try
                {
                    pixels = source.GetPixels32();
                }
                catch (UnityException)
                {
                    temporary = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Default);
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readableCopy = new Texture2D(
                        source.width,
                        source.height,
                        UnityEngine.TextureFormat.RGBA32,
                        false);
                    readableCopy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                    pixels = readableCopy.GetPixels32();
                }

                var result = new SourcePixels
                {
                    pixels = pixels
                };
                sourcePixelsCache[sourceId] = result;
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readableCopy != null)
                    UnityEngine.Object.DestroyImmediate(readableCopy);
                if (temporary != null)
                    RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Color GetToneColor(SkinTone tone)
        {
            if (!EditorGUIUtility.isProSkin)
            {
                switch (tone)
                {
                    case SkinTone.Toolbar: return new Color(0.78f, 0.82f, 0.86f, 1f);
                    case SkinTone.Input: return new Color(0.86f, 0.89f, 0.92f, 1f);
                    case SkinTone.Raised: return new Color(0.80f, 0.84f, 0.88f, 1f);
                    case SkinTone.Hover: return new Color(0.63f, 0.76f, 0.86f, 1f);
                    case SkinTone.Active: return new Color(0.36f, 0.62f, 0.80f, 1f);
                    case SkinTone.Focused: return new Color(0.48f, 0.70f, 0.84f, 1f);
                    case SkinTone.Help: return new Color(0.76f, 0.83f, 0.88f, 1f);
                    default: return new Color(0.83f, 0.86f, 0.89f, 1f);
                }
            }

            switch (tone)
            {
                case SkinTone.Toolbar: return new Color(0.105f, 0.13f, 0.16f, 1f);
                case SkinTone.Input: return new Color(0.13f, 0.17f, 0.205f, 1f);
                case SkinTone.Raised: return new Color(0.17f, 0.215f, 0.255f, 1f);
                case SkinTone.Hover: return new Color(0.18f, 0.30f, 0.38f, 1f);
                case SkinTone.Active: return new Color(0.12f, 0.38f, 0.55f, 1f);
                case SkinTone.Focused: return new Color(0.14f, 0.32f, 0.44f, 1f);
                case SkinTone.Help: return new Color(0.16f, 0.22f, 0.27f, 1f);
                default: return new Color(0.135f, 0.165f, 0.195f, 1f);
            }
        }

        private static void QueueOpenWindowRootRefresh()
        {
            if (rootRefreshQueued || !applied)
                return;
            rootRefreshQueued = true;
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            EditorApplication.delayCall += RefreshOpenWindowRoots;
        }

        private static void RefreshOpenWindowRoots()
        {
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            rootRefreshQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;

            if (globalStyleSheet == null)
                globalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GlobalStyleSheetPath);
            if (globalStyleSheet == null)
                return;

            for (int i = rootSnapshots.Count - 1; i >= 0; i--)
            {
                RootSnapshot snapshot = rootSnapshots[i];
                VisualElement staleRoot = snapshot == null ? null : snapshot.root;
                if (staleRoot != null && staleRoot.panel != null)
                    continue;
                RestoreRoot(snapshot);
                if (staleRoot != null)
                    styledRoots.Remove(staleRoot);
                rootSnapshots.RemoveAt(i);
            }

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                VisualElement root = window == null ? null : window.rootVisualElement;
                if (root == null || root.panel == null || ContainsRoot(root))
                    continue;

                if (!root.styleSheets.Contains(globalStyleSheet))
                    root.styleSheets.Add(globalStyleSheet);
                root.AddToClassList(RootClass);
                root.EnableInClassList(DarkRootClass, EditorGUIUtility.isProSkin);
                root.EnableInClassList(LightRootClass, !EditorGUIUtility.isProSkin);
                rootSnapshots.Add(new RootSnapshot { root = root });
                styledRoots.Add(root);
                root.MarkDirtyRepaint();
            }
        }

        private static void RestoreStyle(StyleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.style == null || snapshot.states == null)
                return;

            for (int i = 0; i < snapshot.states.Length; i++)
            {
                StateSnapshot state = snapshot.states[i];
                if (state == null || state.state == null)
                    continue;
                try
                {
                    state.state.textColor = state.textColor;
                    state.state.background = state.background;
                    state.state.scaledBackgrounds = state.scaledBackgrounds;
                }
                catch
                {
                    // 恢复逐状态隔离；一个 Unity 内部状态失效不阻断其余样式恢复。
                }
            }
        }

        private static void RestoreRoot(RootSnapshot snapshot)
        {
            VisualElement root = snapshot == null ? null : snapshot.root;
            if (root == null)
                return;
            root.RemoveFromClassList(RootClass);
            root.RemoveFromClassList(DarkRootClass);
            root.RemoveFromClassList(LightRootClass);
            if (globalStyleSheet != null && root.styleSheets.Contains(globalStyleSheet))
                root.styleSheets.Remove(globalStyleSheet);
            root.MarkDirtyRepaint();
        }

        private static void DestroyCreatedTextures()
        {
            for (int i = 0; i < createdTextures.Count; i++)
                if (createdTextures[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdTextures[i]);
            createdTextures.Clear();
            createdTextureBytes = 0L;
        }

        private static bool CanCreateTexture(long requiredBytes)
        {
            return requiredBytes > 0L
                && createdTextures.Count < MaxCreatedTextureCount
                && createdTextureBytes + requiredBytes <= MaxCreatedTextureBytes;
        }

        private static string BuildAppliedMessage()
        {
            return "ES 全局深度皮肤已覆盖 " + snapshots.Count + " 个 IMGUI 文字样式和 "
                + rootSnapshots.Count + " 个 UI Toolkit 窗口；纯色只应用到安全内容容器，"
                + "原生窗口根节点与透明绘制层保持不变。进入 PlayMode 自动停用，可随时恢复原生样式。";
        }

        private static bool ContainsStyle(GUIStyle style)
        {
            return style != null && styledStyles.Contains(style);
        }

        private static bool ContainsRoot(VisualElement root)
        {
            return root != null && styledRoots.Contains(root);
        }

    }
}
