using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace ESFramework.ESAITest
{
    /// <summary>
    /// Bounded per-turn context sent from Player to a continuous external AI agent. Recent
    /// attention.snapshot results are retained as event values, so the agent can decide from
    /// the same observable evidence as a human operator instead of polling the whole game.
    /// </summary>
    [Serializable]
    public sealed class ESAITestAutonomyDecisionRequestDto
    {
        public const string Schema = "esaitest.autonomy-decision-request/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public int turnIndex;
        public string goal;
        public int sceneGeneration;
        public string requestId;
        public string requestNonce;
        public long expiresUtcTicks;
        public long generatedUtcTicks;
        public ESAITestEventDto[] recentEvents = Array.Empty<ESAITestEventDto>();
    }

    /// <summary>
    /// Created once per Run before an automatic agent starts or an existing agent attaches. No
    /// secret, arbitrary command or model prompt is written here; the agent receives a fixed
    /// session path and owns its own credential boundary.
    /// </summary>
    [Serializable]
    public sealed class ESAITestAutonomyBridgeSessionDto
    {
        public const string Schema = "esaitest.autonomy-bridge-session/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string goal;
        public string requestDirectory;
        public string decisionDirectory;
        public string statusFile;
        public string stopFile;
        public long createdUtcTicks;
    }

    /// <summary>
    /// The external agent overwrites status/status.json atomically with a strictly increasing
    /// sequence while it is alive. It is a liveness signal, not an authorization token.
    /// </summary>
    [Serializable]
    public sealed class ESAITestAutonomyBridgeStatusDto
    {
        public const string Schema = "esaitest.autonomy-bridge-status/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string agentId;
        public int sequence;
        public string state;
        public long utcTicks;
        public string message;
    }

    [Serializable]
    public sealed class ESAITestAutonomyBridgeStopDto
    {
        public const string Schema = "esaitest.autonomy-bridge-stop/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public string reason;
        public long requestedUtcTicks;
    }

    [DisallowMultipleComponent]
    public sealed class ESAITestAutonomyExternalBridge : MonoBehaviour
    {
        public const string AgentPathEnvironmentVariable = ESAITestAutonomyExternalBridgeEnvironment.AgentPathEnvironmentVariable;
        public const string AgentSha256EnvironmentVariable = ESAITestAutonomyExternalBridgeEnvironment.AgentSha256EnvironmentVariable;

        private const float FastPollSeconds = 0.1f;
        private const float IdlePollSeconds = 0.5f;
        private const int MaxDecisionFilesPerPoll = 4;
        private const int MaxRecentEvents = 18;
        private const string RequestDirectoryName = "requests";
        private const string DecisionDirectoryName = "decisions";
        private const string StatusDirectoryName = "status";
        private const string ControlDirectoryName = "control";
        private const string SessionFileName = "session.json";
        private const string StatusFileName = "status.json";
        private const string StopFileName = "stop.json";

        private readonly List<ESAITestEventDto> recentEvents = new List<ESAITestEventDto>(MaxRecentEvents + 6);
        private ESAITestRunner runner;
        private ESAITestAutonomyExternalBridgeConfigDto configuration;
        private ESAITestAutonomyBridgeDiagnosticsDto diagnostics;
        private Process externalProcess;
        private string activeRunId;
        private string runDirectory;
        private string requestDirectory;
        private string decisionDirectory;
        private string statusFile;
        private string stopFile;
        private int lastPublishedTurn = -1;
        private readonly Dictionary<int, PendingDecisionRequest> pendingRequests = new Dictionary<int, PendingDecisionRequest>(4);
        private int lastHeartbeatSequence;
        private long lastStatusWriteUtcTicks;
        private float boundAtUnscaledTime;
        private float lastHeartbeatAtUnscaledTime;
        private float nextPollTime;

        private sealed class PendingDecisionRequest
        {
            public string requestId;
            public string nonce;
            public long expiresUtcTicks;
        }

        private void Update()
        {
            ESAITestRunner activeRunner = ESAITestPlayerBootstrap.ActiveRunner;
            if (activeRunner == null || !activeRunner.IsRunning || !activeRunner.IsAutonomyEnabled)
            {
                ResetRun("runner_inactive");
                return;
            }

            if (!ReferenceEquals(runner, activeRunner)
                || !string.Equals(activeRunId, activeRunner.RunId, StringComparison.Ordinal))
            {
                BindRun(activeRunner);
            }

            if (runner == null)
                return;

            if (runner.AutonomyWaitingForDecision)
                PublishDecisionRequest(runner.AutonomyTurn + 1);

            if (Time.unscaledTime < nextPollTime)
                return;

            bool processed = PollTransport();
            nextPollTime = Time.unscaledTime + (processed ? FastPollSeconds : IdlePollSeconds);
        }

        private void OnDestroy()
        {
            ResetRun("bridge_destroyed");
        }

        private void BindRun(ESAITestRunner activeRunner)
        {
            ResetRun("superseded");
            runner = activeRunner;
            activeRunId = activeRunner.RunId;
            configuration = activeRunner.AutonomyExternalBridgeConfig;
            diagnostics = new ESAITestAutonomyBridgeDiagnosticsDto
            {
                autoLaunchRequested = configuration != null && configuration.autoLaunch,
                launcherId = configuration?.launcherId ?? string.Empty,
                state = "binding",
                lastStatusCode = ESAITestStatusCode.Passed,
                lastMessage = "正在建立自主外部桥会话。",
            };

            try
            {
                string safeRunId = SanitizeSegment(activeRunId);
                runDirectory = Path.Combine(Application.persistentDataPath, "ESAITest", "autonomy", safeRunId);
                requestDirectory = Path.Combine(runDirectory, RequestDirectoryName);
                decisionDirectory = Path.Combine(runDirectory, DecisionDirectoryName);
                statusFile = Path.Combine(runDirectory, StatusDirectoryName, StatusFileName);
                stopFile = Path.Combine(runDirectory, ControlDirectoryName, StopFileName);
                if (Directory.Exists(runDirectory))
                    throw new IOException("同一 RunId 的自主桥目录已存在，拒绝复用：" + runDirectory);
                Directory.CreateDirectory(runDirectory);
                Directory.CreateDirectory(requestDirectory);
                Directory.CreateDirectory(decisionDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(statusFile));
                Directory.CreateDirectory(Path.GetDirectoryName(stopFile));

                string sessionFile = Path.Combine(runDirectory, SessionFileName);
                WriteAtomically(sessionFile, JsonUtility.ToJson(new ESAITestAutonomyBridgeSessionDto
                {
                    runId = activeRunId,
                    goal = activeRunner.AutonomyGoal,
                    requestDirectory = requestDirectory,
                    decisionDirectory = decisionDirectory,
                    statusFile = statusFile,
                    stopFile = stopFile,
                    createdUtcTicks = DateTime.UtcNow.Ticks,
                }, true));

                lastPublishedTurn = -1;
                pendingRequests.Clear();
                lastHeartbeatSequence = 0;
                lastStatusWriteUtcTicks = 0;
                boundAtUnscaledTime = Time.unscaledTime;
                lastHeartbeatAtUnscaledTime = boundAtUnscaledTime;
                nextPollTime = 0f;
                diagnostics.state = configuration != null && configuration.autoLaunch ? "launching" : "manual_waiting";
                diagnostics.lastMessage = configuration != null && configuration.autoLaunch
                    ? "自主桥会话已建立，正在自动启动受信外部 Agent。"
                    : "自主桥会话已建立，等待外部 Agent 手动连接。";
                PublishDiagnostics();

                if (configuration != null && configuration.autoLaunch)
                    LaunchExternalAgent();
                else
                    Debug.Log("[ESAITest] 自主外部桥已绑定 Run（手动 Agent 模式）：" + activeRunId, this);
            }
            catch (Exception exception)
            {
                FailBridge(ESAITestStatusCode.AutonomyBridgeSessionConflict,
                    "建立自主外部桥会话失败：" + exception.Message);
            }
        }

        private void LaunchExternalAgent()
        {
            if (!TryResolveAutomaticLauncher(out string executablePath, out string error))
            {
                FailBridge(ESAITestStatusCode.AutonomyBridgeLaunchFailed, error);
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = QuoteArgument("--esaitest-autonomy-session") + " "
                        + QuoteArgument(Path.Combine(runDirectory, SessionFileName)),
                    WorkingDirectory = runDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                // Do not inherit Unity's complete environment (which may contain API keys,
                // cloud credentials or editor tooling variables). The Agent gets only the path
                // needed to resolve its own side-by-side dependencies and the Windows system
                // root required by native process startup.
                startInfo.EnvironmentVariables.Clear();
                string executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
                startInfo.EnvironmentVariables["PATH"] = executableDirectory;
                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
                if (!string.IsNullOrEmpty(systemRoot))
                    startInfo.EnvironmentVariables["SystemRoot"] = systemRoot;
                externalProcess = Process.Start(startInfo);
                if (externalProcess == null)
                    throw new InvalidOperationException("Process.Start 未返回 Agent 进程。");

                diagnostics.externalProcessId = externalProcess.Id;
                diagnostics.launchUtcTicks = DateTime.UtcNow.Ticks;
                diagnostics.state = "waiting_ready";
                diagnostics.lastStatusCode = ESAITestStatusCode.Passed;
                diagnostics.lastMessage = "外部 Agent 已启动，等待 status.json 的 ready/alive 心跳。";
                PublishDiagnostics();
                Debug.Log("[ESAITest] 已自动启动自主外部 Agent：pid=" + diagnostics.externalProcessId + " Run=" + activeRunId, this);
            }
            catch (Exception exception)
            {
                FailBridge(ESAITestStatusCode.AutonomyBridgeLaunchFailed,
                    "无法启动受信外部 Agent：" + exception.Message);
            }
        }

        private bool PollTransport()
        {
            bool processed = false;
            if (configuration != null)
                processed |= PollAgentHealth();
            processed |= PollDecisionInbox();
            return processed;
        }

        private bool PollAgentHealth()
        {
            if (runner == null || configuration == null)
                return false;

            if (configuration.autoLaunch && externalProcess != null)
            {
                try
                {
                    if (externalProcess.HasExited)
                    {
                        FailBridge(ESAITestStatusCode.AutonomyBridgeExited,
                            "外部 Agent 已退出，exitCode=" + externalProcess.ExitCode + "。 ");
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    FailBridge(ESAITestStatusCode.AutonomyBridgeExited,
                        "无法读取外部 Agent 状态：" + exception.Message);
                    return true;
                }
            }

            bool updated = ReadHeartbeat();
            float now = Time.unscaledTime;
            float elapsedSinceHeartbeat = now - lastHeartbeatAtUnscaledTime;
            if (diagnostics.readyUtcTicks == 0
                && now - boundAtUnscaledTime > configuration.startupTimeoutSeconds)
            {
                FailBridge(ESAITestStatusCode.AutonomyBridgeStartupTimeout,
                    "外部 Agent 在 " + configuration.startupTimeoutSeconds.ToString("F1") + " 秒内未写入 ready/alive 心跳。 ");
                return true;
            }
            if (diagnostics.readyUtcTicks != 0 && elapsedSinceHeartbeat > configuration.heartbeatTimeoutSeconds)
            {
                FailBridge(ESAITestStatusCode.AutonomyBridgeHeartbeatTimeout,
                    "外部 Agent 心跳超过 " + configuration.heartbeatTimeoutSeconds.ToString("F1") + " 秒未更新。 ");
                return true;
            }
            return updated;
        }

        private bool ReadHeartbeat()
        {
            try
            {
                if (string.IsNullOrEmpty(statusFile) || !File.Exists(statusFile))
                    return false;
                if (new FileInfo(statusFile).Length > ESAITestProtocol.MaximumAutonomyStatusBytes)
                    throw new InvalidDataException("Agent 心跳文件超过大小上限。 ");

                long changedUtcTicks = File.GetLastWriteTimeUtc(statusFile).Ticks;
                if (changedUtcTicks <= lastStatusWriteUtcTicks)
                    return false;

                ESAITestAutonomyBridgeStatusDto status = JsonUtility.FromJson<ESAITestAutonomyBridgeStatusDto>(
                    File.ReadAllText(statusFile, Encoding.UTF8));
                if (!IsValidHeartbeat(status, out string error))
                {
                    diagnostics.lastStatusCode = ESAITestStatusCode.AutonomyBridgeHeartbeatTimeout;
                    diagnostics.lastMessage = "忽略无效 Agent 心跳：" + error;
                    PublishDiagnostics();
                    return false;
                }

                lastStatusWriteUtcTicks = changedUtcTicks;
                lastHeartbeatSequence = status.sequence;
                lastHeartbeatAtUnscaledTime = Time.unscaledTime;
                diagnostics.readyUtcTicks = diagnostics.readyUtcTicks == 0 ? DateTime.UtcNow.Ticks : diagnostics.readyUtcTicks;
                diagnostics.lastHeartbeatUtcTicks = status.utcTicks;
                diagnostics.state = string.Equals(status.state, "stopping", StringComparison.OrdinalIgnoreCase)
                    ? "agent_stopping"
                    : "ready";
                diagnostics.lastStatusCode = ESAITestStatusCode.Passed;
                diagnostics.lastMessage = string.IsNullOrWhiteSpace(status.message)
                    ? "外部 Agent 心跳正常：" + status.agentId
                    : status.message;
                PublishDiagnostics();
                return true;
            }
            catch (Exception exception)
            {
                // A foreign agent must replace the file atomically. A transient partial read is
                // not terminal; the bounded heartbeat timeout converts a persistent fault into
                // an explicit Run result instead of leaving an endless wait.
                diagnostics.lastStatusCode = ESAITestStatusCode.AutonomyBridgeHeartbeatTimeout;
                diagnostics.lastMessage = "读取 Agent 心跳失败，等待下一次原子更新：" + exception.Message;
                PublishDiagnostics();
                return false;
            }
        }

        private bool IsValidHeartbeat(ESAITestAutonomyBridgeStatusDto status, out string error)
        {
            error = string.Empty;
            if (status == null
                || !string.Equals(status.schema, ESAITestAutonomyBridgeStatusDto.Schema, StringComparison.Ordinal)
                || status.protocolVersion != ESAITestProtocol.CurrentVersion
                || !string.Equals(status.runId, activeRunId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(status.agentId)
                || status.agentId.Length > ESAITestProtocol.MaxIdentityLength
                || status.sequence <= 0
                || status.utcTicks <= 0
                || (!string.Equals(status.state, "ready", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(status.state, "alive", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(status.state, "stopping", StringComparison.OrdinalIgnoreCase)))
            {
                error = "协议、RunId、agentId、sequence、时间或 state 不匹配。";
                return false;
            }
            if (lastHeartbeatSequence > 0 && status.sequence <= lastHeartbeatSequence)
            {
                error = "心跳 sequence 未严格递增。";
                return false;
            }
            long heartbeatAgeTicks = DateTime.UtcNow.Ticks - status.utcTicks;
            long allowedAgeTicks = TimeSpan.FromSeconds(configuration?.heartbeatTimeoutSeconds ?? 20f).Ticks;
            if (heartbeatAgeTicks > allowedAgeTicks || heartbeatAgeTicks < -allowedAgeTicks)
            {
                error = "心跳 utcTicks 已超出允许的新鲜度窗口。";
                return false;
            }
            return true;
        }

        private void PublishDecisionRequest(int turnIndex)
        {
            if (turnIndex < 1 || string.IsNullOrEmpty(requestDirectory))
                return;

            if (turnIndex == lastPublishedTurn)
            {
                if (!pendingRequests.TryGetValue(turnIndex, out PendingDecisionRequest pending)
                    || DateTime.UtcNow.Ticks <= pending.expiresUtcTicks)
                    return;

                string expiredPath = Path.Combine(requestDirectory, "turn-" + turnIndex.ToString("D4") + ".json");
                if (File.Exists(expiredPath))
                    MoveToDisposition(expiredPath, "expired");
                pendingRequests.Remove(turnIndex);
                lastPublishedTurn = -1;
            }

            string finalPath = Path.Combine(requestDirectory, "turn-" + turnIndex.ToString("D4") + ".json");
            if (File.Exists(finalPath))
            {
                lastPublishedTurn = turnIndex;
                return;
            }

            runner.CopyRecentEvents(recentEvents, MaxRecentEvents);
            long generatedUtcTicks = DateTime.UtcNow.Ticks;
            var request = new ESAITestAutonomyDecisionRequestDto
            {
                runId = runner.RunId,
                turnIndex = turnIndex,
                goal = runner.AutonomyGoal,
                sceneGeneration = ESAITestRuntime.SceneGeneration,
                requestId = runner.RunId + ":turn:" + turnIndex,
                requestNonce = Guid.NewGuid().ToString("N"),
                expiresUtcTicks = new DateTime(generatedUtcTicks).AddSeconds(ESAITestProtocol.AutonomyDecisionRequestTtlSeconds).Ticks,
                generatedUtcTicks = generatedUtcTicks,
                recentEvents = recentEvents.ToArray(),
            };
            try
            {
                string json = JsonUtility.ToJson(request, true);
                if (Encoding.UTF8.GetByteCount(json) > ESAITestProtocol.MaximumAutonomyRequestBytes)
                    throw new InvalidDataException("自主决策请求超过大小上限。 ");
                WriteAtomically(finalPath, json);
                pendingRequests[turnIndex] = new PendingDecisionRequest
                {
                    requestId = request.requestId,
                    nonce = request.requestNonce,
                    expiresUtcTicks = request.expiresUtcTicks,
                };
                lastPublishedTurn = turnIndex;
                diagnostics.requestsPublished++;
                diagnostics.lastMessage = "已发布第 " + turnIndex + " 回合决策请求。";
                PublishDiagnostics();
                Debug.Log("[ESAITest] 已向外部 AI 请求自主决策：Run=" + request.runId + " turn=" + turnIndex, this);
            }
            catch (Exception exception)
            {
                diagnostics.lastStatusCode = ESAITestStatusCode.InternalError;
                diagnostics.lastMessage = "写入自主决策请求失败：" + exception.Message;
                PublishDiagnostics();
                Debug.LogWarning("[ESAITest] 写入自主决策请求失败：" + exception.Message, this);
            }
        }

        private bool PollDecisionInbox()
        {
            try
            {
                if (string.IsNullOrEmpty(decisionDirectory) || !Directory.Exists(decisionDirectory))
                    return false;

                var files = new List<string>(MaxDecisionFilesPerPoll);
                foreach (string candidate in Directory.EnumerateFiles(
                    decisionDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    files.Add(candidate);
                    if (files.Count >= MaxDecisionFilesPerPoll)
                        break;
                }
                if (files.Count == 0)
                    return false;
                files.Sort(StringComparer.Ordinal);
                for (int i = 0; i < files.Count; i++)
                    ProcessDecisionFile(files[i]);
                return true;
            }
            catch (Exception exception)
            {
                diagnostics.lastStatusCode = ESAITestStatusCode.InternalError;
                diagnostics.lastMessage = "轮询自主决策收件箱失败：" + exception.Message;
                PublishDiagnostics();
                Debug.LogWarning("[ESAITest] 轮询自主决策收件箱失败：" + exception.Message, this);
                return false;
            }
        }

        private void ProcessDecisionFile(string path)
        {
            string disposition = "rejected";
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Length > ESAITestProtocol.MaximumAutonomyDecisionBytes)
                    throw new InvalidDataException("自主决策文件超过大小上限。 ");
                ESAITestAutonomyDecisionDto decision = JsonUtility.FromJson<ESAITestAutonomyDecisionDto>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (decision == null)
                    throw new InvalidDataException("自主决策 JSON 为空。 ");

                if (!pendingRequests.TryGetValue(decision.turnIndex, out PendingDecisionRequest pending)
                    || !string.Equals(decision.requestId, pending.requestId, StringComparison.Ordinal)
                    || !string.Equals(decision.requestNonce, pending.nonce, StringComparison.Ordinal)
                    || decision.requestExpiresUtcTicks != pending.expiresUtcTicks)
                {
                    disposition = decision.requestExpiresUtcTicks > 0
                        && decision.requestExpiresUtcTicks < DateTime.UtcNow.Ticks
                        ? "expired"
                        : "rejected";
                    diagnostics.decisionsRejected++;
                    diagnostics.lastStatusCode = ESAITestStatusCode.AutonomyDecisionRejected;
                    diagnostics.lastMessage = "外部 Agent 决策 requestId/nonce/TTL 不匹配，拒绝重放或伪造结果。";
                    PublishDiagnostics();
                    return;
                }
                if (pending.expiresUtcTicks < DateTime.UtcNow.Ticks)
                {
                    disposition = "expired";
                    diagnostics.decisionsRejected++;
                    diagnostics.lastStatusCode = ESAITestStatusCode.AutonomyDecisionRejected;
                    diagnostics.lastMessage = "外部 Agent 决策已超过 request TTL。";
                    PublishDiagnostics();
                    return;
                }
                if (runner.SubmitAutonomyDecision(decision, out string error))
                {
                    disposition = "accepted";
                    pendingRequests.Remove(decision.turnIndex);
                    diagnostics.decisionsAccepted++;
                    diagnostics.lastStatusCode = ESAITestStatusCode.Passed;
                    diagnostics.lastMessage = "已接收外部 Agent 决策：" + decision.decisionId;
                    PublishDiagnostics();
                    Debug.Log("[ESAITest] 已接收外部 AI 自主决策：" + decision.decisionId, this);
                }
                else if (error.IndexOf("队列已满", StringComparison.Ordinal) >= 0)
                {
                    // Preserve the final JSON for the next short poll. A queued decision has not
                    // been accepted or rejected, so moving it here would lose a valid turn.
                    disposition = null;
                    return;
                }
                else
                {
                    diagnostics.decisionsRejected++;
                    diagnostics.lastStatusCode = ESAITestStatusCode.AutonomyDecisionRejected;
                    diagnostics.lastMessage = "外部 Agent 决策被拒绝：" + error;
                    PublishDiagnostics();
                    Debug.LogWarning("[ESAITest] 外部 AI 自主决策被拒绝：" + error, this);
                }
            }
            catch (Exception exception)
            {
                diagnostics.decisionReadFailures++;
                diagnostics.lastStatusCode = ESAITestStatusCode.InternalError;
                diagnostics.lastMessage = "读取外部 Agent 决策失败：" + exception.Message;
                PublishDiagnostics();
                Debug.LogWarning("[ESAITest] 读取外部 AI 自主决策失败：" + exception.Message, this);
            }
            finally
            {
                if (disposition != null)
                    MoveToDisposition(path, disposition);
            }
        }

        private void FailBridge(string statusCode, string message)
        {
            if (diagnostics == null)
                diagnostics = new ESAITestAutonomyBridgeDiagnosticsDto();
            diagnostics.state = "failed";
            diagnostics.lastStatusCode = statusCode ?? ESAITestStatusCode.AutonomyBridgeLaunchFailed;
            diagnostics.lastMessage = message ?? string.Empty;
            PublishDiagnostics();
            runner?.FailAutonomyBridge(diagnostics.lastStatusCode, diagnostics.lastMessage);
            Debug.LogError("[ESAITest] 自主外部桥失败：" + diagnostics.lastMessage, this);
        }

        private void PublishDiagnostics()
        {
            runner?.UpdateAutonomyBridgeDiagnostics(diagnostics);
        }

        private void ResetRun(string reason)
        {
            if (runner == null && externalProcess == null)
                return;

            WriteStopSignal(reason);
            StopExternalAgent();
            if (diagnostics != null && !string.Equals(diagnostics.state, "failed", StringComparison.Ordinal))
            {
                diagnostics.state = "stopped";
                diagnostics.lastMessage = "自主外部桥已停止：" + reason;
                PublishDiagnostics();
            }
            runner = null;
            configuration = null;
            activeRunId = null;
            runDirectory = null;
            requestDirectory = null;
            decisionDirectory = null;
            statusFile = null;
            stopFile = null;
            diagnostics = null;
            lastPublishedTurn = -1;
            pendingRequests.Clear();
            lastHeartbeatSequence = 0;
            lastStatusWriteUtcTicks = 0;
        }

        private void WriteStopSignal(string reason)
        {
            if (string.IsNullOrEmpty(stopFile) || string.IsNullOrEmpty(activeRunId) || File.Exists(stopFile))
                return;
            try
            {
                WriteAtomically(stopFile, JsonUtility.ToJson(new ESAITestAutonomyBridgeStopDto
                {
                    runId = activeRunId,
                    reason = reason ?? string.Empty,
                    requestedUtcTicks = DateTime.UtcNow.Ticks,
                }, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESAITest] 写入外部 Agent 停止信号失败：" + exception.Message, this);
            }
        }

        private void StopExternalAgent()
        {
            if (externalProcess == null)
                return;
            try
            {
                if (!externalProcess.HasExited)
                    externalProcess.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESAITest] 停止外部 Agent 失败：" + exception.Message, this);
            }
            finally
            {
                externalProcess.Dispose();
                externalProcess = null;
            }
        }

        public static bool TryValidateAutomaticLauncher(out string error)
        {
            return TryResolveAutomaticLauncher(out _, out error);
        }

        private static bool TryResolveAutomaticLauncher(out string executablePath, out string error)
        {
            executablePath = string.Empty;
            error = string.Empty;
            if (!IsDesktopProcessPlatform())
            {
                error = "当前平台不支持自动启动外部 Agent；请使用手动决策文件桥。";
                return false;
            }

            return ESAITestAutonomyExternalBridgeEnvironment.TryResolve(out executablePath, out error);
        }

        private static bool IsDesktopProcessPlatform()
        {
            RuntimePlatform platform = Application.platform;
            return platform == RuntimePlatform.WindowsEditor
                || platform == RuntimePlatform.WindowsPlayer
                || platform == RuntimePlatform.OSXEditor
                || platform == RuntimePlatform.OSXPlayer
                || platform == RuntimePlatform.LinuxEditor
                || platform == RuntimePlatform.LinuxPlayer;
        }


        private static void WriteAtomically(string finalPath, string content)
        {
            string directory = Path.GetDirectoryName(finalPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("自主桥目录无效。");
            Directory.CreateDirectory(directory);
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content ?? string.Empty, new UTF8Encoding(false));
                File.Move(temporaryPath, finalPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void MoveToDisposition(string source, string disposition)
        {
            if (!File.Exists(source))
                return;
            string destination = Path.ChangeExtension(source, "." + disposition);
            if (File.Exists(destination))
                destination += "." + DateTime.UtcNow.Ticks;
            File.Move(source, destination);
        }

        private static string QuoteArgument(string value)
        {
            string argument = value ?? string.Empty;
            var result = new StringBuilder(argument.Length + 2);
            result.Append('"');
            int pendingBackslashes = 0;
            for (int i = 0; i < argument.Length; i++)
            {
                char character = argument[i];
                if (character == '\\')
                {
                    pendingBackslashes++;
                    continue;
                }
                if (character == '"')
                {
                    result.Append('\\', pendingBackslashes * 2 + 1);
                    result.Append(character);
                    pendingBackslashes = 0;
                    continue;
                }
                if (pendingBackslashes > 0)
                {
                    result.Append('\\', pendingBackslashes);
                    pendingBackslashes = 0;
                }
                result.Append(character);
            }
            if (pendingBackslashes > 0)
                result.Append('\\', pendingBackslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static string SanitizeSegment(string value)
        {
            string result = value ?? string.Empty;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return string.IsNullOrEmpty(result) ? "unknown-run" : result;
        }
    }

    public static class ESAITestAutonomyExternalBridgeBootstrap
    {
        private static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            ESAITestRuntime.Activated -= EnsureBridge;
            ESAITestRuntime.Activated += EnsureBridge;
            ESAITestRuntime.Deactivated -= DestroyBridge;
            ESAITestRuntime.Deactivated += DestroyBridge;
        }

        private static void EnsureBridge()
        {
            if (host != null || ESAITestPlayerBootstrap.ActiveRunner == null
                || !ESAITestPlayerBootstrap.ActiveRunner.IsAutonomyEnabled
                || ESAITestPlayerBootstrap.ActiveRunner.AutonomyExternalBridgeConfig == null)
                return;
            host = new GameObject("ESAITest Autonomy External Bridge");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ESAITestAutonomyExternalBridge>();
        }

        private static void DestroyBridge()
        {
            if (host == null)
                return;
            UnityEngine.Object.Destroy(host);
            host = null;
        }
    }
}
