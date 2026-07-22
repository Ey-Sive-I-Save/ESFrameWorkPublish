using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct ItemShotSharedData
    {
        [LabelText("启用飞行物")]
        public bool enabled;

        [ShowIf(nameof(enabled))]
        [LabelText("瞄准模式")]
        public ShotAimMode aimMode;

        [ShowIf(nameof(enabled))]
        [LabelText("阻挡模式")]
        public ShotBlockMode blockMode;

        [ShowIf(nameof(enabled)), MinValue(0)]
        [LabelText("发射延迟")]
        public float launchDelay;

        [ShowIf(nameof(enabled)), MinValue(0)]
        [LabelText("预热时间")]
        public float warmupTime;

        [ShowIf(nameof(enabled))]
        [LabelText("速度")]
        public float speed;

        [ShowIf(nameof(enabled))]
        [LabelText("加速度")]
        public float acceleration;

        [ShowIf(nameof(enabled))]
        [LabelText("最大速度")]
        public float maxSpeed;

        [ShowIf(nameof(enabled)), MinValue(0)]
        [LabelText("锁头开始")]
        public float trackingStartTime;

        [ShowIf(nameof(enabled))]
        [LabelText("锁头持续")]
        [Tooltip("小于 0 表示一直锁头；0 表示只按初始方向飞。")]
        public float trackingDuration;

        [ShowIf(nameof(enabled)), MinValue(0)]
        [LabelText("转向速度")]
        public float turnSpeed;

        [ShowIf(nameof(enabled))]
        [LabelText("寿命")]
        public float lifeTime;

        [ShowIf(nameof(enabled))]
        [LabelText("命中半径")]
        public float radius;

        [ShowIf(nameof(enabled))]
        [LabelText("命中层")]
        public LayerMask hitLayers;

        [ShowIf(nameof(enabled))]
        [LabelText("使用重力")]
        public bool useGravity;

        [ShowIf(nameof(enabled))]
        [LabelText("朝向速度方向")]
        public bool orientToVelocity;

        [ShowIf(nameof(enabled))]
        [LabelText("允许必中")]
        public bool allowMustHit;

        public static ItemShotSharedData Default => new ItemShotSharedData
        {
            enabled = true,
            aimMode = ShotAimMode.Free,
            blockMode = ShotBlockMode.AnyBlocker,
            launchDelay = 0f,
            warmupTime = 0f,
            speed = 30f,
            acceleration = 120f,
            maxSpeed = 30f,
            trackingStartTime = 0f,
            trackingDuration = -1f,
            turnSpeed = 720f,
            lifeTime = 5f,
            radius = 0.05f,
            hitLayers = ~0,
            useGravity = false,
            orientToVelocity = true,
            allowMustHit = true
        };

        public ShotMotionConfig ToShotMotionConfig(in ItemShotVariableData variable)
        {
            ShotMotionFlags flags = ShotMotionFlags.ClampSpeed;
            if (useGravity)
                flags |= ShotMotionFlags.UseGravity;
            if (orientToVelocity)
                flags |= ShotMotionFlags.OrientToVelocity;

            float speedScale = Mathf.Max(0f, variable.speedMultiplier);
            float lifetimeScale = Mathf.Max(0f, variable.lifeTimeMultiplier);
            float radiusScale = Mathf.Max(0f, variable.radiusMultiplier);

            return new ShotMotionConfig
            {
                speed = speed * speedScale,
                acceleration = acceleration * speedScale,
                maxSpeed = maxSpeed * speedScale,
                maxLifetime = lifeTime * lifetimeScale,
                launchDelay = variable.overrideLaunchDelay ? Mathf.Max(0f, variable.launchDelay) : launchDelay,
                warmupTime = warmupTime,
                arriveDistance = radius * radiusScale,
                drag = 0f,
                turnSpeedDegrees = turnSpeed,
                trackingStartTime = variable.overrideTrackingStartTime ? Mathf.Max(0f, variable.trackingStartTime) : trackingStartTime,
                trackingDuration = trackingDuration,
                gravity = Physics.gravity,
                flags = flags
            };
        }
    }

    [Serializable]
    public struct ItemShotVariableData
    {
        [LabelText("逻辑随机种子")]
        public int logicSeed;

        [LabelText("速度倍率")]
        public float speedMultiplier;

        [LabelText("寿命倍率")]
        public float lifeTimeMultiplier;

        [LabelText("半径倍率")]
        public float radiusMultiplier;

        [LabelText("强制必中")]
        public bool forceMustHit;

        [LabelText("覆盖发射延迟")]
        public bool overrideLaunchDelay;

        [ShowIf(nameof(overrideLaunchDelay)), MinValue(0)]
        [LabelText("发射延迟")]
        public float launchDelay;

        [LabelText("覆盖锁头开始")]
        public bool overrideTrackingStartTime;

        [ShowIf(nameof(overrideTrackingStartTime)), MinValue(0)]
        [LabelText("锁头开始")]
        public float trackingStartTime;

        [LabelText("目标偏移")]
        public Vector3 targetOffset;

        [LabelText("散射角度")]
        [MinValue(0)]
        public float spreadAngle;

        public static ItemShotVariableData Default => new ItemShotVariableData
        {
            logicSeed = 0,
            speedMultiplier = 1f,
            lifeTimeMultiplier = 1f,
            radiusMultiplier = 1f,
            forceMustHit = false,
            overrideLaunchDelay = false,
            launchDelay = 0f,
            overrideTrackingStartTime = false,
            trackingStartTime = 0f,
            targetOffset = Vector3.zero,
            spreadAngle = 0f
        };
    }
}
