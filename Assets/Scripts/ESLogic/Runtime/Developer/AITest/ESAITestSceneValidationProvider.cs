using System;
using ESFramework.ESAITest;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ESSceneValidationGuide))]
    [AddComponentMenu("【ES】/开发与验证/诊断/ESAITest Scene Validation Provider")]
    public sealed class ESAITestSceneValidationProvider : MonoBehaviour, ESAITestCapabilityProvider
    {
        [SerializeField] private ESSceneValidationGuide guide;
        [SerializeField] private string capabilityId = "es.scene-validation";

        public string CapabilityId => capabilityId;
        public string ProviderId => "es.aitest.scene-validation";
        public int ProviderVersion => 1;
        public string[] Commands => new[] { "check.state" };

        private void Reset()
        {
            guide = GetComponent<ESSceneValidationGuide>();
        }

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
            if (!string.Equals(request.command, "check.state", StringComparison.Ordinal))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未知 SceneValidation 命令：" + request.command);
            if (!string.Equals(request.operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(request.operation, ESAITestProtocol.OperationVerify, StringComparison.OrdinalIgnoreCase))
                return ESAITestCapabilityResponseDto.Reject(
                    ESAITestStatusCode.CapabilityRejected,
                    "check.state 只允许 see/verify，当前 operation=" + request.operation);
            if (guide == null)
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未绑定 ESSceneValidationGuide。");
            if (!guide.TryGetCheckState(request.target, out ESSceneValidationCheckState state, out string detail))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "Guide 中不存在检查：" + request.target);

            string current = ToProtocolState(state);
            var value = ESAITestValueDto.FromString(current);
            if (string.Equals(request.operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase))
                return Accepted(true, false, current + ": " + detail, value);

            bool conditionMet = string.Equals(current, request.expectedValue, StringComparison.OrdinalIgnoreCase);
            bool retryable = !conditionMet && state != ESSceneValidationCheckState.Failed;
            string message = current + ", expected=" + request.expectedValue + ": " + detail;
            return Accepted(conditionMet, retryable, message, CreateVerifyEvidence(request, conditionMet, current, message));
        }

        private void Register()
        {
            if (!isActiveAndEnabled || !ESAITestRuntime.IsActive)
                return;
            if (gameObject.scene != SceneManager.GetActiveScene())
                return;
            if (guide == null)
                guide = GetComponent<ESSceneValidationGuide>();

            if (!ESAITestRuntime.Registry.Register(this, ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration, out string error))
                Debug.LogError("[ESAITest] SceneValidation Provider 注册失败：" + error, this);
        }

        private static string ToProtocolState(ESSceneValidationCheckState state)
        {
            switch (state)
            {
                case ESSceneValidationCheckState.Passed: return "passed";
                case ESSceneValidationCheckState.Failed: return "failed";
                case ESSceneValidationCheckState.Information: return "information";
                default: return "pending";
            }
        }

        private static ESAITestValueDto CreateVerifyEvidence(
            ESAITestCapabilityRequestDto request,
            bool passed,
            string actual,
            string message)
        {
            var evidence = new ESAITestVerifyResultDto
            {
                passed = passed,
                statusCode = passed ? ESAITestStatusCode.Passed : ESAITestStatusCode.VerificationFailed,
                message = message ?? string.Empty,
                runId = request?.runId ?? string.Empty,
                sceneGeneration = request?.sceneGeneration ?? 0,
                invocationId = request?.invocationId ?? string.Empty,
                stepId = request?.stepId ?? string.Empty,
                capabilityId = request?.capabilityId ?? string.Empty,
                command = request?.command ?? string.Empty,
                target = request?.target ?? string.Empty,
                expectedValue = request?.expectedValue ?? string.Empty,
                actualValue = actual ?? string.Empty,
                evidenceKind = "es.scene-validation.check-state",
                observedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                value = ESAITestValueDto.FromString(actual ?? string.Empty),
            };
            return ESAITestValueDto.FromString(JsonUtility.ToJson(evidence));
        }

        private static ESAITestCapabilityResponseDto Accepted(bool conditionMet, bool retryable, string message, ESAITestValueDto value)
        {
            // Capability admission only: accepted=true never declares task
            // completion or runtime acceptance; the external authority gate
            // must consume conditionMet and its evidence separately.
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
}
