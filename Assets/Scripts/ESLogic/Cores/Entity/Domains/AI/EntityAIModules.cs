using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("AI域模块基类")]
    public abstract class EntityAIModuleBase : Module<Entity, EntityAIDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("AI大脑模块")]
    public class EntityAIBrainModule : EntityAIModuleBase
    {
        [Title("演示开关（默认关闭）")]
        public bool enableDemoLogic = false;

        public Vector3 desiredMove;

        protected override void Update()
        {
            if (!enableDemoLogic) return;
            if (MyCore?.basicDomain?.movementModule != null)
            {
                MyCore.basicDomain.movementModule.desiredMove = desiredMove;
            }
        }
    }

    [Serializable, TypeRegistryItem("玩家输入行为模块")]
    public class EntityPlayerInputBehaviorModule : EntityAIModuleBase
    {
        [Title("演示开关（默认关闭）")]
        public bool enableDemoLogic = false;

        public Vector2 simulatedInput;

        protected override void Update()
        {
            if (!enableDemoLogic) return;
            if (MyCore?.basicDomain?.inputModule != null)
            {
                MyCore.basicDomain.inputModule.moveInput = simulatedInput;
            }
        }
    }
}
