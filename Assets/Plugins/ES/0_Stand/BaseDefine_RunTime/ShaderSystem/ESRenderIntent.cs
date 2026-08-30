using System;

namespace ES
{
    /// <summary>
    /// ES 渲染入口的最小用户意图。它不暴露 Unity 菜单、URP 类型或材质实现细节。
    /// </summary>
    public enum ESRenderVisualGoal
    {
        Default = 0,
        CombatReadability = 1,
        Cinematic = 2,
        MobileStability = 3
    }

    public enum ESRenderTargetPlatform
    {
        Unknown = 0,
        Desktop = 1,
        Mobile = 2,
        Console = 3
    }

    public readonly struct ESRenderIntent
    {
        public ESRenderIntent(
            ESRenderVisualGoal visualGoal,
            ESRenderTargetPlatform targetPlatform,
            bool requiresStableFrameTime,
            bool allowsDynamicResolution)
        {
            VisualGoal = visualGoal;
            TargetPlatform = targetPlatform;
            RequiresStableFrameTime = requiresStableFrameTime;
            AllowsDynamicResolution = allowsDynamicResolution;
        }

        public ESRenderVisualGoal VisualGoal { get; }
        public ESRenderTargetPlatform TargetPlatform { get; }
        public bool RequiresStableFrameTime { get; }
        public bool AllowsDynamicResolution { get; }

        public bool IsValid(out string reason)
        {
            if (VisualGoal == ESRenderVisualGoal.MobileStability
                && TargetPlatform == ESRenderTargetPlatform.Desktop)
            {
                reason = "mobile-stability-intent-targets-desktop";
                return false;
            }

            if (VisualGoal == ESRenderVisualGoal.Cinematic && RequiresStableFrameTime)
            {
                reason = "cinematic-intent-conflicts-with-strict-stable-frame-time";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 将 ES 语义意图确定性映射到质量策略；不执行任何 Unity/URP 写入。
    /// </summary>
    public static class ESRenderPolicyResolver
    {
        public static bool TryResolve(
            ESRenderIntent intent,
            out ESRenderQualityPolicy policy,
            out string reason)
        {
            if (!intent.IsValid(out reason))
            {
                policy = default(ESRenderQualityPolicy);
                return false;
            }

            ESRenderQualityProfileId profile;
            switch (intent.VisualGoal)
            {
                case ESRenderVisualGoal.CombatReadability:
                    profile = ESRenderQualityProfileId.CombatReadability;
                    break;
                case ESRenderVisualGoal.Cinematic:
                    profile = ESRenderQualityProfileId.CinematicShowcase;
                    break;
                case ESRenderVisualGoal.MobileStability:
                    profile = ESRenderQualityProfileId.MobileStable;
                    break;
                default:
                    profile = intent.RequiresStableFrameTime
                        ? ESRenderQualityProfileId.Performant
                        : ESRenderQualityProfileId.Balanced;
                    break;
            }

            policy = ESRenderQualityPolicy.Resolve(profile);
            if (intent.AllowsDynamicResolution != policy.DynamicResolutionAllowed)
            {
                reason = "intent-dynamic-resolution-does-not-match-profile-default";
                return false;
            }

            reason = string.Empty;
            return policy.IsValid(out reason);
        }
    }
}
