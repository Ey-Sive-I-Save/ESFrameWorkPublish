using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ESFramework.ESAITest
{
    public sealed class ESAITestCapabilityRegistry
    {
        private sealed class Entry
        {
            public ESAITestCapabilityProvider provider;
            public string runId;
            public int sceneGeneration;
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> executingCapabilities = new HashSet<string>(StringComparer.Ordinal);
        private readonly object gate = new object();

        public bool Register(ESAITestCapabilityProvider provider, string runId, int sceneGeneration, out string error)
        {
            error = string.Empty;
            if (!ValidateProvider(provider, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                error = "Capability 注册必须绑定 runId。";
                return false;
            }

            lock (gate)
            {
                if (entries.TryGetValue(provider.CapabilityId, out Entry existing) && existing.provider != provider)
                {
                    error = "CapabilityId 已由其他 Provider 注册：" + provider.CapabilityId;
                    return false;
                }

                entries[provider.CapabilityId] = new Entry
                {
                    provider = provider,
                    runId = runId,
                    sceneGeneration = sceneGeneration,
                };
            }
            return true;
        }

        public void Unregister(ESAITestCapabilityProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.CapabilityId))
                return;

            lock (gate)
            {
                if (entries.TryGetValue(provider.CapabilityId, out Entry entry) && entry.provider == provider)
                    entries.Remove(provider.CapabilityId);
            }
        }

        public void RemoveStale(int activeSceneGeneration)
        {
            lock (gate)
            {
                var stale = new List<string>();
                foreach (KeyValuePair<string, Entry> pair in entries)
                    if (pair.Value.sceneGeneration != activeSceneGeneration)
                        stale.Add(pair.Key);

                for (int i = 0; i < stale.Count; i++)
                    entries.Remove(stale[i]);
            }
        }

        public ESAITestCapabilityResponseDto Execute(ESAITestCapabilityRequestDto request)
        {
            ESAITestCapabilityResponseDto response = ExecuteInternal(request, out ESAITestCapabilityProvider provider);
            return StampResponseIdentity(response, request, provider);
        }

        private ESAITestCapabilityResponseDto ExecuteInternal(
            ESAITestCapabilityRequestDto request,
            out ESAITestCapabilityProvider selectedProvider)
        {
            selectedProvider = null;
            if (request == null)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "Capability Request 为空。");

            if (request.protocolVersion != ESAITestProtocol.CurrentVersion)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.UnsupportedProtocol, "不支持的 Capability 协议版本：" + request.protocolVersion);

            if (string.IsNullOrWhiteSpace(request.runId)
                || string.IsNullOrWhiteSpace(request.invocationId)
                || string.IsNullOrWhiteSpace(request.stepId)
                || string.IsNullOrWhiteSpace(request.capabilityId)
                || string.IsNullOrWhiteSpace(request.command)
                || !IsSupportedOperation(request.operation)
                || request.sceneGeneration < 1)
            {
                return ESAITestCapabilityResponseDto.Reject(
                    ESAITestStatusCode.InvalidRequest,
                    "Capability Request 缺少 runId、invocationId、stepId、Capability、命令或有效操作/场景代际。");
            }

            Entry entry;
            lock (gate)
            {
                if (!entries.TryGetValue(request.capabilityId, out entry))
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityUnavailable, "Capability 未注册：" + request.capabilityId);

                selectedProvider = entry.provider;

                if (!string.Equals(entry.runId, request.runId, StringComparison.Ordinal)
                    || entry.sceneGeneration != request.sceneGeneration)
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityUnavailable, "Capability 已越过 runId 或场景代际边界。");

                if (!IsCommandDeclared(entry.provider.Commands, request.command))
                    return ESAITestCapabilityResponseDto.Reject(
                        ESAITestStatusCode.CapabilityRejected,
                        "Capability 未声明命令：" + request.capabilityId + "/" + request.command);

                if (!executingCapabilities.Add(request.capabilityId))
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.RuntimeBusy, "Capability 正在处理另一项操作，拒绝重入：" + request.capabilityId);
            }

            try
            {
                return ValidateProviderResponse(entry.provider.Execute(request));
            }
            catch (Exception exception)
            {
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InternalError, exception.ToString());
            }
            finally
            {
                lock (gate)
                    executingCapabilities.Remove(request.capabilityId);
            }
        }

        private static ESAITestCapabilityResponseDto ValidateProviderResponse(ESAITestCapabilityResponseDto response)
        {
            if (response == null)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InternalError, "Capability 返回了空响应。");

            response.message = response.message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(response.statusCode))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InternalError, "Capability 响应缺少 statusCode。");

            if (!response.accepted)
            {
                if (response.conditionMet || response.retryable)
                    return ESAITestCapabilityResponseDto.Reject(
                        ESAITestStatusCode.InternalError,
                        "Capability 拒绝响应不能声明 conditionMet 或 retryable。");
                return response;
            }

            if (response.conditionMet)
            {
                if (response.retryable || !string.Equals(response.statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal))
                {
                    return ESAITestCapabilityResponseDto.Reject(
                        ESAITestStatusCode.InternalError,
                        "Capability 成功响应必须为 passed 且不可 retryable。");
                }
                return response;
            }

            if (!string.Equals(response.statusCode, ESAITestStatusCode.VerificationFailed, StringComparison.Ordinal))
            {
                return ESAITestCapabilityResponseDto.Reject(
                    ESAITestStatusCode.InternalError,
                    "Capability 未满足条件时必须返回 verification_failed。");
            }
            return response;
        }

        private static ESAITestCapabilityResponseDto StampResponseIdentity(
            ESAITestCapabilityResponseDto response,
            ESAITestCapabilityRequestDto request,
            ESAITestCapabilityProvider provider)
        {
            response = response ?? ESAITestCapabilityResponseDto.Reject(
                ESAITestStatusCode.InternalError,
                "Capability 返回了空响应。");
            response.schema = ESAITestProtocol.CapabilityResponseSchema;
            response.runId = request?.runId ?? string.Empty;
            response.sceneGeneration = request?.sceneGeneration ?? 0;
            response.invocationId = request?.invocationId ?? string.Empty;
            response.stepId = request?.stepId ?? string.Empty;
            response.capabilityId = request?.capabilityId ?? string.Empty;
            response.operation = request?.operation ?? string.Empty;
            response.command = request?.command ?? string.Empty;
            response.providerId = provider?.ProviderId ?? string.Empty;
            response.providerVersion = provider?.ProviderVersion ?? 0;
            return response;
        }

        private static bool IsSupportedOperation(string operation)
        {
            return string.Equals(operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation, ESAITestProtocol.OperationVerify, StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation, ESAITestProtocol.OperationWait, StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation, ESAITestProtocol.OperationAct, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCommandDeclared(string[] commands, string command)
        {
            if (commands == null || string.IsNullOrWhiteSpace(command))
                return false;

            for (int i = 0; i < commands.Length; i++)
                if (string.Equals(commands[i], command, StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static bool ValidateProvider(ESAITestCapabilityProvider provider, out string error)
        {
            error = string.Empty;
            if (provider == null
                || string.IsNullOrWhiteSpace(provider.CapabilityId)
                || string.IsNullOrWhiteSpace(provider.ProviderId)
                || provider.ProviderVersion < 1
                || provider.CapabilityId.Length > ESAITestProtocol.MaxIdentityLength
                || provider.ProviderId.Length > ESAITestProtocol.MaxIdentityLength)
            {
                error = "Capability Provider 的身份或版本无效。";
                return false;
            }

            string[] commands = provider.Commands;
            if (commands == null || commands.Length == 0)
            {
                error = "Capability Provider 必须声明至少一个命令：" + provider.CapabilityId;
                return false;
            }

            var declared = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < commands.Length; i++)
            {
                string command = commands[i];
                if (string.IsNullOrWhiteSpace(command)
                    || command.Length > ESAITestProtocol.MaxIdentityLength
                    || !declared.Add(command))
                {
                    error = "Capability Provider 声明了空、超长或重复命令：" + provider.CapabilityId;
                    return false;
                }
            }

            return true;
        }

        public ESAITestCapabilityManifestDto CreateManifest(string runId, int sceneGeneration)
        {
            var items = new List<ESAITestCapabilityManifestItemDto>();
            lock (gate)
            {
                items.Capacity = entries.Count;
                foreach (Entry entry in entries.Values)
                {
                    if (!string.Equals(entry.runId, runId, StringComparison.Ordinal)
                        || entry.sceneGeneration != sceneGeneration)
                        continue;

                    items.Add(new ESAITestCapabilityManifestItemDto
                    {
                        capabilityId = entry.provider.CapabilityId,
                        providerId = entry.provider.ProviderId,
                        providerVersion = entry.provider.ProviderVersion,
                        commands = CloneCommands(entry.provider.Commands),
                    });
                }
            }

            items.Sort((left, right) => string.CompareOrdinal(left.capabilityId, right.capabilityId));
            return new ESAITestCapabilityManifestDto
            {
                runId = runId,
                sceneGeneration = sceneGeneration,
                capabilities = items.ToArray(),
            };
        }

        private static string[] CloneCommands(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            var copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public static class ESAITestRuntime
    {
        public static bool IsActive { get; private set; }
        public static string RunId { get; private set; }
        public static int SceneGeneration { get; private set; }
        public static ESAITestCapabilityRegistry Registry { get; private set; }

        public static event Action Activated;
        public static event Action SceneGenerationChanged;
        public static event Action Deactivated;

        internal static bool TryActivate(string runId, out string error)
        {
            if (IsActive)
            {
                error = "已有 ESAITest Run 占用运行时：" + RunId;
                return false;
            }

            RunId = runId;
            SceneGeneration = 1;
            Registry = new ESAITestCapabilityRegistry();
            IsActive = true;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Activated?.Invoke();
            error = string.Empty;
            return true;
        }

        internal static void Deactivate(string runId)
        {
            if (!IsActive || !string.Equals(RunId, runId, StringComparison.Ordinal))
                return;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            IsActive = false;
            RunId = null;
            Registry = null;
            SceneGeneration = 0;
            Deactivated?.Invoke();
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            SceneGeneration++;
            Registry?.RemoveStale(SceneGeneration);
            SceneGenerationChanged?.Invoke();
        }
    }

    public sealed class ESAITestRunner : MonoBehaviour
    {
        private readonly List<ESAITestStepResultDto> stepResults = new List<ESAITestStepResultDto>(16);
        private readonly List<ESAITestEventDto> events = new List<ESAITestEventDto>(32);
        private bool cancellationRequested;
        private Stopwatch stopwatch;
        private ESAITestRequestDto request;
        private ESAITestResultDto result;
        private bool hasStarted;
        private bool ownsRuntime;
        private long nextInvocationSequence;
        private readonly Queue<ESAITestAutonomyDecisionDto> autonomyDecisions = new Queue<ESAITestAutonomyDecisionDto>(ESAITestProtocol.MaximumQueuedAutonomyDecisions);
        private readonly object autonomyGate = new object();
        private int autonomyNextTurn = 1;
        private int autonomyDecisionCount;
        private int autonomyRejectedDecisionCount;
        private int autonomyConsecutiveFailures;
        private string autonomyLastDecisionId = string.Empty;
        private bool autonomyWaitingForDecision;
        private string autonomyBridgeFailureStatusCode = string.Empty;
        private string autonomyBridgeFailureMessage = string.Empty;
        private ESAITestAutonomyBridgeDiagnosticsDto autonomyBridgeDiagnostics;

        public ESAITestResultDto Result => result;
        public bool IsRunning { get; private set; }
        public bool CancellationRequested => cancellationRequested;
        public string CurrentStepId { get; private set; }
        public string CurrentOperation { get; private set; }
        public string CurrentMessage { get; private set; }
        public int CompletedStepCount => stepResults.Count;
        public int TotalStepCount => request?.plan?.steps?.Length ?? 0;
        public float ElapsedSeconds => stopwatch == null ? 0f : (float)stopwatch.Elapsed.TotalSeconds;
        public string RunId => request?.runId ?? string.Empty;
        public string PlanId => request?.plan?.planId ?? string.Empty;
        public bool IsAutonomyEnabled => request?.autonomy != null;
        public bool AutonomyWaitingForDecision => autonomyWaitingForDecision;
        public int AutonomyTurn => Mathf.Max(0, autonomyNextTurn - 1);
        public string AutonomyGoal => request?.autonomy?.goal ?? string.Empty;
        public ESAITestAutonomyExternalBridgeConfigDto AutonomyExternalBridgeConfig => request?.autonomy?.externalBridge;
        public ESAITestAutonomyBridgeDiagnosticsDto AutonomyBridgeDiagnostics => autonomyBridgeDiagnostics;
        public event Action<ESAITestResultDto> Completed;
        public event Action StateChanged;

        public void Begin(ESAITestRequestDto runRequest)
        {
            if (IsRunning || hasStarted)
                throw new InvalidOperationException("ESAITestRunner 是一次性 Runner，不能重复执行。");

            hasStarted = true;
            request = runRequest;
            StartCoroutine(Run());
        }

        public void Cancel()
        {
            cancellationRequested = true;
            CurrentMessage = "已请求取消，等待当前安全点。";
            StateChanged?.Invoke();
        }

        public bool SubmitAutonomyDecision(ESAITestAutonomyDecisionDto decision, out string error)
        {
            error = string.Empty;
            if (!IsAutonomyEnabled || result != null || !hasStarted)
            {
                error = "当前 Runner 没有可接收决策的自主会话。";
                return false;
            }
            if (decision == null || decision.protocolVersion != ESAITestProtocol.CurrentVersion)
            {
                error = "自主决策为空或协议版本不支持。";
                return false;
            }
            if (!string.Equals(decision.runId, RunId, StringComparison.Ordinal)
                || decision.turnIndex != autonomyNextTurn
                || string.IsNullOrWhiteSpace(decision.decisionId)
                || decision.decisionId.Length > ESAITestProtocol.MaxIdentityLength)
            {
                error = "自主决策的 RunId、turnIndex 或 decisionId 不匹配；当前 turn=" + autonomyNextTurn;
                return false;
            }
            if (!string.Equals(decision.mode, "goal", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(decision.mode, "explore", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(decision.mode, "recover", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(decision.mode, "stop", StringComparison.OrdinalIgnoreCase))
            {
                error = "自主决策 mode 必须是 goal、explore、recover 或 stop。";
                return false;
            }
            if (!IsWithinLength(decision.rationale, ESAITestProtocol.MaxTextLength)
                || (string.Equals(decision.mode, "stop", StringComparison.OrdinalIgnoreCase) && !decision.terminal)
                || (decision.terminal
                    && !string.IsNullOrWhiteSpace(decision.terminalStatusCode)
                    && decision.terminalStatusCode != ESAITestStatusCode.Passed
                    && decision.terminalStatusCode != ESAITestStatusCode.Failed))
            {
                error = "自主决策的 rationale、stop 模式或终止状态无效。";
                return false;
            }
            ESAITestStepDto[] steps = decision.steps ?? Array.Empty<ESAITestStepDto>();
            if (!decision.terminal && (steps.Length == 0 || steps.Length > ESAITestProtocol.MaximumAutonomyStepsPerDecision))
            {
                error = "非终止自主决策必须包含 1 到 " + ESAITestProtocol.MaximumAutonomyStepsPerDecision + " 个 Step。";
                return false;
            }
            if (decision.terminal && steps.Length > ESAITestProtocol.MaximumAutonomyStepsPerDecision)
            {
                error = "终止自主决策的 Step 数量不能超过 " + ESAITestProtocol.MaximumAutonomyStepsPerDecision + "。";
                return false;
            }
            if (string.Equals(decision.mode, "explore", StringComparison.OrdinalIgnoreCase)
                && request.autonomy != null && !request.autonomy.allowExploration)
            {
                error = "当前自主会话未允许探索模式。";
                return false;
            }

            lock (autonomyGate)
            {
                if (autonomyDecisions.Count >= ESAITestProtocol.MaximumQueuedAutonomyDecisions)
                {
                    error = "自主决策队列已满，必须等待当前回合消费。";
                    return false;
                }
                autonomyDecisions.Enqueue(CloneAutonomyDecision(decision));
            }
            StateChanged?.Invoke();
            return true;
        }

        internal void UpdateAutonomyBridgeDiagnostics(ESAITestAutonomyBridgeDiagnosticsDto diagnostics)
        {
            autonomyBridgeDiagnostics = CloneAutonomyBridgeDiagnostics(diagnostics);
            StateChanged?.Invoke();
        }

        internal bool FailAutonomyBridge(string statusCode, string message)
        {
            if (!IsAutonomyEnabled || result != null || !hasStarted || !string.IsNullOrEmpty(autonomyBridgeFailureStatusCode))
                return false;

            autonomyBridgeFailureStatusCode = string.IsNullOrWhiteSpace(statusCode)
                ? ESAITestStatusCode.AutonomyBridgeLaunchFailed
                : statusCode;
            autonomyBridgeFailureMessage = message ?? string.Empty;
            CurrentMessage = autonomyBridgeFailureMessage;
            Emit(null, "autonomy_bridge_failed", autonomyBridgeFailureStatusCode, autonomyBridgeFailureMessage, null);
            return true;
        }

        public void CopyRecentEvents(List<ESAITestEventDto> target, int maxCount)
        {
            if (target == null)
                return;
            target.Clear();
            int start = Mathf.Max(0, events.Count - Mathf.Max(0, maxCount));
            for (int i = start; i < events.Count; i++)
                target.Add(events[i]);
        }

        private IEnumerator Run()
        {
            IsRunning = true;
            cancellationRequested = false;
            stopwatch = Stopwatch.StartNew();
            long startedUtcTicks = DateTime.UtcNow.Ticks;
            bool hasContinuedFailure = false;

            string validationCode = ValidateRequest(request, out string validationMessage);
            if (validationCode != null)
            {
                Complete(validationCode, validationMessage, startedUtcTicks);
                yield break;
            }

            if (!ESAITestRuntime.TryActivate(request.runId, out string activationError))
            {
                Complete(ESAITestStatusCode.RuntimeBusy, activationError, startedUtcTicks);
                yield break;
            }

            ownsRuntime = true;
            yield return null;
            Emit(null, "run_started", ESAITestStatusCode.Passed, "确定性计划开始执行。", null);

            ESAITestStepDto[] steps = request.plan.steps ?? Array.Empty<ESAITestStepDto>();
            for (int i = 0; i < steps.Length; i++)
            {
                if (cancellationRequested)
                {
                    Complete(ESAITestStatusCode.Cancelled, "运行已取消。", startedUtcTicks);
                    yield break;
                }

                if (stopwatch.Elapsed.TotalSeconds > request.plan.totalTimeoutSeconds)
                {
                    Complete(ESAITestStatusCode.TotalTimeout, "运行超过总超时。", startedUtcTicks);
                    yield break;
                }

                ESAITestStepDto step = steps[i];
                yield return ExecuteStep(step);
                ESAITestStepResultDto stepResult = stepResults[stepResults.Count - 1];
                if (string.Equals(stepResult.statusCode, ESAITestStatusCode.Cancelled, StringComparison.Ordinal))
                {
                    Complete(ESAITestStatusCode.Cancelled, "运行已取消。", startedUtcTicks);
                    yield break;
                }

                if (string.Equals(stepResult.statusCode, ESAITestStatusCode.TotalTimeout, StringComparison.Ordinal))
                {
                    Complete(ESAITestStatusCode.TotalTimeout, "运行超过总超时。", startedUtcTicks);
                    yield break;
                }

                if (!string.Equals(stepResult.statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal))
                {
                    if (!step.continueOnFailure)
                    {
                        Complete(stepResult.statusCode, "Step 失败：" + step.stepId, startedUtcTicks);
                        yield break;
                    }

                    hasContinuedFailure = true;
                }
            }

            if (request.autonomy != null)
            {
                yield return RunAutonomyLoop(startedUtcTicks);
                yield break;
            }

            Complete(
                hasContinuedFailure ? ESAITestStatusCode.Failed : ESAITestStatusCode.Passed,
                hasContinuedFailure ? "计划执行完成，但存在失败 Step。" : "计划执行完成。",
                startedUtcTicks);
        }

        private IEnumerator RunAutonomyLoop(long startedUtcTicks)
        {
            ESAITestAutonomyConfigDto autonomy = request.autonomy;
            while (true)
            {
                if (cancellationRequested)
                {
                    Complete(ESAITestStatusCode.Cancelled, "自主会话已取消。", startedUtcTicks);
                    yield break;
                }
                if (!string.IsNullOrEmpty(autonomyBridgeFailureStatusCode))
                {
                    Complete(autonomyBridgeFailureStatusCode, autonomyBridgeFailureMessage, startedUtcTicks);
                    yield break;
                }
                if (stopwatch.Elapsed.TotalSeconds > request.plan.totalTimeoutSeconds
                    || stopwatch.Elapsed.TotalSeconds > autonomy.maxDurationSeconds)
                {
                    Complete(ESAITestStatusCode.TotalTimeout, "自主会话超过时间预算。", startedUtcTicks);
                    yield break;
                }
                if (autonomyNextTurn > autonomy.maxTurns)
                {
                    Complete(ESAITestStatusCode.AutonomyTurnLimit,
                        "自主会话达到最大决策回合：" + autonomy.maxTurns, startedUtcTicks);
                    yield break;
                }

                ESAITestAutonomyDecisionDto decision = null;
                lock (autonomyGate)
                {
                    if (autonomyDecisions.Count > 0)
                        decision = autonomyDecisions.Dequeue();
                }
                if (decision == null)
                {
                    if (!autonomyWaitingForDecision)
                    {
                        autonomyWaitingForDecision = true;
                        CurrentMessage = "等待 AI 提交下一回合决策。";
                        Emit(null, "autonomy_waiting_for_decision", ESAITestStatusCode.AutonomyWaitingForDecision,
                            "goal=" + autonomy.goal + " | turn=" + autonomyNextTurn, null);
                    }
                    yield return null;
                    continue;
                }

                autonomyWaitingForDecision = false;
                autonomyDecisionCount++;
                autonomyLastDecisionId = decision.decisionId;
                autonomyNextTurn++;
                string appendCode = AppendAutonomyDecision(decision, out string appendMessage);
                if (appendCode != null)
                {
                    autonomyRejectedDecisionCount++;
                    Emit(null, "autonomy_decision_rejected", ESAITestStatusCode.AutonomyDecisionRejected,
                        appendMessage, null);
                    if (autonomyRejectedDecisionCount >= autonomy.maxConsecutiveFailures)
                    {
                        Complete(ESAITestStatusCode.AutonomyDecisionRejected,
                            "连续拒绝自主决策，停止会话：" + appendMessage, startedUtcTicks);
                        yield break;
                    }
                    continue;
                }

                bool decisionHadFailure = false;
                ESAITestStepDto[] decisionSteps = decision.steps ?? Array.Empty<ESAITestStepDto>();
                for (int i = 0; i < decisionSteps.Length; i++)
                {
                    if (cancellationRequested)
                    {
                        Complete(ESAITestStatusCode.Cancelled, "自主会话已取消。", startedUtcTicks);
                        yield break;
                    }
                    yield return ExecuteStep(decisionSteps[i]);
                    ESAITestStepResultDto stepResult = stepResults[stepResults.Count - 1];
                    if (string.Equals(stepResult.statusCode, ESAITestStatusCode.TotalTimeout, StringComparison.Ordinal))
                    {
                        Complete(ESAITestStatusCode.TotalTimeout, "运行超过总超时。", startedUtcTicks);
                        yield break;
                    }
                    if (string.Equals(stepResult.statusCode, ESAITestStatusCode.Cancelled, StringComparison.Ordinal))
                    {
                        Complete(ESAITestStatusCode.Cancelled, "自主会话已取消。", startedUtcTicks);
                        yield break;
                    }
                    if (!string.Equals(stepResult.statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal))
                    {
                        decisionHadFailure = true;
                        if (!decisionSteps[i].continueOnFailure)
                            break;
                    }
                }

                autonomyConsecutiveFailures = decisionHadFailure ? autonomyConsecutiveFailures + 1 : 0;
                if (autonomyConsecutiveFailures >= autonomy.maxConsecutiveFailures)
                {
                    Complete(ESAITestStatusCode.AutonomyStuck,
                        "自主会话连续失败，进入停止保护；请切换 recover 或调整目标。", startedUtcTicks);
                    yield break;
                }

                if (decision.terminal)
                {
                    bool businessEvidenceSatisfied = true;
                    if (!decisionHadFailure && autonomy.requireBusinessVerification)
                    {
                        LinkUseReceiptsToFollowupVerification();
                        businessEvidenceSatisfied = !HasUnverifiedBusinessUseReceipt();
                    }
                    string terminalCode = string.Equals(decision.terminalStatusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal)
                        && !decisionHadFailure
                        && businessEvidenceSatisfied
                        ? ESAITestStatusCode.Passed
                        : ESAITestStatusCode.Failed;
                    Complete(terminalCode,
                        terminalCode == ESAITestStatusCode.Passed
                            ? "AI 声明目标已完成，并且本回合无失败 Step。"
                            : businessEvidenceSatisfied
                                ? "AI 结束自主会话，但目标未被证明完成。"
                                : "AI 结束自主会话，但存在未完成业务效果验证。",
                        startedUtcTicks);
                    yield break;
                }
            }
        }

        private string AppendAutonomyDecision(ESAITestAutonomyDecisionDto decision, out string message)
        {
            message = string.Empty;
            ESAITestStepDto[] added = decision.steps ?? Array.Empty<ESAITestStepDto>();
            if (added.Length == 0)
                return null;

            ESAITestStepDto[] original = request.plan.steps ?? Array.Empty<ESAITestStepDto>();
            var combined = new List<ESAITestStepDto>(original.Length + added.Length);
            combined.AddRange(original);
            combined.AddRange(added);
            request.plan.steps = combined.ToArray();
            string code = ValidateRequest(request, out message);
            if (code != null)
            {
                request.plan.steps = original;
                return code;
            }
            return null;
        }

        private IEnumerator ExecuteStep(ESAITestStepDto step)
        {
            double startedSeconds = stopwatch.Elapsed.TotalSeconds;
            CurrentStepId = step.stepId ?? string.Empty;
            CurrentOperation = (step.operation ?? string.Empty) + "/" + (step.command ?? string.Empty);
            CurrentMessage = "正在执行。";
            StateChanged?.Invoke();
            Emit(step.stepId, "step_started", ESAITestStatusCode.Passed, step.operation + "/" + step.command, null);

            string operation = step.operation ?? string.Empty;
            bool waitMode = string.Equals(operation, ESAITestProtocol.OperationWait, StringComparison.OrdinalIgnoreCase);
            bool retryableSeeMode = string.Equals(operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase);
            string providerOperation = waitMode ? ESAITestProtocol.OperationVerify : operation;
            float timeoutSeconds = Mathf.Max(0.01f, step.timeoutSeconds);
            float pollSeconds = Mathf.Max(0.01f, step.pollIntervalSeconds);
            ESAITestCapabilityResponseDto response = null;
            int capabilityCallCount = 0;
            float totalCapabilityCallMilliseconds = 0f;
            float maxCapabilityCallMilliseconds = 0f;
            long firstCapabilityCallUtcTicks = 0L;
            long lastCapabilityCallUtcTicks = 0L;
            string firstInvocationId = null;
            string lastInvocationId = null;
            int firstSceneGeneration = 0;
            int lastSceneGeneration = 0;
            bool sceneGenerationChangedDuringCall = false;

            while (true)
            {
                if (cancellationRequested)
                {
                    AddStepResult(step, ESAITestStatusCode.Cancelled, "Step 已取消。", startedSeconds, response?.value,
                        CreateCapabilityCallDiagnostic(response, capabilityCallCount, totalCapabilityCallMilliseconds,
                            maxCapabilityCallMilliseconds, firstCapabilityCallUtcTicks, lastCapabilityCallUtcTicks,
                            firstInvocationId, lastInvocationId, firstSceneGeneration, lastSceneGeneration,
                            sceneGenerationChangedDuringCall));
                    yield break;
                }

                double stepElapsed = stopwatch.Elapsed.TotalSeconds - startedSeconds;
                if (stepElapsed > timeoutSeconds)
                {
                    AddStepResult(step, ESAITestStatusCode.StepTimeout, "Step 超时。", startedSeconds, response?.value,
                        CreateCapabilityCallDiagnostic(response, capabilityCallCount, totalCapabilityCallMilliseconds,
                            maxCapabilityCallMilliseconds, firstCapabilityCallUtcTicks, lastCapabilityCallUtcTicks,
                            firstInvocationId, lastInvocationId, firstSceneGeneration, lastSceneGeneration,
                            sceneGenerationChangedDuringCall));
                    yield break;
                }

                if (stopwatch.Elapsed.TotalSeconds > request.plan.totalTimeoutSeconds)
                {
                    AddStepResult(step, ESAITestStatusCode.TotalTimeout, "运行超过总超时。", startedSeconds, response?.value,
                        CreateCapabilityCallDiagnostic(response, capabilityCallCount, totalCapabilityCallMilliseconds,
                            maxCapabilityCallMilliseconds, firstCapabilityCallUtcTicks, lastCapabilityCallUtcTicks,
                            firstInvocationId, lastInvocationId, firstSceneGeneration, lastSceneGeneration,
                            sceneGenerationChangedDuringCall));
                    yield break;
                }

                if (capabilityCallCount >= ESAITestProtocol.MaximumCapabilityCallsPerStep)
                {
                    AddStepResult(step, ESAITestStatusCode.CallBudgetExceeded,
                        "Step 超过 Capability 调用预算：" + ESAITestProtocol.MaximumCapabilityCallsPerStep,
                        startedSeconds, response?.value,
                        CreateCapabilityCallDiagnostic(response, capabilityCallCount, totalCapabilityCallMilliseconds,
                            maxCapabilityCallMilliseconds, firstCapabilityCallUtcTicks, lastCapabilityCallUtcTicks,
                            firstInvocationId, lastInvocationId, firstSceneGeneration, lastSceneGeneration,
                            sceneGenerationChangedDuringCall));
                    yield break;
                }

                long callStartedUtcTicks = DateTime.UtcNow.Ticks;
                double callStartedSeconds = stopwatch.Elapsed.TotalSeconds;
                capabilityCallCount++;
                string invocationId = NextInvocationId();
                int callSceneGeneration = ESAITestRuntime.SceneGeneration;
                if (firstCapabilityCallUtcTicks == 0L)
                {
                    firstCapabilityCallUtcTicks = callStartedUtcTicks;
                    firstInvocationId = invocationId;
                    firstSceneGeneration = callSceneGeneration;
                }
                lastInvocationId = invocationId;
                lastSceneGeneration = callSceneGeneration;
                response = ESAITestRuntime.Registry.Execute(new ESAITestCapabilityRequestDto
                {
                    runId = request.runId,
                    sceneGeneration = callSceneGeneration,
                    invocationId = invocationId,
                    stepId = step.stepId,
                    capabilityId = step.capabilityId,
                    operation = providerOperation,
                    command = step.command,
                    target = step.target,
                    expectedValue = step.expectedValue,
                    arguments = step.arguments ?? Array.Empty<ESAITestArgumentDto>(),
                });
                if (callSceneGeneration != ESAITestRuntime.SceneGeneration)
                {
                    sceneGenerationChangedDuringCall = true;
                    response = CreateSceneGenerationChangedResponse(response, callSceneGeneration, ESAITestRuntime.SceneGeneration);
                }
                lastCapabilityCallUtcTicks = DateTime.UtcNow.Ticks;
                float callMilliseconds = (float)((stopwatch.Elapsed.TotalSeconds - callStartedSeconds) * 1000d);
                totalCapabilityCallMilliseconds += callMilliseconds;
                maxCapabilityCallMilliseconds = Mathf.Max(maxCapabilityCallMilliseconds, callMilliseconds);
                ESAITestCapabilityCallDiagnosticDto capabilityCalls = CreateCapabilityCallDiagnostic(
                    response,
                    capabilityCallCount,
                    totalCapabilityCallMilliseconds,
                    maxCapabilityCallMilliseconds,
                    firstCapabilityCallUtcTicks,
                    lastCapabilityCallUtcTicks,
                    firstInvocationId,
                    lastInvocationId,
                    firstSceneGeneration,
                    lastSceneGeneration,
                    sceneGenerationChangedDuringCall);

                if (!response.accepted)
                {
                    AddStepResult(step, response.statusCode, response.message, startedSeconds, response.value, capabilityCalls);
                    yield break;
                }

                if (response.conditionMet)
                {
                    AddStepResult(step, ESAITestStatusCode.Passed, response.message, startedSeconds, response.value, capabilityCalls);
                    yield break;
                }

                if (!response.retryable || (!waitMode && !retryableSeeMode))
                {
                    AddStepResult(step, ESAITestStatusCode.VerificationFailed, response.message, startedSeconds, response.value, capabilityCalls);
                    yield break;
                }

                float until = Time.realtimeSinceStartup + pollSeconds;
                while (Time.realtimeSinceStartup < until)
                    yield return null;
            }
        }

        private static ESAITestCapabilityCallDiagnosticDto CreateCapabilityCallDiagnostic(
            ESAITestCapabilityResponseDto response,
            int callCount,
            float totalDurationMilliseconds,
            float maxDurationMilliseconds,
            long firstCallUtcTicks,
            long lastCallUtcTicks,
            string firstInvocationId,
            string lastInvocationId,
            int firstSceneGeneration,
            int lastSceneGeneration,
            bool sceneGenerationChangedDuringCall)
        {
            return new ESAITestCapabilityCallDiagnosticDto
            {
                callCount = callCount,
                retryCount = Mathf.Max(0, callCount - 1),
                firstInvocationId = firstInvocationId ?? string.Empty,
                lastInvocationId = lastInvocationId ?? string.Empty,
                firstSceneGeneration = firstSceneGeneration,
                lastSceneGeneration = lastSceneGeneration,
                sceneGenerationChangedDuringCall = sceneGenerationChangedDuringCall,
                firstCallUtcTicks = firstCallUtcTicks,
                lastCallUtcTicks = lastCallUtcTicks,
                totalDurationMilliseconds = totalDurationMilliseconds,
                maxDurationMilliseconds = maxDurationMilliseconds,
                lastAccepted = response != null && response.accepted,
                lastConditionMet = response != null && response.conditionMet,
                lastRetryable = response != null && response.retryable,
                lastStatusCode = response?.statusCode ?? string.Empty,
                lastMessage = response?.message ?? string.Empty,
                lastResponse = response,
            };
        }

        private string NextInvocationId()
        {
            nextInvocationSequence++;
            return nextInvocationSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static ESAITestCapabilityResponseDto CreateSceneGenerationChangedResponse(
            ESAITestCapabilityResponseDto original,
            int requestSceneGeneration,
            int activeSceneGeneration)
        {
            return new ESAITestCapabilityResponseDto
            {
                accepted = false,
                conditionMet = false,
                retryable = false,
                statusCode = ESAITestStatusCode.CapabilityUnavailable,
                message = "Capability 调用跨越场景代际，结果不作为当前场景的有效证据：request="
                    + requestSceneGeneration + " active=" + activeSceneGeneration,
                value = original?.value,
            };
        }

        private void AddStepResult(
            ESAITestStepDto step,
            string code,
            string message,
            double startedSeconds,
            ESAITestValueDto value,
            ESAITestCapabilityCallDiagnosticDto capabilityCalls)
        {
            var stepResult = new ESAITestStepResultDto
            {
                stepId = step.stepId,
                statusCode = code,
                message = message ?? string.Empty,
                elapsedSeconds = (float)(stopwatch.Elapsed.TotalSeconds - startedSeconds),
                value = value,
                capabilityCalls = capabilityCalls,
            };
            stepResults.Add(stepResult);
            CurrentMessage = stepResult.message;
            Emit(step.stepId, "step_completed", code, stepResult.message, value);
        }

        private void Emit(string stepId, string eventType, string code, string message, ESAITestValueDto value)
        {
            events.Add(new ESAITestEventDto
            {
                runId = request?.runId,
                stepId = stepId,
                eventType = eventType,
                statusCode = code,
                message = message ?? string.Empty,
                utcTicks = DateTime.UtcNow.Ticks,
                elapsedSeconds = stopwatch == null ? 0f : (float)stopwatch.Elapsed.TotalSeconds,
                value = value,
            });
            StateChanged?.Invoke();
        }

        private void Complete(string code, string message, long startedUtcTicks)
        {
            if (result != null)
                return;

            stopwatch.Stop();
            int exitCode = ToExitCode(code);
            LinkUseReceiptsToFollowupVerification();
            Emit(null, "run_completed", code, message, null);
            ESAITestRunDiagnosticsDto diagnostics = BuildDiagnostics(code, message);
            ESAITestCapabilityManifestDto manifest = ownsRuntime
                ? ESAITestRuntime.Registry?.CreateManifest(request?.runId, ESAITestRuntime.SceneGeneration)
                : null;
            ReleaseRuntime();
            result = new ESAITestResultDto
            {
                runId = request?.runId,
                planId = request?.plan?.planId,
                statusCode = code,
                message = message ?? string.Empty,
                executionStatusCode = code,
                executionMessage = message ?? string.Empty,
                reportStatusCode = ESAITestReportStatusCode.Pending,
                reportMessage = "报告尚未写入。",
                startedUtcTicks = startedUtcTicks,
                completedUtcTicks = DateTime.UtcNow.Ticks,
                elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds,
                exitCode = exitCode,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                productName = Application.productName,
                applicationVersion = Application.version,
                activeScene = SceneManager.GetActiveScene().name,
                totalStepCount = request?.plan?.steps?.Length ?? 0,
                passedStepCount = CountSteps(ESAITestStatusCode.Passed),
                failedStepCount = CountFailedSteps(),
                firstFailedStepId = FindFirstFailedStepId(),
                request = request,
                manifest = manifest,
                steps = stepResults.ToArray(),
                events = events.ToArray(),
                diagnostics = diagnostics,
            };
            IsRunning = false;
            CurrentMessage = result.message;
            StateChanged?.Invoke();
            Completed?.Invoke(result);
        }

        private int CountSteps(string statusCode)
        {
            int count = 0;
            for (int i = 0; i < stepResults.Count; i++)
                if (string.Equals(stepResults[i].statusCode, statusCode, StringComparison.Ordinal))
                    count++;
            return count;
        }

        private int CountFailedSteps()
        {
            return stepResults.Count - CountSteps(ESAITestStatusCode.Passed);
        }

        private string FindFirstFailedStepId()
        {
            for (int i = 0; i < stepResults.Count; i++)
                if (!string.Equals(stepResults[i].statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal))
                    return stepResults[i].stepId ?? string.Empty;
            return string.Empty;
        }

        private ESAITestRunDiagnosticsDto BuildDiagnostics(string code, string message)
        {
            ESAITestStepResultDto firstFailed = FindFirstFailedStepResult();
            ESAITestStepResultDto lastCompleted = stepResults.Count == 0 ? null : stepResults[stepResults.Count - 1];
            ESAITestScreenCaptureDto screenshot = ESAITestObservationRuntimeState.LatestScreenshot;
            return new ESAITestRunDiagnosticsDto
            {
                runId = RunId,
                executionStatusCode = code ?? string.Empty,
                executionMessage = message ?? string.Empty,
                reportStatusCode = ESAITestReportStatusCode.Pending,
                reportMessage = "报告尚未写入。",
                currentStepId = CurrentStepId ?? string.Empty,
                currentOperation = CurrentOperation ?? string.Empty,
                completedStepCount = CompletedStepCount,
                totalStepCount = TotalStepCount,
                eventCount = events.Count,
                capabilityCallCount = CountCapabilityCalls(),
                capabilityRetryCount = CountCapabilityRetries(),
                capabilityCallMilliseconds = SumCapabilityCallMilliseconds(),
                maxCapabilityCallMilliseconds = FindMaxCapabilityCallMilliseconds(),
                suggestedInvestigation = SuggestInvestigation(code),
                firstFailedStep = CreateStepDiagnostic(firstFailed),
                lastCompletedStep = CreateStepDiagnostic(lastCompleted),
                lastActivityEvent = FindLastActivityEvent(),
                lastObservation = new ESAITestObservationDiagnosticDto
                {
                    command = ESAITestObservationRuntimeState.LastCommand ?? string.Empty,
                    observedUtcTicks = ESAITestObservationRuntimeState.LastObservedUtcTicks,
                    attentionProfile = ESAITestObservationRuntimeState.LastAttentionProfile ?? string.Empty,
                    uiCount = ESAITestObservationRuntimeState.LastUiCount,
                    sceneObjectCount = ESAITestObservationRuntimeState.LastSceneObjectCount,
                    samplingCostMilliseconds = ESAITestObservationRuntimeState.LastSamplingCostMilliseconds,
                    latestScreenshotPath = screenshot == null ? string.Empty : screenshot.relativePath ?? string.Empty,
                },
                promptQueue = new ESAITestPromptQueueDiagnosticDto
                {
                    pendingCount = ESAITestAIPrompt.PendingCount,
                    highestPendingPriority = ESAITestAIPrompt.HighestPendingPriority ?? string.Empty,
                    lastConsumedPrompt = ESAITestObservationRuntimeState.LastConsumedPrompt,
                },
                autonomyEnabled = request?.autonomy != null,
                autonomyGoal = request?.autonomy?.goal ?? string.Empty,
                autonomyTurn = AutonomyTurn,
                autonomyDecisionCount = autonomyDecisionCount,
                autonomyRejectedDecisionCount = autonomyRejectedDecisionCount,
                autonomyConsecutiveFailures = autonomyConsecutiveFailures,
                autonomyLastDecisionId = autonomyLastDecisionId ?? string.Empty,
                autonomyWaitingForDecision = autonomyWaitingForDecision,
                autonomyBridge = CloneAutonomyBridgeDiagnostics(autonomyBridgeDiagnostics),
                conversation = CloneConversationReceipt(ESAITestConversationRuntimeState.LastReceipt),
            };
        }

        private static ESAITestConversationReceiptDto CloneConversationReceipt(ESAITestConversationReceiptDto source)
        {
            if (source == null)
                return null;
            return new ESAITestConversationReceiptDto
            {
                protocolVersion = source.protocolVersion,
                requestId = source.requestId ?? string.Empty,
                stage = source.stage ?? string.Empty,
                statusCode = source.statusCode ?? string.Empty,
                source = source.source ?? string.Empty,
                originalText = source.originalText ?? string.Empty,
                normalizedText = source.normalizedText ?? string.Empty,
                intent = source.intent ?? string.Empty,
                parsedMessage = source.parsedMessage ?? string.Empty,
                parsedGoal = source.parsedGoal ?? string.Empty,
                parsedPriority = source.parsedPriority ?? string.Empty,
                parsedTtlSeconds = source.parsedTtlSeconds,
                confidence = source.confidence,
                boundRunId = source.boundRunId ?? string.Empty,
                runId = source.runId ?? string.Empty,
                promptId = source.promptId ?? string.Empty,
                error = source.error ?? string.Empty,
                verificationState = source.verificationState ?? string.Empty,
                utcTicks = source.utcTicks,
            };
        }

        private static ESAITestAutonomyBridgeDiagnosticsDto CloneAutonomyBridgeDiagnostics(
            ESAITestAutonomyBridgeDiagnosticsDto source)
        {
            if (source == null)
                return null;
            return new ESAITestAutonomyBridgeDiagnosticsDto
            {
                protocolVersion = source.protocolVersion,
                autoLaunchRequested = source.autoLaunchRequested,
                launcherId = source.launcherId ?? string.Empty,
                state = source.state ?? string.Empty,
                externalProcessId = source.externalProcessId,
                launchUtcTicks = source.launchUtcTicks,
                readyUtcTicks = source.readyUtcTicks,
                lastHeartbeatUtcTicks = source.lastHeartbeatUtcTicks,
                requestsPublished = source.requestsPublished,
                decisionsAccepted = source.decisionsAccepted,
                decisionsRejected = source.decisionsRejected,
                decisionReadFailures = source.decisionReadFailures,
                lastStatusCode = source.lastStatusCode ?? string.Empty,
                lastMessage = source.lastMessage ?? string.Empty,
            };
        }

        private ESAITestStepResultDto FindFirstFailedStepResult()
        {
            for (int i = 0; i < stepResults.Count; i++)
                if (!string.Equals(stepResults[i].statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal))
                    return stepResults[i];
            return null;
        }

        private void LinkUseReceiptsToFollowupVerification()
        {
            for (int i = 0; i < stepResults.Count; i++)
            {
                ESAITestStepResultDto useStep = stepResults[i];
                ESAITestStepDto useDefinition = FindStepDefinition(useStep?.stepId);
                if (!TryGetUseReceipt(useStep?.value, out ESAITestUseResultDto receipt)
                    || !MatchesUseReceipt(receipt, useDefinition, useStep))
                    continue;

                ESAITestStepResultDto verification = FindFollowupVerification(i, useStep.stepId, out ESAITestStepDto verificationDefinition);
                if (verification == null)
                    continue;

                receipt.followupVerificationStepId = verification.stepId ?? string.Empty;
                receipt.followupVerificationStatusCode = verification.statusCode ?? string.Empty;
                receipt.followupVerificationMessage = verification.message ?? string.Empty;
                receipt.followupVerificationEvidenceMatched = TryGetVerifyEvidence(verification.value, out ESAITestVerifyResultDto evidence)
                    && MatchesVerifyEvidence(evidence, verificationDefinition, verification);
                receipt.businessEffectVerified = receipt.followupVerificationEvidenceMatched
                    && evidence.passed
                    && string.Equals(verification.statusCode, ESAITestStatusCode.Passed, StringComparison.Ordinal);
                receipt.followupVerificationEvidenceFailure = receipt.followupVerificationEvidenceMatched
                    ? string.Empty
                    : "后续 verify/wait Step 未返回与请求身份匹配的 ESAITestVerifyResultDto。";
                useStep.value.stringValue = JsonUtility.ToJson(receipt);
            }
        }

        private bool HasUnverifiedBusinessUseReceipt()
        {
            for (int i = 0; i < stepResults.Count; i++)
            {
                ESAITestStepResultDto step = stepResults[i];
                if (!TryGetUseReceipt(step?.value, out ESAITestUseResultDto receipt))
                    continue;
                if (string.Equals(receipt.command, "control.acquire", StringComparison.Ordinal)
                    || string.Equals(receipt.command, "control.release", StringComparison.Ordinal)
                    || string.Equals(receipt.command, "action.clear", StringComparison.Ordinal))
                    continue;
                if (!receipt.businessEffectVerified)
                    return true;
            }
            return false;
        }

        private bool TryGetUseReceipt(ESAITestValueDto value, out ESAITestUseResultDto receipt)
        {
            receipt = null;
            if (value == null || !string.Equals(value.kind, "string", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.stringValue))
                return false;

            try
            {
                receipt = JsonUtility.FromJson<ESAITestUseResultDto>(value.stringValue);
                return receipt != null && string.Equals(receipt.schema, ESAITestUseResultDto.Schema, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                receipt = null;
                return false;
            }
        }

        private static bool TryGetVerifyEvidence(ESAITestValueDto value, out ESAITestVerifyResultDto evidence)
        {
            evidence = null;
            if (value == null || !string.Equals(value.kind, "string", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.stringValue))
                return false;

            try
            {
                evidence = JsonUtility.FromJson<ESAITestVerifyResultDto>(value.stringValue);
                return evidence != null && string.Equals(evidence.schema, ESAITestVerifyResultDto.Schema, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                evidence = null;
                return false;
            }
        }

        private bool MatchesUseReceipt(
            ESAITestUseResultDto receipt,
            ESAITestStepDto definition,
            ESAITestStepResultDto result)
        {
            return receipt != null
                && definition != null
                && result != null
                && result.capabilityCalls != null
                && !result.capabilityCalls.sceneGenerationChangedDuringCall
                && string.Equals(receipt.runId, request?.runId, StringComparison.Ordinal)
                && receipt.sceneGeneration == result.capabilityCalls.lastSceneGeneration
                && string.Equals(receipt.invocationId, result.capabilityCalls.lastInvocationId, StringComparison.Ordinal)
                && string.Equals(receipt.stepId, definition.stepId, StringComparison.Ordinal)
                && string.Equals(receipt.capabilityId, definition.capabilityId, StringComparison.Ordinal)
                && string.Equals(receipt.command, definition.command, StringComparison.Ordinal)
                && EqualsProtocolText(receipt.target, definition.target);
        }

        private bool MatchesVerifyEvidence(
            ESAITestVerifyResultDto evidence,
            ESAITestStepDto definition,
            ESAITestStepResultDto result)
        {
            return evidence != null
                && definition != null
                && result != null
                && result.capabilityCalls != null
                && !result.capabilityCalls.sceneGenerationChangedDuringCall
                && string.Equals(evidence.runId, request?.runId, StringComparison.Ordinal)
                && evidence.sceneGeneration == result.capabilityCalls.lastSceneGeneration
                && string.Equals(evidence.invocationId, result.capabilityCalls.lastInvocationId, StringComparison.Ordinal)
                && string.Equals(evidence.stepId, definition.stepId, StringComparison.Ordinal)
                && string.Equals(evidence.capabilityId, definition.capabilityId, StringComparison.Ordinal)
                && string.Equals(evidence.command, definition.command, StringComparison.Ordinal)
                && EqualsProtocolText(evidence.target, definition.target);
        }

        private static bool EqualsProtocolText(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
        }

        private ESAITestStepResultDto FindFollowupVerification(int useStepIndex, string useStepId, out ESAITestStepDto definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(useStepId))
                return null;

            for (int i = useStepIndex + 1; i < stepResults.Count; i++)
            {
                ESAITestStepDto candidate = FindStepDefinition(stepResults[i].stepId);
                if (candidate == null
                    || (!string.Equals(candidate.operation, ESAITestProtocol.OperationVerify, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(candidate.operation, ESAITestProtocol.OperationWait, StringComparison.OrdinalIgnoreCase))
                    || !HasArgument(candidate.arguments, ESAITestProtocol.ArgumentVerifyUseStepId, useStepId))
                    continue;
                definition = candidate;
                return stepResults[i];
            }
            return null;
        }

        private static bool HasArgument(ESAITestArgumentDto[] arguments, string key, string value)
        {
            if (arguments == null)
                return false;

            for (int i = 0; i < arguments.Length; i++)
            {
                ESAITestArgumentDto argument = arguments[i];
                if (argument != null
                    && string.Equals(argument.key, key, StringComparison.Ordinal)
                    && string.Equals(argument.value, value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private ESAITestStepDiagnosticDto CreateStepDiagnostic(ESAITestStepResultDto stepResult)
        {
            if (stepResult == null)
                return null;

            ESAITestStepDto definition = FindStepDefinition(stepResult.stepId);
            return new ESAITestStepDiagnosticDto
            {
                stepId = stepResult.stepId ?? string.Empty,
                operation = definition?.operation ?? string.Empty,
                capabilityId = definition?.capabilityId ?? string.Empty,
                command = definition?.command ?? string.Empty,
                target = definition?.target ?? string.Empty,
                expectedValue = definition?.expectedValue ?? string.Empty,
                arguments = CloneArguments(definition?.arguments),
                timeoutSeconds = definition?.timeoutSeconds ?? 0f,
                pollIntervalSeconds = definition?.pollIntervalSeconds ?? 0f,
                continueOnFailure = definition != null && definition.continueOnFailure,
                statusCode = stepResult.statusCode ?? string.Empty,
                message = stepResult.message ?? string.Empty,
                elapsedSeconds = stepResult.elapsedSeconds,
                value = stepResult.value,
                capabilityCalls = stepResult.capabilityCalls,
            };
        }

        private static ESAITestArgumentDto[] CloneArguments(ESAITestArgumentDto[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ESAITestArgumentDto>();

            var copy = new ESAITestArgumentDto[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ESAITestArgumentDto item = source[i];
                copy[i] = item == null
                    ? new ESAITestArgumentDto()
                    : new ESAITestArgumentDto { key = item.key, value = item.value };
            }
            return copy;
        }

        private static ESAITestAutonomyDecisionDto CloneAutonomyDecision(ESAITestAutonomyDecisionDto source)
        {
            var copy = new ESAITestAutonomyDecisionDto
            {
                protocolVersion = source.protocolVersion,
                runId = source.runId,
                turnIndex = source.turnIndex,
                decisionId = source.decisionId,
                mode = source.mode,
                rationale = source.rationale,
                terminal = source.terminal,
                terminalStatusCode = source.terminalStatusCode,
                steps = Array.Empty<ESAITestStepDto>(),
            };
            ESAITestStepDto[] steps = source.steps ?? Array.Empty<ESAITestStepDto>();
            if (steps.Length == 0)
                return copy;

            copy.steps = new ESAITestStepDto[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                ESAITestStepDto step = steps[i];
                if (step == null)
                {
                    copy.steps[i] = null;
                    continue;
                }
                copy.steps[i] = new ESAITestStepDto
                {
                    protocolVersion = step.protocolVersion,
                    stepId = step.stepId,
                    operation = step.operation,
                    capabilityId = step.capabilityId,
                    command = step.command,
                    target = step.target,
                    expectedValue = step.expectedValue,
                    timeoutSeconds = step.timeoutSeconds,
                    pollIntervalSeconds = step.pollIntervalSeconds,
                    continueOnFailure = step.continueOnFailure,
                    arguments = CloneArguments(step.arguments),
                };
            }
            return copy;
        }

        private ESAITestStepDto FindStepDefinition(string stepId)
        {
            ESAITestStepDto[] steps = request?.plan?.steps ?? Array.Empty<ESAITestStepDto>();
            for (int i = 0; i < steps.Length; i++)
                if (steps[i] != null && string.Equals(steps[i].stepId, stepId, StringComparison.Ordinal))
                    return steps[i];
            return null;
        }

        private ESAITestEventDto FindLastActivityEvent()
        {
            for (int i = events.Count - 1; i >= 0; i--)
                if (!string.Equals(events[i].eventType, "run_completed", StringComparison.Ordinal))
                    return events[i];
            return null;
        }

        private int CountCapabilityCalls()
        {
            int count = 0;
            for (int i = 0; i < stepResults.Count; i++)
                count += stepResults[i]?.capabilityCalls?.callCount ?? 0;
            return count;
        }

        private int CountCapabilityRetries()
        {
            int count = 0;
            for (int i = 0; i < stepResults.Count; i++)
                count += stepResults[i]?.capabilityCalls?.retryCount ?? 0;
            return count;
        }

        private float SumCapabilityCallMilliseconds()
        {
            float milliseconds = 0f;
            for (int i = 0; i < stepResults.Count; i++)
                milliseconds += stepResults[i]?.capabilityCalls?.totalDurationMilliseconds ?? 0f;
            return milliseconds;
        }

        private float FindMaxCapabilityCallMilliseconds()
        {
            float milliseconds = 0f;
            for (int i = 0; i < stepResults.Count; i++)
                milliseconds = Mathf.Max(milliseconds, stepResults[i]?.capabilityCalls?.maxDurationMilliseconds ?? 0f);
            return milliseconds;
        }

        private static string SuggestInvestigation(string statusCode)
        {
            switch (statusCode)
            {
                case ESAITestStatusCode.RuntimeBusy:
                    return "启动/并发链：检查 ActiveRunner、RunId 和 Capability 重入拒绝事件。";
                case ESAITestStatusCode.InvalidRequest:
                case ESAITestStatusCode.InvalidPlan:
                case ESAITestStatusCode.UnsupportedProtocol:
                    return "计划链：先检查 request.json 的协议版本、StepId、操作、Capability 与超时参数。";
                case ESAITestStatusCode.CapabilityUnavailable:
                case ESAITestStatusCode.CapabilityRejected:
                    return "能力链：检查 manifest.json 中的 Capability/Command、场景代际及 Provider 拒绝消息。";
                case ESAITestStatusCode.StepTimeout:
                case ESAITestStatusCode.TotalTimeout:
                    return "执行链：检查首个失败 Step、最近事件、最后 See 快照与截图，确认等待条件或节流预算。";
                case ESAITestStatusCode.CallBudgetExceeded:
                    return "调用预算链：检查 Step 的 timeoutSeconds/pollIntervalSeconds；降低轮询频率或缩短等待条件。";
                case ESAITestStatusCode.AutonomyTurnLimit:
                case ESAITestStatusCode.AutonomyStuck:
                case ESAITestStatusCode.AutonomyDecisionRejected:
                    return "自主链：检查最后决策、See 快照、目标进度和连续失败计数；必要时提交 recover 或 stop 决策。";
                case ESAITestStatusCode.AutonomyBridgeLaunchFailed:
                case ESAITestStatusCode.AutonomyBridgeStartupTimeout:
                case ESAITestStatusCode.AutonomyBridgeHeartbeatTimeout:
                case ESAITestStatusCode.AutonomyBridgeExited:
                case ESAITestStatusCode.AutonomyBridgeSessionConflict:
                    return "自主外部桥：检查 diagnostics.json 的桥状态、Agent 可执行文件、session.json、心跳与 decisions 目录；不要重用旧 RunId。";
                case ESAITestStatusCode.Cancelled:
                    return "取消链：检查最后活动事件、当前 Step 与输入 Lease 的释放记录。";
                case ESAITestStatusCode.ReportWriteFailed:
                    return "报告链：检查 persistentDataPath 可写性、RunId 冲突和 Console 中的落盘异常。";
                default:
                    return "按 diagnostics.json 的首个失败 Step、最近活动事件和完整事件时间线定位。";
            }
        }

        private void OnDestroy()
        {
            ReleaseRuntime();
        }

        private void ReleaseRuntime()
        {
            if (!ownsRuntime)
                return;

            ownsRuntime = false;
            ESAITestRuntime.Deactivate(request?.runId);
        }

        private static string ValidateRequest(ESAITestRequestDto value, out string message)
        {
            if (value == null || value.plan == null)
            {
                message = "Request 或 Plan 为空。";
                return ESAITestStatusCode.InvalidRequest;
            }

            if (value.protocolVersion != ESAITestProtocol.CurrentVersion
                || value.plan.protocolVersion != ESAITestProtocol.CurrentVersion)
            {
                message = "不支持的协议版本。";
                return ESAITestStatusCode.UnsupportedProtocol;
            }

            if (string.IsNullOrWhiteSpace(value.runId) || string.IsNullOrWhiteSpace(value.plan.planId))
            {
                message = "runId 与 planId 必填。";
                return ESAITestStatusCode.InvalidPlan;
            }

            if (!IsWithinLength(value.runId, ESAITestProtocol.MaxIdentityLength)
                || !IsWithinLength(value.plan.planId, ESAITestProtocol.MaxIdentityLength))
            {
                message = "runId 或 planId 超出协议长度限制。";
                return ESAITestStatusCode.InvalidPlan;
            }

            if (!IsPositiveFinite(value.plan.totalTimeoutSeconds))
            {
                message = "totalTimeoutSeconds 必须大于 0。";
                return ESAITestStatusCode.InvalidPlan;
            }

            if (value.plan.totalTimeoutSeconds > ESAITestProtocol.MaximumTotalTimeoutSeconds)
            {
                message = "totalTimeoutSeconds 不能超过 " + ESAITestProtocol.MaximumTotalTimeoutSeconds + " 秒。";
                return ESAITestStatusCode.InvalidPlan;
            }

            if (value.autonomy != null)
            {
                if (value.autonomy.protocolVersion != ESAITestProtocol.CurrentVersion
                    || string.IsNullOrWhiteSpace(value.autonomy.goal)
                    || !IsWithinLength(value.autonomy.goal, ESAITestProtocol.MaxTextLength)
                    || value.autonomy.maxTurns < 1
                    || value.autonomy.maxTurns > ESAITestProtocol.MaximumAutonomyTurns
                    || !IsPositiveFinite(value.autonomy.maxDurationSeconds)
                    || value.autonomy.maxDurationSeconds > value.plan.totalTimeoutSeconds
                    || value.autonomy.maxConsecutiveFailures < 1
                    || value.autonomy.maxConsecutiveFailures > ESAITestProtocol.MaximumAutonomyTurns)
                {
                    message = "自主会话配置无效：目标、回合、时间或失败保护超出边界。";
                    return ESAITestStatusCode.InvalidPlan;
                }

                ESAITestAutonomyExternalBridgeConfigDto bridge = value.autonomy.externalBridge;
                if (bridge != null
                    && (bridge.protocolVersion != ESAITestProtocol.CurrentVersion
                        || !IsWithinLength(bridge.launcherId, ESAITestProtocol.MaxIdentityLength)
                        || !IsPositiveFinite(bridge.startupTimeoutSeconds)
                        || !IsPositiveFinite(bridge.heartbeatTimeoutSeconds)
                        || bridge.startupTimeoutSeconds < ESAITestProtocol.MinimumAutonomyBridgeTimeoutSeconds
                        || bridge.startupTimeoutSeconds > ESAITestProtocol.MaximumAutonomyBridgeTimeoutSeconds
                        || bridge.heartbeatTimeoutSeconds < ESAITestProtocol.MinimumAutonomyBridgeTimeoutSeconds
                        || bridge.heartbeatTimeoutSeconds > ESAITestProtocol.MaximumAutonomyBridgeTimeoutSeconds
                        || (bridge.autoLaunch && !string.Equals(bridge.launcherId,
                            ESAITestAutonomyExternalBridgeConfigDto.EnvironmentLauncherId,
                            StringComparison.Ordinal))))
                {
                    message = "自主外部桥配置无效：只允许 environment-agent，且启动/心跳超时必须位于受控范围。";
                    return ESAITestStatusCode.InvalidPlan;
                }
            }

            ESAITestStepDto[] steps = value.plan.steps ?? Array.Empty<ESAITestStepDto>();
            if (steps.Length == 0 || steps.Length > ESAITestProtocol.MaxPlanStepCount)
            {
                message = "Step 数量必须在 1 到 " + ESAITestProtocol.MaxPlanStepCount + " 之间。";
                return ESAITestStatusCode.InvalidPlan;
            }

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            var precedingActStepIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < steps.Length; i++)
            {
                ESAITestStepDto step = steps[i];
                if (step == null || step.protocolVersion != ESAITestProtocol.CurrentVersion
                    || string.IsNullOrWhiteSpace(step.stepId)
                    || string.IsNullOrWhiteSpace(step.operation)
                    || string.IsNullOrWhiteSpace(step.capabilityId)
                    || string.IsNullOrWhiteSpace(step.command))
                {
                    message = "Step 协议或必填字段无效，索引：" + i;
                    return ESAITestStatusCode.InvalidPlan;
                }

                if (!IsWithinLength(step.stepId, ESAITestProtocol.MaxIdentityLength)
                    || !IsWithinLength(step.capabilityId, ESAITestProtocol.MaxIdentityLength)
                    || !IsWithinLength(step.command, ESAITestProtocol.MaxIdentityLength)
                    || !IsWithinLength(step.target, ESAITestProtocol.MaxTextLength)
                    || !IsWithinLength(step.expectedValue, ESAITestProtocol.MaxTextLength))
                {
                    message = "Step 字段超出协议长度限制：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                if (!stepIds.Add(step.stepId))
                {
                    message = "StepId 重复：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                if (!IsPositiveFinite(step.timeoutSeconds)
                    || step.timeoutSeconds > ESAITestProtocol.MaximumStepTimeoutSeconds
                    || step.timeoutSeconds > value.plan.totalTimeoutSeconds)
                {
                    message = "Step timeoutSeconds 必须大于 0，且不超过单 Step/计划总预算：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                string operation = step.operation.ToLowerInvariant();
                if (operation != ESAITestProtocol.OperationSee
                    && operation != ESAITestProtocol.OperationVerify
                    && operation != ESAITestProtocol.OperationWait
                    && operation != ESAITestProtocol.OperationAct)
                {
                    message = "不支持的 Step 操作：" + step.operation;
                    return ESAITestStatusCode.InvalidPlan;
                }

                if (!IsPositiveFinite(step.pollIntervalSeconds)
                    || step.pollIntervalSeconds < ESAITestProtocol.MinimumPollIntervalSeconds)
                {
                    message = "Step pollIntervalSeconds 必须大于等于 "
                        + ESAITestProtocol.MinimumPollIntervalSeconds + "：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                if ((operation == ESAITestProtocol.OperationWait || operation == ESAITestProtocol.OperationSee)
                    && Math.Ceiling(step.timeoutSeconds / step.pollIntervalSeconds) + 1d
                        > ESAITestProtocol.MaximumCapabilityCallsPerStep)
                {
                    message = "Step 的超时/轮询组合超过 Capability 调用预算 "
                        + ESAITestProtocol.MaximumCapabilityCallsPerStep + "：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                ESAITestArgumentDto[] arguments = step.arguments ?? Array.Empty<ESAITestArgumentDto>();
                if (arguments.Length > ESAITestProtocol.MaxArgumentsPerStep)
                {
                    message = "Step 参数数量超过 " + ESAITestProtocol.MaxArgumentsPerStep + "：" + step.stepId;
                    return ESAITestStatusCode.InvalidPlan;
                }

                var argumentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string verifyUseStepId = null;
                for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
                {
                    ESAITestArgumentDto argument = arguments[argumentIndex];
                    if (argument == null || string.IsNullOrWhiteSpace(argument.key)
                        || !IsWithinLength(argument.key, ESAITestProtocol.MaxIdentityLength)
                        || !IsWithinLength(argument.value, ESAITestProtocol.MaxTextLength)
                        || !argumentKeys.Add(argument.key))
                    {
                        message = "Step 参数无效或重复：" + step.stepId;
                        return ESAITestStatusCode.InvalidPlan;
                    }

                    if (string.Equals(argument.key, ESAITestProtocol.ArgumentVerifyUseStepId, StringComparison.OrdinalIgnoreCase))
                        verifyUseStepId = argument.value;
                }

                if (verifyUseStepId != null)
                {
                    if ((operation != ESAITestProtocol.OperationVerify && operation != ESAITestProtocol.OperationWait)
                        || string.IsNullOrWhiteSpace(verifyUseStepId)
                        || !precedingActStepIds.Contains(verifyUseStepId))
                    {
                        message = "verifyUseStepId 必须引用位于当前 Step 之前的 act Step：" + step.stepId;
                        return ESAITestStatusCode.InvalidPlan;
                    }
                }

                if (operation == ESAITestProtocol.OperationAct)
                    precedingActStepIds.Add(step.stepId);
            }

            message = string.Empty;
            return null;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsWithinLength(string value, int maximum)
        {
            return value == null || value.Length <= maximum;
        }

        public static int ToExitCode(string statusCode)
        {
            if (statusCode == ESAITestStatusCode.Passed) return 0;
            if (statusCode == ESAITestStatusCode.InvalidRequest
                || statusCode == ESAITestStatusCode.InvalidPlan
                || statusCode == ESAITestStatusCode.UnsupportedProtocol) return 2;
            if (statusCode == ESAITestStatusCode.StepTimeout || statusCode == ESAITestStatusCode.TotalTimeout) return 3;
            if (statusCode == ESAITestStatusCode.CallBudgetExceeded) return 3;
            if (statusCode == ESAITestStatusCode.Cancelled) return 4;
            if (statusCode == ESAITestStatusCode.ReportWriteFailed) return 5;
            return 1;
        }
    }

    public static class ESAITestReportWriter
    {
        public static string WriteToPersistentData(ESAITestResultDto result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.runId))
                throw new ArgumentException("Result 与 runId 必填。", nameof(result));

            string reportsRoot = Path.Combine(Application.persistentDataPath, "ESAITest");
            string runSegment = SanitizeSegment(result.runId);
            string finalDirectory = Path.Combine(reportsRoot, runSegment);
            string temporaryDirectory = Path.Combine(reportsRoot, "." + runSegment + "." + Guid.NewGuid().ToString("N") + ".tmp");
            if (Directory.Exists(finalDirectory))
                throw new IOException("同 RunId 报告已存在，拒绝覆盖：" + finalDirectory);

            Directory.CreateDirectory(temporaryDirectory);
            result.reportPath = Path.Combine(finalDirectory, "result.json");
            result.summaryPath = Path.Combine(finalDirectory, "summary.md");
            result.diagnosticsPath = Path.Combine(finalDirectory, "diagnostics.json");
            result.artifacts = ESAITestArtifactStore.CreateManifest(result.runId);
            MarkReportWritten(result);

            try
            {
                ESAITestArtifactStore.CopyIntoReport(result.runId, temporaryDirectory);
                File.WriteAllText(Path.Combine(temporaryDirectory, "result.json"), JsonUtility.ToJson(result, true));
                File.WriteAllText(Path.Combine(temporaryDirectory, "summary.md"), BuildMarkdown(result));
                File.WriteAllText(Path.Combine(temporaryDirectory, "diagnostics.json"), result.diagnostics == null ? "{}" : JsonUtility.ToJson(result.diagnostics, true));
                File.WriteAllText(Path.Combine(temporaryDirectory, "request.json"), result.request == null ? "{}" : JsonUtility.ToJson(result.request, true));
                File.WriteAllText(Path.Combine(temporaryDirectory, "manifest.json"), result.manifest == null ? "{}" : JsonUtility.ToJson(result.manifest, true));
                VerifyTemporaryReport(temporaryDirectory, result);
                Directory.CreateDirectory(reportsRoot);
                Directory.Move(temporaryDirectory, finalDirectory);
                try
                {
                    ESAITestArtifactStore.ClearStaging(result.runId);
                }
                catch (Exception cleanupException)
                {
                    UnityEngine.Debug.LogWarning("[ESAITest] 报告已完成，但 Observation 暂存目录清理失败：" + cleanupException.Message);
                }
            }
            catch
            {
                if (Directory.Exists(temporaryDirectory))
                    Directory.Delete(temporaryDirectory, true);
                throw;
            }

            return result.reportPath;
        }

        private static void VerifyTemporaryReport(string temporaryDirectory, ESAITestResultDto expected)
        {
            if (string.IsNullOrWhiteSpace(temporaryDirectory) || expected == null)
                throw new ArgumentException("报告校验需要临时目录和结果对象。");

            string resultFile = RequireReportFile(temporaryDirectory, "result.json");
            string summaryFile = RequireReportFile(temporaryDirectory, "summary.md");
            string diagnosticsFile = RequireReportFile(temporaryDirectory, "diagnostics.json");
            string requestFile = RequireReportFile(temporaryDirectory, "request.json");
            string manifestFile = RequireReportFile(temporaryDirectory, "manifest.json");

            ESAITestResultDto storedResult = JsonUtility.FromJson<ESAITestResultDto>(File.ReadAllText(resultFile));
            ESAITestRunDiagnosticsDto storedDiagnostics = JsonUtility.FromJson<ESAITestRunDiagnosticsDto>(File.ReadAllText(diagnosticsFile));
            ESAITestRequestDto storedRequest = JsonUtility.FromJson<ESAITestRequestDto>(File.ReadAllText(requestFile));
            ESAITestCapabilityManifestDto storedManifest = expected.manifest == null
                ? null
                : JsonUtility.FromJson<ESAITestCapabilityManifestDto>(File.ReadAllText(manifestFile));
            if (storedResult == null || storedDiagnostics == null || storedRequest == null)
                throw new InvalidDataException("报告 JSON 无法解析为 ESAITest DTO。");

            RequireEqual("result.runId", expected.runId, storedResult.runId);
            RequireEqual("diagnostics.runId", expected.runId, storedDiagnostics.runId);
            RequireEqual("request.runId", expected.runId, storedRequest.runId);
            RequireEqual("result.executionStatusCode", expected.executionStatusCode, storedResult.executionStatusCode);
            RequireEqual("diagnostics.executionStatusCode", expected.executionStatusCode, storedDiagnostics.executionStatusCode);
            RequireEqual("result.reportStatusCode", ESAITestReportStatusCode.Written, storedResult.reportStatusCode);
            RequireEqual("diagnostics.reportStatusCode", ESAITestReportStatusCode.Written, storedDiagnostics.reportStatusCode);
            RequireEqual("firstFailedStepId", expected.firstFailedStepId,
                storedDiagnostics.firstFailedStep == null ? string.Empty : storedDiagnostics.firstFailedStep.stepId);
            if (storedManifest != null)
                RequireEqual("manifest.runId", expected.runId, storedManifest.runId);

            string summary = File.ReadAllText(summaryFile);
            if (string.IsNullOrEmpty(summary)
                || summary.IndexOf("RunId", StringComparison.Ordinal) < 0
                || summary.IndexOf(Escape(expected.runId), StringComparison.Ordinal) < 0
                || summary.IndexOf(expected.executionStatusCode ?? string.Empty, StringComparison.Ordinal) < 0
                || summary.IndexOf(ESAITestReportStatusCode.Written, StringComparison.Ordinal) < 0)
                throw new InvalidDataException("summary.md 缺少 RunId、执行终态或报告落盘终态。");

            ESAITestArtifactDto[] artifacts = storedResult.artifacts ?? Array.Empty<ESAITestArtifactDto>();
            for (int i = 0; i < artifacts.Length; i++)
                VerifyArtifactExists(temporaryDirectory, artifacts[i]);
        }

        private static string RequireReportFile(string directory, string name)
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new FileNotFoundException("报告缺少必需文件：" + name, path);
            return path;
        }

        private static void RequireEqual(string field, string expected, string actual)
        {
            if (!string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.Ordinal))
                throw new InvalidDataException(field + " 不一致：expected=" + expected + " actual=" + actual);
        }

        private static void VerifyArtifactExists(string reportDirectory, ESAITestArtifactDto artifact)
        {
            string relativePath = artifact?.relativePath;
            string normalized = relativePath == null ? string.Empty : relativePath.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativePath)
                || !normalized.StartsWith("artifacts/", StringComparison.Ordinal)
                || normalized.StartsWith("../", StringComparison.Ordinal)
                || normalized.IndexOf("/../", StringComparison.Ordinal) >= 0
                || normalized.EndsWith("/..", StringComparison.Ordinal))
                throw new InvalidDataException("Artifact Manifest 包含非法相对路径：" + relativePath);

            string destination = Path.GetFullPath(Path.Combine(reportDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.GetFullPath(reportDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(destination))
                throw new FileNotFoundException("Artifact Manifest 指向的文件不存在：" + relativePath, destination);
        }

        public static void MarkReportWriteFailed(ESAITestResultDto result, Exception exception)
        {
            if (result == null)
                return;

            string error = exception == null ? "未知报告写入错误。" : exception.ToString();
            result.reportStatusCode = ESAITestReportStatusCode.WriteFailed;
            result.reportMessage = error;
            result.statusCode = ESAITestStatusCode.ReportWriteFailed;
            result.message = error;
            result.exitCode = ESAITestRunner.ToExitCode(result.statusCode);
            if (result.diagnostics != null)
            {
                result.diagnostics.reportStatusCode = ESAITestReportStatusCode.WriteFailed;
                result.diagnostics.reportMessage = error;
                result.diagnostics.suggestedInvestigation = "报告链：检查 persistentDataPath 可写性、RunId 冲突和 Console 中的落盘异常；原始测试执行结论保留在 executionStatusCode。";
            }
        }

        private static void MarkReportWritten(ESAITestResultDto result)
        {
            result.reportStatusCode = ESAITestReportStatusCode.Written;
            result.reportMessage = "报告已纳入原子提升事务。";
            if (result.diagnostics != null)
            {
                result.diagnostics.reportStatusCode = ESAITestReportStatusCode.Written;
                result.diagnostics.reportMessage = result.reportMessage;
            }
        }

        private static string BuildMarkdown(ESAITestResultDto result)
        {
            var text = new StringBuilder(16384);
            text.Append("# ESAITest 运行报告\n\n");
            text.Append("## 执行摘要\n\n");
            text.Append("| 字段 | 值 |\n|---|---|\n");
            AppendRow(text, "RunId", result.runId);
            AppendRow(text, "PlanId", result.planId);
            AppendRow(text, "状态", result.statusCode);
            AppendRow(text, "退出码", result.exitCode.ToString());
            AppendRow(text, "结论", result.message);
            AppendRow(text, "测试执行", result.executionStatusCode + " | " + result.executionMessage);
            AppendRow(text, "报告落盘", result.reportStatusCode + " | " + result.reportMessage);
            AppendRow(text, "耗时", result.elapsedSeconds.ToString("F3") + " 秒");
            AppendRow(text, "步骤", result.passedStepCount + " 通过 / " + result.failedStepCount + " 失败 / " + result.totalStepCount + " 总计");
            AppendRow(text, "首个失败 Step", string.IsNullOrEmpty(result.firstFailedStepId) ? "-" : result.firstFailedStepId);
            AppendRow(text, "诊断索引", string.IsNullOrEmpty(result.diagnosticsPath) ? "-" : result.diagnosticsPath);
            AppendRow(text, "Unity", result.unityVersion);
            AppendRow(text, "平台", result.platform);
            AppendRow(text, "产品", result.productName + " " + result.applicationVersion);
            AppendRow(text, "场景", result.activeScene);
            AppendRow(text, "开始 UTC", new DateTime(result.startedUtcTicks, DateTimeKind.Utc).ToString("O"));
            AppendRow(text, "结束 UTC", new DateTime(result.completedUtcTicks, DateTimeKind.Utc).ToString("O"));

            AppendDiagnostics(text, result.diagnostics);

            text.Append("\n## Capability Manifest\n\n");
            text.Append("| Capability | Provider | Version | Commands |\n|---|---|---:|---|\n");
            ESAITestCapabilityManifestItemDto[] capabilities = result.manifest?.capabilities ?? Array.Empty<ESAITestCapabilityManifestItemDto>();
            for (int i = 0; i < capabilities.Length; i++)
            {
                ESAITestCapabilityManifestItemDto item = capabilities[i];
                text.Append('|').Append(Escape(item.capabilityId)).Append('|')
                    .Append(Escape(item.providerId)).Append('|')
                    .Append(item.providerVersion).Append('|')
                    .Append(Escape(string.Join(", ", item.commands ?? Array.Empty<string>()))).Append("|\n");
            }
            if (capabilities.Length == 0)
                text.Append("| - | - | - | 无 |\n");

            text.Append("\n## Observation Artifacts\n\n");
            text.Append("| 路径 | 类型 | 字节 | SHA-256 |\n|---|---|---:|---|\n");
            ESAITestArtifactDto[] artifacts = result.artifacts ?? Array.Empty<ESAITestArtifactDto>();
            for (int i = 0; i < artifacts.Length; i++)
            {
                ESAITestArtifactDto artifact = artifacts[i];
                text.Append('|').Append(Escape(artifact.relativePath)).Append('|')
                    .Append(Escape(artifact.kind)).Append('|')
                    .Append(artifact.byteLength).Append('|')
                    .Append(Escape(artifact.sha256)).Append("|\n");
            }
            if (artifacts.Length == 0)
                text.Append("| - | - | 0 | 无 |\n");

            text.Append("\n## Step 明细\n\n");
            text.Append("| # | StepId | 状态 | 耗时(s) | 调用诊断 | 消息 | 值 |\n|---:|---|---|---:|---|---|---|\n");
            ESAITestStepResultDto[] steps = result.steps ?? Array.Empty<ESAITestStepResultDto>();
            for (int i = 0; i < steps.Length; i++)
            {
                ESAITestStepResultDto step = steps[i];
                text.Append('|').Append(i + 1).Append('|').Append(Escape(step.stepId)).Append('|')
                    .Append(Escape(step.statusCode)).Append('|').Append(step.elapsedSeconds.ToString("F3")).Append('|')
                    .Append(Escape(FormatCapabilityCalls(step.capabilityCalls))).Append('|')
                    .Append(Escape(step.message)).Append('|').Append(Escape(FormatValue(step.value))).Append("|\n");
            }

            text.Append("\n## 完整事件时间线\n\n");
            text.Append("| 时间(s) | UTC | 类型 | Step | 状态 | 消息 | 值 |\n|---:|---|---|---|---|---|---|\n");
            ESAITestEventDto[] events = result.events ?? Array.Empty<ESAITestEventDto>();
            for (int i = 0; i < events.Length; i++)
            {
                ESAITestEventDto item = events[i];
                text.Append('|').Append(item.elapsedSeconds.ToString("F3")).Append('|')
                    .Append(new DateTime(item.utcTicks, DateTimeKind.Utc).ToString("O")).Append('|')
                    .Append(Escape(item.eventType)).Append('|').Append(Escape(item.stepId)).Append('|')
                    .Append(Escape(item.statusCode)).Append('|').Append(Escape(item.message)).Append('|')
                    .Append(Escape(FormatValue(item.value))).Append("|\n");
            }

            text.Append("\n## 证据边界\n\n");
            text.Append("- 本报告证明 Player 内 ESAITest Runner 实际执行过所列计划与 Capability。\n");
            text.Append("- 本报告不自动等同于 Unity Test Runner、Profiler、Player Build、IL2CPP 或发布验收通过。\n");
            return text.ToString();
        }

        private static void AppendDiagnostics(StringBuilder text, ESAITestRunDiagnosticsDto diagnostics)
        {
            text.Append("\n## 快速定位\n\n");
            if (diagnostics == null)
            {
                text.Append("诊断索引不可用；请使用 Step 明细与完整事件时间线排障。\n");
                return;
            }

            text.Append("| 字段 | 值 |\n|---|---|\n");
            AppendRow(text, "建议先查", diagnostics.suggestedInvestigation);
            AppendRow(text, "测试执行", diagnostics.executionStatusCode + " | " + diagnostics.executionMessage);
            AppendRow(text, "报告落盘", diagnostics.reportStatusCode + " | " + diagnostics.reportMessage);
            AppendRow(text, "终止时 Step", diagnostics.currentStepId + " | " + diagnostics.currentOperation);
            AppendRow(text, "Capability 调用", diagnostics.capabilityCallCount + " 次 | 重试="
                + diagnostics.capabilityRetryCount + " | 总=" + diagnostics.capabilityCallMilliseconds.ToString("F3")
                + "ms | 最大=" + diagnostics.maxCapabilityCallMilliseconds.ToString("F3") + "ms");
            AppendRow(text, "最后活动", FormatEvent(diagnostics.lastActivityEvent));
            AppendRow(text, "最近 See", FormatObservation(diagnostics.lastObservation));
            AppendRow(text, "AI 提示队列", FormatPromptQueue(diagnostics.promptQueue));
            AppendRow(text, "自主会话", diagnostics.autonomyEnabled
                ? "目标=" + diagnostics.autonomyGoal
                    + " | 回合=" + diagnostics.autonomyTurn
                    + " | 决策=" + diagnostics.autonomyDecisionCount
                    + " | 拒绝=" + diagnostics.autonomyRejectedDecisionCount
                    + " | 连续失败=" + diagnostics.autonomyConsecutiveFailures
                    + " | 等待=" + diagnostics.autonomyWaitingForDecision
                : "未启用");
            AppendRow(text, "外部 AI 桥", FormatAutonomyBridge(diagnostics.autonomyBridge));

            AppendStepDiagnostic(text, "首个失败 Step 请求", diagnostics.firstFailedStep);
            AppendStepDiagnostic(text, "最后完成 Step 请求", diagnostics.lastCompletedStep);
        }

        private static string FormatAutonomyBridge(ESAITestAutonomyBridgeDiagnosticsDto bridge)
        {
            if (bridge == null)
                return "未启用";
            return (bridge.state ?? "-")
                + " | launcher=" + (bridge.launcherId ?? "-")
                + " | autoLaunch=" + bridge.autoLaunchRequested
                + " | pid=" + bridge.externalProcessId
                + " | requests=" + bridge.requestsPublished
                + " | accepted=" + bridge.decisionsAccepted
                + " | rejected=" + bridge.decisionsRejected
                + " | readFailures=" + bridge.decisionReadFailures
                + " | status=" + (bridge.lastStatusCode ?? "-")
                + " | message=" + (bridge.lastMessage ?? "-");
        }

        private static void AppendStepDiagnostic(StringBuilder text, string title, ESAITestStepDiagnosticDto step)
        {
            text.Append("\n### ").Append(title).Append("\n\n");
            if (step == null)
            {
                text.Append("无。\n");
                return;
            }

            text.Append("| 字段 | 值 |\n|---|---|\n");
            AppendRow(text, "StepId", step.stepId);
            AppendRow(text, "请求", step.operation + " | " + step.capabilityId + " | " + step.command);
            AppendRow(text, "目标", step.target);
            AppendRow(text, "期望值", step.expectedValue);
            AppendRow(text, "参数", FormatArguments(step.arguments));
            AppendRow(text, "结果", step.statusCode + " | " + step.message);
            AppendRow(text, "耗时", step.elapsedSeconds.ToString("F3") + " 秒");
            AppendRow(text, "Capability 调用", FormatCapabilityCalls(step.capabilityCalls));
            AppendRow(text, "超时/轮询", step.timeoutSeconds.ToString("F3") + " 秒 / " + step.pollIntervalSeconds.ToString("F3") + " 秒");
            AppendRow(text, "失败后继续", step.continueOnFailure.ToString());
            AppendRow(text, "返回值", FormatValue(step.value));
        }

        private static string FormatEvent(ESAITestEventDto value)
        {
            if (value == null)
                return "-";
            return value.eventType + " | " + value.stepId + " | " + value.statusCode + " | " + value.message;
        }

        private static string FormatCapabilityCalls(ESAITestCapabilityCallDiagnosticDto value)
        {
            if (value == null || value.callCount == 0)
                return "未调用 Capability";
            return "调用=" + value.callCount + " | 重试=" + value.retryCount
                + " | invocation=" + value.firstInvocationId + "→" + value.lastInvocationId
                + " | scene=" + value.firstSceneGeneration + "→" + value.lastSceneGeneration
                + (value.sceneGenerationChangedDuringCall ? "（调用中变化）" : string.Empty)
                + " | 总/最大=" + value.totalDurationMilliseconds.ToString("F3") + "/"
                + value.maxDurationMilliseconds.ToString("F3") + "ms | 最后="
                + value.lastStatusCode + " | accepted=" + value.lastAccepted
                + " | condition=" + value.lastConditionMet + " | retryable=" + value.lastRetryable
                + " | response=" + (value.lastResponse?.schema ?? string.Empty)
                + " | provider=" + (value.lastResponse?.providerId ?? string.Empty)
                + "@" + (value.lastResponse?.providerVersion ?? 0);
        }

        private static string FormatObservation(ESAITestObservationDiagnosticDto value)
        {
            if (value == null || string.IsNullOrEmpty(value.command))
                return "尚无";
            return value.command + " | Attention=" + value.attentionProfile + " | UI=" + value.uiCount
                + " | Scene=" + value.sceneObjectCount + " | 截图=" + value.latestScreenshotPath;
        }

        private static string FormatPromptQueue(ESAITestPromptQueueDiagnosticDto value)
        {
            if (value == null)
                return "尚无";
            string consumed = value.lastConsumedPrompt == null
                ? "尚未消费"
                : value.lastConsumedPrompt.promptId + " | " + value.lastConsumedPrompt.priority;
            return "待消费=" + value.pendingCount + " | 最高=" + value.highestPendingPriority + " | 最近消费=" + consumed;
        }

        private static string FormatArguments(ESAITestArgumentDto[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return "-";

            var text = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    text.Append(", ");
                ESAITestArgumentDto item = arguments[i];
                text.Append(item?.key ?? string.Empty).Append('=').Append(item?.value ?? string.Empty);
            }
            return text.ToString();
        }

        private static void AppendRow(StringBuilder text, string key, string value)
        {
            text.Append('|').Append(Escape(key)).Append('|').Append(Escape(value)).Append("|\n");
        }

        private static string FormatValue(ESAITestValueDto value)
        {
            if (value == null) return string.Empty;
            if (value.kind == "boolean") return value.boolValue.ToString();
            if (value.kind == "number") return value.numberValue.ToString("R");
            return value.stringValue ?? string.Empty;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string SanitizeSegment(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (Array.IndexOf(invalid, characters[i]) >= 0)
                    characters[i] = '_';
            string result = new string(characters);
            if (string.IsNullOrWhiteSpace(result) || result == "." || result == "..")
                throw new ArgumentException("runId 不能作为安全目录名。", nameof(value));
            return result;
        }
    }
}
