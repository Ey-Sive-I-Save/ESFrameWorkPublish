using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 状态机数据 - ScriptableObject
    /// 总状态机配置,包含所有状态定义和默认环境
    /// </summary>
    [CreateAssetMenu(fileName = "NewStateMachineData", menuName = "ES/Animation/State Machine Data", order = 0)]
    public class StateMachineData : ScriptableObject
    {
        [InfoBox("基于Playable的多流水线动画状态机配置")]
        
        [TabGroup("基础")]
        [LabelText("状态机名称")]
        public string machineName;

        [TabGroup("基础")]
        [LabelText("默认Clip表")]
        [Tooltip("作为默认环境提供Clip和参数")]
        [AssetsOnly]
        public AnimationClipTable defaultClipTable;

        [TabGroup("基础")]
        [LabelText("额外Clip表")]
        [Tooltip("可以动态加载的额外Clip表列表")]
        public List<AnimationClipTable> additionalClipTables = new List<AnimationClipTable>();

        // ============ 流水线配置 ============
        [TabGroup("流水线")]
        [Title("基本线配置")]
        [LabelText("基本线状态列表")]
        [Tooltip("控制跑跳下蹲的基础动作,直接包含在状态机中")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<StateDefinition> basicStates = new List<StateDefinition>();

        [TabGroup("流水线")]
        [Title("主线配置")]
        [LabelText("主线状态列表")]
        [Tooltip("控制技能、表情、交互等互相排斥的动作")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<StateDefinition> mainStates = new List<StateDefinition>();

        [TabGroup("流水线")]
        [LabelText("主线动态加载")]
        [Tooltip("主线是否支持动态装载")]
        public bool mainLineDynamicLoad = true;

        [TabGroup("流水线")]
        [Title("Buff线配置")]
        [LabelText("Buff线状态列表")]
        [Tooltip("控制Buff效果,可能不输出动作只执行效果")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<StateDefinition> buffStates = new List<StateDefinition>();

        [TabGroup("流水线")]
        [LabelText("Buff线动态加载")]
        [Tooltip("Buff线是否支持动态装载")]
        public bool buffLineDynamicLoad = true;

        // ============ 流水线混合配置 ============
        [TabGroup("流水线")]
        [Title("流水线混合")]
        [LabelText("基本线权重")]
        [Range(0f, 1f)]
        public float basicPipelineWeight = 1f;

        [TabGroup("流水线")]
        [LabelText("主线权重")]
        [Range(0f, 1f)]
        public float mainPipelineWeight = 1f;

        [TabGroup("流水线")]
        [LabelText("Buff线权重")]
        [Range(0f, 1f)]
        public float buffPipelineWeight = 0.5f;

        // ============ 默认参数 ============
        [TabGroup("参数")]
        [LabelText("默认Float参数")]
        [TableList(AlwaysExpanded = true)]
        public List<FloatParameter> defaultFloatParameters = new List<FloatParameter>();

        [TabGroup("参数")]
        [LabelText("默认Int参数")]
        [TableList(AlwaysExpanded = true)]
        public List<IntParameter> defaultIntParameters = new List<IntParameter>();

        [TabGroup("参数")]
        [LabelText("默认Bool参数")]
        [TableList(AlwaysExpanded = true)]
        public List<BoolParameter> defaultBoolParameters = new List<BoolParameter>();

        // ============ 初始状态配置 ============
        [TabGroup("初始化")]
        [LabelText("基本线初始状态ID")]
        [Tooltip("状态机启动时基本线的默认状态")]
        public int basicInitialStateId = 0;

        [TabGroup("初始化")]
        [LabelText("主线初始状态ID")]
        [Tooltip("状态机启动时主线的默认状态(-1表示无初始状态)")]
        public int mainInitialStateId = -1;

        [TabGroup("初始化")]
        [LabelText("自动启动")]
        [Tooltip("是否在Awake时自动初始化并启动状态机")]
        public bool autoStart = true;

        // ============ 调试配置 ============
        [TabGroup("调试")]
        [LabelText("启用调试日志")]
        public bool enableDebugLog = false;

        [TabGroup("调试")]
        [LabelText("显示状态转换")]
        public bool logStateTransitions = true;

        [TabGroup("调试")]
        [LabelText("显示代价变化")]
        public bool logCostChanges = false;

        /// <summary>
        /// 根据ID查找状态定义
        /// </summary>
        public StateDefinition GetStateDefinition(int stateId)
        {
            // 搜索基本线
            var state = basicStates?.Find(s => s.stateId == stateId);
            if (state != null) return state;

            // 搜索主线
            state = mainStates?.Find(s => s.stateId == stateId);
            if (state != null) return state;

            // 搜索Buff线
            state = buffStates?.Find(s => s.stateId == stateId);
            if (state != null) return state;

            return null;
        }

        /// <summary>
        /// 根据流水线类型获取状态列表
        /// </summary>
        public List<StateDefinition> GetStatesByPipeline(StatePipelineType pipelineType)
        {
            return pipelineType switch
            {
                StatePipelineType.Basic => basicStates,
                StatePipelineType.Main => mainStates,
                StatePipelineType.Buff => buffStates,
                _ => new List<StateDefinition>()
            };
        }

        /// <summary>
        /// 动态添加状态
        /// </summary>
        public void AddState(StateDefinition state)
        {
            if (state == null) return;

            var targetList = GetStatesByPipeline(state.pipelineType);
            if (targetList != null && !targetList.Contains(state))
            {
                targetList.Add(state);
            }
        }

        /// <summary>
        /// 动态移除状态
        /// </summary>
        public void RemoveState(int stateId)
        {
            basicStates?.RemoveAll(s => s.stateId == stateId);
            mainStates?.RemoveAll(s => s.stateId == stateId);
            buffStates?.RemoveAll(s => s.stateId == stateId);
        }

        [Button("验证状态机数据", ButtonSizes.Large)]
        [PropertySpace(10)]
        private void ValidateStateMachine()
        {
            int totalStates = 0;
            HashSet<int> stateIds = new HashSet<int>();
            bool hasErrors = false;

            // 验证基本线
            if (basicStates != null)
            {
                foreach (var state in basicStates)
                {
                    if (stateIds.Contains(state.stateId))
                    {
                        Debug.LogError($"Duplicate state ID {state.stateId} in basic pipeline!");
                        hasErrors = true;
                    }
                    stateIds.Add(state.stateId);
                    totalStates++;
                }
            }

            // 验证主线
            if (mainStates != null)
            {
                foreach (var state in mainStates)
                {
                    if (stateIds.Contains(state.stateId))
                    {
                        Debug.LogError($"Duplicate state ID {state.stateId} in main pipeline!");
                        hasErrors = true;
                    }
                    stateIds.Add(state.stateId);
                    totalStates++;
                }
            }

            // 验证Buff线
            if (buffStates != null)
            {
                foreach (var state in buffStates)
                {
                    if (stateIds.Contains(state.stateId))
                    {
                        Debug.LogError($"Duplicate state ID {state.stateId} in buff pipeline!");
                        hasErrors = true;
                    }
                    stateIds.Add(state.stateId);
                    totalStates++;
                }
            }

            // 验证Clip表
            if (defaultClipTable == null)
            {
                Debug.LogWarning("No default clip table assigned!");
            }

            if (!hasErrors)
            {
                Debug.Log($"✓ State machine '{machineName}' validation passed. Total states: {totalStates}");
            }
            else
            {
                Debug.LogError($"✗ State machine '{machineName}' validation failed!");
            }
        }

        [Button("生成状态ID映射", ButtonSizes.Medium)]
        private void GenerateStateIdMap()
        {
            Dictionary<string, int> nameToId = new Dictionary<string, int>();
            
            void AddStates(List<StateDefinition> states, string pipeline)
            {
                if (states == null) return;
                foreach (var state in states)
                {
                    nameToId[$"{pipeline}/{state.stateName}"] = state.stateId;
                }
            }

            AddStates(basicStates, "Basic");
            AddStates(mainStates, "Main");
            AddStates(buffStates, "Buff");

            Debug.Log("=== State ID Mapping ===");
            foreach (var kv in nameToId)
            {
                Debug.Log($"{kv.Key} -> {kv.Value}");
            }
        }
    }

    // ============ 参数定义类 ============
    [Serializable]
    public class FloatParameter
    {
        [TableColumnWidth(150)]
        public string name;
        
        [TableColumnWidth(100)]
        public float defaultValue;
    }

    [Serializable]
    public class IntParameter
    {
        [TableColumnWidth(150)]
        public string name;
        
        [TableColumnWidth(100)]
        public int defaultValue;
    }

    [Serializable]
    public class BoolParameter
    {
        [TableColumnWidth(150)]
        public string name;
        
        [TableColumnWidth(100)]
        public bool defaultValue;
    }
}
