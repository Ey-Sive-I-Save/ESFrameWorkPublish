using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ES.EditorInternal.Tests
{
    public sealed class ESAgentAuthoringGraphTests
    {
        [Test]
        public void AgentAuthoring_ProfileBakesStrongTypedGenerationSpec()
        {
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
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
                Assert.That(spec.DomainId, Is.EqualTo(ESGraphDomainIds.AgentAuthoring));
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
                Assert.That(spec.validations.Single().requireHumanApproval, Is.True);
                Assert.That(spec.relations.Length, Is.EqualTo(4));
                Assert.That(spec.relations.Select(item => item.semanticType), Is.EquivalentTo(new[]
                {
                    ESGraphPortValueIds.AgentContext,
                    ESGraphPortValueIds.AgentContext,
                    ESGraphPortValueIds.AgentRequirement,
                    ESGraphPortValueIds.AgentArtifact
                }));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_FinalPurposeIsUniqueAndMandatory()
        {
            ESGraphAsset graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESAgentAuthoringGraphValidator.TryGetFinalPurpose(graph,
                    out string purpose, out string success), Is.True);
                Assert.That(purpose, Does.Contain("Graph"));
                Assert.That(success, Is.Not.Empty);

                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.BuiltInKind == ESGraphBuiltInNodeKind.AgentGoal);
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

                AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentGoal, new Vector2(0f, 0f));
                AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentGoal, new Vector2(0f, 240f));
                issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.GoalCount"), Is.True);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ImmediatePromptAndAllCopyFormatsPreserveChineseGraphContract()
        {
            ESGraphAsset graph = CreateValidGraph(out _);
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
            ESGraphAsset graph = CreateValidGraph(out _);
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
            ESGraphAsset graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                ESAgentGenerationOutput output = spec.outputs.Single();
                output.operationMode = ESAgentArtifactOperationMode.UpdateOnly;
                output.targetProjectPath = "Assets/Plugins/ES/AICommands/__es_graph_missing_update__.md";
                Assert.That(ESAgentArtifactGenerationWorkspace.TryPrepareArtifactOperations(spec,
                    out string missingError), Is.False);
                Assert.That(missingError, Does.Contain("没有找到可更新"));

                output.operationMode = ESAgentArtifactOperationMode.CreateOnly;
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
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
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
            ESGraphAsset graph = CreateValidGraph(out _);
            try
            {
                ESGraphNodeRecord validation = graph.Nodes.First(node =>
                    node.BuiltInKind == ESGraphBuiltInNodeKind.AgentValidation);
                var payload = new ESAgentValidationPayload { requireDiffReview = false, requireHumanApproval = false };
                graph.UpdateNode(validation.nodeId, validation.typeId, validation.version,
                    validation.title, JsonUtility.ToJson(payload), out _);
                AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(900f, 200f));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.ApprovalPolicy"), Is.True);
                Assert.That(issues.Any(issue => issue?.code == "AgentAuthoring.Unreachable"), Is.True);
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
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentAICommandOutput), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentSkillOutput), Is.True);
            Assert.That(profile.NodeDefinitions.Any(item => item.NodeType.StableId == "es.agent-authoring.output-artifact"), Is.False);
        }

        [Test]
        public void AgentAuthoring_TypedPortsRejectCrossStageConnections()
        {
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                ESGraphNodeRecord goal = graph.Nodes.Single(node =>
                    node.BuiltInKind == ESGraphBuiltInNodeKind.AgentGoal);
                ESGraphPortRecord goalOutput = goal.ports.Single(port => port.direction == ESGraphPortDirection.Output);
                ESGraphPortRecord artifactInput = outputNode.ports.Single(port => port.direction == ESGraphPortDirection.Input);
                Assert.That(graph.CanConnect(goalOutput.portId, artifactInput.portId, null, out string error), Is.False);
                Assert.That(error, Does.Contain("端口类型不兼容"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_RejectsMutatedDomainPortSchema()
        {
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
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
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
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
        public void AgentAuthoring_PresetCreatesValidatedDualOutputGraph()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string error), Is.True, error);
                ESAgentAuthoringGraphPreset.Populate(graph);
                Assert.That(graph.Nodes.Count, Is.EqualTo(6));
                Assert.That(graph.Edges.Count, Is.EqualTo(6));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.outputs.Select(item => item.artifactKind),
                    Is.EquivalentTo(new[] { ESAgentArtifactKind.AICommand, ESAgentArtifactKind.AgentSkill }));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly, ESAgentArtifactKind.AICommand)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly, ESAgentArtifactKind.AgentSkill)]
        public void AgentAuthoring_SingleArtifactPresetsCreateUsableGraphs(
            ESAgentAuthoringPresetKind presetKind, ESAgentArtifactKind expectedKind)
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string error), Is.True, error);
                ESAgentAuthoringGraphPreset.Populate(graph, presetKind);
                Assert.That(graph.Nodes.Count, Is.EqualTo(5));
                Assert.That(graph.Edges.Count, Is.EqualTo(4));
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
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_MindMapPresetCreatesBranchedTypedRequirementGraph()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string error), Is.True, error);
                ESAgentAuthoringGraphPreset.Populate(graph, ESAgentAuthoringPresetKind.MindMapPaired);
                Assert.That(graph.Nodes.Count, Is.EqualTo(10));
                Assert.That(graph.Edges.Count, Is.EqualTo(16));
                List<ESGraphValidationIssue> issues = graph.ValidateGraph();
                new ESAgentAuthoringGraphProfile().Validate(graph, issues);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out issues), Is.True, Describe(issues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot, out ESAgentArtifactGenerationSpec spec,
                    out IReadOnlyList<ESGraphValidationIssue> bakeIssues), Is.True, Describe(bakeIssues));
                Assert.That(spec.references.Length, Is.EqualTo(2));
                Assert.That(spec.constraints.Length, Is.EqualTo(4));
                Assert.That(spec.outputs.Length, Is.EqualTo(2));
                Assert.That(spec.relations.Length, Is.EqualTo(16));
                Assert.That(spec.constraints.Select(item => item.kind), Is.EquivalentTo(new[]
                {
                    ESAgentConstraintKind.Required,
                    ESAgentConstraintKind.Forbidden,
                    ESAgentConstraintKind.Permission,
                    ESAgentConstraintKind.Quality
                }));
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
                Is.EqualTo("Assets/ESNormalAssets/Data/AgentAuthoring/Graphs"));
        }

        [Test]
        public void AgentAuthoring_BakerRejectsCrossDomainSnapshot()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.Generic, out string error), Is.True, error);
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
            ESGraphAsset graph = CreateValidGraph(out _);
            try
            {
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> graphIssues), Is.True, Describe(graphIssues));
                Assert.That(new ESAgentArtifactGenerationBaker().TryBake(snapshot,
                    out ESAgentArtifactGenerationSpec spec, out IReadOnlyList<ESGraphValidationIssue> bakeIssues),
                    Is.True, Describe(bakeIssues));
                string prompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(new ESAgentArtifactGenerationRequest
                {
                    requestId = "test-request",
                    requestDirectory = "ES/Automation/Candidates/AgentAuthoring/test-request",
                    candidateDirectory = "ES/Automation/Candidates/AgentAuthoring/test-request/candidate",
                    spec = spec
                });
                Assert.That(prompt, Does.Contain("思路图关系"));
                Assert.That(prompt, Does.Contain("```mermaid"));
                Assert.That(prompt, Does.Contain(ESGraphPortValueIds.AgentRequirement));
                Assert.That(prompt, Does.Contain("acceptance criteria"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AgentAuthoring_ChinesePayloadAndPromptRoundTripWithoutLoss()
        {
            ESGraphAsset graph = CreateValidGraph(out ESGraphNodeRecord outputNode);
            try
            {
                var payload = new ESAgentAICommandOutputPayload
                {
                    commandName = "生成中文命令",
                    purpose = "根据玩家意图生成可审查的候选文件",
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
        public void CmdAgent_DisabledConfigurationReturnsRejectedAndKeepsPrompt()
        {
            ESCmdAgent agent = ScriptableObject.CreateInstance<ESCmdAgent>();
            try
            {
                agent.enableAgent = false;
                agent.workspacePath = GetProjectRootForTests();
                ESCmdAgentPromptDispatchResult result = ESCmdAgentWindow.DispatchStartForTests(agent,
                    ESCmdAgentSynchronousFailureKind.None, "不要丢失这条提示", out string retainedInput);
                Assert.That(result.State, Is.EqualTo(ESCmdAgentPromptDispatchState.Rejected));
                Assert.That(result.Accepted, Is.False);
                Assert.That(retainedInput, Is.EqualTo("不要丢失这条提示"));
                Assert.That(result.Message, Does.Contain("未启用"));
            }
            finally { UnityEngine.Object.DestroyImmediate(agent); }
        }

        [Test]
        public void CmdAgent_MissingWorkspaceReturnsRejectedAndKeepsPrompt()
        {
            ESCmdAgent agent = ScriptableObject.CreateInstance<ESCmdAgent>();
            try
            {
                agent.enableAgent = true;
                agent.workspacePath = Path.Combine(Path.GetTempPath(), "es-cmd-agent-missing-" + Guid.NewGuid().ToString("N"));
                ESCmdAgentPromptDispatchResult result = ESCmdAgentWindow.DispatchStartForTests(agent,
                    ESCmdAgentSynchronousFailureKind.None, "保留输入", out string retainedInput);
                Assert.That(result.State, Is.EqualTo(ESCmdAgentPromptDispatchState.Rejected));
                Assert.That(retainedInput, Is.EqualTo("保留输入"));
                Assert.That(result.Message, Does.Contain("工作目录不存在"));
            }
            finally { UnityEngine.Object.DestroyImmediate(agent); }
        }

        [TestCase(ESCmdAgentSynchronousFailureKind.ProcessStartRejected, "系统拒绝创建 Cmd Agent 进程")]
        [TestCase(ESCmdAgentSynchronousFailureKind.ProcessStartException, "测试进程启动异常")]
        public void CmdAgent_ProcessStartFailureReturnsRejectedAndKeepsPrompt(
            ESCmdAgentSynchronousFailureKind failureKind, string expectedLog)
        {
            ESCmdAgent agent = ScriptableObject.CreateInstance<ESCmdAgent>();
            try
            {
                agent.enableAgent = true;
                agent.workspacePath = GetProjectRootForTests();
                LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(expectedLog)));
                ESCmdAgentPromptDispatchResult result = ESCmdAgentWindow.DispatchStartForTests(agent,
                    failureKind, "实现 Graph", out string retainedInput);
                Assert.That(result.State, Is.EqualTo(ESCmdAgentPromptDispatchState.Rejected));
                Assert.That(result.IsDispatched, Is.False);
                Assert.That(retainedInput, Is.EqualTo("实现 Graph"));
                Assert.That(result.Message, Does.Contain("启动或提示排队失败"));
            }
            finally { UnityEngine.Object.DestroyImmediate(agent); }
        }

        [Test]
        public void CmdAgent_ConPtyStartFailureReturnsRejectedAndKeepsPrompt()
        {
            ESCmdAgent agent = ScriptableObject.CreateInstance<ESCmdAgent>();
            try
            {
                agent.enableAgent = true;
                agent.workspacePath = GetProjectRootForTests();
                ESCmdAgentPromptDispatchResult result = ESCmdAgentWindow.DispatchStartForTests(agent,
                    ESCmdAgentSynchronousFailureKind.ConPtyStartException, "实现 Graph", out string retainedInput);
                Assert.That(result.State, Is.EqualTo(ESCmdAgentPromptDispatchState.Rejected));
                Assert.That(result.IsDispatched, Is.False);
                Assert.That(retainedInput, Is.EqualTo("实现 Graph"));
                Assert.That(result.Message, Does.Contain("后台 Cmd Agent 终端启动失败"));
            }
            finally { UnityEngine.Object.DestroyImmediate(agent); }
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
            ESGraphAsset commandGraph = CreateValidGraph(out _);
            try
            {
                IReadOnlyList<IESGraphNodeDefinition> commandDefinitions = ESGraphAuthoringRegistry.GetNodeDefinitions(commandGraph);
                Assert.That(commandDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentAICommandOutput), Is.True);
                Assert.That(commandDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentSkillOutput), Is.False);
            }
            finally { Object.DestroyImmediate(commandGraph); }

            ESGraphAsset skillGraph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(skillGraph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string error), Is.True, error);
                AddFromProfile(skillGraph, ESGraphBuiltInNodeKind.AgentSkillOutput, new Vector2(0f, 0f));
                IReadOnlyList<IESGraphNodeDefinition> skillDefinitions = ESGraphAuthoringRegistry.GetNodeDefinitions(skillGraph);
                Assert.That(skillDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentSkillOutput), Is.True);
                Assert.That(skillDefinitions.Any(item => item.NodeType.Kind == ESGraphBuiltInNodeKind.AgentAICommandOutput), Is.False);
            }
            finally { Object.DestroyImmediate(skillGraph); }
        }

        private static ESGraphAsset CreateValidGraph(out ESGraphNodeRecord outputNode)
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            Assert.That(graph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string error), Is.True, error);
            ESGraphNodeRecord goal = AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentGoal, new Vector2(0f, 0f));
            ESGraphNodeRecord reference = AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentReference, new Vector2(180f, 0f));
            ESGraphNodeRecord constraint = AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(360f, 0f));
            outputNode = AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentAICommandOutput, new Vector2(540f, 0f));
            ESGraphNodeRecord validation = AddFromProfile(graph, ESGraphBuiltInNodeKind.AgentValidation, new Vector2(720f, 0f));
            var goalPayload = new ESAgentGoalPayload { title = "Graph Authoring", objective = "通过 Graph 生成 AICommand 候选" };
            graph.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(goalPayload), out _);
            Connect(graph, goal, reference); Connect(graph, reference, constraint); Connect(graph, constraint, outputNode); Connect(graph, outputNode, validation);
            return graph;
        }

        private static ESGraphNodeRecord AddFromProfile(ESGraphAsset graph, ESGraphBuiltInNodeKind nodeKind,
            Vector2 position)
        {
            var profile = new ESAgentAuthoringGraphProfile();
            IESGraphNodeDefinition definition = profile.NodeDefinitions.First(item => item.NodeType.Kind == nodeKind);
            ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName, position,
                definition.Ports);
            graph.UpdateNode(node.nodeId, definition.NodeType, definition.CurrentVersion, node.title,
                definition.CreateDefaultPayload(), out _);
            return node;
        }

        private static void Connect(ESGraphAsset graph, ESGraphNodeRecord from, ESGraphNodeRecord to)
        {
            ESGraphPortRecord output = from.ports.First(port => port.direction == ESGraphPortDirection.Output);
            ESGraphPortRecord input = to.ports.First(port => port.direction == ESGraphPortDirection.Input);
            Assert.That(graph.TryAddEdge(output.portId, input.portId, out _, out string error), Is.True, error);
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

        private static bool IsError(ESGraphValidationIssue issue) => issue != null && issue.severity == ESGraphValidationSeverity.Error;
        private static string Describe(IEnumerable<ESGraphValidationIssue> issues) => string.Join("\n", issues.Where(issue => issue != null).Select(issue => issue.code + ": " + issue.message));
    }
}
