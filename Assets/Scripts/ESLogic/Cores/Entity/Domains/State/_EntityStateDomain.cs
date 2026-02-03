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
            // 必须先初始化StateMachine（创建流水线等基础设施），再初始化StateAniDataPack（注册状态）
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
            
            foreach (var info in stateAniDataPack.Infos.Values)
            {
                if (info == null) continue;
                
                // 1. 确保Runtime初始化（不重复）
                if (!info.sharedData.IsRuntimeInitialized)
                {
                    info.InitializeRuntime();
                }
                
                // 2. 缓存info
                _cachedInfos.Add(info);
                
                // 3. 创建StateBase实例并注册到状态机
                if (stateMachine != null)
                {
                    var state = CreateStateFromInfo(info);
                    if (state != null)
                    {
                        // 获取管线类型
                        var pipelineType = info.sharedData.basicConfig.pipelineType;
                        
                        // 获取状态名称（优先使用配置）
                        string stateName = string.IsNullOrEmpty(info.sharedData.basicConfig.stateName) 
                            ? null  // 传null让StateMachine自动生成
                            : info.sharedData.basicConfig.stateName;
                        
                        // 注册到状态机（仅使用string键，StateMachine自动生成intKey和处理fallback）
                        bool registered = stateMachine.RegisterState(stateName ?? "AutoState", state, pipelineType);
                        
                        if (!registered)
                        {
                            Debug.LogWarning($"[StateDomain] 注册状态失败: {stateName}");
                        }
                    }
                }
            }

            _packDirty = false;
        }
        
        /// <summary>
        /// 从StateAniDataInfo创建StateBase实例
        /// </summary>
        private StateBase CreateStateFromInfo(StateAniDataInfo info)
        {
            // 创建基础状态实例
            var state = new StateBase();
            
            // 绑定SharedData
            state.stateSharedData = info.sharedData;
            
            // 绑定VariableData（运行时独立数据）
            state.stateVariableData = new StateVariableData();
            
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

            Debug.Log($"=== 状态机所有状态 ({stateMachine.stringToStateMap.Count}) ===");
            foreach (var kvp in stateMachine.stringToStateMap)
            {
                var state = kvp.Value;
                var pipelineType = stateMachine.statePipelineMap.ContainsKey(state) 
                    ? stateMachine.statePipelineMap[state].ToString() 
                    : "Unknown";
                var isRunning = stateMachine.runningStates.Contains(state);
                var isFallback = state.stateSharedData?.basicConfig?.canBeFeedback ?? false;
                
                Debug.Log($"  [{pipelineType}] {kvp.Key} (IntKey:{state.intKey}) - " +
                          $"运行:{isRunning}, Fallback:{isFallback}");
            }
        }

        #endregion
    }
}
