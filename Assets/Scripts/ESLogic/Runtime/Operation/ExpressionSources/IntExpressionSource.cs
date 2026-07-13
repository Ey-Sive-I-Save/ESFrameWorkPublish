using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class IntExpressionSource : ESGetIntExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectInt = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectInt))]
        public int directInt;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectInt))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("Int 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetIntExpression expression;

        public IntExpressionSource() { }

        public override int Evaluate(ESRuntimeTargetPack target, IOperationRuntimeServices support)
        {
            return useDirectInt ? directInt : (expression != null ? expression.Evaluate(target, support) : directInt);
        }

        public void SetDirect(int value)
        {
            useDirectInt = true;
            directInt = value;
        }
    }
}
