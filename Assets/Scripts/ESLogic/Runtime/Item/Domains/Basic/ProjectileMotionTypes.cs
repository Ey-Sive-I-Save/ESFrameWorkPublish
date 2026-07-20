using System;
using UnityEngine;

namespace ES
{
    public enum ItemMotionDriverKind
    {
        Transform = 0,
        Rigidbody = 1,
    }

    public enum ProjectileMotionKind
    {
        None = 0,
        Moving = 1,
        Arrived = 2,
        Expired = 3,
        Blocked = 4,
        Delayed = 5,
        Warmup = 6,
    }

    [Flags]
    public enum ProjectileMotionFlags
    {
        None = 0,
        UseGravity = 1 << 0,
        UseDrag = 1 << 1,
        OrientToVelocity = 1 << 2,
        ClampSpeed = 1 << 3,
    }

    [Serializable]
    public struct ProjectileMotionConfig
    {
        public float speed;
        public float acceleration;
        public float maxSpeed;
        public float maxLifetime;
        public float launchDelay;
        public float warmupTime;
        public float arriveDistance;
        public float drag;
        public float turnSpeedDegrees;
        public float trackingStartTime;
        public float trackingDuration;
        public Vector3 gravity;
        public ProjectileMotionFlags flags;

        public static ProjectileMotionConfig Straight(float speed, float maxLifetime)
        {
            return new ProjectileMotionConfig
            {
                speed = speed,
                acceleration = speed * 4f,
                maxSpeed = speed,
                maxLifetime = maxLifetime,
                launchDelay = 0f,
                warmupTime = 0f,
                arriveDistance = 0.1f,
                drag = 0f,
                turnSpeedDegrees = 720f,
                trackingStartTime = 0f,
                trackingDuration = -1f,
                gravity = Physics.gravity,
                flags = ProjectileMotionFlags.ClampSpeed | ProjectileMotionFlags.OrientToVelocity
            };
        }
    }

    public struct ProjectileMotionState
    {
        public Vector3 previousPosition;
        public Vector3 currentPosition;
        public Quaternion currentRotation;
        public Vector3 velocity;
        public Vector3 direction;
        public Vector3 targetPosition;
        public float elapsedTime;
        public bool hasTarget;
        public bool launched;
    }

    public struct ProjectileHitCandidate
    {
        public Collider collider;
        public Vector3 point;
        public Vector3 normal;
        public Vector3 incomingVelocity;
        public float distance;
        public int layer;
        public bool isTrigger;
    }

    public struct ProjectileMotionResult
    {
        public ProjectileMotionKind kind;
        public Vector3 previousPosition;
        public Vector3 currentPosition;
        public Quaternion currentRotation;
        public Vector3 velocity;
        public float elapsedTime;
        public float remainingDistance;
        public bool hasHitCandidate;
        public ProjectileHitCandidate hitCandidate;
    }
}
