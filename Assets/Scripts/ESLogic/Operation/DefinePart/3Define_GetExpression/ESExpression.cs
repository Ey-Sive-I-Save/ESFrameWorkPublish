using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 运行时取值表达式基类。
    /// 表达式通过 Evaluate 从运行目标和支持环境中计算出一个值。
    /// </summary>
    public abstract class ESGetExpression<TOut>
    {
        public abstract TOut Evaluate(ESRuntimeTarget target, IOpSupporter support);
    }

    /// <summary>Float 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetFloatExpression : ESGetExpression<float>
    {
    }

    /// <summary>Bool 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetBoolExpression : ESGetExpression<bool>
    {
    }

    #region Float 表达式实现

    /// <summary>返回固定 float 值。</summary>
    [Serializable]
    public class ESConstantFloatExpression : ESGetFloatExpression
    {
        [SerializeField]
        private float m_Value;

        public ESConstantFloatExpression(float value)
        {
            m_Value = value;
        }

        public override float Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            return m_Value;
        }
    }

    /// <summary>将两个 float 表达式结果相加。</summary>
    [Serializable]
    public class ESAddFloatExpression : ESGetFloatExpression
    {
        [SerializeField]
        private ESGetFloatExpression m_Left;

        [SerializeField]
        private ESGetFloatExpression m_Right;

        public ESAddFloatExpression(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override float Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            float leftValue = m_Left != null ? m_Left.Evaluate(target, support) : 0f;
            float rightValue = m_Right != null ? m_Right.Evaluate(target, support) : 0f;
            return leftValue + rightValue;
        }
    }

    /// <summary>将两个 float 表达式结果相乘。</summary>
    [Serializable]
    public class ESMultiplyFloatExpression : ESGetFloatExpression
    {
        [SerializeField]
        private ESGetFloatExpression m_Left;

        [SerializeField]
        private ESGetFloatExpression m_Right;

        public ESMultiplyFloatExpression(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override float Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            float leftValue = m_Left != null ? m_Left.Evaluate(target, support) : 0f;
            float rightValue = m_Right != null ? m_Right.Evaluate(target, support) : 0f;
            return leftValue * rightValue;
        }
    }

    #endregion

    #region Bool 表达式实现

    /// <summary>返回固定 bool 值。</summary>
    [Serializable]
    public class ESConstantBoolExpression : ESGetBoolExpression
    {
        [SerializeField]
        private bool m_Value;

        public ESConstantBoolExpression(bool value)
        {
            m_Value = value;
        }

        public override bool Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            return m_Value;
        }
    }

    /// <summary>对两个 bool 表达式执行 AND。</summary>
    [Serializable]
    public class ESAndBoolExpression : ESGetBoolExpression
    {
        [SerializeField]
        private ESGetBoolExpression m_Left;

        [SerializeField]
        private ESGetBoolExpression m_Right;

        public ESAndBoolExpression(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override bool Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            return m_Left != null
                   && m_Right != null
                   && m_Left.Evaluate(target, support)
                   && m_Right.Evaluate(target, support);
        }
    }

    /// <summary>对两个 bool 表达式执行 OR。</summary>
    [Serializable]
    public class ESOrBoolExpression : ESGetBoolExpression
    {
        [SerializeField]
        private ESGetBoolExpression m_Left;

        [SerializeField]
        private ESGetBoolExpression m_Right;

        public ESOrBoolExpression(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            m_Left = left;
            m_Right = right;
        }

        public override bool Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            return (m_Left != null && m_Left.Evaluate(target, support))
                   || (m_Right != null && m_Right.Evaluate(target, support));
        }
    }

    /// <summary>比较两个 float 表达式并返回 bool。</summary>
    [Serializable]
    public class ESCompareFloatExpression : ESGetBoolExpression
    {
        public enum CompareType
        {
            Greater,
            GreaterEqual,
            Less,
            LessEqual,
            Equal,
            NotEqual
        }

        [SerializeField]
        private ESGetFloatExpression m_Left;

        [SerializeField]
        private ESGetFloatExpression m_Right;

        [SerializeField]
        private CompareType m_CompareType;

        public ESCompareFloatExpression(ESGetFloatExpression left, ESGetFloatExpression right, CompareType compareType)
        {
            m_Left = left;
            m_Right = right;
            m_CompareType = compareType;
        }

        public override bool Evaluate(ESRuntimeTarget target, IOpSupporter support)
        {
            if (m_Left == null || m_Right == null)
                return false;

            float leftValue = m_Left.Evaluate(target, support);
            float rightValue = m_Right.Evaluate(target, support);

            switch (m_CompareType)
            {
                case CompareType.Greater:
                    return leftValue > rightValue;
                case CompareType.GreaterEqual:
                    return leftValue >= rightValue;
                case CompareType.Less:
                    return leftValue < rightValue;
                case CompareType.LessEqual:
                    return leftValue <= rightValue;
                case CompareType.Equal:
                    return Mathf.Approximately(leftValue, rightValue);
                case CompareType.NotEqual:
                    return !Mathf.Approximately(leftValue, rightValue);
                default:
                    return false;
            }
        }
    }

    #endregion

    #region 表达式构建器

    /// <summary>表达式工厂方法集合。</summary>
    public static class ESExpressionBuilder
    {
        #region Float 表达式构建

        public static ESConstantFloatExpression Constant(float value)
        {
            return new ESConstantFloatExpression(value);
        }

        public static ESAddFloatExpression Add(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESAddFloatExpression(left, right);
        }

        public static ESMultiplyFloatExpression Multiply(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESMultiplyFloatExpression(left, right);
        }

        #endregion

        #region Bool 表达式构建

        public static ESConstantBoolExpression Constant(bool value)
        {
            return new ESConstantBoolExpression(value);
        }

        public static ESAndBoolExpression And(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESAndBoolExpression(left, right);
        }

        public static ESOrBoolExpression Or(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESOrBoolExpression(left, right);
        }

        public static ESCompareFloatExpression Compare(ESGetFloatExpression left, ESGetFloatExpression right, ESCompareFloatExpression.CompareType compareType)
        {
            return new ESCompareFloatExpression(left, right, compareType);
        }

        #endregion
    }

    #endregion
}
