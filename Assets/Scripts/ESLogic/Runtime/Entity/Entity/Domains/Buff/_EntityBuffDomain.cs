using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("Buff域")]
    public class EntityBuffDomain : Domain<Entity, EntityBuffModuleBase>
    {
        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
        }
    }
}
