using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace ES
{
    /// <summary>
    /// 人工 UI、AI Bridge 和未来 CI 共同使用的任务入口。
    /// 它只分发已注册 Endpoint；不接受脚本路径、进程参数或任意文件路径。
    /// </summary>
    public static class ESAutomationFacade
    {
        private static readonly Dictionary<string, IESAutomationTaskEndpoint> endpoints = new Dictionary<string, IESAutomationTaskEndpoint>(StringComparer.Ordinal);

        public static IReadOnlyCollection<IESAutomationTaskEndpoint> Endpoints => endpoints.Values;

        public static void Register(IESAutomationTaskEndpoint endpoint)
        {
            if (endpoint == null || endpoint.Descriptor == null) throw new ArgumentNullException(nameof(endpoint));
            endpoint.Descriptor.Validate();
            if (ESAutomationTaskRegistry.TryGet(endpoint.Descriptor.taskId, endpoint.Descriptor.taskVersion,
                    out ESAutomationTaskContract contract))
            {
                if (!string.Equals(contract.taskId, endpoint.Descriptor.taskId, StringComparison.Ordinal)
                    || contract.version != endpoint.Descriptor.taskVersion)
                    throw new InvalidOperationException("Automation Descriptor 与 TaskContract 身份/版本不一致。");
                if (!string.IsNullOrWhiteSpace(contract.inputSchemaHash)
                    && !string.Equals(contract.inputSchemaHash, endpoint.Descriptor.inputSchemaHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Automation Descriptor 与 TaskContract InputSchemaHash 不一致。");
                if (!(endpoint is IESAutomationContractBoundEndpoint))
                    throw new InvalidOperationException("已注册 TaskContract 的 Endpoint 必须绑定 IESAutomationContractBoundEndpoint。");
            }
            string key = Key(endpoint.Descriptor.taskId, endpoint.Descriptor.taskVersion);
            if (endpoints.ContainsKey(key)) throw new InvalidOperationException("重复注册 Automation Facade Endpoint：" + key);
            endpoints.Add(key, endpoint);
        }

        public static bool TryGetDescriptor(string taskId, int taskVersion, out ESAutomationTaskDescriptor descriptor)
        {
            if (endpoints.TryGetValue(Key(taskId, taskVersion), out IESAutomationTaskEndpoint endpoint))
            {
                descriptor = endpoint.Descriptor;
                return true;
            }
            descriptor = null;
            return false;
        }

        public static List<ESAutomationTaskDescriptor> CopyDescriptors()
        {
            var result = new List<ESAutomationTaskDescriptor>(endpoints.Count);
            foreach (IESAutomationTaskEndpoint endpoint in endpoints.Values) result.Add(endpoint.Descriptor);
            result.Sort((left, right) => string.CompareOrdinal(left.taskId, right.taskId));
            return result;
        }

        public static ESAutomationTaskInvocationResult RunTask(ESAutomationTaskInvocation invocation)
        {
            if (invocation == null) return ESAutomationTaskInvocationResult.Rejected("缺少 Automation 调用请求。");
            if (!string.IsNullOrWhiteSpace(invocation.invocationId)
                && !Guid.TryParseExact(invocation.invocationId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("InvocationId 必须为空或为 N 格式 GUID。");
            if (!endpoints.TryGetValue(Key(invocation.taskId, invocation.taskVersion), out IESAutomationTaskEndpoint endpoint))
                return ESAutomationTaskInvocationResult.Rejected("未注册或不支持的任务：" + invocation.taskId + "@" + invocation.taskVersion);
            if (!ESAutomationTaskRegistry.TryGet(invocation.taskId, invocation.taskVersion,
                    out ESAutomationTaskContract contract))
                return ESAutomationTaskInvocationResult.Rejected("任务缺少受信 TaskContract：" + invocation.taskId + "@" + invocation.taskVersion);
            try
            {
                contract.Validate();
                if (contract.worker == null || !contract.worker.enabled)
                    return ESAutomationTaskInvocationResult.Blocked("TaskContract 的 Worker 未被 C# Editor 启用。");
                if (invocation.fromAi && !endpoint.Descriptor.allowAiInvoke)
                    return ESAutomationTaskInvocationResult.Rejected(
                        "该任务未授权 AI 直接调用：" + endpoint.Descriptor.taskId);
                if (EditorApplication.isPlayingOrWillChangePlaymode
                    && !endpoint.Descriptor.allowInPlayMode)
                    return ESAutomationTaskInvocationResult.Blocked(
                        "该任务未声明允许在 PlayMode 运行：" + endpoint.Descriptor.taskId);
                if (invocation.fromAi
                    && !ESAIBrainCoordinator.TryValidateAuthorization(invocation,
                        out string preflightBrainReason))
                    return ESAutomationTaskInvocationResult.Rejected(preflightBrainReason);
                if (!(endpoint is IESAutomationContractBoundEndpoint bound))
                    return ESAutomationTaskInvocationResult.Rejected("Endpoint 未绑定 TaskContract 执行门禁。");
                ESAutomationInvocationRequirements requirements = bound.DescribeInvocation(invocation);
                if (requirements == null || requirements.worker == null)
                    return ESAutomationTaskInvocationResult.Rejected("Endpoint 未声明本次调用的受信 Worker 身份。");
                if (!SameWorker(contract.worker, requirements.worker))
                    return ESAutomationTaskInvocationResult.Rejected("调用要求的 Worker 身份与 TaskContract 不一致。");
                ESAutomationCapability declared = contract.ResolveCapabilities();
                if ((requirements.requiredCapabilities & ~declared) != ESAutomationCapability.None)
                    return ESAutomationTaskInvocationResult.Rejected("调用要求的能力超出 TaskContract：" + requirements.requiredCapabilities);
                if (contract.capabilityEnvelope != null
                    && !contract.capabilityEnvelope.AllowsInvocation(
                        invocation, requirements.requiredCapabilities, out string capabilityReason))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "CapabilityEnvelope 拒绝本次调用：" + capabilityReason
                        + " required=" + requirements.requiredCapabilities
                        + ", effective=" + contract.capabilityEnvelope.EffectiveCapability());
                if (requirements.dryRun && !contract.supportsDryRun)
                    return ESAutomationTaskInvocationResult.Blocked("该 TaskContract 未声明支持 DryRun。");
                if (!string.IsNullOrWhiteSpace(requirements.inputManifestHash)
                    && !ESAutomationWorkerRegistration.IsSha256(requirements.inputManifestHash))
                    return ESAutomationTaskInvocationResult.Rejected("调用输入 Manifest Hash 无效。");
                bool strictSnapshotBinding = contract.acceptanceCriteria?.freshnessPolicy
                    ?.requireExecutionSnapshotBinding == true;
                if (strictSnapshotBinding && invocation.executionSnapshot == null)
                    return ESAutomationTaskInvocationResult.Blocked(
                        "严格快照绑定合同要求本次 Invocation 携带 ExecutionSnapshot。");
                if (strictSnapshotBinding && invocation.executionSnapshot != null)
                {
                    try
                    {
                        invocation.executionSnapshot.Validate();
                    }
                    catch (Exception snapshotException)
                    {
                        return ESAutomationTaskInvocationResult.Blocked(
                            "严格快照绑定的 ExecutionSnapshot 无效：" + snapshotException.Message);
                    }
                    if (!ESAutomationGovernance.MatchesTaskContract(
                        contract, invocation.executionSnapshot, out string strictContractReason))
                        return ESAutomationTaskInvocationResult.Blocked(
                            "严格快照绑定的 TaskContract 校验失败：" + strictContractReason);
                    if (!ESAutomationGovernance.MatchesInputManifest(
                        requirements.inputManifestHash, invocation.executionSnapshot,
                        out string strictInputReason))
                        return ESAutomationTaskInvocationResult.Blocked(strictInputReason);
                }
                if (requirements.executionSnapshot != null)
                {
                    if (invocation.executionSnapshot == null)
                        return ESAutomationTaskInvocationResult.Blocked("缺少绑定的 ExecutionSnapshot。");
                    if (!ESAutomationGovernance.MatchesSnapshot(
                        requirements.executionSnapshot, invocation.executionSnapshot, out string snapshotReason))
                        return ESAutomationTaskInvocationResult.Blocked("ExecutionSnapshot 校验失败：" + snapshotReason);
                    if (!ESAutomationGovernance.MatchesTaskContract(
                        contract, invocation.executionSnapshot, out string contractReason))
                        return ESAutomationTaskInvocationResult.Blocked("ExecutionSnapshot 合同校验失败：" + contractReason);
                    if (!ESAutomationGovernance.MatchesInputManifest(
                        requirements.inputManifestHash, invocation.executionSnapshot, out string inputReason))
                        return ESAutomationTaskInvocationResult.Blocked(inputReason);
                }
                foreach (string path in requirements.readPaths ?? new List<string>())
                    ESAutomationPathPolicy.EnsureWorkerReadAllowed(path, contract.readRoots);
                foreach (string path in requirements.writePaths ?? new List<string>())
                {
                    if ((requirements.requiredCapabilities & ESAutomationCapability.MaterializeUI) != ESAutomationCapability.None)
                        ESAutomationPathPolicy.EnsureUIWorkerWriteAllowed(path, contract.writeRoots);
                    else
                        ESAutomationPathPolicy.EnsureWorkerWriteAllowed(path, contract.writeRoots);
                }
            }
            catch (Exception exception)
            {
                return ESAutomationTaskInvocationResult.Rejected("Automation Contract 门禁拒绝调用：" + exception.Message);
            }
            // Re-check and consume immediately before execution. Another process may have
            // consumed the bounded grant after the non-consuming preflight above.
            if (invocation.fromAi
                && !ESAIBrainCoordinator.TryConsumeAuthorization(invocation, out string brainReason))
                return ESAutomationTaskInvocationResult.Rejected(brainReason);
            return endpoint.Run(invocation);
        }

        private static bool SameWorker(ESAutomationWorkerRegistration left, ESAutomationWorkerRegistration right)
            => left != null && right != null
                && string.Equals(left.type, right.type, StringComparison.Ordinal)
                && string.Equals(left.workerId, right.workerId, StringComparison.Ordinal)
                && string.Equals(left.version, right.version, StringComparison.Ordinal)
                && string.Equals(left.entrypointHash, right.entrypointHash, StringComparison.OrdinalIgnoreCase);

        public static ESAutomationTaskInvocationResult GetRun(string runId, bool fromAi)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            foreach (IESAutomationTaskEndpoint endpoint in endpoints.Values)
            {
                if (fromAi && !endpoint.Descriptor.allowAiInvoke) continue;
                ESAutomationTaskInvocationResult result = endpoint.GetRun(runId);
                if (!string.Equals(result.status, "NotFound", StringComparison.Ordinal)) return result;
            }
            return ESAutomationTaskInvocationResult.NotFound("未找到该 RunId 的已注册任务记录。");
        }

        public static ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
        {
            if (submission == null) return ESAutomationTaskInvocationResult.Rejected("缺少输入提交。");
            if (!Guid.TryParseExact(submission.runId, "N", out _)) return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            foreach (IESAutomationTaskEndpoint endpoint in endpoints.Values)
            {
                if (submission.fromAi && !endpoint.Descriptor.allowAiInvoke) continue;
                if (EditorApplication.isPlayingOrWillChangePlaymode && !endpoint.Descriptor.allowInPlayMode)
                    continue;
                ESAutomationTaskInvocationResult result = endpoint.SubmitInput(submission);
                if (!string.Equals(result.status, "NotFound", StringComparison.Ordinal)) return result;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return ESAutomationTaskInvocationResult.Blocked("当前 PlayMode 没有声明允许继续输入的自动化任务。");
            return ESAutomationTaskInvocationResult.NotFound("未找到该 RunId 的待输入任务。");
        }

        public static ESAutomationTaskInvocationResult CancelRun(string runId, string actorId, bool fromAi)
        {
            if (!Guid.TryParseExact(runId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            foreach (IESAutomationTaskEndpoint endpoint in endpoints.Values)
            {
                if (!(endpoint is IESAutomationCancellableTaskEndpoint cancellable)) continue;
                if (fromAi && !endpoint.Descriptor.allowAiInvoke) continue;
                ESAutomationTaskInvocationResult result = cancellable.CancelRun(runId, actorId ?? string.Empty);
                if (!string.Equals(result.status, "NotFound", StringComparison.Ordinal)) return result;
            }
            return ESAutomationTaskInvocationResult.NotFound("未找到支持取消的 Automation Run。");
        }

        private static string Key(string taskId, int taskVersion) => (taskId ?? string.Empty) + "@" + taskVersion;
    }

    public interface IESAutomationTaskEndpoint
    {
        ESAutomationTaskDescriptor Descriptor { get; }
        ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation);
        ESAutomationTaskInvocationResult GetRun(string runId);
        ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission);
    }

    /// <summary>
    /// Endpoint 对本次调用声明实际资源要求，供 Facade 与注册 TaskContract 做执行前核对。
    /// </summary>
    public sealed class ESAutomationInvocationRequirements
    {
        public ESAutomationWorkerRegistration worker = new ESAutomationWorkerRegistration();
        public ESAutomationCapability requiredCapabilities;
        public bool dryRun;
        public List<string> readPaths = new List<string>();
        public List<string> writePaths = new List<string>();
        public string inputManifestHash = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
    }

    public interface IESAutomationContractBoundEndpoint
    {
        ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation);
    }

    public interface IESAutomationCancellableTaskEndpoint
    {
        ESAutomationTaskInvocationResult CancelRun(string runId, string actorId);
    }

    [Serializable]
    public sealed class ESAutomationTaskDescriptor
    {
        public string taskId = string.Empty;
        public int taskVersion;
        public string category = string.Empty;
        public string displayName = string.Empty;
        public string summary = string.Empty;
        public bool allowAiInvoke;
        /// <summary>默认 false。PlayMode 任务必须显式声明并自行验证运行时安全边界。</summary>
        public bool allowInPlayMode;
        public string inputSchemaHash = string.Empty;
        public List<ESAutomationTaskPresetDescriptor> presets = new List<ESAutomationTaskPresetDescriptor>();
        /// <summary>
        /// 仅公开已注册、只读的输入描述，供 Center、ESAdvancedDialog 和本机 AI 以相同字段契约驱动任务。
        /// 它不是脚本/路径入口；真正的业务校验仍由 Endpoint 和 Worker 的 SchemaHash 完成。
        /// </summary>
        public List<ESAutomationInputSchemaDescriptor> inputSchemas = new List<ESAutomationInputSchemaDescriptor>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(taskId) || taskVersion < 1 || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Automation TaskDescriptor 缺少稳定身份或显示元数据。");
            if (!string.IsNullOrWhiteSpace(inputSchemaHash) && !ESAutomationWorkerRegistration.IsSha256(inputSchemaHash))
                throw new InvalidOperationException("Automation TaskDescriptor InputSchemaHash 无效。");
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAutomationTaskPresetDescriptor preset in presets ?? new List<ESAutomationTaskPresetDescriptor>())
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.presetId) || string.IsNullOrWhiteSpace(preset.label) || !presetIds.Add(preset.presetId))
                    throw new InvalidOperationException("Automation TaskDescriptor 包含无效或重复 Preset。");
            }
            var schemaIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAutomationInputSchemaDescriptor schema in inputSchemas ?? new List<ESAutomationInputSchemaDescriptor>())
            {
                if (schema == null) throw new InvalidOperationException("Automation TaskDescriptor 包含空输入 Schema。");
                schema.Validate();
                if (!schemaIds.Add(schema.stepId)) throw new InvalidOperationException("Automation TaskDescriptor 包含重复输入 StepId：" + schema.stepId);
            }
        }

        public bool TryGetInputSchema(string stepId, string schemaHash, out ESAutomationInputSchemaDescriptor schema)
        {
            foreach (ESAutomationInputSchemaDescriptor item in inputSchemas ?? new List<ESAutomationInputSchemaDescriptor>())
            {
                if (item != null && string.Equals(item.stepId, stepId, StringComparison.Ordinal)
                    && string.Equals(item.schemaHash, schemaHash, StringComparison.OrdinalIgnoreCase))
                {
                    schema = item;
                    return true;
                }
            }
            schema = null;
            return false;
        }
    }

    [Serializable]
    public sealed class ESAutomationTaskPresetDescriptor
    {
        public string presetId = string.Empty;
        public string label = string.Empty;
        public string summary = string.Empty;
    }

    /// <summary>
    /// Worker 检查点可请求的类型化输入 Schema。所有字段在 C# 中预注册，AI 只能读取并提交这些字段；
    /// 它不能借此指定 Python 脚本、解释器、命令行、输出路径或未注册的任务能力。
    /// </summary>
    [Serializable]
    public sealed class ESAutomationInputSchemaDescriptor
    {
        public string stepId = string.Empty;
        public string schemaHash = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public List<ESAutomationInputFieldDescriptor> fields = new List<ESAutomationInputFieldDescriptor>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(stepId) || string.IsNullOrWhiteSpace(title) || !ESAutomationWorkerRegistration.IsSha256(schemaHash))
                throw new InvalidOperationException("Automation 输入 Schema 缺少 StepId、标题或有效 SchemaHash。");
            if (fields == null || fields.Count == 0) throw new InvalidOperationException("Automation 输入 Schema 必须声明至少一个字段。");
            var fieldIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAutomationInputFieldDescriptor field in fields)
            {
                if (field == null) throw new InvalidOperationException("Automation 输入 Schema 包含空字段。");
                field.Validate();
                if (!fieldIds.Add(field.fieldId)) throw new InvalidOperationException("Automation 输入 Schema 包含重复字段：" + field.fieldId);
            }
        }
    }

    [Serializable]
    public sealed class ESAutomationInputFieldDescriptor
    {
        public string fieldId = string.Empty;
        public string label = string.Empty;
        public string description = string.Empty;
        /// <summary>目前只允许 Boolean、Choice、Integer 三种无副作用的显式输入。</summary>
        public string valueType = string.Empty;
        public object defaultValue;
        public int minimumInteger;
        public int maximumInteger;
        public List<ESAutomationInputChoiceDescriptor> choices = new List<ESAutomationInputChoiceDescriptor>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(fieldId) || string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException("Automation 输入字段缺少稳定 FieldId 或显示标签。");
            switch (valueType)
            {
                case "Boolean":
                    if (!(defaultValue is bool)) throw new InvalidOperationException("Boolean 输入字段必须声明 bool 默认值：" + fieldId);
                    if (choices != null && choices.Count > 0) throw new InvalidOperationException("Boolean 输入字段不得声明 Choice：" + fieldId);
                    break;
                case "Integer":
                    if (!(defaultValue is int) || minimumInteger > maximumInteger)
                        throw new InvalidOperationException("Integer 输入字段必须声明合法范围与 int 默认值：" + fieldId);
                    int integerValue = (int)defaultValue;
                    if (integerValue < minimumInteger || integerValue > maximumInteger)
                        throw new InvalidOperationException("Integer 输入字段默认值超出范围：" + fieldId);
                    if (choices != null && choices.Count > 0) throw new InvalidOperationException("Integer 输入字段不得声明 Choice：" + fieldId);
                    break;
                case "Choice":
                    if (!(defaultValue is string defaultCode) || choices == null || choices.Count == 0)
                        throw new InvalidOperationException("Choice 输入字段必须声明 string 默认值与选项：" + fieldId);
                    bool matchedDefault = false;
                    var choiceCodes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (ESAutomationInputChoiceDescriptor choice in choices)
                    {
                        if (choice == null) throw new InvalidOperationException("Choice 输入字段包含空选项：" + fieldId);
                        choice.Validate();
                        if (!choiceCodes.Add(choice.code)) throw new InvalidOperationException("Choice 输入字段包含重复选项：" + fieldId);
                        if (choice.code == defaultCode) matchedDefault = true;
                    }
                    if (!matchedDefault) throw new InvalidOperationException("Choice 输入字段默认值未在选项中注册：" + fieldId);
                    break;
                default:
                    throw new InvalidOperationException("Automation 输入字段类型未注册：" + valueType);
            }
        }
    }

    [Serializable]
    public sealed class ESAutomationInputChoiceDescriptor
    {
        public string code = string.Empty;
        public string label = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException("Automation 输入选项缺少稳定 code 或标签。");
        }
    }

    public sealed class ESAutomationTaskInvocation
    {
        /// <summary>
        /// 调用方可在首次派发前持久化的稳定调用身份。Endpoint 必须以它去重同一调用，
        /// 并拒绝同一身份对应不同输入；为空时保持普通交互入口的原有行为。
        /// </summary>
        public string invocationId = string.Empty;
        /// <summary>
        /// AI 调用的 AIBrain 计划指纹。Facade 会原子消费与完整 Invocation 绑定的有界复用许可。
        /// </summary>
        public string brainPlanHash = string.Empty;
        /// <summary>副作用重试和恢复使用的稳定幂等键；旧调用可为空以保持兼容。</summary>
        public string idempotencyKey = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
        public string taskId = string.Empty;
        public int taskVersion;
        public string preset = string.Empty;
        public JObject input = new JObject();
        public bool fromAi;
        public bool dryRun;
        public string actorId = string.Empty;
        // Internal authorization bindings are never accepted from Bridge JSON or persisted with task input.
        internal string authorizationClass = string.Empty;
        internal string authorizationBudgetClass = string.Empty;
        internal string authorizationHostId = string.Empty;
        internal string userInstructionHash = string.Empty;
    }

    public sealed class ESAutomationTaskInputSubmission
    {
        public string runId = string.Empty;
        public int requestGeneration;
        public string stepId = string.Empty;
        public string schemaHash = string.Empty;
        public bool accepted;
        public JObject values = new JObject();
        public bool fromAi;
        public string actorId = string.Empty;
    }

    public sealed class ESAutomationTaskInvocationResult
    {
        public string status = "Rejected";
        public string message = string.Empty;
        public string runId = string.Empty;
        public JObject data = new JObject();

        public static ESAutomationTaskInvocationResult Accepted(string message, string runId, JObject data = null)
            => new ESAutomationTaskInvocationResult { status = "Accepted", message = message ?? string.Empty, runId = runId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationTaskInvocationResult Starting(string message, string runId, JObject data = null)
            => new ESAutomationTaskInvocationResult { status = "Starting", message = message ?? string.Empty, runId = runId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationTaskInvocationResult Completed(string message, string runId, JObject data = null)
            => new ESAutomationTaskInvocationResult { status = "Completed", message = message ?? string.Empty, runId = runId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationTaskInvocationResult Blocked(string message, string runId = "", JObject data = null)
            => new ESAutomationTaskInvocationResult { status = "Blocked", message = message ?? string.Empty, runId = runId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationTaskInvocationResult Rejected(string message)
            => new ESAutomationTaskInvocationResult { status = "Rejected", message = message ?? string.Empty };

        public static ESAutomationTaskInvocationResult Failed(string message, string runId = "")
            => new ESAutomationTaskInvocationResult { status = "Failed", message = message ?? string.Empty, runId = runId ?? string.Empty };

        public static ESAutomationTaskInvocationResult NotFound(string message)
            => new ESAutomationTaskInvocationResult { status = "NotFound", message = message ?? string.Empty };
    }

    /// <summary>
    /// AI 内容补充的领域扩展点。Automation 不直接创建 Assets；领域端点自行验证规范并决定是否创建“草稿/提案”。
    /// 未注册 ContentType 一律拒绝。
    /// </summary>
    public static class ESAutomationContentIngress
    {
        private static readonly Dictionary<string, IESAutomationContentProposalEndpoint> endpoints = new Dictionary<string, IESAutomationContentProposalEndpoint>(StringComparer.Ordinal);

        public static void Register(IESAutomationContentProposalEndpoint endpoint)
        {
            if (endpoint == null || endpoint.Descriptor == null) throw new ArgumentNullException(nameof(endpoint));
            endpoint.Descriptor.Validate();
            string key = Key(endpoint.Descriptor.contentType, endpoint.Descriptor.contentVersion);
            if (endpoints.ContainsKey(key)) throw new InvalidOperationException("重复注册 AI 内容提案入口：" + key);
            endpoints.Add(key, endpoint);
        }

        public static List<ESAutomationContentDescriptor> CopyDescriptors()
        {
            var result = new List<ESAutomationContentDescriptor>(endpoints.Count);
            foreach (IESAutomationContentProposalEndpoint endpoint in endpoints.Values) result.Add(endpoint.Descriptor);
            result.Sort((left, right) => string.CompareOrdinal(left.contentType, right.contentType));
            return result;
        }

        public static ESAutomationContentProposalResult Submit(ESAutomationContentProposal proposal)
        {
            if (proposal == null) return ESAutomationContentProposalResult.Rejected("缺少内容提案。");
            if (!endpoints.TryGetValue(Key(proposal.contentType, proposal.contentVersion), out IESAutomationContentProposalEndpoint endpoint))
                return ESAutomationContentProposalResult.Rejected("未注册内容类型；AI 不能直接创建该内容：" + proposal.contentType + "@" + proposal.contentVersion);
            if (!endpoint.Descriptor.allowAiProposal)
                return ESAutomationContentProposalResult.Rejected("该内容类型未授权 AI 提案：" + proposal.contentType);
            if (!string.Equals(endpoint.Descriptor.schemaHash, proposal.schemaHash, StringComparison.OrdinalIgnoreCase))
                return ESAutomationContentProposalResult.Rejected("内容提案 SchemaHash 与已注册领域规范不匹配。");
            return endpoint.SubmitProposal(proposal);
        }

        private static string Key(string contentType, int contentVersion) => (contentType ?? string.Empty) + "@" + contentVersion;
    }

    public interface IESAutomationContentProposalEndpoint
    {
        ESAutomationContentDescriptor Descriptor { get; }
        ESAutomationContentProposalResult SubmitProposal(ESAutomationContentProposal proposal);
    }

    [Serializable]
    public sealed class ESAutomationContentDescriptor
    {
        public string contentType = string.Empty;
        public int contentVersion;
        public string category = string.Empty;
        public string displayName = string.Empty;
        public string schemaHash = string.Empty;
        public bool allowAiProposal;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(contentType) || contentVersion < 1 || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("AI 内容描述缺少稳定身份或显示元数据。");
            if (!ESAutomationWorkerRegistration.IsSha256(schemaHash)) throw new InvalidOperationException("AI 内容描述必须声明 SchemaHash。");
        }
    }

    public sealed class ESAutomationContentProposal
    {
        public string requestId = string.Empty;
        public string actorId = string.Empty;
        public string contentType = string.Empty;
        public int contentVersion;
        public string schemaHash = string.Empty;
        public JObject payload = new JObject();
    }

    public sealed class ESAutomationContentProposalResult
    {
        public string status = "Rejected";
        public string message = string.Empty;
        public string receiptId = string.Empty;
        public JObject data = new JObject();

        public static ESAutomationContentProposalResult Accepted(string message, string receiptId, JObject data = null)
            => new ESAutomationContentProposalResult { status = "Accepted", message = message ?? string.Empty, receiptId = receiptId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationContentProposalResult Rejected(string message)
            => new ESAutomationContentProposalResult { status = "Rejected", message = message ?? string.Empty };
    }
}
