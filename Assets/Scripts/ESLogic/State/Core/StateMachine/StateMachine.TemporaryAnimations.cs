using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        public bool AddTemporaryAnimation(string tempKey, AnimationClip clip, StateLayerType layer = StateLayerType.Main, float speed = 1.0f, bool loopable = false)
        {
            if (string.IsNullOrEmpty(tempKey))
            {
                StateMachineDebugSettings.Instance.LogError("[TempAnim] tempKey is null or empty.");
                return false;
            }

            if (clip == null)
            {
                StateMachineDebugSettings.Instance.LogError("[TempAnim] AnimationClip is null.");
                return false;
            }

            if (_temporaryStates.ContainsKey(tempKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] temp state already exists, remove old first. Key={tempKey}");
                RemoveTemporaryAnimation(tempKey);
            }

            var tempState = StateBase.Pool.GetInPool();
            tempState.strKey = $"__temp_{tempKey}";
            tempState.intKey = -1;

            tempState.stateSharedData = new StateSharedData();
            tempState.stateSharedData.hasAnimation = true;

            tempState.stateSharedData.basicConfig = new StateBasicConfig();
            tempState.stateSharedData.basicConfig.stateName = tempKey;
            tempState.stateSharedData.basicConfig.durationMode = loopable
                ? StateDurationMode.Infinite
                : StateDurationMode.UntilAnimationEnd;
            tempState.stateSharedData.basicConfig.layerType = layer;

            tempState.stateSharedData.animationConfig = new StateAnimationConfigData();
            var calculator = new StateAnimationMixCalculatorForSimpleClip
            {
                clip = clip,
                speed = speed
            };
            tempState.stateSharedData.animationConfig.calculator = calculator;

            tempState.stateSharedData.InitializeRuntime();

            if (!RegisterState(tempState.strKey, tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] register failed. Key={tempKey}");
                return false;
            }

            if (!TryActivateState(tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] activate failed. Key={tempKey}");
                UnregisterState(tempState.strKey);
                return false;
            }

            _temporaryStates[tempKey] = tempState;
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[TempAnim] added. Key={tempKey} | Clip={clip.name} | Layer={layer}");
            return true;
        }

        public bool AddTemporarySkillSequence(string tempKey, ITrackSequence sequence, StateLayerType layer = StateLayerType.Main, bool forceEnter = false)
        {
            return AddTemporarySkillSequence(tempKey, sequence, null, layer, forceEnter);
        }

        public bool AddTemporarySkillSequence(string tempKey, ITrackSequence sequence, StateAniDataInfo baseStateInfo, StateLayerType layer = StateLayerType.Main, bool forceEnter = false)
        {
            if (string.IsNullOrEmpty(tempKey))
            {
                StateMachineDebugSettings.Instance.LogError("[TempSkill] tempKey is null or empty.");
                return false;
            }

            if (sequence == null)
            {
                StateMachineDebugSettings.Instance.LogError("[TempSkill] sequence is null.");
                return false;
            }

            if (_temporaryStates.ContainsKey(tempKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempSkill] temp state already exists, remove old first. Key={tempKey}");
                RemoveTemporaryAnimation(tempKey);
            }

            var tempState = EntityState_Skill.Pool.GetInPool();
            tempState.SetSkillSequence(sequence);
            tempState.stateSharedData = CreateTemporarySkillSharedData(tempKey, sequence, baseStateInfo, layer);
            tempState.stateVariableData = new StateVariableData();
            tempState.stateSharedData.InitializeRuntime();

            string stateKey = "__temp_skill_" + tempKey;
            if (!RegisterState(stateKey, tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempSkill] register failed. Key={tempKey}");
                tempState.TryAutoPushedToPool();
                return false;
            }

            bool activated = forceEnter
                ? ForceEnterState(tempState, layer)
                : TryActivateState(tempState, layer);

            if (!activated)
            {
                StateMachineDebugSettings.Instance.LogError($"[TempSkill] activate failed. Key={tempKey}");
                UnregisterState(stateKey);
                return false;
            }

            _temporaryStates[tempKey] = tempState;
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[TempSkill] added. Key={tempKey} | Sequence={sequence.Name} | Layer={layer}");
            return true;
        }

        private StateSharedData CreateTemporarySkillSharedData(string tempKey, ITrackSequence sequence, StateAniDataInfo baseStateInfo, StateLayerType layer)
        {
            StateSharedData sharedData = null;
            if (baseStateInfo != null)
            {
                baseStateInfo.InitializeRuntime();
                sharedData = CloneTemporarySkillSharedData(baseStateInfo.sharedData);
            }

            if (sharedData == null)
            {
                sharedData = new StateSharedData
                {
                    hasAnimation = false,
                    basicConfig = new StateBasicConfig
                    {
                        ignoreSupportFlag = true,
                        canBeFeedback = false,
                        supportReStart = true
                    }
                };
            }

            if (sharedData.basicConfig == null)
                sharedData.basicConfig = new StateBasicConfig();

            var cache = SkillSequenceRuntimeCache.GetOrBuild(sequence);
            float duration = cache != null ? cache.Duration : 0f;

            var basicConfig = sharedData.basicConfig;
            basicConfig.stateName = tempKey;
            basicConfig.stateId = -1;
            basicConfig.layerType = layer;
            basicConfig.durationMode = StateDurationMode.Timed;
            basicConfig.timedDuration = Mathf.Max(0.001f, duration);
            basicConfig.supportReStart = true;

            // Skill sequence is driven by EntityState_Skill. The base state only provides state machine rules.
            sharedData.hasAnimation = false;
            return sharedData;
        }

        private StateSharedData CloneTemporarySkillSharedData(StateSharedData source)
        {
            if (source == null)
                return null;

            return new StateSharedData
            {
                basicConfig = CloneTemporarySkillBasicConfig(source.basicConfig),
                hasAnimation = false,
                proceduralDriveConfig = source.proceduralDriveConfig,
                tags = source.tags,
                group = source.group,
                displayName = source.displayName,
                description = source.description,
                icon = source.icon,
                mergeData = source.mergeData,
                costData = source.costData,
                canBeTemporary = true,
                autoRemoveWhenDone = true,
                canReplaceAtRuntime = source.canReplaceAtRuntime,
                keepDataOnReplace = source.keepDataOnReplace,
                allowOverride = source.allowOverride,
                notifyOnOverride = source.notifyOnOverride,
                showDebugInfo = source.showDebugInfo,
                debugGizmoColor = source.debugGizmoColor
            };
        }

        private StateBasicConfig CloneTemporarySkillBasicConfig(StateBasicConfig source)
        {
            if (source == null)
                return new StateBasicConfig();

            return new StateBasicConfig
            {
                stateName = source.stateName,
                stateId = source.stateId,
                layerType = source.layerType,
                mixerBias = source.mixerBias,
                stateSupportFlag = source.stateSupportFlag,
                ignoreSupportFlag = source.ignoreSupportFlag,
                disableActiveOnSupportFlagSwitching = source.disableActiveOnSupportFlagSwitching,
                deactivateOnSupportFlagSwitching = source.deactivateOnSupportFlagSwitching,
                supportReStart = source.supportReStart,
                resetSupportFlagOnEnter = source.resetSupportFlagOnEnter,
                removeSupportFlagOnExit = source.removeSupportFlagOnExit,
                canBeFeedback = source.canBeFeedback,
                durationMode = source.durationMode,
                timedDuration = source.timedDuration,
                enableRuntimeProgress = source.enableRuntimeProgress,
                enableClipLengthFallback = source.enableClipLengthFallback,
                internalNote = source.internalNote
            };
        }

        public bool RemoveTemporaryAnimation(string tempKey)
        {
            if (!_temporaryStates.TryGetValue(tempKey, out var tempState))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] temp state not found. Key={tempKey}");
                return false;
            }

            if (tempState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateState(tempState.strKey);
            }

            UnregisterState(tempState.strKey);
            _temporaryStates.Remove(tempKey);
            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] removed. Key={tempKey}");
            return true;
        }

        public void ClearAllTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] no temp states to clear.");
                return;
            }

            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] clear begin. Count={_temporaryStates.Count}");

            var keys = _temporaryKeysCache;
            keys.Clear();
            foreach (var key in _temporaryStates.Keys)
            {
                keys.Add(key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                RemoveTemporaryAnimation(keys[i]);
            }

            _temporaryStates.Clear();
            StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] all temp states cleared.");
        }

        public bool HasTemporaryAnimation(string tempKey)
        {
            return _temporaryStates.ContainsKey(tempKey);
        }

        public int GetTemporaryAnimationCount()
        {
            return _temporaryStates.Count;
        }

        public void BroadcastAnimationEvent(StateBase state, string eventName, string eventParam)
        {
            if (hostEntity != null)
            {
                // Reserved: broadcast through Entity if needed.
                // hostEntity.BroadcastEvent(eventName, eventParam);
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            string stateName = state != null ? state.strKey : "<null>";
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateMachine] animation event. Event={eventName} | State={stateName} | Param={eventParam}");
#endif
#endif
        }
    }
}
