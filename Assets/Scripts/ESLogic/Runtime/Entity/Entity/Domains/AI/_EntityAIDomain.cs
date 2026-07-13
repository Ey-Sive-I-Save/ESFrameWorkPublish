using System;
using Sirenix.OdinInspector;

namespace ES
{
	[Serializable, TypeRegistryItem("AI域")]
	public class EntityAIDomain : Domain<Entity, EntityAIModuleBase>
	{
		public override void _AwakeRegisterAllModules()
		{
			base._AwakeRegisterAllModules();
		}
	}
}
