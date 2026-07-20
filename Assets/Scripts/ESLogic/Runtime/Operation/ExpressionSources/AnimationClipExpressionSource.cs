using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class AnimationClipExpressionSource : ESGetAnimationClipExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectAnimationClip = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectAnimationClip))]
        public AnimationClip directAnimationClip;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectAnimationClip))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("AnimationClip 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetAnimationClipExpression expression;

        public AnimationClipExpressionSource() { }

        public override AnimationClip Evaluate(ESRuntimeTargetPack target, ESOpSupport support)
        {
            return useDirectAnimationClip ? directAnimationClip : (expression != null ? expression.Evaluate(target, support) : directAnimationClip);
        }

        public void SetDirect(AnimationClip value)
        {
            useDirectAnimationClip = true;
            directAnimationClip = value;
        }
    }
}
