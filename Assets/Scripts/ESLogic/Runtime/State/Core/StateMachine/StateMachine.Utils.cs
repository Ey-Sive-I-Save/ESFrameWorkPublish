using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        public bool IsIdle()
        {
            return runningStates.Count == 0;
        }

        public bool IsLayerIdle(StateLayerType layerType)
        {
            var layer = GetLayerByType(layerType);
            return layer != null && !layer.HasActiveStates;
        }

        public int GetRunningStateCount()
        {
            return runningStates.Count;
        }

        public int GetLayerStateCount(StateLayerType layerType)
        {
            var layer = GetLayerByType(layerType);
            return layer != null ? layer.runningStates.Count : 0;
        }

        public float GetStateWeight(StateBase state)
        {
            if (state == null) return 0f;
            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                var layer = GetLayerByType(layerType);
                return layer != null ? layer.GetStateWeight(state) : 0f;
            }
            return 0f;
        }

        public float GetStateWeight(string stateKey)
        {
            var state = GetStateByString(stateKey);
            return GetStateWeight(state);
        }

        public float GetStateWeight(int stateId)
        {
            var state = GetStateByInt(stateId);
            return GetStateWeight(state);
        }

        #region State Animation Override

        public bool TryOverrideStateAnimationClip(StateBase state, int clipIndex, AnimationClip newClip)
        {
            return state != null && state.TryOverrideAnimationClip(clipIndex, newClip);
        }

        public bool TryOverrideStateAnimationClip(StateBase state, string marker, AnimationClip newClip)
        {
            return state != null && state.TryOverrideAnimationClip(marker, newClip);
        }

        public bool TryOverrideStateAnimationClip(StateBase state, AnimationClip sourceClip, AnimationClip newClip)
        {
            return state != null && state.TryOverrideAnimationClip(sourceClip, newClip);
        }

        public int TryOverrideStateAnimationClips(StateBase state, IList<StateAnimationClipOverrideRule> rules)
        {
            return state != null ? state.TryOverrideAnimationClips(rules) : 0;
        }

        public int RestoreStateAnimationClipOverrides(StateBase state)
        {
            return state != null ? state.RestoreAllAnimationClipOverrides() : 0;
        }

        public bool TryGetStateAnimationClipOverrideSlot(StateBase state, int clipIndex, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            slot = default;
            return state != null && state.TryGetAnimationClipOverrideSlot(clipIndex, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(StateBase state, string marker, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            slot = default;
            return state != null && state.TryGetAnimationClipOverrideSlot(marker, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(StateBase state, AnimationClip sourceOrCurrentClip, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            slot = default;
            return state != null && state.TryGetAnimationClipOverrideSlot(sourceOrCurrentClip, out slot);
        }

        public int TryOverrideRunningStateAnimationClip(string marker, AnimationClip newClip)
        {
            if (string.IsNullOrWhiteSpace(marker) || newClip == null)
                return 0;

            int changedCount = 0;
            var states = runningStates.Items;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && state.TryOverrideAnimationClip(marker, newClip))
                {
                    changedCount++;
                }
            }
            return changedCount;
        }

        public int TryOverrideRunningStateAnimationClip(AnimationClip sourceClip, AnimationClip newClip)
        {
            if (sourceClip == null || newClip == null)
                return 0;

            int changedCount = 0;
            var states = runningStates.Items;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && state.TryOverrideAnimationClip(sourceClip, newClip))
                {
                    changedCount++;
                }
            }
            return changedCount;
        }

        public int RestoreRunningStateAnimationClipOverrides()
        {
            int changedCount = 0;
            var states = runningStates.Items;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null)
                {
                    changedCount += state.RestoreAllAnimationClipOverrides();
                }
            }
            return changedCount;
        }

        public bool TryGetRunningStateAnimationClipOverrideSlot(string marker, out StateBase matchedState, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            matchedState = null;
            slot = default;
            if (string.IsNullOrWhiteSpace(marker))
                return false;

            var states = runningStates.Items;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state != null && state.TryGetAnimationClipOverrideSlot(marker, out slot))
                {
                    matchedState = state;
                    return true;
                }
            }

            return false;
        }

        public bool TryOverrideStateAnimationClip(string stateKey, int clipIndex, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByString(stateKey), clipIndex, newClip);
        }

        public bool TryOverrideStateAnimationClip(string stateKey, string marker, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByString(stateKey), marker, newClip);
        }

        public bool TryOverrideStateAnimationClip(string stateKey, AnimationClip sourceClip, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByString(stateKey), sourceClip, newClip);
        }

        public int TryOverrideStateAnimationClips(string stateKey, IList<StateAnimationClipOverrideRule> rules)
        {
            return TryOverrideStateAnimationClips(GetStateByString(stateKey), rules);
        }

        public int RestoreStateAnimationClipOverrides(string stateKey)
        {
            return RestoreStateAnimationClipOverrides(GetStateByString(stateKey));
        }

        public bool TryGetStateAnimationClipOverrideSlot(string stateKey, int clipIndex, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByString(stateKey), clipIndex, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(string stateKey, string marker, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByString(stateKey), marker, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(string stateKey, AnimationClip sourceOrCurrentClip, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByString(stateKey), sourceOrCurrentClip, out slot);
        }

        public bool TryOverrideStateAnimationClip(int stateId, int clipIndex, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByInt(stateId), clipIndex, newClip);
        }

        public bool TryOverrideStateAnimationClip(int stateId, string marker, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByInt(stateId), marker, newClip);
        }

        public bool TryOverrideStateAnimationClip(int stateId, AnimationClip sourceClip, AnimationClip newClip)
        {
            return TryOverrideStateAnimationClip(GetStateByInt(stateId), sourceClip, newClip);
        }

        public int TryOverrideStateAnimationClips(int stateId, IList<StateAnimationClipOverrideRule> rules)
        {
            return TryOverrideStateAnimationClips(GetStateByInt(stateId), rules);
        }

        public int RestoreStateAnimationClipOverrides(int stateId)
        {
            return RestoreStateAnimationClipOverrides(GetStateByInt(stateId));
        }

        public bool TryGetStateAnimationClipOverrideSlot(int stateId, int clipIndex, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByInt(stateId), clipIndex, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(int stateId, string marker, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByInt(stateId), marker, out slot);
        }

        public bool TryGetStateAnimationClipOverrideSlot(int stateId, AnimationClip sourceOrCurrentClip, out AnimationCalculatorRuntime.ClipOverrideSlot slot)
        {
            return TryGetStateAnimationClipOverrideSlot(GetStateByInt(stateId), sourceOrCurrentClip, out slot);
        }

        #endregion

        public string GetRootMixerDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== RootMixer调试信息 ==========");

            sb.AppendLine($"PlayableGraph有效: {playableGraph.IsValid()}");
            if (playableGraph.IsValid())
            {
                sb.AppendLine($"PlayableGraph运行中: {playableGraph.IsPlaying()}");
                sb.AppendLine($"PlayableGraph名称: {playableGraph.GetEditorName()}");
            }

            sb.AppendLine($"\nRootMixer有效: {rootMixer.IsValid()}");
            if (rootMixer.IsValid())
            {
                int inputCount = rootMixer.GetInputCount();
                sb.AppendLine($"RootMixer输入数: {inputCount}");

                for (int i = 0; i < inputCount; i++)
                {
                    var input = rootMixer.GetInput(i);
                    float weight = rootMixer.GetInputWeight(i);
                    StateLayerType layerType = (StateLayerType)i;

                    sb.AppendLine($"\n  槽位[{i}] - {layerType}:");
                    sb.AppendLine($"    输入有效: {input.IsValid()}");
                    sb.AppendLine($"    权重: {weight:F3}");

                    if (input.IsValid())
                    {
                        if (input.IsPlayableOfType<AnimationMixerPlayable>())
                        {
                            var mixer = (AnimationMixerPlayable)input;
                            int subInputCount = mixer.GetInputCount();
                            sb.AppendLine($"    子输入数: {subInputCount}");

                            var layer = GetLayerByType(layerType);
                            if (layer != null)
                            {
                                sb.AppendLine($"    运行状态数: {layer.runningStates.Count}");
                            }
                        }
                    }
                }
            }

            sb.AppendLine($"\nAnimator绑定: {boundAnimator != null}");
            if (boundAnimator != null)
            {
                sb.AppendLine($"Animator启用: {boundAnimator.enabled}");
                sb.AppendLine($"Animator路径: {boundAnimator.gameObject.name}");
            }

            sb.AppendLine($"\nOutput有效: {animationOutput.IsOutputValid()}");
            if (animationOutput.IsOutputValid())
            {
                var sourcePlayable = animationOutput.GetSourcePlayable();
                sb.AppendLine($"Output源Playable有效: {sourcePlayable.IsValid()}");
                sb.AppendLine($"Output权重: {animationOutput.GetWeight():F3}");
            }

            sb.AppendLine("========================================");
            return sb.ToString();
        }

        public void SetStateRuntimePhase(StateBase state, StateRuntimePhase phase, bool lockPhase = true)
        {
            if (state != null)
            {
                state.SetRuntimePhase(phase, lockPhase);
            }
        }

        public void ClearStateRuntimePhaseOverride(StateBase state)
        {
            if (state != null)
            {
                state.ClearRuntimePhaseOverride();
            }
        }

        public void MarkStateResolvedConfigDirty(StateBase state)
        {
            if (state != null)
            {
                state.MarkResolvedConfigDirty();
            }
        }

        public void RefreshStateSharedDataAndResolvedConfig(StateBase state)
        {
            if (state != null)
            {
                state.RefreshSharedDataCacheAndResolvedConfig();
            }
        }

        #region StateContext便捷访问

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMotionSpeedXZ(float localSpeedX, float localSpeedZ)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.ApplyMotionSpeedXZ(localSpeedX, localSpeedZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAvgSpeedXZ(float avgSpeedX, float avgSpeedZ)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.AvgSpeedX = avgSpeedX;
            ctx.AvgSpeedZ = avgSpeedZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetClimbInput(float horizontal, float vertical)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.ClimbHorizontal = horizontal;
            ctx.ClimbVertical = vertical;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(StateDefaultFloatParameter parameter, float value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                ctx.SetDefaultFloat(parameter, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(StateDefaultFloatParameter parameter, float defaultValue = 0f)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.GetDefaultFloat(parameter, defaultValue);
            }
            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInt(StateDefaultIntParameter parameter, int value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                ctx.SetDefaultInt(parameter, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(StateDefaultIntParameter parameter, int defaultValue = 0)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.GetDefaultInt(parameter, defaultValue);
            }
            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInt(StateDefaultIntParameter parameter, out int value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.TryGetDefaultInt(parameter, out value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasInt(StateDefaultIntParameter parameter)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.HasDefaultInt(parameter);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBool(StateDefaultBoolParameter parameter, bool value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                ctx.SetDefaultBool(parameter, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBool(StateDefaultBoolParameter parameter, bool defaultValue = false)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.GetDefaultBool(parameter, defaultValue);
            }
            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBool(StateDefaultBoolParameter parameter, out bool value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.TryGetDefaultBool(parameter, out value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBool(StateDefaultBoolParameter parameter)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.HasDefaultBool(parameter);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(StateParameter parameter, float value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                ctx.SetFloat(parameter, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string paramName, float value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                ctx.SetFloat(paramName, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(StateParameter parameter, float defaultValue = 0f)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.GetFloat(parameter, defaultValue);
            }
            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(string paramName, float defaultValue = 0f)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.GetFloat(paramName, defaultValue);
            }
            return defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFloat(string paramName, out float value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.TryGetFloat(paramName, out value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFloat(StateParameter parameter, out float value)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.TryGetFloat(parameter, out value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFloat(StateParameter parameter)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.HasFloat(parameter);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFloat(string paramName)
        {
            var ctx = stateContext;
            if (ctx != null)
            {
                return ctx.HasFloat(paramName);
            }
            return false;
        }

        #endregion
    }
}
