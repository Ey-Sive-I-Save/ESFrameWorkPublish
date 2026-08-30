using System;

namespace ES
{
    public enum ESRenderModuleTransitionState : byte
    {
        Unresolved = 0,
        Ready = 1,
        ApplyRequested = 2,
        RollbackRequested = 3,
        Rejected = 4
    }

    public enum ESRenderModuleEvidenceState : byte
    {
        None = 0,
        ApplyVerified = 1,
        RollbackRequired = 2,
        RolledBack = 3,
        Failed = 4
    }

    /// <summary>
    /// ES 渲染能力的 GameManager 注册模块。
    /// 只负责保存模板意图、解析 Dry-Run 计划和暴露可审计状态；不直接写 Unity 后端。
    /// </summary>
    [Serializable]
    public sealed class ESRenderModule : ESSystemModule
    {
        public ESRenderVisualStyleId style = ESRenderVisualStyleId.NaturalPbr;
        public ESRenderSceneIntentId sceneIntent = ESRenderSceneIntentId.Exploration;
        public ESRenderPlatformId platform = ESRenderPlatformId.Desktop;
        public ESRenderContentTypeId contentType = ESRenderContentTypeId.RolePlaying;

        [NonSerialized]
        private ESRenderTemplatePlan currentPlan;
        [NonSerialized]
        private bool resolved;
        [NonSerialized]
        private string lastError;
        [NonSerialized]
        private ESRenderModuleTransitionState transitionState;
        [NonSerialized]
        private ESRenderModuleEvidenceState evidenceState;
        [NonSerialized]
        private ESRenderBackendReceipt lastReceipt;

        public bool IsResolved { get { return resolved; } }
        public string LastError { get { return lastError ?? string.Empty; } }
        public ESRenderModuleTransitionState TransitionState { get { return transitionState; } }
        public ESRenderModuleEvidenceState EvidenceState { get { return evidenceState; } }
        public ESRenderBackendReceipt LastReceipt { get { return lastReceipt; } }
        public ESRenderTemplatePlan CurrentPlan { get { return currentPlan; } }

        public override void Start()
        {
            ResolveTemplate();
        }

        public bool RequestTemplate(ESRenderVisualStyleId requestedStyle, ESRenderSceneIntentId requestedIntent, ESRenderPlatformId requestedPlatform)
        {
            style = requestedStyle;
            sceneIntent = requestedIntent;
            platform = requestedPlatform;
            return ResolveTemplate();
        }

        public bool RequestContentType(ESRenderContentTypeId requestedContentType, ESRenderPlatformId requestedPlatform)
        {
            return RequestSceneTemplate(requestedContentType, requestedPlatform);
        }

        public bool RequestSceneTemplate(ESRenderContentTypeId requestedContentType, ESRenderPlatformId requestedPlatform)
        {
            ESRenderSceneTemplateDescriptor descriptor;
            ESRenderTemplatePlan plan;
            string reason;
            if (!ESRenderSceneTemplatePlanFactory.TryCreate(requestedContentType, requestedPlatform, out descriptor, out plan, out reason))
                return Reject(reason);

            contentType = requestedContentType;
            platform = requestedPlatform;
            style = descriptor.Style;
            sceneIntent = descriptor.Intent;
            currentPlan = plan;
            resolved = true;
            transitionState = ESRenderModuleTransitionState.Ready;
            evidenceState = ESRenderModuleEvidenceState.None;
            lastError = string.Empty;
            return true;
        }

        public bool RequestApply(string planId)
        {
            if (!resolved || string.IsNullOrEmpty(planId) || !string.Equals(planId, currentPlan.PlanId, StringComparison.Ordinal))
                return Reject("apply-plan-mismatch");

            transitionState = ESRenderModuleTransitionState.ApplyRequested;
            evidenceState = ESRenderModuleEvidenceState.None;
            lastError = string.Empty;
            return true;
        }

        public bool RequestRollback(string reason)
        {
            if (!resolved || (transitionState != ESRenderModuleTransitionState.ApplyRequested && transitionState != ESRenderModuleTransitionState.Rejected))
                return Reject("rollback-precondition-missing");

            transitionState = ESRenderModuleTransitionState.RollbackRequested;
            evidenceState = ESRenderModuleEvidenceState.RollbackRequired;
            lastError = string.IsNullOrEmpty(reason) ? "rollback-requested" : reason;
            return true;
        }

        public bool RecordApplyReceipt(string planId, ESRenderBackendReceipt receipt)
        {
            if (!resolved || transitionState != ESRenderModuleTransitionState.ApplyRequested || !string.Equals(planId, currentPlan.PlanId, StringComparison.Ordinal))
                return Reject("apply-receipt-plan-or-state-mismatch");

            lastReceipt = receipt;
            if (receipt.Status == ESRenderBackendReceiptStatus.Verified)
            {
                evidenceState = ESRenderModuleEvidenceState.ApplyVerified;
                lastError = string.Empty;
                return true;
            }
            if (receipt.Status == ESRenderBackendReceiptStatus.RollbackRequired)
            {
                transitionState = ESRenderModuleTransitionState.RollbackRequested;
                evidenceState = ESRenderModuleEvidenceState.RollbackRequired;
            }
            else
            {
                evidenceState = ESRenderModuleEvidenceState.Failed;
            }
            lastError = receipt.Reason;
            return false;
        }

        public bool RecordRollbackReceipt(string planId, ESRenderBackendReceipt receipt)
        {
            if (!resolved || transitionState != ESRenderModuleTransitionState.RollbackRequested || !string.Equals(planId, currentPlan.PlanId, StringComparison.Ordinal))
                return Reject("rollback-receipt-plan-or-state-mismatch");

            lastReceipt = receipt;
            if (receipt.Status == ESRenderBackendReceiptStatus.RolledBack)
            {
                transitionState = ESRenderModuleTransitionState.Ready;
                evidenceState = ESRenderModuleEvidenceState.RolledBack;
                lastError = string.Empty;
                return true;
            }
            evidenceState = ESRenderModuleEvidenceState.Failed;
            lastError = receipt.Reason;
            return false;
        }

        public bool ResolveTemplate()
        {
            ESRenderTemplatePlan plan;
            string reason;
            if (!ESRenderTemplatePlan.TryCreate(style, sceneIntent, platform, contentType, out plan, out reason))
            {
                currentPlan = default(ESRenderTemplatePlan);
                resolved = false;
                transitionState = ESRenderModuleTransitionState.Rejected;
                lastError = string.IsNullOrEmpty(reason) ? "ES render template resolution failed." : reason;
                return false;
            }

            currentPlan = plan;
            resolved = true;
            transitionState = ESRenderModuleTransitionState.Ready;
            lastError = string.Empty;
            return true;
        }

        private bool Reject(string reason)
        {
            transitionState = ESRenderModuleTransitionState.Rejected;
            evidenceState = ESRenderModuleEvidenceState.Failed;
            lastError = reason;
            return false;
        }
    }

    /// <summary>
    /// 将模块模板意图投影为后端 Dry-Run 计划；不调用 Unity API，也不构造 Gate。
    /// </summary>
    public static class ESRenderModuleBackendAdapter
    {
        public readonly struct ESRenderBackendGateRequest
        {
            public ESRenderBackendGateRequest(
                ESRenderBackendChangePlan plan,
                ESRenderBackendSnapshot observed,
                string idempotencyKey,
                bool userDirected)
            {
                Plan = plan;
                Observed = observed;
                IdempotencyKey = idempotencyKey ?? string.Empty;
                UserDirected = userDirected;
            }

            public ESRenderBackendChangePlan Plan { get; }
            public ESRenderBackendSnapshot Observed { get; }
            public string IdempotencyKey { get; }
            public bool UserDirected { get; }
        }

        public static bool TryCreateBackendPlan(
            ESRenderModule module,
            ESRenderBackendSnapshot before,
            out ESRenderBackendChangePlan plan,
            out string reason)
        {
            if (module == null || !module.IsResolved)
            {
                plan = default(ESRenderBackendChangePlan);
                reason = "render-module-template-not-resolved";
                return false;
            }

            if (!ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                module.CurrentPlan.QualityPolicy,
                module.CurrentPlan.LightingRecipe,
                out plan,
                out reason))
                return false;

            return true;
        }

        public static bool TryCreateBackendPlan(
            ESRenderModule module,
            IESRenderLightingTarget lightingTarget,
            out ESRenderBackendSnapshot observed,
            out ESRenderBackendChangePlan plan,
            out string reason)
        {
            observed = default(ESRenderBackendSnapshot);
            plan = default(ESRenderBackendChangePlan);
            if (module == null || !module.IsResolved)
            {
                reason = "render-module-template-not-resolved";
                return false;
            }
            if (lightingTarget == null)
            {
                reason = "lighting-target-required-for-module-plan";
                return false;
            }
            if (!ESRenderBackendSnapshot.TryCapture(lightingTarget, out observed, out reason))
                return false;
            return ESRenderBackendChangePlan.TryCreateDryRun(
                observed,
                module.CurrentPlan.QualityPolicy,
                module.CurrentPlan.LightingRecipe,
                out plan,
                out reason);
        }

        public static string BuildIdempotencyKey(
            ESRenderModule module,
            ESRenderBackendSnapshot before)
        {
            if (module == null || !module.IsResolved)
                return string.Empty;

            return "es.render.apply." + module.CurrentPlan.PlanId + ".q" + before.QualityIndex;
        }

        public static bool TryBuildGateRequest(
            ESRenderModule module,
            ESRenderBackendSnapshot observed,
            bool userDirected,
            out ESRenderBackendGateRequest request,
            out string reason)
        {
            ESRenderBackendChangePlan plan;
            if (!TryCreateBackendPlan(module, observed, out plan, out reason))
            {
                request = default(ESRenderBackendGateRequest);
                return false;
            }

            request = new ESRenderBackendGateRequest(
                plan, observed, BuildIdempotencyKey(module, observed), userDirected);
            reason = string.Empty;
            return true;
        }

        public static bool TryBuildGateRequest(
            ESRenderModule module,
            IESRenderLightingTarget lightingTarget,
            bool userDirected,
            out ESRenderBackendGateRequest request,
            out string reason)
        {
            ESRenderBackendSnapshot observed;
            ESRenderBackendChangePlan plan;
            if (!TryCreateBackendPlan(module, lightingTarget, out observed, out plan, out reason))
            {
                request = default(ESRenderBackendGateRequest);
                return false;
            }

            request = new ESRenderBackendGateRequest(
                plan, observed, BuildIdempotencyKey(module, observed), userDirected);
            reason = string.Empty;
            return true;
        }
    }
}
