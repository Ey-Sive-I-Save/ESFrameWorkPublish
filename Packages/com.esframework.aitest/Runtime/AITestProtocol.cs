using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ESFramework.ESAITest
{
    public static class ESAITestProtocol
    {
        public const int CurrentVersion = 1;
        public const string CapabilityResponseSchema = "esaitest.capability-response/v1";

        public const string OperationSee = "see";
        public const string OperationVerify = "verify";
        public const string OperationWait = "wait";
        public const string OperationAct = "act";

        // The Runner is deliberately step-driven rather than frame-driven. These limits keep an
        // externally supplied plan bounded, diagnosable and inexpensive even when it retries.
        public const int MaxPlanStepCount = 256;
        public const int MaxArgumentsPerStep = 32;
        public const int MaxIdentityLength = 256;
        public const int MaxTextLength = 4096;
        public const float MinimumPollIntervalSeconds = 0.05f;
        // A test plan is an operational contract, not an unbounded background job. These caps
        // make the maximum retry pressure explicit to the plan author and protect the Player.
        public const float MaximumTotalTimeoutSeconds = 1800f;
        public const float MaximumStepTimeoutSeconds = 300f;
        public const int MaximumCapabilityCallsPerStep = 1024;
        public const int MaximumAutonomyTurns = 256;
        public const int MaximumAutonomyStepsPerDecision = 16;
        public const int MaximumQueuedAutonomyDecisions = 2;
        public const float MinimumAutonomyBridgeTimeoutSeconds = 1f;
        public const float MaximumAutonomyBridgeTimeoutSeconds = 120f;
        public const int MaximumAutonomyRequestBytes = 512 * 1024;
        public const int MaximumAutonomyDecisionBytes = 256 * 1024;
        public const int MaximumAutonomyStatusBytes = 16 * 1024;
        public const float AutonomyDecisionRequestTtlSeconds = 60f;

        // A verify/wait Step must opt in to link its evidence to a preceding act receipt.
        // This supports cross-capability business verification without assuming that a same-target
        // UI state read proves the action's business effect.
        public const string ArgumentVerifyUseStepId = "verifyUseStepId";
    }

    public static class ESAITestStatusCode
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
        public const string InvalidRequest = "invalid_request";
        public const string InvalidPlan = "invalid_plan";
        public const string UnsupportedProtocol = "unsupported_protocol";
        public const string RuntimeBusy = "runtime_busy";
        public const string CapabilityUnavailable = "capability_unavailable";
        public const string CapabilityRejected = "capability_rejected";
        public const string VerificationFailed = "verification_failed";
        public const string StepTimeout = "step_timeout";
        public const string TotalTimeout = "total_timeout";
        public const string CallBudgetExceeded = "call_budget_exceeded";
        public const string ReportWriteFailed = "report_write_failed";
        public const string InternalError = "internal_error";
        public const string AutonomyWaitingForDecision = "autonomy_waiting_for_decision";
        public const string AutonomyTurnLimit = "autonomy_turn_limit";
        public const string AutonomyStuck = "autonomy_stuck";
        public const string AutonomyDecisionRejected = "autonomy_decision_rejected";
        public const string AutonomyBridgeLaunchFailed = "autonomy_bridge_launch_failed";
        public const string AutonomyBridgeStartupTimeout = "autonomy_bridge_startup_timeout";
        public const string AutonomyBridgeHeartbeatTimeout = "autonomy_bridge_heartbeat_timeout";
        public const string AutonomyBridgeExited = "autonomy_bridge_exited";
        public const string AutonomyBridgeSessionConflict = "autonomy_bridge_session_conflict";
    }

    public static class ESAITestReportStatusCode
    {
        public const string Pending = "pending";
        public const string Written = "written";
        public const string WriteFailed = ESAITestStatusCode.ReportWriteFailed;
    }

    [Serializable]
    public sealed class ESAITestRequestDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public ESAITestPlanDto plan;
        public ESAITestAutonomyConfigDto autonomy;
        public bool quitOnComplete;
    }

    [Serializable]
    public sealed class ESAITestPlanDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string planId;
        public float totalTimeoutSeconds = 300f;
        public ESAITestStepDto[] steps = Array.Empty<ESAITestStepDto>();
    }

    [Serializable]
    public sealed class ESAITestAutonomyConfigDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string goal;
        public int maxTurns = 64;
        public float maxDurationSeconds = 300f;
        public int maxConsecutiveFailures = 3;
        public bool allowExploration = true;
        public bool requireBusinessVerification = true;
        // Null preserves the manual decision-file hand-off. Automatic launching is opt-in
        // and accepts only the locally trusted environment launcher described below.
        public ESAITestAutonomyExternalBridgeConfigDto externalBridge;
    }

    [Serializable]
    public sealed class ESAITestAutonomyPreparationDto
    {
        public const string Schema = "esaitest.autonomy-preparation/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string preparationId;
        public string goal;
        public string launcherId = ESAITestAutonomyExternalBridgeConfigDto.EnvironmentLauncherId;
        public string executableName;
        public string executableSha256;
        public string state = "prepared";
        public long createdUtcTicks;
    }

    /// <summary>
    /// Configuration for the Player-to-external-agent transport. It intentionally contains no
    /// executable path, shell fragment, endpoint or credential: the Player resolves the only
    /// supported launcher from the local operator environment.
    /// </summary>
    [Serializable]
    public sealed class ESAITestAutonomyExternalBridgeConfigDto
    {
        public const string EnvironmentLauncherId = "environment-agent";

        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public bool autoLaunch;
        public string launcherId = EnvironmentLauncherId;
        public float startupTimeoutSeconds = 15f;
        public float heartbeatTimeoutSeconds = 20f;
    }

    [Serializable]
    public sealed class ESAITestConversationIntentEnvelopeDto
    {
        public const string Schema = "esaitest.conversation-intent/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string requestId;
        public string source;
        public long createdUtcTicks;
        public float timeToLiveSeconds = 60f;
        public string originalText;
        public ESAITestNaturalLanguageRouteDto route;
    }

    [Serializable]
    public sealed class ESAITestConversationReceiptDto
    {
        public const string Schema = "esaitest.conversation-receipt/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string requestId;
        public string stage;
        public string statusCode;
        public string source;
        public string originalText;
        public string normalizedText;
        public string intent;
        public string parsedMessage;
        public string parsedGoal;
        public string parsedPriority;
        public float parsedTtlSeconds;
        public float confidence;
        public string boundRunId;
        public string runId;
        public string promptId;
        public string error;
        public string verificationState;
        public long utcTicks;
    }

    public static class ESAITestConversationRuntimeState
    {
        public static ESAITestConversationReceiptDto LastReceipt { get; internal set; }
    }

    [Serializable]
    public sealed class ESAITestAutonomyDecisionDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public int turnIndex;
        public string decisionId;
        public string mode;
        public string rationale;
        public string requestId;
        public string requestNonce;
        public long requestExpiresUtcTicks;
        public bool terminal;
        public string terminalStatusCode;
        public ESAITestStepDto[] steps = Array.Empty<ESAITestStepDto>();
    }

    [Serializable]
    public sealed class ESAITestStepDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string stepId;
        public string operation;
        public string capabilityId;
        public string command;
        public string target;
        public string expectedValue;
        public float timeoutSeconds = 10f;
        public float pollIntervalSeconds = 0.1f;
        public bool continueOnFailure;
        public ESAITestArgumentDto[] arguments = Array.Empty<ESAITestArgumentDto>();
    }

    [Serializable]
    public sealed class ESAITestArgumentDto
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class ESAITestCapabilityRequestDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public int sceneGeneration;
        // Generated by the Runner for every synchronous provider call. It is deterministic
        // within a Run and is the correlation key for call diagnostics.
        public string invocationId;
        public string stepId;
        public string capabilityId;
        public string operation;
        public string command;
        public string target;
        public string expectedValue;
        public ESAITestArgumentDto[] arguments = Array.Empty<ESAITestArgumentDto>();
    }

    [Serializable]
    public sealed class ESAITestCapabilityResponseDto
    {
        public string schema = ESAITestProtocol.CapabilityResponseSchema;
        // These fields are stamped by the runtime boundary, so a Provider cannot accidentally
        // return an uncorrelated success to the AI.
        public string runId;
        public int sceneGeneration;
        public string invocationId;
        public string stepId;
        public string capabilityId;
        public string operation;
        public string command;
        public string providerId;
        public int providerVersion;
        public bool accepted;
        public bool conditionMet;
        public bool retryable;
        public string statusCode;
        public string message;
        public ESAITestValueDto value;

        public static ESAITestCapabilityResponseDto Reject(string statusCode, string message)
        {
            return new ESAITestCapabilityResponseDto
            {
                accepted = false,
                conditionMet = false,
                retryable = false,
                statusCode = statusCode,
                message = message ?? string.Empty,
            };
        }
    }

    [Serializable]
    public sealed class ESAITestValueDto
    {
        public string kind;
        public string stringValue;
        public bool boolValue;
        public double numberValue;

        public static ESAITestValueDto FromString(string value)
        {
            return new ESAITestValueDto { kind = "string", stringValue = value ?? string.Empty };
        }

        public static ESAITestValueDto FromBoolean(bool value)
        {
            return new ESAITestValueDto { kind = "boolean", boolValue = value };
        }

        public static ESAITestValueDto FromNumber(double value)
        {
            return new ESAITestValueDto { kind = "number", numberValue = value };
        }
    }

    [Serializable]
    public sealed class ESAITestCapabilityManifestDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public int sceneGeneration;
        public ESAITestCapabilityManifestItemDto[] capabilities = Array.Empty<ESAITestCapabilityManifestItemDto>();
    }

    [Serializable]
    public sealed class ESAITestCapabilityManifestItemDto
    {
        public string capabilityId;
        public string providerId;
        public int providerVersion;
        public string[] commands = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAITestEventDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string stepId;
        public string eventType;
        public string statusCode;
        public string message;
        public long utcTicks;
        public float elapsedSeconds;
        public ESAITestValueDto value;
    }

    /// <summary>
    /// 汇总一个 Step 内所有同步 Capability 调用。相同条件的轮询不会逐次扩张事件时间线，
    /// 但调用次数、总/最坏耗时和最终 Provider 协议结果都必须可追溯。
    /// </summary>
    [Serializable]
    public sealed class ESAITestCapabilityCallDiagnosticDto
    {
        public int callCount;
        public int retryCount;
        public string firstInvocationId;
        public string lastInvocationId;
        public int firstSceneGeneration;
        public int lastSceneGeneration;
        public bool sceneGenerationChangedDuringCall;
        public long firstCallUtcTicks;
        public long lastCallUtcTicks;
        public float totalDurationMilliseconds;
        public float maxDurationMilliseconds;
        public bool lastAccepted;
        public bool lastConditionMet;
        public bool lastRetryable;
        public string lastStatusCode;
        public string lastMessage;
        public ESAITestCapabilityResponseDto lastResponse;
    }

    [Serializable]
    public sealed class ESAITestStepResultDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string stepId;
        public string statusCode;
        public string message;
        public float elapsedSeconds;
        public ESAITestValueDto value;
        public ESAITestCapabilityCallDiagnosticDto capabilityCalls;
    }

    [Serializable]
    public sealed class ESAITestResultDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string planId;
        public string statusCode;
        public string message;
        public string executionStatusCode;
        public string executionMessage;
        public string reportStatusCode;
        public string reportMessage;
        public long startedUtcTicks;
        public long completedUtcTicks;
        public float elapsedSeconds;
        public int exitCode;
        public string reportPath;
        public string summaryPath;
        public string diagnosticsPath;
        public string unityVersion;
        public string platform;
        public string productName;
        public string applicationVersion;
        public string activeScene;
        public int totalStepCount;
        public int passedStepCount;
        public int failedStepCount;
        public string firstFailedStepId;
        public ESAITestRequestDto request;
        public ESAITestCapabilityManifestDto manifest;
        public ESAITestArtifactDto[] artifacts = Array.Empty<ESAITestArtifactDto>();
        public ESAITestStepResultDto[] steps = Array.Empty<ESAITestStepResultDto>();
        public ESAITestEventDto[] events = Array.Empty<ESAITestEventDto>();
        public ESAITestRunDiagnosticsDto diagnostics;
    }

    /// <summary>
    /// 面向排障的紧凑索引。它只在 Run 结束时生成，完整证据仍保留在 steps、events 和 artifacts 中。
    /// </summary>
    [Serializable]
    public sealed class ESAITestRunDiagnosticsDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string executionStatusCode;
        public string executionMessage;
        public string reportStatusCode;
        public string reportMessage;
        public string currentStepId;
        public string currentOperation;
        public int completedStepCount;
        public int totalStepCount;
        public int eventCount;
        public int capabilityCallCount;
        public int capabilityRetryCount;
        public float capabilityCallMilliseconds;
        public float maxCapabilityCallMilliseconds;
        public string suggestedInvestigation;
        public ESAITestStepDiagnosticDto firstFailedStep;
        public ESAITestStepDiagnosticDto lastCompletedStep;
        public ESAITestEventDto lastActivityEvent;
        public ESAITestObservationDiagnosticDto lastObservation;
        public ESAITestPromptQueueDiagnosticDto promptQueue;
        public bool autonomyEnabled;
        public string autonomyGoal;
        public int autonomyTurn;
        public int autonomyDecisionCount;
        public int autonomyRejectedDecisionCount;
        public int autonomyConsecutiveFailures;
        public string autonomyLastDecisionId;
        public bool autonomyWaitingForDecision;
        public ESAITestAutonomyBridgeDiagnosticsDto autonomyBridge;
        public ESAITestConversationReceiptDto conversation;
    }

    /// <summary>
    /// Compact, secret-free transport evidence for one autonomous Player Run. The agent's
    /// credential and any model-specific prompt remain outside Unity and outside this DTO.
    /// </summary>
    [Serializable]
    public sealed class ESAITestAutonomyBridgeDiagnosticsDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public bool autoLaunchRequested;
        public string launcherId;
        public string state;
        public int externalProcessId;
        public long launchUtcTicks;
        public long readyUtcTicks;
        public long lastHeartbeatUtcTicks;
        public int requestsPublished;
        public int decisionsAccepted;
        public int decisionsRejected;
        public int decisionReadFailures;
        public string lastStatusCode;
        public string lastMessage;
    }

    /// <summary>
    /// Environment-only preflight kept in the protocol/runtime source set so generated Unity
    /// project snapshots can validate the public bootstrap API even before they refresh the
    /// optional MonoBehaviour bridge file. It never checks platform or starts a process.
    /// </summary>
    internal static class ESAITestAutonomyExternalBridgeEnvironment
    {
        internal const string AgentPathEnvironmentVariable = "ESAITEST_AUTONOMY_AGENT_PATH";
        internal const string AgentSha256EnvironmentVariable = "ESAITEST_AUTONOMY_AGENT_SHA256";

        internal static bool TryResolve(out string executablePath, out string error)
        {
            executablePath = string.Empty;
            error = string.Empty;
            string configuredPath = Environment.GetEnvironmentVariable(AgentPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                error = "未配置 " + AgentPathEnvironmentVariable
                    + "。请把它设置为受信外部 Agent 的绝对可执行文件路径。";
                return false;
            }
            if (!Path.IsPathRooted(configuredPath))
            {
                error = AgentPathEnvironmentVariable + " 必须是绝对路径。";
                return false;
            }

            executablePath = Path.GetFullPath(configuredPath);
            if (!File.Exists(executablePath))
            {
                error = "受信外部 Agent 可执行文件不存在：" + executablePath;
                return false;
            }
            string extension = Path.GetExtension(executablePath);
            if (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".sh", StringComparison.OrdinalIgnoreCase))
            {
                error = "自动 Agent 必须是直接可执行文件，禁止通过 shell、脚本或命令片段启动。";
                return false;
            }

            string expectedHash = Environment.GetEnvironmentVariable(AgentSha256EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                error = "自动启动必须配置 " + AgentSha256EnvironmentVariable + " 哈希白名单。";
                return false;
            }
            else
            {
                if (!IsSha256(expectedHash))
                {
                    error = AgentSha256EnvironmentVariable + " 必须是 64 位十六进制 SHA-256。";
                    return false;
                }
                string actualHash = ComputeSha256(executablePath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "受信外部 Agent 指纹不匹配，拒绝启动。";
                    return false;
                }
            }
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F')))
                    return false;
            }
            return true;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(stream);
                var result = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    result.Append(bytes[i].ToString("x2"));
                return result.ToString();
            }
        }
    }

    [Serializable]
    public sealed class ESAITestStepDiagnosticDto
    {
        public string stepId;
        public string operation;
        public string capabilityId;
        public string command;
        public string target;
        public string expectedValue;
        public ESAITestArgumentDto[] arguments = Array.Empty<ESAITestArgumentDto>();
        public float timeoutSeconds;
        public float pollIntervalSeconds;
        public bool continueOnFailure;
        public string statusCode;
        public string message;
        public float elapsedSeconds;
        public ESAITestValueDto value;
        public ESAITestCapabilityCallDiagnosticDto capabilityCalls;
    }

    [Serializable]
    public sealed class ESAITestObservationDiagnosticDto
    {
        public string command;
        public long observedUtcTicks;
        public string attentionProfile;
        public int uiCount;
        public int sceneObjectCount;
        public float samplingCostMilliseconds;
        public string latestScreenshotPath;
    }

    [Serializable]
    public sealed class ESAITestPromptQueueDiagnosticDto
    {
        public int pendingCount;
        public string highestPendingPriority;
        public ESAITestAIPromptDto lastConsumedPrompt;
    }
}
