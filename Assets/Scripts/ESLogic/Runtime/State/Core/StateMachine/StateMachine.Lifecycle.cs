using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    public partial class StateMachine
    {
        public void Initialize(Entity entity, PlayableGraph graph = default, AnimationLayerMixerPlayable root = default)
        {
            if (this.isInitialized) return;

            hostEntity = entity;

            stateContext = new StateMachineContext();
            stateContext.contextID = Guid.NewGuid().ToString();
            stateContext.creationTime = Time.time;
            stateContext.lastUpdateTime = Time.time;

            InitializePipelines(graph, root);
            InitializeSupportFlagsTransitionCache();

            foreach (var kvp in stringToStateMap)
            {
                InitializeState(kvp.Value);
            }

            baseLayer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            mainLayer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            buffLayer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            upperBodyLayer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            lowerBodyLayer.MarkDirty(PipelineDirtyFlags.FallbackCheck);

            this.isInitialized = true;
        }

        public void Initialize(Entity entity, Animator animator, PlayableGraph graph = default, AnimationLayerMixerPlayable root = default)
        {
            if (this.isInitialized)
            {
                if (boundAnimator == null && animator != null)
                {
                    BindToAnimator(animator);
                }
                return;
            }

            Initialize(entity, graph, root);
            BindToAnimator(animator);
            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
                playableGraph.Play();
            }
        }

        private void InitializePipelines(PlayableGraph hanldegraph, AnimationLayerMixerPlayable root)
        {
            if (hanldegraph.IsValid())
            {
                playableGraph = hanldegraph;
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = false;
            }
            else
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = true;
            }

            int layerCount = (int)StateLayerType.Count;

            if (playableGraph.IsValid())
            {
                if (root.IsValid())
                {
                    rootMixer = root;
                    if (rootMixer.GetInputCount() < layerCount)
                    {
                        rootMixer.SetInputCount(layerCount);
                    }
                }
                else
                {
                    rootMixer = AnimationLayerMixerPlayable.Create(playableGraph, layerCount);
                }
            }

            InitializeAllLayers();
            InitializeLayerWeights();
        }

        private void InitializeSupportFlagsTransitionCache()
        {
            if (config == null)
            {
                config = StateMachineConfig.Instance;
            }

            var map = config != null ? config.disableTransitionPermissionMap : null;
            if (map == null)
            {
                _disableTransitionMasks = null;
                return;
            }

            if (_disableTransitionMasks == null)
            {
                _disableTransitionMasks = new Dictionary<StateSupportFlags, uint>(8);
            }
            else
            {
                _disableTransitionMasks.Clear();
            }

            var relations = map.Relations;
            if (relations == null)
            {
                return;
            }

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                var fromFlag = entry.key;
                if (fromFlag == StateSupportFlags.None) continue;

                uint mask = 0u;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        var relatedFlag = related[r];
                        if (relatedFlag == StateSupportFlags.None) continue;
                        mask |= (uint)relatedFlag;
                    }
                }

                _disableTransitionMasks[fromFlag] = mask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTransitionDisabledFast(StateSupportFlags fromFlag, StateSupportFlags toFlag)
        {
            if (fromFlag == StateSupportFlags.None || toFlag == StateSupportFlags.None) return false;
            return _disableTransitionMasks.TryGetValue(fromFlag, out var mask)
                && (mask & (uint)toFlag) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static StateSupportFlags NormalizeSingleSupportFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;
            uint value = (ushort)flag;
            uint lowest = value & (~value + 1u);
            return (StateSupportFlags)(ushort)lowest;
        }

        public void InitializeState(StateBase state)
        {
            state.host = this;
            state.BindHostMachine(this);
        }

        public void Dispose()
        {
            if (isRunning)
            {
                StopStateMachine();
            }

            if (boundAnimator != null)
            {
                var driver = boundAnimator.GetComponent<StateFinalIKDriver>();
                if (driver != null)
                {
                    driver.Unbind();
                }
            }

            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;
                state.OnStateExit();
                state.DestroyPlayable();
            }

            runningStates.Clear();
            ClearAllWeakInterruptions();

            if (_temporaryStates.Count > 0)
            {
                foreach (var kvp in _temporaryStates)
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.OnStateExit();
                        kvp.Value.TryAutoPushedToPool();
                    }
                }
                _temporaryStates.Clear();
            }
            // 清理层级
            if (_layerArray != null)
            {
                for (int i = 0; i < _layerArray.Length; i++)
                {
                    DisposeLayer(_layerArray[i]);
                }
            }
            baseLayer = null;
            mainLayer = null;
            buffLayer = null;
            upperBodyLayer = null;
            lowerBodyLayer = null;

            if (ownsPlayableGraph && playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            animationOutput = default;
            boundAnimator = null;

            ClearAllExitAutoActivations();

            stringToStateMap.Clear();
            intToStateMap.Clear();
            transitionCache.Clear();
            stateLayerMap.Clear();
            _registeredStatesList.Clear();
            _activationCache.Clear();

            if (stateContext != null)
            {
                stateContext.Clear();
            }

            isRunning = false;
            isInitialized = false;
        }

        public void StartStateMachine()
        {
            if (isRunning) return;
            if (!isInitialized)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"状态机 {stateMachineKey} 未初始化，无法启动");
                }
#endif
                return;
            }

            if (boundAnimator == null)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"状态机 {stateMachineKey} 未绑定Animator，无法启动（请先调用 BindToAnimator）");
                }
#endif
                return;
            }

            isRunning = true;

            if (playableGraph.IsValid())
            {
                playableGraph.Play();
            }

            if (!string.IsNullOrEmpty(defaultStateKey))
            {
                TryActivateState(defaultStateKey, StateLayerType.NotClear);
            }
        }

        public void StopStateMachine()
        {
            if (!isRunning) return;

            var layers = _layerArray;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                DeactivateLayer(layer.layerType);
                ForceClearLayerFadesAndResidualPlayables(layer);
            }

            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
            }

            ClearAllExitAutoActivations();

            isRunning = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveExitActivationBindingsRelatedToState(StateBase state)
        {
            if (state == null) return;
            state.ClearAllExitAutoActivations();
            var all = _registeredStatesList;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || ReferenceEquals(s, state)) continue;
                s.ClearActivateOnExitIfReferences(state);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearAllExitAutoActivations()
        {
            var all = _registeredStatesList;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    all[i].ClearAllExitAutoActivations();
                }
            }
        }

        private void ForceClearLayerFadesAndResidualPlayables(StateLayerRuntime layer)
        {
            if (layer == null) return;
            ClearAllWeakInterruptions();

            if (layer.fadeInStates.Count > 0)
            {
                foreach (var kvp in layer.fadeInStates)
                {
                    if (kvp.Value != null) kvp.Value.TryAutoPushedToPool();
                }
                layer.fadeInStates.Clear();
            }

            if (layer.fadeOutStates.Count > 0)
            {
                foreach (var kvp in layer.fadeOutStates)
                {
                    if (kvp.Value != null) kvp.Value.TryAutoPushedToPool();
                }
                layer.fadeOutStates.Clear();
                _fadeOutIKStates.Clear();
            }

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

        public void UpdateStateMachine()
        {
            if (!isRunning) return;

            if (stateContext == null)
            {
#if STATEMACHINEDEBUG
        var dbg = StateMachineDebugSettings.Instance;
        if (dbg != null)
        {
            dbg.LogWarning($"UpdateStateMachine 被调用但 stateContext 为 null | StateMachineKey={stateMachineKey}");
        }
#endif
                return;
            }

            float deltaTime = Time.deltaTime;
            stateContext.lastUpdateTime = Time.time;

            // 更新所有运行中的状态
            var statesToDeactivate = _statesToDeactivateCache;
            statesToDeactivate.Clear();
            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;
                if (state.baseStatus != StateBaseStatus.Running) continue;

                state.OnStateUpdate();
                state.hasEnterTime += deltaTime;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (state.stateSharedData == null) throw new InvalidOperationException("RunningState 的 stateSharedData 不能为空（应在注册/初始化期保证）");
#endif
                if (state._hasAnimationCached)
                {
                    state.UpdateAnimationRuntime(stateContext, deltaTime);
                    state.ProcessMatchTarget(boundAnimator);
                }

                if (state.ShouldAutoExit(Time.time))
                {
#if STATEMACHINEDEBUG
                    string autoExitLayerName = stateLayerMap.TryGetValue(state, out var autoExitLayer)
                        ? autoExitLayer.ToString()
                        : "<unknown>";
                    StateMachineDebug.Log($"[AutoExit] State={state.strKey} | Layer={autoExitLayerName} | ActivationTime={state.activationTime:F3} | Now={Time.time:F3}");
#endif
                    statesToDeactivate.Add(state);
                }
            }

            // 自动退出已完成的状态
            foreach (var state in statesToDeactivate)
            {
                if (stateLayerMap.TryGetValue(state, out var layerType))
                {
                    TruelyDeactivateState(state, layerType);
                }
            }

            // ★ 统一遍历所有层级
            var layers = _layerArray;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                // 更新淡入淡出效果
                UpdateLayerFades(layer, deltaTime);
                // Dirty 自动衰减
                layer.UpdateDirtyDecay();
                // 处理 Dirty 任务（FallBack 检查等）
                ProcessDirtyTasks(layer, layer.layerType);
                // 同步层级权重到 RootMixer
                UpdateSingleLayerWeight(layer);
            }

            // ★ 统一批处理写入 Mixer 输入权重
            UpdateMixerInputWeights();

            // IK 姿态聚合
            UpdateStateGeneralFinalIKDriverPoseCache(deltaTime);

            // 手动推进 PlayableGraph
            if (playableGraph.IsValid())
            {
                if (!playableGraph.IsPlaying())
                {
                    playableGraph.Play();
                }
#if STATEMACHINEDEBUG
        var dbg = StateMachineDebugSettings.Instance;
        if (dbg != null && dbg.IsPerformanceEnabled)
        {
            dbg.LogPerformance(
                $"[StateMachine] 手动评估PlayableGraph，DeltaTime: {deltaTime:F4}" +
                playableGraph.GetTimeUpdateMode() +
                playableGraph.IsPlaying() +
                playableGraph.IsValid());
        }
#endif
                playableGraph.Evaluate(deltaTime);
            }



#if UNITY_EDITOR
#if STATEMACHINEDEBUG
    if (enableContinuousStats)
    {
        var dbg = StateMachineDebugSettings.Instance;
        if (dbg == null || !dbg.IsStressTestSilentMode)
        {
            OutputContinuousStats();
        }
    }
#endif
#endif
        }

        private void UpdateSingleLayerWeight(StateLayerRuntime layer)
        {
            if (!rootMixer.IsValid()) return;
            int index = layer.rootInputIndex;
            if (index < 0) return;
            if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
            {
                rootMixer.SetInputWeight(index, layer.weight);
                layer.lastAppliedRootMixerWeight = layer.weight;
            }
        }
        public StateExitActivationHandle BindActivateOnStateExit(StateBase fromState, StateBase toState, StateExitActivationOptions options = default)
        {
            if (fromState == null || toState == null) return default;

            if (options.oneShot)
            {
                fromState.SetActivateOnExitOneShot(
                    toState,
                    options.toLayer,
                    options.forceEnter,
                    options.fallbackToForceEnterOnFail,
                    options.suppressFromFadeOutOverlap,
                    options.ignoreInteractionWithState,
                    options.condition);
            }
            else
            {
                fromState.SetActivateOnExitPersistent(
                    toState,
                    options.toLayer,
                    options.forceEnter,
                    options.fallbackToForceEnterOnFail,
                    options.suppressFromFadeOutOverlap,
                    options.ignoreInteractionWithState,
                    options.condition);
            }
            return new StateExitActivationHandle(fromState, options.oneShot);
        }

        public StateExitActivationHandle BindActivateOnStateExit(string fromStateKey, string toStateKey, StateExitActivationOptions options = default)
        {
            return BindActivateOnStateExit(GetStateByString(fromStateKey), GetStateByString(toStateKey), options);
        }

        public StateExitActivationHandle BindActivateOnStateExit(int fromStateKey, int toStateKey, StateExitActivationOptions options = default)
        {
            return BindActivateOnStateExit(GetStateByInt(fromStateKey), GetStateByInt(toStateKey), options);
        }

        public bool UnbindActivateOnStateExit(StateExitActivationHandle handle)
        {
            if (!handle.IsValid) return false;

            if (handle.oneShot)
            {
                handle.fromState.ClearActivateOnExitOneShot();
                return true;
            }

            handle.fromState.ClearActivateOnExitPersistent();
            return true;
        }

        public int UnbindActivateOnStateExit(StateBase fromState, StateBase toState)
        {
            if (fromState == null || toState == null) return 0;
            return fromState.ClearActivateOnExitIfToState(toState);
        }

        private void TryFireExitAutoActivation(StateBase fromState, StateLayerType fromLayer, StateLayerRuntime fromLayerRuntime)
        {
            if (fromState == null) return;

            bool usedOneShot = false;
            if (fromState.ConsumeExitAutoActivationOneShot(out var binding))
            {
                usedOneShot = true;
            }
            else
            {
                if (!fromState.TryGetExitAutoActivationPersistent(out binding)) return;
            }

            if (binding.condition != null)
            {
                var ctx = stateContext;
                bool pass = false;
                try
                {
                    pass = ctx != null && binding.condition(ctx);
                }
                catch (Exception e)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null)
                    {
                        string fromStateKey = fromState != null ? fromState.strKey : "<null>";
                        dbg.LogError($"[ExitBinding] condition执行异常: From={fromStateKey} | {e}");
                    }
#endif
                    pass = false;
                }

                if (!pass)
                {
                    if (usedOneShot)
                    {
                        if (!fromState.TryGetExitAutoActivationPersistent(out binding)) return;
                        if (binding.condition != null)
                        {
                            try
                            {
                                pass = ctx != null && binding.condition(ctx);
                            }
                            catch
                            {
                                pass = false;
                            }
                            if (!pass) return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            if (binding.suppressFromFadeOutOverlap && fromLayerRuntime != null)
            {
                CancelStaleFadeData(fromState, fromLayerRuntime);
                fromLayerRuntime.MarkDirty(PipelineDirtyFlags.MixerWeights);
            }

            var toState = binding.toState;
            if (toState == null) return;

            var toLayer = ResolveLayerForState(toState, binding.toLayer);

            bool activated;
            if (binding.forceEnter)
            {
                activated = ForceEnterState(toState, toLayer);
            }
            else
            {
                activated = binding.ignoreInteractionWithState != null
                    ? TryActivateState(toState, toLayer, binding.ignoreInteractionWithState)
                    : TryActivateState(toState, toLayer);
            }

            if (!activated && binding.fallbackToForceEnterOnFail)
            {
                activated = ForceEnterState(toState, toLayer);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!activated)
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"[ExitBinding] 退出激活失败 | From={fromState.strKey} To={toState.strKey} Force={binding.forceEnter} FallbackForce={binding.fallbackToForceEnterOnFail}");
                }
            }
#endif
        }


        private void DisposeLayer(StateLayerRuntime layer)
        {
            if (layer == null) return;
            foreach (var state in layer.runningStates) state.DestroyPlayable();
            layer.runningStates.Clear();
            layer.stateToSlotMap.Clear();
            layer.InternalClearConnectedStates();
            layer.freeSlots.Clear();
            CleanupFadeDict(layer.fadeOutStates);
            _fadeOutIKStates.Clear();
            CleanupFadeDict(layer.fadeInStates);
            if (layer.mixer.IsValid()) layer.mixer.Destroy();
        }

        private void CleanupFadeDict(Dictionary<StateBase, StateFadeData> dict)
        {
            if (dict == null) return;
            foreach (var kvp in dict) kvp.Value?.TryAutoPushedToPool();
            dict.Clear();
        }

    }
}
