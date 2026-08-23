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
    /// 飞书 Task v2 受管接入。读取、派发和状态推进使用独立 TaskContract，
    /// 外部写仅允许在 AIBrain 一次性计划、DryRun 和固定输入合同下执行。
    /// </summary>
    internal static class ESFeishuTaskAutomation
    {
        internal const string MonitorTaskId = "es.feishu.task.monitor";
        internal const string DispatchTaskId = "es.feishu.task.dispatch";
        internal const string TransitionTaskId = "es.feishu.task.transition";
        internal const int TaskVersion = 1;
        internal const string WorkerType = "Other";
        internal const string WorkerId = "es.feishu.task.node";
        internal const string WorkerVersion = "0.1.0";
        internal const string WorkerEntrypointHash = "6eeeb41b0feecd36f38bc1d24453aafcc00a0aa5325dba25af741e12a17fc88b";
        internal const string PackageLockHash = "f12bad503b40ce56b7dedf47bb7e98846d10dbcf4f00bcd95ed6881f98ed9f40";
        private const int TimeoutSeconds = 60;
        private const int MaximumRecoveredRuns = 256;

        private static readonly Dictionary<string, TaskSpec> Specs = CreateSpecs();
        private static readonly Dictionary<string, ActiveRun> ActiveRuns =
            new Dictionary<string, ActiveRun>(StringComparer.Ordinal);

        internal static void Register()
        {
            foreach (TaskSpec spec in Specs.Values.OrderBy(item => item.TaskId, StringComparer.Ordinal))
            {
                RegisterContract(spec);
                if (!ESAutomationFacade.TryGetDescriptor(spec.TaskId, TaskVersion, out _))
                    ESAutomationFacade.Register(new FeishuTaskEndpoint(spec));
            }
            if (!ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId))
                ESAutomationProcessRunner.RegisterAdapter(new FeishuTaskNodeAdapter());

            ReconcileInterruptedRuns();
            EditorApplication.update -= PollActiveRuns;
            EditorApplication.update += PollActiveRuns;
        }

        private static Dictionary<string, TaskSpec> CreateSpecs()
        {
            var monitor = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteTemp
                | ESAutomationCapability.ExternalRead;
            var mutate = monitor | ESAutomationCapability.ExternalWrite;
            return new Dictionary<string, TaskSpec>(StringComparer.Ordinal)
            {
                [MonitorTaskId] = new TaskSpec(
                    MonitorTaskId, "飞书任务监控", "读取任务清单、任务和分页进度。",
                    "ES/Automation/Contracts/feishu-task-monitor-v1.schema.json",
                    "e9752b11191b19f155e36629e5fefcfb3ad8900a854f94057d3d980d57c28345",
                    monitor,
                    new[] { "tasklist-list", "tasklist-get", "task-list", "task-get" }),
                [DispatchTaskId] = new TaskSpec(
                    DispatchTaskId, "飞书任务派发", "DryRun 后创建清单、任务或虚拟团队测试夹具。",
                    "ES/Automation/Contracts/feishu-task-dispatch-v1.schema.json",
                    "90aafeb355a263fbcd10e91897d1f26a702767e9b0a97a5278d8114c7fc7dc8b",
                    mutate,
                    new[] { "tasklist-create", "task-create", "virtual-team-fixture-create" }),
                [TransitionTaskId] = new TaskSpec(
                    TransitionTaskId, "飞书任务推进", "按远端版本前置条件更新进度、完成状态、成员和提醒。",
                    "ES/Automation/Contracts/feishu-task-transition-v1.schema.json",
                    "95008f8bc2bbe84c34b4340c990fb19d0e010432bdd1625f1911e285cc2475d9",
                    mutate,
                    new[] { "task-update", "task-complete", "task-reopen", "members-add", "members-remove", "reminder-add", "reminder-remove" }),
            };
        }

        private static void RegisterContract(TaskSpec spec)
        {
            if (!ESAutomationTaskRegistry.TryGet(spec.TaskId, TaskVersion,
                    out ESAutomationTaskContract contract))
            {
                contract = new ESAutomationTaskContract
                {
                    taskId = spec.TaskId,
                    version = TaskVersion,
                    worker = CreateWorkerRegistration(),
                    inputs = new List<string> { "request.json" },
                    readRoots = new List<string> { "ES/Automation/Temp", "ES/Automation/Contracts" },
                    writeRoots = new List<string> { "ES/Automation/Temp" },
                    capabilities = CapabilityNames(spec.Capabilities),
                    inputSchemaHash = spec.SchemaHash,
                    timeoutSeconds = TimeoutSeconds,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "feishu-task-data.json", "result.json", "run-record.json" },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = TimeoutSeconds,
                        maxOutputBytes = 4L * 1024L * 1024L,
                        maxRetryCount = 0,
                        maxFindingCount = 200,
                    },
                    acceptanceCriteria = CreateAcceptanceCriteria(),
                    capabilityEnvelope = CreateCapabilityEnvelope(spec.Capabilities),
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            else
            {
                ValidateExistingContract(contract, spec);
            }
            contract.Validate();
        }

        private static ESAutomationAcceptanceCriteria CreateAcceptanceCriteria()
            => new ESAutomationAcceptanceCriteria
            {
                freshnessPolicy = new ESAutomationFreshnessPolicy
                {
                    maxAgeHours = 24,
                    requireSourceHash = true,
                    allowRuntimeNotRun = false,
                },
                criteria = new List<ESAutomationAcceptanceCriterion>
                {
                    new ESAutomationAcceptanceCriterion
                    {
                        criterionId = "feishu-task.output-integrity",
                        verifierId = "es.feishu.task.semantic-output",
                        description = "Worker identity, output hashes and DryRun/network/mutation semantics agree.",
                    },
                },
            };

        private static ESAutomationCapabilityEnvelope CreateCapabilityEnvelope(
            ESAutomationCapability capabilities)
            => new ESAutomationCapabilityEnvelope
            {
                userAuthorization = capabilities,
                taskContract = capabilities,
                aiCommand = capabilities,
                workerCapability = capabilities,
                projectBoundary = capabilities,
            };

        private static List<string> CapabilityNames(ESAutomationCapability capabilities)
        {
            var result = new List<string>();
            foreach (var pair in new[]
                     {
                         new KeyValuePair<ESAutomationCapability, string>(ESAutomationCapability.ReadArtifacts, "ReadArtifacts"),
                         new KeyValuePair<ESAutomationCapability, string>(ESAutomationCapability.WriteTemp, "WriteTemp"),
                         new KeyValuePair<ESAutomationCapability, string>(ESAutomationCapability.ExternalRead, "ExternalRead"),
                         new KeyValuePair<ESAutomationCapability, string>(ESAutomationCapability.ExternalWrite, "ExternalWrite"),
                     })
                if ((capabilities & pair.Key) != 0) result.Add(pair.Value);
            return result;
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

        private static void ValidateExistingContract(ESAutomationTaskContract contract, TaskSpec spec)
        {
            if (contract.worker == null
                || contract.worker.type != WorkerType
                || contract.worker.workerId != WorkerId
                || contract.worker.version != WorkerVersion
                || !string.Equals(contract.worker.entrypointHash, WorkerEntrypointHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(contract.inputSchemaHash, spec.SchemaHash,
                    StringComparison.OrdinalIgnoreCase)
                || contract.ResolveCapabilities() != spec.Capabilities
                || !contract.worker.enabled)
                throw new InvalidOperationException("已有飞书任务合同与受信 Worker、Schema 或能力不一致："
                    + spec.TaskId);
        }

        private static ESAutomationTaskInvocationResult Run(TaskSpec spec,
            ESAutomationTaskInvocation invocation)
        {
            if (!TryNormalizeInput(spec, invocation, out JObject normalized, out string error))
                return ESAutomationTaskInvocationResult.Rejected(error);
            if (!TryResolveNode(out _, out string nodeError))
                return ESAutomationTaskInvocationResult.Blocked(nodeError);
            if (!invocation.dryRun && !HasCredentials())
                return ESAutomationTaskInvocationResult.Blocked(
                    "缺少受管 ES_FEISHU_APP_ID/ES_FEISHU_APP_SECRET；不得通过请求或日志补交凭据。");

            string runId = string.IsNullOrWhiteSpace(invocation.invocationId)
                ? Guid.NewGuid().ToString("N") : invocation.invocationId;
            string directory = GetRunDirectory(runId);
            string requestPath = Path.Combine(directory, "request.json");
            string recordPath = Path.Combine(directory, "run-record.json");
            string resultPath = Path.Combine(directory, "result.json");
            string invocationHash = Sha256(JsonConvert.SerializeObject(new
            {
                spec.TaskId,
                version = TaskVersion,
                input = normalized,
                invocation.dryRun,
                invocation.brainPlanHash,
                invocation.idempotencyKey,
            }, Formatting.None));

            if (File.Exists(recordPath))
            {
                ESAutomationRunRecord existing = ReadRecord(recordPath, spec.TaskId);
                if (!string.Equals(existing.invocationHash, invocationHash,
                    StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Rejected(
                        "InvocationId 已绑定不同飞书任务输入，拒绝覆盖或重复副作用。");
                return GetRun(spec, runId);
            }
            if (Directory.Exists(directory))
                return ESAutomationTaskInvocationResult.Rejected(
                    "InvocationId 目录存在但无有效 RunRecord，拒绝猜测恢复。");

            Directory.CreateDirectory(directory);
            var request = new JObject
            {
                ["protocolVersion"] = 1,
                ["taskId"] = spec.TaskId,
                ["taskVersion"] = TaskVersion,
                ["runId"] = runId,
                ["workerType"] = WorkerType,
                ["workerId"] = WorkerId,
                ["workerVersion"] = WorkerVersion,
                ["entrypointHash"] = WorkerEntrypointHash,
                ["inputSchemaHash"] = spec.SchemaHash,
                ["dryRun"] = invocation.dryRun,
                ["operation"] = normalized.Value<string>("operation"),
                ["input"] = normalized,
            };
            WriteJsonAtomic(requestPath, request);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            var record = new ESAutomationRunRecord
            {
                runId = runId,
                taskId = spec.TaskId,
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
                        taskId = spec.TaskId,
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
                    Spec = spec,
                    Execution = execution,
                    Record = record,
                    RecordPath = recordPath,
                    ResultPath = resultPath,
                    Directory = directory,
                    DryRun = invocation.dryRun,
                    Operation = normalized.Value<string>("operation") ?? string.Empty,
                });
                return ESAutomationTaskInvocationResult.Accepted(
                    invocation.dryRun
                        ? "飞书任务计划已进入 DryRun；不会访问网络或修改远端。"
                        : "飞书任务请求已由受管 Worker 接受。",
                    runId);
            }
            catch (Exception exception)
            {
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                record.errors.Add(SanitizeError(exception.GetBaseException().Message));
                WriteJsonAtomic(recordPath, record);
                return ESAutomationTaskInvocationResult.Failed("飞书任务 Worker 启动失败。", runId);
            }
        }

        private static bool TryNormalizeInput(TaskSpec spec, ESAutomationTaskInvocation invocation,
            out JObject normalized, out string error)
        {
            normalized = null;
            error = string.Empty;
            if (invocation?.input == null)
            {
                error = "缺少飞书任务输入。";
                return false;
            }
            string operation = invocation.input.Value<string>("operation")?.Trim() ?? string.Empty;
            if (!spec.Operations.Contains(operation))
            {
                error = spec.TaskId + " 不允许操作：" + operation;
                return false;
            }
            var candidate = (JObject)invocation.input.DeepClone();
            candidate["operation"] = operation;
            if (!ValidateCommonInput(candidate, out error)) return false;
            if (spec.TaskId == MonitorTaskId && !ValidateMonitor(candidate, operation, out error)) return false;
            if (spec.TaskId == DispatchTaskId && !ValidateDispatch(candidate, operation, out error)) return false;
            if (spec.TaskId == TransitionTaskId && !ValidateTransition(candidate, operation, invocation.dryRun, out error)) return false;
            normalized = Canonicalize(candidate);
            return true;
        }

        private static bool ValidateCommonInput(JObject input, out string error)
        {
            error = string.Empty;
            foreach (JProperty property in input.Properties())
            {
                if (property.Name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0
                    || property.Name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0
                        && property.Name != "pageToken")
                {
                    error = "输入不得包含凭据或自定义 Token 字段：" + property.Name;
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateMonitor(JObject input, string operation, out string error)
        {
            error = string.Empty;
            if (!ValidateAllowedFields(input,
                    new[] { "operation", "tasklistGuid", "taskGuid", "pageSize", "pageToken", "completed" }, out error))
                return false;
            int pageSize = input.Value<int?>("pageSize") ?? 20;
            if (pageSize < 1 || pageSize > 50) { error = "pageSize 必须位于 1 到 50。"; return false; }
            input["pageSize"] = pageSize;
            if ((operation == "tasklist-get" || operation == "task-list")
                && !RequireBoundedString(input, "tasklistGuid", 128, out error)) return false;
            if (operation == "task-get" && !RequireBoundedString(input, "taskGuid", 128, out error)) return false;
            return OptionalBoundedString(input, "pageToken", 512, out error);
        }

        private static bool ValidateDispatch(JObject input, string operation, out string error)
        {
            error = string.Empty;
            if (!ValidateAllowedFields(input, new[]
                {
                    "operation", "tasklistName", "tasklistGuid", "summary", "description",
                    "startTimestamp", "dueTimestamp", "isAllDay", "members", "tasks", "fixturePrefix",
                }, out error)) return false;
            if (operation == "tasklist-create"
                && !RequireBoundedString(input, "tasklistName", 80, out error)) return false;
            if (operation == "task-create")
            {
                if (!RequireBoundedString(input, "summary", 200, out error)) return false;
                if (!RequireBoundedString(input, "tasklistGuid", 128, out error)) return false;
            }
            if (operation == "virtual-team-fixture-create"
                && !OptionalBoundedString(input, "fixturePrefix", 40, out error)) return false;
            if (!OptionalBoundedString(input, "description", 4000, out error)
                || !ValidateTimestamp(input, "startTimestamp", out error)
                || !ValidateTimestamp(input, "dueTimestamp", out error)
                || !ValidateMembers(input["members"], out error)
                || !ValidateTaskBatch(input["tasks"], out error)) return false;
            return true;
        }

        private static bool ValidateTransition(JObject input, string operation, bool dryRun,
            out string error)
        {
            error = string.Empty;
            if (!ValidateAllowedFields(input, new[]
                {
                    "operation", "taskGuid", "expectedUpdatedAt", "summary", "description",
                    "startTimestamp", "dueTimestamp", "isAllDay", "agentTaskStatus",
                    "agentTaskProgress", "members", "relativeFireMinute", "reminderId",
                }, out error)) return false;
            if (!RequireBoundedString(input, "taskGuid", 128, out error)) return false;
            if (!dryRun && !RequireBoundedString(input, "expectedUpdatedAt", 64, out error)) return false;
            if (!OptionalBoundedString(input, "expectedUpdatedAt", 64, out error)
                || !OptionalBoundedString(input, "summary", 200, out error)
                || !OptionalBoundedString(input, "description", 4000, out error)
                || !OptionalBoundedString(input, "agentTaskProgress", 2000, out error)
                || !ValidateTimestamp(input, "startTimestamp", out error)
                || !ValidateTimestamp(input, "dueTimestamp", out error)) return false;
            if ((operation == "members-add" || operation == "members-remove")
                && !ValidateMembers(input["members"], out error, true)) return false;
            if (operation == "reminder-add")
            {
                int? minute = input.Value<int?>("relativeFireMinute");
                if (!minute.HasValue || minute.Value < -525600 || minute.Value > 0)
                { error = "relativeFireMinute 必须位于 -525600 到 0。"; return false; }
            }
            if (operation == "reminder-remove"
                && !RequireBoundedString(input, "reminderId", 128, out error)) return false;
            int? status = input.Value<int?>("agentTaskStatus");
            if (status.HasValue && (status.Value < 0 || status.Value > 100))
            { error = "agentTaskStatus 必须位于 0 到 100。"; return false; }
            return true;
        }

        private static bool ValidateAllowedFields(JObject input, IEnumerable<string> allowed,
            out string error)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (JProperty property in input.Properties())
                if (!set.Contains(property.Name))
                { error = "未注册的输入字段：" + property.Name; return false; }
            error = string.Empty;
            return true;
        }

        private static bool RequireBoundedString(JObject input, string name, int maximum,
            out string error)
        {
            string value = input.Value<string>(name)?.Trim() ?? string.Empty;
            if (value.Length < 1 || value.Length > maximum)
            { error = name + " 必须是 1 到 " + maximum + " 个字符。"; return false; }
            input[name] = value;
            error = string.Empty;
            return true;
        }

        private static bool OptionalBoundedString(JObject input, string name, int maximum,
            out string error)
        {
            JToken token = input[name];
            if (token == null || token.Type == JTokenType.Null) { error = string.Empty; return true; }
            if (token.Type != JTokenType.String || token.Value<string>().Length > maximum)
            { error = name + " 最多 " + maximum + " 个字符。"; return false; }
            error = string.Empty;
            return true;
        }

        private static bool ValidateTimestamp(JObject input, string name, out string error)
        {
            string value = input.Value<string>(name);
            if (string.IsNullOrEmpty(value)) { error = string.Empty; return true; }
            if (value.Length > 16 || value.Any(character => character < '0' || character > '9'))
            { error = name + " 必须是最多 16 位的 Unix 毫秒字符串。"; return false; }
            error = string.Empty;
            return true;
        }

        private static bool ValidateMembers(JToken token, out string error, bool required = false)
        {
            if (token == null || token.Type == JTokenType.Null)
            { error = required ? "members 为必填数组。" : string.Empty; return !required; }
            if (!(token is JArray array) || array.Count > 20 || required && array.Count == 0)
            { error = "members 必须是 1 到 20 项的数组。"; return false; }
            foreach (JToken item in array)
            {
                if (!(item is JObject member)
                    || !RequireBoundedString(member, "id", 128, out error)
                    || !OptionalBoundedString(member, "type", 16, out error)
                    || !OptionalBoundedString(member, "role", 16, out error)) return false;
                if (member.Properties().Any(property => property.Name != "id"
                        && property.Name != "type" && property.Name != "role"))
                { error = "member 包含未注册字段。"; return false; }
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateTaskBatch(JToken token, out string error)
        {
            if (token == null || token.Type == JTokenType.Null) { error = string.Empty; return true; }
            if (!(token is JArray array) || array.Count < 1 || array.Count > 20)
            { error = "tasks 必须是 1 到 20 项的数组。"; return false; }
            foreach (JToken item in array)
            {
                if (!(item is JObject task)
                    || !RequireBoundedString(task, "summary", 200, out error)
                    || !OptionalBoundedString(task, "description", 4000, out error)) return false;
                if (task.Properties().Any(property => property.Name != "summary"
                        && property.Name != "description"))
                { error = "fixture task 包含未注册字段。"; return false; }
            }
            error = string.Empty;
            return true;
        }

        private static JObject Canonicalize(JObject input)
        {
            var result = new JObject();
            foreach (JProperty property in input.Properties().OrderBy(item => item.Name,
                         StringComparer.Ordinal))
                result[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static void PollActiveRuns()
        {
            if (ActiveRuns.Count == 0) return;
            foreach (string runId in ActiveRuns.Keys.ToList())
            {
                ActiveRun active = ActiveRuns[runId];
                try
                {
                    if (active.Execution.EnforceTimeout(DateTimeOffset.UtcNow))
                    {
                        FinishWithoutWorkerResult(active, ESAutomationRunStatus.TimedOut,
                            "飞书任务 Worker 超时并已确认终止进程树。");
                        ActiveRuns.Remove(runId);
                        continue;
                    }
                    if (!active.Execution.HasExited) continue;
                    active.Execution.WaitForExit(1000);
                    FinalizeWorkerResult(active);
                    ActiveRuns.Remove(runId);
                }
                catch (Exception exception)
                {
                    FinishWithoutWorkerResult(active, ESAutomationRunStatus.Failed,
                        SanitizeError(exception.GetBaseException().Message));
                    ActiveRuns.Remove(runId);
                }
                finally
                {
                    if (!ActiveRuns.ContainsKey(runId)) active.Execution.Dispose();
                }
            }
        }

        private static void FinalizeWorkerResult(ActiveRun active)
        {
            if (!File.Exists(active.ResultPath))
                throw new InvalidDataException("飞书任务 Worker 已退出但没有 result.json。");
            ESAutomationRunResult result = JsonConvert.DeserializeObject<ESAutomationRunResult>(
                File.ReadAllText(active.ResultPath, new UTF8Encoding(false, true)));
            if (result == null) throw new InvalidDataException("飞书任务 result.json 为空。");
            result.Validate();
            if (result.taskId != active.Spec.TaskId || result.taskVersion != TaskVersion
                || result.runId != active.Record.runId || result.workerType != WorkerType
                || result.workerId != WorkerId || result.workerVersion != WorkerVersion
                || !string.Equals(result.entrypointHash, WorkerEntrypointHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(result.inputManifestHash, active.Record.inputManifestHash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("飞书任务 Worker 结果身份或输入 Hash 不匹配。");

            long outputBytes = 0;
            for (int i = 0; i < result.outputs.Count; i++)
            {
                string output = Path.GetFullPath(result.outputs[i]);
                if (!ESAutomationPathPolicy.IsWithin(output, new[] { active.Directory }))
                    throw new UnauthorizedAccessException("飞书任务输出越过 Run 目录。");
                if (!File.Exists(output)
                    || !string.Equals(ComputeFileHash(output), result.outputHashes[i],
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("飞书任务输出缺失或 Hash 不匹配。");
                outputBytes += new FileInfo(output).Length;
            }
            if (outputBytes > 4L * 1024L * 1024L)
                throw new InvalidDataException("飞书任务输出超过 4 MiB 合同上限。");

            bool successful = result.status == "Passed" || result.status == "DryRun";
            string evidenceHash = string.Empty;
            if (successful)
            {
                string dataPath = Path.Combine(active.Directory, "feishu-task-data.json");
                if (!File.Exists(dataPath)) throw new InvalidDataException("飞书任务缺少结构化数据输出。");
                JObject data = JObject.Parse(File.ReadAllText(dataPath,
                    new UTF8Encoding(false, true)));
                bool networkCalled = data.Value<bool?>("networkCalled") ?? false;
                bool mutationApplied = data.Value<bool?>("mutationApplied") ?? false;
                if (active.DryRun && (networkCalled || mutationApplied))
                    throw new InvalidDataException("DryRun 输出声称发生网络或远端写入。");
                if (!active.DryRun && !networkCalled)
                    throw new InvalidDataException("Live 输出缺少 networkCalled 证据。");
                bool mutatingTask = active.Spec.TaskId != MonitorTaskId;
                if (!active.DryRun && mutationApplied != mutatingTask)
                    throw new InvalidDataException("Live 输出的 mutationApplied 与 TaskContract 不一致。");
                evidenceHash = ComputeFileHash(dataPath);
            }

            active.Record.idempotencyKey = ESAutomationGovernance.ComputeIdempotencyKey(
                active.Spec.TaskId, TaskVersion, active.Record.inputManifestHash,
                active.Record.invocationHash);
            active.Record.completionDecision = new ESAutomationCompletionDecision
            {
                runId = active.Record.runId,
                executionStatus = result.status,
                freshnessPolicy = CreateAcceptanceCriteria().freshnessPolicy,
                traceReconciled = true,
                criterionResults = new List<ESAutomationCriterionResult>
                {
                    new ESAutomationCriterionResult
                    {
                        criterionId = "feishu-task.output-integrity",
                        verifierId = "es.feishu.task.semantic-output",
                        passed = successful,
                        evidenceState = successful ? ESAutomationEvidenceState.Fresh
                            : ESAutomationEvidenceState.Missing,
                        evidenceHash = evidenceHash,
                        evidenceBinding = successful
                            ? new ESAutomationClaimEvidenceBinding
                            {
                                claimId = "feishu-task.output-integrity",
                                criterionId = "feishu-task.output-integrity",
                                evidenceHash = evidenceHash,
                                sourceHash = WorkerEntrypointHash,
                                capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                            }
                            : null,
                        message = successful
                            ? "C# verified identity, hashes and network/mutation semantics."
                            : "Worker did not produce an accepted terminal result.",
                    },
                },
            };
            active.Record.completionDecision.accepted =
                successful && active.Record.completionDecision.CanAccept();
            active.Record.exitCode = result.exitCode;
            active.Record.outputs = result.outputs;
            active.Record.outputHashes = result.outputHashes;
            active.Record.findings = result.findings.Select(SanitizeError).ToList();
            active.Record.errors = result.errors.Select(SanitizeError).ToList();
            string finalStatus = result.status == "Passed" || result.status == "DryRun"
                ? ESAutomationRunStatus.Completed
                : result.status == "Blocked" ? ESAutomationRunStatus.Blocked
                : result.status == "Cancelled" ? ESAutomationRunStatus.Cancelled
                : ESAutomationRunStatus.Failed;
            ESAutomationRunStatus.Transition(active.Record, finalStatus);
            WriteJsonAtomic(active.RecordPath, active.Record);
        }

        private static void FinishWithoutWorkerResult(ActiveRun active, string status, string error)
        {
            if (!ESAutomationRunStatus.IsTerminal(active.Record.status))
                ESAutomationRunStatus.Transition(active.Record, status);
            active.Record.exitCode = -1;
            active.Record.errors.Add(SanitizeError(error));
            WriteJsonAtomic(active.RecordPath, active.Record);
        }

        private static ESAutomationTaskInvocationResult GetRun(TaskSpec spec, string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            string recordPath = Path.Combine(GetRunDirectory(runId), "run-record.json");
            if (!File.Exists(recordPath))
                return ESAutomationTaskInvocationResult.NotFound("未找到飞书任务 RunRecord。");
            ESAutomationRunRecord record = ReadRecord(recordPath, spec.TaskId);
            var data = new JObject
            {
                ["status"] = record.status,
                ["exitCode"] = record.exitCode,
                ["outputs"] = JArray.FromObject(record.outputs ?? new List<string>()),
                ["findings"] = JArray.FromObject(record.findings ?? new List<string>()),
                ["errors"] = JArray.FromObject(record.errors ?? new List<string>()),
            };
            return ESAutomationRunStatus.IsTerminal(record.status)
                ? ESAutomationTaskInvocationResult.Completed("飞书任务 Run 已结束：" + record.status,
                    runId, data)
                : ESAutomationTaskInvocationResult.Starting("飞书任务 Run 正在执行：" + record.status,
                    runId, data);
        }

        private static ESAutomationTaskInvocationResult Cancel(TaskSpec spec, string runId,
            string actorId)
        {
            if (!ActiveRuns.TryGetValue(runId, out ActiveRun active)
                || active.Spec.TaskId != spec.TaskId)
                return ESAutomationTaskInvocationResult.NotFound("未找到正在运行的飞书任务 Run。");
            try
            {
                active.Execution.Terminate();
                FinishWithoutWorkerResult(active, ESAutomationRunStatus.Cancelled,
                    "由 " + (actorId ?? string.Empty) + " 取消；进程树终止已确认。");
                active.Execution.Dispose();
                ActiveRuns.Remove(runId);
                return ESAutomationTaskInvocationResult.Completed("飞书任务 Run 已取消。", runId);
            }
            catch
            {
                return ESAutomationTaskInvocationResult.Failed(
                    "飞书任务取消失败，不能确认进程树已终止。", runId);
            }
        }

        private static void ReconcileInterruptedRuns()
        {
            string root = GetRunRoot();
            if (!Directory.Exists(root)) return;
            foreach (string directory in Directory.EnumerateDirectories(root)
                         .OrderByDescending(Directory.GetLastWriteTimeUtc)
                         .Take(MaximumRecoveredRuns))
            {
                string recordPath = Path.Combine(directory, "run-record.json");
                if (!File.Exists(recordPath)) continue;
                try
                {
                    ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                        File.ReadAllText(recordPath, new UTF8Encoding(false, true)));
                    if (record == null || !Specs.ContainsKey(record.taskId)
                        || ESAutomationRunStatus.IsTerminal(record.status)) continue;
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                    record.exitCode = -1;
                    record.errors.Add("Domain Reload/Editor restart 后未找到可恢复的受管进程；已保守终结，禁止重复远端写入。");
                    WriteJsonAtomic(recordPath, record);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogError("飞书任务中断 RunRecord 无法收口："
                        + SanitizeError(exception.GetBaseException().Message));
                }
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
            { error = "未配置 ES_AUTOMATION_NODE_PATH；受管 Worker 禁止回退 PATH。"; return false; }
            nodePath = Path.GetFullPath(nodePath);
            if (!File.Exists(nodePath) || ESManagedFileIO.ContainsExistingReparsePoint(nodePath))
            { error = "ES_AUTOMATION_NODE_PATH 不存在或穿过 reparse point。"; return false; }
            return true;
        }

        private static string GetRunRoot()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Temp",
                "FeishuTasks");

        private static string GetRunDirectory(string runId) => Path.Combine(GetRunRoot(), runId);

        private static string GetWorkerDirectory()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers",
                "Node", "Feishu");

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void WriteJsonAtomic(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException());
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary,
                JsonConvert.SerializeObject(value, Formatting.Indented) + "\n",
                new UTF8Encoding(false, true));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static ESAutomationRunRecord ReadRecord(string path, string taskId)
        {
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                File.ReadAllText(path, new UTF8Encoding(false, true)));
            if (record == null || record.taskId != taskId || record.taskVersion != TaskVersion)
                throw new InvalidDataException("飞书任务 RunRecord 身份无效。");
            return record;
        }

        private static string SanitizeError(string value)
        {
            string result = value ?? string.Empty;
            foreach (string variable in new[] { "ES_FEISHU_APP_ID", "ES_FEISHU_APP_SECRET" })
            {
                string secret = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(secret)) result = result.Replace(secret, "[REDACTED]");
            }
            return result.Length <= 2000 ? result : result.Substring(0, 2000);
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
            string reference = head.Substring(prefix.Length).Trim()
                .Replace('/', Path.DirectorySeparatorChar);
            string referencePath = Path.Combine(git, reference);
            return File.Exists(referencePath)
                ? File.ReadAllText(referencePath, new UTF8Encoding(false, true)).Trim()
                    .ToLowerInvariant()
                : string.Empty;
        }

        private sealed class FeishuTaskNodeAdapter : IESAutomationWorkerAdapter
        {
            public string WorkerType => ESFeishuTaskAutomation.WorkerType;
            public string WorkerId => ESFeishuTaskAutomation.WorkerId;

            public ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract,
                ESAutomationProcessRequest request)
            {
                if (!TryResolveNode(out string nodePath, out string error))
                    throw new InvalidOperationException(error);
                if (!Specs.TryGetValue(request.taskId, out TaskSpec spec))
                    throw new InvalidOperationException("飞书任务 Adapter 收到未知 TaskId。");
                string directory = GetWorkerDirectory();
                string workerPath = Path.Combine(directory, "task-worker.js");
                string lockPath = Path.Combine(directory, "package-lock.json");
                string schemaPath = Path.Combine(ESAutomationPathPolicy.ProjectRoot,
                    spec.SchemaPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(workerPath) || !File.Exists(lockPath) || !File.Exists(schemaPath)
                    || ESManagedFileIO.ContainsExistingReparsePoint(workerPath)
                    || ESManagedFileIO.ContainsExistingReparsePoint(lockPath)
                    || ESManagedFileIO.ContainsExistingReparsePoint(schemaPath))
                    throw new InvalidOperationException("飞书任务 Worker、依赖锁或 Schema 不存在或路径不安全。");
                if (!string.Equals(ComputeFileHash(workerPath), WorkerEntrypointHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ComputeFileHash(lockPath), PackageLockHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(ComputeFileHash(schemaPath), spec.SchemaHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("飞书任务 Worker、依赖锁或 Schema Hash 漂移。");
                return new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = Quote(workerPath) + " " + Quote(request.inputContractPath) + " "
                        + Quote(GetRunDirectory(request.runId)),
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

        private sealed class FeishuTaskEndpoint : IESAutomationTaskEndpoint,
            IESAutomationContractBoundEndpoint, IESAutomationCancellableTaskEndpoint
        {
            private readonly TaskSpec spec;

            public FeishuTaskEndpoint(TaskSpec spec)
            {
                this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
                Descriptor = new ESAutomationTaskDescriptor
                {
                    taskId = spec.TaskId,
                    taskVersion = TaskVersion,
                    category = "External/FeishuTasks",
                    displayName = spec.DisplayName,
                    summary = spec.Summary,
                    inputSchemaHash = spec.SchemaHash,
                    allowAiInvoke = true,
                    allowInPlayMode = false,
                };
            }

            public ESAutomationTaskDescriptor Descriptor { get; }

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
                => ESFeishuTaskAutomation.Run(spec, invocation);

            public ESAutomationTaskInvocationResult GetRun(string runId)
                => ESFeishuTaskAutomation.GetRun(spec, runId);

            public ESAutomationTaskInvocationResult SubmitInput(
                ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.Rejected("飞书任务合同不接受分阶段任意输入。");

            public ESAutomationTaskInvocationResult CancelRun(string runId, string actorId)
                => ESFeishuTaskAutomation.Cancel(spec, runId, actorId);

            public ESAutomationInvocationRequirements DescribeInvocation(
                ESAutomationTaskInvocation invocation)
            {
                string runId = !string.IsNullOrWhiteSpace(invocation?.invocationId)
                    ? invocation.invocationId : Guid.NewGuid().ToString("N");
                ESAutomationCapability required = ESAutomationCapability.ReadArtifacts
                    | ESAutomationCapability.WriteTemp | ESAutomationCapability.ExternalRead;
                if (spec.TaskId != MonitorTaskId && invocation?.dryRun == false)
                    required |= ESAutomationCapability.ExternalWrite;
                return new ESAutomationInvocationRequirements
                {
                    worker = CreateWorkerRegistration(),
                    requiredCapabilities = required,
                    dryRun = invocation?.dryRun ?? true,
                    readPaths = new List<string>
                    {
                        GetRunDirectory(runId),
                        Path.Combine(ESAutomationPathPolicy.ProjectRoot,
                            spec.SchemaPath.Replace('/', Path.DirectorySeparatorChar)),
                    },
                    writePaths = new List<string> { GetRunDirectory(runId) },
                };
            }
        }

        private sealed class TaskSpec
        {
            public TaskSpec(string taskId, string displayName, string summary, string schemaPath,
                string schemaHash, ESAutomationCapability capabilities,
                IEnumerable<string> operations)
            {
                TaskId = taskId;
                DisplayName = displayName;
                Summary = summary;
                SchemaPath = schemaPath;
                SchemaHash = schemaHash;
                Capabilities = capabilities;
                Operations = new HashSet<string>(operations, StringComparer.Ordinal);
            }

            public string TaskId { get; }
            public string DisplayName { get; }
            public string Summary { get; }
            public string SchemaPath { get; }
            public string SchemaHash { get; }
            public ESAutomationCapability Capabilities { get; }
            public HashSet<string> Operations { get; }
        }

        private sealed class ActiveRun
        {
            public TaskSpec Spec;
            public ESAutomationProcessExecution Execution;
            public ESAutomationRunRecord Record;
            public string RecordPath;
            public string ResultPath;
            public string Directory;
            public bool DryRun;
            public string Operation;
        }
    }

    internal sealed class ESFeishuTaskAutomationInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke() => ESFeishuTaskAutomation.Register();
    }
}
