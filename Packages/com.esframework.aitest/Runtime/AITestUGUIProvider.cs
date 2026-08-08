using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ESFramework.ESAITest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ESAITest/UGUI Capability Provider")]
    public sealed class ESAITestUGUIProvider : MonoBehaviour, ESAITestCapabilityProvider
    {
        [Serializable]
        public sealed class ESAITestButtonBinding
        {
            public string id;
            public Button button;
        }

        [Serializable]
        public sealed class ESAITestToggleBinding
        {
            public string id;
            public Toggle toggle;
        }

        [SerializeField] private string capabilityId = "unity.ugui";
        [SerializeField] private List<ESAITestButtonBinding> buttons = new List<ESAITestButtonBinding>();
        [SerializeField] private List<ESAITestToggleBinding> toggles = new List<ESAITestToggleBinding>();

        public string CapabilityId => capabilityId;
        public string ProviderId => "esframework.aitest.ugui";
        public int ProviderVersion => 1;
        public string[] Commands => new[] { "button.click", "button.interactable", "toggle.state" };

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
            switch (request.command)
            {
                case "button.click":
                    if (!IsOperation(request, ESAITestProtocol.OperationAct))
                        return RejectOperation(request, "button.click", ESAITestProtocol.OperationAct);
                    return ClickButton(request);
                case "button.interactable":
                    if (!IsReadOperation(request))
                        return RejectOperation(request, "button.interactable", "see/verify");
                    return ReadButtonInteractable(request);
                case "toggle.state":
                    if (!IsReadOperation(request))
                        return RejectOperation(request, "toggle.state", "see/verify");
                    return ReadToggleState(request);
                default:
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未知 UGUI 命令：" + request.command);
            }
        }

        private static bool IsOperation(ESAITestCapabilityRequestDto request, string operation)
        {
            return string.Equals(request.operation, operation, StringComparison.OrdinalIgnoreCase);
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
                command + " 只允许 " + expectedOperation + "，当前 operation=" + request.operation);
        }

        private void Register()
        {
            if (!isActiveAndEnabled || !ESAITestRuntime.IsActive)
                return;
            if (gameObject.scene != SceneManager.GetActiveScene())
                return;

            if (!ESAITestRuntime.Registry.Register(this, ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration, out string error))
                Debug.LogError("[ESAITest] UGUI Provider 注册失败：" + error, this);
        }

        private ESAITestCapabilityResponseDto ClickButton(ESAITestCapabilityRequestDto request)
        {
            Button button = FindButton(request.target);
            if (button == null)
                return RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                    "未找到显式 Button 绑定：" + request.target,
                    CreateUseReceipt(request, false, false, false, ESAITestStatusCode.CapabilityRejected,
                        "未找到显式 Button 绑定。", false, false, false, false, false, ESAITestValueDto.FromBoolean(false)));

            bool activeBefore = button.isActiveAndEnabled;
            bool interactableBefore = button.interactable;
            if (!activeBefore || !interactableBefore)
                return Accepted(false, false, "Button 当前不可点击。", CreateUseReceipt(
                    request,
                    true,
                    false,
                    false,
                    ESAITestStatusCode.VerificationFailed,
                    "Button 当前不可点击。",
                    activeBefore,
                    interactableBefore,
                    true,
                    activeBefore,
                    interactableBefore,
                    ESAITestValueDto.FromBoolean(false)));
            if (EventSystem.current == null)
                return RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                    "场景缺少 EventSystem，不能执行 UGUI 点击。",
                    CreateUseReceipt(request, false, false, false, ESAITestStatusCode.CapabilityRejected,
                        "场景缺少 EventSystem，不能执行 UGUI 点击。", activeBefore, interactableBefore,
                        true, activeBefore, interactableBefore, ESAITestValueDto.FromBoolean(false)));

            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            bool handlerMatched = ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            bool targetExistsAfter = button != null;
            bool activeAfter = targetExistsAfter && button.isActiveAndEnabled;
            bool interactableAfter = targetExistsAfter && button.interactable;
            if (!handlerMatched)
                return RejectedWithReceipt(ESAITestStatusCode.CapabilityRejected,
                    "未命中 Button 的 PointerClick 事件处理器。",
                    CreateUseReceipt(request, false, false, false, ESAITestStatusCode.CapabilityRejected,
                        "未命中 Button 的 PointerClick 事件处理器。", activeBefore, interactableBefore,
                        targetExistsAfter, activeAfter, interactableAfter, ESAITestValueDto.FromBoolean(false)));

            return Accepted(true, false, "已通过 EventSystem 执行 Button 点击。", CreateUseReceipt(
                request,
                true,
                true,
                true,
                ESAITestStatusCode.Passed,
                "已通过 EventSystem 执行 Button 点击。",
                activeBefore,
                interactableBefore,
                targetExistsAfter,
                activeAfter,
                interactableAfter,
                ESAITestValueDto.FromBoolean(true)));
        }

        private ESAITestCapabilityResponseDto ReadButtonInteractable(ESAITestCapabilityRequestDto request)
        {
            Button button = FindButton(request.target);
            if (button == null)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未找到显式 Button 绑定：" + request.target);

            bool value = button.isActiveAndEnabled && button.interactable;
            return BuildBooleanResponse(request, value, "Button interactable=" + value, "ugui.button.interactable");
        }

        private ESAITestCapabilityResponseDto ReadToggleState(ESAITestCapabilityRequestDto request)
        {
            Toggle toggle = FindToggle(request.target);
            if (toggle == null)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未找到显式 Toggle 绑定：" + request.target);

            return BuildBooleanResponse(request, toggle.isOn, "Toggle isOn=" + toggle.isOn, "ugui.toggle.state");
        }

        private static ESAITestCapabilityResponseDto BuildBooleanResponse(
            ESAITestCapabilityRequestDto request,
            bool value,
            string message,
            string evidenceKind)
        {
            bool isSee = string.Equals(request.operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase);
            if (isSee)
                return Accepted(true, false, message, ESAITestValueDto.FromBoolean(value));

            if (!bool.TryParse(request.expectedValue, out bool expected))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InvalidRequest, "expectedValue 必须为 true 或 false。");

            bool met = value == expected;
            string resultMessage = message + ", expected=" + expected;
            return Accepted(met, !met, resultMessage, CreateVerifyEvidence(
                request,
                met,
                expected,
                value,
                evidenceKind,
                resultMessage));
        }

        private static ESAITestValueDto CreateUseReceipt(
            ESAITestCapabilityRequestDto request,
            bool accepted,
            bool executed,
            bool handlerMatched,
            string statusCode,
            string message,
            bool targetActiveBefore,
            bool targetInteractableBefore,
            bool targetExistsAfter,
            bool targetActiveAfter,
            bool targetInteractableAfter,
            ESAITestValueDto value)
        {
            var receipt = new ESAITestUseResultDto
            {
                accepted = accepted,
                executed = executed,
                handlerMatched = handlerMatched,
                statusCode = statusCode,
                message = message,
                runId = request?.runId ?? string.Empty,
                sceneGeneration = request?.sceneGeneration ?? 0,
                invocationId = request?.invocationId ?? string.Empty,
                stepId = request?.stepId ?? string.Empty,
                capabilityId = request?.capabilityId ?? string.Empty,
                command = request?.command ?? string.Empty,
                target = request?.target ?? string.Empty,
                executionRoute = "EventSystem.pointerClickHandler",
                executionEvidenceKind = "ugui.pointer-click-handler",
                executedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                targetActiveBefore = targetActiveBefore,
                targetInteractableBefore = targetInteractableBefore,
                targetExistsAfter = targetExistsAfter,
                targetActiveAfter = targetActiveAfter,
                targetInteractableAfter = targetInteractableAfter,
                value = value,
            };
            return ESAITestValueDto.FromString(JsonUtility.ToJson(receipt));
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

        private static ESAITestValueDto CreateVerifyEvidence(
            ESAITestCapabilityRequestDto request,
            bool passed,
            bool expected,
            bool actual,
            string evidenceKind,
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
                capabilityId = request?.capabilityId ?? string.Empty,
                command = request?.command ?? string.Empty,
                target = request?.target ?? string.Empty,
                expectedValue = expected.ToString(),
                actualValue = actual.ToString(),
                evidenceKind = evidenceKind ?? string.Empty,
                observedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                value = ESAITestValueDto.FromBoolean(actual),
            };
            return ESAITestValueDto.FromString(JsonUtility.ToJson(evidence));
        }

        private static ESAITestCapabilityResponseDto Accepted(bool conditionMet, bool retryable, string message, ESAITestValueDto value)
        {
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

        private Button FindButton(string id)
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i] != null && string.Equals(buttons[i].id, id, StringComparison.Ordinal))
                    return buttons[i].button;
            return null;
        }

        private Toggle FindToggle(string id)
        {
            for (int i = 0; i < toggles.Count; i++)
                if (toggles[i] != null && string.Equals(toggles[i].id, id, StringComparison.Ordinal))
                    return toggles[i].toggle;
            return null;
        }
    }
}
