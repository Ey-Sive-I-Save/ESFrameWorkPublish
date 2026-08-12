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
    public sealed class ESAISkillApprovalPayload
    {
        public int schemaVersion = 1;
        public string title = "需要人工确认";
        [TextArea(2, 8)] public string message = "请检查当前步骤产物后决定是否继续。";
        public bool requireCommentOnReject = true;
    }

    [Serializable]
    public sealed class ESAISkillOutputPayload
    {
        public int schemaVersion = 1;
        public string outputId = "result";
        public string displayName = "工作流结果";
    }

    [Serializable]
    public sealed class ESAISkillExecutionStep
    {
        public string nodeId = string.Empty;
        public string nodeTypeId = string.Empty;
        public string title = string.Empty;
        public ESAISkillInputPayload input;
        public ESAISkillTaskPayload task;
        public ESAISkillCallPayload skillCall;
        public ESAISkillBranchPayload branch;
        public ESAISkillForEachPayload forEach;
        public ESAISkillApprovalPayload approval;
        public ESAISkillOutputPayload output;
    }

    [Serializable]
    public sealed class ESAISkillControlEdge
    {
        public string edgeId = string.Empty;
        public string sourceNodeId = string.Empty;
        public string sourcePortKey = string.Empty;
        public string targetNodeId = string.Empty;
    }

    [Serializable]
    public sealed class ESAISkillDataBinding
    {
        public string edgeId = string.Empty;
        public string sourceNodeId = string.Empty;
        public string sourcePortKey = string.Empty;
        public string targetNodeId = string.Empty;
        public string targetPortKey = string.Empty;
        public string valueTypeId = string.Empty;
    }

    [Serializable]
    public sealed class ESAISkillExecutionSpec : IESBakedGraphPlan
    {
        public int schemaVersion = 1;
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
            var nodeByPort = new Dictionary<string, ESGraphNodeSnapshot>(StringComparer.Ordinal);
            var portById = new Dictionary<string, ESGraphPortSnapshot>(StringComparer.Ordinal);
            foreach (ESGraphNodeSnapshot node in source.Nodes)
            {
                foreach (ESGraphPortSnapshot port in node.Ports)
                {
                    nodeByPort[port.PortId] = node;
                    portById[port.PortId] = port;
                }
                if (!TryBakeStep(node, out ESAISkillExecutionStep step, out string error))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Payload", error, node.NodeId));
                else
                    steps.Add(step);
            }

            var controls = new List<ESAISkillControlEdge>();
            var bindings = new List<ESAISkillDataBinding>();
            foreach (ESGraphEdgeSnapshot edge in source.Edges)
            {
                if (!nodeByPort.TryGetValue(edge.OutputPortId, out ESGraphNodeSnapshot from)
                    || !nodeByPort.TryGetValue(edge.InputPortId, out ESGraphNodeSnapshot to)
                    || !portById.TryGetValue(edge.OutputPortId, out ESGraphPortSnapshot output)
                    || !portById.TryGetValue(edge.InputPortId, out ESGraphPortSnapshot input))
                {
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Edge", "无法解析执行边端点。", edge.EdgeId));
                    continue;
                }
                if (string.Equals(output.ValueTypeId, ESAgentGraphStableIds.SkillControlPort,
                        StringComparison.Ordinal))
                {
                    controls.Add(new ESAISkillControlEdge
                    {
                        edgeId = edge.EdgeId,
                        sourceNodeId = from.NodeId,
                        sourcePortKey = output.StableKey,
                        targetNodeId = to.NodeId
                    });
                }
                else
                {
                    bindings.Add(new ESAISkillDataBinding
                    {
                        edgeId = edge.EdgeId,
                        sourceNodeId = from.NodeId,
                        sourcePortKey = output.StableKey,
                        targetNodeId = to.NodeId,
                        targetPortKey = input.StableKey,
                        valueTypeId = output.ValueTypeId
                    });
                }
            }

            ValidateTopology(steps, controls, bindings, failures);
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
                controlEdges = controls.OrderBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray(),
                dataBindings = bindings.OrderBy(edge => edge.edgeId, StringComparer.Ordinal).ToArray()
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
                title = node.Title
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
                && step.approval == null && step.output == null)
            {
                error = "节点缺少可用执行 Payload。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static void ValidateTopology(List<ESAISkillExecutionStep> steps,
            List<ESAISkillControlEdge> controls, List<ESAISkillDataBinding> bindings,
            List<ESGraphValidationIssue> failures)
        {
            ESAISkillExecutionStep[] entries = steps.Where(step => step.input != null).ToArray();
            if (entries.Length != 1)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.EntryCount",
                    "执行图必须且只能有一个参数入口，当前为 " + entries.Length + " 个。"));
            if (steps.Count(step => step.output != null) < 1)
                failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.OutputMissing", "执行图至少需要一个结构化输出。"));

            var ids = new HashSet<string>(steps.Select(step => step.nodeId), StringComparer.Ordinal);
            var controlKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAISkillControlEdge edge in controls)
            {
                if (!ids.Contains(edge.sourceNodeId) || !ids.Contains(edge.targetNodeId)
                    || !controlKeys.Add(edge.sourceNodeId + "\n" + edge.sourcePortKey))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ControlEdge",
                        "控制出口只能连接一个确定目标。", edge.edgeId));
            }

            foreach (ESAISkillExecutionStep step in steps)
            {
                if (step.input != null) ValidateInput(step, failures);
                if (step.task != null) ValidateTask(step, failures);
                if (step.skillCall != null) ValidateSkillCall(step, failures);
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
                    ESAISkillControlEdge item = controls.FirstOrDefault(edge => edge.sourceNodeId == step.nodeId
                        && edge.sourcePortKey == ESAgentGraphStableIds.SkillItemPortKey);
                    ESAISkillExecutionStep body = item == null ? null : steps.FirstOrDefault(value => value.nodeId == item.targetNodeId);
                    if (body?.task == null && body?.skillCall == null)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ForEachBody",
                            "ForEach 的逐项出口必须直接连接一个 Task 或调用 AISkill；循环由协调器内部管理。", step.nodeId));
                }
            }

            foreach (ESAISkillDataBinding binding in bindings)
            {
                if (!ids.Contains(binding.sourceNodeId) || !ids.Contains(binding.targetNodeId)
                    || string.IsNullOrWhiteSpace(binding.valueTypeId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.Binding",
                        "值绑定缺少稳定端点或类型。", binding.edgeId));
            }
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
            return Array.Empty<string>();
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
        public int schemaVersion = 1;
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
        public string cancellationRequestedAtUtc = string.Empty;
        public string cancellationOutcome = string.Empty;
        public string cancellationMessage = string.Empty;
        public string iterationNodeId = string.Empty;
        public string iterationTaskNodeId = string.Empty;
        public int iterationIndex = -1;
        public JArray iterationItems = new JArray();
        public JObject inputs = new JObject();
        public JObject values = new JObject();
        public ESAISkillExecutionSpec spec;
        public List<ESAISkillStepRunRecord> steps = new List<ESAISkillStepRunRecord>();
    }

    public static class ESAISkillExecutionCoordinator
    {
        internal const int MaximumSkillCallDepth = 8;
        private const string ActiveRunsSessionKey = "ES.AISkillGraph.ActiveRuns.v1";
        private static readonly Dictionary<string, ESAISkillWorkflowRun> Active =
            new Dictionary<string, ESAISkillWorkflowRun>(StringComparer.Ordinal);

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

        public static bool TryApprove(string runId, int generation, bool approved, string comment,
            out string error)
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
                if (!TryApprove(child.runId, callRecord.childApprovalGeneration,
                        approved, comment, out error))
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
                    run.values[step.nodeId] = run.inputs.DeepClone();
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
                    JToken value = ResolveBoundValue(run, step.nodeId);
                    if (!string.IsNullOrWhiteSpace(step.branch.valuePath))
                        value = value?.SelectToken(step.branch.valuePath, false);
                    bool matched = string.Equals(value?.ToString() ?? string.Empty,
                        step.branch.expectedValue ?? string.Empty, step.branch.ignoreCase
                            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                    CompleteStep(run, step.nodeId, "Completed", matched ? "条件命中。" : "进入默认分支。", value);
                    Move(run, step.nodeId, matched ? ESAgentGraphStableIds.SkillMatchedPortKey
                        : ESAgentGraphStableIds.SkillDefaultPortKey);
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
                    run.message = step.approval.title + "：" + step.approval.message;
                    Save(run);
                    return;
                }
                if (step.output != null)
                {
                    JToken output = ResolveBoundValue(run, step.nodeId) ?? JValue.CreateNull();
                    run.values[step.output.outputId] = output.DeepClone();
                    CompleteStep(run, step.nodeId, "Completed", "结构化输出已归集。", output);
                    Finish(run, "Completed", "AISkill 工作流执行完成。", 0);
                    return;
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
                ES.ESAutomationTaskInvocationResult result = ES.ESAutomationFacade.RunTask(
                    new ES.ESAutomationTaskInvocation
                    {
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
                return HandleTaskFailure(run, step, result.status, result.message);
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
            return HandleTaskFailure(run, step, current.status, current.message);
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
                            run.values[step.nodeId] = result;
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
                run.values[step.nodeId] = result;
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
            }
            parent.status = "WaitingApproval";
            parent.message = "子 AISkill 等待人工确认：" + child.message;
        }

        internal static bool IsProjectedChildApprovalCurrent(ESAISkillStepRunRecord callRecord,
            ESAISkillWorkflowRun child)
            => callRecord != null && child != null && child.status == "WaitingApproval"
                && callRecord.childApprovalGeneration > 0
                && callRecord.childApprovalGeneration == child.approvalGeneration;

        private static bool HandleSkillCallFailure(ESAISkillWorkflowRun run,
            ESAISkillExecutionStep step, string status, string message)
        {
            status = NormalizeTaskFailureStatus(status);
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
                        value = ResolveBoundValue(run, targetNodeId);
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
            run.values[step.nodeId] = result.data?.DeepClone() ?? new JObject();
            CompleteStep(run, step.nodeId, "Completed", result.message, result.data);
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

        private static bool HandleTaskFailure(ESAISkillWorkflowRun run, ESAISkillExecutionStep step,
            string status, string message)
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
                record.currentAttemptStartedAtUtc = string.Empty;
                record.retryAvailableAtUtc = DateTimeOffset.UtcNow
                    .AddSeconds(step.task.retryDelaySeconds)
                    .ToString("O", CultureInfo.InvariantCulture);
                Save(run);
                return true;
            }
            CompleteStep(run, step.nodeId, status ?? "Failed", message);
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
                JToken source = ResolveBoundValue(run, step.nodeId);
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
            run.values[step.nodeId] = run.iterationItems[run.iterationIndex].DeepClone();
            run.currentNodeId = run.iterationTaskNodeId;
            Save(run);
            return true;
        }

        private static JToken ResolveBoundValue(ESAISkillWorkflowRun run, string targetNodeId)
        {
            ESAISkillDataBinding[] bindings = run.spec.dataBindings
                .Where(binding => binding.targetNodeId == targetNodeId)
                .OrderBy(binding => binding.edgeId, StringComparer.Ordinal).ToArray();
            if (bindings.Length == 0) return null;
            if (bindings.Length == 1) return run.values[bindings[0].sourceNodeId];
            var values = new JArray();
            foreach (ESAISkillDataBinding binding in bindings)
                values.Add(run.values[binding.sourceNodeId]?.DeepClone() ?? JValue.CreateNull());
            return values;
        }

        private static void Move(ESAISkillWorkflowRun run, string sourceNodeId, string portKey)
        {
            string target = ResolveRoute(run, sourceNodeId, portKey);
            if (string.IsNullOrWhiteSpace(target))
            {
                Fail(run, "控制出口未连接：" + portKey);
                return;
            }
            run.currentNodeId = target;
            Save(run);
        }

        private static string ResolveRoute(ESAISkillWorkflowRun run, string nodeId, string portKey)
            => run.spec.controlEdges.FirstOrDefault(edge => edge.sourceNodeId == nodeId
                && edge.sourcePortKey == portKey)?.targetNodeId ?? string.Empty;

        private static ESAISkillExecutionStep FindStep(ESAISkillWorkflowRun run, string nodeId)
            => run.spec?.steps?.FirstOrDefault(step => step.nodeId == nodeId);

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
                        run.values[step.nodeId] = result;
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
            if (spec == null || !ESGraphIdentity.IsValid(spec.sourceGraphId)
                || !ES.ESAutomationWorkerRegistration.IsSha256(spec.sourceContentSignature))
            {
                error = "执行合同缺少有效 GraphId 或内容签名。";
                return false;
            }
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
                if (run == null || run.schemaVersion != 1 || run.spec == null
                    || !string.Equals(run.graphId, run.spec.sourceGraphId, StringComparison.Ordinal)
                    || !string.Equals(run.contentSignature, run.spec.sourceContentSignature, StringComparison.Ordinal))
                    throw new InvalidDataException("RunRecord 与内嵌执行合同不一致。");
                if (!ES.ESAutomationWorkerRegistration.IsSha256(run.executionSpecHash)
                    || !string.Equals(run.executionSpecHash, ComputeExecutionSpecHash(run.spec),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("RunRecord 的执行合同 Hash 与内嵌 Spec 不一致。");
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
                || run.spec?.steps == null)
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
