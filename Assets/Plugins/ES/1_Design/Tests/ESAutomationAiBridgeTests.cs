using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESAutomationAiBridgeTests
    {
        private IDisposable authorizationScope;
        private IDisposable controlActionAuditScope;
        private readonly HashSet<string> testApprovalIds = new HashSet<string>(StringComparer.Ordinal);

        [SetUp]
        public void SetUp()
        {
            // EditMode 测试由 Unity Editor 主线程执行；显式走同一 AssemblyStream 初始化入口，
            // 使 ExecuteJson 的主线程拒绝路径不依赖测试运行器的隐式初始化时机。
            new ESAutomationAiBridgeInitializer().InitInvoke();
            authorizationScope = ESAutomationAiBridge.Internal_BeginTestAuthorizationScope(true);
            controlActionAuditScope = ESAutomationAiBridge.Internal_BeginTestControlActionAuditScope();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string approvalId in testApprovalIds)
                ESAutomationAiBridge.Internal_RemoveTestSceneModificationApproval(approvalId);
            testApprovalIds.Clear();
            controlActionAuditScope?.Dispose();
            controlActionAuditScope = null;
            authorizationScope?.Dispose();
            authorizationScope = null;
        }

        [Test]
        public void ExecuteJson_RequiresCurrentUserAuthorization()
        {
            authorizationScope.Dispose();
            authorizationScope = ESAutomationAiBridge.Internal_BeginTestAuthorizationScope(false);

            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': 'aabbccddeeff00112233445566778899',
                'actorId': 'codex.local',
                'action': 'listTasks',
                'payload': {}
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("尚未获得当前用户授权"));
        }

        [Test]
        public void ExecuteJson_RejectsUnknownEnvelopeField()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '0123456789abcdef0123456789abcdef',
                'actorId': 'codex.local',
                'action': 'listTasks',
                'payload': {},
                'unexpected': true
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("未注册 unexpected"));
        }

        [Test]
        public void ExecuteJson_RejectsArbitraryContentType()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': 'fedcba9876543210fedcba9876543210',
                'actorId': 'codex.local',
                'action': 'submitContentProposal',
                'payload': {
                    'contentType': 'es.unregistered.content',
                    'contentVersion': 1,
                    'schemaHash': '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef',
                    'payload': {}
                }
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("未注册内容类型"));
        }

        [Test]
        public void ExecuteJson_ListTasks_UsesStructuredResponse()
        {
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '00112233445566778899aabbccddeeff',
                'actorId': 'codex.local',
                'action': 'listTasks',
                'payload': {}
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Completed"));
            Assert.That(responseJson, Does.Contain("\"tasks\":"));
            Assert.That(responseJson, Does.Contain("\"contentTypes\":"));
        }

        [Test]
        public void ExecuteJson_RejectsReplayedControlActionRequestId()
        {
            string requestId = Guid.NewGuid().ToString("N");
            string requestJson = @"{
                'protocolVersion': 1,
                'requestId': '" + requestId + @"',
                'actorId': 'codex.local',
                'action': 'getUnityCompilationState',
                'payload': {}
            }";

            ESAutomationResponseSummary first = JsonUtility.FromJson<ESAutomationResponseSummary>(
                ESAutomationAiBridge.ExecuteJson(requestJson));
            ESAutomationResponseSummary replay = JsonUtility.FromJson<ESAutomationResponseSummary>(
                ESAutomationAiBridge.ExecuteJson(requestJson));

            Assert.That(first.status, Is.EqualTo("Completed"));
            Assert.That(replay.status, Is.EqualTo("Rejected"));
            Assert.That(replay.message, Does.Contain("拒绝重放"));
        }

        [Test]
        public void ExecuteJson_RejectsNonEditorThread()
        {
            string responseJson = null;
            var worker = new Thread(() =>
            {
                responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                    'protocolVersion': 1,
                    'requestId': 'ffeeddccbbaa99887766554433221100',
                    'actorId': 'codex.local',
                    'action': 'listTasks',
                    'payload': {}
                }");
            });

            worker.Start();
            Assert.That(worker.Join(3000), Is.True, "后台调用线程未在限定时间内返回。");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("Unity Editor 主线程"));
        }

        [Test]
        public void ExecuteJson_SceneWriteAuditCarriesSubmittedApprovalId()
        {
            string requestId = Guid.NewGuid().ToString("N");
            const string approvalId = "0123456789abcdef0123456789abcdef";
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '" + requestId + @"',
                'actorId': 'codex.local',
                'action': 'modifyActiveScene',
                'payload': {
                    'scenePath': 'Assets/Scenes/Main.unity',
                    'operations': [{
                        'operation': 'setActive',
                        'targetPath': 'Main/Gameplay',
                        'value': true
                    }],
                    'save': false,
                    'dryRun': false,
                    'approvalId': '" + approvalId + @"'
                }
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("场景批准不存在"));

            string auditPath = Path.Combine(ESAutomationAiBridge.ControlActionAuditDirectory, requestId + ".json");
            JObject audit = JObject.Parse(File.ReadAllText(auditPath, new UTF8Encoding(false, true)));
            Assert.That((string)audit["approvalId"], Is.EqualTo(approvalId));
        }

        [Test]
        public void ExecuteJson_RejectsSceneWriteWithoutOneTimeApproval()
        {
            string requestId = Guid.NewGuid().ToString("N");
            string responseJson = ESAutomationAiBridge.ExecuteJson(@"{
                'protocolVersion': 1,
                'requestId': '" + requestId + @"',
                'actorId': 'codex.local',
                'action': 'modifyActiveScene',
                'payload': {
                    'scenePath': 'Assets/Scenes/Main.unity',
                    'operations': [{
                        'operation': 'setActive',
                        'targetPath': 'Main/Gameplay',
                        'value': true
                    }],
                    'save': false,
                    'dryRun': false,
                    'approvalId': '0123456789abcdef0123456789abcdef'
                }
            }");

            ESAutomationResponseSummary response = JsonUtility.FromJson<ESAutomationResponseSummary>(responseJson);
            Assert.That(response.status, Is.EqualTo("Rejected"));
            Assert.That(response.message, Does.Contain("场景批准不存在"));
        }

        [Test]
        public void SceneModificationApproval_RejectAndRevokeHaveDistinctStateAndAuditSemantics()
        {
            string rejectedId = CreateTestSceneModificationApproval(out string rejectedAuditPath);
            Assert.That(ESAutomationAiBridge.TryRevokeSceneModificationApproval(rejectedId,
                out string prematureRevokeReason), Is.False);
            Assert.That(prematureRevokeReason, Does.Contain("AwaitingUserApproval"));
            Assert.That(ESAutomationAiBridge.CopyPendingSceneModificationApprovals()
                .Any(item => item.ApprovalId == rejectedId), Is.True,
                "错误前态不得移除待审批计划。");
            Assert.That(ESAutomationAiBridge.TryRejectSceneModification(rejectedId,
                out string rejectReason), Is.True, rejectReason);
            JObject rejectedAudit = JObject.Parse(File.ReadAllText(rejectedAuditPath,
                new UTF8Encoding(false, true)));
            Assert.That((string)rejectedAudit["status"], Is.EqualTo("RejectedByUser"));

            string revokedId = CreateTestSceneModificationApproval(out string revokedAuditPath);
            Assert.That(ESAutomationAiBridge.TryApproveSceneModification(revokedId,
                out string approveReason), Is.True, approveReason);
            Assert.That(ESAutomationAiBridge.TryRejectSceneModification(revokedId,
                out string lateRejectReason), Is.False);
            Assert.That(lateRejectReason, Does.Contain("Approved"));
            Assert.That(ESAutomationAiBridge.CopyPendingSceneModificationApprovals()
                .Any(item => item.ApprovalId == revokedId && item.Status == "Approved"), Is.True,
                "错误前态不得移除已批准计划。");
            Assert.That(ESAutomationAiBridge.TryRevokeSceneModificationApproval(revokedId,
                out string revokeReason), Is.True, revokeReason);
            JObject revokedAudit = JObject.Parse(File.ReadAllText(revokedAuditPath,
                new UTF8Encoding(false, true)));
            Assert.That((string)revokedAudit["status"], Is.EqualTo("RevokedByUser"));
        }

        [Test]
        public void TestApprovalCleanup_RemovesOnlyOwnedApprovalAndPreservesExistingState()
        {
            string existingId = ESAutomationAiBridge.Internal_CreateTestSceneModificationApproval(
                out string existingAuditPath);
            try
            {
                string ownedId = CreateTestSceneModificationApproval(out _);

                Assert.That(ESAutomationAiBridge.Internal_RemoveTestSceneModificationApproval(ownedId),
                    Is.True);
                testApprovalIds.Remove(ownedId);
                Assert.That(ESAutomationAiBridge.CopyPendingSceneModificationApprovals()
                    .Any(item => item.ApprovalId == existingId), Is.True);
                JObject audit = JObject.Parse(File.ReadAllText(existingAuditPath,
                    new UTF8Encoding(false, true)));
                Assert.That((string)audit["status"], Is.EqualTo("AwaitingUserApproval"));
            }
            finally
            {
                ESAutomationAiBridge.Internal_RemoveTestSceneModificationApproval(existingId);
            }
        }

        private string CreateTestSceneModificationApproval(out string auditPath)
        {
            string approvalId = ESAutomationAiBridge.Internal_CreateTestSceneModificationApproval(
                out auditPath);
            testApprovalIds.Add(approvalId);
            return approvalId;
        }

        [Test]
        public void SceneScan_ExplicitlyDisallowsPlayMode()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ESAutomationSceneScanPrototype).TypeHandle);

            ESAutomationTaskDescriptor sceneScan = FindSceneScanDescriptor();

            Assert.That(sceneScan, Is.Not.Null);
            Assert.That(sceneScan.allowAiInvoke, Is.True);
            Assert.That(sceneScan.allowInPlayMode, Is.False);
        }

        [Test]
        public void SceneScan_ExposesInteractivePresetAndTypedInputSchema()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ESAutomationSceneScanPrototype).TypeHandle);
            ESAutomationTaskDescriptor sceneScan = FindSceneScanDescriptor();

            Assert.That(sceneScan, Is.Not.Null);
            ESAutomationTaskPresetDescriptor interactivePreset = null;
            foreach (ESAutomationTaskPresetDescriptor preset in sceneScan.presets)
            {
                if (preset != null && preset.presetId == "interactive")
                {
                    interactivePreset = preset;
                    break;
                }
            }
            Assert.That(interactivePreset, Is.Not.Null);
            Assert.That(sceneScan.TryGetInputSchema("scene-scan.report-options", sceneScan.inputSchemaHash, out ESAutomationInputSchemaDescriptor schema), Is.True);
            Assert.That(schema.fields.Count, Is.EqualTo(3));
            Assert.That(schema.fields[0].fieldId, Is.EqualTo("includeInactive"));
            Assert.That(schema.fields[0].valueType, Is.EqualTo("Boolean"));
            Assert.That(schema.fields[1].fieldId, Is.EqualTo("detailMode"));
            Assert.That(schema.fields[1].valueType, Is.EqualTo("Choice"));
            Assert.That(schema.fields[2].fieldId, Is.EqualTo("topComponentCount"));
            Assert.That(schema.fields[2].valueType, Is.EqualTo("Integer"));
            Assert.That(schema.fields[2].minimumInteger, Is.EqualTo(1));
            Assert.That(schema.fields[2].maximumInteger, Is.EqualTo(50));
        }

        [Test]
        public void CompletionDecision_RejectsEmptyEvidence()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void AcceptanceCriteria_RequiresRequiredCriterion()
        {
            var criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "optional-only",
                        verifierId = "test.verifier",
                        required = false,
                    },
                },
            };
            Assert.Throws<InvalidOperationException>(() => criteria.Validate());
        }

        [Test]
        public void PerformanceBudget_RejectsRetryOverflow()
        {
            var budget = new ESAutomationPerformanceBudget
            {
                maxDurationSeconds = 60,
                maxOutputBytes = 1024,
                maxRetryCount = 0,
                maxFindingCount = 10,
            };
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                workerType = "Other",
                workerId = "test.worker",
                workerVersion = "1.0.0",
                entrypointHash = new string('a', 64),
                inputManifestHash = new string('b', 64),
                startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                finishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                retryCount = 1,
            };
            Assert.That(budget.TryValidateRunResult(result, out string reason), Is.False);
            Assert.That(reason, Does.Contain("maxRetryCount"));
        }

        [Test]
        public void PerformanceBudget_RejectsUnverifiableOutput()
        {
            var budget = new ESAutomationPerformanceBudget
            {
                maxDurationSeconds = 60,
                maxOutputBytes = 1024,
                maxRetryCount = 0,
                maxFindingCount = 10,
            };
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                workerType = "Other",
                workerId = "test.worker",
                workerVersion = "1.0.0",
                entrypointHash = new string('a', 64),
                inputManifestHash = new string('b', 64),
                startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                finishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                outputs = new List<string> { "missing-output.json" },
                outputHashes = new List<string> { new string('c', 64) },
            };
            Assert.That(budget.TryValidateRunResult(result, out string reason), Is.False);
            Assert.That(reason, Does.Contain("cannot be verified"));
        }

        [Test]
        public void ReleaseGate_RejectsUndeclaredOutputName()
        {
            var declarations = new List<string> { "scene-scan.json", "scene-scan.md" };
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput("scene-scan.json", declarations), Is.True);
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput("unexpected.bin", declarations), Is.False);
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput(
                "F:/project/reports/scene-scan.md", declarations), Is.True);
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput(
                "F:/project/temp/scene-scan.json",
                new List<string> { "reports/scene-scan.json" }), Is.False);
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput(
                "reports/scene-scan.json",
                new List<string> { "reports/scene-scan.json" }), Is.True);
            Assert.That(ESAutomationReleaseGate.IsDeclaredOutput(
                "REPORTS/SCENE-SCAN.JSON",
                new List<string> { "reports/scene-scan.json" }), Is.True);
        }

        [Test]
        public void CompletionDecision_RejectsUnregisteredVerifier()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "test.criterion",
                        verifierId = "test.unregistered-verifier",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void CompletionDecision_UsesRegisteredVerifierImplementation()
        {
            ESAutomationVerifierRegistry.Register("test.rejecting-verifier", _ => false);
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "test.criterion",
                        verifierId = "test.rejecting-verifier",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void CompletionDecision_RejectsMissingRequiredContractCriterion()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "required-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        required = true,
                    },
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "second-required-output",
                        verifierId = "es.feishu.output-hash",
                        required = true,
                    },
                },
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "required-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
            };
            Assert.That(decision.CanAccept(criteria), Is.False);
        }

        [Test]
        public void CompletionDecision_GovernedAcceptanceRequiresExplicitAcceptedStatus()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "declared-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                    },
                },
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = true,
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "declared-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
            };

            Assert.That(decision.CanAccept(criteria), Is.False);
        }

        [Test]
        public void CompletionDecision_RejectsStaticEvidenceForRuntimeCriterion()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "runtime-layout",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        runtimeRequired = true,
                    },
                },
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "runtime-layout",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceScope = ESAutomationEvidenceScope.Static,
                        evidenceHash = new string('a', 64),
                    },
                },
            };

            Assert.That(decision.CanAccept(criteria), Is.False);
        }

        [Test]
        public void CompletionEvaluation_DoesNotBypassRuntimeCriterion()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "runtime-required",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        runtimeRequired = true,
                    },
                },
            };
            bool accepted = ESAutomationGovernance.TryEvaluateCompletion(
                criteria,
                Guid.NewGuid().ToString("N"),
                "Passed",
                new[]
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "runtime-required",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceScope = ESAutomationEvidenceScope.Static,
                        evidenceHash = new string('a', 64),
                    },
                },
                null,
                false,
                false,
                true,
                out _,
                out _);

            Assert.That(accepted, Is.False);
        }

        [Test]
        public void CompletionDecision_StaticReviewIsDistinctFromRuntimeAcceptance()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = false,
                staticCodeStatus = "passed",
                staticContractStatus = "passed",
                staticBoundaryStatus = "passed",
                evidenceStatus = "fresh",
                runtimeStatus = "runtime-not-run",
            };

            decision.RefreshDecisionSemantics();

            Assert.That(decision.decisionStatus, Is.EqualTo("StaticReviewComplete"));
            Assert.That(decision.blockingLayer, Is.EqualTo("runtime"));
            Assert.That(decision.accepted, Is.False);
        }

        [Test]
        public void CompletionDecision_PersistedAcceptanceCannotBypassPendingEvidence()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = true,
                evidenceStatus = "missing",
                runtimeStatus = "passed",
            };

            decision.RefreshDecisionSemantics();

            Assert.That(decision.accepted, Is.False);
            Assert.That(decision.decisionStatus, Is.EqualTo("Unverified"));
            Assert.That(decision.blockingLayer, Is.EqualTo("evidence"));
        }

        [Test]
        public void CompletionDecision_MissingEvidenceIsNotStaticCompletion()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                staticCodeStatus = "passed",
                staticContractStatus = "passed",
                staticBoundaryStatus = "passed",
                runtimeStatus = "runtime-not-run",
            };

            decision.RefreshDecisionSemantics();

            Assert.That(decision.decisionStatus, Is.EqualTo("Unverified"));
            Assert.That(decision.blockingLayer, Is.EqualTo("evidence"));
        }

        [Test]
        public void CompletionDecision_StaticBoundaryCannotBeAcceptedByReceipt()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = true,
                staticCodeStatus = "passed",
                staticContractStatus = "passed",
                staticBoundaryStatus = "blocked",
                evidenceStatus = "fresh",
                runtimeStatus = "passed",
            };

            decision.RefreshDecisionSemantics();

            Assert.That(decision.decisionStatus, Is.EqualTo("Blocked"));
            Assert.That(decision.blockingLayer, Is.EqualTo("static-boundary"));
        }

        [Test]
        public void CompletionDecision_RejectsContradictoryPersistedStatus()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = true,
                decisionStatus = "Blocked",
            };

            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void CompletionDecision_RejectsUndeclaredContractCriterion()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "declared-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                    },
                },
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "undeclared-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
            };
            Assert.That(decision.CanAccept(criteria), Is.False);
        }

        [Test]
        public void CompletionDecision_RejectsFreshnessPolicyBypass()
        {
            ESAutomationAcceptanceCriteria criteria = new ESAutomationAcceptanceCriteria
            {
                freshnessPolicy = new ESAutomationFreshnessPolicy
                {
                    maxAgeHours = 24,
                    requireSourceHash = true,
                    allowRuntimeNotRun = false,
                },
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "fresh-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                    },
                },
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "fresh-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
                freshnessPolicy = null,
            };
            Assert.That(decision.CanAccept(criteria), Is.False);
        }

        [Test]
        public void ReleaseGate_RequiresEvidenceSourceBindingWhenSnapshotExists()
        {
            ESAutomationExecutionSnapshot snapshot = CreateSnapshot("a");
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                executionStatus = "Passed",
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "snapshot-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('b', 64),
                        evidenceBinding = new ESAutomationClaimEvidenceBinding
                        {
                            claimId = "claim-1",
                            criterionId = "snapshot-output",
                            evidenceHash = new string('b', 64),
                            sourceHash = new string('c', 64),
                            capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        },
                    },
                },
            };
            Assert.That(decision.CanAccept(new ESAutomationAcceptanceCriteria
            {
                freshnessPolicy = new ESAutomationFreshnessPolicy { requireSourceHash = true },
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "snapshot-output",
                        verifierId = "es.scene.scan.promoted-output-hash",
                    },
                },
            }), Is.True);
            Assert.That(ESAutomationReleaseGate.EvidenceBindsToSnapshot(decision, snapshot), Is.False);
        }

        [Test]
        public void GovernanceSnapshot_DetectsInputManifestDrift()
        {
            ESAutomationExecutionSnapshot expected = CreateSnapshot("a");
            ESAutomationExecutionSnapshot actual = CreateSnapshot("a");
            actual.inputManifestHash = new string('9', 64);
            Assert.That(ESAutomationGovernance.MatchesSnapshot(expected, actual, out string reason), Is.False);
            Assert.That(reason, Does.Contain("drift"));
            Assert.That(ESAutomationGovernance.MatchesInputManifest(new string('8', 64), actual, out string inputReason), Is.False);
            Assert.That(inputReason, Does.Contain("Manifest Hash"));
        }

        [Test]
        public void GovernanceSnapshot_BindsTaskContractHash()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            ESAutomationExecutionSnapshot snapshot = CreateSnapshot("a");
            snapshot.taskContractHash = contract.ComputeStableHash();
            Assert.That(ESAutomationGovernance.MatchesTaskContract(contract, snapshot, out _), Is.True);
            contract.timeoutSeconds++;
            Assert.That(ESAutomationGovernance.MatchesTaskContract(contract, snapshot, out string reason), Is.False);
            Assert.That(reason, Does.Contain("TaskContract Hash"));
        }

        [Test]
        public void GovernanceSnapshot_StrictReadinessKeepsLegacyProducerUnchanged()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            ESAutomationRunResult legacyResult = new ESAutomationRunResult();
            Assert.That(ESAutomationGovernance.IsStrictSnapshotBindingReady(
                contract, legacyResult, out string reason), Is.False);
            Assert.That(reason, Does.Contain("ExecutionSnapshot"));
        }

        [Test]
        public void FreshnessPolicy_StrictBindingRequiresSourceHash()
        {
            ESAutomationFreshnessPolicy policy = new ESAutomationFreshnessPolicy
            {
                requireExecutionSnapshotBinding = true,
                requireSourceHash = false,
            };
            Assert.Throws<InvalidOperationException>(() => policy.Validate());
        }

        [Test]
        public void VerifierRegistry_RejectsReplacement()
        {
            ESAutomationVerifierRegistry.Register("test.immutable-verifier", _ => true);
            Assert.Throws<InvalidOperationException>(() =>
                ESAutomationVerifierRegistry.Register("test.immutable-verifier", _ => false));
        }

        [Test]
        public void BuiltInVerifier_RequiresEvidenceHash()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void CompletionDecision_RequiresClaimBindingWhenPolicyRequiresSource()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                freshnessPolicy = new ESAutomationFreshnessPolicy { requireSourceHash = true },
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void CompletionDecision_RejectsExpiredEvidenceBinding()
        {
            string evidenceHash = new string('a', 64);
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                freshnessPolicy = new ESAutomationFreshnessPolicy { maxAgeHours = 1, requireSourceHash = true },
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = evidenceHash,
                        evidenceBinding = new ESAutomationClaimEvidenceBinding
                        {
                            claimId = "scene-scan.report-json",
                            criterionId = "scene-scan.report-json",
                            evidenceHash = evidenceHash,
                            sourceHash = new string('b', 64),
                            capturedAtUtc = DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
                        },
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void ExecutionSnapshot_RejectsSourceDrift()
        {
            ESAutomationExecutionSnapshot expected = CreateSnapshot("a");
            ESAutomationExecutionSnapshot actual = CreateSnapshot("b");
            Assert.That(ESAutomationGovernance.MatchesSnapshot(expected, actual, out string reason), Is.False);
            Assert.That(reason, Does.Contain("drift"));
        }

        [Test]
        public void CompletionDecision_RejectsMismatchedEvidenceBinding()
        {
            string evidenceHash = new string('a', 64);
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                freshnessPolicy = new ESAutomationFreshnessPolicy { requireSourceHash = true },
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = evidenceHash,
                        evidenceBinding = new ESAutomationClaimEvidenceBinding
                        {
                            claimId = "scene-scan.report-markdown",
                            criterionId = "scene-scan.report-markdown",
                            evidenceHash = new string('b', 64),
                            sourceHash = new string('c', 64),
                            capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        },
                    },
                },
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void GenericCompletionEvaluation_PreservesFreshnessPolicy()
        {
            var criteria = new ESAutomationAcceptanceCriteria
            {
                freshnessPolicy = new ESAutomationFreshnessPolicy { requireSourceHash = true },
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                    },
                },
            };
            bool accepted = ESAutomationGovernance.TryEvaluateCompletion(
                criteria,
                Guid.NewGuid().ToString("N"),
                "Passed",
                new[]
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "scene-scan.report-json",
                        verifierId = "es.scene.scan.promoted-output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                    },
                },
                null,
                false,
                false,
                true,
                out ESAutomationCompletionDecision decision,
                out _);
            Assert.That(accepted, Is.False);
            Assert.That(decision.freshnessPolicy, Is.Not.Null);
        }

        [Test]
        public void TaskContract_RejectsCapabilityEnvelopeMismatch()
        {
            var contract = new ESAutomationTaskContract
            {
                taskId = "es.test.capability",
                version = 1,
                worker = new ESAutomationWorkerRegistration
                {
                    type = "Other",
                    workerId = "test.worker",
                    version = "1.0.0",
                    entrypointHash = new string('a', 64),
                },
                capabilities = new List<string> { "ReadArtifacts" },
                readRoots = new List<string> { "ES/Automation/Temp" },
                capabilityEnvelope = new ESAutomationCapabilityEnvelope
                {
                    taskContract = ESAutomationCapability.None,
                    workerCapability = ESAutomationCapability.None,
                    projectBoundary = ESAutomationCapability.None,
                },
            };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void TaskContract_RejectsUnknownCapabilityBits()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            contract.capabilities = new List<string> { "ReadArtifacts" };
            contract.readRoots = new List<string> { "ES/Automation/Temp" };
            contract.capabilityEnvelope = new ESAutomationCapabilityEnvelope
            {
                taskContract = ESAutomationCapability.ReadArtifacts,
                workerCapability = ESAutomationCapability.ReadArtifacts,
                projectBoundary = ESAutomationCapability.ReadArtifacts,
                userAuthorization = ESAutomationCapability.ReadArtifacts,
                aiCommand = (ESAutomationCapability)(1 << 30),
            };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void CapabilityEnvelope_RequiresInvocationIdentityAtExecutionBoundary()
        {
            var envelope = new ESAutomationCapabilityEnvelope
            {
                userAuthorization = ESAutomationCapability.ReadArtifacts,
                taskContract = ESAutomationCapability.ReadArtifacts,
                aiCommand = ESAutomationCapability.ReadArtifacts,
                workerCapability = ESAutomationCapability.ReadArtifacts,
                projectBoundary = ESAutomationCapability.ReadArtifacts,
            };
            string reason;
            Assert.That(envelope.AllowsInvocation(new ESAutomationTaskInvocation
            {
                fromAi = true,
                actorId = "aibrain",
                brainPlanHash = string.Empty,
            }, ESAutomationCapability.ReadArtifacts, out reason), Is.False);
            Assert.That(reason, Does.Contain("PlanHash"));
        }

        [Test]
        public void EvidenceBinding_StrictSnapshotModeRequiresAllExecutionHashes()
        {
            var snapshot = new ESAutomationExecutionSnapshot
            {
                snapshotId = "snapshot-1",
                inputManifestHash = new string('1', 64),
                sourceHash = new string('2', 64),
                taskContractHash = new string('3', 64),
                commandHash = new string('4', 64),
                brainPlanHash = new string('5', 64),
            };
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "strict-criterion",
                        verifierId = "test.strict",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = new string('a', 64),
                        evidenceBinding = new ESAutomationClaimEvidenceBinding
                        {
                            claimId = "claim-1",
                            criterionId = "strict-criterion",
                            evidenceHash = new string('a', 64),
                            sourceHash = new string('2', 64),
                            capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        },
                    },
                },
            };

            Assert.That(ESAutomationReleaseGate.EvidenceBindsToSnapshot(decision, snapshot, true), Is.False);
            Assert.That(ESAutomationReleaseGate.EvidenceBindsToSnapshot(
                new ESAutomationCompletionDecision
                {
                    runId = Guid.NewGuid().ToString("N"),
                    criterionResults = new List<ESAutomationCriterionResult>(),
                }, snapshot, true), Is.False);
        }

        [Test]
        public void TaskContract_RejectsUnsafeOutputDeclaration()
        {
            var contract = new ESAutomationTaskContract
            {
                taskId = "es.test.output-contract",
                version = 1,
                worker = new ESAutomationWorkerRegistration
                {
                    type = "Other",
                    workerId = "test.output.worker",
                    version = "1.0.0",
                    entrypointHash = new string('c', 64),
                },
                outputs = new List<string> { "../outside.json" },
            };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void TaskContract_RejectsInvalidInputSchemaHash()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            contract.inputSchemaHash = "not-a-sha256";
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void TaskContract_RejectsUnsafeInputDeclaration()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            contract.inputs = new List<string> { "../outside.json" };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void TaskContract_RejectsCaseCollisionInDeclarations()
        {
            ESAutomationTaskContract contract = CreateGovernedContract();
            contract.inputs = new List<string> { "Result.json", "result.json" };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());

            contract.inputs = new List<string>();
            contract.outputs = new List<string> { "Report.json", "report.json" };
            Assert.Throws<InvalidOperationException>(() => contract.Validate());
        }

        [Test]
        public void Facade_RejectsContractTaskWithoutBoundEndpoint()
        {
            const string taskId = "es.test.binding-contract";
            ESAutomationTaskRegistry.Register(new ESAutomationTaskContract
            {
                taskId = taskId,
                version = 1,
                worker = new ESAutomationWorkerRegistration
                {
                    type = "Other",
                    workerId = "test.binding.worker",
                    version = "1.0.0",
                    entrypointHash = new string('b', 64),
                    enabled = true,
                },
            });

            Assert.Throws<InvalidOperationException>(() => ESAutomationFacade.Register(
                new UnboundEndpoint(new ESAutomationTaskDescriptor
                {
                    taskId = taskId,
                    taskVersion = 1,
                    category = "Test",
                    displayName = "Binding",
                    summary = "Binding",
                })));
        }

        [Test]
        public void CompletionDecision_RejectsMalformedReceipt()
        {
            var decision = new ESAutomationCompletionDecision();
            Assert.Throws<InvalidOperationException>(() => decision.Validate());
            Assert.That(decision.CanAccept(), Is.False);
        }

        [Test]
        public void CompletionDecision_RejectsMalformedDecisionIdAndCriterion()
        {
            var decision = new ESAutomationCompletionDecision
            {
                decisionId = "not-a-guid",
                runId = Guid.NewGuid().ToString("N"),
                criterionResults = new List<ESAutomationCriterionResult>(),
            };
            Assert.Throws<InvalidOperationException>(() => decision.Validate());

            decision.decisionId = Guid.NewGuid().ToString("N");
            decision.criterionResults.Add(new ESAutomationCriterionResult());
            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void CriterionResult_RejectsEvidenceBindingDrift()
        {
            var result = new ESAutomationCriterionResult
            {
                criterionId = "criterion.output",
                verifierId = "es.scene.scan.promoted-output-hash",
                passed = true,
                evidenceState = ESAutomationEvidenceState.Fresh,
                evidenceHash = new string('a', 64),
                evidenceBinding = new ESAutomationClaimEvidenceBinding
                {
                    claimId = "claim.output",
                    criterionId = "criterion.other",
                    evidenceHash = new string('a', 64),
                    sourceHash = new string('b', 64),
                    capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                },
            };

            Assert.Throws<InvalidOperationException>(() => result.Validate());
        }

        private static ESAutomationExecutionSnapshot CreateSnapshot(string sourceMarker)
        {
            return new ESAutomationExecutionSnapshot
            {
                snapshotId = "snapshot-1",
                inputManifestHash = new string('1', 64),
                sourceHash = new string(sourceMarker[0], 64),
                taskContractHash = new string('2', 64),
                commandHash = new string('3', 64),
                brainPlanHash = new string('4', 64),
            };
        }

        private static ESAutomationTaskContract CreateGovernedContract()
        {
            return new ESAutomationTaskContract
            {
                taskId = "es.test.task",
                version = 1,
                worker = new ESAutomationWorkerRegistration
                {
                    type = "Other",
                    workerId = "test-worker",
                    version = "1",
                    entrypointHash = new string('a', 64),
                },
            };
        }

        [Test]
        public void RunResult_GovernanceFieldsRoundTrip()
        {
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                idempotencyKey = new string('a', 64),
                retryCount = 0,
                executionSnapshot = CreateSnapshot("a"),
                completionDecision = new ESAutomationCompletionDecision
                {
                    runId = "run",
                    executionStatus = "Passed",
                },
            };
            JObject root = JObject.FromObject(result);
            ESAutomationRunResult roundTrip = root.ToObject<ESAutomationRunResult>();
            Assert.That(roundTrip.idempotencyKey, Is.EqualTo(result.idempotencyKey));
            Assert.That(roundTrip.retryCount, Is.EqualTo(0));
            Assert.That(roundTrip.executionSnapshot.sourceHash, Is.EqualTo(result.executionSnapshot.sourceHash));
            Assert.That(roundTrip.completionDecision.executionStatus, Is.EqualTo("Passed"));
        }

        [Test]
        public void RunResult_RejectsCompletionDecisionBoundToDifferentRun()
        {
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                workerType = "Other",
                workerId = "test-worker",
                workerVersion = "1",
                entrypointHash = new string('a', 64),
                status = "Passed",
                startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                finishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                inputManifestHash = new string('b', 64),
                completionDecision = new ESAutomationCompletionDecision
                {
                    runId = Guid.NewGuid().ToString("N"),
                },
            };

            Assert.Throws<InvalidOperationException>(() => result.Validate());
        }

        [Test]
        public void CompletionDecision_RejectsAcceptedStatusWithoutAcceptanceFlag()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                decisionStatus = "Accepted",
                accepted = false,
            };

            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void CompletionDecision_RejectsBlockedStatusWithAcceptanceFlag()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                decisionStatus = "Blocked",
                accepted = true,
            };

            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void CompletionDecision_RejectsStaticReviewStatusWithAcceptanceFlag()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                decisionStatus = "StaticReviewComplete",
                accepted = true,
            };

            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void CompletionDecision_RejectsAcceptedStatusWithFailedExecution()
        {
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                decisionStatus = "Accepted",
                accepted = true,
                executionStatus = "Failed",
            };

            Assert.Throws<InvalidOperationException>(() => decision.Validate());
        }

        [Test]
        public void RunResult_RejectsNegativeRetryCountAndReversedTimestamps()
        {
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                workerType = "Other",
                workerId = "test-worker",
                workerVersion = "1",
                entrypointHash = new string('a', 64),
                status = "Passed",
                startedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                finishedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                inputManifestHash = new string('b', 64),
                retryCount = -1,
            };

            Assert.Throws<InvalidOperationException>(() => result.Validate());
        }

        [Test]
        public void RunResult_RejectsOutputPathTraversal()
        {
            var result = new ESAutomationRunResult
            {
                taskId = "es.test.task",
                taskVersion = 1,
                runId = Guid.NewGuid().ToString("N"),
                workerType = "Other",
                workerId = "test-worker",
                workerVersion = "1",
                entrypointHash = new string('a', 64),
                status = "Passed",
                startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                finishedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                inputManifestHash = new string('b', 64),
                outputs = new List<string> { "../outside.json" },
                outputHashes = new List<string> { new string('c', 64) },
            };
            Assert.Throws<InvalidOperationException>(() => result.Validate());
        }

        [Test]
        public void ReportCenter_RejectsUnmanagedReportPath()
        {
            bool accepted = ESAutomationReportCenter.TryReadJson("../outside/result.json",
                out ESAutomationRunResult result, out string reason);
            Assert.That(accepted, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void TraceReconciliation_RejectsUnbalancedToolLedger()
        {
            var trace = new ESAutomationTraceReconciliation
            {
                traceId = "trace-1",
                expectedToolCalls = 2,
                observedToolCalls = 1,
                reconciled = true,
            };
            trace.Validate();
            Assert.That(trace.CanAccept(), Is.False);

            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                traceReconciled = true,
                traceReconciliation = trace,
            };
            Assert.That(decision.CanAccept(), Is.False);
        }

        private static ESAutomationTaskDescriptor FindSceneScanDescriptor()
        {
            foreach (ESAutomationTaskDescriptor descriptor in ESAutomationFacade.CopyDescriptors())
            {
                if (descriptor.taskId == "es.scene.scan" && descriptor.taskVersion == 1)
                    return descriptor;
            }
            return null;
        }

        private sealed class UnboundEndpoint : IESAutomationTaskEndpoint
        {
            public UnboundEndpoint(ESAutomationTaskDescriptor descriptor) { Descriptor = descriptor; }
            public ESAutomationTaskDescriptor Descriptor { get; }
            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
                => ESAutomationTaskInvocationResult.Rejected("test");
            public ESAutomationTaskInvocationResult GetRun(string runId)
                => ESAutomationTaskInvocationResult.NotFound("test");
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.NotFound("test");
        }

        [Serializable]
        private sealed class ESAutomationResponseSummary
        {
            public string status = string.Empty;
            public string message = string.Empty;
        }
    }
}
