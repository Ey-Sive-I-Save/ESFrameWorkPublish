using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>受管 TeamSpace 注册卡入口；AI 菜单选择后通过此 Endpoint 执行固定 Worker。</summary>
    // Worker Completed is an execution result only; it never declares project
    // acceptance. TaskContext/ABCD remains the final decision authority.
    internal static class ESTeamSpaceAutomation
    {
        internal const string TaskId = "es.teamspace.profile";
        internal const int TaskVersion = 1;
        private const string WorkerId = "es.teamspace.profile";
        private const string WorkerVersion = "1.0.0";
        private const string EntrypointHash = "7e7e40b7f1834e4202cfc00c9c03aa03c9c0de245226a907642bf696e30ad4c2";
        private const string InputSchemaHash = "d3927c99243d30f63fcf788d26df8700edb43fd2aa5227e54f718cb722f739ac";

        internal static void Register()
        {
            ESAutomationTaskContract contract;
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out contract))
            {
                contract = new ESAutomationTaskContract
                {
                    taskId = TaskId, version = TaskVersion, worker = Worker(),
                    inputs = new List<string> { "teamspace-request.json" },
                    readRoots = new List<string> { "ES/AISpace/Public/Teams", "ES/AISpace/Public/People", "ES/Automation/UserSpace" },
                    writeRoots = new List<string> { "ES/AISpace/Public/Teams", "ES/Automation/Runs/TeamSpace" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteReports" },
                    inputSchemaHash = InputSchemaHash, timeoutSeconds = 30, supportsDryRun = true,
                    outputs = new List<string> { "teamspace-result.json" },
                    performanceBudget = new ESAutomationPerformanceBudget { maxDurationSeconds = 30, maxOutputBytes = 256 * 1024, maxRetryCount = 0, maxFindingCount = 100 }
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            else if (contract.worker == null || contract.worker.workerId != WorkerId || !string.Equals(contract.worker.entrypointHash, EntrypointHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("TeamSpace TaskContract Worker 身份或入口哈希不一致。");
            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _)) ESAutomationFacade.Register(new Endpoint());
        }

        private static ESAutomationWorkerRegistration Worker() => new ESAutomationWorkerRegistration { type = "PowerShell", workerId = WorkerId, version = WorkerVersion, entrypointHash = EntrypointHash, enabled = true };

        private sealed class Endpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor { taskId = TaskId, taskVersion = TaskVersion, category = "AI/TeamSpace", displayName = "ES Team 团队协作区", summary = "通过固定 Worker 初始化、更新、发现和验证团队协作区。", allowAiInvoke = true, allowInPlayMode = false, inputSchemaHash = InputSchemaHash };
            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation) => new ESAutomationInvocationRequirements { worker = Worker(), requiredCapabilities = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteReports, dryRun = invocation?.dryRun ?? true, readPaths = new List<string> { Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "AISpace", "Public", "Teams"), Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "AISpace", "Public", "People") }, writePaths = new List<string> { Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "AISpace", "Public", "Teams"), Path.Combine(ESAutomationPathPolicy.RunsRoot, "TeamSpace") } };
            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                JObject input = invocation?.input; string action = input?.Value<string>("action") ?? string.Empty;
                if (action != "Initialize" && action != "Update" && action != "Discover" && action != "Validate") return ESAutomationTaskInvocationResult.Rejected("TeamSpace action 无效。");
                if (invocation.dryRun && (action == "Initialize" || action == "Update")) return ESAutomationTaskInvocationResult.Completed("TeamSpace DryRun：未写入注册卡。", invocation.invocationId, new JObject { ["action"] = action, ["runtimeStatus"] = "runtime-not-run" });
                string runId = string.IsNullOrWhiteSpace(invocation.invocationId) ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                try { string output = RunFixedWorker(input, action); WriteRunRecord(runId, action, "Completed", string.Empty); return ESAutomationTaskInvocationResult.Completed("TeamSpace Worker 已完成。", runId, new JObject { ["action"] = action, ["output"] = output, ["runtimeStatus"] = "runtime-not-run" }); }
                catch (Exception ex) { WriteRunRecord(runId, action, "Failed", ex.Message); return ESAutomationTaskInvocationResult.Failed("TeamSpace Worker 失败：" + ex.Message, runId); }
            }
            public ESAutomationTaskInvocationResult GetRun(string runId) { string path = Path.Combine(ESAutomationPathPolicy.RunsRoot, "TeamSpace", runId, "run-record.json"); if (!File.Exists(path)) return ESAutomationTaskInvocationResult.NotFound("未找到 TeamSpace RunRecord。"); return ESAutomationTaskInvocationResult.Completed("TeamSpace RunRecord。", runId, JObject.Parse(File.ReadAllText(path, Encoding.UTF8))); }
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission) => ESAutomationTaskInvocationResult.Rejected("TeamSpace 不接受分阶段输入。");
            private static string RunFixedWorker(JObject input, string action)
            {
                string script = Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "UserSpace", "Invoke-ESTeamSpace.ps1"); string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"); if (!File.Exists(script) || !File.Exists(powershell)) throw new FileNotFoundException("固定 TeamSpace Worker 入口不可用。");
                var args = new StringBuilder("-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ").Append(Q(script)).Append(" -Action ").Append(Q(action)).Append(" -ProjectRoot ").Append(Q(ESAutomationPathPolicy.ProjectRoot));
                Add(input, args, "TeamId"); Add(input, args, "DisplayName"); Add(input, args, "Mission"); Add(input, args, "BranchStrategy"); Add(input, args, "MergePolicy"); Add(input, args, "ExpectedRevision"); if (input.Value<bool?>("TransferOwnership") == true) args.Append(" -TransferOwnership"); AddArray(input, args, "MemberPersonIds"); AddArray(input, args, "MemberRoles");
                var psi = new ProcessStartInfo(powershell, args.ToString()) { WorkingDirectory = ESAutomationPathPolicy.ProjectRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false) };
                using (var process = Process.Start(psi)) { string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit(30000); if (!process.HasExited) { process.Kill(); throw new TimeoutException("TeamSpace Worker 超时。"); } if (process.ExitCode != 0) throw new InvalidOperationException(stderr.Trim()); return stdout.Trim(); }
            }
            private static void WriteRunRecord(string runId, string action, string status, string error) { string path = Path.Combine(ESAutomationPathPolicy.RunsRoot, "TeamSpace", runId, "run-record.json"); ESAutomationPathPolicy.WriteWorkerTextAtomic(path, new JObject { ["taskId"] = TaskId, ["taskVersion"] = TaskVersion, ["runId"] = runId, ["action"] = action, ["status"] = status, ["error"] = error ?? string.Empty, ["updatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O") }.ToString(Newtonsoft.Json.Formatting.None), new[] { ESAutomationPathPolicy.RunsRoot, Path.Combine(ESAutomationPathPolicy.RunsRoot, "TeamSpace") }); }
            private static void Add(JObject input, StringBuilder args, string name) { JToken value = input?[name]; if (value != null && value.Type != JTokenType.Null) args.Append(" -").Append(name).Append(' ').Append(Q(value.ToString())); }
            private static void AddArray(JObject input, StringBuilder args, string name) { JArray values = input?[name] as JArray; if (values == null) return; args.Append(" -").Append(name); foreach (JToken value in values) args.Append(' ').Append(Q(value.ToString())); }
            private static string Q(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
