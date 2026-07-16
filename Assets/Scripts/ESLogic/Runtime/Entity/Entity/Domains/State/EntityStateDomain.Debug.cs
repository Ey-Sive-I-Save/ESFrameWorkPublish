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
        #region 测试方法

        [Button("动态注册单个状态"), FoldoutGroup("状态注册")]
        public void TestRegisterSingleState(StateAniDataInfo info, bool allowOverride = false)
        {
            if (info == null)
            {
                Debug.LogWarning("[StateDomain] 璇锋彁渚涙湁鏁堢殑StateAniDataInfo");
                return;
            }

            var state = RegisterStateFromInfo(info, allowOverride);
            if (state != null)
            {
                Debug.Log($"[StateDomain] 鍔ㄦ€佹敞鍐屾垚鍔? {info.sharedData.basicConfig.stateName}");
            }
        }

        [Button("重新注册所有状态（覆盖）"), FoldoutGroup("状态注册")]
        public void TestReregisterAllStates()
        {
            CollectPackSources(_workingPackSources);
            if (_workingPackSources.Count == 0)
            {
                Debug.LogWarning("[StateDomain] State data pack is null.");
                return;
            }

            int count = RegisterStatesFromPacks(_workingPackSources, allowOverride: true);
            Debug.Log($"[StateDomain] Re-register complete. Count={count}");
        }

        [Button("测试激活状态(String)"), FoldoutGroup("测试功能")]
        public void TestActivateStateByString(string stateKey)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 鐘舵€佹満鏈垵濮嬪寲");
                return;
            }

            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogWarning("[StateDomain] State key cannot be empty.");
                return;
            }

            var state = stateMachine.GetStateByString(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"[StateDomain] 鏈壘鍒扮姸鎬? {stateKey}");
                return;
            }

            bool success = stateMachine.TryActivateState(stateKey);
            if (success)
            {
                Debug.Log($"[StateDomain] 鎴愬姛婵€娲荤姸鎬? {stateKey} (IntKey:{state.intKey})");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 婵€娲荤姸鎬佸け璐? {stateKey}");
            }
        }

        [Button("测试激活状态(Int)"), FoldoutGroup("测试功能")]
        public void TestActivateStateByInt(int stateId)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 鐘舵€佹満鏈垵濮嬪寲");
                return;
            }

            var state = stateMachine.GetStateByInt(stateId);
            if (state == null)
            {
                Debug.LogWarning($"[StateDomain] 鏈壘鍒扮姸鎬両D: {stateId}");
                return;
            }

            bool success = stateMachine.TryActivateState(stateId);
            if (success)
            {
                Debug.Log($"[StateDomain] 鎴愬姛婵€娲荤姸鎬? {state.strKey} (IntKey:{stateId})");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 婵€娲荤姸鎬佸け璐? {stateId}");
            }
        }

        [Button("测试关闭状态(String)"), FoldoutGroup("测试功能")]
        public void TestDeactivateStateByString(string stateKey)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 鐘舵€佹満鏈垵濮嬪寲");
                return;
            }

            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogWarning("[StateDomain] State key cannot be empty.");
                return;
            }

            bool success = stateMachine.TryDeactivateState(stateKey);
            if (success)
            {
                Debug.Log($"[StateDomain] 鎴愬姛鍏抽棴鐘舵€? {stateKey}");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 鍏抽棴鐘舵€佸け璐? {stateKey}");
            }
        }

        [Button("测试关闭状态(Int)"), FoldoutGroup("测试功能")]
        public void TestDeactivateStateByInt(int stateId)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 鐘舵€佹満鏈垵濮嬪寲");
                return;
            }

            bool success = stateMachine.TryDeactivateState(stateId);
            if (success)
            {
                Debug.Log($"[StateDomain] 鎴愬姛鍏抽棴鐘舵€両D: {stateId}");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 鍏抽棴鐘舵€佸け璐? {stateId}");
            }
        }

        [Button("List All States"), FoldoutGroup("Test Tools")]
        public void TestListAllStates()
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 鐘舵€佹満鏈垵濮嬪寲");
                return;
            }

            // 鈹€鈹€ 鏋勫缓瀹屾暣鏂囨湰锛堟帶鍒跺彴 + 瀵硅瘽妗?+ 鍓创鏉垮叡鐢ㄥ悓涓€鏁版嵁婧愶級鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 鐘舵€佹満鎵€鏈夌姸鎬?({stateMachine.RegisteredStateCount}) ===");

            foreach (var kvp in stateMachine.EnumerateRegisteredStatesByKey())
            {
                var state = kvp.Value;
                var layerType = stateMachine.TryGetStateLayerType(state, out var layer)
                    ? layer.ToString() : "Unknown";
                var isRunning = stateMachine.IsStateRunning(state);
                var isFallback = state.stateSharedData?.basicConfig?.canBeFeedback ?? false;

                sb.AppendLine($"  [{layerType}] {kvp.Key} (IntKey:{state.intKey}) - " +
                              $"杩愯:{isRunning}, Fallback:{isFallback}");
            }

            // 鈹€鈹€ 鎺у埗鍙拌緭鍑?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            Debug.Log(sb.ToString());

#if UNITY_EDITOR
            // 鈹€鈹€ 澶嶅埗鍒扮郴缁熷壀璐存澘锛堝彲鐩存帴绮樿创浣跨敤锛?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            UnityEditor.EditorGUIUtility.systemCopyBuffer = sb.ToString();

            // 鈹€鈹€ 寮瑰嚭缂栬緫鍣ㄥ璇濇棰勮锛堝唴瀹瑰凡鍦ㄥ壀璐存澘锛屽彲闅忔椂绮樿创锛?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            // DisplayDialog 鍗曟潯 message 鏈€澶氭樉绀虹害 40 琛岋紝瓒呭嚭鏃舵埅鏂苟鎻愮ず
            const int MaxDialogLines = 35;
            var lines = sb.ToString().Split('\n');
            string dialogBody;
            if (lines.Length <= MaxDialogLines)
            {
                dialogBody = sb.ToString().TrimEnd();
            }
            else
            {
                var truncated = new System.Text.StringBuilder();
                for (int i = 0; i < MaxDialogLines; i++) truncated.AppendLine(lines[i]);
                truncated.AppendLine($"... 锛堝叡 {lines.Length - 1} 琛岋紝瀹屾暣鍐呭宸插鍒跺埌鍓创鏉匡級");
                dialogBody = truncated.ToString().TrimEnd();
            }

            UnityEditor.EditorUtility.DisplayDialog(
                $"StateMachine State List ({stateMachine.RegisteredStateCount})",
                dialogBody + "\n\nFull content copied to clipboard",
                "OK");
#endif
        }

        [Button("Preflight StateMachine Config"), FoldoutGroup("Test Tools")]
        public bool TestPreflightStateMachineConfig()
        {
            return RunStateMachinePreflight(logToConsole: true);
        }

        public bool RunStateMachinePreflight(bool logToConsole)
        {
            var errors = new List<string>(16);
            var warnings = new List<string>(16);
            var allInfos = new List<StateAniDataInfo>(128);
            var stateById = new Dictionary<int, StateAniDataInfo>(128);
            var fallbackCandidateCount = new Dictionary<string, int>(64);

            if (MyCore == null)
            {
                errors.Add("MyCore is null. StateDomain is not bound to Entity.");
            }

            var animator = MyCore != null ? MyCore.animator : _cachedAnimator;
            if (animator == null)
            {
                errors.Add("Animator is null. StateMachine cannot bind PlayableGraph.");
            }

            if (stateMachine == null)
            {
                errors.Add("stateMachine is null.");
            }

            CollectPackSources(_workingPackSources);
            if (_workingPackSources.Count == 0)
            {
                warnings.Add("No StateAniDataPack configured.");
            }

            var nameSet = new HashSet<string>(StringComparer.Ordinal);
            var idSet = new HashSet<int>();

            for (int i = 0; i < _workingPackSources.Count; i++)
            {
                var pack = _workingPackSources[i];
                if (pack == null)
                {
                    warnings.Add($"State pack index {i} is null.");
                    continue;
                }

                pack.Check();

                if (pack.Infos == null)
                {
                    warnings.Add($"State pack {pack.name} Infos is null.");
                    continue;
                }

                foreach (var info in pack.Infos.Values)
                {
                    allInfos.Add(info);

                    if (info == null)
                    {
                        warnings.Add($"State pack {pack.name} contains null StateAniDataInfo.");
                        continue;
                    }

                    var basic = info.sharedData != null ? info.sharedData.basicConfig : null;
                    if (basic == null)
                    {
                        errors.Add($"State pack {pack.name} contains state with null basicConfig.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(basic.stateName))
                    {
                        errors.Add($"State pack {pack.name} contains empty stateName.");
                    }
                    else if (!nameSet.Add(basic.stateName))
                    {
                        errors.Add($"Duplicate state name: {basic.stateName}.");
                    }

                    if (basic.stateId >= 0 && !idSet.Add(basic.stateId))
                    {
                        errors.Add($"Duplicate state id: {basic.stateId}.");
                    }

                    if (basic.stateId >= 0 && !stateById.ContainsKey(basic.stateId))
                    {
                        stateById.Add(basic.stateId, info);
                    }

                    if (basic.ignoreSupportFlag && basic.disableActiveOnSupportFlagSwitching)
                    {
                        warnings.Add($"State {basic.stateName} has conflicting support-flag settings.");
                    }

                    if (basic.durationMode == StateDurationMode.UntilAnimationEnd && (info.sharedData == null || !info.sharedData.hasAnimation))
                    {
                        errors.Add($"State {basic.stateName} uses UntilAnimationEnd but animation is disabled.");
                    }

                    var shared = info.sharedData;
                    var animConfig = shared != null ? shared.animationConfig : null;
                    if (shared != null && shared.hasAnimation)
                    {
                        if (animConfig == null)
                        {
                            errors.Add($"State {basic.stateName} hasAnimation=true but animationConfig is null.");
                        }
                        else if (animConfig.calculator == null)
                        {
                            errors.Add($"State {basic.stateName} animationConfig.calculator is null.");
                        }
                    }

                    ValidateIKContract(info, errors, warnings);

                    if (basic.canBeFeedback)
                    {
                        string fbKey = BuildFallbackCoverageKey(basic.layerType, NormalizeSingleSupportFlagForPreflight(basic.stateSupportFlag));
                        fallbackCandidateCount.TryGetValue(fbKey, out int existing);
                        fallbackCandidateCount[fbKey] = existing + 1;
                    }
                }
            }

            if (!string.IsNullOrEmpty(defaultStateKey) && !nameSet.Contains(defaultStateKey))
            {
                warnings.Add($"defaultStateKey={defaultStateKey} was not found in configured packs.");
            }

            ValidateFallbackCoverageFromPacks(fallbackCandidateCount, warnings);
            ValidateFallbackReachabilityInRuntime(errors, warnings, stateById);
            ValidateActivationMatrixCurrentRuntime(errors, warnings);
            ValidateActivationRollbackConsistency(errors, warnings);

            bool success = errors.Count == 0;

            if (logToConsole)
            {
                var sb = new StringBuilder(512);
                sb.AppendLine("=== StateDomain 棰勬鎶ュ憡 ===");
                sb.AppendLine($"Entity: {(MyCore != null ? MyCore.name : "<null>")}");
                sb.AppendLine($"PackCount: {_workingPackSources.Count} | Errors: {errors.Count} | Warnings: {warnings.Count}");

                if (errors.Count > 0)
                {
                    sb.AppendLine("[Errors]");
                    for (int i = 0; i < errors.Count; i++) sb.AppendLine($"  - {errors[i]}");
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine("[Warnings]");
                    for (int i = 0; i < warnings.Count; i++) sb.AppendLine($"  - {warnings[i]}");
                }

                if (success)
                {
                    Debug.Log(sb.ToString());
                }
                else
                {
                    Debug.LogError(sb.ToString());
                }
            }

            return success;
        }

        private static StateSupportFlags NormalizeSingleSupportFlagForPreflight(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.Grounded;
            ushort value = (ushort)flag;
            ushort lowest = (ushort)(value & (ushort)(-(short)value));
            return (StateSupportFlags)lowest;
        }

        private static string BuildFallbackCoverageKey(StateLayerType layer, StateSupportFlags flag)
        {
            return layer + "|" + flag;
        }

        private static bool HasAnyIKGoalEnabled(StateProceduralDriveData procedural)
        {
            if (procedural == null) return false;

            if (procedural.ikLeftHand != null && procedural.ikLeftHand.enabled) return true;
            if (procedural.ikRightHand != null && procedural.ikRightHand.enabled) return true;
            if (procedural.ikLeftFoot != null && procedural.ikLeftFoot.enabled) return true;
            if (procedural.ikRightFoot != null && procedural.ikRightFoot.enabled) return true;
            if (procedural.ikLookAt != null && procedural.ikLookAt.enabled) return true;

            var segments = procedural.ikSegments;
            if (segments == null) return false;

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null || !segment.enabled) continue;

                if (segment.ikLeftHand != null && segment.ikLeftHand.enabled) return true;
                if (segment.ikRightHand != null && segment.ikRightHand.enabled) return true;
                if (segment.ikLeftFoot != null && segment.ikLeftFoot.enabled) return true;
                if (segment.ikRightFoot != null && segment.ikRightFoot.enabled) return true;
                if (segment.ikLookAt != null && segment.ikLookAt.enabled) return true;
            }

            return false;
        }

        private static void ValidateIKContract(StateAniDataInfo info, List<string> errors, List<string> warnings)
        {
            if (info == null || info.sharedData == null || info.sharedData.basicConfig == null) return;

            string stateName = info.sharedData.basicConfig.stateName;
            var shared = info.sharedData;
            var procedural = shared.proceduralDriveConfig;
            if (procedural == null) return;

            if (procedural.enableIK)
            {
                if (!shared.hasAnimation)
                {
                    errors.Add($"State {stateName} enables procedural IK but hasAnimation=false.");
                }

                if (!HasAnyIKGoalEnabled(procedural))
                {
                    errors.Add($"State {stateName} enables procedural IK but no IK goal is enabled.");
                }
            }

            if (procedural.enableMatchTarget && !shared.hasAnimation)
            {
                warnings.Add($"State {stateName} enables MatchTarget but has no animation.");
            }
        }

        private static void ValidateFallbackCoverageFromPacks(Dictionary<string, int> fallbackCandidateCount, List<string> warnings)
        {
            for (int layer = 0; layer < (int)StateLayerType.Count; layer++)
            {
                var layerType = (StateLayerType)layer;
                for (int i = 0; i < PreflightSupportFlags.Length; i++)
                {
                    var supportFlag = PreflightSupportFlags[i];
                    string key = BuildFallbackCoverageKey(layerType, supportFlag);
                    fallbackCandidateCount.TryGetValue(key, out int count);

                    if (count <= 0)
                    {
                        warnings.Add($"Fallback missing: Layer={layerType}, Support={supportFlag} has no canBeFeedback candidate.");
                    }
                    else if (count > 1)
                    {
                        warnings.Add($"Fallback candidate duplicated: Layer={layerType}, Support={supportFlag}, Count={count}.");
                    }
                }
            }
        }

        private void ValidateFallbackReachabilityInRuntime(List<string> errors, List<string> warnings, Dictionary<int, StateAniDataInfo> stateById)
        {
            if (stateMachine == null || !_stateMachineInitialized) return;

            for (int layer = 0; layer < (int)StateLayerType.Count; layer++)
            {
                var layerType = (StateLayerType)layer;
                var layerRuntime = stateMachine.GetLayer(layerType);
                if (layerRuntime == null)
                {
                    errors.Add($"杩愯鏃跺眰绾т涪澶? {layerType}");
                    continue;
                }

                for (int i = 0; i < PreflightSupportFlags.Length; i++)
                {
                    var supportFlag = PreflightSupportFlags[i];
                    int fallbackStateId = layerRuntime.GetFallBack(supportFlag);
                    if (fallbackStateId < 0)
                    {
                        continue;
                    }

                    var fallbackState = stateMachine.GetStateByInt(fallbackStateId);
                    if (fallbackState == null)
                    {
                        errors.Add($"Fallback unreachable: Layer={layerType}, Support={supportFlag}, StateID={fallbackStateId} not registered.");
                        continue;
                    }

                    if (!stateMachine.TryGetStateLayerType(fallbackState, out var fallbackLayer) || fallbackLayer != layerType)
                    {
                        errors.Add($"Fallback layer mismatch: Layer={layerType}, Support={supportFlag}, State={fallbackState.strKey}, Actual={fallbackLayer}.");
                    }

                    if (stateById.TryGetValue(fallbackStateId, out var info)
                        && info != null
                        && info.sharedData != null
                        && info.sharedData.basicConfig != null
                        && !info.sharedData.basicConfig.canBeFeedback)
                    {
                        warnings.Add($"Fallback spec risk: State={fallbackState.strKey} registered as fallback but canBeFeedback=false.");
                    }
                }
            }
        }

        private void ValidateActivationMatrixCurrentRuntime(List<string> errors, List<string> warnings)
        {
            if (stateMachine == null || !_stateMachineInitialized) return;

            foreach (var kvp in stateMachine.EnumerateRegisteredStatesByKey())
            {
                var state = kvp.Value;
                if (state == null || state.stateSharedData == null || state.stateSharedData.basicConfig == null)
                {
                    errors.Add($"Activation matrix validation failed: registered state {kvp.Key} is null or missing config.");
                    continue;
                }

                var basic = state.stateSharedData.basicConfig;
                var result = stateMachine.TestStateActivation(state, basic.layerType);
                if (!result.CanActivate)
                {
                    warnings.Add($"婵€娲荤煩闃靛綋鍓嶆€佷笉鍙揪: State={state.strKey}, Layer={basic.layerType}, Reason={result.failureReason}");
                }
            }
        }

        private void ValidateActivationRollbackConsistency(List<string> errors, List<string> warnings)
        {
            if (stateMachine == null || !_stateMachineInitialized) return;

            int runningBefore = stateMachine.GetRunningStateCount();
            int layerBefore = stateMachine.GetLayerStateCount(StateLayerType.Main);

            string probeKey = "__preflight_fault_probe__" + Guid.NewGuid().ToString("N");
            int probeId = -1;
            bool registered = false;
            try
            {
                var probe = new PreflightFaultState
                {
                    stateSharedData = new StateSharedData
                    {
                        basicConfig = new StateBasicConfig
                        {
                            stateName = probeKey,
                            stateId = -1,
                            layerType = StateLayerType.Main,
                            durationMode = StateDurationMode.Infinite,
                            ignoreSupportFlag = true,
                            resetSupportFlagOnEnter = false,
                            supportReStart = false,
                            canBeFeedback = false,
                        },
                        hasAnimation = false,
                    },
                    stateVariableData = new StateVariableData()
                };

                probe.stateSharedData.InitializeRuntime();
                registered = stateMachine.RegisterState(probeKey, probe, StateLayerType.Main);
                if (!registered)
                {
                    errors.Add("Activation rollback probe registration failed.");
                    return;
                }

                probeId = probe.intKey;

                bool activated = stateMachine.TryActivateState(probe, StateLayerType.Main);
                if (activated)
                {
                    errors.Add("Activation rollback probe failed: fault state activated unexpectedly.");
                }

                int runningAfter = stateMachine.GetRunningStateCount();
                int layerAfter = stateMachine.GetLayerStateCount(StateLayerType.Main);
                if (runningAfter != runningBefore || layerAfter != layerBefore)
                {
                    errors.Add($"婵€娲诲洖婊氫笉涓€鑷? Running {runningBefore}->{runningAfter}, MainLayer {layerBefore}->{layerAfter}");
                }

                if (!stateMachine.TryGetActivationEventFromLatest(0, out var latest))
                {
                    warnings.Add("Structured activation event is empty.");
                }
                else if (latest.kind != StateMachine.ActivationEventKind.Rollback && latest.kind != StateMachine.ActivationEventKind.Failure)
                {
                    warnings.Add($"缁撴瀯鍖栨縺娲讳簨浠舵湭璁板綍澶辫触缁撹: LatestKind={latest.kind}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"婵€娲诲洖婊氭帰閽堟墽琛屽紓甯? {ex.Message}");
            }
            finally
            {
                if (registered)
                {
                    if (probeId > 0)
                    {
                        stateMachine.UnregisterState(probeId);
                    }
                    else
                    {
                        stateMachine.UnregisterState(probeKey);
                    }
                }
            }
        }

        #endregion

    }
}
