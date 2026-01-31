using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 获取表达式基类 (ESGetExpression)
    /// 【通用值获取接口】
    ///
    /// 【核心概念】
    /// ESGetExpression是ES框架的表达式计算基类，专门用于获取各种类型的值
    /// 通过泛型参数指定输出类型，支持float、bool等多种数据类型
    ///
    /// 【设计优势】
    /// • 类型安全：泛型确保获取值的类型正确性
    /// • 统一接口：所有获取表达式都使用相同的Evaluate方法
    /// • 扩展性：可以轻松添加新的值获取类型
    /// • 序列化友好：支持Unity序列化系统
    ///
    /// 【使用模式】
    /// <code>
    /// // 创建float值获取表达式
    /// var floatExpr = new ESConstantFloatExpression(42.0f);
    ///
    /// // 计算表达式
    /// float result = floatExpr.Evaluate(target, supporter);
    /// </code>
    /// </summary>
    public abstract class ESGetExpression<TOut>
    {
        /// <summary>
        /// 计算并获取值
        /// 【核心方法】所有表达式子类必须实现此方法
        /// 【参数说明】target: 运行时目标对象, support: 操作支持器
        /// 【返回值】计算结果，类型为TOut
        /// </summary>
        public abstract TOut Evaluate(ESRuntimeTarget target, IOpSupporter support);
    }

    /// <summary>
    /// Float值获取表达式基类
    /// 【数值计算】专门用于获取和计算float类型的值
    /// </summary>
    [Serializable]
    public abstract class ESGetFloatExpression : ESGetExpression<float>
    {

    }

    /// <summary>
    /// Bool值获取表达式基类
    /// 【条件判断】专门用于获取和计算bool类型的值
    /// </summary>
    [Serializable]
    public abstract class ESGetBoolExpression : ESGetExpression<bool>
    {
    }

    #region Float表达式实现

    /// <summary>
    /// 常量Float表达式
    /// 【固定值】返回预设的float常量值
    /// </summary>
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

    /// <summary>
    /// Float加法表达式
    /// 【数值相加】将两个float表达式结果相加
    /// </summary>
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
            return m_Left.Evaluate(target, support) + m_Right.Evaluate(target, support);
        }
    }

    /// <summary>
    /// Float乘法表达式
    /// 【数值相乘】将两个float表达式结果相乘
    /// </summary>
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
            return m_Left.Evaluate(target, support) * m_Right.Evaluate(target, support);
        }
    }

    #endregion

    #region Bool表达式实现

    /// <summary>
    /// 常量Bool表达式
    /// 【固定值】返回预设的bool常量值
    /// </summary>
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

    /// <summary>
    /// Bool与运算表达式
    /// 【逻辑与】两个bool表达式结果的逻辑与运算
    /// </summary>
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
            return m_Left.Evaluate(target, support) && m_Right.Evaluate(target, support);
        }
    }

    /// <summary>
    /// Bool或运算表达式
    /// 【逻辑或】两个bool表达式结果的逻辑或运算
    /// </summary>
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
            return m_Left.Evaluate(target, support) || m_Right.Evaluate(target, support);
        }
    }

    /// <summary>
    /// Float比较表达式
    /// 【数值比较】比较两个float表达式结果的大小关系
    /// </summary>
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

    /// <summary>
    /// 表达式构建器
    /// 【流式API】提供直观的表达式构建接口
    /// </summary>
    public static class ESExpressionBuilder
    {
        #region Float表达式构建

        /// <summary>
        /// 创建常量Float表达式
        /// </summary>
        public static ESConstantFloatExpression Constant(float value)
        {
            return new ESConstantFloatExpression(value);
        }

        /// <summary>
        /// 创建Float加法表达式
        /// </summary>
        public static ESAddFloatExpression Add(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESAddFloatExpression(left, right);
        }

        /// <summary>
        /// 创建Float乘法表达式
        /// </summary>
        public static ESMultiplyFloatExpression Multiply(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESMultiplyFloatExpression(left, right);
        }

        #endregion

        #region Bool表达式构建

        /// <summary>
        /// 创建常量Bool表达式
        /// </summary>
        public static ESConstantBoolExpression Constant(bool value)
        {
            return new ESConstantBoolExpression(value);
        }

        /// <summary>
        /// 创建Bool与运算表达式
        /// </summary>
        public static ESAndBoolExpression And(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESAndBoolExpression(left, right);
        }

        /// <summary>
        /// 创建Bool或运算表达式
        /// </summary>
        public static ESOrBoolExpression Or(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESOrBoolExpression(left, right);
        }

        /// <summary>
        /// 创建Float比较表达式
        /// </summary>
        public static ESCompareFloatExpression Compare(ESGetFloatExpression left, ESGetFloatExpression right, ESCompareFloatExpression.CompareType compareType)
        {
            return new ESCompareFloatExpression(left, right, compareType);
        }

        #endregion
    }

    #endregion
}
