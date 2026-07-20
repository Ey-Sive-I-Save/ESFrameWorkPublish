using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class Vector3ExpressionSource : ESGetVector3Expression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectVector3 = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectVector3))]
        public Vector3 directVector3;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectVector3))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("Vector3 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetVector3Expression expression;

        public Vector3ExpressionSource() { }

        public override Vector3 Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return useDirectVector3 ? directVector3 : (expression != null ? expression.Evaluate(target, support) : directVector3);
        }

        public void SetDirect(Vector3 value)
        {
            useDirectVector3 = true;
            directVector3 = value;
        }
    }
}
