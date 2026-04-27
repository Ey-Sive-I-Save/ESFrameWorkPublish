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
                StateMachineDebugSettings.Instance.LogError("[TempAnim] 临时状态键不能为空");
                return false;
            }

            if (clip == null)
            {
                StateMachineDebugSettings.Instance.LogError("[TempAnim] AnimationClip不能为空");
                return false;
            }

            if (_temporaryStates.ContainsKey(tempKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] 临时状态 {tempKey} 已存在，先移除旧的");
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
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] 注册临时状态失败: {tempKey}");
                return false;
            }

            if (!TryActivateState(tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] 激活临时状态失败: {tempKey}");
                UnregisterState(tempState.strKey);
                return false;
            }

            _temporaryStates[tempKey] = tempState;
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[TempAnim] ✓ 添加临时动画: {tempKey} | Clip:{clip.name} | Layer:{layer}");
            return true;
        }

        public bool RemoveTemporaryAnimation(string tempKey)
        {
            if (!_temporaryStates.TryGetValue(tempKey, out var tempState))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] 临时状态 {tempKey} 不存在");
                return false;
            }

            if (tempState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateState(tempState.strKey);
            }
            UnregisterState(tempState.strKey);

            _temporaryStates.Remove(tempKey);
            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] ✓ 移除临时动画: {tempKey}");
            return true;
        }

        public void ClearAllTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] 没有临时动画需要清除");
                return;
            }

            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] 开始清除 {_temporaryStates.Count} 个临时动画");

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
            StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] ✓ 所有临时动画已清除");
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
                // 预留：通过Entity广播
                // hostEntity.BroadcastEvent(eventName, eventParam);
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            string stateName = state != null ? state.strKey : "<null>";
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateMachine] 广播动画事件: {eventName} | State: {stateName} | Param: {eventParam}");
#endif
#endif
        }
    }
}