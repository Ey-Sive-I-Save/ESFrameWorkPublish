using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 同路状态类型 - 用于实现退化机制
    /// </summary>
    public enum SamePathType
    {
        None,           // 非同路
        Idle,           // 静止
        Walk,           // 行走
        Run,            // 奔跑
        Sprint          // 疾跑
    }

    /// <summary>
    /// 状态定义 - 核心状态配置
    /// 支持多态序列化的完整状态数据
    /// </summary>
    [Serializable]
    public class StateDefinition
    {
        [TabGroup("基础")]
        [LabelText("状态ID")]
        public int stateId;
        
        [TabGroup("基础")]
        [LabelText("状态名称")]
        public string stateName;
        
        [TabGroup("基础")]
        [LabelText("所属流水线")]
        public StatePipelineType pipelineType = StatePipelineType.Main;
        
        [TabGroup("基础")]
        [LabelText("状态优先级")]
        [Tooltip("优先级越高越容易打断其他状态")]
        [Range(0, 100)]
        public int priority = 50;

        [TabGroup("基础")]
        [LabelText("状态持续时间(秒)")]
        [Tooltip("负数表示无限循环")]
        public float duration = -1f;

        // ============ 代价系统 ============
        [TabGroup("代价")]
        [LabelText("状态代价配置")]
        [InlineProperty]
        public StateCostData cost = new StateCostData();

        [TabGroup("代价")]
        [LabelText("后摇开始时间(归一化)")]
        [Tooltip("到达此时间后开始返还代价")]
        [Range(0f, 1f)]
        public float recoveryStartTime = 0.7f;

        [TabGroup("代价")]
        [LabelText("后摇持续时间(秒)")]
        [Tooltip("代价返还所需的时间")]
        public float recoveryDuration = 0.3f;

        // ============ 同路退化 ============
        [TabGroup("同路")]
        [LabelText("同路类型")]
        public SamePathType samePathType = SamePathType.None;

        [TabGroup("同路")]
        [LabelText("退化目标ID")]
        [Tooltip("被弱打断时退化到的状态ID")]
        [ShowIf("@samePathType != SamePathType.None && samePathType != SamePathType.Idle")]
        public int degradeTargetId = -1;

        [TabGroup("同路")]
        [LabelText("允许弱打断")]
        [Tooltip("同路状态可以被低级状态弱打断而不是完全退出")]
        public bool allowWeakInterrupt = false;

        // ============ 条件系统 ============
        [TabGroup("条件")]
        [LabelText("进入条件")]
        [SerializeReference]
        public List<StateCondition> enterConditions = new List<StateCondition>();

        [TabGroup("条件")]
        [LabelText("退出条件")]
        [SerializeReference]
        public List<StateCondition> exitConditions = new List<StateCondition>();

        [TabGroup("条件")]
        [LabelText("保持条件")]
        [Tooltip("不满足时自动退出")]
        [SerializeReference]
        public List<StateCondition> keepConditions = new List<StateCondition>();

        // ============ 组件系统 ============
        [TabGroup("组件")]
        [LabelText("显示组件")]
        [InlineProperty]
        public DisplayComponent displayComponent;

        [TabGroup("组件")]
        [LabelText("过渡组件")]
        [InlineProperty]
        public TransitionComponent transitionComponent;

        [TabGroup("组件")]
        [LabelText("执行组件列表")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<ExecutionComponent> executionComponents = new List<ExecutionComponent>();

        [TabGroup("组件")]
        [LabelText("IK组件")]
        [InlineProperty]
        public IKComponent ikComponent;

        [TabGroup("组件")]
        [LabelText("自定义组件")]
        [SerializeReference]
        public List<StateComponent> customComponents = new List<StateComponent>();

        // ============ 转换系统 ============
        [TabGroup("转换")]
        [LabelText("自动转换列表")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<StateTransition> transitions = new List<StateTransition>();

        // ============ 标签系统 ============
        [TabGroup("标签")]
        [LabelText("状态标签")]
        [Tooltip("用于快速分类和查询")]
        public List<string> tags = new List<string>();

        [TabGroup("标签")]
        [LabelText("不参与代价计算")]
        [Tooltip("此状态不影响代价系统和备忘状态")]
        public bool ignoreInCostCalculation = false;

        /// <summary>
        /// 检查进入条件是否满足
        /// </summary>
        public bool CheckEnterConditions(StateMachineContext context)
        {
            if (enterConditions == null || enterConditions.Count == 0)
                return true;

            foreach (var condition in enterConditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查保持条件是否满足
        /// </summary>
        public bool CheckKeepConditions(StateMachineContext context)
        {
            if (keepConditions == null || keepConditions.Count == 0)
                return true;

            foreach (var condition in keepConditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 检查退出条件是否满足
        /// </summary>
        public bool CheckExitConditions(StateMachineContext context)
        {
            if (exitConditions == null || exitConditions.Count == 0)
                return false;

            foreach (var condition in exitConditions)
            {
                if (condition.Evaluate(context))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有组件
        /// </summary>
        public IEnumerable<StateComponent> GetAllComponents()
        {
            if (displayComponent != null)
                yield return displayComponent;
            
            if (transitionComponent != null)
                yield return transitionComponent;
            
            if (executionComponents != null)
            {
                foreach (var comp in executionComponents)
                    yield return comp;
            }
            
            if (ikComponent != null)
                yield return ikComponent;
            
            if (customComponents != null)
            {
                foreach (var comp in customComponents)
                    yield return comp;
            }
        }
    }

    /// <summary>
    /// 状态转换 - 定义从当前状态到其他状态的转换规则
    /// </summary>
    [Serializable]
    public class StateTransition
    {
        [LabelText("目标状态ID")]
        public int targetStateId;

        [LabelText("转换条件")]
        [SerializeReference]
        public List<StateCondition> conditions = new List<StateCondition>();

        [LabelText("转换时间(归一化)")]
        [Tooltip("到达此时间后才允许转换,-1表示任意时间")]
        [Range(-1f, 1f)]
        public float transitionTime = -1f;

        [LabelText("强制转换")]
        [Tooltip("忽略代价和优先级限制")]
        public bool forceTransition = false;

        /// <summary>
        /// 检查转换条件是否满足
        /// </summary>
        public bool CheckConditions(StateMachineContext context, float normalizedTime)
        {
            // 检查时间条件
            if (transitionTime >= 0f && normalizedTime < transitionTime)
                return false;

            // 检查自定义条件
            if (conditions == null || conditions.Count == 0)
                return true;

            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }
    }
}
