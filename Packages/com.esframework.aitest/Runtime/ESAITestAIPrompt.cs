using System;
using System.Collections.Generic;
using UnityEngine;

namespace ESFramework.ESAITest
{
    public enum ESAITestAIPromptPriority
    {
        P0 = 0,
        P1 = 1,
        P2 = 2,
        P3 = 3,
        P4 = 4,
    }

    [Serializable]
    public sealed class ESAITestAIPromptDto
    {
        public string promptId;
        public string source;
        public string message;
        public string priority;
        public long sequence;
        public long publishedUtcTicks;
        public long expiresUtcTicks;
    }

    [Serializable]
    public sealed class ESAITestAIPromptPublishResultDto
    {
        public const string Schema = "esaitest.prompt-publish/v1";

        public string schema = Schema;
        public string runId;
        public int sceneGeneration;
        public string invocationId;
        public string stepId;
        public string promptId;
        public string source;
        public string priority;
        public float timeToLiveSeconds;
        public int pendingCount;
        public long sequence;
        public long publishedUtcTicks;
        public long expiresUtcTicks;
        public string evictedPromptId;
        public string evictedPromptPriority;
    }

    /// <summary>
    /// 游戏运行时向 AI 投递一次性提示的有界收件箱。最高 P 等级优先，同级按投递顺序消费。
    /// </summary>
    public static class ESAITestAIPrompt
    {
        public const int MaxPendingCount = 64;
        private const float DefaultTimeToLiveSeconds = 60f;
        private static readonly object SyncRoot = new object();
        private static readonly List<ESAITestAIPromptEntry> Pending = new List<ESAITestAIPromptEntry>(MaxPendingCount);
        private static long nextSequence;

        public static int PendingCount
        {
            get
            {
                lock (SyncRoot)
                {
                    RemoveExpired(DateTime.UtcNow.Ticks);
                    return Pending.Count;
                }
            }
        }

        public static string HighestPendingPriority
        {
            get
            {
                lock (SyncRoot)
                {
                    RemoveExpired(DateTime.UtcNow.Ticks);
                    int index = FindMostImportantIndex();
                    return index < 0 ? string.Empty : Pending[index].priority.ToString();
                }
            }
        }

        public static string Publish(
            string message,
            ESAITestAIPromptPriority priority = ESAITestAIPromptPriority.P2,
            string source = "game",
            float timeToLiveSeconds = DefaultTimeToLiveSeconds)
        {
            return Publish(message, priority, source, timeToLiveSeconds, out _, out _);
        }

        public static string Publish(
            string message,
            ESAITestAIPromptPriority priority,
            string source,
            float timeToLiveSeconds,
            out ESAITestAIPromptDto publishedPrompt,
            out ESAITestAIPromptDto evictedPrompt)
        {
            publishedPrompt = null;
            evictedPrompt = null;
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("AI 提示内容不能为空。", nameof(message));
            if (!Enum.IsDefined(typeof(ESAITestAIPromptPriority), priority))
                throw new ArgumentOutOfRangeException(nameof(priority));
            if (float.IsNaN(timeToLiveSeconds) || float.IsInfinity(timeToLiveSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeToLiveSeconds), "AI 提示 TTL 必须是有限秒数。");

            string normalizedMessage = message.Trim();
            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "game" : source.Trim();
            if (normalizedMessage.Length > ESAITestProtocol.MaxTextLength)
                throw new ArgumentOutOfRangeException(nameof(message), "AI 提示内容超过协议长度限制。");
            if (normalizedSource.Length > ESAITestProtocol.MaxIdentityLength)
                throw new ArgumentOutOfRangeException(nameof(source), "AI 提示来源超过协议长度限制。");

            long now = DateTime.UtcNow.Ticks;
            long ttlTicks = TimeSpan.FromSeconds(Mathf.Clamp(timeToLiveSeconds, 1f, 3600f)).Ticks;
            lock (SyncRoot)
            {
                RemoveExpired(now);
                var entry = new ESAITestAIPromptEntry
                {
                    promptId = Guid.NewGuid().ToString("N"),
                    source = normalizedSource,
                    message = normalizedMessage,
                    priority = priority,
                    sequence = ++nextSequence,
                    publishedUtcTicks = now,
                    expiresUtcTicks = now + ttlTicks,
                };

                if (Pending.Count >= MaxPendingCount)
                {
                    ESAITestAIPromptEntry evicted = Pending[FindLeastImportantIndex()];
                    Pending.Remove(evicted);
                    evictedPrompt = evicted.ToDto();
                }
                Pending.Add(entry);
                publishedPrompt = entry.ToDto();
                return entry.promptId;
            }
        }

        public static bool TryConsume(out ESAITestAIPromptDto prompt)
        {
            lock (SyncRoot)
            {
                RemoveExpired(DateTime.UtcNow.Ticks);
                int index = FindMostImportantIndex();
                if (index < 0)
                {
                    prompt = null;
                    return false;
                }

                ESAITestAIPromptEntry entry = Pending[index];
                Pending.RemoveAt(index);
                prompt = entry.ToDto();
                return true;
            }
        }

        public static bool TryPeek(out ESAITestAIPromptDto prompt)
        {
            lock (SyncRoot)
            {
                RemoveExpired(DateTime.UtcNow.Ticks);
                int index = FindMostImportantIndex();
                prompt = index < 0 ? null : Pending[index].ToDto();
                return prompt != null;
            }
        }

        private static int FindMostImportantIndex()
        {
            int winner = -1;
            for (int i = 0; i < Pending.Count; i++)
            {
                if (winner < 0
                    || Pending[i].priority < Pending[winner].priority
                    || (Pending[i].priority == Pending[winner].priority && Pending[i].sequence < Pending[winner].sequence))
                    winner = i;
            }
            return winner;
        }

        private static int FindLeastImportantIndex()
        {
            int loser = 0;
            for (int i = 1; i < Pending.Count; i++)
            {
                if (Pending[i].priority > Pending[loser].priority
                    || (Pending[i].priority == Pending[loser].priority && Pending[i].sequence < Pending[loser].sequence))
                    loser = i;
            }
            return loser;
        }

        private static void RemoveExpired(long nowUtcTicks)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
                if (Pending[i].expiresUtcTicks <= nowUtcTicks)
                    Pending.RemoveAt(i);
        }

        private sealed class ESAITestAIPromptEntry
        {
            public string promptId;
            public string source;
            public string message;
            public ESAITestAIPromptPriority priority;
            public long sequence;
            public long publishedUtcTicks;
            public long expiresUtcTicks;

            public ESAITestAIPromptDto ToDto()
            {
                return new ESAITestAIPromptDto
                {
                    promptId = promptId,
                    source = source,
                    message = message,
                    priority = priority.ToString(),
                    sequence = sequence,
                    publishedUtcTicks = publishedUtcTicks,
                    expiresUtcTicks = expiresUtcTicks,
                };
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ESAITestAIPromptCapabilityProvider : MonoBehaviour, ESAITestCapabilityProvider
    {
        private const string Capability = "unity.prompt";

        public string CapabilityId => Capability;
        public string ProviderId => "esframework.aitest.prompt";
        public int ProviderVersion => 1;
        public string[] Commands => new[] { "prompt.publish" };

        private void OnEnable()
        {
            ESAITestRuntime.Activated += Register;
            ESAITestRuntime.SceneGenerationChanged += Register;
            Register();
        }

        private void OnDisable()
        {
            ESAITestRuntime.Activated -= Register;
            ESAITestRuntime.SceneGenerationChanged -= Register;
            ESAITestRuntime.Registry?.Unregister(this);
        }

        public ESAITestCapabilityResponseDto Execute(ESAITestCapabilityRequestDto request)
        {
            if (request == null
                || !string.Equals(request.operation, ESAITestProtocol.OperationAct, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(request.command, "prompt.publish", StringComparison.OrdinalIgnoreCase))
                return ESAITestCapabilityResponseDto.Reject(
                    ESAITestStatusCode.CapabilityRejected,
                    "AI 模拟 Publish 仅允许 act + prompt.publish。");

            string message = FindArgument(request.arguments, "message");
            if (string.IsNullOrWhiteSpace(message)) message = request.target;
            if (string.IsNullOrWhiteSpace(message)) message = request.expectedValue;
            if (string.IsNullOrWhiteSpace(message))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "prompt.publish 缺少 message。");

            string priorityText = FindArgument(request.arguments, "priority") ?? ESAITestAIPromptPriority.P2.ToString();
            if (!Enum.TryParse(priorityText, true, out ESAITestAIPromptPriority priority)
                || !Enum.IsDefined(typeof(ESAITestAIPromptPriority), priority))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "priority 必须是 P0、P1、P2、P3 或 P4。");

            string ttlText = FindArgument(request.arguments, "ttlSeconds");
            float ttlSeconds = 60f;
            if (!string.IsNullOrWhiteSpace(ttlText)
                && !float.TryParse(
                    ttlText,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out ttlSeconds))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "ttlSeconds 不是有效数字。");
            if (float.IsNaN(ttlSeconds) || float.IsInfinity(ttlSeconds))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "ttlSeconds 必须是有限数字。");
            ttlSeconds = Mathf.Clamp(ttlSeconds, 1f, 3600f);

            string requestedSource = FindArgument(request.arguments, "source");
            string source = "ai.simulated/" + (string.IsNullOrWhiteSpace(requestedSource)
                ? (string.IsNullOrWhiteSpace(request.stepId) ? "estest" : request.stepId.Trim())
                : requestedSource.Trim());

            try
            {
                string promptId = ESAITestAIPrompt.Publish(message, priority, source, ttlSeconds,
                    out ESAITestAIPromptDto published, out ESAITestAIPromptDto evicted);
                var result = new ESAITestAIPromptPublishResultDto
                {
                    runId = request.runId,
                    sceneGeneration = request.sceneGeneration,
                    invocationId = request.invocationId,
                    stepId = request.stepId,
                    promptId = promptId,
                    source = source,
                    priority = priority.ToString(),
                    timeToLiveSeconds = ttlSeconds,
                    pendingCount = ESAITestAIPrompt.PendingCount,
                    sequence = published?.sequence ?? 0L,
                    publishedUtcTicks = published?.publishedUtcTicks ?? 0L,
                    expiresUtcTicks = published?.expiresUtcTicks ?? 0L,
                    evictedPromptId = evicted?.promptId ?? string.Empty,
                    evictedPromptPriority = evicted?.priority ?? string.Empty,
                };
                string publishMessage = "AI 已模拟 Publish：" + priority + "，promptId=" + promptId;
                if (evicted != null)
                    publishMessage += "，容量已淘汰较低优先级提示=" + evicted.promptId;
                return new ESAITestCapabilityResponseDto
                {
                    accepted = true,
                    conditionMet = true,
                    retryable = false,
                    statusCode = ESAITestStatusCode.Passed,
                    message = publishMessage,
                    value = ESAITestValueDto.FromString(JsonUtility.ToJson(result)),
                };
            }
            catch (ArgumentException exception)
            {
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, exception.Message);
            }
            catch (Exception exception)
            {
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InternalError, exception.ToString());
            }
        }

        private void Register()
        {
            if (!isActiveAndEnabled || !ESAITestRuntime.IsActive || ESAITestRuntime.Registry == null)
                return;
            if (!ESAITestRuntime.Registry.Register(this, ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration, out string error))
                Debug.LogError("[ESAITest] AI Prompt Capability 注册失败：" + error, this);
        }

        private static string FindArgument(ESAITestArgumentDto[] arguments, string key)
        {
            if (arguments == null)
                return null;
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] != null && string.Equals(arguments[i].key, key, StringComparison.OrdinalIgnoreCase))
                    return arguments[i].value;
            return null;
        }
    }
}
