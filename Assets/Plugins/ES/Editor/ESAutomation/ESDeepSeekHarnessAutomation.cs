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
    // DSH orchestration status only; downstream decision remains with TaskContext/ABCD.
    // This adapter never declares task completion or final acceptance.
    /// <summary>
    /// DeepSeek Harness 的 ES 受管接入：DSH 只提供高权威开发建议/执行能力，
    /// ES Automation/AIBrain 仍拥有权限、证据和最终完成判定。
    /// </summary>
    internal static class ESDeepSeekHarnessAutomation
    {
        internal const string TaskId = "es.deepseek.harness";
        internal const int TaskVersion = 1;
        internal const string WorkerType = "Other";
        internal const string WorkerId = "es.deepseek-harness";
        internal const string WorkerVersion = "0.2.0";
        internal const string WorkerEntrypointHash = "2b840aa7c441ded006b44e87d32537ba0c51f58740f7b045244d07f98f755749";
        internal const string InputSchemaHash = "947d8cb2c2d1d6b1d4e1ba4c43e5899ca1305a07ccaa0d2b28cae6b93368fba4";
        private const int MaximumRecoveredRuns = 64;

        private static readonly Dictionary<string, ActiveRun> ActiveRuns =
            new Dictionary<string, ActiveRun>(StringComparer.Ordinal);

        internal static void Register()
        {
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
                        "ES/Automation/Workers/Node/DeepSeekHarness",
                        "ES/Automation/Contracts",
                        "ES/Automation/Temp/DeepSeekHarness",
                    },
                    writeRoots = new List<string>
                    {
                        "ES/Automation/Runs/DeepSeekHarness",
                        "ES/Automation/Temp/DeepSeekHarness",
                    },
                    capabilities = new List<string> { "ReadArtifacts", "WriteReports", "WriteTemp", "ExternalRead" },
                    inputSchemaHash = InputSchemaHash,
                    timeoutSeconds = 600,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "deepseek-harness-output.json" },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = 600,
                        maxOutputBytes = 256 * 1024,
                        maxRetryCount = 0,
                        maxFindingCount = 32,
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
            {
                throw new InvalidOperationException("DeepSeek Harness TaskContract 与受信 Worker 或 Schema 不一致。");
            }
            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new Endpoint());
            if (!ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId))
                ESAutomationProcessRunner.RegisterAdapter(new DeepSeekHarnessAdapter());

            ReconcileInterruptedRuns();
            EditorApplication.update -= PollActiveRuns;
            EditorApplication.update += PollActiveRuns;
            AssemblyReloadEvents.beforeAssemblyReload -= StopActiveRunsForLifecycle;
            AssemblyReloadEvents.beforeAssemblyReload += StopActiveRunsForLifecycle;
            EditorApplication.quitting -= StopActiveRunsForLifecycle;
            EditorApplication.quitting += StopActiveRunsForLifecycle;
        }

        internal static DeepSeekHarnessStatus GetStatus(bool requireProvider)
        {
            var status = new DeepSeekHarnessStatus
            {
                state = "NotConnected",
                role = "external-execution-plane",
                authority = "ESFramework/ESAI",
                authorityLevel = "high-contributor-not-final-acceptance",
                runtimeStatus = "runtime-not-run",
                nextAction = "运行 ES/Automation/Workers/Node/DeepSeekHarness/Install-ESDeepSeekHarness.ps1。",
            };
            string root = ESAutomationPathPolicy.ProjectRoot;
            string workerRoot = Path.Combine(root, "ES", "Automation", "Workers", "Node", "DeepSeekHarness");
            string packagePath = Path.Combine(workerRoot, "package.json");
            string lockPath = Path.Combine(workerRoot, "package-lock.json");
            if (!File.Exists(packagePath)) return status.With("PACKAGE_MISSING", "DSH package.json 缺失。");
            if (!File.Exists(lockPath)) return status.With("PACKAGE_LOCK_MISSING", "DSH package-lock.json 缺失；依赖尚未冻结。");

            string runtimeConfigPath = GetRuntimeConfigPath(root);
            JObject config = ReadRuntimeConfig(root);
            if (config == null && !File.Exists(runtimeConfigPath))
                return status.With("RUNTIME_CONFIG_MISSING", "缺少 runtime.local.json；请先执行一步安装脚本，禁止使用隐式 Profile 或路径。");
            if (File.Exists(runtimeConfigPath) && config == null)
                return status.With("RUNTIME_CONFIG_INVALID", "runtime.local.json 无法按严格 UTF-8/JSON 读取；请重新执行一步安装脚本。");
            if (config != null && (!string.Equals(config.Value<string>("declaration"), "es-deepseek", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(config.Value<string>("providerDeclaration"), "es-deepseek", StringComparison.OrdinalIgnoreCase)))
                return status.With("RUNTIME_CONFIG_INVALID", "runtime.local.json 缺少有效的 es-deepseek Provider 声明。请重新执行一步安装脚本。");
            string nodePath = FirstNonEmpty(
                Environment.GetEnvironmentVariable("ES_DEEPSEEK_NODE_PATH"),
                Environment.GetEnvironmentVariable("ES_AUTOMATION_NODE_PATH"),
                config?.Value<string>("nodePath"));
            if (string.IsNullOrWhiteSpace(nodePath) || !Path.IsPathRooted(nodePath) || !File.Exists(nodePath))
                return status.With("NODE_UNAVAILABLE", "未配置有效的绝对 node.exe；不会回退 PATH。");

            string dshPath = FirstNonEmpty(
                Environment.GetEnvironmentVariable("DSH_EXECUTABLE"),
                config?.Value<string>("dshExecutable"),
                Path.Combine(workerRoot, "node_modules", ".bin", "dsh.cmd"));
            if (string.IsNullOrWhiteSpace(dshPath) || !Path.IsPathRooted(dshPath) || !File.Exists(dshPath))
                return status.With("DSH_UNAVAILABLE", "未发现受管 node_modules/.bin/dsh.cmd；请先执行一步安装脚本。");
            string dshEntrypoint = Path.Combine(workerRoot, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
            if (!File.Exists(dshEntrypoint))
                return status.With("DSH_UNAVAILABLE", "未发现锁定的 DSH JavaScript 入口；请重新执行一步安装脚本。");
            string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(dshPath).StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return status.With("DSH_PATH_ESCAPE", "DSH 可执行入口必须位于项目根内。");
            string profile = config?.Value<string>("profile") ?? "headless";
            if (!string.Equals(profile, "headless", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(profile, "sdk", StringComparison.OrdinalIgnoreCase))
                return status.With("PROFILE_INVALID", "DSH Profile 只允许受管 headless 或 sdk。");
            try
            {
                EnsureProjectPath(config?.Value<string>("dshHome"), "DSH_HOME", root);
                EnsureProjectPath(config?.Value<string>("workspace"), "workspace", root);
            }
            catch (Exception exception)
            {
                return status.With("RUNTIME_PATH_INVALID", Sanitize(exception.Message));
            }

            bool providerConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
            status.providerConfigured = providerConfigured;
            if (requireProvider && !providerConfigured)
            {
                status.nextAction = "在启动 Unity 的用户会话设置 DEEPSEEK_API_KEY，然后重新检查；不要写入仓库。";
                return status.With("PROVIDER_CREDENTIAL_MISSING", "缺少 DEEPSEEK_API_KEY；只检查存在性，不读取或输出凭据。");
            }
            status.state = "Connected";
            status.reasonCode = string.Empty;
            status.message = requireProvider ? "DSH 本地链路和 Provider 凭据存在性检查通过。" : "DSH 本地运行时已就绪；真实 Provider 调用尚未执行。";
            status.nextAction = "可通过 ESAutomationCenter 运行 DryRun 或受管 headless 任务。";
            return status;
        }

        internal static DeepSeekHarnessStatus RunLocalCheck(bool requireProvider)
        {
            string script = Path.Combine(GetWorkerRoot(), "Test-ESDeepSeekHarness.ps1");
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(script) || !File.Exists(powershell))
                return GetStatus(requireProvider).With("CHECKER_UNAVAILABLE", "本地 PowerShell 检查器不可用。");
            var info = new ProcessStartInfo
            {
                FileName = powershell,
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script)
                    + " -ProjectRoot " + Quote(ESAutomationPathPolicy.ProjectRoot)
                    + (requireProvider ? " -RequireProvider" : string.Empty),
                WorkingDirectory = ESAutomationPathPolicy.ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = new UTF8Encoding(false, true),
            };
            try
            {
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        return GetStatus(requireProvider).With("CHECKER_TIMEOUT", "本地 DSH 检查器超时。");
                    }
                    JObject json = JObject.Parse(output.Trim());
                    DeepSeekHarnessStatus status = json.ToObject<DeepSeekHarnessStatus>();
                    if (status == null) return GetStatus(requireProvider).With("CHECKER_INVALID_OUTPUT", "本地 DSH 检查器返回为空。");
                    if (process.ExitCode != 0 && status.state == "Connected")
                        return status.With("CHECKER_FAILED", Sanitize(error));
                    return status;
                }
            }
            catch (Exception exception)
            {
                return GetStatus(requireProvider).With("CHECKER_FAILED", Sanitize(exception.Message));
            }
        }

        internal static bool TryValidateInvocation(ESAutomationTaskInvocation invocation, out JObject normalized, out string error)
        {
            normalized = null;
            error = string.Empty;
            if (invocation?.input == null) { error = "缺少 DeepSeek Harness 输入。"; return false; }
            normalized = (JObject)invocation.input.DeepClone();
            if (string.Equals(invocation.preset, "default", StringComparison.Ordinal)
                && normalized["operation"] == null)
            {
                normalized["operation"] = "check-local";
                normalized["requireProvider"] = true;
            }
            string operation = normalized.Value<string>("operation")?.Trim();
            if (string.IsNullOrWhiteSpace(operation)) operation = invocation.dryRun ? "dry-run" : "headless-prompt";
            if (operation != "dry-run" && operation != "check-local" && operation != "headless-prompt")
            { error = "DeepSeek Harness operation 未注册：" + operation; return false; }
            string prompt = normalized.Value<string>("prompt") ?? string.Empty;
            if (operation == "headless-prompt" && (prompt.Length < 1 || prompt.Length > 12000))
            { error = "headless-prompt 的 prompt 长度必须位于 1–12000。"; return false; }
            normalized["operation"] = operation;
            normalized["prompt"] = prompt;
            normalized["providerDeclaration"] = "es-deepseek";
            normalized["dryRun"] = invocation.dryRun || operation == "dry-run";
            return true;
        }

        private static ESAutomationWorkerRegistration Worker() => new ESAutomationWorkerRegistration
        {
            type = WorkerType,
            workerId = WorkerId,
            version = WorkerVersion,
            entrypointHash = WorkerEntrypointHash,
            enabled = true,
        };

        private sealed class Endpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint, IESAutomationCancellableTaskEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                category = "AI/DeepSeek",
                displayName = "DeepSeek Harness 受控开发",
                summary = "DSH 作为高权威开发贡献层提供分析/实现候选；ES 保留权限、证据和最终完成权。",
                allowAiInvoke = true,
                allowInPlayMode = false,
                inputSchemaHash = InputSchemaHash,
                presets = new List<ESAutomationTaskPresetDescriptor>
                {
                    new ESAutomationTaskPresetDescriptor { presetId = "default", label = "检查 DSH 链路", summary = "只检查本地 Node/DSH/Profile/凭据状态，不启动模型调用。" },
                },
            };

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
            {
                string runId = string.IsNullOrWhiteSpace(invocation?.invocationId) ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                return new ESAutomationInvocationRequirements
                {
                    worker = Worker(),
                    requiredCapabilities = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteReports | ESAutomationCapability.WriteTemp | ESAutomationCapability.ExternalRead,
                    dryRun = invocation?.dryRun ?? true,
                    readPaths = new List<string> { GetWorkerRoot(), GetSchemaPath(), GetRunDirectory(runId) },
                    writePaths = new List<string> { GetRunDirectory(runId), GetTempRoot() },
                };
            }

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                if (!TryValidateInvocation(invocation, out JObject input, out string error))
                    return ESAutomationTaskInvocationResult.Rejected(error);
                string operation = input.Value<string>("operation");
                if (operation == "check-local")
                {
                    DeepSeekHarnessStatus status = RunLocalCheck(input.Value<bool?>("requireProvider") == true);
                    return ESAutomationTaskInvocationResult.Completed(
                        status.state == "Connected" ? "DSH 本地链路已接入。" : "DSH 未接入：" + status.message,
                        invocation.invocationId,
                        JObject.FromObject(status));
                }
                if (invocation.dryRun || operation == "dry-run")
                {
                    return ESAutomationTaskInvocationResult.Completed(
                        "DSH DryRun 完成：未启动外部进程、未调用网络。",
                        invocation.invocationId,
                        new JObject
                        {
                            ["frameworkId"] = "deepseek-harness",
                            ["declaration"] = "es-deepseek",
                            ["role"] = "external-execution-plane",
                            ["authority"] = "ESFramework/ESAI",
                            ["authorityLevel"] = "high-contributor-not-final-acceptance",
                            ["networkCalled"] = false,
                            ["mutationApplied"] = false,
                            ["runtimeStatus"] = "runtime-not-run",
                        });
                }

                DeepSeekHarnessStatus local = GetStatus(true);
                if (local.state != "Connected")
                    return ESAutomationTaskInvocationResult.Blocked("DSH 未接入：" + local.message, invocation.invocationId, JObject.FromObject(local));
                string runId = string.IsNullOrWhiteSpace(invocation.invocationId) ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                if (!Guid.TryParseExact(runId, "N", out _)) return ESAutomationTaskInvocationResult.Rejected("InvocationId 必须是 N 格式 GUID。");
                string directory = GetRunDirectory(runId);
                string requestPath = Path.Combine(directory, "request.json");
                string recordPath = Path.Combine(directory, "run-record.json");
                if (File.Exists(recordPath)) return GetRun(runId);
                if (Directory.Exists(directory)) return ESAutomationTaskInvocationResult.Rejected("RunId 目录存在但无有效 RunRecord，拒绝猜测恢复。");
                Directory.CreateDirectory(directory);
                input["projectRoot"] = ESAutomationPathPolicy.ProjectRoot;
                input["providerDeclaration"] = "es-deepseek";
                input["workerId"] = WorkerId;
                input["workerVersion"] = WorkerVersion;
                input["taskId"] = TaskId;
                input["runId"] = runId;
                input["entrypointHash"] = WorkerEntrypointHash;
                WriteJsonAtomic(requestPath, input);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var record = new ESAutomationRunRecord
                {
                    runId = runId, taskId = TaskId, taskVersion = TaskVersion, operatorId = invocation.actorId ?? string.Empty,
                    gitCommit = ESAutomationSourceState.GetCurrentGitCommit(), workerType = WorkerType, workerId = WorkerId,
                    workerVersion = WorkerVersion, entrypointHash = WorkerEntrypointHash, inputManifestHash = ComputeFileHash(requestPath),
                    invocationHash = ComputeFileHash(requestPath), status = ESAutomationRunStatus.Starting,
                    startedAtUtc = now.ToString("O"), lastUpdatedAtUtc = now.ToString("O"), operationDirectory = directory,
                };
                WriteJsonAtomic(recordPath, record);
                try
                {
                    ESAutomationProcessExecution execution = ESAutomationProcessRunner.Start(new ESAutomationProcessRequest
                    {
                        taskId = TaskId, taskVersion = TaskVersion, runId = runId, dryRun = false, inputContractPath = requestPath,
                    });
                    record.processId = execution.ProcessId;
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Running);
                    WriteJsonAtomic(recordPath, record);
                    ActiveRuns.Add(runId, new ActiveRun { Execution = execution, Record = record, RecordPath = recordPath, ResultPath = Path.Combine(directory, "result.json"), Directory = directory });
                    return ESAutomationTaskInvocationResult.Accepted("DSH 已由 ES 受信 Adapter 接受；ES 将继续收集证据并决定完成状态。", runId,
                        JObject.FromObject(new { role = "external-execution-plane", authority = "ESFramework/ESAI", authorityLevel = "high-contributor-not-final-acceptance", runtimeStatus = "running" }));
                }
                catch (Exception exception)
                {
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                    record.errors.Add(Sanitize(exception.Message));
                    WriteJsonAtomic(recordPath, record);
                    return ESAutomationTaskInvocationResult.Failed("DSH Worker 启动失败：" + Sanitize(exception.Message), runId);
                }
            }

            public ESAutomationTaskInvocationResult GetRun(string runId) => ESDeepSeekHarnessAutomation.GetRun(runId);
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission) => ESAutomationTaskInvocationResult.Rejected("DeepSeek Harness 不接受分阶段输入。");
            public ESAutomationTaskInvocationResult CancelRun(string runId, string actorId)
            {
                if (!ActiveRuns.TryGetValue(runId, out ActiveRun active)) return ESAutomationTaskInvocationResult.NotFound("未找到活动的 DSH Run。");
                try { active.Execution.Terminate(); } catch (Exception exception) { return ESAutomationTaskInvocationResult.Failed("DSH 取消未确认：" + Sanitize(exception.Message), runId); }
                return ESAutomationTaskInvocationResult.Accepted("已请求终止 DSH 进程树。", runId);
            }
        }

        private sealed class DeepSeekHarnessAdapter : IESAutomationWorkerAdapter
        {
            public string WorkerType => ESDeepSeekHarnessAutomation.WorkerType;
            public string WorkerId => ESDeepSeekHarnessAutomation.WorkerId;
            public ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract, ESAutomationProcessRequest request)
            {
                string root = ESAutomationPathPolicy.ProjectRoot;
                string workerPath = Path.Combine(GetWorkerRoot(), "worker.js");
                string nodePath = FirstNonEmpty(Environment.GetEnvironmentVariable("ES_DEEPSEEK_NODE_PATH"), Environment.GetEnvironmentVariable("ES_AUTOMATION_NODE_PATH"), ReadRuntimeConfig(root)?.Value<string>("nodePath"));
                string dshPath = FirstNonEmpty(Environment.GetEnvironmentVariable("DSH_EXECUTABLE"), ReadRuntimeConfig(root)?.Value<string>("dshExecutable"), Path.Combine(GetWorkerRoot(), "node_modules", ".bin", "dsh.cmd"));
                if (string.IsNullOrWhiteSpace(nodePath) || !Path.IsPathRooted(nodePath) || !File.Exists(nodePath)) throw new InvalidOperationException("DSH 未接入：没有有效的绝对 node.exe。");
                if (string.IsNullOrWhiteSpace(dshPath) || !Path.IsPathRooted(dshPath) || !File.Exists(dshPath)) throw new InvalidOperationException("DSH 未接入：没有受管 dsh.cmd。");
                if (!File.Exists(workerPath) || !File.Exists(Path.Combine(GetWorkerRoot(), "package-lock.json"))) throw new InvalidOperationException("DSH 未接入：Worker 或 package-lock 缺失。");
                JObject config = ReadRuntimeConfig(root);
                string dshHome = config?.Value<string>("dshHome") ?? GetTempRoot();
                string workspace = config?.Value<string>("workspace") ?? Path.Combine(GetTempRoot(), "workspace");
                EnsureProjectPath(dshHome, "DSH_HOME", root); EnsureProjectPath(workspace, "workspace", root);
                var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DSH_EXECUTABLE"] = Path.GetFullPath(dshPath),
                    ["DSH_HOME"] = Path.GetFullPath(dshHome),
                    ["ES_DEEPSEEK_NODE_PATH"] = Path.GetFullPath(nodePath),
                };
                var info = new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(nodePath),
                    Arguments = Quote(workerPath) + " " + Quote(request.inputContractPath) + " " + Quote(GetRunDirectory(request.runId)),
                    WorkingDirectory = GetWorkerRoot(), UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(false, true), StandardErrorEncoding = new UTF8Encoding(false, true),
                };
                foreach (KeyValuePair<string, string> pair in environment) info.EnvironmentVariables[pair.Key] = pair.Value;
                return info;
            }
        }

        private sealed class ActiveRun
        {
            public ESAutomationProcessExecution Execution;
            public ESAutomationRunRecord Record;
            public string RecordPath;
            public string ResultPath;
            public string Directory;
        }

        [Serializable]
        internal sealed class DeepSeekHarnessStatus
        {
            public string state = "NotConnected";
            public string reasonCode = string.Empty;
            public string message = string.Empty;
            public string role = "external-execution-plane";
            public string authority = "ESFramework/ESAI";
            public string authorityLevel = "high-contributor-not-final-acceptance";
            public bool providerConfigured;
            public string runtimeStatus = "runtime-not-run";
            public string nextAction = string.Empty;
            public DeepSeekHarnessStatus With(string code, string detail) { reasonCode = code; message = detail; return this; }
        }

        private static void PollActiveRuns()
        {
            foreach (ActiveRun active in ActiveRuns.Values.ToList())
            {
                try
                {
                    if (active.Execution.EnforceTimeout(DateTimeOffset.UtcNow)) { FinishWithoutResult(active, ESAutomationRunStatus.TimedOut, "DSH Worker 超时。"); continue; }
                    if (!active.Execution.HasExited) continue;
                    FinalizeResult(active);
                }
                catch (Exception exception) { FinishWithoutResult(active, ESAutomationRunStatus.Failed, exception.Message); }
                finally
                {
                    if (active.Execution.HasExited) { active.Execution.Dispose(); ActiveRuns.Remove(active.Record.runId); }
                }
            }
        }

        private static void FinalizeResult(ActiveRun active)
        {
            if (!File.Exists(active.ResultPath)) throw new InvalidDataException("DSH Worker 已退出但没有 result.json。");
            ESAutomationRunResult result = JsonConvert.DeserializeObject<ESAutomationRunResult>(File.ReadAllText(active.ResultPath, new UTF8Encoding(false, true)));
            if (result == null) throw new InvalidDataException("DSH result.json 为空。");
            result.Validate();
            if (result.taskId != TaskId || result.taskVersion != TaskVersion || result.runId != active.Record.runId
                || result.workerType != WorkerType || result.workerId != WorkerId || result.workerVersion != WorkerVersion
                || !string.Equals(result.entrypointHash, WorkerEntrypointHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(result.inputManifestHash, active.Record.inputManifestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("DSH Worker 结果身份或输入 Hash 不匹配。");
            active.Record.exitCode = result.exitCode;
            active.Record.outputs = result.outputs ?? new List<string>();
            active.Record.outputHashes = result.outputHashes ?? new List<string>();
            active.Record.findings = result.findings ?? new List<string>();
            active.Record.errors = result.errors ?? new List<string>();
            active.Record.completionDecision = result.completionDecision;
            string finalStatus = result.status == "Passed" ? ESAutomationRunStatus.Completed : result.status == "Blocked" ? ESAutomationRunStatus.Blocked : result.status == "Cancelled" ? ESAutomationRunStatus.Cancelled : ESAutomationRunStatus.Failed;
            ESAutomationRunStatus.Transition(active.Record, finalStatus);
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
            string recordPath = Path.Combine(GetRunDirectory(runId), "run-record.json");
            if (!File.Exists(recordPath)) return ESAutomationTaskInvocationResult.NotFound("未找到 DSH RunRecord。");
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(File.ReadAllText(recordPath, new UTF8Encoding(false, true)));
            var data = new JObject { ["status"] = record.status, ["exitCode"] = record.exitCode, ["outputs"] = JArray.FromObject(record.outputs ?? new List<string>()), ["findings"] = JArray.FromObject(record.findings ?? new List<string>()), ["errors"] = JArray.FromObject(record.errors ?? new List<string>()), ["role"] = "external-execution-plane", ["authority"] = "ESFramework/ESAI" };
            return ESAutomationRunStatus.IsTerminal(record.status) ? ESAutomationTaskInvocationResult.Completed("DSH Run 已结束：" + record.status, runId, data) : ESAutomationTaskInvocationResult.Starting("DSH Run 当前状态：" + record.status, runId, data);
        }

        private static void ReconcileInterruptedRuns()
        {
            string root = GetRoot();
            if (!Directory.Exists(root)) return;
            foreach (string directory in Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(MaximumRecoveredRuns))
            {
                string path = Path.Combine(directory, "run-record.json");
                if (!File.Exists(path)) continue;
                try
                {
                    ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(File.ReadAllText(path, new UTF8Encoding(false, true)));
                    if (record == null || ESAutomationRunStatus.IsTerminal(record.status)) continue;
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed); record.errors.Add("Editor 重启/域重载后 DSH 进程不可恢复，已保守终结。"); WriteJsonAtomic(path, record);
                }
                catch (Exception exception) { Debug.LogError("DSH RunRecord 恢复失败：" + Sanitize(exception.Message)); }
            }
        }

        private static void StopActiveRunsForLifecycle()
        {
            foreach (ActiveRun active in ActiveRuns.Values.ToList())
            {
                try { if (!active.Execution.HasExited) active.Execution.Terminate(); FinishWithoutResult(active, ESAutomationRunStatus.Failed, "Editor 生命周期结束，DSH 进程已终止。"); } catch (Exception exception) { Debug.LogError("DSH 生命周期终止未确认：" + Sanitize(exception.Message)); }
            }
            ActiveRuns.Clear();
        }

        private static string GetRoot() => Path.Combine(ESAutomationPathPolicy.RunsRoot, "DeepSeekHarness");
        private static string GetRunDirectory(string runId) => Path.Combine(GetRoot(), runId);
        private static string GetTempRoot() => Path.Combine(ESAutomationPathPolicy.TempRoot, "DeepSeekHarness");
        private static string GetWorkerRoot() => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers", "Node", "DeepSeekHarness");
        private static string GetSchemaPath() => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-deepseek-harness-v1.schema.json");
        private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        private static string GetRuntimeConfigPath(string root) => Path.Combine(root, "ES", "Automation", "Temp", "DeepSeekHarness", "runtime.local.json");
        private static JObject ReadRuntimeConfig(string root) { string path = GetRuntimeConfigPath(root); if (!File.Exists(path)) return null; try { return JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true))); } catch { return null; } }
        private static void EnsureProjectPath(string path, string name, string root) { if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(name + " 必须是项目根内绝对路径。"); }
        private static string ComputeFileHash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(); }
        private static void WriteJsonAtomic(string path, object value) { Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException()); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; File.WriteAllText(temp, JsonConvert.SerializeObject(value, Formatting.Indented) + "\n", new UTF8Encoding(false, true)); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        private static string Sanitize(string value) { string result = value ?? string.Empty; foreach (string name in new[] { "DEEPSEEK_API_KEY", "DSH_API_KEY" }) { string secret = Environment.GetEnvironmentVariable(name); if (!string.IsNullOrEmpty(secret)) result = result.Replace(secret, "[REDACTED]"); } return result.Length <= 2000 ? result : result.Substring(0, 2000); }
    }

    internal sealed class ESDeepSeekHarnessAutomationInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke() => ESDeepSeekHarnessAutomation.Register();
    }
}
