using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ES.EditorInternal.Tests
{
    public sealed class ESAgentAuthoringGraphTests
    {
        private sealed class ESFieldOptionalArgumentsFixture
        {
            [ESField]
            public string Defaults { get; set; }

            [ESField(ESFieldLevel.Important)]
            public string LevelOnly { get; set; }

            [ESField(Hint = "只填写提示")]
            public string HintOnly { get; set; }

            [ESField(Required = true)]
            public string RequiredOnly { get; set; }
        }

        [Test]
        public void BranchRoutes_SeparateEndpointCountFromPerEndpointConnectionCapacity()
        {
            AssertBranchOutputsAllowMultipleConnections(new ESGenericGraphAuthoringProfile(),
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.GenericBranch),
                "flow.true", "flow.false");
            AssertBranchOutputsAllowMultipleConnections(new ESStoryGraphAuthoringProfile(),
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.StoryCondition),
                "flow.true", "flow.false");
            AssertBranchOutputsAllowMultipleConnections(new ESAgentAuthoringGraphProfile(),
                ESGraphNodeTypeKey.Parse(ESAgentGraphStableIds.BranchNode),
                ESAgentGraphStableIds.BranchMatchedPortKey,
                ESAgentGraphStableIds.BranchDefaultPortKey);
            AssertBranchOutputsAllowSingleConnection(new ESAgentAuthoringGraphProfile(),
                ESGraphNodeTypeKey.Parse(ESAgentGraphStableIds.SkillBranchNode),
                ESAgentGraphStableIds.SkillMatchedPortKey,
                ESAgentGraphStableIds.SkillDefaultPortKey);
        }

        private static void AssertBranchOutputsAllowMultipleConnections(IESGraphAuthoringProfile profile,
            ESGraphNodeTypeKey nodeType, params string[] portKeys)
        {
            IESGraphNodeDefinition definition = profile.NodeDefinitions.Single(node =>
                string.Equals(node.NodeType.StableId, nodeType.StableId, StringComparison.Ordinal));
            Assert.That(portKeys.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(portKeys.Length));
            Assert.That(definition.Ports.Count(port => port.direction == ESGraphPortDirection.Output
                && portKeys.Contains(port.stableKey, StringComparer.Ordinal)),
                Is.EqualTo(portKeys.Length).And.GreaterThanOrEqualTo(2),
                profile.DisplayName + " 的分支必须先拥有多个不同稳定输出端点。");
            foreach (string portKey in portKeys)
            {
                ESGraphPortDefinition port = definition.Ports.Single(value =>
                    string.Equals(value.stableKey, portKey, StringComparison.Ordinal));
                Assert.That(port.direction, Is.EqualTo(ESGraphPortDirection.Output));
                Assert.That(port.capacity, Is.EqualTo(ESGraphPortCapacity.Multi),
                    profile.DisplayName + " 的分支路线必须允许多目标：" + portKey);
            }
        }

        private static void AssertBranchOutputsAllowSingleConnection(IESGraphAuthoringProfile profile,
            ESGraphNodeTypeKey nodeType, params string[] portKeys)
        {
            IESGraphNodeDefinition definition = profile.NodeDefinitions.Single(node =>
                string.Equals(node.NodeType.StableId, nodeType.StableId, StringComparison.Ordinal));
            Assert.That(portKeys.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(portKeys.Length));
            Assert.That(definition.Ports.Count(port => port.direction == ESGraphPortDirection.Output
                && portKeys.Contains(port.stableKey, StringComparer.Ordinal)),
                Is.EqualTo(portKeys.Length).And.GreaterThanOrEqualTo(2),
                profile.DisplayName + " 的执行分支仍是多输出端口节点，容量 Single 只限制每端点连接数。");
            foreach (string portKey in portKeys)
            {
                ESGraphPortDefinition port = definition.Ports.Single(value =>
                    string.Equals(value.stableKey, portKey, StringComparison.Ordinal));
                Assert.That(port.direction, Is.EqualTo(ESGraphPortDirection.Output));
                Assert.That(port.capacity, Is.EqualTo(ESGraphPortCapacity.Single),
                    profile.DisplayName + " 的执行分支路线必须只选择一个目标：" + portKey);
            }
        }

        [Test]
        public void AgentAuthoring_BranchBakePreservesEveryTargetOnTheSelectedRoute()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph,
                    ESAgentAuthoringPresetKind.MindMapPaired);
                ESGraphNodeRecord branch = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.BranchNode);
                ESGraphPortRecord matchedPort = branch.ports.Single(port =>
                    port.stableKey == ESAgentGraphStableIds.BranchMatchedPortKey);
                ESGraphNodeRecord extraTarget = graph.Nodes
                    .Where(node => node.typeId == ESAgentGraphStableIds.ConstraintNode)
                    .First(node => !graph.Edges.Any(edge =>
                        edge.outputPortId == matchedPort.portId
                        && node.ports.Any(port => port.portId == edge.inputPortId)));
                ConnectEndpoints(graph, branch, ESAgentGraphStableIds.BranchMatchedPortKey,
                    extraTarget, "flow.input");

                List<ESGraphValidationIssue> validation = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(validation.Any(IsError), Is.False, Describe(validation));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True,
                    Describe(bakeIssues));

                ESAgentGenerationBranch baked = spec.branches.Single(item =>
                    item.nodeId == branch.nodeId);
                Assert.That(baked.matchedTargetNodeIds.Length, Is.EqualTo(2));
                Assert.That(baked.matchedTargetNodeIds, Does.Contain(extraTarget.nodeId));
                Assert.That(spec.relations.Count(relation =>
                    relation.fromNodeId == branch.nodeId
                    && relation.fromPortStableKey == ESAgentGraphStableIds.BranchMatchedPortKey),
                    Is.EqualTo(2));
                Assert.That(spec.relations.All(relation =>
                    ESGraphEndpointRules.IsValidMeaning(relation.fromPortMeaning)
                    && ESGraphEndpointRules.IsValidMeaning(relation.toPortMeaning)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_ProfileBakesStrongTypedGenerationSpec()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                var profile = new ESAgentAuthoringGraphProfile();
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                profile.Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                Assert.That(spec.DomainId, Is.EqualTo(ESAgentGraphStableIds.DomainId));
                Assert.That(spec.sourceGraphId, Is.EqualTo(graph.GraphId));
                Assert.That(spec.sourceOriginGraphId, Is.EqualTo(graph.OriginGraphId));
                Assert.That(spec.goal.objective, Does.Contain("Graph"));
                Assert.That(spec.references.Length, Is.EqualTo(1));
                Assert.That(spec.references.Single().projectPath,
                    Is.EqualTo("Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md"));
                Assert.That(spec.constraints.Length, Is.EqualTo(1));
                Assert.That(spec.outputs.Single().nodeId, Is.EqualTo(outputNode.nodeId));
                Assert.That(spec.outputs.Single().artifactKind, Is.EqualTo(ESAgentArtifactKind.AICommand));
                Assert.That(spec.outputs.Single().artifactId,
                    Is.EqualTo("es." + graph.GraphId + "." + outputNode.nodeId));
                Assert.That(spec.outputs.Single().operationMode,
                    Is.EqualTo(ESAgentArtifactOperationMode.CreateOrUpdate));
                Assert.That(spec.outputs.Single().commandIntent,
                    Is.EqualTo(ESAgentCommandIntent.ControlledExecution));
                Assert.That(spec.outputs.Single().writeAuthorization,
                    Is.EqualTo(ESAgentWriteAuthorization.ConfirmBeforeWrite));
                Assert.That(spec.outputs.Single().commandRiskLevel, Is.EqualTo(ESAgentRiskLevel.L2));
                Assert.That(spec.outputs.Single().preconditions, Is.Not.Empty);
                Assert.That(spec.outputs.Single().forbiddenOperations, Is.Not.Empty);
                Assert.That(spec.outputs.Single().requiredEvidence, Is.Not.Empty);
                Assert.That(spec.outputs.Single().rollbackStrategy, Is.Not.Empty);
                Assert.That(spec.validations.Single().requireHumanApproval, Is.True);
                Assert.That(spec.skillBundle, Is.Not.Null);
                Assert.That(spec.skillBundle.kind, Is.EqualTo(ESAgentSkillBundleKind.CommandOnly));
                Assert.That(spec.skillBundle.bundleId, Does.StartWith("es."));
                Assert.That(spec.skillBundle.goalNodeId, Is.EqualTo(spec.goal.nodeId));
                Assert.That(spec.skillBundle.commandOutputNodeIds,
                    Does.Contain(outputNode.nodeId));
                Assert.That(spec.skillBundle.aiSkillOutputNodeIds, Is.Empty);
                Assert.That(spec.skillBundle.validationNodeIds,
                    Does.Contain(spec.validations.Single().nodeId));
                Assert.That(spec.relations.Length, Is.EqualTo(4));
                Assert.That(spec.relations.Select(item => item.semanticType), Is.EquivalentTo(new[]
                {
                    ESAgentGraphStableIds.ContextPort,
                    ESAgentGraphStableIds.ContextPort,
                    ESAgentGraphStableIds.RequirementPort,
                    ESAgentGraphStableIds.ArtifactPort
                }));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_SkillBundleRejectsDriftedPairedOutputs()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> issues), Is.True, Describe(issues));

                spec.skillBundle.aiSkillOutputNodeIds = Array.Empty<string>();
                Assert.That(ESAgentGenerationIntentValidator.TryValidate(spec, out string error), Is.False);
                Assert.That(error, Does.Contain("类型与 AICommand/AISkill 组成不一致"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_CurrentContractRejectsMissingSkillBundle()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> issues), Is.True, Describe(issues));

                spec.skillBundle = null;
                Assert.That(ESAgentGenerationIntentValidator.TryValidate(spec, out string error), Is.False);
                Assert.That(error, Does.Contain("缺少 AICommand + AISkill 共享 Skill 能力包合同"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_FinalPurposeIsUniqueAndMandatory()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESAgentAuthoringGraphValidator.TryGetFinalPurpose(graph,
                    out string purpose, out string success), Is.True);
                Assert.That(purpose, Does.Contain("Graph"));
                Assert.That(success, Is.Not.Empty);

                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.GoalNode);
                var payload = new ESAgentGoalPayload { title = "", objective = "", successCriteria = "" };
                graph.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title,
                    JsonUtility.ToJson(payload), out _);
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.Goal.Title"), Is.True);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.Goal.Objective"), Is.True);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.Goal.SuccessCriteria"), Is.True);
                Assert.That(ESAgentAuthoringGraphValidator.TryGetFinalPurpose(graph, out _, out _), Is.False);

                Assert.That(graph.RemoveNode(goal.nodeId), Is.True);
                issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.GoalCount"
                    && issue.message.Contains("当前为 0 个")), Is.True);

                AddFromProfile(graph, ESAgentGraphStableIds.GoalNode, new Vector2(0f, 0f));
                AddFromProfile(graph, ESAgentGraphStableIds.GoalNode, new Vector2(0f, 240f));
                issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.GoalCount"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_SemanticGateRejectsTemplateAndUnrelatedOutput()
        {
            var template = new ESAgentArtifactGenerationSpec
            {
                goal = new ESAgentGenerationGoal
                {
                    title = "审查字体资产工具",
                    objective = "描述希望 AICommand 或 Agent Skill 解决的问题。",
                    successCriteria = "生成结果可读、可验证、权限边界明确，并能通过人工 Diff Review。"
                },
                outputs = new[]
                {
                    new ESAgentGenerationOutput
                    {
                        artifactName = "生成_新模块工作流_AI命令",
                        requirements = "描述该 AICommand 要授权和约束的单次任务。"
                    }
                }
            };
            Assert.That(ESAgentGenerationSemanticValidator.TryValidate(template, out string templateError),
                Is.False);
            Assert.That(templateError, Does.Contain("模板/占位"));

            template.goal.objective = "审查字体资产工具的字体导入、预览和缺失引用。";
            template.goal.successCriteria = "字体资产问题均能定位并给出修复证据。";
            template.outputs[0].artifactName = "生成_新模块实现_AI命令";
            template.outputs[0].requirements = "实现通用模块并运行编译验证。";
            Assert.That(ESAgentGenerationSemanticValidator.TryValidate(template, out string mismatchError),
                Is.False);
            Assert.That(mismatchError, Does.Contain("Goal 标题"));
        }

        [Test]
        public void AgentAuthoring_UserMayForceSemanticRiskButNotPathViolation()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.GoalNode);
                var goalPayload = new ESAgentGoalPayload
                {
                    title = "审查字体资产工具",
                    objective = "审查字体导入、预览和缺失引用。",
                    successCriteria = "字体资产问题可以定位并给出修复证据。"
                };
                Assert.That(graph.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title,
                    JsonUtility.ToJson(goalPayload), out string updateError), Is.True, updateError);

                List<ESGraphValidationIssue> strictIssues = ESGraphAuthoringRegistry.Validate(graph);
                ESGraphValidationIssue semantic = strictIssues.Single(issue =>
                    issue?.code == "AgentAuthoring.SemanticAlignment");
                Assert.That(semantic.canForceContinue, Is.True);
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out _, out _, out strictIssues), Is.False);
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, true,
                    out ESBakedGraphSnapshot snapshot, out IESBakedGraphPlan plan,
                    out List<ESGraphValidationIssue> forcedIssues), Is.True, Describe(forcedIssues));
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(plan, Is.TypeOf<ESAgentArtifactGenerationSpec>());

                ESAgentAICommandOutputPayload output = JsonUtility
                    .FromJson<ESAgentAICommandOutputPayload>(outputNode.payloadJson);
                output.targetProjectPath = "../outside.md";
                Assert.That(graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version,
                    outputNode.title, JsonUtility.ToJson(output), out updateError), Is.True, updateError);
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, true,
                    out _, out _, out List<ESGraphValidationIssue> blockedIssues), Is.False);
                Assert.That(blockedIssues.Any(issue => issue?.code == "AgentAuthoring.OutputPath"
                    && !issue.canForceContinue), Is.True, Describe(blockedIssues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_UserActionCancelPromptsOnceAndDoesNotBake()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out _);
            try
            {
                int confirmationCount = 0;
                bool succeeded = ESGraphUserActionBaker.TryBake(graph, "测试取消", null, null,
                    (action, issues) =>
                    {
                        confirmationCount++;
                        Assert.That(action, Is.EqualTo("测试取消"));
                        Assert.That(ESGraphAuthoringRegistry.CanForceContinue(issues), Is.True);
                        return false;
                    }, "editor:test", out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan plan, out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues);

                Assert.That(succeeded, Is.False, Describe(issues));
                Assert.That(confirmationCount, Is.EqualTo(1));
                Assert.That(snapshot, Is.Null);
                Assert.That(plan, Is.Null);
                Assert.That(acceptance, Is.Null);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_UserActionContinueBakesOnceWithValidRiskReceipt()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out _);
            try
            {
                int confirmationCount = 0;
                bool succeeded = ESGraphUserActionBaker.TryBake(graph, "测试继续", null, null,
                    (_, issues) =>
                    {
                        confirmationCount++;
                        Assert.That(ESGraphAuthoringRegistry.CanForceContinue(issues), Is.True);
                        return true;
                    }, "editor:test", out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan plan, out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues);

                Assert.That(succeeded, Is.True, Describe(issues));
                Assert.That(confirmationCount, Is.EqualTo(1));
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(acceptance, Is.Not.Null);
                Assert.That(acceptance.acceptanceHash, Has.Length.EqualTo(64));
                Assert.That(acceptance.acceptedBy, Is.EqualTo("editor:test"));
                Assert.That(acceptance.TryValidate(snapshot.GraphId, snapshot.ContentSignature,
                    issues, out string acceptanceError), Is.True, acceptanceError);
                ESAgentArtifactGenerationSpec spec = plan as ESAgentArtifactGenerationSpec;
                Assert.That(spec, Is.Not.Null);
                Assert.That(spec.riskAcceptance.SameAs(acceptance), Is.True);
                Assert.That(ESAgentGenerationRiskValidator.TryValidate(spec, out string riskError),
                    Is.True, riskError);
                Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(spec,
                    ESAgentArtifactKind.AICommand, out ESAgentArtifactGenerationSpec artifactView,
                    out string viewError), Is.True, viewError);
                Assert.That(artifactView.riskAcceptance.SameAs(acceptance), Is.True);
                Assert.That(ESAgentGenerationRiskValidator.TryValidate(artifactView,
                    out string viewRiskError), Is.True, viewRiskError);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_UserConfirmationCannotBypassHardPathError()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out ESGraphNodeRecord outputNode);
            try
            {
                ESAgentAICommandOutputPayload output = JsonUtility
                    .FromJson<ESAgentAICommandOutputPayload>(outputNode.payloadJson);
                output.targetProjectPath = "../outside.md";
                Assert.That(graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version,
                    outputNode.title, JsonUtility.ToJson(output), out string updateError), Is.True,
                    updateError);
                int confirmationCount = 0;

                bool succeeded = ESGraphUserActionBaker.TryBake(graph, "测试硬门禁", null, null,
                    (_, __) =>
                    {
                        confirmationCount++;
                        return true;
                    }, "editor:test", out _, out _, out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues);

                Assert.That(succeeded, Is.False);
                Assert.That(confirmationCount, Is.EqualTo(1));
                Assert.That(acceptance, Is.Null);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.OutputPath"
                    && !issue.canForceContinue), Is.True, Describe(issues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RiskReceiptRejectsTamperingAndContextDrift()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out ESGraphNodeRecord outputNode);
            try
            {
                Assert.That(ESGraphUserActionBaker.TryBake(graph, "创建风险收据", null, null,
                    (_, __) => true, "editor:test", out ESBakedGraphSnapshot snapshot,
                    out _, out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));

                ESGraphRiskAcceptance tampered = JsonUtility.FromJson<ESGraphRiskAcceptance>(
                    JsonUtility.ToJson(acceptance));
                tampered.acceptedBy = "editor:other";
                Assert.That(tampered.TryValidateStored(snapshot.GraphId, snapshot.ContentSignature,
                    out string tamperError), Is.False);
                Assert.That(tamperError, Does.Contain("SHA-256"));
                Assert.That(acceptance.SameAs(tampered), Is.False);

                Assert.That(acceptance.TryValidate(snapshot.GraphId, snapshot.ContentSignature,
                    Array.Empty<ESGraphValidationIssue>(), out string issueDriftError), Is.False);
                Assert.That(issueDriftError, Does.Contain("没有需要风险确认"));

                Assert.That(graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version,
                    outputNode.title + " 已修改", outputNode.payloadJson, out string updateError),
                    Is.True, updateError);
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, true,
                    out ESBakedGraphSnapshot changedSnapshot, out _,
                    out List<ESGraphValidationIssue> changedIssues), Is.True, Describe(changedIssues));
                Assert.That(acceptance.TryValidate(changedSnapshot.GraphId,
                    changedSnapshot.ContentSignature, changedIssues, out string signatureError), Is.False);
                Assert.That(signatureError, Does.Contain("内容签名"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Automation_DryRunPersistsRiskReceiptInRunRecord()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out _);
            string runDirectory = string.Empty;
            try
            {
                Assert.That(ESGraphUserActionBaker.TryBake(graph, "创建派发收据", null, null,
                    (_, __) => true, "editor:test", out ESBakedGraphSnapshot snapshot,
                    out _, out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));

                ESAutomationTaskInvocationResult result = ESAgentGraphAutomation.Dispatch(
                    ESAgentGraphAutomation.UseTaskId, "risk-record-test", string.Empty,
                    snapshot.GraphId, snapshot.ContentSignature, "single-use", "risk record test",
                    acceptance, "editor:test", true);
                Assert.That(result.status, Is.EqualTo("Completed"), result.message);
                Assert.That(result.runId, Is.Not.Empty);
                runDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, result.runId);
                string recordPath = Path.Combine(runDirectory, "run-record.json");
                Assert.That(File.Exists(recordPath), Is.True);
                ESAutomationRunRecord record = JsonUtility.FromJson<ESAutomationRunRecord>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(recordPath));
                Assert.That(record.riskPolicyVersion, Is.EqualTo(acceptance.policyVersion));
                Assert.That(record.riskAcceptanceHash, Is.EqualTo(acceptance.acceptanceHash));
                Assert.That(record.riskAcceptedBy, Is.EqualTo(acceptance.acceptedBy));
                Assert.That(record.riskAcceptedAtUtc, Is.EqualTo(acceptance.acceptedAtUtc));
                Assert.That(record.acceptedRiskCodes, Is.EquivalentTo(acceptance.issueCodes));
                Assert.That(record.outputs, Has.Count.EqualTo(1));
                Assert.That(record.outputs[0], Does.EndWith("agent-graph-dispatch.json"));
                Assert.That(record.outputHashes, Has.Count.EqualTo(record.outputs.Count));
                Assert.That(record.outputHashes[0], Has.Length.EqualTo(64));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(runDirectory) && Directory.Exists(runDirectory))
                    ESManagedFileIO.DeleteDirectory(runDirectory, ESAutomationPathPolicy.TempRoot);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Automation_RunningEventCanProvideFirstAcceptanceReceipt()
        {
            string runId = Guid.NewGuid().ToString("N");
            string runDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, runId);
            string recordPath = Path.Combine(runDirectory, "run-record.json");
            string acceptanceHash = new string('a', 64);
            try
            {
                Directory.CreateDirectory(runDirectory);
                ESAgentArtifactGenerationWorkspace.WriteUtf8(recordPath,
                    JsonUtility.ToJson(new ESAutomationRunRecord
                    {
                        runId = runId,
                        taskId = ESAgentGraphAutomation.UseTaskId,
                        taskVersion = 1,
                        status = ESAutomationRunStatus.Starting,
                        startedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        riskAcceptanceHash = acceptanceHash,
                    }, true));
                ESAgentGraphAutomation.InitializeForEditor();

                ESCmdAgentPromptLifecycle.Publish(new ESCmdAgentPromptLifecycleEvent(runId,
                    ESCmdAgentPromptLifecycleState.Running, "session-test", runDirectory,
                    "turn.started", "Codex turn 已开始。"));

                ESAutomationRunRecord record = JsonUtility.FromJson<ESAutomationRunRecord>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(recordPath));
                Assert.That(record.status, Is.EqualTo(ESAutomationRunStatus.Running));
                Assert.That(record.finishedAtUtc, Is.Empty);
                string receiptPath = Path.Combine(runDirectory, "dispatch-receipt.json");
                Assert.That(File.Exists(receiptPath), Is.True);
                string receipt = ESAgentArtifactGenerationWorkspace.ReadUtf8(receiptPath);
                Assert.That(receipt, Does.Contain("turn.started"));
                Assert.That(receipt, Does.Contain(acceptanceHash));
                Assert.That(record.outputs.Any(item => item.EndsWith("dispatch-receipt.json",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(record.outputHashes, Has.Count.EqualTo(record.outputs.Count));
            }
            finally
            {
                if (Directory.Exists(runDirectory))
                    ESManagedFileIO.DeleteDirectory(runDirectory, ESAutomationPathPolicy.TempRoot);
            }
        }

        [Test]
        public void Automation_CompletedCandidateBindsValidatedFilesAndHashesToRunRecord()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            string requestDirectory = ESAgentArtifactGenerationWorkspace.CandidateRoot
                + "/test_run_output_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            string candidateRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                ESAgentArtifactGenerationWorkspace.CandidateRoot);
            string runId = Guid.NewGuid().ToString("N");
            string runDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, runId);
            try
            {
                Assert.That(TryBakeSpec(graph, out ESAgentArtifactGenerationSpec spec,
                    out string bakeError), Is.True, bakeError);
                Directory.CreateDirectory(requestFull);
                WriteRequest(requestFull, "run-output-request", requestDirectory, spec);
                Assert.That(ESAgentArtifactGenerationWorkspace.TryReadRequest(requestDirectory,
                    out ESAgentArtifactGenerationRequest request, out string requestError),
                    Is.True, requestError);
                WriteValidAICommandCandidate(requestFull, request);

                Directory.CreateDirectory(runDirectory);
                ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(runDirectory,
                    "agent-graph-dispatch.json"), JsonUtility.ToJson(new TestGraphDispatchEnvelope
                    {
                        runId = runId,
                        requestId = request.requestId,
                        graphId = spec.sourceGraphId,
                        contentSignature = spec.SourceContentSignature,
                        requestDirectory = requestDirectory,
                        operationKind = "candidate"
                    }, true));
                var record = new ESAutomationRunRecord
                {
                    runId = runId,
                    taskId = ESAgentGraphAutomation.GenerateTaskId,
                    status = ESAutomationRunStatus.Running
                };
                MethodInfo capture = typeof(ESAgentGraphAutomation).GetMethod(
                    "TryCaptureCandidateOutputs", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(capture, Is.Not.Null);
                object[] arguments = { runDirectory, record, null };
                Assert.That((bool)capture.Invoke(null, arguments), Is.True, arguments[2] as string);
                Assert.That(record.outputs, Has.Count.EqualTo(3));
                Assert.That(record.outputs.Any(path => path.EndsWith("candidate-manifest.json",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(record.outputs.Any(path => path.EndsWith("validation-report.md",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(record.outputs.Any(path => path.EndsWith("candidate/command.md",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(record.outputHashes, Has.Count.EqualTo(record.outputs.Count));
                Assert.That(record.outputHashes.All(hash => hash != null && hash.Length == 64), Is.True);
            }
            finally
            {
                if (Directory.Exists(requestFull))
                    ESManagedFileIO.DeleteDirectory(requestFull, candidateRoot);
                if (Directory.Exists(runDirectory))
                    ESManagedFileIO.DeleteDirectory(runDirectory, ESAutomationPathPolicy.TempRoot);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Automation_CandidateDispatchRejectsPromptDrift()
        {
            ESGraphAssetBase graph = CreateSemanticRiskGraph(out _);
            string requestDirectory = string.Empty;
            try
            {
                Assert.That(ESGraphUserActionBaker.TryBake(graph, "创建候选请求", null, null,
                    (_, __) => true, "editor:test", out _, out IESBakedGraphPlan plan,
                    out ESGraphRiskAcceptance acceptance,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));
                ESAgentArtifactGenerationSpec spec = plan as ESAgentArtifactGenerationSpec;
                Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateRequest(spec,
                    out ESAgentArtifactGenerationRequest request, out string prompt,
                    out string requestError), Is.True, requestError);
                requestDirectory = request.requestDirectory;

                ESAutomationTaskInvocationResult result = ESAgentGraphAutomation.Dispatch(
                    ESAgentGraphAutomation.GenerateTaskId, request.requestId, request.requestDirectory,
                    spec.sourceGraphId, spec.SourceContentSignature, "candidate", prompt + "\n篡改",
                    acceptance, "editor:test", true);

                Assert.That(result.status, Is.EqualTo("Rejected"));
                Assert.That(result.message, Does.Contain("Prompt 不一致"));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(requestDirectory))
                {
                    string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
                    string candidateRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                        ESAgentArtifactGenerationWorkspace.CandidateRoot);
                    if (Directory.Exists(requestFull))
                        ESManagedFileIO.DeleteDirectory(requestFull, candidateRoot);
                }
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_CoreFieldsUseSimpleESFieldContract()
        {
            Assert.That(Enum.GetNames(typeof(ESFieldLevel)),
                Is.EqualTo(new[] { "Normal", "Important", "Core" }));

            ESFieldAttribute defaults = typeof(ESFieldOptionalArgumentsFixture)
                .GetProperty(nameof(ESFieldOptionalArgumentsFixture.Defaults))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute levelOnly = typeof(ESFieldOptionalArgumentsFixture)
                .GetProperty(nameof(ESFieldOptionalArgumentsFixture.LevelOnly))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute hintOnly = typeof(ESFieldOptionalArgumentsFixture)
                .GetProperty(nameof(ESFieldOptionalArgumentsFixture.HintOnly))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute requiredOnly = typeof(ESFieldOptionalArgumentsFixture)
                .GetProperty(nameof(ESFieldOptionalArgumentsFixture.RequiredOnly))
                ?.GetCustomAttribute<ESFieldAttribute>();

            Assert.That(defaults, Is.Not.Null);
            Assert.That(defaults.Level, Is.EqualTo(ESFieldLevel.Normal));
            Assert.That(defaults.Required, Is.False);
            Assert.That(defaults.Hint, Is.Null);
            Assert.That(levelOnly?.Level, Is.EqualTo(ESFieldLevel.Important));
            Assert.That(levelOnly?.Required, Is.False);
            Assert.That(hintOnly?.Level, Is.EqualTo(ESFieldLevel.Normal));
            Assert.That(hintOnly?.Hint, Is.EqualTo("只填写提示"));
            Assert.That(requiredOnly?.Level, Is.EqualTo(ESFieldLevel.Normal));
            Assert.That(requiredOnly?.Required, Is.True);

            ESFieldAttribute commandName = typeof(ESAgentAICommandOutputPayload)
                .GetField(nameof(ESAgentAICommandOutputPayload.commandName))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute writeScope = typeof(ESAgentAICommandOutputPayload)
                .GetField(nameof(ESAgentAICommandOutputPayload.allowedWriteScopes))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute skillBoundary = typeof(ESAgentSkillOutputPayload)
                .GetField(nameof(ESAgentSkillOutputPayload.permissionBoundary))
                ?.GetCustomAttribute<ESFieldAttribute>();
            ESFieldAttribute approval = typeof(ESAgentValidationPayload)
                .GetField(nameof(ESAgentValidationPayload.requireHumanApproval))
                ?.GetCustomAttribute<ESFieldAttribute>();

            foreach (ESFieldAttribute field in new[] { commandName, writeScope, skillBoundary, approval })
            {
                Assert.That(field, Is.Not.Null);
                Assert.That(field.Level, Is.EqualTo(ESFieldLevel.Core));
                Assert.That(field.Required, Is.True);
                Assert.That(field.Hint, Is.Not.Empty);
            }
        }

        [Test]
        public void AgentAuthoring_FieldPresentationMetadataIsCachedAndStable()
        {
            IReadOnlyList<ESFieldPresentationMetadata> first
                = ESFieldPresentationMetadataCache.GetSummaryFields(
                    typeof(ESAgentAICommandOutputPayload));
            IReadOnlyList<ESFieldPresentationMetadata> second
                = ESFieldPresentationMetadataCache.GetSummaryFields(
                    typeof(ESAgentAICommandOutputPayload));

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.Count, Is.GreaterThan(0));
            Assert.That(ESFieldPresentationMetadataCache.TryGet(
                typeof(ESAgentAICommandOutputPayload),
                nameof(ESAgentAICommandOutputPayload.allowedWriteScopes),
                out ESFieldPresentationMetadata writeScope), Is.True);
            Assert.That(writeScope.Level, Is.EqualTo(ESFieldLevel.Core));
            Assert.That(writeScope.Required, Is.True);
            Assert.That(writeScope.Hint, Is.Not.Empty);
            Assert.That(ESFieldPresentationMetadataCache.TryGet(
                typeof(ESAgentAICommandOutputPayload), "missingField", out _), Is.False);
        }

        [Test]
        public void AgentAuthoring_GraphAutomationTasksAreDiscoverable()
        {
            ESAgentGraphAutomation.InitializeForEditor();
            Assert.That(ESAutomationFacade.TryGetDescriptor(
                ESAgentGraphAutomation.GenerateTaskId, 1, out ESAutomationTaskDescriptor generate), Is.True);
            Assert.That(generate.displayName, Does.Contain("候选"));
            Assert.That(ESAutomationFacade.TryGetDescriptor(
                ESAgentGraphAutomation.UseTaskId, 1, out ESAutomationTaskDescriptor use), Is.True);
            Assert.That(use.displayName, Does.Contain("单次"));
            Assert.That(ESAutomationTaskRegistry.TryGet(
                ESAgentGraphAutomation.GenerateTaskId, 1, out ESAutomationTaskContract contract), Is.True);
            Assert.That(contract.worker.entrypointHash, Has.Length.EqualTo(64));
            Assert.That(contract.ResolveCapabilities() & ESAutomationCapability.WriteAssets,
                Is.EqualTo(ESAutomationCapability.None));
        }

        [Test]
        public void Automation_StartIsNotAcceptance()
        {
            var result = new ESCmdAgentPromptDispatchResult(
                ESCmdAgentPromptDispatchState.Starting, "已提交受管会话请求，等待上下文验收。");
            Assert.That(result.IsStarting, Is.True);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.IsDispatched, Is.False);
        }

        [Test]
        public void Automation_RunStatusMachineRejectsFalseCompletion()
        {
            var record = new ESAutomationRunRecord
            {
                status = ESAutomationRunStatus.Created,
                startedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
            Assert.That(ESAutomationRunStatus.TryTransition(
                ESAutomationRunStatus.Created, ESAutomationRunStatus.Starting), Is.True);
            Assert.That(ESAutomationRunStatus.TryTransition(
                ESAutomationRunStatus.Starting, ESAutomationRunStatus.Completed), Is.False);
            ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Starting);
            Assert.That(record.finishedAtUtc, Is.Empty);
            Assert.Throws<InvalidOperationException>(() =>
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Completed));
        }

        [Test]
        public void Automation_FacadeRejectsGraphPathOutsideTaskContract()
        {
            ESAgentGraphAutomation.InitializeForEditor();
            ESAutomationTaskInvocationResult result = ESAgentGraphAutomation.Dispatch(
                ESAgentGraphAutomation.GenerateTaskId, "contract-gate-test", Path.Combine(
                    ESAutomationPathPolicy.ProjectRoot, "Assets"), string.Empty, string.Empty,
                "candidate", "contract gate test", null, "editor.user");
            Assert.That(result.status, Is.EqualTo("Rejected"));
            Assert.That(result.message, Does.Contain("Contract 门禁"));
        }

        [Test]
        public void AgentAuthoring_FieldStyleIsIdempotent()
        {
            var field = new VisualElement { tooltip = "原始提示" };
            var label = new Label("权限边界");

            ESEditorPresentation.StyleField(field, label, ESFieldLevel.Core,
                true, false, "字段说明");
            string firstLabel = label.text;
            string firstTooltip = field.tooltip;
            ESEditorPresentation.StyleField(field, label, ESFieldLevel.Core,
                true, false, "字段说明");

            Assert.That(label.text, Is.EqualTo(firstLabel));
            Assert.That(label.text, Is.EqualTo("核心 · 权限边界 *"));
            Assert.That(field.tooltip, Is.EqualTo(firstTooltip));
            Assert.That(field.tooltip, Is.EqualTo("核心 · 必填\n字段说明\n原始提示"));
        }

        [Test]
        public void AgentAuthoring_SemanticGateAcceptsGoalAlignedOutput()
        {
            var spec = new ESAgentArtifactGenerationSpec
            {
                goal = new ESAgentGenerationGoal
                {
                    title = "审查字体资产工具",
                    objective = "审查字体导入、预览和缺失引用。",
                    successCriteria = "字体资产问题均能定位并给出修复证据。"
                },
                outputs = new[]
                {
                    new ESAgentGenerationOutput
                    {
                        artifactName = "审查_字体资产工具_AI命令",
                        requirements = "检查字体资产工具的导入与引用状态。",
                        acceptanceCriteria = "字体预览、引用和缺失问题均有真实证据。"
                    }
                }
            };
            Assert.That(ESAgentGenerationSemanticValidator.TryValidate(spec, out string error),
                Is.True, error);
        }

        [Test]
        public void AgentAuthoring_CheckSnapshotPersistsStableJsonArtifact()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            string relativePath = string.Empty;
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));
                Assert.That(ESAgentArtifactGenerationWorkspace.TryWriteGraphSnapshot(snapshot,
                    out relativePath, out string error), Is.True, error);
                string fullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(relativePath);
                Assert.That(File.Exists(fullPath), Is.True);
                ESGraphSnapshotArtifact artifact = JsonUtility.FromJson<ESGraphSnapshotArtifact>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(fullPath));
                Assert.That(artifact.graphId, Is.EqualTo(snapshot.GraphId));
                Assert.That(artifact.contentSignature, Is.EqualTo(snapshot.ContentSignature));
                Assert.That(artifact.nodes.Length, Is.EqualTo(snapshot.Nodes.Count));
                Assert.That(artifact.edges.Length, Is.EqualTo(snapshot.Edges.Count));
                Assert.That(artifact.edges.Select(edge => edge.order), Is.Ordered);
                Assert.That(artifact.routes.Select(route => route.order), Is.Ordered);
                Assert.That(artifact.routes.Select(route => route.order),
                    Is.EqualTo(artifact.edges.Select(edge => edge.order)));
            }
            finally
            {
                if (!string.IsNullOrEmpty(relativePath))
                {
                    string fullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(relativePath);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    string graphDirectory = Path.GetDirectoryName(fullPath);
                    string snapshotRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                        ESAgentArtifactGenerationWorkspace.SnapshotRoot);
                    if (!string.IsNullOrEmpty(graphDirectory) && Directory.Exists(graphDirectory)
                        && !Directory.EnumerateFileSystemEntries(graphDirectory).Any())
                        ESManagedFileIO.DeleteDirectory(graphDirectory, snapshotRoot);
                }
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_RequestLookupIsBoundToGraphAndContentSignature()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            string directoryA = ESAgentArtifactGenerationWorkspace.CandidateRoot + "/test_" + suffix + "_a";
            string directoryB = ESAgentArtifactGenerationWorkspace.CandidateRoot + "/test_" + suffix + "_b";
            string fullA = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(directoryA);
            string fullB = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(directoryB);
            string candidateRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                ESAgentArtifactGenerationWorkspace.CandidateRoot);
            try
            {
                Assert.That(TryBakeSpec(graph, out ESAgentArtifactGenerationSpec specA,
                    out string bakeError), Is.True, bakeError);
                ESAgentArtifactGenerationSpec specB = JsonUtility.FromJson<ESAgentArtifactGenerationSpec>(
                    JsonUtility.ToJson(specA));
                specB.sourceContentSignature = new string('b', 64);
                Directory.CreateDirectory(fullA);
                Directory.CreateDirectory(fullB);
                WriteRequest(fullA, "request-a", directoryA, specA);
                WriteRequest(fullB, "request-b", directoryB, specB);
                ESAgentArtifactGenerationSpec current = specA;
                Assert.That(ESAgentArtifactGenerationWorkspace.TryGetRequestDirectory(current,
                    out string matched), Is.True);
                Assert.That(matched, Is.EqualTo(directoryA));
                ESAgentArtifactRequestStatus status =
                    ESAgentArtifactGenerationWorkspace.GetRequestStatus(current);
                Assert.That(status.State, Is.EqualTo(ESAgentArtifactRequestState.AwaitingCandidate));
                Assert.That(status.RequestDirectory, Is.EqualTo(directoryA));
                ESAgentArtifactGenerationRequest requestA;
                Assert.That(ESAgentArtifactGenerationWorkspace.TryReadRequest(directoryA,
                    out requestA, out string readError), Is.True, readError);
                WriteValidAICommandCandidate(fullA, requestA);
                status = ESAgentArtifactGenerationWorkspace.GetRequestStatus(current);
                Assert.That(status.State, Is.EqualTo(ESAgentArtifactRequestState.AwaitingApproval));
                ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(fullA, "approval-manifest.json"),
                    JsonUtility.ToJson(new ESAgentArtifactApprovalManifest
                    {
                        requestId = "request-a",
                        sourceGraphId = specA.sourceGraphId,
                        sourceContentSignature = specA.SourceContentSignature
                    }, true));
                status = ESAgentArtifactGenerationWorkspace.GetRequestStatus(current);
                Assert.That(status.State, Is.EqualTo(ESAgentArtifactRequestState.Invalid));
                Assert.That(status.Message, Does.Contain("批准清单"));

                current.sourceContentSignature = new string('c', 64);
                Assert.That(ESAgentArtifactGenerationWorkspace.TryGetRequestDirectory(current,
                    out _), Is.False);
                status = ESAgentArtifactGenerationWorkspace.GetRequestStatus(current);
                Assert.That(status.State, Is.EqualTo(ESAgentArtifactRequestState.Stale));
                Assert.That(status.Message, Does.Contain("内容已变化"));
            }
            finally
            {
                if (Directory.Exists(fullA)) ESManagedFileIO.DeleteDirectory(fullA, candidateRoot);
                if (Directory.Exists(fullB)) ESManagedFileIO.DeleteDirectory(fullB, candidateRoot);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_RequestReaderRejectsLegacyContractVersion()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            string relative = ESAgentArtifactGenerationWorkspace.CandidateRoot + "/test_legacy_"
                + Guid.NewGuid().ToString("N").Substring(0, 8);
            string full = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(relative);
            string candidateRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                ESAgentArtifactGenerationWorkspace.CandidateRoot);
            try
            {
                Assert.That(TryBakeSpec(graph, out ESAgentArtifactGenerationSpec spec,
                    out string bakeError), Is.True, bakeError);
                spec.contractSchemaVersion = ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion - 1;
                Directory.CreateDirectory(full);
                WriteRequest(full, "legacy-request", relative, spec);

                Assert.That(ESAgentArtifactGenerationWorkspace.TryReadRequest(relative,
                    out _, out string error), Is.False);
                Assert.That(error, Does.Contain("旧版 Graph 语义合同"));
            }
            finally
            {
                if (Directory.Exists(full)) ESManagedFileIO.DeleteDirectory(full, candidateRoot);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_CandidateMustCoverBranchTraversalAndValidationSemantics()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            string relative = ESAgentArtifactGenerationWorkspace.CandidateRoot + "/test_semantics_"
                + Guid.NewGuid().ToString("N").Substring(0, 8);
            string full = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(relative);
            string candidateRoot = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                ESAgentArtifactGenerationWorkspace.CandidateRoot);
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                Assert.That(TryBakeSpec(graph, out ESAgentArtifactGenerationSpec wholeSpec,
                    out string bakeError), Is.True, bakeError);
                string commandNodeId = wholeSpec.outputs.Single(output =>
                    output.artifactKind == ESAgentArtifactKind.AICommand).nodeId;
                Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(wholeSpec,
                    commandNodeId, out ESAgentArtifactGenerationSpec spec, out string viewError),
                    Is.True, viewError);
                Directory.CreateDirectory(full);
                WriteRequest(full, "semantic-request", relative, spec);
                Assert.That(ESAgentArtifactGenerationWorkspace.TryReadRequest(relative,
                    out ESAgentArtifactGenerationRequest request, out string readError), Is.True, readError);
                ESAgentArtifactCandidateManifest manifest = WriteValidAICommandCandidate(full, request);

                List<string> errors = ESAgentArtifactCandidateValidator.Validate(relative, request, manifest);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));

                string candidatePath = Path.Combine(full,
                    manifest.files[0].candidateRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string validText = ESAgentArtifactGenerationWorkspace.ReadUtf8(candidatePath);
                ESAgentGenerationBranch branch = spec.branches.Single();
                ESAgentArtifactGenerationWorkspace.WriteUtf8(candidatePath,
                    validText.Replace(branch.condition, string.Empty));
                errors = ESAgentArtifactCandidateValidator.Validate(relative, request, manifest);
                Assert.That(errors.Any(error => error.Contains("Branch 条件")), Is.True,
                    string.Join("\n", errors));

                ESAgentGenerationTraversal traversal = spec.traversals.Single();
                ESAgentArtifactGenerationWorkspace.WriteUtf8(candidatePath,
                    validText.Replace("maxDepth=" + traversal.maxDepth, string.Empty));
                errors = ESAgentArtifactCandidateValidator.Validate(relative, request, manifest);
                Assert.That(errors.Any(error => error.Contains("Traversal 最大深度")), Is.True,
                    string.Join("\n", errors));

                ESAgentArtifactGenerationWorkspace.WriteUtf8(candidatePath, validText);
                File.Delete(Path.Combine(full, "validation-report.md"));
                errors = ESAgentArtifactCandidateValidator.Validate(relative, request, manifest);
                Assert.That(errors.Any(error => error.Contains("validation-report.md")), Is.True,
                    string.Join("\n", errors));
            }
            finally
            {
                if (Directory.Exists(full)) ESManagedFileIO.DeleteDirectory(full, candidateRoot);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_ImmediatePromptAndAllCopyFormatsPreserveChineseGraphContract()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));

                string immediate = ESAgentArtifactGenerationWorkspace.BuildImmediateExecutionPrompt(spec, "run-test");
                Assert.That(immediate, Does.Contain("最终目的"));
                Assert.That(immediate, Does.Contain("成功标准"));
                Assert.That(immediate, Does.Contain(graph.GraphId));
                Assert.That(immediate, Does.Contain("不要把本次动作误解为生成永久 AICommand/Agent Skill"));

                foreach (ESAgentGraphCopyFormat format in new[]
                {
                    ESAgentGraphCopyFormat.ImmediateExecutionPrompt,
                    ESAgentGraphCopyFormat.ArtifactRequestJson,
                    ESAgentGraphCopyFormat.GraphMarkdown
                })
                {
                    Assert.That(ESAgentArtifactGenerationWorkspace.TryBuildCopyText(spec, format,
                        out string text, out string error), Is.True, error);
                    Assert.That(text, Does.Contain(graph.GraphId));
                    Assert.That(text, Does.Contain("Graph"));
                    Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1));
                }
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_PermanentPromptAndCandidateIdentityUseStableArtifactMarker()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                Assert.That(ESAgentArtifactGenerationWorkspace.TryPrepareArtifactOperations(spec,
                    out string prepareError), Is.True, prepareError);
                ESAgentGenerationOutput output = spec.outputs.Single();
                string marker = ESAgentArtifactGenerationWorkspace.BuildArtifactIdentityMarker(output.artifactId);
                string prompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(new ESAgentArtifactGenerationRequest
                {
                    requestId = "identity-test",
                    requestDirectory = "ES/Automation/Candidates/AgentAuthoring/identity-test",
                    candidateDirectory = "ES/Automation/Candidates/AgentAuthoring/identity-test/candidate",
                    spec = spec
                });
                Assert.That(prompt, Does.Contain(spec.sourceGraphId));
                Assert.That(prompt, Does.Contain(output.nodeId));
                Assert.That(prompt, Does.Contain(output.artifactId));
                Assert.That(prompt, Does.Contain(output.operationMode.ToString()));
                Assert.That(prompt, Does.Contain(output.resolvedOperation.ToString()));
                Assert.That(prompt, Does.Contain(marker));

                var file = new ESAgentArtifactCandidateFile
                {
                    artifactKind = ESAgentArtifactKind.AICommand,
                    targetProjectPath = output.targetProjectPath
                };
                var errors = new List<string>();
                ESAgentArtifactCandidateValidator.ValidateArtifactIdentity("# 缺少身份", file, output, errors);
                Assert.That(errors, Is.Not.Empty);
                errors.Clear();
                ESAgentArtifactCandidateValidator.ValidateArtifactIdentity(marker + "\n# 中文命令", file, output, errors);
                Assert.That(errors, Is.Empty);

                var skillOutput = new ESAgentGenerationOutput
                {
                    artifactKind = ESAgentArtifactKind.AgentSkill,
                    artifactId = "es." + graph.GraphId + ".0123456789abcdef0123456789abcdef",
                    targetProjectPath = ".agents/skills/es-identity-test/"
                };
                var skillFile = new ESAgentArtifactCandidateFile
                {
                    artifactKind = ESAgentArtifactKind.AgentSkill,
                    targetProjectPath = skillOutput.targetProjectPath + "SKILL.md"
                };
                string skillMarker = ESAgentArtifactGenerationWorkspace.BuildArtifactIdentityMarker(
                    skillOutput.artifactId);
                errors.Clear();
                ESAgentArtifactCandidateValidator.ValidateArtifactIdentity(
                    skillMarker + "\n---\nname: es-identity-test\ndescription: test\n---", skillFile,
                    skillOutput, errors);
                Assert.That(errors, Is.Not.Empty);
                errors.Clear();
                ESAgentArtifactCandidateValidator.ValidateArtifactIdentity(
                    "---\nname: es-identity-test\ndescription: test\n---\n" + skillMarker, skillFile,
                    skillOutput, errors);
                Assert.That(errors, Is.Empty);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_OperationModeBlocksMissingUpdateAndOccupiedCreate()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                ESAgentGenerationOutput output = spec.outputs.Single();
                output.operationMode = ESAgentArtifactOperationMode.UpdateOnly;
                output.artifactName = "__es_graph_missing_update__";
                output.targetProjectPath = "Assets/Plugins/ES/AICommands/__es_graph_missing_update__.md";
                Assert.That(ESAgentArtifactGenerationWorkspace.TryPrepareArtifactOperations(spec,
                    out string missingError), Is.False);
                Assert.That(missingError, Does.Contain("没有找到可更新"));

                output.operationMode = ESAgentArtifactOperationMode.CreateOnly;
                output.artifactName = "README";
                output.targetProjectPath = "Assets/Plugins/ES/AICommands/README.md";
                Assert.That(ESAgentArtifactGenerationWorkspace.TryPrepareArtifactOperations(spec,
                    out string occupiedError), Is.False);
                Assert.That(occupiedError, Does.Contain("仅创建"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RejectsRuntimeAndTraversalTargets()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                var payload = new ESAgentAICommandOutputPayload
                {
                    commandName = "Bad",
                    targetProjectPath = "Assets/Scripts/ESLogic/../Runtime/Bad.cs"
                };
                graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version,
                    outputNode.title, JsonUtility.ToJson(payload), out _);
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.OutputPath"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RequiresDiffReviewHumanApprovalAndReachability()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                ESGraphNodeRecord validation = graph.Nodes.First(node =>
                    node.typeId == ESAgentGraphStableIds.ValidationNode);
                var payload = new ESAgentValidationPayload { requireDiffReview = false, requireHumanApproval = false };
                graph.UpdateNode(validation.nodeId, validation.typeId, validation.version,
                    validation.title, JsonUtility.ToJson(payload), out _);
                AddFromProfile(graph, ESAgentGraphStableIds.ConstraintNode, new Vector2(900f, 200f));
                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.ApprovalPolicy"), Is.True);
                Assert.That(issues.Any(issue => issue?.code == "Graph.Reachability.Required"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_PathPolicySeparatesAICommandsAndAgentSkills()
        {
            Assert.That(ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand,
                "Assets/Plugins/ES/AICommands/生成_测试_AI命令.md", out _), Is.True);
            Assert.That(ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill,
                ".agents/skills/es-generated-test/", out _), Is.True);
            Assert.That(ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill,
                "Assets/Scripts/SkillDefinition.cs", out _), Is.False);
        }

        [Test]
        public void AgentAuthoring_ProfileProvidesConcreteAICommandAndAgentSkillNodes()
        {
            var profile = new ESAgentAuthoringGraphProfile();
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AICommandOutputNode), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AISkillOutputNode), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.BranchNode), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.TraverseNode), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == "es.agent-authoring.output-artifact"), Is.False);
            Assert.That(profile.NodeDefinitions.Single(item => item.NodeType.StableId == ESAgentGraphStableIds.AICommandOutputNode).CurrentVersion,
                Is.EqualTo(ESAgentAICommandOutputPayload.CurrentSchemaVersion));
            Assert.That(profile.NodeDefinitions.Single(item => item.NodeType.StableId == ESAgentGraphStableIds.AISkillOutputNode).CurrentVersion,
                Is.EqualTo(ESAgentSkillOutputPayload.CurrentSchemaVersion));
            Assert.That(profile.NodeDefinitions.Single(item => item.NodeType.StableId == ESAgentGraphStableIds.ConstraintNode).CurrentVersion,
                Is.EqualTo(ESAgentConstraintPayload.CurrentSchemaVersion));
            Assert.That(profile.NodeDefinitions.Single(item => item.NodeType.StableId
                == ESAgentGraphStableIds.BranchNode).Ports.Count, Is.EqualTo(4));
            Assert.That(profile.NodeDefinitions.Single(item => item.NodeType.StableId
                == ESAgentGraphStableIds.TraverseNode).Ports.Count, Is.EqualTo(4));
            Assert.That(profile.NodeDefinitions.Where(item =>
                    ESAgentRelationSemantics.IsSkillExecutionNode(item.NodeType.StableId))
                .All(item => item.CurrentVersion == ESAISkillExecutionNodeContractV2.Version),
                Is.True);
        }

        [TestCase(ESAgentGraphStableIds.SkillInputNode)]
        [TestCase(ESAgentGraphStableIds.SkillTaskNode)]
        [TestCase(ESAgentGraphStableIds.SkillCallNode)]
        [TestCase(ESAgentGraphStableIds.SkillBranchNode)]
        [TestCase(ESAgentGraphStableIds.SkillForEachNode)]
        [TestCase(ESAgentGraphStableIds.SkillApprovalNode)]
        [TestCase(ESAgentGraphStableIds.SkillFanOutNode)]
        [TestCase(ESAgentGraphStableIds.SkillJoinNode)]
        [TestCase(ESAgentGraphStableIds.SkillOutputNode)]
        public void AISkillExecution_AllNodeTemplatesMigrateV1ToV2WithoutReplacingExistingPortIds(
            string nodeTypeId)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                Assert.That(ESGraphAuthoringRegistry.TryGetNodeDefinition(graph.DomainKey,
                    ESAgentGraphStableIds.Node(nodeTypeId), out IESGraphNodeDefinition definition),
                    Is.True);
                ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName,
                    Vector2.zero, definition.Ports);
                Assert.That(graph.UpdateNode(node.nodeId, node.TypeKey, 1, node.title,
                    definition.CreateDefaultPayload(), out string updateError), Is.True, updateError);

                ESGraphPortRecord removedPort = node.ports[node.ports.Count - 1];
                node.ports.RemoveAt(node.ports.Count - 1);
                var existingPortIds = node.ports.ToDictionary(port => port.stableKey,
                    port => port.portId, StringComparer.Ordinal);
                node.ports[0].name = "旧端口名称";
                node.ports[0].meaning = "legacy.port";

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, node.nodeId,
                    out string migrationError), Is.True, migrationError);
                Assert.That(node.version, Is.EqualTo(ESAISkillExecutionNodeContractV2.Version));
                Assert.That(node.ports.Count, Is.EqualTo(definition.Ports.Count));
                Assert.That(node.ports.Single(port => port.stableKey == removedPort.stableKey).portId,
                    Is.Not.EqualTo(removedPort.portId));
                foreach (KeyValuePair<string, string> pair in existingPortIds)
                    Assert.That(node.ports.Single(port => port.stableKey == pair.Key).portId,
                        Is.EqualTo(pair.Value), pair.Key);
                foreach (ESGraphPortDefinition expected in definition.Ports)
                {
                    ESGraphPortRecord actual = node.ports.Single(port =>
                        port.stableKey == expected.stableKey);
                    Assert.That(actual.name, Is.EqualTo(expected.name), expected.stableKey);
                    Assert.That(actual.meaning, Is.EqualTo(ESGraphEndpointRules.ResolveMeaning(
                        expected.meaning, expected.name, expected.stableKey)), expected.stableKey);
                    Assert.That(actual.valueTypeId, Is.EqualTo(expected.valueTypeId), expected.stableKey);
                    Assert.That(actual.direction, Is.EqualTo(expected.direction), expected.stableKey);
                    Assert.That(actual.capacity, Is.EqualTo(expected.capacity), expected.stableKey);
                    Assert.That(actual.aggregation, Is.EqualTo(expected.aggregation), expected.stableKey);
                }
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_ConnectedTemplateMigrationPreservesAllStableIdentitiesAndBake()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneScanReview(graph);
                string[] nodeIds = graph.Nodes.Select(node => node.nodeId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] portIds = graph.Nodes.SelectMany(node => node.ports)
                    .Select(port => port.portId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] edgeIds = graph.Edges.Select(edge => edge.edgeId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();

                foreach (ESGraphNodeRecord node in graph.Nodes)
                {
                    node.version = 1;
                    Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, node.nodeId,
                        out string migrationError), Is.True, migrationError);
                }

                Assert.That(graph.Nodes.Select(node => node.nodeId)
                    .OrderBy(value => value, StringComparer.Ordinal), Is.EqualTo(nodeIds));
                Assert.That(graph.Nodes.SelectMany(node => node.ports).Select(port => port.portId)
                    .OrderBy(value => value, StringComparer.Ordinal), Is.EqualTo(portIds));
                Assert.That(graph.Edges.Select(edge => edge.edgeId)
                    .OrderBy(value => value, StringComparer.Ordinal), Is.EqualTo(edgeIds));
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out _, out IESBakedGraphPlan plan,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));
                Assert.That(plan, Is.TypeOf<ESAISkillExecutionSpec>());
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_NodeMigrationRejectsUnexpectedPortWithoutPartialMutation()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                Assert.That(ESGraphAuthoringRegistry.TryGetNodeDefinition(graph.DomainKey,
                    ESAgentGraphStableIds.Node(ESAgentGraphStableIds.SkillTaskNode),
                    out IESGraphNodeDefinition definition), Is.True);
                ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName,
                    Vector2.zero, definition.Ports);
                Assert.That(graph.UpdateNode(node.nodeId, node.TypeKey, 1, node.title,
                    definition.CreateDefaultPayload(), out string updateError), Is.True, updateError);
                Assert.That(graph.AddPort(node.nodeId, new ESGraphPortDefinition(
                    "旧自定义端口", "skill.legacy.extra", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Single, ESGraphPortValueIds.Any), out string addError),
                    Is.Not.Null, addError);
                string[] portState = node.ports.Select(port => port.portId + "|" + port.stableKey
                    + "|" + port.name + "|" + port.meaning).ToArray();

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, node.nodeId,
                    out string migrationError), Is.False);
                Assert.That(migrationError, Does.Contain("不会自动删除或断开"));
                Assert.That(node.version, Is.EqualTo(1));
                Assert.That(node.ports.Select(port => port.portId + "|" + port.stableKey
                    + "|" + port.name + "|" + port.meaning), Is.EqualTo(portState));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AgentAuthoring_ConstraintPayloadV1MigratesToScopedV2Contract()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                ESGraphNodeRecord constraintNode = graph.Nodes.First(node =>
                    node.typeId == ESAgentGraphStableIds.ConstraintNode);
                constraintNode.version = 1;
                constraintNode.payloadJson = JsonUtility.ToJson(new ConstraintPayloadV1
                {
                    kind = ESAgentConstraintKind.Forbidden,
                    statement = "不得写入正式目录。",
                    rationale = "候选必须先审查。",
                    verification = "检查目标路径和 Diff。"
                });

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, constraintNode.nodeId,
                    out string error), Is.True, error);
                ESAgentConstraintPayload migrated = JsonUtility.FromJson<ESAgentConstraintPayload>(
                    constraintNode.payloadJson);
                Assert.That(constraintNode.version, Is.EqualTo(ESAgentConstraintPayload.CurrentSchemaVersion));
                Assert.That(migrated.schemaVersion, Is.EqualTo(ESAgentConstraintPayload.CurrentSchemaVersion));
                Assert.That(migrated.kind, Is.EqualTo(ESAgentConstraintKind.Forbidden));
                Assert.That(migrated.scope, Is.EqualTo(ESAgentConstraintScope.WholeArtifact));
                Assert.That(migrated.combinationMode, Is.EqualTo(ESAgentConstraintCombinationMode.AllOf));
                Assert.That(migrated.priority, Is.EqualTo(50));
                Assert.That(migrated.combinationGroup, Is.Empty);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_BakePreservesConstraintAndRelationSemantics()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));

                Assert.That(spec.contractSchemaVersion,
                    Is.EqualTo(ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion));
                Assert.That(spec.constraints.Single().scope, Is.EqualTo(ESAgentConstraintScope.WholeArtifact));
                Assert.That(spec.constraints.Single().combinationMode,
                    Is.EqualTo(ESAgentConstraintCombinationMode.AllOf));
                Assert.That(spec.constraints.Single().priority, Is.EqualTo(50));
                Assert.That(spec.relations.Select(item => item.relationKind), Is.EquivalentTo(new[]
                {
                    ESAgentRelationKind.ProvidesContext,
                    ESAgentRelationKind.ProvidesContext,
                    ESAgentRelationKind.AppliesConstraint,
                    ESAgentRelationKind.RequiresValidation
                }));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RejectsSingletonAnyOfConstraintGroup()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                ESGraphNodeRecord constraintNode = graph.Nodes.First(node =>
                    node.typeId == ESAgentGraphStableIds.ConstraintNode);
                ESAgentConstraintPayload payload = JsonUtility.FromJson<ESAgentConstraintPayload>(
                    constraintNode.payloadJson);
                payload.combinationMode = ESAgentConstraintCombinationMode.AnyOf;
                payload.combinationGroup = "implementation-option";
                graph.UpdateNode(constraintNode.nodeId, constraintNode.TypeKey, constraintNode.version,
                    constraintNode.title, JsonUtility.ToJson(payload), out _);

                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out _,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.False);
                Assert.That(bakeIssues.Any(issue => issue?.code == "AgentAuthoring.Intent.Bake"
                    && issue.message.Contains("至少需要两条")), Is.True, Describe(bakeIssues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_OutputPayloadV1MigratesToStructuredV2Contracts()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord commandNode);
            try
            {
                commandNode.version = 1;
                commandNode.payloadJson = JsonUtility.ToJson(new AICommandPayloadV1
                {
                    commandName = "检查_迁移_AI命令",
                    targetProjectPath = "Assets/Plugins/ES/AICommands/检查_迁移_AI命令.md",
                    operationMode = ESAgentArtifactOperationMode.UpdateOnly,
                    commandType = "只读体检",
                    defaultWrite = "否",
                    riskLevel = "L1",
                    purpose = "验证 AICommand V1 契约迁移。",
                    expectedInputs = "目标路径。",
                    executionOutline = "读取\n检查\n报告",
                    acceptanceCriteria = "报告真实检查结果。",
                    requiredSections = "必须先读\n交付格式"
                });

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, commandNode.nodeId,
                    out string commandError), Is.True, commandError);
                ESAgentAICommandOutputPayload command = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(
                    commandNode.payloadJson);
                Assert.That(commandNode.version, Is.EqualTo(2));
                Assert.That(command.schemaVersion, Is.EqualTo(2));
                Assert.That(command.commandIntent, Is.EqualTo(ESAgentCommandIntent.ReadOnlyReview));
                Assert.That(command.writeAuthorization, Is.EqualTo(ESAgentWriteAuthorization.NoWrites));
                Assert.That(command.riskLevel, Is.EqualTo(ESAgentRiskLevel.L1));
                Assert.That(command.preconditions, Is.Not.Empty);
                Assert.That(command.blockedHandling, Is.Not.Empty);

                ESGraphNodeRecord skillNode = AddFromProfile(graph,
                    ESAgentGraphStableIds.AISkillOutputNode, new Vector2(540f, 220f));
                skillNode.version = 1;
                skillNode.payloadJson = JsonUtility.ToJson(new SkillPayloadV1
                {
                    skillName = "es-migrated-workflow",
                    targetProjectPath = ".agents/skills/es-migrated-workflow/",
                    operationMode = ESAgentArtifactOperationMode.CreateOrUpdate,
                    description = "迁移 Skill V1。",
                    triggerScenarios = "需要迁移验证时使用。",
                    workflow = "读取\n执行\n验证",
                    nonGoals = "不得扩大授权。",
                    validationSteps = "检查输出。",
                    defaultPrompt = "Use $es-migrated-workflow.",
                    includeReferences = true
                });

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, skillNode.nodeId,
                    out string skillError), Is.True, skillError);
                ESAgentSkillOutputPayload skill = JsonUtility.FromJson<ESAgentSkillOutputPayload>(skillNode.payloadJson);
                Assert.That(skillNode.version, Is.EqualTo(2));
                Assert.That(skill.schemaVersion, Is.EqualTo(2));
                Assert.That(skill.includeAgentsMetadata, Is.True);
                Assert.That(skill.nonTriggerScenarios, Is.Not.Empty);
                Assert.That(skill.inputContract, Is.Not.Empty);
                Assert.That(skill.outputContract, Is.Not.Empty);
                Assert.That(skill.permissionBoundary, Does.Contain("不扩大"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_OutputMigrationRejectsFuturePayloadWithoutMutation()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                outputNode.version = 1;
                outputNode.payloadJson = "{\"schemaVersion\":99,\"commandName\":\"future\"}";
                string before = outputNode.payloadJson;

                Assert.That(ESGraphAuthoringRegistry.TryMigrateNode(graph, outputNode.nodeId,
                    out string error), Is.False);
                Assert.That(error, Does.Contain("不支持"));
                Assert.That(outputNode.version, Is.EqualTo(1));
                Assert.That(outputNode.payloadJson, Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_SemanticContractsRejectAuthorizationAndIdempotencyConflicts()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord commandNode);
            try
            {
                ESAgentAICommandOutputPayload command = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(
                    commandNode.payloadJson);
                command.commandIntent = ESAgentCommandIntent.ReadOnlyReview;
                command.writeAuthorization = ESAgentWriteAuthorization.ScopedWrites;
                graph.UpdateNode(commandNode.nodeId, commandNode.typeId, commandNode.version,
                    commandNode.title, JsonUtility.ToJson(command), out _);

                ESGraphNodeRecord skillNode = AddFromProfile(graph,
                    ESAgentGraphStableIds.AISkillOutputNode, new Vector2(540f, 220f));
                ESAgentSkillOutputPayload skill = JsonUtility.FromJson<ESAgentSkillOutputPayload>(skillNode.payloadJson);
                skill.effectKind = ESAgentSkillEffectKind.ControlledMutation;
                skill.idempotency = ESAgentSkillIdempotency.NotApplicable;
                graph.UpdateNode(skillNode.nodeId, skillNode.typeId, skillNode.version,
                    skillNode.title, JsonUtility.ToJson(skill), out _);

                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(item => item?.code == "AgentAuthoring.AICommandOutput"
                    && item.message.Contains("受控执行")), Is.True, Describe(issues));
                Assert.That(issues.Any(item => item?.code == "AgentAuthoring.AgentSkillOutput"
                    && item.message.Contains("幂等")), Is.True, Describe(issues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_TypedPortsRejectCrossStageConnections()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.GoalNode);
                ESGraphPortRecord goalOutput = goal.ports.Single(port => port.direction == ESGraphPortDirection.Output);
                ESGraphPortRecord artifactInput = outputNode.ports.Single(port => port.direction == ESGraphPortDirection.Input);
                Assert.That(graph.CanConnect(goalOutput.portId, artifactInput.portId, null, out string error), Is.False);
                Assert.That(error, Does.Contain("端口类型不兼容"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ConnectionGateRejectsUnknownSameTypeTransitionBeforeMutation()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESGraphNodeRecord source = AddFromProfile(graph, ESAgentGraphStableIds.GoalNode,
                    Vector2.zero);
                ESGraphNodeRecord target = AddFromProfile(graph, ESAgentGraphStableIds.ReferenceNode,
                    Vector2.right);
                source.typeId = ESAgentGraphStableIds.ValidationNode;
                string output = source.ports.Single(port =>
                    port.direction == ESGraphPortDirection.Output).portId;
                string input = target.ports.Single(port =>
                    port.direction == ESGraphPortDirection.Input).portId;

                Assert.That(graph.CanConnect(output, input, null, out string error), Is.False);
                Assert.That(error, Does.Contain("AI 节点阶段"));
                var compatible = new HashSet<string>(StringComparer.Ordinal);
                Assert.That(graph.TryBuildConnectionCompatibilityIndex(output, compatible,
                    out string indexError), Is.True, indexError);
                Assert.That(compatible, Does.Not.Contain(input));
                Assert.That(graph.Edges, Is.Empty);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RejectsMutatedDomainPortSchema()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                outputNode.ports.Single(port => port.direction == ESGraphPortDirection.Input).valueTypeId = "flow";
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.PortSchema"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RepairsPortSchemaWithoutChangingStableIds()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                string[] nodeIds = graph.Nodes.Select(node => node.nodeId).ToArray();
                string[] portIds = graph.Nodes.SelectMany(node => node.ports).Select(port => port.portId).ToArray();
                string[] edgeIds = graph.Edges.Select(edge => edge.edgeId).ToArray();
                outputNode.ports.Single(port => port.direction == ESGraphPortDirection.Input).valueTypeId = "flow";
                Assert.That(ESAgentAuthoringGraphSchema.TryRepairPorts(graph, out string error), Is.True, error);
                Assert.That(graph.Nodes.Select(node => node.nodeId), Is.EqualTo(nodeIds));
                Assert.That(graph.Nodes.SelectMany(node => node.ports).Select(port => port.portId), Is.EqualTo(portIds));
                Assert.That(graph.Edges.Select(edge => edge.edgeId), Is.EqualTo(edgeIds));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_PortRepairUsesStableKeysAfterPortOrderChanges()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                outputNode.ports.Reverse();
                string[] reorderedIds = outputNode.ports.Select(port => port.portId).ToArray();
                Assert.That(outputNode.TryGetPort(ESGraphBuiltInPortKeys.Input,
                    out ESGraphPortRecord input), Is.True);
                Assert.That(outputNode.TryGetPort(ESAgentGraphStableIds.ArtifactOutputPortKey,
                    out ESGraphPortRecord output), Is.True);
                input.name = "错误输入";
                input.valueTypeId = ESGraphPortValueIds.Flow;
                output.name = "错误输出";
                output.valueTypeId = ESGraphPortValueIds.Text;

                Assert.That(ESAgentAuthoringGraphSchema.TryRepairPorts(graph, out string error),
                    Is.True, error);
                Assert.That(outputNode.ports.Select(port => port.portId), Is.EqualTo(reorderedIds));
                Assert.That(input.name, Is.EqualTo("产物要求"));
                Assert.That(input.valueTypeId, Is.EqualTo(ESAgentGraphStableIds.RequirementPort));
                Assert.That(output.name, Is.EqualTo("候选产物"));
                Assert.That(output.valueTypeId, Is.EqualTo(ESAgentGraphStableIds.ArtifactPort));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_PortRepairFailureDoesNotPartiallyMutateGraph()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.GoalNode);
                Assert.That(goal.TryGetPort(ESAgentGraphStableIds.ContextOutputPortKey,
                    out ESGraphPortRecord goalOutput), Is.True);
                Assert.That(outputNode.TryGetPort(ESGraphBuiltInPortKeys.Input,
                    out ESGraphPortRecord outputInput), Is.True);
                goalOutput.name = "失败时必须保留";
                outputInput.stableKey = "broken.input";

                Assert.That(ESAgentAuthoringGraphSchema.TryRepairPorts(graph, out string error),
                    Is.False);
                Assert.That(error, Does.Contain("稳定端点"));
                Assert.That(goalOutput.name, Is.EqualTo("失败时必须保留"));
                Assert.That(outputInput.stableKey, Is.EqualTo("broken.input"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_PresetCreatesValidatedDualOutputGraph()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(9));
                Assert.That(graph.Edges.Count, Is.EqualTo(13));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.outputs.Select(item => item.artifactKind),
                    Is.EquivalentTo(new[] { ESAgentArtifactKind.AICommand, ESAgentArtifactKind.AgentSkill }));
                Assert.That(spec.branches, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].matchedTargetNodeIds, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].defaultTargetNodeIds, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].failureTargetNodeIds, Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly, ESAgentArtifactKind.AICommand)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly, ESAgentArtifactKind.AgentSkill)]
        public void AgentAuthoring_SingleArtifactPresetsCreateUsableGraphs(
            ESAgentAuthoringPresetKind presetKind, ESAgentArtifactKind expectedKind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, presetKind);
                Assert.That(graph.Nodes.Count, Is.EqualTo(8));
                Assert.That(graph.Edges.Count, Is.EqualTo(9));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.outputs.Single().artifactKind, Is.EqualTo(expectedKind));
                Assert.That(spec.validations.Single().validateAICommand,
                    Is.EqualTo(expectedKind == ESAgentArtifactKind.AICommand));
                Assert.That(spec.validations.Single().validateAgentSkill,
                    Is.EqualTo(expectedKind == ESAgentArtifactKind.AgentSkill));
                Assert.That(spec.branches, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].matchedTargetNodeIds, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].defaultTargetNodeIds, Has.Length.EqualTo(1));
                Assert.That(spec.branches[0].failureTargetNodeIds, Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_MindMapPresetCreatesBranchedTypedRequirementGraph()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                Assert.That(graph.Nodes.Count, Is.EqualTo(12));
                Assert.That(graph.Edges.Count, Is.EqualTo(19));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.references.Length, Is.EqualTo(2));
                Assert.That(spec.constraints.Length, Is.EqualTo(4));
                Assert.That(spec.branches.Length, Is.EqualTo(1));
                Assert.That(spec.traversals.Length, Is.EqualTo(1));
                Assert.That(spec.outputs.Length, Is.EqualTo(2));
                Assert.That(spec.relations.Length, Is.EqualTo(19));
                Assert.That(spec.relations.Count(item => item.relationKind
                    == ESAgentRelationKind.SelectsBranch), Is.EqualTo(3));
                Assert.That(spec.relations.Count(item => item.relationKind
                    == ESAgentRelationKind.TraversesItems), Is.EqualTo(3));
                Assert.That(spec.constraints.Select(item => item.kind), Is.EquivalentTo(new[]
                {
                    ESAgentConstraintKind.Required,
                    ESAgentConstraintKind.Forbidden,
                    ESAgentConstraintKind.Permission,
                    ESAgentConstraintKind.Quality
                }));
                ESAgentAICommandOutputPayload command = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(
                    graph.Nodes.Single(node => node.typeId == ESAgentGraphStableIds.AICommandOutputNode)
                        .payloadJson);
                Assert.That(command.expectedInputs, Does.Contain("Graph References"));
                Assert.That(command.allowedWriteScopes, Does.Contain("Graph Constraint"));
                Assert.That(command.blockedHandling, Does.Contain("停止副作用"));
                Assert.That(command.requiredEvidence, Does.Contain("Graph 关系"));
                ESAgentSkillOutputPayload skill = JsonUtility.FromJson<ESAgentSkillOutputPayload>(
                    graph.Nodes.Single(node => node.typeId == ESAgentGraphStableIds.AISkillOutputNode)
                        .payloadJson);
                Assert.That(skill.nonTriggerScenarios, Is.Not.Empty);
                Assert.That(skill.inputContract, Does.Contain("需求图"));
                Assert.That(skill.failureRecovery, Does.Contain("停止未开始的副作用"));
                Assert.That(skill.permissionBoundary, Does.Contain("不从 Graph 关系推导新权限"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [TestCase(ESAgentArtifactKind.AICommand)]
        [TestCase(ESAgentArtifactKind.AgentSkill)]
        public void AgentAuthoring_BranchAndTraversalWorkForSingleArtifactGraphs(
            ESAgentArtifactKind artifactKind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                string removedType = artifactKind == ESAgentArtifactKind.AICommand
                    ? ESAgentGraphStableIds.AISkillOutputNode : ESAgentGraphStableIds.AICommandOutputNode;
                ESGraphNodeRecord removed = graph.Nodes.Single(node => node.typeId == removedType);
                Assert.That(graph.RemoveNode(removed.nodeId), Is.True);

                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.outputs.Single().artifactKind, Is.EqualTo(artifactKind));
                Assert.That(spec.branches.Length, Is.EqualTo(1));
                Assert.That(spec.traversals.Length, Is.EqualTo(1));
                Assert.That(spec.skillBundle.branchNodeIds, Is.EquivalentTo(
                    spec.branches.Select(item => item.nodeId)));
                Assert.That(spec.skillBundle.traversalNodeIds, Is.EquivalentTo(
                    spec.traversals.Select(item => item.nodeId)));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_LogicNodesRequireEveryDeclaredExit()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                ESGraphNodeRecord branch = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.BranchNode);
                string failurePortId = branch.ports.Single(port =>
                    port.stableKey == ESAgentGraphStableIds.BranchFailurePortKey).portId;
                ESGraphEdgeRecord edge = graph.Edges.Single(item => item.outputPortId == failurePortId);
                Assert.That(graph.RemoveEdge(edge.edgeId), Is.True);

                var issues = new List<ESGraphValidationIssue>();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(item => item?.code == "AgentAuthoring.LogicRoute"), Is.True,
                    Describe(issues));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_TraversalRejectsUnsafeLimitsWithoutMutatingRelations()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                ESGraphNodeRecord traversal = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.TraverseNode);
                string[] edgeIds = graph.Edges.Select(edge => edge.edgeId).ToArray();
                ESAgentTraversePayload payload = JsonUtility.FromJson<ESAgentTraversePayload>(
                    traversal.payloadJson);
                payload.maxDepth = 0;
                payload.maxItems = 513;
                Assert.That(graph.UpdateNode(traversal.nodeId, traversal.typeId, traversal.version,
                    traversal.title, JsonUtility.ToJson(payload), out string error), Is.True, error);

                var issues = new List<ESGraphValidationIssue>();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(item => item?.code == "AgentAuthoring.Traversal"), Is.True,
                    Describe(issues));
                Assert.That(graph.Edges.Select(edge => edge.edgeId), Is.EqualTo(edgeIds));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [TestCase(ESAgentArtifactKind.AICommand)]
        [TestCase(ESAgentArtifactKind.AgentSkill)]
        public void AgentAuthoring_ArtifactViewPreservesConnectedLogicSubgraph(
            ESAgentArtifactKind artifactKind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                ESAgentGenerationOutput output = spec.outputs.Single(item => item.artifactKind == artifactKind);
                Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(spec, output.nodeId,
                    out ESAgentArtifactGenerationSpec view, out string error), Is.True, error);
                Assert.That(view.branches.Length, Is.EqualTo(1));
                Assert.That(view.traversals.Length, Is.EqualTo(1));
                Assert.That(view.relations.Count(item => item.relationKind
                    == ESAgentRelationKind.SelectsBranch), Is.EqualTo(3));
                Assert.That(view.relations.Count(item => item.relationKind
                    == ESAgentRelationKind.TraversesItems), Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ComplexPromptContainsExecutableLogicContract()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                string prompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(
                    new ESAgentArtifactGenerationRequest
                    {
                        requestId = "logic-contract",
                        requestDirectory = "ES/Automation/Candidates/AgentAuthoring/logic-contract",
                        candidateDirectory = "ES/Automation/Candidates/AgentAuthoring/logic-contract/candidate",
                        spec = spec
                    });
                Assert.That(prompt, Does.Contain("结构化分支与有界遍历"));
                Assert.That(prompt, Does.Contain(ESAgentGraphStableIds.BranchDefaultPortKey));
                Assert.That(prompt, Does.Contain(ESAgentGraphStableIds.TraverseCompletedPortKey));
                Assert.That(prompt, Does.Contain("maxDepth=8"));
                Assert.That(prompt, Does.Contain("maxItems=128"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_AssetCatalogFindsProjectCommandsAndSkillsOnDemand()
        {
            Assert.That(ESAgentAuthoringAssetCatalog.GetAICommandTargets()
                .Any(path => path.EndsWith("生成_AgentArtifact候选_AI命令.md")), Is.True);
            Assert.That(ESAgentAuthoringAssetCatalog.GetAgentSkillTargets()
                .Any(path => path == ".agents/skills/es-generate-agent-artifacts/"), Is.True);
            Assert.That(ESAgentAuthoringAssetCatalog.GetReferencePaths(ESAgentReferenceKind.CSharpSource)
                .Any(path => path.EndsWith("ESAgentAuthoringGraphIntegration.cs")), Is.True);
            Assert.That(ESAgentAuthoringAssetCatalog.GetReferencePaths(ESAgentReferenceKind.ProjectAsset)
                .Any(path => path.EndsWith("生成_AgentArtifact候选_AI命令.md")), Is.True);
            Assert.That(ESAgentAuthoringAssetCatalog.GetReferencePaths(ESAgentReferenceKind.ProjectAsset)
                .Any(path => path.StartsWith("Assets/ESNormalAssets/")), Is.True);
            Assert.That(ESAgentAuthoringGraphPreset.DefaultGraphFolder,
                Is.EqualTo("Assets/ESNormalAssets/Editor/AgentAuthoring/Graphs"));
        }

        [Test]
        public void AgentAuthoring_BakerRejectsCrossDomainSnapshot()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESGenericGraphAsset>();
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out _), Is.True);
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out _,
                    out IReadOnlyList<ESGraphValidationIssue> issues), Is.False);
                Assert.That(issues.Any(issue => issue?.code == "Graph.Plan.DomainMismatch"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_GenerationPromptContainsReadableMindMapAndDetailedContract()
        {
            ESGraphAssetBase graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                int[] relationOrders = spec.relations.Select(relation => relation.order).ToArray();
                Assert.That(relationOrders, Is.Ordered);
                spec.relations = spec.relations.Reverse().ToArray();
                string prompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(new ESAgentArtifactGenerationRequest
                {
                    requestId = "test-request",
                    requestDirectory = "ES/Automation/Candidates/AgentAuthoring/test-request",
                    candidateDirectory = "ES/Automation/Candidates/AgentAuthoring/test-request/candidate",
                    spec = spec
                });
                Assert.That(prompt, Does.Contain("思路图关系"));
                Assert.That(prompt, Does.Contain("```mermaid"));
                Assert.That(prompt.IndexOf("顺序 " + relationOrders[0] + " |", StringComparison.Ordinal),
                    Is.LessThan(prompt.IndexOf("顺序 " + relationOrders[relationOrders.Length - 1] + " |",
                        StringComparison.Ordinal)), "Prompt 必须按显式 order 读取，不能信任数组排列。");
                Assert.That(prompt, Does.Contain("#" + relationOrders[0]));
                Assert.That(prompt, Does.Contain(ESAgentGraphStableIds.RequirementPort));
                Assert.That(prompt, Does.Contain("acceptance criteria"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ChinesePayloadAndPromptRoundTripWithoutLoss()
        {
            ESGraphAssetBase graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                var payload = new ESAgentAICommandOutputPayload
                {
                    commandName = "生成中文命令",
                    targetProjectPath = "Assets/Plugins/ES/AICommands/生成中文命令.md",
                    purpose = "根据 Graph Authoring 与玩家意图生成可审查的候选文件",
                    expectedInputs = "用户目标、中文规则和项目路径",
                    acceptanceCriteria = "中文内容保持 UTF-8，Diff 可读且必须人工批准"
                };
                string json = JsonUtility.ToJson(payload);
                Assert.That(json, Does.Contain("生成中文命令"));
                Assert.That(graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version,
                    "生成中文 AICommand", json, out string error), Is.True, error);
                ESAgentAICommandOutputPayload roundTrip = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(
                    graph.FindNode(outputNode.nodeId).payloadJson);
                Assert.That(roundTrip.commandName, Is.EqualTo("生成中文命令"));
                Assert.That(roundTrip.acceptanceCriteria, Does.Contain("人工批准"));

                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                string prompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(new ESAgentArtifactGenerationRequest
                {
                    requestId = "中文测试",
                    requestDirectory = "ES/Automation/Candidates/AgentAuthoring/中文测试",
                    candidateDirectory = "ES/Automation/Candidates/AgentAuthoring/中文测试/candidate",
                    spec = spec
                });
                Assert.That(prompt, Does.Contain("生成中文 AICommand"));
                Assert.That(prompt, Does.Contain("中文标题、描述、规则、路径和验收文本必须原样保留"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ImplementationTaskUsesApprovedCommandAndExplicitAuthorizationBoundary()
        {
            var request = new ESAgentArtifactGenerationRequest
            {
                requestId = "implementation-test",
                spec = new ESAgentArtifactGenerationSpec
                {
                    sourceGraphId = "0123456789abcdef0123456789abcdef",
                    sourceContentSignature = "0123456789abcdef",
                    goal = new ESAgentGenerationGoal
                    {
                        title = "实现中文 Graph 功能",
                        objective = "按实现链修改项目并完成验证",
                        successCriteria = "功能可用且中文显示正确"
                    },
                    constraints = new[]
                    {
                        new ESAgentGenerationConstraint
                        {
                            kind = ESAgentConstraintKind.Permission,
                            statement = "只修改 AICommand 声明的范围。"
                        }
                    },
                    outputs = new[]
                    {
                        new ESAgentGenerationOutput
                        {
                            artifactKind = ESAgentArtifactKind.AICommand,
                            artifactName = "实现 Graph 功能",
                            targetProjectPath = "Assets/Plugins/ES/AICommands/执行_Graph功能_AI命令.md",
                            executionOutline = "检查工作树；实现；验证。",
                            acceptanceCriteria = "编译通过并保留真实证据。"
                        }
                    }
                }
            };
            var approval = new ESAgentArtifactApprovalManifest
            {
                requestId = request.requestId,
                approvedAtUtc = "2026-08-06T00:00:00Z",
                sourceGraphId = request.spec.sourceGraphId,
                sourceContentSignature = request.spec.SourceContentSignature
            };

            string task = ESAgentImplementationSessionLauncher.BuildImplementationTask(request, approval,
                new[] { "Assets/Plugins/ES/AICommands/执行_Graph功能_AI命令.md" });

            Assert.That(task, Does.Contain("显式点击“打开新窗口执行实现”"));
            Assert.That(task, Does.Contain("$es-use-ai-command"));
            Assert.That(task, Does.Contain("执行_Graph功能_AI命令.md"));
            Assert.That(task, Does.Contain(request.spec.sourceGraphId));
            Assert.That(task, Does.Contain("不授权 Git stage/commit/push"));
            Assert.That(task, Does.Contain("按实现链直接完成修改"));
        }

        [Test]
        public void AgentAuthoring_LaunchReportNeverConfusesTerminalCreationWithTaskDelivery()
        {
            var terminalOnly = new ESCodexSessionLaunchResult
            {
                tabTitle = "ES·Graph实现",
                processId = 42,
                launched = true,
                terminalStarted = true,
                envelopePath = "envelope.json"
            };
            string terminalReport = ESAgentImplementationSessionLauncher.BuildLaunchReport(terminalOnly);
            Assert.That(terminalReport, Does.Contain("仅完成终端创建"));
            Assert.That(terminalReport, Does.Contain("不能视为任务已开始执行"));

            terminalOnly.promptObserved = true;
            terminalOnly.startupTimedOut = true;
            string timeoutReport = ESAgentImplementationSessionLauncher.BuildLaunchReport(terminalOnly);
            Assert.That(timeoutReport, Does.Contain("启动证据超时"));
            Assert.That(timeoutReport, Does.Contain("信封接收回执仍未出现"));
            Assert.That(timeoutReport, Does.Contain("不能视为任务已送达"));

            var accepted = new ESCodexSessionLaunchResult
            {
                tabTitle = "ES·Graph实现",
                processId = 43,
                contextAccepted = true,
                acceptanceReceiptPath = "receipt.json"
            };
            string acceptedReport = ESAgentImplementationSessionLauncher.BuildLaunchReport(accepted);
            Assert.That(acceptedReport, Does.Contain("完成初始化"));
            Assert.That(acceptedReport, Does.Contain("receipt.json"));
        }

        [Test]
        public void CmdAgent_NewAndExistingSessionsUseManagedBootstrapModes()
        {
            Assert.That(ESCmdAgentWindow.GetManagedDispatchModeForTests(string.Empty), Is.EqualTo("New"));
            Assert.That(ESCmdAgentWindow.GetManagedDispatchModeForTests("019fed14-4939-76b2-b377-8114c4416762"),
                Is.EqualTo("SendMessage"));
        }

        [Test]
        public void CmdAgent_OnlyTerminalMailboxStatesStopPolling()
        {
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("queued"), Is.False);
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("accepted"), Is.False);
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("turn_started"), Is.False);
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("completed"), Is.True);
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("failed"), Is.True);
            Assert.That(ESCmdAgentWindow.IsTerminalManagedMessageStateForTests("expired"), Is.True);
        }

        [Test]
        public void CmdAgent_TerminalMappingRequiresObservableUniqueTab()
        {
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(true, "ProjectWindow", 42, 1, true),
                Is.EqualTo("唯一可见页签已观测"));
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(true, "ProjectWindow", 42, 2, true),
                Does.StartWith("页签匹配歧义"));
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(true, "ProjectWindow", 42, 0, true),
                Is.EqualTo("进程在线；未找到受管页签"));
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(true, "ProjectWindow", 0, 1, true),
                Is.EqualTo("终端宿主未映射；拒绝按标题操作"));
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(true, "PlainCmd", 0, 0, false),
                Is.EqualTo("精确 CMD 进程在线"));
            Assert.That(ESCmdAgentWindow.GetTerminalMappingStateForTests(false, "ProjectWindow", 42, 1, true),
                Is.EqualTo("进程离线"));
        }

        [Test]
        public void CmdAgent_ResponsibilityKeyIsExplicitAndStrict()
        {
            Assert.That(ESCmdAgentWindow.IsValidResponsibilityKeyForTests("graphics-tools"), Is.True);
            Assert.That(ESCmdAgentWindow.IsValidResponsibilityKeyForTests("a"), Is.False);
            Assert.That(ESCmdAgentWindow.IsValidResponsibilityKeyForTests("图形工具"), Is.False);
            Assert.That(ESCmdAgentWindow.IsValidResponsibilityKeyForTests("graphics tools"), Is.False);
        }

        [Test]
        public void CmdAgent_ManagedStatusNeverFallsBackFromKnownSessionId()
        {
            const string json = "{\"sessions\":[{\"sessionId\":\"other-session\",\"taskKey\":\"same-task\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedStatusRecordForTests(json,
                "expected-session", "same-task", out string match, out string error);

            Assert.That(selected, Is.False);
            Assert.That(match, Is.Empty);
            Assert.That(error, Does.Contain("已拒绝退回 TaskKey"));
        }

        [Test]
        public void CmdAgent_ManagedStatusRejectsAmbiguousTaskKeyWithoutSessionId()
        {
            const string json = "{\"sessions\":[{\"sessionId\":\"session-a\",\"taskKey\":\"same-task\"},"
                + "{\"sessionId\":\"session-b\",\"taskKey\":\"same-task\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedStatusRecordForTests(json,
                string.Empty, "same-task", out string match, out string error);

            Assert.That(selected, Is.False);
            Assert.That(match, Is.Empty);
            Assert.That(error, Does.Contain("匹配到 2 个会话"));
        }

        [Test]
        public void CmdAgent_ManagedMessageStatusRequiresExactMessageId()
        {
            const string json = "{\"messages\":[{\"messageId\":\"message-a\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedMessageRecordForTests(json,
                "message-b", out string match, out string error);

            Assert.That(selected, Is.False);
            Assert.That(match, Is.Empty);
            Assert.That(error, Does.Contain("已拒绝按标题、PID 或列表顺序猜测"));
        }

        [Test]
        public void CmdAgent_PromptSeparatesCurrentDemandFromBudgetedReferenceMaterial()
        {
            string oversized = new string('甲', 60000) + "不应抵达末尾";
            string prompt = ESCmdAgentWindow.BuildPromptForTests("只执行当前可见需求", oversized);

            Assert.That(prompt, Does.StartWith("【当前页签职责｜请在本轮着重采用此视角】"));
            Assert.That(prompt, Does.Contain("负责流程梳理、自动化与协作效率"));
            Assert.That(prompt, Does.Contain("【当前需求】\n只执行当前可见需求"));
            Assert.That(prompt, Does.Contain("上下文安全边界"));
            Assert.That(prompt, Does.Contain("MCP 使用策略"));
            Assert.That(prompt, Does.Contain("显式指定 unityMCP"));
            Assert.That(prompt, Does.Contain("避免为了证明连接而重复枚举全部工具"));
            Assert.That(prompt, Does.Contain("--- CONTEXT BEGIN [测试] 边界 ---"));
            Assert.That(prompt, Does.Contain("[内容已按上下文预算截断]"));
            Assert.That(prompt, Does.Not.Contain("不应抵达末尾"));
            Assert.That(prompt.Length, Is.LessThan(50000));
        }

        [Test]
        public void CmdAgent_ResponsibilityPresetsAreShortEditableAndPrecedeAIWarnings()
        {
            ESCmdAgentWindow.GetResponsibilityPresetsForTests(out string[] names, out string[] texts);
            Assert.That(names, Is.EqualTo(new[]
            {
                "界面开发", "玩法开发", "内容增加", "测试", "流程", "验收"
            }));
            Assert.That(texts, Has.Length.EqualTo(6));
            Assert.That(texts.All(text => !string.IsNullOrWhiteSpace(text) && text.Length <= 30), Is.True);

            string prompt = ESCmdAgentWindow.BuildPromptWithResponsibilityForTests(
                "读取项目规范", new string('职', 40), "# AIWarnings 规则索引");
            Assert.That(prompt, Does.Contain(new string('职', 30)));
            Assert.That(prompt, Does.Not.Contain(new string('职', 31)));
            Assert.That(prompt.IndexOf("【当前页签职责", StringComparison.Ordinal),
                Is.LessThan(prompt.IndexOf("--- CONTEXT BEGIN [AIWarnings]", StringComparison.Ordinal)));
            Assert.That(prompt, Does.Contain("不扩大当前需求的权限或修改范围"));
            Assert.That(prompt, Does.Contain("AIWarnings 执行门禁"));
        }

        [Test]
        public void CmdAgent_AIWarningsOneClickLoadsOnlyFixedMetadataReferences()
        {
            Assert.That(ESCmdAgentWindow.TryLoadAIWarningsForTests(out string[] labels,
                out string[] values, out string error), Is.True, error);

            Assert.That(labels, Is.EqualTo(new[]
            {
                "协作入口 README",
                "当前状态 CurrentStatus",
                "规则索引 RuleIndex"
            }));
            Assert.That(values, Has.Length.EqualTo(3));
            Assert.That(values, Has.All.Contains("禁止递归加载全部 AIWarnings"));
            Assert.That(values, Has.All.Contains("附件版本：3"));
            Assert.That(values, Has.All.Contains("来源（绝对路径）："));
            Assert.That(values, Has.All.Contains("文件 SHA-256："));
            Assert.That(values, Has.All.Contains("必须读取：是"));
            Assert.That(values, Has.All.Contains("Content: omitted from the launch envelope"));
            Assert.That(values, Has.All.Contains("sourceDrift"));
            Assert.That(values[0], Does.Not.Contain("# ES AIWarnings 协作入口"));
            Assert.That(values[1], Does.Not.Contain("# AIWarnings 当前状态"));
            Assert.That(values[2], Does.Not.Contain("# AIWarnings 规则索引"));
            Assert.That(values.Sum(value => value.Length), Is.LessThan(4096));
        }

        [Test]
        public void CmdAgent_McpEndpointContextNeverLeaksCredentialsOrQueryTokens()
        {
            Assert.That(ESCmdAgentWindow.SanitizeMcpEndpointForTests(
                    "https://user:secret@example.com/private/path?token=hidden"),
                Is.EqualTo("https://example.com"));
            Assert.That(ESCmdAgentWindow.SanitizeMcpEndpointForTests(
                    "http://127.0.0.1:8080/mcp?token=hidden"),
                Is.EqualTo("http://127.0.0.1:8080/mcp"));
        }

        [Test]
        public void CmdAgent_CurrentAssetIdentityUsesUnityGuidAndLocalFileId()
        {
            const string assetPath = "Assets/ESNormalAssets/Data/GlobalData/CmdAgent/ESCmdAgent.asset";
            ESCmdAgent agent = UnityEditor.AssetDatabase.LoadAssetAtPath<ESCmdAgent>(assetPath);
            Assert.That(agent, Is.Not.Null, "CmdAgent 配置资产必须存在，才能验证跨页面权威标识。");
            Assert.That(UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(agent,
                out string guid, out long localFileId), Is.True);

            string identity = ESCmdAgentWindow.BuildObjectIdentityForTests(agent);
            Assert.That(identity, Is.EqualTo("unity://asset/" + guid + "/" + localFileId));
        }

        [Test]
        public void AgentArtifactImport_SecondWriteFailureRestoresEveryTouchedFile()
        {
            FakeArtifactFileIO files = CreateTwoExistingFileTransaction(out ESAgentArtifactFileOperation[] operations);
            files.FailCopies.Add(FakeArtifactFileIO.CopyKey("source-2", "target-2"));

            ESAgentArtifactImportResult result = ESAgentArtifactImportTransaction.Execute(operations, files, null);

            Assert.That(result.State, Is.EqualTo(ESAgentArtifactImportState.RolledBack));
            Assert.That(result.RollbackConfirmed, Is.True);
            Assert.That(files.Read("target-1"), Is.EqualTo("old-1"));
            Assert.That(files.Read("target-2"), Is.EqualTo("old-2"));
            Assert.That(result.RecoveryErrors, Is.Empty);
        }

        [Test]
        public void AgentArtifactImport_RestoreFailureContinuesAndReportsUnconfirmedState()
        {
            FakeArtifactFileIO files = CreateTwoExistingFileTransaction(out ESAgentArtifactFileOperation[] operations);
            files.FailCopies.Add(FakeArtifactFileIO.CopyKey("source-2", "target-2"));
            files.FailCopies.Add(FakeArtifactFileIO.CopyKey("backup-1", "target-1"));

            ESAgentArtifactImportResult result = ESAgentArtifactImportTransaction.Execute(operations, files, null);

            Assert.That(result.State, Is.EqualTo(ESAgentArtifactImportState.RollbackUnconfirmed));
            Assert.That(result.RecoveryErrors.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.RecoveryErrors.Any(error => error.Contains("target-1") && error.Contains("恢复失败")), Is.True);
            Assert.That(result.RecoveryErrors.Any(error => error.Contains("target-1") && error.Contains("SHA-256")), Is.True);
            Assert.That(files.CopyAttempts, Does.Contain(FakeArtifactFileIO.CopyKey("backup-2", "target-2")));
            Assert.That(files.Read("target-2"), Is.EqualTo("old-2"));
        }

        [Test]
        public void AgentArtifactImport_HashMismatchMarksRollbackUnconfirmed()
        {
            FakeArtifactFileIO files = CreateTwoExistingFileTransaction(out ESAgentArtifactFileOperation[] operations);
            files.FailCopies.Add(FakeArtifactFileIO.CopyKey("source-2", "target-2"));
            files.HashResults["target-1"] = new Queue<string>(new[] { "original-hash", "wrong-hash" });

            ESAgentArtifactImportResult result = ESAgentArtifactImportTransaction.Execute(operations, files, null);

            Assert.That(result.State, Is.EqualTo(ESAgentArtifactImportState.RollbackUnconfirmed));
            Assert.That(result.RecoveryErrors.Any(error => error.Contains("target-1") && error.Contains("SHA-256")), Is.True);
        }

        [Test]
        public void AgentArtifactImport_NewFileDeletionFailureMarksRollbackUnconfirmed()
        {
            var files = new FakeArtifactFileIO();
            files.Seed("source-new", "new-content");
            files.FailDeletes.Add("target-new");
            var operation = new ESAgentArtifactFileOperation
            {
                SourcePath = "source-new",
                TargetPath = "target-new",
                BackupPath = "backup-new"
            };

            ESAgentArtifactImportResult result = ESAgentArtifactImportTransaction.Execute(
                new[] { operation }, files, () => throw new InvalidOperationException("批准清单写入失败"));

            Assert.That(result.State, Is.EqualTo(ESAgentArtifactImportState.RollbackUnconfirmed));
            Assert.That(files.FileExists("target-new"), Is.True);
            Assert.That(result.RecoveryErrors.Any(error => error.Contains("删除新文件失败")), Is.True);
            Assert.That(result.RecoveryErrors.Any(error => error.Contains("删除核对失败")), Is.True);
        }

        [Test]
        public void AgentAuthoring_CandidateValidatorRejectsEscapingAgentSkillTarget()
        {
            var request = new ESAgentArtifactGenerationRequest
            {
                requestId = "path-validation",
                spec = new ESAgentArtifactGenerationSpec
                {
                    outputs = new[]
                    {
                        new ESAgentGenerationOutput
                        {
                            artifactKind = ESAgentArtifactKind.AgentSkill,
                            targetProjectPath = ".agents/skills/es-safe-workflow/"
                        }
                    }
                }
            };
            var file = new ESAgentArtifactCandidateFile
            {
                artifactKind = ESAgentArtifactKind.AgentSkill,
                candidateRelativePath = "candidate/es-safe-workflow/SKILL.md",
                targetProjectPath = ".agents/skills/es-safe-workflow/../../outside/SKILL.md"
            };

            Assert.That(ESAgentArtifactCandidateValidator.TryResolveFormalTarget(request, file,
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("Agent Skill"));
        }

        [Test]
        public void GraphAuthoring_ProfilesExposeIndependentNodePalettes()
        {
            IReadOnlyList<IESGraphAuthoringProfile> profiles = ESGraphAuthoringRegistry.AllProfiles;
            Assert.That(profiles.Count, Is.GreaterThanOrEqualTo(4));
            foreach (IESGraphAuthoringProfile profile in profiles)
            {
                Assert.That(profile.NodeDefinitions, Is.Not.Empty, profile.DisplayName);
                Assert.That(profile.NodeDefinitions.All(definition => definition.Domain == profile.Domain), Is.True,
                    profile.DisplayName + " 包含了其他领域节点。");
            }

            IESGraphAuthoringProfile generic = profiles.Single(profile => profile.Domain.Kind == ESGraphDomainKind.Generic);
            IESGraphAuthoringProfile behavior = profiles.Single(profile => profile.Domain.Kind == ESGraphDomainKind.BehaviorTree);
            Assert.That(generic.NodeDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.BehaviorAction), Is.False);
            Assert.That(behavior.NodeDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.GenericFlow), Is.False);
        }

        [Test]
        public void AgentAuthoring_SingleOutputTemplatesFilterUnrelatedNodes()
        {
            ESGraphAssetBase commandGraph = CreateValidGraph(out _);
            try
            {
                IReadOnlyList<IESGraphNodeDefinition> commandDefinitions = ESGraphAuthoringRegistry.GetNodeDefinitions(commandGraph);
                Assert.That(commandDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AICommandOutputNode), Is.True);
                Assert.That(commandDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AISkillOutputNode), Is.False);
            }
            finally { Object.DestroyImmediate(commandGraph); }

            ESGraphAssetBase skillGraph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                AddFromProfile(skillGraph, ESAgentGraphStableIds.AISkillOutputNode, new Vector2(0f, 0f));
                IReadOnlyList<IESGraphNodeDefinition> skillDefinitions = ESGraphAuthoringRegistry.GetNodeDefinitions(skillGraph);
                Assert.That(skillDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AISkillOutputNode), Is.True);
                Assert.That(skillDefinitions.Any(item => item.NodeType.StableId == ESAgentGraphStableIds.AICommandOutputNode), Is.False);
            }
            finally { Object.DestroyImmediate(skillGraph); }
        }

        [Test]
        public void AgentAuthoring_ArtifactViewsKeepOnlyTheSelectedConnectedBranch()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                ESAgentArtifactKind.AICommand, out ESAgentArtifactGenerationSpec commandView,
                out string commandError), Is.True, commandError);
            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                ESAgentArtifactKind.AgentSkill, out ESAgentArtifactGenerationSpec skillView,
                out string skillError), Is.True, skillError);

            Assert.That(commandView.outputs.Select(item => item.artifactKind),
                Is.EqualTo(new[] { ESAgentArtifactKind.AICommand }));
            Assert.That(commandView.references.Select(item => item.nodeId),
                Is.EqualTo(new[] { "reference-command" }));
            Assert.That(commandView.constraints.Select(item => item.nodeId),
                Is.EqualTo(new[] { "constraint-command" }));
            Assert.That(commandView.validations.Select(item => item.nodeId),
                Is.EqualTo(new[] { "validation-command" }));
            Assert.That(commandView.relations, Has.Length.EqualTo(4));
            Assert.That(commandView.outputs.Single().preconditions, Is.EqualTo("command-preconditions"));
            Assert.That(commandView.outputs.Single().writeAuthorization,
                Is.EqualTo(ESAgentWriteAuthorization.ConfirmBeforeWrite));

            Assert.That(skillView.outputs.Select(item => item.artifactKind),
                Is.EqualTo(new[] { ESAgentArtifactKind.AgentSkill }));
            Assert.That(skillView.references.Select(item => item.nodeId),
                Is.EqualTo(new[] { "reference-skill" }));
            Assert.That(skillView.constraints.Select(item => item.nodeId),
                Is.EqualTo(new[] { "constraint-skill" }));
            Assert.That(skillView.validations.Select(item => item.nodeId),
                Is.EqualTo(new[] { "validation-skill" }));
            Assert.That(skillView.relations, Has.Length.EqualTo(4));
            Assert.That(skillView.outputs.Single().skillPermissionBoundary,
                Is.EqualTo("skill-permission-boundary"));
            Assert.That(skillView.outputs.Single().skillIdempotency,
                Is.EqualTo(ESAgentSkillIdempotency.Required));
        }

        [Test]
        public void AgentAuthoring_ArtifactViewDoesNotMutateTheSourceSpec()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            ESAgentGenerationOutput originalCommand = source.outputs[0];

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                ESAgentArtifactKind.AICommand, out ESAgentArtifactGenerationSpec commandView,
                out string error), Is.True, error);

            Assert.That(source.outputs, Has.Length.EqualTo(2));
            Assert.That(source.references, Has.Length.EqualTo(2));
            Assert.That(source.constraints, Has.Length.EqualTo(2));
            Assert.That(source.validations, Has.Length.EqualTo(2));
            Assert.That(source.relations, Has.Length.EqualTo(8));
            Assert.That(source.outputs[0], Is.SameAs(originalCommand));
            Assert.That(commandView.outputs[0], Is.Not.SameAs(originalCommand));
        }

        [Test]
        public void AgentAuthoring_ArtifactViewByNodeIdIsolatesOneOutputAmongTheSameKind()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            AddSecondCommandBranch(source);
            ESAgentGenerationOutput originalOutput = source.outputs[0];

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "command-output", out ESAgentArtifactGenerationSpec firstView,
                out string firstError), Is.True, firstError);
            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "command-output-secondary", out ESAgentArtifactGenerationSpec secondView,
                out string secondError), Is.True, secondError);

            Assert.That(firstView.outputs.Select(item => item.nodeId),
                Is.EqualTo(new[] { "command-output" }));
            Assert.That(firstView.references.Select(item => item.nodeId),
                Is.EqualTo(new[] { "reference-command" }));
            Assert.That(firstView.constraints.Select(item => item.nodeId),
                Is.EqualTo(new[] { "constraint-command" }));
            Assert.That(firstView.validations.Select(item => item.nodeId),
                Is.EqualTo(new[] { "validation-command" }));
            Assert.That(firstView.relations, Has.Length.EqualTo(4));
            Assert.That(firstView.relations.Any(item => item.fromNodeId.Contains("secondary")
                || item.toNodeId.Contains("secondary")), Is.False);

            Assert.That(secondView.outputs.Select(item => item.nodeId),
                Is.EqualTo(new[] { "command-output-secondary" }));
            Assert.That(secondView.references.Select(item => item.nodeId),
                Is.EqualTo(new[] { "reference-command-secondary" }));
            Assert.That(secondView.constraints.Select(item => item.nodeId),
                Is.EqualTo(new[] { "constraint-command-secondary" }));
            Assert.That(secondView.validations.Select(item => item.nodeId),
                Is.EqualTo(new[] { "validation-command-secondary" }));
            Assert.That(secondView.relations, Has.Length.EqualTo(4));

            Assert.That(source.outputs, Has.Length.EqualTo(3));
            Assert.That(source.outputs[0], Is.SameAs(originalOutput));
            Assert.That(firstView.outputs[0], Is.Not.SameAs(originalOutput));
        }

        [Test]
        public void AgentAuthoring_ArtifactViewByNodeIdRejectsMissingNonOutputAndDuplicateIdentity()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "goal", out ESAgentArtifactGenerationSpec nonOutputView,
                out string nonOutputError), Is.False);
            Assert.That(nonOutputView, Is.Null);
            Assert.That(nonOutputError, Does.Contain("没有稳定 NodeId 对应的 Output"));

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "missing-output", out ESAgentArtifactGenerationSpec missingView,
                out string missingError), Is.False);
            Assert.That(missingView, Is.Null);
            Assert.That(missingError, Does.Contain("missing-output"));

            source.outputs = source.outputs.Concat(new[]
            {
                new ESAgentGenerationOutput
                {
                    nodeId = "command-output",
                    artifactKind = ESAgentArtifactKind.AICommand,
                    artifactId = "es.command-output.duplicate",
                    artifactName = "command-output-duplicate"
                }
            }).ToArray();
            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "command-output", out ESAgentArtifactGenerationSpec duplicateView,
                out string duplicateError), Is.False);
            Assert.That(duplicateView, Is.Null);
            Assert.That(duplicateError, Does.Contain("不唯一"));
        }

        [Test]
        public void AgentAuthoring_ArtifactViewRejectsMissingOutputKind()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            source.outputs = new[] { source.outputs[0] };

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                ESAgentArtifactKind.AgentSkill, out ESAgentArtifactGenerationSpec skillView,
                out string error), Is.False);
            Assert.That(skillView, Is.Null);
            Assert.That(error, Does.Contain("Agent Skill Output"));
        }

        [Test]
        public void AgentAuthoring_TemporarySkillPromptCannotClaimInstallationOrPersistence()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                ESAgentArtifactKind.AgentSkill, out ESAgentArtifactGenerationSpec skillView,
                out string error), Is.True, error);

            string prompt = ESAgentArtifactGenerationWorkspace.BuildTemporarySkillExecutionPrompt(
                skillView, "skill-test");

            Assert.That(prompt, Does.Contain("仅在本次任务"));
            Assert.That(prompt, Does.Contain("不得安装、创建或更新 `.agents/skills`"));
            Assert.That(prompt, Does.Contain("不构成写入授权"));
            Assert.That(prompt, Does.Contain("不得声称该 Skill 已安装"));
            Assert.That(prompt, Does.Contain("skill-output"));
        }

        [Test]
        public void AgentAuthoring_ArtifactViewPreservesValidAnyOfGroup()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            ESAgentGenerationConstraint first = source.constraints.Single(item =>
                item.nodeId == "constraint-command");
            first.combinationMode = ESAgentConstraintCombinationMode.AnyOf;
            first.combinationGroup = "command-implementation";
            source.constraints = source.constraints.Concat(new[]
            {
                new ESAgentGenerationConstraint
                {
                    nodeId = "constraint-command-alternative",
                    kind = ESAgentConstraintKind.Required,
                    scope = ESAgentConstraintScope.Execution,
                    combinationMode = ESAgentConstraintCombinationMode.AnyOf,
                    combinationGroup = "command-implementation",
                    priority = 40,
                    statement = "使用另一条受控实现路径。",
                    rationale = "允许实现环境差异。",
                    verification = "任一路径均需提供相同证据。"
                }
            }).ToArray();
            first.scope = ESAgentConstraintScope.Execution;
            source.relations = source.relations.Concat(new[]
            {
                Relation("edge-command-alternative-1", "reference-command", "constraint-command-alternative"),
                Relation("edge-command-alternative-2", "constraint-command-alternative", "command-output")
            }).ToArray();
            RefreshBundle(source);

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "command-output", out ESAgentArtifactGenerationSpec view, out string error), Is.True, error);
            Assert.That(view.constraints.Count(item => item.combinationMode
                == ESAgentConstraintCombinationMode.AnyOf), Is.EqualTo(2));
            Assert.That(view.constraints.All(item => item.nodeId.Contains("command")), Is.True);
        }

        [Test]
        public void AgentAuthoring_ArtifactViewRejectsRelationSemanticTampering()
        {
            ESAgentArtifactGenerationSpec source = CreateArtifactSpec();
            ESAgentGenerationRelation relation = source.relations.Single(item =>
                item.edgeId == "edge-command-3");
            relation.semanticType = ESAgentGraphStableIds.ArtifactPort;

            Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(source,
                "command-output", out ESAgentArtifactGenerationSpec view, out string error), Is.False);
            Assert.That(view, Is.Null);
            Assert.That(error, Does.Contain("数据语义"));
        }

        private static ESAgentArtifactGenerationSpec CreateArtifactSpec()
        {
            var spec = new ESAgentArtifactGenerationSpec
            {
                sourceGraphId = "1234567890abcdef1234567890abcdef",
                sourceContentSignature = "artifact-view-signature",
                goal = new ESAgentGenerationGoal
                {
                    nodeId = "goal",
                    title = "整图复用",
                    objective = "把整张图作为可选择的 Command 或 Skill 使用。",
                    successCriteria = "所选分支完整且不扩大授权。"
                },
                references = new[]
                {
                    new ESAgentGenerationReference { nodeId = "reference-command", projectPath = "command.md" },
                    new ESAgentGenerationReference { nodeId = "reference-skill", projectPath = "skill.md" }
                },
                constraints = new[]
                {
                    new ESAgentGenerationConstraint
                    {
                        nodeId = "constraint-command", priority = 50, statement = "Command 约束",
                        rationale = "限定 Command 分支。", verification = "核对 Command 产物。"
                    },
                    new ESAgentGenerationConstraint
                    {
                        nodeId = "constraint-skill", priority = 50, statement = "Skill 约束",
                        rationale = "限定 Skill 分支。", verification = "核对 Skill 产物。"
                    }
                },
                outputs = new[]
                {
                    new ESAgentGenerationOutput
                    {
                        nodeId = "command-output",
                        artifactKind = ESAgentArtifactKind.AICommand,
                        artifactId = "es.command-output",
                        artifactName = "command-output",
                        commandIntent = ESAgentCommandIntent.ControlledExecution,
                        writeAuthorization = ESAgentWriteAuthorization.ConfirmBeforeWrite,
                        commandRiskLevel = ESAgentRiskLevel.L2,
                        preconditions = "command-preconditions"
                    },
                    new ESAgentGenerationOutput
                    {
                        nodeId = "skill-output",
                        artifactKind = ESAgentArtifactKind.AgentSkill,
                        artifactId = "es.skill-output",
                        artifactName = "skill-output",
                        skillDescription = "临时 Skill",
                        skillWorkflow = "执行工作流",
                        skillEffectKind = ESAgentSkillEffectKind.GuidanceOnly,
                        skillIdempotency = ESAgentSkillIdempotency.Required,
                        skillPermissionBoundary = "skill-permission-boundary"
                    }
                },
                validations = new[]
                {
                    new ESAgentGenerationValidation { nodeId = "validation-command", requireHumanApproval = true },
                    new ESAgentGenerationValidation { nodeId = "validation-skill", requireHumanApproval = true }
                },
                relations = new[]
                {
                    Relation("edge-command-1", "goal", "reference-command"),
                    Relation("edge-command-2", "reference-command", "constraint-command"),
                    Relation("edge-command-3", "constraint-command", "command-output"),
                    Relation("edge-command-4", "command-output", "validation-command"),
                    Relation("edge-skill-1", "goal", "reference-skill"),
                    Relation("edge-skill-2", "reference-skill", "constraint-skill"),
                    Relation("edge-skill-3", "constraint-skill", "skill-output"),
                    Relation("edge-skill-4", "skill-output", "validation-skill")
                }
            };
            RefreshBundle(spec);
            return spec;
        }

        private static void AddSecondCommandBranch(ESAgentArtifactGenerationSpec source)
        {
            source.references = source.references.Concat(new[]
            {
                new ESAgentGenerationReference
                {
                    nodeId = "reference-command-secondary",
                    projectPath = "command-secondary.md"
                }
            }).ToArray();
            source.constraints = source.constraints.Concat(new[]
            {
                new ESAgentGenerationConstraint
                {
                    nodeId = "constraint-command-secondary",
                    priority = 50,
                    statement = "第二个 Command 约束",
                    rationale = "限定第二个 Command 分支。",
                    verification = "核对第二个 Command 产物。"
                }
            }).ToArray();
            source.outputs = source.outputs.Concat(new[]
            {
                new ESAgentGenerationOutput
                {
                    nodeId = "command-output-secondary",
                    artifactKind = ESAgentArtifactKind.AICommand,
                    artifactId = "es.command-output.secondary",
                    artifactName = "command-output-secondary"
                }
            }).ToArray();
            source.validations = source.validations.Concat(new[]
            {
                new ESAgentGenerationValidation
                {
                    nodeId = "validation-command-secondary",
                    requireHumanApproval = true
                }
            }).ToArray();
            source.relations = source.relations.Concat(new[]
            {
                Relation("edge-command-secondary-1", "goal", "reference-command-secondary"),
                Relation("edge-command-secondary-2", "reference-command-secondary", "constraint-command-secondary"),
                Relation("edge-command-secondary-3", "constraint-command-secondary", "command-output-secondary"),
                Relation("edge-command-secondary-4", "command-output-secondary", "validation-command-secondary")
            }).ToArray();
            RefreshBundle(source);
        }

        private static ESAgentGenerationRelation Relation(string edgeId, string fromNodeId, string toNodeId)
        {
            ESAgentRelationKind relationKind;
            string semanticType;
            if ((fromNodeId ?? string.Empty).Contains("constraint"))
            {
                relationKind = ESAgentRelationKind.AppliesConstraint;
                semanticType = ESAgentGraphStableIds.RequirementPort;
            }
            else if ((fromNodeId ?? string.Empty).Contains("output"))
            {
                relationKind = ESAgentRelationKind.RequiresValidation;
                semanticType = ESAgentGraphStableIds.ArtifactPort;
            }
            else
            {
                relationKind = ESAgentRelationKind.ProvidesContext;
                semanticType = ESAgentGraphStableIds.ContextPort;
            }
            string sourcePortKey = relationKind == ESAgentRelationKind.AppliesConstraint
                ? ESAgentGraphStableIds.RequirementOutputPortKey
                : relationKind == ESAgentRelationKind.RequiresValidation
                    ? ESAgentGraphStableIds.ArtifactOutputPortKey
                    : ESAgentGraphStableIds.ContextOutputPortKey;
            string fromTypeId = NodeTypeId(fromNodeId);
            string toTypeId = NodeTypeId(toNodeId);
            ESGraphPortDefinition sourcePort = GetAgentPort(fromTypeId, sourcePortKey);
            ESGraphPortDefinition targetPort = GetAgentPort(toTypeId, ESGraphBuiltInPortKeys.Input);
            return new ESAgentGenerationRelation
            {
                edgeId = edgeId,
                fromNodeId = fromNodeId,
                fromNodeTypeId = fromTypeId,
                fromNodeTitle = fromNodeId,
                fromPortStableKey = sourcePortKey,
                fromPortMeaning = ESGraphEndpointRules.ResolveMeaning(sourcePort.meaning,
                    sourcePort.name, sourcePort.stableKey),
                toNodeId = toNodeId,
                toNodeTypeId = toTypeId,
                toNodeTitle = toNodeId,
                toPortStableKey = ESGraphBuiltInPortKeys.Input,
                toPortMeaning = ESGraphEndpointRules.ResolveMeaning(targetPort.meaning,
                    targetPort.name, targetPort.stableKey),
                relationKind = relationKind,
                semanticType = semanticType,
                sourceValueTypeId = sourcePort.valueTypeId,
                targetValueTypeId = targetPort.valueTypeId,
                sourceAggregation = ESGraphPortAggregationRules.Resolve(sourcePort.direction,
                    sourcePort.capacity, sourcePort.aggregation),
                targetAggregation = ESGraphPortAggregationRules.Resolve(targetPort.direction,
                    targetPort.capacity, targetPort.aggregation)
            };
        }

        private static ESGraphPortDefinition GetAgentPort(string nodeTypeId, string portKey)
        {
            Assert.That(ESGraphAuthoringRegistry.TryGetNodeDefinition(ESAgentGraphStableIds.DomainId,
                nodeTypeId, out IESGraphNodeDefinition definition), Is.True,
                "测试需要已注册 Agent 节点定义：" + nodeTypeId);
            ESGraphPortDefinition[] matches = definition.Ports.Where(port => port != null
                && port.stableKey == portKey).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), nodeTypeId + "/" + portKey);
            return matches[0];
        }

        private static string NodeTypeId(string nodeId)
        {
            if (string.Equals(nodeId, "goal", StringComparison.Ordinal))
                return ESAgentGraphStableIds.GoalNode;
            if ((nodeId ?? string.Empty).Contains("reference"))
                return ESAgentGraphStableIds.ReferenceNode;
            if ((nodeId ?? string.Empty).Contains("constraint"))
                return ESAgentGraphStableIds.ConstraintNode;
            if ((nodeId ?? string.Empty).Contains("validation"))
                return ESAgentGraphStableIds.ValidationNode;
            if ((nodeId ?? string.Empty).Contains("skill-output"))
                return ESAgentGraphStableIds.AISkillOutputNode;
            return ESAgentGraphStableIds.AICommandOutputNode;
        }

        private static void RefreshBundle(ESAgentArtifactGenerationSpec spec)
        {
            spec.skillBundle = ESAgentSkillBundleContract.Create(spec.sourceGraphId,
                spec.goal.title, spec.goal.nodeId, spec.references, spec.constraints,
                spec.branches, spec.traversals, spec.outputs, spec.validations);
        }

        private static ESGraphAssetBase CreateValidGraph(out ESGraphNodeRecord outputNode)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            ESGraphNodeRecord goal = AddFromProfile(graph, ESAgentGraphStableIds.GoalNode, new Vector2(0f, 0f));
            ESGraphNodeRecord reference = AddFromProfile(graph, ESAgentGraphStableIds.ReferenceNode, new Vector2(180f, 0f));
            ESGraphNodeRecord constraint = AddFromProfile(graph, ESAgentGraphStableIds.ConstraintNode, new Vector2(360f, 0f));
            outputNode = AddFromProfile(graph, ESAgentGraphStableIds.AICommandOutputNode, new Vector2(540f, 0f));
            ESGraphNodeRecord validation = AddFromProfile(graph, ESAgentGraphStableIds.ValidationNode, new Vector2(720f, 0f));
            var goalPayload = new ESAgentGoalPayload
            {
                title = "Graph Authoring",
                objective = "通过 Graph Authoring 生成 AICommand 候选",
                successCriteria = "Graph Authoring 候选可审查、可验证并保持权限边界。"
            };
            graph.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(goalPayload), out _);
            var outputPayload = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(outputNode.payloadJson);
            outputPayload.commandName = "生成_Graph_Authoring_AI命令";
            outputPayload.targetProjectPath = "Assets/Plugins/ES/AICommands/生成_Graph_Authoring_AI命令.md";
            outputPayload.purpose = "生成与 Graph Authoring 目标一致的 AICommand 候选。";
            outputPayload.acceptanceCriteria = "候选必须覆盖 Graph Authoring 目标并通过人工 Diff Review。";
            graph.UpdateNode(outputNode.nodeId, outputNode.typeId, outputNode.version, outputNode.title,
                JsonUtility.ToJson(outputPayload), out _);
            Connect(graph, goal, reference); Connect(graph, reference, constraint); Connect(graph, constraint, outputNode); Connect(graph, outputNode, validation);
            return graph;
        }

        private static ESGraphAssetBase CreateSemanticRiskGraph(out ESGraphNodeRecord outputNode)
        {
            ESGraphAssetBase graph = CreateValidGraph(out outputNode);
            ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                node.typeId == ESAgentGraphStableIds.GoalNode);
            var goalPayload = new ESAgentGoalPayload
            {
                title = "审查字体资产工具",
                objective = "审查字体导入、预览和缺失引用。",
                successCriteria = "字体资产问题可以定位并给出修复证据。"
            };
            Assert.That(graph.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title,
                JsonUtility.ToJson(goalPayload), out string updateError), Is.True, updateError);
            return graph;
        }

        private static ESGraphNodeRecord AddFromProfile(ESGraphAssetBase graph, string nodeTypeId,
            Vector2 position)
        {
            var profile = new ESAgentAuthoringGraphProfile();
            IESGraphNodeDefinition definition = profile.NodeDefinitions.First(item =>
                item.NodeType.StableId == nodeTypeId);
            ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName, position,
                definition.Ports);
            graph.UpdateNode(node.nodeId, definition.NodeType, definition.CurrentVersion, node.title,
                definition.CreateDefaultPayload(), out _);
            return node;
        }

        private static void Connect(ESGraphAssetBase graph, ESGraphNodeRecord from, ESGraphNodeRecord to)
        {
            ESGraphPortRecord[] outputs = from.ports.Where(port =>
                port.direction == ESGraphPortDirection.Output).ToArray();
            ESGraphPortRecord[] inputs = to.ports.Where(port =>
                port.direction == ESGraphPortDirection.Input).ToArray();
            Assert.That(outputs, Has.Length.EqualTo(1), "测试连接必须显式选择唯一输出端点。");
            Assert.That(inputs, Has.Length.EqualTo(1), "测试连接必须显式选择唯一输入端点。");
            ESGraphPortRecord output = outputs[0];
            ESGraphPortRecord input = inputs[0];
            Assert.That(graph.TryAddEdge(output.portId, input.portId, out _, out string error), Is.True, error);
        }

        private static void ConnectEndpoints(ESGraphAssetBase graph, ESGraphNodeRecord from,
            string outputPortKey, ESGraphNodeRecord to, string inputPortKey)
        {
            ESGraphPortRecord output = from.ports.Single(port =>
                port.direction == ESGraphPortDirection.Output
                && string.Equals(port.stableKey, outputPortKey, StringComparison.Ordinal));
            ESGraphPortRecord input = to.ports.Single(port =>
                port.direction == ESGraphPortDirection.Input
                && string.Equals(port.stableKey, inputPortKey, StringComparison.Ordinal));
            Assert.That(graph.TryAddEdge(output.portId, input.portId, out _, out string error),
                Is.True, error);
        }

        private static FakeArtifactFileIO CreateTwoExistingFileTransaction(
            out ESAgentArtifactFileOperation[] operations)
        {
            var files = new FakeArtifactFileIO();
            files.Seed("source-1", "new-1");
            files.Seed("source-2", "new-2");
            files.Seed("target-1", "old-1");
            files.Seed("target-2", "old-2");
            operations = new[]
            {
                new ESAgentArtifactFileOperation
                {
                    SourcePath = "source-1", TargetPath = "target-1", BackupPath = "backup-1"
                },
                new ESAgentArtifactFileOperation
                {
                    SourcePath = "source-2", TargetPath = "target-2", BackupPath = "backup-2"
                }
            };
            return files;
        }

        private static string GetProjectRootForTests()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static bool TryBakeSpec(ESGraphAssetBase graph,
            out ESAgentArtifactGenerationSpec spec, out string error)
        {
            spec = null;
            if (!ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues))
            {
                error = Describe(graphIssues);
                return false;
            }
            if (!new ESAgentArtifactGenerationBaker().TryBake(snapshot, out spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues))
            {
                error = Describe(bakeIssues);
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static void WriteRequest(string fullDirectory, string requestId, string relativeDirectory,
            ESAgentArtifactGenerationSpec spec)
        {
            var request = new ESAgentArtifactGenerationRequest
            {
                requestId = requestId,
                createdAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                requestDirectory = relativeDirectory,
                candidateDirectory = relativeDirectory + "/candidate",
                spec = spec
            };
            ESAgentArtifactGenerationWorkspace.WriteUtf8(
                Path.Combine(fullDirectory, "generation-request.json"), JsonUtility.ToJson(request, true));
        }

        private static ESAgentArtifactCandidateManifest WriteValidAICommandCandidate(string fullDirectory,
            ESAgentArtifactGenerationRequest request)
        {
            ESAgentGenerationOutput output = request.spec.outputs.Single(item =>
                item.artifactKind == ESAgentArtifactKind.AICommand);
            const string candidateRelativePath = "candidate/command.md";
            string candidatePath = Path.Combine(fullDirectory, "candidate", "command.md");
            Directory.CreateDirectory(Path.GetDirectoryName(candidatePath));
            var builder = new StringBuilder();
            builder.AppendLine("# " + output.artifactName);
            builder.AppendLine(ESAgentArtifactGenerationWorkspace.BuildArtifactIdentityMarker(
                output.artifactId));
            builder.AppendLine("命令类型：" + output.commandType);
            builder.AppendLine("默认改文件：" + output.defaultWrite);
            builder.AppendLine("风险等级：" + output.riskLevel);
            AppendContract(builder, "输入契约：", output.expectedInputs);
            AppendContract(builder, "前置条件：", output.preconditions);
            AppendContract(builder, "允许写入范围：", output.allowedWriteScopes);
            AppendContract(builder, "禁止操作：", output.forbiddenOperations);
            AppendContract(builder, "执行步骤：", output.executionOutline);
            AppendContract(builder, "完成定义：", output.acceptanceCriteria);
            AppendContract(builder, "证据要求：", output.requiredEvidence);
            AppendContract(builder, "阻断处理：", output.blockedHandling);
            AppendContract(builder, "回滚策略：", output.rollbackStrategy);
            AppendContract(builder, "Goal 标题：", request.spec.goal.title);
            AppendContract(builder, "Goal 最终目的：", request.spec.goal.objective);
            AppendContract(builder, "Goal 成功标准：", request.spec.goal.successCriteria);
            AppendContract(builder, "Goal 上下文：", request.spec.goal.context);
            AppendContract(builder, "Goal 使用者：", request.spec.goal.targetUsers);
            foreach (ESAgentGenerationReference reference in request.spec.references
                ?? Array.Empty<ESAgentGenerationReference>())
            {
                AppendContract(builder, "Reference 路径：", reference.projectPath);
                AppendContract(builder, "Reference 用途：", reference.purpose);
            }
            foreach (ESAgentGenerationConstraint constraint in request.spec.constraints
                ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                AppendContract(builder, "Constraint 规则：", constraint.statement);
                AppendContract(builder, "Constraint 原因：", constraint.rationale);
                AppendContract(builder, "Constraint 验证：", constraint.verification);
            }
            foreach (ESAgentGenerationBranch branch in request.spec.branches
                ?? Array.Empty<ESAgentGenerationBranch>())
            {
                AppendContract(builder, "Branch 条件：", branch.condition);
                AppendContract(builder, "Branch 命中：", branch.matchedPath);
                AppendContract(builder, "Branch 默认：", branch.defaultPath);
                AppendContract(builder, "Branch 失败：", branch.failurePath);
            }
            foreach (ESAgentGenerationTraversal traversal in request.spec.traversals
                ?? Array.Empty<ESAgentGenerationTraversal>())
            {
                AppendContract(builder, "Traversal 目标：", traversal.target);
                AppendContract(builder, "Traversal 元素：", traversal.itemAlias);
                builder.AppendLine("maxDepth=" + traversal.maxDepth);
                builder.AppendLine("maxItems=" + traversal.maxItems);
                AppendContract(builder, "Traversal 停止：", traversal.stopCondition);
                AppendContract(builder, "Traversal 空结果：", traversal.emptyResultAction);
                AppendContract(builder, "Traversal 失败：", traversal.failureAction);
            }
            foreach (ESAgentGenerationValidation validation in request.spec.validations
                ?? Array.Empty<ESAgentGenerationValidation>())
            {
                AppendContract(builder, "Validation 附加要求：", validation.additionalRequirements);
                AppendContract(builder, "Validation 审查清单：", validation.reviewChecklist);
            }
            ESAgentArtifactGenerationWorkspace.WriteUtf8(candidatePath, builder.ToString());
            ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(fullDirectory,
                "validation-report.md"), "# Validation\n\n已执行严格 UTF-8、Graph 语义和 Manifest 覆盖检查。\n");
            var manifest = new ESAgentArtifactCandidateManifest
            {
                requestId = request.requestId,
                summary = "与当前 Graph 语义一致的测试候选。",
                files = new[]
                {
                    new ESAgentArtifactCandidateFile
                    {
                        artifactKind = ESAgentArtifactKind.AICommand,
                        candidateRelativePath = candidateRelativePath,
                        targetProjectPath = output.targetProjectPath,
                        summary = "AICommand 主产物"
                    }
                }
            };
            ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(fullDirectory,
                "candidate-manifest.json"), JsonUtility.ToJson(manifest, true));
            return manifest;
        }

        private static void AppendContract(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                builder.AppendLine(label + value);
        }

        [Serializable]
        private sealed class AICommandPayloadV1
        {
            public int schemaVersion = 1;
            public string commandName;
            public string targetProjectPath;
            public ESAgentArtifactOperationMode operationMode;
            public string commandType;
            public string defaultWrite;
            public string riskLevel;
            public string purpose;
            public string expectedInputs;
            public string executionOutline;
            public string acceptanceCriteria;
            public string requiredSections;
        }

        [Serializable]
        private sealed class SkillPayloadV1
        {
            public int schemaVersion = 1;
            public string skillName;
            public string targetProjectPath;
            public ESAgentArtifactOperationMode operationMode;
            public string description;
            public string triggerScenarios;
            public string workflow;
            public string nonGoals;
            public string validationSteps;
            public string defaultPrompt;
            public bool includeAgentsMetadata = false;
            public bool includeReferences;
            public bool includeScripts = false;
        }

        [Serializable]
        private sealed class ConstraintPayloadV1
        {
            public int schemaVersion = 1;
            public ESAgentConstraintKind kind;
            public string statement;
            public string rationale;
            public string verification;
        }

        [Serializable]
        private sealed class TestGraphDispatchEnvelope
        {
            public string runId;
            public string requestId;
            public string graphId;
            public string contentSignature;
            public string requestDirectory;
            public string operationKind;
        }

        private sealed class FakeArtifactFileIO : IESAgentArtifactFileIO
        {
            private readonly Dictionary<string, string> files =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public readonly HashSet<string> FailCopies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> FailDeletes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, Queue<string>> HashResults =
                new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> CopyAttempts = new List<string>();

            public void Seed(string path, string content) { files[path] = content; }
            public string Read(string path) => files.TryGetValue(path, out string value) ? value : null;
            public bool FileExists(string path) => files.ContainsKey(path);

            public void CopyAtomically(string sourcePath, string targetPath)
            {
                string key = CopyKey(sourcePath, targetPath);
                CopyAttempts.Add(key);
                if (FailCopies.Contains(key))
                    throw new IOException("模拟复制失败：" + key);
                if (!files.TryGetValue(sourcePath, out string content))
                    throw new FileNotFoundException("模拟源文件不存在。", sourcePath);
                files[targetPath] = content;
            }

            public void DeleteFile(string path)
            {
                if (FailDeletes.Contains(path))
                    throw new IOException("模拟删除失败：" + path);
                files.Remove(path);
            }

            public string ComputeSha256(string path)
            {
                if (HashResults.TryGetValue(path, out Queue<string> results) && results.Count > 0)
                    return results.Dequeue();
                if (!files.TryGetValue(path, out string content))
                    throw new FileNotFoundException("模拟哈希目标不存在。", path);
                return content;
            }

            public static string CopyKey(string sourcePath, string targetPath)
            {
                return sourcePath + " -> " + targetPath;
            }
        }

        [TestCase(ESAgentAuthoringPresetKind.Paired)]
        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly)]
        [TestCase(ESAgentAuthoringPresetKind.MindMapPaired)]
        [TestCase(ESAgentAuthoringPresetKind.SceneScanReview)]
        [TestCase(ESAgentAuthoringPresetKind.SceneQualityReview)]
        public void AgentAuthoring_AllBuiltInTemplatesBakeThroughTheirDeclaredMode(
            ESAgentAuthoringPresetKind kind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, kind);
                Assert.DoesNotThrow(() => ESAgentAuthoringGraphPreset.EnsureTemplateCanBake(graph, kind));
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out _,
                    out IESBakedGraphPlan plan, out List<ESGraphValidationIssue> issues),
                    Is.True, Describe(issues));
                bool executionTemplate = kind == ESAgentAuthoringPresetKind.SceneScanReview
                    || kind == ESAgentAuthoringPresetKind.SceneQualityReview;
                Assert.That(plan is ESAISkillExecutionSpec, Is.EqualTo(executionTemplate));
                Assert.That(plan is ESAgentArtifactGenerationSpec, Is.EqualTo(!executionTemplate));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(ESAgentAuthoringPresetKind.Paired)]
        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly)]
        [TestCase(ESAgentAuthoringPresetKind.MindMapPaired)]
        [TestCase(ESAgentAuthoringPresetKind.SceneScanReview)]
        [TestCase(ESAgentAuthoringPresetKind.SceneQualityReview)]
        public void AgentAuthoring_AllBuiltInTemplatesUseRepresentativeRoutedOutcomes(
            ESAgentAuthoringPresetKind kind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, kind);
                bool executionTemplate = kind == ESAgentAuthoringPresetKind.SceneScanReview
                    || kind == ESAgentAuthoringPresetKind.SceneQualityReview;
                string ownerTypeId = executionTemplate
                    ? ESAgentGraphStableIds.SkillTaskNode
                    : ESAgentGraphStableIds.BranchNode;
                string[] routeKeys = executionTemplate
                    ? new[]
                    {
                        ESAgentGraphStableIds.SkillSuccessPortKey,
                        ESAgentGraphStableIds.SkillFailurePortKey,
                        ESAgentGraphStableIds.SkillTimeoutPortKey,
                        ESAgentGraphStableIds.SkillCancelledPortKey
                    }
                    : new[]
                    {
                        ESAgentGraphStableIds.BranchMatchedPortKey,
                        ESAgentGraphStableIds.BranchDefaultPortKey,
                        ESAgentGraphStableIds.BranchFailurePortKey
                    };
                ESGraphNodeRecord outcomeOwner = graph.Nodes.Single(node =>
                    node.typeId == ownerTypeId);
                ESGraphNodeTopology topology =
                    ESGraphTopologyAnalyzer.Analyze(outcomeOwner, graph.Nodes, graph.Edges);
                Assert.That(topology.IsMultiEndpointNode, Is.True,
                    kind + " 模板的业务结果节点必须拥有多个独立稳定端点。");

                var routedTargetNodeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string routeKey in routeKeys)
                {
                    Assert.That(outcomeOwner.TryGetPort(routeKey,
                        out ESGraphPortRecord routePort), Is.True,
                        kind + " 模板缺少业务结果端点：" + routeKey);
                    Assert.That(routePort.capacity, Is.EqualTo(executionTemplate
                            ? ESGraphPortCapacity.Single
                            : ESGraphPortCapacity.Multi),
                        executionTemplate
                            ? "互斥执行结果必须各自使用独立 Single 端点。"
                            : "生成决策端点可以分发给多条约束，但容量不能代替独立结果端点。" );
                    ESGraphEdgeRecord[] routes = graph.Edges.Where(edge =>
                        edge.outputPortId == routePort.portId).ToArray();
                    Assert.That(routes, Has.Length.EqualTo(1),
                        kind + " 模板的每个业务结果必须且只能路由到一个明确目标：" + routeKey);
                    Assert.That(graph.TryFindPort(routes[0].inputPortId,
                        out ESGraphNodeRecord targetNode, out _), Is.True);
                    routedTargetNodeIds.Add(targetNode.nodeId);
                }
                Assert.That(routedTargetNodeIds.Count, Is.EqualTo(routeKeys.Length),
                    kind + " 模板不能把不同业务结果回接到同一目标来伪装多端口。");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(ESAgentAuthoringPresetKind.Paired)]
        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly)]
        [TestCase(ESAgentAuthoringPresetKind.MindMapPaired)]
        public void AgentAuthoring_GenerationTemplatesBakeDistinctBranchEndpointsAndArtifactViews(
            ESAgentAuthoringPresetKind kind)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, kind);
                Assert.That(TryBakeSpec(graph, out ESAgentArtifactGenerationSpec spec,
                    out string bakeError), Is.True, bakeError);
                ESAgentGenerationBranch branch = spec.branches.Single();
                string[] branchTargets = branch.matchedTargetNodeIds
                    .Concat(branch.defaultTargetNodeIds)
                    .Concat(branch.failureTargetNodeIds).ToArray();
                Assert.That(branchTargets.Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(branchTargets.Length),
                    "三个分支端点必须指向不同职责节点，不能回接同一目标伪装多端点。");
                foreach (ESAgentGenerationOutput output in spec.outputs)
                    Assert.That(ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(
                        spec, output.nodeId, out _, out string viewError), Is.True, viewError);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void GraphNodeBody_UsesCollapsedFoldoutWithoutOwningThePortContainers()
        {
            var body = new VisualElement { name = "test-node-body" };
            Foldout foldout = ESStableGraphNodeView.CreateBodyFoldout(body);

            Assert.That(foldout.name, Is.EqualTo("es-node-body-foldout"));
            Assert.That(foldout.value, Is.False);
            Assert.That(foldout.Q<VisualElement>("test-node-body"), Is.SameAs(body));
            Assert.That(foldout.tooltip, Does.Contain("端口和连接关系始终保持可见"));

            Foldout restored = ESStableGraphNodeView.CreateBodyFoldout(
                new VisualElement(), true);
            Assert.That(restored.value, Is.True,
                "节点视图重建时必须能恢复当前 Editor 会话中的展开状态。");
        }

        [Test]
        public void AISkillExecution_SceneScanReviewTemplateBakesCompleteWorkflow()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneScanReview(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(8));
                Assert.That(graph.Edges.Count, Is.EqualTo(14));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAISkillExecutionBaker().TryBake(snapshot,
                    out ESAISkillExecutionSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> executionIssues), Is.True,
                    Describe(executionIssues));
                Assert.That(spec.skillId, Is.EqualTo("es.skill.scene-scan-review"));
                Assert.That(spec.steps.Count(step => step.output != null), Is.EqualTo(5));
                Assert.That(spec.controlEdges.Any(edge =>
                    edge.sourcePortKey == ESAgentGraphStableIds.SkillApprovedPortKey), Is.True);
                Assert.That(spec.controlEdges.Any(edge =>
                    edge.sourcePortKey == ESAgentGraphStableIds.SkillRejectedPortKey), Is.True);
                Assert.That(spec.controlEdges.Any(edge =>
                    edge.sourcePortKey == ESAgentGraphStableIds.SkillTimeoutPortKey), Is.True);
                Assert.That(spec.controlEdges.Any(edge =>
                    edge.sourcePortKey == ESAgentGraphStableIds.SkillCancelledPortKey), Is.True);
                Assert.That(spec.dataBindings.Any(edge =>
                    edge.sourcePortKey == ESAgentGraphStableIds.SkillParametersPortKey
                    && edge.targetPortKey == ESAgentGraphStableIds.SkillInputPortKey), Is.True,
                    "场景参数必须通过真实值端点进入任务，不能只从 WorkflowParameter 旁路读取。");
                ESAISkillExecutionStep scan = spec.steps.Single(step => step.task != null);
                Assert.That(scan.task.inputBindings, Has.Length.EqualTo(3));
                Assert.That(scan.task.inputBindings.All(binding =>
                    binding.source == ESAISkillTaskInputSource.BoundValue
                    && binding.sourceId == ESAgentGraphStableIds.SkillInputPortKey), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_SceneQualityReviewUsesBusinessOutcomesBeforeTopologyFeatures()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneQualityReview(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(10));
                Assert.That(graph.Edges.Count, Is.EqualTo(18));

                ESGraphNodeRecord input = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.SkillInputNode);
                Assert.That(input.ports, Is.Not.Empty);
                Assert.That(input.ports.All(port =>
                    port.direction == ESGraphPortDirection.Output), Is.True,
                    "参数入口必须是可执行合同中的纯输出节点。");

                ESGraphNodeRecord task = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.SkillTaskNode);
                ESGraphNodeTopology taskTopology =
                    ESGraphTopologyAnalyzer.Analyze(task, graph.Nodes, graph.Edges);
                Assert.That(taskTopology.InputEndpointCount, Is.EqualTo(2));
                Assert.That(taskTopology.OutputEndpointCount, Is.EqualTo(5));
                Assert.That(taskTopology.IsMultiEndpointNode, Is.True,
                    "Task 的成功、失败、超时、取消和值结果是实际执行合同，不是展示性端口。");
                Assert.That(task.TryGetPort(ESAgentGraphStableIds.SkillRunResultPortKey,
                    out ESGraphPortRecord resultPort), Is.True);
                Assert.That(resultPort.capacity, Is.EqualTo(ESGraphPortCapacity.Multi));
                Assert.That(graph.Edges.Count(edge => edge.outputPortId == resultPort.portId),
                    Is.EqualTo(8), "同一个结果端点可以服务审查与终态，这是单端口多连接。" );

                ESGraphNodeRecord contentReview = graph.Nodes.Single(node =>
                    node.title == "内容完整性审查");
                ESGraphNodeRecord evidenceReview = graph.Nodes.Single(node =>
                    node.title == "证据完整性审查");
                ESGraphNodeRecord contentRejected = graph.Nodes.Single(node =>
                    node.title == "内容审查未通过的扫描结果");
                ESGraphNodeRecord evidenceRejected = graph.Nodes.Single(node =>
                    node.title == "证据审查未通过的扫描结果");
                ESGraphNodeRecord completed = graph.Nodes.Single(node =>
                    node.title == "双审通过的扫描结果");

                Assert.That(contentReview.TryGetPort(ESAgentGraphStableIds.SkillApprovedPortKey,
                    out ESGraphPortRecord contentApprovedPort), Is.True);
                Assert.That(contentReview.TryGetPort(ESAgentGraphStableIds.SkillRejectedPortKey,
                    out ESGraphPortRecord contentRejectedPort), Is.True);
                Assert.That(evidenceReview.TryGetPort(ESAgentGraphStableIds.SkillApprovedPortKey,
                    out ESGraphPortRecord evidenceApprovedPort), Is.True);
                Assert.That(evidenceReview.TryGetPort(ESAgentGraphStableIds.SkillRejectedPortKey,
                    out ESGraphPortRecord evidenceRejectedPort), Is.True);
                Assert.That(graph.Edges.Single(edge => edge.outputPortId == contentApprovedPort.portId)
                    .inputPortId, Is.EqualTo(evidenceReview.ports.Single(port =>
                        port.stableKey == ESGraphBuiltInPortKeys.Input).portId));
                Assert.That(graph.Edges.Single(edge => edge.outputPortId == contentRejectedPort.portId)
                    .inputPortId, Is.EqualTo(contentRejected.ports.Single(port =>
                        port.stableKey == ESGraphBuiltInPortKeys.Input).portId));
                Assert.That(graph.Edges.Single(edge => edge.outputPortId == evidenceApprovedPort.portId)
                    .inputPortId, Is.EqualTo(completed.ports.Single(port =>
                        port.stableKey == ESGraphBuiltInPortKeys.Input).portId));
                Assert.That(graph.Edges.Single(edge => edge.outputPortId == evidenceRejectedPort.portId)
                    .inputPortId, Is.EqualTo(evidenceRejected.ports.Single(port =>
                        port.stableKey == ESGraphBuiltInPortKeys.Input).portId));

                ESGraphNodeRecord[] outputs = graph.Nodes.Where(node =>
                    node.typeId == ESAgentGraphStableIds.SkillOutputNode).ToArray();
                Assert.That(outputs.Length, Is.EqualTo(6));
                Assert.That(outputs.All(node => node.ports.Count == 2
                    && node.ports.All(port => port.direction == ESGraphPortDirection.Input)),
                    Is.True, "结构化终态必须是只接收控制和值的纯输入节点。");

                Assert.That(ESGraphSnapshotBaker.TryBake(graph,
                    out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True,
                    Describe(graphIssues));
                Assert.That(new ESAISkillExecutionBaker().TryBake(snapshot,
                    out ESAISkillExecutionSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> executionIssues), Is.True,
                    Describe(executionIssues));
                Assert.That(spec.schemaVersion,
                    Is.EqualTo(ESAISkillExecutionSpec.CurrentSchemaVersion));
                Assert.That(spec.skillId, Is.EqualTo("es.skill.scene-quality-review"));
                Assert.That(spec.controlEdges.Length, Is.EqualTo(9));
                Assert.That(spec.dataBindings.Length, Is.EqualTo(9));
                Assert.That(spec.fanOutJoinPairs, Is.Empty,
                    "用户模板不应为展示 FanOut/Join 而改变审查通过与拒绝的业务语义。");
                Assert.That(spec.steps.All(step => step.ports.Length == graph.Nodes
                    .Single(node => node.nodeId == step.nodeId).ports.Count), Is.True,
                    "Bake 必须保留每个节点的完整稳定端口合同。");
                Assert.That(ESAISkillExecutionBaker.TryValidateSpec(spec,
                    out string validationError), Is.True, validationError);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_BranchUsesTwoIndependentSingleRoutes()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESGraphNodeRecord input = AddFromProfile(graph,
                    ESAgentGraphStableIds.SkillInputNode, new Vector2(0f, 120f));
                ESGraphNodeRecord branch = AddFromProfile(graph,
                    ESAgentGraphStableIds.SkillBranchNode, new Vector2(240f, 120f));
                ESGraphNodeRecord matched = AddFromProfile(graph,
                    ESAgentGraphStableIds.SkillOutputNode, new Vector2(520f, 40f));
                ESGraphNodeRecord fallback = AddFromProfile(graph,
                    ESAgentGraphStableIds.SkillOutputNode, new Vector2(520f, 240f));

                ConnectEndpoints(graph, input, ESAgentGraphStableIds.SkillNextPortKey,
                    branch, ESGraphBuiltInPortKeys.Input);
                ConnectEndpoints(graph, input, ESAgentGraphStableIds.SkillParametersPortKey,
                    branch, ESAgentGraphStableIds.SkillInputPortKey);
                ConnectEndpoints(graph, branch, ESAgentGraphStableIds.SkillMatchedPortKey,
                    matched, ESGraphBuiltInPortKeys.Input);
                ConnectEndpoints(graph, branch, ESAgentGraphStableIds.SkillDefaultPortKey,
                    fallback, ESGraphBuiltInPortKeys.Input);
                ConnectEndpoints(graph, input, ESAgentGraphStableIds.SkillParametersPortKey,
                    matched, ESAgentGraphStableIds.SkillInputPortKey);
                ConnectEndpoints(graph, input, ESAgentGraphStableIds.SkillParametersPortKey,
                    fallback, ESAgentGraphStableIds.SkillInputPortKey);

                List<ESGraphValidationIssue> validation = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(validation.Any(IsError), Is.False, Describe(validation));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAISkillExecutionBaker().TryBake(snapshot,
                    out ESAISkillExecutionSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> executionIssues), Is.True,
                    Describe(executionIssues));

                ESAISkillControlEdge[] matchedRoutes = spec.controlEdges.Where(edge =>
                    edge.sourceNodeId == branch.nodeId
                    && edge.sourcePortKey == ESAgentGraphStableIds.SkillMatchedPortKey).ToArray();
                Assert.That(matchedRoutes.Length, Is.EqualTo(1));
                Assert.That(matchedRoutes.All(edge => ESGraphIdentity.IsValid(edge.edgeId)
                    && ESGraphIdentity.IsValid(edge.sourcePortId)
                    && ESGraphIdentity.IsValid(edge.targetPortId)
                    && edge.targetPortKey == ESGraphBuiltInPortKeys.Input
                    && ESGraphEndpointRules.IsValidMeaning(edge.sourceMeaning)
                    && ESGraphEndpointRules.IsValidMeaning(edge.targetMeaning)), Is.True);
                Assert.That(spec.controlEdges.Count(edge => edge.sourceNodeId == branch.nodeId
                    && edge.sourcePortKey == ESAgentGraphStableIds.SkillDefaultPortKey), Is.EqualTo(1));
                Assert.That(branch.TryGetPort(ESAgentGraphStableIds.SkillMatchedPortKey,
                    out ESGraphPortRecord matchedPort), Is.True);
                Assert.That(branch.TryGetPort(ESAgentGraphStableIds.SkillDefaultPortKey,
                    out ESGraphPortRecord defaultPort), Is.True);
                Assert.That(matchedPort.capacity, Is.EqualTo(ESGraphPortCapacity.Single));
                Assert.That(defaultPort.capacity, Is.EqualTo(ESGraphPortCapacity.Single));
                Assert.That(ESAISkillExecutionCoordinator.TryResolveSingleRouteTarget(spec,
                    branch.nodeId, ESAgentGraphStableIds.SkillMatchedPortKey,
                    out string matchedTargetId, out string matchedError), Is.True, matchedError);
                Assert.That(matchedTargetId, Is.EqualTo(matched.nodeId));
                Assert.That(ESAISkillExecutionCoordinator.TryResolveSingleRouteTarget(spec,
                    branch.nodeId, ESAgentGraphStableIds.SkillDefaultPortKey,
                    out string defaultTargetId, out string defaultError), Is.True, defaultError);
                Assert.That(defaultTargetId, Is.EqualTo(fallback.nodeId));
                ESAISkillExecutionStep branchStep = spec.steps.Single(step => step.nodeId == branch.nodeId);
                branchStep.branch.valuePath = "approved";
                branchStep.branch.expectedValue = "yes";
                branchStep.branch.ignoreCase = true;
                Assert.That(ESAISkillExecutionCoordinator.TrySelectBranchTarget(spec, branchStep,
                    new JObject { ["approved"] = "YES" }, out string selectedMatchedTarget,
                    out bool matchedResult, out _, out string matchedSelectionError), Is.True,
                    matchedSelectionError);
                Assert.That(matchedResult, Is.True);
                Assert.That(selectedMatchedTarget, Is.EqualTo(matched.nodeId));
                Assert.That(ESAISkillExecutionCoordinator.TrySelectBranchTarget(spec, branchStep,
                    new JObject { ["approved"] = "NO" }, out string selectedDefaultTarget,
                    out bool defaultResult, out _, out string defaultSelectionError), Is.True,
                    defaultSelectionError);
                Assert.That(defaultResult, Is.False);
                Assert.That(selectedDefaultTarget, Is.EqualTo(fallback.nodeId));
                Assert.That(spec.dataBindings, Is.Not.Empty);
                Assert.That(spec.dataBindings.All(binding =>
                    ESGraphIdentity.IsValid(binding.sourcePortId)
                    && ESGraphIdentity.IsValid(binding.targetPortId)
                    && ESGraphStableIdUtility.IsValid(binding.sourcePortKey)
                    && ESGraphStableIdUtility.IsValid(binding.targetPortKey)
                    && ESGraphEndpointRules.IsValidMeaning(binding.sourceMeaning)
                    && ESGraphEndpointRules.IsValidMeaning(binding.targetMeaning)
                    && ESGraphPortValueCatalog.IsValidStableId(binding.sourceValueTypeId)
                    && ESGraphPortValueCatalog.IsValidStableId(binding.targetValueTypeId)
                    && binding.targetAggregation != ESGraphPortAggregation.Auto), Is.True);

                spec.controlEdges = spec.controlEdges.Concat(new[]
                {
                    CreateControlEdge(ESGraphIdentity.NewId(), spec.controlEdges.Max(edge => edge.order) + 1,
                        branch.nodeId,
                        ESAgentGraphStableIds.SkillMatchedPortKey, fallback.nodeId)
                }).ToArray();
                Assert.That(ESAISkillExecutionCoordinator.TryResolveSingleRouteTarget(spec,
                    branch.nodeId, ESAgentGraphStableIds.SkillMatchedPortKey,
                    out _, out string duplicateRouteError), Is.False);
                Assert.That(duplicateRouteError, Does.Contain("FanOut"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_EndpointValueNeverFallsBackToNodeLevelState()
        {
            string sourceNodeId = ESGraphIdentity.NewId();
            string targetNodeId = ESGraphIdentity.NewId();
            var run = new ESAISkillWorkflowRun
            {
                values = new JObject
                {
                    [sourceNodeId] = "legacy-node-value"
                },
                spec = new ESAISkillExecutionSpec
                {
                    dataBindings = new[]
                    {
                        new ESAISkillDataBinding
                        {
                            edgeId = ESGraphIdentity.NewId(),
                            sourceNodeId = sourceNodeId,
                            sourcePortId = ESGraphIdentity.NewId(),
                            sourcePortKey = "result.success",
                            sourceMeaning = "成功结果",
                            targetNodeId = targetNodeId,
                            targetPortId = ESGraphIdentity.NewId(),
                            targetPortKey = "input.success",
                            targetMeaning = "接收成功结果",
                            sourceValueTypeId = ESGraphPortValueIds.Text,
                            targetValueTypeId = ESGraphPortValueIds.Text,
                            targetAggregation = ESGraphPortAggregation.Single
                        }
                    }
                }
            };

            Assert.That(ESAISkillExecutionCoordinator.ResolveBoundValue(run, targetNodeId,
                "input.success"), Is.Null);
            run.values[ESAISkillExecutionCoordinator.EndpointValueKey(sourceNodeId,
                "result.success")] = "endpoint-value";
            Assert.That(ESAISkillExecutionCoordinator.ResolveBoundValue(run, targetNodeId,
                "input.success")?.ToString(), Is.EqualTo("endpoint-value"));
        }

        [Test]
        public void AISkillExecution_InputAggregationKeepsAStableValueShape()
        {
            const string sourceA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string sourceB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string target = "cccccccccccccccccccccccccccccccc";
            var run = new ESAISkillWorkflowRun
            {
                values = new JObject
                {
                    [ESAISkillExecutionCoordinator.EndpointValueKey(sourceA, "result.a")] = "A",
                    [ESAISkillExecutionCoordinator.EndpointValueKey(sourceB, "result.b")] = "B"
                },
                spec = new ESAISkillExecutionSpec
                {
                    dataBindings = new[]
                    {
                        new ESAISkillDataBinding
                        {
                            edgeId = "00000000000000000000000000000002",
                            order = 0,
                            sourceNodeId = sourceB,
                            sourcePortKey = "result.b",
                            targetNodeId = target,
                            targetPortKey = "input.ordered",
                            targetAggregation = ESGraphPortAggregation.Ordered
                        },
                        new ESAISkillDataBinding
                        {
                            edgeId = "00000000000000000000000000000001",
                            order = 1,
                            sourceNodeId = sourceA,
                            sourcePortKey = "result.a",
                            targetNodeId = target,
                            targetPortKey = "input.ordered",
                            targetAggregation = ESGraphPortAggregation.Ordered
                        },
                        new ESAISkillDataBinding
                        {
                            edgeId = "00000000000000000000000000000003",
                            order = 2,
                            sourceNodeId = sourceA,
                            sourcePortKey = "result.a",
                            targetNodeId = target,
                            targetPortKey = "input.named",
                            targetAggregation = ESGraphPortAggregation.Named
                        }
                    }
                }
            };

            JToken ordered = ESAISkillExecutionCoordinator.ResolveBoundValue(run, target,
                "input.ordered");
            Assert.That(ordered, Is.TypeOf<JArray>());
            Assert.That(ordered.Values<string>().ToArray(), Is.EqualTo(new[] { "B", "A" }),
                "Ordered 输入必须只认显式 order，不能回退到 EdgeId 顺序。");

            JToken named = ESAISkillExecutionCoordinator.ResolveBoundValue(run, target,
                "input.named");
            Assert.That(named, Is.TypeOf<JObject>());
            Assert.That(named.ToObject<JObject>().Properties().Single().Value.ToString(),
                Is.EqualTo("A"));

            run.spec.dataBindings = new[] { run.spec.dataBindings[2] };
            JToken namedSingle = ESAISkillExecutionCoordinator.ResolveBoundValue(run, target,
                "input.named");
            Assert.That(namedSingle, Is.TypeOf<JObject>());
        }

        [Test]
        public void AISkillExecution_DataPublicationUsesDeclaredPortsAndFailsAtomically()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneScanReview(graph);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph,
                    out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True,
                    Describe(graphIssues));
                Assert.That(new ESAISkillExecutionBaker().TryBake(snapshot,
                    out ESAISkillExecutionSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> executionIssues), Is.True,
                    Describe(executionIssues));
                ESAISkillExecutionStep task = spec.steps.Single(step => step.task != null);
                var run = new ESAISkillWorkflowRun
                {
                    spec = spec,
                    values = new JObject(),
                };
                var singleResult = new JObject { ["status"] = "ok" };

                Assert.That(ESAISkillExecutionCoordinator.TryPublishDataOutputs(run,
                    task.nodeId, ESAgentGraphStableIds.SkillRunResultPortKey,
                    singleResult, out string singleError), Is.True, singleError);
                Assert.That(run.values[ESAISkillExecutionCoordinator.EndpointValueKey(task.nodeId,
                    ESAgentGraphStableIds.SkillRunResultPortKey)]?["status"]?.ToString(),
                    Is.EqualTo("ok"));

                const string secondPortKey = "skill.value.secondary-result";
                task.ports = task.ports.Concat(new[]
                {
                    new ESAISkillExecutionPort
                    {
                        portId = ESGraphIdentity.NewId(),
                        portKey = secondPortKey,
                        meaning = "次要结果",
                        valueTypeId = ESGraphPortValueIds.Any,
                        direction = ESGraphPortDirection.Output,
                        capacity = ESGraphPortCapacity.Multi,
                        aggregation = ESGraphPortAggregation.Single,
                    }
                }).ToArray();
                run.values = new JObject();
                var incomplete = new JObject
                {
                    [ESAgentGraphStableIds.SkillRunResultPortKey] = "primary",
                };
                Assert.That(ESAISkillExecutionCoordinator.TryPublishDataOutputs(run,
                    task.nodeId, ESAgentGraphStableIds.SkillRunResultPortKey,
                    incomplete, out string missingError), Is.False);
                Assert.That(missingError, Does.Contain(secondPortKey));
                Assert.That(run.values.HasValues, Is.False, "多端点产物缺字段时不得部分写入。" );

                var complete = new JObject
                {
                    [ESAgentGraphStableIds.SkillRunResultPortKey] = "primary",
                    [secondPortKey] = "secondary",
                };
                Assert.That(ESAISkillExecutionCoordinator.TryPublishDataOutputs(run,
                    task.nodeId, ESAgentGraphStableIds.SkillRunResultPortKey,
                    complete, out string completeError), Is.True, completeError);
                Assert.That(run.values[ESAISkillExecutionCoordinator.EndpointValueKey(task.nodeId,
                    ESAgentGraphStableIds.SkillRunResultPortKey)]?.ToString(), Is.EqualTo("primary"));
                Assert.That(run.values[ESAISkillExecutionCoordinator.EndpointValueKey(task.nodeId,
                    secondPortKey)]?.ToString(), Is.EqualTo("secondary"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_TaskFailureBuildsStructuredTerminalResult()
        {
            var record = new ESAISkillStepRunRecord
            {
                invocationId = "invocation-1",
                childRunId = "run-1"
            };
            var source = new JObject
            {
                ["failureCode"] = "EnvironmentUnavailable"
            };

            JObject terminal = ESAISkillExecutionCoordinator.BuildTaskTerminalData(
                record, "Blocked", "环境不可用", source);

            Assert.That(terminal.Value<string>("status"), Is.EqualTo("Blocked"));
            Assert.That(terminal.Value<string>("message"), Is.EqualTo("环境不可用"));
            Assert.That(terminal.Value<string>("runId"), Is.EqualTo("run-1"));
            Assert.That(terminal.Value<string>("invocationId"), Is.EqualTo("invocation-1"));
            Assert.That(terminal.Value<string>("failureCode"),
                Is.EqualTo("EnvironmentUnavailable"));
            Assert.That(source.Property("status"), Is.Null,
                "终态归一化不得回写 Automation 返回的原始数据对象。");
        }

        [Test]
        public void AISkillApprovalEvidenceDetailIncludesPathsHashesAndStepIdentity()
        {
            var evidence = new ESAISkillApprovalEvidenceSnapshot("submission-run", 4,
                "evidence-run", "approval-node", 2,
                Array.Empty<ESAISkillApprovalEvidenceBindingSnapshot>(),
                new[]
                {
                    new ESAISkillApprovalEvidenceItemSnapshot("evidence-run", "scene-scan-task",
                        -1, 1, "invocation", "scene-scan-run",
                        "2026-08-16T01:23:45.0000000Z", "F:/Reports/scene-scan.json", "hash-json"),
                    new ESAISkillApprovalEvidenceItemSnapshot("evidence-run", "scene-scan-task",
                        -1, 1, "invocation", "scene-scan-run",
                        "2026-08-16T01:23:45.0000000Z", "F:/Reports/scene-scan.md", "hash-md")
                });

            string detail = ESStableGraphInspector.BuildAISkillApprovalEvidenceDetail(evidence);

            Assert.That(detail, Does.Contain("F:/Reports/scene-scan.json"));
            Assert.That(detail, Does.Contain("hash-json"));
            Assert.That(detail, Does.Contain("F:/Reports/scene-scan.md"));
            Assert.That(detail, Does.Contain("hash-md"));
            Assert.That(detail, Does.Contain("scene-scan-task"));
            Assert.That(detail, Does.Contain("scene-scan-run"));
        }

        [Test]
        public void AISkillApprovalEvidenceDetailBoundsVisibleArtifacts()
        {
            var evidence = new ESAISkillApprovalEvidenceSnapshot("submission-run", 1,
                "evidence-run", "approval-node", 1,
                Array.Empty<ESAISkillApprovalEvidenceBindingSnapshot>(),
                Enumerable.Range(1, 7).Select(index =>
                    new ESAISkillApprovalEvidenceItemSnapshot("evidence-run", "bulk-task",
                        -1, 1, string.Empty, string.Empty, string.Empty,
                        "F:/Reports/report-" + index + ".json", "hash-" + index)));

            string detail = ESStableGraphInspector.BuildAISkillApprovalEvidenceDetail(evidence);

            Assert.That(detail, Does.Contain("report-5.json"));
            Assert.That(detail, Does.Not.Contain("report-6.json"));
            Assert.That(detail, Does.Contain("其余 2 个产物"));
        }

        [Test]
        public void AISkillApprovalEvidenceSnapshotUsesOnlyApprovalBoundSources()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string firstTaskId,
                out string secondTaskId, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(firstTaskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                SetApprovalArtifact(run, firstTaskId, "bound.md", "bound");
                SetApprovalArtifact(run, secondTaskId, "newer-unbound.md", "unbound");

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out string error), Is.True, error);
                Assert.That(snapshot.CanApprove, Is.True, snapshot.EvidenceError);
                Assert.That(snapshot.Items.Select(item => Path.GetFileName(item.SourceArtifactPath)),
                    Is.EqualTo(new[] { "bound.md" }));
                Assert.That(snapshot.Items.All(item => item.ArtifactPath.StartsWith(
                    Path.Combine(ESAISkillExecutionCoordinator.RunsRoot, run.runId),
                    StringComparison.OrdinalIgnoreCase)), Is.True);
                Assert.That(snapshot.Bindings, Has.Count.EqualTo(1));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillApprovalEvidenceSnapshotKeepsAllOrderedAggregateBindings()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string firstTaskId,
                out string secondTaskId, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(secondTaskId, approvalId, 2, ESGraphPortAggregation.Named),
                    CreateApprovalBinding(firstTaskId, approvalId, 1, ESGraphPortAggregation.Ordered)
                };
                SetApprovalArtifact(run, firstTaskId, "first.json", "first");
                SetApprovalArtifact(run, secondTaskId, "second.json", "second");

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out string error), Is.True, error);
                Assert.That(snapshot.Bindings.Select(binding => binding.Order), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(snapshot.Items.Select(item => Path.GetFileName(item.SourceArtifactPath)),
                    Is.EquivalentTo(new[] { "first.json", "second.json" }));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillApprovalEvidenceSnapshotUsesIterationRecordsAndFreezesGeneration()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string firstTaskId,
                out _, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(firstTaskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                ESAISkillStepRunRecord step = FindRunStep(run, firstTaskId);
                string iterationPath = CreateApprovalArtifact(run, "iteration-3.md", "iteration");
                step.artifacts = new[] { CreateApprovalArtifact(run, "stale-top-level.md", "stale") };
                step.outputHashes = new[] { ComputeFileHash(step.artifacts[0]) };
                step.iterations.Add(new ESAISkillIterationRunRecord
                {
                    index = 3,
                    attemptCount = 2,
                    invocationId = "iteration-invocation",
                    artifacts = new[] { iterationPath },
                    outputHashes = new[] { ComputeFileHash(iterationPath) }
                });

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out string error), Is.True, error);
                int frozenGeneration = snapshot.SubmissionGeneration;
                run.approvalGeneration++;

                Assert.That(snapshot.SubmissionGeneration, Is.EqualTo(frozenGeneration));
                Assert.That(snapshot.Items, Has.Count.EqualTo(1));
                Assert.That(Path.GetFileName(snapshot.Items[0].SourceArtifactPath),
                    Is.EqualTo("iteration-3.md"));
                Assert.That(snapshot.Items[0].IterationIndex, Is.EqualTo(3));
                Assert.That(snapshot.Items[0].AttemptCount, Is.EqualTo(2));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillApprovalEvidence_DataBindingExcludesDifferentControlPredecessor()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string boundTaskId,
                out string controlTaskId, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(boundTaskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                run.spec.controlEdges = new[] { CreateApprovalControlEdge(controlTaskId, approvalId) };
                SetApprovalArtifact(run, boundTaskId, "bound-authority.md", "bound");
                SetApprovalArtifact(run, controlTaskId, "control-only.md", "control");

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out string error), Is.True, error);
                Assert.That(snapshot.Items.Select(item => Path.GetFileName(item.SourceArtifactPath)),
                    Is.EqualTo(new[] { "bound-authority.md" }));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillApprovalEvidence_ControlFlowFallbackRequiresExplicitMode()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string taskId,
                out _, out string approvalId);
            try
            {
                run.spec.dataBindings = Array.Empty<ESAISkillDataBinding>();
                run.spec.controlEdges = new[] { CreateApprovalControlEdge(taskId, approvalId) };
                SetApprovalArtifact(run, taskId, "control.md", "control");

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot blocked, out _), Is.True);
                Assert.That(blocked.CanApprove, Is.False);
                Assert.That(blocked.EvidenceError, Does.Contain("无绑定证据"));

                FindApprovalStep(run).approval.evidenceMode =
                    ESAISkillApprovalEvidenceMode.ControlFlowFallback;
                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot allowed, out string error), Is.True, error);
                Assert.That(allowed.CanApprove, Is.True, allowed.EvidenceError);
                Assert.That(Path.GetFileName(allowed.Items.Single().SourceArtifactPath),
                    Is.EqualTo("control.md"));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillApprovalEvidence_SourceChangesBeforeCopyFailsSnapshotCapture()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string taskId,
                out _, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(taskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                SetApprovalArtifact(run, taskId, "changing.md", "before");
                ESAISkillExecutionCoordinator.Internal_ApprovalEvidenceBeforeCopyTestHook =
                    (source, _) => File.WriteAllText(source, "after", new UTF8Encoding(false));

                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out _), Is.True);
                Assert.That(snapshot.CanApprove, Is.False);
                Assert.That(snapshot.EvidenceError, Does.Contain("Hash"));
            }
            finally
            {
                ESAISkillExecutionCoordinator.Internal_ApprovalEvidenceBeforeCopyTestHook = null;
                CleanupApprovalEvidenceTestFiles(run);
            }
        }

        [TestCase("")]
        [TestCase("not-a-sha256")]
        public void AISkillApprovalEvidence_InvalidOutputHashBlocksApprovalButAllowsReject(string hash)
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string taskId,
                out _, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(taskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                string path = CreateApprovalArtifact(run, "invalid-hash.md", "content");
                FindRunStep(run, taskId).artifacts = new[] { path };
                FindRunStep(run, taskId).outputHashes = new[] { hash };
                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out _), Is.True);
                Assert.That(snapshot.CanApprove, Is.False);

                ESAISkillExecutionCoordinator.Internal_RegisterApprovalTestRun(run);
                Assert.That(ESAISkillExecutionCoordinator.TryApprove(run.runId,
                    run.approvalGeneration, true, string.Empty, out string approveError), Is.False);
                Assert.That(approveError, Does.Contain("证据"));
                Assert.That(ESAISkillExecutionCoordinator.TryApprove(run.runId,
                    run.approvalGeneration, false, "证据无效", out string rejectError),
                    Is.True, rejectError);
            }
            finally
            {
                ESAISkillExecutionCoordinator.Internal_RemoveApprovalTestRun(run);
                CleanupApprovalEvidenceTestFiles(run);
            }
        }

        [Test]
        public void AISkillApprovalEvidence_TamperedApprovalCopyFailsValidation()
        {
            ESAISkillWorkflowRun run = CreateApprovalEvidenceRun(out string taskId,
                out _, out string approvalId);
            try
            {
                run.spec.dataBindings = new[]
                {
                    CreateApprovalBinding(taskId, approvalId, 0, ESGraphPortAggregation.Auto)
                };
                SetApprovalArtifact(run, taskId, "evidence.md", "trusted");
                Assert.That(ESAISkillExecutionCoordinator.TryCreateApprovalEvidenceSnapshot(run,
                    out ESAISkillApprovalEvidenceSnapshot snapshot, out string error), Is.True, error);

                File.AppendAllText(snapshot.Items.Single().ArtifactPath, "tampered",
                    new UTF8Encoding(false));
                Assert.That(ESAISkillExecutionCoordinator.TryValidateApprovalEvidence(run,
                    run.approvalGeneration, out string validationError), Is.False);
                Assert.That(validationError, Does.Contain("Hash"));
            }
            finally { CleanupApprovalEvidenceTestFiles(run); }
        }

        [Test]
        public void AISkillRetryClearsTransientTopLevelEvidenceButKeepsIterationHistory()
        {
            var iteration = new ESAISkillIterationRunRecord
            {
                index = 1,
                artifacts = new[] { "F:/Reports/iteration-history.md" },
                outputHashes = new[] { "iteration-history-hash" }
            };
            var record = new ESAISkillStepRunRecord
            {
                exitCode = 9,
                diagnostics = new[] { "old diagnostic" },
                artifacts = new[] { "F:/Reports/stale-top-level.md" },
                outputHashes = new[] { "stale-hash" },
                iterations = new List<ESAISkillIterationRunRecord> { iteration }
            };

            ESAISkillExecutionCoordinator.ClearCurrentAttemptTransientOutput(record);

            Assert.That(record.exitCode, Is.EqualTo(-1));
            Assert.That(record.diagnostics, Is.Empty);
            Assert.That(record.artifacts, Is.Empty);
            Assert.That(record.outputHashes, Is.Empty);
            Assert.That(record.iterations, Is.EqualTo(new[] { iteration }));
            Assert.That(record.iterations[0].artifacts,
                Is.EqualTo(new[] { "F:/Reports/iteration-history.md" }));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(
                    new ESAISkillIterationRunRecord { index = 1 }),
                Does.Not.Contain("\"attemptCount\""),
                "默认尝试号必须从 JSON 与状态 Hash 输入中省略，保持旧 RunRecord 兼容。");
        }

        private static ESAISkillWorkflowRun CreateApprovalEvidenceRun(out string firstTaskId,
            out string secondTaskId, out string approvalId)
        {
            firstTaskId = ESGraphIdentity.NewId();
            secondTaskId = ESGraphIdentity.NewId();
            approvalId = ESGraphIdentity.NewId();
            return new ESAISkillWorkflowRun
            {
                runId = ESGraphIdentity.NewId(),
                status = "WaitingApproval",
                currentNodeId = approvalId,
                approvalGeneration = 7,
                spec = new ESAISkillExecutionSpec
                {
                    steps = new[]
                    {
                        new ESAISkillExecutionStep { nodeId = firstTaskId, task = new ESAISkillTaskPayload() },
                        new ESAISkillExecutionStep { nodeId = secondTaskId, task = new ESAISkillTaskPayload() },
                        new ESAISkillExecutionStep { nodeId = approvalId, approval = new ESAISkillApprovalPayload() }
                    }
                },
                steps = new List<ESAISkillStepRunRecord>
                {
                    new ESAISkillStepRunRecord { nodeId = firstTaskId, status = "Completed", attemptCount = 1 },
                    new ESAISkillStepRunRecord { nodeId = secondTaskId, status = "Completed", attemptCount = 1 },
                    new ESAISkillStepRunRecord { nodeId = approvalId, status = "WaitingApproval" }
                }
            };
        }

        private static ESAISkillDataBinding CreateApprovalBinding(string sourceNodeId,
            string approvalId, int order, ESGraphPortAggregation aggregation)
            => new ESAISkillDataBinding
            {
                edgeId = ESGraphIdentity.NewId(),
                order = order,
                sourceNodeId = sourceNodeId,
                sourcePortId = ESGraphIdentity.NewId(),
                targetNodeId = approvalId,
                targetPortId = ESGraphIdentity.NewId(),
                targetAggregation = aggregation
            };

        private static ESAISkillStepRunRecord FindRunStep(ESAISkillWorkflowRun run, string nodeId)
            => run.steps.Single(step => string.Equals(step.nodeId, nodeId, StringComparison.Ordinal));

        private static ESAISkillExecutionStep FindApprovalStep(ESAISkillWorkflowRun run)
            => run.spec.steps.Single(step => step.approval != null);

        private static ESAISkillControlEdge CreateApprovalControlEdge(string sourceNodeId,
            string approvalId)
            => new ESAISkillControlEdge
            {
                edgeId = ESGraphIdentity.NewId(),
                sourceNodeId = sourceNodeId,
                sourcePortId = ESGraphIdentity.NewId(),
                targetNodeId = approvalId,
                targetPortId = ESGraphIdentity.NewId()
            };

        private static void SetApprovalArtifact(ESAISkillWorkflowRun run, string nodeId,
            string fileName, string content)
        {
            string path = CreateApprovalArtifact(run, fileName, content);
            ESAISkillStepRunRecord step = FindRunStep(run, nodeId);
            step.artifacts = new[] { path };
            step.outputHashes = new[] { ComputeFileHash(path) };
        }

        private static string CreateApprovalArtifact(ESAISkillWorkflowRun run, string fileName,
            string content)
        {
            string directory = Path.Combine(ESAutomationPathPolicy.TempRoot, "Tests",
                "ApprovalEvidence", run.runId);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        private static string ComputeFileHash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static void CleanupApprovalEvidenceTestFiles(ESAISkillWorkflowRun run)
        {
            if (run == null) return;
            string sourceDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, "Tests",
                "ApprovalEvidence", run.runId);
            string runDirectory = Path.Combine(ESAISkillExecutionCoordinator.RunsRoot, run.runId);
            if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
            if (Directory.Exists(runDirectory)) Directory.Delete(runDirectory, true);
        }

        [Test]
        public void AISkillExecution_NonRetryableTaskRejectsAutomaticRetry()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneScanReview(graph);
                ESGraphNodeRecord task = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.SkillTaskNode);
                ESAISkillTaskPayload payload = JsonUtility.FromJson<ESAISkillTaskPayload>(task.payloadJson);
                payload.retryCount = 1;
                graph.UpdateNode(task.nodeId, task.typeId, task.version, task.title,
                    JsonUtility.ToJson(payload), out _);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(issue => issue != null
                    && issue.code == "AISkill.Execution.Retry"
                    && issue.severity == ESGraphValidationSeverity.Error), Is.True, Describe(issues));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void AISkillExecution_InvalidParameterPatternIsRejectedAtBakeBoundary()
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.PopulateSceneScanReview(graph);
                ESGraphNodeRecord input = graph.Nodes.Single(node =>
                    node.typeId == ESAgentGraphStableIds.SkillInputNode);
                ESAISkillInputPayload payload = JsonUtility.FromJson<ESAISkillInputPayload>(input.payloadJson);
                payload.parameters[0].validationPattern = "[";
                graph.UpdateNode(input.nodeId, input.typeId, input.version, input.title,
                    JsonUtility.ToJson(payload), out _);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(issue => issue != null
                    && issue.code == "AISkill.Execution.ParameterPattern"
                    && issue.severity == ESGraphValidationSeverity.Error), Is.True, Describe(issues));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase("Blocked", "Blocked")]
        [TestCase("Rejected", "Rejected")]
        [TestCase("NotFound", "NotFound")]
        [TestCase("Unexpected", "Failed")]
        public void AISkillExecution_TaskFailureStatusIsClosedAndDeterministic(string input, string expected)
        {
            Assert.That(ESAISkillExecutionCoordinator.NormalizeTaskFailureStatus(input), Is.EqualTo(expected));
        }

        [TestCase(0, true)]
        [TestCase(7, true)]
        [TestCase(8, false)]
        [TestCase(-1, false)]
        public void AISkillExecution_SkillCallDepthHasClosedMaximumBoundary(int depth, bool expected)
        {
            Assert.That(ESAISkillExecutionCoordinator.CanEnterSkillCall(depth), Is.EqualTo(expected));
        }

        [Test]
        public void AISkillExecution_AncestorGraphDetectionRejectsSelfAndIndirectRecursion()
        {
            string graphA = ESGraphIdentity.NewId();
            string graphB = ESGraphIdentity.NewId();
            string graphC = ESGraphIdentity.NewId();
            string[] ancestors = { graphA, graphB };

            Assert.That(ESAISkillExecutionCoordinator.ContainsAncestorGraph(ancestors, graphA), Is.True);
            Assert.That(ESAISkillExecutionCoordinator.ContainsAncestorGraph(ancestors, graphB), Is.True);
            Assert.That(ESAISkillExecutionCoordinator.ContainsAncestorGraph(ancestors, graphC), Is.False);
            Assert.That(ESAISkillExecutionCoordinator.ContainsAncestorGraph(null, graphA), Is.False);
        }

        [Test]
        public void AISkillExecution_ChildApprovalProjectionIsMonotonicAndRejectsStaleGeneration()
        {
            var parent = new ESAISkillWorkflowRun { approvalGeneration = 4, status = "Running" };
            var callRecord = new ESAISkillStepRunRecord();
            var child = new ESAISkillWorkflowRun
            {
                status = "WaitingApproval",
                approvalGeneration = 2,
                message = "第一处确认"
            };

            ESAISkillExecutionCoordinator.ProjectChildApproval(parent, callRecord, child);
            Assert.That(parent.status, Is.EqualTo("WaitingApproval"));
            Assert.That(parent.approvalGeneration, Is.EqualTo(5));
            Assert.That(callRecord.childApprovalGeneration, Is.EqualTo(2));
            Assert.That(ESAISkillExecutionCoordinator.IsProjectedChildApprovalCurrent(callRecord, child),
                Is.True);

            ESAISkillExecutionCoordinator.ProjectChildApproval(parent, callRecord, child);
            Assert.That(parent.approvalGeneration, Is.EqualTo(5), "重复轮询不得生成新父代际。");

            child.approvalGeneration = 3;
            Assert.That(ESAISkillExecutionCoordinator.IsProjectedChildApprovalCurrent(callRecord, child),
                Is.False, "旧父界面不得批准子图的新一代确认。");
            ESAISkillExecutionCoordinator.ProjectChildApproval(parent, callRecord, child);
            Assert.That(parent.approvalGeneration, Is.EqualTo(6));
            Assert.That(callRecord.childApprovalGeneration, Is.EqualTo(3));
        }

        [Test]
        public void AISkillExecution_ExecutionSpecHashRejectsPersistedStepMutation()
        {
            ESAISkillExecutionSpec spec = CreateIntegritySpec();
            string original = ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec);

            spec.steps[0].title = "被改写的持久化步骤";

            Assert.That(ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                Is.Not.EqualTo(original));
        }

        [Test]
        public void AISkillExecution_ExecutionSpecHashCoversCompleteEndpointContract()
        {
            ESAISkillExecutionSpec spec = CreateIntegritySpec();
            var port = new ESAISkillExecutionPort
            {
                portId = ESGraphIdentity.NewId(),
                portKey = ESAgentGraphStableIds.SkillNextPortKey,
                meaning = "继续",
                valueTypeId = ESAgentGraphStableIds.SkillControlPort,
                direction = ESGraphPortDirection.Output,
                capacity = ESGraphPortCapacity.Single,
                aggregation = ESGraphPortAggregation.Single,
            };
            spec.steps[0].ports = new[] { port };
            string original = ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec);

            port.portKey = ESAgentGraphStableIds.SkillDefaultPortKey;
            Assert.That(ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                Is.Not.EqualTo(original), "PortKey 必须进入执行合同 Hash。" );
            port.portKey = ESAgentGraphStableIds.SkillNextPortKey;
            port.meaning = "已篡改";
            Assert.That(ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                Is.Not.EqualTo(original), "Meaning 必须进入执行合同 Hash。" );
            port.meaning = "继续";
            port.valueTypeId = ESGraphPortValueIds.Any;
            Assert.That(ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                Is.Not.EqualTo(original), "ValueType 必须进入执行合同 Hash。" );
            port.valueTypeId = ESAgentGraphStableIds.SkillControlPort;
            port.aggregation = ESGraphPortAggregation.Ordered;
            Assert.That(ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                Is.Not.EqualTo(original), "Aggregation 必须进入执行合同 Hash。" );
        }

        [Test]
        public void AISkillExecution_RunStateHashRejectsCursorMutation()
        {
            ESAISkillExecutionSpec spec = CreateIntegritySpec();
            var run = new ESAISkillWorkflowRun
            {
                runId = Guid.NewGuid().ToString("N"),
                graphId = spec.sourceGraphId,
                contentSignature = spec.sourceContentSignature,
                executionSpecHash = ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                currentNodeId = spec.steps[0].nodeId,
                spec = spec,
                steps = spec.steps.Select(step => new ESAISkillStepRunRecord
                    { nodeId = step.nodeId }).ToList(),
            };
            string original = ESAISkillExecutionCoordinator.ComputeRunStateHash(run);

            run.currentNodeId = ESGraphIdentity.NewId();

            Assert.That(ESAISkillExecutionCoordinator.ComputeRunStateHash(run),
                Is.Not.EqualTo(original));
            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("currentNodeId"));
        }

        [Test]
        public void AISkillExecution_RunStateValidationRejectsForgedForEachCursorAndDuplicateItems()
        {
            ESAISkillExecutionSpec spec = CreateIntegritySpec();
            var run = new ESAISkillWorkflowRun
            {
                runId = Guid.NewGuid().ToString("N"),
                graphId = spec.sourceGraphId,
                contentSignature = spec.sourceContentSignature,
                currentNodeId = spec.steps[0].nodeId,
                iterationNodeId = spec.steps[0].nodeId,
                iterationTaskNodeId = spec.steps[1].nodeId,
                iterationIndex = 1,
                spec = spec,
                steps = spec.steps.Select(step => new ESAISkillStepRunRecord
                    { nodeId = step.nodeId }).ToList(),
            };

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("ForEach"));
        }

        [Test]
        public void AISkillExecution_FanOutContinuationUsesOrderInsteadOfEdgeId()
        {
            ESAISkillExecutionSpec spec = CreateFanOutIntegritySpec();
            var run = CreateRunForSpec(spec, spec.steps[3].nodeId);
            run.activeFanOutNodeId = spec.steps[1].nodeId;
            run.activeJoinNodeId = spec.steps[5].nodeId;
            run.fanOutExpectedCount = 3;
            run.fanOutArrivedCount = 1;
            run.pendingFanOutNodeIds = new List<string> { spec.steps[4].nodeId };

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.True, error);
        }

        [Test]
        public void AISkillExecution_RunStateValidationRejectsFanOutTargetSubstitution()
        {
            ESAISkillExecutionSpec spec = CreateFanOutIntegritySpec();
            var run = CreateRunForSpec(spec, spec.steps[3].nodeId);
            run.activeFanOutNodeId = spec.steps[1].nodeId;
            run.activeJoinNodeId = spec.steps[5].nodeId;
            run.fanOutExpectedCount = 3;
            run.fanOutArrivedCount = 1;
            run.pendingFanOutNodeIds = new List<string> { spec.steps[2].nodeId };

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("FanOut"));
        }

        [Test]
        public void AISkillExecution_RunStateValidationRejectsCurrentNodeOutsideActiveFanOutBranch()
        {
            ESAISkillExecutionSpec spec = CreateFanOutIntegritySpec();
            var run = CreateRunForSpec(spec, spec.steps[2].nodeId);
            run.activeFanOutNodeId = spec.steps[1].nodeId;
            run.activeJoinNodeId = spec.steps[5].nodeId;
            run.fanOutExpectedCount = 3;
            run.fanOutArrivedCount = 1;
            run.pendingFanOutNodeIds = new List<string> { spec.steps[4].nodeId };

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("当前节点"));
        }

        [Test]
        public void AISkillExecution_RunStateValidationRejectsBranchMasqueradingAsFanOut()
        {
            ESAISkillExecutionSpec spec = CreateFanOutIntegritySpec();
            spec.steps[1].fanOut = null;
            spec.steps[1].branch = new ESAISkillBranchPayload();
            var run = CreateRunForSpec(spec, spec.steps[3].nodeId);
            run.activeFanOutNodeId = spec.steps[1].nodeId;
            run.activeJoinNodeId = spec.steps[5].nodeId;
            run.fanOutExpectedCount = 3;
            run.fanOutArrivedCount = 1;
            run.pendingFanOutNodeIds = new List<string> { spec.steps[4].nodeId };

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("FanOut"));
        }

        [Test]
        public void AISkillExecution_RunStateValidationRejectsForgedFanOutContinuation()
        {
            ESAISkillExecutionSpec spec = CreateFanOutIntegritySpec();
            var run = CreateRunForSpec(spec, spec.steps[2].nodeId);
            run.activeFanOutNodeId = spec.steps[1].nodeId;
            run.fanOutExpectedCount = 3;
            run.fanOutArrivedCount = 0;
            run.pendingFanOutNodeIds = new List<string>();

            Assert.That(ESAISkillExecutionCoordinator.TryValidateRunState(run, out string error),
                Is.False);
            Assert.That(error, Does.Contain("FanOut"));
        }

        private static ESAISkillWorkflowRun CreateRunForSpec(ESAISkillExecutionSpec spec,
            string currentNodeId)
        {
            return new ESAISkillWorkflowRun
            {
                runId = Guid.NewGuid().ToString("N"),
                graphId = spec.sourceGraphId,
                contentSignature = spec.sourceContentSignature,
                executionSpecHash = ESAISkillExecutionCoordinator.ComputeExecutionSpecHash(spec),
                currentNodeId = currentNodeId,
                spec = spec,
                steps = spec.steps.Select(step => new ESAISkillStepRunRecord
                    { nodeId = step.nodeId }).ToList(),
            };
        }

        private static ESAISkillExecutionSpec CreateFanOutIntegritySpec()
        {
            string input = ESGraphIdentity.NewId();
            string fanOut = ESGraphIdentity.NewId();
            string branchA = ESGraphIdentity.NewId();
            string branchB = ESGraphIdentity.NewId();
            string branchC = ESGraphIdentity.NewId();
            string join = ESGraphIdentity.NewId();
            return new ESAISkillExecutionSpec
            {
                sourceGraphId = ESGraphIdentity.NewId(),
                sourceContentSignature = new string('b', 64),
                entryNodeId = input,
                steps = new[]
                {
                    new ESAISkillExecutionStep { nodeId = input, input = new ESAISkillInputPayload() },
                    new ESAISkillExecutionStep { nodeId = fanOut, fanOut = new ESAISkillFanOutPayload() },
                    new ESAISkillExecutionStep { nodeId = branchA, task = new ESAISkillTaskPayload() },
                    new ESAISkillExecutionStep { nodeId = branchB, task = new ESAISkillTaskPayload() },
                    new ESAISkillExecutionStep { nodeId = branchC, task = new ESAISkillTaskPayload() },
                    new ESAISkillExecutionStep { nodeId = join, join = new ESAISkillJoinPayload() },
                },
                controlEdges = new[]
                {
                    CreateControlEdge("00000000000000000000000000000003", 0, fanOut,
                        ESAgentGraphStableIds.SkillFanOutPortKey, branchA),
                    CreateControlEdge("00000000000000000000000000000001", 1, fanOut,
                        ESAgentGraphStableIds.SkillFanOutPortKey, branchB),
                    CreateControlEdge("00000000000000000000000000000002", 2, fanOut,
                        ESAgentGraphStableIds.SkillFanOutPortKey, branchC),
                    CreateControlEdge("00000000000000000000000000000011", 3, branchA,
                        ESAgentGraphStableIds.SkillSuccessPortKey, join),
                    CreateControlEdge("00000000000000000000000000000012", 4, branchB,
                        ESAgentGraphStableIds.SkillSuccessPortKey, join),
                    CreateControlEdge("00000000000000000000000000000013", 5, branchC,
                        ESAgentGraphStableIds.SkillSuccessPortKey, join),
                },
                fanOutJoinPairs = new[]
                {
                    new ESAISkillFanOutJoinPair
                    {
                        fanOutNodeId = fanOut,
                        fanOutId = "fan-out",
                        joinNodeId = join,
                        joinId = "join"
                    }
                }
            };
        }

        private static ESAISkillControlEdge CreateControlEdge(string edgeId, int order,
            string sourceNodeId, string sourcePortKey, string targetNodeId)
        {
            return new ESAISkillControlEdge
            {
                edgeId = edgeId,
                order = order,
                sourceNodeId = sourceNodeId,
                sourcePortId = ESGraphIdentity.NewId(),
                sourcePortKey = sourcePortKey,
                sourceMeaning = "多目标路线",
                targetNodeId = targetNodeId,
                targetPortId = ESGraphIdentity.NewId(),
                targetPortKey = ESGraphBuiltInPortKeys.Input,
                targetMeaning = "执行输入",
            };
        }

        private static ESAISkillExecutionSpec CreateIntegritySpec()
        {
            string entry = ESGraphIdentity.NewId();
            string output = ESGraphIdentity.NewId();
            return new ESAISkillExecutionSpec
            {
                sourceGraphId = ESGraphIdentity.NewId(),
                sourceContentSignature = new string('a', 64),
                entryNodeId = entry,
                steps = new[]
                {
                    new ESAISkillExecutionStep
                    {
                        nodeId = entry,
                        title = "输入",
                        input = new ESAISkillInputPayload(),
                    },
                    new ESAISkillExecutionStep
                    {
                        nodeId = output,
                        title = "输出",
                        output = new ESAISkillOutputPayload(),
                    },
                },
            };
        }

        private static bool IsError(ESGraphValidationIssue issue) => issue != null && issue.severity == ESGraphValidationSeverity.Error;
        private static string Describe(IEnumerable<ESGraphValidationIssue> issues) => string.Join("\n", issues.Where(issue => issue != null).Select(issue => issue.code + ": " + issue.message));
    }
}
