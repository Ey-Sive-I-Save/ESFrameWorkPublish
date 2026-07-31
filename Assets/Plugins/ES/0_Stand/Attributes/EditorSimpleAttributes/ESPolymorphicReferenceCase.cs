using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Add this component to a GameObject to verify automatic SerializeReference presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESPolymorphicReferenceCase : MonoBehaviour
    {
        [Title("多态引用案例", "只使用原生 SerializeReference。案例覆盖重选、Undo、快速清除、集合和二层嵌套。", TitleAlignments.Left, true, true)]
        [SerializeReference]
        [LabelText("触发效果")]
        public Effect effect = new DamageEffect();

        [Title("二层嵌套", "展开 CompositeEffect，可继续编辑 primary 与 alternatives 中的多态对象。", TitleAlignments.Left, true, true)]
        [SerializeReference]
        [LabelText("嵌套效果（CompositeEffect）")]
        public Effect nestedEffect = new CompositeEffect();

        [Title("集合与集合内嵌套", "列表元素可单独重选；CompositeEffect 元素还会继续展开第二层。", TitleAlignments.Left, true, true)]
        [SerializeReference]
        [ListDrawerSettings(DefaultExpandedState = true)]
        [LabelText("效果序列")]
        public List<Effect> effects = new List<Effect>
        {
            new DamageEffect(),
            new PlayAudioEffect(),
            new CompositeEffect(),
        };

        [SerializeReference]
        [LabelText("未登记类型验证")]
        public Effect unregisteredEffect;

        [Serializable]
        public abstract class Effect
        {
        }

        [Serializable]
        [TypeRegistryItem("数值/固定伤害")]
        public sealed class DamageEffect : Effect
        {
            [LabelText("伤害数值"), MinValue(0f)]
            public float amount = 10f;

            [LabelText("忽略护甲")]
            public bool ignoreArmor;
        }

        [Serializable]
        [TypeRegistryItem("数值/生命恢复")]
        public sealed class HealEffect : Effect
        {
            [LabelText("恢复数值"), MinValue(0f)]
            public float amount = 5f;
        }

        [Serializable]
        [TypeRegistryItem("表现/播放音效")]
        public sealed class PlayAudioEffect : Effect
        {
            [LabelText("音效")]
            public AudioClip clip;

            [LabelText("音量"), Range(0f, 1f)]
            public float volume = 1f;
        }

        [Serializable]
        [TypeRegistryItem("组合/复合效果")]
        public sealed class CompositeEffect : Effect
        {
            [Title("复合效果内部", "下面两个节点各自都是可重选的 SerializeReference。", TitleAlignments.Left, true, true)]
            [LabelText("主效果")]
            [SerializeReference]
            public Effect primary = new HealEffect();

            [LabelText("备用效果")]
            [SerializeReference]
            [ListDrawerSettings(DefaultExpandedState = true)]
            public List<Effect> alternatives = new List<Effect>
            {
                new DamageEffect(),
                new PlayAudioEffect(),
            };
        }

        [Serializable]
        public sealed class UnregisteredEffect : Effect
        {
            [LabelText("备注")]
            public string note = "没有 TypeRegistryItem 的类型仍可选择，但会归入“未登记类型”。";
        }
    }
}
