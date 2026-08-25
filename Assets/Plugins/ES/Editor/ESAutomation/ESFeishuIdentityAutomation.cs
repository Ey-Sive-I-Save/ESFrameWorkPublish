using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace ES
{
    /// <summary>
    /// Machine-local Feishu role ownership. Raw external identity bindings stay under the
    /// ignored Automation Runs root and are never returned through normal task results.
    /// </summary>
    internal static class ESFeishuIdentityAutomation
    {
        internal const string TaskId = "es.feishu.identity.claim";
        internal const int TaskVersion = 1;
        internal const string WorkerType = "DotNet";
        internal const string WorkerId = "es.feishu.identity.csharp";
        internal const string WorkerVersion = "0.1.0";
        internal const string WorkerEntrypointHash = "ca2b700505014eb6817c9b6b42d1b479cc28e6190713a99fe9dd911b1cc08d73";
        internal const string InputSchemaHash = "2c87970d691504566588d02621354a62c8e7c1342533730180618992ff575c7c";
        private const int TimeoutSeconds = 10;
        private static readonly TimeSpan DryRunEvidenceLifetime = TimeSpan.FromMinutes(30);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly object StoreLock = new object();

        internal static void Register()
        {
            VerifyStaticFiles();
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion,
                    out ESAutomationTaskContract contract))
            {
                var capabilities = ESAutomationCapability.ReadArtifacts
                    | ESAutomationCapability.WriteTemp;
                contract = new ESAutomationTaskContract
                {
                    taskId = TaskId,
                    version = TaskVersion,
                    worker = CreateWorkerRegistration(),
                    inputs = new List<string> { "request.json" },
                    readRoots = new List<string> { "ES/Automation/Runs", "ES/Automation/Contracts",
                        "ES/Automation/Workers/DotNet" },
                    writeRoots = new List<string> { "ES/Automation/Runs" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteTemp" },
                    inputSchemaHash = InputSchemaHash,
                    timeoutSeconds = TimeoutSeconds,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "identity-result.json", "run-record.json" },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = TimeoutSeconds,
                        maxOutputBytes = 256L * 1024L,
                        maxRetryCount = 0,
                        maxFindingCount = 100,
                    },
                    capabilityEnvelope = new ESAutomationCapabilityEnvelope
                    {
                        userAuthorization = capabilities,
                        taskContract = capabilities,
                        aiCommand = capabilities,
                        workerCapability = capabilities,
                        projectBoundary = capabilities,
                    },
                };
                ESAutomationTaskRegistry.Register(contract);
            }
            else
            {
                if (contract.worker == null
                    || !SameWorker(contract.worker, CreateWorkerRegistration())
                    || !string.Equals(contract.inputSchemaHash, InputSchemaHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("已有飞书本地身份合同与受信 C# Worker 或 Schema 不一致。");
            }
            contract.Validate();
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new IdentityEndpoint());
        }

        internal static bool TryResolveTaskRoles(JArray roleBindings,
            IEnumerable<string> allowedRoles, out JArray members, out string resolutionHash,
            out string error)
        {
            members = new JArray();
            resolutionHash = string.Empty;
            error = string.Empty;
            if (roleBindings == null || roleBindings.Count < 1 || roleBindings.Count > 20)
            {
                error = "claimedRoles 必须是 1 到 20 项的数组。";
                return false;
            }
            if (!TryGetAppIdentityHash(out string appHash, out error)) return false;
            var allowed = new HashSet<string>(allowedRoles ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            var roleIds = new HashSet<string>(StringComparer.Ordinal);
            var bindingHashes = new List<string>();
            lock (StoreLock)
            {
                JObject store = ReadStore();
                foreach (JToken token in roleBindings)
                {
                    if (!(token is JObject binding)
                        || binding.Properties().Any(property => property.Name != "roleId"
                            && property.Name != "role"))
                    {
                        error = "claimedRoles 包含无效或未注册字段。";
                        return false;
                    }
                    string roleId = binding.Value<string>("roleId")?.Trim() ?? string.Empty;
                    string role = binding.Value<string>("role")?.Trim() ?? string.Empty;
                    if (!IsRoleId(roleId) || !roleIds.Add(roleId) || !allowed.Contains(role))
                    {
                        error = "claimedRoles 的 roleId 重复/无效，或 role 不属于当前操作。";
                        return false;
                    }
                    JObject claim = FindClaim(store, appHash, roleId);
                    if (claim == null || claim.Value<bool?>("allowTaskAssignment") != true)
                    {
                        error = "角色不存在、AppId 不匹配或未授权任务分配：" + roleId;
                        return false;
                    }
                    string id = claim.Value<string>("taskMemberId") ?? string.Empty;
                    string type = claim.Value<string>("taskMemberType") ?? string.Empty;
                    if (id.Length < 1 || id.Length > 128 || type != "user" && type != "app")
                    {
                        error = "角色缺少有效的飞书任务成员绑定：" + roleId;
                        return false;
                    }
                    members.Add(new JObject
                    {
                        ["id"] = id,
                        ["type"] = type,
                        ["role"] = role,
                    });
                    bindingHashes.Add(Sha256(appHash + "\n" + roleId + "\n" + role
                        + "\n" + type + "\n" + id + "\n"
                        + (claim.Value<string>("updatedAtUtc") ?? string.Empty)
                        + "\nallowTaskAssignment=true"));
                }
            }
            resolutionHash = Sha256(string.Join("\n", bindingHashes));
            return true;
        }

        internal static bool TryResolveMessageRole(string roleId, out string recipientId,
            out string recipientType, out string recipientRefHash, out string error)
        {
            recipientId = string.Empty;
            recipientType = string.Empty;
            recipientRefHash = string.Empty;
            error = string.Empty;
            roleId = roleId?.Trim() ?? string.Empty;
            if (!IsRoleId(roleId)) { error = "roleId 格式无效。"; return false; }
            if (!TryGetAppIdentityHash(out string appHash, out error)) return false;
            lock (StoreLock)
            {
                JObject claim = FindClaim(ReadStore(), appHash, roleId);
                if (claim == null || claim.Value<bool?>("allowDirectMessage") != true)
                {
                    error = "角色不存在、AppId 不匹配或未授权直接消息：" + roleId;
                    return false;
                }
                recipientId = claim.Value<string>("messageRecipientId") ?? string.Empty;
                recipientType = claim.Value<string>("messageRecipientType") ?? string.Empty;
                if (recipientId.Length < 1 || recipientId.Length > 128
                    || !AllowedRecipientTypes.Contains(recipientType))
                {
                    error = "角色缺少有效的飞书消息收件人绑定：" + roleId;
                    return false;
                }
                recipientRefHash = Sha256(appHash + "\n" + roleId + "\n" + recipientType
                    + "\n" + recipientId + "\n"
                    + (claim.Value<string>("updatedAtUtc") ?? string.Empty)
                    + "\nallowDirectMessage=true");
                return true;
            }
        }

        private static ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
        {
            if (invocation == null || invocation.input == null)
                return ESAutomationTaskInvocationResult.Rejected("缺少飞书本地身份输入。");
            if (!invocation.fromAi)
                return ESAutomationTaskInvocationResult.Blocked("飞书身份管理必须来自当前 AIBrain 有界计划。");
            if (!Guid.TryParseExact(invocation.invocationId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("InvocationId 必须是 N 格式 GUID。");
            if (!TryNormalizeInput(invocation.input, out JObject input, out string error))
                return ESAutomationTaskInvocationResult.Rejected(error);

            string operation = input.Value<string>("operation") ?? string.Empty;
            bool mutating = operation == "claim-role" || operation == "release-role";
            if (mutating && (string.IsNullOrWhiteSpace(invocation.actorId)
                    || string.Equals(invocation.actorId, "aibrain", StringComparison.Ordinal)
                    || invocation.actorId.Length > 128))
                return ESAutomationTaskInvocationResult.Blocked(
                    "角色写入必须提供当前用户明确、稳定且不超过 128 字符的 ActorId。");
            if (mutating && invocation.dryRun && input["dryRunEvidenceRunId"] != null)
                return ESAutomationTaskInvocationResult.Rejected(
                    "身份 DryRun 不得引用另一次 DryRun 回执。");
            if (mutating && !invocation.dryRun
                && !TryValidateDryRunEvidence(input, invocation.actorId, out error))
                return ESAutomationTaskInvocationResult.Blocked(error);
            if (mutating && !TryGetAppIdentityHash(out _, out error))
                return ESAutomationTaskInvocationResult.Blocked(error);

            string runId = invocation.invocationId;
            string runDirectory = GetRunDirectory(runId);
            string requestPath = Path.Combine(runDirectory, "request.json");
            string resultPath = Path.Combine(runDirectory, "identity-result.json");
            string recordPath = Path.Combine(runDirectory, "run-record.json");
            string invocationHash = Sha256(JsonConvert.SerializeObject(new
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                input,
                invocation.dryRun,
                invocation.brainPlanHash,
                invocation.idempotencyKey,
                actor = OwnerActorHash(invocation.actorId),
            }, Formatting.None));

            if (File.Exists(recordPath))
            {
                ESAutomationRunRecord existing = ReadRecord(recordPath);
                if (!string.Equals(existing.invocationHash, invocationHash,
                        StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Rejected(
                        "InvocationId 已绑定不同的身份管理输入，拒绝覆盖。");
                return GetRun(runId);
            }
            if (Directory.Exists(runDirectory))
                return ESAutomationTaskInvocationResult.Rejected(
                    "InvocationId 目录存在但没有有效 RunRecord，拒绝猜测恢复。");

            ESAutomationPathPolicy.EnsureWorkerDirectory(runDirectory,
                new[] { ESAutomationPathPolicy.RunsRoot });
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
                ["inputSchemaHash"] = InputSchemaHash,
                ["dryRun"] = invocation.dryRun,
                ["input"] = input,
            };
            WriteJsonAtomic(requestPath, request, GetIdentityRoot());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var record = new ESAutomationRunRecord
            {
                runId = runId,
                taskId = TaskId,
                taskVersion = TaskVersion,
                operatorId = OwnerActorHash(invocation.actorId),
                workerType = WorkerType,
                workerId = WorkerId,
                workerVersion = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                inputManifestHash = ComputeFileHash(requestPath),
                invocationHash = invocationHash,
                status = ESAutomationRunStatus.Starting,
                startedAtUtc = now.ToString("O"),
                lastUpdatedAtUtc = now.ToString("O"),
                operationDirectory = runDirectory,
            };
            WriteJsonAtomic(recordPath, record, GetIdentityRoot());

            try
            {
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Running);
                JObject result = Execute(operation, input, invocation, runId);
                WriteJsonAtomic(resultPath, result, GetIdentityRoot());
                record.outputs.Add(resultPath);
                record.outputHashes.Add(ComputeFileHash(resultPath));
                record.exitCode = 0;
                record.findings.Add(invocation.dryRun
                    ? "Feishu identity DryRun completed without network access or local mutation."
                    : "Feishu local identity operation completed: " + operation);
                ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Completed);
                WriteJsonAtomic(recordPath, record, GetIdentityRoot());
                return ESAutomationTaskInvocationResult.Completed(
                    invocation.dryRun ? "飞书本地身份 DryRun 已完成。" : "飞书本地身份操作已完成。",
                    runId, (JObject)result.DeepClone());
            }
            catch (Exception exception)
            {
                if (!ESAutomationRunStatus.IsTerminal(record.status))
                    ESAutomationRunStatus.Transition(record, ESAutomationRunStatus.Failed);
                record.exitCode = 1;
                record.errors.Add(SanitizeError(exception.GetBaseException().Message));
                WriteJsonAtomic(recordPath, record, GetIdentityRoot());
                return ESAutomationTaskInvocationResult.Failed("飞书本地身份操作失败。", runId);
            }
        }

        private static JObject Execute(string operation, JObject input,
            ESAutomationTaskInvocation invocation, string runId)
        {
            bool appConfigured = TryGetAppIdentityHash(out string appHash, out _);
            if (operation == "setup-status")
            {
                JObject store;
                lock (StoreLock) store = ReadStore();
                int teamCount = appConfigured ? Claims(store)
                    .Count(claim => claim.Value<string>("appIdentityHash") == appHash) : 0;
                int ownedCount = appConfigured ? Claims(store).Count(claim =>
                    claim.Value<string>("appIdentityHash") == appHash
                    && IsOwner(claim, invocation.actorId)) : 0;
                var next = new JArray();
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ES_AUTOMATION_NODE_PATH")))
                    next.Add("configure-managed-node-path");
                if (!appConfigured) next.Add("configure-feishu-app-id-in-unity-environment");
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ES_FEISHU_APP_SECRET")))
                    next.Add("configure-feishu-app-secret-in-unity-environment");
                if (ownedCount == 0) next.Add("dry-run-and-claim-personal-role");
                next.Add("authorize-managed-auth-status-before-network");
                return new JObject
                {
                    ["operation"] = operation,
                    ["dryRun"] = invocation.dryRun,
                    ["networkCalled"] = false,
                    ["mutationApplied"] = false,
                    ["managedNodeConfigured"] = !string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("ES_AUTOMATION_NODE_PATH")),
                    ["appIdConfigured"] = appConfigured,
                    ["appSecretConfigured"] = !string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("ES_FEISHU_APP_SECRET")),
                    ["appIdentityHash"] = appConfigured ? appHash : string.Empty,
                    ["ownedRoleCount"] = ownedCount,
                    ["teamRoleCount"] = teamCount,
                    ["nextActions"] = next,
                };
            }
            if (operation == "list-claims")
            {
                if (!appConfigured)
                    throw new InvalidOperationException("ES_FEISHU_APP_ID 未配置，无法选择本地身份分区。");
                JArray summaries;
                lock (StoreLock)
                {
                    summaries = new JArray(Claims(ReadStore())
                        .Where(claim => claim.Value<string>("appIdentityHash") == appHash)
                        .OrderBy(claim => claim.Value<string>("roleId"), StringComparer.Ordinal)
                        .Select(SanitizeClaim));
                }
                return new JObject
                {
                    ["operation"] = operation,
                    ["dryRun"] = invocation.dryRun,
                    ["networkCalled"] = false,
                    ["mutationApplied"] = false,
                    ["appIdentityHash"] = appHash,
                    ["claims"] = summaries,
                };
            }

            if (!appConfigured)
                throw new InvalidOperationException("ES_FEISHU_APP_ID 未配置，无法绑定本地角色。");
            string roleId = input.Value<string>("roleId") ?? string.Empty;
            if (invocation.dryRun)
            {
                lock (StoreLock)
                    ValidateMutationAgainstStore(operation, input, invocation.actorId, appHash,
                        ReadStore());
                return MutationResult(operation, input, invocation.actorId, appHash, roleId,
                    false, "would-apply");
            }
            lock (StoreLock)
            {
                JObject store = ReadStore();
                ValidateMutationAgainstStore(operation, input, invocation.actorId, appHash, store);
                if (operation == "claim-role") ApplyClaim(store, input, invocation.actorId,
                    appHash, runId);
                else ApplyRelease(store, roleId, invocation.actorId, appHash);
                WriteJsonAtomic(GetClaimStorePath(), store, GetIdentityRoot());
            }
            return MutationResult(operation, input, invocation.actorId, appHash, roleId, true,
                operation == "claim-role" ? "claimed" : "released");
        }

        private static JObject MutationResult(string operation, JObject input, string actorId,
            string appHash, string roleId, bool applied, string action)
            => new JObject
            {
                ["operation"] = operation,
                ["dryRun"] = !applied,
                ["networkCalled"] = false,
                ["mutationApplied"] = applied,
                ["action"] = action,
                ["roleId"] = roleId,
                ["appIdentityHash"] = appHash,
                ["ownerRefHash"] = OwnerRefHash(actorId, appHash),
                ["taskBindingConfigured"] = !string.IsNullOrWhiteSpace(
                    input.Value<string>("taskMemberId")),
                ["messageBindingConfigured"] = !string.IsNullOrWhiteSpace(
                    input.Value<string>("messageRecipientId")),
                ["taskBindingHash"] = HashOptionalBinding(input, "taskMemberType", "taskMemberId"),
                ["messageBindingHash"] = HashOptionalBinding(input, "messageRecipientType",
                    "messageRecipientId"),
                ["allowTaskAssignment"] = input.Value<bool?>("allowTaskAssignment") ?? false,
                ["allowDirectMessage"] = input.Value<bool?>("allowDirectMessage") ?? false,
                ["botOwnershipClaimed"] = input.Value<bool?>("claimBotOwnership") ?? false,
            };

        private static bool TryNormalizeInput(JObject source, out JObject input, out string error)
        {
            input = null;
            error = string.Empty;
            var candidate = (JObject)source.DeepClone();
            string operation = candidate.Value<string>("operation")?.Trim() ?? string.Empty;
            if (!new[] { "setup-status", "list-claims", "claim-role", "release-role" }
                    .Contains(operation, StringComparer.Ordinal))
            { error = "不支持的飞书身份操作：" + operation; return false; }
            candidate["operation"] = operation;
            IEnumerable<string> allowed = operation == "claim-role"
                ? new[] { "operation", "dryRunEvidenceRunId", "roleId", "displayName",
                    "taskMemberId", "taskMemberType", "messageRecipientId",
                    "messageRecipientType", "allowTaskAssignment", "allowDirectMessage",
                    "claimBotOwnership", "botAlias" }
                : operation == "release-role"
                    ? new[] { "operation", "dryRunEvidenceRunId", "roleId" }
                    : new[] { "operation" };
            JProperty unknown = candidate.Properties().FirstOrDefault(property =>
                !allowed.Contains(property.Name, StringComparer.Ordinal));
            if (unknown != null) { error = "未注册的身份输入字段：" + unknown.Name; return false; }
            if (operation == "claim-role" || operation == "release-role")
            {
                string roleId = candidate.Value<string>("roleId")?.Trim() ?? string.Empty;
                if (!IsRoleId(roleId)) { error = "roleId 必须为 2 到 48 位小写稳定标识。"; return false; }
                candidate["roleId"] = roleId;
            }
            string serialized = candidate.ToString(Formatting.None);
            if (Regex.IsMatch(serialized,
                    "(?i)(authorization|app[_-]?secret|client[_-]?secret|access[_-]?token|refresh[_-]?token|bearer)\\s*[:=]"))
            { error = "身份输入包含疑似凭据标记，已在持久化前拒绝。"; return false; }
            foreach (string variable in new[] { "ES_FEISHU_APP_ID", "ES_FEISHU_APP_SECRET" })
            {
                string secret = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(secret)
                    && serialized.IndexOf(secret, StringComparison.Ordinal) >= 0)
                { error = "身份输入包含受管凭据值，已在持久化前拒绝。"; return false; }
            }
            if (operation == "claim-role")
            {
                string displayName = candidate.Value<string>("displayName")?.Trim() ?? string.Empty;
                if (displayName.Length < 1 || displayName.Length > 60)
                { error = "displayName 必须是 1 到 60 个字符。"; return false; }
                candidate["displayName"] = displayName;
                bool hasTask = NormalizeOptionalString(candidate, "taskMemberId", 128, out error);
                if (!string.IsNullOrEmpty(error)) return false;
                bool hasMessage = NormalizeOptionalString(candidate, "messageRecipientId", 128, out error);
                if (!string.IsNullOrEmpty(error)) return false;
                bool claimBot = candidate.Value<bool?>("claimBotOwnership") ?? false;
                bool allowTask = candidate.Value<bool?>("allowTaskAssignment") ?? false;
                bool allowMessage = candidate.Value<bool?>("allowDirectMessage") ?? false;
                candidate["allowTaskAssignment"] = allowTask;
                candidate["allowDirectMessage"] = allowMessage;
                candidate["claimBotOwnership"] = claimBot;
                string taskType = candidate.Value<string>("taskMemberType") ?? string.Empty;
                string recipientType = candidate.Value<string>("messageRecipientType") ?? string.Empty;
                if (hasTask != !string.IsNullOrEmpty(taskType)
                    || hasTask && taskType != "user" && taskType != "app")
                { error = "taskMemberId 与 taskMemberType 必须成对出现且类型为 user/app。"; return false; }
                if (hasMessage != !string.IsNullOrEmpty(recipientType)
                    || hasMessage && !AllowedRecipientTypes.Contains(recipientType))
                { error = "messageRecipientId 与允许的 messageRecipientType 必须成对出现。"; return false; }
                if (allowTask && !hasTask || allowMessage && !hasMessage)
                { error = "启用分配/消息许可前必须配置对应身份绑定。"; return false; }
                string botAlias = candidate.Value<string>("botAlias")?.Trim() ?? string.Empty;
                if (claimBot && !IsRoleId(botAlias))
                { error = "claimBotOwnership=true 时必须提供合法 botAlias。"; return false; }
                if (!claimBot && candidate["botAlias"] != null)
                { error = "botAlias 仅可与 claimBotOwnership=true 同时使用。"; return false; }
                if (!hasTask && !hasMessage && !claimBot)
                { error = "角色至少需要任务绑定、消息绑定或机器人所有权之一。"; return false; }
            }
            if (candidate["dryRunEvidenceRunId"] != null)
            {
                string value = candidate.Value<string>("dryRunEvidenceRunId") ?? string.Empty;
                if (!Guid.TryParseExact(value, "N", out _))
                { error = "dryRunEvidenceRunId 必须是 N 格式 GUID。"; return false; }
            }
            input = Canonicalize(candidate);
            return true;
        }

        private static void ValidateMutationAgainstStore(string operation, JObject input,
            string actorId, string appHash, JObject store = null)
        {
            store = store ?? ReadStore();
            string roleId = input.Value<string>("roleId") ?? string.Empty;
            JObject existing = FindClaim(store, appHash, roleId);
            if (existing != null && !IsOwner(existing, actorId))
                throw new InvalidOperationException("ROLE_OWNED_BY_OTHER：角色已由其他本地主体认领。");
            if (operation == "release-role")
            {
                if (existing == null) throw new InvalidOperationException("ROLE_NOT_FOUND：角色不存在。");
                return;
            }
            if (input.Value<bool?>("claimBotOwnership") == true)
            {
                string botAlias = input.Value<string>("botAlias") ?? string.Empty;
                JObject conflict = Claims(store).FirstOrDefault(claim =>
                    claim.Value<string>("appIdentityHash") == appHash
                    && claim.Value<bool?>("claimBotOwnership") == true
                    && claim.Value<string>("botAlias") == botAlias
                    && !IsOwner(claim, actorId));
                if (conflict != null)
                    throw new InvalidOperationException("BOT_OWNED_BY_OTHER：该机器人别名已由其他本地主体认领。");
            }
        }

        private static void ApplyClaim(JObject store, JObject input, string actorId,
            string appHash, string runId)
        {
            string roleId = input.Value<string>("roleId") ?? string.Empty;
            JObject existing = FindClaim(store, appHash, roleId);
            string now = DateTimeOffset.UtcNow.ToString("O");
            var claim = new JObject
            {
                ["claimId"] = Sha256(appHash + "\n" + roleId),
                ["roleId"] = roleId,
                ["displayName"] = input.Value<string>("displayName") ?? string.Empty,
                ["ownerPrincipalHash"] = OwnerPrincipalHash(),
                ["ownerActorHash"] = OwnerActorHash(actorId),
                ["appIdentityHash"] = appHash,
                ["taskMemberId"] = input.Value<string>("taskMemberId") ?? string.Empty,
                ["taskMemberType"] = input.Value<string>("taskMemberType") ?? string.Empty,
                ["messageRecipientId"] = input.Value<string>("messageRecipientId") ?? string.Empty,
                ["messageRecipientType"] = input.Value<string>("messageRecipientType") ?? string.Empty,
                ["allowTaskAssignment"] = input.Value<bool?>("allowTaskAssignment") ?? false,
                ["allowDirectMessage"] = input.Value<bool?>("allowDirectMessage") ?? false,
                ["claimBotOwnership"] = input.Value<bool?>("claimBotOwnership") ?? false,
                ["botAlias"] = input.Value<string>("botAlias") ?? string.Empty,
                ["createdAtUtc"] = existing?.Value<string>("createdAtUtc") ?? now,
                ["updatedAtUtc"] = now,
                ["lastMutationRunId"] = runId,
            };
            JArray claims = (JArray)store["claims"];
            if (existing == null) claims.Add(claim);
            else existing.Replace(claim);
            store["revision"] = (store.Value<int?>("revision") ?? 0) + 1;
        }

        private static void ApplyRelease(JObject store, string roleId, string actorId,
            string appHash)
        {
            JObject existing = FindClaim(store, appHash, roleId);
            if (existing == null || !IsOwner(existing, actorId))
                throw new InvalidOperationException("角色不存在或当前本地主体不是所有者。");
            existing.Remove();
            store["revision"] = (store.Value<int?>("revision") ?? 0) + 1;
        }

        private static bool TryValidateDryRunEvidence(JObject liveInput, string actorId,
            out string error)
        {
            error = string.Empty;
            string runId = liveInput.Value<string>("dryRunEvidenceRunId") ?? string.Empty;
            if (!Guid.TryParseExact(runId, "N", out _))
            { error = "本地身份写入必须提供 N 格式 dryRunEvidenceRunId。"; return false; }
            try
            {
                string directory = GetRunDirectory(runId);
                string requestPath = Path.Combine(directory, "request.json");
                string resultPath = Path.Combine(directory, "identity-result.json");
                string recordPath = Path.Combine(directory, "run-record.json");
                if (!File.Exists(requestPath) || !File.Exists(resultPath) || !File.Exists(recordPath))
                { error = "未找到绑定的身份 DryRun 完整证据。"; return false; }
                ESAutomationRunRecord record = ReadRecord(recordPath);
                int outputIndex = record.outputs.FindIndex(path => string.Equals(
                    Path.GetFullPath(path), Path.GetFullPath(resultPath),
                    StringComparison.OrdinalIgnoreCase));
                if (record.status != ESAutomationRunStatus.Completed || record.exitCode != 0
                    || !string.Equals(record.operatorId, OwnerActorHash(actorId),
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(record.entrypointHash, WorkerEntrypointHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(record.inputManifestHash, ComputeFileHash(requestPath),
                        StringComparison.OrdinalIgnoreCase)
                    || outputIndex < 0 || outputIndex >= record.outputHashes.Count
                    || !ESAutomationPathPolicy.IsWithin(resultPath, new[] { directory })
                    || !string.Equals(record.outputHashes[outputIndex], ComputeFileHash(resultPath),
                        StringComparison.OrdinalIgnoreCase))
                { error = "绑定的身份 DryRun 身份、状态或 Hash 已漂移。"; return false; }
                if (!DateTimeOffset.TryParse(record.lastUpdatedAtUtc, out DateTimeOffset completed)
                    || DateTimeOffset.UtcNow - completed > DryRunEvidenceLifetime
                    || completed > DateTimeOffset.UtcNow.AddMinutes(1))
                { error = "绑定的身份 DryRun 已过期或时间无效。"; return false; }
                JObject request = JObject.Parse(File.ReadAllText(requestPath, StrictUtf8));
                JObject result = JObject.Parse(File.ReadAllText(resultPath, StrictUtf8));
                if (!TryGetAppIdentityHash(out string currentAppHash, out error)) return false;
                if (request.Value<bool?>("dryRun") != true
                    || request.Value<string>("entrypointHash") != WorkerEntrypointHash
                    || request.Value<string>("inputSchemaHash") != InputSchemaHash
                    || result.Value<bool?>("networkCalled") != false
                    || result.Value<bool?>("mutationApplied") != false
                    || !string.Equals(result.Value<string>("appIdentityHash"), currentAppHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(result.Value<string>("ownerRefHash"),
                        OwnerRefHash(actorId, currentAppHash),
                        StringComparison.OrdinalIgnoreCase))
                { error = "绑定的身份 DryRun 语义证据无效。"; return false; }
                JObject expected = request["input"] as JObject;
                var actual = (JObject)liveInput.DeepClone();
                actual.Remove("dryRunEvidenceRunId");
                if (expected == null || !JToken.DeepEquals(Canonicalize(expected),
                        Canonicalize(actual)))
                { error = "身份 Live 输入与绑定 DryRun 输入不一致。"; return false; }
                return true;
            }
            catch (Exception exception)
            {
                error = "身份 DryRun 证据校验失败：" + SanitizeError(exception.Message);
                return false;
            }
        }

        private static ESAutomationTaskInvocationResult GetRun(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _))
                return ESAutomationTaskInvocationResult.Rejected("RunId 必须是 N 格式 GUID。");
            string recordPath = Path.Combine(GetRunDirectory(runId), "run-record.json");
            if (!File.Exists(recordPath))
                return ESAutomationTaskInvocationResult.NotFound("未找到飞书身份 RunRecord。");
            ESAutomationRunRecord record = ReadRecord(recordPath);
            string requestPath = Path.Combine(GetRunDirectory(runId), "request.json");
            if (!File.Exists(requestPath)
                || !string.Equals(record.inputManifestHash, ComputeFileHash(requestPath),
                    StringComparison.OrdinalIgnoreCase))
                return ESAutomationTaskInvocationResult.Failed(
                    "飞书身份 Run 输入文件缺失或 Hash 已漂移。", runId);
            JObject data = new JObject
            {
                ["status"] = record.status,
                ["exitCode"] = record.exitCode,
                ["errors"] = JArray.FromObject(record.errors ?? new List<string>()),
            };
            string resultPath = Path.Combine(GetRunDirectory(runId), "identity-result.json");
            if (ESAutomationRunStatus.IsTerminal(record.status) && File.Exists(resultPath))
            {
                int outputIndex = record.outputs.FindIndex(path => string.Equals(
                    Path.GetFullPath(path), Path.GetFullPath(resultPath),
                    StringComparison.OrdinalIgnoreCase));
                if (outputIndex < 0 || outputIndex >= record.outputHashes.Count
                    || !string.Equals(record.outputHashes[outputIndex], ComputeFileHash(resultPath),
                        StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Failed(
                        "飞书身份 Run 输出文件 Hash 已漂移。", runId);
                data["result"] = JObject.Parse(File.ReadAllText(resultPath, StrictUtf8));
            }
            return ESAutomationRunStatus.IsTerminal(record.status)
                ? ESAutomationTaskInvocationResult.Completed("飞书身份 Run 已结束：" + record.status,
                    runId, data)
                : ESAutomationTaskInvocationResult.Starting("飞书身份 Run 正在执行：" + record.status,
                    runId, data);
        }

        private static JObject ReadStore()
        {
            string path = GetClaimStorePath();
            if (!File.Exists(path))
                return new JObject { ["schemaVersion"] = 1, ["revision"] = 0,
                    ["claims"] = new JArray() };
            JObject store = JObject.Parse(File.ReadAllText(path, StrictUtf8));
            if (store.Value<int?>("schemaVersion") != 1 || !(store["claims"] is JArray))
                throw new InvalidDataException("飞书本地身份存储协议无效。");
            return store;
        }

        private static IEnumerable<JObject> Claims(JObject store)
            => (store?["claims"] as JArray ?? new JArray()).OfType<JObject>();

        private static JObject FindClaim(JObject store, string appHash, string roleId)
            => Claims(store).SingleOrDefault(claim =>
                claim.Value<string>("appIdentityHash") == appHash
                && claim.Value<string>("roleId") == roleId);

        private static JObject SanitizeClaim(JObject claim)
            => new JObject
            {
                ["roleId"] = claim.Value<string>("roleId") ?? string.Empty,
                ["displayName"] = claim.Value<string>("displayName") ?? string.Empty,
                ["ownerRefHash"] = Sha256((claim.Value<string>("ownerPrincipalHash") ?? string.Empty)
                    + "\n" + (claim.Value<string>("ownerActorHash") ?? string.Empty)
                    + "\n" + (claim.Value<string>("appIdentityHash") ?? string.Empty)),
                ["taskBindingConfigured"] = !string.IsNullOrWhiteSpace(
                    claim.Value<string>("taskMemberId")),
                ["messageBindingConfigured"] = !string.IsNullOrWhiteSpace(
                    claim.Value<string>("messageRecipientId")),
                ["allowTaskAssignment"] = claim.Value<bool?>("allowTaskAssignment") ?? false,
                ["allowDirectMessage"] = claim.Value<bool?>("allowDirectMessage") ?? false,
                ["botOwnershipClaimed"] = claim.Value<bool?>("claimBotOwnership") ?? false,
                ["updatedAtUtc"] = claim.Value<string>("updatedAtUtc") ?? string.Empty,
            };

        private static bool IsOwner(JObject claim, string actorId)
            => claim != null
                && claim.Value<string>("ownerPrincipalHash") == OwnerPrincipalHash()
                && claim.Value<string>("ownerActorHash") == OwnerActorHash(actorId);

        private static string OwnerPrincipalHash()
            => Sha256((Environment.UserDomainName ?? string.Empty) + "\\"
                + (Environment.UserName ?? string.Empty));

        private static string OwnerActorHash(string actorId)
            => Sha256((actorId ?? string.Empty).Trim());

        private static string OwnerRefHash(string actorId, string appHash)
            => Sha256(OwnerPrincipalHash() + "\n" + OwnerActorHash(actorId)
                + "\n" + appHash);

        private static bool TryGetAppIdentityHash(out string hash, out string error)
        {
            string appId = Environment.GetEnvironmentVariable("ES_FEISHU_APP_ID")?.Trim()
                ?? string.Empty;
            if (appId.Length < 1 || appId.Length > 256)
            { hash = string.Empty; error = "ES_FEISHU_APP_ID 未配置或格式无效。"; return false; }
            hash = Sha256(appId);
            error = string.Empty;
            return true;
        }

        private static bool NormalizeOptionalString(JObject input, string name, int maximum,
            out string error)
        {
            error = string.Empty;
            JToken token = input[name];
            if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type != JTokenType.String) { error = name + " 必须是字符串。"; return false; }
            string value = token.Value<string>()?.Trim() ?? string.Empty;
            if (value.Length < 1 || value.Length > maximum)
            { error = name + " 必须是 1 到 " + maximum + " 个字符。"; return false; }
            input[name] = value;
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

        private static bool IsRoleId(string value)
            => !string.IsNullOrWhiteSpace(value)
                && Regex.IsMatch(value, "^[a-z0-9][a-z0-9._-]{1,47}$");

        private static readonly HashSet<string> AllowedRecipientTypes = new HashSet<string>(
            new[] { "open_id", "user_id", "union_id", "email", "chat_id" },
            StringComparer.Ordinal);

        private static string HashOptionalBinding(JObject input, string typeName, string idName)
        {
            string id = input.Value<string>(idName) ?? string.Empty;
            return id.Length == 0 ? string.Empty
                : Sha256((input.Value<string>(typeName) ?? string.Empty) + "\n" + id);
        }

        private static string GetIdentityRoot()
            => Path.Combine(ESAutomationPathPolicy.RunsRoot, "FeishuIdentity");

        private static string GetRunDirectory(string runId)
            => Path.Combine(GetIdentityRoot(), "Runs", runId);

        private static string GetClaimStorePath()
            => Path.Combine(GetIdentityRoot(), "claims.json");

        private static string GetSchemaPath()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts",
                "feishu-identity-claim-v1.schema.json");

        private static string GetManifestPath()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers",
                "DotNet", "FeishuIdentity", "worker-manifest.json");

        private static string GetSourcePath()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets", "Plugins", "ES",
                "Editor", "ESAutomation", "ESFeishuIdentityAutomation.cs");

        private static void VerifyStaticFiles()
        {
            string schemaPath = GetSchemaPath();
            string manifestPath = GetManifestPath();
            string sourcePath = GetSourcePath();
            if (!File.Exists(schemaPath) || !File.Exists(manifestPath) || !File.Exists(sourcePath)
                || ESManagedFileIO.ContainsExistingReparsePoint(schemaPath)
                || ESManagedFileIO.ContainsExistingReparsePoint(manifestPath)
                || ESManagedFileIO.ContainsExistingReparsePoint(sourcePath)
                || !string.Equals(ComputeFileHash(schemaPath), InputSchemaHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ComputeFileHash(manifestPath), WorkerEntrypointHash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("飞书身份 Schema 或受信 C# Worker Manifest 缺失/Hash 漂移。");
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath, StrictUtf8));
            if (!string.Equals(manifest.Value<string>("sourceContractHash"),
                    ComputeSourceContractHash(sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("飞书身份 C# Worker 源码合同 Hash 漂移。");
        }

        private static string ComputeSourceContractHash(string sourcePath)
        {
            string source = File.ReadAllText(sourcePath, StrictUtf8);
            string normalized = Regex.Replace(source,
                "internal const string WorkerEntrypointHash = \"[a-f0-9]{64}\";",
                "internal const string WorkerEntrypointHash = \"<MANIFEST_HASH>\";",
                RegexOptions.CultureInvariant);
            return Sha256(normalized);
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

        private static bool SameWorker(ESAutomationWorkerRegistration left,
            ESAutomationWorkerRegistration right)
            => left != null && right != null && left.type == right.type
                && left.workerId == right.workerId && left.version == right.version
                && string.Equals(left.entrypointHash, right.entrypointHash,
                    StringComparison.OrdinalIgnoreCase);

        private static void WriteJsonAtomic(string path, object value, string allowedRoot)
        {
            ESAutomationPathPolicy.WriteWorkerTextAtomic(path,
                JsonConvert.SerializeObject(value, Formatting.Indented) + "\n",
                new[] { allowedRoot });
        }

        private static ESAutomationRunRecord ReadRecord(string path)
        {
            ESAutomationRunRecord record = JsonConvert.DeserializeObject<ESAutomationRunRecord>(
                File.ReadAllText(path, StrictUtf8));
            if (record == null || record.taskId != TaskId || record.taskVersion != TaskVersion)
                throw new InvalidDataException("飞书身份 RunRecord 身份无效。");
            return record;
        }

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
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value
                        ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string SanitizeError(string value)
        {
            string result = value ?? string.Empty;
            foreach (string variable in new[] { "ES_FEISHU_APP_ID", "ES_FEISHU_APP_SECRET" })
            {
                string secret = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(secret)) result = result.Replace(secret, "[REDACTED]");
            }
            return result.Length <= 1000 ? result : result.Substring(0, 1000);
        }

        private sealed class IdentityEndpoint : IESAutomationTaskEndpoint,
            IESAutomationContractBoundEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor { get; } = new ESAutomationTaskDescriptor
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                category = "External/FeishuIdentity",
                displayName = "飞书本地角色认领",
                summary = "引导配置并管理机器本地、AppId 隔离的角色和机器人所有权。",
                inputSchemaHash = InputSchemaHash,
                allowAiInvoke = true,
                allowInPlayMode = false,
            };

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
                => ESFeishuIdentityAutomation.Run(invocation);

            public ESAutomationTaskInvocationResult GetRun(string runId)
                => ESFeishuIdentityAutomation.GetRun(runId);

            public ESAutomationTaskInvocationResult SubmitInput(
                ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.Rejected("飞书身份合同不接受分阶段任意输入。");

            public ESAutomationInvocationRequirements DescribeInvocation(
                ESAutomationTaskInvocation invocation)
            {
                string runId = !string.IsNullOrWhiteSpace(invocation?.invocationId)
                    ? invocation.invocationId : Guid.NewGuid().ToString("N");
                return new ESAutomationInvocationRequirements
                {
                    worker = CreateWorkerRegistration(),
                    requiredCapabilities = ESAutomationCapability.ReadArtifacts
                        | ESAutomationCapability.WriteTemp,
                    dryRun = invocation?.dryRun ?? true,
                    readPaths = new List<string> { GetIdentityRoot(), GetSchemaPath(),
                        GetManifestPath() },
                    writePaths = new List<string> { GetRunDirectory(runId),
                        GetClaimStorePath() },
                };
            }
        }
    }

    internal sealed class ESFeishuIdentityAutomationInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke() => ESFeishuIdentityAutomation.Register();
    }
}
