using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Obsolete("示例模块，建议放入具体域模块脚本中", false)]
    [Serializable, TypeRegistryItem("模拟标签模块")]
    public class EntityMockTagModule : EntityBasicModuleBase
    {
        public string tagName = "Demo";

        public void SetTag(string value)
        {
            tagName = value;
        }
    }
}
