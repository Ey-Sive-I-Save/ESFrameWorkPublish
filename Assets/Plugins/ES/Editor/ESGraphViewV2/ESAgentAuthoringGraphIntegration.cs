using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.EditorInternal
{
    public interface IESGraphAuthoringPlanBaker
    {
        bool TryBakePlan(ESBakedGraphSnapshot source, out IESBakedGraphPlan plan,
            out IReadOnlyList<ESGraphValidationIssue> issues);
    }

    public enum ESAgentConstraintKind : byte
    {
        Required = 0,
        Forbidden = 1,
        Permission = 2,
        Quality = 3
    }

    public enum ESAgentArtifactKind : byte
    {
        AICommand = 0,
        AgentSkill = 1
    }

    public enum ESAgentArtifactOperationMode : byte
    {
        CreateOrUpdate = 0,
        CreateOnly = 1,
        UpdateOnly = 2
    }

    public enum ESAgentArtifactResolvedOperation : byte
    {
        Create = 0,
        Update = 1
    }

    public enum ESAgentGraphCopyFormat : byte
    {
        ImmediateExecutionPrompt = 0,
        ArtifactRequestJson = 1,
        GraphMarkdown = 2
    }

    public enum ESAgentReferenceKind : byte
    {
        AIWarning = 0,
        AICommand = 1,
        AgentSkill = 2,
        CSharpSource = 3,
        Documentation = 4,
        ProjectAsset = 5
    }

    [Serializable]
    public sealed class ESAgentGoalPayload
    {
        public int schemaVersion = 1;
        public string title = "生成新的 Agent Artifact";
        [TextArea] public string objective = "描述希望 AICommand 或 Agent Skill 解决的问题。";
        [TextArea] public string context = "";
        [TextArea] public string targetUsers = "该 AICommand / Agent Skill 的使用者与触发场景。";
        [TextArea] public string successCriteria = "生成结果可读、可验证、权限边界明确，并能通过人工 Diff Review。";
    }

    [Serializable]
    public sealed class ESAgentReferencePayload
    {
        public int schemaVersion = 1;
        public ESAgentReferenceKind referenceKind = ESAgentReferenceKind.AICommand;
        public string projectPath = "Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md";
        public string purpose = "Agent Artifact 候选生成的权限与输出合同";
        public bool required = true;
    }

    [Serializable]
    public sealed class ESAgentConstraintPayload
    {
        public int schemaVersion = 1;
        public ESAgentConstraintKind kind = ESAgentConstraintKind.Required;
        [TextArea] public string statement = "只生成候选文件，不直接写入正式目录。";
        [TextArea] public string rationale = "说明为什么需要该规则。";
        [TextArea] public string verification = "说明如何验证该规则已经满足。";
    }

    [Serializable]
    public sealed class ESAgentAICommandOutputPayload
    {
        public int schemaVersion = 1;
        public string commandName = "生成_新任务_AI命令";
        public string targetProjectPath = "Assets/Plugins/ES/AICommands/新_AI命令_AI命令.md";
        public ESAgentArtifactOperationMode operationMode = ESAgentArtifactOperationMode.CreateOrUpdate;
        public string commandType = "明确执行";
        public string defaultWrite = "由本节点约束";
        public string riskLevel = "L2";
        [TextArea] public string purpose = "描述该 AICommand 要授权和约束的单次任务。";
        [TextArea] public string expectedInputs = "用户目标、范围、权威规则和相关项目路径。";
        [TextArea] public string executionOutline = "读取规则\n核对现状\n执行受控修改\n验证\n交付";
        [TextArea] public string acceptanceCriteria = "输出必须包含已读规则、改动、验证和剩余风险。";
        [TextArea] public string requiredSections = "必须先读\n执行要求\n交付格式\n需求";
    }

    [Serializable]
    public sealed class ESAgentSkillOutputPayload
    {
        public int schemaVersion = 1;
        public string skillName = "es-generated-workflow";
        public string targetProjectPath = ".agents/skills/es-generated-workflow/";
        public ESAgentArtifactOperationMode operationMode = ESAgentArtifactOperationMode.CreateOrUpdate;
        [TextArea] public string description = "描述该 Skill 的能力、触发场景和适用任务。";
        [TextArea] public string triggerScenarios = "说明何时必须使用该 Skill，以及何时不应触发。";
        [TextArea] public string workflow = "读取权威规则\n执行受控步骤\n验证\n交付";
        [TextArea] public string nonGoals = "不得扩大用户授权，不得绕过 AICommand、候选目录或人工批准。";
        [TextArea] public string validationSteps = "严格 UTF-8\n目标路径白名单\n候选完整性\nDiff Review";
        public bool includeAgentsMetadata = true;
        public bool includeReferences = true;
        public bool includeScripts;
        public string defaultPrompt = "Use $es-generated-workflow to complete the requested ESFramework workflow.";
    }

    [Serializable]
    public sealed class ESAgentValidationPayload
    {
        public int schemaVersion = 1;
        public bool validateAICommand = true;
        public bool validateAgentSkill = true;
        public bool validateUtf8 = true;
        public bool requireDiffReview = true;
        public bool requireHumanApproval = true;
        [TextArea] public string additionalRequirements = "不得包含 U+FFFD；不得越过候选目录。";
        [TextArea] public string reviewChecklist = "目标路径正确\n内容符合 Graph\n没有越权修改\n验证证据真实";
    }

    [Serializable]
    public sealed class ESAgentGenerationGoal
    {
        public string nodeId;
        public string title;
        public string objective;
        public string context;
        public string targetUsers;
        public string successCriteria;
    }

    [Serializable]
    public sealed class ESAgentGenerationReference
    {
        public string nodeId;
        public ESAgentReferenceKind referenceKind;
        public string projectPath;
        public string purpose;
        public bool required;
    }

    [Serializable]
    public sealed class ESAgentGenerationConstraint
    {
        public string nodeId;
        public ESAgentConstraintKind kind;
        public string statement;
        public string rationale;
        public string verification;
    }

    [Serializable]
    public sealed class ESAgentGenerationOutput
    {
        public string nodeId;
        public ESAgentArtifactKind artifactKind;
        public string artifactId;
        public string artifactName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public ESAgentArtifactResolvedOperation resolvedOperation;
        public string requirements;
        public string commandType;
        public string defaultWrite;
        public string riskLevel;
        public string expectedInputs;
        public string executionOutline;
        public string acceptanceCriteria;
        public string skillDescription;
        public string skillTriggerScenarios;
        public string skillWorkflow;
        public string skillNonGoals;
        public string skillValidationSteps;
        public string defaultPrompt;
        public bool includeAgentsMetadata;
        public bool includeReferences;
        public bool includeScripts;
    }

    [Serializable]
    public sealed class ESAgentGenerationValidation
    {
        public string nodeId;
        public bool validateAICommand;
        public bool validateAgentSkill;
        public bool validateUtf8;
        public bool requireDiffReview;
        public bool requireHumanApproval;
        public string additionalRequirements;
        public string reviewChecklist;
    }

    [Serializable]
    public sealed class ESAgentGenerationRelation
    {
        public string edgeId;
        public string fromNodeId;
        public string fromNodeTypeId;
        public string fromNodeTitle;
        public string fromPortStableKey;
        public string toNodeId;
        public string toNodeTypeId;
        public string toNodeTitle;
        public string toPortStableKey;
        public string semanticType;
    }

    /// <summary>Graph V2 烘焙出的 Agent Artifact 生成规格；只用于编辑器生成与审查，不进入运行时。</summary>
    [Serializable]
    public sealed class ESAgentArtifactGenerationSpec : IESBakedGraphPlan
    {
        public string sourceGraphId;
        public string sourceOriginGraphId;
        public string sourceContentSignature;
        public ESAgentGenerationGoal goal;
        public ESAgentGenerationReference[] references = Array.Empty<ESAgentGenerationReference>();
        public ESAgentGenerationConstraint[] constraints = Array.Empty<ESAgentGenerationConstraint>();
        public ESAgentGenerationOutput[] outputs = Array.Empty<ESAgentGenerationOutput>();
        public ESAgentGenerationValidation[] validations = Array.Empty<ESAgentGenerationValidation>();
        public ESAgentGenerationRelation[] relations = Array.Empty<ESAgentGenerationRelation>();

        public ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring);
        public string DomainId => Domain.StableId;
        public string SourceContentSignature => sourceContentSignature ?? string.Empty;
    }

    internal static class ESAgentArtifactIdentity
    {
        public static string Create(string graphId, string outputNodeId)
        {
            if (!ESGraphIdentity.IsValid(graphId) || !ESGraphIdentity.IsValid(outputNodeId))
                return string.Empty;
            return "es." + graphId + "." + outputNodeId;
        }
    }

    public static class ESAgentAuthoringGraphValidator
    {
        public static void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            if (asset == null || issues == null)
                return;
            if (asset.allowCycles)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.CyclePolicy", "智能助手编排图禁止循环。"));

            int goalCount = 0;
            int outputCount = 0;
            int validationCount = 0;
            var definitions = ESGraphAuthoringRegistry
                .GetNodeDefinitions(ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring))
                .ToDictionary(definition => definition.NodeType);
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null)
                    continue;
                ValidateNodeSchema(node, definitions, issues);
                if (definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition))
                {
                    if (definition.Category == ESGraphNodeCategory.Output)
                        outputCount++;
                    else if (definition.Category == ESGraphNodeCategory.Validation)
                        validationCount++;
                }
                switch (node.BuiltInKind)
                {
                    case ESGraphBuiltInNodeKind.AgentGoal:
                        goalCount++;
                        ValidateGoal(node, issues);
                        break;
                    case ESGraphBuiltInNodeKind.AgentReference:
                        ValidateReference(node, issues);
                        break;
                    case ESGraphBuiltInNodeKind.AgentConstraint:
                        ValidateConstraint(node, issues);
                        break;
                    case ESGraphBuiltInNodeKind.AgentAICommandOutput:
                        ValidateAICommandOutput(node, issues);
                        break;
                    case ESGraphBuiltInNodeKind.AgentSkillOutput:
                        ValidateAgentSkillOutput(node, issues);
                        break;
                    case ESGraphBuiltInNodeKind.AgentValidation:
                        ValidateValidation(node, issues);
                        break;
                }
            }
            if (goalCount != 1)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.GoalCount",
                    "智能助手编排图必须且只能有一个 Goal，当前为 " + goalCount + " 个。"));
            if (outputCount == 0)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputMissing", "至少需要一个 OutputArtifact 节点。"));
            if (validationCount == 0)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ValidationMissing", "至少需要一个 Validation 节点。"));

            ESGraphNodeRecord goal = goalCount == 1 ? FindSingle(asset, ESGraphBuiltInNodeKind.AgentGoal) : null;
            if (goal != null)
            {
                ValidateReachability(asset, goal, issues);
                ValidateTopology(asset, definitions, issues);
            }
        }

        internal static bool TryRead<T>(string json, out T payload, out string error) where T : class
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Payload 不能为空。";
                return false;
            }
            try { payload = JsonUtility.FromJson<T>(json); }
            catch (ArgumentException exception)
            {
                error = "Payload JSON 无法解析：" + exception.Message;
                return false;
            }
            error = payload == null ? "Payload JSON 无法解析。" : string.Empty;
            return payload != null;
        }

        private static void ValidateGoal(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentGoalPayload payload, out string error)
                || payload.schemaVersion != 1)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal", string.IsNullOrEmpty(error)
                    ? "Goal SchemaVersion 无效。" : error, node.nodeId));
                return;
            }
            if (string.IsNullOrWhiteSpace(payload.title))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Title", "Goal 标题不能为空。", node.nodeId));
            if (string.IsNullOrWhiteSpace(payload.objective))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Objective", "最终目的不能为空。", node.nodeId));
            if (string.IsNullOrWhiteSpace(payload.successCriteria))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.SuccessCriteria",
                    "成功标准必须明确最终结果如何被验收。", node.nodeId));
        }

        public static bool TryGetFinalPurpose(ESGraphAsset asset, out string objective, out string successCriteria)
        {
            objective = string.Empty;
            successCriteria = string.Empty;
            if (asset == null)
                return false;
            ESGraphNodeRecord goal = null;
            int count = 0;
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null || node.BuiltInKind != ESGraphBuiltInNodeKind.AgentGoal)
                    continue;
                goal = node;
                count++;
            }
            if (count != 1 || !TryRead(goal.payloadJson, out ESAgentGoalPayload payload, out _))
                return false;
            objective = payload.objective?.Trim() ?? string.Empty;
            successCriteria = payload.successCriteria?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(objective) && !string.IsNullOrWhiteSpace(successCriteria);
        }

        private static void ValidateNodeSchema(ESGraphNodeRecord node,
            Dictionary<ESGraphNodeTypeKey, IESGraphNodeDefinition> definitions,
            List<ESGraphValidationIssue> issues)
        {
            if (!definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) return;
            List<ESGraphPortRecord> ports = node.ports ?? new List<ESGraphPortRecord>();
            if (ports.Count != definition.Ports.Count)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.PortSchema",
                    "节点端口数量与领域 Profile 不一致，请重新创建该节点。", node.nodeId));
                return;
            }
            for (int i = 0; i < definition.Ports.Count; i++)
            {
                ESGraphPortDefinition expected = definition.Ports[i];
                ESGraphPortRecord actual = ports.FirstOrDefault(port => port != null
                    && string.Equals(port.stableKey, expected.stableKey, StringComparison.Ordinal));
                if (actual == null || actual.direction != expected.direction || actual.capacity != expected.capacity
                    || !string.Equals(actual.valueTypeId, expected.valueTypeId, StringComparison.Ordinal))
                {
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.PortSchema",
                        "节点端口规则与领域 Profile 不一致：" + expected.stableKey, node.nodeId));
                }
            }
        }

        private static void ValidateReference(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentReferencePayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.projectPath))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference", string.IsNullOrEmpty(error)
                    ? "引用资料必须提供项目内文件路径。" : error, node.nodeId));
                return;
            }
            if (Path.IsPathRooted(payload.projectPath) || payload.projectPath.Contains(".."))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ReferencePath", "Reference 只能保存安全的项目相对路径。", node.nodeId));
            else if (payload.required)
            {
                string fullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(payload.projectPath);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ReferenceMissing",
                        "必须读取的 Reference 不存在：" + payload.projectPath, node.nodeId));
            }
        }

        private static void ValidateConstraint(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentConstraintPayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.statement))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Constraint", string.IsNullOrEmpty(error)
                    ? "规则内容不能为空。" : error, node.nodeId));
        }

        private static void ValidateAICommandOutput(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentAICommandOutputPayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.commandName)
                || string.IsNullOrWhiteSpace(payload.targetProjectPath))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.AICommandOutput", string.IsNullOrEmpty(error)
                    ? "命令产物必须配置名称和正式文件路径。" : error, node.nodeId));
                return;
            }
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand, payload.targetProjectPath, out string pathError))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputPath", pathError, node.nodeId));
            if (!Enum.IsDefined(typeof(ESAgentArtifactOperationMode), payload.operationMode))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputOperation", "命令产物的创建/更新方式非法。", node.nodeId));
            if (string.IsNullOrWhiteSpace(payload.commandType) || string.IsNullOrWhiteSpace(payload.riskLevel))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.AICommandMetadata", "命令产物必须声明命令类型和风险等级。", node.nodeId));
        }

        private static void ValidateAgentSkillOutput(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentSkillOutputPayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.skillName)
                || string.IsNullOrWhiteSpace(payload.description))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.AgentSkillOutput", string.IsNullOrEmpty(error)
                    ? "技能产物必须配置名称、能力说明和正式目录。" : error, node.nodeId));
                return;
            }
            if (!string.Equals(payload.skillName, payload.skillName.ToLowerInvariant(), StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(payload.skillName, "^[a-z0-9-]+$"))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.SkillName", "Skill 名称只允许小写字母、数字和连字符。", node.nodeId));
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill, payload.targetProjectPath, out string pathError))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputPath", pathError, node.nodeId));
            if (!Enum.IsDefined(typeof(ESAgentArtifactOperationMode), payload.operationMode))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputOperation", "Agent Skill 的创建/更新方式非法。", node.nodeId));
            string expected = ".agents/skills/" + payload.skillName + "/";
            if (!string.Equals(payload.targetProjectPath.Replace('\\', '/'), expected, StringComparison.Ordinal))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.SkillTarget", "Skill 名称与目标目录必须一致：" + expected, node.nodeId));
        }

        private static void ValidateValidation(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentValidationPayload payload, out string error)
                || payload.schemaVersion != 1)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Validation", string.IsNullOrEmpty(error)
                    ? "Validation SchemaVersion 无效。" : error, node.nodeId));
                return;
            }
            if (!payload.requireDiffReview || !payload.requireHumanApproval)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ApprovalPolicy", "Diff Review 与人工批准不得关闭。", node.nodeId));
        }

        private static ESGraphNodeRecord FindSingle(ESGraphAsset asset, ESGraphBuiltInNodeKind nodeKind)
        {
            ESGraphNodeRecord result = null;
            for (int i = 0; i < asset.Nodes.Count; i++)
                if (asset.Nodes[i] != null && asset.Nodes[i].BuiltInKind == nodeKind)
                    result = asset.Nodes[i];
            return result;
        }

        private static void ValidateReachability(ESGraphAsset asset, ESGraphNodeRecord root,
            List<ESGraphValidationIssue> issues)
        {
            var nodeByPort = new Dictionary<string, string>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                outgoing[node.nodeId] = new List<string>();
                if (node.ports == null) continue;
                foreach (ESGraphPortRecord port in node.ports)
                    if (port != null) nodeByPort[port.portId] = node.nodeId;
            }
            foreach (ESGraphEdgeRecord edge in asset.Edges)
                if (edge != null && nodeByPort.TryGetValue(edge.outputPortId, out string from)
                    && nodeByPort.TryGetValue(edge.inputPortId, out string to)) outgoing[from].Add(to);
            var visited = new HashSet<string>(StringComparer.Ordinal) { root.nodeId };
            var queue = new Queue<string>();
            queue.Enqueue(root.nodeId);
            while (queue.Count > 0)
                foreach (string next in outgoing[queue.Dequeue()]) if (visited.Add(next)) queue.Enqueue(next);
            foreach (ESGraphNodeRecord node in asset.Nodes)
                if (node != null && !visited.Contains(node.nodeId))
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Unreachable", "节点必须从 Goal 可达。", node.nodeId));
        }

        private static void ValidateTopology(ESGraphAsset asset,
            Dictionary<ESGraphNodeTypeKey, IESGraphNodeDefinition> definitions,
            List<ESGraphValidationIssue> issues)
        {
            var nodeByPort = new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                incoming[node.nodeId] = 0;
                outgoing[node.nodeId] = 0;
                foreach (ESGraphPortRecord port in node.ports ?? new List<ESGraphPortRecord>())
                    if (port != null && !string.IsNullOrEmpty(port.portId)) nodeByPort[port.portId] = node;
            }

            foreach (ESGraphEdgeRecord edge in asset.Edges)
            {
                if (edge == null || !nodeByPort.TryGetValue(edge.outputPortId, out ESGraphNodeRecord from)
                    || !nodeByPort.TryGetValue(edge.inputPortId, out ESGraphNodeRecord to)) continue;
                outgoing[from.nodeId]++;
                incoming[to.nodeId]++;
                if (!definitions.TryGetValue(from.TypeKey, out IESGraphNodeDefinition fromDefinition)
                    || !definitions.TryGetValue(to.TypeKey, out IESGraphNodeDefinition toDefinition))
                    continue;
                if (!IsAllowedTransition(fromDefinition.Category, toDefinition.Category))
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Transition",
                        "不允许的节点关系："
                        + ESGraphChinesePresentation.GetNodeCategoryName(fromDefinition.Category) + " → "
                        + ESGraphChinesePresentation.GetNodeCategoryName(toDefinition.Category), edge.edgeId));
            }

            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                if (!definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition))
                    continue;
                int inCount = incoming[node.nodeId];
                int outCount = outgoing[node.nodeId];
                switch (definition.Category)
                {
                    case ESGraphNodeCategory.Entry:
                        if (inCount != 0 || outCount == 0)
                            issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Topology",
                                "Goal 不能有输入，并且至少连接一个下游思路节点。", node.nodeId));
                        break;
                    case ESGraphNodeCategory.Reference:
                    case ESGraphNodeCategory.Constraint:
                        if (inCount == 0 || outCount == 0)
                            issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Thought.Topology",
                                "Reference/Constraint 必须同时具备上游和下游关系。", node.nodeId));
                        break;
                    case ESGraphNodeCategory.Output:
                        if (inCount == 0 || outCount == 0)
                            issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Topology",
                                "每个输出必须接收至少一条要求，并连接到 Validation。", node.nodeId));
                        break;
                    case ESGraphNodeCategory.Validation:
                        if (inCount == 0 || outCount != 0)
                            issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Validation.Topology",
                                "检查节点必须接收候选产物，并且不能再连接下游。", node.nodeId));
                        break;
                }
            }
        }

        private static bool IsAllowedTransition(ESGraphNodeCategory from, ESGraphNodeCategory to)
        {
            switch (from)
            {
                case ESGraphNodeCategory.Entry:
                case ESGraphNodeCategory.Reference:
                    return to == ESGraphNodeCategory.Reference
                        || to == ESGraphNodeCategory.Constraint;
                case ESGraphNodeCategory.Constraint:
                    return to == ESGraphNodeCategory.Output;
                case ESGraphNodeCategory.Output:
                    return to == ESGraphNodeCategory.Validation;
                default:
                    return false;
            }
        }
    }

    public sealed class ESAgentArtifactGenerationBaker : IESGraphPlanBaker<ESAgentArtifactGenerationSpec>
    {
        public ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring);

        public bool TryBake(ESBakedGraphSnapshot source, out ESAgentArtifactGenerationSpec plan,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            plan = null;
            if (!ESGraphPlanBakeGuard.TryValidateSource(source, Domain, out issues)) return false;
            var failures = new List<ESGraphValidationIssue>();
            if (source.AllowCycles)
                failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.CyclePolicy", "智能助手编排图禁止循环。"));
            var references = new List<ESAgentGenerationReference>();
            var constraints = new List<ESAgentGenerationConstraint>();
            var outputs = new List<ESAgentGenerationOutput>();
            var validations = new List<ESAgentGenerationValidation>();
            var relations = new List<ESAgentGenerationRelation>();
            ESAgentGenerationGoal goal = null;
            foreach (ESGraphNodeSnapshot node in source.Nodes)
            {
                switch (node.BuiltInKind)
                {
                    case ESGraphBuiltInNodeKind.AgentGoal:
                        if (goal != null)
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Bake", "只能烘焙一个 Goal。", node.NodeId));
                            break;
                        }
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentGoalPayload gp, out string ge)
                            || gp.schemaVersion != 1 || string.IsNullOrWhiteSpace(gp.title)
                            || string.IsNullOrWhiteSpace(gp.objective) || string.IsNullOrWhiteSpace(gp.successCriteria))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Bake", string.IsNullOrEmpty(ge) ? "Goal 无法烘焙。" : ge, node.NodeId));
                        else goal = new ESAgentGenerationGoal { nodeId = node.NodeId, title = gp.title, objective = gp.objective,
                            context = gp.context, targetUsers = gp.targetUsers, successCriteria = gp.successCriteria };
                        break;
                    case ESGraphBuiltInNodeKind.AgentReference:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentReferencePayload rp, out string re)
                            || rp.schemaVersion != 1 || string.IsNullOrWhiteSpace(rp.projectPath)
                            || Path.IsPathRooted(rp.projectPath) || rp.projectPath.Contains(".."))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference.Bake", string.IsNullOrEmpty(re) ? "Reference 无法烘焙。" : re, node.NodeId));
                        else
                        {
                            string referenceFullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(rp.projectPath);
                            if (rp.required && !File.Exists(referenceFullPath) && !Directory.Exists(referenceFullPath))
                                failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference.Bake", "必须读取的 Reference 不存在：" + rp.projectPath, node.NodeId));
                            else references.Add(new ESAgentGenerationReference { nodeId = node.NodeId, referenceKind = rp.referenceKind,
                                projectPath = rp.projectPath, purpose = rp.purpose, required = rp.required });
                        }
                        break;
                    case ESGraphBuiltInNodeKind.AgentConstraint:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentConstraintPayload cp, out string ce)
                            || cp.schemaVersion != 1 || string.IsNullOrWhiteSpace(cp.statement))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Constraint.Bake", string.IsNullOrEmpty(ce) ? "Constraint 无法烘焙。" : ce, node.NodeId));
                        else constraints.Add(new ESAgentGenerationConstraint { nodeId = node.NodeId, kind = cp.kind,
                            statement = cp.statement, rationale = cp.rationale, verification = cp.verification });
                        break;
                    case ESGraphBuiltInNodeKind.AgentAICommandOutput:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentAICommandOutputPayload command, out string commandError)
                            || command.schemaVersion != 1 || string.IsNullOrWhiteSpace(command.commandName)
                            || string.IsNullOrWhiteSpace(command.commandType) || string.IsNullOrWhiteSpace(command.riskLevel))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", string.IsNullOrEmpty(commandError) ? "命令产物无法生成检查快照。" : commandError, node.NodeId));
                            break;
                        }
                        if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand, command.targetProjectPath, out string commandPathError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", commandPathError, node.NodeId));
                            break;
                        }
                        outputs.Add(new ESAgentGenerationOutput { nodeId = node.NodeId, artifactKind = ESAgentArtifactKind.AICommand,
                            artifactId = ESAgentArtifactIdentity.Create(source.GraphId, node.NodeId),
                            artifactName = command.commandName, targetProjectPath = command.targetProjectPath,
                            operationMode = command.operationMode,
                            requirements = command.purpose + "\nRequired sections:\n" + command.requiredSections,
                            commandType = command.commandType, defaultWrite = command.defaultWrite, riskLevel = command.riskLevel,
                            expectedInputs = command.expectedInputs, executionOutline = command.executionOutline,
                            acceptanceCriteria = command.acceptanceCriteria });
                        break;
                    case ESGraphBuiltInNodeKind.AgentSkillOutput:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentSkillOutputPayload skill, out string skillError)
                            || skill.schemaVersion != 1 || string.IsNullOrWhiteSpace(skill.skillName))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", string.IsNullOrEmpty(skillError) ? "技能产物无法生成检查快照。" : skillError, node.NodeId));
                            break;
                        }
                        if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill, skill.targetProjectPath, out string skillPathError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", skillPathError, node.NodeId));
                            break;
                        }
                        string expectedSkillTarget = ".agents/skills/" + skill.skillName + "/";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(skill.skillName ?? string.Empty, "^[a-z0-9-]+$")
                            || !string.Equals(skill.targetProjectPath.Replace('\\', '/'), expectedSkillTarget, StringComparison.Ordinal))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake",
                                "Agent Skill 名称与目标目录必须一致：" + expectedSkillTarget, node.NodeId));
                            break;
                        }
                        outputs.Add(new ESAgentGenerationOutput { nodeId = node.NodeId, artifactKind = ESAgentArtifactKind.AgentSkill,
                            artifactId = ESAgentArtifactIdentity.Create(source.GraphId, node.NodeId),
                            artifactName = skill.skillName, targetProjectPath = skill.targetProjectPath,
                            operationMode = skill.operationMode,
                            requirements = skill.description, skillDescription = skill.description, skillWorkflow = skill.workflow,
                            skillTriggerScenarios = skill.triggerScenarios, skillNonGoals = skill.nonGoals,
                            skillValidationSteps = skill.validationSteps,
                            defaultPrompt = skill.defaultPrompt, includeAgentsMetadata = skill.includeAgentsMetadata,
                            includeReferences = skill.includeReferences, includeScripts = skill.includeScripts });
                        break;
                    case ESGraphBuiltInNodeKind.AgentValidation:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentValidationPayload vp, out string ve)
                            || vp.schemaVersion != 1 || !vp.requireDiffReview || !vp.requireHumanApproval)
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Validation.Bake", string.IsNullOrEmpty(ve) ? "检查节点必须保留候选差异检查和人工批准。" : ve, node.NodeId));
                        else validations.Add(new ESAgentGenerationValidation { nodeId = node.NodeId, validateAICommand = vp.validateAICommand,
                            validateAgentSkill = vp.validateAgentSkill, validateUtf8 = vp.validateUtf8, requireDiffReview = true,
                            requireHumanApproval = true, additionalRequirements = vp.additionalRequirements,
                            reviewChecklist = vp.reviewChecklist });
                        break;
                    default:
                        failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.NodeType.Bake", "未知节点类型：" + node.TypeId, node.NodeId));
                        break;
                }
            }
            if (goal == null) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.GoalMissing", "必须且只能烘焙一个 Goal。"));
            if (outputs.Count == 0) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputMissing", "至少需要一个输出。"));
            if (validations.Count == 0) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.ValidationMissing", "至少需要一个验证策略。"));
            BakeRelations(source, relations, failures);
            if (failures.Count > 0) { issues = failures; return false; }
            plan = new ESAgentArtifactGenerationSpec { sourceGraphId = source.GraphId,
                sourceOriginGraphId = source.OriginGraphId, sourceContentSignature = source.ContentSignature, goal = goal,
                references = references.ToArray(), constraints = constraints.ToArray(), outputs = outputs.ToArray(),
                validations = validations.ToArray(), relations = relations.ToArray() };
            issues = failures;
            return true;
        }

        private static void BakeRelations(ESBakedGraphSnapshot source, List<ESAgentGenerationRelation> relations,
            List<ESGraphValidationIssue> failures)
        {
            var ownerByPort = new Dictionary<string, ESGraphNodeSnapshot>(StringComparer.Ordinal);
            foreach (ESGraphNodeSnapshot node in source.Nodes)
                foreach (ESGraphPortSnapshot port in node.Ports)
                    ownerByPort[port.PortId] = node;

            foreach (ESGraphEdgeSnapshot edge in source.Edges)
            {
                if (!source.TryGetPort(edge.OutputPortId, out ESGraphPortSnapshot output)
                    || !source.TryGetPort(edge.InputPortId, out ESGraphPortSnapshot input)
                    || !ownerByPort.TryGetValue(edge.OutputPortId, out ESGraphNodeSnapshot from)
                    || !ownerByPort.TryGetValue(edge.InputPortId, out ESGraphNodeSnapshot to))
                {
                    failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Relation.Bake",
                        "无法解析思路图关系。", edge.EdgeId));
                    continue;
                }
                relations.Add(new ESAgentGenerationRelation
                {
                    edgeId = edge.EdgeId,
                    fromNodeId = from.NodeId,
                    fromNodeTypeId = from.TypeId,
                    fromNodeTitle = from.Title,
                    fromPortStableKey = output.StableKey,
                    toNodeId = to.NodeId,
                    toNodeTypeId = to.TypeId,
                    toNodeTitle = to.Title,
                    toPortStableKey = input.StableKey,
                    semanticType = output.ValueTypeId
                });
            }
        }
    }

    public static class ESAgentArtifactPathPolicy
    {
        public static bool IsAllowedTarget(ESAgentArtifactKind kind, string path, out string error)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized) || normalized.Contains(".."))
            { error = "目标必须是无 .. 的项目相对路径。"; return false; }
            if (kind == ESAgentArtifactKind.AICommand
                && normalized.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal)
                && normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            { error = string.Empty; return true; }
            if (kind == ESAgentArtifactKind.AgentSkill
                && normalized.StartsWith(".agents/skills/es-", StringComparison.Ordinal)
                && normalized.EndsWith("/", StringComparison.Ordinal))
            {
                string folder = normalized.Substring(".agents/skills/".Length).TrimEnd('/');
                if (folder.IndexOf('/') < 0)
                { error = string.Empty; return true; }
            }
            error = kind == ESAgentArtifactKind.AICommand
                ? "AICommand 目标必须位于 Assets/Plugins/ES/AICommands 且为 .md。"
                : "Agent Skill 目标必须是 .agents/skills/es-*/ 直接子目录。";
            return false;
        }
    }

    public static class ESAgentAuthoringAssetCatalog
    {
        private static readonly string[] FirstPartyAssetRoots =
        {
            "Assets/ESNormalAssets",
            "Assets/Plugins/ES",
            "Assets/Scripts"
        };

        private static readonly Dictionary<ESAgentReferenceKind, List<string>> ReferenceCache =
            new Dictionary<ESAgentReferenceKind, List<string>>();
        private static List<string> aiCommandTargetCache;
        private static List<string> agentSkillTargetCache;

        public static List<string> GetReferencePaths(ESAgentReferenceKind kind, string currentPath = null,
            bool forceRefresh = false)
        {
            if (!forceRefresh && ReferenceCache.TryGetValue(kind, out List<string> cached))
                return WithCurrent(cached, currentPath);
            IEnumerable<string> paths;
            switch (kind)
            {
                case ESAgentReferenceKind.AIWarning:
                    paths = FindAssetPaths("Assets/Plugins/ES/AIWarnings", path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.AICommand:
                    paths = FindAssetPaths("Assets/Plugins/ES/AICommands", path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.AgentSkill:
                    paths = EnumerateSkillFiles();
                    break;
                case ESAgentReferenceKind.CSharpSource:
                    paths = FindAssetPaths(FirstPartyAssetRoots,
                        path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.Documentation:
                    paths = EnumerateProjectFiles(new[] { "Documentation", "ES/Documentation" }, ".md");
                    break;
                case ESAgentReferenceKind.ProjectAsset:
                    paths = FindAssetPaths(FirstPartyAssetRoots,
                        path => !AssetDatabase.IsValidFolder(path)
                                && !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    paths = Array.Empty<string>();
                    break;
            }
            var result = paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
            ReferenceCache[kind] = result;
            return WithCurrent(result, currentPath);
        }

        public static List<string> GetAICommandTargets(string currentPath = null, bool forceRefresh = false)
        {
            if (forceRefresh || aiCommandTargetCache == null)
                aiCommandTargetCache = FindAssetPaths("Assets/Plugins/ES/AICommands",
                    path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList();
            return WithCurrent(aiCommandTargetCache, currentPath);
        }

        public static List<string> GetAgentSkillTargets(string currentPath = null, bool forceRefresh = false)
        {
            if (forceRefresh || agentSkillTargetCache == null)
            {
                string root = Path.Combine(ESAgentArtifactGenerationWorkspace.GetProjectRoot(), ".agents", "skills");
                agentSkillTargetCache = (Directory.Exists(root)
                        ? Directory.EnumerateDirectories(root, "es-*", SearchOption.TopDirectoryOnly)
                            .Select(path => ".agents/skills/" + Path.GetFileName(path) + "/")
                        : Array.Empty<string>())
                    .OrderBy(path => path, StringComparer.Ordinal).ToList();
            }
            return WithCurrent(agentSkillTargetCache, currentPath);
        }

        private static IEnumerable<string> FindAssetPaths(string root, Func<string, bool> predicate)
        {
            return FindAssetPaths(new[] { root }, predicate);
        }

        private static IEnumerable<string> FindAssetPaths(IEnumerable<string> roots, Func<string, bool> predicate)
        {
            string[] validRoots = roots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (validRoots.Length == 0) return Array.Empty<string>();
            return AssetDatabase.FindAssets(string.Empty, validRoots).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && predicate(path.Replace('\\', '/')))
                .Select(Normalize);
        }

        private static IEnumerable<string> EnumerateSkillFiles()
        {
            return EnumerateProjectFiles(new[] { ".agents/skills" }, new[] { ".md", ".yaml", ".yml", ".ps1", ".py" });
        }

        private static IEnumerable<string> EnumerateProjectFiles(IEnumerable<string> roots, params string[] extensions)
        {
            string projectRoot = ESAgentArtifactGenerationWorkspace.GetProjectRoot();
            foreach (string relativeRoot in roots)
            {
                string fullRoot = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(fullRoot)) continue;
                foreach (string file in ESManagedFileIO.EnumerateFilesSafely(fullRoot, "*"))
                    if (extensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                        yield return Normalize(file.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        private static List<string> WithCurrent(IEnumerable<string> paths, string currentPath)
        {
            var result = paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
            string current = Normalize(currentPath);
            if (!string.IsNullOrEmpty(current) && !result.Contains(current)) result.Insert(0, current);
            if (result.Count == 0) result.Add(string.IsNullOrEmpty(current) ? "<没有可用项>" : current);
            return result;
        }

        private static string Normalize(string path) { return (path ?? string.Empty).Replace('\\', '/'); }
    }

    public static class ESAgentAuthoringGraphSchema
    {
        public static bool TryRepairPorts(ESGraphAsset asset, out string error)
        {
            if (asset == null || asset.DomainKind != ESGraphDomainKind.AgentAuthoring)
            {
                error = "只能修复智能助手编排图。";
                return false;
            }
            var definitions = ESGraphAuthoringRegistry.GetNodeDefinitions(asset.DomainKey)
                .ToDictionary(definition => definition.NodeType);
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) continue;
                if (node.ports == null || node.ports.Count != definition.Ports.Count)
                {
                    error = "节点端口数量无法安全迁移，请重新创建节点：" + (node.title ?? node.nodeId);
                    return false;
                }
            }
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) continue;
                for (int i = 0; i < definition.Ports.Count; i++)
                {
                    ESGraphPortDefinition expected = definition.Ports[i];
                    ESGraphPortRecord port = node.ports[i];
                    port.name = expected.name;
                    port.stableKey = expected.stableKey;
                    port.valueTypeId = expected.valueTypeId;
                    port.direction = expected.direction;
                    port.capacity = expected.capacity;
                }
            }
            error = string.Empty;
            return true;
        }
    }

    public abstract class ESAgentPayloadInspector<T> : IESGraphPayloadInspector where T : class, new()
    {
        public ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring);
        public abstract ESGraphNodeTypeKey NodeType { get; }
        public virtual int Priority => 0;
        public VisualElement Create(string payloadJson, Action<string> commitPayload)
        {
            if (!ESAgentAuthoringGraphValidator.TryRead(payloadJson, out T payload, out _)) payload = new T();
            VisualElement root = Build(payload, () => commitPayload?.Invoke(JsonUtility.ToJson(payload)));
            ESGraphInspectorVisuals.StylePayloadRoot(root);
            return root;
        }
        protected abstract VisualElement Build(T payload, Action commit);
        protected static TextField Text(string label, string value, bool multiline = false)
        {
            var field = new TextField(label) { value = value ?? string.Empty, multiline = multiline };
            ESGraphInspectorVisuals.StyleTextField(field);
            return field;
        }
        protected static void CommitOnFocusOut(TextField field, Action<string> set, Action commit)
        {
            if (field == null)
                return;
            string lastCommitted = field.value ?? string.Empty;
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                string next = field.value ?? string.Empty;
                if (string.Equals(lastCommitted, next, StringComparison.Ordinal))
                    return;
                set?.Invoke(next);
                commit?.Invoke();
                lastCommitted = next;
            });
        }

        protected static VisualElement SearchPicker(string labelText, string buttonText, string tooltip,
            Action refresh, out Button pickerButton)
        {
            var row = new VisualElement();
            var label = new Label(labelText);
            row.Add(label);

            pickerButton = new Button
            {
                text = buttonText + "  ▼",
                tooltip = tooltip
            };
            row.Add(pickerButton);
            ESGraphInspectorVisuals.StylePickerRow(row, label, pickerButton);

            if (refresh != null)
            {
                var refreshButton = new Button(refresh)
                {
                    text = "刷新",
                    tooltip = "重新扫描项目中的可选项；扫描只在点击后执行。"
                };
                refreshButton.style.width = 48f;
                refreshButton.style.minWidth = 48f;
                refreshButton.style.minHeight = 24f;
                refreshButton.style.flexGrow = 0f;
                refreshButton.style.marginLeft = 3f;
                row.Add(refreshButton);
            }
            return row;
        }

        protected static VisualElement OperationPicker(ESAgentArtifactOperationMode current,
            Action<ESAgentArtifactOperationMode> onSelected)
        {
            VisualElement row = SearchPicker(
                "创建 / 更新方式",
                OperationLabel(current),
                "自动创建或更新最常用；仅创建与仅更新会在目标状态不匹配时阻断。",
                null,
                out Button pickerButton);
            ESAgentArtifactOperationMode selectedValue = current;
            pickerButton.clicked += () =>
            {
                Action<ESAgentArtifactOperationMode> select = value =>
                {
                    selectedValue = value;
                    pickerButton.text = OperationLabel(value) + "  ▼";
                    onSelected?.Invoke(value);
                };
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择创建 / 更新方式",
                    new[]
                    {
                        OperationEntry(ESAgentArtifactOperationMode.CreateOrUpdate, selectedValue,
                            "自动创建或更新", "目标不存在时创建；通过稳定 ArtifactId 找到已有目标时更新。", select,
                            "推荐"),
                        OperationEntry(ESAgentArtifactOperationMode.CreateOnly, selectedValue,
                            "仅创建", "目标或目录已经存在时立即阻断，避免覆盖。", select),
                        OperationEntry(ESAgentArtifactOperationMode.UpdateOnly, selectedValue,
                            "仅更新", "找不到携带相同 ArtifactId 的正式产物时立即阻断。", select)
                    },
                    minimumWindowSize: new Vector2(500f, 280f));
            };
            return row;
        }

        private static ESSearchDropdown.Entry OperationEntry(ESAgentArtifactOperationMode value,
            ESAgentArtifactOperationMode current, string label, string description,
            Action<ESAgentArtifactOperationMode> onSelected, string badge = null)
        {
            bool selected = value == current;
            return ESSearchDropdown.Entry.Item(
                label,
                () => onSelected?.Invoke(value),
                subtitle: description,
                badge: selected ? "当前" : badge,
                selected: selected);
        }

        private static string OperationLabel(ESAgentArtifactOperationMode value)
        {
            switch (value)
            {
                case ESAgentArtifactOperationMode.CreateOnly:
                    return "仅创建";
                case ESAgentArtifactOperationMode.UpdateOnly:
                    return "仅更新";
                default:
                    return "自动创建或更新";
            }
        }

        protected static IEnumerable<ESSearchDropdown.Entry> PathEntries(IEnumerable<string> paths,
            string currentPath, Action<string> onSelected)
        {
            string current = NormalizePickerPath(currentPath);
            if (paths == null)
                yield break;
            foreach (string rawPath in paths)
            {
                string path = NormalizePickerPath(rawPath);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.StartsWith("<", StringComparison.Ordinal))
                {
                    yield return ESSearchDropdown.Entry.Disabled(path, tooltip: "请刷新列表或确认对应目录中已有可用内容。");
                    continue;
                }

                string captured = path;
                bool selected = string.Equals(current, path, StringComparison.Ordinal);
                yield return ESSearchDropdown.Entry.Item(
                    GetPickerDisplayName(path),
                    () => onSelected?.Invoke(captured),
                    GetPickerGroup(path),
                    subtitle: GetPickerParentCaption(path),
                    badge: selected ? "当前" : null,
                    selected: selected);
            }
        }

        private static string NormalizePickerPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string GetPickerDisplayName(string path)
        {
            string trimmed = path.TrimEnd('/');
            int separator = trimmed.LastIndexOf('/');
            return separator >= 0 ? trimmed.Substring(separator + 1) : trimmed;
        }

        private static string GetPickerParentCaption(string path)
        {
            string[] segments = path.TrimEnd('/').Split('/');
            int parentCount = segments.Length - 1;
            if (parentCount <= 0)
                return string.Empty;
            if (parentCount <= 2)
                return string.Join("/", segments.Take(parentCount));
            return "…/" + segments[parentCount - 2] + "/" + segments[parentCount - 1];
        }

        private static string GetPickerGroup(string path)
        {
            const string warningsRoot = "Assets/Plugins/ES/AIWarnings/";
            if (path.StartsWith(warningsRoot, StringComparison.Ordinal))
            {
                string tail = path.Substring(warningsRoot.Length);
                int separator = tail.IndexOf('/');
                return separator > 0 ? "项目规则/" + tail.Substring(0, separator) : "项目规则";
            }
            if (path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal))
                return "AICommand 命令";
            if (path.StartsWith(".agents/skills/", StringComparison.Ordinal))
            {
                if (path.EndsWith("/", StringComparison.Ordinal))
                    return "Agent Skill 技能";
                string[] segments = path.Split('/');
                return segments.Length > 2 ? "Agent Skill 技能/" + segments[2] : "Agent Skill 技能";
            }
            if (path.StartsWith("Assets/Scripts/", StringComparison.Ordinal))
                return "C# 源码/项目逻辑";
            if (path.StartsWith("Assets/Plugins/ES/", StringComparison.Ordinal))
                return "ES 插件内容";
            if (path.StartsWith("Documentation/", StringComparison.Ordinal)
                || path.StartsWith("ES/Documentation/", StringComparison.Ordinal))
                return "项目文档";
            return "项目资产";
        }
    }

    public sealed class ESAgentGoalPayloadInspector : ESAgentPayloadInspector<ESAgentGoalPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentGoal);
        protected override VisualElement Build(ESAgentGoalPayload p, Action commit)
        { var r = new VisualElement(); r.Add(new HelpBox("最终目的和成功标准是发送、生成、更新与复制前的硬门禁。", HelpBoxMessageType.Info)); var a = Text("标题", p.title); var b = Text("最终目的", p.objective, true); var c = Text("背景与上下文", p.context, true); var d = Text("目标用户 / 触发场景", p.targetUsers, true); var e = Text("成功标准 / 最终结果", p.successCriteria, true); foreach (TextField field in new[] { a, b, c, d, e }) r.Add(field); CommitOnFocusOut(a, x => p.title=x, commit); CommitOnFocusOut(b, x => p.objective=x, commit); CommitOnFocusOut(c, x => p.context=x, commit); CommitOnFocusOut(d, x => p.targetUsers=x, commit); CommitOnFocusOut(e, x => p.successCriteria=x, commit); return r; }
    }

    public sealed class ESAgentReferencePayloadInspector : ESAgentPayloadInspector<ESAgentReferencePayload>
    {
        private static readonly List<string> ReferenceKindLabels = new List<string>
        {
            "项目最高规则 / 警告",
            "AICommand 命令",
            "Agent Skill 技能",
            "C# 源代码（高级）",
            "项目文档",
            "项目资产"
        };

        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentReference);
        protected override VisualElement Build(ESAgentReferencePayload p, Action commit)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("选择生成前需要阅读的资料。普通使用可从下拉列表选择，也可以直接拖入项目资产。",
                HelpBoxMessageType.Info));
            var kind = new PopupField<string>("引用类型", ReferenceKindLabels,
                Mathf.Clamp((int)p.referenceKind, 0, ReferenceKindLabels.Count - 1));
            var path = Text("项目内文件路径（系统）", p.projectPath);
            path.tooltip = "相对于项目根目录的文件路径。优先使用拖入或下拉选择，避免手动输入。";
            var objectField = new ObjectField("拖入项目资产") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false,
                value = p.projectPath.StartsWith("Assets/", StringComparison.Ordinal) ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.projectPath) : null };
            VisualElement available = SearchPicker(
                "从项目中选择",
                "搜索项目资料",
                "支持中文名称搜索和目录分组；选择后会同步路径与项目资产。",
                () => ESAgentAuthoringAssetCatalog.GetReferencePaths(p.referenceKind, path.value, true),
                out Button availableButton);
            availableButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    availableButton,
                    "选择项目资料",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetReferencePaths(p.referenceKind, path.value),
                        path.value,
                        selected =>
                        {
                            p.projectPath = selected;
                            path.SetValueWithoutNotify(selected);
                            objectField.SetValueWithoutNotify(selected.StartsWith("Assets/", StringComparison.Ordinal)
                                ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(selected)
                                : null);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(560f, 380f));
            };
            var purpose = Text("用途", p.purpose, true);
            var required = new Toggle("生成前必须读取") { value = p.required };
            root.Add(kind); root.Add(path); root.Add(objectField); root.Add(available); root.Add(purpose); root.Add(required);
            kind.RegisterValueChangedCallback(e => { p.referenceKind = (ESAgentReferenceKind)Math.Max(0, ReferenceKindLabels.IndexOf(e.newValue)); commit(); });
            CommitOnFocusOut(path, value => p.projectPath = value, commit);
            objectField.RegisterValueChangedCallback(e => { string selected = AssetDatabase.GetAssetPath(e.newValue); if (string.IsNullOrEmpty(selected)) return; p.projectPath = selected.Replace('\\', '/'); path.SetValueWithoutNotify(p.projectPath); commit(); });
            CommitOnFocusOut(purpose, value => p.purpose = value, commit);
            required.RegisterValueChangedCallback(e => { p.required = e.newValue; commit(); });
            return root;
        }
    }

    public sealed class ESAgentConstraintPayloadInspector : ESAgentPayloadInspector<ESAgentConstraintPayload>
    {
        private static readonly List<string> ConstraintKindLabels = new List<string>
        {
            "必须做到",
            "禁止事项",
            "允许范围",
            "质量要求"
        };

        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentConstraint);
        protected override VisualElement Build(ESAgentConstraintPayload p, Action commit)
        { var r=new VisualElement();r.Add(new HelpBox("用自然语言描述必须做到、禁止或需要达到的质量要求。",HelpBoxMessageType.Info));var a=new PopupField<string>("规则类型",ConstraintKindLabels,Mathf.Clamp((int)p.kind,0,ConstraintKindLabels.Count-1));var b=Text("规则 / 需求",p.statement,true);var c=Text("为什么需要这条规则",p.rationale,true);var d=Text("如何确认已经做到",p.verification,true);r.Add(a);r.Add(b);r.Add(c);r.Add(d);a.RegisterValueChangedCallback(e=>{p.kind=(ESAgentConstraintKind)Math.Max(0,ConstraintKindLabels.IndexOf(e.newValue));commit();});CommitOnFocusOut(b,x=>p.statement=x,commit);CommitOnFocusOut(c,x=>p.rationale=x,commit);CommitOnFocusOut(d,x=>p.verification=x,commit);return r; }
    }

    public sealed class ESAgentAICommandOutputInspector : ESAgentPayloadInspector<ESAgentAICommandOutputPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentAICommandOutput);
        protected override VisualElement Build(ESAgentAICommandOutputPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("填写命令要解决的问题、允许修改的文件和验收方式。正式文件会在候选差异检查后才写入。",HelpBoxMessageType.Info));
            var a = Text("命令名称", p.commandName);
            var b = Text("正式文件路径（系统）", p.targetProjectPath);
            b.tooltip = "正式 AICommand 文件的项目路径，优先从下拉列表选择。";
            VisualElement picker = SearchPicker(
                "选择已有命令",
                "搜索 AICommand",
                "按中文文件名搜索已有 AICommand；选择后会同步正式文件路径。",
                () => ESAgentAuthoringAssetCatalog.GetAICommandTargets(b.value, true),
                out Button pickerButton);
            pickerButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择已有 AICommand 命令",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetAICommandTargets(b.value),
                        b.value,
                        selected =>
                        {
                            p.targetProjectPath = selected;
                            b.SetValueWithoutNotify(selected);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(560f, 380f));
            };
            VisualElement operation = OperationPicker(p.operationMode, value =>
            {
                p.operationMode = value;
                commit();
            });
            var c = Text("命令类型", p.commandType);
            var d = Text("默认允许修改的文件", p.defaultWrite);
            var e = Text("风险等级（低 / 中 / 高）", p.riskLevel);
            var f = Text("用途", p.purpose, true);
            var g = Text("预期输入", p.expectedInputs, true);
            var h = Text("执行步骤", p.executionOutline, true);
            var i = Text("验收标准", p.acceptanceCriteria, true);
            var j = Text("必须包含的章节", p.requiredSections, true);
            foreach (VisualElement v in new VisualElement[] { a, b, picker, operation, c, d, e, f, g, h, i, j }) r.Add(v);
            CommitOnFocusOut(a,x=>p.commandName=x,commit);CommitOnFocusOut(b,x=>p.targetProjectPath=x,commit);
            CommitOnFocusOut(c,x=>p.commandType=x,commit);CommitOnFocusOut(d,x=>p.defaultWrite=x,commit);CommitOnFocusOut(e,x=>p.riskLevel=x,commit);
            CommitOnFocusOut(f,x=>p.purpose=x,commit);CommitOnFocusOut(g,x=>p.expectedInputs=x,commit);CommitOnFocusOut(h,x=>p.executionOutline=x,commit);
            CommitOnFocusOut(i,x=>p.acceptanceCriteria=x,commit);CommitOnFocusOut(j,x=>p.requiredSections=x,commit);return r;
        }
    }

    public sealed class ESAgentSkillOutputInspector : ESAgentPayloadInspector<ESAgentSkillOutputPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentSkillOutput);
        protected override VisualElement Build(ESAgentSkillOutputPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("填写技能要解决的工作，并说明什么时候使用、什么时候不要使用。技能目录和文件会在候选检查后才写入。",HelpBoxMessageType.Info));
            var a = Text("技能名称（英文小写）", p.skillName);
            var b = Text("正式目录（系统）", p.targetProjectPath);
            b.tooltip = "正式技能目录，优先从下拉列表选择。";
            VisualElement picker = SearchPicker(
                "选择已有技能",
                "搜索 Agent Skill",
                "按技能目录名搜索已有 Agent Skill；选择后会同步目录和技能名称。",
                () => ESAgentAuthoringAssetCatalog.GetAgentSkillTargets(b.value, true),
                out Button pickerButton);
            pickerButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择已有 Agent Skill 技能",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetAgentSkillTargets(b.value),
                        b.value,
                        selected =>
                        {
                            p.targetProjectPath = selected;
                            b.SetValueWithoutNotify(selected);
                            string folder = selected.TrimEnd('/').Split('/').Last();
                            p.skillName = folder;
                            a.SetValueWithoutNotify(folder);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(540f, 360f));
            };
            VisualElement operation = OperationPicker(p.operationMode, value =>
            {
                p.operationMode = value;
                commit();
            });
            var c = Text("能力说明", p.description, true);
            var d = Text("触发场景与使用边界", p.triggerScenarios, true);
            var e = Text("核心工作流程", p.workflow, true);
            var f = Text("不负责的事项 / 禁止事项", p.nonGoals, true);
            var g = Text("验证步骤", p.validationSteps, true);
            var h = Text("默认使用提示", p.defaultPrompt, true);
            foreach (VisualElement v in new VisualElement[] { a, b, picker, operation, c, d, e, f, g, h }) r.Add(v);
            AddToggle(r,"生成技能入口配置（agents/openai.yaml）",()=>p.includeAgentsMetadata,v=>p.includeAgentsMetadata=v,commit);
            AddToggle(r,"允许附带参考资料目录（references/）",()=>p.includeReferences,v=>p.includeReferences=v,commit);
            AddToggle(r,"允许附带脚本目录（scripts/）",()=>p.includeScripts,v=>p.includeScripts=v,commit);
            CommitOnFocusOut(a,x=>p.skillName=x,commit);CommitOnFocusOut(b,x=>p.targetProjectPath=x,commit);
            CommitOnFocusOut(c,x=>p.description=x,commit);CommitOnFocusOut(d,x=>p.triggerScenarios=x,commit);CommitOnFocusOut(e,x=>p.workflow=x,commit);
            CommitOnFocusOut(f,x=>p.nonGoals=x,commit);CommitOnFocusOut(g,x=>p.validationSteps=x,commit);CommitOnFocusOut(h,x=>p.defaultPrompt=x,commit);return r;
        }
        private static void AddToggle(VisualElement root,string label,Func<bool> get,Action<bool> set,Action commit){var t=new Toggle(label){value=get()};root.Add(t);t.tooltip="打开后会把这一部分内容纳入候选产物；正式写入仍需要人工批准。";t.RegisterValueChangedCallback(e=>{set(e.newValue);commit();});}
    }

    public sealed class ESAgentValidationPayloadInspector : ESAgentPayloadInspector<ESAgentValidationPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentValidation);
        protected override VisualElement Build(ESAgentValidationPayload p, Action commit)
        { var r=new VisualElement();r.Add(new HelpBox("这里决定候选文件需要经过哪些检查。候选差异查看和人工批准始终开启。",HelpBoxMessageType.Info)); AddToggle(r,"检查 AICommand 命令格式",()=>p.validateAICommand,v=>p.validateAICommand=v,commit); AddToggle(r,"检查 Agent Skill 技能结构",()=>p.validateAgentSkill,v=>p.validateAgentSkill=v,commit); AddToggle(r,"检查中文编码（严格 UTF-8）",()=>p.validateUtf8,v=>p.validateUtf8=v,commit); var t=Text("其他验收要求",p.additionalRequirements,true);var c=Text("人工检查清单",p.reviewChecklist,true);r.Add(t);r.Add(c);CommitOnFocusOut(t,x=>p.additionalRequirements=x,commit);CommitOnFocusOut(c,x=>p.reviewChecklist=x,commit);return r; }
        private static void AddToggle(VisualElement root,string label,Func<bool> get,Action<bool> set,Action commit){var t=new Toggle(label){value=get()};root.Add(t);t.RegisterValueChangedCallback(e=>{set(e.newValue);commit();});}
    }
}
