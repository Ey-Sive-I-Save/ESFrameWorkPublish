using ES;
using Sirenix.OdinInspector;
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
        public abstract TOut Evaluate(ESRuntimeTargetPack target, ESOpSupport support);
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

    /// <summary>Int 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetIntExpression : ESGetExpression<int>
    {
    }

    /// <summary>Vector3 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetVector3Expression : ESGetExpression<Vector3>
    {
    }

    /// <summary>String 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetStringExpression : ESGetExpression<string>
    {
    }

    /// <summary>Entity 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetEntityExpression : ESGetExpression<Entity>
    {
    }

    [Serializable]
    public abstract class ESGetItemExpression : ESGetExpression<Item>
    {
    }

    /// <summary>AnimationClip 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetAnimationClipExpression : ESGetExpression<AnimationClip>
    {
    }

    /// <summary>AudioClip 取值表达式基类。</summary>
    [Serializable]
    public abstract class ESGetAudioClipExpression : ESGetExpression<AudioClip>
    {
    }

    #region Float 表达式实现

    /// <summary>返回固定 float 值。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantFloat)]
    public class ESConstantFloatExpression : ESGetFloatExpression
    {
        [LabelText("值")]
        public float value;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    /// <summary>将两个 float 表达式结果相加。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.AddFloat)]
    public class ESAddFloatExpression : ESGetFloatExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetFloatExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetFloatExpression right;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            float leftValue = left != null ? left.Evaluate(target, support) : 0f;
            float rightValue = right != null ? right.Evaluate(target, support) : 0f;
            return leftValue + rightValue;
        }
    }

    /// <summary>将两个 float 表达式结果相乘。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.MultiplyFloat)]
    public class ESMultiplyFloatExpression : ESGetFloatExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetFloatExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetFloatExpression right;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            float leftValue = left != null ? left.Evaluate(target, support) : 0f;
            float rightValue = right != null ? right.Evaluate(target, support) : 0f;
            return leftValue * rightValue;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Math/Subtract")]
    public class ESSubtractFloatExpression : ESGetFloatExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetFloatExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetFloatExpression right;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            float leftValue = left != null ? left.Evaluate(target, support) : 0f;
            float rightValue = right != null ? right.Evaluate(target, support) : 0f;
            return leftValue - rightValue;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Math/Divide")]
    public class ESDivideFloatExpression : ESGetFloatExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetFloatExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetFloatExpression right;

        public ESFloatDivideZeroMode divideZeroMode = ESFloatDivideZeroMode.ReturnZero;

        public ESFloatDivideExpressionFallback fallback = new ESFloatDivideExpressionFallback();

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            float leftValue = left != null ? left.Evaluate(target, support) : 0f;
            float rightValue = right != null ? right.Evaluate(target, support) : 0f;
            if (!Mathf.Approximately(rightValue, 0f))
                return leftValue / rightValue;

            switch (divideZeroMode)
            {
                case ESFloatDivideZeroMode.ReturnOne:
                    return 1f;
                case ESFloatDivideZeroMode.ReturnLeft:
                    return leftValue;
                case ESFloatDivideZeroMode.ReturnFallback:
                    return fallback != null ? fallback.value : 0f;
                default:
                    return 0f;
            }
        }
    }

    [Serializable]
    public enum ESFloatDivideZeroMode
    {
        ReturnZero,
        ReturnOne,
        ReturnLeft,
        ReturnFallback
    }

    [Serializable, InlineProperty]
    public class ESFloatDivideExpressionFallback
    {
        public float value;
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Math/Clamp")]
    public class ESClampFloatExpression : ESGetFloatExpression
    {
        [SerializeReference, InlineProperty, LabelText("输入"), ESCompactEdit("输入")]
        public ESGetFloatExpression input;

        [LabelText("最小值")]
        public float min = 0f;

        [LabelText("最大值")]
        public float max = 1f;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            float value = input != null ? input.Evaluate(target, support) : 0f;
            return Mathf.Clamp(value, min, max);
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/RuntimeTarget/RuntimeFloat")]
    public class ESRuntimeTargetFloatExpression : ESGetFloatExpression
    {
        public float defaultValue = 1f;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null ? target.runtimeFloat : defaultValue;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Context/GetFloat")]
    public class ESContextFloatExpression : ESGetFloatExpression
    {
        [LabelText("Key")]
        public string key;

        [LabelText("默认值")]
        public float defaultValue;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return support != null && support.Context != null
                ? support.Context.GetFloat(key, defaultValue)
                : defaultValue;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Random/Range")]
    public class ESRandomRangeFloatExpression : ESGetFloatExpression
    {
        public float min = 0f;
        public float max = 1f;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return UnityEngine.Random.Range(min, max);
        }
    }

    [Serializable, TypeRegistryItem("Expression/Value/Float/Entity/DistanceUserToMainTarget")]
    public class ESDistanceUserToMainTargetFloatExpression : ESGetFloatExpression
    {
        public float defaultValue;

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            Entity user = target != null ? target.GetUserEntity() : null;
            Entity mainTarget = target != null ? target.GetMainTarget() : null;
            if (user == null || mainTarget == null)
                return defaultValue;

            return Vector3.Distance(user.transform.position, mainTarget.transform.position);
        }
    }

    #endregion

    #region Bool 表达式实现

    /// <summary>返回固定 bool 值。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantBool)]
    public class ESConstantBoolExpression : ESGetBoolExpression
    {
        [LabelText("值")]
        public bool value = true;

        public override bool Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    /// <summary>对两个 bool 表达式执行 AND。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.AndBool)]
    public class ESAndBoolExpression : ESGetBoolExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetBoolExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetBoolExpression right;

        public override bool Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return left != null
                   && right != null
                   && left.Evaluate(target, support)
                   && right.Evaluate(target, support);
        }
    }

    /// <summary>对两个 bool 表达式执行 OR。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.OrBool)]
    public class ESOrBoolExpression : ESGetBoolExpression
    {
        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetBoolExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetBoolExpression right;

        public override bool Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return (left != null && left.Evaluate(target, support))
                   || (right != null && right.Evaluate(target, support));
        }
    }

    /// <summary>比较两个 float 表达式并返回 bool。</summary>
    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.CompareFloat)]
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

        [SerializeReference, InlineProperty, LabelText("左"), ESCompactEdit("左")]
        public ESGetFloatExpression left;

        [SerializeReference, InlineProperty, LabelText("右"), ESCompactEdit("右")]
        public ESGetFloatExpression right;

        [LabelText("比较")]
        public CompareType compareType = CompareType.GreaterEqual;

        public override bool Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            if (left == null || right == null)
                return false;

            float leftValue = left.Evaluate(target, support);
            float rightValue = right.Evaluate(target, support);

            switch (compareType)
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

    #region Other 常量表达式实现

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantInt)]
    public class ESConstantIntExpression : ESGetIntExpression
    {
        [LabelText("值")]
        public int value;

        public override int Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantVector3)]
    public class ESConstantVector3Expression : ESGetVector3Expression
    {
        [LabelText("值")]
        public Vector3 value;

        public override Vector3 Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantString)]
    public class ESConstantStringExpression : ESGetStringExpression
    {
        [LabelText("值")]
        public string value;

        public override string Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.DirectEntity)]
    public class ESConstantEntityExpression : ESGetEntityExpression
    {
        [LabelText("值")]
        public Entity value;

        public override Entity Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantAnimationClip)]
    public class ESConstantAnimationClipExpression : ESGetAnimationClipExpression
    {
        [LabelText("值")]
        public AnimationClip value;

        public override AnimationClip Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem(ExpressionTypeRegistryNames.ConstantAudioClip)]
    public class ESConstantAudioClipExpression : ESGetAudioClipExpression
    {
        [LabelText("值")]
        public AudioClip value;

        public override AudioClip Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
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
            return new ESConstantFloatExpression { value = value };
        }

        public static ESAddFloatExpression Add(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESAddFloatExpression { left = left, right = right };
        }

        public static ESMultiplyFloatExpression Multiply(ESGetFloatExpression left, ESGetFloatExpression right)
        {
            return new ESMultiplyFloatExpression { left = left, right = right };
        }

        #endregion

        #region Bool 表达式构建

        public static ESConstantBoolExpression Constant(bool value)
        {
            return new ESConstantBoolExpression { value = value };
        }

        public static ESAndBoolExpression And(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESAndBoolExpression { left = left, right = right };
        }

        public static ESOrBoolExpression Or(ESGetBoolExpression left, ESGetBoolExpression right)
        {
            return new ESOrBoolExpression { left = left, right = right };
        }

        public static ESCompareFloatExpression Compare(ESGetFloatExpression left, ESGetFloatExpression right, ESCompareFloatExpression.CompareType compareType)
        {
            return new ESCompareFloatExpression { left = left, right = right, compareType = compareType };
        }

        #endregion
    }

    #endregion
}
