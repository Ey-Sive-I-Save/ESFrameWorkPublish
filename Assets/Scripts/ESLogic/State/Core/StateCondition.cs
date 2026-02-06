using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 条件评估器 - 用于判断状态进入/退出条件
    /// </summary>
    [Serializable]
    public abstract class StateCondition
    {
        public abstract bool Evaluate(StateMachineContext context);
    }

    /// <summary>
    /// 浮点数比较条件
    /// </summary>
    [Serializable]
    public class FloatCondition : StateCondition
    {
        public enum CompareMode
        {
            Greater,
            Less,
            Equal,
            GreaterOrEqual,
            LessOrEqual,
            NotEqual
        }

        public string parameterName;
        public CompareMode mode;
        public float threshold;

        public override bool Evaluate(StateMachineContext context)
        {
            float value = context.GetFloat(parameterName);
            return mode switch
            {
                CompareMode.Greater => value > threshold,
                CompareMode.Less => value < threshold,
                CompareMode.Equal => Mathf.Approximately(value, threshold),
                CompareMode.GreaterOrEqual => value >= threshold,
                CompareMode.LessOrEqual => value <= threshold,
                CompareMode.NotEqual => !Mathf.Approximately(value, threshold),
                _ => false
            };
        }
    }

    /// <summary>
    /// 整数比较条件
    /// </summary>
    [Serializable]
    public class IntCondition : StateCondition
    {
        public enum CompareMode
        {
            Greater,
            Less,
            Equal,
            GreaterOrEqual,
            LessOrEqual,
            NotEqual
        }

        public string parameterName;
        public CompareMode mode;
        public int threshold;

        public override bool Evaluate(StateMachineContext context)
        {
            int value = context.GetInt(parameterName);
            return mode switch
            {
                CompareMode.Greater => value > threshold,
                CompareMode.Less => value < threshold,
                CompareMode.Equal => value == threshold,
                CompareMode.GreaterOrEqual => value >= threshold,
                CompareMode.LessOrEqual => value <= threshold,
                CompareMode.NotEqual => value != threshold,
                _ => false
            };
        }
    }

    /// <summary>
    /// 布尔条件
    /// </summary>
    [Serializable]
    public class BoolCondition : StateCondition
    {
        public string parameterName;
        public bool expectedValue = true;

        public override bool Evaluate(StateMachineContext context)
        {
            return context.GetBool(parameterName) == expectedValue;
        }
    }

    /// <summary>
    /// 触发器条件
    /// </summary>
    [Serializable]
    public class TriggerCondition : StateCondition
    {
        public string triggerName;

        public override bool Evaluate(StateMachineContext context)
        {
            return context.GetTrigger(triggerName);
        }
    }

    /// <summary>
    /// 组合条件 - 支持AND/OR逻辑
    /// </summary>
    [Serializable]
    public class CompositeCondition : StateCondition
    {
        public enum LogicMode
        {
            And,    // 所有条件都满足
            Or,     // 任一条件满足
            Not     // 取反(只用第一个条件)
        }

        public LogicMode mode = LogicMode.And;
        public List<StateCondition> conditions = new List<StateCondition>();

        public override bool Evaluate(StateMachineContext context)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            switch (mode)
            {
                case LogicMode.And:
                    foreach (var condition in conditions)
                    {
                        if (!condition.Evaluate(context))
                            return false;
                    }
                    return true;

                case LogicMode.Or:
                    foreach (var condition in conditions)
                    {
                        if (condition.Evaluate(context))
                            return true;
                    }
                    return false;

                case LogicMode.Not:
                    return !conditions[0].Evaluate(context);

                default:
                    return true;
            }
        }
    }
}
