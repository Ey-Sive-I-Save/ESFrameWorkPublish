using System;

namespace ES
{
    public readonly struct ESRenderBudgetSnapshot
    {
        public ESRenderBudgetSnapshot(int transparentObjects, int particleSystems, int compiledShaderVariants, float frameMilliseconds)
        {
            TransparentObjects = Math.Max(0, transparentObjects);
            ParticleSystems = Math.Max(0, particleSystems);
            CompiledShaderVariants = Math.Max(0, compiledShaderVariants);
            FrameMilliseconds = Math.Max(0f, frameMilliseconds);
        }

        public int TransparentObjects { get; }
        public int ParticleSystems { get; }
        public int CompiledShaderVariants { get; }
        public float FrameMilliseconds { get; }
    }

    public readonly struct ESRenderBudgetEvaluation
    {
        public ESRenderBudgetEvaluation(bool passed, string reason)
        {
            Passed = passed;
            Reason = reason ?? string.Empty;
        }

        public bool Passed { get; }
        public string Reason { get; }
    }

    public static class ESRenderBudgetEvaluator
    {
        public static ESRenderBudgetEvaluation Evaluate(ESRenderQualityPolicy policy, ESRenderBudgetSnapshot snapshot)
        {
            string reason;
            if (!policy.IsValid(out reason)) return new ESRenderBudgetEvaluation(false, reason);
            if (snapshot.TransparentObjects > policy.TransparencyBudget) return new ESRenderBudgetEvaluation(false, "transparent-budget-exceeded");
            if (snapshot.ParticleSystems > policy.ParticleBudget) return new ESRenderBudgetEvaluation(false, "particle-budget-exceeded");
            if (snapshot.CompiledShaderVariants > policy.ShaderVariantBudget) return new ESRenderBudgetEvaluation(false, "shader-variant-budget-exceeded");
            if (snapshot.FrameMilliseconds > policy.TargetFrameMilliseconds) return new ESRenderBudgetEvaluation(false, "frame-time-budget-exceeded");
            return new ESRenderBudgetEvaluation(true, string.Empty);
        }
    }
}
