using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private void ApplyFadeIn(StateBase state, StateLayerRuntime layer)
        {
            if (!state.stateSharedData.enableFadeInOut) return;

            float fadeInDuration = GetScaledFadeDuration(state.stateSharedData.fadeInDuration, state.stateSharedData);
            if (fadeInDuration <= 0f || !layer.stateToSlotMap.ContainsKey(state))
                return;

            int slotIndex = layer.stateToSlotMap[state];
            state.SetPlayableWeightAssumeBound(0f);

            if (!layer.fadeInStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeInDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = 1f;
                layer.fadeInStates[state] = fadeData;

                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡入] 状态 {state.strKey} 开始淡入，时长 {fadeInDuration:F2}秒");
            }
        }

        private void ApplyFadeOut(StateBase state, StateLayerRuntime layer)
        {
            if (!state.stateSharedData.enableFadeInOut) return;

            float fadeOutDuration = GetScaledFadeDuration(state.stateSharedData.fadeOutDuration, state.stateSharedData);
            if (fadeOutDuration <= 0f)
                return;

            if (!layer.stateToSlotMap.ContainsKey(state))
                return;

            int slotIndex = layer.stateToSlotMap[state];
            float currentWeight = Mathf.Clamp01(state.PlayableWeight);
            state.SetPlayableWeightAssumeBound(currentWeight);

            if (!layer.fadeOutStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeOutDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = currentWeight;
                layer.fadeOutStates[state] = fadeData;

                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡出] 状态 {state.strKey} 开始淡出，时长 {fadeOutDuration:F2}秒，起始权重 {currentWeight:F2}");
            }
        }
        private void UpdateLayerFades(StateLayerRuntime layer, float deltaTime)
        {
            if (layer.fadeInStates.Count == 0 && layer.fadeOutStates.Count == 0)
                return;

            var fadeInToRemove = layer.fadeInToRemoveCache;
            fadeInToRemove.Clear();
            foreach (var kvp in layer.fadeInStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: true);
                float weight = Mathf.Lerp(0f, 1f, eased);
                state.SetPlayableWeightAssumeBound(weight);

                if (t >= 1f)
                {
                    fadeInToRemove.Add(state);
#if STATEMACHINEDEBUG
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡入完成] 状态 {state.strKey}");
#endif
                }
            }

            foreach (var state in fadeInToRemove)
            {
                if (layer.fadeInStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                layer.fadeInStates.Remove(state);
            }

            var fadeOutToRemove = layer.fadeOutToRemoveCache;
            fadeOutToRemove.Clear();
            foreach (var kvp in layer.fadeOutStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: false);
                float weight = Mathf.Lerp(fadeData.startWeight, 0f, eased);
                state.SetPlayableWeightAssumeBound(weight);

                if (state.baseStatus != StateBaseStatus.Running && stateContext != null)
                {
                    state.UpdateAnimationRuntimeForFadeOut(stateContext, deltaTime);
                }

                if (t >= 1f)
                {
                    fadeOutToRemove.Add(state);
                    HotUnplugStateFromPlayable(state, layer);
#if STATEMACHINEDEBUG
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡出完成] 状态 {state.strKey}");
#endif
                }
            }

            foreach (var state in fadeOutToRemove)
            {
                if (layer.fadeOutStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                layer.fadeOutStates.Remove(state);
            }
        }

        private float GetScaledFadeDuration(float baseDuration, StateSharedData sharedData)
        {
            if (baseDuration <= 0f) return baseDuration;

            float scale = Mathf.Max(0.01f, sharedData.fadeSpeedMultiplier);
            if (sharedData.fadeFollowTimeScale)
                scale *= Mathf.Max(0.01f, Time.timeScale);

            return baseDuration / scale;
        }

        private float EvaluateFadeCurve(StateBase state, float t, bool isFadeIn)
        {
            if (!state.stateSharedData.useAdvancedFadeCurve) return t;

            var curve = isFadeIn ? state.stateSharedData.fadeInCurve : state.stateSharedData.fadeOutCurve;
            if (curve == null || curve.length == 0)
            {
                return t;
            }
            return Mathf.Clamp01(curve.Evaluate(t));
        }

        private void CancelStaleFadeData(StateBase state, StateLayerRuntime layer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (layer == null) throw new System.ArgumentNullException(nameof(layer));
#endif

            if (layer.fadeOutStates.TryGetValue(state, out var fadeOutData))
            {
                HotUnplugStateFromPlayable(state, layer);
                fadeOutData.TryAutoPushedToPool();
                layer.fadeOutStates.Remove(state);
#if STATEMACHINEDEBUG
                StateMachineDebugSettings.Instance.LogFade(
                    $"[Fade修复] 取消状态 {state.strKey} 的残留fadeOut（快速重入）");
#endif
            }

            if (layer.fadeInStates.TryGetValue(state, out var fadeInData))
            {
                fadeInData.TryAutoPushedToPool();
                layer.fadeInStates.Remove(state);
#if STATEMACHINEDEBUG
                StateMachineDebugSettings.Instance.LogFade(
                    $"[Fade修复] 取消状态 {state.strKey} 的残留fadeIn（快速重入）");
#endif
            }
        }
    }
}