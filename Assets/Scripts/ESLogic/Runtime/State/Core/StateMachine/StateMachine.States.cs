using System;
using System.Collections.Generic;

namespace ES
{
    public partial class StateMachine
    {
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            return RegisterStateFromInfo(info, null, allowOverride);
        }

        public StateBase RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, bool allowOverride = false)
        {
            return RegisterStateFromInfo(info, customStringKey, null, allowOverride);
        }

        public StateBase RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, int? customIntKey, bool allowOverride = false)
        {
            if (info == null)
            {
                StateMachineDebugSettings.Instance.LogError("注册状态失败: StateAniDataInfo为空");
                return null;
            }

            StateBase state = null;
            try
            {
                info.InitializeRuntime();
                StateMachineDebugSettings.Instance.LogRuntimeInit($"✓ Info初始化完成: {info.sharedData.basicConfig.stateName}");

                state = CreateStateFromInfo(info);
                string finalStringKey = customStringKey ?? info.sharedData.basicConfig.stateName;
                int finalIntKey = customIntKey ?? info.sharedData.basicConfig.stateId;

                var layerType = info.sharedData.basicConfig.layerType;

                bool registered;
                if (customStringKey != null || customIntKey.HasValue)
                {
                    registered = RegisterStateCore(finalStringKey, finalIntKey, state, layerType);
                    if (!registered && !allowOverride)
                    {
                        registered = RegisterState(state, layerType, allowOverride);
                    }
                }
                else
                {
                    registered = RegisterState(state, layerType, allowOverride);
                }

                if (registered)
                {
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"✓ 注册状态: [{layerType}] {state.strKey} (ID:{state.intKey})");
                    return state;
                }
                else
                {
                    StateMachineDebugSettings.Instance.LogWarning($"注册状态失败: {info.sharedData.basicConfig.stateName}");
                    state.TryAutoPushedToPool();
                }
                return null;
            }
            catch (Exception e)
            {
                string stateName = info.sharedData != null && info.sharedData.basicConfig != null
                    ? info.sharedData.basicConfig.stateName
                    : "<unknown>";
                StateMachineDebugSettings.Instance.LogError($"注册状态异常: {stateName}\n{e}");
                state?.TryAutoPushedToPool();
                return null;
            }
        }

        private StateBase CreateStateFromInfo(StateAniDataInfo info)
        {
            var state = StateBase.Pool.GetInPool();
            state.stateSharedData = info.sharedData;
            state.stateVariableData = new StateVariableData();
            return state;
        }

        private bool RegisterState(StateBase state, StateLayerType layer, bool allowOverride = false)
        {
            var config = state.stateSharedData.basicConfig;
            string originalName = string.IsNullOrEmpty(config.stateName) ? "AutoState" : config.stateName;
            int originalId = config.stateId;

            string finalName = originalName;
            if (!allowOverride)
            {
                int attempt = 0;
                while (stringToStateMap.ContainsKey(finalName))
                {
                    finalName = $"{originalName}_r{++attempt}";
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"⚠️ String键冲突: '{originalName}' → '{finalName}'");
                }
            }

            if (!allowOverride && originalId > 0 && intToStateMap.ContainsKey(originalId))
            {
                config.stateId = -1;
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ Int键冲突! ID={originalId} 已占用，自动重新分配");
            }

            int finalId = GenerateUniqueIntKey(state);

            return RegisterStateCore(finalName, finalId, state, layer);
        }

        public bool RegisterState(string stateKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                StateMachineDebugSettings.Instance.LogError("状态键不能为空");
                return false;
            }
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError("状态实例不能为空");
                return false;
            }

            layer = ResolveLayerForState(state, layer);

            string finalStateKey = stateKey;
            int renameAttempt = 0;
            while (stringToStateMap.ContainsKey(finalStateKey))
            {
                renameAttempt++;
                finalStateKey = $"{stateKey}_r{renameAttempt}";
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ String键冲突: '{stateKey}'已存在，自动重命名为'{finalStateKey}'");
            }

            int autoIntKey = GenerateUniqueIntKey(state);

            return RegisterStateCore(finalStateKey, autoIntKey, state, layer);
        }

        public bool RegisterState(int stateKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }

            layer = ResolveLayerForState(state, layer);

            string autoStrKey = GenerateUniqueStringKey(state);

            return RegisterStateCore(autoStrKey, stateKey, state, layer);
        }

        public bool RegisterState(string stringKey, int intKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                StateMachineDebugSettings.Instance.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError($"状态实例不能为空: {stringKey}");
                return false;
            }

            layer = ResolveLayerForState(state, layer);

            return RegisterStateCore(stringKey, intKey, state, layer);
        }

        public bool RegisterStateFromSharedData(StateSharedData sharedData, string customStringKey = null, int? customIntKey = null, bool allowOverride = false)
        {
            if (sharedData == null)
            {
                StateMachineDebugSettings.Instance.LogError("StateSharedData为空");
                return false;
            }

            if (!sharedData.IsRuntimeInitialized)
            {
                sharedData.InitializeRuntime();
            }

            var state = StateBase.Pool.GetInPool();
            state.stateSharedData = sharedData;
            state.stateVariableData = new StateVariableData();

            try
            {
                string finalStringKey = customStringKey ?? sharedData.basicConfig.stateName;
                int finalIntKey = customIntKey ?? sharedData.basicConfig.stateId;
                var layerType = sharedData.basicConfig.layerType;

                bool registered;
                if (customStringKey != null || customIntKey.HasValue)
                {
                    registered = RegisterStateCore(finalStringKey, finalIntKey, state, layerType);
                    if (!registered && !allowOverride)
                    {
                        registered = RegisterState(state, layerType, allowOverride);
                    }
                }
                else
                {
                    registered = RegisterState(state, layerType, allowOverride);
                }

                if (registered)
                {
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"✓ 注册SharedData状态: [{layerType}] {state.strKey} (ID:{state.intKey})");
                    return true;
                }

                state.TryAutoPushedToPool();
                return false;
            }
            catch (Exception e)
            {
                string stateName = sharedData.basicConfig != null ? sharedData.basicConfig.stateName : "<unknown>";
                StateMachineDebugSettings.Instance.LogError($"注册SharedData状态异常: {stateName}\n{e}");
                state.TryAutoPushedToPool();
                return false;
            }
        }

        public bool UnregisterState(string stateKey)
        {
            if (!stringToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        public bool UnregisterState(int stateKey)
        {
            if (!intToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        private bool UnregisterStateCore(StateBase state)
        {
            if (state == null) return false;
            if (state.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateState(state.strKey);
            }

            RemoveExitActivationBindingsRelatedToState(state);

            if (!string.IsNullOrEmpty(state.strKey))
            {
                stringToStateMap.Remove(state.strKey);
            }
            if (state.intKey != -1)
            {
                intToStateMap.Remove(state.intKey);
            }

            transitionCache.Remove(state.strKey);
            stateLayerMap.Remove(state);
            _activationCache.Remove(state);
            _registeredStatesList.Remove(state);
            MarkDirty(StateDirtyReason.Release);
            state.TryAutoPushedToPool();
            return true;
        }

        private int GenerateUniqueIntKey(StateBase state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("State.stateSharedData 不能为空（注册期不应出现空共享数据）");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("State.stateSharedData.basicConfig 不能为空（注册期不应出现空基础配置）");
#endif

            var sharedData = state.stateSharedData;
            var basicConfig = sharedData.basicConfig;

            int configId = basicConfig.stateId;

            if (configId == -1)
            {
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"状态'{basicConfig.stateName}' ID=-1，触发自动分配");
            }
            else if (configId > 0 && !intToStateMap.ContainsKey(configId))
            {
                return configId;
            }
            else if (configId > 0 && intToStateMap.ContainsKey(configId))
            {
                StateMachineDebugSettings.Instance.LogWarning(
                    $"⚠️ IntKey冲突! ID={configId} 已被'{intToStateMap[configId].strKey}'占用");
            }

            while (intToStateMap.ContainsKey(_nextAutoIntId))
            {
                _nextAutoIntId++;
            }
            int newId = _nextAutoIntId++;
            StateMachineDebugSettings.Instance.LogStateTransition($"✓ 自动分配IntKey: {newId}");
            return newId;
        }

        private string GenerateUniqueStringKey(StateBase state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("State.stateSharedData 不能为空（注册期不应出现空共享数据）");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("State.stateSharedData.basicConfig 不能为空（注册期不应出现空基础配置）");
#endif

            string configName = state.stateSharedData.basicConfig.stateName;
            if (!string.IsNullOrEmpty(configName) && !stringToStateMap.ContainsKey(configName))
            {
                return configName;
            }

            string baseName = "State";
            string candidateName;
            do
            {
                candidateName = $"{baseName}_{_nextAutoStringIdSuffix++}";
            }
            while (stringToStateMap.ContainsKey(candidateName));

            return candidateName;
        }

        private string GenerateUniqueStringKey(string preferredKey)
        {
            string baseName = string.IsNullOrEmpty(preferredKey) ? "State" : preferredKey;
            string candidateName = baseName;
            int attempt = 0;

            while (stringToStateMap.ContainsKey(candidateName))
            {
                candidateName = $"{baseName}_r{++attempt}";
            }

            return candidateName;
        }

        private void CheckAndSetFallbackState(StateBase state, StateLayerType layerType)
        {
            var sharedData = state.stateSharedData;
            var basicConfig = sharedData.basicConfig;

            if (basicConfig.canBeFeedback)
            {
                var fallbackFlag = basicConfig.stateSupportFlag;

                var layerRuntime = GetLayerByType(layerType);
                if (layerRuntime != null)
                {
                    layerRuntime.SetFallBack(state.intKey, fallbackFlag);
                    StateMachineDebugSettings.Instance.LogFallback(
                        $"[FallBack-Register] ✓ [{layerType}] Flag={fallbackFlag} <- State '{state.strKey}' (ID:{state.intKey})");
                }
            }
        }

        private bool RegisterStateCore(string stringKey, int intKey, StateBase state, StateLayerType layer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("RegisterStateCore: stateSharedData 不能为空");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("RegisterStateCore: basicConfig 不能为空");
#else
            if (state == null || state.stateSharedData == null || state.stateSharedData.basicConfig == null) return false;
#endif

            var sharedData = state.stateSharedData;

            if (string.IsNullOrEmpty(stringKey))
            {
                StateMachineDebugSettings.Instance.LogError("RegisterStateCore: stringKey 不能为空");
                return false;
            }

            if (stringToStateMap.TryGetValue(stringKey, out var existedByString) && !ReferenceEquals(existedByString, state))
            {
                string oldStringKey = stringKey;
                stringKey = GenerateUniqueStringKey(stringKey);
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ String键冲突: '{oldStringKey}'已存在，自动重命名为'{stringKey}'");
            }

            if (intKey <= 0)
            {
                intKey = GenerateUniqueIntKey(state);
            }

            if (intToStateMap.TryGetValue(intKey, out var existedByInt) && !ReferenceEquals(existedByInt, state))
            {
                int oldIntKey = intKey;
                intKey = GenerateUniqueIntKey(state);
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ Int键冲突: ID={oldIntKey} 已存在，自动重新分配为 {intKey}");
            }

            stringToStateMap[stringKey] = state;
            intToStateMap[intKey] = state;
            state.strKey = stringKey;
            state.intKey = intKey;
            state.host = this;
            stateLayerMap[state] = layer;

            state.InternalRefreshSharedDataCache();
            if (!_registeredStatesList.Contains(state))
            {
                _registeredStatesList.Add(state);
            }

            CheckAndSetFallbackState(state, layer);

            if (sharedData.RequiresStateMachinePlayableAnimation)
            {
                var animationConfig = sharedData.animationConfig;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (animationConfig == null) throw new InvalidOperationException($"RegisterStateCore: animationSource={sharedData.animationSource} 但 animationConfig 为空 | State={stringKey}");
#endif
                var calculator = state._calculatorCached;
                if (calculator == null && animationConfig != null)
                {
                    calculator = animationConfig.calculator;
                }

                if (calculator != null)
                {
                    try
                    {
                        calculator.InitializeCalculator();
                        StateMachineDebugSettings.Instance.LogRuntimeInit(
                            $"✓ Calculator初始化: {stringKey} - {calculator.GetType().Name}");
                    }
                    catch (System.Exception e)
                    {
                        StateMachineDebugSettings.Instance.LogError($"Calculator初始化失败: {stringKey}\n{e}");
                    }
                }
            }

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

#if STATEMACHINEDEBUG
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsRuntimeInitEnabled)
            {
                dbg.LogRuntimeInit($"注册状态: {stringKey} (IntKey:{intKey}, Layer:{layer})");
            }
#endif
            return true;
        }

        public StateBase GetStateByString(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey)) return null;

            if (transitionCache.TryGetValue(stateKey, out var cachedState))
            {
                return cachedState;
            }

            if (stringToStateMap.TryGetValue(stateKey, out var state))
            {
                transitionCache[stateKey] = state;
                return state;
            }

            return null;
        }

        public StateBase GetStateByInt(int stateKey)
        {
            return intToStateMap.TryGetValue(stateKey, out var state) ? state : null;
        }

        public bool HasState(string stateKey)
        {
            return stringToStateMap.ContainsKey(stateKey);
        }

        public bool HasState(int stateKey)
        {
            return intToStateMap.ContainsKey(stateKey);
        }

        public void SetFallbackState(StateLayerType layerType, int stateId, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var layer = GetLayerByType(layerType);
            if (layer != null)
            {
                layer.SetFallBack(stateId, supportFlag);
            }
        }
    }
}
