#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>视口工具之间的最小互斥策略。领域视口只提供状态，不复制仲裁条件。</summary>
    public static class ESWorkbenchInteractionPolicy
    {
        public static bool ShouldBeginObjectMove(
            bool hasHitObject,
            bool selectionInteraction,
            bool moveInteractionEnabled,
            bool canMove,
            bool hierarchyLocked)
        {
            return hasHitObject
                && selectionInteraction
                && moveInteractionEnabled
                && canMove
                && !hierarchyLocked;
        }

        /// <summary>
        /// 确定左键主意图的优先级：显式选择/变换工具拥有对象命中，笔刷工具拥有
        /// 地面命中。领域视口不得在命中区域后自行把笔刷升级为对象移动。
        /// </summary>
        public static bool ShouldBeginTerrainPaint(
            bool terrainToolActive,
            bool selectionOrTransformInteractionActive)
        {
            return terrainToolActive && !selectionOrTransformInteractionActive;
        }

        public static bool ShouldHandleNavigation(
            bool externalContentDragActive,
            bool primaryAuthoringGestureActive)
        {
            return !externalContentDragActive && !primaryAuthoringGestureActive;
        }

        /// <summary>
        /// 精确目标悬停的统一门槛。工具切换到笔刷并不意味着目标不可见；
        /// 只有实际绘制、变换、相机捕获或不具备作者目标语义时才清除悬停。
        /// </summary>
        public static bool ShouldShowPreciseHover(
            bool readOnly,
            bool transforming,
            bool painting,
            bool navigationCapturing,
            ESWorkbenchToolCapabilities capabilities,
            bool pointerInside)
        {
            if (readOnly || transforming || painting || navigationCapturing || !pointerInside)
                return false;
            return ESWorkbenchToolCapabilityResolver.Has(
                       capabilities, ESWorkbenchToolCapabilities.Select)
                || ESWorkbenchToolCapabilityResolver.Has(
                       capabilities, ESWorkbenchToolCapabilities.Paint)
                || ESWorkbenchToolCapabilityResolver.Has(
                       capabilities, ESWorkbenchToolCapabilities.Move);
        }
    }

    /// <summary>
    /// 正式拖放提交的屏幕边界合同。视口可以在地图留白中把坐标夹到世界边界，
    /// 但不能把工具栏、状态覆盖层或其它非交互区域误当成场景落点。
    /// </summary>
    public static class ESWorkbenchDropPointPolicy
    {
        public static bool CanCommit(Rect interactionRect, Vector2 localPoint)
        {
            return IsFinite(interactionRect)
                && IsFinite(localPoint)
                && interactionRect.width > 1f
                && interactionRect.height > 1f
                && localPoint.x >= interactionRect.xMin
                && localPoint.x <= interactionRect.xMax
                && localPoint.y >= interactionRect.yMin
                && localPoint.y <= interactionRect.yMax;
        }

        public static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsFinite(Rect value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.width) && !float.IsInfinity(value.width)
            && !float.IsNaN(value.height) && !float.IsInfinity(value.height);

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    /// <summary>
    /// 拖放预览的布局等价判断。内容身份由宿主单独校验；这里仅负责空间、尺寸、
    /// 间距和接受状态，避免每个视口复制一套浮点容差与状态比较规则。
    /// </summary>
    public static class ESWorkbenchDropPreviewRefreshPolicy
    {
        public static bool IsEquivalent(
            Vector3 previousAnchor,
            Vector3 nextAnchor,
            int previousCount,
            int nextCount,
            float previousSpacing,
            float nextSpacing,
            Vector3 previousSize,
            Vector3 nextSize,
            ESWorkbenchDropPreviewState previousState,
            ESWorkbenchDropPreviewState nextState,
            float positionEpsilon = 0.0001f,
            float scalarEpsilon = 0.0001f,
            bool previousSnapEnabled = false,
            bool nextSnapEnabled = false,
            float previousSnapStep = 0f,
            float nextSnapStep = 0f)
        {
            if (!IsFinite(previousAnchor) || !IsFinite(nextAnchor)
                || !IsFinite(previousSize) || !IsFinite(nextSize)
                || !IsFinite(positionEpsilon) || positionEpsilon < 0f
                || !IsFinite(scalarEpsilon) || scalarEpsilon < 0f
                || !IsFinite(previousSnapStep) || !IsFinite(nextSnapStep)
                || previousSnapStep < 0f || nextSnapStep < 0f)
                return false;
            return previousCount == nextCount
                && (previousAnchor - nextAnchor).sqrMagnitude
                    <= positionEpsilon * positionEpsilon
                && (previousSize - nextSize).sqrMagnitude
                    <= positionEpsilon * positionEpsilon
                && Mathf.Abs(previousSpacing - nextSpacing) <= scalarEpsilon
                && previousSnapEnabled == nextSnapEnabled
                && (!previousSnapEnabled
                    || Mathf.Abs(previousSnapStep - nextSnapStep) <= scalarEpsilon)
                && previousState.Accepted == nextState.Accepted
                && string.Equals(previousState.Reason, nextState.Reason, StringComparison.Ordinal);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>作者视口空间投影的明确意图；不同意图拥有不同的边界和高度语义。</summary>
    public enum ESWorkbenchViewportProjectionIntent : byte
    {
        AuthorHit,
        TerrainPaint,
        DropPreview,
        EdgePanPreview
    }

    public readonly struct ESWorkbenchViewportProjectionRequest
    {
        private ESWorkbenchViewportProjectionRequest(
            ESWorkbenchViewportProjectionIntent intent,
            bool requireTerrainSurface,
            bool allowOutside,
            bool requireInteractionBoundary,
            bool clampToWorld)
        {
            Intent = intent;
            RequireTerrainSurface = requireTerrainSurface;
            AllowOutside = allowOutside;
            RequireInteractionBoundary = requireInteractionBoundary;
            ClampToWorld = clampToWorld;
        }

        public ESWorkbenchViewportProjectionIntent Intent { get; }
        public bool RequireTerrainSurface { get; }
        public bool AllowOutside { get; }
        public bool RequireInteractionBoundary { get; }
        public bool ClampToWorld { get; }

        public static ESWorkbenchViewportProjectionRequest For(
            ESWorkbenchViewportProjectionIntent intent,
            bool requireTerrainSurface = false)
        {
            switch (intent)
            {
                case ESWorkbenchViewportProjectionIntent.TerrainPaint:
                    return new ESWorkbenchViewportProjectionRequest(
                        intent, requireTerrainSurface: true, allowOutside: false,
                        requireInteractionBoundary: true, clampToWorld: false);
                case ESWorkbenchViewportProjectionIntent.DropPreview:
                    return new ESWorkbenchViewportProjectionRequest(
                        intent, requireTerrainSurface, allowOutside: true,
                        requireInteractionBoundary: true, clampToWorld: true);
                case ESWorkbenchViewportProjectionIntent.EdgePanPreview:
                    return new ESWorkbenchViewportProjectionRequest(
                        intent, requireTerrainSurface: false, allowOutside: true,
                        requireInteractionBoundary: false, clampToWorld: true);
                default:
                    return new ESWorkbenchViewportProjectionRequest(
                        ESWorkbenchViewportProjectionIntent.AuthorHit,
                        requireTerrainSurface: false, allowOutside: false,
                        requireInteractionBoundary: true, clampToWorld: true);
            }
        }
    }

    /// <summary>
    /// 左键在作者视口中的唯一主意图。各领域只提供事实输入，不再自行组合
    /// “命中对象/当前工具/锁定/可移动”的优先级。
    /// </summary>
    public enum ESWorkbenchPointerIntentKind : byte
    {
        None,
        Select,
        Manipulate,
        Paint,
        GroundAction
    }

    /// <summary>
    /// 主指针意图的结构化决策原因。它属于输入仲裁合同，而不是具体领域的提示文案。
    /// 视口可以据此决定是否捕获指针、是否阻断相机导航以及是否允许提交，避免再次按
    /// “当前工具名称”自行猜测优先级。
    /// </summary>
    public enum ESWorkbenchPointerIntentDecisionReason : byte
    {
        None,
        ExternalContentDrag,
        NavigationAlreadyOwned,
        SelectTarget,
        ManipulateTarget,
        PaintGround,
        GroundAction,
        SelectEmpty,
        UnsupportedTool,
        HierarchyLocked
    }

    public readonly struct ESWorkbenchPointerIntentDecision
    {
        public ESWorkbenchPointerIntentDecision(
            ESWorkbenchPointerIntentKind intent,
            bool canStart,
            bool consumesNavigation,
            bool canCommit,
            ESWorkbenchPointerIntentDecisionReason reason)
        {
            Intent = intent;
            CanStart = canStart;
            ConsumesNavigation = consumesNavigation;
            CanCommit = canCommit;
            Reason = reason;
        }

        public ESWorkbenchPointerIntentKind Intent { get; }
        public bool CanStart { get; }
        public bool ConsumesNavigation { get; }
        public bool CanCommit { get; }
        public ESWorkbenchPointerIntentDecisionReason Reason { get; }

        public static ESWorkbenchPointerIntentDecision Blocked(
            ESWorkbenchPointerIntentDecisionReason reason,
            bool consumesNavigation = false)
        {
            return new ESWorkbenchPointerIntentDecision(
                ESWorkbenchPointerIntentKind.None,
                canStart: false,
                consumesNavigation,
                canCommit: false,
                reason);
        }
    }

    [Flags]
    public enum ESWorkbenchToolCapabilities : byte
    {
        None = 0,
        Select = 1 << 0,
        Move = 1 << 1,
        Rotate = 1 << 2,
        Scale = 1 << 3,
        Paint = 1 << 4,
        GroundAction = 1 << 5,
        Auto = 1 << 7
    }

    /// <summary>
    /// 主指针命中的空间层级。精确目标（POI、Prefab、控制点）与区域容器
    /// 的让渡语义不同：容器可被移动时应让出给移动，不可移动时仍允许
    /// 地面笔刷继续工作；精确目标则保留选择语义，避免笔刷误吞对象。
    /// </summary>
    public enum ESWorkbenchPointerHitKind : byte
    {
        Unspecified,
        Ground,
        Container,
        PreciseTarget
    }

    public static class ESWorkbenchToolCapabilityResolver
    {
        public static ESWorkbenchToolCapabilities Resolve(string toolId,
            ESWorkbenchToolCapabilities declared = ESWorkbenchToolCapabilities.Auto)
        {
            if (declared != ESWorkbenchToolCapabilities.Auto) return declared;
            if (string.IsNullOrWhiteSpace(toolId)) return ESWorkbenchToolCapabilities.None;
            // 领域工具必须通过 ESWorkbenchToolDescriptor 显式声明能力。
            // 公共底座只保留核心通用工具的历史兼容推断，不能按 World/Scene
            // 的字符串命名猜测作者语义。
            if (toolId == "core.rotate")
                return ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Rotate;
            if (toolId == "core.scale")
                return ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Scale;
            if (toolId == "core.move" || toolId == "core.select" || toolId.EndsWith(".select", StringComparison.Ordinal))
                return ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move;
            return ESWorkbenchToolCapabilities.None;
        }

        public static bool Has(ESWorkbenchToolCapabilities capabilities, ESWorkbenchToolCapabilities required) =>
            (capabilities & required) == required;

        public static ESWorkbenchToolCapabilities ResolveTarget(bool canMove, bool canRotate, bool canScale)
        {
            ESWorkbenchToolCapabilities result = ESWorkbenchToolCapabilities.Select;
            if (canMove) result |= ESWorkbenchToolCapabilities.Move;
            if (canRotate) result |= ESWorkbenchToolCapabilities.Rotate;
            if (canScale) result |= ESWorkbenchToolCapabilities.Scale;
            return result;
        }
    }

    public readonly struct ESWorkbenchPointerIntentContext
    {
        public ESWorkbenchPointerIntentContext(
            bool externalContentDragActive,
            bool navigationGestureActive,
            bool paintInteractionActive,
            bool selectionInteractionActive,
            bool hasHitTarget,
            bool manipulationEnabled,
            bool canManipulate,
            bool hierarchyLocked,
            bool groundActionEnabled = true,
            ESWorkbenchPointerHitKind hitKind = ESWorkbenchPointerHitKind.Unspecified)
        {
            ExternalContentDragActive = externalContentDragActive;
            NavigationGestureActive = navigationGestureActive;
            PaintInteractionActive = paintInteractionActive;
            SelectionInteractionActive = selectionInteractionActive;
            HasHitTarget = hasHitTarget;
            ManipulationEnabled = manipulationEnabled;
            CanManipulate = canManipulate;
            HierarchyLocked = hierarchyLocked;
            GroundActionEnabled = groundActionEnabled;
            HitKind = hitKind == ESWorkbenchPointerHitKind.Unspecified
                ? (hasHitTarget
                    ? ESWorkbenchPointerHitKind.PreciseTarget
                    : ESWorkbenchPointerHitKind.Ground)
                : hitKind;
            ToolCapabilities = (selectionInteractionActive
                    ? (manipulationEnabled ? ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                        : ESWorkbenchToolCapabilities.Select)
                    : ESWorkbenchToolCapabilities.None)
                | (paintInteractionActive ? ESWorkbenchToolCapabilities.Paint : ESWorkbenchToolCapabilities.None)
                | (!selectionInteractionActive && !paintInteractionActive && groundActionEnabled
                    ? ESWorkbenchToolCapabilities.GroundAction
                    : ESWorkbenchToolCapabilities.None);
            ViewportCapabilities = ESWorkbenchToolCapabilities.Select
                | ESWorkbenchToolCapabilities.Move
                | ESWorkbenchToolCapabilities.Rotate
                | ESWorkbenchToolCapabilities.Scale
                | ESWorkbenchToolCapabilities.Paint
                | ESWorkbenchToolCapabilities.GroundAction;
            TargetCapabilities = canManipulate
                ? ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                : ESWorkbenchToolCapabilities.Select;
        }

        public ESWorkbenchPointerIntentContext(
            bool externalContentDragActive,
            bool navigationGestureActive,
            ESWorkbenchToolCapabilities toolCapabilities,
            ESWorkbenchToolCapabilities viewportCapabilities,
            ESWorkbenchToolCapabilities targetCapabilities,
            bool hasHitTarget,
            bool hierarchyLocked,
            bool groundActionEnabled = true,
            ESWorkbenchPointerHitKind hitKind = ESWorkbenchPointerHitKind.Unspecified)
        {
            ExternalContentDragActive = externalContentDragActive;
            NavigationGestureActive = navigationGestureActive;
            PaintInteractionActive = ESWorkbenchToolCapabilityResolver.Has(toolCapabilities, ESWorkbenchToolCapabilities.Paint);
            SelectionInteractionActive = ESWorkbenchToolCapabilityResolver.Has(toolCapabilities, ESWorkbenchToolCapabilities.Select);
            HasHitTarget = hasHitTarget;
            // ManipulationEnabled 描述当前工具/视口是否存在操作路径，
            // CanManipulate 再叠加命中目标、锁定和笔刷让出门槛；不能只复制工具
            // 声明位，否则适配器会与 ResolveDecision 看到相反的事实。
            ESWorkbenchToolCapabilities directTransform = SelectionInteractionActive
                ? toolCapabilities & viewportCapabilities
                    & (ESWorkbenchToolCapabilities.Move
                        | ESWorkbenchToolCapabilities.Rotate
                        | ESWorkbenchToolCapabilities.Scale)
                : ESWorkbenchToolCapabilities.None;
            bool brushMoveHandoff = PaintInteractionActive
                && ESWorkbenchToolCapabilityResolver.Has(
                    viewportCapabilities, ESWorkbenchToolCapabilities.Move);
            ManipulationEnabled = directTransform != ESWorkbenchToolCapabilities.None
                || brushMoveHandoff;
            ESWorkbenchToolCapabilities targetTransform = targetCapabilities
                & (ESWorkbenchToolCapabilities.Move
                    | ESWorkbenchToolCapabilities.Rotate
                    | ESWorkbenchToolCapabilities.Scale);
            CanManipulate = HasHitTarget && !hierarchyLocked
                && ((directTransform & targetTransform) != ESWorkbenchToolCapabilities.None
                    || (brushMoveHandoff
                        && ESWorkbenchToolCapabilityResolver.Has(
                            targetCapabilities, ESWorkbenchToolCapabilities.Select)
                        && ESWorkbenchToolCapabilityResolver.Has(
                            targetCapabilities, ESWorkbenchToolCapabilities.Move)));
            HierarchyLocked = hierarchyLocked;
            GroundActionEnabled = groundActionEnabled;
            HitKind = hitKind == ESWorkbenchPointerHitKind.Unspecified
                ? (hasHitTarget
                    ? ESWorkbenchPointerHitKind.PreciseTarget
                    : ESWorkbenchPointerHitKind.Ground)
                : hitKind;
            ToolCapabilities = toolCapabilities;
            ViewportCapabilities = viewportCapabilities;
            TargetCapabilities = targetCapabilities;
        }

        public bool ExternalContentDragActive { get; }
        public bool NavigationGestureActive { get; }
        public bool PaintInteractionActive { get; }
        public bool SelectionInteractionActive { get; }
        public bool HasHitTarget { get; }
        public bool ManipulationEnabled { get; }
        public bool CanManipulate { get; }
        public bool HierarchyLocked { get; }
        public bool GroundActionEnabled { get; }
        public ESWorkbenchToolCapabilities ToolCapabilities { get; }
        public ESWorkbenchToolCapabilities ViewportCapabilities { get; }
        public ESWorkbenchToolCapabilities TargetCapabilities { get; }
        public ESWorkbenchPointerHitKind HitKind { get; }
    }

    public static class ESWorkbenchPointerIntentResolver
    {
        public static ESWorkbenchPointerIntentKind Resolve(ESWorkbenchPointerIntentContext context)
        {
            return ResolveDecision(context).Intent;
        }

        public static ESWorkbenchPointerIntentDecision ResolveDecision(
            ESWorkbenchPointerIntentContext context)
        {
            if (context.ExternalContentDragActive)
                return ESWorkbenchPointerIntentDecision.Blocked(
                    ESWorkbenchPointerIntentDecisionReason.ExternalContentDrag,
                    consumesNavigation: true);
            if (context.NavigationGestureActive)
                return ESWorkbenchPointerIntentDecision.Blocked(
                    ESWorkbenchPointerIntentDecisionReason.NavigationAlreadyOwned,
                    consumesNavigation: true);

            // 选择/变换工具永远先处理对象命中；即使对象锁定或不可移动，也必须保留选择语义。
            ESWorkbenchToolCapabilities supportedTool = context.ToolCapabilities & context.ViewportCapabilities;
            bool brushActive = ESWorkbenchToolCapabilityResolver.Has(
                supportedTool, ESWorkbenchToolCapabilities.Paint);
            bool containerCanMove = ESWorkbenchToolCapabilityResolver.Has(
                    context.ViewportCapabilities, ESWorkbenchToolCapabilities.Move)
                && ESWorkbenchToolCapabilityResolver.Has(
                    context.TargetCapabilities,
                    ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move);

            // 容器区域不是精确对象：如果当前区域没有可执行移动能力，
            // 笔刷应继续作用于其地面，而不是被“选择但不可操作”卡住。
            // 一旦区域声明移动能力，锁定状态和实际让渡仍由统一目标分支处理。

            if (ESWorkbenchToolCapabilityResolver.Has(supportedTool, ESWorkbenchToolCapabilities.Select))
            {
                if (!context.HasHitTarget)
                {
                    // 混合工具在空地上应交给其明确的地面动作：笔刷绘制优先于
                    // 选择占位，区域/预制件等创建动作仍由 GroundAction 接管。
                    if (ESWorkbenchToolCapabilityResolver.Has(
                        supportedTool, ESWorkbenchToolCapabilities.Paint))
                        return new ESWorkbenchPointerIntentDecision(
                            ESWorkbenchPointerIntentKind.Paint,
                            canStart: true,
                            consumesNavigation: true,
                            canCommit: true,
                            ESWorkbenchPointerIntentDecisionReason.PaintGround);
                    if (context.GroundActionEnabled
                        && ESWorkbenchToolCapabilityResolver.Has(
                            supportedTool, ESWorkbenchToolCapabilities.GroundAction))
                        return new ESWorkbenchPointerIntentDecision(
                            ESWorkbenchPointerIntentKind.GroundAction,
                            canStart: true,
                            consumesNavigation: true,
                            canCommit: true,
                            ESWorkbenchPointerIntentDecisionReason.GroundAction);
                    return new ESWorkbenchPointerIntentDecision(
                        ESWorkbenchPointerIntentKind.Select,
                        canStart: true,
                        consumesNavigation: true,
                        canCommit: false,
                        ESWorkbenchPointerIntentDecisionReason.SelectEmpty);
                }
                ESWorkbenchToolCapabilities transform = supportedTool & context.TargetCapabilities
                    & (ESWorkbenchToolCapabilities.Move | ESWorkbenchToolCapabilities.Rotate | ESWorkbenchToolCapabilities.Scale);
                if (transform != ESWorkbenchToolCapabilities.None && !context.HierarchyLocked)
                    return new ESWorkbenchPointerIntentDecision(
                        ESWorkbenchPointerIntentKind.Manipulate,
                        canStart: true,
                        consumesNavigation: true,
                        canCommit: true,
                        ESWorkbenchPointerIntentDecisionReason.ManipulateTarget);
                if (brushActive
                    && context.HitKind == ESWorkbenchPointerHitKind.Container
                    && !containerCanMove)
                    return new ESWorkbenchPointerIntentDecision(
                        ESWorkbenchPointerIntentKind.Paint,
                        canStart: true,
                        consumesNavigation: true,
                        canCommit: true,
                        ESWorkbenchPointerIntentDecisionReason.PaintGround);
                return new ESWorkbenchPointerIntentDecision(
                    ESWorkbenchPointerIntentKind.Select,
                    canStart: true,
                    consumesNavigation: true,
                    canCommit: false,
                    context.HierarchyLocked
                        ? ESWorkbenchPointerIntentDecisionReason.HierarchyLocked
                        : ESWorkbenchPointerIntentDecisionReason.SelectTarget);
            }

            if (brushActive
                && context.HitKind == ESWorkbenchPointerHitKind.Container
                && context.HasHitTarget
                && !containerCanMove)
                return new ESWorkbenchPointerIntentDecision(
                    ESWorkbenchPointerIntentKind.Paint,
                    canStart: true,
                    consumesNavigation: true,
                    canCommit: true,
                    ESWorkbenchPointerIntentDecisionReason.PaintGround);

            // 笔刷默认拥有地面命中；若射线先命中一个可操作的精确目标，
            // 仍必须先完成对象选择/移动，避免 POI/Prefab 被笔刷吞掉。
            // 区域容器的例外已在上方按 HitKind 明确处理。
            if (ESWorkbenchToolCapabilityResolver.Has(supportedTool, ESWorkbenchToolCapabilities.Paint))
            {
                if (context.HasHitTarget
                    && ESWorkbenchToolCapabilityResolver.Has(
                        context.TargetCapabilities, ESWorkbenchToolCapabilities.Select))
                {
                    // 笔刷只可让开给对象的平面移动；旋转/缩放仍由对应显式工具声明。
                    ESWorkbenchToolCapabilities transform = context.ViewportCapabilities
                        & context.TargetCapabilities
                        & ESWorkbenchToolCapabilities.Move;
                    if (transform != ESWorkbenchToolCapabilities.None && !context.HierarchyLocked)
                        return new ESWorkbenchPointerIntentDecision(
                            ESWorkbenchPointerIntentKind.Manipulate,
                            canStart: true,
                            consumesNavigation: true,
                            canCommit: true,
                            ESWorkbenchPointerIntentDecisionReason.ManipulateTarget);
                    return new ESWorkbenchPointerIntentDecision(
                        ESWorkbenchPointerIntentKind.Select,
                        canStart: true,
                        consumesNavigation: true,
                        canCommit: false,
                        context.HierarchyLocked
                            ? ESWorkbenchPointerIntentDecisionReason.HierarchyLocked
                            : ESWorkbenchPointerIntentDecisionReason.SelectTarget);
                }
                return new ESWorkbenchPointerIntentDecision(
                    ESWorkbenchPointerIntentKind.Paint,
                    canStart: true,
                    consumesNavigation: true,
                    canCommit: true,
                    ESWorkbenchPointerIntentDecisionReason.PaintGround);
            }
            if (context.GroundActionEnabled
                && ESWorkbenchToolCapabilityResolver.Has(supportedTool, ESWorkbenchToolCapabilities.GroundAction)
            )
                return new ESWorkbenchPointerIntentDecision(
                    ESWorkbenchPointerIntentKind.GroundAction,
                    canStart: true,
                    consumesNavigation: true,
                    canCommit: true,
                    ESWorkbenchPointerIntentDecisionReason.GroundAction);
            return ESWorkbenchPointerIntentDecision.Blocked(
                ESWorkbenchPointerIntentDecisionReason.UnsupportedTool);
        }
    }

    /// <summary>
    /// 二维空间视图的稳定命中层级：点、对象或手柄等精确目标优先于区域、分组等面积容器。
    /// 领域视图负责各层内部的距离和绘制顺序，本合同只防止背景容器覆盖已命中的直接操作目标。
    /// </summary>
    public static class ESWorkbenchSpatialHitResolver
    {
        /// <summary>
        /// 将选择身份投影为公共命中层级。容器身份由宿主注入，
        /// 公共层不依赖 World、Scene 或任何领域的字符串命名。
        /// </summary>
        public static ESWorkbenchPointerHitKind ResolveHitKind(
            ESWorkbenchSelection selection,
            Func<ESWorkbenchSelection, bool> isContainer)
        {
            if (selection == null || selection.IsEmpty)
                return ESWorkbenchPointerHitKind.Ground;
            return isContainer != null && isContainer(selection)
                ? ESWorkbenchPointerHitKind.Container
                : ESWorkbenchPointerHitKind.PreciseTarget;
        }

        public static ESWorkbenchSelection PreferPrecise(
            ESWorkbenchSelection preciseHit,
            ESWorkbenchSelection areaHit)
        {
            return preciseHit != null && !preciseHit.IsEmpty
                ? preciseHit
                : areaHit ?? ESWorkbenchSelection.Empty;
        }

        /// <summary>
        /// 通用 2D 空间命中：点状/对象目标先于矩形区域，矩形允许共享的像素容差。
        /// 领域只提供投影列表和画布坐标，不再按注册顺序自行决定命中优先级。
        /// </summary>
        public static ESWorkbenchHierarchyDescriptor HitTest2D(
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> projected,
            Vector2 localPoint,
            Rect worldBounds,
            Rect canvasBounds,
            float selectionHitRadiusPixels)
        {
            if (projected == null || !IsFinite(localPoint)
                || !IsFinite(worldBounds) || !IsFinite(canvasBounds)
                || worldBounds.width <= 0f || worldBounds.height <= 0f
                || canvasBounds.width <= 0f || canvasBounds.height <= 0f) return null;
            float radius = IsFinite(selectionHitRadiusPixels)
                ? Mathf.Max(0f, selectionHitRadiusPixels)
                : 0f;
            ESWorkbenchHierarchyDescriptor precise = null;
            float nearest = float.MaxValue;
            for (int i = projected.Count - 1; i >= 0; i--)
            {
                ESWorkbenchHierarchyDescriptor item = projected[i];
                if (item?.Spatial == null || item.Spatial.Shape == ESWorkbenchSpatialShape.Rectangle)
                    continue;
                Vector2 center = WorldToCanvas(item.Spatial.Position, worldBounds, canvasBounds);
                float distance = Vector2.Distance(center, localPoint);
                if (distance > radius || distance >= nearest) continue;
                nearest = distance;
                precise = item;
            }
            if (precise != null) return precise;

            for (int i = projected.Count - 1; i >= 0; i--)
            {
                ESWorkbenchHierarchyDescriptor item = projected[i];
                if (item?.Spatial == null || item.Spatial.Shape != ESWorkbenchSpatialShape.Rectangle)
                    continue;
                Vector2 center = WorldToCanvas(item.Spatial.Position, worldBounds, canvasBounds);
                Vector2 half = WorldSizeToCanvas(item.Spatial.Size, worldBounds, canvasBounds) * 0.5f;
                Rect bounds = new Rect(
                    center - half - Vector2.one * radius,
                    half * 2f + Vector2.one * radius * 2f);
                if (bounds.Contains(localPoint)) return item;
            }
            return null;
        }

        private static Vector2 WorldToCanvas(
            Vector3 value,
            Rect worldBounds,
            Rect canvasBounds)
        {
            return new Vector2(
                Mathf.Lerp(canvasBounds.xMin, canvasBounds.xMax,
                    Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, value.x)),
                Mathf.Lerp(canvasBounds.yMax, canvasBounds.yMin,
                    Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, value.z)));
        }

        private static Vector2 WorldSizeToCanvas(
            Vector3 size,
            Rect worldBounds,
            Rect canvasBounds)
        {
            return new Vector2(
                size.x / Mathf.Max(0.001f, Mathf.Abs(worldBounds.width)) * canvasBounds.width,
                size.z / Mathf.Max(0.001f, Mathf.Abs(worldBounds.height)) * canvasBounds.height);
        }

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.width) && IsFinite(value.height);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 视口屏幕空间的多边形命中合同。
    /// 区域、贴地引导和其它投影作者层必须按真实投影边界命中，不能用包围盒
    /// 把透视下的四边形外区域误交给移动或笔刷工具。
    /// </summary>
    public static class ESWorkbenchScreenGeometry
    {
        public static bool ContainsPolygon(
            IReadOnlyList<Vector2> polygon,
            int count,
            Vector2 point,
            float tolerance = 0f)
        {
            if (polygon == null || count < 3 || count > polygon.Count
                || !IsFinite(point)) return false;
            float safeTolerance = IsFinite(tolerance) ? Mathf.Max(0f, tolerance) : 0f;
            float toleranceSquared = safeTolerance * safeTolerance;
            bool inside = false;
            for (int i = 0; i < count; i++)
            {
                Vector2 start = polygon[i];
                Vector2 end = polygon[(i + 1) % count];
                if (!IsFinite(start) || !IsFinite(end)) return false;
                Vector2 edge = end - start;
                float edgeLengthSquared = edge.sqrMagnitude;
                if (!IsFinite(edgeLengthSquared)) return false;
                if (DistanceSquaredToSegment(point, start, end) <= toleranceSquared)
                    return true;
                bool crossesRay = (start.y > point.y) != (end.y > point.y);
                if (!crossesRay || Mathf.Abs(edge.y) <= 0.000001f) continue;
                float intersectionX = start.x + (point.y - start.y) * edge.x / edge.y;
                if (!IsFinite(intersectionX)) return false;
                if (point.x < intersectionX) inside = !inside;
            }
            return inside;
        }

        private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            if (!IsFinite(lengthSquared) || lengthSquared <= 0.000001f)
                return (point - start).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
            return (point - (start + delta * t)).sqrMagnitude;
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 预览对象的 Renderer 集合缓存。命中、悬停和选中框可以高频读取世界 Bounds，
    /// 但不应在每次鼠标事件中重新遍历 GameObject 子树；Renderer.bounds 仍每次读取，
    /// 因此移动、旋转和缩放预览不会使用过期几何结果。
    /// </summary>
    public sealed class ESWorkbenchRendererBoundsCache
    {
        private readonly Dictionary<GameObject, Renderer[]> rendererSets =
            new Dictionary<GameObject, Renderer[]>();

        public int RendererSetBuildCount { get; private set; }

        public Bounds Calculate(GameObject root)
        {
            if (root == null)
                return new Bounds(Vector3.zero, Vector3.one);

            if (!rendererSets.TryGetValue(root, out Renderer[] renderers))
            {
                renderers = root.GetComponentsInChildren<Renderer>(true);
                rendererSets[root] = renderers ?? Array.Empty<Renderer>();
                RendererSetBuildCount++;
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.one);
        }

        public void Invalidate(GameObject root)
        {
            if (root != null) rendererSets.Remove(root);
        }

        public void Clear()
        {
            rendererSets.Clear();
            RendererSetBuildCount = 0;
        }
    }

    /// <summary>
    /// 视口共用的只读悬停状态。只保存稳定 ID，不持有领域对象，也不触发选择、Undo 或作者事务。
    /// </summary>
    public sealed class ESWorkbenchHoverState
    {
        private string stableId = string.Empty;

        public string StableId => stableId;
        public bool HasValue => !string.IsNullOrEmpty(stableId);

        public bool IsHovered(string candidateStableId) =>
            !string.IsNullOrEmpty(candidateStableId)
            && string.Equals(stableId, candidateStableId, StringComparison.Ordinal);

        public bool Update(string candidateStableId)
        {
            string next = string.IsNullOrWhiteSpace(candidateStableId)
                ? string.Empty
                : candidateStableId.Trim();
            if (string.Equals(stableId, next, StringComparison.Ordinal)) return false;
            stableId = next;
            return true;
        }

        public bool Clear() => Update(null);
    }

    /// <summary>
    /// 高频命中/悬停查询使用的稳定选择对象缓存。
    /// 选择合同本身不可变；领域在绑定、重建或对象代际变化时必须清空，避免旧
    /// Payload 跨数据代际泄漏。缓存不持有 PreviewScene 或业务可变对象。
    /// </summary>
    public sealed class ESWorkbenchSelectionCache
    {
        private readonly Dictionary<string, ESWorkbenchSelection> selections =
            new Dictionary<string, ESWorkbenchSelection>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, ESWorkbenchSelection>> localSelections =
            new Dictionary<string, Dictionary<string, ESWorkbenchSelection>>(StringComparer.Ordinal);
        private readonly LinkedList<CacheEntry> insertionOrder = new LinkedList<CacheEntry>();
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> insertionNodes =
            new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.Ordinal);
        private readonly int maximumEntries;

        private readonly struct CacheEntry
        {
            public CacheEntry(string stableId, ESWorkbenchSelection selection)
            {
                StableId = stableId;
                Selection = selection;
            }

            public string StableId { get; }
            public ESWorkbenchSelection Selection { get; }
        }

        public ESWorkbenchSelectionCache(int maximumEntries = 8192)
        {
            this.maximumEntries = Mathf.Clamp(maximumEntries, 1, 1_000_000);
        }

        public int Count => selections.Count;
        public int MaximumEntries => maximumEntries;

        public ESWorkbenchSelection GetOrCreate(
            string stableId,
            string kind,
            UnityEngine.Object unityObject = null,
            object payload = null)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return ESWorkbenchSelection.Empty;
            string normalizedId = stableId.Trim();
            string normalizedKind = kind?.Trim() ?? string.Empty;
            if (selections.TryGetValue(normalizedId, out ESWorkbenchSelection existing)
                && string.Equals(existing.Kind, normalizedKind, StringComparison.Ordinal)
                && existing.UnityObject == unityObject
                && Equals(existing.Payload, payload))
                return existing;

            ESWorkbenchSelection next = new ESWorkbenchSelection(
                normalizedId,
                normalizedKind,
                unityObject,
                payload);
            if (insertionNodes.TryGetValue(normalizedId, out LinkedListNode<CacheEntry> previousNode))
                insertionOrder.Remove(previousNode);
            selections[normalizedId] = next;
            insertionNodes[normalizedId] = insertionOrder.AddLast(
                new CacheEntry(normalizedId, next));
            TrimToCapacity();
            return next;
        }

        /// <summary>
        /// 使用领域本地 ID 查找选择。稳定 ID 只在该本地 ID 首次出现时拼接，
        /// 避免高频悬停命中路径为每个事件分配字符串。
        /// </summary>
        public ESWorkbenchSelection GetOrCreateLocal(
            string kind,
            string localId,
            string stableIdPrefix,
            UnityEngine.Object unityObject = null,
            object payload = null)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(localId))
                return ESWorkbenchSelection.Empty;
            string normalizedKind = kind.Trim();
            string normalizedLocalId = localId.Trim();
            if (!localSelections.TryGetValue(
                    normalizedKind,
                    out Dictionary<string, ESWorkbenchSelection> bucket))
            {
                bucket = new Dictionary<string, ESWorkbenchSelection>(StringComparer.Ordinal);
                localSelections.Add(normalizedKind, bucket);
            }
            if (bucket.TryGetValue(normalizedLocalId, out ESWorkbenchSelection existing)
                && selections.TryGetValue(existing.StableId, out ESWorkbenchSelection current)
                && ReferenceEquals(current, existing)
                && existing.UnityObject == unityObject
                && Equals(existing.Payload, payload))
                return existing;

            string prefix = stableIdPrefix ?? string.Empty;
            ESWorkbenchSelection next = GetOrCreate(
                prefix + normalizedLocalId,
                normalizedKind,
                unityObject,
                payload);
            bucket[normalizedLocalId] = next;
            return next;
        }

        public bool Invalidate(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return false;
            string normalizedId = stableId.Trim();
            bool removed = selections.Remove(normalizedId);
            if (insertionNodes.TryGetValue(normalizedId, out LinkedListNode<CacheEntry> node))
            {
                insertionOrder.Remove(node);
                insertionNodes.Remove(normalizedId);
                removed = true;
            }
            removed |= RemoveLocalReferences(normalizedId);
            return removed;
        }

        public void Clear()
        {
            selections.Clear();
            localSelections.Clear();
            insertionOrder.Clear();
            insertionNodes.Clear();
        }

        private void TrimToCapacity()
        {
            while (selections.Count > maximumEntries && insertionOrder.Count > 0)
            {
                LinkedListNode<CacheEntry> oldestNode = insertionOrder.First;
                insertionOrder.RemoveFirst();
                CacheEntry oldest = oldestNode.Value;
                insertionNodes.Remove(oldest.StableId);
                if (!selections.TryGetValue(oldest.StableId, out ESWorkbenchSelection current)
                    || !ReferenceEquals(current, oldest.Selection)) continue;
                selections.Remove(oldest.StableId);
                RemoveLocalReferences(oldest.StableId);
            }
        }

        private bool RemoveLocalReferences(string stableId)
        {
            bool removed = false;
            foreach (Dictionary<string, ESWorkbenchSelection> bucket in localSelections.Values)
            {
                List<string> staleKeys = null;
                foreach (KeyValuePair<string, ESWorkbenchSelection> pair in bucket)
                    if (pair.Value != null
                        && string.Equals(pair.Value.StableId, stableId, StringComparison.Ordinal))
                    {
                        staleKeys ??= new List<string>();
                        staleKeys.Add(pair.Key);
                    }
                if (staleKeys == null) continue;
                for (int i = 0; i < staleKeys.Count; i++)
                    removed |= bucket.Remove(staleKeys[i]);
            }
            return removed;
        }
    }

    /// <summary>
    /// 工作台视口的领域无关交互手感配置。
    /// 业务视口只提供内容尺度和作者语义，不再各自猜测拖动、相机和笔刷响应曲线。
    /// </summary>
    public enum ESWorkbenchViewportFeelPreset : byte
    {
        Standard,
        Precision,
        RapidAuthoring
    }

    /// <summary>指针阈值、事件限幅和命中容差的语义分组。</summary>
    public readonly struct ESWorkbenchPointerFeelGroup
    {
        public ESWorkbenchPointerFeelGroup(
            float dragStartPixels,
            float maximumPointerDeltaPerEvent,
            float maximumWheelDeltaPerEvent,
            float selectionHitRadiusPixels)
        {
            DragStartPixels = dragStartPixels;
            MaximumPointerDeltaPerEvent = maximumPointerDeltaPerEvent;
            MaximumWheelDeltaPerEvent = maximumWheelDeltaPerEvent;
            SelectionHitRadiusPixels = selectionHitRadiusPixels;
        }

        public float DragStartPixels { get; }
        public float MaximumPointerDeltaPerEvent { get; }
        public float MaximumWheelDeltaPerEvent { get; }
        public float SelectionHitRadiusPixels { get; }
    }

    /// <summary>轨道相机、画布缩放和边缘平移的语义分组。</summary>
    public readonly struct ESWorkbenchNavigationFeelGroup
    {
        public ESWorkbenchNavigationFeelGroup(
            float orbitYawDegreesPerPixel,
            float orbitPitchDegreesPerPixel,
            float panWorldPerPixelAtDistance,
            float cameraWheelDistanceScale,
            float cameraWheelZoomSensitivity,
            float canvasWheelZoomSensitivity,
            ESWorkbenchEdgePanSettings edgePanSettings,
            float canvasOverscrollPixels,
            float panReferenceViewportHeight,
            float verticalFieldOfViewDegrees,
            float canvasMinimumZoom,
            float canvasMaximumZoom,
            float canvasViewportPaddingPixels)
        {
            OrbitYawDegreesPerPixel = orbitYawDegreesPerPixel;
            OrbitPitchDegreesPerPixel = orbitPitchDegreesPerPixel;
            PanWorldPerPixelAtDistance = panWorldPerPixelAtDistance;
            CameraWheelDistanceScale = cameraWheelDistanceScale;
            CameraWheelZoomSensitivity = cameraWheelZoomSensitivity;
            CanvasWheelZoomSensitivity = canvasWheelZoomSensitivity;
            EdgePanSettings = edgePanSettings;
            CanvasOverscrollPixels = canvasOverscrollPixels;
            PanReferenceViewportHeight = panReferenceViewportHeight;
            VerticalFieldOfViewDegrees = verticalFieldOfViewDegrees;
            CanvasMinimumZoom = canvasMinimumZoom;
            CanvasMaximumZoom = canvasMaximumZoom;
            CanvasViewportPaddingPixels = canvasViewportPaddingPixels;
        }

        public float OrbitYawDegreesPerPixel { get; }
        public float OrbitPitchDegreesPerPixel { get; }
        public float PanWorldPerPixelAtDistance { get; }
        public float CameraWheelDistanceScale { get; }
        public float CameraWheelZoomSensitivity { get; }
        public float CanvasWheelZoomSensitivity { get; }
        public ESWorkbenchEdgePanSettings EdgePanSettings { get; }
        public float CanvasOverscrollPixels { get; }
        public float PanReferenceViewportHeight { get; }
        public float VerticalFieldOfViewDegrees { get; }
        public float CanvasMinimumZoom { get; }
        public float CanvasMaximumZoom { get; }
        public float CanvasViewportPaddingPixels { get; }
    }

    /// <summary>笔刷、变换、微调和批量拖放的语义分组。</summary>
    public readonly struct ESWorkbenchAuthoringFeelGroup
    {
        public ESWorkbenchAuthoringFeelGroup(
            float strokeSpacingFactor,
            float minimumStrokeSpacing,
            int maximumStrokeSamplesPerEvent,
            float rotationDegreesPerPixel,
            float scaleExponentPerPixel,
            float minimumTransformScale,
            float maximumTransformScale,
            float nudgeWorldUnits,
            float nudgeFineMultiplier,
            float nudgeCoarseMultiplier,
            float minimumDropSpacing)
        {
            StrokeSpacingFactor = strokeSpacingFactor;
            MinimumStrokeSpacing = minimumStrokeSpacing;
            MaximumStrokeSamplesPerEvent = maximumStrokeSamplesPerEvent;
            RotationDegreesPerPixel = rotationDegreesPerPixel;
            ScaleExponentPerPixel = scaleExponentPerPixel;
            MinimumTransformScale = minimumTransformScale;
            MaximumTransformScale = maximumTransformScale;
            NudgeWorldUnits = nudgeWorldUnits;
            NudgeFineMultiplier = nudgeFineMultiplier;
            NudgeCoarseMultiplier = nudgeCoarseMultiplier;
            MinimumDropSpacing = minimumDropSpacing;
        }

        public float StrokeSpacingFactor { get; }
        public float MinimumStrokeSpacing { get; }
        public int MaximumStrokeSamplesPerEvent { get; }
        public float RotationDegreesPerPixel { get; }
        public float ScaleExponentPerPixel { get; }
        public float MinimumTransformScale { get; }
        public float MaximumTransformScale { get; }
        public float NudgeWorldUnits { get; }
        public float NudgeFineMultiplier { get; }
        public float NudgeCoarseMultiplier { get; }
        public float MinimumDropSpacing { get; }
    }

    /// <summary>连续预览的合帧策略分组。</summary>
    public readonly struct ESWorkbenchPreviewFeelGroup
    {
        public ESWorkbenchPreviewFeelGroup(float coalescingDelayMilliseconds)
        {
            CoalescingDelayMilliseconds = coalescingDelayMilliseconds;
        }

        public float CoalescingDelayMilliseconds { get; }
    }

    public sealed class ESWorkbenchViewportFeelSettings
    {
        public static ESWorkbenchViewportFeelSettings Standard { get; } =
            new ESWorkbenchViewportFeelSettings(
                dragStartPixels: 6f,
                orbitYawDegreesPerPixel: 0.35f,
                orbitPitchDegreesPerPixel: 0.25f,
                panWorldPerPixelAtDistance: 0.0018f,
                cameraWheelDistanceScale: 0.75f,
                 canvasWheelZoomSensitivity: 0.035f,
                 strokeSpacingFactor: 0.35f,
                 minimumStrokeSpacing: 0.25f,
                rotationDegreesPerPixel: 0.6f,
                scaleExponentPerPixel: 0.01f,
                maximumPointerDeltaPerEvent: 160f,
                maximumWheelDeltaPerEvent: 4f,
                maximumStrokeSamplesPerEvent: 2048,
                canvasOverscrollPixels: 64f,
                minimumTransformScale: 0.01f,
                maximumTransformScale: 10000f,
                nudgeWorldUnits: 1f,
                nudgeFineMultiplier: 0.1f,
                nudgeCoarseMultiplier: 10f,
                cameraWheelZoomSensitivity: 0.055f,
                minimumDropSpacing: 0.25f,
                panReferenceViewportHeight: 600f,
                verticalFieldOfViewDegrees: 42f,
                canvasMinimumZoom: 0.35f,
                canvasMaximumZoom: 12f,
                canvasViewportPaddingPixels: 16f,
                previewCoalescingDelayMilliseconds: 32f);

        /// <summary>
        /// 供 Prefab、Scene 等宿主选择的语义化手感档位。
        /// 档位只生成配置，不改变输入仲裁或作者事务边界。
        /// </summary>
        public static ESWorkbenchViewportFeelSettings CreatePreset(
            ESWorkbenchViewportFeelPreset preset)
        {
            switch (preset)
            {
                case ESWorkbenchViewportFeelPreset.Precision:
                    return new ESWorkbenchViewportFeelSettings(
                        dragStartPixels: 5f,
                        orbitYawDegreesPerPixel: 0.28f,
                        orbitPitchDegreesPerPixel: 0.2f,
                        canvasWheelZoomSensitivity: 0.028f,
                        strokeSpacingFactor: 0.25f,
                        minimumStrokeSpacing: 0.1f,
                        maximumPointerDeltaPerEvent: 96f,
                        edgePanMaximumPixelsPerSecond: 280f,
                        edgePanResponseExponent: 2.2f,
                        selectionHitRadiusPixels: 12f,
                        previewCoalescingDelayMilliseconds: 24f);
                case ESWorkbenchViewportFeelPreset.RapidAuthoring:
                    return new ESWorkbenchViewportFeelSettings(
                        dragStartPixels: 6f,
                        orbitYawDegreesPerPixel: 0.42f,
                        orbitPitchDegreesPerPixel: 0.3f,
                        canvasWheelZoomSensitivity: 0.045f,
                        strokeSpacingFactor: 0.45f,
                        minimumStrokeSpacing: 0.35f,
                        maximumPointerDeltaPerEvent: 192f,
                        edgePanMaximumPixelsPerSecond: 520f,
                        edgePanResponseExponent: 1.7f,
                        selectionHitRadiusPixels: 10f,
                        previewCoalescingDelayMilliseconds: 32f);
                default:
                    return Standard;
            }
        }

        public ESWorkbenchViewportFeelSettings(
            float dragStartPixels = 6f,
            float orbitYawDegreesPerPixel = 0.35f,
            float orbitPitchDegreesPerPixel = 0.25f,
            float panWorldPerPixelAtDistance = 0.0018f,
            float cameraWheelDistanceScale = 0.75f,
            float canvasWheelZoomSensitivity = 0.035f,
            float strokeSpacingFactor = 0.35f,
            float minimumStrokeSpacing = 0.25f,
            float rotationDegreesPerPixel = 0.6f,
            float scaleExponentPerPixel = 0.01f,
            float maximumPointerDeltaPerEvent = 160f,
            float maximumWheelDeltaPerEvent = 4f,
            int maximumStrokeSamplesPerEvent = 2048,
            float canvasOverscrollPixels = 64f,
            float minimumTransformScale = 0.01f,
            float maximumTransformScale = 10000f,
            float nudgeWorldUnits = 1f,
            float nudgeFineMultiplier = 0.1f,
            float nudgeCoarseMultiplier = 10f,
            float cameraWheelZoomSensitivity = 0.055f,
            float edgePanSizePixels = 48f,
            float edgePanMaximumPixelsPerSecond = 420f,
            float edgePanResponseExponent = 2f,
            float selectionHitRadiusPixels = 10f,
            float minimumDropSpacing = 0.25f,
            float panReferenceViewportHeight = 600f,
            float verticalFieldOfViewDegrees = 42f,
            float canvasMinimumZoom = 0.35f,
            float canvasMaximumZoom = 12f,
            float canvasViewportPaddingPixels = 16f,
            float previewCoalescingDelayMilliseconds = 32f,
            float presentationRadiusScale = 2.8f)
        {
            DragStartPixels = Mathf.Max(0f, FiniteOr(dragStartPixels, 6f));
            OrbitYawDegreesPerPixel = FiniteOr(orbitYawDegreesPerPixel, 0.35f);
            OrbitPitchDegreesPerPixel = FiniteOr(orbitPitchDegreesPerPixel, 0.25f);
            PanWorldPerPixelAtDistance = Mathf.Max(0.00001f, FiniteOr(panWorldPerPixelAtDistance, 0.0018f));
            CameraWheelDistanceScale = Mathf.Max(0.0001f, FiniteOr(cameraWheelDistanceScale, 0.75f));
            CanvasWheelZoomSensitivity = Mathf.Max(0.0001f, FiniteOr(canvasWheelZoomSensitivity, 0.035f));
            StrokeSpacingFactor = Mathf.Max(0.01f, FiniteOr(strokeSpacingFactor, 0.35f));
            MinimumStrokeSpacing = Mathf.Max(0.001f, FiniteOr(minimumStrokeSpacing, 0.25f));
            RotationDegreesPerPixel = FiniteOr(rotationDegreesPerPixel, 0.6f);
            ScaleExponentPerPixel = FiniteOr(scaleExponentPerPixel, 0.01f);
            MaximumPointerDeltaPerEvent = Mathf.Max(
                1f, FiniteOr(maximumPointerDeltaPerEvent, 160f));
            MaximumWheelDeltaPerEvent = Mathf.Max(
                0.1f, FiniteOr(maximumWheelDeltaPerEvent, 4f));
            MaximumStrokeSamplesPerEvent = Mathf.Clamp(maximumStrokeSamplesPerEvent, 1, 8192);
            CanvasOverscrollPixels = Mathf.Clamp(
                FiniteOr(canvasOverscrollPixels, 64f), 0f, 512f);
            MinimumTransformScale = Mathf.Max(0.0001f, FiniteOr(minimumTransformScale, 0.01f));
            MaximumTransformScale = Mathf.Max(
                MinimumTransformScale, FiniteOr(maximumTransformScale, 10000f));
            NudgeWorldUnits = Mathf.Max(0.0001f, FiniteOr(nudgeWorldUnits, 1f));
            NudgeFineMultiplier = Mathf.Clamp(FiniteOr(nudgeFineMultiplier, 0.1f), 0.001f, 1f);
            NudgeCoarseMultiplier = Mathf.Max(1f, FiniteOr(nudgeCoarseMultiplier, 10f));
            CameraWheelZoomSensitivity = Mathf.Clamp(
                FiniteOr(cameraWheelZoomSensitivity, 0.055f), 0.0001f, 1f);
            EdgePanSettings = new ESWorkbenchEdgePanSettings(
                edgePanSizePixels,
                edgePanMaximumPixelsPerSecond,
                edgePanResponseExponent);
            SelectionHitRadiusPixels = Mathf.Clamp(
                FiniteOr(selectionHitRadiusPixels, 10f), 2f, 32f);
            MinimumDropSpacing = Mathf.Max(
                0.001f, FiniteOr(minimumDropSpacing, 0.25f));
            PanReferenceViewportHeight = Mathf.Max(
                64f, FiniteOr(panReferenceViewportHeight, 600f));
            VerticalFieldOfViewDegrees = Mathf.Clamp(
                FiniteOr(verticalFieldOfViewDegrees, 42f), 1f, 170f);
            CanvasMinimumZoom = Mathf.Clamp(
                FiniteOr(canvasMinimumZoom, 0.35f), 0.01f, 1000f);
            CanvasMaximumZoom = Mathf.Max(
                CanvasMinimumZoom, FiniteOr(canvasMaximumZoom, 12f));
            CanvasViewportPaddingPixels = Mathf.Clamp(
                FiniteOr(canvasViewportPaddingPixels, 16f), 0f, 512f);
            PreviewCoalescingDelayMilliseconds = Mathf.Clamp(
                FiniteOr(previewCoalescingDelayMilliseconds, 32f), 0f, 250f);
            PresentationRadiusScale = Mathf.Max(
                0.01f, FiniteOr(presentationRadiusScale, 2.8f));
            Pointer = new ESWorkbenchPointerFeelGroup(
                DragStartPixels,
                MaximumPointerDeltaPerEvent,
                MaximumWheelDeltaPerEvent,
                SelectionHitRadiusPixels);
            Navigation = new ESWorkbenchNavigationFeelGroup(
                OrbitYawDegreesPerPixel,
                OrbitPitchDegreesPerPixel,
                PanWorldPerPixelAtDistance,
                CameraWheelDistanceScale,
                CameraWheelZoomSensitivity,
                CanvasWheelZoomSensitivity,
                EdgePanSettings,
                CanvasOverscrollPixels,
                PanReferenceViewportHeight,
                VerticalFieldOfViewDegrees,
                CanvasMinimumZoom,
                CanvasMaximumZoom,
                CanvasViewportPaddingPixels);
            Authoring = new ESWorkbenchAuthoringFeelGroup(
                StrokeSpacingFactor,
                MinimumStrokeSpacing,
                MaximumStrokeSamplesPerEvent,
                RotationDegreesPerPixel,
                ScaleExponentPerPixel,
                MinimumTransformScale,
                MaximumTransformScale,
                NudgeWorldUnits,
                NudgeFineMultiplier,
                NudgeCoarseMultiplier,
                MinimumDropSpacing);
            Preview = new ESWorkbenchPreviewFeelGroup(
                PreviewCoalescingDelayMilliseconds);
        }

        public float DragStartPixels { get; }
        public float OrbitYawDegreesPerPixel { get; }
        public float OrbitPitchDegreesPerPixel { get; }
        public float PanWorldPerPixelAtDistance { get; }
        public float CameraWheelDistanceScale { get; }
        public float CanvasWheelZoomSensitivity { get; }
        public float StrokeSpacingFactor { get; }
        public float MinimumStrokeSpacing { get; }
        public float RotationDegreesPerPixel { get; }
        public float ScaleExponentPerPixel { get; }
        public float MaximumPointerDeltaPerEvent { get; }
        public float MaximumWheelDeltaPerEvent { get; }
        public int MaximumStrokeSamplesPerEvent { get; }
        /// <summary>画布可保留的轻微越界距离，避免边缘自动平移把内容锁死在屏幕边缘。</summary>
        public float CanvasOverscrollPixels { get; }
        public float MinimumTransformScale { get; }
        public float MaximumTransformScale { get; }
        public float NudgeWorldUnits { get; }
        public float NudgeFineMultiplier { get; }
        public float NudgeCoarseMultiplier { get; }
        /// <summary>3D 轨道相机滚轮指数，由工作台手感配置注入。</summary>
        public float CameraWheelZoomSensitivity { get; }
        /// <summary>边缘平移的边缘宽度、速度上限和响应曲线。</summary>
        public ESWorkbenchEdgePanSettings EdgePanSettings { get; }
        /// <summary>点状作者目标的屏幕命中半径；矩形目标仍优先按其几何范围命中。</summary>
        public float SelectionHitRadiusPixels { get; }
        /// <summary>批量拖放阵列的最小世界间距，预览与正式提交必须共享。</summary>
        public float MinimumDropSpacing { get; }
        /// <summary>3D 平移投影标定高度；用于跨窗口尺寸保持一致的屏幕手感。</summary>
        public float PanReferenceViewportHeight { get; }
        /// <summary>所有 3D 作者视口共享的垂直视场角，避免领域之间的投影手感漂移。</summary>
        public float VerticalFieldOfViewDegrees { get; }
        /// <summary>所有 2D 作者画布共享的最小/最大缩放范围。</summary>
        public float CanvasMinimumZoom { get; }
        public float CanvasMaximumZoom { get; }
        /// <summary>2D 画布内容与视口边缘的统一留白。</summary>
        public float CanvasViewportPaddingPixels { get; }
        /// <summary>
        /// 连续预览（例如笔刷高度预览）的合帧等待时间。它只影响预览投影，
        /// 不延迟正式提交；释放时宿主仍应强制刷新最后一个样本。
        /// </summary>
        public float PreviewCoalescingDelayMilliseconds { get; }
        /// <summary>
        /// 作者轨道状态距离到预览 pose 半径的统一标定。渲染宿主只读取此配置，
        /// 不得复制领域常量，以便不同预览后端保持同一输入手感。
        /// </summary>
        public float PresentationRadiusScale { get; }
        public ESWorkbenchPointerFeelGroup Pointer { get; }
        public ESWorkbenchNavigationFeelGroup Navigation { get; }
        public ESWorkbenchAuthoringFeelGroup Authoring { get; }
        public ESWorkbenchPreviewFeelGroup Preview { get; }

        public float NormalizeWheelDelta(float wheelDelta)
        {
            if (float.IsNaN(wheelDelta) || float.IsInfinity(wheelDelta)) return 0f;
            return Mathf.Clamp(wheelDelta, -MaximumWheelDeltaPerEvent, MaximumWheelDeltaPerEvent);
        }

        public Vector2 NormalizePointerDelta(Vector2 pointerDelta)
        {
            if (float.IsNaN(pointerDelta.x) || float.IsInfinity(pointerDelta.x)
                || float.IsNaN(pointerDelta.y) || float.IsInfinity(pointerDelta.y))
                return Vector2.zero;
            return Vector2.ClampMagnitude(pointerDelta, MaximumPointerDeltaPerEvent);
        }

        /// <summary>
        /// 消费一次指针增量并返回实际消费到的指针位置。
        /// 宿主应保存 consumedPointer 作为下一次基准，避免单事件限幅丢失连续拖动距离。
        /// </summary>
        public bool TryConsumePointerDelta(
            Vector2 previousPointer,
            Vector2 currentPointer,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            return TryConsumePointerDelta(
                previousPointer,
                currentPointer,
                true,
                out delta,
                out consumedPointer);
        }

        /// <summary>连续事件可限幅，释放端点可关闭限幅以精确收敛到最终指针位置。</summary>
        public bool TryConsumePointerDelta(
            Vector2 previousPointer,
            Vector2 currentPointer,
            bool capDelta,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            ESWorkbenchPointerDeltaResolution resolution = ResolvePointerDelta(
                previousPointer, currentPointer, capDelta);
            delta = resolution.ConsumedDelta;
            consumedPointer = resolution.ConsumedPointer;
            return resolution.IsValid;
        }

        /// <summary>
        /// 解析一次指针增量并保留未消费部分。宿主通常只需保存
        /// <see cref="ESWorkbenchPointerDeltaResolution.ConsumedPointer"/>；
        /// 需要诊断或合并输入事件时可直接使用剩余增量。
        /// </summary>
        public ESWorkbenchPointerDeltaResolution ResolvePointerDelta(
            Vector2 previousPointer,
            Vector2 currentPointer,
            bool capDelta = true)
        {
            if (!IsFinite(previousPointer) || !IsFinite(currentPointer))
                return ESWorkbenchPointerDeltaResolution.Invalid(previousPointer);

            Vector2 rawDelta = currentPointer - previousPointer;
            Vector2 consumedDelta = capDelta ? NormalizePointerDelta(rawDelta) : rawDelta;
            Vector2 consumedPointer = previousPointer + consumedDelta;
            if (!IsFinite(rawDelta) || !IsFinite(consumedDelta) || !IsFinite(consumedPointer))
                return ESWorkbenchPointerDeltaResolution.Invalid(previousPointer);

            Vector2 remainingDelta = rawDelta - consumedDelta;
            bool wasCapped = capDelta && remainingDelta.sqrMagnitude > 0.000001f;
            return new ESWorkbenchPointerDeltaResolution(
                rawDelta,
                consumedDelta,
                IsFinite(remainingDelta) ? remainingDelta : Vector2.zero,
                consumedPointer,
                wasCapped,
                true);
        }

        public float ResolveStrokeSpacing(float brushRadius)
        {
            float safeRadius = FiniteOr(brushRadius, 0f);
            return Mathf.Max(MinimumStrokeSpacing, Mathf.Max(0f, safeRadius) * StrokeSpacingFactor);
        }

        /// <summary>
        /// 根据同一组命中手感参数解析点状目标的可视标记半径。
        /// 这样命中容差、悬停反馈和选中反馈不会在不同视口各自漂移。
        /// </summary>
        public float ResolveMarkerRadiusPixels(bool selected, bool hovered)
        {
            float radius = Mathf.Max(3f, SelectionHitRadiusPixels * 0.6f);
            if (hovered) radius += 1.5f;
            if (selected) radius += 2.5f;
            return Mathf.Clamp(radius, 3f, 32f);
        }

        private static float FiniteOr(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 一次指针事件的纯值解析结果。消费增量可能受到单事件限幅，但剩余增量
    /// 必须显式暴露给宿主，以便把 consumedPointer 作为下一次基准而不吞掉轨迹。
    /// final 事件应关闭限幅并让 ConsumedPointer 收敛到当前指针。
    /// </summary>
    public readonly struct ESWorkbenchPointerDeltaResolution
    {
        public ESWorkbenchPointerDeltaResolution(
            Vector2 rawDelta,
            Vector2 consumedDelta,
            Vector2 remainingDelta,
            Vector2 consumedPointer,
            bool wasCapped,
            bool isValid)
        {
            RawDelta = rawDelta;
            ConsumedDelta = consumedDelta;
            RemainingDelta = remainingDelta;
            ConsumedPointer = consumedPointer;
            WasCapped = wasCapped;
            IsValid = isValid;
        }

        public Vector2 RawDelta { get; }
        public Vector2 ConsumedDelta { get; }
        public Vector2 RemainingDelta { get; }
        public Vector2 ConsumedPointer { get; }
        public bool WasCapped { get; }
        public bool IsValid { get; }

        public static ESWorkbenchPointerDeltaResolution Invalid(Vector2 previousPointer) =>
            new ESWorkbenchPointerDeltaResolution(
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                previousPointer,
                false,
                false);
    }

    /// <summary>
    /// 编辑器调度驱动的统一时间步。宿主只提供单调时间戳，暂停恢复、时间倒退、
    /// 无效时间和长帧都在这里收敛，避免每个视口复制一套边缘平移节流逻辑。
    /// </summary>
    public static class ESWorkbenchInputClock
    {
        public const float MinimumDeltaTime = 0.001f;
        public const float MaximumDeltaTime = 0.1f;

        public static float ResolveDeltaTime(double previousTimestamp, double currentTimestamp)
        {
            if (double.IsNaN(previousTimestamp) || double.IsInfinity(previousTimestamp)
                || double.IsNaN(currentTimestamp) || double.IsInfinity(currentTimestamp))
                return MinimumDeltaTime;

            double raw = currentTimestamp - previousTimestamp;
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw <= 0d)
                return MinimumDeltaTime;
            if (raw >= MaximumDeltaTime)
                return MaximumDeltaTime;
            return Mathf.Clamp((float)raw, MinimumDeltaTime, MaximumDeltaTime);
        }
    }

    /// <summary>
    /// 画布拖动/内容拖放共用的边缘平移曲线。它只计算“内容应反向移动多少像素”，
    /// 不持有导航对象，也不负责调度；因此 Scene、World 等宿主可以共用同一合同。
    /// </summary>
    public sealed class ESWorkbenchEdgePanSettings
    {
        public static ESWorkbenchEdgePanSettings Standard { get; } =
            new ESWorkbenchEdgePanSettings(48f, 420f, 2f);

        public ESWorkbenchEdgePanSettings(
            float edgeSizePixels = 48f,
            float maximumPanPixelsPerSecond = 420f,
            float responseExponent = 2f)
        {
            EdgeSizePixels = Mathf.Max(4f, FiniteOr(edgeSizePixels, 48f));
            MaximumPanPixelsPerSecond = Mathf.Max(
                1f, FiniteOr(maximumPanPixelsPerSecond, 420f));
            ResponseExponent = Mathf.Max(1f, FiniteOr(responseExponent, 2f));
        }

        public float EdgeSizePixels { get; }
        public float MaximumPanPixelsPerSecond { get; }
        public float ResponseExponent { get; }

        private static float FiniteOr(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    public sealed class ESWorkbenchEdgePanController
    {
        private readonly ESWorkbenchEdgePanSettings settings;

        public ESWorkbenchEdgePanController(ESWorkbenchEdgePanSettings settings = null)
        {
            this.settings = settings ?? ESWorkbenchEdgePanSettings.Standard;
        }

        public bool Evaluate(
            Rect viewport,
            Vector2 pointer,
            float deltaTime,
            out Vector2 panDelta)
        {
            panDelta = Vector2.zero;
            if (!IsFinite(viewport) || !IsFinite(pointer)
                || !IsFinite(deltaTime) || deltaTime <= 0f
                || viewport.width <= 1f || viewport.height <= 1f)
                return false;

            // 调用方可能在编辑器卡顿或恢复后传入很大的时间步；边缘平移
            // 必须按固定上限推进，避免一次调度把内容瞬移出视口。
            float safeDeltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.1f);

            // 画布内容向指针反方向移动，等价于相机向边缘方向推进。
            float x = ResolveSignedEdgeIntensity(pointer.x, viewport.xMin, viewport.xMax);
            float y = ResolveSignedEdgeIntensity(pointer.y, viewport.yMin, viewport.yMax);
            float scale = settings.MaximumPanPixelsPerSecond * safeDeltaTime;
            panDelta = new Vector2(
                Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), settings.ResponseExponent) * scale,
                Mathf.Sign(y) * Mathf.Pow(Mathf.Abs(y), settings.ResponseExponent) * scale);
            // 角落同时命中两条边时，合速度仍不得超过配置上限；否则对角线
            // 边缘平移会比水平/垂直边缘快 sqrt(2)，产生可感知的突然加速。
            panDelta = Vector2.ClampMagnitude(panDelta, scale);
            if (!IsFinite(panDelta) || panDelta.sqrMagnitude <= 0.000001f)
            {
                panDelta = Vector2.zero;
                return false;
            }
            return true;
        }

        private float ResolveSignedEdgeIntensity(float coordinate, float minimum, float maximum)
        {
            float span = Mathf.Max(0f, maximum - minimum);
            // 窄视口中边缘区不能重叠，否则中心点会被错误判定为某一侧边缘。
            float edgeSize = Mathf.Min(settings.EdgeSizePixels, span * 0.5f);
            if (edgeSize <= 0.0001f) return 0f;
            float fromMinimum = Mathf.Clamp01((edgeSize - (coordinate - minimum)) / edgeSize);
            float fromMaximum = Mathf.Clamp01((edgeSize - (maximum - coordinate)) / edgeSize);
            if (fromMinimum > fromMaximum) return fromMinimum;
            if (fromMaximum > fromMinimum) return -fromMaximum;
            return 0f;
        }

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.width) && IsFinite(value.height);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 边缘平移的纯会话状态。它只保存最后一个屏幕指针、锁轴意图和单调时间戳，
    /// 不持有调度器或视口对象；UI Toolkit、IMGUI 和外部拖放都可以共用同一合同。
    /// </summary>
    public sealed class ESWorkbenchEdgePanSession
    {
        private Vector2 pointer;
        private bool pointerValid;
        private bool lockDominantAxis;
        private double lastTimestamp;

        public bool IsActive => pointerValid;
        public Vector2 Pointer => pointer;
        public bool LockDominantAxis => lockDominantAxis;
        public double LastTimestamp => lastTimestamp;

        public bool Begin(Vector2 nextPointer, bool nextLockDominantAxis, double timestamp)
        {
            if (!IsFinite(nextPointer)) return false;
            pointer = nextPointer;
            lockDominantAxis = nextLockDominantAxis;
            lastTimestamp = IsFinite(timestamp) ? timestamp : 0d;
            pointerValid = true;
            return true;
        }

        public bool UpdatePointer(Vector2 nextPointer, bool nextLockDominantAxis)
        {
            if (!pointerValid || !IsFinite(nextPointer)) return false;
            pointer = nextPointer;
            lockDominantAxis = nextLockDominantAxis;
            return true;
        }

        public bool TryAdvance(double timestamp, out float deltaTime)
        {
            deltaTime = 0f;
            if (!pointerValid || !IsFinite(timestamp)) return false;
            // 编辑器时钟在重载、暂停恢复或测试注入时可能短暂倒退。
            // 倒退帧仍给出最小推进量，但不能污染单调基准；否则下一次正常
            // 时间戳会被解析成长帧，边缘平移突然跳动。
            if (timestamp <= lastTimestamp)
            {
                deltaTime = ESWorkbenchInputClock.MinimumDeltaTime;
                return true;
            }
            deltaTime = ESWorkbenchInputClock.ResolveDeltaTime(lastTimestamp, timestamp);
            lastTimestamp = timestamp;
            return IsFinite(deltaTime) && deltaTime > 0f;
        }

        public bool Stop()
        {
            bool changed = pointerValid;
            pointer = default;
            lockDominantAxis = false;
            lastTimestamp = 0d;
            pointerValid = false;
            return changed;
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>领域无关的连续笔划采样器；按固定间距补齐中间点并保证释放终点落盘。</summary>
    public sealed class ESWorkbenchStrokeSampler
    {
        private bool hasLast;
        private Vector3 lastPoint;
        private Vector3 pendingPoint;
        private bool hasPending;

        public bool HasSample => hasLast;

        public void Reset()
        {
            hasLast = false;
            hasPending = false;
            lastPoint = default;
            pendingPoint = default;
        }

        public int Sample(Vector3 point, float spacing, Action<Vector3> emit, int maximumSamples = 2048)
        {
            if (emit == null || !IsFinite(point)) return 0;
            float safeSpacing = IsFinite(spacing) ? Mathf.Max(0.001f, spacing) : 0.001f;
            int safeMaximumSamples = Mathf.Clamp(maximumSamples, 1, 8192);
            pendingPoint = point;
            hasPending = true;
            if (!hasLast)
            {
                lastPoint = point;
                hasLast = true;
                hasPending = false;
                emit(point);
                return 1;
            }

            Vector3 delta = point - lastPoint;
            float distance = delta.magnitude;
            if (!IsFinite(distance))
            {
                return EmitCapped(lastPoint, point, safeMaximumSamples, emit);
            }
            if (distance < safeSpacing) return 0;
            float normalizedSteps = distance / safeSpacing;
            if (!IsFinite(normalizedSteps) || normalizedSteps > safeMaximumSamples)
            {
                // 超大跨度或极小间距不得把编辑器主线程拖入百万级循环；在本次事件内
                // 均匀降采样并直接收敛到终点，下一事件从终点继续，保证响应优先。
                return EmitCapped(lastPoint, point, safeMaximumSamples, emit);
            }
            Vector3 direction = delta / distance;
            int count = Mathf.FloorToInt(normalizedSteps);
            int emitted = 0;
            for (int i = 1; i <= count; i++)
            {
                lastPoint += direction * safeSpacing;
                emit(lastPoint);
                emitted++;
            }
            hasPending = !Approximately(lastPoint, point);
            return emitted;
        }

        public int Flush(Action<Vector3> emit)
        {
            if (emit == null || !hasPending || !hasLast) return 0;
            if (Approximately(lastPoint, pendingPoint))
            {
                hasPending = false;
                return 0;
            }
            lastPoint = pendingPoint;
            hasPending = false;
            emit(lastPoint);
            return 1;
        }

        private int EmitCapped(Vector3 from, Vector3 to, int sampleCount, Action<Vector3> emit)
        {
            int emitted = 0;
            for (int i = 1; i <= sampleCount; i++)
            {
                lastPoint = Vector3.Lerp(from, to, i / (float)sampleCount);
                if (!IsFinite(lastPoint)) break;
                emit(lastPoint);
                emitted++;
            }
            hasPending = false;
            return emitted;
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.000001f;
        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 领域无关的最新值合帧器。它只保留最后一次输入，不持有调度器或 UI 对象；
    /// 宿主可以用任意时钟唤醒，并在释放/取消时显式收敛或清空。
    /// </summary>
    public sealed class ESWorkbenchLatestValueCoalescer<T>
    {
        private readonly double delaySeconds;
        private T pendingValue;
        private bool hasPending;
        private double dueAt;
        private double lastTimestamp;

        public ESWorkbenchLatestValueCoalescer(float delayMilliseconds)
        {
            float safeDelay = float.IsNaN(delayMilliseconds) || float.IsInfinity(delayMilliseconds)
                ? 0f
                : Mathf.Clamp(delayMilliseconds, 0f, 250f);
            delaySeconds = safeDelay / 1000d;
        }

        public bool HasPending => hasPending;
        public double DueAt => dueAt;
        public double RemainingMilliseconds(double timestamp)
        {
            if (!hasPending) return 0d;
            double normalized = NormalizeTimestamp(timestamp);
            return Math.Max(0d, (dueAt - normalized) * 1000d);
        }

        public void Queue(T value, double timestamp)
        {
            double normalized = NormalizeTimestamp(timestamp);
            pendingValue = value;
            if (!hasPending)
            {
                hasPending = true;
                dueAt = normalized + delaySeconds;
            }
        }

        public bool TryConsume(double timestamp, out T value)
        {
            value = default(T);
            if (!hasPending) return false;
            double normalized = NormalizeTimestamp(timestamp);
            if (normalized + 0.0000001d < dueAt) return false;
            value = pendingValue;
            pendingValue = default(T);
            hasPending = false;
            dueAt = 0d;
            lastTimestamp = normalized;
            return true;
        }

        public bool Flush(out T value)
        {
            value = default(T);
            if (!hasPending) return false;
            value = pendingValue;
            pendingValue = default(T);
            hasPending = false;
            dueAt = 0d;
            return true;
        }

        public void Cancel()
        {
            pendingValue = default(T);
            hasPending = false;
            dueAt = 0d;
        }

        private double NormalizeTimestamp(double timestamp)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp))
                timestamp = lastTimestamp;
            if (timestamp < lastTimestamp) timestamp = lastTimestamp;
            lastTimestamp = timestamp;
            return timestamp;
        }
    }

    public enum ESWorkbenchPointerDragPhase : byte
    {
        Idle,
        Armed,
        Started
    }

    /// <summary>领域无关的指针拖动手势状态；只判定点击/拖动互斥，不持有 VisualElement 或业务数据。</summary>
    public sealed class ESWorkbenchPointerDragState
    {
        public const float DefaultThreshold = 6f;
        private readonly float startThreshold;

        public ESWorkbenchPointerDragState(float threshold = DefaultThreshold)
        {
            startThreshold = IsFinite(threshold) ? Mathf.Max(0f, threshold) : DefaultThreshold;
        }

        public ESWorkbenchPointerDragPhase Phase { get; private set; }
        public int PointerId { get; private set; } = -1;
        public Vector2 StartPosition { get; private set; }

        public bool IsActive => Phase != ESWorkbenchPointerDragPhase.Idle;
        public bool IsStarted => Phase == ESWorkbenchPointerDragPhase.Started;

        public bool Arm(int pointerId, Vector2 position)
        {
            // 一个拖动源只能由一个主指针持有。多指针、重入 PointerDown 或
            // 编辑器控件抢占时不得覆盖既有锚点，否则会出现跳手和错误释放。
            if (IsActive || pointerId < 0 || !IsFinite(position)) return false;
            PointerId = pointerId;
            StartPosition = position;
            Phase = ESWorkbenchPointerDragPhase.Armed;
            return true;
        }

        public bool ShouldStart(int pointerId, Vector2 position, float threshold = -1f)
        {
            if (Phase != ESWorkbenchPointerDragPhase.Armed || pointerId != PointerId || !IsFinite(position))
                return false;
            float safeThreshold = !IsFinite(threshold) || threshold < 0f
                ? startThreshold
                : Mathf.Max(0f, threshold);
            return (position - StartPosition).sqrMagnitude >= safeThreshold * safeThreshold;
        }

        public bool MarkStarted(int pointerId)
        {
            if (Phase != ESWorkbenchPointerDragPhase.Armed || pointerId != PointerId) return false;
            Phase = ESWorkbenchPointerDragPhase.Started;
            return true;
        }

        public bool ShouldClick(int pointerId, Vector2 position, float threshold = -1f)
        {
            if (Phase != ESWorkbenchPointerDragPhase.Armed || pointerId != PointerId || !IsFinite(position))
                return false;
            float safeThreshold = !IsFinite(threshold) || threshold < 0f
                ? startThreshold
                : Mathf.Max(0f, threshold);
            return (position - StartPosition).sqrMagnitude < safeThreshold * safeThreshold;
        }

        public int Reset()
        {
            int previousPointerId = PointerId;
            PointerId = -1;
            StartPosition = default;
            Phase = ESWorkbenchPointerDragPhase.Idle;
            return previousPointerId;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 面板尺寸拖动的领域无关会话。尺寸本身由宿主读取和应用，本合同只负责
    /// 绑定起始尺寸、指针身份以及提交/取消的终态，避免 PointerCaptureOut 把
    /// 半截布局误当成一次正常提交。
    /// </summary>
    public sealed class ESWorkbenchPaneResizeSession
    {
        private float startDimension;

        public bool IsActive { get; private set; }
        public int PointerId { get; private set; } = -1;
        public float StartDimension => startDimension;

        public bool Begin(int pointerId, float dimension)
        {
            if (IsActive || pointerId < 0 || !IsFinite(dimension)) return false;
            PointerId = pointerId;
            startDimension = dimension;
            IsActive = true;
            return true;
        }

        public bool Owns(int pointerId) => IsActive && PointerId == pointerId;

        public bool TryCommit(int pointerId, float currentDimension, out float before, out float after)
        {
            before = default;
            after = default;
            if (!Owns(pointerId) || !IsFinite(currentDimension)) return false;
            before = startDimension;
            after = currentDimension;
            Reset();
            return true;
        }

        public bool TryCancel(int pointerId, out float restoreDimension)
        {
            restoreDimension = default;
            if (!Owns(pointerId)) return false;
            restoreDimension = startDimension;
            Reset();
            return true;
        }

        public void Reset()
        {
            IsActive = false;
            PointerId = -1;
            startDimension = default;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 宿主级指针所有权闸门。
    /// 每个内容源可以有自己的 Armed 状态，但一个工作台在同一时刻只能让一个
    /// 指针进入内容拖动预备态，避免多指针/重入事件把点击和拖放交叉解释。
    /// </summary>
    public sealed class ESWorkbenchPointerOwnershipGate
    {
        private object owner;
        private int pointerId = -1;

        public bool IsOwned => owner != null && pointerId >= 0;
        public int PointerId => pointerId;

        public bool TryAcquire(object ownerToken, int nextPointerId)
        {
            if (ownerToken == null || nextPointerId < 0) return false;
            if (IsOwned) return ReferenceEquals(owner, ownerToken)
                && pointerId == nextPointerId;
            owner = ownerToken;
            pointerId = nextPointerId;
            return true;
        }

        public bool Owns(object ownerToken, int candidatePointerId) =>
            IsOwned && ReferenceEquals(owner, ownerToken) && pointerId == candidatePointerId;

        public bool Release(object ownerToken, int candidatePointerId)
        {
            if (!Owns(ownerToken, candidatePointerId)) return false;
            Reset();
            return true;
        }

        public bool Reset()
        {
            bool changed = IsOwned;
            owner = null;
            pointerId = -1;
            return changed;
        }
    }

    public enum ESWorkbenchPointerOwnerKind : byte
    {
        None,
        Content,
        Viewport,
        Orbit,
        PaneResize,
        ExternalContent
    }

    public readonly struct ESWorkbenchPointerInteractionSnapshot
    {
        internal ESWorkbenchPointerInteractionSnapshot(
            ESWorkbenchPointerOwnerKind ownerKind,
            int pointerId,
            bool isActive)
        {
            OwnerKind = ownerKind;
            PointerId = pointerId;
            IsActive = isActive;
        }

        public ESWorkbenchPointerOwnerKind OwnerKind { get; }
        public int PointerId { get; }
        public bool IsActive { get; }
        public bool IsExternalContent => OwnerKind == ESWorkbenchPointerOwnerKind.ExternalContent;
    }

    /// <summary>
    /// 工作台宿主级主指针仲裁器。
    /// 内容卡片、视口移动/绘制、轨道相机和外部拖放不能只依赖事件冒泡顺序；它们
    /// 必须在同一个仲裁器中登记 owner，才能让后续贡献视口复用相同的互斥和转移语义。
    /// </summary>
    public sealed class ESWorkbenchPointerInteractionCoordinator
    {
        private object owner;
        private int pointerId = -1;
        private ESWorkbenchPointerOwnerKind ownerKind;

        public ESWorkbenchPointerInteractionSnapshot Snapshot =>
            new ESWorkbenchPointerInteractionSnapshot(ownerKind, pointerId, IsActive);

        public bool IsActive => owner != null && ownerKind != ESWorkbenchPointerOwnerKind.None;
        public bool IsExternalContentActive =>
            IsActive && ownerKind == ESWorkbenchPointerOwnerKind.ExternalContent;
        public ESWorkbenchPointerOwnerKind OwnerKind => ownerKind;
        public int PointerId => pointerId;

        public bool TryAcquire(
            object ownerToken,
            int nextPointerId,
            ESWorkbenchPointerOwnerKind nextOwnerKind)
        {
            if (ownerToken == null || nextPointerId < 0
                || nextOwnerKind == ESWorkbenchPointerOwnerKind.None
                || nextOwnerKind == ESWorkbenchPointerOwnerKind.ExternalContent)
                return false;
            if (IsActive)
                return ReferenceEquals(owner, ownerToken)
                    && pointerId == nextPointerId
                    && ownerKind == nextOwnerKind;
            owner = ownerToken;
            pointerId = nextPointerId;
            ownerKind = nextOwnerKind;
            return true;
        }

        public bool Owns(
            object ownerToken,
            int candidatePointerId,
            ESWorkbenchPointerOwnerKind expectedOwnerKind)
        {
            return IsActive
                && !IsExternalContentActive
                && ReferenceEquals(owner, ownerToken)
                && pointerId == candidatePointerId
                && ownerKind == expectedOwnerKind;
        }

        public bool TryBeginExternalContent(object externalOwnerToken)
        {
            if (externalOwnerToken == null) return false;
            if (IsExternalContentActive)
                return ReferenceEquals(owner, externalOwnerToken);
            if (IsActive) return false;
            owner = externalOwnerToken;
            pointerId = -1;
            ownerKind = ESWorkbenchPointerOwnerKind.ExternalContent;
            return true;
        }

        public bool TryPromoteToExternalContent(
            object currentOwnerToken,
            int currentPointerId,
            object externalOwnerToken)
        {
            if (externalOwnerToken == null
                || !Owns(currentOwnerToken, currentPointerId, ESWorkbenchPointerOwnerKind.Content))
                return false;
            owner = externalOwnerToken;
            pointerId = -1;
            ownerKind = ESWorkbenchPointerOwnerKind.ExternalContent;
            return true;
        }

        public bool Release(
            object ownerToken,
            int candidatePointerId,
            ESWorkbenchPointerOwnerKind expectedOwnerKind)
        {
            if (!Owns(ownerToken, candidatePointerId, expectedOwnerKind)) return false;
            Reset();
            return true;
        }

        public bool EndExternalContent(object externalOwnerToken)
        {
            if (!IsExternalContentActive || !ReferenceEquals(owner, externalOwnerToken)) return false;
            Reset();
            return true;
        }

        public bool ResetIfOwnerKind(ESWorkbenchPointerOwnerKind expectedOwnerKind)
        {
            if (!IsActive || ownerKind != expectedOwnerKind) return false;
            return Reset();
        }

        public bool Reset()
        {
            bool changed = IsActive;
            owner = null;
            pointerId = -1;
            ownerKind = ESWorkbenchPointerOwnerKind.None;
            return changed;
        }
    }

    public enum ESWorkbenchContentDragSource : byte
    {
        Unknown,
        ContentCard,
        ObjectRow,
        ExternalAsset
    }

    public enum ESWorkbenchContentDragEndReason : byte
    {
        Commit,
        Cancel,
        CaptureLost,
        Invalidated,
        Deactivate
    }

    /// <summary>
    /// 内容目录拖放的公共会话合同。
    /// 它统一卡片、列表行和外部资源的阈值、来源、批次快照及幂等结束，
    /// 但不持有 VisualElement、Unity DragAndDrop 或领域创建事务。
    /// </summary>
    public sealed class ESWorkbenchContentDragSession
    {
        private readonly ESWorkbenchPointerDragState pointer;
        private readonly ESWorkbenchContentDragSource source;
        private ESWorkbenchObjectDescriptor primaryItem;
        private IReadOnlyList<ESWorkbenchObjectDescriptor> items =
            Array.Empty<ESWorkbenchObjectDescriptor>();

        public ESWorkbenchContentDragSession(
            ESWorkbenchContentDragSource source,
            float threshold = ESWorkbenchPointerDragState.DefaultThreshold)
        {
            this.source = source;
            pointer = new ESWorkbenchPointerDragState(threshold);
        }

        public ESWorkbenchContentDragSource Source => source;
        public ESWorkbenchPointerDragPhase Phase => pointer.Phase;
        public int PointerId => pointer.PointerId;
        public Vector2 StartPosition => pointer.StartPosition;
        public bool IsActive => pointer.IsActive;
        public bool IsStarted => pointer.IsStarted;
        public ESWorkbenchObjectDescriptor PrimaryItem => primaryItem;
        public IReadOnlyList<ESWorkbenchObjectDescriptor> Items => items;
        public bool HasEndReason { get; private set; }
        public ESWorkbenchContentDragEndReason LastEndReason { get; private set; }

        public bool Arm(
            int pointerId,
            Vector2 position,
            ESWorkbenchObjectDescriptor item)
        {
            if (item == null || !pointer.Arm(pointerId, position)) return false;
            primaryItem = item;
            items = Array.Empty<ESWorkbenchObjectDescriptor>();
            HasEndReason = false;
            return true;
        }

        public bool ShouldStart(int pointerId, Vector2 position, float threshold = -1f) =>
            pointer.ShouldStart(pointerId, position, threshold);

        public bool TryStart(
            int pointerId,
            Vector2 position,
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch)
        {
            if (!pointer.ShouldStart(pointerId, position) || item == null) return false;
            if (!pointer.MarkStarted(pointerId)) return false;
            primaryItem = item;
            items = SnapshotBatch(item, batch);
            return true;
        }

        public bool ShouldClick(int pointerId, Vector2 position, float threshold = -1f) =>
            pointer.ShouldClick(pointerId, position, threshold);

        public bool End(ESWorkbenchContentDragEndReason reason)
        {
            if (!pointer.IsActive) return false;
            LastEndReason = reason;
            HasEndReason = true;
            pointer.Reset();
            primaryItem = null;
            items = Array.Empty<ESWorkbenchObjectDescriptor>();
            return true;
        }

        public int Reset()
        {
            int previousPointerId = pointer.Reset();
            primaryItem = null;
            items = Array.Empty<ESWorkbenchObjectDescriptor>();
            HasEndReason = false;
            return previousPointerId;
        }

        private static IReadOnlyList<ESWorkbenchObjectDescriptor> SnapshotBatch(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch)
        {
            var snapshot = new List<ESWorkbenchObjectDescriptor>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Add(item, snapshot, ids);
            if (batch != null)
            {
                for (int i = 0; i < batch.Count; i++) Add(batch[i], snapshot, ids);
            }
            return snapshot.ToArray();
        }

        private static void Add(
            ESWorkbenchObjectDescriptor item,
            List<ESWorkbenchObjectDescriptor> snapshot,
            HashSet<string> ids)
        {
            if (item == null || string.IsNullOrEmpty(item.BaseObjectId)
                || !ids.Add(item.BaseObjectId)) return;
            snapshot.Add(item);
        }
    }

    /// <summary>
    /// 视口主指针手势的领域无关生命周期。
    /// 它只拥有“谁在操作”和“是否已经越过拖动阈值”，不决定提交、回滚或预览数据。
    /// UI Toolkit 与 IMGUI 都可以用同一合同阻止平移、变换和笔刷互相抢占。
    /// </summary>
    public sealed class ESWorkbenchPointerGestureSession
    {
        public enum Kind : byte
        {
            None,
            Pan,
            Move,
            Transform,
            Paint,
            ExternalContent,
            Orbit
        }

        public enum EndReason : byte
        {
            Commit,
            Cancel,
            CaptureLost,
            ExternalDrag,
            Deactivate
        }

        public readonly struct AdvanceResult
        {
            internal AdvanceResult(
                bool ownsPointer,
                bool started,
                bool startedNow,
                Vector2 delta,
                Vector2 consumedPointer)
            {
                OwnsPointer = ownsPointer;
                IsStarted = started;
                StartedNow = startedNow;
                Delta = delta;
                ConsumedPointer = consumedPointer;
            }

            public bool OwnsPointer { get; }
            public bool IsStarted { get; }
            public bool StartedNow { get; }
            public Vector2 Delta { get; }
            public Vector2 ConsumedPointer { get; }
        }

        private readonly ESWorkbenchPointerDragState drag;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private Vector2 lastPointer;
        private bool hasPointer;

        public ESWorkbenchPointerGestureSession(
            float threshold = ESWorkbenchPointerDragState.DefaultThreshold,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            drag = new ESWorkbenchPointerDragState(threshold);
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
        }

        public Kind ActiveKind { get; private set; }
        public int PointerId => drag.PointerId;
        public Vector2 StartPosition => drag.StartPosition;
        public bool IsActive => ActiveKind != Kind.None && drag.IsActive;
        public bool IsStarted => IsActive && drag.IsStarted;
        public bool HasEndReason { get; private set; }
        public EndReason LastEndReason { get; private set; }

        public bool TryArm(Kind kind, int pointerId, Vector2 position)
        {
            if (kind == Kind.None || IsActive) return false;
            if (!drag.Arm(pointerId, position)) return false;
            ActiveKind = kind;
            HasEndReason = false;
            lastPointer = position;
            hasPointer = true;
            return true;
        }

        public bool Owns(Kind kind, int pointerId) =>
            IsActive && ActiveKind == kind && PointerId == pointerId;

        public bool TryStart(int pointerId, Vector2 position)
        {
            if (!IsActive || pointerId != PointerId || !drag.ShouldStart(pointerId, position))
                return false;
            return drag.MarkStarted(pointerId);
        }

        /// <summary>
        /// 确保当前指针手势已经越过阈值。已启动手势直接返回 true；尚未越阈值时
        /// 保持 Armed，不会被误判为捕获丢失。适配器应使用此方法表达“继续更新”语义，
        /// 只有需要判断首次越阈值的代码才直接调用 TryStart。
        /// </summary>
        public bool TryEnsureStarted(int pointerId, Vector2 position)
        {
            if (!IsActive || pointerId != PointerId) return false;
            return IsStarted || TryStart(pointerId, position);
        }

        /// <summary>
        /// 在同一事件内完成“越过阈值并消费首帧增量”。宿主不应分别解释
        /// TryStart 和 TryConsume，否则新视口很容易漏掉越阈值事件的第一段位移。
        /// 未越过阈值时返回 false，且不会改变已消费指针位置。
        /// </summary>
        public bool TryStartAndConsumePointerDelta(
            int pointerId,
            Vector2 currentPointer,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            delta = Vector2.zero;
            consumedPointer = lastPointer;
            if (!IsActive || pointerId != PointerId) return false;
            if (!TryEnsureStarted(pointerId, currentPointer)) return false;
            return TryConsumePointerDelta(
                pointerId, currentPointer, out delta, out consumedPointer);
        }

        /// <summary>释放事件的无损版本，保证最终端点仍收敛到 PointerUp。</summary>
        public bool TryStartAndConsumePointerDeltaFinal(
            int pointerId,
            Vector2 currentPointer,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            delta = Vector2.zero;
            consumedPointer = lastPointer;
            if (!IsActive || pointerId != PointerId) return false;
            if (!TryEnsureStarted(pointerId, currentPointer)) return false;
            return TryConsumePointerDeltaFinal(
                pointerId, currentPointer, out delta, out consumedPointer);
        }

        /// <summary>
        /// 结构化推进一次主指针手势：统一校验所有权、越过阈值和首帧位移消费。
        /// `final` 为 true 时关闭事件限幅，确保 PointerUp 精确收敛到终点。
        /// </summary>
        public bool TryAdvance(
            int pointerId,
            Vector2 currentPointer,
            bool final,
            out AdvanceResult result)
        {
            result = default;
            bool ownsPointer = IsActive && pointerId == PointerId;
            if (!ownsPointer) return false;
            bool wasStarted = IsStarted;
            Vector2 resolvedDelta;
            Vector2 resolvedConsumed;
            bool advanced;
            if (final)
            {
                advanced = TryStartAndConsumePointerDeltaFinal(
                    pointerId, currentPointer, out resolvedDelta, out resolvedConsumed);
            }
            else
            {
                advanced = TryStartAndConsumePointerDelta(
                    pointerId, currentPointer, out resolvedDelta, out resolvedConsumed);
            }
            result = new AdvanceResult(
                ownsPointer,
                IsStarted,
                !wasStarted && IsStarted,
                resolvedDelta,
                resolvedConsumed);
            return advanced;
        }

        /// <summary>
        /// 消费当前手势的一次连续指针增量。事件过大时只消费配置上限，
        /// 下一次调用继续从 consumedPointer 收敛，避免跳手又不丢失总位移。
        /// </summary>
        public bool TryConsumePointerDelta(
            int pointerId,
            Vector2 currentPointer,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            return TryConsumePointerDelta(
                pointerId, currentPointer, true, out delta, out consumedPointer);
        }

        /// <summary>释放端点使用无损增量，确保最终位置精确到 MouseUp/PointerUp。</summary>
        public bool TryConsumePointerDeltaFinal(
            int pointerId,
            Vector2 currentPointer,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            return TryConsumePointerDelta(
                pointerId, currentPointer, false, out delta, out consumedPointer);
        }

        private bool TryConsumePointerDelta(
            int pointerId,
            Vector2 currentPointer,
            bool capDelta,
            out Vector2 delta,
            out Vector2 consumedPointer)
        {
            delta = Vector2.zero;
            consumedPointer = lastPointer;
            if (!IsStarted || !hasPointer || pointerId != PointerId)
                return false;
            if (!feel.TryConsumePointerDelta(
                    lastPointer,
                    currentPointer,
                    capDelta,
                    out delta,
                    out consumedPointer))
                return false;
            lastPointer = consumedPointer;
            return true;
        }

        public bool Finish(EndReason reason)
        {
            if (!IsActive) return false;
            LastEndReason = reason;
            HasEndReason = true;
            ActiveKind = Kind.None;
            drag.Reset();
            lastPointer = default;
            hasPointer = false;
            return true;
        }

        /// <summary>
        /// 只允许当前拥有者结束手势。事件宿主应优先使用此入口处理
        /// PointerUp/PointerCaptureOut，避免错误指针把另一笔操作提前终止。
        /// </summary>
        public bool TryFinishOwned(int pointerId, EndReason reason)
        {
            return IsActive && pointerId == PointerId && Finish(reason);
        }

        public bool Cancel(EndReason reason = EndReason.Cancel) => Finish(reason);

        public bool TryCancelOwned(int pointerId, EndReason reason = EndReason.Cancel) =>
            TryFinishOwned(pointerId, reason);
    }

    /// <summary>
    /// 捕获丢失时领域作者操作的明确策略。变换预览通常应回滚，已经逐点写入
    /// 草稿的连续笔刷则可以冲刷尾点并结束当前 Undo 笔划；两者不能由视口自行猜测。
    /// </summary>
    public enum ESWorkbenchCaptureLossPolicy : byte
    {
        CancelPreview,
        CommitPendingSamples
    }

    public readonly struct ESWorkbenchGestureTerminationDecision
    {
        private ESWorkbenchGestureTerminationDecision(
            ESWorkbenchPointerGestureSession.EndReason reason,
            bool flushPendingSamples,
            bool commitAuthoring,
            bool restorePreview)
        {
            Reason = reason;
            FlushPendingSamples = flushPendingSamples;
            CommitAuthoring = commitAuthoring;
            RestorePreview = restorePreview;
        }

        public ESWorkbenchPointerGestureSession.EndReason Reason { get; }
        public bool FlushPendingSamples { get; }
        public bool CommitAuthoring { get; }
        public bool RestorePreview { get; }

        public static ESWorkbenchGestureTerminationDecision Resolve(
            ESWorkbenchPointerGestureSession.EndReason reason,
            ESWorkbenchCaptureLossPolicy captureLossPolicy,
            bool hasPreview)
        {
            switch (reason)
            {
                case ESWorkbenchPointerGestureSession.EndReason.Commit:
                    return new ESWorkbenchGestureTerminationDecision(
                        reason, flushPendingSamples: true,
                        commitAuthoring: true, restorePreview: false);
                case ESWorkbenchPointerGestureSession.EndReason.CaptureLost:
                    if (captureLossPolicy == ESWorkbenchCaptureLossPolicy.CommitPendingSamples)
                        return new ESWorkbenchGestureTerminationDecision(
                            reason, flushPendingSamples: true,
                            commitAuthoring: true, restorePreview: false);
                    return new ESWorkbenchGestureTerminationDecision(
                        reason, flushPendingSamples: false,
                        commitAuthoring: false, restorePreview: hasPreview);
                default:
                    return new ESWorkbenchGestureTerminationDecision(
                        reason, flushPendingSamples: false,
                        commitAuthoring: false, restorePreview: hasPreview);
            }
        }
    }

    [Flags]
    public enum ESWorkbenchMoveAxes : byte
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        Horizontal = X | Z,
        All = X | Y | Z
    }

    /// <summary>
    /// 对象移动的领域无关抓取锚点。移动目标由对象起点加指针世界位移得到，
    /// 避免从大型对象边缘开始拖动时把对象中心瞬间吸到指针。
    /// </summary>
    public sealed class ESWorkbenchMoveGestureAnchor
    {
        private Vector3 objectStart;
        private Vector3 pointerStart;
        private ESWorkbenchMoveAxes lockedDominantAxis;

        public bool IsValid { get; private set; }
        public Vector3 ObjectStart => objectStart;
        public Vector3 PointerStart => pointerStart;

        public bool Capture(Vector3 objectPosition, Vector3 pointerWorldPosition)
        {
            if (!IsFinite(objectPosition) || !IsFinite(pointerWorldPosition)) return false;
            objectStart = objectPosition;
            pointerStart = pointerWorldPosition;
            lockedDominantAxis = ESWorkbenchMoveAxes.None;
            IsValid = true;
            return true;
        }

        /// <summary>拖动越过误触阈值时重设指针基准，但保持对象起点不变，避免起拖首帧跳变。</summary>
        public bool RebasePointer(Vector3 pointerWorldPosition)
        {
            if (!IsValid || !IsFinite(pointerWorldPosition)) return false;
            pointerStart = pointerWorldPosition;
            lockedDominantAxis = ESWorkbenchMoveAxes.None;
            return true;
        }

        public bool TryResolve(
            Vector3 pointerWorldPosition,
            Func<Vector3, Vector3> snapPosition,
            out Vector3 objectPosition)
        {
            return TryResolve(
                pointerWorldPosition,
                snapPosition,
                ESWorkbenchMoveAxes.All,
                false,
                out objectPosition);
        }

        public bool TryResolve(
            Vector3 pointerWorldPosition,
            Func<Vector3, Vector3> snapPosition,
            ESWorkbenchMoveAxes allowedAxes,
            bool lockDominantAxis,
            out Vector3 objectPosition)
        {
            objectPosition = default;
            ESWorkbenchMoveAxes supportedAxes = allowedAxes & ESWorkbenchMoveAxes.All;
            if (!IsValid || supportedAxes == ESWorkbenchMoveAxes.None
                || !IsFinite(pointerWorldPosition)) return false;
            Vector3 delta = pointerWorldPosition - pointerStart;
            if (!IsFinite(delta)) return false;
            if (lockDominantAxis)
            {
                if (lockedDominantAxis == ESWorkbenchMoveAxes.None)
                    lockedDominantAxis = ResolveDominantAxis(delta, supportedAxes);
                if (lockedDominantAxis == ESWorkbenchMoveAxes.None) return false;
                supportedAxes &= lockedDominantAxis;
            }
            else
            {
                lockedDominantAxis = ESWorkbenchMoveAxes.None;
            }
            Vector3 candidate = objectStart + FilterAxes(delta, supportedAxes);
            if (!IsFinite(candidate)) return false;
            candidate = snapPosition?.Invoke(candidate) ?? candidate;
            if (!IsFinite(candidate)) return false;
            candidate = objectStart + FilterAxes(candidate - objectStart, supportedAxes);
            objectPosition = candidate;
            return true;
        }

        public void Reset()
        {
            objectStart = default;
            pointerStart = default;
            lockedDominantAxis = ESWorkbenchMoveAxes.None;
            IsValid = false;
        }

        private static ESWorkbenchMoveAxes ResolveDominantAxis(
            Vector3 delta,
            ESWorkbenchMoveAxes allowedAxes)
        {
            float x = (allowedAxes & ESWorkbenchMoveAxes.X) != 0 ? Mathf.Abs(delta.x) : -1f;
            float y = (allowedAxes & ESWorkbenchMoveAxes.Y) != 0 ? Mathf.Abs(delta.y) : -1f;
            float z = (allowedAxes & ESWorkbenchMoveAxes.Z) != 0 ? Mathf.Abs(delta.z) : -1f;
            if (Mathf.Max(x, Mathf.Max(y, z)) <= 0.000001f) return ESWorkbenchMoveAxes.None;
            if (x >= y && x >= z) return ESWorkbenchMoveAxes.X;
            return y >= z ? ESWorkbenchMoveAxes.Y : ESWorkbenchMoveAxes.Z;
        }

        private static Vector3 FilterAxes(Vector3 value, ESWorkbenchMoveAxes axes)
        {
            return new Vector3(
                (axes & ESWorkbenchMoveAxes.X) != 0 ? value.x : 0f,
                (axes & ESWorkbenchMoveAxes.Y) != 0 ? value.y : 0f,
                (axes & ESWorkbenchMoveAxes.Z) != 0 ? value.z : 0f);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    /// <summary>领域无关的单项/批量拖放阵列布局；悬停预览与正式提交必须共享此算法。</summary>
    public static class ESWorkbenchDropLayout
    {
        public static void FillGridPositions(
            Vector3 anchor,
            int count,
            float spacing,
            Func<Vector3, Vector3> snapPosition,
            IList<Vector3> output,
            float minimumSpacing = 0.25f)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (count <= 0) return;

            float safeMinimumSpacing = !float.IsNaN(minimumSpacing)
                && !float.IsInfinity(minimumSpacing)
                ? Mathf.Max(0.001f, minimumSpacing)
                : 0.25f;
            float safeSpacing = !float.IsNaN(spacing) && !float.IsInfinity(spacing)
                ? Mathf.Max(safeMinimumSpacing, spacing)
                : safeMinimumSpacing;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Vector3 position = anchor + new Vector3(
                    (column - (columns - 1) * 0.5f) * safeSpacing,
                    0f,
                    (row - (rows - 1) * 0.5f) * safeSpacing);
                output.Add(snapPosition?.Invoke(position) ?? position);
            }
        }
    }

    /// <summary>领域无关的二维画布导航与 XZ 世界坐标投影。</summary>
    public sealed class ESWorkbenchCanvasNavigationState
    {
        private readonly ESWorkbenchViewportLayoutState layout;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly float minimumZoom;
        private readonly float maximumZoom;
        private readonly float viewportPadding;

        public ESWorkbenchCanvasNavigationState(
            ESWorkbenchViewportLayoutState layout,
            float minimumZoom = 0.35f,
            float maximumZoom = 12f,
            float viewportPadding = 16f,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            this.minimumZoom = Mathf.Max(0.01f, FiniteOr(minimumZoom, 0.35f));
            this.maximumZoom = Mathf.Max(this.minimumZoom, FiniteOr(maximumZoom, 12f));
            this.viewportPadding = Mathf.Max(0f, FiniteOr(viewportPadding, 16f));
            Pan = IsFinite(layout.pan) ? layout.pan : Vector2.zero;
            Zoom = IsFinite(layout.zoom) && layout.zoom > 0f
                ? Mathf.Clamp(layout.zoom, this.minimumZoom, this.maximumZoom)
                : 1f;
            Save();
        }

        public Vector2 Pan { get; private set; }
        public float Zoom { get; private set; }

        public void Reset()
        {
            Pan = Vector2.zero;
            Zoom = 1f;
            Save();
        }

        public void PanBy(Vector2 delta)
        {
            if (!IsFinite(delta)) return;
            Vector2 candidate = Pan + delta;
            if (!IsFinite(candidate)) return;
            Pan = candidate;
            Save();
        }

        /// <summary>
        /// 把画布限制在当前视口附近，保留有限越界以避免拖动贴边时产生硬碰撞感。
        /// 该方法只约束导航状态，不修改任何作者数据。
        /// </summary>
        public void ConstrainPan(Rect viewport, Rect worldBounds, float overscrollPixels = 64f)
        {
            if (!IsFinite(viewport) || !IsFinite(worldBounds)
                || viewport.width <= 1f || viewport.height <= 1f)
                return;
            float overscroll = Mathf.Clamp(
                IsFinite(overscrollPixels) ? overscrollPixels : 64f, 0f, 512f);
            Rect canvas = ResolveCanvasBounds(viewport, worldBounds);
            Vector2 correction = Vector2.zero;
            correction.x = ResolvePanCorrection(
                canvas.xMin, canvas.xMax, viewport.xMin, viewport.xMax,
                canvas.width <= viewport.width, viewport.center.x, overscroll);
            correction.y = ResolvePanCorrection(
                canvas.yMin, canvas.yMax, viewport.yMin, viewport.yMax,
                canvas.height <= viewport.height, viewport.center.y, overscroll);
            if (IsFinite(correction) && correction.sqrMagnitude > 0.000001f)
            {
                Pan += correction;
                Save();
            }
        }

        public void ZoomAt(Vector2 canvasPoint, float wheelDelta, Rect viewport, Rect worldBounds)
        {
            if (!IsFinite(canvasPoint) || !IsFinite(wheelDelta)) return;
            Rect before = ResolveCanvasBounds(viewport, worldBounds);
            // 指针可能落在内容矩形外的视口留白区。这里不能使用会夹断到
            // [0,1] 的 InverseLerp，否则留白区滚轮会把内容吸到最近边缘，
            // 产生明显的“跳手”；非夹断参数才能保持指针锚点连续。
            Vector2 normalized = new Vector2(
                ResolveInverseLerpUnclamped(before.xMin, before.xMax, canvasPoint.x),
                ResolveInverseLerpUnclamped(before.yMin, before.yMax, canvasPoint.y));
            // 正值滚轮表示向后滚动，2D 画布缩小；与 3D 相机距离增大保持一致。
            float normalizedWheelDelta = this.feel.NormalizeWheelDelta(wheelDelta);
            Zoom = Mathf.Clamp(
                Zoom * Mathf.Exp(-normalizedWheelDelta * this.feel.CanvasWheelZoomSensitivity),
                minimumZoom,
                maximumZoom);
            Rect after = ResolveCanvasBounds(viewport, worldBounds);
            Vector2 anchored = new Vector2(
                ResolveLerpUnclamped(after.xMin, after.xMax, normalized.x),
                ResolveLerpUnclamped(after.yMin, after.yMax, normalized.y));
            Vector2 candidate = Pan + canvasPoint - anchored;
            if (IsFinite(candidate)) Pan = candidate;
            Save();
        }

        public Rect ResolveCanvasBounds(Rect viewport, Rect worldBounds)
        {
            float availableWidth = Mathf.Max(1f, viewport.width - viewportPadding * 2f);
            float availableHeight = Mathf.Max(1f, viewport.height - viewportPadding * 2f);
            float worldWidth = Mathf.Max(0.0001f, Mathf.Abs(worldBounds.width));
            float worldHeight = Mathf.Max(0.0001f, Mathf.Abs(worldBounds.height));
            float aspect = worldWidth / worldHeight;
            float width = availableWidth;
            float height = width / aspect;
            if (height > availableHeight)
            {
                height = availableHeight;
                width = height * aspect;
            }
            Vector2 size = new Vector2(width, height) * Zoom;
            return new Rect(viewport.center + Pan - size * 0.5f, size);
        }

        /// <summary>把屏幕命中容差换算成当前缩放下的世界半径，保持缩放前后的点击手感一致。</summary>
        public float ResolveWorldRadiusForPixels(Rect viewport, Rect worldBounds, float pixels)
        {
            if (!IsFinite(viewport.width) || !IsFinite(viewport.height)
                || !IsFinite(worldBounds.width) || !IsFinite(worldBounds.height)
                || viewport.width <= 1f || viewport.height <= 1f
                || worldBounds.width <= 0f || worldBounds.height <= 0f)
                return 0f;
            Rect canvas = ResolveCanvasBounds(viewport, worldBounds);
            float safePixels = IsFinite(pixels) ? Mathf.Max(0f, pixels) : 0f;
            float worldPerPixelX = Mathf.Abs(worldBounds.width) / Mathf.Max(1f, canvas.width);
            float worldPerPixelY = Mathf.Abs(worldBounds.height) / Mathf.Max(1f, canvas.height);
            return Mathf.Max(0f, safePixels * Mathf.Max(worldPerPixelX, worldPerPixelY));
        }

        public Vector2 WorldToCanvas(Vector3 world, Rect worldBounds, Rect viewport)
        {
            Rect canvas = ResolveCanvasBounds(viewport, worldBounds);
            return WorldToCanvas(new Vector2(world.x, world.z), worldBounds, canvas);
        }

        public Vector2 WorldSizeToCanvas(Vector3 size, Rect worldBounds, Rect viewport)
        {
            Rect canvas = ResolveCanvasBounds(viewport, worldBounds);
            return new Vector2(
                size.x / Mathf.Max(0.001f, worldBounds.width) * canvas.width,
                size.z / Mathf.Max(0.001f, worldBounds.height) * canvas.height);
        }

        public bool TryCanvasToWorld(
            Vector2 canvasPoint,
            Rect worldBounds,
            Rect viewport,
            float preservedY,
            out Vector3 world,
            bool requireInside = false)
        {
            world = default;
            if (!IsFinite(canvasPoint) || worldBounds.width <= 0f || worldBounds.height <= 0f) return false;
            Rect canvas = ResolveCanvasBounds(viewport, worldBounds);
            if (requireInside && !canvas.Contains(canvasPoint)) return false;
            float u = Mathf.InverseLerp(canvas.xMin, canvas.xMax, canvasPoint.x);
            float v = 1f - Mathf.InverseLerp(canvas.yMin, canvas.yMax, canvasPoint.y);
            world = new Vector3(
                Mathf.Lerp(worldBounds.xMin, worldBounds.xMax, u),
                preservedY,
                Mathf.Lerp(worldBounds.yMin, worldBounds.yMax, v));
            return IsFinite(world);
        }

        public static Vector2 WorldToCanvas(Vector2 world, Rect worldBounds, Rect canvasBounds)
        {
            return new Vector2(
                Mathf.Lerp(canvasBounds.xMin, canvasBounds.xMax,
                    Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, world.x)),
                Mathf.Lerp(canvasBounds.yMax, canvasBounds.yMin,
                    Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, world.y)));
        }

        private void Save()
        {
            layout.pan = Pan;
            layout.zoom = Zoom;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float FiniteOr(float value, float fallback) => IsFinite(value) ? value : fallback;
        private static float ResolveInverseLerpUnclamped(float minimum, float maximum, float value)
        {
            float span = maximum - minimum;
            if (!IsFinite(minimum) || !IsFinite(maximum) || !IsFinite(value)
                || Mathf.Abs(span) <= 0.000001f)
                return 0f;
            float result = (value - minimum) / span;
            return IsFinite(result) ? result : 0f;
        }
        private static float ResolveLerpUnclamped(float minimum, float maximum, float normalized)
        {
            float result = minimum + (maximum - minimum) * normalized;
            return IsFinite(result) ? result : minimum;
        }
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(Rect value) => IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.width) && IsFinite(value.height);

        private static float ResolvePanCorrection(
            float contentMin,
            float contentMax,
            float viewportMin,
            float viewportMax,
            bool contentFits,
            float viewportCenter,
            float overscroll)
        {
            if (contentFits)
            {
                float desiredCenter = (contentMin + contentMax) * 0.5f;
                float minCenter = viewportCenter - overscroll;
                float maxCenter = viewportCenter + overscroll;
                return Mathf.Clamp(desiredCenter, minCenter, maxCenter) - desiredCenter;
            }
            float correction = 0f;
            if (contentMin > viewportMax - overscroll)
                correction = viewportMax - overscroll - contentMin;
            else if (contentMax < viewportMin + overscroll)
                correction = viewportMin + overscroll - contentMax;
            return correction;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>领域无关的 PreviewScene 轨道相机状态，不持有 Camera 或场景对象。</summary>
    public sealed class ESWorkbenchOrbitCameraState
    {
        private readonly ESWorkbenchViewportLayoutState layout;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly float minimumPitch;
        private readonly float maximumPitch;
        private readonly float minimumDistance;
        private readonly float maximumDistance;
        private readonly float presentationRadiusScale;

        public ESWorkbenchOrbitCameraState(
            Vector3 focus,
            float distance,
            float yaw,
            float pitch,
            float minimumPitch = -80f,
            float maximumPitch = 80f,
            float minimumDistance = 0.3f,
            float maximumDistance = 5000f,
            ESWorkbenchViewportFeelSettings feel = null,
            float presentationRadiusScale = 0f)
        {
            this.layout = null;
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            this.minimumPitch = Mathf.Min(minimumPitch, maximumPitch);
            this.maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            this.minimumDistance = Mathf.Max(0.01f, minimumDistance);
            this.maximumDistance = Mathf.Max(this.minimumDistance, maximumDistance);
            this.presentationRadiusScale = IsFinite(presentationRadiusScale)
                && presentationRadiusScale > 0f ? presentationRadiusScale : 0f;
            SetView(focus, distance, yaw, pitch);
        }

        public ESWorkbenchOrbitCameraState(
            ESWorkbenchViewportLayoutState layout,
            Vector3 defaultFocus,
            float defaultDistance,
            float defaultYaw,
            float defaultPitch,
            float minimumPitch = -80f,
            float maximumPitch = 80f,
            float minimumDistance = 0.3f,
            float maximumDistance = 5000f,
            ESWorkbenchViewportFeelSettings feel = null,
            float presentationRadiusScale = 0f)
        {
            this.layout = layout;
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            this.minimumPitch = Mathf.Min(minimumPitch, maximumPitch);
            this.maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            this.minimumDistance = Mathf.Max(0.01f, minimumDistance);
            this.maximumDistance = Mathf.Max(this.minimumDistance, maximumDistance);
            this.presentationRadiusScale = IsFinite(presentationRadiusScale)
                && presentationRadiusScale > 0f ? presentationRadiusScale : 0f;

            if (layout != null
                && layout.cameraInitialized
                && IsFinite(layout.cameraFocus)
                && IsFinite(layout.cameraDistance)
                && layout.cameraDistance > 0f
                && IsFinite(layout.cameraYaw)
                && IsFinite(layout.cameraPitch))
            {
                SetView(layout.cameraFocus, layout.cameraDistance, layout.cameraYaw, layout.cameraPitch);
            }
            else
            {
                SetView(defaultFocus, defaultDistance, defaultYaw, defaultPitch);
            }
        }

        public Vector3 Focus { get; private set; }
        public float Distance { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        /// <summary>
        /// 状态距离与渲染器 pose.Radius 的比例。默认 0 表示状态距离就是直接相机距离；
        /// 预览渲染器注入正值后，才按 pose 半径、FOV 和纵横比换算真实相机距离。
        /// </summary>
        public float PresentationRadiusScale => presentationRadiusScale;

        /// <summary>
        /// 返回预览渲染器使用的内容半径。轨道状态的 Distance 可能是经过
        /// presentationRadiusScale 标定的作者距离；渲染宿主不得自行复制比例常量。
        /// 未启用表示比例时，状态距离本身就是半径语义。
        /// </summary>
        public float ResolvePresentationRadius() =>
            ResolvePresentationRadius(Distance);

        /// <summary>按指定状态距离解析预览内容半径，供重建/外部相机绑定复用。</summary>
        public float ResolvePresentationRadius(float stateDistance)
        {
            float safeDistance = IsFinite(stateDistance) && stateDistance > 0f
                ? stateDistance
                : minimumDistance;
            if (presentationRadiusScale <= 0f)
                return safeDistance;
            float radius = safeDistance / presentationRadiusScale;
            return IsFinite(radius) && radius > 0f
                ? radius
                : Mathf.Max(0.0001f, safeDistance);
        }

        public bool HasPersistedView => layout != null && layout.cameraInitialized;

        public void SetView(Vector3 focus, float distance, float yaw, float pitch)
        {
            Focus = IsFinite(focus) ? focus : Vector3.zero;
            Distance = Mathf.Clamp(IsFinite(distance) ? distance : minimumDistance,
                minimumDistance, maximumDistance);
            Yaw = IsFinite(yaw) ? yaw : 0f;
            Pitch = Mathf.Clamp(IsFinite(pitch) ? pitch : 0f, minimumPitch, maximumPitch);
            Save();
        }

        public void Orbit(Vector2 pointerDelta)
        {
            if (!IsFinite(pointerDelta)) return;
            Yaw = NormalizeAngle(Yaw + pointerDelta.x * feel.OrbitYawDegreesPerPixel);
            Pitch = Mathf.Clamp(Pitch - pointerDelta.y * feel.OrbitPitchDegreesPerPixel, minimumPitch, maximumPitch);
            Save();
        }

        public void Pan(Vector2 pointerDelta)
        {
            Pan(pointerDelta, default, feel.VerticalFieldOfViewDegrees);
        }

        /// <summary>
        /// 按当前视口投影尺度平移焦点。视口高度变化时，单位屏幕位移仍对应
        /// 相同的屏幕视觉距离；无效视口回退到历史距离比例，保证纯状态调用可用。
        /// </summary>
        public void Pan(
            Vector2 pointerDelta,
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            if (!IsFinite(pointerDelta)) return;
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            float worldPerPixel = ResolvePanWorldPerPixel(viewport, verticalFieldOfViewDegrees);
            Vector3 candidate = Focus + (-(rotation * Vector3.right) * pointerDelta.x
                + rotation * Vector3.up * pointerDelta.y) * worldPerPixel;
            if (IsFinite(candidate))
            {
                Focus = candidate;
                Save();
            }
        }

        /// <summary>返回当前视口下每个指针像素对应的世界距离。</summary>
        public float ResolvePanWorldPerPixel(
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            float fallback = ResolvePresentationCameraDistance(
                    Distance, viewport, verticalFieldOfViewDegrees)
                * feel.PanWorldPerPixelAtDistance;
            if (!IsFinite(viewport) || viewport.height <= 1f)
                return fallback;

            float fieldOfView = Mathf.Clamp(
                IsFinite(verticalFieldOfViewDegrees) && verticalFieldOfViewDegrees > 0f
                    ? verticalFieldOfViewDegrees
                    : feel.VerticalFieldOfViewDegrees,
                1f,
                170f);
            float halfHeight = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            float referenceHalfHeight = Mathf.Tan(
                feel.VerticalFieldOfViewDegrees * Mathf.Deg2Rad * 0.5f);
            float viewportScale = feel.PanReferenceViewportHeight / viewport.height;
            float fieldOfViewScale = halfHeight / Mathf.Max(0.0001f, referenceHalfHeight);
            float result = fallback * viewportScale * fieldOfViewScale;
            return IsFinite(result) && result > 0f ? result : fallback;
        }

        public void Zoom(float wheelDelta)
        {
            if (!IsFinite(wheelDelta)) return;
            float normalizedWheelDelta = feel.NormalizeWheelDelta(wheelDelta);
            float multiplier = Mathf.Exp(
                normalizedWheelDelta * feel.CameraWheelZoomSensitivity * feel.CameraWheelDistanceScale);
            Distance = Mathf.Clamp(Distance * multiplier, minimumDistance, maximumDistance);
            Save();
        }

        /// <summary>
        /// 以指针下方的视线为锚点缩放轨道相机。只改变轨道状态，不依赖 Unity
        /// Camera，因此 3D Scene、Prefab 和空间工作台可以共用同一曲线。
        /// </summary>
        public void ZoomAt(
            Vector2 viewportPoint,
            Rect viewport,
            float wheelDelta,
            float verticalFieldOfViewDegrees = -1f)
        {
            if (!IsFinite(viewportPoint) || !IsFinite(viewport.x) || !IsFinite(viewport.y)
                || !IsFinite(viewport.width) || !IsFinite(viewport.height)
                || !IsFinite(wheelDelta)
                || viewport.width <= 1f || viewport.height <= 1f)
                return;

            float normalizedWheelDelta = feel.NormalizeWheelDelta(wheelDelta);
            float multiplier = Mathf.Exp(
                normalizedWheelDelta * feel.CameraWheelZoomSensitivity * feel.CameraWheelDistanceScale);
            float previousDistance = Distance;
            float nextDistance = Mathf.Clamp(
                previousDistance * multiplier, minimumDistance, maximumDistance);
            if (!IsFinite(nextDistance)) return;

            float fieldOfView = Mathf.Clamp(
                IsFinite(verticalFieldOfViewDegrees) && verticalFieldOfViewDegrees > 0f
                    ? verticalFieldOfViewDegrees
                    : feel.VerticalFieldOfViewDegrees,
                1f, 170f);
            float halfHeight = Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            float aspect = viewport.width / Mathf.Max(1f, viewport.height);
            float normalizedX = ((viewportPoint.x - viewport.xMin) / viewport.width) * 2f - 1f;
            float normalizedY = 1f - ((viewportPoint.y - viewport.yMin) / viewport.height) * 2f;
            Vector3 cameraDirection = new Vector3(
                normalizedX * halfHeight * aspect,
                normalizedY * halfHeight,
                1f).normalized;
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 viewDirection = rotation * Vector3.forward;
            Vector3 rayDirection = rotation * cameraDirection;
            float rayCosine = Vector3.Dot(rayDirection, viewDirection);
            if (!IsFinite(rayCosine) || rayCosine <= 0.0001f)
            {
                Distance = nextDistance;
                Save();
                return;
            }

            // 让指针射线与焦点平面的交点在缩放前后保持不变；焦点只在
            // 相机屏幕平面内移动，因此轨道旋转角不会被缩放偷偷改写。
            float distanceDelta = ResolvePresentationCameraDistance(
                    nextDistance, viewport, fieldOfView)
                - ResolvePresentationCameraDistance(
                    previousDistance, viewport, fieldOfView);
            Vector3 focusShift = (viewDirection - rayDirection / rayCosine) * distanceDelta;
            Vector3 nextFocus = Focus + focusShift;
            Focus = IsFinite(nextFocus) ? nextFocus : Focus;
            Distance = nextDistance;
            Save();
        }

        /// <summary>返回当前渲染器实际使用的相机距离，而不是作者状态中的 pose 半径。</summary>
        public float ResolvePresentationCameraDistance(
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            return ResolvePresentationCameraDistanceCore(
                Distance, viewport, verticalFieldOfViewDegrees);
        }

        /// <summary>
        /// 按指定状态距离解析渲染器实际相机距离。预览宿主在需要把外部
        /// Camera 姿态接入轨道状态时使用此重载，避免各领域重复复制 FOV/纵横比换算。
        /// </summary>
        public float ResolvePresentationCameraDistance(
            float stateDistance,
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            return ResolvePresentationCameraDistanceCore(
                stateDistance, viewport, verticalFieldOfViewDegrees);
        }

        /// <summary>
        /// 将渲染器实际相机距离反解为轨道状态距离。
        /// 外部 Camera 首次接入时必须使用该入口，保证不同纵横比和表示半径
        /// 标定下捕获后再次投影仍保持原构图。
        /// </summary>
        public float ResolveStateDistanceForPresentationCameraDistance(
            float actualCameraDistance,
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            float safeActualDistance = IsFinite(actualCameraDistance) && actualCameraDistance > 0f
                ? actualCameraDistance
                : ResolvePresentationCameraDistance(viewport, verticalFieldOfViewDegrees);
            if (presentationRadiusScale <= 0f)
                return Mathf.Clamp(safeActualDistance, minimumDistance, maximumDistance);

            if (!IsFinite(viewport) || viewport.width <= 1f || viewport.height <= 1f)
            {
                float fallbackStateDistance = safeActualDistance * presentationRadiusScale;
                return Mathf.Clamp(
                    IsFinite(fallbackStateDistance) && fallbackStateDistance > 0f
                        ? fallbackStateDistance
                        : minimumDistance,
                    minimumDistance,
                    maximumDistance);
            }

            float fieldOfView = Mathf.Clamp(
                IsFinite(verticalFieldOfViewDegrees) && verticalFieldOfViewDegrees > 0f
                    ? verticalFieldOfViewDegrees
                    : feel.VerticalFieldOfViewDegrees,
                1f,
                170f);
            float verticalHalfFov = fieldOfView * Mathf.Deg2Rad * 0.5f;
            float aspect = viewport.width / Mathf.Max(1f, viewport.height);
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * aspect);
            float limitingHalfFov = Mathf.Max(
                Mathf.Deg2Rad,
                Mathf.Min(verticalHalfFov, horizontalHalfFov));
            float stateDistance = safeActualDistance
                * Mathf.Max(0.02f, Mathf.Sin(limitingHalfFov))
                * presentationRadiusScale;
            return Mathf.Clamp(
                IsFinite(stateDistance) && stateDistance > 0f
                    ? stateDistance
                    : minimumDistance,
                minimumDistance,
                maximumDistance);
        }

        private float ResolvePresentationCameraDistanceCore(
            float stateDistance,
            Rect viewport,
            float verticalFieldOfViewDegrees)
        {
            float presentationRadius = ResolvePresentationRadius(stateDistance);
            if (presentationRadiusScale <= 0f)
                return presentationRadius;

            float radius = presentationRadius;
            if (!IsFinite(viewport) || viewport.width <= 1f || viewport.height <= 1f)
                return radius;

            float fieldOfView = Mathf.Clamp(
                IsFinite(verticalFieldOfViewDegrees) && verticalFieldOfViewDegrees > 0f
                    ? verticalFieldOfViewDegrees
                    : feel.VerticalFieldOfViewDegrees,
                1f, 170f);
            float verticalHalfFov = fieldOfView * Mathf.Deg2Rad * 0.5f;
            float aspect = viewport.width / Mathf.Max(1f, viewport.height);
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * aspect);
            float limitingHalfFov = Mathf.Max(
                Mathf.Deg2Rad, Mathf.Min(verticalHalfFov, horizontalHalfFov));
            float distance = radius / Mathf.Max(0.02f, Mathf.Sin(limitingHalfFov));
            return IsFinite(distance) && distance > 0f ? distance : radius;
        }

        /// <summary>按内容边界设置稳定推荐视角；只改变可持久化的轨道状态。</summary>
        public void FrameBounds(Bounds bounds, float padding = 1.15f, float minimumFrameDistance = 2f)
        {
            if (!IsFinite(bounds.center) || !IsFinite(bounds.size))
                return;

            float radius = Mathf.Max(minimumFrameDistance, bounds.extents.magnitude * Mathf.Max(1f, padding));
            SetView(bounds.center, radius, Yaw, Pitch);
        }

        /// <summary>按领域内容边界恢复稳定的首次观察姿态，不继承用户上次的任意旋转角。</summary>
        public void FrameRecommended(
            Bounds bounds,
            float padding = 1.15f,
            float minimumFrameDistance = 2f,
            float recommendedYaw = 35f,
            float recommendedPitch = 25f)
        {
            if (!IsFinite(bounds.center) || !IsFinite(bounds.size))
                return;

            float radius = Mathf.Max(minimumFrameDistance, bounds.extents.magnitude * Mathf.Max(1f, padding));
            SetView(bounds.center, radius, recommendedYaw, recommendedPitch);
        }

        /// <summary>设置统一的作者推荐观察姿态，并清除用户上一次的异常距离。</summary>
        public void ResetRecommended(Vector3 focus, float distance, float yaw = 35f, float pitch = 25f)
        {
            SetView(focus, distance, yaw, pitch);
        }

        private void Save()
        {
            if (layout == null)
                return;

            layout.cameraFocus = Focus;
            layout.cameraDistance = Distance;
            layout.cameraYaw = Yaw;
            layout.cameraPitch = Pitch;
            layout.cameraInitialized = true;
        }

        private static float NormalizeAngle(float value)
        {
            value = Mathf.Repeat(value + 180f, 360f) - 180f;
            return Mathf.Approximately(value, -180f) ? 180f : value;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.width) && IsFinite(value.height);
    }

    public enum ESWorkbenchOrbitInputResult : byte
    {
        None,
        Captured,
        Orbit,
        Pan,
        Zoom,
        Released
    }

    /// <summary>领域无关的 IMGUI 轨道输入捕获；统一滚轮、右键旋转、中键平移和 hotControl 释放。</summary>
    public sealed class ESWorkbenchIMGUIOrbitInput
    {
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly ESWorkbenchPointerGestureSession gestureSession;
        private readonly ESWorkbenchPointerInteractionCoordinator pointerCoordinator;
        private readonly object pointerOwnerToken = new object();
        private readonly float verticalFieldOfViewDegrees;
        private bool orbiting;
        private bool panning;
        private int activeControlId;
        private ESWorkbenchPointerOwnerKind activeOwnerKind;

        public ESWorkbenchIMGUIOrbitInput(
            ESWorkbenchViewportFeelSettings feel = null,
            float verticalFieldOfViewDegrees = -1f,
            ESWorkbenchPointerInteractionCoordinator pointerCoordinator = null)
        {
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            this.pointerCoordinator = pointerCoordinator;
            gestureSession = new ESWorkbenchPointerGestureSession(
                this.feel.DragStartPixels, this.feel);
            this.verticalFieldOfViewDegrees = IsFinite(verticalFieldOfViewDegrees)
                && verticalFieldOfViewDegrees > 0f
                ? Mathf.Clamp(verticalFieldOfViewDegrees, 1f, 170f)
                : this.feel.VerticalFieldOfViewDegrees;
        }

        public bool IsCapturing => gestureSession.IsActive;

        public ESWorkbenchOrbitInputResult Handle(
            Rect viewport,
            ESWorkbenchOrbitCameraState camera,
            int controlId)
        {
            return Handle(viewport, viewport, camera, controlId);
        }

        /// <summary>
        /// 交互区域与相机投影区域可以不同：工具栏、状态覆盖层等只应阻止输入，
        /// 不应改变滚轮锚点、纵横比或平移的世界单位换算。
        /// </summary>
        public ESWorkbenchOrbitInputResult Handle(
            Rect interactionRect,
            Rect projectionRect,
            ESWorkbenchOrbitCameraState camera,
            int controlId)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            Event evt = Event.current;
            if (evt == null)
                return ESWorkbenchOrbitInputResult.None;
            if (IsCapturing
                && pointerCoordinator != null
                && activeOwnerKind != ESWorkbenchPointerOwnerKind.None
                && !pointerCoordinator.Owns(
                    pointerOwnerToken, 0, activeOwnerKind))
            {
                // 轨道/平移 owner 被窗口或其它工作台控件夺走后，不能继续消费
                // 后续 IMGUI MouseDrag；统一按 capture lost 释放 hotControl。
                Release(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
                return ESWorkbenchOrbitInputResult.Released;
            }
            bool inside = interactionRect.Contains(evt.mousePosition);
            if (!inside && !IsCapturing)
                return ESWorkbenchOrbitInputResult.None;

            if (evt.type == EventType.ScrollWheel && inside)
            {
                // 正值滚轮统一表示向后滚动，镜头距离增大；同时保持指针下方
                // 的内容稳定，避免滚轮时整个场景向焦点漂移。
                camera.ZoomAt(
                    evt.mousePosition,
                    projectionRect,
                    evt.delta.y,
                    verticalFieldOfViewDegrees);
                evt.Use();
                return ESWorkbenchOrbitInputResult.Zoom;
            }

            if (evt.type == EventType.MouseDown && inside)
            {
                bool wantsOrbit = evt.button == 1 || (evt.button == 0 && evt.alt);
                bool wantsPan = evt.button == 2;
                if (!wantsOrbit && !wantsPan)
                    return ESWorkbenchOrbitInputResult.None;
                ESWorkbenchPointerOwnerKind ownerKind = wantsOrbit
                    ? ESWorkbenchPointerOwnerKind.Orbit
                    : ESWorkbenchPointerOwnerKind.Viewport;
                if (pointerCoordinator != null
                    && !pointerCoordinator.TryAcquire(pointerOwnerToken, 0, ownerKind))
                    return ESWorkbenchOrbitInputResult.None;
                activeOwnerKind = ownerKind;
                if (!gestureSession.TryArm(
                        wantsOrbit
                            ? ESWorkbenchPointerGestureSession.Kind.Orbit
                        : ESWorkbenchPointerGestureSession.Kind.Pan,
                        0,
                        evt.mousePosition))
                {
                    pointerCoordinator?.Release(pointerOwnerToken, 0, ownerKind);
                    activeOwnerKind = ESWorkbenchPointerOwnerKind.None;
                    return ESWorkbenchOrbitInputResult.None;
                }

                orbiting = wantsOrbit;
                panning = wantsPan;
                activeControlId = controlId;
                if (controlId != 0)
                    GUIUtility.hotControl = controlId;
                evt.Use();
                return ESWorkbenchOrbitInputResult.Captured;
            }

            if (evt.type == EventType.MouseDrag && IsCapturing)
            {
                if (!gestureSession.TryStartAndConsumePointerDelta(
                        0,
                        evt.mousePosition,
                        out Vector2 delta,
                        out _))
                {
                    if (!gestureSession.IsStarted)
                    {
                        evt.Use();
                        return ESWorkbenchOrbitInputResult.Captured;
                    }
                    Release();
                    return ESWorkbenchOrbitInputResult.Released;
                }
                ESWorkbenchOrbitInputResult result;
                if (orbiting)
                {
                    camera.Orbit(delta);
                    result = ESWorkbenchOrbitInputResult.Orbit;
                }
                else
                {
                    camera.Pan(delta, projectionRect, verticalFieldOfViewDegrees);
                    result = ESWorkbenchOrbitInputResult.Pan;
                }
                evt.Use();
                return result;
            }

            // 离开窗口不是终止条件：GUIUtility.hotControl 仍然代表本次轨道手势，
            // 重新进入或收到 MouseUp 时继续消费位移；真正失焦由 Ignore/hotControl
            // 丢失路径结束。这样 IMGUI 与 UI Toolkit 的 PointerCapture 语义一致，
            // 拖到视口边缘不会突然断手。
            if (evt.type == EventType.MouseLeaveWindow && IsCapturing)
            {
                evt.Use();
                return ESWorkbenchOrbitInputResult.Captured;
            }

            if ((evt.type == EventType.MouseUp || evt.type == EventType.Ignore) && IsCapturing)
            {
                if (evt.type == EventType.MouseUp
                    && gestureSession.TryStartAndConsumePointerDeltaFinal(
                        0,
                        evt.mousePosition,
                        out Vector2 finalDelta,
                        out _))
                {
                    if (orbiting) camera.Orbit(finalDelta);
                    else camera.Pan(finalDelta, projectionRect, verticalFieldOfViewDegrees);
                }
                Release(evt.type == EventType.MouseUp
                    ? ESWorkbenchPointerGestureSession.EndReason.Commit
                    : ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
                if (evt.type == EventType.MouseUp)
                    evt.Use();
                return ESWorkbenchOrbitInputResult.Released;
            }

            if (IsCapturing && activeControlId != 0 && GUIUtility.hotControl != activeControlId)
            {
                Release(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
                return ESWorkbenchOrbitInputResult.Released;
            }

            return ESWorkbenchOrbitInputResult.None;
        }

        public void Release(
            ESWorkbenchPointerGestureSession.EndReason reason =
                ESWorkbenchPointerGestureSession.EndReason.Deactivate)
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            activeControlId = 0;
            orbiting = false;
            panning = false;
            gestureSession.Cancel(reason);
            if (pointerCoordinator != null && activeOwnerKind != ESWorkbenchPointerOwnerKind.None)
                pointerCoordinator.Release(pointerOwnerToken, 0, activeOwnerKind);
            activeOwnerKind = ESWorkbenchPointerOwnerKind.None;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 统一 3D 作者视口覆盖层。它只绘制导航状态，不拥有领域选择、Draft 或正式资产。
    /// </summary>
    public static class ESWorkbenchViewportOverlay
    {
        public const float HeaderHeight = 32f;

        public static Rect GetInteractionRect(Rect viewport, float headerHeight = HeaderHeight)
        {
            return new Rect(
                viewport.x,
                viewport.y + Mathf.Max(0f, headerHeight),
                viewport.width,
                Mathf.Max(1f, viewport.height - Mathf.Max(0f, headerHeight)));
        }

        /// <summary>
        /// 边缘平移允许指针离开渲染矩形继续拖动，但渲染矩形内部的工具栏、
        /// 状态条等覆盖层必须被排除。所有 3D 宿主共享这条边界规则。
        /// </summary>
        public static bool AllowsEdgePanPointer(
            Rect renderRect,
            Rect interactionRect,
            Vector2 pointer)
        {
            if (!IsFinite(renderRect) || !IsFinite(interactionRect)
                || !IsFinite(pointer)) return false;
            return !renderRect.Contains(pointer) || interactionRect.Contains(pointer);
        }

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.width) && IsFinite(value.height);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool DrawNavigationToolbar(
            Rect viewport,
            ESWorkbenchOrbitCameraState camera,
            string title,
            string status,
            Action frameAll,
            bool readOnly = false)
        {
            if (camera == null || viewport.width < 1f || viewport.height < 1f)
                return false;

            string cameraStatus = string.Format(
                "距离 {0:0.##} · 角度 {1:0.#}/{2:0.#}{3}",
                camera.Distance,
                camera.Yaw,
                camera.Pitch,
                readOnly ? " · 只读" : string.Empty);
            string combinedStatus = string.IsNullOrWhiteSpace(status)
                ? cameraStatus
                : status + " · " + cameraStatus;
            ESWorkbenchViewportRenderStyle.DrawGuiChrome(
                viewport,
                string.IsNullOrWhiteSpace(title) ? "三维作者视图" : title,
                readOnly ? "运行时构图投影" : "世界空间作者视口",
                combinedStatus,
                ESWorkbenchViewportRenderStyle.ResolveAccent(readOnly),
                readOnly);

            bool clicked = false;
            if (viewport.width >= 150f)
            {
                bool showFrameButton = frameAll != null && viewport.width >= 300f;
                float right = viewport.xMax - 10f;
                Rect zoomInButton = new Rect(right - 26f, viewport.y + 5f, 26f, 22f);
                Rect zoomOutButton = new Rect(right - 56f, viewport.y + 5f, 26f, 22f);
                if (GUI.Button(zoomOutButton, new GUIContent("-", "缩小视口"), EditorStyles.miniButtonLeft))
                {
                    camera.Zoom(1f);
                    GUI.changed = true;
                }
                if (GUI.Button(zoomInButton, new GUIContent("+", "放大视口"), EditorStyles.miniButtonRight))
                {
                    camera.Zoom(-1f);
                    GUI.changed = true;
                }
                if (showFrameButton && GUI.Button(
                    new Rect(viewport.xMax - 176f, viewport.y + 5f, 110f, 22f),
                    new GUIContent("推荐视角", "按当前内容重新取景"), EditorStyles.miniButton))
                {
                    frameAll();
                    clicked = true;
                }
            }
            DrawAxis(viewport, camera);
            return clicked;
        }

        public static void DrawAxis(Rect viewport, ESWorkbenchOrbitCameraState camera)
        {
            if (camera == null || viewport.width < 120f || viewport.height < 100f
                || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            const float size = 30f;
            Rect panel = new Rect(viewport.xMax - 94f, viewport.yMin + HeaderHeight + 8f, 86f, 86f);
            EditorGUI.DrawRect(panel, new Color(0.02f, 0.028f, 0.038f, 0.86f));
            GUI.Label(new Rect(panel.x + 7f, panel.y + 4f, panel.width - 14f, 16f),
                new GUIContent("世界轴", "X 红、Y 绿、Z 蓝"), EditorStyles.whiteMiniLabel);
            Vector2 origin = new Vector2(panel.center.x, panel.center.y + 10f);
            Quaternion worldToView = Quaternion.Inverse(Quaternion.Euler(camera.Pitch, camera.Yaw, 0f));
            Color oldHandle = Handles.color;
            Color oldGui = GUI.color;
            Handles.BeginGUI();
            DrawAxisLine(origin, worldToView * Vector3.right, new Color(0.96f, 0.3f, 0.28f, 1f), "X", size);
            DrawAxisLine(origin, worldToView * Vector3.up, new Color(0.36f, 0.92f, 0.44f, 1f), "Y", size);
            DrawAxisLine(origin, worldToView * Vector3.forward, new Color(0.3f, 0.6f, 1f, 1f), "Z", size);
            Handles.EndGUI();
            Handles.color = oldHandle;
            GUI.color = oldGui;
            EditorGUI.DrawRect(new Rect(origin.x - 2f, origin.y - 2f, 4f, 4f),
                new Color(0.92f, 0.94f, 0.96f, 1f));
        }

        private static void DrawAxisLine(Vector2 start, Vector3 direction, Color color, string label, float size)
        {
            Vector2 projected = new Vector2(direction.x, -direction.y);
            if (projected.sqrMagnitude < 0.0001f)
                projected = Vector2.up * 0.1f;
            Vector2 end = start + projected.normalized * Mathf.Lerp(10f, size, Mathf.Clamp01(projected.magnitude));
            Handles.color = color;
            Handles.DrawAAPolyLine(3f, new Vector3(start.x, start.y), new Vector3(end.x, end.y));
            GUI.color = color;
            GUI.Label(new Rect(end.x - 5f, end.y - 9f, 16f, 18f), label, EditorStyles.whiteMiniLabel);
        }
    }

    /// <summary>
    /// 工作台视口的公共渲染语义。它只负责舞台、网格和 chrome，不拥有领域数据、
    /// 选择或相机状态；2D、3D 和其他专业工作台必须通过这一层保持视觉一致。
    /// </summary>
    public static class ESWorkbenchViewportRenderStyle
    {
        public static readonly Color StageBackground = new Color(0.035f, 0.047f, 0.058f, 1f);
        public static readonly Color StageSurface = new Color(0.055f, 0.071f, 0.084f, 1f);
        public static readonly Color GridMinor = new Color(0.34f, 0.43f, 0.48f, 0.16f);
        public static readonly Color GridMajor = new Color(0.40f, 0.55f, 0.62f, 0.30f);
        public static readonly Color AuthoringAccent = new Color(0.28f, 0.72f, 0.92f, 1f);
        public static readonly Color ReadOnlyAccent = new Color(0.67f, 0.50f, 0.94f, 1f);
        public static readonly Color SelectionAccent = new Color(0.98f, 0.78f, 0.24f, 1f);
        public static readonly Color WarningAccent = new Color(1f, 0.56f, 0.24f, 1f);
        public static readonly Color ErrorAccent = new Color(1f, 0.30f, 0.34f, 1f);
        public static readonly Color StatusSurface = new Color(0.018f, 0.026f, 0.033f, 0.92f);

        public enum InteractionState : byte
        {
            Normal,
            Hover,
            Selected,
            PreviewAllowed,
            PreviewRejected,
            Brush
        }

        public static Color ResolveAccent(bool readOnly)
        {
            return readOnly ? ReadOnlyAccent : AuthoringAccent;
        }

        public static Color ResolveInteractionColor(InteractionState state)
        {
            switch (state)
            {
                case InteractionState.Selected: return SelectionAccent;
                case InteractionState.PreviewRejected: return ErrorAccent;
                case InteractionState.PreviewAllowed: return AuthoringAccent;
                case InteractionState.Brush: return WarningAccent;
                case InteractionState.Hover: return Color.Lerp(AuthoringAccent, Color.white, 0.18f);
                default: return Color.Lerp(AuthoringAccent, Color.white, 0.04f);
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static void DrawGuiBackdrop(Rect viewport)
        {
            if (!IsRepaint(viewport)) return;
            EditorGUI.DrawRect(viewport, StageBackground);
            EditorGUI.DrawRect(
                new Rect(viewport.x, viewport.y, viewport.width, Mathf.Min(1f, viewport.height)),
                new Color(1f, 1f, 1f, 0.06f));
        }

        public static void DrawGuiChrome(
            Rect viewport,
            string title,
            string subtitle,
            string status,
            Color accent,
            bool readOnly)
        {
            if (!IsRepaint(viewport) || viewport.width < 1f || viewport.height < 1f)
                return;

            float headerHeight = Mathf.Min(ESWorkbenchViewportOverlay.HeaderHeight, viewport.height);
            Rect header = new Rect(viewport.x, viewport.y, viewport.width, headerHeight);
            EditorGUI.DrawRect(header, new Color(0.025f, 0.036f, 0.045f, 0.94f));
            EditorGUI.DrawRect(
                new Rect(header.x, header.yMax - 1f, header.width, 1f),
                Color.Lerp(accent, Color.white, 0.14f));
            EditorGUI.DrawRect(
                new Rect(header.x, header.y, Mathf.Min(3f, header.width), header.height),
                accent);

            // 缩放按钮在窄视口仍可用，标题必须主动让出它们的固定占位；
            // 否则 150~180px 的面板会出现标题压住按钮的重叠。
            float titleRight = viewport.width >= 150f
                ? viewport.xMax - 72f
                : viewport.xMax - 10f;
            float titleWidth = Mathf.Max(32f, titleRight - (header.x + 10f));
            GUI.Label(
                new Rect(header.x + 10f, header.y + 5f, titleWidth, 16f),
                string.IsNullOrWhiteSpace(title) ? "视口" : title,
                EditorStyles.boldLabel);
            if (viewport.width >= 310f && !string.IsNullOrWhiteSpace(subtitle))
            {
                GUI.color = new Color(0.73f, 0.79f, 0.82f, 1f);
                GUI.Label(
                    new Rect(header.x + 10f, header.y + 18f, titleWidth, 12f),
                    subtitle,
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            string mode = readOnly ? "只读预览" : "编辑中";
            if (viewport.width >= 360f)
            {
                float badgeWidth = readOnly ? 62f : 48f;
                Rect badge = new Rect(header.xMax - badgeWidth - 182f, header.y + 6f, badgeWidth, 19f);
                EditorGUI.DrawRect(badge, new Color(accent.r, accent.g, accent.b, 0.16f));
                GUI.color = accent;
                GUI.Label(badge, mode, EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
            }

            Rect footer = new Rect(
                viewport.x + 8f,
                Mathf.Max(viewport.y, viewport.yMax - 24f),
                Mathf.Max(1f, viewport.width - 16f),
                Mathf.Min(18f, viewport.height));
            DrawGuiStatusStrip(
                footer,
                string.IsNullOrWhiteSpace(status) ? mode : status,
                accent);

            DrawFrame(viewport, accent);
        }

        /// <summary>
        /// 在有限宽度内把状态拆成可扫描的语义片段。超出宽度的内容被收纳，
        /// 不再让长坐标或相机描述挤压标题和交互区域。
        /// </summary>
        public static void DrawGuiStatusStrip(
            Rect footer,
            string statusText,
            Color accent)
        {
            if (!IsRepaint(footer) || footer.width <= 1f || footer.height <= 1f)
                return;
            EditorGUI.DrawRect(footer, StatusSurface);
            float cursor = footer.x + 6f;
            float right = footer.xMax - 6f;
            int hidden = 0;
            int segmentStart = 0;
            int segmentIndex = 0;
            while (!string.IsNullOrWhiteSpace(statusText) && segmentStart < statusText.Length)
            {
                int separator = statusText.IndexOf(" · ", segmentStart, StringComparison.Ordinal);
                int segmentEnd = separator >= 0 ? separator : statusText.Length;
                string value = statusText.Substring(segmentStart, segmentEnd - segmentStart).Trim();
                segmentStart = separator >= 0 ? separator + 3 : statusText.Length;
                if (string.IsNullOrWhiteSpace(value)) continue;
                float measured = EditorStyles.whiteMiniLabel.CalcSize(new GUIContent(value)).x;
                float width = Mathf.Clamp(measured + 14f, 42f, 190f);
                if (cursor + width > right)
                {
                    int remaining = CountStatusSegments(statusText, segmentStart);
                    float available = right - cursor;
                    float reservedMore = remaining > 0 ? 34f : 0f;
                    float clippedWidth = available - reservedMore;
                    if (clippedWidth >= 42f)
                    {
                        // 长坐标/相机状态优先保留当前片段，剩余片段再收纳；
                        // 这样极窄视口不会只剩一个没有语义的“+1”。
                        Rect clippedChip = new Rect(cursor, footer.y + 2f, clippedWidth,
                            Mathf.Max(12f, footer.height - 4f));
                        EditorGUI.DrawRect(clippedChip,
                            new Color(accent.r, accent.g, accent.b, segmentIndex == 0 ? 0.15f : 0.08f));
                        GUI.color = segmentIndex == 0 ? accent : new Color(0.78f, 0.84f, 0.87f, 1f);
                        GUI.Label(new Rect(
                                clippedChip.x + 7f,
                                clippedChip.y,
                                clippedChip.width - 14f,
                                clippedChip.height),
                            new GUIContent(value, value), EditorStyles.whiteMiniLabel);
                        GUI.color = Color.white;
                        cursor += clippedWidth + 4f;
                        hidden = remaining;
                    }
                    else
                    {
                        hidden = remaining + 1;
                    }
                    break;
                }
                Rect chip = new Rect(cursor, footer.y + 2f, width, Mathf.Max(12f, footer.height - 4f));
                EditorGUI.DrawRect(chip, new Color(accent.r, accent.g, accent.b, segmentIndex == 0 ? 0.15f : 0.08f));
                GUI.color = segmentIndex == 0 ? accent : new Color(0.78f, 0.84f, 0.87f, 1f);
                GUI.Label(new Rect(chip.x + 7f, chip.y, chip.width - 14f, chip.height), value,
                    EditorStyles.whiteMiniLabel);
                cursor += width + 4f;
                GUI.color = Color.white;
                segmentIndex++;
            }
            if (hidden > 0)
            {
                float available = right - cursor;
                float moreWidth = Mathf.Min(30f, Mathf.Max(0f, available));
                if (moreWidth >= 14f)
                {
                    Rect more = new Rect(cursor, footer.y + 2f, moreWidth,
                        Mathf.Max(12f, footer.height - 4f));
                    EditorGUI.DrawRect(more, new Color(1f, 1f, 1f, 0.06f));
                    GUI.color = new Color(0.68f, 0.74f, 0.78f, 1f);
                    GUI.Label(more,
                        new GUIContent(moreWidth >= 24f ? "+" + hidden : "...", statusText),
                        EditorStyles.centeredGreyMiniLabel);
                    GUI.color = Color.white;
                }
                else if (footer.width >= 8f)
                {
                    // 极窄布局仍保留一条可悬停的状态提示，不让信息完全消失。
                    Rect marker = new Rect(footer.xMax - 5f, footer.y + 3f, 3f,
                        Mathf.Max(10f, footer.height - 6f));
                    EditorGUI.DrawRect(marker, WithAlpha(accent, 0.86f));
                    GUI.Label(marker, new GUIContent("", statusText));
                }
            }
        }

        private static int CountStatusSegments(string statusText, int start)
        {
            int count = 0;
            int cursor = Mathf.Clamp(start, 0, statusText?.Length ?? 0);
            while (!string.IsNullOrWhiteSpace(statusText) && cursor < statusText.Length)
            {
                int separator = statusText.IndexOf(" · ", cursor, StringComparison.Ordinal);
                count++;
                if (separator < 0) break;
                cursor = separator + 3;
            }
            return count;
        }

        public static void DrawCanvasBackdrop(Painter2D painter, Rect viewport)
        {
            if (painter == null || viewport.width <= 1f || viewport.height <= 1f)
                return;
            painter.fillColor = StageBackground;
            painter.BeginPath();
            painter.MoveTo(viewport.min);
            painter.LineTo(new Vector2(viewport.xMax, viewport.yMin));
            painter.LineTo(viewport.max);
            painter.LineTo(new Vector2(viewport.xMin, viewport.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        public static void DrawCanvasGrid(Painter2D painter, Rect rect, int columns, int rows)
        {
            if (painter == null || rect.width <= 1f || rect.height <= 1f)
                return;
            columns = Mathf.Clamp(columns, 1, 256);
            rows = Mathf.Clamp(rows, 1, 256);

            painter.strokeColor = GridMinor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            for (int x = 0; x <= columns; x++)
            {
                float px = Mathf.Lerp(rect.xMin, rect.xMax, x / (float)columns);
                painter.MoveTo(new Vector2(px, rect.yMin));
                painter.LineTo(new Vector2(px, rect.yMax));
            }
            for (int y = 0; y <= rows; y++)
            {
                float py = Mathf.Lerp(rect.yMin, rect.yMax, y / (float)rows);
                painter.MoveTo(new Vector2(rect.xMin, py));
                painter.LineTo(new Vector2(rect.xMax, py));
            }
            painter.Stroke();

            painter.strokeColor = GridMajor;
            painter.lineWidth = 1.4f;
            painter.BeginPath();
            int majorStepX = Mathf.Max(1, columns / 4);
            int majorStepY = Mathf.Max(1, rows / 4);
            for (int x = 0; x <= columns; x += majorStepX)
            {
                float px = Mathf.Lerp(rect.xMin, rect.xMax, x / (float)columns);
                painter.MoveTo(new Vector2(px, rect.yMin));
                painter.LineTo(new Vector2(px, rect.yMax));
            }
            for (int y = 0; y <= rows; y += majorStepY)
            {
                float py = Mathf.Lerp(rect.yMin, rect.yMax, y / (float)rows);
                painter.MoveTo(new Vector2(rect.xMin, py));
                painter.LineTo(new Vector2(rect.xMax, py));
            }
            painter.Stroke();
        }

        private static void DrawFrame(Rect viewport, Color accent)
        {
            EditorGUI.DrawRect(new Rect(viewport.x, viewport.y, viewport.width, 1f), new Color(1f, 1f, 1f, 0.09f));
            EditorGUI.DrawRect(new Rect(viewport.x, viewport.yMax - 1f, viewport.width, 1f), new Color(0f, 0f, 0f, 0.46f));
            EditorGUI.DrawRect(new Rect(viewport.x, viewport.y, 1f, viewport.height), Color.Lerp(accent, Color.black, 0.35f));
            EditorGUI.DrawRect(new Rect(viewport.xMax - 1f, viewport.y, 1f, viewport.height), new Color(0f, 0f, 0f, 0.42f));
        }

        private static bool IsRepaint(Rect viewport)
        {
            return Event.current != null
                && Event.current.type == EventType.Repaint
                && viewport.width > 0f
                && viewport.height > 0f;
        }
    }

    /// <summary>统一把 IMGUI 视口局部坐标转换为 Camera.ViewportPointToRay 使用的归一化坐标。</summary>
    public static class ESWorkbenchCameraViewportProjection
    {
        public static bool TryNormalize(
            Rect renderRect,
            Rect interactionRect,
            Vector2 localPoint,
            out Vector3 viewportPoint,
            bool allowOutside = false)
        {
            viewportPoint = default;
            if (!IsFinite(renderRect) || !IsFinite(interactionRect) || !IsFinite(localPoint)
                || renderRect.width <= 1f || renderRect.height <= 1f
                || (!allowOutside && (!renderRect.Contains(localPoint)
                    || !interactionRect.Contains(localPoint))))
                return false;

            float x = (localPoint.x - renderRect.xMin) / renderRect.width;
            float y = 1f - (localPoint.y - renderRect.yMin) / renderRect.height;
            if (!IsFinite(x) || !IsFinite(y)
                || (!allowOutside && (x < 0f || x > 1f || y < 0f || y > 1f)))
                return false;
            viewportPoint = new Vector3(x, y, 0f);
            return true;
        }

        /// <summary>
        /// 将相机世界坐标投影回 IMGUI/UI Toolkit 视口坐标。
        /// 与 <see cref="TryNormalize"/> 成对使用，统一处理 Unity 的左下原点、
        /// 编辑器覆盖层和相机后方点，供 World、Prefab、Scene 等宿主复用。
        /// </summary>
        public static bool TryProjectWorldToGui(
            Camera camera,
            Vector3 worldPoint,
            Rect renderRect,
            Rect interactionRect,
            out Vector2 guiPoint,
            out float depth,
            bool allowOutside = false)
        {
            guiPoint = default;
            depth = float.MaxValue;
            if (camera == null || !IsFinite(worldPoint)
                || !IsFinite(renderRect) || !IsFinite(interactionRect)
                || renderRect.width <= 1f || renderRect.height <= 1f)
                return false;

            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            if (!IsFinite(viewport) || viewport.z <= 0f) return false;
            guiPoint = new Vector2(
                renderRect.xMin + viewport.x * renderRect.width,
                renderRect.yMin + (1f - viewport.y) * renderRect.height);
            depth = viewport.z;
            return allowOutside || interactionRect.Contains(guiPoint);
        }

        /// <summary>
        /// 把屏幕命中/标记半径换算成当前相机深度下的世界半径。
        /// 2D/3D 领域只提供相机和渲染矩形，不能各自按 chunk 或对象尺寸猜测
        /// 点击手感；透视与正交相机都使用同一屏幕像素合同。
        /// </summary>
        public static bool TryResolveWorldRadiusForPixels(
            Camera camera,
            Vector3 worldPoint,
            Rect renderRect,
            float pixels,
            out float worldRadius)
        {
            worldRadius = 0f;
            if (camera == null || !IsFinite(worldPoint) || !IsFinite(renderRect)
                || renderRect.height <= 1f || !IsFinite(pixels) || pixels <= 0f)
                return false;

            Vector3 cameraPoint = camera.transform.InverseTransformPoint(worldPoint);
            if (!IsFinite(cameraPoint) || cameraPoint.z <= 0.0001f) return false;
            float worldPerPixel;
            if (camera.orthographic)
            {
                float size = camera.orthographicSize;
                if (!IsFinite(size) || size <= 0f) return false;
                worldPerPixel = size * 2f / renderRect.height;
            }
            else
            {
                float fieldOfView = camera.fieldOfView;
                if (!IsFinite(fieldOfView) || fieldOfView <= 0f || fieldOfView >= 180f)
                    return false;
                worldPerPixel = 2f * cameraPoint.z
                    * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f)
                    / renderRect.height;
            }
            worldRadius = worldPerPixel * pixels;
            return IsFinite(worldRadius) && worldRadius > 0f;
        }

        private static bool IsFinite(Rect value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.width) && IsFinite(value.height);
        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum ESWorkbenchPrecisionTransformMode : byte
    {
        Absolute,
        Delta
    }

    /// <summary>
    /// 领域无关的旋转/缩放拖动解析。预览与提交必须复用同一结果，且非法输入不得污染对象。
    /// </summary>
    public static class ESWorkbenchTransformGestureResolver
    {
        public static bool TryResolve(
            ESWorkbenchMutationKind kind,
            Vector2 startPointer,
            Vector2 currentPointer,
            Vector3 startValue,
            ESWorkbenchViewportFeelSettings feel,
            Func<Vector3, Vector3> snap,
            out Vector3 value)
        {
            return TryResolve(
                kind,
                startPointer,
                currentPointer,
                startValue,
                feel,
                snap,
                out value,
                out _);
        }

        public static bool TryResolve(
            ESWorkbenchMutationKind kind,
            Vector2 startPointer,
            Vector2 currentPointer,
            Vector3 startValue,
            ESWorkbenchViewportFeelSettings feel,
            Func<Vector3, Vector3> snap,
            out Vector3 value,
            out Vector3 unsnappedValue)
        {
            value = default;
            unsnappedValue = default;
            if ((kind != ESWorkbenchMutationKind.Rotate && kind != ESWorkbenchMutationKind.Scale)
                || !IsFinite(startPointer) || !IsFinite(currentPointer) || !IsFinite(startValue))
                return false;
            ESWorkbenchViewportFeelSettings profile = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            Vector2 delta = currentPointer - startPointer;
            if (!IsFinite(delta)) return false;
            Vector3 candidate;
            if (kind == ESWorkbenchMutationKind.Rotate)
            {
                candidate = startValue + new Vector3(
                    0f, delta.x * profile.RotationDegreesPerPixel, 0f);
            }
            else
            {
                float exponent = Mathf.Clamp(
                    (delta.x - delta.y) * profile.ScaleExponentPerPixel,
                    -20f,
                    20f);
                candidate = startValue * Mathf.Exp(exponent);
            }
            if (!IsFinite(candidate)) return false;
            unsnappedValue = candidate;
            candidate = snap?.Invoke(candidate) ?? candidate;
            if (!IsFinite(candidate))
            {
                unsnappedValue = default;
                return false;
            }
            if (kind == ESWorkbenchMutationKind.Scale)
            {
                candidate = new Vector3(
                    Mathf.Clamp(candidate.x, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.y, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.z, profile.MinimumTransformScale, profile.MaximumTransformScale));
            }
            value = candidate;
            return true;
        }

        /// <summary>
        /// 解析旋转/缩放的单事件增量。调用方保留原始起点用于取消回滚，
        /// 同时把上一事件的结果作为下一事件的基准，避免异常大事件造成跳变，
        /// 也避免把整段长拖动错误截断在单事件上限内。
        /// </summary>
        public static bool TryResolveIncremental(
            ESWorkbenchMutationKind kind,
            Vector2 previousPointer,
            Vector2 currentPointer,
            Vector3 previousValue,
            ESWorkbenchViewportFeelSettings feel,
            Func<Vector3, Vector3> snap,
            out Vector3 value)
        {
            return TryResolveIncremental(
                kind,
                previousPointer,
                currentPointer,
                previousValue,
                feel,
                snap,
                out value,
                out _,
                out _);
        }

        public static bool TryResolveIncremental(
            ESWorkbenchMutationKind kind,
            Vector2 previousPointer,
            Vector2 currentPointer,
            Vector3 previousValue,
            ESWorkbenchViewportFeelSettings feel,
            Func<Vector3, Vector3> snap,
            out Vector3 value,
            out Vector3 unsnappedValue)
        {
            return TryResolveIncremental(
                kind,
                previousPointer,
                currentPointer,
                previousValue,
                feel,
                snap,
                out value,
                out unsnappedValue,
                out _);
        }

        /// <summary>
        /// 解析增量并返回本次实际消耗到的指针位置。输入事件超过单事件上限时，
        /// 调用方必须把 consumedPointer 作为下一次基准，避免限幅位移永久丢失。
        /// </summary>
        public static bool TryResolveIncremental(
            ESWorkbenchMutationKind kind,
            Vector2 previousPointer,
            Vector2 currentPointer,
            Vector3 previousValue,
            ESWorkbenchViewportFeelSettings feel,
            Func<Vector3, Vector3> snap,
            out Vector3 value,
            out Vector3 unsnappedValue,
            out Vector2 consumedPointer)
        {
            value = default;
            unsnappedValue = default;
            consumedPointer = previousPointer;
            if ((kind != ESWorkbenchMutationKind.Rotate && kind != ESWorkbenchMutationKind.Scale)
                || !IsFinite(previousPointer) || !IsFinite(currentPointer) || !IsFinite(previousValue))
                return false;
            ESWorkbenchViewportFeelSettings profile = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            if (!profile.TryConsumePointerDelta(
                    previousPointer,
                    currentPointer,
                    out Vector2 delta,
                    out consumedPointer)) return false;
            Vector3 candidate;
            if (kind == ESWorkbenchMutationKind.Rotate)
            {
                candidate = previousValue + new Vector3(
                    0f, delta.x * profile.RotationDegreesPerPixel, 0f);
            }
            else
            {
                float exponent = Mathf.Clamp(
                    (delta.x - delta.y) * profile.ScaleExponentPerPixel,
                    -20f,
                    20f);
                candidate = previousValue * Mathf.Exp(exponent);
            }
            if (!IsFinite(candidate))
            {
                consumedPointer = previousPointer;
                return false;
            }
            if (kind == ESWorkbenchMutationKind.Scale)
            {
                candidate = new Vector3(
                    Mathf.Clamp(candidate.x, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.y, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.z, profile.MinimumTransformScale, profile.MaximumTransformScale));
            }
            unsnappedValue = candidate;
            candidate = snap?.Invoke(candidate) ?? candidate;
            if (!IsFinite(candidate))
            {
                unsnappedValue = default;
                consumedPointer = previousPointer;
                return false;
            }
            if (kind == ESWorkbenchMutationKind.Scale)
            {
                candidate = new Vector3(
                    Mathf.Clamp(candidate.x, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.y, profile.MinimumTransformScale, profile.MaximumTransformScale),
                    Mathf.Clamp(candidate.z, profile.MinimumTransformScale, profile.MaximumTransformScale));
            }
            value = candidate;
            return true;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 外部 Camera 与公共轨道状态之间的领域无关绑定合同。
    /// Prefab、Scene、World 和构图预览都应通过此入口同步，避免各自复制
    /// FOV、纵横比、表示半径和无效姿态的修复逻辑。
    /// </summary>
    public static class ESWorkbenchOrbitCameraBinding
    {
        /// <summary>
        /// 以外部 Camera 的真实构图距离捕获轨道状态。
        /// 这是预览宿主首次接入的首选入口；状态距离的反解必须留在公共底座，
        /// 避免领域代码把表示半径或纵横比换算写死。
        /// </summary>
        public static bool TryCaptureExternalCameraAtDistance(
            ESWorkbenchOrbitCameraState state,
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float actualCameraDistance,
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            if (state == null
                || !IsFinite(actualCameraDistance)
                || actualCameraDistance <= 0f)
                return false;

            float stateDistance = state.ResolveStateDistanceForPresentationCameraDistance(
                actualCameraDistance,
                viewport,
                verticalFieldOfViewDegrees);
            return TryCaptureExternalCamera(
                state,
                cameraPosition,
                cameraRotation,
                stateDistance,
                viewport,
                verticalFieldOfViewDegrees);
        }

        public static bool TryCaptureExternalCamera(
            ESWorkbenchOrbitCameraState state,
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float stateDistance,
            Rect viewport,
            float verticalFieldOfViewDegrees = -1f)
        {
            if (state == null || !IsFinite(cameraPosition) || !IsFinite(cameraRotation)
                || !IsFinite(stateDistance) || stateDistance <= 0f)
                return false;

            Vector3 euler = cameraRotation.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, euler.y);
            float pitch = Mathf.Clamp(
                Mathf.DeltaAngle(0f, euler.x),
                -89.9f,
                89.9f);
            float actualDistance = state.ResolvePresentationCameraDistance(
                stateDistance, viewport, verticalFieldOfViewDegrees);
            Quaternion normalizedRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = cameraPosition + normalizedRotation * Vector3.forward * actualDistance;
            if (!IsFinite(focus)) return false;
            state.SetView(focus, stateDistance, yaw, pitch);
            return true;
        }

        public static bool TryApplyToExternalCamera(
            ESWorkbenchOrbitCameraState state,
            Rect viewport,
            out Vector3 cameraPosition,
            out Quaternion cameraRotation,
            float verticalFieldOfViewDegrees = -1f)
        {
            cameraPosition = default;
            cameraRotation = Quaternion.identity;
            if (state == null) return false;
            cameraRotation = Quaternion.Euler(state.Pitch, state.Yaw, 0f);
            float actualDistance = state.ResolvePresentationCameraDistance(
                viewport, verticalFieldOfViewDegrees);
            cameraPosition = state.Focus + cameraRotation * Vector3.back * actualDistance;
            return IsFinite(cameraPosition) && IsFinite(cameraRotation);
        }

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.z) && IsFinite(value.w);
        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 旋转/缩放拖动的领域无关状态机。
    /// 它保存原始起点、实际消费到的指针和未吸附累计值，统一处理首帧阈值、
    /// 单事件限幅、吸附预览以及释放端点；不持有 Unity 对象、选择或事务。
    /// </summary>
    public sealed class ESWorkbenchTransformGestureSession
    {
        private readonly ESWorkbenchViewportFeelSettings feel;
        private ESWorkbenchMutationKind kind;
        private Vector2 startPointer;
        private Vector2 consumedPointer;
        private Vector3 startValue;
        private Vector3 accumulatedValue;

        public ESWorkbenchTransformGestureSession(
            ESWorkbenchViewportFeelSettings feel = null)
        {
            this.feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
        }

        public bool IsActive { get; private set; }
        public ESWorkbenchMutationKind Kind => kind;
        public Vector2 StartPointer => startPointer;
        public Vector2 ConsumedPointer => consumedPointer;
        public Vector3 StartValue => startValue;
        public Vector3 AccumulatedValue => accumulatedValue;

        public bool Begin(
            ESWorkbenchMutationKind gestureKind,
            Vector2 pointer,
            Vector3 value)
        {
            if (IsActive
                || (gestureKind != ESWorkbenchMutationKind.Rotate
                    && gestureKind != ESWorkbenchMutationKind.Scale)
                || !IsFinite(pointer) || !IsFinite(value))
                return false;
            kind = gestureKind;
            startPointer = pointer;
            consumedPointer = pointer;
            startValue = value;
            accumulatedValue = value;
            IsActive = true;
            return true;
        }

        public bool TryUpdate(
            Vector2 pointer,
            Func<Vector3, Vector3> snap,
            out Vector3 value)
        {
            value = default;
            if (!IsActive) return false;
            if (!ESWorkbenchTransformGestureResolver.TryResolveIncremental(
                    kind,
                    consumedPointer,
                    pointer,
                    accumulatedValue,
                    feel,
                    snap,
                    out value,
                    out Vector3 nextAccumulated,
                    out Vector2 nextConsumed))
                return false;
            accumulatedValue = nextAccumulated;
            consumedPointer = nextConsumed;
            return true;
        }

        public bool TryFinalize(
            Vector2 pointer,
            Func<Vector3, Vector3> snap,
            out Vector3 value)
        {
            value = default;
            if (!IsActive) return false;
            if (!ESWorkbenchTransformGestureResolver.TryResolve(
                    kind,
                    startPointer,
                    pointer,
                    startValue,
                    feel,
                    snap,
                    out value,
                    out Vector3 nextAccumulated))
                return false;
            accumulatedValue = nextAccumulated;
            consumedPointer = pointer;
            return true;
        }

        public void Reset()
        {
            IsActive = false;
            kind = ESWorkbenchMutationKind.Move;
            startPointer = default;
            consumedPointer = default;
            startValue = default;
            accumulatedValue = default;
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>统一键盘微调方向和步长；视口只负责解析当前选择并提交正式移动。</summary>
    public static class ESWorkbenchNudgeResolver
    {
        public static bool TryResolveDelta(
            KeyCode keyCode,
            bool shift,
            bool controlOrCommand,
            ESWorkbenchViewportFeelSettings feel,
            out Vector3 delta)
        {
            delta = Vector3.zero;
            Vector3 direction;
            switch (keyCode)
            {
                case KeyCode.LeftArrow: direction = Vector3.left; break;
                case KeyCode.RightArrow: direction = Vector3.right; break;
                case KeyCode.UpArrow: direction = Vector3.forward; break;
                case KeyCode.DownArrow: direction = Vector3.back; break;
                case KeyCode.PageUp: direction = Vector3.up; break;
                case KeyCode.PageDown: direction = Vector3.down; break;
                default: return false;
            }
            ESWorkbenchViewportFeelSettings profile = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            float multiplier = controlOrCommand
                ? profile.NudgeFineMultiplier
                : shift ? profile.NudgeCoarseMultiplier : 1f;
            delta = direction * profile.NudgeWorldUnits * multiplier;
            return IsFinite(delta) && delta.sqrMagnitude > 0.0000001f;
        }

        public static bool TryResolvePosition(
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> hierarchy,
            ESWorkbenchSelection selection,
            out Vector3 position)
        {
            position = default;
            if (selection == null || string.IsNullOrWhiteSpace(selection.StableId)
                || hierarchy == null) return false;
            for (int i = 0; i < hierarchy.Count; i++)
            {
                ESWorkbenchHierarchyDescriptor descriptor = hierarchy[i];
                if (descriptor?.Spatial == null
                    || !string.Equals(descriptor.ItemId, selection.StableId, StringComparison.Ordinal)) continue;
                position = descriptor.Spatial.Position;
                return IsFinite(position);
            }
            return false;
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    public static class ESWorkbenchPrecisionTransformResolver
    {
        public static bool TryResolve(
            ESWorkbenchPrecisionTransformMode mode,
            ESWorkbenchMutationKind kind,
            ESWorkbenchSpatialDescriptor spatial,
            Vector3 input,
            out Vector3 value,
            out string error)
        {
            value = default;
            error = string.Empty;
            if (spatial == null)
            {
                error = "缺少当前对象的空间投影。";
                return false;
            }
            if (kind != ESWorkbenchMutationKind.Move
                && kind != ESWorkbenchMutationKind.Rotate
                && kind != ESWorkbenchMutationKind.Scale)
            {
                error = "精确变换只接受位置、旋转或缩放操作。";
                return false;
            }
            Vector3 baseline = kind == ESWorkbenchMutationKind.Move ? spatial.Position
                : kind == ESWorkbenchMutationKind.Rotate ? spatial.RotationEuler : spatial.Size;
            value = mode == ESWorkbenchPrecisionTransformMode.Delta ? baseline + input : input;
            if (!IsFinite(value))
            {
                error = "数值包含 NaN 或 Infinity，未提交。";
                return false;
            }
            if (kind == ESWorkbenchMutationKind.Scale && (value.x <= 0f || value.y <= 0f || value.z <= 0f))
            {
                error = "缩放或尺寸必须全部大于 0，未提交。";
                return false;
            }
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>通过正式 Authoring Service 提交的通用精确变换面板。</summary>
    public sealed class ESWorkbenchPrecisionTransformElement : VisualElement
    {
        private readonly ESWorkbenchViewportContext context;
        private readonly ESWorkbenchSelection selection;
        private readonly IReadOnlyList<ESWorkbenchSelection> selections;
        private readonly ESWorkbenchSpatialDescriptor spatial;
        private readonly Vector3Field positionField;
        private readonly Vector3Field rotationField;
        private readonly Vector3Field scaleField;
        private readonly Label feedback;
        private readonly Button absoluteButton;
        private readonly Button deltaButton;
        private ESWorkbenchPrecisionTransformMode mode;

        public ESWorkbenchPrecisionTransformElement(
            ESWorkbenchViewportContext context,
            ESWorkbenchSelection selection,
            ESWorkbenchSpatialDescriptor spatial,
            bool locked,
            IReadOnlyList<ESWorkbenchSelection> selections = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.selections = selections ?? new[] { selection };
            this.spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
            name = "ESWorkbenchPrecisionTransform";
            style.marginTop = 7f;
            style.marginBottom = 6f;
            style.paddingTop = 7f;
            style.paddingBottom = 7f;
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderTopColor = new Color(0.24f, 0.27f, 0.3f, 1f);
            style.borderBottomColor = new Color(0.24f, 0.27f, 0.3f, 1f);

            var heading = new Label("精确变换");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 5f;
            Add(heading);

            var modeRow = new VisualElement { name = "ESWorkbenchPrecisionMode" };
            modeRow.style.flexDirection = FlexDirection.Row;
            absoluteButton = CreateModeButton("绝对值", () => SetMode(ESWorkbenchPrecisionTransformMode.Absolute));
            deltaButton = CreateModeButton("增量", () => SetMode(ESWorkbenchPrecisionTransformMode.Delta));
            modeRow.Add(absoluteButton);
            modeRow.Add(deltaButton);
            Add(modeRow);

            bool editsRectangleSize = spatial.Shape == ESWorkbenchSpatialShape.Rectangle;
            positionField = CreateVectorField("位置");
            rotationField = CreateVectorField("旋转");
            scaleField = CreateVectorField(editsRectangleSize ? "尺寸" : "缩放");
            Add(CreateOperationRow(positionField, "应用位置", () => Commit(ESWorkbenchMutationKind.Move)));
            Add(CreateOperationRow(rotationField, "应用旋转", () => Commit(ESWorkbenchMutationKind.Rotate)));
            Add(CreateOperationRow(scaleField, editsRectangleSize ? "应用尺寸" : "应用缩放",
                () => Commit(ESWorkbenchMutationKind.Scale)));

            bool canMove = !locked && context.Actions.Authoring.CanMove(selection);
            bool canRotate = !locked && context.Actions.Authoring.CanRotate(selection);
            bool canScale = !locked && context.Actions.Authoring.CanScale(selection);
            positionField.parent.SetEnabled(canMove);
            rotationField.parent.SetEnabled(canRotate);
            scaleField.parent.SetEnabled(canScale);

            Add(CreateSnapControls());
            feedback = new Label(locked ? "当前对象或视口只读，精确变换不可提交。" : "数值仅在点击应用后提交，并进入 Undo。")
            {
                name = "ESWorkbenchPrecisionFeedback"
            };
            feedback.style.whiteSpace = WhiteSpace.Normal;
            feedback.style.fontSize = 9f;
            feedback.style.marginTop = 5f;
            Add(feedback);
            SetMode(ESWorkbenchPrecisionTransformMode.Absolute);
        }

        internal ESWorkbenchPrecisionTransformMode Mode => mode;
        internal Vector3 PositionValue => positionField.value;
        internal Vector3 RotationValue => rotationField.value;
        internal Vector3 ScaleValue => scaleField.value;

        private VisualElement CreateSnapControls()
        {
            var root = new VisualElement { name = "ESWorkbenchPrecisionSnap" };
            root.style.marginTop = 6f;
            var enabled = new Toggle("启用吸附") { value = context.Layout.snapEnabled };
            enabled.RegisterValueChangedCallback(evt => context.Layout.snapEnabled = evt.newValue);
            root.Add(enabled);
            root.Add(CreateSnapField("位置步长", context.Layout.moveSnap, 0.001f,
                value => context.Layout.moveSnap = value));
            root.Add(CreateSnapField("旋转步长", context.Layout.rotationSnap, 0.1f,
                value => context.Layout.rotationSnap = value));
            root.Add(CreateSnapField("缩放步长", context.Layout.scaleSnap, 0.001f,
                value => context.Layout.scaleSnap = value));
            return root;
        }

        private static FloatField CreateSnapField(string label, float value, float minimum, Action<float> assign)
        {
            var field = new FloatField(label) { value = Mathf.Max(minimum, value) };
            field.RegisterValueChangedCallback(evt =>
            {
                float sanitized = !float.IsNaN(evt.newValue) && !float.IsInfinity(evt.newValue)
                    ? Mathf.Max(minimum, evt.newValue)
                    : minimum;
                field.SetValueWithoutNotify(sanitized);
                assign(sanitized);
            });
            return field;
        }

        private static Vector3Field CreateVectorField(string label)
        {
            var field = new Vector3Field(label);
            field.style.flexGrow = 1f;
            field.style.minWidth = 0f;
            return field;
        }

        private static Button CreateModeButton(string label, Action clicked)
        {
            var button = new Button(clicked) { text = label };
            button.style.flexGrow = 1f;
            button.style.height = 23f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 3f;
            return button;
        }

        private static VisualElement CreateOperationRow(Vector3Field field, string actionName, Action commit)
        {
            var row = new VisualElement();
            row.style.marginTop = 4f;
            row.Add(field);
            var button = new Button(commit) { text = actionName };
            button.style.height = 24f;
            button.style.marginTop = 2f;
            row.Add(button);
            return row;
        }

        private void SetMode(ESWorkbenchPrecisionTransformMode next)
        {
            mode = next;
            bool absolute = mode == ESWorkbenchPrecisionTransformMode.Absolute;
            positionField.label = absolute ? "位置" : "位置增量";
            rotationField.label = absolute ? "旋转" : "旋转增量";
            bool editsRectangleSize = spatial.Shape == ESWorkbenchSpatialShape.Rectangle;
            scaleField.label = absolute
                ? editsRectangleSize ? "尺寸" : "缩放"
                : editsRectangleSize ? "尺寸增量" : "缩放增量";
            positionField.SetValueWithoutNotify(absolute ? spatial.Position : Vector3.zero);
            rotationField.SetValueWithoutNotify(absolute ? spatial.RotationEuler : Vector3.zero);
            scaleField.SetValueWithoutNotify(absolute ? spatial.Size : Vector3.zero);
            Color active = new Color(0.18f, 0.43f, 0.66f, 0.9f);
            Color inactive = new Color(0.14f, 0.15f, 0.17f, 1f);
            absoluteButton.style.backgroundColor = absolute ? active : inactive;
            deltaButton.style.backgroundColor = absolute ? inactive : active;
        }

        private void Commit(ESWorkbenchMutationKind kind)
        {
            Vector3 input = kind == ESWorkbenchMutationKind.Move ? positionField.value
                : kind == ESWorkbenchMutationKind.Rotate ? rotationField.value : scaleField.value;
            if (!ESWorkbenchPrecisionTransformResolver.TryResolve(
                    mode, kind, spatial, input, out Vector3 value, out string validationError))
            {
                Report(validationError, MessageType.Warning);
                return;
            }
            value = kind == ESWorkbenchMutationKind.Move ? context.SnapPosition(value)
                : kind == ESWorkbenchMutationKind.Rotate ? context.SnapRotation(value) : context.SnapScale(value);
            if (!ESWorkbenchPrecisionTransformResolver.TryResolve(
                    ESWorkbenchPrecisionTransformMode.Absolute,
                    kind,
                    spatial,
                    value,
                    out value,
                    out validationError))
            {
                Report("吸附后的" + validationError, MessageType.Warning);
                return;
            }
            bool succeeded;
            string message;
            if (selections.Count > 1)
            {
                succeeded = kind == ESWorkbenchMutationKind.Move
                    ? context.Actions.Authoring.TryMoveMany(selections, value, out message)
                    : kind == ESWorkbenchMutationKind.Rotate
                        ? context.Actions.Authoring.TryRotateMany(selections, value, out message)
                        : context.Actions.Authoring.TryScaleMany(selections, value, out message);
            }
            else
            {
                succeeded = kind == ESWorkbenchMutationKind.Move
                    ? context.Actions.Authoring.TryMove(selection, value, out message)
                    : kind == ESWorkbenchMutationKind.Rotate
                        ? context.Actions.Authoring.TryRotate(selection, value, out message)
                        : context.Actions.Authoring.TryScale(selection, value, out message);
            }
            Report(string.IsNullOrWhiteSpace(message)
                    ? succeeded ? "精确变换已提交。" : "精确变换提交失败。"
                    : message,
                succeeded ? MessageType.Info : MessageType.Error);
        }

        private void Report(string message, MessageType type)
        {
            feedback.text = message;
            context.Actions.SetStatus(message, type);
        }
    }
}
#endif
