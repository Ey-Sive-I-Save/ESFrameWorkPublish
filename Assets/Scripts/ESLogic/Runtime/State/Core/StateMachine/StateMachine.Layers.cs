using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        private void InitializeAllLayers()
        {
            baseLayer = InitializeSingleLayer(StateLayerType.Base);
            mainLayer = InitializeSingleLayer(StateLayerType.Main);
            buffLayer = InitializeSingleLayer(StateLayerType.Buff);
            upperBodyLayer = InitializeSingleLayer(StateLayerType.UpperBody);
            lowerBodyLayer = InitializeSingleLayer(StateLayerType.LowerBody);

            _layerArray = new StateLayerRuntime[] { baseLayer, mainLayer, buffLayer, upperBodyLayer, lowerBodyLayer }; 

        }

        private StateLayerRuntime InitializeSingleLayer(StateLayerType layerType)
        {
#if STATEMACHINEDEBUG
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsRuntimeInitEnabled)
            {
                dbg.LogRuntimeInit($"开始初始化层级: {layerType}");
            }
#endif
            var layer = new StateLayerRuntime(layerType, this);

            layer.avatarMask = ResolveLayerMask(layerType);
            layer.blendMode = StateLayerBlendMode.Override;
            layer.weight = GetDefaultLayerWeight(layerType);
            layer.allowStateMaskOverride = false;

            if (playableGraph.IsValid())
            {
                bool useReferencePose = (this.referencePoseClip != null && layerType == StateLayerType.Base);
                int initialSlotCount = useReferencePose ? 1 : 0;
                layer.mixer = AnimationMixerPlayable.Create(playableGraph, initialSlotCount);
                layer.rootInputIndex = (int)layerType;
                playableGraph.Connect(layer.mixer, 0, rootMixer, layer.rootInputIndex);
                rootMixer.SetInputWeight(layer.rootInputIndex, layer.weight);
                layer.lastAppliedRootMixerWeight = layer.weight;

                if (useReferencePose)
                {
                    layer.referencePosePlayable = AnimationClipPlayable.Create(playableGraph, this.referencePoseClip);
                    layer.referencePosePlayable.SetSpeed(0);
                    layer.referencePosePlayable.SetTime(0);
                    playableGraph.Connect(layer.referencePosePlayable, 0, layer.mixer, 0);
                    layer.mixer.SetInputWeight(0, 1f);
                    layer.hasReferencePose = true;
#if STATEMACHINEDEBUG
                    var dbg2 = StateMachineDebugSettings.Instance;
                    if (dbg2 != null && dbg2.IsRuntimeInitEnabled)
                    {
                        dbg2.LogRuntimeInit($"✓ Base层参考姿态已创建 | Clip={referencePoseClip.name}");
                    }
#endif
                }

                if (layer.avatarMask != null)
                {
                    rootMixer.SetLayerMaskFromAvatarMask((uint)layer.rootInputIndex, layer.avatarMask);
                }

                rootMixer.SetLayerAdditive((uint)layer.rootInputIndex, layer.blendMode == StateLayerBlendMode.Additive);

#if STATEMACHINEDEBUG
                var dbg3 = StateMachineDebugSettings.Instance;
                if (dbg3 != null && dbg3.IsRuntimeInitEnabled)
                {
                    dbg3.LogRuntimeInit($"✓ {layerType}层级Mixer创建成功 | Valid:{layer.mixer.IsValid()} | RootIndex:{layer.rootInputIndex}");
                }
#endif
            }
            else
            {
#if STATEMACHINEDEBUG
                var dbg4 = StateMachineDebugSettings.Instance;
                if (dbg4 != null)
                {
                    dbg4.LogWarning($"✗ {layerType}层级Mixer创建失败 - PlayableGraph无效");
                }
#endif
            }

            return layer;
        }

        private static float GetDefaultLayerWeight(StateLayerType layerType)
        {
            switch (layerType)
            {
                case StateLayerType.Base:
                case StateLayerType.Main:
                case StateLayerType.UpperBody:
                case StateLayerType.LowerBody:
                    return 1f;
                case StateLayerType.Buff:
                    return 0.5f;
                default:
                    return 0f;
            }
        }

        private AvatarMask ResolveLayerMask(StateLayerType layerType)
        {
            var globalConfig = config != null ? config : StateMachineConfig.Instance;

            switch (layerType)
            {
                case StateLayerType.UpperBody:
                    return this.upperBodyMask != null ? this.upperBodyMask : globalConfig != null ? globalConfig.upperBodyMask : null;
                case StateLayerType.LowerBody:
                    return this.lowerBodyMask != null ? this.lowerBodyMask : globalConfig != null ? globalConfig.lowerBodyMask : null;
                default:
                    return null;
            }
        }

        private void UpdateLayerWeights()
        {
            if (!rootMixer.IsValid()) return;

            var mixer = rootMixer;
            {
                var layer = baseLayer;
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
            {
                var layer = mainLayer;
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
            {
                var layer = buffLayer;
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
            {
                var layer = upperBodyLayer;
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
            {
                var layer = lowerBodyLayer;
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
        }

        private void InitializeLayerWeights()
        {
            UpdateLayerWeights();
        }

        public void SetLayerWeight(StateLayerType layerType, float weight)
        {
            var layer = GetLayerByType(layerType);
            if (layer != null)
            {
                layer.weight = Mathf.Clamp01(weight);
                layer.UpdateLayerMixer();
            }
        }

        public StateLayerRuntime GetLayer(StateLayerType layerType)
        {
            return GetLayerByType(layerType);
        }
    }
}