using NUnit.Framework;
using ES.EditorInternal;

namespace ES.Tests
{
    public sealed class ESRenderBackendContractTests
    {
        [Test]
        public void LightingRecipe_CoversEveryCatalogStyle()
        {
            for (int index = 0; index < ESRenderStyleCatalog.Count; index++)
            {
                ESRenderVisualStyleId style = ESRenderStyleCatalog.GetStyleIdAt(index);
                ESRenderLightingRecipe recipe = ESRenderLightingRecipe.Resolve(style);
                Assert.That(recipe.IsValid(out string reason), Is.True, style + ": " + reason);
            }
        }

        [Test]
        public void LightingRecipe_QualityProjection_CoversEveryStyleAndProfile()
        {
            ESRenderQualityProfileId[] profiles =
            {
                ESRenderQualityProfileId.Performant,
                ESRenderQualityProfileId.Balanced,
                ESRenderQualityProfileId.HighFidelity,
                ESRenderQualityProfileId.CombatReadability,
                ESRenderQualityProfileId.CinematicShowcase,
                ESRenderQualityProfileId.MobileStable
            };
            for (int index = 0; index < ESRenderStyleCatalog.Count; index++)
            {
                ESRenderVisualStyleId style = ESRenderStyleCatalog.GetStyleIdAt(index);
                for (int profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
                {
                    ESRenderLightingRecipe recipe = ESRenderLightingRecipe.Resolve(style, profiles[profileIndex]);
                    Assert.That(
                        recipe.IsValid(out string reason),
                        Is.True,
                        style + "/" + profiles[profileIndex] + ": " + reason);
                }
            }
        }

        [Test]
        public void LightingRecipe_TryResolveRejectsUnknownStyleWithoutThrowing()
        {
            ESRenderLightingRecipe recipe;
            Assert.That(ESRenderLightingRecipe.TryResolve(
                (ESRenderVisualStyleId)999,
                out recipe,
                out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("lighting-style-unknown"));
        }

        [Test]
        public void ChangePlan_HighFidelityQualityName_IsIdempotentlyRecognized()
        {
            var before = new ESRenderBackendSnapshot(2, "High Fidelity", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;

            Assert.That(
                ESRenderBackendChangePlan.TryCreateDryRun(
                    before,
                    ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.HighFidelity),
                    out plan,
                    out reason), Is.True, reason);
            Assert.That(plan.Status, Is.EqualTo(ESRenderBackendPlanStatus.NoChange));
            Assert.That(plan.IsDryRun, Is.False);
        }

        [Test]
        public void ChangePlan_CarriesValidatedLightingIntentWithoutApplyingIt()
        {
            var baselineLighting = ESRenderLightingRecipe.Resolve(ESRenderVisualStyleId.NaturalPbr, ESRenderQualityProfileId.Performant);
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true, 2, "Performant\u001FBalanced", baselineLighting);
            ESRenderBackendChangePlan plan;
            string reason;
            var lighting = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Balanced);

            Assert.That(
                ESRenderBackendChangePlan.TryCreateDryRun(
                    before,
                    ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                    lighting,
                    out plan,
                    out reason), Is.True, reason);
            Assert.That(plan.TargetLighting.HasValue, Is.True);
            Assert.That(plan.TargetLighting.Value.Style, Is.EqualTo(ESRenderVisualStyleId.NaturalPbr));
            Assert.That(plan.TargetLighting.Value.IsValid(out reason), Is.True, reason);
            Assert.That(plan.Status, Is.EqualTo(ESRenderBackendPlanStatus.DryRunReady));
            Assert.That(plan.RequiresUnityWriter, Is.True);
        }

        [Test]
        public void ChangePlan_RejectsInvalidLightingIntentBeforeWriter()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            var invalidLighting = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.Disabled,
                0,
                10f,
                0,
                false,
                false);

            Assert.That(
                ESRenderBackendChangePlan.TryCreateDryRun(
                    before,
                    ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                    invalidLighting,
                    out plan,
                    out reason), Is.False);
            Assert.That(plan.Status, Is.EqualTo(ESRenderBackendPlanStatus.Invalid));
            Assert.That(reason, Is.EqualTo("disabled-shadows-cannot-have-budget"));
        }

        [Test]
        public void LightingRecipe_PerformantRealtimeStyle_DisablesShadowBudgetCoherently()
        {
            var recipe = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Performant);

            Assert.That(recipe.ShadowMode, Is.EqualTo(ESRenderShadowMode.Disabled));
            Assert.That(recipe.AdditionalLightsPerObject, Is.EqualTo(0));
            Assert.That(recipe.ShadowDistance, Is.EqualTo(0f));
            Assert.That(recipe.CascadeCount, Is.EqualTo(0));
            Assert.That(recipe.IsValid(out string reason), Is.True, reason);
        }

        [Test]
        public void LightingRecipe_TryCreate_ReturnsActionableValidationReason()
        {
            ESRenderLightingRecipe recipe;
            string reason;
            Assert.That(ESRenderLightingRecipe.TryCreate(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.BakedOnly,
                0,
                40f,
                1,
                false,
                false,
                out recipe,
                out reason,
                contactShadows: true), Is.False);
            Assert.That(reason, Is.EqualTo("contact-shadows-require-realtime-shadowing"));
        }

        [Test]
        public void LightingRecipe_RejectsCascadeCountUnsupportedByUrp()
        {
            var recipe = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NoirContrast,
                ESRenderShadowMode.Realtime,
                1,
                60f,
                3,
                false,
                false);
            Assert.That(recipe.IsValid(out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("cascade-count-must-be-0-1-2-or-4"));
        }

        [Test]
        public void LightingRecipe_MobileFlatRejectsAdditionalLights()
        {
            var recipe = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.MobileFlat,
                ESRenderShadowMode.BakedOnly,
                1,
                30f,
                1,
                false,
                false);
            Assert.That(recipe.IsValid(out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("mobile-flat-disallows-expensive-lighting"));
        }

        [Test]
        public void BackendDiff_ReportsMissingLightingFactInsteadOfAssumingSuccess()
        {
            var snapshot = new ESRenderBackendSnapshot(0, "Balanced", "Universal Render Pipeline", true);
            var targetPolicy = ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced);
            var targetLighting = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Balanced);

            ESRenderBackendDiff diff = ESRenderBackendDiff.Evaluate(snapshot, targetPolicy, targetLighting);
            Assert.That(diff.Differences.HasFlag(ESRenderBackendDifference.LightingStateMissing), Is.True);
            Assert.That(diff.HasDifferences, Is.True);
        }

        [Test]
        public void ApplyReceipt_RequiresActualLightingSnapshotWhenPlanTargetsLighting()
        {
            var beforeLighting = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Performant);
            var before = new ESRenderBackendSnapshot(
                0, "Performant", "Universal Render Pipeline", true, 2,
                "Performant\u001FBalanced", beforeLighting);
            var targetPolicy = ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced);
            var targetLighting = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Balanced);
            ESRenderBackendChangePlan plan;
            Assert.That(ESRenderBackendChangePlan.TryCreateDryRun(
                before, targetPolicy, targetLighting, out plan, out string reason), Is.True, reason);

            var afterWithoutLighting = new ESRenderBackendSnapshot(
                1, "Balanced", "Universal Render Pipeline", true);
            ESRenderBackendReceipt receipt = ESRenderBackendReceipt.EvaluateApply(
                plan, afterWithoutLighting, true);
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.RollbackRequired));
            Assert.That(receipt.Reason, Is.EqualTo("post-apply-lighting-state-missing"));

            var afterWithLighting = new ESRenderBackendSnapshot(
                1, "Balanced", "Universal Render Pipeline", true, 2,
                "Performant\u001FBalanced", targetLighting);
            receipt = ESRenderBackendReceipt.EvaluateApply(plan, afterWithLighting, true);
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.Verified));
        }

        [Test]
        public void BackendDiff_UsesUnityQualityDisplayNameAndIsNullSafe()
        {
            var snapshot = new ESRenderBackendSnapshot(2, "High Fidelity", "Universal Render Pipeline", true);
            ESRenderBackendDiff diff = ESRenderBackendDiff.Evaluate(
                snapshot,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.HighFidelity));
            Assert.That(diff.HasDifferences, Is.False);

            diff = ESRenderBackendDiff.Evaluate(
                default(ESRenderBackendSnapshot),
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Performant));
            Assert.That(diff.HasDifferences, Is.True);
        }

        [Test]
        public void ApplyGate_RejectsQualityListFingerprintDrift()
        {
            var before = new ESRenderBackendSnapshot(
                0, "Performant", "Universal Render Pipeline", true, 3, "Performant\u001FBalanced\u001FHigh Fidelity");
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendApplyGate gate;
            Assert.That(ESRenderBackendApplyGate.TryAuthorize(
                plan, before, true, "render-quality-fingerprint", out gate, out reason), Is.True, reason);

            var drifted = new ESRenderBackendSnapshot(
                0, "Performant", "Universal Render Pipeline", true, 3, "Performant\u001FHigh Fidelity\u001FBalanced");
            Assert.That(ESRenderBackendApplyGate.TryAuthorize(
                plan, drifted, true, "render-quality-fingerprint", out gate, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("observed-backend-snapshot-drifted"));
        }

        [Test]
        public void ApplyGate_BindsAndRejectsMismatchedIdempotencyKey()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            Assert.That(
                ESRenderBackendChangePlan.TryCreateDryRun(
                    before,
                    ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                    out plan,
                    out reason), Is.True, reason);

            ESRenderBackendApplyGate gate;
            Assert.That(
                ESRenderBackendApplyGate.TryAuthorize(
                    plan, before, true, "render-quality-001", out gate, out reason), Is.True, reason);
            Assert.That(gate.MatchesIdempotencyKey("render-quality-001"), Is.True);
            Assert.That(gate.MatchesIdempotencyKey("render-quality-002"), Is.False);

            ESRenderBackendChangePlan otherPlan;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.HighFidelity),
                out otherPlan,
                out reason);
            Assert.That(gate.MatchesPlan(otherPlan), Is.False);
        }

        [Test]
        public void RollbackReceipt_RequiresExactBaselineSnapshot()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            Assert.That(
                ESRenderBackendChangePlan.TryCreateDryRun(
                    before,
                    ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                    out plan,
                    out reason), Is.True, reason);

            var drifted = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", false);
            ESRenderBackendReceipt receipt = ESRenderBackendReceipt.EvaluateRollback(plan, drifted, true);
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.RollbackRequired));

            receipt = ESRenderBackendReceipt.EvaluateRollback(plan, before, true);
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.RolledBack));
        }

        [Test]
        public void ApplySession_IsSingleUseAndCapturesAfterState()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendApplyGate gate;
            ESRenderBackendApplyGate.TryAuthorize(plan, before, true, "render-quality-session", out gate, out reason);
            ESRenderBackendApplySession session;
            Assert.That(
                ESRenderBackendApplySession.TryCreate(
                    plan, gate, "render-quality-session", out session, out reason), Is.True, reason);

            int writerCalls = 0;
            ESRenderBackendReceipt receipt = session.Execute(
                () => { writerCalls++; return true; },
                () => new ESRenderBackendSnapshot(1, "Balanced", "Universal Render Pipeline", true));
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.Verified));
            Assert.That(writerCalls, Is.EqualTo(1));

            receipt = session.Execute(() => { writerCalls++; return true; }, () => before);
            Assert.That(receipt.Status, Is.EqualTo(ESRenderBackendReceiptStatus.Failed));
            Assert.That(receipt.Reason, Is.EqualTo("apply-session-already-consumed"));
            Assert.That(writerCalls, Is.EqualTo(1));
        }

        [Test]
        public void EvidenceReceipt_BindsOperationStatusAndNeverClaimsRuntimeAcceptance()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendReceipt receipt = ESRenderBackendReceipt.EvaluateApply(
                plan,
                new ESRenderBackendSnapshot(1, "Balanced", "Universal Render Pipeline", true),
                true);
            ESRenderBackendEvidenceReceipt evidence = ESRenderBackendEvidenceReceipt.Create(
                plan, receipt, "render-quality-evidence-001");

            Assert.That(evidence.operation, Is.EqualTo("apply"));
            Assert.That(evidence.receiptStatus, Is.EqualTo("Verified"));
            Assert.That(evidence.backendStateVerified, Is.True);
            Assert.That(evidence.runtimeAcceptance, Is.False);
        }

        [Test]
        public void EvidenceReceiptStore_RoundTripsAndRejectsRuntimeClaim()
        {
            var evidence = new ESRenderBackendEvidenceReceipt
            {
                operation = "apply",
                idempotencyKey = "render-quality-json-001",
                planStatus = "DryRunReady",
                receiptStatus = "Verified",
                reason = "post-apply-snapshot-matches-target-quality"
            };
            Assert.That(ESRenderBackendEvidenceReceiptStore.TrySerialize(
                evidence, out string json, out string reason), Is.True, reason);
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryDeserialize(
                json, out ESRenderBackendEvidenceReceipt restored, out reason), Is.True, reason);
            Assert.That(restored.idempotencyKey, Is.EqualTo(evidence.idempotencyKey));
            Assert.That(restored.runtimeAcceptance, Is.False);

            evidence.runtimeAcceptance = true;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TrySerialize(
                evidence, out json, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("runtime-acceptance-cannot-be-serialized-in-static-receipt"));

            evidence.runtimeAcceptance = false;
            evidence.receiptStatus = "999";
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryDeserialize(
                UnityEngine.JsonUtility.ToJson(evidence), out restored, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("receipt-status-unsupported"));
        }

        [Test]
        public void EvidencePathPolicy_AllowsOnlyProjectRenderingEvidenceJson()
        {
            string root = "C:/ESProject";
            Assert.That(ESRenderEvidencePathPolicy.TryValidate(
                root,
                "C:/ESProject/ES/Output/RenderingEvidence/apply.json",
                out string normalized,
                out string reason), Is.True, reason);
            Assert.That(normalized.Replace('\\', '/'), Does.EndWith("/ES/Output/RenderingEvidence/apply.json"));

            Assert.That(ESRenderEvidencePathPolicy.TryValidate(
                root,
                "C:/ESProject/ES/Output/RenderingEvidence/../.. /escape.json".Replace(" ", string.Empty),
                out normalized,
                out reason), Is.False);
            Assert.That(reason, Is.EqualTo("evidence-path-outside-project-allowlist"));

            Assert.That(ESRenderEvidencePathPolicy.TryValidate(
                root,
                "C:/ESProject/ES/Output/RenderingEvidence/apply.txt",
                out normalized,
                out reason), Is.False);
            Assert.That(reason, Is.EqualTo("evidence-path-must-be-json"));
        }

        [Test]
        public void ResourceSnapshot_DoesNotTreatMissingRendererIdentityAsPipelineAbsence()
        {
            var snapshot = new ESRenderBackendResourceSnapshot
            {
                pipelineAssetName = "URP-Balanced",
                rendererDataName = string.Empty
            };
            Assert.That(snapshot.IsPipelinePresent, Is.True);
            Assert.That(snapshot.IsRendererIdentityPresent, Is.False);
        }

        [Test]
        public void EvidenceReceiptStore_BindsRendererIdentityWhenProvided()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(
                plan,
                new ESRenderBackendSnapshot(1, "Balanced", "Universal Render Pipeline", true),
                true);
            var resources = new ESRenderBackendResourceSnapshot
            {
                pipelineAssetName = "URP-Balanced",
                rendererDataTypeName = "UniversalRendererData",
                rendererDataName = "URP-Balanced-Renderer"
            };
            ESRenderBackendEvidenceReceipt evidence;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithResourceSnapshot(
                plan, backendReceipt, "render-resource-evidence-001", resources,
                out evidence, out reason), Is.True, reason);
            Assert.That(evidence.rendererDataName, Is.EqualTo(resources.rendererDataName));
            Assert.That(evidence.pipelineAssetName, Is.EqualTo(resources.pipelineAssetName));
        }

        [Test]
        public void EvidenceReceiptStore_BindsVolumeInventoryWhenProvided()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(
                plan,
                new ESRenderBackendSnapshot(1, "Balanced", "Universal Render Pipeline", true),
                true);
            var resources = new ESRenderBackendResourceSnapshot { pipelineAssetName = "URP-Balanced" };
            var volumes = new ESRenderVolumeResourceSnapshot
            {
                profileAssetCount = 3,
                profileGuidFingerprint = "guid-a\u001Fguid-b\u001Fguid-c",
                profileNameFingerprint = "Combat\u001FDefault\u001FShowcase"
            };
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithResourceAndVolumeSnapshot(
                plan, backendReceipt, "render-volume-evidence-001", resources, volumes,
                out ESRenderBackendEvidenceReceipt evidence, out reason), Is.True, reason);
            Assert.That(evidence.volumeProfileAssetCount, Is.EqualTo(3));
            Assert.That(evidence.volumeProfileGuidFingerprint, Is.EqualTo(volumes.profileGuidFingerprint));
        }

        [Test]
        public void ShaderResourceSnapshot_SeparatesKeywordInventoryFromVariantAcceptance()
        {
            var snapshot = new ESRenderShaderResourceSnapshot
            {
                shaderAssetCount = 4,
                keywordSpaceShaderCount = 3,
                shaderGuidFingerprint = "guid-a\u001Fguid-b",
                keywordFingerprint = "guid-a=FOG_LINEAR,QUALITY_BALANCED"
            };
            Assert.That(snapshot.shaderAssetCount, Is.EqualTo(4));
            Assert.That(snapshot.keywordSpaceShaderCount, Is.EqualTo(3));
            Assert.That(snapshot.keywordFingerprint, Does.Contain("QUALITY_BALANCED"));
        }

        [Test]
        public void EvidenceReceiptStore_BindsAllRenderResourceInventories()
        {
            var before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(
                before,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out plan,
                out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(
                plan,
                new ESRenderBackendSnapshot(1, "Balanced", "Universal Render Pipeline", true),
                true);
            var resources = new ESRenderBackendResourceSnapshot { pipelineAssetName = "URP-Balanced" };
            var volumes = new ESRenderVolumeResourceSnapshot { profileAssetCount = 1 };
            var shaders = new ESRenderShaderResourceSnapshot
            {
                shaderAssetCount = 2,
                keywordSpaceShaderCount = 1,
                keywordFingerprint = "guid-a=QUALITY_BALANCED"
            };
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithAllResourceSnapshots(
                plan, backendReceipt, "render-all-resources-001", resources, volumes, shaders,
                out ESRenderBackendEvidenceReceipt evidence, out reason), Is.True, reason);
            Assert.That(evidence.shaderAssetCount, Is.EqualTo(2));
            Assert.That(evidence.keywordFingerprint, Does.Contain("QUALITY_BALANCED"));
        }

        [Test]
        public void EvidenceReceipt_BindsUrpCompatibilityIdentity()
        {
            ESRenderBackendSnapshot before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(before, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Performant), out plan, out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(
                plan, new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true), true);
            var resources = new ESRenderBackendResourceSnapshot
            {
                pipelineAssetName = "URP-Performant",
                compatibilityStatus = "CurrentBaseline",
                unityVersion = "2022.3.45f1",
                urpPackageVersion = "14.0.11"
            };
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithResourceSnapshot(
                plan, backendReceipt, "compat-identity-001", resources,
                out ESRenderBackendEvidenceReceipt evidence, out reason), Is.True, reason);
            Assert.That(evidence.compatibilityStatus, Is.EqualTo("CurrentBaseline"));
            Assert.That(evidence.urpPackageVersion, Is.EqualTo("14.0.11"));
        }

        [Test]
        public void VolumeResourceSnapshot_SeparatesInventoryFromRuntimeClaims()
        {
            var snapshot = new ESRenderVolumeResourceSnapshot
            {
                profileAssetCount = 2,
                profileGuidFingerprint = "guid-a\u001Fguid-b",
                profileNameFingerprint = "Combat\u001FShowcase"
            };
            Assert.That(snapshot.profileAssetCount, Is.EqualTo(2));
            Assert.That(snapshot.profileNameFingerprint, Does.Contain("Combat"));
        }

        [Test]
        public void ResourceTypeFingerprints_AreStableAndExplicit()
        {
            var backend = new ESRenderBackendResourceSnapshot
            {
                rendererFeatureCount = 2,
                rendererFeatureTypeFingerprint = "FeatureA\u001FFeatureB"
            };
            var volume = new ESRenderVolumeResourceSnapshot
            {
                componentCount = 2,
                componentTypeFingerprint = "Bloom\u001FColorAdjustments"
            };
            Assert.That(backend.rendererFeatureCount, Is.EqualTo(2));
            Assert.That(backend.rendererFeatureTypeFingerprint, Is.EqualTo("FeatureA\u001FFeatureB"));
            Assert.That(volume.componentTypeFingerprint, Does.Contain("Bloom"));
        }

        [Test]
        public void ShaderVariantCompileLogParser_IsBoundedAndStaticOnly()
        {
            ESShaderVariantCompileLogSummary summary;
            string reason;
            Assert.That(ESShaderVariantCompileLogParser.TryParse(
                "Compiled shader variant: Lit\nkeyword _FOG_LINEAR\nwarning: stripped\ninfo", out summary, out reason), Is.True, reason);
            Assert.That(summary.VariantRecordCount, Is.EqualTo(1));
            Assert.That(summary.KeywordRecordCount, Is.EqualTo(1));
            Assert.That(summary.WarningCount, Is.EqualTo(1));
            Assert.That(summary.RuntimeAcceptance, Is.False);
        }

        [Test]
        public void ShaderVariantCompileLogParser_RejectsOversizedInput()
        {
            ESShaderVariantCompileLogSummary summary;
            string reason;
            Assert.That(ESShaderVariantCompileLogParser.TryParse(
                new string('x', ESShaderVariantCompileLogParser.MaxCharacters + 1), out summary, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("log-too-large"));
        }

        [Test]
        public void RenderBudgetEvaluator_ReportsDeterministicOverrun()
        {
            var policy = ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced);
            var result = ESRenderBudgetEvaluator.Evaluate(
                policy, new ESRenderBudgetSnapshot(49, 1, 1, 10f));
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Reason, Is.EqualTo("transparent-budget-exceeded"));
        }

        [Test]
        public void RenderBudgetEvaluator_AcceptsWithinPolicy()
        {
            var policy = ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.CombatReadability);
            var result = ESRenderBudgetEvaluator.Evaluate(
                policy, new ESRenderBudgetSnapshot(32, 96, 16, 16.6f));
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void RenderMetricSnapshot_RequiresBoundedMeasurementIdentity()
        {
            var invalid = new ESRenderMetricSnapshot("", "combat", 1, 1, 1, 1f, 1f, 0, 0L, false);
            string reason;
            Assert.That(invalid.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("platform-required"));

            var valid = new ESRenderMetricSnapshot("Android-GPU-A", "combat-baseline", 120, 40, 12, 8f, 7f, 0, 1024, true);
            Assert.That(valid.IsValid(out reason), Is.True, reason);
            Assert.That(valid.RuntimeCaptured, Is.True);
        }

        [Test]
        public void UrpCompatibilityPolicy_RejectsOtherPipelinesAndMarksUnity6Unverified()
        {
            string reason;
            Assert.That(ESUrpCompatibilityPolicy.Evaluate("Built-in", 2022, "14.0.11", out reason),
                Is.EqualTo(ESUrpCompatibilityStatus.Rejected));
            Assert.That(reason, Is.EqualTo("pipeline-not-supported"));
            Assert.That(ESUrpCompatibilityPolicy.Evaluate("URP", 2022, "14.0.11", out reason),
                Is.EqualTo(ESUrpCompatibilityStatus.CurrentBaseline));
            Assert.That(ESUrpCompatibilityPolicy.Evaluate("URP", 2023, "15.0.0", out reason),
                Is.EqualTo(ESUrpCompatibilityStatus.ForwardCandidateUnverified));
            Assert.That(reason, Is.EqualTo("urp-forward-candidate-requires-runtime-verification"));
            Assert.That(ESUrpCompatibilityPolicy.Evaluate("URP", 6, "17.0.0", out reason),
                Is.EqualTo(ESUrpCompatibilityStatus.ForwardCandidateUnverified));
            Assert.That(reason, Is.EqualTo("unity6-forward-candidate-requires-runtime-verification"));
        }

        [Test]
        public void UrpFeatureBudgetEvaluator_UsesExplicitFeatureBudget()
        {
            string reason;
            var policy = ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced);
            Assert.That(ESUrpFeatureBudgetEvaluator.TryEvaluate(
                policy, new ESUrpFeatureSnapshot(3, 1, 4, true), 2, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("renderer-feature-budget-exceeded"));
            Assert.That(ESUrpFeatureBudgetEvaluator.TryEvaluate(
                policy, new ESUrpFeatureSnapshot(2, 1, 4, true), 2, out reason), Is.True, reason);
        }

        [Test]
        public void EvidenceReceipt_BindsMetricSnapshotWithoutChangingRuntimeClaim()
        {
            ESRenderBackendSnapshot before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(before, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Performant), out plan, out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(plan, before, true);
            var resources = new ESRenderBackendResourceSnapshot { pipelineAssetName = "URP-Performant" };
            var volumes = new ESRenderVolumeResourceSnapshot();
            var shaders = new ESRenderShaderResourceSnapshot();
            var metrics = new ESRenderMetricSnapshot("Desktop-GPU", "baseline", 60, 20, 8, 5f, 4f, 0, 2048, true);
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithAllResourceAndMetricsSnapshots(
                plan, backendReceipt, "metric-bind-001", resources, volumes, shaders, metrics,
                out ESRenderBackendEvidenceReceipt evidence, out reason), Is.True, reason);
            Assert.That(evidence.drawCalls, Is.EqualTo(20));
            Assert.That(evidence.runtimeAcceptance, Is.False);
            Assert.That(evidence.runtimeCaptured, Is.True);
        }

        [Test]
        public void RenderEvidenceBatch_RejectsDuplicateIdempotencyKeysAndRoundTrips()
        {
            ESRenderBackendSnapshot before = new ESRenderBackendSnapshot(0, "Performant", "Universal Render Pipeline", true);
            ESRenderBackendChangePlan plan;
            string reason;
            ESRenderBackendChangePlan.TryCreateDryRun(before, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Performant), out plan, out reason);
            ESRenderBackendReceipt backendReceipt = ESRenderBackendReceipt.EvaluateApply(plan, before, true);
            var resources = new ESRenderBackendResourceSnapshot { pipelineAssetName = "URP-Performant" };
            ESRenderBackendEvidenceReceipt evidence;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryCreateWithResourceSnapshot(
                plan, backendReceipt, "batch-key-001", resources, out evidence, out reason), Is.True, reason);
            ESRenderEvidenceBatch batch;
            Assert.That(ESRenderEvidenceBatch.TryCreate("batch-001", new[] { evidence }, out batch, out reason), Is.True, reason);
            string json;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TrySerializeBatch(batch, out json, out reason), Is.True, reason);
            ESRenderEvidenceBatch roundTrip;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TryDeserializeBatch(json, out roundTrip, out reason), Is.True, reason);
            Assert.That(roundTrip.receipts.Length, Is.EqualTo(1));
            Assert.That(ESRenderEvidenceBatch.TryCreate("batch-dup", new[] { evidence, evidence }, out batch, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("batch-idempotency-key-duplicate"));
        }

        [Test]
        public void RenderEvidenceBatch_RejectsRuntimeAcceptanceClaim()
        {
            var receipt = new ESRenderBackendEvidenceReceipt
            {
                idempotencyKey = "runtime-claim", runtimeAcceptance = true
            };
            ESRenderEvidenceBatch batch;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("runtime-batch", new[] { receipt }, out batch, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("batch-runtime-acceptance-forbidden"));
        }

        [Test]
        public void RenderEvidenceBatchBudgetAudit_DistinguishesUnmeasuredAndOverrun()
        {
            var measured = new ESRenderBackendEvidenceReceipt
            {
                idempotencyKey = "budget-measured", runtimeCaptured = true, metricSampleCount = 60,
                cpuMilliseconds = 20f, gpuMilliseconds = 1f
            };
            var unmeasured = new ESRenderBackendEvidenceReceipt { idempotencyKey = "budget-missing" };
            ESRenderEvidenceBatch batch;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("budget-batch", new[] { measured, unmeasured }, out batch, out reason), Is.True, reason);
            var audit = ESRenderEvidenceBatchBudgetAudit.Evaluate(batch, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced));
            Assert.That(audit.EvaluatedCount, Is.EqualTo(1));
            Assert.That(audit.UnmeasuredCount, Is.EqualTo(1));
            Assert.That(audit.OverrunCount, Is.EqualTo(1));
        }

        [Test]
        public void RenderEvidenceBatchDecision_PreservesDriftAndUnproven()
        {
            var baselineReceipt = new ESRenderBackendEvidenceReceipt { idempotencyKey = "decision-a" };
            var candidateReceipt = new ESRenderBackendEvidenceReceipt { idempotencyKey = "decision-b" };
            ESRenderEvidenceBatch baseline, candidate;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("decision-base", new[] { baselineReceipt }, out baseline, out reason), Is.True, reason);
            Assert.That(ESRenderEvidenceBatch.TryCreate("decision-candidate", new[] { candidateReceipt }, out candidate, out reason), Is.True, reason);
            var decision = ESRenderEvidenceBatchDecision.Evaluate(
                baseline, candidate, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced));
            Assert.That(decision.Status, Is.EqualTo(ESRenderEvidenceBatchDecisionStatus.DriftedAndUnproven));
            Assert.That(decision.Diff.HasChanges, Is.True);
            Assert.That(decision.BudgetAudit.UnmeasuredCount, Is.EqualTo(1));
        }

        [Test]
        public void RenderEvidenceScenarioSummary_GroupsDeterministically()
        {
            var a = new ESRenderBackendEvidenceReceipt { idempotencyKey = "scenario-a", qualityProfile = "Balanced", metricPlatform = "desktop", metricScenario = "combat", runtimeCaptured = true, metricSampleCount = 10, cpuMilliseconds = 10f };
            var b = new ESRenderBackendEvidenceReceipt { idempotencyKey = "scenario-b", qualityProfile = "Balanced", metricPlatform = "desktop", metricScenario = "combat" };
            ESRenderEvidenceBatch batch;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("scenario-batch", new[] { a, b }, out batch, out reason), Is.True, reason);
            var summaries = ESRenderEvidenceScenarioSummary.Build(batch, ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced));
            Assert.That(summaries.Length, Is.EqualTo(1));
            Assert.That(summaries[0].Scenario, Is.EqualTo("combat"));
            Assert.That(summaries[0].Platform, Is.EqualTo("desktop"));
            Assert.That(summaries[0].QualityProfile, Is.EqualTo("Balanced"));
            Assert.That(summaries[0].ReceiptCount, Is.EqualTo(2));
            Assert.That(summaries[0].MeasuredCount, Is.EqualTo(1));
            Assert.That(summaries[0].UnmeasuredCount, Is.EqualTo(1));
        }

        [Test]
        public void RenderEvidenceBatchDiff_ReportsAddedRemovedAndChanged()
        {
            var first = new ESRenderBackendEvidenceReceipt
            {
                idempotencyKey = "diff-a", receiptStatus = "Verified", compatibilityStatus = "CurrentBaseline", drawCalls = 10, setPassCalls = 2
            };
            var changed = new ESRenderBackendEvidenceReceipt
            {
                idempotencyKey = "diff-a", receiptStatus = "Verified", compatibilityStatus = "CurrentBaseline", pipelineAssetName = "URP-Balanced", drawCalls = 12, setPassCalls = 2
            };
            var added = new ESRenderBackendEvidenceReceipt { idempotencyKey = "diff-c", receiptStatus = "Verified" };
            ESRenderEvidenceBatch baseline, candidate;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("base", new[] { first }, out baseline, out reason), Is.True, reason);
            Assert.That(ESRenderEvidenceBatch.TryCreate("candidate", new[] { changed, added }, out candidate, out reason), Is.True, reason);
            var diff = ESRenderEvidenceBatchDiff.Compare(baseline, candidate);
            Assert.That(diff.AddedCount, Is.EqualTo(1));
            Assert.That(diff.RemovedCount, Is.EqualTo(0));
            Assert.That(diff.ChangedCount, Is.EqualTo(1));
            Assert.That(diff.HasChanges, Is.True);
            Assert.That(diff.IsIdentical, Is.False);
            Assert.That(diff.ChangedIdempotencyKeys[0], Is.EqualTo("diff-a"));
        }

        [Test]
        public void RenderMetricCaptureSession_RequiresExactSamplesAndAggregatesPeakValues()
        {
            ESRenderMetricCaptureSession session;
            string reason;
            Assert.That(ESRenderMetricCaptureSession.TryCreate(
                new ESRenderMetricSamplingRequest("Desktop-GPU", "combat", 2),
                out session, out reason), Is.True, reason);
            Assert.That(session.TryComplete(out ESRenderMetricSnapshot incomplete, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("capture-session-requires-exact-sample-count"));
            Assert.That(session.TryAddSample(10, 2, 4f, 3f, 100, 1024, out reason), Is.True, reason);
            Assert.That(session.TryAddSample(20, 4, 8f, 7f, 50, 2048, out reason), Is.True, reason);
            ESRenderMetricSnapshot snapshot;
            Assert.That(session.TryComplete(out snapshot, out reason), Is.True, reason);
            Assert.That(snapshot.DrawCalls, Is.EqualTo(20));
            Assert.That(snapshot.CpuMilliseconds, Is.EqualTo(8f));
            Assert.That(snapshot.GcAllocBytes, Is.EqualTo(100));
            Assert.That(snapshot.RuntimeCaptured, Is.True);
            Assert.That(session.TryAddSample(1, 1, 1f, 1f, 0, 0L, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("capture-session-already-completed"));
        }

        [Test]
        public void RenderMetricCaptureSession_RejectsNonFiniteTiming()
        {
            ESRenderMetricCaptureSession session;
            string reason;
            Assert.That(ESRenderMetricCaptureSession.TryCreate(
                new ESRenderMetricSamplingRequest("Desktop-GPU", "combat", 1),
                out session, out reason), Is.True, reason);
            Assert.That(session.TryAddSample(1, 1, float.NaN, 1f, 0, 0L, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("metric-time-must-be-finite"));
        }

        [Test]
        public void RenderQualitySamplingQueue_EnumeratesProfilesWithoutDuplicates()
        {
            ESRenderQualitySamplingQueue queue;
            string reason;
            Assert.That(ESRenderQualitySamplingQueue.TryCreate(
                new[] { ESRenderQualityProfileId.Performant, ESRenderQualityProfileId.Balanced },
                out queue, out reason), Is.True, reason);
            ESRenderQualityProfileId profile;
            Assert.That(queue.TryBeginNext(out profile, out reason), Is.True, reason);
            Assert.That(profile, Is.EqualTo(ESRenderQualityProfileId.Performant));
            Assert.That(queue.TryCompleteCurrent(out reason), Is.True, reason);
            Assert.That(queue.TryBeginNext(out profile, out reason), Is.True, reason);
            Assert.That(profile, Is.EqualTo(ESRenderQualityProfileId.Balanced));
            Assert.That(queue.TryCompleteCurrent(out reason), Is.True, reason);
            Assert.That(queue.Status, Is.EqualTo(ESRenderQualitySamplingQueueStatus.Completed));
            Assert.That(queue.TryBeginNext(out profile, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("sampling-queue-completed"));
        }

        [Test]
        public void RenderQualitySamplingQueue_RejectsDuplicateProfiles()
        {
            ESRenderQualitySamplingQueue queue;
            string reason;
            Assert.That(ESRenderQualitySamplingQueue.TryCreate(
                new[] { ESRenderQualityProfileId.Balanced, ESRenderQualityProfileId.Balanced },
                out queue, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("sampling-queue-profile-duplicate"));
        }

        [Test]
        public void RenderEvidenceReport_AggregatesDecisionAndScenarioSummary()
        {
            var receipt = new ESRenderBackendEvidenceReceipt
            {
                idempotencyKey = "report-001", qualityProfile = "Balanced", receiptStatus = "Verified",
                metricPlatform = "Editor", metricScenario = "combat", runtimeCaptured = true,
                metricSampleCount = 60, cpuMilliseconds = 8f, gpuMilliseconds = 7f
            };
            ESRenderEvidenceBatch batch;
            string reason;
            Assert.That(ESRenderEvidenceBatch.TryCreate("report-batch", new[] { receipt }, out batch, out reason), Is.True, reason);
            ESRenderEvidenceReport report;
            Assert.That(ESRenderEvidenceReport.TryCreate(
                "report-001", batch, batch,
                ESRenderQualityPolicy.Resolve(ESRenderQualityProfileId.Balanced),
                out report, out reason), Is.True, reason);
            Assert.That(report.decisionStatus, Is.EqualTo("Stable"));
            Assert.That(report.evaluatedCount, Is.EqualTo(1));
            Assert.That(report.scenarioSummaries.Length, Is.EqualTo(1));
        }

        [Test]
        public void RenderEvidenceAggregateReport_PreservesWorstOverallState()
        {
            var stable = new ESRenderEvidenceReport
            {
                schemaVersion = ESRenderEvidenceReport.CurrentSchemaVersion,
                reportId = "stable", decisionStatus = "Stable", evaluatedCount = 1
            };
            var unproven = new ESRenderEvidenceReport
            {
                schemaVersion = ESRenderEvidenceReport.CurrentSchemaVersion,
                reportId = "unproven", decisionStatus = "Unproven", unmeasuredCount = 1
            };
            ESRenderEvidenceAggregateReport aggregate;
            string reason;
            Assert.That(ESRenderEvidenceAggregateReport.TryCreate(
                "aggregate-001", new[] { stable, unproven }, out aggregate, out reason), Is.True, reason);
            Assert.That(aggregate.overallStatus, Is.EqualTo("Unproven"));
            Assert.That(aggregate.reportCount, Is.EqualTo(2));
            Assert.That(aggregate.unmeasuredCount, Is.EqualTo(1));
            string json;
            Assert.That(ESRenderBackendEvidenceReceiptStore.TrySerializeAggregateReport(
                aggregate, out json, out reason), Is.True, reason);
            Assert.That(json, Does.Contain("aggregate-001"));
        }

        [Test]
        public void RenderEvidenceAggregateReport_RejectsEmptyOrSchemaDriftedInput()
        {
            ESRenderEvidenceAggregateReport aggregate;
            string reason;
            Assert.That(ESRenderEvidenceAggregateReport.TryCreate(
                "aggregate-empty", new ESRenderEvidenceReport[0], out aggregate, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("aggregate-reports-required"));

            var drifted = new ESRenderEvidenceReport
            {
                schemaVersion = ESRenderEvidenceReport.CurrentSchemaVersion + 1,
                reportId = "drifted"
            };
            Assert.That(ESRenderEvidenceAggregateReport.TryCreate(
                "aggregate-drifted", new[] { drifted }, out aggregate, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("aggregate-report-invalid"));
        }

        [Test]
        public void RenderEvidenceAggregateReport_AllowsCrossPlatformScenarioReportsWithoutFlattening()
        {
            var desktop = new ESRenderEvidenceReport
            {
                schemaVersion = ESRenderEvidenceReport.CurrentSchemaVersion,
                reportId = "desktop-combat",
                decisionStatus = "Stable",
                evaluatedCount = 1
            };
            var mobile = new ESRenderEvidenceReport
            {
                schemaVersion = ESRenderEvidenceReport.CurrentSchemaVersion,
                reportId = "mobile-menu",
                decisionStatus = "Stable",
                evaluatedCount = 1
            };
            ESRenderEvidenceAggregateReport aggregate;
            string reason;
            Assert.That(ESRenderEvidenceAggregateReport.TryCreate(
                "aggregate-platforms", new[] { desktop, mobile }, out aggregate, out reason), Is.True, reason);
            Assert.That(aggregate.reportCount, Is.EqualTo(2));
            Assert.That(aggregate.reports[0].reportId, Is.EqualTo("desktop-combat"));
            Assert.That(aggregate.reports[1].reportId, Is.EqualTo("mobile-menu"));
        }

        [Test]
        public void RenderStylePreset_ResolvesReusableNonMonoBehaviourTemplates()
        {
            foreach (ESRenderVisualStyleId style in Enum.GetValues(typeof(ESRenderVisualStyleId)))
            {
                ESRenderStylePreset preset = ESRenderStylePreset.Resolve(style);
                string reason;
                Assert.That(preset.IsValid(out reason), Is.True, reason);
                Assert.That(preset.PreserveSilhouette, Is.True);
            }

            ESRenderStylePreset neon = ESRenderStylePreset.Resolve(ESRenderVisualStyleId.NeonSciFi);
            Assert.That(neon.QualityProfile, Is.EqualTo(ESRenderQualityProfileId.HighFidelity));
            Assert.That(neon.BloomIntensity, Is.GreaterThan(0f));
        }

        [Test]
        public void RenderStylePreset_ClampsInvalidNumericInputsDeterministically()
        {
            var preset = new ESRenderStylePreset(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderQualityProfileId.Balanced,
                float.NaN,
                float.PositiveInfinity,
                -99f,
                99f,
                -1f,
                true);
            Assert.That(preset.Saturation, Is.EqualTo(1f));
            Assert.That(preset.Contrast, Is.EqualTo(1f));
            Assert.That(preset.Exposure, Is.EqualTo(-2f));
            Assert.That(preset.BloomIntensity, Is.EqualTo(2f));
            Assert.That(preset.ShadowSoftness, Is.EqualTo(0f));
        }

        [Test]
        public void RenderStyleCatalog_IsDeterministicAndValidatesAllTemplates()
        {
            string reason;
            Assert.That(ESRenderStyleCatalog.Validate(out reason), Is.True, reason);
            Assert.That(ESRenderStyleCatalog.Count, Is.EqualTo(10));
            Assert.That(ESRenderStyleCatalog.GetStyleIdAt(0), Is.EqualTo(ESRenderVisualStyleId.NaturalPbr));
            Assert.That(ESRenderStyleCatalog.GetStyleIdAt(9), Is.EqualTo(ESRenderVisualStyleId.TacticalRealism));
        }

        [Test]
        public void RenderStyleCatalog_SelectsStableFirstMatchForQuality()
        {
            ESRenderStylePreset preset;
            Assert.That(ESRenderStyleCatalog.TryGetFirstForQuality(
                ESRenderQualityProfileId.HighFidelity, out preset), Is.True);
            Assert.That(preset.Style, Is.EqualTo(ESRenderVisualStyleId.NoirContrast));

            Assert.That(ESRenderStyleCatalog.TryGetFirstForQuality(
                (ESRenderQualityProfileId)999, out preset), Is.False);
        }

        [Test]
        public void RenderSceneIntent_ResolvesStyleAndPreservesIntentSpecificBudgets()
        {
            ESRenderSceneIntent combat = ESRenderSceneIntent.Resolve(ESRenderSceneIntentId.Combat);
            ESRenderStylePreset preset;
            bool usedFallback;
            Assert.That(combat.TryResolvePreset(out preset, out usedFallback), Is.True);
            Assert.That(usedFallback, Is.False);
            Assert.That(preset.Style, Is.EqualTo(ESRenderVisualStyleId.StylizedToon));
            Assert.That(combat.PreserveReadability, Is.True);
            Assert.That(combat.TransparencyBudgetScale, Is.LessThan(1f));

            ESRenderSceneIntent cinematic = ESRenderSceneIntent.Resolve(ESRenderSceneIntentId.Cinematic);
            Assert.That(cinematic.AllowPostProcessing, Is.True);
            Assert.That(cinematic.TransparencyBudgetScale, Is.GreaterThan(1f));
        }

        [Test]
        public void RenderPlatformProfile_ClampsQualityWithoutRuntimeDependencies()
        {
            ESRenderPlatformProfile mobile = ESRenderPlatformProfile.Resolve(ESRenderPlatformId.Mobile);
            Assert.That(mobile.Allows(ESRenderQualityProfileId.MobileStable), Is.True);
            Assert.That(mobile.Allows(ESRenderQualityProfileId.HighFidelity), Is.False);
            Assert.That(mobile.ClampQuality(ESRenderQualityProfileId.HighFidelity), Is.EqualTo(ESRenderQualityProfileId.MobileStable));
            Assert.That(mobile.DynamicResolutionAllowed, Is.True);

            ESRenderPlatformProfile web = ESRenderPlatformProfile.Resolve(ESRenderPlatformId.WebGl);
            Assert.That(web.ClampQuality(ESRenderQualityProfileId.CinematicShowcase), Is.EqualTo(ESRenderQualityProfileId.Balanced));
            Assert.That(web.DynamicResolutionAllowed, Is.False);
        }

        [Test]
        public void RenderFeatureRecipe_ProvidesValidComposableDefaults()
        {
            foreach (ESRenderVisualStyleId style in Enum.GetValues(typeof(ESRenderVisualStyleId)))
            {
                ESRenderFeatureRecipe recipe = ESRenderFeatureRecipe.Resolve(style);
                string reason;
                Assert.That(recipe.IsValid(out reason), Is.True, reason);
                Assert.That(recipe.FeatureBudget, Is.GreaterThanOrEqualTo(0));
            }

            ESRenderFeatureRecipe mobile = ESRenderFeatureRecipe.Resolve(ESRenderVisualStyleId.MobileFlat);
            Assert.That(mobile.AllowVolumetrics, Is.False);
            Assert.That(mobile.EnableDecals, Is.False);
            Assert.That(mobile.ShadowCascades, Is.EqualTo(0));
        }

        [Test]
        public void RenderConfigurationResolver_MergesSceneStyleAndPlatformDeterministically()
        {
            ESRenderSceneIntent cinematic = ESRenderSceneIntent.Resolve(ESRenderSceneIntentId.Cinematic);
            ESRenderPlatformProfile mobile = ESRenderPlatformProfile.Resolve(ESRenderPlatformId.Mobile);
            ESRenderResolvedConfiguration configuration;
            string reason;
            Assert.That(ESRenderConfigurationResolver.TryResolve(cinematic, mobile, out configuration, out reason), Is.True, reason);
            Assert.That(configuration.Platform, Is.EqualTo(ESRenderPlatformId.Mobile));
            Assert.That(configuration.QualityProfile, Is.EqualTo(ESRenderQualityProfileId.MobileStable));
            Assert.That(configuration.QualityDowngraded, Is.True);
            Assert.That(configuration.StyleFallback, Is.True);
            Assert.That(configuration.VolumetricsEnabled, Is.False);
            Assert.That(configuration.FeatureBudget, Is.LessThan(configuration.FeatureRecipe.FeatureBudget));
            Assert.That(configuration.MaterialRecipe.Style, Is.EqualTo(ESRenderVisualStyleId.MobileFlat));
            Assert.That(configuration.LightingRecipe.ShadowMode, Is.EqualTo(ESRenderShadowMode.BakedOnly));
            Assert.That(configuration.EffectsRecipe.Bloom, Is.False);
        }

        [Test]
        public void RenderMaterialRecipe_CoversSurfaceAndStyleConstraints()
        {
            string reason;
            foreach (ESRenderVisualStyleId style in Enum.GetValues(typeof(ESRenderVisualStyleId)))
            {
                ESRenderMaterialRecipe recipe = ESRenderMaterialRecipe.Resolve(style);
                Assert.That(recipe.IsValid(out reason), Is.True, reason);
            }

            ESRenderMaterialRecipe toon = ESRenderMaterialRecipe.Resolve(ESRenderVisualStyleId.StylizedToon);
            Assert.That(toon.OutlineWidth, Is.GreaterThan(0f));
            Assert.That(toon.Surface, Is.EqualTo(ESRenderSurfaceModel.Opaque));

            var additive = new ESRenderMaterialRecipe(
                ESRenderVisualStyleId.NeonSciFi,
                ESRenderSurfaceModel.Additive,
                1, 0f, 0.5f, 1f, 0f, true);
            Assert.That(additive.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("additive-material-cannot-receive-shadows"));
        }

        [Test]
        public void RenderLightingRecipe_CoversShadowAndLightBudgets()
        {
            string reason;
            foreach (ESRenderVisualStyleId style in Enum.GetValues(typeof(ESRenderVisualStyleId)))
            {
                ESRenderLightingRecipe recipe = ESRenderLightingRecipe.Resolve(style);
                Assert.That(recipe.IsValid(out reason), Is.True, reason);
            }

            ESRenderLightingRecipe mobile = ESRenderLightingRecipe.Resolve(ESRenderVisualStyleId.MobileFlat);
            Assert.That(mobile.SoftShadows, Is.False);
            Assert.That(mobile.ReflectionProbes, Is.False);
            Assert.That(mobile.ShadowMode, Is.EqualTo(ESRenderShadowMode.BakedOnly));
            Assert.That(mobile.MainLightShadowsEnabled, Is.False);
            Assert.That(mobile.EstimatedShadowPassBudget, Is.EqualTo(0));

            var invalid = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.Disabled,
                0, 10f, 1, false, false);
            Assert.That(invalid.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("disabled-shadows-cannot-have-budget"));

            var disabled = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.Disabled,
                0, 0f, 0, false, false);
            Assert.That(disabled.IsValid(out reason), Is.True, reason);

            var softOnBaked = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.RetroPixel,
                ESRenderShadowMode.BakedOnly,
                0, 40f, 1, true, false);
            Assert.That(softOnBaked.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("soft-shadows-require-realtime-shadowing"));

            var contactOnBaked = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.BakedOnly,
                1, 40f, 1, false, false,
                1f, 0.8f, 0.05f, 0.4f, true);
            Assert.That(contactOnBaked.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("contact-shadows-require-realtime-shadowing"));

            ESRenderLightingRecipe enhanced = new ESRenderLightingRecipe(
                ESRenderVisualStyleId.NeonSciFi,
                ESRenderShadowMode.Realtime,
                4, 100f, 2, true, true,
                2f, 0.9f, 0.03f, 0.35f, true);
            Assert.That(enhanced.IsValid(out reason), Is.True, reason);
            Assert.That(enhanced.MainLightIntensity, Is.EqualTo(2f));
            Assert.That(enhanced.ContactShadows, Is.True);

            ESRenderLightingRecipe named = ESRenderLightingRecipe.Create(
                style: ESRenderVisualStyleId.NaturalPbr,
                shadowMode: ESRenderShadowMode.Mixed,
                additionalLightsPerObject: 2,
                shadowDistance: 80f,
                cascadeCount: 2,
                softShadows: true,
                reflectionProbes: true,
                contactShadows: true,
                ambientIntensity: 1.2f,
                useColorTemperature: true,
                colorTemperatureKelvin: 4200f,
                ambientColor: new ESRenderRgbColor(0.8f, 0.9f, 1f));
            Assert.That(named.IsValid(out reason), Is.True, reason);
            Assert.That(named.AmbientIntensity, Is.EqualTo(1.2f));
            Assert.That(named.UseColorTemperature, Is.True);
            Assert.That(named.MainLightTemperatureKelvin, Is.EqualTo(4200f));
            Assert.That(named.AmbientColor.Blue, Is.EqualTo(1f));

            ESRenderLightingRecipe performant = ESRenderLightingRecipe.Resolve(
                ESRenderVisualStyleId.NoirContrast,
                ESRenderQualityProfileId.Performant);
            Assert.That(performant.IsValid(out reason), Is.True, reason);
            Assert.That(performant.SoftShadows, Is.False);
            Assert.That(performant.ReflectionProbes, Is.False);
            Assert.That(performant.ShadowDistance, Is.LessThanOrEqualTo(35f));

            ESRenderLightingRecipe authored = ESRenderLightingRecipe.Create(
                ESRenderVisualStyleId.NaturalPbr,
                ESRenderShadowMode.Mixed,
                4, 120f, 4, true, true,
                ambientIntensity: 1.6f,
                useColorTemperature: true,
                colorTemperatureKelvin: 4800f);
            ESRenderLightingRecipe projected = ESRenderLightingRecipe.Resolve(
                authored,
                ESRenderQualityProfileId.Performant);
            Assert.That(projected.AmbientIntensity, Is.EqualTo(authored.AmbientIntensity));
            Assert.That(projected.UseColorTemperature, Is.EqualTo(authored.UseColorTemperature));
            Assert.That(projected.MainLightTemperatureKelvin, Is.EqualTo(authored.MainLightTemperatureKelvin));
            Assert.That(projected.AmbientColor.Green, Is.EqualTo(authored.AmbientColor.Green));
            Assert.That(ESRenderLightingRecipe.TryResolve(
                authored,
                ESRenderQualityProfileId.CombatReadability,
                out ESRenderLightingRecipe triedProjection,
                out reason), Is.True, reason);
            Assert.That(triedProjection.AmbientIntensity, Is.EqualTo(authored.AmbientIntensity));

            Assert.That(named.HasShadows, Is.True);
            Assert.That(named.UsesRealtimeShadows, Is.True);
            Assert.That(named.MainLightShadowsEnabled, Is.True);
            Assert.That(named.AdditionalLightShadowsEnabled, Is.True);
            Assert.That(named.EstimatedShadowPassBudget, Is.EqualTo(4));
        }

        [Test]
        public void RenderEffectsRecipe_CoversPostProcessAndVariantBudgets()
        {
            string reason;
            foreach (ESRenderVisualStyleId style in Enum.GetValues(typeof(ESRenderVisualStyleId)))
            {
                ESRenderEffectsRecipe recipe = ESRenderEffectsRecipe.Resolve(style);
                Assert.That(recipe.IsValid(out reason), Is.True, reason);
                Assert.That(recipe.ParticleBudget, Is.LessThanOrEqualTo(recipe.TransparentBudget * 4));
            }

            ESRenderEffectsRecipe mobile = ESRenderEffectsRecipe.Resolve(ESRenderVisualStyleId.MobileFlat);
            Assert.That(mobile.DecalBudget, Is.EqualTo(0));
            Assert.That(mobile.Bloom, Is.False);
            Assert.That(mobile.ShaderVariantBudget, Is.EqualTo(8));

            var invalid = new ESRenderEffectsRecipe(
                ESRenderVisualStyleId.NaturalPbr,
                1, 9, 0, false, true, true, false, false, 8);
            Assert.That(invalid.IsValid(out reason), Is.False);
            Assert.That(reason, Is.EqualTo("particle-budget-exceeds-transparent-capacity"));
        }

        [Test]
        public void RenderTemplateBundle_IsVersionedAndCompatibilityBounded()
        {
            ESRenderTemplateBundle bundle = ESRenderTemplateBundle.CreateBuiltIn();
            string reason;
            Assert.That(bundle.Validate(out reason), Is.True, reason);
            Assert.That(bundle.StyleCount, Is.EqualTo(10));
            Assert.That(bundle.IsCompatible("2022.3.45f1", "14.0.11", out reason), Is.True, reason);
            Assert.That(bundle.IsCompatible("6000.0.0f1", "14.0.11", out reason), Is.True, reason);
            Assert.That(bundle.IsCompatible("2022.3.45f1", "15.0.0", out reason), Is.False);
            Assert.That(reason, Is.EqualTo("urp-version-mismatch"));
        }

        [Test]
        public void RenderTemplateCatalog_ResolvesEveryStyleScenePlatformCombination()
        {
            string reason;
            Assert.That(ESRenderTemplateCatalog.CombinationCount, Is.EqualTo(240));
            Assert.That(ESRenderTemplateCatalog.ValidateBuiltIn(out reason), Is.True, reason);

            ESRenderResolvedConfiguration configuration;
            Assert.That(ESRenderTemplateCatalog.TryResolve(
                ESRenderVisualStyleId.TacticalRealism,
                ESRenderSceneIntentId.Combat,
                ESRenderPlatformId.Mobile,
                out configuration,
                out reason), Is.True, reason);
            Assert.That(configuration.QualityProfile, Is.EqualTo(ESRenderQualityProfileId.MobileStable));
            Assert.That(configuration.QualityDowngraded, Is.True);
        }

        [Test]
        public void RenderTemplateResourceMap_ProvidesCompleteEsOwnedResourceIdentity()
        {
            string reason;
            Assert.That(ESRenderTemplateResourceMap.ValidateCatalog(out reason), Is.True, reason);

            ESRenderTemplateResourceBinding binding;
            Assert.That(ESRenderTemplateResourceMap.TryGet(
                ESRenderVisualStyleId.NeonSciFi,
                ESRenderQualityProfileId.HighFidelity,
                out binding,
                out reason), Is.True, reason);
            Assert.That(binding.RendererAssetPath, Is.EqualTo("Assets/Settings/URP-HighFidelity-Renderer.asset"));
            Assert.That(binding.VolumeProfileKey, Is.EqualTo("Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-NeonSciFi.volume.json"));
            Assert.That(binding.MaterialRecipeKey, Is.EqualTo("Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-NeonSciFi.mat"));
        }

        [Test]
        public void RenderTemplatePlan_IsDeterministicDryRunAndCarriesResourceProjection()
        {
            string reason;
            ESRenderTemplatePlan plan;
            Assert.That(ESRenderTemplatePlan.TryCreate(
                ESRenderVisualStyleId.NeonSciFi,
                ESRenderSceneIntentId.Cinematic,
                ESRenderPlatformId.Console,
                out plan,
                out reason), Is.True, reason);
            Assert.That(plan.IsDryRun, Is.True);
            Assert.That(plan.PlanId, Is.EqualTo("es.urp.template.NeonSciFi.Cinematic.Console"));
            Assert.That(plan.Resources.RendererAssetPath, Is.EqualTo("Assets/Settings/URP-HighFidelity-Renderer.asset"));
        }

        [Test]
        public void RenderTemplateResourceMap_AllStylesUseEsPhysicalAssetProjection()
        {
            for (int i = 0; i < ESRenderStyleCatalog.Count; i++)
            {
                ESRenderVisualStyleId style = ESRenderStyleCatalog.GetStyleIdAt(i);
                ESRenderStylePreset preset = ESRenderStylePreset.Resolve(style);
                ESRenderTemplateResourceBinding binding;
                string reason;
                Assert.That(ESRenderTemplateResourceMap.TryGet(style, preset.QualityProfile, out binding, out reason), Is.True, reason);
                Assert.That(binding.RendererAssetPath, Does.StartWith("Assets/Settings/URP-"));
                Assert.That(binding.VolumeProfileKey, Does.StartWith("Assets/Plugins/ES/"));
                Assert.That(binding.MaterialRecipeKey, Does.StartWith("Assets/Plugins/ES/"));
                Assert.That(binding.ShaderFamilyKey, Does.StartWith("Assets/Plugins/ES/"));
            }
        }
    }
}
