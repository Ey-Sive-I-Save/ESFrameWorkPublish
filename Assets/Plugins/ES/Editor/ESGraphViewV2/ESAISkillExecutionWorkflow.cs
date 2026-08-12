using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            if (step.input == null && step.task == null && step.branch == null && step.forEach == null
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
                if (step.input == null && !controls.Any(edge => edge.targetNodeId == step.nodeId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ControlInput",
                        "执行节点缺少控制输入，数据连线不能代替执行顺序。", step.nodeId));
                if ((step.branch != null || step.forEach != null || step.output != null)
                    && !bindings.Any(edge => edge.targetNodeId == step.nodeId))
                    failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ValueInput",
                        "该节点缺少必需值输入。", step.nodeId));
                bool isForEachBody = step.task != null && controls.Any(edge =>
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
                    if (body?.task == null)
                        failures.Add(ESGraphValidationIssue.Error("AISkill.Execution.ForEachBody",
                            "ForEach 的逐项出口必须直接连接一个 Task；循环由协调器内部管理。", step.nodeId));
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
        public string childRunId = string.Empty;
        public int exitCode = -1;
        public string message = string.Empty;
        public string[] diagnostics = Array.Empty<string>();
        public string[] artifacts = Array.Empty<string>();
        public string[] outputHashes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAISkillWorkflowRun
    {
        public int schemaVersion = 1;
        public string runId = Guid.NewGuid().ToString("N");
        public string graphId = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string contentSignature = string.Empty;
        public string skillId = string.Empty;
        public string operatorId = string.Empty;
        public string status = "Running";
        public string currentNodeId = string.Empty;
        public string startedAtUtc = string.Empty;
        public string updatedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string message = string.Empty;
        public int exitCode = -1;
        public int approvalGeneration;
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
            string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            run = new ESAISkillWorkflowRun
            {
                graphId = spec.sourceGraphId,
                sourceAssetGuid = sourceAssetGuid ?? string.Empty,
                contentSignature = spec.sourceContentSignature,
                skillId = spec.skillId,
                operatorId = string.IsNullOrWhiteSpace(operatorId) ? Environment.UserName : operatorId.Trim(),
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
                ES.ESAutomationFacade.CancelRun(step.childRunId, actorId ?? string.Empty, false);
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
                JObject input = JObject.Parse(string.IsNullOrWhiteSpace(step.task.staticInputJson)
                    ? "{}" : step.task.staticInputJson);
                if (!TryApplyTaskInputBindings(run, step, input, out string bindingError))
                    return HandleTaskFailure(run, step, "Failed", bindingError);
                ES.ESAutomationTaskInvocationResult result = ES.ESAutomationFacade.RunTask(
                    new ES.ESAutomationTaskInvocation
                    {
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
                    ES.ESAutomationFacade.CancelRun(record.childRunId, run.operatorId, false);
                    return HandleTaskFailure(run, step, "TimedOut", "工作流步骤超过声明超时。" );
                }
                return true;
            }
            if (IsSuccessfulTaskStatus(current.status))
                return HandleTaskTerminal(run, step, current);
            return HandleTaskFailure(run, step, current.status, current.message);
        }

        private static bool TryApplyTaskInputBindings(ESAISkillWorkflowRun run,
            ESAISkillExecutionStep step, JObject input, out string error)
        {
            foreach (ESAISkillTaskInputBinding binding in step.task.inputBindings
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
                        value = ResolveBoundValue(run, step.nodeId);
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
                    Move(run, step.nodeId, ESAgentGraphStableIds.SkillFailurePortKey);
                    return true;
                }
                if (items.Count > step.forEach.maxItems)
                {
                    Fail(run, "ForEach 输入数量 " + items.Count + " 超过上限 " + step.forEach.maxItems + "。" );
                    return false;
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
            Save(run);
        }

        private static void ResetStepForNextAttempt(ESAISkillWorkflowRun run, string nodeId)
        {
            ESAISkillStepRunRecord record = FindStepRecord(run, nodeId);
            record.status = "Pending";
            record.childRunId = string.Empty;
            record.currentAttemptCount = 0;
            record.currentAttemptStartedAtUtc = string.Empty;
            record.retryAvailableAtUtc = string.Empty;
            record.startedAtUtc = string.Empty;
            record.finishedAtUtc = string.Empty;
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
                        })))
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
                error = string.Empty;
                return true;
            }
            catch (Exception exception) { run = null; error = exception.Message; return false; }
        }

        private static void RestoreActiveRuns()
        {
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
            foreach (string id in ids.Distinct(StringComparer.Ordinal))
            {
                if (!TryLoad(id, out ESAISkillWorkflowRun run, out _) || IsTerminal(run.status))
                    continue;
                if (!TryValidateCurrentSource(run, out string sourceError))
                {
                    run.status = "Blocked";
                    run.message = "恢复已阻断：" + sourceError;
                    run.exitCode = -1;
                    run.finishedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                    try { Save(run); }
                    catch (Exception exception)
                    {
                        Debug.LogError("[ES AISkillGraph] 无法持久化 stale Graph 阻断状态：" + exception.Message);
                    }
                    continue;
                }
                Active[id] = run;
            }
            TrySaveActiveRunIds();
            EnsureUpdateHook();
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
