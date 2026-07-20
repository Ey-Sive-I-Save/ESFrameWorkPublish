using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private void ApplyFadeIn(StateBase state, StateLayerRuntime layer)
        {
            var sharedData = state != null ? state.stateSharedData : null;
            if (sharedData == null || !sharedData.enableFadeInOut) return;

            float fadeInDuration = GetScaledFadeDuration(sharedData.fadeInDuration, sharedData);
            if (fadeInDuration <= 0f || layer == null || !layer.stateToSlotMap.TryGetValue(state, out int slotIndex))
                return;

            state.SetPlayableWeightAssumeBound(0f);

            if (!layer.fadeInStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeInDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = 1f;
                layer.fadeInStates[state] = fadeData;

#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFadeEnabled)
                {
                    dbg.LogFade($"[淡入] 状态 {state.strKey} 开始淡入，时长 {fadeInDuration:F2}秒");
                }
#endif
            }
        }

        private void ApplyFadeOut(StateBase state, StateLayerRuntime layer)
        {
            var sharedData = state != null ? state.stateSharedData : null;
            if (sharedData == null || !sharedData.enableFadeInOut) return;

            float fadeOutDuration = GetScaledFadeDuration(sharedData.fadeOutDuration, sharedData);
            if (fadeOutDuration <= 0f)
                return;

            if (layer == null || !layer.stateToSlotMap.TryGetValue(state, out int slotIndex))
                return;

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
                TrackFadeOutIKState(state, layer.layerType);

#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFadeEnabled)
                {
                    dbg.LogFade($"[淡出] 状态 {state.strKey} 开始淡出，时长 {fadeOutDuration:F2}秒，起始权重 {currentWeight:F2}");
                }
#endif
            }
        }

        private void BeginFadeOutOrUnplugImmediately(StateBase state, StateLayerRuntime layer)
        {
            if (state == null || layer == null)
                return;

            RemoveFadeInData(state, layer);

            ApplyFadeOut(state, layer);

            if (layer.fadeOutStates != null && layer.fadeOutStates.ContainsKey(state))
            {
                TryUpdateMixerWeightsImmediately(layer);
            }

            var fadeSharedData = state.stateSharedData;
            bool shouldUnplugImmediately = fadeSharedData == null ||
                                           !fadeSharedData.enableFadeInOut ||
                                           fadeSharedData.fadeOutDuration <= 0f;
            if (!shouldUnplugImmediately)
                return;

            if (layer.stateToSlotMap.ContainsKey(state))
            {
                HotUnplugStateFromPlayable(state, layer);
            }

            RemoveFadeOutData(state, layer);
        }

        private void CleanupFadeDataForActivationRollback(StateBase state, StateLayerRuntime layer)
        {
            if (state == null || layer == null)
                return;

            RemoveFadeInData(state, layer);
            RemoveFadeOutData(state, layer);
        }

        private void RemoveFadeInData(StateBase state, StateLayerRuntime layer)
        {
            if (state == null || layer == null || layer.fadeInStates == null)
                return;

            if (layer.fadeInStates.TryGetValue(state, out var fadeInData))
            {
                fadeInData.TryAutoPushedToPool();
                layer.fadeInStates.Remove(state);
            }
        }

        private void RemoveFadeOutData(StateBase state, StateLayerRuntime layer)
        {
            if (state == null || layer == null || layer.fadeOutStates == null)
                return;

            if (layer.fadeOutStates.TryGetValue(state, out var fadeOutData))
            {
                fadeOutData.TryAutoPushedToPool();
                layer.fadeOutStates.Remove(state);
                UntrackFadeOutIKState(state);
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
                if (state == null || fadeData == null || fadeData.duration <= 0f || !layer.stateToSlotMap.ContainsKey(state))
                {
                    fadeInToRemove.Add(state);
                    continue;
                }

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: true);
                float weight = Mathf.Lerp(0f, 1f, eased);
                state.SetPlayableWeightAssumeBound(weight);

                if (t >= 1f)
                {
                    fadeInToRemove.Add(state);
#if STATEMACHINEDEBUG
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsFadeEnabled)
                    {
                        dbg.LogFade($"[淡入完成] 状态 {state.strKey}");
                    }
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
                if (state == null || fadeData == null || fadeData.duration <= 0f)
                {
                    fadeOutToRemove.Add(state);
                    continue;
                }

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
                    if (layer.stateToSlotMap.ContainsKey(state))
                    {
                        HotUnplugStateFromPlayable(state, layer);
                    }
#if STATEMACHINEDEBUG
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsFadeEnabled)
                    {
                        dbg.LogFade($"[淡出完成] 状态 {state.strKey}");
                    }
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
                UntrackFadeOutIKState(state);
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
            var sharedData = state != null ? state.stateSharedData : null;
            if (sharedData == null || !sharedData.useAdvancedFadeCurve) return t;

            var curve = isFadeIn ? sharedData.fadeInCurve : sharedData.fadeOutCurve;
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
                UntrackFadeOutIKState(state);
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFadeEnabled)
                {
                    dbg.LogFade($"[Fade修复] 取消状态 {state.strKey} 的残留fadeOut（快速重入）");
                }
#endif
            }

            if (layer.fadeInStates.TryGetValue(state, out var fadeInData))
            {
                fadeInData.TryAutoPushedToPool();
                layer.fadeInStates.Remove(state);
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFadeEnabled)
                {
                    dbg.LogFade($"[Fade修复] 取消状态 {state.strKey} 的残留fadeIn（快速重入）");
                }
#endif
            }
        }

        private void ForceClearLayerFadesAndResidualPlayables(StateLayerRuntime layer)
        {
            if (layer == null) return;
            ClearAllWeakInterruptions();

            CleanupFadeDict(layer.fadeInStates);
            CleanupFadeDict(layer.fadeOutStates);
            _fadeOutIKStates.Clear();

            if (layer.stateToSlotMap.Count > 0)
            {
                var buffer = _statesToDeactivateCache;
                buffer.Clear();
                foreach (var kvp in layer.stateToSlotMap)
                {
                    if (kvp.Key != null) buffer.Add(kvp.Key);
                }
                for (int i = 0; i < buffer.Count; i++)
                {
                    HotUnplugStateFromPlayable(buffer[i], layer);
                }
                buffer.Clear();
            }
        }

        private void CleanupFadeDict(Dictionary<StateBase, StateFadeData> dict)
        {
            if (dict == null) return;
            foreach (var kvp in dict) kvp.Value?.TryAutoPushedToPool();
            dict.Clear();
        }
    }
}
