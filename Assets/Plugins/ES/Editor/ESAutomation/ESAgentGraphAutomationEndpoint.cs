using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Stable Graph 的唯一 AI 派发入口。Graph 只提交已经写入候选目录的不可变 Prompt，
    /// AutomationCenter 负责 RunId、输入指纹、受控发送和可恢复证据。
    /// </summary>
    internal static class ESAgentGraphAutomation
    {
        internal const string GenerateTaskId = "es.agent.generate";
        internal const string UseTaskId = "es.agent.use";
        private const int TaskVersion = 1;
        private const string WorkerType = "Other";
        private const string WorkerId = "es.agent.graph.dispatch";
        private const string WorkerVersion = "2.0.0";
        private const int MaximumPromptBytes = 1024 * 1024;
        private const string EnvelopeFileName = "agent-graph-dispatch.json";
        private const string RunRecordFileName = "run-record.json";
        private const string ReceiptFileName = "dispatch-receipt.json";
        private static bool initialized;
        private static string entrypointHash;
        private static string EntrypointHash => entrypointHash ??= ComputeEntrypointHash();

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                ESCmdAgentPromptLifecycle.Changed -= HandlePromptLifecycle;
                ESCmdAgentPromptLifecycle.Changed += HandlePromptLifecycle;
                RegisterTask(GenerateTaskId, "生成 Graph AI 候选", "将已校验 Graph Prompt 派发到受控 AI 会话，结果写入候选目录。",
                    "candidate");
                RegisterTask(UseTaskId, "执行 Graph AI 单次任务", "将已校验 Graph Prompt 派发到受控 AI 会话，保留完整 Run 证据。",
                    "use");
            }
            catch (Exception exception)
            {
                initialized = false;
                Debug.LogError("[ESAutomation] Graph AI Endpoint 注册失败：" + exception.Message);
            }
        }

        internal static ESAutomationTaskInvocationResult Dispatch(string taskId, string requestId,
            string requestDirectory, string graphId, string contentSignature, string operationKind,
            string prompt, ESGraphRiskAcceptance riskAcceptance, string actorId, bool dryRun = false)
        {
            InitializeForEditor();
            bool isCandidate = string.Equals(taskId, GenerateTaskId, StringComparison.Ordinal);
            JObject input = new JObject
            {
                ["requestId"] = requestId ?? string.Empty,
                ["requestDirectory"] = requestDirectory ?? string.Empty,
                ["graphId"] = graphId ?? string.Empty,
                ["contentSignature"] = contentSignature ?? string.Empty,
                ["operationKind"] = operationKind ?? string.Empty,
                ["prompt"] = prompt ?? string.Empty,
                ["riskAcceptance"] = riskAcceptance == null
                    ? JValue.CreateNull() : JObject.FromObject(riskAcceptance),
            };
            return ESAIBrainCoordinator.Run(new ESAIBrainRequest
            {
                objective = isCandidate
                    ? "生成已校验 Stable Graph 的 Agent Artifact 候选。"
                    : "执行已校验 Stable Graph 的单次 AI 任务。",
                routeKeys = new List<string> { "aibrain", "orchestration" },
                commandId = isCandidate ? "agent-artifact.candidate" : "graph.single-use.execute",
                skillNames = isCandidate
                    ? new List<string> { "es-generate-agent-artifacts" } : new List<string>(),
                workflow = new ESAIBrainWorkflowAuthority
                {
                    workflowId = string.IsNullOrWhiteSpace(graphId) ? "graph.unknown" : graphId,
                    contentHash = contentSignature ?? string.Empty,
                },
                taskId = taskId,
                taskVersion = TaskVersion,
                input = input,
                actorId = string.IsNullOrWhiteSpace(actorId) ? "editor.user" : actorId,
                fromAi = false,
                dryRun = dryRun,
                invocationId = CreateStableInvocationId(taskId, requestId, contentSignature),
            });
        }

        private static string CreateStableInvocationId(string taskId, string requestId,
            string contentSignature)
        {
            byte[] source = Encoding.UTF8.GetBytes((taskId ?? string.Empty) + "\n"
                + (requestId ?? string.Empty) + "\n" + (contentSignature ?? string.Empty));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(source);
                byte[] guidBytes = new byte[16];
                Buffer.BlockCopy(hash, 0, guidBytes, 0, guidBytes.Length);
                return new Guid(guidBytes).ToString("N");
            }
        }

        private static void RegisterTask(string taskId, string displayName, string summary, string operationKind)
        {
            if (!ESAutomationTaskRegistry.TryGet(taskId, TaskVersion, out ESAutomationTaskContract existing))
            {
                ESAutomationTaskRegistry.Register(new ESAutomationTaskContract
                {
                    taskId = taskId,
                    version = TaskVersion,
                    worker = new ESAutomationWorkerRegistration
                    {
                        type = WorkerType,
                        workerId = WorkerId,
                        version = WorkerVersion,
                        entrypointHash = EntrypointHash,
                        enabled = true,
                    },
                    inputs = new List<string> { EnvelopeFileName },
                    readRoots = new List<string> { "ES/Automation/Candidates" },
                    writeRoots = new List<string> { "ES/Automation/Temp" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteTemp" },
                    timeoutSeconds = 600,
                    supportsDryRun = true,
                    outputs = new List<string> { RunRecordFileName, ReceiptFileName },
                });
            }
            else if (existing.worker == null || existing.worker.workerId != WorkerId
                || !string.Equals(existing.worker.entrypointHash, EntrypointHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("已有 Graph AI TaskContract 与受信身份不一致：" + taskId);
            }

            if (!ESAutomationFacade.TryGetDescriptor(taskId, TaskVersion, out _))
            {
                ESAutomationFacade.Register(new GraphEndpoint(new ESAutomationTaskDescriptor
                {
                    taskId = taskId,
                    taskVersion = TaskVersion,
                    category = "Graph/AI",
                    displayName = displayName,
                    summary = summary,
                    allowAiInvoke = false,
                    allowInPlayMode = false,
                    presets = new List<ESAutomationTaskPresetDescriptor>
                    {
                        new ESAutomationTaskPresetDescriptor
                        {
                            presetId = operationKind,
                            label = operationKind == "candidate" ? "生成候选" : "单次执行",
                            summary = summary,
                        }
                    }
                }));
            }
        }

        private sealed class GraphEndpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint,
            IESAutomationCancellableTaskEndpoint
        {
            public GraphEndpoint(ESAutomationTaskDescriptor descriptor) { Descriptor = descriptor; }
            public ESAutomationTaskDescriptor Descriptor { get; }

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
            {
                JObject input = invocation?.input ?? throw new InvalidOperationException("缺少 Graph AI 输入。");
                string requestDirectory = input.Value<string>("requestDirectory") ?? string.Empty;
                return new ESAutomationInvocationRequirements
                {
                    worker = CreateWorkerRegistration(),
                    requiredCapabilities = string.IsNullOrWhiteSpace(requestDirectory)
                        ? ESAutomationCapability.WriteTemp
                        : ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteTemp,
                    dryRun = invocation.dryRun,
                    readPaths = string.IsNullOrWhiteSpace(requestDirectory)
                        ? new List<string>() : new List<string> { requestDirectory },
                    writePaths = new List<string> { ESAutomationPathPolicy.TempRoot },
                    inputManifestHash = ComputeInvocationHash(input),
                };
            }

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                string runId = string.IsNullOrWhiteSpace(invocation?.invocationId)
                    ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                try
                {
                    JObject input = invocation?.input;
                    if (input == null) return ESAutomationTaskInvocationResult.Rejected("缺少 Graph AI 输入。");
                    string invocationHash = ComputeStableInvocationHash(invocation);
                    string existingRecordPath = Path.Combine(ESAutomationPathPolicy.TempRoot,
                        runId, RunRecordFileName);
                    if (File.Exists(existingRecordPath))
                    {
                        ESAutomationRunRecord existing = ReadRecord(existingRecordPath);
                        if (!string.Equals(existing.taskId, Descriptor.taskId, StringComparison.Ordinal)
                            || existing.taskVersion != Descriptor.taskVersion
                            || !string.Equals(existing.invocationHash, invocationHash,
                                StringComparison.OrdinalIgnoreCase))
                            return ESAutomationTaskInvocationResult.Rejected(
                                "InvocationId 已绑定其他任务或输入，拒绝重复派发。");
                        return GetRun(runId);
                    }
                    if (Directory.Exists(Path.Combine(ESAutomationPathPolicy.TempRoot, runId)))
                        return ESAutomationTaskInvocationResult.Rejected(
                            "InvocationId 对应目录已存在但缺少有效 RunRecord，拒绝猜测恢复。");
                    string prompt = input.Value<string>("prompt") ?? string.Empty;
                    string graphId = input.Value<string>("graphId") ?? string.Empty;
                    string signature = input.Value<string>("contentSignature") ?? string.Empty;
                    string requestDirectory = input.Value<string>("requestDirectory") ?? string.Empty;
                    string requestId = input.Value<string>("requestId") ?? string.Empty;
                    string operationKind = input.Value<string>("operationKind") ?? string.Empty;
                    ESGraphRiskAcceptance riskAcceptance = input["riskAcceptance"] == null
                        || input["riskAcceptance"].Type == JTokenType.Null
                        ? null : input["riskAcceptance"].ToObject<ESGraphRiskAcceptance>();
                    if (string.IsNullOrWhiteSpace(prompt)
                        || Encoding.UTF8.GetByteCount(prompt) > MaximumPromptBytes)
                        return ESAutomationTaskInvocationResult.Rejected("Graph AI Prompt 不能为空且 UTF-8 编码后不得超过 1 MiB。");
                    if (!IsSafeRequestId(requestId))
                        return ESAutomationTaskInvocationResult.Rejected("Graph AI RequestId 格式无效。");
                    if (Descriptor.taskId == GenerateTaskId
                        && (!string.Equals(operationKind, "candidate", StringComparison.Ordinal)
                            || string.IsNullOrWhiteSpace(requestDirectory)))
                        return ESAutomationTaskInvocationResult.Rejected(
                            "候选生成任务必须使用 candidate 操作并绑定候选请求目录。");
                    if (Descriptor.taskId == UseTaskId
                        && ((operationKind != "immediate" && operationKind != "single-use")
                            || !string.IsNullOrWhiteSpace(requestDirectory)))
                        return ESAutomationTaskInvocationResult.Rejected(
                            "单次使用任务只能使用 immediate 或 single-use，且不得冒充候选请求。");
                    if (!ESGraphIdentity.IsValid(graphId))
                        return ESAutomationTaskInvocationResult.Rejected("GraphId 无效，拒绝跨图派发。");
                    if (!IsSha256(signature))
                        return ESAutomationTaskInvocationResult.Rejected("Graph 内容签名无效，拒绝派发。");
                    if (riskAcceptance != null
                        && !riskAcceptance.TryValidateStored(graphId, signature, out string riskError))
                        return ESAutomationTaskInvocationResult.Rejected("Graph 风险确认无效：" + riskError);
                    if (riskAcceptance != null
                        && !string.Equals(riskAcceptance.acceptedBy, invocation.actorId,
                            StringComparison.Ordinal))
                        return ESAutomationTaskInvocationResult.Rejected(
                            "Graph 风险确认操作者与本次派发操作者不一致。");
                    if (!string.IsNullOrWhiteSpace(requestDirectory))
                    {
                        if (!ESAgentArtifactGenerationWorkspace.TryReadRequest(requestDirectory,
                            out ESAgentArtifactGenerationRequest request, out string requestError))
                            return ESAutomationTaskInvocationResult.Rejected(requestError);
                        string normalizedRequestDirectory = NormalizeProjectPath(requestDirectory);
                        if (!string.Equals(request.requestId, requestId, StringComparison.Ordinal)
                            || !string.Equals(NormalizeProjectPath(request.requestDirectory),
                                normalizedRequestDirectory, StringComparison.Ordinal)
                            || !string.Equals(NormalizeProjectPath(request.candidateDirectory),
                                normalizedRequestDirectory + "/candidate", StringComparison.Ordinal))
                            return ESAutomationTaskInvocationResult.Rejected(
                                "候选请求的 RequestId 或目录合同与派发输入不一致。");
                        if (!string.Equals(request.spec.sourceGraphId, graphId, StringComparison.Ordinal)
                            || !string.Equals(request.spec.SourceContentSignature, signature, StringComparison.Ordinal))
                            return ESAutomationTaskInvocationResult.Rejected("候选请求与当前 Graph 身份或内容签名不匹配。");
                        if (!ESAgentGenerationIntentValidator.TryValidate(request.spec,
                                out string requestIntentError))
                            return ESAutomationTaskInvocationResult.Rejected(
                                "候选请求的开发意图合同无效：" + requestIntentError);
                        if (!ESAgentGenerationRiskValidator.TryValidate(request.spec, out string requestRiskError))
                            return ESAutomationTaskInvocationResult.Rejected("候选请求的风险合同无效：" + requestRiskError);
                        if ((request.spec.riskAcceptance == null) != (riskAcceptance == null)
                            || request.spec.riskAcceptance != null
                            && !request.spec.riskAcceptance.SameAs(riskAcceptance))
                            return ESAutomationTaskInvocationResult.Rejected("派发输入与候选请求的风险确认不一致。");
                        string expectedPrompt = ESAgentArtifactGenerationWorkspace.BuildPrompt(request);
                        if (!string.Equals(prompt, expectedPrompt, StringComparison.Ordinal))
                            return ESAutomationTaskInvocationResult.Rejected(
                                "候选请求内容与待派发 Prompt 不一致，请重新生成请求。");
                    }

                    string runDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, runId);
                    ESAutomationPathPolicy.EnsureWorkerDirectory(runDirectory, new[] { ESAutomationPathPolicy.TempRoot });
                    string envelopePath = Path.Combine(runDirectory, EnvelopeFileName);
                    string recordPath = Path.Combine(runDirectory, RunRecordFileName);
                    GraphDispatchEnvelope envelope = new GraphDispatchEnvelope
                    {
                        runId = runId, requestId = requestId, graphId = graphId,
                        contentSignature = signature, requestDirectory = requestDirectory,
                        operationKind = operationKind, prompt = prompt, riskAcceptance = riskAcceptance,
                    };
                    var record = new ESAutomationRunRecord
                    {
                        runId = runId,
                        taskId = Descriptor.taskId,
                        taskVersion = Descriptor.taskVersion,
                        operatorId = invocation.actorId ?? "editor.user",
                        workerType = WorkerType,
                        workerId = WorkerId,
                        workerVersion = WorkerVersion,
                        entrypointHash = EntrypointHash,
                        gitCommit = ESAutomationSourceState.GetCurrentGitCommit(),
                        inputManifestHash = ComputeInvocationHash(input),
                        invocationHash = invocationHash,
                        riskPolicyVersion = riskAcceptance?.policyVersion ?? 0,
                        riskAcceptanceHash = riskAcceptance?.acceptanceHash ?? string.Empty,
                        riskAcceptedAtUtc = riskAcceptance?.acceptedAtUtc ?? string.Empty,
                        riskAcceptedBy = riskAcceptance?.acceptedBy ?? string.Empty,
                        acceptedRiskCodes = new List<string>(riskAcceptance?.issueCodes ?? Array.Empty<string>()),
                        startedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        lastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        status = ESAutomationRunStatus.Created,
                        findings = new List<string> { "GraphId=" + graphId, "ContentSignature=" + signature, "Operation=" + operationKind }
                    };
                    ESAutomationPathPolicy.WriteWorkerTextAtomic(envelopePath,
                        JsonConvert.SerializeObject(envelope, Formatting.Indented), new[] { ESAutomationPathPolicy.TempRoot });
                    AddOutput(record, envelopePath);
                    WriteRecord(recordPath, record);

                    if (invocation.dryRun)
                    {
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.DryRun);
                        record.exitCode = 0;
                        WriteRecord(recordPath, record);
                        return ESAutomationTaskInvocationResult.Completed("Graph AI DryRun 已完成，未启动受控会话。", runId,
                            new JObject { ["runRecordPath"] = ToProjectRelative(recordPath), ["inputManifestHash"] = record.inputManifestHash });
                    }

                    ESCmdAgentPromptDispatchResult dispatch = ESCmdAgentWindow.OpenAndSendPromptWithReceipt(
                        prompt, runId, ResolveTimeoutSeconds(Descriptor.taskId, Descriptor.taskVersion));
                    record.sessionId = dispatch.SessionId;
                    record.messageId = dispatch.MessageId;
                    record.operationDirectory = dispatch.OperationDirectory;
                    record.startedAtUtc = string.IsNullOrWhiteSpace(dispatch.StartedAtUtc)
                        ? record.startedAtUtc : dispatch.StartedAtUtc;
                    if (dispatch.IsStarting)
                    {
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Starting);
                        record.findings.Add("已提交受管会话请求；等待精确上下文或消息状态回执，不把启动器 PID 当作 AI 接收证据。");
                    }
                    else if (dispatch.State == ESCmdAgentPromptDispatchState.HeldForUser)
                    {
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Blocked);
                        record.errors.Add(dispatch.Message ?? "受控会话等待人工发送。");
                    }
                    else
                    {
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                        record.errors.Add(dispatch.Message ?? "受控会话未能创建。");
                    }
                    WriteRecord(recordPath, record);
                    JObject data = new JObject
                    {
                        ["runRecordPath"] = ToProjectRelative(recordPath),
                        ["inputManifestHash"] = record.inputManifestHash,
                        ["dispatchState"] = dispatch.State.ToString(),
                        ["acceptancePending"] = dispatch.IsStarting,
                    };
                    if (dispatch.IsStarting)
                        return ESAutomationTaskInvocationResult.Starting("Graph AI Prompt 已启动受控会话，但尚未收到 Codex 接收回执。RunId：" + runId + "。", runId, data);
                    if (dispatch.State == ESCmdAgentPromptDispatchState.HeldForUser)
                        return ESAutomationTaskInvocationResult.Blocked("Graph AI Prompt 已保存为草稿，等待人工发送。RunId：" + runId + "。", runId, data);
                    return ESAutomationTaskInvocationResult.Failed("Graph AI Prompt 派发失败。RunId：" + runId + "。" + (dispatch.Message ?? string.Empty), runId);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    TryFinalizeFailure(runId, exception.Message);
                    return ESAutomationTaskInvocationResult.Failed("Graph AI Automation 失败：" + exception.Message, runId);
                }
            }

            public ESAutomationTaskInvocationResult GetRun(string runId)
            {
                if (!Guid.TryParseExact(runId, "N", out _)) return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
                string path = Path.Combine(ESAutomationPathPolicy.TempRoot, runId, RunRecordFileName);
                if (!File.Exists(path)) return ESAutomationTaskInvocationResult.NotFound("未找到 Graph AI RunRecord。");
                try
                {
                    ESAutomationRunRecord record = ReadRecord(path);
                    string status = ToInvocationStatus(record.status);
                    return new ESAutomationTaskInvocationResult
                    {
                        status = status,
                        runId = runId,
                        message = "Graph AI RunRecord：" + record.status,
                        data = JObject.FromObject(record)
                    };
                }
                catch (Exception exception) { return ESAutomationTaskInvocationResult.Failed("读取 Graph AI RunRecord 失败：" + exception.Message, runId); }
            }

            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.NotFound("Graph AI 派发任务不接受额外交互输入。");

            public ESAutomationTaskInvocationResult CancelRun(string runId, string actorId)
            {
                string path = Path.Combine(ESAutomationPathPolicy.TempRoot, runId, RunRecordFileName);
                if (!File.Exists(path)) return ESAutomationTaskInvocationResult.NotFound("未找到 Graph AI RunRecord。");
                try
                {
                    ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                        File.ReadAllText(path, new UTF8Encoding(false, true)));
                    if (record == null) return ESAutomationTaskInvocationResult.Failed("Graph AI RunRecord 为空。", runId);
                    NormalizeRecordCollections(record);
                    if (ESAutomationRunStatus.IsTerminal(record.status))
                        return GetRun(runId);
                    if (!ESCmdAgentWindow.TryCancelAutomationRun(runId, out string message))
                        return ESAutomationTaskInvocationResult.Failed("Graph AI 取消失败：" + message, runId);
                    record.findings.Add("取消请求者=" + (string.IsNullOrWhiteSpace(actorId) ? "editor.user" : actorId));
                    record.lastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                    WriteRecord(path, record);
                    return ESAutomationTaskInvocationResult.Accepted(message, runId);
                }
                catch (Exception exception)
                {
                    return ESAutomationTaskInvocationResult.Failed("Graph AI 取消失败：" + exception.Message, runId);
                }
            }
        }

        private static void HandlePromptLifecycle(ESCmdAgentPromptLifecycleEvent lifecycleEvent)
        {
            if (string.IsNullOrWhiteSpace(lifecycleEvent.CorrelationId)
                || !Guid.TryParseExact(lifecycleEvent.CorrelationId, "N", out _)) return;
            string runDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, lifecycleEvent.CorrelationId);
            string recordPath = Path.Combine(runDirectory, RunRecordFileName);
            if (!File.Exists(recordPath)) return;
            try
            {
                ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                    File.ReadAllText(recordPath, new UTF8Encoding(false, true)));
                if (record == null) return;
                NormalizeRecordCollections(record);
                if (!TryApplyManagedLifecycleIdentity(record, lifecycleEvent, out string identityError))
                {
                    record.errors.Add(identityError);
                    if (!ESAutomationRunStatus.IsTerminal(record.status)
                        && ESAutomationRunStatus.TryTransition(record.status, ESAutomationRunStatus.Failed))
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                    WriteRecord(recordPath, record);
                    return;
                }
                if (lifecycleEvent.State == ESCmdAgentPromptLifecycleState.Accepted)
                {
                    if (record.status == ESAutomationRunStatus.Starting)
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Accepted);
                    string acceptanceFinding = "Codex 接收事件：" + lifecycleEvent.EventType;
                    if (!record.findings.Contains(acceptanceFinding)) record.findings.Add(acceptanceFinding);
                    WriteDispatchReceipt(runDirectory, lifecycleEvent, record);
                }
                else if (lifecycleEvent.State == ESCmdAgentPromptLifecycleState.Running)
                {
                    bool runningIsFirstAcceptance = record.status == ESAutomationRunStatus.Starting;
                    if (record.status == ESAutomationRunStatus.Accepted || runningIsFirstAcceptance)
                        ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Running);
                    if (runningIsFirstAcceptance)
                    {
                        string acceptanceFinding = "Codex 运行事件直接确认接收：" + lifecycleEvent.EventType;
                        if (!record.findings.Contains(acceptanceFinding)) record.findings.Add(acceptanceFinding);
                        WriteDispatchReceipt(runDirectory, lifecycleEvent, record);
                    }
                }
                else
                {
                    string next = lifecycleEvent.State == ESCmdAgentPromptLifecycleState.Completed
                        ? ESAutomationRunStatus.Completed
                        : lifecycleEvent.State == ESCmdAgentPromptLifecycleState.Cancelled
                            ? ESAutomationRunStatus.Cancelled
                            : lifecycleEvent.State == ESCmdAgentPromptLifecycleState.TimedOut
                                ? ESAutomationRunStatus.TimedOut : ESAutomationRunStatus.Failed;
                    if (record.status == ESAutomationRunStatus.Starting)
                    {
                        record.errors.Add("进程结束前未收到 Codex 接收回执，拒绝把本次任务记为成功。");
                        next = lifecycleEvent.State == ESCmdAgentPromptLifecycleState.Cancelled
                            ? ESAutomationRunStatus.Cancelled : ESAutomationRunStatus.Failed;
                    }
                    if (next == ESAutomationRunStatus.Completed
                        && !TryCaptureCandidateOutputs(runDirectory, record, out string candidateError))
                    {
                        next = ESAutomationRunStatus.Failed;
                        record.errors.Add(candidateError);
                    }
                    if (ESAutomationRunStatus.TryTransition(record.status, next))
                        ESAutomationRunStatus.Transition(record, next);
                    record.exitCode = lifecycleEvent.ExitCode;
                    if (next == ESAutomationRunStatus.Failed || next == ESAutomationRunStatus.TimedOut)
                        record.errors.Add(string.IsNullOrWhiteSpace(lifecycleEvent.Message)
                            ? "受控会话未成功完成。" : lifecycleEvent.Message);
                }
                WriteRecord(recordPath, record);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESAutomation] Graph AI 生命周期回写失败：" + exception.Message);
            }
        }

        private static bool TryCaptureCandidateOutputs(string runDirectory,
            ESAutomationRunRecord record, out string error)
        {
            error = string.Empty;
            string envelopePath = Path.Combine(runDirectory, EnvelopeFileName);
            if (!File.Exists(envelopePath))
            {
                error = "Graph AI Run 缺少派发信封，无法核对候选输出。";
                return false;
            }

            GraphDispatchEnvelope envelope;
            try
            {
                envelope = JsonConvert.DeserializeObject<GraphDispatchEnvelope>(
                    File.ReadAllText(envelopePath, new UTF8Encoding(false, true)));
            }
            catch (Exception exception)
            {
                error = "Graph AI 派发信封无法读取：" + exception.Message;
                return false;
            }
            if (envelope == null)
            {
                error = "Graph AI 派发信封为空。";
                return false;
            }
            if (!string.Equals(envelope.operationKind, "candidate", StringComparison.Ordinal))
                return true;
            if (!ESAgentArtifactGenerationWorkspace.TryReadRequest(envelope.requestDirectory,
                    out ESAgentArtifactGenerationRequest request, out error))
                return false;

            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(
                envelope.requestDirectory);
            string manifestPath = Path.Combine(requestFull, "candidate-manifest.json");
            if (!File.Exists(manifestPath))
                manifestPath = Path.Combine(requestFull, "candidate", "candidate-manifest.json");
            string validationReportPath = Path.Combine(requestFull, "validation-report.md");
            if (!File.Exists(manifestPath) || !File.Exists(validationReportPath))
            {
                error = "候选生成未产出 candidate-manifest.json 或 validation-report.md。";
                return false;
            }

            ESAgentArtifactCandidateManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<ESAgentArtifactCandidateManifest>(
                    File.ReadAllText(manifestPath, new UTF8Encoding(false, true)));
            }
            catch (Exception exception)
            {
                error = "候选 Manifest 无法读取：" + exception.Message;
                return false;
            }
            List<string> validationErrors = ESAgentArtifactCandidateValidator.Validate(
                envelope.requestDirectory, request, manifest);
            if (validationErrors.Count > 0)
            {
                error = "候选未通过当前 Graph 合同：" + validationErrors[0];
                return false;
            }

            AddOutput(record, manifestPath);
            AddOutput(record, validationReportPath);
            foreach (ESAgentArtifactCandidateFile file in manifest.files
                ?? Array.Empty<ESAgentArtifactCandidateFile>())
            {
                if (!ESAgentArtifactCandidateValidator.TryResolveCandidate(envelope.requestDirectory,
                        file.candidateRelativePath, out string candidatePath, out error))
                    return false;
                AddOutput(record, candidatePath);
            }
            record.findings.Add("已验证并绑定候选输出：RequestId=" + request.requestId
                + "，Files=" + (manifest.files?.Length ?? 0));
            return true;
        }

        private static void WriteDispatchReceipt(string runDirectory,
            ESCmdAgentPromptLifecycleEvent lifecycleEvent, ESAutomationRunRecord record)
        {
            string receiptPath = Path.Combine(runDirectory, ReceiptFileName);
            var receipt = new GraphDispatchReceipt
            {
                runId = lifecycleEvent.CorrelationId,
                state = lifecycleEvent.State.ToString(),
                eventType = lifecycleEvent.EventType,
                sessionId = lifecycleEvent.SessionId,
                messageId = lifecycleEvent.MessageId,
                operationDirectory = lifecycleEvent.OperationDirectory,
                acceptedAtUtc = lifecycleEvent.FinishedAtUtc,
                riskAcceptanceHash = record.riskAcceptanceHash,
                message = lifecycleEvent.Message,
            };
            ESAutomationPathPolicy.WriteWorkerTextAtomic(receiptPath,
                JsonConvert.SerializeObject(receipt, Formatting.Indented),
                new[] { ESAutomationPathPolicy.TempRoot });
            AddOutput(record, receiptPath);
        }

        private static bool TryApplyManagedLifecycleIdentity(ESAutomationRunRecord record,
            ESCmdAgentPromptLifecycleEvent lifecycleEvent, out string error)
        {
            error = string.Empty;
            if (!MatchesOrEmpty(record.sessionId, lifecycleEvent.SessionId))
            {
                error = "受管生命周期回执 SessionId 与 RunRecord 不一致，已拒绝跨会话覆盖。";
                return false;
            }
            if (!MatchesOrEmpty(record.messageId, lifecycleEvent.MessageId))
            {
                error = "受管生命周期回执 messageId 与 RunRecord 不一致，已拒绝跨消息覆盖。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(lifecycleEvent.SessionId))
                record.sessionId = lifecycleEvent.SessionId;
            if (!string.IsNullOrWhiteSpace(lifecycleEvent.MessageId))
                record.messageId = lifecycleEvent.MessageId;
            if (!string.IsNullOrWhiteSpace(lifecycleEvent.OperationDirectory))
                record.operationDirectory = lifecycleEvent.OperationDirectory;
            return true;
        }

        private static bool MatchesOrEmpty(string existing, string incoming)
        {
            return string.IsNullOrWhiteSpace(existing) || string.IsNullOrWhiteSpace(incoming)
                || string.Equals(existing, incoming, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddOutput(ESAutomationRunRecord record, string path)
        {
            string relative = ToProjectRelative(path);
            int index = record.outputs.IndexOf(relative);
            string hash = ComputeSha256(path);
            if (index < 0)
            {
                record.outputs.Add(relative);
                record.outputHashes.Add(hash);
            }
            else if (index < record.outputHashes.Count)
            {
                record.outputHashes[index] = hash;
            }
        }

        private static void TryFinalizeFailure(string runId, string message)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) return;
            string path = Path.Combine(ESAutomationPathPolicy.TempRoot, runId, RunRecordFileName);
            if (!File.Exists(path)) return;
            try
            {
                ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                    File.ReadAllText(path, new UTF8Encoding(false, true)));
                if (record == null) return;
                NormalizeRecordCollections(record);
                if (ESAutomationRunStatus.TryTransition(record.status, ESAutomationRunStatus.Failed))
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                record.errors.Add(message ?? "Automation 异常。");
                record.exitCode = -1;
                WriteRecord(path, record);
            }
            catch (Exception exception) { Debug.LogError("[ESAutomation] 无法持久化 Graph AI 异常：" + exception.Message); }
        }

        private static ESAutomationWorkerRegistration CreateWorkerRegistration()
            => new ESAutomationWorkerRegistration
            {
                type = WorkerType,
                workerId = WorkerId,
                version = WorkerVersion,
                entrypointHash = EntrypointHash,
                enabled = true,
            };

        private static int ResolveTimeoutSeconds(string taskId, int version)
            => ESAutomationTaskRegistry.TryGet(taskId, version, out ESAutomationTaskContract contract)
                ? contract.timeoutSeconds : 600;

        private static string ComputeInvocationHash(JObject input)
            => ComputeSha256(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input ?? new JObject(), Formatting.None)));

        private static string ComputeStableInvocationHash(ESAutomationTaskInvocation invocation)
        {
            var identity = new JObject
            {
                ["taskId"] = invocation?.taskId ?? string.Empty,
                ["taskVersion"] = invocation?.taskVersion ?? 0,
                ["preset"] = invocation?.preset ?? string.Empty,
                ["dryRun"] = invocation?.dryRun ?? false,
                ["input"] = invocation?.input?.DeepClone() ?? new JObject(),
            };
            return ComputeSha256(Encoding.UTF8.GetBytes(identity.ToString(Formatting.None)));
        }

        private static string ToInvocationStatus(string status)
        {
            if (status == ESAutomationRunStatus.Created || status == ESAutomationRunStatus.Starting) return "Starting";
            if (status == ESAutomationRunStatus.Accepted) return "Accepted";
            if (status == ESAutomationRunStatus.Running) return "Running";
            if (status == ESAutomationRunStatus.Completed || status == ESAutomationRunStatus.DryRun) return "Completed";
            if (status == ESAutomationRunStatus.Blocked) return "Blocked";
            if (status == ESAutomationRunStatus.Cancelled) return "Cancelled";
            if (status == ESAutomationRunStatus.TimedOut) return "TimedOut";
            if (status == ESAutomationRunStatus.Failed) return "Failed";
            return "Failed";
        }

        [Serializable]
        private sealed class GraphDispatchEnvelope
        {
            public string runId;
            public string requestId;
            public string graphId;
            public string contentSignature;
            public string requestDirectory;
            public string operationKind;
            public string prompt;
            public ESGraphRiskAcceptance riskAcceptance;
        }

        [Serializable]
        private sealed class GraphDispatchReceipt
        {
            public string runId;
            public string state;
            public string eventType;
            public string sessionId;
            public string messageId;
            public string operationDirectory;
            public string acceptedAtUtc;
            public string riskAcceptanceHash;
            public string message;
        }

        private static void NormalizeRecordCollections(ESAutomationRunRecord record)
        {
            record.acceptedRiskCodes ??= new List<string>();
            record.outputs ??= new List<string>();
            record.outputHashes ??= new List<string>();
            while (record.outputHashes.Count < record.outputs.Count)
                record.outputHashes.Add(string.Empty);
            record.findings ??= new List<string>();
            record.errors ??= new List<string>();
        }

        private static void WriteRecord(string path, ESAutomationRunRecord record)
        {
            ESAutomationPathPolicy.WriteWorkerTextAtomic(path, JsonConvert.SerializeObject(record, Formatting.Indented), new[] { ESAutomationPathPolicy.TempRoot });
        }

        private static ESAutomationRunRecord ReadRecord(string path)
        {
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                File.ReadAllText(path, new UTF8Encoding(false, true)));
            if (record == null) throw new InvalidDataException("Graph AI RunRecord 为空。");
            NormalizeRecordCollections(record);
            if (!Guid.TryParseExact(record.runId, "N", out _)
                || !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), record.runId,
                    StringComparison.Ordinal)
                || !IsSha256(record.inputManifestHash))
                throw new InvalidDataException("Graph AI RunRecord 身份或输入 Hash 无效。");
            return record;
        }

        private static string ToProjectRelative(string path)
        {
            string root = ESAutomationPathPolicy.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length).Replace('\\', '/') : path;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ComputeEntrypointHash()
        {
            string path = Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES", "Editor",
                "ESCmdAgent", "ESCmdAgentWindow.cs");
            if (!File.Exists(path))
                throw new FileNotFoundException("Graph AI 受信入口不存在，无法建立 Worker 指纹。", path);
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

        private static bool IsSafeRequestId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool asciiLetterOrDigit = character >= 'a' && character <= 'z'
                    || character >= 'A' && character <= 'Z'
                    || character >= '0' && character <= '9';
                if (!asciiLetterOrDigit && character != '-' && character != '_' && character != '.')
                    return false;
            }
            return true;
        }

        private static string NormalizeProjectPath(string value)
            => (value ?? string.Empty).Replace('\\', '/').Trim('/');
    }

    internal sealed class ESAgentGraphAutomationInitializer : ES.EditorInvoker_Level0
    {
        public override void InitInvoke() => ESAgentGraphAutomation.InitializeForEditor();
    }
}
