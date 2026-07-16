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

            // 纭繚榛樿鐘舵€佸湪娉ㄥ唽瀹屾垚鍚庡彲琚縺娲?
            if (!string.IsNullOrEmpty(defaultStateKey))
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
        /// 鎵归噺娉ㄥ唽鐘舵€侊紙浠嶪nfo鍒楄〃锛?
        /// </summary>
        /// <param name="infos">鐘舵€両nfo闆嗗悎</param>
        /// <param name="allowOverride">鏄惁鍏佽瑕嗙洊宸插瓨鍦ㄧ殑鐘舵€侀敭</param>
        /// <returns>鎴愬姛娉ㄥ唽鐨勭姸鎬佹暟閲?/returns>
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
        /// 娉ㄥ唽鍗曚釜鐘舵€侊紙浠嶪nfo锛? 绾补濮旀墭缁橲tateMachine
        /// </summary>
        /// <param name="info">鐘舵€両nfo</param>
        /// <param name="allowOverride">鏄惁鍏佽瑕嗙洊宸插瓨鍦ㄧ殑鐘舵€侀敭</param>
        /// <returns>鎴愬姛杩斿洖 StateBase锛屽け璐ヨ繑鍥?null</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            if (stateMachine == null)
            {
                Debug.LogError("[StateDomain] StateMachine is not initialized, cannot register state.");
                return null;
            }

            // 鐩存帴濮旀墭缁橲tateMachine澶勭悊鎵€鏈夐€昏緫锛堝垵濮嬪寲銆侀敭鍐茬獊銆佹敞鍐岋級
            var state = stateMachine.RegisterStateFromInfo(info, allowOverride);

            // 娉ㄥ唽鎴愬姛鍚庣紦瀛業nfo锛堢敤浜嶥omain灞傜鐞嗭級
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
            stateMachine.StartStateMachine();
            _stateMachineInitialized = true;
            _warnedMissingCoreForStateMachineInit = false;
            _warnedMissingAnimatorForStateMachineInit = false;

            // 6. 灏濊瘯婵€娲诲垵濮嬬姸鎬?
            if (!string.IsNullOrEmpty(initialStateName))
            {
                // TODO: 绛夊緟鐘舵€佽浆鎹㈤€昏緫楠岃瘉鍚庡惎鐢?
                // bool activated = stateMachine.TryEnterState(stateMachine.GetStateByStringKey(initialStateName));
                // if (activated)
                // {
                //     Debug.Log($"[StateDomain] 婵€娲诲垵濮嬬姸鎬? {initialStateName}");
                // }
                // else
                // {
                //     Debug.LogWarning($"[StateDomain] 鏃犳硶婵€娲诲垵濮嬬姸鎬? {initialStateName}");
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
