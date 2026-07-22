using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class ItemExpressionSource : ESGetItemExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectItem = true;

        [PropertyOrder(-9), LabelText("Item"), ShowIf(nameof(useDirectItem))]
        public Item directItem;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectItem))]
        [ESCompactEdit("Item 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetItemExpression expression;

        public override Item Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return useDirectItem ? directItem : (expression != null ? expression.Evaluate(target, support) : directItem);
        }

        public void SetDirect(Item value)
        {
            useDirectItem = true;
            directItem = value;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Item/直接引用")]
    public sealed class ESConstantItemExpression : ESGetItemExpression
    {
        [LabelText("Item")]
        public Item value;

        public override Item Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return value;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Item/运行目标/使用者Item")]
    public sealed class ESRuntimeTargetUserItemExpression : ESGetItemExpression
    {
        public override Item Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null ? target.userItem : null;
        }
    }

    [Serializable, TypeRegistryItem("Expression/Item/运行目标/主Item目标")]
    public sealed class ESRuntimeTargetMainItemExpression : ESGetItemExpression
    {
        public override Item Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return target != null ? target.itemMainTarget : null;
        }
    }
}
