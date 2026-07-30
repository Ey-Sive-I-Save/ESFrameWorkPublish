using UnityEngine;

namespace ES
{
    /// <summary>
    /// 运动能力在 KCC 原生阶段中的执行顺序。数值越小越先执行。
    /// 这里只保存顺序，不承担运动模式判断；扩展模块可以直接构造自己的顺序。
    /// </summary>
    [System.Serializable]
    public readonly struct EntityKCCMotionOrder
    {
        public readonly int before;
        public readonly int rotation;
        public readonly int velocity;

        public EntityKCCMotionOrder(int before, int rotation, int velocity)
        {
            this.before = before;
            this.rotation = rotation;
            this.velocity = velocity;
        }

        public static readonly EntityKCCMotionOrder Fly = new EntityKCCMotionOrder(100, 120, 120);
        public static readonly EntityKCCMotionOrder Swim = new EntityKCCMotionOrder(110, 130, 130);
        public static readonly EntityKCCMotionOrder Climb = new EntityKCCMotionOrder(120, 110, 110);
        public static readonly EntityKCCMotionOrder Mount = new EntityKCCMotionOrder(130, 100, 100);
    }

    /// <summary>
    /// 一个运动能力在 KCC 三个原生阶段中的注册句柄。
    /// 句柄只在初始化、启停和销毁阶段使用；运行热路径不产生分配。
    /// </summary>
    [System.Serializable]
    public struct EntityKCCMotionRegistration
    {
        public ESWorkHandle beforeHandle;
        public ESWorkHandle rotationHandle;
        public ESWorkHandle velocityHandle;

        public bool IsValid => beforeHandle.IsValid || rotationHandle.IsValid || velocityHandle.IsValid;

        public void Clear()
        {
            beforeHandle = ESWorkHandle.Invalid;
            rotationHandle = ESWorkHandle.Invalid;
            velocityHandle = ESWorkHandle.Invalid;
        }
    }

    public interface IEntityKCCBeforeMotion
    {
        bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, Vector3 initialPosition, float deltaTime);
    }

    public interface IEntityKCCRotationMotion
    {
        bool UpdateRotation(Entity owner, EntityKCCData kcc, Quaternion initialRotation, ref Quaternion currentRotation, float deltaTime);
    }

    public interface IEntityKCCVelocityMotion
    {
        bool UpdateVelocity(Entity owner, EntityKCCData kcc, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime);
    }
}
