using System;
using ESFramework.ESAITest;
using UnityEngine;

namespace ES
{
    public sealed class ESAITestInputCapabilityProvider : MonoBehaviour, ESAITestCapabilityProvider
    {
        private const string Capability = "es.input";
        private int leaseToken;
        private int leaseGeneration;
        private string leaseOwner;

        public string CapabilityId => Capability;
        public string ProviderId => "es.aitest.input";
        public int ProviderVersion => 1;
        public string[] Commands => new[]
        {
            "control.acquire", "control.release", "control.state",
            "button.set", "button.pulse", "axis.set", "vector2.set", "action.clear"
        };

        private void OnEnable()
        {
            ESAITestRuntime.Activated += Register;
            ESAITestRuntime.SceneGenerationChanged += Register;
            ESAITestRuntime.Deactivated += HandleRuntimeDeactivated;
            Register();
        }

        private void OnDisable()
        {
            ESAITestRuntime.Activated -= Register;
            ESAITestRuntime.SceneGenerationChanged -= Register;
            ESAITestRuntime.Deactivated -= HandleRuntimeDeactivated;
            ESAITestRuntime.Registry?.Unregister(this);
            ReleaseLease();
        }

        public ESAITestCapabilityResponseDto Execute(ESAITestCapabilityRequestDto request)
        {
            switch (request.command)
            {
                case "control.acquire":
                    if (!IsOperation(request, ESAITestProtocol.OperationAct))
                        return RejectOperation(request, "control.acquire", ESAITestProtocol.OperationAct);
                    return AcquireLease(request);
                case "control.release":
                    if (!IsOperation(request, ESAITestProtocol.OperationAct))
                        return RejectOperation(request, "control.release", ESAITestProtocol.OperationAct);
                    return ReleaseLeaseResponse(request);
                case "control.state": return ControlState(request);
                case "button.set": return ExecuteAction(request, SetButton);
                case "button.pulse": return ExecuteAction(request, PulseButton);
                case "axis.set": return ExecuteAction(request, SetAxis);
                case "vector2.set": return ExecuteAction(request, SetVector2);
                case "action.clear": return ExecuteAction(request, ClearAction);
                default:
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未知 ES 输入命令：" + request.command);
            }
        }

        private static ESAITestCapabilityResponseDto ExecuteAction(
            ESAITestCapabilityRequestDto request,
            Func<ESAITestCapabilityRequestDto, ESAITestCapabilityResponseDto> action)
        {
            if (!IsOperation(request, ESAITestProtocol.OperationAct))
                return RejectOperation(request, request.command, ESAITestProtocol.OperationAct);
            return action(request);
        }

        private static bool IsOperation(ESAITestCapabilityRequestDto request, string operation)
        {
            return request != null && string.Equals(request.operation, operation, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReadOperation(ESAITestCapabilityRequestDto request)
        {
            return IsOperation(request, ESAITestProtocol.OperationSee)
                || IsOperation(request, ESAITestProtocol.OperationVerify);
        }

        private static ESAITestCapabilityResponseDto RejectOperation(
            ESAITestCapabilityRequestDto request,
            string command,
            string expectedOperation)
        {
            return ESAITestCapabilityResponseDto.Reject(
                ESAITestStatusCode.CapabilityRejected,
                command + " 只允许 " + expectedOperation + "，当前 operation=" + (request == null ? string.Empty : request.operation));
        }

        private void Register()
        {
            if (!isActiveAndEnabled || !ESAITestRuntime.IsActive || ESAITestRuntime.Registry == null)
                return;
            if (!ESAITestRuntime.Registry.Register(this, ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration, out string error))
                Debug.LogError("[ESAITest] 输入 Capability 注册失败：" + error, this);
        }

        private ESAITestCapabilityResponseDto AcquireLease(ESAITestCapabilityRequestDto request)
        {
            string runId = request?.runId;
            if (HasLease)
            {
                if (!string.Equals(leaseOwner, runId, StringComparison.Ordinal))
                    return RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                        "输入 Lease 归属与当前 Run 不一致，拒绝复用。", CreateInputUseReceipt(
                            request, false, false, ESAITestStatusCode.CapabilityRejected,
                            "输入 Lease 归属与当前 Run 不一致，拒绝复用。", ESAITestValueDto.FromBoolean(false),
                            leaseOwner, leaseGeneration, true));
                return Accepted(true, false, "当前 Run 已持有 ES 输入控制。", CreateInputUseReceipt(
                    request,
                    true,
                    true,
                    ESAITestStatusCode.Passed,
                    "当前 Run 已持有 ES 输入控制。",
                    ESAITestValueDto.FromString(leaseOwner),
                    leaseOwner,
                    leaseGeneration,
                    true));
            }

            ClearStaleLocalLease();
            ESInputModule module = ESGameManager.InputModule;
            if (module == null)
                return RejectedWithReceipt(ESAITestStatusCode.CapabilityUnavailable, "ESInputModule 尚未注入。", CreateInputUseReceipt(
                    request,
                    false,
                    false,
                    ESAITestStatusCode.CapabilityUnavailable,
                    "ESInputModule 尚未注入。",
                    ESAITestValueDto.FromBoolean(false),
                    leaseOwner,
                    leaseGeneration,
                    false));

            if (!module.TryAcquireAITestInput(runId, out leaseToken, out leaseGeneration, out string error))
                return RejectedWithReceipt(ESAITestStatusCode.RuntimeBusy, error, CreateInputUseReceipt(
                    request,
                    false,
                    false,
                    ESAITestStatusCode.RuntimeBusy,
                    error,
                    ESAITestValueDto.FromBoolean(false),
                    leaseOwner,
                    leaseGeneration,
                    false));

            leaseOwner = runId;
            return Accepted(true, false, "已取得 ES 测试输入独占控制 Lease。", CreateInputUseReceipt(
                request,
                true,
                true,
                ESAITestStatusCode.Passed,
                "已取得 ES 测试输入独占控制 Lease。",
                ESAITestValueDto.FromString(leaseOwner),
                leaseOwner,
                leaseGeneration,
                true));
        }

        private ESAITestCapabilityResponseDto ReleaseLeaseResponse(ESAITestCapabilityRequestDto request)
        {
            if (HasLease && !string.Equals(leaseOwner, request?.runId, StringComparison.Ordinal))
                return RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                    "输入 Lease 归属与当前 Run 不一致，拒绝释放。", CreateInputUseReceipt(
                        request, false, false, ESAITestStatusCode.CapabilityRejected,
                        "输入 Lease 归属与当前 Run 不一致，拒绝释放。", ESAITestValueDto.FromBoolean(false),
                        leaseOwner, leaseGeneration, true));

            string ownerBeforeRelease = leaseOwner;
            int generationBeforeRelease = leaseGeneration;
            bool heldBeforeRelease = HasLease;
            bool released = ReleaseLease();
            string message = released ? "已释放 ES 测试输入控制。" : "当前没有可释放的输入 Lease。";
            ESAITestValueDto receipt = CreateInputUseReceipt(
                request,
                heldBeforeRelease,
                released,
                released ? ESAITestStatusCode.Passed : ESAITestStatusCode.CapabilityRejected,
                message,
                ESAITestValueDto.FromBoolean(released),
                ownerBeforeRelease,
                generationBeforeRelease,
                false);
            return released
                ? Accepted(true, false, message, receipt)
                : RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected, message, receipt);
        }

        private ESAITestCapabilityResponseDto ControlState(ESAITestCapabilityRequestDto request)
        {
            if (!IsReadOperation(request))
                return RejectOperation(request, "control.state", "see/verify");

            bool value = HasLease
                && string.Equals(leaseOwner, request?.runId, StringComparison.Ordinal)
                && ESGameManager.InputModule != null
                && ESGameManager.InputModule.HasAITestInputControl;
            if (string.Equals(request.operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase))
                return Accepted(true, false, "AI 输入控制=" + value, ESAITestValueDto.FromBoolean(value));
            if (!bool.TryParse(request.expectedValue, out bool expected))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "expectedValue 必须为 true 或 false。");
            bool matched = value == expected;
            string message = "AI 输入控制=" + value + ", expected=" + expected;
            return Accepted(matched, !matched, message, CreateInputVerifyEvidence(request, matched, expected, value, message));
        }

        private ESAITestCapabilityResponseDto SetButton(ESAITestCapabilityRequestDto request)
        {
            if (!TryResolveAction(request, request.target, ESInputValueType.Button, out ESInputActionId action, out string error))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, error);
            if (!bool.TryParse(request.expectedValue, out bool held))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, "button.set 的 expectedValue 必须为 true 或 false。");
            return Write(request, ESGameManager.InputModule?.AITestSetButton(leaseOwner, leaseToken, leaseGeneration, action, held) == true,
                action + " held=" + held, ESAITestValueDto.FromBoolean(held));
        }

        private ESAITestCapabilityResponseDto PulseButton(ESAITestCapabilityRequestDto request)
        {
            if (!TryResolveAction(request, request.target, ESInputValueType.Button, out ESInputActionId action, out string error))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, error);
            return Write(request, ESGameManager.InputModule?.AITestPulseButton(leaseOwner, leaseToken, leaseGeneration, action) == true,
                action + " pulse", ESAITestValueDto.FromBoolean(true));
        }

        private ESAITestCapabilityResponseDto SetAxis(ESAITestCapabilityRequestDto request)
        {
            if (!TryResolveAction(request, request.target, ESInputValueType.Axis, out ESInputActionId action, out string error))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, error);
            if (!float.TryParse(request.expectedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
                || !IsFinite(value))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, "axis.set 的 expectedValue 必须为有限的 InvariantCulture 浮点数。");
            value = Mathf.Clamp(value, -1f, 1f);
            return Write(request, ESGameManager.InputModule?.AITestSetAxis(leaseOwner, leaseToken, leaseGeneration, action, value) == true,
                action + " axis=" + value, ESAITestValueDto.FromNumber(value));
        }

        private ESAITestCapabilityResponseDto SetVector2(ESAITestCapabilityRequestDto request)
        {
            if (!TryResolveAction(request, request.target, ESInputValueType.Vector2, out ESInputActionId action, out string error))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, error);
            if (!TryReadVector2(request, out Vector2 value))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, "vector2.set 需要 x/y 参数或 expectedValue='x,y'。");
            value = Vector2.ClampMagnitude(value, 1f);
            return Write(request, ESGameManager.InputModule?.AITestSetVector2(leaseOwner, leaseToken, leaseGeneration, action, value) == true,
                action + " vector2=" + value, ESAITestValueDto.FromString(value.x.ToString("R") + "," + value.y.ToString("R")));
        }

        private ESAITestCapabilityResponseDto ClearAction(ESAITestCapabilityRequestDto request)
        {
            if (!HasLeaseForRequest(request, out string leaseError))
                return RejectAction(request, ESAITestStatusCode.CapabilityRejected, leaseError);
            if (!Enum.TryParse(request.target, true, out ESInputActionId action))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, "未知 ESInputActionId：" + request.target);

            ESInputRuntimeCache cache = ESGameManager.InputModule?.Service?.Cache;
            int index = (int)action;
            if (cache == null || !cache.IsValidIndex(index))
                return RejectAction(request, ESAITestStatusCode.InvalidRequest, "不可用的 ESInputActionId：" + request.target);

            return Write(request, ESGameManager.InputModule?.AITestClearAction(leaseOwner, leaseToken, leaseGeneration, action) == true,
                "已清理 " + action, ESAITestValueDto.FromBoolean(true));
        }

        private bool TryResolveAction(
            ESAITestCapabilityRequestDto request,
            string value,
            ESInputValueType expectedType,
            out ESInputActionId action,
            out string error)
        {
            action = default;
            error = string.Empty;
            if (!HasLeaseForRequest(request, out error))
                return false;
            if (!Enum.TryParse(value, true, out action))
            {
                error = "未知 ESInputActionId：" + value;
                return false;
            }

            ESInputRuntimeCache cache = ESGameManager.InputModule?.Service?.Cache;
            int index = (int)action;
            if (cache == null || !cache.IsValidIndex(index) || cache.metas[index].valueType != expectedType)
            {
                error = action + " 不是可用的 " + expectedType + " 动作。";
                return false;
            }
            return true;
        }

        private static bool TryReadVector2(ESAITestCapabilityRequestDto request, out Vector2 value)
        {
            value = Vector2.zero;
            string xText = FindArgument(request.arguments, "x");
            string yText = FindArgument(request.arguments, "y");
            if (string.IsNullOrEmpty(xText) || string.IsNullOrEmpty(yText))
            {
                string[] parts = (request.expectedValue ?? string.Empty).Split(',');
                if (parts.Length != 2) return false;
                xText = parts[0];
                yText = parts[1];
            }
            return float.TryParse(xText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value.x)
                   && float.TryParse(yText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value.y)
                   && IsFinite(value.x)
                   && IsFinite(value.y);
        }

        private static string FindArgument(ESAITestArgumentDto[] arguments, string key)
        {
            if (arguments == null) return null;
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] != null && string.Equals(arguments[i].key, key, StringComparison.OrdinalIgnoreCase))
                    return arguments[i].value;
            return null;
        }

        private ESAITestCapabilityResponseDto Write(
            ESAITestCapabilityRequestDto request,
            bool success,
            string message,
            ESAITestValueDto value)
        {
            ESAITestValueDto receipt = CreateInputUseReceipt(
                request,
                success,
                success,
                success ? ESAITestStatusCode.Passed : ESAITestStatusCode.CapabilityRejected,
                message,
                value,
                leaseOwner,
                leaseGeneration,
                HasLease);
            return success
                ? Accepted(true, false, message, receipt)
                : RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                    "输入 Lease 已失效或 ESInputModule 不可用：" + message,
                    receipt);
        }

        private ESAITestCapabilityResponseDto RejectAction(
            ESAITestCapabilityRequestDto request,
            string statusCode,
            string message)
        {
            return RejectedWithReceipt(statusCode, message, CreateInputUseReceipt(
                request,
                false,
                false,
                statusCode,
                message,
                ESAITestValueDto.FromBoolean(false),
                leaseOwner,
                leaseGeneration,
                HasLease));
        }

        private ESAITestValueDto CreateInputUseReceipt(
            ESAITestCapabilityRequestDto request,
            bool accepted,
            bool executed,
            string statusCode,
            string message,
            ESAITestValueDto value,
            string owner,
            int generation,
            bool held)
        {
            var receipt = new ESAITestUseResultDto
            {
                accepted = accepted,
                executed = executed,
                statusCode = statusCode,
                message = message,
                runId = request?.runId ?? string.Empty,
                sceneGeneration = request?.sceneGeneration ?? 0,
                invocationId = request?.invocationId ?? string.Empty,
                stepId = request?.stepId ?? string.Empty,
                capabilityId = request?.capabilityId ?? Capability,
                command = request?.command ?? "control",
                target = request?.target ?? string.Empty,
                executionRoute = "ESInputModule.AITest/" + (request?.command ?? "control"),
                executionEvidenceKind = "es.input.lease-write",
                handlerMatched = false,
                leaseOwner = owner ?? string.Empty,
                leaseGeneration = generation,
                leaseHeld = held,
                executedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                value = value,
            };
            return ESAITestValueDto.FromString(JsonUtility.ToJson(receipt));
        }

        private ESAITestValueDto CreateInputVerifyEvidence(
            ESAITestCapabilityRequestDto request,
            bool passed,
            bool expected,
            bool actual,
            string message)
        {
            var evidence = new ESAITestVerifyResultDto
            {
                passed = passed,
                statusCode = passed ? ESAITestStatusCode.Passed : ESAITestStatusCode.VerificationFailed,
                message = message,
                runId = request?.runId ?? string.Empty,
                sceneGeneration = request?.sceneGeneration ?? 0,
                invocationId = request?.invocationId ?? string.Empty,
                stepId = request?.stepId ?? string.Empty,
                capabilityId = request?.capabilityId ?? Capability,
                command = request?.command ?? "control.state",
                target = request?.target ?? string.Empty,
                expectedValue = expected.ToString(),
                actualValue = actual.ToString(),
                evidenceKind = "es.input.control.state",
                observedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                value = ESAITestValueDto.FromBoolean(actual),
            };
            return ESAITestValueDto.FromString(JsonUtility.ToJson(evidence));
        }

        private static ESAITestCapabilityResponseDto RejectedWithReceipt(
            string statusCode,
            string message,
            ESAITestValueDto value)
        {
            return new ESAITestCapabilityResponseDto
            {
                accepted = false,
                conditionMet = false,
                retryable = false,
                statusCode = statusCode,
                message = message,
                value = value,
            };
        }

        private bool ReleaseLease()
        {
            if (string.IsNullOrEmpty(leaseOwner))
                return false;

            bool released = ESGameManager.InputModule?.ReleaseAITestInput(leaseOwner, leaseToken, leaseGeneration) == true;
            ClearLocalLease();
            return released;
        }

        private void ClearStaleLocalLease()
        {
            if (!string.IsNullOrEmpty(leaseOwner) && !HasLease)
                ClearLocalLease();
        }

        private void ClearLocalLease()
        {
            leaseOwner = null;
            leaseToken = 0;
            leaseGeneration = 0;
        }

        private bool HasLease
        {
            get
            {
                if (string.IsNullOrEmpty(leaseOwner))
                    return false;

                ESInputModule module = ESGameManager.InputModule;
                return module != null && module.IsAITestInputLeaseHeld(leaseOwner, leaseToken, leaseGeneration);
            }
        }

        private bool HasLeaseForRequest(ESAITestCapabilityRequestDto request, out string error)
        {
            if (!HasLease)
            {
                error = "必须先执行 control.acquire，且 Lease 必须仍然有效。";
                return false;
            }

            if (!string.Equals(leaseOwner, request?.runId, StringComparison.Ordinal))
            {
                error = "输入 Lease 归属与当前 Run 不一致。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void HandleRuntimeDeactivated()
        {
            ReleaseLease();
        }

        private static ESAITestCapabilityResponseDto Accepted(bool conditionMet, bool retryable, string message, ESAITestValueDto value)
        {
            // Capability admission only: accepted=true never declares task
            // completion or release acceptance; TaskContext/ABCD owns the
            // final decision.
            return new ESAITestCapabilityResponseDto
            {
                accepted = true,
                conditionMet = conditionMet,
                retryable = retryable,
                statusCode = conditionMet ? ESAITestStatusCode.Passed : ESAITestStatusCode.VerificationFailed,
                message = message,
                value = value,
            };
        }
    }

    public static class ESAITestInputProviderBootstrap
    {
        private static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            ESAITestRuntime.Activated -= EnsureProvider;
            ESAITestRuntime.Activated += EnsureProvider;
            ESAITestRuntime.Deactivated -= DestroyProvider;
            ESAITestRuntime.Deactivated += DestroyProvider;
        }

        private static void EnsureProvider()
        {
            if (host != null)
                return;
            host = new GameObject("ESAITest ES Input Provider");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ESAITestInputCapabilityProvider>();
        }

        private static void DestroyProvider()
        {
            if (host == null)
                return;
            UnityEngine.Object.Destroy(host);
            host = null;
        }
    }
}
