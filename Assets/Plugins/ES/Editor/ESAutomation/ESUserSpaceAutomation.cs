using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>受管 UserSpace 注册卡入口。脚本路径、项目根和参数形状均由代码固定。</summary>
    internal static class ESUserSpaceAutomation
    {
        internal const string TaskId = "es.userspace.profile";
        internal const int TaskVersion = 1;
        private const string WorkerType = "PowerShell";
        private const string WorkerId = "es.userspace.profile";
        private const string WorkerVersion = "1.0.0";
        private const string EntrypointHash = "2bbba33e81f79b718fd28407a70b5fff204534ff96e3b516da7f5c57bfcd3a70";
        private const string InputSchemaHash = "1e880caeade266dba0a0f6a00b1b0d4a9943ca55934915a3ec7760e0b2fcf333";

        internal static void Register()
        {
            ESAutomationTaskContract contract;
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out contract))
            {
                contract = new ESAutomationTaskContract
                {
                    taskId = TaskId, version = TaskVersion,
                    worker = Worker(),
                    inputs = new List<string> { "userspace-request.json" },
                    readRoots = new List<string> { "ES/AISpace/Public/People", "ES/Automation/UserSpace" },
                    writeRoots = new List<string> { "ES/AISpace/Public/People", "ES/Automation/Runs/UserSpace" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteReports" },
                    inputSchemaHash = InputSchemaHash, timeoutSeconds = 30, supportsDryRun = true,
                    outputs = new List<string> { "userspace-result.json" },
                    performanceBudget = new ESAutomationPerformanceBudget { maxDurationSeconds = 30, maxOutputBytes = 256 * 1024, maxRetryCount = 0, maxFindingCount = 100 },
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            else if (contract.worker == null || contract.worker.workerId != WorkerId
                || !string.Equals(contract.worker.entrypointHash, EntrypointHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UserSpace TaskContract Worker 身份或入口哈希不一致。");
            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new Endpoint());
        }

        private static ESAutomationWorkerRegistration Worker() => new ESAutomationWorkerRegistration
        { type = WorkerType, workerId = WorkerId, version = WorkerVersion, entrypointHash = EntrypointHash, enabled = true };

        private sealed class Endpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor
            { taskId = TaskId, taskVersion = TaskVersion, category = "AI/UserSpace", displayName = "UserSpace 用户区域", summary = "通过固定 Worker 初始化、更新、发现和验证用户公共注册卡。", allowAiInvoke = true, allowInPlayMode = false, inputSchemaHash = InputSchemaHash };

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
            {
                string action = invocation?.input?.Value<string>("action") ?? "Validate";
                return new ESAutomationInvocationRequirements { worker = Worker(), requiredCapabilities = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteReports, dryRun = invocation?.dryRun ?? true, readPaths = new List<string> { ESAutomationPathPolicy.PublicPeopleRoot, Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "UserSpace") }, writePaths = new List<string> { ESAutomationPathPolicy.PublicPeopleRoot, Path.Combine(ESAutomationPathPolicy.RunsRoot, "UserSpace") } };
            }

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
            {
                JObject input = invocation?.input;
                string action = input?.Value<string>("action") ?? string.Empty;
                if (action != "Initialize" && action != "Update" && action != "Discover" && action != "Validate") return ESAutomationTaskInvocationResult.Rejected("UserSpace action 无效。");
                if (invocation.dryRun && (action == "Initialize" || action == "Update")) return ESAutomationTaskInvocationResult.Completed("UserSpace DryRun：未写入注册卡。", invocation.invocationId, new JObject { ["action"] = action, ["runtimeStatus"] = "runtime-not-run" });
                string runId = string.IsNullOrWhiteSpace(invocation.invocationId) ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                try
                {
                    string output = RunFixedWorker(input, action);
                    WriteRunRecord(runId, action, "Completed", string.Empty);
                    return ESAutomationTaskInvocationResult.Completed("UserSpace Worker 已完成。", runId, new JObject { ["action"] = action, ["output"] = output, ["runtimeStatus"] = "runtime-not-run" });
                }
                catch (Exception ex) { WriteRunRecord(runId, action, "Failed", ex.Message); return ESAutomationTaskInvocationResult.Failed("UserSpace Worker 失败：" + ex.Message, runId); }
            }

            public ESAutomationTaskInvocationResult GetRun(string runId) { string path = Path.Combine(ESAutomationPathPolicy.RunsRoot, "UserSpace", runId, "run-record.json"); if (!File.Exists(path)) return ESAutomationTaskInvocationResult.NotFound("未找到 UserSpace RunRecord。"); return ESAutomationTaskInvocationResult.Completed("UserSpace RunRecord。", runId, JObject.Parse(File.ReadAllText(path, Encoding.UTF8))); }
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission) => ESAutomationTaskInvocationResult.Rejected("UserSpace 不接受分阶段输入。");

            private static string RunFixedWorker(JObject input, string action)
            {
                string script = Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "UserSpace", "Invoke-ESUserSpace.ps1");
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(script) || !File.Exists(powershell)) throw new FileNotFoundException("固定 UserSpace Worker 入口不可用。");
                var args = new StringBuilder("-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ").Append(Q(script)).Append(" -Action ").Append(Q(action)).Append(" -ProjectRoot ").Append(Q(ESAutomationPathPolicy.ProjectRoot));
                Add(input, args, "PersonId"); Add(input, args, "DisplayName"); Add(input, args, "Kind"); Add(input, args, "ExpectedRevision"); Add(input, args, "BranchStrategy"); Add(input, args, "MergePolicy"); Add(input, args, "WorkingHours"); Add(input, args, "Language"); Add(input, args, "Contact"); Add(input, args, "PrivateStorageClass"); Add(input, args, "PrivateLocator"); if (input.Value<bool?>("TransferOwnership") == true) { args.Append(" -TransferOwnership"); if (input.Value<bool?>("ConfirmTeamMember") == true) args.Append(" -ConfirmTeamMember"); if (input.Value<bool?>("ConfirmVisibility") == true) args.Append(" -ConfirmVisibility"); if (input.Value<bool?>("ConfirmPreviousOwnerLockout") == true) args.Append(" -ConfirmPreviousOwnerLockout"); Add(input, args, "TakeoverReason"); }
                AddArray(input, args, "Responsibilities"); AddArray(input, args, "DiscoverableRoutes");
                var psi = new System.Diagnostics.ProcessStartInfo(powershell, args.ToString()) { WorkingDirectory = ESAutomationPathPolicy.ProjectRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false) };
                using (var process = System.Diagnostics.Process.Start(psi)) { string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit(30000); if (!process.HasExited) { process.Kill(); throw new TimeoutException("UserSpace Worker 超时。"); } if (process.ExitCode != 0) throw new InvalidOperationException(stderr.Trim()); return stdout.Trim(); }
            }
            private static void WriteRunRecord(string runId, string action, string status, string error) { string path = Path.Combine(ESAutomationPathPolicy.RunsRoot, "UserSpace", runId, "run-record.json"); ESAutomationPathPolicy.WriteWorkerTextAtomic(path, new JObject { ["taskId"] = TaskId, ["taskVersion"] = TaskVersion, ["runId"] = runId, ["action"] = action, ["status"] = status, ["error"] = error ?? string.Empty, ["updatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O") }.ToString(Newtonsoft.Json.Formatting.None), new[] { ESAutomationPathPolicy.RunsRoot, Path.Combine(ESAutomationPathPolicy.RunsRoot, "UserSpace") }); }
            private static void Add(JObject input, StringBuilder args, string name) { JToken value = input?[name]; if (value != null && value.Type != JTokenType.Null) args.Append(" -").Append(name).Append(' ').Append(Q(value.ToString())); }
            private static void AddArray(JObject input, StringBuilder args, string name) { JArray values = input?[name] as JArray; if (values == null) return; args.Append(" -").Append(name); foreach (JToken value in values) args.Append(' ').Append(Q(value.ToString())); }
            private static string Q(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
