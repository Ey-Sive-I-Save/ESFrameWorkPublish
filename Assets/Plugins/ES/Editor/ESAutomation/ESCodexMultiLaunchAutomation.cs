using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>
    /// AITalk 的受管 Codex 多启动入口。AITalk 只能通过 AIBrain planTask/runTask
    /// 到达这里；脚本路径、输出根和参数形状均固定，不能注入任意命令行。
    /// </summary>
    // Invocation Completed/Accepted is orchestration status only; it never declares task completion; authoritative downstream decision remains separate.
    internal static class ESCodexMultiLaunchAutomation
    {
        internal const string TaskId = "es.codex.multilaunch";
        internal const int TaskVersion = 1;
        private const string WorkerType = "PowerShell";
        private const string WorkerId = "es.codex.multilaunch";
        private const string WorkerVersion = "1.0.0";
        private const string EntrypointHash = "b66d075d848ba1e6403675eb07bf61e12a75a16afbb93093e8455980c6144315";
        private const string InputSchemaHash = "9c09ff1e53d2090ea751e9a9a8ced16acffe1a2443ad0a9827642324fda410d3";
        private const int TimeoutSeconds = 900;
        private const ESAutomationCapability Capabilities =
            ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteReports | ESAutomationCapability.ExternalWrite;

        private static string WorkerPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers", "PowerShell", "Invoke-ESCodexMultiLaunchWorker.ps1");
        private static string SchemaPath => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-codex-multilaunch-request-v1.schema.json");
        private static string RunsRoot => Path.Combine(ESAutomationPathPolicy.RunsRoot, "CodexMultiLaunch");

        internal static void Register()
        {
            VerifyBindings();
            ESAutomationTaskContract contract;
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out contract))
            {
                contract = new ESAutomationTaskContract
                {
                    taskId = TaskId, version = TaskVersion, worker = Worker(),
                    inputs = new List<string> { "request.json" },
                    readRoots = new List<string> { "ES/Automation/Runs/CodexMultiLaunch", "ES/Automation/Contracts" },
                    writeRoots = new List<string> { "ES/Automation/Runs/CodexMultiLaunch" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteReports", "ExternalWrite" },
                    inputSchemaHash = InputSchemaHash, timeoutSeconds = TimeoutSeconds,
                    supportsDryRun = true, supportsRetry = false,
                    outputs = new List<string> { "launch-plan.json", "multilaunch-result.json", "run-record.json" },
                    acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        authorityDomain = "editor-tooling",
                        authorityRiskClass = "high",
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "codex-multilaunch.acceptance-receipts",
                                verifierId = "es.codex.multilaunch.per-launch-acceptance",
                                description = "每个职责窗口必须有独立 acceptance 回执；编排结果不能替代逐项验收。",
                            },
                        },
                    },
                    performanceBudget = new ESAutomationPerformanceBudget { maxDurationSeconds = TimeoutSeconds, maxOutputBytes = 1024 * 1024, maxRetryCount = 0, maxFindingCount = 128 },
                    capabilityEnvelope = new ESAutomationCapabilityEnvelope { userAuthorization = Capabilities, taskContract = Capabilities, aiCommand = Capabilities, workerCapability = Capabilities, projectBoundary = Capabilities },
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _)) ESAutomationFacade.Register(new Endpoint());
        }

        private static ESAutomationWorkerRegistration Worker() => new ESAutomationWorkerRegistration { type = WorkerType, workerId = WorkerId, version = WorkerVersion, entrypointHash = EntrypointHash, enabled = true };

        private static void VerifyBindings()
        {
            if (!File.Exists(WorkerPath) || !File.Exists(SchemaPath)) throw new InvalidOperationException("Codex MultiLaunch Worker 或输入 Schema 缺失。");
            if (!string.Equals(ComputeHash(WorkerPath), EntrypointHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(ComputeHash(SchemaPath), InputSchemaHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Codex MultiLaunch Worker/Schema Hash 漂移。");
        }

        private static string ComputeHash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class Endpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor { taskId = TaskId, taskVersion = TaskVersion, category = "AI/Codex", displayName = "AITalk 全自动 Codex 多启动", summary = "经 AIBrain 计划、用户授权和 TaskContract 交集后，分波次启动 Codex 并回收逐项 acceptance。", allowAiInvoke = true, allowInPlayMode = false, inputSchemaHash = InputSchemaHash };

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation) => new ESAutomationInvocationRequirements { worker = Worker(), requiredCapabilities = Capabilities, dryRun = invocation == null || invocation.dryRun, readPaths = new List<string> { SchemaPath, Path.Combine(ESAutomationPathPolicy.ProjectRoot, ".agents", "skills", "es-codex-session-bootstrap") }, writePaths = new List<string> { RunsRoot } };

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                if (invocation == null || invocation.input == null) return ESAutomationTaskInvocationResult.Rejected("Codex MultiLaunch 缺少输入。");
                if (!TryValidate(invocation.input, out string error)) return ESAutomationTaskInvocationResult.Rejected(error);
                string runId = string.IsNullOrWhiteSpace(invocation.invocationId) ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                string directory = Path.Combine(RunsRoot, runId);
                string requestPath = Path.Combine(directory, "request.json");
                string resultPath = Path.Combine(directory, "multilaunch-result.json");
                if (File.Exists(resultPath)) return ESAutomationTaskInvocationResult.Completed("Codex MultiLaunch 已有相同 RunId 的结果。", runId, JObject.Parse(File.ReadAllText(resultPath, Encoding.UTF8)));
                Directory.CreateDirectory(directory);
                ESAutomationPathPolicy.WriteWorkerTextAtomic(requestPath, invocation.input.ToString(Formatting.None), new[] { RunsRoot });
                if (invocation.dryRun)
                {
                    JObject data = new JObject { ["runId"] = runId, ["status"] = "DryRun", ["runtimeStatus"] = "runtime-not-run", ["request"] = invocation.input.DeepClone() };
                    WriteRecord(runId, "DryRun", string.Empty); return ESAutomationTaskInvocationResult.Completed("Codex MultiLaunch DryRun 已完成。", runId, data);
                }
                try
                {
                    string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                    if (!File.Exists(powershell)) throw new FileNotFoundException("Windows PowerShell 入口不可用。");
                    var psi = new ProcessStartInfo(powershell, "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + WorkerPath.Replace("\"", "\\\"") + "\" -InputPath \"" + requestPath.Replace("\"", "\\\"") + "\" -OutputDirectory \"" + directory.Replace("\"", "\\\"") + "\" -ProjectRoot \"" + ESAutomationPathPolicy.ProjectRoot.Replace("\"", "\\\"") + "\"") { WorkingDirectory = ESAutomationPathPolicy.ProjectRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false) };
                    using (var process = Process.Start(psi))
                    {
                        string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit(TimeoutSeconds * 1000);
                        if (!process.HasExited) { try { process.Kill(); } catch { } throw new TimeoutException("Codex MultiLaunch Worker 超时。"); }
                        if (process.ExitCode != 0) throw new InvalidOperationException(stderr.Trim());
                        WriteRecord(runId, "Completed", string.Empty);
                        return ESAutomationTaskInvocationResult.Completed("AITalk Codex MultiLaunch 已完成启动编排；请以逐项 acceptance 字段判断结果。", runId, new JObject { ["result"] = stdout, ["runtimeStatus"] = "runtime-executed" });
                    }
                }
                catch (Exception ex) { WriteRecord(runId, "Failed", ex.Message); return ESAutomationTaskInvocationResult.Failed("Codex MultiLaunch Worker 失败：" + ex.Message, runId); }
            }

            public ESAutomationTaskInvocationResult GetRun(string runId) { string path = Path.Combine(RunsRoot, runId, "run-record.json"); if (!File.Exists(path)) return ESAutomationTaskInvocationResult.NotFound("未找到 Codex MultiLaunch RunRecord。"); return ESAutomationTaskInvocationResult.Completed("Codex MultiLaunch RunRecord。", runId, JObject.Parse(File.ReadAllText(path, Encoding.UTF8))); }
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission) => ESAutomationTaskInvocationResult.Rejected("Codex MultiLaunch 不接受分阶段输入。");

            private static bool TryValidate(JObject input, out string error)
            {
                error = string.Empty;
                if (input.Value<string>("batchId") == null || input["launches"] == null || input["launches"].Type != JTokenType.Array || input["launches"].Count() < 1) { error = "Codex MultiLaunch 必须包含 batchId 和 launches。"; return false; }
                int max = input.Value<int?>("maxParallel") ?? 0; if (max < 1 || max > 16) { error = "maxParallel 必须在 1–16。"; return false; }
                return true;
            }
            private static void WriteRecord(string runId, string status, string error) { string path = Path.Combine(RunsRoot, runId, "run-record.json"); ESAutomationPathPolicy.WriteWorkerTextAtomic(path, new JObject { ["taskId"] = TaskId, ["taskVersion"] = TaskVersion, ["runId"] = runId, ["status"] = status, ["error"] = error ?? string.Empty, ["updatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O") }.ToString(Formatting.None), new[] { RunsRoot }); }
        }
    }
}
