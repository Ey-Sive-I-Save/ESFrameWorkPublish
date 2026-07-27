using System;
using System.Collections.Generic;
using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace ES.EditorInternal
{
    /// <summary>
    /// 所有 ESAssetRefer 子类默认内联绘制，不生成额外 Foldout。
    /// </summary>
    public sealed class ESAssetReferInlineProcessor
        : OdinAttributeProcessor<ESAssetReferBase>
    {
        public override void ProcessSelfAttributes(
            InspectorProperty property,
            List<Attribute> attributes)
        {
            attributes.RemoveAll(attribute => attribute is LabelTextAttribute);
            attributes.GetOrAddAttribute<InlinePropertyAttribute>();
            attributes.GetOrAddAttribute<HideLabelAttribute>();
        }
    }
}
