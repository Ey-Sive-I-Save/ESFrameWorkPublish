using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("Buff域模块基类")]
    public abstract class EntityBuffModuleBase : Module<Entity, EntityBuffDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }
}
