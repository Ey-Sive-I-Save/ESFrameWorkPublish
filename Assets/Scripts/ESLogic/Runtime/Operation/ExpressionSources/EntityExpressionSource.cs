using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class EntityExpressionSource : ESGetEntityExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectEntity = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectEntity))]
        public Entity directEntity;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectEntity))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("Entity 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetEntityExpression expression;

        public EntityExpressionSource() { }

        public override Entity Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return useDirectEntity ? directEntity : (expression != null ? expression.Evaluate(target, support) : directEntity);
        }

        public void SetDirect(Entity value)
        {
            useDirectEntity = true;
            directEntity = value;
        }
    }
}
