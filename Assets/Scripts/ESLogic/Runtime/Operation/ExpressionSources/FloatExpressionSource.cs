using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Float value source. It is also a float expression node, so it can be nested in expression trees.
    /// </summary>
    [Serializable, InlineProperty]
    public class FloatExpressionSource : ESGetFloatExpression
    {
        [PropertyOrder(-10)]
        [LabelText("直接值")]
        [ToggleLeft]
        [PropertyTooltip("启用时直接读取原生 float；关闭时使用下面的表达式动态计算。")]
        public bool useDirectFloat = true;

        [PropertyOrder(-9)]
        [LabelText("值")]
        [ShowIf(nameof(useDirectFloat))]
        public float directFloat = 1f;

        [PropertyOrder(-8)]
        [LabelText("表达式")]
        [HideIf(nameof(useDirectFloat))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("Float 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetFloatExpression expression;

        public FloatExpressionSource()
        {
        }

        public override float Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            if (useDirectFloat)
                return directFloat;

            return expression != null ? expression.Evaluate(target, support) : directFloat;
        }

        public void SetDirect(float value)
        {
            useDirectFloat = true;
            directFloat = value;
        }
    }
}
