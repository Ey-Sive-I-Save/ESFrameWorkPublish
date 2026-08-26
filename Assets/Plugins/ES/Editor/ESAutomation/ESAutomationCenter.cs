using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using EditorUtility = ES.ESDesignUtility.SafeEditor;

namespace ES
{
    [Flags]
    public enum ESAutomationCapability
    {
        None = 0,
        ReadArtifacts = 1 << 0,
        WriteReports = 1 << 1,
        WriteAssets = 1 << 2,
        Delete = 1 << 3,
        Upload = 1 << 4,
        Publish = 1 << 5,
        WriteTemp = 1 << 6,
        ExternalRead = 1 << 7,
        ExternalWrite = 1 << 8,
        // Fixed, domain-scoped UI materialization. This is intentionally distinct from
        // the forbidden generic WriteAssets capability.
        MaterializeUI = 1 << 9,
    }

    /// <summary>
    /// Automation 运行记录的稳定状态机。Starting 只代表宿主进程已创建，
    /// Accepted 必须由受控会话事件确认，终态才允许写入 finishedAtUtc。
    /// </summary>
    public static class ESAutomationRunStatus
    {
        public const string Created = "Created";
        public const string Starting = "Starting";
        public const string Accepted = "Accepted";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string TimedOut = "TimedOut";
        public const string Blocked = "Blocked";
        public const string DryRun = "DryRun";

        public static bool IsTerminal(string status)
            => status == Completed || status == Failed || status == Cancelled
                || status == TimedOut || status == Blocked || status == DryRun;

        public static bool TryTransition(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.Ordinal)) return true;
            if (from == Created) return to == Starting || to == Failed || to == Blocked || to == DryRun;
            if (from == Starting) return to == Accepted || to == Running || to == Failed
                || to == Cancelled || to == TimedOut || to == Blocked;
            if (from == Accepted) return to == Running || to == Completed || to == Failed
                || to == Cancelled || to == TimedOut;
            if (from == Running) return to == Completed || to == Failed || to == Cancelled || to == TimedOut;
            return false;
        }

        public static void Transition(ESAutomationRunRecord record, string next)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!TryTransition(record.status, next))
                throw new InvalidOperationException("Automation RunRecord 非法状态迁移："
                    + record.status + " -> " + next);
            record.status = next;
            record.lastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            if (IsTerminal(next) && string.IsNullOrWhiteSpace(record.finishedAtUtc))
                record.finishedAtUtc = record.lastUpdatedAtUtc;
        }
    }

    [Serializable]
    public sealed class ESAutomationWorkerRegistration
    {
        public string type = string.Empty;
        public string workerId = string.Empty;
        public string version = string.Empty;
        public string entrypointHash = string.Empty;

        // 启用状态属于 C# Editor 本地受信注册表，不属于跨语言 TaskContract JSON。
        [JsonIgnore]
        public bool enabled;

        public void Validate()
        {
            if (type != "Python" && type != "PowerShell" && type != "DotNet" && type != "Other")
                throw new InvalidOperationException("Worker 类型不受支持：" + type);
            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("Worker 必须有稳定 ID 和版本。");
            if (!IsSha256(entrypointHash))
                throw new InvalidOperationException("Worker 必须声明 64 位 SHA-256 入口指纹。");
        }

        internal static bool IsSha256(string value)
            => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$");
    }

    [Serializable]
    public sealed class ESAutomationTaskContract
    {
        public const int MaximumTimeoutSeconds = 7200;

        public int protocolVersion = 1;
        public string taskId = string.Empty;
        public int version = 1;
        public ESAutomationWorkerRegistration worker = new ESAutomationWorkerRegistration();
        public List<string> inputs = new List<string>();
        public List<string> readRoots = new List<string>();
        public List<string> writeRoots = new List<string>();
        // 与 JSON Schema 保持字符串数组一致；Flags 只在 C# 受信层按需解析。
        public List<string> capabilities = new List<string>();
        /// <summary>
        /// 可选的输入语义指纹。为空时保持旧 TaskContract 兼容；非空时必须与 Facade Descriptor 一致。
        /// </summary>
        public string inputSchemaHash = string.Empty;
        public int timeoutSeconds = 600;
        public bool supportsDryRun = true;
        /// <summary>
        /// 仅当同一输入重复调用不会扩大副作用时开启。AISkill 工作流据此决定能否自动重试；默认关闭。
        /// </summary>
        public bool supportsRetry;
        public List<string> outputs = new List<string>();
        public ESAutomationPerformanceBudget performanceBudget;
        // Optional governance extensions. Kept nullable for legacy Worker compatibility.
        public ESAutomationAcceptanceCriteria acceptanceCriteria;
        public ESAutomationCapabilityEnvelope capabilityEnvelope;

        public void Validate()
        {
            if (protocolVersion != 1) throw new InvalidOperationException("不支持的 Automation 任务协议版本。");
            if (string.IsNullOrWhiteSpace(taskId) || !Regex.IsMatch(taskId, "^es\\.[a-z0-9]+(?:\\.[a-z0-9-]+)+$")) throw new InvalidOperationException("TaskId 必须符合 es.<domain>.<name> 的稳定命名。");
            if (version < 1 || timeoutSeconds < 1 || timeoutSeconds > MaximumTimeoutSeconds)
                throw new InvalidOperationException("任务版本和超时必须位于 1–7200 秒范围内。");
            if (!string.IsNullOrWhiteSpace(inputSchemaHash) && !ESAutomationWorkerRegistration.IsSha256(inputSchemaHash))
                throw new InvalidOperationException("TaskContract InputSchemaHash 无效。");
            if (worker == null) throw new InvalidOperationException("TaskContract 缺少 Worker 声明。");
            worker.Validate();
            ESAutomationCapability resolvedCapabilities = ResolveCapabilities();
            if (acceptanceCriteria != null) acceptanceCriteria.Validate();
            if (capabilityEnvelope != null) capabilityEnvelope.Validate();
            if (performanceBudget != null)
            {
                performanceBudget.Validate();
                if (timeoutSeconds > performanceBudget.maxDurationSeconds)
                    throw new InvalidOperationException("TaskContract timeout exceeds PerformanceBudget.");
                if (!supportsRetry && performanceBudget.maxRetryCount != 0)
                    throw new InvalidOperationException("TaskContract disables retry but PerformanceBudget allows retries.");
            }
            if (capabilityEnvelope != null)
            {
                if (capabilityEnvelope.taskContract != resolvedCapabilities)
                    throw new InvalidOperationException("CapabilityEnvelope.taskContract does not match TaskContract capabilities.");
                if ((capabilityEnvelope.workerCapability & ~resolvedCapabilities) != ESAutomationCapability.None)
                    throw new InvalidOperationException("CapabilityEnvelope.workerCapability exceeds TaskContract capabilities.");
                if ((capabilityEnvelope.projectBoundary & ~resolvedCapabilities) != ESAutomationCapability.None)
                    throw new InvalidOperationException("CapabilityEnvelope.projectBoundary exceeds TaskContract capabilities.");
            }

            if ((resolvedCapabilities & ESAutomationCapability.WriteAssets) != 0)
                throw new InvalidOperationException("受管 Worker 不得声明 Unity Assets 写权限。");
            if ((resolvedCapabilities & (ESAutomationCapability.Delete | ESAutomationCapability.Upload | ESAutomationCapability.Publish)) != 0)
                throw new InvalidOperationException("管理骨架阶段禁止注册删除、上传或发布 Worker。");
            if ((resolvedCapabilities & ESAutomationCapability.ExternalWrite) != 0
                && (!supportsDryRun || capabilityEnvelope == null))
                throw new InvalidOperationException("外部写任务必须支持 DryRun 并声明 CapabilityEnvelope。");
            if ((resolvedCapabilities & ESAutomationCapability.ReadArtifacts) != 0 && (readRoots == null || readRoots.Count == 0))
                throw new InvalidOperationException("ReadArtifacts 任务必须声明 ReadRoots。");
            if ((resolvedCapabilities & (ESAutomationCapability.WriteReports | ESAutomationCapability.WriteTemp)) != 0 && (writeRoots == null || writeRoots.Count == 0))
                throw new InvalidOperationException("WriteReports 或 WriteTemp 任务必须声明 WriteRoots。");
            foreach (string root in readRoots ?? Enumerable.Empty<string>()) ESAutomationPathPolicy.EnsureDeclaredReadRoot(root);
            foreach (string root in writeRoots ?? Enumerable.Empty<string>()) ESAutomationPathPolicy.EnsureDeclaredWriteRoot(root);
            ValidateInputDeclarations();
            ValidateOutputDeclarations();
        }

        private void ValidateInputDeclarations()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string input in inputs ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(input))
                    throw new InvalidOperationException("TaskContract inputs 不得包含空声明。");
                string normalized = input.Replace('\\', '/').Trim();
                if (Path.IsPathRooted(normalized) || normalized.Contains(":")
                    || normalized.Split('/').Any(segment => segment == ".."))
                    throw new InvalidOperationException("TaskContract inputs 必须是受管 Run 目录下的相对输入名：" + input);
                if (!seen.Add(normalized))
                    throw new InvalidOperationException("TaskContract inputs 包含重复声明：" + input);
            }
        }

        private void ValidateOutputDeclarations()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string output in outputs ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(output))
                    throw new InvalidOperationException("TaskContract outputs 不得包含空声明。");
                string normalized = output.Replace('\\', '/').Trim();
                if (Path.IsPathRooted(normalized) || normalized.Contains(":")
                    || normalized.Split('/').Any(segment => segment == ".."))
                    throw new InvalidOperationException("TaskContract outputs 必须是项目 Run 目录内的相对路径：" + output);
                if (!seen.Add(normalized))
                    throw new InvalidOperationException("TaskContract outputs 包含重复声明：" + output);
            }
        }

        public ESAutomationCapability ResolveCapabilities()
        {
            ESAutomationCapability result = ESAutomationCapability.None;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string capability in capabilities ?? Enumerable.Empty<string>())
            {
                if (!seen.Add(capability ?? string.Empty)) throw new InvalidOperationException("TaskContract 包含重复能力：" + capability);
                switch (capability)
                {
                    case "ReadArtifacts": result |= ESAutomationCapability.ReadArtifacts; break;
                    case "WriteReports": result |= ESAutomationCapability.WriteReports; break;
                    case "WriteAssets": result |= ESAutomationCapability.WriteAssets; break;
                    case "Delete": result |= ESAutomationCapability.Delete; break;
                    case "Upload": result |= ESAutomationCapability.Upload; break;
                    case "Publish": result |= ESAutomationCapability.Publish; break;
                    case "WriteTemp": result |= ESAutomationCapability.WriteTemp; break;
                    case "ExternalRead": result |= ESAutomationCapability.ExternalRead; break;
                    case "ExternalWrite": result |= ESAutomationCapability.ExternalWrite; break;
                    case "MaterializeUI": result |= ESAutomationCapability.MaterializeUI; break;
                    default: throw new InvalidOperationException("TaskContract 包含未知能力：" + capability);
                }
            }
            return result;
        }

        /// <summary>
        /// 计算不包含本机运行时 enabled 标志的稳定合同摘要，供 ExecutionSnapshot 绑定。
        /// </summary>
        public string ComputeStableHash()
        {
            Validate();
            JToken normalized = Canonicalize(JsonConvert.SerializeObject(this, Formatting.None));
            string canonical = normalized.ToString(Formatting.None);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JToken Canonicalize(string json)
        {
            return CanonicalizeToken(JToken.Parse(json));
        }

        private static JToken CanonicalizeToken(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
                    result.Add(property.Name, CanonicalizeToken(property.Value));
                return result;
            }
            if (token is JArray array)
            {
                var result = new JArray();
                foreach (JToken item in array) result.Add(CanonicalizeToken(item));
                return result;
            }
            return token.DeepClone();
        }
    }

    [Serializable]
    public sealed class ESAutomationExternalSourceRef
    {
        public string provider = string.Empty;
        public string tenantHash = string.Empty;
        public string spaceIdHash = string.Empty;
        public string objectType = string.Empty;
        public string objectTokenHash = string.Empty;
        public string remoteVersion = string.Empty;
        public string updatedAtUtc = string.Empty;
        public string retrievedAtUtc = string.Empty;
        public string contentHash = string.Empty;
        public string classification = string.Empty;
        public string sanitizerVersion = string.Empty;

        public void Validate()
        {
            if (!string.Equals(provider, "feishu", StringComparison.Ordinal))
                throw new InvalidOperationException("External SourceRef provider is invalid.");
            ValidateHash(tenantHash, nameof(tenantHash));
            ValidateHash(spaceIdHash, nameof(spaceIdHash));
            ValidateHash(objectTokenHash, nameof(objectTokenHash));
            if (string.IsNullOrWhiteSpace(objectType))
                throw new InvalidOperationException("External SourceRef requires objectType.");
            if (!string.IsNullOrWhiteSpace(contentHash)) ValidateHash(contentHash, nameof(contentHash));
            if (!string.IsNullOrWhiteSpace(updatedAtUtc)
                && !DateTimeOffset.TryParse(updatedAtUtc, out _))
                throw new InvalidOperationException("External SourceRef updatedAtUtc is invalid.");
            if (!DateTimeOffset.TryParse(retrievedAtUtc, out _))
                throw new InvalidOperationException("External SourceRef retrievedAtUtc is invalid.");
            if (!string.Equals(classification, "ExternalCollaboration", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(sanitizerVersion))
                throw new InvalidOperationException("External SourceRef classification or sanitizer is invalid.");
        }

        private static void ValidateHash(string value, string name)
        {
            if (!ESAutomationWorkerRegistration.IsSha256(value))
                throw new InvalidOperationException("External SourceRef requires SHA-256 " + name + ".");
        }
    }

    [Serializable]
    public sealed class ESAutomationExternalEvidenceReceipt
    {
        public int protocolVersion = 1;
        public string planHash = string.Empty;
        public string commandId = string.Empty;
        public string taskId = string.Empty;
        public int taskVersion;
        public string governanceHash = string.Empty;
        public bool dryRun;
        public string operation = string.Empty;
        public string runId = string.Empty;
        public string invocationHash = string.Empty;
        public string inputManifestHash = string.Empty;
        public List<string> outputHashes = new List<string>();
        public string evidenceScope = string.Empty;
        public string classification = string.Empty;
        public string sanitizerVersion = string.Empty;
        public bool networkCalled;
        public int exitCode;
        public string startedAtUtc = string.Empty;
        public string completedAtUtc = string.Empty;
        public string runtimeAuthorizationRef = string.Empty;
        public string credentialSourceType = string.Empty;
        public string tenantHash = string.Empty;
        public string spacePolicyHash = string.Empty;
        public int redactionCount;
        public List<ESAutomationExternalSourceRef> sourceRefs = new List<ESAutomationExternalSourceRef>();
        public List<string> unresolvedGaps = new List<string>();

        public void Validate()
        {
            if (protocolVersion != 1) throw new InvalidOperationException("External receipt protocol is invalid.");
            ValidateHash(planHash, nameof(planHash));
            ValidateHash(governanceHash, nameof(governanceHash));
            ValidateHash(invocationHash, nameof(invocationHash));
            ValidateHash(inputManifestHash, nameof(inputManifestHash));
            if (!Guid.TryParseExact(runId, "N", out _))
                throw new InvalidOperationException("External receipt runId is invalid.");
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(taskId)
                || taskVersion < 1 || string.IsNullOrWhiteSpace(operation))
                throw new InvalidOperationException("External receipt requires command and operation identity.");
            if (outputHashes == null || sourceRefs == null || unresolvedGaps == null)
                throw new InvalidOperationException("External receipt collections cannot be null.");
            foreach (string outputHash in outputHashes) ValidateHash(outputHash, nameof(outputHashes));
            foreach (ESAutomationExternalSourceRef sourceRef in sourceRefs)
            {
                if (sourceRef == null) throw new InvalidOperationException("External receipt contains a null SourceRef.");
                sourceRef.Validate();
            }
            if (!string.Equals(classification, "ExternalCollaboration", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(sanitizerVersion))
                throw new InvalidOperationException("External receipt classification or sanitizer is invalid.");
            if (!DateTimeOffset.TryParse(startedAtUtc, out DateTimeOffset started)
                || !DateTimeOffset.TryParse(completedAtUtc, out DateTimeOffset completed)
                || completed < started)
                throw new InvalidOperationException("External receipt timestamps are invalid.");
            if (redactionCount < 0) throw new InvalidOperationException("External receipt redactionCount is invalid.");
            if (dryRun)
            {
                if (networkCalled || !string.Equals(evidenceScope, "Static", StringComparison.Ordinal))
                    throw new InvalidOperationException("External DryRun receipt cannot claim Runtime network evidence.");
            }
            else
            {
                if (!networkCalled || !string.Equals(evidenceScope, "Runtime", StringComparison.Ordinal))
                    throw new InvalidOperationException("External live receipt requires Runtime network evidence.");
                ValidateHash(runtimeAuthorizationRef, nameof(runtimeAuthorizationRef));
                ValidateHash(tenantHash, nameof(tenantHash));
                ValidateHash(spacePolicyHash, nameof(spacePolicyHash));
                if (string.IsNullOrWhiteSpace(credentialSourceType))
                    throw new InvalidOperationException("External live receipt requires credentialSourceType.");
            }
        }

        private static void ValidateHash(string value, string name)
        {
            if (!ESAutomationWorkerRegistration.IsSha256(value))
                throw new InvalidOperationException("External receipt requires SHA-256 " + name + ".");
        }
    }

    [Serializable]
    public sealed class ESAutomationRunRecord
    {
        public int protocolVersion = 1;
        public string runId = Guid.NewGuid().ToString("N");
        public string taskId = string.Empty;
        public int taskVersion;
        public string operatorId = string.Empty;
        public string gitCommit = string.Empty;
        public string unityVersion = Application.unityVersion;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string inputManifestHash = string.Empty;
        public string invocationHash = string.Empty;
        public int riskPolicyVersion;
        public string riskAcceptanceHash = string.Empty;
        public string riskAcceptedAtUtc = string.Empty;
        public string riskAcceptedBy = string.Empty;
        public List<string> acceptedRiskCodes = new List<string>();
        public string status = ESAutomationRunStatus.Created;
        public int exitCode = -1;
        public int retryCount;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string lastUpdatedAtUtc = string.Empty;
        public string sessionId = string.Empty;
        public string messageId = string.Empty;
        public string operationDirectory = string.Empty;
        public int processId;
        public int codexProcessId;
        public string threadId = string.Empty;
        public List<string> outputs = new List<string>();
        public List<string> outputHashes = new List<string>();
        public List<string> findings = new List<string>();
        public List<string> errors = new List<string>();
        // Optional governance evidence; legacy records may leave these fields null.
        public string idempotencyKey = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
        public ESAutomationCompletionDecision completionDecision;
        public ESAutomationTraceReconciliation traceReconciliation;
        // Optional, validated projection for untrusted external collaboration evidence.
        public ESAutomationExternalEvidenceReceipt externalEvidence;
    }

    [Serializable]
    public sealed class ESAutomationRunResult
    {
        public int protocolVersion = 1;
        public string taskId = string.Empty;
        public int taskVersion;
        public string runId = string.Empty;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string status = "Blocked";
        public int exitCode = -1;
        public int retryCount;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string inputManifestHash = string.Empty;
        public List<string> outputs = new List<string>();
        public List<string> outputHashes = new List<string>();
        public List<string> findings = new List<string>();
        public List<string> errors = new List<string>();
        public string idempotencyKey = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
        public ESAutomationCompletionDecision completionDecision;
        public ESAutomationTraceReconciliation traceReconciliation;

        public void Validate()
        {
            if (protocolVersion != 1) throw new InvalidOperationException("不支持的 Automation 结果协议版本。");
            if (string.IsNullOrWhiteSpace(taskId) || !Regex.IsMatch(taskId, "^es\\.[a-z0-9]+(?:\\.[a-z0-9-]+)+$"))
                throw new InvalidOperationException("RunResult 的 TaskId 无效。");
            if (taskVersion < 1) throw new InvalidOperationException("RunResult 的任务版本无效。");
            if (!Guid.TryParseExact(runId, "N", out _)) throw new InvalidOperationException("RunResult 的 RunId 必须是 N 格式 GUID。");
            new ESAutomationWorkerRegistration
            {
                type = workerType,
                workerId = workerId,
                version = workerVersion,
                entrypointHash = entrypointHash,
            }.Validate();
            if (status != "Passed" && status != "Failed" && status != "Blocked" && status != "Cancelled" && status != "DryRun")
                throw new InvalidOperationException("RunResult 的状态无效：" + status);
            if (string.IsNullOrWhiteSpace(startedAtUtc) || string.IsNullOrWhiteSpace(finishedAtUtc))
                throw new InvalidOperationException("RunResult 必须记录开始和结束 UTC 时间。");
            if (!DateTimeOffset.TryParse(startedAtUtc, out DateTimeOffset startedAt)
                || !DateTimeOffset.TryParse(finishedAtUtc, out DateTimeOffset finishedAt))
                throw new InvalidOperationException("RunResult 的 UTC 时间格式无效。");
            if (finishedAt < startedAt) throw new InvalidOperationException("RunResult 的结束时间不能早于开始时间。");
            if (retryCount < 0) throw new InvalidOperationException("RunResult 的 retryCount 不能为负数。");
            if (!string.IsNullOrWhiteSpace(idempotencyKey)
                && (idempotencyKey.Length > 160 || !Regex.IsMatch(idempotencyKey, "^[A-Za-z0-9._:-]+$")))
                throw new InvalidOperationException("RunResult 的 idempotencyKey 格式无效。");
            if (!ESAutomationWorkerRegistration.IsSha256(inputManifestHash))
                throw new InvalidOperationException("RunResult 必须记录输入 Manifest SHA-256。");
            if (outputs == null || outputHashes == null || findings == null || errors == null)
                throw new InvalidOperationException("RunResult 的集合字段不得为 null。");
            if (outputs.Count != outputHashes.Count)
                throw new InvalidOperationException("RunResult 的输出路径与输出 Hash 数量不一致。");
            foreach (string outputHash in outputHashes)
            {
                if (!ESAutomationWorkerRegistration.IsSha256(outputHash))
                    throw new InvalidOperationException("RunResult 包含无效输出 SHA-256。");
            }
            foreach (string output in outputs)
            {
                if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("RunResult 包含空输出路径。");
                string normalizedOutput = output.Replace('\\', '/').Trim();
                if (normalizedOutput.Split('/').Any(segment => segment == ".."))
                    throw new InvalidOperationException("RunResult 输出路径不得包含 .. 穿越：" + output);
            }
            if (executionSnapshot != null) executionSnapshot.Validate();
            if (completionDecision != null)
            {
                completionDecision.Validate();
                if (!string.Equals(completionDecision.runId, runId, StringComparison.Ordinal))
                    throw new InvalidOperationException("RunResult 的 CompletionDecision 必须绑定同一 RunId。");
            }
            if (traceReconciliation != null) traceReconciliation.Validate();
        }
    }

    /// <summary>
    /// Python 运行时的受管解析结果。环境变量是本机显式覆盖；项目运行时必须由锁文件和二进制指纹共同识别。
    /// 不回退到 PATH、py launcher 或 Windows Store 别名，避免不同机器静默切换解释器。
    /// </summary>
    public sealed class ESAutomationPythonRuntime
    {
        public string source = string.Empty;
        public string runtimeId = string.Empty;
        public string interpreterPath = string.Empty;
        public string expectedPythonVersion = string.Empty;
        public string expectedInterpreterSha256 = string.Empty;
        public string expectedRuntimeContentSha256 = string.Empty;
        public string detectedPythonVersion = string.Empty;
        public string environmentFingerprint = string.Empty;
    }

    [Serializable]
    internal sealed class ESAutomationPythonRuntimeLock
    {
        // 锁文件反序列化前保持非法值，避免未读取锁文件时被误判为合法协议。
        public int protocolVersion = -1;
        public string runtimeId = string.Empty;
        public string pythonVersion = string.Empty;
        public string interpreterRelativePath = string.Empty;
        public string interpreterSha256 = string.Empty;
        public string runtimeContentSha256 = string.Empty;
        public string requirementsLockRelativePath = string.Empty;
        public string requirementsLockSha256 = string.Empty;
    }

    /// <summary>
    /// 受管 Python 环境解析器。当前 Worker 只使用标准库，但仍锁定解释器；未来第三方依赖必须连同 requirements lock 一起锁定。
    /// </summary>
    public static class ESAutomationPythonEnvironment
    {
        private const string OverrideVariableName = "ES_AUTOMATION_PYTHON";
        private const string LockFileName = "python-runtime.lock.json";
        private const int ProbeTimeoutMilliseconds = 3000;
        private static readonly Regex PythonVersionPattern = new Regex("^[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$", RegexOptions.Compiled);
        private static readonly Regex PythonProbePattern = new Regex("^Python\\s+(3\\.[0-9]+(?:\\.[0-9]+)?)\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public static string ManagedRuntimeRoot => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Environments", "Python");
        public static string ManagedRuntimeLockPath => Path.Combine(ManagedRuntimeRoot, LockFileName);

        /// <summary>只做轻量路径/锁文件解析，适合 Center OnGUI。启动 Worker 前必须再调用 TryValidateForExecution。</summary>
        public static bool TryResolve(out ESAutomationPythonRuntime runtime, out string reason)
        {
            string overridePath = Environment.GetEnvironmentVariable(OverrideVariableName);
            if (!string.IsNullOrWhiteSpace(overridePath)) return TryResolveLocalOverride(overridePath, out runtime, out reason);
            return TryResolveManagedRuntime(out runtime, out reason);
        }

        /// <summary>启动前的强校验：确认解释器文件指纹，并实际探测 Python 3 版本。</summary>
        public static bool TryValidateForExecution(ESAutomationPythonRuntime runtime, out string reason)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.interpreterPath))
            {
                reason = "EnvironmentUnavailable：没有可验证的 Python 运行时。";
                return false;
            }
            if (!File.Exists(runtime.interpreterPath))
            {
                reason = "EnvironmentUnavailable：Python 解释器文件不存在。";
                return false;
            }

            string actualHash;
            try
            {
                actualHash = ComputeFileSha256(runtime.interpreterPath);
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：无法读取 Python 解释器指纹：" + exception.Message;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(runtime.expectedInterpreterSha256)
                && !string.Equals(actualHash, runtime.expectedInterpreterSha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = "EnvironmentUnavailable：项目受管 Python 的 SHA-256 与锁文件不匹配。";
                return false;
            }
            string runtimeFingerprint = actualHash;
            if (!string.IsNullOrWhiteSpace(runtime.expectedRuntimeContentSha256))
            {
                try
                {
                    string runtimeRoot = Path.GetDirectoryName(runtime.interpreterPath);
                    runtimeFingerprint = ComputeRuntimeContentSha256(runtimeRoot);
                }
                catch (Exception exception)
                {
                    reason = "EnvironmentUnavailable：无法读取受管 Python Runtime 指纹：" + exception.Message;
                    return false;
                }
                if (!string.Equals(runtimeFingerprint, runtime.expectedRuntimeContentSha256, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "EnvironmentUnavailable：项目受管 Python Runtime 内容指纹与锁文件不匹配。";
                    return false;
                }
            }
            if (!TryProbePython3(runtime.interpreterPath, out string detectedVersion, out reason)) return false;
            if (!string.IsNullOrWhiteSpace(runtime.expectedPythonVersion)
                && !VersionMatches(detectedVersion, runtime.expectedPythonVersion))
            {
                reason = "EnvironmentUnavailable：Python 版本与锁文件不匹配，期望 " + runtime.expectedPythonVersion + "，实际 " + detectedVersion + "。";
                return false;
            }

            runtime.detectedPythonVersion = detectedVersion;
            runtime.environmentFingerprint = runtimeFingerprint;
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveLocalOverride(string overridePath, out ESAutomationPythonRuntime runtime, out string reason)
        {
            runtime = null;
            if (!Path.IsPathRooted(overridePath))
            {
                reason = "EnvironmentUnavailable：ES_AUTOMATION_PYTHON 必须是 Python 3 python.exe 的绝对路径。";
                return false;
            }
            string fullPath = Path.GetFullPath(overridePath);
            if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                reason = "EnvironmentUnavailable：ES_AUTOMATION_PYTHON 必须指向现有的 python.exe。";
                return false;
            }
            runtime = new ESAutomationPythonRuntime
            {
                source = "local-override",
                runtimeId = OverrideVariableName,
                interpreterPath = fullPath,
            };
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveManagedRuntime(out ESAutomationPythonRuntime runtime, out string reason)
        {
            runtime = null;
            if (!File.Exists(ManagedRuntimeLockPath))
            {
                reason = "EnvironmentUnavailable：未配置 " + OverrideVariableName + "，且项目受管运行时锁文件不存在。请部署 ES/Automation/Environments/Python/python-runtime.lock.json 与其锁定的 Runtime/python.exe。";
                return false;
            }

            ESAutomationPythonRuntimeLock runtimeLock;
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(ManagedRuntimeLockPath, Encoding.UTF8));
                RequireExactProperties(root, new[]
                {
                    "protocolVersion", "runtimeId", "pythonVersion", "interpreterRelativePath", "interpreterSha256",
                    "runtimeContentSha256",
                    "requirementsLockRelativePath", "requirementsLockSha256",
                }, "Python 运行时锁文件");
                runtimeLock = root.ToObject<ESAutomationPythonRuntimeLock>();
                ValidateRuntimeLock(runtimeLock);
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：项目 Python 运行时锁文件无效：" + exception.Message;
                return false;
            }

            try
            {
                string interpreterPath = ResolveManagedRelativePath(runtimeLock.interpreterRelativePath);
                if (!File.Exists(interpreterPath))
                {
                    reason = "EnvironmentUnavailable：锁定的项目 Python 解释器不存在：" + runtimeLock.interpreterRelativePath;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(runtimeLock.requirementsLockRelativePath))
                {
                    string requirementsPath = ResolveManagedRelativePath(runtimeLock.requirementsLockRelativePath);
                    if (!File.Exists(requirementsPath))
                    {
                        reason = "EnvironmentUnavailable：锁定的 Python 依赖文件不存在：" + runtimeLock.requirementsLockRelativePath;
                        return false;
                    }
                    if (!string.Equals(ComputeFileSha256(requirementsPath), runtimeLock.requirementsLockSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "EnvironmentUnavailable：Python 依赖锁文件 SHA-256 不匹配。";
                        return false;
                    }
                }
                runtime = new ESAutomationPythonRuntime
                {
                    source = "project-managed",
                    runtimeId = runtimeLock.runtimeId,
                    interpreterPath = interpreterPath,
                    expectedPythonVersion = runtimeLock.pythonVersion,
                    expectedInterpreterSha256 = runtimeLock.interpreterSha256,
                    expectedRuntimeContentSha256 = runtimeLock.runtimeContentSha256,
                };
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：项目受管 Python 路径无效：" + exception.Message;
                return false;
            }
        }

        private static void ValidateRuntimeLock(ESAutomationPythonRuntimeLock runtimeLock)
        {
            if (runtimeLock == null || runtimeLock.protocolVersion != 1 || string.IsNullOrWhiteSpace(runtimeLock.runtimeId))
                throw new InvalidOperationException("protocolVersion 或 runtimeId 无效。");
            if (!PythonVersionPattern.IsMatch(runtimeLock.pythonVersion ?? string.Empty))
                throw new InvalidOperationException("pythonVersion 必须是 x.y 或 x.y.z。\n");
            if (!IsSafeRelativePath(runtimeLock.interpreterRelativePath) || !runtimeLock.interpreterRelativePath.EndsWith("python.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("interpreterRelativePath 必须是指向 python.exe 的安全相对路径。");
            if (!ESAutomationWorkerRegistration.IsSha256(runtimeLock.interpreterSha256))
                throw new InvalidOperationException("interpreterSha256 必须是 64 位 SHA-256。\n");
            if (!ESAutomationWorkerRegistration.IsSha256(runtimeLock.runtimeContentSha256))
                throw new InvalidOperationException("runtimeContentSha256 必须是 64 位 SHA-256。\n");
            bool hasRequirementsPath = !string.IsNullOrWhiteSpace(runtimeLock.requirementsLockRelativePath);
            bool hasRequirementsHash = !string.IsNullOrWhiteSpace(runtimeLock.requirementsLockSha256);
            if (hasRequirementsPath != hasRequirementsHash)
                throw new InvalidOperationException("requirements 锁文件路径与 SHA-256 必须同时存在或同时为空。\n");
            if (hasRequirementsPath && (!IsSafeRelativePath(runtimeLock.requirementsLockRelativePath) || !ESAutomationWorkerRegistration.IsSha256(runtimeLock.requirementsLockSha256)))
                throw new InvalidOperationException("requirements 锁文件配置无效。\n");
        }

        private static string ResolveManagedRelativePath(string relativePath)
        {
            if (!IsSafeRelativePath(relativePath)) throw new InvalidOperationException("路径必须是安全相对路径。");
            string fullPath = Path.GetFullPath(Path.Combine(ManagedRuntimeRoot, relativePath));
            if (!ESAutomationPathPolicy.IsWithin(fullPath, new[] { ManagedRuntimeRoot }))
                throw new UnauthorizedAccessException("路径越出受管 Python 根目录。");
            return fullPath;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..") return false;
            }
            return true;
        }

        private static bool TryProbePython3(string interpreterPath, out string version, out string reason)
        {
            version = string.Empty;
            try
            {
                using (ESManagedEditorProcess process = ESManagedEditorProcessRunner.StartPythonProbe(
                    interpreterPath, ESAutomationPathPolicy.ProjectRoot, 3))
                {
                    if (!process.WaitForExit(ProbeTimeoutMilliseconds))
                    {
                        process.Terminate();
                        reason = "EnvironmentUnavailable：Python --version 探测超时。";
                        return false;
                    }
                    string standardOutput = process.ReadStandardOutputToEnd();
                    string standardError = process.ReadStandardErrorToEnd();
                    string output = (standardOutput + "\n" + standardError).Trim();
                    Match match = PythonProbePattern.Match(output);
                    if (!process.TryGetExitCode(out int exitCode) || exitCode != 0 || !match.Success)
                    {
                        reason = "EnvironmentUnavailable：解释器未返回 Python 3 版本信息。";
                        return false;
                    }
                    version = match.Groups[1].Value;
                    reason = string.Empty;
                    return true;
                }
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：无法执行 Python --version：" + exception.Message;
                return false;
            }
        }

        private static bool VersionMatches(string detected, string expected)
        {
            string[] detectedParts = (detected ?? string.Empty).Split('.');
            string[] expectedParts = (expected ?? string.Empty).Split('.');
            if (expectedParts.Length < 2 || detectedParts.Length < expectedParts.Length) return false;
            for (int index = 0; index < expectedParts.Length; index++)
            {
                if (!string.Equals(detectedParts[index], expectedParts[index], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static string ComputeFileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string ComputeRuntimeContentSha256(string runtimeRoot)
        {
            if (string.IsNullOrWhiteSpace(runtimeRoot) || !Directory.Exists(runtimeRoot))
                throw new DirectoryNotFoundException("受管 Python Runtime 根目录不存在。");
            string normalizedRoot = Path.GetFullPath(runtimeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            string[] files = new List<string>(ESManagedFileIO.EnumerateFilesSafely(normalizedRoot, "*")).ToArray();
            Array.Sort(files, StringComparer.Ordinal);
            var manifest = new StringBuilder(files.Length * 96);
            foreach (string file in files)
            {
                if (!ESAutomationPathPolicy.IsWithin(file, new[] { normalizedRoot }))
                    throw new UnauthorizedAccessException("受管 Python Runtime 包含越界文件。");
                if (!file.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("受管 Python Runtime 包含非根内文件。");
                string relativePath = file.Substring(rootPrefix.Length).Replace('\\', '/');
                manifest.Append(relativePath).Append('\n').Append(ComputeFileSha256(file)).Append('\n');
            }
            byte[] bytes = new UTF8Encoding(false).GetBytes(manifest.ToString());
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static void RequireExactProperties(JObject value, IEnumerable<string> fields, string context)
        {
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in value.Properties()) actual.Add(property.Name);
            if (actual.SetEquals(expected)) return;
            throw new InvalidOperationException(context + " 字段必须与受管协议完全一致。\n");
        }
    }

    public static class ESAutomationTaskRegistry
    {
        private static readonly Dictionary<string, ESAutomationTaskContract> tasks = new Dictionary<string, ESAutomationTaskContract>(StringComparer.Ordinal);

        public static IReadOnlyCollection<ESAutomationTaskContract> Tasks => tasks.Values;

        public static void Register(ESAutomationTaskContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            contract.Validate();
            string key = contract.taskId + "@" + contract.version;
            if (tasks.ContainsKey(key)) throw new InvalidOperationException("重复注册 Automation 任务：" + key);
            tasks.Add(key, contract);
        }

        public static bool TryGet(string taskId, int version, out ESAutomationTaskContract contract)
            => tasks.TryGetValue(taskId + "@" + version, out contract);
    }

    internal static class ESAutomationSourceState
    {
        private static readonly Regex CommitPattern = new Regex("^[a-fA-F0-9]{40}$", RegexOptions.Compiled);

        public static string GetCurrentGitCommit()
        {
            string projectRoot = ESAutomationPathPolicy.ProjectRoot;
            string gitPath = Path.Combine(projectRoot, ".git");
            if (File.Exists(gitPath))
            {
                string gitDirectory = File.ReadAllText(gitPath, new UTF8Encoding(false, true)).Trim();
                const string prefix = "gitdir:";
                if (gitDirectory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    gitDirectory = gitDirectory.Substring(prefix.Length).Trim();
                if (!Path.IsPathRooted(gitDirectory))
                    gitDirectory = Path.GetFullPath(Path.Combine(projectRoot, gitDirectory));
                gitPath = gitDirectory;
            }

            string headPath = Path.Combine(gitPath, "HEAD");
            if (!File.Exists(headPath)) return string.Empty;
            string head = File.ReadAllText(headPath, new UTF8Encoding(false, true)).Trim();
            if (CommitPattern.IsMatch(head)) return head.ToLowerInvariant();
            const string refPrefix = "ref:";
            if (!head.StartsWith(refPrefix, StringComparison.Ordinal)) return string.Empty;
            string referencePath = Path.Combine(gitPath, head.Substring(refPrefix.Length).Trim().Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(referencePath))
            {
                string commit = File.ReadAllText(referencePath, new UTF8Encoding(false, true)).Trim();
                return CommitPattern.IsMatch(commit) ? commit.ToLowerInvariant() : string.Empty;
            }

            string packedRefsPath = Path.Combine(gitPath, "packed-refs");
            if (!File.Exists(packedRefsPath)) return string.Empty;
            foreach (string line in File.ReadAllLines(packedRefsPath, new UTF8Encoding(false, true)))
            {
                string[] parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && string.Equals(parts[1], head.Substring(refPrefix.Length).Trim(), StringComparison.Ordinal)
                    && CommitPattern.IsMatch(parts[0]))
                    return parts[0].ToLowerInvariant();
            }
            return string.Empty;
        }
    }

    public sealed class ESAutomationProcessRequest
    {
        public string taskId = string.Empty;
        public int taskVersion;
        public string runId = string.Empty;
        public bool dryRun = true;
        public string inputContractPath = string.Empty;
    }

    /// <summary>
    /// 一次受管 Worker 进程的唯一所有者。
    ///
    /// 任务 Endpoint 只负责自身的阶段协议与结果文件；解释器启动、进程句柄、超时判断和终止统一由
    /// 执行器负责。它不把 stdout/stderr 当作业务协议，正式结果必须仍由受签名的结构化文件返回。
    /// </summary>
    internal static class ESAutomationRunReservation
    {
        private static readonly object sync = new object();
        private static readonly HashSet<string> activeRunIds = new HashSet<string>(StringComparer.Ordinal);

        public static bool Reserve(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return false;
            lock (sync) return activeRunIds.Add(runId);
        }

        public static void Release(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return;
            lock (sync) activeRunIds.Remove(runId);
        }
    }

    public sealed class ESAutomationProcessExecution : IESManagedProcessHandle
    {
        private readonly string runId;
        private readonly object lifecycleSync = new object();
        private Process process;
        private readonly DateTimeOffset startedAtUtc;
        private readonly int timeoutSeconds;
        private bool terminationRequested;
        private readonly IDisposable processTreeScope;
        private readonly Task<string> standardOutputTask;
        private readonly Task<string> standardErrorTask;

        internal ESAutomationProcessExecution(Process process, int timeoutSeconds, string runId)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
            this.runId = runId ?? string.Empty;
            if (timeoutSeconds < 1 || timeoutSeconds > ESAutomationTaskContract.MaximumTimeoutSeconds)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Worker 任务超时必须位于 1–7200 秒范围内。");
            this.timeoutSeconds = timeoutSeconds;
            startedAtUtc = DateTimeOffset.UtcNow;
            processTreeScope = ESManagedProcessTree.Attach(process);
            if (process.StartInfo.RedirectStandardOutput)
                standardOutputTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardOutput);
            if (process.StartInfo.RedirectStandardError)
                standardErrorTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardError);
            ESManagedProcessRegistry.Register(this);
        }

        public DateTimeOffset StartedAtUtc => startedAtUtc;
        public int TimeoutSeconds => timeoutSeconds;
        public bool TerminationRequested => terminationRequested;
        public bool HasJobObject => processTreeScope != null;

        /// <summary>
        /// 受管 Worker 的宿主进程 ID，仅用于 RunRecord 观测；进程生命周期仍由本对象统一拥有。
        /// </summary>
        public int ProcessId
        {
            get
            {
                lock (lifecycleSync)
                    return process == null ? 0 : process.Id;
            }
        }

        public bool HasExited
        {
            get
            {
                lock (lifecycleSync)
                {
                    if (process == null) return true;
                    return process.HasExited;
                }
            }
        }

        public bool HasTimedOut(DateTimeOffset nowUtc)
            => !HasExited && nowUtc - startedAtUtc > TimeSpan.FromSeconds(timeoutSeconds);

        public bool TryGetExitCode(out int exitCode)
        {
            exitCode = -1;
            Process current;
            lock (lifecycleSync) current = process;
            if (current == null || !current.HasExited) return false;
            exitCode = current.ExitCode;
            return true;
        }

        public bool WaitForExit(int milliseconds)
        {
            Process current;
            lock (lifecycleSync) current = process;
            return current != null && current.WaitForExit(milliseconds);
        }

        public string ReadStandardOutputToEnd()
            => ESManagedProcessOutput.GetResult(standardOutputTask, "stdout");

        public string ReadStandardErrorToEnd()
            => ESManagedProcessOutput.GetResult(standardErrorTask, "stderr");

        public bool EnforceTimeout(DateTimeOffset nowUtc)
        {
            if (!HasTimedOut(nowUtc)) return false;
            Terminate();
            return true;
        }

        /// <summary>
        /// 终止受管 Worker 及其可能创建的子进程。
        /// Windows 使用固定系统 taskkill 的 /T /F 进程树边界；其它平台回退到当前进程终止。
        /// 终止失败仍会抛出，让上层把结果标记为“终止未确认”，不能伪装成已取消。
        /// </summary>
        public void Terminate()
        {
            lock (lifecycleSync)
            {
                if (process == null || process.HasExited) return;
                terminationRequested = true;
                ESManagedProcessTree.Terminate(process, processTreeScope);
            }
        }

        public void Dispose()
        {
            lock (lifecycleSync)
            {
                if (process == null) return;
                if (!process.HasExited) Terminate();
                ESManagedProcessRegistry.Unregister(this);
                ESAutomationRunReservation.Release(runId);
                processTreeScope?.Dispose();
                process.Dispose();
                process = null;
            }
        }
    }

    /// <summary>
    /// 只有 C# Editor 代码可注册的受信 Worker 适配器。
    /// 调用方不能提交 executable 或任意命令行；适配器必须自行解析固定入口与环境。
    /// </summary>
    public interface IESAutomationWorkerAdapter
    {
        string WorkerType { get; }
        string WorkerId { get; }
        ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract, ESAutomationProcessRequest request);
    }

    public static class ESAutomationProcessRunner
    {
        private static readonly Dictionary<string, IESAutomationWorkerAdapter> adapters = new Dictionary<string, IESAutomationWorkerAdapter>(StringComparer.Ordinal);

        public static void RegisterAdapter(IESAutomationWorkerAdapter adapter)
        {
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.WorkerType) || string.IsNullOrWhiteSpace(adapter.WorkerId))
                throw new ArgumentException("受信 WorkerAdapter 必须具备类型和稳定 ID。", nameof(adapter));
            string key = AdapterKey(adapter.WorkerType, adapter.WorkerId);
            if (adapters.ContainsKey(key)) throw new InvalidOperationException("重复注册 WorkerAdapter：" + key);
            adapters.Add(key, adapter);
        }

        public static bool IsAdapterRegistered(string workerType, string workerId)
            => adapters.ContainsKey(AdapterKey(workerType, workerId));

        public static ProcessStartInfo CreateStartInfo(ESAutomationProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!ESAutomationTaskRegistry.TryGet(request.taskId, request.taskVersion, out ESAutomationTaskContract contract))
                throw new InvalidOperationException("未注册的 Automation 任务：" + request.taskId + "@" + request.taskVersion);
            if (contract.worker == null || !contract.worker.enabled)
                throw new InvalidOperationException("Worker 未被 C# Editor 显式启用：" + request.taskId);
            if (string.IsNullOrWhiteSpace(request.runId) || string.IsNullOrWhiteSpace(request.inputContractPath))
                throw new InvalidOperationException("受管运行请求必须具备 RunId 和结构化输入路径。");
            if (!Guid.TryParseExact(request.runId, "N", out _))
                throw new InvalidOperationException("受管运行请求的 RunId 必须是 N 格式 GUID。");
            ESAutomationPathPolicy.EnsureWorkerReadAllowed(request.inputContractPath, contract.readRoots);
            if (!IsAdapterRegistered(contract.worker.type, contract.worker.workerId))
                throw new NotSupportedException("没有已注册的受信 WorkerAdapter：" + AdapterKey(contract.worker.type, contract.worker.workerId));

            return adapters[AdapterKey(contract.worker.type, contract.worker.workerId)].CreateStartInfo(contract, request);
        }

        /// <summary>
        /// 只执行已注册 TaskContract 对应的受信 Adapter。调用方不能提供解释器、脚本或命令行；
        /// Adapter 生成的启动信息还会在此处接受 shell、文件和工作目录的最终约束检查。
        /// </summary>
        public static ESAutomationProcessExecution Start(ESAutomationProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!ESAutomationTaskRegistry.TryGet(request.taskId, request.taskVersion, out ESAutomationTaskContract contract))
                throw new InvalidOperationException("受管 Worker 启动前找不到对应 TaskContract。");
            if (!Guid.TryParseExact(request.runId, "N", out _))
                throw new InvalidOperationException("受管 Worker 启动的 RunId 必须是 N 格式 GUID。");
            if (!ESAutomationRunReservation.Reserve(request.runId))
                throw new InvalidOperationException("同一 RunId 已有受管 Worker 运行，拒绝并发重复启动：" + request.runId);

            Process process = null;
            bool started = false;
            try
            {
                ProcessStartInfo startInfo = CreateStartInfo(request);
                ValidateTrustedStartInfo(startInfo);
                process = new Process { StartInfo = startInfo };
                started = process.Start();
                if (!started) throw new InvalidOperationException("受管 Worker 未能启动。");
                return new ESAutomationProcessExecution(process, contract.timeoutSeconds, request.runId);
            }
            catch
            {
                Exception cleanupFailure = null;
                try
                {
                    if (process != null && started && !process.HasExited) ESManagedProcessTree.Terminate(process, null);
                }
                catch (Exception exception) { cleanupFailure = exception; }
                ESAutomationRunReservation.Release(request.runId);
                process?.Dispose();
                if (cleanupFailure != null)
                    throw new AggregateException("受管 Worker 启动后初始化失败，且进程树终止未确认。", cleanupFailure);
                throw;
            }
        }

        private static void ValidateTrustedStartInfo(ProcessStartInfo startInfo)
        {
            if (startInfo == null) throw new InvalidOperationException("受信 WorkerAdapter 未返回启动信息。");
            if (startInfo.UseShellExecute) throw new InvalidOperationException("受管 Worker 禁止通过 Shell 执行。");
            if (string.IsNullOrWhiteSpace(startInfo.FileName)) throw new InvalidOperationException("受管 Worker 缺少可执行入口。");
            if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
                throw new InvalidOperationException("受管 Worker 必须同时重定向 stdout/stderr，由框架统一异步排空并限额采集。");

            string executablePath = Path.GetFullPath(startInfo.FileName);
            if (!File.Exists(executablePath)) throw new FileNotFoundException("受管 Worker 可执行入口不存在。", executablePath);
            if (ESManagedFileIO.ContainsExistingReparsePoint(executablePath))
                throw new UnauthorizedAccessException("受管 Worker 可执行入口不能穿过 junction 或 symlink。");
            startInfo.FileName = executablePath;
            if (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
                throw new InvalidOperationException("受管 Worker 必须显式声明项目内工作目录。");
            string workingDirectory = Path.GetFullPath(startInfo.WorkingDirectory);
            if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException("受管 Worker 工作目录不存在：" + workingDirectory);
            if (!ESAutomationPathPolicy.IsWithin(workingDirectory, new[] { ESAutomationPathPolicy.ProjectRoot }))
                throw new UnauthorizedAccessException("受管 Worker 工作目录必须位于项目根目录内。");
            if (ESManagedFileIO.ContainsExistingReparsePoint(workingDirectory))
                throw new UnauthorizedAccessException("受管 Worker 工作目录不能穿过 junction 或 symlink。");
            startInfo.WorkingDirectory = workingDirectory;
        }

        public static void RejectUnregisteredExecution()
            => throw new InvalidOperationException("管理级阶段不执行未注册 Worker；先完成受信 WorkerAdapter、入口指纹、环境版本和权限审查。");

        private static string AdapterKey(string workerType, string workerId) => workerType + "@" + workerId;
    }

    internal sealed class ESAutomationManagedProcessReloadGuard : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= TerminateAllBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += TerminateAllBeforeReload;
            EditorApplication.quitting -= TerminateAllBeforeReload;
            EditorApplication.quitting += TerminateAllBeforeReload;
        }

        private static void TerminateAllBeforeReload()
        {
            try
            {
                ESManagedProcessRegistry.TerminateAll();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESAutomation] ReloadDomain 前无法确认所有受管进程已终止：" + exception);
            }
        }
    }

    public static class ESAutomationPathPolicy
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string ReportsRoot => Path.Combine(ProjectRoot, "ES", "Automation", "Reports");
        public static string TempRoot => Path.Combine(ProjectRoot, "ES", "Automation", "Temp");
        public static string RunsRoot => Path.Combine(ProjectRoot, "ES", "Automation", "Runs");

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径不能为空。", nameof(path));
            if (ContainsParentTraversal(path)) throw new ArgumentException("路径不得包含 .. 段。", nameof(path));
            string candidate = Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path);
            string normalized = Path.GetFullPath(candidate);
            EnsureNoExistingReparsePoint(normalized);
            string root = Path.GetPathRoot(normalized);
            return string.Equals(root, normalized, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool IsWithin(string path, IEnumerable<string> roots)
        {
            string candidate = Normalize(path);
            foreach (string root in roots ?? Enumerable.Empty<string>())
            {
                string normalizedRoot = Normalize(root);
                if (string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)) return true;
                string rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? normalizedRoot
                    : normalizedRoot + Path.DirectorySeparatorChar;
                if (candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void EnsureWorkerWriteAllowed(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, new[] { ProjectRoot })) throw new UnauthorizedAccessException("Worker 写入路径必须位于项目根目录内。");
            if (IsWithin(normalized, ProtectedWriteRoots)) throw new InvalidOperationException("Worker 禁止写入受保护目录：" + normalized);
            if (!IsWithin(normalized, writeRoots)) throw new UnauthorizedAccessException("路径不在任务 WriteRoots 内：" + normalized);
        }

        /// <summary>
        /// Dedicated in-process UI materializer boundary. Generic Workers still cannot
        /// write Assets; only the fixed generated UI roots and evidence root are allowed.
        /// </summary>
        public static void EnsureUIWorkerWriteAllowed(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            string generatedPrefabRoot = Path.Combine(ProjectRoot, "Assets", "UI", "Prefabs", "Generated");
            string generatedSceneRoot = Path.Combine(ProjectRoot, "Assets", "UI", "Scenes", "Generated");
            string evidenceRoot = Path.Combine(ProjectRoot, "ES", "UIEvidence");
            if (!IsWithin(normalized, new[] { generatedPrefabRoot, generatedSceneRoot, evidenceRoot }))
                throw new UnauthorizedAccessException("MaterializeUI 只能写入 Generated UI 或 ES/UIEvidence：" + normalized);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("MaterializeUI 路径不在任务 WriteRoots 内：" + normalized);
        }

        public static void EnsureWorkerReadAllowed(string path, IEnumerable<string> readRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, new[] { ProjectRoot })) throw new UnauthorizedAccessException("Worker 读取路径必须位于项目根目录内。");
            if (!IsWithin(normalized, readRoots)) throw new UnauthorizedAccessException("路径不在任务 ReadRoots 内：" + normalized);
        }

        public static void EnsureWorkerDirectory(string path, IEnumerable<string> writeRoots)
        {
            EnsureWorkerWriteAllowed(path, writeRoots);
            Directory.CreateDirectory(Normalize(path));
        }

        public static void DeleteWorkerFile(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("删除路径不在 Worker WriteRoots 内：" + normalized);
            if (File.Exists(normalized)) File.Delete(normalized);
        }

        public static void DeleteWorkerDirectory(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("删除目录不在 Worker WriteRoots 内：" + normalized);
            if (!Directory.Exists(normalized)) return;
            string managedRoot = IsWithin(normalized, new[] { ReportsRoot }) ? ReportsRoot : TempRoot;
            ESManagedFileIO.DeleteDirectory(normalized, managedRoot);
        }

        public static void CopyWorkerFileAtomic(string sourcePath, string destinationPath,
            IEnumerable<string> readRoots, IEnumerable<string> writeRoots)
        {
            string source = Normalize(sourcePath);
            string destination = Normalize(destinationPath);
            if (!IsWithin(source, readRoots)) throw new UnauthorizedAccessException("复制源不在 Worker ReadRoots 内：" + source);
            if (!IsWithin(destination, writeRoots)) throw new UnauthorizedAccessException("复制目标不在 Worker WriteRoots 内：" + destination);
            EnsureWorkerDirectory(Path.GetDirectoryName(destination), writeRoots);
            var allowedRoots = (readRoots ?? Enumerable.Empty<string>()).Concat(writeRoots ?? Enumerable.Empty<string>()).ToArray();
            ESManagedFileIO.CopyFileAtomic(source, destination, allowedRoots);
        }

        public static void WriteWorkerTextAtomic(string path, string text, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("写入路径不在 Worker WriteRoots 内：" + normalized);
            EnsureWorkerDirectory(Path.GetDirectoryName(normalized), writeRoots);
            ESManagedFileIO.WriteTextAtomic(normalized, text ?? string.Empty, new UTF8Encoding(false), (writeRoots ?? Enumerable.Empty<string>()).ToArray());
        }

        public static void EnsureDeclaredReadRoot(string root)
        {
            string normalized = Normalize(root);
            if (!IsWithin(normalized, new[] { ProjectRoot }))
                throw new UnauthorizedAccessException("ReadRoots 必须位于项目根目录内：" + normalized);
        }

        public static void EnsureDeclaredWriteRoot(string root)
        {
            string normalized = Normalize(root);
            if (!IsWithin(normalized, new[] { ReportsRoot, TempRoot, RunsRoot,
                    Path.Combine(ProjectRoot, "Assets", "UI"),
                    Path.Combine(ProjectRoot, "ES", "UIEvidence"),
                    Path.Combine(ProjectRoot, "ES", "Output", "TaskContextRuntime") }))
                throw new UnauthorizedAccessException("WriteRoots 必须位于 ES/Automation/Reports、Temp、Runs、Assets/UI、ES/UIEvidence 或平台 TaskContextRuntime StoreRoot：" + normalized);
        }

        private static IEnumerable<string> ProtectedWriteRoots
        {
            get
            {
                yield return Application.dataPath;
                yield return Path.Combine(ProjectRoot, "Packages");
                yield return Path.Combine(ProjectRoot, "ProjectSettings");
                yield return Path.Combine(ProjectRoot, "Library");
                yield return Path.Combine(ProjectRoot, "Temp");
                yield return Path.Combine(ProjectRoot, ".git");
            }
        }

        private static bool ContainsParentTraversal(string path)
        {
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment == "..") return true;
            }
            return false;
        }

        private static void EnsureNoNestedReparsePoint(string directory)
            => ESManagedFileIO.EnsureNoNestedReparsePoints(directory);

        private static void EnsureNoExistingReparsePoint(string path)
        {
            string root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return;
            string current = root;
            string relative = path.Substring(root.Length);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new UnauthorizedAccessException("自动化路径不得穿过 junction 或 symlink：" + current);
            }
        }
    }

    public static class ESAutomationReportCenter
    {
        /// <summary>
        /// 受控读取并验证报告。恢复流程不得直接信任磁盘上的 JSON；路径、UTF-8、RunId
        /// 目录绑定和协议字段必须全部通过后才返回结果。
        /// </summary>
        public static bool TryReadJson(string path, out ESAutomationRunResult result, out string reason)
        {
            result = null;
            reason = string.Empty;
            try
            {
                string normalized = ESAutomationPathPolicy.Normalize(path);
                ESAutomationPathPolicy.EnsureWorkerReadAllowed(normalized,
                    new[] { ESAutomationPathPolicy.ReportsRoot });
                if (!File.Exists(normalized))
                {
                    reason = "Automation 报告不存在。";
                    return false;
                }

                string runDirectory = Path.GetFileName(Path.GetDirectoryName(normalized));
                if (!Guid.TryParseExact(runDirectory, "N", out _))
                {
                    reason = "Automation 报告目录不是有效 RunId。";
                    return false;
                }

                string json = File.ReadAllText(normalized, new UTF8Encoding(false, true));
                ESAutomationRunResult parsed = JsonConvert.DeserializeObject<ESAutomationRunResult>(json);
                if (parsed == null)
                {
                    reason = "Automation 报告 JSON 为空。";
                    return false;
                }
                parsed.Validate();
                if (!string.Equals(parsed.runId, runDirectory, StringComparison.Ordinal))
                {
                    reason = "Automation 报告 RunId 与目录不一致。";
                    return false;
                }
                result = parsed;
                return true;
            }
            catch (Exception exception)
            {
                reason = "Automation 报告读取或协议校验失败：" + exception.Message;
                return false;
            }
        }

        public static string WriteJson(ESAutomationRunResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Validate();
            if (result.executionSnapshot != null) result.executionSnapshot.Validate();
            if (result.completionDecision != null) result.completionDecision.Validate();

            string directory = Path.Combine(ESAutomationPathPolicy.ReportsRoot, result.runId);
            string temporaryDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, result.runId);
            if (Directory.Exists(temporaryDirectory)) throw new IOException("Automation 临时 RunId 目录已存在，拒绝复用：" + result.runId);
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(directory, new[] { ESAutomationPathPolicy.ReportsRoot });
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
            if (Directory.Exists(directory)) throw new IOException("Automation 报告 RunId 已存在：" + result.runId);

            try
            {
                ESAutomationPathPolicy.EnsureWorkerDirectory(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
                string temporaryPath = Path.Combine(temporaryDirectory, "result.json");
                ESAutomationPathPolicy.WriteWorkerTextAtomic(temporaryPath,
                    JsonConvert.SerializeObject(result, Formatting.Indented), new[] { ESAutomationPathPolicy.TempRoot });
                string stagedHash = ComputeSha256(temporaryPath);
                ESAutomationPathPolicy.EnsureWorkerDirectory(ESAutomationPathPolicy.ReportsRoot, new[] { ESAutomationPathPolicy.ProjectRoot });
                Directory.Move(temporaryDirectory, directory);
                string finalPath = Path.Combine(directory, "result.json");
                if (!File.Exists(finalPath) || !string.Equals(stagedHash, ComputeSha256(finalPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Automation 报告移动后哈希校验失败：" + finalPath);
                return finalPath;
            }
            catch
            {
                ESAutomationPathPolicy.DeleteWorkerDirectory(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
                throw;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }

    public static class ESAutomationReleaseGate
    {
        public static bool IsPublishAllowed(ESAutomationRunResult result, out string reason)
        {
            if (result == null) { reason = "缺少运行结果。"; return false; }
            try
            {
                result.Validate();
            }
            catch (Exception exception)
            {
                reason = "Automation 结果协议无效：" + exception.Message;
                return false;
            }
            if (!ESAutomationTaskRegistry.TryGet(result.taskId, result.taskVersion, out ESAutomationTaskContract contract))
            {
                reason = "Automation 结果不属于当前注册任务。";
                return false;
            }
            if (contract.worker == null || !contract.worker.enabled)
            {
                reason = "Automation 结果对应的 Worker 未被 C# Editor 显式启用。";
                return false;
            }
            if (contract.worker.type != result.workerType || contract.worker.workerId != result.workerId || contract.worker.version != result.workerVersion || !string.Equals(contract.worker.entrypointHash, result.entrypointHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Automation 结果的 Worker 身份与受信注册不一致。";
                return false;
            }
            if (contract.outputs != null && contract.outputs.Count > 0)
            {
                foreach (string output in result.outputs ?? new List<string>())
                {
                    if (!IsDeclaredOutput(output, contract.outputs))
                    {
                        reason = "Automation 结果包含 TaskContract 未声明的输出：" + output;
                        return false;
                    }
                }
            }
            if ((contract.performanceBudget != null || contract.acceptanceCriteria != null)
                && !TryValidateOutputHashes(result, out string outputHashReason))
            {
                reason = "Automation 输出完整性校验失败：" + outputHashReason;
                return false;
            }
            if (!string.Equals(result.status, "Passed", StringComparison.OrdinalIgnoreCase)) { reason = "Automation 任务未通过：" + result.status; return false; }
            if (result.exitCode != 0) { reason = "Automation 退出码非 0。"; return false; }
            if (result.errors != null && result.errors.Count > 0) { reason = "Automation 报告包含错误。"; return false; }
            if (contract.performanceBudget != null
                && !contract.performanceBudget.TryValidateRunResult(result, out string budgetReason))
            {
                reason = "Automation PerformanceBudget 拒绝结果：" + budgetReason;
                return false;
            }
            if (contract.acceptanceCriteria != null
                && (result.completionDecision == null
                    || !result.completionDecision.CanAccept(contract.acceptanceCriteria)))
            {
                reason = "Automation 缺少有效 CompletionDecision。";
                return false;
            }
            ESAutomationFreshnessPolicy freshness = contract.acceptanceCriteria?.freshnessPolicy;
            if (freshness?.requireExecutionSnapshotBinding == true && result.executionSnapshot == null)
            {
                reason = "严格验收要求 RunResult 携带 ExecutionSnapshot。";
                return false;
            }
            if (freshness?.requireSourceHash == true
                && result.executionSnapshot != null
                && !EvidenceBindsToSnapshot(result.completionDecision, result.executionSnapshot,
                    freshness.requireExecutionSnapshotBinding))
            {
                reason = "Automation 验收证据未绑定当前 ExecutionSnapshot 身份与源哈希。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        internal static bool EvidenceBindsToSnapshot(ESAutomationCompletionDecision decision,
            ESAutomationExecutionSnapshot snapshot, bool requireFullBinding = false)
        {
            if (decision == null || snapshot == null || decision.criterionResults == null
                || decision.criterionResults.Count == 0)
                return false;
            foreach (ESAutomationCriterionResult criterion in decision.criterionResults)
            {
                if (criterion == null) return false;
                try
                {
                    criterion.Validate();
                }
                catch
                {
                    return false;
                }
                ESAutomationClaimEvidenceBinding binding = criterion?.evidenceBinding;
                if (binding == null
                    || !string.Equals(binding.sourceHash, snapshot.sourceHash, StringComparison.OrdinalIgnoreCase)
                    || (requireFullBinding && !string.Equals(binding.snapshotId, snapshot.snapshotId, StringComparison.Ordinal)))
                    return false;
                if (requireFullBinding
                    && (string.IsNullOrWhiteSpace(binding.inputManifestHash)
                        || string.IsNullOrWhiteSpace(binding.taskContractHash)
                        || string.IsNullOrWhiteSpace(binding.commandHash)
                        || string.IsNullOrWhiteSpace(binding.brainPlanHash)))
                    return false;
                if (!string.IsNullOrWhiteSpace(binding.inputManifestHash)
                    && !string.Equals(binding.inputManifestHash, snapshot.inputManifestHash, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrWhiteSpace(binding.taskContractHash)
                    && !string.Equals(binding.taskContractHash, snapshot.taskContractHash, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrWhiteSpace(binding.commandHash)
                    && !string.Equals(binding.commandHash, snapshot.commandHash, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrWhiteSpace(binding.brainPlanHash)
                    && !string.Equals(binding.brainPlanHash, snapshot.brainPlanHash, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private static bool TryValidateOutputHashes(ESAutomationRunResult result, out string reason)
        {
            reason = string.Empty;
            if (result.outputs.Count != result.outputHashes.Count)
            {
                reason = "输出路径与输出哈希数量不一致。";
                return false;
            }
            for (int index = 0; index < result.outputs.Count; index++)
            {
                string path = ESAutomationPerformanceBudget.ResolveGovernedOutputPath(
                    result.runId, result.outputs[index]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    reason = "输出文件不在受管 Reports/Temp Run 目录内：" + result.outputs[index];
                    return false;
                }
                string actual;
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha = SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (!string.Equals(actual, result.outputHashes[index], StringComparison.OrdinalIgnoreCase))
                {
                    reason = "输出文件哈希不一致：" + result.outputs[index];
                    return false;
                }
            }
            return true;
        }

        internal static bool IsDeclaredOutput(string output, IEnumerable<string> declarations)
        {
            if (string.IsNullOrWhiteSpace(output)) return false;
            string normalized = output.Replace('\\', '/').Trim();
            string fileName = Path.GetFileName(normalized);
            foreach (string declaration in declarations ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(declaration)) continue;
                string declared = declaration.Replace('\\', '/').Trim();
                bool declarationHasDirectory = declared.IndexOf('/') >= 0;
                if (string.Equals(normalized, declared, StringComparison.OrdinalIgnoreCase)
                    || (!declarationHasDirectory
                        && string.Equals(fileName, Path.GetFileName(declared), StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }
    }

    public sealed class ESAutomationCenterWindow : ESSinglePageIMGUIWindow<ESAutomationCenterWindow>
    {
        private Vector2 scrollPosition;

        [MenuItem(MenuItemPathDefine.AUTOMATION_CENTER_PATH + "打开自动化中心")]
        private static void Open() => OpenWindow();

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 自动化中心", "管理受信自动化任务、Worker、运行记录与 AI 调用授权");
        }
        public override string ESWindow_PresentationShortTitle => "自动化";

        protected override string ESWindow_Subtitle => "受信任务、Worker 与 AI 调用门禁";
        protected override Vector2 ESWindow_MinSize => new Vector2(640f, 520f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(880f, 720f);
        protected override string ESWindow_PageStableId => "automation.center";
        protected override string ESWindow_PageTitle => "自动化中心";
        protected override string ESWindow_PageKeywords => "Automation Worker Task Run AI Bridge 自动化 任务";

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            maxSize = new Vector2(1400f, 1000f);
        }

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "automation.validate-python",
                    "验证 Python",
                    "验证受管 Python 3 环境与运行时指纹。",
                    context =>
                    {
                        if (ESAutomationSceneScanPythonAdapter.TryPrepareRuntime(
                                out ESAutomationPythonRuntime runtime,
                                out string reason))
                        {
                            EditorUtility.DisplayDialog(
                                "Python 环境可用",
                                "来源：" + runtime.source
                                + "\n运行时：" + runtime.runtimeId
                                + "\n版本：" + runtime.detectedPythonVersion
                                + "\n指纹：" + runtime.environmentFingerprint,
                                "关闭");
                            context.SetStatus("Python 环境验证通过");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Python 环境不可用", reason, "关闭");
                            context.SetStatus("Python 环境验证失败", ESMenuTreePageStatus.Warning);
                        }
                    })
                .WithUnityIcon("TestPassed")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "automation.copy-ai-example",
                    "复制 AI 样例",
                    "复制受管 AI Inbox 请求样例。",
                    context =>
                    {
                        CopyAiRequestExample();
                        context.Notify("AI 调用样例已复制");
                    })
                .WithUnityIcon("Clipboard")
                .WithPriority(30));
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
            EditorGUILayout.LabelField("ES 自动化中心", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("C# Editor 是任务权限、路径策略、运行记录和发布门禁的权威入口。仅已注册、入口指纹固定且有受信 Adapter 的 Worker 可执行。", MessageType.Info);
            EditorGUILayout.LabelField("已注册任务", ESAutomationTaskRegistry.Tasks.Count.ToString());
            EditorGUILayout.LabelField("报告目录", ESAutomationPathPolicy.ReportsRoot);
            EditorGUILayout.LabelField("Worker 写 Assets", "禁止");
            EditorGUILayout.LabelField("发布门禁", "失败或缺少结构化报告时阻止");
            EditorGUILayout.LabelField("受管进程", ESManagedProcessRegistry.ActiveCount + " 个（ReloadDomain 前统一终止）");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("已注册原型", EditorStyles.boldLabel);
            bool hasConfiguredPython = ESAutomationSceneScanPythonAdapter.TryGetConfiguredInterpreter(out _, out string pythonReason);
            EditorGUILayout.HelpBox(
                "执行器：Python 3 · es.scene.scan.python@0.1.0\n"
                + "入口：ES/Automation/Workers/Python/es_scene_scan_worker.py\n"
                + "场景扫描只导出当前 Active Scene 的规范化快照。Python 到达 NeedsInput 检查点后退出，由 C# 高级对话框收集固定报告选项。",
                MessageType.None);
            if (!hasConfiguredPython)
                EditorGUILayout.HelpBox(pythonReason + " 不会使用 PATH、py launcher 或 Windows Store 的 python 占位别名。", MessageType.Warning);
            else
                EditorGUILayout.LabelField("Python 环境", "已解析（启动时会复核 Python 3 版本与受管环境指纹）");
            if (GUILayout.Button("验证 Python 环境", GUILayout.Height(22f)))
            {
                if (ESAutomationSceneScanPythonAdapter.TryPrepareRuntime(out ESAutomationPythonRuntime runtime, out string validationReason))
                    EditorUtility.DisplayDialog("Python 环境可用", "来源：" + runtime.source + "\n运行时：" + runtime.runtimeId + "\n版本：" + runtime.detectedPythonVersion + "\n指纹：" + runtime.environmentFingerprint, "关闭");
                else
                    EditorUtility.DisplayDialog("Python 环境不可用", validationReason, "关闭");
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !hasConfiguredPython))
            {
                if (GUILayout.Button("扫描当前场景（Python 原型）", GUILayout.Height(24f)))
                    ESAutomationSceneScanPrototype.StartSceneScan();
            }
            if (GUILayout.Button("继续待输入的场景扫描", GUILayout.Height(22f)))
                ESAutomationSceneScanPrototype.ResumePendingSceneScan();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("快速任务", EditorStyles.boldLabel);
            Rect quickTaskRect = GUILayoutUtility.GetRect(new GUIContent("选择自动化任务…"), GUI.skin.button, GUILayout.Height(24f));
            if (GUI.Button(quickTaskRect, "选择自动化任务…")) OpenQuickTaskDropdown(quickTaskRect);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("AI 直接调用", EditorStyles.boldLabel);
            bool aiBridgeEnabled = EditorGUILayout.Toggle("授权本机 AI 请求收件箱", ESAutomationAiBridge.IsUserAuthorized);
            if (aiBridgeEnabled != ESAutomationAiBridge.IsUserAuthorized) ESAutomationAiBridge.IsEnabled = aiBridgeEnabled;
            EditorGUILayout.HelpBox(
                aiBridgeEnabled
                    ? ESAutomationAiBridge.ListeningStateDescription + " 受信 AI 仅可通过 ES/Automation/AI/Inbox/*.request.json 调用白名单任务；Unity 会将结构化响应写入 Responses。"
                    : "未授权：AI 请求收件箱默认关闭。首次授权后仍只能调用已注册且 allowAiInvoke 的任务。",
                aiBridgeEnabled ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.LabelField("监听状态", ESAutomationAiBridge.IsListening ? "监听中" : "未监听", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("AI 收件箱", ESAutomationAiBridge.InboxDirectory, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("复制 AI 调用样例", GUILayout.Height(22f))) CopyAiRequestExample();
            DrawPendingSceneModificationApprovals();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawPendingSceneModificationApprovals()
        {
            IReadOnlyList<ESAutomationSceneModificationApprovalInfo> approvals =
                ESAutomationAiBridge.CopyPendingSceneModificationApprovals();
            if (approvals.Count == 0) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("待批准的 AI 场景计划", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "待批准容量",
                approvals.Count + " / " + ESAutomationAiBridge.PendingSceneModificationApprovalCapacity,
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(
                "场景计划必须先由 AI dry-run，再由人工批准一次。批准只允许同一 actor 提交完全相同的计划；切换 PlayMode、关闭 Bridge、域重载、过期或执行一次后都会失效。",
                MessageType.Warning);

            foreach (ESAutomationSceneModificationApprovalInfo approval in approvals)
            {
                JObject plan = approval.CreatePlanData();
                JArray operations = plan["operations"] as JArray ?? new JArray();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("计划 ID", EditorStyles.miniLabel);
                    EditorGUILayout.SelectableLabel(approval.ApprovalId, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    EditorGUILayout.LabelField("请求方", approval.ActorId, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("状态", approval.Status, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("场景", (string)plan["scenePath"] ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("保存当前场景", (bool?)plan["saveRequested"] == true ? "是" : "否", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("到期", approval.ExpiresAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("操作数", operations.Count.ToString(), EditorStyles.wordWrappedMiniLabel);
                    foreach (JToken token in operations)
                    {
                        JObject operation = token as JObject;
                        if (operation == null) continue;
                        string operationName = (string)operation["operation"] ?? string.Empty;
                        string targetPath = (string)operation["targetPath"] ?? string.Empty;
                        string targetGlobalObjectId = (string)operation["targetGlobalObjectId"] ?? string.Empty;
                        string value = operation["value"]?.ToString(Formatting.None) ?? "null";
                        EditorGUILayout.LabelField(
                            operationName + "  " + targetPath + " = " + value,
                            EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.LabelField("目标身份  " + targetGlobalObjectId, EditorStyles.wordWrappedMiniLabel);
                    }

                    if (string.Equals(approval.Status, "AwaitingUserApproval", StringComparison.Ordinal))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("批准一次", GUILayout.Height(22f))
                                && EditorUtility.DisplayDialog(
                                    "批准 AI 场景计划",
                                    "将批准当前计划的一次执行权。AI 仍需使用相同计划和新的 RequestId 提交；批准不会立即修改场景。\n\n"
                                    + "场景：" + ((string)plan["scenePath"] ?? string.Empty)
                                    + "\n操作数：" + operations.Count,
                                    "批准一次",
                                    "取消"))
                            {
                                if (ESAutomationAiBridge.TryApproveSceneModification(approval.ApprovalId, out string reason))
                                    ShowNotification("已批准 AI 场景计划一次。\n" + approval.ApprovalId);
                                else
                                    EditorUtility.DisplayDialog("未能批准场景计划", reason, "关闭");
                            }
                            if (GUILayout.Button("拒绝计划", GUILayout.Height(22f))
                                && EditorUtility.DisplayDialog(
                                    "拒绝 AI 场景计划",
                                    "拒绝后，该 approvalId 将立即失效，AI 必须重新提交 dry-run 计划。",
                                    "拒绝计划",
                                    "取消"))
                            {
                                if (ESAutomationAiBridge.TryRejectSceneModification(approval.ApprovalId, out string reason))
                                    ShowNotification("已拒绝 AI 场景计划。\n" + approval.ApprovalId);
                                else
                                    EditorUtility.DisplayDialog("未能拒绝场景计划", reason, "关闭");
                            }
                        }
                    }
                    else if (string.Equals(approval.Status, "Approved", StringComparison.Ordinal))
                    {
                        if (GUILayout.Button("撤销批准", GUILayout.Height(22f))
                            && EditorUtility.DisplayDialog(
                                "撤销 AI 场景计划批准",
                                "撤销后，该 approvalId 不能再执行场景写入。",
                                "撤销批准",
                                "取消"))
                        {
                            if (ESAutomationAiBridge.TryRevokeSceneModificationApproval(
                                    approval.ApprovalId, out string reason))
                                ShowNotification("已撤销 AI 场景计划批准。\n" + approval.ApprovalId);
                            else
                                EditorUtility.DisplayDialog("未能撤销场景计划批准", reason, "关闭");
                        }
                    }
                }
            }
        }

        private static void OpenQuickTaskDropdown(Rect anchorRect)
        {
            var entries = new List<ESSearchDropdown.Entry>();
            foreach (ESAutomationTaskDescriptor descriptor in ESAutomationFacade.CopyDescriptors())
            {
                ESAutomationTaskPresetDescriptor defaultPreset = null;
                foreach (ESAutomationTaskPresetDescriptor preset in descriptor.presets)
                {
                    if (preset != null && preset.presetId == "default")
                    {
                        defaultPreset = preset;
                        break;
                    }
                }
                if (defaultPreset == null)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled(descriptor.displayName, descriptor.category, "该任务没有无输入快速预设。"));
                    continue;
                }

                ESAutomationTaskDescriptor capturedDescriptor = descriptor;
                ESAutomationTaskPresetDescriptor capturedPreset = defaultPreset;
                entries.Add(ESSearchDropdown.Entry.Item(
                    capturedDescriptor.displayName,
                    () => RunQuickTask(capturedDescriptor, capturedPreset),
                    capturedDescriptor.category,
                    subtitle: capturedPreset.summary,
                    tooltip: capturedDescriptor.summary,
                    keywords: capturedDescriptor.taskId,
                    badge: capturedDescriptor.allowAiInvoke ? "AI" : "人工"));
            }
            if (entries.Count == 0) entries.Add(ESSearchDropdown.Entry.Disabled("没有已注册的快速任务"));
            ESSearchDropdown.Open(anchorRect, "快速自动化任务", entries, minimumWindowSize: new Vector2(620f, 340f));
        }

        private static void RunQuickTask(ESAutomationTaskDescriptor descriptor, ESAutomationTaskPresetDescriptor preset)
        {
            ESAutomationTaskInvocationResult result = ESAutomationFacade.RunTask(new ESAutomationTaskInvocation
            {
                taskId = descriptor.taskId,
                taskVersion = descriptor.taskVersion,
                preset = preset.presetId,
                input = new JObject(),
                fromAi = false,
                actorId = "editor.user",
            });
            if (result.status == "Accepted")
            {
                Debug.Log("[ESAutomation] 快速任务已接受：" + descriptor.taskId + " / RunId=" + result.runId);
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("已启动：" + descriptor.displayName));
                return;
            }
            EditorUtility.DisplayDialog("自动化任务未启动", result.message, "关闭");
        }

        private static void CopyAiRequestExample()
        {
            EditorGUIUtility.systemCopyBuffer = "{\n"
                + "  \"protocolVersion\": 1,\n"
                + "  \"requestId\": \"<32位GUID，不含连字符>\",\n"
                + "  \"actorId\": \"codex.local\",\n"
                + "  \"action\": \"runTask\",\n"
                + "  \"payload\": {\n"
                + "    \"taskId\": \"es.scene.scan\",\n"
                + "    \"taskVersion\": 1,\n"
                + "    \"preset\": \"default\",\n"
                + "    \"input\": {}\n"
                + "  }\n"
                + "}";
            ShowNotification("AI 调用样例已复制；请替换 requestId 后以 .request.json 原子提交到 Inbox。");
        }

        private static void ShowNotification(string message)
        {
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message));
            Debug.Log("[ESAutomation] " + message);
        }
    }

    /// <summary>
    /// Optional commercial governance contracts. They extend the existing Automation
    /// pipeline without changing legacy Worker entry points.
    /// </summary>
    public enum ESAutomationEvidenceState
    {
        Missing, Fresh, Stale, Contradictory, RuntimeNotRun, Invalid
    }

    /// <summary>
    /// 证据来源范围。历史收据没有该字段时默认 Static，保持旧 ES
    /// Worker 可读取；声明 runtimeRequired 的 Criterion 必须显式提交 Runtime。
    /// </summary>
    public enum ESAutomationEvidenceScope
    {
        Static,
        Runtime
    }

    /// <summary>
    /// CompletionDecision 的分层语义。它是对既有 Accepted/Blocked 的兼容扩展：
    /// StaticReviewComplete 只表示静态审查完成，不等价于 Unity/Player 可用。
    /// </summary>
    public enum ESAutomationDecisionStatus
    {
        Unverified,
        PartiallyDone,
        StaticReviewComplete,
        Accepted,
        Blocked
    }

    public enum ESAutomationBlockingLayer
    {
        None,
        StaticCode,
        StaticContract,
        StaticBoundary,
        Evidence,
        Runtime
    }

    [Serializable]
    public sealed class ESAutomationPerformanceBudget
    {
        public int maxDurationSeconds;
        public long maxOutputBytes;
        public int maxRetryCount;
        public int maxFindingCount;

        public void Validate()
        {
            if (maxDurationSeconds < 1 || maxDurationSeconds > ESAutomationTaskContract.MaximumTimeoutSeconds)
                throw new InvalidOperationException("PerformanceBudget maxDurationSeconds is outside the TaskContract limit.");
            if (maxOutputBytes < 1 || maxRetryCount < 0 || maxFindingCount < 0)
                throw new InvalidOperationException("PerformanceBudget contains an invalid limit.");
        }

        public bool TryValidateRunResult(ESAutomationRunResult result, out string reason)
        {
            reason = string.Empty;
            Validate();
            if (result == null)
            {
                reason = "RunResult missing.";
                return false;
            }
            if (!DateTimeOffset.TryParse(result.startedAtUtc, out DateTimeOffset started)
                || !DateTimeOffset.TryParse(result.finishedAtUtc, out DateTimeOffset finished)
                || finished < started)
            {
                reason = "RunResult timestamps are invalid.";
                return false;
            }
            if ((finished - started).TotalSeconds > maxDurationSeconds)
            {
                reason = "RunResult exceeded PerformanceBudget maxDurationSeconds.";
                return false;
            }
            if (result.findings != null && result.findings.Count > maxFindingCount)
            {
                reason = "RunResult exceeded PerformanceBudget maxFindingCount.";
                return false;
            }
            if (result.retryCount < 0 || result.retryCount > maxRetryCount)
            {
                reason = "RunResult exceeded PerformanceBudget maxRetryCount.";
                return false;
            }
            long outputBytes = 0;
            foreach (string declaredOutput in result.outputs ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(declaredOutput))
                {
                    reason = "RunResult contains an empty output path.";
                    return false;
                }
                string outputPath = ResolveGovernedOutputPath(result.runId, declaredOutput);
                if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
                {
                    reason = "RunResult output cannot be verified inside the governed Run directory: " + declaredOutput;
                    return false;
                }
                try
                {
                    outputBytes = checked(outputBytes + new FileInfo(outputPath).Length);
                }
                catch (Exception exception)
                {
                    reason = "RunResult output size cannot be read: " + exception.Message;
                    return false;
                }
            }
            if (outputBytes > maxOutputBytes)
            {
                reason = "RunResult exceeded PerformanceBudget maxOutputBytes.";
                return false;
            }
            return true;
        }

        internal static string ResolveGovernedOutputPath(string runId, string declaredOutput)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) return string.Empty;
            string[] roots =
            {
                Path.Combine(ESAutomationPathPolicy.ReportsRoot, runId),
                Path.Combine(ESAutomationPathPolicy.TempRoot, runId),
            };
            foreach (string root in roots)
            {
                string candidate;
                try
                {
                    candidate = ESAutomationPathPolicy.Normalize(
                        Path.IsPathRooted(declaredOutput) ? declaredOutput : Path.Combine(root, declaredOutput));
                }
                catch
                {
                    continue;
                }
                if (ESAutomationPathPolicy.IsWithin(candidate, new[] { root }) && File.Exists(candidate))
                    return candidate;
            }
            return string.Empty;
        }
    }

    [Serializable]
    public sealed class ESAutomationFreshnessPolicy
    {
        public int maxAgeHours = 168;
        public bool requireSourceHash = true;
        public bool allowRuntimeNotRun = true;
        /// <summary>严格验收时要求 EvidenceBinding 绑定完整 ExecutionSnapshot；旧合同默认关闭。</summary>
        public bool requireExecutionSnapshotBinding;

        public void Validate()
        {
            if (maxAgeHours < 1 || maxAgeHours > 8760)
                throw new InvalidOperationException("FreshnessPolicy maxAgeHours is outside the supported range.");
            if (requireExecutionSnapshotBinding && !requireSourceHash)
                throw new InvalidOperationException(
                    "Strict ExecutionSnapshot binding requires requireSourceHash=true.");
        }
    }

    [Serializable]
    public sealed class ESAutomationClaimEvidenceBinding
    {
        public string claimId = string.Empty;
        public string criterionId = string.Empty;
        public string evidenceHash = string.Empty;
        public string sourceHash = string.Empty;
        public string capturedAtUtc = string.Empty;
        // Optional in protocol v1; required when FreshnessPolicy enables strict snapshot binding.
        public string snapshotId = string.Empty;
        public string inputManifestHash = string.Empty;
        public string taskContractHash = string.Empty;
        public string commandHash = string.Empty;
        public string brainPlanHash = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(claimId) || string.IsNullOrWhiteSpace(criterionId))
                throw new InvalidOperationException("ClaimEvidenceBinding requires claimId and criterionId.");
            if (!ESAutomationWorkerRegistration.IsSha256(evidenceHash)
                || !ESAutomationWorkerRegistration.IsSha256(sourceHash))
                throw new InvalidOperationException("ClaimEvidenceBinding requires evidence and source SHA-256.");
            if (!DateTimeOffset.TryParse(capturedAtUtc, out _))
                throw new InvalidOperationException("ClaimEvidenceBinding capturedAtUtc is invalid.");
            ValidateOptionalHash(inputManifestHash, nameof(inputManifestHash));
            ValidateOptionalHash(taskContractHash, nameof(taskContractHash));
            ValidateOptionalHash(commandHash, nameof(commandHash));
            ValidateOptionalHash(brainPlanHash, nameof(brainPlanHash));
        }

        private static void ValidateOptionalHash(string value, string name)
        {
            if (!string.IsNullOrWhiteSpace(value) && !ESAutomationWorkerRegistration.IsSha256(value))
                throw new InvalidOperationException("ClaimEvidenceBinding " + name + " must be SHA-256 when provided.");
        }
    }

    /// <summary>
    /// Independent verifier identity registry. A matching string alone is not
    /// sufficient evidence; the verifier must be registered by the application.
    /// </summary>
    public static class ESAutomationVerifierRegistry
    {
        private static readonly Dictionary<string, Func<ESAutomationCriterionResult, bool>> Verifiers =
            new Dictionary<string, Func<ESAutomationCriterionResult, bool>>(StringComparer.Ordinal);

        static ESAutomationVerifierRegistry()
        {
            Register("es.scene.scan.promoted-output-hash", IsFreshPassedResult);
            Register("es.feishu.output-hash", IsFreshPassedResult);
        }

        public static void Register(string verifierId, Func<ESAutomationCriterionResult, bool> verifier)
        {
            if (string.IsNullOrWhiteSpace(verifierId))
                throw new ArgumentException("VerifierId cannot be empty.", nameof(verifierId));
            if (verifier == null) throw new ArgumentNullException(nameof(verifier));
            string key = verifierId.Trim();
            if (Verifiers.ContainsKey(key))
                throw new InvalidOperationException("VerifierId is already registered and cannot be replaced: " + key);
            Verifiers.Add(key, verifier);
        }

        public static bool IsRegistered(string verifierId)
            => !string.IsNullOrWhiteSpace(verifierId) && Verifiers.ContainsKey(verifierId.Trim());

        public static bool TryVerify(string verifierId, ESAutomationCriterionResult result, out string reason)
        {
            reason = string.Empty;
            if (!IsRegistered(verifierId))
            {
                reason = "Verifier is not registered.";
                return false;
            }
            if (!Verifiers[verifierId.Trim()](result))
            {
                reason = "Registered verifier rejected the criterion result.";
                return false;
            }
            return true;
        }

        public static IReadOnlyCollection<string> RegisteredVerifiers => Verifiers.Keys.ToArray();

        private static bool IsFreshPassedResult(ESAutomationCriterionResult result)
            => result != null && result.passed
                && result.evidenceState == ESAutomationEvidenceState.Fresh
                && ESAutomationWorkerRegistration.IsSha256(result.evidenceHash);
    }

    [Serializable]
    public sealed class ESAutomationAcceptanceCriterion
    {
        public string criterionId = string.Empty;
        public string verifierId = string.Empty;
        public string description = string.Empty;
        public bool required = true;
        public bool runtimeRequired;
        public List<string> forbiddenConditions = new List<string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(criterionId) || string.IsNullOrWhiteSpace(verifierId))
                throw new InvalidOperationException("Acceptance criterion requires criterionId and verifierId.");
            if ((forbiddenConditions ?? new List<string>()).Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Acceptance criterion contains an empty forbidden condition.");
        }
    }

    [Serializable]
    public sealed class ESAutomationAcceptanceCriteria
    {
        public int schemaVersion = 1;
        public List<ESAutomationAcceptanceCriterion> criteria = new List<ESAutomationAcceptanceCriterion>();
        public ESAutomationFreshnessPolicy freshnessPolicy;

        public void Validate()
        {
            if (schemaVersion != 1) throw new InvalidOperationException("Unsupported AcceptanceCriteria schema version.");
            if (freshnessPolicy != null) freshnessPolicy.Validate();
            if (criteria == null || criteria.Count == 0)
                throw new InvalidOperationException("AcceptanceCriteria requires at least one criterion.");
            if (!criteria.Any(criterion => criterion != null && criterion.required))
                throw new InvalidOperationException("AcceptanceCriteria requires at least one required criterion.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAutomationAcceptanceCriterion criterion in criteria ?? new List<ESAutomationAcceptanceCriterion>())
            {
                if (criterion == null) throw new InvalidOperationException("AcceptanceCriteria contains a null criterion.");
                criterion.Validate();
                if (!ids.Add(criterion.criterionId))
                    throw new InvalidOperationException("Duplicate acceptance criterion: " + criterion.criterionId);
            }
        }
    }

    [Serializable]
    public sealed class ESAutomationCapabilityEnvelope
    {
        private const ESAutomationCapability KnownCapabilities =
            ESAutomationCapability.ReadArtifacts
            | ESAutomationCapability.WriteReports
            | ESAutomationCapability.WriteAssets
            | ESAutomationCapability.Delete
            | ESAutomationCapability.Upload
            | ESAutomationCapability.Publish
            | ESAutomationCapability.WriteTemp
            | ESAutomationCapability.ExternalRead
            | ESAutomationCapability.ExternalWrite
            | ESAutomationCapability.MaterializeUI;

        public ESAutomationCapability userAuthorization;
        public ESAutomationCapability taskContract;
        public ESAutomationCapability aiCommand;
        public ESAutomationCapability workerCapability;
        public ESAutomationCapability projectBoundary;

        public ESAutomationCapability EffectiveCapability()
            => userAuthorization & taskContract & aiCommand & workerCapability & projectBoundary;

        public bool Allows(ESAutomationCapability requested)
            => (requested & ~EffectiveCapability()) == ESAutomationCapability.None;

        /// <summary>
        /// 调用级权限门禁。Envelope 仍是合同的一部分，但不能脱离调用身份单独解释：
        /// AI 调用必须绑定已签发的 AIBrain PlanHash；人工调用必须带有明确 ActorId，
        /// 且 userAuthorization 必须覆盖本次实际请求能力。
        /// </summary>
        public bool AllowsInvocation(ESAutomationTaskInvocation invocation,
            ESAutomationCapability requested, out string reason)
        {
            reason = string.Empty;
            try { Validate(); }
            catch (Exception exception) { reason = exception.Message; return false; }
            if (invocation == null)
            {
                reason = "CapabilityEnvelope 缺少调用身份。";
                return false;
            }
            if (invocation.fromAi)
            {
                if (!ESAutomationAiBridge.IsUserAuthorized)
                {
                    reason = "AI CapabilityEnvelope 未获得当前用户授权。";
                    return false;
                }
                if (!ESAutomationWorkerRegistration.IsSha256(invocation.brainPlanHash))
                {
                    reason = "AI CapabilityEnvelope 缺少有效的 AIBrain PlanHash。";
                    return false;
                }
            }
            else if (string.IsNullOrWhiteSpace(invocation.actorId))
            {
                reason = "人工 CapabilityEnvelope 缺少 ActorId。";
                return false;
            }
            if (!Allows(requested))
            {
                reason = "请求能力超出 CapabilityEnvelope 有效权限交集。";
                return false;
            }
            if ((requested & ~userAuthorization) != ESAutomationCapability.None)
            {
                reason = "本次请求能力未获得 userAuthorization 覆盖。";
                return false;
            }
            return true;
        }

        public void Validate()
        {
            if ((userAuthorization & ~KnownCapabilities) != ESAutomationCapability.None
                || (taskContract & ~KnownCapabilities) != ESAutomationCapability.None
                || (aiCommand & ~KnownCapabilities) != ESAutomationCapability.None
                || (workerCapability & ~KnownCapabilities) != ESAutomationCapability.None
                || (projectBoundary & ~KnownCapabilities) != ESAutomationCapability.None)
                throw new InvalidOperationException("CapabilityEnvelope contains unknown capability bits.");
            if (EffectiveCapability() != ESAutomationCapability.None
                && (taskContract == ESAutomationCapability.None || workerCapability == ESAutomationCapability.None))
                throw new InvalidOperationException("CapabilityEnvelope requires TaskContract and Worker bounds.");
        }
    }

    [Serializable]
    public sealed class ESAutomationExecutionSnapshot
    {
        public string snapshotId = string.Empty;
        public string inputManifestHash = string.Empty;
        public string sourceHash = string.Empty;
        public string taskContractHash = string.Empty;
        public string commandHash = string.Empty;
        public string brainPlanHash = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(snapshotId)) throw new InvalidOperationException("ExecutionSnapshot requires snapshotId.");
            ValidateHash(inputManifestHash, nameof(inputManifestHash));
            ValidateHash(sourceHash, nameof(sourceHash));
            ValidateHash(taskContractHash, nameof(taskContractHash));
            ValidateHash(commandHash, nameof(commandHash));
            ValidateHash(brainPlanHash, nameof(brainPlanHash));
        }

        private static void ValidateHash(string value, string name)
        {
            if (!ESAutomationWorkerRegistration.IsSha256(value))
                throw new InvalidOperationException("ExecutionSnapshot requires SHA-256 " + name + ".");
        }
    }

    [Serializable]
    public sealed class ESAutomationCriterionResult
    {
        public string criterionId = string.Empty;
        public string verifierId = string.Empty;
        public bool passed;
        public ESAutomationEvidenceState evidenceState = ESAutomationEvidenceState.Missing;
        public ESAutomationEvidenceScope evidenceScope = ESAutomationEvidenceScope.Static;
        public string evidenceHash = string.Empty;
        public string message = string.Empty;
        public ESAutomationClaimEvidenceBinding evidenceBinding;

        /// <summary>
        /// 统一的 Criterion 收据结构门禁。它只验证字段形状和身份绑定，
        /// 不替代注册 Verifier 的业务判断，因此不会把静态合同误当成业务通过。
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(criterionId) || string.IsNullOrWhiteSpace(verifierId))
                throw new InvalidOperationException("CriterionResult requires criterionId and verifierId.");
            if (!Enum.IsDefined(typeof(ESAutomationEvidenceState), evidenceState))
                throw new InvalidOperationException("CriterionResult contains an unknown evidence state.");
            if (!Enum.IsDefined(typeof(ESAutomationEvidenceScope), evidenceScope))
                throw new InvalidOperationException("CriterionResult contains an unknown evidence scope.");
            if (passed && !ESAutomationWorkerRegistration.IsSha256(evidenceHash))
                throw new InvalidOperationException("Passed CriterionResult requires an evidence SHA-256.");
            if (evidenceBinding != null)
            {
                evidenceBinding.Validate();
                if (!string.Equals(evidenceBinding.criterionId, criterionId, StringComparison.Ordinal)
                    || !string.Equals(evidenceBinding.evidenceHash, evidenceHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("CriterionResult evidence binding does not match the result.");
            }
        }
    }

    [Serializable]
    public sealed class ESAutomationTraceReconciliation
    {
        public string traceId = string.Empty;
        public int expectedToolCalls;
        public int observedToolCalls;
        public int unauthorizedToolCalls;
        public int duplicateToolCalls;
        public bool reconciled;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(traceId)) throw new InvalidOperationException("TraceReconciliation requires traceId.");
            if (expectedToolCalls < 0 || observedToolCalls < 0 || unauthorizedToolCalls < 0 || duplicateToolCalls < 0)
                throw new InvalidOperationException("TraceReconciliation counters cannot be negative.");
            if (observedToolCalls < unauthorizedToolCalls)
                throw new InvalidOperationException("TraceReconciliation unauthorized calls exceed observed calls.");
        }

        public bool CanAccept()
            => reconciled && expectedToolCalls == observedToolCalls
                && unauthorizedToolCalls == 0 && duplicateToolCalls == 0;
    }

    [Serializable]
    public sealed class ESAutomationCompletionDecision
    {
        public string decisionId = Guid.NewGuid().ToString("N");
        public string runId = string.Empty;
        public bool accepted;
        public string executionStatus = string.Empty;
        public ESAutomationFreshnessPolicy freshnessPolicy;
        public List<ESAutomationCriterionResult> criterionResults = new List<ESAutomationCriterionResult>();
        public List<string> forbiddenConditions = new List<string>();
        public bool unauthorizedToolCalls;
        public bool staleEvidence;
        public bool contradictoryEvidence;
        public bool sourceDrift;
        public bool budgetViolation;
        public bool traceReconciled;
        public ESAutomationTraceReconciliation traceReconciliation;
        // 商业级分层状态；旧 Worker 不填充时保持空值并按既有字段判断。
        public string decisionStatus = string.Empty;
        public string blockingLayer = string.Empty;
        public string staticCodeStatus = string.Empty;
        public string staticContractStatus = string.Empty;
        public string staticBoundaryStatus = string.Empty;
        public string evidenceStatus = string.Empty;
        public string runtimeStatus = string.Empty;
        public List<string> claimsNotProven = new List<string>();
        public string nextAction = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new InvalidOperationException("CompletionDecision requires runId.");
            if (!Guid.TryParseExact(decisionId, "N", out _))
                throw new InvalidOperationException("CompletionDecision decisionId must be an N-format GUID.");
            if (criterionResults == null) criterionResults = new List<ESAutomationCriterionResult>();
            if (forbiddenConditions == null) forbiddenConditions = new List<string>();
            if (claimsNotProven == null) claimsNotProven = new List<string>();
            if (freshnessPolicy != null) freshnessPolicy.Validate();
            if (traceReconciliation != null) traceReconciliation.Validate();
            if (!string.IsNullOrWhiteSpace(decisionStatus)
                && !Enum.IsDefined(typeof(ESAutomationDecisionStatus), decisionStatus))
                throw new InvalidOperationException("CompletionDecision contains an unknown decisionStatus.");
            if (!string.IsNullOrWhiteSpace(blockingLayer)
                && !Enum.IsDefined(typeof(ESAutomationBlockingLayer), BlockingLayerToEnum(blockingLayer)))
                throw new InvalidOperationException("CompletionDecision contains an unknown blockingLayer.");
            if (string.Equals(decisionStatus, "Accepted", StringComparison.Ordinal)
                && !accepted)
                throw new InvalidOperationException("Accepted CompletionDecision must set accepted=true.");
            if (string.Equals(decisionStatus, "Blocked", StringComparison.Ordinal)
                && accepted)
                throw new InvalidOperationException("Blocked CompletionDecision cannot set accepted=true.");
            if (accepted
                && !string.IsNullOrWhiteSpace(decisionStatus)
                && !string.Equals(decisionStatus, "Accepted", StringComparison.Ordinal))
                throw new InvalidOperationException("Only an Accepted CompletionDecision may set accepted=true.");
            if (accepted && !string.Equals(executionStatus, "Passed", StringComparison.Ordinal))
                throw new InvalidOperationException("Accepted CompletionDecision requires executionStatus=Passed.");
            if (string.Equals(decisionStatus, "Accepted", StringComparison.Ordinal)
                && !string.Equals(executionStatus, "Passed", StringComparison.Ordinal))
                throw new InvalidOperationException("Accepted CompletionDecision requires executionStatus=Passed.");
            foreach (ESAutomationCriterionResult result in criterionResults)
            {
                if (result == null) throw new InvalidOperationException("CompletionDecision contains an invalid Criterion result.");
                result.Validate();
            }
        }

        public bool CanAccept()
        {
            return CanAccept(null);
        }

        /// <summary>
        /// 对照受信 TaskContract 验收清单判定收据。无参数重载保留旧调用语义；
        /// 严格发布路径必须传入合同，确保 required Criterion 不能被收据遗漏。
        /// </summary>
        public bool CanAccept(ESAutomationAcceptanceCriteria contractCriteria)
        {
            try
            {
                Validate();
                if (contractCriteria != null) contractCriteria.Validate();
            }
            catch
            {
                return false;
            }
            if (!string.Equals(executionStatus, "Passed", StringComparison.Ordinal)
                || criterionResults == null || criterionResults.Count == 0)
                return false;
            // A governed acceptance contract opts into the commercial decision
            // vocabulary. Legacy callers without a contract may still use the
            // historical boolean-only compatibility path, but a release gate
            // must never accept an unlabeled decision.
            if (contractCriteria != null
                && !string.Equals(decisionStatus, "Accepted", StringComparison.Ordinal))
                return false;
            // 新分层字段一旦明确报告静态硬阻断，旧的无参数/兼容入口也不能绕过它。
            if (string.Equals(decisionStatus, "Blocked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(staticCodeStatus, "blocked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(staticContractStatus, "blocked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(staticBoundaryStatus, "blocked", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrWhiteSpace(decisionStatus)
                && !string.Equals(decisionStatus, "Accepted", StringComparison.Ordinal))
                return false;
            if (contractCriteria != null)
            {
                if (contractCriteria.freshnessPolicy != null
                    && !HasSameFreshnessPolicy(freshnessPolicy, contractCriteria.freshnessPolicy))
                    return false;
                var resultIds = new HashSet<string>(StringComparer.Ordinal);
                var contractIds = new HashSet<string>(contractCriteria.criteria.Select(item => item.criterionId), StringComparer.Ordinal);
                foreach (ESAutomationCriterionResult result in criterionResults)
                {
                    if (result == null || string.IsNullOrWhiteSpace(result.criterionId)
                        || !resultIds.Add(result.criterionId)
                        || !contractIds.Contains(result.criterionId))
                        return false;
                }
                foreach (ESAutomationAcceptanceCriterion criterion in contractCriteria.criteria)
                {
                    ESAutomationCriterionResult result = criterionResults.FirstOrDefault(item =>
                        item != null && string.Equals(item.criterionId, criterion.criterionId, StringComparison.Ordinal));
                    if (criterion.required && result == null) return false;
                    if (result != null && !string.Equals(result.verifierId, criterion.verifierId, StringComparison.Ordinal))
                        return false;
                    if (result != null && criterion.runtimeRequired
                        && result.evidenceScope != ESAutomationEvidenceScope.Runtime)
                        return false;
                }
            }
            return criterionResults.All(result => result != null && result.passed
                && ESAutomationVerifierRegistry.TryVerify(result.verifierId, result, out _)
                && (freshnessPolicy == null || !freshnessPolicy.requireSourceHash
                    || IsValidEvidenceBinding(result, freshnessPolicy))
                && result.evidenceState == ESAutomationEvidenceState.Fresh)
                && (forbiddenConditions ?? new List<string>()).Count == 0
                && !unauthorizedToolCalls && !staleEvidence && !contradictoryEvidence
                && !sourceDrift && !budgetViolation
                && (traceReconciliation == null ? traceReconciled : traceReconciliation.CanAccept());
        }

        /// <summary>
        /// 根据外部验证器已提交的分层状态生成可解释结论。
        /// 不执行副作用，也不把静态完成升级为 Runtime Accepted。
        /// </summary>
        public void RefreshDecisionSemantics()
        {
            bool acceptanceWasClaimed = accepted;
            if (claimsNotProven == null) claimsNotProven = new List<string>();
            if (string.IsNullOrWhiteSpace(runtimeStatus)) runtimeStatus = "runtime-not-run";
            if (string.IsNullOrWhiteSpace(evidenceStatus)) evidenceStatus = "not-evaluated";
            bool codeBlocked = string.Equals(staticCodeStatus, "blocked", StringComparison.OrdinalIgnoreCase);
            bool contractBlocked = string.Equals(staticContractStatus, "blocked", StringComparison.OrdinalIgnoreCase);
            bool boundaryBlocked = string.Equals(staticBoundaryStatus, "blocked", StringComparison.OrdinalIgnoreCase);
            bool evidencePending = string.Equals(evidenceStatus, "not-evaluated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "stale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "invalid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "contradictory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "missing-or-stale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evidenceStatus, "evidence-pending", StringComparison.OrdinalIgnoreCase);
            bool runtimePending = string.Equals(runtimeStatus, "runtime-not-run", StringComparison.OrdinalIgnoreCase);
            if (codeBlocked || contractBlocked || boundaryBlocked || unauthorizedToolCalls || sourceDrift || budgetViolation)
            {
                accepted = false;
                decisionStatus = "Blocked";
                blockingLayer = boundaryBlocked ? "static-boundary" : contractBlocked ? "static-contract" : codeBlocked ? "static-code" : "evidence";
                if (acceptanceWasClaimed)
                    ESAIBrainFailureTelemetry.Record("ClaimDowngraded", "completion-decision",
                        decisionStatus + "|" + blockingLayer, runId);
                return;
            }
            // A persisted boolean is not an external acceptance proof. If the
            // report still has pending evidence/runtime, or lacks criterion
            // results, clear the compatibility flag before deriving the public
            // status so an intermediate report can never masquerade as Accepted.
            if (accepted && (evidencePending || runtimePending
                || criterionResults == null || criterionResults.Count == 0
                || (forbiddenConditions ?? new List<string>()).Count > 0
                || unauthorizedToolCalls || staleEvidence || contradictoryEvidence
                || sourceDrift || budgetViolation))
                accepted = false;
            if (acceptanceWasClaimed && !accepted)
                ESAIBrainFailureTelemetry.Record("ClaimDowngraded", "completion-decision",
                    evidenceStatus + "|" + runtimeStatus, runId);
            if (accepted)
            {
                decisionStatus = "Accepted";
                blockingLayer = "none";
                return;
            }
            if (evidencePending)
            {
                decisionStatus = "Unverified";
                blockingLayer = "evidence";
                return;
            }
            if (runtimePending)
            {
                decisionStatus = "StaticReviewComplete";
                blockingLayer = "runtime";
                return;
            }
            decisionStatus = criterionResults != null && criterionResults.Any(item => item != null && item.passed)
                ? "PartiallyDone" : "Unverified";
            blockingLayer = "none";
        }

        private static bool HasSameFreshnessPolicy(ESAutomationFreshnessPolicy actual,
            ESAutomationFreshnessPolicy expected)
        {
            return actual != null && expected != null
                && actual.maxAgeHours == expected.maxAgeHours
                && actual.requireSourceHash == expected.requireSourceHash
                && actual.allowRuntimeNotRun == expected.allowRuntimeNotRun
                && actual.requireExecutionSnapshotBinding == expected.requireExecutionSnapshotBinding;
        }

        private static ESAutomationBlockingLayer BlockingLayerToEnum(string value)
        {
            switch (value ?? string.Empty)
            {
                case "none": return ESAutomationBlockingLayer.None;
                case "static-code": return ESAutomationBlockingLayer.StaticCode;
                case "static-contract": return ESAutomationBlockingLayer.StaticContract;
                case "static-boundary": return ESAutomationBlockingLayer.StaticBoundary;
                case "evidence": return ESAutomationBlockingLayer.Evidence;
                case "runtime": return ESAutomationBlockingLayer.Runtime;
                default: return (ESAutomationBlockingLayer)(-1);
            }
        }

        private static bool IsValidEvidenceBinding(ESAutomationCriterionResult result,
            ESAutomationFreshnessPolicy policy)
        {
            ESAutomationClaimEvidenceBinding binding = result?.evidenceBinding;
            if (binding == null) return false;
            try
            {
                binding.Validate();
                if (!string.Equals(binding.criterionId, result.criterionId, StringComparison.Ordinal)
                    || !string.Equals(binding.evidenceHash, result.evidenceHash, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!DateTimeOffset.TryParse(binding.capturedAtUtc, out DateTimeOffset capturedAt)) return false;
                TimeSpan age = DateTimeOffset.UtcNow - capturedAt.ToUniversalTime();
                return age >= TimeSpan.Zero && age <= TimeSpan.FromHours(policy.maxAgeHours);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Deterministic helpers used by Facade/Endpoint adapters during migration.
    /// They are deliberately side-effect free so static replay can exercise them.
    /// </summary>
    public static class ESAutomationGovernance
    {
        public static bool MatchesTaskContract(ESAutomationTaskContract contract,
            ESAutomationExecutionSnapshot snapshot, out string reason)
        {
            reason = string.Empty;
            if (contract == null || snapshot == null)
            {
                reason = "TaskContract 或 ExecutionSnapshot 缺失。";
                return false;
            }
            try
            {
                string actualHash = contract.ComputeStableHash();
                if (!string.Equals(actualHash, snapshot.taskContractHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    reason = "TaskContract Hash 与 ExecutionSnapshot 不一致。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                reason = "TaskContract Hash 无法计算：" + exception.Message;
                return false;
            }
        }

        public static bool MatchesInputManifest(string inputManifestHash,
            ESAutomationExecutionSnapshot snapshot, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(inputManifestHash)) return true;
            if (snapshot == null)
            {
                reason = "Invocation 输入 Manifest Hash 缺少 ExecutionSnapshot。";
                return false;
            }
            if (!string.Equals(inputManifestHash, snapshot.inputManifestHash,
                StringComparison.OrdinalIgnoreCase))
            {
                reason = "Invocation 输入 Manifest Hash 与 ExecutionSnapshot 不一致。";
                return false;
            }
            return true;
        }

        public static string ComputeIdempotencyKey(string taskId, int taskVersion,
            string inputManifestHash, string brainPlanHash)
        {
            string canonical = (taskId ?? string.Empty).Trim() + "|" + taskVersion
                + "|" + (inputManifestHash ?? string.Empty).Trim().ToLowerInvariant()
                + "|" + (brainPlanHash ?? string.Empty).Trim().ToLowerInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static bool MatchesSnapshot(ESAutomationExecutionSnapshot expected,
            ESAutomationExecutionSnapshot actual, out string reason)
        {
            reason = string.Empty;
            if (expected == null || actual == null)
            {
                reason = "ExecutionSnapshot missing.";
                return false;
            }
            try
            {
                expected.Validate();
                actual.Validate();
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
            if (!string.Equals(expected.snapshotId, actual.snapshotId, StringComparison.Ordinal)
                || !string.Equals(expected.inputManifestHash, actual.inputManifestHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.sourceHash, actual.sourceHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.taskContractHash, actual.taskContractHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.commandHash, actual.commandHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.brainPlanHash, actual.brainPlanHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = "ExecutionSnapshot drift detected.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether a RunResult is ready to opt into strict snapshot-bound
        /// acceptance. This is a migration gate only: it does not enable strict
        /// mode for legacy producers and it has no side effects.
        /// </summary>
        public static bool IsStrictSnapshotBindingReady(ESAutomationTaskContract contract,
            ESAutomationRunResult result, out string reason)
        {
            reason = string.Empty;
            if (contract == null || result == null)
            {
                reason = "Strict snapshot binding requires TaskContract and RunResult.";
                return false;
            }
            if (result.executionSnapshot == null)
            {
                reason = "Strict snapshot binding requires RunResult.ExecutionSnapshot.";
                return false;
            }
            try
            {
                contract.Validate();
                result.Validate();
                if (!MatchesTaskContract(contract, result.executionSnapshot, out reason)) return false;
                if (!MatchesInputManifest(result.inputManifestHash, result.executionSnapshot, out reason)) return false;
                if (result.completionDecision == null)
                {
                    reason = "Strict snapshot binding requires CompletionDecision.";
                    return false;
                }
                if (!ESAutomationReleaseGate.EvidenceBindsToSnapshot(
                    result.completionDecision, result.executionSnapshot, true))
                {
                    reason = "Strict snapshot binding requires complete criterion evidence bindings.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        public static bool TryEvaluateCompletion(ESAutomationAcceptanceCriteria criteria,
            string runId, string executionStatus, IEnumerable<ESAutomationCriterionResult> results,
            IEnumerable<string> forbiddenConditions, bool unauthorizedToolCalls,
            bool sourceDrift, bool traceReconciled, out ESAutomationCompletionDecision decision,
            out string reason)
        {
            decision = new ESAutomationCompletionDecision
            {
                runId = runId ?? string.Empty,
                executionStatus = executionStatus ?? string.Empty,
                unauthorizedToolCalls = unauthorizedToolCalls,
                sourceDrift = sourceDrift,
                traceReconciled = traceReconciled,
                forbiddenConditions = new List<string>(forbiddenConditions ?? Enumerable.Empty<string>())
            };
            reason = string.Empty;
            try
            {
                if (criteria == null) throw new InvalidOperationException("AcceptanceCriteria missing.");
                criteria.Validate();
                decision.freshnessPolicy = criteria.freshnessPolicy;
                decision.Validate();
                var actual = (results ?? Enumerable.Empty<ESAutomationCriterionResult>())
                    .Where(item => item != null)
                    .ToDictionary(item => item.criterionId, StringComparer.Ordinal);
                foreach (ESAutomationAcceptanceCriterion criterion in criteria.criteria)
                {
                    if (!criterion.required) continue;
                    if (!actual.TryGetValue(criterion.criterionId, out ESAutomationCriterionResult result))
                    {
                        decision.criterionResults.Add(new ESAutomationCriterionResult
                        {
                            criterionId = criterion.criterionId,
                            verifierId = criterion.verifierId,
                            passed = false,
                            evidenceState = ESAutomationEvidenceState.Missing,
                            message = "Required criterion result missing."
                        });
                        continue;
                    }
                    if (!string.Equals(result.verifierId, criterion.verifierId, StringComparison.Ordinal))
                    {
                        result.passed = false;
                        result.evidenceState = ESAutomationEvidenceState.Invalid;
                        result.message = "Verifier identity mismatch.";
                    }
                    decision.criterionResults.Add(result);
                }
                // 评估阶段也必须使用合同绑定重载；无参数重载仅为旧观察模式保留，
                // 不能让 runtimeRequired、Verifier 身份或必需 Criterion 在这里被绕过。
                decision.accepted = decision.CanAccept(criteria);
                decision.RefreshDecisionSemantics();
                if (!decision.accepted) reason = "CompletionDecision conditions are not satisfied.";
                return decision.accepted;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                decision.accepted = false;
                return false;
            }
        }
    }
}
