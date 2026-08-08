using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace ESFramework.ESAITest
{
    public enum ESAITestNaturalLanguageIntent
    {
        Unknown = 0,
        StartESTEST = 1,
        StartAutonomy = 2,
        StartAutonomyWithExternalAi = 3,
        StartAutonomyUsingExistingAi = 4,
        PrepareAutonomyExternalAi = 5,
        PublishPrompt = 6,
        Cancel = 7,
        QueryStatus = 8,
    }

    [Serializable]
    public sealed class ESAITestNaturalLanguageRouteDto
    {
        public const string Schema = "esaitest.natural-language-route/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public bool accepted;
        public bool requiresClarification;
        public string intent;
        public string normalizedText;
        public string boundRunId;
        public string message;
        public string goal;
        public string priority = ESAITestAIPromptPriority.P2.ToString();
        public float ttlSeconds = 60f;
        public float confidence;
        public string rejectionReason;
    }

    [Serializable]
    public sealed class ESAITestNaturalLanguageExecutionResultDto
    {
        public const string Schema = "esaitest.natural-language-execution/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public bool accepted;
        public string intent;
        public float confidence;
        public string normalizedText;
        public string parsedMessage;
        public string parsedGoal;
        public string parsedPriority;
        public float parsedTtlSeconds;
        public string boundRunId;
        public string rejectionReason;
        public string message;
        public string error;
        public string statusCode;
        public string requestId;
        public bool requestIdPersisted;
        public string runId;
        public string promptId;
        public string preparationPath;
        public bool runnerRunning;
    }

    /// <summary>
    /// 安全的自然语言门面。它只识别有限意图，并把结果转成已有授权 API；未知、冲突或缺参
    /// 文本永远不会直接变成输入、脚本、路径或 Unity API 调用。
    /// </summary>
    public static class ESAITestNaturalLanguageRouter
    {
        private const int MaximumInputLength = ESAITestProtocol.MaxTextLength;
        private const int MaximumRememberedRequestIds = 256;
        private const long RequestIdRetentionTicks = 24L * TimeSpan.TicksPerHour;
        private static readonly object ExecutionSyncRoot = new object();
        private static readonly HashSet<string> CompletedRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Queue<string> CompletedRequestOrder = new Queue<string>(MaximumRememberedRequestIds);
        private static readonly Dictionary<string, long> CompletedRequestTimes = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Mutex RequestLedgerMutex = new Mutex(false, "ESFramework.ESAITest.NaturalLanguageLedger");
        private static bool requestLedgerLoaded;
        private const string RequestLedgerRelativePath = "ESAITest/natural-language/request-ledger.json";

        [Serializable]
        private sealed class RequestLedgerDto
        {
            public string[] requestIds = Array.Empty<string>();
            public long[] completedUtcTicks = Array.Empty<long>();
        }
        private static readonly Regex PriorityPattern = new Regex(
            @"(?<![A-Za-z0-9])P([0-4])(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex TtlPattern = new Regex(
            @"(?:ttl|有效期|保留)\s*[:=：]?\s*([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(string input, out ESAITestNaturalLanguageRouteDto route, out string error)
        {
            route = new ESAITestNaturalLanguageRouteDto();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
                return Reject(route, "自然语言请求不能为空。", out error);

            string normalized = Normalize(input);
            route.normalizedText = normalized;
            ESAITestRunner currentRunner = ESAITestPlayerBootstrap.ActiveRunner;
            route.boundRunId = currentRunner != null && currentRunner.IsRunning ? currentRunner.RunId : string.Empty;
            if (normalized.Length > MaximumInputLength)
                return Reject(route, "自然语言请求超过长度上限。", out error);
            if (IsDiscussionOnly(normalized))
                return RejectClarification(route, "这是解释/讨论请求，不是可执行动作；请明确说出要执行的一个动作。", out error);

            bool publish = IsPublishIntent(normalized);
            bool cancel = IsCancelIntent(normalized);
            bool query = IsQueryIntent(normalized);
            bool startEstest = IsStartESTESTIntent(normalized);
            bool autonomy = IsAutonomyIntent(normalized);
            int intentCount = (publish ? 1 : 0) + (cancel ? 1 : 0) + (query ? 1 : 0)
                + (startEstest ? 1 : 0) + (autonomy ? 1 : 0);
            if (intentCount == 0)
            {
                if (TryParseAdaptiveConversation(normalized, route, out error))
                    return true;
                if (route.requiresClarification && !string.IsNullOrWhiteSpace(error))
                {
                    route.accepted = false;
                    route.rejectionReason = error;
                    return false;
                }
                return Reject(route, "未识别到受支持意图；只能执行启动、有限自主、Publish、取消或状态查询。", out error);
            }
            if (intentCount > 1)
                return RejectClarification(route, "请求包含多个动作意图，必须拆成一次一个动作。", out error);

            if (publish)
                return ParsePublish(normalized, route, out error);
            if (cancel)
            {
                route.intent = ESAITestNaturalLanguageIntent.Cancel.ToString();
                route.confidence = 0.99f;
                return Accept(route);
            }
            if (query)
            {
                route.intent = ESAITestNaturalLanguageIntent.QueryStatus.ToString();
                route.confidence = 0.98f;
                return Accept(route);
            }
            if (startEstest)
            {
                route.intent = ESAITestNaturalLanguageIntent.StartESTEST.ToString();
                route.confidence = 0.96f;
                return Accept(route);
            }

            return ParseAutonomy(normalized, route, out error);
        }

        public static bool TryExecute(
            string input,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            return TryExecute(input, null, out result, out error);
        }

        public static bool TryExecute(
            string input,
            string requestId,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            lock (ExecutionSyncRoot)
                return TryExecuteCore(input, requestId, out result, out error);
        }

        private static bool TryExecuteCore(
            string input,
            string requestId,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            result = new ESAITestNaturalLanguageExecutionResultDto();
            requestId = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId.Trim();
            result.requestId = requestId;
            EnsureRequestLedgerLoaded();
            if (!string.IsNullOrWhiteSpace(requestId) && requestId.Length > ESAITestProtocol.MaxIdentityLength)
            {
                error = "自然语言 requestId 超过长度限制。";
                result.statusCode = "natural_language_invalid_request";
                result.error = error;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(requestId) && CompletedRequestIds.Contains(requestId))
            {
                error = "自然语言请求已处理，拒绝重复执行。";
                result.statusCode = "natural_language_duplicate";
                result.error = error;
                return false;
            }
            if (!TryParse(input, out ESAITestNaturalLanguageRouteDto route, out error))
            {
                result.accepted = false;
                result.statusCode = route != null && route.requiresClarification
                    ? "natural_language_clarification_required"
                    : "natural_language_rejected";
                result.error = error;
                result.message = route?.rejectionReason ?? error;
                result.intent = route?.intent ?? string.Empty;
                result.confidence = route?.confidence ?? 0f;
                CopyRouteToResult(route, result);
                return false;
            }

            CopyRouteToResult(route, result);

            if (!string.IsNullOrWhiteSpace(route.boundRunId))
            {
                ESAITestRunner boundRunner = ESAITestPlayerBootstrap.ActiveRunner;
                if (boundRunner == null || !boundRunner.IsRunning
                    || !string.Equals(boundRunner.RunId, route.boundRunId, StringComparison.Ordinal))
                {
                    error = "自然语言请求绑定的 Run 已变化，拒绝执行以防止串 Run。";
                    result.statusCode = "natural_language_run_conflict";
                    result.error = error;
                    return false;
                }
            }

            return ExecuteParsedRoute(route, requestId, result, out error);
        }

        public static bool TryExecuteInterpretedRoute(
            ESAITestNaturalLanguageRouteDto route,
            string requestId,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            lock (ExecutionSyncRoot)
            {
                result = new ESAITestNaturalLanguageExecutionResultDto();
                requestId = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId.Trim();
                result.requestId = requestId;
                EnsureRequestLedgerLoaded();
                if (!string.IsNullOrWhiteSpace(requestId) && requestId.Length > ESAITestProtocol.MaxIdentityLength)
                    return RejectInterpretedRoute(result, "自然语言 requestId 超过长度限制。", "natural_language_invalid_request", out error);
                if (!string.IsNullOrWhiteSpace(requestId) && CompletedRequestIds.Contains(requestId))
                    return RejectInterpretedRoute(result, "自然语言请求已处理，拒绝重复执行。", "natural_language_duplicate", out error);
                if (!ValidateInterpretedRoute(route, out error))
                    return RejectInterpretedRoute(result, error, route != null && route.requiresClarification
                        ? "natural_language_clarification_required"
                        : "natural_language_rejected", out error);
                CopyRouteToResult(route, result);
                if (!string.IsNullOrWhiteSpace(route.boundRunId))
                {
                    ESAITestRunner boundRunner = ESAITestPlayerBootstrap.ActiveRunner;
                    if (boundRunner == null || !boundRunner.IsRunning
                        || !string.Equals(boundRunner.RunId, route.boundRunId, StringComparison.Ordinal))
                        return RejectInterpretedRoute(result, "自然语言请求绑定的 Run 已变化，拒绝执行以防止串 Run。", "natural_language_run_conflict", out error);
                }
                return ExecuteParsedRoute(route, requestId, result, out error);
            }
        }

        private static bool ValidateInterpretedRoute(ESAITestNaturalLanguageRouteDto route, out string error)
        {
            error = string.Empty;
            if (route == null || !route.accepted || route.requiresClarification
                || route.protocolVersion != ESAITestProtocol.CurrentVersion
                || !string.Equals(route.schema, ESAITestNaturalLanguageRouteDto.Schema, StringComparison.Ordinal)
                || !Enum.TryParse(route.intent, false, out ESAITestNaturalLanguageIntent intent)
                || intent == ESAITestNaturalLanguageIntent.Unknown
                || !IsFinite(route.confidence) || route.confidence < 0.75f
                || (route.normalizedText != null && route.normalizedText.Length > MaximumInputLength))
            {
                error = "LLM 意图信封的 schema、置信度或意图不符合授权边界。";
                return false;
            }
            if (intent == ESAITestNaturalLanguageIntent.PublishPrompt)
            {
                if (string.IsNullOrWhiteSpace(route.message) || route.message.Length > ESAITestProtocol.MaxTextLength
                    || !Enum.TryParse(route.priority, true, out ESAITestAIPromptPriority priority)
                    || !Enum.IsDefined(typeof(ESAITestAIPromptPriority), priority)
                    || !IsFinite(route.ttlSeconds) || route.ttlSeconds < 1f || route.ttlSeconds > 3600f)
                {
                    error = "LLM Publish 意图的 message、P 等级或 TTL 不符合边界。";
                    return false;
                }
            }
            if (intent == ESAITestNaturalLanguageIntent.StartAutonomy
                || intent == ESAITestNaturalLanguageIntent.StartAutonomyWithExternalAi
                || intent == ESAITestNaturalLanguageIntent.StartAutonomyUsingExistingAi
                || intent == ESAITestNaturalLanguageIntent.PrepareAutonomyExternalAi)
            {
                if (string.IsNullOrWhiteSpace(route.goal) || route.goal.Length > ESAITestProtocol.MaxTextLength)
                {
                    error = "LLM 自主意图必须包含合法 goal。";
                    return false;
                }
            }
            return true;
        }

        private static bool RejectInterpretedRoute(
            ESAITestNaturalLanguageExecutionResultDto result,
            string reason,
            string statusCode,
            out string error)
        {
            error = reason ?? string.Empty;
            result.accepted = false;
            result.statusCode = statusCode;
            result.error = error;
            result.message = error;
            result.rejectionReason = error;
            return false;
        }

        private static bool ExecuteParsedRoute(
            ESAITestNaturalLanguageRouteDto route,
            string requestId,
            ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            result.intent = route.intent;
            result.confidence = route.confidence;
            bool succeeded;
            switch ((ESAITestNaturalLanguageIntent)Enum.Parse(
                typeof(ESAITestNaturalLanguageIntent), route.intent, false))
            {
                case ESAITestNaturalLanguageIntent.StartESTEST:
                    succeeded = ESAITestPlayerBootstrap.TryStartESTEST(out error);
                    break;
                case ESAITestNaturalLanguageIntent.StartAutonomy:
                    succeeded = ESAITestPlayerBootstrap.TryStartAutonomy(route.goal, out error);
                    break;
                case ESAITestNaturalLanguageIntent.StartAutonomyWithExternalAi:
                    succeeded = ESAITestPlayerBootstrap.TryStartAutonomyWithExternalAi(route.goal, out error);
                    break;
                case ESAITestNaturalLanguageIntent.StartAutonomyUsingExistingAi:
                    succeeded = ESAITestPlayerBootstrap.TryStartAutonomyUsingExistingAi(route.goal, out error);
                    break;
                case ESAITestNaturalLanguageIntent.PrepareAutonomyExternalAi:
                    succeeded = ESAITestPlayerBootstrap.TryPrepareAutonomyExternalAi(
                        route.goal, out result.preparationPath, out error);
                    break;
                case ESAITestNaturalLanguageIntent.PublishPrompt:
                    try
                    {
                        result.promptId = ESAITestAIPrompt.Publish(
                            route.message, ParsePriority(route.priority), "natural-language", route.ttlSeconds);
                        error = string.Empty;
                        succeeded = true;
                    }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                        succeeded = false;
                    }
                    break;
                case ESAITestNaturalLanguageIntent.Cancel:
                    succeeded = ESAITestPlayerBootstrap.RequestCancel();
                    error = succeeded ? string.Empty : "当前没有可取消的 ESAITest Runner。";
                    break;
                case ESAITestNaturalLanguageIntent.QueryStatus:
                    succeeded = true;
                    error = string.Empty;
                    break;
                default:
                    succeeded = false;
                    error = "路由意图不在授权白名单中。";
                    break;
            }
            result.accepted = succeeded;
            result.statusCode = succeeded ? "passed" : "natural_language_execution_failed";
            result.error = succeeded ? string.Empty : error;
            result.message = succeeded ? BuildSuccessMessage(route, result) : error;
            result.runnerRunning = ESAITestPlayerBootstrap.ActiveRunner != null
                && ESAITestPlayerBootstrap.ActiveRunner.IsRunning;
            if (result.runnerRunning)
                result.runId = ESAITestPlayerBootstrap.ActiveRunner.RunId;
            if (succeeded && !string.IsNullOrWhiteSpace(requestId))
            {
                result.requestIdPersisted = RememberRequestId(requestId);
                if (!result.requestIdPersisted)
                    result.statusCode = "passed_idempotency_persistence_failed";
            }
            return succeeded;
        }

        private static bool ParsePublish(string text, ESAITestNaturalLanguageRouteDto route, out string error)
        {
            string message = RemoveRoutingMetadata(ExtractPayload(text));
            if (string.IsNullOrWhiteSpace(message))
                return RejectClarification(route, "Publish 必须明确提供要发送给测试 AI 的消息。", out error);
            route.intent = ESAITestNaturalLanguageIntent.PublishPrompt.ToString();
            route.message = message;
            route.confidence = 0.97f;
            if (!TryParsePriority(text, out route.priority, out error))
                return false;
            if (!TryParseTtl(text, out route.ttlSeconds, out error))
                return false;
            return Accept(route);
        }

        private static string RemoveRoutingMetadata(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;
            string cleaned = PriorityPattern.Replace(message, " ");
            cleaned = TtlPattern.Replace(cleaned, " ");
            return Normalize(cleaned).Trim(' ', '，', ',', '。', '.', ';', '；');
        }

        private static bool ParseAutonomy(string text, ESAITestNaturalLanguageRouteDto route, out string error)
        {
            error = string.Empty;
            bool prepare = ContainsAny(text, "仅准备", "只准备", "暂不启动", "不要启动", "不立即开始");
            bool reuse = ContainsAny(text, "复用已有", "使用已有", "使用现有", "已有ai", "已有 ai");
            bool external = ContainsAny(text, "自动拉起", "启动外部ai", "启动外部 ai", "新建外部ai", "新建外部 ai");
            if (prepare && (reuse || external))
                return RejectClarification(route, "仅准备不能同时要求复用或自动拉起外部 AI。", out error);
            if (reuse && external)
                return RejectClarification(route, "不能同时要求复用已有 AI 和自动拉起新 Agent。", out error);

            string goal = ExtractPayload(text);
            if (string.IsNullOrWhiteSpace(goal))
                return RejectClarification(route, "自主操作必须明确提供 goal，例如“启动自主测试：完成当前关卡”。", out error);
            route.goal = goal;
            route.confidence = 0.94f;
            if (prepare)
                route.intent = ESAITestNaturalLanguageIntent.PrepareAutonomyExternalAi.ToString();
            else if (reuse)
                route.intent = ESAITestNaturalLanguageIntent.StartAutonomyUsingExistingAi.ToString();
            else if (external)
                route.intent = ESAITestNaturalLanguageIntent.StartAutonomyWithExternalAi.ToString();
            else
                route.intent = ESAITestNaturalLanguageIntent.StartAutonomy.ToString();
            return Accept(route);
        }

        private static bool TryParseAdaptiveConversation(
            string text,
            ESAITestNaturalLanguageRouteDto route,
            out string error)
        {
            error = string.Empty;
            bool conversationalDrive = ContainsAny(
                text,
                "帮我玩",
                "替我玩",
                "继续推进",
                "推进游戏",
                "让测试ai继续",
                "让测试 ai 继续",
                "继续测试");
            if (!conversationalDrive)
            {
                error = "未识别到对话式驱动意图。";
                return false;
            }

            ESAITestRunner activeRunner = ESAITestPlayerBootstrap.ActiveRunner;
            if (activeRunner != null && activeRunner.IsRunning)
            {
                route.intent = ESAITestNaturalLanguageIntent.PublishPrompt.ToString();
                route.message = text;
                route.priority = ESAITestAIPromptPriority.P2.ToString();
                route.ttlSeconds = 60f;
                route.confidence = 0.86f;
                return Accept(route);
            }

            if (!ESAITestAutonomyExternalBridgeEnvironment.TryResolve(out _, out string resolveError))
            {
                error = "当前没有运行中的测试 AI，且未找到通过哈希校验的受信外部 Agent；" + resolveError;
                route.requiresClarification = true;
                route.rejectionReason = error;
                return false;
            }

            route.intent = ESAITestNaturalLanguageIntent.StartAutonomyWithExternalAi.ToString();
            route.goal = text;
            route.confidence = 0.84f;
            return Accept(route);
        }

        private static bool TryParsePriority(string text, out string priority, out string error)
        {
            priority = ESAITestAIPromptPriority.P2.ToString();
            error = string.Empty;
            MatchCollection matches = PriorityPattern.Matches(text);
            if (matches.Count > 1)
                return RejectValue("Publish 请求包含多个优先级。", out error);
            if (matches.Count == 1)
                priority = "P" + matches[0].Groups[1].Value;
            return true;
        }

        private static bool TryParseTtl(string text, out float ttl, out string error)
        {
            ttl = 60f;
            error = string.Empty;
            MatchCollection matches = TtlPattern.Matches(text);
            if (matches.Count > 1)
                return RejectValue("Publish 请求包含多个 TTL。", out error);
            if (matches.Count == 0)
                return true;
            if (!float.TryParse(matches[0].Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out ttl)
                || float.IsNaN(ttl) || float.IsInfinity(ttl)
                || ttl < 1f || ttl > 3600f)
                return RejectValue("TTL 必须是 1 到 3600 秒。", out error);
            return true;
        }

        private static ESAITestAIPromptPriority ParsePriority(string value)
        {
            return (ESAITestAIPromptPriority)Enum.Parse(typeof(ESAITestAIPromptPriority), value, true);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string BuildSuccessMessage(
            ESAITestNaturalLanguageRouteDto route,
            ESAITestNaturalLanguageExecutionResultDto result)
        {
            switch ((ESAITestNaturalLanguageIntent)Enum.Parse(
                typeof(ESAITestNaturalLanguageIntent), route.intent, false))
            {
                case ESAITestNaturalLanguageIntent.PublishPrompt:
                    return "已安全 Publish 给测试 AI，promptId=" + result.promptId;
                case ESAITestNaturalLanguageIntent.PrepareAutonomyExternalAi:
                    return "已创建测试 AI 准备记录，未启动任何 Runner 或进程。";
                case ESAITestNaturalLanguageIntent.QueryStatus:
                    return result.runnerRunning ? "当前 ESAITest Runner 正在运行。" : "当前没有运行中的 ESAITest Runner。";
                default:
                    return "已通过自然语言路由执行：" + route.intent;
            }
        }

        private static bool IsPublishIntent(string text)
        {
            return ContainsAny(text, "publish", "告诉测试ai", "告诉测试 ai", "提示测试ai", "提示测试 ai", "通知测试ai", "通知测试 ai")
                || (ContainsAny(text, "发布") && ContainsAny(text, "测试ai", "测试 ai", "ai"));
        }

        private static bool IsDiscussionOnly(string text)
        {
            if (ContainsAny(text, "怎么启动", "如何启动", "怎么开始", "如何开始", "怎么运行", "如何运行")
                && !ContainsAny(text, "请执行", "直接启动", "立即启动", "马上启动", "帮我启动"))
                return true;
            return ContainsAny(text, "解释", "什么意思", "为什么", "如何实现", "怎么实现", "怎么写", "代码", "文档", "风险", "评估", "能否", "是否可以")
                && !ContainsAny(text, "请执行", "直接执行", "马上", "立即", "开始", "启动", "告诉测试ai", "告诉测试 ai");
        }

        private static bool IsCancelIntent(string text)
        {
            return ContainsAny(text, "取消", "中断", "停止")
                && ContainsAny(text, "estest", "测试", "自主", "ai");
        }

        private static bool IsQueryIntent(string text)
        {
            return ContainsAny(text, "查看状态", "查询状态", "当前状态", "运行状态", "运行报告", "测试报告");
        }

        private static bool IsStartESTESTIntent(string text)
        {
            return ContainsAny(text, "直接启动estest", "启动estest", "开始estest", "运行estest", "启动测试", "开始测试", "运行测试",
                "启动ai测试", "启动 ai 测试", "开始ai测试", "开始 ai 测试");
        }

        private static bool IsAutonomyIntent(string text)
        {
            return ContainsAny(text, "自主", "自己玩", "自动玩", "自主测试", "自动拉起外部ai", "自动拉起外部 ai",
                "启动外部ai", "启动外部 ai", "新建外部ai", "新建外部 ai", "复用已有ai", "复用已有 ai",
                "使用已有ai", "使用已有 ai", "使用现有ai", "使用现有 ai", "仅准备测试ai", "仅准备测试 ai",
                "仅准备外部ai", "仅准备外部 ai");
        }

        private static string ExtractPayload(string text)
        {
            int separator = text.IndexOf('：');
            if (separator < 0)
                separator = text.IndexOf(':');
            if (separator >= 0 && separator + 1 < text.Length)
                return text.Substring(separator + 1).Trim();

            string result = text;
            string[] prefixes =
            {
                "告诉测试ai", "告诉测试 ai", "提示测试ai", "提示测试 ai", "通知测试ai", "通知测试 ai",
                "发布", "启动自主测试", "开始自主测试", "启动自主", "开始自主", "自动玩", "自己玩",
                "仅准备测试ai", "只准备测试ai", "复用已有ai", "复用已有 ai", "启动外部ai", "启动外部 ai"
            };
            for (int i = 0; i < prefixes.Length; i++)
                if (result.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    result = result.Substring(prefixes[i].Length).Trim(' ', '，', ',', '。', '.');
            return result;
        }

        private static string Normalize(string input)
        {
            var builder = new StringBuilder(input.Length);
            bool previousWhitespace = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i] == '\r' || input[i] == '\n' || input[i] == '\t' ? ' ' : input[i];
                if (char.IsWhiteSpace(c))
                {
                    if (previousWhitespace)
                        continue;
                    previousWhitespace = true;
                    builder.Append(' ');
                }
                else
                {
                    previousWhitespace = false;
                    builder.Append(c);
                }
            }
            return builder.ToString().Trim();
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (text.IndexOf(values[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static bool Accept(ESAITestNaturalLanguageRouteDto route)
        {
            route.accepted = true;
            route.requiresClarification = false;
            route.rejectionReason = string.Empty;
            return true;
        }

        private static bool RememberRequestId(string requestId)
        {
            if (CompletedRequestIds.Add(requestId))
            {
                CompletedRequestOrder.Enqueue(requestId);
                CompletedRequestTimes[requestId] = DateTime.UtcNow.Ticks;
                while (CompletedRequestOrder.Count > MaximumRememberedRequestIds)
                {
                    string evicted = CompletedRequestOrder.Dequeue();
                    CompletedRequestIds.Remove(evicted);
                    CompletedRequestTimes.Remove(evicted);
                }
                return PersistRequestLedger();
            }
            return true;
        }

        private static void CopyRouteToResult(
            ESAITestNaturalLanguageRouteDto route,
            ESAITestNaturalLanguageExecutionResultDto result)
        {
            if (route == null || result == null)
                return;
            result.normalizedText = route.normalizedText ?? string.Empty;
            result.parsedMessage = route.message ?? string.Empty;
            result.parsedGoal = route.goal ?? string.Empty;
            result.parsedPriority = route.priority ?? string.Empty;
            result.parsedTtlSeconds = route.ttlSeconds;
            result.boundRunId = route.boundRunId ?? string.Empty;
            result.rejectionReason = route.rejectionReason ?? string.Empty;
        }

        private static void EnsureRequestLedgerLoaded()
        {
            if (requestLedgerLoaded)
                return;
            string path = Path.Combine(Application.persistentDataPath, RequestLedgerRelativePath);
            bool mutexHeld = false;
            try
            {
                if (!TryAcquireLedgerMutex(out bool acquired))
                    return;
                if (!acquired)
                    return;
                mutexHeld = true;
                if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024)
                    return;
                RequestLedgerDto ledger = JsonUtility.FromJson<RequestLedgerDto>(File.ReadAllText(path, Encoding.UTF8));
                if (ledger?.requestIds == null)
                    return;
                long now = DateTime.UtcNow.Ticks;
                for (int i = 0; i < ledger.requestIds.Length; i++)
                {
                    string requestId = ledger.requestIds[i];
                    if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > ESAITestProtocol.MaxIdentityLength)
                        continue;
                    long completedTicks = ledger.completedUtcTicks != null && i < ledger.completedUtcTicks.Length
                        ? ledger.completedUtcTicks[i]
                        : now;
                    if (completedTicks <= 0 || now - completedTicks > RequestIdRetentionTicks)
                        continue;
                    if (CompletedRequestIds.Add(requestId))
                    {
                        CompletedRequestOrder.Enqueue(requestId);
                        CompletedRequestTimes[requestId] = completedTicks;
                    }
                    if (CompletedRequestOrder.Count >= MaximumRememberedRequestIds)
                        break;
                }
                requestLedgerLoaded = true;
            }
            catch
            {
                // A corrupt ledger must not prevent a safe rejection/clarification response.
                CompletedRequestIds.Clear();
                CompletedRequestOrder.Clear();
                CompletedRequestTimes.Clear();
                try
                {
                    if (File.Exists(path))
                        File.Move(path, path + ".corrupt." + DateTime.UtcNow.Ticks);
                }
                catch
                {
                    // Preserve the original failure without making recovery destructive.
                }
                requestLedgerLoaded = true;
            }
            finally
            {
                if (mutexHeld)
                    RequestLedgerMutex.ReleaseMutex();
            }
        }

        private static bool PersistRequestLedger()
        {
            string path = Path.Combine(Application.persistentDataPath, RequestLedgerRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return false;
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool mutexHeld = false;
            try
            {
                if (!TryAcquireLedgerMutex(out bool acquired) || !acquired)
                    return false;
                mutexHeld = true;
                Directory.CreateDirectory(directory);
                long now = DateTime.UtcNow.Ticks;
                var ids = new List<string>(CompletedRequestOrder.Count);
                var ticks = new List<long>(CompletedRequestOrder.Count);
                foreach (string requestId in CompletedRequestOrder)
                {
                    if (!CompletedRequestTimes.TryGetValue(requestId, out long completedTicks)
                        || now - completedTicks > RequestIdRetentionTicks)
                        continue;
                    ids.Add(requestId);
                    ticks.Add(completedTicks);
                }
                var ledger = new RequestLedgerDto
                {
                    requestIds = ids.ToArray(),
                    completedUtcTicks = ticks.ToArray(),
                };
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(ledger, true), new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
                return true;
            }
            catch
            {
                // Execution result remains valid; persistence failure is surfaced by the next
                // report layer when available, without retrying the action in this call.
                return false;
            }
            finally
            {
                if (mutexHeld)
                    RequestLedgerMutex.ReleaseMutex();
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static bool TryAcquireLedgerMutex(out bool acquired)
        {
            acquired = false;
            try
            {
                acquired = RequestLedgerMutex.WaitOne(TimeSpan.FromSeconds(1));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool Reject(ESAITestNaturalLanguageRouteDto route, string reason, out string error)
        {
            route.accepted = false;
            route.requiresClarification = false;
            route.rejectionReason = reason;
            error = reason;
            return false;
        }

        private static bool RejectClarification(ESAITestNaturalLanguageRouteDto route, string reason, out string error)
        {
            route.accepted = false;
            route.requiresClarification = true;
            route.rejectionReason = reason;
            error = reason;
            return false;
        }

        private static bool RejectValue(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
