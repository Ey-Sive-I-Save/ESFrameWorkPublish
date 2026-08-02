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
        [Title("ES 多态引用案例", "只使用原生 SerializeReference；Section 负责目录，Reference Drawer 负责类型编辑。", TitleAlignments.Left, true, true)]
        [ESEditorBeginSection("single", "单体引用", -100f, "当前类型、清除、Undo 和安全重选。")]
        [ESFieldPolicy(ESFieldRequirement.Required)]
        [ESFieldHint("这个字段为空时，触发流程不能继续。")]
        [SerializeReference]
        [LabelText("触发效果")]
        public Effect effect = new DamageEffect();

        [ESEditorBeginSection("nested", "嵌套引用", 10f, "CompositeEffect 内部继续使用同一套多态绘制。")]
        [ESFieldPolicy(ESFieldRequirement.Recommended)]
        [ESFieldHint("推荐配置；未设置不会阻止其他字段编辑。")]
        [SerializeReference]
        [LabelText("嵌套效果（CompositeEffect）")]
        public Effect nestedEffect = new CompositeEffect();

        [ESEditorBeginSection("collection", "效果序列", 20f, "集合元素可以独立重选，嵌套元素继续保持层级颜色。")]
        [SerializeReference]
        [ListDrawerSettings(DefaultExpandedState = true)]
        [LabelText("效果序列")]
        public List<Effect> effects = new List<Effect>
        {
            new DamageEffect(),
            new PlayAudioEffect(),
            new CompositeEffect(),
        };

        [ESEditorSection]
        [ESFieldPolicy(ESFieldRequirement.Optional)]
        [ESFieldHint("用于验证未登记类型仍可进入目录。")]
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
