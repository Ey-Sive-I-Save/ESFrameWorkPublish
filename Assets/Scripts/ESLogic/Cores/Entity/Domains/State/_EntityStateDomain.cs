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

        [Title("状态机")]
        [LabelText("状态机")]
        public StateMachine stateMachine = new StateMachine();

        [LabelText("默认状态键")]
        public string defaultStateKey = "";
        
        [LabelText("初始激活状态名（可为空）")]
        [Tooltip("状态机启动后自动激活的状态，留空则不自动激活")]
        public string initialStateName = "";

        [NonSerialized] private bool _stateMachineInitialized;
        [NonSerialized] private Animator _cachedAnimator;

        [NonSerialized] private StateAniDataPack _cachedPack;
        [NonSerialized] private bool _packDirty = true;
        [NonSerialized] private List<StateAniDataInfo> _cachedInfos = new List<StateAniDataInfo>(64);

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
            if (stateAniDataPack == null) return;
            if (_cachedPack != stateAniDataPack)
            {
                _cachedPack = stateAniDataPack;
                _packDirty = true;
            }

            if (!_packDirty) return;

            stateAniDataPack.Check();
            _cachedInfos.Clear();
            
            // 批量注册所有状态
            RegisterStatesFromInfos(stateAniDataPack.Infos.Values, allowOverride: false);

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
            if (MyCore == null) return;
            if (_cachedAnimator == null)
            {
                _cachedAnimator = MyCore.animator;
            }
            if (_cachedAnimator == null) return;

            if (stateMachine == null) stateMachine = new StateMachine();
            stateMachine.stateMachineKey = string.IsNullOrEmpty(defaultStateKey) ? "Entity" : defaultStateKey;
            stateMachine.Initialize(MyCore, _cachedAnimator);
            stateMachine.defaultStateKey = defaultStateKey;
            stateMachine.StartStateMachine();
            _stateMachineInitialized = true;
            
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
            if (stateAniDataPack == null)
            {
                Debug.LogWarning("[StateDomain] 状态数据包为空");
                return;
            }
            
            int count = RegisterStatesFromInfos(stateAniDataPack.Infos.Values, allowOverride: true);
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

            Debug.Log($"=== 状态机所有状态 ({stateMachine.RegisteredStateCount}) ===");
            foreach (var kvp in stateMachine.EnumerateRegisteredStatesByKey())
            {
                var state = kvp.Value;
                var layerType = stateMachine.TryGetStateLayerType(state, out var layer)
                    ? layer.ToString()
                    : "Unknown";
                var isRunning = stateMachine.IsStateRunning(state);
                var isFallback = state.stateSharedData?.basicConfig?.canBeFeedback ?? false;
                
                Debug.Log($"  [{layerType}] {kvp.Key} (IntKey:{state.intKey}) - " +
                          $"运行:{isRunning}, Fallback:{isFallback}");
            }
        }

        #endregion
    }
}
