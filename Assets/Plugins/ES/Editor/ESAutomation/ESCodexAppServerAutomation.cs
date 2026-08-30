using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ES
{
    /// <summary>
    /// Codex App Server 的 ES 受管适配器。Codex 只提供会话、推理和候选文本；
    /// ES 保留用户授权、路径/能力交集、RunRecord、证据和业务完成判定。
    /// </summary>
    // candidate-only / orchestration status only: Codex cannot declare ES task completion.
    internal static class ESCodexAppServerAutomation
    {
        internal const string TaskId = "es.codex.app-server";
        internal const int TaskVersion = 1;
        internal const string WorkerType = "PowerShell";
        internal const string WorkerId = "es.codex.app-server";
        internal const string WorkerVersion = "1.0.0";
        internal const string WorkerEntrypointHash = "cfb2d21f0dc523c6c52fed941daa7b2fdb0922ed0559ab7e4c219b2820ca837f";
        internal const string InputSchemaHash = "466dd186db60bf3f1271f7e76c7b786b9b3f421edc2da1320f00c962427d2315";
        internal const string CandidateEnvelopeSchemaHash = "b1221cd51b0008cea6c60b306ad419c3055a033d38a558340f9429f6947f0d82";
        internal const string ReceiptVerifierId = "es.codex.app-server.receipt";
        internal const string CommandId = "codex.appserver.execute";
        private const int TimeoutSeconds = 900;
        private const ESAutomationCapability Capabilities =
            ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteReports
            | ESAutomationCapability.WriteTemp | ESAutomationCapability.ExternalRead;
        private const int MaximumRecoveredRuns = 64;
        private static readonly Dictionary<string, ActiveRun> ActiveRuns =
            new Dictionary<string, ActiveRun>(StringComparer.Ordinal);

        private static string WorkerPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers", "PowerShell", "Invoke-ESCodexAppServerWorker.ps1");
        private static string SchemaPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-codex-app-server-v1.schema.json");
        private static string CandidateEnvelopeSchemaPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-codex-candidate-envelope-v1.schema.json");
        private static string CommandPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES", "AICommands", "CodexAppServerHarness受管开发_AI命令.md");
        private static string RunsRoot => Path.Combine(ESAutomationPathPolicy.RunsRoot, "CodexAppServer");

        private static List<string> RequiredAuthorityReadPaths()
            => new List<string>
            {
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "AGENTS.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "AISpace", "README.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES", "AIWarnings", "00_开始阅读（Start）", "README.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES", "AIWarnings", "00_开始阅读（Start）", "当前状态（CurrentStatus）.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES", "AIWarnings", "00_开始阅读（Start）", "规则索引（RuleIndex）.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Documentation", "AIKnowledge", "AIBRAIN_ENTRY.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Documentation", "AIKnowledge", "KnowledgeIndex.yaml"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Documentation", "AIKnowledge", "entries", "codex-app-server-integration.md"),
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-codex-app-server-integration-declaration-v1.json"),
                SchemaPath,
                CandidateEnvelopeSchemaPath,
                Path.Combine(ESAutomationPathPolicy.ProjectRoot, ".agents", "skills", "es-codex-session-bootstrap", "SKILL.md"),
                CommandPath,
            };

        private static bool RequiredAuthorityReadsPresent()
        {
            foreach (string path in RequiredAuthorityReadPaths())
            {
                if (!File.Exists(path) || ESManagedFileIO.ContainsExistingReparsePoint(path)) return false;
            }
            return true;
        }

        internal static void Register()
        {
            VerifyBindings();
            if (!ESAutomationVerifierRegistry.IsRegistered(ReceiptVerifierId))
                ESAutomationVerifierRegistry.Register(ReceiptVerifierId, VerifyReceipt);
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract contract))
            {
                contract = new ESAutomationTaskContract
                {
                    taskId = TaskId,
                    version = TaskVersion,
                    worker = Worker(),
                    inputs = new List<string> { "request.json" },
                    readRoots = new List<string>
                    {
                        "AGENTS.md",
                        "ES/AISpace",
                        "Documentation/AIKnowledge",
                        "ES/Automation/Contracts",
                        "ES/Automation/Runs/CodexAppServer",
                        ".agents/skills/es-codex-session-bootstrap",
                        "Assets/Plugins/ES/AIWarnings",
                        "Assets/Plugins/ES/AICommands",
                    },
                    writeRoots = new List<string> { "ES/Automation/Runs/CodexAppServer" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteReports", "WriteTemp", "ExternalRead" },
                    inputSchemaHash = InputSchemaHash,
                    timeoutSeconds = TimeoutSeconds,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "codex-app-server-result.json", "codex-candidate-envelope.json", "run-record.json" },
                    acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        authorityDomain = "editor-tooling",
                        authorityRiskClass = "high",
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "codex-app-server.receipt",
                                verifierId = ReceiptVerifierId,
                                description = "App Server 线程/回合身份、输入 Hash、事件和权限拒绝必须由 ES 回收；不等于业务完成。",
                                runtimeRequired = true,
                            },
                        },
                    },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = TimeoutSeconds,
                        maxOutputBytes = 1024 * 1024,
                        maxRetryCount = 0,
                        maxFindingCount = 128,
                    },
                    capabilityEnvelope = new ESAutomationCapabilityEnvelope
                    {
                        userAuthorization = Capabilities,
                        taskContract = Capabilities,
                        aiCommand = Capabilities,
                        workerCapability = Capabilities,
                        projectBoundary = Capabilities,
                    },
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            else if (contract.worker == null
                || contract.worker.type != WorkerType
                || contract.worker.workerId != WorkerId
                || contract.worker.version != WorkerVersion
                || !string.Equals(contract.worker.entrypointHash, WorkerEntrypointHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(contract.inputSchemaHash, InputSchemaHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Codex App Server TaskContract 与受信 Worker 或 Schema 不一致。");

            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new Endpoint());
            if (!ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId))
                ESAutomationProcessRunner.RegisterAdapter(new Adapter());
            ReconcileInterruptedRuns();
            EditorApplication.update -= PollActiveRuns;
            EditorApplication.update += PollActiveRuns;
            AssemblyReloadEvents.beforeAssemblyReload -= StopActiveRunsForLifecycle;
            AssemblyReloadEvents.beforeAssemblyReload += StopActiveRunsForLifecycle;
            EditorApplication.quitting -= StopActiveRunsForLifecycle;
            EditorApplication.quitting += StopActiveRunsForLifecycle;
        }

        internal sealed class CodexAppServerStatus
        {
            public string state = "Unknown";
            public string message = string.Empty;
            public string runtimeStatus = "runtime-not-run";
            public bool registered;
        }

        /// <summary>
        /// 返回无副作用的合同/注册状态。此方法不启动 Codex、不访问 Provider，
        /// 也不把“本地合同已注册”投影为运行时连接或业务完成。
        /// </summary>
        internal static CodexAppServerStatus GetStatus()
        {
            var status = new CodexAppServerStatus();
            try
            {
                VerifyBindings();
                bool taskRegistered = ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out _);
                bool descriptorRegistered = ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _);
                bool adapterRegistered = ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId);
                bool requiredReadsPresent = RequiredAuthorityReadsPresent();
                status.registered = taskRegistered && descriptorRegistered && adapterRegistered && requiredReadsPresent;
                status.state = status.registered ? "Registered" : "Incomplete";
                status.message = status.registered
                    ? "TaskContract、Facade Descriptor 和受信 WorkerAdapter 均已注册。"
                    : "Codex 合同/注册链路或项目权威必读文件尚未完整就绪。";
            }
            catch (Exception exception)
            {
                status.state = "Invalid";
                status.message = Sanitize(exception.Message);
            }
            return status;
        }

        private static ESAutomationWorkerRegistration Worker()
            => new ESAutomationWorkerRegistration
            {
                type = WorkerType,
                workerId = WorkerId,
                version = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                enabled = true,
            };

        private static void VerifyBindings()
        {
            if (!File.Exists(WorkerPath) || !File.Exists(SchemaPath) || !File.Exists(CandidateEnvelopeSchemaPath)
                || ESManagedFileIO.ContainsExistingReparsePoint(WorkerPath)
                || ESManagedFileIO.ContainsExistingReparsePoint(SchemaPath)
                || ESManagedFileIO.ContainsExistingReparsePoint(CandidateEnvelopeSchemaPath))
                throw new InvalidOperationException("Codex App Server Worker 或输入 Schema 缺失。");
            if (!File.Exists(CommandPath) || ESManagedFileIO.ContainsExistingReparsePoint(CommandPath))
                throw new InvalidOperationException("Codex App Server AICommand 合同缺失或路径不安全。");
            if (!string.Equals(ComputeHash(WorkerPath), WorkerEntrypointHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ComputeHash(SchemaPath), InputSchemaHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ComputeHash(CandidateEnvelopeSchemaPath), CandidateEnvelopeSchemaHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Codex App Server Worker/Schema Hash 漂移。");
        }

        private sealed class Endpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint, IESAutomationCancellableTaskEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                category = "AI/Codex",
                displayName = "Codex App Server 受管 Harness",
                summary = "经 AIBrain、AICommand 与 ESAutomationFacade 门禁驱动 Codex 线程/回合；只读沙箱，ES 保留业务权威。",
                allowAiInvoke = true,
                allowInPlayMode = false,
                inputSchemaHash = InputSchemaHash,
            };

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
                => new ESAutomationInvocationRequirements
                {
                    worker = Worker(),
                    requiredCapabilities = Capabilities,
                    dryRun = invocation == null || invocation.dryRun,
                    readPaths = RequiredAuthorityReadPaths(),
                    writePaths = new List<string> { RunsRoot },
                };

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                if (invocation == null || invocation.input == null)
                    return ESAutomationTaskInvocationResult.Rejected("Codex App Server 缺少输入。");
                try { VerifyBindings(); }
                catch (Exception exception)
                { return ESAutomationTaskInvocationResult.Rejected("Codex App Server 受信绑定校验失败：" + Sanitize(exception.Message)); }
                if (!TryValidateInput(invocation.input, out string operation, out string prompt, out string threadId, out string model, out string error))
                    return ESAutomationTaskInvocationResult.Rejected(error);
                if (invocation.dryRun || operation == "dry-run")
                    return ESAutomationTaskInvocationResult.Completed(
                        "Codex App Server DryRun 完成：未启动 Codex、未访问 Provider、未写入项目源文件。",
                        invocation.invocationId,
                        new JObject
                        {
                            ["providerDeclaration"] = "es-codex",
                            ["workerId"] = WorkerId,
                            ["operation"] = operation,
                            ["authority"] = "ESFramework/ESAI",
                            ["authorityLevel"] = "candidate-only-not-final-acceptance",
                            ["runtimeStatus"] = "runtime-not-run",
                            ["networkCalled"] = false,
                            ["mutationApplied"] = false,
                        });
                if (!RequiredAuthorityReadsPresent())
                    return ESAutomationTaskInvocationResult.Blocked(
                        "Codex App Server 缺少 ES 权威必读文件；拒绝启动外部推理。");

                if (!ESAutomationWorkerRegistration.IsSha256(invocation.brainPlanHash))
                    return ESAutomationTaskInvocationResult.Rejected("Codex App Server 外部执行必须绑定有效的 AIBrain PlanHash。");
                if (!string.IsNullOrWhiteSpace(invocation.idempotencyKey)
                    && (invocation.idempotencyKey.Length > 160
                        || !System.Text.RegularExpressions.Regex.IsMatch(invocation.idempotencyKey, "^[A-Za-z0-9._:-]+$")))
                    return ESAutomationTaskInvocationResult.Rejected("Codex App Server idempotencyKey 格式无效。");
                string commandHash = ComputeHash(CommandPath);
                if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract registeredContract))
                    return ESAutomationTaskInvocationResult.Rejected("Codex App Server 缺少已注册 TaskContract。");
                string taskContractHash = registeredContract.ComputeStableHash();

                string runId = string.IsNullOrWhiteSpace(invocation.invocationId)
                    ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                if (!Guid.TryParseExact(runId, "N", out _))
                    return ESAutomationTaskInvocationResult.Rejected("InvocationId 必须是 N 格式 GUID。");
                string directory = Path.Combine(RunsRoot, runId);
                string requestPath = Path.Combine(directory, "request.json");
                string recordPath = Path.Combine(directory, "run-record.json");
                string resultPath = Path.Combine(directory, "codex-app-server-result.json");
                if (File.Exists(recordPath)) return GetRun(runId);
                if (Directory.Exists(directory))
                    return ESAutomationTaskInvocationResult.Rejected("RunId 目录存在但没有有效 RunRecord，拒绝猜测恢复。");

                ESAutomationPathPolicy.EnsureWorkerDirectory(directory, new[] { RunsRoot });
                var request = new JObject
                {
                    ["projectRoot"] = ESAutomationPathPolicy.ProjectRoot,
                    ["providerDeclaration"] = "es-codex",
                    ["workerId"] = WorkerId,
                    ["workerVersion"] = WorkerVersion,
                    ["taskId"] = TaskId,
                    ["runId"] = runId,
                    ["dryRun"] = false,
                    ["operation"] = operation,
                    ["prompt"] = prompt,
                    ["brainPlanHash"] = invocation.brainPlanHash,
                    ["commandId"] = CommandId,
                    ["commandHash"] = commandHash,
                    ["taskContractHash"] = taskContractHash,
                    ["invocationId"] = runId,
                };
                if (!string.IsNullOrWhiteSpace(invocation.idempotencyKey)) request["idempotencyKey"] = invocation.idempotencyKey;
                if (!string.IsNullOrWhiteSpace(threadId)) request["threadId"] = threadId;
                if (!string.IsNullOrWhiteSpace(model)) request["model"] = model;
                WriteJsonAtomic(requestPath, request);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var record = new ESAutomationRunRecord
                {
                    runId = runId,
                    taskId = TaskId,
                    taskVersion = TaskVersion,
                    operatorId = invocation.actorId ?? string.Empty,
                    gitCommit = ESAutomationSourceState.GetCurrentGitCommit(),
                    workerType = WorkerType,
                    workerId = WorkerId,
                    workerVersion = WorkerVersion,
                    entrypointHash = WorkerEntrypointHash,
                    inputManifestHash = ComputeHash(requestPath),
                    invocationHash = ComputeHash(requestPath),
                    idempotencyKey = invocation.idempotencyKey ?? string.Empty,
                    executionSnapshot = invocation.executionSnapshot,
                    completionDecision = null,
                    status = ESAutomationRunStatus.Starting,
                    startedAtUtc = now.ToString("O"),
                    lastUpdatedAtUtc = now.ToString("O"),
                    operationDirectory = directory,
                };
                WriteJsonAtomic(recordPath, record);
                try
                {
                    ESAutomationProcessExecution execution = ESAutomationProcessRunner.Start(new ESAutomationProcessRequest
                    {
                        taskId = TaskId,
                        taskVersion = TaskVersion,
                        runId = runId,
                        dryRun = false,
                        inputContractPath = requestPath,
                    });
                    record.processId = execution.ProcessId;
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Running);
                    WriteJsonAtomic(recordPath, record);
                    ActiveRuns.Add(runId, new ActiveRun
                    {
                        Execution = execution,
                        Record = record,
                        RecordPath = recordPath,
                        ResultPath = resultPath,
                        Directory = directory,
                        BrainPlanHash = invocation.brainPlanHash,
                        CommandHash = commandHash,
                        TaskContractHash = taskContractHash,
                        InvocationId = runId,
                        IdempotencyKey = invocation.idempotencyKey ?? string.Empty,
                        GenerationMode = invocation.generationMode?.Trim().ToLowerInvariant() ?? string.Empty,
                        SourceScopeHash = invocation.executionSnapshot?.sourceHash?.Trim().ToLowerInvariant() ?? string.Empty,
                        CurrentHead = record.gitCommit ?? string.Empty,
                    });
                    return ESAutomationTaskInvocationResult.Accepted(
                        "Codex App Server 已由 ES 受信 Adapter 接受；Codex 仅在只读沙箱中贡献候选，ES 将继续收集证据并决定完成状态。",
                        runId,
                        new JObject
                        {
                            ["providerDeclaration"] = "es-codex",
                            ["role"] = "external-execution-plane",
                            ["authority"] = "ESFramework/ESAI",
                            ["authorityLevel"] = "candidate-only-not-final-acceptance",
                            ["runtimeStatus"] = "running",
                            ["operation"] = operation,
                            ["brainPlanHash"] = invocation.brainPlanHash,
                            ["commandId"] = CommandId,
                            ["commandHash"] = commandHash,
                            ["taskContractHash"] = taskContractHash,
                        });
                }
                catch (Exception exception)
                {
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                    record.errors.Add(Sanitize(exception.Message));
                    WriteJsonAtomic(recordPath, record);
                    return ESAutomationTaskInvocationResult.Failed("Codex App Server Worker 启动失败：" + Sanitize(exception.Message), runId);
                }
            }

            public ESAutomationTaskInvocationResult GetRun(string runId) => ESCodexAppServerAutomation.GetRun(runId);
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.Rejected("Codex App Server v1 不接受未绑定计划的分阶段输入；请用同一 threadId 重新规划 turn。");
            public ESAutomationTaskInvocationResult CancelRun(string runId, string actorId)
            {
                if (!ActiveRuns.TryGetValue(runId, out ActiveRun active))
                    return ESAutomationTaskInvocationResult.NotFound("未找到活动的 Codex App Server Run。");
                active.CancelRequested = true;
                try { active.Execution.Terminate(); }
                catch (Exception exception) { return ESAutomationTaskInvocationResult.Failed("Codex App Server 取消未确认：" + Sanitize(exception.Message), runId); }
                return ESAutomationTaskInvocationResult.Accepted("已请求终止 Codex App Server 进程树；最终状态以 RunRecord 为准。", runId);
            }
        }

        private sealed class Adapter : IESAutomationWorkerAdapter
        {
            public string WorkerType => ESCodexAppServerAutomation.WorkerType;
            public string WorkerId => ESCodexAppServerAutomation.WorkerId;

            public ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract, ESAutomationProcessRequest request)
            {
                VerifyBindings();
                if (!RequiredAuthorityReadsPresent())
                    throw new InvalidOperationException("Codex App Server 启动前缺少安全的 ES 权威必读文件。");
                if (!File.Exists(WorkerPath) || !File.Exists(SchemaPath))
                    throw new InvalidOperationException("Codex App Server Worker 或 Schema 缺失。");
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(powershell)) throw new FileNotFoundException("Windows PowerShell 入口不可用。", powershell);
                string runDirectory = Path.Combine(RunsRoot, request.runId);
                if (!ESAutomationPathPolicy.IsWithin(runDirectory, new[] { RunsRoot }))
                    throw new UnauthorizedAccessException("Codex App Server Run 目录越界。");
                return new ProcessStartInfo
                {
                    FileName = powershell,
                    Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "
                        + Quote(WorkerPath) + " -InputPath " + Quote(request.inputContractPath)
                        + " -OutputDirectory " + Quote(runDirectory)
                        + " -ProjectRoot " + Quote(ESAutomationPathPolicy.ProjectRoot),
                    WorkingDirectory = ESAutomationPathPolicy.ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(false, true),
                    StandardErrorEncoding = new UTF8Encoding(false, true),
                };
            }
        }

        private sealed class ActiveRun
        {
            public ESAutomationProcessExecution Execution;
            public ESAutomationRunRecord Record;
            public string RecordPath;
            public string ResultPath;
            public string Directory;
            public bool CancelRequested;
            public string BrainPlanHash;
            public string CommandHash;
            public string TaskContractHash;
            public string InvocationId;
            public string IdempotencyKey;
            public string GenerationMode;
            public string SourceScopeHash;
            public string CurrentHead;
        }

        private static bool TryValidateInput(JObject input, out string operation, out string prompt,
            out string threadId, out string model, out string error)
        {
            operation = (string)input["operation"] ?? string.Empty;
            prompt = (string)input["prompt"] ?? string.Empty;
            threadId = (string)input["threadId"] ?? string.Empty;
            model = (string)input["model"] ?? string.Empty;
            error = string.Empty;
            string[] allowed = { "operation", "prompt", "threadId", "model" };
            if (input.Properties().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            { error = "Codex App Server 输入包含未声明字段；不得提交 executable、cwd、sandbox、权限或输出路径。"; return false; }
            if (!new[] { "dry-run", "check-local", "start-thread", "turn" }.Contains(operation, StringComparer.Ordinal))
            { error = "operation 必须是 dry-run、check-local、start-thread 或 turn。"; return false; }
            if (prompt.Length > 12000)
            { error = "prompt 长度不得超过 12000 字符。"; return false; }
            if (operation == "turn" && string.IsNullOrWhiteSpace(threadId))
            { error = "turn 必须绑定精确 threadId；不按标题或最近会话猜测。"; return false; }
            if (!string.IsNullOrWhiteSpace(threadId) && (threadId.Length > 160 || !System.Text.RegularExpressions.Regex.IsMatch(threadId, "^[A-Za-z0-9._:-]+$")))
            { error = "threadId 格式无效。"; return false; }
            if (!string.IsNullOrWhiteSpace(model) && (model.Length > 96 || !System.Text.RegularExpressions.Regex.IsMatch(model, "^[A-Za-z0-9._:-]+$")))
            { error = "model 格式无效。"; return false; }
            if ((operation == "start-thread" || operation == "turn") && string.IsNullOrWhiteSpace(prompt))
            { error = operation + " 必须提供非空 prompt。"; return false; }
            return true;
        }

        private static bool VerifyReceipt(ESAutomationCriterionResult result)
        {
            return result != null
                && result.criterionId == "codex-app-server.receipt"
                && result.verifierId == ReceiptVerifierId
                && result.passed
                && result.evidenceState == ESAutomationEvidenceState.Fresh
                && result.evidenceScope == ESAutomationEvidenceScope.Runtime
                && ESAutomationWorkerRegistration.IsSha256(result.evidenceHash);
        }

        private static void PollActiveRuns()
        {
            foreach (ActiveRun active in ActiveRuns.Values.ToList())
            {
                try
                {
                    if (active.Execution.EnforceTimeout(DateTimeOffset.UtcNow))
                    { FinishWithoutResult(active, ESAutomationRunStatus.TimedOut, "Codex App Server Worker 超时。"); continue; }
                    if (!active.Execution.HasExited) continue;
                    FinalizeResult(active);
                }
                catch (Exception exception)
                { FinishWithoutResult(active, active.CancelRequested ? ESAutomationRunStatus.Cancelled : ESAutomationRunStatus.Failed, exception.Message); }
                finally
                {
                    if (active.Execution.HasExited) { active.Execution.Dispose(); ActiveRuns.Remove(active.Record.runId); }
                }
            }
        }

        private static void FinalizeResult(ActiveRun active)
        {
            if (!File.Exists(active.ResultPath) || ESManagedFileIO.ContainsExistingReparsePoint(active.ResultPath))
                throw new InvalidDataException("Codex App Server Worker 已退出但没有安全的结构化 result。");
            JObject result = JObject.Parse(File.ReadAllText(active.ResultPath, new UTF8Encoding(false, true)));
            if ((string)result["taskId"] != TaskId || (int?)result["taskVersion"] != TaskVersion
                || (string)result["runId"] != active.Record.runId
                || (string)result["providerDeclaration"] != "es-codex"
                || (string)result["workerId"] != WorkerId || (string)result["workerVersion"] != WorkerVersion
                || !string.Equals((string)result["inputManifestHash"], active.Record.inputManifestHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals((string)result["brainPlanHash"], active.BrainPlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals((string)result["commandId"], CommandId, StringComparison.Ordinal)
                        || !string.Equals((string)result["commandHash"], active.CommandHash, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals((string)result["taskContractHash"], active.TaskContractHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals((string)result["invocationId"], active.InvocationId, StringComparison.Ordinal)
                || !string.Equals((string)result["idempotencyKey"] ?? string.Empty, active.IdempotencyKey, StringComparison.Ordinal))
                throw new InvalidDataException("Codex App Server Worker 结果身份不匹配。");
            active.Record.exitCode = (int?)result["exitCode"] ?? -1;
            active.Record.threadId = (string)result["threadId"] ?? string.Empty;
            active.Record.sessionId = (string)result["sessionId"] ?? string.Empty;
            active.Record.codexProcessId = (int?)result["codexProcessId"] ?? 0;
            var outputs = new List<string> { "codex-app-server-result.json" };
            var outputHashes = new List<string> { ComputeHash(active.ResultPath) };
            active.Record.findings = new List<string> { "Codex 输出为候选贡献；ES 业务完成与正式写入未由本 Worker 判定。" };
            foreach (JToken item in result["errors"] as JArray ?? new JArray()) active.Record.errors.Add(Sanitize(item.ToString()));
            string status = (string)result["status"] ?? "Failed";
            if (status == "Passed" && !string.IsNullOrWhiteSpace(active.GenerationMode))
            {
                if (!ESCodexCandidateEnvelopeAdapter.TryNormalize(
                        result,
                        active.GenerationMode,
                        active.CurrentHead,
                        active.BrainPlanHash,
                        active.SourceScopeHash,
                        ESAutomationPathPolicy.ProjectRoot,
                        out ESCodexCandidateEnvelope candidateEnvelope,
                        out string candidateError))
                    throw new InvalidDataException("Codex CandidateEnvelope 归一化失败：" + candidateError);
                string candidateEnvelopePath = Path.Combine(active.Directory, "codex-candidate-envelope.json");
                WriteJsonAtomic(candidateEnvelopePath, candidateEnvelope);
                outputs.Add("codex-candidate-envelope.json");
                outputHashes.Add(ComputeHash(candidateEnvelopePath));
                active.Record.findings.Add("Codex 结果已归一化为 CandidateEnvelope；仍需 ABCD 审计和用户明确 Apply 授权。");
            }
            active.Record.outputs = outputs;
            active.Record.outputHashes = outputHashes;
            string next = status == "Passed" ? ESAutomationRunStatus.Completed
                : status == "Blocked" ? ESAutomationRunStatus.Blocked
                : status == "Cancelled" ? ESAutomationRunStatus.Cancelled
                : ESAutomationRunStatus.Failed;
            ESAutomationRunStatus.Transition(active.Record, next);
            WriteJsonAtomic(active.RecordPath, active.Record);
        }

        private static void FinishWithoutResult(ActiveRun active, string status, string error)
        {
            if (!ESAutomationRunStatus.IsTerminal(active.Record.status)) ESAutomationRunStatus.Transition(active.Record, status);
            active.Record.exitCode = -1;
            active.Record.errors.Add(Sanitize(error));
            WriteJsonAtomic(active.RecordPath, active.Record);
        }

        private static ESAutomationTaskInvocationResult GetRun(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            string directory = Path.Combine(RunsRoot, runId);
            string recordPath = Path.Combine(directory, "run-record.json");
            if (!File.Exists(recordPath)) return ESAutomationTaskInvocationResult.NotFound("未找到 Codex App Server RunRecord。");
            if (ESManagedFileIO.ContainsExistingReparsePoint(recordPath))
                return ESAutomationTaskInvocationResult.Failed("Codex App Server RunRecord 路径不安全。", runId);
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(File.ReadAllText(recordPath, new UTF8Encoding(false, true)));
            if (record == null || record.runId != runId || record.taskId != TaskId) return ESAutomationTaskInvocationResult.Failed("Codex App Server RunRecord 身份无效。", runId);
            JObject data = new JObject
            {
                ["status"] = record.status,
                ["exitCode"] = record.exitCode,
                ["threadId"] = record.threadId,
                ["sessionId"] = record.sessionId,
                ["outputs"] = JArray.FromObject(record.outputs ?? new List<string>()),
                ["findings"] = JArray.FromObject(record.findings ?? new List<string>()),
                ["errors"] = JArray.FromObject(record.errors ?? new List<string>()),
                ["authority"] = "ESFramework/ESAI",
                ["authorityLevel"] = "candidate-only-not-final-acceptance",
                ["completionDecision"] = null,
                ["runtimeStatus"] = ESAutomationRunStatus.IsTerminal(record.status) ? "runtime-executed" : "running",
            };
            if (!ESAutomationRunStatus.IsTerminal(record.status))
                return ESAutomationTaskInvocationResult.Starting("Codex App Server Run 当前状态：" + record.status, runId, data);
            if (record.status != ESAutomationRunStatus.Completed
                && record.status != ESAutomationRunStatus.Failed
                && record.status != ESAutomationRunStatus.Cancelled
                && record.status != ESAutomationRunStatus.TimedOut
                && record.status != ESAutomationRunStatus.Blocked
                && record.status != ESAutomationRunStatus.DryRun)
                return ESAutomationTaskInvocationResult.Failed("Codex App Server RunRecord 终态无效。", runId);
            return new ESAutomationTaskInvocationResult
            {
                status = record.status,
                message = "Codex App Server Run 已结束：" + record.status,
                runId = runId,
                data = data,
            };
        }

        private static void ReconcileInterruptedRuns()
        {
            if (!Directory.Exists(RunsRoot) || ESManagedFileIO.ContainsExistingReparsePoint(RunsRoot)) return;
            var safeDirectories = new List<string>();
            foreach (string directory in Directory.EnumerateDirectories(RunsRoot))
            {
                try
                {
                    if (ESAutomationPathPolicy.IsWithin(directory, new[] { RunsRoot })
                        && !ESManagedFileIO.ContainsExistingReparsePoint(directory))
                        safeDirectories.Add(directory);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Codex App Server Run 目录路径不安全，已跳过恢复：" + Sanitize(exception.Message));
                }
            }
            foreach (string directory in safeDirectories.OrderByDescending(Directory.GetLastWriteTimeUtc).Take(MaximumRecoveredRuns))
            {
                string path = Path.Combine(directory, "run-record.json");
                if (!File.Exists(path) || ESManagedFileIO.ContainsExistingReparsePoint(path)) continue;
                try
                {
                    ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(File.ReadAllText(path, new UTF8Encoding(false, true)));
                    if (record == null || ESAutomationRunStatus.IsTerminal(record.status)) continue;
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                    record.errors.Add("Editor 重启/域重载后 Codex App Server 进程不可恢复，已保守终结。");
                    WriteJsonAtomic(path, record);
                }
                catch (Exception exception) { Debug.LogError("Codex App Server RunRecord 恢复失败：" + Sanitize(exception.Message)); }
            }
        }

        private static void StopActiveRunsForLifecycle()
        {
            foreach (ActiveRun active in ActiveRuns.Values.ToList())
            {
                try
                {
                    if (!active.Execution.HasExited) active.Execution.Terminate();
                    FinishWithoutResult(active, ESAutomationRunStatus.Failed, "Editor 生命周期结束，Codex App Server 进程已终止。");
                }
                catch (Exception exception) { Debug.LogError("Codex App Server 生命周期终止未确认：" + Sanitize(exception.Message)); }
            }
            ActiveRuns.Clear();
        }

        private static string ComputeHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void WriteJsonAtomic(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Run directory is empty."));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, value is JToken token ? token.ToString(Formatting.Indented) + "\n" : JsonConvert.SerializeObject(value, Formatting.Indented) + "\n", new UTF8Encoding(false, true));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        private static string Sanitize(string value)
        {
            string result = value ?? string.Empty;
            foreach (string name in new[] { "OPENAI_API_KEY", "CODEX_API_KEY", "ES_CODEX_CLI_PATH" })
            {
                string secret = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(secret)) result = result.Replace(secret, "[REDACTED]");
            }
            return result.Length <= 2000 ? result : result.Substring(0, 2000);
        }
    }

    internal sealed class ESCodexAppServerAutomationInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke() => ESCodexAppServerAutomation.Register();
    }
}
