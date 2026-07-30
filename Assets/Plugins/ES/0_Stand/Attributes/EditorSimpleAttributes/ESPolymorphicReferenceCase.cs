using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Add this component to a GameObject to verify the custom SerializeReference presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESPolymorphicReferenceCase : MonoBehaviour
    {
        [Title("多态引用案例", "Unity 继续保存 SerializeReference；ES 只替换类型选择与对象编辑的呈现。", TitleAlignments.Left, true, true)]
        [SerializeReference]
        [ESPolymorphicReference("触发效果", Subtitle = "选择一个效果类型，再填写该类型的业务字段。")]
        public Effect effect;

        [Serializable]
        public abstract class Effect
        {
        }

        [Serializable]
        [ESPolymorphicType("固定伤害", "数值", "对目标施加固定数值的伤害。", Order = 10)]
        public sealed class DamageEffect : Effect
        {
            [LabelText("伤害数值"), MinValue(0f)]
            public float amount = 10f;

            [LabelText("忽略护甲")]
            public bool ignoreArmor;
        }

        [Serializable]
        [ESPolymorphicType("生命恢复", "数值", "为目标恢复指定生命值。", Order = 20)]
        public sealed class HealEffect : Effect
        {
            [LabelText("恢复数值"), MinValue(0f)]
            public float amount = 5f;
        }

        [Serializable]
        [ESPolymorphicType("播放音效", "表现", "在目标位置播放一次性音效。", Order = 10)]
        public sealed class PlayAudioEffect : Effect
        {
            [LabelText("音效")]
            public AudioClip clip;

            [LabelText("音量"), Range(0f, 1f)]
            public float volume = 1f;
        }
    }
}
