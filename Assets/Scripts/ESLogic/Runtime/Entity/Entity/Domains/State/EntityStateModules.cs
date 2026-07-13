using System;
using Sirenix.OdinInspector;

namespace ES
{
	[Serializable, TypeRegistryItem("状态域模块基类")]
	public abstract class EntityStateModuleBase : Module<Entity, EntityStateDomain>
	{
		public sealed override Type TableKeyType => GetType();
	}

	// 状态机已由状态域直接持有并驱动
}
