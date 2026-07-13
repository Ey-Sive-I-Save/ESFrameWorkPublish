using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, InlineProperty]
    public class AudioClipExpressionSource : ESGetAudioClipExpression
    {
        [PropertyOrder(-10), LabelText("直接值"), ToggleLeft]
        public bool useDirectAudioClip = true;

        [PropertyOrder(-9), LabelText("值"), ShowIf(nameof(useDirectAudioClip))]
        public AudioClip directAudioClip;

        [PropertyOrder(-8), LabelText("表达式"), HideIf(nameof(useDirectAudioClip))]
        [PropertyTooltip("动态表达式。允许继续套用 Source，但递归层级需要由配置者控制。")]
        [ESCompactEdit("AudioClip 表达式")]
        [SerializeReference, InlineProperty]
        public ESGetAudioClipExpression expression;

        public AudioClipExpressionSource() { }

        public override AudioClip Evaluate(ESRuntimeTargetPack target, IOperationRuntimeServices support)
        {
            return useDirectAudioClip ? directAudioClip : (expression != null ? expression.Evaluate(target, support) : directAudioClip);
        }

        public void SetDirect(AudioClip value)
        {
            useDirectAudioClip = true;
            directAudioClip = value;
        }
    }
}
