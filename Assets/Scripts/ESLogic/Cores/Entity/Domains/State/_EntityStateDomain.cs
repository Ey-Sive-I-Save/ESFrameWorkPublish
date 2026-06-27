

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
    [Serializable, TypeRegistryItem("状态域")]
    public class EntityStateDomain : Domain<Entity, EntityStateModuleBase>, IPreviewElement
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

        [Title("状态数据")]
        [LabelText("动画状态数据包")]
        public StateAniDataPack stateAniDataPack;

        [LabelText("枪械状态数据包")]
        public GunStateAniDataPack gunStateAniDataPack;

        [LabelText("额外状态数据包")]
        public List<StateAniDataPack> additionalStateAniDataPacks = new List<StateAniDataPack>();

        [Title("状态机")]
        [LabelText("状态机")]
        public StateMachine stateMachine = new StateMachine();

        [LabelText("设定默认状态键(可为空)")]
        public string defaultStateKey = "";

        [LabelText("初始激活状态名（可为空）")]
        [Tooltip("状态机启动后自动激活的状态，留空则不自动激活")]
        public string initialStateName = "";

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
            // 必须先初始化StateMachine（创建层级等基础设施），再初始化StateAniDataPack（注册状态）
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

            // 确保默认状态在注册完成后可被激活
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
        /// 批量注册状态（从Info列表）
        /// </summary>
        /// <param name="infos">状态Info集合</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态键</param>
        /// <returns>成功注册的状态数量</returns>
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
        /// 注册单个状态（从Info）- 纯粹委托给StateMachine
        /// </summary>
        /// <param name="info">状态Info</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态键</param>
        /// <returns>成功返回 StateBase，失败返回 null</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            if (stateMachine == null)
            {
                Debug.LogError("[StateDomain] 状态机未初始化，无法注册状态");
                return null;
            }

            // 直接委托给StateMachine处理所有逻辑（初始化、键冲突、注册）
            var state = stateMachine.RegisterStateFromInfo(info, allowOverride);

            // 注册成功后缓存Info（用于Domain层管理）
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
                    "[StateDomain] InitializeStateMachine 已跳过：MyCore 为空，状态机不会初始化，StateFinalIKDriver 也不会被绑定启用。");
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
                    $"[StateDomain] InitializeStateMachine 已跳过：{entityName}.animator 为空，StateMachine.BindToAnimator 未执行，StateFinalIKDriver 将保持禁用。请先给 Entity.animator 正确赋值。"
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

            // 6. 尝试激活初始状态
            if (!string.IsNullOrEmpty(initialStateName))
            {
                // TODO: 等待状态转换逻辑验证后启用
                // bool activated = stateMachine.TryEnterState(stateMachine.GetStateByStringKey(initialStateName));
                // if (activated)
                // {
                //     Debug.Log($"[StateDomain] 激活初始状态: {initialStateName}");
                // }
                // else
                // {
                //     Debug.LogWarning($"[StateDomain] 无法激活初始状态: {initialStateName}");
                // }

                Debug.Log($"[StateDomain] 初始状态已配置: {initialStateName}（待状态转换验证后启用）");
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
                Debug.LogWarning("[StateDomain] 请提供有效的StateAniDataInfo");
                return;
            }

            var state = RegisterStateFromInfo(info, allowOverride);
            if (state != null)
            {
                Debug.Log($"[StateDomain] 动态注册成功: {info.sharedData.basicConfig.stateName}");
            }
        }

        [Button("重新注册所有状态（覆盖）"), FoldoutGroup("状态注册")]
        public void TestReregisterAllStates()
        {
            CollectPackSources(_workingPackSources);
            if (_workingPackSources.Count == 0)
            {
                Debug.LogWarning("[StateDomain] 状态数据包为空");
                return;
            }

            int count = RegisterStatesFromPacks(_workingPackSources, allowOverride: true);
            Debug.Log($"[StateDomain] 重新注册完成，共 {count} 个状态");
        }

        [Button("测试激活状态(String)"), FoldoutGroup("测试功能")]
        public void TestActivateStateByString(string stateKey)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 状态机未初始化");
                return;
            }

            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogWarning("[StateDomain] 状态键不能为空");
                return;
            }

            var state = stateMachine.GetStateByString(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"[StateDomain] 未找到状态: {stateKey}");
                return;
            }

            bool success = stateMachine.TryActivateState(stateKey);
            if (success)
            {
                Debug.Log($"[StateDomain] 成功激活状态: {stateKey} (IntKey:{state.intKey})");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 激活状态失败: {stateKey}");
            }
        }

        [Button("测试激活状态(Int)"), FoldoutGroup("测试功能")]
        public void TestActivateStateByInt(int stateId)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 状态机未初始化");
                return;
            }

            var state = stateMachine.GetStateByInt(stateId);
            if (state == null)
            {
                Debug.LogWarning($"[StateDomain] 未找到状态ID: {stateId}");
                return;
            }

            bool success = stateMachine.TryActivateState(stateId);
            if (success)
            {
                Debug.Log($"[StateDomain] 成功激活状态: {state.strKey} (IntKey:{stateId})");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 激活状态失败: {stateId}");
            }
        }

        [Button("测试关闭状态(String)"), FoldoutGroup("测试功能")]
        public void TestDeactivateStateByString(string stateKey)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 状态机未初始化");
                return;
            }

            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogWarning("[StateDomain] 状态键不能为空");
                return;
            }

            bool success = stateMachine.TryDeactivateState(stateKey);
            if (success)
            {
                Debug.Log($"[StateDomain] 成功关闭状态: {stateKey}");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 关闭状态失败: {stateKey}");
            }
        }

        [Button("测试关闭状态(Int)"), FoldoutGroup("测试功能")]
        public void TestDeactivateStateByInt(int stateId)
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 状态机未初始化");
                return;
            }

            bool success = stateMachine.TryDeactivateState(stateId);
            if (success)
            {
                Debug.Log($"[StateDomain] 成功关闭状态ID: {stateId}");
            }
            else
            {
                Debug.LogWarning($"[StateDomain] 关闭状态失败: {stateId}");
            }
        }

        [Button("列出所有状态"), FoldoutGroup("测试功能")]
        public void TestListAllStates()
        {
            if (!_stateMachineInitialized)
            {
                Debug.LogWarning("[StateDomain] 状态机未初始化");
                return;
            }

            // ── 构建完整文本（控制台 + 对话框 + 剪贴板共用同一数据源）────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 状态机所有状态 ({stateMachine.RegisteredStateCount}) ===");

            foreach (var kvp in stateMachine.EnumerateRegisteredStatesByKey())
            {
                var state = kvp.Value;
                var layerType = stateMachine.TryGetStateLayerType(state, out var layer)
                    ? layer.ToString() : "Unknown";
                var isRunning = stateMachine.IsStateRunning(state);
                var isFallback = state.stateSharedData?.basicConfig?.canBeFeedback ?? false;

                sb.AppendLine($"  [{layerType}] {kvp.Key} (IntKey:{state.intKey}) - " +
                              $"运行:{isRunning}, Fallback:{isFallback}");
            }

            // ── 控制台输出 ────────────────────────────────────────────────────────
            Debug.Log(sb.ToString());

#if UNITY_EDITOR
            // ── 复制到系统剪贴板（可直接粘贴使用） ───────────────────────────────
            UnityEditor.EditorGUIUtility.systemCopyBuffer = sb.ToString();

            // ── 弹出编辑器对话框预览（内容已在剪贴板，可随时粘贴） ───────────────
            // DisplayDialog 单条 message 最多显示约 40 行，超出时截断并提示
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
                truncated.AppendLine($"... （共 {lines.Length - 1} 行，完整内容已复制到剪贴板）");
                dialogBody = truncated.ToString().TrimEnd();
            }

            UnityEditor.EditorUtility.DisplayDialog(
                $"状态机状态列表（{stateMachine.RegisteredStateCount} 个）",
                dialogBody + "\n\n✓ 完整内容已复制到剪贴板",
                "确定");
#endif
        }

        [Button("预检状态机配置"), FoldoutGroup("测试功能")]
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
                errors.Add("MyCore 为空，StateDomain 还未绑定到 Entity。");
            }

            var animator = MyCore != null ? MyCore.animator : _cachedAnimator;
            if (animator == null)
            {
                errors.Add("Animator 为空，StateMachine 无法绑定 PlayableGraph。");
            }

            if (stateMachine == null)
            {
                errors.Add("stateMachine 为空。");
            }

            CollectPackSources(_workingPackSources);
            if (_workingPackSources.Count == 0)
            {
                warnings.Add("未配置任何 StateAniDataPack。");
            }

            var nameSet = new HashSet<string>(StringComparer.Ordinal);
            var idSet = new HashSet<int>();

            for (int i = 0; i < _workingPackSources.Count; i++)
            {
                var pack = _workingPackSources[i];
                if (pack == null)
                {
                    warnings.Add($"第 {i} 个状态包为空引用。");
                    continue;
                }

                pack.Check();

                if (pack.Infos == null)
                {
                    warnings.Add($"状态包 {pack.name} 的 Infos 为空。");
                    continue;
                }

                foreach (var info in pack.Infos.Values)
                {
                    allInfos.Add(info);

                    if (info == null)
                    {
                        warnings.Add($"状态包 {pack.name} 含空的 StateAniDataInfo。");
                        continue;
                    }

                    var basic = info.sharedData != null ? info.sharedData.basicConfig : null;
                    if (basic == null)
                    {
                        errors.Add($"状态包 {pack.name} 中存在 basicConfig 为空的状态。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(basic.stateName))
                    {
                        errors.Add($"状态包 {pack.name} 中存在空状态名（stateName）。");
                    }
                    else if (!nameSet.Add(basic.stateName))
                    {
                        errors.Add($"状态名重复: {basic.stateName}。");
                    }

                    if (basic.stateId >= 0 && !idSet.Add(basic.stateId))
                    {
                        errors.Add($"状态ID重复: {basic.stateId}。");
                    }

                    if (basic.stateId >= 0 && !stateById.ContainsKey(basic.stateId))
                    {
                        stateById.Add(basic.stateId, info);
                    }

                    if (basic.ignoreSupportFlag && basic.disableActiveOnSupportFlagSwitching)
                    {
                        warnings.Add($"状态 {basic.stateName} 同时开启 ignoreSupportFlag 与 disableActiveOnSupportFlagSwitching，切换矩阵语义冲突。");
                    }

                    if (basic.durationMode == StateDurationMode.UntilAnimationEnd && (info.sharedData == null || !info.sharedData.hasAnimation))
                    {
                        errors.Add($"状态 {basic.stateName} 使用 UntilAnimationEnd，但未启用动画。");
                    }

                    var shared = info.sharedData;
                    var animConfig = shared != null ? shared.animationConfig : null;
                    if (shared != null && shared.hasAnimation)
                    {
                        if (animConfig == null)
                        {
                            errors.Add($"状态 {basic.stateName} 标记 hasAnimation=true，但 animationConfig 为空。");
                        }
                        else if (animConfig.calculator == null)
                        {
                            errors.Add($"状态 {basic.stateName} 的 animationConfig.calculator 为空。");
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
                warnings.Add($"defaultStateKey={defaultStateKey} 未在已配置状态包中找到。");
            }

            ValidateFallbackCoverageFromPacks(fallbackCandidateCount, warnings);
            ValidateFallbackReachabilityInRuntime(errors, warnings, stateById);
            ValidateActivationMatrixCurrentRuntime(errors, warnings);
            ValidateActivationRollbackConsistency(errors, warnings);

            bool success = errors.Count == 0;

            if (logToConsole)
            {
                var sb = new StringBuilder(512);
                sb.AppendLine("=== StateDomain 预检报告 ===");
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
                    errors.Add($"状态 {stateName} 启用了 procedural IK，但 hasAnimation=false。");
                }

                if (!HasAnyIKGoalEnabled(procedural))
                {
                    errors.Add($"状态 {stateName} 启用了 procedural IK，但未配置任何启用的 IK limb/lookAt/segment。");
                }
            }

            if (procedural.enableMatchTarget && !shared.hasAnimation)
            {
                warnings.Add($"状态 {stateName} 启用了 MatchTarget 但无动画，运行时将无法生效。");
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
                        warnings.Add($"Fallback覆盖缺失: Layer={layerType}, Support={supportFlag} 无 canBeFeedback 状态候选。");
                    }
                    else if (count > 1)
                    {
                        warnings.Add($"Fallback候选重复: Layer={layerType}, Support={supportFlag} 有 {count} 个 canBeFeedback 候选，最终注册覆盖顺序需确认。");
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
                    errors.Add($"运行时层级丢失: {layerType}");
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
                        errors.Add($"Fallback不可达: Layer={layerType}, Support={supportFlag}, StateID={fallbackStateId} 未注册。");
                        continue;
                    }

                    if (!stateMachine.TryGetStateLayerType(fallbackState, out var fallbackLayer) || fallbackLayer != layerType)
                    {
                        errors.Add($"Fallback层级错误: Layer={layerType}, Support={supportFlag}, State={fallbackState.strKey} 实际层={fallbackLayer}。");
                    }

                    if (stateById.TryGetValue(fallbackStateId, out var info)
                        && info != null
                        && info.sharedData != null
                        && info.sharedData.basicConfig != null
                        && !info.sharedData.basicConfig.canBeFeedback)
                    {
                        warnings.Add($"Fallback规范风险: State={fallbackState.strKey} 被登记为Fallback，但 canBeFeedback=false。");
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
                    errors.Add($"激活矩阵校验失败: 注册状态 {kvp.Key} 为空或配置缺失。");
                    continue;
                }

                var basic = state.stateSharedData.basicConfig;
                var result = stateMachine.TestStateActivation(state, basic.layerType);
                if (!result.CanActivate)
                {
                    warnings.Add($"激活矩阵当前态不可达: State={state.strKey}, Layer={basic.layerType}, Reason={result.failureReason}");
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
                    errors.Add("激活回滚探针注册失败，无法验证回滚一致性。");
                    return;
                }

                probeId = probe.intKey;

                bool activated = stateMachine.TryActivateState(probe, StateLayerType.Main);
                if (activated)
                {
                    errors.Add("激活回滚探针异常：故障注入状态竟然激活成功，回滚链路未命中。");
                }

                int runningAfter = stateMachine.GetRunningStateCount();
                int layerAfter = stateMachine.GetLayerStateCount(StateLayerType.Main);
                if (runningAfter != runningBefore || layerAfter != layerBefore)
                {
                    errors.Add($"激活回滚不一致: Running {runningBefore}->{runningAfter}, MainLayer {layerBefore}->{layerAfter}");
                }

                if (!stateMachine.TryGetActivationEventFromLatest(0, out var latest))
                {
                    warnings.Add("结构化激活事件为空：无法用于线上归因。");
                }
                else if (latest.kind != StateMachine.ActivationEventKind.Rollback && latest.kind != StateMachine.ActivationEventKind.Failure)
                {
                    warnings.Add($"结构化激活事件未记录失败结论: LatestKind={latest.kind}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"激活回滚探针执行异常: {ex.Message}");
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
        // 缓存各层级的折叠状态（避免每次OnGUI重置）
        private static Dictionary<StateLayerType, bool> layerFoldouts = new Dictionary<StateLayerType, bool>();

        public bool IsSingleArea => true;
        public bool CanPreview => Application.isPlaying && stateMachine != null;
        public bool EditorPreviewCanPreviewNonPlay => false;

        public void DrawPreviewGUIPlaying() => EditorPreviewDrawPreviewGUIImpl();
        public void EditorPreviewDrawPreviewGUINonPlay()
        {
            // 非运行模式显示友好提示
            EditorGUILayout.HelpBox("进入运行模式后显示状态机实时信息", MessageType.Info);
        }
        private void EditorPreviewDrawPreviewGUIImpl()
        {
            var sm = stateMachine;
            if (sm == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("无状态机", UnityEditor.MessageType.Warning);
                return;
            }

            UnityEditor.EditorGUILayout.LabelField("状态机实时预览", UnityEditor.EditorStyles.boldLabel);
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
                    $"层级: {layer.layerType}  (权重: {layer.weight:F2})  运行状态: {runningCount}/{slotCount}",
                    true);
                layerFoldouts[layer.layerType] = foldout;
                if (!foldout) continue;

                UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);

                foreach (var kvp in layer.stateToSlotMap)
                {
                    var state = kvp.Key;
                    int slot = kvp.Value;
                    if (state == null) continue;

                    // 权重
                    float weight = 0f;
                    if (layer.mixer.IsValid() && slot >= 0 && slot < layer.mixer.GetInputCount())
                        weight = layer.mixer.GetInputWeight(slot);

                    bool isActive = layer.runningStates.Contains(state);
                    bool isFadingIn = layer.fadeInStates.ContainsKey(state);
                    bool isFadingOut = layer.fadeOutStates.ContainsKey(state);

                    // 当前 RuntimePhase
                    var phase = state.RuntimePhase;

                    // ---- 状态名称样式 ----
                    var nameStyle = new GUIStyle(UnityEditor.EditorStyles.label);
                    if (isActive) nameStyle.fontStyle = FontStyle.Bold;
                    if (isFadingIn)
                        nameStyle.normal.textColor = Color.Lerp(Color.white, Color.green, 0.7f);
                    else if (isFadingOut)
                        nameStyle.normal.textColor = Color.Lerp(Color.white, Color.red, 0.6f);

                    UnityEditor.EditorGUILayout.BeginHorizontal();

                    // ① 状态名称（含 Tooltip 显示激活时间等）
                    string tooltip = $"激活时间: {state.activationTime:F2}\n持续: {state.hasEnterTime:F2}s";
                    GUILayout.Label(new GUIContent(state.strKey ?? state.GetType().Name, tooltip), nameStyle, GUILayout.Width(140));

                    // ② RuntimePhase 标签（彩色）
                    Color phaseColor = phase switch
                    {
                        StateRuntimePhase.Pre => new Color(0.4f, 0.8f, 1f),     // 淡蓝
                        StateRuntimePhase.Main => new Color(0.3f, 0.9f, 0.3f),   // 绿色
                        StateRuntimePhase.Wait => new Color(1f, 0.8f, 0.2f),     // 黄色
                        StateRuntimePhase.Released => new Color(0.5f, 0.5f, 0.5f), // 灰色
                        _ => Color.white
                    };
                    var phaseStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = phaseColor },
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUILayout.Label(phase.ToString(), phaseStyle, GUILayout.Width(55));

                    // ③ 权重数值
                    UnityEditor.EditorGUILayout.LabelField($"{weight:F2}", GUILayout.Width(35));

                    // ④ 动态进度条（夸张长度变化）
                    float maxBarWidth = Mathf.Max(40, UnityEditor.EditorGUIUtility.currentViewWidth - 290);
                    float barWidth = Mathf.Lerp(4f, maxBarWidth, weight);
                    var barRect = UnityEditor.EditorGUILayout.GetControlRect(
                        GUILayout.Width(barWidth), GUILayout.Height(18));

                    // 黑色背景
                    UnityEditor.EditorGUI.DrawRect(barRect, Color.black);

                    // 填充色
                    float fillFactor = Mathf.Clamp01(weight);
                    var fillRect = new Rect(barRect.x, barRect.y, barRect.width, barRect.height);
                    Color lowColor = new Color(0.1f, 0.1f, 0.1f);
                    Color highColor = new Color(0.2f, 1f, 0.2f);
                    Color fillColor = Color.Lerp(lowColor, highColor, fillFactor);
                    UnityEditor.EditorGUI.DrawRect(fillRect, fillColor);

                    // 光晕
                    if (weight > 0.1f)
                    {
                        var glowRect = new Rect(barRect.x, barRect.y, barRect.width, 4);
                        UnityEditor.EditorGUI.DrawRect(glowRect, new Color(1f, 1f, 1f, weight * 0.5f));
                    }

                    // 白边框
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + barRect.height - 1, barRect.width, 1), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, 1, barRect.height), Color.white);
                    UnityEditor.EditorGUI.DrawRect(new Rect(barRect.x + barRect.width - 1, barRect.y, 1, barRect.height), Color.white);

                    // 居中百分比
                    GUIStyle percentStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
                    {
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUI.Label(barRect, $"{weight:P0}", percentStyle);

                    // ⑤ 淡入/淡出标签
                    if (isFadingIn)
                        GUILayout.Label("淡入", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
                    else if (isFadingOut)
                        GUILayout.Label("淡出", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
                    else
                        GUILayout.Space(35); // 对齐

                    UnityEditor.EditorGUILayout.EndHorizontal();
                }

                UnityEditor.EditorGUILayout.EndVertical();
            }
        }
#endif

        #endregion

    }
}
