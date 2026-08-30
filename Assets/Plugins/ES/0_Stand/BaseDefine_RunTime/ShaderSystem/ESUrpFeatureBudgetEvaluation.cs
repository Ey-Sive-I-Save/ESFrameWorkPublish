using System;

namespace ES
{
    public readonly struct ESUrpFeatureSnapshot
    {
        public ESUrpFeatureSnapshot(int rendererFeatureCount, int shadowLevel, int ssaoSamples, bool dynamicResolutionEnabled)
        {
            RendererFeatureCount = Math.Max(0, rendererFeatureCount);
            ShadowLevel = Math.Max(0, shadowLevel);
            SsaoSamples = Math.Max(0, ssaoSamples);
            DynamicResolutionEnabled = dynamicResolutionEnabled;
        }

        public int RendererFeatureCount { get; }
        public int ShadowLevel { get; }
        public int SsaoSamples { get; }
        public bool DynamicResolutionEnabled { get; }
    }

    public static class ESUrpFeatureBudgetEvaluator
    {
        public static bool TryEvaluate(
            ESRenderQualityPolicy policy,
            ESUrpFeatureSnapshot snapshot,
            int maxRendererFeatureCount,
            out string reason)
        {
            if (!policy.IsValid(out reason)) return false;
            if (maxRendererFeatureCount < 0) { reason = "renderer-feature-budget-invalid"; return false; }
            if (snapshot.RendererFeatureCount > maxRendererFeatureCount) { reason = "renderer-feature-budget-exceeded"; return false; }
            if (snapshot.ShadowLevel > policy.ShadowLevel) { reason = "shadow-level-exceeded"; return false; }
            if (snapshot.SsaoSamples > policy.SsaoSamples) { reason = "ssao-samples-exceeded"; return false; }
            if (!policy.DynamicResolutionAllowed && snapshot.DynamicResolutionEnabled)
            {
                reason = "dynamic-resolution-not-allowed";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
