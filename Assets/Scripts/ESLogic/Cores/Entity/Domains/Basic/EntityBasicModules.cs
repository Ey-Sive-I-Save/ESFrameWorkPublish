using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础域模块基类")]
    public abstract class EntityBasicModuleBase : Module<Entity, EntityBasicDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("基础移动模块")]
    public class EntityBasicMovementModule : EntityBasicModuleBase
    {
        [Title("演示开关（默认关闭）")]
        public bool enableDemoLogic = false;

        [Title("基础移动参数")]
        public float moveSpeed = 3f;
        public Vector3 desiredMove;

        protected override void Update()
        {
            // 演示用：默认不执行真实移动逻辑，避免影响后续接入 KCC。
            if (!enableDemoLogic) return;
         
        }
    }

    [Serializable, TypeRegistryItem("基础输入模块")]
    public class EntityBasicInputModule : EntityBasicModuleBase
    {
        [Title("演示开关（默认关闭）")]
        public bool enableDemoLogic = false;

        public Vector2 moveInput;

        protected override void Update()
        {
            if (!enableDemoLogic) return;
            if (MyCore?.basicDomain?.movementModule != null)
            {
                MyCore.basicDomain.movementModule.desiredMove = new Vector3(moveInput.x, 0f, moveInput.y);
            }
        }
    }

    [Serializable, TypeRegistryItem("基础功能支持模块")]
    public class EntityBasicSupportModule : EntityBasicModuleBase
    {
        public bool enableSprint;
        public bool enableJump;
    }
}
