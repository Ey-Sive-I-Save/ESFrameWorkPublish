using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Obsolete("已替换为Module<Entity, EntityDomainBase>体系，请使用具体模块基类", false)]
    [Serializable, TypeRegistryItem("实体模块基类(废弃)")]
    public abstract class EntityModuleBase : Module<Entity, EntityDomainBase>
    {
        public sealed override Type TableKeyType => GetType();
    }
}
