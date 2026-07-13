using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private static readonly NormalMergeRule DefaultNormalMergeRule = new NormalMergeRule();
        private static readonly List<UnconditionalMatchRule> EmptyUnconditionalRules = new List<UnconditionalMatchRule>(0);

        public StateActivationResult TestStateActivation(StateBase targetState, StateLayerType layer = StateLayerType.NotClear, StateBase ignoreInteractionWithState = null)
        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] Begin | State={(targetState != null ? targetState.strKey : "<null>")} | Layer={layer} | Running={isRunning} | DirtyVersion={_dirtyVersion}");
                }
            }
#endif

            if (targetState == null)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning("[TestStateActivation] Fail: targetState is null");
                }
#endif
                return StateActivationResult.FailureStateIsNull;
            }

            var basicConfig = targetState.stateSharedData.basicConfig;
            if (layer == StateLayerType.NotClear)
            {
                layer = basicConfig.layerType;
            }
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] ResolveLayer -> {layer}");
                }
            }
#endif
            if (!basicConfig.ignoreSupportFlag)
            {
                var targetFlag = basicConfig.stateSupportFlag;
                if (targetFlag != StateSupportFlags.None)
                {
                    var supportFlags = currentSupportFlags;
                    if ((supportFlags & targetFlag) == StateSupportFlags.None)
                    {
                        if (basicConfig.disableActiveOnSupportFlagSwitching || IsTransitionDisabledFast(supportFlags, targetFlag))
                        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null)
                            {
                                dbg.LogWarning(
                                    $"[TestStateActivation] Fail: SupportFlags not satisfied | Current={supportFlags} Target={targetFlag} DisableOnSwitch={basicConfig.disableActiveOnSupportFlagSwitching}");
                            }
#endif
                            return StateActivationResult.FailureSupportFlagsNotSatisfied;
                        }
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[TestStateActivation] SupportFlags mismatch but not blocked | Current={supportFlags} Target={targetFlag}");
                            }
                        }
#endif
                    }
                }
            }

            int layerIndex = (int)layer;
            var cache = ignoreInteractionWithState == null ? GetOrCreateActivationCache(targetState) : null;
            if (cache != null && cache.versions[layerIndex] == _dirtyVersion)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition($"[TestStateActivation] Cache hit | LayerIndex={layerIndex}");
                    }
                }
#endif
                return cache.results[layerIndex];
            }

            if (targetState.baseStatus == StateBaseStatus.Running)
            {
                var failure = basicConfig.supportReStart
                    ? StateActivationResult.SuccessRestart
                    : StateActivationResult.FailureStateAlreadyRunning;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition($"[TestStateActivation] State already running | Restart={basicConfig.supportReStart}");
                    }
                }
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }

            var targetLayer = GetLayerByType(layer);
            if (targetLayer == null)
            {
                var failure = StateActivationResult.FailurePipelineNotFound;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"[TestStateActivation] Fail: Layer not found | {layer}");
                }
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }

            if (!targetLayer.isEnabled)
            {
                var failure = StateActivationResult.FailurePipelineDisabled;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"[TestStateActivation] Fail: Layer disabled | {layer}");
                }
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }

            var allRunningStates = GetRunningStatesList();
            if (allRunningStates.Count == 0)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition("[TestStateActivation] No running states -> SuccessNoMerge");
                    }
                }
#endif
                var success = StateActivationResult.SuccessNoMerge;
                if (cache != null)
                {
                    cache.results[layerIndex] = success;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return success;
            }

            int totalMotionCost = 0;
            int totalAgilityCost = 0;
            int totalTargetCost = 0;

            targetState.EnsureResolvedRuntimeConfig();
            var incomingResolved = targetState.ResolvedConfig;
            if (incomingResolved.enableCostCalculation)
            {
                totalMotionCost += incomingResolved.costForMotion;
                totalAgilityCost += incomingResolved.costForAgility;
                totalTargetCost += incomingResolved.costForTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition(
                            $"[TestStateActivation] IncomingCost | M/A/T={incomingResolved.costForMotion}/{incomingResolved.costForAgility}/{incomingResolved.costForTarget} " +
                            $"TotalNow M/A/T={totalMotionCost}/{totalAgilityCost}/{totalTargetCost}");
                    }
                }
#endif
            }

            bool needsInterrupt = false;
            bool canMerge = false;
            var interruptList = cache?.interruptLists[layerIndex] ?? _tmpInterruptStates;
            interruptList.Clear();
#if UNITY_EDITOR
            var mergeList = cache?.mergeLists[layerIndex] ?? _tmpMergeStates;
            mergeList.Clear();
#endif

            foreach (var existingState in allRunningStates)
            {
                if (ignoreInteractionWithState != null && existingState == ignoreInteractionWithState)
                    continue;

#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        string existingStateKey = existingState != null ? existingState.strKey : "<null>";
                        dbg.LogStateTransition($"[TestStateActivation] MergeCheck: {existingStateKey} vs {targetState.strKey}");
                    }
                }
#endif
                var mergeResult = CheckStateMergeCompatibility(existingState, targetState,
                    ref totalMotionCost, ref totalAgilityCost, ref totalTargetCost);

                switch (mergeResult)
                {
                    case StateMergeResult.MergeFail:
                        var failure = StateActivationResult.FailureMergeConflict;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null)
                        {
                            string existingStateKey = existingState != null ? existingState.strKey : "<null>";
                            dbg.LogWarning($"[TestStateActivation] Fail: MergeConflict with {existingStateKey}");
                        }
#endif
                        if (cache != null)
                        {
                            cache.results[layerIndex] = failure;
                            cache.versions[layerIndex] = _dirtyVersion;
                        }
                        return failure;
                    case StateMergeResult.MergeComplete:
                        canMerge = true;
#if UNITY_EDITOR
                        mergeList.Add(existingState);
#endif
                        break;
                    case StateMergeResult.HitAndReplace:
                    case StateMergeResult.TryWeakInterrupt:
                        needsInterrupt = true;
                        interruptList.Add(existingState);
                        break;
                    default:
                        {
                            var failureDefault = StateActivationResult.FailureMergeConflict;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            var dbgDef = StateMachineDebugSettings.Instance;
                            if (dbgDef != null)
                            {
                                string existingStateKey = existingState != null ? existingState.strKey : "<null>";
                                dbgDef.LogWarning($"[TestStateActivation] Fail: Unexpected merge result with {existingStateKey}");
                            }
#endif
                            if (cache != null)
                            {
                                cache.results[layerIndex] = failureDefault;
                                cache.versions[layerIndex] = _dirtyVersion;
                            }
                            return failureDefault;
                        }
                }
            }

            StateActivationCode code = StateActivationCode.Success;
            if (needsInterrupt)
            {
                code |= StateActivationCode.HasInterrupt;
            }
            if (canMerge)
            {
                code |= StateActivationCode.HasMerge;
            }

            var defaultSuccess = new StateActivationResult
            {
                code = code,
                failureReason = string.Empty,
                statesToInterrupt = interruptList,
                interruptCount = interruptList.Count
#if UNITY_EDITOR
                ,
                debugMergeStates = mergeList,
                debugMergeCount = mergeList.Count
#endif
            };
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] Success | Code={code} | Interrupts={interruptList.Count} | Merges={(canMerge ? mergeList.Count : 0)}");
                }
            }
#endif
            if (cache != null)
            {
                cache.results[layerIndex] = defaultSuccess;
                cache.versions[layerIndex] = _dirtyVersion;
            }
            return defaultSuccess;
        }

        public bool TryActivateState(StateBase targetState, StateLayerType layer, StateBase ignoreInteractionWithState)
        {
            if (targetState == null) return false;
            layer = ResolveLayerForState(targetState, layer);
            var result = TestStateActivation(targetState, layer, ignoreInteractionWithState);
            return ExecuteStateActivation(targetState, layer, result);
        }

        public bool TryActivateState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    string targetStateKey = targetState != null ? targetState.strKey : "<null>";
                    dbg.LogStateTransition($"尝试激活状态: {targetStateKey} | Layer: {layer}");
                }
            }
#endif
            if (targetState == null) return false;
            layer = ResolveLayerForState(targetState, layer);
            var result = TestStateActivation(targetState, layer);
            return ExecuteStateActivation(targetState, layer, result);
        }

        public bool TryActivateState(string stateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByString(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {stateKey} 不存在");
                return false;
            }
            return TryActivateState(state, layer);
        }

        public bool TryActivateState(string stateKey, string ignoreInteractionStateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByString(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {stateKey} 不存在");
                return false;
            }
            var ignore = GetStateByString(ignoreInteractionStateKey);
            return TryActivateState(state, layer, ignore);
        }

        public bool TryActivateState(int stateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByInt(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态ID {stateKey} 不存在");
                return false;
            }
            return TryActivateState(state, layer);
        }

        public bool TryActivateState(int stateKey, int ignoreInteractionStateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByInt(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态ID {stateKey} 不存在");
                return false;
            }
            var ignore = GetStateByInt(ignoreInteractionStateKey);
            return TryActivateState(state, layer, ignore);
        }

        public bool ExecuteStateActivation(StateBase targetState, StateLayerType layer, in StateActivationResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (targetState == null) throw new ArgumentNullException(nameof(targetState));
            if (targetState.stateSharedData == null) throw new InvalidOperationException("ExecuteStateActivation: targetState.stateSharedData 不能为空（状态必须先完成注册/初始化）");
            if (targetState.stateSharedData.basicConfig == null) throw new InvalidOperationException("ExecuteStateActivation: targetState.stateSharedData.basicConfig 不能为空（状态必须先完成注册/初始化）");
#else
            if (targetState == null || targetState.stateSharedData == null || targetState.stateSharedData.basicConfig == null) return false;
#endif

            var sharedData = targetState.stateSharedData;
            var basicConfig = sharedData.basicConfig;
            int txId = ++_activationTxIdCounter;
            int runningBefore = runningStates.Count;

            if ((result.code & StateActivationCode.Success) == 0)
            {
                PushActivationEvent(
                    txId, targetState, layer, layer, result.code,
                    ActivationEventKind.Failure, ActivationFailureKind.Validation,
                    result.interruptCount, runningBefore, runningStates.Count, 0, 0);
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"状态激活失败: {result.failureReason}");
                }
#endif
#endif
                return false;
            }

            var layerRuntime = GetLayerByType(layer);
            if (layerRuntime == null)
            {
                PushActivationEvent(
                    txId, targetState, layer, layer, result.code,
                    ActivationEventKind.Failure, ActivationFailureKind.LayerNotFound,
                    result.interruptCount, runningBefore, runningStates.Count, 0, 0);
                StateMachineDebugSettings.Instance.LogError($"获取层级失败: {layer}");
                return false;
            }

            int layerRunningBefore = layerRuntime.runningStates != null ? layerRuntime.runningStates.Count : 0;
            PushActivationEvent(
                txId, targetState, layer, layerRuntime.layerType, result.code,
                ActivationEventKind.Begin, ActivationFailureKind.None,
                result.interruptCount, runningBefore, runningStates.Count,
                layerRunningBefore, layerRunningBefore);

            if ((result.code & StateActivationCode.Restart) == 0 && targetState.baseStatus == StateBaseStatus.Running)
            {
                if (!runningStates.Contains(targetState)) runningStates.Add(targetState);
                if (!layerRuntime.runningStates.Contains(targetState)) layerRuntime.runningStates.Add(targetState);
                return true;
            }

            if ((result.code & StateActivationCode.Restart) != 0 && targetState.baseStatus == StateBaseStatus.Running)
            {
                TruelyDeactivateState(targetState, layer);
            }

            if ((result.code & StateActivationCode.HasInterrupt) != 0)
            {
                var interruptStates = result.statesToInterrupt;
                if (interruptStates != null && result.interruptCount > 0)
                {
                    for (int i = 0; i < interruptStates.Count; i++)
                    {
                        var interruptedState = interruptStates[i];
                        string interruptedLayerName = stateLayerMap.TryGetValue(interruptedState, out var interruptedLayer)
                            ? interruptedLayer.ToString() : "<unknown>";
                        string interruptedStateKey = interruptedState != null ? interruptedState.strKey : "<null>";
                        StateMachineDebug.Log($"[Interrupt] Activating={targetState.strKey} | TargetLayer={layer} | Interrupting={interruptedStateKey} | InterruptStateLayer={interruptedLayerName}");
                        var deactiveLayer = interruptedLayer;
                        if (interruptedState == null || !stateLayerMap.TryGetValue(interruptedState, out deactiveLayer))
                            deactiveLayer = layer;
                        TruelyDeactivateState(interruptedState, deactiveLayer);
                    }
                    PushActivationEvent(
                        txId, targetState, layer, layerRuntime.layerType, result.code,
                        ActivationEventKind.Interrupt, ActivationFailureKind.None,
                        result.interruptCount, runningBefore, runningStates.Count,
                        layerRunningBefore, layerRuntime.runningStates != null ? layerRuntime.runningStates.Count : 0);
                }
            }

            bool entered = false;
            bool addedToRunning = false;
            bool addedToLayerRunning = false;
            bool hotPlugged = false;

            try
            {
                if (basicConfig.resetSupportFlagOnEnter)
                {
                    var enterFlag = basicConfig.stateSupportFlag;
                    if (enterFlag != StateSupportFlags.None)
                    {
                        SetSupportFlags(enterFlag);
                    }
                }

                targetState.OnStateEnter();
                entered = true;

                if (!runningStates.Contains(targetState))
                {
                    runningStates.Add(targetState);
                    addedToRunning = true;
                }

                if (!layerRuntime.runningStates.Contains(targetState))
                {
                    layerRuntime.runningStates.Add(targetState);
                    addedToLayerRunning = true;
                }

                CancelStaleFadeData(targetState, layerRuntime);
                HotPlugStateToPlayable(targetState, layerRuntime);
                hotPlugged = true;
                ApplyFadeIn(targetState, layerRuntime);

                if (targetState.stateSharedData.hasAnimation)
                {
                    targetState.ImmediateUpdateAnimationRuntime(stateContext);
                }

                MarkDirty(StateDirtyReason.Enter);

                PushActivationEvent(
                    txId, targetState, layer, layerRuntime.layerType, result.code,
                    ActivationEventKind.Success, ActivationFailureKind.None,
                    result.interruptCount, runningBefore, runningStates.Count,
                    layerRunningBefore, layerRuntime.runningStates != null ? layerRuntime.runningStates.Count : 0);

                return true;
            }
            catch (Exception ex)
            {
                StateMachineDebugSettings.Instance.LogError($"状态激活异常: {(targetState != null ? targetState.strKey : "<null>")}\n{ex}");

                if (hotPlugged || layerRuntime.stateToSlotMap.ContainsKey(targetState))
                {
                    HotUnplugStateFromPlayable(targetState, layerRuntime);
                }

                if (layerRuntime.fadeInStates.TryGetValue(targetState, out var fadeInData))
                {
                    fadeInData.TryAutoPushedToPool();
                    layerRuntime.fadeInStates.Remove(targetState);
                }
                if (layerRuntime.fadeOutStates.TryGetValue(targetState, out var fadeOutData))
                {
                    fadeOutData.TryAutoPushedToPool();
                    layerRuntime.fadeOutStates.Remove(targetState);
                }

                if (addedToLayerRunning)
                {
                    layerRuntime.runningStates.Remove(targetState);
                }
                if (addedToRunning)
                {
                    runningStates.Remove(targetState);
                }

                if (entered && targetState.baseStatus == StateBaseStatus.Running)
                {
                    try
                    {
                        targetState.OnStateExit();
                    }
                    catch (Exception nested)
                    {
                        StateMachineDebugSettings.Instance.LogError($"状态激活回滚时 OnStateExit 异常: {(targetState != null ? targetState.strKey : "<null>")}\n{nested}");
                    }
                }

                MarkDirty(StateDirtyReason.Exit);

                int layerAfterRollback = layerRuntime.runningStates != null ? layerRuntime.runningStates.Count : 0;
                PushActivationEvent(
                    txId, targetState, layer, layerRuntime.layerType, result.code,
                    ActivationEventKind.Rollback, ActivationFailureKind.Exception,
                    result.interruptCount, runningBefore, runningStates.Count,
                    layerRunningBefore, layerAfterRollback);
                PushActivationEvent(
                    txId, targetState, layer, layerRuntime.layerType, result.code,
                    ActivationEventKind.Failure, ActivationFailureKind.Exception,
                    result.interruptCount, runningBefore, runningStates.Count,
                    layerRunningBefore, layerAfterRollback);
                return false;
            }
        }

        public bool ForceEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            if (targetState == null) return false;

            layer = ResolveLayerForState(targetState, layer);
            var layerData = GetLayerByType(layer);
            if (layerData == null) return false;

            while (layerData.runningStates.Count > 0)
            {
                var state = layerData.runningStates.Items[0];
                TruelyDeactivateState(state, layer);
            }

            targetState.OnStateEnter();
            runningStates.Add(targetState);
            layerData.runningStates.Add(targetState);

            CancelStaleFadeData(targetState, layerData);
            HotPlugStateToPlayable(targetState, layerData);
            ApplyFadeIn(targetState, layerData);

            if (targetState.stateSharedData.hasAnimation)
            {
                targetState.ImmediateUpdateAnimationRuntime(stateContext);
            }

            MarkDirty(StateDirtyReason.Enter);
            return true;
        }

        private void TruelyDeactivateState(StateBase state, StateLayerType layer)
        {
            if (state == null) return;

            StateMachineDebug.Log($"[Deactivate] Begin | State={state.strKey} | RequestedLayer={layer} | BaseStatus={state.baseStatus}");

            var layerRuntime = ResolveConnectedLayerRuntimeForState(state, layer, out layer);
            StateMachineDebug.Log($"[Deactivate] ResolvedLayer | State={state.strKey} | RequestedLayer={layer} | ResolvedRuntime={(layerRuntime != null ? layerRuntime.layerType.ToString() : "<null>")}");

            if (layerRuntime != null)
            {
                if (layerRuntime.fadeInStates != null && layerRuntime.fadeInStates.TryGetValue(state, out var fadeInData))
                {
                    fadeInData.TryAutoPushedToPool();
                    layerRuntime.fadeInStates.Remove(state);
                }

                ApplyFadeOut(state, layerRuntime);

                if (layerRuntime.fadeOutStates != null && layerRuntime.fadeOutStates.ContainsKey(state))
                {
                    TryUpdateMixerWeightsImmediately(layerRuntime);
                }
            }

            if (layerRuntime != null && (!state.stateSharedData.enableFadeInOut || state.stateSharedData.fadeOutDuration <= 0f))
            {
                HotUnplugStateFromPlayable(state, layerRuntime);

                if (layerRuntime.fadeOutStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                    layerRuntime.fadeOutStates.Remove(state);
                }
            }

            state.OnStateExit();

            var stateSharedData = state.stateSharedData;
            var exitBasicConfig = stateSharedData != null ? stateSharedData.basicConfig : null;
            if (exitBasicConfig != null && exitBasicConfig.removeSupportFlagOnExit)
            {
                var exitFlag = exitBasicConfig.stateSupportFlag;
                if (exitFlag != StateSupportFlags.None && currentSupportFlags == exitFlag)
                {
                    SetSupportFlags(StateSupportFlags.None);
                }
            }

            runningStates.Remove(state);

            if (layerRuntime != null)
            {
                layerRuntime.runningStates.Remove(state);
                layerRuntime.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }

            MarkDirty(StateDirtyReason.Exit);

            StateMachineDebug.Log($"[Deactivate] End | State={state.strKey} | FinalLayer={layer} | BaseStatus={state.baseStatus} | RunningCount={runningStates.Count}");

            TryFireExitAutoActivation(state, layer, layerRuntime);
        }

        public bool TryDeactivateState(string stateKey)
        {
            var state = GetStateByString(stateKey);
            if (state == null || state.baseStatus != StateBaseStatus.Running)
            {
                bool foundState = state != null;
                string baseStatusText = foundState ? state.baseStatus.ToString() : "<null>";
                StateMachineDebug.LogWarning($"[Deactivate] Skip | Key={stateKey} | Found={foundState} | BaseStatus={baseStatusText}");
                return false;
            }

            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                TruelyDeactivateState(state, layerType);
                return true;
            }

            return false;
        }

        public bool TryDeactivateState(int stateKey)
        {
            var state = GetStateByInt(stateKey);
            if (state == null || state.baseStatus != StateBaseStatus.Running) return false;

            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                TruelyDeactivateState(state, layerType);
                return true;
            }

            return false;
        }

        public StateActivationResult TestEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            return TestStateActivation(targetState, layer);
        }

        public bool TryEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            return TryActivateState(targetState, layer);
        }

        public StateExitResult TestExitState(StateBase targetState)
        {
            if (targetState == null) return StateExitResult.Failure("目标状态为空", StateLayerType.NotClear);
            if (targetState.baseStatus != StateBaseStatus.Running) return StateExitResult.Failure("状态未在运行中", StateLayerType.NotClear);

            if (!stateLayerMap.TryGetValue(targetState, out var layer))
                layer = StateLayerType.NotClear;
            layer = ResolveLayerForState(targetState, layer);
            return StateExitResult.Success(layer);
        }

        public bool TryExitState(StateBase targetState)
        {
            var result = TestExitState(targetState);
            if (!result.canExit) return false;

            TruelyDeactivateState(targetState, result.layer);
            return true;
        }

        public void ForceExitState(StateBase targetState)
        {
            if (targetState == null) return;

            if (stateLayerMap.TryGetValue(targetState, out var layer))
            {
                TruelyDeactivateState(targetState, layer);
            }
        }

        public void DeactivateLayer(StateLayerType layer)
        {
            var layerData = GetLayerByType(layer);
            if (layerData == null) return;

            while (layerData.runningStates.Count > 0)
            {
                var state = layerData.runningStates.Items[0];
                TruelyDeactivateState(state, layer);
            }
        }

        private StateLayerRuntime ResolveConnectedLayerRuntimeForState(StateBase state, StateLayerType fallbackLayer, out StateLayerType resolvedLayer)
        {
            resolvedLayer = fallbackLayer;

            if (state == null) return GetLayerByType(fallbackLayer);

            if (stateLayerMap.TryGetValue(state, out var mappedLayer))
                resolvedLayer = mappedLayer;
            else
                resolvedLayer = ResolveLayerForState(state, fallbackLayer);

            var layerRuntime = GetLayerByType(resolvedLayer);
            if (layerRuntime != null &&
                (layerRuntime.stateToSlotMap.ContainsKey(state)
                 || layerRuntime.fadeInStates.ContainsKey(state)
                 || layerRuntime.fadeOutStates.ContainsKey(state)
                 || layerRuntime.runningStates.Contains(state)))
            {
                return layerRuntime;
            }

            if (baseLayer != null &&
                (baseLayer.stateToSlotMap.ContainsKey(state) || baseLayer.fadeInStates.ContainsKey(state) || baseLayer.fadeOutStates.ContainsKey(state) || baseLayer.runningStates.Contains(state)))
            {
                resolvedLayer = StateLayerType.Base;
                return baseLayer;
            }
            if (mainLayer != null &&
                (mainLayer.stateToSlotMap.ContainsKey(state) || mainLayer.fadeInStates.ContainsKey(state) || mainLayer.fadeOutStates.ContainsKey(state) || mainLayer.runningStates.Contains(state)))
            {
                resolvedLayer = StateLayerType.Main;
                return mainLayer;
            }
            if (buffLayer != null &&
                (buffLayer.stateToSlotMap.ContainsKey(state) || buffLayer.fadeInStates.ContainsKey(state) || buffLayer.fadeOutStates.ContainsKey(state) || buffLayer.runningStates.Contains(state)))
            {
                resolvedLayer = StateLayerType.Buff;
                return buffLayer;
            }
            if (upperBodyLayer != null &&
                (upperBodyLayer.stateToSlotMap.ContainsKey(state) || upperBodyLayer.fadeInStates.ContainsKey(state) || upperBodyLayer.fadeOutStates.ContainsKey(state) || upperBodyLayer.runningStates.Contains(state)))
            {
                resolvedLayer = StateLayerType.UpperBody;
                return upperBodyLayer;
            }
            if (lowerBodyLayer != null &&
                (lowerBodyLayer.stateToSlotMap.ContainsKey(state) || lowerBodyLayer.fadeInStates.ContainsKey(state) || lowerBodyLayer.fadeOutStates.ContainsKey(state) || lowerBodyLayer.runningStates.Contains(state)))
            {
                resolvedLayer = StateLayerType.LowerBody;
                return lowerBodyLayer;
            }

            return layerRuntime;
        }

        private StateMergeResult CheckStateMergeCompatibility(StateBase existing, StateBase incoming,
            ref int totalMotionCost, ref int totalAgilityCost, ref int totalTargetCost)
        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    string existingStrKey = existing != null ? existing.strKey : "<null>";
                    int existingIntKey = existing != null ? existing.intKey : -1;
                    string incomingStrKey = incoming != null ? incoming.strKey : "<null>";
                    int incomingIntKey = incoming != null ? incoming.intKey : -1;
                    dbg.LogStateTransition(
                        $"[MergeCheck] Begin | Existing={existingStrKey} (ID:{existingIntKey}) " +
                        $"Incoming={incomingStrKey} (ID:{incomingIntKey}) | " +
                        $"CostsBefore: M{totalMotionCost}/A{totalAgilityCost}/T{totalTargetCost}");
                }
            }
#endif
            if (existing == incoming)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null) dbg.LogWarning("[MergeCheck] Fail: existing == incoming");
#endif
                return StateMergeResult.MergeFail;
            }

            existing.EnsureResolvedRuntimeConfig();
            incoming.EnsureResolvedRuntimeConfig();

            var leftResolved = existing.ResolvedConfig;
            var rightResolved = incoming.ResolvedConfig;

            var existingSharedData = existing.stateSharedData;
            var incomingSharedData = incoming.stateSharedData;
            var existingMergeData = existingSharedData != null ? existingSharedData.mergeData : null;
            var incomingMergeData = incomingSharedData != null ? incomingSharedData.mergeData : null;

            NormalMergeRule leftRule = leftResolved.asLeftRule ?? (existingMergeData != null ? existingMergeData.asLeftRule : null) ?? DefaultNormalMergeRule;
            NormalMergeRule rightRule = rightResolved.asRightRule ?? (incomingMergeData != null ? incomingMergeData.asRightRule : null) ?? DefaultNormalMergeRule;
            List<UnconditionalMatchRule> leftUnconditionalRules = leftRule.unconditionalRule ?? EmptyUnconditionalRules;
            List<UnconditionalMatchRule> rightUnconditionalRules = rightRule.unconditionalRule ?? EmptyUnconditionalRules;

#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition(
                        $"[MergeCheck] ChannelMask L={leftResolved.channelMask} R={rightResolved.channelMask} | " +
                        $"StayLevel L={leftResolved.stayLevel} R={rightResolved.stayLevel} | " +
                        $"CostEnabled={leftResolved.enableCostCalculation} " +
                        $"Cost(M/A/T)={leftResolved.costForMotion}/{leftResolved.costForAgility}/{leftResolved.costForTarget}");
                    dbg.LogStateTransition(
                        $"[MergeCheck] LeftRule: Unconditional={leftRule.enableUnconditionalRule} " +
                        $"HitByLayer={leftRule.hitByLayerOption} Priority={leftRule.EffectialPripority} EqualIsEffectial={leftRule.EqualIsEffectial_}");
                    dbg.LogStateTransition(
                        $"[MergeCheck] RightRule: Unconditional={rightRule.enableUnconditionalRule} " +
                        $"HitByLayer={rightRule.hitByLayerOption} Priority={rightRule.EffectialPripority} EqualIsEffectial={rightRule.EqualIsEffectial_}");
                }
            }
#endif

            #region 优先检查无条件规则
            int leftRuleCount = leftUnconditionalRules.Count;
            if (leftRuleCount > 0 && leftRule.enableUnconditionalRule)
            {
                var list = leftUnconditionalRules;
                for (int i = 0; i < leftRuleCount; i++)
                {
                    var item = list[i];
                    if (item == null)
                        continue;

                    if (item.stateName != null && item.stateName.Length > 0 && item.stateName == incoming.strKey)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                            dbg.LogStateTransition($"[MergeCheck] Unconditional(L->R) Hit by Name: {item.stateName} => {item.matchBackType}");
#endif
                        return item.matchBackType;
                    }
                    if (item.stateID >= 0 && incoming.intKey == item.stateID)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                            dbg.LogStateTransition($"[MergeCheck] Unconditional(L->R) Hit by ID: {item.stateID} => {item.matchBackType}");
#endif
                        return item.matchBackType;
                    }
                }
            }

            int rightRuleCount = rightUnconditionalRules.Count;
            if (rightRuleCount > 0 && rightRule.enableUnconditionalRule)
            {
                var list = rightUnconditionalRules;
                for (int i = 0; i < rightRuleCount; i++)
                {
                    var item = list[i];
                    if (item == null)
                        continue;

                    if (item.stateName != null && item.stateName.Length > 0 && item.stateName == existing.strKey)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                            dbg.LogStateTransition($"[MergeCheck] Unconditional(R->L) Hit by Name: {item.stateName} => {item.matchBackType}");
#endif
                        return item.matchBackType;
                    }
                    if (item.stateID >= 0 && existing.intKey == item.stateID)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                            dbg.LogStateTransition($"[MergeCheck] Unconditional(R->L) Hit by ID: {item.stateID} => {item.matchBackType}");
#endif
                        return item.matchBackType;
                    }
                }
            }
            #endregion

            bool onlyInterruptTest = false;
            bool channelOverlap = (leftResolved.channelMask & rightResolved.channelMask) != StateChannelMask.None;

            if (channelOverlap)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] Channel overlap detected");
#endif
                if (!leftResolved.enableCostCalculation)
                {
                    onlyInterruptTest = true;
                }
                else
                {
                    const int costLimit = 100;
                    int nextMotionCost = totalMotionCost + leftResolved.costForMotion;
                    int nextAgilityCost = totalAgilityCost + leftResolved.costForAgility;
                    int nextTargetCost = totalTargetCost + leftResolved.costForTarget;

                    bool overMotion = nextMotionCost > costLimit;
                    bool overAgility = nextAgilityCost > costLimit;
                    bool overTarget = nextTargetCost > costLimit;

                    onlyInterruptTest = overMotion || overAgility || overTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    var dbg2 = StateMachineDebugSettings.Instance;
                    if (dbg2 != null && dbg2.IsStateTransitionEnabled)
                        dbg2.LogStateTransition(
                            $"[MergeCheck] CostCalc | Limit={costLimit} " +
                            $"Next(M/A/T)={nextMotionCost}/{nextAgilityCost}/{nextTargetCost} " +
                            $"Over(M/A/T)={overMotion}/{overAgility}/{overTarget} " +
                            $"OnlyInterrupt={onlyInterruptTest}");
#endif
                }

                if (onlyInterruptTest)
                {
                    string existingKey = existing != null ? existing.strKey : "<null>";
                    string incomingKey = incoming != null ? incoming.strKey : "<null>";
                    string interruptReason = $"[MergeCheckReason] Existing={existingKey} Incoming={incomingKey} | ChannelOverlap={channelOverlap} | CostTotals={totalMotionCost}/{totalAgilityCost}/{totalTargetCost} | ExistingStay={leftResolved.stayLevel} IncomingStay={rightResolved.stayLevel} | ExistingPriority={leftRule.EffectialPripority} IncomingPriority={rightRule.EffectialPripority} | ExistingEqualEffective={leftRule.EqualIsEffectial_} IncomingEqualEffective={rightRule.EqualIsEffectial_} | ExistingHitBy={leftRule.hitByLayerOption} IncomingHitBy={rightRule.hitByLayerOption}";

                    if (rightRule.hitByLayerOption == StateHitByLayerOption.Never)
                    {
                        StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=IncomingNeverHits");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null) dbg.LogWarning("[MergeCheck] Fail: Right hitByLayer=Never");
#endif
                        return StateMergeResult.MergeFail;
                    }
                    if (leftRule.hitByLayerOption == StateHitByLayerOption.Never)
                    {
                        StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=ExistingNeverBeHit");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null) dbg.LogWarning("[MergeCheck] Fail: Left hitByLayer=Never");
#endif
                        return StateMergeResult.MergeFail;
                    }

                    var levelOverlap = leftResolved.stayLevel & rightResolved.stayLevel;
                    if (levelOverlap == StateStayLevel.Rubbish)
                    {
                        if (leftRule.hitByLayerOption == StateHitByLayerOption.SameLevelTest
                            && rightRule.hitByLayerOption == StateHitByLayerOption.SameLevelTest)
                        {
                            StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=SameLevelTestButNoStayOverlap");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null) dbg.LogWarning("[MergeCheck] Fail: SameLevelTest + Rubbish overlap");
#endif
                            return StateMergeResult.MergeFail;
                        }
                        else if (rightResolved.stayLevel > leftResolved.stayLevel)
                        {
                            StateMachineDebug.Log($"{interruptReason} | Decision=HitAndReplace | Reason=IncomingStayLevelHigher");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] HitAndReplace: Right stayLevel higher");
#endif
                            return StateMergeResult.HitAndReplace;
                        }
                    }

                    byte rightPriority = rightRule.EffectialPripority;
                    byte leftPriority = leftRule.EffectialPripority;

                    if (rightRule.EqualIsEffectial_ && leftRule.EqualIsEffectial_)
                    {
                        if (rightPriority < leftPriority)
                        {
                            StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=IncomingPriorityLowerEqualAllowed");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] Fail: Right priority lower (EqualIsEffectial)");
#endif
                            return StateMergeResult.MergeFail;
                        }
                        StateMachineDebug.Log($"{interruptReason} | Decision=HitAndReplace | Reason=PriorityEqualAndEqualIsEffective");
                        return StateMergeResult.HitAndReplace;
                    }
                    else if (rightPriority < leftPriority)
                    {
                        StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=IncomingPriorityLower");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] Fail: Right priority lower");
#endif
                        return StateMergeResult.MergeFail;
                    }
                    else if (rightPriority > leftPriority)
                    {
                        StateMachineDebug.Log($"{interruptReason} | Decision=HitAndReplace | Reason=IncomingPriorityHigher");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] HitAndReplace: Right priority higher");
#endif
                        return StateMergeResult.HitAndReplace;
                    }

                    StateMachineDebug.Log($"{interruptReason} | Decision=MergeFail | Reason=NoInterruptDirection");
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    var dbgFinal = StateMachineDebugSettings.Instance;
                    if (dbgFinal != null) dbgFinal.LogWarning("[MergeCheck] Fail: Unable to decide interrupt direction");
#endif
                    return StateMergeResult.MergeFail;
                }

                if (leftResolved.enableCostCalculation)
                {
                    totalMotionCost += leftResolved.costForMotion;
                    totalAgilityCost += leftResolved.costForAgility;
                    totalTargetCost += leftResolved.costForTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                        dbg.LogStateTransition(
                            $"[MergeCheck] MergeComplete by cost | CostsAfter M{totalMotionCost}/A{totalAgilityCost}/T{totalTargetCost}");
#endif
                }
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg2 = StateMachineDebugSettings.Instance;
                if (dbg2 != null && dbg2.IsStateTransitionEnabled) dbg2.LogStateTransition("[MergeCheck] MergeComplete (channel overlap allowed)");
#endif
                return StateMergeResult.MergeComplete;
            }
            else
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled) dbg.LogStateTransition("[MergeCheck] MergeComplete (no channel overlap)");
#endif
                return StateMergeResult.MergeComplete;
            }
        }

        private StateMergeResult? ResolveUnconditionalRule(StateBasicConfig selfBasic, NormalMergeRule rule, StateBase other)
        {
            if (rule == null || !rule.enableUnconditionalRule || rule.unconditionalRule == null || other == null)
                return null;

            foreach (var item in rule.unconditionalRule)
            {
                if (item == null) continue;

                bool nameMatch = !string.IsNullOrEmpty(item.stateName) && item.stateName == other.strKey;
                bool idMatch = item.stateID >= 0 && other.intKey == item.stateID;

                if (!nameMatch && !idMatch) continue;

                switch (item.matchBackType)
                {
                    case StateMergeResult.MergeComplete: return StateMergeResult.MergeComplete;
                    case StateMergeResult.MergeFail: return StateMergeResult.MergeFail;
                    case StateMergeResult.HitAndReplace: return StateMergeResult.HitAndReplace;
                }
            }

            return null;
        }

        private static float GetStayLevelValue(StateStayLevel level)
        {
            return (float)level;
        }
    }
}
