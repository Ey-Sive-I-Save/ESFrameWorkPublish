using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        private const int MaxConnectedStates = 32; // 与 maxPlayableSlots 默认值一致
        private readonly float[] _mixerEffectiveWeights = new float[MaxConnectedStates];
        private readonly int[] _mixerWeightSortIndices = new int[MaxConnectedStates];
        private void UpdateMixerInputWeights()
        {
            if (_isUpdatingMixerInputWeights) return;
            _isUpdatingMixerInputWeights = true;
            try
            {
                {
                    var layer = baseLayer;
                    if (layer.mixer.IsValid() && layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights))
                        UpdateMixerInputWeightsForLayer(layer);
                }
                {
                    var layer = mainLayer;
                    if (layer.mixer.IsValid() && layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights))
                        UpdateMixerInputWeightsForLayer(layer);
                }
                {
                    var layer = buffLayer;
                    if (layer.mixer.IsValid() && layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights))
                        UpdateMixerInputWeightsForLayer(layer);
                }
                {
                    var layer = upperBodyLayer;
                    if (layer.mixer.IsValid() && layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights))
                        UpdateMixerInputWeightsForLayer(layer);
                }
                {
                    var layer = lowerBodyLayer;
                    if (layer.mixer.IsValid() && layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights))
                        UpdateMixerInputWeightsForLayer(layer);
                }
            }
            finally
            {
                _isUpdatingMixerInputWeights = false;
            }
        }

        private void UpdateMixerInputWeightsForLayer(StateLayerRuntime layer)
        {
            // 有效性校验
            if (!layer.mixer.IsValid()) return;

            var states = layer.connectedStates;
            var slots = layer.connectedSlots;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (states.Count != slots.Count)
                throw new System.InvalidOperationException(
                    $"connectedStates/connectedSlots 计数不一致: {layer.layerType}");
#endif

            int stateCount = states.Count;
            if (stateCount == 0)
            {
                if (layer.hasReferencePose)
                {
                    layer.mixer.SetInputWeight(0, 1f);
                    layer.referencePoseWeightsNormalized = false;
                }
                layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
                return;
            }

            // 固定数组越界检查（编辑器/开发构建下捕获异常）
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (stateCount > MaxConnectedStates)
                throw new System.InvalidOperationException(
                    $"层级 {layer.layerType} 连接状态数 {stateCount} 超过最大限制 {MaxConnectedStates}");
#endif

            // 初始化数组（仅处理实际状态数）
            for (int i = 0; i < stateCount; i++)
            {
                _mixerWeightSortIndices[i] = i;
                _mixerEffectiveWeights[i] = 0f;
            }

            // ========== 叠加层：不强制权重和为1，直接使用请求权重 ==========
            if (layer.layerType == StateLayerType.UpperBody || layer.layerType == StateLayerType.LowerBody)
            {
                for (int i = 0; i < stateCount; i++)
                {
                    var state = states[i];
                    float w = Mathf.Clamp01(state.RequestedPlayableWeight);
                    _mixerEffectiveWeights[i] = w;
                }

                for (int i = 0; i < stateCount; i++)
                {
                    var state = states[i];
                    float w = _mixerEffectiveWeights[i];
                    state.SetEffectivePlayableWeightNoDirty(w);
                    layer.mixer.SetInputWeight(slots[i], w);
                }

                if (layer.hasReferencePose)
                {
                    layer.mixer.SetInputWeight(0, 0f);
                    layer.referencePoseWeightsNormalized = false;
                }

                // 清理未使用槽位
                {
                    int inputCount = layer.mixer.GetInputCount();
                    int start = layer.hasReferencePose ? 1 : 0;
                    for (int slot = start; slot < inputCount; slot++)
                    {
                        bool used = false;
                        for (int i = 0; i < stateCount; i++)
                        {
                            if (slots[i] == slot) { used = true; break; }
                        }
                        if (!used) layer.mixer.SetInputWeight(slot, 0f);
                    }
                }

                layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
                return;
            }

            // ========== 普通层（Base / Main / Buff）：强制权重和=1 ==========

            // ---------- Dominant 独占检测 ----------
            StateBase dominantState = null;
            int dominantIdx = -1;
            for (int i = 0; i < stateCount; i++)
            {
                var s = states[i];
                s.EnsureResolvedRuntimeConfig();
                if (s.ResolvedConfig.mixerBias == StateMixerBias.Dominant)
                {
                    dominantState = s;
                    dominantIdx = i;
                    break;
                }
            }

            if (dominantState != null)
            {
                // 独占模式：Dominant 状态权重=1，其他清零（淡出中状态保留）
                for (int i = 0; i < stateCount; i++)
                {
                    var s = states[i];
                    if (i == dominantIdx)
                    {
                        _mixerEffectiveWeights[i] = 1f;
                        s.SetPlayableWeightAssumeBound(1f);
                    }
                    else
                    {
                        if (layer.fadeOutStates.ContainsKey(s))
                        {
                            _mixerEffectiveWeights[i] = Mathf.Clamp01(s.PlayableWeight);
                        }
                        else
                        {
                            _mixerEffectiveWeights[i] = 0f;
                            s.SetPlayableWeightAssumeBound(0f);
                        }
                    }
                }

                for (int i = 0; i < stateCount; i++)
                {
                    var s = states[i];
                    float w = _mixerEffectiveWeights[i];
                    s.SetEffectivePlayableWeightNoDirty(w);
                    layer.mixer.SetInputWeight(slots[i], w);
                }

                if (layer.hasReferencePose)
                {
                    layer.mixer.SetInputWeight(0, 0f);
                    layer.referencePoseWeightsNormalized = false;
                }

                // 清理未使用槽位
                {
                    int inputCount = layer.mixer.GetInputCount();
                    int start = layer.hasReferencePose ? 1 : 0;
                    for (int slot = start; slot < inputCount; slot++)
                    {
                        bool used = false;
                        for (int i = 0; i < stateCount; i++)
                        {
                            if (slots[i] == slot) { used = true; break; }
                        }
                        if (!used) layer.mixer.SetInputWeight(slot, 0f);
                    }
                }

                layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
                return;
            }

            // ---------- 软性优先级归一化（原逻辑） ----------
            float scoreSum = 0f;
            int bestIdx = 0;
            StateBase bestState = null;
            const float kEpsilon = 0.0001f;

            for (int i = 0; i < stateCount; i++)
            {
                var state = states[i];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (state == null) throw new System.InvalidOperationException($"connectedStates 存在 null: {layer.layerType}");
#endif

                float req = state.RequestedPlayableWeight;
                if (req <= 0f)
                {
                    _mixerEffectiveWeights[i] = 0f;
                }
                else
                {
                    if (req > 1f) req = 1f;
                    state.EnsureResolvedRuntimeConfig();
                    float mixerBiasFactor = ResolveMixerBiasScoreFactor(state.ResolvedConfig.mixerBias);
                    float score = req * mixerBiasFactor;
                    _mixerEffectiveWeights[i] = score;
                    scoreSum += score;
                }

                if (bestState == null || CompareStateForMixerWeightSort(bestState, state) > 0)
                {
                    bestState = state;
                    bestIdx = i;
                }
            }

            if (scoreSum <= kEpsilon)
            {
                for (int i = 0; i < stateCount; i++) _mixerEffectiveWeights[i] = 0f;
                _mixerEffectiveWeights[bestIdx] = 1f;
            }
            else
            {
                float inv = 1f / scoreSum;
                for (int i = 0; i < stateCount; i++)
                {
                    _mixerEffectiveWeights[i] *= inv;
                }
            }

            for (int i = 0; i < stateCount; i++)
            {
                var state = states[i];
                float w = _mixerEffectiveWeights[i];
                state.SetEffectivePlayableWeightNoDirty(w);
                layer.mixer.SetInputWeight(slots[i], w);
            }

            if (layer.hasReferencePose)
            {
                layer.mixer.SetInputWeight(0, 0f);
                layer.referencePoseWeightsNormalized = false;
            }

            // 清理未使用槽位
            {
                int inputCount = layer.mixer.GetInputCount();
                int start = layer.hasReferencePose ? 1 : 0;
                for (int slot = start; slot < inputCount; slot++)
                {
                    bool used = false;
                    for (int i = 0; i < stateCount; i++)
                    {
                        if (slots[i] == slot) { used = true; break; }
                    }
                    if (!used) layer.mixer.SetInputWeight(slot, 0f);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 校验权重和（仅在非叠加层且无 Dominant 时）
            {
                float sum = 0f;
                int inputCount = layer.mixer.GetInputCount();
                for (int i = 0; i < inputCount; i++)
                    sum += layer.mixer.GetInputWeight(i);

                if (Mathf.Abs(sum - 1f) > 0.001f)
                {
                    StateMachineDebugSettings.Instance.LogWarning(
                        $"[MixerWeightSum] Layer={layer.layerType} Sum={sum:F4} " +
                        $"InputCount={inputCount} HasRefPose={layer.hasReferencePose} " +
                        $"StateCount={stateCount}");
                }
            }
#endif

            layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryUpdateMixerWeightsImmediately(StateLayerRuntime layer)
        {
            if (layer == null) return;
            if (!isRunning) return;
            if (!playableGraph.IsValid()) return;
            if (!layer.mixer.IsValid()) return;
            if (_isUpdatingMixerInputWeights) return;

            layer.MarkDirty(PipelineDirtyFlags.MixerWeights);
            UpdateMixerInputWeights();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveMixerBiasScoreFactor(StateMixerBias mixerBias)
        {
            if (mixerBias == StateMixerBias.Dominant)
                return float.MaxValue; // 或一个特别大的值

            int index = (int)mixerBias;
            if ((uint)index >= MixerBiasScoreFactors.Length)
                index = (int)StateMixerBias.Normal;
            return MixerBiasScoreFactors[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CompareStateForMixerWeightSort(StateBase a, StateBase b)
        {
            if (ReferenceEquals(a, b)) return 0;

            a.EnsureResolvedRuntimeConfig();
            b.EnsureResolvedRuntimeConfig();

            int ba = (int)a.ResolvedConfig.mixerBias;
            int bb = (int)b.ResolvedConfig.mixerBias;
            if (ba != bb)
            {
                return bb.CompareTo(ba);
            }

            int timeCompare = b.activationTime.CompareTo(a.activationTime);
            if (timeCompare != 0) return timeCompare;

            return CompareStateDeterministic(a, b);
        }
    }
}