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

namespace ES
{
    /// <summary>
    /// Feishu 第一阶段只读接入。任务只能执行固定 auth/search/pull 操作，凭据仅从进程环境读取。
    /// </summary>
    internal static class ESFeishuReadAutomation
    {
        internal const string TaskId = "es.feishu.read";
        internal const int TaskVersion = 1;
        internal const string WorkerType = "Other";
        internal const string WorkerId = "es.feishu.node";
        internal const string WorkerVersion = "0.1.0";
        internal const string WorkerEntrypointHash = "16b419b0452b9761e9d8f08acbf1e65ff815d8ac3043773df3e146786a4c887e";
        internal const string PackageLockHash = "f12bad503b40ce56b7dedf47bb7e98846d10dbcf4f00bcd95ed6881f98ed9f40";
        private const int TimeoutSeconds = 60;

        private static readonly Dictionary<string, ActiveRun> ActiveRuns =
            new Dictionary<string, ActiveRun>(StringComparer.Ordinal);

        internal static void Register()
        {
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract existing))
            {
                ESAutomationTaskRegistry.Register(new ESAutomationTaskContract
                {
                    taskId = TaskId,
                    version = TaskVersion,
                    worker = CreateWorkerRegistration(),
                    inputs = new List<string> { "request.json" },
                    readRoots = new List<string> { "ES/Automation/Temp" },
                    writeRoots = new List<string> { "ES/Automation/Temp" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteTemp" },
                    timeoutSeconds = TimeoutSeconds,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "feishu-data.json", "result.json", "run-record.json" },
                    acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        freshnessPolicy = new ESAutomationFreshnessPolicy { maxAgeHours = 168, requireSourceHash = true, allowRuntimeNotRun = true },
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "feishu.outputs-hashed",
                                verifierId = "es.feishu.output-hash",
                                description = "Every declared Feishu output exists inside the run directory and matches its hash.",
                            },
                        },
                    },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = TimeoutSeconds,
                        maxOutputBytes = 8L * 1024L * 1024L,
                        maxRetryCount = 0,
                        maxFindingCount = 1000,
                    },
                });
            }
            else
            {
                ValidateExistingContract(existing);
                if (existing.acceptanceCriteria == null)
                {
                    existing.acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "feishu.outputs-hashed",
                                verifierId = "es.feishu.output-hash",
                                description = "Every declared Feishu output exists and matches its hash.",
                            },
                        },
                    };
                }
                existing.acceptanceCriteria.Validate();
                if (existing.performanceBudget == null)
                {
                    existing.performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = TimeoutSeconds,
                        maxOutputBytes = 8L * 1024L * 1024L,
                        maxRetryCount = 0,
                        maxFindingCount = 1000,
                    };
                }
                existing.performanceBudget.Validate();
            }

            if (!ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId))
                ESAutomationProcessRunner.RegisterAdapter(new FeishuNodeAdapter());
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new FeishuEndpoint());

            EditorApplication.update -= PollActiveRuns;
            EditorApplication.update += PollActiveRuns;
        }

        private static ESAutomationWorkerRegistration CreateWorkerRegistration()
            => new ESAutomationWorkerRegistration
            {
                type = WorkerType,
                workerId = WorkerId,
                version = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                enabled = true,
            };

        private static void ValidateExistingContract(ESAutomationTaskContract contract)
        {
            if (contract.worker == null
                || contract.worker.type != WorkerType
                || contract.worker.workerId != WorkerId
                || contract.worker.version != WorkerVersion
                || !string.Equals(contract.worker.entrypointHash, WorkerEntrypointHash,
                    StringComparison.OrdinalIgnoreCase)
                || !contract.worker.enabled)
                throw new InvalidOperationException("已有 Feishu TaskContract 与受信 Worker 身份不一致。");
        }

        private static ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
        {
            if (!TryNormalizeInput(invocation, out JObject normalized, out string error))
                return ESAutomationTaskInvocationResult.Rejected(error);
            if (!TryResolveNode(out _, out string nodeError))
                return ESAutomationTaskInvocationResult.Blocked(nodeError);
            if (!invocation.dryRun && !HasCredentials())
                return ESAutomationTaskInvocationResult.Blocked(
                    "缺少 ES_FEISHU_APP_ID 或 ES_FEISHU_APP_SECRET；凭据不得写入请求、日志或项目文件。");

            string runId = string.IsNullOrWhiteSpace(invocation.invocationId)
                ? Guid.NewGuid().ToString("N") : invocation.invocationId;
            string directory = GetRunDirectory(runId);
            string requestPath = Path.Combine(directory, "request.json");
            string recordPath = Path.Combine(directory, "run-record.json");
            string resultPath = Path.Combine(directory, "result.json");
            string invocationHash = Sha256(normalized.ToString(Formatting.None)
                + "|dryRun=" + invocation.dryRun);

            if (File.Exists(recordPath))
            {
                ESAutomationRunRecord existing = ReadRecord(recordPath);
                if (!string.Equals(existing.invocationHash, invocationHash, StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Rejected(
                        "InvocationId 已绑定不同 Feishu 输入，拒绝覆盖或重复执行。");
                return GetRun(runId);
            }
            if (Directory.Exists(directory))
                return ESAutomationTaskInvocationResult.Rejected(
                    "InvocationId 目录已存在但缺少有效 RunRecord，拒绝猜测恢复。");

            Directory.CreateDirectory(directory);
            var request = new JObject
            {
                ["protocolVersion"] = 1,
                ["taskId"] = TaskId,
                ["taskVersion"] = TaskVersion,
                ["runId"] = runId,
                ["workerType"] = WorkerType,
                ["workerId"] = WorkerId,
                ["workerVersion"] = WorkerVersion,
                ["entrypointHash"] = WorkerEntrypointHash,
                ["dryRun"] = invocation.dryRun,
                ["operation"] = normalized.Value<string>("operation"),
                ["query"] = normalized.Value<string>("query") ?? string.Empty,
                ["spaceId"] = normalized.Value<string>("spaceId") ?? string.Empty,
                ["documentId"] = normalized.Value<string>("documentId") ?? string.Empty,
                ["pageSize"] = normalized.Value<int?>("pageSize") ?? 20,
            };
            WriteJsonAtomic(requestPath, request);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            var record = new ESAutomationRunRecord
            {
                runId = runId,
                taskId = TaskId,
                taskVersion = TaskVersion,
                operatorId = invocation.actorId ?? string.Empty,
                gitCommit = ReadGitCommit(),
                workerType = WorkerType,
                workerId = WorkerId,
                workerVersion = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                inputManifestHash = ComputeFileHash(requestPath),
                invocationHash = invocationHash,
                status = ESAutomationRunStatus.Starting,
                startedAtUtc = now.ToString("O"),
                lastUpdatedAtUtc = now.ToString("O"),
                operationDirectory = directory,
            };
            WriteJsonAtomic(recordPath, record);

            try
            {
                ESAutomationProcessExecution execution = ESAutomationProcessRunner.Start(
                    new ESAutomationProcessRequest
                    {
                        taskId = TaskId,
                        taskVersion = TaskVersion,
                        runId = runId,
                        dryRun = invocation.dryRun,
                        inputContractPath = requestPath,
                    });
                record.processId = execution.ProcessId;
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Running);
                WriteJsonAtomic(recordPath, record);
                ActiveRuns.Add(runId, new ActiveRun
                {
                    execution = execution,
                    record = record,
                    recordPath = recordPath,
                    resultPath = resultPath,
                    directory = directory,
                });
                return ESAutomationTaskInvocationResult.Accepted(
                    invocation.dryRun
                        ? "Feishu 只读请求已进入 DryRun；不会访问网络。"
                        : "Feishu 只读请求已由受管 Node Worker 接受。",
                    runId);
            }
            catch (Exception exception)
            {
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                record.errors.Add(exception.GetBaseException().Message);
                WriteJsonAtomic(recordPath, record);
                return ESAutomationTaskInvocationResult.Failed(
                    "Feishu Worker 启动失败：" + exception.GetBaseException().Message, runId);
            }
        }

        private static bool TryNormalizeInput(ESAutomationTaskInvocation invocation,
            out JObject normalized, out string error)
        {
            normalized = null;
            error = string.Empty;
            if (invocation == null)
            {
                error = "缺少 Feishu 调用请求。";
                return false;
            }
            string operation = invocation.input?.Value<string>("operation")?.Trim() ?? string.Empty;
            if (operation != "auth-status" && operation != "knowledge-search" && operation != "document-pull")
            {
                error = "Feishu 第一阶段只允许 auth-status、knowledge-search、document-pull。";
                return false;
            }
            string query = invocation.input?.Value<string>("query")?.Trim() ?? string.Empty;
            string spaceId = invocation.input?.Value<string>("spaceId")?.Trim() ?? string.Empty;
            string documentId = invocation.input?.Value<string>("documentId")?.Trim() ?? string.Empty;
            int pageSize = invocation.input?.Value<int?>("pageSize") ?? 20;
            if (pageSize < 1 || pageSize > 50)
            {
                error = "pageSize 必须位于 1 到 50。";
                return false;
            }
            if (operation == "knowledge-search" && string.IsNullOrWhiteSpace(query))
            {
                error = "knowledge-search 必须提供 query。";
                return false;
            }
            if (operation == "document-pull" && string.IsNullOrWhiteSpace(documentId))
            {
                error = "document-pull 必须提供 documentId。";
                return false;
            }
            normalized = new JObject
            {
                ["operation"] = operation,
                ["query"] = query,
                ["spaceId"] = spaceId,
                ["documentId"] = documentId,
                ["pageSize"] = pageSize,
            };
            return true;
        }

        private static void PollActiveRuns()
        {
            if (ActiveRuns.Count == 0) return;
            foreach (string runId in ActiveRuns.Keys.ToList())
            {
                ActiveRun active = ActiveRuns[runId];
                try
                {
                    if (active.execution.EnforceTimeout(DateTimeOffset.UtcNow))
                    {
                        FinishWithoutWorkerResult(active, ESAutomationRunStatus.TimedOut,
                            "Feishu Worker 超时并已确认终止进程树。");
                        ActiveRuns.Remove(runId);
                        continue;
                    }
                    if (!active.execution.HasExited) continue;
                    active.execution.WaitForExit(1000);
                    FinalizeWorkerResult(active);
                    ActiveRuns.Remove(runId);
                }
                catch (Exception exception)
                {
                    FinishWithoutWorkerResult(active, ESAutomationRunStatus.Failed,
                        exception.GetBaseException().Message);
                    ActiveRuns.Remove(runId);
                }
                finally
                {
                    if (!ActiveRuns.ContainsKey(runId)) active.execution.Dispose();
                }
            }
        }

        private static void FinalizeWorkerResult(ActiveRun active)
        {
            if (!File.Exists(active.resultPath))
                throw new InvalidDataException("Feishu Worker 已退出但没有 result.json。");
            ESAutomationRunResult result = JsonConvert.DeserializeObject<ESAutomationRunResult>(
                File.ReadAllText(active.resultPath, new UTF8Encoding(false, true)));
            if (result == null) throw new InvalidDataException("Feishu result.json 为空。");
            result.Validate();
            if (result.taskId != TaskId || result.taskVersion != TaskVersion
                || result.runId != active.record.runId || result.workerType != WorkerType
                || result.workerId != WorkerId || result.workerVersion != WorkerVersion
                || !string.Equals(result.entrypointHash, WorkerEntrypointHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(result.inputManifestHash, active.record.inputManifestHash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Feishu Worker 结果身份或输入 Hash 不匹配。");

            for (int i = 0; i < result.outputs.Count; i++)
            {
                string output = Path.GetFullPath(result.outputs[i]);
                if (!ESAutomationPathPolicy.IsWithin(output, new[] { active.directory }))
                    throw new UnauthorizedAccessException("Feishu Worker 输出越过 Run 目录。");
                if (!File.Exists(output)
                    || !string.Equals(ComputeFileHash(output), result.outputHashes[i],
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Feishu Worker 输出缺失或 Hash 不匹配。");
            }

            long outputBytes = result.outputs.Sum(path => new FileInfo(
                Path.IsPathRooted(path) ? path : Path.Combine(active.directory, path)).Length);
            if (ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract contract)
                && contract.performanceBudget != null
                && outputBytes > contract.performanceBudget.maxOutputBytes)
                throw new InvalidDataException("Feishu outputs exceed PerformanceBudget maxOutputBytes.");
            active.record.idempotencyKey = ESAutomationGovernance.ComputeIdempotencyKey(
                TaskId, TaskVersion, active.record.inputManifestHash, active.record.invocationHash);
            active.record.completionDecision = new ESAutomationCompletionDecision
            {
                runId = active.record.runId,
                executionStatus = result.status,
                freshnessPolicy = new ESAutomationFreshnessPolicy { maxAgeHours = 168, requireSourceHash = true, allowRuntimeNotRun = true },
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "feishu.outputs-hashed",
                        verifierId = "es.feishu.output-hash",
                        passed = true,
                        evidenceState = ESAutomationEvidenceState.Fresh,
                        evidenceHash = Sha256(string.Join("|", result.outputHashes ?? new List<string>())),
                        evidenceBinding = new ESAutomationClaimEvidenceBinding
                        {
                            claimId = "feishu.outputs-hashed",
                            criterionId = "feishu.outputs-hashed",
                            evidenceHash = Sha256(string.Join("|", result.outputHashes ?? new List<string>())),
                            sourceHash = WorkerEntrypointHash,
                            capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        },
                        message = "All declared outputs were independently hashed by the C# boundary.",
                    },
                },
            };
            active.record.completionDecision.accepted = active.record.completionDecision.CanAccept();
            active.record.exitCode = result.exitCode;
            active.record.outputs = result.outputs;
            active.record.outputHashes = result.outputHashes;
            active.record.findings = result.findings;
            active.record.errors = result.errors;
            string finalStatus = result.status == "Passed" || result.status == "DryRun"
                ? ESAutomationRunStatus.Completed
                : result.status == "Blocked" ? ESAutomationRunStatus.Blocked
                : result.status == "Cancelled" ? ESAutomationRunStatus.Cancelled
                : ESAutomationRunStatus.Failed;
            ESAutomationRunStatus.Transition(active.record, finalStatus);
            WriteJsonAtomic(active.recordPath, active.record);
        }

        private static void FinishWithoutWorkerResult(ActiveRun active, string status, string error)
        {
            if (!ESAutomationRunStatus.IsTerminal(active.record.status))
                ESAutomationRunStatus.Transition(active.record, status);
            active.record.exitCode = -1;
            active.record.errors.Add(error ?? string.Empty);
            WriteJsonAtomic(active.recordPath, active.record);
        }

        private static ESAutomationTaskInvocationResult GetRun(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            string recordPath = Path.Combine(GetRunDirectory(runId), "run-record.json");
            if (!File.Exists(recordPath)) return ESAutomationTaskInvocationResult.NotFound("未找到 Feishu RunRecord。");
            ESAutomationRunRecord record = ReadRecord(recordPath);
            var data = new JObject
            {
                ["status"] = record.status,
                ["exitCode"] = record.exitCode,
                ["outputs"] = JArray.FromObject(record.outputs ?? new List<string>()),
                ["findings"] = JArray.FromObject(record.findings ?? new List<string>()),
                ["errors"] = JArray.FromObject(record.errors ?? new List<string>()),
            };
            return ESAutomationRunStatus.IsTerminal(record.status)
                ? ESAutomationTaskInvocationResult.Completed("Feishu Run 已结束：" + record.status, runId, data)
                : ESAutomationTaskInvocationResult.Starting("Feishu Run 正在执行：" + record.status, runId, data);
        }

        private static ESAutomationTaskInvocationResult Cancel(string runId, string actorId)
        {
            if (!ActiveRuns.TryGetValue(runId, out ActiveRun active))
                return ESAutomationTaskInvocationResult.NotFound("未找到正在运行的 Feishu Run。");
            try
            {
                active.execution.Terminate();
                FinishWithoutWorkerResult(active, ESAutomationRunStatus.Cancelled,
                    "由 " + (actorId ?? string.Empty) + " 取消；进程树终止已确认。");
                active.execution.Dispose();
                ActiveRuns.Remove(runId);
                return ESAutomationTaskInvocationResult.Completed("Feishu Run 已取消。", runId);
            }
            catch (Exception exception)
            {
                return ESAutomationTaskInvocationResult.Failed(
                    "Feishu Run 取消失败，不能确认进程树已终止：" + exception.GetBaseException().Message, runId);
            }
        }

        private static bool HasCredentials()
            => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ES_FEISHU_APP_ID"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ES_FEISHU_APP_SECRET"));

        private static bool TryResolveNode(out string nodePath, out string error)
        {
            nodePath = Environment.GetEnvironmentVariable("ES_AUTOMATION_NODE_PATH") ?? string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                error = "未配置 ES_AUTOMATION_NODE_PATH；受管 Worker 禁止静默回退 PATH。";
                return false;
            }
            nodePath = Path.GetFullPath(nodePath);
            if (!File.Exists(nodePath) || ESManagedFileIO.ContainsExistingReparsePoint(nodePath))
            {
                error = "ES_AUTOMATION_NODE_PATH 不存在或穿过 reparse point。";
                return false;
            }
            return true;
        }

        private static string GetRunDirectory(string runId)
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Temp", "Feishu", runId);

        private static string GetWorkerDirectory()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers", "Node", "Feishu");

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void WriteJsonAtomic(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(value, Formatting.Indented) + "\n",
                new UTF8Encoding(false, true));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static ESAutomationRunRecord ReadRecord(string path)
        {
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                File.ReadAllText(path, new UTF8Encoding(false, true)));
            if (record == null || record.taskId != TaskId || record.taskVersion != TaskVersion)
                throw new InvalidDataException("Feishu RunRecord 身份无效。");
            return record;
        }

        private static string ReadGitCommit()
        {
            string git = Path.Combine(ESAutomationPathPolicy.ProjectRoot, ".git");
            string headPath = Path.Combine(git, "HEAD");
            if (!File.Exists(headPath)) return string.Empty;
            string head = File.ReadAllText(headPath, new UTF8Encoding(false, true)).Trim();
            if (head.Length == 40 && head.All(Uri.IsHexDigit)) return head.ToLowerInvariant();
            const string prefix = "ref:";
            if (!head.StartsWith(prefix, StringComparison.Ordinal)) return string.Empty;
            string reference = head.Substring(prefix.Length).Trim().Replace('/', Path.DirectorySeparatorChar);
            string referencePath = Path.Combine(git, reference);
            return File.Exists(referencePath)
                ? File.ReadAllText(referencePath, new UTF8Encoding(false, true)).Trim().ToLowerInvariant()
                : string.Empty;
        }

        private sealed class FeishuNodeAdapter : IESAutomationWorkerAdapter
        {
            public string WorkerType => ESFeishuReadAutomation.WorkerType;
            public string WorkerId => ESFeishuReadAutomation.WorkerId;

            public ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract,
                ESAutomationProcessRequest request)
            {
                if (!TryResolveNode(out string nodePath, out string error))
                    throw new InvalidOperationException(error);
                string directory = GetWorkerDirectory();
                string workerPath = Path.Combine(directory, "worker.js");
                string lockPath = Path.Combine(directory, "package-lock.json");
                if (!File.Exists(workerPath) || !File.Exists(lockPath)
                    || ESManagedFileIO.ContainsExistingReparsePoint(workerPath)
                    || ESManagedFileIO.ContainsExistingReparsePoint(lockPath))
                    throw new InvalidOperationException("Feishu Worker 或 package-lock 不存在或路径不安全。");
                if (!string.Equals(ComputeFileHash(workerPath), WorkerEntrypointHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ComputeFileHash(lockPath), PackageLockHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Feishu Worker 或依赖锁文件 Hash 漂移，必须重新审查注册。");

                string outputDirectory = GetRunDirectory(request.runId);
                return new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = Quote(workerPath) + " " + Quote(request.inputContractPath) + " "
                        + Quote(outputDirectory),
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false, true),
                    StandardErrorEncoding = new UTF8Encoding(false, true),
                };
            }

            private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private sealed class FeishuEndpoint : IESAutomationTaskEndpoint,
            IESAutomationContractBoundEndpoint, IESAutomationCancellableTaskEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                category = "External/Feishu",
                displayName = "Feishu 只读知识接入",
                summary = "受管执行 auth-status、knowledge-search、document-pull；不发送、不发布。",
                allowAiInvoke = true,
                allowInPlayMode = false,
            };

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
                => ESFeishuReadAutomation.Run(invocation);

            public ESAutomationTaskInvocationResult GetRun(string runId)
                => ESFeishuReadAutomation.GetRun(runId);

            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.Rejected("Feishu 第一阶段任务不接受分阶段输入。");

            public ESAutomationTaskInvocationResult CancelRun(string runId, string actorId)
                => ESFeishuReadAutomation.Cancel(runId, actorId);

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
            {
                string runId = !string.IsNullOrWhiteSpace(invocation?.invocationId)
                    ? invocation.invocationId : Guid.NewGuid().ToString("N");
                string directory = GetRunDirectory(runId);
                return new ESAutomationInvocationRequirements
                {
                    worker = CreateWorkerRegistration(),
                    requiredCapabilities = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteTemp,
                    dryRun = invocation?.dryRun ?? true,
                    readPaths = new List<string> { directory },
                    writePaths = new List<string> { directory },
                };
            }
        }

        private sealed class ActiveRun
        {
            public ESAutomationProcessExecution execution;
            public ESAutomationRunRecord record;
            public string recordPath;
            public string resultPath;
            public string directory;
        }
    }

    internal sealed class ESFeishuReadAutomationInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke() => ESFeishuReadAutomation.Register();
    }
}
