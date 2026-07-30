using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public partial class EntityStateDomain
    {
        public void MarkStatePackDirty()
        {
            _packDirty = true;
        }

        private void InitializeStateAniDataPack()
        {
            CollectPackSources(_workingPackSources);
            if (_workingPackSources.Count == 0) return;

            if (HavePackSourcesChanged(_workingPackSources))
            {
                CachePackSources(_workingPackSources);
                _packDirty = true;
            }

            if (!_packDirty) return;
            _cachedInfos.Clear();

            for (int i = 0; i < _workingPackSources.Count; i++)
            {
                var pack = _workingPackSources[i];
                if (pack == null) continue;
                pack.Check();
                RegisterStatesFromInfos(pack.Infos.Values, allowOverride: false);
            }

            // 运行中热加载数据包时，补齐默认状态；首次启动交给 StartStateMachineAfterDataLoaded。
            if (stateMachine.isRunning && !string.IsNullOrEmpty(defaultStateKey))
            {
                var defaultState = stateMachine.GetStateByString(defaultStateKey);
                if (defaultState != null && defaultState.baseStatus != StateBaseStatus.Running)
                {
                    stateMachine.TryActivateState(defaultStateKey);
                }
            }

            _packDirty = false;
        }

        private void CollectPackSources(List<StateAniDataPack> result)
        {
            result.Clear();
            AppendPack(result, stateAniDataPack);
            AppendPack(result, gunStateAniDataPack);

            if (additionalStateAniDataPacks == null) return;
            for (int i = 0; i < additionalStateAniDataPacks.Count; i++)
                AppendPack(result, additionalStateAniDataPacks[i]);
        }

        private static void AppendPack(List<StateAniDataPack> result, StateAniDataPack pack)
        {
            if (pack == null || result.Contains(pack)) return;
            result.Add(pack);
        }

        private bool HavePackSourcesChanged(List<StateAniDataPack> current)
        {
            if (_cachedPackSources.Count != current.Count)
                return true;

            for (int i = 0; i < current.Count; i++)
            {
                if (!ReferenceEquals(_cachedPackSources[i], current[i]))
                    return true;
            }

            return false;
        }

        private void CachePackSources(List<StateAniDataPack> current)
        {
            _cachedPackSources.Clear();
            _cachedPackSources.AddRange(current);
        }

        /// <summary>
        /// 批量注册状态（从 Info 列表）。
        /// </summary>
        /// <param name="infos">状态 Info 集合。</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态键。</param>
        /// <returns>成功注册的状态数量。</returns>
        public int RegisterStatesFromInfos(IEnumerable<StateAniDataInfo> infos, bool allowOverride = false)
        {
            if (infos == null) return 0;

            int successCount = 0;
            foreach (var info in infos)
            {
                if (RegisterStateFromInfo(info, allowOverride) != null)
                {
                    successCount++;
                }
            }

            return successCount;
        }

        public int RegisterStatesFromPack(StateAniDataPack pack, bool allowOverride = false)
        {
            if (pack == null) return 0;
            pack.Check();
            return RegisterStatesFromInfos(pack.Infos.Values, allowOverride);
        }

        public int RegisterStatesFromPacks(IEnumerable<StateAniDataPack> packs, bool allowOverride = false)
        {
            if (packs == null) return 0;

            int successCount = 0;
            foreach (var pack in packs)
                successCount += RegisterStatesFromPack(pack, allowOverride);

            return successCount;
        }

        /// <summary>
        /// 注册单个状态（从 Info），纯粹委托给 StateMachine。
        /// </summary>
        /// <param name="info">状态 Info。</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态键。</param>
        /// <returns>成功返回 StateBase，失败返回 null。</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            if (stateMachine == null)
            {
                Debug.LogError("[StateDomain] StateMachine is not initialized, cannot register state.");
                return null;
            }

            // 直接委托 StateMachine 处理全部逻辑（初始化、键冲突、注册）。
            var state = stateMachine.RegisterStateFromInfo(info, allowOverride);

            // 注册成功后缓存 Info，供 Domain 层管理。
            if (state != null && info != null)
            {
                _cachedInfos.Add(info);
            }

            return state;
        }

        private void InitializeStateMachine()
        {
            if (MyCore == null)
            {
                WarnStateMachineInitSkipped(
                    ref _warnedMissingCoreForStateMachineInit,
                    "[StateDomain] InitializeStateMachine skipped: MyCore is null.");
                return;
            }

            if (_cachedAnimator == null)
            {
                _cachedAnimator = MyCore.animator;
            }
            if (_cachedAnimator == null)
            {
                string entityName = MyCore.GetType().Name;
                WarnStateMachineInitSkipped(
                    ref _warnedMissingAnimatorForStateMachineInit,
                    $"[StateDomain] InitializeStateMachine skipped: {entityName}.animator is null."
                );
                return;
            }

            if (stateMachine == null) stateMachine = new StateMachine();
            stateMachine.stateMachineKey = string.IsNullOrEmpty(defaultStateKey) ? "Entity" : defaultStateKey;
            stateMachine.Initialize(MyCore, _cachedAnimator);
            stateMachine.defaultStateKey = defaultStateKey;
            _stateMachineInitialized = true;
            _warnedMissingCoreForStateMachineInit = false;
            _warnedMissingAnimatorForStateMachineInit = false;

            // 6. 尝试激活初始状态。
            if (!string.IsNullOrEmpty(initialStateName))
            {
                // TODO: 等待状态转换逻辑验证后启用。
                // bool activated = stateMachine.TryEnterState(stateMachine.GetStateByStringKey(initialStateName));
                // if (activated)
                // {
                //     Debug.Log($"[StateDomain] 激活初始状态：{initialStateName}");
                // }
                // else
                // {
                //     Debug.LogWarning($"[StateDomain] 无法激活初始状态：{initialStateName}");
                // }

                Debug.Log($"[StateDomain] Initial state configured: {initialStateName}");
            }
        }

        private void StartStateMachineAfterDataLoaded()
        {
            if (!_stateMachineInitialized || stateMachine == null || stateMachine.isRunning)
                return;

            stateMachine.defaultStateKey = defaultStateKey;
            stateMachine.StartStateMachine();
        }

        private void WarnStateMachineInitSkipped(ref bool warnedFlag, string message)
        {
            if (warnedFlag) return;
            warnedFlag = true;
            Debug.LogWarning(message);
        }
    }
}
