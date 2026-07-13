using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Bool value source. It is also a bool expression node, so it can be nested in expression trees.
    /// </summary>
    [Serializable, InlineProperty]
    public class BoolExpressionSource : ESGetBoolExpression
    {
        [PropertyOrder(-10)]
        [LabelText("直接值")]
        [ToggleLeft]
        [PropertyTooltip("启用时直接读取原生 bool；关闭时使用下面的表达式动态判断。")]
        public bool useDirectBool = true;

        [PropertyOrder(-9)]
        [LabelText("值")]
        [ShowIf(nameof(useDirectBool))]
        public bool directBool = true;

        [PropertyOrder(-8)]
        [LabelText("表达式")]
        [HideIf(nameof(useDirectBool))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("Bool 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetBoolExpression expression;

        public BoolExpressionSource()
        {
        }

        public override bool Evaluate(ESRuntimeTargetPack target, IOperationRuntimeServices support)
        {
            if (useDirectBool)
                return directBool;

            return expression != null ? expression.Evaluate(target, support) : directBool;
        }

        public void SetDirect(bool value)
        {
            useDirectBool = true;
            directBool = value;
        }
    }
}
