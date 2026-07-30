using System;
using System.Collections.Generic;
using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace ES.EditorInternal
{
    /// <summary>
    /// ConfigKey 是定义资产与分支配置的首要身份字段；在当前 Odin 容器内始终优先显示。
    /// </summary>
    public sealed class ESConfigKeyPriorityProcessor : OdinAttributeProcessor<IESConfigKey>
    {
        private const float IdentityOrder = -10000f;

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            attributes.RemoveAll(attribute => attribute is PropertyOrderAttribute);
            attributes.Add(new PropertyOrderAttribute(IdentityOrder));
            attributes.GetOrAddAttribute<InlinePropertyAttribute>();
        }
    }
}
