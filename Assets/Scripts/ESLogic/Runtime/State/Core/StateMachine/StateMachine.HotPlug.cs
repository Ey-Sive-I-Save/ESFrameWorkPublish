using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        internal void HotPlugStateToPlayable(StateBase state, StateLayerRuntime layer)
        {
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                {
                    dbg.LogAnimationBlend("[HotPlug] === 开始热插拔状态到Playable ===");
                    string stateKey = state != null ? state.strKey : "<null>";
                    string layerType = layer != null ? layer.layerType.ToString() : "<null>";
                    dbg.LogAnimationBlend($"[HotPlug] 状态: {stateKey} | 层级: {layerType}");
                }
            }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (layer == null) throw new System.ArgumentNullException(nameof(layer));
            if (state.stateSharedData == null) throw new System.InvalidOperationException("HotPlugStateToPlayable: state.stateSharedData 不能为空");
#else
            if (state == null || layer == null || state.stateSharedData == null) return;
#endif

            var sharedData = state.stateSharedData;

            if (!sharedData.hasAnimation)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                    dbg.LogAnimationBlend("[HotPlug] 状态无动画，跳过热插拔");
#endif
                return;
            }

            layer.MarkDirty(PipelineDirtyFlags.MixerWeights);

            if (layer.stateToSlotMap.ContainsKey(state))
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                    dbg.LogAnimationBlend("[HotPlug] 状态已在槽位映射中，跳过");
#endif
                return;
            }

            if (!playableGraph.IsValid() || !layer.mixer.IsValid())
            {
                StateMachineDebugSettings.Instance.LogError(
                    $"[HotPlug] 无法插入状态动画：PlayableGraph({playableGraph.IsValid()})或Mixer({layer.mixer.IsValid()})无效 | 层级:{layer.layerType} | 初始化:{isInitialized} | 运行:{isRunning}");
                return;
            }

            var animConfig = sharedData.animationConfig;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (animConfig == null) throw new System.InvalidOperationException($"HotPlugStateToPlayable: hasAnimation=true 但 animationConfig 为空 | State={state.strKey}");
#else
            if (animConfig == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {state.strKey} 标记了hasAnimation=true，但没有animationConfig");
                return;
            }
#endif

            var statePlayable = CreateStatePlayable(state, animConfig, layer);
            if (!statePlayable.IsValid())
            {
                StateMachineDebugSettings.Instance.LogWarning($"无法为状态 {state.strKey} 创建有效的Playable节点");
                return;
            }

            int inputIndex;

            if (layer.freeSlots.Count > 0)
            {
                inputIndex = layer.freeSlots.Pop();

                if (inputIndex == 0 && layer.hasReferencePose)
                {
                    int currentCount = layer.mixer.GetInputCount();
                    inputIndex = currentCount;
                    layer.mixer.SetInputCount(inputIndex + 1);
                }
                else if (layer.mixer.GetInput(inputIndex).IsValid())
                {
                    playableGraph.Disconnect(layer.mixer, inputIndex);
                }
            }
            else
            {
                int currentCount = layer.mixer.GetInputCount();
                if (currentCount >= layer.maxPlayableSlots)
                {
                    StateMachineDebugSettings.Instance.LogWarning($"层级 {layer.layerType} 已达到最大Playable槽位限制 {layer.maxPlayableSlots}，无法添加新状态");
                    statePlayable.Destroy();
                    return;
                }

                inputIndex = currentCount;
                layer.mixer.SetInputCount(inputIndex + 1);
            }
#if STATEMACHINEDEBUG
            var dbg2 = StateMachineDebugSettings.Instance;
            if (dbg2 != null && dbg2.IsAnimationBlendEnabled)
                dbg2.LogAnimationBlend($"[HotPlug] 插入状态Playable到Mixer槽位 {inputIndex}");
#endif

            playableGraph.Connect(statePlayable, 0, layer.mixer, inputIndex);
            layer.mixer.SetInputWeight(inputIndex, 0f);

            layer.stateToSlotMap[state] = inputIndex;
            state.BindLayerSlot(layer, inputIndex);
            layer.InternalOnStateConnected(state, inputIndex);
#if STATEMACHINEDEBUG
            var dbg3 = StateMachineDebugSettings.Instance;
            if (dbg3 != null && dbg3.IsAnimationBlendEnabled)
                dbg3.LogAnimationBlend($"[HotPlug] 状态 {state.strKey} 映射到槽位 {inputIndex}");
#endif

            layer.MarkDirty(PipelineDirtyFlags.HotPlug | PipelineDirtyFlags.MixerWeights);
            TryUpdateMixerWeightsImmediately(layer);
        }

        internal void HotUnplugStateFromPlayable(StateBase state, StateLayerRuntime layer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (layer == null) throw new System.ArgumentNullException(nameof(layer));
#else
            if (state == null || layer == null) return;
#endif

            layer.MarkDirty(PipelineDirtyFlags.MixerWeights);

            if (!layer.stateToSlotMap.TryGetValue(state, out int slotIndex))
                return;

            if (layer.mixer.IsValid())
            {
                var inputPlayable = layer.mixer.GetInput(slotIndex);
                if (inputPlayable.IsValid() && playableGraph.IsValid())
                {
                    playableGraph.Disconnect(layer.mixer, slotIndex);
                }

                layer.mixer.SetInputWeight(slotIndex, 0f);
            }

            layer.stateToSlotMap.Remove(state);
            layer.InternalOnStateDisconnected(state);
            state.ClearLayerSlot();

            if (!(slotIndex == 0 && layer.hasReferencePose))
            {
                layer.freeSlots.Push(slotIndex);
            }

            layer.MarkDirty(PipelineDirtyFlags.HotPlug);
            TryUpdateMixerWeightsImmediately(layer);
            state.DestroyPlayable();
        }

        protected virtual Playable CreateStatePlayable(StateBase state, StateAnimationConfigData animConfig, StateLayerRuntime layer)
        {
            if (state == null) return Playable.Null;

            if (state.CreatePlayable(playableGraph, out Playable output))
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                    dbg.LogAnimationBlend($"状态 {state.strKey} Playable创建成功 | Valid:{output.IsValid()}");
#endif
                return output;
            }
#if STATEMACHINEDEBUG
            var dbg2 = StateMachineDebugSettings.Instance;
            if (dbg2 != null)
                dbg2.LogWarning($"状态 {state.strKey} Playable创建失败");
#endif
            return Playable.Null;
        }

        protected virtual AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
        {
            if (!playableGraph.IsValid() || clip == null) return default;
            return AnimationClipPlayable.Create(playableGraph, clip);
        }

        protected virtual AnimationMixerPlayable CreateMixerPlayable(int inputCount)
        {
            if (!playableGraph.IsValid()) return default;
            return AnimationMixerPlayable.Create(playableGraph, inputCount);
        }
    }
}
