using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ESFramework.ESAITest
{
    /// <summary>
    /// 低频、事件驱动的外部对话 Agent IPC。Agent 写入严格的 LLM 意图 JSON，Unity 只执行
    /// ESAITestNaturalLanguageRouter 的授权 DTO；普通游戏帧不做全量扫描。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESAITestConversationIpc : MonoBehaviour
    {
        private const float FastPollSeconds = 0.1f;
        private const float IdlePollSeconds = 0.5f;
        private const int MaximumFilesPerPoll = 2;
        private const int MaximumEnvelopeBytes = 128 * 1024;
        private const string RootDirectoryName = "conversation";
        private const string RequestDirectoryName = "requests";
        private const string ReceiptDirectoryName = "receipts";
        private const string ProcessedDirectoryName = "processed";

        private readonly List<string> files = new List<string>(MaximumFilesPerPoll);
        private readonly Dictionary<string, PendingVerification> pendingVerifications = new Dictionary<string, PendingVerification>(4);
        private string requestDirectory;
        private string receiptDirectory;
        private string processedDirectory;
        private float nextPollTime;

        private sealed class PendingVerification
        {
            public ESAITestConversationIntentEnvelopeDto envelope;
            public ESAITestNaturalLanguageExecutionResultDto result;
            public string error;
        }

        private void Awake()
        {
            string root = Path.Combine(Application.persistentDataPath, "ESAITest", RootDirectoryName);
            requestDirectory = Path.Combine(root, RequestDirectoryName);
            receiptDirectory = Path.Combine(root, ReceiptDirectoryName);
            processedDirectory = Path.Combine(root, ProcessedDirectoryName);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPollTime)
                return;
            bool processed = PollRequests();
            nextPollTime = Time.unscaledTime + (processed ? FastPollSeconds : IdlePollSeconds);
        }

        private bool PollRequests()
        {
            try
            {
                PollPendingVerifications();
                if (!Directory.Exists(requestDirectory))
                    return false;
                files.Clear();
                foreach (string path in Directory.EnumerateFiles(requestDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    files.Add(path);
                    if (files.Count >= MaximumFilesPerPoll)
                        break;
                }
                if (files.Count == 0)
                    return false;
                files.Sort(StringComparer.Ordinal);
                for (int i = 0; i < files.Count; i++)
                    ProcessRequest(files[i]);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESAITest] 对话 IPC 轮询失败：" + exception.Message, this);
                return false;
            }
        }

        private void ProcessRequest(string path)
        {
            ESAITestConversationIntentEnvelopeDto envelope = null;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > MaximumEnvelopeBytes)
                    throw new InvalidDataException("LLM 意图信封超过大小上限。");
                envelope = JsonUtility.FromJson<ESAITestConversationIntentEnvelopeDto>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.requestId))
                    WriteReceipt(CreateReceipt(envelope, "received", "received", null, null));
                ValidateEnvelope(envelope);

                if (DateTime.UtcNow.Ticks > envelope.createdUtcTicks
                    + TimeSpan.FromSeconds(envelope.timeToLiveSeconds).Ticks)
                    throw new InvalidDataException("LLM 意图信封已过期。");

                WriteReceipt(CreateReceipt(envelope, "accepted", "accepted_pending_validation", null, null));
                ESAITestNaturalLanguageRouter.TryExecuteInterpretedRoute(
                    envelope.route,
                    envelope.requestId,
                    out ESAITestNaturalLanguageExecutionResultDto result,
                    out string error);
                WriteReceipt(CreateReceipt(envelope, "accepted", result.accepted ? "accepted" : result.statusCode, result, error));
                WriteReceipt(CreateReceipt(envelope, "executed", result.statusCode, result, error));
                string verificationState = ResolveVerificationState(result);
                WriteReceipt(CreateReceipt(envelope, "verified", result.statusCode, result, error, verificationState));
                if (result.accepted && string.Equals(verificationState, "pending_prompt_consumption", StringComparison.Ordinal))
                {
                    pendingVerifications[envelope.requestId] = new PendingVerification
                    {
                        envelope = envelope,
                        result = result,
                        error = error,
                    };
                }
                MoveToDisposition(path, "processed");
            }
            catch (Exception exception)
            {
                if (envelope != null)
                {
                    WriteReceipt(CreateReceipt(envelope, "accepted", "rejected", null, exception.Message));
                    WriteReceipt(CreateReceipt(envelope, "executed", "not_executed", null, exception.Message));
                    WriteReceipt(CreateReceipt(envelope, "verified", "not_verified", null, exception.Message, "rejected"));
                }
                MoveToDisposition(path, "rejected");
            }
        }

        private void PollPendingVerifications()
        {
            if (pendingVerifications.Count == 0)
                return;
            ESAITestAIPromptDto consumed = ESAITestObservationRuntimeState.LastConsumedPrompt;
            if (consumed == null)
                return;
            string[] requestIds = new string[pendingVerifications.Count];
            pendingVerifications.Keys.CopyTo(requestIds, 0);
            for (int i = 0; i < requestIds.Length; i++)
            {
                if (!pendingVerifications.TryGetValue(requestIds[i], out PendingVerification pending)
                    || pending.result == null
                    || !string.Equals(pending.result.promptId, consumed.promptId, StringComparison.Ordinal))
                    continue;
                WriteReceipt(CreateReceipt(pending.envelope, "verified", "passed", pending.result,
                    pending.error, "prompt_consumed"));
                pendingVerifications.Remove(requestIds[i]);
            }
        }

        private static void ValidateEnvelope(ESAITestConversationIntentEnvelopeDto envelope)
        {
            if (envelope == null
                || !string.Equals(envelope.schema, ESAITestConversationIntentEnvelopeDto.Schema, StringComparison.Ordinal)
                || envelope.protocolVersion != ESAITestProtocol.CurrentVersion
                || string.IsNullOrWhiteSpace(envelope.requestId)
                || envelope.requestId.Length > ESAITestProtocol.MaxIdentityLength
                || string.IsNullOrWhiteSpace(envelope.source)
                || envelope.source.Length > ESAITestProtocol.MaxIdentityLength
                || envelope.createdUtcTicks <= 0
                || float.IsNaN(envelope.timeToLiveSeconds)
                || float.IsInfinity(envelope.timeToLiveSeconds)
                || envelope.timeToLiveSeconds < 1f
                || envelope.timeToLiveSeconds > 3600f
                || envelope.route == null)
                throw new InvalidDataException("LLM 意图信封的身份、TTL、协议或 route 无效。");
            if (!string.IsNullOrWhiteSpace(envelope.originalText)
                && envelope.originalText.Length > ESAITestProtocol.MaxTextLength)
                throw new InvalidDataException("originalText 超过协议长度上限。");
        }

        private static ESAITestConversationReceiptDto CreateReceipt(
            ESAITestConversationIntentEnvelopeDto envelope,
            string stage,
            string statusCode,
            ESAITestNaturalLanguageExecutionResultDto result,
            string error,
            string verificationState = null)
        {
            return new ESAITestConversationReceiptDto
            {
                requestId = envelope?.requestId ?? string.Empty,
                stage = stage,
                statusCode = statusCode ?? string.Empty,
                source = envelope?.source ?? string.Empty,
                originalText = envelope?.originalText ?? string.Empty,
                normalizedText = result?.normalizedText ?? envelope?.route?.normalizedText ?? string.Empty,
                intent = result?.intent ?? envelope?.route?.intent ?? string.Empty,
                parsedMessage = result?.parsedMessage ?? envelope?.route?.message ?? string.Empty,
                parsedGoal = result?.parsedGoal ?? envelope?.route?.goal ?? string.Empty,
                parsedPriority = result?.parsedPriority ?? envelope?.route?.priority ?? string.Empty,
                parsedTtlSeconds = result?.parsedTtlSeconds ?? envelope?.route?.ttlSeconds ?? 0f,
                confidence = result?.confidence ?? envelope?.route?.confidence ?? 0f,
                boundRunId = result?.boundRunId ?? envelope?.route?.boundRunId ?? string.Empty,
                runId = result?.runId ?? string.Empty,
                promptId = result?.promptId ?? string.Empty,
                error = error ?? result?.error ?? string.Empty,
                verificationState = verificationState ?? "pending",
                utcTicks = DateTime.UtcNow.Ticks,
            };
        }

        private static string ResolveVerificationState(ESAITestNaturalLanguageExecutionResultDto result)
        {
            if (result == null || !result.accepted)
                return "rejected";
            if (string.Equals(result.intent, ESAITestNaturalLanguageIntent.PublishPrompt.ToString(), StringComparison.Ordinal))
            {
                ESAITestAIPromptDto consumed = ESAITestObservationRuntimeState.LastConsumedPrompt;
                return consumed != null && string.Equals(consumed.promptId, result.promptId, StringComparison.Ordinal)
                    ? "prompt_consumed"
                    : "pending_prompt_consumption";
            }
            if (string.Equals(result.intent, ESAITestNaturalLanguageIntent.QueryStatus.ToString(), StringComparison.Ordinal)
                || string.Equals(result.intent, ESAITestNaturalLanguageIntent.PrepareAutonomyExternalAi.ToString(), StringComparison.Ordinal))
                return "verified";
            return result.runnerRunning ? "runner_active_business_verify_pending" : "execution_complete_verify_pending";
        }

        private void WriteReceipt(ESAITestConversationReceiptDto receipt)
        {
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.requestId))
                return;
            ESAITestConversationRuntimeState.LastReceipt = receipt;
            string fileName = SanitizeSegment(receipt.requestId) + "." + receipt.stage + ".json";
            WriteAtomically(Path.Combine(receiptDirectory, fileName), JsonUtility.ToJson(receipt, true));
        }

        private void MoveToDisposition(string source, string disposition)
        {
            if (!File.Exists(source))
                return;
            Directory.CreateDirectory(processedDirectory);
            string destination = Path.Combine(processedDirectory,
                Path.GetFileNameWithoutExtension(source) + "." + disposition + ".json");
            if (File.Exists(destination))
                destination += "." + DateTime.UtcNow.Ticks;
            File.Move(source, destination);
        }

        private static void WriteAtomically(string finalPath, string content)
        {
            string directory = Path.GetDirectoryName(finalPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("对话 IPC 回执目录无效。");
            Directory.CreateDirectory(directory);
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content ?? string.Empty, new UTF8Encoding(false));
                if (File.Exists(finalPath))
                    File.Replace(temporaryPath, finalPath, null);
                else
                    File.Move(temporaryPath, finalPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static string SanitizeSegment(string value)
        {
            string result = value ?? "unknown-request";
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return string.IsNullOrEmpty(result) ? "unknown-request" : result;
        }
    }

    public static class ESAITestConversationIpcBootstrap
    {
        private static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (host != null)
                return;
            host = new GameObject("ESAITest Conversation IPC");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ESAITestConversationIpc>();
        }
    }
}
