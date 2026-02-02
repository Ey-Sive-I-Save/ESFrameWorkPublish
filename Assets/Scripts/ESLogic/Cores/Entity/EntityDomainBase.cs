using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Obsolete("已替换为Domain<Entity, EntityModuleBase>体系，请使用具体域", false)]
    [Serializable, TypeRegistryItem("实体域基类(废弃)")]
    public abstract class EntityDomainBase : Domain<Entity, EntityModuleBase>
    {
    }
}
