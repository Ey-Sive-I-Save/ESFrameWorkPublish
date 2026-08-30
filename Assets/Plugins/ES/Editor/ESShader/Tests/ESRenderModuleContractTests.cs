using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESRenderModuleContractTests
    {
        [Test]
        public void ContentTypeRequest_ResolvesStableTemplate()
        {
            var module = new ESRenderModule();
            Assert.That(module.RequestContentType(ESRenderContentTypeId.Horror, ESRenderPlatformId.Desktop), Is.True);
            Assert.That(module.IsResolved, Is.True);
            Assert.That(module.CurrentPlan.IsDryRun, Is.True);
            Assert.That(module.TransitionState, Is.EqualTo(ESRenderModuleTransitionState.Ready));
        }

        [Test]
        public void GateRequest_UsesCurrentPlanIdentityAndStableKey()
        {
            var module = new ESRenderModule();
            Assert.That(module.RequestTemplate(ESRenderVisualStyleId.NeonSciFi, ESRenderSceneIntentId.Exploration, ESRenderPlatformId.Desktop), Is.True);
            var beforeLighting = ESRenderLightingRecipe.Resolve(ESRenderVisualStyleId.NeonSciFi, ESRenderQualityProfileId.Performant);
            var before = new ESRenderBackendSnapshot(0, "Performant", "UniversalRenderPipelineAsset", true, 3, "Performant\u001FBalanced\u001FHigh Fidelity", beforeLighting);
            ESRenderModuleBackendAdapter.ESRenderBackendGateRequest request;
            string reason;
            Assert.That(ESRenderModuleBackendAdapter.TryBuildGateRequest(module, before, true, out request, out reason), Is.True, reason);
            Assert.That(request.Plan.IsDryRun, Is.True);
            Assert.That(request.Plan.TargetLighting.HasValue, Is.True);
            Assert.That(request.Plan.TargetLighting.Value.Style, Is.EqualTo(ESRenderVisualStyleId.NeonSciFi));
            Assert.That(request.IdempotencyKey, Is.EqualTo("es.render.apply." + module.CurrentPlan.PlanId + ".q0"));
            Assert.That(request.UserDirected, Is.True);
        }

        [Test]
        public void ApplyAndRollbackReceipts_AdvanceOnlyFromMatchingStates()
        {
            var module = new ESRenderModule();
            Assert.That(module.RequestTemplate(ESRenderVisualStyleId.NaturalPbr, ESRenderSceneIntentId.Exploration, ESRenderPlatformId.Desktop), Is.True);
            string planId = module.CurrentPlan.PlanId;
            Assert.That(module.RequestApply(planId), Is.True);

            var before = new ESRenderBackendSnapshot(0, "Performant", "UniversalRenderPipelineAsset", true, 3, "Performant\u001FBalanced\u001FHigh Fidelity");
            var after = new ESRenderBackendSnapshot(1, "Balanced", "UniversalRenderPipelineAsset", true, 3, "Performant\u001FBalanced\u001FHigh Fidelity");
            var backendPlan = ESRenderBackendChangePlan.TryCreateDryRun(before, module.CurrentPlan.QualityPolicy, out var plan, out string reason) ? plan : default(ESRenderBackendChangePlan);
            Assert.That(reason, Is.Empty);
            var verified = ESRenderBackendReceipt.EvaluateApply(backendPlan, after, true);
            Assert.That(module.RecordApplyReceipt(planId, verified), Is.True);
            Assert.That(module.EvidenceState, Is.EqualTo(ESRenderModuleEvidenceState.ApplyVerified));

            Assert.That(module.RequestRollback("operator-request"), Is.False);
            Assert.That(module.EvidenceState, Is.EqualTo(ESRenderModuleEvidenceState.Failed));
        }

        [Test]
        public void SceneTemplateFactory_ProjectsContentTypeBudgetScales()
        {
            ESRenderSceneTemplateDescriptor descriptor;
            ESRenderTemplatePlan plan;
            string reason;
            Assert.That(ESRenderSceneTemplatePlanFactory.TryCreate(
                ESRenderContentTypeId.Racing,
                ESRenderPlatformId.Desktop,
                out descriptor,
                out plan,
                out reason), Is.True, reason);
            Assert.That(plan.Configuration.ContentType, Is.EqualTo(ESRenderContentTypeId.Racing));
            Assert.That(plan.Configuration.TransparencyBudgetScale, Is.EqualTo(0.85f));
            Assert.That(plan.Configuration.ParticleBudgetScale, Is.EqualTo(0.9f));
            Assert.That(plan.Configuration.EffectsRecipe.TransparentBudget, Is.EqualTo(54));
            Assert.That(plan.Configuration.EffectsRecipe.ParticleBudget, Is.EqualTo(173));
            Assert.That(descriptor.Resources.IsComplete(out reason), Is.True, reason);
        }

        [Test]
        public void SceneTemplateFactory_ResolvesEveryRegisteredContentType()
        {
            foreach (ESRenderContentTypeId contentType in System.Enum.GetValues(typeof(ESRenderContentTypeId)))
            {
                ESRenderSceneTemplateDescriptor descriptor;
                ESRenderTemplatePlan plan;
                string reason;
                Assert.That(ESRenderSceneTemplatePlanFactory.TryCreate(
                    contentType,
                    ESRenderPlatformId.Desktop,
                    out descriptor,
                    out plan,
                    out reason), Is.True, contentType + ": " + reason);
                Assert.That(descriptor.ContentType, Is.EqualTo(contentType));
                Assert.That(descriptor.Resources.IsComplete(out reason), Is.True, contentType + ": " + reason);
                Assert.That(plan.Configuration.ContentType, Is.EqualTo(contentType));
                Assert.That(plan.Configuration.TransparencyBudgetScale, Is.GreaterThan(0f));
                Assert.That(plan.Configuration.ParticleBudgetScale, Is.GreaterThan(0f));
            }
        }
    }
}
