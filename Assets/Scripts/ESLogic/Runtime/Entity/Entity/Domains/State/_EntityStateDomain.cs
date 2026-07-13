

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
    [Serializable, TypeRegistryItem("State Domain")]
    public class EntityStateDomain : Domain<Entity, EntityStateModuleBase>, IPreviewElement, IPreviewAreaModeProvider, IPreviewElementLifecycle, IPreviewElementEditorUpdate
    {
        private static readonly StateSupportFlags[] PreflightSupportFlags =
        {
            StateSupportFlags.Grounded,
            StateSupportFlags.Crouched,
            StateSupportFlags.Prone,
            StateSupportFlags.Swimming,
            StateSupportFlags.Flying,
            StateSupportFlags.Mounted,
            StateSupportFlags.Climbing,
            StateSupportFlags.SpecialInteraction,
            StateSupportFlags.Observer,
            StateSupportFlags.Dead,
            StateSupportFlags.Transition,
        };

        private sealed class PreflightFaultState : StateBase
        {
            protected override void OnStateEnterLogic()
            {
                throw new InvalidOperationException("PreflightFaultState.OnStateEnterLogic");
            }
        }

        [Title("State Data")]
        [LabelText("Animation State Data Pack")]
        public StateAniDataPack stateAniDataPack;

        [LabelText("Gun State Data Pack")]
        public GunStateAniDataPack gunStateAniDataPack;

        [LabelText("Additional State Data Packs")]
        public List<StateAniDataPack> additionalStateAniDataPacks = new List<StateAniDataPack>();

        [Title("State Machine")]
        [LabelText("State Machine")]
        public StateMachine stateMachine = new StateMachine();

        [LabelText("Default State Key (Optional)")]
        public string defaultStateKey = "";

        [LabelText("Initial Active State Name (Optional)")]
        [Tooltip("State activated automatically after state machine startup. Empty means no auto activation.")]
        public string initialStateName = "";

        [Title("Skill Runtime Test")]
        [Button("确保技能轨道运行时测试模块存在"), PropertyOrder(-10)]
        public void EnsureSkillRuntimeTestModuleExists()
        {
            if (FindMyModule<EntityStateSkillRuntimeTestModule>() != null)
                return;

            MyModules.Add(new EntityStateSkillRuntimeTestModule());
            MyModules.ApplyBuffers(true);
        }

        [NonSerialized] private bool _stateMachineInitialized;
        [NonSerialized] private Animator _cachedAnimator;
        [NonSerialized] private bool _warnedMissingCoreForStateMachineInit;
        [NonSerialized] private bool _warnedMissingAnimatorForStateMachineInit;

        [NonSerialized] private bool _packDirty = true;
        [NonSerialized] private List<StateAniDataInfo> _cachedInfos = new List<StateAniDataInfo>(64);
        [NonSerialized] private List<StateAniDataPack> _cachedPackSources = new List<StateAniDataPack>(4);
        [NonSerialized] private List<StateAniDataPack> _workingPackSources = new List<StateAniDataPack>(4);

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            // 蹇呴』鍏堝垵濮嬪寲StateMachine锛堝垱寤哄眰绾х瓑鍩虹璁炬柦锛夛紝鍐嶅垵濮嬪寲StateAniDataPack锛堟敞鍐岀姸鎬侊級
            InitializeStateMachine();
            InitializeStateAniDataPack();
        }

        protected override void Update()
        {
            if (_stateMachineInitialized)
            {
                stateMachine.UpdateStateMachine();
            }
            base.Update();
        }

        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            DisposePreviewRender();
#endif
            if (_stateMachineInitialized)
            {
                stateMachine.StopStateMachine();
                stateMachine.Dispose();
                _stateMachineInitialized = false;
            }
            base.OnDestroy();
        }

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

        private void WarnStateMachineInitSkipped(ref bool warnedFlag, string message)
        {
            if (warnedFlag) return;
            warnedFlag = true;
            Debug.LogWarning(message);
        }

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

        #region EditorPreview
#if UNITY_EDITOR
        // 缓存各层级折叠状态，避免每次 OnGUI 重置。
        private static Dictionary<StateLayerType, bool> layerFoldouts = new Dictionary<StateLayerType, bool>();
        [NonSerialized] private IEditorTrackSupport_GetSequence _previewTrackContainer;
        [NonSerialized] private float _previewTrackTime;
        [NonSerialized] private bool _previewTrackAutoScanned;
        [NonSerialized] private GameObject _previewCameraRoot;
        [NonSerialized] private Camera _previewCamera;
        [NonSerialized] private Light _previewKeyLight;
        [NonSerialized] private Light _previewFillLight;
        [NonSerialized] private RenderTexture _previewRenderTexture;
        [NonSerialized] private GameObject _previewRenderRoot;
        [NonSerialized] private Entity _previewRenderEntity;
        [NonSerialized] private EditorSequencePlayer _previewRenderPlayer;
        [NonSerialized] private ESEditorPreviewResourceScope _previewResourceScope;
        [NonSerialized] private bool _previewRenderPlaying;
        [NonSerialized] private bool _previewRenderCompleted;
        [NonSerialized] private bool _previewRenderUpdateRegistered;
        [NonSerialized] private double _previewRenderLastTime;
        [NonSerialized] private Vector2 _previewRenderOrbit = new Vector2(25f, -20f);
        [NonSerialized] private float _previewZoom = 1f;
        [NonSerialized] private bool _previewAutoFitView = false;
        [NonSerialized] private Vector3 _previewViewCenter = Vector3.up;
        [NonSerialized] private float _previewViewRadius = 1f;
        [NonSerialized] private float _previewPlaybackSpeed = 1f;
        private static bool _entityTrackPreviewFoldout = true;
        private static bool _suppressAutoPreviewRebuildAfterManualCleanup;
        private const float PreviewRenderHeight = 260f;
        private const float PreviewRenderTextureMaxSize = 2048f;
        private const float PreviewRenderScale = 2f;
        private const int PreviewRenderLayer = 31;
        private const string PreviewRootSuffix = "_ESPreview";
        private const string PreviewCameraName = "ES_URP_EntityPreviewCamera";
        private const string PreviewKeyLightName = "ES_URP_EntityPreviewKeyLight";
        private const string PreviewFillLightName = "ES_URP_EntityPreviewFillLight";
        private const string PreviewResourceOwner = nameof(EntityStateDomain);

        public bool IsSingleArea => true;
        public bool CanPreview => stateMachine != null || !Application.isPlaying;
        public bool EditorPreviewCanPreviewNonPlay => true;
        public PreviewAreaMode PreviewAreaMode => PreviewAreaMode.Large;
        public bool WantsPreviewEditorUpdate => _previewRenderPlaying && _previewRenderPlayer != null && _previewRenderRoot != null;

        public void OnPreviewEnable()
        {
        }

        public void OnPreviewDisable()
        {
            DisposePreviewRender();
        }

        public void DisposePreview()
        {
            DisposePreviewRender();
        }

        public void OnPreviewEditorUpdate(float deltaTime)
        {
            UpdatePreviewPlayback(deltaTime, repaintAllViews: false);
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void CleanupEntityPreviewObjectsOnEditorLoad()
        {
            UnityEditor.EditorApplication.delayCall -= CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.delayCall += CleanupLingeringPreviewObjects;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= CleanupLingeringPreviewObjects;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.playModeStateChanged -= CleanupPreviewObjectsOnPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += CleanupPreviewObjectsOnPlayModeChange;
            UnityEditor.EditorApplication.quitting -= CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.quitting += CleanupLingeringPreviewObjects;
        }

        private static void CleanupPreviewObjectsOnPlayModeChange(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode
                || state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                CleanupLingeringPreviewObjects();
        }

        [UnityEditor.MenuItem(MenuItemPathDefine.ROOT_PATH + "状态/清理实体预览残留对象", false, 2000)]
        private static void CleanupEntityPreviewObjectsMenu()
        {
            int removed = CleanupLingeringPreviewObjectsInternal();
            _suppressAutoPreviewRebuildAfterManualCleanup = true;
            Debug.Log($"[EntityStateDomain Preview] 已清理实体预览残留对象：{removed}");
        }

        public void DrawPreviewGUIPlaying() => EditorPreviewDrawPreviewGUIImpl();
        public void EditorPreviewDrawPreviewGUINonPlay()
        {
            EditorPreviewDrawTrackPreviewNonPlay();
        }

        private void EditorPreviewDrawTrackPreviewNonPlay()
        {
            _entityTrackPreviewFoldout = UnityEditor.EditorGUILayout.Foldout(_entityTrackPreviewFoldout, "实体轨道预览", true);
            if (!_entityTrackPreviewFoldout)
            {
                DisposePreviewRender();
                return;
            }

            Entity entity = ResolveEditorPreviewEntity();
            if (entity == null)
            {
                DisposePreviewRender();
                UnityEditor.EditorGUILayout.HelpBox("没有找到 Entity。请选择带 Entity 的对象，或检查当前 Inspector 目标。", UnityEditor.MessageType.Warning);
                return;
            }

            EnsurePreviewTrackAutoSelected();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            string selectedName = _previewTrackContainer != null
                ? $"{_previewTrackContainer.trackName} / {_previewTrackContainer.Sequence?.Name ?? "<无序列>"}"
                : "未选择轨道序列";
            UnityEditor.EditorGUILayout.LabelField("当前序列", selectedName);

            if (GUILayout.Button("自动", GUILayout.Width(52)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                _previewTrackAutoScanned = false;
                EnsurePreviewTrackAutoSelected(forceProjectScan: true);
                Entity autoEntity = ResolveEditorPreviewEntity();
                if (_previewTrackContainer != null && autoEntity != null)
                    RebuildPreviewRender(autoEntity, 0f);
            }

            if (GUILayout.Button("选择", GUILayout.Width(64)))
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                IEditorTrackSupport_GetSequence.ShowDynamicMenu(rect, OnPreviewTrackSelected);
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (_previewTrackContainer == null || _previewTrackContainer.Sequence == null)
            {
                DisposePreviewRender();
                UnityEditor.EditorGUILayout.HelpBox("没有可用的轨道序列。请选择 SKillDataInfo 资源，或点击“自动”扫描项目技能数据。", UnityEditor.MessageType.None);
                return;
            }

            float duration = _previewRenderPlayer != null ? Mathf.Max(0.01f, _previewRenderPlayer.Duration) : Mathf.Max(0.01f, GetSequenceDuration(_previewTrackContainer.Sequence));
            float current = _previewRenderPlayer != null ? _previewRenderPlayer.CurrentTime : Mathf.Clamp(_previewTrackTime, 0f, duration);

            UnityEditor.EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重建", GUILayout.Width(72)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                RebuildPreviewRender(entity, current);
            }

            if (GUILayout.Button("播放", GUILayout.Width(48)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                StartPreviewPlayback(entity, duration);
            }

            if (GUILayout.Button("暂停", GUILayout.Width(52)))
            {
                _previewRenderPlayer?.Pause();
                _previewRenderPlaying = false;
            }

            if (GUILayout.Button("停止", GUILayout.Width(48)))
            {
                StopPreviewPlaybackToIdle(resetTime: true, completed: false);
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("速度", GUILayout.Width(44));
            UnityEditor.EditorGUI.BeginChangeCheck();
            _previewPlaybackSpeed = UnityEditor.EditorGUILayout.Slider(_previewPlaybackSpeed, 0.05f, 3f, GUILayout.MaxWidth(220));
            if (UnityEditor.EditorGUI.EndChangeCheck() && _previewRenderPlayer != null)
                _previewRenderPlayer.Speed = _previewPlaybackSpeed;
            UnityEditor.EditorGUILayout.LabelField($"{_previewPlaybackSpeed:F2}x", GUILayout.Width(42));
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            _previewAutoFitView = UnityEditor.EditorGUILayout.ToggleLeft("自动适配视距", _previewAutoFitView, GUILayout.Width(120));
            UnityEditor.EditorGUILayout.LabelField("视距", GUILayout.Width(36));
            _previewZoom = UnityEditor.EditorGUILayout.Slider(_previewZoom, 0.25f, 4f, GUILayout.MaxWidth(180));
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("时间", GUILayout.Width(44));
            float newTime = UnityEditor.EditorGUILayout.Slider(current, 0f, duration, GUILayout.MaxWidth(320));
            UnityEditor.EditorGUILayout.LabelField($"{current:F2}s / {duration:F2}s", GUILayout.Width(110));
            UnityEditor.EditorGUILayout.EndHorizontal();
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                _previewTrackTime = newTime;
                if (!_suppressAutoPreviewRebuildAfterManualCleanup)
                {
                    EnsurePreviewRender(entity);
                    SetPreviewRenderTime(newTime);
                }
            }

            if (_suppressAutoPreviewRebuildAfterManualCleanup && _previewRenderPlayer == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("已手动清理实体预览对象，当前不会自动重建。需要预览时请点击“重建”或“播放”。", UnityEditor.MessageType.Info);
                return;
            }

            EnsurePreviewRender(entity);
            DrawPreviewRenderArea(entity);
        }

        private void OnPreviewTrackSelected(object userData)
        {
            _previewTrackContainer = userData as IEditorTrackSupport_GetSequence;
            _previewTrackTime = 0f;
            _suppressAutoPreviewRebuildAfterManualCleanup = false;

            Entity entity = ResolveEditorPreviewEntity();
            if (_previewTrackContainer != null && entity != null)
                RebuildPreviewRender(entity, 0f);
        }

        private void EnsurePreviewTrackAutoSelected(bool forceProjectScan = false)
        {
            if (_previewTrackContainer != null && _previewTrackContainer.Sequence != null && !forceProjectScan)
                return;

            if (TryResolveTrackContainerFromSelection(out var selected))
            {
                _previewTrackContainer = selected;
                _previewTrackTime = 0f;
                return;
            }

            if (_previewTrackAutoScanned && !forceProjectScan)
                return;

            _previewTrackAutoScanned = true;
            if (TryResolveFirstTrackContainerInProject(out var projectOne))
            {
                _previewTrackContainer = projectOne;
                _previewTrackTime = 0f;
            }
        }

        private static bool TryResolveTrackContainerFromSelection(out IEditorTrackSupport_GetSequence result)
        {
            result = null;
            UnityEngine.Object selected = UnityEditor.Selection.activeObject;

            if (selected is IEditorTrackSupport_GetSequence direct && IsUsableTrackContainer(direct))
            {
                result = direct;
                return true;
            }

            if (selected is GameObject go)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IEditorTrackSupport_GetSequence support && IsUsableTrackContainer(support))
                    {
                        result = support;
                        return true;
                    }
                }
            }

            if (selected is Component component && component is IEditorTrackSupport_GetSequence componentSupport && IsUsableTrackContainer(componentSupport))
            {
                result = componentSupport;
                return true;
            }

            return false;
        }

        private static bool TryResolveFirstTrackContainerInProject(out IEditorTrackSupport_GetSequence result)
        {
            result = null;
            List<IEditorTrackSupport_GetSequence> all = null;
            try
            {
                all = ESDesignUtility.SafeEditor.FindAllSOAssets<IEditorTrackSupport_GetSequence>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (all == null)
                return false;

            for (int i = 0; i < all.Count; i++)
            {
                if (!IsUsableTrackContainer(all[i]))
                    continue;

                result = all[i];
                return true;
            }

            return false;
        }

        private static bool IsUsableTrackContainer(IEditorTrackSupport_GetSequence container)
        {
            return container != null && container.Sequence != null;
        }

        private Entity ResolveEditorPreviewEntity()
        {
            if (MyCore != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(MyCore);
                return MyCore;
            }

            GameObject selected = UnityEditor.Selection.activeGameObject;
            if (selected == null && UnityEditor.Selection.activeObject is Component component)
                selected = component.gameObject;

            if (selected == null)
                return EditorRememberedEntityTarget.StatePreview.ResolveOrSceneFallback();

            Entity entity = selected.GetComponent<Entity>();
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            entity = selected.GetComponentInParent<Entity>();
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            entity = selected.GetComponentInChildren<Entity>(true);
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            return EditorRememberedEntityTarget.StatePreview.ResolveOrSceneFallback();
        }

        private void EnsurePreviewRender(Entity sourceEntity)
        {
            if (_suppressAutoPreviewRebuildAfterManualCleanup)
                return;

            if (_previewCamera != null
                && _previewRenderRoot != null
                && _previewRenderEntity != null
                && _previewRenderPlayer != null)
                return;

            RebuildPreviewRender(sourceEntity, _previewTrackTime);
        }

        private void RebuildPreviewRender(Entity sourceEntity, float startTime)
        {
            _suppressAutoPreviewRebuildAfterManualCleanup = false;
            StopPreviewPlaybackState();
            DisposePreviewRender();
            CleanupLingeringPreviewObjects();

            if (sourceEntity == null || _previewTrackContainer == null || _previewTrackContainer.Sequence == null)
                return;

            _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview temporary resource.");
            CreateUrpPreviewCameraRig();

            _previewRenderRoot = UnityEngine.Object.Instantiate(sourceEntity.gameObject);
            _previewRenderRoot.name = $"{sourceEntity.name}{PreviewRootSuffix}";
            _previewResourceScope.RegisterGameObject(_previewRenderRoot, recursiveHideFlags: true);
            _previewRenderRoot.SetActive(true);
            NormalizePreviewRootTransform(_previewRenderRoot.transform);
            SetLayerRecursive(_previewRenderRoot.transform, PreviewRenderLayer);

            _previewRenderEntity = _previewRenderRoot.GetComponent<Entity>();
            if (_previewRenderEntity == null)
                _previewRenderEntity = _previewRenderRoot.GetComponentInChildren<Entity>(true);

            CopyPreviewRendererState(sourceEntity.gameObject, _previewRenderRoot);
            DisablePreviewBehaviours(_previewRenderRoot);
            EnsurePreviewAnimatorEnabled(_previewRenderRoot);
            EnsurePreviewRenderers(_previewRenderRoot);
            ApplyPreviewIdlePose(_previewRenderRoot);
            AlignPreviewRootToGround(_previewRenderRoot);

            _previewRenderPlayer = CreatePreviewSequencePlayer(_previewRenderEntity, _previewTrackContainer.Sequence, _previewTrackContainer.trackName);
            _previewRenderPlayer.StartAllSamplers();
            _previewTrackTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0.01f, _previewRenderPlayer.Duration));
            _previewRenderCompleted = false;
            _previewRenderPlayer.SetPreviewIdleWeight(1f);
            AlignPreviewRootToGround(_previewRenderRoot);
            CachePreviewViewBounds(_previewRenderRoot);
        }

        private void DrawPreviewRenderArea(Entity sourceEntity)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, PreviewRenderHeight, GUILayout.ExpandWidth(true));
            if (_previewCamera == null || _previewRenderRoot == null || _previewRenderEntity == null)
            {
                UnityEditor.EditorGUI.HelpBox(rect, "URP 预览相机没有创建。请点击“重建”。", UnityEditor.MessageType.Info);
                return;
            }

            if (!_previewRenderPlaying)
                MaintainPreviewIdlePlayable();

            HandlePreviewInput(rect);
            Bounds bounds = CalculatePreviewBounds(_previewRenderRoot);
            if (_previewAutoFitView)
                CachePreviewViewBounds(bounds);
            DrawPreviewTexture(rect, bounds);
        }

        private void UpdatePreviewPlayback()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min(0.1f, (float)(now - _previewRenderLastTime));
            _previewRenderLastTime = now;
            UpdatePreviewPlayback(deltaTime, repaintAllViews: true);
        }

        private void UpdatePreviewPlayback(float deltaTime, bool repaintAllViews)
        {
            if (!_previewRenderPlaying || _previewRenderPlayer == null)
                return;

            float nextTime = _previewRenderPlayer.CurrentTime + deltaTime * Mathf.Max(0.01f, _previewRenderPlayer.Speed);
            if (nextTime >= _previewRenderPlayer.Duration)
            {
                nextTime = _previewRenderPlayer.Duration;
                _previewTrackTime = nextTime;
                StopPreviewPlaybackToIdle(resetTime: false, completed: true);
                if (repaintAllViews)
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                return;
            }

            SetPreviewRenderTime(nextTime);
            if (repaintAllViews)
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void SetPreviewRenderTime(float time)
        {
            if (_previewRenderPlayer == null)
                return;

            _previewTrackTime = Mathf.Clamp(time, 0f, Mathf.Max(0.01f, _previewRenderPlayer.Duration));
            _previewRenderPlayer.SetTime(_previewTrackTime);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void MaintainPreviewIdlePlayable()
        {
            if (_previewRenderPlayer == null || _previewRenderRoot == null)
                return;

            _previewRenderPlayer.SetPreviewIdleWeight(1f);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void StartPreviewPlayback(Entity entity, float duration)
        {
            float startTime = _previewTrackTime;
            if (_previewRenderCompleted
                || startTime >= Mathf.Max(0f, duration - 0.0001f)
                || (_previewRenderPlayer != null && _previewRenderPlayer.CurrentTime >= Mathf.Max(0f, duration - 0.0001f)))
                startTime = 0f;

            RebuildPreviewRender(entity, startTime);
            if (_previewRenderPlayer == null)
                return;

            StopPreviewAnimationMode();

            _previewRenderCompleted = false;
            _previewRenderPlayer.UsePreviewIdleAutoBlend();
            _previewRenderPlayer.SetTime(_previewTrackTime);
            _previewRenderPlayer.Play();
            _previewRenderPlaying = true;
            _previewRenderLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void StopPreviewPlaybackToIdle(bool resetTime, bool completed)
        {
            _previewRenderPlaying = false;
            _previewRenderCompleted = completed;
            UnregisterPreviewRenderUpdate();

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.Pause();
                _previewRenderPlayer.StopAllSamplers();
                if (resetTime)
                    _previewTrackTime = 0f;
            }

            ApplyPreviewIdlePose(_previewRenderRoot);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void RegisterPreviewRenderUpdate()
        {
            if (_previewRenderUpdateRegistered)
                return;

            UnityEditor.EditorApplication.update -= OnPreviewRenderEditorUpdate;
            UnityEditor.EditorApplication.update += OnPreviewRenderEditorUpdate;
            _previewRenderUpdateRegistered = true;
        }

        private void UnregisterPreviewRenderUpdate()
        {
            UnityEditor.EditorApplication.update -= OnPreviewRenderEditorUpdate;
            _previewRenderUpdateRegistered = false;
        }

        private void OnPreviewRenderEditorUpdate()
        {
            if (!_previewRenderPlaying || _previewRenderPlayer == null || _previewRenderRoot == null)
            {
                UnregisterPreviewRenderUpdate();
                return;
            }

            UpdatePreviewPlayback();
        }

        private void StopPreviewPlaybackState()
        {
            _previewRenderPlaying = false;
            _previewRenderCompleted = false;
            UnregisterPreviewRenderUpdate();

            if (_previewRenderPlayer == null)
                return;

            _previewRenderPlayer.Pause();
            _previewRenderPlayer.StopAllSamplers();
        }

        private static void StopPreviewAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private EditorSequencePlayer CreatePreviewSequencePlayer(Entity previewEntity, ITrackSequence sequence, string sequenceName)
        {
            var seqPlayer = new EditorSequencePlayer
            {
                Name = string.IsNullOrEmpty(sequenceName) ? "实体预览序列" : sequenceName,
                Duration = Mathf.Max(0.01f, GetSequenceDuration(sequence)),
                Speed = Mathf.Max(0.05f, _previewPlaybackSpeed)
            };

            EditorRememberedEntityTarget.StatePreview.FillPreviewTarget(seqPlayer.PreviewTarget, previewEntity);

            if (sequence != null && sequence.Tracks != null)
            {
                foreach (var track in sequence.Tracks)
                {
                    if (track == null || !track.Enabled)
                        continue;

                    List<IEditorTimeSampler> samplers = null;
                    try
                    {
                        samplers = track.CreateEditorSamplers(sequence, seqPlayer.PreviewTarget);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EntityStateDomain Preview Render] CreateEditorSamplers failed. Track={track.DisplayName}, Type={track.GetType().Name}");
                        Debug.LogException(e);
                    }

                    if (samplers == null)
                        continue;

                    for (int i = 0; i < samplers.Count; i++)
                        seqPlayer.RegisterSampler(samplers[i]);
                }
            }

            seqPlayer.OnTimeUpdated += time => _previewTrackTime = time;
            return seqPlayer;
        }

        private void DrawPreviewTexture(Rect rect, Bounds bounds)
        {
            if (!TryBuildPreviewRects(rect, out Rect displayRect, out Rect renderRect))
            {
                UnityEditor.EditorGUI.HelpBox(rect, "预览区域尺寸无效，本帧跳过渲染。", UnityEditor.MessageType.Warning);
                return;
            }

            Camera camera = _previewCamera;
            if (camera == null)
                return;

            if (!EnsurePreviewRenderTexture(renderRect))
            {
                UnityEditor.EditorGUI.HelpBox(displayRect, "URP 预览 RenderTexture 创建失败。", UnityEditor.MessageType.Warning);
                return;
            }

            Vector3 center = _previewAutoFitView ? bounds.center : _previewViewCenter;
            float radius = _previewAutoFitView ? Mathf.Max(0.5f, bounds.extents.magnitude) : Mathf.Max(0.5f, _previewViewRadius);
            Quaternion rotation = Quaternion.Euler(_previewRenderOrbit.x, _previewRenderOrbit.y, 0f);
            float halfFov = Mathf.Max(1f, camera.fieldOfView) * 0.5f * Mathf.Deg2Rad;
            float fitDistance = radius / Mathf.Sin(halfFov);
            float distance = fitDistance * 1.15f * Mathf.Clamp(_previewZoom, 0.25f, 4f);
            Vector3 cameraOffset = rotation * (Vector3.back * distance);

            camera.transform.position = center + cameraOffset;
            camera.transform.LookAt(center, Vector3.up);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2.2f);
            camera.farClipPlane = distance + radius * 2.2f;
            camera.cullingMask = 1 << PreviewRenderLayer;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            camera.targetTexture = _previewRenderTexture;

            camera.Render();
            GUI.DrawTexture(displayRect, _previewRenderTexture, ScaleMode.StretchToFill, false);
        }

        private static bool TryBuildPreviewRects(Rect layoutRect, out Rect displayRect, out Rect renderRect)
        {
            displayRect = layoutRect;
            renderRect = default;

            if (!IsFinite(layoutRect.x) || !IsFinite(layoutRect.y) || !IsFinite(layoutRect.width) || !IsFinite(layoutRect.height))
                return false;

            if (layoutRect.width < 2f || layoutRect.height < 2f)
                return false;

            float viewWidth = UnityEditor.EditorGUIUtility.currentViewWidth;
            float safeDisplayWidth = IsFinite(viewWidth) && viewWidth > 32f
                ? Mathf.Min(layoutRect.width, Mathf.Max(32f, viewWidth - layoutRect.x - 8f))
                : Mathf.Min(layoutRect.width, PreviewRenderTextureMaxSize);

            displayRect.width = Mathf.Clamp(safeDisplayWidth, 16f, PreviewRenderTextureMaxSize);
            displayRect.height = Mathf.Clamp(layoutRect.height, 16f, PreviewRenderHeight);
            renderRect = new Rect(0f, 0f, displayRect.width, displayRect.height);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void CreateUrpPreviewCameraRig()
        {
            if (_previewCameraRoot != null && _previewCamera != null)
                return;

            _previewCameraRoot = new GameObject(PreviewCameraName);
            if (_previewResourceScope == null)
                _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview camera rig.");

            _previewResourceScope.RegisterGameObject(_previewCameraRoot);

            _previewCamera = _previewCameraRoot.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = 1000f;
            _previewCamera.clearFlags = CameraClearFlags.Color;
            _previewCamera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            _previewCamera.cullingMask = 1 << PreviewRenderLayer;
            _previewCamera.allowHDR = true;
            _previewCamera.allowMSAA = true;
            _previewCamera.renderingPath = RenderingPath.Forward;

            AddAndConfigureUniversalCameraData(_previewCamera);

            _previewKeyLight = CreatePreviewDirectionalLight(PreviewKeyLightName, 1.8f, Quaternion.Euler(35f, 35f, 0f));
            _previewFillLight = CreatePreviewDirectionalLight(PreviewFillLightName, 1.0f, Quaternion.Euler(340f, 218f, 177f));
        }

        private static void AddAndConfigureUniversalCameraData(Camera camera)
        {
            if (camera == null)
                return;

            Type cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType == null)
            {
                Debug.LogWarning("[EntityStateDomain Preview] URP package is required for this preview camera.");
                return;
            }

            Component cameraData = camera.GetComponent(cameraDataType);
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent(cameraDataType);

            SetBoolPropertyIfExists(cameraData, "renderPostProcessing", false);
            SetBoolPropertyIfExists(cameraData, "renderShadows", true);
            SetEnumPropertyIfExists(cameraData, "renderType", "Base");
            SetEnumPropertyIfExists(cameraData, "requiresDepthOption", "Off");
            SetEnumPropertyIfExists(cameraData, "requiresColorOption", "Off");
            SetEnumPropertyIfExists(cameraData, "antialiasing", "None");
        }

        private static void SetBoolPropertyIfExists(object target, string propertyName, bool value)
        {
            if (target == null)
                return;

            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite || property.PropertyType != typeof(bool))
                return;

            property.SetValue(target, value, null);
        }

        private static void SetEnumPropertyIfExists(object target, string propertyName, string enumName)
        {
            if (target == null)
                return;

            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            try
            {
                property.SetValue(target, Enum.Parse(property.PropertyType, enumName), null);
            }
            catch
            {
                // URP enum names can vary by version. Keep the package default if a value is missing.
            }
        }

        private Light CreatePreviewDirectionalLight(string name, float intensity, Quaternion rotation)
        {
            GameObject lightRoot = new GameObject(name);
            if (_previewResourceScope == null)
                _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview light.");

            _previewResourceScope.RegisterGameObject(lightRoot);
            lightRoot.transform.rotation = rotation;

            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private bool EnsurePreviewRenderTexture(Rect renderRect)
        {
            int width = Mathf.Clamp(Mathf.CeilToInt(renderRect.width * PreviewRenderScale), 16, (int)PreviewRenderTextureMaxSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(renderRect.height * PreviewRenderScale), 16, (int)PreviewRenderTextureMaxSize);
            if (_previewRenderTexture != null
                && _previewRenderTexture.width == width
                && _previewRenderTexture.height == height)
                return _previewRenderTexture.IsCreated() || _previewRenderTexture.Create();

            ReleasePreviewRenderTexture();

            _previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ES_URP_EntityPreviewRT",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _previewResourceScope?.RegisterRenderTexture(_previewRenderTexture);
            return _previewRenderTexture.Create();
        }

        private void ReleasePreviewRenderTexture()
        {
            if (_previewRenderTexture == null)
                return;

            if (_previewCamera != null && _previewCamera.targetTexture == _previewRenderTexture)
                _previewCamera.targetTexture = null;

            _previewRenderTexture.Release();
            UnityEngine.Object.DestroyImmediate(_previewRenderTexture);
            _previewRenderTexture = null;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                _previewRenderOrbit.y += evt.delta.x;
                _previewRenderOrbit.x = Mathf.Clamp(_previewRenderOrbit.x + evt.delta.y, -80f, 80f);
                evt.Use();
            }
            else if (evt.type == EventType.ScrollWheel)
            {
                _previewZoom = Mathf.Clamp(_previewZoom * (1f + evt.delta.y * 0.08f), 0.25f, 4f);
                evt.Use();
            }
        }

        private void CachePreviewViewBounds(GameObject root)
        {
            if (root == null)
                return;

            CachePreviewViewBounds(CalculatePreviewBounds(root));
        }

        private void CachePreviewViewBounds(Bounds bounds)
        {
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return;

            _previewViewCenter = bounds.center;
            _previewViewRadius = Mathf.Max(0.5f, bounds.extents.magnitude);
        }

        private static Bounds CalculatePreviewBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);

            if (TryCalculatePreviewBounds(renderers, renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer, out Bounds meshBounds))
                return meshBounds;

            if (TryCalculatePreviewBounds(renderers, renderer => true, out Bounds rendererBounds))
                return rendererBounds;

            return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
        }

        private static bool TryCalculatePreviewBounds(Renderer[] renderers, Predicate<Renderer> filter, out Bounds bounds)
        {
            bool hasValidRenderer = false;
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (filter != null && !filter(renderer))
                    continue;

                Bounds rendererBounds = renderer.bounds;
                if (!IsFinite(rendererBounds.center) || !IsFinite(rendererBounds.extents))
                    continue;

                if (!hasValidRenderer)
                {
                    bounds = rendererBounds;
                    hasValidRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasValidRenderer;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                   && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private void DisposePreviewRender()
        {
            UnregisterPreviewRenderUpdate();
            _previewRenderPlaying = false;
            _previewRenderCompleted = false;
            StopPreviewAnimationMode();

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.Pause();
                _previewRenderPlayer.StopAllSamplers();
                _previewRenderPlayer.DisposeEditorPreviewTarget();
                _previewRenderPlayer = null;
            }

            if (_previewCamera != null && _previewCamera.targetTexture == _previewRenderTexture)
                _previewCamera.targetTexture = null;

            _previewResourceScope?.Dispose();
            _previewResourceScope = null;
            _previewRenderRoot = null;
            _previewRenderEntity = null;
            _previewRenderTexture = null;
            _previewCameraRoot = null;
            _previewCamera = null;
            _previewKeyLight = null;
            _previewFillLight = null;

            CleanupLingeringPreviewObjects();
        }

        private static void CleanupLingeringPreviewObjects()
        {
            CleanupLingeringPreviewObjectsInternal();
        }

        private static int CleanupLingeringPreviewObjectsInternal()
        {
            int removed = 0;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (!IsLingeringPreviewObject(obj))
                    continue;

                if (DestroyPreviewObjectImmediate(obj))
                    removed++;
            }

            return removed;
        }

        private static bool IsLingeringPreviewObject(GameObject obj)
        {
            if (obj == null || UnityEditor.EditorUtility.IsPersistent(obj))
                return false;

            if (obj.GetComponent<EditorPreviewGameObjectSign>() != null)
                return true;

            string objectName = obj.name;
            if (IsPreviewObjectName(objectName))
                return true;

            return obj.layer == PreviewRenderLayer && HasPreviewHideFlags(obj.hideFlags);
        }

        private static bool DestroyPreviewObjectImmediate(GameObject obj)
        {
            if (obj == null)
                return false;

            Transform root = obj.transform;
            while (root.parent != null && IsLingeringPreviewObject(root.parent.gameObject))
                root = root.parent;

            if (root == null || root.gameObject == null)
                return false;

            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return true;
        }

        private static bool IsPreviewObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName == PreviewCameraName
                   || objectName == PreviewKeyLightName
                   || objectName == PreviewFillLightName
                   || objectName.StartsWith(PreviewCameraName + " ", StringComparison.Ordinal)
                   || objectName.StartsWith(PreviewKeyLightName + " ", StringComparison.Ordinal)
                   || objectName.StartsWith(PreviewFillLightName + " ", StringComparison.Ordinal)
                   || objectName.Contains(PreviewRootSuffix);
        }

        private static bool HasPreviewHideFlags(HideFlags hideFlags)
        {
            return (hideFlags & HideFlags.HideInHierarchy) != 0
                   || (hideFlags & HideFlags.HideInInspector) != 0
                   || (hideFlags & HideFlags.DontSaveInEditor) != 0
                   || (hideFlags & HideFlags.DontSaveInBuild) != 0
                   || (hideFlags & HideFlags.DontUnloadUnusedAsset) != 0;
        }

        private static void MarkPreviewObject(GameObject obj)
        {
            if (obj == null)
                return;

            ESEditorPreviewResourceScope.MarkPreviewObject(obj, PreviewResourceOwner, "Entity editor preview temporary object.");
        }

        private static void SetHideFlagsRecursive(Transform root, HideFlags flags)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = flags;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsRecursive(root.GetChild(i), flags);
        }

        private static void NormalizePreviewRootTransform(Transform root)
        {
            if (root == null)
                return;

            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private static void AlignPreviewRootToGround(GameObject root)
        {
            if (root == null)
                return;

            Bounds bounds = CalculatePreviewBounds(root);
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return;

            float yOffset = -bounds.min.y;
            if (Mathf.Abs(yOffset) <= 0.0001f)
                return;

            root.transform.position += Vector3.up * yOffset;
        }

        private void ApplyPreviewIdlePose(GameObject root)
        {
            if (root == null)
                return;

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.SetPreviewIdleWeight(1f);
                return;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                animator.Rebind();
                animator.Update(0f);
            }

            AnimationClip idleClip = null;
            StateMachineConfig config = StateMachineConfig.Instance;
            if (config != null)
                idleClip = config.previewIdleClip;

            if (idleClip == null)
                return;

            _previewRenderPlayer?.SetPreviewIdleWeight(1f);

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].Update(0f);
            }
        }

        private static void DisablePreviewBehaviours(GameObject root)
        {
            if (root == null)
                return;

            var behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour is Animator)
                    continue;

                behaviour.enabled = false;
            }
        }

        private static void EnsurePreviewAnimatorEnabled(GameObject root)
        {
            if (root == null)
                return;

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                animators[i].enabled = true;
                animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private static void EnsurePreviewRenderers(GameObject root)
        {
            if (root == null)
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials != null && sharedMaterials.Length > 0)
                    renderer.sharedMaterials = sharedMaterials;
            }
        }

        private static void CopyPreviewRendererState(GameObject sourceRoot, GameObject previewRoot)
        {
            if (sourceRoot == null || previewRoot == null)
                return;

            var sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            var previewRenderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers == null || previewRenderers == null)
                return;

            var previewMap = new Dictionary<string, Renderer>(previewRenderers.Length);
            for (int i = 0; i < previewRenderers.Length; i++)
            {
                Renderer previewRenderer = previewRenderers[i];
                if (previewRenderer == null)
                    continue;

                string key = GetRendererPathKey(previewRoot.transform, previewRenderer);
                if (!previewMap.ContainsKey(key))
                    previewMap.Add(key, previewRenderer);
            }

            var propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                Renderer sourceRenderer = sourceRenderers[i];
                if (sourceRenderer == null)
                    continue;

                string key = GetRendererPathKey(sourceRoot.transform, sourceRenderer);
                if (!previewMap.TryGetValue(key, out Renderer previewRenderer) || previewRenderer == null)
                    continue;

                previewRenderer.enabled = sourceRenderer.enabled;
                previewRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                previewRenderer.receiveShadows = sourceRenderer.receiveShadows;
                previewRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                previewRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                previewRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

                sourceRenderer.GetPropertyBlock(propertyBlock);
                previewRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private static string GetRendererPathKey(Transform root, Renderer renderer)
        {
            if (root == null || renderer == null)
                return string.Empty;

            Transform current = renderer.transform;
            string path = renderer.GetType().FullName;
            while (current != null && current != root)
            {
                path = current.GetSiblingIndex() + "/" + current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static float GetSequenceDuration(ITrackSequence sequence)
        {
            float duration = 0f;
            if (sequence == null || sequence.Tracks == null)
                return 0.01f;

            foreach (var track in sequence.Tracks)
            {
                if (track == null || track.Clips == null)
                    continue;

                foreach (var clip in track.Clips)
                {
                    if (clip == null)
                        continue;

                    duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.DurationTime));
                }
            }

            return Mathf.Max(0.01f, duration);
        }
        private void EditorPreviewDrawPreviewGUIImpl()
        {
            var sm = stateMachine;
            if (sm == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("没有 StateMachine", UnityEditor.MessageType.Warning);
                return;
            }

            UnityEditor.EditorGUILayout.LabelField("状态机运行时预览", UnityEditor.EditorStyles.boldLabel);
            var layers = sm.LayerRuntimes;
            if (layers == null) return;

            foreach (var layer in layers)
            {
                if (layer == null) continue;

                if (!layerFoldouts.ContainsKey(layer.layerType))
                    layerFoldouts[layer.layerType] = true;

                int runningCount = layer.runningStates.Count;
                int slotCount = layer.stateToSlotMap.Count;
                bool foldout = UnityEditor.EditorGUILayout.Foldout(
                    layerFoldouts[layer.layerType],
                    $"Layer: {layer.layerType}  (Weight: {layer.weight:F2})  Running: {runningCount}/{slotCount}",
                    true);
                layerFoldouts[layer.layerType] = foldout;
                if (!foldout) continue;

                UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);

                foreach (var kvp in layer.stateToSlotMap)
                {
                    var state = kvp.Key;
                    int slot = kvp.Value;
                    if (state == null) continue;

                    // 鏉冮噸
                    float weight = 0f;
                    if (layer.mixer.IsValid() && slot >= 0 && slot < layer.mixer.GetInputCount())
                        weight = layer.mixer.GetInputWeight(slot);

                    bool isActive = layer.runningStates.Contains(state);
                    bool isFadingIn = layer.fadeInStates.ContainsKey(state);
                    bool isFadingOut = layer.fadeOutStates.ContainsKey(state);

                    // 褰撳墠 RuntimePhase
                    var phase = state.RuntimePhase;

                    // ---- 鐘舵€佸悕绉版牱寮?----
                    var nameStyle = new GUIStyle(UnityEditor.EditorStyles.label);
                    if (isActive) nameStyle.fontStyle = FontStyle.Bold;
                    if (isFadingIn)
                        nameStyle.normal.textColor = Color.Lerp(Color.white, Color.green, 0.7f);
                    else if (isFadingOut)
                        nameStyle.normal.textColor = Color.Lerp(Color.white, Color.red, 0.6f);

                    UnityEditor.EditorGUILayout.BeginHorizontal();

                    // 鈶?鐘舵€佸悕绉帮紙鍚?Tooltip 鏄剧ず婵€娲绘椂闂寸瓑锛?
                    string tooltip = $"激活时间: {state.activationTime:F2}\n持续时间: {state.hasEnterTime:F2}s";
                    GUILayout.Label(new GUIContent(state.strKey ?? state.GetType().Name, tooltip), nameStyle, GUILayout.Width(140));

                    // 鈶?RuntimePhase 鏍囩锛堝僵鑹诧級
                    Color phaseColor = phase switch
                    {
                        StateRuntimePhase.Pre => new Color(0.4f, 0.8f, 1f),     // 娣¤摑
                        StateRuntimePhase.Main => new Color(0.3f, 0.9f, 0.3f),   // 缁胯壊
                        StateRuntimePhase.Wait => new Color(1f, 0.8f, 0.2f),     // 榛勮壊
                        StateRuntimePhase.Released => new Color(0.5f, 0.5f, 0.5f), // 鐏拌壊
                        _ => Color.white
                    };
                    var phaseStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = phaseColor },
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUILayout.Label(phase.ToString(), phaseStyle, GUILayout.Width(55));

                    // 鈶?鏉冮噸鏁板€?
                    UnityEditor.EditorGUILayout.LabelField($"{weight:F2}", GUILayout.Width(35));

                    // 鈶?鍔ㄦ€佽繘搴︽潯锛堝じ寮犻暱搴﹀彉鍖栵級
                    float maxBarWidth = Mathf.Max(40, UnityEditor.EditorGUIUtility.currentViewWidth - 290);
                    float barWidth = Mathf.Lerp(4f, maxBarWidth, weight);
                    var barRect = UnityEditor.EditorGUILayout.GetControlRect(
                        GUILayout.Width(barWidth), GUILayout.Height(18));

                    // 榛戣壊鑳屾櫙
                    UnityEditor.EditorGUI.DrawRect(barRect, Color.black);

                    // 濉厖鑹?
                    float fillFactor = Mathf.Clamp01(weight);
                    var fillRect = new Rect(barRect.x, barRect.y, barRect.width, barRect.height);
                    Color lowColor = new Color(0.1f, 0.1f, 0.1f);
                    Color highColor = new Color(0.2f, 1f, 0.2f);
                    Color fillColor = Color.Lerp(lowColor, highColor, fillFactor);
                    UnityEditor.EditorGUI.DrawRect(fillRect, fillColor);

                    // 鍏夋檿
                    if (weight > 0.1f)
                    {
                        var glowRect = new Rect(barRect.x, barRect.y, barRect.width, 4);
                        UnityEditor.EditorGUI.DrawRect(glowRect, new Color(1f, 1f, 1f, weight * 0.5f));
                    }

                    // 鐧借竟妗?
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + barRect.height - 1, barRect.width, 1), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, 1, barRect.height), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x + barRect.width - 1, barRect.y, 1, barRect.height), Color.white);

                    // 灞呬腑鐧惧垎姣?
                    GUIStyle percentStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
                    {
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUI.Label(barRect, $"{weight:P0}", percentStyle);

                    // 鈶?娣″叆/娣″嚭鏍囩
                    if (isFadingIn)
                        GUILayout.Label("娣″叆", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
                    else if (isFadingOut)
                        GUILayout.Label("娣″嚭", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
                    else
                        GUILayout.Space(35); // 瀵归綈

                    UnityEditor.EditorGUILayout.EndHorizontal();
                }

                UnityEditor.EditorGUILayout.EndVertical();
            }
        }
#endif

        #endregion

    }
}
