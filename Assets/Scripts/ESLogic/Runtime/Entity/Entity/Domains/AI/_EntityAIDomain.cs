using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
	[Serializable, TypeRegistryItem("AI域")]
	public class EntityAIDomain : Domain<Entity, EntityAIModuleBase>
	{
		[Title("输入状态")]
		[HideLabel]
		public EntityInputState inputState = new EntityInputState();

		[Title("玩家输入原型")]
		[LabelText("自动确保玩家输入链路")]
		[Tooltip("开启后：运行时如果 AI 域缺少玩家输入写入、输入调度模块，会自动创建并加入。建议只给当前可控玩家实体开启。")]
		public bool autoEnsurePlayerInputModules = false;

		public override void _AwakeRegisterAllModules()
		{
			inputState ??= new EntityInputState();

			if (autoEnsurePlayerInputModules)
				EnsurePlayerInputModulesExist();

			base._AwakeRegisterAllModules();
		}

		protected override void OnDisable()
		{
			inputState?.ClearAll();
			base.OnDisable();
		}

		[Button("确保玩家输入链路存在"), PropertyOrder(-10)]
		public void EnsurePlayerInputModulesExist()
		{
			EnsureModuleExists<EntityPlayerInputWriteModule>();
			EnsureModuleExists<EntityAIInputDispatchModule>();
			MyModules.ApplyBuffers(true);
		}

		private void EnsureModuleExists<T>() where T : EntityAIModuleBase, new()
		{
			if (FindMyModule<T>() != null)
				return;

			MyModules.Add(new T());
		}
	}
}
