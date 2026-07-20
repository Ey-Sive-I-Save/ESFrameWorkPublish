using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// GameObject value source. It is also a GameObject expression node, so it can be nested in expression trees.
    /// </summary>
    [Serializable, InlineProperty]
    public class GameObjectExpressionSource : ESGetGameObjectExpression
    {
        [PropertyOrder(-10)]
        [LabelText("直接值")]
        [ToggleLeft]
        [PropertyTooltip("启用时直接使用 GameObject 引用；关闭时使用下面的表达式动态解析。")]
        public bool useDirectGameObject = true;

        [PropertyOrder(-9)]
        [LabelText("值")]
        [ShowIf(nameof(useDirectGameObject))]
        public GameObject directGameObject;

        [PropertyOrder(-8)]
        [LabelText("表达式")]
        [HideIf(nameof(useDirectGameObject))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("GameObject 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetGameObjectExpression expression;

        public GameObjectExpressionSource()
        {
        }

        public override GameObject Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            if (useDirectGameObject)
                return directGameObject;

            return expression != null ? expression.Evaluate(target, support) : directGameObject;
        }

        public void SetDirect(GameObject value)
        {
            useDirectGameObject = true;
            directGameObject = value;
        }
    }
}
