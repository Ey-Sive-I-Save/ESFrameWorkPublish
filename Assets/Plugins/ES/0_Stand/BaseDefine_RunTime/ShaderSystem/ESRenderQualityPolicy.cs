using System;

namespace ES
{
    /// <summary>
    /// ES 对渲染质量意图的稳定后端投影。
    /// 这里只保存可验证的策略数据，不直接写入 Unity/URP 状态。
    /// Resolve 返回的是 ES 设计目标；未经目标平台基线与 Profiler 校准，不得当作性能验收阈值。
    /// </summary>
    public enum ESRenderQualityProfileId
    {
        Performant = 0,
        Balanced = 1,
        HighFidelity = 2,
        CombatReadability = 10,
        CinematicShowcase = 11,
        MobileStable = 12
    }

    public readonly struct ESRenderQualityPolicy
    {
        public ESRenderQualityPolicy(
            ESRenderQualityProfileId profile,
            int shadowLevel,
            int ssaoSamples,
            int transparencyBudget,
            int particleBudget,
            int shaderVariantBudget,
            float targetFrameMilliseconds,
            bool dynamicResolutionAllowed,
            bool preserveCombatReadability)
        {
            Profile = profile;
            ShadowLevel = Math.Max(0, shadowLevel);
            SsaoSamples = Math.Max(0, ssaoSamples);
            TransparencyBudget = Math.Max(0, transparencyBudget);
            ParticleBudget = Math.Max(0, particleBudget);
            ShaderVariantBudget = Math.Max(0, shaderVariantBudget);
            TargetFrameMilliseconds = Math.Max(0.1f, targetFrameMilliseconds);
            DynamicResolutionAllowed = dynamicResolutionAllowed;
            PreserveCombatReadability = preserveCombatReadability;
        }

        public ESRenderQualityProfileId Profile { get; }
        public int ShadowLevel { get; }
        public int SsaoSamples { get; }
        public int TransparencyBudget { get; }
        public int ParticleBudget { get; }
        public int ShaderVariantBudget { get; }
        public float TargetFrameMilliseconds { get; }
        public bool DynamicResolutionAllowed { get; }
        public bool PreserveCombatReadability { get; }

        public bool IsValid(out string reason)
        {
            if (TransparencyBudget == 0 && ParticleBudget > 0)
            {
                reason = "particle-budget-requires-transparent-budget";
                return false;
            }

            if (Profile == ESRenderQualityProfileId.HighFidelity && ShaderVariantBudget < 16)
            {
                reason = "high-fidelity-variant-budget-too-small";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderQualityPolicy Resolve(ESRenderQualityProfileId profile)
        {
            switch (profile)
            {
                case ESRenderQualityProfileId.Performant:
                    return new ESRenderQualityPolicy(profile, 0, 0, 24, 64, 8, 16.6f, true, true);
                case ESRenderQualityProfileId.Balanced:
                    return new ESRenderQualityPolicy(profile, 1, 4, 48, 128, 16, 16.6f, true, true);
                case ESRenderQualityProfileId.HighFidelity:
                    return new ESRenderQualityPolicy(profile, 2, 8, 96, 256, 32, 16.6f, false, true);
                case ESRenderQualityProfileId.CombatReadability:
                    return new ESRenderQualityPolicy(profile, 1, 4, 32, 96, 16, 16.6f, true, true);
                case ESRenderQualityProfileId.CinematicShowcase:
                    return new ESRenderQualityPolicy(profile, 2, 8, 128, 320, 32, 33.3f, false, true);
                case ESRenderQualityProfileId.MobileStable:
                    return new ESRenderQualityPolicy(profile, 0, 0, 16, 48, 8, 33.3f, true, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown ES render quality profile.");
            }
        }
    }
}
