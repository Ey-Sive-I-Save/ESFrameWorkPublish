using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("状态域")]
    public class EntityStateDomain : Domain<Entity, EntityStateModuleBase>
    {
        // 注意：Domain 内不推荐 public 模块字段。
        // 如需超高频访问，允许 public 但必须禁止序列化（NonSerialized）。
        [NonSerialized] public EntityStateMachineModule stateMachineModule;

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            // 仅做缓存：不负责注入、也不要求一定存在
            stateMachineModule = FindMyModule<EntityStateMachineModule>();
        }
    }
}
