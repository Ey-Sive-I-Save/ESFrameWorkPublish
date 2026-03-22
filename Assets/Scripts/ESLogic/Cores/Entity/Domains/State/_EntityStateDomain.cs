using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("状态域")]
    public class EntityStateDomain : Domain<Entity, EntityStateModuleBase>
    {
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
                var state     = kvp.Value;
                var layerType = stateMachine.TryGetStateLayerType(state, out var layer)
                    ? layer.ToString() : "Unknown";
                var isRunning  = stateMachine.IsStateRunning(state);
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

        #endregion
    }
}
