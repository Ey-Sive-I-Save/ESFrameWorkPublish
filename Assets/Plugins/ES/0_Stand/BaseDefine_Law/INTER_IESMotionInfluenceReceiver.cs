using System;
using UnityEngine;

namespace ES
{
    public enum ESMotionFieldKind
    {
        Acceleration = 0,
        Attraction = 1
    }

    public enum ESMotionAttractionModel
    {
        TargetVelocity = 0,
        SpringDamper = 1
    }

    public enum ESMotionAttractionVelocityMode
    {
        RadialOnly = 0,
        FullVelocity = 1
    }

    [Flags]
    public enum ESMotionInfluencePermissions
    {
        None = 0,
        AllowDuringMatchTarget = 1 << 0,
        AllowWhileMounted = 1 << 1,
        AllowWhileClimbing = 1 << 2
    }

    [Flags]
    public enum ESMotionReceiverLockState
    {
        None = 0,
        MatchTarget = 1 << 0,
        Mounted = 1 << 1,
        Climbing = 1 << 2
    }

    [Serializable]
    public struct ESMotionAttractionSettings
    {
        public ESMotionAttractionModel model;
        [Min(0f)] public float stopRadius;
        [Min(0f)] public float maxSpeed;
        [Min(0f)] public float maxAcceleration;
        [Min(0f)] public float response;
        [Min(0f)] public float stiffness;
        [Min(0f)] public float damping;
        [Tooltip("Radial Only 只改变朝向锚点的径向速度；Full Velocity 会同时制动切向速度。")]
        public ESMotionAttractionVelocityMode velocityMode;
    }

    [Serializable]
    public struct ESMotionFieldRequest
    {
        public ESMotionFieldKind kind;
        [Tooltip("Acceleration 模式的世界空间加速度，单位 m/s²。")]
        public Vector3 acceleration;
        [Tooltip("存在时使用 Transform 的实时世界坐标，否则使用 Anchor Position。")]
        public Transform anchorTransform;
        public Vector3 anchorPosition;
        public ESMotionAttractionSettings attraction;
        public ESMotionInfluencePermissions permissions;
        [Tooltip("稳定来源身份。相同优先级的 Field 按此值稳定组合。")]
        public ulong sourceId;
        [Tooltip("较高优先级先参与组合。")]
        public int priority;

        public Vector3 ResolveAnchorPosition()
        {
            return anchorTransform != null ? anchorTransform.position : anchorPosition;
        }
    }

    public interface IESMotionInfluenceReceiver
    {
        bool AddVelocity(
            Vector3 velocity,
            ESMotionInfluencePermissions permissions = ESMotionInfluencePermissions.None);

        bool TryAcquireField(
            in ESMotionFieldRequest request,
            out ESMotionFieldLease lease);
    }

    public interface IESMotionFieldLeaseOwner
    {
        bool ReleaseMotionField(int slotIndex, int slotGeneration, int ownerGeneration);
    }

    public readonly struct ESMotionFieldLease : IDisposable
    {
        private readonly IESMotionFieldLeaseOwner owner;
        private readonly int slotIndex;
        private readonly int slotGeneration;
        private readonly int ownerGeneration;

        public ESMotionFieldLease(
            IESMotionFieldLeaseOwner owner,
            int slotIndex,
            int slotGeneration,
            int ownerGeneration)
        {
            this.owner = owner;
            this.slotIndex = slotIndex;
            this.slotGeneration = slotGeneration;
            this.ownerGeneration = ownerGeneration;
        }

        public bool IsValid => owner != null && slotIndex >= 0 && slotGeneration > 0 && ownerGeneration > 0;

        public void Dispose()
        {
            owner?.ReleaseMotionField(slotIndex, slotGeneration, ownerGeneration);
        }
    }
}
