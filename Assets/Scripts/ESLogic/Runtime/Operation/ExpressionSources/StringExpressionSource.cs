using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class StringExpressionSource : ESGetStringExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectString = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectString))]
        public string directString;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectString))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("String 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetStringExpression expression;

        public StringExpressionSource() { }

        public override string Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return useDirectString ? directString : (expression != null ? expression.Evaluate(target, support) : directString);
        }

        public void SetDirect(string value)
        {
            useDirectString = true;
            directString = value;
        }
    }
}
