using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    // GraphView 边界：AISkill Execution Graph 是固定、可烘焙、可验证的流程作者/执行工具。
    // 它当前不是已验证的动态多子 Agent 协作内核；FanOut/Join 的拓扑表达、编辑效率、
    // 并行性能、上下文隔离和执行准确性均不能替代 AIBrain/TaskContext/Automation 的证据。
    public enum ESAISkillValueType : byte
    {
        Text,
        Boolean,
        Integer,
        Choice,
        ProjectPath,
        TextList,
        ProjectPathList
    }

    [Serializable]
    public sealed class ESAISkillParameter
    {
        public string parameterId = "target";
        public string label = "目标";
        public ESAISkillValueType valueType = ESAISkillValueType.Text;
        public bool required = true;
        public string defaultValue = string.Empty;
        public string validationPattern = string.Empty;
        public string[] choices = Array.Empty<string>();
        public string[] allowedRoots = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAISkillInputPayload
    {
        public int schemaVersion = 1;
        public string skillId = "es.skill.workflow";
        public string displayName = "可复用 AI 工作流";
        public ESAISkillParameter[] parameters =
        {
            new ESAISkillParameter()
        };
    }

    [Serializable]
    public enum ESAISkillTaskInputSource : byte
    {
        WorkflowParameter,
        BoundValue,
        IterationItem
    }

    [Serializable]
    public sealed class ESAISkillTaskInputBinding
    {
        public string targetField = string.Empty;
        public ESAISkillTaskInputSource source = ESAISkillTaskInputSource.BoundValue;
        public string sourceId = string.Empty;
        public string sourcePath = string.Empty;
        public bool required = true;
    }

    [Serializable]
    public sealed class ESAISkillTaskPayload
    {
        public int schemaVersion = 1;
        public string taskId = string.Empty;
        public int taskVersion = 1;
        public string preset = "default";
        [TextArea(3, 12)] public string staticInputJson = "{}";
        public ESAISkillTaskInputBinding[] inputBindings = Array.Empty<ESAISkillTaskInputBinding>();
        [Range(0, 8)] public int retryCount;
        [Range(0, 300)] public int retryDelaySeconds = 2;
        [Range(1, ES.ESAutomationTaskContract.MaximumTimeoutSeconds)] public int timeoutSeconds = 600;
        public bool dryRun;
    }

    [Serializable]
    public sealed class ESAISkillCallPayload
    {
        public int schemaVersion = 1;
        public string sourceAssetGuid = string.Empty;
        public string targetGraphId = string.Empty;
        public string targetContentSignature = string.Empty;
        [TextArea(3, 12)] public string staticInputJson = "{}";
        public ESAISkillTaskInputBinding[] inputBindings = Array.Empty<ESAISkillTaskInputBinding>();
        [Range(1, ES.ESAutomationTaskContract.MaximumTimeoutSeconds)] public int timeoutSeconds = 600;
    }

    [Serializable]
    public sealed class ESAISkillBranchPayload
    {
        public int schemaVersion = 1;
        public string valuePath = string.Empty;
        public string expectedValue = "true";
        public bool ignoreCase = true;
    }

    [Serializable]
    public sealed class ESAISkillForEachPayload
    {
        public int schemaVersion = 1;
        public string itemsPath = string.Empty;
        [Range(1, 512)] public int maxItems = 128;
        public string itemName = "item";
    }

    [Serializable]
    public enum ESAISkillApprovalEvidenceMode : byte
    {
        BoundDataOnly = 0,
        ControlFlowFallback = 1
    }

    [Serializable]
    public sealed class ESAISkillApprovalPayload
    {
        public int schemaVersion = 1;
        public string title = "需要人工确认";
        [TextArea(2, 8)] public string message = "请检查当前步骤产物后决定是否继续。";
        public bool requireCommentOnReject = true;
        public ESAISkillApprovalEvidenceMode evidenceMode = ESAISkillApprovalEvidenceMode.BoundDataOnly;
    }

    [Serializable]
    public sealed class ESAISkillFanOutPayload
    {
        public int schemaVersion = 1;
        public string fanOutId = "fan-out";
        public bool stopOnFailure = true;
        // 当前执行器按确定顺序推进分支；不要将此节点的存在解释为真实并行 Agent 调度。
    }

    [Serializable]
    public sealed class ESAISkillJoinPayload
    {
        public int schemaVersion = 1;
        public string joinId = "join";
        public bool requireAll = true;
    }

    [Serializable]
    public sealed class ESAISkillOutputPayload
    {
        public int schemaVersion = 1;
        public string outputId = "result";
        public string displayName = "工作流结果";
    }

    [Serializable]
    public sealed class ESAISkillExecutionPort
    {
        public string portId = string.Empty;
        public string portKey = string.Empty;
        public string meaning = string.Empty;
        public string valueTypeId = string.Empty;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity;
        public ESGraphPortAggregation aggregation;
    }

    [Serializable]
    public sealed class ESAISkillExecutionStep
    {
        public string nodeId = string.Empty;
        public string nodeTypeId = string.Empty;
        public string title = string.Empty;
        public ESAISkillExecutionPort[] ports = Array.Empty<ESAISkillExecutionPort>();
        public ESAISkillInputPayload input;
        public ESAISkillTaskPayload task;
        public ESAISkillCallPayload skillCall;
        public ESAISkillBranchPayload branch;
        public ESAISkillForEachPayload forEach;
        public ESAISkillApprovalPayload approval;
        public ESAISkillFanOutPayload fanOut;
        public ESAISkillJoinPayload join;
        public ESAISkillOutputPayload output;
    }

    [Serializable]
    public sealed class ESAISkillControlEdge
    {
        public string edgeId = string.Empty;
        public int order;
        public string sourceNodeId = string.Empty;
        public string sourcePortId = string.Empty;
        public string sourcePortKey = string.Empty;
        public string sourceMeaning = string.Empty;
        public string targetNodeId = string.Empty;
        public string targetPortId = string.Empty;
        public string targetPortKey = string.Empty;
        public string targetMeaning = string.Empty;
    }

    [Serializable]
    public sealed class ESAISkillDataBinding
    {
        public string edgeId = string.Empty;
        public int order;
        public string sourceNodeId = string.Empty;
        public string sourcePortId = string.Empty;
        public string sourcePortKey = string.Empty;
        public string sourceMeaning = string.Empty;
        public string targetNodeId = string.Empty;
        public string targetPortId = string.Empty;
        public string targetPortKey = string.Empty;
        public string targetMeaning = string.Empty;
        public string sourceValueTypeId = string.Empty;
        public string targetValueTypeId = string.Empty;
        public ESGraphPortAggregation targetAggregation = ESGraphPortAggregation.Auto;
    }

    [Serializable]
    public sealed class ESAISkillFanOutJoinPair
    {
        public string fanOutNodeId = string.Empty;
        public string fanOutId = string.Empty;
        public string joinNodeId = string.Empty;
        public string joinId = string.Empty;
    }

    [Serializable]
    public sealed class ESAISkillExecutionSpec : IESBakedGraphPlan
    {
        public const int CurrentSchemaVersion = 7;
        public int schemaVersion = CurrentSchemaVersion;
        public string sourceGraphId = string.Empty;
        [JsonIgnore] public string sourceAssetGuid = string.Empty;
        public string sourceContentSignature = string.Empty;
        public string skillId = string.Empty;
        public string displayName = string.Empty;
        public string entryNodeId = string.Empty;
        public ESAISkillParameter[] parameters = Array.Empty<ESAISkillParameter>();
        public ESAISkillExecutionStep[] steps = Array.Empty<ESAISkillExecutionStep>();
        public ESAISkillControlEdge[] controlEdges = Array.Empty<ESAISkillControlEdge>();
        public ESAISkillDataBinding[] dataBindings = Array.Empty<ESAISkillDataBinding>();
        public ESAISkillFanOutJoinPair[] fanOutJoinPairs = Array.Empty<ESAISkillFanOutJoinPair>();

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public string DomainId => Domain.StableId;
        public string SourceContentSignature => sourceContentSignature;
    }

    public sealed class ESAISkillExecutionBaker : IESGraphPlanBaker<ESAISkillExecutionSpec>
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;

        public bool TryBake(ESBakedGraphSnapshot source, out ESAISkillExecutionSpec plan,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            plan = null;
            if (!ESGraphPlanBakeGuard.TryValidateSource(source, Domain, out issues))
                return false;

            var failures = new List<ESGraphValidationIssue>();
            var steps = new List<ESAISkillExecutionStep>();
            foreach (ESGraphNodeSnapshot node in source.Nodes)
            {
                if (!TryBakeStep(node, out ESAISkillExecutionStep step, out string error))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Payload", error, node.NodeId));
                else
                    steps.Add(step);
            }

            var controls = new List<ESAISkillControlEdge>();
            var bindings = new List<ESAISkillDataBinding>();
            foreach (ESGraphRouteSnapshot route in source.Routes)
            {
                if (route.SourceNode == null || route.TargetNode == null
                    || route.SourcePort == null || route.TargetPort == null)
                {
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Edge",
                        "无法解析执行边端点。", route.EdgeId));
                    continue;
                }
                if (string.Equals(route.SourceValueTypeId, ESAgentGraphStableIds.SkillControlPort,
                        StringComparison.Ordinal))
                {
                    controls.Add(new ESAISkillControlEdge
                    {
                        edgeId = route.EdgeId,
                        order = route.Order,
                        sourceNodeId = route.SourceNodeId,
                        sourcePortId = route.SourcePortId,
                        sourcePortKey = route.SourcePortKey,
                        sourceMeaning = route.SourceMeaning,
                        targetNodeId = route.TargetNodeId,
                        targetPortId = route.TargetPortId,
                        targetPortKey = route.TargetPortKey,
                        targetMeaning = route.TargetMeaning
                    });
                }
                else
                {
                    bindings.Add(new ESAISkillDataBinding
                    {
                        edgeId = route.EdgeId,
                        order = route.Order,
                        sourceNodeId = route.SourceNodeId,
                        sourcePortId = route.SourcePortId,
                        sourcePortKey = route.SourcePortKey,
                        sourceMeaning = route.SourceMeaning,
                        targetNodeId = route.TargetNodeId,
                        targetPortId = route.TargetPortId,
                        targetPortKey = route.TargetPortKey,
                        targetMeaning = route.TargetMeaning,
                        sourceValueTypeId = route.SourceValueTypeId,
                        targetValueTypeId = route.TargetValueTypeId,
                        targetAggregation = route.TargetAggregation
                    });
                }
            }

            ValidateTopology(steps, controls, bindings, failures,
                out ESAISkillFanOutJoinPair[] fanOutJoinPairs);
            if (failures.Count > 0)
            {
                issues = failures;
                return false;
            }

            ESAISkillExecutionStep entry = steps.Single(step => step.input != null);
            plan = new ESAISkillExecutionSpec
            {
                sourceGraphId = source.GraphId,
                sourceContentSignature = source.ContentSignature,
                skillId = entry.input.skillId.Trim(),
                displayName = entry.input.displayName.Trim(),
                entryNodeId = entry.nodeId,
                parameters = entry.input.parameters ?? Array.Empty<ESAISkillParameter>(),
                steps = steps.OrderBy(step => step.nodeId, StringComparer.Ordinal).ToArray(),
                controlEdges = controls.OrderBy(edge => edge.order)
                    .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray(),
                dataBindings = bindings.OrderBy(edge => edge.order)
                    .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray(),
                fanOutJoinPairs = fanOutJoinPairs
            };
            issues = failures;
            return true;
        }

        private static bool TryBakeStep(ESGraphNodeSnapshot node, out ESAISkillExecutionStep step,
            out string error)
        {
            step = new ESAISkillExecutionStep
            {
                nodeId = node.NodeId,
                nodeTypeId = node.TypeId,
                title = node.Title,
                ports = (node.Ports ?? Array.Empty<ESGraphPortSnapshot>())
                    .Where(port => port != null)
                    .OrderBy(port => port.PortId, StringComparer.Ordinal)
                    .Select(port => new ESAISkillExecutionPort
                    {
                        portId = port.PortId,
                        portKey = port.StableKey,
                        meaning = port.Meaning,
                        valueTypeId = port.ValueTypeId,
                        direction = port.Direction,
                        capacity = port.Capacity,
                        aggregation = port.Aggregation,
                    }).ToArray()
            };
            try
            {
                switch (node.TypeId)
                {
                    case ESAgentGraphStableIds.SkillInputNode:
                        step.input = JsonUtility.FromJson<ESAISkillInputPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillTaskNode:
                        step.task = JsonUtility.FromJson<ESAISkillTaskPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillCallNode:
                        step.skillCall = JsonUtility.FromJson<ESAISkillCallPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillBranchNode:
                        step.branch = JsonUtility.FromJson<ESAISkillBranchPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillForEachNode:
                        step.forEach = JsonUtility.FromJson<ESAISkillForEachPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillApprovalNode:
                        step.approval = JsonUtility.FromJson<ESAISkillApprovalPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillFanOutNode:
                        step.fanOut = JsonUtility.FromJson<ESAISkillFanOutPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillJoinNode:
                        step.join = JsonUtility.FromJson<ESAISkillJoinPayload>(node.PayloadJson);
                        break;
                    case ESAgentGraphStableIds.SkillOutputNode:
                        step.output = JsonUtility.FromJson<ESAISkillOutputPayload>(node.PayloadJson);
                        break;
                    default:
                        error = "执行图包含生成型或未知节点：" + node.TypeId;
                        return false;
                }
            }
            catch (Exception exception)
            {
                error = "Payload JSON 无法解析：" + exception.Message;
                return false;
            }
            if (step.input == null && step.task == null && step.skillCall == null
                && step.branch == null && step.forEach == null
                && step.approval == null && step.fanOut == null && step.join == null
                && step.output == null)
            {
                error = "节点缺少可用执行 Payload。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static void ValidateTopology(List<ESAISkillExecutionStep> steps,
            List<ESAISkillControlEdge> controls, List<ESAISkillDataBinding> bindings,
            List<ESGraphValidationIssue> failures,
            out ESAISkillFanOutJoinPair[] fanOutJoinPairs)
        {
            fanOutJoinPairs = Array.Empty<ESAISkillFanOutJoinPair>();
            ESAISkillExecutionStep[] entries = steps.Where(step => step.input != null).ToArray();
            if (entries.Length != 1)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.EntryCount",
                    "执行图必须且只能有一个参数入口，当前为 " + entries.Length + " 个。"));
            if (steps.Count(step => step.output != null) < 1)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.OutputMissing", "执行图至少需要一个结构化输出。"));

            var ids = new HashSet<string>(steps.Select(step => step.nodeId), StringComparer.Ordinal);
            if (ids.Count != steps.Count || ids.Any(id => !ESGraphIdentity.IsValid(id)))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.NodeIdentity",
                    "执行步骤必须具有有效且唯一的稳定 NodeId。"));
            var portsByEndpoint = new Dictionary<ESGraphEndpointKey, ESAISkillExecutionPort>();
            var portIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAISkillExecutionStep step in steps)
            {
                foreach (ESAISkillExecutionPort port in step?.ports
                             ?? Array.Empty<ESAISkillExecutionPort>())
                {
                    var endpoint = new ESGraphEndpointKey(step.nodeId, port?.portKey);
                    if (port == null || !ESGraphIdentity.IsValid(port.portId)
                        || !ESGraphStableIdUtility.IsValid(port.portKey)
                        || !ESGraphEndpointRules.IsValidMeaning(port.meaning)
                        || !ESGraphPortValueCatalog.IsValidStableId(port.valueTypeId)
                        || !Enum.IsDefined(typeof(ESGraphPortDirection), port.direction)
                        || !Enum.IsDefined(typeof(ESGraphPortCapacity), port.capacity)
                        || !Enum.IsDefined(typeof(ESGraphPortAggregation), port.aggregation)
                        || port.aggregation == ESGraphPortAggregation.Auto
                        || !portIds.Add(port.portId) || !portsByEndpoint.TryAdd(endpoint, port))
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.PortIdentity",
                            "执行步骤包含无效或重复的稳定端点。", step?.nodeId));
                }
            }
            var controlKeys = new HashSet<ESGraphEndpointKey>();
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            var edgeOrders = new HashSet<int>();
            foreach (ESAISkillControlEdge edge in controls)
            {
                ESAISkillExecutionStep sourceStep = steps.FirstOrDefault(step =>
                    step.nodeId == edge.sourceNodeId);
                bool allowFanOut = sourceStep?.fanOut != null
                    && edge.sourcePortKey == ESAgentGraphStableIds.SkillFanOutPortKey;
                bool hasSourcePort = portsByEndpoint.TryGetValue(
                    new ESGraphEndpointKey(edge.sourceNodeId, edge.sourcePortKey),
                    out ESAISkillExecutionPort sourcePort);
                bool hasTargetPort = portsByEndpoint.TryGetValue(
                    new ESGraphEndpointKey(edge.targetNodeId, edge.targetPortKey),
                    out ESAISkillExecutionPort targetPort);
                if (!ESGraphIdentity.IsValid(edge.edgeId) || !edgeIds.Add(edge.edgeId)
                    || edge.order < 0 || !edgeOrders.Add(edge.order)
                    || !ESGraphIdentity.IsValid(edge.sourcePortId)
                    || !ESGraphIdentity.IsValid(edge.targetPortId)
                    || !ESGraphStableIdUtility.IsValid(edge.sourcePortKey)
                    || !ESGraphStableIdUtility.IsValid(edge.targetPortKey)
                    || !ESGraphEndpointRules.IsValidMeaning(edge.sourceMeaning)
                    || !ESGraphEndpointRules.IsValidMeaning(edge.targetMeaning)
                    || !ids.Contains(edge.sourceNodeId) || !ids.Contains(edge.targetNodeId)
                    || !hasSourcePort || !hasTargetPort
                    || sourcePort.direction != ESGraphPortDirection.Output
                    || targetPort.direction != ESGraphPortDirection.Input
                    || !string.Equals(sourcePort.portId, edge.sourcePortId,
                        StringComparison.Ordinal)
                    || !string.Equals(targetPort.portId, edge.targetPortId,
                        StringComparison.Ordinal)
                    || !string.Equals(sourcePort.meaning, edge.sourceMeaning,
                        StringComparison.Ordinal)
                    || !string.Equals(targetPort.meaning, edge.targetMeaning,
                        StringComparison.Ordinal)
                    || !string.Equals(sourcePort.valueTypeId,
                        ESAgentGraphStableIds.SkillControlPort, StringComparison.Ordinal)
                    || !string.Equals(targetPort.valueTypeId,
                        ESAgentGraphStableIds.SkillControlPort, StringComparison.Ordinal)
                    || (!allowFanOut
                        && !controlKeys.Add(new ESGraphEndpointKey(edge.sourceNodeId,
                            edge.sourcePortKey))))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ControlEdge",
                        allowFanOut ? "多目标出口包含无效关系。"
                            : "控制出口只能连接一个确定目标。", edge.edgeId));
            }

            foreach (ESAISkillExecutionStep step in steps)
            {
                if (step.input != null) ValidateInput(step, failures);
                if (step.task != null) ValidateTask(step, failures);
                if (step.skillCall != null) ValidateSkillCall(step, failures);
                if (step.branch != null) ValidateBranch(step, failures);
                if (step.fanOut != null) ValidateFanOut(step, controls, failures);
                if (step.join != null) ValidateJoin(step, controls, failures);
                if (step.input == null && !controls.Any(edge => edge.targetNodeId == step.nodeId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ControlInput",
                        "执行节点缺少控制输入，数据连线不能代替执行顺序。", step.nodeId));
                if ((step.branch != null || step.forEach != null || step.output != null)
                    && !bindings.Any(edge => edge.targetNodeId == step.nodeId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ValueInput",
                        "该节点缺少必需值输入。", step.nodeId));
                bool isForEachBody = (step.task != null || step.skillCall != null) && controls.Any(edge =>
                    edge.targetNodeId == step.nodeId
                    && edge.sourcePortKey == ESAgentGraphStableIds.SkillItemPortKey
                    && steps.Any(owner => owner.nodeId == edge.sourceNodeId && owner.forEach != null));
                foreach (string requiredPort in RequiredControlOutputs(step, isForEachBody))
                {
                    if (!controls.Any(edge => edge.sourceNodeId == step.nodeId
                            && edge.sourcePortKey == requiredPort))
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.RequiredRoute",
                            "节点缺少必需控制出口：" + requiredPort, step.nodeId));
                }
                if (step.forEach != null)
                {
                    if (step.forEach.maxItems < 1 || step.forEach.maxItems > 512)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ForEachLimit",
                            "ForEach 最大数量必须位于 1-512。", step.nodeId));
                    ESAISkillControlEdge[] itemRoutes = controls.Where(edge => edge.sourceNodeId == step.nodeId
                            && edge.sourcePortKey == ESAgentGraphStableIds.SkillItemPortKey)
                        .ToArray();
                    ESAISkillExecutionStep body = itemRoutes.Length == 1
                        ? steps.FirstOrDefault(value => value.nodeId == itemRoutes[0].targetNodeId)
                        : null;
                    if (body?.task == null && body?.skillCall == null)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ForEachBody",
                            "ForEach 的逐项出口必须且只能直接连接一个 Task 或调用 AISkill；循环由协调器内部管理。", step.nodeId));
                }
            }

            foreach (ESAISkillDataBinding binding in bindings)
            {
                bool hasSourcePort = portsByEndpoint.TryGetValue(
                    new ESGraphEndpointKey(binding.sourceNodeId, binding.sourcePortKey),
                    out ESAISkillExecutionPort sourcePort);
                bool hasTargetPort = portsByEndpoint.TryGetValue(
                    new ESGraphEndpointKey(binding.targetNodeId, binding.targetPortKey),
                    out ESAISkillExecutionPort targetPort);
                if (!ESGraphIdentity.IsValid(binding.edgeId) || !edgeIds.Add(binding.edgeId)
                    || binding.order < 0 || !edgeOrders.Add(binding.order)
                    || !ESGraphIdentity.IsValid(binding.sourcePortId)
                    || !ESGraphIdentity.IsValid(binding.targetPortId)
                    || !ESGraphStableIdUtility.IsValid(binding.sourcePortKey)
                    || !ESGraphStableIdUtility.IsValid(binding.targetPortKey)
                    || !ESGraphEndpointRules.IsValidMeaning(binding.sourceMeaning)
                    || !ESGraphEndpointRules.IsValidMeaning(binding.targetMeaning)
                    || !ids.Contains(binding.sourceNodeId) || !ids.Contains(binding.targetNodeId)
                    || !ESGraphPortValueCatalog.IsValidStableId(binding.sourceValueTypeId)
                    || !ESGraphPortValueCatalog.IsValidStableId(binding.targetValueTypeId)
                    || !Enum.IsDefined(typeof(ESGraphPortAggregation), binding.targetAggregation)
                    || binding.targetAggregation == ESGraphPortAggregation.Auto
                    || !hasSourcePort || !hasTargetPort
                    || sourcePort.direction != ESGraphPortDirection.Output
                    || targetPort.direction != ESGraphPortDirection.Input
                    || !string.Equals(sourcePort.portId, binding.sourcePortId,
                        StringComparison.Ordinal)
                    || !string.Equals(targetPort.portId, binding.targetPortId,
                        StringComparison.Ordinal)
                    || !string.Equals(sourcePort.meaning, binding.sourceMeaning,
                        StringComparison.Ordinal)
                    || !string.Equals(targetPort.meaning, binding.targetMeaning,
                        StringComparison.Ordinal)
                    || !string.Equals(sourcePort.valueTypeId, binding.sourceValueTypeId,
                        StringComparison.Ordinal)
                    || !string.Equals(targetPort.valueTypeId, binding.targetValueTypeId,
                        StringComparison.Ordinal)
                    || targetPort.aggregation != binding.targetAggregation)
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Binding",
                        "值绑定缺少稳定端点、类型或已解析的输入聚合方式。", binding.edgeId));
            }
            foreach (IGrouping<ESGraphEndpointKey, ESAISkillDataBinding> group in bindings.GroupBy(binding =>
                new ESGraphEndpointKey(binding.targetNodeId, binding.targetPortKey)))
            {
                ESAISkillDataBinding[] groupBindings = group.ToArray();
                ESGraphPortAggregation[] modes = groupBindings.Select(binding => binding.targetAggregation)
                    .Where(mode => mode != ESGraphPortAggregation.Auto)
                    .Distinct().ToArray();
                if (modes.Length > 1)
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.BindingAggregation",
                        "同一输入端点的多条关系必须声明一致的聚合模式。", group.Key.ToString()));
                if (modes.Length == 1 && modes[0] == ESGraphPortAggregation.Single
                    && groupBindings.Length > 1)
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.BindingSingle",
                        "Single 输入端点不能接收多条值关系。", group.Key.ToString()));
            }
            fanOutJoinPairs = ValidateFanOutJoinPairing(steps, controls, failures);
        }

        internal static bool TryValidateSpec(ESAISkillExecutionSpec spec, out string error)
        {
            if (spec == null || spec.schemaVersion != ESAISkillExecutionSpec.CurrentSchemaVersion
                || !ESGraphIdentity.IsValid(spec.sourceGraphId)
                || !ES.ESAutomationWorkerRegistration.IsSha256(spec.sourceContentSignature)
                || spec.steps == null || spec.controlEdges == null || spec.dataBindings == null
                || spec.fanOutJoinPairs == null
                || spec.steps.Any(step => step == null)
                || spec.controlEdges.Any(edge => edge == null)
                || spec.dataBindings.Any(binding => binding == null)
                || spec.fanOutJoinPairs.Any(pair => pair == null))
            {
                error = "执行合同缺少有效的版本、GraphId、内容签名或拓扑集合。";
                return false;
            }

            var failures = new List<ESGraphValidationIssue>();
            var steps = spec.steps.ToList();
            ValidateTopology(steps, spec.controlEdges.ToList(), spec.dataBindings.ToList(), failures,
                out ESAISkillFanOutJoinPair[] expectedPairs);
            if (!FanOutJoinPairsMatch(spec.fanOutJoinPairs, expectedPairs))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutJoinContract",
                    "执行合同中的 FanOut/Join 配对与已烘焙拓扑不一致。"));
            ESAISkillExecutionStep[] entries = steps.Where(step => step.input != null).ToArray();
            if (entries.Length == 1)
            {
                ESAISkillExecutionStep entry = entries[0];
                if (!string.Equals(spec.entryNodeId, entry.nodeId, StringComparison.Ordinal)
                    || !string.Equals(spec.skillId?.Trim(), entry.input.skillId?.Trim(),
                        StringComparison.Ordinal)
                    || !string.Equals(spec.displayName?.Trim(), entry.input.displayName?.Trim(),
                        StringComparison.Ordinal)
                    || !JToken.DeepEquals(JArray.FromObject(spec.parameters
                            ?? Array.Empty<ESAISkillParameter>()),
                        JArray.FromObject(entry.input.parameters
                            ?? Array.Empty<ESAISkillParameter>())))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.EntryContract",
                        "执行合同入口、SkillId、名称或参数与入口节点不一致。", spec.entryNodeId));
            }

            foreach (ESAISkillExecutionStep step in steps)
            {
                int payloadCount = (step.input != null ? 1 : 0) + (step.task != null ? 1 : 0)
                    + (step.skillCall != null ? 1 : 0) + (step.branch != null ? 1 : 0)
                    + (step.forEach != null ? 1 : 0) + (step.approval != null ? 1 : 0)
                    + (step.fanOut != null ? 1 : 0) + (step.join != null ? 1 : 0)
                    + (step.output != null ? 1 : 0);
                if (payloadCount != 1 || !StepTypeMatchesPayload(step))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.StepContract",
                        "每个执行步骤必须且只能携带与 NodeType 对应的一份 Payload。", step.nodeId));
                if (step.approval != null
                    && step.approval.evidenceMode != ESAISkillApprovalEvidenceMode.BoundDataOnly
                    && step.approval.evidenceMode != ESAISkillApprovalEvidenceMode.ControlFlowFallback)
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ApprovalEvidenceMode",
                        "Approval 的证据来源模式无效；控制流回退必须显式声明。", step.nodeId));
            }

            ESGraphValidationIssue first = failures.FirstOrDefault(issue => issue != null
                && issue.severity == ESGraphValidationSeverity.Error);
            error = first?.message ?? string.Empty;
            return first == null;
        }

        private static bool StepTypeMatchesPayload(ESAISkillExecutionStep step)
        {
            if (step == null) return false;
            if (step.input != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillInputNode;
            if (step.task != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillTaskNode;
            if (step.skillCall != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillCallNode;
            if (step.branch != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillBranchNode;
            if (step.forEach != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillForEachNode;
            if (step.approval != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillApprovalNode;
            if (step.fanOut != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillFanOutNode;
            if (step.join != null) return step.nodeTypeId == ESAgentGraphStableIds.SkillJoinNode;
            return step.output != null && step.nodeTypeId == ESAgentGraphStableIds.SkillOutputNode;
        }

        private static ESAISkillFanOutJoinPair[] ValidateFanOutJoinPairing(
            List<ESAISkillExecutionStep> steps,
            List<ESAISkillControlEdge> controls, List<ESGraphValidationIssue> failures)
        {
            var pairs = new List<ESAISkillFanOutJoinPair>();
            Dictionary<string, ESAISkillExecutionStep> stepsById = steps
                .Where(step => step != null && !string.IsNullOrWhiteSpace(step.nodeId))
                .GroupBy(step => step.nodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            Dictionary<string, ESAISkillControlEdge[]> outgoingByNode = controls
                .Where(edge => edge != null && !string.IsNullOrWhiteSpace(edge.sourceNodeId))
                .GroupBy(edge => edge.sourceNodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(edge => edge.order)
                        .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);
            foreach (IGrouping<string, ESAISkillExecutionStep> duplicate in steps
                         .Where(step => step?.fanOut != null)
                         .GroupBy(step => step.fanOut.fanOutId?.Trim() ?? string.Empty,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutIdentity",
                    "FanOut 稳定身份必须唯一：" + duplicate.Key,
                    duplicate.First().nodeId));
            foreach (IGrouping<string, ESAISkillExecutionStep> duplicate in steps
                         .Where(step => step?.join != null)
                         .GroupBy(step => step.join.joinId?.Trim() ?? string.Empty,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.JoinIdentity",
                    "Join 稳定身份必须唯一：" + duplicate.Key,
                    duplicate.First().nodeId));

            foreach (ESAISkillExecutionStep fanOut in steps.Where(step => step?.fanOut != null))
            {
                ESAISkillControlEdge[] starts = controls.Where(edge => edge.sourceNodeId == fanOut.nodeId
                    && edge.sourcePortKey == ESAgentGraphStableIds.SkillFanOutPortKey)
                    .OrderBy(edge => edge.order)
                    .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray();
                string sharedJoinId = string.Empty;
                var branchOwners = new Dictionary<string, string>(StringComparer.Ordinal);
                var reportedOverlaps = new HashSet<string>(StringComparer.Ordinal);
                foreach (ESAISkillControlEdge start in starts)
                {
                    var queue = new Queue<string>();
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var routeJoins = new HashSet<string>(StringComparer.Ordinal);
                    bool containsNestedFanOut = false;
                    bool reachesOutputBeforeJoin = false;
                    queue.Enqueue(start.targetNodeId);
                    while (queue.Count > 0)
                    {
                        string nodeId = queue.Dequeue();
                        if (!visited.Add(nodeId)) continue;
                        if (!stepsById.TryGetValue(nodeId, out ESAISkillExecutionStep node))
                            continue;
                        if (node == null) continue;
                        if (node.join != null)
                        {
                            routeJoins.Add(node.nodeId);
                            continue;
                        }
                        if (node.fanOut != null)
                        {
                            containsNestedFanOut = true;
                            continue;
                        }
                        if (node.output != null)
                        {
                            reachesOutputBeforeJoin = true;
                            continue;
                        }
                        if (outgoingByNode.TryGetValue(nodeId, out ESAISkillControlEdge[] outgoing))
                            foreach (ESAISkillControlEdge edge in outgoing)
                                queue.Enqueue(edge.targetNodeId);
                    }
                    if (containsNestedFanOut)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.NestedFanOut",
                            "当前版本不允许 FanOut 分支内再次进入 FanOut。", start.edgeId));
                    if (reachesOutputBeforeJoin)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutOutput",
                            "FanOut 的每条可能路线都必须先经过 Join，不能直接到达输出。", start.edgeId));
                    if (routeJoins.Count == 0)
                    {
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutJoinMissing",
                            "FanOut 的每个直接目标都必须可达一个 Join。", start.edgeId));
                        continue;
                    }
                    if (routeJoins.Count > 1)
                    {
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutJoinAmbiguous",
                            "FanOut 的同一目标存在多个可能 Join。", start.edgeId));
                        continue;
                    }
                    foreach (string visitedNodeId in visited.Where(nodeId => !routeJoins.Contains(nodeId)))
                    {
                        if (!branchOwners.TryGetValue(visitedNodeId, out string ownerEdgeId))
                        {
                            branchOwners.Add(visitedNodeId, start.edgeId);
                            continue;
                        }
                        if (!string.Equals(ownerEdgeId, start.edgeId, StringComparison.Ordinal)
                            && reportedOverlaps.Add(visitedNodeId))
                            failures.Add(ESGraphValidationIssue.Error(
                                "AISkill.Execution.FanOutEarlyMerge",
                                "FanOut 的不同直接分支只能在共同 Join 汇合，不能提前共享执行节点。",
                                visitedNodeId));
                    }
                    string routeJoinId = routeJoins.Single();
                    if (string.IsNullOrEmpty(sharedJoinId))
                        sharedJoinId = routeJoinId;
                    else if (!string.Equals(sharedJoinId, routeJoinId, StringComparison.Ordinal))
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutJoinMismatch",
                            "FanOut 的所有直接目标必须汇合到同一个 Join。", start.edgeId));
                }
                if (!string.IsNullOrEmpty(sharedJoinId)
                    && stepsById.TryGetValue(sharedJoinId, out ESAISkillExecutionStep pairedJoin)
                    && pairedJoin?.join != null)
                    pairs.Add(new ESAISkillFanOutJoinPair
                    {
                        fanOutNodeId = fanOut.nodeId,
                        fanOutId = fanOut.fanOut.fanOutId?.Trim() ?? string.Empty,
                        joinNodeId = pairedJoin.nodeId,
                        joinId = pairedJoin.join.joinId?.Trim() ?? string.Empty
                    });
            }
            return pairs.OrderBy(pair => pair.fanOutNodeId, StringComparer.Ordinal).ToArray();
        }

        private static bool FanOutJoinPairsMatch(IEnumerable<ESAISkillFanOutJoinPair> actual,
            IEnumerable<ESAISkillFanOutJoinPair> expected)
        {
            ESAISkillFanOutJoinPair[] left = (actual ?? Array.Empty<ESAISkillFanOutJoinPair>())
                .OrderBy(pair => pair?.fanOutNodeId, StringComparer.Ordinal).ToArray();
            ESAISkillFanOutJoinPair[] right = (expected ?? Array.Empty<ESAISkillFanOutJoinPair>())
                .OrderBy(pair => pair?.fanOutNodeId, StringComparer.Ordinal).ToArray();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] == null || right[i] == null
                    || !string.Equals(left[i].fanOutNodeId, right[i].fanOutNodeId,
                        StringComparison.Ordinal)
                    || !string.Equals(left[i].fanOutId, right[i].fanOutId,
                        StringComparison.Ordinal)
                    || !string.Equals(left[i].joinNodeId, right[i].joinNodeId,
                        StringComparison.Ordinal)
                    || !string.Equals(left[i].joinId, right[i].joinId,
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static IEnumerable<string> RequiredControlOutputs(ESAISkillExecutionStep step,
            bool isForEachBody)
        {
            if (step.input != null) return new[] { ESAgentGraphStableIds.SkillNextPortKey };
            if (step.task != null) return isForEachBody ? Array.Empty<string>() : new[] { ESAgentGraphStableIds.SkillSuccessPortKey,
                ESAgentGraphStableIds.SkillFailurePortKey, ESAgentGraphStableIds.SkillTimeoutPortKey,
                ESAgentGraphStableIds.SkillCancelledPortKey };
            if (step.skillCall != null) return isForEachBody ? Array.Empty<string>() : new[] { ESAgentGraphStableIds.SkillSuccessPortKey,
                ESAgentGraphStableIds.SkillFailurePortKey, ESAgentGraphStableIds.SkillTimeoutPortKey,
                ESAgentGraphStableIds.SkillCancelledPortKey };
            if (step.branch != null) return new[] { ESAgentGraphStableIds.SkillMatchedPortKey,
                ESAgentGraphStableIds.SkillDefaultPortKey };
            if (step.forEach != null) return new[] { ESAgentGraphStableIds.SkillItemPortKey,
                ESAgentGraphStableIds.SkillCompletedPortKey, ESAgentGraphStableIds.SkillEmptyPortKey,
                ESAgentGraphStableIds.SkillFailurePortKey };
            if (step.approval != null) return new[] { ESAgentGraphStableIds.SkillApprovedPortKey,
                ESAgentGraphStableIds.SkillRejectedPortKey };
            if (step.fanOut != null) return new[] { ESAgentGraphStableIds.SkillFanOutPortKey };
            if (step.join != null) return new[] { ESAgentGraphStableIds.SkillJoinPortKey };
            return Array.Empty<string>();
        }

        private static void ValidateFanOut(ESAISkillExecutionStep step,
            List<ESAISkillControlEdge> controls, List<ESGraphValidationIssue> failures)
        {
            if (step.fanOut.schemaVersion != 1
                || !ESGraphStableIdUtility.IsValid(step.fanOut.fanOutId))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOut",
                    "FanOut 必须提供有效的版本和稳定身份。", step.nodeId));
            int count = controls.Count(edge => edge.sourceNodeId == step.nodeId
                && edge.sourcePortKey == ESAgentGraphStableIds.SkillFanOutPortKey);
            if (count < 2)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.FanOutTargets",
                    "FanOut 至少需要两个目标分支。", step.nodeId));
        }

        private static void ValidateBranch(ESAISkillExecutionStep step,
            List<ESGraphValidationIssue> failures)
        {
            if (step.branch.schemaVersion != 1)
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Branch",
                    "条件分支 Payload 版本无效。", step.nodeId));
                return;
            }
            if (string.IsNullOrWhiteSpace(step.branch.valuePath)) return;
            try
            {
                _ = new JObject().SelectToken(step.branch.valuePath, false);
            }
            catch (JsonException exception)
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.BranchPath",
                    "条件分支 ValuePath 无效：" + exception.Message, step.nodeId));
            }
        }

        private static void ValidateJoin(ESAISkillExecutionStep step,
            List<ESAISkillControlEdge> controls, List<ESGraphValidationIssue> failures)
        {
            if (step.join.schemaVersion != 1
                || !ESGraphStableIdUtility.IsValid(step.join.joinId))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Join",
                    "Join 必须提供有效的版本和稳定身份。", step.nodeId));
            int count = controls.Count(edge => edge.targetNodeId == step.nodeId);
            if (count < 2)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.JoinInputs",
                    "Join 至少需要两个控制输入。", step.nodeId));
        }

        private static void ValidateInput(ESAISkillExecutionStep step, List<ESGraphValidationIssue> failures)
        {
            ESAISkillInputPayload input = step.input;
            if (input.schemaVersion != 1 || !ESGraphStableIdUtility.IsValid(input.skillId)
                || string.IsNullOrWhiteSpace(input.displayName))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Input",
                    "参数入口必须提供稳定 SkillId 和显示名称。", step.nodeId));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAISkillParameter parameter in input.parameters ?? Array.Empty<ESAISkillParameter>())
            {
                if (parameter == null || !ESGraphStableIdUtility.IsValid(parameter.parameterId)
                    || !ids.Add(parameter.parameterId) || !Enum.IsDefined(typeof(ESAISkillValueType), parameter.valueType))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Parameter",
                        "参数 ID 必须稳定、唯一且类型有效。", step.nodeId));
                if (parameter?.valueType == ESAISkillValueType.Choice
                    && (parameter.choices == null || parameter.choices.Length == 0))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ParameterChoice",
                        "Choice 参数至少需要一个选项。", step.nodeId));
                if (!string.IsNullOrWhiteSpace(parameter?.validationPattern))
                {
                    try { _ = new Regex(parameter.validationPattern); }
                    catch (ArgumentException exception)
                    {
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ParameterPattern",
                            "参数正则约束无效：" + exception.Message, step.nodeId));
                    }
                }
            }
        }

        private static void ValidateTask(ESAISkillExecutionStep step, List<ESGraphValidationIssue> failures)
        {
            ESAISkillTaskPayload task = step.task;
            if (task.schemaVersion != 1 || task.taskVersion < 1
                || !ES.ESAutomationTaskRegistry.TryGet(task.taskId, task.taskVersion,
                    out ES.ESAutomationTaskContract contract))
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.TaskContract",
                    "Task 必须引用已注册的 TaskContract：" + task.taskId + "@" + task.taskVersion, step.nodeId));
                return;
            }
            try
            {
                JObject.Parse(string.IsNullOrWhiteSpace(task.staticInputJson) ? "{}" : task.staticInputJson);
            }
            catch (Exception exception)
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.TaskInput",
                    "Task 静态输入必须是 JSON Object：" + exception.Message, step.nodeId));
            }
            if (task.retryCount > 0 && !contract.supportsRetry)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Retry",
                    "该 TaskContract 未声明幂等重试能力，自动重试必须为 0。", step.nodeId));
            if (task.timeoutSeconds < 1 || task.timeoutSeconds > contract.timeoutSeconds)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Timeout",
                    "Task 超时必须位于 1 到合同上限 " + contract.timeoutSeconds + " 秒。", step.nodeId));
            if (task.retryCount < 0 || task.retryCount > 8
                || task.retryDelaySeconds < 0 || task.retryDelaySeconds > 300)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.RetryPolicy",
                    "重试次数必须位于 0-8，重试间隔必须位于 0-300 秒。", step.nodeId));
            var targetFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAISkillTaskInputBinding binding in task.inputBindings
                         ?? Array.Empty<ESAISkillTaskInputBinding>())
            {
                if (binding == null || !Regex.IsMatch(binding.targetField ?? string.Empty,
                        "^[A-Za-z][A-Za-z0-9_]*$") || !targetFields.Add(binding.targetField)
                    || !Enum.IsDefined(typeof(ESAISkillTaskInputSource), binding.source))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.TaskBinding",
                        "Task 输入映射必须有唯一的目标字段和有效来源。", step.nodeId));
                if (binding?.source == ESAISkillTaskInputSource.WorkflowParameter
                    && !ESGraphStableIdUtility.IsValid(binding.sourceId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.TaskBinding",
                        "工作流参数映射必须填写稳定 SourceId。", step.nodeId));
            }
        }

        private static void ValidateSkillCall(ESAISkillExecutionStep step,
            List<ESGraphValidationIssue> failures)
        {
            ESAISkillCallPayload call = step.skillCall;
            if (call.schemaVersion != 1 || string.IsNullOrWhiteSpace(call.sourceAssetGuid)
                || !ESGraphIdentity.IsValid(call.targetGraphId)
                || !ES.ESAutomationWorkerRegistration.IsSha256(call.targetContentSignature))
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallIdentity",
                    "调用 AISkill 必须固定 Asset GUID、GraphId 和内容签名。", step.nodeId));
                return;
            }
            if (call.timeoutSeconds < 1
                || call.timeoutSeconds > ES.ESAutomationTaskContract.MaximumTimeoutSeconds)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallTimeout",
                    "子 AISkill 超时必须位于 1-7200 秒。", step.nodeId));
            try { JObject.Parse(string.IsNullOrWhiteSpace(call.staticInputJson) ? "{}" : call.staticInputJson); }
            catch (Exception exception)
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallInput",
                    "子 AISkill 静态输入必须是 JSON Object：" + exception.Message, step.nodeId));
            }
            ValidateBindings(call.inputBindings, step.nodeId, failures);
            string assetPath = AssetDatabase.GUIDToAssetPath(call.sourceAssetGuid);
            ESGraphAssetBase target = string.IsNullOrWhiteSpace(assetPath) ? null
                : AssetDatabase.LoadAssetAtPath<ESGraphAssetBase>(assetPath);
            if (target == null)
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallTarget",
                    "调用 AISkill 的目标资产不存在。", step.nodeId));
                return;
            }
            if (!string.Equals(target.GraphId, call.targetGraphId, StringComparison.Ordinal))
            {
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallGraphId",
                    "调用 AISkill 的目标 GraphId 已漂移。", step.nodeId));
                return;
            }
            if (!ESGraphSnapshotBaker.TryBake(target, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> targetIssues))
            {
                ESGraphValidationIssue first = targetIssues?.FirstOrDefault(issue => issue != null
                    && issue.severity == ESGraphValidationSeverity.Error);
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallTarget",
                    "调用 AISkill 的目标图无法生成验证快照：" + (first?.message ?? "未知错误"), step.nodeId));
                return;
            }
            if (!string.Equals(snapshot.ContentSignature, call.targetContentSignature,
                    StringComparison.Ordinal))
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallSignature",
                    "调用 AISkill 的目标内容签名已漂移，请重新绑定。", step.nodeId));
        }

        private static void ValidateBindings(IEnumerable<ESAISkillTaskInputBinding> bindings,
            string nodeId, List<ESGraphValidationIssue> failures)
        {
            var targetFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAISkillTaskInputBinding binding in bindings
                         ?? Array.Empty<ESAISkillTaskInputBinding>())
            {
                if (binding == null || !Regex.IsMatch(binding.targetField ?? string.Empty,
                        "^[A-Za-z][A-Za-z0-9_]*$") || !targetFields.Add(binding.targetField)
                    || !Enum.IsDefined(typeof(ESAISkillTaskInputSource), binding.source))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallBinding",
                        "子 AISkill 输入映射必须有唯一目标字段和有效来源。", nodeId));
                if (binding?.source == ESAISkillTaskInputSource.WorkflowParameter
                    && !ESGraphStableIdUtility.IsValid(binding.sourceId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.SkillCallBinding",
                        "子 AISkill 参数映射必须填写稳定 SourceId。", nodeId));
            }
        }
    }

    public static class ESAISkillExecutionGraphValidator
    {
        public static bool IsExecutionGraph(ESGraphAssetBase asset)
            => asset != null && asset.Nodes.Any(node => node != null
                && ESAgentRelationSemantics.IsSkillExecutionNode(node.typeId));

        public static void ValidateMode(ESGraphAssetBase asset, List<ESGraphValidationIssue> issues)
        {
            bool hasExecution = IsExecutionGraph(asset);
            bool hasGeneration = asset != null && asset.Nodes.Any(node => node != null
                && !ESAgentRelationSemantics.IsSkillExecutionNode(node.typeId));
            if (hasExecution && hasGeneration)
                issues.Add(ESGraphValidationIssue.Error("AISkill.Execution.MixedMode",
                    "候选生成节点与 AISkill 执行节点不能放在同一张图；请拆成两个独立合同。"));
        }
    }

    public static class ESAISkillExecutionLauncher
    {
        public static bool TryBake(ESGraphAssetBase asset, out ESAISkillExecutionSpec spec,
            out string error)
        {
            spec = null;
            if (asset == null || !ESAISkillExecutionGraphValidator.IsExecutionGraph(asset))
            {
                error = "请选择一张 AISkill 执行图。";
                return false;
            }
            if (!ESGraphAuthoringRegistry.TryBake(asset, out _, out IESBakedGraphPlan plan,
                    out List<ESGraphValidationIssue> issues) || !(plan is ESAISkillExecutionSpec baked))
            {
                ESGraphValidationIssue first = issues?.FirstOrDefault(issue =>
                    issue != null && issue.severity == ESGraphValidationSeverity.Error);
                error = first?.message ?? "执行图无法构造稳定合同。";
                return false;
            }
            spec = baked;
            spec.sourceAssetGuid = ResolveAssetGuid(asset);
            error = string.Empty;
            return true;
        }

        public static bool TryCollectInputs(ESAISkillExecutionSpec spec, EditorWindow owner,
            out JObject values, out string error)
        {
            values = null;
            if (spec == null)
            {
                error = "缺少 AISkill 执行合同。";
                return false;
            }
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "es.ai-skill.run." + spec.sourceGraphId,
                title = "运行 " + spec.displayName,
                subtitle = "参数化 AISkill 执行",
                message = "参数会写入本次 input-snapshot；任务执行仍受 TaskContract、路径和能力门禁约束。",
                confirmText = "开始运行",
                preferredSize = new Vector2(620f, 560f),
                tone = ESDialogTone.Info,
                owner = owner
            };
            foreach (ESAISkillParameter parameter in spec.parameters ?? Array.Empty<ESAISkillParameter>())
            {
                string label = string.IsNullOrWhiteSpace(parameter.label)
                    ? parameter.parameterId : parameter.label;
                switch (parameter.valueType)
                {
                    case ESAISkillValueType.Boolean:
                        request.AddToggle(parameter.parameterId, label,
                            bool.TryParse(parameter.defaultValue, out bool defaultBoolean) && defaultBoolean);
                        break;
                    case ESAISkillValueType.Choice:
                        request.AddChoice(parameter.parameterId, label,
                            parameter.choices ?? Array.Empty<string>(), parameter.defaultValue,
                            parameter.required);
                        break;
                    case ESAISkillValueType.ProjectPath:
                        ESAdvancedDialogField pathField = request.AddText(parameter.parameterId,
                            label, parameter.defaultValue, parameter.required);
                        pathField.help = "填写项目相对路径；禁止绝对路径和 ..。允许根："
                            + string.Join("、", parameter.allowedRoots ?? Array.Empty<string>());
                        break;
                    case ESAISkillValueType.TextList:
                    case ESAISkillValueType.ProjectPathList:
                        request.AddMultilineText(parameter.parameterId, label,
                            parameter.defaultValue, parameter.required);
                        break;
                    default:
                        request.AddText(parameter.parameterId, label,
                            parameter.defaultValue, parameter.required);
                        break;
                }
            }
            ESAdvancedDialogResult result = ESDialogService.ShowModal(request);
            if (result == null || !result.accepted || result.values == null)
            {
                error = "用户取消运行。";
                return false;
            }
            values = BuildInputObject(spec.parameters, result.values);
            error = string.Empty;
            return true;
        }

        public static bool TryStartWithDialog(ESGraphAssetBase asset, EditorWindow owner,
            out ESAISkillWorkflowRun run, out string error)
        {
            run = null;
            if (!TryBake(asset, out ESAISkillExecutionSpec spec, out error)
                || !TryCollectInputs(spec, owner, out JObject values, out error))
                return false;
            return ESAISkillExecutionCoordinator.TryStart(spec, values, Environment.UserName,
                ResolveAssetGuid(asset), out run, out error);
        }

        internal static string ResolveAssetGuid(ESGraphAssetBase asset)
        {
            string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static JObject BuildInputObject(IEnumerable<ESAISkillParameter> parameters,
            ESAdvancedDialogValues dialogValues)
        {
            var values = new JObject();
            foreach (ESAISkillParameter parameter in parameters ?? Array.Empty<ESAISkillParameter>())
            {
                switch (parameter.valueType)
                {
                    case ESAISkillValueType.Boolean:
                        values[parameter.parameterId] = dialogValues.GetToggle(parameter.parameterId);
                        break;
                    case ESAISkillValueType.Integer:
                        string integerText = dialogValues.GetString(parameter.parameterId);
                        values[parameter.parameterId] = int.TryParse(integerText, out int integer)
                            ? integer : (JToken)integerText;
                        break;
                    case ESAISkillValueType.TextList:
                    case ESAISkillValueType.ProjectPathList:
                        values[parameter.parameterId] = new JArray(
                            dialogValues.GetString(parameter.parameterId).Split(new[] { '\r', '\n' },
                                StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()));
                        break;
                    default:
                        values[parameter.parameterId] = dialogValues.GetString(parameter.parameterId);
                        break;
                }
            }
            return values;
        }
    }

    internal static class ESAISkillExecutionMenu
    {
        private const string MenuPath = MenuItemPathDefine.AUTOMATION_AGENT_COLLABORATION_PATH
            + "运行选中的 AISkill 执行图";

        [MenuItem(MenuPath, false, 220)]
        private static void RunSelected()
        {
            if (!ESAISkillExecutionLauncher.TryStartWithDialog(
                    Selection.activeObject as ESGraphAssetBase, EditorWindow.focusedWindow,
                    out ESAISkillWorkflowRun run, out string error))
            {
                if (!string.Equals(error, "用户取消运行。", StringComparison.Ordinal))
                    EditorUtility.DisplayDialog("无法运行 AISkill", error, "确定");
                return;
            }
            SessionState.SetString("ES.AISkillGraph.LatestRun." + run.graphId, run.runId);
            Debug.Log("[ES AISkillGraph] 已启动：" + run.runId + "，状态：" + run.status);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRunSelected()
            => Selection.activeObject is ESGraphAssetBase asset
                && ESAISkillExecutionGraphValidator.IsExecutionGraph(asset);
    }

    [Serializable]
    public sealed class ESAISkillIterationRunRecord
    {
        public int index;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int attemptCount;
        public string inputHash = string.Empty;
        public string invocationId = string.Empty;
        public string childRunId = string.Empty;
        public string status = "Pending";
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string message = string.Empty;
        public string[] artifacts = Array.Empty<string>();
        public string[] outputHashes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAISkillStepRunRecord
    {
        public string nodeId = string.Empty;
        public string status = "Pending";
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public int attemptCount;
        public int currentAttemptCount;
        public string currentAttemptStartedAtUtc = string.Empty;
        public string retryAvailableAtUtc = string.Empty;
        public string invocationId = string.Empty;
        public string childRunId = string.Empty;
        public int childApprovalGeneration;
        public int exitCode = -1;
        public string message = string.Empty;
        public string[] diagnostics = Array.Empty<string>();
        public string[] artifacts = Array.Empty<string>();
        public string[] outputHashes = Array.Empty<string>();
        public List<ESAISkillIterationRunRecord> iterations =
            new List<ESAISkillIterationRunRecord>();
    }

    [Serializable]
    public sealed class ESAISkillWorkflowRun
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public string runId = Guid.NewGuid().ToString("N");
        public string graphId = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string contentSignature = string.Empty;
        public string executionSpecHash = string.Empty;
        public string runStateHash = string.Empty;
        public string skillId = string.Empty;
        public string operatorId = string.Empty;
        public string parentRunId = string.Empty;
        public int callDepth;
        public string[] ancestorGraphIds = Array.Empty<string>();
        public string status = "Running";
        public string currentNodeId = string.Empty;
        public string startedAtUtc = string.Empty;
        public string updatedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string message = string.Empty;
        public int exitCode = -1;
        public int approvalGeneration;
        public string approvalEvidenceManifestPath = string.Empty;
        public string approvalEvidenceManifestHash = string.Empty;
        public string approvalEvidenceNodeId = string.Empty;
        public int approvalEvidenceGeneration;
        public string cancellationRequestedAtUtc = string.Empty;
        public string cancellationOutcome = string.Empty;
        public string cancellationMessage = string.Empty;
        public string iterationNodeId = string.Empty;
        public string iterationTaskNodeId = string.Empty;
        public int iterationIndex = -1;
        public JArray iterationItems = new JArray();
        // FanOut 的各目标按确定顺序执行，并且必须经过同一个 Join 才能继续。
        public string activeFanOutNodeId = string.Empty;
        public string activeJoinNodeId = string.Empty;
        public int fanOutExpectedCount;
        public int fanOutArrivedCount;
        public List<string> pendingFanOutNodeIds = new List<string>();
        public JObject inputs = new JObject();
        public JObject values = new JObject();
        public ESAISkillExecutionSpec spec;
        public List<ESAISkillStepRunRecord> steps = new List<ESAISkillStepRunRecord>();
    }

    public sealed class ESAISkillApprovalEvidenceBindingSnapshot
    {
        public ESAISkillApprovalEvidenceBindingSnapshot(ESAISkillDataBinding binding)
        {
            EdgeId = binding?.edgeId ?? string.Empty;
            Order = binding?.order ?? 0;
            SourceNodeId = binding?.sourceNodeId ?? string.Empty;
            SourcePortId = binding?.sourcePortId ?? string.Empty;
            SourcePortKey = binding?.sourcePortKey ?? string.Empty;
            SourceMeaning = binding?.sourceMeaning ?? string.Empty;
            TargetNodeId = binding?.targetNodeId ?? string.Empty;
            TargetPortId = binding?.targetPortId ?? string.Empty;
            TargetPortKey = binding?.targetPortKey ?? string.Empty;
            TargetMeaning = binding?.targetMeaning ?? string.Empty;
            Aggregation = binding?.targetAggregation ?? ESGraphPortAggregation.Auto;
        }

        public string EdgeId { get; }
        public int Order { get; }
        public string SourceNodeId { get; }
        public string SourcePortId { get; }
        public string SourcePortKey { get; }
        public string SourceMeaning { get; }
        public string TargetNodeId { get; }
        public string TargetPortId { get; }
        public string TargetPortKey { get; }
        public string TargetMeaning { get; }
        public ESGraphPortAggregation Aggregation { get; }
    }

    public sealed class ESAISkillApprovalEvidenceItemSnapshot
    {
        public ESAISkillApprovalEvidenceItemSnapshot(string sourceRunId, string sourceNodeId,
            int iterationIndex, int attemptCount, string invocationId, string childRunId,
            string finishedAtUtc, string artifactPath, string outputHash,
            string sourceArtifactPath = "")
        {
            SourceRunId = sourceRunId ?? string.Empty;
            SourceNodeId = sourceNodeId ?? string.Empty;
            IterationIndex = iterationIndex;
            AttemptCount = attemptCount;
            InvocationId = invocationId ?? string.Empty;
            ChildRunId = childRunId ?? string.Empty;
            FinishedAtUtc = finishedAtUtc ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            OutputHash = outputHash ?? string.Empty;
            SourceArtifactPath = sourceArtifactPath ?? string.Empty;
        }

        public string SourceRunId { get; }
        public string SourceNodeId { get; }
        public int IterationIndex { get; }
        public int AttemptCount { get; }
        public string InvocationId { get; }
        public string ChildRunId { get; }
        public string FinishedAtUtc { get; }
        public string ArtifactPath { get; }
        public string OutputHash { get; }
        public string SourceArtifactPath { get; }
    }

    public sealed class ESAISkillApprovalEvidenceSnapshot
    {
        public ESAISkillApprovalEvidenceSnapshot(string submissionRunId, int submissionGeneration,
            string evidenceRunId, string approvalNodeId, int evidenceGeneration,
            IEnumerable<ESAISkillApprovalEvidenceBindingSnapshot> bindings,
            IEnumerable<ESAISkillApprovalEvidenceItemSnapshot> items,
            string manifestPath = "", string manifestHash = "",
            ESAISkillApprovalEvidenceMode resolutionMode = ESAISkillApprovalEvidenceMode.BoundDataOnly,
            bool canApprove = true, string evidenceError = "")
        {
            SubmissionRunId = submissionRunId ?? string.Empty;
            SubmissionGeneration = submissionGeneration;
            EvidenceRunId = evidenceRunId ?? string.Empty;
            ApprovalNodeId = approvalNodeId ?? string.Empty;
            EvidenceGeneration = evidenceGeneration;
            Bindings = Array.AsReadOnly((bindings
                    ?? Array.Empty<ESAISkillApprovalEvidenceBindingSnapshot>())
                .Where(item => item != null).ToArray());
            Items = Array.AsReadOnly((items
                    ?? Array.Empty<ESAISkillApprovalEvidenceItemSnapshot>())
                .Where(item => item != null).ToArray());
            ManifestPath = manifestPath ?? string.Empty;
            ManifestHash = manifestHash ?? string.Empty;
            ResolutionMode = resolutionMode;
            CanApprove = canApprove;
            EvidenceError = evidenceError ?? string.Empty;
        }

        public string SubmissionRunId { get; }
        public int SubmissionGeneration { get; }
        public string EvidenceRunId { get; }
        public string ApprovalNodeId { get; }
        public int EvidenceGeneration { get; }
        public IReadOnlyList<ESAISkillApprovalEvidenceBindingSnapshot> Bindings { get; }
        public IReadOnlyList<ESAISkillApprovalEvidenceItemSnapshot> Items { get; }
        public string ManifestPath { get; }
        public string ManifestHash { get; }
        public ESAISkillApprovalEvidenceMode ResolutionMode { get; }
        public bool CanApprove { get; }
        public string EvidenceError { get; }
    }

    public static class ESAISkillExecutionCoordinator
    {
        internal const int MaximumSkillCallDepth = 8;
        private const string ActiveRunsSessionKey = "ES.AISkillGraph.ActiveRuns.v1";
        private static readonly Dictionary<string, ESAISkillWorkflowRun> Active =
            new Dictionary<string, ESAISkillWorkflowRun>(StringComparer.Ordinal);
#if UNITY_INCLUDE_TESTS
        internal static Action<string, string> Internal_ApprovalEvidenceBeforeCopyTestHook;

        internal static void Internal_RegisterApprovalTestRun(ESAISkillWorkflowRun run)
        {
            if (run == null || Active.ContainsKey(run.runId))
                throw new InvalidOperationException("测试 Run 为空或 RunId 已注册。");
            Active.Add(run.runId, run);
        }

        internal static void Internal_RemoveApprovalTestRun(ESAISkillWorkflowRun run)
        {
            if (run != null && Active.TryGetValue(run.runId, out ESAISkillWorkflowRun current)
                && ReferenceEquals(current, run))
                Active.Remove(run.runId);
        }
#endif

        internal static void InitializeForEditor()
        {
            RestoreActiveRuns();
        }

        public static string RunsRoot => Path.Combine(ES.ESAutomationPathPolicy.RunsRoot, "AISkillGraph");

        public static bool TryStart(ESAISkillExecutionSpec spec, JObject inputs, string operatorId,
            out ESAISkillWorkflowRun run, out string error)
            => TryStart(spec, inputs, operatorId, spec?.sourceAssetGuid, out run, out error);

        public static bool TryStart(ESAISkillExecutionSpec spec, JObject inputs, string operatorId,
            string sourceAssetGuid, out ESAISkillWorkflowRun run, out string error)
            => TryStartCore(spec, inputs, operatorId, sourceAssetGuid, string.Empty, 0,
                new[] { spec?.sourceGraphId }, string.Empty, out run, out error);

        private static bool TryStartCore(ESAISkillExecutionSpec spec, JObject inputs, string operatorId,
            string sourceAssetGuid, string parentRunId, int callDepth, string[] ancestorGraphIds,
            string stableRunId, out ESAISkillWorkflowRun run, out string error)
        {
            run = null;
            if (!TryValidateInputs(spec, inputs, out JObject normalized, out error))
                return false;
            string assetPath = string.IsNullOrWhiteSpace(sourceAssetGuid)
                ? string.Empty : AssetDatabase.GUIDToAssetPath(sourceAssetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "AISkill 执行必须绑定已保存源 Graph 的精确 Asset GUID。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(stableRunId))
            {
                if (!Guid.TryParseExact(stableRunId, "N", out _))
                {
                    error = "稳定子 AISkill RunId 必须是 N 格式 GUID。";
                    return false;
                }
                if (File.Exists(RunPath(stableRunId)))
                {
                    if (!TryLoad(stableRunId, out ESAISkillWorkflowRun existing,
                            out string loadError))
                    {
                        error = "稳定子 AISkill RunRecord 无效：" + loadError;
                        return false;
                    }
                    if (!string.Equals(existing.executionSpecHash,
                            ComputeExecutionSpecHash(spec), StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(existing.sourceAssetGuid, sourceAssetGuid,
                            StringComparison.Ordinal)
                        || !string.Equals(existing.parentRunId, parentRunId,
                            StringComparison.Ordinal)
                        || existing.callDepth != callDepth
                        || !JToken.DeepEquals(existing.inputs, normalized))
                    {
                        error = "稳定子 AISkill RunId 已绑定其他执行合同或输入。";
                        return false;
                    }
                    run = existing;
                    if (!IsTerminal(existing.status) && !Active.ContainsKey(existing.runId))
                    {
                        if (!TryValidateCurrentSource(existing, out string sourceError))
                        {
                            error = "稳定子 AISkill Run 当前不可恢复：" + sourceError;
                            return false;
                        }
                        Active.Add(existing.runId, existing);
                        TrySaveActiveRunIds();
                        EnsureUpdateHook();
                    }
                    error = string.Empty;
                    return true;
                }
                if (Directory.Exists(RunDirectory(stableRunId)))
                {
                    error = "稳定子 AISkill RunId 目录存在但缺少有效 RunRecord。";
                    return false;
                }
            }
            string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            run = new ESAISkillWorkflowRun
            {
                runId = string.IsNullOrWhiteSpace(stableRunId)
                    ? Guid.NewGuid().ToString("N") : stableRunId,
                graphId = spec.sourceGraphId,
                sourceAssetGuid = sourceAssetGuid ?? string.Empty,
                contentSignature = spec.sourceContentSignature,
                executionSpecHash = ComputeExecutionSpecHash(spec),
                skillId = spec.skillId,
                operatorId = string.IsNullOrWhiteSpace(operatorId) ? Environment.UserName : operatorId.Trim(),
                parentRunId = parentRunId ?? string.Empty,
                callDepth = callDepth,
                ancestorGraphIds = ancestorGraphIds ?? Array.Empty<string>(),
                currentNodeId = spec.entryNodeId,
                startedAtUtc = now,
                updatedAtUtc = now,
                inputs = normalized,
                spec = spec,
                steps = spec.steps.Select(step => new ESAISkillStepRunRecord { nodeId = step.nodeId }).ToList()
            };
            try
            {
                Save(run);
                Active.Add(run.runId, run);
                TrySaveActiveRunIds();
                EnsureUpdateHook();
                Tick(run);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Active.Remove(run.runId);
                TrySaveActiveRunIds();
                try
                {
                    run.status = "Failed";
                    run.message = "AISkill 启动失败：" + exception.Message;
                    run.exitCode = -1;
                    run.finishedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                    Save(run);
                }
                catch (Exception persistException)
                {
                    Debug.LogError("[ES AISkillGraph] 启动失败记录无法持久化：" + persistException.Message);
                }
                error = "无法启动 AISkill 工作流：" + exception.Message;
                return false;
            }
        }

        public static bool TryGet(string runId, out ESAISkillWorkflowRun run)
        {
            if (Active.TryGetValue(runId ?? string.Empty, out run))
                return true;
            return TryLoad(runId, out run, out _);
        }

        internal static bool TryCreateApprovalEvidenceSnapshot(ESAISkillWorkflowRun submissionRun,
            out ESAISkillApprovalEvidenceSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (submissionRun == null || submissionRun.status != "WaitingApproval"
                || !ESGraphIdentity.IsValid(submissionRun.runId)
                || submissionRun.approvalGeneration <= 0)
            {
                error = "Run 不存在或当前未等待人工确认。";
                return false;
            }

            ESAISkillWorkflowRun evidenceRun = submissionRun;
            var projectedRuns = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (!projectedRuns.Add(evidenceRun.runId))
                {
                    error = "子 AISkill 审批投影形成循环，拒绝生成证据快照。";
                    return false;
                }
                ESAISkillExecutionStep current = FindStep(evidenceRun, evidenceRun.currentNodeId);
                if (current?.skillCall == null)
                    break;
                ESAISkillStepRunRecord callRecord = FindStepRecord(evidenceRun, current.nodeId);
                if (!TryGet(callRecord?.childRunId, out ESAISkillWorkflowRun child)
                    || !IsProjectedChildApprovalCurrent(callRecord, child))
                {
                    error = "子 AISkill 的审批投影已过期，请刷新后重试。";
                    return false;
                }
                evidenceRun = child;
            }

            ESAISkillExecutionStep approval = FindStep(evidenceRun, evidenceRun.currentNodeId);
            if (approval?.approval == null || !ESGraphIdentity.IsValid(approval.nodeId))
            {
                error = "当前审批上下文没有精确对应的 Approval 节点。";
                return false;
            }
            ESAISkillApprovalEvidenceMode evidenceMode = approval.approval.evidenceMode;
            if (evidenceMode != ESAISkillApprovalEvidenceMode.BoundDataOnly
                && evidenceMode != ESAISkillApprovalEvidenceMode.ControlFlowFallback)
            {
                error = "Approval 的证据来源模式无效。";
                return false;
            }

            ESAISkillDataBinding[] directBindings = (evidenceRun.spec?.dataBindings
                    ?? Array.Empty<ESAISkillDataBinding>())
                .Where(binding => binding != null
                    && string.Equals(binding.targetNodeId, approval.nodeId, StringComparison.Ordinal))
                .OrderBy(binding => binding.order)
                .ThenBy(binding => binding.edgeId, StringComparer.Ordinal)
                .ToArray();
            var bindings = directBindings
                .Select(binding => new ESAISkillApprovalEvidenceBindingSnapshot(binding)).ToArray();
            var items = new List<ESAISkillApprovalEvidenceItemSnapshot>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < directBindings.Length; i++)
                CollectApprovalEvidence(evidenceRun, directBindings[i].sourceNodeId, visited, items,
                    allowControlFlowTraversal: false);

            if (directBindings.Length == 0
                && evidenceMode == ESAISkillApprovalEvidenceMode.ControlFlowFallback)
            {
                ESAISkillControlEdge[] controlSources = (evidenceRun.spec?.controlEdges
                        ?? Array.Empty<ESAISkillControlEdge>())
                    .Where(edge => edge != null
                        && string.Equals(edge.targetNodeId, approval.nodeId, StringComparison.Ordinal))
                    .OrderBy(edge => edge.order)
                    .ThenBy(edge => edge.edgeId, StringComparer.Ordinal)
                    .ToArray();
                for (int i = 0; i < controlSources.Length; i++)
                    CollectApprovalEvidence(evidenceRun, controlSources[i].sourceNodeId, visited, items,
                        allowControlFlowTraversal: true);
            }

            string evidenceError = string.Empty;
            if (directBindings.Length == 0
                && evidenceMode == ESAISkillApprovalEvidenceMode.BoundDataOnly)
                evidenceError = "无绑定证据：Approval 未连接“审查数据”，且未显式启用控制流回退。";
            else if (items.Count == 0)
                evidenceError = "审批来源没有产生可固化的任务产物。";

            return TryPersistApprovalEvidenceSnapshot(submissionRun, evidenceRun, approval.nodeId,
                evidenceMode, bindings, items, evidenceError, out snapshot, out error);
        }

        private static void CollectApprovalEvidence(ESAISkillWorkflowRun run, string nodeId,
            HashSet<string> visited, List<ESAISkillApprovalEvidenceItemSnapshot> items,
            bool allowControlFlowTraversal)
        {
            if (run == null || string.IsNullOrWhiteSpace(nodeId)
                || !visited.Add(run.runId + ":" + nodeId))
                return;

            ESAISkillStepRunRecord record = FindStepRecord(run, nodeId);
            bool collected = false;
            ESAISkillIterationRunRecord[] iterations = (record?.iterations
                    ?? new List<ESAISkillIterationRunRecord>())
                .Where(item => item != null && item.artifacts != null
                    && item.artifacts.Length > 0)
                .OrderBy(item => item.index).ToArray();
            for (int i = 0; i < iterations.Length; i++)
            {
                ESAISkillIterationRunRecord iteration = iterations[i];
                AddApprovalEvidenceItems(items, run.runId, nodeId, iteration.index,
                    iteration.attemptCount, iteration.invocationId, iteration.childRunId,
                    iteration.finishedAtUtc, iteration.artifacts, iteration.outputHashes);
                collected = true;
            }

            // ForEach 的顶层字段是最后一次迭代的工作缓冲；只要存在迭代记录就绝不读取它。
            if ((record?.iterations?.Count ?? 0) == 0
                && record?.artifacts != null && record.artifacts.Length > 0)
            {
                AddApprovalEvidenceItems(items, run.runId, nodeId, -1, record.attemptCount,
                    record.invocationId, record.childRunId, record.finishedAtUtc,
                    record.artifacts, record.outputHashes);
                collected = true;
            }
            if (collected)
                return;

            ESAISkillDataBinding[] dataSources = (run.spec?.dataBindings
                    ?? Array.Empty<ESAISkillDataBinding>())
                .Where(binding => binding != null
                    && string.Equals(binding.targetNodeId, nodeId, StringComparison.Ordinal))
                .OrderBy(binding => binding.order)
                .ThenBy(binding => binding.edgeId, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < dataSources.Length; i++)
                CollectApprovalEvidence(run, dataSources[i].sourceNodeId, visited, items,
                    allowControlFlowTraversal: false);

            if (!allowControlFlowTraversal)
                return;

            ESAISkillControlEdge[] controlSources = (run.spec?.controlEdges
                    ?? Array.Empty<ESAISkillControlEdge>())
                .Where(edge => edge != null
                    && string.Equals(edge.targetNodeId, nodeId, StringComparison.Ordinal))
                .OrderBy(edge => edge.order)
                .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < controlSources.Length; i++)
            {
                ESAISkillStepRunRecord sourceRecord = FindStepRecord(run, controlSources[i].sourceNodeId);
                if (sourceRecord != null && !string.Equals(sourceRecord.status, "Pending",
                        StringComparison.Ordinal))
                    CollectApprovalEvidence(run, controlSources[i].sourceNodeId, visited, items,
                        allowControlFlowTraversal: true);
            }
        }

        private static void AddApprovalEvidenceItems(
            List<ESAISkillApprovalEvidenceItemSnapshot> items, string runId, string nodeId,
            int iterationIndex, int attemptCount, string invocationId, string childRunId,
            string finishedAtUtc, IReadOnlyList<string> artifacts, IReadOnlyList<string> hashes)
        {
            for (int i = 0; i < (artifacts?.Count ?? 0); i++)
            {
                if (string.IsNullOrWhiteSpace(artifacts[i])) continue;
                items.Add(new ESAISkillApprovalEvidenceItemSnapshot(runId, nodeId,
                    iterationIndex, attemptCount, invocationId, childRunId, finishedAtUtc,
                    artifacts[i], hashes != null && i < hashes.Count ? hashes[i] : string.Empty));
            }
        }

        private static bool TryPersistApprovalEvidenceSnapshot(
            ESAISkillWorkflowRun submissionRun,
            ESAISkillWorkflowRun evidenceRun,
            string approvalNodeId,
            ESAISkillApprovalEvidenceMode evidenceMode,
            IReadOnlyList<ESAISkillApprovalEvidenceBindingSnapshot> bindings,
            IReadOnlyList<ESAISkillApprovalEvidenceItemSnapshot> sourceItems,
            string initialEvidenceError,
            out ESAISkillApprovalEvidenceSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            string runRoot = RunDirectory(submissionRun.runId);
            string approvalRoot = Path.Combine(runRoot, "approvals",
                submissionRun.approvalGeneration.ToString(CultureInfo.InvariantCulture)
                + "-" + approvalNodeId);
            string filesRoot = Path.Combine(approvalRoot, "files");
            var capturedItems = new List<ESAISkillApprovalEvidenceItemSnapshot>();
            string evidenceError = initialEvidenceError ?? string.Empty;
            if (string.IsNullOrEmpty(evidenceError))
            {
                for (int i = 0; i < (sourceItems?.Count ?? 0); i++)
                {
                    if (!TryCaptureApprovalEvidenceFile(sourceItems[i], filesRoot, runRoot,
                            out ESAISkillApprovalEvidenceItemSnapshot captured, out evidenceError))
                        break;
                    capturedItems.Add(captured);
                }
            }

            bool canApprove = string.IsNullOrEmpty(evidenceError) && capturedItems.Count > 0;
            JObject manifest = BuildApprovalEvidenceManifest(submissionRun, evidenceRun,
                approvalNodeId, evidenceMode, bindings, capturedItems, canApprove, evidenceError);
            string manifestJson = manifest.ToString(Formatting.None);
            string manifestHash = ComputeUtf8Hash(manifestJson);
            string manifestPath = Path.Combine(approvalRoot, "manifests", manifestHash + ".json");
            try
            {
                if (File.Exists(manifestPath))
                {
                    if (!string.Equals(ComputeFileSha256(manifestPath), manifestHash,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("审批 manifest 的既有内容与内容寻址文件名不一致。");
                }
                else
                {
                    ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(manifestPath, manifestJson,
                        new[] { runRoot });
                }

                submissionRun.approvalEvidenceManifestPath = manifestPath;
                submissionRun.approvalEvidenceManifestHash = manifestHash;
                submissionRun.approvalEvidenceNodeId = approvalNodeId;
                submissionRun.approvalEvidenceGeneration = submissionRun.approvalGeneration;
                Save(submissionRun);
                snapshot = new ESAISkillApprovalEvidenceSnapshot(submissionRun.runId,
                    submissionRun.approvalGeneration, evidenceRun.runId, approvalNodeId,
                    evidenceRun.approvalGeneration, bindings, capturedItems, manifestPath,
                    manifestHash, evidenceMode, canApprove, evidenceError);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "无法固化审批证据 manifest：" + exception.Message;
                return false;
            }
        }

        private static bool TryCaptureApprovalEvidenceFile(
            ESAISkillApprovalEvidenceItemSnapshot sourceItem,
            string filesRoot,
            string runRoot,
            out ESAISkillApprovalEvidenceItemSnapshot captured,
            out string error)
        {
            captured = null;
            string temporaryPath = string.Empty;
            try
            {
                if (sourceItem == null
                    || !ES.ESAutomationWorkerRegistration.IsSha256(sourceItem.OutputHash))
                    throw new InvalidDataException("审批证据缺少有效的 SHA-256 OutputHash。来源节点："
                        + (sourceItem?.SourceNodeId ?? "<unknown>"));
                string sourcePath = ES.ESAutomationPathPolicy.Normalize(sourceItem.ArtifactPath);
                if (!ES.ESAutomationPathPolicy.IsWithin(sourcePath,
                        new[] { ES.ESAutomationPathPolicy.ProjectRoot }))
                    throw new UnauthorizedAccessException("审批证据必须位于项目目录内：" + sourcePath);
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("审批证据原产物不存在。", sourcePath);

                ES.ESAutomationPathPolicy.EnsureWorkerDirectory(filesRoot, new[] { runRoot });
                temporaryPath = Path.Combine(filesRoot, ".capture-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite))
                using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None))
                {
#if UNITY_INCLUDE_TESTS
                    Internal_ApprovalEvidenceBeforeCopyTestHook?.Invoke(sourcePath, temporaryPath);
#endif
                    source.CopyTo(temporary);
                    temporary.Flush(true);
                }

                string actualHash = ComputeFileSha256(temporaryPath);
                if (!string.Equals(actualHash, sourceItem.OutputHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("审批证据副本 Hash 与 RunRecord OutputHash 不一致。来源："
                        + sourcePath);

                string extension = GetSafeApprovalEvidenceExtension(sourcePath);
                string destinationPath = Path.Combine(filesRoot, actualHash + extension);
                if (File.Exists(destinationPath))
                {
                    if (!string.Equals(ComputeFileSha256(destinationPath), actualHash,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("内容寻址审批副本已存在但内容 Hash 不一致。");
                    ES.ESAutomationPathPolicy.DeleteWorkerFile(temporaryPath, new[] { runRoot });
                    temporaryPath = string.Empty;
                }
                else
                {
                    try
                    {
                        File.Move(temporaryPath, destinationPath);
                        temporaryPath = string.Empty;
                    }
                    catch (IOException) when (File.Exists(destinationPath))
                    {
                        if (!string.Equals(ComputeFileSha256(destinationPath), actualHash,
                                StringComparison.OrdinalIgnoreCase))
                            throw;
                        ES.ESAutomationPathPolicy.DeleteWorkerFile(temporaryPath, new[] { runRoot });
                        temporaryPath = string.Empty;
                    }
                }

                captured = new ESAISkillApprovalEvidenceItemSnapshot(sourceItem.SourceRunId,
                    sourceItem.SourceNodeId, sourceItem.IterationIndex, sourceItem.AttemptCount,
                    sourceItem.InvocationId, sourceItem.ChildRunId, sourceItem.FinishedAtUtc,
                    destinationPath, actualHash, sourcePath);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
                {
                    try { ES.ESAutomationPathPolicy.DeleteWorkerFile(temporaryPath, new[] { runRoot }); }
                    catch { }
                }
                error = exception.Message;
                return false;
            }
        }

        private static JObject BuildApprovalEvidenceManifest(
            ESAISkillWorkflowRun submissionRun,
            ESAISkillWorkflowRun evidenceRun,
            string approvalNodeId,
            ESAISkillApprovalEvidenceMode evidenceMode,
            IReadOnlyList<ESAISkillApprovalEvidenceBindingSnapshot> bindings,
            IReadOnlyList<ESAISkillApprovalEvidenceItemSnapshot> items,
            bool canApprove,
            string evidenceError)
        {
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["submissionRunId"] = submissionRun.runId,
                ["submissionGeneration"] = submissionRun.approvalGeneration,
                ["evidenceRunId"] = evidenceRun.runId,
                ["evidenceGeneration"] = evidenceRun.approvalGeneration,
                ["approvalNodeId"] = approvalNodeId,
                ["resolutionMode"] = evidenceMode.ToString(),
                ["canApprove"] = canApprove,
                ["evidenceError"] = evidenceError ?? string.Empty,
                ["bindings"] = new JArray((bindings
                    ?? Array.Empty<ESAISkillApprovalEvidenceBindingSnapshot>()).Select(binding =>
                    new JObject
                    {
                        ["edgeId"] = binding.EdgeId,
                        ["order"] = binding.Order,
                        ["sourceNodeId"] = binding.SourceNodeId,
                        ["sourcePortId"] = binding.SourcePortId,
                        ["targetNodeId"] = binding.TargetNodeId,
                        ["targetPortId"] = binding.TargetPortId,
                        ["aggregation"] = binding.Aggregation.ToString()
                    })),
                ["items"] = new JArray((items
                    ?? Array.Empty<ESAISkillApprovalEvidenceItemSnapshot>()).Select(item =>
                    new JObject
                    {
                        ["sourceRunId"] = item.SourceRunId,
                        ["sourceNodeId"] = item.SourceNodeId,
                        ["iterationIndex"] = item.IterationIndex,
                        ["attemptCount"] = item.AttemptCount,
                        ["invocationId"] = item.InvocationId,
                        ["childRunId"] = item.ChildRunId,
                        ["finishedAtUtc"] = item.FinishedAtUtc,
                        ["sourceArtifactPath"] = item.SourceArtifactPath,
                        ["artifactPath"] = item.ArtifactPath,
                        ["sha256"] = item.OutputHash
                    }))
            };
        }

        private static string GetSafeApprovalEvidenceExtension(string path)
        {
            string extension = Path.GetExtension(path) ?? string.Empty;
            return extension.Length <= 16 && Regex.IsMatch(extension, @"^\.[A-Za-z0-9]+$")
                ? extension.ToLowerInvariant() : string.Empty;
        }

        private static string ComputeFileSha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static bool TryValidateApprovalEvidenceManifest(ESAISkillWorkflowRun run,
            int generation, out string error)
        {
            if (run == null || generation != run.approvalEvidenceGeneration
                || string.IsNullOrWhiteSpace(run.approvalEvidenceNodeId)
                || !ES.ESAutomationWorkerRegistration.IsSha256(run.approvalEvidenceManifestHash)
                || string.IsNullOrWhiteSpace(run.approvalEvidenceManifestPath))
            {
                error = "当前审批没有绑定完整的证据 manifest。";
                return false;
            }
            try
            {
                string manifestPath = ES.ESAutomationPathPolicy.Normalize(
                    run.approvalEvidenceManifestPath);
                string approvalRoot = Path.Combine(RunDirectory(run.runId), "approvals",
                    generation.ToString(CultureInfo.InvariantCulture) + "-"
                    + run.approvalEvidenceNodeId);
                string manifestRoot = Path.Combine(approvalRoot, "manifests");
                if (!ES.ESAutomationPathPolicy.IsWithin(manifestPath, new[] { manifestRoot })
                    || !File.Exists(manifestPath))
                    throw new InvalidDataException("审批 manifest 不存在或越出当前审批目录。");
                string manifestHash = ComputeFileSha256(manifestPath);
                if (!string.Equals(manifestHash, run.approvalEvidenceManifestHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetFileNameWithoutExtension(manifestPath), manifestHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("审批 manifest 内容 Hash 或内容寻址文件名不匹配。");

                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath,
                    new UTF8Encoding(false, true)));
                if (manifest.Value<int?>("schemaVersion") != 1
                    || !string.Equals(manifest.Value<string>("submissionRunId"), run.runId,
                        StringComparison.Ordinal)
                    || manifest.Value<int?>("submissionGeneration") != generation
                    || !string.Equals(manifest.Value<string>("approvalNodeId"),
                        run.approvalEvidenceNodeId, StringComparison.Ordinal)
                    || manifest.Value<bool?>("canApprove") != true)
                    throw new InvalidDataException(manifest.Value<string>("evidenceError")
                        ?? "审批 manifest 与当前 Run、代际或 Approval 节点不匹配。");

                JArray items = manifest["items"] as JArray;
                if (items == null || items.Count == 0)
                    throw new InvalidDataException("审批 manifest 没有可验证的证据副本。");
                string filesRoot = Path.Combine(approvalRoot, "files");
                foreach (JToken token in items)
                {
                    string artifactPath = token?.Value<string>("artifactPath") ?? string.Empty;
                    string expectedHash = token?.Value<string>("sha256") ?? string.Empty;
                    if (!ES.ESAutomationWorkerRegistration.IsSha256(expectedHash))
                        throw new InvalidDataException("审批 manifest 含有非法副本 Hash。");
                    string normalized = ES.ESAutomationPathPolicy.Normalize(artifactPath);
                    if (!ES.ESAutomationPathPolicy.IsWithin(normalized, new[] { filesRoot })
                        || !File.Exists(normalized)
                        || !string.Equals(ComputeFileSha256(normalized), expectedHash,
                            StringComparison.OrdinalIgnoreCase)
                        || !Path.GetFileName(normalized).StartsWith(expectedHash,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("审批证据副本不存在、越界或内容 Hash 已变化："
                            + artifactPath);
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryValidateApprovalEvidence(string runId, int generation,
            out string error)
        {
            if (!TryGet(runId, out ESAISkillWorkflowRun run) || run.status != "WaitingApproval"
                || run.approvalGeneration != generation)
            {
                error = "Run 不存在、已离开审批状态或审批代际已过期。";
                return false;
            }
            return TryValidateApprovalEvidenceManifest(run, generation, out error);
        }

        internal static bool TryValidateApprovalEvidence(ESAISkillWorkflowRun run, int generation,
            out string error)
            => TryValidateApprovalEvidenceManifest(run, generation, out error);

        public static bool TryApprove(string runId, int generation, bool approved, string comment,
            out string error)
            => TryApproveCore(runId, generation, approved, comment,
                requireEvidenceValidation: true, out error);

        private static bool TryApproveCore(string runId, int generation, bool approved, string comment,
            bool requireEvidenceValidation, out string error)
        {
            if (!TryGet(runId, out ESAISkillWorkflowRun run) || run.status != "WaitingApproval")
            {
                error = "Run 不存在或当前未等待人工确认。";
                return false;
            }
            if (generation != run.approvalGeneration)
            {
                error = "人工确认代际已过期，请刷新后重试。";
                return false;
            }
            if (approved && requireEvidenceValidation
                && !TryValidateApprovalEvidenceManifest(run, generation, out string evidenceError))
            {
                error = "审批证据完整性校验失败：" + evidenceError;
                return false;
            }
            if (approved && !TryValidateCurrentSource(run, out string sourceError))
            {
                error = "当前 Graph 或执行合同已变化，拒绝批准旧 Run：" + sourceError;
                return false;
            }
            ESAISkillExecutionStep step = FindStep(run, run.currentNodeId);
            if (step?.skillCall != null)
            {
                ESAISkillStepRunRecord callRecord = FindStepRecord(run, step.nodeId);
                if (!TryGet(callRecord?.childRunId, out ESAISkillWorkflowRun child)
                    || child.status != "WaitingApproval")
                {
                    error = "子 AISkill 不存在或当前未等待人工确认。";
                    return false;
                }
                if (!IsProjectedChildApprovalCurrent(callRecord, child))
                {
                    error = "子 AISkill 的人工确认代际已变化，请刷新后重试。";
                    return false;
                }
                if (!TryApproveCore(child.runId, callRecord.childApprovalGeneration,
                        approved, comment, requireEvidenceValidation: false, out error))
                    return false;
                run.status = "Running";
                run.message = "子 AISkill 的人工确认已提交。";
                Save(run);
                Tick(run);
                error = string.Empty;
                return true;
            }
            if (!approved && step?.approval?.requireCommentOnReject == true
                && string.IsNullOrWhiteSpace(comment))
            {
                error = "拒绝时必须填写原因。";
                return false;
            }
            CompleteStep(run, run.currentNodeId, approved ? "Approved" : "Rejected", comment);
            run.status = "Running";
            run.currentNodeId = ResolveRoute(run, run.currentNodeId, approved
                ? ESAgentGraphStableIds.SkillApprovedPortKey
                : ESAgentGraphStableIds.SkillRejectedPortKey);
            if (string.IsNullOrEmpty(run.currentNodeId))
                Fail(run, "人工确认出口没有连接目标。");
            else
                Tick(run);
            error = string.Empty;
            return true;
        }

        public static bool TryCancel(string runId, string actorId, out string error)
        {
            if (!TryGet(runId, out ESAISkillWorkflowRun run) || IsTerminal(run.status))
            {
                error = "Run 不存在或已经结束。";
                return false;
            }
            ESAISkillStepRunRecord step = FindStepRecord(run, run.currentNodeId);
            if (!string.IsNullOrWhiteSpace(step?.childRunId))
            {
                ESAISkillExecutionStep current = FindStep(run, run.currentNodeId);
                if (current?.skillCall != null)
                {
                    if (!TryCancel(step.childRunId, actorId, out string childError))
                    {
                        MarkCancellationFailed(run, childError);
                        error = childError;
                        return false;
                    }
                    if (TryGet(step.childRunId, out ESAISkillWorkflowRun child)
                        && !IsTerminal(child.status))
                    {
                        run.status = "Cancelling";
                        run.cancellationRequestedAtUtc = DateTimeOffset.UtcNow.ToString("O",
                            CultureInfo.InvariantCulture);
                        run.cancellationOutcome = "Pending";
                        run.cancellationMessage = "等待子 AISkill 确认取消。";
                        run.message = run.cancellationMessage;
                        Save(run);
                        error = string.Empty;
                        return true;
                    }
                }
                else
                {
                    ES.ESAutomationTaskInvocationResult result = ES.ESAutomationFacade.CancelRun(
                        step.childRunId, actorId ?? string.Empty, false);
                    if (result.status == "Accepted" || IsTaskInProgress(result.status))
                    {
                        run.status = "Cancelling";
                        run.cancellationRequestedAtUtc = DateTimeOffset.UtcNow.ToString("O",
                            CultureInfo.InvariantCulture);
                        run.cancellationOutcome = "Pending";
                        run.cancellationMessage = result.message ?? string.Empty;
                        run.message = "已请求取消子 Automation，等待终态确认。";
                        Save(run);
                        error = string.Empty;
                        return true;
                    }
                    if (result.status != "Cancelled" && result.status != "Completed")
                    {
                        MarkCancellationFailed(run, result.message ?? "子 Automation 拒绝取消。");
                        error = run.cancellationMessage;
                        return false;
                    }
                }
            }
            Finish(run, "Cancelled", "用户取消工作流。", -1);
            error = string.Empty;
            return true;
        }

        private static void Update()
        {
            foreach (ESAISkillWorkflowRun run in Active.Values.ToArray())
            {
                try { Tick(run); }
                catch (Exception exception)
                {
                    try { Fail(run, "协调器异常：" + exception.Message); }
                    catch (Exception persistException)
                    {
                        Active.Remove(run.runId);
                        TrySaveActiveRunIds();
                        Debug.LogError("[ES AISkillGraph] Run 失败状态无法持久化：" + persistException.Message);
                    }
                }
            }
            if (Active.Count == 0)
                EditorApplication.update -= Update;
        }

        private static void Tick(ESAISkillWorkflowRun run)
        {
            if (run == null || IsTerminal(run.status) || run.status == "WaitingApproval")
                return;
            if (run.status == "Cancelling")
            {
                PollCancellation(run);
                return;
            }
            for (int guard = 0; guard < 128 && run.status == "Running"; guard++)
            {
                ESAISkillExecutionStep step = FindStep(run, run.currentNodeId);
                if (step == null) { Fail(run, "当前节点不在已烘焙执行合同中。"); return; }
                if (step.input != null)
                {
                    SetEndpointValue(run, step.nodeId,
                        ESAgentGraphStableIds.SkillParametersPortKey, run.inputs);
                    CompleteStep(run, step.nodeId, "Completed", "参数已绑定。", run.inputs);
                    Move(run, step.nodeId, ESAgentGraphStableIds.SkillNextPortKey);
                    continue;
                }
                if (step.task != null)
                {
                    if (PollOrStartTask(run, step)) return;
                    continue;
                }
                if (step.skillCall != null)
                {
                    if (PollOrStartSkillCall(run, step)) return;
                    continue;
                }
                if (step.branch != null)
                {
                    JToken value = ResolveBoundValue(run, step.nodeId,
                        ESAgentGraphStableIds.SkillInputPortKey);
                    if (!TrySelectBranchTarget(run.spec, step, value, out string targetNodeId,
                            out bool matched, out JToken selectedValue, out string branchError))
                    {
                        Fail(run, branchError);
                        return;
                    }
                    CompleteStep(run, step.nodeId, "Completed", matched ? "条件命中。" : "进入默认分支。",
                        selectedValue);
                    run.currentNodeId = targetNodeId;
                    Save(run);
                    continue;
                }
                if (step.forEach != null)
                {
                    if (!EnterOrContinueForEach(run, step)) return;
                    continue;
                }
                if (step.approval != null)
                {
                    BeginStep(run, step.nodeId);
                    run.status = "WaitingApproval";
                    run.approvalGeneration++;
                    ClearApprovalEvidenceBinding(run);
                    run.message = step.approval.title + "：" + step.approval.message;
                    Save(run);
                    return;
                }
                if (step.output != null)
                {
                    if (!string.IsNullOrWhiteSpace(run.activeFanOutNodeId))
                    {
                        Fail(run, "FanOut 分支必须先经过 Join，不能直接结束工作流。");
                        return;
                    }
                    JToken output = ResolveBoundValue(run, step.nodeId,
                        ESAgentGraphStableIds.SkillInputPortKey)
                        ?? JValue.CreateNull();
                    run.values[step.output.outputId] = output.DeepClone();
                    CompleteStep(run, step.nodeId, "Completed", "结构化输出已归集。", output);
                    Finish(run, "Completed", "AISkill 工作流执行完成。", 0);
                    return;
                }
                if (step.fanOut != null)
                {
                    if (!StartFanOut(run, step)) return;
                    continue;
                }
                if (step.join != null)
                {
                    if (!EnterJoin(run, step)) return;
                    continue;
                }
            }
            if (run.status == "Running")
                Fail(run, "单次调度超过 128 个同步步骤，已阻断疑似异常流程。");
        }

        private static bool PollOrStartTask(ESAISkillWorkflowRun run, ESAISkillExecutionStep step)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, step.nodeId);
            if (string.IsNullOrWhiteSpace(record.childRunId))
            {
                if (DateTimeOffset.TryParse(record.retryAvailableAtUtc,
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out DateTimeOffset retryAt) && DateTimeOffset.UtcNow < retryAt)
                    return true;
                BeginStep(run, step.nodeId);
                if (string.IsNullOrWhiteSpace(record.invocationId))
                {
                    record.invocationId = Guid.NewGuid().ToString("N");
                    SyncCurrentIterationIdentity(run, record);
                    Save(run);
                }
                JObject input = JObject.Parse(string.IsNullOrWhiteSpace(step.task.staticInputJson)
                    ? "{}" : step.task.staticInputJson);
                if (!TryApplyInputBindings(run, step.nodeId, step.task.inputBindings,
                        input, out string bindingError))
                    return HandleTaskFailure(run, step, "Failed", bindingError);
                ES.ESAutomationTaskInvocationResult result = ES.ESAIBrainCoordinator.Run(
                    new ES.ESAIBrainRequest
                    {
                        objective = "执行 AISkill Graph 步骤：" + run.skillId + "/" + step.nodeId,
                        routeKeys = new List<string> { "aibrain", "orchestration" },
                        commandId = "aiskill.graph.execute",
                        skillNames = new List<string>(),
                        workflow = new ES.ESAIBrainWorkflowAuthority
                        {
                            workflowId = run.skillId,
                            contentHash = run.executionSpecHash,
                            sourceAssetGuid = run.sourceAssetGuid,
                        },
                        invocationId = record.invocationId,
                        taskId = step.task.taskId,
                        taskVersion = step.task.taskVersion,
                        preset = step.task.preset,
                        input = input,
                        dryRun = step.task.dryRun,
                        actorId = run.operatorId,
                        fromAi = false
                    });
                record.childRunId = result.runId ?? string.Empty;
                record.message = result.message ?? string.Empty;
                SyncCurrentIterationIdentity(run, record);
                Save(run);
                if (IsSuccessfulTaskStatus(result.status))
                    return HandleTaskTerminal(run, step, result);
                if (IsTaskInProgress(result.status))
                {
                    if (!Guid.TryParseExact(record.childRunId, "N", out _))
                        return HandleTaskFailure(run, step, "Failed",
                            "Automation 返回运行中状态但未提供有效 RunId。" );
                    return true;
                }
                return HandleTaskFailure(run, step, result.status, result.message, result.data);
            }

            ES.ESAutomationTaskInvocationResult current = ES.ESAutomationFacade.GetRun(record.childRunId, false);
            if (IsTaskInProgress(current.status))
            {
                if (DateTimeOffset.TryParse(record.currentAttemptStartedAtUtc, out DateTimeOffset started)
                    && DateTimeOffset.UtcNow - started > TimeSpan.FromSeconds(step.task.timeoutSeconds))
                {
                    ES.ESAutomationTaskInvocationResult cancellation = ES.ESAutomationFacade.CancelRun(
                        record.childRunId, run.operatorId, false);
                    if (cancellation.status == "Accepted" || IsTaskInProgress(cancellation.status))
                    {
                        run.status = "Cancelling";
                        run.cancellationRequestedAtUtc = DateTimeOffset.UtcNow.ToString("O",
                            CultureInfo.InvariantCulture);
                        run.cancellationOutcome = "PendingAfterTimeout";
                        run.cancellationMessage = cancellation.message ?? string.Empty;
                        Save(run);
                        return true;
                    }
                    if (cancellation.status != "Cancelled" && cancellation.status != "Completed")
                    {
                        MarkCancellationFailed(run, "步骤超时，但子 Automation 取消失败："
                            + (cancellation.message ?? cancellation.status));
                        return true;
                    }
                    if (cancellation.status == "Completed")
                    {
                        ES.ESAutomationTaskInvocationResult completed = ES.ESAutomationFacade.GetRun(
                            record.childRunId, false);
                        if (IsSuccessfulTaskStatus(completed.status))
                            return HandleTaskTerminal(run, step, completed);
                    }
                    return HandleTaskFailure(run, step, "TimedOut", "工作流步骤超过声明超时。" );
                }
                return true;
            }
            if (IsSuccessfulTaskStatus(current.status))
                return HandleTaskTerminal(run, step, current);
            return HandleTaskFailure(run, step, current.status, current.message, current.data);
        }

        private static bool PollOrStartSkillCall(ESAISkillWorkflowRun run,
            ESAISkillExecutionStep step)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, step.nodeId);
            if (string.IsNullOrWhiteSpace(record.childRunId))
            {
                if (!CanEnterSkillCall(run.callDepth))
                    return HandleSkillCallFailure(run, step, "Blocked",
                        "AISkill 调用深度超过上限 " + MaximumSkillCallDepth + "。" );
                string assetPath = AssetDatabase.GUIDToAssetPath(step.skillCall.sourceAssetGuid);
                ESGraphAssetBase asset = string.IsNullOrWhiteSpace(assetPath) ? null
                    : AssetDatabase.LoadAssetAtPath<ESGraphAssetBase>(assetPath);
                if (asset == null)
                    return HandleSkillCallFailure(run, step, "NotFound", "子 AISkill 资产不存在。" );
                if (!ESAISkillExecutionLauncher.TryBake(asset, out ESAISkillExecutionSpec childSpec,
                        out string bakeError))
                    return HandleSkillCallFailure(run, step, "Blocked", "子 AISkill 无法 Bake：" + bakeError);
                if (!string.Equals(childSpec.sourceGraphId, step.skillCall.targetGraphId,
                        StringComparison.Ordinal)
                    || !string.Equals(childSpec.sourceContentSignature,
                        step.skillCall.targetContentSignature, StringComparison.Ordinal))
                    return HandleSkillCallFailure(run, step, "Blocked",
                        "子 AISkill 的 GraphId 或内容签名已漂移。" );
                if (ContainsAncestorGraph(run.ancestorGraphIds, childSpec.sourceGraphId))
                    return HandleSkillCallFailure(run, step, "Blocked", "检测到 AISkill 递归调用。" );

                JObject childInput = JObject.Parse(string.IsNullOrWhiteSpace(step.skillCall.staticInputJson)
                    ? "{}" : step.skillCall.staticInputJson);
                if (!TryApplyInputBindings(run, step.nodeId, step.skillCall.inputBindings,
                        childInput, out string bindingError))
                    return HandleSkillCallFailure(run, step, "Failed", bindingError);
                if (string.IsNullOrWhiteSpace(record.invocationId))
                {
                    record.invocationId = Guid.NewGuid().ToString("N");
                    SyncCurrentIterationIdentity(run, record);
                    Save(run);
                }
                BeginStep(run, step.nodeId);
                string[] ancestors = (run.ancestorGraphIds ?? Array.Empty<string>())
                    .Concat(new[] { childSpec.sourceGraphId }).ToArray();
                if (!TryStartCore(childSpec, childInput, run.operatorId,
                        step.skillCall.sourceAssetGuid, run.runId, run.callDepth + 1, ancestors,
                        record.invocationId, out ESAISkillWorkflowRun child, out string startError))
                    return HandleSkillCallFailure(run, step, "Failed", startError);
                record.childRunId = child.runId;
                SyncCurrentIterationIdentity(run, record);
                Save(run);
            }

            if (!TryGet(record.childRunId, out ESAISkillWorkflowRun current))
                return HandleSkillCallFailure(run, step, "NotFound", "子 AISkill RunRecord 不存在。" );
            if (current.status == "WaitingApproval")
            {
                ProjectChildApproval(run, record, current);
                Save(run);
                return true;
            }
            if (current.status == "Running")
            {
                if (DateTimeOffset.TryParse(record.currentAttemptStartedAtUtc,
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out DateTimeOffset started)
                    && DateTimeOffset.UtcNow - started
                        > TimeSpan.FromSeconds(step.skillCall.timeoutSeconds))
                {
                    if (!TryCancel(current.runId, run.operatorId, out string cancelError))
                    {
                        if (TryGet(current.runId, out ESAISkillWorkflowRun racedChild)
                            && racedChild.status == "Completed")
                        {
                            JObject result = BuildChildRunResult(racedChild);
                            if (!TryPublishDataOutputs(run, step.nodeId,
                                    ESAgentGraphStableIds.SkillRunResultPortKey, result,
                                    out string publishError))
                                return HandleSkillCallFailure(run, step, "Failed", publishError);
                            CompleteStep(run, step.nodeId, "Completed",
                                "子 AISkill 在取消调用前已完成。", result);
                            Move(run, step.nodeId, ESAgentGraphStableIds.SkillSuccessPortKey);
                            return false;
                        }
                        MarkCancellationFailed(run, "子 AISkill 超时且取消失败：" + cancelError);
                        return true;
                    }
                    if (TryGet(current.runId, out ESAISkillWorkflowRun cancellingChild)
                        && !IsTerminal(cancellingChild.status))
                    {
                        run.status = "Cancelling";
                        run.cancellationRequestedAtUtc = DateTimeOffset.UtcNow.ToString("O",
                            CultureInfo.InvariantCulture);
                        run.cancellationOutcome = "PendingAfterTimeout";
                        run.cancellationMessage = "等待超时子 AISkill 确认取消。";
                        Save(run);
                        return true;
                    }
                    return HandleSkillCallFailure(run, step, "TimedOut", "子 AISkill 超过声明超时。" );
                }
                return true;
            }
            if (current.status == "Completed")
            {
                JObject result = BuildChildRunResult(current);
                if (!TryPublishDataOutputs(run, step.nodeId,
                        ESAgentGraphStableIds.SkillRunResultPortKey, result,
                        out string publishError))
                    return HandleSkillCallFailure(run, step, "Failed", publishError);
                CompleteStep(run, step.nodeId, "Completed", "子 AISkill 执行完成。", result);
                if (string.Equals(run.iterationTaskNodeId, step.nodeId, StringComparison.Ordinal))
                {
                    run.iterationIndex++;
                    run.currentNodeId = run.iterationNodeId;
                    ResetStepForNextAttempt(run, step.nodeId);
                    Save(run);
                    return false;
                }
                Move(run, step.nodeId, ESAgentGraphStableIds.SkillSuccessPortKey);
                return false;
            }
            return HandleSkillCallFailure(run, step, current.status, current.message);
        }

        private static JObject BuildChildRunResult(ESAISkillWorkflowRun child)
        {
            return new JObject
            {
                ["runId"] = child.runId,
                ["graphId"] = child.graphId,
                ["contentSignature"] = child.contentSignature,
                ["status"] = child.status,
                ["exitCode"] = child.exitCode,
                ["runRecordPath"] = RunPath(child.runId),
                ["values"] = child.values?.DeepClone() ?? new JObject(),
                ["outputs"] = new JArray((child.steps ?? new List<ESAISkillStepRunRecord>())
                    .Where(item => item != null).SelectMany(item => item.artifacts ?? Array.Empty<string>())),
                ["outputHashes"] = new JArray((child.steps ?? new List<ESAISkillStepRunRecord>())
                    .Where(item => item != null).SelectMany(item => item.outputHashes ?? Array.Empty<string>()))
            };
        }

        internal static bool TryPublishDataOutputs(ESAISkillWorkflowRun run, string nodeId,
            string singlePortKey, JToken value, out string error)
        {
            ESAISkillExecutionStep step = FindStep(run, nodeId);
            string[] portKeys = (step?.ports ?? Array.Empty<ESAISkillExecutionPort>())
                .Where(port => port != null && port.direction == ESGraphPortDirection.Output
                    && !string.Equals(port.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                        StringComparison.Ordinal))
                .Select(port => port.portKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal).ToArray();
            string[] connectedPortKeys = (run?.spec?.dataBindings
                    ?? Array.Empty<ESAISkillDataBinding>())
                .Where(binding => binding != null && binding.sourceNodeId == nodeId
                    && !string.IsNullOrWhiteSpace(binding.sourcePortKey))
                .Select(binding => binding.sourcePortKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal).ToArray();
            if (portKeys.Length == 0)
            {
                error = "节点没有声明可发布的数据输出端点。";
                return false;
            }
            if (connectedPortKeys.Any(key => !portKeys.Contains(key, StringComparer.Ordinal)))
            {
                error = "值关系引用了节点未声明的数据输出端点。";
                return false;
            }
            if (portKeys.Length == 1)
            {
                if (!string.Equals(portKeys[0], singlePortKey, StringComparison.Ordinal))
                {
                    error = "节点单一数据输出端点与执行器声明不一致：" + portKeys[0];
                    return false;
                }
                SetEndpointValue(run, nodeId, portKeys[0], value);
                error = string.Empty;
                return true;
            }

            JObject fields = value as JObject;
            JObject nested = fields?["outputs"] as JObject;
            var outputValues = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (string portKey in portKeys)
            {
                JToken field = fields?[portKey] ?? nested?[portKey];
                if (field == null)
                {
                    error = "节点有多个独立输出端点，但产物没有提供对应字段：" + portKey;
                    return false;
                }
                outputValues.Add(portKey, field);
            }
            foreach (KeyValuePair<string, JToken> output in outputValues)
                SetEndpointValue(run, nodeId, output.Key, output.Value);
            error = string.Empty;
            return true;
        }

        internal static bool CanEnterSkillCall(int currentDepth)
            => currentDepth >= 0 && currentDepth < MaximumSkillCallDepth;

        internal static bool ContainsAncestorGraph(IEnumerable<string> ancestorGraphIds,
            string targetGraphId)
            => !string.IsNullOrWhiteSpace(targetGraphId)
                && (ancestorGraphIds ?? Array.Empty<string>()).Contains(
                    targetGraphId, StringComparer.Ordinal);

        internal static void ProjectChildApproval(ESAISkillWorkflowRun parent,
            ESAISkillStepRunRecord callRecord, ESAISkillWorkflowRun child)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (callRecord == null) throw new ArgumentNullException(nameof(callRecord));
            if (child == null || child.status != "WaitingApproval"
                || child.approvalGeneration <= 0)
                throw new ArgumentException("子 Run 当前没有可投影的人工确认。", nameof(child));

            if (callRecord.childApprovalGeneration != child.approvalGeneration)
            {
                checked { parent.approvalGeneration++; }
                callRecord.childApprovalGeneration = child.approvalGeneration;
                ClearApprovalEvidenceBinding(parent);
            }
            parent.status = "WaitingApproval";
            parent.message = "子 AISkill 等待人工确认：" + child.message;
        }

        internal static bool IsProjectedChildApprovalCurrent(ESAISkillStepRunRecord callRecord,
            ESAISkillWorkflowRun child)
            => callRecord != null && child != null && child.status == "WaitingApproval"
                && callRecord.childApprovalGeneration > 0
                && callRecord.childApprovalGeneration == child.approvalGeneration;

        private static void ClearApprovalEvidenceBinding(ESAISkillWorkflowRun run)
        {
            if (run == null) return;
            run.approvalEvidenceManifestPath = string.Empty;
            run.approvalEvidenceManifestHash = string.Empty;
            run.approvalEvidenceNodeId = string.Empty;
            run.approvalEvidenceGeneration = 0;
        }

        private static bool HandleSkillCallFailure(ESAISkillWorkflowRun run,
            ESAISkillExecutionStep step, string status, string message)
        {
            status = NormalizeTaskFailureStatus(status);
            if (ShouldStopFanOutOnFailure(run))
            {
                CompleteStep(run, step.nodeId, status, message);
                Fail(run, "FanOut 分支失败，已按 stopOnFailure 终止：" + (message ?? status));
                return false;
            }
            CompleteStep(run, step.nodeId, status, message);
            if (string.Equals(run.iterationTaskNodeId, step.nodeId, StringComparison.Ordinal))
            {
                string owner = run.iterationNodeId;
                ClearIteration(run);
                Move(run, owner, ESAgentGraphStableIds.SkillFailurePortKey);
                return false;
            }
            string route = status == "TimedOut" ? ESAgentGraphStableIds.SkillTimeoutPortKey
                : status == "Cancelled" ? ESAgentGraphStableIds.SkillCancelledPortKey
                : ESAgentGraphStableIds.SkillFailurePortKey;
            Move(run, step.nodeId, route);
            return false;
        }

        private static bool TryApplyInputBindings(ESAISkillWorkflowRun run, string targetNodeId,
            IEnumerable<ESAISkillTaskInputBinding> bindings, JObject input, out string error)
        {
            foreach (ESAISkillTaskInputBinding binding in bindings
                         ?? Array.Empty<ESAISkillTaskInputBinding>())
            {
                JToken value;
                switch (binding.source)
                {
                    case ESAISkillTaskInputSource.WorkflowParameter:
                        value = run.inputs?[binding.sourceId];
                        break;
                    case ESAISkillTaskInputSource.IterationItem:
                        value = !string.IsNullOrWhiteSpace(run.iterationNodeId)
                            && run.iterationIndex >= 0 && run.iterationIndex < run.iterationItems.Count
                                ? run.iterationItems[run.iterationIndex]
                                : null;
                        break;
                    default:
                        value = ResolveBoundValue(run, targetNodeId, binding.sourceId);
                        break;
                }
                if (value != null && !string.IsNullOrWhiteSpace(binding.sourcePath))
                    value = value.SelectToken(binding.sourcePath, false);
                if ((value == null || value.Type == JTokenType.Null) && binding.required)
                {
                    error = "Task 必填输入映射无法解析：" + binding.targetField;
                    return false;
                }
                if (value != null)
                    input[binding.targetField] = value.DeepClone();
            }
            error = string.Empty;
            return true;
        }

        private static bool HandleTaskTerminal(ESAISkillWorkflowRun run, ESAISkillExecutionStep step,
            ES.ESAutomationTaskInvocationResult result)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, step.nodeId);
            JObject taskData = BuildTaskTerminalData(record, result.status,
                result.message, result.data);
            if (!TryPublishDataOutputs(run, step.nodeId,
                    ESAgentGraphStableIds.SkillRunResultPortKey, taskData,
                    out string publishError))
                return HandleSkillDataPublicationFailure(run, step, publishError);
            CompleteStep(run, step.nodeId, "Completed", result.message, taskData);
            if (string.Equals(run.iterationTaskNodeId, step.nodeId, StringComparison.Ordinal))
            {
                run.iterationIndex++;
                string ownerId = run.iterationNodeId;
                run.currentNodeId = ownerId;
                ResetStepForNextAttempt(run, step.nodeId);
                Save(run);
                return false;
            }
            Move(run, step.nodeId, ESAgentGraphStableIds.SkillSuccessPortKey);
            return false;
        }

        private static bool HandleSkillDataPublicationFailure(ESAISkillWorkflowRun run,
            ESAISkillExecutionStep step, string message)
        {
            CompleteStep(run, step.nodeId, "Failed", message);
            if (ShouldStopFanOutOnFailure(run))
            {
                Fail(run, "节点输出端点解析失败，FanOut 已终止：" + message);
                return false;
            }
            Move(run, step.nodeId, ESAgentGraphStableIds.SkillFailurePortKey);
            return false;
        }

        private static bool HandleTaskFailure(ESAISkillWorkflowRun run, ESAISkillExecutionStep step,
            string status, string message, JObject taskData = null)
        {
            status = NormalizeTaskFailureStatus(status);
            ESAISkillStepRunRecord record = FindStepRecord(run, step.nodeId);
            bool mayRetry = status == "Failed" || status == "TimedOut";
            if (mayRetry && record.currentAttemptCount <= step.task.retryCount)
            {
                record.childRunId = string.Empty;
                record.invocationId = string.Empty;
                record.status = "Retrying";
                record.message = message ?? status;
                ClearCurrentAttemptTransientOutput(record);
                record.currentAttemptStartedAtUtc = string.Empty;
                record.retryAvailableAtUtc = DateTimeOffset.UtcNow
                    .AddSeconds(step.task.retryDelaySeconds)
                    .ToString("O", CultureInfo.InvariantCulture);
                Save(run);
                return true;
            }
            JObject terminalData = BuildTaskTerminalData(record, status, message, taskData);
            if (!TryPublishDataOutputs(run, step.nodeId,
                    ESAgentGraphStableIds.SkillRunResultPortKey, terminalData,
                    out string publicationError))
            {
                message = (message ?? status) + "；终态结果发布失败：" + publicationError;
            }
            CompleteStep(run, step.nodeId, status ?? "Failed", message, terminalData);
            if (ShouldStopFanOutOnFailure(run))
            {
                Fail(run, "FanOut 分支失败，已按 stopOnFailure 终止：" + (message ?? status));
                return false;
            }
            if (string.Equals(run.iterationTaskNodeId, step.nodeId, StringComparison.Ordinal))
            {
                string owner = run.iterationNodeId;
                ClearIteration(run);
                Move(run, owner, ESAgentGraphStableIds.SkillFailurePortKey);
                return false;
            }
            string route = status == "TimedOut" ? ESAgentGraphStableIds.SkillTimeoutPortKey
                : status == "Cancelled" ? ESAgentGraphStableIds.SkillCancelledPortKey
                : ESAgentGraphStableIds.SkillFailurePortKey;
            Move(run, step.nodeId, route);
            return false;
        }

        internal static JObject BuildTaskTerminalData(ESAISkillStepRunRecord record,
            string status, string message, JObject taskData = null)
        {
            JObject terminalData = taskData?.DeepClone() as JObject ?? new JObject();
            terminalData["status"] = status ?? "Failed";
            terminalData["message"] = message ?? string.Empty;
            terminalData["runId"] = record?.childRunId ?? string.Empty;
            terminalData["invocationId"] = record?.invocationId ?? string.Empty;
            return terminalData;
        }

        private static bool IsTaskInProgress(string status)
            => status == "Starting" || status == "Accepted" || status == "Running";

        private static bool IsSuccessfulTaskStatus(string status)
            => status == "Completed" || status == "DryRun";

        internal static string NormalizeTaskFailureStatus(string status)
        {
            switch (status)
            {
                case "Failed":
                case "TimedOut":
                case "Cancelled":
                case "Blocked":
                case "Rejected":
                case "NotFound":
                    return status;
                default:
                    return "Failed";
            }
        }

        private static bool EnterOrContinueForEach(ESAISkillWorkflowRun run, ESAISkillExecutionStep step)
        {
            if (!string.Equals(run.iterationNodeId, step.nodeId, StringComparison.Ordinal))
            {
            JToken source = ResolveBoundValue(run, step.nodeId,
                ESAgentGraphStableIds.SkillItemsPortKey);
                if (!string.IsNullOrWhiteSpace(step.forEach.itemsPath))
                    source = source?.SelectToken(step.forEach.itemsPath, false);
                JArray items = source as JArray;
                if (items == null)
                {
                    BeginStep(run, step.nodeId);
                    CompleteStep(run, step.nodeId, "Failed", "ForEach 输入必须是 JSON Array。",
                        source);
                    Move(run, step.nodeId, ESAgentGraphStableIds.SkillFailurePortKey);
                    return true;
                }
                if (items.Count > step.forEach.maxItems)
                {
                    BeginStep(run, step.nodeId);
                    CompleteStep(run, step.nodeId, "Failed", "ForEach 输入数量 " + items.Count
                        + " 超过上限 " + step.forEach.maxItems + "。", items);
                    Move(run, step.nodeId, ESAgentGraphStableIds.SkillFailurePortKey);
                    return true;
                }
                if (items.Count == 0)
                {
                    CompleteStep(run, step.nodeId, "Completed", "集合为空。", items);
                    Move(run, step.nodeId, ESAgentGraphStableIds.SkillEmptyPortKey);
                    return true;
                }
                run.iterationNodeId = step.nodeId;
                run.iterationTaskNodeId = ResolveRoute(run, step.nodeId, ESAgentGraphStableIds.SkillItemPortKey);
                run.iterationItems = (JArray)items.DeepClone();
                run.iterationIndex = 0;
                BeginStep(run, step.nodeId);
            }
            if (run.iterationIndex >= run.iterationItems.Count)
            {
                CompleteStep(run, step.nodeId, "Completed",
                    "已串行处理 " + run.iterationItems.Count + " 项。", run.iterationItems);
                ClearIteration(run);
                Move(run, step.nodeId, ESAgentGraphStableIds.SkillCompletedPortKey);
                return true;
            }
            SetEndpointValue(run, step.nodeId, ESAgentGraphStableIds.SkillItemValuePortKey,
                run.iterationItems[run.iterationIndex]);
            run.currentNodeId = run.iterationTaskNodeId;
            Save(run);
            return true;
        }

        internal static string EndpointValueKey(string nodeId, string portKey)
            => (nodeId ?? string.Empty) + "\n" + (portKey ?? string.Empty);

        private static void SetEndpointValue(ESAISkillWorkflowRun run, string nodeId,
            string portKey, JToken value)
        {
            if (run == null || string.IsNullOrWhiteSpace(nodeId)) return;
            if (run.values == null) run.values = new JObject();
            JToken clone = value?.DeepClone() ?? JValue.CreateNull();
            run.values[EndpointValueKey(nodeId, portKey)] = clone;
        }

        private static JToken GetEndpointValue(ESAISkillWorkflowRun run, string nodeId,
            string portKey)
        {
            if (run?.values == null) return null;
            return run.values[EndpointValueKey(nodeId, portKey)];
        }

        internal static JToken ResolveBoundValue(ESAISkillWorkflowRun run, string targetNodeId,
            string targetPortKey = null)
        {
            ESAISkillDataBinding[] bindings = (run?.spec?.dataBindings
                ?? Array.Empty<ESAISkillDataBinding>())
                .Where(binding => binding.targetNodeId == targetNodeId
                    && (string.IsNullOrWhiteSpace(targetPortKey)
                        || string.Equals(binding.targetPortKey, targetPortKey,
                            StringComparison.Ordinal)))
                .OrderBy(binding => binding.order)
                .ThenBy(binding => binding.edgeId, StringComparer.Ordinal).ToArray();
            if (bindings.Length == 0) return null;

            var groups = bindings.GroupBy(binding => binding.targetPortKey ?? string.Empty)
                .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
            if (string.IsNullOrWhiteSpace(targetPortKey) && groups.Length > 1)
            {
                var byPort = new JObject();
                foreach (IGrouping<string, ESAISkillDataBinding> group in groups)
                    byPort[group.Key] = ResolveBindingValues(run, group);
                return byPort;
            }
            return ResolveBindingValues(run, groups[0]);
        }

        private static JToken ResolveBindingValues(ESAISkillWorkflowRun run,
            IEnumerable<ESAISkillDataBinding> bindings)
        {
            ESAISkillDataBinding[] ordered = bindings.OrderBy(binding => binding.order)
                .ThenBy(binding => binding.edgeId, StringComparer.Ordinal).ToArray();
            ESGraphPortAggregation aggregation = ordered
                .Select(binding => binding.targetAggregation)
                .FirstOrDefault(value => value != ESGraphPortAggregation.Auto);
            if (aggregation == ESGraphPortAggregation.Single)
            {
                return ordered.Length == 1
                    ? GetEndpointValue(run, ordered[0].sourceNodeId, ordered[0].sourcePortKey)
                        ?.DeepClone() ?? JValue.CreateNull()
                    : null;
            }
            if (aggregation == ESGraphPortAggregation.Named)
            {
                var named = new JObject();
                foreach (IGrouping<string, ESAISkillDataBinding> group in ordered.GroupBy(
                    BindingName, StringComparer.Ordinal))
                {
                    JToken value = group.Count() == 1
                        ? GetEndpointValue(run, group.First().sourceNodeId, group.First().sourcePortKey)
                        : new JArray(group.Select(binding =>
                            GetEndpointValue(run, binding.sourceNodeId, binding.sourcePortKey)
                                ?.DeepClone() ?? JValue.CreateNull()));
                    named[group.Key] = value?.DeepClone() ?? JValue.CreateNull();
                }
                return named;
            }
            if (aggregation != ESGraphPortAggregation.Ordered)
                return null;
            var values = new JArray();
            foreach (ESAISkillDataBinding binding in ordered)
                values.Add(GetEndpointValue(run, binding.sourceNodeId, binding.sourcePortKey)
                    ?.DeepClone() ?? JValue.CreateNull());
            return values;
        }

        private static string BindingName(ESAISkillDataBinding binding)
            => (binding?.sourceNodeId ?? string.Empty) + "/"
                + (binding?.sourcePortKey ?? string.Empty);

        private static void Move(ESAISkillWorkflowRun run, string sourceNodeId, string portKey)
        {
            if (!TryResolveSingleRouteTarget(run?.spec, sourceNodeId, portKey,
                    out string targetNodeId, out string error))
            {
                Fail(run, error);
                return;
            }
            run.currentNodeId = targetNodeId;
            Save(run);
        }

        private static string ResolveRoute(ESAISkillWorkflowRun run, string nodeId, string portKey)
        {
            if (!TryResolveSingleRouteTarget(run?.spec, nodeId, portKey,
                    out string targetNodeId, out string error))
            {
                Fail(run, error);
                return string.Empty;
            }
            return targetNodeId;
        }

        internal static bool TryResolveSingleRouteTarget(ESAISkillExecutionSpec spec,
            string sourceNodeId, string sourcePortKey, out string targetNodeId, out string error)
        {
            targetNodeId = string.Empty;
            if (spec == null || !ESGraphIdentity.IsValid(sourceNodeId)
                || !ESGraphStableIdUtility.IsValid(sourcePortKey))
            {
                error = "控制路线缺少有效的源节点或端点身份。";
                return false;
            }

            ESAISkillControlEdge[] routes = (spec.controlEdges
                    ?? Array.Empty<ESAISkillControlEdge>())
                .Where(edge => edge != null
                    && string.Equals(edge.sourceNodeId, sourceNodeId, StringComparison.Ordinal)
                    && string.Equals(edge.sourcePortKey, sourcePortKey, StringComparison.Ordinal))
                .OrderBy(edge => edge.order)
                .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray();
            if (routes.Length == 0)
            {
                error = "控制出口未连接：" + sourcePortKey;
                return false;
            }
            if (routes.Length > 1)
            {
                error = "控制出口只能连接一个目标；需要执行多个目标时请使用 FanOut："
                    + sourcePortKey;
                return false;
            }

            ESAISkillControlEdge route = routes[0];
            ESAISkillExecutionStep[] sourceSteps = (spec.steps
                    ?? Array.Empty<ESAISkillExecutionStep>())
                .Where(step => step != null
                    && string.Equals(step.nodeId, sourceNodeId, StringComparison.Ordinal))
                .ToArray();
            ESAISkillExecutionStep[] targetSteps = (spec.steps
                    ?? Array.Empty<ESAISkillExecutionStep>())
                .Where(step => step != null
                    && string.Equals(step.nodeId, route.targetNodeId, StringComparison.Ordinal))
                .ToArray();
            ESAISkillExecutionPort sourcePort = null;
            ESAISkillExecutionPort targetPort = null;
            bool hasSourcePort = sourceSteps.Length == 1 && TryGetStepPort(sourceSteps[0],
                sourcePortKey, out sourcePort);
            bool hasTargetPort = targetSteps.Length == 1 && TryGetStepPort(targetSteps[0],
                route.targetPortKey, out targetPort);
            if (!ESGraphIdentity.IsValid(route.edgeId)
                || !ESGraphIdentity.IsValid(route.sourcePortId)
                || !ESGraphIdentity.IsValid(route.targetNodeId)
                || !ESGraphIdentity.IsValid(route.targetPortId)
                || !ESGraphStableIdUtility.IsValid(route.targetPortKey)
                || !hasSourcePort || !hasTargetPort
                || sourcePort.direction != ESGraphPortDirection.Output
                || targetPort.direction != ESGraphPortDirection.Input
                || !string.Equals(sourcePort.portId, route.sourcePortId,
                    StringComparison.Ordinal)
                || !string.Equals(targetPort.portId, route.targetPortId,
                    StringComparison.Ordinal)
                || !string.Equals(sourcePort.meaning, route.sourceMeaning,
                    StringComparison.Ordinal)
                || !string.Equals(targetPort.meaning, route.targetMeaning,
                    StringComparison.Ordinal)
                || !string.Equals(sourcePort.valueTypeId,
                    ESAgentGraphStableIds.SkillControlPort, StringComparison.Ordinal)
                || !string.Equals(targetPort.valueTypeId,
                    ESAgentGraphStableIds.SkillControlPort, StringComparison.Ordinal))
            {
                error = "控制路线目标端点不存在或身份无效：" + sourcePortKey;
                return false;
            }

            targetNodeId = route.targetNodeId;
            error = string.Empty;
            return true;
        }

        internal static bool TrySelectBranchTarget(ESAISkillExecutionSpec spec,
            ESAISkillExecutionStep step, JToken value, out string targetNodeId, out bool matched,
            out JToken selectedValue, out string error)
        {
            targetNodeId = string.Empty;
            matched = false;
            selectedValue = value;
            if (step?.branch == null || step.branch.schemaVersion != 1)
            {
                error = "条件分支缺少有效 Payload。";
                return false;
            }
            try
            {
                if (!string.IsNullOrWhiteSpace(step.branch.valuePath))
                    selectedValue = value?.SelectToken(step.branch.valuePath, false);
            }
            catch (JsonException exception)
            {
                error = "条件分支 ValuePath 无效：" + exception.Message;
                return false;
            }

            matched = selectedValue != null && selectedValue.Type != JTokenType.Null
                && string.Equals(selectedValue.ToString(), step.branch.expectedValue ?? string.Empty,
                    step.branch.ignoreCase ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            string portKey = matched ? ESAgentGraphStableIds.SkillMatchedPortKey
                : ESAgentGraphStableIds.SkillDefaultPortKey;
            return TryResolveSingleRouteTarget(spec, step.nodeId, portKey,
                out targetNodeId, out error);
        }

        private static bool TryGetStepPort(ESAISkillExecutionStep step, string portKey,
            out ESAISkillExecutionPort port)
        {
            port = null;
            if (step?.ports == null || !ESGraphStableIdUtility.IsValid(portKey))
                return false;
            for (int i = 0; i < step.ports.Length; i++)
            {
                ESAISkillExecutionPort candidate = step.ports[i];
                if (candidate == null || !string.Equals(candidate.portKey, portKey,
                        StringComparison.Ordinal))
                    continue;
                if (port != null)
                {
                    port = null;
                    return false;
                }
                port = candidate;
            }
            return port != null;
        }

        private static ESAISkillControlEdge[] ResolveRoutes(ESAISkillWorkflowRun run,
            string nodeId, string portKey)
            => (run?.spec?.controlEdges ?? Array.Empty<ESAISkillControlEdge>())
                .Where(edge => edge != null && edge.sourceNodeId == nodeId
                    && edge.sourcePortKey == portKey)
                .OrderBy(edge => edge.order)
                .ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray();

        private static bool StartFanOut(ESAISkillWorkflowRun run, ESAISkillExecutionStep step)
        {
            if (!string.IsNullOrWhiteSpace(run.activeFanOutNodeId))
            {
                Fail(run, "不支持嵌套 FanOut，当前分支尚未汇合。");
                return false;
            }
            ESAISkillControlEdge[] routes = ResolveRoutes(run, step.nodeId,
                ESAgentGraphStableIds.SkillFanOutPortKey);
            if (routes.Length < 2)
            {
                Fail(run, "FanOut 至少需要两个有效目标分支。");
                return false;
            }
            ESAISkillFanOutJoinPair[] pairs = (run.spec.fanOutJoinPairs
                    ?? Array.Empty<ESAISkillFanOutJoinPair>())
                .Where(value => value != null
                    && string.Equals(value.fanOutNodeId, step.nodeId, StringComparison.Ordinal))
                .ToArray();
            ESAISkillFanOutJoinPair pair = pairs.Length == 1 ? pairs[0] : null;
            if (pair == null || !ESGraphIdentity.IsValid(pair.joinNodeId)
                || !string.Equals(pair.fanOutId, step.fanOut.fanOutId,
                    StringComparison.Ordinal))
            {
                Fail(run, "FanOut 缺少与当前执行合同一致的 Join 配对。");
                return false;
            }
            run.activeFanOutNodeId = step.nodeId;
            run.activeJoinNodeId = pair.joinNodeId;
            run.fanOutExpectedCount = routes.Length;
            run.fanOutArrivedCount = 0;
            run.pendingFanOutNodeIds = routes.Skip(1).Select(route => route.targetNodeId).ToList();
            run.currentNodeId = routes[0].targetNodeId;
            BeginStep(run, step.nodeId);
            CompleteStep(run, step.nodeId, "Completed", "已创建 " + routes.Length + " 个串行分支。");
            Save(run);
            return true;
        }

        private static bool EnterJoin(ESAISkillWorkflowRun run, ESAISkillExecutionStep step)
        {
            if (string.IsNullOrWhiteSpace(run.activeFanOutNodeId))
            {
                Fail(run, "Join 未收到活动 FanOut 分支。");
                return false;
            }
            if (string.IsNullOrWhiteSpace(run.activeJoinNodeId)
                || !string.Equals(run.activeJoinNodeId, step.nodeId, StringComparison.Ordinal))
            {
                Fail(run, "当前 Join 与 FanOut 的已烘焙配对不一致。");
                return false;
            }
            BeginStep(run, step.nodeId);
            run.fanOutArrivedCount++;
            if (run.fanOutArrivedCount < run.fanOutExpectedCount)
            {
                string next = run.pendingFanOutNodeIds?.FirstOrDefault() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    Fail(run, "FanOut 分支数量与 Join 到达数量不一致。");
                    return false;
                }
                run.pendingFanOutNodeIds.RemoveAt(0);
                run.currentNodeId = next;
                CompleteStep(run, step.nodeId, "WaitingBranches",
                    "已汇合 " + run.fanOutArrivedCount + "/" + run.fanOutExpectedCount + " 个分支。");
                Save(run);
                return true;
            }
            if ((run.pendingFanOutNodeIds?.Count ?? 0) != 0)
            {
                Fail(run, "Join 已达到预期数量但仍存在未调度分支。");
                return false;
            }
            CompleteStep(run, step.nodeId, "Completed",
                "全部 " + run.fanOutExpectedCount + " 个分支已汇合。");
            run.activeFanOutNodeId = string.Empty;
            run.activeJoinNodeId = string.Empty;
            run.fanOutExpectedCount = 0;
            run.fanOutArrivedCount = 0;
            run.pendingFanOutNodeIds = new List<string>();
            Move(run, step.nodeId, ESAgentGraphStableIds.SkillJoinPortKey);
            return true;
        }

        private static bool ShouldStopFanOutOnFailure(ESAISkillWorkflowRun run)
        {
            if (string.IsNullOrWhiteSpace(run?.activeFanOutNodeId)) return false;
            ESAISkillExecutionStep fanOut = FindStep(run, run.activeFanOutNodeId);
            return fanOut?.fanOut?.stopOnFailure == true;
        }

        private static ESAISkillExecutionStep FindStep(ESAISkillWorkflowRun run, string nodeId)
            => run?.spec?.steps?.FirstOrDefault(step => step != null && step.nodeId == nodeId);

        private static ESAISkillStepRunRecord FindStepRecord(ESAISkillWorkflowRun run, string nodeId)
            => run.steps.FirstOrDefault(step => step.nodeId == nodeId);

        private static void BeginStep(ESAISkillWorkflowRun run, string nodeId)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, nodeId);
            record.status = "Running";
            record.attemptCount++;
            record.currentAttemptCount++;
            record.currentAttemptStartedAtUtc = DateTimeOffset.UtcNow.ToString("O",
                CultureInfo.InvariantCulture);
            record.retryAvailableAtUtc = string.Empty;
            if (string.IsNullOrWhiteSpace(record.startedAtUtc))
                record.startedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            ESAISkillIterationRunRecord iteration = GetOrCreateCurrentIteration(run, nodeId);
            if (iteration != null)
            {
                iteration.status = "Running";
                iteration.attemptCount = record.currentAttemptCount;
                iteration.startedAtUtc = record.currentAttemptStartedAtUtc;
                iteration.invocationId = record.invocationId;
            }
            Save(run);
        }

        private static void CompleteStep(ESAISkillWorkflowRun run, string nodeId, string status,
            string message, JToken data = null)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, nodeId);
            record.status = status ?? "Completed";
            record.message = message ?? string.Empty;
            record.finishedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (data is JObject obj)
            {
                record.exitCode = obj.Value<int?>("exitCode") ?? -1;
                record.artifacts = obj["outputs"]?.Values<string>().ToArray() ?? Array.Empty<string>();
                record.outputHashes = obj["outputHashes"]?.Values<string>().ToArray() ?? Array.Empty<string>();
            }
            ESAISkillIterationRunRecord iteration = GetOrCreateCurrentIteration(run, nodeId);
            if (iteration != null)
            {
                iteration.status = record.status;
                iteration.finishedAtUtc = record.finishedAtUtc;
                iteration.message = record.message;
                iteration.invocationId = record.invocationId;
                iteration.childRunId = record.childRunId;
                iteration.artifacts = record.artifacts?.ToArray() ?? Array.Empty<string>();
                iteration.outputHashes = record.outputHashes?.ToArray() ?? Array.Empty<string>();
            }
            Save(run);
        }

        private static void ResetStepForNextAttempt(ESAISkillWorkflowRun run, string nodeId)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, nodeId);
            record.status = "Pending";
            record.invocationId = string.Empty;
            record.childRunId = string.Empty;
            record.childApprovalGeneration = 0;
            record.currentAttemptCount = 0;
            record.currentAttemptStartedAtUtc = string.Empty;
            record.retryAvailableAtUtc = string.Empty;
            record.startedAtUtc = string.Empty;
            record.finishedAtUtc = string.Empty;
            record.message = string.Empty;
            ClearCurrentAttemptTransientOutput(record);
        }

        internal static void ClearCurrentAttemptTransientOutput(ESAISkillStepRunRecord record)
        {
            if (record == null) return;
            record.exitCode = -1;
            record.diagnostics = Array.Empty<string>();
            record.artifacts = Array.Empty<string>();
            record.outputHashes = Array.Empty<string>();
        }

        private static ESAISkillIterationRunRecord GetOrCreateCurrentIteration(
            ESAISkillWorkflowRun run, string nodeId)
        {
            if (run == null || !string.Equals(run.iterationTaskNodeId, nodeId,
                    StringComparison.Ordinal) || run.iterationIndex < 0
                || run.iterationIndex >= (run.iterationItems?.Count ?? 0)) return null;
            ESAISkillStepRunRecord step = FindStepRecord(run, nodeId);
            if (step == null) return null;
            step.iterations ??= new List<ESAISkillIterationRunRecord>();
            ESAISkillIterationRunRecord item = step.iterations.FirstOrDefault(value =>
                value != null && value.index == run.iterationIndex);
            if (item != null) return item;
            item = new ESAISkillIterationRunRecord
            {
                index = run.iterationIndex,
                inputHash = ComputeTokenHash(run.iterationItems[run.iterationIndex]),
            };
            step.iterations.Add(item);
            return item;
        }

        private static void SyncCurrentIterationIdentity(ESAISkillWorkflowRun run,
            ESAISkillStepRunRecord record)
        {
            ESAISkillIterationRunRecord item = GetOrCreateCurrentIteration(run, record?.nodeId);
            if (item == null || record == null) return;
            item.invocationId = record.invocationId;
            item.childRunId = record.childRunId;
        }

        private static void ClearIteration(ESAISkillWorkflowRun run)
        {
            run.iterationNodeId = string.Empty;
            run.iterationTaskNodeId = string.Empty;
            run.iterationIndex = -1;
            run.iterationItems = new JArray();
        }

        private static void Fail(ESAISkillWorkflowRun run, string message)
            => Finish(run, "Failed", message, -1);

        private static void MarkCancellationFailed(ESAISkillWorkflowRun run, string message)
        {
            run.cancellationOutcome = "Failed";
            run.cancellationMessage = message ?? "取消未被确认。";
            Finish(run, "Blocked", "取消未能形成终态确认：" + run.cancellationMessage, -1);
        }

        private static void PollCancellation(ESAISkillWorkflowRun run)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, run.currentNodeId);
            ESAISkillExecutionStep step = FindStep(run, run.currentNodeId);
            if (record == null || step == null || string.IsNullOrWhiteSpace(record.childRunId))
            {
                MarkCancellationFailed(run, "取消中的 Run 缺少当前子运行身份。");
                return;
            }
            string status;
            string message;
            ESAISkillWorkflowRun childRun = null;
            ES.ESAutomationTaskInvocationResult automationResult = null;
            if (step.skillCall != null)
            {
                if (!TryGet(record.childRunId, out childRun))
                {
                    MarkCancellationFailed(run, "取消中的子 AISkill RunRecord 不存在。");
                    return;
                }
                status = childRun.status;
                message = childRun.message;
            }
            else
            {
                automationResult = ES.ESAutomationFacade.GetRun(
                    record.childRunId, false);
                status = automationResult.status;
                message = automationResult.message;
            }
            if (IsTaskInProgress(status) || status == "Cancelling") return;
            if (status != "Cancelled" && status != "Completed")
            {
                MarkCancellationFailed(run, message ?? ("子运行终态为 " + status));
                return;
            }
            bool timedOut = string.Equals(run.cancellationOutcome, "PendingAfterTimeout",
                StringComparison.Ordinal);
            run.cancellationOutcome = "Confirmed";
            run.cancellationMessage = message ?? string.Empty;
            if (timedOut)
            {
                run.status = "Running";
                if (status == "Completed")
                {
                    if (step.skillCall != null)
                    {
                        JObject result = BuildChildRunResult(childRun);
                        if (!TryPublishDataOutputs(run, step.nodeId,
                                ESAgentGraphStableIds.SkillRunResultPortKey, result,
                                out string publishError))
                        {
                            HandleSkillCallFailure(run, step, "Failed", publishError);
                            return;
                        }
                        CompleteStep(run, step.nodeId, "Completed",
                            "子 AISkill 在取消生效前已完成。", result);
                        Move(run, step.nodeId, ESAgentGraphStableIds.SkillSuccessPortKey);
                    }
                    else
                    {
                        HandleTaskTerminal(run, step, automationResult);
                    }
                    return;
                }
                if (step.skillCall != null)
                    HandleSkillCallFailure(run, step, "TimedOut", "子 AISkill 超时且已确认停止。");
                else
                    HandleTaskFailure(run, step, "TimedOut", "Automation 步骤超时且已确认停止。");
                return;
            }
            Finish(run, "Cancelled", "子运行已确认停止，工作流取消完成。", -1);
        }

        private static void Finish(ESAISkillWorkflowRun run, string status, string message, int exitCode)
        {
            run.status = status;
            run.message = message ?? string.Empty;
            run.exitCode = exitCode;
            run.finishedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Save(run);
            Active.Remove(run.runId);
            TrySaveActiveRunIds();
        }

        private static bool IsTerminal(string status)
            => status == "Completed" || status == "Failed" || status == "Cancelled" || status == "Blocked";

        private static bool TryValidateInputs(ESAISkillExecutionSpec spec, JObject values,
            out JObject normalized, out string error)
        {
            normalized = values == null ? new JObject() : (JObject)values.DeepClone();
            if (!ESAISkillExecutionBaker.TryValidateSpec(spec, out error))
                return false;
            foreach (ESAISkillParameter parameter in spec.parameters ?? Array.Empty<ESAISkillParameter>())
            {
                JToken value = normalized[parameter.parameterId];
                if ((value == null || value.Type == JTokenType.Null || string.IsNullOrWhiteSpace(value.ToString()))
                    && !string.IsNullOrWhiteSpace(parameter.defaultValue))
                {
                    value = ParseDefault(parameter);
                    normalized[parameter.parameterId] = value;
                }
                if (parameter.required && (value == null || value.Type == JTokenType.Null
                    || string.IsNullOrWhiteSpace(value.ToString())))
                {
                    error = "必填参数为空：" + parameter.parameterId;
                    return false;
                }
                if (value != null && !ValidateValue(parameter, value, out error))
                    return false;
            }
            error = string.Empty;
            return true;
        }

        private static JToken ParseDefault(ESAISkillParameter parameter)
        {
            switch (parameter.valueType)
            {
                case ESAISkillValueType.Boolean:
                    return bool.TryParse(parameter.defaultValue, out bool boolean) ? boolean : false;
                case ESAISkillValueType.Integer:
                    return int.TryParse(parameter.defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int integer) ? integer : 0;
                case ESAISkillValueType.TextList:
                case ESAISkillValueType.ProjectPathList:
                    return new JArray(parameter.defaultValue.Split(new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()));
                default:
                    return parameter.defaultValue;
            }
        }

        private static bool ValidateValue(ESAISkillParameter parameter, JToken value, out string error)
        {
            if (parameter.valueType == ESAISkillValueType.Boolean && value.Type != JTokenType.Boolean
                || parameter.valueType == ESAISkillValueType.Integer && value.Type != JTokenType.Integer
                || (parameter.valueType == ESAISkillValueType.TextList
                    || parameter.valueType == ESAISkillValueType.ProjectPathList) && value.Type != JTokenType.Array)
            {
                error = "参数类型不匹配：" + parameter.parameterId + " 应为 " + parameter.valueType;
                return false;
            }
            if (parameter.valueType == ESAISkillValueType.Choice
                && !(parameter.choices ?? Array.Empty<string>()).Contains(value.ToString(), StringComparer.Ordinal))
            {
                error = "参数不在允许选项中：" + parameter.parameterId;
                return false;
            }
            if (parameter.valueType == ESAISkillValueType.ProjectPath)
                return ValidateProjectPath(parameter, value.ToString(), out error);
            if (parameter.valueType == ESAISkillValueType.ProjectPathList)
            {
                foreach (JToken item in (JArray)value)
                    if (!ValidateProjectPath(parameter, item.ToString(), out error)) return false;
            }
            if (!string.IsNullOrWhiteSpace(parameter.validationPattern)
                && !Regex.IsMatch(value.ToString(), parameter.validationPattern))
            {
                error = "参数不符合格式约束：" + parameter.parameterId;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateProjectPath(ESAISkillParameter parameter, string path, out string error)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized)
                || normalized.Split('/').Contains(".."))
            {
                error = "项目路径必须是无 .. 的相对路径：" + parameter.parameterId;
                return false;
            }
            string[] roots = parameter.allowedRoots ?? Array.Empty<string>();
            if (roots.Length > 0 && !roots.Any(root => normalized == root.TrimEnd('/')
                || normalized.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal)))
            {
                error = "项目路径越出参数允许根：" + parameter.parameterId;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static string RunDirectory(string runId) => Path.Combine(RunsRoot, runId ?? string.Empty);
        private static string RunPath(string runId) => Path.Combine(RunDirectory(runId), "workflow-run.json");

        private static void Save(ESAISkillWorkflowRun run)
        {
            run.schemaVersion = ESAISkillWorkflowRun.CurrentSchemaVersion;
            run.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            run.runStateHash = ComputeRunStateHash(run);
            ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(RunPath(run.runId),
                JsonConvert.SerializeObject(run, Formatting.Indented), new[] { RunsRoot });
            ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(
                Path.Combine(RunDirectory(run.runId), "input-snapshot.json"),
                JsonConvert.SerializeObject(run.inputs ?? new JObject(), Formatting.Indented),
                new[] { RunsRoot });
            foreach (ESAISkillStepRunRecord step in run.steps ?? new List<ESAISkillStepRunRecord>())
            {
                if (step == null || !ESGraphIdentity.IsValid(step.nodeId)) continue;
                ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(
                    Path.Combine(RunDirectory(run.runId), "steps", step.nodeId, "step-record.json"),
                    JsonConvert.SerializeObject(step, Formatting.Indented), new[] { RunsRoot });
            }
            var artifacts = new JObject
            {
                ["schemaVersion"] = 1,
                ["runId"] = run.runId,
                ["artifacts"] = new JArray((run.steps ?? new List<ESAISkillStepRunRecord>())
                    .Where(step => step != null)
                    .SelectMany(step => (step.artifacts ?? Array.Empty<string>())
                        .Select((path, index) => new JObject
                        {
                            ["nodeId"] = step.nodeId,
                            ["path"] = path,
                            ["sha256"] = index < (step.outputHashes?.Length ?? 0)
                                ? step.outputHashes[index] : string.Empty
                        }).Concat((step.iterations ?? new List<ESAISkillIterationRunRecord>())
                            .Where(item => item != null)
                            .SelectMany(item => (item.artifacts ?? Array.Empty<string>())
                                .Select((path, index) => new JObject
                                {
                                    ["nodeId"] = step.nodeId,
                                    ["iterationIndex"] = item.index,
                                    ["path"] = path,
                                    ["sha256"] = index < (item.outputHashes?.Length ?? 0)
                                        ? item.outputHashes[index] : string.Empty
                                })))))
            };
            ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(
                Path.Combine(RunDirectory(run.runId), "artifacts", "artifact-manifest.json"),
                artifacts.ToString(Formatting.Indented), new[] { RunsRoot });
        }

        private static bool TryLoad(string runId, out ESAISkillWorkflowRun run, out string error)
        {
            run = null;
            if (!Guid.TryParseExact(runId, "N", out _)) { error = "RunId 无效。"; return false; }
            string path = RunPath(runId);
            if (!File.Exists(path)) { error = "RunRecord 不存在。"; return false; }
            try
            {
                run = JsonConvert.DeserializeObject<ESAISkillWorkflowRun>(File.ReadAllText(path,
                    new System.Text.UTF8Encoding(false, true)));
                if (run == null
                    || run.schemaVersion != ESAISkillWorkflowRun.CurrentSchemaVersion
                    || run.spec == null
                    || run.spec.schemaVersion != ESAISkillExecutionSpec.CurrentSchemaVersion
                    || !string.Equals(run.graphId, run.spec.sourceGraphId, StringComparison.Ordinal)
                    || !string.Equals(run.contentSignature, run.spec.sourceContentSignature, StringComparison.Ordinal))
                    throw new InvalidDataException("RunRecord 与内嵌执行合同不一致。");
                if (!ES.ESAutomationWorkerRegistration.IsSha256(run.executionSpecHash)
                    || !string.Equals(run.executionSpecHash, ComputeExecutionSpecHash(run.spec),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("RunRecord 的执行合同 Hash 与内嵌 Spec 不一致。");
                if (!ESAISkillExecutionBaker.TryValidateSpec(run.spec, out string specError))
                    throw new InvalidDataException("RunRecord 的内嵌执行合同无效：" + specError);
                if (!ES.ESAutomationWorkerRegistration.IsSha256(run.runStateHash)
                    || !string.Equals(run.runStateHash, ComputeRunStateHash(run),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("RunRecord 可变状态摘要不一致。");
                if (!TryValidateRunState(run, out string stateError))
                    throw new InvalidDataException("RunRecord 状态不变量无效：" + stateError);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) { run = null; error = exception.Message; return false; }
        }

        private static void RestoreActiveRuns()
        {
            EditorApplication.update -= Update;
            Active.Clear();
            var ids = new HashSet<string>(SessionState.GetString(ActiveRunsSessionKey, string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            string indexPath = Path.Combine(RunsRoot, "active-runs.json");
            if (File.Exists(indexPath))
            {
                try
                {
                    foreach (string id in JArray.Parse(File.ReadAllText(indexPath,
                                 new System.Text.UTF8Encoding(false, true))).Values<string>())
                        if (Guid.TryParseExact(id, "N", out _)) ids.Add(id);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ES AISkillGraph] 活跃运行索引无效，拒绝猜测恢复：" + exception.Message);
                }
            }
            var candidates = new Dictionary<string, ESAISkillWorkflowRun>(StringComparer.Ordinal);
            var blocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids.Distinct(StringComparer.Ordinal))
            {
                if (!TryLoad(id, out ESAISkillWorkflowRun run, out _) || IsTerminal(run.status))
                    continue;
                candidates[id] = run;
                if (!TryValidateCurrentSource(run, out string sourceError))
                {
                    BlockRestoredRun(run, sourceError);
                    blocked.Add(id);
                }
            }

            bool changed;
            do
            {
                changed = false;
                foreach (ESAISkillWorkflowRun run in candidates.Values)
                {
                    if (blocked.Contains(run.runId) || string.IsNullOrWhiteSpace(run.parentRunId))
                        continue;
                    if (!candidates.TryGetValue(run.parentRunId, out ESAISkillWorkflowRun parent)
                        || blocked.Contains(run.parentRunId)
                        || !(parent.steps ?? new List<ESAISkillStepRunRecord>()).Any(step =>
                            step != null && (string.Equals(step.childRunId, run.runId,
                                StringComparison.Ordinal) || string.Equals(step.invocationId,
                                run.runId, StringComparison.Ordinal))))
                    {
                        BlockRestoredRun(run,
                            "父 AISkill Run 未处于同一份有效活动集合或缺少反向子运行引用，拒绝孤立恢复。" );
                        blocked.Add(run.runId);
                        changed = true;
                    }
                }
            }
            while (changed);

            foreach (KeyValuePair<string, ESAISkillWorkflowRun> pair in candidates)
                if (!blocked.Contains(pair.Key)) Active[pair.Key] = pair.Value;
            TrySaveActiveRunIds();
            EnsureUpdateHook();
        }

        private static void BlockRestoredRun(ESAISkillWorkflowRun run, string reason)
        {
            run.status = "Blocked";
            run.message = "恢复已阻断：" + reason;
            run.exitCode = -1;
            run.finishedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            try { Save(run); }
            catch (Exception exception)
            {
                Debug.LogError("[ES AISkillGraph] 无法持久化恢复阻断状态：" + exception.Message);
            }
        }

        private static bool TryValidateCurrentSource(ESAISkillWorkflowRun run, out string error)
        {
            if (run == null || string.IsNullOrWhiteSpace(run.sourceAssetGuid))
            {
                error = "旧 RunRecord 缺少私有源资产 GUID，只允许查看，不能自动续跑。";
                return false;
            }
            string assetPath = AssetDatabase.GUIDToAssetPath(run.sourceAssetGuid);
            ESGraphAssetBase asset = string.IsNullOrWhiteSpace(assetPath)
                ? null : AssetDatabase.LoadAssetAtPath<ESGraphAssetBase>(assetPath);
            if (asset == null)
            {
                error = "源 Graph 资产已移动到不可解析位置或已不存在。";
                return false;
            }
            if (!string.Equals(asset.GraphId, run.graphId, StringComparison.Ordinal))
            {
                error = "源资产 GraphId 与 RunRecord 不一致。";
                return false;
            }
            if (!ESGraphSnapshotBaker.TryBake(asset, out ESBakedGraphSnapshot current,
                    out List<ESGraphValidationIssue> issues))
            {
                ESGraphValidationIssue first = issues?.FirstOrDefault(issue => issue != null
                    && issue.severity == ESGraphValidationSeverity.Error);
                error = "源 Graph 当前无法生成验证快照：" + (first?.message ?? "未知错误");
                return false;
            }
            if (!string.Equals(current.ContentSignature, run.contentSignature,
                    StringComparison.Ordinal))
            {
                error = "源 Graph 内容签名已变化；旧 Run 保留审计记录，但不得继续执行。";
                return false;
            }
            if (!ESAISkillExecutionLauncher.TryBake(asset, out ESAISkillExecutionSpec currentSpec,
                    out string bakeError))
            {
                error = "源 Graph 当前无法重建 AISkill 执行合同：" + bakeError;
                return false;
            }
            string currentSpecHash = ComputeExecutionSpecHash(currentSpec);
            if (!string.Equals(currentSpecHash, run.executionSpecHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "当前重新 Bake 的执行合同与持久化合同 Hash 不一致。";
                return false;
            }
            run.spec = currentSpec;
            error = string.Empty;
            return true;
        }

        internal static string ComputeExecutionSpecHash(ESAISkillExecutionSpec spec)
        {
            string json = JsonConvert.SerializeObject(spec, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
            return ComputeUtf8Hash(json);
        }

        private static string ComputeTokenHash(JToken value)
            => ComputeUtf8Hash((value ?? JValue.CreateNull()).ToString(Formatting.None));

        internal static string ComputeRunStateHash(ESAISkillWorkflowRun run)
        {
            JObject state = JObject.FromObject(run ?? throw new ArgumentNullException(nameof(run)));
            state["runStateHash"] = string.Empty;
            return ComputeUtf8Hash(state.ToString(Formatting.None));
        }

        private static string ComputeUtf8Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value
                    ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static bool TryValidateRunState(ESAISkillWorkflowRun run, out string error)
        {
            if (run == null || !Guid.TryParseExact(run.runId, "N", out _)
                || run.spec?.schemaVersion != ESAISkillExecutionSpec.CurrentSchemaVersion
                || run.spec.steps == null)
            {
                error = "缺少有效 RunId 或执行步骤。";
                return false;
            }
            string[] allowedStatuses = { "Running", "WaitingApproval", "Cancelling",
                "Completed", "Failed", "Cancelled", "Blocked" };
            if (!allowedStatuses.Contains(run.status, StringComparer.Ordinal))
            {
                error = "Run 状态不在允许状态机中。";
                return false;
            }
            bool hasApprovalEvidenceBinding = !string.IsNullOrWhiteSpace(
                run.approvalEvidenceManifestPath)
                || !string.IsNullOrWhiteSpace(run.approvalEvidenceManifestHash)
                || !string.IsNullOrWhiteSpace(run.approvalEvidenceNodeId)
                || run.approvalEvidenceGeneration != 0;
            if (hasApprovalEvidenceBinding
                && (string.IsNullOrWhiteSpace(run.approvalEvidenceManifestPath)
                    || !ES.ESAutomationWorkerRegistration.IsSha256(
                        run.approvalEvidenceManifestHash)
                    || !ESGraphIdentity.IsValid(run.approvalEvidenceNodeId)
                    || run.approvalEvidenceGeneration <= 0
                    || run.approvalEvidenceGeneration > run.approvalGeneration))
            {
                error = "审批证据 manifest 绑定字段不完整或代际无效。";
                return false;
            }
            var specNodeIds = new HashSet<string>(run.spec.steps.Where(step => step != null)
                .Select(step => step.nodeId), StringComparer.Ordinal);
            if (specNodeIds.Count != run.spec.steps.Length
                || run.steps == null || run.steps.Count != specNodeIds.Count
                || run.steps.Any(step => step == null || !specNodeIds.Contains(step.nodeId))
                || run.steps.Select(step => step.nodeId).Distinct(StringComparer.Ordinal).Count()
                    != run.steps.Count)
            {
                error = "StepRecord 集合与执行合同节点不一致。";
                return false;
            }
            if (!IsTerminal(run.status) && !specNodeIds.Contains(run.currentNodeId))
            {
                error = "活动 Run 的 currentNodeId 不在执行合同中。";
                return false;
            }
            if (run.callDepth < 0 || run.callDepth > MaximumSkillCallDepth
                || !string.IsNullOrWhiteSpace(run.parentRunId)
                && !Guid.TryParseExact(run.parentRunId, "N", out _))
            {
                error = "父子调用深度或父 RunId 无效。";
                return false;
            }
            bool iterating = !string.IsNullOrWhiteSpace(run.iterationNodeId);
            if (iterating != !string.IsNullOrWhiteSpace(run.iterationTaskNodeId)
                || iterating && (!specNodeIds.Contains(run.iterationNodeId)
                    || !specNodeIds.Contains(run.iterationTaskNodeId)
                    || run.iterationItems == null || run.iterationIndex < 0
                    || run.iterationIndex > run.iterationItems.Count)
                || !iterating && (run.iterationIndex != -1
                    || (run.iterationItems?.Count ?? 0) != 0))
            {
                error = "ForEach 游标与节点身份不一致。";
                return false;
            }
            bool branching = !string.IsNullOrWhiteSpace(run.activeFanOutNodeId);
            if (branching)
            {
                ESAISkillExecutionStep fanOut = FindStep(run, run.activeFanOutNodeId);
                ESAISkillControlEdge[] declaredRoutes = ResolveRoutes(run,
                    run.activeFanOutNodeId, ESAgentGraphStableIds.SkillFanOutPortKey);
                string[] expectedPendingTargets = declaredRoutes
                    .Skip(run.fanOutArrivedCount + 1)
                    .Select(route => route.targetNodeId).ToArray();
                if (fanOut?.fanOut == null
                    || run.fanOutExpectedCount < 2
                    || declaredRoutes.Length != run.fanOutExpectedCount
                    || run.fanOutArrivedCount < 0
                    || run.fanOutArrivedCount >= run.fanOutExpectedCount
                    || run.pendingFanOutNodeIds == null
                    || !run.pendingFanOutNodeIds.SequenceEqual(expectedPendingTargets,
                        StringComparer.Ordinal))
                {
                    error = "FanOut 活动多目标状态与执行合同不一致。";
                    return false;
                }
                string currentBranchStart = declaredRoutes[run.fanOutArrivedCount].targetNodeId;
                HashSet<string> currentBranchScope = CollectFanOutBranchScope(run.spec,
                    currentBranchStart, out HashSet<string> reachableJoins);
                if (!currentBranchScope.Contains(run.currentNodeId)
                    || reachableJoins.Count != 1)
                {
                    error = "FanOut 当前节点不属于正在执行的合同分支，或该分支缺少唯一 Join。";
                    return false;
                }
                string expectedJoinNodeId = reachableJoins.Single();
                ESAISkillFanOutJoinPair[] declaredPairs = (run.spec.fanOutJoinPairs
                        ?? Array.Empty<ESAISkillFanOutJoinPair>())
                    .Where(pair => pair != null
                        && string.Equals(pair.fanOutNodeId, run.activeFanOutNodeId,
                            StringComparison.Ordinal)).ToArray();
                ESAISkillFanOutJoinPair declaredPair = declaredPairs.Length == 1
                    ? declaredPairs[0] : null;
                if (declaredPair == null
                    || !string.Equals(declaredPair.joinNodeId, expectedJoinNodeId,
                        StringComparison.Ordinal)
                    || !string.Equals(run.activeJoinNodeId, expectedJoinNodeId,
                        StringComparison.Ordinal))
                {
                    error = "活动 Join 与 FanOut 合同声明的唯一 Join 不一致。";
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(run.activeJoinNodeId)
                || run.fanOutExpectedCount != 0 || run.fanOutArrivedCount != 0
                || (run.pendingFanOutNodeIds?.Count ?? 0) != 0)
            {
                error = "非活动 FanOut 不应携带分支续点状态。";
                return false;
            }
            foreach (ESAISkillStepRunRecord step in run.steps)
            {
                if (!string.IsNullOrWhiteSpace(step.invocationId)
                    && !Guid.TryParseExact(step.invocationId, "N", out _)
                    || !string.IsNullOrWhiteSpace(step.childRunId)
                    && !Guid.TryParseExact(step.childRunId, "N", out _))
                {
                    error = "步骤包含无效 InvocationId 或 ChildRunId。";
                    return false;
                }
                var indices = new HashSet<int>();
                foreach (ESAISkillIterationRunRecord item in step.iterations
                             ?? new List<ESAISkillIterationRunRecord>())
                {
                    if (item == null || item.index < 0 || !indices.Add(item.index)
                        || !ES.ESAutomationWorkerRegistration.IsSha256(item.inputHash)
                        || !string.IsNullOrWhiteSpace(item.invocationId)
                        && !Guid.TryParseExact(item.invocationId, "N", out _)
                        || !string.IsNullOrWhiteSpace(item.childRunId)
                        && !Guid.TryParseExact(item.childRunId, "N", out _))
                    {
                        error = "ForEach 逐项记录身份或输入 Hash 无效。";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        private static HashSet<string> CollectFanOutBranchScope(ESAISkillExecutionSpec spec,
            string startNodeId, out HashSet<string> reachableJoins)
        {
            reachableJoins = new HashSet<string>(StringComparer.Ordinal);
            var scope = new HashSet<string>(StringComparer.Ordinal);
            var steps = (spec?.steps ?? Array.Empty<ESAISkillExecutionStep>())
                .Where(step => step != null)
                .GroupBy(step => step.nodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var outgoing = (spec?.controlEdges ?? Array.Empty<ESAISkillControlEdge>())
                .Where(edge => edge != null)
                .GroupBy(edge => edge.sourceNodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(startNodeId ?? string.Empty);
            while (queue.Count > 0)
            {
                string nodeId = queue.Dequeue();
                if (!scope.Add(nodeId) || !steps.TryGetValue(nodeId, out ESAISkillExecutionStep step))
                    continue;
                if (step.join != null)
                {
                    reachableJoins.Add(nodeId);
                    continue;
                }
                if (outgoing.TryGetValue(nodeId, out ESAISkillControlEdge[] routes))
                    foreach (ESAISkillControlEdge route in routes)
                        queue.Enqueue(route.targetNodeId);
            }
            return scope;
        }

        private static void TrySaveActiveRunIds()
        {
            try
            {
                string[] ids = Active.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                SessionState.SetString(ActiveRunsSessionKey, string.Join(";", ids));
                ES.ESAutomationPathPolicy.WriteWorkerTextAtomic(
                    Path.Combine(RunsRoot, "active-runs.json"),
                    JsonConvert.SerializeObject(ids, Formatting.Indented), new[] { RunsRoot });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ES AISkillGraph] 活跃运行索引写入失败；各 RunRecord 仍保留，可从运行目录查看："
                    + exception.Message);
            }
        }

        private static void EnsureUpdateHook()
        {
            EditorApplication.update -= Update;
            if (Active.Count > 0) EditorApplication.update += Update;
        }
    }


    internal sealed class ESAISkillExecutionCoordinatorInitializer : ES.EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAISkillExecutionCoordinator.InitializeForEditor();
        }
    }
}
